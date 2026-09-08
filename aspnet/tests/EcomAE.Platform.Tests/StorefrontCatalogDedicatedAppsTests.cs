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

    [Fact]
    public void ContentSearchFilterApps_UseClassicHpanelNotInventHero()
    {
        var texts = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpAdditionalTextsApp.razor"));
        Assert.Contains("class=\"hpanel\"", texts, StringComparison.Ordinal);
        Assert.Contains("/cp/additional-texts/write", texts, StringComparison.Ordinal);
        Assert.Contains("/cp/additional-texts/delete", texts, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", texts, StringComparison.Ordinal);

        var sitemap = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpSitemapApp.razor"));
        Assert.Contains("class=\"hpanel\"", sitemap, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", sitemap, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", sitemap, StringComparison.Ordinal);

        var tabs = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpSearchTabsApp.razor"));
        Assert.Contains("class=\"hpanel\"", tabs, StringComparison.Ordinal);
        Assert.Contains("/cp/search-tabs/write", tabs, StringComparison.Ordinal);
        Assert.Contains("name=\"action\" value=\"activation\"", tabs, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", tabs, StringComparison.Ordinal);

        var filters = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpProductFiltersApp.razor"));
        Assert.Contains("class=\"hpanel\"", filters, StringComparison.Ordinal);
        Assert.Contains("/cp/product-filters/write", filters, StringComparison.Ordinal);
        Assert.Contains("name=\"action\" value=\"save_storages\"", filters, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", filters, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginsTemplatesDumpsPriceListApps_UseClassicHpanelNotInventHero()
    {
        var plugins = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPluginsManagerApp.razor"));
        Assert.Contains("class=\"hpanel\"", plugins, StringComparison.Ordinal);
        Assert.Contains("HasStaffAccess", plugins, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", plugins, StringComparison.Ordinal);

        var templates = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpTemplatesManagerApp.razor"));
        Assert.Contains("class=\"hpanel\"", templates, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", templates, StringComparison.Ordinal);

        var dumps = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpStructureDumpsApp.razor"));
        Assert.Contains("class=\"hpanel\"", dumps, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", dumps, StringComparison.Ordinal);

        var prices = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPriceListsApp.razor"));
        Assert.Contains("class=\"hpanel\"", prices, StringComparison.Ordinal);
        Assert.Contains("/cp/prices/storage-rules", prices, StringComparison.Ordinal);
        Assert.Contains("save_storage_rule", prices, StringComparison.Ordinal);
        Assert.Contains("Classic twin", prices, StringComparison.Ordinal);
        Assert.DoesNotContain("stays PHP", prices, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-pl-hero", prices, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-pl-kpis", prices, StringComparison.Ordinal);
    }

    [Fact]
    public void StoragesRequestsTokensApps_UseClassicHpanelNotInventHero()
    {
        var storages = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpStoragesApp.razor"));
        Assert.Contains("class=\"hpanel\"", storages, StringComparison.Ordinal);
        Assert.Contains("/cp/storages/groups", storages, StringComparison.Ordinal);
        Assert.Contains("/cp/storages/write", storages, StringComparison.Ordinal);
        Assert.Contains("/cp/storages/membership", storages, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-st-hero", storages, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-st-kpis", storages, StringComparison.Ordinal);

        var requests = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpSystemRequestsApp.razor"));
        Assert.Contains("class=\"hpanel\"", requests, StringComparison.Ordinal);
        Assert.Contains("/cp/requests/set-vin-viewed", requests, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", requests, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", requests, StringComparison.Ordinal);

        var tokens = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpDesignTokensApp.razor"));
        Assert.Contains("class=\"hpanel\"", tokens, StringComparison.Ordinal);
        Assert.Contains("HasStaffAccess", tokens, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", tokens, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoPriceAndInfoBlocksApps_UseClassicHpanelNotInventHero()
    {
        var auto = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpAutoPriceApp.razor"));
        Assert.Contains("class=\"hpanel\"", auto, StringComparison.Ordinal);
        Assert.Contains("HasStaffAccess", auto, StringComparison.Ordinal);
        Assert.Contains("Classic twin", auto, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", auto, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ap-hero", auto, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ap-kpis", auto, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", auto, StringComparison.Ordinal);

        var blocks = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpInfoBlocksApp.razor"));
        Assert.Contains("class=\"hpanel\"", blocks, StringComparison.Ordinal);
        Assert.Contains("Classic twin", blocks, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", blocks, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w19-hero", blocks, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w19-kpis", blocks, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperCpHostGate", blocks, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", blocks, StringComparison.Ordinal);
    }

    [Fact]
    public void CrmWmsBudgetsConsolidationsApps_UseClassicHpanelNotInventHero()
    {
        var board = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCrmBoardApp.razor"));
        Assert.Contains("class=\"hpanel\"", board, StringComparison.Ordinal);
        Assert.Contains("Classic twin", board, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-crm-hero", board, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-crm-kpis", board, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", board, StringComparison.Ordinal);

        var tickets = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCrmTicketsApp.razor"));
        Assert.Contains("class=\"hpanel\"", tickets, StringComparison.Ordinal);
        Assert.Contains("/erp/tickets/reply", tickets, StringComparison.Ordinal);
        Assert.Contains("ErpSlaCreateForm", tickets, StringComparison.Ordinal);
        Assert.Contains("ErpTicketsCreateForm", tickets, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ct-hero", tickets, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", tickets, StringComparison.Ordinal);

        var opps = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCrmOpportunitiesApp.razor"));
        Assert.Contains("class=\"hpanel\"", opps, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-opp-hero", opps, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", opps, StringComparison.Ordinal);

        var activities = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCrmActivitiesApp.razor"));
        Assert.Contains("class=\"hpanel\"", activities, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w15-hero", activities, StringComparison.Ordinal);

        var wms = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpWarehouseWmsApp.razor"));
        Assert.Contains("class=\"hpanel\"", wms, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", wms, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/receive", wms, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/locations/save", wms, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/locations/delete", wms, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/waves/create", wms, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/waves/release", wms, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/work/complete", wms, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-wms-hero", wms, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", wms, StringComparison.Ordinal);

        var budgets = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));
        Assert.Contains("class=\"hpanel\"", budgets, StringComparison.Ordinal);
        Assert.Contains("/erp/pm/budgets/save", budgets, StringComparison.Ordinal);
        Assert.Contains("/erp/pm/budget-lines/add", budgets, StringComparison.Ordinal);
        Assert.Contains("/erp/budgets/advance", budgets, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-bud-hero", budgets, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", budgets, StringComparison.Ordinal);

        var cons = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpConsolidationsApp.razor"));
        Assert.Contains("class=\"hpanel\"", cons, StringComparison.Ordinal);
        Assert.Contains("/erp/consolidations/figures/save", cons, StringComparison.Ordinal);
        Assert.Contains("/erp/consolidations/ic/save", cons, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w14-hero", cons, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", cons, StringComparison.Ordinal);
    }

    [Fact]
    public void HrUaeWorkflowsCollectionsApps_UseClassicHpanelNotInventHero()
    {
        var hr = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpHrOverviewApp.razor"));
        Assert.Contains("class=\"hpanel\"", hr, StringComparison.Ordinal);
        Assert.Contains("Add an employee", hr, StringComparison.Ordinal);
        Assert.Contains("/erp/hr/employees/save", hr, StringComparison.Ordinal);
        Assert.Contains("/erp/hr/attendance/log", hr, StringComparison.Ordinal);
        Assert.Contains("/erp/hr/payroll/generate", hr, StringComparison.Ordinal);
        Assert.Contains("CpPhpModuleCopy.PurposeFor", hr, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-hr-hero", hr, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", hr, StringComparison.Ordinal);

        var uae = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpUaeTaxComplianceApp.razor"));
        Assert.Contains("class=\"hpanel\"", uae, StringComparison.Ordinal);
        Assert.Contains("/erp/uae-tax/ct-adjustments/save", uae, StringComparison.Ordinal);
        Assert.Contains("/erp/uae-tax/legislation/checklist/set", uae, StringComparison.Ordinal);
        Assert.Contains("Save tourist VAT", uae, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-uae-hero", uae, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", uae, StringComparison.Ordinal);

        var wf = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpWorkflowsApp.razor"));
        Assert.Contains("epc-auto-hero", wf, StringComparison.Ordinal);
        Assert.Contains("ErpAutomationCatalogue", wf, StringComparison.Ordinal);
        Assert.Contains("/erp/automation/deactivate", wf, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-wf-hero", wf, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", wf, StringComparison.Ordinal);

        var dunning = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCollectionsDunningApp.razor"));
        Assert.Contains("class=\"hpanel\"", dunning, StringComparison.Ordinal);
        Assert.Contains("/cp/collections-dunning/write", dunning, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", dunning, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w15-hero", dunning, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", dunning, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingPosSeoProductionInsuranceProjectsApps_UseClassicHpanelNotInventHero()
    {
        var growth = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpMarketingGrowthApp.razor"));
        Assert.Contains("class=\"hpanel\"", growth, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", growth, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-mg-hero", growth, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", growth, StringComparison.Ordinal);

        var seo = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpSeoApp.razor"));
        Assert.Contains("class=\"hpanel\"", seo, StringComparison.Ordinal);
        Assert.Contains("ASP.NET-primary storefront SEO", seo, StringComparison.Ordinal);
        Assert.Contains("Probe CHPU SEO", seo, StringComparison.Ordinal);
        Assert.Contains("/sitemap.xml", seo, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-nw-hero", seo, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", seo, StringComparison.Ordinal);

        var pos = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPosOverviewApp.razor"));
        Assert.Contains("class=\"hpanel\"", pos, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/pos/open-session\"", pos, StringComparison.Ordinal);
        Assert.Contains("Save POS advance", pos, StringComparison.Ordinal);
        Assert.Contains("/cp/pos/complete-sale", pos, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-pos-hero", pos, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", pos, StringComparison.Ordinal);

        var pages = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPageBuilderApp.razor"));
        Assert.Contains("class=\"hpanel\"", pages, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-pb-hero", pages, StringComparison.Ordinal);

        var broadcast = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpMarketingBroadcastApp.razor"));
        Assert.Contains("class=\"hpanel\"", broadcast, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-mkt-hero", broadcast, StringComparison.Ordinal);

        var promo = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPromotionsApp.razor"));
        Assert.Contains("class=\"hpanel\"", promo, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-prm-hero", promo, StringComparison.Ordinal);

        var prod = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpProductionOverviewApp.razor"));
        Assert.Contains("class=\"hpanel\"", prod, StringComparison.Ordinal);
        Assert.Contains("/erp/manufacturing/work-orders/create", prod, StringComparison.Ordinal);
        Assert.Contains("/erp/manufacturing/bom/save", prod, StringComparison.Ordinal);
        Assert.Contains("/erp/mfgr/planned/firm", prod, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-mfg-hero", prod, StringComparison.Ordinal);

        var ins = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpInsuranceComplianceApp.razor"));
        Assert.Contains("class=\"hpanel\"", ins, StringComparison.Ordinal);
        Assert.Contains("/erp/insurance/save", ins, StringComparison.Ordinal);
        Assert.Contains("/erp/insurance/delete", ins, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", ins, StringComparison.Ordinal);

        var prj = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpProjectsOverviewApp.razor"));
        Assert.Contains("class=\"hpanel\"", prj, StringComparison.Ordinal);
        Assert.Contains("/erp/projects/save", prj, StringComparison.Ordinal);
        Assert.Contains("/erp/projects/tasks/save", prj, StringComparison.Ordinal);
        Assert.Contains("/erp/projects/timesheets/log", prj, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-prj-hero", prj, StringComparison.Ordinal);

        var cost = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCostModelsApp.razor"));
        Assert.Contains("class=\"hpanel\"", cost, StringComparison.Ordinal);
        Assert.Contains("/erp/cost-models/txns/add", cost, StringComparison.Ordinal);
        Assert.Contains("/erp/cost-models/items/set", cost, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-costm-hero", cost, StringComparison.Ordinal);

        var po = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPoApprovalsApp.razor"));
        Assert.Contains("class=\"hpanel\"", po, StringComparison.Ordinal);
        Assert.Contains("/cp/po-approvals/approve", po, StringComparison.Ordinal);
        Assert.Contains("/cp/po-approvals/reject", po, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w18-hero", po, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", po, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantFinComplianceProcurementReportingApps_UseClassicHpanelNotInventHero()
    {
        var tenant = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantConfigApp.razor"));
        Assert.Contains("class=\"hpanel\"", tenant, StringComparison.Ordinal);
        Assert.Contains("/erp/tenant-config/save", tenant, StringComparison.Ordinal);
        Assert.Contains("/erp/security/roles/save", tenant, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w17-hero", tenant, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", tenant, StringComparison.Ordinal);

        var fin = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpFinAdvancedApp.razor"));
        Assert.Contains("class=\"hpanel\"", fin, StringComparison.Ordinal);
        Assert.Contains("/erp/fin/periods/status", fin, StringComparison.Ordinal);
        Assert.Contains("/erp/fin/periods/generate", fin, StringComparison.Ordinal);
        Assert.Contains("/erp/fin/alloc/save", fin, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-finadv-hero", fin, StringComparison.Ordinal);

        var docx = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpDocExpiryApp.razor"));
        Assert.Contains("/erp/doc-expiry/save", docx, StringComparison.Ordinal);
        Assert.Contains("/erp/doc-expiry/delete", docx, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w17-hero", docx, StringComparison.Ordinal);

        var soc2 = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpSoc2ComplianceApp.razor"));
        Assert.Contains("/erp/compliance/obligations/add", soc2, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-soc2-hero", soc2, StringComparison.Ordinal);

        var er = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpElectronicReportingApp.razor"));
        Assert.Contains("/erp/electronic-reporting/formats/save", er, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w15-hero", er, StringComparison.Ordinal);

        var prq = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPurchaseRequestsApp.razor"));
        Assert.Contains("/erp/procurement/requisitions/save", prq, StringComparison.Ordinal);
        Assert.Contains("/erp/procurement/requisitions/add-line", prq, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-prq-hero", prq, StringComparison.Ordinal);

        var rma = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpReturnsRmaApp.razor"));
        Assert.Contains("/erp/aftersales/rma-create", rma, StringComparison.Ordinal);
        Assert.Contains("Resolve RMA", rma, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-rma-hero", rma, StringComparison.Ordinal);

        var quotes = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpQuoteRequestsApp.razor"));
        Assert.Contains("/cp/quote-requests/send", quotes, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w19-hero", quotes, StringComparison.Ordinal);

        var syn = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpSynonymsApp.razor"));
        Assert.Contains("/cp/synonyms/write", syn, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-nw-hero", syn, StringComparison.Ordinal);

        var prices = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPricesEditApp.razor"));
        Assert.Contains("/cp/prices-edit/write", prices, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-cpmod-hero", prices, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", prices, StringComparison.Ordinal);
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
