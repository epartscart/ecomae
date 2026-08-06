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
