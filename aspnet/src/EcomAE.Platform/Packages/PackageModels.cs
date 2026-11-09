namespace EcomAE.Platform.Packages;

/// <summary>Package types per the extensibility architecture: core / extension / industry / customer.</summary>
public static class PackageTypes
{
    public const string Core = "core";
    public const string Extension = "extension";
    public const string Industry = "industry";
    public const string Customer = "customer";
}

/// <summary>Globally deployed package (registry <c>epc_packages</c>) — present on the platform, inert until installed+activated per tenant.</summary>
public sealed record DeployedPackage(
    string PackageId,
    string Name,
    string PackageType,
    string Version,
    string? CoreMinVersion,
    string? CoreMaxVersion,
    string? OwnerSiteKey,        // customer packages: exclusive tenant assignment
    string Status);

/// <summary>Per-tenant installation/activation record (<c>epc_tenant_package_installs</c>).</summary>
public sealed record TenantPackageInstall(
    string SiteKey,
    string PackageId,
    string InstalledVersion,
    string InstallStatus,
    string MigrationStatus,
    bool Enabled,
    string? InstalledAt,
    string? ActivatedAt,
    string? ActivatedBy);

/// <summary>
/// Pure activation/install decision rules — kept side-effect free so the dependency,
/// ownership and isolation gates are unit-testable without a database.
/// </summary>
public static class PackageRules
{
    /// <summary>
    /// Validates an install or activate request. Returns an error message or null.
    /// </summary>
    public static string? ValidateLifecycle(
        DeployedPackage? deployed,
        string siteKey,
        string packageId,
        TenantPackageInstall? install,
        bool forInstall,
        Func<string, TenantPackageInstall?> dependencyLookup)
    {
        if (deployed is null)
        {
            return "Unknown package.";
        }

        // Customer-package ownership: bound exclusively to the authorized tenant — applies
        // identically to CP actions and direct API calls.
        if (!string.IsNullOrWhiteSpace(deployed.OwnerSiteKey)
            && !string.Equals(deployed.OwnerSiteKey, siteKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"Package is owned by tenant '{deployed.OwnerSiteKey}'.";
        }

        if (forInstall)
        {
            return null;
        }

        if (install is null)
        {
            return "Install the package for this tenant first.";
        }

        // Dependency gate: each declared dependency must be installed AND enabled for the
        // tenant (the core package is always on).
        foreach (var dep in PackageCatalog.DependenciesOf(packageId))
        {
            if (dep == PackageCatalog.CorePackageId)
            {
                continue;
            }

            var depInstall = dependencyLookup(dep);
            if (depInstall is null || !depInstall.Enabled)
            {
                return $"Requires {dep} activated first.";
            }
        }

        return null;
    }
}
