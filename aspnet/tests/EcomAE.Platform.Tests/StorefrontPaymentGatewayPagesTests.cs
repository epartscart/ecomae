using System.Data.Common;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.WebUtilities;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The PHP gateway pages against goldens recorded by <c>Fixtures/PaymentGateways/harness.sh</c> (php -S serving the real
/// <c>content/shop/finance/payment_systems/</c> files over the same fixture), byte-for-byte with status, type and Location.
/// </summary>
public sealed class StorefrontPaymentGatewayPagesTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PaymentGateways");

    private static readonly Dictionary<string, string> Config = new(StringComparer.Ordinal)
    {
        ["domain_path"] = "https://www.epartscart.com/",
        ["backend_dir"] = "cp",
    };

    [Theory]
    [InlineData("g2p_stripe_order", "stripe", "operation=40&csrf_guard_key=ck")]
    [InlineData("g2p_stripe_topup", "stripe", "operation=41&csrf_guard_key=ck")]
    [InlineData("g2p_paypal_live", "paypal", "operation=40&csrf_guard_key=ck")]
    [InlineData("g2p_nowpayments", "nowpayments", "operation=40&csrf_guard_key=ck")]
    [InlineData("g2p_code2", "stripe", "operation=42&csrf_guard_key=ck")]
    [InlineData("g2p_csrf1", "stripe", "operation=40")]
    [InlineData("g2p_csrf4", "stripe", "operation=40&csrf_guard_key=bad")]
    [InlineData("g2p_csrf31", "stripe", "operation=40&csrf_guard_key=ck", "nope")]
    [InlineData("g2p_no_handler", "", "")]
    public Task GoToPay_MatchesPhpGolden_OnThrowawayDatabase_ThenDropped(string golden, string handler, string query, string session = "cs")
        => WithDatabaseAsync(async connection =>
        {
            var request = Request(query, string.Empty, session);
            var payload = await StorefrontPhpAjax.GoToPayAsync(connection, handler, request, CancellationToken.None);
            AssertGolden(golden, payload);
        });

    [Theory]
    [InlineData("pay_entry", true, "EPC_PAY_HANDLER=amazon_ps&operation_id=40&sum=1234.5&operation_description=Pay+%3Corder%3E+%22x%22&currency=AED&user_id=5")]
    [InlineData("pay_demo_entry", true, "EPC_PAY_HANDLER=amazon_ps&operation_id=40&sum=1234.5&operation_description=Pay+%3Corder%3E+%22x%22&currency=AED&user_id=5")]
    [InlineData("pay_success", true, "EPC_PAY_HANDLER=amazon_ps&operation_id=40&sum=1234.5&operation_description=Pay+%3Corder%3E+%22x%22&currency=AED&user_id=5&action=pay_execute&need_result=success")]
    [InlineData("pay_declined", false, "EPC_PAY_HANDLER=amazon_ps&operation_id=40&sum=1234.5&operation_description=Pay+%3Corder%3E+%22x%22&currency=AED&user_id=5&action=pay_execute&need_result=error")]
    [InlineData("pay_entry_zero", true, "EPC_PAY_HANDLER=0&sum=abc")]
    [InlineData("pay_direct_zero", false, "EPC_PAY_HANDLER=0")]
    [InlineData("pay_entry_upper", true, "EPC_PAY_HANDLER=Stripe_X1")]
    public void PayPage_MatchesPhpGolden(string golden, bool entry, string form)
        => AssertGolden(golden, StorefrontPhpAjax.PayPage(Request(string.Empty, form), entry));

    private const string CryptoForm = "EPC_PAY_HANDLER=nowpayments&operation_id=40&sum=150&operation_description=Order+300&currency=aed&user_id=5";

    [Theory]
    [InlineData("crypto_pick", CryptoForm)]
    [InlineData("crypto_btc", CryptoForm + "&action=create_invoice&pay_coin=btc")]
    [InlineData("crypto_btc_small", "EPC_PAY_HANDLER=nowpayments&operation_id=41&sum=1&currency=usd&action=create_invoice&pay_coin=btc")]
    [InlineData("crypto_usdt", CryptoForm + "&action=create_invoice&pay_coin=usdttrc20")]
    [InlineData("crypto_doge", CryptoForm + "&action=create_invoice&pay_coin=doge")]
    [InlineData("crypto_bad_coin", CryptoForm + "&action=create_invoice&pay_coin=eth")]
    [InlineData("crypto_confirm", CryptoForm + "&action=confirm_demo&pay_coin=btc")]
    [InlineData("crypto_live_dummy", "EPC_PAY_HANDLER=paypal&operation_id=40&sum=150&currency=usd&action=create_invoice&pay_coin=btc")]
    [InlineData("crypto_live_pick", "EPC_PAY_HANDLER=paypal&operation_id=40&sum=150&currency=usd")]
    public Task CryptoPayPage_MatchesPhpGolden_OnThrowawayDatabase_ThenDropped(string golden, string form)
        => WithDatabaseAsync(async connection =>
        {
            var payload = await StorefrontPhpAjax.CryptoPayPageAsync(connection, Request(string.Empty, form), (_, _, _, _) => throw new InvalidOperationException("no live call"), CancellationToken.None);
            AssertGolden(golden, payload);
        });

    [Theory]
    [InlineData("notify_already", "stripe", "operation_id=42&sum=99&demo_token=epc-demo-ok")]
    [InlineData("notify_forbidden", "paypal", "operation_id=40&sum=150")]
    [InlineData("notify_no_handler", "", "operation_id=40")]
    public Task Notification_MatchesPhpGolden_OnThrowawayDatabase_ThenDropped(string golden, string handler, string form)
        => WithDatabaseAsync(async connection =>
        {
            var payload = await StorefrontPhpAjax.GatewayNotificationAsync(connection, Payments(), handler, Request(string.Empty, form), CancellationToken.None);
            AssertGolden(golden, payload);
        });

    [Theory]
    [InlineData("ipn_rejected", "{\"payment_status\":\"waiting\",\"order_id\":\"40\",\"price_amount\":150}", true)]
    [InlineData("ipn_already", "{\"payment_id\":7,\"payment_status\":\"finished\",\"order_id\":\"42\",\"price_amount\":99}", true)]
    [InlineData("ipn_bad_sig", "{\"payment_id\":7,\"payment_status\":\"finished\",\"order_id\":\"42\",\"price_amount\":99}", false)]
    public Task NowPaymentsIpn_MatchesPhpGolden_OnThrowawayDatabase_ThenDropped(string golden, string json, bool signed)
        => WithDatabaseAsync(async connection =>
        {
            var signature = signed
                ? Convert.ToHexStringLower(System.Security.Cryptography.HMACSHA512.HashData("sek"u8.ToArray(), System.Text.Encoding.UTF8.GetBytes(json)))
                : "deadbeef";
            var payload = await StorefrontPhpAjax.NowPaymentsNotificationAsync(connection, Payments(), Request(string.Empty, string.Empty), json, signature, CancellationToken.None);
            AssertGolden(golden, payload);
        });

    [Fact]
    public Task CryptoPayPage_LiveNowPayments_PostsThePhpPayloadAndRendersTheInvoice_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async connection =>
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE shop_payment_systems SET parameters_values = '{\"api_key\":\"live-key\",\"sandbox\":true}' WHERE handler = 'paypal'";
                await command.ExecuteNonQueryAsync();
            }

            var calls = new List<(string Url, string Key, string Json)>();
            StorefrontPhpAjax.NowPaymentsPost post = (url, key, json, _) =>
            {
                calls.Add((url, key, json));
                return Task.FromResult((201, "{\"payment_id\":\"5077\",\"pay_address\":\"TXyz\",\"pay_amount\":0.0023077,\"pay_currency\":\"btc\"}"));
            };
            var form = "EPC_PAY_HANDLER=paypal&operation_id=40&sum=150&operation_description=Order+%2F300&currency=usd&action=create_invoice&pay_coin=btc";
            var page = Assert.IsType<StorefrontPhpAjax.RawHttp>(await StorefrontPhpAjax.CryptoPayPageAsync(connection, Request(string.Empty, form), post, CancellationToken.None));
            var call = Assert.Single(calls);
            Assert.Equal(("https://api-sandbox.nowpayments.io/v1/payment", "live-key"), (call.Url, call.Key));
            Assert.Equal(
                "{\"price_amount\":150.0,\"price_currency\":\"usd\",\"pay_currency\":\"btc\",\"order_id\":\"40\",\"order_description\":\"Order \\/300\",\"ipn_callback_url\":\"https:\\/\\/www.epartscart.com\\/content\\/shop\\/finance\\/payment_systems\\/paypal\\/notification.php\",\"is_fixed_rate\":false}",
                call.Json);
            Assert.Contains("<span class=\"badge\">Live crypto</span>", page.Body, StringComparison.Ordinal);
            Assert.Contains("10px;\">0.0023077 BTC</p>", page.Body, StringComparison.Ordinal);
            Assert.Contains("<div class=\"addr\">TXyz</div>", page.Body, StringComparison.Ordinal);
            Assert.Contains("\t\t\t\t\tPayment ID: 5077\t\t\t\t\t\t\t\t\t</p>", page.Body, StringComparison.Ordinal);
            Assert.Contains("href=\"/en/shop/balans\">Back to balance</a>", page.Body, StringComparison.Ordinal);

            post = (_, _, _, _) => Task.FromResult((401, "{\"statusCode\":401,\"message\":\"Invalid api key\"}"));
            page = Assert.IsType<StorefrontPhpAjax.RawHttp>(await StorefrontPhpAjax.CryptoPayPageAsync(connection, Request(string.Empty, form), post, CancellationToken.None));
            Assert.Contains("<div class=\"alert alert-danger\">Invalid api key</div>", page.Body, StringComparison.Ordinal);
            post = (_, _, _, _) => Task.FromResult((502, "<html>"));
            page = Assert.IsType<StorefrontPhpAjax.RawHttp>(await StorefrontPhpAjax.CryptoPayPageAsync(connection, Request(string.Empty, form), post, CancellationToken.None));
            Assert.Contains("<div class=\"alert alert-danger\">NOWPayments HTTP 502</div>", page.Body, StringComparison.Ordinal);
        });

    [Theory]
    [InlineData(150d, "150")]
    [InlineData(12.5d, "12.5")]
    [InlineData(0.1d + 0.2d, "0.3")]
    [InlineData(1d / 65000d, "1.5384615384615E-5")]
    [InlineData(0.0001d, "0.0001")]
    [InlineData(0.00001d, "1.0E-5")]
    [InlineData(1e14d, "1.0E+14")]
    [InlineData(1e13d, "10000000000000")]
    [InlineData(-2.5d, "-2.5")]
    public void PhpFloatString_FollowsPhpPrecision14(double value, string expected)
        => Assert.Equal(expected, StorefrontPhpAjax.PhpFloatString(value));

    [Theory]
    [InlineData("150", 150d)]
    [InlineData(" 12.50abc", 12.5d)]
    [InlineData("abc", 0d)]
    [InlineData("1e3x", 1000d)]
    [InlineData(".5", 0.5d)]
    public void PhpFloatCast_ReadsTheLeadingNumber(string raw, double expected)
        => Assert.Equal(expected, StorefrontPhpAjax.PhpFloatCast(raw));

    private static StorefrontPaymentWriteService Payments() => new(new NoConnections());

    private static StorefrontPhpAjax.GatewayPageRequest Request(string query, string form, string session = "cs")
        => new(Parse(query), Parse(form), session, "5", null, null, string.Empty, "/en", Config);

    private static Dictionary<string, string> Parse(string raw)
        => QueryHelpers.ParseQuery(raw).ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.Ordinal);

    private static void AssertGolden(string golden, object payload)
    {
        var expectedBody = File.ReadAllText(Path.Combine(FixtureDir, golden + ".html"));
        var meta = File.ReadAllText(Path.Combine(FixtureDir, golden + ".meta")).Split('|');
        switch (payload)
        {
            case StorefrontPhpAjax.GatewayRedirect redirect:
                Assert.Equal(("302", meta[2]), ("302", redirect.Location));
                Assert.Equal("302", meta[0]);
                Assert.Equal(string.Empty, expectedBody);
                break;
            case StorefrontPhpAjax.RawHttp raw:
                Assert.Equal(expectedBody, raw.Body);
                Assert.Equal((meta[0], meta[1], string.Empty), (raw.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture), raw.ContentType, meta[2]));
                break;
            default:
                Assert.Fail("Unexpected payload " + payload.GetType().Name);
                break;
        }
    }

    private static async Task WithDatabaseAsync(Func<DbConnection, Task> run)
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
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            foreach (var statement in File.ReadAllText(Path.Combine(FixtureDir, "fixture.sql")).Split(";\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = statement;
                await command.ExecuteNonQueryAsync();
            }

            await run(connection);
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

    private sealed class NoConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("The caller's connection is used.");
    }
}
