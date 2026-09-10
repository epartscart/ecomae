using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosSoc2ControlWriteTests
{
    [Fact]
    public void Route_exposes_update_control()
    {
        Assert.Equal("/bos/soc2/update-control", EcomAeRoutes.BosSoc2UpdateControl);
    }

    [Fact]
    public void Parse_control_data_matches_php_isset()
    {
        Assert.Empty(BosSoc2WriteService.ParseControlData(null));
        Assert.Empty(BosSoc2WriteService.ParseControlData("{"));
        Assert.Empty(BosSoc2WriteService.ParseControlData("[]"));
        var parsed = BosSoc2WriteService.ParseControlData("{\"status\":\"tested\",\"owner\":\"ops\",\"ignored\":1}");
        Assert.Equal("tested", parsed["status"]);
        Assert.Equal("ops", parsed["owner"]);
        Assert.False(BosSoc2WriteService.ParseControlData("{\"status\":null}").ContainsKey("status"));
    }

    [Fact]
    public async Task Empty_fields_fail_without_db()
    {
        var result = await new BosSoc2WriteService(new UnconfiguredConnections())
            .UpdateControlAsync("CC1.1", new Dictionary<string, string?>());
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("No fields to update", result.Message);
    }

    [Fact]
    public void Page_posts_native_update_control()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/soc2/update-control\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"control_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"control_data\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_update_control_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/soc2/update-control");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("update_control", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_soc2_controls", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_update_control_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosSoc2UpdateControl", module, StringComparison.Ordinal);
        Assert.Contains("UpdateControlAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosSoc2WriteService.cs"));
        Assert.Contains("epc_soc2_update_control", service, StringComparison.Ordinal);
        Assert.Contains("epc_soc2_add_evidence", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_soc2_controls`", service, StringComparison.Ordinal);
        Assert.Contains("No fields to update", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
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
