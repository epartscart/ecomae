using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>subscription_billing</c> <c>cancel</c> / <c>epc_billing_cancel</c>.
/// Create-plan, subscribe, pay, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosBillingWriteService
{
    Task<ErpSimpleWriteResult> CancelAsync(
        long subscriptionId,
        string? reason,
        CancellationToken cancellationToken = default);
}

public sealed class BosBillingWriteService : IBosBillingWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosBillingWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CancelAsync(
        long subscriptionId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var cancelledReason = reason ?? string.Empty;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_subscriptions` SET `status`='cancelled', `cancel_at`=CURDATE(), `cancelled_reason`=?
                    WHERE `id`=?
                    """),
                cancellationToken, cancelledReason, subscriptionId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Subscription cancelled", subscriptionId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Subscriptions table is missing — schema-ensure stays Classic.");
        }
    }
}
