using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <see cref="EpcRegistrationEnhanced"/>, <see cref="EpcUaeCustomerVat"/> and <see cref="EpcEinvoiceBuyer"/>.
/// <c>Fixtures/RegistrationEnhanced/goldens.json</c> is what the real <c>epc_registration_enhanced.php</c> returned behind
/// <c>php -S</c> for the posted fields and uploaded documents of each case in <c>cases.json</c>, and the rows and KYC files it
/// left, run by <c>harness.py</c> on a throwaway schema seeded with <c>seed.sql</c>.
/// </summary>
public sealed class StorefrontRegistrationEnhancedTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "RegistrationEnhanced");

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Results_RowsAndKycFiles_MatchPhp_OnThrowawayDatabase_ThenDropped(string name)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var testCase = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == name).Clone();
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json")))![name]!;

        var post = new Dictionary<string, string>(StringComparer.Ordinal);
        if (testCase.TryGetProperty("post", out var postJson))
        {
            foreach (var field in postJson.EnumerateObject())
            {
                post[field.Name] = field.Value.GetString()!;
            }
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

        var webRoot = Directory.CreateTempSubdirectory("epc-reg-").FullName;
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
            var helpers = new JsonArray(
                EpcRegistrationEnhanced.CustomerType(post), EpcRegistrationEnhanced.CountryCode(post), EpcRegistrationEnhanced.TrnMode(post),
                EpcRegistrationEnhanced.UaeRequested(post), EpcRegistrationEnhanced.ExtractTrn(post));
            AssertSame(golden["helpers"], helpers, "helpers");

            var index = 0;
            foreach (var op in testCase.GetProperty("ops").EnumerateArray())
            {
                JsonNode? result;
                try
                {
                    result = await RunAsync(connection, webRoot, post, files, op.EnumerateArray().ToArray());
                }
                catch (Exception e) when (e is EpcRegistrationException or InvalidOperationException)
                {
                    result = new JsonObject { ["error"] = e.Message };
                }

                AssertSame(golden["results"]![index], result, $"op {index} {op}");
                index++;
            }

            var profiles = await RowsAsync(connection, "SELECT user_id, data_key, data_value FROM users_profiles ORDER BY user_id, data_key, id");
            foreach (var row in profiles?.Select(r => r!.AsArray()) ?? [])
            {
                if (row[2]?.GetValue<string>() is { } value)
                {
                    row[2] = Stamp(value);
                }
            }

            AssertSame(golden["profiles"], profiles, "users_profiles");
            var buyers = await RowsAsync(connection, "SELECT user_id, buyer_name, trn, tin, legal_reg_no, legal_reg_type, authority_name, address_line1, city, emirate, country_code, phone, email, electronic_id, peppol_endpoint, buyer_onboarded, time_updated FROM epc_einvoice_buyer_profiles ORDER BY user_id");
            foreach (var row in buyers?.Select(r => r!.AsArray()) ?? [])
            {
                row[16] = "T";
            }

            AssertSame(golden["buyers"], buyers, "epc_einvoice_buyer_profiles");
            AssertSame(golden["settings"], await RowsAsync(connection, "SELECT setting_key, setting_value FROM epc_einvoice_settings ORDER BY setting_key"), "epc_einvoice_settings");

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

    private static string Stamp(string value) => Regex.Replace(value, "_\\d{8}_\\d{6}\\.", "_D.");

    private static void AssertSame(JsonNode? php, JsonNode? actual, string what)
        => Assert.True(JsonNode.DeepEquals(php, actual), $"{what}: PHP {php?.ToJsonString()} vs {actual?.ToJsonString()}");

    private static async Task<JsonNode?> RunAsync(
        MySqlConnection connection,
        string webRoot,
        IReadOnlyDictionary<string, string> post,
        IReadOnlyDictionary<string, EpcRegistrationUpload> files,
        JsonElement[] a)
    {
        var ct = CancellationToken.None;
        long L(int i) => a[i].GetInt64();
        string S(int i) => a[i].GetString()!;
        switch (S(0))
        {
            case "validate":
                EpcRegistrationEnhanced.ValidateEnhancedFields(post, S(1), files);
                return "ok";
            case "validate_uae":
                EpcRegistrationEnhanced.ValidateUaeFields(post);
                return "ok";
            case "save":
                await EpcRegistrationEnhanced.SaveEnhancedProfileAsync(connection, null, webRoot, L(1), post, files, S(2), S(3), ct);
                return "ok";
            case "save_uae":
                await EpcRegistrationEnhanced.SaveUaeBuyerProfileAsync(connection, null, webRoot, L(1), post, files, S(2), S(3), ct);
                return "ok";
            case "vat_sync":
                return await EpcUaeCustomerVat.SyncAsync(connection, null, L(1), ct);
            case "buyer":
                var profile = await EpcEinvoiceBuyer.BuyerProfileAsync(connection, null, L(1), ct);
                if (profile.Count == 0)
                {
                    return new JsonArray();
                }

                var buyer = new JsonObject();
                foreach (var (key, value) in profile.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    buyer[key] = key == "time_updated" && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) && t > 0 ? "T" : value;
                }

                return buyer;
            default:
                return "unknown op";
        }
    }

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
}
