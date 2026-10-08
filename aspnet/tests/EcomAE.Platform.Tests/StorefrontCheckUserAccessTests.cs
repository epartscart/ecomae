using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>content/users/check_user_access.php</c>. <c>Fixtures/CheckUserAccess/goldens.json</c> is what the real PHP include
/// (with the real <c>dp_user.php</c> and <c>lang/dp_lang.php</c>, as a Control Panel ajax request) answered for each case
/// of <c>cases.json</c>, run by <c>harness.py</c> on a throwaway schema seeded with <c>seed.sql</c>. "ALLOWED" means the
/// include let the script go on.
/// </summary>
public sealed class StorefrontCheckUserAccessTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CheckUserAccess");

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Decision_AndMessage_MatchPhp_OnThrowawayDatabase_ThenDropped(string name)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var testCase = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == name).Clone();
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name).Clone();
        Assert.False(golden.GetProperty("fatal").GetBoolean());

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "`");
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            await ExecAsync(cs, await File.ReadAllTextAsync(Path.Combine(FixtureDir, "seed.sql")));
            if (testCase.TryGetProperty("setup", out var setup))
            {
                foreach (var statement in setup.EnumerateArray())
                {
                    await ExecAsync(cs, statement.GetString()!);
                }
            }

            var pages = testCase.GetProperty("pages").EnumerateArray()
                .Select(p => new StorefrontPhpAjax.PhpPageToCheck(p.GetProperty("url").GetString()!, p.GetProperty("is_frontend").GetInt32()))
                .ToList();
            var cpLang = new StorefrontPhpAjax.CpLangRequest(
                testCase.TryGetProperty("lang_cp", out var langCp) ? langCp.GetString() : null,
                testCase.TryGetProperty("backend_ui_lang", out var forced) ? forced.GetString() : null,
                testCase.TryGetProperty("multilang", out var multilang) && multilang.GetBoolean());

            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            var denied = await StorefrontPhpAjax.CheckUserAccessAsync(
                connection,
                pages,
                testCase.TryGetProperty("admin", out var adminId) ? adminId.GetInt64() : 0,
                testCase.TryGetProperty("user", out var userId) ? userId.GetInt64() : 0,
                () => StorefrontPhpAjax.PhpBackendLangAsync(connection, cpLang, CancellationToken.None),
                CancellationToken.None);

            var body = golden.GetProperty("body").GetString()!;
            if (body == "ALLOWED")
            {
                Assert.Null(denied);
                return;
            }

            Assert.NotNull(denied);
            var php = JsonDocument.Parse(body).RootElement;
            Assert.Equal(["status", "error", "message"], php.EnumerateObject().Select(p => p.Name).ToArray());
            Assert.Equal(php.GetProperty("status").GetBoolean(), denied.Status);
            Assert.Equal(php.GetProperty("error").GetString(), denied.Error);
            Assert.Equal(php.GetProperty("message").GetString(), denied.Message);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
