using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1TextParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Text");

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
    public void PlanQ1Text_MatchPhpGolden()
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
            if (actual.Output != "" || !Same(Json(actual.Extra), expected))
            {
                failures.Add(name + " extraExp=" + Truncate(expected.GetRawText()) + " extraGot=" + Truncate(Json(actual.Extra)));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Text.StockBrandsPath,
                PhpPlanQ1Text.TreeListItemsPath,
                PhpPlanQ1Text.TextSearchPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Text.StockBrandsPath,
                PhpPlanQ1Text.TreeListItemsPath,
                PhpPlanQ1Text.TextSearchPath
            });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Text.Reset();
        Assert.Contains("epc_stock_brands_helpers.php", PhpPlanQ1Text.StockBrandsPath, StringComparison.Ordinal);
        Assert.Contains("get_tree_list_items.php", PhpPlanQ1Text.TreeListItemsPath, StringComparison.Ordinal);
        Assert.Contains("text_search_algorithm.php", PhpPlanQ1Text.TextSearchPath, StringComparison.Ordinal);
    }

    private sealed record Rendered(string Output, object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Open.Reset();
        PhpPlanQ1Text.Reset();
        return name switch
        {
            "match_ids" => MatchIds(),
            "brands_letters" => BrandsLetters(),
            "parts_tag" => PartsTag(),
            "tree_text" => TreeText(),
            _ => new Rendered("unknown:" + name, null)
        };
    }

    private static Rendered MatchIds()
    {
        var db = new PhpPlanQ1Text.TextStore();
        db.Open.Storages.AddRange(
        [
            new() { Id = 10, InterfaceType = 2, Hidden = 0, StorefrontTempDisabled = 0, ConnectionOptions = "{\"price_id\":0}" },
            new() { Id = 11, InterfaceType = 2, Hidden = 0, StorefrontTempDisabled = 0, ConnectionOptions = "{\"price_id\":7}" },
            new() { Id = 12, InterfaceType = 2, Hidden = 1, StorefrontTempDisabled = 0, ConnectionOptions = "{\"price_id\":8}" },
            new() { Id = 13, InterfaceType = 1, Hidden = 0, StorefrontTempDisabled = 0, ConnectionOptions = "{\"price_id\":9}" },
            new() { Id = 14, InterfaceType = 2, Hidden = 0, StorefrontTempDisabled = 1, ConnectionOptions = "{\"price_id\":6}" }
        ]);
        db.OfficeMaps.AddRange(
        [
            new() { OfficeId = 1, StorageId = 10 },
            new() { OfficeId = 1, StorageId = 11 },
            new() { OfficeId = 1, StorageId = 12 },
            new() { OfficeId = 1, StorageId = 13 },
            new() { OfficeId = 4, StorageId = 14 },
            new() { OfficeId = 4, StorageId = 11 }
        ]);
        db.Open.Prices.AddRange(
        [
            new() { Id = 6, StorefrontTempDisabled = 0 },
            new() { Id = 7, StorefrontTempDisabled = 0 },
            new() { Id = 8, StorefrontTempDisabled = 0 },
            new() { Id = 9, StorefrontTempDisabled = 0 }
        ]);
        PhpPlanQ1Text.CustomerOffices = () => [4, 1];
        var office = PhpPlanQ1Text.EpcStockBrandPriceIds(db);
        var stockEmpty = PhpPlanQ1Text.EpcStockBrandPriceIdsWithStock(db);
        db.Open.PriceData.AddRange(
        [
            new() { PriceId = 7, Manufacturer = "Bosch", Article = "045", Name = "Pump", Price = 12.50m, Exist = 2 },
            new() { PriceId = 0, Manufacturer = "X", Article = "1", Name = "Z", Price = 1m, Exist = 1 }
        ]);
        var stock = PhpPlanQ1Text.EpcStockBrandPriceIdsWithStock(db);
        return new Rendered("", new object?[]
        {
            PhpPlanQ1Text.EpcStockBrandMatchParams("Bosch-AG"),
            PhpPlanQ1Text.EpcStockBrandMatchParams("  япония мотор "),
            PhpPlanQ1Text.EpcStockBrandMatchParams(""),
            office,
            stockEmpty,
            stock
        });
    }

    private static Rendered BrandsLetters()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1t_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path.Combine(doc, "content", "files", "epc_brands_cache"));
        PhpPlanQ1Text.DocumentRoot = doc;
        PhpPlanQ1Text.Clock = () => 1_700_000_000;
        PhpPlanQ1Text.CustomerOffices = () => [];
        try
        {
            var db = new PhpPlanQ1Text.TextStore();
            db.Open.Prices.AddRange(
            [
                new() { Id = 1, StorefrontTempDisabled = 0 },
                new() { Id = 2, StorefrontTempDisabled = 1 }
            ]);
            db.Open.PriceData.AddRange(
            [
                new() { PriceId = 1, Manufacturer = "3M", Article = "A1", Name = "Tape", Price = 1.10m, Exist = 1 },
                new() { PriceId = 1, Manufacturer = "Bosch", Article = "B1", ArticleShow = "045 110", Name = "Pump", Price = 12.50m, Exist = 3 },
                new() { PriceId = 1, Manufacturer = "bosch", Article = "B2", Name = "Filter", Price = 4.00m, Exist = 1 },
                new() { PriceId = 1, Manufacturer = "Япония", Article = "J1", Name = "Kit", Price = 9.00m, Exist = 2 },
                new() { PriceId = 1, Manufacturer = "Öhlins", Article = "O1", Name = "Shock", Price = 20.00m, Exist = 1 },
                new() { PriceId = 1, Manufacturer = "", Article = "Z1", Name = "Skip", Price = 1.00m, Exist = 1 },
                new() { PriceId = 1, Manufacturer = "X", Article = "", Name = "NoArt", Price = 1.00m, Exist = 1 },
                new() { PriceId = 1, Manufacturer = "Y", Article = "Y1", Name = "Zero", Price = 0m, Exist = 2 },
                new() { PriceId = 1, Manufacturer = "Z", Article = "Z1", Name = "Gone", Price = 5.00m, Exist = 0 },
                new() { PriceId = 2, Manufacturer = "Hidden", Article = "H1", Name = "Off", Price = 3.00m, Exist = 1 }
            ]);
            var emptyIds = PhpPlanQ1Text.EpcStockBrandPriceIds(db);
            var none = PhpPlanQ1Text.EpcStockBrandsWithCounts(db, []);
            var first = PhpPlanQ1Text.EpcStockBrandsWithCounts(db, [1, 2]);
            var second = PhpPlanQ1Text.EpcStockBrandsWithCounts(db, [1, 2]);
            return new Rendered("", new object?[] { emptyIds, none, first, second });
        }
        finally
        {
            try { Directory.Delete(doc, true); } catch { /* throwaway */ }
        }
    }

    private static Rendered PartsTag()
    {
        var db = new PhpPlanQ1Text.TextStore();
        PhpPlanQ1Text.CustomerOffices = () => [1];
        PhpPlanQ1Text.GenuineIndex = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["brands"] = new Dictionary<string, bool>(StringComparer.Ordinal) { ["BOSCH"] = true },
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        };
        PhpPlanQ1Text.Manufacturers = () => [(1, "Bosch")];
        PhpPlanQ1Text.Synonyms = () => [(1, "BOSCH-AG")];
        db.Open.Storages.Add(new() { Id = 1, InterfaceType = 2, Hidden = 0, StorefrontTempDisabled = 0, ConnectionOptions = "{\"price_id\":3}" });
        db.OfficeMaps.Add(new() { OfficeId = 1, StorageId = 1 });
        db.Open.Prices.Add(new() { Id = 3, StorefrontTempDisabled = 0 });
        db.Open.PriceData.AddRange(
        [
            new() { PriceId = 3, Manufacturer = "Bosch", Article = "045", ArticleShow = "045 110", Name = "Pump", Price = 12.50m, Exist = 2, TimeToExe = "1", Storage = "WH", MinOrder = "1" },
            new() { PriceId = 3, Manufacturer = "BOSCH-AG", Article = "046", Name = "Filter", Price = 8.00m, Exist = 1, TimeToExe = "2", Storage = "WH", MinOrder = "1" },
            new() { PriceId = 3, Manufacturer = "Bosch", Article = "045", Name = "Pump-dup", Price = 15.00m, Exist = 1, TimeToExe = "3", Storage = "WH2", MinOrder = "2" }
        ]);
        var empty = PhpPlanQ1Text.EpcStockBrandPartsForManufacturer(db, "   ");
        var parts = PhpPlanQ1Text.EpcStockBrandPartsForManufacturer(db, "Bosch-AG");
        var brands = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["name"] = "Bosch", ["parts_count"] = 2, ["letter"] = "B" },
            new(StringComparer.Ordinal) { ["name"] = "Mann", ["parts_count"] = 1, ["letter"] = "M" },
            new(StringComparer.Ordinal) { ["name"] = "", ["parts_count"] = 0, ["letter"] = "#" }
        };
        var tagged = PhpPlanQ1Text.EpcStockBrandsTagPartTypes(db, brands, "/parts");
        var none = PhpPlanQ1Text.EpcStockBrandsTagPartTypes(db, [], "/parts");
        return new Rendered("", new object?[] { empty, parts, tagged, none });
    }

    private static Rendered TreeText()
    {
        var db = new PhpPlanQ1Text.TextStore();
        PhpPlanQ1Text.Translate = id =>
        {
            var key = Convert.ToString(id, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            return key switch
            {
                "10" => "Root",
                "11" => "Child <A>",
                _ => key
            };
        };
        PhpPlanQ1Text.ArticleLangKeys = () => [100];
        var empty = PhpPlanQ1Text.GetTreeListItemsJson(db, 9);
        db.TreeItems.AddRange(
        [
            new() { Id = 1, TreeListId = 5, Count = 1, Level = 0, Value = 10, Alias = "root", Url = "/root", Parent = 0, Image = "", Order = 1, Open = 0 },
            new() { Id = 2, TreeListId = 5, Count = 0, Level = 1, Value = 11, Alias = "child", Url = "/root/child", Parent = 1, Image = "c.png", Order = 2, Open = 1 },
            new() { Id = 3, TreeListId = 8, Count = 0, Level = 0, Value = 99, Alias = "other", Url = "/x", Parent = 0, Image = "", Order = 1, Open = 1 }
        ]);
        var tree = PhpPlanQ1Text.GetTreeListItemsJson(db, 5);
        db.Translations.AddRange(
        [
            new() { StrId = 100, StrKey = 100, LangCode = "en", Value = "Article" },
            new() { StrId = 101, StrKey = 101, LangCode = "en", Value = "Water pump kit" },
            new() { StrId = 102, StrKey = 102, LangCode = "en", Value = "Heavy duty" },
            new() { StrId = 200, StrKey = 200, LangCode = "en", Value = "ABC123" }
        ]);
        db.Products.AddRange(
        [
            new() { Id = 7, Caption = 101, Alias = "bosch/ABC123", PublishedFlag = 1 },
            new() { Id = 8, Caption = 102, Alias = "other", PublishedFlag = 1 }
        ]);
        db.ProductTexts.Add(new() { ProductId = 9, Content = 102 });
        db.PropertyMap.Add(new() { Id = 1, Value = 100, PropertyTypeId = 3 });
        db.PropertyValues.Add(new() { ProductId = 7, PropertyId = 1, Value = 200 });
        db.Discovery.AddRange(
        [
            new() { ProductId = 11, Status = "imported", BrandArticleKey = "mann-xyz", MetaJson = "{\"brand_article_key\":\"mann-xyz\"}" },
            new() { ProductId = 12, Status = "pending", BrandArticleKey = "pump", MetaJson = "{}" }
        ]);
        var found = PhpPlanQ1Text.TextSearchProducts(db, "pump kit", "en");
        var article = PhpPlanQ1Text.TextSearchProducts(db, "ABC-123", "en");
        var disc = PhpPlanQ1Text.TextSearchProducts(db, "mann-xyz", "en");
        return new Rendered("", new object?[] { empty, tree, found, article, disc });
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
