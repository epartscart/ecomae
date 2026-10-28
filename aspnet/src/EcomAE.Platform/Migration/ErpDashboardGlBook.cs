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

    private static void Add(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
