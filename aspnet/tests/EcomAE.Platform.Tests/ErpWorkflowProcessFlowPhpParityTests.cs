using System.Reflection;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards workflow board, process-flow tabs, and Automation Centre against Classic-twin dead-end chrome.</summary>
public sealed class ErpWorkflowProcessFlowPhpParityTests
{
    [Fact]
    public void WorkflowBoard_MatchesPhpDepartmentBoard()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpWorkflowApp.razor"));
        Assert.Contains("Department workflow board", text, StringComparison.Ordinal);
        Assert.Contains("Sales</strong> → <strong>Purchase", text, StringComparison.Ordinal);
        Assert.Contains("/erp/workflow/create", text, StringComparison.Ordinal);
        Assert.Contains("/erp/workflow/status", text, StringComparison.Ordinal);
        Assert.Contains("name=\"dept\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("classic twin", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessFlow_HasSevenViewsAndCaseTable()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpProcessFlowTasksApp.razor"));
        Assert.Contains("nav nav-tabs", text, StringComparison.Ordinal);
        Assert.Contains("pf_view", text, StringComparison.Ordinal);
        Assert.Contains("pf-track-wrap", text, StringComparison.Ordinal);
        Assert.Contains("Customer Order → Delivery", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/pf-case-cancel", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/pf-step-delete", text, StringComparison.Ordinal);
        Assert.Contains("Live site map", text, StringComparison.Ordinal);
        foreach (var view in new[] { "monitor", "orgmap", "hierarchy", "workforce", "inbox", "processes", "heads" })
        {
            Assert.Contains(view, text, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("epc_erp_processflow.php", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("classic twin", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomationCentre_UsesPhpTealCatalogue()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpWorkflowsApp.razor"));
        Assert.Contains("epc-auto-hero", text, StringComparison.Ordinal);
        Assert.Contains("epc-auto-tabs", text, StringComparison.Ordinal);
        Assert.Contains("auto_view", text, StringComparison.Ordinal);
        Assert.Contains("Accounting &amp; Business Process Automation", text, StringComparison.Ordinal);
        Assert.Contains("ErpAutomationCatalogue", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-wf-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomationCatalogue_PortsPhpIds()
    {
        Assert.Equal(22, ErpAutomationCatalogue.All.Count);
        Assert.Contains(ErpAutomationCatalogue.All, i => i.Id == "order_to_erp" && i.Category == "accounting");
        Assert.Contains(ErpAutomationCatalogue.All, i => i.Id == "process_flow_routing" && i.Tab == "processflow");
        Assert.Equal("/erp/process-flow-tasks-app", ErpAutomationCatalogue.OpenHref(
            ErpAutomationCatalogue.All.First(i => i.Id == "process_flow_routing")));
        Assert.True(ErpAutomationCatalogue.Templates.Count >= 7);
    }

    [Fact]
    public void Dashboard_UsesExecutiveTilesWithoutStackDisclosure()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpDashboardSummaryApp.razor"));
        Assert.Contains("epc-exec-kpis", text, StringComparison.Ordinal);
        Assert.Contains("Executive dashboard", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("classic twin", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvoicesAndOrders_DropGhostScaffoldAndShameCopy()
    {
        foreach (var rel in new[]
                 {
                     "aspnet/src/EcomAE.Platform/Components/Pages/ErpInvoicesApp.razor",
                     "aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor",
                     "aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchaseOrdersApp.razor",
                 })
        {
            var text = File.ReadAllText(FindRepoFile(rel));
            Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
            Assert.Contains("ErpPhpStatusLabel", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Live shop digest", text, StringComparison.Ordinal);
            Assert.DoesNotContain("classic twin", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void StatusLabel_MapsPhpBadgeClasses()
    {
        Assert.Equal("success", ErpPhpStatusLabel.Class("paid"));
        Assert.Equal("info", ErpPhpStatusLabel.Class("draft"));
        Assert.Equal("danger", ErpPhpStatusLabel.Class("cancelled"));
        Assert.Equal("default", ErpPhpStatusLabel.Class(""));
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

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
