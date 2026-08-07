using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Part9;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPart9EcosystemTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void EcosystemVisionHasAnalogsAndSixLayers()
    {
        using var sp = Build();
        var eco = sp.GetRequiredService<ILifeOsEcosystemPlatform>();
        Assert.Contains(eco.EcosystemAnalogs, a => a.Key == "android");
        Assert.Contains(eco.EcosystemAnalogs, a => a.Key == "aws");
        Assert.Equal(6, eco.PlatformLayers.Count);
        Assert.Equal(1, eco.PlatformLayers.First().Number);
        Assert.Equal(6, eco.PlatformLayers.Last().Number);
    }

    [Fact]
    public void MarketplaceAndAgentStoreArePopulated()
    {
        using var sp = Build();
        var eco = sp.GetRequiredService<ILifeOsEcosystemPlatform>();
        Assert.True(eco.MarketplaceStores.Count >= 9);
        Assert.Equal(20, eco.AgentCategories.Count);
        Assert.Equal("agent.finance.v1", eco.SampleAgentListing.AgentId);
        Assert.Contains(eco.SampleAgentListing.Permissions, p => p == "finance.read");
    }

    [Fact]
    public void PluginsAppsWorkflowsAndKnowledgeExist()
    {
        using var sp = Build();
        var eco = sp.GetRequiredService<ILifeOsEcosystemPlatform>();
        Assert.Contains(eco.PluginExamples, p => p.Key == "slack");
        Assert.Contains(eco.PluginLifecycle, s => s == "Permission Review");
        Assert.Contains(eco.ApplicationExamples, a => a.Key == "hospital");
        Assert.Contains(eco.WorkflowTemplates, w => w.Key == "invoice");
        Assert.Contains(eco.KnowledgePacks, k => k.Key == "legal");
    }

    [Fact]
    public void DeveloperPlatformCoversSdkApisAndCli()
    {
        using var sp = Build();
        var eco = sp.GetRequiredService<ILifeOsEcosystemPlatform>();
        Assert.Contains(eco.SdkLanguages, l => l.Key == "csharp");
        Assert.Contains(eco.PublicApis, a => a.Path == "/workflow" && a.Method == "POST");
        Assert.Contains(eco.CliCommands, c => c.Command == "life publish");
        Assert.Contains(eco.DeveloperPortalModules, m => m == "Agent SDK");
    }

    [Fact]
    public void BillingCertificationPartnersAndRoadmapAreDefined()
    {
        using var sp = Build();
        var eco = sp.GetRequiredService<ILifeOsEcosystemPlatform>();
        Assert.Contains(eco.BillingPlans, p => p.Key == "enterprise");
        Assert.Contains(eco.CertificationLevels, c => c.Key == "certified");
        Assert.Contains(eco.PartnerPrograms, p => p.Key == "startup");
        Assert.Equal(4, eco.EcosystemRoadmap.Count);
        Assert.Contains(eco.EcosystemRoadmap.Last().Deliverables, d => d.Contains("Digital Twin Exchange"));
    }

    [Fact]
    public void Part9DigestCoversChapters126To150()
    {
        using var sp = Build();
        var json = System.Text.Json.JsonSerializer.Serialize(
            sp.GetRequiredService<ILifeOsEcosystemPlatform>().FullPart9Digest());
        Assert.Contains("LifeOS Marketplace", json);
        Assert.Contains("AI Agent Store", json);
        Assert.Contains("Developer Portal", json);
        Assert.Contains("Ecosystem Roadmap", json);
        Assert.Contains("Ambient Artificial Intelligence", json);
    }
}
