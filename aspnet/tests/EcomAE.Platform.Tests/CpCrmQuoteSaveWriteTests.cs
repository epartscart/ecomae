using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCrmQuoteSaveWriteTests
{
    [Fact]
    public void Route_exposes_quote_write()
    {
        Assert.Equal("/cp/crm/quotes/write", EcomAeRoutes.CpCrmQuotesWrite);
    }

    [Fact]
    public void Status_allowlist_and_number_match_php()
    {
        Assert.Contains("draft", CpCrmQuoteWriteService.Statuses);
        Assert.Contains("accepted", CpCrmQuoteWriteService.Statuses);
        Assert.DoesNotContain("won", CpCrmQuoteWriteService.Statuses);
        Assert.Equal("draft", CpCrmQuoteWriteService.NormalizeStatus("nope"));
        Assert.Equal("Q-202609-0003", CpCrmQuoteWriteService.NextQuoteNumber(3, new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Page_posts_native_save_quote()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesQuotationsApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/crm/quotes/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"action\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_quote\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"quote_number\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"line_description\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_quote_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/crm/quotes/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_crm.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_quote", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_quote()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCrmQuoteWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCrmQuoteWriteService", module, StringComparison.Ordinal);
        Assert.Contains("SaveAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCrmQuoteWriteService.cs"));
        Assert.Contains("epc_crm_save_quote", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_quotes`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_quote_lines`", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
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
