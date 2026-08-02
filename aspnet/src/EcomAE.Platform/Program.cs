using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Modules;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Security;
using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EcomAeOptions>(builder.Configuration.GetSection(EcomAeOptions.SectionName));
builder.Services.Configure<MigrationRouteCutoverOptions>(builder.Configuration.GetSection(MigrationRouteCutoverOptions.SectionName));
builder.Services.Configure<PriceLookupOptions>(builder.Configuration.GetSection(PriceLookupOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITenantRegistry, ConfigurationTenantRegistry>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<ILegacySessionValidator, HttpLegacySessionValidator>();
builder.Services.AddSingleton<ILegacySessionParityReporter, LegacySessionParityReporter>();
builder.Services.AddSingleton<ITenantDbConnectionFactory, MySqlTenantDbConnectionFactory>();
builder.Services.AddSingleton<ILegacyApiClientStore, DbLegacyApiClientStore>();
builder.Services.AddSingleton<ILegacyApiUsageLogger, DbLegacyApiUsageLogger>();
builder.Services.AddSingleton<ILegacyApiClientAuthenticator, LegacyApiClientAuthenticator>();
builder.Services.AddSingleton<ILegacyApiClientParityReporter, LegacyApiClientParityReporter>();
builder.Services.AddSingleton<ITenantResolver, RouteTenantResolver>();
builder.Services.AddEcomAeAuthorization();
builder.Services.AddEcomAeSurfaceModules();
builder.Services.AddSingleton<ISurfaceShellCatalog, MigrationSurfaceShellCatalog>();
builder.Services.AddSingleton<IPriceOfferRepository>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var csvPath = configuration["PriceLookup:FixtureCsvPath"];
    if (!string.IsNullOrWhiteSpace(csvPath))
    {
        return new CsvPriceOfferRepository(csvPath);
    }

    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    if (connections.IsConfigured)
    {
        return ActivatorUtilities.CreateInstance<DbPriceOfferRepository>(sp);
    }

    return new MigrationPriceOfferRepository();
});
builder.Services.AddSingleton<IPriceLookupService, RepositoryPriceLookupService>();
builder.Services.AddSingleton<IPriceLookupParityReporter, PriceLookupParityReporter>();
builder.Services.AddSingleton<ICatalogStatusRepository>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    return connections.IsConfigured
        ? ActivatorUtilities.CreateInstance<DbCatalogStatusRepository>(sp)
        : new MigrationCatalogStatusRepository();
});
builder.Services.AddSingleton<ICatalogStatusService, CatalogStatusService>();
builder.Services.AddSingleton<ICatalogManufacturerRepository>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    return connections.IsConfigured
        ? ActivatorUtilities.CreateInstance<DbCatalogManufacturerRepository>(sp)
        : new MigrationCatalogManufacturerRepository();
});
builder.Services.AddSingleton<ICatalogManufacturerService, CatalogManufacturerService>();
builder.Services.AddSingleton<ICatalogVehicleCacheRepository>(sp =>
{
    var connections = sp.GetRequiredService<ITenantDbConnectionFactory>();
    return connections.IsConfigured
        ? ActivatorUtilities.CreateInstance<DbCatalogVehicleCacheRepository>(sp)
        : new MigrationCatalogVehicleCacheRepository();
});
builder.Services.AddSingleton<ICatalogVehicleCacheService, CatalogVehicleCacheService>();
builder.Services.AddSingleton<ICatalogParityReporter, CatalogParityReporter>();
builder.Services.AddSingleton<IMigrationParityReporter, MigrationParityReporter>();
builder.Services.AddSingleton<IControlPanelParityReporter, ControlPanelParityReporter>();
builder.Services.AddSingleton<IErpParityReporter, ErpParityReporter>();
builder.Services.AddSingleton<IBosParityReporter, BosParityReporter>();
builder.Services.AddSingleton<IStorefrontParityReporter, StorefrontParityReporter>();
builder.Services.AddSingleton<ITenantWorkspaceParityReporter, TenantWorkspaceParityReporter>();
builder.Services.AddSingleton<IMigrationReadinessReporter, MigrationReadinessReporter>();
builder.Services.AddSingleton<IMigrationCutoverPlanner, MigrationCutoverPlanner>();
builder.Services.AddSingleton<IMigrationRouteCutoverPolicy, MigrationRouteCutoverPolicy>();
builder.Services.AddSingleton<IDataParityReporter, DataParityReporter>();
builder.Services.AddSingleton<ICutoverValidationReporter, CutoverValidationReporter>();
builder.Services.AddSingleton<IMigrationProgressReporter, MigrationProgressReporter>();
builder.Services.AddSingleton<ISurfaceParityReporter, SurfaceParityReporter>();
builder.Services.AddSingleton<IZeroPhpCompletionReporter, ZeroPhpCompletionReporter>();
builder.Services.AddSingleton<IPythonSidecarCatalogReporter, PythonSidecarCatalogReporter>();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<RouteCutoverDecisionMiddleware>();

app.MapHealthChecks(EcomAeRoutes.Health);

app.MapGet(EcomAeRoutes.MigrationStatus, (IMigrationParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationReadiness, (IMigrationReadinessReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationCutoverPlan, (IMigrationCutoverPlanner planner) => Results.Ok(planner.BuildPlan()));

app.MapGet(EcomAeRoutes.MigrationProgress, (IMigrationProgressReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.ZeroPhpCompletion, (IZeroPhpCompletionReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.PythonSidecars, (IPythonSidecarCatalogReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationRouteCutover, (HttpContext context, IMigrationRouteCutoverPolicy policy) =>
{
    var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
    return tenant is null ? Results.Problem("Tenant context was not resolved.") : Results.Ok(policy.Decide(tenant));
});

app.MapGet(EcomAeRoutes.MigrationDataParity, (IDataParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationCutoverValidation, (ICutoverValidationReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.SurfaceParity, (ISurfaceParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.TenantContext, (HttpContext context) =>
{
    var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
    return tenant is null ? Results.Problem("Tenant context was not resolved.") : Results.Ok(tenant);
});

app.MapGet(EcomAeRoutes.TenantWorkspaceParity, (ITenantWorkspaceParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.LegacySessionProbe, async (HttpContext context, ILegacySessionValidator validator) =>
{
    var session = await validator.ValidateAsync(context, context.RequestAborted);
    return Results.Ok(new { session.Kind, session.UserId, session.IsAuthenticated, session.Permissions });
});

app.MapGet(EcomAeRoutes.LegacyApiClientParity, (ILegacyApiClientParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.LegacySessionParity, (ILegacySessionParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapEcomAeSurfaceModules();

app.Run();

public partial class Program;
