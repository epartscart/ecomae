using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP twin contract for cp/content/control/portal/industry_settings.php and its
/// ajax_portal.php?action=save_settings write path.
/// </summary>
public sealed class CpIndustrySettingsPhpParityTests
{
    [Fact]
    public void IndustrySettingsPhpRoutes_OpenTheDedicatedPage()
    {
        Assert.Equal(
            "/cp/industry-settings-app",
            PhpSurfaceLinkMap.MapCpPhpPath("/CP/control/portal/industry_settings"));
        Assert.Equal(
            "/cp/industry-settings-app",
            PhpSurfaceLinkMap.MapCpPhpPath("/CP/industry_settings"));
    }

    [Fact]
    public void IndustryPackDigestRoutes_StayOnThePacksPage()
    {
        Assert.Equal(
            "/cp/industry-packs-app",
            PhpSurfaceLinkMap.MapCpPhpPath("/CP/control/portal/epc_industry_packs"));
        Assert.Equal(
            "/cp/industry-packs-app",
            PhpSurfaceLinkMap.MapCpPhpPath("/CP/control/portal/industry_consolidation"));
    }

    [Fact]
    public void Page_PostsTheWholePhpSaveSettingsForm()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpIndustrySettingsApp.razor"));

        Assert.Contains("@page \"/cp/industry-settings-app\"", text, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/industry-settings/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);

        foreach (var field in new[]
        {
            "industry_code", "theme_template", "storefront_layout", "access_mode",
            "cp_default_lang", "country_code", "system_name", "hub_name", "tagline",
            "domain_path", "contact_trade_name", "contact_from_email", "contact_admin_email",
            "contact_phone", "contact_head_office_address", "contact_city", "contact_country",
            "enabled_packs", "erp_modules", "visible_groups", "visible_items",
        })
        {
            Assert.Contains("name=\"" + field + "\"", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SaveEndpoint_IsCpAdminGatedAndHostScoped()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));

        Assert.Contains("EcomAeRoutes.CpIndustrySettingsWrite", text, StringComparison.Ordinal);
        Assert.Contains(
            "Admin CP capability required for industry-settings save.",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizePacks_ForcesCoreAndBlocksSuperPlatformOnTenantHosts()
    {
        var tenant = CpIndustrySettingsWriteService.NormalizePacks(
            ["Commerce", "super_platform", "bad code!"],
            isClientHost: true);
        Assert.Contains("core", tenant);
        Assert.Contains("commerce", tenant);
        Assert.DoesNotContain("super_platform", tenant);

        var super = CpIndustrySettingsWriteService.NormalizePacks(
            ["super_platform"],
            isClientHost: false);
        Assert.Contains("super_platform", super);
    }

    [Fact]
    public void NormalizeModules_FallsBackToPhpDefaultsWhenEmpty()
    {
        Assert.Equal(
            PortalSettingsCatalog.ErpModuleDefaults("erp_only"),
            CpIndustrySettingsWriteService.NormalizeModules([], "erp_only"));
        Assert.Equal(
            PortalSettingsCatalog.ErpModuleDefaults("full"),
            CpIndustrySettingsWriteService.NormalizeModules([], "full"));
    }

    [Fact]
    public void ThemeTemplateAliases_MatchPhpNormalisation()
    {
        Assert.Equal("classic", PortalSettingsCatalog.NormalizeThemeTemplate("auto_parts", "default"));
        Assert.Equal("classic", PortalSettingsCatalog.NormalizeThemeTemplate("auto_parts", "standard"));
        Assert.Equal("modern", PortalSettingsCatalog.NormalizeThemeTemplate("auto_parts", "light"));
        Assert.Equal("midnight", PortalSettingsCatalog.NormalizeThemeTemplate("auto_parts", "dark"));
    }

    [Fact]
    public void CpLanguages_MatchPhpTranslateRegistry()
    {
        Assert.Equal(27, PortalSettingsCatalog.CpLanguages.Count);
        Assert.Equal("en", PortalSettingsCatalog.CpLanguages[0].Code);
        Assert.Contains(PortalSettingsCatalog.CpLanguages, l => l.Code == "ar" && l.Label == "العربية");
        Assert.Contains(PortalSettingsCatalog.CpLanguages, l => l.Code == "zh-CN");
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
