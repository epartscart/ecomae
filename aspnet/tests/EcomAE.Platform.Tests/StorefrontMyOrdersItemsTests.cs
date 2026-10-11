using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP 8.3 <c>Fixtures/MyOrdersItems/harness.php</c> ran the real page on one isolated
/// <c>ecomae_cpw_*</c> database per fixture. Except for the documented escaping deviation, C# must match every byte.
/// </summary>
public sealed class StorefrontMyOrdersItemsTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MyOrdersItems");

    [Fact]
    public async Task Page_MatchesPhp83RuntimeGoldens_ByteForByte()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        Assert.Equal("UTC", golden.GetProperty("timezone").GetString());
        var cases = spec.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(35, cases.Count);
        Assert.Equal(cases.Count, results.Count);
        for (var i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            Assert.Equal(c.GetProperty("name").GetString(), results[i].GetProperty("name").GetString());
            if (c.GetProperty("name").GetString() == "hostile_cookie_and_db")
            {
                continue;
            }

            await WithDatabaseAsync(password, spec, c, async connection =>
            {
                var input = Input(c);
                var html = await StorefrontMyOrdersItems.RenderAsync(
                    connection,
                    input,
                    key => Task.FromResult("{" + key + "}"),
                    unix => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
                        + "<br><small>" + DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("H:mm", CultureInfo.InvariantCulture) + "</small>",
                    CancellationToken.None);
                Assert.True(
                    results[i].GetProperty("html").GetString() == html,
                    Difference(c.GetProperty("name").GetString()!, results[i].GetProperty("html").GetString()!, html));
            });
        }
    }

    [Fact]
    public async Task HostileCookieAndDatabaseValues_AreEscaped_AsIntentionalDeviation()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var c = spec.GetProperty("cases").EnumerateArray().Single(e => e.GetProperty("name").GetString() == "hostile_cookie_and_db");
        await WithDatabaseAsync(password, spec, c, async connection =>
        {
            var html = await StorefrontMyOrdersItems.RenderAsync(
                connection,
                Input(c),
                key => Task.FromResult("{" + key + "}"),
                _ => "date",
                CancellationToken.None);
            Assert.Contains("value=\"&quot;&gt;&lt;script&gt;cookie1&lt;/script&gt;\"", html, StringComparison.Ordinal);
            Assert.Contains("&lt;img src=x onerror=db1&gt;", html, StringComparison.Ordinal);
            Assert.Contains("A&amp;B", html, StringComparison.Ordinal);
            Assert.Contains("&lt;script&gt;db2&lt;/script&gt;", html, StringComparison.Ordinal);
            Assert.Contains("&lt;svg onload=db3&gt;", html, StringComparison.Ordinal);
            Assert.Contains("{&lt;b&gt;Status&lt;/b&gt;}", html, StringComparison.Ordinal);
            Assert.Contains("background:&quot; onmouseover=db4", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<script>cookie1", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<img src=x", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<svg onload", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<b>Status</b>", html, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(7, 1, "<li class=\"active\"><a>1</a></li><li><a href='/shop/orders/items?page=2'>2</a></li><li><a href='/shop/orders/items?page=3'>3</a></li><li><a href='/shop/orders/items?page=4' >...</a></li><li><a href='/shop/orders/items?page=7' >7</a></li>")]
    [InlineData(7, 5, "<li><a href='/shop/orders/items'>1</a></li><li><a href='/shop/orders/items?page=2' >...</a></li><li><a href='/shop/orders/items?page=3'>3</a></li><li><a href='/shop/orders/items?page=4'>4</a></li><li class=\"active\"><a>5</a></li><li><a href='/shop/orders/items?page=6'>6</a></li><li><a href='/shop/orders/items?page=7'>7</a></li>")]
    public void Pagination_PreservesPhpMissingLanguageScopeQuirk(long rows, long page, string expected)
        => Assert.Equal(expected, StorefrontMyOrdersItems.Pagination(rows, 1, page, "shop/orders/items"));

    [Fact]
    public void DedicatedRazorPage_OwnsOnlyCanonicalItemRoutes()
    {
        var root = FindRepo();
        var dedicated = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontMyOrdersItemsApp.razor"));
        var old = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOrdersApp.razor"));
        Assert.Contains("@page \"/en/shop/orders/items\"", dedicated, StringComparison.Ordinal);
        Assert.Contains("@page \"/shop/orders/items\"", dedicated, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/en/shop/orders/items\"", old, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/shop/orders/items\"", old, StringComparison.Ordinal);
        Assert.Contains("@page \"/storefront/orders-app\"", old, StringComparison.Ordinal);
    }

    private static StorefrontMyOrdersItems.Input Input(JsonElement c)
    {
        var get = c.GetProperty("get");
        var cookies = c.GetProperty("cookies");
        string? Value(JsonElement source, string key) => source.TryGetProperty(key, out var value) ? value.GetString() : null;
        return new(
            c.GetProperty("user_id").GetInt64(),
            c.TryGetProperty("lang_href", out var lang) ? lang.GetString()! : "/en",
            "shop/orders/items",
            int.Parse(c.GetProperty("config").GetProperty("list_page_limit").GetString()!, CultureInfo.InvariantCulture),
            Value(get, "page"),
            Value(cookies, StorefrontMyOrdersItems.FilterCookie),
            Value(cookies, StorefrontMyOrdersItems.SortCookie),
            "[[LOGIN_FORM:" + StorefrontMyOrdersItems.LoginFormPostfix + "|]]");
    }

    private static async Task WithDatabaseAsync(string password, JsonElement spec, JsonElement c, Func<MySqlConnection, Task> body)
    {
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";");
            await connection.OpenAsync();
            var sql = spec.GetProperty("schema").EnumerateArray().Concat(spec.GetProperty("base").EnumerateArray());
            if (c.TryGetProperty("sql", out var extra))
            {
                sql = sql.Concat(extra.EnumerateArray());
            }

            foreach (var statement in sql.Select(e => e.GetString()!))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = statement;
                await command.ExecuteNonQueryAsync();
            }

            await body(connection);
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
    }

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }

    private static string Difference(string name, string expected, string actual)
    {
        var at = 0;
        while (at < expected.Length && at < actual.Length && expected[at] == actual[at])
        {
            at++;
        }

        var start = Math.Max(0, at - 120);
        return $"{name} differs at byte {at}; expected length {expected.Length}, actual {actual.Length}\n"
            + "EXPECTED: " + expected[start..Math.Min(expected.Length, at + 240)].Replace("\n", "\\n", StringComparison.Ordinal)
            + "\nACTUAL: " + actual[start..Math.Min(actual.Length, at + 240)].Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
