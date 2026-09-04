using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Every PHP CP / ERP operator guide is present, complete, and mapped.</summary>
[Collection(PreferAspNetAppsCollection.Name)]
public sealed class OperatorGuidesParityTests
{
    public OperatorGuidesParityTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
    }

    [Fact]
    public void CatalogContainsEveryRequiredPhpGuide()
    {
        foreach (var key in OperatorGuidesCatalog.RequiredKeys)
        {
            var guide = OperatorGuidesCatalog.Get(key);
            Assert.NotNull(guide);
            Assert.False(string.IsNullOrWhiteSpace(guide!.Title));
            Assert.False(string.IsNullOrWhiteSpace(guide.PhpPath));
            Assert.StartsWith("/cp/guides-app?g=", guide.Href, StringComparison.Ordinal);
            Assert.True(guide.Chapters.Count >= 3, $"{key} must have complete PHP chapters, got {guide.Chapters.Count}");
            Assert.All(guide.Chapters, ch =>
            {
                Assert.False(string.IsNullOrWhiteSpace(ch.Title));
                Assert.True(ch.Steps.Count >= 1, $"{key} / {ch.Title} has no steps");
            });
        }
    }

    [Fact]
    public void HubRazorIsStaticGetOnly()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpGuidesHubApp.razor"));
        Assert.Contains("@page \"/cp/guides-app\"", src);
        Assert.Contains("@page \"/cp/shop/prices/guide\"", src);
        Assert.Contains("@page \"/cp/shop/orders/oms-guide\"", src);
        Assert.Contains("@page \"/cp/shop/orders/guide\"", src);
        Assert.Contains("@page \"/cp/shop/orders/whatsapp-guide\"", src);
        Assert.Contains("@page \"/cp/control/cp-guideline\"", src);
        Assert.Contains("@page \"/cp/control/portal/epc_super_cp_operator_guide\"", src);
        Assert.Contains("OperatorGuidesCatalog", src);
        Assert.Contains("PhpCpDesktopChrome", src);
        Assert.DoesNotContain("@onclick", src);
        Assert.DoesNotContain("@oninput", src);
        Assert.DoesNotContain("ASP.NET", src);
        Assert.DoesNotContain("href=\"/php-reference/", src);
    }

    [Fact]
    public void ErpGuideAppHasBooksAndGetSearch()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpGuideApp.razor"));
        Assert.Contains("@page \"/erp/guide-app\"", src);
        Assert.Contains("@page \"/erp/guide/howto\"", src);
        Assert.Contains("@page \"/erp/guide/full\"", src);
        Assert.Contains("@page \"/erp/guide/advanced\"", src);
        Assert.Contains("@page \"/erp/guide/erp-only\"", src);
        Assert.Contains("@page \"/erp/guide/customs\"", src);
        Assert.Contains("@page \"/cp/shop/finance/erp/guide\"", src);
        Assert.Contains("ErpGuideBooks", src);
        Assert.Contains("method=\"get\"", src);
        Assert.DoesNotContain("@oninput", src);
        Assert.DoesNotContain("@onclick", src);
        Assert.DoesNotContain("ASP.NET", src);
        Assert.DoesNotContain("href=\"/php-reference/", src);
    }

    [Fact]
    public void ErpBooksCoverPhpGuideSet()
    {
        Assert.Equal(6, ErpGuideBooks.All.Count);
        Assert.True(ErpGuideBooks.Get(ErpGuideBooks.Howto).Chapters.Count >= 8);
        Assert.True(ErpGuideBooks.Get(ErpGuideBooks.Full).Chapters.Count >= 3);
        Assert.True(ErpGuideBooks.Get(ErpGuideBooks.Advanced).Chapters.Count >= 6);
        Assert.True(ErpGuideBooks.Get(ErpGuideBooks.ErpOnly).Chapters.Count >= 5);
        Assert.True(ErpGuideBooks.Get(ErpGuideBooks.Customs).Chapters.Count >= 4);
        Assert.True(ErpGuideCatalog.All.Count >= 70);
    }

    [Theory]
    [InlineData("/CP/shop/orders/oms-guide", "/cp/guides-app?g=oms-daily")]
    [InlineData("/CP/shop/orders/whatsapp-guide", "/cp/guides-app?g=whatsapp")]
    [InlineData("/CP/shop/orders/guide", "/cp/guides-app?g=fulfilment")]
    [InlineData("/CP/shop/logistics/guide", "/cp/guides-app?g=logistics")]
    [InlineData("/CP/shop/logistics/whatsapp-guide", "/cp/guides-app?g=whatsapp")]
    [InlineData("/CP/shop/payments/guide", "/cp/guides-app?g=payments")]
    [InlineData("/CP/shop/channels/guide", "/cp/guides-app?g=channels")]
    [InlineData("/CP/shop/procurement/procurement_guide", "/cp/guides-app?g=procurement")]
    [InlineData("/CP/shop/prices/guide", "/cp/guides-app?g=prices-upload")]
    [InlineData("/CP/shop/prices?view=guide", "/cp/guides-app?g=prices-upload")]
    [InlineData("/CP/control/cp-guideline", "/cp/guides-app?g=cp-guideline")]
    [InlineData("/CP/control/portal/epc_api_documentation_guide", "/cp/guides-app?g=api-docs")]
    [InlineData("/CP/control/portal/epc_auto_price_guide", "/cp/guides-app?g=auto-price")]
    [InlineData("/CP/control/portal/epc_autoworkshop_guide", "/cp/guides-app?g=workshop")]
    [InlineData("/CP/control/portal/epc_custom_shipping_guide", "/cp/guides-app?g=custom-shipping")]
    [InlineData("/CP/control/portal/epc_erp_only_onboard_guide", "/cp/guides-app?g=erp-only-onboard")]
    [InlineData("/CP/control/portal/epc_integrations_guide", "/cp/guides-app?g=integrations")]
    [InlineData("/CP/control/portal/epc_platform_failover_guide", "/cp/guides-app?g=failover")]
    [InlineData("/CP/control/portal/epc_power_bi_guide", "/cp/guides-app?g=power-bi")]
    [InlineData("/CP/control/portal/epc_super_cp_operator_guide", "/cp/guides-app?g=super-cp-operator")]
    [InlineData("/CP/shop/customer_mgmt/customer_mgmt_guide", "/cp/guides-app?g=customer-mgmt")]
    [InlineData("/CP/shop/document_control/document_control_guide", "/cp/guides-app?g=document-control")]
    [InlineData("/CP/shop/finance/erp/guide", "/erp/guide-app?book=howto")]
    [InlineData("/CP/shop/finance/erp/erp_full_guide", "/erp/guide-app?book=full")]
    [InlineData("/CP/shop/finance/erp/erp_advanced_guide", "/erp/guide-app?book=advanced")]
    [InlineData("/CP/shop/finance/erp/erp_only_operator_guide", "/erp/guide-app?book=erp-only")]
    [InlineData("/CP/shop/finance/erp/custom_shipping/custom_shipping_guide", "/erp/guide-app?book=customs")]
    public void PhpGuidePathsMapToHubOrErpBook(string php, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.MapCpPhpPath(php));
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(php));
    }

    [Fact]
    public void GuidePathsDoNotCollapseToModuleHubs()
    {
        Assert.NotEqual("/cp/orders", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/orders/oms-guide"));
        Assert.NotEqual("/cp/orders", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/orders/whatsapp-guide"));
        Assert.NotEqual("/cp/orders", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/orders/guide"));
        Assert.NotEqual("/cp/ops-guides-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/prices/guide"));
        Assert.NotEqual("/cp/price-lists-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/prices/guide"));
        Assert.NotEqual("/cp/delivery-methods-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/logistics/guide"));
        Assert.NotEqual("/cp/payment-gateways-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/payments/guide"));
        Assert.NotEqual("/cp/marketplace-channels-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/channels/guide"));
        Assert.NotEqual("/cp/ops-guides-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/control/portal/epc_erp_only_onboard_guide"));
    }

    [Fact]
    public void ErpTabGuideStillMapsToModuleBook()
    {
        Assert.Equal(
            "/erp/guide-app",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=overview&tab=guide"));
    }

    [Fact]
    public void HubDoesNotLinkPhpReference()
    {
        var hub = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpGuidesHubApp.razor"));
        var erp = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpGuideApp.razor"));
        Assert.DoesNotContain("/php-reference/", hub);
        Assert.DoesNotContain("/php-reference/", erp);
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

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
