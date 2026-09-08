using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bplan_line_add</c> / ajax <c>bplan_line_add</c> twin.
/// INSERT <c>epc_bplan_line</c> when the plan is draft or review.
/// Plan save, forecast positions, publish, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpBplanLineAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpBplanLineAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBplanLineAddWriteRequest(
    long PlanId = 0,
    string? Account = null,
    string? Dimension = null,
    string? Scenario = null,
    string? Period = null,
    decimal Amount = 0);

public sealed class ErpBplanLineAddWriteService : IErpBplanLineAddWriteService
{
    public static readonly HashSet<string> EditableStages = new(StringComparer.Ordinal)
    {
        "draft", "review",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBplanLineAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpBplanLineAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PlanId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Plan not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var account = request.Account ?? string.Empty;
        var dimension = request.Dimension ?? string.Empty;
        var scenario = request.Scenario ?? "base";
        var period = request.Period ?? string.Empty;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bplan_plan", "stage", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bplan_line", "account", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Budget worksheet table is not provisioned");
        }

        var stageObj = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `stage` FROM `epc_bplan_plan` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.PlanId).ConfigureAwait(false);
        if (stageObj is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Plan not found");
        }

        var stage = Convert.ToString(stageObj, CultureInfo.InvariantCulture) ?? string.Empty;
        if (!EditableStages.Contains(stage))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Lines can only be edited while the plan is in draft or review");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bplan_line` (`plan_id`,`account`,`dimension`,`scenario`,`period`,`amount`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            request.PlanId,
            account,
            dimension,
            scenario,
            period,
            request.Amount).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Worksheet line added", id);
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
