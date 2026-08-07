using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Demo;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsDemoRunnerTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void CatalogHasFourSampleScenarios()
    {
        using var sp = Build();
        var demo = sp.GetRequiredService<ILifeOsDemoRunner>();
        Assert.Equal(4, demo.Scenarios.Count);
        Assert.Equal("board-meeting", demo.DefaultScenario.Key);
        var json = System.Text.Json.JsonSerializer.Serialize(demo.CatalogDigest());
        Assert.Contains("Perceive", json, StringComparison.Ordinal);
        Assert.Contains("board-meeting", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunProducesPerceiveDecideActLearnWithSampleContext()
    {
        using var sp = Build();
        var demo = sp.GetRequiredService<ILifeOsDemoRunner>();
        var result = await demo.RunAsync("board-meeting");
        Assert.Equal("board-meeting", result.ScenarioKey);
        Assert.Contains("board meeting", result.Transcript, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, result.HowItWorks.Count);

        var perceive = System.Text.Json.JsonSerializer.Serialize(result.Perceive);
        Assert.Contains("sampleContext", perceive, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Calendar", perceive, StringComparison.Ordinal);

        var decide = System.Text.Json.JsonSerializer.Serialize(result.Decide);
        Assert.Contains("recommendation", decide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ethics", decide, StringComparison.OrdinalIgnoreCase);

        var act = System.Text.Json.JsonSerializer.Serialize(result.Act);
        Assert.Contains("selectedAgents", act, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plan", act, StringComparison.OrdinalIgnoreCase);

        var learn = System.Text.Json.JsonSerializer.Serialize(result.Learn);
        Assert.Contains("memoryLayers", learn, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DemoAppPageIsWired()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/LifeOsDemoApp.razor"));
        Assert.Contains("@page \"/lifeos/demo-app\"", page, StringComparison.Ordinal);
        Assert.Contains("Run full demo", page, StringComparison.Ordinal);
        Assert.Contains("Perceive", page, StringComparison.Ordinal);

        var routes = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("LifeOsDemoApp", routes, StringComparison.Ordinal);
        Assert.Contains("/lifeos/demo/run", routes, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
