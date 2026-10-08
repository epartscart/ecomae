using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
/// E-mail code sign-in. <c>Fixtures/AuthOtpVerifyLogin/goldens.json</c> is what the real <c>epc-auth-verify-code.php</c>
/// (and its content alias) answered for each case of <c>cases.json</c>, run by <c>harness.py</c> on a throwaway schema
/// seeded with <c>seed.sql</c>: status, headers, body, the unpacked handoff, the cookies and the rows left behind.
/// Cases marked <c>deviation</c> or <c>aspnet_binds</c> record where ASP.NET refuses to grant CP access that PHP grants.
/// </summary>
public sealed class AuthOtpVerifyLoginTests
{
    private const string DeployToken = "epartscart-deploy-2026";
    private const string Secret = "verify-test-secret";

    private static readonly Regex Handoff = new("p=([A-Za-z0-9_\\-%]+)&s=([0-9a-f]+)", RegexOptions.CultureInvariant);

    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "AuthOtpVerifyLogin");

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Answer_cookies_and_rows_match_php_on_a_throwaway_database(string name)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EPC_DEPLOY_TOKEN")))
        {
            return;
        }

        var testCase = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == name).Clone();
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name).Clone();

        await using var db = await Throwaway.CreateAsync(password);
        var (status, type, cache, body, cookies) = await RunAsync(db, password, testCase);
        var actual = await db.SnapshotAsync();
        var handoff = Handoff.Matches(body).Select(m => Unpack(m.Groups[1].Value)).ToList();

        if (testCase.TryGetProperty("deviation", out _))
        {
            Assert.Equal(400, status);
            Assert.Equal(AuthEmailOtp.Fail(AuthOtpVerifyLogin.MsgCpRefused), body);
            Assert.Empty(cookies);
            Assert.Equal("[]", actual["sessions"]);
            Assert.Equal(0, golden.GetProperty("handoff").GetArrayLength());
            Assert.True(golden.GetProperty("status").GetInt32() == 200, "PHP signs this user in; ASP.NET refuses on purpose.");
            if (!testCase.TryGetProperty("setup", out _))
            {
                Assert.Equal("[]", actual["users"]);
                Assert.Equal("[]", actual["binds"]);
            }
            else
            {
                Assert.DoesNotContain("\"3\"]", actual["binds"], StringComparison.Ordinal);
            }

            return;
        }

        Assert.Equal(golden.GetProperty("status").GetInt32(), status);
        Assert.Equal(golden.GetProperty("type").GetString(), type);
        Assert.Equal(golden.GetProperty("cache").GetString(), cache);
        Assert.Equal(golden.GetProperty("body").GetString(), Handoff.Replace(body, "p=P&s=S"));
        Assert.Equal(Canonical(golden.GetProperty("handoff").GetRawText()), Canonical(JsonSerializer.Serialize(handoff)));
        Assert.Equal(Canonical(golden.GetProperty("cookies").GetRawText()), Canonical(JsonSerializer.Serialize(cookies)));
        Assert.Equal(golden.GetProperty("otp_rows").GetInt32(), int.Parse(actual["otp_rows"], CultureInfo.InvariantCulture));
        foreach (var table in new[] { "users", "profiles", "sessions", "carts" })
        {
            Assert.True(Canonical(golden.GetProperty(table).GetRawText()) == Canonical(actual[table]), table + ": " + actual[table]);
        }

        var binds = testCase.TryGetProperty("aspnet_binds", out var own) ? own.GetRawText() : golden.GetProperty("binds").GetRawText();
        Assert.Equal(Canonical(binds), Canonical(actual["binds"]));

        foreach (Match match in Handoff.Matches(body))
        {
            var unpacked = OAuthCallback.UnpackHandoff(
                Uri.UnescapeDataString(match.Groups[1].Value),
                match.Groups[2].Value,
                Secret,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Assert.NotNull(unpacked);
        }
    }

    [Fact]
    public async Task Super_cp_signs_in_an_existing_operator_and_refuses_a_new_email()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EPC_DEPLOY_TOKEN")))
        {
            return;
        }

        await using var db = await Throwaway.CreateAsync(password);
        var ok = JsonDocument.Parse("""
            {"name": "x", "host": "cp.ecomae.com", "https": true, "path": "/epc-auth-verify-code.php",
             "setup": ["INSERT INTO users (user_id, email, email_confirmed, unlocked) VALUES (54, 'ops@example.com', 1, 1)", "INSERT INTO users_groups_bind VALUES (54, 3)"],
             "otp": {"email": "ops@example.com", "code": "121212", "tenant_key": ""},
             "json": {"email": "ops@example.com", "code": "121212", "context": "cp"}}
            """).RootElement;
        var (status, _, _, body, cookies) = await RunAsync(db, password, ok);
        Assert.Equal(200, status);
        Assert.Equal(AuthOtpVerifyLogin.Ok("https://cp.ecomae.com/cp/control"), body);
        Assert.Equal(["admin_session=S32", "admin_u_id=54", "epc_platform_erp=deleted"], cookies);
        Assert.Equal("""[["S32","54","1","1","email","40","0"]]""", (await db.SnapshotAsync())["sessions"]);

        await using var db2 = await Throwaway.CreateAsync(password);
        var stranger = JsonDocument.Parse("""
            {"name": "y", "host": "cp.ecomae.com", "https": true, "path": "/epc-auth-verify-code.php",
             "otp": {"email": "stranger@example.com", "code": "131313", "tenant_key": ""},
             "json": {"email": "stranger@example.com", "code": "131313", "context": "cp"}}
            """).RootElement;
        var refused = await RunAsync(db2, password, stranger);
        Assert.Equal((400, AuthEmailOtp.Fail(AuthOtpVerifyLogin.MsgCpRefused)), (refused.Status, refused.Body));
        Assert.Equal("[]", (await db2.SnapshotAsync())["users"]);
    }

    [Fact]
    public void Redirects_and_handoff_follow_php()
    {
        Assert.Equal("https://acme.example/en/cart", AuthOtpVerifyLogin.StorefrontRedirect("acme.example", "/en/", " /en/cart "));
        Assert.Equal("https://acme.example/en/", AuthOtpVerifyLogin.StorefrontRedirect("acme.example", "/en/", "https://evil.example/"));
        Assert.Equal("https://acme.example/en/", AuthOtpVerifyLogin.StorefrontRedirect("acme.example", "/en/", "/a//b"));
        Assert.Equal("/en/x", AuthOtpVerifyLogin.StorefrontRedirect("", "/en/", "/en/x"));
        Assert.Equal("https://acme.example/cp/control", AuthOtpVerifyLogin.CpRedirect("tenant", "acme", "acme.example", "/cp/control", false));
        Assert.Equal("/cp/control", AuthOtpVerifyLogin.CpRedirect("tenant_local", "", "", "/cp/control", false));
        Assert.Equal("/cp/demo/d1/shop/orders", AuthOtpVerifyLogin.CpRedirect("demo", "d1", "d1.example", "/cp/demo/d1/shop/orders", false));
        Assert.Equal("https://www.ecomae.com/cp/control", AuthOtpVerifyLogin.CpRedirect("super", "", "", "/cp/control", false));
        Assert.Equal("/cp/demo/d1/shop/finance/erp", AuthOtpVerifyLogin.DemoCpReturnPath("D1", true));
        Assert.True(AuthOtpVerifyLogin.DemoRowIsErpOnly("{\"demo_erp_only\":\"1\"}", "auto_parts"));
        Assert.True(AuthOtpVerifyLogin.DemoRowIsErpOnly(null, "erp_standalone"));
        Assert.False(AuthOtpVerifyLogin.DemoRowIsErpOnly("{\"demo_erp_only\":0}", "auto_parts"));
        Assert.False(AuthOtpVerifyLogin.ShouldHandoff("www.Acme.example", "acme.example"));
        Assert.True(AuthOtpVerifyLogin.ShouldHandoff("localhost", "acme.example"));
        Assert.False(AuthOtpVerifyLogin.ShouldHandoff("localhost", ""));

        var url = AuthOtpVerifyLogin.HandoffUrl(7, "abc", "acme.example", "/en/", "acme", "storefront", 2_000_000_000, Secret);
        var match = Handoff.Match(url);
        var unpacked = OAuthCallback.UnpackHandoff(Uri.UnescapeDataString(match.Groups[1].Value), match.Groups[2].Value, Secret, 1_900_000_000);
        Assert.Equal(new OAuthHandoffPayload(7, "abc", "storefront", "/en/", "acme.example"), unpacked);
        Assert.Null(OAuthCallback.UnpackHandoff(Uri.UnescapeDataString(match.Groups[1].Value), match.Groups[2].Value, "other", 1_900_000_000));
        Assert.StartsWith("https://acme.example/epc-auth-handoff.php?p=", url, StringComparison.Ordinal);
    }

    private static async Task<(int Status, string? Type, string? Cache, string Body, List<string> Cookies)> RunAsync(Throwaway db, string password, JsonElement testCase)
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

        if (testCase.TryGetProperty("otp", out var otp))
        {
            var ttl = otp.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 300;
            await db.ExecAsync(
                "INSERT INTO epc_auth_otp_requests (email, code_hash, tenant_key, expires_at, ip_address, created_at) VALUES ('"
                + otp.GetProperty("email").GetString() + "','" + AuthEmailOtp.CodeHash(otp.GetProperty("code").GetString()!, DeployToken) + "','"
                + otp.GetProperty("tenant_key").GetString() + "',UNIX_TIMESTAMP()+" + ttl.ToString(CultureInfo.InvariantCulture) + ",'10.0.0.9',UNIX_TIMESTAMP()-10)");
        }

        await using var host = await StartAsync(db.ConnectionString);
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = host.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Post, testCase.GetProperty("path").GetString()!.TrimStart('/'));
        request.Headers.Host = testCase.TryGetProperty("host", out var hostEl) ? hostEl.GetString() : "localhost";
        request.Headers.UserAgent.ParseAdd("golden-agent");
        if (testCase.TryGetProperty("https", out var https) && https.GetBoolean())
        {
            request.Headers.Add("X-Forwarded-Proto", "https");
        }

        if (testCase.TryGetProperty("cookie", out var cookie))
        {
            request.Headers.Add("Cookie", cookie.GetString());
        }

        request.Content = testCase.TryGetProperty("form", out var form)
            ? new FormUrlEncodedContent(form.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!))
            : new StringContent(testCase.GetProperty("json").GetRawText(), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        var cookies = (response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [])
            .Select(c => Regex.Replace(c.Split(';', 2)[0], "=[0-9a-f]{32}$", "=S32"))
            .Order(StringComparer.Ordinal)
            .ToList();
        return ((int)response.StatusCode, response.Content.Headers.ContentType?.ToString(), response.Headers.CacheControl?.ToString(), await response.Content.ReadAsStringAsync(), cookies);
    }

    private static Dictionary<string, object?> Unpack(string encoded)
    {
        var raw = Uri.UnescapeDataString(encoded);
        using var doc = JsonDocument.Parse(OAuthStart.Base64UrlDecode(raw));
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            data[p.Name] = p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetInt64() : p.Value.GetString();
        }

        data["sess"] = data["sess"] is string s && Regex.IsMatch(s, "^[0-9a-f]{32}$") ? "S32" : data["sess"];
        var left = (long)data["exp"]! - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        data["exp"] = left is >= 110 and <= 121 ? "E120" : data["exp"];
        return data;
    }

    private static string Canonical(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    private static async Task<StorefrontOrderPrintTests.ProbeHost> StartAsync(string cs)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        builder.Services.AddSingleton<ITenantDbConnectionFactory>(new StorefrontOrderPrintTests.FixedConnections(cs));
        builder.Services.Configure<EcomAeOptions>(o => o.SecretSuccession = Secret);
        var app = builder.Build();
        app.UseRouting();
        AuthOtpVerifyLogin.Map(app);
        await app.StartAsync();
        return new StorefrontOrderPrintTests.ProbeHost(app, new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/"));
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
            var cs = "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
            await Exec(admin, "CREATE DATABASE `" + name + "`");
            return new Throwaway(name, admin, cs);
        }

        public Task ExecAsync(string sql) => Exec(ConnectionString, sql);

        public async Task<Dictionary<string, string>> SnapshotAsync()
        {
            var queries = new Dictionary<string, string>
            {
                ["users"] = "SELECT user_id, email, email_confirmed, unlocked, reg_variant, time_registered > 0, time_last_visit > 0, admin_created FROM users ORDER BY user_id",
                ["profiles"] = "SELECT user_id, data_key, IF(data_value REGEXP '^[0-9]{9,}$', 'T', IFNULL(data_value, 'NULL')) FROM users_profiles ORDER BY user_id, data_key",
                ["binds"] = "SELECT user_id, group_id FROM users_groups_bind ORDER BY user_id, group_id",
                ["sessions"] = "SELECT IF(session REGEXP '^[0-9a-f]{32}$', 'S32', session), user_id, time > 1, type, contact_type, LENGTH(csrf_guard_key), last_activiti_time > 0 FROM sessions ORDER BY id",
                ["carts"] = "SELECT id, user_id, session_id FROM shop_carts ORDER BY id",
            };
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, sql) in queries)
            {
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var rows = new List<string[]>();
                while (await reader.ReadAsync())
                {
                    rows.Add(Enumerable.Range(0, reader.FieldCount)
                        .Select(i => reader.GetValue(i) is bool flag ? (flag ? "1" : "0") : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "")
                        .ToArray());
                }

                result[key] = JsonSerializer.Serialize(rows);
            }

            await using var count = new MySqlCommand("SELECT COUNT(*) FROM epc_auth_otp_requests", connection);
            result["otp_rows"] = Convert.ToString(await count.ExecuteScalarAsync(), CultureInfo.InvariantCulture)!;
            return result;
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
}
