namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_pf_case_cancel</c> twin. Case start, act, reassign, and seed stay PHP.
/// </summary>
public interface IErpPfCaseCancelWriteService
{
    Task<ErpSimpleWriteResult> CancelAsync(long caseId, CancellationToken cancellationToken = default);
}

public sealed class ErpPfCaseCancelWriteService : IErpPfCaseCancelWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPfCaseCancelWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CancelAsync(long caseId, CancellationToken cancellationToken = default)
    {
        if (caseId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A process-flow case id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_pf_cases` SET `status` = 'cancelled', `completed_at` = ?, `time_updated` = ? WHERE `id` = ? AND `status` = 'open'"),
            cancellationToken,
            now, now, caseId);
        return ErpSimpleWriteResult.Ok("Case cancelled", caseId);
    }
}
