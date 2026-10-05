using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
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

public sealed class StorefrontPhpCatalogueDemandTests
{
    [Fact]
    public async Task CataloguePickupDemandAndGarage_OnThrowawayDatabase_ThenDropped()
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
            var user = "session=user-token";
            var adminCookie = "admin_session=admin-token; admin_u_id=9";
            var request = RequestJson(3, 1, string.Empty);

            var missingCount = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", request)), string.Empty);
            Assert.Equal(StorefrontPhpAjax.CatalogueProductsMissing, missingCount.Body);
            Assert.DoesNotContain("doesn't exist", missingCount.Body, StringComparison.OrdinalIgnoreCase);
            var missingPage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", request)), string.Empty);
            Assert.Equal(StorefrontPhpAjax.CatalogueProductsMissing, missingPage.Body);
            var missingList = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueListPath, Form(("propucts_request", request)), string.Empty);
            Assert.Equal(StorefrontPhpAjax.CatalogueProductsMissing, missingList.Body);

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_catalogue_products (
                  id INT NOT NULL PRIMARY KEY,
                  category_id INT NOT NULL,
                  caption VARCHAR(255) NOT NULL,
                  alias VARCHAR(255) NOT NULL,
                  published_flag TINYINT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO shop_catalogue_products (id, category_id, caption, alias, published_flag) VALUES
                (1, 3, 'Brake Pad', 'brake-pad', 1),
                (2, 3, 'Hidden Pad', 'hidden-pad', 0),
                (3, 4, 'Filter', 'filter', 1)
                """);
            var missingOffice = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", request)), string.Empty);
            Assert.Equal(StorefrontPhpAjax.OfficesMissing, missingOffice.Body);

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_offices (
                  id INT NOT NULL PRIMARY KEY,
                  city VARCHAR(64) NOT NULL,
                  address VARCHAR(255) NOT NULL,
                  timetable VARCHAR(255) NOT NULL,
                  phone VARCHAR(64) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices (id, city, address, timetable, phone) VALUES (4, 'Dubai', 'Al Quoz', '9-6\nSat', '050')");
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_catalogue_categories (
                  id INT NOT NULL PRIMARY KEY,
                  url VARCHAR(64) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_catalogue_categories (id, url) VALUES (3, 'pads'), (4, 'filters')");

            var count = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", request)), string.Empty);
            Assert.Equal("1", count.Body);
            var adminCount = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestJson(3, 2, string.Empty))), string.Empty);
            Assert.Equal("2", adminCount.Body);
            var other = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestJson(9, 1, string.Empty))), string.Empty);
            Assert.Equal("0", other.Body);

            var page = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", request)), string.Empty);
            Assert.Contains("product_div_tile col-xs-12 col-sm-4 col-md-4 col-lg-3", page.Body, StringComparison.Ordinal);
            Assert.Contains("Brake Pad", page.Body, StringComparison.Ordinal);
            Assert.Contains("/pads/brake-pad", page.Body, StringComparison.Ordinal);
            Assert.Contains(">4106<", page.Body, StringComparison.Ordinal);
            Assert.Contains(">4111<", page.Body, StringComparison.Ordinal);
            Assert.Contains(">4099<", page.Body, StringComparison.Ordinal);
            Assert.Contains("product_div_price\">**", page.Body, StringComparison.Ordinal);
            Assert.Contains(">3608<", page.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Hidden Pad", page.Body, StringComparison.Ordinal);
            var emptyPage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestJson(9, 1, string.Empty))), string.Empty);
            Assert.Contains("<div style=\"text-center\">4078</div>", emptyPage.Body, StringComparison.Ordinal);
            var list = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueListPath, Form(("propucts_request", request)), string.Empty);
            Assert.Equal(string.Empty, list.Body);
            Assert.Contains("text/html", list.ContentType, StringComparison.OrdinalIgnoreCase);

            var searchMissing = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestJson(3, 1, "Brake"))), string.Empty);
            Assert.Equal(StorefrontPhpAjax.CatalogueTextSearchMissing, searchMissing.Body);
            await ExecuteAsync(connectionString, """
                CREATE TABLE lang_text_strings_translation (
                  str_id VARCHAR(64) NOT NULL,
                  str_key VARCHAR(64) NOT NULL,
                  value VARCHAR(255) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO lang_text_strings_translation (str_id, str_key, value) VALUES ('501', '501', 'Brake Pad')");
            await ExecuteAsync(connectionString, "UPDATE shop_catalogue_products SET caption = '501' WHERE id = 1");
            var descriptionMissing = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestJson(3, 1, "Brake"))), string.Empty);
            Assert.Equal(StorefrontPhpAjax.ProductDescriptionsMissing, descriptionMissing.Body);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_products_text (product_id INT NOT NULL, content VARCHAR(64) NOT NULL)");
            var found = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestJson(3, 1, "Brake"))), string.Empty);
            Assert.Equal("1", found.Body);
            var missed = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestJson(3, 1, "zzzz"))), string.Empty);
            Assert.Equal("0", missed.Body);
            var translated = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestJson(3, 1, "Brake"))), string.Empty);
            Assert.Contains("Brake Pad", translated.Body, StringComparison.Ordinal);
            Assert.Contains("<div style=\"text-center\">4078</div>", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestJson(3, 1, "zzzz"))), string.Empty)).Body, StringComparison.Ordinal);

            var pickupMissing = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PickupTimingPath, Form(("office_id", "4")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.OfficeStoragesMissing, pickupMissing.Body);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_offices_storages_map (
                  office_id INT NOT NULL,
                  storage_id INT NOT NULL,
                  additional_time INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE sessions (
                  id INT NOT NULL PRIMARY KEY,
                  session VARCHAR(64) NOT NULL,
                  user_id INT NOT NULL,
                  type INT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO sessions (id, session, user_id, type) VALUES
                (12, 'user-token', 7, 0),
                (15, 'admin-token', 9, 1)
                """);
            var guest = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PickupTimingPath, Form(("office_id", "4")), string.Empty);
            Assert.False(guest.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("incorrect_session", guest.Json.RootElement.GetProperty("code").GetString());
            Assert.Equal("Session error", guest.Json.RootElement.GetProperty("message").GetString());

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_carts (
                  id INT NOT NULL PRIMARY KEY,
                  user_id INT NOT NULL,
                  session_id INT NOT NULL,
                  product_type INT NOT NULL,
                  checked_for_order TINYINT NOT NULL,
                  t2_storage_id INT NOT NULL DEFAULT 0,
                  t2_time_to_exe INT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_carts_details (
                  id INT NOT NULL PRIMARY KEY,
                  cart_record_id INT NOT NULL,
                  storage_id INT NOT NULL,
                  storage_record_id INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_storages_data (
                  id INT NOT NULL PRIMARY KEY,
                  arrival_time INT NOT NULL,
                  time_to_exe INT NOT NULL,
                  exist INT NOT NULL,
                  reserved INT NOT NULL
                )
                """);
            var ready = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PickupTimingPath, Form(("office_id", "4")), user);
            Assert.Contains("alert-success", ready.Body, StringComparison.Ordinal);
            Assert.Contains("4421", ready.Body, StringComparison.Ordinal);
            Assert.Contains("nextStep()", ready.Body, StringComparison.Ordinal);
            Assert.Contains("4447", ready.Body, StringComparison.Ordinal);
            Assert.Contains("3376: Dubai, Al Quoz", ready.Body, StringComparison.Ordinal);
            Assert.Contains("4446: 9-6<br>Sat", ready.Body, StringComparison.Ordinal);
            Assert.Contains("1312: 050", ready.Body, StringComparison.Ordinal);

            await ExecuteAsync(connectionString, "INSERT INTO shop_carts (id, user_id, session_id, product_type, checked_for_order) VALUES (1, 7, 0, 1, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_carts_details (id, cart_record_id, storage_id, storage_record_id) VALUES (1, 1, 8, 3)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages_data (id, arrival_time, time_to_exe, exist, reserved) VALUES (3, 1, 0, 5, 1)");
            var refused = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PickupTimingPath, Form(("office_id", "4")), user);
            Assert.Contains("alert-danger", refused.Body, StringComparison.Ordinal);
            Assert.Contains("4422", refused.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("nextStep()", refused.Body, StringComparison.Ordinal);
            Assert.Equal("5", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id = 3"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id = 3"));

            await ExecuteAsync(connectionString, "INSERT INTO shop_offices_storages_map (office_id, storage_id, additional_time) VALUES (4, 8, 48)");
            var later = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PickupTimingPath, Form(("office_id", "4")), user);
            Assert.Contains("alert-warning", later.Body, StringComparison.Ordinal);
            Assert.Contains("4442", later.Body, StringComparison.Ordinal);
            Assert.Contains("4443", later.Body, StringComparison.Ordinal);
            Assert.Equal("5", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id = 3"));

            var demandGuest = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandMetaPath, null, string.Empty);
            Assert.False(demandGuest.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("auth", demandGuest.Json.RootElement.GetProperty("code").GetString());
            Assert.Equal(StorefrontPhpAjax.DemandAuthMessage, demandGuest.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_article_demand'"));

            var meta = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandMetaPath, null, user);
            Assert.True(meta.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.False(meta.Json.RootElement.GetProperty("access").GetProperty("is_admin").GetBoolean());
            Assert.Equal(string.Empty, meta.Json.RootElement.GetProperty("access").GetProperty("default_country").GetString());
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_demand_country"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_article_demand"));

            var showcaseBlocked = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandShowcasePath + "?reseed=0", null, user);
            Assert.Equal(StorefrontPhpAjax.PriceListsMissing, showcaseBlocked.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(0, showcaseBlocked.Json.RootElement.GetProperty("parts").GetArrayLength());
            Assert.Equal(0, showcaseBlocked.Json.RootElement.GetProperty("seeded").GetInt32());
            Assert.DoesNotContain("Piston (demo)", showcaseBlocked.Body, StringComparison.Ordinal);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_article_demand"));

            var locked = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandByCountryPath + "?country=SDN", null, user);
            Assert.False(locked.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("forbidden", locked.Json.RootElement.GetProperty("code").GetString());
            Assert.Equal(StorefrontPhpAjax.NoDemandCountry, locked.Json.RootElement.GetProperty("message").GetString());

            var card = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandCardPath, null, user);
            Assert.True(card.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("TOYOTA", card.Json.RootElement.GetProperty("brand").GetString());
            Assert.Equal("1310154101", card.Json.RootElement.GetProperty("article_norm").GetString());
            Assert.Equal(3, card.Json.RootElement.GetProperty("demand_countries").GetArrayLength());
            Assert.Equal(0, card.Json.RootElement.GetProperty("fitment").GetProperty("vehicle_count").GetInt32());
            Assert.Equal(string.Empty, card.Json.RootElement.GetProperty("fitment").GetProperty("fitment_source").GetString());
            Assert.Equal(0, card.Json.RootElement.GetProperty("sellable_crosses").GetArrayLength());
            Assert.Equal("no_stock_signal", card.Json.RootElement.GetProperty("supply_status").GetString());
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_article_demand WHERE manufacturer = 'TOYOTA' AND article_norm = '1310154101'"));
            Assert.Equal("DZA,KEN,SDN", await ScalarAsync(connectionString, "SELECT GROUP_CONCAT(country_code ORDER BY country_code) FROM epc_article_demand"));

            await ExecuteAsync(connectionString, "INSERT INTO epc_user_demand_country (user_id, country_code) VALUES (7, 'SDN')");
            var tags = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandTagsPath, null, user);
            Assert.True(tags.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("SDN", tags.Json.RootElement.GetProperty("country").GetString());
            Assert.Equal(JsonValueKind.Array, tags.Json.RootElement.GetProperty("index").GetProperty("TOYOTA|1310154101").ValueKind);
            var own = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandByCountryPath + "?country=SDN", null, user);
            Assert.True(own.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(1, own.Json.RootElement.GetProperty("total").GetInt32());
            Assert.Equal("TOYOTA", own.Json.RootElement.GetProperty("parts")[0].GetProperty("brand").GetString());
            var otherCountry = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandByCountryPath + "?country=KEN", null, user);
            Assert.Equal("You can only view demand intelligence for Sudan.", otherCountry.Json.RootElement.GetProperty("message").GetString());

            var adminMeta = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandMetaPath, null, adminCookie);
            Assert.True(adminMeta.Json.RootElement.GetProperty("access").GetProperty("is_admin").GetBoolean());
            Assert.Equal("SDN", adminMeta.Json.RootElement.GetProperty("access").GetProperty("default_country").GetString());
            var unknown = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandByCountryPath + "?country=ZZZ", null, adminCookie);
            Assert.Equal(StorefrontPhpAjax.UnknownCountryCode, unknown.Json.RootElement.GetProperty("message").GetString());

            var none = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandVehiclesPath + "?action=start&country=SDN", null, user);
            Assert.True(none.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(0, none.Json.RootElement.GetProperty("parts_total").GetInt32());
            Assert.Equal("No in-stock price-list parts with demand tag for this country.", none.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(0, none.Json.RootElement.GetProperty("vehicles").GetArrayLength());

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_prices (
                  id INT NOT NULL PRIMARY KEY,
                  storefront_temp_disabled TINYINT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices (id, storefront_temp_disabled) VALUES (1, 0)");
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_prices_data (
                  id INT NOT NULL PRIMARY KEY,
                  price_id INT NOT NULL,
                  manufacturer VARCHAR(64) NOT NULL,
                  article VARCHAR(64) NOT NULL,
                  article_show VARCHAR(64) NOT NULL,
                  name VARCHAR(255) NOT NULL,
                  price DECIMAL(12,2) NOT NULL,
                  exist INT NOT NULL,
                  storage VARCHAR(64) NOT NULL
                )
                """);
            var demo = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandShowcasePath + "?reseed=0&limit=10", null, user);
            Assert.Equal("uae_top_stock", demo.Json.RootElement.GetProperty("source").GetString());
            Assert.Equal("Piston (demo)", demo.Json.RootElement.GetProperty("parts")[0].GetProperty("name").GetString());
            Assert.Equal(0, demo.Json.RootElement.GetProperty("seeded").GetInt32());
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_article_demand"));

            await ExecuteAsync(connectionString, """
                INSERT INTO shop_docpart_prices_data (id, price_id, manufacturer, article, article_show, name, price, exist, storage)
                VALUES (1, 1, 'TOYOTA', '1310154101', '1310154101', 'Piston ring', 12.50, 4, 'R-UAE')
                """);
            var stocked = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandVehiclesPath + "?action=start&country=SDN", null, user);
            Assert.Equal(1, stocked.Json.RootElement.GetProperty("parts_total").GetInt32());
            Assert.Equal("Found 1 price-list parts for this country. Loading products & fitment…", stocked.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(0, stocked.Json.RootElement.GetProperty("vehicles").GetArrayLength());
            var jobId = stocked.Json.RootElement.GetProperty("job_id").GetString() ?? string.Empty;
            Assert.StartsWith("di_SDN_", jobId, StringComparison.Ordinal);
            var step = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandVehiclesPath + "?action=step&job_id=" + jobId, null, user);
            Assert.True(step.Json.RootElement.GetProperty("done").GetBoolean());
            Assert.Equal("Scan complete — products and vehicles ready.", step.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(0, step.Json.RootElement.GetProperty("vehicles").GetArrayLength());
            Assert.Equal(0, step.Json.RootElement.GetProperty("summary").GetProperty("vehicles_count").GetInt32());
            Assert.Equal("Piston", step.Json.RootElement.GetProperty("part_lines")[0].GetProperty("product_group").GetString());
            Assert.Equal(string.Empty, step.Json.RootElement.GetProperty("fitment_source").GetString());
            Assert.DoesNotContain("Corolla", step.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("ucats", step.Body, StringComparison.OrdinalIgnoreCase);
            var missingJob = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandVehiclesPath + "?action=step&job_id=missing", null, user);
            Assert.Equal("Job not found or expired", missingJob.Json.RootElement.GetProperty("message").GetString());
            var badAction = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.DemandVehiclesPath + "?action=nope", null, user);
            Assert.Equal("Unknown action", badAction.Json.RootElement.GetProperty("message").GetString());

            var models = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.GarageMarkModelsPath, Form(("to_mark", "4")), user);
            Assert.Equal("{\"status\":false}", models.Body);
            Assert.False(models.Json.RootElement.TryGetProperty("list", out _));
            var types = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.GarageModelTypesPath, Form(("to_model", "9")), user);
            Assert.Equal("{\"status\":false}", types.Body);
            Assert.False(types.Json.RootElement.TryGetProperty("list", out _));

            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `docpart`.`users`"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `ecomae`.`users`"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'ecomae_cpw_%'"));
    }

    [Fact]
    public async Task CataloguePropertyFiltersAndUcatsAjax_OnThrowawayDatabase_ThenDropped()
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
            foreach (var path in StorefrontPhpAjax.UcatsProductPaths.Concat(StorefrontPhpAjax.UcatsGroupFieldPaths))
            {
                foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
                {
                    var ucats = await SendAsync(client, method, path, Form(("products_query", "{\"brand\":\"BOSCH\"}"), ("group", "5")), string.Empty);
                    Assert.Equal("null", ucats.Body);
                    Assert.Contains("json", ucats.ContentType, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("[", ucats.Body, StringComparison.Ordinal);
                    Assert.DoesNotContain("BOSCH", ucats.Body, StringComparison.Ordinal);
                    Assert.DoesNotContain("status", ucats.Body, StringComparison.OrdinalIgnoreCase);
                }
            }

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_catalogue_products (
                  id INT NOT NULL PRIMARY KEY,
                  category_id INT NOT NULL,
                  caption VARCHAR(255) NOT NULL,
                  alias VARCHAR(255) NOT NULL,
                  published_flag TINYINT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO shop_catalogue_products (id, category_id, caption, alias, published_flag) VALUES
                (1, 3, 'Pad A', 'pad-a', 1),
                (2, 3, 'Pad B', 'pad-b', 1),
                (9, 3, 'Hidden', 'hidden', 0),
                (10, 3, 'Pad Dry', 'pad-dry', 1),
                (11, 4, 'Other', 'other', 1)
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_offices (
                  id INT NOT NULL PRIMARY KEY,
                  city VARCHAR(64) NOT NULL,
                  address VARCHAR(255) NOT NULL,
                  timetable VARCHAR(255) NOT NULL,
                  phone VARCHAR(64) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices (id, city, address, timetable, phone) VALUES (4, 'Dubai', 'Al Quoz', '9-6', '050')");

            var plain = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestJson(3, 1, string.Empty))), string.Empty);
            Assert.Equal("3", plain.Body);

            var intFilter = RangeProperty(1, 11, 10, 0, 30, 100);
            var missingInt = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, intFilter))), string.Empty);
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, missingInt.Body);
            Assert.DoesNotContain("doesn't exist", missingInt.Body, StringComparison.OrdinalIgnoreCase);
            var missingList = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueListPath, Form(("propucts_request", RequestWith(3, 1, intFilter))), string.Empty);
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, missingList.Body);
            Assert.Equal("5", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_catalogue_products"));

            await ExecuteAsync(connectionString, "CREATE TABLE shop_properties_values_int (product_id INT NOT NULL, property_id INT NOT NULL, value INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_properties_values_int (product_id, property_id, value) VALUES (1, 11, 5), (2, 11, 20)");
            var intCount = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, intFilter))), string.Empty);
            Assert.Equal("1", intCount.Body);
            var intPage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestWith(3, 1, intFilter))), string.Empty);
            Assert.Contains("Pad B", intPage.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Pad A", intPage.Body, StringComparison.Ordinal);
            var fullInt = RangeProperty(1, 11, 0, 0, 100, 100);
            Assert.Equal("3", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, fullInt))), string.Empty)).Body);
            Assert.Equal(string.Empty, (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueListPath, Form(("propucts_request", RequestWith(3, 1, intFilter))), string.Empty)).Body);

            var floatFilter = RangeProperty(2, 12, 5, 0, 12, 100);
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, floatFilter))), string.Empty)).Body);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_properties_values_float (product_id INT NOT NULL, property_id INT NOT NULL, value DECIMAL(10,2) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_properties_values_float (product_id, property_id, value) VALUES (1, 12, 1.50), (2, 12, 9.50)");
            Assert.Equal("1", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, floatFilter))), string.Empty)).Body);
            var floatPage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestWith(3, 1, floatFilter))), string.Empty);
            Assert.Contains("Pad B", floatPage.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Pad A", floatPage.Body, StringComparison.Ordinal);

            var boolFilter = "{\"property_type_id\":4,\"property_id\":13,\"true_checked\":true,\"false_checked\":false}";
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, boolFilter))), string.Empty)).Body);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_properties_values_bool (product_id INT NOT NULL, property_id INT NOT NULL, value TINYINT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_properties_values_bool (product_id, property_id, value) VALUES (1, 13, 1), (2, 13, 0)");
            Assert.Equal("1", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, boolFilter))), string.Empty)).Body);
            var boolPage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestWith(3, 1, boolFilter))), string.Empty);
            Assert.Contains("Pad A", boolPage.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Pad B", boolPage.Body, StringComparison.Ordinal);
            var bothBool = "{\"property_type_id\":4,\"property_id\":13,\"true_checked\":true,\"false_checked\":true}";
            Assert.Equal("3", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, bothBool))), string.Empty)).Body);

            var listFilter = "{\"property_type_id\":5,\"property_id\":14,\"list_type\":1,\"list_options\":[{\"id\":4,\"value\":false},{\"id\":8,\"value\":true}]}";
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, listFilter))), string.Empty)).Body);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_properties_values_list (product_id INT NOT NULL, property_id INT NOT NULL, value INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_properties_values_list (product_id, property_id, value) VALUES (1, 14, 4), (1, 14, 8), (2, 14, 8)");
            Assert.Equal("2", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, listFilter))), string.Empty)).Body);
            var listPage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestWith(3, 1, listFilter))), string.Empty);
            Assert.Contains("Pad A", listPage.Body, StringComparison.Ordinal);
            Assert.Contains("Pad B", listPage.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Pad Dry", listPage.Body, StringComparison.Ordinal);
            var listAnd = "{\"property_type_id\":5,\"property_id\":14,\"list_type\":2,\"list_options\":[{\"id\":4,\"value\":true},{\"id\":8,\"value\":true}]}";
            Assert.Equal("1", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, listAnd))), string.Empty)).Body);
            var andPage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestWith(3, 1, listAnd))), string.Empty);
            Assert.Contains("Pad A", andPage.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Pad B", andPage.Body, StringComparison.Ordinal);
            var listNone = "{\"property_type_id\":5,\"property_id\":14,\"list_type\":1,\"list_options\":[{\"id\":4,\"value\":false},{\"id\":8,\"value\":false}]}";
            Assert.Equal("3", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, listNone))), string.Empty)).Body);

            var treeAll = "{\"property_type_id\":6,\"property_id\":15,\"current_level\":1,\"current_value\":0}";
            Assert.Equal("3", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, treeAll))), string.Empty)).Body);
            var treeFilter = "{\"property_type_id\":6,\"property_id\":15,\"current_level\":2,\"current_value\":7}";
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, treeFilter))), string.Empty)).Body);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_properties_values_tree_list (product_id INT NOT NULL, property_id INT NOT NULL, value INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_properties_values_tree_list (product_id, property_id, value) VALUES (1, 15, 3), (2, 15, 7)");
            Assert.Equal("1", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, treeFilter))), string.Empty)).Body);
            var treePage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestWith(3, 1, treeFilter))), string.Empty);
            Assert.Contains("Pad B", treePage.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Pad A", treePage.Body, StringComparison.Ordinal);

            var priceWide = "{\"property_id\":\"price\",\"min_need\":0,\"min_value\":0,\"max_need\":100,\"max_value\":100}";
            Assert.Equal("3", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, priceWide))), string.Empty)).Body);
            var priceFilter = "{\"property_id\":\"price\",\"min_need\":10,\"min_value\":0,\"max_need\":30,\"max_value\":100}";
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, priceFilter))), string.Empty)).Body);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages_data (id INT NOT NULL PRIMARY KEY, product_id INT NOT NULL, storage_id INT NOT NULL, price DECIMAL(10,2) NOT NULL, exist INT NOT NULL, reserved INT NOT NULL)");
            await ExecuteAsync(connectionString, """
                INSERT INTO shop_storages_data (id, product_id, storage_id, price, exist, reserved) VALUES
                (1, 1, 8, 15.00, 3, 0),
                (2, 2, 8, 50.00, 2, 0),
                (3, 2, 9, 20.00, 5, 0),
                (4, 10, 8, 12.00, 0, 0),
                (5, 11, 8, 12.00, 5, 0)
                """);
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestWith(3, 1, priceFilter))), string.Empty)).Body);
            Assert.Equal("5", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_catalogue_products"));
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages (id INT NOT NULL PRIMARY KEY, interface_type INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, interface_type) VALUES (8, 1), (9, 2)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_offices_storages_map (office_id INT NOT NULL, storage_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices_storages_map (office_id, storage_id) VALUES (4, 8), (4, 9)");
            Assert.Equal("1", (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, priceFilter))), string.Empty)).Body);
            var pricePage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CataloguePagePath, Form(("propucts_request", RequestWith(3, 1, priceFilter))), string.Empty);
            Assert.Contains("Pad A", pricePage.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Pad B", pricePage.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Pad Dry", pricePage.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Other", pricePage.Body, StringComparison.Ordinal);
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT exist FROM shop_storages_data WHERE id = 1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT reserved FROM shop_storages_data WHERE id = 1"));

            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'epc_erp%'"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `docpart`.`users`"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `ecomae`.`users`"));
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'ecomae_cpw_%'"));
    }

    private static string RequestJson(int categoryId, int blockType, string search)
        => "{\"category_id\":" + categoryId.ToString(CultureInfo.InvariantCulture)
            + ",\"properties_list\":[],\"product_block_type\":" + blockType.ToString(CultureInfo.InvariantCulture)
            + ",\"productsPerPage\":10,\"needPagesCount\":1,\"startFrom\":0,\"page_style\":1,\"search_string\":\"" + search + "\"}";

    private static string RequestWith(int categoryId, int blockType, string property)
        => "{\"category_id\":" + categoryId.ToString(CultureInfo.InvariantCulture)
            + ",\"properties_list\":[" + property + "],\"product_block_type\":" + blockType.ToString(CultureInfo.InvariantCulture)
            + ",\"productsPerPage\":10,\"needPagesCount\":1,\"startFrom\":0,\"page_style\":1,\"search_string\":\"\"}";

    private static string RangeProperty(int typeId, int propertyId, int minNeed, int minValue, int maxNeed, int maxValue)
        => "{\"property_type_id\":" + typeId.ToString(CultureInfo.InvariantCulture)
            + ",\"property_id\":" + propertyId.ToString(CultureInfo.InvariantCulture)
            + ",\"min_need\":" + minNeed.ToString(CultureInfo.InvariantCulture)
            + ",\"min_value\":" + minValue.ToString(CultureInfo.InvariantCulture)
            + ",\"max_need\":" + maxNeed.ToString(CultureInfo.InvariantCulture)
            + ",\"max_value\":" + maxValue.ToString(CultureInfo.InvariantCulture) + "}";

    private static Dictionary<string, string> Form(params (string Key, string Value)[] fields)
    {
        var form = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            form[field.Key] = field.Value;
        }

        return form;
    }

    private static async Task<Sent> SendAsync(HttpClient client, HttpMethod method, string path, Dictionary<string, string>? form, string cookie)
    {
        using var request = new HttpRequestMessage(method, path);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonDocument json = null!;
        if (body.Length > 0 && body[0] is '{' or '[')
        {
            json = JsonDocument.Parse(body);
        }

        return new Sent(body, response.Content.Headers.ContentType?.ToString() ?? string.Empty, json);
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

    private static async Task<ProbeHost> StartAsync(string connectionString)
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
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private sealed record Sent(string Body, string ContentType, JsonDocument Json);

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
