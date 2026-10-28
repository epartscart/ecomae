using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Posted-ledger figures for the ERP home: balances by COA code band (cash 10xx, AR 1100-1149,
/// VAT input 1150-1199, inventory 13xx, AP 2000-2099, VAT output 2100-2199) and period revenue /
/// expense by account type. Fills any dashboard figure the operational tables leave at zero, so
/// ERP-only tenants (jewellery, INDUS imports) show the same numbers as the trial balance.
/// </summary>
public sealed record ErpDashboardGlBook(
    decimal Cash,
    decimal Receivables,
    decimal Inventory,
    decimal Payables,
    decimal VatNet,
    decimal Revenue,
    decimal Expense)
{
    public static readonly ErpDashboardGlBook Empty = new(0, 0, 0, 0, 0, 0, 0);

    public bool HasData => Cash != 0 || Receivables != 0 || Inventory != 0 || Payables != 0 || Revenue != 0 || Expense != 0;

    public static string BuildSql(int companyId, bool includeUnassignedZero)
    {
        var scope = companyId <= 0
            ? string.Empty
            : includeUnassignedZero
                ? " AND (j.`company_id` = @companyId OR IFNULL(j.`company_id`, 0) = 0)"
                : " AND j.`company_id` = @companyId";
        return $"""
            SELECT
                IFNULL(SUM(CASE WHEN a.`code` LIKE '10%' AND j.`journal_date` <= @dateTo THEN l.`debit` - l.`credit` ELSE 0 END), 0) AS cash,
                IFNULL(SUM(CASE WHEN a.`code` BETWEEN '1100' AND '1149' AND j.`journal_date` <= @dateTo THEN l.`debit` - l.`credit` ELSE 0 END), 0) AS ar,
                IFNULL(SUM(CASE WHEN a.`code` LIKE '13%' AND j.`journal_date` <= @dateTo THEN l.`debit` - l.`credit` ELSE 0 END), 0) AS inv,
                IFNULL(SUM(CASE WHEN a.`code` BETWEEN '2000' AND '2099' AND j.`journal_date` <= @dateTo THEN l.`credit` - l.`debit` ELSE 0 END), 0) AS ap,
                IFNULL(SUM(CASE WHEN a.`code` BETWEEN '2100' AND '2199' AND j.`journal_date` <= @dateTo THEN l.`credit` - l.`debit`
                                WHEN a.`code` BETWEEN '1150' AND '1199' AND j.`journal_date` <= @dateTo THEN l.`credit` - l.`debit` ELSE 0 END), 0) AS vat,
                IFNULL(SUM(CASE WHEN a.`account_type` = 'revenue' AND j.`journal_date` BETWEEN @dateFrom AND @dateTo THEN l.`credit` - l.`debit` ELSE 0 END), 0) AS rev,
                IFNULL(SUM(CASE WHEN a.`account_type` = 'expense' AND j.`journal_date` BETWEEN @dateFrom AND @dateTo THEN l.`debit` - l.`credit` ELSE 0 END), 0) AS exp
            FROM `epc_erp_gl_lines` l
            INNER JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1
            INNER JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id`
            WHERE 1 = 1{scope}
            """;
    }

    public static async Task<ErpDashboardGlBook> ReadAsync(
        DbConnection connection, long dateFrom, long dateTo, int companyId, bool includeUnassignedZero, CancellationToken ct)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            ErpFirstPaint.ApplyIfErp(cmd);
            cmd.CommandText = BuildSql(companyId, includeUnassignedZero);
            Add(cmd, "@dateFrom", dateFrom);
            Add(cmd, "@dateTo", dateTo);
            if (companyId > 0)
            {
                Add(cmd, "@companyId", companyId);
            }

            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await r.ReadAsync(ct).ConfigureAwait(false))
            {
                return Empty;
            }

            decimal D(string k) => r[k] is DBNull ? 0m : Convert.ToDecimal(r[k], CultureInfo.InvariantCulture);
            return new(D("cash"), D("ar"), D("inv"), D("ap"), D("vat"), D("rev"), D("exp"));
        }
        catch (DbException)
        {
            return Empty;
        }
    }

    public static ErpWorkspacePeriodKpis Apply(ErpWorkspacePeriodKpis k, ErpDashboardGlBook gl)
    {
        if (!gl.HasData)
        {
            return k;
        }

        static decimal Pick(decimal op, decimal ledger) => op != 0 ? op : Math.Round(ledger, 2);
        var revenue = Pick(k.RevenueExVat, gl.Revenue);
        var purchase = Pick(k.PurchaseExVat, gl.Expense);
        var glNet = Math.Round(gl.Revenue - gl.Expense, 2);
        return k with
        {
            CashPosition = Pick(k.CashPosition, gl.Cash),
            RevenueExVat = revenue,
            PurchaseExVat = purchase,
            ProfitExVat = k.ProfitExVat != 0 ? k.ProfitExVat : Math.Round(revenue - purchase, 2),
            Receivables = Pick(k.Receivables, gl.Receivables),
            ArBalance = Pick(k.ArBalance, gl.Receivables),
            ReceivableDueOrders = Pick(k.ReceivableDueOrders, gl.Receivables),
            Payables = Pick(k.Payables, gl.Payables),
            ApBalance = Pick(k.ApBalance, gl.Payables),
            StockValue = Pick(k.StockValue, gl.Inventory),
            VatNetPayable = Pick(k.VatNetPayable, gl.VatNet),
            SalesInclVat = Pick(k.SalesInclVat, gl.Revenue),
            GlNetProfit = k.GlNetProfit != 0 ? k.GlNetProfit : glNet,
        };
    }

    private const string SaleTypes = "'RIN','RSI','RSC','MSL','MSI','MSC','RSL','SI'";

    /// <summary>ERP / jewellery operational counts for tenants without storefront order tables.</summary>
    public static async Task<ErpDashboardOps> ReadOpsAsync(DbConnection connection, long dateFrom, long dateTo, int companyId, CancellationToken ct)
    {
        var co = companyId > 0 ? " AND IFNULL(`company_id`, 0) IN (0, @companyId)" : string.Empty;
        var day = DateTimeOffset.FromUnixTimeSeconds(dateTo).UtcDateTime.Date;
        async Task<int> N(string sql, params (string Name, object Value)[] args)
        {
            try
            {
                await using var cmd = connection.CreateCommand();
                ErpFirstPaint.ApplyIfErp(cmd);
                cmd.CommandText = sql;
                foreach (var (name, value) in args) Add(cmd, name, value);
                if (companyId > 0 && sql.Contains("@companyId", StringComparison.Ordinal)) Add(cmd, "@companyId", companyId);
                var o = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                return o is null or DBNull ? 0 : Convert.ToInt32(o, CultureInfo.InvariantCulture);
            }
            catch (DbException)
            {
                return 0;
            }
        }

        var from = DateTimeOffset.FromUnixTimeSeconds(dateFrom).UtcDateTime.Date;
        var sales = $"SELECT COUNT(*) FROM `epc_jewel_voucher` WHERE `voc_type` IN ({SaleTypes}){co} AND `voc_date` BETWEEN @d1 AND @d2";
        var orders = await N(sales, ("@d1", from), ("@d2", day)).ConfigureAwait(false)
            + await N("SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `time_created` BETWEEN @t1 AND @t2", ("@t1", dateFrom), ("@t2", dateTo)).ConfigureAwait(false);
        return new ErpDashboardOps(
            orders,
            await N("SELECT COUNT(*) FROM `epc_jewel_metal_stock` WHERE 1=1" + co).ConfigureAwait(false)
                + await N("SELECT COUNT(*) FROM `epc_erp_inv_items` WHERE `active` = 1").ConfigureAwait(false),
            await N("SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'draft'").ConfigureAwait(false),
            await N("SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'confirmed'").ConfigureAwait(false),
            await N("SELECT COUNT(*) FROM `epc_erp_purchases` WHERE `active` = 1 AND `status` IN ('draft','confirmed','open')").ConfigureAwait(false),
            await N($"SELECT COUNT(*) FROM `epc_jewel_voucher` WHERE `voc_type` IN ({SaleTypes}){co} AND IFNULL(`total_with_vat`,0) > IFNULL(`receipt_total`,0) + 0.005").ConfigureAwait(false),
            await N("SELECT COUNT(*) FROM `epc_jewel_metal_stock` WHERE IFNULL(`reorder_level`,0) > 0 AND IFNULL(`stock_qty`,0) < `reorder_level`" + co).ConfigureAwait(false),
            await N(sales, ("@d1", day), ("@d2", day)).ConfigureAwait(false),
            await N(sales, ("@d1", day.AddDays(-6)), ("@d2", day)).ConfigureAwait(false),
            await N(sales, ("@d1", day.AddDays(-13)), ("@d2", day.AddDays(-7))).ConfigureAwait(false),
            await N("SELECT COUNT(*) FROM `epc_jewel_pos_customer` WHERE 1=1" + co).ConfigureAwait(false)
                + await N("SELECT COUNT(*) FROM `epc_jewel_party` WHERE 1=1" + co).ConfigureAwait(false),
            await N("SELECT COUNT(*) FROM `epc_rma_requests` WHERE IFNULL(`status`,'') NOT IN ('closed','completed','rejected')").ConfigureAwait(false),
            await N("SELECT COUNT(*) FROM `epc_erp_inv_warehouses` WHERE `active` = 1").ConfigureAwait(false),
            await N("SELECT COUNT(*) FROM `epc_price_settings`").ConfigureAwait(false));
    }

    public static ErpWorkspacePeriodKpis ApplyOps(ErpWorkspacePeriodKpis k, ErpDashboardOps o)
    {
        static int Pick(int op, int erp) => op != 0 ? op : erp;
        return k with
        {
            OrdersCount = Pick(k.OrdersCount, o.Orders),
            InventoryItems = Pick(k.InventoryItems, o.Items),
            DraftSalesOrders = Pick(k.DraftSalesOrders, o.DraftSalesOrders),
            ConfirmedSalesOrders = Pick(k.ConfirmedSalesOrders, o.ConfirmedSalesOrders),
            OpenPurchaseOrders = Pick(k.OpenPurchaseOrders, o.OpenPurchases),
            InvoicesDue = Pick(k.InvoicesDue, o.InvoicesDue),
            LowStockItems = Pick(k.LowStockItems, o.LowStock),
        };
    }

    public static ErpInsightsCommerceStats ApplyCommerce(ErpInsightsCommerceStats c, ErpDashboardOps o)
    {
        static int Pick(int op, int erp) => op != 0 ? op : erp;
        return c with
        {
            OrdersToday = Pick(c.OrdersToday, o.OrdersToday),
            OrdersWeek = Pick(c.OrdersWeek, o.OrdersWeek),
            OrdersPrevWeek = Pick(c.OrdersPrevWeek, o.OrdersPrevWeek),
            OpenOrders = Pick(c.OpenOrders, o.DraftSalesOrders + o.ConfirmedSalesOrders),
            Products = Pick(c.Products, o.Items),
            Clients = Pick(c.Clients, o.Clients),
            ReturnsOpen = Pick(c.ReturnsOpen, o.ReturnsOpen),
            Warehouses = Pick(c.Warehouses, o.Warehouses),
            PriceLists = Pick(c.PriceLists, o.PriceLists),
        };
    }

    private static void Add(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}

public sealed record ErpDashboardOps(
    int Orders, int Items, int DraftSalesOrders, int ConfirmedSalesOrders, int OpenPurchases, int InvoicesDue, int LowStock,
    int OrdersToday, int OrdersWeek, int OrdersPrevWeek, int Clients, int ReturnsOpen, int Warehouses, int PriceLists);
