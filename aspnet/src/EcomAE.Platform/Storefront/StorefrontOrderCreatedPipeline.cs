using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The non-blocking tail of PHP <c>ajax_checkout_create.php</c> after the order commits:
/// <c>epc_send_supplier_lpo_notifications()</c> (one LPO e-mail per warehouse, LPO number = order number),
/// then — for signed-in customers — <c>epc_erp_order_fulfillment_bootstrap()</c> (ERP sales order plus one
/// draft PO per supplier with <c>order_id</c> = customer order). Failures never undo the order; a bootstrap
/// failure is logged as <c>ERP fulfillment bootstrap skipped: …</c> like PHP.
/// </summary>
public interface IStorefrontOrderCreatedPipeline
{
    Task<StorefrontOrderCreatedOutcome> RunAsync(long orderId, int userId, CancellationToken cancellationToken = default);
}

public sealed record StorefrontOrderCreatedOutcome(
    int LpoSent,
    int LpoSkipped,
    long SalesOrderId,
    IReadOnlyList<long> PurchaseOrderIds,
    string? BootstrapSkipped);

public sealed class StorefrontOrderCreatedPipeline : IStorefrontOrderCreatedPipeline
{
    public const string BootstrapSkippedPrefix = "ERP fulfillment bootstrap skipped: ";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IStorefrontSupplierLpoNotifier _lpo;
    private readonly IErpOrderFulfillmentWriteService _fulfillment;

    public StorefrontOrderCreatedPipeline(
        IErpWriteConnectionFactory connections,
        IStorefrontSupplierLpoNotifier lpo,
        IErpOrderFulfillmentWriteService fulfillment)
    {
        _connections = connections;
        _lpo = lpo;
        _fulfillment = fulfillment;
    }

    public async Task<StorefrontOrderCreatedOutcome> RunAsync(long orderId, int userId, CancellationToken cancellationToken = default)
    {
        var sent = 0;
        var skipped = 0;
        try
        {
            var lpo = await _lpo.SendAsync(orderId, cancellationToken).ConfigureAwait(false);
            sent = lpo.Sent;
            skipped = lpo.Skipped;
        }
        catch (Exception ex) when (ex is DbException or ErpWriteException or InvalidOperationException)
        {
        }

        if (userId <= 0)
        {
            return new StorefrontOrderCreatedOutcome(sent, skipped, 0, [], null);
        }

        string? failure;
        try
        {
            var result = await _fulfillment.BootstrapAsync(orderId, userId, cancellationToken).ConfigureAwait(false);
            if (result.Ok && result.Payload is ErpFulfillmentBootstrapPayload payload)
            {
                return new StorefrontOrderCreatedOutcome(sent, skipped, payload.SalesOrderId, payload.PoIds, null);
            }

            failure = result.Ok ? null : result.Message;
        }
        catch (Exception ex) when (ex is DbException or ErpWriteException or InvalidOperationException)
        {
            failure = ex.Message;
        }

        if (failure is not null)
        {
            await LogSkippedAsync(orderId, failure, cancellationToken).ConfigureAwait(false);
        }

        return new StorefrontOrderCreatedOutcome(sent, skipped, 0, [], failure);
    }

    private async Task LogSkippedAsync(long orderId, string message, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?, ?, 0, 1, ?, 1)"),
                cancellationToken,
                orderId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                BootstrapSkippedPrefix + message).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    public static string Summary(StorefrontOrderCreatedOutcome outcome)
    {
        var parts = new List<string>
        {
            "Supplier LPO e-mails: " + outcome.LpoSent.ToString(CultureInfo.InvariantCulture) + " sent, "
                + outcome.LpoSkipped.ToString(CultureInfo.InvariantCulture) + " skipped.",
        };
        if (outcome.SalesOrderId > 0)
        {
            parts.Add("ERP sales order #" + outcome.SalesOrderId.ToString(CultureInfo.InvariantCulture)
                + " with " + outcome.PurchaseOrderIds.Count.ToString(CultureInfo.InvariantCulture) + " supplier PO(s).");
        }
        else if (outcome.BootstrapSkipped is not null)
        {
            parts.Add(BootstrapSkippedPrefix + outcome.BootstrapSkipped);
        }

        return string.Join(" ", parts);
    }
}
