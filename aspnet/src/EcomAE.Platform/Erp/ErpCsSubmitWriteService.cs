namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cs_submit_declaration</c> twin. Save, delete, PDF import, and get stay PHP.
/// </summary>
public interface IErpCsSubmitWriteService
{
    Task<ErpSimpleWriteResult> SubmitAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpCsSubmitWriteService : IErpCsSubmitWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCsSubmitWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SubmitAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A declaration id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_custom_shipping_declarations` SET `status` = 'submitted', `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            id);
        return ErpSimpleWriteResult.Ok("Declaration submitted", id);
    }
}
