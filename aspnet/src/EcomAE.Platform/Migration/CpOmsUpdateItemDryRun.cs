namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP OMS <c>ajax_epc_orders_oms.php</c> action <c>update_item</c>.
/// Never executes UPDATE. PHP remains authoritative.
/// </summary>
public interface ICpOmsUpdateItemDryRun
{
    Task<CpOmsUpdateItemDryRunResult> EvaluateAsync(
        CpOmsUpdateItemRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsUpdateItemDryRun : ICpOmsUpdateItemDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsUpdateItemDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsUpdateItemDryRunResult> EvaluateAsync(
        CpOmsUpdateItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS update_item is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.ItemId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId and itemId must be positive (PHP Invalid item).", request);
        }

        if (request.CountNeed is < 1)
        {
            return Refuse("dry-run-invalid", "invalid_qty",
                "Quantity must be at least 1 (PHP Quantity must be at least 1).", request);
        }

        if (request.Price is <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_price",
                "Price must be greater than 0 (PHP Price must be greater than 0).", request);
        }

        var brand = (request.Manufacturer ?? string.Empty).Trim();
        var article = (request.Article ?? string.Empty).Trim();
        if (brand.Length == 0 || article.Length == 0)
        {
            return Refuse("dry-run-invalid", "brand_article_required",
                "Brand and article number are required (PHP).", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request);
    }

    public static CpOmsUpdateItemDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsUpdateItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS update_item is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.ItemId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId and itemId must be positive (PHP Invalid item).", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders-digest window.", request);
        }

        return new CpOmsUpdateItemDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            ItemId: request.ItemId,
            Price: request.Price,
            CountNeed: request.CountNeed,
            Manufacturer: request.Manufacturer,
            Article: request.Article,
            SimulatedSql:
            [
                "UPDATE `shop_orders_items` SET price/count_need/t2_* WHERE id=@item AND order_id=@order (NOT executed)",
                "UPDATE `shop_orders_items_details` SET storage_id=@storage WHERE order_item_id=@item (NOT executed)",
                "INSERT OMS log line (NOT executed)"
            ],
            Detail: "Order found in digest window; item-row existence + warehouse reprice stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=update_item");
    }

    private static CpOmsUpdateItemDryRunResult Refuse(
        string status, string code, string detail, CpOmsUpdateItemRequest request) =>
        new(status, 0, true, false, true, code, false,
            request.OrderId, request.ItemId, request.Price, request.CountNeed,
            request.Manufacturer, request.Article, [], detail,
            "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=update_item");
}

public sealed record CpOmsUpdateItemRequest(
    long OrderId,
    long ItemId,
    decimal? Price = null,
    int? CountNeed = null,
    string? Manufacturer = null,
    string? Article = null,
    int? StorageId = null,
    bool ConfirmWrites = false);

public sealed record CpOmsUpdateItemDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, long ItemId, decimal? Price, int? CountNeed,
    string? Manufacturer, string? Article, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new
        {
            order_id = OrderId,
            item_id = ItemId,
            price = Price,
            count_need = CountNeed,
            manufacturer = Manufacturer,
            article = Article
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
