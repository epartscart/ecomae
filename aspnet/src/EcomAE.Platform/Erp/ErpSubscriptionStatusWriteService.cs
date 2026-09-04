namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_sub_set_status</c> twin. Schema ensure and cycle-invoice generate stay PHP.
/// </summary>
public interface IErpSubscriptionStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(long id, string? status, CancellationToken cancellationToken = default);
}

public sealed class ErpSubscriptionStatusWriteService : IErpSubscriptionStatusWriteService
{
    internal static readonly string[] Allowed = ["active", "paused", "cancelled"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpSubscriptionStatusWriteService(IErpWriteConnectionFactory connections)
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
            return ErpSimpleWriteResult.Fail("invalid", "A subscription id is required.");
        }

        var next = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!Allowed.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid subscription status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_subscriptions` SET `status` = ? WHERE `id` = ?"),
            cancellationToken,
            next, id);
        return ErpSimpleWriteResult.Ok("Subscription " + next, id);
    }
}
