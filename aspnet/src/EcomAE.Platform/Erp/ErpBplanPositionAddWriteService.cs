using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bplan_position_add</c> / ajax <c>bplan_position_add</c> twin.
/// INSERT <c>epc_bplan_position</c> when the plan is draft or review.
/// Plan save, worksheet lines, publish, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpBplanPositionAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpBplanPositionAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBplanPositionAddWriteRequest(
    long PlanId = 0,
    string? Title = null,
    string? Department = null,
    int Headcount = 0,
    decimal AnnualCost = 0,
    string? StartPeriod = null);

public sealed class ErpBplanPositionAddWriteService : IErpBplanPositionAddWriteService
{
    public static readonly HashSet<string> EditableStages = new(StringComparer.Ordinal)
    {
        "draft", "review",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBplanPositionAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpBplanPositionAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PlanId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Plan not found");
        }

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Position title is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var department = request.Department ?? string.Empty;
        var startPeriod = request.StartPeriod ?? string.Empty;
        var headcount = Math.Max(1, request.Headcount);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bplan_plan", "stage", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bplan_position", "title", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Budget forecast position table is not provisioned");
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
            return ErpSimpleWriteResult.Fail("invalid", "Positions can only be edited while the plan is in draft or review");
        }

        var companyObj = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `company_id` FROM `epc_bplan_plan` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.PlanId).ConfigureAwait(false);
        var companyId = companyObj is null ? 0L : Convert.ToInt64(companyObj, CultureInfo.InvariantCulture);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bplan_position` (`plan_id`,`company_id`,`title`,`department`,`headcount`,`annual_cost`,`start_period`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            request.PlanId,
            companyId,
            title,
            department,
            headcount,
            request.AnnualCost,
            startPeriod).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Forecast position added", id);
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
