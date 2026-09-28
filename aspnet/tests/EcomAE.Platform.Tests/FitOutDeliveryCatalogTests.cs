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
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));

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
