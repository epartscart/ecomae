using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_as_job_create</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpAftersalesJobWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpAftersalesJobCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpAftersalesJobCreateRequest(
    string? JobNo = null,
    long CustomerId = 0,
    string? AssetRef = null,
    string? Complaint = null,
    bool UnderWarranty = false);

public sealed class ErpAftersalesJobWriteService : IErpAftersalesJobWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpAftersalesJobWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpAftersalesJobCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var complaint = (request.Complaint ?? string.Empty).Trim();
        var asset = (request.AssetRef ?? string.Empty).Trim();
        if (complaint.Length == 0 && asset.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Complaint or asset reference is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_as_jobs", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "After-sales job tables are not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var jobNo = (request.JobNo ?? string.Empty).Trim();
        if (jobNo.Length == 0)
        {
            jobNo = "JOB-" + now.ToString(CultureInfo.InvariantCulture);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_as_jobs` (`job_no`,`customer_id`,`asset_ref`,`complaint`,`status`,`under_warranty`,`time_created`,`time_updated`) VALUES (?, ?, ?, ?, 'open', ?, ?, ?)"),
            cancellationToken,
            Clip(jobNo, 40),
            request.CustomerId < 0 ? 0 : request.CustomerId,
            Clip(asset, 120),
            complaint,
            request.UnderWarranty ? 1 : 0,
            now,
            now).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        if ((request.JobNo ?? string.Empty).Trim().Length == 0 && id > 0)
        {
            jobNo = "JOB-" + id.ToString(CultureInfo.InvariantCulture);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_as_jobs` SET `job_no` = ? WHERE `id` = ?"),
                cancellationToken,
                jobNo,
                id).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Service job " + jobNo + " created", id);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
