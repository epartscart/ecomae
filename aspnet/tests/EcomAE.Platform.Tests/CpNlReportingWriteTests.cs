using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpNlReportingWriteTests
{
    [Fact]
    public void Route_exposes_nl_reporting_write()
    {
        Assert.Equal("/cp/nl-reporting/write", EcomAeRoutes.CpNlReportingWrite);
    }

    [Fact]
    public void Normalize_schedule_and_format_match_php_defaults()
    {
        Assert.Equal("eparts-cart", CpNlReportingWriteService.NormalizeSiteKey(" eParts-Cart! "));
        Assert.Equal("manual", CpNlReportingWriteService.NormalizeSchedule(""));
        Assert.Equal("daily", CpNlReportingWriteService.NormalizeSchedule("daily"));
        Assert.Equal("csv", CpNlReportingWriteService.NormalizeFormat(""));
        Assert.Equal("json", CpNlReportingWriteService.NormalizeFormat("json"));
        Assert.Equal("pdf", CpNlReportingWriteService.NormalizeFormat("pdf"));
    }

    [Fact]
    public void Page_posts_native_save_and_delete()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpNlReportingApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/nl-reporting/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"delete\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"recipients", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"query_template", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/nl-reporting/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_nl_reporting.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_definition()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpNlReportingWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpNlReportingWriteService", module, StringComparison.Ordinal);
        Assert.Contains("save_definition", module, StringComparison.Ordinal);
        Assert.Contains("delete_definition", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpNlReportingWriteService.cs"));
        Assert.Contains("epc_nlr_create_definition", service, StringComparison.Ordinal);
        Assert.Contains("epc_nlr_delete_definition", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_report_definitions`", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_report_definitions`", service, StringComparison.Ordinal);
        Assert.Contains("`active`=0", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        Assert.DoesNotContain("query_template`=?", service, StringComparison.Ordinal);
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
