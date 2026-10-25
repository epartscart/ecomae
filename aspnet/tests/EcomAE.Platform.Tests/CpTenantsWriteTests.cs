using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpTenantsWriteTests
{
    [Fact]
    public void Route_exposes_tenants_write()
    {
        Assert.Equal("/cp/tenants/write", EcomAeRoutes.CpTenantsWrite);
    }

    [Fact]
    public void Site_key_and_message_match_php()
    {
        Assert.Equal("epartscart", CpTenantsWriteService.NormalizeSiteKey(" ePartsCart! "));
        Assert.Equal("", CpTenantsWriteService.NormalizeSiteKey(" !!! "));
        Assert.Equal("Tenant enabled", CpTenantsWriteService.ToggleMessage(true));
        Assert.Equal("Tenant disabled — storefront and CP blocked", CpTenantsWriteService.ToggleMessage(false));
    }

    [Fact]
    public void Page_posts_native_set_active()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpTenantsApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/tenants/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"tenant_set_active\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"onboard_erp_only\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"onboard_four_erp_only\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"delete_standalone_erp\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"reset_operator_password\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"repair_erp_only_shop\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"set_operator_password\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"add_operator_login\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"login_email\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirm_password\"", razor, StringComparison.Ordinal);
        Assert.Contains("Set my password", razor, StringComparison.Ordinal);
        Assert.Contains("Add extra login", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirm_site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"industry_code\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"fit_out\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"real_estate\"", razor, StringComparison.Ordinal);
        Assert.Contains("@ecomae.com", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpReferenceOnlyHref", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_set_active_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/tenants/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("onboard_erp_only", write.Notes, StringComparison.Ordinal);
        Assert.Contains("onboard_four_erp_only", write.Notes, StringComparison.Ordinal);
        Assert.Contains("delete_standalone_erp", write.Notes, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", write.Notes, StringComparison.Ordinal);
        Assert.Contains("repair_erp_only_shop", write.Notes, StringComparison.Ordinal);
        Assert.Contains("repair_four_erp_only", write.Notes, StringComparison.Ordinal);
        Assert.Contains("set_operator_password", write.Notes, StringComparison.Ordinal);
        Assert.Contains("add_operator_login", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Erp_only_guards_protect_commerce_keys()
    {
        Assert.True(CpTenantsWriteService.IsProtectedSiteKey("ePartsCart"));
        Assert.True(CpTenantsWriteService.IsProtectedSiteKey("taxofinca"));
        Assert.False(CpTenantsWriteService.IsProtectedSiteKey("asap"));
        Assert.False(CpTenantsWriteService.IsProtectedSiteKey("indj"));
        Assert.False(CpTenantsWriteService.IsProtectedSiteKey("asapcustom"));
        Assert.True(CpTenantsWriteService.IsAllowedIndustry("jewellery"));
        Assert.True(CpTenantsWriteService.IsAllowedIndustry("auto_parts"));
        Assert.True(CpTenantsWriteService.IsAllowedIndustry("fit_out"));
        Assert.True(CpTenantsWriteService.IsAllowedIndustry("real_estate"));
        Assert.False(CpTenantsWriteService.IsAllowedIndustry("platform_host"));
        Assert.Equal("jewellery", CpTenantsWriteService.NormalizeIndustry("jewellery"));
        Assert.Equal("fito@ecomae.com", CpTenantsWriteService.OperatorEmailFor("FITO"));
        Assert.Equal("real@ecomae.com", CpTenantsWriteService.OperatorEmailFor("real"));
        Assert.Equal(5, ErpOnlyLaunchClients.All.Count);
        Assert.Equal("yawer@ecomae.com", ErpOnlyLaunchClients.SuperMailbox);
        // asap offboarded — no longer a launch client; its DB is retained off-catalog.
        Assert.DoesNotContain(ErpOnlyLaunchClients.All, c => c.SiteKey == "asap");
        Assert.Contains(ErpOnlyLaunchClients.All, c => c.SiteKey == "sham" && c.Email == "sham@ecomae.com" && c.IndustryCode == "auto_parts");
        Assert.Contains(ErpOnlyLaunchClients.All, c => c.SiteKey == "indj");
        Assert.Contains(ErpOnlyLaunchClients.All, c => c.SiteKey == "fito");
        Assert.Contains(ErpOnlyLaunchClients.All, c => c.SiteKey == "real");
        Assert.Contains(ErpOnlyLaunchClients.All, c => c.SiteKey == "fmcg" && c.IndustryCode == "fmcg_wholesale");
        Assert.False(CpTenantsWriteService.IsDroppableDatabase("ecomae"));
        Assert.True(CpTenantsWriteService.IsDroppableDatabase("asap"));
    }

    [Fact]
    public void Program_and_module_set_active()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpTenantsWriteService", program, StringComparison.Ordinal);
        Assert.Contains("IErpOnlyShopBootstrap", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpTenantsWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = true", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpTenantsWriteService.cs"));
        Assert.Contains("epc_portal_tenant_control_set_active", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_portal_tenants`", service, StringComparison.Ordinal);
        Assert.Contains("OnboardErpOnlyAsync", service, StringComparison.Ordinal);
        Assert.Contains("DeleteStandaloneErpAsync", service, StringComparison.Ordinal);
        Assert.Contains("RepairErpOnlyShopAsync", service, StringComparison.Ordinal);
        Assert.Contains("onboard_erp_only", module, StringComparison.Ordinal);
        Assert.Contains("delete_standalone_erp", module, StringComparison.Ordinal);
        Assert.Contains("keep_database", module, StringComparison.Ordinal);
        Assert.Contains("KeepDatabase", service, StringComparison.Ordinal);
        Assert.Contains("database retained for backup requests", service, StringComparison.Ordinal);
        Assert.Contains("repair_erp_only_shop", module, StringComparison.Ordinal);
        Assert.Contains("repair_four_erp_only", module, StringComparison.Ordinal);
        Assert.Contains("set_operator_password", module, StringComparison.Ordinal);
        Assert.Contains("add_operator_login", module, StringComparison.Ordinal);
        Assert.Contains("SetOperatorPasswordAsync", service, StringComparison.Ordinal);
        Assert.Contains("AddOperatorLoginAsync", service, StringComparison.Ordinal);
        Assert.True(CpTenantsWriteService.IsUsableLoginEmail("indj.accounts@ecomae.com"));
        Assert.False(CpTenantsWriteService.IsUsableLoginEmail("not-an-email"));
        Assert.True(CpTenantsWriteService.IsUsableOperatorPassword("Abcdefg1"));
        Assert.False(CpTenantsWriteService.IsUsableOperatorPassword("short"));
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.Contains("EnsureShopAsync", service, StringComparison.Ordinal);
        var grant = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts/cloudpanel_grant_erp_only_shop_dbs.sh"));
        Assert.Contains("GRANT_ERP_ONLY_SHOP_DBS", grant, StringComparison.Ordinal);
        Assert.Contains("Does not remap TenantRegistry to PHP", grant, StringComparison.Ordinal);
        Assert.Contains("indj", grant, StringComparison.Ordinal);
        var onboard = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts/cloudpanel_onboard_four_erp_only.sh"));
        Assert.Contains("cloudpanel_grant_erp_only_shop_dbs.sh", onboard, StringComparison.Ordinal);
        var bootstrap = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/ErpOnlyShopBootstrap.cs"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS `users`", bootstrap, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `sessions`", bootstrap, StringComparison.Ordinal);
        Assert.Contains("do not remap TenantRegistry to PHP", bootstrap, StringComparison.Ordinal);
        Assert.Contains("OpenAsync(dbName, userName: null, password: null", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = false", service, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
