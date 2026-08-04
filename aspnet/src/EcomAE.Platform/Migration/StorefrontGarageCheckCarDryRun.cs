namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_operations_cars.php</c> action <c>check_car</c>
/// (toggle garage↔order link). Never executes INSERT/DELETE. PHP authoritative.
/// </summary>
public interface IStorefrontGarageCheckCarDryRun
{
    Task<StorefrontGarageCheckCarDryRunResult> EvaluateAsync(
        int userId,
        StorefrontGarageCheckCarRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontGarageCheckCarDryRun : IStorefrontGarageCheckCarDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontGarageCheckCarDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontGarageCheckCarDryRunResult> EvaluateAsync(
        int userId,
        StorefrontGarageCheckCarRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET garage check_car is not implemented; PHP ajax_operations_cars.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP DP_User::getUserId).", request);
        }

        if (request.CarId <= 0 || request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "carId and orderId must be positive.", request);
        }

        var garage = await _dashboards.ListStorefrontGarageAsync(userId, 100, cancellationToken);
        var orders = await _dashboards.ListStorefrontOrdersAsync(userId, 100, cancellationToken);
        return EvaluateAgainstDigests(userId, garage.Vehicles, orders.Orders, request);
    }

    public static StorefrontGarageCheckCarDryRunResult EvaluateAgainstDigests(
        int userId,
        IReadOnlyList<StorefrontGarageVehicleDigest> vehicles,
        IReadOnlyList<StorefrontOrderDigest> orders,
        StorefrontGarageCheckCarRequest request)
    {
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET garage check_car is not implemented; PHP ajax_operations_cars.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP DP_User::getUserId).", request);
        }

        if (request.CarId <= 0 || request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "carId and orderId must be positive.", request);
        }

        var car = vehicles.FirstOrDefault(v => v.Id == request.CarId);
        if (car is null)
        {
            return Refuse("dry-run-invalid", "garage_not_owned",
                $"Car {request.CarId} not in customer garage digest (PHP No Access).", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not in customer /storefront/orders digest window.", request);
        }

        return new StorefrontGarageCheckCarDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            CarId: car.Id,
            OrderId: order.Id,
            UserId: userId,
            SimulatedSql:
            [
                "SELECT id FROM `shop_docpart_garage_orders` WHERE order_id=@order AND garage_id=@car (NOT executed)",
                "TOGGLE: DELETE link if present else INSERT `shop_docpart_garage_orders` (NOT executed)",
                "Link presence (flag 0/1) stays PHP until dual-sample — digest has no garage_orders join"
            ],
            Detail: "Owned car + customer order found; garage↔order toggle simulated. Current link flag stays PHP.",
            PhpAjax: "/content/shop/docpart/garage/ajax_operations_cars.php?action=check_car");
    }

    private static StorefrontGarageCheckCarDryRunResult Refuse(
        string status, string code, string detail, StorefrontGarageCheckCarRequest request) =>
        new(status, 0, true, false, true, code, false, request.CarId, request.OrderId, 0,
            [], detail,
            "/content/shop/docpart/garage/ajax_operations_cars.php?action=check_car");
}

public sealed record StorefrontGarageCheckCarRequest(
    long CarId,
    long OrderId,
    bool ConfirmWrites = false);

public sealed record StorefrontGarageCheckCarDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CarId, long OrderId, int UserId,
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
        intended = new { car_id = CarId, order_id = OrderId },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
