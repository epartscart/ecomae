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
        Assert.Contains(FitOutDeliveryCatalog.Steps, step => step.Name == "Approved variations and contract value revision");
        Assert.Contains(FitOutDeliveryCatalog.Steps, step => step.Name == "Progress claim and certification");
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
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_estimates`", service, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `ecomae_fitout_boq_lines`", service, StringComparison.Ordinal);
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
