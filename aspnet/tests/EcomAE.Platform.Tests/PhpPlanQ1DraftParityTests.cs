using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1DraftParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Draft");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Draft_MatchPhpGolden()
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
        => Assert.Equal("content/shop/docpart/docpart_cross_interchange.php", PhpPlanQ1Draft.CrossInterchangePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Draft.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Draft.CrossInterchangePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Draft.DocpartCrossPrepareBrandName("Bosch"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Draft.Reset();
        return name switch
        {
            "names" => Names(),
            "partners" => Partners(),
            "refs" => Refs(),
            "persist" => Persist(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Names() => new object?[]
    {
        PhpPlanQ1Draft.DocpartPriceNameClusterCore(""),
        PhpPlanQ1Draft.DocpartPriceNameClusterCore("ab"),
        PhpPlanQ1Draft.DocpartPriceNameClusterCore("FILTER"),
        PhpPlanQ1Draft.DocpartPriceNameClusterCore("OIL FILTER 90915-YZZD1"),
        PhpPlanQ1Draft.DocpartPriceNameClusterCore("AIR FILTER"),
        PhpPlanQ1Draft.DocpartPriceNameClusterCore("SPARK PLUG X"),
        PhpPlanQ1Draft.DocpartPriceNameClusterCore("WIDGET 12345"),
        PhpPlanQ1Draft.DocpartPriceNameClusterCore("fuel  filter"),
        PhpPlanQ1Draft.DocpartCrossPrepareBrandName(" Toyota "),
        PhpPlanQ1Draft.DocpartCrossPrepareBrandName("bosch#\n"),
        PhpPlanQ1Draft.DocpartCrossPrepareBrandName("a`b'\""),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("90915YZZD1"),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("15400AA"),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("15208X"),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("26300A"),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("06A123"),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("A000123456"),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("B6Y1X"),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("12279A"),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("46256X"),
        PhpPlanQ1Draft.DocpartCrossInferBrandFromArticleNorm("OC47"),
        PhpPlanQ1Draft.DocpartAnalogsHostLoad1()
    };

    private static object Partners()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_draft_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            var empty = PhpPlanQ1Draft.DocpartLoadInterchangePartners(db, "");
            var missing = PhpPlanQ1Draft.DocpartLoadInterchangePartners(db, "OC-47");
            Exec(db, "CREATE TABLE shop_docpart_articles_analogs_list (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer_article VARCHAR(64), analog VARCHAR(64), manufacturer_analog VARCHAR(64))");
            Exec(db, "INSERT INTO shop_docpart_articles_analogs_list (article, manufacturer_article, analog, manufacturer_analog) VALUES ('OC-47','Bosch','OC47X','Mann'),('HU712','Febi','OC47X','Mann')");
            var rows = PhpPlanQ1Draft.DocpartLoadInterchangePartners(db, "OC-47", 6, 5000);
            var capped = PhpPlanQ1Draft.DocpartLoadInterchangePartners(db, "OC-47", 0, 10);
            return new object[]
            {
                empty.Count,
                missing.Count,
                rows.Count,
                rows.Select(r => r["brand"]).ToList(),
                rows.Select(r => r["article_norm"]).ToList(),
                capped.Count
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Refs()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_draft_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), article_show VARCHAR(64), manufacturer VARCHAR(64), name VARCHAR(128), `exist` INT, price DECIMAL(10,2))");
            Exec(db, "INSERT INTO shop_docpart_prices_data (article, article_show, manufacturer, name, `exist`, price) VALUES ('OC47A','OC47A','Bosch','OIL FILTER 90915YZZD1',2,12.5),('HU712','','Mann','OIL FILTER KIT',1,8),('HU816','HU-816','Wix','Fits OC47A Toyota',3,9),('SKIP','','X','WIDGET',1,1),('Z0','','Y','OIL FILTER ZERO',0,4)");
            var cluster = PhpPlanQ1Draft.DocpartCrossRefsFromPriceNameCluster(db, "OC47A");
            var oem = PhpPlanQ1Draft.DocpartCrossRefsFromStockOemMention(db, "OC47A");
            var shortOem = PhpPlanQ1Draft.DocpartCrossRefsFromStockOemMention(db, "AB");
            return new object[]
            {
                cluster.Count,
                cluster.Select(r => r["article"]).ToList(),
                cluster.Count > 0 ? cluster[0]["brand"] ?? "" : "",
                oem.Count,
                oem.Select(r => r["article"]).ToList(),
                shortOem.Count
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Persist()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_draft_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer VARCHAR(64), `exist` INT, price DECIMAL(10,2))");
            Exec(db, "INSERT INTO shop_docpart_prices_data (article, manufacturer, `exist`, price) VALUES ('OC-47','Bosch',2,12.5)");
            Exec(db, "CREATE TABLE shop_docpart_articles_analogs_list (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer_article VARCHAR(64), analog VARCHAR(64), manufacturer_analog VARCHAR(64))");
            Exec(db, "INSERT INTO shop_docpart_articles_analogs_list (article, manufacturer_article, analog, manufacturer_analog) VALUES ('HU712','Febi','OC47X','Mann'),('OC-47','','90915YZZD1',''),('ZZ','','YY','')");
            var fallback = PhpPlanQ1Draft.DocpartCrossResolveBrandForArticle(db, "ZZ", new Dictionary<string, string>(StringComparer.Ordinal) { ["fallback_brand"] = " acme " });
            var fromPrice = PhpPlanQ1Draft.DocpartCrossResolveBrandForArticle(db, "OC-47");
            var fromCross = PhpPlanQ1Draft.DocpartCrossResolveBrandForArticle(db, "HU712");
            var fromInfer = PhpPlanQ1Draft.DocpartCrossResolveBrandForArticle(db, "90915YZZD1");
            var same = PhpPlanQ1Draft.DocpartCrossPairExistsWithBrands(db, "OC47", "Bosch", "OC47", "Bosch");
            var ok = PhpPlanQ1Draft.DocpartCrossPersistInterchangePair(db, "OC-47", "Bosch", "OC47X", "Mann") ? 1 : 0;
            var dup = PhpPlanQ1Draft.DocpartCrossPersistInterchangePair(db, "OC-47", "Bosch", "OC47X", "Mann") ? 1 : 0;
            var exists = PhpPlanQ1Draft.DocpartCrossPairExistsWithBrands(db, "OC-47", "Bosch", "OC47X", "Mann");
            var bi = PhpPlanQ1Draft.DocpartCrossPersistInterchangePairBidirectional(db, "P1", "Febi", "P2", "Valeo");
            var repair = PhpPlanQ1Draft.DocpartCrossRepairEmptyManufacturers(db, "OC-47", 50);
            return new object[]
            {
                fallback,
                fromPrice,
                fromCross,
                fromInfer,
                true.Equals(same["linked"]) ? 1 : 0,
                ok,
                dup,
                true.Equals(exists["linked"]) ? 1 : 0,
                Convert.ToInt32(exists["id"], System.Globalization.CultureInfo.InvariantCulture) > 0 ? 1 : 0,
                bi,
                repair["updated"]!,
                repair["skipped"]!,
                repair["error"]!
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
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
