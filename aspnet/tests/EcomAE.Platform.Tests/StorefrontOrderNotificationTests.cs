using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP <c>epc_checkout_send_order_notifications()</c>: staff + customer new-order e-mails (staff / customer layouts,
/// content/notifications/template.php wrapper, admin retry, order log) ahead of the supplier LPO step.
/// </summary>
public sealed class StorefrontOrderNotificationTests
{
    private const string Cell = "<td style=\"border: 1px solid #cfcfcf; padding: 5px;\">";

    [Fact]
    public void Wrap_AppliesPhpTemplateReplacements()
    {
        var html = StorefrontNotifyDispatcher.Wrap(
            "New order #7",
            "<p class=\"lead\">Obtaining - Courier</p><table class=\"table\"><tr><th>H</th><td>A</td><td colspan=\"2\">B</td></tr></table>"
                + "<div class=\"collapse\" id=\"collapse_office_map_container\">map</div><h4>T</h4>",
            new StorefrontNotifyDispatcher.EmailTemplateSettings("/content/files/logo.png", true, "#123456", 4),
            logoExists: true,
            "https://www.epartscart.com/",
            new DateTimeOffset(2026, 10, 7, 9, 5, 0, TimeSpan.FromHours(4)),
            "Footer one",
            "Footer two",
            ["Courier"],
            "Obtaining");

        Assert.Contains("src=\"https://www.epartscart.com/content/files/logo.png?v=4\"", html, StringComparison.Ordinal);
        Assert.Contains("margin-right: 50px; background:#123456; image-rendering", html, StringComparison.Ordinal);
        Assert.Contains("New order #7<br/><small style=\"font-size: 13px;\">07.10.2026 09:05</small>", html, StringComparison.Ordinal);
        Assert.Contains("Footer one.\n\t\t\t<br/>\n\t\t\tFooter two.", html, StringComparison.Ordinal);
        Assert.Contains("<h4 style=\"font-family: Calibri; font-size: 16px;", html, StringComparison.Ordinal);
        Assert.Contains(">Obtaining - Courier</h4>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<p class=\"lead\">", html, StringComparison.Ordinal);
        Assert.Contains("<div style=\"display:none;\">map</div>", html, StringComparison.Ordinal);
        Assert.Contains("<table cellspacing=\"0\" style=\"border-collapse: collapse;", html, StringComparison.Ordinal);
        Assert.Contains("<tr style=\"border: 1px solid #cfcfcf;\"><th style=\"border: 1px solid #cfcfcf; padding: 5px;\">H</th>" + Cell + "A</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td style=\"border: 1px solid #cfcfcf; padding: 5px;\" colspan=\"2\">B</td>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Wrap_OmitsLogoWhenFileMissing()
    {
        var html = StorefrontNotifyDispatcher.Wrap(
            "S", "B", new StorefrontNotifyDispatcher.EmailTemplateSettings("/logo.png", false, "", 1), false, "https://x/", DateTimeOffset.UnixEpoch, "a", "b", [], "c");
        Assert.DoesNotContain("logotype", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"admin_email\":\"owner@x.ae\",\"from_email\":\"from@x.ae\"}", "cfg@x.ae", "owner@x.ae")]
    [InlineData("{\"from_email\":\"from@x.ae\"}", "cfg@x.ae", "from@x.ae")]
    [InlineData("{}", "noreply@x.ae", "noreply@x.ae")]
    [InlineData("", "", "admin@epartscart.com")]
    public void AdminEmail_FollowsSiteContextThenFallback(string contactJson, string configFrom, string expected)
        => Assert.Equal(
            expected,
            StorefrontOrderNotificationService.AdminEmail(CpIndustrySettingsService.ParseContact(contactJson, null), configFrom, "www.epartscart.com"));

    [Fact]
    public void EmailStatus_MatchesDirectEmailOrUserId()
    {
        var answer = new StorefrontNotifyAnswer(true, string.Empty,
        [
            new(StorefrontNotifyPerson.Direct("Owner@X.ae"), true, false, "fail"),
            new(StorefrontNotifyPerson.User(5), true, true, string.Empty),
            new(StorefrontNotifyPerson.User(6), false, false, string.Empty),
        ]);
        Assert.False(answer.EmailStatus("owner@x.ae"));
        Assert.True(answer.EmailStatus("5"));
        Assert.Null(answer.EmailStatus("6"));
        Assert.False(answer.EmailStatus());
        Assert.Null(new StorefrontNotifyAnswer(false, "Notification not found", []).EmailStatus());
    }

    [Theory]
    [InlineData("{\"city\":\"Dubai\",\"address\":\"Al Quoz\"}", "Al Quoz")]
    [InlineData("{\"address\":\"0\",\"city\":\"Dubai\"}", "Dubai")]
    [InlineData("{\"mode\":2,\"url\":\"a/b\",\"n\":\"Ж\"}", "{\"mode\":2,\"url\":\"a\\/b\",\"n\":\"Ж\"}")]
    [InlineData("{}", "")]
    [InlineData("", "")]
    public void DeliveryAddress_FollowsStaffLayout(string json, string expected)
        => Assert.Equal(expected, StorefrontOrderNotificationService.DeliveryAddress(json));

    [Theory]
    [InlineData(1234.5, 2, ",", "1,234.50")]
    [InlineData(0.125, 2, "", "0.13")]
    [InlineData(-0.001, 2, "", "0.00")]
    [InlineData(1.5, 3, "", "1.500")]
    public void PhpNumber_MatchesNumberFormat(double value, int decimals, string thousands, string expected)
        => Assert.Equal(expected, StorefrontOrderNotificationService.PhpNumber((decimal)value, decimals, thousands));

    [Fact]
    public void Nl2Br_KeepsLineBreaks()
        => Assert.Equal("a<br />\nb<br />\r\nc", StorefrontOrderNotificationService.Nl2Br("a\nb\r\nc"));

    [Fact]
    public async Task Checkout_SendsStaffAndCustomerOrderMails_OnThrowawayDatabase_ThenDropped()
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
            foreach (var ddl in StorefrontOrderSupplierPoTests.Schema.Concat(ExtraSchema))
            {
                await ExecuteAsync(cs, ddl);
            }

            await SeedAsync(cs);
            var mailer = new CapturingMailer(failFirstTo: "owner@epartscart.com");
            await using var provider = Services(cs, mailer);
            await using var scope = provider.CreateAsyncScope();
            var checkout = scope.ServiceProvider.GetRequiredService<IStorefrontCheckoutWriteService>();

            var result = await checkout.CreateAsync(5, new StorefrontCheckoutWriteRequest(
                HowGetMode: 2,
                UsersAgreement: true,
                OrderMessage: "Please call <VIP>\nbefore delivery",
                HowGetCookieJson: "{\"carrier\":\"dhl\",\"city\":\"Dubai\",\"country\":\"AE\",\"address\":\"Al Quoz 1\",\"phone\":\"+971500000000\",\"delivery_price\":\"40\"}"));
            Assert.True(result.Ok, result.Message);
            var no = result.OrderId.ToString(CultureInfo.InvariantCulture);
            Assert.Contains("Order e-mails: admin sent, customer sent.", result.Message, StringComparison.Ordinal);
            Assert.Contains("Supplier LPO e-mails: 2 sent, 1 skipped.", result.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("remain PHP", result.Message, StringComparison.Ordinal);
            Assert.Equal("1", await ScalarAsync(cs, "SELECT office_id FROM shop_orders WHERE id = " + no));

            Assert.Equal(
                ["owner@epartscart.com", "crm@example.com", "ops@example.com", "owner@epartscart.com", "Buyer@Example.com", "own.stock@example.com", "supplier-a@example.com"],
                mailer.Attempts.Select(a => a.To).ToArray());
            Assert.False(mailer.Attempts[0].Delivered);
            Assert.All(mailer.Attempts.Skip(1), a => Assert.True(a.Delivered));
            Assert.Equal("New order #" + no, mailer.Attempts[0].Subject);
            Assert.Equal("Your order #" + no, mailer.Attempts[4].Subject);

            var staff = mailer.Attempts[3].Body;
            Assert.Contains("New order #" + no + "<br/><small style=\"font-size: 13px;\">", staff, StringComparison.Ordinal);
            Assert.Contains("href=\"https://www.epartscart.com/cp/shop/orders/order?order_id=" + no + "\" target=\"_blank\">View order in your Control Panel</a>", staff, StringComparison.Ordinal);
            Assert.Contains("<strong>New order #" + no + "</strong>", staff, StringComparison.Ordinal);
            Assert.Contains("· New\n</p>", staff, StringComparison.Ordinal);
            Assert.Contains("Information on the client who placed the order:", staff, StringComparison.Ordinal);
            Assert.Contains("Name:</td><td style=\"padding:4px 0;\">Ali Buyer</td>", staff, StringComparison.Ordinal);
            Assert.Contains("<a href=\"mailto:Buyer@Example.com\">Buyer@Example.com</a>", staff, StringComparison.Ordinal);
            Assert.Contains("Profile:</td><td style=\"padding:4px 0;\">Retail customers</td>", staff, StringComparison.Ordinal);
            Assert.Contains("href=\"https://www.epartscart.com/cp/users/usermanager/user?user_id=5\">Open customer in Control Panel</a>", staff, StringComparison.Ordinal);
            Assert.Contains("Delivery address:</td>" + Cell + "Al Quoz 1</td>", staff, StringComparison.Ordinal);
            Assert.Contains("Delivery type:</td>" + Cell + "Courier delivery</td>", staff, StringComparison.Ordinal);
            Assert.Contains("Payment method:</td>" + Cell + "Bank transfer</td>", staff, StringComparison.Ordinal);
            Assert.Contains("<td style=\"border:1px solid #ccc;padding:6px;\">Own Warehouse Dubai</td>", staff, StringComparison.Ordinal);
            Assert.Contains("<td style=\"border:1px solid #ccc;padding:6px;\">from 2 to 5 days</td>", staff, StringComparison.Ordinal);
            Assert.Contains("120.00 AED<br><span style=\"font-size:11px;color:#555;\">(32.68 USD)</span>", staff, StringComparison.Ordinal);
            Assert.Contains("Gross (incl. VAT):</td><td style=\"padding:4px 0;text-align:right;font-weight:bold;\">630.00 AED", staff, StringComparison.Ordinal);
            Assert.Contains("Net (ex VAT):</td><td style=\"padding:4px 0;text-align:right;font-weight:bold;\">600.01 AED", staff, StringComparison.Ordinal);
            Assert.Contains("VAT on courier:</td><td style=\"padding:4px 0;text-align:right;\">2.00 AED", staff, StringComparison.Ordinal);
            Assert.Contains("<strong>672.00 AED<br>", staff, StringComparison.Ordinal);
            Assert.Contains("UAE — VAT on goods and courier (income)", staff, StringComparison.Ordinal);
            Assert.Contains("Weight:</td><td style=\"padding:4px 0;text-align:right;\">1.500</td>", staff, StringComparison.Ordinal);
            Assert.Contains("Please call &amp;lt;VIP&amp;gt;&lt;br/&gt;before delivery", staff, StringComparison.Ordinal);
            Assert.Contains("Footer one.", staff, StringComparison.Ordinal);

            var customer = mailer.Attempts[4].Body;
            Assert.Contains("background: #123456;", customer, StringComparison.Ordinal);
            Assert.Contains("href=\"https://www.epartscart.com/shop/orders/order?order_id=" + no + "\">View order</a>", customer, StringComparison.Ordinal);
            Assert.Contains(Cell + "Order number</td> " + Cell + no + "</td>", customer, StringComparison.Ordinal);
            Assert.Contains("background-color:#e74c3c;", customer, StringComparison.Ordinal);
            Assert.Contains(Cell + "630.00</td>", customer, StringComparison.Ordinal);
            Assert.Contains("Delivery — DHL Express", customer, StringComparison.Ordinal);
            Assert.Contains(Cell + "Dubai, AE</td>", customer, StringComparison.Ordinal);
            Assert.Contains(Cell + "2 - 5 days</td>", customer, StringComparison.Ordinal);
            Assert.Contains(Cell + "Pending</td>", customer, StringComparison.Ordinal);
            Assert.Contains(">Your comment</h4><div style=\"font-family: Calibri; font-size: 14px;\">Please call &lt;VIP&gt;<br/>before delivery</div>", customer, StringComparison.Ordinal);

            Assert.Equal(
                string.Join('\n',
                    "Order email to admin owner@epartscart.com: sent (retry)",
                    "Order email to customer (user #5): sent",
                    "Supplier LPO to own.stock@example.com (Own Warehouse Dubai, LPO #" + no + "): sent",
                    "Supplier LPO to supplier-a@example.com (Supplier A Prices, LPO #" + no + "): sent",
                    "Supplier LPO skipped (no order e-mail): Supplier B [ID 30]"),
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(text ORDER BY id SEPARATOR '\n') FROM shop_orders_logs WHERE is_robot = 1 AND order_id = " + no));
            Assert.Contains("LPO number</td>", mailer.Attempts[5].Body, StringComparison.Ordinal);
            Assert.Contains("Footer two.", mailer.Attempts[5].Body, StringComparison.Ordinal);

            await ExecuteAsync(cs, """
                INSERT INTO shop_orders (id, user_id, time, successfully_created, status, paid, how_get, how_get_json, email_not_auth, phone_not_auth, office_id)
                VALUES (900, 0, 1791360000, 1, 1, 1, 1, '{"mode":1,"office_id":1}', 'guest@x.ae', '+971', 1)
                """);
            await ExecuteAsync(cs, "INSERT INTO shop_orders_items (order_id, product_type, price, count_need, status, t2_name) VALUES (900, 2, 15, 1, 1, 'Wiper')");
            var notifications = scope.ServiceProvider.GetRequiredService<IStorefrontOrderNotificationService>();
            mailer.Attempts.Clear();
            var guest = await notifications.SendAsync(900);
            Assert.True(guest.AdminSent);
            Assert.False(guest.AdminRetried);
            Assert.True(guest.CustomerSent);
            Assert.Equal(["owner@epartscart.com", "ops@example.com", "guest@x.ae"], mailer.Attempts.Select(a => a.To).ToArray());
            var guestStaff = mailer.Attempts[0].Body;
            Assert.Contains("Profile:</td><td style=\"padding:4px 0;\">Guests</td>", guestStaff, StringComparison.Ordinal);
            Assert.Contains("<a href=\"mailto:guest@x.ae\">guest@x.ae</a>", guestStaff, StringComparison.Ordinal);
            Assert.DoesNotContain("Open customer in Control Panel", guestStaff, StringComparison.Ordinal);
            var guestCustomer = mailer.Attempts[2].Body;
            Assert.Contains("shop/orders/zakaz-bez-registracii?order_id=900", guestCustomer, StringComparison.Ordinal);
            Assert.Contains("background-color:#62cb31;", guestCustomer, StringComparison.Ordinal);
            Assert.Contains("Obtaining method - Pickup", guestCustomer, StringComparison.Ordinal);
            Assert.Contains("Address: Dubai, Al Quoz</span>", guestCustomer, StringComparison.Ordinal);
            Assert.Contains("<div style=\"display:none;\">", guestCustomer, StringComparison.Ordinal);
            Assert.Contains("Opening hours: 9-18<br>Fri off</td>", guestCustomer, StringComparison.Ordinal);
            Assert.Contains("Phone: 04 000 0000</td>", guestCustomer, StringComparison.Ordinal);
            var placed = StorefrontNotifyDispatcher.PlatformTime(1791360000);
            Assert.Contains(
                Cell + placed.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + " " + placed.Hour.ToString(CultureInfo.InvariantCulture) + placed.ToString(":mm", CultureInfo.InvariantCulture) + "</td>",
                guestCustomer,
                StringComparison.Ordinal);

            await ExecuteAsync(cs, "DELETE FROM notifications_settings WHERE name LIKE 'new_order_to_%'");
            mailer.Attempts.Clear();
            var missing = await notifications.SendAsync(900);
            Assert.False(missing.AdminSent);
            Assert.False(missing.CustomerSent);
            Assert.Empty(mailer.Attempts);
            Assert.Equal(
                string.Join('\n',
                    "Order email to admin owner@epartscart.com: sent",
                    "Order email to customer (guest@x.ae): sent",
                    "Order email to admin owner@epartscart.com: FAILED after retry",
                    "Order email to customer (guest@x.ae): FAILED"),
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(text ORDER BY id SEPARATOR '\n') FROM shop_orders_logs WHERE order_id = 900"));
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

    private static async Task SeedAsync(string cs)
    {
        await ExecuteAsync(cs, """
            INSERT INTO shop_storages (id, name, interface_type, connection_options, currency) VALUES
            (10, 'Own Warehouse Dubai', 1, '{"order_email":" Own.Stock@Example.com "}', 'AED'),
            (20, 'Supplier A Prices', 2, '{"price_id":"7"}', 'AED'),
            (30, 'Supplier B', 2, '{}', 'AED')
            """);
        await ExecuteAsync(cs, "INSERT INTO shop_docpart_prices (id, sender_email) VALUES (7, 'Supplier-A@Example.com')");
        await ExecuteAsync(cs, "INSERT INTO epc_erp_suppliers (storage_id, name) VALUES (20, 'Supplier A LLC')");
        await ExecuteAsync(cs, "INSERT INTO shop_obtaining_modes (id, caption, handler) VALUES (1, '7001', 'get_in_office'), (2, '7002', 'epc_carriers')");
        await ExecuteAsync(cs, "INSERT INTO shop_orders_statuses_ref (id, for_created, name) VALUES (1, 1, 'New')");
        await ExecuteAsync(cs, "INSERT INTO shop_orders_items_statuses_ref (id, for_created, name, color, count_flag) VALUES (1, 1, 'Pending', '#ffffff', 1), (9, 0, 'Cancelled', '#eeeeee', 0)");
        await ExecuteAsync(cs, "INSERT INTO shop_currencies (iso_code, rate) VALUES ('AED', 1), ('840', 3.6725)");
        await ExecuteAsync(cs, "INSERT INTO shop_storages_data (id, storage_id, price, price_purchase) VALUES (500, 10, 90, 60)");
        await ExecuteAsync(cs, "INSERT INTO shop_orders_paid_type (id, name, active, `order`) VALUES (0, 'Bank transfer', 1, 1)");
        await ExecuteAsync(cs, """
            INSERT INTO notifications_settings (name, email_on, sms_on, send_for_not_confirmed, email_subject, email_body, sms_body, vars) VALUES
            ('lpo_to_supplier', 1, 0, 1, 'LPO #%lpo_number% — please supply parts (%storage_name%)', '%order_text%', '',
             '[{"name":"order_id","type":"text"},{"name":"lpo_number","type":"text"},{"name":"storage_name","type":"text"},{"name":"order_text","type":"text"}]'),
            ('new_order_to_manager', 1, 0, 1, 'New order #%order_id%', '%order_text%', '', '[{"name":"order_id","type":"text"},{"name":"order_text","type":"text"}]'),
            ('new_order_to_user', 1, 0, 1, 'Your order #%order_id%', '%order_text%', '', '[{"name":"order_id","type":"text"},{"name":"order_text","type":"text"}]')
            """);
        await ExecuteAsync(cs, """
            INSERT INTO users (user_id, email, email_confirmed, phone) VALUES
            (5, 'Buyer@Example.com', 0, '+971501111111'), (7, 'crm@example.com', 1, ''), (8, 'ops@example.com', 1, ''), (9, 'nobody@example.com', 1, '')
            """);
        await ExecuteAsync(cs, "INSERT INTO users_profiles (user_id, data_key, data_value) VALUES (5, 'name', 'Ali'), (5, 'surname', 'Buyer'), (5, 'account_manager_id', '7')");
        await ExecuteAsync(cs, "INSERT INTO `groups` (id, parent, for_backend, for_guests, value) VALUES (1, 0, 1, 0, 'Staff'), (2, 1, 0, 0, 'Managers'), (3, 0, 0, 1, '7200'), (4, 0, 0, 0, '7201')");
        await ExecuteAsync(cs, "INSERT INTO users_groups_bind (user_id, group_id) VALUES (5, 4), (7, 2), (8, 2), (9, 4)");
        await ExecuteAsync(cs, "INSERT INTO shop_offices (id, users, city, address, timetable, phone) VALUES (1, '[\"8\",\"9\"]', '7101', '7102', '7103', '04 000 0000')");
        await ExecuteAsync(cs, "INSERT INTO templates (is_frontend, current, data_value) VALUES (1, 1, '{\"main_color\":\"#123456\",\"logo_file\":\"/content/files/missing-logo.png\",\"version\":3}')");
        await ExecuteAsync(cs, "INSERT INTO epc_portal_site_settings (host, contact_json) VALUES ('www.epartscart.com', '{\"admin_email\":\"owner@epartscart.com\"}')");
        await ExecuteAsync(cs, """
            INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
            ('4643', 'en', 'View order'), ('4929', 'en', 'Footer one'), ('4930', 'en', 'Footer two'), ('3507', 'en', 'Obtaining method'),
            ('5315', 'en', 'days'), ('1082', 'en', 'Order number'), ('4509', 'en', 'Your comment'), ('7001', 'en', 'Pickup'), ('7002', 'en', 'Courier delivery'),
            ('3376', 'en', 'Address'), ('4446', 'en', 'Opening hours'), ('1312', 'en', 'Phone'), ('7101', 'en', 'Dubai'), ('7102', 'en', 'Al Quoz'),
            ('7103', 'en', '9-18\nFri off'), ('7200', 'en', 'Guests'), ('7201', 'en', 'Retail customers'), ('2242', 'en', 'Date')
            """);
        await ExecuteAsync(cs, """
            INSERT INTO shop_carts (user_id, session_id, checked_for_order, product_type, product_id, price, count_need,
              t2_manufacturer, t2_article, t2_article_show, t2_name, t2_price_purchase, t2_storage_id, t2_office_id, t2_time_to_exe, t2_time_to_exe_guaranteed, t2_product_json, t2_json_params) VALUES
            (5, 0, 1, 2, 0, 120, 2, 'BOSCH', '0986', '0 986', 'Brake pad', 80, 10, 1, 2, 5, '{"weight":"0.75"}', ''),
            (5, 0, 1, 2, 0, 50, 1, 'MANN', 'W712', 'W 712', 'Oil filter', 30, 20, 1, 1, 1, NULL, ''),
            (5, 0, 1, 2, 0, 40, 3, 'NGK', 'BKR6', 'BKR6', 'Spark plug', 20, 20, 1, 0, 0, NULL, ''),
            (5, 0, 1, 2, 0, 70, 1, 'GATES', 'K015', 'K015', 'Belt kit', 45, 30, 1, 3, 3, NULL, ''),
            (5, 0, 1, 1, 9, 150, 1, '', '', '', 'Catalogue stock item', 0, 0, 0, 0, 0, '{"weight":"1.5"}', '{"own":1}')
            """);
        var catalogueCart = await ScalarAsync(cs, "SELECT id FROM shop_carts WHERE product_type = 1");
        await ExecuteAsync(cs, "INSERT INTO shop_carts_details (cart_record_id, office_id, storage_id, storage_record_id, count_reserved, price_purchase) VALUES (" + catalogueCart + ", 1, 10, 500, 1, 60)");
    }

    private static readonly string[] ExtraSchema =
    [
        "ALTER TABLE shop_obtaining_modes ADD caption VARCHAR(255) NOT NULL DEFAULT '', ADD handler VARCHAR(64) NOT NULL DEFAULT ''",
        "ALTER TABLE shop_orders_statuses_ref ADD name VARCHAR(64) NOT NULL DEFAULT '', ADD `order` INT NOT NULL DEFAULT 0",
        "ALTER TABLE shop_orders_items_statuses_ref ADD name VARCHAR(64) NOT NULL DEFAULT '', ADD color VARCHAR(16) NOT NULL DEFAULT '', ADD count_flag TINYINT NOT NULL DEFAULT 1, ADD `order` INT NOT NULL DEFAULT 0",
        "ALTER TABLE shop_orders ADD paid_type INT NOT NULL DEFAULT 0",
        "CREATE TABLE shop_orders_paid_type (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, active TINYINT NOT NULL DEFAULT 1, `order` INT NOT NULL DEFAULT 0)",
        "CREATE TABLE users (user_id INT NOT NULL PRIMARY KEY, email VARCHAR(255) NOT NULL DEFAULT '', email_confirmed TINYINT NOT NULL DEFAULT 0, phone VARCHAR(64) NOT NULL DEFAULT '', phone_confirmed TINYINT NOT NULL DEFAULT 0, reg_variant INT NOT NULL DEFAULT 0)",
        "CREATE TABLE `groups` (id INT NOT NULL PRIMARY KEY, parent INT NOT NULL DEFAULT 0, for_backend TINYINT NOT NULL DEFAULT 0, for_guests TINYINT NOT NULL DEFAULT 0, value VARCHAR(255) NOT NULL DEFAULT '')",
        "CREATE TABLE users_groups_bind (user_id INT NOT NULL, group_id INT NOT NULL)",
        "CREATE TABLE shop_offices (id INT NOT NULL PRIMARY KEY, users TEXT NULL, city VARCHAR(64) NOT NULL DEFAULT '', address VARCHAR(255) NOT NULL DEFAULT '', timetable VARCHAR(255) NOT NULL DEFAULT '', phone VARCHAR(64) NOT NULL DEFAULT '')",
        "CREATE TABLE templates (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, is_frontend TINYINT NOT NULL, current TINYINT NOT NULL, data_value TEXT NULL)",
        "CREATE TABLE epc_portal_site_settings (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, host VARCHAR(255) NOT NULL, contact_json TEXT NULL)",
        "CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NOT NULL)",
    ];

    private static ServiceProvider Services(string cs, ICpPlatformMailer mailer)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IErpWriteConnectionFactory>(new WriteConnections(cs));
        services.AddSingleton(mailer);
        services.AddScoped<IErpVoucherNumberService, ErpVoucherNumberService>();
        services.AddScoped<IErpTaxAmountCalculator, ErpTaxAmountCalculator>();
        services.AddScoped<IErpAuditLogWriter, ErpAuditLogWriter>();
        services.AddScoped<IErpGlPostingService, ErpGlPostingService>();
        services.AddScoped<IErpSettlementAllocationService, ErpSettlementAllocationService>();
        services.AddScoped<IErpAdvanceVatService, ErpAdvanceVatService>();
        services.AddScoped<IErpCashWriteService, ErpCashWriteService>();
        services.AddScoped<IErpSalesInvoiceWriteService, ErpSalesInvoiceWriteService>();
        services.AddScoped<IErpInvoiceFromOrderWriteService, ErpInvoiceFromOrderWriteService>();
        services.AddScoped<IErpPurchaseInvoiceWriteService, ErpPurchaseInvoiceWriteService>();
        services.AddScoped<IErpSupplierWriteService, ErpSupplierWriteService>();
        services.AddScoped<IErpOrderFulfillmentWriteService, ErpOrderFulfillmentWriteService>();
        services.AddScoped<IStorefrontNotifyDispatcher, StorefrontNotifyDispatcher>();
        services.AddScoped<IStorefrontOrderNotificationService, StorefrontOrderNotificationService>();
        services.AddScoped<IStorefrontSupplierLpoNotifier, StorefrontSupplierLpoNotifier>();
        services.AddScoped<IStorefrontOrderCreatedPipeline, StorefrontOrderCreatedPipeline>();
        services.AddScoped<IStorefrontCheckoutWriteService, StorefrontCheckoutWriteService>();
        return services.BuildServiceProvider();
    }

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

    private sealed class CapturingMailer(string failFirstTo) : ICpPlatformMailer
    {
        private bool _failed;

        public List<(string To, string Subject, string Body, bool Delivered)> Attempts { get; } = [];

        public IReadOnlyDictionary<string, string> ReadConfig()
            => new Dictionary<string, string>
            {
                ["domain_path"] = "https://www.epartscart.com/",
                ["backend_dir"] = "cp",
                ["from_email"] = "noreply@epartscart.com",
            };

        public Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            var fail = !_failed && to == failFirstTo;
            _failed |= fail;
            Attempts.Add((to, subject, htmlBody, !fail));
            return Task.FromResult(fail ? CpSmsSendOutcome.Fail("421 try again") : new CpSmsSendOutcome(true, string.Empty));
        }
    }

    private sealed class WriteConnections(string connectionString) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }
}
