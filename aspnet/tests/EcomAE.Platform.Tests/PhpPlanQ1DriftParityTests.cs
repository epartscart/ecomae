using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1DriftParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Drift");

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
    public void PlanQ1Drift_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Drift.MfaUiPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Drift.MfaUiPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Drift.Reset();
        Assert.Contains("epc_mfa_ui.php", PhpPlanQ1Drift.MfaUiPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Drift.MfaUiPath, StringComparison.Ordinal);
        Assert.Equal("epcMfaAjax", PhpPlanQ1Drift.EpcMfaAjax());
        Assert.Equal("epcMfaStartEnroll", PhpPlanQ1Drift.EpcMfaStartEnroll());
        Assert.Equal("epcMfaRegenBackup", PhpPlanQ1Drift.EpcMfaRegenBackup());
        Assert.Equal("epcMfaDisable", PhpPlanQ1Drift.EpcMfaDisable());
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Drift.Reset();
        PhpPlanQ1Drift.QrDataUri = uri => "QR[" + uri + "]";
        return name switch
        {
            "enroll" => Enroll(),
            "verify" => new Rendered(PhpPlanQ1Drift.EpcMfaRenderVerifyPage()),
            "settings" => Settings(),
            "methods" => Methods(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Enroll()
    {
        var full = PhpPlanQ1Drift.EpcMfaRenderEnrollPage(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["secret"] = "AB&C's <x>",
            ["qr_uri"] = "otpauth://totp/EcomAE:user?secret=AB&issuer=Ecom",
            ["backup_codes"] = new object[] { "CODE-1", "O'Reilly" }
        });
        var empty = PhpPlanQ1Drift.EpcMfaRenderEnrollPage(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["secret"] = "",
            ["qr_uri"] = ""
        });
        var zero = PhpPlanQ1Drift.EpcMfaRenderEnrollPage(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["secret"] = "0",
            ["qr_uri"] = "x",
            ["backup_codes"] = "0"
        });
        return new Rendered(new object[] { full, empty, zero });
    }

    private static Rendered Settings()
    {
        var off = PhpPlanQ1Drift.EpcMfaRenderSettingsPanel(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enrolled"] = false,
            ["backup_codes_left"] = 3,
            ["session_verified"] = true
        });
        var on = PhpPlanQ1Drift.EpcMfaRenderSettingsPanel(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enrolled"] = true,
            ["backup_codes_left"] = 3,
            ["session_verified"] = true
        });
        var nobackup = PhpPlanQ1Drift.EpcMfaRenderSettingsPanel(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enrolled"] = 1,
            ["backup_codes_left"] = 0,
            ["session_verified"] = false
        });
        return new Rendered(new object[] { off, on, nobackup });
    }

    private static Rendered Methods()
    {
        var html = PhpPlanQ1Drift.EpcMfaRenderSettingsPanel(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enrolled"] = true,
            ["backup_codes_left"] = 1,
            ["session_verified"] = true,
            ["methods"] = new object[]
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["method"] = "totp",
                    ["label"] = "App <main>",
                    ["confirmed"] = 1,
                    ["last_used_at"] = "2026-10-09 12:00:00"
                },
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["method"] = "backup",
                    ["label"] = "Codes",
                    ["confirmed"] = "0"
                }
            }
        });
        var none = PhpPlanQ1Drift.EpcMfaRenderSettingsPanel(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enrolled"] = true,
            ["backup_codes_left"] = 2,
            ["session_verified"] = true,
            ["methods"] = Array.Empty<object>()
        });
        return new Rendered(new object[]
        {
            html, none,
            html.Contains("epcMfaAjax", StringComparison.Ordinal),
            html.Contains("epcMfaStartEnroll", StringComparison.Ordinal),
            html.Contains("epcMfaRegenBackup", StringComparison.Ordinal),
            html.Contains("epcMfaDisable", StringComparison.Ordinal)
        });
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
        => value.Length <= 1200 ? value : value[..1200] + "…";
}
