using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>marketplace</c> <c>uninstall</c> / <c>epc_marketplace_uninstall</c>.
/// Install, review, seed, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/not-found checks.
/// </summary>
public interface IBosMarketplaceWriteService
{
    Task<ErpSimpleWriteResult> UninstallAsync(
        long appId,
        string? siteKey,
        CancellationToken cancellationToken = default);
}

public sealed class BosMarketplaceWriteService : IBosMarketplaceWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosMarketplaceWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    public async Task<ErpSimpleWriteResult> UninstallAsync(
        long appId,
        string? siteKey,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_marketplace_installs` SET `status`='uninstalled' WHERE `app_id`=? AND `site_key`=?
                    """),
                cancellationToken, appId, PhpBosSiteKey(siteKey)).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Marketplace app uninstalled", appId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketplace table is missing — schema-ensure stays Classic.");
        }
    }
}
