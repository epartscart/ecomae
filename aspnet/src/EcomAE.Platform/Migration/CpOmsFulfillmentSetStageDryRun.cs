namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP OMS <c>supplier_fulfillment_set_stage</c>.
/// Never executes UPDATE/INSERT. PHP remains authoritative.
/// </summary>
public interface ICpOmsFulfillmentSetStageDryRun
{
    Task<CpOmsFulfillmentSetStageDryRunResult> EvaluateAsync(
        CpOmsFulfillmentSetStageRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsFulfillmentSetStageDryRun : ICpOmsFulfillmentSetStageDryRun
{
    public static readonly IReadOnlyList<string> AllowedStages =
    [
        "supplier_confirm",
        "supplier_payment_done",
        "supplier_ready_to_delivery",
        "delivered",
        "receipt_in_warehouse",
        "ready_to_customer",
        "packing",
        "dispatch",
        "deliver",
        "complete"
    ];

    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsFulfillmentSetStageDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsFulfillmentSetStageDryRunResult> EvaluateAsync(
        CpOmsFulfillmentSetStageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS fulfillment_set_stage is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var key = (request.SupplierKey ?? string.Empty).Trim();
        var stage = (request.Stage ?? string.Empty).Trim();
        if (key.Length == 0 || stage.Length == 0)
        {
            return Refuse("dry-run-invalid", "supplier_key_stage_required",
                "supplier_key and stage required (PHP).", request);
        }

        if (!AllowedStages.Contains(stage, StringComparer.Ordinal))
        {
            return Refuse("dry-run-invalid", "unknown_stage",
                $"Unknown fulfillment stage '{stage}' (PHP).", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request with { SupplierKey = key, Stage = stage });
    }

    public static CpOmsFulfillmentSetStageDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsFulfillmentSetStageRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS fulfillment_set_stage is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var key = (request.SupplierKey ?? string.Empty).Trim();
        var stage = (request.Stage ?? string.Empty).Trim();
        if (key.Length == 0 || stage.Length == 0)
        {
            return Refuse("dry-run-invalid", "supplier_key_stage_required",
                "supplier_key and stage required (PHP).", request);
        }

        if (!AllowedStages.Contains(stage, StringComparer.Ordinal))
        {
            return Refuse("dry-run-invalid", "unknown_stage",
                $"Unknown fulfillment stage '{stage}' (PHP).", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders-digest window.", request);
        }

        return new CpOmsFulfillmentSetStageDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            SupplierKey: key,
            Stage: stage,
            SimulatedSql:
            [
                "epc_order_supplier_fulfillment_bootstrap(@order) (NOT executed)",
                "UPDATE `epc_order_supplier_fulfillment` SET stage=@stage WHERE order_id=@order AND supplier_key=@key (NOT executed)",
                "INSERT shop_orders_logs OMS fulfillment line (NOT executed)"
            ],
            Detail: "Order found; set-stage UPDATE + OMS log simulated. Row existence after bootstrap stays PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=supplier_fulfillment_set_stage");
    }

    private static CpOmsFulfillmentSetStageDryRunResult Refuse(
        string status, string code, string detail, CpOmsFulfillmentSetStageRequest request) =>
        new(status, 0, true, false, true, code, false,
            request.OrderId, request.SupplierKey, request.Stage, [], detail,
            "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=supplier_fulfillment_set_stage");
}

public sealed record CpOmsFulfillmentSetStageRequest(
    long OrderId,
    string? SupplierKey,
    string? Stage,
    bool ConfirmWrites = false);

public sealed record CpOmsFulfillmentSetStageDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, string? SupplierKey, string? Stage,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "cp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { order_id = OrderId, supplier_key = SupplierKey, stage = Stage },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
