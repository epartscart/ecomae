using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpLandedCostWritePhpParityTests
{
    [Theory]
    [InlineData("value", 100, 10, 100)]
    [InlineData("weight", 2, 10, 20)]
    [InlineData("volume", 3, 10, 30)]
    [InlineData("quantity", 2, 10, 10)]
    [InlineData("qty", 2, 10, 10)]
    [InlineData("equal", 99, 10, 1)]
    public void Basis_MatchesPhpDistributionMethods(string method, decimal lineValue, decimal qty, decimal expected)
    {
        var line = new ErpLandedCostWriteService.LandedCostLine(1, qty, 4, lineValue, 2, 3);

        Assert.Equal(expected, ErpLandedCostWriteService.Basis(method, line));
    }

    [Fact]
    public void RoutesAndPageExposeCalculateAndPostActions()
    {
        var repo = FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpLandedCostApp.razor");
        var page = File.ReadAllText(repo);
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));

        Assert.Contains("ErpLandedCostCalculate", routes, StringComparison.Ordinal);
        Assert.Contains("ErpLandedCostPost", routes, StringComparison.Ordinal);
        Assert.Contains("Calculate allocation", page, StringComparison.Ordinal);
        Assert.Contains("Post landed cost", page, StringComparison.Ordinal);
        Assert.Contains("IErpLandedCostWriteService", module, StringComparison.Ordinal);
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
