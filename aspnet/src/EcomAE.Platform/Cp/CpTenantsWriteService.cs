using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_portal.php</c> twin of <c>tenant_set_active</c> / <c>epc_portal_tenant_control_set_active</c>.
/// Password reset, reveal, demo access, and schema-ensure stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpTenantsWriteService
{
    Task<ErpSimpleWriteResult> SetActiveAsync(
        CpTenantsSetActiveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpTenantsSetActiveRequest(string? SiteKey, bool Active);

public sealed class CpTenantsWriteService : ICpTenantsWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpTenantsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', strtolower($site_key))</c>.</summary>
    public static string NormalizeSiteKey(string? siteKey)
        => SiteKeySafe.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string ToggleMessage(bool active)
        => active ? "Tenant enabled" : "Tenant disabled — storefront and CP blocked";

    public async Task<ErpSimpleWriteResult> SetActiveAsync(
        CpTenantsSetActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid site key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var active = request.Active ? 1 : 0;

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var id = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_portal_tenants` WHERE `site_key`=? LIMIT 1"),
                cancellationToken, siteKey).ConfigureAwait(false);
            if (id <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Tenant not in registry");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_portal_tenants` SET `is_active`=?, `updated_at`=? WHERE `site_key`=?"),
                cancellationToken, active, now, siteKey).ConfigureAwait(false);

            return ErpSimpleWriteResult.Ok(ToggleMessage(request.Active), id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Tenant registry table is missing — schema-ensure stays Classic.");
        }
    }
}
