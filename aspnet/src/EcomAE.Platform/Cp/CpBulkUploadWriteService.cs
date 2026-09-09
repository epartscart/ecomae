using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_bulk_cp.php</c> twin of <c>epc_bulk_mark_reviewed</c>.
/// process_upload, quote, cart, and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpBulkUploadWriteService
{
    Task<ErpSimpleWriteResult> MarkReviewedAsync(
        CpBulkUploadMarkReviewedRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpBulkUploadMarkReviewedRequest(
    long UploadId,
    int AdminUserId,
    string? Notes);

public sealed class CpBulkUploadWriteService : ICpBulkUploadWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpBulkUploadWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>mb_substr(trim($notes), 0, 512)</c>.</summary>
    public static string NormalizeNotes(string? notes)
    {
        var raw = (notes ?? string.Empty).Trim();
        return raw.Length > 512 ? raw[..512] : raw;
    }

    public async Task<ErpSimpleWriteResult> MarkReviewedAsync(
        CpBulkUploadMarkReviewedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UploadId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Upload id required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var notes = NormalizeNotes(request.Notes);
        var adminId = request.AdminUserId < 0 ? 0 : request.AdminUserId;

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var updated = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    "UPDATE `epc_bulk_upload_history` SET `cp_reviewed_at`=NOW(), `cp_reviewed_by`=?, `cp_notes`=?, `updated_at`=NOW() WHERE `id`=?"),
                cancellationToken, adminId, notes, request.UploadId).ConfigureAwait(false);
            if (updated <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Update failed");
            }

            return ErpSimpleWriteResult.Ok("Marked reviewed", request.UploadId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Bulk-upload history table is missing — schema-ensure stays Classic.");
        }
    }
}
