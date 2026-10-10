using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1BeamParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Beam");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Beam_MatchPhpGolden()
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
        => Assert.Equal("content/shop/docpart/docpart_article_match.php", PhpPlanQ1Beam.ArticleMatchPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Beam.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Beam.ArticleMatchPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Beam.DocpartNormalizeArticleForPrice("OC-47"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Beam.Reset();
        return name switch
        {
            "norm" => Norm(),
            "clause" => Clause(),
            "collect" => Collect(),
            "chpu" => Chpu(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Norm()
    {
        var cfg = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["chpu_search_config"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["level_1"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "parts" },
                ["level_2"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mode_1"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "brands" }
                },
                ["slash_code"] = "---"
            }
        };
        return new object[]
        {
            PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(null),
            PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(""),
            PhpPlanQ1Beam.DocpartNormalizeArticleForPrice("OC-47"),
            PhpPlanQ1Beam.DocpartNormalizeArticleForPrice("filter oil_01/`.'\"#\\\t"),
            PhpPlanQ1Beam.DocpartNormalizeArticleForPrice("oc@47"),
            PhpPlanQ1Beam.DocpartSqlArticleNormalizedExpr(),
            PhpPlanQ1Beam.DocpartSqlArticleNormalizedExpr("`analog`"),
            PhpPlanQ1Beam.EpcChpuBuildPartUrl(cfg, "/en/", "Bosch", "OC-47"),
            PhpPlanQ1Beam.EpcChpuBuildPartUrl(cfg, "/en/", "", "OC-47"),
            PhpPlanQ1Beam.EpcChpuBuildPartUrl(cfg, "/en/", "JS ASAKASHI", "FILTER-OIL-01"),
            PhpPlanQ1Beam.EpcChpuBuildPartUrl(cfg, "/en/", "A/B", "X1"),
            PhpPlanQ1Beam.EpcChpuBuildPartUrl(cfg, "/en/", "Bosch", "")
        };
    }

    private static object Clause()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_beam_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            var bind = new List<object?>();
            var empty = PhpPlanQ1Beam.DocpartSqlArticleValuesMatchClause(db, ["", ""], bind);
            var noTable = PhpPlanQ1Beam.DocpartSqlArticleValuesMatchClause(db, ["OC47"], bind);
            Exec(db, "CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer VARCHAR(64), price_id INT, `exist` INT, price DECIMAL(10,2))");
            var probe = PhpPlanQ1Beam.DocpartPriceDataEnsureArticleSearchColumn(db, false) ? 1 : 0;
            var bind2 = new List<object?>();
            var afterProbe = PhpPlanQ1Beam.DocpartSqlArticleValuesMatchClause(db, ["OC47"], bind2);
            var hasAnalogs = PhpPlanQ1Beam.DocpartAnalogsHasSearchColumns(db) ? 1 : 0;
            var (art, analog) = PhpPlanQ1Beam.DocpartAnalogsMatchExprs(db);
            return new object[]
            {
                empty,
                noTable,
                bind.Count,
                probe,
                afterProbe,
                bind2.Count,
                hasAnalogs,
                art == "`article_search`" ? 1 : 0,
                analog.Length >= 6 ? analog[..6] : analog
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Collect()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_beam_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE shop_docpart_articles_analogs_list (article VARCHAR(64), analog VARCHAR(64), manufacturer_article VARCHAR(64), manufacturer_analog VARCHAR(64))");
            Exec(db, "INSERT INTO shop_docpart_articles_analogs_list VALUES ('OC-47','OC47X','Bosch','Mann'),('ZZ','OC-47','Skip','Febi')");
            Exec(db, "CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer VARCHAR(64), price_id INT, `exist` INT, price DECIMAL(10,2))");
            Exec(db, "INSERT INTO shop_docpart_prices_data (article, manufacturer, price_id, `exist`, price) VALUES ('OC-47','Bosch',1,2,12.5)");
            Exec(db, "CREATE TABLE shop_storages (id INT PRIMARY KEY, connection_options TEXT)");
            Exec(db, "INSERT INTO shop_storages VALUES (8, '{\"price_id\":1}'),(9, '{\"price_id\":2}')");
            var none = PhpPlanQ1Beam.DocpartCollectArticleCandidates(db, "", true);
            var direct = PhpPlanQ1Beam.DocpartCollectArticleCandidates(db, "OC-47", false);
            var cross = PhpPlanQ1Beam.DocpartCollectArticleCandidates(db, "OC-47", true);
            var cfg = new Dictionary<string, object?>(StringComparer.Ordinal) { ["local_crosses"] = 1 };
            var resolvedHit = PhpPlanQ1Beam.DocpartResolveArticleSearchValues(db, cfg, "OC-47", [1]);
            var cfg0 = new Dictionary<string, object?>(StringComparer.Ordinal) { ["local_crosses"] = 0 };
            var resolvedOff = PhpPlanQ1Beam.DocpartResolveArticleSearchValues(db, cfg0, "OC-47", [1]);
            var ids = PhpPlanQ1Beam.DocpartPriceIdsFromOfficeStorageBunches(db,
            [
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 8 },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 8 },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 9 },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 0 }
            ]);
            var emptyIds = PhpPlanQ1Beam.DocpartPriceIdsFromOfficeStorageBunches(db, []);
            return new object[] { none, direct, cross, resolvedHit, resolvedOff, ids, emptyIds };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Chpu()
    {
        PhpPlanQ1Beam.ArticleBrandsFromUmapi = (_, _, brands, seen) =>
        {
            brands["BOSCH"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["manufacturer"] = "Bosch" };
            seen["BOSCH"] = true;
            return 1;
        };
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_beam_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE shop_docpart_prices (id INT PRIMARY KEY)");
            Exec(db, "INSERT INTO shop_docpart_prices VALUES (1)");
            Exec(db, "CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer VARCHAR(64), price_id INT, `exist` INT, price DECIMAL(10,2))");
            Exec(db, "INSERT INTO shop_docpart_prices_data (article, manufacturer, price_id, `exist`, price) VALUES ('OC-47','Bosch',1,2,12.5),('OC-47','MANN',1,1,8)");
            var cfg = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["local_crosses"] = 0,
                ["chpu_search_config"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["chpu_search_on"] = 1,
                    ["level_1"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "parts" },
                    ["level_2"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["mode_1"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "brands" }
                    },
                    ["slash_code"] = "---"
                }
            };
            var two = PhpPlanQ1Beam.EpcChpuDistinctWarehouseBrandsForArticle(db, cfg, "OC-47", [1]);
            var single = PhpPlanQ1Beam.EpcChpuResolveSingleBrandForArticle(db, cfg, "OC-47");
            var off = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["chpu_search_config"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["chpu_search_on"] = 0 }
            };
            var redirOff = PhpPlanQ1Beam.EpcChpuSingleBrandRedirectUrl(db, off, "OC-47", "/en");
            Exec(db, "DELETE FROM shop_docpart_prices_data WHERE manufacturer='MANN'");
            var one = PhpPlanQ1Beam.EpcChpuDistinctWarehouseBrandsForArticle(db, cfg, "OC-47", [1]);
            var resolved = PhpPlanQ1Beam.EpcChpuResolveSingleBrandForArticle(db, cfg, "OC-47");
            var redir = PhpPlanQ1Beam.EpcChpuSingleBrandRedirectUrl(db, cfg, "OC-47", "/en");
            Exec(db, "DELETE FROM shop_docpart_prices_data");
            var emptyWh = PhpPlanQ1Beam.EpcChpuDistinctWarehouseBrandsForArticle(db, cfg, "OC-47", [1]);
            var fromUmapi = PhpPlanQ1Beam.EpcChpuResolveSingleBrandForArticle(db, cfg, "OC-47");
            var umapiOnly = PhpPlanQ1Beam.EpcChpuSingleBrandFromUmapi(cfg, "OC-47");
            return new object[]
            {
                two,
                single is null ? 1 : 0,
                redirOff,
                one,
                resolved ?? "",
                redir,
                emptyWh,
                fromUmapi ?? "",
                umapiOnly ?? ""
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
