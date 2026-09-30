using System.Reflection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpMultiEntityMemberSelectorParityTests
{
    [Fact]
    public void MultiEntityPage_UsesPersistedMemberSitesWithTextFallback()
    {
        var page = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpMultiEntityApp.razor"));
        var service = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Erp/ErpMultiEntityMemberReadService.cs"));

        Assert.Contains("IErpMultiEntityMemberReadService", page, StringComparison.Ordinal);
        Assert.Contains("Members.LoadAsync", page, StringComparison.Ordinal);
        Assert.Contains("name=\"siteKey\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"fromSiteKey\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"toSiteKey\"", page, StringComparison.Ordinal);
        Assert.Contains("_memberOptions.Count == 0", page, StringComparison.Ordinal);
        Assert.Contains("epc_entity_members", service, StringComparison.Ordinal);
        Assert.Contains("GROUP BY `site_key`", service, StringComparison.Ordinal);
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
