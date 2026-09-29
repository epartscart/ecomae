using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>ERP guide book + approvals + route-alias PHP parity floors.</summary>
[Collection(PreferAspNetAppsCollection.Name)]
public sealed class ErpGuideParityTests
{
    public ErpGuideParityTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
    }

    [Fact]
    public void GuideJsonLoadsAtLeastSeventyModules()
    {
        Assert.True(
            ErpGuideCatalog.All.Count >= 70,
            $"Expected >= 70 guide modules, got {ErpGuideCatalog.All.Count}");
    }

    [Fact]
    public void GuideCatalogContainsCoreCompanyInventory()
    {
        Assert.NotNull(ErpGuideCatalog.Get("core"));
        Assert.NotNull(ErpGuideCatalog.Get("company"));
        Assert.NotNull(ErpGuideCatalog.Get("inventory"));
        Assert.Contains(ErpGuideCatalog.All, m => m.Module.Equals("core", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ErpGuideCatalog.All, m => m.Module.Equals("company", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ErpGuideCatalog.All, m => m.Module.Equals("inventory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ErpGuideAppHasGuideAppPage()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpGuideApp.razor"));
        Assert.Contains("@page \"/erp/guide-app\"", src);
        Assert.Contains("@page \"/erp/guide\"", src);
        Assert.Contains("PhpErpDesktopChrome", src);
        Assert.Contains("ErpGuideCatalog", src);
    }

    [Fact]
    public void MapErpPhpPathMapsTabGuide()
    {
        Assert.Equal(
            "/erp/guide-app",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=overview&tab=guide"));
        Assert.Equal(
            "/erp/guide-app",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?tab=knowledge_base"));
        Assert.Equal(
            "/erp/guide-app",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?tab=knowledge"));
        Assert.Equal(
            "/erp/approvals-app",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&tab=approvals"));
        Assert.Equal(
            "/erp/approvals-app",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?tab=approval"));
    }

    [Fact]
    public void ApprovalsAppPageExists()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpApprovalsApp.razor"));
        Assert.Contains("@page \"/erp/approvals-app\"", src);
        Assert.Contains("@page \"/erp/approvals\"", src);
        Assert.Contains("BuildErpAsync", src);
        Assert.Contains("ApprovalQueue", src);
        Assert.Contains("PhpErpDesktopChrome", src);
    }

    [Fact]
    public void WorkflowAppUsesSharedDocumentWorkspaceForOpenedTasks()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpWorkflowApp.razor"));
        Assert.Contains("ErpDocumentWorkspace", text, StringComparison.Ordinal);
        Assert.Contains("WorkflowWorkspaceFields", text, StringComparison.Ordinal);
        Assert.Contains("WorkflowWorkspaceLines", text, StringComparison.Ordinal);
        Assert.Contains("task_id", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovalAndAgendaPagesUseSharedWorkspaceForOpenedRecords()
    {
        var approvals = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpApprovalsApp.razor"));
        var agenda = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpAgendaApp.razor"));
        Assert.Contains("ErpDocumentWorkspace", approvals, StringComparison.Ordinal);
        Assert.Contains("ApprovalWorkspaceFields", approvals, StringComparison.Ordinal);
        Assert.Contains("item", approvals, StringComparison.Ordinal);
        Assert.Contains("ErpDocumentWorkspace", agenda, StringComparison.Ordinal);
        Assert.Contains("AgendaWorkspaceFields", agenda, StringComparison.Ordinal);
        Assert.Contains("AgendaWorkspaceLines", agenda, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryPlanningAndForecastPagesUseSharedWorkspaceForOpenedRecords()
    {
        var forecast = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpInventoryForecastApp.razor"));
        var planning = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpOrderPlanningApp.razor"));
        var report = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpInventoryReportApp.razor"));
        Assert.Contains("ErpDocumentWorkspace", forecast, StringComparison.Ordinal);
        Assert.Contains("ForecastWorkspaceFields", forecast, StringComparison.Ordinal);
        Assert.Contains("ErpDocumentWorkspace", planning, StringComparison.Ordinal);
        Assert.Contains("PlanningWorkspaceLines", planning, StringComparison.Ordinal);
        Assert.Contains("ErpDocumentWorkspace", report, StringComparison.Ordinal);
        Assert.Contains("SnapshotWorkspaceFields", report, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryWarehouseAndMovementPagesUseSharedWorkspaceForOpenedRecords()
    {
        var warehouses = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpWarehousesApp.razor"));
        var movements = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpStockMovementsApp.razor"));
        Assert.Contains("ErpDocumentWorkspace", warehouses, StringComparison.Ordinal);
        Assert.Contains("WarehouseWorkspaceFields", warehouses, StringComparison.Ordinal);
        Assert.Contains("ErpDocumentWorkspace", movements, StringComparison.Ordinal);
        Assert.Contains("MovementWorkspaceLines", movements, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryRfidPageUsesSharedWorkspaceForOpenedSessions()
    {
        var rfid = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpRfidApp.razor"));
        Assert.Contains("ErpDocumentWorkspace", rfid, StringComparison.Ordinal);
        Assert.Contains("RfidWorkspaceFields", rfid, StringComparison.Ordinal);
        Assert.Contains("RfidWorkspaceLines", rfid, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryTransferAndQualityPagesUseSharedWorkspaceForOpenedRecords()
    {
        var transfers = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpStockTransfersApp.razor"));
        var quality = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpQualityApp.razor"));
        Assert.Contains("ErpDocumentWorkspace", transfers, StringComparison.Ordinal);
        Assert.Contains("TransferWorkspaceFields", transfers, StringComparison.Ordinal);
        Assert.Contains("TransferWorkspaceLines", transfers, StringComparison.Ordinal);
        Assert.Contains("ErpDocumentWorkspace", quality, StringComparison.Ordinal);
        Assert.Contains("QualityWorkspaceFields", quality, StringComparison.Ordinal);
        Assert.Contains("QualityWorkspaceLines", quality, StringComparison.Ordinal);
    }

    [Fact]
    public void WarehouseWmsPageUsesSharedWorkspaceForOpenedWork()
    {
        var wms = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpWarehouseWmsApp.razor"));
        Assert.Contains("ErpDocumentWorkspace", wms, StringComparison.Ordinal);
        Assert.Contains("WmsWorkspaceFields", wms, StringComparison.Ordinal);
        Assert.Contains("WmsWorkspaceLines", wms, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/wms/work/complete\"", wms, StringComparison.Ordinal);
    }

    [Fact]
    public void QualityOrdersUseSharedWorkspaceWithoutClaimingResultWriteOwnership()
    {
        var quality = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpQualityApp.razor"));
        Assert.Contains("DocumentKind=\"Quality order\"", quality, StringComparison.Ordinal);
        Assert.Contains("QualityOrderWorkspaceFields", quality, StringComparison.Ordinal);
        Assert.Contains("QualityOrderWorkspaceLines", quality, StringComparison.Ordinal);
        Assert.Contains("Inspection-result recording remains on the Classic/ERP ajax ownership path.", quality, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardSummaryAppHasDashboardAppAlias()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpDashboardSummaryApp.razor"));
        Assert.Contains("@page \"/erp/dashboard-summary-app\"", src);
        Assert.Contains("@page \"/erp/dashboard-app\"", src);
    }

    [Fact]
    public void ErpTopbarLinksGuideAndApprovals()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("href=\"/erp/guide-app\"", src);
        Assert.Contains("href=\"/erp/approvals-app\"", src);
        Assert.Contains(">Guide<", src);
        Assert.Contains(">Approvals<", src);
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

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
