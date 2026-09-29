using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpMfgrMrpRunParityTests
{
    [Fact]
    public void MasterPlanningPageOffersGuardedRegenerationInputs()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpOrderPlanningApp.razor"));
        Assert.Contains("ErpAjaxMfgrMrpRun", page, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", page, StringComparison.Ordinal);
        Assert.Contains("demand", page, StringComparison.Ordinal);
        Assert.Contains("onHand", page, StringComparison.Ordinal);
    }

    [Fact]
    public void MRPWriteServicePreservesPhpRegenerationSemantics()
    {
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpMfgrMrpRunWriteService.cs"));
        Assert.Contains("DELETE FROM `epc_mfg_planned`", service, StringComparison.Ordinal);
        Assert.Contains("source`='mrp'", service, StringComparison.Ordinal);
        Assert.Contains("order_type", service, StringComparison.Ordinal);
        Assert.Contains("production", service, StringComparison.Ordinal);
        Assert.Contains("purchase", service, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", File.ReadAllText(FindRepoFile("content/shop/finance/epc_erp_mfg_routing.php")), StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingDryRunRemainsWhenConfirmationIsAbsent()
    {
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("ErpMfgrMrpRunDryRun", module, StringComparison.Ordinal);
        Assert.Contains("if (!body.ConfirmWrites)", module, StringComparison.Ordinal);
        Assert.Contains("invalid_json", module, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var candidate = Path.Combine(directory, relative);
            if (File.Exists(candidate)) return candidate;
            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }
        throw new FileNotFoundException(relative);
    }
}
