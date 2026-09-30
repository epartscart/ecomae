using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_pm_save</c> / ajax <c>pm_save</c> twin.
/// UPDATE/INSERT one whitelist <c>epc_erp_pm_*</c> master row for present columns only.
/// Toggle, budget, listing, cheque, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpPmSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPmSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPmSaveWriteRequest(
    string? PmTable = null,
    long Id = 0,
    IReadOnlyDictionary<string, string>? Fields = null);

public sealed class ErpPmSaveWriteService : IErpPmSaveWriteService
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

    public ErpPmSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPmSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var table = request.PmTable ?? "";
        if (!Registry.ContainsKey(table))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown master table");
        }

        var fields = CollectAllowed(table, request.Fields);
        if (fields.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Nothing to save");
        }

        var fieldValidation = ValidateMasterFields(table, request.Id, fields);
        if (fieldValidation is not null)
        {
            return fieldValidation;
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

        if (request.Id > 0)
        {
            var existing = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `" + table + "` WHERE `id` = ?"),
                cancellationToken,
                request.Id).ConfigureAwait(false);
            if (existing != request.Id)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Master record was not found.");
            }
        }

        var relationshipValidation = await ValidateRelationshipsAsync(
            connection,
            table,
            request.Id,
            fields,
            cancellationToken).ConfigureAwait(false);
        if (relationshipValidation is not null)
        {
            return relationshipValidation;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var set = new List<string>();
        var vals = new List<object?>();
        foreach (var pair in fields)
        {
            set.Add("`" + pair.Key + "` = ?");
            vals.Add(pair.Value);
        }

        long id;
        if (request.Id > 0)
        {
            id = request.Id;
            set.Add("`time_updated` = ?");
            vals.Add(now);
            vals.Add(id);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `" + table + "` SET " + string.Join(",", set) + " WHERE `id` = ?"),
                cancellationToken,
                vals.ToArray()).ConfigureAwait(false);
        }
        else
        {
            set.Add("`active` = 1");
            set.Add("`time_created` = " + now.ToString(CultureInfo.InvariantCulture));
            set.Add("`time_updated` = " + now.ToString(CultureInfo.InvariantCulture));
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `" + table + "` SET " + string.Join(",", set)),
                cancellationToken,
                vals.ToArray()).ConfigureAwait(false);
            id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Saved", id);
    }

    public static ErpSimpleWriteResult? ValidateMasterFields(
        string table,
        long id,
        IReadOnlyDictionary<string, string> fields)
    {
        if (table is not ("epc_erp_pm_dimensions"
            or "epc_erp_pm_dimension_values"
            or "epc_erp_pm_legal_entities"
            or "epc_erp_pm_business_units"))
        {
            return null;
        }

        if (fields.TryGetValue("code", out var code) && string.IsNullOrWhiteSpace(code))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A master code is required.");
        }

        if (fields.TryGetValue("name", out var name) && string.IsNullOrWhiteSpace(name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A master name is required.");
        }

        if (id <= 0 && (!fields.ContainsKey("code") || !fields.ContainsKey("name")))
        {
            return ErpSimpleWriteResult.Fail("invalid", "New organizational masters require code and name.");
        }

        if (table == "epc_erp_pm_dimension_values"
            && fields.TryGetValue("dimension_id", out var dimensionId)
            && (!long.TryParse(dimensionId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDimensionId)
                || parsedDimensionId <= 0))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A valid parent dimension is required.");
        }

        return null;
    }

    public static Dictionary<string, string> CollectAllowed(string table, IReadOnlyDictionary<string, string>? incoming)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (incoming is null || !Registry.TryGetValue(table, out var cols))
        {
            return values;
        }

        foreach (var col in cols)
        {
            if (incoming.TryGetValue(col, out var raw)
                || incoming.TryGetValue(ToCamel(col), out raw))
            {
                values[col] = raw.Trim();
            }
        }

        return values;
    }

    public static Dictionary<string, string> CollectFromJson(string table, JsonElement root)
    {
        var incoming = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object || !Registry.TryGetValue(table, out var cols))
        {
            return incoming;
        }

        foreach (var col in cols)
        {
            if (TryGetJsonString(root, col, out var value)
                || TryGetJsonString(root, ToCamel(col), out value))
            {
                incoming[col] = value;
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
            if (TryGetJsonString(root, name, out var value))
            {
                return value;
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

    private static bool TryGetJsonString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var prop))
        {
            return false;
        }

        value = prop.ValueKind switch
        {
            JsonValueKind.String => (prop.GetString() ?? string.Empty).Trim(),
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Null => string.Empty,
            _ => prop.GetRawText(),
        };
        return true;
    }

    private static async Task<ErpSimpleWriteResult?> ValidateRelationshipsAsync(
        DbConnection connection,
        string table,
        long id,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        if (table == "epc_erp_pm_dimension_values"
            && fields.TryGetValue("dimension_id", out var dimensionIdText)
            && long.TryParse(dimensionIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dimensionId))
        {
            if (!await TableExistsAsync(connection, "epc_erp_pm_dimensions", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Dimension master is not provisioned.");
            }

            var activeDimension = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_dimensions` WHERE `id` = ? AND `active` = 1"),
                cancellationToken,
                dimensionId).ConfigureAwait(false);
            if (activeDimension != dimensionId)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Dimension values must belong to an active dimension.");
            }
        }

        if (table == "epc_erp_pm_business_units"
            && fields.TryGetValue("legal_entity_id", out var legalEntityText)
            && long.TryParse(legalEntityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var legalEntityId)
            && legalEntityId > 0)
        {
            if (!await TableExistsAsync(connection, "epc_erp_pm_legal_entities", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Legal-entity master is not provisioned.");
            }

            var activeLegalEntity = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `id` = ? AND `active` = 1"),
                cancellationToken,
                legalEntityId).ConfigureAwait(false);
            if (activeLegalEntity != legalEntityId)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Business units must belong to an active legal entity.");
            }

            if (fields.TryGetValue("parent_id", out var parentText)
                && long.TryParse(parentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentId)
                && parentId > 0)
            {
                if (parentId == id)
                {
                    return ErpSimpleWriteResult.Fail("invalid", "A business unit cannot be its own parent.");
                }

                if (!await TableExistsAsync(connection, "epc_erp_pm_business_units", cancellationToken).ConfigureAwait(false))
                {
                    return ErpSimpleWriteResult.Fail("invalid", "Business-unit master is not provisioned.");
                }

                var parentLegalEntity = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `legal_entity_id` FROM `epc_erp_pm_business_units` WHERE `id` = ? AND `active` = 1"),
                    cancellationToken,
                    parentId).ConfigureAwait(false);
                if (parentLegalEntity != legalEntityId)
                {
                    return ErpSimpleWriteResult.Fail("invalid", "Business-unit parents must use the same legal entity.");
                }
            }
        }

        return null;
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

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }
}
