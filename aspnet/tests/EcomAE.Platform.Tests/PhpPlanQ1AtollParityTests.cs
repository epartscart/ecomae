using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1AtollParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Atoll");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Atoll_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = results[i].GetProperty("result");
            var actual = Render(name);
            if (!JsonEquivalent(JsonDocument.Parse(Json(actual)).RootElement, expected))
            {
                failures.Add(name + " exp=" + expected.GetRawText()[..Math.Min(400, expected.GetRawText().Length)] + " got=" + Json(actual)[..Math.Min(400, Json(actual).Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("cp/content/control/portal/epc_social_media_hub_panel.php", PhpPlanQ1Atoll.SocialMediaHubPanelPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Atoll.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Atoll.SocialMediaHubPanelPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Atoll.EpcSocialMediaExploreHintHtml(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Atoll.Reset();
        BindDefaults();
        return name switch
        {
            "names" => Names(),
            "gates" => Gates(),
            "hub" => Hub(),
            "tabs" => Tabs(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static void BindDefaults()
    {
        PhpPlanQ1Atoll.PackPosts = plat => plat == "linkedin"
            ? [new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "A" }]
            : [];
        PhpPlanQ1Atoll.PackPostsForBrand = (plat, _) => plat switch
        {
            "linkedin" => [new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "O'Reilly post", ["caption"] = "Stock {brand}" }],
            "tiktok" => [new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "Reel 1", ["caption"] = "Fast clip" }],
            _ => []
        };
        PhpPlanQ1Atoll.BrandContext = key => key == "beta"
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand_name"] = "Beta Demo", ["industry"] = "electronics", ["market"] = "PK", ["country"] = "PK", ["handle"] = "@beta"
            }
            : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand_name"] = "Acme Parts", ["industry"] = "auto_parts", ["market"] = "O'Reilly UAE", ["country"] = "AE", ["handle"] = "@acme"
            };
        PhpPlanQ1Atoll.AccountPublicMeta = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["page_id"] = "p1", ["ig_user_id"] = "ig1", ["open_id"] = "", ["privacy_level"] = "SELF_ONLY", ["has_token"] = true
        };
    }

    private static object Names()
        => new object[]
        {
            PhpPlanQ1Atoll.EpcSocialMediaExploreHintHtml(),
            PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderVideoCard(new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "", ["title"] = "Nope" })),
            PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderVideoCard(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "O'Reilly reel",
                ["url"] = "https://cdn.test/a.mp4?q=1&x=2",
                ["blurb"] = "Hook & cut"
            }, true)),
            PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderVideoCard(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Guide",
                ["url"] = "https://cdn.test/g.mp4"
            }))
        };

    private static object Gates()
    {
        var superNo = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialMediaRenderHub(new Dictionary<string, object?>(StringComparer.Ordinal) { ["is_super"] = 1 }));
        var tenantNo = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialMediaRenderHub());
        PhpPlanQ1Atoll.IsAdmin = () => true;
        var noDb = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialMediaRenderHub(new Dictionary<string, object?>(StringComparer.Ordinal) { ["is_super"] = 1 }));
        var zeroSuper = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialMediaRenderHub(new Dictionary<string, object?>(StringComparer.Ordinal) { ["is_super"] = "0" }));
        return new object[] { superNo, tenantNo, noDb, zeroSuper };
    }

    private static object Hub()
    {
        PhpPlanQ1Atoll.IsAdmin = () => true;
        PhpPlanQ1Atoll.SocialPdo = () => new object();
        PhpPlanQ1Atoll.ResolveSiteKey = () => "acme_parts";
        PhpPlanQ1Atoll.Query["tab"] = "Pack!";
        var acme = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialMediaRenderHub(new Dictionary<string, object?>(StringComparer.Ordinal) { ["is_super"] = 1 }));
        PhpPlanQ1Atoll.ResolveSiteKey = () => "beta";
        PhpPlanQ1Atoll.Query.Clear();
        PhpPlanQ1Atoll.Query["tab"] = "ai";
        var beta = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialMediaRenderHub(new Dictionary<string, object?>(StringComparer.Ordinal) { ["is_super"] = 1 }));
        PhpPlanQ1Atoll.ResolveSiteKey = () => "acme_parts";
        PhpPlanQ1Atoll.Query.Clear();
        PhpPlanQ1Atoll.Query["sub"] = "guide";
        var embed = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialMediaRenderHub(new Dictionary<string, object?>(StringComparer.Ordinal) { ["embed_tenant_hub"] = 1 }));
        PhpPlanQ1Atoll.Query.Clear();
        PhpPlanQ1Atoll.RequestMethod = "POST";
        PhpPlanQ1Atoll.Post["epc_social_action"] = "save_account";
        PhpPlanQ1Atoll.Post["platform"] = "instagram";
        PhpPlanQ1Atoll.VerifyCsrf = () => false;
        var csrf = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialMediaRenderHub(new Dictionary<string, object?>(StringComparer.Ordinal) { ["is_super"] = 1 }));
        PhpPlanQ1Atoll.VerifyCsrf = () => true;
        var okPost = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialMediaRenderHub(new Dictionary<string, object?>(StringComparer.Ordinal) { ["is_super"] = 1 }));
        return new object[] { acme, beta, embed, csrf, okPost };
    }

    private static object Tabs()
    {
        var brand = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["brand_name"] = "Acme Parts", ["industry"] = "auto_parts", ["country"] = "AE", ["handle"] = "@acme", ["market"] = "UAE"
        };
        var pack = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderPackTab(brand));
        var tt = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderTiktokTab(brand));
        var ig = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderInstagramTab(brand));
        var ai = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderAiTab(brand,
            [new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Reel", ["platforms"] = "IG/TT", ["tip"] = "Keep it under 15s" }],
            ["Same-day delivery"], "tok-1"));
        var emptyDrafts = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderDraftsTab(brand, [], "tok-1"));
        var drafts = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderDraftsTab(brand,
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 9, ["title"] = "O'Reilly drop", ["platform"] = "instagram", ["status"] = "draft",
                ["updated_at"] = 1700000000, ["caption"] = "Buy now",
                ["media_url"] = "https://cdn.test/very-long-media-name-that-should-clip-after-sixty-chars.mp4"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 8, ["title"] = "Done", ["platform"] = "facebook", ["status"] = "published",
                ["updated_at"] = 1700000000, ["caption"] = "Live", ["external_post_id"] = "fb-1"
            }
        ], "tok-1"));
        var guide = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderGuideTab(brand, "/cp/control/portal/epc_integrations_hub", "/cp/control/portal/epc_social_media_hub?tab=guide"));
        PhpPlanQ1Atoll.SocialPdo = () => new object();
        var accounts = PhpPlanQ1Atoll.Capture(() => PhpPlanQ1Atoll.EpcSocialRenderAccountsTab(brand, "acme_parts",
            new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
            {
                ["instagram"] = new(StringComparer.Ordinal)
                {
                    ["status"] = "verified", ["account_label"] = "O'Reilly IG", ["username"] = "@acme", ["last_test_at"] = 1700000000
                }
            }, "/cp/control/portal/epc_integrations_hub", "tok-1"));
        return new object[] { pack, tt, ig, ai, emptyDrafts, drafts, guide, accounts };
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => left.EnumerateObject().All(p => right.TryGetProperty(p.Name, out var o) && JsonEquivalent(p.Value, o))
                && left.EnumerateObject().Count() == right.EnumerateObject().Count(),
            JsonValueKind.Array => left.EnumerateArray().ToList().Zip(right.EnumerateArray().ToList(), JsonEquivalent).All(x => x)
                && left.GetArrayLength() == right.GetArrayLength(),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => true
        };
    }
}
