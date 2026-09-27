using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public interface IErpPrjaRecognitionWriteService
{
    Task<ErpSimpleWriteResult> RunAsync(
        ErpPrjaRecognitionWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPrjaRecognitionWriteRequest(
    long CompanyId = 0,
    long ProjectId = 0,
    string? Method = null,
    decimal Fraction = 0,
    long AsOf = 0);

public sealed class ErpPrjaRecognitionWriteService : IErpPrjaRecognitionWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPrjaRecognitionWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RunAsync(
        ErpPrjaRecognitionWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (request.ProjectId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project ID is required.");
        }

        var method = request.Method is "completed" or "straight_line" or "poc"
            ? request.Method
            : "poc";
        var fraction = Math.Clamp(request.Fraction, 0, 1);
        var asOf = request.AsOf > 0 ? request.AsOf : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await RequiredTablesExistAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project accounting tables are not provisioned.");
        }

        var revenueBudget = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(SUM(`revenue_budget`),0) FROM `epc_prja_budget` WHERE `project_id` = ?"),
            cancellationToken,
            request.ProjectId).ConfigureAwait(false);
        var costBudget = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(SUM(`cost_budget`),0) FROM `epc_prja_budget` WHERE `project_id` = ?"),
            cancellationToken,
            request.ProjectId).ConfigureAwait(false);
        var costActual = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(SUM(`amount`),0) FROM `epc_prja_txn` WHERE `project_id` = ? AND `txn_type` = 'cost'"),
            cancellationToken,
            request.ProjectId).ConfigureAwait(false);
        var billed = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(SUM(`amount`),0) FROM `epc_prja_txn` WHERE `project_id` = ? AND `txn_type` = 'billing'"),
            cancellationToken,
            request.ProjectId).ConfigureAwait(false);

        var pct = costBudget <= 0 ? (costActual > 0 ? 1m : 0m) : Math.Clamp(costActual / costBudget, 0, 1);
        var recognizedRevenue = method switch
        {
            "completed" => pct >= 1 ? revenueBudget : 0,
            "straight_line" => decimal.Round(revenueBudget * fraction, 2, MidpointRounding.AwayFromZero),
            _ => decimal.Round(revenueBudget * pct, 2, MidpointRounding.AwayFromZero),
        };
        var recognizedCost = method == "completed" && pct < 1 ? 0 : costActual;
        var wip = decimal.Round(recognizedRevenue - billed, 2, MidpointRounding.AwayFromZero);
        var detail = JsonSerializer.Serialize(new
        {
            budget = new { cost_budget = costBudget, revenue_budget = revenueBudget },
            actual = new { cost = costActual, billing = billed },
        });

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_prja_recognition` (`company_id`,`project_id`,`method`,`as_of`,`pct_complete`,`recognized_revenue`,`recognized_cost`,`wip`,`detail_json`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            Math.Max(0, request.CompanyId),
            request.ProjectId,
            method,
            asOf,
            decimal.Round(pct, 4, MidpointRounding.AwayFromZero),
            recognizedRevenue,
            decimal.Round(recognizedCost, 2, MidpointRounding.AwayFromZero),
            wip,
            detail,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Project recognition run saved", id);
    }

    private static async Task<bool> RequiredTablesExistAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('epc_prja_budget','epc_prja_txn','epc_prja_recognition')"),
            cancellationToken).ConfigureAwait(false);
        return count == 3;
    }
}
