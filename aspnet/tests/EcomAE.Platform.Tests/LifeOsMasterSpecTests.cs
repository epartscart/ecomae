using System.Text.Json;
using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Orchestrator;
using EcomAE.Platform.LifeOs.Part3;
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
            new LifeOsSpecRuntimeDigests(
                Part2: sp.GetRequiredService<ILifeOsOrchestrator>().ArchitectureDigest()));
        var json = JsonSerializer.Serialize(digest);
        Assert.Contains("Cognitive Systems", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Multimodal", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Plugin", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FORCE_LIVE", json);
        Assert.Contains("SEC-01", json);
        Assert.Contains("/lifeos/roadmap", json, StringComparison.Ordinal);
        Assert.Contains("/lifeos/spec/json", json, StringComparison.Ordinal);
    }

    [Fact]
    public void FullDigest_places_rich_parts_at_top_level_not_nested_under_part2()
    {
        using var sp = Build();
        var orch = sp.GetRequiredService<ILifeOsOrchestrator>().ArchitectureDigest();
        var ai = sp.GetRequiredService<ILifeOsAiCore>().FullPart3Digest();
        var digest = sp.GetRequiredService<ILifeOsMasterSpec>().FullDigest(
            sp.GetRequiredService<ILifeOsCognitiveEngines>(),
            new LifeOsSpecRuntimeDigests(Part2: orch, Part3: ai));

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(digest));
        var root = doc.RootElement;
        Assert.Equal("/lifeos/spec/json", root.GetProperty("self").GetString());
        Assert.Equal("/lifeos/spec", root.GetProperty("ui").GetString());
        Assert.True(root.TryGetProperty("part1", out _));
        Assert.True(root.TryGetProperty("part2", out var part2));
        Assert.True(root.TryGetProperty("part3", out var part3));
        // Regression: module previously stuffed all rich digests into part2.
        Assert.False(part2.TryGetProperty("part3", out _), "part3 must not be nested under part2");
        Assert.False(part2.TryGetProperty("part10", out _), "part10 must not be nested under part2");
        Assert.True(part3.ValueKind is JsonValueKind.Object);
        Assert.Contains(root.GetProperty("links").EnumerateObject(), p => p.Name == "json");
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
