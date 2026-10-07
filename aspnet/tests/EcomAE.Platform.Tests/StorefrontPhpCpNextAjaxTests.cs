using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPhpCpNextAjaxTests
{
    [Fact]
    public async Task CpNextAjax_OnThrowawayDatabase_ThenDropped()
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
        WriteConfig(configRoot, false);
        try
        {
            await using var host = await StartAsync(connectionString, configRoot);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var staff = "admin_session=admin-token; admin_u_id=9";

            var noSessions = await SendAsync(client, StorefrontPhpAjax.CpSaveStringDescriptionPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("value", "Hi")), staff);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, noSessions.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf')");

            var langMissing = await SendAsync(client, StorefrontPhpAjax.CpSaveStringDescriptionPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("value", "Hi")), staff);
            Assert.Equal(StorefrontPhpAjax.LangEditorMissing, langMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE content (
                  id INT NOT NULL PRIMARY KEY,
                  url VARCHAR(64) NOT NULL DEFAULT '',
                  alias VARCHAR(64) NOT NULL DEFAULT '',
                  parent INT NOT NULL DEFAULT 0,
                  is_frontend INT NOT NULL DEFAULT 0,
                  value VARCHAR(64) NOT NULL DEFAULT '',
                  description VARCHAR(64) NOT NULL DEFAULT '',
                  title_tag VARCHAR(64) NOT NULL DEFAULT '',
                  description_tag VARCHAR(64) NOT NULL DEFAULT '',
                  keywords_tag VARCHAR(64) NOT NULL DEFAULT '',
                  author_tag VARCHAR(64) NOT NULL DEFAULT ''
                )
                """);
            await ExecuteAsync(connectionString, "CREATE TABLE content_access (id INT NOT NULL PRIMARY KEY, content_id INT NOT NULL, group_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE users_groups_bind (id INT NOT NULL PRIMARY KEY, user_id INT NOT NULL, group_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO content (id, url, is_frontend) VALUES (11, 'lang/editor', 0)");
            await ExecuteAsync(connectionString, "INSERT INTO content_access (id, content_id, group_id) VALUES (1, 11, 3)");
            await ExecuteAsync(connectionString, "INSERT INTO users_groups_bind (id, user_id, group_id) VALUES (1, 9, 3)");

            var stringsMissing = await SendAsync(client, StorefrontPhpAjax.CpSaveStringDescriptionPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("value", "Hi")), staff);
            Assert.Equal(StorefrontPhpAjax.LangStringsMissing, stringsMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", stringsMissing.Body, StringComparison.OrdinalIgnoreCase);
            await ExecuteAsync(connectionString, "CREATE TABLE lang_languages (id INT NOT NULL PRIMARY KEY AUTO_INCREMENT, lang_code VARCHAR(16) NOT NULL, restrict_edit INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "CREATE TABLE lang_text_strings (id INT NOT NULL PRIMARY KEY AUTO_INCREMENT, str_key VARCHAR(64) NOT NULL, description VARCHAR(255) NOT NULL DEFAULT '', `same` VARCHAR(16) NULL, is_error INT NOT NULL DEFAULT 0, is_custom INT NOT NULL DEFAULT 0, used_found INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "CREATE TABLE lang_text_strings_translation (id INT NOT NULL PRIMARY KEY AUTO_INCREMENT, str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(16) NOT NULL, value VARCHAR(255) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO lang_languages (lang_code) VALUES ('en')");
            await ExecuteAsync(connectionString, "INSERT INTO lang_text_strings (str_key, description, used_found) VALUES ('hello', 'Greeting', 1)");

            var described = await SendAsync(client, StorefrontPhpAjax.CpSaveStringDescriptionPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("value", "a <b> &")), staff);
            Assert.Equal("a &lt;b&gt; &amp;", described.Json.RootElement.GetProperty("new_value").GetString());
            Assert.Equal("a <b> &", await ScalarAsync(connectionString, "SELECT description FROM lang_text_strings WHERE str_key = 'hello'"));
            var badUsed = await SendAsync(client, StorefrontPhpAjax.CpSetUsedFoundPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("used_found", "9")), staff);
            Assert.Equal(StorefrontPhpAjax.IncorrectUsedFound, badUsed.Json.RootElement.GetProperty("message").GetString());
            var used = await SendAsync(client, StorefrontPhpAjax.CpSetUsedFoundPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("used_found", "1")), staff);
            Assert.Equal(1, used.Json.RootElement.GetProperty("used_found").GetInt32());

            var inserted = await SendAsync(client, StorefrontPhpAjax.CpSaveTranslationPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("lang_code", "en"), ("value", "Hello")), staff);
            Assert.Equal("Hello", inserted.Json.RootElement.GetProperty("value").GetString());
            var updated = await SendAsync(client, StorefrontPhpAjax.CpSaveTranslationPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("lang_code", "en"), ("value", "Hi &")), staff);
            Assert.Equal("Hi &amp;", updated.Json.RootElement.GetProperty("value").GetString());
            Assert.Equal("Hi &", await ScalarAsync(connectionString, "SELECT value FROM lang_text_strings_translation WHERE str_key = 'hello' AND lang_code = 'en'"));

            var created = await SendAsync(client, StorefrontPhpAjax.CpCreateStringPath, Form(("csrf_guard_key", "admin-csrf"), ("description", "Custom"), ("same", "no"), ("is_error", "0"), ("is_custom", "1"), ("used_found", "2")), staff);
            Assert.True(created.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(0, created.Json.RootElement.GetProperty("str").GetProperty("has_en").GetInt32());
            Assert.Equal(JsonValueKind.Null, created.Json.RootElement.GetProperty("str").GetProperty("same").ValueKind);
            var customKey = created.Json.RootElement.GetProperty("str").GetProperty("str_key").GetString();
            Assert.Contains("_1_", customKey, StringComparison.Ordinal);
            await SendAsync(client, StorefrontPhpAjax.CpSaveTranslationPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", customKey!), ("lang_code", "en"), ("value", "Mine")), staff);
            var removed = await SendAsync(client, StorefrontPhpAjax.CpDeleteUnusedStringsPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal("OK", removed.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM lang_text_strings WHERE description = 'Custom'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM lang_text_strings_translation WHERE str_key = '" + customKey + "'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM lang_text_strings WHERE str_key = 'hello'"));

            var searchMiss = await SendAsync(client, StorefrontPhpAjax.CpSearchUsedFoundPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal(StorefrontPhpAjax.SearchFailed, searchMiss.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", searchMiss.Body, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT used_found FROM lang_text_strings WHERE str_key = 'hello'"));
            await CreateUsageTablesAsync(connectionString);
            await ExecuteAsync(connectionString, "INSERT INTO `groups` (id, value, description) VALUES (3, 'hello', '')");
            await ExecuteAsync(connectionString, "INSERT INTO lang_text_strings (str_key, description, is_custom, used_found) VALUES ('orphan', 'Unused', 1, 0)");
            var searched = await SendAsync(client, StorefrontPhpAjax.CpSearchUsedFoundPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal("OK", searched.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT used_found FROM lang_text_strings WHERE str_key = 'hello'"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT used_found FROM lang_text_strings WHERE str_key = 'orphan'"));

            var forbiddenAlias = await SendAsync(client, StorefrontPhpAjax.CpContentAliasPath + "?code=nope&alias=pads&content_id=2&parent=1&is_frontend=1", null, staff);
            Assert.Equal("Forbidden", forbiddenAlias.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "INSERT INTO content (id, url, alias, parent, is_frontend) VALUES (2, 'pads', 'pads', 1, 1), (3, 'other', 'pads', 1, 1), (4, 'solo', 'solo', 1, 1)");
            var duplicated = await SendAsync(client, StorefrontPhpAjax.CpContentAliasPath + "?code=local-secret&alias=pads&content_id=9&parent=1&is_frontend=1", null, staff);
            Assert.Equal("duplicated", duplicated.Json.RootElement.GetProperty("result_code").GetString());
            var ownAlias = await SendAsync(client, StorefrontPhpAjax.CpContentAliasPath + "?code=local-secret&alias=solo&content_id=4&parent=1&is_frontend=1", null, staff);
            Assert.Equal("ok", ownAlias.Json.RootElement.GetProperty("result_code").GetString());

            var guestMfr = await SendAsync(client, StorefrontPhpAjax.CpManufacturersPath, Form(("request_object", "{\"action\":\"get_manufacturers\"}")), "session=user-token; u_id=7");
            Assert.Equal(StorefrontPhpAjax.ManufacturerForbidden, guestMfr.Json.RootElement.GetProperty("message").GetString());
            var csrfMfr = await SendAsync(client, StorefrontPhpAjax.CpManufacturersPath, Form(("request_object", "{\"action\":\"get_manufacturers\"}")), staff);
            Assert.Contains("CSRF 1", csrfMfr.Body, StringComparison.Ordinal);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_docpart_manufacturers'"));
            var added = await SendAsync(client, StorefrontPhpAjax.CpManufacturersPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"add_manufacturer\",\"name\":\"Bosch\"}")), staff);
            Assert.True(added.Json.RootElement.GetProperty("status").GetBoolean());
            var duplicate = await SendAsync(client, StorefrontPhpAjax.CpManufacturersPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"add_manufacturer\",\"name\":\"Bosch\"}")), staff);
            Assert.Equal("duplicate", duplicate.Json.RootElement.GetProperty("message").GetString());
            var mfrId = added.Json.RootElement.GetProperty("id").GetInt32();
            var synonym = await SendAsync(client, StorefrontPhpAjax.CpManufacturersPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"add_synonym\",\"id\":" + mfrId.ToString(CultureInfo.InvariantCulture) + ",\"name\":\"BOS\"}")), staff);
            Assert.True(synonym.Json.RootElement.GetProperty("status").GetBoolean());
            var listed = await SendAsync(client, StorefrontPhpAjax.CpManufacturersPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_synonyms\",\"id\":" + mfrId.ToString(CultureInfo.InvariantCulture) + "}")), staff);
            Assert.Equal("BOS", listed.Json.RootElement.GetProperty("synonyms")[0].GetProperty("synonym").GetString());
            await SendAsync(client, StorefrontPhpAjax.CpManufacturersPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"del_manufacturer\",\"id\":" + mfrId.ToString(CultureInfo.InvariantCulture) + "}")), staff);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_manufacturers"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_manufacturers_synonyms"));

            var groupsMissing = await SendAsync(client, StorefrontPhpAjax.CpStorageGroupsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.WarehouseGroupsMissing, groupsMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, interface_type INT NOT NULL, hidden INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages_groups (id INT NOT NULL PRIMARY KEY AUTO_INCREMENT, name VARCHAR(64) NOT NULL, storages VARCHAR(255) NOT NULL, `order` INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, name, interface_type) VALUES (1, 'Own', 1), (6, 'Sharjah', 4)");
            var choices = await SendAsync(client, StorefrontPhpAjax.CpStorageGroupsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_storages\"}")), staff);
            Assert.Equal("Sharjah", choices.Json.RootElement.GetProperty("storages_list")[0].GetProperty("name").GetString());
            Assert.Equal(1, choices.Json.RootElement.GetProperty("storages_list").GetArrayLength());
            var emptyTable = await SendAsync(client, StorefrontPhpAjax.CpStorageGroupsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table\"}")), staff);
            Assert.Contains("No warehouse groups yet.", emptyTable.Body, StringComparison.Ordinal);
            var grouped = await SendAsync(client, StorefrontPhpAjax.CpStorageGroupsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"add_group\",\"name\":\"Async\",\"storages\":[6]}")), staff);
            Assert.True(grouped.Json.RootElement.GetProperty("status").GetBoolean());
            var table = await SendAsync(client, StorefrontPhpAjax.CpStorageGroupsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"get_table\"}")), staff);
            Assert.Contains("Async", table.Body, StringComparison.Ordinal);
            Assert.Contains("Sharjah", table.Body, StringComparison.Ordinal);
            var groupId = await ScalarAsync(connectionString, "SELECT id FROM shop_storages_groups LIMIT 1");
            await SendAsync(client, StorefrontPhpAjax.CpStorageGroupsPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"del\",\"id\":" + groupId + "}")), staff);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_storages_groups"));

            var invalidReturn = await SendAsync(client, StorefrontPhpAjax.CpReturnActionPath, Form(("csrf_guard_key", "nope"), ("action", "set_return_status"), ("return_id", "0")), staff);
            Assert.Equal(StorefrontPhpAjax.ReturnCsrf, invalidReturn.Json.RootElement.GetProperty("message").GetString());
            var missingReturn = await SendAsync(client, StorefrontPhpAjax.CpReturnActionPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_return_status"), ("return_id", "4"), ("status_id", "3")), staff);
            Assert.Equal(StorefrontPhpAjax.ReturnStatusesMissing, missingReturn.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", missingReturn.Body, StringComparison.OrdinalIgnoreCase);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_returns (id INT NOT NULL PRIMARY KEY, status_id INT NOT NULL, return_complete INT NULL, `sum` DECIMAL(12,2) NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_returns_statuses (id INT NOT NULL PRIMARY KEY, caption VARCHAR(64) NOT NULL, color VARCHAR(16) NOT NULL DEFAULT '')");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_returns_items (id INT NOT NULL PRIMARY KEY, return_id INT NOT NULL, item_id INT NOT NULL, return_success INT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_items (id INT NOT NULL PRIMARY KEY, order_id INT NOT NULL, status INT NOT NULL, price DECIMAL(12,2) NOT NULL, count_need INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_logs (id INT NOT NULL PRIMARY KEY AUTO_INCREMENT, order_id INT NOT NULL, time INT NOT NULL, user_id INT NOT NULL, is_manager INT NOT NULL, text VARCHAR(255) NOT NULL, is_robot INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_returns_statuses (id, caption) VALUES (1, '3796'), (2, '3806'), (3, '3798')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items_statuses_ref (id, name, complete_return, reject_return) VALUES (8, 'Approved', 1, 0), (9, 'Rejected', 0, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_returns (id, status_id, return_complete) VALUES (4, 1, 0)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_returns_items (id, return_id, item_id, return_success) VALUES (5, 4, 20, NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items (id, order_id, status, price, count_need) VALUES (20, 41, 1, 10.00, 2)");
            var pending = await SendAsync(client, StorefrontPhpAjax.CpReturnActionPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "finalize_return"), ("return_id", "4")), staff);
            Assert.Equal(StorefrontPhpAjax.ReturnDecideFirst, pending.Json.RootElement.GetProperty("message").GetString());
            var decided = await SendAsync(client, StorefrontPhpAjax.CpReturnActionPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "decide_line"), ("return_id", "4"), ("line_id", "5"), ("decide", "1")), staff);
            Assert.True(decided.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("8", await ScalarAsync(connectionString, "SELECT status FROM shop_orders_items WHERE id = 20"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT status_id FROM shop_orders_returns WHERE id = 4"));
            Assert.Contains("approved", await ScalarAsync(connectionString, "SELECT text FROM shop_orders_logs WHERE order_id = 41"), StringComparison.Ordinal);
            var closed = await SendAsync(client, StorefrontPhpAjax.CpReturnActionPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "finalize_return"), ("return_id", "4")), staff);
            Assert.Equal(20m, closed.Json.RootElement.GetProperty("approved_sum").GetDecimal());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT return_complete FROM shop_orders_returns WHERE id = 4"));
            Assert.Contains("20.00", await ScalarAsync(connectionString, "SELECT text FROM shop_orders_logs WHERE text LIKE 'Return #4%'"), StringComparison.Ordinal);

            var toggleDenied = await SendAsync(client, StorefrontPhpAjax.CpStorageTogglePath, Form(("csrf_guard_key", "admin-csrf"), ("action", "toggle"), ("entity_type", "storage"), ("entity_id", "6"), ("storefront_enabled", "0")), staff);
            Assert.Equal("2388", toggleDenied.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "INSERT INTO content (id, url, is_frontend) VALUES (12, 'shop/prices', 0)");
            await ExecuteAsync(connectionString, "INSERT INTO content_access (id, content_id, group_id) VALUES (2, 12, 3)");
            var toggled = await SendAsync(client, StorefrontPhpAjax.CpStorageTogglePath, Form(("csrf_guard_key", "admin-csrf"), ("action", "toggle"), ("entity_type", "storage"), ("entity_id", "6"), ("storefront_enabled", "0")), staff);
            Assert.True(toggled.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(1, toggled.Json.RootElement.GetProperty("storefront_disabled").GetInt32());
            Assert.Equal("Sharjah", toggled.Json.RootElement.GetProperty("entity_name").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(storefront_temp_disabled AS CHAR) FROM shop_storages WHERE id = 6"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_storefront_storage_toggle_audit"));

            WriteConfig(configRoot, true);
            var blocked = await SendAsync(client, StorefrontPhpAjax.CpSaveStringDescriptionPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("value", "Changed")), staff);
            Assert.Equal(StorefrontPhpAjax.LangRestricted, blocked.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("a <b> &", await ScalarAsync(connectionString, "SELECT description FROM lang_text_strings WHERE str_key = 'hello'"));

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

    private static async Task CreateUsageTablesAsync(string connectionString)
    {
        var tables = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["config_groups"] = ["caption"],
            ["config_items"] = ["caption", "hint"],
            ["control_groups"] = ["caption"],
            ["control_items"] = ["caption"],
            ["groups"] = ["value", "description"],
            ["menu"] = ["caption", "structure"],
            ["metadata_handler_rules"] = ["title_rule"],
            ["modules"] = ["prototype_name", "caption", "data"],
            ["notifications_settings"] = ["caption", "description", "event", "email_subject", "email_body", "sms_body", "default_email_subject", "default_email_body", "default_sms_body", "vars"],
            ["plugins"] = ["caption", "description", "data_structure", "data_value"],
            ["reg_fields"] = ["caption"],
            ["reg_variants"] = ["caption"],
            ["shop_accounting_codes"] = ["name"],
            ["shop_catalogue_categories"] = ["value", "title_tag", "description_tag", "keywords_tag"],
            ["shop_catalogue_products"] = ["caption", "title_tag", "description_tag", "keywords_tag"],
            ["shop_categories_properties_map"] = ["value"],
            ["shop_currencies"] = ["caption_short"],
            ["shop_docpart_cars"] = ["caption"],
            ["shop_docpart_cars_catalogues"] = ["caption"],
            ["shop_docpart_prices_cols_types"] = ["caption"],
            ["shop_docpart_prices_load_modes"] = ["name"],
            ["shop_docpart_search_tabs"] = ["caption"],
            ["shop_geo"] = ["value"],
            ["shop_kkt_devices"] = ["name"],
            ["shop_kkt_interfaces_types"] = ["description"],
            ["shop_line_lists"] = ["caption"],
            ["shop_line_lists_items"] = ["value"],
            ["shop_main_page_groups"] = ["caption"],
            ["shop_obtaining_modes"] = ["caption"],
            ["shop_offices"] = ["caption", "country", "region", "city", "address", "description", "timetable"],
            ["shop_orders_items_statuses_ref"] = ["name", "complete_return", "reject_return"],
            ["shop_orders_statuses_ref"] = ["name"],
            ["shop_payment_systems"] = ["name", "description"],
            ["shop_print_docs"] = ["caption", "description"],
            ["shop_products_stickers"] = ["value", "description"],
            ["shop_products_text"] = ["content"],
            ["shop_properties_types"] = ["caption", "info"],
            ["shop_properties_values_text"] = ["value"],
            ["shop_sao_actions"] = ["name"],
            ["shop_sao_states"] = ["name"],
            ["shop_tree_lists"] = ["caption"],
            ["shop_tree_lists_items"] = ["value"]
        };
        foreach (var table in tables)
        {
            var columns = string.Join(", ", table.Value.Select(column => "`" + column + "` VARCHAR(64) NOT NULL DEFAULT ''"));
            if (table.Key == "shop_orders_items_statuses_ref")
            {
                columns = "`name` VARCHAR(64) NOT NULL DEFAULT '', `complete_return` INT NOT NULL DEFAULT 0, `reject_return` INT NOT NULL DEFAULT 0";
            }

            await ExecuteAsync(connectionString, "CREATE TABLE `" + table.Key + "` (id INT NOT NULL PRIMARY KEY, " + columns + ")");
        }
    }

    private static void WriteConfig(string root, bool restricted)
    {
        var text = """
            <?php
            class DP_Config {
            public $tech_key = 'local-tech';
            public $secret_succession = 'local-secret';
            public $domain_path = 'http://local.test/';
            public $multilang_editor_restricted_mode = 'RESTRICTED';
            }
            """;
        File.WriteAllText(Path.Combine(root, "config.php"), text.Replace("RESTRICTED", restricted ? "1" : "0", StringComparison.Ordinal));
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
