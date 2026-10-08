using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The storefront login POST. <c>Fixtures/LoginPost/goldens.json</c> is what the real
/// <c>plugins/authentication/plugin.php</c> answered for each case of <c>cases.json</c>, run by <c>harness.py</c> on a
/// throwaway schema seeded with <c>seed.sql</c>: status, Location, body, cookies, the staff notices and the rows left
/// behind. The one intended row difference is the new customer session's <c>last_activiti_time</c>, stamped at once.
/// </summary>
public sealed class StorefrontLoginPostTests
{
    private const string Secret = "golden-secret";

    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "LoginPost");

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Answer_cookies_and_rows_match_php_on_a_throwaway_database(string name)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var testCase = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == name).Clone();
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name).Clone();

        var schema = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + schema + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await Exec(admin, "CREATE DATABASE `" + schema + "`");
        try
        {
            await Exec(cs, File.ReadAllText(Path.Combine(FixtureDir, "seed.sql")));
            await Exec(cs, "CREATE TABLE `lang_text_strings` (`str_key` VARCHAR(32) NOT NULL, `is_error` TINYINT(1) NOT NULL DEFAULT 0, `same` VARCHAR(8) NULL);"
                + "CREATE TABLE `lang_text_strings_translation` (`str_key` VARCHAR(32) NOT NULL, `lang_code` VARCHAR(8) NOT NULL, `value` TEXT NULL);"
                + "INSERT INTO `lang_text_strings` (`str_key`) VALUES ('4787'); INSERT INTO `lang_text_strings_translation` VALUES ('4787', 'en', 'T4787');");
            if (testCase.TryGetProperty("setup", out var setup))
            {
                foreach (var statement in setup.EnumerateArray())
                {
                    await Exec(cs, statement.GetString()!);
                }
            }

            var agent = testCase.TryGetProperty("agent", out var a) ? a.GetString()! : "Mozilla/5.0 golden";
            if (StorefrontLoginPost.IsBot(agent))
            {
                Assert.Equal("HTML:", golden.GetProperty("body").GetString());
                Assert.Empty(golden.GetProperty("cookies").EnumerateArray());
                return;
            }

            var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
            if (testCase.TryGetProperty("cookie", out var cookieHeader))
            {
                foreach (var part in cookieHeader.GetString()!.Split("; "))
                {
                    var eq = part.IndexOf('=');
                    cookies[part[..eq]] = part[(eq + 1)..];
                }
            }

            var query = testCase.TryGetProperty("query", out var q) ? q.GetString()! : string.Empty;
            var queryMap = query.Length == 0
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.Ordinal);
            var form = testCase.GetProperty("form").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
            var notices = new List<(long UserId, string Contact)>();

            StorefrontLoginPost.Outcome outcome;
            await using (var connection = new MySqlConnection(cs))
            {
                await connection.OpenAsync();
                outcome = await StorefrontLoginPost.HandleAsync(
                    connection,
                    new StorefrontLoginPost.Request(
                        form,
                        queryMap,
                        cookies.GetValueOrDefault("session"),
                        cookies.GetValueOrDefault("u_id"),
                        "127.0.0.1",
                        agent,
                        Secret,
                        "http://acme.example/",
                        "http://acme.example/en/users/login" + query,
                        "en"),
                    (_, userId, contact, _) =>
                    {
                        notices.Add((userId, contact));
                        return Task.CompletedTask;
                    },
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    CancellationToken.None);
            }

            switch (outcome.Kind)
            {
                case StorefrontLoginPost.Kind.Redirect:
                    Assert.Equal(302, golden.GetProperty("status").GetInt32());
                    Assert.Equal(golden.GetProperty("location").GetString(), outcome.Location);
                    break;
                case StorefrontLoginPost.Kind.Body:
                    Assert.Equal(200, golden.GetProperty("status").GetInt32());
                    Assert.Equal(golden.GetProperty("body").GetString(), outcome.Body);
                    break;
                default:
                    Assert.Equal(200, golden.GetProperty("status").GetInt32());
                    Assert.Equal(golden.GetProperty("body").GetString(), "HTML:" + outcome.Alert);
                    break;
            }

            var actualCookies = outcome.Cookies
                .Select(c => c.Name + "=" + (Regex.IsMatch(c.Value, "^[0-9a-f]{32}$") ? "S32" : c.Value) + (c.Expires > 0 ? "|expires" : "|session"))
                .Order(StringComparer.Ordinal)
                .ToList();
            Assert.Equal(golden.GetProperty("cookies").EnumerateArray().Select(c => c.GetString()!).ToList(), actualCookies);

            var expectedNotices = golden.GetProperty("notices").EnumerateArray()
                .Select(n => (n[0].GetString(), n[1].GetProperty("user_id").GetInt64(), n[1].GetProperty("login_contact").GetString()))
                .ToList();
            Assert.Equal(expectedNotices, notices.Select(n => ((string?)"epc_customer_login", n.UserId, (string?)n.Contact)).ToList());

            var snapshot = await SnapshotAsync(cs);
            foreach (var table in new[] { "users", "options", "profiles", "carts" })
            {
                Assert.True(Canonical(golden.GetProperty(table)) == snapshot[table], table + ": " + snapshot[table]);
            }

            var sessions = golden.GetProperty("sessions").EnumerateArray()
                .Select(r => r.EnumerateArray().Select(v => v.GetString()!).ToArray())
                .Select(r => r[0] == "S32" && r[1] != "0" ? r.Select((v, i) => i == 4 ? "1" : v).ToArray() : r)
                .ToList();
            Assert.True(JsonSerializer.Serialize(sessions) == snapshot["sessions"], "sessions: " + snapshot["sessions"]);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await Exec(admin, "DROP DATABASE IF EXISTS `" + schema + "`");
        }
    }

    [Fact]
    public void Safe_target_bot_check_and_csrf_body_follow_php()
    {
        Assert.Equal("/", StorefrontLoginPost.SafeTarget(""));
        Assert.Equal("/", StorefrontLoginPost.SafeTarget("HTTPS://evil.example"));
        Assert.Equal("/", StorefrontLoginPost.SafeTarget("//evil.example"));
        Assert.Equal("/", StorefrontLoginPost.SafeTarget("en/cart"));
        Assert.Equal("/", StorefrontLoginPost.SafeTarget("/en/\ncart"));
        Assert.Equal("/en/cart?x=1", StorefrontLoginPost.SafeTarget(" /en/cart?x=1 "));
        Assert.True(StorefrontLoginPost.IsBot(""));
        Assert.True(StorefrontLoginPost.IsBot("Mozilla/5.0 (compatible; Googlebot/2.1)"));
        Assert.False(StorefrontLoginPost.IsBot("Mozilla/5.0 (X11; Linux x86_64) Chrome/120"));
        Assert.False(StorefrontLoginPost.IsBot("ecomae CP monitor"));
        Assert.Equal("{\"error\":\"Error! CSRF 4\",\"message\":\"Error! CSRF 4\",\"status\":false}", StorefrontLoginPost.CsrfError("Error! CSRF 4"));
        Assert.True(StorefrontLoginPostMiddleware.IsLoginPagePath("/en/users/login"));
        Assert.True(StorefrontLoginPostMiddleware.IsLoginPagePath("/ar/users/login/"));
        Assert.True(StorefrontLoginPostMiddleware.IsLoginPagePath("/users/login"));
        Assert.True(StorefrontLoginPostMiddleware.IsLoginPagePath("/storefront/login"));
        Assert.False(StorefrontLoginPostMiddleware.IsLoginPagePath("/cp/login"));
        Assert.False(StorefrontLoginPostMiddleware.IsLoginPagePath("/en/users/loginx"));
    }

    private static string Canonical(JsonElement rows)
        => JsonSerializer.Serialize(rows.EnumerateArray().Select(r => r.EnumerateArray().Select(v => v.GetString()!).ToArray()).ToList());

    private static async Task<Dictionary<string, string>> SnapshotAsync(string cs)
    {
        var queries = new Dictionary<string, string>
        {
            ["users"] = "SELECT user_id, LEFT(password, 4), time_last_visit > 0 FROM users ORDER BY user_id",
            ["sessions"] = "SELECT IF(session REGEXP '^[0-9a-f]{32}$', 'S32', session), user_id, time > 1, LENGTH(csrf_guard_key), last_activiti_time > 0, IFNULL(`2fa_attempts`, 'NULL') FROM sessions ORDER BY id",
            ["options"] = "SELECT id, session_id FROM users_options ORDER BY id",
            ["profiles"] = "SELECT user_id, data_key, IFNULL(data_value, 'NULL') FROM users_profiles ORDER BY user_id, data_key",
            ["carts"] = "SELECT id, user_id, session_id FROM shop_carts ORDER BY id",
        };
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, sql) in queries)
        {
            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<string[]>();
            while (await reader.ReadAsync())
            {
                rows.Add(Enumerable.Range(0, reader.FieldCount)
                    .Select(i => reader.GetValue(i) is bool flag ? (flag ? "1" : "0") : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "")
                    .ToArray());
            }

            result[key] = JsonSerializer.Serialize(rows);
        }

        return result;
    }

    private static async Task Exec(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
