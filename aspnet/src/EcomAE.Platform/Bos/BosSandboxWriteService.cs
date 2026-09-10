using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>config_sandbox</c> <c>promote</c> / <c>epc_sandbox_promote</c>
/// and <c>discard</c> / <c>epc_sandbox_discard</c>.
/// Create, apply-change, rollback, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/not-found/only-active checks.
/// </summary>
public interface IBosSandboxWriteService
{
    Task<ErpSimpleWriteResult> PromoteAsync(
        long snapshotId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DiscardAsync(
        long snapshotId,
        CancellationToken cancellationToken = default);
}

public sealed class BosSandboxWriteService : IBosSandboxWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosSandboxWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public Task<ErpSimpleWriteResult> PromoteAsync(
        long snapshotId,
        CancellationToken cancellationToken = default)
        => SetStatusAsync(
            snapshotId,
            """
            UPDATE `epc_config_snapshots` SET `status`='promoted', `promoted_at`=NOW() WHERE `id`=? AND `status`='active'
            """,
            "Sandbox snapshot promoted",
            cancellationToken);

    public Task<ErpSimpleWriteResult> DiscardAsync(
        long snapshotId,
        CancellationToken cancellationToken = default)
        => SetStatusAsync(
            snapshotId,
            """
            UPDATE `epc_config_snapshots` SET `status`='discarded' WHERE `id`=? AND `status`='active'
            """,
            "Sandbox snapshot discarded",
            cancellationToken);

    private async Task<ErpSimpleWriteResult> SetStatusAsync(
        long snapshotId,
        string sql,
        string okMessage,
        CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(sql),
                cancellationToken, snapshotId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(okMessage, snapshotId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Sandbox table is missing — schema-ensure stays Classic.");
        }
    }
}
