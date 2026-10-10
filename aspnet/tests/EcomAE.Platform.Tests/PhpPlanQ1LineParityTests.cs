using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1LineParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Line");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Line_MatchPhpGolden()
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
        => Assert.Equal("content/shop/docpart/docpart_epc_article_brands.php", PhpPlanQ1Line.ArticleBrandsPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Line.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Line.ArticleBrandsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Line.EpcArticleBrandsUmapiKey(new Dictionary<string, object?> { ["umapi_api_key"] = "k" }), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Line.Reset();
        return name switch
        {
            "empty" => Empty(),
            "addkey" => AddKey(),
            "crosshtml" => CrossHtml(),
            "collect" => Collect(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        using var db = OpenAdmin();
        var cfg = new Dictionary<string, object?> { ["local_crosses"] = 1 };
        var a = PhpPlanQ1Line.EpcCollectArticleCatalogBrands(db, cfg, "");
        var b = PhpPlanQ1Line.EpcCollectArticleCatalogBrands(db, cfg, "---");
        return new object[] { true.Equals(a["status"]) ? 1 : 0, a["message"]!, true.Equals(b["status"]) ? 1 : 0, b["message"]! };
    }

    private static object AddKey()
    {
        var brands = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        var shortAdd = PhpPlanQ1Line.EpcArticleBrandsAdd(brands, seen, "X", "", "t") ? 1 : 0;
        var ok = PhpPlanQ1Line.EpcArticleBrandsAdd(brands, seen, "Bosch", "Pad", "umapi") ? 1 : 0;
        var dup = PhpPlanQ1Line.EpcArticleBrandsAdd(brands, seen, "BOSCH", "Better", "warehouse") ? 1 : 0;
        return new object[]
        {
            shortAdd,
            ok,
            dup,
            brands["BOSCH"]["name"]!,
            brands["BOSCH"]["sources"]!,
            PhpPlanQ1Line.EpcArticleBrandsUmapiKey(new Dictionary<string, object?> { ["umapi_api_key"] = "", ["umapi_api_url"] = "https://api.umapi.ru/v1/KEY99/" }),
            PhpPlanQ1Line.EpcArticleBrandsUmapiKey(new Dictionary<string, object?> { ["umapi_api_key"] = "  abc  " })
        };
    }

    private static object CrossHtml()
    {
        var brand = PhpPlanQ1Line.EpcArticleBrandsCrossbaseBrand("OC47", "Bosch OC47");
        var brand2 = PhpPlanQ1Line.EpcArticleBrandsCrossbaseBrand("OC47", "Mann <b>OC47</b>");
        PhpPlanQ1Line.CacheRead = _ => "<tr><td>1</td><td><a href=\"/cross/?q=OC47\">Bosch OC47</a></td></tr><a href=\"/cross/?q=OC90\">Skip OC90</a>";
        var brands = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        var n = PhpPlanQ1Line.EpcArticleBrandsFromCrossbase("OC-47", "OC47", brands, seen);
        var id = brands.Keys.FirstOrDefault() ?? "";
        return new object[] { brand, brand2, n, id, brands.TryGetValue(id, out var row) ? row["sources"]! : Array.Empty<string>() };
    }

    private static object Collect()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_line_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE shop_docpart_articles_analogs_list (article VARCHAR(64), manufacturer_article VARCHAR(64), analog VARCHAR(64), manufacturer_analog VARCHAR(64))");
            Exec(db, "INSERT INTO shop_docpart_articles_analogs_list VALUES ('OC47','Bosch','OC47X','Mann'),('ZZ','Skip','OC47','Febi')");
            PhpPlanQ1Line.WarehouseBrands = (_, _, _) => ["ACME", "B"];
            PhpPlanQ1Line.CanonicalMap = _ => new Dictionary<string, string>(StringComparer.Ordinal) { ["Bosch"] = "BOSCH", ["Mann"] = "MANN" };
            PhpPlanQ1Line.SynonymCanonical = (b, map) => map.TryGetValue(b, out var c) ? c : "";
            PhpPlanQ1Line.CacheRead = _ => "";
            PhpPlanQ1Line.HttpGet = (_, _) => "";
            var cfg = new Dictionary<string, object?> { ["local_crosses"] = 1, ["umapi_api_key"] = "" };
            var row = PhpPlanQ1Line.EpcCollectArticleCatalogBrands(db, cfg, "OC-47");
            var shows = ((List<Dictionary<string, object?>>)row["manufacturers"]!).Select(m => m["manufacturer_show"]).ToList();
            var first = ((List<Dictionary<string, object?>>)row["manufacturers"]!)[0];
            var type = Convert.ToString(((Dictionary<string, object?>)first["params"]!)["type"]) ?? "";
            return new object[]
            {
                true.Equals(row["status"]) ? 1 : 0,
                row["article"]!,
                row["warehouse_count"]!,
                row["cp_crosses_count"]!,
                row["crossbase_count"]!,
                row["umapi_count"]!,
                shows,
                type
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
            _ => true
        };
    }
}
