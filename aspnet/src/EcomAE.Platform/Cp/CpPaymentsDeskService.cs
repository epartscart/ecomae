using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

/// <summary>A <c>shop_payment_systems</c> row with its decoded parameter widgets and saved values.</summary>
public sealed record CpPaymentGateway(
    long Id,
    string Name,
    string Handler,
    string Description,
    bool Enabled,
    bool Active,
    string Region,
    IReadOnlyList<PhpPaymentParameter> Parameters,
    IReadOnlyDictionary<string, string> Values)
{
    /// <summary>PHP renders the "Demo" badge from <c>parameters_values.demo_mode</c>.</summary>
    public bool DemoMode
        => Values.TryGetValue("demo_mode", out var flag) && flag is not ("" or "0" or "false");

    public string DisplayName => Name.Length > 0 ? Name : PhpPaymentGatewayCatalog.Title(Handler);
}

/// <summary>One <c>epc_payment_accounts</c> row — office / vendor / platform merchant identity.</summary>
public sealed record CpPaymentAccount(
    long Id,
    string OwnerType,
    long OwnerId,
    string Title,
    string Handler,
    long PaySystemId,
    string Mode,
    string CredentialsJson,
    string ConnectedAccountId,
    string PayoutIban,
    string PayoutBank,
    string PayoutName,
    decimal PlatformFeePct,
    string Status,
    bool DemoMode,
    bool IsDefault)
{
    public static CpPaymentAccount Empty { get; } = new(
        0, "platform", 0, "", "", 0, "direct", "{\"demo_mode\":1,\"currency\":\"AED\"}",
        "", "", "", "", 0m, "active", true, false);
}

/// <summary>One <c>epc_payment_settlements</c> row — who received an order payment.</summary>
public sealed record CpPaymentSettlement(
    long Id,
    long OrderId,
    long AccountId,
    string OwnerType,
    long OwnerId,
    string Handler,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetAmount,
    string Currency,
    string Status);

public sealed record CpPaymentOwnerOption(long Id, string Caption);

public sealed record CpPaymentsDesk(
    bool Available,
    string Message,
    IReadOnlyList<CpPaymentGateway> Gateways,
    IReadOnlyList<CpPaymentAccount> Accounts,
    IReadOnlyList<CpPaymentSettlement> Settlements,
    IReadOnlyList<CpPaymentOwnerOption> Offices,
    IReadOnlyList<CpPaymentOwnerOption> Vendors)
{
    public static CpPaymentsDesk Unavailable(string message)
        => new(false, message, [], [], [], [], []);

    public CpPaymentGateway? ActiveGateway => Gateways.FirstOrDefault(g => g.Active);

    public int CountInRegion(string region)
        => Gateways.Count(g => string.Equals(g.Region, region, StringComparison.Ordinal));

    public IReadOnlyList<CpPaymentGateway> InRegion(string region)
        => [.. Gateways.Where(g => string.Equals(g.Region, region, StringComparison.Ordinal))];
}

/// <summary>
/// Read twin of the PHP payment hub (<c>cp/content/shop/payments/payments_main.php</c>,
/// <c>payments_accounts.php</c>, <c>payments_configure.php</c>): gateways grouped by region with their
/// credential widgets, the individual office/vendor payment accounts and recent settlements.
/// </summary>
public interface ICpPaymentsDeskService
{
    Task<CpPaymentsDesk> LoadDeskAsync(int settlementLimit = 30, CancellationToken cancellationToken = default);
}

public sealed class CpPaymentsDeskService : ICpPaymentsDeskService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPaymentsDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>
    /// Decodes a PHP <c>parameters</c> JSON array into widgets, falling back to the catalogue definition
    /// so gateways seeded before a catalogue change still render every field.
    /// </summary>
    public static IReadOnlyList<PhpPaymentParameter> ParseParameters(string? json, string handler)
    {
        var parsed = new List<PhpPaymentParameter>();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        if (name.Length == 0)
                        {
                            continue;
                        }

                        var type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "text" : "text";
                        var caption = item.TryGetProperty("caption", out var c) ? c.GetString() ?? name : name;
                        parsed.Add(new PhpPaymentParameter(name, type, caption));
                    }
                }
            }
            catch (JsonException)
            {
                parsed.Clear();
            }
        }

        if (parsed.Count > 0)
        {
            return parsed;
        }

        return PhpPaymentGatewayCatalog.Definitions.TryGetValue(handler, out var def)
            ? def.Parameters
            : [];
    }

    /// <summary>Decodes a PHP <c>parameters_values</c> / <c>credentials</c> JSON object into flat strings.</summary>
    public static IReadOnlyDictionary<string, string> ParseValues(string? json)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                map[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? "",
                    JsonValueKind.True => "1",
                    JsonValueKind.False => "0",
                    JsonValueKind.Null or JsonValueKind.Undefined => "",
                    _ => property.Value.GetRawText()
                };
            }
        }
        catch (JsonException)
        {
            map.Clear();
        }

        return map;
    }

    public async Task<CpPaymentsDesk> LoadDeskAsync(
        int settlementLimit = 30,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPaymentsDesk.Unavailable("No database configured — payment gateways are unavailable.");
        }

        var limit = Math.Clamp(settlementLimit, 1, 200);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var gateways = await LoadGatewaysAsync(connection, cancellationToken).ConfigureAwait(false);
            var accounts = await LoadAccountsAsync(connection, cancellationToken).ConfigureAwait(false);
            var settlements = await LoadSettlementsAsync(connection, limit, cancellationToken).ConfigureAwait(false);
            var offices = await LoadOwnerOptionsAsync(
                connection,
                "SELECT `id`, IFNULL(`caption`,'') FROM `shop_offices` ORDER BY `id` ASC",
                cancellationToken).ConfigureAwait(false);
            var vendors = await LoadOwnerOptionsAsync(
                connection,
                "SELECT `id`, IFNULL(NULLIF(`vendor_full`,''), IFNULL(`vendor_short`,'')) "
                + "FROM `epc_vendor_accounts` ORDER BY `id` DESC LIMIT 500",
                cancellationToken).ConfigureAwait(false);

            return new CpPaymentsDesk(true, "", gateways, accounts, settlements, offices, vendors);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new CpPaymentsDesk(true, string.Empty, [], [], [], [], []);
        }
        catch (DbException ex)
        {
            return CpPaymentsDesk.Unavailable("Payment tables are unavailable: " + ex.Message);
        }
    }

    private static async Task<IReadOnlyList<CpPaymentGateway>> LoadGatewaysAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpPaymentGateway>();
        await using var cmd = connection.CreateCommand();
        // PHP epc_payment_list_all(): every handler row, default rail first.
        cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`handler`,''), IFNULL(`description`,''), "
                          + "IFNULL(`anable`,0), IFNULL(`active`,0), IFNULL(`parameters`,''), IFNULL(`parameters_values`,'') "
                          + "FROM `shop_payment_systems` WHERE `handler` <> '' ORDER BY `active` DESC, `id` ASC";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var handler = reader.GetString(2);
            var definition = PhpPaymentGatewayCatalog.Definitions.GetValueOrDefault(handler);
            var name = reader.GetString(1);
            var description = reader.GetString(3);
            rows.Add(new CpPaymentGateway(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                // PHP resolves the lang key through translate_str_by_id(); the catalogue holds the English strings.
                definition is not null ? definition.Title : name,
                handler,
                definition is not null ? definition.Description : description,
                Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture) == 1,
                Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture) == 1,
                PhpPaymentGatewayCatalog.Region(handler),
                ParseParameters(reader.GetString(6), handler),
                ParseValues(reader.GetString(7))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpPaymentAccount>> LoadAccountsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpPaymentAccount>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `id`, `owner_type`, `owner_id`, `title`, `handler`, `pay_system_id`, `mode`, "
                              + "IFNULL(`credentials`,''), `connected_account_id`, `payout_iban`, `payout_bank`, "
                              + "`payout_name`, `platform_fee_pct`, `status`, `demo_mode`, `is_default` "
                              + "FROM `epc_payment_accounts` ORDER BY `is_default` DESC, `id` DESC";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpPaymentAccount(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.GetString(3),
                    reader.GetString(4),
                    Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetString(9),
                    reader.GetString(10),
                    reader.GetString(11),
                    Convert.ToDecimal(reader.GetValue(12), CultureInfo.InvariantCulture),
                    reader.GetString(13),
                    Convert.ToInt64(reader.GetValue(14), CultureInfo.InvariantCulture) == 1,
                    Convert.ToInt64(reader.GetValue(15), CultureInfo.InvariantCulture) == 1));
            }
        }
        catch (DbException)
        {
            // PHP creates epc_payment_accounts on demand; an absent table simply means no accounts yet.
            rows.Clear();
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpPaymentSettlement>> LoadSettlementsAsync(
        DbConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpPaymentSettlement>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `id`, `order_id`, `account_id`, `owner_type`, `owner_id`, `handler`, "
                              + "`gross_amount`, `fee_amount`, `net_amount`, `currency`, `status` "
                              + "FROM `epc_payment_settlements` ORDER BY `id` DESC LIMIT "
                              + limit.ToString(CultureInfo.InvariantCulture);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpPaymentSettlement(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.GetString(3),
                    Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture),
                    reader.GetString(5),
                    Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(7), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(8), CultureInfo.InvariantCulture),
                    reader.GetString(9),
                    reader.GetString(10)));
            }
        }
        catch (DbException)
        {
            rows.Clear();
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpPaymentOwnerOption>> LoadOwnerOptionsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpPaymentOwnerOption>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpPaymentOwnerOption(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? "" : reader.GetString(1)));
            }
        }
        catch (DbException)
        {
            // Marketplace vendors are optional (epc_vendor_accounts may not exist), exactly as in PHP.
            rows.Clear();
        }

        return rows;
    }
}
