using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/Returns/harness.php</c> rendered PHP <c>content/shop/returns</c> (returns.php, return.php with return_messages.php,
/// add_return.php, assets/add_return.js.php) on <c>fixture.sql</c> with PHP 8.3, <c>short_open_tag=1</c> and the real
/// <c>lang/dp_lang.php</c>, one page per process in the order below; ASP.NET must echo the same bytes.
/// </summary>
public sealed class StorefrontReturnsPagesTests
{
    private const string Photo = "/tmp/ecomae_returns_fixture_photo.png";
    private const string MissingPhoto = "/tmp/ecomae_returns_fixture_missing.png";
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Returns");

    [Fact]
    public void ItemIds_KeepsOnlyIntegers_SoTheInListCannotCarrySql()
    {
        Assert.Equal([90L, 91L], StorefrontReturnsPages.ItemIds("[90,\"91\",\"1) OR (1=1\",1.5,null]"));
        Assert.Empty(StorefrontReturnsPages.ItemIds("{\"a\":1}"));
        Assert.Empty(StorefrontReturnsPages.ItemIds("not json"));
        Assert.Empty(StorefrontReturnsPages.ItemIds(null));
    }

    [Fact]
    public void OrderReturnSelection_ChecksLinesThenOpensAddReturnLikeMyOrderPhp()
    {
        var script = EcomAE.Platform.Components.Pages.StorefrontOrdersApp.ReturnSelectionScript(
            "Check \"items\" </script>.", "Not returnable", "csrf-seven", "/en/shop/returns/add_return?items=");

        Assert.Contains("fetch(\"/content/shop/order_process/ajax_check_items_returns.php\"", script, StringComparison.Ordinal);
        Assert.Contains("items_id: items, csrf_guard_key: \"csrf-seven\"", script, StringComparison.Ordinal);
        Assert.Contains("answer.count_confirm > 0 || !answer.all_complete", script, StringComparison.Ordinal);
        Assert.Contains("location = \"/en/shop/returns/add_return?items=\" + JSON.stringify(items)", script, StringComparison.Ordinal);
        Assert.Contains("alert(\"Check \\u0022items\\u0022 \\u003C/script\\u003E.\")", script, StringComparison.Ordinal);
        Assert.Equal(1, script.Split("</script>").Length - 1);
    }

    [Fact]
    public void NumberFormat_GroupsThousandsWithSpacesLikePhp()
    {
        Assert.Equal("1 000 000.25", StorefrontReturnsPages.NumberFormat("1000000.25"));
        Assert.Equal("72.23", StorefrontReturnsPages.NumberFormat("72.225"));
        Assert.Equal("0.00", StorefrontReturnsPages.NumberFormat(null));
        Assert.Equal("-1 234.50", StorefrontReturnsPages.NumberFormat("-1234.5"));
    }

    [Fact]
    public void FormKey_IsBoundToTheSessionAndNeverTheTechKeyItself()
    {
        var key = StorefrontReturnsPages.FormKey("tk-secret", "csrf-seven");
        Assert.Equal(64, key.Length);
        Assert.DoesNotContain("tk-secret", key, StringComparison.Ordinal);
        Assert.Equal(key, StorefrontReturnsPages.FormKey("tk-secret", "csrf-seven"));
        Assert.NotEqual(key, StorefrontReturnsPages.FormKey("tk-secret", "csrf-eight"));
        Assert.Equal(string.Empty, StorefrontReturnsPages.FormKey(string.Empty, "csrf-seven"));
    }

    [Fact]
    public async Task Pages_MatchPhpGoldens_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await using (var adminConnection = new MySqlConnection(admin))
        {
            await adminConnection.OpenAsync();
            await using var create = adminConnection.CreateCommand();
            create.CommandText = "CREATE DATABASE `" + database + "` CHARACTER SET utf8mb4";
            await create.ExecuteNonQueryAsync();
        }

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        await File.WriteAllBytesAsync(Photo, [0x89, .. "PNG\r\n"u8, 0x1a, .. "\nfixture"u8]);
        File.Delete(MissingPhoto);
        try
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await using (var load = connection.CreateCommand())
            {
                load.CommandText = await File.ReadAllTextAsync(Path.Combine(Fixtures, "fixture.sql"));
                await load.ExecuteNonQueryAsync();
            }

            var config = new Dictionary<string, string>
            {
                ["domain_path"] = "https://www.epartscart.com/",
                ["tech_key"] = "tk-secret",
                ["return_available"] = "1",
                ["retention_percentage"] = "10",
                ["retention_percentage_text"] = "ret_note",
            };
            var translator = new StorefrontPhpTranslator(connection, "en");
            var ct = CancellationToken.None;
            var csrf = await StorefrontReturnsPages.SessionCsrfKeyAsync(connection, "tok-7", 7, ct);
            Assert.Equal("csrf-seven", csrf);
            Assert.Equal(string.Empty, await StorefrontReturnsPages.SessionCsrfKeyAsync(connection, "tok-7", 8, ct));

            await Golden("list_guest", StorefrontReturnsPages.ListAsync(connection, translator, 0, null, config, ct));
            await Golden("list", StorefrontReturnsPages.ListAsync(connection, translator, 7, null, config, ct));
            await Golden("list_unread", StorefrontReturnsPages.ListAsync(connection, translator, 7, "0", config, ct));
            await Golden("list_other", StorefrontReturnsPages.ListAsync(connection, translator, 8, null, config, ct));
            await Golden("return_guest", StorefrontReturnsPages.ReturnAsync(connection, translator, 0, "1", string.Empty, config, null, ct));
            await Golden("return_foreign", StorefrontReturnsPages.ReturnAsync(connection, translator, 7, "4", csrf, config, null, ct));
            await Golden("return_pending", StorefrontReturnsPages.ReturnAsync(connection, translator, 7, "1", csrf, config, null, ct));
            await Golden("return_decided", StorefrontReturnsPages.ReturnAsync(connection, translator, 7, "2", csrf, config, null, ct));
            await Golden("list_after_read", StorefrontReturnsPages.ListAsync(connection, translator, 7, "0", config, ct));

            await using (var read = connection.CreateCommand())
            {
                read.CommandText = "SELECT GROUP_CONCAT(id ORDER BY id) FROM shop_orders_messages WHERE `read` = 1";
                Assert.Equal("1,2,4", Convert.ToString(await read.ExecuteScalarAsync()));
            }

            await Golden("add_disabled", StorefrontReturnsPages.AddReturnAsync(connection, translator, 7, "[90]", csrf, "tk-secret", new Dictionary<string, string>(config) { ["return_available"] = "0" }, ct));
            await Golden("add_items", StorefrontReturnsPages.AddReturnAsync(connection, translator, 7, "[90,91,92,93]", csrf, "tk-secret", config, ct));
            await Golden("add_no_retention", StorefrontReturnsPages.AddReturnAsync(
                connection,
                translator,
                7,
                "[\"91\"]",
                csrf,
                "tk-secret",
                new Dictionary<string, string>(config) { ["retention_percentage"] = "0", ["retention_percentage_text"] = "0" },
                ct));
            await Golden("add_foreign", StorefrontReturnsPages.AddReturnAsync(connection, translator, 7, "[90,97]", csrf, "tk-secret", config, ct));
            await Golden("add_none", StorefrontReturnsPages.AddReturnAsync(connection, translator, 7, "[92]", csrf, "tk-secret", config, ct));
            await Golden("add_script", StorefrontReturnsPages.AddReturnScriptAsync(translator, "en/", ct));
        }
        finally
        {
            File.Delete(Photo);
            MySqlConnection.ClearAllPools();
            await using var adminConnection = new MySqlConnection(admin);
            await adminConnection.OpenAsync();
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task Golden(string name, Task<string> render)
        => Assert.Equal(await File.ReadAllTextAsync(Path.Combine(Fixtures, name + ".html")), await render);
}
