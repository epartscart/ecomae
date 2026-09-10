using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>fulfillment_queue</c> <c>pick_item</c> / <c>epc_fulfillment_pick_item</c>
/// and <c>transition</c> / <c>epc_fulfillment_transition</c>.
/// Queue, wave, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosFulfillmentWriteService
{
    Task<ErpSimpleWriteResult> PickItemAsync(
        long itemId,
        int qtyPicked,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> TransitionAsync(
        long fulfillmentId,
        string? newStatus,
        long assignedTo,
        string? assignedName,
        string? carrier,
        string? trackingNumber,
        CancellationToken cancellationToken = default);
}

public sealed class BosFulfillmentWriteService : IBosFulfillmentWriteService
{
    private static readonly Dictionary<string, string[]> Valid = new(StringComparer.Ordinal)
    {
        ["queued"] = ["picking", "cancelled"],
        ["picking"] = ["picked", "queued"],
        ["picked"] = ["packing"],
        ["packing"] = ["packed", "picked"],
        ["packed"] = ["shipping"],
        ["shipping"] = ["shipped"],
        ["shipped"] = ["delivered"],
    };

    private readonly IErpWriteConnectionFactory _connections;

    public BosFulfillmentWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> PickItemAsync(
        long itemId,
        int qtyPicked,
        CancellationToken cancellationToken = default)
    {
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
                    UPDATE `epc_fulfillment_items` SET `qty_picked` = ?, `pick_status` = ?
                    WHERE `id` = ?
                    """),
                cancellationToken, qtyPicked, "picked", itemId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Line pick saved", itemId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Fulfillment items table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> TransitionAsync(
        long fulfillmentId,
        string? newStatus,
        long assignedTo,
        string? assignedName,
        string? carrier,
        string? trackingNumber,
        CancellationToken cancellationToken = default)
    {
        var next = newStatus ?? string.Empty;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var current = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `status` FROM `epc_fulfillment_orders` WHERE `id` = ?"),
                cancellationToken, fulfillmentId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(current))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Fulfillment order not found");
            }

            if (!Valid.TryGetValue(current, out var allowed)
                || !allowed.Contains(next, StringComparer.Ordinal))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Invalid transition: " + current + " → " + next);
            }

            var updates = new List<string> { "`status` = ?" };
            var parameters = new List<object?> { next };
            switch (next)
            {
                case "picking":
                    updates.Add("`pick_started_at` = NOW()");
                    if (assignedTo != 0)
                    {
                        updates.Add("`assigned_to` = ?");
                        parameters.Add(assignedTo);
                        updates.Add("`assigned_name` = ?");
                        parameters.Add(assignedName ?? string.Empty);
                    }

                    break;
                case "picked":
                    updates.Add("`pick_completed_at` = NOW()");
                    break;
                case "packed":
                    updates.Add("`pack_completed_at` = NOW()");
                    break;
                case "shipping":
                    if (!IsPhpEmpty(carrier))
                    {
                        updates.Add("`carrier` = ?");
                        parameters.Add(carrier);
                    }

                    if (!IsPhpEmpty(trackingNumber))
                    {
                        updates.Add("`tracking_number` = ?");
                        parameters.Add(trackingNumber);
                    }

                    break;
                case "shipped":
                    updates.Add("`ship_date` = NOW()");
                    break;
            }

            parameters.Add(fulfillmentId);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_fulfillment_orders` SET " + string.Join(", ", updates) + " WHERE `id` = ?"),
                cancellationToken, parameters.ToArray()).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Fulfillment transitioned", fulfillmentId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Fulfillment orders table is missing — schema-ensure stays Classic.");
        }
    }

    private static bool IsPhpEmpty(string? value)
        => string.IsNullOrEmpty(value) || string.Equals(value, "0", StringComparison.Ordinal);
