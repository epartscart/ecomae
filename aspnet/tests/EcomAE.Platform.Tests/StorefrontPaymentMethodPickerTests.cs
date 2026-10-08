using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/PaymentPicker/harness.php</c> rendered PHP <c>epc_payment_method_picker.php</c> on <c>fixture.sql</c>
/// (PHP 8.3, real <c>lang/dp_lang.php</c>); ASP.NET must echo the same bytes.
/// </summary>
public sealed class StorefrontPaymentMethodPickerTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PaymentPicker");

    [Fact]
    public void Render_WithoutMethods_MatchesPhpGoldenStructure()
    {
        var html = StorefrontPaymentMethodPicker.Render([]);
        Assert.Contains("<select id=\"epc_pay_handler\" class=\"form-control\" style=\"max-width:420px;\">\n\t\t\t</select>\n", html, StringComparison.Ordinal);
        Assert.Contains("window.EPC_PAY_HANDLERS = [];\n", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Label_AddsCryptoSuffixAndRegionCaption()
    {
        Assert.Equal("NOW · Crypto (Cryptocurrency)", StorefrontPaymentMethodPicker.Label(new(1, "nowpayments", "NOW", 0, "crypto")));
        Assert.Equal(" (Legacy (CIS))", StorefrontPaymentMethodPicker.Label(new(2, "robokassa", null, 0, "legacy")));
        Assert.Equal("X (odd)", StorefrontPaymentMethodPicker.Label(new(3, "x", "X", 0, "odd")));
    }

    [Fact]
    public async Task Picker_MatchesPhpGoldens_OnThrowawayDatabase_ThenDropped()
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
        try
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await using (var load = connection.CreateCommand())
            {
                load.CommandText = await File.ReadAllTextAsync(Path.Combine(Fixtures, "fixture.sql"));
                await load.ExecuteNonQueryAsync();
            }

            var methods = await StorefrontPaymentMethodPicker.ListSelectableAsync(connection, new StorefrontPhpTranslator(connection, "en"), CancellationToken.None);
            Assert.Equal(["stripe", "robokassa", "nowpayments", "jazzcash", "paypal", "tamara"], methods.Select(m => m.Handler));
            Assert.Equal(await File.ReadAllTextAsync(Path.Combine(Fixtures, "picker_full.html")), StorefrontPaymentMethodPicker.Render(methods));

            await using (var disable = connection.CreateCommand())
            {
                disable.CommandText = "UPDATE shop_payment_systems SET anable = 0";
                await disable.ExecuteNonQueryAsync();
            }

            var empty = await StorefrontPaymentMethodPicker.ListSelectableAsync(connection, new StorefrontPhpTranslator(connection, "en"), CancellationToken.None);
            Assert.Equal(await File.ReadAllTextAsync(Path.Combine(Fixtures, "picker_empty.html")), StorefrontPaymentMethodPicker.Render(empty));
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
