using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpTenantFeaturesWriteTests
{
    [Fact]
    public void Route_exposes_tenant_features_write()
    {
        Assert.Equal("/cp/tenant-features/write", EcomAeRoutes.CpTenantFeaturesWrite);
    }

    [Fact]
    public void Site_and_features_match_php()
    {
        Assert.Equal("e-parts_cart", CpTenantFeaturesWriteService.NormalizeSiteKey(" E-Parts_Cart! "));
        Assert.Equal("", CpTenantFeaturesWriteService.NormalizeSiteKey(" !!! "));
        Assert.Equal("emailsmtp", CpTenantFeaturesWriteService.NormalizeFeatureKey(" Email-SMTP! "));
        Assert.Equal("email_smtp", CpTenantFeaturesWriteService.NormalizeFeatureKey(" Email_SMTP! "));
        Assert.True(CpTenantFeaturesWriteService.IsSaveable("email_smtp"));
        Assert.False(CpTenantFeaturesWriteService.IsSaveable("tenant_registry"));
        Assert.False(CpTenantFeaturesWriteService.IsSaveable("unknown"));
        var flags = CpTenantFeaturesWriteService.NormalizeFeatures(new Dictionary<string, bool> { ["email_smtp"] = true });
        Assert.True(flags["email_smtp"]);
        Assert.False(flags["oauth"]);
        Assert.DoesNotContain("tenant_registry", flags.Keys);
        Assert.Contains("mobile_apps", CpTenantFeaturesWriteService.SaveableKeys);
    }

    [Fact]
    public void Page_posts_native_save_feature_flags()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpTenantFeaturesApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/tenant-features/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_feature_flags\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_feature_flags_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/tenant-features/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_integrations.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_feature_flags()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpTenantFeaturesWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpTenantFeaturesWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpTenantFeaturesWriteService.cs"));
        Assert.Contains("epc_integrations_save_feature_flags", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_tenant_feature_flags`", service, StringComparison.Ordinal);
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
