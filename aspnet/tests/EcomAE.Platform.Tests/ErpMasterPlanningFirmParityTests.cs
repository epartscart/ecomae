using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpMasterPlanningFirmParityTests
{
    [Fact]
    public void MasterPlanningPageOffersGuardedFirmAction()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpOrderPlanningApp.razor"));
        Assert.Contains("ErpMasterPlanningFirm", page, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", page, StringComparison.Ordinal);
        Assert.Contains("row.Status, \"planned\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void FirmServiceKeepsMrpGenerationOutsideAspNetOwnership()
    {
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpMasterPlannedOrderFirmWriteService.cs"));
        Assert.Contains("epc_mfg_planned", service, StringComparison.Ordinal);
        Assert.Contains("source`='mrp'", service, StringComparison.Ordinal);
        Assert.Contains("status`='planned'", service, StringComparison.Ordinal);
        Assert.Contains("MRP generation remains PHP-owned", service, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var candidate = Path.Combine(directory, relative);
            if (File.Exists(candidate))
                return candidate;
            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new FileNotFoundException(relative);
    }
}
