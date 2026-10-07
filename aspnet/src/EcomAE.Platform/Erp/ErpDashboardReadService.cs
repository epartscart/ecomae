using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP twin of <c>epc_erp_dashboard()</c> (<c>content/shop/finance/epc_erp_helpers.php</c>, ajax
/// <c>dashboard</c>). Dynamic order-completion rules, customer/destination VAT semantics
/// (<c>epc_uae_customer_vat_shop_order_totals</c>), courier VAT, payables with incomplete-order
/// exclusion, cash/bank totals and the VAT-return figures are ported 1:1. Read-only: never
/// provisions schema, never persists the resolved customer VAT type; absent optional tables yield 0.
/// </summary>
public interface IErpDashboardReadService
{
    Task<ErpDashboardReadResult> DashboardAsync(string? dateFrom, string? dateTo, CancellationToken cancellationToken = default);

    /// <summary>
    /// KPI read of <c>epc_erp_dashboard</c> on an already-open tenant connection.
    /// Does not provision schema and does not attach command-center tiles.
    /// </summary>
    Task<ErpDashboardReadResult> DashboardOnConnectionAsync(DbConnection connection, CancellationToken cancellationToken = default);
}

public sealed record ErpDashboardReadResult(ErpSimpleWriteResult Result, IReadOnlyDictionary<string, object?> Data);

public sealed record ErpCompletionSql(string OrderCompleteExpr, string ItemWhereAnd, string ItemWherePlain, string ItemFinishWhere);

public sealed record ErpVatLineAmounts(decimal LineNet, decimal VatAmount, decimal Gross, decimal TaxRate, bool Inclusive);

public sealed class ErpDashboardReadService : IErpDashboardReadService
{
    private const int RevenueLimit = 5000;
    private static readonly string[] GccCodes = ["SA", "BH", "OM", "KW", "QA"];
    private static readonly string[] VatTypes = ["local_b2c", "local_b2b", "gcc", "export", "tax_exempt"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpCommandCenterReadService _commandCenter;
    private readonly TimeProvider _clock;

    public ErpDashboardReadService(IErpWriteConnectionFactory connections, IErpCommandCenterReadService commandCenter, TimeProvider? clock = null)
    {
        _connections = connections;
        _commandCenter = commandCenter;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ErpDashboardReadResult> DashboardAsync(string? dateFrom, string? dateTo, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadKpisAsync(c, dateFrom, dateTo, includeCommandCenter: true, cancellationToken).ConfigureAwait(false);
    }

    public Task<ErpDashboardReadResult> DashboardOnConnectionAsync(DbConnection connection, CancellationToken cancellationToken = default)
        => ReadKpisAsync(connection, null, null, includeCommandCenter: false, cancellationToken);

    private async Task<ErpDashboardReadResult> ReadKpisAsync(
        DbConnection c,
        string? dateFrom,
        string? dateTo,
        bool includeCommandCenter,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var from = ErpFinanceAjaxReadService.FromUnix(dateFrom, now);
        var to = ErpFinanceAjaxReadService.ToUnix(dateTo, now);

        var orderFinish = await IdsAsync(c, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC", cancellationToken).ConfigureAwait(false);
        var itemFinish = await IdsAsync(c, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC", cancellationToken).ConfigureAwait(false);
        var notCount = await IdsAsync(c, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0", cancellationToken).ConfigureAwait(false);
        var hasStatus = await HasOrderStatusColumnAsync(c, cancellationToken).ConfigureAwait(false);
        var cmp = CompletionSql(hasStatus, orderFinish, itemFinish, notCount);
        var sums = OrderSumSql(cmp);

        long orderCount = 0;
        var purchaseEx = 0m;
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT SUM(IF(" + cmp.OrderCompleteExpr + ", 1, 0)) AS order_count, IFNULL(SUM(" + sums.Purchase + "), 0) AS purchase_ex_vat"
                + " FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?");
            ErpDb.AddParameters(cmd, from, to);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                orderCount = r.IsDBNull(0) ? 0 : Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                purchaseEx = r.IsDBNull(1) ? 0m : Convert.ToDecimal(r.GetValue(1), CultureInfo.InvariantCulture);
            }
        }
        catch (DbException ex)
        {
            return new(ErpSimpleWriteResult.Fail("db", "shop_orders is not readable: " + ex.Message), new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var tax = await LoadTenantVatAsync(c, cancellationToken).ConfigureAwait(false);
        var (revenueEx, receivableDue) = await RevenueAsync(c, cmp, sums, tax, from, to, cancellationToken).ConfigureAwait(false);
        var profitEx = Round2(revenueEx - purchaseEx);

        var receivable = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1", cancellationToken).ConfigureAwait(false);
        var paidOut = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0", cancellationToken).ConfigureAwait(false);
        var customerBalance = receivable - paidOut;

        var exclude = IncompleteOrderExcludeSql(cmp);
        var payUp = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_supplier_accounting` WHERE `active` = 1 AND `is_credit` = 1" + exclude, cancellationToken).ConfigureAwait(false);
        var payDown = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_supplier_accounting` WHERE `active` = 1 AND `is_credit` = 0" + exclude, cancellationToken).ConfigureAwait(false);
        var payable = payUp - payDown;

        var cash = await SafeDecimalAsync(
            c,
            "SELECT IFNULL(SUM(a.`opening_balance` + IFNULL(x.in_amt, 0) - IFNULL(x.out_amt, 0)), 0) AS total FROM `epc_erp_cash_bank_accounts` a"
            + " LEFT JOIN (SELECT `account_id`, SUM(CASE WHEN `direction` = 1 THEN `amount` ELSE 0 END) AS in_amt, SUM(CASE WHEN `direction` = 0 THEN `amount` ELSE 0 END) AS out_amt"
            + " FROM `epc_erp_cash_bank_entries` WHERE `active` = 1 GROUP BY `account_id`) x ON x.`account_id` = a.`id` WHERE a.`active` = 1",
            cancellationToken).ConfigureAwait(false);

        // epc_uae_vat_return_report: SQL stored-price sales sum (not the customer-VAT rewrite) × tenant rate when sales VAT is enabled.
        var salesEx = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(" + sums.Sale + "), 0) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?", cancellationToken, from, to).ConfigureAwait(false);
        var outputVat = tax.SalesEnabled ? Round2(salesEx * tax.RatePercent / 100m) : 0m;
        var salesIncl = Round2(salesEx + outputVat);
        var inputVat = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(p.`vat_amount`), 0) FROM `epc_erp_purchases` p WHERE p.`active` = 1 AND p.`purchase_date` >= ? AND p.`purchase_date` <= ?", cancellationToken, from, to).ConfigureAwait(false);
        var net = Round2(outputVat - inputVat);

        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["date_from"] = from,
            ["date_to"] = to,
            ["order_count"] = orderCount,
            ["revenue_ex_vat"] = Round2(revenueEx),
            ["purchase_ex_vat"] = purchaseEx,
            ["profit_ex_vat"] = profitEx,
            ["receivable_due_orders"] = Round2(receivableDue),
            ["customer_ledger_balance"] = customerBalance,
            ["payable_balance"] = payable,
            ["cash_bank_total"] = cash,
            ["vat_5_on_revenue"] = outputVat,
            ["vat_output"] = outputVat,
            ["vat_input"] = inputVat,
            ["vat_net_payable"] = net,
            ["vat_net_status"] = net >= 0 ? "payable_to_fta" : "recoverable_from_fta",
            ["sales_incl_vat"] = salesIncl,
        };
        if (includeCommandCenter)
        {
            var tiles = await _commandCenter.KpiTilesAsync(dateFrom, dateTo, cancellationToken).ConfigureAwait(false);
            var queue = await _commandCenter.ApprovalQueueAsync(cancellationToken).ConfigureAwait(false);
            data["kpi_tiles"] = tiles.Rows;
            data["approval_queue"] = queue.Rows;
        }

        return new(new ErpSimpleWriteResult(true, "ok", "OK", 0, 0), data);
    }

    // ---- PHP epc_erp_order_complete_sql / epc_erp_item_status_exclusion / epc_erp_order_sum_sql ----

    public static ErpCompletionSql CompletionSql(bool hasOrderStatus, IReadOnlyList<long> orderFinish, IReadOnlyList<long> itemFinish, IReadOnlyList<long> notCount)
    {
        var whereAnd = string.Concat(notCount.Select(id => " AND `status` != " + id.ToString(CultureInfo.InvariantCulture)));
        var wherePlain = "1=1" + whereAnd;
        var itemFinishWhere = itemFinish.Count == 0 ? " AND 1=0" : " AND `status` IN (" + ErpOrderCompletionGuard.Join(itemFinish) + ")";
        if (hasOrderStatus && orderFinish.Count > 0)
        {
            return new("`shop_orders`.`status` IN (" + ErpOrderCompletionGuard.Join(orderFinish) + ")", whereAnd, wherePlain, itemFinishWhere);
        }

        if (itemFinish.Count == 0)
        {
            return new("0", whereAnd, wherePlain, itemFinishWhere);
        }

        var expr = "((SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`" + whereAnd + ") > 0"
            + " AND (SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`" + whereAnd
            + " AND `status` NOT IN (" + ErpOrderCompletionGuard.Join(itemFinish) + ")) = 0)";
        return new(expr, whereAnd, wherePlain, itemFinishWhere);
    }

    public static (string Sale, string Purchase, string Paid) OrderSumSql(ErpCompletionSql cmp)
    {
        var itemWhere = cmp.ItemWhereAnd + cmp.ItemFinishWhere;
        var itemWherePurchase = cmp.ItemWherePlain + cmp.ItemFinishWhere;
        var saleSub = "(SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`" + itemWhere + ")";
        var purchaseSub = "(SELECT SUM(`t2_price_purchase`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` AND " + itemWherePurchase + ")";
        var sale = "CAST(IF(" + cmp.OrderCompleteExpr + ", IFNULL(" + saleSub + ", 0), 0) AS DECIMAL(20,2))";
        var purchase = "CAST(IF(" + cmp.OrderCompleteExpr + ", IFNULL(" + purchaseSub + ", 0), 0) AS DECIMAL(20,2))";
        var paidIssue = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = `shop_orders`.`id`), 0)";
        var paidIncome = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = `shop_orders`.`id`), 0)";
        var paid = "CAST(IF(" + cmp.OrderCompleteExpr + ", (" + paidIssue + " - " + paidIncome + "), 0) AS DECIMAL(20,2))";
        return (sale, purchase, paid);
    }

    public static string IncompleteOrderExcludeSql(ErpCompletionSql cmp)
    {
        if (cmp.OrderCompleteExpr == "0")
        {
            return string.Empty;
        }

        var incomplete = "(SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 AND NOT (" + cmp.OrderCompleteExpr + "))";
        return " AND NOT ((`order_id` > 0 AND `order_id` IN " + incomplete + ")"
            + " OR (`purchase_id` > 0 AND `purchase_id` IN (SELECT `id` FROM `epc_erp_purchases` WHERE `active` = 1 AND `order_id` > 0 AND `order_id` IN " + incomplete + ")))";
    }

    // ---- PHP epc_uae_customer_vat_* / epc_order_courier_vat ----

    public sealed record TenantVat(string CompanyCountry, bool VatRegistered, bool SalesOnlyFlag, decimal RatePercent)
    {
        public bool SalesEnabled => SalesOnlyFlag && CompanyCountry == "AE" && VatRegistered;
    }

    private async Task<(decimal RevenueEx, decimal ReceivableDue)> RevenueAsync(DbConnection c, ErpCompletionSql cmp, (string Sale, string Purchase, string Paid) sums, TenantVat tax, long from, long to, CancellationToken ct)
    {
        var orders = new List<(long Id, long UserId, decimal Paid, string HowGet)>();
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `shop_orders`.`id`, `shop_orders`.`user_id`, " + sums.Paid + " AS paid_amount, `shop_orders`.`how_get_json`"
                + " FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ? AND " + cmp.OrderCompleteExpr
                + " ORDER BY `time` DESC LIMIT " + RevenueLimit.ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(cmd, from, to);
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                orders.Add((
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    r.IsDBNull(1) ? 0 : Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                    r.IsDBNull(2) ? 0m : Convert.ToDecimal(r.GetValue(2), CultureInfo.InvariantCulture),
                    r.IsDBNull(3) ? string.Empty : Convert.ToString(r.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
        catch (DbException)
        {
            return (0m, 0m);
        }

        var revenue = 0m;
        var due = 0m;
        var ctxCache = new Dictionary<long, CustomerVatContext>();
        foreach (var o in orders)
        {
            if (!ctxCache.TryGetValue(o.UserId, out var ctx))
            {
                ctx = await CustomerContextAsync(c, o.UserId, ct).ConfigureAwait(false);
                ctxCache[o.UserId] = ctx;
            }

            var items = await ItemsAsync(c, o.Id, ct).ConfigureAwait(false);
            var dest = DestinationCountry(o.HowGet, ctx.Country);
            var exports = dest != "AE";
            var goodsRate = SupplyRate(ctx.Country, exports, tax.RatePercent);
            var inclusive = DisplayMode(ctx.VatType) == "inclusive" && goodsRate > 0m;
            var goodsGross = 0m;
            var lineNet = 0m;
            foreach (var (unit, qty) in items)
            {
                var line = LineAmounts(unit, qty, goodsRate, inclusive, tax.SalesEnabled);
                lineNet += line.LineNet;
                goodsGross += line.Gross;
            }

            var courierNet = Round2(Math.Max(0m, CourierAmount(o.HowGet)));
            var courierRate = SupplyRate(dest, exports, tax.RatePercent);
            var courierGross = courierNet;
            if (courierNet > 0m && courierRate > 0m && tax.SalesEnabled)
            {
                courierGross = Round2(courierNet + Round2(courierNet * courierRate / 100m));
            }

            var amountDueBase = Round2(Round2(goodsGross) + Round2(courierGross));
            revenue += Round2(lineNet);
            due += Math.Max(0m, Round2(amountDueBase - Round2(o.Paid)));
        }

        return (revenue, due);
    }

    public sealed record CustomerVatContext(string Country, string CustomerType, bool TaxExempt, string VatType);

    public static async Task<CustomerVatContext> CustomerContextAsync(DbConnection c, long userId, CancellationToken ct)
    {
        if (userId <= 0)
        {
            return new("AE", "retail", false, ResolveVatType("AE", "retail", false));
        }

        var customerType = await ProfileAsync(c, userId, "epc_customer_type", ct).ConfigureAwait(false);
        if (customerType.Length == 0)
        {
            customerType = "retail";
        }

        var regCountry = (await ProfileAsync(c, userId, "epc_reg_country", ct).ConfigureAwait(false)).ToUpperInvariant();
        var country = NormalizeCountry(regCountry.Length == 0 ? "AE" : regCountry);
        var taxStatus = await ProfileAsync(c, userId, "epc_tax_exempt_cert_status", ct).ConfigureAwait(false);
        var taxExempt = customerType == "wholesale" && taxStatus == "approved";

        // epc_einvoice_buyer_profile: stored buyer row, else derived from users_profiles (2-letter epc_reg_country or AE).
        string? buyerCountry = null;
        var hasBuyerTable = true;
        try
        {
            buyerCountry = await ErpDb.StringAsync(c, null, ErpDb.Positional("SELECT `country_code` FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1"), ct, userId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            hasBuyerTable = false;
        }

        if (hasBuyerTable)
        {
            country = !string.IsNullOrWhiteSpace(buyerCountry)
                ? NormalizeCountry(buyerCountry)
                : NormalizeCountry(regCountry.Length == 2 ? regCountry : "AE");
        }

        var stored = await ProfileAsync(c, userId, "customer_vat_type", ct).ConfigureAwait(false);
        var vatType = VatTypes.Contains(stored, StringComparer.Ordinal) ? stored : ResolveVatType(country, customerType, taxExempt);
        return new(country, customerType, taxExempt, vatType);
    }

    private static async Task<string> ProfileAsync(DbConnection c, long userId, string key, CancellationToken ct)
    {
        try
        {
            var v = await ErpDb.StringAsync(c, null, ErpDb.Positional("SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1"), ct, userId, key).ConfigureAwait(false);
            return (v ?? string.Empty).Trim();
        }
        catch (DbException)
        {
            return string.Empty;
        }
    }

    public static async Task<List<(decimal Unit, decimal Qty)>> ItemsAsync(DbConnection c, long orderId, CancellationToken ct)
    {
        var items = new List<(decimal, decimal)>();
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = ErpDb.Positional("SELECT `price`, `count_need` FROM `shop_orders_items` WHERE `order_id` = ?");
            ErpDb.AddParameters(cmd, orderId);
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                items.Add((
                    r.IsDBNull(0) ? 0m : Convert.ToDecimal(r.GetValue(0), CultureInfo.InvariantCulture),
                    r.IsDBNull(1) ? 0m : Convert.ToDecimal(r.GetValue(1), CultureInfo.InvariantCulture)));
            }
        }
        catch (DbException)
        {
        }

        return items;
    }

    public static async Task<TenantVat> LoadTenantVatAsync(DbConnection c, CancellationToken ct)
    {
        var country = NormalizeCountry(await SettingAsync(c, "company_country_code", "AE", ct).ConfigureAwait(false));
        try
        {
            // epc_uae_company_profile: e-invoice seller_country_code (default AE) overrides when the settings table exists.
            var seller = await ErpDb.StringAsync(c, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_einvoice_settings` WHERE `setting_key` = ? LIMIT 1"), ct, "seller_country_code").ConfigureAwait(false);
            country = NormalizeCountry(seller ?? "AE");
        }
        catch (DbException)
        {
        }

        var reg = (await SettingAsync(c, "company_vat_registered", "1", ct).ConfigureAwait(false)).Trim();
        var registered = reg is "1" or "true" or "";
        var flagRaw = (await SettingAsync(c, "vat_uae_sales_only", "1", ct).ConfigureAwait(false)).Trim();
        var flag = flagRaw is "1" or "true" or "";
        var rate = decimal.TryParse(await SettingAsync(c, "vat_percent", "5.00", ct).ConfigureAwait(false), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : 0m;
        rate = Math.Round(Math.Clamp(rate, 0m, 100m), 2, MidpointRounding.AwayFromZero);
        return new(country, registered, flag, rate);
    }

    private static async Task<string> SettingAsync(DbConnection c, string key, string fallback, CancellationToken ct)
    {
        try
        {
            return await ErpDb.StringAsync(c, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"), ct, key).ConfigureAwait(false) ?? fallback;
        }
        catch (DbException)
        {
            return fallback;
        }
    }

    /// <summary>PHP <c>epc_uae_vat_normalize_country</c>.</summary>
    public static string NormalizeCountry(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (c.Length == 0 || c is "UAE" or "ARE" or "UNITED ARAB EMIRATES" or "U.A.E." or "U.A.E")
        {
            return "AE";
        }

        return c;
    }

    /// <summary>PHP <c>epc_uae_customer_vat_resolve_type</c>.</summary>
    public static string ResolveVatType(string country, string customerType, bool taxExempt)
    {
        if (taxExempt)
        {
            return "tax_exempt";
        }

        var c = NormalizeCountry(country);
        if (c != "AE")
        {
            return GccCodes.Contains(c, StringComparer.Ordinal) ? "gcc" : "export";
        }

        return customerType == "wholesale" ? "local_b2b" : "local_b2c";
    }

    /// <summary>PHP <c>epc_uae_customer_vat_display_mode</c>.</summary>
    public static string DisplayMode(string vatType) => vatType == "local_b2c" ? "inclusive" : VatTypes.Contains(vatType, StringComparer.Ordinal) ? "exclusive" : "inclusive";

    /// <summary>PHP <c>epc_uae_vat_supply_tax_category</c> rate: exports or non-AE buyer are zero-rated.</summary>
    public static decimal SupplyRate(string buyerCountry, bool exports, decimal tenantRatePercent)
        => exports || NormalizeCountry(buyerCountry) != "AE" ? 0m : tenantRatePercent;

    /// <summary>PHP <c>epc_uae_customer_vat_order_line</c>: inclusive stored prices are split, exclusive ones are grossed up.</summary>
    public static ErpVatLineAmounts LineAmounts(decimal unitPrice, decimal qty, decimal ratePercent, bool inclusive, bool salesEnabled)
    {
        qty = Math.Max(0m, qty);
        var unit = Round2(Math.Max(0m, unitPrice));
        var grossStored = Round2(unit * qty);
        if (ratePercent <= 0m || !salesEnabled)
        {
            return new(grossStored, 0m, grossStored, 0m, inclusive);
        }

        if (inclusive)
        {
            var ex = Round2(grossStored / (1m + ratePercent / 100m));
            return new(ex, Round2(grossStored - ex), grossStored, ratePercent, true);
        }

        var vat = Round2(grossStored * ratePercent / 100m);
        return new(grossStored, vat, Round2(grossStored + vat), ratePercent, false);
    }

    /// <summary>PHP <c>epc_order_destination_country</c>: 2-letter how_get_json country, else the customer's country.</summary>
    public static string DestinationCountry(string howGetJson, string customerCountry)
    {
        var how = HowGet(howGetJson);
        var ship = (Str(how, "country") ?? Str(how, "country_code") ?? string.Empty).Trim().ToUpperInvariant();
        return ship.Length == 2 ? NormalizeCountry(ship) : NormalizeCountry(customerCountry);
    }

    /// <summary>PHP <c>epc_order_courier_charge</c>: delivery_price, else rate.</summary>
    public static decimal CourierAmount(string howGetJson)
    {
        var how = HowGet(howGetJson);
        return Num(how, "delivery_price") ?? Num(how, "rate") ?? 0m;
    }

    private static JsonElement? HowGet(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Str(JsonElement? how, string key)
    {
        if (how is null || !how.Value.TryGetProperty(key, out var v))
        {
            return null;
        }

        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }

    private static decimal? Num(JsonElement? how, string key)
    {
        var s = Str(how, key);
        return s is not null && decimal.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    public static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private static async Task<List<long>> IdsAsync(DbConnection c, string sql, CancellationToken ct)
    {
        var ids = new List<long>();
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture));
            }
        }
        catch (DbException)
        {
        }

        return ids;
    }

    private static async Task<bool> HasOrderStatusColumnAsync(DbConnection c, CancellationToken ct)
    {
        try
        {
            return !string.IsNullOrEmpty(await ErpDb.StringAsync(c, null, "SHOW COLUMNS FROM `shop_orders` LIKE 'status'", ct).ConfigureAwait(false));
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static async Task<decimal> SafeDecimalAsync(DbConnection c, string sql, CancellationToken ct, params object?[] p)
    {
        try
        {
            var s = await ErpDb.StringAsync(c, null, ErpDb.Positional(sql), ct, p).ConfigureAwait(false);
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }
        catch (DbException)
        {
            return 0m;
        }
    }
}
