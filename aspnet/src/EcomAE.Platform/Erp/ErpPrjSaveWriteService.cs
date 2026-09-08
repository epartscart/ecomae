using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_prj_save</c> twin. INSERT/UPDATE <c>epc_prj_projects</c>.
/// Schema ensure, task save, and timesheet log stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpPrjSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPrjSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPrjSaveWriteRequest(
    long Id = 0,
    string? Code = null,
    string? Name = null,
    long CustomerId = 0,
    string? BillingType = null,
    decimal BudgetCost = 0,
    decimal ContractValue = 0,
    string? Status = null);

public sealed class ErpPrjSaveWriteService : IErpPrjSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPrjSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPrjSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project code is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        code = Clip(code, 40);
        var name = Clip((request.Name ?? string.Empty).Trim(), 160);
        var billing = Clip((request.BillingType ?? string.Empty).Trim(), 12);
        if (billing.Length == 0)
        {
            billing = "tm";
        }

        var status = Clip((request.Status ?? string.Empty).Trim(), 12);
        if (status.Length == 0)
        {
            status = "open";
        }

        var customerId = request.CustomerId < 0 ? 0 : request.CustomerId;
        var budget = decimal.Round(request.BudgetCost, 2, MidpointRounding.AwayFromZero);
        var contract = decimal.Round(request.ContractValue, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_prj_projects", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_prj_projects", "billing_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_prj_projects", "contract_value", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_prj_projects` SET `name`=?, `customer_id`=?, `billing_type`=?, `budget_cost`=?, `contract_value`=?, `status`=? WHERE `id`=?"),
                cancellationToken,
                name,
                customerId,
                billing,
                budget,
                contract,
                status,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Project saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_prj_projects` (`code`,`name`,`customer_id`,`billing_type`,`budget_cost`,`contract_value`,`status`,`time_created`) VALUES (?,?,?,?,?,?, 'open', ?)"),
            cancellationToken,
            code,
            name,
            customerId,
            billing,
            budget,
            contract,
            now).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Project saved", inserted);
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
