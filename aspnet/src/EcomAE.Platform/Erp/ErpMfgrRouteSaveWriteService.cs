using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_mfgr_route_save</c> / ajax <c>mfgr_route_save</c> twin.
/// UPDATE/INSERT <c>epc_mfg_route</c> then DELETE+INSERT <c>epc_mfg_route_op</c>.
/// MRP, WO issue/complete, and schema stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpMfgrRouteSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpMfgrRouteSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpMfgrRouteOpInput(
    int OpNo = 10,
    int WorkcenterId = 0,
    string Description = "",
    decimal SetupMin = 0,
    decimal RunMinPerUnit = 0);

public sealed record ErpMfgrRouteSaveWriteRequest(
    long Id = 0,
    long ProductItemId = 0,
    string? Name = null,
    int? Active = null,
    long CompanyHint = 0,
    IReadOnlyList<ErpMfgrRouteOpInput>? Ops = null);

public sealed class ErpMfgrRouteSaveWriteService : IErpMfgrRouteSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpMfgrRouteSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpMfgrRouteSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var name = request.Name ?? "";
        var active = request.Active ?? 1;
        var ops = request.Ops ?? [];

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_mfg_route", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_mfg_route_op", "route_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Route table is not provisioned");
        }

        var companyId = await ResolveActiveCompanyIdAsync(connection, request.CompanyHint, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        long id;
        if (request.Id > 0)
        {
            id = request.Id;
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_mfg_route` SET `product_item_id`=?, `name`=?, `active`=? WHERE `id`=?"),
                cancellationToken,
                (int)request.ProductItemId, name, active, id).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_mfg_route_op` WHERE `route_id`=?"),
                cancellationToken,
                id).ConfigureAwait(false);
        }
        else
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `epc_mfg_route` (`company_id`,`product_item_id`,`name`,`active`,`time_created`) VALUES (?,?,?,?,?)"),
                cancellationToken,
                (int)companyId, (int)request.ProductItemId, name, active, now).ConfigureAwait(false);
            id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        }

        foreach (var op in ops)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `epc_mfg_route_op` (`route_id`,`op_no`,`workcenter_id`,`description`,`setup_min`,`run_min_per_unit`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                id, op.OpNo, op.WorkcenterId, op.Description ?? "", op.SetupMin, op.RunMinPerUnit).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Route saved", id);
    }

    /// <summary>PHP ajax skips an op when workcenter_id &lt;= 0 and run == 0 and setup == 0.</summary>
    public static bool ShouldSkipOp(int workcenterId, decimal runMinPerUnit, decimal setupMin)
        => workcenterId <= 0 && runMinPerUnit == 0 && setupMin == 0;

    public static IReadOnlyList<ErpMfgrRouteOpInput> ParseOpsFromForm(IFormCollection form)
    {
        var opNos = FormValues(form, "op_no", "op_no[]");
        var wcs = FormValues(form, "workcenter_id", "workcenter_id[]");
        var setups = FormValues(form, "setup_min", "setup_min[]");
        var runs = FormValues(form, "run_min_per_unit", "run_min_per_unit[]");
        var descs = FormValues(form, "op_desc", "op_desc[]", "description", "description[]");
        var ops = new List<ErpMfgrRouteOpInput>();
        for (var i = 0; i < opNos.Count; i++)
        {
            var opNo = ParseInt(opNos[i], 0);
            var wcId = i < wcs.Count ? ParseInt(wcs[i], 0) : 0;
            var setup = i < setups.Count ? ParseDec(setups[i], 0) : 0;
            var run = i < runs.Count ? ParseDec(runs[i], 0) : 0;
            var desc = i < descs.Count ? descs[i] : "";
            if (ShouldSkipOp(wcId, run, setup))
            {
                continue;
            }

            ops.Add(new ErpMfgrRouteOpInput(opNo, wcId, desc, setup, run));
        }

        return ops;
    }

    public static IReadOnlyList<ErpMfgrRouteOpInput> ParseOpsFromJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (root.TryGetProperty("ops", out var opsEl) && opsEl.ValueKind == JsonValueKind.Array)
        {
            var fromObjects = new List<ErpMfgrRouteOpInput>();
            foreach (var item in opsEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var opNo = JsonLongOrDefault(item, 10, "op_no", "opNo");
                var wcId = (int)JsonLong(item, "workcenter_id", "workcenterId");
                var setup = JsonDec(item, "setup_min", "setupMin");
                var run = JsonDec(item, "run_min_per_unit", "runMinPerUnit");
                var desc = JsonText(item, "description", "op_desc", "opDesc");
                if (ShouldSkipOp(wcId, run, setup))
                {
                    continue;
                }

                fromObjects.Add(new ErpMfgrRouteOpInput((int)opNo, wcId, desc, setup, run));
            }

            return fromObjects;
        }

        var opNos = JsonArrayTexts(root, "op_no", "opNo");
        if (opNos.Count == 0)
        {
            return [];
        }

        var wcs = JsonArrayTexts(root, "workcenter_id", "workcenterId");
        var setups = JsonArrayTexts(root, "setup_min", "setupMin");
        var runs = JsonArrayTexts(root, "run_min_per_unit", "runMinPerUnit");
        var descs = JsonArrayTexts(root, "op_desc", "opDesc", "description");
        var ops = new List<ErpMfgrRouteOpInput>();
        for (var i = 0; i < opNos.Count; i++)
        {
            var opNo = ParseInt(opNos[i], 0);
            var wcId = i < wcs.Count ? ParseInt(wcs[i], 0) : 0;
            var setup = i < setups.Count ? ParseDec(setups[i], 0) : 0;
            var run = i < runs.Count ? ParseDec(runs[i], 0) : 0;
            var desc = i < descs.Count ? descs[i] : "";
            if (ShouldSkipOp(wcId, run, setup))
            {
                continue;
            }

            ops.Add(new ErpMfgrRouteOpInput(opNo, wcId, desc, setup, run));
        }

        return ops;
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

    public static long JsonLongOrDefault(JsonElement root, long fallback, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return fallback;
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

        return fallback;
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

    private static IReadOnlyList<string> JsonArrayTexts(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var values = new List<string>();
            foreach (var item in prop.EnumerateArray())
            {
                values.Add(item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? ""
                    : item.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                        ? item.GetRawText()
                        : "");
            }

            return values;
        }

        return [];
    }

    private static IReadOnlyList<string> FormValues(IFormCollection form, params string[] names)
    {
        foreach (var name in names)
        {
            if (!form.ContainsKey(name))
            {
                continue;
            }

            return form[name].Select(v => v ?? "").ToArray();
        }

        return [];
    }

    private static int ParseInt(string? raw, int fallback)
        => int.TryParse((raw ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : fallback;

    private static decimal ParseDec(string? raw, decimal fallback)
        => decimal.TryParse((raw ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n)
            ? n
            : fallback;

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
