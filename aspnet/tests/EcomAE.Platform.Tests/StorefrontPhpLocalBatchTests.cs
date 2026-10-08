using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Cp;
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

public sealed class StorefrontPhpLocalBatchTests
{
    [Theory]
    [InlineData("[a-z]+@[a-z.]+", "a@b.c", true)]
    [InlineData("[a-z]+@[a-z.]+", "x a@b.c", false)]
    [InlineData("([a-z]+)@[a-z.]+", "a@b.c", false)]
    [InlineData("[a-z]+(x)?@[a-z.]+", "a@b.c", true)]
    [InlineData("^\\+?[0-9]{7,15}$", "+971501112233", true)]
    [InlineData("a/b", "a/b", false)]
    [InlineData("a\\/b", "a/b", true)]
    [InlineData("[", "[", false)]
    public void PregWholeMatch_FollowsThePhpCountAndWholeValueRule(string pattern, string value, bool expected)
        => Assert.Equal(expected, StorefrontPhpAjax.PregWholeMatch(pattern, value));

    [Fact]
    public async Task LocalBatch_OnThrowawayDatabase_ThenDropped()
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
        var day = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
        try
        {
            var mailer = new RecordingMailer();
            await using var host = await StartAsync(connectionString, mailer);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var signed = "session=user-token; u_id=7";
            var adminCookie = "admin_session=admin-token; admin_u_id=9";
            await ExecuteAsync(connectionString, """
                CREATE TABLE sessions (
                  id INT NOT NULL PRIMARY KEY,
                  session VARCHAR(64) NOT NULL,
                  user_id INT NOT NULL,
                  type INT NOT NULL DEFAULT 0,
                  csrf_guard_key VARCHAR(64) NOT NULL DEFAULT '',
                  data TEXT NULL,
                  `2fa_code` VARCHAR(16) NULL,
                  `2fa_attempts` INT NOT NULL DEFAULT 3
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO sessions (id, session, user_id, type, csrf_guard_key, data, `2fa_code`, `2fa_attempts`) VALUES
                (12, 'user-token', 7, 0, 'csrf-1', '{}', '123456', 3),
                (15, 'admin-token', 9, 1, 'admin-csrf', '{}', '', 3)
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE users (
                  user_id INT NOT NULL PRIMARY KEY,
                  email VARCHAR(190) NOT NULL DEFAULT '',
                  email_confirmed INT NOT NULL DEFAULT 0,
                  email_new VARCHAR(190) NOT NULL DEFAULT '',
                  email_code VARCHAR(64) NOT NULL DEFAULT '',
                  email_code_expired INT NOT NULL DEFAULT 0,
                  email_code_attempts INT NOT NULL DEFAULT 0,
                  email_code_send_lock_expired INT NOT NULL DEFAULT 0,
                  phone VARCHAR(64) NOT NULL DEFAULT '',
                  phone_confirmed INT NOT NULL DEFAULT 0,
                  phone_new VARCHAR(64) NOT NULL DEFAULT '',
                  phone_code_send_lock_expired INT NOT NULL DEFAULT 0,
                  reg_variant INT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO users (user_id, email) VALUES (7, ''), (8, 'taken@example.com')");

            var noCsrf = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath, Form(("tech_key", "")), signed);
            Assert.Equal("Error! CSRF 1", noCsrf.Json.RootElement.GetProperty("message").GetString());
            var missingItems = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath, Form(
                ("csrf_guard_key", "csrf-1"),
                ("tech_key", ""),
                ("user_id", "7"),
                ("total_sum", "10"),
                ("items[0][item_id]", "90"),
                ("items[0][reason_id]", "1"),
                ("items[0][comment]", "a <b> & \""),
                ("items[0][count]", "1")), signed);
            Assert.Equal(StorefrontPhpAjax.OrderItemsMissing, missingItems.Body);
            Assert.DoesNotContain("doesn't exist", missingItems.Body, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_orders_returns'"));

            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_items (
                  id INT NOT NULL PRIMARY KEY,
                  count_need INT NOT NULL,
                  order_id INT NOT NULL,
                  product_type INT NOT NULL,
                  status INT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_items (id, count_need, order_id, product_type, status) VALUES (90, 1, 40, 2, 0)");
            var missingReturnItems = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath, ReturnForm(), signed);
            Assert.Equal(StorefrontPhpAjax.ReturnsMissing, missingReturnItems.Body);
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_returns_items (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  comment VARCHAR(255) NULL,
                  reason_id INT NULL,
                  return_id INT NULL,
                  item_id INT NULL,
                  count_need VARCHAR(32) NULL
                )
                """);
            var missingStatuses = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath, ReturnForm(), signed);
            Assert.Equal(StorefrontPhpAjax.ReturnStatusesMissing, missingStatuses.Body);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_orders_returns_statuses (id INT NOT NULL PRIMARY KEY, caption VARCHAR(64) NOT NULL)");
            var noStatus = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath, ReturnForm(), signed);
            Assert.Equal(StorefrontPhpAjax.ReturnsNoStatus, noStatus.Json.RootElement.GetProperty("error_message").GetString());
            await ExecuteAsync(connectionString, "INSERT INTO shop_orders_returns_statuses (id, caption) VALUES (1, '3806')");
            var missingHeader = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath, ReturnForm(), signed);
            Assert.Equal(StorefrontPhpAjax.ReturnsHeaderMissing, missingHeader.Body);
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_orders_returns'"));
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_orders_returns (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  status_id INT NULL,
                  user_id INT NULL,
                  `sum` DECIMAL(12,2) NULL
                )
                """);
            var forbidden = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath, Form(
                ("csrf_guard_key", "csrf-1"),
                ("tech_key", "no"),
                ("user_id", "7"),
                ("total_sum", "10"),
                ("items[0][item_id]", "90"),
                ("items[0][reason_id]", "1"),
                ("items[0][comment]", "a <b> & \""),
                ("items[0][count]", "1")), signed);
            Assert.Equal(StorefrontPhpAjax.ReturnsForbidden, forbidden.Json.RootElement.GetProperty("error_message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_returns"));
            var loaded = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath, ReturnForm(), signed);
            Assert.True(loaded.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("success", loaded.Json.RootElement.GetProperty("data").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT status_id FROM shop_orders_returns"));
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT user_id FROM shop_orders_returns"));
            Assert.Equal("10.00", await ScalarAsync(connectionString, "SELECT `sum` FROM shop_orders_returns"));
            Assert.Equal("a &lt;b&gt; &amp; &quot;", await ScalarAsync(connectionString, "SELECT comment FROM shop_orders_returns_items"));
            Assert.Equal("90", await ScalarAsync(connectionString, "SELECT item_id FROM shop_orders_returns_items"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT status FROM shop_orders_items WHERE id = 90"));
            var duplicate = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ReturnsLoadPath, ReturnForm(), signed);
            Assert.Equal(StorefrontPhpAjax.ReturnsDuplicate, duplicate.Json.RootElement.GetProperty("error_message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_orders_returns"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_orders_returns_items_images'"));

            var denied = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.GarageManagerPath, Form(("action", "create_job")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.GarageDenied, denied.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_ws_jobs'"));
            var garageCsrf = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.GarageManagerPath, Form(("action", "create_job"), ("csrf_guard_key", "no")), adminCookie);
            Assert.Equal(StorefrontPhpAjax.GarageCsrf, garageCsrf.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_ws_jobs'"));
            var required = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.WorkshopPublicPath, Form(("action", "book"), ("customer_name", "Nora")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.WorkshopRequired, required.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_ws_jobs'"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_ws_jobs"));
            var booked = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.WorkshopPublicPath, Form(
                ("action", "book"),
                ("customer_name", "Nora"),
                ("customer_phone", "+971501112233"),
                ("plate", "d-9"),
                ("complaint", "Brake noise"),
                ("make", "Toyota")), string.Empty);
            Assert.True(booked.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.WorkshopBooked, booked.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("WS-" + day + "-001", booked.Json.RootElement.GetProperty("job_no").GetString());
            Assert.Equal("D-9", await ScalarAsync(connectionString, "SELECT plate FROM epc_ws_jobs"));
            Assert.Equal("checkin", await ScalarAsync(connectionString, "SELECT status FROM epc_ws_jobs"));
            Assert.Equal("Booked from storefront /auto-workshop", await ScalarAsync(connectionString, "SELECT notes FROM epc_ws_jobs"));
            var tracked = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.WorkshopPublicPath, Form(("action", "track"), ("ref", "WS-" + day + "-001"), ("phone", "0501112233")), string.Empty);
            Assert.Equal("Check-in", tracked.Json.RootElement.GetProperty("job").GetProperty("status_label").GetString());
            Assert.Equal("Nora", tracked.Json.RootElement.GetProperty("job").GetProperty("customer_name").GetString());
            var wrongPhone = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.WorkshopPublicPath, Form(("action", "track"), ("ref", "D-9"), ("phone", "0000000")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.WorkshopNoJob, wrongPhone.Json.RootElement.GetProperty("message").GetString());
            var unknown = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.WorkshopPublicPath, Form(("action", "nope")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.WorkshopUnknown, unknown.Json.RootElement.GetProperty("message").GetString());
            var garageJob = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.GarageManagerPath, Form(
                ("action", "create_job"),
                ("csrf_guard_key", "admin-csrf"),
                ("customer_name", "Omar"),
                ("customer_phone", "050"),
                ("plate", "g-2"),
                ("complaint", "Oil")), adminCookie);
            Assert.Equal(StorefrontPhpAjax.GarageCreated, garageJob.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("WS-" + day + "-002", garageJob.Json.RootElement.GetProperty("job").GetProperty("header").GetProperty("job_no").GetString());
            Assert.Equal("G-2", await ScalarAsync(connectionString, "SELECT plate FROM epc_ws_jobs WHERE job_no = 'WS-" + day + "-002'"));
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_ws_jobs"));

            var guestContact = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "set"), ("csrf_guard_key", "csrf-1"), ("contact", "new@example.com")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.ContactsNotLoggedIn, guestContact.Json.RootElement.GetProperty("message").GetString());
            var missingContactFields = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email")), signed);
            Assert.Equal(StorefrontPhpAjax.ContactsBadInput, missingContactFields.Json.RootElement.GetProperty("message").GetString());
            var badContactCsrf = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "set"), ("csrf_guard_key", "no"), ("contact", "new@example.com")), signed);
            Assert.Equal(StorefrontPhpAjax.ContactsCsrf, badContactCsrf.Json.RootElement.GetProperty("message").GetString());
            var missingRegexp = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "set"), ("csrf_guard_key", "csrf-1"), ("contact", "new@example.com")), signed);
            Assert.Equal(StorefrontPhpAjax.RegistrationFieldsMissing, missingRegexp.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(string.Empty, await ScalarAsync(connectionString, "SELECT email FROM users WHERE user_id = 7"));
            await ExecuteAsync(connectionString, "CREATE TABLE reg_fields (name VARCHAR(32) NOT NULL, `regexp` VARCHAR(255) NOT NULL DEFAULT '')");
            var duplicateContact = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "set"), ("csrf_guard_key", "csrf-1"), ("contact", "taken@example.com")), signed);
            Assert.Equal(StorefrontPhpAjax.ContactsDuplicate, duplicateContact.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(string.Empty, await ScalarAsync(connectionString, "SELECT email FROM users WHERE user_id = 7"));
            var notify = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "set"), ("csrf_guard_key", "csrf-1"), ("contact", "new@example.com")), signed);
            Assert.False(notify.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal(StorefrontPhpAjax.ContactsNotifyFailed, notify.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(string.Empty, await ScalarAsync(connectionString, "SELECT email FROM users WHERE user_id = 7"));
            Assert.Empty(mailer.Sent);

            await ExecuteAsync(connectionString, "CREATE TABLE lang_text_strings (str_key VARCHAR(32) NOT NULL, is_error INT NOT NULL DEFAULT 0, same VARCHAR(8) NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE lang_text_strings_translation (str_key VARCHAR(32) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO lang_text_strings (str_key) VALUES ('77'), ('4696'), ('4698'), ('4692')");
            await ExecuteAsync(connectionString, """
                INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
                ('77', 'en', 'EPartsCart'), ('4696', 'en', 'Confirm e-mail'), ('4698', 'en', 'Code not sent'), ('4692', 'en', 'Bad contact'),
                ('4696', 'ar', 'Confirm (ar)')
                """);
            await ExecuteAsync(connectionString, """
                CREATE TABLE notifications_settings (
                  name VARCHAR(64) NOT NULL,
                  email_on INT NOT NULL DEFAULT 0,
                  sms_on INT NOT NULL DEFAULT 0,
                  send_for_not_confirmed INT NOT NULL DEFAULT 0,
                  email_subject VARCHAR(255) NOT NULL DEFAULT '',
                  email_body TEXT NULL,
                  sms_body TEXT NULL,
                  vars TEXT NULL
                )
                """);
            await ExecuteAsync(connectionString, """
                INSERT INTO notifications_settings (name, email_on, send_for_not_confirmed, email_subject, email_body, vars) VALUES
                ('email_confirm_other', 1, 0, 'Confirm on %site_name%', '<p>%email_confirm_href%</p>', '[{"type":"text","name":"site_name"},{"type":"text","name":"email_confirm_href"}]'),
                ('verification_code', 1, 1, 'Code', '<p>Code %verification_code%</p>', '[{"type":"text","name":"verification_code"}]')
                """);
            var notQualified = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "set"), ("csrf_guard_key", "csrf-1"), ("contact", "new@example.com")), signed);
            Assert.False(notQualified.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("Code not sent", notQualified.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(string.Empty, await ScalarAsync(connectionString, "SELECT email FROM users WHERE user_id = 7"));
            Assert.Empty(mailer.Sent);

            await ExecuteAsync(connectionString, "UPDATE notifications_settings SET send_for_not_confirmed = 1 WHERE name = 'email_confirm_other'");
            await ExecuteAsync(connectionString, "INSERT INTO reg_fields (name, `regexp`) VALUES ('email', '[a-z0-9.]+@[a-z0-9.]+')");
            var badEmail = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "set"), ("csrf_guard_key", "csrf-1"), ("contact", "x new@example.com")), signed);
            Assert.Equal("Bad contact", badEmail.Json.RootElement.GetProperty("message").GetString());
            var set = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "set"), ("csrf_guard_key", "csrf-1"), ("contact", "new@example.com")), signed);
            Assert.Equal("{\"status\":true,\"message\":\"\",\"type\":\"email\",\"action\":\"set\",\"contact\":\"new@example.com\"}", set.Body);
            Assert.Equal("new@example.com", await ScalarAsync(connectionString, "SELECT email FROM users WHERE user_id = 7"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT email_confirmed FROM users WHERE user_id = 7"));
            var setCode = await ScalarAsync(connectionString, "SELECT email_code FROM users WHERE user_id = 7");
            Assert.Matches("^[0-9a-f]{32}$", setCode);
            var unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.InRange(long.Parse(await ScalarAsync(connectionString, "SELECT email_code_expired FROM users WHERE user_id = 7"), CultureInfo.InvariantCulture), unixNow + 1790, unixNow + 1810);
            Assert.InRange(long.Parse(await ScalarAsync(connectionString, "SELECT email_code_send_lock_expired FROM users WHERE user_id = 7"), CultureInfo.InvariantCulture), unixNow + 290, unixNow + 310);
            var first = Assert.Single(mailer.Sent);
            Assert.Equal("new@example.com", first.To);
            Assert.Equal("Confirm on EPartsCart", first.Subject);
            Assert.Contains("<a target='_blank' href='https://shop.test//users/confirm_contact?code=" + setCode + "&u_id=7&type=email'>Confirm e-mail</a>", first.Body, StringComparison.Ordinal);

            var locked = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "change"), ("csrf_guard_key", "csrf-1"), ("contact", "other@example.com")), signed);
            Assert.Equal(StorefrontPhpAjax.ContactsLock, locked.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "UPDATE users SET email_code_send_lock_expired = 0 WHERE user_id = 7");
            var changed = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "change"), ("csrf_guard_key", "csrf-1"), ("contact", "other@example.com")), signed, "https://shop.test/ar/users/profile");
            Assert.True(changed.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("new@example.com", changed.Json.RootElement.GetProperty("contact").GetString());
            Assert.Equal("new@example.com", await ScalarAsync(connectionString, "SELECT email FROM users WHERE user_id = 7"));
            Assert.Equal("other@example.com", await ScalarAsync(connectionString, "SELECT email_new FROM users WHERE user_id = 7"));
            var changeCode = await ScalarAsync(connectionString, "SELECT email_code FROM users WHERE user_id = 7");
            Assert.NotEqual(setCode, changeCode);
            Assert.Equal(2, mailer.Sent.Count);
            Assert.Equal("other@example.com", mailer.Sent[1].To);
            Assert.Contains("href='https://shop.test/ar/users/confirm_contact?code=" + changeCode + "&u_id=7&type=email'>Confirm (ar)</a>", mailer.Sent[1].Body, StringComparison.Ordinal);

            await ExecuteAsync(connectionString, "UPDATE users SET email_code_send_lock_expired = 0 WHERE user_id = 7");
            mailer.Fail = true;
            var bounced = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "confirm"), ("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal("Code not sent", bounced.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal(changeCode, await ScalarAsync(connectionString, "SELECT email_code FROM users WHERE user_id = 7"));
            Assert.Equal("other@example.com", await ScalarAsync(connectionString, "SELECT email_new FROM users WHERE user_id = 7"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT email_code_send_lock_expired FROM users WHERE user_id = 7"));
            Assert.Equal(3, mailer.Sent.Count);
            mailer.Fail = false;
            var confirmed = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.ContactsPath, Form(("type", "email"), ("action", "confirm"), ("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal("confirm", confirmed.Json.RootElement.GetProperty("action").GetString());
            Assert.Equal(string.Empty, await ScalarAsync(connectionString, "SELECT email_new FROM users WHERE user_id = 7"));
            Assert.Equal("new@example.com", mailer.Sent[3].To);
            await ExecuteAsync(connectionString, "DELETE FROM reg_fields");
            await ExecuteAsync(connectionString, "UPDATE notifications_settings SET name = 'verification_code_off' WHERE name = 'verification_code'");

            var loginNoCsrf = await SendJsonAsync(client, StorefrontPhpAjax.LoginSendCodePath, "{\"method\":\"fax\"}", signed);
            Assert.Equal("Error! CSRF 1", loginNoCsrf.Json.RootElement.GetProperty("message").GetString());
            var fax = await SendJsonAsync(client, StorefrontPhpAjax.LoginSendCodePath, "{\"csrf_guard_key\":\"csrf-1\",\"method\":\"fax\",\"contact\":\"new@example.com\"}", signed);
            Assert.Equal(403, fax.Json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(StorefrontPhpAjax.LoginUnknownMethod, fax.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("123456", await ScalarAsync(connectionString, "SELECT `2fa_code` FROM sessions WHERE id = 12"));
            var smtp = await SendJsonAsync(client, StorefrontPhpAjax.LoginSendCodePath, "{\"csrf_guard_key\":\"csrf-1\",\"method\":\"smtp\",\"contact\":\"new@example.com\"}", signed);
            Assert.Equal(501, smtp.Json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(StorefrontPhpAjax.LoginNotifyFailed, smtp.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("123456", await ScalarAsync(connectionString, "SELECT `2fa_code` FROM sessions WHERE id = 12"));
            await ExecuteAsync(connectionString, "UPDATE notifications_settings SET name = 'verification_code' WHERE name = 'verification_code_off'");
            var sentBefore = mailer.Sent.Count;
            var codeSent = await SendJsonAsync(client, StorefrontPhpAjax.LoginSendCodePath, "{\"csrf_guard_key\":\"csrf-1\",\"method\":\"smtp\",\"type\":\"login\",\"contact_string\":\"a/b\",\"contact\":\"code@example.com\"}", signed);
            Assert.Equal("{\"status\":200}", codeSent.Body);
            var loginCode = await ScalarAsync(connectionString, "SELECT `2fa_code` FROM sessions WHERE id = 12");
            Assert.Matches("^[1-9][0-9]{5}$", loginCode);
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT `2fa_attempts` FROM sessions WHERE id = 12"));
            var sessionData = await ScalarAsync(connectionString, "SELECT data FROM sessions WHERE id = 12");
            Assert.Contains(",\"type\":\"login\",\"method\":\"smtp\",\"contact_string\":\"a\\/b\",\"contact\":\"code@example.com\"}", sessionData, StringComparison.Ordinal);
            using (var parsed = JsonDocument.Parse(sessionData))
            {
                Assert.Equal(300, parsed.RootElement.GetProperty("expireFaCode").GetInt64() - parsed.RootElement.GetProperty("timeSendFaCode").GetInt64());
            }

            Assert.Equal(sentBefore + 1, mailer.Sent.Count);
            Assert.Equal("code@example.com", mailer.Sent[^1].To);
            Assert.Contains("Code " + loginCode, mailer.Sent[^1].Body, StringComparison.Ordinal);
            await ExecuteAsync(connectionString, "UPDATE sessions SET `2fa_code` = '123456', data = '{}' WHERE id = 12");
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            await ExecuteAsync(connectionString, "UPDATE sessions SET data = '{\"timeSendFaCode\":" + now + "}' WHERE id = 12");
            var limited = await SendJsonAsync(client, StorefrontPhpAjax.LoginSendCodePath, "{\"csrf_guard_key\":\"csrf-1\",\"method\":\"smtp\",\"contact\":\"new@example.com\"}", signed);
            Assert.Equal(501, limited.Json.RootElement.GetProperty("status").GetInt32());
            Assert.Contains("5656 ", limited.Json.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Contains(" 5647", limited.Json.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal("123456", await ScalarAsync(connectionString, "SELECT `2fa_code` FROM sessions WHERE id = 12"));
            await ExecuteAsync(connectionString, "UPDATE sessions SET data = '{\"expireFaCode\":2000000000}' WHERE id = 12");
            var matched = await SendJsonAsync(client, StorefrontPhpAjax.LoginCheckCodePath, "{\"csrf_guard_key\":\"csrf-1\",\"code\":\"123456\"}", signed);
            Assert.Equal(200, matched.Json.RootElement.GetProperty("status").GetInt32());
            Assert.False(matched.Json.RootElement.TryGetProperty("message", out _));
            Assert.Equal("3", await ScalarAsync(connectionString, "SELECT `2fa_attempts` FROM sessions WHERE id = 12"));
            var mismatch = await SendJsonAsync(client, StorefrontPhpAjax.LoginCheckCodePath, "{\"csrf_guard_key\":\"csrf-1\",\"code\":\"000000\"}", signed);
            Assert.Equal("5643: 2.", mismatch.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT `2fa_attempts` FROM sessions WHERE id = 12"));
            await ExecuteAsync(connectionString, "UPDATE sessions SET data = '{\"expireFaCode\":1}' WHERE id = 12");
            var expired = await SendJsonAsync(client, StorefrontPhpAjax.LoginCheckCodePath, "{\"csrf_guard_key\":\"csrf-1\",\"code\":\"123456\"}", signed);
            Assert.Equal(StorefrontPhpAjax.LoginExpired, expired.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("2", await ScalarAsync(connectionString, "SELECT `2fa_attempts` FROM sessions WHERE id = 12"));
            await ExecuteAsync(connectionString, "UPDATE sessions SET `2fa_attempts` = 0 WHERE id = 12");
            var none = await SendJsonAsync(client, StorefrontPhpAjax.LoginCheckCodePath, "{\"csrf_guard_key\":\"csrf-1\",\"code\":\"123456\"}", signed);
            Assert.Equal(StorefrontPhpAjax.LoginNoAttempts, none.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT `2fa_attempts` FROM sessions WHERE id = 12"));

            var bulkGuest = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(), string.Empty);
            Assert.Equal(StorefrontPhpAjax.BulkLogin, bulkGuest.Json.RootElement.GetProperty("message").GetString());
            var bulkCsrf = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(), signed);
            Assert.Equal("Error! CSRF 1", bulkCsrf.Json.RootElement.GetProperty("message").GetString());
            var bulkWrong = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "no")), signed);
            Assert.Equal("Error! CSRF 4", bulkWrong.Json.RootElement.GetProperty("message").GetString());
            var bulkProfiles = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.UserProfilesMissing, bulkProfiles.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE users_profiles (user_id INT NULL, data_key VARCHAR(64) NULL, data_value VARCHAR(255) NULL)");
            var bulkGroups = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.CustomerGroupsMissing, bulkGroups.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE users_groups_bind (user_id INT NULL, group_id INT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE groups (id INT NOT NULL PRIMARY KEY, for_registrated TINYINT NOT NULL DEFAULT 0, for_backend TINYINT NOT NULL DEFAULT 0, parent INT NOT NULL DEFAULT 0)");
            var bulkProfile = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.BulkProfile, bulkProfile.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "INSERT INTO users_groups_bind (user_id, group_id) VALUES (7, 4)");
            var history = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "csrf-1"), ("action", "history_update")), signed);
            Assert.Equal(StorefrontPhpAjax.BulkHistory, history.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_bulk_upload_history'"));
            var warehouses = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.BulkWarehousesMissing, warehouses.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "CREATE TABLE shop_geo (id INT NOT NULL PRIMARY KEY)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_offices_geo_map (office_id INT NULL, geo_id INT NULL)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_offices (id INT NOT NULL PRIMARY KEY)");
            var noneWarehouses = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.BulkNoWarehouses, noneWarehouses.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "INSERT INTO shop_geo (id) VALUES (1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices_geo_map (office_id, geo_id) VALUES (4, 1)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices (id) VALUES (4)");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_storages_interfaces_types (id INT NOT NULL PRIMARY KEY, handler_folder VARCHAR(64) NOT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages_interfaces_types (id, handler_folder) VALUES (1, 'prices')");
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_storages (
                  id INT NOT NULL PRIMARY KEY,
                  name VARCHAR(64) NOT NULL,
                  currency VARCHAR(8) NOT NULL,
                  hidden TINYINT NOT NULL,
                  interface_type INT NOT NULL,
                  connection_options TEXT NULL
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO shop_storages (id, name, currency, hidden, interface_type, connection_options) VALUES (8, 'SHJ', 'AED', 0, 1, '{\"price_id\":3}')");
            await ExecuteAsync(connectionString, "CREATE TABLE shop_offices_storages_map (office_id INT NULL, storage_id INT NULL)");
            await ExecuteAsync(connectionString, "INSERT INTO shop_offices_storages_map (office_id, storage_id) VALUES (4, 8)");
            var needFile = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.BulkFileRequired, needFile.Json.RootElement.GetProperty("message").GetString());
            var needPart = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "csrf-1"), ("action", "cross"), ("article", " ")), signed);
            Assert.Equal(StorefrontPhpAjax.BulkPartRequired, needPart.Json.RootElement.GetProperty("message").GetString());
            var cross = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.BulkUploadPath, Form(("csrf_guard_key", "csrf-1"), ("action", "cross"), ("article", "0986")), signed);
            Assert.Equal(StorefrontPhpAjax.PriceListsMissing, cross.Json.RootElement.GetProperty("message").GetString());
            Assert.DoesNotContain("BOSCH", cross.Body, StringComparison.Ordinal);
            var priced = await SendFileAsync(client, StorefrontPhpAjax.BulkUploadPath, "bulk_file", "rows.csv", "Brand,Part,Qty\nBOSCH,0986,1\n", Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.PriceListsMissing, priced.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('shop_docpart_prices_data','shop_storages_data','epc_bulk_upload_history')"));

            var vendorGuest = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.VendorIngestPath, Form(), string.Empty);
            Assert.Equal(StorefrontPhpAjax.VendorSignIn, vendorGuest.Json.RootElement.GetProperty("message").GetString());
            var vendorMissing = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.VendorIngestPath, Form(), signed);
            Assert.Equal(StorefrontPhpAjax.VendorAccountsMissing, vendorMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'epc_vendor_accounts'"));
            await ExecuteAsync(connectionString, """
                CREATE TABLE epc_vendor_accounts (
                  id INT NOT NULL PRIMARY KEY,
                  user_id INT NOT NULL,
                  storage_id INT NOT NULL DEFAULT 0,
                  status VARCHAR(16) NOT NULL,
                  vendor_full VARCHAR(64) NOT NULL DEFAULT '',
                  vendor_short VARCHAR(64) NOT NULL DEFAULT ''
                )
                """);
            await ExecuteAsync(connectionString, "INSERT INTO epc_vendor_accounts (id, user_id, storage_id, status) VALUES (1, 7, 0, 'pending')");
            var pending = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.VendorIngestPath, Form(), signed);
            Assert.Equal(StorefrontPhpAjax.VendorNotApproved, pending.Json.RootElement.GetProperty("message").GetString());
            await ExecuteAsync(connectionString, "UPDATE epc_vendor_accounts SET status = 'approved' WHERE id = 1");
            var token = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.VendorIngestPath, Form(("csrf_guard_key", "no")), signed);
            Assert.Equal(StorefrontPhpAjax.VendorToken, token.Json.RootElement.GetProperty("message").GetString());
            var choose = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.VendorIngestPath, Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.VendorChooseFile, choose.Json.RootElement.GetProperty("message").GetString());
            var pdf = await SendFileAsync(client, StorefrontPhpAjax.VendorIngestPath, "price_file", "book.pdf", "a,b\n", Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.VendorBadType, pdf.Json.RootElement.GetProperty("message").GetString());
            var huge = await SendFileAsync(client, StorefrontPhpAjax.VendorIngestPath, "price_file", "big.csv", new string('a', (12 * 1024 * 1024) + 1), Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.VendorTooLarge, huge.Json.RootElement.GetProperty("message").GetString());
            var imported = await SendFileAsync(client, StorefrontPhpAjax.VendorIngestPath, "price_file", "stock.csv", "brand,article,price\nBOSCH,0986,10\n", Form(("csrf_guard_key", "csrf-1")), signed);
            Assert.Equal(StorefrontPhpAjax.VendorImportFailed, imported.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT storage_id FROM epc_vendor_accounts WHERE id = 1"));
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('shop_storages_data','shop_docpart_prices_data')"));

            var direct = await SendAsync(client, HttpMethod.Get, "/api/UCatalog/get_marks.php", null, "UCatalog=1");
            Assert.Equal(StorefrontPhpAjax.UcatsNoAccess, direct.Body);
            Assert.DoesNotContain("ABARTH", direct.Body, StringComparison.Ordinal);
            var forbiddenCatalog = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"get_marks\",\"type\":\"cars\"}")), string.Empty);
            Assert.Equal(StorefrontPhpAjax.UCatalogForbidden403, forbiddenCatalog.Body);
            var marks = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"get_marks\",\"type\":\"cars\"}")), "UCatalog=1");
            Assert.False(marks.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Contains("2096", marks.Json.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal("get_types", marks.Json.RootElement.GetProperty("request_object").GetProperty("action").GetString());
            Assert.DoesNotContain("ABARTH", marks.Body, StringComparison.Ordinal);
            var garageMissing = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"get_garage\",\"id\":4}")), "UCatalog=1; " + signed);
            Assert.Equal(StorefrontPhpAjax.GarageMissing, garageMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_docpart_garage'"));
            var guestNotepad = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"add_notepad\",\"article\":\"A1\"}")), "UCatalog=1");
            Assert.Contains("2063", guestNotepad.Body, StringComparison.Ordinal);
            Assert.True(guestNotepad.Json.RootElement.GetProperty("status").GetBoolean());
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_garage (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  caption VARCHAR(255) NOT NULL,
                  UCatalog_json TEXT NOT NULL,
                  user_id INT NOT NULL,
                  mark_id INT NOT NULL DEFAULT 0
                )
                """);
            await ExecuteAsync(connectionString, "CREATE TABLE shop_docpart_cars (id INT NOT NULL PRIMARY KEY, caption VARCHAR(64) NOT NULL)");
            var addedGarage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"add_garage\",\"request_object\":{\"caption\":\"Daily\"}}")), "UCatalog=1; " + signed);
            Assert.True(addedGarage.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Contains("2062", addedGarage.Body, StringComparison.Ordinal);
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT user_id FROM shop_docpart_garage"));
            Assert.Equal("Daily", await ScalarAsync(connectionString, "SELECT caption FROM shop_docpart_garage"));
            Assert.Contains("Daily", await ScalarAsync(connectionString, "SELECT UCatalog_json FROM shop_docpart_garage"), StringComparison.Ordinal);
            var notepad = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"get_notepad\",\"manufacturer\":\"Bosch\",\"article\":\"0986\",\"name\":\"Pad\"}")), "UCatalog=1; " + signed);
            Assert.Contains("Daily", notepad.Body, StringComparison.Ordinal);
            Assert.Contains("2100", notepad.Body, StringComparison.Ordinal);
            Assert.Contains("2099", notepad.Body, StringComparison.Ordinal);
            var notepadMissing = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"add_notepad\",\"id_notepad\":1,\"manufacturer\":\"Bosch\",\"article\":\"A&1\",\"name\":\"<b>Pad</b>\"}")), "UCatalog=1; " + signed);
            Assert.Equal(StorefrontPhpAjax.NotepadMissing, notepadMissing.Json.RootElement.GetProperty("message").GetString());
            Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'shop_docpart_garage_notepad'"));
            await ExecuteAsync(connectionString, """
                CREATE TABLE shop_docpart_garage_notepad (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  user_id INT NOT NULL,
                  garage_id INT NOT NULL,
                  brend VARCHAR(64) NOT NULL,
                  article VARCHAR(64) NOT NULL,
                  name VARCHAR(255) NOT NULL,
                  exist INT NOT NULL,
                  price DECIMAL(12,2) NOT NULL,
                  comment VARCHAR(255) NOT NULL
                )
                """);
            var noted = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"add_notepad\",\"id_notepad\":1,\"manufacturer\":\"Bosch\",\"article\":\"A&1\",\"name\":\"<b>Pad</b>\"}")), "UCatalog=1; " + signed);
            Assert.Contains("2066", noted.Body, StringComparison.Ordinal);
            Assert.Equal("A&amp;1", await ScalarAsync(connectionString, "SELECT article FROM shop_docpart_garage_notepad"));
            Assert.Equal("Pad", await ScalarAsync(connectionString, "SELECT name FROM shop_docpart_garage_notepad"));
            Assert.StartsWith("2065 ", await ScalarAsync(connectionString, "SELECT comment FROM shop_docpart_garage_notepad"), StringComparison.Ordinal);
            var wrongGarage = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"add_notepad\",\"id_notepad\":99,\"article\":\"A1\"}")), "UCatalog=1; " + signed);
            Assert.Contains("2064", wrongGarage.Body, StringComparison.Ordinal);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_garage_notepad"));
            var info = await SendAsync(client, HttpMethod.Post, StorefrontPhpAjax.UCatalogApiPath, Form(("request_object", "{\"action\":\"get_info\",\"key\":\"pad-1\",\"article\":\"0986\",\"manufacturer\":\"Bosch\"}")), "UCatalog=1");
            Assert.False(info.Json.RootElement.GetProperty("status").GetBoolean());
            Assert.Equal("pad-1", info.Json.RootElement.GetProperty("key").GetString());
            Assert.DoesNotContain("0986", info.Json.RootElement.GetProperty("json").GetString(), StringComparison.Ordinal);

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

    private static Dictionary<string, string> ReturnForm()
        => Form(
            ("csrf_guard_key", "csrf-1"),
            ("tech_key", ""),
            ("user_id", "7"),
            ("total_sum", "10"),
            ("items[0][item_id]", "90"),
            ("items[0][reason_id]", "1"),
            ("items[0][comment]", "a <b> & \""),
            ("items[0][count]", "1"));

    private static Dictionary<string, string> Form(params (string Key, string Value)[] fields)
    {
        var form = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            form[field.Key] = field.Value;
        }

        return form;
    }

    private static Task<Sent> SendAsync(HttpClient client, HttpMethod method, string path, Dictionary<string, string>? form, string cookie, string? referer = null)
        => SendContentAsync(client, method, path, form is null ? null : new FormUrlEncodedContent(form), cookie, referer);

    private static Task<Sent> SendJsonAsync(HttpClient client, string path, string json, string cookie)
        => SendContentAsync(client, HttpMethod.Post, path, new StringContent(json, Encoding.UTF8, "application/json"), cookie);

    private static Task<Sent> SendFileAsync(HttpClient client, string path, string field, string fileName, string text, Dictionary<string, string> fields, string cookie)
    {
        var content = new MultipartFormDataContent();
        foreach (var fieldPair in fields)
        {
            content.Add(new StringContent(fieldPair.Value), fieldPair.Key);
        }

        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, field, fileName);
        return SendContentAsync(client, HttpMethod.Post, path, content, cookie);
    }

    private static async Task<Sent> SendContentAsync(HttpClient client, HttpMethod method, string path, HttpContent? content, string cookie, string? referer = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (referer is not null)
        {
            request.Headers.Referrer = new Uri(referer);
        }

        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        if (content is not null)
        {
            request.Content = content;
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

    private static async Task<ProbeHost> StartAsync(string connectionString, RecordingMailer mailer)
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
        builder.Services.AddSingleton<ICpPlatformMailer>(mailer);
        builder.Services.AddSingleton<IStorefrontNotifyDispatcher>(new StorefrontNotifyDispatcher(mailer));
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private sealed record Sent(string Body, string ContentType, JsonDocument Json);

    private sealed record Mail(string To, string Subject, string Body);

    private sealed class RecordingMailer : ICpPlatformMailer
    {
        public List<Mail> Sent { get; } = [];

        public bool Fail { get; set; }

        public IReadOnlyDictionary<string, string> ReadConfig()
            => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["domain_path"] = "https://shop.test/",
                ["secret_succession"] = "succession",
                ["site_name"] = "77",
            };

        public Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Sent.Add(new Mail(to, subject, htmlBody));
            return Task.FromResult(Fail ? CpSmsSendOutcome.Fail("SMTP refused") : new CpSmsSendOutcome(true, "Sent"));
        }
    }

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
