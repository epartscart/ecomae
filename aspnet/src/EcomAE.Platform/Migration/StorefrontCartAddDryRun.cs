namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_add_to_basket.php</c> product_type=2 INSERT.
/// Never executes INSERT. PHP remains authoritative.
/// </summary>
public interface IStorefrontCartAddDryRun
{
    Task<StorefrontCartAddDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCartAddRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontCartAddDryRun : IStorefrontCartAddDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontCartAddDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontCartAddDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCartAddRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET add-to-cart is not implemented; PHP ajax_add_to_basket.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Authenticated customer userId required (guest path stays PHP).", request);
        }

        // Touch cart digest so dry-run exercises the same DB gate as other cart posts.
        _ = await _dashboards.ListStorefrontCartAsync(userId, 10, cancellationToken);
        return EvaluateProduct(request);
    }

    public static StorefrontCartAddDryRunResult EvaluateProduct(StorefrontCartAddRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET add-to-cart is not implemented; PHP ajax_add_to_basket.php remains authoritative.",
                request);
        }

        if (request.ProductType != 2)
        {
            return Refuse("dry-run-needs-sample", "product_type_unsupported",
                "Only product_type=2 (docpart) dry-run in this slice; type-1 reserve path stays PHP.", request);
        }

        if (string.IsNullOrWhiteSpace(request.Manufacturer) || string.IsNullOrWhiteSpace(request.Article))
        {
            return Refuse("dry-run-invalid", "incorrect_data", "manufacturer and article are required.", request);
        }

        if (request.CountNeed <= 0 || request.Price < 0)
        {
            return Refuse("dry-run-invalid", "incorrect_data", "countNeed must be > 0 and price >= 0.", request);
        }

        if (request.MinOrder > 0 && request.CountNeed < request.MinOrder)
        {
            return Refuse("dry-run-invalid", "below_min_order",
                $"countNeed {request.CountNeed} below t2_min_order {request.MinOrder}.", request);
        }

        if (request.Exist > 0 && request.CountNeed > request.Exist)
        {
            return Refuse("dry-run-invalid", "not_enough",
                $"countNeed {request.CountNeed} exceeds t2_exist {request.Exist}.", request);
        }

        return new StorefrontCartAddDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            ProductType: 2,
            Manufacturer: request.Manufacturer.Trim(),
            Article: request.Article.Trim(),
            CountNeed: request.CountNeed,
            Price: request.Price,
            SimulatedSql: "INSERT INTO `shop_carts` (product_type=2, price, count_need, user_id, session_id=0, t2_*) VALUES (…) (NOT executed)",
            Detail: "Type-2 add-to-cart INSERT would be valid under basic PHP guards; hash/margin/duplicate checks stay PHP-authoritative until dual-sample.",
            PhpAjax: "/content/shop/order_process/ajax_add_to_basket.php");
    }

    private static StorefrontCartAddDryRunResult Refuse(
        string status, string code, string detail, StorefrontCartAddRequest request) =>
        new(status, 0, true, false, true, code, false, request.ProductType,
            request.Manufacturer ?? "", request.Article ?? "", request.CountNeed, request.Price,
            null, detail, "/content/shop/order_process/ajax_add_to_basket.php");
}

public sealed record StorefrontCartAddRequest(
    int ProductType,
    string? Manufacturer,
    string? Article,
    decimal CountNeed,
    decimal Price,
    decimal MinOrder = 0,
    decimal Exist = 0,
    bool ConfirmWrites = false);

public sealed record StorefrontCartAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, int ProductType, string Manufacturer, string Article,
    decimal CountNeed, decimal Price, string? SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "storefront",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new
        {
            product_type = ProductType,
            manufacturer = Manufacturer,
            article = Article,
            count_need = CountNeed,
            price = Price
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
