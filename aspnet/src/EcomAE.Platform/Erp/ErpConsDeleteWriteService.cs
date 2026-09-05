namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cons_entity_delete</c> / <c>epc_cons_ic_delete</c> twins.
/// Schema ensure, figures, and IC save stay PHP. Entity save is <c>IErpConsEntitySaveWriteService</c>.
/// </summary>
public interface IErpConsDeleteWriteService
{
    Task<ErpSimpleWriteResult> DeleteEntityAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteIcAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpConsDeleteWriteService : IErpConsDeleteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpConsDeleteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public Task<ErpSimpleWriteResult> DeleteEntityAsync(long id, CancellationToken cancellationToken = default)
        => DeleteAsync(id, "A consolidation entity id is required.", "DELETE FROM `epc_cons_entities` WHERE `id` = ?", "Entity removed", cancellationToken);

    public Task<ErpSimpleWriteResult> DeleteIcAsync(long id, CancellationToken cancellationToken = default)
        => DeleteAsync(id, "An intercompany transaction id is required.", "DELETE FROM `epc_cons_ic` WHERE `id` = ?", "Intercompany transaction removed", cancellationToken);

    private async Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        string missingId,
        string sql,
        string okMessage,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", missingId);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(sql), cancellationToken, id);
        return ErpSimpleWriteResult.Ok(okMessage, id);
    }
}
