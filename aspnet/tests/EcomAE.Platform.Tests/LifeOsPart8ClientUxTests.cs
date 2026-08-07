using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Part8;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPart8ClientUxTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void ExperiencePrinciplesAndClientEcosystemArePopulated()
    {
        using var sp = Build();
        var ux = sp.GetRequiredService<ILifeOsClientExperience>();
        Assert.Equal(8, ux.ExperiencePrinciples.Count);
        Assert.True(ux.ClientPlatforms.Count >= 20);
        Assert.Contains(ux.ClientPlatforms, p => p.Family == "Web" && p.Status.Contains("live"));
        Assert.Contains(ux.ClientPlatforms, p => p.Family == "Vehicle");
    }

    [Fact]
    public void LifeDesignSystemAndIntentNavigationExist()
    {
        using var sp = Build();
        var ux = sp.GetRequiredService<ILifeOsClientExperience>();
        Assert.Contains(ux.DesignComponents, c => c.Key == "voice-ui");
        Assert.Contains(ux.DesignComponents, c => c.Key == "ai-chat");
        Assert.Contains(ux.NavigationMethods, n => n.Key == "voice");
        Assert.Contains(ux.DesignPrinciples, p => p.Contains("AI-First"));
    }

    [Fact]
    public void WorkspaceSearchAndDashboardsCoverSemanticDomains()
    {
        using var sp = Build();
        var ux = sp.GetRequiredService<ILifeOsClientExperience>();
        Assert.Contains(ux.AiWorkspaceModules, m => m.Key == "memory");
        Assert.Contains(ux.SearchDomains, d => d.Key == "conversations");
        Assert.Equal(3, ux.DashboardKinds.Count);
        Assert.Contains(ux.DashboardKinds, d => d.Key == "executive");
    }

    [Fact]
    public void ModalityClientsIncludeMobileDesktopGlassesVehicle()
    {
        using var sp = Build();
        var json = System.Text.Json.JsonSerializer.Serialize(
            sp.GetRequiredService<ILifeOsClientExperience>().ModalityClientsDigest());
        Assert.Contains("Offline AI", json);
        Assert.Contains("AI Sidebar", json);
        Assert.Contains("Instruction Overlay", json);
        Assert.Contains("No distracting visual", json);
    }

    [Fact]
    public void ContinuityAccessibilityOfflineAndFocusAreDefined()
    {
        using var sp = Build();
        var ux = sp.GetRequiredService<ILifeOsClientExperience>();
        Assert.Contains(ux.ContinuityFlow, s => s == "Desktop");
        Assert.Contains(ux.AccessibilitySupports, a => a.Key == "screen-reader");
        Assert.Contains(ux.OfflineCapabilities, o => o.Key == "wake");
        Assert.Contains(ux.FocusModes, f => f.Key == "driving");
        Assert.Contains(ux.DigitalTwinCapabilities, c => c.Contains("Simulate plans"));
    }

    [Fact]
    public void Part8DigestCoversChapters102To125()
    {
        using var sp = Build();
        var json = System.Text.Json.JsonSerializer.Serialize(
            sp.GetRequiredService<ILifeOsClientExperience>().FullPart8Digest());
        Assert.Contains("Life Design System", json);
        Assert.Contains("Cross-Device Continuity", json);
        Assert.Contains("Digital Twin", json);
        Assert.Contains("User Experience Metrics", json);
        Assert.Contains("native", json, StringComparison.OrdinalIgnoreCase);
    }
}
