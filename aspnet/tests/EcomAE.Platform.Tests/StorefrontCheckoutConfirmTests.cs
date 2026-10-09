using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/CheckoutConfirm/harness.php</c> executes PHP 8.3 <c>checkout_confirm.php</c> on one isolated
/// <c>ecomae_cpw_*</c> schema per case. Except for explicit unsafe DB/JavaScript escaping, C# matches each byte.
/// </summary>
public sealed class StorefrontCheckoutConfirmTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CheckoutConfirm");

    [Fact]
    public async Task Page_MatchesPhp83RuntimeGoldens_IsReadOnly_AndOwnershipGated()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var specDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json")));
        using var goldenDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json")));
        var spec = specDoc.RootElement;
        var cases = spec.GetProperty("cases").EnumerateArray().ToArray();
        var expected = goldenDoc.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Assert.StartsWith("8.3.", goldenDoc.RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);
        Assert.Equal(14, cases.Length);
        Assert.Equal(cases.Length, expected.Length);

        for (var i = 0; i < cases.Length; i++)
        {
            var c = cases[i];
            var name = c.GetProperty("name").GetString()!;
            Assert.Equal(name, expected[i].GetProperty("name").GetString());
            await WithDatabaseAsync(password, spec, c, async connection =>
            {
                var before = await CartSnapshotAsync(connection);
                var result = await StorefrontCheckoutConfirm.RenderAsync(
                    connection,
                    Input(c),
                    key => Task.FromResult("{" + key + "}"),
                    handler => handler == "fixture_handler",
                    (handler, _, json) => Task.FromResult("[[DETAILS:" + handler + ":" + json.Replace("/", "\\/", StringComparison.Ordinal) + "]]"),
                    () => Task.FromResult(c.TryGetProperty("complementary_html", out var related) ? related.GetString()! : string.Empty),
                    CancellationToken.None);

                var expectedRedirect = expected[i].GetProperty("redirect").ValueKind == JsonValueKind.Null
                    ? null
                    : expected[i].GetProperty("redirect").GetString();
                Assert.Equal(expectedRedirect, result.RedirectLocation);
                Assert.Equal(before, await CartSnapshotAsync(connection));
                Assert.Equal(expected[i].GetProperty("cart_before").GetString(), expected[i].GetProperty("cart_after").GetString());

                if (name == "hostile_db_js_and_how_get_values")
                {
                    Assert.DoesNotContain("</script><script>", result.Html, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("<img src=x", result.Html, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("&lt;img src=x onerror=alert(2)&gt;", result.Html, StringComparison.Ordinal);
                    Assert.Contains("\\u003C/script\\u003E", result.Html, StringComparison.Ordinal);
                    Assert.Contains("&csrf_guard_key=key\\'\\u003C/script\\u003E", result.Html, StringComparison.Ordinal);
                    return;
                }

                var comparableHtml = name == "guest_session_ownership_and_validations"
                    ? result.Html.Replace("\\\\", "\\", StringComparison.Ordinal)
                    : result.Html;
                if (name == "guest_session_ownership_and_validations")
                {
                    Assert.Contains("new RegExp('^\\\\+[0-9]{7,15}$')", result.Html, StringComparison.Ordinal);
                    Assert.Contains("new RegExp('^[^@]+@[^@]+\\\\.[^@]+$')", result.Html, StringComparison.Ordinal);
                }
                var bytes = Encoding.UTF8.GetBytes(comparableHtml);
                if (Environment.GetEnvironmentVariable("ECOMAE_CHECKOUT_CONFIRM_GOLDEN_DUMP") is { Length: > 0 } dump)
                {
                    Directory.CreateDirectory(dump);
                    await File.WriteAllTextAsync(Path.Combine(dump, name + ".html"), result.Html);
                }
                Assert.True(
                    expected[i].GetProperty("html_length").GetInt32() == bytes.Length,
                    name + ": expected " + expected[i].GetProperty("html_length").GetInt32() + " bytes, got " + bytes.Length);
                Assert.Equal(
                    expected[i].GetProperty("html_sha256").GetString(),
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());

                if (name == "signed_type1_type2_images_ignored")
                {
                    Assert.DoesNotContain("cat.jpg", result.Html, StringComparison.Ordinal);
                    Assert.DoesNotContain("remote/path.webp", result.Html, StringComparison.Ordinal);
                    Assert.Contains("MCo A-1 Catalogue filter", result.Html, StringComparison.Ordinal);
                    Assert.Contains("Bosch D-1 Docpart item", result.Html, StringComparison.Ordinal);
                    Assert.DoesNotContain("Unchecked item", result.Html, StringComparison.Ordinal);
                }
            });
        }
    }

    [Fact]
    public async Task RuntimeTranslator_ReplacesFixtureTokens()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var specDoc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json")));
        var spec = specDoc.RootElement;
        var c = spec.GetProperty("cases").EnumerateArray().First(x => x.GetProperty("name").GetString() == "signed_type1_type2_images_ignored");
        await WithDatabaseAsync(password, spec, c, async connection =>
        {
            var result = await StorefrontCheckoutConfirm.RenderAsync(
                connection,
                Input(c),
                key => Task.FromResult("[translated:" + key + "]"),
                handler => handler == "fixture_handler",
                (handler, _, json) => Task.FromResult("[[DETAILS:" + handler + ":" + json + "]]"),
                () => Task.FromResult(string.Empty),
                CancellationToken.None);
            Assert.DoesNotMatch(@"\{(?:4506|4507|4508|3550|2751|2752|3251|3503|4509|4510|4521|4293|4751|4752|4753|4754)\}", result.Html);
            Assert.Contains("[translated:4506]", result.Html, StringComparison.Ordinal);
            Assert.Contains("[translated:4754]", result.Html, StringComparison.Ordinal);
            Assert.Contains("[translated:4521]", result.Html, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void RazorRoute_OwnsOnlyCanonicalConfirmRoutes_WithoutCreatingGuestSessions()
    {
        var root = FindRepoRoot();
        var dedicated = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCheckoutConfirmPage.razor"));
        var shared = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCheckoutApp.razor"));
        Assert.Contains("@page \"/en/shop/checkout/confirm\"", dedicated, StringComparison.Ordinal);
        Assert.Contains("@page \"/shop/checkout/confirm\"", dedicated, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/en/shop/checkout/confirm\"", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/shop/checkout/confirm\"", shared, StringComparison.Ordinal);
        Assert.Contains("createIfMissing: false", dedicated, StringComparison.Ordinal);
        Assert.Contains("new StorefrontPhpTranslator", dedicated, StringComparison.Ordinal);
        Assert.Contains("StorefrontCheckoutConfirm.RenderAsync", dedicated, StringComparison.Ordinal);
        Assert.Contains("/content/shop/order_process/ajax_checkout_create.php", File.ReadAllText(
            Path.Combine(root, "aspnet/src/EcomAE.Platform/Storefront/StorefrontCheckoutConfirm.cs")), StringComparison.Ordinal);
        Assert.Contains("@page \"/en/shop/checkout/how_get\"", shared, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/shop/checkout/login_offer\"", shared, StringComparison.Ordinal);
        // checkout_confirm is a compatibility alias, not the canonical checkout/confirm route moved in this cutover.
        // Keep it on the shared compatibility page unless the PHP route map proves both aliases resolve to this script.
        Assert.Contains("@page \"/en/shop/checkout_confirm\"", shared, StringComparison.Ordinal);
        Assert.Contains("@page \"/shop/checkout_confirm\"", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/en/shop/checkout_confirm\"", dedicated, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/shop/checkout_confirm\"", dedicated, StringComparison.Ordinal);

        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        var tenantAt = program.IndexOf("UseMiddleware<TenantResolutionMiddleware>()", StringComparison.Ordinal);
        var interceptAt = program.IndexOf("UseMiddleware<EcomAE.Platform.Storefront.StorefrontCheckoutConfirmSessionlessMiddleware>()", StringComparison.Ordinal);
        var routingAt = program.IndexOf("app.UseRouting()", StringComparison.Ordinal);
        Assert.True(tenantAt >= 0 && interceptAt > tenantAt && routingAt > interceptAt,
            "Sessionless checkout interception must run after tenant resolution and before Razor routing.");
    }

    private static StorefrontCheckoutConfirm.Input Input(JsonElement c)
    {
        var session = c.GetProperty("session");
        var config = c.GetProperty("config");
        var trade = c.GetProperty("trade").GetString();
        return new(
            c.GetProperty("user_id").GetInt32(),
            session.ValueKind == JsonValueKind.Object ? session.GetProperty("id").GetInt64() : 0,
            session.ValueKind == JsonValueKind.Object ? session.GetProperty("csrf_guard_key").GetString()! : string.Empty,
            c.GetProperty("lang_href").GetString()!,
            config.GetProperty("shop_currency").GetString()!,
            config.GetProperty("currency_show_mode").GetString()!,
            c.GetProperty("how_get").GetString()!,
            trade != "approved",
            c.TryGetProperty("trade_message", out var message) ? message.GetString()! : string.Empty);
    }

    private static async Task WithDatabaseAsync(string password, JsonElement spec, JsonElement c, Func<MySqlConnection, Task> body)
    {
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        await ExecAsync(admin, "CREATE DATABASE `" + name + "` CHARACTER SET utf8mb4");
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
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
    }

    private static async Task<string> CartSnapshotAsync(MySqlConnection connection)
    {
        var rows = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,user_id,session_id,price,product_type,count_need,checked_for_order,t2_name,t2_manufacturer,t2_article,IFNULL(image,'') FROM shop_carts ORDER BY id";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join("\u001f", Enumerable.Range(0, reader.FieldCount).Select(i =>
                Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty)));
        }
        return string.Join("\n", rows);
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

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "content/shop/order_process/checkout_confirm.php")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
