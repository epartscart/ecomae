namespace EcomAE.Platform.Auth;

/// <summary>
/// Mirrors PHP <c>epc_api_client_require_auth</c> for exact-route ASP.NET API cutover.
/// </summary>
public sealed class LegacyApiClientAuthenticator : ILegacyApiClientAuthenticator
{
    private readonly ILegacyApiClientStore _store;
    private readonly ILegacyApiUsageLogger _usageLogger;
    private readonly TimeProvider _timeProvider;

    public LegacyApiClientAuthenticator(
        ILegacyApiClientStore store,
        ILegacyApiUsageLogger usageLogger,
        TimeProvider timeProvider)
    {
        _store = store;
        _usageLogger = usageLogger;
        _timeProvider = timeProvider;
    }

    public async Task<LegacyApiClientAuthResult> RequireAsync(
        HttpRequest request,
        string needProduct,
        string? action,
        CancellationToken cancellationToken = default)
    {
        if (!_store.IsConfigured)
        {
            return LegacyApiClientAuthResult.Fail(503, "platform_db_unavailable", "API client registry unavailable.");
        }

        var raw = LegacyApiClientKeyParser.ExtractFromRequest(request);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return LegacyApiClientAuthResult.Fail(
                401,
                "missing_api_key",
                "Send X-API-Key: epc_catalog_… or epc_pricepro_… (issued on onboarding).");
        }

        var parsed = LegacyApiClientKeyParser.Parse(raw);
        if (parsed is null)
        {
            return LegacyApiClientAuthResult.Fail(401, "invalid_key_format", "Key must start with epc_catalog_ or epc_pricepro_.");
        }

        var client = await _store.FindActiveByHashAsync(parsed.Sha256Hash, cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return LegacyApiClientAuthResult.Fail(401, "invalid_api_key", "API key not recognized or revoked.");
        }

        if (!string.Equals(parsed.Product, needProduct, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(needProduct, "both", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(client.Product, "both", StringComparison.OrdinalIgnoreCase))
        {
            return LegacyApiClientAuthResult.Fail(403, "wrong_product_key", $"This endpoint requires a {needProduct} client key.");
        }

        if (!LegacyApiClientPolicy.ProductAllowed(client, needProduct))
        {
            return LegacyApiClientAuthResult.Fail(403, "product_not_enabled", $"This key is not enabled for {needProduct}.");
        }

        if (!string.IsNullOrWhiteSpace(action) && !LegacyApiClientPolicy.ActionAllowed(client, action))
        {
            return LegacyApiClientAuthResult.Fail(403, "action_not_allowed", $"Action not permitted for this client: {action}");
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        await _store.ResetDailyQuotaIfNeededAsync(client, today, cancellationToken).ConfigureAwait(false);
        client = await _store.FindActiveByHashAsync(parsed.Sha256Hash, cancellationToken).ConfigureAwait(false) ?? client;

        if (!LegacyApiClientPolicy.QuotaAvailable(client))
        {
            await _usageLogger.LogAsync(new LegacyApiUsageLogEntry(
                action ?? "auth",
                string.Empty,
                "api_client",
                client.Id,
                request.Path.HasValue ? request.Path.Value! : string.Empty,
                429,
                QuotaBlocked: true,
                "Daily quota exceeded",
                request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty), cancellationToken).ConfigureAwait(false);

            return LegacyApiClientAuthResult.Fail(429, "daily_quota_exceeded", "Daily API quota exceeded. Contact support to raise your limit.");
        }

        if (!await _store.TryConsumeDailyQuotaAsync(client, cancellationToken).ConfigureAwait(false))
        {
            return LegacyApiClientAuthResult.Fail(429, "daily_quota_exceeded", "Daily API quota exceeded.");
        }

        return LegacyApiClientAuthResult.Ok(client, parsed.Product);
    }
}
