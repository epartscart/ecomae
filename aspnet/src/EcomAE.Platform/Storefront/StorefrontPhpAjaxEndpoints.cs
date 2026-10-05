using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

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
