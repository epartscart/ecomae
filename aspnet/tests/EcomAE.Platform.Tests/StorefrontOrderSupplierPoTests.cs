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
/// PHP <c>ajax_checkout_create.php</c> tail: supplier LPO e-mail per warehouse (LPO number = customer order number)
/// and <c>epc_erp_order_fulfillment_bootstrap()</c> (sales order + one draft PO per supplier, own warehouse included).
/// </summary>
public sealed class StorefrontOrderSupplierPoTests
{
    [Theory]
    [InlineData("{\"order_email\":\" Buyer@Example.com \"}", "buyer@example.com", 0)]
    [InlineData("{\"order_email\":\"bad\",\"supplier_order_email\":\"po@x.ae\"}", "po@x.ae", 0)]
    [InlineData("{\"lpo_email\":\"lpo@x.ae\"}", "lpo@x.ae", 0)]
    [InlineData("{\"price_id\":\"7\"}", "", 7)]
    [InlineData("{\"price_id\":12}", "", 12)]
    [InlineData("not json", "", 0)]
    [InlineData("", "", 0)]
    public void OrderEmailFromOptions_FollowsPhpKeyOrder(string json, string email, long priceId)
    {
        var (found, price) = StorefrontSupplierLpoNotifier.OrderEmailFromOptions(json);
        Assert.Equal(email, found);
        Assert.Equal(priceId, price);
    }

    [Theory]
    [InlineData("a@b.ae", true)]
    [InlineData("Name <a@b.ae>", false)]
    [InlineData("a b@c.ae", false)]
    [InlineData("a@b", false)]
    [InlineData("", false)]
    public void IsPlainEmail_RejectsDisplayNamesLikeFilterValidateEmail(string value, bool ok)
        => Assert.Equal(ok, StorefrontSupplierLpoNotifier.IsPlainEmail(value));

    [Fact]
    public void Render_ReplacesOnlyDeclaredTextVars()
    {
        const string vars = "[{\"name\":\"lpo_number\",\"type\":\"text\"},{\"name\":\"storage_name\",\"type\":\"text\"},{\"name\":\"hidden\",\"type\":\"html\"}]";
        var rendered = StorefrontNotifyDispatcher.Render(
            "LPO #%lpo_number% — please supply parts (%storage_name%) %hidden% %missing%",
            vars,
            new Dictionary<string, string> { ["lpo_number"] = "42", ["storage_name"] = "Own", ["hidden"] = "x" });
        Assert.Equal("LPO #42 — please supply parts (Own) %hidden% %missing%", rendered);
    }

    [Fact]
    public void LpoHtml_UsesCustomerOrderNumberAndEscapes()
    {
        var html = StorefrontSupplierLpoNotifier.BuildLpoHtml(
            77,
            "A&B <Store>",
            [new("BOSCH", "0986'1", "Pad \"front\"", 2), new("X", "Y", "Zero", 0)],
            "https://www.epartscart.com");
        Assert.Contains("<tr><td style=\"padding:4px 16px 4px 0;font-weight:bold;\">LPO number</td><td>77</td></tr>", html, StringComparison.Ordinal);
        Assert.Contains("LPO / PO number <span style=\"color:#b45309;\">77</span>", html, StringComparison.Ordinal);
        Assert.Contains("A&amp;B &lt;Store&gt;", html, StringComparison.Ordinal);
        Assert.Contains("<td>0986&#039;1</td>", html, StringComparison.Ordinal);
        Assert.Contains("Pad &quot;front&quot;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Zero", html, StringComparison.Ordinal);
        Assert.Contains("https://www.epartscart.com</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Checkout_CreatesSupplierPosAndLpoMails_OnThrowawayDatabase_ThenDropped()
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

            await ExecuteAsync(cs, """
                INSERT INTO shop_storages (id, name, interface_type, connection_options, currency) VALUES
                (10, 'Own Warehouse Dubai', 1, '{"order_email":" Own.Stock@Example.com "}', 'AED'),
                (20, 'Supplier A Prices', 2, '{"price_id":"7"}', 'AED'),
                (30, 'Supplier B', 2, '{}', 'AED')
                """);
            await ExecuteAsync(cs, "INSERT INTO shop_docpart_prices (id, sender_email) VALUES (7, 'Supplier-A@Example.com')");
            await ExecuteAsync(cs, "INSERT INTO epc_erp_suppliers (storage_id, name) VALUES (20, 'Supplier A LLC')");
            await ExecuteAsync(cs, "INSERT INTO shop_obtaining_modes (id) VALUES (2)");
            await ExecuteAsync(cs, "INSERT INTO shop_orders_statuses_ref (id, for_created) VALUES (1, 1)");
            await ExecuteAsync(cs, "INSERT INTO shop_orders_items_statuses_ref (id, for_created) VALUES (1, 1)");
            await ExecuteAsync(cs, "INSERT INTO shop_currencies (iso_code, rate) VALUES ('AED', 1)");
            await ExecuteAsync(cs, "INSERT INTO shop_storages_data (id, storage_id, price, price_purchase) VALUES (500, 10, 90, 60)");
            await ExecuteAsync(cs, """
                INSERT INTO notifications_settings (name, email_on, sms_on, send_for_not_confirmed, email_subject, email_body, sms_body, vars) VALUES
                ('lpo_to_supplier', 1, 0, 1, 'LPO #%lpo_number% — please supply parts (%storage_name%)', '%order_text%', '',
                 '[{"name":"order_id","type":"text"},{"name":"lpo_number","type":"text"},{"name":"storage_name","type":"text"},{"name":"order_text","type":"text"}]')
                """);
            await ExecuteAsync(cs, """
                INSERT INTO shop_carts (user_id, session_id, checked_for_order, product_type, product_id, price, count_need,
                  t2_manufacturer, t2_article, t2_article_show, t2_name, t2_price_purchase, t2_storage_id, t2_json_params) VALUES
                (5, 0, 1, 2, 0, 120, 2, 'BOSCH', '0986', '0 986', 'Brake pad', 80, 10, ''),
                (5, 0, 1, 2, 0, 50, 1, 'MANN', 'W712', 'W 712', 'Oil filter', 30, 20, ''),
                (5, 0, 1, 2, 0, 40, 3, 'NGK', 'BKR6', 'BKR6', 'Spark plug', 20, 20, ''),
                (5, 0, 1, 2, 0, 70, 1, 'GATES', 'K015', 'K015', 'Belt kit', 45, 30, ''),
                (5, 0, 1, 1, 9, 150, 1, '', '', '', 'Catalogue stock item', 0, 0, '{"own":1}')
                """);
            var catalogueCart = await ScalarAsync(cs, "SELECT id FROM shop_carts WHERE product_type = 1");
            await ExecuteAsync(cs, "INSERT INTO shop_carts_details (cart_record_id, office_id, storage_id, storage_record_id, count_reserved, price_purchase) VALUES (" + catalogueCart + ", 1, 10, 500, 1, 60)");

            var mailer = new CapturingMailer(failFirstTo: "supplier-a@example.com");
            await using var provider = Services(cs, mailer);
            await using var scope = provider.CreateAsyncScope();
            var checkout = scope.ServiceProvider.GetRequiredService<IStorefrontCheckoutWriteService>();

            var result = await checkout.CreateAsync(5, new StorefrontCheckoutWriteRequest(HowGetMode: 2, UsersAgreement: true));
            Assert.True(result.Ok, result.Message);
            var orderId = result.OrderId;
            var no = orderId.ToString(CultureInfo.InvariantCulture);
            Assert.Contains("Supplier LPO e-mails: 2 sent, 1 skipped.", result.Message, StringComparison.Ordinal);
            Assert.Contains("with 3 supplier PO(s).", result.Message, StringComparison.Ordinal);

            Assert.Equal("{\"own\":1}", await ScalarAsync(cs, "SELECT t2_json_params FROM shop_orders_items WHERE product_type = 1"));
            Assert.Equal("0", await ScalarAsync(cs, "SELECT COUNT(*) FROM shop_carts"));

            Assert.Equal("1", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_erp_sales_orders WHERE shop_order_id = " + no + " AND status = 'confirmed' AND fulfillment_status = 'open'"));
            Assert.Equal("5", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_erp_sales_order_lines"));

            Assert.Equal("3", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_erp_purchase_orders WHERE order_id = " + no + " AND status = 'draft' AND notes = 'Customer order ref #" + no + "'"));
            Assert.Equal(
                "PO for order #" + no + " — Own Warehouse Dubai|PO for order #" + no + " — Supplier A LLC|PO for order #" + no + " — Supplier B",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(title ORDER BY title SEPARATOR '|') FROM epc_erp_purchase_orders"));
            Assert.Equal("2", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_erp_po_lines l JOIN epc_erp_purchase_orders p ON p.id = l.po_id JOIN epc_erp_suppliers s ON s.id = p.supplier_id WHERE s.storage_id = 20"));
            Assert.Equal("90.00", await ScalarAsync(cs, "SELECT amount_ex_vat FROM epc_erp_purchase_orders p JOIN epc_erp_suppliers s ON s.id = p.supplier_id WHERE s.storage_id = 20"));
            Assert.Equal("160.00", await ScalarAsync(cs, "SELECT amount_ex_vat FROM epc_erp_purchase_orders p JOIN epc_erp_suppliers s ON s.id = p.supplier_id WHERE s.storage_id = 10"));
            Assert.Equal("Own Warehouse Dubai", await ScalarAsync(cs, "SELECT name FROM epc_erp_suppliers WHERE storage_id = 10"));
            Assert.Equal("3", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_erp_audit_log WHERE action = 'po_from_shop_order'"));

            Assert.Equal(3, mailer.Attempts.Count);
            var delivered = mailer.Attempts.Where(a => a.Delivered).ToList();
            Assert.Equal(["own.stock@example.com", "supplier-a@example.com"], delivered.Select(a => a.To).ToArray());
            Assert.Equal("LPO #" + no + " — please supply parts (Own Warehouse Dubai)", delivered[0].Subject);
            Assert.Contains("LPO number</td><td style=\"border: 1px solid #cfcfcf; padding: 5px;\">" + no + "</td>", delivered[0].Body, StringComparison.Ordinal);
            Assert.Contains("Brake pad", delivered[0].Body, StringComparison.Ordinal);
            Assert.Contains("Catalogue stock item", delivered[0].Body, StringComparison.Ordinal);
            Assert.Contains("W 712", delivered[1].Body, StringComparison.Ordinal);
            Assert.Contains("Spark plug", delivered[1].Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Belt kit", delivered[1].Body, StringComparison.Ordinal);

            var logs = await ScalarAsync(cs, "SELECT GROUP_CONCAT(text ORDER BY id SEPARATOR '\n') FROM shop_orders_logs WHERE is_robot = 1");
            Assert.Equal(
                string.Join('\n',
                    "Supplier LPO to own.stock@example.com (Own Warehouse Dubai, LPO #" + no + "): sent",
                    "Supplier LPO to supplier-a@example.com (Supplier A Prices, LPO #" + no + "): sent",
                    "Supplier LPO skipped (no order e-mail): Supplier B [ID 30]"),
                logs);

            var again = await scope.ServiceProvider.GetRequiredService<IErpOrderFulfillmentWriteService>().BootstrapAsync(orderId, 5);
            Assert.True(again.Ok, again.Message);
            Assert.Equal("3", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_erp_purchase_orders"));
            Assert.Equal("1", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_erp_sales_orders"));

            var pipeline = scope.ServiceProvider.GetRequiredService<IStorefrontOrderCreatedPipeline>();
            var missing = await pipeline.RunAsync(999_999, 5);
            Assert.Equal("Shop order not found", missing.BootstrapSkipped);
            Assert.Equal(
                "ERP fulfillment bootstrap skipped: Shop order not found",
                await ScalarAsync(cs, "SELECT text FROM shop_orders_logs WHERE order_id = 999999 AND is_manager = 1"));
            Assert.Equal(
                StorefrontSupplierLpoNotifier.NoLinesLog,
                await ScalarAsync(cs, "SELECT text FROM shop_orders_logs WHERE order_id = 999999 AND is_manager = 0"));

            var guest = await pipeline.RunAsync(orderId, 0);
            Assert.Null(guest.BootstrapSkipped);
            Assert.Equal(0, guest.SalesOrderId);
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
        services.AddScoped<IStorefrontSupplierLpoNotifier, StorefrontSupplierLpoNotifier>();
        services.AddScoped<IStorefrontOrderCreatedPipeline, StorefrontOrderCreatedPipeline>();
        services.AddScoped<IStorefrontCheckoutWriteService, StorefrontCheckoutWriteService>();
        return services.BuildServiceProvider();
    }

    internal static readonly string[] Schema =
    [
        "CREATE TABLE users_profiles (user_id INT NOT NULL, data_key VARCHAR(64) NOT NULL, data_value TEXT NULL)",
        "CREATE TABLE shop_obtaining_modes (id INT NOT NULL PRIMARY KEY)",
        "CREATE TABLE shop_orders_statuses_ref (id INT NOT NULL PRIMARY KEY, for_created TINYINT NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_orders_items_statuses_ref (id INT NOT NULL PRIMARY KEY, for_created TINYINT NOT NULL DEFAULT 0)",
        """
        CREATE TABLE shop_carts (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, session_id INT NOT NULL DEFAULT 0,
          checked_for_order TINYINT NOT NULL DEFAULT 1, product_type INT NOT NULL, product_id INT NOT NULL DEFAULT 0,
          price DECIMAL(14,2) NOT NULL, count_need DECIMAL(14,3) NOT NULL,
          t2_manufacturer VARCHAR(64) NOT NULL DEFAULT '', t2_article VARCHAR(64) NOT NULL DEFAULT '', t2_article_show VARCHAR(64) NOT NULL DEFAULT '',
          t2_name VARCHAR(255) NOT NULL DEFAULT '', t2_exist INT NOT NULL DEFAULT 0, t2_time_to_exe INT NOT NULL DEFAULT 0,
          t2_time_to_exe_guaranteed INT NOT NULL DEFAULT 0, t2_storage VARCHAR(64) NOT NULL DEFAULT '', t2_min_order INT NOT NULL DEFAULT 1,
          t2_probability INT NOT NULL DEFAULT 0, t2_markup DECIMAL(14,2) NOT NULL DEFAULT 0, t2_price_purchase DECIMAL(14,2) NOT NULL DEFAULT 0,
          t2_office_id INT NOT NULL DEFAULT 0, t2_storage_id INT NOT NULL DEFAULT 0, t2_product_json TEXT NULL, t2_json_params TEXT NULL)
        """,
        """
        CREATE TABLE shop_carts_details (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, cart_record_id INT NOT NULL, office_id INT NOT NULL DEFAULT 0,
          storage_id INT NOT NULL DEFAULT 0, storage_record_id INT NOT NULL DEFAULT 0, count_reserved DECIMAL(14,3) NOT NULL DEFAULT 0,
          price_purchase DECIMAL(14,2) NOT NULL DEFAULT 0)
        """,
        """
        CREATE TABLE shop_orders (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, session_id INT NOT NULL DEFAULT 0, time INT NOT NULL,
          successfully_created TINYINT NOT NULL DEFAULT 0, status INT NOT NULL, paid TINYINT NOT NULL DEFAULT 0, how_get INT NOT NULL,
          how_get_json TEXT NULL, phone_not_auth VARCHAR(64) NOT NULL DEFAULT '', email_not_auth VARCHAR(255) NOT NULL DEFAULT '',
          office_id INT NOT NULL DEFAULT 0)
        """,
        """
        CREATE TABLE shop_orders_items (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, product_type INT NOT NULL, price DECIMAL(14,2) NOT NULL,
          count_need DECIMAL(14,3) NOT NULL, product_id INT NOT NULL DEFAULT 0, status INT NOT NULL,
          t2_manufacturer VARCHAR(64) NOT NULL DEFAULT '', t2_article VARCHAR(64) NOT NULL DEFAULT '', t2_article_show VARCHAR(64) NULL,
          t2_name VARCHAR(255) NOT NULL DEFAULT '', t2_exist INT NOT NULL DEFAULT 0, t2_time_to_exe INT NOT NULL DEFAULT 0,
          t2_time_to_exe_guaranteed INT NOT NULL DEFAULT 0, t2_storage VARCHAR(64) NOT NULL DEFAULT '', t2_min_order INT NOT NULL DEFAULT 1,
          t2_probability INT NOT NULL DEFAULT 0, t2_markup DECIMAL(14,2) NOT NULL DEFAULT 0, t2_price_purchase DECIMAL(14,2) NOT NULL DEFAULT 0,
          t2_office_id INT NOT NULL DEFAULT 0, t2_storage_id INT NOT NULL DEFAULT 0, t2_product_json TEXT NULL,
          sao_state INT NOT NULL DEFAULT 0, sao_robot INT NOT NULL DEFAULT 0, t2_json_params TEXT NULL)
        """,
        "CREATE TABLE shop_sao_states_types_link (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, state_id INT NOT NULL, is_start TINYINT NOT NULL, interface_type_id INT NOT NULL)",
        "CREATE TABLE shop_sao_states_types_actions_link (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, action_id INT NOT NULL, is_start TINYINT NOT NULL, state_type_id INT NOT NULL)",
        "CREATE TABLE shop_storages (id INT NOT NULL PRIMARY KEY, name VARCHAR(255) NOT NULL, interface_type INT NOT NULL DEFAULT 1, connection_options TEXT NULL, currency VARCHAR(8) NOT NULL DEFAULT 'AED')",
        "CREATE TABLE shop_currencies (iso_code VARCHAR(8) NOT NULL PRIMARY KEY, rate DECIMAL(14,6) NOT NULL)",
        "CREATE TABLE shop_storages_data (id INT NOT NULL PRIMARY KEY, storage_id INT NOT NULL, price DECIMAL(14,2) NOT NULL, price_purchase DECIMAL(14,2) NOT NULL)",
        """
        CREATE TABLE shop_orders_items_details (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, order_item_id INT NOT NULL, office_id INT NOT NULL DEFAULT 0,
          storage_id INT NOT NULL DEFAULT 0, storage_record_id INT NOT NULL DEFAULT 0, count_reserved DECIMAL(14,3) NOT NULL DEFAULT 0,
          count_issued DECIMAL(14,3) NOT NULL DEFAULT 0, count_canceled DECIMAL(14,3) NOT NULL DEFAULT 0, price_purchase DECIMAL(14,2) NOT NULL DEFAULT 0)
        """,
        "CREATE TABLE shop_orders_messages (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, is_customer TINYINT NOT NULL, text TEXT NOT NULL, time INT NOT NULL)",
        "CREATE TABLE shop_orders_logs (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, time INT NOT NULL, user_id INT NOT NULL, is_manager TINYINT NOT NULL, text TEXT NOT NULL, is_robot TINYINT NOT NULL DEFAULT 0)",
        "CREATE TABLE shop_docpart_garage (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, active TINYINT NOT NULL DEFAULT 1)",
        "CREATE TABLE shop_docpart_garage_orders (garage_id INT NOT NULL, order_id INT NOT NULL)",
        "CREATE TABLE shop_docpart_prices (id INT NOT NULL PRIMARY KEY, sender_email VARCHAR(255) NOT NULL DEFAULT '')",
        """
        CREATE TABLE notifications_settings (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, name VARCHAR(64) NOT NULL, email_on TINYINT NOT NULL, sms_on TINYINT NOT NULL,
          send_for_not_confirmed TINYINT NOT NULL, email_subject TEXT NOT NULL, email_body TEXT NOT NULL, sms_body TEXT NOT NULL, vars TEXT NOT NULL)
        """,
        """
        CREATE TABLE epc_erp_sales_orders (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, so_no VARCHAR(32) NOT NULL, customer_user_id INT NOT NULL DEFAULT 0, contact_id INT NOT NULL DEFAULT 0,
          title VARCHAR(255) NOT NULL DEFAULT '', amount_ex_vat DECIMAL(14,2) NOT NULL DEFAULT 0, vat_amount DECIMAL(14,2) NOT NULL DEFAULT 0,
          total_amount DECIMAL(14,2) NOT NULL DEFAULT 0, status ENUM('draft','confirmed','invoiced','cancelled') NOT NULL DEFAULT 'draft',
          sales_invoice_id INT NOT NULL DEFAULT 0, notes TEXT NULL, admin_id INT NOT NULL DEFAULT 0, time_created INT NOT NULL DEFAULT 0, time_updated INT NOT NULL DEFAULT 0)
        """,
        """
        CREATE TABLE epc_erp_sales_order_lines (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, sales_order_id INT NOT NULL, line_no INT NOT NULL DEFAULT 1, item_code VARCHAR(32) NOT NULL DEFAULT '',
          description VARCHAR(255) NOT NULL, qty DECIMAL(14,3) NOT NULL DEFAULT 1, unit_price_ex_vat DECIMAL(14,4) NOT NULL DEFAULT 0, line_ex_vat DECIMAL(14,2) NOT NULL DEFAULT 0)
        """,
        """
        CREATE TABLE epc_erp_purchase_orders (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, po_no VARCHAR(32) NOT NULL, supplier_id INT NOT NULL DEFAULT 0, title VARCHAR(255) NOT NULL DEFAULT '',
          amount_ex_vat DECIMAL(14,2) NOT NULL DEFAULT 0, vat_amount DECIMAL(14,2) NOT NULL DEFAULT 0, total_amount DECIMAL(14,2) NOT NULL DEFAULT 0,
          status ENUM('draft','approved','partial','received','cancelled') NOT NULL DEFAULT 'draft', purchase_id INT NOT NULL DEFAULT 0,
          order_id INT NOT NULL DEFAULT 0, approved_at INT NOT NULL DEFAULT 0, received_at INT NOT NULL DEFAULT 0, notes TEXT NULL,
          admin_id INT NOT NULL DEFAULT 0, time_created INT NOT NULL DEFAULT 0, time_updated INT NOT NULL DEFAULT 0,
          voucher_no VARCHAR(32) NOT NULL DEFAULT '', row_version INT NOT NULL DEFAULT 1)
        """,
        """
        CREATE TABLE epc_erp_suppliers (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, storage_id INT NULL, name VARCHAR(255) NOT NULL, contact_email VARCHAR(255) NULL,
          contact_phone VARCHAR(64) NULL, trn VARCHAR(64) NULL, currency_code VARCHAR(8) NOT NULL DEFAULT 'AED', active TINYINT(1) NOT NULL DEFAULT 1,
          time_created INT NOT NULL DEFAULT 0, country_code VARCHAR(8) NOT NULL DEFAULT 'AE', vat_registered TINYINT(1) NOT NULL DEFAULT 1,
          vendor_account VARCHAR(32) NULL, vendor_group VARCHAR(64) NULL, legal_entity_id INT NOT NULL DEFAULT 0, business_unit_id INT NOT NULL DEFAULT 0,
          registration_number VARCHAR(64) NULL, payment_terms VARCHAR(255) NULL, payment_method VARCHAR(64) NULL, delivery_terms VARCHAR(64) NULL,
          delivery_mode VARCHAR(64) NULL, credit_limit DECIMAL(14,2) NOT NULL DEFAULT 0, on_hold VARCHAR(16) NULL, tax_exempt TINYINT NOT NULL DEFAULT 0,
          bank_name VARCHAR(255) NULL, bank_account_number VARCHAR(64) NULL, iban VARCHAR(64) NULL, swift_bic VARCHAR(32) NULL,
          contact_person VARCHAR(255) NULL, website VARCHAR(255) NULL, address VARCHAR(255) NULL, city VARCHAR(128) NULL,
          state_region VARCHAR(128) NULL, postal_code VARCHAR(32) NULL, notes TEXT NULL)
        """,
        "CREATE TABLE epc_erp_voucher_sequences (voucher_type VARCHAR(8) NOT NULL, year INT NOT NULL, last_seq INT NOT NULL DEFAULT 0, PRIMARY KEY (voucher_type, year))",
        """
        CREATE TABLE epc_erp_audit_log (
          id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, time INT NOT NULL, admin_id INT NOT NULL, action VARCHAR(64) NOT NULL, entity_type VARCHAR(32) NOT NULL,
          entity_id INT NOT NULL, summary VARCHAR(512) NOT NULL, detail_json TEXT NULL, old_json TEXT NULL, new_json TEXT NULL,
          ip_address VARCHAR(64) NOT NULL DEFAULT '', user_agent VARCHAR(255) NOT NULL DEFAULT '')
        """,
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

    private sealed class CapturingMailer(string failFirstTo) : ICpPlatformMailer
    {
        private bool _failed;

        public List<(string To, string Subject, string Body, bool Delivered)> Attempts { get; } = [];

        public IReadOnlyDictionary<string, string> ReadConfig()
            => new Dictionary<string, string> { ["domain_path"] = "https://www.epartscart.com/" };

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
