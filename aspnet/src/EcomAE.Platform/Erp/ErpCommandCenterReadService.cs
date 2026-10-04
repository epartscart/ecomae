using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP twin of <c>content/shop/finance/epc_erp_command_center.php</c> (ajax <c>cc_kpi_tiles</c>,
/// <c>cc_approval_queue</c>, <c>command_center</c>). Every SQL block runs through the PHP
/// <c>epc_erp_cc_safe_query</c> rule: a failing or absent table yields <c>0</c> for that metric only.
/// Read-only; never creates schema and never writes.
/// </summary>
public interface IErpCommandCenterReadService
{
    Task<ErpAjaxRowsResult> KpiTilesAsync(string? dateFrom, string? dateTo, CancellationToken cancellationToken = default);

    Task<ErpAjaxRowsResult> ApprovalQueueAsync(CancellationToken cancellationToken = default);

    Task<ErpCommandCenterResult> CommandCenterAsync(string? role, string? dateFrom, string? dateTo, CancellationToken cancellationToken = default);
}

public sealed record ErpCommandCenterResult(
    ErpSimpleWriteResult Result,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> KpiTiles,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> ApprovalQueue,
    IReadOnlyDictionary<string, object?> Widgets,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> QuickActions,
    string Period,
    string GeneratedAt);

public sealed class ErpCommandCenterReadService : IErpCommandCenterReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public ErpCommandCenterReadService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>PHP <c>number_format($v, 2)</c>: thousands separator, two decimals.</summary>
    public static string Money(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>PHP <c>number_format((int)$v)</c>.</summary>
    public static string Count(long value) => value.ToString("#,##0", CultureInfo.InvariantCulture);

    /// <summary>PHP <c>ucfirst(str_replace('_',' ',$status))</c>.</summary>
    public static string PeriodValue(string status)
    {
        var s = status.Replace('_', ' ');
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }

    public static string PeriodColor(string status) => status switch
    {
        "locked" => "#dc3545",
        "soft_close" => "#fd7e14",
        _ => "#28a745",
    };

    public static readonly IReadOnlyDictionary<string, string> RoleMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["super_admin"] = "executive",
        ["finance_admin"] = "finance",
        ["finance_controller"] = "finance",
        ["finance_user"] = "finance",
        ["operations_admin"] = "operations",
        ["warehouse_user"] = "operations",
        ["sales_admin"] = "sales",
        ["sales_user"] = "sales",
    };

    private static IReadOnlyDictionary<string, object?> Widget(string id, string label, string size, string icon)
        => new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = id, ["label"] = label, ["size"] = size, ["icon"] = icon };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> WidgetSets =
        new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(StringComparer.Ordinal)
        {
            ["finance"] =
            [
                Widget("revenue_chart", "Revenue Trend", "half", "fa-line-chart"),
                Widget("ar_aging", "AR Aging Summary", "half", "fa-clock-o"),
                Widget("cash_flow", "Cash Flow", "half", "fa-exchange"),
                Widget("vat_status", "VAT Return Status", "half", "fa-balance-scale"),
                Widget("period_close", "Period Close Status", "full", "fa-calendar-check-o"),
            ],
            ["operations"] =
            [
                Widget("order_pipeline", "Order Pipeline", "half", "fa-shopping-cart"),
                Widget("inventory_val", "Inventory Value", "half", "fa-cubes"),
                Widget("low_stock", "Low Stock Alerts", "half", "fa-exclamation-triangle"),
                Widget("po_status", "PO Status", "half", "fa-truck"),
            ],
            ["executive"] =
            [
                Widget("pl_summary", "P&L Summary", "half", "fa-bar-chart"),
                Widget("revenue_chart", "Revenue Trend", "half", "fa-line-chart"),
                Widget("cash_flow", "Cash Position", "half", "fa-university"),
                Widget("compliance", "Compliance Status", "half", "fa-shield"),
            ],
            ["sales"] =
            [
                Widget("order_pipeline", "Order Pipeline", "half", "fa-shopping-cart"),
                Widget("so_status", "Sales Orders", "half", "fa-file-text"),
                Widget("ar_aging", "Customer Aging", "half", "fa-clock-o"),
                Widget("revenue_chart", "Sales Trend", "half", "fa-line-chart"),
            ],
        };

    /// <summary>PHP <c>epc_erp_cc_role_widgets</c>: unknown role falls back to the executive layout.</summary>
    public static IReadOnlyDictionary<string, object?> RoleWidgets(string role)
    {
        var layout = RoleMap.TryGetValue(role, out var l) ? l : "executive";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["role"] = role,
            ["layout"] = layout,
            ["widgets"] = WidgetSets[layout],
        };
    }

    private static IReadOnlyDictionary<string, object?> Action(string id, string label, string icon, string link, params string[] roles)
        => new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = id, ["label"] = label, ["icon"] = icon, ["link"] = link, ["roles"] = roles };

    private static readonly IReadOnlyList<IReadOnlyDictionary<string, object?>> Actions =
    [
        Action("new_invoice", "New Invoice", "fa-plus-circle", "/erp/?area=sales&tab=invoices&action=create", "super_admin", "finance_admin", "finance_user", "sales_admin"),
        Action("new_payment", "Record Payment", "fa-money", "/erp/?area=finance&tab=cash_bank&action=entry", "super_admin", "finance_admin", "finance_user"),
        Action("new_po", "New Purchase Order", "fa-cart-plus", "/erp/?area=procurement&tab=purchase_orders&action=create", "super_admin", "finance_admin", "operations_admin"),
        Action("new_journal", "GL Journal Entry", "fa-pencil-square", "/erp/?area=finance&tab=gl&action=manual", "super_admin", "finance_admin", "finance_controller"),
        Action("vat_return", "VAT Return", "fa-balance-scale", "/erp/?area=finance&tab=vat_return", "super_admin", "finance_admin"),
        Action("aging_report", "Aging Report", "fa-clock-o", "/erp/?area=finance&tab=aging", "super_admin", "finance_admin", "finance_user", "sales_admin"),
        Action("inv_movement", "Inventory Movement", "fa-exchange", "/erp/?area=inventory&tab=movements&action=create", "super_admin", "operations_admin", "warehouse_user"),
        Action("einvoice", "E-Invoice", "fa-paper-plane", "/erp/?area=finance&tab=einvoice", "super_admin", "finance_admin"),
    ];

    /// <summary>PHP <c>epc_erp_cc_quick_actions</c>: empty role returns everything.</summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> QuickActions(string role)
        => role.Length == 0 ? Actions : Actions.Where(a => ((string[])a["roles"]!).Contains(role, StringComparer.Ordinal)).ToList();

    public async Task<ErpAjaxRowsResult> KpiTilesAsync(string? dateFrom, string? dateTo, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new(ErpSimpleWriteResult.Ok("OK", 0), await TilesAsync(connection, dateFrom, dateTo, cancellationToken).ConfigureAwait(false));
    }

    public async Task<ErpAjaxRowsResult> ApprovalQueueAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new(ErpSimpleWriteResult.Ok("OK", 0), await QueueAsync(connection, cancellationToken).ConfigureAwait(false));
    }

    public async Task<ErpCommandCenterResult> CommandCenterAsync(string? role, string? dateFrom, string? dateTo, CancellationToken cancellationToken = default)
    {
        var r = role ?? string.Empty;
        var now = _clock.GetUtcNow();
        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), [], [], RoleWidgets(r), QuickActions(r), now.ToString("yyyy-MM", CultureInfo.InvariantCulture), now.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture));
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var tiles = await TilesAsync(connection, dateFrom, dateTo, cancellationToken).ConfigureAwait(false);
        var queue = await QueueAsync(connection, cancellationToken).ConfigureAwait(false);
        return new(ErpSimpleWriteResult.Ok("OK", 0), tiles, queue, RoleWidgets(r), QuickActions(r), now.ToString("yyyy-MM", CultureInfo.InvariantCulture), now.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture));
    }

    private static IReadOnlyDictionary<string, object?> Tile(string id, string label, string value, string icon, string color, string unit)
        => new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = id, ["label"] = label, ["value"] = value, ["icon"] = icon, ["color"] = color, ["unit"] = unit };

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> TilesAsync(DbConnection c, string? dateFrom, string? dateTo, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var from = ErpFinanceAjaxReadService.FromUnix(dateFrom, now);
        var to = ErpFinanceAjaxReadService.ToUnix(dateTo, now);

        var rev = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(CASE WHEN `successfully_created` = 1 THEN `price_total_wt` - `price_total_wt_vat` ELSE 0 END), 0) AS val FROM `shop_orders` WHERE `time` >= ? AND `time` <= ?", ct, from, to).ConfigureAwait(false);
        var orders = await SafeLongAsync(c, "SELECT COUNT(*) AS val FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?", ct, from, to).ConfigureAwait(false);
        var ar = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(CASE WHEN `income` = 1 THEN `amount` ELSE -`amount` END), 0) AS val FROM `shop_users_accounting` WHERE `active` = 1", ct).ConfigureAwait(false);
        var ap = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(`balance`), 0) AS val FROM `epc_erp_suppliers` WHERE `active` = 1", ct).ConfigureAwait(false);
        var cash = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(`balance`), 0) AS val FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1", ct).ConfigureAwait(false);
        var vatOut = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(`price_total_wt_vat`), 0) AS val FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?", ct, from, to).ConfigureAwait(false);
        var vatIn = await SafeDecimalAsync(c, "SELECT IFNULL(SUM(`vat_amount`), 0) AS val FROM `epc_erp_purchases` WHERE `active` = 1 AND `purchase_date` >= ? AND `purchase_date` <= ?", ct, from, to).ConfigureAwait(false);
        var vatNet = vatOut - vatIn;
        var ym = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var periodStatus = await SafeStringAsync(c, "SELECT `status` AS val FROM `epc_erp_periods` WHERE `year_month` = ? LIMIT 1", ct, ym).ConfigureAwait(false);
        if (string.IsNullOrEmpty(periodStatus))
        {
            periodStatus = "open";
        }

        var inv = await SafeLongAsync(c, "SELECT COUNT(*) AS val FROM `epc_erp_inv_items` WHERE `active` = 1", ct).ConfigureAwait(false);

        return
        [
            Tile("revenue", "Revenue (ex. VAT)", Money(rev), "fa-line-chart", "#28a745", "AED"),
            Tile("orders", "Orders", Count(orders), "fa-shopping-cart", "#007bff", string.Empty),
            Tile("ar_balance", "AR Balance", Money(ar), "fa-file-text-o", ar > 0 ? "#fd7e14" : "#28a745", "AED"),
            Tile("ap_balance", "AP Balance", Money(Math.Abs(ap)), "fa-credit-card", "#dc3545", "AED"),
            Tile("cash_bank", "Cash & Bank", Money(cash), "fa-university", "#6f42c1", "AED"),
            Tile("vat_net", "VAT Net Payable", Money(Math.Abs(vatNet)), "fa-balance-scale", vatNet >= 0 ? "#e83e8c" : "#20c997", "AED"),
            Tile("period_status", "Period " + now.ToString("MMM yyyy", CultureInfo.InvariantCulture), PeriodValue(periodStatus), "fa-calendar-check-o", PeriodColor(periodStatus), string.Empty),
            Tile("inventory_items", "Active Items", Count(inv), "fa-cubes", "#17a2b8", string.Empty),
        ];
    }

    private static IReadOnlyDictionary<string, object?> QueueItem(string id, string category, string label, long count, string action, string link, string severity, string icon)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = id, ["category"] = category, ["label"] = label, ["count"] = count,
            ["action"] = action, ["link"] = link, ["severity"] = severity, ["icon"] = icon,
        };

    private static string Plural(long n, string noun) => n.ToString(CultureInfo.InvariantCulture) + " " + noun + (n > 1 ? "s" : string.Empty);

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueueAsync(DbConnection c, CancellationToken ct)
    {
        var queue = new List<IReadOnlyDictionary<string, object?>>();

        var draftSo = await SafeLongAsync(c, "SELECT COUNT(*) AS val FROM `epc_erp_sales_orders` WHERE `status` = 'draft'", ct).ConfigureAwait(false);
        if (draftSo > 0)
        {
            queue.Add(QueueItem("draft_so", "Sales", Plural(draftSo, "draft sales order") + " awaiting confirmation", draftSo, "Open Sales Orders", "/erp/?area=sales&tab=sales_orders", "warning", "fa-file-text"));
        }

        var pendingPo = await SafeLongAsync(c, "SELECT COUNT(*) AS val FROM `epc_erp_purchase_orders` WHERE `status` IN ('draft', 'pending')", ct).ConfigureAwait(false);
        if (pendingPo > 0)
        {
            queue.Add(QueueItem("pending_po", "Procurement", Plural(pendingPo, "purchase order") + " pending approval", pendingPo, "Open Purchase Orders", "/erp/?area=procurement&tab=purchase_orders", "warning", "fa-truck"));
        }

        var unpostedGl = await SafeLongAsync(c, "SELECT COUNT(*) AS val FROM `epc_erp_gl_journals` WHERE `status` = 'draft' AND `active` = 1", ct).ConfigureAwait(false);
        if (unpostedGl > 0)
        {
            queue.Add(QueueItem("unposted_gl", "Finance", Plural(unpostedGl, "unposted GL journal"), unpostedGl, "Open General Ledger", "/erp/?area=finance&tab=gl", "info", "fa-book"));
        }

        var overdue = await SafeLongAsync(c, "SELECT COUNT(*) AS val FROM `epc_erp_sales_invoices` WHERE `status` = 'unpaid' AND `due_date` < ?", ct, _clock.GetUtcNow().ToUnixTimeSeconds() - 86400L * 30).ConfigureAwait(false);
        if (overdue > 0)
        {
            queue.Add(QueueItem("overdue_invoices", "Finance", Plural(overdue, "overdue invoice") + " (30+ days)", overdue, "Open Aging Report", "/erp/?area=finance&tab=aging", "danger", "fa-exclamation-triangle"));
        }

        var lowStock = await SafeLongAsync(c, "SELECT COUNT(*) AS val FROM `epc_erp_inv_stock` s INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1 WHERE i.`reorder_level` > 0 AND s.`qty_on_hand` > 0 AND s.`qty_on_hand` <= i.`reorder_level`", ct).ConfigureAwait(false);
        if (lowStock > 0)
        {
            queue.Add(QueueItem("low_stock", "Inventory", Plural(lowStock, "item") + " at or below reorder level", lowStock, "Open Inventory", "/erp/?area=inventory&tab=items", "warning", "fa-archive"));
        }

        var pendingEinv = await SafeLongAsync(c, "SELECT COUNT(*) AS val FROM `epc_einvoice_documents` WHERE `status` IN ('draft', 'queued')", ct).ConfigureAwait(false);
        if (pendingEinv > 0)
        {
            queue.Add(QueueItem("pending_einvoice", "Compliance", Plural(pendingEinv, "e-invoice") + " pending submission", pendingEinv, "Open E-Invoicing", "/erp/?area=finance&tab=einvoice", "info", "fa-paper-plane"));
        }

        return queue;
    }

    private static async Task<string?> SafeStringAsync(DbConnection c, string sql, CancellationToken ct, params object?[] p)
    {
        try
        {
            return await ErpDb.StringAsync(c, null, ErpDb.Positional(sql), ct, p).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<long> SafeLongAsync(DbConnection c, string sql, CancellationToken ct, params object?[] p)
    {
        var s = await SafeStringAsync(c, sql, ct, p).ConfigureAwait(false);
        return long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static async Task<decimal> SafeDecimalAsync(DbConnection c, string sql, CancellationToken ct, params object?[] p)
    {
        var s = await SafeStringAsync(c, sql, ct, p).ConfigureAwait(false);
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }
}
