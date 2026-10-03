using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the read twin of PHP <c>epc_bos_wf_requests</c> / <c>epc_bos_wf_request_log</c> on the approvals page.</summary>
public sealed class ErpBosWfRequestReadPhpParityTests
{
    [Fact]
    public void StepLabels_MirrorPhpDecodeSteps()
    {
        Assert.Equal(["Approval"], ErpBosWfRequestReadService.ErpBosWfStepLabels(null));
        Assert.Equal(["Approval"], ErpBosWfRequestReadService.ErpBosWfStepLabels("[]"));
        Assert.Equal(["Approval"], ErpBosWfRequestReadService.ErpBosWfStepLabels("not json"));
        Assert.Equal(
            ["Manager sign-off", "Finance"],
            ErpBosWfRequestReadService.ErpBosWfStepLabels("""[{"role":"Manager","label":"Manager sign-off"},{"role":"CFO","label":"Finance"}]"""));
    }

    [Fact]
    public void EntityTypeLabels_MatchPhpEntityTypes()
    {
        Assert.Equal(9, ErpBosWfRequestReadService.EntityTypeLabels.Count);
        Assert.Equal("Purchase invoice / bill", ErpBosWfRequestReadService.EntityTypeLabel("purchase_invoice"));
        Assert.Equal("Expense claim", ErpBosWfRequestReadService.EntityTypeLabel("expense"));
        Assert.Equal("custom_type", ErpBosWfRequestReadService.EntityTypeLabel("custom_type"));
    }

    [Fact]
    public void Service_MatchesPhpQueriesWithoutDdl()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBosWfRequestReadService.cs"));
        Assert.Contains("ORDER BY `created_at` DESC LIMIT", text, StringComparison.Ordinal);
        Assert.Contains("WHERE `request_id` = ? ORDER BY `id` ASC", text, StringComparison.Ordinal);
        Assert.Contains("WHERE `active` = 1", text, StringComparison.Ordinal);
        Assert.Equal(100, ErpBosWfRequestReadService.PendingLimit);
        Assert.Equal(80, ErpBosWfRequestReadService.HistoryLimit);
        Assert.DoesNotContain("CREATE TABLE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovalsApp_RendersPendingAndHistoryRowsWithPerRowDecide()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpApprovalsApp.razor"));
        Assert.Contains("IErpBosWfRequestReadService", text, StringComparison.Ordinal);
        Assert.Contains("Pending approvals", text, StringComparison.Ordinal);
        Assert.Contains("History &amp; audit", text, StringComparison.Ordinal);
        Assert.Contains("No pending approvals.", text, StringComparison.Ordinal);
        Assert.Contains("No approval requests yet.", text, StringComparison.Ordinal);
        Assert.Contains("id=\"approval-request-@r.Id\"", text, StringComparison.Ordinal);
        Assert.Contains("id=\"approval-history-@r.Id\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"@r.Id\"", text, StringComparison.Ordinal);
        Assert.Contains("Reject this request?", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersRequestReadService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBosWfRequestReadService, EcomAE.Platform.Erp.ErpBosWfRequestReadService", text, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
