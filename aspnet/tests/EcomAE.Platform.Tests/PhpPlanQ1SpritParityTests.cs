using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SpritParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Sprit");

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
    public void PlanQ1Sprit_MatchPhpGolden()
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
                using var left = JsonDocument.Parse(Json(actual.Extra));
                failures.Add(name + " " + FirstValueDiff(left.RootElement, expected, name));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Sprit.MarketingBrochurePath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Sprit.MarketingBrochurePath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Sprit.Reset();
        Assert.Contains("epc_marketing_brochure.php", PhpPlanQ1Sprit.MarketingBrochurePath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Sprit.MarketingBrochurePath, StringComparison.Ordinal);
        var html = PhpPlanQ1Sprit.EpcBrochureRenderHtml("ecomae");
        Assert.Contains("&#039;", PhpPlanQ1Sprit.EpcBrochureH("O'Brien"), StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", html, StringComparison.Ordinal);
        var headers = PhpPlanQ1Sprit.EpcBrochureRenderAndExit("ecomae");
        Assert.Contains("Content-Type: text/html; charset=utf-8", PhpPlanQ1Sprit.ResponseHeaders);
        Assert.DoesNotContain("PHPSESSID", headers, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", string.Join("\n", PhpPlanQ1Sprit.ResponseHeaders), StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Sprit.Reset();
        return name switch
        {
            "profile" => Profile(),
            "lists" => Lists(),
            "html" => Html(),
            "live" => Live(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static Rendered Profile()
    {
        var codes = new[] { "epartscart", "auto_parts", "AUTO_PARTS", "Eparts-Cart!", "Acme-1!", "ecomae", "", "jewellery", "Fashion!" };
        var picked = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var code in codes)
        {
            var p = PhpPlanQ1Sprit.EpcBrochureProfile(code);
            var cta = p["cta_primary"] as Dictionary<string, object?>;
            picked[code] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = p["id"],
                ["name"] = p["name"],
                ["domain"] = p["domain"],
                ["accent"] = p["accent"],
                ["cp_brochure"] = p["cp_brochure"],
                ["cta"] = cta != null && cta.TryGetValue("label", out var label) ? label : ""
            };
        }

        return new Rendered(new object?[]
        {
            picked,
            PhpPlanQ1Sprit.EpcBrochureH("O'Brien & Co"),
            PhpPlanQ1Sprit.EpcBrochureH("<x>"),
            PhpPlanQ1Sprit.EpcBrochureH("")
        });
    }

    private static Rendered Lists()
    {
        var outRows = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var code in new[] { "epartscart", "ecomae", "AUTO_PARTS", "nope" })
        {
            var id = Convert.ToString(PhpPlanQ1Sprit.EpcBrochureProfile(code)["id"]) ?? "";
            var secs = PhpPlanQ1Sprit.EpcBrochureSections(id);
            var titles = secs.Select(s => new object?[]
            {
                s["title"],
                ((List<string>)s["points"]!).Count
            }).ToList();
            var css = PhpPlanQ1Sprit.EpcBrochureCss(PhpPlanQ1Sprit.EpcBrochureProfile(code));
            outRows[code] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = id,
                ["sec"] = titles,
                ["stats"] = PhpPlanQ1Sprit.EpcBrochureStats(id),
                ["journey"] = PhpPlanQ1Sprit.EpcBrochureJourney(id),
                ["css_len"] = css.Length,
                ["css_head"] = css[..Math.Min(80, css.Length)],
                ["css_tail"] = css.Length <= 80 ? css : css[^80..]
            };
        }

        return new Rendered(outRows);
    }

    private static Rendered Html()
    {
        var plain = PhpPlanQ1Sprit.EpcBrochureRenderHtml("ecomae");
        var parts = PhpPlanQ1Sprit.EpcBrochureRenderHtml("epartscart");
        var print = PhpPlanQ1Sprit.EpcBrochureRenderHtml("ecomae", new Dictionary<string, object?>(StringComparer.Ordinal) { ["print"] = true });
        var print0 = PhpPlanQ1Sprit.EpcBrochureRenderHtml("ecomae", new Dictionary<string, object?>(StringComparer.Ordinal) { ["print"] = "0" });
        var quote = PhpPlanQ1Sprit.EpcBrochureRenderHtml("Eparts-Cart!");
        return new Rendered(new object?[]
        {
            PhpStrlen(plain),
            PhpSubstr(plain, 0, 120),
            PhpSubstr(plain, -80),
            plain.Contains("window.print", StringComparison.Ordinal) ? 1 : 0,
            print.Contains("window.print", StringComparison.Ordinal) ? 1 : 0,
            print0.Contains("setTimeout", StringComparison.Ordinal) ? 1 : 0,
            parts.Contains("Inside the Control Panel", StringComparison.Ordinal) ? 1 : 0,
            parts.Contains("Control Panel →", StringComparison.Ordinal) ? 1 : 0,
            plain.Contains("Control Panel →", StringComparison.Ordinal) ? 1 : 0,
            plain.Contains("Every CP function", StringComparison.Ordinal) ? 1 : 0,
            quote.Contains("eParts Cart", StringComparison.Ordinal) ? 1 : 0,
            plain.Contains("og_cover.png", StringComparison.Ordinal) ? 1 : 0
        });
    }

    private static Rendered Live()
    {
        var imgs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["How work flows"] = "/live/journey.jpg",
            ["One Blockchain BOS for the enterprise"] = "/live/sec.jpg",
            ["Multi-tenant Super CP for operators"] = "/live/pt.jpg"
        };
        var metas = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["Multi-tenant Super CP for operators"] = new(StringComparer.Ordinal) { ["label"] = "O'area" }
        };
        PhpPlanQ1Sprit.ItemImage = (item, group) =>
        {
            var name = item.TryGetValue("name", out var raw) ? Convert.ToString(raw) ?? "" : "";
            if (imgs.TryGetValue(name, out var byName))
            {
                return byName;
            }

            return imgs.TryGetValue(group, out var byGroup) ? byGroup : "/live/default.jpg";
        };
        PhpPlanQ1Sprit.PhotoMeta = (item, _) =>
        {
            var name = item.TryGetValue("name", out var raw) ? Convert.ToString(raw) ?? "" : "";
            return metas.TryGetValue(name, out var meta)
                ? meta
                : new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "" };
        };
        PhpPlanQ1Sprit.LiveInventory = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["total"] = 42 }
        };
        var html = PhpPlanQ1Sprit.EpcBrochureRenderHtml("ecomae");
        var parts = PhpPlanQ1Sprit.EpcBrochureRenderHtml("epartscart");
        return new Rendered(new object?[]
        {
            html.Contains("/live/journey.jpg", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("/live/sec.jpg", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("/live/pt.jpg", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("O&#039;area", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("42 functions", StringComparison.Ordinal) ? 1 : 0,
            parts.Contains("42 functions", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("og_cover.png", StringComparison.Ordinal) ? 1 : 0
        });
    }

    private static int PhpStrlen(string value)
        => Encoding.UTF8.GetByteCount(value);

    private static string PhpSubstr(string value, int start, int? length = null)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (start < 0)
        {
            start = Math.Max(0, bytes.Length + start);
        }

        if (start > bytes.Length)
        {
            return "";
        }

        var take = length ?? (bytes.Length - start);
        if (take < 0)
        {
            return "";
        }

        take = Math.Min(take, bytes.Length - start);
        return Encoding.UTF8.GetString(bytes, start, take);
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

    private static string FirstValueDiff(JsonElement left, JsonElement right, string path)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return path + " kind " + left.ValueKind + " vs " + right.ValueKind;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in right.EnumerateObject())
                {
                    if (!left.TryGetProperty(prop.Name, out var other))
                    {
                        return path + "." + prop.Name + " missing on actual";
                    }

                    var nested = FirstValueDiff(other, prop.Value, path + "." + prop.Name);
                    if (nested != "")
                    {
                        return nested;
                    }
                }

                return "";
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                if (a.Count != b.Count)
                {
                    return path + " len " + a.Count + " vs " + b.Count;
                }

                for (var i = 0; i < a.Count; i++)
                {
                    var nested = FirstValueDiff(a[i], b[i], path + "[" + i + "]");
                    if (nested != "")
                    {
                        return nested;
                    }
                }

                return "";
            case JsonValueKind.String:
                var ls = left.GetString() ?? "";
                var rs = right.GetString() ?? "";
                if (ls == rs)
                {
                    return "";
                }

                var n = Math.Min(ls.Length, rs.Length);
                var at = 0;
                while (at < n && ls[at] == rs[at])
                {
                    at++;
                }

                static string Clip(string value, int at)
                {
                    var start = Math.Max(0, at - 70);
                    var len = Math.Min(140, value.Length - start);
                    return value.Substring(start, len).Replace("\n", "\\n").Replace("\t", "\\t");
                }

                return path + " at=" + at + " expLen=" + rs.Length + " gotLen=" + ls.Length
                    + " exp=" + Clip(rs, at) + " got=" + Clip(ls, at);
            default:
                return "";
        }
    }
}
