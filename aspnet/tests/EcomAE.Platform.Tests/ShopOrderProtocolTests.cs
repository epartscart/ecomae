using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ShopOrderProtocolTests
{
    [Fact]
    public void StatusAllows_GatesSmsAndWhatsappByStatusFlagsLikePhp()
    {
        var on = new Dictionary<string, string> { ["orders_statuses_notifications_settings"] = "1" };
        var off = new Dictionary<string, string>();
        var silent = new Dictionary<string, string> { ["to_manager_sms"] = "0", ["to_customer_sms"] = "2" };

        Assert.Equal((true, true), StorefrontNotifyDispatcher.StatusAllows("order_status_to_manager", silent, off));
        Assert.Equal((false, false), StorefrontNotifyDispatcher.StatusAllows("order_status_to_manager", silent, on));
        Assert.Equal((false, false), StorefrontNotifyDispatcher.StatusAllows("order_item_status_to_manager", silent, on));
        Assert.Equal((true, false), StorefrontNotifyDispatcher.StatusAllows("order_status_to_customer", silent, on));
        Assert.Equal((true, true), StorefrontNotifyDispatcher.StatusAllows("order_status_to_customer", new Dictionary<string, string>(), on));
        Assert.Equal((true, true), StorefrontNotifyDispatcher.StatusAllows("new_order_to_user", silent, on));
    }

    [Fact]
    public void WhatsappLines_CapsAndFormatsLikePhp()
    {
        var items = Enumerable.Range(1, 9)
            .Select(i => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
            {
                ["t2_manufacturer"] = " TOYOTA ",
                ["t2_article"] = "A" + i.ToString(CultureInfo.InvariantCulture),
                ["t2_article_show"] = null!,
                ["t2_name"] = i == 1 ? "Filter" : string.Empty,
                ["count_need"] = i == 2 ? "0" : "3",
            })
            .ToList();
        var lines = ShopOrderProtocolService.WhatsappLines(items, 8).Split('\n');
        Assert.Equal(9, lines.Length);
        Assert.Equal("TOYOTA A1 — Filter ×3", lines[0]);
        Assert.Equal("TOYOTA A2", lines[1]);
        Assert.Equal("…", lines[8]);

        var message = ShopOrderProtocolService.WhatsappStatusMessage("eParts Cart", "https://www.epartscart.com/", 7, "Shipped", items.Take(1).ToList());
        Assert.StartsWith("Hello from eParts Cart.\n\nOrder #7 update: Shipped.\n\nItems:\nTOYOTA A1 — Filter ×3\n\nTrack your order: https://www.epartscart.com/shop/orders", message, StringComparison.Ordinal);
        Assert.Contains("تحديث طلب #7 من eParts Cart: Shipped.", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("20", "20")]
    [InlineData("12.50", "12.5")]
    [InlineData("0.1", "0.1")]
    public void PhpFloat_EchoesLikePhp(string value, string expected)
        => Assert.Equal(expected, ShopOrderProtocolService.PhpFloat(decimal.Parse(value, CultureInfo.InvariantCulture)));

    [Fact]
    public Task Protocol_StatusesRefundsStockAndCascadesLikePhp_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async cs =>
        {
            var notify = new RecordingDispatcher();
            var protocol = new ShopOrderProtocolService(notify, new ConfigMailer());
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            var manager = ShopProtocolActor.Manager(9);

            // Finishing an unpaid order is refused with PHP 5296 / code 101 and changes nothing.
            var refused = await protocol.SetOrderStatusAsync(connection, [200], 3, manager);
            Assert.Equal(new ShopProtocolResult(false, "Order is not paid", 101), refused);
            Assert.Equal("1", await ScalarAsync(cs, "SELECT status FROM shop_orders WHERE id = 200"));
            Assert.Empty(notify.Sent);

            // Cancelling a line of a paid order refunds it to the balance and returns the reserved stock.
            var cancel = await protocol.SetOrderItemStatusAsync(connection, [1001], 12, manager);
            Assert.True(cancel.Status);
            Assert.Equal("12", await ScalarAsync(cs, "SELECT status FROM shop_orders_items WHERE id = 1001"));
            Assert.Equal(
                "5|1|20.00|5|100|1",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', user_id, income, amount, operation_code, order_id, office_id) FROM shop_users_accounting WHERE operation_code = 5"));
            Assert.Equal("1", await ScalarAsync(cs, "SELECT paid FROM shop_orders WHERE id = 100"));
            Assert.Equal("10|0|0", await ScalarAsync(cs, "SELECT CONCAT_WS('|', exist, reserved, issued) FROM shop_storages_data WHERE id = 50"));
            Assert.Equal("0|0|2", await ScalarAsync(cs, "SELECT CONCAT_WS('|', count_reserved, count_issued, count_canceled) FROM shop_orders_items_details WHERE order_item_id = 1001"));
            Assert.Equal(
                "Refunded <b>20.00</b> (to the customer balance)|0|0|1",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', text, user_id, is_manager, is_robot) FROM shop_orders_logs WHERE order_id = 100 ORDER BY id LIMIT 1"));
            Assert.Equal(
                "Line 1001 changed to <b>Line cancelled</b>|9|1|0",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', text, user_id, is_manager, is_robot) FROM shop_orders_logs WHERE order_id = 100 ORDER BY id DESC LIMIT 1"));
            Assert.Equal(["order_item_status_to_manager", "order_item_status_to_customer"], notify.Sent.Select(s => s.Name));
            Assert.Equal(7, Assert.Single(notify.Sent[0].Persons).UserId);
            Assert.Equal(5, Assert.Single(notify.Sent[1].Persons).UserId);
            Assert.Equal("1001", notify.Sent[1].Vars["item_id"]);
            Assert.Equal("Line cancelled", notify.Sent[1].Vars["status_name"]);
            Assert.Equal("0", notify.Sent[0].StatusRef!["to_manager_sms"]);
            Assert.Equal("2", await ScalarAsync(cs, "SELECT status FROM shop_orders WHERE id = 100"));

            // Issuing the last open line of the fully paid order finishes it automatically (robot).
            notify.Sent.Clear();
            Assert.True((await protocol.SetOrderItemStatusAsync(connection, [1002], 11, manager)).Status);
            Assert.Equal("3", await ScalarAsync(cs, "SELECT status FROM shop_orders WHERE id = 100"));
            Assert.Equal(
                ["order_item_status_to_manager", "order_item_status_to_customer", "order_status_to_manager", "order_status_to_customer"],
                notify.Sent.Select(s => s.Name));
            Assert.Equal("Finished", notify.Sent[3].Vars["status_name"]);
            Assert.Equal("100", notify.Sent[3].Vars["order_id"]);
            Assert.False(notify.Sent[3].Vars.ContainsKey("item_id"));
            var tracking = await ScalarAsync(cs, "SELECT text FROM shop_orders_logs WHERE order_id = 100 AND text LIKE 'WhatsApp%'");
            Assert.StartsWith("WhatsApp tracking template ready: Finished — https://wa.me/971501111111?text=", tracking, StringComparison.Ordinal);
            Assert.Contains(Uri.EscapeDataString("TOYOTA 04152 — Oil filter ×2"), tracking, StringComparison.Ordinal);
            Assert.Equal(
                "Status changed to <b>Finished</b>|0|0|1",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', text, user_id, is_manager, is_robot) FROM shop_orders_logs WHERE order_id = 100 ORDER BY id DESC LIMIT 1"));

            // A manager cancelling a guest order cancels its lines (robot), returns stock and notifies the guest directly.
            notify.Sent.Clear();
            Assert.True((await protocol.SetOrderStatusAsync(connection, [200], 4, manager)).Status);
            Assert.Equal("4|12", await ScalarAsync(cs, "SELECT CONCAT_WS('|', o.status, i.status) FROM shop_orders o JOIN shop_orders_items i ON i.order_id = o.id WHERE o.id = 200"));
            Assert.Equal("3|0|0", await ScalarAsync(cs, "SELECT CONCAT_WS('|', exist, reserved, issued) FROM shop_storages_data WHERE id = 60"));
            Assert.Equal("0", await ScalarAsync(cs, "SELECT COUNT(*) FROM shop_users_accounting WHERE order_id = 200"));
            var guest = Assert.Single(notify.Sent.Last().Persons);
            Assert.Equal(StorefrontNotifyPerson.Direct("guest@example.com", "+971 50 999 9999"), guest);
            Assert.Equal("Cancelled", notify.Sent.Last().Vars["status_name"]);
            Assert.Equal(
                "Line 2001 changed to <b>Line cancelled</b>|0|0|1",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', text, user_id, is_manager, is_robot) FROM shop_orders_logs WHERE order_id = 200 AND text LIKE 'Line%'"));
            Assert.StartsWith(
                "WhatsApp tracking template ready: Cancelled — https://wa.me/971509999999?text=",
                await ScalarAsync(cs, "SELECT text FROM shop_orders_logs WHERE order_id = 200 AND text LIKE 'WhatsApp%'"),
                StringComparison.Ordinal);
            Assert.Equal(
                "Status changed to <b>Cancelled</b>|9|1|0",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', text, user_id, is_manager, is_robot) FROM shop_orders_logs WHERE order_id = 200 ORDER BY id DESC LIMIT 1"));

            // Return split: bad requests are refused, a partial count splits the line and the status applies to the new line.
            Assert.Equal(new ShopProtocolResult(false, "Error. Only one line"), await protocol.SetOrderItemStatusAsync(connection, [3001, 1002], 12, manager, 2));
            Assert.Equal(new ShopProtocolResult(false, "Error. Bad count"), await protocol.SetOrderItemStatusAsync(connection, [3001], 12, manager, 9));
            Assert.Equal("1", await ScalarAsync(cs, "SELECT COUNT(*) FROM shop_orders_items WHERE order_id = 300"));

            Assert.True((await protocol.SetOrderItemStatusAsync(connection, [3001], 12, manager, 2)).Status);
            Assert.Equal(
                "3001|10|3|30.00|TOYOTA,3002|12|2|30.00|TOYOTA",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', id, status, count_need, price, t2_manufacturer) ORDER BY id) FROM shop_orders_items WHERE order_id = 300"));
            Assert.Equal(
                "3001|3|0,3002|0|2",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', order_item_id, count_reserved, count_canceled) ORDER BY order_item_id) FROM shop_orders_items_details WHERE order_id = 300"));
            Assert.Equal("2|3|0", await ScalarAsync(cs, "SELECT CONCAT_WS('|', exist, reserved, issued) FROM shop_storages_data WHERE id = 70"));
            Assert.Equal(
                "Split 3002 from  ID 3001. Was 5 now 3",
                await ScalarAsync(cs, "SELECT text FROM shop_orders_logs WHERE order_id = 300 AND text LIKE 'Split%'"));
            Assert.Equal("1", await ScalarAsync(cs, "SELECT status FROM shop_orders WHERE id = 300"));
        });

    [Fact]
    public Task PayOnPlace_RunsTheRobotStatusProtocolLikePhp_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async cs =>
        {
            var notify = new RecordingDispatcher();
            var payments = new StorefrontPaymentWriteService(new WriteConnections(cs), new ShopOrderProtocolService(notify, new ConfigMailer()));

            var result = await payments.PayOnPlaceAsync(5, 300);
            Assert.True(result.Ok);
            Assert.Equal("Pay on place saved.", result.Message);
            Assert.Equal("2|1", await ScalarAsync(cs, "SELECT CONCAT_WS('|', status, paid_type) FROM shop_orders WHERE id = 300"));
            Assert.Equal(["order_status_to_manager", "order_status_to_customer"], notify.Sent.Select(s => s.Name));
            Assert.Equal("Paid", notify.Sent[1].Vars["status_name"]);
            Assert.Equal(
                "Status changed to <b>Paid</b>|0|0|1",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', text, user_id, is_manager, is_robot) FROM shop_orders_logs WHERE order_id = 300 ORDER BY id DESC LIMIT 1"));
            Assert.Equal("already", (await payments.PayOnPlaceAsync(5, 300)).Code);
        });

    [Fact]
    public Task OnlinePayment_RunsPhpPayForOrderSideEffects_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async cs =>
        {
            await ExecuteAsync(cs, "INSERT INTO shop_users_accounting (id, user_id, time, income, amount, operation_code, active, order_id, office_id, pay_orders) VALUES (40, 5, 1, 1, 150, 3, 0, 0, 1, '300')");
            await ExecuteAsync(cs, "INSERT INTO shop_users_accounting (id, user_id, time, income, amount, operation_code, active, order_id, office_id, pay_orders) VALUES (41, 0, 1, 1, 5, 3, 0, 0, 1, '200')");
            var notify = new RecordingDispatcher();
            var mailer = new ConfigMailer();
            var payments = new StorefrontPaymentWriteService(new WriteConnections(cs), new ShopOrderProtocolService(notify, mailer), notify, mailer);

            var result = await payments.NotifyAsync(5, 40, 0, StorefrontPaymentWriteService.DemoToken, "epc_demo");
            Assert.True(result.Ok);
            Assert.Equal("1", await ScalarAsync(cs, "SELECT active FROM shop_users_accounting WHERE id = 40"));
            var payBySite = notify.Sent[0];
            Assert.Equal("pay_by_site", payBySite.Name);
            Assert.Equal(("40", "150.00"), (payBySite.Vars["operation_id"], payBySite.Vars["amount"]));
            Assert.Equal([7, 8], payBySite.Persons.Select(p => p.UserId));
            notify.Sent.RemoveAt(0);
            Assert.Equal("1|3|2", await ScalarAsync(cs, "SELECT CONCAT_WS('|', paid, paid_type, status) FROM shop_orders WHERE id = 300"));
            Assert.Equal(
                [
                    "Payment. Amount <b>150.00</b><br/>Paid status: <b>Fully paid</b>|1",
                    "Payment method: <b>Card online</b>|1",
                    "Status changed to <b>Paid</b>|1",
                ],
                (await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', text, is_robot) ORDER BY id SEPARATOR '~') FROM shop_orders_logs WHERE order_id = 300 AND text NOT LIKE 'WhatsApp%'")).Split('~'));
            Assert.Equal(["order_pay_to_manager", "order_pay_to_customer", "order_status_to_manager", "order_status_to_customer"], notify.Sent.Select(s => s.Name));
            Assert.Equal([7, 8], notify.Sent[0].Persons.Select(p => p.UserId));
            var vars = notify.Sent[1].Vars;
            Assert.Equal(("300", "150.00", "Fully paid", "150.00", "150", "0"), (vars["order_id"], vars["pay_value"], vars["paid"], vars["order_sum"], vars["paid_sum"], vars["paid_left"]));
            Assert.Contains("background: #123456;", vars["order_link"], StringComparison.Ordinal);
            Assert.Contains("href=\"https://www.epartscart.com/shop/orders/order?order_id=300\">Open order</a>", vars["order_link"], StringComparison.Ordinal);
            Assert.Contains("href=\"https://www.epartscart.com/cp/shop/orders/order?order_id=300\"", notify.Sent[0].Vars["order_link"], StringComparison.Ordinal);

            // A partial guest payment notifies the guest directly and leaves the status alone.
            notify.Sent.Clear();
            Assert.True((await payments.NotifyAsync(0, 41, 0, StorefrontPaymentWriteService.DemoToken, "epc_demo")).Ok);
            Assert.Equal("pay_by_site", notify.Sent[0].Name);
            notify.Sent.RemoveAt(0);
            Assert.Equal("2|3|1", await ScalarAsync(cs, "SELECT CONCAT_WS('|', paid, paid_type, status) FROM shop_orders WHERE id = 200"));
            Assert.Equal(["order_pay_to_manager", "order_pay_to_customer"], notify.Sent.Select(s => s.Name));
            Assert.Equal(StorefrontNotifyPerson.Direct("guest@example.com", "+971 50 999 9999"), Assert.Single(notify.Sent[1].Persons));
            Assert.Equal(("Part paid", "12.00", "5", "7"), (notify.Sent[1].Vars["paid"], notify.Sent[1].Vars["order_sum"], notify.Sent[1].Vars["paid_sum"], notify.Sent[1].Vars["paid_left"]));
            Assert.Contains("shop/orders/zakaz-bez-registracii?order_id=200", notify.Sent[1].Vars["order_link"], StringComparison.Ordinal);
        });

    [Fact]
    public Task OnlinePayment_SettlesToVendorThenPlatformAccounts_AndIpnIsIdempotent_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async cs =>
        {
            await ExecuteAsync(cs, "CREATE TABLE shop_payment_systems (id INT NOT NULL PRIMARY KEY, handler VARCHAR(64) NOT NULL, anable TINYINT NOT NULL DEFAULT 0, active TINYINT NOT NULL DEFAULT 0, parameters_values TEXT NULL)");
            await ExecuteAsync(cs, "INSERT INTO shop_payment_systems (id, handler, anable, active, parameters_values) VALUES (1, 'stripe', 1, 0, '{\"currency\":\"USD\",\"demo_mode\":true,\"live\":false}'), (2, 'tap', 0, 1, '{\"currency\":\"SAR\"}')");
            await ExecuteAsync(cs, "CREATE TABLE epc_vendor_accounts (id INT NOT NULL PRIMARY KEY, storage_id INT NOT NULL, status VARCHAR(16) NOT NULL, vendor_full VARCHAR(64) NOT NULL DEFAULT '')");
            await ExecuteAsync(cs, "INSERT INTO epc_vendor_accounts (id, storage_id, status) VALUES (3, 9, 'approved')");
            await ExecuteAsync(cs, "UPDATE shop_orders_items SET t2_storage_id = 9 WHERE id = 3001");
            await ExecuteAsync(cs, "INSERT INTO shop_users_accounting (id, user_id, time, income, amount, operation_code, active, order_id, office_id, pay_orders) VALUES (40, 5, 1, 1, 150, 3, 0, 0, 1, '300')");
            await ExecuteAsync(cs, "INSERT INTO shop_users_accounting (id, user_id, time, income, amount, operation_code, active, order_id, office_id, pay_orders) VALUES (41, 0, 1, 1, 5, 3, 0, 0, 1, '200')");
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await StorefrontPaymentAccounts.EnsureSchemaAsync(connection, CancellationToken.None);
            await ExecuteAsync(cs, "INSERT INTO epc_payment_accounts (id, owner_type, owner_id, handler, platform_fee_pct, status, is_default) VALUES (1, 'platform', 0, 'stripe', 2.5, 'active', 1)");
            await ExecuteAsync(cs, "INSERT INTO epc_payment_accounts (id, owner_type, owner_id, handler, credentials, platform_fee_pct, status) VALUES (2, 'vendor', 3, 'tap', '{\"currency\":\"USD\"}', 10, 'active')");

            var parameters = await StorefrontPaymentAccounts.ParametersAsync(connection, 999, "stripe", false, CancellationToken.None);
            Assert.Equal(new Dictionary<string, string> { ["currency"] = "USD", ["demo_mode"] = "1", ["live"] = "" }, parameters.Values);
            Assert.Null(parameters.Account);
            var resolved = await StorefrontPaymentAccounts.ParametersAsync(connection, 40, "", false, CancellationToken.None);
            Assert.Equal(("2", "tap"), (resolved.Account!["id"], resolved.Handler));
            Assert.Equal(new Dictionary<string, string> { ["currency"] = "USD", ["demo_mode"] = "1" }, resolved.Values);

            var notify = new RecordingDispatcher();
            var mailer = new ConfigMailer();
            var payments = new StorefrontPaymentWriteService(new WriteConnections(cs), new ShopOrderProtocolService(notify, mailer), notify, mailer);
            Assert.True((await payments.NotifyAsync(connection, 5, 40, 0, StorefrontPaymentWriteService.DemoToken, "tap")).Ok);
            Assert.True((await payments.NotifyAsync(connection, 0, 41, 0, StorefrontPaymentWriteService.DemoToken, "stripe")).Ok);
            Assert.Equal(
                "40|300|2|vendor|3|tap|150.00|15.00|135.00|USD|credited|Auto-settlement to vendor account~41|200|1|platform|0|stripe|5.00|0.13|4.87|AED|credited|Payment credited to individual account",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', operation_id, order_id, account_id, owner_type, owner_id, handler, gross_amount, fee_amount, net_amount, currency, status, note) ORDER BY id SEPARATOR '~') FROM epc_payment_settlements"));

            Assert.Equal("already", (await payments.NotifyAsync(connection, 5, 40, 0, StorefrontPaymentWriteService.DemoToken, "tap")).Code);
            var ipn = await payments.ApplyIpnAsync(connection, 40, 150);
            Assert.Equal((true, "already", "already processed"), (ipn.Ok, ipn.Code, ipn.Message));
            Assert.Equal("2", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_payment_settlements"));
        });

    [Fact]
    public Task CreateOperation_ChecksOrderThenPicksGatewayAccountAndOfficeLikePhp_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async cs =>
        {
            await ExecuteAsync(cs, "CREATE TABLE shop_payment_systems (id INT NOT NULL PRIMARY KEY, handler VARCHAR(64) NOT NULL, anable TINYINT NOT NULL DEFAULT 0, active TINYINT NOT NULL DEFAULT 0, parameters_values TEXT NULL)");
            await ExecuteAsync(cs, "INSERT INTO shop_payment_systems (id, handler, anable, active) VALUES (1, 'stripe', 1, 0), (2, 'tap', 0, 1), (3, 'tabby', 1, 0)");
            await ExecuteAsync(cs, "INSERT INTO shop_accounting_codes (id, `key`) VALUES (3, '3_income_by_customer'), (4, '4_income_for_direct_pay')");
            await ExecuteAsync(cs, "CREATE TABLE epc_vendor_accounts (id INT NOT NULL PRIMARY KEY, storage_id INT NOT NULL, status VARCHAR(16) NOT NULL, vendor_full VARCHAR(64) NOT NULL DEFAULT '')");
            await ExecuteAsync(cs, "INSERT INTO epc_vendor_accounts (id, storage_id, status) VALUES (3, 9, 'approved')");
            await ExecuteAsync(cs, "UPDATE shop_orders_items SET t2_storage_id = 9 WHERE id = 3001");
            await ExecuteAsync(cs, "ALTER TABLE shop_offices ADD COLUMN caption VARCHAR(64) NOT NULL DEFAULT '', ADD COLUMN pay_system_id INT NOT NULL DEFAULT 0, ADD COLUMN pay_system_parameters TEXT NULL");
            await ExecuteAsync(cs, "UPDATE shop_offices SET pay_system_id = 3 WHERE id = 1");
            await ExecuteAsync(cs, "CREATE TABLE shop_geo (id INT NOT NULL PRIMARY KEY)");
            await ExecuteAsync(cs, "CREATE TABLE shop_offices_geo_map (geo_id INT NOT NULL, office_id INT NOT NULL)");
            await ExecuteAsync(cs, "INSERT INTO shop_geo (id) VALUES (4)");
            await ExecuteAsync(cs, "INSERT INTO shop_offices_geo_map (geo_id, office_id) VALUES (4, 1)");
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();

            var none = new Dictionary<string, string>();
            async Task<string> Create(int user, string request, Dictionary<string, string>? config = null, string? city = null)
            {
                var answer = await StorefrontPhpAjax.CreateOperationAsync(connection, user, request, CancellationToken.None, config ?? none, city);
                return answer as string ?? JsonSerializer.Serialize(answer, new JsonSerializerOptions { PropertyNamingPolicy = null, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never });
            }

            Assert.Equal(string.Empty, await Create(5, "{\"amount\":null}"));
            Assert.Equal(string.Empty, await Create(5, "{\"order_id\":300}"));
            Assert.Contains("\"user\":false", await Create(5, "{\"amount\":0,\"order_id\":200}"));
            Assert.Contains("\"user\":false", await Create(5, "{\"amount\":10,\"order_id\":999}"));
            Assert.Contains("\"message\":\"Forbidden\"", await Create(0, "{\"amount\":10,\"order_id\":999}"));
            Assert.Contains("\"message\":\"Forbidden\"", await Create(5, "{\"amount\":50,\"order_id\":300}"));
            var partial = new Dictionary<string, string> { ["partial_payment"] = "1", ["partial_payment_min_percent"] = "50" };
            Assert.Contains("\"message\":\"Forbidden\"", await Create(5, "{\"amount\":50,\"order_id\":300}", partial));
            Assert.Contains("\"message\":\"Forbidden\"", await Create(5, "{\"amount\":150.01,\"order_id\":300}", partial));
            Assert.Equal("0", await ScalarAsync(cs, "SELECT COUNT(*) FROM shop_users_accounting WHERE operation_code IN (3, 4)"));

            var officeAccount = await Create(5, "{\"amount\":80,\"order_id\":300}", partial);
            Assert.Matches(
                "^\\{\"result\":true,\"operation\":\\d+,\"pay_system\":\"tabby\",\"payment_account_id\":0,\"payment_account\":\\{\"id\":0,\"title\":\"\",\"owner_type\":\"office\",\"owner_id\":1,\"handler\":\"tabby\"\\}\\}$",
                officeAccount);
            Assert.Equal("80.00|300|1|0", await ScalarAsync(cs, "SELECT CONCAT_WS('|', amount, pay_orders, office_id, epc_payment_account_id) FROM shop_users_accounting WHERE id = " + JsonDocument.Parse(officeAccount).RootElement.GetProperty("operation").GetInt64()));

            await StorefrontPaymentAccounts.EnsureSchemaAsync(connection, CancellationToken.None);
            await ExecuteAsync(cs, "INSERT INTO epc_payment_accounts (id, owner_type, owner_id, title, handler, platform_fee_pct, status, is_default) VALUES (1, 'platform', 0, 'Platform', 'stripe', 2.5, 'active', 1)");
            await ExecuteAsync(cs, "INSERT INTO epc_payment_accounts (id, owner_type, owner_id, title, handler, platform_fee_pct, status) VALUES (2, 'vendor', 3, 'Vendor \"Tap\"', 'tap', 10, 'active')");

            var topUp = await Create(5, "{\"amount\":20}");
            Assert.Matches(
                "^\\{\"result\":true,\"operation\":\\d+,\"pay_system\":\"stripe\",\"payment_account_id\":1,\"payment_account\":\\{\"id\":1,\"title\":\"Platform\",\"owner_type\":\"platform\",\"owner_id\":0,\"handler\":\"stripe\"\\}\\}$",
                topUp);
            Assert.Equal(
                "5|1|20.00|3|0||0|1",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', user_id, income, amount, operation_code, active, pay_orders, office_id, epc_payment_account_id) FROM shop_users_accounting WHERE id = " + JsonDocument.Parse(topUp).RootElement.GetProperty("operation").GetInt64()));

            Assert.Contains("\"pay_system\":\"tap\",\"payment_account_id\":2,", await Create(5, "{\"amount\":150,\"order_id\":300}"));
            var picked = await Create(5, "{\"amount\":150,\"order_id\":\"300\",\"pay_handler\":\"Tabby\"}");
            Assert.Contains("\"pay_system\":\"tap\",\"payment_account_id\":2,", picked);
            picked = await Create(5, "{\"amount\":150,\"order_id\":300,\"pay_handler\":\"tabby\"}");
            Assert.Contains("\"pay_system\":\"tabby\",\"payment_account_id\":2,\"payment_account\":{\"id\":2,\"title\":\"Vendor \\u0022Tap\\u0022\",\"owner_type\":\"vendor\",\"owner_id\":3,\"handler\":\"tap\"}", picked);
            Assert.Equal(
                "5|150.00|4|300|0|2",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', user_id, amount, operation_code, pay_orders, office_id, epc_payment_account_id) FROM shop_users_accounting WHERE id = " + JsonDocument.Parse(picked).RootElement.GetProperty("operation").GetInt64()));

            var wholesaler = new Dictionary<string, string> { ["wholesaler"] = "1" };
            Assert.Equal(string.Empty, await Create(5, "{\"amount\":10,\"office_id\":2}", wholesaler));
            var office = await Create(5, "{\"amount\":10,\"office_id\":\"1\",\"pay_handler\":\"stripe\"}", wholesaler, "4");
            Assert.Contains("\"pay_system\":\"tabby\",\"payment_account_id\":1,", office);
            Assert.Equal("1|1", await ScalarAsync(cs, "SELECT CONCAT_WS('|', office_id, epc_payment_account_id) FROM shop_users_accounting WHERE id = " + JsonDocument.Parse(office).RootElement.GetProperty("operation").GetInt64()));

            await ExecuteAsync(cs, "DELETE FROM epc_payment_accounts");
            var legacyOffice = await Create(0, "{\"amount\":12,\"order_id\":200}");
            Assert.Contains("\"pay_system\":\"tabby\",\"payment_account_id\":0,\"payment_account\":{\"id\":0,\"title\":\"\",\"owner_type\":\"office\",\"owner_id\":1,\"handler\":\"tabby\"}", legacyOffice);
            Assert.Equal("0|200|1|0", await ScalarAsync(cs, "SELECT CONCAT_WS('|', user_id, pay_orders, office_id, epc_payment_account_id) FROM shop_users_accounting WHERE id = " + JsonDocument.Parse(legacyOffice).RootElement.GetProperty("operation").GetInt64()));
        });

    [Fact]
    public Task PayForOrderProtocol_GatesLikePhp_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async cs =>
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            var engine = new ShopPayForOrderService();

            Task<string> Run(string? initiator, string? directPay, string? csrf = null, string? code = null, string session = "cs", string user = "5")
                => PayAsync(connection, engine, new StorefrontPhpAjax.PayForOrderProtocolRequest(initiator, "300", "10", directPay, code, csrf, session, user, null, null, ""), PayConfig());

            Assert.Equal(string.Empty, await Run("2", null));
            Assert.Equal(string.Empty, await Run("4", "0"));
            Assert.Equal(string.Empty, await Run("2", "2"));
            Assert.Equal(string.Empty, await Run("2", ""));
            Assert.Equal("{\"error\":\"Error! CSRF 1\",\"message\":\"Error! CSRF 1\",\"status\":false}", await Run("2", "0"));
            Assert.Equal("{\"error\":\"Error! CSRF 3\",\"message\":\"Error! CSRF 3\",\"status\":false}", await Run("2", "0", ""));
            Assert.Equal("{\"error\":\"Error! CSRF 3.1\",\"message\":\"Error! CSRF 3.1\",\"status\":false}", await Run("2", "0", "ck", session: "nope"));
            Assert.Equal("{\"error\":\"Error! CSRF 4\",\"message\":\"Error! CSRF 4\",\"status\":false}", await Run("2", "0", "bad"));
            Assert.Equal("{\"status\":false,\"message\":\"Forbidden\"}", await Run("3", "0", code: "wrong"));
            Assert.Equal("{\"status\":false,\"message\":\"Forbidden\"}", await Run("2", "0", "ck", user: "8", session: "cs8"));
            Assert.Equal(
                "{\"status\":false,\"message\":\"Forbidden\"}",
                await PayAsync(connection, engine, new StorefrontPhpAjax.PayForOrderProtocolRequest("3", "100", "5", "0", "tech", null, null, null, null, null, null), PayConfig()));
            Assert.Equal(
                "{\"status\":false,\"message\":\"Forbidden\"}",
                await PayAsync(connection, engine, new StorefrontPhpAjax.PayForOrderProtocolRequest("3", "300", "150.01", "0", "tech", null, null, null, null, null, null), PayConfig()));
            Assert.Equal("1", await ScalarAsync(cs, "SELECT COUNT(*) FROM shop_users_accounting"));
            Assert.Equal("0", await ScalarAsync(cs, "SELECT COUNT(*) FROM shop_orders_logs"));
        });

    [Fact]
    public Task PayForOrderProtocol_CustomerPaysFromBalance_WithPartialAndOverdraftRulesAndAdvanceVat_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async cs =>
        {
            await ExecuteAsync(cs, "INSERT INTO shop_users_accounting (user_id, time, income, amount, operation_code, active, order_id, office_id) VALUES (5, 1, 1, 125, NULL, 1, 0, 1)");
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            var notify = new RecordingDispatcher();
            var engine = new ShopPayForOrderService(notify, new ShopOrderProtocolService(notify, new ConfigMailer()), new ErpAdvanceVatService(new ErpGlPostingService(new ErpVoucherNumberService())));

            Task<string> Pay(string sum, Dictionary<string, string> config)
                => PayAsync(connection, engine, new StorefrontPhpAjax.PayForOrderProtocolRequest("2", "300", sum, "0", null, "ck", "cs", "5", null, null, "https://www.epartscart.com/shop/orders/order?order_id=300"), config);

            const string forbidden = "{\"status\":false,\"message\":\"Forbidden\"}";
            Assert.Equal(forbidden, await Pay("50", PayConfig()));
            Assert.Equal(forbidden, await Pay("50", PayConfig(("partial_payment", "1"), ("partial_payment_min_percent", "50"))));
            Assert.Equal(forbidden, await Pay("150", PayConfig()));
            Assert.Equal(forbidden, await Pay("150", PayConfig(("client_overdraft", "1"), ("client_overdraft_value", "20"))));
            Assert.Equal("0", await ScalarAsync(cs, "SELECT COUNT(*) FROM shop_orders_logs"));

            Assert.Equal("{\"status\":true,\"message\":\"\"}", await Pay("80", PayConfig(("partial_payment", "1"), ("partial_payment_min_percent", "50"))));
            Assert.Equal("2|2", await ScalarAsync(cs, "SELECT CONCAT_WS('|', paid, paid_type) FROM shop_orders WHERE id = 300"));
            Assert.Equal(
                "5|0|80.00|1|1|300|1",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', user_id, income, amount, operation_code, active, order_id, office_id) FROM shop_users_accounting WHERE order_id = 300"));
            Assert.Equal(
                "Payment. Amount <b>80</b><br/>Paid status: <b>Part paid</b>|5|0|0~Payment method: <b>From balance</b>|5|0|0",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', text, user_id, is_manager, is_robot) ORDER BY id SEPARATOR '~') FROM shop_orders_logs WHERE order_id = 300"));
            Assert.Equal(
                "300|5|80.00|76.19|3.81|5.00",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', a.order_id, a.user_id, a.payment_amount, a.amount_ex_vat, a.vat_amount, a.vat_rate) FROM epc_uae_vat_advance a JOIN shop_users_accounting l ON l.id = a.ledger_id WHERE l.order_id = 300"));
            Assert.Equal(["order_pay_to_manager", "order_pay_to_customer"], notify.Sent.Select(s => s.Name));
            var vars = notify.Sent[1].Vars;
            Assert.Equal(("300", "80", "Part paid", "150.00", "80", "70"), (vars["order_id"], vars["pay_value"], vars["paid"], vars["order_sum"], vars["paid_sum"], vars["paid_left"]));

            // The rest of the debt is above the balance (20 left): an unlimited overdraft (value 0) lets it through.
            notify.Sent.Clear();
            Assert.Equal("{\"status\":true,\"message\":\"\"}", await Pay("70", PayConfig(("client_overdraft", "1"), ("client_overdraft_value", "0"))));
            Assert.Equal("1|2|2", await ScalarAsync(cs, "SELECT CONCAT_WS('|', paid, paid_type, status) FROM shop_orders WHERE id = 300"));
            Assert.Equal(["order_pay_to_manager", "order_pay_to_customer", "order_status_to_manager", "order_status_to_customer"], notify.Sent.Select(s => s.Name));
            Assert.Equal(forbidden, await Pay("1", PayConfig(("client_overdraft", "1"))));
        });

    [Fact]
    public Task PayForOrderProtocol_ManagerDirectPayment_AddsTheIncomeFirst_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async cs =>
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            var notify = new RecordingDispatcher();
            var engine = new ShopPayForOrderService(notify, new ShopOrderProtocolService(notify, new ConfigMailer()));
            const string cp = "https://www.epartscart.com/cp/shop/orders/order?order_id=200";

            Task<string> Pay(string orderId, string directPay, string adminSession, string adminUser, string csrf)
                => PayAsync(connection, engine, new StorefrontPhpAjax.PayForOrderProtocolRequest("1", orderId, "12", directPay, null, csrf, null, null, adminSession, adminUser, cp), PayConfig());

            Assert.Equal("{\"error\":\"Error! CSRF 4\",\"message\":\"Error! CSRF 4\",\"status\":false}", await Pay("200", "1", "as", "7", "ck"));
            Assert.Equal("{\"status\":false,\"message\":\"Forbidden\"}", await Pay("200", "1", "as9", "9", "ak9"));
            Assert.Equal("{\"status\":false,\"message\":\"Forbidden\"}", await Pay("200", "0", "as", "7", "ak"));

            Assert.Equal("{\"status\":true,\"message\":\"\"}", await Pay("200", "1", "as", "7", "ak"));
            Assert.Equal(
                "1|12.00|2|200~0|12.00|1|",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', income, amount, operation_code, IFNULL(pay_orders, '')) ORDER BY id SEPARATOR '~') FROM shop_users_accounting WHERE order_id = 200 OR pay_orders = '200'"));
            Assert.Equal("1|1|2", await ScalarAsync(cs, "SELECT CONCAT_WS('|', paid, paid_type, status) FROM shop_orders WHERE id = 200"));
            Assert.Equal(
                "Payment. Amount <b>12</b><br/>Paid status: <b>Fully paid</b>|7|1|0~Payment method: <b>Pay on place</b>|7|1|0",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT_WS('|', text, user_id, is_manager, is_robot) ORDER BY id SEPARATOR '~') FROM shop_orders_logs WHERE order_id = 200 AND text NOT LIKE 'Status%' AND text NOT LIKE 'WhatsApp%'"));
            Assert.Equal(StorefrontNotifyPerson.Direct("guest@example.com", "+971 50 999 9999"), Assert.Single(notify.Sent[1].Persons));
        });

    private static Dictionary<string, string> PayConfig(params (string Key, string Value)[] extra)
    {
        var config = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["domain_path"] = "https://www.epartscart.com/",
            ["backend_dir"] = "cp",
            ["partial_payment"] = "",
            ["client_overdraft"] = "",
        };
        foreach (var (key, value) in extra)
        {
            config[key] = value;
        }

        return config;
    }

    private static async Task<string> PayAsync(DbConnection connection, IShopPayForOrderService engine, StorefrontPhpAjax.PayForOrderProtocolRequest request, Dictionary<string, string> config)
    {
        var payload = await StorefrontPhpAjax.PayForOrderProtocolAsync(connection, engine, request, config, "tech", CancellationToken.None);
        return payload is StorefrontPhpAjax.RawHttp raw ? raw.Body : JsonSerializer.Serialize(payload);
    }

    private static async Task WithDatabaseAsync(Func<string, Task> run)
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

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            foreach (var ddl in Schema)
            {
                await ExecuteAsync(cs, ddl);
            }

            foreach (var seed in Seed)
            {
                await ExecuteAsync(cs, seed);
            }

            await run(cs);
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

    private static readonly string[] Schema =
    [
        "CREATE TABLE shop_orders (id INT NOT NULL PRIMARY KEY, user_id INT NOT NULL DEFAULT 0, successfully_created TINYINT NOT NULL DEFAULT 1, status INT NOT NULL DEFAULT 0, paid TINYINT NOT NULL DEFAULT 0, office_id INT NOT NULL DEFAULT 0, email_not_auth VARCHAR(255) NOT NULL DEFAULT '', phone_not_auth VARCHAR(64) NOT NULL DEFAULT '', time INT NOT NULL DEFAULT 0, paid_type INT NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_orders_paid_type (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, active TINYINT NOT NULL DEFAULT 1, `order` INT NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_orders_items (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, product_type INT NOT NULL DEFAULT 1, status INT NOT NULL DEFAULT 0, price DECIMAL(10,2) NOT NULL DEFAULT 0, count_need INT NOT NULL DEFAULT 0, t2_manufacturer VARCHAR(64) NOT NULL DEFAULT '', t2_article VARCHAR(64) NOT NULL DEFAULT '', t2_article_show VARCHAR(64) NULL, t2_name VARCHAR(255) NOT NULL DEFAULT '', t2_storage_id INT NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_orders_items_details (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, order_item_id INT NOT NULL, office_id INT NOT NULL DEFAULT 0, storage_id INT NOT NULL DEFAULT 0, storage_record_id INT NOT NULL DEFAULT 0, count_reserved INT NOT NULL DEFAULT 0, count_issued INT NOT NULL DEFAULT 0, count_canceled INT NOT NULL DEFAULT 0, price_purchase DECIMAL(10,2) NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_storages_data (id INT NOT NULL PRIMARY KEY, exist INT NOT NULL DEFAULT 0, reserved INT NOT NULL DEFAULT 0, issued INT NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_orders_statuses_ref (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, `order` INT NOT NULL, for_finish TINYINT NOT NULL DEFAULT 0, for_inverse TINYINT NOT NULL DEFAULT 0, for_paid TINYINT NOT NULL DEFAULT 0, to_manager_sms TINYINT NOT NULL DEFAULT 1, to_customer_sms TINYINT NOT NULL DEFAULT 1)",
        "CREATE TABLE shop_orders_items_statuses_ref (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, color VARCHAR(16) NOT NULL DEFAULT '', `order` INT NOT NULL, count_flag TINYINT NOT NULL DEFAULT 1, issue_flag TINYINT NOT NULL DEFAULT 0, for_finish TINYINT NOT NULL DEFAULT 0, to_manager_sms TINYINT NOT NULL DEFAULT 1, to_customer_sms TINYINT NOT NULL DEFAULT 1)",
        "CREATE TABLE shop_offices (id INT NOT NULL PRIMARY KEY, users TEXT NULL)",
        "CREATE TABLE shop_accounting_codes (id INT NOT NULL PRIMARY KEY, `key` VARCHAR(64) NOT NULL)",
        "CREATE TABLE shop_users_accounting (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, time INT NOT NULL, income TINYINT NOT NULL, amount DECIMAL(10,2) NOT NULL, operation_code INT NULL, active TINYINT NOT NULL, order_id INT NOT NULL DEFAULT 0, office_id INT NULL, pay_orders VARCHAR(64) NULL)",
        "CREATE TABLE templates (id INT NOT NULL PRIMARY KEY, is_frontend TINYINT NOT NULL, current TINYINT NOT NULL, data_value TEXT NULL)",
        "CREATE TABLE shop_orders_logs (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, time INT NOT NULL, user_id INT NOT NULL DEFAULT 0, is_manager TINYINT NOT NULL DEFAULT 0, text TEXT NOT NULL, is_robot TINYINT NOT NULL DEFAULT 0)",
        "CREATE TABLE users (user_id INT NOT NULL PRIMARY KEY, email VARCHAR(255) NOT NULL DEFAULT '', phone VARCHAR(64) NOT NULL DEFAULT '')",
        "CREATE TABLE `groups` (id INT NOT NULL PRIMARY KEY, parent INT NOT NULL DEFAULT 0, for_backend TINYINT NOT NULL DEFAULT 0, value VARCHAR(64) NOT NULL DEFAULT '')",
        "CREATE TABLE users_groups_bind (user_id INT NOT NULL, group_id INT NOT NULL)",
        "CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NOT NULL)",
        "CREATE TABLE sessions (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL DEFAULT 0, csrf_guard_key VARCHAR(64) NOT NULL DEFAULT '')",
        "CREATE TABLE epc_portal_site_settings (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, host VARCHAR(255) NOT NULL, hub_name VARCHAR(120) NOT NULL DEFAULT '', contact_json TEXT NULL)",
    ];

    private static readonly string[] Seed =
    [
        """
        INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
        ('1316','en','Payment'), ('4366','en','Amount'), ('4529','en','Paid status'), ('3584','en','Fully paid'), ('3515','en','Part paid'),
        ('4645','en','Payment method'), ('4643','en','Open order'),
        ('pt_place','en','Pay on place'), ('pt_card','en','Card online'), ('pt_balance','en','From balance'),
        ('st_new','en','New'), ('st_paid','en','Paid'), ('st_finish','en','Finished'), ('st_cancel','en','Cancelled'),
        ('it_wait','en','Waiting'), ('it_issue','en','Issued'), ('it_cancel','en','Line cancelled'),
        ('4568','en','Status changed to'), ('4569','en','Line'), ('4570','en','changed to'), ('5296','en','Order is not paid'),
        ('2122','en','Error'), ('5630','en','Only one line'), ('5631','en','Bad count'), ('3492','en','Refunded'),
        ('4642','en','to the customer balance'), ('3493','en','paid back to the guest'),
        ('5636','en','Split'), ('5637','en','from'), ('5638','en','Was'), ('5639','en','now')
        """,
        """
        INSERT INTO shop_orders_statuses_ref (id, name, `order`, for_finish, for_inverse, for_paid, to_manager_sms) VALUES
        (1,'st_new',1,0,0,0,1), (2,'st_paid',2,0,0,1,1), (3,'st_finish',3,1,0,0,1), (4,'st_cancel',4,0,1,0,1)
        """,
        """
        INSERT INTO shop_orders_items_statuses_ref (id, name, `order`, count_flag, issue_flag, for_finish, to_manager_sms) VALUES
        (10,'it_wait',1,1,0,0,1), (11,'it_issue',2,1,1,1,1), (12,'it_cancel',3,0,0,0,0)
        """,
        "INSERT INTO shop_orders_paid_type (id, name, active, `order`) VALUES (1, 'pt_place', 1, 1), (2, 'pt_balance', 1, 2), (3, 'pt_card', 1, 3)",
        "INSERT INTO shop_offices (id, users) VALUES (1, '[\"7\",\"8\"]')",
        "INSERT INTO `groups` (id, parent, for_backend) VALUES (1, 0, 1), (2, 1, 0), (3, 0, 0)",
        "INSERT INTO users_groups_bind (user_id, group_id) VALUES (7, 2), (8, 3)",
        "INSERT INTO sessions (session, user_id, type, csrf_guard_key) VALUES ('cs', 5, 0, 'ck'), ('cs8', 8, 0, 'ck'), ('as', 7, 1, 'ak'), ('as9', 9, 1, 'ak9')",
        "INSERT INTO shop_accounting_codes (id, `key`) VALUES (1, '1_pay_for_order'), (2, '2_income_for_direct_pay'), (5, '5_refund_from_order_to_balance'), (6, '6_refund_from_balance')",
        "INSERT INTO templates (id, is_frontend, current, data_value) VALUES (1, 1, 1, '{\"main_color\":\"#123456\"}')",
        "INSERT INTO users (user_id, email, phone) VALUES (5, 'buyer@example.com', '+971 50-111 1111')",
        "INSERT INTO epc_portal_site_settings (host, hub_name, contact_json) VALUES ('www.epartscart.com', 'Hub', '{\"trade_name\":\"eParts Cart\"}')",
        """
        INSERT INTO shop_orders (id, user_id, status, paid, office_id, email_not_auth, phone_not_auth) VALUES
        (100, 5, 2, 1, 1, '', ''), (200, 0, 1, 0, 1, 'guest@example.com', '+971 50 999 9999'), (300, 5, 1, 0, 1, '', '')
        """,
        """
        INSERT INTO shop_orders_items (id, order_id, product_type, status, price, count_need, t2_manufacturer, t2_article, t2_name) VALUES
        (1001, 100, 1, 10, 10, 2, 'TOYOTA', '04152', 'Oil filter'), (1002, 100, 2, 10, 5, 1, 'DENSO', 'K20', 'Plug'),
        (2001, 200, 1, 10, 4, 3, 'NISSAN', 'X1', ''), (3001, 300, 1, 10, 30, 5, 'TOYOTA', 'P5', 'Pad')
        """,
        """
        INSERT INTO shop_orders_items_details (order_id, order_item_id, office_id, storage_id, storage_record_id, count_reserved) VALUES
        (100, 1001, 1, 1, 50, 2), (200, 2001, 1, 1, 60, 3), (300, 3001, 1, 1, 70, 5)
        """,
        "INSERT INTO shop_storages_data (id, exist, reserved, issued) VALUES (50, 8, 2, 0), (60, 0, 3, 0), (70, 0, 5, 0)",
        "INSERT INTO shop_users_accounting (user_id, time, income, amount, operation_code, active, order_id, office_id) VALUES (5, 1, 0, 25, 1, 1, 100, 1)",
    ];

    private static async Task ExecuteAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class ConfigMailer : ICpPlatformMailer
    {
        public IReadOnlyDictionary<string, string> ReadConfig()
            => new Dictionary<string, string> { ["domain_path"] = "https://www.epartscart.com/", ["backend_dir"] = "cp" };

        public Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
            => Task.FromResult(new CpSmsSendOutcome(true, string.Empty));
    }

    private sealed class WriteConnections(string cs) : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private sealed class RecordingDispatcher : IStorefrontNotifyDispatcher
    {
        public List<(string Name, IReadOnlyDictionary<string, string> Vars, IReadOnlyList<StorefrontNotifyPerson> Persons, IReadOnlyDictionary<string, string>? StatusRef)> Sent { get; } = [];

        public Task<StorefrontNotifyAnswer> SendAsync(
            DbConnection connection,
            string name,
            IReadOnlyDictionary<string, string> vars,
            IReadOnlyList<StorefrontNotifyPerson> persons,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string>? statusRef = null)
        {
            Sent.Add((name, vars, persons, statusRef));
            return Task.FromResult(new StorefrontNotifyAnswer(true, string.Empty, []));
        }

        public Task<StorefrontNotifyOutcome> SendDirectEmailAsync(DbConnection connection, string name, IReadOnlyDictionary<string, string> vars, string email, CancellationToken cancellationToken = default)
            => Task.FromResult(new StorefrontNotifyOutcome(true, true, string.Empty));
    }
}
