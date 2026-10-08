using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <see cref="StorefrontPhpAjax.UsersRegisterAsync"/>. <c>Fixtures/UsersRegister/goldens.json</c> is what the real
/// <c>content/users/register.php</c> left behind <c>php -S</c> for each case in <c>cases.json</c>: the redirect or page,
/// the account, profile, group, session and buyer rows, the notify calls and the KYC files, run by <c>harness.py</c> on a
/// throwaway schema seeded with <c>seed.sql</c>. Cases marked <c>ddl_rollback</c> refuse after the e-invoice schema DDL,
/// which commits implicitly in MySQL so PHP keeps the account; C# creates the schema first and rolls the account back.
/// The <c>simple_register</c> hand-over form escapes the posted values, which PHP echoes raw.
/// </summary>
public sealed class StorefrontUsersRegisterTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "UsersRegister");

    private static readonly Dictionary<string, string> DefaultCookies = new(StringComparer.Ordinal)
    {
        ["captcha"] = "2d429bb3ea4f162977dcc27e380578f4",
        ["users_agreement"] = "yes",
    };

    private static readonly Dictionary<string, string> DefaultConfig = new(StringComparer.Ordinal)
    {
        ["site_name"] = "Parts",
        ["domain_path"] = "http://shop.test/",
        ["backend_dir"] = "cp",
        ["secret_succession"] = "s3cr3t",
        ["from_email"] = "noreply@shop.test",
    };

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Outcome_RowsNotifiesAndKycFiles_MatchPhp_OnThrowawayDatabase_ThenDropped(string name)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var testCase = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == name).Clone();
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json")))![name]!;
        Assert.Null(golden["fatal"]);

        var post = Strings(testCase, "post") ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var cookies = Strings(testCase, "cookies") ?? DefaultCookies;
        var config = new Dictionary<string, string>(DefaultConfig, StringComparer.Ordinal);
        foreach (var (key, value) in Strings(testCase, "config") ?? [])
        {
            config[key] = value;
        }

        var files = new Dictionary<string, EpcRegistrationUpload>(StringComparer.Ordinal);
        if (testCase.TryGetProperty("files", out var filesJson))
        {
            foreach (var file in filesJson.EnumerateObject())
            {
                var bytes = new byte[file.Value[1].GetInt32()];
                Array.Fill(bytes, (byte)'x');
                files[file.Name] = new EpcRegistrationUpload(file.Value[0].GetString()!, bytes);
            }
        }

        var answers = new Dictionary<string, (bool Status, bool Contact)>(StringComparer.Ordinal);
        if (testCase.TryGetProperty("notify", out var notifyJson))
        {
            foreach (var entry in notifyJson.EnumerateObject())
            {
                answers[entry.Name] = (entry.Value[0].GetBoolean(), entry.Value[1].GetBoolean());
            }
        }

        var ddlRollback = testCase.TryGetProperty("ddl_rollback", out var rollbackJson) && rollbackJson.GetBoolean();
        var webRoot = Directory.CreateTempSubdirectory("epc-register-").FullName;
        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "` DEFAULT CHARACTER SET utf8mb4");
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            await ExecAsync(cs, await File.ReadAllTextAsync(Path.Combine(FixtureDir, "seed.sql")));
            if (testCase.TryGetProperty("setup", out var setup))
            {
                foreach (var statement in setup.EnumerateArray())
                {
                    await ExecAsync(cs, statement.GetString()!);
                }
            }

            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            var dispatcher = new RecordingDispatcher(answers);
            var outcome = await StorefrontPhpAjax.UsersRegisterAsync(
                connection,
                async ct =>
                {
                    var separate = new MySqlConnection(cs);
                    await separate.OpenAsync(ct);
                    return separate;
                },
                dispatcher,
                config,
                new StorefrontPhpAjax.UsersRegisterRequest(post, files, cookies, "127.0.0.1", "EpcHarness/1.0", "/en", webRoot),
                CancellationToken.None);

            var phone = post.GetValueOrDefault("reg_contact_type") == "phone";
            var simple = post.ContainsKey("simple_register");
            AssertSame(golden["location"], outcome.Location, "location");
            AssertSame(golden["notify"], dispatcher.Calls, "notify");
            var users = await RowsAsync(connection, "SELECT user_id, email, phone, reg_variant, password, email_code, phone_code, email_code_expired, phone_code_expired, time_registered, unlocked, email_confirmed, phone_confirmed, ip_address FROM users WHERE user_id > 7 ORDER BY user_id");
            if (ddlRollback)
            {
                Assert.Empty(users!);
                return;
            }

            var html = golden["html"]!.GetValue<string>();
            if (simple)
            {
                foreach (var key in new[] { "reg_contact", "reg_contact_type", "code", "csrf_guard_key" })
                {
                    var raw = post.GetValueOrDefault(key) ?? string.Empty;
                    html = html.Replace("value=\"" + raw + "\">", "value=\"" + WebUtility.HtmlEncode(raw) + "\">", StringComparison.Ordinal);
                }
            }

            AssertSame(html, outcome.Html, "html");
            foreach (var row in users!.Select(r => r!.AsArray()))
            {
                row[9] = "T";
                row[7] = row[7]!.GetValue<string>() == "0" ? "0" : "T";
                row[8] = row[8]!.GetValue<string>() == "0" ? "0" : "T";
                if (phone && row[6]!.GetValue<string>().Length > 0)
                {
                    row[6] = "R";
                }

                if (simple)
                {
                    row[4] = "R";
                }
            }

            AssertSame(golden["users"], users, "users");
            var profiles = await RowsAsync(connection, "SELECT user_id, data_key, data_value FROM users_profiles ORDER BY user_id, id");
            foreach (var row in profiles!.Select(r => r!.AsArray()))
            {
                if (row[1]!.GetValue<string>() is "epc_trade_registered_at" or "epc_trade_approved_at")
                {
                    row[2] = "T";
                }
                else if (row[2]?.GetValue<string>() is { } value)
                {
                    row[2] = Stamp(value);
                }
            }

            AssertSame(golden["profiles"], profiles, "users_profiles");
            AssertSame(golden["binds"], await RowsAsync(connection, "SELECT user_id, group_id FROM users_groups_bind ORDER BY user_id, id"), "users_groups_bind");
            AssertSame(golden["sessions"], await RowsAsync(connection, "SELECT session, `2fa_attempts` FROM sessions ORDER BY id"), "sessions");
            if (golden["buyers"] is not null)
            {
                AssertSame(golden["buyers"], await RowsAsync(connection, "SELECT user_id, buyer_name, trn, tin, legal_reg_no, country_code, emirate, city, email, phone, peppol_endpoint, buyer_onboarded FROM epc_einvoice_buyer_profiles ORDER BY user_id"), "buyers");
            }

            var stored = new JsonArray();
            var kyc = Path.Combine(webRoot, "content", "files", "kyc");
            if (Directory.Exists(kyc))
            {
                foreach (var path in Directory.GetFiles(kyc, "*", SearchOption.AllDirectories)
                    .Select(p => (Rel: Stamp(p[webRoot.Length..].Replace('\\', '/')), Size: new FileInfo(p).Length))
                    .OrderBy(f => f.Rel, StringComparer.Ordinal))
                {
                    stored.Add(new JsonArray(path.Rel, path.Size));
                }
            }

            AssertSame(golden["files"], stored, "kyc files");
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
            Directory.Delete(webRoot, true);
        }
    }

    /// <summary>PHP refuses before its transaction starts, so the <c>rollBack()</c> in its catch dies; C# redirects with string 4740.</summary>
    [Fact]
    public async Task SignedInUser_IsRefusedWith4740_WithoutTouchingTheAccountTables()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "` DEFAULT CHARACTER SET utf8mb4");
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        try
        {
            await ExecAsync(cs, await File.ReadAllTextAsync(Path.Combine(FixtureDir, "seed.sql")));
            await ExecAsync(cs, "INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES ('4740', 'en', 'You are already signed in')");
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            var dispatcher = new RecordingDispatcher([]);
            var outcome = await StorefrontPhpAjax.UsersRegisterAsync(
                connection,
                _ => throw new InvalidOperationException("no send expected"),
                dispatcher,
                DefaultConfig,
                new StorefrontPhpAjax.UsersRegisterRequest(
                    new Dictionary<string, string> { ["reg_contact"] = "x@example.test", ["reg_contact_type"] = "email", ["reg_variant"] = "1", ["capcha_input"] = "k7n4" },
                    new Dictionary<string, EpcRegistrationUpload>(),
                    new Dictionary<string, string>(DefaultCookies) { ["session"] = "s7", ["u_id"] = "7" },
                    "127.0.0.1",
                    "EpcHarness/1.0",
                    "/en",
                    Path.GetTempPath()),
                CancellationToken.None);

            Assert.Equal("/en/?error_message=You+are+already+signed+in", outcome.Location);
            Assert.Empty(dispatcher.Calls);
            Assert.Equal("0", (await RowsAsync(connection, "SELECT COUNT(*) FROM users WHERE user_id > 7"))![0]![0]!.GetValue<string>());
            Assert.Null(await RowsAsync(connection, "SELECT COUNT(*) FROM epc_einvoice_buyer_profiles"));
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    [Fact]
    public async Task Endpoint_RedirectsGets_RefusesAndRegistersPosts_AndCarriesThePageToTheResultPage()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "` DEFAULT CHARACTER SET utf8mb4");
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        var docRoot = Directory.CreateTempSubdirectory("epc-register-host-").FullName;
        await File.WriteAllTextAsync(Path.Combine(docRoot, "config.php"), """
            <?php
            class DP_Config {
            public $site_name = 'Parts';
            public $domain_path = 'http://shop.test/';
            public $backend_dir = 'cp';
            public $secret_succession = 's3cr3t';
            public $from_email = 'noreply@shop.test';
            }
            """);
        try
        {
            await ExecAsync(cs, await File.ReadAllTextAsync(Path.Combine(FixtureDir, "seed.sql")));
            var dispatcher = new RecordingDispatcher([]);
            using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
            builder.Services.AddSingleton<EcomAE.Platform.Data.ITenantDbConnectionFactory>(new FixedConnections(cs));
            builder.Services.AddSingleton<IStorefrontNotifyDispatcher>(dispatcher);
            builder.Services.Configure<EcomAE.Platform.Configuration.PhpReferenceOptions>(options => options.PhpDocRoot = docRoot);
            builder.Services.AddDataProtection();
            await using var app = builder.Build();
            app.UseRouting();
            StorefrontUsersRegisterEndpoints.Map(app);
            await app.StartAsync();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/") };

            foreach (var path in new[] { "/users/register", "/en/users/register" })
            {
                using var get = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
                Assert.Equal("/en/users/registration", get.Headers.Location!.OriginalString);
            }

            var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement;
            JsonElement Case(string caseName) => cases.EnumerateArray().First(c => c.GetProperty("name").GetString() == caseName);

            using (var refused = await PostAsync(client, "/en/users/register", Case("bad_captcha")))
            {
                Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
                Assert.Equal("/en/?error_message=Wrong+captcha", refused.Headers.Location!.OriginalString);
            }

            using (var registered = await PostAsync(client, "/en/users/register", Case("email_retail_ok")))
            {
                Assert.Equal(HttpStatusCode.Redirect, registered.StatusCode);
                var location = registered.Headers.Location!.OriginalString;
                Assert.StartsWith("/en" + StorefrontUsersRegisterEndpoints.ResultPath + "?r=", location, StringComparison.Ordinal);
                var token = Uri.UnescapeDataString(location[(location.IndexOf("?r=", StringComparison.Ordinal) + 3)..]);
                var provider = app.Services.GetRequiredService<IDataProtectionProvider>();
                var html = StorefrontUsersRegisterEndpoints.ReadResult(provider, token);
                Assert.Equal(JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json")))!["email_retail_ok"]!["html"]!.GetValue<string>(), html);
                Assert.Null(StorefrontUsersRegisterEndpoints.ReadResult(provider, token[..^2] + "xx"));
            }

            using (var wholesale = await PostAsync(client, "/users/register", Case("phone_wholesale_pending")))
            {
                Assert.Equal(HttpStatusCode.Redirect, wholesale.StatusCode);
                Assert.StartsWith(StorefrontUsersRegisterEndpoints.ResultPath + "?r=", wholesale.Headers.Location!.OriginalString, StringComparison.Ordinal);
            }

            await using var check = new MySqlConnection(cs);
            await check.OpenAsync();
            var users = await RowsAsync(check, "SELECT user_id, email, phone FROM users WHERE user_id > 7 ORDER BY user_id");
            Assert.Equal("[[\"8\",\"new@example.test\",\"\"],[\"9\",\"\",\"\\u002B971 50 123 4567\"]]", users!.ToJsonString());
            Assert.Equal(["reg_email_confirm", "reg_notify_admin", "reg_phone_confirm", "reg_notify_admin"], dispatcher.Calls.Select(c => c!["name"]!.GetValue<string>()));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(docRoot, "content", "files", "kyc", "9")).Length);
            await app.StopAsync();
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
            Directory.Delete(docRoot, true);
        }
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, JsonElement testCase)
    {
        var content = new MultipartFormDataContent();
        foreach (var field in testCase.GetProperty("post").EnumerateObject())
        {
            content.Add(new StringContent(field.Value.GetString()!), field.Name);
        }

        if (testCase.TryGetProperty("files", out var files))
        {
            foreach (var file in files.EnumerateObject())
            {
                var bytes = new byte[file.Value[1].GetInt32()];
                Array.Fill(bytes, (byte)'x');
                content.Add(new ByteArrayContent(bytes), file.Name, file.Value[0].GetString()!);
            }
        }

        var cookies = Strings(testCase, "cookies") ?? DefaultCookies;
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(c => c.Key + "=" + Uri.EscapeDataString(c.Value))));
        return await client.SendAsync(request);
    }

    private sealed class FixedConnections(string cs) : EcomAE.Platform.Data.ITenantDbConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default) => OpenAsync();

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default) => OpenAsync();

        public Task<DbConnection> OpenForTenantAsync(EcomAE.Platform.Services.TenantContext? tenant, CancellationToken cancellationToken = default) => OpenAsync();

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default) => OpenAsync();

        private async Task<DbConnection> OpenAsync()
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            return connection;
        }
    }

    private static Dictionary<string, string>? Strings(JsonElement testCase, string key)
        => testCase.TryGetProperty(key, out var json)
            ? json.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal)
            : null;

    private static string Stamp(string value) => Regex.Replace(value, "_\\d{8}_\\d{6}\\.", "_D.");

    private static void AssertSame(JsonNode? php, JsonNode? actual, string what)
        => Assert.True(JsonNode.DeepEquals(php, actual), $"{what}: PHP {php?.ToJsonString()} vs {actual?.ToJsonString()}");

    private static async Task<JsonArray?> RowsAsync(MySqlConnection connection, string sql)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new JsonArray();
            while (await reader.ReadAsync())
            {
                var row = new JsonArray();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i) is bool flag ? (flag ? "1" : "0") : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture));
                }

                rows.Add(row);
            }

            return rows;
        }
        catch (MySqlException)
        {
            return null;
        }
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>The harness <c>send_notify</c> stub: records each call and answers with the case's [status, contact status].</summary>
    private sealed class RecordingDispatcher(Dictionary<string, (bool Status, bool Contact)> answers) : IStorefrontNotifyDispatcher
    {
        public JsonArray Calls { get; } = [];

        public Task<StorefrontNotifyAnswer> SendAsync(
            DbConnection connection,
            string name,
            IReadOnlyDictionary<string, string> vars,
            IReadOnlyList<StorefrontNotifyPerson> persons,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string>? statusRef = null)
        {
            var recordedVars = new JsonObject();
            foreach (var (key, value) in vars.OrderBy(v => v.Key, StringComparer.Ordinal))
            {
                recordedVars[key] = key == "phone_confirm_code" ? "R" : Regex.Replace(value, @"Time: \d{2}\.\d{2}\.\d{4} \d{2}:\d{2}", "Time: T");
            }

            var recordedPersons = new JsonArray();
            foreach (var person in persons)
            {
                recordedPersons.Add(person.Type == StorefrontNotifyPerson.UserIdType
                    ? new JsonArray("user_id", person.UserId.ToString(CultureInfo.InvariantCulture))
                    : new JsonArray("direct", person.Email, person.Phone));
            }

            Calls.Add(new JsonObject { ["name"] = name, ["vars"] = recordedVars, ["persons"] = recordedPersons, ["wait"] = name != "reg_notify_admin" });
            var (status, contact) = answers.TryGetValue(name, out var answer) ? answer : (true, true);
            var channel = new StorefrontNotifyChannel(contact, contact, string.Empty);
            return Task.FromResult(new StorefrontNotifyAnswer(
                status,
                string.Empty,
                [new StorefrontNotifyPersonResult(persons.Count > 0 ? persons[0] : StorefrontNotifyPerson.Direct(string.Empty), contact, contact, string.Empty) { Sms = channel }]));
        }

        public Task<StorefrontNotifyOutcome> SendDirectEmailAsync(DbConnection connection, string name, IReadOnlyDictionary<string, string> vars, string email, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("register.php sends no direct e-mail");
    }
}
