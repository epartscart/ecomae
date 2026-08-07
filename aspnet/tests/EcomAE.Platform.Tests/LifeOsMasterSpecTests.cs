using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Orchestrator;
using EcomAE.Platform.LifeOs.Spec;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsMasterSpecTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void MasterSpecHasTenParts()
    {
        using var sp = Build();
        var spec = sp.GetRequiredService<ILifeOsMasterSpec>();
        Assert.Equal("4.0", spec.Version);
        Assert.Equal(10, spec.Parts.Count);
        Assert.Equal(1, spec.Parts.First().Number);
        Assert.Equal(10, spec.Parts.Last().Number);
    }

    [Fact]
    public void CognitiveEnginesProduceExplainableDecision()
    {
        using var sp = Build();
        var cog = sp.GetRequiredService<ILifeOsCognitiveEngines>();
        var trace = cog.Reason("Schedule tomorrow's meeting.", ["calendar:0.55", "voice:0.92"]);
        Assert.NotEmpty(trace.Steps);
        var decision = cog.Decide(trace, allowIrreversible: false);
        Assert.True(decision.RequiresHumanApproval);
        Assert.Contains("confirmation", decision.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FullDigestCoversPartsThreeToTen()
    {
        using var sp = Build();
        var digest = sp.GetRequiredService<ILifeOsMasterSpec>().FullDigest(
            sp.GetRequiredService<ILifeOsCognitiveEngines>(),
            sp.GetRequiredService<ILifeOsOrchestrator>().ArchitectureDigest());
        var json = System.Text.Json.JsonSerializer.Serialize(digest);
        Assert.Contains("Cognitive Systems", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Multimodal", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Plugin", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FORCE_LIVE", json);
        Assert.Contains("SEC-01", json);
    }

    [Fact]
    public void MultimodalAndPluginRegistriesPopulated()
    {
        using var sp = Build();
        var spec = sp.GetRequiredService<ILifeOsMasterSpec>();
        Assert.Contains(spec.MultimodalAdapters, a => a.Key == "voice");
        Assert.Contains(spec.Clients, c => c.Key == "web");
        Assert.Contains(spec.Plugins, p => p.Id == "sdk-agent");
        Assert.True(spec.ApiSurfaces.Count >= 10);
    }
}
