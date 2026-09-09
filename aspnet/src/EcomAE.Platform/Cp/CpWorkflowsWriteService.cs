using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_workflow_toggle</c> twin from BOS <c>ajax_epc_bos.php</c> sub-action
/// <c>toggle</c>. Tick / execute / builder save stay Classic.
/// Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpWorkflowsWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(
        long id,
        bool active,
        CancellationToken cancellationToken = default);
}

public sealed class CpWorkflowsWriteService : ICpWorkflowsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpWorkflowsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ToggleAsync(
        long id,
        bool active,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Workflow id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var existing = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_workflows` WHERE `id`=? LIMIT 1"),
                cancellationToken, id).ConfigureAwait(false);
            if (existing <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Workflow not found");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_workflows` SET `active`=? WHERE `id`=?"),
                cancellationToken, active ? 1 : 0, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(active ? "Workflow enabled" : "Workflow disabled", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Workflows table is missing — schema-ensure stays Classic.");
        }
    }
}
