namespace EcomAE.Platform.Packages;

/// <summary>
/// Compile-time deployed-package catalog (Phase 1): packages ship in the platform image and
/// register here; runtime gating (install/activate) is per tenant via the registry tables.
/// Dynamic assembly loading is deferred by design.
/// </summary>
public static class PackageCatalog
{
    public const string CorePackageId = "ecomae.core";
    public const string CoreVersion = "3.0.0";

    /// <summary>Deployed package rows synced into <c>epc_packages</c> (status <c>deployed</c>).</summary>
    public static readonly IReadOnlyList<DeployedPackage> Deployed =
    [
        new(
            CorePackageId,
            "EcomAE Standard Core",
            PackageTypes.Core,
            CoreVersion,
            CoreMinVersion: null,
            CoreMaxVersion: null,
            OwnerSiteKey: null,
            Status: "installed"),
        new(
            "industry.auto_parts",
            "Auto Parts Industry Package",
            PackageTypes.Industry,
            "1.0.0",
            CoreMinVersion: "3.0.0",
            CoreMaxVersion: "4.0.0",
            OwnerSiteKey: null,
            Status: "deployed"),
        new(
            "customer.auto247",
            "Auto247 Customer Extension",
            PackageTypes.Customer,
            "1.0.0",
            CoreMinVersion: "3.0.0",
            CoreMaxVersion: "4.0.0",
            // Customer package ownership: activatable only for the authorized tenant.
            OwnerSiteKey: "auto247",
            Status: "deployed"),
    ];

    public static DeployedPackage? Find(string packageId)
        => Deployed.FirstOrDefault(p =>
            string.Equals(p.PackageId, packageId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Manifest <c>depends_on</c> for compile-time packages (Phase 1).</summary>
    public static IReadOnlyList<string> DependenciesOf(string packageId)
        => packageId switch
        {
            "customer.auto247" => ["industry.auto_parts", CorePackageId],
            "industry.auto_parts" => [CorePackageId],
            _ => [],
        };
}
