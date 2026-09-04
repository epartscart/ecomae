namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_compliance_disable_obligation</c> twin. Add, file, and retention stay PHP.
/// </summary>
public interface IErpBosComplianceDisableObligationWriteService
{
    Task<ErpSimpleWriteResult> DisableAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpBosComplianceDisableObligationWriteService : IErpBosComplianceDisableObligationWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosComplianceDisableObligationWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DisableAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A compliance obligation id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_bos_compliance_obligations` SET `active` = 0 WHERE `id` = ?"),
            cancellationToken,
            id);
        return ErpSimpleWriteResult.Ok("Obligation disabled", id);
    }
}
