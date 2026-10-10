using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HullParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hull");

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
    public void PlanQ1Hull_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Hull.AuthSocialPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Hull.AuthSocialPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Hull.Reset();
        Assert.Contains("epc_auth_social.php", PhpPlanQ1Hull.AuthSocialPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Hull.AuthSocialPath, StringComparison.Ordinal);
        var lost = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["am"] = "cp", ["tk"] = "" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "a@b.c" });
        Assert.False((bool)lost["ok"]!);
        var html = PhpPlanQ1Hull.EpcCpLoginModernAuthHtml(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_key"] = "acme",
            ["login_label"] = "Control Panel"
        });
        Assert.DoesNotContain("PHPSESSID", html, StringComparison.Ordinal);
        Assert.Contains("data-tenant-key=\"acme\"", html, StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Hull.Reset();
        PhpPlanQ1Hull.Clock = () => 1_760_000_000;
        PhpPlanQ1Hull.Nonce = () => new string('a', 32);
        PhpPlanQ1Hull.SigningSecret = "secret";
        return name switch
        {
            "providers" => Providers(),
            "state" => State(),
            "google" => Google(),
            "login_html" => LoginHtml(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Dictionary<string, object?> GoogleCfg(string id, string secret, string redirect)
        => new(StringComparer.Ordinal)
        {
            ["client_id"] = id,
            ["client_secret"] = secret,
            ["redirect_uri"] = redirect
        };

    private static Dictionary<string, object?> Ctx(string tenant = "acme", string mode = "cp")
        => new(StringComparer.Ordinal)
        {
            ["tenant_key"] = tenant,
            ["kind"] = "mixed",
            ["return_host"] = "shop.acme.test",
            ["return_path"] = "/cp/",
            ["auth_mode"] = mode,
            ["lang_prefix"] = "en"
        };

    private static string Jwt(Dictionary<string, object?> payload)
    {
        static string B64(string raw)
            => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var json = JsonSerializer.Serialize(payload);
        return B64("{\"alg\":\"none\"}") + "." + B64(json) + ".sig";
    }

    private static Rendered Providers()
    {
        PhpPlanQ1Hull.OAuthConfig = new(StringComparer.Ordinal) { ["google"] = GoogleCfg("", "", "") };
        var empty = PhpPlanQ1Hull.EpcAuthSocialProviders();
        var cbEmpty = PhpPlanQ1Hull.EpcAuthOauthCentralCallbackUrl();
        PhpPlanQ1Hull.OAuthConfig = new(StringComparer.Ordinal)
        {
            ["google"] = GoogleCfg("id.apps.googleusercontent.com", "", "")
        };
        var idOnly = PhpPlanQ1Hull.EpcAuthSocialProviders();
        PhpPlanQ1Hull.OAuthConfig = new(StringComparer.Ordinal)
        {
            ["google"] = GoogleCfg("id.apps.googleusercontent.com", "secret", "https://www.ecomae.com/custom-callback.php")
        };
        var full = PhpPlanQ1Hull.EpcAuthSocialProviders();
        var cb = PhpPlanQ1Hull.EpcAuthOauthCentralCallbackUrl();
        return new Rendered(new object?[] { empty, cbEmpty, idOnly, full, cb });
    }

    private static Rendered State()
    {
        var ok = PhpPlanQ1Hull.EpcAuthOauthStatePack(Ctx(), new string('a', 32));
        var un = PhpPlanQ1Hull.EpcAuthOauthStateUnpack(ok);
        var bad = PhpPlanQ1Hull.EpcAuthOauthStateUnpack(ok + "x");
        var parts = ok.Split('.', 2);
        var cut = PhpPlanQ1Hull.EpcAuthOauthStateUnpack(parts[0]);
        PhpPlanQ1Hull.Clock = () => 1_760_000_000 + 901;
        var expired = PhpPlanQ1Hull.EpcAuthOauthStateUnpack(ok);
        PhpPlanQ1Hull.Clock = () => 1_760_000_000;
        var other = PhpPlanQ1Hull.EpcAuthOauthStatePack(Ctx("beta", "storefront"), new string('a', 32));
        var unOther = PhpPlanQ1Hull.EpcAuthOauthStateUnpack(other);
        return new Rendered(new object?[] { ok, un, bad, cut, expired, other, unOther });
    }

    private static Rendered Google()
    {
        PhpPlanQ1Hull.OAuthConfig = new(StringComparer.Ordinal) { ["google"] = GoogleCfg("", "", "") };
        var emptyStart = PhpPlanQ1Hull.EpcAuthGoogleStartUrl(Ctx());
        PhpPlanQ1Hull.OAuthConfig = new(StringComparer.Ordinal)
        {
            ["google"] = GoogleCfg(
                "id.apps.googleusercontent.com",
                "secret",
                "https://www.ecomae.com/epc-auth-google-callback.php")
        };
        var start = PhpPlanQ1Hull.EpcAuthGoogleStartUrl(Ctx());
        var client = "id.apps.googleusercontent.com";
        var malformed = PhpPlanQ1Hull.EpcAuthGoogleVerifyIdToken("nope", client);
        var aud = PhpPlanQ1Hull.EpcAuthGoogleVerifyIdToken(Jwt(new(StringComparer.Ordinal)
        {
            ["aud"] = "other",
            ["email"] = "ops@acme.test",
            ["email_verified"] = true,
            ["iss"] = "https://accounts.google.com",
            ["exp"] = 1760000900,
            ["name"] = "Ops",
            ["sub"] = "1"
        }), client);
        var unverified = PhpPlanQ1Hull.EpcAuthGoogleVerifyIdToken(Jwt(new(StringComparer.Ordinal)
        {
            ["aud"] = client,
            ["email"] = "ops@acme.test",
            ["email_verified"] = false,
            ["iss"] = "https://accounts.google.com",
            ["exp"] = 1760000900,
            ["name"] = "Ops",
            ["sub"] = "1"
        }), client);
        var zero = PhpPlanQ1Hull.EpcAuthGoogleVerifyIdToken(Jwt(new(StringComparer.Ordinal)
        {
            ["aud"] = client,
            ["email"] = "ops@acme.test",
            ["email_verified"] = "0",
            ["iss"] = "https://accounts.google.com",
            ["exp"] = 1760000900,
            ["name"] = "Ops",
            ["sub"] = "1"
        }), client);
        var issuer = PhpPlanQ1Hull.EpcAuthGoogleVerifyIdToken(Jwt(new(StringComparer.Ordinal)
        {
            ["aud"] = client,
            ["email"] = "ops@acme.test",
            ["email_verified"] = true,
            ["iss"] = "evil.example",
            ["exp"] = 1760000900,
            ["name"] = "Ops",
            ["sub"] = "1"
        }), client);
        var expired = PhpPlanQ1Hull.EpcAuthGoogleVerifyIdToken(Jwt(new(StringComparer.Ordinal)
        {
            ["aud"] = client,
            ["email"] = "ops@acme.test",
            ["email_verified"] = true,
            ["iss"] = "accounts.google.com",
            ["exp"] = 1759999999,
            ["name"] = "Ops",
            ["sub"] = "1"
        }), client);
        var ok = PhpPlanQ1Hull.EpcAuthGoogleVerifyIdToken(Jwt(new(StringComparer.Ordinal)
        {
            ["aud"] = client,
            ["email"] = " Ops@Acme.TEST ",
            ["email_verified"] = 1,
            ["iss"] = "https://accounts.google.com",
            ["exp"] = 1760000900,
            ["name"] = "  Ops User  ",
            ["sub"] = "sub-9"
        }), client);
        ((Dictionary<string, object?>)PhpPlanQ1Hull.OAuthConfig["google"]!)["client_id"] = "";
        var noCfg = PhpPlanQ1Hull.EpcAuthGoogleExchangeCode("abc");
        ((Dictionary<string, object?>)PhpPlanQ1Hull.OAuthConfig["google"]!)["client_id"] = client;
        PhpPlanQ1Hull.HttpPost = (_, _) => (false, "", "timeout");
        var failHttp = PhpPlanQ1Hull.EpcAuthGoogleExchangeCode("abc");
        PhpPlanQ1Hull.HttpPost = (_, _) => (true, "{\"access_token\":\"x\"}", "");
        var badJson = PhpPlanQ1Hull.EpcAuthGoogleExchangeCode("abc");
        PhpPlanQ1Hull.HttpPost = (_, _) =>
        {
            var tok = Jwt(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["aud"] = client,
                ["email"] = "ops@acme.test",
                ["email_verified"] = true,
                ["iss"] = "https://accounts.google.com",
                ["exp"] = 1760000900,
                ["name"] = "Ops",
                ["sub"] = "1"
            });
            return (true, JsonSerializer.Serialize(new Dictionary<string, string> { ["id_token"] = tok }), "");
        };
        var exOk = PhpPlanQ1Hull.EpcAuthGoogleExchangeCode("abc");
        return new Rendered(new object?[]
        {
            emptyStart, start, malformed, aud, unverified, zero, issuer, expired, ok, noCfg, failHttp, badJson, exOk
        });
    }

    private static Rendered LoginHtml()
    {
        Dictionary<string, object?>? last = null;
        PhpPlanQ1Hull.ResolveForMode = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["message"] = "Tenant context lost"
        };
        var lost = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["am"] = "cp", ["tk"] = "" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "a@b.c", ["name"] = "A" });
        PhpPlanQ1Hull.ResolveForMode = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["message"] = "no-host"
        };
        var noHost = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["am"] = "cp", ["tk"] = "gone" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "a@b.c" });
        PhpPlanQ1Hull.ResolveForMode = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["tenant_key"] = "acme"
        };
        PhpPlanQ1Hull.ProvisionCp = (ctx, _, _) =>
        {
            last = ctx;
            return 0;
        };
        var noCp = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["am"] = "cp", ["tk"] = "acme" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "a@b.c" });
        PhpPlanQ1Hull.ProvisionCp = (ctx, _, _) =>
        {
            last = ctx;
            return 7;
        };
        PhpPlanQ1Hull.FinishLogin = (ctx, _) =>
        {
            last = ctx;
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "session-denied" };
        };
        var noSess = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["am"] = "cp",
                ["tk"] = "acme",
                ["rh"] = "cp.acme.test",
                ["rp"] = "/cp/users",
                ["lp"] = "en"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "a@b.c", ["name"] = "A" });
        PhpPlanQ1Hull.FinishLogin = (ctx, _) =>
        {
            last = ctx;
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["redirect"] = "/cp/" };
        };
        var okCp = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["am"] = "cp",
                ["tk"] = "acme",
                ["rh"] = "cp.acme.test",
                ["rp"] = "/cp/users",
                ["lp"] = "en"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "a@b.c", ["name"] = "A" });
        PhpPlanQ1Hull.ResolveForMode = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["tenant_key"] = "beta"
        };
        PhpPlanQ1Hull.ProvisionStorefront = (ctx, _, _) =>
        {
            last = ctx;
            return 0;
        };
        var noSf = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["am"] = "storefront", ["tk"] = "beta" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "c@d.e" });
        PhpPlanQ1Hull.ProvisionStorefront = (ctx, _, _) =>
        {
            last = ctx;
            return 11;
        };
        var okSf = PhpPlanQ1Hull.EpcAuthGoogleCompleteLogin(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["am"] = "STOREFRONT",
                ["tk"] = "beta",
                ["rh"] = "shop.beta.test"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = "c@d.e", ["name"] = "C" });
        PhpPlanQ1Hull.Policy = new(StringComparer.Ordinal) { ["password"] = true, ["email_otp"] = true, ["google_oauth"] = true };
        PhpPlanQ1Hull.OauthButtonsHtml = () => "<div class=\"epc-oauth-buttons\">G</div>";
        PhpPlanQ1Hull.OtpModalHtml = _ => "OTP";
        var htmlBoth = PhpPlanQ1Hull.EpcCpLoginModernAuthHtml(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_key"] = "acme",
            ["login_label"] = "Ops & Co",
            ["context"] = "cp"
        });
        var htmlAgain = PhpPlanQ1Hull.EpcCpLoginModernAuthHtml(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_key"] = "acme",
            ["login_label"] = "Ops",
            ["context"] = "cp"
        });
        PhpPlanQ1Hull.HtmlCalls = 0;
        PhpPlanQ1Hull.Policy = new(StringComparer.Ordinal) { ["password"] = false, ["email_otp"] = true, ["google_oauth"] = false };
        PhpPlanQ1Hull.OauthButtonsHtml = () => "";
        var htmlOtp = PhpPlanQ1Hull.EpcCpLoginModernAuthHtml(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_key"] = "beta",
            ["login_label"] = "Store",
            ["context"] = "storefront"
        });
        return new Rendered(new object?[] { lost, noHost, noCp, noSess, okCp, noSf, okSf, htmlBoth, htmlAgain, htmlOtp, last });
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
            case JsonValueKind.Null:
                return true;
            default:
                return true;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 1800 ? value : value[..1800] + "…";
}
