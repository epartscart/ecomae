using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosDunningAddInvoiceWriteTests
{
    [Fact]
    public void Route_exposes_add_invoice()
    {
        Assert.Equal("/bos/dunning/add-invoice", EcomAeRoutes.BosDunningAddInvoice);
    }

    [Fact]
    public void Php_invoice_and_days_match_php()
    {
        var empty = BosDunningWriteService.ParseInvoice(null);
        Assert.Equal(0, empty.CustomerId);
        Assert.Equal("", empty.CustomerName);
        Assert.Equal("", empty.InvoiceRef);
        Assert.Equal(0m, empty.InvoiceAmount);
        Assert.Equal(0m, empty.AmountDue);
        Assert.Null(empty.DueDate);
        Assert.Equal(0, empty.ProfileId);

        var invalid = BosDunningWriteService.ParseInvoice("not-json");
        Assert.Equal(0m, invalid.InvoiceAmount);

        var parsed = BosDunningWriteService.ParseInvoice(
            "{\"customer_id\":\"12x\",\"customer_name\":\"Ada\",\"invoice_ref\":\"INV-1\",\"invoice_amount\":\"99.5\",\"due_date\":\"2026-01-01\",\"profile_id\":\"3\"}");
        Assert.Equal(12, parsed.CustomerId);
        Assert.Equal("Ada", parsed.CustomerName);
        Assert.Equal("INV-1", parsed.InvoiceRef);
        Assert.Equal(99.5m, parsed.InvoiceAmount);
        Assert.Equal(99.5m, parsed.AmountDue);
        Assert.Equal("2026-01-01", parsed.DueDate);
        Assert.Equal(3, parsed.ProfileId);

        var dueOverride = BosDunningWriteService.ParseInvoice(
            "{\"invoice_amount\":40,\"amount_due\":\"12.25\"}");
        Assert.Equal(40m, dueOverride.InvoiceAmount);
        Assert.Equal(12.25m, dueOverride.AmountDue);

        var now = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Local);
        Assert.Equal(0, BosDunningWriteService.DaysOverdue("2026-09-11", now));
        Assert.Equal(10, BosDunningWriteService.DaysOverdue("2026-09-01", now));
        Assert.Equal(0, BosDunningWriteService.DaysOverdue("2026-09-20", now));
        Assert.Equal(0, BosDunningWriteService.DaysOverdue("not-a-date", now));
        Assert.Equal(12, BosDunningWriteService.PhpIntval("12days"));
        Assert.Equal(99.5m, BosDunningWriteService.PhpFloat("99.5usd"));
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var written = await new BosDunningWriteService(new UnconfiguredConnections())
            .AddInvoiceAsync("!!!", 1, "Ada", "INV-1", 10m, null, null, 0);
        Assert.False(written.Succeeded);
        Assert.Equal("invalid", written.Code);
        Assert.Equal("Missing site_key", written.Message);
    }

    [Fact]
    public void Page_posts_native_add_invoice()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/dunning/add-invoice\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"invoice_ref\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"invoice_amount\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_add_invoice_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/dunning/add-invoice");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("add_invoice", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_dunning_add_invoice", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_dunning_queue", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_add_invoice_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosDunningAddInvoice", module, StringComparison.Ordinal);
        Assert.Contains("AddInvoiceAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosDunningWriteService.cs"));
        Assert.Contains("epc_dunning_add_invoice", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_dunning_queue`", service, StringComparison.Ordinal);
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
