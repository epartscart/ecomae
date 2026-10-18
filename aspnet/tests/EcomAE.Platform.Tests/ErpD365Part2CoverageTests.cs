using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpD365Part2CoverageTests
{
    [Fact]
    public void Catalog_CoversEveryPart2ModuleAndPhotoLabel()
    {
        Assert.True(ErpD365Part2Catalog.All.Count >= 650);
        Assert.Equal(
            ErpD365Part2Catalog.All.Count,
            ErpD365Part2Catalog.All.Select(p => p.Tab).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Empty(
            ErpD365Part2Catalog.All.Select(p => p.Tab)
                .Intersect(ErpD365Part1Catalog.All.Select(p => p.Tab), StringComparer.OrdinalIgnoreCase));
        Assert.Empty(
            ErpD365Part2Catalog.All.Select(p => p.Tab)
                .Intersect(ErpD365Part3Catalog.All.Select(p => p.Tab), StringComparer.OrdinalIgnoreCase));

        Assert.Contains("Vendor catalogs", ErpD365Part2Catalog.RequiredLabels("purchasing"));
        Assert.Contains("Trade agreements", ErpD365Part2Catalog.RequiredLabels("purchasing"));
        Assert.Contains("Purchase agreement fulfillment report", ErpD365Part2Catalog.RequiredLabels("purchasing"));
        Assert.Contains("Released products", ErpD365Part2Catalog.RequiredLabels("pim"));
        Assert.Contains("Bills of materials", ErpD365Part2Catalog.RequiredLabels("pim"));
        Assert.Contains("Hazardous material classification", ErpD365Part2Catalog.RequiredLabels("pim"));
        Assert.Contains("All production orders", ErpD365Part2Catalog.RequiredLabels("production"));
        Assert.Contains("Report as finished", ErpD365Part2Catalog.RequiredLabels("production"));
        Assert.Contains("Kanban board for process jobs", ErpD365Part2Catalog.RequiredLabels("production"));
        Assert.Contains("All projects", ErpD365Part2Catalog.RequiredLabels("projects"));
        Assert.Contains("Project invoice proposals", ErpD365Part2Catalog.RequiredLabels("projects"));
        Assert.Contains("Project profit and loss (actual vs. budget)", ErpD365Part2Catalog.RequiredLabels("projects"));
        Assert.Contains("Questionnaires", ErpD365Part2Catalog.RequiredLabels("questionnaire"));
        Assert.Contains("Complete a questionnaire", ErpD365Part2Catalog.RequiredLabels("questionnaire"));
        Assert.Contains("All rebate management deals", ErpD365Part2Catalog.RequiredLabels("rebate_mgmt"));
        Assert.Contains("Rebate workbench", ErpD365Part2Catalog.RequiredLabels("rebate_mgmt"));
        Assert.Contains("All catalogs", ErpD365Part2Catalog.RequiredLabels("retail"));
        Assert.Contains("Mix and match discounts", ErpD365Part2Catalog.RequiredLabels("retail"));
        Assert.Contains("Registers", ErpD365Part2Catalog.RequiredLabels("retail"));
        Assert.Contains("All sales orders", ErpD365Part2Catalog.RequiredLabels("sales"));
        Assert.Contains("All opportunities", ErpD365Part2Catalog.RequiredLabels("sales"));
        Assert.Contains("Sales and marketing parameters", ErpD365Part2Catalog.RequiredLabels("sales"));
        Assert.Contains("Service orders", ErpD365Part2Catalog.RequiredLabels("service_mgmt"));
        Assert.Contains("Dispatch board", ErpD365Part2Catalog.RequiredLabels("service_mgmt"));
        Assert.Contains("Users", ErpD365Part2Catalog.RequiredLabels("setup"));
        Assert.Contains("Security configuration", ErpD365Part2Catalog.RequiredLabels("setup"));
        Assert.Contains("Sales tax codes", ErpD365Part2Catalog.RequiredLabels("tax"));
        Assert.Contains("Settle and post sales tax", ErpD365Part2Catalog.RequiredLabels("tax"));
        Assert.Contains("Approve", ErpD365Part2Catalog.RequiredLabels("time_att"));
        Assert.Contains("Transfer to pay", ErpD365Part2Catalog.RequiredLabels("time_att"));
        Assert.Contains("Load planning workbench", ErpD365Part2Catalog.RequiredLabels("transportation"));
        Assert.Contains("Shipping carriers", ErpD365Part2Catalog.RequiredLabels("transportation"));
        Assert.Contains("Work planning", ErpD365Part2Catalog.RequiredLabels("warehouse"));
        Assert.Contains("Wave templates", ErpD365Part2Catalog.RequiredLabels("warehouse"));
        Assert.Contains("Cycle count work pending review", ErpD365Part2Catalog.RequiredLabels("warehouse"));
    }

    [Fact]
    public void RouteMap_ResolvesNewTabsAndKeepsExistingDedicatedApps()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("ps_vendor_catalogs", out var catalogs));
        Assert.Equal("/erp/procurement-workspace-app?tab=ps_vendor_catalogs", catalogs);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("pim_released", out var released));
        Assert.Equal("/erp/product-info-app", released);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("prd_kanban_board", out var kanban));
        Assert.Equal("/erp/production-workspace-app?tab=prd_kanban_board", kanban);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("pma_timesheets", out var sheets));
        Assert.Equal("/erp/projects-workspace-app?tab=pma_timesheets", sheets);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("qnr_complete", out var complete));
        Assert.Equal("/erp/questionnaire-workspace-app?tab=qnr_complete", complete);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("rbt_workbench", out var workbench));
        Assert.Equal("/erp/rebate-workspace-app?tab=rbt_workbench", workbench);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("rtc_mix_match", out var mix));
        Assert.Equal("/erp/retail-workspace-app?tab=rtc_mix_match", mix);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("sm_opportunities", out var opps));
        Assert.Equal("/erp/crm-opportunities-app", opps);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("svc_dispatch", out var dispatch));
        Assert.Equal("/erp/service-workspace-app?tab=svc_dispatch", dispatch);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("sys_users", out var users));
        Assert.Equal("/erp/users-app", users);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("tax_sales_codes", out var codes));
        Assert.Equal("/erp/vat-app", codes);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("tna_transfer_pay", out var pay));
        Assert.Equal("/erp/payroll-app", pay);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("tms_load_plan", out var load));
        Assert.Equal("/erp/transportation-workspace-app?tab=tms_load_plan", load);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("wh_wave_templates", out var waves));
        Assert.Equal("/erp/warehouse-workspace-app?tab=wh_wave_templates", waves);

        Assert.True(ErpPhpTabRouteMap.TryMapTab("sales_orders", out var orders));
        Assert.Equal("/erp/sales-orders-app", orders);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("product_info", out var product));
        Assert.Equal("/erp/product-info-app", product);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("wms", out var wms));
        Assert.Equal("/erp/warehouse-wms-app", wms);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("vat", out var vat));
        Assert.Equal("/erp/vat-app", vat);
    }

    [Fact]
    public void AreaHubs_OpenDedicatedWorkspaces()
    {
        Assert.Equal("/erp/procurement-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=purchasing"));
        Assert.Equal("/erp/pim-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=pim"));
        Assert.Equal("/erp/production-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=production"));
        Assert.Equal("/erp/projects-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=projects"));
        Assert.Equal("/erp/questionnaire-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=questionnaire"));
        Assert.Equal("/erp/rebate-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=rebate_mgmt"));
        Assert.Equal("/erp/retail-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=retail"));
        Assert.Equal("/erp/sales-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=sales"));
        Assert.Equal("/erp/service-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=service_mgmt"));
        Assert.Equal("/erp/system-admin-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=setup"));
        Assert.Equal("/erp/tax-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=tax"));
        Assert.Equal("/erp/time-attendance-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=time_att"));
        Assert.Equal("/erp/transportation-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=transportation"));
        Assert.Equal("/erp/warehouse-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=warehouse"));
        Assert.Equal("/erp/sales-orders-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders"));
        Assert.Equal("/erp/vat-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=tax&tab=vat"));
        Assert.Equal("/erp/product-info-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=pim&tab=product_info"));
    }

    [Fact]
    public void Nav_ListsNewWorkspaceTabs()
    {
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "purchasing/ps_vendor_catalogs");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "pim/pim_boms");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "production/prd_kanban_board");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "projects/pma_timesheets");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "questionnaire/qnr_complete");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "rebate_mgmt/rbt_workbench");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "retail/rtc_mix_match");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "sales/sm_source_types");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "service_mgmt/svc_dispatch");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "setup/sys_features");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "tax/tax_hs_rules");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "time_att/tna_approve");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "transportation/tms_load_plan");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "warehouse/wh_wave_templates");
        Assert.DoesNotContain(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "pim/pim_released");
        Assert.DoesNotContain(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "setup/sys_users");
        Assert.DoesNotContain(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "tax/tax_sales_codes");
    }

    [Fact]
    public void Write_ValidatesKindAndRefusesDedicatedPages()
    {
        Assert.Equal("invalid", ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "pim_released"))!.Code);
        Assert.Equal("invalid", ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "ps_vendor_catalogs"))!.Code);
        Assert.Null(ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "ps_vendor_catalogs", Code: "CAT-1", Name: "Vendor catalog")));
        Assert.Null(ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "qnr_complete", Code: "SESS-1", Name: "Safety survey")));
        Assert.Null(ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "wh_wave_templates", Code: "WAVE-1", Name: "Ship wave")));
        Assert.Equal("pending", ErpD365WorkspaceWriteService.NormalizeStatus("", ErpD365Part3Catalog.FormKind.Request));
    }

    [Fact]
    public void App_EmitsWorkspaceFormsWithoutForbiddenWords()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        Assert.Contains("@page \"/erp/pim-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/production-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/projects-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/questionnaire-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/rebate-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/retail-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/sales-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/service-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/system-admin-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/tax-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/time-attendance-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/transportation-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/warehouse-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpD365WorkspaceCatalog.SavePath", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);

        var catalog = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Presentation/ErpD365Part2Catalog.cs"));
        Assert.DoesNotContain("PHP", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void UnionCatalog_ServesPart2WithPart1AndPart3()
    {
        Assert.True(ErpD365WorkspaceCatalog.All.Count >= 1400);
        Assert.True(ErpD365WorkspaceCatalog.TryGet("ps_vendor_catalogs", out var proc));
        Assert.Equal("purchasing", proc.Area);
        Assert.True(ErpD365WorkspaceCatalog.TryGet("ap_invoice_register", out var ap));
        Assert.Equal("ap", ap.Area);
        Assert.True(ErpD365WorkspaceCatalog.TryGet("time_off_requests", out var leave));
        Assert.Equal("leave_abs", leave.Area);
        Assert.Equal("/erp/pim-workspace-app", ErpD365WorkspaceCatalog.AppPathForArea("pim"));
        Assert.Equal("/erp/procurement-workspace-app", ErpD365WorkspaceCatalog.AppPathForArea("purchasing"));
        Assert.Equal("/erp/leave-absence-app", ErpD365WorkspaceCatalog.AppPathForArea("leave_abs"));
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

        throw new FileNotFoundException(relative);
    }
}
