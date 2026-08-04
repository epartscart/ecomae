using System.Globalization;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_change_count_need.php</c> product_type=2 path.
/// SELECT-only validation; never executes UPDATE. PHP remains authoritative.
/// </summary>
public interface IStorefrontCartChangeCountNeedDryRun
{
    Task<StorefrontCartChangeCountNeedDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCartChangeCountNeedRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontCartChangeCountNeedDryRun : IStorefrontCartChangeCountNeedDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontCartChangeCountNeedDryRun(ISurfaceDashboardSummaryReporter dashboards)
    {
        _dashboards = dashboards;
    }

    public async Task<StorefrontCartChangeCountNeedDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCartChangeCountNeedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes requested but live ASP.NET cart qty write is not implemented; writes remain blocked (PHP ajax_change_count_need.php authoritative).",
                request,
                productType: null,
                currentCountNeed: null,
                intendedCountNeed: request.CountNeed,
                validationCode: "confirm_writes_refused");
        }

        if (userId <= 0 || request.Id <= 0)
        {
            return Refuse(
                "dry-run-invalid",
                "userId and cart line id are required.",
                request,
                productType: null,
                currentCountNeed: null,
                intendedCountNeed: request.CountNeed,
                validationCode: "invalid_request");
        }

        var cart = await _dashboards.ListStorefrontCartAsync(userId, 500, cancellationToken);
        return EvaluateOwnedLines(cart.Lines, request);
    }

    /// <summary>Pure type-2 validation used by unit tests and the HTTP dry-run.</summary>
    public static StorefrontCartChangeCountNeedDryRunResult EvaluateOwnedLines(
        IReadOnlyList<StorefrontCartLineDigest> lines,
        StorefrontCartChangeCountNeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes requested but live ASP.NET cart qty write is not implemented; writes remain blocked (PHP ajax_change_count_need.php authoritative).",
                request,
                productType: null,
                currentCountNeed: null,
                intendedCountNeed: request.CountNeed,
                validationCode: "confirm_writes_refused");
        }

        var line = lines.FirstOrDefault(l => l.Id == request.Id);
        if (line is null)
        {
            return Refuse(
                "dry-run-invalid",
                "Cart line not found for authenticated user (session_id=0 ownership gate).",
                request,
                productType: null,
                currentCountNeed: null,
                intendedCountNeed: request.CountNeed,
                validationCode: "cart_item_not_found");
        }

        if (line.ProductType != 2)
        {
            return Refuse(
                "dry-run-needs-sample",
                "product_type=1 (catalog reserve) dry-run not in this Wave B slice; use PHP ajax_change_count_need.php.",
                request,
                productType: line.ProductType,
                currentCountNeed: line.CountNeed,
                intendedCountNeed: request.CountNeed,
                validationCode: "product_type_unsupported");
        }

        var current = line.CountNeed;
        var exist = line.T2Exist;
        var minOrder = line.MinOrder <= 0 ? 1m : line.MinOrder;
        var intended = request.CountNeed;

        if (intended == current)
        {
            return Validated(
                request,
                line.ProductType,
                current,
                intended,
                "the_same_count",
                wouldWrite: false,
                simulatedSql: null,
                detail: "Requested count equals current count_need — PHP would return the_same_count.");
        }

        if (intended > exist)
        {
            return Validated(
                request,
                line.ProductType,
                current,
                intended,
                "not_enough",
                wouldWrite: false,
                simulatedSql: null,
                detail: "Requested count_need exceeds t2_exist — PHP would refuse without raising qty.");
        }

        if (intended < exist)
        {
            var multipleOk = false;
            if (minOrder > 0)
            {
                for (var i = minOrder; i <= exist; i += minOrder)
                {
                    if (i == intended)
                    {
                        multipleOk = true;
                        break;
                    }
                }
            }

            if (!multipleOk)
            {
                return Validated(
                    request,
                    line.ProductType,
                    current,
                    intended,
                    "error",
                    wouldWrite: false,
                    simulatedSql: "UPDATE `shop_carts` SET `count_need` = @t2_min_order WHERE `id` = @id (PHP clamp on invalid multiple — NOT executed)",
                    detail: "count_need is not a multiple of t2_min_order within t2_exist — PHP clamps to min_order (write blocked here).");
            }
        }

        if (intended < minOrder)
        {
            return Validated(
                request,
                line.ProductType,
                current,
                intended,
                "below_min_order",
                wouldWrite: false,
                simulatedSql: "UPDATE `shop_carts` SET `count_need` = @t2_min_order WHERE `id` = @id (PHP path — NOT executed)",
                detail: "Below t2_min_order — PHP would clamp; ASP.NET dry-run does not write.");
        }

        return Validated(
            request,
            line.ProductType,
            current,
            intended,
            "ok",
            wouldWrite: true,
            simulatedSql: "UPDATE `shop_carts` SET `count_need` = @count_need WHERE `id` = @id (NOT executed — dry-run)",
            detail: "Type-2 qty change would be valid under PHP rules; write remains blocked until dual-sample + approval.");
    }

    private static StorefrontCartChangeCountNeedDryRunResult Validated(
        StorefrontCartChangeCountNeedRequest request,
        int productType,
        decimal current,
        decimal intended,
        string validationCode,
        bool wouldWrite,
        string? simulatedSql,
        string detail) =>
        new(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: validationCode,
            WouldWrite: wouldWrite,
            ProductType: productType,
            CurrentCountNeed: current,
            IntendedCountNeed: intended,
            SimulatedSql: simulatedSql,
            Detail: detail,
            PhpAjax: "/content/shop/order_process/ajax_change_count_need.php",
            RequestId: request.Id);

    private static StorefrontCartChangeCountNeedDryRunResult Refuse(
        string status,
        string detail,
        StorefrontCartChangeCountNeedRequest request,
        int? productType,
        decimal? currentCountNeed,
        decimal intendedCountNeed,
        string validationCode) =>
        new(
            Status: status,
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: validationCode,
            WouldWrite: false,
            ProductType: productType,
            CurrentCountNeed: currentCountNeed,
            IntendedCountNeed: intendedCountNeed,
            SimulatedSql: null,
            Detail: detail,
            PhpAjax: "/content/shop/order_process/ajax_change_count_need.php",
            RequestId: request.Id);
}

public sealed record StorefrontCartChangeCountNeedRequest(int Id, decimal CountNeed, bool ConfirmWrites = false);

public sealed record StorefrontCartChangeCountNeedDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    int? ProductType,
    decimal? CurrentCountNeed,
    decimal? IntendedCountNeed,
    string? SimulatedSql,
    string Detail,
    string PhpAjax,
    int RequestId)
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
        product_type = ProductType,
        current_count_need = CurrentCountNeed?.ToString(CultureInfo.InvariantCulture),
        intended = new
        {
            id = RequestId,
            count_need = IntendedCountNeed?.ToString(CultureInfo.InvariantCulture),
            product_type = ProductType
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
