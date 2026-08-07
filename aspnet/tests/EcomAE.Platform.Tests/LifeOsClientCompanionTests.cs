using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Clients;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsClientCompanionTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void Test_client_is_seeded_for_immediate_mobile_trial()
    {
        using var sp = Build();
        var dir = sp.GetRequiredService<ILifeOsClientDirectory>();
        Assert.Equal("test-amina", dir.TestClient.ClientId);
        Assert.Equal("Amina", dir.TestClient.DisplayName);
        Assert.Equal("Amina", dir.TestClient.CloneName);
        Assert.True(dir.TestClient.IsTest);
        Assert.Contains(dir.List(), c => c.ClientId == "test-amina");
    }

    [Fact]
    public void Join_creates_client_with_same_name_clone_and_country()
    {
        using var sp = Build();
        var dir = sp.GetRequiredService<ILifeOsClientDirectory>();
        var result = dir.Join(new LifeOsJoinRequest(
            DisplayName: "Omar",
            Email: "omar@example.com",
            Country: "United Arab Emirates",
            CountryCode: "AE",
            City: "Dubai",
            TimeZone: "Asia/Dubai",
            Locale: "en-AE",
            Platform: "unit-test",
            UserAgent: "LifeOsClientCompanionTests",
            Referrer: "/lifeos/join",
            JoinSource: "mobile-web",
            IpCountryHint: "AE",
            UseTestClient: false));
        Assert.True(result.Ok);
        Assert.Equal("Omar", result.Client.DisplayName);
        Assert.Equal("Omar", result.Client.CloneName);
        Assert.Equal("United Arab Emirates", result.Client.Country);
        Assert.Equal("AE", result.Client.CountryCode);
        Assert.Equal("mobile-web", result.Client.JoinSource);
        Assert.False(result.Client.IsTest);
        Assert.StartsWith("/lifeos/mobile?clientId=", result.CompanionUrl);
        Assert.Contains("token=", result.CompanionUrl);
        Assert.StartsWith("/lifeos/results?clientId=", result.ResultsUrl);
        Assert.Equal("/lifeos/manifest.webmanifest", result.ManifestUrl);
    }

    [Fact]
    public void Results_and_control_panel_expose_discussions_and_country()
    {
        using var sp = Build();
        var dir = sp.GetRequiredService<ILifeOsClientDirectory>();
        var join = dir.Join(new LifeOsJoinRequest(
            "Sara", "sara@example.com", "Saudi Arabia", "SA", "Riyadh",
            "Asia/Riyadh", "en-SA", "iPhone", "MobileSafari", "/lifeos", "mobile-web", "SA", false));
        dir.RecordTrack(new LifeOsTrackEvent(join.Client.ClientId, join.Client.JoinToken, "walk", "Walk", 2, "note"));
        dir.Talk(new LifeOsTalkRequest(join.Client.ClientId, join.Client.JoinToken, "Guide me today", "guide"));

        var results = dir.Results(join.Client.ClientId, join.Client.JoinToken, null, null, "all");
        Assert.True(results.Ok);
        Assert.Contains(results.Activities, a => a.Kind == "track");
        Assert.Contains(results.Activities, a => a.Kind is "talk" or "guide");
        Assert.Equal("Saudi Arabia", results.Client.Country);

        var bad = dir.Results(join.Client.ClientId, "wrong-token", null, null, null);
        Assert.False(bad.Ok);

        var cp = System.Text.Json.JsonSerializer.Serialize(dir.ControlPanelDigest());
        Assert.Contains("Sara", cp, StringComparison.Ordinal);
        Assert.Contains("Saudi Arabia", cp, StringComparison.Ordinal);
        Assert.Contains("/lifeos/results", cp, StringComparison.Ordinal);
        Assert.Contains("/cp/lifeos-clients-app", System.Text.Json.JsonSerializer.Serialize(dir.DirectoryDigest()), StringComparison.Ordinal);
    }

    [Fact]
    public void Open_test_client_and_track_talk_guide_loop()
    {
        using var sp = Build();
        var dir = sp.GetRequiredService<ILifeOsClientDirectory>();
        var join = dir.OpenTestClient();
        Assert.True(join.Ok);

        var track = dir.RecordTrack(new LifeOsTrackEvent(
            join.Client.ClientId,
            join.Client.JoinToken,
            "walk",
            "Morning walk",
            1.2,
            "scaffold"));
        Assert.True(track.Ok);
        Assert.Contains("Amina", track.CloneAdvice, StringComparison.Ordinal);
        Assert.True(track.Session.TrackEventCount >= 1);

        var talk = dir.Talk(new LifeOsTalkRequest(
            join.Client.ClientId,
            join.Client.JoinToken,
            "Amina, walk me through today.",
            "guide"));
        Assert.True(talk.Ok);
        Assert.Equal("Amina", talk.HumanName);
        Assert.Equal("Amina", talk.CloneName);
        Assert.Contains("Amina", talk.Reply, StringComparison.Ordinal);
        Assert.NotEmpty(talk.SuggestedActions);

        var session = dir.CompanionSession(join.Client.ClientId, join.Client.JoinToken);
        Assert.NotEmpty(session.GuideBeats);
        Assert.Contains(session.GuideBeats, b => b.ToString()!.Contains("morning", StringComparison.OrdinalIgnoreCase)
            || System.Text.Json.JsonSerializer.Serialize(b).Contains("morning", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Directory_and_companion_digests_expose_pwa_paths()
    {
        using var sp = Build();
        var dir = sp.GetRequiredService<ILifeOsClientDirectory>();
        var directoryJson = System.Text.Json.JsonSerializer.Serialize(dir.DirectoryDigest());
        var companionJson = System.Text.Json.JsonSerializer.Serialize(dir.CompanionDigest());
        Assert.Contains("/lifeos/join", directoryJson, StringComparison.Ordinal);
        Assert.Contains("/lifeos/mobile", directoryJson, StringComparison.Ordinal);
        Assert.Contains("/lifeos/results", directoryJson, StringComparison.Ordinal);
        Assert.Contains("manifest.webmanifest", directoryJson, StringComparison.Ordinal);
        Assert.Contains("test-amina", directoryJson, StringComparison.Ordinal);
        Assert.Contains("/lifeos/sw.js", companionJson, StringComparison.Ordinal);
        Assert.Contains("track", companionJson, StringComparison.Ordinal);
        Assert.Contains("talk", companionJson, StringComparison.Ordinal);
        Assert.Contains("listen", companionJson, StringComparison.Ordinal);
        Assert.Contains("guide", companionJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Pwa_assets_exist_under_wwwroot_lifeos()
    {
        var root = FindLifeOsWwwRoot();
        Assert.True(root is not null, "wwwroot/lifeos not found from test host");
        Assert.True(File.Exists(Path.Combine(root!, "manifest.webmanifest")), root);
        Assert.True(File.Exists(Path.Combine(root!, "sw.js")), root);
        Assert.True(File.Exists(Path.Combine(root!, "icons", "lifeos-pwa-192.svg")), root);
        Assert.True(File.Exists(Path.Combine(root!, "icons", "lifeos-pwa-512.svg")), root);
        var manifest = File.ReadAllText(Path.Combine(root!, "manifest.webmanifest"));
        Assert.Contains("/lifeos/mobile", manifest, StringComparison.Ordinal);
        Assert.Contains("standalone", manifest, StringComparison.Ordinal);
    }

    private static string? FindLifeOsWwwRoot()
    {
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, "src", "EcomAE.Platform", "wwwroot", "lifeos");
            if (File.Exists(Path.Combine(candidate, "manifest.webmanifest")))
            {
                return candidate;
            }

            candidate = Path.Combine(probe.FullName, "aspnet", "src", "EcomAE.Platform", "wwwroot", "lifeos");
            if (File.Exists(Path.Combine(candidate, "manifest.webmanifest")))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        return null;
    }
}
