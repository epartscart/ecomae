using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_as_job_create</c> / <c>epc_as_job_add_line</c> / <c>epc_as_job_close</c> twin.
/// Schema-ensure stays PHP.
/// </summary>
public interface IErpAftersalesJobWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpAftersalesJobCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddLineAsync(
        ErpAftersalesJobAddLineRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CloseAsync(
        ErpAftersalesJobCloseRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpAftersalesJobCreateRequest(
    string? JobNo = null,
    long CustomerId = 0,
    string? AssetRef = null,
    string? Complaint = null,
    bool UnderWarranty = false);

public sealed record ErpAftersalesJobAddLineRequest(
    long JobId = 0,
    string? LineType = null,
    string? Description = null,
    long ItemId = 0,
    decimal Qty = 0,
    decimal UnitPrice = 0,
    decimal TaxPercent = 0,
    bool Chargeable = true);

public sealed record ErpAftersalesJobCloseRequest(long JobId = 0);

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

    public async Task<ErpSimpleWriteResult> AddLineAsync(
        ErpAftersalesJobAddLineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.JobId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job id is required.");
        }

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length == 0 && request.ItemId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Description or item id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_as_jobs", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_as_job_lines", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "After-sales job tables are not provisioned");
        }

        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_as_jobs` WHERE `id` = ?"),
            cancellationToken,
            request.JobId).ConfigureAwait(false);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job not found");
        }

        var lineType = NormalizeLineType(request.LineType);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_as_job_lines` (`job_id`,`line_type`,`description`,`item_id`,`qty`,`unit_price`,`tax_percent`,`chargeable`) VALUES (?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.JobId,
            lineType,
            Clip(description, 190),
            request.ItemId < 0 ? 0 : request.ItemId,
            request.Qty,
            request.UnitPrice,
            request.TaxPercent,
            request.Chargeable ? 1 : 0).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        await RecalcJobAsync(connection, request.JobId, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Job line added", id);
    }

    public async Task<ErpSimpleWriteResult> CloseAsync(
        ErpAftersalesJobCloseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.JobId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job id is required.");
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

        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_as_jobs` WHERE `id` = ?"),
            cancellationToken,
            request.JobId).ConfigureAwait(false);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job not found");
        }

        if (await TableExistsAsync(connection, "epc_as_job_lines", cancellationToken).ConfigureAwait(false))
        {
            await RecalcJobAsync(connection, request.JobId, cancellationToken).ConfigureAwait(false);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_as_jobs` SET `status` = 'closed', `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            now,
            request.JobId).ConfigureAwait(false);

        return ErpSimpleWriteResult.Ok("Service job closed", request.JobId);
    }

    private static async Task RecalcJobAsync(DbConnection connection, long jobId, CancellationToken cancellationToken)
    {
        var parts = decimal.Round(
            await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT COALESCE(SUM(CASE WHEN `chargeable` = 1 AND IFNULL(`line_type`,'') <> 'labour' THEN `qty` * `unit_price` ELSE 0 END), 0) FROM `epc_as_job_lines` WHERE `job_id` = ?"),
                cancellationToken,
                jobId).ConfigureAwait(false),
            2,
            MidpointRounding.AwayFromZero);
        var labour = decimal.Round(
            await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT COALESCE(SUM(CASE WHEN `chargeable` = 1 AND `line_type` = 'labour' THEN `qty` * `unit_price` ELSE 0 END), 0) FROM `epc_as_job_lines` WHERE `job_id` = ?"),
                cancellationToken,
                jobId).ConfigureAwait(false),
            2,
            MidpointRounding.AwayFromZero);
        var tax = decimal.Round(
            await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT COALESCE(SUM(CASE WHEN `chargeable` = 1 THEN `qty` * `unit_price` * `tax_percent` / 100 ELSE 0 END), 0) FROM `epc_as_job_lines` WHERE `job_id` = ?"),
                cancellationToken,
                jobId).ConfigureAwait(false),
            2,
            MidpointRounding.AwayFromZero);
        var grand = decimal.Round(parts + labour + tax, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_as_jobs` SET `parts_total` = ?, `labour_total` = ?, `tax_total` = ?, `grand_total` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            parts,
            labour,
            tax,
            grand,
            now,
            jobId).ConfigureAwait(false);
    }

    private static string NormalizeLineType(string? value)
    {
        var raw = (value ?? string.Empty).Trim().ToLowerInvariant();
        return raw == "labour" ? "labour" : "part";
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
