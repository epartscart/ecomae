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

    // --- Multi-instance deactivation: two resolvers, separate caches, shared registry ---
    // Simulates two app instances against one registry store. Limitations: the shared
    // store stands in for MariaDB + network; it does not model DB outages mid-request
    // or clock skew — those paths are covered by the fail-closed checks.

    private sealed class SharedRegistryStore
    {
        private readonly Dictionary<string, long> _revisions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _enabled = new(StringComparer.OrdinalIgnoreCase);

        public long Revision(string siteKey) => _revisions.TryGetValue(siteKey, out var r) ? r : 0;

        public IReadOnlySet<string> Enabled(string siteKey)
            => _enabled.TryGetValue(siteKey, out var s)
                ? new HashSet<string>(s, StringComparer.OrdinalIgnoreCase) // snapshot like a DB read
                : new HashSet<string>();

        public void Activate(string siteKey, string packageId)
        {
            if (!_enabled.TryGetValue(siteKey, out var s))
            {
                s = _enabled[siteKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            s.Add(packageId);
            _revisions[siteKey] = Revision(siteKey) + 1;
        }

        public void Deactivate(string siteKey, string packageId)
        {
            if (_enabled.TryGetValue(siteKey, out var s))
            {
                s.Remove(packageId);
            }

            _revisions[siteKey] = Revision(siteKey) + 1;
        }
    }

    private static PackageResolver Instance(SharedRegistryStore store, TimeSpan revisionTtl)
        => new(
            (siteKey, _) => Task.FromResult(store.Revision(siteKey)),
            (siteKey, _) => Task.FromResult(store.Enabled(siteKey)),
            new MemoryCache(new MemoryCacheOptions()),
            revisionTtl);

    [Fact]
    public async Task MultiInstance_DeactivationPropagatesWithinBound()
    {
        var store = new SharedRegistryStore();
        var revisionBound = TimeSpan.FromMilliseconds(60);
        var instanceA = Instance(store, revisionBound);
        var instanceB = Instance(store, revisionBound);

        // 2. Activate for the tenant; 3. both instances allow it.
        store.Activate("auto247", "industry.auto_parts");
        Assert.True(await instanceA.IsEnabledAsync("auto247", "industry.auto_parts"));
        Assert.True(await instanceB.IsEnabledAsync("auto247", "industry.auto_parts"));

        // B must hold the package in its local cache before deactivation (second hit is cache-served).
        Assert.True(await instanceB.IsEnabledAsync("auto247", "industry.auto_parts"));

        // 4. Deactivate through instance A — the bound is measured from the commit.
        var committedAt = DateTime.UtcNow;
        store.Deactivate("auto247", "industry.auto_parts");
        instanceA.Invalidate("auto247");
        Assert.False(await instanceA.IsEnabledAsync("auto247", "industry.auto_parts"));

        // 5. Instance B must stop within the bound measured from the commit.
        var stoppedAt = DateTime.MinValue;
        var deadline = committedAt + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (!await instanceB.IsEnabledAsync("auto247", "industry.auto_parts"))
            {
                stoppedAt = DateTime.UtcNow;
                break;
            }

            await Task.Delay(5);
        }

        Assert.NotEqual(DateTime.MinValue, stoppedAt);
        var elapsed = stoppedAt - committedAt;
        Assert.True(elapsed <= revisionBound + TimeSpan.FromMilliseconds(250),
            $"instance B executed the disabled package for {elapsed.TotalMilliseconds}ms after commit");

        // Unaffected tenant keeps executing throughout.
        store.Activate("ecomae", "industry.auto_parts");
        Assert.True(await instanceB.IsEnabledAsync("ecomae", "industry.auto_parts"));
    }

    [Fact]
    public async Task MultiInstance_RevisionCheckingDisabled_ServesStaleForever()
    {
        // Negative control: a resolver pinned to a stale revision never sees the
        // deactivation — proving the revision check is what enforces the bound.
        var store = new SharedRegistryStore();
        store.Activate("auto247", "industry.auto_parts");

        var stalePinned = new PackageResolver(
            (siteKey, _) => Task.FromResult(1L), // frozen at pre-deactivation revision
            (siteKey, _) => Task.FromResult(store.Enabled(siteKey)),
            new MemoryCache(new MemoryCacheOptions()),
            TimeSpan.FromMilliseconds(40));

        Assert.True(await stalePinned.IsEnabledAsync("auto247", "industry.auto_parts"));
        store.Deactivate("auto247", "industry.auto_parts");
        await Task.Delay(120);

        Assert.True(await stalePinned.IsEnabledAsync("auto247", "industry.auto_parts"));
    }

    [Fact]
    public async Task MultiInstance_UnverifiableRevision_FailsClosed()
    {
        var store = new SharedRegistryStore();
        store.Activate("auto247", "industry.auto_parts");

        // Instance with a throwing revision loader: must never serve a stale enabled set.
        var failing = new PackageResolver(
            (siteKey, _) => Task.FromException<long>(new InvalidOperationException("db unreachable")),
            (siteKey, _) => Task.FromResult(store.Enabled(siteKey)),
            new MemoryCache(new MemoryCacheOptions()),
            TimeSpan.FromMilliseconds(50));

        Assert.False(await failing.IsEnabledAsync("auto247", "industry.auto_parts"));
        Assert.Empty(await failing.GetEnabledAsync("auto247"));
    }

    [Fact]
    public async Task MultiInstance_TenantChange_DoesNotAffectOtherTenants()
    {
        var store = new SharedRegistryStore();
        var instanceA = Instance(store, TimeSpan.FromMilliseconds(40));
        var instanceB = Instance(store, TimeSpan.FromMilliseconds(40));

        store.Activate("auto247", "industry.auto_parts");
        store.Activate("ecomae", "industry.auto_parts");
        Assert.True(await instanceB.IsEnabledAsync("ecomae", "industry.auto_parts"));

        store.Deactivate("auto247", "industry.auto_parts");
        instanceA.Invalidate("auto247");
        await Task.Delay(80);

        Assert.False(await instanceB.IsEnabledAsync("auto247", "industry.auto_parts"));
        Assert.True(await instanceB.IsEnabledAsync("ecomae", "industry.auto_parts"));
    }
}
