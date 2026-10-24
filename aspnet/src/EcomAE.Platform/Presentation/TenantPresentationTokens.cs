using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Tenant personalization layer (D365-style): every allowlisted
/// <c>epc_settings</c> token saved via CP "Organization presentation settings"
/// is resolved per tenant site_key and emitted into the chrome as scoped CSS
/// variables + data attributes. The super layer (base markup, routes,
/// workflow semantics) stays untouched — tenants get their own look, density,
/// labels and login surface without forking code.
/// </summary>
public sealed record TenantPresentation(
    string SiteKey,
    IReadOnlyDictionary<string, string> Values)
{
    public static readonly TenantPresentation Empty = new(string.Empty, new Dictionary<string, string>());

    public string? Get(string key)
        => Values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    public bool Flag(string key)
    {
        var v = Get(key);
        return v is not null && v is not "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>CSS variable overrides + attribute-driven rules, scoped to the tenant class.</summary>
    public string BuildCss()
    {
        var sb = new StringBuilder();
        var vars = new StringBuilder();

        MapColor(vars, "brand_primary", "--epc-erp-primary", "--epc-blue-600", "--ep-accent");
        MapColor(vars, "brand_secondary", "--epc-blue-700");
        MapColor(vars, "brand_accent", "--epc-blue-400", "--epc-sidebar-hover");
        MapColor(vars, "brand_bg_dark", "--epc-blue-950", "--epc-bg-dark");
        MapColor(vars, "brand_sidebar_bg", "--epc-sidebar-from", "--epc-sidebar-to");
        MapColor(vars, "brand_header_bg", "--epc-header-bg");
        MapColor(vars, "brand_login_bg", "--epc-login-bg");
        MapColor(vars, "ui_accent", "--epc-blue-500", "--epc-accent");

        var radius = Get("brand_radius") ?? Get("ui_radius");
        if (IsSafeLength(radius))
        {
            vars.Append("--epc-radius:").Append(radius).Append(';');
            vars.Append("--epc-panel-radius:").Append(radius).Append(';');
        }

        var font = Get("brand_font");
        if (IsSafeFont(font))
        {
            vars.Append("--epc-font:").Append(font).Append(';');
        }

        if (vars.Length > 0)
        {
            sb.Append(".epc-tenant-tokens,.epc-tenant-tokens .epc-erp-shell,.epc-tenant-tokens .epc-cp-shell{")
              .Append(vars)
              .AppendLine("}");
        }

        var density = Get("ui_density");
        if (density is "compact" or "comfortable")
        {
            var size = density == "compact" ? "12px" : "13.5px";
            var pad = density == "compact" ? "4px" : "8px";
            sb.AppendLine($".epc-tenant-tokens[data-epc-density='{density}'] .epc-erp-shell,.epc-tenant-tokens[data-epc-density='{density}'] .epc-cp-shell{{font-size:{size}}}");
            sb.AppendLine($".epc-tenant-tokens[data-epc-density='{density}'] .epc-erp-topnav-links a,.epc-tenant-tokens[data-epc-density='{density}'] .epc-cp-topnav-links a{{padding-top:{pad};padding-bottom:{pad}}}");
        }

        var navMode = Get("ui_nav_label_mode");
        if (navMode is "short" or "full")
        {
            sb.AppendLine($".epc-tenant-tokens[data-epc-nav-labels='{navMode}'] .epc-erp-topnav-label,.epc-tenant-tokens[data-epc-nav-labels='{navMode}'] .epc-cp-topnav-label{{}}");
            if (navMode == "short")
            {
                sb.AppendLine(".epc-tenant-tokens[data-epc-nav-labels='short'] .epc-erp-topnav-label,.epc-tenant-tokens[data-epc-nav-labels='short'] .epc-cp-topnav-label{font-size:0;}");
                sb.AppendLine(".epc-tenant-tokens[data-epc-nav-labels='short'] .epc-erp-topnav-label::after,.epc-tenant-tokens[data-epc-nav-labels='short'] .epc-cp-topnav-label::after{content:attr(data-short);font-size:13px;}");
            }
        }

        if (Get("ui_show_help") is "0" or "false")
        {
            sb.AppendLine(".epc-tenant-tokens .epc-help,.epc-tenant-tokens .help-block,.epc-tenant-tokens .epc-erp-help{display:none!important}");
        }

        var formLayout = Get("ui_form_layout");
        if (formLayout is "compact")
        {
            sb.AppendLine(".epc-tenant-tokens[data-epc-form-layout='compact'] .form-group,.epc-tenant-tokens[data-epc-form-layout='compact'] .epc-erp-field{margin-bottom:.35rem}");
        }
        else if (formLayout is "stacked")
        {
            sb.AppendLine(".epc-tenant-tokens[data-epc-form-layout='stacked'] .form-inline .form-group,.epc-tenant-tokens[data-epc-form-layout='stacked'] .epc-erp-form-row{display:block;width:100%}");
        }

        if (Get("ui_show_status_legend") is "0" or "false")
        {
            sb.AppendLine(".epc-tenant-tokens .epc-status-legend,.epc-tenant-tokens .epc-dash-legend{display:none!important}");
        }

        var logo = Get("brand_logo_url");
        if (logo is not null && IsSafeUrl(logo))
        {
            sb.AppendLine(".epc-tenant-tokens .epc-erp-topnav-brand img,.epc-tenant-tokens .epc-cp-topnav-brand img{content:url('").Append(EscapeCssString(logo)).AppendLine("')}");
        }

        return sb.ToString();
    }

    /// <summary>Extra <c>data-epc-*</c> attributes for splatting onto the chrome root.</summary>
    public IReadOnlyDictionary<string, object> Attributes()
    {
        var attrs = new Dictionary<string, object>(StringComparer.Ordinal);
        var density = Get("ui_density");
        if (density is "compact" or "comfortable")
        {
            attrs["data-epc-density"] = density;
        }

        var layout = Get("ui_form_layout");
        if (layout is "d365" or "split" or "stacked" or "compact")
        {
            attrs["data-epc-form-layout"] = layout;
        }

        var navMode = Get("ui_nav_label_mode");
        if (navMode is "short" or "full")
        {
            attrs["data-epc-nav-labels"] = navMode;
        }

        var dash = Get("ui_dashboard_style");
        if (dash is "executive" or "classic" or "compact")
        {
            attrs["data-epc-dash-style"] = dash;
        }

        return attrs;
    }

    private void MapColor(StringBuilder vars, string key, params string[] cssVars)
    {
        var value = Get(key);
        if (!IsSafeColor(value))
        {
            return;
        }

        foreach (var cssVar in cssVars)
        {
            vars.Append(cssVar).Append(':').Append(value).Append(';');
        }
    }

    private static bool IsSafeColor(string? value)
        => !string.IsNullOrWhiteSpace(value) && ColorPattern.IsMatch(value.Trim());

    private static bool IsSafeLength(string? value)
        => !string.IsNullOrWhiteSpace(value) && LengthPattern.IsMatch(value.Trim());

    private static bool IsSafeFont(string? value)
        => !string.IsNullOrWhiteSpace(value) && FontPattern.IsMatch(value.Trim());

    private static bool IsSafeUrl(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || value.StartsWith('/'));

    private static string EscapeCssString(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private static readonly Regex ColorPattern = new(
        @"^(#[0-9a-fA-F]{3,8}|rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}(\s*,\s*(0|1|0?\.\d+))?\s*\)|hsla?\(\s*\d{1,3}\s*,\s*\d{1,3}%\s*,\s*\d{1,3}%(\s*,\s*(0|1|0?\.\d+))?\s*\)|[a-zA-Z]{3,24})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LengthPattern = new(
        @"^\d{1,3}(\.\d{1,2})?(px|em|rem|%)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FontPattern = new(
        @"^[a-zA-Z0-9\s\-',\.]{2,80}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}

public interface ITenantPresentationProvider
{
    Task<TenantPresentation> GetAsync(HttpContext context, CancellationToken cancellationToken = default);
}

public sealed class TenantPresentationProvider : ITenantPresentationProvider
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IMemoryCache _cache;

    public TenantPresentationProvider(IErpWriteConnectionFactory connections, IMemoryCache cache)
    {
        _connections = connections;
        _cache = cache;
    }

    public async Task<TenantPresentation> GetAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return TenantPresentation.Empty;
        }

        var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var siteKey = tenant?.SiteKey;
        if (string.IsNullOrWhiteSpace(siteKey))
        {
            siteKey = "ecomae";
        }

        siteKey = siteKey.Trim().ToLowerInvariant();
        var cacheKey = "tenant-presentation:" + siteKey;
        if (_cache.TryGetValue(cacheKey, out TenantPresentation? cached) && cached is not null)
        {
            return cached;
        }

        var tokens = await LoadAsync(siteKey, cancellationToken).ConfigureAwait(false);
        _cache.Set(cacheKey, tokens, CacheLifetime);
        return tokens;
    }

    private async Task<TenantPresentation> LoadAsync(string siteKey, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT `setting_key`, `setting_value` FROM `epc_settings` WHERE (`site_key` IS NULL OR `site_key` = '' OR `site_key` = ?) AND (`setting_key` LIKE 'brand_%' OR `setting_key` LIKE 'ui_%' OR `setting_key` = 'white_label_login') ORDER BY IFNULL(`site_key`,'') ASC";
            var parameter = command.CreateParameter();
            parameter.Value = siteKey;
            command.Parameters.Add(parameter);

            // Global (empty site_key) rows load first, tenant rows overwrite — tenant wins.
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
            return TenantPresentation.Empty;
        }

        return new TenantPresentation(siteKey, values);
    }
}
