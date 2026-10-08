using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// E-mail sign-in code send + registration verify-only. <c>Fixtures/AuthEmailOtp/goldens.json</c> is what the real
/// <c>epc-auth-send-code.php</c> / <c>epc-auth-otp-verify-only.php</c> (and their content aliases) answered for each case of
/// <c>cases.json</c> — status, headers, body and the resulting <c>epc_auth_otp_requests</c> rows — run by <c>harness.py</c>
/// on a throwaway schema seeded with <c>seed.sql</c>.
/// </summary>
public sealed class AuthEmailOtpTests
{
    private const string Secret = "epartscart-deploy-2026";

    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "AuthEmailOtp");

    private static readonly Dictionary<string, string> DefaultConfig = new(StringComparer.Ordinal)
    {
        ["smtp_mode"] = "0",
        ["smtp_host"] = "",
        ["smtp_port"] = "465",
        ["smtp_encryption"] = "ssl",
        ["smtp_username"] = "",
        ["smtp_password"] = "",
        ["from_email"] = "shop@example.com",
        ["from_name"] = "Shop",
    };

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Answer_AndRows_MatchPhp_OnThrowawayDatabase_ThenDropped(string name)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EPC_DEPLOY_TOKEN")))
        {
            return;
        }

        var testCase = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == name).Clone();
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name).Clone();

        await using var db = await ThrowawayDb.CreateAsync(password);
        var docRoot = Directory.CreateTempSubdirectory("otp-docroot-").FullName;
        var mailer = new RecordingMailer(null);
        try
        {
            await db.ExecAsync((await File.ReadAllTextAsync(Path.Combine(FixtureDir, "seed.sql"))).Replace("@DB@", db.Name).Replace("@PW@", password));
            await db.ExecAsync(AuthEmailOtp.OtpTableDdl);
            if (testCase.TryGetProperty("setup", out var setup))
            {
                foreach (var statement in setup.EnumerateArray())
                {
                    await db.ExecAsync(statement.GetString()!);
                }
            }

            var ipRows = testCase.TryGetProperty("setup_ip_rows", out var ipEl) ? ipEl.GetInt32() : 0;
            for (var i = 0; i < ipRows; i++)
            {
                await db.ExecAsync(
                    "INSERT INTO epc_auth_otp_requests (email, code_hash, tenant_key, expires_at, ip_address, created_at) VALUES ('ip"
                    + i.ToString(CultureInfo.InvariantCulture) + "@example.com','x','acme',UNIX_TIMESTAMP()+600,'127.0.0.1',UNIX_TIMESTAMP()-30)");
            }

            if (testCase.TryGetProperty("otp", out var otp))
            {
                var hash = AuthEmailOtp.CodeHash(otp.GetProperty("code").GetString()!, Secret);
                await db.ExecAsync(
                    "INSERT INTO epc_auth_otp_requests (email, code_hash, tenant_key, expires_at, ip_address, created_at) VALUES ('"
                    + otp.GetProperty("email").GetString() + "','" + hash + "','" + otp.GetProperty("tenant_key").GetString()
                    + "',UNIX_TIMESTAMP()+" + otp.GetProperty("expires_in").GetInt32().ToString(CultureInfo.InvariantCulture)
                    + ",'10.0.0.9',UNIX_TIMESTAMP()-10)");
            }

            var config = new Dictionary<string, string>(DefaultConfig, StringComparer.Ordinal);
            if (testCase.TryGetProperty("config", out var overrides))
            {
                foreach (var p in overrides.EnumerateObject())
                {
                    config[p.Name] = p.Value.GetString()!;
                }
            }

            await File.WriteAllTextAsync(
                Path.Combine(docRoot, "config.php"),
                "<?php\nclass DP_Config {\n" + string.Concat(config.Select(p => "public $" + p.Key + " = " + PhpString(p.Value) + ";\n")) + "}\n");
            if (testCase.TryGetProperty("smtp_file", out var smtpFile))
            {
                await File.WriteAllTextAsync(
                    Path.Combine(docRoot, "config.epc-smtp.php"),
                    "<?php\nreturn array(" + string.Join(", ", smtpFile.EnumerateObject().Select(p => PhpString(p.Name) + " => " + PhpString(p.Value.GetString()!))) + ");\n");
            }

            await using var host = await StartAsync(db.ConnectionString, docRoot, mailer);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            using var request = new HttpRequestMessage(HttpMethod.Post, testCase.GetProperty("path").GetString()!.TrimStart('/'));
            request.Headers.Host = testCase.TryGetProperty("host", out var hostEl) ? hostEl.GetString() : "localhost";
            request.Content = testCase.TryGetProperty("form", out var form)
                ? new FormUrlEncodedContent(form.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!))
                : new StringContent(testCase.GetProperty("json").GetRawText(), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(golden.GetProperty("status").GetInt32(), (int)response.StatusCode);
            Assert.Equal(golden.GetProperty("type").GetString(), response.Content.Headers.ContentType?.ToString());
            Assert.Equal(golden.GetProperty("cache").GetString(), response.Headers.CacheControl?.ToString());
            Assert.Equal(golden.GetProperty("body").GetString(), body);
            Assert.Equal(golden.GetProperty("rows").GetRawText(), JsonSerializer.Serialize(await db.RowsAsync()), JsonElementComparerIgnoringWhitespace.Instance);
            Assert.Empty(mailer.Sent);
        }
        finally
        {
            Directory.Delete(docRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Send_WithCompleteSmtp_MailsTheCode_ThatVerifyOnlyAccepts_Once()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using var db = await ThrowawayDb.CreateAsync(password);
        var docRoot = Directory.CreateTempSubdirectory("otp-docroot-").FullName;
        var mailer = new RecordingMailer(null);
        try
        {
            await db.ExecAsync((await File.ReadAllTextAsync(Path.Combine(FixtureDir, "seed.sql"))).Replace("@DB@", db.Name).Replace("@PW@", password));
            await File.WriteAllTextAsync(
                Path.Combine(docRoot, "config.php"),
                "<?php\nclass DP_Config {\npublic $smtp_mode = '1';\npublic $smtp_host = 'smtp.example.net';\npublic $smtp_port = '587';\n"
                + "public $smtp_encryption = 'tls';\npublic $smtp_username = 'shop@example.com';\npublic $smtp_password = 'longpassword';\n"
                + "public $from_email = 'shop@example.com';\npublic $from_name = 'Shop';\n}\n");

            await using var host = await StartAsync(db.ConnectionString, docRoot, mailer);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var (status, body) = await PostAsync(client, "epc-auth-send-code.php", "{\"email\":\"Buyer@Example.com\",\"tenant_key\":\"acme\",\"context\":\"storefront\"}");
            Assert.Equal(200, status);
            Assert.Equal("{\"ok\":true,\"message\":\"Sign-in code sent \\u2014 check your inbox\",\"expires_in\":600}", body);

            var sent = Assert.Single(mailer.Sent);
            Assert.Equal("buyer@example.com", sent.To);
            Assert.Equal("smtp.example.net", sent.Host);
            var code = sent.Subject[^6..];
            Assert.Equal("Acme Parts — sign-in code " + code, sent.Subject);
            Assert.Equal(
                "<p>Your sign-in code for <strong>Acme Parts</strong> is:</p><p style=\"font-size:28px;letter-spacing:6px;font-weight:700;\">"
                + code + "</p><p>Expires in 10 minutes.</p>",
                sent.Html);
            var rows = await db.RowsAsync();
            Assert.Equal("acme", Assert.Single(rows).tenant_key);
            Assert.Equal("", rows[0].operator_otp);

            (status, body) = await PostAsync(client, "epc-auth-otp-verify-only.php", "{\"email\":\"buyer@example.com\",\"code\":\"" + code + "\",\"tenant_key\":\"acme\"}");
            Assert.Equal(200, status);
            Assert.Equal("{\"ok\":true,\"message\":\"Email verified\",\"verified_email\":\"buyer@example.com\"}", body);
            Assert.Empty(await db.RowsAsync());

            (status, body) = await PostAsync(client, "epc-auth-otp-verify-only.php", "{\"email\":\"buyer@example.com\",\"code\":\"" + code + "\",\"tenant_key\":\"acme\"}");
            Assert.Equal(400, status);
            Assert.Equal("{\"ok\":false,\"message\":\"Invalid or expired code \\u2014 please try again\"}", body);
        }
        finally
        {
            Directory.Delete(docRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Send_SmtpRejects_ClassifiesTheError_AndDemoTenantsFallBackToTheOperatorCode()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using var db = await ThrowawayDb.CreateAsync(password);
        var docRoot = Directory.CreateTempSubdirectory("otp-docroot-").FullName;
        var mailer = new RecordingMailer("535 5.7.8 Username and Password not accepted");
        try
        {
            await db.ExecAsync((await File.ReadAllTextAsync(Path.Combine(FixtureDir, "seed.sql"))).Replace("@DB@", db.Name).Replace("@PW@", password));
            await File.WriteAllTextAsync(
                Path.Combine(docRoot, "config.php"),
                "<?php\nclass DP_Config {\npublic $smtp_mode = '1';\npublic $smtp_host = 'smtp.example.net';\npublic $smtp_port = '465';\n"
                + "public $smtp_encryption = 'ssl';\npublic $smtp_username = 'shop@example.com';\npublic $smtp_password = 'longpassword';\n"
                + "public $from_email = 'shop@example.com';\npublic $from_name = 'Shop';\n}\n");

            await using var host = await StartAsync(db.ConnectionString, docRoot, mailer);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var (status, body) = await PostAsync(client, "epc-auth-send-code.php", "{\"email\":\"buyer@example.com\",\"tenant_key\":\"acme\",\"context\":\"cp\"}");
            Assert.Equal(400, status);
            Assert.Equal(
                "{\"ok\":false,\"message\":\"SMTP authentication failed \\u2014 use an app password (Gmail) or correct mailbox password (Hostinger).\","
                + "\"smtp_detail\":\"535 5.7.8 Username and Password not accepted\"}",
                body);
            Assert.Equal("Acme Parts CP — sign-in code ", Assert.Single(mailer.Sent).Subject[..^6]);

            (status, body) = await PostAsync(client, "epc-auth-send-code.php", "{\"email\":\"buyer@example.com\",\"tenant_key\":\"demo_acme\",\"context\":\"storefront\"}");
            Assert.Equal(200, status);
            Assert.Contains("\"demo_otp_logged\":true,\"smtp_hint\":\"SMTP authentication failed", body, StringComparison.Ordinal);
            var rows = await db.RowsAsync();
            Assert.Equal(2, rows.Count);
            Assert.Equal("D6", rows[1].operator_otp);
            Assert.Equal(mailer.Sent[1].Subject[^6..], await db.ScalarAsync("SELECT JSON_UNQUOTE(JSON_EXTRACT(context_json, '$._operator_otp')) FROM epc_auth_otp_requests ORDER BY id DESC LIMIT 1"));
        }
        finally
        {
            Directory.Delete(docRoot, recursive: true);
        }
    }

    [Fact]
    public void Diagnose_And_Classify_FollowThePhpRules()
    {
        var cfg = AuthEmailOtp.EffectiveConfig(
            new Dictionary<string, string> { ["smtp_mode"] = "0", ["smtp_host"] = "mail.cfg", ["from_email"] = "a@b.co", ["smtp_password"] = "x" },
            new Dictionary<string, string?> { ["smtp_password"] = "from-local-file" },
            AuthEmailOtp.ParsePhpArrayPairs("<?php return array('smtp_mode' => 1, 'smtp_host' => '', 'allow_mail_fallback' => true, 'from_name' => 'O\\'Brien');"),
            new Dictionary<string, string> { ["smtp_host"] = "tenant.smtp" });
        Assert.Equal("1", cfg["smtp_mode"]);
        Assert.Equal("tenant.smtp", cfg["smtp_host"]);
        Assert.Equal("from-local-file", cfg["smtp_password"]);
        Assert.Equal("O'Brien", cfg["from_name"]);
        Assert.True(cfg.AllowMailFallback);
        Assert.Empty(AuthEmailOtp.Diagnose(cfg));

        Assert.StartsWith("SMTP connection failed", AuthEmailOtp.ClassifyError("Connection refused").Message, StringComparison.Ordinal);
        Assert.StartsWith("SMTP password not configured", AuthEmailOtp.ClassifyError("Password is empty").Message, StringComparison.Ordinal);
        Assert.Equal(AuthEmailOtp.MsgSendFallback, AuthEmailOtp.ClassifyError("mailbox full").Message);

        Assert.Empty(AuthEmailOtp.TenantOverlay("{\"smtp\":{\"use_tenant_smtp\":false,\"smtp_host\":\"x\"}}"));
        Assert.Equal("x", AuthEmailOtp.TenantOverlay("{\"smtp\":{\"use_tenant_smtp\":1,\"smtp_host\":\"x\",\"smtp_port\":\"\"}}")["smtp_host"]);
        Assert.False(AuthEmailOtp.DemoFallbackAllowed("acme", null));
        Assert.True(AuthEmailOtp.DemoFallbackAllowed("demo_acme", new Dictionary<string, string?> { ["disable_demo_otp_fallback"] = "0" }));
        Assert.Equal("Shop", AuthEmailOtp.MailLabel("storefront", null));
        Assert.Equal("Sign in", AuthEmailOtp.MailLabel("cp", null));
        Assert.StartsWith("<p>Your sign-in code for <strong>A&amp;B &#039;Ünï&#039;</strong> is:</p>", AuthEmailOtp.MailHtml("A&B 'Ünï'", "123456"), StringComparison.Ordinal);
    }

    private static async Task<(int Status, string Body)> PostAsync(HttpClient client, string path, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Host = "localhost";
        using var response = await client.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static string PhpString(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

    private static async Task<StorefrontOrderPrintTests.ProbeHost> StartAsync(string cs, string docRoot, IAuthOtpMailer mailer)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new StorefrontOrderPrintTests.FixedConnections(cs));
        builder.Services.AddSingleton(mailer);
        builder.Services.Configure<PhpReferenceOptions>(o => o.PhpDocRoot = docRoot);
        var app = builder.Build();
        app.UseRouting();
        AuthEmailOtpEndpoints.Map(app);
        await app.StartAsync();
        return new StorefrontOrderPrintTests.ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    public sealed record OtpRow(string email, string tenant_key, int hash_len, int ttl, string ip, bool fresh, string auth_mode, string operator_otp);

    private sealed record SentMail(string Host, string To, string Subject, string Html);

    private sealed class RecordingMailer(string? error) : IAuthOtpMailer
    {
        public List<SentMail> Sent { get; } = [];

        public Task<string?> SendAsync(SmtpEffectiveConfig config, string to, string subject, string html, string text, CancellationToken cancellationToken)
        {
            Sent.Add(new SentMail(config["smtp_host"], to, subject, html));
            return Task.FromResult(error);
        }
    }

    private sealed class ThrowawayDb : IAsyncDisposable
    {
        private readonly string _admin;

        private ThrowawayDb(string name, string admin, string cs)
        {
            Name = name;
            _admin = admin;
            ConnectionString = cs;
        }

        public string Name { get; }

        public string ConnectionString { get; }

        public static async Task<ThrowawayDb> CreateAsync(string password)
        {
            var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
            var cs = "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";";
            Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
            await Exec(admin, "CREATE DATABASE `" + name + "`");
            return new ThrowawayDb(name, admin, cs);
        }

        public Task ExecAsync(string sql) => Exec(ConnectionString, sql);

        public async Task<string?> ScalarAsync(string sql)
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }

        public async Task<List<OtpRow>> RowsAsync()
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT email, tenant_key, LENGTH(code_hash), IFNULL(context_json,''), expires_at - created_at, ip_address, "
                + "created_at > UNIX_TIMESTAMP() - 60 FROM epc_auth_otp_requests ORDER BY id";
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<OtpRow>();
            while (await reader.ReadAsync())
            {
                var context = reader.GetString(3);
                string authMode = "", operatorOtp = "";
                if (context.Length > 0)
                {
                    using var doc = JsonDocument.Parse(context);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        authMode = doc.RootElement.TryGetProperty("auth_mode", out var m) ? m.GetString() ?? "" : "";
                        var op = doc.RootElement.TryGetProperty("_operator_otp", out var o) ? o.GetString() ?? "" : "";
                        operatorOtp = op.Length == 6 && op.All(char.IsAsciiDigit) ? "D6" : op;
                    }
                }

                rows.Add(new OtpRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    reader.GetString(5),
                    Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture) == 1,
                    authMode,
                    operatorOtp));
            }

            return rows;
        }

        public async ValueTask DisposeAsync()
        {
            MySqlConnection.ClearAllPools();
            await Exec(_admin, "DROP DATABASE IF EXISTS `" + Name + "`");
        }

        private static async Task Exec(string cs, string sql)
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class JsonElementComparerIgnoringWhitespace : IEqualityComparer<string>
    {
        public static readonly JsonElementComparerIgnoringWhitespace Instance = new();

        public bool Equals(string? x, string? y)
            => JsonSerializer.Serialize(JsonDocument.Parse(x!).RootElement) == JsonSerializer.Serialize(JsonDocument.Parse(y!).RootElement);

        public int GetHashCode(string obj) => 0;
    }
}
