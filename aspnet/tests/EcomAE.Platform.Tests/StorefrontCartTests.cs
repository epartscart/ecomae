using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/Cart/harness.php</c> executes PHP 8.3 <c>cart.php</c> on one isolated <c>ecomae_cpw_*</c>
/// schema per case. The compact fixture stores the exact output byte length and SHA-256 plus post-GET cart state.
/// </summary>
public sealed class StorefrontCartTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cart");

    [Fact]
    public async Task Page_MatchesPhp83RuntimeGoldens_AndGetWritesAreOwnershipGated()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var specDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json")));
        using var goldenDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json")));
        var spec = specDoc.RootElement;
        var expected = goldenDoc.RootElement.GetProperty("results").EnumerateArray().ToList();
        var cases = spec.GetProperty("cases").EnumerateArray().ToList();
        Assert.StartsWith("8.3.", goldenDoc.RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);
        Assert.Equal(15, cases.Count);
        Assert.Equal(cases.Count, expected.Count);

        for (var i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            var name = c.GetProperty("name").GetString()!;
            Assert.Equal(name, expected[i].GetProperty("name").GetString());
            await WithDatabaseAsync(password, spec, c, async connection =>
            {
                var html = await StorefrontCart.RenderAsync(connection, Input(c), file => file == "exists.jpg", CancellationToken.None);
                if (name == "hostile_db_and_js_values")
                {
                    Assert.DoesNotContain("</script><script>", html, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("<img src=x", html, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("&lt;script&gt;alert(2)&lt;/script&gt;", html, StringComparison.Ordinal);
                    Assert.Contains("&lt;img src=x onerror=alert(4)&gt;", html, StringComparison.Ordinal);
                    return;
                }

                var bytes = Encoding.UTF8.GetBytes(html);
                if (Environment.GetEnvironmentVariable("ECOMAE_CART_GOLDEN_DUMP") is { Length: > 0 } dump)
                {
                    Directory.CreateDirectory(dump);
                    await File.WriteAllTextAsync(Path.Combine(dump, name + ".html"), html);
                }
                Assert.True(
                    expected[i].GetProperty("html_length").GetInt32() == bytes.Length,
                    name + ": expected " + expected[i].GetProperty("html_length").GetInt32() + " bytes, got " + bytes.Length);
                Assert.True(
                    expected[i].GetProperty("html_sha256").GetString() == Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                    name + ": rendered bytes differ from PHP 8.3 golden");
                var expectedState = expected[i].GetProperty("cart_after").EnumerateArray()
                    .Select(x => (x.GetProperty("id").GetInt32(), x.GetProperty("checked_for_order").GetInt32()))
                    .ToArray();
                var actualState = (await CartStateAsync(connection))
                    .Select(x => (Convert.ToInt32(x["id"], CultureInfo.InvariantCulture), Convert.ToInt32(x["checked_for_order"], CultureInfo.InvariantCulture)))
                    .ToArray();
                Assert.Equal(expectedState, actualState);
            });
        }
    }

    [Fact]
    public async Task InaccessibleWrite_CannotUncheckAnotherShopperRow()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var specDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json")));
        var spec = specDoc.RootElement;
        var c = spec.GetProperty("cases").EnumerateArray().First(x => x.GetProperty("name").GetString() == "signed_cart_all_line_types_and_access_write");
        await WithDatabaseAsync(password, spec, c, async connection =>
        {
            await ExecAsync(connection, "UPDATE shop_carts SET user_id=8 WHERE id=102");
            _ = await StorefrontCart.RenderAsync(connection, Input(c), _ => false, CancellationToken.None);
            Assert.Equal(1L, await ScalarAsync(connection, "SELECT checked_for_order FROM shop_carts WHERE id=102"));
        });
    }

    private static StorefrontCart.Input Input(JsonElement c)
    {
        var session = c.GetProperty("session");
        var config = c.GetProperty("config");
        return new(
            c.GetProperty("user_id").GetInt32(),
            session.ValueKind == JsonValueKind.Object ? session.GetProperty("id").GetInt64() : 0,
            session.ValueKind == JsonValueKind.Object ? session.GetProperty("csrf_guard_key").GetString()! : string.Empty,
            c.GetProperty("blocked").GetBoolean(),
            c.GetProperty("trade").GetString() != "approved",
            c.TryGetProperty("trade_message", out var message)
                ? message.GetString()!
                : c.GetProperty("trade").GetString() == "pending" ? "Awaiting manager approval." : "Trade account blocked.",
            c.GetProperty("lang_href").GetString()!,
            config.GetProperty("product_url").GetString()!,
            "[[PRICE_STYLES]]",
            "[[WA_STYLES]]",
            "[[WA_SCRIPT]]");
    }

    private static async Task WithDatabaseAsync(string password, JsonElement spec, JsonElement c, Func<MySqlConnection, Task> body)
    {
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";");
            await connection.OpenAsync();
            foreach (var sql in spec.GetProperty("schema").EnumerateArray()
                         .Concat(spec.GetProperty("base").EnumerateArray())
                         .Concat(c.TryGetProperty("sql", out var extra) ? extra.EnumerateArray() : []))
            {
                await ExecAsync(connection, sql.GetString()!);
            }
            await body(connection);
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
    }

    private static async Task<List<Dictionary<string, object>>> CartStateAsync(MySqlConnection connection)
    {
        var rows = new List<Dictionary<string, object>>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,checked_for_order FROM shop_carts ORDER BY id";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new() { ["id"] = reader.GetInt32(0), ["checked_for_order"] = reader.GetInt32(1) });
        }
        return rows;
    }

    private static async Task<long> ScalarAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task ExecAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await ExecAsync(connection, sql);
    }
}
