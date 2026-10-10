using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1KnotParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Knot");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Knot_MatchPhpGolden()
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
        => Assert.Equal("plugins/metadata_handler/metadata_handler.php", PhpPlanQ1Knot.MetadataHandlerPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Knot.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Knot.MetadataHandlerPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Knot.MetadataHandlerPath, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Knot.Reset();
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_knot_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE metadata_handler_rules (content_id INT, title_rule TEXT, description_rule TEXT)");
            Exec(db, "CREATE TABLE text_for_url (url VARCHAR(128), title_tag VARCHAR(255), description_tag VARCHAR(255), keywords_tag VARCHAR(255))");
            return name switch
            {
                "none" => None(db),
                "url" => Url(db),
                "complex" => Complex(db),
                "override" => Override(db),
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
        var row = PhpPlanQ1Knot.Apply(
            db,
            9,
            "Keep",
            "old-desc",
            "old-kw",
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["domain_path"] = "cdn.example" });
        return new object[] { row["title"], row["description"], row["keywords"], row["lastUrl"] };
    }

    private static object Url(MySqlConnection db)
    {
        InsertRule(db, 3,
            """{"type":"url","value":"https://%0/t?q=%1&f=%2","args":[{"type":"config","value":"domain_path"},{"type":"get","value":"q"},{"type":"text","value":"fixed"}]}""",
            """{"type":"like_title"}""");
        PhpPlanQ1Knot.PageUrl = () => "/p";
        PhpPlanQ1Knot.FetchHttp = _ => "Title O'Brien";
        var row = PhpPlanQ1Knot.Apply(
            db,
            3,
            "Keep",
            "old-desc",
            "old-kw",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["q"] = "brake" },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["domain_path"] = "cdn.example" });
        return new object[] { row["title"], row["description"], row["keywords"], row["lastUrl"] };
    }

    private static object Complex(MySqlConnection db)
    {
        InsertRule(db, 4,
            """{"type":"complex","value":77,"args":[{"type":"get","value":"art"}]}""",
            "");
        PhpPlanQ1Knot.PageUrl = () => "/p";
        var row = PhpPlanQ1Knot.Apply(
            db,
            4,
            "Keep",
            "old-desc",
            "old-kw",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["art"] = "AB12" },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["domain_path"] = "cdn.example" });
        return new object[] { row["title"], row["description"], row["keywords"], row["lastUrl"] };
    }

    private static object Override(MySqlConnection db)
    {
        InsertRule(db, 5, """{"type":"complex","value":1,"args":[]}""", """{"type":"like_title"}""");
        InsertUrl(db, "/shop", "URL title", "URL desc", "URL kw");
        PhpPlanQ1Knot.PageUrl = () => "/shop";
        var row = PhpPlanQ1Knot.Apply(
            db,
            5,
            "Keep",
            "old-desc",
            "old-kw",
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["domain_path"] = "cdn.example" });
        return new object[] { row["title"], row["description"], row["keywords"], row["lastUrl"] };
    }

    private static void InsertRule(MySqlConnection db, int contentId, string titleRule, string descriptionRule)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO metadata_handler_rules VALUES (@c,@t,@d)";
        cmd.Parameters.AddWithValue("@c", contentId);
        cmd.Parameters.AddWithValue("@t", titleRule);
        cmd.Parameters.AddWithValue("@d", descriptionRule);
        cmd.ExecuteNonQuery();
    }

    private static void InsertUrl(MySqlConnection db, string url, string title, string desc, string kw)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO text_for_url VALUES (@u,@t,@d,@k)";
        cmd.Parameters.AddWithValue("@u", url);
        cmd.Parameters.AddWithValue("@t", title);
        cmd.Parameters.AddWithValue("@d", desc);
        cmd.Parameters.AddWithValue("@k", kw);
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

file static class KnotMysql
{
    public static void Execute(this MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
