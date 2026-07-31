namespace EcomAE.Platform.Modules;

public static class SurfaceModuleEndpointExtensions
{
    public static IServiceCollection AddEcomAeSurfaceModules(this IServiceCollection services)
    {
        services.AddSingleton<ISurfaceModule, ControlPanelModule>();
        services.AddSingleton<ISurfaceModule, ErpModule>();
        services.AddSingleton<ISurfaceModule, BosModule>();
        services.AddSingleton<ISurfaceModule, StorefrontModule>();
        services.AddSingleton<ISurfaceModule, ApiModule>();
        return services;
    }

    public static IEndpointRouteBuilder MapEcomAeSurfaceModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in endpoints.ServiceProvider.GetServices<ISurfaceModule>())
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
