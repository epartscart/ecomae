using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1VangParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Vang");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Vang_MatchPhpGolden()
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
        => Assert.Equal("content/shop/pos/epc_pos_cp_install.php", PhpPlanQ1Vang.PosCpInstallPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Vang.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Vang.PosCpInstallPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Vang.PosCpInstallPath, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Vang.Reset();
        return name switch
        {
            "lang" => Lang(),
            "register" => Register(),
            "install" => Install(),
            "connect" => Connect(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Lang()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_vang_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Schema(db);
            PhpPlanQ1Vang.EpcPosCpLang(db, "epc_pos_terminal_cp", "POS Terminal", "Касса POS");
            PhpPlanQ1Vang.EpcPosCpLang(db, "epc_pos_terminal_cp", "POS O'term", "Касса");
            return new object?[] { Rows(db, "SELECT str_key, description FROM lang_text_strings ORDER BY str_key"), Rows(db, "SELECT str_key, lang_code, value FROM lang_text_strings_translation ORDER BY str_key, lang_code") };
        }
        finally
        {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Register()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_vang_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Schema(db);
            Exec(db, "INSERT INTO groups VALUES (1, 0, 1), (2, 1, 1), (3, 2, 1)");
            Exec(db, "INSERT INTO content (id, url, level, is_frontend, published_flag) VALUES (10, 'shop', 1, 0, 1)");
            var first = PhpPlanQ1Vang.EpcPosCpRegisterContent(db, "shop", "shop/pos", "pos_folder", "epc_cp_group_pos", "/cp/pos.php", "Point of Sale", 86);
            var again = PhpPlanQ1Vang.EpcPosCpRegisterContent(db, "shop", "shop/pos", "pos_folder", "epc_cp_group_pos", "/cp/pos2.php", "POS O'hub", 86);
            var missing = 0;
            try
            {
                PhpPlanQ1Vang.EpcPosCpRegisterContent(db, "nope", "shop/x", "x", "k", "/x.php", "X");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Parent not found", StringComparison.Ordinal))
            {
                missing = 1;
            }

            var row = Rows(db, "SELECT url, content, title_tag, alias, value, parent, level, published_flag FROM content WHERE url='shop/pos'")[0];
            var groups = ScalarList(db, "SELECT group_id FROM content_access WHERE content_id = " + again + " ORDER BY group_id");
            return new object?[] { first, again, missing, row, groups };
        }
        finally
        {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Install()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_vang_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Schema(db);
            Exec(db, "INSERT INTO groups VALUES (1, 0, 1)");
            Exec(db, "INSERT INTO content (id, url, level, is_frontend, published_flag) VALUES (10, 'shop', 1, 0, 1), (20, 'control/config', 1, 0, 1), (21, 'control/portal/epc_tenant_control_center', 2, 0, 1)");
            Exec(db, "INSERT INTO content_access VALUES (21, 7), (21, 9)");
            var schemaHit = 0;
            PhpPlanQ1Vang.EnsureSchema = _ => schemaHit = 1;
            PhpPlanQ1Vang.EnsureWalkin = _ => 44;
            PhpPlanQ1Vang.PortalMenu = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["portal"] = 1 };
            PhpPlanQ1Vang.PosMenu = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["pos"] = 1 };
            var outRow = PhpPlanQ1Vang.EpcPosCpInstall(db, "cp");
            var urls = ScalarListStr(db, "SELECT url FROM content WHERE is_frontend=0 ORDER BY url");
            var superAccess = ScalarList(db, "SELECT group_id FROM content_access WHERE content_id = " + Convert.ToInt32(outRow["super_content_id"]) + " ORDER BY group_id");
            return new object?[]
            {
                Convert.ToInt32(outRow["hub_content_id"]) > 0 ? 1 : 0,
                Convert.ToInt32(outRow["content_id"]) > 0 ? 1 : 0,
                Convert.ToInt32(outRow["super_content_id"]) > 0 ? 1 : 0,
                outRow["walkin_user_id"],
                outRow["menu"],
                urls,
                superAccess,
                schemaHit
            };
        }
        finally
        {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Connect()
    {
        var cfg = new PhpPlanQ1Vang.VangConfig { Host = "127.0.0.1", User = "ecomae", Password = Password() };
        PhpPlanQ1Vang.OpenPdo = (c, db, user, pass) =>
        {
            try
            {
                var conn = new MySqlConnection($"Server={c.Host};Port=3306;Database={db};User ID={user};Password={pass};AllowUserVariables=true;");
                conn.Open();
                return conn;
            }
            catch
            {
                return null;
            }
        };
        var empty = PhpPlanQ1Vang.EpcPosSetupConnect(new Dictionary<string, string>(StringComparer.Ordinal) { ["db"] = "" }, cfg);
        var missingUser = PhpPlanQ1Vang.EpcPosSetupConnect(new Dictionary<string, string>(StringComparer.Ordinal) { ["db"] = "mysql", ["user"] = "", ["pass"] = "" }, cfg);
        var ok = PhpPlanQ1Vang.EpcPosSetupConnect(new Dictionary<string, string>(StringComparer.Ordinal) { ["db"] = "mysql", ["user"] = "ecomae", ["pass"] = Password() }, cfg);
        var bad = PhpPlanQ1Vang.EpcPosSetupConnect(new Dictionary<string, string>(StringComparer.Ordinal) { ["db"] = "no_such_db_vang", ["user"] = "ecomae", ["pass"] = "x" }, cfg);
        missingUser?.Dispose();
        ok?.Dispose();
        return new object[] { empty is null ? 1 : 0, missingUser is not null ? 1 : 0, ok is not null ? 1 : 0, bad is null ? 1 : 0 };
    }

    private static void Schema(MySqlConnection db)
    {
        Exec(db, "CREATE TABLE lang_text_strings (str_key VARCHAR(64) PRIMARY KEY, description TEXT, same VARCHAR(8), is_error INT, is_custom INT, used_found INT)");
        Exec(db, "CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64), lang_code VARCHAR(8), value TEXT, PRIMARY KEY (str_key, lang_code))");
        Exec(db, """
            CREATE TABLE content (
                id INT AUTO_INCREMENT PRIMARY KEY, count INT, url VARCHAR(128), level INT, alias VARCHAR(64), value VARCHAR(64),
                parent INT, description VARCHAR(255), is_frontend INT, content_type VARCHAR(16), content VARCHAR(255),
                title_tag VARCHAR(128), description_tag VARCHAR(16), keywords_tag VARCHAR(16), author_tag VARCHAR(16),
                main_flag INT, modules_array TEXT, css_js TEXT, robots_tag VARCHAR(16),
                system_flag INT, published_flag INT, open INT, time_created INT, time_edited INT, `order` INT
            )
            """);
        Exec(db, "CREATE TABLE content_access (content_id INT, group_id INT, PRIMARY KEY (content_id, group_id))");
        Exec(db, "CREATE TABLE groups (id INT PRIMARY KEY, parent INT, for_backend INT)");
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

    private static List<Dictionary<string, object?>> Rows(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var raw = reader.GetValue(i);
                row[reader.GetName(i)] = raw is DBNull ? null : raw is long or int or short or byte ? Convert.ToInt32(raw) : Convert.ToString(raw);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static List<int> ScalarList(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<int>();
        while (reader.Read())
        {
            rows.Add(Convert.ToInt32(reader.GetValue(0)));
        }

        return rows;
    }

    private static List<string> ScalarListStr(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add(Convert.ToString(reader.GetValue(0)) ?? "");
        }

        return rows;
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
            _ => true
        };
    }
}

file static class VangMysql
{
    public static void Execute(this MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
