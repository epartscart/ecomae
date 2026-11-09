using EcomAE.Platform.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EcomAE.Customer.Auto247;

/// <summary>
/// Auto247 customer package entry point. Invoked reflectively by
/// <c>PackageBootstrap.RegisterPackages</c>. Customer-specific storefront skin
/// lives here — never in Standard Core.
/// </summary>
public static class Auto247Package
{
    public const string PackageId = "customer.auto247";
    public const string OwnerSiteKey = "auto247";

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICustomerSkinContributor, Auto247SkinContributor>();
    }
}
