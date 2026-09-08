using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection(PreferAspNetAppsCollection.Name)]
public sealed class StorefrontCatalogDedicatedAppsTests : IDisposable
{
    public StorefrontCatalogDedicatedAppsTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
    }

    public void Dispose()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
    }

    [Fact]
    public void DedicatedCatalogAppsAreNotHomepageHashes()
    {
        Assert.Equal("/storefront/umapi-catalog-app", StorefrontAspNetCanonical.UmapiCatalog);
        Assert.Equal("/storefront/product-family-app", StorefrontAspNetCanonical.ProductFamily);
        Assert.Equal("/storefront/available-brands-app", StorefrontAspNetCanonical.AvailableBrands);
        Assert.Equal("/storefront/available-brands-app", StorefrontAspNetCanonical.PartsInStock);
        Assert.Equal("/storefront/original-catalog-app", StorefrontAspNetCanonical.OriginalCatalog);
        Assert.Equal("/storefront/original-catalog-app", StorefrontAspNetCanonical.LevamOem);
        Assert.Equal("/storefront/eparts-cata-app", StorefrontAspNetCanonical.EpartsCata);
        Assert.Equal("/storefront/eparts-cata-app", StorefrontAspNetCanonical.PartsApiCatalog);
        Assert.Equal("/storefront/eparts-mod-app", StorefrontAspNetCanonical.EpartsMod);
        Assert.Equal("/storefront/ucats-app", StorefrontAspNetCanonical.UcatsService);
        Assert.Equal("/storefront/demand-intelligence-app", StorefrontAspNetCanonical.DemandIntelligence);
        Assert.DoesNotContain("#epc-", StorefrontAspNetCanonical.UmapiCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("#epc-", StorefrontAspNetCanonical.ProductFamily, StringComparison.Ordinal);
        Assert.DoesNotContain("#epc-", StorefrontAspNetCanonical.UcatsService, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/en/umapi_catalog", "/storefront/umapi-catalog-app")]
    [InlineData("/en/product-family", "/storefront/product-family-app")]
    [InlineData("/en/available-brands", "/storefront/available-brands-app")]
    [InlineData("/en/original-catalog", "/storefront/original-catalog-app")]
    [InlineData("/en/eparts-cata", "/storefront/eparts-cata-app")]
    [InlineData("/en/eparts-mod", "/storefront/eparts-mod-app")]
    [InlineData("/en/shop/katalogi-ucats", "/storefront/ucats-app")]
    [InlineData("/en/shop/katalogi-ucats/shiny", "/storefront/ucats-app")]
    [InlineData("/en/demand-intelligence", "/storefront/demand-intelligence-app")]
    [InlineData("/en/partsapi-catalog", "/storefront/eparts-cata-app")]
    [InlineData("/en/levam-oem", "/storefront/original-catalog-app")]
    public void PreferAspNetCatalogBrowseMapsToDedicatedApps(string phpPath, string expected)
    {
        Assert.Equal(expected, StorefrontSurfaceLinks.ForCatalogBrowse(phpPath));
    }

    [Theory]
    [InlineData("/en/umapi_catalog")]
    [InlineData("/en/product-family")]
    [InlineData("/en/available-brands")]
    [InlineData("/en/original-catalog")]
    [InlineData("/en/eparts-cata")]
    [InlineData("/en/eparts-mod")]
    [InlineData("/en/shop/katalogi-ucats")]
    [InlineData("/en/shop/katalogi-ucats/shiny")]
    [InlineData("/en/demand-intelligence")]
    [InlineData("/en/accessories-spare-parts")]
    [InlineData("/en/parts")]
    [InlineData("/en/vehicle-catalog")]
    [InlineData("/en/katalog-laximo")]
    public void IncomingPhpCatalogPathsStayOnBlazorSameUrl(string incoming)
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(incoming, out _));
    }

    [Fact]
    public void UcatsHubHasEightPhpCatalogues()
    {
        Assert.Equal(8, StorefrontUcatsCatalog.Cards.Count);
        Assert.NotNull(StorefrontUcatsCatalog.Find("shiny"));
        Assert.NotNull(StorefrontUcatsCatalog.Find("akkumulyatory"));
        Assert.NotNull(StorefrontUcatsCatalog.Find("kolesnye-gajki-bolty-prostavki"));
    }

    [Fact]
    public void CatalogAppsDeclarePhpSameUrlAliases()
    {
        AssertPage("StorefrontUmapiCatalogApp.razor", "/en/umapi_catalog", "/storefront/umapi-catalog-app");
        AssertPage("StorefrontProductFamilyApp.razor", "/en/product-family", "/storefront/product-family-app");
        AssertPage("StorefrontAvailableBrandsApp.razor", "/en/available-brands", "/storefront/available-brands-app");
        AssertPage("StorefrontOriginalCatalogApp.razor", "/en/original-catalog", "/storefront/original-catalog-app");
        AssertPage("StorefrontEpartsCataApp.razor", "/en/eparts-cata", "/storefront/eparts-cata-app");
        AssertPage("StorefrontEpartsModApp.razor", "/en/eparts-mod", "/storefront/eparts-mod-app");
        AssertPage("StorefrontUcatsHubApp.razor", "/en/shop/katalogi-ucats", "/storefront/ucats-app");
        AssertPage("StorefrontDemandIntelligenceApp.razor", "/en/demand-intelligence", "/storefront/demand-intelligence-app");
        AssertPage("StorefrontPreviewApp.razor", "/en", "/storefront/app");
        AssertPage("StorefrontAccessoriesApp.razor", "/en/accessories-spare-parts", "/storefront/accessories-app");
    }

    [Fact]
    public void OfficesPhpPathMapsToDedicatedAppNotDeliveryMethods()
    {
        Assert.Equal("/cp/offices-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/logistics/offices"));
        Assert.Equal("/cp/offices-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/logistics/offices"));
        Assert.NotEqual("/cp/delivery-methods-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/logistics/offices"));
        Assert.Equal("/cp/delivery-methods-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/logistics"));

        var delivery = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpDeliveryMethodsApp.razor"));
        Assert.Contains("class=\"hpanel\"", delivery, StringComparison.Ordinal);
        Assert.Contains("panel-heading hbuilt", delivery, StringComparison.Ordinal);
        Assert.Contains("table table-condensed table-striped", delivery, StringComparison.Ordinal);
        Assert.Contains("id=\"check_uncheck_all\"", delivery, StringComparison.Ordinal);
        Assert.Contains("/cp/delivery-methods/write", delivery, StringComparison.Ordinal);
        Assert.Contains("name=\"action\" value=\"activation\"", delivery, StringComparison.Ordinal);
        Assert.Contains("name=\"obtain_mode_id\"", delivery, StringComparison.Ordinal);
        Assert.Contains("name=\"activate_obtain_mode\"", delivery, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-del-kpis", delivery, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-del-hero", delivery, StringComparison.Ordinal);
    }

    [Fact]
    public void SmsAndTenantEmailApps_UseClassicHpanelNotInventHero()
    {
        var sms = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpSmsWhatsappApp.razor"));
        Assert.Contains("class=\"hpanel\"", sms, StringComparison.Ordinal);
        Assert.Contains("panel-heading hbuilt", sms, StringComparison.Ordinal);
        Assert.Contains("table table-condensed table-striped", sms, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", sms, StringComparison.Ordinal);
        Assert.Contains("/CP/control/sms_turning", sms, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", sms, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-sms-hero", sms, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-sms-kpis", sms, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", sms, StringComparison.Ordinal);

        var email = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantEmailApp.razor"));
        Assert.Contains("class=\"hpanel\"", email, StringComparison.Ordinal);
        Assert.Contains("well well-sm", email, StringComparison.Ordinal);
        Assert.Contains("This tenant", email, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not invent a send", email, StringComparison.Ordinal);
        Assert.Contains("/CP/control/portal/epc_tenant_email_settings", email, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperCpHostGate", email, StringComparison.Ordinal);
        Assert.DoesNotContain("Deploy targets", email, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", email, StringComparison.Ordinal);
    }

    [Fact]
    public void CommunicationsTestApp_UsesClassicCnChromeNotInventHero()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCommunicationsTestApp.razor"));
        Assert.Contains("epc-cn-hero", text, StringComparison.Ordinal);
        Assert.Contains("epc-cn-quick", text, StringComparison.Ordinal);
        Assert.Contains("epc-cn-card", text, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", text, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", text, StringComparison.Ordinal);
        Assert.Contains("/CP/control/communications", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-kpis", text, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=\"epcCnTest", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderStatusesApp_UsesClassicStatusesChromeNotInventHero()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpOrderStatusesApp.razor"));
        Assert.Contains("epc-statuses-page", text, StringComparison.Ordinal);
        Assert.Contains("epc-statuses-card", text, StringComparison.Ordinal);
        Assert.Contains("/cp/order-statuses/write", text, StringComparison.Ordinal);
        Assert.Contains("name=\"ordersJson\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"itemsJson\"", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-kpis", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CarriersApp_UsesClassicLogisticsChromeNotInventHero()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCarriersApp.razor"));
        Assert.Contains("epc-lc-brandbar", text, StringComparison.Ordinal);
        Assert.Contains("epc-lc-kpi", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-cs-save", text, StringComparison.Ordinal);
        Assert.Contains("/cp/custom-shipping/write", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-car-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-car-kpis", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NotificationsLanguagesIntegrationsApps_UseClassicChromeNotInventHero()
    {
        var notify = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpNotificationsApp.razor"));
        Assert.Contains("epc-cn-hero", notify, StringComparison.Ordinal);
        Assert.Contains("epc-cn-quick", notify, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", notify, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", notify, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w20-hero", notify, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w20-kpis", notify, StringComparison.Ordinal);

        var lang = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpLanguagesApp.razor"));
        Assert.Contains("class=\"hpanel\"", lang, StringComparison.Ordinal);
        Assert.Contains("panel-heading hbuilt", lang, StringComparison.Ordinal);
        Assert.Contains("/cp/lang/save-translation", lang, StringComparison.Ordinal);
        Assert.Contains("/cp/lang/create-string", lang, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", lang, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-kpis", lang, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", lang, StringComparison.Ordinal);

        var integrations = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpIntegrationsApp.razor"));
        Assert.Contains("epc-inthub-brand", integrations, StringComparison.Ordinal);
        Assert.Contains("epc-inthub-stats", integrations, StringComparison.Ordinal);
        Assert.Contains("/erp/integrations/events/raise", integrations, StringComparison.Ordinal);
        Assert.Contains("/erp/integrations/subscriptions/save", integrations, StringComparison.Ordinal);
        Assert.Contains("/erp/integrations/entities/save", integrations, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-int-hero", integrations, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-int-kpis", integrations, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperCpHostGate", integrations, StringComparison.Ordinal);
    }

    [Fact]
    public void GeoCurrenciesConfigSliderApps_UseClassicHpanelNotInventHero()
    {
        var geo = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpGeoRegionsApp.razor"));
        Assert.Contains("class=\"hpanel\"", geo, StringComparison.Ordinal);
        Assert.Contains("/cp/geo-regions/write", geo, StringComparison.Ordinal);
        Assert.Contains("name=\"treeJson\"", geo, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", geo, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-kpis", geo, StringComparison.Ordinal);

        var currencies = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCurrenciesApp.razor"));
        Assert.Contains("class=\"hpanel\"", currencies, StringComparison.Ordinal);
        Assert.Contains("/cp/currencies/set-rate", currencies, StringComparison.Ordinal);
        Assert.Contains("/cp/currencies/set-available", currencies, StringComparison.Ordinal);
        Assert.Contains("Classic twin", currencies, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP twin", currencies, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-cu-hero", currencies, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-cu-kpis", currencies, StringComparison.Ordinal);

        var config = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpConfigItemsApp.razor"));
        Assert.Contains("class=\"hpanel\"", config, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", config, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ci-hero", config, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ci-kpis", config, StringComparison.Ordinal);

        var slider = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpSliderBannersApp.razor"));
        Assert.Contains("class=\"hpanel\"", slider, StringComparison.Ordinal);
        Assert.Contains("/cp/slider-banners/write", slider, StringComparison.Ordinal);
        Assert.Contains("name=\"action\" value=\"setings\"", slider, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", slider, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-kpis", slider, StringComparison.Ordinal);
    }

    private static void AssertPage(string fileName, string phpAlias, string aspNetApp)
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Pages/" + fileName);
        var text = File.ReadAllText(path);
        Assert.Contains("@page \"" + phpAlias, text, StringComparison.Ordinal);
        Assert.Contains("@page \"" + aspNetApp, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sitemap_catalogue_and_brochure_apps_exist()
    {
        AssertPage("StorefrontSitemapApp.razor", "/en/sitemap", "/storefront/sitemap-app");
        AssertPage("StorefrontOwnCatalogApp.razor", "/en/shop/catalogue", "/storefront/own-catalog-app");
        AssertPage("StorefrontProductApp.razor", "/en/shop/product", "/storefront/product-app");
        var brochure = Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontBrochureApp.razor");
        var text = File.ReadAllText(brochure);
        Assert.Contains("@page \"/storefront/brochure-app\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/brochure\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
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
