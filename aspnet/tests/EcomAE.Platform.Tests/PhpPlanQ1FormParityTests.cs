using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1FormParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex CssStamp = new(@"\?v=\d+(?=[""']|$)", RegexOptions.CultureInvariant);

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Form");

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
    public void PlanQ1Form_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = new
            {
                output = results[i].GetProperty("output").GetString(),
                result = results[i].GetProperty("result")
            };
            var actual = Render(name);
            if (actual.Output != expected.output || !Same(Json(actual.Extra), expected.result))
            {
                failures.Add(name + " outExp=" + Truncate(expected.output ?? "") + " outGot=" + Truncate(actual.Output)
                    + " extraExp=" + Truncate(expected.result.GetRawText()) + " extraGot=" + Truncate(Json(actual.Extra)));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Form.SkuMediaManagerPath, PhpPlanQ1Form.StoragePanelPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Form.SkuMediaManagerPath, PhpPlanQ1Form.StoragePanelPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Form.Reset();
        Assert.Equal("epc_sku_media_cp_config" + ".php", PhpPlanQ1Form.ConfigScriptName());
        Assert.Contains("epc_sku_media_manager.php", PhpPlanQ1Form.SkuMediaManagerPath, StringComparison.Ordinal);
    }

    private sealed record Rendered(string Output, object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Open.Reset();
        PhpPlanQ1View.Reset();
        PhpPlanQ1Form.Reset();
        return name switch
        {
            "mgr_pure" => MgrPure(),
            "mgr_qs" => MgrQs(),
            "ssf_empty" => SsfEmpty(),
            "ssf_rows" => SsfRows(),
            _ => new Rendered("unknown:" + name, null)
        };
    }

    private static Rendered MgrPure()
    {
        var db = new PhpPlanQ1Open.OpenStore();
        var rendered = PhpPlanQ1Form.EpcSkuMediaManagerRender(
            db,
            [],
            [],
            [],
            [],
            new Dictionary<string, object?>(StringComparer.Ordinal),
            "");
        return new Rendered(Norm(Str(rendered["html"])), new object?[] { rendered["css"], rendered["js"] });
    }

    private static Rendered MgrQs()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1f_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path.Combine(doc, "content", "shop", "catalogue"));
        File.WriteAllText(Path.Combine(doc, "content", "shop", "catalogue", "epc_sku_media.css"), ".x{}");
        File.WriteAllText(Path.Combine(doc, "content", "shop", "catalogue", "epc_sku_media.js"), "x");
        PhpPlanQ1Form.DocumentRoot = doc;
        PhpPlanQ1Form.GetAdminSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "a'b" };
        try
        {
            var db = new PhpPlanQ1Open.OpenStore();
            var rendered = PhpPlanQ1Form.EpcSkuMediaManagerRender(
                db,
                [],
                [],
                [],
                [],
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["profile_id"] = "3",
                    ["product_id"] = "7",
                    ["brand"] = "O'Brien",
                    ["article"] = "045&110"
                },
                "/CP/");
            return new Rendered(
                Norm(Str(rendered["html"])),
                new object?[]
                {
                    ((List<string>)rendered["css"]!).Select(Norm).ToList(),
                    ((List<string>)rendered["js"]!).Select(Norm).ToList()
                });
        }
        finally
        {
            try { Directory.Delete(doc, true); } catch { /* throwaway */ }
        }
    }

    private static Rendered SsfEmpty()
    {
        var db = new PhpPlanQ1Open.OpenStore();
        return new Rendered(PhpPlanQ1Form.EpcStorefrontStoragePanelRender(db), new object?[] { "ok" });
    }

    private static Rendered SsfRows()
    {
        var db = new PhpPlanQ1Open.OpenStore();
        db.Interfaces.AddRange(
        [
            new() { Id = 1, HandlerFolder = "", Name = "Warehouse" },
            new() { Id = 2, HandlerFolder = "prices", Name = "Prices" },
            new() { Id = 3, HandlerFolder = "treelax_catalogue", Name = "Catalogue" },
            new() { Id = 4, HandlerFolder = "laximo", Name = "Laximo" }
        ]);
        db.Storages.AddRange(
        [
            new() { Id = 1, Name = "Main WH", ShortName = "MW", InterfaceType = 1, ConnectionOptions = "", StorefrontTempDisabled = 0, Hidden = 0 },
            new() { Id = 2, Name = "Price WH", ShortName = "PW", InterfaceType = 2, ConnectionOptions = "{\"price_id\":10}", StorefrontTempDisabled = 0, Hidden = 0 },
            new() { Id = 3, Name = "Cat WH", ShortName = "CW", InterfaceType = 3, ConnectionOptions = "", StorefrontTempDisabled = 1, Hidden = 0 },
            new() { Id = 4, Name = "Ext WH", ShortName = "EW", InterfaceType = 4, ConnectionOptions = "{\"price_id\":11}", StorefrontTempDisabled = 0, Hidden = 1 }
        ]);
        db.Prices.AddRange(
        [
            new() { Id = 10, Name = "Linked A", StorefrontTempDisabled = 0 },
            new() { Id = 12, Name = "Unlinked C", StorefrontTempDisabled = 1 }
        ]);
        db.PriceData.AddRange(
        [
            new() { Id = 1, PriceId = 12 },
            new() { Id = 2, PriceId = 12 }
        ]);
        db.Audits.AddRange(
        [
            new() { Id = 1, EntityType = "storage", EntityId = 3, EntityName = "Cat WH", StorefrontDisabled = 1, UserLabel = "ops", CreatedAt = "2024-01-02 03:04:05" },
            new() { Id = 2, EntityType = "price_list", EntityId = 12, EntityName = "Unlinked C", StorefrontDisabled = 1, UserLabel = "verify-probe", CreatedAt = "2024-01-03 00:00:00" },
            new() { Id = 3, EntityType = "storage", EntityId = 1, EntityName = "Main's", StorefrontDisabled = 0, UserLabel = "Verify-Probe-X", CreatedAt = "2024-01-04 00:00:00" }
        ]);
        return new Rendered(PhpPlanQ1Form.EpcStorefrontStoragePanelRender(db), new object?[] { "ok" });
    }

    private static string Norm(string value) => CssStamp.Replace(value, "?v=MTIME");

    private static string Str(object? value) => Convert.ToString(value) ?? "";

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
        => value.Length <= 400 ? value : value[..400] + "…";
}
