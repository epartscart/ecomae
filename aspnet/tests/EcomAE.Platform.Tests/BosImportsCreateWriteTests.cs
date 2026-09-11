using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosImportsCreateWriteTests
{
    [Fact]
    public void Route_exposes_create()
    {
        Assert.Equal("/bos/imports/create", EcomAeRoutes.BosImportsCreate);
    }

    [Fact]
    public void Php_job_data_and_schemas_match_php()
    {
        Assert.Equal("acme1", BosImportWriteService.PhpBosSiteKey("Acme-1!"));
        Assert.Equal("", BosImportWriteService.PhpBosSiteKey(null));

        var empty = BosImportWriteService.ParseJobData(null);
        Assert.Equal("products", empty.EntityType);
        Assert.Equal("csv", empty.SourceFormat);
        Assert.Equal("", empty.Filename);
        Assert.Equal(0, empty.TotalRows);
        Assert.Equal("[]", empty.FieldMappingJson);
        Assert.Equal("[]", empty.OptionsJson);
        Assert.Equal(0, empty.DryRun);

        var invalid = BosImportWriteService.ParseJobData("not-json");
        Assert.Equal("products", invalid.EntityType);

        var parsed = BosImportWriteService.ParseJobData(
            "{\"entity_type\":\"customers\",\"source_format\":\"xlsx\",\"filename\":\"a.csv\",\"total_rows\":\"12x\",\"field_mapping\":{\"email\":\"e\"},\"dry_run\":1}");
        Assert.Equal("customers", parsed.EntityType);
        Assert.Equal("xlsx", parsed.SourceFormat);
        Assert.Equal("a.csv", parsed.Filename);
        Assert.Equal(12, parsed.TotalRows);
        Assert.Contains("email", parsed.FieldMappingJson, StringComparison.Ordinal);
        Assert.Equal(1, parsed.DryRun);

        Assert.True(BosImportWriteService.EntitySchemas.ContainsKey("products"));
        Assert.True(BosImportWriteService.EntitySchemas.ContainsKey("gl_entries"));
        Assert.False(BosImportWriteService.EntitySchemas.ContainsKey("widgets"));
        Assert.Equal(12, BosImportWriteService.PhpIntval("12rows"));
    }

    [Fact]
    public async Task Missing_site_key_and_unknown_type_fail_invalid_before_db()
    {
        var missing = await new BosImportWriteService(new UnconfiguredConnections())
            .CreateJobAsync("!!!", null);
        Assert.False(missing.Succeeded);
        Assert.Equal("invalid", missing.Code);
        Assert.Equal("Missing site_key", missing.Message);

        var unknown = await new BosImportWriteService(new UnconfiguredConnections())
            .CreateJobAsync("acme", "{\"entity_type\":\"widgets\"}");
        Assert.False(unknown.Succeeded);
        Assert.Equal("invalid", unknown.Code);
        Assert.Equal("Unknown entity type: widgets", unknown.Message);
    }

    [Fact]
    public void Page_posts_native_create()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/imports/create\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_type\"", razor, StringComparison.Ordinal);
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
            item.AspNetRouteOrCapability == "/bos/imports/create");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_import_create_job", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_import_jobs", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_create_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosImportsCreate", module, StringComparison.Ordinal);
        Assert.Contains("CreateJobAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosImportWriteService.cs"));
        Assert.Contains("epc_import_create_job", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_import_jobs`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IBosImportWriteService", program, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"imports\"", policy, StringComparison.Ordinal);
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
