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

        endpoints.MapGet(EcomAeRoutes.CatalogStatus, () => Results.Ok(new CatalogStatusResult(
            "Catalog",
            "api/v1/catalog.php",
            EcomAeRoutes.CatalogStatus,
            "placeholder",
            [
                "Connect catalog status to UMAPI/Laximo replacement services",
                "Add manufacturer/model/catalog endpoints",
                "Retire PHP api/v1/catalog.php after parity"
            ])));

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
}
