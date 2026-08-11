using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// www.ecomae.com product HTML must not emit active .php entrypoints.
/// PHP remains compare-only under /php-reference/*.
/// </summary>
public sealed class NoProductPhpMarketingTests
{
    [Fact]
    public void LegacyAssetBridge_HasNoMergeConflictMarkers()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.DoesNotContain("<<<<<<<", text, StringComparison.Ordinal);
        Assert.DoesNotContain(">>>>>>>", text, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_cp_aspnet_module_parity.css", text, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_erp_aspnet_module_parity.css", text, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_orders_cp.css", text, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/marketing_screens/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassicEntryNginx_ProxiesProductPhpFrontControllersToKestrel()
    {
        var text = File.ReadAllText(FindRepoFile("deploy/aspnet/nginx-classic-entry-aspnet-primary-shadow-example.conf"));
        Assert.Contains("location = /index.php", text, StringComparison.Ordinal);
        Assert.Contains("location = /epc-blockchain-verify.php", text, StringComparison.Ordinal);
        Assert.Contains("location = /blockchain/verify", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StopProductPhpPack_BlocksIndexAndVerifyPhpFpm()
    {
        var text = File.ReadAllText(FindRepoFile("scripts/cloudpanel_STOP_PRODUCT_PHP_NOW.sh"));
        Assert.Contains("location = /index.php", text, StringComparison.Ordinal);
        Assert.Contains("location = /epc-blockchain-verify.php", text, StringComparison.Ordinal);
        Assert.Contains("stop-product-php-index-php", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockchainVerifyApp_ExistsAtAspNetRoute()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/BlockchainVerifyApp.razor"));
        Assert.Contains("@page \"/blockchain/verify\"", text, StringComparison.Ordinal);
        Assert.Contains("Verify a business proof", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AspNetPrimaryHref_MapsIndexPhpToHome()
    {
        Assert.Equal("/", PhpSurfaceLinkMap.AspNetPrimaryHref("/index.php"));
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
