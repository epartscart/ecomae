using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Caching.Memory;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Storefront theme overrides (<c>epc_settings</c> keys <c>sf_*</c>) layered
/// global (site_key '') → industry (<c>industry:code</c>) → tenant (site_key), tenant wins.
/// Edited from CP "Organization presentation settings"; emitted as sanitized CSS variable
/// overrides by <c>PhpSurfaceHead</c> so a tenant/country palette needs no code change.
/// </summary>
public interface IStorefrontThemeTokens
{
    Task<IReadOnlyDictionary<string, string>> GetAsync(HttpContext context, CancellationToken cancellationToken = default);
}

public sealed class StorefrontThemeTokens : IStorefrontThemeTokens
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

    // sf_* key → CSS variable (auto247 theme file). Sanitized hex colors only.
    public static readonly IReadOnlyDictionary<string, string> CssVarMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sf_primary"] = "--auto247-blue",
            ["sf_primary_dark"] = "--auto247-blue-dark",
            ["sf_primary_deep"] = "--auto247-blue-deep",
            ["sf_primary_light"] = "--auto247-blue-light",
            ["sf_sky"] = "--auto247-blue-sky",
            ["sf_soft"] = "--auto247-blue-soft",
            ["sf_line"] = "--auto247-blue-line",
        };

    public static readonly Regex HexColor = new(
        @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly IReadOnlyList<string> ColorKeys = [.. CssVarMap.Keys, "sf_page_bg"];

    /// <summary>Sanitized <c>&lt;style&gt;</c> fragment with :root var overrides ("" when none).</summary>
    public static string StyleBlock(IReadOnlyDictionary<string, string> tokens, string bodyClassSuffix)
    {
        var vars = new List<string>();
        foreach (var (key, cssVar) in CssVarMap)
        {
            if (tokens.TryGetValue(key, out var value) && HexColor.IsMatch(value))
            {
                vars.Add(cssVar + ":" + value);
            }
        }

        var pageBg = "";
        if (tokens.TryGetValue("sf_page_bg", out var bg) && HexColor.IsMatch(bg))
        {
            pageBg = $"body.epc-storefront--auto247{{background:{bg} !important;}}";
        }

        if (vars.Count == 0 && pageBg.Length == 0)
        {
            return string.Empty;
        }

        var root = vars.Count > 0 ? ":root{" + string.Join(';', vars) + "}" : "";
        return $"<style id=\"epc-sf-theme-{bodyClassSuffix}\">{root}{pageBg}</style>";
    }

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IMemoryCache _cache;

    public StorefrontThemeTokens(IErpWriteConnectionFactory connections, IMemoryCache cache)
    {
        _connections = connections;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var siteKey = string.IsNullOrWhiteSpace(tenant?.SiteKey)
            ? "ecomae"
            : tenant!.SiteKey!.Trim().ToLowerInvariant();
        var industryKey = string.IsNullOrWhiteSpace(tenant?.IndustryCode)
            ? null
            : "industry:" + tenant!.IndustryCode!.Trim().ToLowerInvariant();
        var cacheKey = "sf-theme-tokens:" + (tenant?.DatabaseName ?? "registry") + ":" + (industryKey ?? "") + ":" + siteKey;
        if (_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
        {
            return cached;
        }

        var tokens = await LoadAsync(siteKey, industryKey, cancellationToken).ConfigureAwait(false);
        _cache.Set(cacheKey, tokens, CacheLifetime);
        return tokens;
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadAsync(
        string siteKey,
        string? industryKey,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT `setting_key`, `setting_value` FROM `epc_settings` WHERE (`site_key` IS NULL OR `site_key` = '' OR `site_key` = ? OR `site_key` = ?) AND `setting_key` LIKE 'sf\\_%' ORDER BY CASE WHEN `site_key` IS NULL OR `site_key` = '' THEN 0 WHEN `site_key` LIKE 'industry:%' THEN 1 ELSE 2 END ASC";
            var parameter = command.CreateParameter();
            parameter.Value = industryKey ?? siteKey;
            command.Parameters.Add(parameter);
            var parameter2 = command.CreateParameter();
            parameter2.Value = siteKey;
            command.Parameters.Add(parameter2);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                values[key.Trim()] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1) ?? string.Empty;
            }
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return values;
    }
}
