namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_pf_step_delete</c> twin. Process/step save, case start, and seed stay PHP.
/// </summary>
public interface IErpPfStepDeleteWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(long stepId, CancellationToken cancellationToken = default);
}

public sealed class ErpPfStepDeleteWriteService : IErpPfStepDeleteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPfStepDeleteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long stepId, CancellationToken cancellationToken = default)
    {
        if (stepId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A process-flow step id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_pf_steps` WHERE `id` = ?"),
            cancellationToken,
            stepId);
        return ErpSimpleWriteResult.Ok("Step removed", stepId);
    }
}
