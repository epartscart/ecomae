using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPhpCpShopAjaxTests
{
    [Fact]
    public async Task CpShopAjax_OnThrowawayDatabase_ThenDropped()
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
        var configRoot = Path.Combine(Path.GetTempPath(), "ecomae-php-ref-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configRoot);
        WriteConfig(configRoot);
        try
        {
            await using var host = await StartAsync(connectionString, configRoot);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var staff = "admin_session=admin-token; admin_u_id=9";
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");

            var marketingGuest = await SendAsync(client, StorefrontPhpAjax.CpMarketingPath, Form(("action", "snapshot")), string.Empty);
            Assert.Equal("Access denied", marketingGuest.Json.RootElement.GetProperty("message").GetString());
            var marketingGet = await SendAsync(client, StorefrontPhpAjax.CpMarketingEndpointPath, null, staff);
            Assert.Equal("No action", marketingGet.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_marketing_task_progress'"));

            var marketingUnknown = await SendAsync(client, StorefrontPhpAjax.CpMarketingPath, Form(("action", "nope")), staff);
            Assert.Equal("Unknown action", marketingUnknown.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_marketing_task_progress'"));
            var invalidTask = await SendAsync(client, StorefrontPhpAjax.CpMarketingPath, Form(("action", "toggle_task"), ("strategy_key", "measurement"), ("task_key", "missing"), ("is_done", "1")), staff);
            Assert.Equal("Invalid task", invalidTask.Json.RootElement.GetProperty("message").GetString());
            var marked = await SendAsync(client, StorefrontPhpAjax.CpMarketingEndpointPath, Form(("action", "toggle_task"), ("strategy_key", "measurement"), ("task_key", "gsc_verify"), ("is_done", "1")), staff);
            Assert.Equal("Task marked done", marked.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(63, marked.Json.RootElement.GetProperty("completion").GetProperty("total").GetInt32());
            Assert.True(marked.Json.RootElement.GetProperty("completion").GetProperty("done").GetInt32() >= 1);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(`is_done` AS CHAR) FROM `epc_marketing_task_progress` WHERE `strategy_key`='measurement' AND `task_key`='gsc_verify'"));
            var reopened = await SendAsync(client, StorefrontPhpAjax.CpMarketingPath, Form(("action", "toggle_task"), ("strategy_key", "measurement"), ("task_key", "gsc_verify"), ("is_done", "0")), staff);
            Assert.Equal("Task reopened", reopened.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT CAST(`is_done` AS CHAR) FROM `epc_marketing_task_progress` WHERE `strategy_key`='measurement' AND `task_key`='gsc_verify'"));
            var kpi = await SendAsync(client, StorefrontPhpAjax.CpMarketingPath, Form(("action", "save_kpi"), ("strategy_key", "measurement"), ("kpi_key", "monthly_sessions"), ("value", "42"), ("note", "week")), staff);
            Assert.Equal("KPI recorded", kpi.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("42", await ScalarAsync(connectionString, "SELECT `value_text` FROM `epc_marketing_kpi_log` WHERE `kpi_key`='monthly_sessions'"));
            var review = await SendAsync(client, StorefrontPhpAjax.CpMarketingPath, Form(("action", "save_review"), ("strategy_key", "measurement"), ("review_type", "weekly"), ("score", "9"), ("notes", "ok")), staff);
            Assert.Equal("Review saved", review.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("5", await ScalarAsync(connectionString, "SELECT CAST(`score` AS CHAR) FROM `epc_marketing_reviews`"));
            var snapshot = await SendAsync(client, StorefrontPhpAjax.CpMarketingEndpointPath, Form(("action", "snapshot")), staff);
            Assert.True(snapshot.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(0, snapshot.Json.RootElement.GetProperty("data").GetProperty("live").GetProperty("orders_total").GetInt32());
            Assert.Equal("G-J19D1KHXCG", snapshot.Json.RootElement.GetProperty("data").GetProperty("live").GetProperty("ga_property").GetString());
            Assert.Contains("measurement", snapshot.Json.RootElement.GetProperty("data").GetProperty("strategies").EnumerateArray().Select(item => item.GetString()));

            var workshopGuest = await SendAsync(client, StorefrontPhpAjax.CpWorkshopEndpointPath, Form(("action", "list_jobs")), string.Empty);
            Assert.Equal("Access denied", workshopGuest.Json.RootElement.GetProperty("message").GetString());
            var workshopCsrf = await SendAsync(client, StorefrontPhpAjax.CpWorkshopEndpointPath, Form(("action", "list_jobs")), staff);
            Assert.Equal("CSRF failed", workshopCsrf.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_ws_jobs'"));
            var created = await SendAsync(client, StorefrontPhpAjax.CpWorkshopEndpointPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "create_job"), ("plate", "d-999"), ("customer_name", "Nora"), ("complaint", "noise")), staff);
            Assert.Equal("Job created", created.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("D-999", created.Json.RootElement.GetProperty("job").GetProperty("header").GetProperty("plate").GetString());
            Assert.Equal("D-999", await ScalarAsync(connectionString, "SELECT `plate` FROM `epc_ws_jobs` WHERE `customer_name`='Nora'"));
            var listed = await SendAsync(client, StorefrontPhpAjax.CpWorkshopEndpointPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "list_jobs")), staff);
            Assert.Contains("D-999", listed.Body, StringComparison.Ordinal);
            var jobId = created.Json.RootElement.GetProperty("job").GetProperty("header").GetProperty("id").GetInt32();
            var status = await SendAsync(client, StorefrontPhpAjax.CpWorkshopEndpointPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_status"), ("job_id", jobId.ToString(CultureInfo.InvariantCulture)), ("status", "in_progress")), staff);
            Assert.Equal("Status updated", status.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("in_progress", await ScalarAsync(connectionString, "SELECT `status` FROM `epc_ws_jobs` WHERE `id`=" + jobId.ToString(CultureInfo.InvariantCulture)));
            var badStatus = await SendAsync(client, StorefrontPhpAjax.CpWorkshopEndpointPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_status"), ("job_id", jobId.ToString(CultureInfo.InvariantCulture)), ("status", "nope")), staff);
            Assert.Equal("Invalid job or status", badStatus.Json.RootElement.GetProperty("message").GetString());
            var workshopUnknown = await SendAsync(client, StorefrontPhpAjax.CpWorkshopEndpointPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "nope")), staff);
            Assert.Equal("Unknown action", workshopUnknown.Json.RootElement.GetProperty("message").GetString());
            var seeded = await SendAsync(client, StorefrontPhpAjax.CpWorkshopEndpointPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "seed_demo")), staff);
            Assert.Equal("Demo garage data ready", seeded.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `epc_ws_jobs` WHERE `job_no`='WS-DEMO-001'"));
            Assert.Equal("8", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `epc_ws_labour_ops`"));

            var crossGuest = await SendAsync(client, StorefrontPhpAjax.CpCrossesOperationsPath, Form(("request_object", "{\"action\":\"get_table_crosses\"}")), string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, crossGuest.Status);
            Assert.Equal("forbidden", crossGuest.Json.RootElement.GetProperty("message").GetString());
            var crossCsrf = await SendAsync(client, StorefrontPhpAjax.CpCrossesOperationsPath, Form(("request_object", "{\"action\":\"add_crosses\"}")), staff);
            Assert.Contains("CSRF 1", crossCsrf.Body, StringComparison.Ordinal);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_docpart_articles_analogs_list'"));
            var crossMissing = await SendAsync(client, StorefrontPhpAjax.CpCrossesOperationsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table_crosses\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.CrossLinksMissing, crossMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", crossMissing.Body, StringComparison.OrdinalIgnoreCase);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_articles_analogs_list (
                  id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
                  article VARCHAR(64) NOT NULL,
                  manufacturer_article VARCHAR(64) NOT NULL,
                  analog VARCHAR(64) NOT NULL,
                  manufacturer_analog VARCHAR(64) NOT NULL
                )
                """);
            var crossBad = await SendAsync(client, StorefrontPhpAjax.CpCrossesOperationsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "not-json")), staff);
            Assert.Equal("bad_request", crossBad.Json.RootElement.GetProperty("message").GetString());
            var crossAdd = await SendAsync(client, StorefrontPhpAjax.CpCrossesOperationsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"add_crosses\",\"article\":\"0986\",\"manufacturer_article\":\"BOSCH\",\"analog\":\"0987\",\"manufacturer_analog\":\"MANN\"}")), staff);
            Assert.True(crossAdd.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list`"));
            Assert.Equal("BOSCH", await ScalarAsync(connectionString, "SELECT `manufacturer_article` FROM `shop_docpart_articles_analogs_list` WHERE `article`='0986'"));
            var crossTable = await SendAsync(client, StorefrontPhpAjax.CpCrossesOperationsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table_crosses\",\"article\":\"0986\"}")), staff);
            Assert.Contains("0986", crossTable.Body, StringComparison.Ordinal);
            var makers = await SendAsync(client, StorefrontPhpAjax.CpCrossesOperationsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_search_manufacturer\",\"article\":\"0986\"}")), staff);
            Assert.Contains("BOSCH", makers.Json.RootElement.GetProperty("list_manufacturer").GetString(), StringComparison.Ordinal);
            var crossId = await ScalarAsync(connectionString, "SELECT `id` FROM `shop_docpart_articles_analogs_list` LIMIT 1");
            var crossDel = await SendAsync(client, StorefrontPhpAjax.CpCrossesOperationsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"del_crosses\",\"id\":" + crossId + "}")), staff);
            Assert.True(crossDel.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list`"));
            var wipe = await SendAsync(client, StorefrontPhpAjax.CpCrossesOperationsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"del_search_crosses\",\"article\":\"\",\"manufacturer\":\"\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.CrossDeleteNeedsFilter, wipe.Json.RootElement.GetProperty("message").GetString());

            var priceGuest = await SendAsync(client, StorefrontPhpAjax.CpPricesEditPath, Form(("request_object", "{\"action\":\"get_table\"}")), "admin_session=nope; admin_u_id=9");
            Assert.False(priceGuest.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.DoesNotContain("message", priceGuest.Body, StringComparison.Ordinal);
            var priceMissing = await SendAsync(client, StorefrontPhpAjax.CpPricesEditPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.PriceRowsMissing, priceMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", priceMissing.Body, StringComparison.OrdinalIgnoreCase);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_prices_data (
                  id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
                  price_id INT NOT NULL,
                  manufacturer VARCHAR(64) NOT NULL,
                  article VARCHAR(64) NOT NULL,
                  article_show VARCHAR(64) NOT NULL DEFAULT '',
                  name VARCHAR(128) NOT NULL DEFAULT '',
                  exist INT NOT NULL DEFAULT 0,
                  price DECIMAL(12,2) NOT NULL DEFAULT 0,
                  time_to_exe INT NOT NULL DEFAULT 0,
                  storage VARCHAR(64) NOT NULL DEFAULT '',
                  min_order INT NOT NULL DEFAULT 1
                )
                """);
            var emptyTable = await SendAsync(client, StorefrontPhpAjax.CpPricesEditPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table\"}")), staff);
            Assert.Contains("Nothing found", emptyTable.Body, StringComparison.Ordinal);
            var priceAdd = await SendAsync(client, StorefrontPhpAjax.CpPricesEditPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"add\",\"price_id\":4,\"article\":\"09-86\",\"manufacturer\":\"bosch\",\"article_show\":\"shown\",\"name\":\"Pad\",\"exist\":\"2\",\"price\":\"12.5\",\"time_to_exe\":\"1\",\"storage\":\"A\",\"min_order\":\"1\"}")), staff);
            Assert.True(priceAdd.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("0986", await ScalarAsync(connectionString, "SELECT `article` FROM `shop_docpart_prices_data`"));
            Assert.Equal("BOSCH", await ScalarAsync(connectionString, "SELECT `manufacturer` FROM `shop_docpart_prices_data`"));
            var priceRow = await ScalarAsync(connectionString, "SELECT `id` FROM `shop_docpart_prices_data`");
            var shown = await SendAsync(client, StorefrontPhpAjax.CpPricesEditPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table\"}")), staff);
            Assert.Contains("0986", shown.Body, StringComparison.Ordinal);
            var priceSave = await SendAsync(client, StorefrontPhpAjax.CpPricesEditPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"save\",\"id\":" + priceRow + ",\"price_id\":4,\"article\":\"0986\",\"manufacturer\":\"BOSCH\",\"name\":\"Pad\",\"exist\":\"2\",\"price\":\"15\",\"time_to_exe\":\"1\",\"storage\":\"A\",\"min_order\":\"1\"}")), staff);
            Assert.True(priceSave.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("15.00", await ScalarAsync(connectionString, "SELECT CAST(`price` AS CHAR) FROM `shop_docpart_prices_data` WHERE `id`=" + priceRow));
            var searched = await SendAsync(client, StorefrontPhpAjax.CpPosEndpointPath, Form(("action", "search_products"), ("q", "0986")), staff);
            Assert.Equal("BOSCH", searched.Json.RootElement.GetProperty("products")[0].GetProperty("brand").GetString());
            var priceDel = await SendAsync(client, StorefrontPhpAjax.CpPricesEditPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"del\",\"id\":" + priceRow + "}")), staff);
            Assert.True(priceDel.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `shop_docpart_prices_data`"));

            var modalCsrf = await SendAsync(client, StorefrontPhpAjax.CpLoadUserModalPath, Form(("customer_id", "7")), staff);
            Assert.Contains("CSRF 1", modalCsrf.Body, StringComparison.Ordinal);
            var modalMissing = await SendAsync(client, StorefrontPhpAjax.CpLoadUserModalPath, Form(("csrf_guard_key", "admin-csrf"), ("customer_id", "7")), staff);
            Assert.Equal(StorefrontPhpAjax.CustomerAccountingMissing, modalMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", modalMissing.Body, StringComparison.OrdinalIgnoreCase);
            await ExecuteAsync(connectionString, "CREATE TABLE users (user_id INT NOT NULL PRIMARY KEY, email VARCHAR(128) NOT NULL, phone VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO users (user_id, email, phone) VALUES (7, 'sara@example.test', '0500')");
            await ExecuteAsync(connectionString, "CREATE TABLE `groups` (id INT NOT NULL PRIMARY KEY, value VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO `groups` (id, value) VALUES (3, 'Retail')");
            await ExecuteAsync(connectionString, "CREATE TABLE users_groups_bind (user_id INT NOT NULL, group_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO users_groups_bind (user_id, group_id) VALUES (7, 3)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_users_accounting (id INT NOT NULL PRIMARY KEY AUTO_INCREMENT, user_id INT NOT NULL, amount DECIMAL(12,2) NOT NULL, income TINYINT NOT NULL, active TINYINT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_users_accounting (user_id, amount, income, active) VALUES (7, 1500.50, 1, 1), (7, 200.00, 0, 1)");
            var modal = await SendAsync(client, StorefrontPhpAjax.CpLoadUserModalPath, Form(("csrf_guard_key", "admin-csrf"), ("customer_id", "7")), staff);
            Assert.True(modal.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Contains("5579", modal.Json.RootElement.GetProperty("modal").GetString(), StringComparison.Ordinal);
            Assert.Contains(">7<", modal.Json.RootElement.GetProperty("modal").GetString(), StringComparison.Ordinal);
            Assert.Contains("1 300.50", modal.Json.RootElement.GetProperty("modal").GetString(), StringComparison.Ordinal);
            Assert.Contains("Retail", modal.Json.RootElement.GetProperty("modal").GetString(), StringComparison.Ordinal);

            var noFile = await SendAsync(client, StorefrontPhpAjax.CpDemandUploadTmpPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal("No file", noFile.Json.RootElement.GetProperty("message").GetString());
            var textFile = await SendFileAsync(client, StorefrontPhpAjax.CpCrossesUploadTmpPath, "admin-csrf", "pads.txt", "x"u8.ToArray(), staff);
            Assert.Equal("Use .csv files only", textFile.Json.RootElement.GetProperty("message").GetString());
            Assert.False(Directory.Exists(Path.Combine(configRoot, "cp", "tmp")));

            var handleBad = await SendAsync(client, StorefrontPhpAjax.CpCrossesHandleFilePath, Form(("csrf_guard_key", "admin-csrf"), ("import_options", "{")), staff);
            Assert.Equal("bad_request", handleBad.Json.RootElement.GetProperty("message").GetString());
            var handleOutside = await SendAsync(client, StorefrontPhpAjax.CpCrossesHandleFilePath, Form(("csrf_guard_key", "admin-csrf"), ("import_options", "{\"file_full_path\":\"/etc/passwd\"}")), staff);
            Assert.Equal("Invalid file path", handleOutside.Json.RootElement.GetProperty("message").GetString());
            var tmp = Path.Combine(configRoot, "cp", "tmp");
            Directory.CreateDirectory(tmp);
            var csvPath = Path.Combine(tmp, "links.csv");
            await File.WriteAllTextAsync(csvPath, "manufacturer;article;manufacturer_cross;article_cross\nBOSCH;0986;MANN;0987\n");
            var imported = await SendAsync(client, StorefrontPhpAjax.CpCrossesHandleFilePath, Form(("csrf_guard_key", "admin-csrf"), ("import_options", "{\"file_full_path\":" + JsonSerializer.Serialize(csvPath) + "}")), staff);
            Assert.Contains("Imported 1 link(s)", imported.Body, StringComparison.Ordinal);
            Assert.False(File.Exists(csvPath));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list` WHERE `article`='0986'"));

            var dashboard = await SendAsync(client, StorefrontPhpAjax.CpBulkCpPath, Form(("action", "dashboard")), staff);
            Assert.Equal(0, dashboard.Json.RootElement.GetProperty("dashboard").GetProperty("total").GetInt32());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_bulk_upload_history'"));
            await ExecuteAsync(connectionString, "INSERT INTO epc_bulk_upload_history (user_id, file_name, created_at, updated_at) VALUES (7, 'pads.csv', NOW(), NOW())");
            var history = await SendAsync(client, StorefrontPhpAjax.CpBulkCpPath, Form(("action", "list_history")), staff);
            Assert.Equal("pads.csv", history.Json.RootElement.GetProperty("rows")[0].GetProperty("file_name").GetString());
            var uploadId = history.Json.RootElement.GetProperty("rows")[0].GetProperty("id").GetInt32();
            var reviewed = await SendAsync(client, StorefrontPhpAjax.CpBulkCpPath, Form(("action", "mark_reviewed"), ("upload_id", uploadId.ToString(CultureInfo.InvariantCulture)), ("notes", "seen")), staff);
            Assert.Equal("Marked reviewed", reviewed.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("9", await ScalarAsync(connectionString, "SELECT CAST(`cp_reviewed_by` AS CHAR) FROM `epc_bulk_upload_history` WHERE `id`=" + uploadId.ToString(CultureInfo.InvariantCulture)));
            var missingUpload = await SendAsync(client, StorefrontPhpAjax.CpBulkCpPath, Form(("action", "get_upload"), ("upload_id", "0")), staff);
            Assert.Equal("Upload not found", missingUpload.Json.RootElement.GetProperty("message").GetString());
            var crm = await SendAsync(client, StorefrontPhpAjax.CpBulkCpPath, Form(("action", "create_crm_quote"), ("upload_id", "0")), staff);
            Assert.Equal("Upload not found", crm.Json.RootElement.GetProperty("message").GetString());
            var bulkUnknown = await SendAsync(client, StorefrontPhpAjax.CpBulkCpPath, Form(("action", "nope")), staff);
            Assert.Equal("Unknown action", bulkUnknown.Json.RootElement.GetProperty("message").GetString());
            var beforePrices = await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `shop_docpart_prices_data`");
            var process = await SendAsync(client, StorefrontPhpAjax.CpBulkCpPath, Form(("action", "process_upload")), staff);
            Assert.Equal("Select a customer first", process.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(beforePrices, await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `shop_docpart_prices_data`"));

            var posGuest = await SendAsync(client, StorefrontPhpAjax.CpPosEndpointPath, Form(("action", "search_products"), ("q", "0986")), string.Empty);
            Assert.Equal("Access denied", posGuest.Json.RootElement.GetProperty("message").GetString());
            var posScript = await SendAsync(client, StorefrontPhpAjax.CpPosPath, Form(("action", "search_products")), staff);
            Assert.Equal("No access", posScript.Body);
            var procurementScript = await SendAsync(client, CpLegacyPhpAjaxLinks.ProcurementScript, Form(("action", "create_supplier")), staff);
            Assert.Equal("No access", procurementScript.Body);
            var erpScript = await SendAsync(client, CpLegacyPhpAjaxLinks.ErpScript, Form(("action", "create_supplier")), staff);
            Assert.Equal("No access", erpScript.Body);
            var crmScript = await SendAsync(client, CpLegacyPhpAjaxLinks.CrmScript, Form(("action", "save_lead")), staff);
            Assert.Equal("No access", crmScript.Body);
            var customerScript = await SendAsync(client, CpLegacyPhpAjaxLinks.CustomerScript, Form(("action", "save_customer")), staff);
            Assert.Equal("No access", customerScript.Body);
            var packDir = Path.Combine(configRoot, "cp", "tmp", "pack_setup");
            Directory.CreateDirectory(packDir);
            var junk = Path.Combine(packDir, "junk.csv");
            var keep = Path.Combine(packDir, "index.html");
            await File.WriteAllTextAsync(junk, "x");
            await File.WriteAllTextAsync(keep, "stay");
            var packed = await SendAsync(client, CpLegacyPhpAjaxLinks.PricePackSetup + "?key=local-tech", null, string.Empty);
            Assert.Equal(string.Empty, packed.Body);
            Assert.False(File.Exists(junk));
            Assert.Equal("stay", await File.ReadAllTextAsync(keep));
            await ExecuteAsync(connectionString, "DROP TABLE shop_docpart_prices_data");
            var keysMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.PriceEnableKeys + "?key=local-tech", null, string.Empty);
            Assert.Equal(0, keysMissing.Json.RootElement.GetProperty("result").GetInt32());
            Assert.Equal(StorefrontPhpAjax.PriceRowsMissing, keysMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_prices_data (id INT NOT NULL PRIMARY KEY, article VARCHAR(64) NOT NULL)");
            var keys = await SendAsync(client, CpLegacyPhpAjaxLinks.PriceEnableKeys + "?key=local-tech", null, string.Empty);
            Assert.Equal(1, keys.Json.RootElement.GetProperty("result").GetInt32());
            var keysGuest = await SendAsync(client, CpLegacyPhpAjaxLinks.PriceEnableKeys, null, string.Empty);
            Assert.Equal("Forbidden", keysGuest.Json.RootElement.GetProperty("message").GetString());
            var uploadDir = Path.Combine(configRoot, "cp", "tmp", "prices_upload_files");
            Directory.CreateDirectory(uploadDir);
            var zipPath = Path.Combine(uploadDir, "pads.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("pads.csv");
                await using var stream = entry.Open();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync("article,price\n0986,12.50");
            }

            var extracted = await SendAsync(client, CpLegacyPhpAjaxLinks.PriceExtract + "?key=local-tech", null, string.Empty);
            Assert.True(extracted.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(1, extracted.Json.RootElement.GetProperty("packs_count").GetInt32());
            Assert.Equal(1, extracted.Json.RootElement.GetProperty("packs_successfully_extracted").GetInt32());
            Assert.Equal(0, extracted.Json.RootElement.GetProperty("packs_error").GetInt32());
            Assert.False(File.Exists(zipPath));
            Assert.Contains("0986", await File.ReadAllTextAsync(Path.Combine(uploadDir, "pads.csv")), StringComparison.Ordinal);
            var health = await SendAsync(client, CpLegacyPhpAjaxLinks.PypricesHealth, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.False(health.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.True(health.Json.RootElement.GetProperty("critical").GetBoolean());
            Assert.Equal("pyprices unavailable", health.Json.RootElement.GetProperty("message").GetString());
            var detailGuest = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersDetailPane + "?order_id=12", null, string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, detailGuest.Status);
            Assert.Contains("Access denied", detailGuest.Body, StringComparison.Ordinal);
            var detailMissing = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersDetailPane + "?order_id=12", null, staff);
            Assert.Contains(StorefrontPhpAjax.OrdersMissing, detailMissing.Body, StringComparison.Ordinal);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders (id INT NOT NULL PRIMARY KEY, status INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders (id, status) VALUES (12, 1)");
            var detail = await SendAsync(client, CpLegacyPhpAjaxLinks.OrdersDetailPane + "?order_id=12", null, staff);
            Assert.Contains("data-order-id=\"12\"", detail.Body, StringComparison.Ordinal);
            await ExecuteAsync(connectionString, "DROP TABLE shop_orders");
            var sale = await SendAsync(client, StorefrontPhpAjax.CpPosEndpointPath, Form(("action", "complete_sale"), ("lines", "[]")), staff);
            Assert.Equal(StorefrontPhpAjax.CompleteSaleNotPosted, sale.Json.RootElement.GetProperty("message").GetString());
            Assert.False(sale.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_orders'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_pos_sales'"));
            var emptySearch = await SendAsync(client, StorefrontPhpAjax.CpPosEndpointPath, Form(("action", "search_products"), ("q", "")), staff);
            Assert.Equal(0, emptySearch.Json.RootElement.GetProperty("products").GetArrayLength());
            var opened = await SendAsync(client, StorefrontPhpAjax.CpPosEndpointPath, Form(("action", "open_session"), ("opening_float", "25.50")), staff);
            Assert.True(opened.Json.RootElement.GetProperty("status").GetBoolean());
            var sessionNo = opened.Json.RootElement.GetProperty("session").GetProperty("session_no").GetString();
            var sessionId = opened.Json.RootElement.GetProperty("session").GetProperty("session_id").GetInt64();
            var again = await SendAsync(client, StorefrontPhpAjax.CpPosEndpointPath, Form(("action", "open_session"), ("opening_float", "1")), staff);
            Assert.Contains("already open (" + sessionNo + ")", again.Body, StringComparison.Ordinal);
            var closed = await SendAsync(client, StorefrontPhpAjax.CpPosEndpointPath, Form(("action", "close_session"), ("session_id", sessionId.ToString(CultureInfo.InvariantCulture)), ("closing_cash", "25.50")), staff);
            Assert.Equal("25.50", await ScalarAsync(connectionString, "SELECT CAST(`expected_cash` AS CHAR) FROM `epc_pos_sessions` WHERE `id`=" + sessionId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("closed", await ScalarAsync(connectionString, "SELECT `status` FROM `epc_pos_sessions` WHERE `id`=" + sessionId.ToString(CultureInfo.InvariantCulture)));
            Assert.True(closed.Json.RootElement.GetProperty("status").GetBoolean());
            var posStatus = await SendAsync(client, StorefrontPhpAjax.CpPosEndpointPath, Form(("action", "session_status")), staff);
            Assert.Equal(JsonValueKind.Null, posStatus.Json.RootElement.GetProperty("session").ValueKind);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_pos_sales'"));

            var catalogueGuest = await SendAsync(client, StorefrontPhpAjax.CpCatalogueProductsPath, Form(("request_object", "{\"action\":\"get_table\"}")), string.Empty);
            Assert.False(catalogueGuest.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.False(catalogueGuest.Json.RootElement.TryGetProperty("message", out _));
            var catalogueMissing = await SendAsync(client, StorefrontPhpAjax.CpCatalogueProductsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.CatalogueProductsMissing, catalogueMissing.Json.RootElement.GetProperty("message").GetString());
            var ymlGuest = await SendAsync(client, StorefrontPhpAjax.CpYmlExportPath + "?export_options=" + Uri.EscapeDataString("{\"data_output_mode\":\"download_file\"}") + "&csrf_guard_key=admin-csrf", null, string.Empty);
            Assert.Equal("Forbidden", ymlGuest.Json.RootElement.GetProperty("message").GetString());
            var ymlMissing = await SendAsync(client, StorefrontPhpAjax.CpYmlExportPath + "?export_options=" + Uri.EscapeDataString("{\"data_output_mode\":\"download_file\",\"arr_category\":[62]}") + "&csrf_guard_key=admin-csrf", null, staff);
            Assert.Equal(StorefrontPhpAjax.CatalogueProductsMissing, ymlMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.False(File.Exists(Path.Combine(configRoot, "cp", "tmp", "yml_dump_DBS_download.xml")));
            await ExecuteAsync(connectionString, "CREATE TABLE shop_catalogue_products (id INT NOT NULL PRIMARY KEY, caption VARCHAR(255) NOT NULL, category_id INT NOT NULL, published_flag INT NOT NULL, min_limit INT NOT NULL, min_limit_enable INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_catalogue_products (id, caption, category_id, published_flag, min_limit, min_limit_enable) VALUES (41, 'Pad', 62, 1, 0, 0)");
            var limitValue = await SendAsync(client, StorefrontPhpAjax.CpCatalogueProductsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"save_product_value_limit\",\"product_id\":41,\"value\":3}")), staff);
            Assert.True(limitValue.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT CAST(`min_limit` AS CHAR) FROM `shop_catalogue_products` WHERE `id`=41"));
            var limitStatus = await SendAsync(client, StorefrontPhpAjax.CpCatalogueProductsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"save_product_status_limit\",\"product_id\":41,\"status\":1}")), staff);
            Assert.True(limitStatus.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(`min_limit_enable` AS CHAR) FROM `shop_catalogue_products` WHERE `id`=41"));
            var catalogueTable = await SendAsync(client, StorefrontPhpAjax.CpCatalogueProductsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table\",\"page\":1}")), staff);
            Assert.Contains("data-product-id=\"41\"", catalogueTable.Body, StringComparison.Ordinal);
            Assert.Contains("value=\"3\"", catalogueTable.Body, StringComparison.Ordinal);
            var yml = await SendAsync(client, StorefrontPhpAjax.CpYmlExportPath + "?export_options=" + Uri.EscapeDataString("{\"data_output_mode\":\"download_file\",\"FBY_flag\":0,\"arr_category\":[62]}") + "&csrf_guard_key=admin-csrf", null, staff);
            Assert.True(yml.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("yml_dump_DBS_download.xml", yml.Json.RootElement.GetProperty("filename").GetString());
            Assert.Equal(1, yml.Json.RootElement.GetProperty("_COUNT_products_categoryes_all").GetInt32());
            Assert.Equal(0, yml.Json.RootElement.GetProperty("_COUNT_products_read_all").GetInt32());
            Assert.Equal(1, yml.Json.RootElement.GetProperty("_COUNT_products_blocked_no_storage_record").GetInt32());
            Assert.Contains("<offers></offers>", File.ReadAllText(Path.Combine(configRoot, "cp", "tmp", "yml_dump_DBS_download.xml")), StringComparison.Ordinal);
            var ymlFby = await SendAsync(client, StorefrontPhpAjax.CpYmlExportPath + "?export_options=" + Uri.EscapeDataString("{\"data_output_mode\":\"create_file\",\"FBY_flag\":1,\"arr_category\":[62]}") + "&csrf_guard_key=admin-csrf", null, staff);
            Assert.Equal("yml_dump_FBY_FBS.xml", ymlFby.Json.RootElement.GetProperty("filename").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `docpart`.`users`"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `ecomae`.`users`"));
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

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
    }

    private static void WriteConfig(string root)
    {
        File.WriteAllText(Path.Combine(root, "config.php"), """
            <?php
            class DP_Config {
            public $tech_key = 'local-tech';
            public $secret_succession = 'local-secret';
            public $domain_path = 'http://local.test/';
            public $backend_dir = 'cp';
            }
            """);
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

    private static async Task<Sent> SendFileAsync(HttpClient client, string path, string csrf, string fileName, byte[] bytes, string cookie)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(csrf), "csrf_guard_key");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, "csv_file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return new Sent(response.StatusCode, body, JsonDocument.Parse(body));
    }

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<ProbeHost> StartAsync(string connectionString, string configRoot)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new FixedConnections(connectionString));
        builder.Services.AddSingleton<IStorefrontPriceAccess>(new GuestPrices());
        builder.Services.AddSingleton(ReporterStub.Create());
        builder.Services.Configure<PhpReferenceOptions>(options => options.PhpDocRoot = configRoot);
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
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

    private sealed class FixedConnections : ITenantDbConnectionFactory
    {
        private readonly string _connectionString;

        public FixedConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => OpenAsync();

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => OpenAsync();

        private async Task<System.Data.Common.DbConnection> OpenAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }

    private class ReporterStub : System.Reflection.DispatchProxy
    {
        public ReporterStub()
        {
        }

        public static ISurfaceDashboardSummaryReporter Create()
            => Create<ISurfaceDashboardSummaryReporter, ReporterStub>();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(targetMethod?.Name ?? "dispatch");
    }
}
