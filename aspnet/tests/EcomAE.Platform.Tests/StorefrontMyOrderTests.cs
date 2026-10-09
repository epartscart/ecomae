using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/MyOrder/harness.php</c> ran PHP 8.3 <c>my_order.php</c> on isolated MariaDB databases.
/// The renderer must produce the same bytes except for separately asserted escaping of legacy raw values.
/// </summary>
public sealed class StorefrontMyOrderTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MyOrder");

    [Fact]
    public async Task PhpGoldens_ExerciseEveryBalanceAvailabilityBranch()
    {
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var results = golden.GetProperty("results").EnumerateArray().ToDictionary(
            row => row.GetProperty("name").GetString()!,
            row => row.GetProperty("html").GetString()!);

        Assert.Contains("onclick=\"add_payment_to_order(0);\"", results["balance_nonpartial_sufficient"], StringComparison.Ordinal);
        Assert.Contains("id=\"optionsRadios1\"", results["balance_partial_limit_below_debt"], StringComparison.Ordinal);
        Assert.Contains("{4532} 40.00)", results["balance_partial_limit_below_debt"], StringComparison.Ordinal);
        Assert.Contains("if( pay_value > 40.00 )", results["balance_partial_limit_below_debt"], StringComparison.Ordinal);
        Assert.Contains("onclick=\"add_payment_to_order(0);\"", results["balance_unlimited_overdraft_negative"], StringComparison.Ordinal);
        Assert.Contains("confirm('{4544} -72.05. {4545}')", results["balance_unlimited_overdraft_negative"], StringComparison.Ordinal);
        Assert.Contains("{4532} 35)", results["balance_finite_overdraft_allowed"], StringComparison.Ordinal);
        Assert.Contains("if( pay_value > 35 )", results["balance_finite_overdraft_allowed"], StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"optionsRadios1\"", results["balance_finite_overdraft_denied"], StringComparison.Ordinal);
        Assert.Contains("if( pay_value > 0 )", results["balance_finite_overdraft_denied"], StringComparison.Ordinal);
        Assert.Contains("onclick=\"add_payment_to_order(0);\"", results["balance_wholesaler_office_scoped"], StringComparison.Ordinal);
        Assert.Contains("if( pay_value > 70.00 )", results["balance_wholesaler_office_scoped"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Page_MatchesPhpGolden_AndMarksOnlyOwnedOrderMessagesRead()
    {
        var password = Password();
        if (password is null)
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        Assert.Equal("UTC", golden.GetProperty("timezone").GetString());
        var cases = spec.GetProperty("cases").EnumerateArray().ToArray();
        var results = golden.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(13, cases.Length);
        Assert.Equal(cases.Length, results.Length);
        for (var i = 0; i < cases.Length; i++)
        {
            var c = cases[i];
            var caseSql = c.TryGetProperty("sql", out var sql)
                ? sql.EnumerateArray().Select(value => value.GetString()!).ToArray()
                : [];
            await WithDatabaseAsync(password, spec, caseSql, async connection =>
            {
                var get = c.GetProperty("get");
                string? Query(string key) => get.TryGetProperty(key, out var value) ? value.GetString() : null;
                var config = c.GetProperty("config").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
                var alerts = StorefrontActionsAlert.Render(Query, id => "{" + id + "}");
                var input = new StorefrontMyOrder.Input(
                    c.GetProperty("user_id").GetInt64(),
                    Query("order_id"),
                    c.TryGetProperty("lang_href", out var lang) ? lang.GetString()! : "/en",
                    c.TryGetProperty("csrf", out var csrf) ? csrf.GetString()! : string.Empty,
                    config,
                    null,
                    null,
                    "[[PAY_PICKER]]",
                    alerts);
                var html = await StorefrontMyOrder.RenderAsync(
                    connection,
                    input,
                    key => Task.FromResult("{" + key + "}"),
                    unix => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("dd.MM.yyyy H:mm", CultureInfo.InvariantCulture),
                    CancellationToken.None);
                var expected = results[i].GetProperty("html").GetString();
                Assert.True(expected == html, c.GetProperty("name").GetString() + ":\n--- PHP ---\n" + expected + "\n--- ASP.NET ---\n" + html);
                Assert.Equal(results[i].GetProperty("unread_after").GetInt64(), await ScalarAsync(connection, "SELECT COUNT(*) FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0"));
            });
        }
    }

    [Fact]
    public async Task LegacyRawDatabaseValues_AreEscaped()
    {
        var password = Password();
        if (password is null)
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        string[] extra =
        [
            "UPDATE `shop_docpart_garage` SET `caption`='<script>alert(1)</script>', `vin`='V&<x>' WHERE `id`=1",
            "UPDATE `shop_orders_items` SET `t2_name`='<img src=x onerror=alert(2)>', `t2_article`='A&B', `t2_manufacturer`='<b>X</b>' WHERE `id`=101",
        ];
        await WithDatabaseAsync(password, spec, extra, async connection =>
        {
            var input = new StorefrontMyOrder.Input(7, "10", "/en", "csrf-7",
                new Dictionary<string, string> { ["shop_currency"] = "784", ["currency_show_mode"] = "sign_before", ["return_available"] = "1", ["order_pay_on_place"] = "1" },
                null, null, "[[PAY_PICKER]]", StorefrontActionsAlert.Render(_ => null, id => "{" + id + "}"));
            var html = await StorefrontMyOrder.RenderAsync(connection, input, key => Task.FromResult("{" + key + "}"), _ => "date", CancellationToken.None);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
            Assert.Contains("V&amp;&lt;x&gt;", html, StringComparison.Ordinal);
            Assert.Contains("&lt;img src=x onerror=alert(2)&gt;", html, StringComparison.Ordinal);
            Assert.Contains("A&amp;B", html, StringComparison.Ordinal);
            Assert.Contains("&lt;b&gt;X&lt;/b&gt;", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<script>alert(1)", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<img src=x", html, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task JavaScriptStringContexts_EscapeSessionPathAndTranslatedValues()
    {
        var password = Password();
        if (password is null)
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        await WithDatabaseAsync(password, spec, [], async connection =>
        {
            const string hostile = "x\"'</script><b>";
            var input = new StorefrontMyOrder.Input(
                7,
                "10",
                "/en" + hostile,
                hostile,
                new Dictionary<string, string> { ["shop_currency"] = "784", ["currency_show_mode"] = "sign_before", ["return_available"] = "1" },
                null,
                null,
                "[[PAY_PICKER]]",
                string.Empty);
            var html = await StorefrontMyOrder.RenderAsync(
                connection,
                input,
                key => Task.FromResult(key == "4263" ? hostile : "{" + key + "}"),
                _ => "date",
                CancellationToken.None);

            Assert.Contains("value=\"x&quot;&#039;&lt;/script&gt;&lt;b&gt;\"", html, StringComparison.Ordinal);
            Assert.Contains(@"x\u0022\u0027\u003C/script\u003E\u003Cb\u003E", html, StringComparison.Ordinal);
            Assert.DoesNotContain("x\"'</script><b>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("confirm(\"x\"'</script>", html, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task PayOnPlace_RequiresCsrfAndOwnedSignedInOrder()
    {
        var password = Password();
        if (password is null)
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        string[] extra =
        [
            "CREATE TABLE `sessions` (`id` int NOT NULL AUTO_INCREMENT, `session` varchar(64) NOT NULL, `user_id` int NOT NULL, `type` int NOT NULL DEFAULT 0, `csrf_guard_key` varchar(64) NOT NULL, PRIMARY KEY (`id`))",
            "CREATE TABLE `shop_orders_logs` (`id` int NOT NULL AUTO_INCREMENT, `order_id` int NOT NULL, `time` int NOT NULL, `user_id` int NOT NULL, `is_manager` tinyint NOT NULL, `text` text NOT NULL, `is_robot` tinyint NOT NULL, PRIMARY KEY (`id`))",
            "INSERT INTO `sessions` (`session`,`user_id`,`csrf_guard_key`) VALUES ('customer-token',7,'csrf-7'),('other-token',8,'csrf-8')",
        ];
        await WithDatabaseAsync(password, spec, extra, async connection =>
        {
            var calls = new List<(long, long)>();
            var ok = await StorefrontMyOrder.PostAsync(
                connection,
                Post("customer-token", "7", "csrf-7", "10"),
                key => Task.FromResult("{" + key + "}"),
                (order, status) => { calls.Add((order, status)); return Task.FromResult(true); },
                1700500000,
                CancellationToken.None);
            Assert.Contains("success_message={3630}", ok.Body, StringComparison.Ordinal);
            Assert.Equal(1L, await ScalarAsync(connection, "SELECT `paid_type` FROM `shop_orders` WHERE `id`=10"));
            Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM `shop_orders_logs` WHERE `order_id`=10 AND `user_id`=7"));
            Assert.Equal([(10L, 2L)], calls);

            var other = await StorefrontMyOrder.PostAsync(connection, Post("other-token", "8", "csrf-8", "11"), key => Task.FromResult("{" + key + "}"), (_, _) => Task.FromResult(true), 1700500001, CancellationToken.None);
            Assert.Contains("error_message={2122}: <br/> {2304}.", other.Body, StringComparison.Ordinal);
            Assert.Equal(2L, await ScalarAsync(connection, "SELECT `paid_type` FROM `shop_orders` WHERE `id`=11"));

            var csrf = await StorefrontMyOrder.PostAsync(connection, Post("customer-token", "7", "wrong", "11"), key => Task.FromResult("{" + key + "}"), (_, _) => Task.FromResult(true), 1700500002, CancellationToken.None);
            Assert.Contains("\"status\":false", csrf.Body, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("/shop/orders/order", true)]
    [InlineData("/en/shop/orders/order", true)]
    [InlineData("/en/shop/orders/order/", true)]
    [InlineData("/shop/orders/items", false)]
    [InlineData("/storefront/orders-app", false)]
    public void PostMiddleware_ClaimsOnlyTheParityOrderPage(string path, bool expected)
        => Assert.Equal(expected, StorefrontMyOrderPostMiddleware.IsPagePath(path));

    private static StorefrontMyOrder.PostInput Post(string session, string uid, string csrf, string order)
        => new(new Dictionary<string, string> { ["action"] = "pay_on_place", ["order_id"] = order, ["csrf_guard_key"] = csrf }, session, uid, null, null, null, "/en", "[[ALERTS]]");

    private static string? Password()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        return string.IsNullOrWhiteSpace(password) ? null : password;
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

    private static async Task<long> ScalarAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
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
