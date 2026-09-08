using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_intel_set_control</c> / ajax <c>bos_intel_toggle_control</c> twin.
/// UPSERT <c>epc_price_settings.bos_intel_controls</c> JSON. Schema ensure stays PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpBosIntelToggleWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(
        ErpBosIntelToggleWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBosIntelToggleWriteRequest(
    string? Code = null,
    bool Checked = false);

public sealed class ErpBosIntelToggleWriteService : IErpBosIntelToggleWriteService
{
    public const string SettingKey = "bos_intel_controls";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosIntelToggleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ToggleAsync(
        ErpBosIntelToggleWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var code = request.Code ?? "";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_price_settings", "setting_key", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Settings table is not provisioned");
        }

        var raw = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"),
            cancellationToken,
            SettingKey).ConfigureAwait(false);
        var state = ParseStateObject(raw);
        if (request.Checked)
        {
            state[code] = 1;
        }
        else
        {
            state.Remove(code);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            cancellationToken,
            SettingKey,
            EncodeState(state)).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Control updated", 0);
    }

    /// <summary>PHP <c>json_decode(..., true)</c> of the stored blob; invalid JSON becomes [].</summary>
    public static Dictionary<string, int> ParseState(string? raw)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in ParseStateObject(raw))
        {
            state[pair.Key] = JsonNodeToInt(pair.Value);
        }

        return state;
    }

    public static JsonObject ParseStateObject(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return [];
        }

        try
        {
            var node = JsonNode.Parse(raw);
            if (node is JsonObject obj)
            {
                return obj;
            }

            if (node is JsonArray arr)
            {
                var fromArray = new JsonObject();
                for (var i = 0; i < arr.Count; i++)
                {
                    fromArray[i.ToString(CultureInfo.InvariantCulture)] = arr[i]?.DeepClone();
                }

                return fromArray;
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    /// <summary>PHP <c>json_encode</c>: empty array is <c>[]</c>, otherwise an object.</summary>
    public static string EncodeState(IReadOnlyDictionary<string, int> state)
    {
        if (state.Count == 0)
        {
            return "[]";
        }

        var obj = new JsonObject();
        foreach (var pair in state)
        {
            obj[pair.Key] = pair.Value;
        }

        return obj.ToJsonString();
    }

    public static string EncodeState(JsonObject state)
        => state.Count == 0 ? "[]" : state.ToJsonString();

    public static bool PhpCheckedIsOne(string? raw)
    {
        if (int.TryParse((raw ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return n == 1;
        }

        return false;
    }

    public static bool JsonCheckedIsOne(JsonElement root, params string[] names)
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

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n == 1;
            }

            if (prop.ValueKind == JsonValueKind.String
                && int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                return i == 1;
            }
        }

        return false;
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

    private static int JsonNodeToInt(JsonNode? value)
        => value?.GetValueKind() switch
        {
            JsonValueKind.True => 1,
            JsonValueKind.Number when value.TryGetValue<long>(out var n) => n != 0 ? 1 : 0,
            JsonValueKind.Number when value.TryGetValue<decimal>(out var d) => d != 0 ? 1 : 0,
            JsonValueKind.String when int.TryParse(value.GetValue<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                => i == 0 ? 0 : 1,
            JsonValueKind.String when !string.IsNullOrEmpty(value.GetValue<string>()) && value.GetValue<string>() is not "0" => 1,
            _ => 0,
        };

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
