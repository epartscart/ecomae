using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosNlReportingCreateWriteTests
{
    [Fact]
    public void Route_exposes_create()
    {
        Assert.Equal("/bos/nl-reporting/create", EcomAeRoutes.BosNlReportingCreate);
    }

    [Fact]
    public void Php_report_data_matches_php_defaults()
    {
        Assert.Equal("acme1", BosNlReportingWriteService.PhpBosSiteKey("Acme-1!"));
        Assert.Equal("", BosNlReportingWriteService.PhpBosSiteKey("!!!"));

        var empty = BosNlReportingWriteService.ParseReportData(null);
        Assert.Equal("Custom Report", empty.Name);
        Assert.Equal("", empty.Description);
        Assert.Equal("custom", empty.ReportType);
        Assert.Equal("", empty.QueryTemplate);
        Assert.Equal("[]", empty.ParametersJson);
        Assert.Equal("manual", empty.Schedule);
        Assert.Equal("csv", empty.Format);
        Assert.Equal("[]", empty.RecipientsJson);
        Assert.Equal(0L, empty.CreatedBy);
        Assert.Equal(empty, BosNlReportingWriteService.ParseReportData(""));
        Assert.Equal(empty, BosNlReportingWriteService.ParseReportData("{}"));
        Assert.Equal(empty, BosNlReportingWriteService.ParseReportData("[]"));
        Assert.Equal(empty, BosNlReportingWriteService.ParseReportData("not-json"));

        var parsed = BosNlReportingWriteService.ParseReportData(
            "{\"name\":\"Sales\",\"description\":\"daily\",\"report_type\":\"builtin\",\"query_template\":\"SELECT 1\",\"parameters\":{\"start_date\":\"date\"},\"schedule\":\"daily\",\"format\":\"json\",\"recipients\":[\"ops@x\"],\"created_by\":\"4x\"}");
        Assert.Equal("Sales", parsed.Name);
        Assert.Equal("daily", parsed.Description);
        Assert.Equal("builtin", parsed.ReportType);
        Assert.Equal("SELECT 1", parsed.QueryTemplate);
        Assert.Contains("start_date", parsed.ParametersJson, StringComparison.Ordinal);
        Assert.Equal("daily", parsed.Schedule);
        Assert.Equal("json", parsed.Format);
        Assert.Contains("ops@x", parsed.RecipientsJson, StringComparison.Ordinal);
        Assert.Equal(4L, parsed.CreatedBy);

        var emptyName = BosNlReportingWriteService.ParseReportData("{\"name\":\"\",\"format\":\"\"}");
        Assert.Equal("", emptyName.Name);
        Assert.Equal("", emptyName.Format);
        Assert.Equal("[]", BosNlReportingWriteService.ParseReportData("{\"parameters\":{}}").ParametersJson);
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var written = await new BosNlReportingWriteService(new UnconfiguredConnections())
            .CreateAsync("!!!", "{\"name\":\"Sales\"}");
        Assert.False(written.Succeeded);
        Assert.Equal("invalid", written.Code);
        Assert.Equal("Missing site_key", written.Message);
    }

    [Fact]
    public void Page_posts_native_create()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/nl-reporting/create\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_create_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/nl-reporting/create");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_nlr_create_definition", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_report_definitions", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_create_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosNlReportingCreate", module, StringComparison.Ordinal);
        Assert.Contains("IBosNlReportingWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosNlReportingWriteService.cs"));
        Assert.Contains("epc_nlr_create_definition", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_report_definitions`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IBosNlReportingWriteService", program, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"nl-reporting\"", policy, StringComparison.Ordinal);
    }

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
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
