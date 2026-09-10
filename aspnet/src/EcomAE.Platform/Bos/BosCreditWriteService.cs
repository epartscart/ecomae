using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>credit_limit</c> <c>hold</c> / <c>epc_credit_hold</c>
/// and <c>release</c> / <c>epc_credit_release</c>.
/// Set-limit already lives on CP. Check-order and schema-ensure stay Classic.
/// This service does not invent a send. It does not emit CREATE/ALTER.
/// </summary>
public interface IBosCreditWriteService
{
    Task<ErpSimpleWriteResult> HoldAsync(
        string? siteKey,
        long customerId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ReleaseAsync(
        string? siteKey,
        long customerId,
        CancellationToken cancellationToken = default);
}

public sealed class BosCreditWriteService : IBosCreditWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosCreditWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', strtolower($site_key))</c>.</summary>
    public static string NormalizeSiteKey(string? siteKey)
        => SiteKeySafe.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public async Task<ErpSimpleWriteResult> HoldAsync(
        string? siteKey,
        long customerId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
        var holdReason = reason ?? string.Empty;
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
                    UPDATE `epc_credit_limits` SET `status` = 'on_hold', `hold_reason` = ?
                    WHERE `site_key` = ? AND `customer_id` = ?
                    """),
                cancellationToken, holdReason, key, customerId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Credit hold applied", customerId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Credit limits table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> ReleaseAsync(
        string? siteKey,
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
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
                    UPDATE `epc_credit_limits` SET `status` = 'active', `hold_reason` = ''
                    WHERE `site_key` = ? AND `customer_id` = ?
                    """),
                cancellationToken, key, customerId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Credit hold released", customerId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Credit limits table is missing — schema-ensure stays Classic.");
        }
    }
}
