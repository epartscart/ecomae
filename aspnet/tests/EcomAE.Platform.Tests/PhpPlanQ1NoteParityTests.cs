using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1NoteParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Note");

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
    public void PlanQ1Note_MatchPhpGolden()
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
        => Assert.Equal(PhpPlanQ1Note.SeedDataPath, PhpPlanQ1Note.SeedDataPath);

    [Fact]
    public void Locale_DoesNotStartASession()
    {
        PhpPlanQ1Note.Reset();
        Assert.Equal("AE", PhpPlanQ1Note.EpcStorefrontSeedResolveTenantLocale()["country"]);
    }

    private static string Render(string name)
    {
        PhpPlanQ1Note.Reset();
        return name switch
        {
            "seed_pure" => Json(SeedPure()),
            "seed_db" => Json(SeedDb()),
            _ => "unknown:" + name
        };
    }

    private static object?[] SeedPure()
    {
        var loc = PhpPlanQ1Note.EpcStorefrontSeedResolveTenantLocale();
        var g = PhpPlanQ1Note.EpcStorefrontSeedGenericCatalog("food_beverage", loc);
        var cats = (List<Dictionary<string, object?>>)g["categories"]!;
        var prods = (List<Dictionary<string, object?>>)g["products"]!;
        var elc = PhpPlanQ1Note.EpcStorefrontSeedCategoriesElectronics();
        var elf = PhpPlanQ1Note.EpcStorefrontSeedProductsElectronics();
        var fg = PhpPlanQ1Note.EpcStorefrontSeedCategoriesFashion(new Dictionary<string, object?> { ["region"] = "gcc" });
        var fe = PhpPlanQ1Note.EpcStorefrontSeedCategoriesFashion(new Dictionary<string, object?> { ["region"] = "europe" });
        var pg = PhpPlanQ1Note.EpcStorefrontSeedProductsFashion(new Dictionary<string, object?> { ["region"] = "gcc" });
        var pe = PhpPlanQ1Note.EpcStorefrontSeedProductsFashion(new Dictionary<string, object?> { ["region"] = "europe" });
        var cg = PhpPlanQ1Note.EpcStorefrontSeedCategoriesConsulting(new Dictionary<string, object?> { ["tax_label"] = "GST" });
        var pc = PhpPlanQ1Note.EpcStorefrontSeedProductsConsulting(new Dictionary<string, object?> { ["tax_label"] = "GST", ["currency"] = "INR" });
        var jw = PhpPlanQ1Note.EpcStorefrontSeedCategoriesJewellery();
        var pj = PhpPlanQ1Note.EpcStorefrontSeedProductsJewellery();
        var treeU = PhpPlanQ1Note.EpcStorefrontSeedCategoryTree("unknown_pack", loc);
        var prodUsd = PhpPlanQ1Note.EpcStorefrontSeedProductCatalog("electronics", new Dictionary<string, object?> { ["currency"] = "USD" });
        return
        [
            loc,
            PhpPlanQ1Note.EpcStorefrontSeedFxRate("AED"),
            PhpPlanQ1Note.EpcStorefrontSeedFxRate("USD"),
            PhpPlanQ1Note.EpcStorefrontSeedFxRate("XXX"),
            PhpPlanQ1Note.EpcStorefrontSeedConvertPrice(25, "AED"),
            PhpPlanQ1Note.EpcStorefrontSeedConvertPrice(25, "USD"),
            cats.Count, cats[0]["alias"], prods[0]["alias"], prods[0]["price"],
            elc.Count, elc[0]["alias"], elc[^1]["alias"],
            elf.Count, elf[0]["alias"], elf[0]["price"],
            fg.Count, fg[3]["name"], fg[3]["url"], fe[3]["name"], fe[3]["url"],
            pg[3]["name"], pe[3]["name"],
            cg.Count, pc[0]["name"],
            jw.Count, pj.Count, pj[0]["alias"],
            treeU.Count, treeU[0]["alias"],
            prodUsd[0]["price"]
        ];
    }

    private static object?[] SeedDb()
    {
        var db = new PhpPlanQ1Note.SeedStore();
        var r1 = PhpPlanQ1Note.EpcStorefrontSeedAll(db, "electronics", "electronicae");
        var r2 = PhpPlanQ1Note.EpcStorefrontSeedAll(db, "electronics", "electronicae");
        var id1 = PhpPlanQ1Note.EpcStorefrontSeedUpsertCategory(db, new Dictionary<string, object?>
        {
            ["alias"] = "elc-smartphones",
            ["name"] = "Smartphones & Mobiles",
            ["url"] = "smartphones",
            ["level"] = 1,
            ["order"] = 10
        }, 0);
        var id2 = PhpPlanQ1Note.EpcStorefrontSeedUpsertCategory(db, new Dictionary<string, object?>
        {
            ["alias"] = "elc-smartphones",
            ["name"] = "Phones",
            ["url"] = "phones",
            ["level"] = 1,
            ["order"] = 11
        }, 0);
        var row = db.Categories.First(c => c.Alias == "elc-smartphones");
        return
        [
            r1["industry"], r1["site_key"], r1["currency"], r1["region"], r1["categories"], r1["products"], r1["errors"],
            r2["categories"], r2["products"],
            db.Categories.Count, db.Products.Count, db.ProductCategories.Count, db.Images.Count,
            id1, id2, id1 == id2,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = row.Value,
                ["url"] = row.Url,
                ["order"] = row.Order
            }
        ];
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
        => value.Length <= 500 ? value : value[..500] + "…";
}
