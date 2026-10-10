using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1WindParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Wind");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Wind_MatchPhpGolden()
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
        => Assert.Equal("content/shop/docpart/epc_price_upload_diagnostics.php", PhpPlanQ1Wind.PriceUploadDiagnosticsPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Wind.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Wind.PriceUploadDiagnosticsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Wind.EpcPypricesApiUrl(""), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Wind.Reset();
        return name switch
        {
            "url" => new object[] { PhpPlanQ1Wind.EpcPypricesApiUrl(""), PhpPlanQ1Wind.EpcPypricesApiUrl("https://shop.example/"), PhpPlanQ1Wind.EpcPypricesApiUrl("https://shop.example") },
            "channels" => Channels(),
            "snap" => Snap(),
            "health" => Health(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Channels()
    {
        var rows = PhpPlanQ1Wind.EpcPriceUploadChannelDefinitions(new Dictionary<string, object?> { ["backend_dir"] = "cp", ["domain_path"] = "https://shop.example", ["tech_key"] = "k1" });
        var slash = PhpPlanQ1Wind.EpcPriceUploadChannelDefinitions(new Dictionary<string, object?> { ["backend_dir"] = "cp", ["domain_path"] = "https://shop.example/", ["tech_key"] = "k1" });
        return new object?[]
        {
            rows.Count,
            rows[0]["cp_url"],
            rows.First(r => Convert.ToString(r["id"]) == "cron_scheduled")["cron_wget"],
            slash.First(r => Convert.ToString(r["id"]) == "cron_scheduled")["cron_wget"],
            rows[6]["id"]
        };
    }

    private static object Snap()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_wind_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, "CREATE TABLE shop_docpart_prices_load_modes (id INT, name VARCHAR(32))");
            Exec(db, "INSERT INTO shop_docpart_prices_load_modes VALUES (1,'Manual'),(2,'FTP')");
            Exec(db, "CREATE TABLE shop_docpart_prices (id INT, name VARCHAR(64), load_mode INT, last_updated VARCHAR(32))");
            Exec(db, "INSERT INTO shop_docpart_prices VALUES (3,'Beta',1,'2026-01-02'),(2,'Acme',1,'2026-01-01')");
            Exec(db, "CREATE TABLE shop_docpart_prices_data (price_id INT)");
            Exec(db, "INSERT INTO shop_docpart_prices_data VALUES (2),(2),(3)");
            Exec(db, "CREATE TABLE epc_price_upload_history (upload_source VARCHAR(32), created_at VARCHAR(32))");
            Exec(db, "INSERT INTO epc_price_upload_history VALUES ('cp_wizard','2026-02-01'),('cp_wizard','2026-02-02')");
            var schemaHits = 0;
            PhpPlanQ1Wind.EnsureSchema = _ => schemaHits = 1;
            var cfg = new Dictionary<string, object?> { ["backend_dir"] = "cp", ["domain_path"] = "", ["tech_key"] = "" };
            var ok = PhpPlanQ1Wind.EpcPriceUploadDiagnosticsSnapshot(db, cfg);
            var by = (Dictionary<string, object?>)ok["by_load_mode"]!;
            var mode1 = (Dictionary<string, object?>)by["1"]!;
            var lists = (List<Dictionary<string, object?>>)mode1["lists"]!;
            var hist = (Dictionary<string, object?>)((Dictionary<string, object?>)ok["history_by_source"]!)["cp_wizard"]!;
            return new object[]
            {
                schemaHits,
                ok["price_lists_total"]!,
                mode1["count"]!,
                mode1["records"]!,
                lists[0]["name"]!,
                hist["uploads"]!,
                ok["cron_tasks"]!,
                ok["pyprices_pending_tasks"]!,
                ((List<Dictionary<string, object?>>)ok["channels"]!).Count
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Health()
    {
        var root = Path.Combine(Path.GetTempPath(), "ecomae_wind_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "cp", "tmp", "prices_upload_files"));
        Directory.CreateDirectory(Path.Combine(root, "content", "files", "price_upload_history"));
        Directory.CreateDirectory(Path.Combine(root, "cp", "content", "shop", "prices_upload"));
        File.WriteAllText(Path.Combine(root, "epc-upload-uae-prices.php"), "1");
        File.WriteAllText(Path.Combine(root, "cp", "content", "shop", "prices_upload", "ajax_1_prepare_tmp_dir.php"), "1");
        File.WriteAllText(Path.Combine(root, "cp", "content", "shop", "prices_upload", "ajax_5_import_csv_to_db.php"), "1");
        File.WriteAllText(Path.Combine(root, "cp", "content", "shop", "prices_upload", "ajax_6_complete_session.php"), "1");
        PhpPlanQ1Wind.DocumentRoot = root;
        PhpPlanQ1Wind.PathIsFile = File.Exists;
        PhpPlanQ1Wind.PathIsDir = Directory.Exists;
        PhpPlanQ1Wind.PathWritable = _ => true;
        PhpPlanQ1Wind.EnsureDir = p => { Directory.CreateDirectory(p); return true; };
        PhpPlanQ1Wind.CurlRaw = (url, _, post) =>
        {
            if (post != null)
            {
                return new Dictionary<string, object?> { ["http_code"] = 200, ["body"] = """{"status":true,"list_to_handle":[]}""" };
            }

            return new Dictionary<string, object?> { ["http_code"] = 200, ["body"] = "ok" };
        };
        var cfg = new Dictionary<string, object?> { ["domain_path"] = "https://shop.example", ["backend_dir"] = "cp", ["tech_key"] = "k1", ["tmp_dir_prices_upload"] = "/tmp/prices_upload_files" };
        var ok = PhpPlanQ1Wind.EpcPriceUploadRunHealthChecks(cfg);
        PhpPlanQ1Wind.CurlRaw = (_, _, post) => post != null
            ? new Dictionary<string, object?> { ["http_code"] = 503, ["body"] = "nope" }
            : new Dictionary<string, object?> { ["http_code"] = 503, ["body"] = "nope" };
        var fail = PhpPlanQ1Wind.EpcPriceUploadRunHealthChecks(cfg);
        try { Directory.Delete(root, true); } catch { /* ignore */ }
        var failCron = (Dictionary<string, object?>)((Dictionary<string, object?>)fail["checks"]!)["cron_crutch"]!;
        return new object[]
        {
            true.Equals(ok["all_ok"]) ? 1 : 0,
            true.Equals(((Dictionary<string, object?>)((Dictionary<string, object?>)ok["checks"]!)["pyprices_api_reachable"]!)["ok"]) ? 1 : 0,
            ok["pyprices_url"]!,
            true.Equals(fail["all_ok"]) ? 1 : 0,
            true.Equals(failCron["ok"]) ? 1 : 0,
            Convert.ToString(failCron["detail"])![..Math.Min(8, Convert.ToString(failCron["detail"])!.Length)]
        };
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
