using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpProcurementDashboard(
    int Suppliers,
    int SuppliersWithTrn,
    int PurchaseInvoices,
    decimal PayableBalance,
    int Warehouses,
    int WarehousesLinked,
    decimal AdvancesPaid);

public sealed record CpProcurementSupplier(
    long Id,
    string Name,
    string FullName,
    string VendorCode,
    string DisplayLabel,
    string Trn,
    string CountryCode,
    bool VatRegistered,
    long StorageId,
    string WarehouseName,
    decimal Balance,
    string ContactEmail,
    string ContactPhone,
    string LegalRegNo,
    string LegalRegType,
    string AuthorityName,
    string AddressLine1,
    string City,
    string Emirate,
    string PaymentTerms,
    string Notes);

public sealed record CpProcurementCashAccount(long Id, string Name, decimal Balance);

public sealed record CpProcurementPurchase(
    long Id,
    long PurchaseDate,
    long SupplierId,
    string SupplierName,
    string InvoiceNumber,
    long OrderId,
    decimal AmountExVat,
    decimal VatAmount,
    decimal TotalAmount);

public sealed record CpProcurementAdvance(long Id, long Time, string SupplierName, decimal Amount, string Reference, string Note);

public sealed record CpProcurementWarehouse(long Id, string Name, string VendorCode, long SupplierId);

public sealed record CpProcurementOrderLink(
    long OrderId,
    long Time,
    long UserId,
    string CustomerEmail,
    int LineCount,
    decimal PurchaseEx,
    decimal SaleEx,
    int PurchaseBills);

public sealed record CpProcurementFulfilment(int TotalOrders, int DeliveryDone, int ReturnsOpen);

public sealed record CpProcurementDesk(
    bool Available,
    string Error,
    CpProcurementDashboard Dashboard,
    IReadOnlyList<CpProcurementSupplier> Suppliers,
    IReadOnlyList<CpProcurementCashAccount> Accounts,
    IReadOnlyList<CpProcurementPurchase> Purchases,
    IReadOnlyList<CpProcurementAdvance> Advances,
    IReadOnlyList<CpProcurementWarehouse> Warehouses,
    CpProcurementSupplier? EditSupplier,
    IReadOnlyList<CpProcurementOrderLink> SupplierOrders,
    CpProcurementFulfilment Fulfilment,
    decimal VatRate)
{
    public static CpProcurementDesk Unavailable(string error) =>
        new(false, error, new CpProcurementDashboard(0, 0, 0, 0m, 0, 0, 0m), [], [], [], [], [], null, [], new CpProcurementFulfilment(0, 0, 0), 5m);
}

/// <summary>
/// Read twin of PHP <c>content/shop/procurement/epc_procurement_helpers.php</c> +
/// the <c>epc_erp_list_*</c> reads that <c>cp/content/shop/procurement/procurement_main.php</c> renders.
/// </summary>
public interface ICpProcurementDeskService
{
    Task<CpProcurementDesk> LoadAsync(string tab, long supplierId, CancellationToken cancellationToken = default);
}

public sealed class CpProcurementDeskService : ICpProcurementDeskService
{
    public static readonly IReadOnlyList<(string Key, string Label)> Tabs =
    [
        ("dashboard", "Dashboard"),
        ("suppliers", "Suppliers"),
        ("purchases", "Purchase bills"),
        ("payments", "Payments"),
        ("advances", "Advances"),
        ("fulfillment", "Fulfillment"),
        ("warehouses", "Warehouses"),
        ("guide", "Guide"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> LegalRegTypes =
    [
        ("TL", "Trade licence"),
        ("EID", "Emirates ID"),
        ("PAS", "Passport"),
        ("CD", "Other"),
    ];

    private static readonly string[] SupplierTabs = ["suppliers", "purchases", "payments", "advances", "fulfillment"];

    /// <summary>PHP <c>epc_procurement_normalize_vendor_code</c>: strips a trailing "(old)" marker.</summary>
    public static string NormalizeVendorCode(string? code)
    {
        var value = (code ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var idx = value.LastIndexOf("(old)", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0 && value[(idx + 5)..].Trim().Length == 0)
        {
            value = value[..idx];
        }

        return value.Trim();
    }

    /// <summary>PHP <c>epc_procurement_supplier_label</c>: "Full Legal Name [VENDOR-CODE]".</summary>
    public static string SupplierLabel(long id, string? fullName, string? vendorCode)
    {
        var full = (fullName ?? string.Empty).Trim();
        var code = NormalizeVendorCode(vendorCode);
        if (full.Length == 0)
        {
            full = code.Length > 0 ? code : "Supplier #" + id.ToString(CultureInfo.InvariantCulture);
        }

        return code.Length > 0 && !string.Equals(code, full, StringComparison.OrdinalIgnoreCase)
            ? full + " [" + code + "]"
            : full;
    }

    /// <summary>PHP <c>epc_procurement_enrich_supplier</c> full-name / vendor-code resolution.</summary>
    public static (string FullName, string VendorCode) ResolveNames(
        long id,
        string? name,
        string? vendorCode,
        string? vendorAccount,
        string? warehouseFull,
        string? warehouseShort)
    {
        var whFull = (warehouseFull ?? string.Empty).Trim();
        var whShort = NormalizeVendorCode(warehouseShort);
        var supplierName = (name ?? string.Empty).Trim();
        var code = NormalizeVendorCode(vendorCode);
        if (code.Length == 0)
        {
            code = NormalizeVendorCode(vendorAccount);
        }

        if (code.Length == 0 && whShort.Length > 0)
        {
            code = whShort;
        }

        var full = supplierName;
        if (whFull.Length > 0)
        {
            if (full.Length == 0
                || string.Equals(full, code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, whShort, StringComparison.OrdinalIgnoreCase)
                || (whFull.Length > full.Length && (whFull.Contains(full, StringComparison.OrdinalIgnoreCase) || full.Length <= 12)))
            {
                full = whFull;
            }
        }

        if (full.Length == 0)
        {
            full = code.Length > 0 ? code : "Supplier #" + id.ToString(CultureInfo.InvariantCulture);
        }

        return (full, code);
    }

    public static bool IsTab(string? tab) => Tabs.Any(t => t.Key == tab);

    private readonly IErpWriteConnectionFactory _connections;

    public CpProcurementDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpProcurementDesk> LoadAsync(string tab, long supplierId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpProcurementDesk.Unavailable("No database");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpProcurementSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);

            var dashboard = tab == "dashboard" || tab == "guide"
                ? await DashboardAsync(connection, cancellationToken).ConfigureAwait(false)
                : new CpProcurementDashboard(0, 0, 0, 0m, 0, 0, 0m);
            var suppliers = SupplierTabs.Contains(tab)
                ? await ListSuppliersAsync(connection, cancellationToken).ConfigureAwait(false)
                : [];
            var accounts = tab is "payments" or "advances"
                ? await ListCashAccountsAsync(connection, cancellationToken).ConfigureAwait(false)
                : [];
            var purchases = tab == "purchases"
                ? await ListPurchasesAsync(connection, 200, cancellationToken).ConfigureAwait(false)
                : [];
            var advances = tab == "advances"
                ? await ListAdvancesAsync(connection, 100, cancellationToken).ConfigureAwait(false)
                : [];
            var warehouses = tab is "warehouses" or "suppliers"
                ? await ListWarehousesAsync(connection, cancellationToken).ConfigureAwait(false)
                : [];
            var edit = supplierId > 0
                ? await GetSupplierAsync(connection, supplierId, cancellationToken).ConfigureAwait(false)
                : null;
            var orders = tab == "suppliers" && supplierId > 0
                ? await SupplierOrderLinksAsync(connection, supplierId, 40, cancellationToken).ConfigureAwait(false)
                : [];
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fulfilment = tab == "fulfillment"
                ? await FulfilmentSummaryAsync(connection, now - 30 * 86400, now, cancellationToken).ConfigureAwait(false)
                : new CpProcurementFulfilment(0, 0, 0);
            var vat = await VatRatePercentAsync(connection, cancellationToken).ConfigureAwait(false);

            return new CpProcurementDesk(true, "", dashboard, suppliers, accounts, purchases, advances, warehouses, edit, orders, fulfilment, vat);
        }
        catch (DbException ex)
        {
            return CpProcurementDesk.Unavailable(ex.Message);
        }
    }

    /// <summary>PHP <c>epc_procurement_dashboard</c>.</summary>
    public static async Task<CpProcurementDashboard> DashboardAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var suppliers = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1", cancellationToken).ConfigureAwait(false);
        var withTrn = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1 AND IFNULL(`trn`,'') != ''", cancellationToken).ConfigureAwait(false);
        var purchases = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_erp_purchases` WHERE `active` = 1", cancellationToken).ConfigureAwait(false);
        var payable = await PayableBalanceAsync(connection, cancellationToken).ConfigureAwait(false);
        var storages = await SafeLongAsync(connection, "SELECT COUNT(*) FROM `shop_storages`", cancellationToken).ConfigureAwait(false);
        var linked = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1 AND `storage_id` IS NOT NULL AND `storage_id` > 0", cancellationToken).ConfigureAwait(false);
        var advances = await ErpDb.DecimalAsync(connection, null, "SELECT IFNULL(SUM(`amount`),0) FROM `epc_procurement_advances` WHERE `active` = 1", cancellationToken).ConfigureAwait(false);
        return new CpProcurementDashboard((int)suppliers, (int)withTrn, (int)purchases, payable, (int)storages, (int)linked, advances);
    }

    /// <summary>
    /// PHP <c>epc_erp_incomplete_order_exclude_sql</c>: ledger rows tied to an order (directly or via a purchase)
    /// that is not yet in a <c>for_finish</c> status stay out of the payable balance.
    /// </summary>
    internal static async Task<string> IncompleteOrderExcludeSqlAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        long finishCount;
        try
        {
            finishCount = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return string.Empty;
        }

        if (finishCount <= 0)
        {
            return string.Empty;
        }

        const string incomplete = "(SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 AND `status` NOT IN (SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1))";
        return " AND NOT ((`order_id` > 0 AND `order_id` IN " + incomplete + ")"
            + " OR (`purchase_id` > 0 AND `purchase_id` IN (SELECT `id` FROM `epc_erp_purchases` WHERE `active` = 1 AND `order_id` > 0 AND `order_id` IN " + incomplete + ")))";
    }

    /// <summary>PHP <c>epc_erp_payable_balance</c>.</summary>
    public static async Task<decimal> PayableBalanceAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var exclude = await IncompleteOrderExcludeSqlAsync(connection, cancellationToken).ConfigureAwait(false);
        var up = await ErpDb.DecimalAsync(connection, null, "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_supplier_accounting` WHERE `active` = 1 AND `is_credit` = 1" + exclude, cancellationToken).ConfigureAwait(false);
        var down = await ErpDb.DecimalAsync(connection, null, "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_supplier_accounting` WHERE `active` = 1 AND `is_credit` = 0" + exclude, cancellationToken).ConfigureAwait(false);
        return up - down;
    }

    /// <summary>PHP <c>epc_procurement_list_suppliers</c> (= <c>epc_erp_list_suppliers</c> + enrichment).</summary>
    public static async Task<IReadOnlyList<CpProcurementSupplier>> ListSuppliersAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var exclude = await IncompleteOrderExcludeSqlAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = new List<RawSupplier>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = SupplierSelect(exclude) + " WHERE s.`active` = 1 ORDER BY s.`name`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ReadRawSupplier(r));
            }
        }

        var list = new List<CpProcurementSupplier>(rows.Count);
        foreach (var raw in rows)
        {
            list.Add(await EnrichAsync(connection, raw, cancellationToken).ConfigureAwait(false));
        }

        return list;
    }

    /// <summary>PHP <c>epc_procurement_get_supplier</c>.</summary>
    public static async Task<CpProcurementSupplier?> GetSupplierAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return null;
        }

        var exclude = await IncompleteOrderExcludeSqlAsync(connection, cancellationToken).ConfigureAwait(false);
        RawSupplier? raw = null;
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional(SupplierSelect(exclude) + " WHERE s.`id` = ? AND s.`active` = 1 LIMIT 1");
            ErpDb.AddParameters(c, id);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw = ReadRawSupplier(r);
            }
        }

        return raw is null ? null : await EnrichAsync(connection, raw, cancellationToken).ConfigureAwait(false);
    }

    private sealed record RawSupplier(
        long Id, string Name, string VendorCode, string VendorAccount, string Trn, string CountryCode, bool VatRegistered, long StorageId, decimal Balance,
        string ContactEmail, string ContactPhone, string LegalRegNo, string LegalRegType, string AuthorityName, string AddressLine1, string City, string Emirate, string PaymentTerms, string Notes);

    private static string SupplierSelect(string exclude) =>
        "SELECT s.`id`, IFNULL(s.`name`,''), IFNULL(s.`vendor_code`,''), IFNULL(s.`vendor_account`,''), IFNULL(s.`trn`,''), IFNULL(s.`country_code`,'AE'), IFNULL(s.`vat_registered`,1), IFNULL(s.`storage_id`,0),"
        + " IFNULL((SELECT SUM(`amount`) FROM `epc_erp_supplier_accounting` WHERE `supplier_id` = s.`id` AND `active` = 1 AND `is_credit` = 1" + exclude + "), 0)"
        + " - IFNULL((SELECT SUM(`amount`) FROM `epc_erp_supplier_accounting` WHERE `supplier_id` = s.`id` AND `active` = 1 AND `is_credit` = 0" + exclude + "), 0) AS balance,"
        + " IFNULL(s.`contact_email`,''), IFNULL(s.`contact_phone`,''), IFNULL(s.`legal_reg_no`,''), IFNULL(s.`legal_reg_type`,'TL'), IFNULL(s.`authority_name`,''),"
        + " IFNULL(s.`address_line1`,''), IFNULL(s.`city`,''), IFNULL(s.`emirate`,''), IFNULL(s.`payment_terms`,''), IFNULL(s.`notes`,'')"
        + " FROM `epc_erp_suppliers` s";

    private static RawSupplier ReadRawSupplier(DbDataReader r) => new(
        L(r, 0), S(r, 1), S(r, 2), S(r, 3), S(r, 4), S(r, 5), L(r, 6) != 0, L(r, 7), D(r, 8),
        S(r, 9), S(r, 10), S(r, 11), S(r, 12), S(r, 13), S(r, 14), S(r, 15), S(r, 16), S(r, 17), S(r, 18));

    private static async Task<CpProcurementSupplier> EnrichAsync(DbConnection connection, RawSupplier raw, CancellationToken cancellationToken)
    {
        var whFull = string.Empty;
        var whShort = string.Empty;
        var warehouseName = string.Empty;
        if (raw.StorageId > 0)
        {
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional("SELECT IFNULL(`name`,''), IFNULL(`short_name`,'') FROM `shop_storages` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(c, raw.StorageId);
                try
                {
                    await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        whFull = S(r, 0).Trim();
                        whShort = S(r, 1);
                    }
                }
                catch (DbException)
                {
                }
            }

            warehouseName = whFull;
            try
            {
                await using var va = connection.CreateCommand();
                va.CommandText = ErpDb.Positional("SELECT IFNULL(`vendor_full`,''), IFNULL(`vendor_short`,'') FROM `epc_vendor_accounts` WHERE `storage_id` = ? AND `status` = 'approved' ORDER BY `id` DESC LIMIT 1");
                ErpDb.AddParameters(va, raw.StorageId);
                await using var ar = await va.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await ar.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var vf = S(ar, 0).Trim();
                    var vs = NormalizeVendorCode(S(ar, 1));
                    if (vf.Length > 0)
                    {
                        whFull = vf;
                    }

                    if (vs.Length > 0)
                    {
                        whShort = vs;
                    }
                }
            }
            catch (DbException)
            {
                // PHP swallows a missing epc_vendor_accounts table.
            }
        }

        var (full, code) = ResolveNames(raw.Id, raw.Name, raw.VendorCode, raw.VendorAccount, whFull, whShort);
        return new CpProcurementSupplier(
            raw.Id, raw.Name, full, code, SupplierLabel(raw.Id, full, code), raw.Trn, raw.CountryCode, raw.VatRegistered, raw.StorageId, warehouseName, raw.Balance,
            raw.ContactEmail, raw.ContactPhone, raw.LegalRegNo, raw.LegalRegType, raw.AuthorityName, raw.AddressLine1, raw.City, raw.Emirate, raw.PaymentTerms, raw.Notes);
    }

    /// <summary>PHP <c>epc_erp_list_cash_accounts</c> with <c>epc_erp_account_balance</c>.</summary>
    public static async Task<IReadOnlyList<CpProcurementCashAccount>> ListCashAccountsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<CpProcurementCashAccount>();
        try
        {
            await using var c = connection.CreateCommand();
            c.CommandText = "SELECT a.`id`, IFNULL(a.`name`,''),"
                + " IFNULL(a.`opening_balance`,0)"
                + " + IFNULL((SELECT SUM(`amount`) FROM `epc_erp_cash_bank_entries` WHERE `account_id` = a.`id` AND `active` = 1 AND `direction` = 1),0)"
                + " - IFNULL((SELECT SUM(`amount`) FROM `epc_erp_cash_bank_entries` WHERE `account_id` = a.`id` AND `active` = 1 AND `direction` = 0),0)"
                + " FROM `epc_erp_cash_bank_accounts` a WHERE a.`active` = 1 ORDER BY a.`account_type`, a.`name`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(new CpProcurementCashAccount(L(r, 0), S(r, 1), D(r, 2)));
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    /// <summary>PHP <c>epc_erp_list_purchases</c>.</summary>
    public static async Task<IReadOnlyList<CpProcurementPurchase>> ListPurchasesAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var list = new List<CpProcurementPurchase>();
        await using var c = connection.CreateCommand();
        c.CommandText = "SELECT p.`id`, IFNULL(p.`purchase_date`,0), p.`supplier_id`, IFNULL(s.`name`,''), IFNULL(p.`invoice_number`,''), IFNULL(p.`order_id`,0),"
            + " IFNULL(p.`amount_ex_vat`,0), IFNULL(p.`vat_amount`,0), IFNULL(p.`total_amount`,0)"
            + " FROM `epc_erp_purchases` p INNER JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id`"
            + " WHERE p.`active` = 1 ORDER BY p.`purchase_date` DESC, p.`id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CpProcurementPurchase(L(r, 0), L(r, 1), L(r, 2), S(r, 3), S(r, 4), L(r, 5), D(r, 6), D(r, 7), D(r, 8)));
        }

        return list;
    }

    /// <summary>PHP <c>epc_procurement_list_advances</c>.</summary>
    public static async Task<IReadOnlyList<CpProcurementAdvance>> ListAdvancesAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var list = new List<CpProcurementAdvance>();
        await using var c = connection.CreateCommand();
        c.CommandText = "SELECT a.`id`, a.`time`, IFNULL(s.`name`,''), IFNULL(a.`amount`,0), IFNULL(a.`reference`,''), IFNULL(a.`note`,'')"
            + " FROM `epc_procurement_advances` a INNER JOIN `epc_erp_suppliers` s ON s.`id` = a.`supplier_id`"
            + " WHERE a.`active` = 1 ORDER BY a.`time` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CpProcurementAdvance(L(r, 0), L(r, 1), S(r, 2), D(r, 3), S(r, 4), S(r, 5)));
        }

        return list;
    }

    /// <summary>PHP <c>epc_procurement_list_warehouses</c>.</summary>
    public static async Task<IReadOnlyList<CpProcurementWarehouse>> ListWarehousesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<CpProcurementWarehouse>();
        try
        {
            await using var c = connection.CreateCommand();
            c.CommandText = "SELECT s.`id`, IFNULL(s.`name`,''), IFNULL(s.`short_name`,''),"
                + " IFNULL((SELECT `id` FROM `epc_erp_suppliers` WHERE `storage_id` = s.`id` AND `active` = 1 LIMIT 1),0)"
                + " FROM `shop_storages` s ORDER BY s.`name`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(new CpProcurementWarehouse(L(r, 0), S(r, 1), NormalizeVendorCode(S(r, 2)), L(r, 3)));
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    /// <summary>PHP <c>epc_procurement_supplier_order_links</c>.</summary>
    public static async Task<IReadOnlyList<CpProcurementOrderLink>> SupplierOrderLinksAsync(DbConnection connection, long supplierId, int limit, CancellationToken cancellationToken)
    {
        if (supplierId <= 0)
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 100);
        var storageId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT IFNULL(`storage_id`,0) FROM `epc_erp_suppliers` WHERE `id` = ? AND `active` = 1 LIMIT 1"), cancellationToken, supplierId).ConfigureAwait(false);
        var rows = new List<CpProcurementOrderLink>();
        var seen = new HashSet<long>();
        if (storageId > 0)
        {
            try
            {
                await using var c = connection.CreateCommand();
                c.CommandText = ErpDb.Positional(
                    "SELECT o.`id`, IFNULL(o.`time`,0), IFNULL(o.`user_id`,0), IFNULL(u.`email`,''), COUNT(oi.`id`),"
                    + " IFNULL(SUM(oi.`t2_price_purchase` * oi.`count_need`), 0), IFNULL(SUM(oi.`price` * oi.`count_need`), 0),"
                    + " (SELECT COUNT(*) FROM `epc_erp_purchases` p WHERE p.`active` = 1 AND p.`order_id` = o.`id` AND p.`supplier_id` = ?)"
                    + " FROM `shop_orders` o INNER JOIN `shop_orders_items` oi ON oi.`order_id` = o.`id` AND oi.`t2_storage_id` = ?"
                    + " LEFT JOIN `users` u ON u.`user_id` = o.`user_id`"
                    + " WHERE o.`successfully_created` = 1 GROUP BY o.`id`, o.`time`, o.`user_id`, u.`email` ORDER BY o.`time` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture));
                ErpDb.AddParameters(c, supplierId, storageId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var link = new CpProcurementOrderLink(L(r, 0), L(r, 1), L(r, 2), S(r, 3), (int)L(r, 4), D(r, 5), D(r, 6), (int)L(r, 7));
                    if (seen.Add(link.OrderId))
                    {
                        rows.Add(link);
                    }
                }
            }
            catch (DbException)
            {
            }
        }

        try
        {
            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(
                "SELECT o.`id`, IFNULL(o.`time`,0), IFNULL(o.`user_id`,0), IFNULL(u.`email`,''), IFNULL(SUM(p.`amount_ex_vat`), 0), COUNT(p.`id`)"
                + " FROM `epc_erp_purchases` p INNER JOIN `shop_orders` o ON o.`id` = p.`order_id` AND o.`successfully_created` = 1"
                + " LEFT JOIN `users` u ON u.`user_id` = o.`user_id`"
                + " WHERE p.`active` = 1 AND p.`supplier_id` = ? AND p.`order_id` > 0 GROUP BY o.`id`, o.`time`, o.`user_id`, u.`email` ORDER BY o.`time` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(c, supplierId);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var orderId = L(r, 0);
                if (seen.Add(orderId))
                {
                    rows.Add(new CpProcurementOrderLink(orderId, L(r, 1), L(r, 2), S(r, 3), 0, D(r, 4), 0m, (int)L(r, 5)));
                }
            }
        }
        catch (DbException)
        {
        }

        return rows.OrderByDescending(x => x.Time).Take(limit).ToList();
    }

    /// <summary>PHP <c>epc_erp_fulfilment_summary_light</c>.</summary>
    public static async Task<CpProcurementFulfilment> FulfilmentSummaryAsync(DbConnection connection, long from, long to, CancellationToken cancellationToken)
    {
        var total = await SafeLongAsync(connection, ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?"), cancellationToken, from, to).ConfigureAwait(false);
        var delivered = await SafeLongAsync(
            connection,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ? AND `status` IN (SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1)"),
            cancellationToken,
            from,
            to).ConfigureAwait(false);
        var returns = await SafeLongAsync(connection, "SELECT COUNT(*) FROM `shop_orders_returns`", cancellationToken).ConfigureAwait(false);
        return new CpProcurementFulfilment((int)total, (int)delivered, (int)returns);
    }

    /// <summary>PHP <c>epc_uae_vat_rate_percent</c>: <c>epc_price_settings.vat_percent</c> clamped to 0..100.</summary>
    public static async Task<decimal> VatRatePercentAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        string? raw = null;
        try
        {
            raw = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"), cancellationToken, "vat_percent").ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate))
        {
            rate = 5m;
        }

        return decimal.Round(Math.Clamp(rate, 0m, 100m), 2, MidpointRounding.AwayFromZero);
    }

    private static async Task<long> SafeLongAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] parameters)
    {
        try
        {
            return await ErpDb.LongAsync(connection, null, sql, cancellationToken, parameters).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static long L(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i), CultureInfo.InvariantCulture);

    private static decimal D(DbDataReader r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);

    private static string S(DbDataReader r, int i) => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
}

/// <summary>Port of PHP <c>epc_procurement_ensure_schema</c> (supplier master extensions + advances log).</summary>
internal static class CpProcurementSchema
{
    private static readonly (string Column, string Definition)[] SupplierColumns =
    [
        ("country_code", "varchar(8) NOT NULL DEFAULT 'AE'"),
        ("vat_registered", "tinyint(1) NOT NULL DEFAULT 1"),
        ("vendor_account", "varchar(32) DEFAULT NULL"),
        ("legal_reg_no", "varchar(64) DEFAULT NULL"),
        ("legal_reg_type", "varchar(8) NOT NULL DEFAULT 'TL'"),
        ("authority_name", "varchar(255) DEFAULT NULL"),
        ("address_line1", "varchar(255) DEFAULT NULL"),
        ("city", "varchar(128) DEFAULT NULL"),
        ("emirate", "varchar(64) DEFAULT NULL"),
        ("payment_terms", "varchar(255) DEFAULT NULL"),
        ("notes", "text"),
        ("is_procurement_master", "tinyint(1) NOT NULL DEFAULT 1"),
        ("vendor_code", "varchar(64) DEFAULT NULL"),
    ];

    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_suppliers` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `storage_id` int(11) DEFAULT NULL,"
            + " `name` varchar(255) NOT NULL,"
            + " `contact_email` varchar(255) DEFAULT NULL,"
            + " `contact_phone` varchar(64) DEFAULT NULL,"
            + " `trn` varchar(64) DEFAULT NULL,"
            + " `currency_code` varchar(8) NOT NULL DEFAULT 'AED',"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), KEY `x_storage` (`storage_id`), KEY `x_active` (`active`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP supplier master'",
            cancellationToken).ConfigureAwait(false);

        foreach (var (column, definition) in SupplierColumns)
        {
            await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_erp_suppliers` ADD COLUMN `" + column + "` " + definition, cancellationToken).ConfigureAwait(false);
        }

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_procurement_advances` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `supplier_id` int(11) NOT NULL,"
            + " `time` int(11) NOT NULL,"
            + " `amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `reference` varchar(128) DEFAULT NULL,"
            + " `note` text,"
            + " `cash_entry_id` int(11) NOT NULL DEFAULT 0,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " PRIMARY KEY (`id`), KEY `x_supplier` (`supplier_id`,`active`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Supplier advance payments'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_erp_purchases` ADD COLUMN `inv_receipt_posted` tinyint(1) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_purchase_inv_lines` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `purchase_id` int(11) NOT NULL,"
            + " `warehouse_id` int(11) NOT NULL,"
            + " `item_id` int(11) NOT NULL,"
            + " `qty` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,"
            + " `batch_no` varchar(64) DEFAULT NULL,"
            + " `expiry_date` date DEFAULT NULL,"
            + " `movement_id` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), KEY `x_purchase` (`purchase_id`), KEY `x_item` (`item_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
    }
}
