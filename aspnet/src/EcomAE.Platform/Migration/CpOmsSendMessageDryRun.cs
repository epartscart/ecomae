namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP OMS <c>ajax_epc_orders_oms.php</c> action <c>send_message</c>.
/// Simulates INSERT into shop_orders_messages + log; customer notify stays PHP.
/// Never executes writes. PHP remains authoritative.
/// </summary>
public interface ICpOmsSendMessageDryRun
{
    Task<CpOmsSendMessageDryRunResult> EvaluateAsync(
        CpOmsSendMessageRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsSendMessageDryRun : ICpOmsSendMessageDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsSendMessageDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsSendMessageDryRunResult> EvaluateAsync(
        CpOmsSendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS send_message is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Refuse("dry-run-invalid", "message_text_required",
                "Message text is required (PHP epc_oms_fail).", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request with { Text = text });
    }

    public static CpOmsSendMessageDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsSendMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS send_message is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Refuse("dry-run-invalid", "message_text_required",
                "Message text is required (PHP epc_oms_fail).", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders digest window — expand sample or use PHP OMS console.",
                request);
        }

        var itemId = request.ItemId ?? 0;
        var storedText = itemId > 0
            ? $"[Item #{itemId} …] {text}"
            : text;

        return new CpOmsSendMessageDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            ItemId: itemId > 0 ? itemId : null,
            TextPreview: storedText.Length > 200 ? storedText[..200] + "…" : storedText,
            OrderStatus: order.Status,
            OrderPaid: order.Paid,
            SimulatedSql:
            [
                "INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`, `read`) VALUES (@orderId, 0, @text, @now, 0, 0) (NOT executed)",
                "INSERT INTO `shop_orders_logs` (…) OMS message to customer (NOT executed)",
                "Customer notify (send_notify order_message_to_customer) remains PHP-only in this dry-run slice"
            ],
            Detail: "Order found in digest window; message INSERT + log simulated. Item-row prefix lookup + notify stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=send_message");
    }

    private static CpOmsSendMessageDryRunResult Refuse(
        string status, string code, string detail, CpOmsSendMessageRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderId, request.ItemId,
            request.Text, null, null, [], detail,
            "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=send_message");
}

public sealed record CpOmsSendMessageRequest(
    long OrderId,
    string? Text,
    long? ItemId = null,
    bool ConfirmWrites = false);

public sealed record CpOmsSendMessageDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, long? ItemId, string? TextPreview,
    int? OrderStatus, int? OrderPaid, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { order_id = OrderId, item_id = ItemId, text = TextPreview },
        order_context = OrderStatus is null ? null : new { status = OrderStatus, paid = OrderPaid },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
