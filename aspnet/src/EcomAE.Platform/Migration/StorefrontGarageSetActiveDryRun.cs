namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_operations_cars.php</c> action <c>active_car</c>.
/// Never executes UPDATE. Delete/search/check_car stay PHP-authoritative.
/// </summary>
public interface IStorefrontGarageSetActiveDryRun
{
    Task<StorefrontGarageSetActiveDryRunResult> EvaluateAsync(
        int userId,
        StorefrontGarageSetActiveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontGarageSetActiveDryRun : IStorefrontGarageSetActiveDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontGarageSetActiveDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontGarageSetActiveDryRunResult> EvaluateAsync(
        int userId,
        StorefrontGarageSetActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET garage set-active is not implemented; PHP ajax_operations_cars.php remains authoritative.",
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

    public static StorefrontGarageSetActiveDryRunResult EvaluateAgainstVehicles(
        int userId,
        IReadOnlyList<StorefrontGarageVehicleDigest> vehicles,
        StorefrontGarageSetActiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET garage set-active is not implemented; PHP ajax_operations_cars.php remains authoritative.",
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

        var currentlyActiveId = vehicles.FirstOrDefault(v => v.Active == 1)?.Id ?? 0;
        var wouldActivate = currentlyActiveId != request.CarId;

        return new StorefrontGarageSetActiveDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            CarId: car.Id,
            UserId: userId,
            CurrentlyActive: car.Active == 1,
            WouldActivate: wouldActivate,
            SimulatedSql:
            [
                "UPDATE `shop_docpart_garage` SET `active`=0 WHERE `user_id`=@user (NOT executed)",
                wouldActivate
                    ? "UPDATE `shop_docpart_garage` SET `active`=1 WHERE `id`=@car_id (NOT executed)"
                    : "PHP toggle-off: previously active car equals car_id — leave all inactive (NOT executed)",
                "delete_car / check_car / search remain PHP-only in this dry-run slice"
            ],
            Detail: wouldActivate
                ? "Owned garage car found; clear-all then set-active UPDATE simulated."
                : "Owned garage car is already active; PHP clears active flags (toggle-off) — simulated.",
            PhpAjax: "/content/shop/docpart/garage/ajax_operations_cars.php?action=active_car");
    }

    private static StorefrontGarageSetActiveDryRunResult Refuse(
        string status, string code, string detail, StorefrontGarageSetActiveRequest request) =>
        new(status, 0, true, false, true, code, false, request.CarId, 0, false, false,
            [], detail,
            "/content/shop/docpart/garage/ajax_operations_cars.php?action=active_car");
}

public sealed record StorefrontGarageSetActiveRequest(
    long CarId,
    bool ConfirmWrites = false);

public sealed record StorefrontGarageSetActiveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CarId, int UserId, bool CurrentlyActive,
    bool WouldActivate, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { car_id = CarId, would_activate = WouldActivate },
        current = UserId <= 0 ? null : new { car_id = CarId, active = CurrentlyActive, user_id = UserId },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
