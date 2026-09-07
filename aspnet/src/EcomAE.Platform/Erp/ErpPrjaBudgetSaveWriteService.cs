using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_prja_budget_save</c> twin. INSERT/UPDATE <c>epc_prja_budget</c>.
/// Transaction add, recognition, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpPrjaBudgetSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPrjaBudgetSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPrjaBudgetSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    long ProjectId = 0,
    string? Category = null,
    decimal CostBudget = 0,
    decimal RevenueBudget = 0);

public sealed class ErpPrjaBudgetSaveWriteService : IErpPrjaBudgetSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPrjaBudgetSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPrjaBudgetSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var category = Clip(string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim(), 60);
        var cost = decimal.Round(request.CostBudget, 2, MidpointRounding.AwayFromZero);
        var revenue = decimal.Round(request.RevenueBudget, 2, MidpointRounding.AwayFromZero);
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var projectId = request.ProjectId < 0 ? 0 : request.ProjectId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_prja_budget", "category", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_prja_budget", "cost_budget", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project budget table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_prja_budget` SET `project_id`=?, `category`=?, `cost_budget`=?, `revenue_budget`=? WHERE `id`=?"),
                cancellationToken,
                projectId,
                category,
                cost,
                revenue,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Budget line saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_prja_budget` (`company_id`,`project_id`,`category`,`cost_budget`,`revenue_budget`,`time_created`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            projectId,
            category,
            cost,
            revenue,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Budget line saved", id);
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

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
