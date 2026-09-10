using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>po_approval</c> <c>cancel</c> / <c>epc_po_cancel</c>,
/// <c>reject</c> / <c>epc_po_reject</c>, and <c>approve</c> / <c>epc_po_approve</c>.
/// Create and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosPoWriteService
{
    Task<ErpSimpleWriteResult> CancelAsync(
        long poId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RejectAsync(
        long poId,
        int tier,
        long approverId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ApproveAsync(
        long poId,
        int tier,
        long approverId,
        string? comment,
        CancellationToken cancellationToken = default);
}

public sealed class BosPoWriteService : IBosPoWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosPoWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CancelAsync(
        long poId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var status = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `status` FROM `epc_po_requests` WHERE `id` = ?"),
                cancellationToken, poId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(status))
            {
                return ErpSimpleWriteResult.Fail("invalid", "PO not found");
            }

            if (string.Equals(status, "approved", StringComparison.Ordinal))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Cannot cancel approved PO");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_po_requests` SET `status` = 'cancelled' WHERE `id` = ?
                    """),
                cancellationToken, poId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("PO cancelled", poId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "PO requests table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> RejectAsync(
        long poId,
        int tier,
        long approverId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var rejectionReason = reason ?? string.Empty;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var status = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `status` FROM `epc_po_requests` WHERE `id` = ?"),
                cancellationToken, poId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(status))
            {
                return ErpSimpleWriteResult.Fail("invalid", "PO not found");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_po_approval_steps`
                    SET `decision` = 'rejected', `approver_id` = ?, `comment` = ?, `decided_at` = NOW()
                    WHERE `po_id` = ? AND `tier` = ?
                    """),
                cancellationToken, approverId, rejectionReason, poId, tier).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_po_requests` SET `status` = 'rejected', `rejected_at` = NOW(), `rejection_reason` = ?
                    WHERE `id` = ?
                    """),
                cancellationToken, rejectionReason, poId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("PO rejected", poId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "PO requests table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> ApproveAsync(
        long poId,
        int tier,
        long approverId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var approvalComment = comment ?? string.Empty;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var status = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `status` FROM `epc_po_requests` WHERE `id` = ?"),
                cancellationToken, poId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(status))
            {
                return ErpSimpleWriteResult.Fail("invalid", "PO not found");
            }

            if (!string.Equals(status, "pending", StringComparison.Ordinal))
            {
                return ErpSimpleWriteResult.Fail("invalid", "PO is not pending approval");
            }

            var currentTier = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `current_tier` FROM `epc_po_requests` WHERE `id` = ?"),
                cancellationToken, poId).ConfigureAwait(false);
            if (currentTier != tier)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Not the current approval tier");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_po_approval_steps`
                    SET `decision` = 'approved', `approver_id` = ?, `comment` = ?, `decided_at` = NOW()
                    WHERE `po_id` = ? AND `tier` = ? AND `decision` = 'pending'
                    """),
                cancellationToken, approverId, approvalComment, poId, tier).ConfigureAwait(false);

            var nextTier = tier + 1;
            var hasNext = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_po_approval_steps` WHERE `po_id` = ? AND `tier` = ?"),
                cancellationToken, poId, nextTier).ConfigureAwait(false) > 0;
            if (hasNext)
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_po_requests` SET `current_tier` = ? WHERE `id` = ?"),
                    cancellationToken, nextTier, poId).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("PO tier approved", poId);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_po_requests` SET `status` = 'approved', `approved_at` = NOW() WHERE `id` = ?
                    """),
                cancellationToken, poId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("PO fully approved", poId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "PO requests table is missing — schema-ensure stays Classic.");
        }
    }
}
