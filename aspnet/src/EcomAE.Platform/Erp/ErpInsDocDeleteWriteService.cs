namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ins_doc_delete</c> twin. Schema ensure, policy save, and doc add stay PHP.
/// </summary>
public interface IErpInsDocDeleteWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpInsDocDeleteWriteService : IErpInsDocDeleteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpInsDocDeleteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An insurance document id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_erp_ins_documents` WHERE `id` = ?"),
            cancellationToken,
            id);
        return ErpSimpleWriteResult.Ok("Document removed", id);
    }
}
