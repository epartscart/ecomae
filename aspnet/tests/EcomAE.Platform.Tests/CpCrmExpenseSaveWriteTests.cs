using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCrmExpenseSaveWriteTests
{
    [Fact]
    public void Route_exposes_expense_write()
    {
        Assert.Equal("/cp/crm/expenses/write", EcomAeRoutes.CpCrmExpensesWrite);
    }

    [Fact]
    public void Status_and_category_match_php()
    {
        Assert.Contains("draft", CpCrmExpenseWriteService.Statuses);
        Assert.Contains("submitted", CpCrmExpenseWriteService.Statuses);
        Assert.Contains("approved", CpCrmExpenseWriteService.Statuses);
        Assert.Contains("rejected", CpCrmExpenseWriteService.Statuses);
        Assert.Contains("paid", CpCrmExpenseWriteService.Statuses);
        Assert.DoesNotContain("open", CpCrmExpenseWriteService.Statuses);
        Assert.Equal("draft", CpCrmExpenseWriteService.NormalizeStatus("nope"));
        Assert.Equal("travel", CpCrmExpenseWriteService.NormalizeCategory(""));
        Assert.Equal(0m, CpCrmExpenseWriteService.NormalizeAmount(-8));
        Assert.Equal(512, CpCrmExpenseWriteService.NormalizeReceiptNote(new string('x', 600)).Length);
    }

    [Fact]
    public void Page_posts_native_save_expense()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmOpportunitiesApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/crm/expenses/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_expense\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"employee_user_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"receipt_note\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"approve_expense\"", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_expense_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/crm/expenses/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_crm.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_expense", write.Notes, StringComparison.Ordinal);
        Assert.Contains("approve", write.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_and_module_save_expense()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCrmExpenseWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCrmExpenseWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCrmExpenseWriteService.cs"));
        Assert.Contains("epc_crm_save_expense", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_expenses`", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_cash_entry", service, StringComparison.Ordinal);
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
