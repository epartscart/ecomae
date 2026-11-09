namespace EcomAE.Platform.Storefront;

/// <summary>
/// OEM (original catalog) drill-down extension point. The Auto Parts package implements
/// this against the shared platform Laximo credentials (Guayaquil SOAP twin); Standard
/// Core holds only the contract so /api/laximo_proxy.php actions have a target when the
/// industry package is disabled.
/// </summary>
public interface ILaximoCatalogService
{
    Task<LaximoCatalogResult> ExecuteAsync(
        LaximoCatalogRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record LaximoCatalogRequest(
    string Action,
    string? Catalog = null,
    string? Ssd = null,
    string? VehicleId = null,
    string? CategoryId = null,
    string? UnitId = null,
    string? GroupId = null,
    string? Value = null,
    string? Query = null,
    string? Vin = null,
    string? DetailId = null,
    string? Oem = null);

public sealed record LaximoCatalogResult(bool Ok, int HttpStatus, string Json)
{
    public static LaximoCatalogResult Payload(string json) => new(true, StatusCodes.Status200OK, json);
    public static LaximoCatalogResult Error(int status, string code, string message)
        => new(false, status,
            System.Text.Json.JsonSerializer.Serialize(new { success = false, error = message, code }));
}

/// <summary>Core fallback: industry.auto_parts disabled → the proxy keeps answering 503.</summary>
public sealed class UnavailableLaximoCatalogService : ILaximoCatalogService
{
    public Task<LaximoCatalogResult> ExecuteAsync(
        LaximoCatalogRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(LaximoCatalogResult.Error(
            StatusCodes.Status503ServiceUnavailable,
            "package_disabled",
            "Laximo catalog service is unavailable for this storefront."));
}
