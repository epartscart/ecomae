using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Slice 1b boundary guards: Standard Core contains no package-specific
/// business logic — the Auto247 skin contributor and the Auto Parts VIN
/// implementation live in their own assemblies and are reached only through
/// core extension points.
/// </summary>
public class PackageBoundaryTests
{
    [Fact]
    public void Skin_extension_point_lives_in_core_not_the_customer_package()
    {
        Assert.Equal("EcomAE.Platform",
            typeof(EcomAE.Platform.Presentation.ICustomerSkinContributor).Assembly.GetName().Name);

        // The old Auto247Theme helper must no longer exist inside Standard Core.
        Assert.Null(typeof(EcomAE.Platform.Middleware.TenantResolutionMiddleware)
            .Assembly.GetType("EcomAE.Platform.Presentation.Auto247Theme"));
    }

    [Fact]
    public void Vin_decode_implementation_lives_in_the_auto_parts_package()
    {
        // Contract stays in core; implementation is the package's.
        Assert.Equal("EcomAE.Platform", typeof(EcomAE.Platform.Storefront.ILaximoVinDecodeService)
            .Assembly.GetName().Name);
        Assert.Equal("EcomAE.Package.AutoParts", typeof(EcomAE.Package.AutoParts.LaximoVinDecodeService)
            .Assembly.GetName().Name);
        Assert.True(typeof(EcomAE.Package.AutoParts.LaximoVinDecodeService)
            .IsAssignableTo(typeof(EcomAE.Platform.Storefront.ILaximoVinDecodeService)));
    }

    [Fact]
    public void Standard_core_declares_no_package_namespaces()
    {
        var core = typeof(EcomAE.Platform.Middleware.TenantResolutionMiddleware).Assembly;
        var offenders = core.GetTypes()
            .Where(t => t.FullName is not null
                && (t.FullName.StartsWith("EcomAE.Customer.", StringComparison.Ordinal)
                    || t.FullName.StartsWith("EcomAE.Package.", StringComparison.Ordinal)))
            .Select(t => t.FullName!)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Core_references_no_package_assembly()
    {
        var referenced = typeof(EcomAE.Platform.Middleware.TenantResolutionMiddleware)
            .Assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();
        Assert.DoesNotContain("EcomAE.Customer.Auto247", referenced);
        Assert.DoesNotContain("EcomAE.Package.AutoParts", referenced);
    }
}
