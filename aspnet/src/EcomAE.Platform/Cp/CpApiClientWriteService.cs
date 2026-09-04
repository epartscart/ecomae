using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_api_clients_manage.php</c> twins for revoke / activate.
/// Create / rotate / update stay PHP (key minting).
/// </summary>
public interface ICpApiClientWriteService
{
    Task<ErpSimpleWriteResult> SetActiveAsync(long clientId, int active, CancellationToken cancellationToken = default);
}

public sealed class CpApiClientWriteService : ICpApiClientWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpApiClientWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetActiveAsync(
        long clientId,
        int active,
        CancellationToken cancellationToken = default)
    {
        if (clientId <= 0 || active is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A client id and active 0 or 1 are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_api_clients` SET `active` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            active, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), clientId);
        return ErpSimpleWriteResult.Ok(
            active == 0 ? "Client revoked (active = 0)." : "Client re-activated.",
            clientId);
    }
}
