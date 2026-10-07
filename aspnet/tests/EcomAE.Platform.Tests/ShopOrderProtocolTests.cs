using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Cp;
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
    public async Task Protocol_StatusesRefundsStockAndCascadesLikePhp_OnThrowawayDatabase_ThenDropped()
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
        "CREATE TABLE shop_orders (id INT NOT NULL PRIMARY KEY, user_id INT NOT NULL DEFAULT 0, status INT NOT NULL DEFAULT 0, paid TINYINT NOT NULL DEFAULT 0, office_id INT NOT NULL DEFAULT 0, email_not_auth VARCHAR(255) NOT NULL DEFAULT '', phone_not_auth VARCHAR(64) NOT NULL DEFAULT '', time INT NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_orders_items (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, product_type INT NOT NULL DEFAULT 1, status INT NOT NULL DEFAULT 0, price DECIMAL(10,2) NOT NULL DEFAULT 0, count_need INT NOT NULL DEFAULT 0, t2_manufacturer VARCHAR(64) NOT NULL DEFAULT '', t2_article VARCHAR(64) NOT NULL DEFAULT '', t2_article_show VARCHAR(64) NULL, t2_name VARCHAR(255) NOT NULL DEFAULT '', t2_storage_id INT NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_orders_items_details (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, order_item_id INT NOT NULL, office_id INT NOT NULL DEFAULT 0, storage_id INT NOT NULL DEFAULT 0, storage_record_id INT NOT NULL DEFAULT 0, count_reserved INT NOT NULL DEFAULT 0, count_issued INT NOT NULL DEFAULT 0, count_canceled INT NOT NULL DEFAULT 0, price_purchase DECIMAL(10,2) NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_storages_data (id INT NOT NULL PRIMARY KEY, exist INT NOT NULL DEFAULT 0, reserved INT NOT NULL DEFAULT 0, issued INT NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_orders_statuses_ref (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, `order` INT NOT NULL, for_finish TINYINT NOT NULL DEFAULT 0, for_inverse TINYINT NOT NULL DEFAULT 0, for_paid TINYINT NOT NULL DEFAULT 0, to_manager_sms TINYINT NOT NULL DEFAULT 1, to_customer_sms TINYINT NOT NULL DEFAULT 1)",
        "CREATE TABLE shop_orders_items_statuses_ref (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, color VARCHAR(16) NOT NULL DEFAULT '', `order` INT NOT NULL, count_flag TINYINT NOT NULL DEFAULT 1, issue_flag TINYINT NOT NULL DEFAULT 0, for_finish TINYINT NOT NULL DEFAULT 0, to_manager_sms TINYINT NOT NULL DEFAULT 1, to_customer_sms TINYINT NOT NULL DEFAULT 1)",
        "CREATE TABLE shop_offices (id INT NOT NULL PRIMARY KEY, users TEXT NULL)",
        "CREATE TABLE shop_accounting_codes (id INT NOT NULL PRIMARY KEY, `key` VARCHAR(64) NOT NULL)",
        "CREATE TABLE shop_users_accounting (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, time INT NOT NULL, income TINYINT NOT NULL, amount DECIMAL(10,2) NOT NULL, operation_code INT NULL, active TINYINT NOT NULL, order_id INT NOT NULL DEFAULT 0, office_id INT NULL)",
        "CREATE TABLE shop_orders_logs (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, time INT NOT NULL, user_id INT NOT NULL DEFAULT 0, is_manager TINYINT NOT NULL DEFAULT 0, text TEXT NOT NULL, is_robot TINYINT NOT NULL DEFAULT 0)",
        "CREATE TABLE users (user_id INT NOT NULL PRIMARY KEY, email VARCHAR(255) NOT NULL DEFAULT '', phone VARCHAR(64) NOT NULL DEFAULT '')",
        "CREATE TABLE `groups` (id INT NOT NULL PRIMARY KEY, parent INT NOT NULL DEFAULT 0, for_backend TINYINT NOT NULL DEFAULT 0, value VARCHAR(64) NOT NULL DEFAULT '')",
        "CREATE TABLE users_groups_bind (user_id INT NOT NULL, group_id INT NOT NULL)",
        "CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NOT NULL)",
        "CREATE TABLE epc_portal_site_settings (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, host VARCHAR(255) NOT NULL, hub_name VARCHAR(120) NOT NULL DEFAULT '', contact_json TEXT NULL)",
    ];

    private static readonly string[] Seed =
    [
        """
        INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
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
        "INSERT INTO shop_offices (id, users) VALUES (1, '[\"7\",\"8\"]')",
        "INSERT INTO `groups` (id, parent, for_backend) VALUES (1, 0, 1), (2, 1, 0), (3, 0, 0)",
        "INSERT INTO users_groups_bind (user_id, group_id) VALUES (7, 2), (8, 3)",
        "INSERT INTO shop_accounting_codes (id, `key`) VALUES (5, '5_refund_from_order_to_balance'), (6, '6_refund_from_balance')",
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
            => new Dictionary<string, string> { ["domain_path"] = "https://www.epartscart.com/" };

        public Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
            => Task.FromResult(new CpSmsSendOutcome(true, string.Empty));
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
