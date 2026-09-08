using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hrt_applicant_add</c> / ajax <c>hrt_applicant_add</c> twin.
/// INSERT <c>epc_hrt_applicant</c> after the job row exists.
/// Job save, stage, reviews, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpHrtApplicantAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpHrtApplicantAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpHrtApplicantAddWriteRequest(
    long JobId = 0,
    string? Name = null,
    string? Email = null,
    string? Phone = null,
    int Rating = 0,
    string? Notes = null);

public sealed class ErpHrtApplicantAddWriteService : IErpHrtApplicantAddWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrtApplicantAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpHrtApplicantAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.JobId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job requisition not found");
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Applicant name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var email = request.Email ?? string.Empty;
        var phone = request.Phone ?? string.Empty;
        var notes = request.Notes ?? string.Empty;
        var rating = request.Rating;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_hrt_job", "title", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_hrt_applicant", "name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Applicant table is not provisioned");
        }

        var companyObj = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `company_id` FROM `epc_hrt_job` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.JobId).ConfigureAwait(false);
        if (companyObj is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job requisition not found");
        }

        var companyId = Convert.ToInt64(companyObj, CultureInfo.InvariantCulture);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_hrt_applicant` (`job_id`,`company_id`,`name`,`email`,`phone`,`stage`,`rating`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,'applied',?,?,?,?)"),
            cancellationToken,
            request.JobId,
            companyId,
            name,
            email,
            phone,
            rating,
            notes,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Applicant added", id);
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
