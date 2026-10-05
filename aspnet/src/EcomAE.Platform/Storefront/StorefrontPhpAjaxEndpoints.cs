using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

public static class StorefrontPhpAjaxEndpoints
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null
    };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(StorefrontPhpAjax.WarehouseOffersPath, ["GET", "POST"], WarehouseAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ArticleBrandsPath, ["GET", "POST"], BrandsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CrossSearchPath, ["GET", "POST"], CrossAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.OfficeBunchesPath, ["GET", "POST"], BunchesAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ProductsOfBunchPath, ["GET", "POST"], BunchProductsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CartInfoPath, ["GET", "POST"], CartAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.UnreadMessagesPath, ["GET", "POST"], UnreadAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.VinInfoPath, ["GET", "POST"], VinAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ManufacturersListPath, ["GET", "POST"], ManufacturersAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ManufacturersFromPricesPath, ["GET", "POST"], ManufacturersFromPricesAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ManufacturersFromCrossServerPath, ["GET", "POST"], ManufacturersFromCrossAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.AnalogsListPath, ["GET", "POST"], AnalogsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.AsynchronPath, ["GET", "POST"], AsynchronAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.PartInfoPath, ["GET", "POST"], PartInfoAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ProductsOfBunch2Path, ["GET", "POST"], Bunch2Async)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.SkuMediaPublicPath, ["GET", "POST"], SkuMediaAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.AddToBasketPath, ["GET", "POST"], AddToBasketAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ChangeCountPath, ["GET", "POST"], ChangeCountAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.DeleteCartPath, ["GET", "POST"], DeleteCartAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.NotepadPath, ["GET", "POST"], NotepadAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.GarageCarsPath, ["GET", "POST"], GarageCarsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CheckoutCreatePath, ["GET", "POST"], CheckoutCreateAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.OrderMessagesPath, ["GET", "POST"], OrderMessagesAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.SendMessagePath, ["GET", "POST"], SendMessageAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CreateOperationPath, ["GET", "POST"], CreateOperationAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ReturnsCheckPath, ["GET", "POST"], ReturnsCheckAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ArticleListPath, ["GET", "POST"], ArticleListAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CheckForOrderPath, ["GET", "POST"], CheckForOrderAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.SetUserOptionPath, ["GET", "POST"], SetUserOptionAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.SetMyCityPath, ["GET", "POST"], SetMyCityAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.AddEvaluationPath, ["GET", "POST"], AddEvaluationAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ProductEvaluationsPath, ["GET", "POST"], ProductEvaluationsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ProductMarkPath, ["GET", "POST"], ProductMarkAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.VinMessagesPath, ["GET", "POST"], VinMessagesAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.VinSendMessagePath, ["GET", "POST"], VinSendMessageAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.GuestOrderLookupPath, ["GET", "POST"], GuestOrderLookupAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CatalogueCountPath, ["GET", "POST"], CatalogueCountAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CatalogueListPath, ["GET", "POST"], CatalogueListAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CataloguePagePath, ["GET", "POST"], CataloguePageAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.PickupTimingPath, ["GET", "POST"], PickupTimingAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.DemandMetaPath, ["GET", "POST"], DemandMetaAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.DemandShowcasePath, ["GET", "POST"], DemandShowcaseAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.DemandByCountryPath, ["GET", "POST"], DemandByCountryAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.DemandCardPath, ["GET", "POST"], DemandCardAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.DemandTagsPath, ["GET", "POST"], DemandTagsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.DemandVehiclesPath, ["GET", "POST"], DemandVehiclesAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.GarageMarkModelsPath, ["GET", "POST"], GarageMarkModelsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.GarageModelTypesPath, ["GET", "POST"], GarageModelTypesAsync)
            .DisableAntiforgery().AllowAnonymous();
        foreach (var path in StorefrontPhpAjax.UcatsProductPaths)
        {
            endpoints.MapMethods(path, ["GET", "POST"], UcatsAjaxAsync)
                .DisableAntiforgery().AllowAnonymous();
        }

        foreach (var path in StorefrontPhpAjax.UcatsGroupFieldPaths)
        {
            endpoints.MapMethods(path, ["GET", "POST"], UcatsAjaxAsync)
                .DisableAntiforgery().AllowAnonymous();
        }

        foreach (var path in StorefrontPhpAjax.UcatsFragmentPaths)
        {
            endpoints.MapMethods(path, ["GET", "POST"], UcatsFragmentAsync)
                .DisableAntiforgery().AllowAnonymous();
        }

        endpoints.MapMethods(StorefrontPhpAjax.UcatsCataloguesPath, ["GET", "POST"], UcatsCataloguesAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.UcatsAuthControlPath, ["GET", "POST"], UcatsAuthControlAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.QuoteAddPath, ["GET", "POST"], QuoteAddAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.QuoteManualPath, ["GET", "POST"], QuoteManualAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.QuoteSubmitPath, ["GET", "POST"], QuoteSubmitAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.QuoteAcceptPath, ["GET", "POST"], QuoteAcceptAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.TreeBrunchPath, ["GET", "POST"], TreeBrunchAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.TreeAsyncPath, ["GET", "POST"], TreeAsyncAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ToMarksPath, ["GET", "POST"], ToMarksAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ReturnsLoadPath, ["GET", "POST"], ReturnsLoadAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.WorkshopPublicPath, ["GET", "POST"], WorkshopPublicAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.GarageManagerPath, ["GET", "POST"], GarageManagerAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.ContactsPath, ["GET", "POST"], ContactsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.LoginSendCodePath, ["GET", "POST"], LoginSendCodeAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.LoginCheckCodePath, ["GET", "POST"], LoginCheckCodeAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.BulkUploadPath, ["GET", "POST"], BulkUploadAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.VendorIngestPath, ["GET", "POST"], VendorIngestAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.UCatalogApiPath, ["GET", "POST"], UCatalogApiAsync)
            .DisableAntiforgery().AllowAnonymous();
        foreach (var path in StorefrontPhpAjax.UCatalogDirectPaths)
        {
            endpoints.MapMethods(path, ["GET", "POST"], UCatalogDirectAsync)
                .DisableAntiforgery().AllowAnonymous();
        }

        endpoints.MapMethods(StorefrontPhpAjax.LicenseApiPath, ["GET", "POST"], LicenseApiAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpAddOrderLogPath, ["GET", "POST"], CpAddOrderLogAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpSetOrdersViewedPath, ["GET", "POST"], CpSetOrdersViewedAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpOrdersInfoPath, ["GET", "POST"], CpOrdersInfoAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPaidOrdersCountPath, ["GET", "POST"], CpPaidOrdersCountAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpDeleteOrdersPath, ["GET", "POST"], CpDeleteOrdersAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpSetUserCommentPath, ["GET", "POST"], CpSetUserCommentAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpUsersAutocompletePath, ["GET", "POST"], CpUsersAutocompleteAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpReturnsInfoPath, ["GET", "POST"], CpReturnsInfoAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpProductAliasPath, ["GET", "POST"], CpProductAliasAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpVinViewedPath, ["GET", "POST"], CpVinViewedAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CrossbaseStatusPath, ["GET", "POST"], CrossbaseStatusAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.FitmentJsPath, ["GET", "POST"], FitmentJsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.AiPartsExpertPath, ["GET", "POST"], AiPartsExpertAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.PartsAgentPath, ["GET", "POST"], PartsAgentAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.LaximoProxyPath, ["GET", "POST"], LaximoProxyAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.PriceUploadPath, ["GET", "POST"], PriceUploadApiAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.SkuMediaAdminPath, ["GET", "POST"], SkuMediaAdminAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.TaxExemptUploadPath, ["GET", "POST"], TaxExemptUploadAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.PricesSettingsPath, ["GET", "POST"], PricesSettingsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.PriceLookupPath, ["GET", "POST"], PriceLookupAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.LicenseActivatePath, ["GET", "POST"], LicenseActivateAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.OnPremHealthPath, ["GET", "POST"], OnPremHealthAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpUnreadMessagesPath, ["GET", "POST"], CpUnreadMessagesAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpVinInfoPath, ["GET", "POST"], CpVinInfoAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpMultilangPath, ["GET", "POST"], CpMultilangAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpStringTranslationPath, ["GET", "POST"], CpStringTranslationAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpStringInfoPath, ["GET", "POST"], CpStringInfoAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpSetStringErrorPath, ["GET", "POST"], CpSetStringErrorAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpSetStringCustomPath, ["GET", "POST"], CpSetStringCustomAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpSetStringSamePath, ["GET", "POST"], CpSetStringSameAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpCategoryTemplatesPath, ["GET", "POST"], CpCategoryTemplatesAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpOrderItemObjectPath, ["GET", "POST"], CpOrderItemObjectAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpSaveStringDescriptionPath, ["GET", "POST"], CpSaveStringDescriptionAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpSetUsedFoundPath, ["GET", "POST"], CpSetUsedFoundAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpDeleteUnusedStringsPath, ["GET", "POST"], CpDeleteUnusedStringsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpCreateStringPath, ["GET", "POST"], CpCreateStringAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpSaveTranslationPath, ["GET", "POST"], CpSaveTranslationAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpSearchUsedFoundPath, ["GET", "POST"], CpSearchUsedFoundAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpContentAliasPath, ["GET", "POST"], CpContentAliasAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpManufacturersPath, ["GET", "POST"], CpManufacturersAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpStorageGroupsPath, ["GET", "POST"], CpStorageGroupsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpReturnActionPath, ["GET", "POST"], CpReturnActionAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpStorageTogglePath, ["GET", "POST"], CpStorageToggleAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPricePreviewPath, ["GET", "POST"], CpPricePreviewAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpCompletePriceSessionPath, ["GET", "POST"], CpCompletePriceSessionAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPaymentsPath, ["GET", "POST"], CpPaymentsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPaymentsEndpointPath, ["GET", "POST"], CpPaymentsEndpointAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPriceDiagnosticsPath, ["GET", "POST"], CpPriceDiagnosticsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPricesSendPath, ["GET", "POST"], CpPricesSendAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpChannelsPath, ["GET", "POST"], CpChannelsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpLogisticsPath, ["GET", "POST"], CpLogisticsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpAccessoryPhotosPath, ["GET", "POST"], CpAccessoryPhotosAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPriceHistoryPath, ["GET", "POST"], CpPriceHistoryAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpDemandCsvPath, ["GET", "POST"], CpDemandCsvAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPartsAgentPath, ["GET", "POST"], CpPartsAgentAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpMarketingPath, ["GET", "POST"], CpMarketingAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpMarketingEndpointPath, ["GET", "POST"], CpMarketingEndpointAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpWorkshopEndpointPath, ["GET", "POST"], CpWorkshopEndpointAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpCrossesOperationsPath, ["GET", "POST"], CpCrossesOperationsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPricesEditPath, ["GET", "POST"], CpPricesEditAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpLoadUserModalPath, ["GET", "POST"], CpLoadUserModalAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpDemandUploadTmpPath, ["GET", "POST"], CpDemandUploadTmpAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpCrossesUploadTmpPath, ["GET", "POST"], CpCrossesUploadTmpAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpCrossesHandleFilePath, ["GET", "POST"], CpCrossesHandleFileAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpBulkCpPath, ["GET", "POST"], CpBulkCpAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPosPath, ["GET", "POST"], CpPosScriptAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpPosEndpointPath, ["GET", "POST"], CpPosEndpointAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpCatalogueProductsPath, ["GET", "POST"], CpCatalogueProductsAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontPhpAjax.CpYmlExportPath, ["GET", "POST"], CpYmlExportAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.ProcurementScript, ["GET", "POST"], CpGuardedScriptAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.ErpScript, ["GET", "POST"], CpGuardedScriptAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.CrmScript, ["GET", "POST"], CpGuardedScriptAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.CustomerScript, ["GET", "POST"], CpGuardedScriptAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.UsersCustomerScript, ["GET", "POST"], CpGuardedScriptAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.DocumentScript, ["GET", "POST"], CpGuardedScriptAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.CrmEndpoint, ["GET", "POST"], CpCrmEndpointAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.CustomerEndpoint, ["GET", "POST"], CpCustomerMgmtAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.UsersCustomerEndpoint, ["GET", "POST"], CpCustomerMgmtAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.DocumentEndpoint, ["GET", "POST"], CpDocumentControlAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.PricePackSetup, ["GET", "POST"], CpPricePackSetupAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.PriceEnableKeys, ["GET", "POST"], CpPriceEnableKeysAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.PriceExtract, ["GET", "POST"], CpPriceExtractAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.PriceExcelConvert, ["GET", "POST"], CpPriceExcelConvertAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.PricePrepareCsv, ["GET", "POST"], CpPricePrepareCsvAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.PriceImportCsv, ["GET", "POST"], CpPriceImportCsvAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.PypricesHealth, ["GET", "POST"], CpPypricesHealthAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.OrdersDetailPane, ["GET", "POST"], CpOrdersDetailPaneAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CpLegacyPhpAjaxLinks.CurrencyRates, ["GET", "POST"], CpCurrencyFallbackAsync)
            .DisableAntiforgery().AllowAnonymous();
    }

    private static IResult CpCurrencyFallbackAsync(HttpContext context)
    {
        _ = context;
        return Results.Json(new { ok = false, error = "bad_action" }, statusCode: StatusCodes.Status400BadRequest);
    }

    private static Task<IResult> CpGuardedScriptAsync(HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return Task.FromResult<IResult>(Results.Text("No access", "text/html; charset=utf-8"));
    }

    private static IResult LicenseApiAsync()
        => Results.Json(
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.LicenseDisabled),
            Json,
            contentType: "application/json; charset=utf-8",
            statusCode: StatusCodes.Status403Forbidden);

    private static async Task<IResult> WarehouseAsync(
        HttpContext context,
        ISurfaceDashboardSummaryReporter dashboards,
        IStorefrontPriceAccess priceAccess,
        CancellationToken cancellationToken)
    {
        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        var brand = await BrandAsync(context, cancellationToken).ConfigureAwait(false);
        var limit = Limit(await FieldAsync(context, "limit", cancellationToken).ConfigureAwait(false), 100, 500);
        var access = await priceAccess.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var result = await dashboards.SearchStorefrontPartsAsync(article, brand, limit, cancellationToken).ConfigureAwait(false);
        return Php(StorefrontPhpAjax.WarehouseOffers(result, brand, access.PricesVisible));
    }

    private static async Task<IResult> BrandsAsync(
        HttpContext context,
        ISurfaceDashboardSummaryReporter dashboards,
        CancellationToken cancellationToken)
    {
        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        var result = await dashboards.ListStorefrontArticleBrandsAsync(article, 200, cancellationToken).ConfigureAwait(false);
        return Php(StorefrontPhpAjax.ArticleBrands(result));
    }

    private static async Task<IResult> CrossAsync(
        HttpContext context,
        ISurfaceDashboardSummaryReporter dashboards,
        IStorefrontPriceAccess priceAccess,
        CancellationToken cancellationToken)
    {
        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        var brand = await BrandAsync(context, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(brand))
        {
            brand = await FieldAsync(context, "manufacturer", cancellationToken).ConfigureAwait(false);
        }

        var include = await FieldAsync(context, "include_crossbase", cancellationToken).ConfigureAwait(false);
        var wantCrossbase = include is "1" or "true" or "yes";
        var access = await priceAccess.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var result = await dashboards.BuildStorefrontCrossSearchAsync(
            article,
            brand,
            LegacySurfaceDashboardSql.StorefrontCrossSearchMax,
            cancellationToken,
            includeCrossbase: wantCrossbase).ConfigureAwait(false);
        return Php(StorefrontPhpAjax.CrossSearch(result, access.PricesVisible));
    }

    private static async Task<IResult> BunchesAsync(
        HttpContext context,
        ISurfaceDashboardSummaryReporter dashboards,
        CancellationToken cancellationToken)
    {
        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        var brand = await BrandAsync(context, cancellationToken).ConfigureAwait(false);
        var result = await dashboards.ListStorefrontOfficeStorageBunchesAsync(article, brand, cancellationToken).ConfigureAwait(false);
        return Php(StorefrontPhpAjax.Bunches(result));
    }

    private static async Task<IResult> BunchProductsAsync(
        HttpContext context,
        ISurfaceDashboardSummaryReporter dashboards,
        IStorefrontPriceAccess priceAccess,
        CancellationToken cancellationToken)
    {
        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        var brand = await BrandAsync(context, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(article))
        {
            return Php(StorefrontPhpAjax.ProductsOfBunch(
                new StorefrontProductsOfBunchResult(0, 0, 0, [], false, "empty", "Empty article"),
                pricesVisible: false));
        }

        _ = int.TryParse(await FieldAsync(context, "office_id", cancellationToken).ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var officeId);
        _ = int.TryParse(await FieldAsync(context, "storage_id", cancellationToken).ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var storageId);
        _ = int.TryParse(await FieldAsync(context, "geo_id", cancellationToken).ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var geoId);
        var query = await FieldAsync(context, "query", cancellationToken).ConfigureAwait(false);
        var access = await priceAccess.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var result = await dashboards.PollStorefrontProductsOfBunchAsync(
            article,
            brand,
            officeId,
            storageId,
            string.IsNullOrWhiteSpace(query) ? null : query,
            geoId,
            cancellationToken).ConfigureAwait(false);
        return Php(StorefrontPhpAjax.ProductsOfBunch(result, access.PricesVisible));
    }

    private static Task<IResult> ManufacturersAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(context, connections, cancellationToken, async (connection, ct) =>
        {
            _ = int.TryParse(await FieldAsync(context, "storage_id", ct).ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var storageId);
            var query = await FieldAsync(context, "query", ct).ConfigureAwait(false);
            return await StorefrontPhpAjax.ManufacturersFromStorageAsync(connection, query, storageId, ct).ConfigureAwait(false);
        }, StorefrontPhpAjax.DatabaseUnavailableManufacturers(0));

    private static Task<IResult> ManufacturersFromPricesAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(context, connections, cancellationToken, async (connection, ct) =>
        {
            var query = await FieldAsync(context, "query", ct).ConfigureAwait(false);
            var bunches = await FieldAsync(context, "office_storage_bunches", ct).ConfigureAwait(false);
            _ = int.TryParse(await FieldAsync(context, "group_id", ct).ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupId);
            return await StorefrontPhpAjax.ManufacturersFromPricesAsync(connection, query, bunches, groupId, ct).ConfigureAwait(false);
        }, StorefrontPhpAjax.DatabaseUnavailableManufacturers(StorefrontPhpAjax.PricesStorageLabel));

    private static Task<IResult> ManufacturersFromCrossAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(context, connections, cancellationToken, async (connection, ct) =>
            await StorefrontPhpAjax.ManufacturersFromCrossServerAsync(connection, await FieldAsync(context, "query", ct).ConfigureAwait(false), ct).ConfigureAwait(false),
            StorefrontPhpAjax.DatabaseUnavailableManufacturers(StorefrontPhpAjax.CrossStorageLabel));

    private static async Task<IResult> AnalogsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var search = await FieldAsync(context, "search_object", cancellationToken).ConfigureAwait(false);
        if (StorefrontPhpAjax.AnalogsArticleEmpty(search))
        {
            return Php(StorefrontPhpAjax.AnalogsEmptyArticle());
        }

        if (!connections.IsConfigured)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }

        return await WithDbAsync(context, connections, cancellationToken, (connection, ct) =>
            StorefrontPhpAjax.AnalogsListAsync(connection, search, ct), StorefrontPhpAjax.NoDbConnect).ConfigureAwait(false);
    }

    private static async Task<IResult> AsynchronAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IStorefrontPriceAccess priceAccess,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }

        var request = await FieldAsync(context, "request_object", cancellationToken).ConfigureAwait(false);
        var access = await priceAccess.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var outcome = await StorefrontPhpAjax.AsynchronAsync(connection, request, access.PricesVisible, cancellationToken).ConfigureAwait(false);
            return outcome.PlainText ? Plain(Convert.ToString(outcome.Payload, CultureInfo.InvariantCulture) ?? StorefrontPhpAjax.NoDbConnect) : Php(outcome.Payload);
        }
        catch (Exception)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }
    }

    private static IResult PartInfoAsync(HttpContext context)
    {
        var referer = context.Request.Headers.Referer.ToString();
        if (!StorefrontPhpAjax.RefererAllowed(referer, context.Request.Host.Host))
        {
            return Plain("Forbidden 403");
        }

        return Php(StorefrontPhpAjax.PartInfoUnconfigured());
    }

    private static async Task<IResult> Bunch2Async(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IStorefrontPriceAccess priceAccess,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }

        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        _ = int.TryParse(await FieldAsync(context, "office_id", cancellationToken).ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var officeId);
        _ = int.TryParse(await FieldAsync(context, "storage_id", cancellationToken).ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var storageId);
        _ = int.TryParse(await FieldAsync(context, "group_id", cancellationToken).ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupId);
        var query = await FieldAsync(context, "query", cancellationToken).ConfigureAwait(false);
        var access = await priceAccess.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, ct) => StorefrontPhpAjax.ProductsOfBunch2Async(connection, article, officeId, storageId, query, 0, groupId, access.PricesVisible, ct),
            StorefrontPhpAjax.NoDbConnect).ConfigureAwait(false);
    }

    private static async Task<IResult> SkuMediaAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var action = await FieldAsync(context, "action", cancellationToken).ConfigureAwait(false);
        if (action.Length == 0)
        {
            action = "lookup";
        }

        if (!string.Equals(action, "lookup", StringComparison.Ordinal))
        {
            return Php(StorefrontPhpAjax.SkuMediaUnknownAction());
        }

        var brand = await BrandAsync(context, cancellationToken).ConfigureAwait(false);
        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        _ = int.TryParse(await FieldAsync(context, "product_id", cancellationToken).ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out var productId);
        if (!connections.IsConfigured)
        {
            return Php(StorefrontPhpAjax.SkuMediaNoDatabase());
        }

        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, ct) => StorefrontPhpAjax.SkuMediaLookupAsync(connection, brand, article, productId, ct),
            StorefrontPhpAjax.SkuMediaNoDatabase()).ConfigureAwait(false);
    }

    private static async Task<IResult> AddToBasketAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IStorefrontPriceAccess priceAccess,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var session = await StorefrontPhpAjax.ReadSessionAsync(connection, context.Request.Cookies["session"], cancellationToken).ConfigureAwait(false);
            if (session.MissingTable)
            {
                return Php(new StorefrontPhpAjax.CartWriteBody(false, null, StorefrontPhpAjax.SessionsMissing, null, null, null, null, null, null, null));
            }

            var access = await priceAccess.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
            var products = await FieldAsync(context, "product_objects", cancellationToken).ConfigureAwait(false);
            var payload = await StorefrontPhpAjax.AddToBasketAsync(
                connection,
                session.UserId,
                session.SessionRecordId,
                access.PricesVisible,
                products,
                ExpectedTechKey(context),
                cancellationToken).ConfigureAwait(false);
            return payload is string text ? Plain(text) : Php(payload);
        }
        catch (Exception)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }
    }

    private static async Task<IResult> ChangeCountAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IStorefrontPriceAccess priceAccess,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Php(StorefrontPhpAjax.ChangeCountNoDatabase());
        }

        var request = await FieldAsync(context, "request_object", cancellationToken).ConfigureAwait(false);
        if (StorefrontPhpAjax.RequestHasTechKey(request, emptyCounts: false))
        {
            var accepted = StorefrontPhpAjax.TechKeyAccepted(ExpectedTechKey(context), StorefrontPhpAjax.RequestTechKey(request));
            return await WithDbAsync(
                context,
                connections,
                cancellationToken,
                (connection, ct) => StorefrontPhpAjax.ChangeCountAsync(connection, 0, 0, true, request, true, accepted, ct, context.Request.Cookies["my_city"]),
                StorefrontPhpAjax.ChangeCountNoDatabase()).ConfigureAwait(false);
        }

        return await WithSessionAsync(context, connections, cancellationToken, async (connection, csrf, ct) =>
        {
            var access = await priceAccess.ResolveAsync(context, ct).ConfigureAwait(false);
            return await StorefrontPhpAjax.ChangeCountAsync(
                connection,
                csrf.UserId,
                csrf.SessionRecordId,
                access.PricesVisible,
                request,
                false,
                false,
                ct,
                context.Request.Cookies["my_city"]).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static async Task<IResult> DeleteCartAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }

        var request = await FieldAsync(context, "request_object", cancellationToken).ConfigureAwait(false);
        if (StorefrontPhpAjax.RequestHasTechKey(request, emptyCounts: true))
        {
            var accepted = StorefrontPhpAjax.TechKeyAccepted(ExpectedTechKey(context), StorefrontPhpAjax.RequestTechKey(request));
            return await WithDbAsync(
                context,
                connections,
                cancellationToken,
                (connection, ct) => StorefrontPhpAjax.DeleteCartAsync(connection, 0, 0, request, true, accepted, ct),
                StorefrontPhpAjax.NoDbConnect).ConfigureAwait(false);
        }

        return await WithSessionAsync(context, connections, cancellationToken, (connection, csrf, ct) =>
            StorefrontPhpAjax.DeleteCartAsync(connection, csrf.UserId, csrf.SessionRecordId, request, false, false, ct)).ConfigureAwait(false);
    }

    private static Task<IResult> NotepadAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Plain(StorefrontPhpAjax.NoDbConnect),
            async (connection, csrf, ct) => await StorefrontPhpAjax.AddToNotepadAsync(
                connection,
                csrf.UserId,
                await FieldAsync(context, "garage", ct).ConfigureAwait(false),
                await FieldAsync(context, "product", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static Task<IResult> GarageCarsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Php(new StorefrontPhpAjax.GarageAnswer { Status = false, Message = StorefrontPhpAjax.NoDbConnect }),
            async (connection, csrf, ct) =>
            {
                var payload = await StorefrontPhpAjax.GarageCarsAsync(
                    connection,
                    csrf.UserId,
                    await FieldAsync(context, "request_object", ct).ConfigureAwait(false),
                    ct).ConfigureAwait(false);
                return payload is string html ? html : payload;
            });

    private static Task<IResult> CheckoutCreateAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Php(new StorefrontPhpAjax.ShopStatus(false, StorefrontPhpAjax.NoDbConnect, null, null)),
            async (connection, csrf, ct) =>
            {
                var phone = context.Request.HasFormContentType && context.Request.Form.ContainsKey("phone_not_auth")
                    ? await FieldAsync(context, "phone_not_auth", ct).ConfigureAwait(false)
                    : null;
                var email = context.Request.HasFormContentType && context.Request.Form.ContainsKey("email_not_auth")
                    ? await FieldAsync(context, "email_not_auth", ct).ConfigureAwait(false)
                    : null;
                return await StorefrontPhpAjax.CheckoutCreateAsync(
                    connection,
                    csrf.UserId,
                    csrf.SessionRecordId,
                    context.Request.Cookies["users_agreement"],
                    context.Request.Cookies["how_get"],
                    phone,
                    email,
                    ct,
                    context.Request.Host.Host).ConfigureAwait(false);
            });

    private static async Task<IResult> OrderMessagesAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }

        var manager = !string.IsNullOrEmpty(context.Request.Query["manager"]);
        var orderId = context.Request.Query["order_id"].ToString();
        var returnId = context.Request.Query["return_id"].ToString();
        if (manager)
        {
            return Php(new StorefrontPhpAjax.NumericDenied(false, "Forbidden", 501));
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var session = await StorefrontPhpAjax.ReadSessionAsync(connection, context.Request.Cookies["session"], cancellationToken).ConfigureAwait(false);
            var access = await StorefrontPhpAjax.OrderOwnedAsync(connection, session.UserId, orderId, returnId, cancellationToken).ConfigureAwait(false);
            if (access is not null)
            {
                return Php(access);
            }
        }
        catch (Exception)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }

        return await ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Plain(StorefrontPhpAjax.NoDbConnect),
            (connection, csrf, ct) => StorefrontPhpAjax.OrderMessagesAsync(connection, csrf.UserId, orderId, returnId, false, ct)).ConfigureAwait(false);
    }

    private static async Task<IResult> SendMessageAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Plain(StorefrontPhpAjax.NoDbConnect);
        }

        var manager = !string.IsNullOrEmpty(context.Request.Query["manager"]);
        if (manager)
        {
            return Php(new StorefrontPhpAjax.NumericDenied(false, "Forbidden", 501));
        }

        var orderId = context.Request.Query["order_id"].ToString();
        var returnId = context.Request.Query["return_id"].ToString();
        var text = context.Request.Query["text"].ToString();
        return await ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Plain(StorefrontPhpAjax.NoDbConnect),
            (connection, csrf, ct) => StorefrontPhpAjax.SendOrderMessageAsync(connection, csrf.UserId, orderId, returnId, text, false, ct, context.Request.Host.Host)).ConfigureAwait(false);
    }

    private static Task<IResult> CreateOperationAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Php(new StorefrontPhpAjax.FinanceResult { Result = false, Message = StorefrontPhpAjax.NoDbConnectChange }),
            async (connection, csrf, ct) => await StorefrontPhpAjax.CreateOperationAsync(
                connection,
                csrf.UserId,
                await FieldAsync(context, "request_object", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static Task<IResult> ReturnsCheckAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Php(new StorefrontPhpAjax.ReturnsBody { Status = false, Message = "DB connect error", Code = 502 }),
            async (connection, _, ct) => await StorefrontPhpAjax.CheckReturnsAsync(
                connection,
                await FieldAsync(context, "items_id", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static async Task<IResult> ArticleListAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Php(new StorefrontPhpAjax.ShopStatus(false, StorefrontPhpAjax.NoDbConnect, null, null));
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var session = await StorefrontPhpAjax.ReadSessionAsync(connection, context.Request.Cookies["session"], cancellationToken).ConfigureAwait(false);
            var payload = await StorefrontPhpAjax.ArticleListAsync(
                connection,
                session.UserId,
                context.Request.Cookies["shop_stat"],
                await FieldAsync(context, "request_object", cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
            return Php(payload);
        }
        catch (Exception)
        {
            return Php(new StorefrontPhpAjax.ShopStatus(false, StorefrontPhpAjax.NoDbConnect, null, null));
        }
    }

    private static async Task<IResult> CheckForOrderAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var missing = new StorefrontPhpAjax.CheckForOrderBody(false, StorefrontPhpAjax.NoDbConnect, "no_db_connect", null);
        if (!connections.IsConfigured)
        {
            return Php(missing);
        }

        var request = await FieldAsync(context, "request_object", cancellationToken).ConfigureAwait(false);
        if (StorefrontPhpAjax.RequestHasTechKey(request, emptyCounts: false))
        {
            var accepted = StorefrontPhpAjax.TechKeyAccepted(ExpectedTechKey(context), StorefrontPhpAjax.RequestTechKey(request));
            return await WithDbAsync(
                context,
                connections,
                cancellationToken,
                (connection, ct) => StorefrontPhpAjax.CheckForOrderAsync(connection, 0, 0, request, true, accepted, ct),
                missing).ConfigureAwait(false);
        }

        return await ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Php(missing),
            (connection, csrf, ct) => StorefrontPhpAjax.CheckForOrderAsync(connection, csrf.UserId, csrf.SessionRecordId, request, false, false, ct)).ConfigureAwait(false);
    }

    private static async Task<IResult> ShopCsrfAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken,
        IResult noDatabase,
        Func<System.Data.Common.DbConnection, StorefrontPhpAjax.CsrfDecision, CancellationToken, Task<object>> body)
    {
        if (!connections.IsConfigured)
        {
            return noDatabase;
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var posted = await FieldAsync(context, "csrf_guard_key", cancellationToken).ConfigureAwait(false);
            var session = context.Request.Cookies["session"];
            var csrf = await StorefrontPhpAjax.ReadCsrfAsync(
                connection,
                session,
                context.Request.Query.ContainsKey("csrf_guard_key") || (context.Request.HasFormContentType && context.Request.Form.ContainsKey("csrf_guard_key"))
                    ? posted
                    : null,
                cancellationToken).ConfigureAwait(false);
            if (!csrf.Ok)
            {
                return Php(StorefrontPhpAjax.CsrfFailure(csrf.Message));
            }

            var payload = await body(connection, csrf, cancellationToken).ConfigureAwait(false);
            if (payload is StorefrontPhpAjax.RawHttp raw)
            {
                return Results.Text(raw.Body, raw.ContentType, statusCode: raw.StatusCode);
            }

            if (payload is string text)
            {
                return text.Length == 0
                    || text is "true" or "false"
                    || string.Equals(text, StorefrontPhpAjax.NoDbConnect, StringComparison.Ordinal)
                    ? Plain(text)
                    : Results.Text(text, "text/html; charset=utf-8");
            }

            return Php(payload);
        }
        catch (Exception)
        {
            return noDatabase;
        }
    }

    private static string ExpectedTechKey(HttpContext context)
    {
        var options = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>();
        if (options is null)
        {
            return string.Empty;
        }

        var config = CpPhpConfig.Read(options.Value);
        return config.TryGetValue("tech_key", out var key) ? key : string.Empty;
    }

    private static IResult GuestOrderLookupAsync()
        => Results.Text(string.Empty, "text/html; charset=utf-8");

    private static Task<IResult> CatalogueCountAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, ct) => await StorefrontPhpAjax.CatalogueCountAsync(
                connection,
                await FieldAsync(context, "propucts_request", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false),
            StorefrontPhpAjax.NoDbConnect);

    private static Task<IResult> CatalogueListAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, ct) => await StorefrontPhpAjax.CatalogueListAsync(
                connection,
                await FieldAsync(context, "propucts_request", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false),
            StorefrontPhpAjax.NoDbConnect);

    private static Task<IResult> CataloguePageAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IStorefrontPriceAccess priceAccess,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, ct) =>
            {
                var access = await priceAccess.ResolveAsync(context, ct).ConfigureAwait(false);
                return await StorefrontPhpAjax.CataloguePageAsync(
                    connection,
                    await FieldAsync(context, "propucts_request", ct).ConfigureAwait(false),
                    access.PricesVisible,
                    ct).ConfigureAwait(false);
            },
            StorefrontPhpAjax.NoDbConnect);

    private static Task<IResult> PickupTimingAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, ct) =>
            {
                var look = await StorefrontPhpAjax.ReadSessionAsync(connection, context.Request.Cookies["session"], ct).ConfigureAwait(false);
                return await StorefrontPhpAjax.PickupTimingAsync(
                    connection,
                    look,
                    await FieldAsync(context, "office_id", ct).ConfigureAwait(false),
                    ct).ConfigureAwait(false);
            },
            StorefrontPhpAjax.NoDbConnect);

    private static Task<IResult> DemandMetaAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => DemandAsync(context, connections, cancellationToken, StorefrontPhpAjax.DemandMetaAsync);

    private static Task<IResult> DemandShowcaseAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => DemandAsync(context, connections, cancellationToken, async (connection, userId, admin, ct) =>
            await StorefrontPhpAjax.DemandShowcaseAsync(
                connection,
                userId,
                admin,
                await OptionalFieldAsync(context, "limit", ct).ConfigureAwait(false),
                await OptionalFieldAsync(context, "reseed", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static Task<IResult> DemandByCountryAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => DemandAsync(context, connections, cancellationToken, async (connection, userId, admin, ct) =>
            await StorefrontPhpAjax.DemandByCountryAsync(
                connection,
                userId,
                admin,
                await FieldAsync(context, "country", ct).ConfigureAwait(false),
                await OptionalFieldAsync(context, "limit", ct).ConfigureAwait(false),
                await OptionalFieldAsync(context, "seed", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static Task<IResult> DemandCardAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => DemandAsync(context, connections, cancellationToken, async (connection, userId, admin, ct) =>
            await StorefrontPhpAjax.DemandCardAsync(
                connection,
                userId,
                admin,
                await OptionalFieldAsync(context, "brand", ct).ConfigureAwait(false),
                await OptionalFieldAsync(context, "article", ct).ConfigureAwait(false),
                await FieldAsync(context, "country", ct).ConfigureAwait(false),
                await OptionalFieldAsync(context, "seed", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static Task<IResult> DemandTagsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => DemandAsync(context, connections, cancellationToken, async (connection, userId, admin, ct) =>
            await StorefrontPhpAjax.DemandTagsAsync(
                connection,
                userId,
                admin,
                await FieldAsync(context, "country", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static Task<IResult> DemandVehiclesAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => DemandAsync(context, connections, cancellationToken, async (connection, userId, admin, ct) =>
            await StorefrontPhpAjax.DemandVehiclesAsync(
                connection,
                userId,
                admin,
                await FieldAsync(context, "action", ct).ConfigureAwait(false),
                await FieldAsync(context, "country", ct).ConfigureAwait(false),
                await FieldAsync(context, "job_id", ct).ConfigureAwait(false),
                await OptionalFieldAsync(context, "limit", ct).ConfigureAwait(false),
                await OptionalFieldAsync(context, "batch", ct).ConfigureAwait(false),
                await OptionalFieldAsync(context, "seed", ct).ConfigureAwait(false),
                await OptionalFieldAsync(context, "require_stock", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static IResult GarageMarkModelsAsync()
        => Php(StorefrontPhpAjax.GarageUcatsUnavailable());

    private static IResult GarageModelTypesAsync()
        => Php(StorefrontPhpAjax.GarageUcatsUnavailable());

    private static IResult UcatsAjaxAsync()
        => Results.Text(StorefrontPhpAjax.UcatsLocalFailure, "application/json; charset=utf-8");

    private static IResult UcatsFragmentAsync()
        => Results.Text(StorefrontPhpAjax.UcatsNoAccess, "text/html; charset=utf-8");

    private static IResult UcatsCataloguesAsync(HttpContext context)
    {
        var options = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>();
        if (options is null)
        {
            return Results.Text(StorefrontPhpAjax.UcatsConfigMissing, "text/html; charset=utf-8");
        }

        return Results.Text(
            StorefrontPhpAjax.UcatsCatalogues(CpPhpConfig.Read(options.Value)),
            "text/html; charset=utf-8");
    }

    private static async Task<IResult> QuoteAddAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var products = await FieldAsync(context, "product_objects", cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.AddToQuoteAsync(
                connection,
                context.Request.Cookies["session"],
                context.Request.Cookies["u_id"],
                products,
                ExpectedTechKey(context),
                token),
            new StorefrontPhpAjax.QuoteBody(false, Message: StorefrontPhpAjax.QuoteDbMessage)).ConfigureAwait(false);
    }

    private static async Task<IResult> QuoteManualAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var manufacturer = await FieldAsync(context, "manufacturer", cancellationToken).ConfigureAwait(false);
        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        var articleShow = await FieldAsync(context, "article_show", cancellationToken).ConfigureAwait(false);
        var name = await FieldAsync(context, "name", cancellationToken).ConfigureAwait(false);
        var count = await FieldAsync(context, "count_need", cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.AddManualQuoteAsync(
                connection,
                context.Request.Cookies["session"],
                context.Request.Cookies["u_id"],
                manufacturer,
                article,
                articleShow,
                name,
                count,
                token),
            new StorefrontPhpAjax.QuoteBody(false, Message: StorefrontPhpAjax.QuoteDbMessage)).ConfigureAwait(false);
    }

    private static async Task<IResult> QuoteSubmitAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var quoteId = await FieldAsync(context, "quote_id", cancellationToken).ConfigureAwait(false);
        var note = await FieldAsync(context, "customer_note", cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.SubmitQuoteAsync(
                connection,
                context.Request.Cookies["session"],
                context.Request.Cookies["u_id"],
                quoteId,
                note,
                token),
            new StorefrontPhpAjax.QuoteBody(false, Message: StorefrontPhpAjax.QuoteDbMessage)).ConfigureAwait(false);
    }

    private static async Task<IResult> QuoteAcceptAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var quoteId = await FieldAsync(context, "quote_id", cancellationToken).ConfigureAwait(false);
        var techKey = ExpectedTechKey(context);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.AcceptQuoteAsync(
                connection,
                context.Request.Cookies["session"],
                context.Request.Cookies["u_id"],
                quoteId,
                techKey,
                token),
            new StorefrontPhpAjax.QuoteBody(false, Message: StorefrontPhpAjax.QuoteDbMessage)).ConfigureAwait(false);
    }

    private static Task<IResult> TreeBrunchAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.TreeBrunchAsync(
                connection,
                QueryInt(context, "tree_list_id"),
                QueryInt(context, "parent_id"),
                QueryInt(context, "int_1"),
                QueryInt(context, "int_2"),
                QueryInt(context, "int_3"),
                token),
            StorefrontPhpAjax.NoDbConnect);

    private static Task<IResult> TreeAsyncAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.TreeAsyncAsync(
                connection,
                QueryRaw(context, "tree_list_id"),
                QueryRaw(context, "parent_id"),
                token),
            StorefrontPhpAjax.NoDbConnect);

    private static Task<IResult> ReturnsLoadAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Plain(StorefrontPhpAjax.NoDbConnect),
            async (connection, _, token) => await StorefrontPhpAjax.LoadReturnsAsync(
                connection,
                ExpectedTechKey(context),
                await FieldAsync(context, "tech_key", token).ConfigureAwait(false),
                await ReturnLinesAsync(context, token).ConfigureAwait(false),
                await FieldAsync(context, "user_id", token).ConfigureAwait(false),
                await FieldAsync(context, "total_sum", token).ConfigureAwait(false),
                token).ConfigureAwait(false));

    private static Task<IResult> WorkshopPublicAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.WorkshopPublicAsync(
                connection,
                await FieldAsync(context, "action", token).ConfigureAwait(false),
                await FieldAsync(context, "customer_name", token).ConfigureAwait(false),
                await WorkshopPhoneAsync(context, token).ConfigureAwait(false),
                await FieldAsync(context, "plate", token).ConfigureAwait(false),
                await FieldAsync(context, "complaint", token).ConfigureAwait(false),
                await FieldAsync(context, "customer_email", token).ConfigureAwait(false),
                await FieldAsync(context, "vin", token).ConfigureAwait(false),
                await FieldAsync(context, "make", token).ConfigureAwait(false),
                await FieldAsync(context, "model", token).ConfigureAwait(false),
                await FieldAsync(context, "year", token).ConfigureAwait(false),
                await FieldAsync(context, "odometer", token).ConfigureAwait(false),
                await FieldAsync(context, "ref", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.WorkshopUnavailable));

    private static Task<IResult> GarageManagerAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.GarageManagerAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                context.Request.Cookies["session"],
                context.Request.Cookies["u_id"],
                await FieldAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "action", token).ConfigureAwait(false),
                await FieldAsync(context, "customer_name", token).ConfigureAwait(false),
                await FieldAsync(context, "customer_phone", token).ConfigureAwait(false),
                await FieldAsync(context, "plate", token).ConfigureAwait(false),
                await FieldAsync(context, "complaint", token).ConfigureAwait(false),
                await FieldAsync(context, "customer_email", token).ConfigureAwait(false),
                await FieldAsync(context, "vin", token).ConfigureAwait(false),
                await FieldAsync(context, "make", token).ConfigureAwait(false),
                await FieldAsync(context, "model", token).ConfigureAwait(false),
                await FieldAsync(context, "year", token).ConfigureAwait(false),
                await FieldAsync(context, "odometer", token).ConfigureAwait(false),
                await FieldAsync(context, "job_id", token).ConfigureAwait(false),
                await FieldAsync(context, "status", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.DatabaseUnavailable));

    private static Task<IResult> ContactsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var hasType = await HasFieldAsync(context, "type", token).ConfigureAwait(false);
                var hasAction = await HasFieldAsync(context, "action", token).ConfigureAwait(false);
                var hasCsrf = await HasFieldAsync(context, "csrf_guard_key", token).ConfigureAwait(false);
                var hasContact = await HasFieldAsync(context, "contact", token).ConfigureAwait(false);
                return await StorefrontPhpAjax.ContactsAsync(
                    connection,
                    context.Request.Cookies["session"],
                    context.Request.Cookies["u_id"],
                    hasType ? await FieldAsync(context, "type", token).ConfigureAwait(false) : null,
                    hasAction ? await FieldAsync(context, "action", token).ConfigureAwait(false) : null,
                    hasCsrf ? await FieldAsync(context, "csrf_guard_key", token).ConfigureAwait(false) : null,
                    hasContact ? await FieldAsync(context, "contact", token).ConfigureAwait(false) : null,
                    hasType,
                    hasAction,
                    hasCsrf,
                    token).ConfigureAwait(false);
            },
            StorefrontPhpAjax.NoDbConnect);

    private static async Task<IResult> LoginSendCodeAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var body = await JsonFieldsAsync(context, cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.SendLoginCodeAsync(
                connection,
                context.Request.Cookies["session"],
                context.Request.Cookies["u_id"],
                body.TryGetValue("csrf_guard_key", out var csrf) ? csrf : null,
                body.GetValueOrDefault("method"),
                body.GetValueOrDefault("contact"),
                token),
            StorefrontPhpAjax.NoDbConnect).ConfigureAwait(false);
    }

    private static async Task<IResult> LoginCheckCodeAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var body = await JsonFieldsAsync(context, cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.CheckLoginCodeAsync(
                connection,
                context.Request.Cookies["session"],
                context.Request.Cookies["u_id"],
                body.TryGetValue("csrf_guard_key", out var csrf) ? csrf : null,
                body.GetValueOrDefault("code"),
                token),
            StorefrontPhpAjax.NoDbConnect).ConfigureAwait(false);
    }

    private static Task<IResult> BulkUploadAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var file = await PostedFileAsync(context, "bulk_file", token).ConfigureAwait(false);
                return await StorefrontPhpAjax.BulkUploadAsync(
                    connection,
                    context.Request.Cookies["session"],
                    context.Request.Cookies["u_id"],
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                    await FieldAsync(context, "action", token).ConfigureAwait(false),
                    await FieldAsync(context, "upload_id", token).ConfigureAwait(false),
                    await FieldAsync(context, "summary", token).ConfigureAwait(false),
                    await FieldAsync(context, "rows", token).ConfigureAwait(false),
                    await FieldAsync(context, "article", token).ConfigureAwait(false),
                    file.HasFile,
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> VendorIngestAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var file = await PostedFileAsync(context, "price_file", token).ConfigureAwait(false);
                return await StorefrontPhpAjax.VendorIngestAsync(
                    connection,
                    context.Request.Cookies["session"],
                    context.Request.Cookies["u_id"],
                    await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                    file.HasFile,
                    file.Name,
                    file.Size,
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.DatabaseUnavailable));

    private static Task<IResult> UCatalogApiAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (!UCatalogCookie(context.Request.Cookies["UCatalog"]))
        {
            return Task.FromResult(Results.Text(StorefrontPhpAjax.UCatalogForbidden403, "text/html; charset=utf-8"));
        }

        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.UCatalogApiAsync(
                connection,
                context.Request.Cookies["session"],
                context.Request.Cookies["u_id"],
                await FieldAsync(context, "request_object", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            StorefrontPhpAjax.NoDbConnect);
    }

    private static IResult UCatalogDirectAsync()
        => Results.Text(StorefrontPhpAjax.UcatsNoAccess, "text/html; charset=utf-8");

    private static Task<IResult> CpAddOrderLogAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.AddOrderLogAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "order_id", token).ConfigureAwait(false),
                await FieldAsync(context, "text", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> CpSetOrdersViewedAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.SetOrdersViewedAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "request_object", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.CpCodedBody(false, StorefrontPhpAjax.DbConnectError, 502));

    private static Task<IResult> CpOrdersInfoAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.OrdersInfoAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "request_object", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.CpCodedBody(false, StorefrontPhpAjax.DbConnectError, 502));

    private static Task<IResult> CpPaidOrdersCountAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PaidOrdersCountAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.CpCodedBody(false, StorefrontPhpAjax.DbConnectError, 502));

    private static Task<IResult> CpDeleteOrdersAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.DeleteUnpaidOrdersAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "orders_list", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.DbConnectError));

    private static Task<IResult> CpSetUserCommentAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.SetUserCommentAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "user_id", token).ConfigureAwait(false),
                await FieldAsync(context, "comment", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> CpUsersAutocompleteAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.UsersAutocompleteAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "input_str", token).ConfigureAwait(false) ?? string.Empty,
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> CpReturnsInfoAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.ReturnsInfoAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                token),
            new StorefrontPhpAjax.ReturnsBadgeBody(0, 0));

    private static Task<IResult> CpProductAliasAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.ProductAliasAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "product_id", token).ConfigureAwait(false),
                await FieldAsync(context, "category_id", token).ConfigureAwait(false),
                await FieldAsync(context, "alias", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.RawHttp(StorefrontPhpAjax.AliasNoDbConnect, "text/html; charset=utf-8"));

    private static Task<IResult> CpVinViewedAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.SetVinViewedAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "request_object", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static bool UCatalogCookie(string? cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return false;
        }

        var text = cookie.Trim();
        var index = 0;
        if (text[0] is '+' or '-')
        {
            index++;
        }

        var end = index;
        while (end < text.Length && char.IsDigit(text[end]))
        {
            end++;
        }

        return end > index && int.TryParse(text[..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value == 1;
    }

    private static async Task<string> WorkshopPhoneAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var customerPhone = await FieldAsync(context, "customer_phone", cancellationToken).ConfigureAwait(false);
        if (customerPhone.Length > 0)
        {
            return customerPhone;
        }

        return await FieldAsync(context, "phone", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<StorefrontPhpAjax.ReturnLine>> ReturnLinesAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var lines = new SortedDictionary<int, (int ItemId, int ReasonId, string Comment, string Count)>();
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.HasFormContentType)
        {
            return [];
        }

        var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        foreach (var key in form.Keys)
        {
            var match = Regex.Match(key, @"^items\[(\d+)\]\[(item_id|reason_id|comment|count)\]$");
            if (!match.Success)
            {
                continue;
            }

            var index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            lines.TryGetValue(index, out var line);
            var value = form[key].ToString();
            line = match.Groups[2].Value switch
            {
                "item_id" => line with { ItemId = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0 },
                "reason_id" => line with { ReasonId = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reason) ? reason : 0 },
                "comment" => line with { Comment = value },
                _ => line with { Count = value }
            };
            lines[index] = line;
        }

        return lines.Values.Select(line => new StorefrontPhpAjax.ReturnLine(line.ItemId, line.ReasonId, line.Comment ?? string.Empty, line.Count ?? string.Empty)).ToList();
    }

    private static async Task<bool> HasFieldAsync(HttpContext context, string name, CancellationToken cancellationToken)
    {
        if (context.Request.Query.ContainsKey(name))
        {
            return true;
        }

        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            return form.ContainsKey(name);
        }

        return false;
    }

    private static async Task<string?> OptionalPostedAsync(HttpContext context, string name, CancellationToken cancellationToken)
    {
        if (context.Request.Query.ContainsKey(name))
        {
            return context.Request.Query[name].ToString();
        }

        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            if (form.ContainsKey(name))
            {
                return form[name].ToString();
            }
        }

        return null;
    }

    private static async Task<(bool HasFile, string Name, long Size)> PostedFileAsync(HttpContext context, string name, CancellationToken cancellationToken)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.HasFormContentType)
        {
            return (false, string.Empty, 0);
        }

        var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var file = form.Files.GetFile(name);
        if (file is null)
        {
            return (false, string.Empty, 0);
        }

        return (true, file.FileName, file.Length);
    }

    private static async Task<Dictionary<string, string>> JsonFieldsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.Request.ContentType is null || context.Request.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return fields;
        }

        using var reader = new StreamReader(context.Request.Body);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return fields;
        }

        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            fields[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.ToString();
        }

        return fields;
    }

    private static async Task<IResult> ToMarksAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            return Results.Text(StorefrontPhpAjax.NoDbConnect, "text/html; charset=utf-8");
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var posted = await FieldAsync(context, "csrf_guard_key", cancellationToken).ConfigureAwait(false);
            var csrf = await StorefrontPhpAjax.ReadCsrfAsync(
                connection,
                context.Request.Cookies["session"],
                context.Request.Query.ContainsKey("csrf_guard_key") || (context.Request.HasFormContentType && context.Request.Form.ContainsKey("csrf_guard_key"))
                    ? posted
                    : null,
                cancellationToken).ConfigureAwait(false);
            if (!csrf.Ok
                || !string.Equals(context.Request.Cookies["u_id"], csrf.UserId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return Php(StorefrontPhpAjax.CsrfFailure(csrf.Ok ? "Error! CSRF 3.1" : csrf.Message));
            }

            return Results.Text(StorefrontPhpAjax.ToMarksLocalFailure, "text/html; charset=utf-8");
        }
        catch (Exception)
        {
            return Results.Text(StorefrontPhpAjax.NoDbConnect, "text/html; charset=utf-8");
        }
    }

    private static int? QueryInt(HttpContext context, string name)
    {
        if (!context.Request.Query.ContainsKey(name))
        {
            return null;
        }

        return int.TryParse(context.Request.Query[name].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static string? QueryRaw(HttpContext context, string name)
        => context.Request.Query.ContainsKey(name) ? context.Request.Query[name].ToString() : null;

    private static Task<IResult> UcatsAuthControlAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.UcatsAuthControlAsync(
                connection,
                context.Request.Path + context.Request.QueryString,
                ClientIp(context),
                token),
            StorefrontPhpAjax.NoDbConnect);

    private static string ClientIp(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return string.Empty;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.ToString();
    }

    private static async Task<IResult> DemandAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken,
        Func<System.Data.Common.DbConnection, int, bool, CancellationToken, Task<object>> body)
    {
        if (!connections.IsConfigured)
        {
            return Php(StorefrontPhpAjax.DemandAuth());
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var look = await StorefrontPhpAjax.ReadSessionAsync(connection, context.Request.Cookies["session"], cancellationToken).ConfigureAwait(false);
            var admin = await StorefrontPhpAjax.DemandAdminAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                cancellationToken).ConfigureAwait(false);
            if (look.UserId <= 0 && !admin)
            {
                return Php(StorefrontPhpAjax.DemandAuth());
            }

            return Php(await body(connection, look.UserId, admin, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception)
        {
            return Php(StorefrontPhpAjax.DemandDatabaseUnavailable());
        }
    }

    private static async Task<string?> OptionalFieldAsync(HttpContext context, string name, CancellationToken cancellationToken)
    {
        if (context.Request.Query.ContainsKey(name))
        {
            return await FieldAsync(context, name, cancellationToken).ConfigureAwait(false);
        }

        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            if (form.ContainsKey(name))
            {
                return form[name].ToString();
            }
        }

        return null;
    }

    private static Task<IResult> SetUserOptionAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Plain(StorefrontPhpAjax.NoDbConnect),
            async (connection, csrf, ct) => await StorefrontPhpAjax.SetUserOptionAsync(
                connection,
                csrf.UserId,
                csrf.SessionRecordId,
                await FieldAsync(context, "key", ct).ConfigureAwait(false),
                await FieldAsync(context, "value", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static async Task<IResult> SetMyCityAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var geoId = await FieldAsync(context, "geo_id", cancellationToken).ConfigureAwait(false);
        return await ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Plain(StorefrontPhpAjax.NoDbConnect),
            (_, _, _) =>
            {
                context.Response.Cookies.Append("my_city", geoId, new CookieOptions
                {
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddSeconds(9999999),
                    HttpOnly = false,
                    Secure = false
                });
                return Task.FromResult<object>(new StorefrontPhpAjax.RawHttp("1", "text/html; charset=utf-8"));
            }).ConfigureAwait(false);
    }

    private static Task<IResult> AddEvaluationAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Plain(StorefrontPhpAjax.NoDbConnect),
            async (connection, csrf, ct) => await StorefrontPhpAjax.AddEvaluationAsync(
                connection,
                csrf.UserId,
                await FieldAsync(context, "evaluation_object", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static Task<IResult> ProductEvaluationsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Plain(StorefrontPhpAjax.NoDbConnect),
            async (connection, _, ct) => await StorefrontPhpAjax.ProductEvaluationsAsync(
                connection,
                await FieldAsync(context, "evaluation_query", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static Task<IResult> ProductMarkAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Plain(StorefrontPhpAjax.NoDbConnect),
            async (connection, _, ct) => await StorefrontPhpAjax.ProductMarkAsync(
                connection,
                await FieldAsync(context, "product_id", ct).ConfigureAwait(false),
                ct).ConfigureAwait(false));

    private static Task<IResult> VinMessagesAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Php(new StorefrontPhpAjax.VinConnectDenied(false, StorefrontPhpAjax.NoDbConnectChange, "no_db_connect")),
            async (connection, csrf, ct) => await StorefrontPhpAjax.VinMessagesAsync(
                connection,
                csrf.UserId,
                await FieldAsync(context, "vin_id", ct).ConfigureAwait(false),
                await FieldAsync(context, "manager", ct).ConfigureAwait(false),
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                ct).ConfigureAwait(false));

    private static Task<IResult> VinSendMessageAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopCsrfAsync(
            context,
            connections,
            cancellationToken,
            Php(new StorefrontPhpAjax.OptionBody(false, StorefrontPhpAjax.NoDbConnect)),
            async (connection, csrf, ct) => await StorefrontPhpAjax.VinSendMessageAsync(
                connection,
                csrf.UserId,
                await FieldAsync(context, "vin_id", ct).ConfigureAwait(false),
                await FieldAsync(context, "text", ct).ConfigureAwait(false),
                await FieldAsync(context, "manager", ct).ConfigureAwait(false),
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                ct).ConfigureAwait(false));

    private static Task<IResult> CartAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IStorefrontPriceAccess priceAccess,
        CancellationToken cancellationToken)
        => WithSessionAsync(context, connections, cancellationToken, async (connection, csrf, ct) =>
        {
            var access = await priceAccess.ResolveAsync(context, ct).ConfigureAwait(false);
            return await StorefrontPhpAjax.CartInfoAsync(connection, csrf.UserId, csrf.SessionRecordId, access.PricesVisible, ct).ConfigureAwait(false);
        });

    private static Task<IResult> UnreadAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithSessionAsync(context, connections, cancellationToken, async (connection, csrf, ct) =>
        {
            var returns = string.Equals(await FieldAsync(context, "returns", ct).ConfigureAwait(false), "1", StringComparison.Ordinal);
            return await StorefrontPhpAjax.UnreadMessagesAsync(connection, csrf.UserId, returns, ct).ConfigureAwait(false);
        });

    private static Task<IResult> VinAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithSessionAsync(context, connections, cancellationToken, (connection, csrf, ct) =>
            StorefrontPhpAjax.VinUnreadAsync(connection, csrf.UserId, ct));

    private static async Task<IResult> CrossbaseStatusAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var sample = await FieldAsync(context, "sample", cancellationToken).ConfigureAwait(false);
        long rows = 0;
        if (connections.IsConfigured)
        {
            try
            {
                var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                rows = await StorefrontPhpAjax.CrossRowCountAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                rows = 0;
            }
        }

        var config = PhpConfig(context);
        var local = config.TryGetValue("local_crosses", out var flag) && flag.Length > 0 && !string.Equals(flag, "0", StringComparison.Ordinal);
        return Php(StorefrontPhpAjax.CrossbaseOffline(sample, rows, local));
    }

    private static async Task<IResult> FitmentJsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var article = await FieldAsync(context, "n", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(article))
        {
            article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        }

        context.Response.Headers.CacheControl = "public, max-age=300";
        return Emit(context, StorefrontPhpAjax.FitmentScript(article));
    }

    private static async Task<IResult> AiPartsExpertAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var config = PhpConfig(context);
        var action = await FieldAsync(context, "action", cancellationToken).ConfigureAwait(false);
        var gate = StorefrontPhpAjax.AiExpertGate(
            config,
            action,
            await OptionalPostedAsync(context, "csrf", cancellationToken).ConfigureAwait(false),
            await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false),
            DateTime.UtcNow);
        if (gate is not StorefrontPhpAjax.AiReady)
        {
            return Emit(context, gate);
        }

        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        var brand = await FieldAsync(context, "brand", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(brand))
        {
            brand = await FieldAsync(context, "manufacturer", cancellationToken).ConfigureAwait(false);
        }

        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.AiExpertSearchAsync(connection, article, brand, token),
            new StorefrontPhpAjax.CodedJson(StatusCodes.Status503ServiceUnavailable, new StorefrontPhpAjax.OkMessage(false, StorefrontPhpAjax.AiDatabase))).ConfigureAwait(false);
    }

    private static async Task<IResult> PartsAgentAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var config = PhpConfig(context);
        if (StorefrontPhpAjax.AgentIsDisabled(config))
        {
            return Php(new StorefrontPhpAjax.OkMessage(false, StorefrontPhpAjax.AgentDisabled));
        }

        var action = await FieldAsync(context, "action", cancellationToken).ConfigureAwait(false);
        if (string.Equals(action, "bootstrap", StringComparison.Ordinal))
        {
            return Php(StorefrontPhpAjax.AgentBootstrap());
        }

        var sessionId = await OptionalPostedAsync(context, "session_id", cancellationToken).ConfigureAwait(false);
        var message = await OptionalPostedAsync(context, "message", cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.AgentActionAsync(connection, action, sessionId, message, token),
            new StorefrontPhpAjax.OkMessage(false, StorefrontPhpAjax.AgentDatabase)).ConfigureAwait(false);
    }

    private static async Task<IResult> LaximoProxyAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var action = await FieldAsync(context, "action", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(action))
        {
            action = "catalogs";
        }

        var vin = await FieldAsync(context, "vin", cancellationToken).ConfigureAwait(false);
        var catalog = await FieldAsync(context, "catalog", cancellationToken).ConfigureAwait(false);
        if (!connections.IsConfigured)
        {
            return Emit(context, StorefrontPhpAjax.LaximoLocal(action, vin, catalog, false, []));
        }

        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var catalogs = await StorefrontPhpAjax.LaximoCatalogsAsync(connection, token).ConfigureAwait(false);
                return StorefrontPhpAjax.LaximoLocal(action, vin, catalog, catalogs.Fresh, catalogs.Rows);
            },
            StorefrontPhpAjax.LaximoLocal(action, vin, catalog, false, [])).ConfigureAwait(false);
    }

    private static async Task<IResult> PriceUploadApiAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var techKey = await OptionalPostedAsync(context, "tech_key", cancellationToken).ConfigureAwait(false)
            ?? await OptionalPostedAsync(context, "key", cancellationToken).ConfigureAwait(false);
        var priceRaw = await FieldAsync(context, "id", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(priceRaw))
        {
            priceRaw = await FieldAsync(context, "price_id", cancellationToken).ConfigureAwait(false);
        }

        _ = int.TryParse(priceRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var priceId);
        var hasFile = false;
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            hasFile = form.Files.GetFile("document") is not null
                || form.Files.GetFile("file") is not null
                || form.Files.GetFile("price_file") is not null;
        }

        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.PriceUploadAsync(connection, PhpConfig(context), techKey, priceId, hasFile, token),
            new StorefrontPhpAjax.PriceDataBody(false, StorefrontPhpAjax.PriceUploadNoDb)).ConfigureAwait(false);
    }

    private static async Task<IResult> SkuMediaAdminAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var fields = await FormFieldsAsync(context, cancellationToken).ConfigureAwait(false);
        var action = fields.TryGetValue("action", out var posted) ? posted : string.Empty;
        var hasPhoto = false;
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            hasPhoto = form.Files.GetFile("photo") is not null;
        }

        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.SkuMediaAdminAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                fields.TryGetValue("csrf_guard_key", out var csrf) ? csrf : null,
                action,
                fields,
                hasPhoto,
                token),
            new StorefrontPhpAjax.SkuErrorBody(false, StorefrontPhpAjax.SkuNoDatabase)).ConfigureAwait(false);
    }

    private static async Task<IResult> TaxExemptUploadAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        IFormFile? file = null;
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            file = form.Files.GetFile("tax_exempt_cert");
        }

        var posted = await OptionalPostedAsync(context, "csrf_guard_key", cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                if (file is null)
                {
                    return await StorefrontPhpAjax.TaxExemptAsync(
                        connection,
                        context.Request.Cookies["session"],
                        posted,
                        string.Empty,
                        0,
                        false,
                        null,
                        Path.Combine(Path.GetTempPath(), "ecomae-tax-exempt"),
                        token).ConfigureAwait(false);
                }

                await using var stream = file.OpenReadStream();
                return await StorefrontPhpAjax.TaxExemptAsync(
                    connection,
                    context.Request.Cookies["session"],
                    posted,
                    file.FileName,
                    file.Length,
                    true,
                    stream,
                    Path.Combine(Path.GetTempPath(), "ecomae-tax-exempt"),
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, "Database unavailable")).ConfigureAwait(false);
    }

    private static Task<IResult> PricesSettingsAsync(HttpContext context, CancellationToken cancellationToken)
        => PricesSettingsBodyAsync(context, cancellationToken);

    private static async Task<IResult> PricesSettingsBodyAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var key = await OptionalPostedAsync(context, "tech_key", cancellationToken).ConfigureAwait(false);
        return Php(StorefrontPhpAjax.PricesSettings(
            PhpConfig(context),
            key,
            Environment.GetEnvironmentVariable("EPC_PRICES_TECH_IPS"),
            context.Connection.RemoteIpAddress?.ToString()));
    }

    private static async Task<IResult> PriceLookupAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var key = context.Request.Headers["X-API-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                key = authorization["Bearer ".Length..].Trim();
            }
        }

        var brand = await FieldAsync(context, "brand", cancellationToken).ConfigureAwait(false);
        var article = await FieldAsync(context, "article", cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.PriceLookupAsync(connection, key, brand, article, token),
            new StorefrontPhpAjax.CodedJson(
                StatusCodes.Status503ServiceUnavailable,
                new StorefrontPhpAjax.ApiErrorBody(false, new StorefrontPhpAjax.ApiError("platform_db_unavailable", "API client registry unavailable.")),
                "X-ECOM-API-Client",
                "1")).ConfigureAwait(false);
    }

    private static async Task<IResult> LicenseActivateAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return Emit(context, new StorefrontPhpAjax.CodedJson(StatusCodes.Status405MethodNotAllowed, new StorefrontPhpAjax.LicenseError(false, "method_not_allowed", "Use POST.")));
        }

        using var reader = new StreamReader(context.Request.Body);
        var raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.LicenseActivateAsync(connection, raw, token),
            new StorefrontPhpAjax.CodedJson(StatusCodes.Status503ServiceUnavailable, new StorefrontPhpAjax.LicenseError(false, "platform_db_unavailable", "License registry unavailable."))).ConfigureAwait(false);
    }

    private static async Task<IResult> OnPremHealthAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return Emit(context, new StorefrontPhpAjax.CodedJson(StatusCodes.Status405MethodNotAllowed, new StorefrontPhpAjax.HealthError(false, "method_not_allowed")));
        }

        using var reader = new StreamReader(context.Request.Body);
        var raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.OnPremHealthAsync(connection, raw, token),
            new StorefrontPhpAjax.CodedJson(StatusCodes.Status503ServiceUnavailable, new StorefrontPhpAjax.HealthError(false, "platform_db_unavailable"))).ConfigureAwait(false);
    }

    private static Task<IResult> CpUnreadMessagesAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.UnreadCpAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                context.Request.Query.ContainsKey("returns"),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.CpCodeBody(false, StorefrontPhpAjax.DbConnectErrorWord, 502));

    private static Task<IResult> CpVinInfoAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.VinBadgeAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static async Task<IResult> CpMultilangAsync(HttpContext context, CancellationToken cancellationToken)
        => Emit(context, StorefrontPhpAjax.MultilangValue(PhpConfig(context), await OptionalPostedAsync(context, "key", cancellationToken).ConfigureAwait(false)));

    private static Task<IResult> CpStringTranslationAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.StringTranslationAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "str_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "lang_code", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> CpStringInfoAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.StringInfoAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "str_key", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> CpSetStringErrorAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => SetLangFlagAsync(context, connections, "is_error", cancellationToken);

    private static Task<IResult> CpSetStringCustomAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => SetLangFlagAsync(context, connections, "is_custom", cancellationToken);

    private static async Task<IResult> SetLangFlagAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        string column,
        CancellationToken cancellationToken)
    {
        if (StorefrontPhpAjax.EditorRestricted(PhpConfig(context)))
        {
            return Php(new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.LangRestricted));
        }

        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.SetStringFlagAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                column,
                await OptionalPostedAsync(context, column, token).ConfigureAwait(false),
                await HasFieldAsync(context, column, token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "str_key", token).ConfigureAwait(false),
                await HasFieldAsync(context, "str_key", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect)).ConfigureAwait(false);
    }

    private static async Task<IResult> CpSetStringSameAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (StorefrontPhpAjax.EditorRestricted(PhpConfig(context)))
        {
            return Php(new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.LangRestricted));
        }

        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.SetStringSameAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "same", token).ConfigureAwait(false),
                await HasFieldAsync(context, "same", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "str_key", token).ConfigureAwait(false),
                await HasFieldAsync(context, "str_key", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect)).ConfigureAwait(false);
    }

    private static Task<IResult> CpCategoryTemplatesAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.CategoryTemplatesAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                await FieldAsync(context, "caption", token).ConfigureAwait(false),
                await FieldAsync(context, "category_object", token).ConfigureAwait(false),
                await FieldAsync(context, "template_id", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> CpOrderItemObjectAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => OrderItemObjectBodyAsync(context, connections, cancellationToken);

    private static async Task<IResult> OrderItemObjectBodyAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var key = await FieldAsync(context, "key", cancellationToken).ConfigureAwait(false);
        if (!StorefrontPhpAjax.TechKeyEquals(PhpConfig(context), key))
        {
            return Php(new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.OrderItemWrongKey));
        }

        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.OrderItemObjectAsync(
                connection,
                PhpConfig(context),
                key,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                context.Request.Query.ContainsKey("csrf_guard_key") ? context.Request.Query["csrf_guard_key"].ToString() : null,
                context.Request.Query["order_item_id"].ToString(),
                token),
            StorefrontPhpAjax.NoDbConnect).ConfigureAwait(false);
    }

    private static Task<IResult> CpSaveStringDescriptionAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (StorefrontPhpAjax.EditorRestricted(PhpConfig(context)))
        {
            return Task.FromResult(Php(new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.LangRestricted)));
        }

        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.SaveStringDescriptionAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await HasFieldAsync(context, "str_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "str_key", token).ConfigureAwait(false),
                await HasFieldAsync(context, "value", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "value", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));
    }

    private static Task<IResult> CpSetUsedFoundAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (StorefrontPhpAjax.EditorRestricted(PhpConfig(context)))
        {
            return Task.FromResult(Php(new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.LangRestricted)));
        }

        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.SetUsedFoundAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await HasFieldAsync(context, "str_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "str_key", token).ConfigureAwait(false),
                await HasFieldAsync(context, "used_found", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "used_found", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));
    }

    private static Task<IResult> CpDeleteUnusedStringsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (StorefrontPhpAjax.EditorRestricted(PhpConfig(context)))
        {
            return Task.FromResult(Php(new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.LangRestricted)));
        }

        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.DeleteUnusedStringsAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));
    }

    private static Task<IResult> CpCreateStringAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (StorefrontPhpAjax.EditorRestricted(PhpConfig(context)))
        {
            return Task.FromResult(Php(new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.LangRestricted)));
        }

        var config = PhpConfig(context);
        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.CreateStringAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                config,
                await HasFieldAsync(context, "description", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "description", token).ConfigureAwait(false),
                await HasFieldAsync(context, "same", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "same", token).ConfigureAwait(false),
                await HasFieldAsync(context, "is_error", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "is_error", token).ConfigureAwait(false),
                await HasFieldAsync(context, "is_custom", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "is_custom", token).ConfigureAwait(false),
                await HasFieldAsync(context, "used_found", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "used_found", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));
    }

    private static Task<IResult> CpSaveTranslationAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.SaveTranslationAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                StorefrontPhpAjax.EditorRestricted(PhpConfig(context)),
                await HasFieldAsync(context, "str_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "str_key", token).ConfigureAwait(false),
                await HasFieldAsync(context, "lang_code", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "lang_code", token).ConfigureAwait(false),
                await HasFieldAsync(context, "value", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "value", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> CpSearchUsedFoundAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        if (StorefrontPhpAjax.EditorRestricted(PhpConfig(context)))
        {
            return Task.FromResult(Php(new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.LangRestricted)));
        }

        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.SearchUsedFoundAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));
    }

    private static Task<IResult> CpContentAliasAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var config = PhpConfig(context);
        var secret = config.TryGetValue("secret_succession", out var configured) ? configured : string.Empty;
        if (!string.Equals(secret, context.Request.Query["code"].ToString(), StringComparison.Ordinal))
        {
            return Task.FromResult(Php(new StorefrontPhpAjax.FlagBody(false, "Forbidden")));
        }

        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.ContentAliasAsync(
                connection,
                context.Request.Query["alias"].ToString(),
                context.Request.Query["content_id"].ToString(),
                context.Request.Query["parent"].ToString(),
                context.Request.Query["is_frontend"].ToString(),
                token),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));
    }

    private static Task<IResult> CpManufacturersAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.ManufacturersAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "request_object", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.ManufacturerBody(false, "No DB connect", [], []));

    private static Task<IResult> CpStorageGroupsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.StorageGroupsAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "request_object", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.GroupsDb));

    private static Task<IResult> CpReturnActionAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.ReturnActionAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "return_id", token).ConfigureAwait(false)),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "status_id", token).ConfigureAwait(false)),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "line_id", token).ConfigureAwait(false)),
                await OptionalPostedAsync(context, "decide", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.ReturnDb));

    private static Task<IResult> CpStorageToggleAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var enabledRaw = await OptionalPostedAsync(context, "storefront_enabled", token).ConfigureAwait(false);
                var enabled = !string.IsNullOrEmpty(enabledRaw) && !string.Equals(enabledRaw, "0", StringComparison.Ordinal);
                return await StorefrontPhpAjax.StorageToggleAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                    await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                    await OptionalPostedAsync(context, "entity_type", token).ConfigureAwait(false),
                    StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "entity_id", token).ConfigureAwait(false)),
                    enabled,
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.ToggleBody(false, StorefrontPhpAjax.NoDbConnect, null, null));

    private static Task<IResult> CpPricePreviewAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PricePreviewAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "price_id", token).ConfigureAwait(false)),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> CpCrmEndpointAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.CrmEndpointGateAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                context.Request.Method,
                token),
            new StorefrontPhpAjax.FlagBody(false, "Database connection failed"));

    private static async Task<IResult> CpCustomerMgmtAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IErpEinvoiceProfileWriteService buyers,
        IErpCashWriteService cash,
        CancellationToken cancellationToken)
    {
        var action = await OptionalPostedAsync(context, "action", cancellationToken).ConfigureAwait(false) ?? string.Empty;
        var userId = StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "user_id", cancellationToken).ConfigureAwait(false));
        var amountText = await OptionalPostedAsync(context, "amount", cancellationToken).ConfigureAwait(false);
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.CustomerMgmtAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                context.Request.Method,
                action,
                userId,
                await OptionalPostedAsync(context, "buyer_name", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "company", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "address_line1", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "city", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "phone", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "email", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "trn", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "country_code", token).ConfigureAwait(false),
                amountText,
                buyers,
                cash,
                token),
            new StorefrontPhpAjax.FlagBody(false, "Database connection failed")).ConfigureAwait(false);
    }

    private static async Task<IResult> CpDocumentControlAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        ICpDocumentControlWriteService documents,
        CancellationToken cancellationToken)
    {
        var action = await OptionalPostedAsync(context, "action", cancellationToken).ConfigureAwait(false) ?? string.Empty;
        IReadOnlySet<string> posted = new HashSet<string>(StringComparer.Ordinal);
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            posted = form.Keys.ToHashSet(StringComparer.Ordinal);
        }

        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.DocumentControlAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                context.Request.Method,
                action,
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "expected_version", token).ConfigureAwait(false)),
                await OptionalPostedAsync(context, "legal_name", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "code", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "title", token).ConfigureAwait(false),
                posted,
                documents,
                token),
            new StorefrontPhpAjax.FlagBody(false, "Database connection failed")).ConfigureAwait(false);
    }

    private static Task<IResult> CpPricePackSetupAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var config = PhpConfig(context);
        var tech = config.TryGetValue("tech_key", out var key) ? key : string.Empty;
        config.TryGetValue("backend_dir", out var backend);
        var root = context.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<PhpReferenceOptions>>()?.Value.PhpDocRoot ?? string.Empty;
        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PricePackSetupAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                context.Request.Query["key"].ToString(),
                tech,
                root,
                backend,
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));
    }

    private static Task<IResult> CpPriceExtractAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var config = PhpConfig(context);
        var tech = config.TryGetValue("tech_key", out var key) ? key : string.Empty;
        config.TryGetValue("backend_dir", out var backend);
        config.TryGetValue("tmp_dir_prices_upload", out var tmp);
        var root = context.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<PhpReferenceOptions>>()?.Value.PhpDocRoot ?? string.Empty;
        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PriceExtractAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                context.Request.Query["key"].ToString(),
                tech,
                root,
                backend,
                tmp,
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));
    }

    private static Task<IResult> CpPriceExcelConvertAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => PriceFolderStepAsync(context, connections, cancellationToken, (connection, session, user, csrf, key, tech, root, backend, tmp, token) =>
            StorefrontPhpAjax.PriceExcelConvertAsync(connection, session, user, csrf, key, tech, root, backend, tmp, token));

    private static Task<IResult> CpPricePrepareCsvAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => PriceFolderStepAsync(context, connections, cancellationToken, (connection, session, user, csrf, key, tech, root, backend, tmp, token) =>
            StorefrontPhpAjax.PricePrepareCsvAsync(
                connection,
                session,
                user,
                csrf,
                key,
                tech,
                root,
                backend,
                tmp,
                StorefrontPhpAjax.PhpInt(context.Request.Query["price_id"].ToString()),
                token));

    private static Task<IResult> CpPriceImportCsvAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        ICpPriceImportService imports,
        CancellationToken cancellationToken)
    {
        var initiator = context.Request.Query["initiator"].ToString();
        bool? cleanBefore = string.Equals(initiator, "js", StringComparison.Ordinal) && context.Request.Query.ContainsKey("clean_before")
            ? true
            : null;
        return PriceFolderStepAsync(context, connections, cancellationToken, (connection, session, user, csrf, key, tech, root, backend, tmp, token) =>
            StorefrontPhpAjax.PriceImportCsvAsync(
                connection,
                session,
                user,
                csrf,
                key,
                tech,
                root,
                backend,
                tmp,
                StorefrontPhpAjax.PhpInt(context.Request.Query["price_id"].ToString()),
                imports,
                cleanBefore,
                string.Equals(tech, key, StringComparison.Ordinal) && tech.Length > 0 ? 0 : StorefrontPhpAjax.PhpInt(user),
                token));
    }

    private static Task<IResult> PriceFolderStepAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken,
        Func<System.Data.Common.DbConnection, string?, string?, string?, string, string, string, string?, string?, CancellationToken, Task<object>> body)
    {
        var config = PhpConfig(context);
        var tech = config.TryGetValue("tech_key", out var key) ? key : string.Empty;
        config.TryGetValue("backend_dir", out var backend);
        config.TryGetValue("tmp_dir_prices_upload", out var tmp);
        var root = context.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<PhpReferenceOptions>>()?.Value.PhpDocRoot ?? string.Empty;
        var session = context.Request.Cookies["admin_session"];
        var user = context.Request.Cookies["admin_u_id"];
        var postedKey = context.Request.Query["key"].ToString();
        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await body(
                connection,
                session,
                user,
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                postedKey,
                tech,
                root,
                backend,
                tmp,
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));
    }

    private static Task<IResult> CpPypricesHealthAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PypricesHealthAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));

    private static Task<IResult> CpOrdersDetailPaneAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.OrdersDetailPaneAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                StorefrontPhpAjax.PhpInt(context.Request.Query["order_id"].ToString()),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.RawHttp("<div class=\"epc-scp-orders-detail__empty\"><p>Database unavailable</p></div>", "text/html; charset=utf-8", StatusCodes.Status502BadGateway));

    private static Task<IResult> CpPriceEnableKeysAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var config = PhpConfig(context);
        var tech = config.TryGetValue("tech_key", out var key) ? key : string.Empty;
        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PriceEnableKeysAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                context.Request.Query["key"].ToString(),
                tech,
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));
    }

    private static Task<IResult> CpCompletePriceSessionAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var config = PhpConfig(context);
        var tech = config.TryGetValue("tech_key", out var key) ? key : string.Empty;
        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.CompletePriceSessionAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                context.Request.Query["key"].ToString(),
                tech,
                StorefrontPhpAjax.PhpInt(context.Request.Query["price_id"].ToString()),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.SessionResult(0, StorefrontPhpAjax.NoDbConnect));
    }

    private static Task<IResult> CpPaymentsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PaymentsAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "handler", token).ConfigureAwait(false),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "system_id", token).ConfigureAwait(false)),
                await OptionalPostedAsync(context, "parameters_values", token).ConfigureAwait(false),
                false,
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "No database"));

    private static Task<IResult> CpPaymentsEndpointAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                if (!HttpMethods.IsPost(context.Request.Method))
                {
                    return new StorefrontPhpAjax.FlagBody(false, "No action");
                }

                var action = await OptionalPostedAsync(context, "action", token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(action))
                {
                    return new StorefrontPhpAjax.FlagBody(false, "No action");
                }

                return await StorefrontPhpAjax.PaymentsAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    null,
                    action,
                    await OptionalPostedAsync(context, "handler", token).ConfigureAwait(false),
                    StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "system_id", token).ConfigureAwait(false)),
                    await OptionalPostedAsync(context, "parameters_values", token).ConfigureAwait(false),
                    true,
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, "Database connection failed"));

    private static Task<IResult> CpPriceDiagnosticsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var config = PhpConfig(context);
        var root = context.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<PhpReferenceOptions>>()?.Value.PhpDocRoot ?? string.Empty;
        return WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PriceDiagnosticsAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                config,
                root,
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));
    }

    private static Task<IResult> CpPricesSendAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PricesSendAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await FieldAsync(context, "request_object", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.CodedJson(503, new StorefrontPhpAjax.FlagBody(false, "DB connect error")));

    private static Task<IResult> CpChannelsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.ChannelsAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "channel_code", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "code", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "enabled", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "channel", token).ConfigureAwait(false),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "marketplace_order_id", token).ConfigureAwait(false)),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "Database unavailable"));

    private static Task<IResult> CpLogisticsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.LogisticsAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "carrier_code", token).ConfigureAwait(false),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "order_id", token).ConfigureAwait(false)),
                await OptionalPostedAsync(context, "weight_kg", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "Database unavailable"));

    private static Task<IResult> CpAccessoryPhotosAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.AccessoryPhotosAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "listing_id", token).ConfigureAwait(false)),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "photo_id", token).ConfigureAwait(false)),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.PhotoBody(false, "No database"));

    private static Task<IResult> CpPriceHistoryAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PriceHistoryAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "price_id", token).ConfigureAwait(false)),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, StorefrontPhpAjax.NoDbConnect));

    private static Task<IResult> CpDemandCsvAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.DemandCsvAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "country", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "file_full_path", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.CodedJson(503, new StorefrontPhpAjax.FlagBody(false, "No DB connect")));

    private static Task<IResult> CpPartsAgentAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PartsAgentAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "action", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "session_id", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));

    private static Task<IResult> CpMarketingAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var fields = await FormFieldsAsync(context, token).ConfigureAwait(false);
                return await StorefrontPhpAjax.MarketingAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    fields,
                    false,
                    HttpMethods.IsPost(context.Request.Method),
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, "No database"));

    private static Task<IResult> CpMarketingEndpointAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var fields = await FormFieldsAsync(context, token).ConfigureAwait(false);
                return await StorefrontPhpAjax.MarketingAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    fields,
                    true,
                    HttpMethods.IsPost(context.Request.Method),
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, "Database connection failed"));

    private static Task<IResult> CpWorkshopEndpointAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var fields = await FormFieldsAsync(context, token).ConfigureAwait(false);
                return await StorefrontPhpAjax.WorkshopEndpointAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    fields,
                    HttpMethods.IsPost(context.Request.Method),
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, "Database connection failed"));

    private static Task<IResult> CpCrossesOperationsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.CrossesOperationsAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "request_object", token).ConfigureAwait(false),
                context.Request.Cookies["crosses_sort"],
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.CodedJson(503, new StorefrontPhpAjax.FlagBody(false, "No DB connect")));

    private static Task<IResult> CpPricesEditAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.PricesEditAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "request_object", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));

    private static Task<IResult> CpLoadUserModalAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var config = PhpConfig(context);
                config.TryGetValue("domain_path", out var domain);
                config.TryGetValue("backend_dir", out var backend);
                return await StorefrontPhpAjax.LoadUserModalAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                    StorefrontPhpAjax.PhpInt(await OptionalPostedAsync(context, "customer_id", token).ConfigureAwait(false)),
                    domain,
                    backend,
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));

    private static Task<IResult> CpDemandUploadTmpAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopUploadAsync(context, connections, cancellationToken, "epc_demand_");

    private static Task<IResult> CpCrossesUploadTmpAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => ShopUploadAsync(context, connections, cancellationToken, "crosses_");

    private static async Task<IResult> ShopUploadAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken,
        string prefix)
    {
        string? fileName = null;
        byte[]? bytes = null;
        string? csrf = null;
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            csrf = form.ContainsKey("csrf_guard_key") ? form["csrf_guard_key"].ToString() : null;
            var file = form.Files.GetFile("csv_file");
            if (file is not null && file.FileName.Length > 0)
            {
                fileName = file.FileName;
                await using var stream = file.OpenReadStream();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
                bytes = memory.ToArray();
            }
        }

        var root = context.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<PhpReferenceOptions>>()?.Value.PhpDocRoot ?? string.Empty;
        var capturedName = fileName;
        var capturedBytes = bytes;
        var capturedCsrf = csrf;
        return await WithDbAsync(
            context,
            connections,
            cancellationToken,
            (connection, token) => StorefrontPhpAjax.ShopCsvUploadAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                capturedCsrf,
                capturedName,
                capturedBytes,
                root,
                prefix,
                token),
            new StorefrontPhpAjax.CodedJson(503, new StorefrontPhpAjax.FlagBody(false, "No DB connect"))).ConfigureAwait(false);
    }

    private static Task<IResult> CpCrossesHandleFileAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.CrossesHandleFileAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "import_options", token).ConfigureAwait(false),
                context.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<PhpReferenceOptions>>()?.Value.PhpDocRoot ?? string.Empty,
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.CodedJson(503, new StorefrontPhpAjax.FlagBody(false, "No DB connect")));

    private static Task<IResult> CpBulkCpAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var fields = await FormFieldsAsync(context, token).ConfigureAwait(false);
                return await StorefrontPhpAjax.BulkCpAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    fields,
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, "Database unavailable"));

    private static Task<IResult> CpCatalogueProductsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) => await StorefrontPhpAjax.CatalogueProductsAsync(
                connection,
                context.Request.Cookies["admin_session"],
                context.Request.Cookies["admin_u_id"],
                await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false),
                await OptionalPostedAsync(context, "request_object", token).ConfigureAwait(false),
                token).ConfigureAwait(false),
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));

    private static Task<IResult> CpYmlExportAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var csrf = context.Request.Query["csrf_guard_key"].ToString();
                if (csrf.Length == 0)
                {
                    csrf = await OptionalPostedAsync(context, "csrf_guard_key", token).ConfigureAwait(false) ?? string.Empty;
                }

                var options = context.Request.Query["export_options"].ToString();
                if (options.Length == 0)
                {
                    options = await OptionalPostedAsync(context, "export_options", token).ConfigureAwait(false) ?? string.Empty;
                }

                var root = context.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<PhpReferenceOptions>>()?.Value.PhpDocRoot ?? string.Empty;
                return await StorefrontPhpAjax.YmlExportAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    csrf,
                    options,
                    root,
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, "No DB connect"));

    private static Task<IResult> CpPosScriptAsync(HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return Task.FromResult<IResult>(Results.Text("No access", "text/html; charset=utf-8"));
    }

    private static Task<IResult> CpPosEndpointAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
        => WithDbAsync(
            context,
            connections,
            cancellationToken,
            async (connection, token) =>
            {
                var fields = await FormFieldsAsync(context, token).ConfigureAwait(false);
                return await StorefrontPhpAjax.PosEndpointAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    fields,
                    HttpMethods.IsPost(context.Request.Method),
                    token).ConfigureAwait(false);
            },
            new StorefrontPhpAjax.FlagBody(false, "Database connection failed"));

    private static IReadOnlyDictionary<string, string> PhpConfig(HttpContext context)
    {
        var options = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>();
        return options is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : CpPhpConfig.Read(options.Value);
    }

    private static async Task<Dictionary<string, string>> FormFieldsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in context.Request.Query)
        {
            fields[pair.Key] = pair.Value.ToString();
        }

        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            foreach (var pair in form)
            {
                fields[pair.Key] = pair.Value.ToString();
            }
        }

        return fields;
    }

    private static async Task<IResult> WithDbAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken,
        Func<System.Data.Common.DbConnection, CancellationToken, Task<object>> body,
        object fallback)
    {
        if (!connections.IsConfigured)
        {
            return Fallback(context, fallback);
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var payload = await body(connection, cancellationToken).ConfigureAwait(false);
            return Emit(context, payload);
        }
        catch (Exception)
        {
            return Fallback(context, fallback);
        }
    }

    private static IResult Fallback(HttpContext? context, object fallback)
        => Emit(context, fallback);

    private static IResult Emit(HttpContext? context, object payload)
    {
        if (payload is StorefrontPhpAjax.CodedJson coded)
        {
            if (context is not null && coded.HeaderName is not null && coded.HeaderValue is not null)
            {
                context.Response.Headers[coded.HeaderName] = coded.HeaderValue;
            }

            return Results.Json(coded.Body, Json, contentType: "application/json; charset=utf-8", statusCode: coded.StatusCode);
        }

        if (payload is StorefrontPhpAjax.RawHttp raw)
        {
            return Results.Text(raw.Body, raw.ContentType, statusCode: raw.StatusCode);
        }

        return payload is string text ? Plain(text) : Php(payload);
    }

    private static IResult Plain(string text)
        => Results.Text(
            text,
            string.Equals(text, StorefrontPhpAjax.NoDbConnect, StringComparison.Ordinal)
                ? "application/json; charset=utf-8"
                : "text/plain; charset=utf-8");

    private static async Task<IResult> WithSessionAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken,
        Func<System.Data.Common.DbConnection, StorefrontPhpAjax.CsrfDecision, CancellationToken, Task<object>> body)
    {
        if (!connections.IsConfigured)
        {
            return Php(new StorefrontPhpAjax.StatusBody(false, "Database unavailable", null));
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var posted = await FieldAsync(context, "csrf_guard_key", cancellationToken).ConfigureAwait(false);
            var session = context.Request.Cookies["session"];
            var csrf = await StorefrontPhpAjax.ReadCsrfAsync(
                connection,
                session,
                context.Request.Query.ContainsKey("csrf_guard_key") || (context.Request.HasFormContentType && context.Request.Form.ContainsKey("csrf_guard_key"))
                    ? posted
                    : null,
                cancellationToken).ConfigureAwait(false);
            if (!csrf.Ok)
            {
                return Php(StorefrontPhpAjax.CsrfFailure(csrf.Message));
            }

            return Php(await body(connection, csrf, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception)
        {
            return Php(new StorefrontPhpAjax.StatusBody(false, "Database unavailable", null));
        }
    }

    private static async Task<string> BrandAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var brand = await FieldAsync(context, "brand", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(brand))
        {
            brand = await FieldAsync(context, "brend", cancellationToken).ConfigureAwait(false);
        }

        return brand;
    }

    private static async Task<string> FieldAsync(HttpContext context, string name, CancellationToken cancellationToken)
    {
        if (context.Request.Query.TryGetValue(name, out var query) && query.Count > 0)
        {
            return query.ToString();
        }

        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            if (form.TryGetValue(name, out var value))
            {
                return value.ToString();
            }
        }

        return string.Empty;
    }

    private static int Limit(string raw, int fallback, int max)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) || limit < 1)
        {
            return fallback;
        }

        return Math.Min(limit, max);
    }

    private static IResult Php(object payload)
        => Results.Json(payload, Json, contentType: "application/json; charset=utf-8");
}
