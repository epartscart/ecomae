using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
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
            // query_products_all.php also prices through shop_currencies and the group markup columns of shop_offices_storages_map.
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, priceFilter))), string.Empty)).Body);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_currencies (id INT NOT NULL PRIMARY KEY, iso_code INT NOT NULL, rate DECIMAL(15,6) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_currencies (id, iso_code, rate) VALUES (1, 784, 1)");
            await ExecuteAsync(connectionString, "ALTER TABLE shop_storages ADD COLUMN currency INT NOT NULL DEFAULT 784");
            Assert.Equal(StorefrontPhpAjax.CataloguePropertyFiltersMissing, (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", RequestWith(3, 1, priceFilter))), string.Empty)).Body);
            await ExecuteAsync(connectionString, "ALTER TABLE shop_offices_storages_map ADD COLUMN group_id INT NOT NULL DEFAULT 0, ADD COLUMN min_point DECIMAL(15,2) NOT NULL DEFAULT 0, ADD COLUMN max_point DECIMAL(15,2) NOT NULL DEFAULT 999999999, ADD COLUMN markup DECIMAL(10,2) NOT NULL DEFAULT 0");
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

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
    }

    [Fact]
    public async Task UcatsPages_LocalFailureWithoutHttp_OnThrowawayDatabase_ThenDropped()
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
            foreach (var path in StorefrontPhpAjax.UcatsFragmentPaths)
            {
                foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
                {
                    var page = await SendAsync(client, method, path + "?tovar=4&car_name=Toyota&car_id=1", Form(("tovar", "4")), string.Empty);
                    Assert.Equal(StorefrontPhpAjax.UcatsNoAccess, page.Body);
                    Assert.Contains("html", page.ContentType, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Toyota", page.Body, StringComparison.Ordinal);
                    Assert.DoesNotContain("<h1>", page.Body, StringComparison.Ordinal);
                    Assert.DoesNotContain("search_tab_car", page.Body, StringComparison.Ordinal);
                }
            }

            var catalogues = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.UcatsCataloguesPath, null, string.Empty);
            Assert.Equal(StorefrontPhpAjax.UcatsConfigMissing, catalogues.Body);
            Assert.DoesNotContain("4585", catalogues.Body, StringComparison.Ordinal);
            Assert.Equal(StorefrontPhpAjax.UcatsConfigMissing, StorefrontPhpAjax.UcatsCatalogues(new Dictionary<string, string>()));
            Assert.Equal(string.Empty, StorefrontPhpAjax.UcatsCatalogues(new Dictionary<string, string> { ["tech_key"] = "local" }));
            var shiny = StorefrontPhpAjax.UcatsCatalogues(new Dictionary<string, string>
            {
                ["ucats_shiny"] = "1",
                ["ucats_oil"] = string.Empty
            });
            Assert.Contains("section-title\">4584", shiny, StringComparison.Ordinal);
            Assert.Contains("href=\"/shop/katalogi-ucats/shiny\"", shiny, StringComparison.Ordinal);
            Assert.Contains("new-cat-block-tires", shiny, StringComparison.Ordinal);
            Assert.Contains("navbar-inverse\">4585", shiny, StringComparison.Ordinal);
            Assert.DoesNotContain("/shop/katalogi-ucats/avtoximiya", shiny, StringComparison.Ordinal);
            Assert.DoesNotContain("4590", shiny, StringComparison.Ordinal);
            Assert.DoesNotContain("BOSCH", shiny, StringComparison.Ordinal);

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

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
    }

    [Fact]
    public async Task UcatsAuthControl_WritesOnThrowawayDatabase_ThenDropped()
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
            var missingBots = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UcatsAuthControlPath, Form(("x", "1")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.BotAddressesMissing, missingBots.Body);
            Assert.DoesNotContain("doesn't exist", missingBots.Body, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_ucats_auth_control'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'bot_ips'"));

            await ExecuteAsync(connectionString, "CREATE TABLE bot_ips (id INT NOT NULL PRIMARY KEY, `from` VARCHAR(64) NOT NULL, `to` VARCHAR(64) NOT NULL)");
            var missingAuth = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.UcatsAuthControlPath, null, string.Empty);
            Assert.Equal(StorefrontPhpAjax.UcatsAccessControlMissing, missingAuth.Body);
            Assert.DoesNotContain("doesn't exist", missingAuth.Body, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_ucats_auth_control'"));

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_ucats_auth_control (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  time INT NOT NULL,
                  ip VARCHAR(64) NOT NULL,
                  user_id INT NOT NULL,
                  queries_count INT NOT NULL
                )
                """);
            var first = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UcatsAuthControlPath, Form(("x", "1")), string.Empty);
            Assert.Equal(string.Empty, first.Body);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_ucats_auth_control"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT user_id FROM shop_ucats_auth_control"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT queries_count FROM shop_ucats_auth_control"));
            var ip = await ScalarAsync(connectionString, "SELECT ip FROM shop_ucats_auth_control");
            Assert.False(string.IsNullOrWhiteSpace(ip));
            var storedTime = long.Parse(await ScalarAsync(connectionString, "SELECT time FROM shop_ucats_auth_control"), CultureInfo.InvariantCulture);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.InRange(storedTime, now - 30, now + 5);

            Assert.Equal(string.Empty, (await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.UcatsAuthControlPath, null, string.Empty)).Body);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT queries_count FROM shop_ucats_auth_control"));

            await ExecuteAsync(connectionString, "UPDATE shop_ucats_auth_control SET queries_count = 101");
            var blocked = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.UcatsAuthControlPath, null, string.Empty);
            Assert.Equal(StorefrontPhpAjax.UcatsForbidden, blocked.Body);
            Assert.Equal("101", await ScalarAsync(connectionString, "SELECT queries_count FROM shop_ucats_auth_control"));

            await ExecuteAsync(connectionString, "UPDATE shop_ucats_auth_control SET queries_count = 100");
            Assert.Equal(string.Empty, (await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.UcatsAuthControlPath, null, string.Empty)).Body);
            Assert.Equal("101", await ScalarAsync(connectionString, "SELECT queries_count FROM shop_ucats_auth_control"));

            await ExecuteAsync(connectionString, "DELETE FROM shop_ucats_auth_control");
            await ExecuteAsync(connectionString, "INSERT INTO bot_ips (id, `from`, `to`) VALUES (1, '" + ip + "', '" + ip + "')");
            var bot = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.UcatsAuthControlPath, null, string.Empty);
            Assert.Equal(StorefrontPhpAjax.UcatsForbidden, bot.Body);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_ucats_auth_control"));

            await ExecuteAsync(connectionString, "DELETE FROM bot_ips");
            await ExecuteAsync(connectionString, "INSERT INTO shop_ucats_auth_control (time, ip, user_id, queries_count) VALUES (" + (now - 90000).ToString(CultureInfo.InvariantCulture) + ", '" + ip + "', 0, 7)");
            Assert.Equal(string.Empty, (await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.UcatsAuthControlPath, null, string.Empty)).Body);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_ucats_auth_control"));
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT queries_count FROM shop_ucats_auth_control WHERE time < " + (now - 80000).ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT queries_count FROM shop_ucats_auth_control WHERE time > " + (now - 80000).ToString(CultureInfo.InvariantCulture)));

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

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
    }

    [Fact]
    public async Task QuotesTreeAndToMarks_OnThrowawayDatabase_ThenDropped()
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
        var product = "{\"product_type\":2,\"manufacturer\":\"BOSCH\",\"article\":\"0986\",\"article_show\":\"0986\",\"name\":\"Pad\",\"exist\":4,\"price\":12.5,\"time_to_exe\":1,\"time_to_exe_guaranteed\":2,\"storage\":\"SHJ\",\"min_order\":1,\"probability\":80,\"office_id\":4,\"storage_id\":8,\"price_purchase\":10,\"markup\":2,\"json_params\":\"\"}";
        var hash = Md5("BOSCH09860986Pad412.512SHJ180481022");
        var signed = "session=user-token; u_id=7";
        try
        {
            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };

            var missingSession = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAddPath, Form(("product_objects", "[" + product + ",\"check_hash\":\"" + hash + "\"}]")), signed);
            Assert.Equal(StorefrontPhpAjax.SessionsMissing, missingSession.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", missingSession.Body, StringComparison.OrdinalIgnoreCase);

            await ExecuteAsync(connectionString, """
                CREATE TABLE sessions (
                  id INT NOT NULL PRIMARY KEY,
                  session VARCHAR(64) NOT NULL,
                  user_id INT NOT NULL,
                  csrf_guard_key VARCHAR(64) NOT NULL DEFAULT ''
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, csrf_guard_key) VALUES (12, 'user-token', 7, 'csrf-1')");
            var guest = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAddPath, Form(("product_objects", "[]")), string.Empty);
            Assert.False(guest.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("auth", guest.Json.RootElement.GetProperty("code").GetString());
            Assert.Equal(StorefrontPhpAjax.GuestAuthMessage, guest.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(StorefrontPhpAjax.GuestLoginUrl, guest.Json.RootElement.GetProperty("login_url").GetString());

            var missingQuotes = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAddPath, Form(("product_objects", "[" + product.TrimEnd('}') + ",\"check_hash\":\"" + hash + "\"}]")), signed);
            Assert.Equal(StorefrontPhpAjax.QuotesMissing, missingQuotes.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_quote_requests'"));

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_quote_requests (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  user_id INT NOT NULL,
                  session_id INT NOT NULL,
                  status VARCHAR(32) NOT NULL,
                  time_created INT NOT NULL,
                  time_updated INT NOT NULL,
                  time_submitted INT NULL,
                  customer_note TEXT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_quote_items (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  quote_id INT NOT NULL,
                  product_type INT NOT NULL,
                  product_object_json TEXT NOT NULL,
                  count_need INT NOT NULL,
                  quoted_price DECIMAL(12,4) NULL,
                  quoted_time_to_exe INT NULL,
                  offer_alternative INT NULL,
                  alt_manufacturer VARCHAR(64) NULL,
                  alt_article VARCHAR(64) NULL,
                  alt_article_show VARCHAR(64) NULL,
                  alt_name VARCHAR(255) NULL,
                  alt_count_need INT NULL,
                  alt_quoted_price DECIMAL(12,4) NULL,
                  alt_storage_id INT NULL
                )
                """);

            var type1 = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAddPath, Form(("product_objects", "[{\"product_type\":1}]")), signed);
            Assert.Equal(StorefrontPhpAjax.QuoteType2Only, type1.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_quote_requests"));

            var bad = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAddPath, Form(("product_objects", "[" + product.TrimEnd('}') + ",\"check_hash\":\"nope\"}]")), signed);
            Assert.Equal("35", bad.Json.RootElement.GetProperty("code").GetString());
            Assert.Equal(StorefrontPhpAjax.QuoteHashFailed, bad.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_quote_requests"));

            var line = product.TrimEnd('}') + ",\"check_hash\":\"" + hash + "\"}";
            var added = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAddPath, Form(("product_objects", "[" + line + "]")), signed);
            Assert.True(added.Json.RootElement.GetProperty("status").GetBoolean());
            var quoteId = added.Json.RootElement.GetProperty("quote_id").GetInt64();
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_quote_items WHERE quote_id = " + quoteId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT user_id FROM shop_quote_requests WHERE id = " + quoteId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("draft", await ScalarAsync(connectionString, "SELECT status FROM shop_quote_requests WHERE id = " + quoteId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT count_need FROM shop_quote_items WHERE quote_id = " + quoteId.ToString(CultureInfo.InvariantCulture)));

            var again = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAddPath, Form(("product_objects", "[" + line + "]")), signed);
            Assert.Equal(quoteId, again.Json.RootElement.GetProperty("quote_id").GetInt64());
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_quote_items WHERE quote_id = " + quoteId.ToString(CultureInfo.InvariantCulture)));

            var manualMissing = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteManualPath, Form(("manufacturer", ""), ("article", "0986")), signed);
            Assert.Equal(StorefrontPhpAjax.QuoteBrandRequired, manualMissing.Json.RootElement.GetProperty("message").GetString());
            var manual = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteManualPath, Form(("manufacturer", "bosch & co"), ("article", "09-86"), ("count_need", "2")), signed);
            Assert.True(manual.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(quoteId, manual.Json.RootElement.GetProperty("quote_id").GetInt64());
            var manualJson = await ScalarAsync(connectionString, "SELECT product_object_json FROM shop_quote_items WHERE quote_id = " + quoteId.ToString(CultureInfo.InvariantCulture) + " ORDER BY id DESC LIMIT 1");
            Assert.Contains("BOSCH &amp; CO", manualJson, StringComparison.Ordinal);
            Assert.Contains("\"article\":\"0986\"", manualJson, StringComparison.Ordinal);
            Assert.Contains("\"check_hash\":\"manual\"", manualJson, StringComparison.Ordinal);
            Assert.Contains("\"epc_manual_quote\":1", manualJson, StringComparison.Ordinal);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT count_need FROM shop_quote_items WHERE quote_id = " + quoteId.ToString(CultureInfo.InvariantCulture) + " ORDER BY id DESC LIMIT 1"));

            await ExecuteAsync(connectionString, "INSERT INTO shop_quote_requests (id, user_id, session_id, status, time_created, time_updated) VALUES (90, 7, 0, 'draft', 1, 1)");
            var emptySubmit = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteSubmitPath, Form(("quote_id", "90"), ("customer_note", " later ")), signed);
            Assert.Equal(StorefrontPhpAjax.QuoteNeedLine, emptySubmit.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("draft", await ScalarAsync(connectionString, "SELECT status FROM shop_quote_requests WHERE id = 90"));

            var submitted = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteSubmitPath, Form(("quote_id", quoteId.ToString(CultureInfo.InvariantCulture)), ("customer_note", " please ")), signed);
            Assert.True(submitted.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.False(submitted.Json.RootElement.TryGetProperty("quote_id", out _));
            Assert.Equal("submitted", await ScalarAsync(connectionString, "SELECT status FROM shop_quote_requests WHERE id = " + quoteId.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal("please", await ScalarAsync(connectionString, "SELECT customer_note FROM shop_quote_requests WHERE id = " + quoteId.ToString(CultureInfo.InvariantCulture)));
            var againSubmit = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteSubmitPath, Form(("quote_id", quoteId.ToString(CultureInfo.InvariantCulture))), signed);
            Assert.Equal(StorefrontPhpAjax.QuoteNotDraft, againSubmit.Json.RootElement.GetProperty("message").GetString());

            var invalid = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAcceptPath, Form(("quote_id", "0")), signed);
            Assert.Equal(StorefrontPhpAjax.QuoteInvalid, invalid.Json.RootElement.GetProperty("message").GetString());
            var acceptProduct = "{\"product_type\":2,\"manufacturer\":\"BOSCH\",\"article\":\"0986\",\"article_show\":\"0986\",\"name\":\"Pad\",\"exist\":4,\"price\":12.5,\"time_to_exe\":1,\"time_to_exe_guaranteed\":2,\"storage\":\"SHJ\",\"min_order\":1,\"probability\":80,\"office_id\":4,\"storage_id\":8,\"price_purchase\":10,\"markup\":2,\"json_params\":\"\",\"count_need\":1}";
            await ExecuteAsync(connectionString, "INSERT INTO shop_quote_requests (id, user_id, session_id, status, time_created, time_updated) VALUES (91, 7, 0, 'quoted', 1, 1), (92, 7, 0, 'quoted', 1, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_quote_items (quote_id, product_type, product_object_json, count_need, quoted_price, quoted_time_to_exe) VALUES (91, 2, '" + acceptProduct + "', 1, 0, 3), (92, 2, '" + acceptProduct + "', 1, 15, 3)");
            var incomplete = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAcceptPath, Form(("quote_id", "91")), signed);
            Assert.Equal(StorefrontPhpAjax.QuoteIncomplete, incomplete.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("quoted", await ScalarAsync(connectionString, "SELECT status FROM shop_quote_requests WHERE id = 91"));
            var noCart = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAcceptPath, Form(("quote_id", "92")), signed);
            Assert.Equal(StorefrontPhpAjax.QuoteAcceptFailed, noCart.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("quoted", await ScalarAsync(connectionString, "SELECT status FROM shop_quote_requests WHERE id = 92"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_carts'"));

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_carts (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  product_type INT NULL,
                  price DECIMAL(12,4) NULL,
                  count_need INT NULL,
                  time INT NULL,
                  user_id INT NULL,
                  session_id INT NULL,
                  t2_manufacturer VARCHAR(64) NULL,
                  t2_article VARCHAR(64) NULL,
                  t2_article_show VARCHAR(64) NULL,
                  t2_name VARCHAR(255) NULL,
                  t2_exist INT NULL,
                  t2_time_to_exe INT NULL,
                  t2_time_to_exe_guaranteed INT NULL,
                  t2_storage VARCHAR(64) NULL,
                  t2_min_order INT NULL,
                  t2_probability INT NULL,
                  t2_markup DECIMAL(12,4) NULL,
                  t2_price_purchase DECIMAL(12,4) NULL,
                  t2_office_id INT NULL,
                  t2_storage_id INT NULL,
                  t2_product_json TEXT NULL,
                  t2_json_params TEXT NULL
                )
                """);
            var accepted = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAcceptPath, Form(("quote_id", "92")), signed);
            Assert.True(accepted.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("accepted", await ScalarAsync(connectionString, "SELECT status FROM shop_quote_requests WHERE id = 92"));
            Assert.Equal("15.0000", await ScalarAsync(connectionString, "SELECT price FROM shop_carts"));
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT t2_time_to_exe FROM shop_carts"));
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT t2_time_to_exe_guaranteed FROM shop_carts"));
            Assert.Equal("BOSCH", await ScalarAsync(connectionString, "SELECT t2_manufacturer FROM shop_carts"));
            Assert.Equal("0986", await ScalarAsync(connectionString, "SELECT t2_article FROM shop_carts"));
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT user_id FROM shop_carts"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT session_id FROM shop_carts"));
            Assert.Contains(Md5("BOSCH09860986Pad41533SHJ180481022"), await ScalarAsync(connectionString, "SELECT t2_product_json FROM shop_carts"), StringComparison.Ordinal);

            await ExecuteAsync(connectionString, "INSERT INTO shop_quote_requests (id, user_id, session_id, status, time_created, time_updated) VALUES (93, 7, 0, 'quoted', 1, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_quote_items (quote_id, product_type, product_object_json, count_need, quoted_price, quoted_time_to_exe) VALUES (93, 2, '" + acceptProduct + "', 1, 15, 3)");
            var duplicate = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.QuoteAcceptPath, Form(("quote_id", "93")), signed);
            Assert.Equal("already", duplicate.Json.RootElement.GetProperty("code").GetString());
            Assert.Equal(StorefrontPhpAjax.QuoteAlreadyInCart, duplicate.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("quoted", await ScalarAsync(connectionString, "SELECT status FROM shop_quote_requests WHERE id = 93"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_carts"));

            var missingTree = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.TreeBrunchPath + "?tree_list_id=3&parent_id=0", null, string.Empty);
            Assert.Equal(StorefrontPhpAjax.TreeListsMissing, missingTree.Body);
            Assert.DoesNotContain("doesn't exist", missingTree.Body, StringComparison.OrdinalIgnoreCase);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_tree_lists_items (
                  id INT NOT NULL PRIMARY KEY,
                  tree_list_id INT NOT NULL,
                  parent INT NOT NULL,
                  value VARCHAR(64) NOT NULL,
                  `count` INT NOT NULL,
                  `order` INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO shop_tree_lists_items (id, tree_list_id, parent, value, `count`, `order`) VALUES
                (1, 3, 0, 'Pads', 2, 2),
                (2, 3, 0, 'Rotors', 0, 1),
                (3, 3, 1, 'Front', 0, 1)
                """);
            var brunch = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.TreeBrunchPath + "?tree_list_id=3&parent_id=0&int_2=4", null, string.Empty);
            Assert.Equal(JsonValueKind.Null, brunch.Json.RootElement.GetProperty("int_1").ValueKind);
            Assert.Equal(4, brunch.Json.RootElement.GetProperty("int_2").GetInt32());
            Assert.Equal("2", brunch.Json.RootElement.GetProperty("data")[0].GetProperty("id").GetString());
            Assert.Equal("Rotors", brunch.Json.RootElement.GetProperty("data")[0].GetProperty("value").GetString());
            Assert.Equal("0", brunch.Json.RootElement.GetProperty("data")[0].GetProperty("webix_kids").GetString());
            Assert.Equal("Pads", brunch.Json.RootElement.GetProperty("data")[1].GetProperty("value").GetString());
            Assert.Equal("2", brunch.Json.RootElement.GetProperty("data")[1].GetProperty("webix_kids").GetString());
            var child = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.TreeBrunchPath + "?tree_list_id=3&parent_id=1", null, string.Empty);
            Assert.Equal("Front", child.Json.RootElement.GetProperty("data")[0].GetProperty("value").GetString());
            var loader = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.TreeAsyncPath + "?tree_list_id=3&parent_id=0", null, string.Empty);
            Assert.Equal("0", loader.Json.RootElement.GetProperty("parent").GetString());
            Assert.False(loader.Json.RootElement.GetProperty("data")[0].TryGetProperty("webix_kids", out _));
            Assert.Equal("2", loader.Json.RootElement.GetProperty("data")[1].GetProperty("webix_kids").GetString());

            var noCsrf = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.ToMarksPath, null, signed);
            Assert.Equal("Error! CSRF 1", noCsrf.Json.RootElement.GetProperty("message").GetString());
            var marks = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.ToMarksPath + "?csrf_guard_key=csrf-1", null, signed);
            Assert.Equal(StorefrontPhpAjax.ToMarksLocalFailure, marks.Body);
            Assert.DoesNotContain("search_tab_car", marks.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Toyota", marks.Body, StringComparison.Ordinal);

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

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
    }

    [Fact]
    public async Task CatalogueCountEndpoint_PricesWithTheSessionUsersGroupAndCityCookie_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CatalogueCount");
        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixtures, "cases.json"))).RootElement;
        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixtures, "golden.json"))).RootElement.GetProperty("results").EnumerateArray()
            .Where(r => r.TryGetProperty("output", out _))
            .ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("output").GetString()!);
        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await ExecuteAsync(admin, "CREATE DATABASE `" + database + "`");
        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        try
        {
            foreach (var sql in spec.GetProperty("schema").EnumerateArray().Concat(spec.GetProperty("base").EnumerateArray()))
            {
                await ExecuteAsync(connectionString, sql.GetString()!);
            }

            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL DEFAULT 0, type INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (session, user_id) VALUES ('registered-token', 6), ('bound-token', 5), ('guest-token', 0)");

            await using var host = await StartAsync(connectionString);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var request = "{\"category_id\":10,\"product_block_type\":1,\"properties_list\":[{\"property_id\":\"price\",\"property_type_id\":0,\"min_value\":0,\"max_value\":3000,\"min_need\":100,\"max_need\":200}]}";
            Assert.Equal(golden["price_two_offices_union_dedup_guest"], (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", request)), "my_city=1")).Body);
            Assert.Equal(golden["price_two_offices_union_dedup_guest"], (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", request)), "my_city=1; session=guest-token")).Body);
            Assert.Equal(golden["price_registered_group_markup"], (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", request)), "my_city=1; session=registered-token")).Body);
            Assert.Equal(golden["price_group_without_markup_rows_uses_raw_price"], (await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CatalogueCountPath, Form(("propucts_request", request)), "my_city=1; session=bound-token")).Body);
            Assert.NotEqual(golden["price_two_offices_union_dedup_guest"], golden["price_registered_group_markup"]);
        }
        finally
        {
            await ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
    }

    private static string Md5(string raw)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

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
