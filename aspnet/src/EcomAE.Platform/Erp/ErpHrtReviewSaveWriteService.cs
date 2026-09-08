using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hrt_review_save</c> / ajax <c>hrt_review_save</c> twin.
/// UPDATE <c>epc_hrt_review</c> when <c>id</c> &gt; 0, else INSERT with status=draft.
/// Goal add, finalize, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpHrtReviewSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpHrtReviewSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpHrtReviewSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    long EmployeeId = 0,
    string? EmployeeName = null,
    string? Period = null,
    string? Reviewer = null,
    string? Notes = null);

public sealed class ErpHrtReviewSaveWriteService : IErpHrtReviewSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrtReviewSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpHrtReviewSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = (request.EmployeeName ?? string.Empty).Trim();
        if (name.Length == 0 && request.EmployeeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Employee name or id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var employeeId = request.EmployeeId < 0 ? 0 : request.EmployeeId;
        var period = request.Period ?? string.Empty;
        var reviewer = request.Reviewer ?? string.Empty;
        var notes = request.Notes ?? string.Empty;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_hrt_review", "employee_name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Performance review table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_hrt_review` SET `employee_id`=?, `employee_name`=?, `period`=?, `reviewer`=?, `notes`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                employeeId,
                name,
                period,
                reviewer,
                notes,
                now,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Review saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_hrt_review` (`company_id`,`employee_id`,`employee_name`,`period`,`status`,`reviewer`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,'draft',?,?,?,?)"),
            cancellationToken,
            companyId,
            employeeId,
            name,
            period,
            reviewer,
            notes,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Review saved", id);
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
