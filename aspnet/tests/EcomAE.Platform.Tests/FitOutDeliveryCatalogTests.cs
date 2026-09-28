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
