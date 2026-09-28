using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class FitOutDeliveryCatalogTests
{
    [Fact]
    public void CatalogPreservesTheFullPhasedFitOutScenario()
    {
        Assert.Equal(32, FitOutDeliveryCatalog.Steps.Count);
        Assert.Equal(4, FitOutDeliveryCatalog.Steps.Select(step => step.Phase).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(FitOutDeliveryCatalog.Steps, step => step.Name == "Estimate and BOQ header");
        Assert.Equal("partial", FitOutDeliveryCatalog.Steps.Single(step => step.Id == "P1-07").Status);
        Assert.Equal("partial", FitOutDeliveryCatalog.Steps.Single(step => step.Id == "P1-06").Status);
        Assert.Contains(FitOutDeliveryCatalog.Steps, step => step.Name == "Approved variations and contract value revision");
        Assert.Contains(FitOutDeliveryCatalog.Steps, step => step.Name == "Progress claim and certification");
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Purchase requisition and RFQ linkage").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Three-way match and tolerance").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Material issue and return").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Subcontract order and measurement").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Subcontract certification").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Site daily report and photo evidence").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Approved variations and contract value revision").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Progress claim and certification").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "RFI and drawing revisions").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "QA/QC inspection and snag lifecycle").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Weighted BOQ progress").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Retention and advance recovery").Status);
        Assert.Equal(
            "partial",
            FitOutDeliveryCatalog.Steps.Single(step => step.Name == "Numbering and approval thresholds").Status);
    }

    [Fact]
    public void CatalogDoesNotOverstateMigrationCompletion()
    {
        var report = FitOutDeliveryCatalog.BuildReport().ToString();

        Assert.NotNull(report);
        Assert.DoesNotContain("readyForPhpRemoval: true", report!, StringComparison.OrdinalIgnoreCase);
        Assert.All(FitOutDeliveryCatalog.Steps, step =>
            Assert.Contains(step.Status, new[] { "complete", "partial", "pending" }));
    }

    [Fact]
    public void CatalogHasAReadOnlyDiagnosticsRoute()
    {
        Assert.Equal("/migration/fitout", EcomAeRoutes.MigrationFitOut);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("EcomAeRoutes.MigrationFitOut", program, StringComparison.Ordinal);
    }

    [Fact]
    public void PhaseOneExposesEstimateAndBoqWriteContracts()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutEstimateWriteService.cs"));
        var deliveryService = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutDeliveryRecordWriteService.cs"));
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        var projectAccounting = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpProjectAccountingApp.razor"));

        Assert.Contains("ErpFitOutEstimateSave = \"/erp/fitout/estimates/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutBoqLineSave = \"/erp/fitout/boq-lines/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutContractTermsSave = \"/erp/fitout/contracts/terms/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutCostCodeSave = \"/erp/fitout/cost-codes/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutQuotationSave = \"/erp/fitout/quotations/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/procurement-links/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/three-way-matches/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/material-movements/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/delivery-records/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/project-pnl\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/estimates/csv\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/delivery-dashboard\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/recovery-summary\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/lead-handoff/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/executive-dashboard\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/approval-queue\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/operations-report\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/finance-operations-report\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/approval/decide\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/invoice-bridge/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("Open project P&amp;L / forecast JSON", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("forecastMarginPercent", module, StringComparison.Ordinal);
        Assert.Contains("actualMarginPercent", module, StringComparison.Ordinal);
        Assert.Contains("Read-only project P&L projection", module, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/invoice-bridge\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/invoice-reconciliation\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/commercial-reconciliation\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/contract-closure\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/retention-ageing\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/proforma-receipts\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/approval-audit\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/subcontract-reconciliation\"", routes, StringComparison.Ordinal);
        Assert.Contains("\"/erp/fitout/acceptance-evidence\"", routes, StringComparison.Ordinal);
        Assert.Equal("partial", FitOutDeliveryCatalog.Steps.Single(step => step.Id == "P1-01").Status);
        Assert.Equal("partial", FitOutDeliveryCatalog.Steps.Single(step => step.Name.Contains("dashboards", StringComparison.OrdinalIgnoreCase)).Status);
        Assert.Equal("partial", FitOutDeliveryCatalog.Steps.Single(step => step.Name.StartsWith("Sales,", StringComparison.Ordinal)).Status);
        Assert.Equal("partial", FitOutDeliveryCatalog.Steps.Single(step => step.Name.StartsWith("Inventory,", StringComparison.Ordinal)).Status);
        Assert.Equal("EST", ErpVoucherNumberService.NormalizeType("est"));
        Assert.Equal("QUO", ErpVoucherNumberService.NormalizeType("quo"));
        Assert.Contains("ecomae_fitout_lead_handoffs", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutLeadHandoffWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("equipment_usage", deliveryService, StringComparison.Ordinal);
        Assert.Contains("timesheet", deliveryService, StringComparison.Ordinal);
        Assert.Contains("subcontract_payment_certificate", deliveryService, StringComparison.Ordinal);
        Assert.Contains("work_completion_certificate", deliveryService, StringComparison.Ordinal);
        Assert.Contains("client_payment_certificate", deliveryService, StringComparison.Ordinal);
        Assert.Contains("supplier_rfq", deliveryService, StringComparison.Ordinal);
        Assert.Contains("subcontractor_progress_claim", deliveryService, StringComparison.Ordinal);
        Assert.Contains("client_progress_claim", deliveryService, StringComparison.Ordinal);
        Assert.Contains("vendor_bill", deliveryService, StringComparison.Ordinal);
        Assert.Contains("payment_voucher", deliveryService, StringComparison.Ordinal);
        Assert.Contains("retention_release", deliveryService, StringComparison.Ordinal);
        Assert.Contains("site_engineer_approval", deliveryService, StringComparison.Ordinal);
        Assert.Contains("project_manager_approval", deliveryService, StringComparison.Ordinal);
        Assert.Contains("variation_approval", deliveryService, StringComparison.Ordinal);
        Assert.Contains("final_settlement", deliveryService, StringComparison.Ordinal);
        var executiveDashboard = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutExecutiveDashboardReadService.cs"));
        Assert.Contains("'site_engineer_approval','project_manager_approval'", executiveDashboard, StringComparison.Ordinal);
        Assert.Contains("'retention_release','vendor_bill','payment_voucher'", executiveDashboard, StringComparison.Ordinal);
        Assert.Contains("'subcontract_payment_certificate','client_payment_certificate'", executiveDashboard, StringComparison.Ordinal);
        Assert.Contains("client_certified_amount", executiveDashboard, StringComparison.Ordinal);
        Assert.Contains("subcontract_certified_amount", executiveDashboard, StringComparison.Ordinal);
        var operations = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutOperationsReportReadService.cs"));
        Assert.Contains("delivery', 'approved_records", operations, StringComparison.Ordinal);
        Assert.Contains("delivery', 'pending_approvals", operations, StringComparison.Ordinal);
        Assert.Contains("delivery', 'retention_ledger", operations, StringComparison.Ordinal);
        var financeOperations = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutFinanceOperationsReportReadService.cs"));
        Assert.Contains("client_certifications", financeOperations, StringComparison.Ordinal);
        Assert.Contains("subcontract_certifications", financeOperations, StringComparison.Ordinal);
        Assert.Contains("retention_releases", financeOperations, StringComparison.Ordinal);
        Assert.Contains("approved_payment_vouchers", financeOperations, StringComparison.Ordinal);
        Assert.Contains("'variation_approval','final_settlement'", deliveryService, StringComparison.Ordinal);
        Assert.Contains("'retention_release','vendor_bill','payment_voucher'", deliveryService, StringComparison.Ordinal);
        Assert.Contains("guardedApprovalRecord", deliveryService, StringComparison.Ordinal);
        Assert.Contains("draftApprovalRecord", deliveryService, StringComparison.Ordinal);
        Assert.Contains("ecomae_fitout_approval_audit", deliveryService, StringComparison.Ordinal);
        Assert.Contains("decided_at_utc", deliveryService, StringComparison.Ordinal);
        Assert.Contains("ecomae_fitout_retention_ledger", deliveryService, StringComparison.Ordinal);
        Assert.Contains("entry_type", deliveryService, StringComparison.Ordinal);
        var closure = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutContractClosureReadService.cs"));
        Assert.Contains("CanClose", closure, StringComparison.Ordinal);
        Assert.Contains("No approved final settlement exists", closure, StringComparison.Ordinal);
        Assert.Contains("Certified subcontract work remains unpaid", closure, StringComparison.Ordinal);
        Assert.Contains("Measured subcontract work exceeds ordered value", closure, StringComparison.Ordinal);
        Assert.Contains("IErpFitOutSubcontractReconciliationReadService", closure, StringComparison.Ordinal);
        Assert.Contains("IErpFitOutContractClosureReadService", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
        var ageing = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutRetentionAgeingReadService.cs"));
        Assert.Contains("ReleaseEligible", ageing, StringComparison.Ordinal);
        Assert.Contains("warranty_months", ageing, StringComparison.Ordinal);
        Assert.Contains("client_payment_certificate", ageing, StringComparison.Ordinal);
        var recovery = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutRecoverySummaryReadService.cs"));
        Assert.Contains("ClientCertifiedAmount", recovery, StringComparison.Ordinal);
        Assert.Contains("SubcontractCertifiedAmount", recovery, StringComparison.Ordinal);
        Assert.Contains("status`='approved'", recovery, StringComparison.Ordinal);
        Assert.Contains("clientCertified * advancePercent", recovery, StringComparison.Ordinal);
        var receipts = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutProformaReceiptReadService.cs"));
        Assert.Contains("sales_invoice_id", receipts, StringComparison.Ordinal);
        Assert.Contains("TotalOutstanding", receipts, StringComparison.Ordinal);
        Assert.Contains("receipt_count", receipts, StringComparison.Ordinal);
        Assert.Contains("last_receipt_time", receipts, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutProformaReceiptInvoiceRow", receipts, StringComparison.Ordinal);
        Assert.Contains("GroupBy(row => row.Stage", receipts, StringComparison.Ordinal);
        var reconciliation = File.ReadAllText(
            FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutCommercialReconciliationReadService.cs"));
        Assert.Contains("record_type`='retention_release'", reconciliation, StringComparison.Ordinal);
        Assert.Contains("status`='approved'", reconciliation, StringComparison.Ordinal);
        Assert.Contains("ApprovedVariationAmount", reconciliation, StringComparison.Ordinal);
        Assert.Contains("ClientCertifiedAmount", reconciliation, StringComparison.Ordinal);
        Assert.Contains("SubcontractCertifiedAmount", reconciliation, StringComparison.Ordinal);
        Assert.Contains(
            "'progress_claim','client_progress_claim','client_payment_certificate'",
            reconciliation,
            StringComparison.Ordinal);
        Assert.Contains("totals.ClientCertified - clientBilled", reconciliation, StringComparison.Ordinal);
        var invoiceReconciliation = File.ReadAllText(
            FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutInvoiceReconciliationReadService.cs"));
        Assert.Contains("client_payment_certificate", invoiceReconciliation, StringComparison.Ordinal);
        Assert.Contains("status`='approved'", invoiceReconciliation, StringComparison.Ordinal);
        Assert.Contains("ReadCertifiedAsync", invoiceReconciliation, StringComparison.Ordinal);
        Assert.Contains("certified.Client - invoiced.ExVat", invoiceReconciliation, StringComparison.Ordinal);
        Assert.Contains(
            "approved client/subcontract certification-to-invoice reconciliation",
            File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs")),
            StringComparison.Ordinal);
        var erpModule = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("approved client/subcontract certification", erpModule, StringComparison.Ordinal);
        Assert.Contains("variation_approval", reconciliation, StringComparison.Ordinal);
        Assert.Contains("client_payment_certificate", reconciliation, StringComparison.Ordinal);
        Assert.Contains("record_type`='vendor_bill'", reconciliation, StringComparison.Ordinal);
        Assert.Contains("record_type`='payment_voucher'", reconciliation, StringComparison.Ordinal);
        var audit = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutApprovalAuditReadService.cs"));
        Assert.Contains("ecomae_fitout_approval_audit", audit, StringComparison.Ordinal);
        Assert.Contains("decided_at_utc", audit, StringComparison.Ordinal);
        var subcontract = File.ReadAllText(
            FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutSubcontractReconciliationReadService.cs"));
        Assert.Contains("UncertifiedAmount", subcontract, StringComparison.Ordinal);
        Assert.Contains("subcontract_payment_certificate", subcontract, StringComparison.Ordinal);
        Assert.Contains("subcontractor_id`>0", subcontract, StringComparison.Ordinal);
        var approvalQueue = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutApprovalQueueReadService.cs"));
        Assert.Contains("IErpFitOutApprovalQueueReadService", approvalQueue, StringComparison.Ordinal);
        Assert.Contains("'variation_approval','final_settlement'", approvalQueue, StringComparison.Ordinal);
        Assert.Contains("IErpFitOutApprovalQueueReadService", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_estimates`", service, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_boq_lines`", service, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutContractTermsWriteService", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutContractTermsWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_cost_codes`", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutCostCodeWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_quotations`", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutQuotationWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_procurement_links`", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutProcurementLinkWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_three_way_matches`", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutThreeWayMatchWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_material_movements`", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutMaterialMovementWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_delivery_records`", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutDeliveryRecordWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("HandleFitOutEstimateSaveAsync", module, StringComparison.Ordinal);
        Assert.Contains("HandleFitOutBoqLineSaveAsync", module, StringComparison.Ordinal);
        Assert.Contains(
            "IErpFitOutInvoiceBridgeWriteService",
            File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE TABLE IF NOT EXISTS `ecomae_fitout_invoice_links`",
            File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFitOutInvoiceBridgeWriteService.cs")),
            StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/fitout/invoice-bridge/save\"", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("Fit-out VAT / invoice bridge", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/fitout/invoice-bridge\"", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/fitout/invoice-reconciliation\"", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/fitout/commercial-reconciliation\"", projectAccounting, StringComparison.Ordinal);
        Assert.Contains(
            "IErpFitOutCommercialReconciliationReadService",
            File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs")),
            StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/fitout/acceptance-evidence\"", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("work_completion_certificate", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("subcontract_payment_certificate", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("client_payment_certificate", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("supplier_rfq", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("subcontractor_progress_claim", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("client_progress_claim", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("vendor_bill", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("payment_voucher", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("retention_release", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("site_engineer_approval", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("project_manager_approval", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("variation_approval", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("final_settlement", projectAccounting, StringComparison.Ordinal);
        Assert.Contains("Approval-controlled records remain pending", projectAccounting, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovalThresholdAutoApprovesRequestsAtOrBelowPolicy()
    {
        Assert.Equal(
            "approved",
            ErpFitOutApprovalPolicy.ResolveInitialStatus(
                "approval_request",
                1000m,
                1000m));
        Assert.Equal(
            "approved",
            ErpFitOutApprovalPolicy.ResolveInitialStatus(
                "approval_request",
                900m,
                1000m));
        Assert.Equal(
            "pending",
            ErpFitOutApprovalPolicy.ResolveInitialStatus(
                "approval_request",
                1000.01m,
                1000m));
        Assert.Equal(
            "pending",
            ErpFitOutApprovalPolicy.ResolveInitialStatus(
                "approval_request",
                900m,
                null));
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
