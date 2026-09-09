using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCrmTicketStatusWriteTests
{
    [Fact]
    public void Route_exposes_ticket_write()
    {
        Assert.Equal("/cp/crm/tickets/write", EcomAeRoutes.CpCrmTicketsWrite);
    }

    [Fact]
    public void Status_and_priority_allowlists()
    {
        Assert.Contains("open", CpCrmTicketWriteService.Statuses);
        Assert.Contains("closed", CpCrmTicketWriteService.Statuses);
        Assert.DoesNotContain("critical", CpCrmTicketWriteService.Statuses);
        Assert.Contains("normal", CpCrmTicketWriteService.Priorities);
        Assert.Contains("urgent", CpCrmTicketWriteService.Priorities);
        Assert.DoesNotContain("medium", CpCrmTicketWriteService.Priorities);
    }

    [Fact]
    public void Page_posts_native_update_ticket_status()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmTicketsApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/crm/tickets/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"action\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"update_ticket_status\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"status\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/tickets/reply", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_update_ticket_status_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/crm/tickets/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_crm.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("update_ticket_status", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_update_ticket_status()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCrmTicketWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCrmTicketWriteService", module, StringComparison.Ordinal);
        Assert.Contains("UpdateStatusAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCrmTicketWriteService.cs"));
        Assert.Contains("epc_crm_save_ticket", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_crm_tickets`", service, StringComparison.Ordinal);
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
