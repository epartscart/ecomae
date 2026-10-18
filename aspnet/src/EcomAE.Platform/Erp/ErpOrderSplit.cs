using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Partial confirmation, partial delivery, and partial invoice for a sales or
/// purchase order. The undelivered balance stays a backorder until it is
/// cancelled. One order can be posted on many deliveries and invoices, and one
/// delivery or invoice can carry lines from many orders of the same side.
/// </summary>
public static class ErpOrderSplit
{
    public const string Sales = "sales";
    public const string Purchase = "purchase";
    public const string Confirm = "confirm";
    public const string Deliver = "deliver";
    public const string Invoice = "invoice";
    public const string CancelBackorder = "cancel_backorder";

    public sealed record Line(
        long OrderId,
        long LineId,
        decimal Ordered,
        decimal Confirmed,
        decimal Delivered,
        decimal Invoiced,
        decimal Cancelled)
    {
        public decimal Backorder => Rest(Ordered - Math.Max(Delivered, Invoiced) - Cancelled);

        public decimal StillToConfirm => Rest(Ordered - Confirmed - Cancelled);

        public decimal Deliverable => Rest(Math.Min(Confirmed, Ordered - Cancelled) - Delivered);

        public decimal Invoiceable => Rest(Math.Min(Confirmed, Ordered - Cancelled) - Invoiced);
    }

    public sealed record Move(long OrderId, long LineId, decimal Qty);

    public static bool KnownSide(string? side)
        => Is(side, Sales) || Is(side, Purchase);

    public static bool KnownAction(string? action)
        => Is(action, Confirm) || Is(action, Deliver) || Is(action, Invoice) || Is(action, CancelBackorder);

    public static string ActionLabel(string? action)
        => (action ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Confirm => "Confirmation",
            Deliver => "Delivery",
            Invoice => "Invoice",
            CancelBackorder => "Backorder cancellation",
            _ => "Posting",
        };

    public static IReadOnlyList<string> Refuse(IReadOnlyList<Line> lines, string? action, IReadOnlyList<Move> moves)
    {
        var reasons = new List<string>();
        if (!KnownAction(action))
        {
            reasons.Add("Choose confirmation, delivery, invoice, or cancel backorder.");
            return reasons;
        }

        if (moves is null || moves.Count == 0)
        {
            reasons.Add("Enter at least one order line.");
            return reasons;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var move in moves)
        {
            var key = move.OrderId.ToString(CultureInfo.InvariantCulture) + ":" + move.LineId.ToString(CultureInfo.InvariantCulture);
            if (!seen.Add(key))
            {
                reasons.Add("Order " + move.OrderId.ToString(CultureInfo.InvariantCulture) + " line " + move.LineId.ToString(CultureInfo.InvariantCulture) + " is listed twice.");
                continue;
            }

            if (move.Qty <= 0m)
            {
                reasons.Add("Quantity must be greater than zero.");
                continue;
            }

            var line = (lines ?? []).FirstOrDefault(item => item.OrderId == move.OrderId && item.LineId == move.LineId);
            if (line is null)
            {
                reasons.Add("Order " + move.OrderId.ToString(CultureInfo.InvariantCulture) + " line " + move.LineId.ToString(CultureInfo.InvariantCulture) + " was not found.");
                continue;
            }

            var gap = Gap(line, action!, move.Qty);
            if (gap is not null)
            {
                reasons.Add(gap);
            }
        }

        return reasons;
    }

    public static Line Apply(Line line, string action, decimal qty)
    {
        ArgumentNullException.ThrowIfNull(line);
        var amount = Round(qty);
        if (Is(action, Confirm))
        {
            return line with { Confirmed = Round(line.Confirmed + amount) };
        }

        if (Is(action, Deliver))
        {
            return line with { Delivered = Round(line.Delivered + amount) };
        }

        if (Is(action, Invoice))
        {
            return line with { Invoiced = Round(line.Invoiced + amount) };
        }

        if (Is(action, CancelBackorder))
        {
            return line with { Cancelled = Round(line.Cancelled + amount) };
        }

        return line;
    }

    public static IReadOnlyList<Move> Parse(string? text)
    {
        var moves = new List<Move>();
        foreach (var raw in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split(':');
            if (parts.Length != 3
                || !long.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderId)
                || !long.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineId)
                || !decimal.TryParse(parts[2].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var qty))
            {
                continue;
            }

            moves.Add(new Move(orderId, lineId, Round(qty)));
        }

        return moves;
    }

    public static decimal Round(decimal value)
        => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static string? Gap(Line line, string action, decimal qty)
    {
        var amount = Round(qty);
        var where = "Order " + line.OrderId.ToString(CultureInfo.InvariantCulture)
            + " line " + line.LineId.ToString(CultureInfo.InvariantCulture);
        if (Is(action, Confirm))
        {
            return amount > line.StillToConfirm + 0.0001m
                ? where + " can confirm " + Money(line.StillToConfirm) + ". The rest is already confirmed or cancelled."
                : null;
        }

        if (Is(action, Deliver))
        {
            return amount > line.Deliverable + 0.0001m
                ? where + " can deliver " + Money(line.Deliverable) + ". Confirm the quantity first. The undelivered balance stays a backorder."
                : null;
        }

        if (Is(action, Invoice))
        {
            return amount > line.Invoiceable + 0.0001m
                ? where + " can invoice " + Money(line.Invoiceable) + ". Confirm the quantity first."
                : null;
        }

        return amount > line.Backorder + 0.0001m
            ? where + " backorder is " + Money(line.Backorder) + ". Cancel no more than that."
            : null;
    }

    private static decimal Rest(decimal value)
        => Round(value < 0m ? 0m : value);

    private static string Money(decimal value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool Is(string? value, string expected)
        => string.Equals((value ?? string.Empty).Trim(), expected, StringComparison.OrdinalIgnoreCase);
}
