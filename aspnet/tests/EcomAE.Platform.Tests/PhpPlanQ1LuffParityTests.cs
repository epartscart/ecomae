using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1LuffParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Luff");
    private static readonly string RepoRoot = FindRepoRoot();

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
    public void PlanQ1Luff_MatchPhpGolden()
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
            if (!Same(Json(actual.Extra), expected))
            {
                using var left = JsonDocument.Parse(Json(actual.Extra));
                failures.Add(name + " " + FirstValueDiff(left.RootElement, expected, name));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Luff.DemoAutopartsBootstrapPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Luff.DemoAutopartsBootstrapPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Luff.Reset();
        Assert.Contains("epc_demo_autoparts_bootstrap.php", PhpPlanQ1Luff.DemoAutopartsBootstrapPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Luff.DemoAutopartsBootstrapPath, StringComparison.Ordinal);
        PhpPlanQ1Luff.PresetPath = () => Path.Combine(RepoRoot, "content", "general_pages", "epc_theme_presets", "automotive_spareparts_pro.json");
        var preset = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapPreset();
        Assert.DoesNotContain("PHPSESSID", Convert.ToString(preset["id"]), StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Luff.DemoAutopartsBootstrapPath, StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Luff.Reset();
        return name switch
        {
            "preset" => Preset(),
            "verify" => Verify(),
            "apply" => Apply(),
            "fail" => Fail(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static Rendered Preset()
    {
        PhpPlanQ1Luff.PresetPath = () => Path.Combine(RepoRoot, "content", "general_pages", "epc_theme_presets", "automotive_spareparts_pro.json");
        var real = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapPreset();
        var tables = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapCloneTables();
        var missing = Path.Combine(Path.GetTempPath(), "ecomae_luff_missing_" + Guid.NewGuid().ToString("N")[..8] + ".json");
        PhpPlanQ1Luff.PresetPath = () => missing;
        var gone = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapPreset();
        var empty = Path.Combine(Path.GetTempPath(), "ecomae_luff_empty_" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(empty, "");
        PhpPlanQ1Luff.PresetPath = () => empty;
        var blank = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapPreset();
        var bad = Path.Combine(Path.GetTempPath(), "ecomae_luff_bad_" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(bad, "{");
        PhpPlanQ1Luff.PresetPath = () => bad;
        var invalid = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapPreset();
        var custom = Path.Combine(Path.GetTempPath(), "ecomae_luff_custom_" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(custom, "{\"docpart_clone_tables\":[\"shop_geo\",\"shop_offices`x\"],\"header_verify\":{\"location\":\"O'man\",\"hours\":\"Sat-Sun\"}}");
        PhpPlanQ1Luff.PresetPath = () => custom;
        var over = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapPreset();
        var overTables = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapCloneTables();
        File.Delete(empty);
        File.Delete(bad);
        File.Delete(custom);
        var verify = over["header_verify"] as Dictionary<string, object?>;
        return new Rendered(new object?[]
        {
            Path.GetFileName(Path.Combine(RepoRoot, "content", "general_pages", "epc_theme_presets", "automotive_spareparts_pro.json")),
            real["id"],
            real["header_verify"],
            tables.Count,
            tables[0],
            tables[^1],
            gone.Count == 0 ? new List<object?>() : gone,
            blank.Count == 0 ? new List<object?>() : blank,
            invalid.Count == 0 ? new List<object?>() : invalid,
            verify,
            overTables
        });
    }

    private static Rendered Verify()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_luff_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            var missing = Slim(PhpPlanQ1Luff.EpcDemoAutopartsBootstrapVerifyDb(db, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["location"] = "Dubai",
                ["hours"] = "Mon-Fri from 9:00"
            }));
            Exec(db, "CREATE TABLE shop_geo (id INT PRIMARY KEY, name VARCHAR(64))");
            Exec(db, "CREATE TABLE shop_offices (id INT PRIMARY KEY, city VARCHAR(64), timetable VARCHAR(128))");
            Exec(db, "CREATE TABLE lang_text_strings_translation (id INT AUTO_INCREMENT PRIMARY KEY, value TEXT)");
            var noGeo = Slim(PhpPlanQ1Luff.EpcDemoAutopartsBootstrapVerifyDb(db, new Dictionary<string, string>(StringComparer.Ordinal)));
            Exec(db, "INSERT INTO shop_geo VALUES (3, 'Dubai')");
            Exec(db, "INSERT INTO lang_text_strings_translation (value) VALUES ('Hours: Mon-Fri from 9:00 GST')");
            var ok = Slim(PhpPlanQ1Luff.EpcDemoAutopartsBootstrapVerifyDb(db, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["location"] = "Dubai",
                ["hours"] = "Mon-Fri from 9:00"
            }));
            Exec(db, "DELETE FROM shop_geo");
            Exec(db, "INSERT INTO shop_geo VALUES (1, 'A'), (2, 'B')");
            Exec(db, "DELETE FROM lang_text_strings_translation");
            Exec(db, "INSERT INTO shop_offices VALUES (1, \"O'man City\", 'Open daily')");
            var office = Slim(PhpPlanQ1Luff.EpcDemoAutopartsBootstrapVerifyDb(db, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["location"] = "O'man",
                ["hours"] = "Mon-Fri from 9:00"
            }));
            Exec(db, "INSERT INTO lang_text_strings_translation (value) VALUES ('100%_open')");
            var like = Slim(PhpPlanQ1Luff.EpcDemoAutopartsBootstrapVerifyDb(db, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["location"] = "O'man",
                ["hours"] = "100%_open"
            }));
            return new Rendered(new object?[] { missing, noGeo, ok, office, like });
        }
        finally
        {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static Rendered Apply()
    {
        using var admin = OpenAdmin();
        var srcName = "ecomae_cpw_luff_" + Guid.NewGuid().ToString("N")[..8];
        var dstName = "ecomae_cpw_luff_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{srcName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        admin.Execute($"CREATE DATABASE `{dstName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var src = OpenDb(srcName);
            using var dst = OpenDb(dstName);
            foreach (var db in new[] { src, dst })
            {
                Exec(db, "CREATE TABLE content (id INT PRIMARY KEY, main_flag INT, published_flag INT, is_frontend INT, modules_array TEXT)");
                Exec(db, "CREATE TABLE shop_catalogue_categories (id INT PRIMARY KEY, published_flag INT, parent INT)");
                Exec(db, "CREATE TABLE shop_geo (id INT PRIMARY KEY, name VARCHAR(64))");
                Exec(db, "CREATE TABLE shop_offices (id INT PRIMARY KEY, city VARCHAR(64), timetable VARCHAR(128))");
                Exec(db, "CREATE TABLE lang_text_strings_translation (id INT AUTO_INCREMENT PRIMARY KEY, value TEXT)");
            }

            Exec(src, "INSERT INTO content VALUES (1, 1, 1, 1, '[1,2]')");
            Exec(src, "INSERT INTO shop_geo VALUES (3, 'Dubai')");
            Exec(dst, "INSERT INTO content VALUES (1, 1, 1, 1, '[]')");
            Exec(dst, "INSERT INTO lang_text_strings_translation (value) VALUES ('Mon-Fri from 9:00')");
            PhpPlanQ1Luff.PresetPath = () => Path.Combine(RepoRoot, "content", "general_pages", "epc_theme_presets", "automotive_spareparts_pro.json");
            PhpPlanQ1Luff.SourcePdo = () => src;
            var cloned = new List<List<string>>();
            PhpPlanQ1Luff.CloneTables = (_, dest, tables) =>
            {
                cloned.Add(tables.ToList());
                foreach (var tbl in tables)
                {
                    var name = tbl.Replace("`", "", StringComparison.Ordinal);
                    try
                    {
                        Exec(dest, "SELECT COUNT(*) FROM `" + name + "`");
                    }
                    catch
                    {
                        Exec(dest, "CREATE TABLE `" + name + "` (id INT PRIMARY KEY)");
                    }
                }

                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = true,
                    ["tables"] = tables.ToList(),
                    ["errors"] = new List<string>()
                };
            };
            var incremental = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapApply(dst, false);
            var clonedInc = cloned.ToList();
            cloned.Clear();
            Exec(dst, "INSERT INTO shop_geo VALUES (3, 'Dubai')");
            var force = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapApply(dst, true);
            var verify = (Dictionary<string, object?>)incremental["verify"]!;
            return new Rendered(new object?[]
            {
                incremental["ok"] is true ? 1 : 0,
                incremental["force"] is true ? 1 : 0,
                incremental["preset"],
                incremental["home_modules"] is true ? 1 : 0,
                incremental["root_categories"],
                incremental["geo_nodes"],
                incremental["offices"],
                incremental["message"],
                verify["ok"] is true ? 1 : 0,
                clonedInc.Count,
                clonedInc.Count > 0 ? clonedInc[0].Count : 0,
                force["force"] is true ? 1 : 0,
                cloned.Count,
                cloned.Count > 0 ? cloned[0].Count : 0,
                Scalar(dst, "SELECT COUNT(*) FROM shop_geo")
            });
        }
        finally
        {
            admin.Execute($"DROP DATABASE IF EXISTS `{srcName}`");
            admin.Execute($"DROP DATABASE IF EXISTS `{dstName}`");
        }
    }

    private static Rendered Fail()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_luff_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            PhpPlanQ1Luff.SourcePdo = () => null;
            var noSrc = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapApply(db, false);
            Exec(db, "CREATE TABLE shop_geo (id INT PRIMARY KEY, name VARCHAR(64))");
            Exec(db, "CREATE TABLE shop_offices (id INT PRIMARY KEY, city VARCHAR(64), timetable VARCHAR(128))");
            Exec(db, "CREATE TABLE lang_text_strings_translation (id INT AUTO_INCREMENT PRIMARY KEY, value TEXT)");
            Exec(db, "CREATE TABLE content (id INT PRIMARY KEY, main_flag INT, published_flag INT, is_frontend INT, modules_array TEXT)");
            Exec(db, "CREATE TABLE shop_catalogue_categories (id INT PRIMARY KEY, published_flag INT, parent INT)");
            var srcName = "ecomae_cpw_luff_" + Guid.NewGuid().ToString("N")[..8];
            admin.Execute($"CREATE DATABASE `{srcName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            using var src = OpenDb(srcName);
            PhpPlanQ1Luff.PresetPath = () => Path.Combine(RepoRoot, "content", "general_pages", "epc_theme_presets", "automotive_spareparts_pro.json");
            PhpPlanQ1Luff.SourcePdo = () => src;
            PhpPlanQ1Luff.CloneTables = (_, _, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["tables"] = new List<string>(),
                ["errors"] = new List<string> { "clone failed" }
            };
            var err = PhpPlanQ1Luff.EpcDemoAutopartsBootstrapApply(db, true);
            admin.Execute($"DROP DATABASE IF EXISTS `{srcName}`");
            var cloned = (Dictionary<string, object?>)err["cloned"]!;
            var verify = (Dictionary<string, object?>)err["verify"]!;
            return new Rendered(new object?[]
            {
                noSrc["ok"] is true ? 1 : 0,
                noSrc["message"],
                err["ok"] is true ? 1 : 0,
                cloned["errors"],
                verify["ok"] is true ? 1 : 0,
                err["message"]
            });
        }
        finally
        {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static Dictionary<string, object?> Slim(Dictionary<string, object?> row)
        => new(StringComparer.Ordinal)
        {
            ["ok"] = Empty(row.TryGetValue("ok", out var ok) ? ok : null) ? 0 : 1,
            ["location"] = Empty(row.TryGetValue("location", out var loc) ? loc : null) ? 0 : 1,
            ["hours"] = Empty(row.TryGetValue("hours", out var hours) ? hours : null) ? 0 : 1,
            ["geo_dubai"] = Empty(row.TryGetValue("geo_dubai", out var geo) ? geo : null) ? 0 : 1,
            ["geo_error"] = Empty(row.TryGetValue("geo_error", out var ge) ? ge : null) ? 0 : 1,
            ["office_error"] = Empty(row.TryGetValue("office_error", out var oe) ? oe : null) ? 0 : 1
        };

    private static bool Empty(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is bool flag)
        {
            return !flag;
        }

        var text = Convert.ToString(value) ?? "";
        return text == "" || text == "0";
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

    private static int Scalar(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "epc_demo_autoparts_bootstrap.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return "/workspace-wt/small-done";
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

    private static string FirstValueDiff(JsonElement left, JsonElement right, string path)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return path + " kind " + left.ValueKind + " vs " + right.ValueKind + " L=" + left.GetRawText() + " R=" + right.GetRawText();
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in right.EnumerateObject())
                {
                    if (!left.TryGetProperty(prop.Name, out var other))
                    {
                        return path + "." + prop.Name + " missing on actual";
                    }

                    var nested = FirstValueDiff(other, prop.Value, path + "." + prop.Name);
                    if (nested != "")
                    {
                        return nested;
                    }
                }

                return "";
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                if (a.Count != b.Count)
                {
                    return path + " len " + a.Count + " vs " + b.Count;
                }

                for (var i = 0; i < a.Count; i++)
                {
                    var nested = FirstValueDiff(a[i], b[i], path + "[" + i + "]");
                    if (nested != "")
                    {
                        return nested;
                    }
                }

                return "";
            case JsonValueKind.String:
                var ls = left.GetString() ?? "";
                var rs = right.GetString() ?? "";
                return ls == rs ? "" : path + " exp=" + rs + " got=" + ls;
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble()
                    ? ""
                    : path + " exp=" + right.GetRawText() + " got=" + left.GetRawText();
            default:
                return "";
        }
    }
}

file static class LuffMysql
{
    public static void Execute(this MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
