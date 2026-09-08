using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_mfgr_wc_save</c> / ajax <c>mfgr_wc_save</c> twin.
/// UPDATE/INSERT <c>epc_mfg_wc</c>. Route save, MRP, WO issue/complete, and schema stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpMfgrWcSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpMfgrWcSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpMfgrWcSaveWriteRequest(
    long Id = 0,
    string? Code = null,
    string? Name = null,
    int? CapacityMinPerDay = null,
    decimal CostPerHour = 0,
    int? Active = null,
    long CompanyHint = 0);

public sealed class ErpMfgrWcSaveWriteService : IErpMfgrWcSaveWriteService
{
    public const string CodeRequired = "Work center code is required";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpMfgrWcSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpMfgrWcSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = NormalizeCode(request.Code);
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", CodeRequired);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var name = request.Name ?? "";
        var cap = request.CapacityMinPerDay ?? 480;
        if (cap < 0)
        {
            cap = 0;
        }

        var active = request.Active ?? 1;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_mfg_wc", "code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Work center table is not provisioned");
        }

        var companyId = await ResolveActiveCompanyIdAsync(connection, request.CompanyHint, cancellationToken).ConfigureAwait(false);
        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_mfg_wc` SET `code`=?, `name`=?, `capacity_min_per_day`=?, `cost_per_hour`=?, `active`=? WHERE `id`=?"),
                cancellationToken,
                code, name, cap, request.CostPerHour, active, request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Work center saved", request.Id);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_mfg_wc` (`company_id`,`code`,`name`,`capacity_min_per_day`,`cost_per_hour`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            (int)companyId, code, name, cap, request.CostPerHour, active, now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Work center saved", id);
    }

    /// <summary>PHP <c>strtoupper(trim($code))</c>.</summary>
    public static string NormalizeCode(string? raw)
        => (raw ?? "").Trim().ToUpperInvariant();

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

    public static int? JsonIntOrNull(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return (int)n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                return i;
            }
        }

        return null;
    }

    public static decimal JsonDec(JsonElement root, params string[] names)
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

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d))
            {
                return d;
            }

            if (prop.ValueKind == JsonValueKind.String
                && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
        }

        return 0;
    }

    private static async Task<long> ResolveActiveCompanyIdAsync(
        DbConnection connection,
        long hint,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, "epc_erp_pm_legal_entities", "code", cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var ids = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(reader.GetInt64(0));
            }
        }

        if (ids.Count == 0)
        {
            return 0;
        }

        if (hint > 0 && ids.Contains(hint))
        {
            return hint;
        }

        return ids[0];
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
