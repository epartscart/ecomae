using EcomAE.Platform.Configuration;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EcomAeOptions>(builder.Configuration.GetSection(EcomAeOptions.SectionName));
builder.Services.AddSingleton<ITenantResolver, RouteTenantResolver>();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapHealthChecks("/health");

app.MapGet("/migration/status", () => Results.Ok(new
{
    service = "EcomAE ASP.NET Core platform foundation",
    status = "started",
    target = "Replace PHP CP, ERP, BOS, storefront, API and worker surfaces in phases",
    phpRuntime = "kept during migration only",
    finalState = "zero PHP files and no PHP runtime"
}));

app.MapGet("/tenant/context", (HttpContext context) =>
{
    var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
    return tenant is null ? Results.Problem("Tenant context was not resolved.") : Results.Ok(tenant);
});

app.MapGet("/CP", () => Results.Ok(new { surface = "Super CP / tenant CP", migration = "placeholder" }));
app.MapGet("/ERP", () => Results.Ok(new { surface = "Super ERP / tenant ERP", migration = "placeholder" }));
app.MapGet("/BOS", () => Results.Ok(new { surface = "Super BOS", migration = "placeholder" }));

app.Run();

public partial class Program;
