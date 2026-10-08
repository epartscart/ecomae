using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/OrderNotAuthorized/harness.php</c> ran PHP <c>my_order_not_authorized.php</c> (GET) with the real
/// <c>orders_background.php</c> and <c>actions_alert.php</c> per case on throwaway MariaDB databases, with a marker in
/// place of the payment method picker; ASP.NET must render the same bytes.
/// </summary>
public sealed class StorefrontOrderNotAuthorizedTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OrderNotAuthorized");

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
        Assert.Equal(10, cases.Count);
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
                foreach (var sql in spec.GetProperty("schema").EnumerateArray().Concat(spec.GetProperty("base").EnumerateArray()))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql.GetString();
                    await command.ExecuteNonQueryAsync();
                }

                var get = c.GetProperty("get");
                string? Query(string key) => get.TryGetProperty(key, out var v) ? v.GetString() : null;
                var config = c.GetProperty("config").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
                var input = new StorefrontOrderNotAuthorized.Input(
                    Query("order_id"),
                    c.TryGetProperty("lang_href", out var lh) ? lh.GetString()! : "/en",
                    c.TryGetProperty("csrf", out var csrf) ? csrf.GetString()! : string.Empty,
                    config,
                    "[[PAY_PICKER]]",
                    StorefrontActionsAlert.Render(Query, id => "{" + id + "}"));
                var html = await StorefrontOrderNotAuthorized.RenderAsync(
                    connection,
                    input,
                    key => Task.FromResult("{" + key + "}"),
                    unix => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("dd.MM.yyyy H:mm", CultureInfo.InvariantCulture),
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

    [Fact]
    public async Task HostileOrderId_IsEscaped_AndTheScriptGetsTheMatchedId()
    {
        await WithDatabaseAsync(async connection =>
        {
            var input = new StorefrontOrderNotAuthorized.Input("10\";alert(1)//<b>", "/en", "k", new Dictionary<string, string> { ["order_pay_on_place"] = "1" }, "", "");
            var html = await StorefrontOrderNotAuthorized.RenderAsync(connection, input, key => Task.FromResult("{" + key + "}"), _ => "d", CancellationToken.None);
            Assert.Contains("<td>10&quot;;alert(1)//&lt;b&gt;</td>", html, StringComparison.Ordinal);
            Assert.Contains("name=\"order_id\" value=\"10&quot;;alert(1)//&lt;b&gt;\"", html, StringComparison.Ordinal);
            Assert.Contains("request_object.order_id = 10;", html, StringComparison.Ordinal);
            Assert.DoesNotContain("alert(1)//<b>", html, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task PayOnPlace_GuestOrder_SetsPaidType_LogsAndRunsTheStatusProtocol()
    {
        await WithDatabaseAsync(async connection =>
        {
            var calls = new List<(long Order, long Status)>();
            var answer = await StorefrontOrderNotAuthorized.PostAsync(
                connection,
                Post(new() { ["action"] = "pay_on_place", ["order_id"] = "10", ["csrf_guard_key"] = "key-guest" }, "guest-token", "0"),
                key => Task.FromResult("{" + key + "}"),
                (order, status) => { calls.Add((order, status)); return Task.FromResult(true); },
                1700500000,
                CancellationToken.None);
            Assert.Equal("[[ALERTS]]\t<script>\n\t\tlocation=\"/en/shop/orders/zakaz-bez-registracii?order_id=10&error_message=&success_message={3630}\";\n\t</script>\n\t", answer.Body);
            Assert.Equal(1L, await ScalarAsync(connection, "SELECT `paid_type` FROM `shop_orders` WHERE `id` = 10"));
            Assert.Equal("{4645}: <b>{Pay on place}</b>", await StringAsync(connection, "SELECT `text` FROM `shop_orders_logs` WHERE `order_id` = 10 AND `time` = 1700500000 AND `user_id` = 0 AND `is_manager` = 0 AND `is_robot` = 0"));
            Assert.Equal([(10L, 2L)], calls);
        });
    }

    [Fact]
    public async Task PayOnPlace_StatusProtocolFailure_ReportsTheWarning()
    {
        await WithDatabaseAsync(async connection =>
        {
            var answer = await StorefrontOrderNotAuthorized.PostAsync(
                connection,
                Post(new() { ["action"] = "pay_on_place", ["order_id"] = "10", ["csrf_guard_key"] = "key-guest" }, "guest-token", "0"),
                key => Task.FromResult("{" + key + "}"),
                (_, _) => Task.FromResult(false),
                1700500000,
                CancellationToken.None);
            Assert.Contains("&error_message={2385} <br/> {4662}.&success_message={3630}\"", answer.Body, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("10", "wrong", "guest-token", "0", "{\"error\":\"Error! CSRF 4\",\"message\":\"Error! CSRF 4\",\"status\":false}")]
    [InlineData("10", "key-41", "user-token", "41", null)]
    [InlineData("12", "key-guest", "guest-token", "0", null)]
    [InlineData("11", "key-guest", "guest-token", "0", null)]
    [InlineData("13", "key-guest", "guest-token", "0", null)]
    [InlineData("14", "key-guest", "guest-token", "0", null)]
    public async Task PayOnPlace_WithoutAValidGuestOrder_WritesNothing(string orderId, string csrf, string session, string uid, string? csrfError)
    {
        await WithDatabaseAsync(async connection =>
        {
            var called = false;
            var answer = await StorefrontOrderNotAuthorized.PostAsync(
                connection,
                Post(new() { ["action"] = "pay_on_place", ["order_id"] = orderId, ["csrf_guard_key"] = csrf }, session, uid),
                key => Task.FromResult("{" + key + "}"),
                (_, _) => { called = true; return Task.FromResult(true); },
                1700500000,
                CancellationToken.None);
            Assert.Equal(
                csrfError is null
                    ? "[[ALERTS]]\t<script>\n\t\tlocation=\"/en/shop/orders/zakaz-bez-registracii?order_id=" + orderId + "&error_message={2122}: <br/> {2304}.&success_message=\";\n\t</script>\n\t"
                    : "[[ALERTS]]" + csrfError,
                answer.Body);
            Assert.False(called);
            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM `shop_orders_logs`"));
            Assert.Equal(5L, await ScalarAsync(connection, "SELECT COUNT(*) FROM `shop_orders` WHERE `paid_type` = (CASE `id` WHEN 11 THEN 2 WHEN 12 THEN 1 ELSE 0 END)"));
        });
    }

    private static StorefrontOrderNotAuthorized.PostInput Post(Dictionary<string, string> form, string session, string uid)
        => new(form, session, uid, null, null, null, "/en", "[[ALERTS]]");

    /// <summary>The fixture schema and data plus <c>sessions</c>, <c>shop_orders_logs</c> and guest order 14 of a missing office.</summary>
    private static async Task WithDatabaseAsync(Func<MySqlConnection, Task> body)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";");
            await connection.OpenAsync();
            var extra = new[]
            {
                "CREATE TABLE `sessions` (`id` int(11) NOT NULL AUTO_INCREMENT, `session` varchar(64) NOT NULL, `user_id` int(11) NOT NULL, `type` int(11) NOT NULL DEFAULT 0, `csrf_guard_key` varchar(64) NOT NULL DEFAULT '', PRIMARY KEY (`id`))",
                "CREATE TABLE `shop_orders_logs` (`id` int(11) NOT NULL AUTO_INCREMENT, `order_id` int(11) NOT NULL, `time` int(11) NOT NULL, `user_id` int(11) NOT NULL DEFAULT 0, `is_manager` tinyint(1) NOT NULL DEFAULT 0, `text` text NOT NULL, `is_robot` tinyint(1) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))",
                "INSERT INTO `sessions` (`session`, `user_id`, `csrf_guard_key`) VALUES ('guest-token', 0, 'key-guest'), ('user-token', 41, 'key-41')",
                "INSERT INTO `shop_orders` (`id`, `user_id`, `time`, `office_id`, `status`, `paid`, `paid_type`, `how_get`) VALUES (14, 0, 1700000000, 7, 1, 0, 0, 1)",
            };
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

    private static async Task<string?> StringAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync() as string;
    }

    [Fact]
    public void HtmlEntities_MatchesPhp()
    {
        Assert.Equal("&amp;amp; &eacute; &mdash; &quot;x&quot; &#039;y&#039; &lt;", PhpHtmlEntities.Encode("&amp; é — \"x\" 'y' <"));
        Assert.Equal("&euro;5 &rarr; &hearts; ж", PhpHtmlEntities.Encode("€5 → ♥ ж"));
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
