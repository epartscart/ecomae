using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The storefront profile page. <c>Fixtures/ProfileForm/goldens.json</c> is what the real
/// <c>content/users/profileform.php</c> answered for each case of <c>cases.json</c>, run by <c>harness.py</c> on a
/// throwaway schema seeded with <c>seed.sql</c> (translations "T&lt;key&gt;", the admin notice recorded): the body, the
/// notice and the profile, price setting, currency and table rows left behind.
/// </summary>
public sealed class StorefrontProfileFormTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProfileForm");

    private static readonly Dictionary<string, string> BaseConfig = new(StringComparer.Ordinal)
    {
        ["shop_currency"] = "784", ["show_phone_mask"] = "0", ["country_phone_mask"] = "ru", ["backend_dir"] = "cp",
        ["domain_path"] = "http://acme.example/", ["from_name"] = "", ["from_email"] = "", ["smtp_mode"] = "", ["smtp_encryption"] = "",
        ["smtp_host"] = "", ["smtp_port"] = "", ["smtp_username"] = "", ["smtp_password"] = "",
    };

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Page_notice_and_rows_match_php_on_a_throwaway_database(string name)
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
            if (testCase.TryGetProperty("setup", out var setup))
            {
                foreach (var statement in setup.EnumerateArray())
                {
                    await Exec(cs, statement.GetString()!);
                }
            }

            var config = new Dictionary<string, string>(BaseConfig, StringComparer.Ordinal);
            if (testCase.TryGetProperty("config", out var overrides))
            {
                foreach (var p in overrides.EnumerateObject())
                {
                    config[p.Name] = p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetRawText() : p.Value.GetString()!;
                }
            }

            var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
            var cookieHeader = testCase.TryGetProperty("cookie", out var c) ? c.GetString()! : "session=sess41; u_id=41";
            foreach (var part in cookieHeader.Split("; ", StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                cookies[part[..eq]] = part[(eq + 1)..];
            }

            var request = new StorefrontProfileForm.Request(
                cookies.GetValueOrDefault("session"),
                cookies.GetValueOrDefault("u_id"),
                testCase.TryGetProperty("lang", out var lang) ? lang.GetString()! : "/en",
                config);
            var query = testCase.TryGetProperty("query", out var q) ? q.GetString()! : string.Empty;
            var queryMap = query.Length == 0
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.Ordinal);
            var notices = new List<string>();
            var notifier = new StorefrontProfileForm.Notifier(
                (_, userId, _) => Task.FromResult("PROFILE|" + userId.ToString(CultureInfo.InvariantCulture)),
                (_, notifyName, vars, userId, _) =>
                {
                    notices.Add(JsonSerializer.Serialize(new object[] { notifyName, vars, new[] { "P|" + userId.ToString(CultureInfo.InvariantCulture) }, true }));
                    return Task.CompletedTask;
                });

            string body;
            await using (var connection = new MySqlConnection(cs))
            {
                await connection.OpenAsync();
                StorefrontProfileForm.PostOutcome outcome = new(null, null);
                if (testCase.TryGetProperty("method", out var method) && method.GetString() == "POST")
                {
                    var form = testCase.GetProperty("form").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
                    outcome = await StorefrontProfileForm.PostAsync(connection, request, form, queryMap, notifier, CancellationToken.None);
                }

                body = outcome.Body ?? (outcome.Alert ?? string.Empty)
                    + await StorefrontProfileForm.RenderAsync(connection, request, key => Task.FromResult("T" + key), CancellationToken.None);
            }

            var expected = golden.GetProperty("body").GetString()!;
            if (body != expected)
            {
                var i = 0;
                while (i < body.Length && i < expected.Length && body[i] == expected[i])
                {
                    i++;
                }

                Assert.Fail(name + " differs at " + i + ": got " + JsonSerializer.Serialize(body.Substring(Math.Max(0, i - 80), Math.Min(200, body.Length - Math.Max(0, i - 80))))
                    + " want " + JsonSerializer.Serialize(expected.Substring(Math.Max(0, i - 80), Math.Min(200, expected.Length - Math.Max(0, i - 80)))));
            }

            var expectedNotices = golden.GetProperty("notices").EnumerateArray().Select(n => JsonSerializer.Serialize(n)).ToList();
            Assert.Equal(expectedNotices, notices);

            var snapshot = await SnapshotAsync(cs);
            foreach (var table in new[] { "profiles", "settings", "currencies", "tables" })
            {
                Assert.True(Canonical(golden.GetProperty(table)) == snapshot[table], table + ": " + snapshot[table]);
            }
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await Exec(admin, "DROP DATABASE IF EXISTS `" + schema + "`");
        }
    }

    [Fact]
    public void Currency_change_post_is_recognised_by_its_flag()
    {
        Assert.True(StorefrontProfileForm.IsCurrencyChangePost(new Dictionary<string, string> { ["epc_request_currency_change"] = "" }));
        Assert.False(StorefrontProfileForm.IsCurrencyChangePost(new Dictionary<string, string> { ["csrf_guard_key"] = "x" }));
        Assert.True(StorefrontProfilePostMiddleware.IsProfilePagePath("/en/users/profile"));
        Assert.True(StorefrontProfilePostMiddleware.IsProfilePagePath("/users/profile/"));
        Assert.False(StorefrontProfilePostMiddleware.IsProfilePagePath("/en/users/profilex"));
        Assert.False(StorefrontProfilePostMiddleware.IsProfilePagePath("/en/users/editform"));
    }

    private static string Canonical(JsonElement rows)
        => JsonSerializer.Serialize(rows.EnumerateArray().Select(r => r.EnumerateArray().Select(v => v.GetString()!).ToArray()).ToList());

    private static async Task<Dictionary<string, string>> SnapshotAsync(string cs)
    {
        var queries = new Dictionary<string, string>
        {
            ["profiles"] = "SELECT user_id, data_key, IFNULL(data_value, 'NULL') FROM users_profiles ORDER BY user_id, data_key",
            ["settings"] = "SELECT setting_key, IFNULL(setting_value, 'NULL') FROM epc_price_settings ORDER BY setting_key",
            ["currencies"] = "SELECT iso_code, iso_name, caption_short, sign, available, `order` FROM shop_currencies ORDER BY iso_code",
            ["tables"] = "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() ORDER BY table_name",
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
