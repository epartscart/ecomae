using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_qm_plan_save</c> / ajax <c>qm_plan_save</c> twin.
/// UPDATE <c>epc_qm_plan</c> when <c>id</c> &gt; 0, else UPSERT on <c>company_id</c>+<c>code</c>.
/// Test add, orders, NCR, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpQmPlanSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpQmPlanSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpQmPlanSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    int? Active = null);

public sealed class ErpQmPlanSaveWriteService : IErpQmPlanSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpQmPlanSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpQmPlanSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Plan code is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var name = request.Name ?? string.Empty;
        var active = request.Active is null || request.Active == 0 ? 0 : 1;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_qm_plan", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_qm_plan", "name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Test plan table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_qm_plan` SET `name`=?, `active`=?, `time_updated`=? WHERE `id`=? AND `company_id`=?"),
                cancellationToken,
                name,
                active,
                now,
                request.Id,
                companyId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Test plan saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_qm_plan` (`company_id`,`code`,`name`,`active`,`time_updated`) VALUES (?,?,?,?,?) "
                + "ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `active`=VALUES(`active`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            companyId,
            code,
            name,
            active,
            now).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_qm_plan` WHERE `company_id`=? AND `code`=? LIMIT 1"),
            cancellationToken,
            companyId,
            code).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Test plan saved", id);
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
