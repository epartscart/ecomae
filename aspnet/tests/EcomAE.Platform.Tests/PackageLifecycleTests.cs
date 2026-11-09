using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Packages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PackageLifecycleTests
{
    private static TenantPackageInstall Installed(string siteKey, string packageId, bool enabled = true)
        => new(siteKey, packageId, "1.0.0", "installed", "none", enabled, null, null, null);

    // --- Dependency validation ---

    [Fact]
    public void Activate_CustomerPackage_RequiresIndustryDependencyEnabled()
    {
        var deployed = PackageCatalog.Find("customer.auto247");
        var error = PackageRules.ValidateLifecycle(
            deployed, "auto247", "customer.auto247",
            Installed("auto247", "customer.auto247", enabled: false),
            forInstall: false,
            dep => dep == "industry.auto_parts" ? Installed("auto247", dep, enabled: false) : null);

        Assert.Equal("Requires industry.auto_parts activated first.", error);
    }

    [Fact]
    public void Activate_CustomerPackage_PassesWhenDependenciesEnabled()
    {
        var deployed = PackageCatalog.Find("customer.auto247");
        var error = PackageRules.ValidateLifecycle(
            deployed, "auto247", "customer.auto247",
            Installed("auto247", "customer.auto247", enabled: false),
            forInstall: false,
            dep => dep == "industry.auto_parts" ? Installed("auto247", dep) : null);

        Assert.Null(error);
    }

    [Fact]
    public void Activate_UnknownPackage_IsRejected()
    {
        var error = PackageRules.ValidateLifecycle(
            null, "auto247", "bogus.pkg", null, forInstall: false, _ => null);

        Assert.Equal("Unknown package.", error);
    }

    [Fact]
    public void Activate_RequiresPriorInstall()
    {
        var deployed = PackageCatalog.Find("industry.auto_parts");
        var error = PackageRules.ValidateLifecycle(
            deployed, "auto247", "industry.auto_parts",
            install: null, forInstall: false, _ => null);

        Assert.Equal("Install the package for this tenant first.", error);
    }

    // --- Tenant isolation (customer package ownership) ---

    [Fact]
    public void Install_CustomerPackage_RejectedForOtherTenant()
    {
        var deployed = PackageCatalog.Find("customer.auto247");
        var error = PackageRules.ValidateLifecycle(
            deployed, "ecomae", "customer.auto247",
            install: null, forInstall: true, _ => null);

        Assert.Contains("auto247", error);
    }

    [Fact]
    public void Activate_CustomerPackage_RejectedForOtherTenant()
    {
        var deployed = PackageCatalog.Find("customer.auto247");
        var error = PackageRules.ValidateLifecycle(
            deployed, "ecomae", "customer.auto247",
            Installed("ecomae", "customer.auto247", enabled: false),
            forInstall: false, _ => null);

        Assert.Contains("auto247", error);
    }

    [Fact]
    public void Install_IndustryPackage_AllowedForAnyTenant()
    {
        var deployed = PackageCatalog.Find("industry.auto_parts");
        var error = PackageRules.ValidateLifecycle(
            deployed, "ecomae", "industry.auto_parts",
            install: null, forInstall: true, _ => null);

        Assert.Null(error);
    }

    // --- Resolver: fail-closed + core always on + cache invalidation ---

    [Fact]
    public async Task Resolver_UnconfiguredFactory_FailsClosed()
    {
        var resolver = new PackageResolver(
            new UnconfiguredFactory(), new MemoryCache(new MemoryCacheOptions()),
            NullLogger<PackageResolver>.Instance);

        var set = await resolver.GetEnabledAsync("auto247");
        Assert.Empty(set);
        Assert.False(await resolver.IsEnabledAsync("auto247", "industry.auto_parts"));
    }

    [Fact]
    public async Task Resolver_CorePackage_AlwaysEnabled()
    {
        var resolver = new PackageResolver(
            new UnconfiguredFactory(), new MemoryCache(new MemoryCacheOptions()),
            NullLogger<PackageResolver>.Instance);

        Assert.True(await resolver.IsEnabledAsync("ecomae", PackageCatalog.CorePackageId));
        Assert.True(await resolver.IsEnabledAsync("auto247", PackageCatalog.CorePackageId));
    }

    [Fact]
    public async Task Resolver_BlankSiteKey_FailsClosed()
    {
        var resolver = new PackageResolver(
            new UnconfiguredFactory(), new MemoryCache(new MemoryCacheOptions()),
            NullLogger<PackageResolver>.Instance);

        Assert.Empty(await resolver.GetEnabledAsync(""));
        Assert.False(await resolver.IsEnabledAsync(string.Empty, "industry.auto_parts"));
    }

    private sealed class UnconfiguredFactory : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("unconfigured");
    }
}
