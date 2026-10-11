using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1CringleParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Cringle");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Cringle_MatchPhpGolden()
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
        => Assert.Equal("modules/bread_crumbs/helper.php", PhpPlanQ1Cringle.BreadCrumbsHelperPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Cringle.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Cringle.BreadCrumbsHelperPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Cringle.BreadCrumbsHelperPath, StringComparison.Ordinal);
    }

    private static object? Render(string name)
    {
        PhpPlanQ1Cringle.Reset();
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_cringle_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE bread_crumbs_rules (url VARCHAR(128), bread_crumb VARCHAR(128), bread_crumb_caption TEXT, bread_crumb_href_args TEXT)");
            Seed(db);
            return name switch
            {
                "none" => None(db),
                "text" => Text(db),
                "geturl" => GetUrl(db),
                "href" => Href(db),
                _ => throw new ArgumentOutOfRangeException(nameof(name))
            };
        }
        finally
        {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object None(MySqlConnection db)
    {
        PhpPlanQ1Cringle.QueryGet = _ => "";
        return new object?[]
        {
            PhpPlanQ1Cringle.GetAlternativeBreadCrumbs(db, "/nope", "/x"),
            PhpPlanQ1Cringle.GetAlternativeBreadCrumbs(db, "/shop/parts", "/wrong")
        };
    }

    private static object? Text(MySqlConnection db)
    {
        PhpPlanQ1Cringle.QueryGet = key => key == "lang" ? "ar" : "";
        return PhpPlanQ1Cringle.GetAlternativeBreadCrumbs(db, "/shop/parts", "/catalogue");
    }

    private static object GetUrl(MySqlConnection db)
    {
        PhpPlanQ1Cringle.QueryGet = key => key switch { "title" => "Pad <b>", "code" => "AB12", _ => "" };
        PhpPlanQ1Cringle.ConfigGet = key => key == "domain_path" ? "cdn.example" : "";
        var last = "";
        PhpPlanQ1Cringle.FetchUrl = url =>
        {
            last = url;
            return "Remote O'label";
        };
        var get = PhpPlanQ1Cringle.GetAlternativeBreadCrumbs(db, "/shop/item", "/item");
        var urlRow = PhpPlanQ1Cringle.GetAlternativeBreadCrumbs(db, "/shop/ext", "/ext");
        return new object?[] { get, urlRow, last };
    }

    private static object? Href(MySqlConnection db)
        => PhpPlanQ1Cringle.GetAlternativeBreadCrumbs(db, "/shop/empty", "/empty");

    private static void Seed(MySqlConnection db)
    {
        Insert(db, "/shop/parts", "/catalogue",
            """{"type":"text","value":"O'Brien parts"}""",
            """[{"name":"q","type":"text","value":"brake"},{"name":"lang","type":"get","value":"lang"}]""");
        Insert(db, "/shop/item", "/item", """{"type":"get","value":"title"}""", "");
        Insert(db, "/shop/ext", "/ext",
            """{"type":"url","value":"https://%0/label?x=%1&f=%2","args":[{"type":"config","value":"domain_path"},{"type":"get","value":"code"},{"type":"text","value":"fixed"}]}""",
            """[{"name":"id","type":"get","value":"code"}]""");
        Insert(db, "/shop/empty", "/empty", "", "");
    }

    private static void Insert(MySqlConnection db, string url, string crumb, string caption, string href)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO bread_crumbs_rules VALUES (@u,@b,@c,@h)";
        cmd.Parameters.AddWithValue("@u", url);
        cmd.Parameters.AddWithValue("@b", crumb);
        cmd.Parameters.AddWithValue("@c", caption);
        cmd.Parameters.AddWithValue("@h", href);
        cmd.ExecuteNonQuery();
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

file static class CringleMysql
{
    public static void Execute(this MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
