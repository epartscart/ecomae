using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpDeskPhpAjaxTests
{
    [Fact]
    public async Task CustomerDocumentAndCrm_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        Assert.DoesNotContain("Database=docpart", connectionString, StringComparison.OrdinalIgnoreCase);
        try
        {
            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var staff = "admin_session=admin-token; admin_u_id=9";
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");

            var guest = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("action", "save_customer"), ("user_id", "7")), string.Empty);
            Assert.Equal("Access denied", guest.Json.RootElement.GetProperty("message").GetString());
            var noAction = await SendAsync(client, CpLegacyPhpAjaxLinks.UsersCustomerEndpoint, null, staff);
            Assert.Equal("No action", noAction.Json.RootElement.GetProperty("message").GetString());
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("action", "save_customer"), ("user_id", "7")), staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var missingBuyer = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "save_customer"), ("user_id", "7"), ("company", "Acme")), staff);
            Assert.Equal(StorefrontPhpAjax.BuyerProfilesMissing, missingBuyer.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_einvoice_buyer_profiles (
                  user_id INT NOT NULL PRIMARY KEY,
                  buyer_name VARCHAR(255) NOT NULL DEFAULT '',
                  trn VARCHAR(64) NOT NULL DEFAULT '',
                  tin VARCHAR(64) NOT NULL DEFAULT '',
                  legal_reg_no VARCHAR(64) NOT NULL DEFAULT '',
                  legal_reg_type VARCHAR(8) NOT NULL DEFAULT '',
                  authority_name VARCHAR(255) NOT NULL DEFAULT '',
                  address_line1 VARCHAR(255) NOT NULL DEFAULT '',
                  city VARCHAR(120) NOT NULL DEFAULT '',
                  emirate VARCHAR(120) NOT NULL DEFAULT '',
                  country_code VARCHAR(8) NOT NULL DEFAULT '',
                  phone VARCHAR(64) NOT NULL DEFAULT '',
                  email VARCHAR(120) NOT NULL DEFAULT '',
                  electronic_id VARCHAR(16) NOT NULL DEFAULT '',
                  peppol_endpoint VARCHAR(64) NOT NULL DEFAULT '',
                  buyer_onboarded TINYINT NOT NULL DEFAULT 0,
                  time_updated BIGINT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, "CREATE TABLE users_profiles (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, data_key VARCHAR(64) NOT NULL, data_value VARCHAR(255) NOT NULL)");
            var saved = await SendAsync(client, CpLegacyPhpAjaxLinks.UsersCustomerEndpoint, Form(
                ("csrf_guard_key", "admin-csrf"),
                ("action", "save_customer"),
                ("user_id", "7"),
                ("buyer_name", "Nora"),
                ("company", "Acme"),
                ("address_line1", "Dock 4"),
                ("city", "Dubai"),
                ("phone", "050"),
                ("trn", "100-200"),
                ("country_code", "ae")), staff);
            Assert.True(saved.Json.RootElement.GetProperty("status").GetBoolean(), saved.Body);
            Assert.Equal("Customer profile saved", saved.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("Acme", await ScalarAsync(connectionString, "SELECT data_value FROM users_profiles WHERE user_id = 7 AND data_key = 'company'"));
            Assert.Equal("AE", await ScalarAsync(connectionString, "SELECT data_value FROM users_profiles WHERE user_id = 7 AND data_key = 'epc_reg_country'"));
            Assert.Equal("100200", await ScalarAsync(connectionString, "SELECT data_value FROM users_profiles WHERE user_id = 7 AND data_key = 'epc_reg_trn'"));
            Assert.Equal("Nora", await ScalarAsync(connectionString, "SELECT buyer_name FROM epc_einvoice_buyer_profiles WHERE user_id = 7"));

            var advanceMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "customer_advance"), ("user_id", "7"), ("amount", "25.50")), staff);
            Assert.Equal(StorefrontPhpAjax.CustomerAccountingMissing, advanceMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE users (user_id INT NOT NULL PRIMARY KEY)");
            await ExecuteAsync(connectionString, "INSERT INTO users (user_id) VALUES (7)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_accounting_codes (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, income TINYINT NOT NULL, name VARCHAR(255) NOT NULL, manual_available TINYINT NOT NULL, `key` VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_users_accounting (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, time BIGINT NOT NULL, income TINYINT NOT NULL, amount DECIMAL(12,2) NOT NULL, operation_code INT NOT NULL, active TINYINT NOT NULL, office_id INT NOT NULL)");
            var advance = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "customer_advance"), ("user_id", "7"), ("amount", "25.50")), staff);
            Assert.True(advance.Json.RootElement.GetProperty("status").GetBoolean(), advance.Body);
            Assert.Equal("Customer advance recorded", advance.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("25.50", await ScalarAsync(connectionString, "SELECT CAST(amount AS CHAR) FROM shop_users_accounting WHERE user_id = 7"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(income AS CHAR) FROM shop_users_accounting WHERE user_id = 7"));
            var invoice = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "einvoice_create"), ("order_id", "3")), staff);
            Assert.False(invoice.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.EinvoiceNotPosted, invoice.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_einvoice_documents'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));

            var companyMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.DocumentEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "save_company"), ("legal_name", "Acme LLC")), staff);
            Assert.False(companyMissing.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Contains("missing", companyMissing.Json.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_document_company'"));
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_document_company (
                  id INT NOT NULL PRIMARY KEY,
                  legal_name VARCHAR(255) NOT NULL DEFAULT '',
                  trade_name VARCHAR(255) NOT NULL DEFAULT '',
                  address_line1 VARCHAR(255) NOT NULL DEFAULT '',
                  address_line2 VARCHAR(255) NOT NULL DEFAULT '',
                  city VARCHAR(120) NOT NULL DEFAULT '',
                  country VARCHAR(80) NOT NULL DEFAULT '',
                  trn VARCHAR(32) NOT NULL DEFAULT '',
                  phone VARCHAR(64) NOT NULL DEFAULT '',
                  email VARCHAR(120) NOT NULL DEFAULT '',
                  website VARCHAR(120) NOT NULL DEFAULT '',
                  logo_path VARCHAR(255) NOT NULL DEFAULT '',
                  bank_name VARCHAR(120) NOT NULL DEFAULT '',
                  bank_iban VARCHAR(64) NOT NULL DEFAULT '',
                  legal_footer VARCHAR(4000) NOT NULL DEFAULT '',
                  updated_at BIGINT NOT NULL DEFAULT 0,
                  row_version INT NOT NULL DEFAULT 1
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO epc_document_company (id, legal_name) VALUES (1, '')");
            var company = await SendAsync(client, CpLegacyPhpAjaxLinks.DocumentEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "save_company"), ("legal_name", "Acme LLC")), staff);
            Assert.True(company.Json.RootElement.GetProperty("status").GetBoolean(), company.Body);
            Assert.Equal("Company profile saved", company.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("Acme LLC", await ScalarAsync(connectionString, "SELECT legal_name FROM epc_document_company WHERE id = 1"));
            var template = await SendAsync(client, CpLegacyPhpAjaxLinks.DocumentEndpoint, Form(("csrf_guard_key", "admin-csrf"), ("action", "save_template")), staff);
            Assert.Equal("Template code required", template.Json.RootElement.GetProperty("message").GetString());

            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_crm_leads (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  company VARCHAR(255) NOT NULL,
                  contact_name VARCHAR(255) NOT NULL,
                  email VARCHAR(255) NOT NULL,
                  phone VARCHAR(64) NOT NULL,
                  source VARCHAR(64) NOT NULL,
                  status VARCHAR(32) NOT NULL,
                  owner_user_id INT NOT NULL,
                  expected_value DECIMAL(12,2) NOT NULL,
                  notes TEXT NOT NULL,
                  time_created BIGINT NOT NULL,
                  time_updated BIGINT NOT NULL
                )
                """);
            var lead = await SendAsync(client, CpLegacyPhpAjaxLinks.CrmEndpoint, Form(("action", "save_lead"), ("company", "Acme"), ("contact_name", "Nora")), staff);
            Assert.True(lead.Json.RootElement.GetProperty("status").GetBoolean(), lead.Body);
            Assert.Equal("Lead saved", lead.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("Acme", await ScalarAsync(connectionString, "SELECT company FROM epc_crm_leads WHERE contact_name = 'Nora'"));
            var crmQuiet = await SendAsync(client, CpLegacyPhpAjaxLinks.CrmEndpoint, null, staff);
            Assert.Equal("No action", crmQuiet.Json.RootElement.GetProperty("message").GetString());
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task SaoExec_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var configRoot = Path.Combine(Path.GetTempPath(), "ecomae-sao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configRoot);
        File.WriteAllText(Path.Combine(configRoot, "config.php"), """
            <?php
            class DP_Config {
            public $tech_key = 'local-tech';
            }
            """);
        try
        {
            await using (var closed = await StartAsync(connectionString, configRoot, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var wrong = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.SaoExec + "?key=nope&order_item_id=12&sao_action_id=3", null, string.Empty);
                Assert.Equal(StorefrontPhpAjax.OrderItemWrongKey, wrong.Json.RootElement.GetProperty("message").GetString());
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=3", null, string.Empty);
                Assert.Equal(StorefrontPhpAjax.NoDbConnect, offline.Body);
            }

            await using var host = await StartAsync(connectionString, configRoot);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var staff = "admin_session=admin-token; admin_u_id=9";
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=3", null, staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=3&csrf_guard_key=admin-csrf", null, staff);
            Assert.Equal(StorefrontPhpAjax.SaoActionsMissing, missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'shop_sao%'"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_items (id INT NOT NULL PRIMARY KEY, t2_storage_id INT NOT NULL, sao_state INT NOT NULL, sao_message VARCHAR(255) NOT NULL DEFAULT '')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items (id, t2_storage_id, sao_state) VALUES (12, 4, 8)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages (id INT NOT NULL PRIMARY KEY, interface_type INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, interface_type) VALUES (4, 2)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_states_types_link (id INT NOT NULL PRIMARY KEY, interface_type_id INT NOT NULL, state_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_states_types_link (id, interface_type_id, state_id) VALUES (5, 2, 8)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_states_types_actions_link (id INT NOT NULL PRIMARY KEY, state_type_id INT NOT NULL, action_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_states_types_actions_link (id, state_type_id, action_id) VALUES (1, 5, 3)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_actions (id INT NOT NULL PRIMARY KEY, script VARCHAR(255) NOT NULL, name VARCHAR(255) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_actions (id, script, name) VALUES (3, 'do.php', 'Do')");

            var blocked = await SendAsync(client, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=9&csrf_guard_key=admin-csrf", null, staff);
            Assert.False(blocked.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("12", blocked.Json.RootElement.GetProperty("order_item_id").GetString());
            Assert.Equal(StorefrontPhpAjax.SaoAlreadyDone, blocked.Json.RootElement.GetProperty("sao_action_message").GetString());
            var refused = await SendAsync(client, CpLegacyPhpAjaxLinks.SaoExec + "?key=local-tech&order_item_id=12&sao_action_id=3&csrf_guard_key=admin-csrf", null, staff);
            Assert.False(refused.Json.RootElement.GetProperty("status").GetBoolean(), refused.Body);
            Assert.Equal(StorefrontPhpAjax.SaoScriptNotExecuted, refused.Json.RootElement.GetProperty("sao_action_message").GetString());
            Assert.Equal(string.Empty, await ScalarAsync(connectionString, "SELECT sao_message FROM shop_orders_items WHERE id = 12"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
            if (Directory.Exists(configRoot))
            {
                Directory.Delete(configRoot, true);
            }
        }
    }

    [Fact]
    public async Task OrderPayRefund_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.OrderPayRefund, Form(("csrf_guard_key", "admin-csrf"), ("order_id", "12"), ("direct_refund", "1")), staff);
                Assert.Equal(StorefrontPhpAjax.NoDbConnect, offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.OrderPayRefund, Form(("order_id", "12"), ("direct_refund", "1")), staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var fields = await SendAsync(client, CpLegacyPhpAjaxLinks.OrderPayRefund, Form(("csrf_guard_key", "admin-csrf"), ("order_id", "12")), staff);
            Assert.Equal("Forbidden", fields.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, CpLegacyPhpAjaxLinks.OrderPayRefund, Form(("csrf_guard_key", "admin-csrf"), ("order_id", "12"), ("direct_refund", "1")), staff);
            Assert.Equal(StorefrontPhpAjax.OrdersMissing, missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_orders'"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders (id INT NOT NULL PRIMARY KEY, paid INT NOT NULL, user_id INT NOT NULL, office_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders (id, paid, user_id, office_id) VALUES (12, 0, 7, 3)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_offices (id INT NOT NULL PRIMARY KEY, users VARCHAR(255) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices (id, users) VALUES (3, '[\"8\"]')");
            var office = await SendAsync(client, CpLegacyPhpAjaxLinks.OrderPayRefund, Form(("csrf_guard_key", "admin-csrf"), ("order_id", "12"), ("direct_refund", "1")), staff);
            Assert.Equal("Forbidden", office.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "UPDATE shop_offices SET users = '[\"9\"]' WHERE id = 3");
            var unpaid = await SendAsync(client, CpLegacyPhpAjaxLinks.OrderPayRefund, Form(("csrf_guard_key", "admin-csrf"), ("order_id", "12"), ("direct_refund", "1")), staff);
            Assert.Equal("Order is not paid.", unpaid.Json.RootElement.GetProperty("message").GetString());

            await ExecuteAsync(connectionString, "UPDATE shop_orders SET paid = 1 WHERE id = 12");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_users_accounting (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, time BIGINT NOT NULL, income TINYINT NOT NULL, amount DECIMAL(12,2) NOT NULL, operation_code INT NOT NULL, active TINYINT NOT NULL, order_id INT NOT NULL, office_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_users_accounting (user_id, time, income, amount, operation_code, active, order_id, office_id) VALUES (7, 1, 0, 25.50, 1, 1, 12, 3)");
            var codes = await SendAsync(client, CpLegacyPhpAjaxLinks.OrderPayRefund, Form(("csrf_guard_key", "admin-csrf"), ("order_id", "12"), ("direct_refund", "1")), staff);
            Assert.Equal(StorefrontPhpAjax.RefundAccountingMissing, codes.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_accounting_codes'"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_accounting_codes (id INT NOT NULL PRIMARY KEY, `key` VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_accounting_codes (id, `key`) VALUES (5, '5_refund_from_order_to_balance'), (6, '6_refund_from_balance')");
            var refund = await SendAsync(client, CpLegacyPhpAjaxLinks.OrderPayRefund, Form(("csrf_guard_key", "admin-csrf"), ("order_id", "12"), ("direct_refund", "1")), staff);
            Assert.True(refund.Json.RootElement.GetProperty("status").GetBoolean(), refund.Body);
            Assert.Equal(string.Empty, refund.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT CAST(paid AS CHAR) FROM shop_orders WHERE id = 12"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(income AS CHAR) FROM shop_users_accounting WHERE order_id = 12 AND operation_code = 5"));
            Assert.Equal("25.50", await ScalarAsync(connectionString, "SELECT CAST(amount AS CHAR) FROM shop_users_accounting WHERE order_id = 12 AND operation_code = 5"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT CAST(income AS CHAR) FROM shop_users_accounting WHERE order_id = 0 AND operation_code = 6"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task OrdersOms_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.OrdersOms, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_item_status"), ("order_id", "12")), staff);
                Assert.Equal("DB unavailable", offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersOms, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_item_status"), ("order_id", "12")), staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersOms, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_item_status"), ("order_id", "12")), string.Empty);
            Assert.Equal("Forbidden", guest.Json.RootElement.GetProperty("message").GetString());
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersOms, Form(("action", "set_item_status"), ("order_id", "12")), staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var invalid = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersOms, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_item_status"), ("order_id", "0")), staff);
            Assert.Equal("Invalid order", invalid.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersOms, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_item_status"), ("order_id", "12"), ("item_id", "4"), ("status", "2")), staff);
            Assert.Equal(StorefrontPhpAjax.OrdersMissing, missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_orders'"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders (id INT NOT NULL PRIMARY KEY, paid INT NOT NULL, user_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders (id, paid, user_id) VALUES (12, 1, 7)");
            var unknown = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersOms, Form(("csrf_guard_key", "admin-csrf"), ("action", "nope"), ("order_id", "12")), staff);
            Assert.Equal("Unknown action", unknown.Json.RootElement.GetProperty("message").GetString());
            var map = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersOms, Form(("csrf_guard_key", "admin-csrf"), ("action", "erp_document_map"), ("order_id", "12")), staff);
            Assert.False(map.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.OmsErpMapNotRead, map.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));

            var items = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersOms, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_item_status"), ("order_id", "12"), ("item_id", "4"), ("status", "2")), staff);
            Assert.Equal(StorefrontPhpAjax.OrderItemsMissing, items.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_items (id INT NOT NULL PRIMARY KEY, order_id INT NOT NULL, status INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items (id, order_id, status) VALUES (4, 12, 1)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_logs (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, time BIGINT NOT NULL, user_id INT NOT NULL, is_manager TINYINT NOT NULL, text VARCHAR(255) NOT NULL, is_robot TINYINT NOT NULL)");
            var status = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersOms, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_item_status"), ("order_id", "12"), ("item_id", "4"), ("status", "2")), staff);
            Assert.True(status.Json.RootElement.GetProperty("status").GetBoolean(), status.Body);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT CAST(status AS CHAR) FROM shop_orders_items WHERE id = 4"));
            Assert.Contains("status to 2", await ScalarAsync(connectionString, "SELECT text FROM shop_orders_logs WHERE order_id = 12"), StringComparison.Ordinal);
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MultivendorIngest_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.MultivendorIngest + "?action=sample", null, staff);
                Assert.Equal(StorefrontPhpAjax.NoDbConnectChange, offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.MultivendorIngest, Form(("action", "sample")), staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var sample = await SendAsync(client, CpLegacyPhpAjaxLinks.MultivendorIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "sample")), staff);
            Assert.True(sample.Json.RootElement.GetProperty("status").GetBoolean(), sample.Body);
            Assert.Equal("epc-multivendor-sample.csv", sample.Json.RootElement.GetProperty("filename").GetString());
            Assert.Contains("TOYOTA,446610010", sample.Json.RootElement.GetProperty("csv").GetString(), StringComparison.Ordinal);
            var aclMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.MultivendorIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "min_price_acl_get")), staff);
            Assert.True(aclMissing.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.True(aclMissing.Json.RootElement.GetProperty("acl").GetProperty("restrict").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_mv_min_price_acl'"));
            var saveMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.MultivendorIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "min_price_acl_save"), ("restrict", "0"), ("group_ids", "[3]"), ("user_ids", "[7]")), staff);
            Assert.Contains("missing", saveMissing.Json.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_mv_min_price_acl'"));

            await ExecuteAsync(connectionString, "CREATE TABLE epc_mv_min_price_acl (id INT NOT NULL PRIMARY KEY, restrict_min TINYINT NOT NULL, group_ids_json TEXT NULL, user_ids_json TEXT NULL, updated_at BIGINT NOT NULL, updated_by INT NOT NULL)");
            var saved = await SendAsync(client, CpLegacyPhpAjaxLinks.MultivendorIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "min_price_acl_save"), ("restrict", "0"), ("group_ids", "[3]"), ("user_ids", "[7]")), staff);
            Assert.True(saved.Json.RootElement.GetProperty("status").GetBoolean(), saved.Body);
            Assert.Equal("Minimum price access saved", saved.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT CAST(restrict_min AS CHAR) FROM epc_mv_min_price_acl WHERE id = 1"));
            Assert.Equal("[3]", await ScalarAsync(connectionString, "SELECT group_ids_json FROM epc_mv_min_price_acl WHERE id = 1"));
            Assert.Equal("[7]", await ScalarAsync(connectionString, "SELECT user_ids_json FROM epc_mv_min_price_acl WHERE id = 1"));

            var vendorsMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.MultivendorIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "vendor_codes_list")), staff);
            Assert.Equal(0, vendorsMissing.Json.RootElement.GetProperty("count").GetInt32());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_storages'"));
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages (id INT NOT NULL PRIMARY KEY, name VARCHAR(255) NOT NULL, short_name VARCHAR(64) NOT NULL, hidden TINYINT NOT NULL, connection_options TEXT NULL, interface_type INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, name, short_name, hidden, connection_options, interface_type) VALUES (4, 'Old Vendor', 'OLD', 0, '{}', 2)");
            var renamed = await SendAsync(client, CpLegacyPhpAjaxLinks.MultivendorIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "vendor_code_save"), ("storage_id", "4"), ("vendor_code", "S-UAE"), ("vendor_full", "Gulf Parts")), staff);
            Assert.True(renamed.Json.RootElement.GetProperty("status").GetBoolean(), renamed.Body);
            Assert.Equal("S-UAE", await ScalarAsync(connectionString, "SELECT short_name FROM shop_storages WHERE id = 4"));
            Assert.Equal("Gulf Parts", await ScalarAsync(connectionString, "SELECT name FROM shop_storages WHERE id = 4"));
            var listed = await SendAsync(client, CpLegacyPhpAjaxLinks.MultivendorIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "vendor_codes_list")), staff);
            Assert.Equal(1, listed.Json.RootElement.GetProperty("count").GetInt32());
            Assert.Equal("S-UAE", listed.Json.RootElement.GetProperty("vendors")[0].GetProperty("vendor_code").GetString());
            var upload = await SendAsync(client, CpLegacyPhpAjaxLinks.MultivendorIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "upload")), staff);
            Assert.Equal(StorefrontPhpAjax.MultivendorChooseFile, upload.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_storages"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task CommerceIngest_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("action", "list_sources")), staff);
                Assert.Equal(StorefrontPhpAjax.NoDbConnectChange, offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("action", "list_sources")), staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "list_sources")), staff);
            Assert.True(missing.Json.RootElement.GetProperty("status").GetBoolean(), missing.Body);
            Assert.Equal(0, missing.Json.RootElement.GetProperty("count").GetInt32());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_docpart_prices'"));
            var refreshMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "refresh_url"), ("price_id", "2")), staff);
            Assert.Equal(StorefrontPhpAjax.CommercePricesMissing, refreshMissing.Json.RootElement.GetProperty("message").GetString());
            var none = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "refresh_all")), staff);
            Assert.Equal("No commerce URL-linked lists found", none.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_docpart_prices'"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_prices (id INT NOT NULL PRIMARY KEY, name VARCHAR(255) NOT NULL, link VARCHAR(512) NULL, load_mode INT NOT NULL, message_header_substring TEXT NULL, last_updated BIGINT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices (id, name, link, load_mode, message_header_substring, last_updated) VALUES (1, 'Gulf-S', '', 1, '', 11), (3, 'Stock-L', 'ftp://files.example/s.csv', 4, '', 33), (4, 'Spare-S', 'https://example.test/s.csv', 4, '', 44), (5, 'Orphan', 'https://example.test/x.csv', 4, '', 55)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices (id, name, link, load_mode, message_header_substring, last_updated) VALUES (2, 'Parts.P', 'https://example.test/p.csv', 4, 'EPC_COMMERCE:{\"role\":\"purchase\",\"base\":\"PartsBook\",\"margin\":12.5}', 22)");
            var listed = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "list_sources")), staff);
            Assert.Equal(4, listed.Json.RootElement.GetProperty("count").GetInt32());
            var parts = SourceNamed(listed.Json.RootElement, "Parts.P");
            Assert.Equal("purchase", parts.GetProperty("role").GetString());
            Assert.Equal("PartsBook", parts.GetProperty("base_name").GetString());
            Assert.Equal(12.5, parts.GetProperty("margin_percent").GetDouble());
            Assert.True(parts.GetProperty("has_url").GetBoolean());
            Assert.Equal(0, parts.GetProperty("records_count").GetInt32());
            var stock = SourceNamed(listed.Json.RootElement, "Stock-L");
            Assert.Equal("inventory", stock.GetProperty("role").GetString());
            Assert.False(stock.GetProperty("has_url").GetBoolean());
            Assert.Equal("Gulf", SourceNamed(listed.Json.RootElement, "Gulf-S").GetProperty("base_name").GetString());
            var urls = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "list_sources"), ("url_only", "1")), staff);
            Assert.Equal(2, urls.Json.RootElement.GetProperty("count").GetInt32());
            Assert.Equal("Parts.P", urls.Json.RootElement.GetProperty("sources")[0].GetProperty("price_name").GetString());
            Assert.Equal("Spare-S", urls.Json.RootElement.GetProperty("sources")[1].GetProperty("price_name").GetString());
            var required = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "refresh_url"), ("price_id", "0")), staff);
            Assert.Equal("price_id required", required.Json.RootElement.GetProperty("message").GetString());
            var absent = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "refresh_url"), ("price_id", "99")), staff);
            Assert.Equal("Price list not found", absent.Json.RootElement.GetProperty("message").GetString());
            var unlinked = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "refresh_url"), ("price_id", "1")), staff);
            Assert.Equal("No http(s) link on this price list", unlinked.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(1, unlinked.Json.RootElement.GetProperty("price_id").GetInt32());
            var role = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "refresh_url"), ("price_id", "5")), staff);
            Assert.Equal("Cannot detect commerce role from list name Orphan", role.Json.RootElement.GetProperty("message").GetString());
            var stayed = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "refresh_url"), ("price_id", "2")), staff);
            Assert.Equal(StorefrontPhpAjax.CommerceUrlStaysClassic, stayed.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("22", await ScalarAsync(connectionString, "SELECT CAST(last_updated AS CHAR) FROM shop_docpart_prices WHERE id = 2"));
            var refreshAll = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "refresh_all")), staff);
            Assert.Equal(StorefrontPhpAjax.CommerceUrlStaysClassic, refreshAll.Json.RootElement.GetProperty("message").GetString());
            Assert.False(refreshAll.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(2, refreshAll.Json.RootElement.GetProperty("total").GetInt32());
            Assert.Equal("22", await ScalarAsync(connectionString, "SELECT CAST(last_updated AS CHAR) FROM shop_docpart_prices WHERE id = 2"));
            Assert.Equal("44", await ScalarAsync(connectionString, "SELECT CAST(last_updated AS CHAR) FROM shop_docpart_prices WHERE id = 4"));
            var choose = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("action", "upload")), staff);
            Assert.Equal(StorefrontPhpAjax.CommerceChooseFile, choose.Json.RootElement.GetProperty("message").GetString());
            var ingest = await SendAsync(client, CpLegacyPhpAjaxLinks.CommerceIngest, Form(("csrf_guard_key", "admin-csrf"), ("source_url", "https://example.test/new.csv")), staff);
            Assert.Equal(StorefrontPhpAjax.CommerceFileStaysClassic, ingest.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("5", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_prices"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task CrossCp_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        var link = "{\"action\":\"add_cross_link\",\"article\":\"04465-YZZD2\",\"manufacturer\":\"TOYOTA\",\"ref_article\":\"446610010\",\"ref_brand\":\"AISIN\"}";
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.CrossCp, Form(("request_object", link)), staff);
                Assert.Equal(StorefrontPhpAjax.NoDbConnect, offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("request_object", link)), staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", link)), string.Empty);
            Assert.Equal("Access denied", guest.Json.RootElement.GetProperty("message").GetString());
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("request_object", link)), staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var unknown = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"other\"}")), staff);
            Assert.Equal("Unknown action", unknown.Json.RootElement.GetProperty("message").GetString());
            var lookup = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"lookup_crosses\",\"article\":\"04465-YZZD2\",\"manufacturer\":\"TOYOTA\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.CrossSearchStaysClassic, lookup.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_docpart_articles_analogs_list'"));
            var missing = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", link)), staff);
            Assert.Equal(StorefrontPhpAjax.CrossLinksMissing, missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_docpart_articles_analogs_list'"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_articles_analogs_list (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, article VARCHAR(255) NOT NULL, article_search VARCHAR(255) NULL, manufacturer_article VARCHAR(255) NOT NULL, analog VARCHAR(255) NOT NULL, analog_search VARCHAR(255) NULL, manufacturer_analog VARCHAR(255) NOT NULL)");
            var added = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", link)), staff);
            Assert.True(added.Json.RootElement.GetProperty("status").GetBoolean(), added.Body);
            Assert.Equal(1, added.Json.RootElement.GetProperty("inserted").GetInt32());
            Assert.Equal(0, added.Json.RootElement.GetProperty("already").GetInt32());
            Assert.Equal(2, added.Json.RootElement.GetProperty("cp_links_for_article").GetInt32());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_articles_analogs_list WHERE article = '04465YZZD2' AND manufacturer_article = 'TOYOTA' AND analog = '446610010' AND manufacturer_analog = 'AISIN'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_articles_analogs_list WHERE article = '446610010' AND manufacturer_article = 'AISIN' AND analog = '04465YZZD2' AND manufacturer_analog = 'TOYOTA'"));
            var again = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", link)), staff);
            Assert.Equal(1, again.Json.RootElement.GetProperty("already").GetInt32());
            Assert.Equal("already_linked", again.Json.RootElement.GetProperty("reason").GetString());
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_articles_analogs_list"));
            var same = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"add_cross_link\",\"article\":\"04465-YZZD2\",\"manufacturer\":\"TOYOTA\",\"ref_article\":\"04465-YZZD2\",\"ref_brand\":\"TOYOTA\"}")), staff);
            Assert.Equal("same_part_same_brand", same.Json.RootElement.GetProperty("reason").GetString());
            var bulk = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"add_cross_bulk\",\"article\":\"04465-YZZD2\",\"manufacturer\":\"TOYOTA\",\"references\":[{\"article\":\"0986AF0078\",\"brand\":\"BOSCH\"},{\"article\":\"446610010\",\"brand\":\"AISIN\"}]}")), staff);
            Assert.Equal(1, bulk.Json.RootElement.GetProperty("inserted").GetInt32());
            Assert.Equal(1, bulk.Json.RootElement.GetProperty("already").GetInt32());
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_articles_analogs_list"));
            var repair = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"repair_empty_brands\",\"article\":\"04465-YZZD2\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.CrossRepairStaysClassic, repair.Json.RootElement.GetProperty("message").GetString());
            var catalog = await SendAsync(client, CpLegacyPhpAjaxLinks.CrossCp, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"import_full_catalog\",\"article\":\"04465-YZZD2\",\"manufacturer\":\"TOYOTA\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.CrossCatalogStaysClassic, catalog.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_articles_analogs_list"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task ContentJsonList_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var configRoot = Path.Combine(Path.GetTempPath(), "ecomae-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configRoot);
        File.WriteAllText(Path.Combine(configRoot, "config.php"), """
            <?php
            class DP_Config {
            public $secret_succession = 'local-secret';
            public $list_page_limit = '1';
            }
            """);
        var staff = "admin_session=admin-token; admin_u_id=9";
        var first = CpLegacyPhpAjaxLinks.ContentJsonList + "?code=local-secret&csrf_guard_key=admin-csrf&is_frontend=1&s_page=0&content_id=1";
        try
        {
            await using (var closed = await StartAsync(connectionString, configRoot, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var forbidden = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.ContentJsonList + "?code=nope", null, staff);
                Assert.Equal("Forbidden", forbidden.Json.RootElement.GetProperty("message").GetString());
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.ContentJsonList + "?code=local-secret", null, staff);
                Assert.Equal(StorefrontPhpAjax.NoDbConnect, offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString, configRoot);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, CpLegacyPhpAjaxLinks.ContentJsonList + "?code=local-secret&csrf_guard_key=admin-csrf&is_frontend=1&s_page=0", null, staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.ContentJsonList + "?code=local-secret&is_frontend=1&s_page=0", null, staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, first, null, staff);
            Assert.Equal(StorefrontPhpAjax.ContentMissing, missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'content'"));

            await ExecuteAsync(connectionString, "CREATE TABLE content (id INT NOT NULL PRIMARY KEY, value VARCHAR(255) NULL, level INT NOT NULL, parent INT NOT NULL, is_frontend INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO content (id, value, level, parent, is_frontend) VALUES (1, 'Home', 1, 0, 1), (2, 'About', 2, 1, 1), (3, '10', 1, 0, 1), (4, 'Backend', 1, 0, 0)");
            await ExecuteAsync(connectionString, "CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value VARCHAR(255) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES ('10', 'en', 'Shop')");
            var page = await SendAsync(client, first, null, staff);
            Assert.True(page.Json.RootElement.GetProperty("status").GetBoolean(), page.Body);
            Assert.Equal(2, page.Json.RootElement.GetProperty("max_level").GetInt32());
            Assert.Equal(2, page.Json.RootElement.GetProperty("count_total_for_pagination").GetInt32());
            Assert.Equal(3, page.Json.RootElement.GetProperty("count_total").GetInt32());
            Assert.Equal(1, page.Json.RootElement.GetProperty("list_page_limit").GetInt32());
            Assert.Equal(2, page.Json.RootElement.GetProperty("content").GetArrayLength());
            Assert.Equal(1, page.Json.RootElement.GetProperty("content")[0].GetProperty("l1_id").GetInt32());
            Assert.Equal("Home", page.Json.RootElement.GetProperty("content")[0].GetProperty("l1_value").GetString());
            Assert.Equal(2, page.Json.RootElement.GetProperty("content")[0].GetProperty("l2_id").GetInt32());
            Assert.Equal("About", page.Json.RootElement.GetProperty("content")[1].GetProperty("l2_value").GetString());
            var next = await SendAsync(client, CpLegacyPhpAjaxLinks.ContentJsonList + "?code=local-secret&csrf_guard_key=admin-csrf&is_frontend=1&s_page=1&content_id=1", null, staff);
            Assert.Equal(1, next.Json.RootElement.GetProperty("content").GetArrayLength());
            Assert.Equal("Shop", next.Json.RootElement.GetProperty("content")[0].GetProperty("l1_value").GetString());
            Assert.Equal(2, next.Json.RootElement.GetProperty("max_level").GetInt32());
            var backend = await SendAsync(client, CpLegacyPhpAjaxLinks.ContentJsonList + "?code=local-secret&csrf_guard_key=admin-csrf&is_frontend=0&s_page=0", null, staff);
            Assert.Equal("Backend", backend.Json.RootElement.GetProperty("content")[0].GetProperty("l1_value").GetString());
            Assert.Equal(1, backend.Json.RootElement.GetProperty("count_total").GetInt32());
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM content"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
            if (Directory.Exists(configRoot))
            {
                Directory.Delete(configRoot, true);
            }
        }
    }

    [Fact]
    public async Task NotificationTest_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var configRoot = Path.Combine(Path.GetTempPath(), "ecomae-notify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configRoot);
        File.WriteAllText(Path.Combine(configRoot, "config.php"), """
            <?php
            class DP_Config {
            public $secret_succession = 'local-secret';
            }
            """);
        var staff = "admin_session=admin-token; admin_u_id=9";
        try
        {
            await using (var closed = await StartAsync(connectionString, configRoot, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.NotificationTest, Form(("type", "email"), ("contact", "ops@example.test")), staff);
                Assert.Equal(StorefrontPhpAjax.NoDbConnect, offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString, configRoot);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "email"), ("contact", "ops@example.test")), staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "email"), ("contact", "ops@example.test")), string.Empty);
            Assert.Equal("Forbidden", guest.Json.RootElement.GetProperty("message").GetString());
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("type", "email"), ("contact", "ops@example.test")), staff);
            Assert.Equal("Error! CSRF 1", csrf.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("contact", "ops@example.test")), staff);
            Assert.Equal("No params", missing.Json.RootElement.GetProperty("message").GetString());
            var fax = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "fax"), ("contact", "ops@example.test")), staff);
            Assert.Equal("Incorrect type", fax.Json.RootElement.GetProperty("message").GetString());
            var unavailable = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "email"), ("contact", "ops@example.test")), staff);
            Assert.Equal("Tenant database unavailable.", unavailable.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'reg_fields'"));

            await ExecuteAsync(connectionString, "CREATE TABLE reg_fields (name VARCHAR(32) NOT NULL, `regexp` VARCHAR(255) NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO reg_fields (name, `regexp`) VALUES ('email', '^desk@only$')");
            var format = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "email"), ("contact", "ops@example.test")), staff);
            Assert.Equal("The contact does not match the required format.", format.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "UPDATE reg_fields SET `regexp` = '^.+@.+$' WHERE name = 'email'");
            var noTemplate = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "email"), ("contact", "ops@example.test")), staff);
            Assert.Equal("Tenant database unavailable.", noTemplate.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'notifications_settings'"));

            await ExecuteAsync(connectionString, "CREATE TABLE notifications_settings (name VARCHAR(64) NOT NULL, email_on INT NOT NULL, sms_on INT NOT NULL, send_for_not_confirmed INT NOT NULL, email_subject VARCHAR(255) NULL, email_body TEXT NULL, sms_body TEXT NULL)");
            var absent = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "email"), ("contact", "ops@example.test")), staff);
            Assert.Equal("The email test notification template is not configured.", absent.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "INSERT INTO notifications_settings (name, email_on, sms_on, send_for_not_confirmed, email_subject, email_body, sms_body) VALUES ('test_email', 0, 0, 0, '', '', '')");
            var disabled = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "email"), ("contact", "ops@example.test")), staff);
            Assert.Equal("The email test notification template is disabled.", disabled.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "UPDATE notifications_settings SET email_on = 1 WHERE name = 'test_email'");
            var direct = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "email"), ("contact", "ops@example.test")), staff);
            Assert.Equal("The email test notification does not allow direct test contacts.", direct.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "UPDATE notifications_settings SET send_for_not_confirmed = 1 WHERE name = 'test_email'");
            var smtp = await SendAsync(client, CpLegacyPhpAjaxLinks.NotificationTest, Form(("csrf_guard_key", "admin-csrf"), ("type", "email"), ("contact", "ops@example.test")), staff);
            Assert.Equal("SMTP is not fully configured — fill the e-mail group in Configuration first.", smtp.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'debug_results'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
            if (Directory.Exists(configRoot))
            {
                Directory.Delete(configRoot, true);
            }
        }
    }

    [Fact]
    public async Task SocialMedia_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        var draft = Form(("csrf_token", "admin-csrf"), ("action", "save_draft"), ("platform", "Instagram"), ("title", "Brake pads"), ("caption", "Front kit"), ("hashtags", "#parts"), ("media_url", "https://example.test/a.jpg"));
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.SocialMedia, draft, staff);
                Assert.False(offline.Json.RootElement.GetProperty("ok").GetBoolean());
                Assert.Equal("DB unavailable", offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, CpLegacyPhpAjaxLinks.SocialMedia, draft, staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, CpLegacyPhpAjaxLinks.SocialMedia, draft, string.Empty);
            Assert.Equal("Admin required", guest.Json.RootElement.GetProperty("message").GetString());
            var csrf = await SendAsync(client, CpLegacyPhpAjaxLinks.SocialMedia, Form(("action", "save_draft"), ("title", "Brake pads")), staff);
            Assert.Equal("CSRF failed", csrf.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, CpLegacyPhpAjaxLinks.SocialMedia, draft, staff);
            Assert.Equal("Social drafts table is missing — schema-ensure stays Classic.", missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_social_post_drafts'"));

            await ExecuteAsync(connectionString, "CREATE TABLE epc_social_post_drafts (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, site_key VARCHAR(64) NOT NULL, platform VARCHAR(32) NOT NULL, title VARCHAR(255) NOT NULL, caption TEXT NULL, hashtags TEXT NULL, media_url VARCHAR(512) NULL, status VARCHAR(32) NOT NULL, created_at BIGINT NOT NULL, updated_at BIGINT NOT NULL)");
            var saved = await SendAsync(client, CpLegacyPhpAjaxLinks.SocialMedia, draft, staff);
            Assert.True(saved.Json.RootElement.GetProperty("ok").GetBoolean(), saved.Body);
            Assert.Equal("Draft saved.", saved.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("127-0-0-1", await ScalarAsync(connectionString, "SELECT site_key FROM epc_social_post_drafts WHERE id = 1"));
            Assert.Equal("instagram", await ScalarAsync(connectionString, "SELECT platform FROM epc_social_post_drafts WHERE id = 1"));
            Assert.Equal("Brake pads", await ScalarAsync(connectionString, "SELECT title FROM epc_social_post_drafts WHERE id = 1"));
            Assert.Equal("draft", await ScalarAsync(connectionString, "SELECT status FROM epc_social_post_drafts WHERE id = 1"));
            var updated = await SendAsync(client, CpLegacyPhpAjaxLinks.SocialMedia, Form(("csrf_token", "admin-csrf"), ("action", "save_draft"), ("id", "1"), ("platform", "instagram"), ("title", "Brake pads"), ("caption", "Rear kit")), staff);
            Assert.True(updated.Json.RootElement.GetProperty("ok").GetBoolean(), updated.Body);
            Assert.Equal("Rear kit", await ScalarAsync(connectionString, "SELECT caption FROM epc_social_post_drafts WHERE id = 1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_social_post_drafts"));
            var absent = await SendAsync(client, CpLegacyPhpAjaxLinks.SocialMedia, Form(("csrf_token", "admin-csrf"), ("action", "save_draft"), ("id", "99"), ("title", "Missing")), staff);
            Assert.Equal("Draft not found", absent.Json.RootElement.GetProperty("message").GetString());
            var caption = await SendAsync(client, CpLegacyPhpAjaxLinks.SocialMedia, Form(("csrf_token", "admin-csrf"), ("action", "generate_caption"), ("platform", "instagram")), staff);
            Assert.Equal(StorefrontPhpAjax.SocialCaptionStaysClassic, caption.Json.RootElement.GetProperty("message").GetString());
            var publish = await SendAsync(client, CpLegacyPhpAjaxLinks.SocialMedia, Form(("csrf_token", "admin-csrf"), ("action", "publish_now")), staff);
            Assert.Equal(StorefrontPhpAjax.SocialPublishStaysClassic, publish.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_social_post_drafts"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Integrations_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        var longName = new string('N', 130);
        var mobile = Form(("action", "save_mobile"), ("enabled", "1"), ("app_name", longName), ("bundle_id", "com.desk.app"), ("deep_link_scheme", "desk"), ("pwa_enabled", "0"));
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.Integrations, mobile, staff);
                Assert.False(offline.Json.RootElement.GetProperty("status").GetBoolean());
                Assert.Equal("Database connection failed", offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, mobile, staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, mobile, string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, guest.Status);
            Assert.Equal(StorefrontPhpAjax.IntegrationsAdminRequired, guest.Json.RootElement.GetProperty("message").GetString());
            var unknown = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "missing")), staff);
            Assert.Equal("Unknown action", unknown.Json.RootElement.GetProperty("message").GetString());
            var missingTable = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, mobile, staff);
            Assert.Contains("Mobile settings save failed", missingTable.Json.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_portal_site_settings'"));

            await ExecuteAsync(connectionString, "CREATE TABLE epc_portal_site_settings (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, host VARCHAR(120) NOT NULL, cp_menu_json TEXT NULL, updated_at INT NOT NULL DEFAULT 0)");
            var noRow = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, mobile, staff);
            Assert.Equal("No epc_portal_site_settings row exists for this host yet.", noRow.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_portal_site_settings"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'epc_portal_site_settings' AND column_name = 'integrations_json'"));
            var smtpMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "save_tenant_smtp"), ("smtp_host", "mail.example.test")), staff);
            Assert.Equal("Site settings row is missing. Schema ensure stays on the Classic twin.", smtpMissing.Json.RootElement.GetProperty("message").GetString());

            await ExecuteAsync(connectionString, "INSERT INTO epc_portal_site_settings (host, updated_at) VALUES ('127.0.0.1', 1)");
            var savedMobile = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, mobile, staff);
            Assert.True(savedMobile.Json.RootElement.GetProperty("status").GetBoolean(), savedMobile.Body);
            Assert.Equal("Mobile settings saved.", savedMobile.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(longName[..120], await ScalarAsync(connectionString, "SELECT JSON_UNQUOTE(JSON_EXTRACT(integrations_json, '$.mobile.app_name')) FROM epc_portal_site_settings WHERE id = 1"));
            Assert.Equal("true", await ScalarAsync(connectionString, "SELECT JSON_UNQUOTE(JSON_EXTRACT(integrations_json, '$.mobile.enabled')) FROM epc_portal_site_settings WHERE id = 1"));
            Assert.Equal("false", await ScalarAsync(connectionString, "SELECT JSON_UNQUOTE(JSON_EXTRACT(integrations_json, '$.mobile.pwa_enabled')) FROM epc_portal_site_settings WHERE id = 1"));

            var smtp = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "save_tenant_smtp"), ("smtp_host", "mail.example.test"), ("smtp_port", "2525"), ("smtp_encryption", "starttls"), ("smtp_password", "desk-secret"), ("from_email", "desk@example.test")), staff);
            Assert.True(smtp.Json.RootElement.GetProperty("status").GetBoolean(), smtp.Body);
            Assert.Equal("SMTP settings saved.", smtp.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("desk-secret", await ScalarAsync(connectionString, "SELECT JSON_UNQUOTE(JSON_EXTRACT(integrations_json, '$.smtp.smtp_password')) FROM epc_portal_site_settings WHERE id = 1"));
            Assert.Equal(longName[..120], await ScalarAsync(connectionString, "SELECT JSON_UNQUOTE(JSON_EXTRACT(integrations_json, '$.mobile.app_name')) FROM epc_portal_site_settings WHERE id = 1"));
            Assert.Equal("", await ScalarAsync(connectionString, "SELECT JSON_UNQUOTE(JSON_EXTRACT(integrations_json, '$.smtp.smtp_encryption')) FROM epc_portal_site_settings WHERE id = 1"));
            var kept = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "save_tenant_smtp"), ("smtp_host", "mail.example.test"), ("smtp_password", "")), staff);
            Assert.True(kept.Json.RootElement.GetProperty("status").GetBoolean(), kept.Body);
            Assert.Equal("desk-secret", await ScalarAsync(connectionString, "SELECT JSON_UNQUOTE(JSON_EXTRACT(integrations_json, '$.smtp.smtp_password')) FROM epc_portal_site_settings WHERE id = 1"));

            var badMail = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "test_tenant_smtp"), ("test_to", "not-an-email")), staff);
            Assert.Equal("Valid test email required", badMail.Json.RootElement.GetProperty("message").GetString());
            var off = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "test_tenant_smtp"), ("test_to", "desk@example.test")), staff);
            Assert.Contains("Use tenant SMTP is off", off.Json.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            var enabled = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "save_tenant_smtp"), ("use_tenant_smtp", "1"), ("smtp_host", ""), ("smtp_password", "")), staff);
            Assert.True(enabled.Json.RootElement.GetProperty("status").GetBoolean(), enabled.Body);
            var noHost = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "test_tenant_smtp"), ("test_to", "desk@example.test")), staff);
            Assert.Equal("SMTP host and port are required.", noHost.Json.RootElement.GetProperty("message").GetString());

            var tenantFlags = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "save_feature_flags"), ("site_key", "epc-demo"), ("features[email_smtp]", "1")), staff);
            Assert.Equal(StorefrontPhpAjax.IntegrationsSuperOnly, tenantFlags.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_tenant_feature_flags'"));
            var badKey = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "save_feature_flags"), ("site_key", "!!!")), staff, "ecomae.com");
            Assert.Equal("Invalid site_key", badKey.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_tenant_feature_flags'"));
            var flags = await SendAsync(client, CpLegacyPhpAjaxLinks.Integrations, Form(("action", "save_feature_flags"), ("site_key", "epc-demo"), ("features[email_smtp]", "1"), ("features[tenant_registry]", "1")), staff, "ecomae.com");
            Assert.True(flags.Json.RootElement.GetProperty("status").GetBoolean(), flags.Body);
            Assert.Equal("Saved " + CpTenantFeaturesWriteService.SaveableKeys.Count.ToString(CultureInfo.InvariantCulture) + " feature flags.", flags.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT IF(enabled = 1, '1', '0') FROM epc_tenant_feature_flags WHERE site_key = 'epc-demo' AND feature_key = 'email_smtp'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_tenant_feature_flags WHERE feature_key = 'tenant_registry'"));
            Assert.Equal(CpTenantFeaturesWriteService.SaveableKeys.Count.ToString(CultureInfo.InvariantCulture), await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_tenant_feature_flags"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_portal_site_settings"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Portal_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        var settings = Form(
            ("action", "save_settings"),
            ("industry_code", "platform_host"),
            ("access_mode", "full_commerce"),
            ("system_name", "Desk Parts"),
            ("enabled_packs[0]", "super_platform"),
            ("enabled_packs[1]", "commerce"),
            ("hidden_groups[0]", "4"));
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, CpLegacyPhpAjaxLinks.Portal, settings, staff);
                Assert.False(offline.Json.RootElement.GetProperty("status").GetBoolean());
                Assert.Equal("Database connection failed", offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, settings, staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, settings, string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, guest.Status);
            Assert.Equal(StorefrontPhpAjax.IntegrationsAdminRequired, guest.Json.RootElement.GetProperty("message").GetString());
            var unknown = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "missing")), staff);
            Assert.Equal("Unknown action", unknown.Json.RootElement.GetProperty("message").GetString());
            var badIndustry = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "save_settings"), ("industry_code", "nope")), staff);
            Assert.Equal("Unknown industry code: nope", badIndustry.Json.RootElement.GetProperty("message").GetString());
            var missingTable = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, settings, staff);
            Assert.Contains("Settings save failed", missingTable.Json.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_portal_site_settings'"));

            await ExecuteAsync(connectionString, "CREATE TABLE epc_portal_site_settings (host VARCHAR(120) NOT NULL PRIMARY KEY, industry_code VARCHAR(64) NOT NULL, theme_template VARCHAR(64) NOT NULL, access_mode VARCHAR(32) NOT NULL, erp_modules_json TEXT NULL, cp_default_lang VARCHAR(8) NOT NULL, country_code CHAR(2) NOT NULL, system_name VARCHAR(120) NOT NULL, hub_name VARCHAR(120) NOT NULL, tagline VARCHAR(255) NOT NULL, domain_path VARCHAR(255) NOT NULL, contact_json TEXT NULL, enabled_packs_json TEXT NULL, theme_json TEXT NULL, cp_menu_json TEXT NULL, updated_at INT NOT NULL DEFAULT 0)");
            var saved = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, settings, staff);
            Assert.True(saved.Json.RootElement.GetProperty("status").GetBoolean(), saved.Body);
            Assert.Equal("Settings saved.", saved.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("auto_parts", await ScalarAsync(connectionString, "SELECT industry_code FROM epc_portal_site_settings WHERE host = '127.0.0.1'"));
            Assert.Equal("full", await ScalarAsync(connectionString, "SELECT access_mode FROM epc_portal_site_settings WHERE host = '127.0.0.1'"));
            Assert.Equal("Desk Parts", await ScalarAsync(connectionString, "SELECT system_name FROM epc_portal_site_settings WHERE host = '127.0.0.1'"));
            Assert.Equal("AE", await ScalarAsync(connectionString, "SELECT country_code FROM epc_portal_site_settings WHERE host = '127.0.0.1'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT IF(JSON_CONTAINS(enabled_packs_json, '\"core\"'), '1', '0') FROM epc_portal_site_settings WHERE host = '127.0.0.1'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT IF(JSON_CONTAINS(enabled_packs_json, '\"commerce\"'), '1', '0') FROM epc_portal_site_settings WHERE host = '127.0.0.1'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT IF(JSON_CONTAINS(enabled_packs_json, '\"super_platform\"'), '1', '0') FROM epc_portal_site_settings WHERE host = '127.0.0.1'"));
            Assert.Equal("4", await ScalarAsync(connectionString, "SELECT JSON_UNQUOTE(JSON_EXTRACT(cp_menu_json, '$.hidden_groups[0]')) FROM epc_portal_site_settings WHERE host = '127.0.0.1'"));

            var pushed = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "save_settings"), ("industry_code", "auto_parts"), ("system_name", "Platform Desk"), ("target_host", "client.example.test")), staff, "ecomae.com");
            Assert.True(pushed.Json.RootElement.GetProperty("status").GetBoolean(), pushed.Body);
            Assert.Contains(StorefrontPhpAjax.PortalPushStaysClassic, pushed.Json.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal("Platform Desk", await ScalarAsync(connectionString, "SELECT system_name FROM epc_portal_site_settings WHERE host = 'ecomae.com'"));
            Assert.Equal("Desk Parts", await ScalarAsync(connectionString, "SELECT system_name FROM epc_portal_site_settings WHERE host = '127.0.0.1'"));

            var menu = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "menu_items"), ("group_id", "3")), staff);
            Assert.Equal(StorefrontPhpAjax.PortalMenuStaysClassic, menu.Json.RootElement.GetProperty("message").GetString());
            var seed = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "seed_storefront_data")), staff);
            Assert.Equal(StorefrontPhpAjax.PortalSeedStaysClassic, seed.Json.RootElement.GetProperty("message").GetString());
            var deployTenant = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "deploy_site"), ("site_key", "epc_demo")), staff);
            Assert.Equal(StorefrontPhpAjax.PortalDeployOnlyOnPlatform, deployTenant.Json.RootElement.GetProperty("message").GetString());
            var deploy = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "deploy_site"), ("site_key", "epc_demo")), staff, "ecomae.com");
            Assert.Equal(StorefrontPhpAjax.PortalDeployStaysClassic, deploy.Json.RootElement.GetProperty("message").GetString());
            var resetTenant = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "tenant_reset_password"), ("site_key", "epc_demo")), staff);
            Assert.Equal(HttpStatusCode.Forbidden, resetTenant.Status);
            var reset = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "tenant_reset_password"), ("site_key", "epc_demo")), staff, "ecomae.com");
            Assert.Equal(StorefrontPhpAjax.PortalPasswordStaysClassic, reset.Json.RootElement.GetProperty("message").GetString());
            Assert.False(reset.Json.RootElement.TryGetProperty("password", out _));
            var reveal = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "tenant_reveal_password"), ("site_key", "epc_demo")), staff, "ecomae.com");
            Assert.Equal(StorefrontPhpAjax.PortalRevealStaysClassic, reveal.Json.RootElement.GetProperty("message").GetString());

            var activeTenant = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "tenant_set_active"), ("site_key", "epc_demo"), ("active", "0")), staff);
            Assert.Equal(HttpStatusCode.Forbidden, activeTenant.Status);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_portal_tenants'"));
            var badKey = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "tenant_set_active"), ("site_key", "!!!"), ("active", "0")), staff, "ecomae.com");
            Assert.Equal("Invalid site key", badKey.Json.RootElement.GetProperty("message").GetString());
            var missingTenants = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "tenant_set_active"), ("site_key", "epc_demo"), ("active", "0")), staff, "ecomae.com");
            Assert.Equal("Tenant registry table is missing — schema-ensure stays Classic.", missingTenants.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_portal_tenants'"));

            await ExecuteAsync(connectionString, "CREATE TABLE epc_portal_tenants (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, site_key VARCHAR(64) NOT NULL, is_active TINYINT NOT NULL DEFAULT 1, updated_at INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "INSERT INTO epc_portal_tenants (site_key, is_active, updated_at) VALUES ('epc_demo', 1, 1)");
            var disabled = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "tenant_set_active"), ("site_key", "epc_demo"), ("active", "0")), staff, "ecomae.com");
            Assert.True(disabled.Json.RootElement.GetProperty("status").GetBoolean(), disabled.Body);
            Assert.Equal("Tenant disabled — storefront and CP blocked", disabled.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(0, disabled.Json.RootElement.GetProperty("is_active").GetInt32());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT IF(is_active = 0, '0', '1') FROM epc_portal_tenants WHERE site_key = 'epc_demo'"));
            var absent = await SendAsync(client, CpLegacyPhpAjaxLinks.Portal, Form(("action", "tenant_set_active"), ("site_key", "missing"), ("active", "1")), staff, "ecomae.com");
            Assert.Equal("Tenant not in registry", absent.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_portal_site_settings"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_portal_deploy_targets'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MarketingBroadcast_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        var manual = CpLegacyPhpAjaxLinks.MarketingBroadcast
            + "?action=count_recipients&audience_mode=manual&channel=email&audience_meta="
            + Uri.EscapeDataString("a@b.test,nope,c@d.test");
        var phones = CpLegacyPhpAjaxLinks.MarketingBroadcast
            + "?action=count_recipients&audience_mode=manual&channel=whatsapp&audience_meta="
            + Uri.EscapeDataString("971500000001;971500000002");
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, manual, null, staff);
                Assert.False(offline.Json.RootElement.GetProperty("ok").GetBoolean());
                Assert.Equal("DB unavailable", offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, manual, null, staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, manual, null, string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, guest.Status);
            Assert.Equal("Forbidden", guest.Json.RootElement.GetProperty("message").GetString());
            var unknown = await SendAsync(client, CpLegacyPhpAjaxLinks.MarketingBroadcast, Form(("action", "send_email")), staff);
            Assert.Equal("Unknown action", unknown.Json.RootElement.GetProperty("message").GetString());
            var postedMode = await SendAsync(client, CpLegacyPhpAjaxLinks.MarketingBroadcast, Form(("action", "count_recipients"), ("audience_mode", "manual"), ("audience_meta", "a@b.test")), staff);
            Assert.True(postedMode.Json.RootElement.GetProperty("ok").GetBoolean(), postedMode.Body);
            Assert.Equal(0, postedMode.Json.RootElement.GetProperty("count").GetInt32());
            var counted = await SendAsync(client, manual, null, staff);
            Assert.True(counted.Json.RootElement.GetProperty("ok").GetBoolean(), counted.Body);
            Assert.Equal(2, counted.Json.RootElement.GetProperty("count").GetInt32());
            var whatsapp = await SendAsync(client, phones, null, staff);
            Assert.Equal(2, whatsapp.Json.RootElement.GetProperty("count").GetInt32());
            var email = await SendAsync(client, CpLegacyPhpAjaxLinks.MarketingBroadcast + "?action=template_preview&channel=email&template_key=blank", null, staff);
            Assert.Equal(MarketingBroadcastCatalog.EmailTemplate("blank").Subject, email.Json.RootElement.GetProperty("subject").GetString());
            Assert.Equal(MarketingBroadcastCatalog.EmailTemplate("blank").Html, email.Json.RootElement.GetProperty("body_html").GetString());
            var text = await SendAsync(client, CpLegacyPhpAjaxLinks.MarketingBroadcast + "?action=template_preview&channel=whatsapp&template_key=missing", null, staff);
            Assert.Equal(MarketingBroadcastCatalog.WhatsappTemplate("blank").Body, text.Json.RootElement.GetProperty("body_text").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'users'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_marketing_broadcast_campaigns'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task PartsCatalogues_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var settings = """
            {"autoxp_show":"on","autoxp_caption":"AutoXP","autoxp_order":"2","autoxp_id":"99","autoxp_show_cars":[3],"ilcats_show":"on","ilcats_caption":"Ilcats","ilcats_order":"1","ilcats_clid":"CL1","ilcats_car_3":"PID3","catalogs_parts_com_show":"on","catalogs_parts_com_caption":"Catalogs Parts","catalogs_parts_com_order":"3","catalogs_parts_com_id":"CLIENT","catalogs_parts_com_show_cars":["3"],"levam_show":"on","levam_caption":"Levam","levam_order":"4"}
            """;
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, StorefrontPartsCatalogues.Path, null, string.Empty);
                Assert.False(offline.Json.RootElement.GetProperty("status").GetBoolean());
                Assert.Equal(StorefrontPhpAjax.NoDbConnect, offline.Json.RootElement.GetProperty("message").GetString());
                var clicksOff = await SendAsync(closedClient, StorefrontPartsCatalogues.AutoxpClicksPath, null, string.Empty);
                Assert.Equal(StorefrontPhpAjax.NoDbConnect, clicksOff.Body);
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var missing = await SendAsync(client, StorefrontPartsCatalogues.Path, null, string.Empty);
            Assert.Equal(StorefrontPartsCatalogues.NotInDatabase, missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(0, missing.Json.RootElement.GetProperty("brands").GetArrayLength());
            var clicksMissing = await SendAsync(client, StorefrontPartsCatalogues.AutoxpClicksPath, null, string.Empty);
            Assert.Equal(StorefrontPartsCatalogues.AutoxpMissing, clicksMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('shop_docpart_search_tabs','shop_docpart_cars','shop_docpart_cars_catalogues','shop_docpart_cars_catalogue_links','shop_docpart_autoxp_clicks')"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_search_tabs (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, parameters_values TEXT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_cars (id INT NOT NULL PRIMARY KEY, caption VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_cars_catalogues (id INT NOT NULL PRIMARY KEY, assoc_name VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_cars_catalogue_links (id INT NOT NULL PRIMARY KEY, car_id INT NOT NULL, catalogue_id INT NOT NULL, href VARCHAR(512) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_search_tabs (id, name, parameters_values) VALUES (1, 'parts_catalogues', '" + settings.Replace("'", "''", StringComparison.Ordinal) + "')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_cars (id, caption) VALUES (3, 'Toyota'), (5, 'Bmw')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_cars_catalogues (id, assoc_name) VALUES (1, 'autoxp'), (2, 'ilcats'), (3, 'catalogs_parts_com'), (4, 'levam'), (5, 'hidden')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_cars_catalogue_links (id, car_id, catalogue_id, href) VALUES (1, 3, 1, 'https://autoxp.example/?'), (2, 3, 2, 'https://ilcats.example/?pid=<pid>&clid=<clid>'), (3, 3, 3, 'http://toyota.catalogs-parts.com/#{client:;page:models}'), (4, 3, 4, 'https://levam.example/toyota'), (5, 5, 1, 'https://autoxp.example/bmw?'), (6, 5, 5, 'https://hidden.example/bmw')");
            var listed = await SendAsync(client, StorefrontPartsCatalogues.Path, null, string.Empty);
            Assert.True(listed.Json.RootElement.GetProperty("status").GetBoolean(), listed.Body);
            Assert.Equal(1, listed.Json.RootElement.GetProperty("brands").GetArrayLength());
            var toyota = listed.Json.RootElement.GetProperty("brands")[0];
            Assert.Equal("TOYOTA", toyota.GetProperty("caption").GetString());
            Assert.Equal(3, toyota.GetProperty("car_id").GetInt32());
            var catalogues = toyota.GetProperty("catalogues");
            Assert.Equal(4, catalogues.GetArrayLength());
            Assert.Equal("ilcats", catalogues[0].GetProperty("name").GetString());
            Assert.Equal("https://ilcats.example/?pid=PID3&clid=CL1", catalogues[0].GetProperty("href").GetString());
            Assert.Equal("autoxp", catalogues[1].GetProperty("name").GetString());
            Assert.Equal("https://autoxp.example/?99", catalogues[1].GetProperty("href").GetString());
            Assert.Equal("catalogs_parts_com", catalogues[2].GetProperty("name").GetString());
            Assert.Equal("http://toyota.catalogs-parts.com/#{client:CLIENT;page:models}", catalogues[2].GetProperty("href").GetString());
            Assert.Equal("levam", catalogues[3].GetProperty("name").GetString());
            Assert.Equal("https://levam.example/toyota", catalogues[3].GetProperty("href").GetString());

            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_autoxp_clicks (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, month INT NOT NULL, year INT NOT NULL, clicks_count INT NOT NULL)");
            var allowed = await SendAsync(client, StorefrontPartsCatalogues.AutoxpClicksPath, null, string.Empty);
            Assert.Equal("1", allowed.Body);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT clicks_count FROM shop_docpart_autoxp_clicks"));
            await ExecuteAsync(connectionString, "UPDATE shop_docpart_autoxp_clicks SET clicks_count = 2000");
            var blocked = await SendAsync(client, StorefrontPartsCatalogues.AutoxpClicksPath, null, string.Empty);
            Assert.Equal("0", blocked.Body);
            Assert.Equal("2000", await ScalarAsync(connectionString, "SELECT clicks_count FROM shop_docpart_autoxp_clicks"));
            using var redirectHandler = new HttpClientHandler { AllowAutoRedirect = false };
            using var redirectClient = new HttpClient(redirectHandler) { BaseAddress = host.BaseAddress };
            await ExecuteAsync(connectionString, "UPDATE shop_docpart_autoxp_clicks SET clicks_count = 1");
            var redirected = await SendAsync(redirectClient, StorefrontPartsCatalogues.AutoxpClicksPath + "?next=" + Uri.EscapeDataString("https://autoxp.example/?99"), null, string.Empty);
            Assert.Equal(HttpStatusCode.Redirect, redirected.Status);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT clicks_count FROM shop_docpart_autoxp_clicks"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task WebTracker_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        var dash = CpLegacyPhpAjaxLinks.WebTracker + "?action=dashboard&from=2026-10-06&to=2026-10-06";
        var seen = DateTimeOffset.Parse("2026-10-06T12:00:00Z", CultureInfo.InvariantCulture).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, dash, null, staff);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, offline.Status);
                Assert.False(offline.Json.RootElement.GetProperty("ok").GetBoolean());
                Assert.Equal("db", offline.Json.RootElement.GetProperty("error").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, dash, null, staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, dash, null, string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, guest.Status);
            Assert.Equal("forbidden", guest.Json.RootElement.GetProperty("error").GetString());
            var scope = await SendAsync(client, CpLegacyPhpAjaxLinks.WebTracker + "?action=dashboard&site_key=epartscart", null, staff);
            Assert.Equal(HttpStatusCode.Forbidden, scope.Status);
            Assert.Equal("tenant_scope", scope.Json.RootElement.GetProperty("error").GetString());
            var missingId = await SendAsync(client, CpLegacyPhpAjaxLinks.WebTracker + "?action=session&id=0", null, staff);
            Assert.False(missingId.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("Missing session id.", missingId.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, dash, null, staff);
            Assert.Equal(HttpStatusCode.InternalServerError, missing.Status);
            Assert.Equal("query_failed", missing.Json.RootElement.GetProperty("error").GetString());
            Assert.Equal(StorefrontPhpAjax.TrackerTablesMissing, missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('epc_web_tracker_sessions','epc_web_tracker_pageviews','epc_web_tracker_events')"));

            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_web_tracker_sessions (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  session_uid VARCHAR(64) NOT NULL DEFAULT '',
                  site_key VARCHAR(64) NOT NULL DEFAULT '',
                  hostname VARCHAR(120) NOT NULL DEFAULT '',
                  user_id INT NOT NULL DEFAULT 0,
                  is_registered TINYINT NOT NULL DEFAULT 0,
                  first_seen_at BIGINT NOT NULL DEFAULT 0,
                  last_seen_at BIGINT NOT NULL DEFAULT 0,
                  pageview_count INT NOT NULL DEFAULT 0,
                  event_count INT NOT NULL DEFAULT 0,
                  duration_ms BIGINT NOT NULL DEFAULT 0,
                  landing_path VARCHAR(255) NOT NULL DEFAULT '',
                  exit_path VARCHAR(255) NOT NULL DEFAULT '',
                  country_code VARCHAR(8) NOT NULL DEFAULT '',
                  country_name VARCHAR(64) NOT NULL DEFAULT '',
                  city VARCHAR(64) NOT NULL DEFAULT '',
                  region VARCHAR(64) NOT NULL DEFAULT '',
                  device_type VARCHAR(32) NOT NULL DEFAULT '',
                  browser VARCHAR(40) NOT NULL DEFAULT '',
                  os VARCHAR(40) NOT NULL DEFAULT '',
                  ip VARCHAR(45) NOT NULL DEFAULT '',
                  referrer_host VARCHAR(120) NOT NULL DEFAULT '',
                  utm_source VARCHAR(64) NOT NULL DEFAULT '',
                  utm_medium VARCHAR(64) NOT NULL DEFAULT '',
                  utm_campaign VARCHAR(64) NOT NULL DEFAULT '',
                  visitor_uid VARCHAR(64) NOT NULL DEFAULT ''
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_web_tracker_pageviews (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  session_id INT NOT NULL,
                  session_uid VARCHAR(64) NOT NULL DEFAULT '',
                  site_key VARCHAR(64) NOT NULL DEFAULT '',
                  ts BIGINT NOT NULL DEFAULT 0,
                  path VARCHAR(255) NOT NULL DEFAULT '',
                  `query` VARCHAR(255) NOT NULL DEFAULT '',
                  title VARCHAR(255) NOT NULL DEFAULT '',
                  time_on_page_ms BIGINT NOT NULL DEFAULT 0,
                  scroll_max_pct INT NOT NULL DEFAULT 0,
                  load_time_ms BIGINT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_web_tracker_events (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  session_id INT NOT NULL,
                  session_uid VARCHAR(64) NOT NULL DEFAULT '',
                  site_key VARCHAR(64) NOT NULL DEFAULT '',
                  ts BIGINT NOT NULL DEFAULT 0,
                  event_type VARCHAR(32) NOT NULL DEFAULT '',
                  path VARCHAR(255) NOT NULL DEFAULT '',
                  search_query VARCHAR(255) NOT NULL DEFAULT '',
                  search_context VARCHAR(64) NOT NULL DEFAULT '',
                  element_tag VARCHAR(32) NOT NULL DEFAULT '',
                  element_id VARCHAR(64) NOT NULL DEFAULT '',
                  element_text VARCHAR(255) NOT NULL DEFAULT '',
                  element_href VARCHAR(255) NOT NULL DEFAULT '',
                  x INT NOT NULL DEFAULT 0,
                  y INT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO epc_web_tracker_sessions (session_uid, site_key, hostname, visitor_uid, pageview_count, event_count, is_registered, duration_ms, first_seen_at, last_seen_at, landing_path, exit_path, country_code, country_name, city, device_type, browser, os) VALUES ('s1', '127_0_0_1', '127.0.0.1', 'v1', 2, 1, 0, 1500, " + seen + ", " + seen + ", '/desk/parts', '/desk/parts', 'AE', 'United Arab Emirates', 'Dubai', 'desktop', 'Chrome', 'Linux')");
            await ExecuteAsync(connectionString, "INSERT INTO epc_web_tracker_sessions (session_uid, site_key, hostname, visitor_uid, pageview_count, last_seen_at, first_seen_at, landing_path) VALUES ('s2', 'epartscart', 'epartscart.com', 'v2', 1, " + seen + ", " + seen + ", '/other')");
            await ExecuteAsync(connectionString, "INSERT INTO epc_web_tracker_pageviews (session_id, session_uid, site_key, ts, path, title) VALUES (1, 's1', '127_0_0_1', " + seen + ", '/desk/parts', 'Desk parts')");
            var listed = await SendAsync(client, dash, null, staff);
            Assert.True(listed.Json.RootElement.GetProperty("ok").GetBoolean(), listed.Body);
            Assert.Equal("127_0_0_1", listed.Json.RootElement.GetProperty("site_key").GetString());
            Assert.False(listed.Json.RootElement.GetProperty("is_super").GetBoolean());
            Assert.Equal(1, listed.Json.RootElement.GetProperty("data").GetProperty("summary").GetProperty("sessions").GetInt64());
            Assert.Equal("/desk/parts", listed.Json.RootElement.GetProperty("data").GetProperty("recent_sessions")[0].GetProperty("landing_path").GetString());
            var fleet = await SendAsync(client, CpLegacyPhpAjaxLinks.WebTracker + "?action=dashboard&from=2026-10-06&to=2026-10-06", null, staff, "ecomae.com");
            Assert.True(fleet.Json.RootElement.GetProperty("ok").GetBoolean(), fleet.Body);
            Assert.Equal("_all", fleet.Json.RootElement.GetProperty("site_key").GetString());
            Assert.True(fleet.Json.RootElement.GetProperty("is_super").GetBoolean());
            Assert.Equal(2, fleet.Json.RootElement.GetProperty("data").GetProperty("summary").GetProperty("sessions").GetInt64());
            var absent = await SendAsync(client, CpLegacyPhpAjaxLinks.WebTracker + "?action=session&id=999", null, staff);
            Assert.Equal("Session not found.", absent.Json.RootElement.GetProperty("message").GetString());
            var detail = await SendAsync(client, CpLegacyPhpAjaxLinks.WebTracker + "?action=session&id=1", null, staff);
            Assert.True(detail.Json.RootElement.GetProperty("ok").GetBoolean(), detail.Body);
            Assert.Equal("/desk/parts", detail.Json.RootElement.GetProperty("detail").GetProperty("session").GetProperty("landing_path").GetString());
            Assert.Equal("/desk/parts", detail.Json.RootElement.GetProperty("detail").GetProperty("pageviews")[0].GetProperty("path").GetString());
            var csv = await SendAsync(client, CpLegacyPhpAjaxLinks.WebTracker + "?action=csv&from=2026-10-06&to=2026-10-06", null, staff);
            Assert.Equal(HttpStatusCode.OK, csv.Status);
            Assert.Contains("Website tracker full report", csv.Body, StringComparison.Ordinal);
            Assert.Contains("/desk/parts", csv.Body, StringComparison.Ordinal);
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('epc_web_tracker_sessions','epc_web_tracker_pageviews','epc_web_tracker_events')"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task PlatformGovernance_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        var list = CpLegacyPhpAjaxLinks.PlatformGovernance + "?action=list_rules";
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, list, null, staff);
                Assert.False(offline.Json.RootElement.GetProperty("status").GetBoolean());
                Assert.Equal(StorefrontPhpAjax.GovernanceDbError, offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, list, null, staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, list, null, string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, guest.Status);
            Assert.Equal("Admin login required", guest.Json.RootElement.GetProperty("message").GetString());
            var tenant = await SendAsync(client, list, null, staff);
            Assert.Equal(HttpStatusCode.Forbidden, tenant.Status);
            Assert.Equal("Super CP only", tenant.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_platform_governance_rules'"));
            var badKey = await SendAsync(client, CpLegacyPhpAjaxLinks.PlatformGovernance, Form(("action", "save_rule"), ("rule_key", "!!!")), staff, "ecomae.com");
            Assert.Equal("Invalid rule_key", badKey.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, list, null, staff, "ecomae.com");
            Assert.Equal(StorefrontPhpAjax.GovernanceRulesMissing, missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_platform_governance_rules'"));

            await ExecuteAsync(connectionString, "CREATE TABLE epc_platform_governance_rules (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, rule_key VARCHAR(64) NOT NULL, category VARCHAR(24) NOT NULL DEFAULT 'tenant', title VARCHAR(160) NOT NULL DEFAULT '', enforcement VARCHAR(16) NOT NULL DEFAULT 'required', scope VARCHAR(32) NOT NULL DEFAULT 'all_tenants', active TINYINT NOT NULL DEFAULT 1, time_updated INT NOT NULL DEFAULT 0, UNIQUE KEY rule_key (rule_key))");
            await ExecuteAsync(connectionString, "INSERT INTO epc_platform_governance_rules (rule_key, category, title, enforcement, scope, active) VALUES ('auth_otp', 'auth', 'OTP', 'required', 'all_tenants', 1)");
            var absent = await SendAsync(client, CpLegacyPhpAjaxLinks.PlatformGovernance, Form(("action", "save_rule"), ("rule_key", "missing_rule"), ("active", "0"), ("enforcement", "advisory")), staff, "ecomae.com");
            Assert.Equal("Rule not found", absent.Json.RootElement.GetProperty("message").GetString());
            var saved = await SendAsync(client, CpLegacyPhpAjaxLinks.PlatformGovernance, Form(("action", "save_rule"), ("rule_key", "auth_otp"), ("active", "0"), ("enforcement", "advisory")), staff, "ecomae.com");
            Assert.True(saved.Json.RootElement.GetProperty("status").GetBoolean(), saved.Body);
            Assert.Equal("auth_otp", saved.Json.RootElement.GetProperty("rule_key").GetString());
            Assert.Equal("Rule saved", saved.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT IF(active = 0, '0', '1') FROM epc_platform_governance_rules WHERE rule_key = 'auth_otp'"));
            Assert.Equal("advisory", await ScalarAsync(connectionString, "SELECT enforcement FROM epc_platform_governance_rules WHERE rule_key = 'auth_otp'"));
            var listed = await SendAsync(client, list, null, staff, "ecomae.com");
            Assert.True(listed.Json.RootElement.GetProperty("status").GetBoolean(), listed.Body);
            Assert.Equal("auth_otp", listed.Json.RootElement.GetProperty("rules")[0].GetProperty("rule_key").GetString());
            Assert.Equal(0, listed.Json.RootElement.GetProperty("rules")[0].GetProperty("active").GetInt32());
            var unknown = await SendAsync(client, CpLegacyPhpAjaxLinks.PlatformGovernance + "?action=seed", null, staff, "ecomae.com");
            Assert.Equal(HttpStatusCode.BadRequest, unknown.Status);
            Assert.Equal("Unknown action", unknown.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_platform_governance_rules"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task FreeToolsAdmin_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var staff = "admin_session=admin-token; admin_u_id=9";
        var stats = CpLegacyPhpAjaxLinks.FreeToolsAdmin + "?action=stats";
        var seen = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        try
        {
            await using (var closed = await StartAsync(connectionString, configured: false))
            {
                using var closedClient = new HttpClient { BaseAddress = closed.BaseAddress };
                var offline = await SendAsync(closedClient, stats, null, staff);
                Assert.False(offline.Json.RootElement.GetProperty("ok").GetBoolean());
                Assert.Equal(StorefrontPhpAjax.FreeToolsDbError, offline.Json.RootElement.GetProperty("message").GetString());
            }

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var sessions = await SendAsync(client, stats, null, staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, sessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");
            var guest = await SendAsync(client, stats, null, string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, guest.Status);
            Assert.Equal("Admin login required", guest.Json.RootElement.GetProperty("message").GetString());
            var tenant = await SendAsync(client, CpLegacyPhpAjaxLinks.FreeToolsAdmin, Form(("action", "toggle"), ("tool", "vat"), ("active", "0")), staff);
            Assert.Equal(HttpStatusCode.Forbidden, tenant.Status);
            Assert.Equal("Super CP only", tenant.Json.RootElement.GetProperty("message").GetString());
            var unknownTool = await SendAsync(client, CpLegacyPhpAjaxLinks.FreeToolsAdmin, Form(("action", "toggle"), ("tool", "nope"), ("active", "0")), staff, "ecomae.com");
            Assert.Equal("Unknown tool", unknownTool.Json.RootElement.GetProperty("message").GetString());
            var missing = await SendAsync(client, CpLegacyPhpAjaxLinks.FreeToolsAdmin, Form(("action", "toggle"), ("tool", "vat"), ("active", "0")), staff, "ecomae.com");
            Assert.Equal("Free-tools settings table is missing — schema-ensure stays Classic.", missing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_free_tool_settings'"));

            await ExecuteAsync(connectionString, "CREATE TABLE epc_free_tool_settings (name VARCHAR(64) NOT NULL PRIMARY KEY, val TEXT NULL, time_updated INT NOT NULL DEFAULT 0)");
            var off = await SendAsync(client, CpLegacyPhpAjaxLinks.FreeToolsAdmin, Form(("action", "toggle"), ("tool", "vat"), ("active", "0")), staff, "ecomae.com");
            Assert.True(off.Json.RootElement.GetProperty("ok").GetBoolean(), off.Body);
            Assert.False(off.Json.RootElement.GetProperty("active").GetBoolean());
            Assert.Equal(CpFreeToolsWriteService.ToggleMessage("vat", false), off.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT IF(JSON_CONTAINS(val, '\"vat\"'), '1', '0') FROM epc_free_tool_settings WHERE name = 'disabled_tools'"));
            var on = await SendAsync(client, CpLegacyPhpAjaxLinks.FreeToolsAdmin, Form(("action", "toggle"), ("tool", "VAT"), ("active", "1")), staff, "ecomae.com");
            Assert.True(on.Json.RootElement.GetProperty("active").GetBoolean());
            Assert.Equal("[]", await ScalarAsync(connectionString, "SELECT val FROM epc_free_tool_settings WHERE name = 'disabled_tools'"));
            var usageMissing = await SendAsync(client, stats, null, staff, "ecomae.com");
            Assert.Equal(StorefrontPhpAjax.FreeToolsUsageMissing, usageMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('epc_free_tool_accounts','epc_free_tool_saves')"));

            await ExecuteAsync(connectionString, "CREATE TABLE epc_free_tool_accounts (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, email VARCHAR(120) NOT NULL DEFAULT '', company VARCHAR(120) NOT NULL DEFAULT '', country VARCHAR(8) NOT NULL DEFAULT '', pass_hash VARCHAR(255) NULL, time_created INT NOT NULL DEFAULT 0, time_last_seen INT NOT NULL DEFAULT 0, use_count INT NOT NULL DEFAULT 0, login_count INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "CREATE TABLE epc_free_tool_saves (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, tool VARCHAR(32) NOT NULL DEFAULT '', account_id INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "INSERT INTO epc_free_tool_accounts (email, company, country, pass_hash, time_last_seen) VALUES ('a@b.test', 'Desk', 'AE', 'hash', " + seen + ")");
            await ExecuteAsync(connectionString, "INSERT INTO epc_free_tool_saves (tool, account_id) VALUES ('vat', 1)");
            var counted = await SendAsync(client, stats, null, staff, "ecomae.com");
            Assert.True(counted.Json.RootElement.GetProperty("ok").GetBoolean(), counted.Body);
            Assert.Equal(1, counted.Json.RootElement.GetProperty("stats").GetProperty("accounts").GetInt64());
            Assert.Equal(1, counted.Json.RootElement.GetProperty("stats").GetProperty("with_password").GetInt64());
            Assert.Equal(1, counted.Json.RootElement.GetProperty("stats").GetProperty("active_30d").GetInt64());
            Assert.Equal(1, counted.Json.RootElement.GetProperty("stats").GetProperty("saves").GetInt64());
            var unknown = await SendAsync(client, CpLegacyPhpAjaxLinks.FreeToolsAdmin + "?action=send", null, staff, "ecomae.com");
            Assert.Equal("Unknown action", unknown.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_boc%'"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static JsonElement SourceNamed(JsonElement body, string name)
    {
        foreach (var source in body.GetProperty("sources").EnumerateArray())
        {
            if (source.GetProperty("price_name").GetString() == name)
            {
                return source;
            }
        }

        throw new InvalidOperationException("Missing source " + name);
    }

    private static async Task<ProbeHost> StartAsync(string connectionString, string? configRoot = null, bool configured = true)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(configured ? new FixedConnections(connectionString) : new UnconfiguredConnections());
        builder.Services.Configure<PhpReferenceOptions>(options => options.PhpDocRoot = configRoot ?? string.Empty);
        builder.Services.AddSingleton<IErpWriteConnectionFactory>(new WriteConnections(connectionString));
        builder.Services.AddSingleton<IErpEinvoiceProfileWriteService>(sp => new ErpEinvoiceProfileWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpDocumentControlWriteService>(sp => new CpDocumentControlWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpCrmWriteService>(sp => new CpCrmWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<IErpVoucherNumberService, ErpVoucherNumberService>();
        builder.Services.AddSingleton<IErpGlPostingService, ErpGlPostingService>();
        builder.Services.AddSingleton<IErpAuditLogWriter, ErpAuditLogWriter>();
        builder.Services.AddSingleton<IErpSettlementAllocationService, ErpSettlementAllocationService>();
        builder.Services.AddSingleton<IErpAdvanceVatService, ErpAdvanceVatService>();
        builder.Services.AddSingleton<IErpCashWriteService, ErpCashWriteService>();
        builder.Services.AddSingleton<ICpOmsWriteService>(sp => new CpOmsWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpPricesUploadWriteService>(sp => new CpPricesUploadWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpCrossWriteService>(sp => new CpCrossWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpSocialHubWriteService>(sp => new CpSocialHubWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpMobileAppsWriteService>(sp => new CpMobileAppsWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpTenantFeaturesWriteService>(sp => new CpTenantFeaturesWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpTenantEmailWriteService>(sp => new CpTenantEmailWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpIndustrySettingsWriteService>(sp => new CpIndustrySettingsWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpTenantsWriteService>(sp => new CpTenantsWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpMarketingBroadcastService>(sp => new CpMarketingBroadcastService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpPlatformGovernanceWriteService>(sp => new CpPlatformGovernanceWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpFreeToolsWriteService>(sp => new CpFreeToolsWriteService(sp.GetRequiredService<IErpWriteConnectionFactory>()));
        builder.Services.AddSingleton<ICpSmsGateway, IdleSms>();
        builder.Services.AddSingleton<ICpCommunicationsTestService>(sp => new CpCommunicationsTestService(
            sp.GetRequiredService<IErpWriteConnectionFactory>(),
            sp.GetRequiredService<ICpSmsGateway>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PhpReferenceOptions>>()));
        builder.Services.AddSingleton<IStorefrontPriceAccess>(new GuestPrices());
        builder.Services.AddSingleton<ICpPriceImportService>(new IdleImports());
        builder.Services.AddSingleton(ReporterStub.Create());
        var app = builder.Build();
        app.UseMiddleware<CpLegacyPhpAjaxLinkMiddleware>();
        // Implicit routing already matched the PHP URL. Match again after the rewrite,
        // the same way Program.cs calls UseRouting after this middleware.
        app.UseRouting();
        StorefrontPhpAjaxEndpoints.Map(app);
        app.MapPost(EcomAeRoutes.CpCrmAction, async (HttpContext context, ICpCrmWriteService writes, CancellationToken cancellationToken) =>
        {
            if (!context.Items.ContainsKey(CpLegacyPhpAjaxLinks.OperatorPostItem))
            {
                return Results.Json(new { status = false, message = "confirm required" });
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var written = await writes.SaveLeadAsync(
                0,
                form["company"].ToString(),
                form["contact_name"].ToString(),
                string.Empty,
                string.Empty,
                "web",
                "new",
                9,
                0m,
                string.Empty,
                cancellationToken);
            return Results.Json(new { status = written.Succeeded, message = written.Message });
        });
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private static Dictionary<string, string> Form(params (string Key, string Value)[] fields)
    {
        var form = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            form[field.Key] = field.Value;
        }

        return form;
    }

    private static Task<Sent> SendAsync(HttpClient client, string path, Dictionary<string, string>? form, string cookie)
        => SendAsync(client, path, form, cookie, null);

    private static async Task<Sent> SendAsync(HttpClient client, string path, Dictionary<string, string>? form, string cookie, string? hostHeader)
    {
        using var request = new HttpRequestMessage(form is null ? HttpMethod.Get : HttpMethod.Post, path);
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        if (!string.IsNullOrEmpty(hostHeader))
        {
            request.Headers.Host = hostHeader;
        }

        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        JsonDocument json = null!;
        if (body.Length > 0 && body[0] is '{' or '[')
        {
            json = JsonDocument.Parse(body);
        }

        return new Sent(response.StatusCode, body, json);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed record Sent(HttpStatusCode Status, string Body, JsonDocument Json);

    private sealed class ProbeHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public ProbeHost(WebApplication app, Uri baseAddress)
        {
            _app = app;
            BaseAddress = baseAddress;
        }

        public Uri BaseAddress { get; }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class IdleSms : ICpSmsGateway
    {
        public Task<CpSmsSendOutcome> SendAsync(string handler, IReadOnlyDictionary<string, string> parameters, string phone, string body, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SMS was called");
    }

    private sealed class GuestPrices : IStorefrontPriceAccess
    {
        public ValueTask<StorefrontPriceAccessResult> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new StorefrontPriceAccessResult(StorefrontPriceAccessState.Guest, false, "**", string.Empty, string.Empty));

        public IReadOnlyList<StorefrontPartOfferDigest> RedactOffers(IReadOnlyList<StorefrontPartOfferDigest> offers) => offers;
    }

    private sealed class IdleImports : ICpPriceImportService
    {
        public Task<CpPriceImportResult> ImportUploadAsync(CpPriceImportRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Price upload was called.");

        public Task<CpPriceImportResult> ImportWizardDirectoryAsync(long priceId, string directory, bool? cleanBefore, long uploadedBy, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Price import was called.");

        public Task<IReadOnlyList<CpPriceImportResult>> ImportRemoteAsync(IReadOnlyList<long> priceIds, long uploadedBy, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Remote price import was called.");
    }

    private class ReporterStub : System.Reflection.DispatchProxy
    {
        public static ISurfaceDashboardSummaryReporter Create()
            => Create<ISurfaceDashboardSummaryReporter, ReporterStub>();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(targetMethod?.Name ?? "dispatch");
    }

    private sealed class WriteConnections : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public WriteConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }

    private sealed class UnconfiguredConnections : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Database was called.");

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Database was called.");

        public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Database was called.");

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Database was called.");
    }

    private sealed class FixedConnections : ITenantDbConnectionFactory
    {
        private readonly string _connectionString;

        public FixedConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => OpenAsync();

        private async Task<DbConnection> OpenAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}
