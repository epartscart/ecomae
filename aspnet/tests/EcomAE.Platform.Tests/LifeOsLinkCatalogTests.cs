using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Spec;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsLinkCatalogTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void FrontendAndBackendInventoriesArePopulated()
    {
        using var sp = Build();
        var spec = sp.GetRequiredService<ILifeOsMasterSpec>();
        Assert.True(LifeOsLinkCatalog.FrontendApps.Count >= 20);
        Assert.True(LifeOsLinkCatalog.Hosts.Count >= 5);
        var backend = LifeOsLinkCatalog.BackendFromSpec(spec);
        Assert.True(backend.Count >= 40);
        Assert.Contains(LifeOsLinkCatalog.FrontendApps, r => r.Path == "/cp/lifeos-guide-app");
        Assert.Contains(LifeOsLinkCatalog.FrontendApps, r => r.Path == "/ip");
        Assert.Contains(LifeOsLinkCatalog.FrontendApps, r => r.Path == "/lifeos");
        Assert.Contains(LifeOsLinkCatalog.FrontendApps, r => r.Path == "/lifeos/spec");
        Assert.Contains(backend, r => r.Path == "/lifeos/spec/json");
        Assert.DoesNotContain(backend, r => r.Path == "/lifeos/spec");
        Assert.Contains(backend, r => r.Path == "/lifeos/orchestrate" && r.Method == "POST");
    }

    [Fact]
    public void EverySpecPartHasAtLeastOneFrontendOrBackendRow()
    {
        using var sp = Build();
        var spec = sp.GetRequiredService<ILifeOsMasterSpec>();
        var all = LifeOsLinkCatalog.All(spec);
        foreach (var part in spec.Parts)
        {
            Assert.True(
                all.Any(r => r.Part == part.Number),
                $"Part {part.Number} missing from LifeOsLinkCatalog");
        }
    }
}
