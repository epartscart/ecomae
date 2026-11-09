using EcomAE.Platform.Storefront;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EcomAE.Package.AutoParts;

/// <summary>
/// Auto Parts industry package entry point. Invoked reflectively by
/// <c>PackageBootstrap.RegisterPackages</c> — compile-time registration,
/// runtime on/off is enforced by the package resolver.
/// </summary>
public static class AutoPartsPackage
{
    public const string PackageId = "industry.auto_parts";

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(nameof(LaximoVinDecodeService));
        services.AddScoped<ILaximoVinDecodeService, LaximoVinDecodeService>();
    }
}
