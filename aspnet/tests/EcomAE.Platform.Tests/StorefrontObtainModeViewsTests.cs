using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/ObtainModes/harness.php</c> rendered the PHP <c>content/shop/obtaining_modes</c> includes on
/// <c>fixture.sql</c> (PHP 8.3, real <c>lang/dp_lang.php</c>, one include per process); ASP.NET must echo the same bytes.
/// </summary>
public sealed class StorefrontObtainModeViewsTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ObtainModes");

    public static TheoryData<string, string, string, string, long> Cases => new()
    {
        { "office_details", "get_in_office", "details", "{\"mode\":1,\"office_id\":1}", 40 },
        { "office_missing", "get_in_office", "details", "{\"mode\":1,\"office_id\":99}", 40 },
        { "office_manager", "get_in_office", "manager", "{\"mode\":1,\"office_id\":\"1\"}", 40 },
        { "carriers_details", "epc_carriers", "details", "{\"mode\":2,\"carrier\":\"aramex\",\"service\":\"PPX\",\"city\":\"Dubai & Co\",\"country\":\"AE\",\"address\":\"<b>Warehouse</b> 7\",\"phone\":\"+971 50\",\"weight_kg\":2.5}", 40 },
        { "carriers_details_price", "epc_carriers", "details", "{\"mode\":2,\"carrier\":\"zzz\",\"service\":\"S1\",\"delivery_price\":\"12.5\",\"rate\":99,\"country\":\"SA\",\"weight_kg\":\"3\"}", 40 },
        { "carriers_details_rate", "epc_carriers", "details", "{\"mode\":2,\"carrier\":\"DHL\",\"service\":\"EXPRESS\",\"rate\":1234.567,\"weight_kg\":1.0}", 40 },
        { "carriers_details_demo", "epc_carriers", "details", "{\"mode\":2,\"country\":\"sa\"}", 40 },
        { "carriers_manager_form", "epc_carriers", "manager", "{\"mode\":2,\"carrier\":\"fedex\",\"city\":\"Sharjah\",\"country\":\"AE\",\"address\":\"A \\\"1\\\"\",\"phone\":\"06\",\"weight_kg\":4}", 40 },
        { "carriers_manager_shipments", "epc_carriers", "manager", "{\"mode\":2,\"carrier\":\"nope\"}", 41 },
    };

    [Fact]
    public async Task ManagerInterface_UnknownHandler_IsOrderCardWarning()
    {
        var html = await StorefrontObtainModeViews.ManagerInterfaceAsync(null!, null!, "cdek<x>", "c", "{}", 1, "cp", "en", CancellationToken.None);
        Assert.Equal("<div class=\"alert alert-warning\">Delivery mode panel unavailable for handler cdek&lt;x&gt;.</div>", html);
        Assert.Equal(string.Empty, await StorefrontObtainModeViews.ShowDetailsAsync(null!, null!, "cdek", "c", "{}", "en", CancellationToken.None));
    }

    [Theory]
    [InlineData("dhl", 1, "AE", 53.5)]
    [InlineData("dhl", 1, "sa", 72.23)]
    [InlineData("nope", 0, "", 35.85)]
    [InlineData("DHL", 2, "AE", 52)]
    public void DemoRate_MatchesPhpEpcChannelDemoRate(string carrier, double weight, string country, double expected)
        => Assert.Equal(expected, StorefrontObtainModeViews.DemoRate(carrier, weight, country));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Include_MatchesPhpGolden_OnThrowawayDatabase_ThenDropped(string name, string handler, string include, string howGet, long orderId)
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

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await using (var load = connection.CreateCommand())
            {
                load.CommandText = await File.ReadAllTextAsync(Path.Combine(Fixtures, "fixture.sql"));
                await load.ExecuteNonQueryAsync();
            }

            var translator = new StorefrontPhpTranslator(connection, "en");
            var caption = handler == "get_in_office" ? "om_office" : "om_carriers";
            var html = include == "details"
                ? await StorefrontObtainModeViews.ShowDetailsAsync(connection, translator, handler, caption, howGet, "en", CancellationToken.None)
                : await StorefrontObtainModeViews.ManagerInterfaceAsync(connection, translator, handler, caption, howGet, orderId, "cp", "en", CancellationToken.None);
            Assert.Equal(await File.ReadAllTextAsync(Path.Combine(Fixtures, name + ".html")), html);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await using var adminConnection = new MySqlConnection(admin);
            await adminConnection.OpenAsync();
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
