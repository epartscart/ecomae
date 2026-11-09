using EcomAE.Platform.Packages;

namespace EcomAE.Package.AutoParts;

/// <summary>
/// Auto Parts surface contributions (Slice 1c): the VIN surface is owned by the
/// industry package — its routes are blocked at the API gate and its menu entries
/// disappear when the package is disabled for the tenant.
/// </summary>
public sealed class AutoPartsRouteContributor : IPackageRouteContributor
{
    public string PackageId => AutoPartsPackage.PackageId;

    public IReadOnlyList<string> RoutePrefixes { get; } = ["/storefront/vin"];
}

public sealed class AutoPartsMenuContributor : IPackageMenuContributor
{
    public string PackageId => AutoPartsPackage.PackageId;

    public IReadOnlyList<PackageMenuItem> MenuItems { get; } =
    [
        new("storefront", "VIN lookup", "/storefront/vin-app", 10),
        new("cp", "VIN fields", "/cp/vin-fields-app", 40),
    ];
}
