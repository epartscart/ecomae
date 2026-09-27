using System.Data.Common;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live twin of PHP <c>ajax_portal.php?action=save_settings</c> → <c>epc_portal_save_site_settings()</c>:
/// the Industry-settings save behind <c>/cp/control/portal/industry_settings</c>.
/// </summary>
public interface ICpIndustrySettingsWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpIndustrySettingsSaveRequest request,
        string? host = null,
        CancellationToken cancellationToken = default);
}

public sealed record CpIndustrySettingsSaveRequest(
    string? IndustryCode,
    string? ThemeTemplate,
    string? StorefrontLayout,
    string? AccessMode,
    string? CpDefaultLang,
    string? CountryCode,
    string? SystemName,
    string? HubName,
    string? Tagline,
    string? DomainPath,
    string? ContactTradeName,
    string? ContactFromEmail,
    string? ContactAdminEmail,
    string? ContactPhone,
    string? ContactHeadOfficeAddress,
    string? ContactCity,
    string? ContactCountry,
    IReadOnlyList<string> EnabledPacks,
    IReadOnlyList<string> ErpModules,
    IReadOnlyList<int> HiddenGroups,
    IReadOnlyList<int> HiddenItems);

public sealed class CpIndustrySettingsWriteService : ICpIndustrySettingsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpIndustrySettingsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>substr(trim($v), 0, $max)</c>.</summary>
    public static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }

    /// <summary>PHP forces <c>core</c> in, and drops <c>super_platform</c> on tenant hosts.</summary>
    public static IReadOnlyList<string> NormalizePacks(IReadOnlyList<string> raw, bool isClientHost)
    {
        var known = PortalSettingsCatalog.Packs.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        var packs = raw
            .Select(p => CpIndustrySettingsService.Slug(p, string.Empty))
            .Where(p => p.Length > 0 && known.Contains(p))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (isClientHost)
        {
            packs.RemoveAll(p => p == "super_platform");
        }

        if (!packs.Contains("core", StringComparer.Ordinal))
        {
            packs.Insert(0, "core");
        }

        return packs;
    }

    /// <summary>PHP <c>epc_portal_erp_modules_normalize_list()</c> with the access-mode default fallback.</summary>
    public static IReadOnlyList<string> NormalizeModules(IReadOnlyList<string> raw, string accessMode)
    {
        var known = PortalSettingsCatalog.ErpModules.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var ids = raw
            .Select(m => CpIndustrySettingsService.Slug(m, string.Empty))
            .Where(known.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return ids.Count > 0 ? ids : PortalSettingsCatalog.ErpModuleDefaults(accessMode);
    }

    /// <summary>PHP <c>strtoupper(substr(preg_replace('/[^A-Za-z]/', '', $code), 0, 2))</c>, default AE.</summary>
    public static string CountryCode(string? raw)
    {
        var letters = new string((raw ?? string.Empty).Where(char.IsLetter).ToArray()).ToUpperInvariant();
        return letters.Length >= 2 ? letters[..2] : "AE";
    }

    public static JsonObject BuildContact(CpIndustrySettingsSaveRequest request, string countryCode, string layout)
    {
        var tradeName = Clip(request.ContactTradeName, 120);
        var fromEmail = Clip(request.ContactFromEmail, 160);
        var phone = Clip(request.ContactPhone, 64);
        return new JsonObject
        {
            ["trade_name"] = tradeName,
            ["from_name"] = tradeName,
            ["from_email"] = fromEmail,
            ["admin_email"] = Clip(request.ContactAdminEmail, 160),
            ["contact_phone"] = phone,
            ["whatsapp_number"] = phone,
            ["head_office_address"] = Clip(request.ContactHeadOfficeAddress, 255),
            ["head_office_email"] = fromEmail,
            ["city"] = Clip(request.ContactCity, 120),
            ["country"] = Clip(request.ContactCountry, 120),
            ["country_code"] = countryCode,
            ["storefront_layout"] = layout,
        };
    }

    public static JsonObject BuildTheme(string industryCode, string themeTemplate)
    {
        var industry = PortalSettingsCatalog.Industry(industryCode);
        var style = industry?.StyleTemplates.FirstOrDefault(t => t.Id == themeTemplate);
        if (style is null)
        {
            return [];
        }

        return new JsonObject
        {
            ["primary"] = style.Theme.Primary,
            ["primary_dark"] = style.Theme.PrimaryDark,
            ["accent"] = style.Theme.Accent,
            ["sidebar_from"] = style.Theme.SidebarFrom,
            ["sidebar_to"] = style.Theme.SidebarTo,
            ["hero_from"] = style.Theme.HeroFrom,
            ["hero_to"] = style.Theme.HeroTo,
        };
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpIndustrySettingsSaveRequest request,
        string? host = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var hostName = (host ?? string.Empty).Trim().ToLowerInvariant();
        if (hostName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("host", "Host is required to save site settings.");
        }

        var isClientHost = !PlatformHostPolicy.IsSuperCpHost(hostName);
        var industry = CpIndustrySettingsService.Slug(request.IndustryCode, "auto_parts");
        if (isClientHost && industry == "platform_host")
        {
            industry = "auto_parts";
        }

        if (PortalSettingsCatalog.Industry(industry) is null)
        {
            return ErpSimpleWriteResult.Fail("industry", "Unknown industry code: " + industry);
        }

        var accessMode = CpIndustrySettingsService.AccessMode(request.AccessMode);
        var themeTemplate = PortalSettingsCatalog.NormalizeThemeTemplate(industry, request.ThemeTemplate);
        var layout = CpIndustrySettingsService.Slug(request.StorefrontLayout, string.Empty);
        var countryCode = CountryCode(request.CountryCode);
        var packs = NormalizePacks(request.EnabledPacks, isClientHost);
        var modules = NormalizeModules(request.ErpModules, accessMode);
        var contact = BuildContact(request, countryCode, layout);
        var theme = BuildTheme(industry, themeTemplate);
        var menu = new JsonObject
        {
            ["hidden_groups"] = new JsonArray([.. request.HiddenGroups.Distinct().Select(id => JsonValue.Create(id))]),
            ["hidden_items"] = new JsonArray([.. request.HiddenItems.Distinct().Select(id => JsonValue.Create(id))]),
        };

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var writes = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_portal_site_settings` "
                    + "(`host`, `industry_code`, `theme_template`, `access_mode`, `erp_modules_json`, `cp_default_lang`, "
                    + "`country_code`, `system_name`, `hub_name`, `tagline`, `domain_path`, `contact_json`, "
                    + "`enabled_packs_json`, `theme_json`, `cp_menu_json`, `updated_at`) "
                    + "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) "
                    + "ON DUPLICATE KEY UPDATE `industry_code` = VALUES(`industry_code`), "
                    + "`theme_template` = VALUES(`theme_template`), `access_mode` = VALUES(`access_mode`), "
                    + "`erp_modules_json` = VALUES(`erp_modules_json`), `cp_default_lang` = VALUES(`cp_default_lang`), "
                    + "`country_code` = VALUES(`country_code`), `system_name` = VALUES(`system_name`), "
                    + "`hub_name` = VALUES(`hub_name`), `tagline` = VALUES(`tagline`), "
                    + "`domain_path` = VALUES(`domain_path`), `contact_json` = VALUES(`contact_json`), "
                    + "`enabled_packs_json` = VALUES(`enabled_packs_json`), `theme_json` = VALUES(`theme_json`), "
                    + "`cp_menu_json` = VALUES(`cp_menu_json`), `updated_at` = VALUES(`updated_at`)"),
                cancellationToken,
                hostName,
                industry,
                themeTemplate,
                accessMode,
                new JsonArray([.. modules.Select(m => JsonValue.Create(m))]).ToJsonString(),
                Clip(CpIndustrySettingsService.Lang(request.CpDefaultLang), 8),
                countryCode,
                Clip(request.SystemName, 120),
                Clip(request.HubName, 120),
                Clip(request.Tagline, 255),
                Clip(request.DomainPath, 255),
                contact.ToJsonString(),
                new JsonArray([.. packs.Select(p => JsonValue.Create(p))]).ToJsonString(),
                theme.ToJsonString(),
                menu.ToJsonString(),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

            return writes > 0
                ? ErpSimpleWriteResult.Ok("Settings saved.", 0)
                : ErpSimpleWriteResult.Fail("unchanged", "Settings were not updated.");
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", "Settings save failed: " + ex.Message);
        }
    }
}
