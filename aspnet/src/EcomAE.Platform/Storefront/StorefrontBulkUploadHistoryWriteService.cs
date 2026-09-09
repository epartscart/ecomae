using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>PHP <c>epc_bulk_save_history</c> / <c>epc_bulk_update_history</c>. Schema ensure, CP review / quote / cart stay Classic.</summary>
public interface IStorefrontBulkUploadHistoryWriteService
{
    Task<StorefrontBulkUploadHistoryWriteResult> SaveAsync(
        int userId,
        bool isAdmin,
        StorefrontBulkUploadHistorySaveRequest request,
        CancellationToken cancellationToken = default);

    Task<StorefrontBulkUploadHistoryWriteResult> UpdateAsync(
        int userId,
        bool isAdmin,
        StorefrontBulkUploadHistoryUpdateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontBulkUploadHistorySaveRequest(
    string FileName,
    string Priority,
    StorefrontBulkUploadSummary Summary,
    IReadOnlyList<StorefrontBulkUploadRow> Rows,
    string Csv,
    int AdminGroupId = 0);

public sealed record StorefrontBulkUploadHistoryUpdateRequest(
    long UploadId,
    StorefrontBulkUploadSummary Summary,
    string ResultJson,
    string Csv);

public sealed record StorefrontBulkUploadHistoryWriteResult(
    bool Ok,
    string Status,
    string Code,
    string Message,
    long UploadId,
    int Writes)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        status = Ok,
        surface = "storefront",
        status_token = Status,
        writes = Writes,
        writesBlocked = false,
        cutoverAllowed = true,
        phpAuthoritative = false,
        validation_code = Code,
        would_write = Ok && Writes > 0,
        upload_id = UploadId,
        message = Message,
        note = Message,
        session
    };
}

public sealed class StorefrontBulkUploadHistoryWriteService : IStorefrontBulkUploadHistoryWriteService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontBulkUploadHistoryWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizePriority(string? priority)
        => string.Equals(priority, "delivery", StringComparison.OrdinalIgnoreCase) ? "delivery" : "price";

    public static string NormalizeFileName(string? fileName)
    {
        var raw = (fileName ?? string.Empty).Trim();
        return raw.Length <= 255 ? raw : raw[..255];
    }

    public static StorefrontBulkUploadSummary NormalizeSummary(StorefrontBulkUploadSummary? summary, int rowCount)
    {
        if (summary is null)
        {
            return new StorefrontBulkUploadSummary(rowCount, 0, 0, 0, 0);
        }

        var uploaded = summary.Uploaded > 0 ? summary.Uploaded : rowCount;
        return new StorefrontBulkUploadSummary(
            uploaded,
            Math.Max(0, summary.Available),
            Math.Max(0, summary.Cross),
            Math.Max(0, summary.Short),
            Math.Max(0, summary.Notfound));
    }

    public static string SerializeRows(IReadOnlyList<StorefrontBulkUploadRow> rows)
        => JsonSerializer.Serialize(rows, JsonOptions);

    public static bool TryParseSummary(string? json, int rowCount, out StorefrontBulkUploadSummary summary)
    {
        summary = new StorefrontBulkUploadSummary(rowCount, 0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(json))
        {
            return rowCount > 0;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<StorefrontBulkUploadSummary>(json, JsonOptions);
            summary = NormalizeSummary(parsed, rowCount);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task<StorefrontBulkUploadHistoryWriteResult> SaveAsync(
        int userId,
        bool isAdmin,
        StorefrontBulkUploadHistorySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId <= 0 && !isAdmin)
        {
            return Fail("auth", "Please log in first.");
        }

        if (request.Rows.Count == 0)
        {
            return Fail("invalid", "No valid rows found. Use Brand, Part Number, Qty columns.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        var summary = NormalizeSummary(request.Summary, request.Rows.Count);
        var fileName = NormalizeFileName(request.FileName);
        var priority = NormalizePriority(request.Priority);
        var csv = request.Csv ?? string.Empty;
        var resultJson = SerializeRows(request.Rows);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
            {
                return Fail("schema", "epc_bulk_upload_history is not provisioned. Schema ensure stays on the Classic twin.");
            }

            var groupId = await ResolveGroupIdAsync(connection, userId, isAdmin, request.AdminGroupId, cancellationToken)
                .ConfigureAwait(false);
            var writes = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_bulk_upload_history`
                    (`user_id`, `created_by_admin`, `group_id`, `file_name`, `priority`, `source`,
                     `uploaded_count`, `available_count`, `cross_count`, `short_count`, `notfound_count`,
                     `result_json`, `csv_result`, `created_at`, `updated_at`)
                    VALUES (?, ?, ?, ?, ?, 'storefront', ?, ?, ?, ?, ?, ?, ?, NOW(), NOW())
                    """),
                cancellationToken,
                userId,
                isAdmin ? 1 : 0,
                groupId,
                fileName,
                priority,
                summary.Uploaded,
                summary.Available,
                summary.Cross,
                summary.Short,
                summary.Notfound,
                resultJson,
                csv).ConfigureAwait(false);
            if (writes <= 0)
            {
                return Fail("db", "History row was not inserted.");
            }

            var uploadId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return new StorefrontBulkUploadHistoryWriteResult(
                true,
                "ok",
                "ok",
                "Bulk upload history saved.",
                uploadId,
                writes);
        }
        catch (Exception ex)
        {
            return Fail("db", ex.Message);
        }
    }

    public async Task<StorefrontBulkUploadHistoryWriteResult> UpdateAsync(
        int userId,
        bool isAdmin,
        StorefrontBulkUploadHistoryUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UploadId <= 0)
        {
            return Fail("invalid", "History update data is invalid.");
        }

        if (userId <= 0 && !isAdmin)
        {
            return Fail("auth", "Please log in first.");
        }

        if (string.IsNullOrWhiteSpace(request.ResultJson))
        {
            return Fail("invalid", "History update data is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        var summary = NormalizeSummary(request.Summary, 0);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
            {
                return Fail("schema", "epc_bulk_upload_history is not provisioned. Schema ensure stays on the Classic twin.");
            }

            var owned = isAdmin
                ? await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_bulk_upload_history` WHERE `id` = ?"),
                    cancellationToken,
                    request.UploadId).ConfigureAwait(false)
                : await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_bulk_upload_history` WHERE `id` = ? AND `user_id` = ?"),
                    cancellationToken,
                    request.UploadId,
                    userId).ConfigureAwait(false);
            if (owned <= 0)
            {
                return Fail("not_found", "History row was not found.");
            }

            int writes;
            if (isAdmin)
            {
                writes = await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_bulk_upload_history`
                        SET `uploaded_count` = ?, `available_count` = ?, `cross_count` = ?,
                            `short_count` = ?, `notfound_count` = ?, `result_json` = ?,
                            `csv_result` = ?, `updated_at` = NOW()
                        WHERE `id` = ?
                        LIMIT 1
                        """),
                    cancellationToken,
                    summary.Uploaded,
                    summary.Available,
                    summary.Cross,
                    summary.Short,
                    summary.Notfound,
                    request.ResultJson,
                    request.Csv ?? string.Empty,
                    request.UploadId).ConfigureAwait(false);
            }
            else
            {
                writes = await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_bulk_upload_history`
                        SET `uploaded_count` = ?, `available_count` = ?, `cross_count` = ?,
                            `short_count` = ?, `notfound_count` = ?, `result_json` = ?,
                            `csv_result` = ?, `updated_at` = NOW()
                        WHERE `id` = ? AND `user_id` = ?
                        LIMIT 1
                        """),
                    cancellationToken,
                    summary.Uploaded,
                    summary.Available,
                    summary.Cross,
                    summary.Short,
                    summary.Notfound,
                    request.ResultJson,
                    request.Csv ?? string.Empty,
                    request.UploadId,
                    userId).ConfigureAwait(false);
            }

            if (writes <= 0)
            {
                return Fail("unchanged", "History row was not updated.");
            }

            return new StorefrontBulkUploadHistoryWriteResult(
                true,
                "ok",
                "ok",
                "Bulk upload history updated.",
                request.UploadId,
                writes);
        }
        catch (Exception ex)
        {
            return Fail("db", ex.Message);
        }
    }

    private static async Task<int> ResolveGroupIdAsync(
        DbConnection connection,
        int userId,
        bool isAdmin,
        int adminGroupId,
        CancellationToken cancellationToken)
    {
        if (isAdmin && adminGroupId > 0)
        {
            var profile = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `group_id` FROM `epc_price_profiles` WHERE `group_id` = ? LIMIT 1"),
                cancellationToken,
                adminGroupId).ConfigureAwait(false);
            if (profile > 0)
            {
                return (int)profile;
            }
        }

        if (userId <= 0)
        {
            return 0;
        }

        var bound = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? ORDER BY `group_id` ASC LIMIT 1"),
            cancellationToken,
            userId).ConfigureAwait(false);
        return (int)bound;
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            "epc_bulk_upload_history").ConfigureAwait(false);
        return n > 0;
    }

    private static StorefrontBulkUploadHistoryWriteResult Fail(string code, string message)
        => new(false, "error", code, message, 0, 0);
}
