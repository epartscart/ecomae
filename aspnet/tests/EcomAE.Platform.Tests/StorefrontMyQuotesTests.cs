using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/MyQuotes/harness.php</c> ran PHP <c>content/shop/order_process/my_quotes.php</c> per case (visitor,
/// customer and administrator; list, detail, missing quote; currency cookies and display modes) on throwaway MariaDB
/// databases with a marker in place of the login form; ASP.NET must render the same bytes.
/// </summary>
public sealed class StorefrontMyQuotesTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MyQuotes");

    [Fact]
    public async Task Page_MatchesPhpGolden_OnThrowawayDatabases_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.Equal("UTC", golden.GetProperty("timezone").GetString());
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var cases = spec.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(16, cases.Count);
        Assert.Equal(cases.Count, results.Count);
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        for (var i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            Assert.Equal(c.GetProperty("name").GetString(), results[i].GetProperty("name").GetString());
            var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
            try
            {
                await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";");
                await connection.OpenAsync();
                var setup = c.TryGetProperty("setup", out var s) ? s.EnumerateArray().ToList() : [];
                foreach (var sql in spec.GetProperty("schema").EnumerateArray().Concat(spec.GetProperty("base").EnumerateArray()).Concat(setup))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql.GetString();
                    await command.ExecuteNonQueryAsync();
                }

                string? Cookie(string key) => c.TryGetProperty("cookies", out var cookies) && cookies.TryGetProperty(key, out var v) ? v.GetString() : null;
                var getId = c.GetProperty("get_id");
                var lang = c.TryGetProperty("lang_href", out var lh) ? lh.GetString()! : "/en";
                var input = new StorefrontMyQuotes.Input(
                    c.GetProperty("user_id").GetInt64(),
                    c.GetProperty("admin_id").GetInt64(),
                    getId.ValueKind == JsonValueKind.Null ? 0 : UmapiImageProxy.PhpIntCast(getId.ValueKind == JsonValueKind.String ? getId.GetString() : getId.GetRawText()),
                    lang,
                    "cp",
                    c.TryGetProperty("currency_show_mode", out var mode) ? mode.GetString()! : "sign_before",
                    "784",
                    Cookie("epc_currency"),
                    Cookie("epc_country"),
                    "[[LOGIN_FORM:" + StorefrontMyQuotes.LoginFormPostfix + "]]");
                var html = await StorefrontMyQuotes.RenderAsync(
                    connection,
                    input,
                    id => "{" + id + "}",
                    unix => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    CancellationToken.None);
                Assert.True(
                    results[i].GetProperty("html").GetString() == html,
                    c.GetProperty("name").GetString() + ":\n--- PHP ---\n" + results[i].GetProperty("html").GetString() + "\n--- ASP.NET ---\n" + html);
            }
            finally
            {
                await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
            }
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
