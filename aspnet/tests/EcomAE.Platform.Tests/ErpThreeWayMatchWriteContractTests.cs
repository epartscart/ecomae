using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpThreeWayMatchWriteContractTests
{
    [Fact]
    public void GeneralThreeWayMatchExposesServerSourcedDecisionContract()
    {
        var root = FindRepoRoot();
        var routes = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpThreeWayMatchWriteService.cs"));
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpThreeWayMatchApp.razor"));

        Assert.Contains("ErpThreeWayMatchSave = \"/erp/three-way-match/save\"", routes, StringComparison.Ordinal);
        Assert.Contains("epc_erp_purchase_orders", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_po_receipts", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_purchases", service, StringComparison.Ordinal);
        Assert.Contains("ecomae_erp_three_way_matches", service, StringComparison.Ordinal);
        Assert.Contains("PhpMatchToleranceAmount = 0.02m", service, StringComparison.Ordinal);
        Assert.DoesNotContain("request.ToleranceAmount", service, StringComparison.Ordinal);
        Assert.Contains("three_way_match", service, StringComparison.Ordinal);
        Assert.Contains("ErpCapabilityCatalog.CanAction(session, \"purchasing\", \"Approve\")", module, StringComparison.Ordinal);
        Assert.Contains("writesBlocked = true", module, StringComparison.Ordinal);
        Assert.Contains("/erp/three-way-match/save", page, StringComparison.Ordinal);
        Assert.Contains("Accept match", page, StringComparison.Ordinal);
        Assert.Contains("Route exception", page, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
