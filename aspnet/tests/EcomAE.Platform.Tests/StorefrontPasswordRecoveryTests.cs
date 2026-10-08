using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>The storefront password reset request and the reset link or SMS code, on a throwaway schema.</summary>
public sealed class StorefrontPasswordRecoveryTests
{
    private static readonly IReadOnlyDictionary<string, string> Config = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["domain_path"] = "https://shop.test/",
        ["secret_succession"] = "succession",
        ["site_name"] = "77",
    };

    [Fact]
    public async Task ForgotAndNewPassword_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "`");
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            await ExecAsync(cs, """
                CREATE TABLE sessions (id INT NOT NULL PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL DEFAULT 0);
                INSERT INTO sessions (id, session, user_id) VALUES (1, 'user-token', 7);
                CREATE TABLE users (
                  user_id INT NOT NULL PRIMARY KEY,
                  password VARCHAR(64) NOT NULL DEFAULT '',
                  email VARCHAR(190) NOT NULL DEFAULT '',
                  email_confirmed INT NOT NULL DEFAULT 0,
                  email_code_send_lock_expired INT NOT NULL DEFAULT 0,
                  phone VARCHAR(64) NOT NULL DEFAULT '',
                  phone_confirmed INT NOT NULL DEFAULT 0,
                  phone_code_send_lock_expired INT NOT NULL DEFAULT 0,
                  forgot_password_time INT NOT NULL DEFAULT 0,
                  forgot_password_code VARCHAR(128) NOT NULL DEFAULT ''
                );
                INSERT INTO users (user_id, password, email, email_confirmed, phone, phone_confirmed) VALUES
                (7, 'old', 'nora@x.test', 1, '0501112233', 1),
                (8, 'old', 'unconfirmed@x.test', 0, '', 0);
                CREATE TABLE lang_text_strings (str_key VARCHAR(64) NOT NULL, is_error INT NOT NULL DEFAULT 0, same VARCHAR(8) NULL);
                CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NULL);
                INSERT INTO lang_text_strings (str_key) VALUES ('77'), ('4721'), ('fp_subject'), ('fp_body'), ('fp_sms');
                INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
                ('77', 'en', 'EPartsCart'), ('4721', 'en', 'Reset password'),
                ('fp_subject', 'en', 'Reset on %site_name%'), ('fp_body', 'en', '<p>%forgot_password_code_href%</p>'), ('fp_sms', 'en', 'Code %forgot_password_code%');
                CREATE TABLE notifications_settings (
                  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                  name VARCHAR(64) NOT NULL,
                  email_on INT NOT NULL DEFAULT 0,
                  sms_on INT NOT NULL DEFAULT 0,
                  send_for_not_confirmed INT NOT NULL DEFAULT 0,
                  email_subject VARCHAR(255) NOT NULL DEFAULT '',
                  email_body TEXT NULL,
                  sms_body TEXT NULL,
                  vars TEXT NULL
                );
                INSERT INTO notifications_settings (name, email_on, sms_on, email_subject, email_body, sms_body, vars) VALUES
                ('forgot_password_by_email', 1, 0, 'fp_subject', 'fp_body', '', '[{"type":"text","name":"site_name"},{"type":"text","name":"forgot_password_code_href"}]'),
                ('forgot_password_by_phone', 0, 1, '', '', 'fp_sms', '[{"type":"text","name":"forgot_password_code"}]');
                CREATE TABLE sms_api (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, handler VARCHAR(64) NOT NULL, parameters_values TEXT NULL, active INT NOT NULL DEFAULT 0);
                CREATE TABLE debug_results (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, name VARCHAR(32) NOT NULL, status VARCHAR(8) NULL, debug_result TEXT NULL, time INT NULL);
                """);

            var mailer = new RecordingMailer();
            var sms = new RecordingSms();
            await using var host = await StartAsync(cs, mailer, sms);
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = host.BaseAddress };

            Assert.Equal("/", await PostAsync(client, Form(("forgot_password_contact", "nora@x.test"), ("forgot_password_contact_type", "email")), "session=user-token; u_id=7"));
            Assert.Equal("/users/forgot_password", await PostAsync(client, Form(("forgot_password_contact_type", "email"))));
            Assert.Equal("/users/forgot_password?r=4719", await PostAsync(client, Form(("forgot_password_contact", "nora@x.test"))));
            Assert.Equal("/users/forgot_password?r=4719", await PostAsync(client, Form(("forgot_password_contact", "nora@x.test"), ("forgot_password_contact_type", "fax"))));
            Assert.Equal("/users/forgot_password?r=4720", await PostAsync(client, Form(("forgot_password_contact", "unconfirmed@x.test"), ("forgot_password_contact_type", "email"))));
            Assert.Equal("", await ScalarAsync(cs, "SELECT forgot_password_code FROM users WHERE user_id = 8"));

            var sent = await PostAsync(client, Form(("forgot_password_contact", "nora@x.test"), ("forgot_password_contact_type", "email")), referer: "https://shop.test/en/users/forgot_password");
            Assert.Equal("/en/users/forgot_password?r=4724", sent);
            var code = await ScalarAsync(cs, "SELECT forgot_password_code FROM users WHERE user_id = 7");
            Assert.Matches("^[0-9a-f]{64}$", code);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.InRange(long.Parse(await ScalarAsync(cs, "SELECT forgot_password_time FROM users WHERE user_id = 7"), CultureInfo.InvariantCulture), now - 10, now + 10);
            Assert.InRange(long.Parse(await ScalarAsync(cs, "SELECT email_code_send_lock_expired FROM users WHERE user_id = 7"), CultureInfo.InvariantCulture), now + 290, now + 310);
            var mail = Assert.Single(mailer.Sent);
            Assert.Equal("nora@x.test", mail.To);
            Assert.Equal("Reset on EPartsCart", mail.Subject);
            Assert.Contains("<a target='_blank' href='https://shop.test/en/users/new_password?code=" + code + "&type=email&contact=nora%40x.test'>Reset password</a>", mail.Body, StringComparison.Ordinal);

            Assert.Equal("/users/forgot_password?r=4694", await PostAsync(client, Form(("forgot_password_contact", "nora@x.test"), ("forgot_password_contact_type", "email"))));
            Assert.Single(mailer.Sent);
            Assert.Equal(code, await ScalarAsync(cs, "SELECT forgot_password_code FROM users WHERE user_id = 7"));

            await using (var connection = new MySqlConnection(cs))
            {
                await connection.OpenAsync();
                Assert.True((await StorefrontPhpAjax.NewPasswordAsync(connection, null, null, null, Config, CancellationToken.None)).RedirectHome);
                Assert.True((await StorefrontPhpAjax.NewPasswordAsync(connection, code, "fax", "nora@x.test", Config, CancellationToken.None)).RedirectHome);
                Assert.True((await StorefrontPhpAjax.NewPasswordAsync(connection, code, "email", "nobody@x.test", Config, CancellationToken.None)).RedirectHome);

                await ExecAsync(cs, "UPDATE users SET forgot_password_time = UNIX_TIMESTAMP() - 1801 WHERE user_id = 7");
                var late = await StorefrontPhpAjax.NewPasswordAsync(connection, code, "email", "nora@x.test", Config, CancellationToken.None);
                Assert.Equal(new StorefrontPhpAjax.NewPasswordOutcome(false, "4730", null), late);
                Assert.Equal("", await ScalarAsync(cs, "SELECT forgot_password_code FROM users WHERE user_id = 7"));
                Assert.Equal("0", await ScalarAsync(cs, "SELECT forgot_password_time FROM users WHERE user_id = 7"));
                Assert.True((await StorefrontPhpAjax.NewPasswordAsync(connection, code, "email", "nora@x.test", Config, CancellationToken.None)).RedirectHome);

                await ExecAsync(cs, "UPDATE users SET forgot_password_time = UNIX_TIMESTAMP(), forgot_password_code = 'abc' WHERE user_id = 7");
                var wrong = await StorefrontPhpAjax.NewPasswordAsync(connection, "abd", "email", "nora@x.test", Config, CancellationToken.None);
                Assert.Equal(new StorefrontPhpAjax.NewPasswordOutcome(false, "4729", null), wrong);
                Assert.Equal("", await ScalarAsync(cs, "SELECT forgot_password_code FROM users WHERE user_id = 7"));
                Assert.Equal("old", await ScalarAsync(cs, "SELECT password FROM users WHERE user_id = 7"));
            }

            Assert.Equal("/users/forgot_password?r=4723", await PostAsync(client, Form(("forgot_password_contact", "0501112233"), ("forgot_password_contact_type", "phone"))));
            Assert.Empty(sms.Sent);
            await ExecAsync(cs, "UPDATE users SET phone_code_send_lock_expired = 0 WHERE user_id = 7");
            await ExecAsync(cs, "INSERT INTO sms_api (handler, parameters_values, active) VALUES ('stub', '{}', 1)");
            Assert.Equal("/users/forgot_password?r=4725&contact=0501112233", await PostAsync(client, Form(("forgot_password_contact", "0501112233"), ("forgot_password_contact_type", "phone"))));
            var phoneCode = await ScalarAsync(cs, "SELECT forgot_password_code FROM users WHERE user_id = 7");
            Assert.Matches("^[1-9][0-9]{4}$", phoneCode);
            Assert.Equal("Code " + phoneCode, Assert.Single(sms.Sent));

            await using (var connection = new MySqlConnection(cs))
            {
                await connection.OpenAsync();
                var reset = await StorefrontPhpAjax.NewPasswordAsync(connection, "0" + phoneCode, "phone", "0501112233", Config, CancellationToken.None);
                Assert.False(reset.RedirectHome);
                Assert.Equal("4732", reset.MessageId);
                Assert.Matches("^[0-9a-f]{10}$", reset.Password);
                Assert.Equal(Md5(reset.Password + "succession"), await ScalarAsync(cs, "SELECT password FROM users WHERE user_id = 7"));
                Assert.Equal("", await ScalarAsync(cs, "SELECT forgot_password_code FROM users WHERE user_id = 7"));
                Assert.Equal("0", await ScalarAsync(cs, "SELECT forgot_password_time FROM users WHERE user_id = 7"));

                Assert.Equal(new StorefrontPhpAjax.Communications(false, true), await StorefrontPhpAjax.AvailableCommunicationsAsync(connection, Config, CancellationToken.None));
                await ExecAsync(cs, "INSERT INTO sms_api (handler, parameters_values, active) VALUES ('stub2', '{}', 1)");
                var smtp = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["from_name"] = "Shop", ["from_email"] = "a@x.test", ["smtp_mode"] = "1", ["smtp_encryption"] = "tls",
                    ["smtp_host"] = "mail", ["smtp_port"] = "587", ["smtp_username"] = "u", ["smtp_password"] = "p",
                };
                Assert.Equal(new StorefrontPhpAjax.Communications(true, false), await StorefrontPhpAjax.AvailableCommunicationsAsync(connection, smtp, CancellationToken.None));
            }

            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `docpart`.`users`"));
            Assert.Equal("2", await ScalarAsync(admin, "SELECT COUNT(*) FROM `ecomae`.`users`"));
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }

        Assert.Equal("0", await ScalarAsync(admin, "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '" + database + "'"));
    }

    [Fact]
    public void HtmlEntitiesQuotes_EscapesSingleQuotesLikePhp81()
        => Assert.Equal("a&amp;b &lt;i&gt; &quot;x&quot; &#039;y&#039;", StorefrontPhpAjax.HtmlEntitiesQuotes("a&b <i> \"x\" 'y'"));

    private static Dictionary<string, string> Form(params (string Key, string Value)[] fields)
        => fields.ToDictionary(f => f.Key, f => f.Value);

    private static async Task<string> PostAsync(HttpClient client, Dictionary<string, string> form, string cookie = "", string? referer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, StorefrontPhpAjax.ForgotPasswordSendPath.TrimStart('/')) { Content = new FormUrlEncodedContent(form) };
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        if (referer is not null)
        {
            request.Headers.Referrer = new Uri(referer);
        }

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!.OriginalString;
    }

    private static string Md5(string value)
        => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task ExecAsync(string cs, string sql)
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
        return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<StorefrontOrderPrintTests.ProbeHost> StartAsync(string cs, RecordingMailer mailer, RecordingSms sms)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new StorefrontOrderPrintTests.FixedConnections(cs));
        builder.Services.AddSingleton<ICpPlatformMailer>(mailer);
        builder.Services.AddSingleton<IStorefrontNotifyDispatcher>(new StorefrontNotifyDispatcher(mailer, null, sms));
        builder.Services.AddSingleton<IStorefrontPriceAccess>(StorefrontOrderPrintTests.Stub<IStorefrontPriceAccess>.Create());
        builder.Services.AddSingleton(StorefrontOrderPrintTests.Stub<ISurfaceDashboardSummaryReporter>.Create());
        var app = builder.Build();
        StorefrontPhpAjaxEndpoints.Map(app);
        await app.StartAsync();
        return new StorefrontOrderPrintTests.ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private sealed record Mail(string To, string Subject, string Body);

    private sealed class RecordingMailer : ICpPlatformMailer
    {
        public List<Mail> Sent { get; } = [];

        public IReadOnlyDictionary<string, string> ReadConfig() => Config;

        public Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Sent.Add(new Mail(to, subject, htmlBody));
            return Task.FromResult(new CpSmsSendOutcome(true, "Sent"));
        }
    }

    private sealed class RecordingSms : ICpSmsGateway
    {
        public List<string> Sent { get; } = [];

        public Task<CpSmsSendOutcome> SendAsync(
            string handler,
            IReadOnlyDictionary<string, string> parameters,
            string phone,
            string body,
            CancellationToken cancellationToken = default,
            CpSmsHandlerContext? context = null)
        {
            Sent.Add(body);
            return Task.FromResult(new CpSmsSendOutcome(true, "Sent"));
        }
    }
}
