using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosTenantConfigBulkSetWriteTests
{
    [Fact]
    public void Route_exposes_bulk_set()
    {
        Assert.Equal("/bos/tenant-config/bulk-set", EcomAeRoutes.BosTenantConfigBulkSet);
        Assert.Equal("/bos/ajax-writes/dry-run/bulk_set", new BosAjaxWriteCatalog().All
            .First(item => item.Action == "bulk_set").AspNetRouteHint);
    }

    [Fact]
    public void Parse_values_matches_php_json_decode()
    {
        Assert.Empty(BosTenantConfigWriteService.ParseValues(null));
        Assert.Empty(BosTenantConfigWriteService.ParseValues(""));
        Assert.Empty(BosTenantConfigWriteService.ParseValues("{"));
        Assert.Empty(BosTenantConfigWriteService.ParseValues("{}"));
        Assert.Empty(BosTenantConfigWriteService.ParseValues("[]"));
        Assert.Empty(BosTenantConfigWriteService.ParseValues("null"));
        Assert.Empty(BosTenantConfigWriteService.ParseValues("5"));

        var named = BosTenantConfigWriteService.ParseValues(
            "{\"company_name\":\"Acme\",\"logo_url\":\"/x\",\"flag\":true,\"off\":false,\"n\":5}");
        Assert.Equal(5, named.Count);
        Assert.Equal(("company_name", "Acme"), named[0]);
        Assert.Equal(("logo_url", "/x"), named[1]);
        Assert.Equal(("flag", "1"), named[2]);
        Assert.Equal(("off", ""), named[3]);
        Assert.Equal(("n", "5"), named[4]);

        var nested = BosTenantConfigWriteService.ParseValues("{\"company_name\":{\"x\":1}}");
        Assert.Equal(("company_name", "Array"), nested[0]);
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var missing = await new BosTenantConfigWriteService(new UnconfiguredConnections())
            .BulkSetAsync("!!!", "branding", "{\"company_name\":\"Acme\"}", 0);
        Assert.False(missing.Succeeded);
        Assert.Equal("invalid", missing.Code);
        Assert.Equal("Missing site_key", missing.Message);
    }

    [Fact]
    public async Task Empty_or_unwhitelisted_values_succeed_without_db()
    {
        var empty = await new BosTenantConfigWriteService(new UnconfiguredConnections())
            .BulkSetAsync("acme", "branding", "{}", 0);
        Assert.True(empty.Succeeded);
        Assert.Equal(1, empty.Writes);
        Assert.Equal("Tenant config saved", empty.Message);

        var omitted = await new BosTenantConfigWriteService(new UnconfiguredConnections())
            .BulkSetAsync("acme", "branding", null, 0);
        Assert.True(omitted.Succeeded);
        Assert.Equal(1, omitted.Writes);

        var rawKey = await new BosTenantConfigWriteService(new UnconfiguredConnections())
            .BulkSetAsync("acme", "branding", "{\"Company-name\":\"Acme\"}", 0);
        Assert.True(rawKey.Succeeded);
        Assert.Equal(1, rawKey.Writes);

        var badGroup = await new BosTenantConfigWriteService(new UnconfiguredConnections())
            .BulkSetAsync("acme", "widgets", "{\"company_name\":\"Acme\"}", 0);
        Assert.True(badGroup.Succeeded);
        Assert.Equal(1, badGroup.Writes);
    }

    [Fact]
    public async Task Whitelisted_values_fail_db_when_unconfigured()
    {
        var written = await new BosTenantConfigWriteService(new UnconfiguredConnections())
            .BulkSetAsync("acme", "branding", "{\"company_name\":\"Acme\"}", 0);
        Assert.False(written.Succeeded);
        Assert.Equal("db", written.Code);
        Assert.Equal("Database unavailable", written.Message);
    }

    [Fact]
    public void Page_posts_native_bulk_set()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/tenant-config/bulk-set\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"group\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"values\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_bulk_set_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/tenant-config/bulk-set");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_tenant_config_bulk_set", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_tenant_config", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
        Assert.Contains("/bos/ajax-writes/dry-run/bulk_set", write.Notes, StringComparison.Ordinal);
        Assert.Contains("refuse-confirm", write.Notes, StringComparison.Ordinal);

        var set = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/tenant-config/set");
        Assert.DoesNotContain("Bulk-set, import", set.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_bulk_set_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosTenantConfigBulkSet", module, StringComparison.Ordinal);
        Assert.Contains("IBosTenantConfigWriteService", module, StringComparison.Ordinal);
        Assert.Contains("BulkSetAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosTenantConfigWriteService.cs"));
        Assert.Contains("epc_tenant_config_bulk_set", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_tenant_config`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_tenant_config_history`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("refuse-confirm", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IBosTenantConfigWriteService", program, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"tenant-config\"", policy, StringComparison.Ordinal);
        var reporter = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs"));
        Assert.Contains("tenant-config set/bulk-set", reporter, StringComparison.Ordinal);
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
