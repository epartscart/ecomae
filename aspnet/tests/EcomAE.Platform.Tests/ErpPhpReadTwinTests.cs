using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPhpReadTwinTests
{
    [Fact]
    public void WorkflowTabMapsToDedicatedApp_NotProcessFlow()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("workflow", out var href));
        Assert.Equal("/erp/workflow-app", href);
        Assert.Equal("/erp/workflow-app", EcomAeRoutes.ErpWorkflowApp);
        Assert.Equal("/erp/process-flow-tasks-app", EcomAeRoutes.ErpProcessFlowTasksApp);
        Assert.NotEqual(EcomAeRoutes.ErpWorkflowApp, EcomAeRoutes.ErpProcessFlowTasksApp);
    }

    [Fact]
    public void MissingPhpTabsHaveSqlAndApps()
    {
        var root = FindRepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        Assert.Contains("SelectErpWorkflowTasks", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpVatReturnSales", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpWithholdingCodes", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpPettyCash", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpCashForecasts", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpBankInstruments", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpSubscriptions", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpSupplierPortalSuppliers", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpVirtualWarehouses", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpStaffProfiles", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpContracts", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpOpeningBatches", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpMarketingCampaigns", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpPayrollRuns", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpPrintTemplates", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpOrderRecommendations", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpProcCategories", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpQmPlans", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpRfidTags", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpRecruitmentJobs", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpCustomerGroups", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpPerformanceReviews", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpProductInfoItems", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpReportSchedules", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpPrjaBudgets", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpDocAttachments", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpInventoryReportCategories", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("corrective_action", LegacySurfaceDashboardSql.SelectErpQmNcrs, StringComparison.Ordinal);
        Assert.DoesNotContain("`notes`", LegacySurfaceDashboardSql.SelectErpRecruitmentJobs, StringComparison.Ordinal);
        Assert.DoesNotContain("description", LegacySurfaceDashboardSql.SelectErpCustomerGroups, StringComparison.Ordinal);
        Assert.DoesNotContain("`notes`", LegacySurfaceDashboardSql.SelectErpPerformanceReviews, StringComparison.Ordinal);
        Assert.DoesNotContain("options_json", LegacySurfaceDashboardSql.SelectErpProductInfoFieldDefs, StringComparison.Ordinal);
        Assert.DoesNotContain("combo_json", LegacySurfaceDashboardSql.SelectErpProductInfoVariants, StringComparison.Ordinal);
        Assert.DoesNotContain("recipients", LegacySurfaceDashboardSql.SelectErpReportSchedules, StringComparison.Ordinal);
        Assert.DoesNotContain("body_template", LegacySurfaceDashboardSql.SelectErpReportSchedules, StringComparison.Ordinal);
        Assert.DoesNotContain("detail_json", LegacySurfaceDashboardSql.SelectErpPrjaRecognitions, StringComparison.Ordinal);
        Assert.DoesNotContain("file_path", LegacySurfaceDashboardSql.SelectErpDocAttachments, StringComparison.Ordinal);
        Assert.DoesNotContain("body_text", LegacySurfaceDashboardSql.SelectErpContracts, StringComparison.Ordinal);
        Assert.DoesNotContain("ocr_text", LegacySurfaceDashboardSql.SelectErpContracts, StringComparison.Ordinal);
        Assert.DoesNotContain("header_html", LegacySurfaceDashboardSql.SelectErpPrintTemplates, StringComparison.Ordinal);
        Assert.DoesNotContain("custom_css", LegacySurfaceDashboardSql.SelectErpPrintTemplates, StringComparison.Ordinal);

        var pages = Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages");
        Assert.True(File.Exists(Path.Combine(pages, "ErpWorkflowApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpStaffApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpContractsApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpOpeningApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpMarketingApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpPayrollApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpPrintDesignerApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpOrderPlanningApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpProcurementCategoriesApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpQualityApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpRfidApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpRecruitmentApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpCustomerGroupsApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpPerformanceApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpProductInfoApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpReportSchedulerApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpProjectAccountingApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpDocAttachmentsApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpInventoryReportApp.razor")));
        Assert.Contains("BuildErpVatReturnDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpVatApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpWithholdingDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpWithholdingApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpPettyCashAsync", File.ReadAllText(Path.Combine(pages, "ErpCashAccountsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpCashForecastDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpCashAccountsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpBankInstrumentsDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpBankReconciliationApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpSubscriptionsDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpSalesOrdersApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpSupplierPortalDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpSuppliersApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpVirtualWarehouseDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpWarehousesApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpStaffAsync", File.ReadAllText(Path.Combine(pages, "ErpStaffApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpContractsAsync", File.ReadAllText(Path.Combine(pages, "ErpContractsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpOpeningBatchesAsync", File.ReadAllText(Path.Combine(pages, "ErpOpeningApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpMarketingCampaignsAsync", File.ReadAllText(Path.Combine(pages, "ErpMarketingApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpPayrollRunsAsync", File.ReadAllText(Path.Combine(pages, "ErpPayrollApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpPrintTemplatesAsync", File.ReadAllText(Path.Combine(pages, "ErpPrintDesignerApp.razor")), StringComparison.Ordinal);
    }

    [Fact]
    public void DedicatedPeopleAndSetupTabsMapOffPhpAndGenericShells()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("staff", out var staff));
        Assert.Equal("/erp/staff-app", staff);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("contracts", out var contracts));
        Assert.Equal("/erp/contracts-app", contracts);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("opening", out var opening));
        Assert.Equal("/erp/opening-app", opening);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("opening_balances", out var openingBalances));
        Assert.Equal("/erp/opening-app", openingBalances);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("marketing", out var marketing));
        Assert.Equal("/erp/marketing-app", marketing);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("payroll", out var payroll));
        Assert.Equal("/erp/payroll-app", payroll);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("print_designer", out var print));
        Assert.Equal("/erp/print-designer-app", print);
        Assert.Equal("/erp/staff-app", EcomAeRoutes.ErpStaffApp);
        Assert.Equal("/erp/print-designer-app", EcomAeRoutes.ErpPrintDesignerApp);
        Assert.DoesNotContain("/cp/hr-overview-app", staff, StringComparison.Ordinal);
        Assert.DoesNotContain("/erp/module-app", print, StringComparison.Ordinal);

        Assert.True(ErpPhpTabRouteMap.TryMapTab("order_planning", out var opl));
        Assert.Equal("/erp/order-planning-app", opl);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("procurement_categories", out var proc));
        Assert.Equal("/erp/procurement-categories-app", proc);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("quality", out var quality));
        Assert.Equal("/erp/quality-app", quality);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("rfid", out var rfid));
        Assert.Equal("/erp/rfid-app", rfid);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("recruitment", out var recruitment));
        Assert.Equal("/erp/recruitment-app", recruitment);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("customer_groups", out var groups));
        Assert.Equal("/erp/customer-groups-app", groups);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("performance", out var performance));
        Assert.Equal("/erp/performance-app", performance);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("product_info", out var productInfo));
        Assert.Equal("/erp/product-info-app", productInfo);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("report_scheduler", out var scheduler));
        Assert.Equal("/erp/report-scheduler-app", scheduler);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("project_accounting", out var prja));
        Assert.Equal("/erp/project-accounting-app", prja);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("doc_attachment", out var docs));
        Assert.Equal("/erp/doc-attachments-app", docs);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("inventory_report", out var invReport));
        Assert.Equal("/erp/inventory-report-app", invReport);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("ext_reports", out var extReports));
        Assert.Equal("/erp/tax-external-reporting-app", extReports);
        Assert.DoesNotContain("/cp/tax-external-reporting-app", extReports, StringComparison.Ordinal);
        Assert.DoesNotContain("/cp/production-overview-app", opl, StringComparison.Ordinal);
        Assert.DoesNotContain("/cp/hr-overview-app", recruitment, StringComparison.Ordinal);
        Assert.DoesNotContain("/cp/hr-overview-app", performance, StringComparison.Ordinal);
        Assert.DoesNotContain("/cp/projects-overview-app", prja, StringComparison.Ordinal);
        Assert.DoesNotContain("/erp/inventory-stock-app", productInfo, StringComparison.Ordinal);
    }

    [Fact]
    public void ErpAppsDoNotDiscloseAspNetInMarkup()
    {
        var root = FindRepoRoot();
        var dir = Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages");
        var offenders = Directory.GetFiles(dir, "Erp*App.razor")
            .Where(f => File.ReadAllText(f).Contains("ASP.NET Core digest", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(offenders.Count == 0, "ASP.NET disclosure leftovers: " + string.Join(", ", offenders));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
