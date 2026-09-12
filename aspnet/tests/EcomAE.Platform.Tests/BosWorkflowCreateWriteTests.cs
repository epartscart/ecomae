using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosWorkflowCreateWriteTests
{
    [Fact]
    public void Route_exposes_create()
    {
        Assert.Equal("/bos/workflows/create", EcomAeRoutes.BosWorkflowsCreate);
    }

    [Fact]
    public void Php_defaults_match_php()
    {
        Assert.Equal("acme", BosWorkflowWriteService.PhpBosSiteKey("ACME"));
        Assert.Equal("", BosWorkflowWriteService.PhpBosSiteKey("!!!"));
        Assert.Equal(12, BosWorkflowWriteService.PhpIntval("12abc"));
        Assert.Equal(0, BosWorkflowWriteService.PhpIntval("abc"));

        var empty = BosWorkflowWriteService.ParseWorkflowData(null);
        Assert.Equal("Untitled Workflow", empty.Name);
        Assert.Equal("manual", empty.TriggerType);
        Assert.Equal("[]", empty.TriggerConfigJson);
        Assert.Equal(0, empty.Active);
        Assert.Empty(empty.Steps);

        var named = BosWorkflowWriteService.ParseWorkflowData("{\"name\":\"\",\"trigger_config\":{},\"active\":true}");
        Assert.Equal("", named.Name);
        Assert.Equal("[]", named.TriggerConfigJson);
        Assert.Equal(1, named.Active);

        var stepped = BosWorkflowWriteService.ParseWorkflowData(
            "{\"name\":\"Ship\",\"steps\":[{\"action_type\":\"notify\",\"config\":{}}]}");
        Assert.Equal("Ship", stepped.Name);
        Assert.Single(stepped.Steps);
        Assert.Equal("action", stepped.Steps[0].StepType);
        Assert.Equal("notify", stepped.Steps[0].ActionType);
        Assert.Equal("[]", stepped.Steps[0].ConfigJson);
        Assert.Equal("stop", stepped.Steps[0].OnFailure);
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var missing = await new BosWorkflowWriteService(new UnconfiguredConnections())
            .CreateAsync("!!!", "{\"name\":\"Ship\"}");
        Assert.False(missing.Succeeded);
        Assert.Equal("invalid", missing.Code);
        Assert.Equal("Missing site_key", missing.Message);
    }

    [Fact]
    public void Page_posts_native_create()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/workflows/create\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
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
            item.AspNetRouteOrCapability == "/bos/workflows/create");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("create", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_workflow_create", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_workflows", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_create_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosWorkflowsCreate", module, StringComparison.Ordinal);
        Assert.Contains("CreateAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosWorkflowWriteService.cs"));
        Assert.Contains("epc_workflow_create", service, StringComparison.Ordinal);
        Assert.Contains("epc_workflow_toggle", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_workflows`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_workflow_steps`", service, StringComparison.Ordinal);
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
