using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1BowParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Bow");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Bow_MatchPhpGolden()
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
        => Assert.Equal("content/shop/docpart/epc_product_family.php", PhpPlanQ1Bow.ProductFamilyPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Bow.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Bow.ProductFamilyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Bow.EpcPfInferProductGroup("", "Piston"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Bow.Reset();
        return name switch
        {
            "infer" => Infer(),
            "map" => Map(),
            "catalog" => Catalog(),
            "fetch" => Fetch(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Infer() => new object[]
    {
        PhpPlanQ1Bow.EpcPfInferProductGroup("", "Piston ring"),
        PhpPlanQ1Bow.EpcPfInferProductGroup("", "Oil filter kit"),
        PhpPlanQ1Bow.EpcPfInferProductGroup("", "Cabin filter"),
        PhpPlanQ1Bow.EpcPfInferProductGroup("", "Brake pad set"),
        PhpPlanQ1Bow.EpcPfInferProductGroup("", "Brake disc"),
        PhpPlanQ1Bow.EpcPfInferProductGroup("", "Wheel bearing"),
        PhpPlanQ1Bow.EpcPfInferProductGroup("", "Cylinder head"),
        PhpPlanQ1Bow.EpcPfInferProductGroup("", ""),
        PhpPlanQ1Bow.EpcPfInferProductGroup("", "Widget"),
        PhpPlanQ1Bow.EpcPfInferProductGroup("Custom group", "Piston")
    };

    private static object Map()
    {
        var mapped = PhpPlanQ1Bow.EpcPfMapStockRows(
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = " Bosch ",
                ["article"] = "OC-47",
                ["article_show"] = "OC47",
                ["name"] = "Oil filter",
                ["qty"] = "10",
                ["price"] = 12.5,
                ["warehouse"] = " S-UAE "
            },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "", ["article"] = "X", ["name"] = "Skip" },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "Febi",
                ["article"] = "",
                ["article_show"] = "",
                ["name"] = "Skip2"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "Mann", ["article"] = "HU-712", ["name"] = "Filter" }
        ]);
        var slice = PhpPlanQ1Bow.EpcPfTopBrandsSlice(
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "Zed", ["total_qty"] = 5 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "Ace", ["total_qty"] = 5 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "Big", ["total_qty"] = 20 }
        ], 2);
        var one = PhpPlanQ1Bow.EpcPfTopBrandsSlice(
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = "Solo", ["total_qty"] = 1 }
        ], 0);
        var top = (List<Dictionary<string, object?>>)slice["top"]!;
        var oneTop = (List<Dictionary<string, object?>>)one["top"]!;
        return new object[]
        {
            mapped.Count,
            mapped[0]["brand"]!,
            mapped[0]["article"]!,
            mapped[0]["article_norm"]!,
            mapped[0]["warehouse"]!,
            ToNum(mapped[0]["qty"]),
            mapped[1]["article_norm"]!,
            slice["total"]!,
            slice["more_count"]!,
            top[0]["brand"]!,
            top[1]["brand"]!,
            one["more_count"]!,
            oneTop[0]["brand"]!
        };
    }

    private static object Catalog()
    {
        var cat = PhpPlanQ1Bow.EpcPfBuildCatalogFromLines(
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "Bosch",
                ["article"] = "OC-47",
                ["article_show"] = "OC47",
                ["article_norm"] = "OC47",
                ["name"] = "Oil filter",
                ["qty"] = 10,
                ["price"] = 12.5
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "Mann",
                ["article"] = "HU712",
                ["name"] = "Oil filter kit",
                ["qty"] = 4,
                ["price"] = 8
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "Febi",
                ["article"] = "P1",
                ["name"] = "Brake pad",
                ["qty"] = 2,
                ["price"] = 4
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "Skip",
                ["article"] = "",
                ["name"] = "No art",
                ["qty"] = 1
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "X",
                ["article"] = "Z",
                ["name"] = "Widget",
                ["qty"] = 1,
                ["price"] = 1
            }
        ]);
        var products = (List<Dictionary<string, object?>>)cat["products"]!;
        var cards = PhpPlanQ1Bow.EpcPfProductsForCards(products, 1);
        var groups = (Dictionary<string, Dictionary<string, object?>>)cat["product_groups"]!;
        var found = PhpPlanQ1Bow.EpcPfFindGroup(groups, "Oil filter");
        var foundCase = PhpPlanQ1Bow.EpcPfFindGroup(groups, "OIL FILTER");
        var miss = PhpPlanQ1Bow.EpcPfFindGroup(groups, "missing");
        var detail = PhpPlanQ1Bow.EpcPfGroupDetail(found!, "bosch");
        var detailAll = PhpPlanQ1Bow.EpcPfGroupDetail(found!, "");
        var summary = (Dictionary<string, object?>)cat["summary"]!;
        return new object[]
        {
            summary["parts_count"]!,
            summary["product_groups_count"]!,
            summary["brands_count"]!,
            ToNum(summary["total_stock_qty"]),
            products.Select(p => p["label"]).ToList(),
            products.Select(p => p["parts_count"]).ToList(),
            cards[0]["brands_more_count"]!,
            cards[0]["brands_total"]!,
            cards[0]["label"]!,
            found?["label"] ?? "",
            foundCase?["label"] ?? "",
            miss is null ? 1 : 0,
            detail["parts_count"]!,
            ToNum(detail["total_qty"]),
            detailAll["parts_count"]!,
            ToNum(detailAll["total_qty"])
        };
    }

    private static object Fetch()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_bow_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            var missing = PhpPlanQ1Bow.EpcPfFetchCatalogLines(db, 200);
            Exec(db, "CREATE TABLE shop_docpart_prices_data (manufacturer VARCHAR(64), article VARCHAR(64), article_show VARCHAR(64), name VARCHAR(128), `exist` INT, price DECIMAL(10,2), storage VARCHAR(32))");
            var empty = PhpPlanQ1Bow.EpcPfFetchCatalogLines(db, 200);
            Exec(db, "INSERT INTO shop_docpart_prices_data VALUES ('Bosch','OC-47','OC47','Oil filter',10,12.5,'S-UAE'),('Bosch','OC-47','OC47','Oil filter',3,9.5,'R-UAE'),('','SKIP','','',1,1,'S-UAE'),('Mann','HU712','','',0,8,'S-UAE'),('Febi','X1','X1','Pad',2,4.2,'S-UAE'),('Skip','BIG','BIG','Huge',60001,1,'S-UAE'),('Zero','Z1','Z1','Zero',2,0,'S-UAE')");
            var rows = PhpPlanQ1Bow.EpcPfFetchCatalogLines(db, 200);
            var cfg = new object();
            var budget = 5;
            var emptyBrand = PhpPlanQ1Bow.EpcPfResolveProductGroup(cfg, db, "", "OC47", "Piston", ref budget);
            var inferred = PhpPlanQ1Bow.EpcPfResolveProductGroup(cfg, db, "Bosch", "OC-47", "Oil filter", ref budget);
            Exec(db, "INSERT INTO `epc_umapi_product_group` (`manufacturer`,`article_norm`,`product_group`,`umapi_raw`,`updated_at`) VALUES ('Bosch','OC47','CachedFam','raw',1)");
            var cached = PhpPlanQ1Bow.EpcPfResolveProductGroup(cfg, db, "Bosch", "OC-47", "Oil filter", ref budget);
            return new object[]
            {
                missing.Count,
                empty.Count,
                rows.Count,
                rows.Select(r => r["brand"]).ToList(),
                rows.Select(r => ToNum(r["qty"])).ToList(),
                rows[0]["article_norm"]!,
                emptyBrand,
                inferred,
                cached,
                budget
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
            JsonValueKind.Null => true,
            _ => true
        };
    }
}
