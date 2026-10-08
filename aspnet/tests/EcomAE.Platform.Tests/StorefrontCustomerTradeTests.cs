using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <see cref="EpcCustomerTrade"/> and <see cref="EpcCurrency"/>. <c>Fixtures/CustomerTrade/goldens.json</c> is what the real
/// <c>epc_customer_trade.php</c> and <c>epc_currency.php</c> returned for the operations of each case in <c>cases.json</c>,
/// and the rows they left, run by <c>harness.py</c> on a throwaway schema seeded with <c>seed.sql</c>.
/// </summary>
public sealed class StorefrontCustomerTradeTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CustomerTrade");

    public static IEnumerable<object[]> Cases()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Results_AndRows_MatchPhp_OnThrowawayDatabase_ThenDropped(string name)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var testCase = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json")))
            .RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == name).Clone();
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json")))![name]!;

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
            var results = new JsonArray();
            var index = 0;
            foreach (var op in testCase.GetProperty("ops").EnumerateArray())
            {
                results.Add(await RunAsync(connection, op.EnumerateArray().ToArray()));
                Assert.True(JsonNode.DeepEquals(golden["results"]![index], results[index]), $"op {index} {op}: PHP {golden["results"]![index]?.ToJsonString()} vs {results[index]?.ToJsonString()}");
                index++;
            }

            var profiles = await RowsAsync(connection, "SELECT user_id, data_key, data_value FROM users_profiles ORDER BY user_id, data_key, id");
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var row in profiles?.Select(r => r!.AsArray()) ?? [])
            {
                var value = row[2]?.GetValue<string>();
                if (row[1]!.GetValue<string>().EndsWith("_at", StringComparison.Ordinal) && value is { Length: > 0 } && value.All(char.IsAsciiDigit)
                    && Math.Abs(long.Parse(value, CultureInfo.InvariantCulture) - now) < 600)
                {
                    row[2] = "T";
                }
            }

            AssertSame(golden["profiles"], profiles, "users_profiles");
            AssertSame(golden["binds"], await RowsAsync(connection, "SELECT user_id, group_id FROM users_groups_bind ORDER BY user_id, group_id"), "users_groups_bind");
            AssertSame(golden["currencies"], await RowsAsync(connection, "SELECT iso_code, iso_name, caption_short, sign, rate, available, `order` FROM shop_currencies ORDER BY iso_code, id"), "shop_currencies");
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    [Fact]
    public async Task CheckoutCreate_RefusesUnapprovedCustomers_WithPhpBlockMessage()
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
            await ExecAsync(cs, """
                INSERT INTO users_profiles (user_id, data_key, data_value) VALUES
                (3, 'epc_trade_approval_status', 'pending'),
                (4, 'epc_trade_approval_status', 'rejected'), (4, 'epc_trade_rejection_note', ' No licence '),
                (5, 'epc_trade_approval_status', 'Pending')
                """);
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();

            async Task<StorefrontPhpAjax.ShopStatus> Checkout(int userId)
                => Assert.IsType<StorefrontPhpAjax.ShopStatus>(await StorefrontPhpAjax.CheckoutCreateAsync(connection, userId, 0, "1", "{}", null, null, CancellationToken.None));

            var pending = await Checkout(3);
            Assert.Equal((false, "trade_not_approved", EpcCustomerTrade.PendingCheckoutMessage), (pending.Status, pending.Code, pending.Message));
            var rejected = await Checkout(4);
            Assert.Equal((false, "trade_not_approved", EpcCustomerTrade.RejectedCheckoutMessage + " Note: No licence"), (rejected.Status, rejected.Code, rejected.Message));
            var otherStatus = await Checkout(5);
            Assert.Equal((false, "trade_not_approved", string.Empty), (otherStatus.Status, otherStatus.Code, otherStatus.Message));
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private static void AssertSame(JsonNode? php, JsonNode? actual, string what)
        => Assert.True(JsonNode.DeepEquals(php, actual), $"{what}: PHP {php?.ToJsonString()} vs {actual?.ToJsonString()}");

    private static async Task<JsonNode?> RunAsync(MySqlConnection connection, JsonElement[] a)
    {
        var ct = CancellationToken.None;
        long L(int i) => a[i].GetInt64();
        string S(int i) => a[i].GetString()!;
        string? N(int i) => a[i].ValueKind == JsonValueKind.Null ? null : a[i].GetString();
        switch (S(0))
        {
            case "save":
                await EpcCustomerTrade.SaveRegistrationAsync(connection, null, L(1), S(2), ct);
                return null;
            case "approve":
                return await EpcCustomerTrade.ApproveCustomerAsync(connection, null, L(1), S(2), S(3), L(4), ct);
            case "reject":
                await EpcCustomerTrade.RejectCustomerAsync(connection, null, L(1), S(2), L(3), ct);
                return null;
            case "request_change":
                await EpcCustomerTrade.RequestCurrencyChangeAsync(connection, null, L(1), S(2), S(3), ct);
                return null;
            case "status":
                return await EpcCustomerTrade.ApprovalStatusAsync(connection, null, L(1), ct);
            case "can_order":
                return await EpcCustomerTrade.CanPlaceOrderAsync(connection, null, L(1), ct);
            case "block":
                return await EpcCustomerTrade.CheckoutBlockMessageAsync(connection, null, L(1), ct);
            case "currency_iso":
                return await EpcCustomerTrade.UserCurrencyIsoAsync(connection, null, L(1), ct);
            case "locked":
                return await EpcCustomerTrade.CurrencyLockedAsync(connection, null, L(1), ct);
            case "label":
                return EpcCustomerTrade.CustomerTypeLabel(S(1));
            case "normalize":
                return EpcCustomerTrade.NormalizeCustomerType(S(1));
            case "group_id":
                return await EpcCustomerTrade.PriceProfileGroupIdAsync(connection, null, S(1), ct);
            case "assign":
                return await EpcCustomerTrade.AssignPriceProfileAsync(connection, null, L(1), S(2), ct);
            case "get":
                return await EpcCustomerTrade.ProfileGetAsync(connection, null, L(1), S(2), ct, S(3));
            case "default_retail":
                return await EpcCustomerTrade.DefaultRetailCurrencyIsoAsync(connection, null, ct);
            case "pending":
                var pending = new JsonArray();
                foreach (var p in await EpcCustomerTrade.PendingCustomersAsync(connection, ct))
                {
                    pending.Add(new JsonObject
                    {
                        ["user_id"] = p.UserId.ToString(CultureInfo.InvariantCulture),
                        ["email"] = p.Email,
                        ["phone"] = p.Phone,
                        ["time_registered"] = p.TimeRegistered,
                        ["email_confirmed"] = p.EmailConfirmed,
                        ["customer_type"] = p.CustomerType,
                        ["name"] = p.Name,
                        ["surname"] = p.Surname,
                        ["company"] = p.Company,
                    });
                }

                return pending;
            case "records":
                return Records(await EpcCustomerTrade.CurrencyOptionsAsync(connection, S(1), ct));
            case "selected":
                var records = await EpcCurrency.RecordsAsync(connection, S(1), ct);
                return await EpcCurrency.SelectedIsoAsync(connection, records, S(1), L(2), N(3), N(4), ct);
            case "format":
                return EpcCurrency.FormatAmount(a[1].GetDouble(), await EpcCurrency.RecordsAsync(connection, S(2), ct), S(3), S(4));
            default:
                return "unknown op";
        }
    }

    private static JsonArray Records(IReadOnlyList<EpcCurrencyRecord> records)
    {
        var array = new JsonArray();
        foreach (var r in records)
        {
            array.Add(new JsonArray(r.IsoCode, r.IsoName, r.CaptionShort, r.Sign, r.Rate.ToString("R", CultureInfo.InvariantCulture)));
        }

        return array;
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
                    row.Add(reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture));
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
