using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bplan_save</c> / ajax <c>bplan_save</c> twin.
/// UPDATE <c>epc_bplan_plan</c> when <c>id</c> &gt; 0, else INSERT with stage=draft.
/// Worksheet lines, forecast positions, publish, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpBplanSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpBplanSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBplanSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Name = null,
    string? FiscalYear = null,
    string? Owner = null,
    string? Notes = null);

public sealed class ErpBplanSaveWriteService : IErpBplanSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpBplanSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpBplanSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Plan name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var year = request.FiscalYear ?? string.Empty;
        var owner = request.Owner ?? string.Empty;
        var notes = request.Notes ?? string.Empty;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bplan_plan", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bplan_plan", "fiscal_year", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Budget plan table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_bplan_plan` SET `name`=?, `fiscal_year`=?, `owner`=?, `notes`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                name,
                year,
                owner,
                notes,
                now,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Budget plan saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bplan_plan` (`company_id`,`name`,`fiscal_year`,`stage`,`owner`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,'draft',?,?,?,?)"),
            cancellationToken,
            companyId,
            name,
            year,
            owner,
            notes,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Budget plan saved", id);
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
