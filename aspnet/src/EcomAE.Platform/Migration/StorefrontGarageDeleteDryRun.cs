namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_operations_cars.php</c> action <c>delete_car</c>.
/// Never executes DELETE. search/check_car/active_car stay PHP for their own dry-runs.
/// </summary>
public interface IStorefrontGarageDeleteDryRun
{
    Task<StorefrontGarageDeleteDryRunResult> EvaluateAsync(
        int userId,
        StorefrontGarageDeleteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontGarageDeleteDryRun : IStorefrontGarageDeleteDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontGarageDeleteDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontGarageDeleteDryRunResult> EvaluateAsync(
        int userId,
        StorefrontGarageDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET garage delete is not implemented; PHP ajax_operations_cars.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP DP_User::getUserId).", request);
        }

        if (request.CarId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "carId must be positive.", request);
        }

        var garage = await _dashboards.ListStorefrontGarageAsync(userId, 100, cancellationToken);
        return EvaluateAgainstVehicles(userId, garage.Vehicles, request);
    }

    public static StorefrontGarageDeleteDryRunResult EvaluateAgainstVehicles(
        int userId,
        IReadOnlyList<StorefrontGarageVehicleDigest> vehicles,
        StorefrontGarageDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET garage delete is not implemented; PHP ajax_operations_cars.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP DP_User::getUserId).", request);
        }

        if (request.CarId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "carId must be positive.", request);
        }

        var car = vehicles.FirstOrDefault(v => v.Id == request.CarId);
        if (car is null)
        {
            return Refuse("dry-run-invalid", "garage_not_owned",
                $"Car {request.CarId} not in customer garage digest (PHP No Access).", request);
        }

        return new StorefrontGarageDeleteDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            CarId: car.Id,
            UserId: userId,
            Caption: car.Caption,
            SimulatedSql:
            [
                "DELETE FROM `shop_docpart_garage` WHERE `id`=@car_id AND `user_id`=@user (NOT executed)",
                "garage_orders unlink / notepad cleanup remain PHP-only if required outside this slice"
            ],
            Detail: "Owned garage car found; DELETE simulated.",
            PhpAjax: "/content/shop/docpart/garage/ajax_operations_cars.php?action=delete_car");
    }

    private static StorefrontGarageDeleteDryRunResult Refuse(
        string status, string code, string detail, StorefrontGarageDeleteRequest request) =>
        new(status, 0, true, false, true, code, false, request.CarId, 0, null,
            [], detail,
            "/content/shop/docpart/garage/ajax_operations_cars.php?action=delete_car");
}

public sealed record StorefrontGarageDeleteRequest(
    long CarId,
    bool ConfirmWrites = false);

public sealed record StorefrontGarageDeleteDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CarId, int UserId, string? Caption,
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
        intended = new { car_id = CarId, caption = Caption },
        current = UserId <= 0 ? null : new { car_id = CarId, user_id = UserId },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
