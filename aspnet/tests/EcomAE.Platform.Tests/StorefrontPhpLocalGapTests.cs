using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
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

public sealed class StorefrontPhpLocalGapTests
{
    [Fact]
    public async Task LocalGap_OnThrowawayDatabase_ThenDropped()
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
        WriteConfig(configRoot, false, false, false);
        try
        {
            await using var host = await StartAsync(connectionString, configRoot);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var staff = "admin_session=admin-token; admin_u_id=9";
            var customer = "session=user-token; u_id=7";

            var cross = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.CrossbaseStatusPath + "?sample=C110J", null, string.Empty);
            Assert.False(cross.Json.RootElement.GetProperty("connected").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.CrossbaseUnavailable, cross.Json.RootElement.GetProperty("message").GetString());
            Assert.False(cross.Json.RootElement.GetProperty("used_stale_cache").GetBoolean());
            Assert.Equal(0, cross.Json.RootElement.GetProperty("cp_cross_rows").GetInt32());
            Assert.Contains("epc-offline-resilience-warm.php", cross.Body, StringComparison.Ordinal);

            var missingFitment = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.FitmentJsPath, null, string.Empty);
            Assert.Equal(StorefrontPhpAjax.FitmentMissingArticle, missingFitment.Body);
            Assert.Contains("javascript", missingFitment.ContentType, StringComparison.OrdinalIgnoreCase);
            var fitment = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.FitmentJsPath + "?n=C110J", null, string.Empty);
            Assert.Equal(StorefrontPhpAjax.FitmentUnavailable, fitment.Body);

            var bootstrap = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.AiPartsExpertPath, Form(("action", "bootstrap")), string.Empty);
            var token = bootstrap.Json.RootElement.GetProperty("csrf").GetString();
            Assert.True(bootstrap.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(token));
            var unknownAi = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.AiPartsExpertPath, Form(("action", "nope")), string.Empty);
            Assert.Equal(HttpStatusCode.BadRequest, unknownAi.Status);
            Assert.Equal(StorefrontPhpAjax.AiUnknown, unknownAi.Json.RootElement.GetProperty("message").GetString());
            var badCsrf = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.AiPartsExpertPath, Form(("action", "search"), ("article", "C110J"), ("csrf", "nope")), string.Empty);
            Assert.Equal(HttpStatusCode.Forbidden, badCsrf.Status);
            Assert.Equal(StorefrontPhpAjax.AiCsrfInvalid, badCsrf.Json.RootElement.GetProperty("message").GetString());
            var shortArticle = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.AiPartsExpertPath, Form(("action", "search"), ("article", "AB"), ("csrf", token!)), string.Empty);
            Assert.Equal(StorefrontPhpAjax.AiShortArticle, shortArticle.Json.RootElement.GetProperty("message").GetString());
            var search = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.AiPartsExpertPath, Form(("action", "search"), ("article", "C110J"), ("csrf", token!)), string.Empty);
            Assert.True(search.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.False(search.Json.RootElement.GetProperty("sources").GetProperty("crossbase").GetBoolean());
            Assert.False(search.Json.RootElement.GetProperty("sources").GetProperty("umapi").GetBoolean());
            Assert.Contains(StorefrontPhpAjax.AiCrossUnavailable, search.Body, StringComparison.Ordinal);
            Assert.Contains(StorefrontPhpAjax.AiFitmentUnavailable, search.Body, StringComparison.Ordinal);
            Assert.Equal(0, search.Json.RootElement.GetProperty("local_stock").GetArrayLength());

            var agentBoot = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.PartsAgentPath + "?action=bootstrap", null, string.Empty);
            Assert.True(agentBoot.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Contains("/en/zapros-prodavczu", agentBoot.Body, StringComparison.Ordinal);
            var hello = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PartsAgentPath, Form(("action", "chat"), ("message", "hello")), string.Empty);
            Assert.Contains("Hello!", hello.Json.RootElement.GetProperty("reply").GetProperty("text").GetString(), StringComparison.Ordinal);
            var emptyChat = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PartsAgentPath, Form(("action", "chat"), ("message", "")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.AgentEmpty, emptyChat.Json.RootElement.GetProperty("reply").GetProperty("text").GetString());
            var vinChat = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PartsAgentPath, Form(("action", "chat"), ("message", "WVWZZZ3CZWE123456")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.AgentVinFailed, vinChat.Json.RootElement.GetProperty("reply").GetProperty("text").GetString());
            var missingSession = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PartsAgentPath, Form(("action", "history")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.AgentMissingSession, missingSession.Json.RootElement.GetProperty("message").GetString());
            var unknownAgent = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PartsAgentPath, Form(("action", "nope")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.AgentUnknown, unknownAgent.Json.RootElement.GetProperty("message").GetString());

            var laximo = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.LaximoProxyPath, null, string.Empty);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, laximo.Status);
            Assert.Equal(StorefrontPhpAjax.LaximoCredentials, laximo.Json.RootElement.GetProperty("error").GetString());
            var shortVin = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.LaximoProxyPath + "?action=find_vehicle&vin=ABC", null, string.Empty);
            Assert.Equal(HttpStatusCode.BadRequest, shortVin.Status);
            Assert.Equal(StorefrontPhpAjax.LaximoVinShort, shortVin.Json.RootElement.GetProperty("error").GetString());
            var sync = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.LaximoProxyPath, Form(("action", "sync")), string.Empty);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, sync.Status);
            Assert.Equal(StorefrontPhpAjax.LaximoSyncFailed, sync.Json.RootElement.GetProperty("error").GetString());
            var unknownLax = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.LaximoProxyPath + "?action=nope", null, string.Empty);
            Assert.Equal(HttpStatusCode.BadRequest, unknownLax.Status);
            Assert.Contains("Unknown action: nope", unknownLax.Body, StringComparison.Ordinal);
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_laximo_catalogs (
                  id INT NOT NULL PRIMARY KEY,
                  code VARCHAR(32) NOT NULL,
                  brand VARCHAR(64) NOT NULL,
                  name VARCHAR(64) NOT NULL,
                  updated_at INT NOT NULL
                )
                """);
            var fresh = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            await ExecuteAsync(connectionString, "INSERT INTO epc_laximo_catalogs (id, code, brand, name, updated_at) VALUES (1, 'TOY', 'Toyota', 'Toyota', " + fresh + "), (2, 'NIS', 'Nissan', 'Nissan', " + fresh + ")");
            var catalogs = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.LaximoProxyPath + "?action=catalogs", null, string.Empty);
            Assert.Equal(HttpStatusCode.OK, catalogs.Status);
            Assert.Equal("db", catalogs.Json.RootElement.GetProperty("source").GetString());
            Assert.Equal(2, catalogs.Json.RootElement.GetProperty("catalogs").GetArrayLength());

            var noSettings = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.PricesSettingsPath, null, string.Empty);
            Assert.Equal(StorefrontPhpAjax.PricesForbidden, noSettings.Json.RootElement.GetProperty("message").GetString());
            var settings = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PricesSettingsPath, Form(("tech_key", "local-tech")), string.Empty);
            Assert.True(settings.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("redacted", settings.Json.RootElement.GetProperty("secrets").GetString());
            Assert.Equal(JsonValueKind.Null, settings.Json.RootElement.GetProperty("db").GetProperty("password").ValueKind);
            Assert.Equal(JsonValueKind.Null, settings.Json.RootElement.GetProperty("prices_email").GetProperty("prices_email_password").ValueKind);
            Assert.DoesNotContain("hidden-db-secret", settings.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("hidden-mail-secret", settings.Body, StringComparison.Ordinal);
            Assert.Equal("127.0.0.1", settings.Json.RootElement.GetProperty("db").GetProperty("host").GetString());

            var badUpload = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PriceUploadPath, Form(("tech_key", "nope"), ("id", "4")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.PriceWrongKey, badUpload.Json.RootElement.GetProperty("data").GetString());
            var missingPrice = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PriceUploadPath, Form(("tech_key", "local-tech"), ("id", "0")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.PriceNotFound, missingPrice.Json.RootElement.GetProperty("data").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_prices (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL DEFAULT '')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices (id, name) VALUES (4, 'Local')");
            var noFile = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.PriceUploadPath, Form(("tech_key", "local-tech"), ("id", "4")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.PriceNoFile, noFile.Json.RootElement.GetProperty("data").GetString());
            var uploaded = await SendFileAsync(client, StorefrontPhpAjax.PriceUploadPath, "document", "price.csv", "a,b\n"u8.ToArray(), ("tech_key", "local-tech"), ("id", "4"));
            Assert.False(uploaded.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.PriceImportFailed, uploaded.Json.RootElement.GetProperty("data").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_docpart_prices_data'"));

            var missingKey = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.PriceLookupPath + "?brand=BOSCH&article=0986424590", null, string.Empty);
            Assert.Equal(HttpStatusCode.Unauthorized, missingKey.Status);
            Assert.Equal("missing_api_key", missingKey.Json.RootElement.GetProperty("error").GetProperty("code").GetString());
            Assert.Equal("1", missingKey.Headers.GetValues("X-ECOM-API-Client").First());
            var badPrefix = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.PriceLookupPath + "?brand=BOSCH&article=0986424590", null, string.Empty, "not-a-key");
            Assert.Equal("invalid_key_format", badPrefix.Json.RootElement.GetProperty("error").GetProperty("code").GetString());
            const string priceKey = "epc_pricepro_localtest";
            var unknownKey = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.PriceLookupPath + "?brand=BOSCH&article=0986424590", null, string.Empty, priceKey);
            Assert.Equal("invalid_api_key", unknownKey.Json.RootElement.GetProperty("error").GetProperty("code").GetString());
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(priceKey))).ToLowerInvariant();
            await ExecuteAsync(connectionString, "INSERT INTO epc_api_clients (client_key_hash, client_key_prefix, product, label, allowed_actions_json, daily_limit, calls_today, calls_reset_date, active) VALUES ('" + hash + "', 'epc_pricepro_local', 'price_pro', 'Local', '', 5, 0, CURDATE(), 1)");
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_prices_data (
                  id INT NOT NULL PRIMARY KEY,
                  manufacturer VARCHAR(64) NOT NULL,
                  article VARCHAR(64) NOT NULL,
                  article_show VARCHAR(64) NOT NULL DEFAULT '',
                  name VARCHAR(120) NOT NULL DEFAULT '',
                  price DECIMAL(12,2) NOT NULL,
                  exist INT NOT NULL DEFAULT 0,
                  storage VARCHAR(64) NOT NULL DEFAULT '',
                  time_to_exe VARCHAR(32) NOT NULL DEFAULT ''
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices_data (id, manufacturer, article, article_show, name, price, exist, storage, time_to_exe) VALUES (1, 'BOSCH', '0986424590', '0 986 424 590', 'Pad', 12.50, 4, 'DXB', '1')");
            var offer = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.PriceLookupPath + "?brand=BOSCH&article=0986%20424%20590", null, string.Empty, priceKey);
            Assert.Equal(HttpStatusCode.OK, offer.Status);
            Assert.True(offer.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("AED", offer.Json.RootElement.GetProperty("offers")[0].GetProperty("currency").GetString());
            Assert.Equal("DXB", offer.Json.RootElement.GetProperty("offers")[0].GetProperty("supplier").GetString());
            Assert.Equal(string.Empty, offer.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT calls_today FROM epc_api_clients WHERE label = 'Local'"));
            var stocked = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.AiPartsExpertPath, Form(("action", "search"), ("article", "0986424590"), ("csrf", token!)), string.Empty);
            Assert.Equal(1, stocked.Json.RootElement.GetProperty("local_stock").GetArrayLength());

            var licenseGet = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.LicenseActivatePath, null, string.Empty);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, licenseGet.Status);
            Assert.Equal("method_not_allowed", licenseGet.Json.RootElement.GetProperty("error").GetString());
            var badJson = await SendJsonAsync(client, StorefrontPhpAjax.LicenseActivatePath, "not-json");
            Assert.Equal(HttpStatusCode.BadRequest, badJson.Status);
            Assert.Equal("invalid_json", badJson.Json.RootElement.GetProperty("error").GetString());
            var healthMissing = await SendJsonAsync(client, StorefrontPhpAjax.OnPremHealthPath, """{"license_key":"LIC-2026-AB12-CD34"}""");
            Assert.Equal(HttpStatusCode.Forbidden, healthMissing.Status);
            Assert.Equal(StorefrontPhpAjax.OnPremLicensesMissing, healthMissing.Json.RootElement.GetProperty("error").GetString());
            var notFound = await SendJsonAsync(client, StorefrontPhpAjax.LicenseActivatePath, """{"license_key":"LIC-2026-AB12-CD34","fingerprint":"fp-one"}""");
            Assert.Equal("not_found", notFound.Json.RootElement.GetProperty("error").GetString());
            await ExecuteAsync(connectionString, "INSERT INTO epc_onprem_licenses (license_key, customer_name, tier, modules_json, users_max, status, issued_at, expires_at) VALUES ('LIC-2026-AB12-CD34', 'Local', 'standard', '[\"all\"]', 25, 'issued', 1, NULL)");
            var activated = await SendJsonAsync(client, StorefrontPhpAjax.LicenseActivatePath, """{"license_key":"LIC-2026-AB12-CD34","fingerprint":"fp-one","hostname":"box","ip":"127.0.0.1"}""");
            Assert.Equal(HttpStatusCode.BadRequest, activated.Status);
            Assert.Equal("signing_unavailable", activated.Json.RootElement.GetProperty("error").GetString());
            Assert.Equal(StorefrontPhpAjax.SigningUnavailable, activated.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("active", await ScalarAsync(connectionString, "SELECT status FROM epc_onprem_licenses WHERE license_key = 'LIC-2026-AB12-CD34'"));
            Assert.Equal("fp-one", await ScalarAsync(connectionString, "SELECT fingerprint FROM epc_onprem_licenses WHERE license_key = 'LIC-2026-AB12-CD34'"));
            var moved = await SendJsonAsync(client, StorefrontPhpAjax.LicenseActivatePath, """{"license_key":"LIC-2026-AB12-CD34","fingerprint":"fp-two"}""");
            Assert.Equal("already_activated", moved.Json.RootElement.GetProperty("error").GetString());
            Assert.Equal("fp-one", await ScalarAsync(connectionString, "SELECT fingerprint FROM epc_onprem_licenses WHERE license_key = 'LIC-2026-AB12-CD34'"));
            var health = await SendJsonAsync(client, StorefrontPhpAjax.OnPremHealthPath, """{"license_key":"LIC-2026-AB12-CD34","status":"ok","uptime":"3","disk_free_gb":1.5}""");
            Assert.True(health.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_onprem_health_log"));
            Assert.NotEqual("0", await ScalarAsync(connectionString, "SELECT IFNULL(last_seen_at, 0) FROM epc_onprem_licenses WHERE license_key = 'LIC-2026-AB12-CD34'"));

            var skuNoSessions = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "meta")), customer);
            Assert.Equal(StorefrontPhpAjax.AdminSessionsMissing, skuNoSessions.Json.RootElement.GetProperty("error").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE sessions (
                  id INT NOT NULL PRIMARY KEY,
                  session VARCHAR(64) NOT NULL,
                  user_id INT NOT NULL,
                  type INT NOT NULL DEFAULT 0,
                  csrf_guard_key VARCHAR(64) NOT NULL DEFAULT ''
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO sessions (id, session, user_id, type, csrf_guard_key) VALUES
                (12, 'user-token', 7, 0, 'csrf-1'),
                (15, 'admin-token', 9, 1, 'admin-csrf')
                """);
            var skuGuest = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "meta")), customer);
            Assert.Equal(StorefrontPhpAjax.SkuUnauthorized, skuGuest.Json.RootElement.GetProperty("error").GetString());
            var skuMismatch = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "meta"), ("csrf_guard_key", "nope")), staff);
            Assert.Equal(StorefrontPhpAjax.SkuCsrf, skuMismatch.Json.RootElement.GetProperty("error").GetString());
            var saved = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "save_profile"), ("csrf_guard_key", "admin-csrf"), ("brand", "Bosch"), ("article", "0986-424-590"), ("title", "Pad")), staff);
            Assert.True(saved.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("BOSCH", saved.Json.RootElement.GetProperty("payload").GetProperty("profile").GetProperty("brand").GetString());
            var profileId = saved.Json.RootElement.GetProperty("payload").GetProperty("profile").GetProperty("id").ToString();
            var listed = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "list"), ("csrf_guard_key", "admin-csrf"), ("q", "BOSCH")), staff);
            Assert.Equal(1, listed.Json.RootElement.GetProperty("items").GetArrayLength());
            var grouped = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "add_spec_group"), ("csrf_guard_key", "admin-csrf"), ("profile_id", profileId), ("name", "Technical")), staff);
            Assert.True(grouped.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_sku_spec_groups"));
            var noPhoto = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "upload_photo"), ("csrf_guard_key", "admin-csrf"), ("profile_id", profileId)), staff);
            Assert.Equal(StorefrontPhpAjax.SkuNoFile, noPhoto.Json.RootElement.GetProperty("error").GetString());
            var meta = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "meta"), ("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal("Product", meta.Json.RootElement.GetProperty("photo_types").GetProperty("product").GetString());
            var removed = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "delete_profile"), ("csrf_guard_key", "admin-csrf"), ("profile_id", profileId)), staff);
            Assert.True(removed.Json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_sku_profiles"));
            var skuUnknown = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.SkuMediaAdminPath, Form(("action", "nope"), ("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal(StorefrontPhpAjax.SkuUnknown, skuUnknown.Json.RootElement.GetProperty("error").GetString());

            var taxGuest = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.TaxExemptUploadPath, Form(("csrf_guard_key", "csrf-1")), string.Empty);
            Assert.Contains("CSRF", taxGuest.Body, StringComparison.Ordinal);
            var taxRetail = await SendFileAsync(client, StorefrontPhpAjax.TaxExemptUploadPath, "tax_exempt_cert", "cert.pdf", "%PDF-1.1"u8.ToArray(), ("csrf_guard_key", "csrf-1"));
            taxRetail = await SendFileAsync(client, StorefrontPhpAjax.TaxExemptUploadPath, "tax_exempt_cert", "cert.pdf", "%PDF-1.1"u8.ToArray(), customer, ("csrf_guard_key", "csrf-1"));
            Assert.Equal(StorefrontPhpAjax.WholesaleOnly, taxRetail.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE users_profiles (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  user_id INT NOT NULL,
                  data_key VARCHAR(64) NOT NULL,
                  data_value VARCHAR(255) NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO users_profiles (user_id, data_key, data_value) VALUES (7, 'epc_customer_type', 'retail')");
            var stillRetail = await SendFileAsync(client, StorefrontPhpAjax.TaxExemptUploadPath, "tax_exempt_cert", "cert.pdf", "%PDF-1.1"u8.ToArray(), customer, ("csrf_guard_key", "csrf-1"));
            Assert.Equal(StorefrontPhpAjax.WholesaleOnly, stillRetail.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "UPDATE users_profiles SET data_value = 'wholesale' WHERE data_key = 'epc_customer_type'");
            var badType = await SendFileAsync(client, StorefrontPhpAjax.TaxExemptUploadPath, "tax_exempt_cert", "cert.gif", "GIF89a"u8.ToArray(), customer, ("csrf_guard_key", "csrf-1"));
            Assert.Equal(StorefrontPhpAjax.TaxBadType, badType.Json.RootElement.GetProperty("message").GetString());
            var cert = await SendFileAsync(client, StorefrontPhpAjax.TaxExemptUploadPath, "tax_exempt_cert", "cert.pdf", "%PDF-1.1"u8.ToArray(), customer, ("csrf_guard_key", "csrf-1"));
            Assert.True(cert.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.TaxUploaded, cert.Json.RootElement.GetProperty("message").GetString());
            Assert.StartsWith("/content/files/tax_exempt/7/tax_exempt_", cert.Json.RootElement.GetProperty("path").GetString(), StringComparison.Ordinal);
            Assert.Equal("pending_review", await ScalarAsync(connectionString, "SELECT data_value FROM users_profiles WHERE user_id = 7 AND data_key = 'epc_tax_exempt_cert_status'"));
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM users_profiles WHERE user_id = 7 AND data_key = 'epc_tax_exempt_cert_path'"));

            var noMessages = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpUnreadMessagesPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal(StorefrontPhpAjax.OrderMessagesMissing, noMessages.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_messages (
                  id INT NOT NULL PRIMARY KEY,
                  order_id INT NOT NULL DEFAULT 0,
                  return_id INT NOT NULL DEFAULT 0,
                  `read` INT NOT NULL DEFAULT 0,
                  is_customer INT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_messages (id, order_id, return_id, `read`, is_customer) VALUES (1, 4, 0, 0, 1), (2, 5, 0, 0, 1), (3, 0, 9, 0, 1), (4, 6, 0, 1, 1)");
            var unread = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpUnreadMessagesPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal("2", unread.Json.RootElement.GetProperty("count").GetString());
            var unreadReturns = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.CpUnreadMessagesPath + "?returns=1&csrf_guard_key=admin-csrf", null, staff);
            Assert.Equal("1", unreadReturns.Json.RootElement.GetProperty("count").GetString());
            var noVins = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpVinInfoPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal(StorefrontPhpAjax.VinRequestsMissing, noVins.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE users_vin (id INT NOT NULL PRIMARY KEY, viewed INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "INSERT INTO users_vin (id, viewed) VALUES (1, 0), (2, 1)");
            var vins = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpVinInfoPath, Form(("csrf_guard_key", "admin-csrf")), staff);
            Assert.Equal("1", vins.Json.RootElement.GetProperty("count").GetString());

            var multilangDenied = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpMultilangPath, Form(("key", "nope")), staff);
            Assert.Equal("No access", multilangDenied.Body);
            var multilang = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpMultilangPath, Form(("key", "local-tech")), staff);
            Assert.Equal("OFF", multilang.Body);
            WriteConfig(configRoot, true, false, false);
            var multilangOn = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpMultilangPath, Form(("key", "local-tech")), staff);
            Assert.Equal("ON", multilangOn.Body);
            WriteConfig(configRoot, false, false, false);

            var langMissing = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpStringTranslationPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("lang_code", "en")), staff);
            Assert.Equal(StorefrontPhpAjax.LangEditorMissing, langMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE content (id INT NOT NULL PRIMARY KEY, url VARCHAR(64) NOT NULL, is_frontend INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE content_access (id INT NOT NULL PRIMARY KEY, content_id INT NOT NULL, group_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE users_groups_bind (id INT NOT NULL PRIMARY KEY, user_id INT NOT NULL, group_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO content (id, url, is_frontend) VALUES (11, 'lang/editor', 0)");
            await ExecuteAsync(connectionString, "INSERT INTO content_access (id, content_id, group_id) VALUES (1, 11, 3)");
            await ExecuteAsync(connectionString, "INSERT INTO users_groups_bind (id, user_id, group_id) VALUES (1, 9, 3)");
            await ExecuteAsync(connectionString, "CREATE TABLE `groups` (id INT NOT NULL PRIMARY KEY, parent INT NOT NULL DEFAULT 0, `count` INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "CREATE TABLE lang_languages (id INT NOT NULL PRIMARY KEY, lang_code VARCHAR(8) NOT NULL, active INT NOT NULL DEFAULT 1, is_default INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "CREATE TABLE lang_text_strings (id INT NOT NULL PRIMARY KEY, str_key VARCHAR(64) NOT NULL, description VARCHAR(255) NOT NULL DEFAULT '', `same` VARCHAR(16) NULL, is_error INT NOT NULL DEFAULT 0, is_custom INT NOT NULL DEFAULT 0)");
            await ExecuteAsync(connectionString, "CREATE TABLE lang_text_strings_translation (id INT NOT NULL PRIMARY KEY, str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value VARCHAR(255) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO lang_languages (id, lang_code) VALUES (1, 'en')");
            await ExecuteAsync(connectionString, "INSERT INTO lang_text_strings (id, str_key, description) VALUES (1, 'hello', 'Greeting')");
            await ExecuteAsync(connectionString, "INSERT INTO lang_text_strings_translation (id, str_key, lang_code, value) VALUES (1, 'hello', 'en', 'Hello')");
            var translated = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpStringTranslationPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("lang_code", "en")), staff);
            Assert.Equal("Hello", translated.Json.RootElement.GetProperty("value").GetString());
            var info = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpStringInfoPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello")), staff);
            Assert.Equal("Greeting", info.Json.RootElement.GetProperty("str_info").GetProperty("description").GetString());
            Assert.Equal("1", info.Json.RootElement.GetProperty("str_info").GetProperty("has_en").ToString());
            var flagged = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetStringErrorPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("is_error", "1")), staff);
            Assert.True(flagged.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT is_error FROM lang_text_strings WHERE str_key = 'hello'"));
            var custom = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetStringCustomPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("is_custom", "1")), staff);
            Assert.Equal(1, custom.Json.RootElement.GetProperty("is_custom").GetInt32());
            var same = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetStringSamePath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("same", "en")), staff);
            Assert.Equal("en", same.Json.RootElement.GetProperty("same").GetString());
            Assert.Equal("en", await ScalarAsync(connectionString, "SELECT `same` FROM lang_text_strings WHERE str_key = 'hello'"));
            var cleared = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetStringSamePath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("same", "no")), staff);
            Assert.Equal(JsonValueKind.Null, cleared.Json.RootElement.GetProperty("same").ValueKind);
            Assert.Equal(string.Empty, await ScalarAsync(connectionString, "SELECT IFNULL(`same`, '') FROM lang_text_strings WHERE str_key = 'hello'"));
            WriteConfig(configRoot, false, true, false);
            var restricted = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpSetStringErrorPath, Form(("csrf_guard_key", "admin-csrf"), ("str_key", "hello"), ("is_error", "0")), staff);
            Assert.Equal(StorefrontPhpAjax.LangRestricted, restricted.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT is_error FROM lang_text_strings WHERE str_key = 'hello'"));
            WriteConfig(configRoot, false, false, false);

            await ExecuteAsync(connectionString, "UPDATE lang_languages SET is_default = 1 WHERE lang_code = 'en'");
            await ExecuteAsync(connectionString, "INSERT INTO lang_languages (id, lang_code) VALUES (2, 'ar')");
            await ExecuteAsync(connectionString, "ALTER TABLE lang_text_strings ADD used_found INT NOT NULL DEFAULT 0");
            await ExecuteAsync(connectionString, "INSERT INTO lang_text_strings (id, str_key, description) VALUES (2, 'bye', '<b>Bye</b>'), (3, 'cart', 'Cart')");
            await ExecuteAsync(connectionString, "INSERT INTO lang_text_strings_translation (id, str_key, lang_code, value) VALUES (2, 'cart', 'en', 'Cart'), (3, 'cart', 'ar', 'سلة')");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_currencies (id INT NOT NULL PRIMARY KEY, caption_short VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_currencies (id, caption_short) VALUES (1, 'cart')");
            async Task<Sent> Strings(string filter, string limitCount = "10", string sortField = "str_key", string itemsNew = "[]", string left = "", string right = "", string cookie = "")
                => await SendAsync(
                    client,
                    HttpMethod.Post,
                    StorefrontPhpAjax.CpTextStringsPath,
                    Form(
                        ("csrf_guard_key", "admin-csrf"),
                        ("items_filter", filter),
                        ("items_sort", "{\"field\":\"" + sortField + "\",\"asc_desc\":\"asc\"}"),
                        ("limit_from", "0"),
                        ("limit_count", limitCount),
                        ("items_new", itemsNew),
                        ("left_lang", left),
                        ("right_lang", right)),
                    cookie.Length > 0 ? cookie : staff);
            static string[] Keys(Sent sent)
                => sent.Json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("str_key").GetString()!).ToArray();

            var stringsGuest = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpTextStringsPath, Form(("csrf_guard_key", "admin-csrf")), string.Empty);
            Assert.False(stringsGuest.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.StartsWith("Error! CSRF", stringsGuest.Json.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            var allStrings = await Strings("{}");
            Assert.True(allStrings.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.False(allStrings.Json.RootElement.TryGetProperty("SQL", out _));
            Assert.Equal(["bye", "cart", "hello"], Keys(allStrings));
            var bye = allStrings.Json.RootElement.GetProperty("items")[0];
            Assert.Equal("&lt;b&gt;Bye&lt;/b&gt;", bye.GetProperty("description").GetString());
            Assert.Equal("0", bye.GetProperty("has_en").ToString());
            Assert.Equal("1", allStrings.Json.RootElement.GetProperty("items")[1].GetProperty("has_ar").ToString());
            Assert.Equal("Hello", allStrings.Json.RootElement.GetProperty("items")[2].GetProperty("current_lang_translation").GetString());
            Assert.Equal(["bye"], Keys(await Strings("{\"translation_progress\":3}")));
            Assert.Equal(["cart"], Keys(await Strings("{\"translation_progress\":1}")));
            Assert.Equal(["hello"], Keys(await Strings("{\"translation_progress\":2}")));
            Assert.Equal(["cart"], Keys(await Strings("{\"translation\":\"سل\",\"translation_like\":1}")));
            Assert.Equal(["hello"], Keys(await Strings("{\"no_translation_in\":\"ar\",\"has_translation_in\":\"en\"}")));
            Assert.Equal(["hello"], Keys(await Strings("{\"is_error\":1,\"is_custom\":\"1\"}")));
            Assert.Equal(["bye", "cart"], Keys(await Strings("{\"is_error\":2}")));
            Assert.Equal(["cart", "hello"], Keys(await Strings("{}", itemsNew: "[\"bye\"]")));
            Assert.Equal(["cart"], Keys(await Strings("{\"table\":\"shop_currencies\"}")));
            var twoColumns = await Strings("{\"str_key\":\"cart\"}", left: "en", right: "ar");
            Assert.Equal("سلة", twoColumns.Json.RootElement.GetProperty("items")[0].GetProperty("right_lang_translation").GetString());
            Assert.Equal(string.Empty, (await Strings("{\"table\":\"users\"}")).Body);
            Assert.Equal(string.Empty, (await Strings("{\"table\":\"shop_currencies\",\"column\":\"id\"}")).Body);
            Assert.Equal(string.Empty, (await Strings("{}", limitCount: "0")).Body);
            Assert.Equal(string.Empty, (await Strings("{}", limitCount: "5001")).Body);
            Assert.Equal(string.Empty, (await Strings("{}", sortField: "id`; DROP TABLE x; --")).Body);
            Assert.Equal(["bye"], Keys(await Strings("{}", limitCount: "1")));
            WriteConfig(configRoot, true, false, false);
            var arabic = await Strings("{\"str_key\":\"cart\"}", cookie: staff + "; lang_cp=ar");
            Assert.Equal("سلة", arabic.Json.RootElement.GetProperty("items")[0].GetProperty("current_lang_translation").GetString());
            WriteConfig(configRoot, false, true, false);
            Assert.Equal(["bye", "cart"], Keys(await Strings("{}")));
            WriteConfig(configRoot, false, false, false);

            var templatesMissing = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpCategoryTemplatesPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "get_all")), staff);
            Assert.Equal(StorefrontPhpAjax.TemplatesMissing, templatesMissing.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_catalogue_categories_templates (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  caption VARCHAR(120) NOT NULL,
                  category_object TEXT NULL,
                  image LONGBLOB NULL,
                  image_name VARCHAR(120) NULL
                )
                """);
            var created = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpCategoryTemplatesPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "create"), ("caption", "Pads"), ("category_object", "{\"id\":1}")), staff);
            Assert.True(created.Json.RootElement.GetProperty("status").GetBoolean());
            var templateId = created.Json.RootElement.GetProperty("template_id").GetString();
            var all = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpCategoryTemplatesPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "get_all")), staff);
            Assert.Equal("Pads", all.Json.RootElement.GetProperty("templates")[0].GetProperty("value").GetString());
            var deletedTemplate = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.CpCategoryTemplatesPath, Form(("csrf_guard_key", "admin-csrf"), ("action", "delete"), ("template_id", templateId!)), staff);
            Assert.True(deletedTemplate.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_catalogue_categories_templates"));

            var wrongItem = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.CpOrderItemObjectPath + "?order_item_id=8&key=nope&csrf_guard_key=admin-csrf", null, staff);
            Assert.Equal(StorefrontPhpAjax.OrderItemWrongKey, wrongItem.Json.RootElement.GetProperty("message").GetString());
            var missingItem = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.CpOrderItemObjectPath + "?order_item_id=8&key=local-tech&csrf_guard_key=admin-csrf", null, staff);
            Assert.Equal(StorefrontPhpAjax.OrderItemsMissingMessage, missingItem.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_items (
                  id INT NOT NULL PRIMARY KEY,
                  status INT NOT NULL,
                  sao_state INT NOT NULL,
                  sao_message VARCHAR(120) NOT NULL DEFAULT '',
                  t2_storage_id INT NOT NULL
                )
                """);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_items_statuses_ref (id INT NOT NULL PRIMARY KEY, color VARCHAR(32) NOT NULL, name VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_states (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, color_background VARCHAR(32) NOT NULL, color_text VARCHAR(32) NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages (id INT NOT NULL PRIMARY KEY, interface_type INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_states_types_link (id INT NOT NULL PRIMARY KEY, state_id INT NOT NULL, interface_type_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_states_types_actions_link (state_type_id INT NOT NULL, action_id INT NOT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_sao_actions (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, btn_class VARCHAR(32) NOT NULL, fontawesome VARCHAR(32) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items_statuses_ref (id, color, name) VALUES (2, '#abc', 'Ordered')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_states (id, name, color_background, color_text) VALUES (3, 'Queued', '#111', '#eee')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, interface_type) VALUES (6, 4)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_states_types_link (id, state_id, interface_type_id) VALUES (10, 3, 4)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_states_types_actions_link (state_type_id, action_id) VALUES (10, 20)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_sao_actions (id, name, btn_class, fontawesome) VALUES (20, 'Send', 'btn', 'fa-paper-plane')");
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items (id, status, sao_state, sao_message, t2_storage_id) VALUES (8, 2, 3, 'waiting', 6)");
            var item = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.CpOrderItemObjectPath + "?order_item_id=8&key=local-tech&csrf_guard_key=admin-csrf", null, staff);
            Assert.True(item.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("Ordered", item.Json.RootElement.GetProperty("item").GetProperty("status_name").GetString());
            Assert.Equal("Send", item.Json.RootElement.GetProperty("item").GetProperty("sao").GetProperty("actions")[0].GetProperty("name").GetString());

            WriteConfig(configRoot, false, false, true);
            var aiOff = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.AiPartsExpertPath, Form(("action", "bootstrap")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.AiDisabled, aiOff.Json.RootElement.GetProperty("message").GetString());
            var agentOff = await SendAsync(client, HttpMethod.Get, StorefrontPhpAjax.PartsAgentPath + "?action=bootstrap", null, string.Empty);
            Assert.Equal(StorefrontPhpAjax.AgentDisabled, agentOff.Json.RootElement.GetProperty("message").GetString());

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

    private static void WriteConfig(string root, bool multilang, bool restricted, bool agentOff)
    {
        var text = """
            <?php
            class DP_Config {
            public $tech_key = 'local-tech';
            public $multilang = 'MULTI';
            public $host_external = '127.0.0.1';
            public $user = 'ecomae';
            public $db = 'throwaway';
            public $prices_email_server = 'mail.local';
            public $prices_email_encryption = 'ssl';
            public $prices_email_port = '993';
            public $prices_email_username = 'prices@local';
            public $prices_email_password = 'hidden-mail-secret';
            public $password = 'hidden-db-secret';
            public $local_crosses = '1';
            public $multilang_editor_restricted_mode = 'RESTRICTED';
            public $epc_parts_agent_enabled = 'AGENT';
            }
            """;
        text = text.Replace("MULTI", multilang ? "1" : "0", StringComparison.Ordinal)
            .Replace("RESTRICTED", restricted ? "1" : "0", StringComparison.Ordinal)
            .Replace("AGENT", agentOff ? "0" : "1", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(root, "config.php"), text);
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

    private static async Task<Sent> SendAsync(HttpClient client, HttpMethod method, string path, Dictionary<string, string>? form, string cookie, string? apiKey = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
        }

        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form);
        }

        return await ReadAsync(client, request).ConfigureAwait(false);
    }

    private static async Task<Sent> SendJsonAsync(HttpClient client, string path, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return await ReadAsync(client, request).ConfigureAwait(false);
    }

    private static async Task<Sent> SendFileAsync(HttpClient client, string path, string field, string fileName, byte[] bytes, params (string Key, string Value)[] fields)
        => await SendFileAsync(client, path, field, fileName, bytes, string.Empty, fields).ConfigureAwait(false);

    private static async Task<Sent> SendFileAsync(HttpClient client, string path, string field, string fileName, byte[] bytes, string cookie, params (string Key, string Value)[] fields)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        var content = new MultipartFormDataContent();
        foreach (var item in fields)
        {
            content.Add(new StringContent(item.Value), item.Key);
        }

        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, field, fileName);
        request.Content = content;
        return await ReadAsync(client, request).ConfigureAwait(false);
    }

    private static async Task<Sent> ReadAsync(HttpClient client, HttpRequestMessage request)
    {
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        JsonDocument json = null!;
        if (body.Length > 0 && body[0] is '{' or '[')
        {
            json = JsonDocument.Parse(body);
        }

        return new Sent(response.StatusCode, body, response.Content.Headers.ContentType?.ToString() ?? string.Empty, response.Headers, json);
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

    private sealed record Sent(HttpStatusCode Status, string Body, string ContentType, HttpResponseHeaders Headers, JsonDocument Json);

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
