using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosTenantConfigSetWriteTests
{
    [Fact]
    public void Route_exposes_set()
    {
        Assert.Equal("/bos/tenant-config/set", EcomAeRoutes.BosTenantConfigSet);
    }

    [Fact]
    public void Php_keys_and_groups_match_php()
    {
        Assert.Equal("acme1", BosTenantConfigWriteService.PhpBosSiteKey("Acme-1!"));
        Assert.Equal("", BosTenantConfigWriteService.PhpBosSiteKey("!!!"));
        Assert.Equal("branding", BosTenantConfigWriteService.PhpGroupKey("branding"));
        Assert.Equal("randing", BosTenantConfigWriteService.PhpGroupKey("Branding!"));
        Assert.Equal("company_name", BosTenantConfigWriteService.PhpConfigKey("company_name"));
        Assert.Equal("ompanyname", BosTenantConfigWriteService.PhpConfigKey("Company-name!"));
        Assert.True(BosTenantConfigWriteService.Groups.ContainsKey("branding"));
        Assert.True(BosTenantConfigWriteService.Groups.ContainsKey("features"));
        Assert.True(BosTenantConfigWriteService.TryField("tax", "vat_rate", out var vat));
        Assert.Equal("float", vat.Type);
        Assert.Equal("5", vat.Default);
        Assert.False(BosTenantConfigWriteService.TryField("branding", "vat_rate", out _));
    }

    [Fact]
    public async Task Missing_or_invalid_keys_fail_invalid_before_db()
    {
        var missing = await new BosTenantConfigWriteService(new UnconfiguredConnections())
            .SetAsync("!!!", "branding", "company_name", "Acme", 0);
        Assert.False(missing.Succeeded);
        Assert.Equal("invalid", missing.Code);
        Assert.Equal("Missing site_key", missing.Message);

        var badGroup = await new BosTenantConfigWriteService(new UnconfiguredConnections())
            .SetAsync("acme", "widgets", "company_name", "Acme", 0);
        Assert.False(badGroup.Succeeded);
        Assert.Equal("invalid", badGroup.Code);
        Assert.Equal("Invalid config group", badGroup.Message);

        var badKey = await new BosTenantConfigWriteService(new UnconfiguredConnections())
            .SetAsync("acme", "branding", "vat_rate", "5", 0);
        Assert.False(badKey.Succeeded);
        Assert.Equal("invalid", badKey.Code);
        Assert.Equal("Invalid config key", badKey.Message);
    }

    [Fact]
    public void Page_posts_native_set()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/tenant-config/set\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"group\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"key\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_set_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/tenant-config/set");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_tenant_config_set", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_tenant_config", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_set_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosTenantConfigSet", module, StringComparison.Ordinal);
        Assert.Contains("IBosTenantConfigWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosTenantConfigWriteService.cs"));
        Assert.Contains("epc_tenant_config_set", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_tenant_config`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_tenant_config_history`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IBosTenantConfigWriteService", program, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"tenant-config\"", policy, StringComparison.Ordinal);
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
