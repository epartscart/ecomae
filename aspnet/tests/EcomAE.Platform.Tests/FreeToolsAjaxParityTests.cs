using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/FreeTools/accounts_driver.py</c> replayed <c>accounts_steps.json</c> against PHP <c>ajax_epc_free_tools.php</c>
/// (<c>php -S</c>, throwaway MariaDB, no SMTP); ASP.NET must answer every step and leave the tables exactly as PHP did.
/// </summary>
public sealed class FreeToolsAjaxParityTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FreeTools");

    private static readonly (string Name, string Sql)[] StateSql =
    [
        ("accounts", "SELECT id, email, company, country, use_count, login_count, LEFT(pass_hash, 7), reset_code_hash IS NULL, "
                     + "reset_code_expires IS NULL, del_code_hash IS NULL, time_created > 1000 FROM epc_free_tool_accounts ORDER BY id"),
        ("saves", "SELECT id, account_id, tool, country, title, payload, time_created > 1000 FROM epc_free_tool_saves ORDER BY id"),
        ("settings", "SELECT name, val FROM epc_free_tool_settings ORDER BY name"),
        ("columns", "SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, IFNULL(COLUMN_DEFAULT, 'NULL') FROM information_schema.columns "
                    + "WHERE table_schema = DATABASE() AND TABLE_NAME LIKE 'epc\\_free\\_tool\\_%' ORDER BY TABLE_NAME, ORDINAL_POSITION"),
    ];

    [Fact]
    public async Task AccountFlows_MatchPhpGolden_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var golden = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "accounts_golden.json"))).RootElement;
        var steps = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "accounts_steps.json"))).RootElement;
        await using var db = await Throwaway.CreateAsync(password);
        var docRoot = Directory.CreateTempSubdirectory("ft-docroot-").FullName;
        var mailer = new RecordingMailer();
        try
        {
            await using var host = await StartAsync(db.ConnectionString, docRoot, mailer);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            var expected = golden.GetProperty("results").EnumerateArray().ToList();
            var i = 0;
            foreach (var step in steps.EnumerateArray())
            {
                var want = expected[i];
                if (step.TryGetProperty("dump", out _))
                {
                    Assert.Equal(JsonSerializer.Serialize(want.GetProperty("state")), JsonSerializer.Serialize(await db.StateAsync(tokens)));
                }
                else if (step.TryGetProperty("sql", out var sql))
                {
                    await db.ExecAsync(sql.GetString()!);
                }
                else
                {
                    var body = step.GetProperty("body").GetString()!;
                    foreach (var (name, value) in tokens)
                    {
                        body = body.Replace("{" + name + "U}", value.ToUpperInvariant(), StringComparison.Ordinal).Replace("{" + name + "}", value, StringComparison.Ordinal);
                    }

                    var method = step.TryGetProperty("method", out var m) ? m.GetString()! : "POST";
                    using var request = new HttpRequestMessage(new HttpMethod(method), FreeToolsAjaxEndpoint.Path.TrimStart('/'));
                    if (method != "GET")
                    {
                        request.Content = new StringContent(body, Encoding.UTF8);
                        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                            step.TryGetProperty("content_type", out var ct) ? ct.GetString()! : "application/json");
                    }

                    using var response = await client.SendAsync(request);
                    var text = await response.Content.ReadAsStringAsync();
                    Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
                    Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
                    if (step.TryGetProperty("capture", out var capture))
                    {
                        tokens[capture.GetString()!] = JsonDocument.Parse(text).RootElement.GetProperty("token").GetString()!;
                    }

                    foreach (var (name, value) in tokens)
                    {
                        text = text.Replace(value, "{" + name + "}", StringComparison.Ordinal);
                    }

                    text = Regex.Replace(text, "\"time_created\":[0-9]+", "\"time_created\":\"*\"");
                    Assert.True(
                        want.GetProperty("status").GetInt32() == (int)response.StatusCode && want.GetProperty("body").GetString() == text,
                        "step " + i + " " + body + "\n php: " + want.GetProperty("status").GetInt32() + " " + want.GetProperty("body").GetString()
                        + "\n net: " + (int)response.StatusCode + " " + text);
                }

                i++;
            }

            Assert.Equal(expected.Count, i);
            Assert.Matches("^[0-9a-f]{48}$", tokens["T1"]);
            Assert.Equal(JsonSerializer.Serialize(golden.GetProperty("state")), JsonSerializer.Serialize(await db.StateAsync(tokens)));
            Assert.Empty(mailer.Sent);
        }
        finally
        {
            Directory.Delete(docRoot, true);
        }
    }

    [Fact]
    public async Task Mails_UseTheBrandedShell_AndTheDeleteCodeFromTheMailDeletesTheAccount()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using var db = await Throwaway.CreateAsync(password);
        var docRoot = Directory.CreateTempSubdirectory("ft-docroot-").FullName;
        var mailer = new RecordingMailer();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(docRoot, "config.php"),
                "<?php\nclass DP_Config {\npublic $smtp_mode = '1';\npublic $smtp_host = 'smtp.example.net';\npublic $smtp_port = '587';\n"
                + "public $smtp_encryption = 'tls';\npublic $smtp_username = 'shop@example.com';\npublic $smtp_password = 'longpassword';\n"
                + "public $from_email = 'shop@example.com';\npublic $from_name = 'Shop';\n}\n");
            await using var host = await StartAsync(db.ConnectionString, docRoot, mailer);
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            var reg = await PostAsync(client, "{\"action\":\"register\",\"email\":\"a@example.com\",\"company\":\"O'Neil & <Sons>\",\"country\":\"sa\",\"password\":\"secret1\"}");
            var token = reg.GetProperty("token").GetString()!;
            var welcome = Assert.Single(mailer.Sent);
            Assert.Equal(("a@example.com", "Your ECOM AE free tools account"), (welcome.To, welcome.Subject));
            Assert.Equal(
                FreeToolsAccounts.MailShell(
                    "Welcome to ECOM AE free tools",
                    "<p>Hi O&#039;Neil &amp; &lt;Sons&gt;,</p><p>Your free account is ready. One login unlocks <strong>every</strong> free tool, and your results are saved so you can keep working on them daily.</p>"
                    + "<p><strong>Email:</strong> a@example.com<br><strong>Sign in:</strong> <a href=\"https://www.ecomae.com/platform/free-tools\" style=\"color:#0284c7\">ecomae.com/platform/free-tools</a> &rarr; <em>Log in</em></p>"
                    + "<p>Every tool localises automatically to your registered country (SA).</p>"),
                welcome.Html);
            Assert.StartsWith("<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#171717\">", welcome.Html, StringComparison.Ordinal);
            Assert.DoesNotContain("<", welcome.Text, StringComparison.Ordinal);

            var del = await PostAsync(client, "{\"action\":\"request_delete\",\"token\":\"" + token + "\"}");
            Assert.True(del.GetProperty("sent").GetBoolean());
            Assert.Equal("We emailed a confirmation cross-code to a@example.com. Enter it to confirm deletion.", del.GetProperty("message").GetString());
            var code = Regex.Match(mailer.Sent[^1].Html, "color:#dc2626\">([0-9]{6})</p>").Groups[1].Value;
            Assert.Equal(6, code.Length);
            var hash = await db.ScalarAsync("SELECT del_code_hash FROM epc_free_tool_accounts");
            Assert.StartsWith("$2y$10$", hash, StringComparison.Ordinal);

            var done = await PostAsync(client, "{\"action\":\"confirm_delete\",\"token\":\"" + token + "\",\"code\":\"" + code + "\"}");
            Assert.True(done.GetProperty("ok").GetBoolean());
            Assert.Equal("Your ECOM AE free tools data has been deleted", mailer.Sent[^1].Subject);
            Assert.Equal("0", await db.ScalarAsync("SELECT COUNT(*) FROM epc_free_tool_accounts"));
        }
        finally
        {
            Directory.Delete(docRoot, true);
        }
    }

    [Fact]
    public void PasswordHash_IsPhpCompatibleBcrypt()
    {
        var hash = FreeToolsAccounts.PasswordHash("secret1");
        Assert.StartsWith("$2y$10$", hash, StringComparison.Ordinal);
        Assert.True(FreeToolsAccounts.PasswordVerify("123456", "$2y$10$IY817hnmoOea4YYqcMZBjO8L0COoKVtLgjrMnB4CHjRUr/NJ0ycIm"));
        Assert.False(FreeToolsAccounts.PasswordVerify("123457", "$2y$10$IY817hnmoOea4YYqcMZBjO8L0COoKVtLgjrMnB4CHjRUr/NJ0ycIm"));
        Assert.False(FreeToolsAccounts.PasswordVerify("x", "not-a-hash"));
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string json)
    {
        using var response = await client.PostAsync(FreeToolsAjaxEndpoint.Path.TrimStart('/'), new StringContent(json, Encoding.UTF8, "application/json"));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

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
        FreeToolsAjaxEndpoint.Map(app);
        await app.StartAsync();
        return new StorefrontOrderPrintTests.ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
    }

    private sealed record SentMail(string To, string Subject, string Html, string Text);

    private sealed class RecordingMailer : IAuthOtpMailer
    {
        public List<SentMail> Sent { get; } = [];

        public Task<string?> SendAsync(SmtpEffectiveConfig config, string to, string subject, string html, string text, CancellationToken cancellationToken)
        {
            Sent.Add(new SentMail(to, subject, html, text));
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class Throwaway : IAsyncDisposable
    {
        private readonly string _admin;

        private Throwaway(string name, string admin, string cs)
        {
            Name = name;
            _admin = admin;
            ConnectionString = cs;
        }

        public string Name { get; }

        public string ConnectionString { get; }

        public static async Task<Throwaway> CreateAsync(string password)
        {
            var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
            var cs = "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";";
            await Exec(admin, "CREATE DATABASE `" + name + "`");
            return new Throwaway(name, admin, cs);
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

        /// <summary>The tables as <c>mysql -N -B</c> prints them (NULL, escaped backslashes), tokens masked.</summary>
        public async Task<Dictionary<string, List<List<string>>>> StateAsync(IReadOnlyDictionary<string, string> tokens)
        {
            var state = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            foreach (var (name, sql) in StateSql)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await using var reader = await command.ExecuteReaderAsync();
                var rows = new List<List<string>>();
                while (await reader.ReadAsync())
                {
                    var row = new List<string>();
                    for (var c = 0; c < reader.FieldCount; c++)
                    {
                        var cell = reader.IsDBNull(c)
                            ? "NULL"
                            : Convert.ToString(reader.GetValue(c), CultureInfo.InvariantCulture)!
                                .Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
                        foreach (var (token, value) in tokens)
                        {
                            cell = cell.Replace(value, "{" + token + "}", StringComparison.Ordinal);
                        }

                        row.Add(cell);
                    }

                    rows.Add(row);
                }

                state[name] = rows;
            }

            return state;
        }

        public async ValueTask DisposeAsync() => await Exec(_admin, "DROP DATABASE IF EXISTS `" + Name + "`");

        private static async Task Exec(string cs, string sql)
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }
}
