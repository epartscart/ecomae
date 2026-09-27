using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpLegacyAliasRouteTests
{
    [Theory]
    [InlineData("CpApiClientsApp.razor", "/cp/api-documentation-app")]
    [InlineData("CpGuidesHubApp.razor", "/cp/guideline-app")]
    [InlineData("CpIntegrationsApp.razor", "/cp/integrations-hub-app")]
    [InlineData("CpMarketplaceAppsApp.razor", "/cp/marketplace-app")]
    [InlineData("CpMobileAppsApp.razor", "/cp/mobile-app")]
    [InlineData("CpStoragesApp.razor", "/cp/warehouses-app")]
    public void LegacyCpAliasUsesAnImplementedAspNetPage(string page, string route)
    {
        var text = File.ReadAllText(FindRepoFile($"aspnet/src/EcomAE.Platform/Components/Pages/{page}"));

        Assert.Contains($"@page \"{route}\"", text, StringComparison.Ordinal);
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
