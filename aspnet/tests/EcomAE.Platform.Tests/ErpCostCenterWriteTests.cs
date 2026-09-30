using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCostCenterWriteTests
{
    [Fact]
    public void Allocation_reconciles_rounding_residual_to_largest_weight()
    {
        var allocation = ErpCostCenterWriteService.Allocate(
            100m,
            new Dictionary<long, decimal>
            {
                [12] = 2m,
                [15] = 1m,
            });

        Assert.Equal(66.67m, allocation[12]);
        Assert.Equal(33.33m, allocation[15]);
        Assert.Equal(100m, allocation.Values.Sum());
    }

    [Fact]
    public void Allocation_ignores_non_positive_weights()
    {
        var allocation = ErpCostCenterWriteService.Allocate(
            100m,
            new Dictionary<long, decimal>
            {
                [12] = 0m,
                [15] = -1m,
            });

        Assert.Empty(allocation);
    }

    [Fact]
    public void ParseWeights_matches_php_center_pipe_weight_format()
    {
        var parsed = ErpCostCenterWriteService.ParseWeights("12|2\n\nbad\n15|1.5\n|9");

        Assert.Equal(2m, parsed[12]);
        Assert.Equal(1.5m, parsed[15]);
        Assert.Equal(2, parsed.Count);
    }

    [Fact]
    public void Route_and_page_expose_guarded_cost_center_operations()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpFinAdvancedApp.razor"));

        Assert.Contains("/erp/fin/cost-centers/write", routes, StringComparison.Ordinal);
        Assert.Contains("HandleCostCenterWriteAsync", module, StringComparison.Ordinal);
        Assert.Contains("IErpCostCenterWriteService", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
        Assert.Contains("post_allocation", page, StringComparison.Ordinal);
        Assert.Contains("Rounding residuals go to the largest weight", page, StringComparison.Ordinal);
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

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
