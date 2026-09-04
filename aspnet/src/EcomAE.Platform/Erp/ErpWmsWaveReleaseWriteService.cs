namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wms_wave_release</c> twin. Schema ensure, wave create, pick add,
/// and work complete stay PHP.
/// </summary>
public interface IErpWmsWaveReleaseWriteService
{
    Task<ErpSimpleWriteResult> ReleaseAsync(long waveId, CancellationToken cancellationToken = default);
}

public sealed class ErpWmsWaveReleaseWriteService : IErpWmsWaveReleaseWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWmsWaveReleaseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ReleaseAsync(
        long waveId,
        CancellationToken cancellationToken = default)
    {
        if (waveId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A warehouse wave id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_wms_waves` SET `status` = 'released', `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), waveId);
        return ErpSimpleWriteResult.Ok("Wave released", waveId);
    }
}
