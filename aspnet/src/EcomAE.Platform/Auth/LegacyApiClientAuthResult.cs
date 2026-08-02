namespace EcomAE.Platform.Auth;

public sealed record LegacyApiClientAuthResult(
    bool Succeeded,
    int StatusCode,
    string Code,
    string Message,
    LegacyApiClientRecord? Client = null,
    string? KeyProduct = null)
{
    public static LegacyApiClientAuthResult Ok(LegacyApiClientRecord client, string keyProduct) =>
        new(true, 200, string.Empty, string.Empty, client, keyProduct);

    public static LegacyApiClientAuthResult Fail(int statusCode, string code, string message) =>
        new(false, statusCode, code, message);
}
