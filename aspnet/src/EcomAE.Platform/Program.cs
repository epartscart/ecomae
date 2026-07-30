using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Modules;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Security;
using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EcomAeOptions>(builder.Configuration.GetSection(EcomAeOptions.SectionName));
builder.Services.AddSingleton<ITenantRegistry, ConfigurationTenantRegistry>();
builder.Services.AddSingleton<ILegacySessionValidator, HttpLegacySessionValidator>();
builder.Services.AddSingleton<ILegacyApiUsageLogger, MigrationLegacyApiUsageLogger>();
builder.Services.AddSingleton<ITenantResolver, RouteTenantResolver>();
builder.Services.AddEcomAeAuthorization();
builder.Services.AddEcomAeSurfaceModules();
builder.Services.AddSingleton<ISurfaceShellCatalog, MigrationSurfaceShellCatalog>();
builder.Services.AddSingleton<IPriceOfferRepository, MigrationPriceOfferRepository>();
builder.Services.AddSingleton<IPriceLookupService, RepositoryPriceLookupService>();
builder.Services.AddSingleton<IMigrationParityReporter, MigrationParityReporter>();
builder.Services.AddSingleton<IMigrationReadinessReporter, MigrationReadinessReporter>();
builder.Services.AddSingleton<IMigrationCutoverPlanner, MigrationCutoverPlanner>();
builder.Services.AddSingleton<IMigrationProgressReporter, MigrationProgressReporter>();
builder.Services.AddSingleton<ISurfaceParityReporter, SurfaceParityReporter>();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapHealthChecks(EcomAeRoutes.Health);

app.MapGet(EcomAeRoutes.MigrationStatus, (IMigrationParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationReadiness, (IMigrationReadinessReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.MigrationCutoverPlan, (IMigrationCutoverPlanner planner) => Results.Ok(planner.BuildPlan()));

app.MapGet(EcomAeRoutes.MigrationProgress, (IMigrationProgressReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.SurfaceParity, (ISurfaceParityReporter reporter) => Results.Ok(reporter.BuildReport()));

app.MapGet(EcomAeRoutes.TenantContext, (HttpContext context) =>
{
    var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
    return tenant is null ? Results.Problem("Tenant context was not resolved.") : Results.Ok(tenant);
});

app.MapGet(EcomAeRoutes.LegacySessionProbe, async (HttpContext context, ILegacySessionValidator validator) =>
{
    var session = await validator.ValidateAsync(context, context.RequestAborted);
    return Results.Ok(new { session.Kind, session.UserId, session.IsAuthenticated, session.Permissions });
});

app.MapEcomAeSurfaceModules();

app.Run();

public partial class Program;
