using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpDashboardSummaryPresentationTests
{
    [Fact]
    public void Super_cp_dashboard_summary_has_php_shaped_command_workspace()
    {
        var source = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpDashboardSummaryApp.razor"));

        Assert.Contains("epc-cp-summary__hero", source, StringComparison.Ordinal);
        Assert.Contains("epc-cp-summary__kpis", source, StringComparison.Ordinal);
        Assert.Contains("epc-cp-summary__workspace", source, StringComparison.Ordinal);
        Assert.Contains("Platform activity", source, StringComparison.Ordinal);
        Assert.Contains("Operator destinations", source, StringComparison.Ordinal);
        Assert.Contains("MetricWidth", source, StringComparison.Ordinal);
        Assert.Contains("Classic twin", source, StringComparison.Ordinal);
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

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
