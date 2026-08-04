namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_add_to_notepad.php</c> (garage notepad INSERT).
/// Never executes writes. PHP remains authoritative.
/// </summary>
public interface IStorefrontGarageNotepadAddDryRun
{
    Task<StorefrontGarageNotepadAddDryRunResult> EvaluateAsync(
        int userId,
        StorefrontGarageNotepadAddRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontGarageNotepadAddDryRun : IStorefrontGarageNotepadAddDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontGarageNotepadAddDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontGarageNotepadAddDryRunResult> EvaluateAsync(
        int userId,
        StorefrontGarageNotepadAddRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET garage notepad add is not implemented; PHP ajax_add_to_notepad.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP DP_User::getUserId).", request);
        }

        var article = (request.Article ?? string.Empty).Trim();
        if (article.Length == 0)
        {
            return Refuse("dry-run-invalid", "article_required",
                "Article is required (PHP message 2068).", request);
        }

        IReadOnlyList<StorefrontGarageVehicleDigest> vehicles = [];
        if (request.GarageId > 0)
        {
            var garage = await _dashboards.ListStorefrontGarageAsync(userId, 100, cancellationToken);
            vehicles = garage.Vehicles;
        }

        return EvaluateProduct(userId, vehicles, request with
        {
            Article = article,
            Manufacturer = (request.Manufacturer ?? string.Empty).Trim(),
            Name = (request.Name ?? string.Empty).Trim()
        });
    }

    public static StorefrontGarageNotepadAddDryRunResult EvaluateProduct(
        int userId,
        IReadOnlyList<StorefrontGarageVehicleDigest> vehicles,
        StorefrontGarageNotepadAddRequest request)
    {
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET garage notepad add is not implemented; PHP ajax_add_to_notepad.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP DP_User::getUserId).", request);
        }

        var article = (request.Article ?? string.Empty).Trim();
        if (article.Length == 0)
        {
            return Refuse("dry-run-invalid", "article_required",
                "Article is required (PHP message 2068).", request);
        }

        if (request.GarageId > 0 && vehicles.All(v => v.Id != request.GarageId))
        {
            return Refuse("dry-run-invalid", "garage_not_owned",
                $"Garage {request.GarageId} not in customer garage digest (PHP message 2064).", request);
        }

        var brand = (request.Manufacturer ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();

        return new StorefrontGarageNotepadAddDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            UserId: userId,
            GarageId: request.GarageId,
            Manufacturer: brand,
            Article: article,
            Name: name,
            Exist: request.Exist,
            Price: request.Price,
            SimulatedSql:
            [
                "INSERT INTO `shop_docpart_garage_notepad` (`user_id`, `garage_id`, `brend`, `article`, `name`, `exist`, `price`, `comment`) VALUES (?,?,?,?,?,?,?,?) (NOT executed)"
            ],
            Detail: "Customer + article validated; garage ownership checked when garageId>0. Live INSERT stays PHP until dual-sample.",
            PhpAjax: "/content/shop/docpart/garage/ajax_add_to_notepad.php");
    }

    private static StorefrontGarageNotepadAddDryRunResult Refuse(
        string status, string code, string detail, StorefrontGarageNotepadAddRequest request) =>
        new(status, 0, true, false, true, code, false, 0, request.GarageId,
            request.Manufacturer, request.Article, request.Name, request.Exist, request.Price,
            [], detail, "/content/shop/docpart/garage/ajax_add_to_notepad.php");
}

public sealed record StorefrontGarageNotepadAddRequest(
    long GarageId,
    string? Manufacturer,
    string? Article,
    string? Name = null,
    int Exist = 0,
    decimal Price = 0,
    bool ConfirmWrites = false);

public sealed record StorefrontGarageNotepadAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, int UserId, long GarageId, string? Manufacturer,
    string? Article, string? Name, int Exist, decimal Price, IReadOnlyList<string> SimulatedSql,
    string Detail, string PhpAjax)
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
            garage_id = GarageId,
            manufacturer = Manufacturer,
            article = Article,
            name = Name,
            exist = Exist,
            price = Price
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
