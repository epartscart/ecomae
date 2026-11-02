using System.Data.Common;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutProjectPnlReadService
{
    Task<ErpFitOutProjectPnlResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutProjectPnlRow(
    long ProjectId,
    decimal RevenueBudget,
    decimal CostBudget,
    decimal ActualRevenue,
    decimal ActualCost,
    decimal BudgetMargin,
    decimal ActualMargin,
    decimal ForecastRevenue,
    decimal ForecastCost,
    decimal ForecastProfit);

public sealed record ErpFitOutProjectPnlResult(
    IReadOnlyList<ErpFitOutProjectPnlRow> Rows,
    string Source,
    string Message)
{
    public static ErpFitOutProjectPnlResult Empty(string message)
        => new([], "empty", message);
}

/// <summary>
/// Aggregate project P&amp;L/forecast over the whole budget and transaction
/// ledgers — unlike the capped row digest, every project and amount is counted.
/// </summary>
public sealed class ErpFitOutProjectPnlReadService : IErpFitOutProjectPnlReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutProjectPnlReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutProjectPnlResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpFitOutProjectPnlResult.Empty("TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections
                .OpenAsync(cancellationToken)
                .ConfigureAwait(false);
            await ErpLazySchema
                .EnsureProjectAccountingAsync(connection, cancellationToken)
                .ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("""
                SELECT b.project_id,
                       b.revenue_budget, b.cost_budget,
                       IFNULL(t.actual_cost,0) AS actual_cost,
                       IFNULL(t.actual_revenue,0) AS actual_revenue
                FROM (
                    SELECT IFNULL(`project_id`,0) AS project_id,
                           SUM(IFNULL(`revenue_budget`,0)) AS revenue_budget,
                           SUM(IFNULL(`cost_budget`,0)) AS cost_budget
                    FROM `epc_prja_budget`
                    GROUP BY IFNULL(`project_id`,0)
                ) b
                LEFT JOIN (
                    SELECT IFNULL(`project_id`,0) AS project_id,
                           SUM(CASE WHEN IFNULL(`amount`,0) >= 0 THEN IFNULL(`amount`,0) ELSE 0 END) AS actual_cost,
                           SUM(CASE WHEN IFNULL(`amount`,0) < 0 THEN -IFNULL(`amount`,0) ELSE 0 END) AS actual_revenue
                    FROM `epc_prja_txn`
                    GROUP BY IFNULL(`project_id`,0)
                ) t ON t.project_id = b.project_id
                UNION ALL
                SELECT t2.project_id, 0, 0,
                       t2.actual_cost, t2.actual_revenue
                FROM (
                    SELECT IFNULL(`project_id`,0) AS project_id,
                           SUM(CASE WHEN IFNULL(`amount`,0) >= 0 THEN IFNULL(`amount`,0) ELSE 0 END) AS actual_cost,
                           SUM(CASE WHEN IFNULL(`amount`,0) < 0 THEN -IFNULL(`amount`,0) ELSE 0 END) AS actual_revenue
                    FROM `epc_prja_txn`
                    GROUP BY IFNULL(`project_id`,0)
                ) t2
                WHERE t2.project_id NOT IN (
                    SELECT IFNULL(`project_id`,0) FROM `epc_prja_budget`
                )
                """);
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            var byProject = new Dictionary<long, ErpFitOutProjectPnlRow>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetInt64(0);
                var revenueBudget = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                var costBudget = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
                var actualCost = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
                var actualRevenue = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
                var forecastRevenue = revenueBudget > 0m ? revenueBudget : actualRevenue;
                var forecastCost = costBudget > 0m ? costBudget : actualCost;
                byProject[id] = new ErpFitOutProjectPnlRow(
                    id,
                    revenueBudget,
                    costBudget,
                    actualRevenue,
                    actualCost,
                    revenueBudget - costBudget,
                    actualRevenue - actualCost,
                    forecastRevenue,
                    forecastCost,
                    forecastRevenue - forecastCost);
            }

            var rows = byProject.Values
                .Where(row => !projectId.HasValue || row.ProjectId == projectId.Value)
                .OrderBy(row => row.ProjectId)
                .ToList();
            if (rows.Count == 0)
            {
                return ErpFitOutProjectPnlResult.Empty(
                    "No budgets or transactions for this selection.");
            }
            return new ErpFitOutProjectPnlResult(rows, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new ErpFitOutProjectPnlResult([], "database-error", ex.Message);
        }
    }
}
