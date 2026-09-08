using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hrt_job_save</c> / ajax <c>hrt_job_save</c> twin.
/// UPDATE <c>epc_hrt_job</c> when <c>id</c> &gt; 0, else INSERT with hired=0 status=open.
/// Applicant add, stage, reviews, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpHrtJobSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpHrtJobSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpHrtJobSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Title = null,
    string? Department = null,
    int Headcount = 1,
    string? HiringManager = null,
    string? Notes = null);

public sealed class ErpHrtJobSaveWriteService : IErpHrtJobSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrtJobSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpHrtJobSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job title is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var department = request.Department ?? string.Empty;
        var headcount = request.Headcount < 1 ? 1 : request.Headcount;
        var manager = request.HiringManager ?? string.Empty;
        var notes = request.Notes ?? string.Empty;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_hrt_job", "title", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_hrt_job", "department", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job requisition table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_hrt_job` SET `title`=?, `department`=?, `headcount`=?, `hiring_manager`=?, `notes`=? WHERE `id`=?"),
                cancellationToken,
                title,
                department,
                headcount,
                manager,
                notes,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Job requisition saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_hrt_job` (`company_id`,`title`,`department`,`headcount`,`hired`,`status`,`hiring_manager`,`notes`,`time_created`) VALUES (?,?,?,?,0,'open',?,?,?)"),
            cancellationToken,
            companyId,
            title,
            department,
            headcount,
            manager,
            notes,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Job requisition saved", id);
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
