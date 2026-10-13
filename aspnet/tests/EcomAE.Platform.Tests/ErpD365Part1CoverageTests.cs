using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpD365Part1CoverageTests
{
    [Fact]
    public void Catalog_CoversEveryPart1ModuleAndPhotoLabel()
    {
        Assert.True(ErpD365Part1Catalog.All.Count >= 480);
        Assert.Equal(
            ErpD365Part1Catalog.All.Count,
            ErpD365Part1Catalog.All.Select(p => p.Tab).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Empty(
            ErpD365Part1Catalog.All.Select(p => p.Tab)
                .Intersect(ErpD365Part3Catalog.All.Select(p => p.Tab), StringComparer.OrdinalIgnoreCase));

        Assert.Contains("Invoice journal", ErpD365Part1Catalog.RequiredLabels("ap"));
        Assert.Contains("Pending vendor invoices", ErpD365Part1Catalog.RequiredLabels("ap"));
        Assert.Contains("Invoice matching", ErpD365Part1Catalog.RequiredLabels("ap"));
        Assert.Contains("All vendors", ErpD365Part1Catalog.RequiredLabels("ap"));
        Assert.Contains("Vendor payment journal", ErpD365Part1Catalog.RequiredLabels("ap"));
        Assert.Contains("All customers", ErpD365Part1Catalog.RequiredLabels("ar"));
        Assert.Contains("Free text invoices", ErpD365Part1Catalog.RequiredLabels("ar"));
        Assert.Contains("Customer payment journal", ErpD365Part1Catalog.RequiredLabels("ar"));
        Assert.Contains("All assets", ErpD365Part1Catalog.RequiredLabels("asset_mgmt"));
        Assert.Contains("All work orders", ErpD365Part1Catalog.RequiredLabels("asset_mgmt"));
        Assert.Contains("Audit cases", ErpD365Part1Catalog.RequiredLabels("audit_wb"));
        Assert.Contains("Audit policies", ErpD365Part1Catalog.RequiredLabels("audit_wb"));
        Assert.Contains("Budget register entries", ErpD365Part1Catalog.RequiredLabels("budgeting"));
        Assert.Contains("Budget control configuration", ErpD365Part1Catalog.RequiredLabels("budgeting"));
        Assert.Contains("Bank accounts", ErpD365Part1Catalog.RequiredLabels("banking"));
        Assert.Contains("Reconciliation", ErpD365Part1Catalog.RequiredLabels("banking"));
        Assert.Contains("Work items assigned to me", ErpD365Part1Catalog.RequiredLabels("common"));
        Assert.Contains("All CAPA cases", ErpD365Part1Catalog.RequiredLabels("common"));
        Assert.Contains("Consolidate online", ErpD365Part1Catalog.RequiredLabels("consolidations"));
        Assert.Contains("Elimination rules", ErpD365Part1Catalog.RequiredLabels("consolidations"));
        Assert.Contains("Cost accounting ledgers", ErpD365Part1Catalog.RequiredLabels("cost_acct"));
        Assert.Contains("Costing versions", ErpD365Part1Catalog.RequiredLabels("cost_mgmt"));
        Assert.Contains("Collections cases", ErpD365Part1Catalog.RequiredLabels("credit_coll"));
        Assert.Contains("Collection letters", ErpD365Part1Catalog.RequiredLabels("credit_coll"));
        Assert.Contains("Expense reports", ErpD365Part1Catalog.RequiredLabels("expense"));
        Assert.Contains("Travel requisition", ErpD365Part1Catalog.RequiredLabels("expense"));
        Assert.Contains("Fixed assets", ErpD365Part1Catalog.RequiredLabels("fixed_assets"));
        Assert.Contains("Depreciation profiles", ErpD365Part1Catalog.RequiredLabels("fixed_assets"));
        Assert.Contains("General journals", ErpD365Part1Catalog.RequiredLabels("gl"));
        Assert.Contains("Trial balance", ErpD365Part1Catalog.RequiredLabels("gl"));
        Assert.Contains("Chart of accounts", ErpD365Part1Catalog.RequiredLabels("gl"));
        Assert.Contains("Employees", ErpD365Part1Catalog.RequiredLabels("people"));
        Assert.Contains("Recruitment projects", ErpD365Part1Catalog.RequiredLabels("people"));
        Assert.Contains("On-hand", ErpD365Part1Catalog.RequiredLabels("inventory_mgmt"));
        Assert.Contains("Quality orders", ErpD365Part1Catalog.RequiredLabels("inventory_mgmt"));
        Assert.Contains("All voyages", ErpD365Part1Catalog.RequiredLabels("landed_cost_area"));
        Assert.Contains("Voyage costing by individual cost", ErpD365Part1Catalog.RequiredLabels("landed_cost_area"));
    }

    [Fact]
    public void RouteMap_ResolvesNewTabsAndKeepsExistingDedicatedApps()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("ap_invoice_register", out var register));
        Assert.Equal("/erp/payables-workspace-app?tab=ap_invoice_register", register);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("ar_invoice_pool", out var pool));
        Assert.Equal("/erp/receivables-workspace-app?tab=ar_invoice_pool", pool);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("am_all_work_orders", out var wo));
        Assert.Equal("/erp/asset-management-app?tab=am_all_work_orders", wo);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("aw_policies", out var policies));
        Assert.Equal("/erp/audit-workbench-app?tab=aw_policies", policies);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("bud_control_config", out var control));
        Assert.Equal("/erp/budgeting-workspace-app?tab=bud_control_config", control);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("cb_positive_pay", out var pay));
        Assert.Equal("/erp/cash-bank-workspace-app?tab=cb_positive_pay", pay);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("cmn_all_capa", out var capa));
        Assert.Equal("/erp/common-workspace-app?tab=cmn_all_capa", capa);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("cons_elim_rules", out var cons));
        Assert.Equal("/erp/consolidations-workspace-app?tab=cons_elim_rules", cons);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("ca_overhead", out var overhead));
        Assert.Equal("/erp/cost-accounting-app?tab=ca_overhead", overhead);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("cm_variance", out var variance));
        Assert.Equal("/erp/cost-management-app?tab=cm_variance", variance);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("cc_collection_letters", out var letters));
        Assert.Equal("/erp/credit-collections-workspace-app?tab=cc_collection_letters", letters);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("exp_travel_req", out var travel));
        Assert.Equal("/erp/expense-workspace-app?tab=exp_travel_req", travel);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("fa_depr_profiles", out var depr));
        Assert.Equal("/erp/fixed-assets-workspace-app?tab=fa_depr_profiles", depr);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("gl_account_structures", out var structures));
        Assert.Equal("/erp/general-ledger-workspace-app?tab=gl_account_structures", structures);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("hr_departments", out var openPos));
        Assert.Equal("/erp/hr-workspace-app?tab=hr_departments", openPos);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("inv_tag_counting", out var tags));
        Assert.Equal("/erp/inventory-workspace-app?tab=inv_tag_counting", tags);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("lc_folios", out var folios));
        Assert.Equal("/erp/landed-cost-workspace-app?tab=lc_folios", folios);

        Assert.True(ErpPhpTabRouteMap.TryMapTab("payables", out var payables));
        Assert.Equal("/erp/payables-app", payables);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("receivables", out var receivables));
        Assert.Equal("/erp/receivables-app", receivables);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("jw_trial_balance", out var tb));
        Assert.Equal("/erp/trial-balance-app", tb);
        Assert.True(ErpD365Part1Catalog.TryGet("ap_invoice_journal", out var invoice));
        Assert.Equal("/erp/payables-app", invoice.ExistingHref);
        Assert.True(ErpD365Part1Catalog.TryGet("gl_trial_balance", out var trial));
        Assert.Equal("/erp/trial-balance-app", trial.ExistingHref);
    }

    [Fact]
    public void AreaHubs_OpenDedicatedWorkspaces()
    {
        Assert.Equal("/erp/payables-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=ap"));
        Assert.Equal("/erp/receivables-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=ar"));
        Assert.Equal("/erp/asset-management-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=asset_mgmt"));
        Assert.Equal("/erp/audit-workbench-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=audit_wb"));
        Assert.Equal("/erp/budgeting-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=budgeting"));
        Assert.Equal("/erp/cash-bank-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=banking"));
        Assert.Equal("/erp/common-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=common"));
        Assert.Equal("/erp/consolidations-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=consolidations"));
        Assert.Equal("/erp/cost-accounting-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=cost_acct"));
        Assert.Equal("/erp/cost-management-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=cost_mgmt"));
        Assert.Equal("/erp/credit-collections-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=credit_coll"));
        Assert.Equal("/erp/expense-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=expense"));
        Assert.Equal("/erp/fixed-assets-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=fixed_assets"));
        Assert.Equal("/erp/general-ledger-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=gl"));
        Assert.Equal("/erp/hr-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=people"));
        Assert.Equal("/erp/inventory-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=inventory_mgmt"));
        Assert.Equal("/erp/landed-cost-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=landed_cost_area"));
        Assert.Equal("/erp/payables-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=ap&tab=payables"));
        Assert.Equal("/erp/trial-balance-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&tab=jw_trial_balance"));
    }

    [Fact]
    public void Nav_ListsNewWorkspaceTabs()
    {
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "ap/ap_invoice_register");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "ar/ar_invoice_pool");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "asset_mgmt/am_all_work_orders");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "audit_wb/aw_policies");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "budgeting/bud_control_config");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "banking/cb_positive_pay");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "common/cmn_all_capa");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "consolidations/cons_elim_rules");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "cost_acct/ca_overhead");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "cost_mgmt/cm_variance");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "credit_coll/cc_collection_letters");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "expense/exp_travel_req");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "fixed_assets/fa_depr_profiles");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "gl/gl_account_structures");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "people/hr_departments");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "inventory_mgmt/inv_tag_counting");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "landed_cost_area/lc_folios");
        Assert.DoesNotContain(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "ap/ap_invoice_journal");
        Assert.DoesNotContain(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "gl/gl_trial_balance");
    }

    [Fact]
    public void Write_ValidatesKindAndRefusesDedicatedPages()
    {
        Assert.Equal("invalid", ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "ap_invoice_journal"))!.Code);
        Assert.Equal("invalid", ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "ap_invoice_register"))!.Code);
        Assert.Null(ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "ap_invoice_register", Code: "REG-1", Name: "Register")));
        Assert.Null(ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "aw_policies", Code: "POL-1", Name: "Duplicate invoice")));
        Assert.Null(ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "am_all_work_orders", Name: "Pump-1")));
        Assert.Equal("pending", ErpD365WorkspaceWriteService.NormalizeStatus("", ErpD365Part3Catalog.FormKind.Request));
    }

    [Fact]
    public void App_EmitsWorkspaceFormsWithoutForbiddenWords()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        Assert.Contains("@page \"/erp/payables-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/receivables-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/asset-management-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/audit-workbench-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/budgeting-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/cash-bank-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/common-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/consolidations-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/cost-accounting-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/cost-management-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/credit-collections-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/expense-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/fixed-assets-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/general-ledger-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/hr-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/inventory-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/landed-cost-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpD365WorkspaceCatalog.SavePath", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);

        var catalog = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Presentation/ErpD365Part1Catalog.cs"));
        Assert.DoesNotContain("PHP", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void UnionCatalog_ServesPart1AndPart3()
    {
        Assert.True(ErpD365WorkspaceCatalog.All.Count >= 680);
        Assert.True(ErpD365WorkspaceCatalog.TryGet("ap_invoice_register", out var ap));
        Assert.Equal("ap", ap.Area);
        Assert.True(ErpD365WorkspaceCatalog.TryGet("time_off_requests", out var leave));
        Assert.Equal("leave_abs", leave.Area);
        Assert.Equal("/erp/payables-workspace-app", ErpD365WorkspaceCatalog.AppPathForArea("ap"));
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
