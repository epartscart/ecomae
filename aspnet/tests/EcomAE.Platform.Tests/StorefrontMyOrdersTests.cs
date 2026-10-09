using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/MyOrders/harness.php</c> ran PHP <c>my_orders.php</c> with the real <c>orders_background.php</c> per case
/// on throwaway MariaDB databases, with a marker in place of the general login form; ASP.NET must render the same bytes.
/// </summary>
public sealed class StorefrontMyOrdersTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MyOrders");

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
        Assert.Equal(26, cases.Count);
        Assert.Equal(cases.Count, results.Count);
        for (var i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            Assert.Equal(c.GetProperty("name").GetString(), results[i].GetProperty("name").GetString());
            await WithDatabaseAsync(password, spec, [], async connection =>
            {
                var get = c.GetProperty("get");
                var cookies = c.GetProperty("cookies");
                string? Get(string key) => get.TryGetProperty(key, out var v) ? v.GetString() : null;
                string? Cookie(string key) => cookies.TryGetProperty(key, out var v) ? v.GetString() : null;
                var userId = c.GetProperty("user_id").GetInt64();
                var input = new StorefrontMyOrders.Input(
                    userId,
                    c.TryGetProperty("lang_href", out var lh) ? lh.GetString()! : "/en",
                    "shop/orders",
                    int.Parse(c.GetProperty("config").GetProperty("list_page_limit").GetString()!, CultureInfo.InvariantCulture),
                    Get("page"),
                    Get("garage"),
                    Get("read"),
                    Cookie(StorefrontMyOrders.FilterCookie),
                    Cookie(StorefrontMyOrders.SortCookie),
                    "[[LOGIN_FORM:" + StorefrontMyOrders.LoginFormPostfix + "|" + StorefrontMyOrders.LoginFormTarget + "]]");
                var html = await StorefrontMyOrders.RenderAsync(
                    connection,
                    input,
                    key => Task.FromResult("{" + key + "}"),
                    unix => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
                        + "<br><small>" + DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("H:mm", CultureInfo.InvariantCulture) + "</small>",
                    CancellationToken.None);
                Assert.True(
                    results[i].GetProperty("html").GetString() == html,
                    c.GetProperty("name").GetString() + ":\n--- PHP ---\n" + results[i].GetProperty("html").GetString() + "\n--- ASP.NET ---\n" + html);
            });
        }
    }

    [Fact]
    public async Task HostileCookieValues_GarageCaptions_AndLineTexts_AreEscaped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        string[] extra =
        [
            "UPDATE `shop_docpart_garage` SET `caption` = '<img src=x onerror=alert(1)>' WHERE `id` = 1",
            "UPDATE `shop_orders_items` SET `t2_name` = '<script>alert(2)</script>', `t2_article` = 'A&B' WHERE `order_id` = 101",
        ];
        await WithDatabaseAsync(password, spec, extra, async connection =>
        {
            var filter = "{\"time_from\":\"\\\"><script>alert(3)</script>\",\"time_to\":\"\",\"order_id\":\"101<b>\",\"status\":\"0\",\"paid\":\"-1\",\"paid_type\":\"-1\",\"garage\":\"0\"}";
            var input = new StorefrontMyOrders.Input(7, "/en", "shop/orders", 20, null, null, null, filter, null, string.Empty);
            var html = await StorefrontMyOrders.RenderAsync(connection, input, key => Task.FromResult("{" + key + "}"), _ => "d", CancellationToken.None);
            Assert.Contains("id=\"time_from\" value=\"&quot;&gt;&lt;script&gt;alert(3)&lt;/script&gt;\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"order_id\" value=\"101&lt;b&gt;\"", html, StringComparison.Ordinal);
            Assert.Contains("<option value=\"1\">&lt;img src=x onerror=alert(1)&gt;</option>", html, StringComparison.Ordinal);
            Assert.Contains("title=\"&lt;img src=x onerror=alert(1)&gt;\"", html, StringComparison.Ordinal);
            Assert.Contains("&lt;script&gt;alert(2)&lt;/script&gt;", html, StringComparison.Ordinal);
            Assert.Contains(">A&amp;B</td>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<img src=x", html, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("a=1; my_orders_filter=%7B%22status%22%3A%221%22%7D", "{\"status\":\"1\"}")]
    [InlineData("my_orders_filter={\"time_from\":\"\",\"status\":0}; other=x", "{\"time_from\":\"\",\"status\":0}")]
    [InlineData("my_orders_filter=first; my_orders_filter=second", "first")]
    [InlineData("my_orders_filter=a+b", "a b")]
    [InlineData("my_orders_filter", "")]
    [InlineData("x=1", null)]
    [InlineData(null, null)]
    public void PhpCookie_DecodesLikePhp_AndKeepsTheFirstValue(string? header, string? expected)
        => Assert.Equal(expected, StorefrontMyOrders.PhpCookie(header, StorefrontMyOrders.FilterCookie));

    [Fact]
    public void Pagination_MatchesPhpForEveryPageOfSeven()
    {
        Assert.Equal(
            "<li class=\"active\"><a>1</a></li><li><a href='/shop/orders?page=2'>2</a></li><li><a href='/shop/orders?page=3'>3</a></li><li><a href='/shop/orders?page=4' >...</a></li><li><a href='/shop/orders?page=7' >7</a></li>",
            StorefrontMyOrders.Pagination(7, 1, 2, 1, "active", "shop/orders"));
        Assert.Equal(
            "<li><a href='/shop/orders'>1</a></li><li><a href='/shop/orders?page=2' >...</a></li><li><a href='/shop/orders?page=3'>3</a></li><li><a href='/shop/orders?page=4'>4</a></li><li class=\"active\"><a>5</a></li><li><a href='/shop/orders?page=6'>6</a></li><li><a href='/shop/orders?page=7'>7</a></li>",
            StorefrontMyOrders.Pagination(7, 1, 2, 5, "active", "shop/orders"));
    }

    private static async Task WithDatabaseAsync(string password, JsonElement spec, string[] extra, Func<MySqlConnection, Task> body)
    {
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";");
            await connection.OpenAsync();
            foreach (var sql in spec.GetProperty("schema").EnumerateArray().Concat(spec.GetProperty("base").EnumerateArray()).Select(e => e.GetString()!).Concat(extra))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }

            await body(connection);
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
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
