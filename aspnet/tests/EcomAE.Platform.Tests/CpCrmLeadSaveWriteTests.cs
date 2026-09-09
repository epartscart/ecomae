using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCrmLeadSaveWriteTests
{
    [Fact]
    public void Route_exposes_crm_action()
    {
        Assert.Equal("/cp/crm/action", EcomAeRoutes.CpCrmAction);
    }

    [Fact]
    public void Lead_clip_and_status_allowlist()
    {
        Assert.Equal("Acme", CpCrmWriteService.Clip("  Acme  ", 255));
        Assert.Equal("12345", CpCrmWriteService.Clip("1234567890", 5));
        Assert.Contains("new", CpCrmWriteService.LeadStatuses);
        Assert.Contains("converted", CpCrmWriteService.LeadStatuses);
        Assert.DoesNotContain("won", CpCrmWriteService.LeadStatuses);
    }

    [Fact]
    public void Dry_run_still_blocks_quote_email()
    {
        var confirmEmail = new CpCrmActionDryRun().Evaluate(new CpCrmActionRequest("crm_quote_email", true));
        Assert.Equal(0, confirmEmail.Writes);
        Assert.True(confirmEmail.WritesBlocked);
        Assert.False(confirmEmail.CutoverAllowed);
        Assert.Equal("confirm_writes_refused", confirmEmail.ValidationCode);

        var preview = new CpCrmActionDryRun().Evaluate(new CpCrmActionRequest("save_lead", false));
        Assert.Equal(0, preview.Writes);
        Assert.True(preview.WouldWrite);
    }

    [Fact]
    public void Page_posts_native_save_lead()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmBoardApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/crm/action\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"action\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_lead\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"company\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"contact_name\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("crm_quote_email", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_lead_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/crm/action");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_crm.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_lead", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_lead()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCrmWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCrmWriteService", module, StringComparison.Ordinal);
        Assert.Contains("SaveLeadAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCrmWriteService.cs"));
        Assert.Contains("epc_crm_save_lead", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_leads`", service, StringComparison.Ordinal);
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
