using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>workflow_builder</c> <c>toggle</c> / <c>epc_workflow_toggle</c>
/// and <c>delete</c> / <c>epc_workflow_delete</c>.
/// Create, execute, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/not-found checks.
/// </summary>
public interface IBosWorkflowWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(
        long workflowId,
        bool active,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(
        long workflowId,
        CancellationToken cancellationToken = default);
}

public sealed class BosWorkflowWriteService : IBosWorkflowWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosWorkflowWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>(bool)($_POST['active'] ?? false)</c> — any non-empty string is true, including <c>0</c>.</summary>
    public static bool PhpPostedBool(string? raw)
        => !string.IsNullOrEmpty(raw);

    public async Task<ErpSimpleWriteResult> ToggleAsync(
        long workflowId,
        bool active,
        CancellationToken cancellationToken = default)
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
                ErpDb.Positional("UPDATE `epc_workflows` SET `active` = ? WHERE `id` = ?"),
                cancellationToken, active ? 1 : 0, workflowId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Workflow toggled", workflowId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Workflows table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long workflowId,
        CancellationToken cancellationToken = default)
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
                ErpDb.Positional("DELETE FROM `epc_workflow_steps` WHERE `workflow_id` = ?"),
                cancellationToken, workflowId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_workflows` WHERE `id` = ?"),
                cancellationToken, workflowId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Workflow deleted", workflowId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Workflows table is missing — schema-ensure stays Classic.");
        }
    }
}
