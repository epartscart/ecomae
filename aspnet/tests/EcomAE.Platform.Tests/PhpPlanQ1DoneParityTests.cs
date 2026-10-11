using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1DoneParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Done");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpPlanQ1Done.PhpServingDeactivatePath,
        PhpPlanQ1Done.CpTracePath,
        PhpPlanQ1Done.ConsultingDataPath,
        PhpPlanQ1Done.MarketingContentPath,
        PhpPlanQ1Done.Soc2CompliancePath
    };

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
    public void PlanQ1Done_MatchPhpGolden()
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
            if (!Same(actual, expected))
            {
                failures.Add(name + " expected=" + Truncate(expected.GetRawText()) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(CoveredFiles, new HashSet<string>(StringComparer.Ordinal)
        {
            CaseFile("deact_off"),
            CaseFile("trace_off"),
            CaseFile("cpi_data"),
            CaseFile("mkt_catalogs"),
            CaseFile("soc2_flow")
        });

    [Fact]
    public void DeactivateOff_DoesNotStartASession()
    {
        var ctx = new PhpPlanQ1Done.DeactivateCtx { DocumentRoot = Path.GetTempPath() };
        Assert.False(PhpPlanQ1Done.EpcPhpServingIsTemporarilyDeactivated(ctx));
    }

    private static string CaseFile(string name)
        => name.StartsWith("deact_", StringComparison.Ordinal) ? PhpPlanQ1Done.PhpServingDeactivatePath
            : name.StartsWith("trace_", StringComparison.Ordinal) ? PhpPlanQ1Done.CpTracePath
            : name.StartsWith("cpi_", StringComparison.Ordinal) ? PhpPlanQ1Done.ConsultingDataPath
            : name.StartsWith("mkt_", StringComparison.Ordinal) ? PhpPlanQ1Done.MarketingContentPath
            : name.StartsWith("soc2_", StringComparison.Ordinal) ? PhpPlanQ1Done.Soc2CompliancePath
            : "unknown";

    private static string Render(string name)
        => name switch
        {
            "deact_off" => Json(DeactOff()),
            "deact_on" => Json(DeactOn()),
            "trace_off" => Json(TraceOff()),
            "trace_on_get" => Json(TraceOnGet()),
            "trace_on_flag" => Json(TraceOnFlag()),
            "trace_write" => Json(TraceWrite()),
            "cpi_data" => Json(CpiData()),
            "mkt_catalogs" => Json(MktCatalogs()),
            "soc2_flow" => Json(Soc2Flow()),
            _ => "unknown:" + name
        };

    private static object[] DeactOff()
    {
        var doc = NewTemp();
        var ctx = new PhpPlanQ1Done.DeactivateCtx { DocumentRoot = doc, Sapi = "cli" };
        return new object[]
        {
            PhpPlanQ1Done.EpcPhpServingIsTemporarilyDeactivated(ctx),
            PhpPlanQ1Done.EpcPhpServingDeactivatedMaybeExit(ctx)
        };
    }

    private static object[] DeactOn()
    {
        var doc = NewTemp();
        File.WriteAllText(Path.Combine(doc, ".epc_php_serving_deactivated"), "1");
        var ctx = new PhpPlanQ1Done.DeactivateCtx { DocumentRoot = doc, Sapi = "cli" };
        return new object[]
        {
            PhpPlanQ1Done.EpcPhpServingIsTemporarilyDeactivated(ctx),
            PhpPlanQ1Done.EpcPhpServingDeactivatedMaybeExit(ctx),
            ctx.Sapi == "cli"
        };
    }

    private static object[] TraceOff()
    {
        var ctx = new PhpPlanQ1Done.TraceCtx { TempDir = NewTemp() };
        return new object[]
        {
            PhpPlanQ1Done.EpcCpTraceEnabled(ctx),
            PhpPlanQ1Done.EpcCpTraceLogfile(ctx).EndsWith("/epc_cp_trace.log", StringComparison.Ordinal)
        };
    }

    private static object TraceOnGet()
    {
        var ctx = new PhpPlanQ1Done.TraceCtx { TempDir = NewTemp() };
        ctx.Get["epc_trace"] = "1";
        return PhpPlanQ1Done.EpcCpTraceEnabled(ctx);
    }

    private static object TraceOnFlag()
    {
        var tmp = NewTemp();
        File.WriteAllText(Path.Combine(tmp, "epc_cp_trace.on"), "1");
        var ctx = new PhpPlanQ1Done.TraceCtx { TempDir = tmp };
        return PhpPlanQ1Done.EpcCpTraceEnabled(ctx);
    }

    private static object[] TraceWrite()
    {
        var tmp = NewTemp();
        var ctx = new PhpPlanQ1Done.TraceCtx { TempDir = tmp, RequestUri = "/cp/control" };
        ctx.Get["epc_trace"] = "1";
        PhpPlanQ1Done.EpcCpTrace(ctx, "phase-a");
        var line = File.ReadAllText(PhpPlanQ1Done.EpcCpTraceLogfile(ctx));
        return new object[] { line.Contains("phase-a", StringComparison.Ordinal), line.Contains("/cp/control", StringComparison.Ordinal), line.EndsWith('\n') };
    }

    private static object?[] CpiData()
    {
        var stamped = PhpPlanQ1Done.EpcCpiStampSkus(new[]
        {
            new Dictionary<string, object?> { ["name"] = "x" },
            new Dictionary<string, object?> { ["sku"] = "KEEP", ["name"] = "y" }
        }, "AB");
        var packages = PhpPlanQ1Done.EpcCpiServicePackages();
        return new object?[]
        {
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiNavLinks().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiHeaderContact().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiHeroSlides().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiIconBoxes().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiCredentials().GetRawText()),
            PhpPlanQ1Done.EpcCpiAboutImageUrl(),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiServices().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiStats().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiTeam().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiTestimonials().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiPartners().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiProcessSteps().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiFooterColumns().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiThemePalette().GetRawText()),
            PhpPlanQ1Done.EpcCpiProHeroEyebrow(),
            PhpPlanQ1Done.EpcCpiProHeroTitle(),
            PhpPlanQ1Done.EpcCpiProHeroCopy(),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiProHeroActions("ar").GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Done.EpcCpiProHeroStats().GetRawText()),
            stamped,
            new[]
            {
                PhpPlanQ1Done.EpcCpiFormatAed(0),
                PhpPlanQ1Done.EpcCpiFormatAed(-1),
                PhpPlanQ1Done.EpcCpiFormatAed(2500),
                PhpPlanQ1Done.EpcCpiFormatAed("3500.9")
            },
            packages.GetArrayLength(),
            packages[0].GetProperty("sku").GetString(),
            packages[11].GetProperty("sku").GetString()
        };
    }

    private static object?[] MktCatalogs()
    {
        var d = PhpPlanQ1Done.EpcEcomaeDocsCatalog();
        var c = PhpPlanQ1Done.EpcEcomaeCompareCatalog();
        var b = PhpPlanQ1Done.EpcEcomaeBosArticlesCatalog();
        var s = PhpPlanQ1Done.EpcEcomaeSolutionsCatalog();
        return new object?[]
        {
            d.EnumerateObject().Select(p => p.Name).ToArray(),
            d.EnumerateObject().Count(),
            TitleOf(d.EnumerateObject().First().Value),
            c.EnumerateObject().Select(p => p.Name).ToArray(),
            c.EnumerateObject().Count(),
            TitleOf(c.EnumerateObject().First().Value),
            b.EnumerateObject().Select(p => p.Name).ToArray(),
            b.EnumerateObject().Count(),
            TitleOf(b.EnumerateObject().First().Value),
            s.EnumerateObject().Select(p => p.Name).ToArray(),
            s.EnumerateObject().Count(),
            TitleOf(s.EnumerateObject().First().Value)
        };
    }

    private static object?[] Soc2Flow()
    {
        var store = new PhpPlanQ1Done.Soc2Store();
        var seed1 = PhpPlanQ1Done.EpcSoc2SeedControls(store);
        var seed2 = PhpPlanQ1Done.EpcSoc2SeedControls(store);
        var all = PhpPlanQ1Done.EpcSoc2ListControls(store);
        var sec = PhpPlanQ1Done.EpcSoc2ListControls(store, "security");
        var upd = PhpPlanQ1Done.EpcSoc2UpdateControl(store, "CC5.1", new Dictionary<string, object?> { ["status"] = "effective", ["owner"] = "sec" });
        var empty = PhpPlanQ1Done.EpcSoc2UpdateControl(store, "CC5.1", new Dictionary<string, object?> { ["nope"] = 1 });
        var ev = PhpPlanQ1Done.EpcSoc2AddEvidence(store, "CC5.1", new Dictionary<string, object?>
        {
            ["title"] = "shot",
            ["evidence_type"] = "screenshot",
            ["collected_by"] = "op"
        });
        var gap = PhpPlanQ1Done.EpcSoc2GapAnalysis(store);
        var pol = PhpPlanQ1Done.EpcSoc2CreatePolicy(store, new Dictionary<string, object?>
        {
            ["policy_code"] = "isp",
            ["title"] = "InfoSec",
            ["content"] = "body",
            ["owner"] = "ciso",
            ["related_controls"] = new[] { "CC5.1" }
        });
        var fleet = PhpPlanQ1Done.EpcSoc2FleetStats(store);
        var plan = PhpPlanQ1Done.EpcSoc2RemediationPlan(store);
        var crit = plan.Where(x => Convert.ToInt32(x["priority"], System.Globalization.CultureInfo.InvariantCulture) == 1).ToList();
        var sum = PhpPlanQ1Done.EpcSoc2EvidenceSummary(store);
        var rep = PhpPlanQ1Done.EpcSoc2ComplianceReport(store);
        var builtin = PhpPlanQ1Done.EpcSoc2BuiltinControls();
        return new object?[]
        {
            PhpPlanQ1Done.EpcSoc2Version,
            builtin.Count,
            builtin[0]["control_id"],
            builtin[^1]["control_id"],
            seed1,
            seed2,
            all.Count,
            sec.Count,
            upd,
            empty,
            ev["ok"],
            Convert.ToInt32(ev["evidence_id"], System.Globalization.CultureInfo.InvariantCulture) > 0,
            gap["total_controls"],
            gap["readiness_pct"],
            ((List<Dictionary<string, object?>>)gap["gaps"]!).Count,
            ((Dictionary<string, int>)gap["by_status"]!)["effective"],
            pol["ok"],
            fleet,
            plan.Count,
            plan[0]["priority"],
            crit.Count,
            crit.Any(x => (string?)x["control_id"] == "A1.2"),
            sum.Count,
            rep
        };
    }

    private static string? TitleOf(JsonElement el)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty("title", out var title) ? title.GetString() : null;

    private static string NewTemp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1d_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(dir);
        return dir;
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
        => value.Length <= 280 ? value : value[..280] + "…";
}
