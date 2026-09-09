using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_auto_price.php</c> twin of <c>epc_disc_source_save</c> (add),
/// <c>epc_disc_source_toggle</c>, and <c>epc_disc_source_delete</c>.
/// Auth password, login test, crawl, compare-run, and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpAutoPriceWriteService
{
    Task<ErpSimpleWriteResult> AddSourceAsync(
        CpAutoPriceSourceAddRequest request,
        CancellationToken cancellationToken = default);

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

public sealed record CpAutoPriceSourceAddRequest(
    long Id,
    string? Domain,
    string? Label,
    string? SiteKey,
    bool? Enabled,
    int? Priority,
    string? RequestHost);

public sealed class CpAutoPriceWriteService : ICpAutoPriceWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpAutoPriceWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    private static readonly Regex HttpScheme = new("^https?://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LeadingWww = new("^www\\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NotHostChars = new("[^a-z0-9.\\-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string NormalizeSiteKey(string? siteKey)
    {
        var raw = (siteKey ?? string.Empty).Trim().ToLowerInvariant();
        return SiteKeySafe.Replace(raw, string.Empty);
    }

    /// <summary>PHP <c>epc_disc_source_normalize_domain</c>.</summary>
    public static string NormalizeDomain(string? input)
    {
        var domain = (input ?? string.Empty).Trim();
        if (domain.Length == 0)
        {
            return string.Empty;
        }

        if (HttpScheme.IsMatch(domain))
        {
            if (!Uri.TryCreate(domain, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            {
                return string.Empty;
            }

            return LeadingWww.Replace(uri.Host.ToLowerInvariant(), string.Empty);
        }

        domain = LeadingWww.Replace(domain, string.Empty);
        var slash = domain.IndexOf('/');
        if (slash >= 0)
        {
            domain = domain[..slash];
        }

        return domain.ToLowerInvariant().TrimEnd('/');
    }

    /// <summary>PHP <c>epc_apai_normalize_domain</c> used by the own-storefront refuse.</summary>
    public static string NormalizeOwnDomain(string? input)
    {
        var domain = (input ?? string.Empty).Trim().ToLowerInvariant();
        domain = HttpScheme.Replace(domain, string.Empty);
        var slash = domain.IndexOf('/');
        if (slash >= 0)
        {
            domain = domain[..slash];
        }

        domain = LeadingWww.Replace(domain, string.Empty);
        return NotHostChars.Replace(domain, string.Empty);
    }

    /// <summary>PHP <c>epc_apai_is_tenant_own_domain</c> compact twin (locked hosts + request host).</summary>
    public static bool IsOwnStorefrontDomain(string? domain, string? requestHost)
    {
        var bare = NormalizeOwnDomain(domain);
        if (bare.Length == 0)
        {
            return false;
        }

        foreach (var own in OwnStorefrontHosts(requestHost))
        {
            var ownBare = NormalizeOwnDomain(own);
            if (ownBare.Length == 0)
            {
                continue;
            }

            if (bare == ownBare || bare == "www." + ownBare)
            {
                return true;
            }

            if (bare.Length > ownBare.Length && bare.EndsWith("." + ownBare, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static int NextEnabled(long current, bool? requested)
        => requested is null ? (current == 0 ? 1 : 0) : (requested.Value ? 1 : 0);

    public static int AddEnabled(bool? requested) => requested is false ? 0 : 1;

    public static int AddPriority(int? requested) => requested ?? 100;

    private static IEnumerable<string> OwnStorefrontHosts(string? requestHost)
    {
        yield return "epartscart.com";
        yield return "ecomae.com";
        yield return "localhost";
        yield return "127.0.0.1";
        foreach (var host in LiveTenantPresentationLock.AllHosts)
        {
            yield return host;
        }

        if (!string.IsNullOrWhiteSpace(requestHost))
        {
            yield return requestHost.Trim();
        }
    }

    public async Task<ErpSimpleWriteResult> AddSourceAsync(
        CpAutoPriceSourceAddRequest request,
        CancellationToken cancellationToken = default)
    {
        var domain = NormalizeDomain(request.Domain);
        if (domain.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Domain or URL is required");
        }

        if (IsOwnStorefrontDomain(domain, request.RequestHost))
        {
            return ErpSimpleWriteResult.Fail(
                "forbidden",
                "Your own storefront domain cannot be used as an external discovery source");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var siteKey = NormalizeSiteKey(request.SiteKey);
        var label = (request.Label ?? string.Empty).Trim();
        if (label.Length == 0)
        {
            label = domain;
        }

        var enabled = AddEnabled(request.Enabled);
        var priority = AddPriority(request.Priority);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_discovery_sources` WHERE `id`=?"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Discovery source not found");
                }

                if (siteKey.Length > 0)
                {
                    var rowSite = (await ErpDb.StringAsync(
                        connection, null,
                        ErpDb.Positional("SELECT `site_key` FROM `epc_discovery_sources` WHERE `id`=?"),
                        cancellationToken, request.Id) ?? string.Empty).Trim();
                    if (!string.Equals(rowSite, siteKey, StringComparison.Ordinal))
                    {
                        return ErpSimpleWriteResult.Fail("not_found", "Discovery source not found");
                    }
                }

                var custom = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `created_by_tenant` FROM `epc_discovery_sources` WHERE `id`=?"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (custom <= 0)
                {
                    return ErpSimpleWriteResult.Fail("forbidden", "Country pack sources cannot be edited");
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        "UPDATE `epc_discovery_sources` SET `site_key`=?, `source_type`=?, `domain`=?, `label`=?, `enabled`=?, `priority`=?, `created_by_tenant`=?, `updated_at`=? WHERE `id`=?"),
                    cancellationToken, siteKey, "custom_website", domain, label, enabled, priority, 1, now, request.Id)
                    .ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Custom source saved", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    "INSERT INTO `epc_discovery_sources` (`site_key`, `source_type`, `domain`, `label`, `enabled`, `priority`, `created_by_tenant`, `taxonomy_node_id`, `product_line_slug`, `auth_type`, `auth_username`, `auth_password`, `config_json`, `updated_at`, `created_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                siteKey, "custom_website", domain, label, enabled, priority, 1, 0, "", "none", "", "", null, now, now)
                .ConfigureAwait(false);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Custom source saved", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Discovery-source table is missing — schema-ensure stays Classic.");
        }
    }

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
