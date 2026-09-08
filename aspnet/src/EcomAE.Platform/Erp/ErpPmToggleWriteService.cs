using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_pm_toggle</c> / ajax <c>pm_toggle</c> twin.
/// UPDATE <c>active</c> on one whitelist <c>epc_erp_pm_*</c> master row.
/// Save, budget, listing, cheque, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpPmToggleWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(
        ErpPmToggleWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPmToggleWriteRequest(
    string? PmTable = null,
    long Id = 0,
    bool Active = false);

public sealed class ErpPmToggleWriteService : IErpPmToggleWriteService
{
    public static readonly IReadOnlyDictionary<string, string[]> Registry =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["epc_erp_pm_business_units"] = ["code", "name", "legal_entity_id", "parent_id", "manager", "note"],
            ["epc_erp_pm_class_units"] = ["code", "name", "class_type", "note"],
            ["epc_erp_pm_legal_entities"] = ["code", "name", "country_code", "currency_code", "trn", "note"],
            ["epc_erp_pm_dimensions"] = ["code", "name", "dim_type", "note"],
            ["epc_erp_pm_dimension_values"] = ["dimension_id", "code", "name", "note"],
            ["epc_erp_pm_vendor_groups"] = ["code", "name", "terms_id", "note"],
            ["epc_erp_pm_customer_groups"] = ["code", "name", "terms_id", "note"],
            ["epc_erp_pm_pay_methods"] = ["code", "name", "method_type", "account_code", "note"],
            ["epc_erp_pm_pay_terms"] = ["code", "name", "net_days", "note"],
            ["epc_erp_pm_inv_groups"] = ["code", "name", "valuation", "note"],
            ["epc_erp_pm_barcode_formats"] = ["code", "name", "symbology", "pattern", "note"],
        };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPmToggleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ToggleAsync(
        ErpPmToggleWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var table = request.PmTable ?? "";
        if (!Registry.ContainsKey(table))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown master table");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, table, "code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Master table is not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `" + table + "` SET `active` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            request.Active ? 1 : 0,
            now,
            (int)request.Id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Updated", request.Id);
    }

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
    }

    public static string JsonText(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
        }

        return "";
    }

    public static long JsonLong(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }
        }

        return 0;
    }

    /// <summary>PHP <c>(int)$active ? 1 : 0</c>.</summary>
    public static bool IsPhpActive(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw == "0")
        {
            return false;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return n != 0;
        }

        return raw is not "false" and not "False";
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
