using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>industry_packs</c> <c>assign</c> / <c>epc_industry_assign_pack</c>.
/// Seed and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/site-key checks.
/// </summary>
public interface IBosIndustryWriteService
{
    Task<ErpSimpleWriteResult> AssignAsync(
        string? siteKey,
        string? packKey,
        long appliedBy,
        CancellationToken cancellationToken = default);
}

public sealed class BosIndustryWriteService : IBosIndustryWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosIndustryWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    public async Task<ErpSimpleWriteResult> AssignAsync(
        string? siteKey,
        string? packKey,
        long appliedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var key = PhpBosSiteKey(siteKey);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_tenant_pack_assignments` (`site_key`,`pack_key`,`applied_by`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `applied_at` = NOW()
                    """),
                cancellationToken, key, packKey ?? "", appliedBy).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Industry pack assigned", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Industry pack table is missing — schema-ensure stays Classic.");
        }
    }
}
