using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Part10;
using EcomAE.Platform.LifeOs.Spec;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPart10ExecutionTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void Part10HasEighteenChaptersAndScaffoldStatus()
    {
        using var sp = Build();
        var part = sp.GetRequiredService<ILifeOsMasterSpec>().Parts.Single(p => p.Number == 10);
        Assert.Equal("scaffold", part.Status);
        Assert.Equal(18, part.Chapters.Count);
        Assert.Contains(part.Chapters, c => c == "Mission");
        Assert.Contains(part.Chapters, c => c == "Closing Statement");
        Assert.Contains(part.Deliverables, d => d.Contains("/lifeos/roadmap", StringComparison.Ordinal));
    }

    [Fact]
    public void ExecutionStrategyDigestCoversMissionPhasesAndBlueprint()
    {
        using var sp = Build();
        var exec = sp.GetRequiredService<ILifeOsExecutionStrategy>();
        Assert.Contains("AI Operating System", exec.MissionStatement, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(6, exec.DevelopmentPhases.Count);
        Assert.Equal(20, exec.ProductPortfolio.Count);
        Assert.Contains(exec.Vision2035Goals, g => g.Contains("billion", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(8, exec.PlatformBlueprintLayers.Count);
        Assert.Contains("Human-first AI", exec.GuidingPrinciples);
        Assert.Contains("LifeOS", exec.CinematicVideoPrompt, StringComparison.Ordinal);

        var json = System.Text.Json.JsonSerializer.Serialize(exec.FullPart10Digest());
        Assert.Contains("151", json);
        Assert.Contains("Vision2035", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cinematicVideoPrompt", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RoadmapAppIsWired()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Pages/LifeOsRoadmapApp.razor"));
        Assert.Contains("@page \"/lifeos/roadmap-app\"", text, StringComparison.Ordinal);
        Assert.Contains("Chapters 151–168", text, StringComparison.Ordinal);
        Assert.Contains("Development phases", text, StringComparison.Ordinal);
        Assert.Contains("Cinematic product film prompt", text, StringComparison.Ordinal);

        var routes = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("LifeOsRoadmapApp", routes, StringComparison.Ordinal);
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
