using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>warranty_rma</c> <c>rma_transition</c> / <c>epc_rma_transition</c>.
/// Create, register, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosRmaWriteService
{
    Task<ErpSimpleWriteResult> TransitionAsync(
        long rmaId,
        string? status,
        string? notes,
        CancellationToken cancellationToken = default);
}

public sealed class BosRmaWriteService : IBosRmaWriteService
{
    private static readonly Dictionary<string, string[]> Valid = new(StringComparer.Ordinal)
    {
        ["pending"] = ["approved", "rejected", "cancelled"],
        ["approved"] = ["received", "cancelled"],
        ["received"] = ["inspecting"],
        ["inspecting"] = ["repair", "replacement", "refund", "rejected"],
        ["repair"] = ["completed"],
        ["replacement"] = ["completed"],
        ["refund"] = ["completed"],
    };

    private readonly IErpWriteConnectionFactory _connections;

    public BosRmaWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> TransitionAsync(
        long rmaId,
        string? status,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var newStatus = status ?? string.Empty;
        var noteSuffix = string.IsNullOrEmpty(notes) ? string.Empty : "\n" + notes;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var current = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `status` FROM `epc_rma_requests` WHERE `id` = ?"),
                cancellationToken, rmaId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(current))
            {
                return ErpSimpleWriteResult.Fail("invalid", "RMA not found");
            }

            if (!Valid.TryGetValue(current, out var allowed)
                || !allowed.Contains(newStatus, StringComparer.Ordinal))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Invalid transition: " + current + " → " + newStatus);
            }

            object? completedAt = string.Equals(newStatus, "completed", StringComparison.Ordinal)
                ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                : null;
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_rma_requests` SET `status` = ?, `resolution_notes` = CONCAT(IFNULL(`resolution_notes`,''), ?), `completed_at` = COALESCE(?, `completed_at`) WHERE `id` = ?
                    """),
                cancellationToken, newStatus, noteSuffix, completedAt, rmaId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("RMA transitioned", rmaId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "RMA requests table is missing — schema-ensure stays Classic.");
        }
    }
}
