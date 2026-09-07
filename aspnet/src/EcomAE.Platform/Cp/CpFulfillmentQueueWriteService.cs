using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_fulfillment_transition</c> / <c>assign</c> / <c>pick_item</c> /
/// <c>pack_item</c> / <c>create_wave</c> twins. Queue-from-order INSERT, packing-slip
/// PDF, and BOS provider enqueue stay PHP.
/// </summary>
public interface ICpFulfillmentQueueWriteService
{
    Task<ErpSimpleWriteResult> TransitionAsync(
        long fulfillmentId,
        string? newStatus,
        long assignedTo = 0,
        string? assignedName = null,
        string? carrier = null,
        string? trackingNumber = null,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AssignAsync(
        long fulfillmentId,
        long assignedTo,
        string? assignedName,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PickItemAsync(
        long itemId,
        int qtyPicked,
        string? pickStatus,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PackItemAsync(
        long itemId,
        int qtyPacked,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateWaveAsync(
        string? siteKey,
        IReadOnlyList<long> fulfillmentIds,
        CancellationToken cancellationToken = default);
}

public sealed class CpFulfillmentQueueWriteService : ICpFulfillmentQueueWriteService
{
    private static readonly Dictionary<string, string[]> Transitions = new(StringComparer.Ordinal)
    {
        ["queued"] = ["picking", "cancelled"],
        ["picking"] = ["picked", "queued"],
        ["picked"] = ["packing"],
        ["packing"] = ["packed", "picked"],
        ["packed"] = ["shipping"],
        ["shipping"] = ["shipped"],
        ["shipped"] = ["delivered"],
    };

    private static readonly HashSet<string> PickStatuses = new(StringComparer.Ordinal)
    {
        "picked", "short", "substituted",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpFulfillmentQueueWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static IReadOnlyList<string> AllowedNextStatuses(string? current)
        => Transitions.TryGetValue(NormalizeStatus(current), out var next) ? next : [];

    public async Task<ErpSimpleWriteResult> TransitionAsync(
        long fulfillmentId,
        string? newStatus,
        long assignedTo = 0,
        string? assignedName = null,
        string? carrier = null,
        string? trackingNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (fulfillmentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A fulfillment id is required.");
        }

        var target = NormalizeStatus(newStatus);
        if (target.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A fulfillment status is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var current = NormalizeStatus(await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_fulfillment_orders` WHERE `id` = ?"),
            cancellationToken,
            fulfillmentId));
        if (current.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Fulfillment order not found");
        }

        if (!Transitions.TryGetValue(current, out var allowed) || !allowed.Contains(target, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid transition: " + current + " → " + target);
        }

        var sets = new List<string> { "`status` = ?" };
        var args = new List<object?> { target };
        switch (target)
        {
            case "picking":
                sets.Add("`pick_started_at` = NOW()");
                if (assignedTo > 0)
                {
                    sets.Add("`assigned_to` = ?");
                    args.Add(assignedTo);
                    sets.Add("`assigned_name` = ?");
                    args.Add(Clip(assignedName, 128));
                }

                break;
            case "picked":
                sets.Add("`pick_completed_at` = NOW()");
                break;
            case "packed":
                sets.Add("`pack_completed_at` = NOW()");
                break;
            case "shipping":
                var carrierText = Clip(carrier, 64);
                var trackingText = Clip(trackingNumber, 128);
                if (carrierText.Length > 0)
                {
                    sets.Add("`carrier` = ?");
                    args.Add(carrierText);
                }

                if (trackingText.Length > 0)
                {
                    sets.Add("`tracking_number` = ?");
                    args.Add(trackingText);
                }

                break;
            case "shipped":
                sets.Add("`ship_date` = NOW()");
                break;
        }

        args.Add(fulfillmentId);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_fulfillment_orders` SET " + string.Join(", ", sets) + " WHERE `id` = ?"),
            cancellationToken,
            args.ToArray());
        return ErpSimpleWriteResult.Ok("Status set to " + target + ".", fulfillmentId);
    }

    public async Task<ErpSimpleWriteResult> AssignAsync(
        long fulfillmentId,
        long assignedTo,
        string? assignedName,
        CancellationToken cancellationToken = default)
    {
        if (fulfillmentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A fulfillment id is required.");
        }

        if (assignedTo < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "assigned_to cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_fulfillment_orders` SET `assigned_to` = ?, `assigned_name` = ? WHERE `id` = ?"),
            cancellationToken,
            assignedTo, Clip(assignedName, 128), fulfillmentId);
        return ErpSimpleWriteResult.Ok("Picker assigned.", fulfillmentId);
    }

    public async Task<ErpSimpleWriteResult> PickItemAsync(
        long itemId,
        int qtyPicked,
        string? pickStatus,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A fulfillment item id is required.");
        }

        if (qtyPicked < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "qty_picked cannot be negative.");
        }

        var status = NormalizeStatus(pickStatus);
        if (!PickStatuses.Contains(status))
        {
            status = "picked";
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_fulfillment_items` SET `qty_picked` = ?, `pick_status` = ? WHERE `id` = ?"),
            cancellationToken,
            qtyPicked, status, itemId);
        return ErpSimpleWriteResult.Ok("Line pick saved.", itemId);
    }

    public async Task<ErpSimpleWriteResult> PackItemAsync(
        long itemId,
        int qtyPacked,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A fulfillment item id is required.");
        }

        if (qtyPacked < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "qty_packed cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_fulfillment_items` SET `qty_packed` = ? WHERE `id` = ?"),
            cancellationToken,
            qtyPacked, itemId);
        return ErpSimpleWriteResult.Ok("Line pack saved.", itemId);
    }

    public async Task<ErpSimpleWriteResult> CreateWaveAsync(
        string? siteKey,
        IReadOnlyList<long> fulfillmentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = (fulfillmentIds ?? [])
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No fulfillment IDs provided");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var key = Clip(siteKey, 64);
        if (key.Length == 0)
        {
            key = Clip(await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `site_key` FROM `epc_fulfillment_orders` WHERE `id` = ?"),
                cancellationToken,
                ids[0]), 64);
        }

        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A site key is required to create a wave.");
        }

        var waveId = long.Parse(DateTime.UtcNow.ToString("yyMMddHHmm", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var placeholders = string.Join(",", ids.Select(_ => "?"));
        var args = new List<object?> { waveId, key };
        args.AddRange(ids.Cast<object?>());
        var affected = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_fulfillment_orders` SET `wave_id` = ? WHERE `site_key` = ? AND `id` IN ("
                + placeholders
                + ") AND `status` = 'queued'"),
            cancellationToken,
            args.ToArray());
        return new ErpSimpleWriteResult(true, "ok", "Wave " + waveId.ToString(CultureInfo.InvariantCulture) + " created (" + affected.ToString(CultureInfo.InvariantCulture) + " queued jobs).", waveId, 1);
    }

    private static string NormalizeStatus(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
