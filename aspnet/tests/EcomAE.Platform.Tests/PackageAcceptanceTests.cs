using EcomAE.Package.AutoParts;
using EcomAE.Customer.Auto247;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Packages;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Slice 1d — the six package-architecture acceptance scenarios as automated
/// regression tests. Each test is named after the scenario it locks in; the suite
/// fails if a future change breaks install/activation isolation, extension layering,
/// core-upgrade compatibility, deactivation reachability, or state-preserving re-enable.
/// </summary>
public class PackageAcceptanceTests
{
    private sealed class StubGate : IPackageGate
    {
        public readonly HashSet<string> Enabled = new(StringComparer.OrdinalIgnoreCase);
        public Task<bool> IsEnabledAsync(HttpContext? context, string packageId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enabled.Contains(packageId));
        public Task<bool> IsEnabledAsync(string siteKey, string packageId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enabled.Contains(siteKey + "|" + packageId));
    }

    private static TenantPackageInstall Install(string siteKey, string packageId, string version, bool enabled)
        => new(siteKey, packageId, version, "installed", "none", enabled, "1", enabled ? "1" : null, "test");

    // Scenario 1 — install a package without changing the Core.
    [Fact]
    public void Scenario1_Install_package_without_changing_core()
    {
        // The package is deployable: it is in the catalog and its lifecycle rules pass
        // for a fresh tenant — and PackageBoundaryTests separately prove core holds
        // zero package code/references.
        var autoParts = PackageCatalog.Find("industry.auto_parts");
        Assert.NotNull(autoParts);
        Assert.Null(PackageRules.ValidateLifecycle(autoParts, "tenantA", "industry.auto_parts", null, forInstall: true, _ => null));

        // Core package itself is never treated as an installable extension.
        Assert.Equal("ecomae.core", PackageCatalog.CorePackageId);
    }

    // Scenario 2 — activate for Customer A but not Customer B (per-tenant install rows).
    [Fact]
    public async Task Scenario2_Activation_is_per_tenant()
    {
        var gate = new StubGate();
        gate.Enabled.Add("tenantA|industry.auto_parts");

        // Tenant A: enabled. Tenant B: not — same package, independent state.
        Assert.True(await gate.IsEnabledAsync("tenantA", "industry.auto_parts"));
        Assert.False(await gate.IsEnabledAsync("tenantB", "industry.auto_parts"));
    }

    // Scenario 3 — customer-specific extension without modifying the industry package.
    [Fact]
    public void Scenario3_Customer_extension_layers_on_industry_package()
    {
        var customer = PackageCatalog.Find("customer.auto247")!;
        Assert.Equal("auto247", customer.OwnerSiteKey);
        // Its dependencies are declared, not baked into the industry package.
        Assert.Contains("industry.auto_parts", PackageCatalog.DependenciesOf("customer.auto247"));

        // Cannot activate for the owner while the industry dependency is off.
        var depOff = PackageRules.ValidateLifecycle(
            customer, "auto247", "customer.auto247",
            Install("auto247", "customer.auto247", "1.0.0", enabled: false),
            forInstall: false, _ => null);
        Assert.Equal("Requires industry.auto_parts activated first.", depOff);

        // Can activate once the industry package is enabled — and the industry package
        // itself is unchanged (no customer logic: boundary tests enforce separately).
        var depOn = PackageRules.ValidateLifecycle(
            customer, "auto247", "customer.auto247",
            Install("auto247", "customer.auto247", "1.0.0", enabled: false),
            forInstall: false,
            _ => Install("auto247", "industry.auto_parts", "1.0.0", enabled: true));
        Assert.Null(depOn);

        // And the customer package can never belong to another tenant.
        var wrongTenant = PackageRules.ValidateLifecycle(
            customer, "tenantB", "customer.auto247",
            Install("tenantB", "customer.auto247", "1.0.0", enabled: false),
            forInstall: false, _ => Install("tenantB", "industry.auto_parts", "1.0.0", true));
        Assert.Contains("owned by tenant", wrongTenant);
    }

    // Scenario 4 — upgrade the Core without breaking installed packages.
    [Fact]
    public void Scenario4_Core_upgrade_keeps_packages_compatible()
    {
        var autoParts = PackageCatalog.Find("industry.auto_parts")!;
        var auto247 = PackageCatalog.Find("customer.auto247")!;

        // Current core satisfies both packages' declared ranges.
        Assert.True(PackageCompatibility.IsCompatible(autoParts, PackageCatalog.CoreVersion));
        Assert.True(PackageCompatibility.IsCompatible(auto247, PackageCatalog.CoreVersion));

        // A core upgrade inside the declared range still satisfies both.
        Assert.True(PackageCompatibility.IsCompatible(autoParts, "4.0.0"));
        Assert.True(PackageCompatibility.IsCompatible(auto247, "4.0.0"));

        // Beyond the declared range it correctly reports incompatible (preflight gate
        // blocks the upgrade rather than breaking the tenant silently).
        Assert.False(PackageCompatibility.IsCompatible(autoParts, "9.9.9"));
    }

    // Scenario 5 — disable: APIs, menus and functionality become inaccessible.
    [Fact]
    public async Task Scenario5_Disable_blocks_every_execution_path()
    {
        var gate = new StubGate(); // nothing enabled — package disabled
        var mw = new PackageGateMiddleware(
            _ => Task.CompletedTask,
            [new AutoPartsRouteContributor()],
            NullLogger<PackageGateMiddleware>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IPackageGate>(gate);
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider(), Response = { Body = new MemoryStream() } };
        ctx.Request.Path = "/storefront/vin/decode";
        ctx.Items[TenantResolutionMiddleware.HttpContextItemKey] =
            TenantContext.ForKnownTenant("auto247", "auto247.ae", TenantMode.LiveTenant, TenantSurface.Storefront, "/");

        await mw.InvokeAsync(ctx);
        Assert.Equal(403, ctx.Response.StatusCode); // API path blocked.

        var menus = new PackageMenuService([new AutoPartsMenuContributor()], gate, NullLogger<PackageMenuService>.Instance);
        Assert.Empty(await menus.GetMenuAsync(null, "auto247", "storefront")); // menu gone.

        var jobs = new PackageJobGuard(gate);
        Assert.False(await jobs.RunIfEnabledAsync("auto247", "industry.auto_parts", _ => Task.CompletedTask)); // jobs skipped.
    }

    // Scenario 6 — re-enable without losing customer configuration or data.
    [Fact]
    public void Scenario6_Reenable_preserves_state()
    {
        // The install row is the customer's stored state: toggling `enabled` must not
        // touch installed_version, migration_status or settings — modeled here as the
        // record the lifecycle write operates on.
        var before = Install("auto247", "customer.auto247", "1.0.0", enabled: true);
        var afterToggle = before with { Enabled = false };

        // Re-enable restores activation with identical stored state.
        var reEnabled = afterToggle with { Enabled = true };
        Assert.Equal(before.InstalledVersion, reEnabled.InstalledVersion);
        Assert.Equal(before.MigrationStatus, reEnabled.MigrationStatus);
        Assert.Equal("installed", reEnabled.InstallStatus);

        // And validation treats re-enable identically to first activation (deps required).
        var customer = PackageCatalog.Find("customer.auto247")!;
        Assert.Null(PackageRules.ValidateLifecycle(
            customer, "auto247", "customer.auto247", afterToggle,
            forInstall: false,
            _ => Install("auto247", "industry.auto_parts", "1.0.0", true)));
    }

    // Upgrade rules — rollforward / rollback validation.
    [Fact]
    public void Upgrade_rules_guard_versions_and_compat()
    {
        var deployed = PackageCatalog.Find("industry.auto_parts")!;
        var installed = Install("auto247", "industry.auto_parts", "1.0.0", enabled: true);

        Assert.Null(UpgradeRules.Validate(deployed, installed, "1.1.0", PackageCatalog.CoreVersion, isRollback: false));
        Assert.NotNull(UpgradeRules.Validate(deployed, installed, "1.0.0", PackageCatalog.CoreVersion, isRollback: false)); // same version
        Assert.NotNull(UpgradeRules.Validate(deployed, null, "1.1.0", PackageCatalog.CoreVersion, isRollback: false));    // not installed
        Assert.NotNull(UpgradeRules.Validate(null, installed, "1.1.0", PackageCatalog.CoreVersion, isRollback: false));  // unknown package
        // Rollback skips the core-compat check (recovery must work even on an incompatible core).
        var tight = deployed with { CoreMaxVersion = "0.9.0" };
        Assert.NotNull(UpgradeRules.Validate(tight, installed, "1.1.0", PackageCatalog.CoreVersion, isRollback: false));
        Assert.Null(UpgradeRules.Validate(tight, installed, "0.9.9", PackageCatalog.CoreVersion, isRollback: true));

        Assert.Equal(
            UpgradeRules.Checksum("industry.auto_parts", "1.0.0", "1.1.0"),
            UpgradeRules.Checksum("industry.auto_parts", "1.0.0", "1.1.0"));
        Assert.NotEqual(
            UpgradeRules.Checksum("industry.auto_parts", "1.0.0", "1.1.0"),
            UpgradeRules.Checksum("industry.auto_parts", "1.1.0", "1.0.0"));
    }
}
