using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpTenantHubRouteTests
{
    [Fact]
    public void TenantHubAliasUsesTheImplementedTenantControlCenter()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantsApp.razor"));

        Assert.Contains("@page \"/cp/tenant-hub-app\"", text, StringComparison.Ordinal);
        Assert.Contains("Tenant control center", text, StringComparison.Ordinal);
        Assert.DoesNotContain("digest shell", text, StringComparison.OrdinalIgnoreCase);
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
