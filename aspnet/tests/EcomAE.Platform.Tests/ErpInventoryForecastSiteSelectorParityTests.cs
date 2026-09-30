using System.Reflection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpInventoryForecastSiteSelectorParityTests
{
    [Fact]
    public void InventoryForecast_UsesPersistedMemberSitesWithTextFallback()
    {
        var page = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpInventoryForecastApp.razor"));
        var sql = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));

        Assert.Contains("IErpMultiEntityMemberReadService", page, StringComparison.Ordinal);
        Assert.Contains("Members.LoadAsync", page, StringComparison.Ordinal);
        Assert.Contains("name=\"siteKey\"", page, StringComparison.Ordinal);
        Assert.Contains("_memberOptions.Count == 0", page, StringComparison.Ordinal);
        Assert.Contains("row.SiteKey", page, StringComparison.Ordinal);
        Assert.Contains("IFNULL(`site_key`,'') AS site_key", sql, StringComparison.Ordinal);
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

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
