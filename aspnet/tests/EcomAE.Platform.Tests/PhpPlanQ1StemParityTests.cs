using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1StemParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Stem");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Stem_MatchPhpGolden()
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
                failures.Add(name + " exp=" + expected.GetRawText() + " got=" + Json(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/shop/docpart/epc_accessories_catalog.php", PhpPlanQ1Stem.AccessoriesCatalogPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Stem.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Stem.AccessoriesCatalogPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Stem.EpcAccCachePath(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Stem.Reset();
        Seed();
        return name switch
        {
            "path" => PathCase(),
            "fetch" => Fetch(),
            "build" => Build(),
            "search" => Search(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static void Seed()
    {
        PhpPlanQ1Stem.NormalizeArticle = value => System.Text.RegularExpressions.Regex.Replace((value ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
        PhpPlanQ1Stem.Classify = (name, _) =>
            name.Contains("pad", StringComparison.OrdinalIgnoreCase)
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["category"] = "brakes",
                    ["subcategory"] = "pads",
                    ["category_label"] = "Brakes",
                    ["subcategory_label"] = "Brake Pads"
                }
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["category"] = "other",
                    ["subcategory"] = "general",
                    ["category_label"] = "Other Parts",
                    ["subcategory_label"] = "General"
                };
        PhpPlanQ1Stem.WarehouseRegions = () => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["S-UAE"] = "Dubai / Sharjah stock",
            ["R-UAE"] = "Ras Al Khaimah stock"
        };
        PhpPlanQ1Stem.Taxonomy = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["brakes"] = new(StringComparer.Ordinal)
            {
                ["label"] = "Brakes",
                ["icon"] = "fa-stop",
                ["subs"] = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
                {
                    ["pads"] = new(StringComparer.Ordinal) { ["label"] = "Brake Pads" }
                }
            },
            ["other"] = new(StringComparer.Ordinal)
            {
                ["label"] = "Other Parts",
                ["icon"] = "fa-tag",
                ["subs"] = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
                {
                    ["general"] = new(StringComparer.Ordinal) { ["label"] = "General" }
                }
            }
        };
    }

    private static object PathCase()
    {
        PhpPlanQ1Stem.DocumentRoot = "/shop/acme";
        var p = PhpPlanQ1Stem.EpcAccCachePath();
        return new object[] { Path.GetFileName(p), "097854dd", p.Contains("epc_acc_catalog_v1_", StringComparison.Ordinal) ? 1 : 0 };
    }

    private static object Fetch()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_stem_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            var empty = PhpPlanQ1Stem.EpcAccFetchRawRows(db, 200);
            Exec(db, "CREATE TABLE shop_docpart_prices_data (manufacturer VARCHAR(64), article VARCHAR(64), article_show VARCHAR(64), name VARCHAR(128), `exist` INT, price DECIMAL(10,2), storage VARCHAR(32), price_id INT)");
            Exec(db, "INSERT INTO shop_docpart_prices_data VALUES ('Bosch','OC-47','OC47','Oil filter',10,12.5,'S-UAE',1),('Bosch','OC-47','OC47','Oil filter',3,9.5,'R-UAE',1),('','SKIP','','',1,1,'S-UAE',2),('Mann','HU712','','',0,8,'S-UAE',3),('Febi','X1','X1','Pad',2,4.2,'S-UAE',4)");
            var rows = PhpPlanQ1Stem.EpcAccFetchRawRows(db, 200);
            return new object[] { empty.Count, rows.Count, rows.Select(r => r["brand"]).ToList(), ToNum(rows[0]["qty"]) };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Build()
    {
        var items = PhpPlanQ1Stem.EpcAccBuildItems(
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "Bosch", ["article"] = "OC-47", ["article_show"] = "OC47", ["name"] = "", ["qty"] = 10, ["price"] = 12.5, ["warehouse"] = "S-UAE" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "Bosch", ["article"] = "OC-47", ["article_show"] = "OC47", ["name"] = "Oil filter", ["qty"] = 3, ["price"] = 9.5, ["warehouse"] = "" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "X", ["article"] = "Z", ["name"] = "Pad", ["qty"] = 1, ["price"] = 1, ["warehouse"] = "S-UAE" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "Skip", ["article"] = "Z", ["name"] = "Big", ["qty"] = 60000, ["price"] = 1, ["warehouse"] = "S-UAE" }
        ]);
        var first = items[0];
        return new object[]
        {
            items.Count,
            first["brand"]!,
            first["article_norm"]!,
            first["name"]!,
            ToNum(first["qty"]),
            ToNum(first["price"]),
            first["region"]!,
            first["category"]!,
            items[1]["category"]!
        };
    }

    private static object Search()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_stem_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE shop_docpart_prices_data (manufacturer VARCHAR(64), article VARCHAR(64), article_show VARCHAR(64), name VARCHAR(128), `exist` INT, price DECIMAL(10,2), storage VARCHAR(32), price_id INT)");
            Exec(db, "INSERT INTO shop_docpart_prices_data VALUES ('Bosch','OC47','OC47','Oil filter',5,20,'S-UAE',1),('Febi','P1','P1','Pad set',2,8,'R-UAE',2),('Febi','P2','P2','Pad',1,12,'S-UAE',3)");
            var hit = PhpPlanQ1Stem.EpcAccSearch(db, new Dictionary<string, object?> { ["refresh"] = 1, ["brand"] = "febi", ["sort"] = "price-asc", ["per_page"] = 12 });
            var q = PhpPlanQ1Stem.EpcAccSearch(db, new Dictionary<string, object?> { ["refresh"] = 1, ["q"] = "OC-47", ["sort"] = "price-desc" });
            var page = PhpPlanQ1Stem.EpcAccSearch(db, new Dictionary<string, object?> { ["refresh"] = 1, ["page"] = 9, ["per_page"] = 12 });
            var items = (List<Dictionary<string, object?>>)hit["items"]!;
            var qItems = (List<Dictionary<string, object?>>)q["items"]!;
            var brands = ((List<Dictionary<string, object?>>)((Dictionary<string, object?>)hit["facets"]!)["brands"]!).Select(b => b["brand"]).ToList();
            var tax0 = ((List<Dictionary<string, object?>>)hit["taxonomy"]!)[0]["slug"]!;
            return new object[]
            {
                hit["total"]!,
                items[0]["article"]!,
                ToNum(items[0]["price"]),
                q["total"]!,
                qItems[0]["brand"]!,
                page["page"]!,
                page["from"]!,
                page["to"]!,
                brands,
                tax0,
                hit["sort"]!
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object ToNum(object? value)
    {
        var d = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        return Math.Abs(d - Math.Round(d)) < 0.0000001 ? (object)(int)Math.Round(d) : d;
    }

    private static string Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? throw new InvalidOperationException("missing ECOMAE_LOCAL_MARIADB_E2E_DSN");

    private static MySqlConnection OpenAdmin()
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static MySqlConnection OpenDb(string schema)
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;Database={schema};User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static void Exec(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
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
            _ => true
        };
    }
}
