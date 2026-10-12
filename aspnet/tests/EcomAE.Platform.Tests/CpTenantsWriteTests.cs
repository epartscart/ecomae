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
        Assert.Contains("value=\"delete_standalone_erp\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"reset_operator_password\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirm_site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"industry_code\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_set_active_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/tenants/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("onboard_erp_only", write.Notes, StringComparison.Ordinal);
        Assert.Contains("delete_standalone_erp", write.Notes, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", write.Notes, StringComparison.Ordinal);
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
        Assert.False(CpTenantsWriteService.IsAllowedIndustry("platform_host"));
        Assert.Equal("jewellery", CpTenantsWriteService.NormalizeIndustry("jewellery"));
        Assert.False(CpTenantsWriteService.IsDroppableDatabase("ecomae"));
        Assert.True(CpTenantsWriteService.IsDroppableDatabase("asap"));
    }

    [Fact]
    public void Program_and_module_set_active()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpTenantsWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpTenantsWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpTenantsWriteService.cs"));
        Assert.Contains("epc_portal_tenant_control_set_active", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_portal_tenants`", service, StringComparison.Ordinal);
        Assert.Contains("OnboardErpOnlyAsync", service, StringComparison.Ordinal);
        Assert.Contains("DeleteStandaloneErpAsync", service, StringComparison.Ordinal);
        Assert.Contains("onboard_erp_only", module, StringComparison.Ordinal);
        Assert.Contains("delete_standalone_erp", module, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
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
