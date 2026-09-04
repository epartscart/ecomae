namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ins_claim_set_status</c> twin. Schema ensure and claim save stay PHP.
/// </summary>
public interface IErpInsClaimStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(long id, string? status, CancellationToken cancellationToken = default);
}

public sealed class ErpInsClaimStatusWriteService : IErpInsClaimStatusWriteService
{
    internal static readonly string[] Allowed = ["notified", "survey", "docs", "assessed", "settled", "rejected", "closed"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpInsClaimStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long id,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A claim id is required.");
        }

        var next = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!Allowed.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid claim status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_ins_claims` SET `status` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            next, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), id);
        return ErpSimpleWriteResult.Ok("Claim status updated", id);
    }
}
