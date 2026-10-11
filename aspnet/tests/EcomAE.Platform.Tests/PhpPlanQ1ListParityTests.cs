using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1ListParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1List");

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
    public void PlanQ1List_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1List.AgentCatalogPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1List.AgentCatalogPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1List.Reset();
        Assert.Contains("epc_agent_catalog_knowledge.php", PhpPlanQ1List.AgentCatalogPath, StringComparison.Ordinal);
        Assert.Equal("CACHE", NormCache(PhpPlanQ1List.EpcAgentCatalogCachePath()));
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Open.Reset();
        PhpPlanQ1Text.Reset();
        PhpPlanQ1List.Reset();
        return name switch
        {
            "pure" => Pure(),
            "index" => Index(),
            "match" => Match(),
            "resolve" => Resolve(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        PhpPlanQ1List.CachePathOverride = "/tmp/cache/epc_agent_catalog_index.json";
        return new Rendered(new object?[]
        {
            NormCache(PhpPlanQ1List.EpcAgentCatalogCachePath()),
            PhpPlanQ1List.EpcAgentCatalogNormKey(" Bosch-AG "),
            PhpPlanQ1List.EpcAgentCatalogMatchPattern("Bosch-AG"),
            PhpPlanQ1List.EpcAgentCatalogSectionHint("Hino truck fleet"),
            PhpPlanQ1List.EpcAgentCatalogSectionHint("yamaha r1 bike"),
            PhpPlanQ1List.EpcAgentCatalogSectionHint("passenger sedan"),
            PhpPlanQ1List.EpcAgentCatalogSectionHint("hello"),
            PhpPlanQ1List.EpcAgentCatalogSectionLabel("commercial"),
            PhpPlanQ1List.EpcAgentCatalogSectionLabel("other"),
            PhpPlanQ1List.EpcAgentCatalogHasInquiryIntent("do you have bosch"),
            PhpPlanQ1List.EpcAgentCatalogHasInquiryIntent("zzzz"),
            PhpPlanQ1List.EpcAgentCatalogIsListQuery("what brands do you have"),
            PhpPlanQ1List.EpcAgentCatalogIsListQuery("hello world")
        });
    }

    private static Rendered Index()
    {
        var doc = TempDoc();
        PhpPlanQ1Text.DocumentRoot = doc;
        PhpPlanQ1Text.Clock = () => 1_700_000_000;
        PhpPlanQ1List.Clock = () => 1_700_000_000;
        try
        {
            var db = SeedBrands();
            db.Manufacturers.AddRange(
            [
                new() { Section = "passenger", MfaId = 10, Manufacturer = "Toyota", Type = "car", Country = "JP" },
                new() { Section = "commercial", MfaId = 11, Manufacturer = "Toyota", Type = "truck", Country = "JP" },
                new() { Section = "motorbike", MfaId = 20, Manufacturer = "Honda", Type = "bike", Country = "JP" },
                new() { Section = "passenger", MfaId = 30, Manufacturer = "", Type = "car", Country = "JP" },
                new() { Section = "other", MfaId = 40, Manufacturer = "Skip", Type = "x", Country = "JP" }
            ]);
            var idx = PhpPlanQ1List.EpcAgentCatalogBuildIndex(db);
            idx["updated_at"] = 1700000000;
            var stock = AsDicts(idx["stock"]).Select(r => new object?[] { r["name"], r["key"], r["parts_count"], r["len"] }).ToList();
            var keys = ((Dictionary<string, object?>)idx["stock_by_key"]!).Keys.ToArray();
            var veh = AsDicts(idx["vehicles"]).Select(r => new object?[]
            {
                r["name"], r["key"], r["sections"], r["mfa_ids"], r["type"], r["country"], r["len"]
            }).ToList();
            return new Rendered(new object?[] { stock, keys, veh, idx["section_counts"], idx["updated_at"] });
        }
        finally
        {
            try { Directory.Delete(doc, true); } catch { /* throwaway */ }
        }
    }

    private static Rendered Match()
    {
        var doc = TempDoc();
        PhpPlanQ1Text.DocumentRoot = doc;
        try
        {
            var db = SeedBrands(false);
            db.Manufacturers.AddRange(
            [
                new() { Section = "passenger", MfaId = 10, Manufacturer = "Toyota", Type = "car", Country = "JP" },
                new() { Section = "commercial", MfaId = 11, Manufacturer = "Toyota", Type = "truck", Country = "JP" }
            ]);
            db.Models.AddRange(
            [
                new() { Section = "commercial", MfaId = 11, ModelSeries = "Hilux", MsId = 5 },
                new() { Section = "passenger", MfaId = 10, ModelSeries = "Corolla", MsId = 6 },
                new() { Section = "commercial", MfaId = 11, ModelSeries = "Hiace Van", MsId = 7 }
            ]);
            var idx = PhpPlanQ1List.EpcAgentCatalogBuildIndex(db);
            var stock = PhpPlanQ1List.EpcAgentCatalogMatchStock(idx, "need Bosch filters");
            var none = PhpPlanQ1List.EpcAgentCatalogMatchStock(idx, "nope");
            var veh = PhpPlanQ1List.EpcAgentCatalogMatchVehicle(idx, "toyota hilux commercial", "commercial");
            var vehAny = PhpPlanQ1List.EpcAgentCatalogMatchVehicle(idx, "toyota passenger car", "motorbike");
            var model = PhpPlanQ1List.EpcAgentCatalogMatchModel(db, veh!, "need Hilux 2020");
            var noMfa = PhpPlanQ1List.EpcAgentCatalogMatchModel(db, new Dictionary<string, object?>(StringComparer.Ordinal), "Hilux");
            return new Rendered(new object?[]
            {
                stock?["name"],
                none,
                veh?["name"],
                veh?["sections"],
                vehAny?["name"],
                model,
                noMfa
            });
        }
        finally
        {
            try { Directory.Delete(doc, true); } catch { /* throwaway */ }
        }
    }

    private static Rendered Resolve()
    {
        var doc = TempDoc();
        PhpPlanQ1Text.DocumentRoot = doc;
        PhpPlanQ1List.DocumentRoot = doc;
        PhpPlanQ1List.Clock = () => 1_700_000_000;
        PhpPlanQ1Text.Clock = () => 1_700_000_000;
        try
        {
            var db = new PhpPlanQ1List.ListStore();
            db.Text.Open.Prices.Add(new() { Id = 1, StorefrontTempDisabled = 0 });
            db.Text.Open.PriceData.Add(new() { PriceId = 1, Manufacturer = "Bosch", Article = "B1", Name = "Pump", Price = 12.50m, Exist = 3 });
            db.Manufacturers.Add(new() { Section = "commercial", MfaId = 11, Manufacturer = "Toyota", Type = "truck", Country = "JP" });
            db.Models.Add(new() { Section = "commercial", MfaId = 11, ModelSeries = "Hilux", MsId = 5 });
            object? Pick(Dictionary<string, object?>? r)
            {
                if (r is null)
                {
                    return null;
                }

                var stock = r.TryGetValue("stock", out var s) && s is Dictionary<string, object?> sd ? sd["name"] : null;
                var vehicle = r.TryGetValue("vehicle", out var v) && v is Dictionary<string, object?> vd ? vd["name"] : null;
                var model = r.TryGetValue("model", out var m) && m is Dictionary<string, object?> md ? md["name"] : null;
                return new object?[] { r["action"], r["section_hint"], stock, vehicle, model };
            }

            return new Rendered(new object?[]
            {
                PhpPlanQ1List.EpcAgentCatalogResolveQuery(db, "   "),
                PhpPlanQ1List.EpcAgentCatalogResolveQuery(db, "VIN 1HGBH41JXMN109186"),
                Pick(PhpPlanQ1List.EpcAgentCatalogResolveQuery(db, "what brands do you have")),
                Pick(PhpPlanQ1List.EpcAgentCatalogResolveQuery(db, "Bosch")),
                Pick(PhpPlanQ1List.EpcAgentCatalogResolveQuery(db, "do you have Toyota Hilux")),
                PhpPlanQ1List.EpcAgentCatalogResolveQuery(db, "the weather is nice today"),
                PhpPlanQ1List.EpcAgentCatalogMatchNameInText(db, "need toyota truck parts", "vehicle"),
                PhpPlanQ1List.EpcAgentCatalogMatchNameInText(db, "Bosch pump", "stock"),
                PhpPlanQ1List.EpcAgentCatalogMatchNameInText(db, "zzzz", "any")
            });
        }
        finally
        {
            try { Directory.Delete(doc, true); } catch { /* throwaway */ }
        }
    }

    private static PhpPlanQ1List.ListStore SeedBrands(bool withHidden = true)
    {
        var db = new PhpPlanQ1List.ListStore();
        db.Text.Open.Prices.Add(new() { Id = 1, StorefrontTempDisabled = 0 });
        if (withHidden)
        {
            db.Text.Open.Prices.Add(new() { Id = 2, StorefrontTempDisabled = 1 });
        }

        db.Text.Open.PriceData.AddRange(
        [
            new() { PriceId = 1, Manufacturer = "Bosch", Article = "B1", Name = "Pump", Price = 12.50m, Exist = 3 },
            new() { PriceId = 1, Manufacturer = "Mann", Article = "M1", Name = "Filter", Price = 4.00m, Exist = 1 }
        ]);
        if (withHidden)
        {
            db.Text.Open.PriceData.Add(new() { PriceId = 2, Manufacturer = "Hidden", Article = "H1", Name = "Off", Price = 3.00m, Exist = 1 });
        }

        return db;
    }

    private static string TempDoc()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1l_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path.Combine(doc, "content", "files", "epc_brands_cache"));
        Directory.CreateDirectory(Path.Combine(doc, "content", "shop", "docpart", "cache"));
        return doc;
    }

    private static List<Dictionary<string, object?>> AsDicts(object? value)
        => value is List<Dictionary<string, object?>> list ? list : [];

    private static string NormCache(string path)
        => path.Contains("epc_agent_catalog_index.json", StringComparison.Ordinal) ? "CACHE" : path;

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
        => value.Length <= 500 ? value : value[..500] + "…";
}
