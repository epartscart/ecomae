using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
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
    }

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
                return Results.Text(raw.Body, raw.ContentType);
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

    private static async Task<IResult> WithDbAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken,
        Func<System.Data.Common.DbConnection, CancellationToken, Task<object>> body,
        object fallback)
    {
        if (!connections.IsConfigured)
        {
            return Fallback(fallback);
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var payload = await body(connection, cancellationToken).ConfigureAwait(false);
            if (payload is StorefrontPhpAjax.RawHttp raw)
            {
                return Results.Text(raw.Body, raw.ContentType);
            }

            return payload is string text ? Plain(text) : Php(payload);
        }
        catch (Exception)
        {
            return Fallback(fallback);
        }
    }

    private static IResult Fallback(object fallback)
        => fallback is string text ? Plain(text) : Php(fallback);

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
