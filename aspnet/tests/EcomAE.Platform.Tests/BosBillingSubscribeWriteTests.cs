using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosBillingSubscribeWriteTests
{
    [Fact]
    public void Route_exposes_subscribe()
    {
        Assert.Equal("/bos/billing/subscribe", EcomAeRoutes.BosBillingSubscribe);
    }

    [Fact]
    public void Php_cycle_mrr_and_invoice_match_php()
    {
        Assert.Equal("acme1", BosBillingWriteService.PhpBosSiteKey("Acme-1!"));
        Assert.Equal(30, BosBillingWriteService.CycleDays(null));
        Assert.Equal(30, BosBillingWriteService.CycleDays("monthly"));
        Assert.Equal(90, BosBillingWriteService.CycleDays("quarterly"));
        Assert.Equal(180, BosBillingWriteService.CycleDays("semi_annual"));
        Assert.Equal(365, BosBillingWriteService.CycleDays("annual"));
        Assert.Equal(99.5m, BosBillingWriteService.MonthlyRecurring(99.5m, "monthly"));
        Assert.Equal(10m, BosBillingWriteService.MonthlyRecurring(30m, "quarterly"));
        Assert.Equal(20m, BosBillingWriteService.MonthlyRecurring(120m, "semi_annual"));
        Assert.Equal(10m, BosBillingWriteService.MonthlyRecurring(120m, "annual"));
        Assert.Equal(
            "INV-ACME-20260911-0007",
            BosBillingWriteService.InvoiceNumber("acme", 7, new DateTime(2026, 9, 11)));
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var written = await new BosBillingWriteService(new UnconfiguredConnections())
            .SubscribeAsync("!!!", 9);
        Assert.False(written.Succeeded);
        Assert.Equal("invalid", written.Code);
        Assert.Equal("Missing site_key", written.Message);
    }

    [Fact]
    public void Page_posts_native_subscribe()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/billing/subscribe\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"plan_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_subscribe_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/billing/subscribe");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_billing_subscribe", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_subscriptions", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_subscribe_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosBillingSubscribe", module, StringComparison.Ordinal);
        Assert.Contains("SubscribeAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosBillingWriteService.cs"));
        Assert.Contains("epc_billing_subscribe", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_subscriptions`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_billing_invoices`", service, StringComparison.Ordinal);
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
