using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpWmsAssignmentParityTests
{
    [Fact]
    public void WmsPageOffersGuardedAssignmentAction()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpWarehouseWmsApp.razor"));
        Assert.Contains("/erp/wms/work/assign", page, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", page, StringComparison.Ordinal);
        Assert.Contains("name=\"assignee\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AssignmentServiceMatchesPhpTransition()
    {
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpWmsWorkAssignWriteService.cs"));
        Assert.Contains("assigned_to", service, StringComparison.Ordinal);
        Assert.Contains("status`='open','in_progress'", service, StringComparison.Ordinal);
        Assert.Contains("status`<>'closed'", service, StringComparison.Ordinal);
        Assert.Contains("epc_wms_work_assign", service, StringComparison.Ordinal);
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
