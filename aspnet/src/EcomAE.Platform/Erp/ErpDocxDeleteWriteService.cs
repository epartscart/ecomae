namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_docx_delete</c> twin. Schema ensure, save, and reminder run stay PHP.
/// </summary>
public interface IErpDocxDeleteWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpDocxDeleteWriteService : IErpDocxDeleteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpDocxDeleteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A document id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_erp_doc_expiry_reminders` WHERE `doc_id` = ?"),
            cancellationToken,
            id);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_erp_doc_expiry` WHERE `id` = ?"),
            cancellationToken,
            id);
        return ErpSimpleWriteResult.Ok("Document removed from register", id);
    }
}
