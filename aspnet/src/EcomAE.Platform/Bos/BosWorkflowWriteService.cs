using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>workflow_builder</c> <c>toggle</c> / <c>epc_workflow_toggle</c>.
/// Create, execute, delete, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/not-found checks.
/// </summary>
public interface IBosWorkflowWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(
        long workflowId,
        bool active,
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
}
