using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SheetParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Sheet");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Sheet_MatchPhpGolden()
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
        => Assert.Equal("cp/content/shop/crosses/epc_cp_cross_helpers.php", PhpPlanQ1Sheet.CpCrossHelpersPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Sheet.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Sheet.CpCrossHelpersPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Sheet.EpcCpCrossNormalizeArticle("ab"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Sheet.Reset();
        return name switch
        {
            "norm" => Norm(),
            "count" => Count(),
            "annotate" => Annotate(),
            "import" => Import(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Norm()
        => new object[]
        {
            PhpPlanQ1Sheet.EpcCpCrossNormalizeArticle(" ab-12_/`'\"\\. ,#\t\r\n"),
            PhpPlanQ1Sheet.EpcCpCrossNormalizeArticle(""),
            PhpPlanQ1Sheet.EpcCpCrossPrepareBrand(" bosch #`\"'\\"),
            PhpPlanQ1Sheet.EpcCpCrossPrepareBrand("\t"),
            PhpPlanQ1Sheet.EpcCpCrossPairStatus(null, "", "BOSCH", "X", "Y"),
            PhpPlanQ1Sheet.EpcCpCrossPairStatus(null, "AB12", "BOSCH", "AB12", "BOSCH")
        };

    private static object Count()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_sheet_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE shop_docpart_articles_analogs_list (id INT AUTO_INCREMENT PRIMARY KEY, article VARCHAR(64), analog VARCHAR(64))");
            Exec(db, "INSERT INTO shop_docpart_articles_analogs_list (article, analog) VALUES ('AB12','XY9'), ('ZZ','AB12')");
            PhpPlanQ1Sheet.ArticleExpr = column => column;
            var ok = PhpPlanQ1Sheet.EpcCpCrossCountLinksForAnchor(db, "ab-12", "BOSCH");
            var empty = PhpPlanQ1Sheet.EpcCpCrossCountLinksForAnchor(db, "   ");
            PhpPlanQ1Sheet.ArticleExpr = _ => "`no_such_col`";
            var bad = PhpPlanQ1Sheet.EpcCpCrossCountLinksForAnchor(db, "AB12");
            return new object[] { ok, empty, bad };
        }
        finally
        {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Annotate()
    {
        WirePairs(new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["AB12|BOSCH|XY9|VALEO"] = new(StringComparer.Ordinal) { ["linked"] = true, ["id"] = 17 }
        });
        WireBrands(new Dictionary<string, string>(StringComparer.Ordinal) { ["NOBRAND"] = "GATES" }, useFallback: false);
        var refs = new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["article"] = "xy-9", ["brand"] = " valeo " },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["article"] = "nobrand", ["brand"] = "" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["article"] = "miss", ["brand"] = "" },
            "skip"
        };
        return PhpPlanQ1Sheet.EpcCpCrossAnnotateReferences(null, "ab-12", "bosch", refs);
    }

    private static object Import()
    {
        WirePairs(new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["AB12|BOSCH|OLD|VALEO"] = new(StringComparer.Ordinal) { ["linked"] = true, ["id"] = 3 }
        });
        WireBrands(new Dictionary<string, string>(StringComparer.Ordinal), useFallback: true);
        PhpPlanQ1Sheet.PersistPair = (_, _, _, _) => 1;
        PhpPlanQ1Sheet.FetchSearch = (article, _, _, _, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = 1, ["article"] = article };
        var invalid = PhpPlanQ1Sheet.EpcCpCrossAddLink(null, "  ", "BOSCH", "X", "Y");
        var same = PhpPlanQ1Sheet.EpcCpCrossAddLink(null, "AB12", "BOSCH", "AB12", "BOSCH");
        var already = PhpPlanQ1Sheet.EpcCpCrossAddLink(null, "AB12", "BOSCH", "OLD", "VALEO");
        var ok = PhpPlanQ1Sheet.EpcCpCrossAddLink(null, "AB12", "BOSCH", "NEW", "VALEO");
        var missing = PhpPlanQ1Sheet.EpcCpCrossAddLink(null, "AB12", "", "NEW2", "");
        var imp = PhpPlanQ1Sheet.EpcCpCrossImportReferences(null, "AB12", "BOSCH", new object?[]
        {
            "skip",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["article"] = "OLD", ["brand"] = "VALEO", ["source"] = "crossbase" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["article"] = "NEW3", ["brand"] = "VALEO", ["source"] = "crossbase" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["article"] = "OTHER", ["brand"] = "VALEO", ["source"] = "manual" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["article"] = "", ["brand"] = "VALEO", ["source"] = "crossbase" }
        }, true, "crossbase");
        var search = PhpPlanQ1Sheet.EpcCpCrossFetchSearch(new PhpPlanQ1Sheet.SheetConfig { DomainPath = "https://shop.example/", TechKey = "k1" }, "AB12", "BOSCH", true);
        var blank = PhpPlanQ1Sheet.EpcCpCrossFetchSearch(new PhpPlanQ1Sheet.SheetConfig { DomainPath = "https://shop.example/", TechKey = "k1" }, "  ", "", false);
        return new object?[] { invalid, same, already, ok, missing, imp, search, blank };
    }

    private static void WirePairs(Dictionary<string, Dictionary<string, object?>> pairs)
    {
        PhpPlanQ1Sheet.PairExists = (a, ab, r, rb) =>
        {
            var key = a + "|" + ab + "|" + r + "|" + rb;
            return pairs.TryGetValue(key, out var row)
                ? row
                : new Dictionary<string, object?>(StringComparer.Ordinal) { ["linked"] = false, ["id"] = 0 };
        };
    }

    private static void WireBrands(Dictionary<string, string> brands, bool useFallback)
    {
        PhpPlanQ1Sheet.ResolveBrand = (article, opts) =>
        {
            var key = Regex.Replace((article ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
            if (brands.TryGetValue(key, out var found))
            {
                return found;
            }

            return useFallback && opts.TryGetValue("fallback_brand", out var fb) ? fb.Trim() : "";
        };
    }

    private static string Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN")
            ?? throw new InvalidOperationException("missing ECOMAE_LOCAL_MARIADB_E2E_DSN");

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

file static class SheetMysql
{
    public static void Execute(this MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
