using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Part3;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPart3CognitiveTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void PerceptionVoicePipelineHasAsrAndIntent()
    {
        var perception = new LifeOsPerceptionEngine();
        var result = perception.Perceive(LifeOsEventFactory.SampleVoice());
        Assert.Equal("voice", result.Modality);
        Assert.Contains(result.Pipeline, s => s.Name == "Speech Recognition");
        Assert.Contains(result.Pipeline, s => s.Name == "Intent Recognition");
        Assert.Contains(result.ExtractedEntities, e => e is "meeting" or "tomorrow");
    }

    [Fact]
    public void ContextEngineBuildsCrmSampleShape()
    {
        var ctxEngine = new LifeOsContextEngine();
        var evt = LifeOsEventFactory.SampleVoice();
        var ctx = ctxEngine.Build(evt);
        var crm = ctxEngine.BuildCurrentReality(evt, ctx);
        Assert.Equal("Meeting Prep", crm.Activity);
        Assert.Equal("MEDIUM", crm.Interruptibility);
        Assert.InRange(crm.FocusScore, 1, 100);
        Assert.NotNull(crm.CalendarEvent);
    }

    [Fact]
    public void EthicalLayerBlocksWithoutPermissionWhenIrreversible()
    {
        var ethics = new LifeOsEthicalAiLayer();
        var blocked = ethics.Validate("wipe data", 0.9, userPermission: false, irreversible: true);
        Assert.False(blocked.Allowed);
        var ok = ethics.Validate("suggest break", 0.8, userPermission: false, irreversible: false);
        Assert.True(ok.Allowed);
    }

    [Fact]
    public void DecisionScoreFormulaSubtractsRisk()
    {
        var highRisk = LifeOsDecisionScore.Compute(0.7, 0.7, 0.7, risk: 0.9, 0.7, 0.7, 0.7);
        var lowRisk = LifeOsDecisionScore.Compute(0.7, 0.7, 0.7, risk: 0.1, 0.7, 0.7, 0.7);
        Assert.True(lowRisk.Total > highRisk.Total);
    }

    [Fact]
    public void AiCoreRunsFullCognitiveCycle()
    {
        using var sp = Build();
        var ai = sp.GetRequiredService<ILifeOsAiCore>();
        var cycle = ai.RunCycle(LifeOsEventFactory.SampleVoice(), userPermission: false);

        Assert.StartsWith("CYC-", cycle.CycleId);
        Assert.Equal(12, cycle.CycleStages.Count);
        Assert.Equal(4, cycle.Reasoning.Count);
        Assert.Contains(cycle.Reasoning, r => r.Method == LifeOsReasoningMethod.Logical);
        Assert.NotEmpty(cycle.Predictions);
        Assert.True(cycle.Emotion.UserMayOverride);
        Assert.NotNull(cycle.Ethics);
        Assert.Contains("Perception", cycle.CycleStages);
        Assert.Contains("Continuous Improvement", cycle.CycleStages);
    }

    [Fact]
    public void Part3DigestIncludesChapters12To25()
    {
        using var sp = Build();
        var json = System.Text.Json.JsonSerializer.Serialize(
            sp.GetRequiredService<ILifeOsAiCore>().FullPart3Digest());
        Assert.Contains("Perception Engine", json);
        Assert.Contains("Ethical AI Layer", json);
        Assert.Contains("Self-Reflection", json);
        Assert.Contains("Unified Cognitive Cycle", json);
        Assert.Contains("Current Reality Model", json);
    }

    [Fact]
    public void LaunchLifeOsPlanMatchesChapter18Example()
    {
        var plan = new LifeOsPlanningEngine().Decompose("Launch LifeOS");
        Assert.Equal(10, plan.Tasks.Count);
        Assert.Equal("Research", plan.Tasks[0].Title);
        Assert.Equal("Launch", plan.Tasks[^1].Title);
    }
}
