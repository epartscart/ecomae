using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosEntityIntercompanyWriteTests
{
    [Fact]
    public void Route_exposes_intercompany()
    {
        Assert.Equal("/bos/entities/intercompany", EcomAeRoutes.BosEntitiesIntercompany);
    }

    [Fact]
    public void Php_float_amount_matches_leading_numeric_cast()
    {
        Assert.Equal(0m, BosEntityWriteService.PhpFloat(null));
        Assert.Equal(0m, BosEntityWriteService.PhpFloat(""));
        Assert.Equal(0m, BosEntityWriteService.PhpFloat("abc"));
        Assert.Equal(12.5m, BosEntityWriteService.PhpFloat("12.5abc"));
        Assert.Equal(-3.25m, BosEntityWriteService.PhpFloat("-3.25"));
    }

    [Fact]
    public void Page_posts_native_intercompany()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/entities/intercompany\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"group_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"from\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"to\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_intercompany_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/entities/intercompany");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_entity_record_intercompany", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_intercompany_txns", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_intercompany_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosEntitiesIntercompany", module, StringComparison.Ordinal);
        Assert.Contains("RecordIntercompanyAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosEntityWriteService.cs"));
        Assert.Contains("epc_entity_add_member", service, StringComparison.Ordinal);
        Assert.Contains("epc_entity_record_intercompany", service, StringComparison.Ordinal);
        Assert.Contains("epc_entity_eliminate", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_intercompany_txns`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        Assert.DoesNotContain("From and to site keys are required", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Inter-company amount cannot be zero", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Entity group not found", service, StringComparison.Ordinal);
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
