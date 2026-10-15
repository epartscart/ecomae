namespace EcomAE.Platform.Erp;

/// <summary>
/// One allocation rule for ASAP central stock. Every sales channel asks this
/// before a reservation row is written. Commit order is the order of successful
/// locks, not the order of screens the users are looking at.
/// </summary>
public static class ErpAsapStock
{
    public const string FulfilmentOwnBranch = "own_branch";
    public const string FulfilmentOwnCentral = "own_central";
    public const string FulfilmentIntercompany = "intercompany";
    public const string FulfilmentSupplier = "supplier";
    public const string FulfilmentBackorder = "backorder";
    public const string StockOwned = "owned";
    public const string StockConsignment = "consignment";

    public static decimal Atp(decimal physical, decimal reserved, decimal blocked = 0m)
        => decimal.Max(0m, Round(physical) - Round(reserved) - Round(blocked));

    public static string NormalizeReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Trim().Where(ch => char.IsLetterOrDigit(ch)).ToArray();
        return new string(chars).ToUpperInvariant();
    }

    public static bool Interchangeable(string? equivalence)
        => string.Equals(equivalence?.Trim(), "verified", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A selling company uses its own owned stock, or another company's stock only
    /// through an intercompany fulfilment. Consignment is never treated as owned.
    /// </summary>
    public static bool MayReserve(string? sellingCompany, string? ownerCompany, string? fulfilment, string? stockKind)
    {
        if (string.Equals(stockKind?.Trim(), StockConsignment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var selling = (sellingCompany ?? string.Empty).Trim();
        var owner = (ownerCompany ?? string.Empty).Trim();
        var mode = (fulfilment ?? string.Empty).Trim();
        if (mode.Length == 0)
        {
            return true;
        }

        if (mode is FulfilmentSupplier or FulfilmentBackorder)
        {
            return false;
        }

        if (mode is FulfilmentOwnBranch or FulfilmentOwnCentral)
        {
            return selling.Length > 0 && owner.Length > 0
                && string.Equals(selling, owner, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(mode, FulfilmentIntercompany, StringComparison.OrdinalIgnoreCase))
        {
            return selling.Length > 0 && owner.Length > 0
                && !string.Equals(selling, owner, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public static ErpAsapDecision Decide(
        decimal onHand,
        decimal reservedByOthers,
        decimal requested,
        decimal alreadyMine,
        bool allowPartial)
    {
        var physical = Round(onHand);
        var others = Round(decimal.Max(0m, reservedByOthers));
        var mine = Round(decimal.Max(0m, alreadyMine));
        var ask = Round(requested);
        var open = others + mine;
        var available = Atp(physical, open);
        var need = ask - mine;
        if (need <= 0m)
        {
            return new ErpAsapDecision(true, "idempotent", mine, 0m, 0m, available);
        }

        if (need <= available)
        {
            return new ErpAsapDecision(true, "ok", ask, need, 0m, available - need);
        }

        if (allowPartial && available > 0m)
        {
            return new ErpAsapDecision(true, "partial", mine + available, available, need - available, 0m);
        }

        return new ErpAsapDecision(false, "unavailable", mine, 0m, need, available);
    }

    /// <summary>Apply requests in the order their transactions commit.</summary>
    public static IReadOnlyList<ErpAsapDecision> Allocate(
        decimal onHand,
        IReadOnlyList<(string Key, decimal Qty, bool AllowPartial)> requests)
    {
        var mine = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var reserved = 0m;
        var decisions = new List<ErpAsapDecision>(requests.Count);
        foreach (var request in requests)
        {
            var key = request.Key ?? string.Empty;
            mine.TryGetValue(key, out var already);
            var others = reserved - already;
            var decision = Decide(onHand, others, request.Qty, already, request.AllowPartial);
            if (decision.AddedQty > 0m)
            {
                mine[key] = decision.ReservedQty;
                reserved += decision.AddedQty;
            }

            decisions.Add(decision);
        }

        return decisions;
    }

    private static decimal Round(decimal value)
        => decimal.Round(value, 3, MidpointRounding.AwayFromZero);
}

public readonly record struct ErpAsapDecision(
    bool Accepted,
    string Code,
    decimal ReservedQty,
    decimal AddedQty,
    decimal BackorderQty,
    decimal AvailableAfter);

/// <summary>In-process stand-in for the database row lock. One SKU, one lock.</summary>
public sealed class ErpAsapReservationBoard
{
    private readonly object _gate = new();
    private readonly Dictionary<string, decimal> _holds = new(StringComparer.Ordinal);
    private decimal _onHand;

    public ErpAsapReservationBoard(decimal onHand) => _onHand = onHand;

    public decimal OnHand
    {
        get { lock (_gate) return _onHand; }
    }

    public decimal Reserved
    {
        get { lock (_gate) return _holds.Values.Sum(); }
    }

    public decimal Available
    {
        get { lock (_gate) return ErpAsapStock.Atp(_onHand, _holds.Values.Sum()); }
    }

    public ErpAsapDecision TryReserve(string key, decimal qty, bool allowPartial)
    {
        lock (_gate)
        {
            var id = key ?? string.Empty;
            _holds.TryGetValue(id, out var mine);
            var others = _holds.Values.Sum() - mine;
            var decision = ErpAsapStock.Decide(_onHand, others, qty, mine, allowPartial);
            if (decision.AddedQty > 0m)
            {
                _holds[id] = decision.ReservedQty;
            }

            return decision;
        }
    }
}
