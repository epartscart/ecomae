using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>subscription_billing</c> <c>cancel</c> / <c>epc_billing_cancel</c>
/// and <c>pay</c> / <c>epc_billing_record_payment</c>.
/// Create-plan, subscribe, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosBillingWriteService
{
    Task<ErpSimpleWriteResult> CancelAsync(
        long subscriptionId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PayAsync(
        long invoiceId,
        string? method,
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

    public async Task<ErpSimpleWriteResult> PayAsync(
        long invoiceId,
        string? method,
        CancellationToken cancellationToken = default)
    {
        var paymentMethod = method ?? "card";
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
                    UPDATE `epc_billing_invoices` SET `status`='paid', `paid_at`=NOW(), `payment_method`=?
                    WHERE `id`=? AND `status` IN ('sent','overdue')
                    """),
                cancellationToken, paymentMethod, invoiceId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Invoice payment recorded", invoiceId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Billing invoices table is missing — schema-ensure stays Classic.");
        }
    }
}
