using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_integrations.php</c> twin of <c>save_mobile</c> / <c>epc_integrations_save_tenant_config</c>.
/// Schema-ensure and SMTP stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpMobileAppsWriteService
{
    Task<ErpSimpleWriteResult> SaveMobileAsync(
        CpMobileAppsSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpMobileAppsSaveRequest(
    bool Enabled,
    string? AppName,
    string? BundleId,
    string? DeepLinkScheme,
    string? DeepLinkDomain,
    string? ApiBaseUrl,
    string? PlayStoreUrl,
    string? AppStoreUrl,
    bool PwaEnabled,
    string? FirebaseProjectId,
    bool PushEnabled);

public sealed class CpMobileAppsWriteService : ICpMobileAppsWriteService
{
    private static readonly JsonSerializerOptions JsonWrite = new()
    {
        WriteIndented = false,
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpMobileAppsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>substr(trim(...), 0, $max)</c>.</summary>
    public static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }

    public static JsonObject MergeMobile(JsonNode? root, CpMobileAppsSaveRequest request)
    {
        var obj = root as JsonObject ?? new JsonObject();
        obj["mobile"] = new JsonObject
        {
            ["enabled"] = request.Enabled,
            ["app_name"] = Clip(request.AppName, 120),
            ["bundle_id"] = Clip(request.BundleId, 120),
            ["deep_link_scheme"] = Clip(request.DeepLinkScheme, 64),
            ["deep_link_domain"] = Clip(request.DeepLinkDomain, 120),
            ["api_base_url"] = Clip(request.ApiBaseUrl, 255),
            ["play_store_url"] = Clip(request.PlayStoreUrl, 255),
            ["app_store_url"] = Clip(request.AppStoreUrl, 255),
            ["pwa_enabled"] = request.PwaEnabled,
            ["firebase_project_id"] = Clip(request.FirebaseProjectId, 120),
            ["push_enabled"] = request.PushEnabled,
        };
        return obj;
    }

    public async Task<ErpSimpleWriteResult> SaveMobileAsync(
        CpMobileAppsSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var id = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `epc_portal_site_settings` ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (id <= 0)
            {
                return ErpSimpleWriteResult.Fail("db", "Site settings row is missing — schema-ensure stays Classic.");
            }

            var raw = await ErpDb.StringAsync(
                connection,
                null,
                "SELECT IFNULL(`integrations_json`, '') FROM `epc_portal_site_settings` WHERE `id` = @p0 LIMIT 1",
                cancellationToken,
                id).ConfigureAwait(false) ?? string.Empty;

            JsonNode? root = null;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    root = JsonNode.Parse(raw);
                }
                catch (JsonException)
                {
                    return ErpSimpleWriteResult.Fail("bad_json", "integrations_json is not valid JSON.");
                }
            }

            var merged = MergeMobile(root, request);
            var json = merged.ToJsonString(JsonWrite);
            var writes = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_portal_site_settings` SET `integrations_json` = ?, `updated_at` = ? WHERE `id` = ?"),
                cancellationToken,
                json,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                id).ConfigureAwait(false);

            return writes > 0
                ? ErpSimpleWriteResult.Ok("Mobile settings saved.", id)
                : ErpSimpleWriteResult.Fail("unchanged", "Mobile settings were not updated.");
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Site settings table is missing — schema-ensure stays Classic.");
        }
    }
}
