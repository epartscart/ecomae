namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_checkout_create.php</c>.
/// Never executes INSERT into shop_orders / items. PHP remains authoritative.
/// </summary>
public interface IStorefrontCheckoutCreateDryRun
{
    Task<StorefrontCheckoutCreateDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCheckoutCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontCheckoutCreateDryRun : IStorefrontCheckoutCreateDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontCheckoutCreateDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontCheckoutCreateDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCheckoutCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Touch cart digest so dry-run exercises the same DB gate as other cart posts.
        var cart = await _dashboards.ListStorefrontCartAsync(userId, 50, cancellationToken);
        return EvaluateShape(userId, request, cart.Count, cart.Source);
    }

    public static StorefrontCheckoutCreateDryRunResult EvaluateShape(
        int userId,
        StorefrontCheckoutCreateRequest request,
        int cartCount,
        string cartSource)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET checkout create is not implemented; PHP ajax_checkout_create.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required for checkout create dry-run (guest cookie path stays PHP).", request);
        }

        if (request.HowGetMode <= 0)
        {
            return Refuse("dry-run-invalid", "how_get_missing",
                "howGetMode required (PHP how_get cookie mode).", request);
        }

        if (cartCount <= 0 && string.Equals(cartSource, "database", StringComparison.Ordinal))
        {
            return Refuse("dry-run-invalid", "cart_empty",
                "Cart digest empty for user — PHP would refuse order create with no basket lines.", request);
        }

        return new StorefrontCheckoutCreateDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            HowGetMode: request.HowGetMode,
            OfficeId: request.OfficeId,
            CartLineHint: cartCount,
            SimulatedSql:
            [
                "INSERT INTO `shop_orders` (…) (NOT executed)",
                "INSERT INTO `shop_orders_items` … from cart (NOT executed)",
                "INSERT INTO `shop_orders_items_details` … (NOT executed)",
                "DELETE FROM cart / clear guest cookies (NOT executed)"
            ],
            Detail: "Payload shape + cart presence validated; order create INSERT blocked. Office/how_get mode edge cases stay PHP until dual-sample.",
            PhpAjax: "/content/shop/order_process/ajax_checkout_create.php");
    }

    private static StorefrontCheckoutCreateDryRunResult Refuse(
        string status, string code, string detail, StorefrontCheckoutCreateRequest request) =>
        new(status, 0, true, false, true, code, false, request.HowGetMode, request.OfficeId, 0,
            [], detail, "/content/shop/order_process/ajax_checkout_create.php");
}

public sealed record StorefrontCheckoutCreateRequest(
    int HowGetMode,
    int? OfficeId = null,
    string? PhoneNotAuth = null,
    string? EmailNotAuth = null,
    bool ConfirmWrites = false);

public sealed record StorefrontCheckoutCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, int HowGetMode, int? OfficeId, int CartLineHint,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { how_get_mode = HowGetMode, office_id = OfficeId, cart_line_hint = CartLineHint },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
