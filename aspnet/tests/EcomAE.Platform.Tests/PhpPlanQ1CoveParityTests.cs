using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1CoveParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Cove");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public void PlanQ1Cove_MatchPhpGolden()
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
            if (!Same(Json(actual.Extra), expected))
            {
                failures.Add(name + " extraExp=" + Truncate(expected.GetRawText()) + " extraGot=" + Truncate(Json(actual.Extra)));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Cove.SocialPublishPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Cove.SocialPublishPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Cove.Reset();
        Assert.Contains("epc_social_publish.php", PhpPlanQ1Cove.SocialPublishPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Cove.SocialPublishPath, StringComparison.Ordinal);
        Assert.Equal("v21.0", PhpPlanQ1Cove.EpcSocialGraphVersion());
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Cove.Reset();
        PhpPlanQ1Cove.UnixNow = () => 1_700_000_000;
        PhpPlanQ1Cove.Sleep = _ => { };
        PhpPlanQ1Cove.Decrypt = (enc, _) => enc;
        PhpPlanQ1Cove.Curl = Route;
        return name switch
        {
            "pure" => Pure(),
            "accounts" => Accounts(),
            "publish" => Publish(),
            "now" => Now(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static PhpPlanQ1Cove.CurlHit Route(PhpPlanQ1Cove.CurlReq req)
    {
        var url = req.Url;
        var method = req.Method;
        if (url.Contains("graph.facebook.com", StringComparison.Ordinal) && method == "GET" && url.Contains("/me?", StringComparison.Ordinal))
        {
            return Ok("""{"id":"me1","name":"Me Page"}""");
        }

        if (url.Contains("graph.facebook.com", StringComparison.Ordinal) && method == "GET" && url.Contains("fields=id,name", StringComparison.Ordinal))
        {
            return Ok("""{"id":"pg1","name":"Shop Page"}""");
        }

        if (url.Contains("graph.facebook.com", StringComparison.Ordinal) && method == "GET" && url.Contains("fields=id,username", StringComparison.Ordinal))
        {
            return Ok("""{"id":"ig1","username":"parts.ae"}""");
        }

        if (url.Contains("graph.facebook.com", StringComparison.Ordinal) && method == "GET" && url.Contains("fields=status_code", StringComparison.Ordinal))
        {
            return Ok("""{"status_code":"FINISHED"}""");
        }

        if (url.Contains("/photos", StringComparison.Ordinal))
        {
            return Ok("""{"id":"fb-photo-1"}""");
        }

        if (url.Contains("/videos", StringComparison.Ordinal))
        {
            return Ok("""{"id":"fb-vid-1"}""");
        }

        if (url.Contains("/feed", StringComparison.Ordinal))
        {
            return Ok("""{"id":"fb-feed-1"}""");
        }

        if (url.Contains("/media_publish", StringComparison.Ordinal))
        {
            return Ok("""{"id":"ig-media-1"}""");
        }

        if (url.Contains("/media", StringComparison.Ordinal))
        {
            return Ok("""{"id":"ig-box-1"}""");
        }

        if (url.Contains("creator_info/query", StringComparison.Ordinal))
        {
            return Ok("""{"data":{"creator_username":"ttuser","privacy_level_options":["SELF_ONLY","PUBLIC_TO_EVERYONE"]}}""");
        }

        if (url.Contains("video/init", StringComparison.Ordinal))
        {
            return Ok("""{"data":{"publish_id":"tt-pub-1"}}""");
        }

        if (url.Contains("status/fetch", StringComparison.Ordinal))
        {
            return Ok("""{"data":{"status":"PUBLISH_COMPLETE"}}""");
        }

        if (url.Contains("fail.example", StringComparison.Ordinal))
        {
            return new PhpPlanQ1Cove.CurlHit(400, """{"error":{"message":"bad token"}}""", 0, "");
        }

        if (url.Contains("errno.example", StringComparison.Ordinal))
        {
            return new PhpPlanQ1Cove.CurlHit(0, "", 7, "Failed to connect");
        }

        return new PhpPlanQ1Cove.CurlHit(404, """{"message":"no route"}""", 0, "");
    }

    private static PhpPlanQ1Cove.CurlHit Ok(string raw) => new(200, raw, 0, "");

    private static Rendered Pure()
    {
        var def = PhpPlanQ1Cove.EpcSocialGraphVersion();
        PhpPlanQ1Cove.GraphVersionConfig = " v22.0-beta! ";
        var custom = PhpPlanQ1Cove.EpcSocialGraphVersion();
        PhpPlanQ1Cove.GraphVersionConfig = "!!!";
        var fallback = PhpPlanQ1Cove.EpcSocialGraphVersion();
        var caps = new object[]
        {
            PhpPlanQ1Cove.EpcSocialComposeCaption("Hello", ""),
            PhpPlanQ1Cove.EpcSocialComposeCaption("", "#parts"),
            PhpPlanQ1Cove.EpcSocialComposeCaption("Hello", "#parts"),
            PhpPlanQ1Cove.EpcSocialComposeCaption("  Hi  ", "  #x  ")
        };
        var urls = new object[]
        {
            PhpPlanQ1Cove.EpcSocialIsVideoUrl("https://cdn.example/a.MP4?x=1"),
            PhpPlanQ1Cove.EpcSocialIsVideoUrl("https://cdn.example/a.jpg"),
            PhpPlanQ1Cove.EpcSocialIsImageUrl("https://cdn.example/a.PNG"),
            PhpPlanQ1Cove.EpcSocialIsImageUrl("https://cdn.example/a.mp4"),
            PhpPlanQ1Cove.EpcSocialIsVideoUrl("not a url")
        };
        var ok = PhpPlanQ1Cove.EpcSocialHttpJson("GET", "https://graph.facebook.com/v21.0/me?fields=id,name&access_token=t");
        var bad = PhpPlanQ1Cove.EpcSocialHttpJson("GET", "https://fail.example/x");
        var net = PhpPlanQ1Cove.EpcSocialHttpJson("GET", "https://errno.example/x");
        var form = PhpPlanQ1Cove.EpcSocialHttpJson("POST", "https://graph.facebook.com/v21.0/pg/feed", [], new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["message"] = "Hi & you",
            ["access_token"] = "t"
        }, 45, new Dictionary<string, object?>(StringComparer.Ordinal) { ["form"] = true });
        var emptyArr = PhpPlanQ1Cove.EpcSocialHttpJson("POST", "https://open.tiktokapis.com/v2/post/publish/creator_info/query/", ["Authorization: Bearer t"], Array.Empty<object>());
        var last = PhpPlanQ1Cove.LastCurl;
        return new Rendered(new object?[]
        {
            def, custom, fallback, caps, urls, ok, bad, net, form, emptyArr,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["method"] = last?.Method,
                ["url"] = last?.Url,
                ["body"] = last?.Body
            }
        });
    }

    private static Rendered Accounts()
    {
        var db = new PhpPlanQ1Cove.CoveStore();
        PhpPlanQ1Cove.UseStore(db);
        var miss = PhpPlanQ1Cove.EpcSocialAccountCredentials(db, "alpha", "facebook");
        var metaMiss = PhpPlanQ1Cove.EpcSocialAccountPublicMeta(db, "alpha", "facebook");
        Seed(db, "alpha", "facebook", "page", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "tok",
            ["page_id"] = "pg1",
            ["privacy_level"] = "SELF_ONLY"
        });
        Seed(db, "alpha", "instagram", "ig", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "igtok",
            ["page_id"] = "ig1",
            ["ig_user_id"] = "ig1",
            ["privacy_level"] = "SELF_ONLY"
        });
        Seed(db, "alpha", "tiktok", "tt", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "tttok",
            ["privacy_level"] = "SELF_ONLY"
        });
        Seed(db, "alpha", "linkedin", "li", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "li",
            ["privacy_level"] = "SELF_ONLY"
        });
        Seed(db, "beta", "facebook", "", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "other",
            ["page_id"] = "pgX",
            ["privacy_level"] = "SELF_ONLY"
        });
        var cred = (Dictionary<string, object?>)PhpPlanQ1Cove.EpcSocialAccountCredentials(db, "alpha", "Facebook!");
        cred.Remove("access_token");
        cred.Remove("api_key");
        cred.Remove("api_secret");
        var meta = PhpPlanQ1Cove.EpcSocialAccountPublicMeta(db, "alpha", "facebook");
        var other = PhpPlanQ1Cove.EpcSocialAccountPublicMeta(db, "beta", "facebook");
        var fb = PhpPlanQ1Cove.EpcSocialTestAccountLive(db, "alpha", "facebook");
        var ig = PhpPlanQ1Cove.EpcSocialTestAccountLive(db, "alpha", "instagram");
        var tt = PhpPlanQ1Cove.EpcSocialTestAccountLive(db, "alpha", "tiktok");
        var li = PhpPlanQ1Cove.EpcSocialTestAccountLive(db, "alpha", "linkedin");
        var unk = PhpPlanQ1Cove.EpcSocialTestAccountLive(db, "alpha", "nope");
        var none = PhpPlanQ1Cove.EpcSocialTestAccountLive(db, "gamma", "facebook");
        var noTok = PhpPlanQ1Cove.EpcSocialMetaTestFacebook(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "",
            ["page_id"] = "x"
        });
        return new Rendered(new object?[]
        {
            miss, metaMiss, cred, meta, other["has_token"],
            fb, ig, tt, li, unk, none, noTok,
            Snap(db, "alpha", "facebook"),
            Snap(db, "alpha", "linkedin")
        });
    }

    private static Rendered Publish()
    {
        var db = new PhpPlanQ1Cove.CoveStore();
        PhpPlanQ1Cove.UseStore(db);
        Seed(db, "alpha", "facebook", "", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "tok",
            ["page_id"] = "pg1",
            ["privacy_level"] = "SELF_ONLY"
        });
        Seed(db, "alpha", "instagram", "", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "igtok",
            ["ig_user_id"] = "ig1",
            ["privacy_level"] = "SELF_ONLY"
        });
        Seed(db, "alpha", "tiktok", "", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "tttok",
            ["privacy_level"] = "SELF_ONLY"
        });
        Draft(db, "alpha", "facebook", "F1", "Hi", "#ae", "https://cdn.example/a.jpg");
        Draft(db, "alpha", "facebook", "F2", "Vid", "", "https://cdn.example/a.mp4");
        Draft(db, "alpha", "facebook", "F3", "Text only", "", "");
        Draft(db, "alpha", "instagram", "I1", "IG", "", "https://cdn.example/a.jpg");
        Draft(db, "alpha", "tiktok", "T1", "TT", "", "https://cdn.example/a.mp4");
        Draft(db, "alpha", "linkedin", "L1", "LI", "", "");
        var photo = PhpPlanQ1Cove.EpcSocialPublishDraft(db, "alpha", 1);
        var video = PhpPlanQ1Cove.EpcSocialPublishDraft(db, "alpha", 2);
        var feed = PhpPlanQ1Cove.EpcSocialPublishDraft(db, "alpha", 3);
        var ig = PhpPlanQ1Cove.EpcSocialPublishDraft(db, "alpha", 4);
        var tt = PhpPlanQ1Cove.EpcSocialPublishDraft(db, "alpha", 5);
        var li = PhpPlanQ1Cove.EpcSocialPublishDraft(db, "alpha", 6);
        var miss = PhpPlanQ1Cove.EpcSocialPublishDraft(db, "alpha", 99);
        var cross = PhpPlanQ1Cove.EpcSocialPublishDraft(db, "beta", 1);
        var needPage = PhpPlanQ1Cove.EpcSocialMetaPublishFacebook(db, "beta", "x", "");
        var needIg = PhpPlanQ1Cove.EpcSocialMetaPublishInstagram(db, "alpha", "x", "");
        var needTt = PhpPlanQ1Cove.EpcSocialTiktokPublishVideo(db, "alpha", "x", "https://cdn.example/a.jpg");
        var rows = db.Drafts.OrderBy(d => d.Id).Select(d => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = d.Title,
            ["status"] = d.Status,
            ["external_post_id"] = d.ExternalPostId,
            ["last_error"] = d.LastError
        }).ToList();
        return new Rendered(new object?[] { photo, video, feed, ig, tt, li, miss, cross, needPage, needIg, needTt, rows });
    }

    private static Rendered Now()
    {
        var db = new PhpPlanQ1Cove.CoveStore();
        PhpPlanQ1Cove.UseStore(db);
        Seed(db, "alpha", "facebook", "", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["access_token"] = "tok",
            ["page_id"] = "pg1",
            ["privacy_level"] = "SELF_ONLY"
        });
        var bad = PhpPlanQ1Cove.EpcSocialPublishNow(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["platform"] = "",
            ["caption"] = "x"
        });
        var empty = PhpPlanQ1Cove.EpcSocialPublishNow(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["platform"] = "facebook"
        });
        var ok = PhpPlanQ1Cove.EpcSocialPublishNow(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["platform"] = "facebook",
            ["title"] = "",
            ["caption"] = "Now post",
            ["media_url"] = "https://cdn.example/a.jpg"
        });
        var rows = db.Drafts.OrderBy(d => d.Id).Select(d => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = d.Title,
            ["status"] = d.Status,
            ["caption"] = d.Caption
        }).ToList();
        return new Rendered(new object?[] { bad, empty, ok, rows });
    }

    private static void Seed(PhpPlanQ1Cove.CoveStore db, string site, string platform, string username, Dictionary<string, object?> cred)
    {
        db.Accounts.Add(new PhpPlanQ1Cove.AccountRow
        {
            SiteKey = site,
            Platform = platform,
            Username = username,
            EncryptedCredentials = JsonSerializer.Serialize(cred, JsonOpts)
        });
    }

    private static void Draft(PhpPlanQ1Cove.CoveStore db, string site, string platform, string title, string caption, string hashtags, string media)
    {
        db.Drafts.Add(new PhpPlanQ1Cove.DraftRow
        {
            Id = db.NextDraftId++,
            SiteKey = site,
            Platform = platform,
            Title = title,
            Caption = caption,
            Hashtags = hashtags,
            MediaUrl = media
        });
    }

    private static Dictionary<string, object?> Snap(PhpPlanQ1Cove.CoveStore db, string site, string platform)
    {
        var row = db.Accounts.First(a => a.SiteKey == site && a.Platform == platform);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = row.Status,
            ["last_test_ok"] = row.LastTestOk
        };
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool Same(string actual, JsonElement expected)
    {
        try
        {
            using var left = JsonDocument.Parse(actual);
            return JsonEquivalent(left.RootElement, expected);
        }
        catch (JsonException)
        {
            return actual == (expected.ValueKind == JsonValueKind.String ? expected.GetString() : expected.GetRawText());
        }
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                if (left.EnumerateObject().Count() != right.EnumerateObject().Count())
                {
                    return false;
                }

                foreach (var prop in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(prop.Name, out var other) || !JsonEquivalent(prop.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                return a.Count == b.Count && a.Zip(b, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble();
            default:
                return true;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 1400 ? value : value[..1400] + "…";
}
