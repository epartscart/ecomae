using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosEntityAddMemberWriteTests
{
    [Fact]
    public void Route_exposes_add_member()
    {
        Assert.Equal("/bos/entities/add-member", EcomAeRoutes.BosEntitiesAddMember);
    }

    [Fact]
    public void Php_bos_site_key_matches_preg_replace()
    {
        Assert.Equal("", BosEntityWriteService.PhpBosSiteKey(null));
        Assert.Equal("", BosEntityWriteService.PhpBosSiteKey(""));
        Assert.Equal("acmeshop", BosEntityWriteService.PhpBosSiteKey("Acme-Shop!"));
        Assert.Equal("epartscart", BosEntityWriteService.PhpBosSiteKey("ePartsCart"));
    }

    [Fact]
    public void Php_float_matches_leading_numeric_cast()
    {
        Assert.Equal(0m, BosEntityWriteService.PhpFloat(null));
        Assert.Equal(0m, BosEntityWriteService.PhpFloat(""));
        Assert.Equal(0m, BosEntityWriteService.PhpFloat("abc"));
        Assert.Equal(100m, BosEntityWriteService.PhpFloat("100"));
        Assert.Equal(50.5m, BosEntityWriteService.PhpFloat("50.5abc"));
        Assert.Equal(100m, BosEntityWriteService.PhpFloat("1e2"));
        Assert.Equal(-12.5m, BosEntityWriteService.PhpFloat("-12.5"));
    }

    [Fact]
    public void Php_member_data_matches_php_defaults()
    {
        var missing = BosEntityWriteService.ParseMemberData(null, "acme");
        Assert.Equal("acme", missing.EntityName);
        Assert.Equal(100m, missing.OwnershipPct);
        Assert.Equal("AED", missing.LocalCurrency);
        Assert.Equal("full", missing.Consolidation);

        var empty = BosEntityWriteService.ParseMemberData("{}", "acme");
        Assert.Equal("acme", empty.EntityName);
        Assert.Equal(100m, empty.OwnershipPct);

        var invalid = BosEntityWriteService.ParseMemberData("not-json", "acme");
        Assert.Equal("acme", invalid.EntityName);
        Assert.Equal(100m, invalid.OwnershipPct);

        var blankPct = BosEntityWriteService.ParseMemberData("{\"ownership_pct\":\"\"}", "acme");
        Assert.Equal(0m, blankPct.OwnershipPct);
        Assert.Equal("acme", blankPct.EntityName);

        var blankName = BosEntityWriteService.ParseMemberData("{\"entity_name\":\"\"}", "acme");
        Assert.Equal("", blankName.EntityName);

        var parsed = BosEntityWriteService.ParseMemberData(
            "{\"entity_name\":\"Branch\",\"ownership_pct\":\"25.5\",\"local_currency\":\"USD\",\"consolidation\":\"equity\"}",
            "acme");
        Assert.Equal("Branch", parsed.EntityName);
        Assert.Equal(25.5m, parsed.OwnershipPct);
        Assert.Equal("USD", parsed.LocalCurrency);
        Assert.Equal("equity", parsed.Consolidation);
    }

    [Fact]
    public void Page_posts_native_add_member()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/entities/add-member\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"group_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_name\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"ownership_pct\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_add_member_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/entities/add-member");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_entity_add_member", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_entity_members", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_add_member_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosEntitiesAddMember", module, StringComparison.Ordinal);
        Assert.Contains("AddMemberAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosEntityWriteService.cs"));
        Assert.Contains("epc_entity_add_member", service, StringComparison.Ordinal);
        Assert.Contains("epc_entity_eliminate", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_entity_members`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        Assert.DoesNotContain("A group id is required", service, StringComparison.Ordinal);
        Assert.DoesNotContain("A site key is required", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Invalid consolidation method", service, StringComparison.Ordinal);
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
