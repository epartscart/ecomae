using EcomAE.Platform.Cp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// cp/content/shop/pricing (price_management.php + epc_pm_storage_panel.php + content/shop/pricing/epc_pricing.php)
/// twin: /cp/price-management-app must keep the PHP actions, margin stack, section layout, guides and DB semantics.
/// </summary>
public sealed class CpPriceManagementPhpParityTests
{
    [Fact]
    public void ServiceActions_MatchPhpPostActions()
    {
        var php = File.ReadAllText(FindRepoFile("cp/content/shop/pricing/price_management.php"));
        var storage = File.ReadAllText(FindRepoFile("cp/content/shop/pricing/epc_pm_storage_panel.php"));

        foreach (var action in CpPriceManagementService.ProfileActions)
        {
            Assert.Contains("'" + action + "'", php, StringComparison.Ordinal);
        }

        foreach (var action in CpPriceManagementService.StorageActions)
        {
            Assert.Contains("'" + action + "'", storage, StringComparison.Ordinal);
        }

        Assert.Equal(11, CpPriceManagementService.ProfileActions.Length);
        Assert.Equal(6, CpPriceManagementService.StorageActions.Length);
    }

    [Fact]
    public void Endpoint_DispatchesAllActions_KeepsCpAdminGate_AndBrandsArray()
    {
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        var start = module.IndexOf("MapPost(EcomAeRoutes.CpPriceManagementAction", StringComparison.Ordinal);
        Assert.True(start > 0);
        var endpoint = module[start..module.IndexOf("MapPost(EcomAeRoutes.CpPriceStorageRules", start, StringComparison.Ordinal)];

        Assert.Equal("/cp/price-management/action", EcomAeRoutes.CpPriceManagementAction);
        Assert.Equal("/cp/price-management-app", EcomAeRoutes.ControlPanelPriceManagementApp);
        Assert.Contains("session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains(\"cp\")", endpoint, StringComparison.Ordinal);
        Assert.Contains("pair.Key is \"brands[]\" or \"brands\"", endpoint, StringComparison.Ordinal);
        Assert.Contains("pricing.ApplyAsync(action, fields, brands", endpoint, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", endpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_KeepsPhpSections_Forms_GuidesAndStyle()
    {
        var razor = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPriceManagementApp.razor"));
        Assert.Contains("@page \"/cp/price-management-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("@layout Layout.PhpChromeLayout", razor, StringComparison.Ordinal);
        Assert.Contains("<PhpChromeStyles Surface=\"cp\" />", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);

        // hero + stats + flow + sections (price_management.php)
        Assert.Contains("epc-pm-hero", razor, StringComparison.Ordinal);
        Assert.Contains("linear-gradient(135deg, #0c4a6e 0%, #0369a1 50%, #0ea5e9 100%)", razor, StringComparison.Ordinal);
        Assert.Contains("<h1>Price Management</h1>", razor, StringComparison.Ordinal);
        foreach (var id in new[] { "epc-pm-step-wh", "epc-pm-step1", "epc-pm-step2", "epc-pm-step3", "epc-pm-step4", "epc-pm-step5", "epc-pm-step6", "epc-pm-step7" })
        {
            Assert.Contains("id=\"" + id + "\"", razor, StringComparison.Ordinal);
        }

        Assert.Contains("How margins stack (top → bottom):", razor, StringComparison.Ordinal);
        Assert.Contains("VAT policy (no double tax):", razor, StringComparison.Ordinal);
        Assert.Contains("Customer price profiles", razor, StringComparison.Ordinal);
        Assert.Contains("Guest margin &amp; default VAT", razor, StringComparison.Ordinal);
        Assert.Contains("Assign customer to profile", razor, StringComparison.Ordinal);
        Assert.Contains("Brand-level rules", razor, StringComparison.Ordinal);
        Assert.Contains("Article-level rules (most specific)", razor, StringComparison.Ordinal);
        Assert.Contains("Live price calculator", razor, StringComparison.Ordinal);
        Assert.Contains("Quick scenarios (base 100.00)", razor, StringComparison.Ordinal);
        Assert.Contains("Verify on the storefront", razor, StringComparison.Ordinal);
        Assert.Contains("Advanced: bulk brand visibility", razor, StringComparison.Ordinal);

        // storage panel (epc_pm_storage_panel.php)
        Assert.Contains("Warehouse / supplier pricing", razor, StringComparison.Ordinal);
        Assert.Contains("data-wh-tab=\"overall\"", razor, StringComparison.Ordinal);
        Assert.Contains("data-wh-tab=\"brand\"", razor, StringComparison.Ordinal);
        Assert.Contains("data-wh-tab=\"article\"", razor, StringComparison.Ordinal);

        // every PHP action posted from the page
        foreach (var action in CpPriceManagementService.ProfileActions.Concat(CpPriceManagementService.StorageActions))
        {
            Assert.Contains("@Hidden(\"" + action + "\")", razor, StringComparison.Ordinal);
        }

        // PHP field names
        foreach (var field in new[] { "profile_margin_percent", "profile_vat_percent", "guest_margin_percent", "vat_percent", "profile_name", "profile_code", "user_id", "group_id", "manufacturer", "article", "margin_percent", "visible", "rule_id", "storage_id", "brands[]", "base_price" })
        {
            Assert.Contains("name=\"" + field + "\"", razor, StringComparison.Ordinal);
        }

        Assert.Contains("epc_brand_visibility_filter", razor, StringComparison.Ordinal);
        Assert.Contains("epcFilterBrandVisibilityList", razor, StringComparison.Ordinal);
        Assert.Contains("href=\"/cp/guideline-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("href=\"/cp/prices-upload-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("href=\"/cp/prices-edit-app\"", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_UsesPhpTables_ParameterizedSql_AndLimits()
    {
        var svc = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpPriceManagementService.cs"));
        foreach (var table in new[]
        {
            "epc_price_settings", "epc_price_profiles", "epc_price_profile_brand_rules", "epc_price_profile_article_rules",
            "epc_price_storage_rules", "epc_price_storage_brand_rules", "epc_price_storage_article_rules",
            "lang_text_strings", "lang_text_strings_translation", "groups", "users_groups_bind", "users_profiles", "shop_storages"
        })
        {
            Assert.Contains("`" + table + "`", svc, StringComparison.Ordinal);
        }

        Assert.Contains("EPC_PROFILE_", svc, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM `users_groups_bind`", svc, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", svc, StringComparison.Ordinal);
        Assert.Equal(300, CpPriceManagementService.CustomersLimit);
        Assert.Equal("5.00", CpPriceManagementService.DefaultVat);
        Assert.Equal("0.00", CpPriceManagementService.DefaultGuestMargin);
        Assert.Equal("fleet_sales", CpPriceManagementService.ProfileCode(" Fleet Sales! "));
    }

    [Fact]
    public void Engine_ExposesPhpBreakdownStack_AndHiddenReasons()
    {
        var engine = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/EpcPricing.cs"));
        var order = new[] { "\"storage\"", "\"storage_brand\"", "\"storage_article\"", "\"profile\"", "\"brand\"", "\"article\"", "\"guest\"" };
        var last = -1;
        foreach (var step in order)
        {
            var idx = engine.IndexOf("Step(" + step, StringComparison.Ordinal);
            Assert.True(idx > last, step);
            last = idx;
        }

        foreach (var reason in new[] { "Supplier / warehouse hidden", "Brand hidden for this supplier", "Article hidden for this supplier", "Brand hidden for this profile", "Article hidden for this profile" })
        {
            Assert.Contains(reason, engine, StringComparison.Ordinal);
        }

        var result = new EpcPricing.PriceRulesResult(true, "", 100m, 140m, 0m, []);
        Assert.Equal(40m, result.TotalMarginPercent);
    }

    [Fact]
    public void Navigation_AndPhpSurfaceMap_PointToPriceManagement()
    {
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, l => l.Href == "/cp/price-management-app");
        // PHP shop/pricing keeps its /cp/price-lists-app surface; that page must hand off to the twin.
        var lists = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPriceListsApp.razor"));
        Assert.Contains("href=\"/cp/price-management-app\"", lists, StringComparison.Ordinal);
        Assert.Equal("/cp/price-lists-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/pricing"));
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
