using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The storefront profile edit page. <c>Fixtures/EditForm/goldens.json</c> is what the real
/// <c>content/users/editform.php</c> answered for each case of <c>cases.json</c>, run by <c>harness.py</c> on a throwaway
/// schema seeded with <c>seed.sql</c> (translations "T&lt;key&gt;"): the body and the profile and user rows left behind.
/// A new password is stored as bcrypt rather than PHP's md5, and keys that are not registration fields
/// (<c>kept</c> in a case) survive a save, which PHP deletes.
/// </summary>
public sealed class StorefrontEditFormTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "EditForm");

    private static readonly Dictionary<string, string> BaseConfig = new(StringComparer.Ordinal)
    {
        ["min_password_len"] = "6", ["secret_succession"] = "s3cr3t",
    };

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Page_and_rows_match_php_on_a_throwaway_database(string name)
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
            var form = testCase.TryGetProperty("form", out var f)
                ? f.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);

            Task<string> T(string? key) => Task.FromResult("T" + key);
            string body;
            await using (var connection = new MySqlConnection(cs))
            {
                await connection.OpenAsync();
                string? answer = null;
                if (testCase.TryGetProperty("method", out var method) && method.GetString() == "POST")
                {
                    answer = await StorefrontEditForm.PostAsync(connection, request, form, T, CancellationToken.None);
                }

                body = answer ?? await StorefrontEditForm.RenderAsync(connection, request, T, CancellationToken.None);
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

            var kept = testCase.TryGetProperty("kept", out var k) ? k.EnumerateArray().Select(v => v.GetString()!).ToHashSet(StringComparer.Ordinal) : [];
            var profiles = await RowsAsync(cs, "SELECT user_id, data_key, IFNULL(data_value, 'NULL') FROM users_profiles ORDER BY id");
            Assert.Equal(Rows(golden.GetProperty("profiles")), Serialize(profiles.Where(r => !kept.Contains(r[1]))));
            Assert.Equal(kept.Count, profiles.Count(r => kept.Contains(r[1])));

            var users = await RowsAsync(cs, "SELECT user_id, IFNULL(reg_variant, 'NULL'), password FROM users ORDER BY user_id");
            var goldenUsers = golden.GetProperty("users").EnumerateArray().Select(r => r.EnumerateArray().Select(v => v.GetString()!).ToArray()).ToList();
            Assert.Equal(goldenUsers.Count, users.Count);
            for (var i = 0; i < users.Count; i++)
            {
                Assert.Equal(goldenUsers[i][0], users[i][0]);
                Assert.Equal(goldenUsers[i][1], users[i][1]);
                var seeded = users[i][0] == "41" ? "old-hash" : "other-hash";
                if (goldenUsers[i][2] == seeded)
                {
                    Assert.Equal(seeded, users[i][2]);
                }
                else
                {
                    var plain = form["password"];
                    Assert.Equal(Md5(plain + config["secret_succession"]), goldenUsers[i][2]);
                    Assert.StartsWith("$2y$12$", users[i][2], StringComparison.Ordinal);
                    Assert.True(BCrypt.Net.BCrypt.Verify(plain, users[i][2]));
                }
            }
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await Exec(admin, "DROP DATABASE IF EXISTS `" + schema + "`");
        }
    }

    [Fact]
    public void Edit_post_flag_path_and_php_loose_comparisons()
    {
        Assert.True(StorefrontEditForm.IsEditPost(new Dictionary<string, string> { ["edit_user"] = "" }));
        Assert.False(StorefrontEditForm.IsEditPost(new Dictionary<string, string> { ["csrf_guard_key"] = "x" }));
        Assert.True(StorefrontEditFormPostMiddleware.IsEditFormPagePath("/en/users/editform"));
        Assert.True(StorefrontEditFormPostMiddleware.IsEditFormPagePath("/users/editform/"));
        Assert.False(StorefrontEditFormPostMiddleware.IsEditFormPagePath("/en/users/profile"));
        Assert.False(StorefrontEditFormPostMiddleware.IsEditFormPagePath("/en/users/editformx"));

        Assert.True(StorefrontEditForm.ShownFor("[1,2]", " 2"));
        Assert.True(StorefrontEditForm.ShownFor("[1,2]", "2.0"));
        Assert.True(StorefrontEditForm.ShownFor("[\"2\"]", "2"));
        Assert.True(StorefrontEditForm.ShownFor("{\"a\":3}", "3"));
        Assert.False(StorefrontEditForm.ShownFor("[1,2]", "2abc"));
        Assert.False(StorefrontEditForm.ShownFor("[1,2]", null));
        Assert.True(StorefrontEditForm.ShownFor("[0]", null));
        Assert.False(StorefrontEditForm.ShownFor(null, "1"));
        Assert.False(StorefrontEditForm.ShownFor("5", "5"));
        Assert.True(StorefrontEditForm.LooseEquals("1", "1.0"));
        Assert.True(StorefrontEditForm.LooseEquals("1e1", "10"));
        Assert.False(StorefrontEditForm.LooseEquals("abc", "ABC"));
        Assert.True(StorefrontEditForm.LooseEquals(null, ""));
        Assert.False(StorefrontEditForm.LooseEquals("1", null));
    }

    private static string Md5(string value) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Rows(JsonElement rows)
        => JsonSerializer.Serialize(rows.EnumerateArray().Select(r => r.EnumerateArray().Select(v => v.GetString()!).ToArray()).ToList());

    private static string Serialize(IEnumerable<string[]> rows) => JsonSerializer.Serialize(rows.ToList());

    private static async Task<List<string[]>> RowsAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string[]>();
        while (await reader.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetValue(i) is bool flag ? (flag ? "1" : "0") : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "")
                .ToArray());
        }

        return rows;
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
