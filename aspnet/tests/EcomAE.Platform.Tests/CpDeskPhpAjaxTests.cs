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
        if (!string.IsNullOrEmpty(configRoot))
        {
            builder.Services.Configure<PhpReferenceOptions>(options => options.PhpDocRoot = configRoot);
        }
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

    private static async Task<Sent> SendAsync(HttpClient client, string path, Dictionary<string, string>? form, string cookie)
    {
        using var request = new HttpRequestMessage(form is null ? HttpMethod.Get : HttpMethod.Post, path);
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
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
