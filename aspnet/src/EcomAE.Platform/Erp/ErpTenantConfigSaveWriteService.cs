using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP ajax <c>tenant_config_save</c> / <c>epc_erp_adv_set_setting</c> twin.
/// UPSERT whitelist keys into <c>epc_price_settings</c> as <c>erp_{key}</c>.
/// Schema ensure stays PHP. Does not CREATE tables.
/// </summary>
public interface IErpTenantConfigSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpTenantConfigSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpTenantConfigSaveWriteRequest(
    IReadOnlyDictionary<string, string>? Values = null);

public sealed class ErpTenantConfigSaveWriteService : IErpTenantConfigSaveWriteService
{
    public static readonly string[] AllowedKeys =
    [
        "company_name", "company_name_ar", "company_trn", "company_address",
        "company_phone", "company_email", "company_country", "company_city",
        "company_license_no", "industry_profile", "industry_pack",
        "default_currency", "fiscal_year_start", "vat_rate", "date_format",
        "number_format_decimals", "weight_unit",
        "po_prefix", "so_prefix", "inv_prefix", "jv_prefix", "pv_prefix", "rv_prefix", "dn_prefix",
        "auto_number_vouchers", "default_warehouse", "default_payment_terms",
        "bank_name", "bank_account", "bank_iban", "bank_swift",
        "ui_theme", "ui_density", "ui_grid_rows",
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpTenantConfigSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpTenantConfigSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var values = CollectAllowed(request.Values);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_price_settings", "setting_key", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Settings table is not provisioned");
        }

        foreach (var pair in values)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
                cancellationToken,
                "erp_" + pair.Key,
                pair.Value).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok(values.Count.ToString(CultureInfo.InvariantCulture) + " settings saved", 0);
    }

    public static Dictionary<string, string> CollectAllowed(IReadOnlyDictionary<string, string>? incoming)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (incoming is null)
        {
            return values;
        }

        foreach (var key in AllowedKeys)
        {
            if (incoming.TryGetValue(key, out var raw))
            {
                values[key] = raw.Trim();
            }
        }

        return values;
    }

    public static Dictionary<string, string> CollectFromJson(JsonElement root)
    {
        var incoming = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object)
        {
            return incoming;
        }

        foreach (var key in AllowedKeys)
        {
            if (TryGetJsonString(root, key, out var value)
                || TryGetJsonString(root, ToCamel(key), out value))
            {
                incoming[key] = value;
            }
        }

        return incoming;
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

            if (prop.ValueKind == JsonValueKind.String)
            {
                var raw = prop.GetString()?.Trim();
                if (raw is "1" or "true" or "True" or "on" or "yes")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetJsonString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var prop))
        {
            return false;
        }

        value = prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? string.Empty,
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Null => string.Empty,
            _ => prop.GetRawText(),
        };
        return true;
    }

    private static string ToCamel(string snake)
    {
        var parts = snake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return snake;
        }

        var sb = new StringBuilder(snake.Length);
        sb.Append(parts[0]);
        for (var i = 1; i < parts.Length; i++)
        {
            sb.Append(char.ToUpperInvariant(parts[i][0]));
            if (parts[i].Length > 1)
            {
                sb.Append(parts[i].AsSpan(1));
            }
        }

        return sb.ToString();
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
