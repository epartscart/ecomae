using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_config_sandbox.php</c> twin of <c>epc_sandbox_promote</c>
/// and <c>epc_sandbox_discard</c>. Status only — create, apply-change, rollback,
/// and schema-ensure stay Classic. This service does not invent a send.
/// </summary>
public interface ICpConfigSandboxWriteService
{
    Task<ErpSimpleWriteResult> PromoteAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DiscardAsync(
        long id,
        CancellationToken cancellationToken = default);
}

public sealed class CpConfigSandboxWriteService : ICpConfigSandboxWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpConfigSandboxWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public Task<ErpSimpleWriteResult> PromoteAsync(long id, CancellationToken cancellationToken = default)
        => SetStatusAsync(id, "promoted", "Snapshot promoted.", cancellationToken);

    public Task<ErpSimpleWriteResult> DiscardAsync(long id, CancellationToken cancellationToken = default)
        => SetStatusAsync(id, "discarded", "Snapshot discarded.", cancellationToken);

    private async Task<ErpSimpleWriteResult> SetStatusAsync(
        long id,
        string status,
        string okMessage,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Snapshot id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var current = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT IFNULL(`status`,'') FROM `epc_config_snapshots` WHERE `id`=? LIMIT 1"),
                cancellationToken, id).ConfigureAwait(false);
            if (current is null)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Snapshot not found");
            }

            if (!string.Equals(current, "active", StringComparison.OrdinalIgnoreCase))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Only an active snapshot can be promoted or discarded");
            }

            var sql = string.Equals(status, "promoted", StringComparison.Ordinal)
                ? "UPDATE `epc_config_snapshots` SET `status`='promoted', `promoted_at`=NOW() WHERE `id`=? AND `status`='active'"
                : "UPDATE `epc_config_snapshots` SET `status`='discarded' WHERE `id`=? AND `status`='active'";
            var writes = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(sql),
                cancellationToken, id).ConfigureAwait(false);
            if (writes <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Only an active snapshot can be promoted or discarded");
            }

            return ErpSimpleWriteResult.Ok(okMessage, id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Sandbox table is missing — schema-ensure stays Classic.");
        }
    }
}
