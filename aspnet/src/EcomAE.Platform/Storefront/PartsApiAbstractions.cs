namespace EcomAE.Platform.Storefront;

/// <summary>
/// TecDoc catalog extension point backed by partsapi.ru. The Auto Parts package
/// implements this against the shared platform API key (one EcomAE-level
/// subscription reused by every auto-parts tenant); Standard Core holds only the
/// contract so /api/partsapi_proxy.php actions have a target when the industry
/// package is disabled.
/// </summary>
public interface IPartsApiService
{
    Task<PartsApiResult> ExecuteAsync(
        PartsApiRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// <paramref name="Method"/> is the provider's method name (getMakes, tecdocCrosses,
/// VINdecode, ...); <paramref name="Params"/> carries the method's own documented
/// parameters verbatim — the proxy never renames them.
/// </summary>
public sealed record PartsApiRequest(
    string Method,
    IReadOnlyDictionary<string, string> Params);

public sealed record PartsApiResult(bool Ok, int HttpStatus, string Json)
{
    public static PartsApiResult Payload(string json) => new(true, StatusCodes.Status200OK, json);
    public static PartsApiResult Error(int status, string code, string message)
        => new(false, status,
            System.Text.Json.JsonSerializer.Serialize(new { success = false, error = message, code }));
}

/// <summary>Core fallback: industry.auto_parts disabled → the proxy keeps answering 503.</summary>
public sealed class UnavailablePartsApiService : IPartsApiService
{
    public Task<PartsApiResult> ExecuteAsync(
        PartsApiRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PartsApiResult.Error(
            StatusCodes.Status503ServiceUnavailable,
            "package_disabled",
            "partsapi.ru catalog service is unavailable for this storefront."));
}
