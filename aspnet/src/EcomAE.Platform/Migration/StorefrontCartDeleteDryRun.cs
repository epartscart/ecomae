namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_delete_cart_record.php</c>.
/// Type-2: single DELETE shop_carts. Type-1: reserve release path not executed here.
/// </summary>
public interface IStorefrontCartDeleteDryRun
{
    Task<StorefrontCartDeleteDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCartDeleteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontCartDeleteDryRun : IStorefrontCartDeleteDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontCartDeleteDryRun(ISurfaceDashboardSummaryReporter dashboards)
    {
        _dashboards = dashboards;
    }

    public async Task<StorefrontCartDeleteDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCartDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET cart delete is not implemented; PHP ajax_delete_cart_record.php remains authoritative.",
                request,
                []);
        }

        if (userId <= 0 || request.RecordsToDel.Count == 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "userId and records_to_del[] are required.",
                request,
                []);
        }

        var cart = await _dashboards.ListStorefrontCartAsync(userId, 500, cancellationToken);
        return EvaluateOwnedLines(cart.Lines, request);
    }

    public static StorefrontCartDeleteDryRunResult EvaluateOwnedLines(
        IReadOnlyList<StorefrontCartLineDigest> lines,
        StorefrontCartDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET cart delete is not implemented; PHP ajax_delete_cart_record.php remains authoritative.",
                request,
                []);
        }

        if (request.RecordsToDel.Count == 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "records_to_del[] required.",
                request,
                []);
        }

        var planned = new List<StorefrontCartDeletePlannedRow>();
        foreach (var id in request.RecordsToDel.Distinct())
        {
            if (id <= 0)
            {
                return Refuse(
                    "dry-run-invalid",
                    "invalid_request",
                    "record ids must be positive.",
                    request,
                    planned);
            }

            var line = lines.FirstOrDefault(l => l.Id == id);
            if (line is null)
            {
                return Refuse(
                    "dry-run-invalid",
                    "alien_cart",
                    $"Cart line {id} not found for authenticated user (PHP alien_cart).",
                    request,
                    planned);
            }

            if (line.ProductType == 1)
            {
                return Refuse(
                    "dry-run-needs-sample",
                    "product_type_unsupported",
                    "product_type=1 delete releases shop_storages_data reserves — not in this Wave B slice; use PHP.",
                    request,
                    planned);
            }

            if (line.ProductType != 2)
            {
                return Refuse(
                    "dry-run-invalid",
                    "product_type_unknown",
                    $"Unsupported product_type={line.ProductType}.",
                    request,
                    planned);
            }

            planned.Add(new StorefrontCartDeletePlannedRow(id, line.ProductType));
        }

        return new StorefrontCartDeleteDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            Planned: planned,
            SimulatedSql: "DELETE FROM `shop_carts` WHERE `id` = @id (type-2 only — NOT executed)",
            Detail: $"Would delete {planned.Count} type-2 cart line(s); write remains blocked until dual-sample + approval.",
            PhpAjax: "/content/shop/order_process/ajax_delete_cart_record.php",
            RequestRecords: request.RecordsToDel);
    }

    private static StorefrontCartDeleteDryRunResult Refuse(
        string status,
        string validationCode,
        string detail,
        StorefrontCartDeleteRequest request,
        IReadOnlyList<StorefrontCartDeletePlannedRow> planned) =>
        new(
            Status: status,
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: validationCode,
            WouldWrite: false,
            Planned: planned,
            SimulatedSql: null,
            Detail: detail,
            PhpAjax: "/content/shop/order_process/ajax_delete_cart_record.php",
            RequestRecords: request.RecordsToDel);
}

public sealed record StorefrontCartDeleteRequest(IReadOnlyList<long> RecordsToDel, bool ConfirmWrites = false);

public sealed record StorefrontCartDeletePlannedRow(long CartRecordId, int ProductType);

public sealed record StorefrontCartDeleteDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    IReadOnlyList<StorefrontCartDeletePlannedRow> Planned,
    string? SimulatedSql,
    string Detail,
    string PhpAjax,
    IReadOnlyList<long> RequestRecords)
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
        records_to_del = Planned.Select(p => new { id = p.CartRecordId, product_type = p.ProductType }),
        intended = new { records_to_del = RequestRecords },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
