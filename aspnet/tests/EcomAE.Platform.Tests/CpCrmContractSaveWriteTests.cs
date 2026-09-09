using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCrmContractSaveWriteTests
{
    [Fact]
    public void Route_exposes_contract_write()
    {
        Assert.Equal("/cp/crm/contracts/write", EcomAeRoutes.CpCrmContractsWrite);
    }

    [Fact]
    public void Status_interval_and_title_match_php()
    {
        Assert.Contains("draft", CpCrmContractWriteService.Statuses);
        Assert.Contains("paused", CpCrmContractWriteService.Statuses);
        Assert.DoesNotContain("cancelled", CpCrmContractWriteService.Statuses);
        Assert.Equal("draft", CpCrmContractWriteService.NormalizeStatus("nope"));
        Assert.Contains("monthly", CpCrmContractWriteService.Intervals);
        Assert.Contains("once", CpCrmContractWriteService.Intervals);
        Assert.Equal("monthly", CpCrmContractWriteService.NormalizeInterval("weekly"));
        Assert.Equal("Contract", CpCrmContractWriteService.NormalizeTitle(""));
        Assert.Equal(0m, CpCrmContractWriteService.NormalizeAmount(-4));
        var now = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(now.AddMonths(1).ToUnixTimeSeconds(), CpCrmContractWriteService.ParseNextBilling("", now));
        Assert.True(CpCrmContractWriteService.ParseNextBilling("2026-10-09", now) > 0);
    }

    [Fact]
    public void Page_posts_native_save_contract()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmOpportunitiesApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/crm/contracts/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_contract\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"customer_user_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"billing_interval\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"next_billing_date\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_contract_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/crm/contracts/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_crm.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_contract", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_contract()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCrmContractWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCrmContractWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCrmContractWriteService.cs"));
        Assert.Contains("epc_crm_save_contract", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_contracts`", service, StringComparison.Ordinal);
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
