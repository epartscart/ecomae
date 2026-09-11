using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosCreditSetLimitWriteTests
{
    [Fact]
    public void Route_exposes_set_limit()
    {
        Assert.Equal("/bos/credit/set-limit", EcomAeRoutes.BosCreditSetLimit);
    }

    [Fact]
    public void Php_defaults_match_php()
    {
        Assert.Equal("acme", BosCreditWriteService.NormalizeSiteKey("ACME"));
        Assert.Equal(12.5m, BosCreditWriteService.PhpFloat("12.5x"));
        Assert.Equal(0m, BosCreditWriteService.PhpFloat("abc"));
        Assert.Equal("2026-12-10", BosCreditWriteService.DefaultNextReview(new DateTime(2026, 9, 11)));
    }

    [Fact]
    public async Task Missing_site_or_customer_fails_invalid_before_db()
    {
        var missingSite = await new BosCreditWriteService(new UnconfiguredConnections())
            .SetLimitAsync("!!!", 9, "1000", null, null, null, null, 0);
        Assert.False(missingSite.Succeeded);
        Assert.Equal("invalid", missingSite.Code);
        Assert.Equal("Missing site_key or customer_id", missingSite.Message);

        var missingCustomer = await new BosCreditWriteService(new UnconfiguredConnections())
            .SetLimitAsync("acme", 0, "1000", null, null, null, null, 0);
        Assert.False(missingCustomer.Succeeded);
        Assert.Equal("invalid", missingCustomer.Code);
        Assert.Equal("Missing site_key or customer_id", missingCustomer.Message);
    }

    [Fact]
    public void Page_posts_native_set_limit()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/credit/set-limit\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"customer_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"credit_limit\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_set_limit_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/credit/set-limit");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("set_limit", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_credit_set_limit", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_credit_limits", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_set_limit_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosCreditSetLimit", module, StringComparison.Ordinal);
        Assert.Contains("SetLimitAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosCreditWriteService.cs"));
        Assert.Contains("epc_credit_set_limit", service, StringComparison.Ordinal);
        Assert.Contains("epc_credit_hold", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_credit_limits`", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
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
