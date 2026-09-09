using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_auto_price.php</c> twin of <c>epc_disc_source_toggle</c> and <c>epc_disc_source_delete</c>.
/// Add, crawl, compare-run, and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpAutoPriceWriteService
{
    Task<ErpSimpleWriteResult> ToggleSourceAsync(
        CpAutoPriceSourceToggleRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteSourceAsync(
        long id,
        string? siteKey,
        CancellationToken cancellationToken = default);
}

public sealed record CpAutoPriceSourceToggleRequest(
    long Id,
    string? SiteKey,
    bool? Enabled);

public sealed class CpAutoPriceWriteService : ICpAutoPriceWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpAutoPriceWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? siteKey)
    {
        var raw = (siteKey ?? string.Empty).Trim().ToLowerInvariant();
        return SiteKeySafe.Replace(raw, string.Empty);
    }

    public static int NextEnabled(long current, bool? requested)
        => requested is null ? (current == 0 ? 1 : 0) : (requested.Value ? 1 : 0);

    public async Task<ErpSimpleWriteResult> ToggleSourceAsync(
        CpAutoPriceSourceToggleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Source id required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var siteKey = NormalizeSiteKey(request.SiteKey);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var exists = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_discovery_sources` WHERE `id`=?"),
                cancellationToken, request.Id).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Source not found");
            }

            if (siteKey.Length > 0)
            {
                var rowSite = (await ErpDb.StringAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `site_key` FROM `epc_discovery_sources` WHERE `id`=?"),
                    cancellationToken, request.Id) ?? string.Empty).Trim();
                if (!string.Equals(rowSite, siteKey, StringComparison.Ordinal))
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Source not found");
                }
            }

            var current = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `enabled` FROM `epc_discovery_sources` WHERE `id`=?"),
                cancellationToken, request.Id).ConfigureAwait(false);
            var next = NextEnabled(current, request.Enabled);

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_discovery_sources` SET `enabled`=?, `updated_at`=? WHERE `id`=?"),
                cancellationToken, next, now, request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Source updated", request.Id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Discovery-source table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteSourceAsync(
        long id,
        string? siteKey,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Source id required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var wantSite = NormalizeSiteKey(siteKey);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var exists = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_discovery_sources` WHERE `id`=?"),
                cancellationToken, id).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Only custom tenant sources can be deleted");
            }

            if (wantSite.Length > 0)
            {
                var rowSite = (await ErpDb.StringAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `site_key` FROM `epc_discovery_sources` WHERE `id`=?"),
                    cancellationToken, id) ?? string.Empty).Trim();
                if (!string.Equals(rowSite, wantSite, StringComparison.Ordinal))
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Only custom tenant sources can be deleted");
                }
            }

            var custom = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `created_by_tenant` FROM `epc_discovery_sources` WHERE `id`=?"),
                cancellationToken, id).ConfigureAwait(false);
            if (custom <= 0)
            {
                return ErpSimpleWriteResult.Fail("forbidden", "Only custom tenant sources can be deleted");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_discovery_sources` WHERE `id`=?"),
                cancellationToken, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Custom source removed", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Discovery-source table is missing — schema-ensure stays Classic.");
        }
    }
}
