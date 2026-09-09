using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPlatformGovernanceWriteTests
{
    [Fact]
    public void Route_exposes_platform_governance_write()
    {
        Assert.Equal("/cp/platform-governance/write", EcomAeRoutes.CpPlatformGovernanceWrite);
    }

    [Fact]
    public void Rule_key_and_enforcement_match_php()
    {
        Assert.Equal("data_retention", CpPlatformGovernanceWriteService.NormalizeRuleKey(" Data-Retention! "));
        Assert.Equal("", CpPlatformGovernanceWriteService.NormalizeRuleKey(" !!! "));
        Assert.Equal("required", CpPlatformGovernanceWriteService.NormalizeEnforcement(null));
        Assert.Equal("required", CpPlatformGovernanceWriteService.NormalizeEnforcement(""));
        Assert.Equal("advisory", CpPlatformGovernanceWriteService.NormalizeEnforcement(" Advisory "));
        Assert.Equal("blocked", CpPlatformGovernanceWriteService.NormalizeEnforcement("blocked"));
        Assert.Null(CpPlatformGovernanceWriteService.NormalizeEnforcement("nope"));
        Assert.Contains("advisory", CpPlatformGovernanceWriteService.EnforcementLevels);
        Assert.Contains("required", CpPlatformGovernanceWriteService.EnforcementLevels);
        Assert.Contains("blocked", CpPlatformGovernanceWriteService.EnforcementLevels);
    }

    [Fact]
    public void Page_posts_native_save_rule()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPlatformGovernanceApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/platform-governance/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_rule\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"rule_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"active\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"enforcement\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_rule_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/platform-governance/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_platform_governance.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_rule()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpPlatformGovernanceWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpPlatformGovernanceWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPlatformGovernanceWriteService.cs"));
        Assert.Contains("epc_platform_governance_update_rule", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_platform_governance_rules`", service, StringComparison.Ordinal);
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
