using System.Globalization;
using System.Net;
using System.Net.Sockets;
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

public sealed class StorefrontPhpCpLocalAjaxTests
{
    [Fact]
    public async Task CpLocalAjax_OnThrowawayDatabase_ThenDropped()
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

            var previewCsrf = await SendAsync(client, StorefrontPhpAjax.CpPricePreviewPath, Form(("price_id", "1")), staff);
            Assert.Contains("CSRF 1", previewCsrf.Body, StringComparison.Ordinal);
            await ExecuteAsync(connectionString, "CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL, csrf_guard_key VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES (15, 'admin-token', 9, 1, 'admin-csrf'), (16, 'zero-token', 0, 1, 'admin-csrf')");

            var previewDenied = await SendAsync(client, StorefrontPhpAjax.CpPricePreviewPath, Form(("csrf_guard_key", "admin-csrf"), ("price_id", "1")), "admin_session=zero-token; admin_u_id=0");
            Assert.Equal(501, previewDenied.Json.RootElement.GetProperty("code").GetInt32());
            Assert.Equal("Forbidden", previewDenied.Json.RootElement.GetProperty("message").GetString());

            var previewMissing = await SendAsync(client, StorefrontPhpAjax.CpPricePreviewPath, Form(("csrf_guard_key", "admin-csrf"), ("price_id", "1")), staff);
            Assert.Equal(StorefrontPhpAjax.PriceRowsMissing, previewMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("doesn't exist", previewMissing.Body, StringComparison.OrdinalIgnoreCase);

            var diagnosticsMissing = await SendAsync(client, StorefrontPhpAjax.CpPriceDiagnosticsPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal(StorefrontPhpAjax.PriceListsMissing, diagnosticsMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_price_upload_history'"));

            var brandsMissing = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"list_brands\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.PriceRowsMissing, brandsMissing.Json.RootElement.GetProperty("message").GetString());
            var createMissing = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"create_prices\",\"profile_group_ids\":[1]}")), staff);
            Assert.Equal(StorefrontPhpAjax.PriceRowsMissing, createMissing.Json.RootElement.GetProperty("message").GetString());

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
            var emptyPreview = await SendAsync(client, StorefrontPhpAjax.CpPricePreviewPath, Form(("csrf_guard_key", "admin-csrf"), ("price_id", "1")), staff);
            Assert.Contains("3689", emptyPreview.Body, StringComparison.Ordinal);
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices_data (price_id, manufacturer, article, article_show, name, exist, price, time_to_exe, storage, min_order) VALUES (1, 'BOSCH', '0986', '0986', 'Pads', 4, 12.50, 1, 'Own', 1)");
            var preview = await SendAsync(client, StorefrontPhpAjax.CpPricePreviewPath, Form(("csrf_guard_key", "admin-csrf"), ("price_id", "1")), staff);
            Assert.Contains("3690", preview.Body, StringComparison.Ordinal);
            Assert.Contains("BOSCH", preview.Body, StringComparison.Ordinal);
            Assert.Contains("0986", preview.Body, StringComparison.Ordinal);

            var sessionMissing = await SendAsync(client, StorefrontPhpAjax.CpCompletePriceSessionPath + "?key=local-tech&price_id=1", null, string.Empty);
            Assert.Equal(0, sessionMissing.Json.RootElement.GetProperty("result").GetInt32());
            Assert.Equal(StorefrontPhpAjax.PriceListsMissing, sessionMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_prices (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, load_mode INT NOT NULL DEFAULT 1, last_updated INT NOT NULL DEFAULT 0, records_count INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices (id, name, load_mode) VALUES (1, 'Bosch list', 1)");
            var completed = await SendAsync(client, StorefrontPhpAjax.CpCompletePriceSessionPath + "?key=local-tech&price_id=1", null, string.Empty);
            Assert.Equal(1, completed.Json.RootElement.GetProperty("result").GetInt32());
            Assert.NotEqual("0", await ScalarAsync(connectionString, "SELECT last_updated FROM shop_docpart_prices WHERE id = 1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT records_count FROM shop_docpart_prices WHERE id = 1"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_prices_data"));

            var snapshot = await SendAsync(client, StorefrontPhpAjax.CpPriceDiagnosticsPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal(1, snapshot.Json.RootElement.GetProperty("snapshot").GetProperty("price_lists_total").GetInt32());
            var health = await SendAsync(client, StorefrontPhpAjax.CpPriceDiagnosticsPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "health")), staff);
            Assert.False(health.Json.RootElement.GetProperty("health").GetProperty("all_ok").GetBoolean());
            Assert.Contains(StorefrontPhpAjax.CurlUnavailable, health.Body, StringComparison.Ordinal);

            var badSend = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "not-json")), staff);
            Assert.Equal("bad_request", badSend.Json.RootElement.GetProperty("message").GetString());
            var brands = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"list_brands\"}")), staff);
            Assert.Equal("BOSCH", brands.Json.RootElement.GetProperty("brands")[0].GetProperty("brand").GetString());
            var mapMissing = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"check_office_storages_map\",\"offices\":\"3\",\"arr_storages\":[6]}")), staff);
            Assert.Equal("Office storage markups are not in this database.", mapMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_offices_storages_map (id INT NOT NULL PRIMARY KEY AUTO_INCREMENT, office_id INT NOT NULL, storage_id INT NOT NULL, group_id INT NOT NULL, min_point INT NOT NULL, max_point INT NOT NULL, markup INT NOT NULL, additional_time INT NOT NULL)");
            var warehousesMissing = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"check_office_storages_map\",\"offices\":\"3\",\"arr_storages\":[6]}")), staff);
            Assert.Equal(StorefrontPhpAjax.WarehousesMissing, warehousesMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, name) VALUES (6, 'Sharjah')");
            var unlinked = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"check_office_storages_map\",\"offices\":\"3\",\"arr_storages\":[6]}")), staff);
            Assert.Equal("Sharjah", unlinked.Json.RootElement.GetProperty("message").GetString());
            var linked = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"ensure_office_storage_links\",\"offices\":\"3\",\"arr_storages\":[6],\"group_ids\":[2]}")), staff);
            Assert.Equal(1, linked.Json.RootElement.GetProperty("linked").GetInt32());
            var linkedAgain = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"ensure_office_storage_links\",\"offices\":\"3\",\"arr_storages\":[6],\"group_ids\":[2]}")), staff);
            Assert.Equal(0, linkedAgain.Json.RootElement.GetProperty("linked").GetInt32());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_offices_storages_map"));
            var sent = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"send_prices\"}")), staff);
            Assert.True(sent.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(0, sent.Json.RootElement.GetProperty("sent").GetInt32());
            var noProfile = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"create_prices\"}")), staff);
            Assert.Equal(StorefrontPhpAjax.NoMarkupProfile, noProfile.Json.RootElement.GetProperty("message").GetString());
            var profileStillLocal = await SendAsync(client, StorefrontPhpAjax.CpPricesSendPath, Form(("csrf_guard_key", "admin-csrf"), ("request_object", "{\"action\":\"create_prices\",\"profile_group_ids\":[1]}")), staff);
            Assert.Equal(StorefrontPhpAjax.NoMarkupProfile, profileStillLocal.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_prices_data"));

            var paymentsCsrf = await SendAsync(client, StorefrontPhpAjax.CpPaymentsPath, Form(("action", "activate"), ("handler", "cash")), staff);
            Assert.Contains("CSRF 1", paymentsCsrf.Body, StringComparison.Ordinal);
            var paymentsGuest = await SendAsync(client, StorefrontPhpAjax.CpPaymentsEndpointPath, Form(("action", "activate"), ("handler", "cash")), string.Empty);
            Assert.Equal("Access denied", paymentsGuest.Json.RootElement.GetProperty("message").GetString());
            var paymentsMissing = await SendAsync(client, StorefrontPhpAjax.CpPaymentsPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "activate"), ("handler", "cash")), staff);
            Assert.Equal(StorefrontPhpAjax.PaymentSystemsMissing, paymentsMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_payment_systems (id INT NOT NULL PRIMARY KEY, handler VARCHAR(64) NOT NULL, active INT NOT NULL DEFAULT 0, parameters_values TEXT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_payment_systems (id, handler, active) VALUES (1, 'cash', 0), (2, 'card', 1)");
            var activated = await SendAsync(client, StorefrontPhpAjax.CpPaymentsPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "activate"), ("handler", "cash")), staff);
            Assert.Equal("Activated: Cash", activated.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(active AS CHAR) FROM shop_payment_systems WHERE handler = 'cash'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT CAST(active AS CHAR) FROM shop_payment_systems WHERE handler = 'card'"));
            var handlerRequired = await SendAsync(client, StorefrontPhpAjax.CpPaymentsPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "activate")), staff);
            Assert.Equal("Handler required", handlerRequired.Json.RootElement.GetProperty("message").GetString());
            var unknownGateway = await SendAsync(client, StorefrontPhpAjax.CpPaymentsPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "activate"), ("handler", "stripe")), staff);
            Assert.Equal("Gateway not found", unknownGateway.Json.RootElement.GetProperty("message").GetString());
            var badJson = await SendAsync(client, StorefrontPhpAjax.CpPaymentsEndpointPath, Form(("action", "save_config"), ("parameters_values", "not-json"), ("system_id", "2")), staff);
            Assert.Equal("Invalid parameters JSON", badJson.Json.RootElement.GetProperty("message").GetString());
            var disabled = await SendAsync(client, StorefrontPhpAjax.CpPaymentsEndpointPath, Form(("action", "save_config"), ("parameters_values", "{}"), ("system_id", "0")), staff);
            Assert.Equal("All payment gateways disabled", disabled.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_payment_systems WHERE active = 1"));
            var saved = await SendAsync(client, StorefrontPhpAjax.CpPaymentsEndpointPath, Form(("action", "save_config"), ("parameters_values", "{\"mode\":\"test\"}"), ("system_id", "2")), staff);
            Assert.Equal("Payment gateway saved and activated", saved.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("{\"mode\":\"test\"}", await ScalarAsync(connectionString, "SELECT parameters_values FROM shop_payment_systems WHERE id = 2"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(active AS CHAR) FROM shop_payment_systems WHERE id = 2"));
            var accounts = await SendAsync(client, StorefrontPhpAjax.CpPaymentsPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "seed_dummy")), staff);
            Assert.Equal(StorefrontPhpAjax.PaymentAccountsMissing, accounts.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_payment_accounts'"));
            var settlement = await SendAsync(client, StorefrontPhpAjax.CpPaymentsPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "mark_settlement")), staff);
            Assert.Equal(StorefrontPhpAjax.PaymentSettlementsMissing, settlement.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_payment_settlements'"));

            var channelsGuest = await SendAsync(client, StorefrontPhpAjax.CpChannelsPath, Form(("action", "seed_channels")), string.Empty);
            Assert.Equal("Access denied", channelsGuest.Json.RootElement.GetProperty("message").GetString());
            var channelsMissing = await SendAsync(client, StorefrontPhpAjax.CpChannelsPath, Form(("action", "toggle_channel"), ("channel_code", "amazon"), ("enabled", "0")), staff);
            Assert.Equal(StorefrontPhpAjax.MarketplaceChannelsMissing, channelsMissing.Json.RootElement.GetProperty("message").GetString());
            var seeded = await SendAsync(client, StorefrontPhpAjax.CpChannelsPath, Form(("action", "seed_channels")), staff);
            Assert.Equal(35, seeded.Json.RootElement.GetProperty("seeded").GetInt32());
            Assert.Equal("Amazon.ae", await ScalarAsync(connectionString, "SELECT name FROM epc_marketplace_channels WHERE code = 'amazon'"));
            var toggledChannel = await SendAsync(client, StorefrontPhpAjax.CpChannelsPath, Form(("action", "toggle_channel"), ("channel_code", "amazon"), ("enabled", "0")), staff);
            Assert.Equal(0, toggledChannel.Json.RootElement.GetProperty("active").GetInt32());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT CAST(active AS CHAR) FROM epc_marketplace_channels WHERE code = 'amazon'"));
            var sample = await SendAsync(client, StorefrontPhpAjax.CpChannelsPath, Form(("action", "seed_sample")), staff);
            Assert.True(sample.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_marketplace_sku_map"));
            var pushed = await SendAsync(client, StorefrontPhpAjax.CpChannelsPath, Form(("action", "sync_inventory"), ("channel", "amazon")), staff);
            Assert.Equal("Pushed 1 SKUs to amazon", pushed.Json.RootElement.GetProperty("message").GetString());
            var orderId = await ScalarAsync(connectionString, "SELECT id FROM epc_marketplace_orders WHERE external_order_id = 'AMZ-402-8819201'");
            var imported = await SendAsync(client, StorefrontPhpAjax.CpChannelsPath, Form(("action", "import_order"), ("marketplace_order_id", orderId)), staff);
            Assert.Equal(0, imported.Json.RootElement.GetProperty("order_id").GetInt32());
            Assert.Equal("imported", await ScalarAsync(connectionString, "SELECT status FROM epc_marketplace_orders WHERE external_order_id = 'AMZ-402-8819201'"));
            Assert.Equal(string.Empty, await ScalarAsync(connectionString, "SELECT IFNULL(CAST(shop_order_id AS CHAR), '') FROM epc_marketplace_orders WHERE external_order_id = 'AMZ-402-8819201'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_orders'"));
            var unknownChannel = await SendAsync(client, StorefrontPhpAjax.CpChannelsPath, Form(("action", "nope")), staff);
            Assert.Equal("Unknown action", unknownChannel.Json.RootElement.GetProperty("message").GetString());

            var carriers = await SendAsync(client, StorefrontPhpAjax.CpLogisticsPath, Form(("action", "seed_carriers")), staff);
            Assert.Contains("Seeded 20", carriers.Body, StringComparison.Ordinal);
            Assert.Equal("20", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_carrier_accounts"));
            var sampleShipment = await SendAsync(client, StorefrontPhpAjax.CpLogisticsPath, Form(("action", "seed_sample")), staff);
            Assert.Equal(StorefrontPhpAjax.OrdersMissing, sampleShipment.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_orders'"));
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders (id INT NOT NULL PRIMARY KEY, successfully_created INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders (id, successfully_created) VALUES (41, 1)");
            var loaded = await SendAsync(client, StorefrontPhpAjax.CpLogisticsPath, Form(("action", "seed_sample")), staff);
            Assert.True(loaded.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("JD014600012345678901", await ScalarAsync(connectionString, "SELECT tracking_number FROM epc_carrier_shipments WHERE order_id = 41"));
            Assert.Contains("57.75", await ScalarAsync(connectionString, "SELECT CAST(cost AS CHAR) FROM epc_carrier_shipments WHERE order_id = 41"), StringComparison.Ordinal);
            var toggledCarrier = await SendAsync(client, StorefrontPhpAjax.CpLogisticsPath, Form(("action", "toggle_carrier"), ("carrier_code", "dhl")), staff);
            Assert.Equal(0, toggledCarrier.Json.RootElement.GetProperty("active").GetInt32());
            var labeled = await SendAsync(client, StorefrontPhpAjax.CpLogisticsPath, Form(("action", "create_shipment"), ("order_id", "41"), ("carrier_code", "dhl"), ("weight_kg", "1.5")), staff);
            Assert.StartsWith("Label created:", labeled.Json.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_carrier_shipments"));
            Assert.Contains("dhl.com", await ScalarAsync(connectionString, "SELECT label_url FROM epc_carrier_shipments WHERE label_url <> ''"), StringComparison.Ordinal);
            var unknownCarrier = await SendAsync(client, StorefrontPhpAjax.CpLogisticsPath, Form(("action", "create_shipment"), ("order_id", "41"), ("carrier_code", "not_a_carrier")), staff);
            Assert.Equal("Unknown carrier", unknownCarrier.Json.RootElement.GetProperty("message").GetString());
            var missingOrder = await SendAsync(client, StorefrontPhpAjax.CpLogisticsPath, Form(("action", "create_shipment"), ("order_id", "99"), ("carrier_code", "dhl")), staff);
            Assert.Equal("Order not found", missingOrder.Json.RootElement.GetProperty("message").GetString());

            var photosDenied = await SendAsync(client, StorefrontPhpAjax.CpAccessoryPhotosPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "list"), ("listing_id", "4")), "admin_session=nope; admin_u_id=9");
            Assert.Equal("Unauthorized", photosDenied.Json.RootElement.GetProperty("error").GetString());
            var photosCsrf = await SendAsync(client, StorefrontPhpAjax.CpAccessoryPhotosPath, Form(("csrf_guard_key", "wrong"), ("action", "list"), ("listing_id", "4")), staff);
            Assert.Equal("CSRF mismatch", photosCsrf.Json.RootElement.GetProperty("error").GetString());
            var photosRequired = await SendAsync(client, StorefrontPhpAjax.CpAccessoryPhotosPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "list")), staff);
            Assert.Equal("listing_id required", photosRequired.Json.RootElement.GetProperty("error").GetString());
            var photosEmpty = await SendAsync(client, StorefrontPhpAjax.CpAccessoryPhotosPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "list"), ("listing_id", "4")), staff);
            Assert.Equal(0, photosEmpty.Json.RootElement.GetProperty("photos").GetArrayLength());
            await ExecuteAsync(connectionString, "INSERT INTO epc_acc_photos (listing_id, file_name, sort_order, is_primary, created_at) VALUES (4, 'pad.jpg', 1, 0, 1), (4, 'rotor.jpg', 2, 0, 1)");
            var photos = await SendAsync(client, StorefrontPhpAjax.CpAccessoryPhotosPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "list"), ("listing_id", "4")), staff);
            Assert.Equal("pad.jpg", photos.Json.RootElement.GetProperty("photos")[0].GetProperty("file_name").GetString());
            var photoId = await ScalarAsync(connectionString, "SELECT id FROM epc_acc_photos WHERE file_name = 'rotor.jpg'");
            var primary = await SendAsync(client, StorefrontPhpAjax.CpAccessoryPhotosPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "set_primary"), ("listing_id", "4"), ("photo_id", photoId)), staff);
            Assert.True(primary.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(is_primary AS CHAR) FROM epc_acc_photos WHERE file_name = 'rotor.jpg'"));
            var deleted = await SendAsync(client, StorefrontPhpAjax.CpAccessoryPhotosPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "delete"), ("listing_id", "4"), ("photo_id", photoId)), staff);
            Assert.Equal(1, deleted.Json.RootElement.GetProperty("photos").GetArrayLength());
            var noFile = await SendAsync(client, StorefrontPhpAjax.CpAccessoryPhotosPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "upload"), ("listing_id", "4")), staff);
            Assert.Equal("No file", noFile.Json.RootElement.GetProperty("error").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_acc_listings'"));

            var historyEmpty = await SendAsync(client, StorefrontPhpAjax.CpPriceHistoryPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "list")), staff);
            Assert.Contains("No upload history yet", historyEmpty.Body, StringComparison.Ordinal);
            await ExecuteAsync(connectionString, "INSERT INTO epc_price_upload_history (price_id, original_filename, upload_source, rows_imported, created_at) VALUES (1, 'pads.csv', 'manual', 1, NOW())");
            var history = await SendAsync(client, StorefrontPhpAjax.CpPriceHistoryPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "list"), ("price_id", "1")), staff);
            Assert.Contains("pads.csv", history.Body, StringComparison.Ordinal);
            var exported = await SendAsync(client, StorefrontPhpAjax.CpPriceHistoryPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "export_db"), ("price_id", "1")), staff);
            Assert.Contains("0986", exported.Body, StringComparison.Ordinal);
            var exportRequired = await SendAsync(client, StorefrontPhpAjax.CpPriceHistoryPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "export_db")), staff);
            Assert.Equal("price_id required", exportRequired.Json.RootElement.GetProperty("message").GetString());
            var download = await SendAsync(client, StorefrontPhpAjax.CpPriceHistoryPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "download"), ("price_id", "1")), staff);
            Assert.Contains("Upload file not available", download.Body, StringComparison.Ordinal);

            var demand = await SendAsync(client, StorefrontPhpAjax.CpDemandCsvPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "stats")), staff);
            Assert.Equal(0, demand.Json.RootElement.GetProperty("stats").GetProperty("total_tags").GetInt32());
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_demand_country"));
            await ExecuteAsync(connectionString, "INSERT INTO epc_article_demand (manufacturer, article_norm, country_code) VALUES ('BOSCH', '0986', 'SDN')");
            var demandAfter = await SendAsync(client, StorefrontPhpAjax.CpDemandCsvPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "stats")), staff);
            Assert.Equal(1, demandAfter.Json.RootElement.GetProperty("stats").GetProperty("total_tags").GetInt32());
            var sudan = await SendAsync(client, StorefrontPhpAjax.CpDemandCsvPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "country_parts"), ("country", "SDN")), staff);
            Assert.Equal("BOSCH", sudan.Json.RootElement.GetProperty("parts")[0].GetProperty("brand").GetString());
            Assert.Equal("0986", sudan.Json.RootElement.GetProperty("parts")[0].GetProperty("article").GetString());
            var unknownCountry = await SendAsync(client, StorefrontPhpAjax.CpDemandCsvPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "country_parts"), ("country", "ZZZ")), staff);
            Assert.Equal("Unknown country code", unknownCountry.Json.RootElement.GetProperty("message").GetString());
            var uae = await SendAsync(client, StorefrontPhpAjax.CpDemandCsvPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "country_parts"), ("country", "ARE")), staff);
            Assert.Equal("ARE is UAE stock pool — not a demand market", uae.Json.RootElement.GetProperty("message").GetString());
            var missingFile = await SendAsync(client, StorefrontPhpAjax.CpDemandCsvPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "preview")), staff);
            Assert.Equal("Missing file path", missingFile.Json.RootElement.GetProperty("message").GetString());
            var invalidFile = await SendAsync(client, StorefrontPhpAjax.CpDemandCsvPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "import"), ("file_full_path", "/tmp/nope.csv")), staff);
            Assert.Equal("Invalid file path", invalidFile.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_article_demand"));

            var unregistered = await SendAsync(client, StorefrontPhpAjax.CpPartsAgentPath, Form(("action", "list")), staff);
            Assert.Equal(StorefrontPhpAjax.PartsAgentUnregistered, unregistered.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE content (id INT NOT NULL PRIMARY KEY, url VARCHAR(64) NOT NULL, is_frontend INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "INSERT INTO content (id, url, is_frontend) VALUES (8, 'shop/parts_agent_chats', 0)");
            var accessMissing = await SendAsync(client, StorefrontPhpAjax.CpPartsAgentPath, Form(("action", "list")), staff);
            Assert.Equal(StorefrontPhpAjax.ContentAccessMissing, accessMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE content_access (id INT NOT NULL PRIMARY KEY, content_id INT NOT NULL, group_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE users_groups_bind (id INT NOT NULL PRIMARY KEY, user_id INT NOT NULL, group_id INT NOT NULL)");
            var deniedAgent = await SendAsync(client, StorefrontPhpAjax.CpPartsAgentPath, Form(("action", "list")), staff);
            Assert.Equal("2388", deniedAgent.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "INSERT INTO content_access (id, content_id, group_id) VALUES (1, 8, 3)");
            await ExecuteAsync(connectionString, "INSERT INTO users_groups_bind (id, user_id, group_id) VALUES (1, 9, 3)");
            var agentCsrf = await SendAsync(client, StorefrontPhpAjax.CpPartsAgentPath, Form(("action", "list")), staff);
            Assert.Contains("CSRF 1", agentCsrf.Body, StringComparison.Ordinal);
            var agentMissing = await SendAsync(client, StorefrontPhpAjax.CpPartsAgentPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "list")), staff);
            Assert.Equal(StorefrontPhpAjax.PartsAgentMissing, agentMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_parts_agent_session'"));
            await ExecuteAsync(connectionString, "CREATE TABLE epc_parts_agent_session (session_id VARCHAR(64) NOT NULL PRIMARY KEY, message_count INT NOT NULL, last_user_text VARCHAR(255) NOT NULL, updated_at INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO epc_parts_agent_session (session_id, message_count, last_user_text, updated_at) VALUES ('s1', 2, 'pads', 1)");
            var agentList = await SendAsync(client, StorefrontPhpAjax.CpPartsAgentPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "list")), staff);
            Assert.Equal(1, agentList.Json.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(0, agentList.Json.RootElement.GetProperty("auto_synced").GetInt32());
            Assert.Equal("s1", agentList.Json.RootElement.GetProperty("sessions")[0].GetProperty("session_id").GetString());
            var agentMissingDetail = await SendAsync(client, StorefrontPhpAjax.CpPartsAgentPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "detail"), ("session_id", "missing")), staff);
            Assert.Equal("Session not found", agentMissingDetail.Json.RootElement.GetProperty("message").GetString());
            var agentUnknown = await SendAsync(client, StorefrontPhpAjax.CpPartsAgentPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "nope")), staff);
            Assert.Equal("Unknown action", agentUnknown.Json.RootElement.GetProperty("message").GetString());

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
