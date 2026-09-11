using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosSoc2PolicyWriteTests
{
    [Fact]
    public void Route_exposes_create_policy()
    {
        Assert.Equal("/bos/soc2/create-policy", EcomAeRoutes.BosSoc2CreatePolicy);
    }

    [Fact]
    public void Php_policy_data_matches_php_defaults()
    {
        Assert.Equal(("", "", "", "", "[]"), BosSoc2WriteService.ParsePolicyData(null));
        Assert.Equal(("", "", "", "", "[]"), BosSoc2WriteService.ParsePolicyData(""));
        Assert.Equal(("", "", "", "", "[]"), BosSoc2WriteService.ParsePolicyData("{}"));
        Assert.Equal(("", "", "", "", "[]"), BosSoc2WriteService.ParsePolicyData("[]"));
        Assert.Equal(("", "", "", "", "[]"), BosSoc2WriteService.ParsePolicyData("not-json"));
        var parsed = BosSoc2WriteService.ParsePolicyData(
            "{\"policy_code\":\"is-1\",\"title\":\"Access\",\"content\":\"Text\",\"owner\":\"Ada\",\"related_controls\":[\"CC1.1\"]}");
        Assert.Equal("IS-1", parsed.PolicyCode);
        Assert.Equal("Access", parsed.Title);
        Assert.Equal("Text", parsed.Content);
        Assert.Equal("Ada", parsed.Owner);
        Assert.Contains("CC1.1", parsed.RelatedControlsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_posts_native_create_policy()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/soc2/create-policy\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"policy_code\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"related_controls\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_create_policy_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/soc2/create-policy");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_soc2_create_policy", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_soc2_policies", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_create_policy_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosSoc2CreatePolicy", module, StringComparison.Ordinal);
        Assert.Contains("CreatePolicyAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosSoc2WriteService.cs"));
        Assert.Contains("epc_soc2_add_evidence", service, StringComparison.Ordinal);
        Assert.Contains("epc_soc2_update_control", service, StringComparison.Ordinal);
        Assert.Contains("epc_soc2_create_policy", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_soc2_policies`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
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
