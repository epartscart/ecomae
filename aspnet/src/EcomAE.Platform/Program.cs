using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Security;
using EcomAE.Platform.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EcomAeOptions>(builder.Configuration.GetSection(EcomAeOptions.SectionName));
builder.Services.AddSingleton<ITenantRegistry, ConfigurationTenantRegistry>();
builder.Services.AddSingleton<ILegacySessionValidator, HttpLegacySessionValidator>();
builder.Services.AddSingleton<ITenantResolver, RouteTenantResolver>();
builder.Services.AddEcomAeAuthorization();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapHealthChecks(EcomAeRoutes.Health);

app.MapGet(EcomAeRoutes.MigrationStatus, () => Results.Ok(new
{
    service = "EcomAE ASP.NET Core platform foundation",
    status = "started",
    target = "Replace PHP CP, ERP, BOS, storefront, API and worker surfaces in phases",
    phpRuntime = "kept during migration only",
    finalState = "zero PHP files and no PHP runtime"
}));

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

app.MapGet(EcomAeRoutes.ControlPanel, () => Results.Ok(new { surface = "Super CP / tenant CP", migration = "placeholder" }));
app.MapGet(EcomAeRoutes.Erp, () => Results.Ok(new { surface = "Super ERP / tenant ERP", migration = "placeholder" }));
app.MapGet(EcomAeRoutes.Bos, () => Results.Ok(new { surface = "Super BOS", migration = "placeholder" }));

app.Run();

public partial class Program;
