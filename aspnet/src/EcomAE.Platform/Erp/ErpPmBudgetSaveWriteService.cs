using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_pm_budget_save</c> / ajax <c>pm_budget_save</c> twin.
/// UPDATE/INSERT <c>epc_erp_pm_budgets</c>. Line save, listing, cheque, toggle, and schema stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpPmBudgetSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPmBudgetSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPmBudgetSaveWriteRequest(
    long Id = 0,
    string? Code = null,
    string? Name = null,
    string? FiscalYear = null,
    long BusinessUnitId = 0,
    bool IsMaster = false,
    string? Note = null);

public sealed class ErpPmBudgetSaveWriteService : IErpPmBudgetSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPmBudgetSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPmBudgetSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var code = (request.Code ?? "").Trim();
        var name = (request.Name ?? "").Trim();
        var fiscalYear = (request.FiscalYear ?? "").Trim();
        var note = (request.Note ?? "").Trim();
        var buId = (int)request.BusinessUnitId;
        var isMaster = request.IsMaster ? 1 : 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_pm_budgets", "code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Budget table is not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long id;
        if (request.Id > 0)
        {
            id = request.Id;
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_pm_budgets` SET `code`=?,`name`=?,`fiscal_year`=?,`business_unit_id`=?,`is_master`=?,`note`=?,`time_updated`=? WHERE `id`=?"),
                cancellationToken,
                code, name, fiscalYear, buId, isMaster, note, now, id).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_erp_pm_budgets` (`code`,`name`,`fiscal_year`,`business_unit_id`,`is_master`,`note`,`active`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,1,?,?)"),
                cancellationToken,
                code, name, fiscalYear, buId, isMaster, note, now, now).ConfigureAwait(false);
            id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Budget saved", id);
    }

    /// <summary>PHP <c>!empty</c> for is_master.</summary>
    public static bool IsPhpNonEmpty(string? raw)
    {
        return !string.IsNullOrEmpty(raw) && raw is not "0";
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

            if (prop.ValueKind == JsonValueKind.String && IsPhpNonEmpty(prop.GetString()))
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
                return (prop.GetString() ?? "").Trim();
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
