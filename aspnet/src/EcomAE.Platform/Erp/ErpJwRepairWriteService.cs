namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>jw_repair_update_status</c> twin. Create and sample seed stay PHP.</summary>
public interface IErpJwRepairWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(long repairId, string? status, CancellationToken cancellationToken = default);
}

public sealed class ErpJwRepairWriteService : IErpJwRepairWriteService
{
    internal static readonly string[] AllowedStatuses =
    [
        "received",
        "in_progress",
        "ready",
        "delivered",
        "invoiced",
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwRepairWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long repairId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (repairId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid parameters");
        }

        var next = (status ?? string.Empty).Trim();
        if (next.Length == 0 || !AllowedStatuses.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", next.Length == 0 ? "Invalid parameters" : "Invalid status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_jw_repairs` SET `status` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            next, updatedAt, repairId);
        return ErpSimpleWriteResult.Ok("Status updated to " + next, repairId);
    }
}
