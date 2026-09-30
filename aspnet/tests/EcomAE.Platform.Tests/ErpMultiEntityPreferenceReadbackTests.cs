using System.Reflection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpMultiEntityPreferenceReadbackTests
{
    [Fact]
    public void MultiEntityPage_ReadsAndPrefillsPersistedPreference()
    {
        var page = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpMultiEntityApp.razor"));
        var service = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Erp/ErpMultiEntityPreferenceReadService.cs"));
        var program = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Program.cs"));

        Assert.Contains("IErpMultiEntityPreferenceReadService", page, StringComparison.Ordinal);
        Assert.Contains("Preferences.LoadAsync", page, StringComparison.Ordinal);
        Assert.Contains("selected=\"@_crossTenantEnabled\"", page, StringComparison.Ordinal);
        Assert.Contains("multi_entity_enabled", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_platform_settings", service, StringComparison.Ordinal);
        Assert.Contains("IErpMultiEntityPreferenceReadService", program, StringComparison.Ordinal);
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
