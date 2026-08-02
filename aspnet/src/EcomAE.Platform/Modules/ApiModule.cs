using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Security;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Modules;

public sealed class ApiModule : ISurfaceModule
{
    public SurfaceModuleDescriptor Descriptor { get; } = new(
        "api",
        "Public and tenant APIs",
        EcomAeRoutes.ApiPrefix,
        "api/, api/v1/, epc-api-v1.php, pyapi/",
        "placeholder",
        [EcomAePermissions.ApiAccess]);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(EcomAeRoutes.ApiMigrationStatus, () => Results.Ok(new
        {
            surface = "API",
            migration = "placeholder",
            next = "Port catalog, price lookup, tenant, ERP, BOS, mobile, and webhook APIs"
        }));

        endpoints.MapGet(EcomAeRoutes.CatalogStatus, async (
            HttpContext httpContext,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogStatusService catalogStatus,
            CancellationToken cancellationToken) =>
        {
            // Reuse RequireApiClientAuth as the shared exact-route API auth switch.
            LegacyApiClientRecord? client = null;
            if (options.Value.RequireApiClientAuth)
            {
                var auth = await authenticator.RequireAsync(httpContext.Request, "catalog", "status", cancellationToken);
                if (!auth.Succeeded)
                {
                    return Results.Json(
                        new { ok = false, error = new { code = auth.Code, message = auth.Message } },
                        statusCode: auth.StatusCode);
                }

                client = auth.Client;
            }

            var payload = await catalogStatus.GetStatusAsync(cancellationToken);
            if (client is not null)
            {
                await usageLogger.LogAsync(new LegacyApiUsageLogEntry(
                    "catalog_status",
                    string.Empty,
                    "api_client",
                    client.Id,
                    "/api/v1/catalog/status",
                    200,
                    QuotaBlocked: false,
                    payload.Source,
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty), cancellationToken);
            }

            return Results.Ok(new
            {
                connected = payload.Connected,
                message = payload.Message,
                last_checked = payload.LastChecked,
                last_success = payload.LastSuccess,
                last_error = payload.LastError,
                status_code = payload.StatusCode,
                counts = new
                {
                    manufacturers = payload.Counts.Manufacturers,
                    models = payload.Counts.Models,
                    modifications = payload.Counts.Modifications,
                    brands = payload.Counts.Brands,
                    vins = payload.Counts.Vins
                },
                sections = payload.Sections,
                cache_rows = payload.CacheRows,
                offline_ready = payload.OfflineReady,
                action_required = payload.ActionRequired,
                source = payload.Source,
                migration = new CatalogStatusResult(
                    "Catalog",
                    "api/v1/catalog.php?action=status",
                    EcomAeRoutes.CatalogStatus,
                    payload.Source == "database" ? "db-backed-read-only" : "migration-placeholder",
                    [
                        "Add manufacturer/model/catalog endpoints with exact-route parity",
                        "Replay PHP catalog fixtures before public cutover",
                        "Keep PHP fallback until live smoke passes"
                    ]),
                client = client is null ? null : new
                {
                    label = client.Label,
                    key_prefix = client.ClientKeyPrefix
                }
            });
        });

        endpoints.MapGet(EcomAeRoutes.CatalogManufacturers, async (
            HttpContext httpContext,
            string? section,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogManufacturerService manufacturers,
            CancellationToken cancellationToken) =>
        {
            LegacyApiClientRecord? client = null;
            if (options.Value.RequireApiClientAuth)
            {
                var auth = await authenticator.RequireAsync(httpContext.Request, "catalog", "manufacturers", cancellationToken);
                if (!auth.Succeeded)
                {
                    return Results.Json(
                        new { ok = false, error = new { code = auth.Code, message = auth.Message } },
                        statusCode: auth.StatusCode);
                }

                client = auth.Client;
            }

            var result = await manufacturers.GetBySectionAsync(section ?? "passenger", cancellationToken);
            if (client is not null)
            {
                await usageLogger.LogAsync(new LegacyApiUsageLogEntry(
                    "catalog_manufacturers",
                    result.Section,
                    "api_client",
                    client.Id,
                    "/api/v1/catalog/manufacturers",
                    200,
                    QuotaBlocked: false,
                    $"{result.Section}:{result.Rows}",
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty), cancellationToken);
            }

            return Results.Ok(new
            {
                ok = result.Ok,
                section = result.Section,
                rows = result.Rows,
                source = result.Source,
                stale = true,
                data = result.Data,
                message = result.Message,
                client = client is null ? null : new
                {
                    label = client.Label,
                    key_prefix = client.ClientKeyPrefix
                }
            });
        });

        endpoints.MapGet(EcomAeRoutes.CatalogModels, async (
            HttpContext httpContext,
            string? section,
            int? mfa_id,
            int? MFA_ID,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogVehicleCacheService vehicleCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "models", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var mfaId = mfa_id ?? MFA_ID ?? 0;
            var result = await vehicleCache.GetModelsAsync(section ?? "passenger", mfaId, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(new { ok = false, error = new { code = "missing_params", message = result.Message } }, statusCode: 400);
            }

            await LogCatalogAsync(httpContext, usageLogger, authResult.Client, "catalog_models", result, cancellationToken);
            return CatalogListOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogModifications, async (
            HttpContext httpContext,
            string? section,
            int? ms_id,
            int? MS_ID,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogVehicleCacheService vehicleCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "modifications", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var msId = ms_id ?? MS_ID ?? 0;
            var result = await vehicleCache.GetModificationsAsync(section ?? "passenger", msId, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(new { ok = false, error = new { code = "missing_params", message = result.Message } }, statusCode: 400);
            }

            await LogCatalogAsync(httpContext, usageLogger, authResult.Client, "catalog_modifications", result, cancellationToken);
            return CatalogListOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogBrands, async (
            HttpContext httpContext,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogVehicleCacheService vehicleCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "brands", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await vehicleCache.GetBrandsAsync(cancellationToken);
            await LogCatalogAsync(httpContext, usageLogger, authResult.Client, "catalog_brands", result, cancellationToken);
            return CatalogListOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogVin, async (
            HttpContext httpContext,
            string? vin,
            string? language,
            string? region,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogOfflineCacheService offlineCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "vin", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await offlineCache.LookupVinAsync(vin, language, region, cancellationToken);
            await LogOfflineCacheAsync(httpContext, usageLogger, authResult.Client, "catalog_vin", result.Ok, result.Code, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new { ok = false, error = new { code = result.Code, message = result.Message } },
                    statusCode: result.StatusCode);
            }

            return Results.Ok(result.Payload);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogEngines, async (
            HttpContext httpContext,
            string? section,
            int? mfa_id,
            int? MFA_ID,
            string? language,
            string? region,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogOfflineCacheService offlineCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "engines", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await offlineCache.LookupEnginesAsync(section, mfa_id ?? MFA_ID ?? 0, language, region, cancellationToken);
            await LogOfflineCacheAsync(httpContext, usageLogger, authResult.Client, "catalog_engines", result.Ok, result.Code, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new
                    {
                        ok = false,
                        error = new { code = result.Code, message = result.Message },
                        action = result.Action,
                        section = result.Section,
                        source = result.Source,
                        note = result.Code == "cache_miss"
                            ? "No epc_umapi_cache row for engines; PHP/UMAPI remains authoritative for live fills."
                            : null
                    },
                    statusCode: result.StatusCode);
            }

            return OfflineCacheOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogAnalogs, async (
            HttpContext httpContext,
            string? section,
            string? article,
            string? brand,
            string? language,
            string? region,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogOfflineCacheService offlineCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "analogs", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await offlineCache.LookupAnalogsAsync(section, article, brand, language, region, cancellationToken);
            await LogOfflineCacheAsync(httpContext, usageLogger, authResult.Client, "catalog_analogs", result.Ok, result.Code, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new
                    {
                        ok = false,
                        error = new { code = result.Code, message = result.Message },
                        action = result.Action,
                        section = result.Section,
                        source = result.Source,
                        note = result.Code == "cache_miss"
                            ? "No epc_umapi_cache row for analogs; PHP/UMAPI remains authoritative for live fills."
                            : null
                    },
                    statusCode: result.StatusCode);
            }

            return OfflineCacheOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogArticleBrands, async (
            HttpContext httpContext,
            string? section,
            string? article,
            string? language,
            string? region,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogOfflineCacheService offlineCache,
            CancellationToken cancellationToken) =>
        {
            // PHP action=brands is BrandRefinement(article). ASP.NET /catalog/brands is suppliers list.
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "brands", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await offlineCache.LookupArticleBrandsAsync(section, article, language, region, cancellationToken);
            await LogOfflineCacheAsync(httpContext, usageLogger, authResult.Client, "catalog_article_brands", result.Ok, result.Code, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new
                    {
                        ok = false,
                        error = new { code = result.Code, message = result.Message },
                        action = result.Action,
                        section = result.Section,
                        source = result.Source,
                        note = result.Code == "cache_miss"
                            ? "No epc_umapi_cache row for BrandRefinement; PHP/UMAPI remains authoritative for live fills."
                            : null
                    },
                    statusCode: result.StatusCode);
            }

            return OfflineCacheOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogCategories, async (
            HttpContext httpContext,
            string? section,
            string? id,
            string? ID,
            string? vehicle_type,
            string? type,
            string? language,
            string? region,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogOfflineCacheService offlineCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "categories", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await offlineCache.LookupCategoriesAsync(
                section,
                id ?? ID,
                vehicle_type ?? type,
                language,
                region,
                cancellationToken);
            await LogOfflineCacheAsync(httpContext, usageLogger, authResult.Client, "catalog_categories", result.Ok, result.Code, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new
                    {
                        ok = false,
                        error = new { code = result.Code, message = result.Message },
                        action = result.Action,
                        section = result.Section,
                        source = result.Source,
                        note = result.Code == "cache_miss"
                            ? "No epc_umapi_cache row for categories; PHP/UMAPI remains authoritative for live fills."
                            : null
                    },
                    statusCode: result.StatusCode);
            }

            return OfflineCacheOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogProducts, async (
            HttpContext httpContext,
            string? section,
            string? category_id,
            string? CATEGORY_ID,
            string? id,
            string? ID,
            string? vehicle_type,
            string? type,
            string? language,
            string? region,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogOfflineCacheService offlineCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "products", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await offlineCache.LookupProductsAsync(
                section,
                category_id ?? CATEGORY_ID,
                id ?? ID,
                vehicle_type ?? type,
                language,
                region,
                cancellationToken);
            await LogOfflineCacheAsync(httpContext, usageLogger, authResult.Client, "catalog_products", result.Ok, result.Code, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new
                    {
                        ok = false,
                        error = new { code = result.Code, message = result.Message },
                        action = result.Action,
                        section = result.Section,
                        source = result.Source,
                        note = result.Code == "cache_miss"
                            ? "No epc_umapi_cache row for products; PHP/UMAPI remains authoritative for live fills."
                            : null
                    },
                    statusCode: result.StatusCode);
            }

            return OfflineCacheOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogEngineSearch, async (
            HttpContext httpContext,
            string? section,
            string? code,
            string? engine,
            int? mfa_id,
            int? MFA_ID,
            string? language,
            string? region,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogOfflineCacheService offlineCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "engine_search", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await offlineCache.LookupEngineSearchAsync(
                section,
                code ?? engine,
                mfa_id ?? MFA_ID ?? 0,
                language,
                region,
                cancellationToken);
            await LogOfflineCacheAsync(httpContext, usageLogger, authResult.Client, "catalog_engine_search", result.Ok, result.Code, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new
                    {
                        ok = false,
                        error = new { code = result.Code, message = result.Message },
                        action = result.Action,
                        section = result.Section,
                        source = result.Source,
                        note = result.Code == "cache_miss"
                            ? "No epc_umapi_cache row for engine_search; PHP/UMAPI remains authoritative for live fills."
                            : null
                    },
                    statusCode: result.StatusCode);
            }

            return OfflineCacheOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogArticleLinks, async (
            HttpContext httpContext,
            string? section,
            int? id,
            string? language,
            string? region,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogOfflineCacheService offlineCache,
            CancellationToken cancellationToken) =>
        {
            // PHP external catalog allowlist omits article_links; accept catalog "article" keys.
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "article", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await offlineCache.LookupArticleLinksAsync(section, id ?? 0, language, region, cancellationToken);
            await LogOfflineCacheAsync(httpContext, usageLogger, authResult.Client, "catalog_article_links", result.Ok, result.Code, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new
                    {
                        ok = false,
                        error = new { code = result.Code, message = result.Message },
                        action = result.Action,
                        section = result.Section,
                        source = result.Source,
                        note = result.Code == "cache_miss"
                            ? "No epc_umapi_cache row for article_links; PHP/UMAPI remains authoritative for live fills."
                            : null
                    },
                    statusCode: result.StatusCode);
            }

            return OfflineCacheOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogArticle, async (
            HttpContext httpContext,
            string? section,
            int? id,
            string? language,
            string? region,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogOfflineCacheService offlineCache,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "article", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await offlineCache.LookupArticleAsync(section, id ?? 0, language, region, cancellationToken);
            await LogOfflineCacheAsync(httpContext, usageLogger, authResult.Client, "catalog_article", result.Ok, result.Code, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new
                    {
                        ok = false,
                        error = new { code = result.Code, message = result.Message },
                        action = result.Action,
                        section = result.Section,
                        source = result.Source,
                        requested_id = id,
                        note = result.Code == "cache_miss"
                            ? "No epc_umapi_cache row matched this article id; PHP/UMAPI remains authoritative for live fills."
                            : null
                    },
                    statusCode: result.StatusCode);
            }

            return OfflineCacheOk(result, authResult.Client);
        });

        endpoints.MapGet(EcomAeRoutes.CatalogBrandParts, async (
            HttpContext httpContext,
            string? brand,
            int? limit,
            int? offset,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> options,
            ICatalogBrandPartsService brandParts,
            CancellationToken cancellationToken) =>
        {
            var authResult = await AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "products", cancellationToken);
            if (authResult.Error is not null)
            {
                return authResult.Error;
            }

            var result = await brandParts.ListAsync(brand, limit ?? 100, offset ?? 0, cancellationToken);
            if (!result.Ok)
            {
                return Results.Json(
                    new { ok = false, error = new { code = "missing_params", message = result.Message } },
                    statusCode: 400);
            }

            if (authResult.Client is not null)
            {
                await usageLogger.LogAsync(new LegacyApiUsageLogEntry(
                    "catalog_brand_parts",
                    result.Brand,
                    "api_client",
                    authResult.Client.Id,
                    EcomAeRoutes.CatalogBrandParts,
                    200,
                    QuotaBlocked: false,
                    $"{result.Brand}:{result.Rows}",
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty), cancellationToken);
            }

            return Results.Ok(new
            {
                ok = true,
                brand = result.Brand,
                rows = result.Rows,
                source = result.Source,
                data = result.Data,
                message = result.Message,
                client = authResult.Client is null ? null : new
                {
                    label = authResult.Client.Label,
                    key_prefix = authResult.Client.ClientKeyPrefix
                }
            });
        });

        endpoints.MapGet(EcomAeRoutes.CatalogParity, (ICatalogParityReporter reporter) => Results.Ok(reporter.BuildReport()));

        endpoints.MapGet(EcomAeRoutes.PriceLookup, async (
            HttpContext httpContext,
            string brand,
            string article,
            ILegacyApiClientAuthenticator authenticator,
            ILegacyApiUsageLogger usageLogger,
            IOptions<PriceLookupOptions> priceLookupOptions,
            IPriceLookupService service,
            CancellationToken cancellationToken) =>
        {
            LegacyApiClientRecord? client = null;
            if (priceLookupOptions.Value.RequireApiClientAuth)
            {
                var auth = await authenticator.RequireAsync(httpContext.Request, "price_pro", "lookup", cancellationToken);
                if (!auth.Succeeded)
                {
                    return Results.Json(
                        new { ok = false, error = new { code = auth.Code, message = auth.Message } },
                        statusCode: auth.StatusCode);
                }

                client = auth.Client;
            }

            if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(article))
            {
                return Results.Json(
                    new { ok = false, error = new { code = "missing_params", message = "Query params brand and article are required." } },
                    statusCode: 400);
            }

            if (client is not null)
            {
                await usageLogger.LogAsync(new LegacyApiUsageLogEntry(
                    "price_lookup",
                    string.Empty,
                    "api_client",
                    client.Id,
                    "/api/v1/price/lookup",
                    200,
                    QuotaBlocked: false,
                    $"{brand}/{article}",
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty), cancellationToken);
            }

            var result = await service.LookupAsync(new PriceLookupRequest(brand, article), cancellationToken);
            if (!result.Status)
            {
                return Results.BadRequest(result);
            }

            if (client is null)
            {
                return Results.Ok(result);
            }

            return Results.Ok(new
            {
                result.Status,
                result.Brand,
                result.Article,
                result.Offers,
                result.MigrationStatus,
                result.Message,
                client = new
                {
                    label = client.Label,
                    key_prefix = client.ClientKeyPrefix
                }
            });
        });

        endpoints.MapGet(EcomAeRoutes.PriceLookupParity, async (IPriceLookupParityReporter reporter, CancellationToken cancellationToken) =>
        {
            var report = await reporter.BuildReportAsync(cancellationToken);
            return Results.Ok(report);
        });
    }

    private static async Task<(LegacyApiClientRecord? Client, IResult? Error)> AuthorizeCatalogAsync(
        HttpContext httpContext,
        ILegacyApiClientAuthenticator authenticator,
        ILegacyApiUsageLogger usageLogger,
        IOptions<PriceLookupOptions> options,
        string action,
        CancellationToken cancellationToken)
    {
        if (!options.Value.RequireApiClientAuth)
        {
            return (null, null);
        }

        var auth = await authenticator.RequireAsync(httpContext.Request, "catalog", action, cancellationToken);
        if (auth.Succeeded)
        {
            return (auth.Client, null);
        }

        return (null, Results.Json(
            new { ok = false, error = new { code = auth.Code, message = auth.Message } },
            statusCode: auth.StatusCode));
    }

    private static async Task LogCatalogAsync(
        HttpContext httpContext,
        ILegacyApiUsageLogger usageLogger,
        LegacyApiClientRecord? client,
        string action,
        CatalogCacheListResult result,
        CancellationToken cancellationToken)
    {
        if (client is null)
        {
            return;
        }

        await usageLogger.LogAsync(new LegacyApiUsageLogEntry(
            action,
            result.Section,
            "api_client",
            client.Id,
            httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value! : string.Empty,
            200,
            QuotaBlocked: false,
            $"{result.Section}:{result.Rows}",
            httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty), cancellationToken);
    }

    private static IResult CatalogListOk(CatalogCacheListResult result, LegacyApiClientRecord? client)
    {
        return Results.Ok(new
        {
            ok = result.Ok,
            action = result.Action,
            section = result.Section,
            mfa_id = result.MfaId,
            ms_id = result.MsId,
            rows = result.Rows,
            source = result.Source,
            stale = true,
            data = result.Data,
            message = result.Message,
            client = client is null ? null : new
            {
                label = client.Label,
                key_prefix = client.ClientKeyPrefix
            }
        });
    }

    private static async Task LogOfflineCacheAsync(
        HttpContext httpContext,
        ILegacyApiUsageLogger usageLogger,
        LegacyApiClientRecord? client,
        string action,
        bool ok,
        string code,
        CancellationToken cancellationToken)
    {
        if (client is null)
        {
            return;
        }

        await usageLogger.LogAsync(new LegacyApiUsageLogEntry(
            action,
            string.Empty,
            "api_client",
            client.Id,
            httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value! : string.Empty,
            ok ? 200 : (code == "cache_miss" ? 404 : 400),
            QuotaBlocked: false,
            ok ? "cache_hit" : code,
            httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty), cancellationToken);
    }

    private static IResult OfflineCacheOk(CatalogActionCacheLookupResult result, LegacyApiClientRecord? client)
    {
        return Results.Ok(new
        {
            ok = true,
            action = result.Action,
            section = result.Section,
            rows = result.Rows,
            source = result.Source,
            stale = result.Stale,
            data = result.Payload,
            client = client is null ? null : new
            {
                label = client.Label,
                key_prefix = client.ClientKeyPrefix
            }
        });
    }
}
