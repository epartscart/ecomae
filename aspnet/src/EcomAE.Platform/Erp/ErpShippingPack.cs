using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Merges several customer invoices that leave in one shipment into one shipping
/// invoice and one packing slip. The source invoices stay as they are. The pack
/// is customer documentation, not a second tax invoice.
/// </summary>
public static class ErpShippingPack
{
    public const int MaxInvoices = 40;

    public sealed record Line(
        long DocumentId,
        string InvoiceNumber,
        int LineNo,
        string Item,
        string Description,
        decimal Qty,
        string Uom,
        decimal UnitPrice,
        decimal Net,
        decimal TaxRate,
        decimal Vat,
        decimal Gross);

    public sealed record Source(
        long Id,
        long CustomerId,
        string Number,
        string Currency,
        string Status,
        string Category,
        bool Active,
        decimal Net,
        decimal Vat,
        decimal Gross,
        string CustomerName,
        IReadOnlyList<Line> Lines);

    public sealed record Built(
        long CustomerId,
        string CustomerName,
        string Currency,
        decimal Net,
        decimal Vat,
        decimal Gross,
        decimal Quantity,
        IReadOnlyList<Source> Invoices,
        IReadOnlyList<Line> Lines);

    public static IReadOnlyList<long> Parse(string? text)
    {
        var ids = new List<long>();
        foreach (var raw in (text ?? string.Empty).Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    public static IReadOnlyList<string> Refuse(
        IReadOnlyList<long> requested,
        IReadOnlyList<Source> sources,
        IReadOnlySet<long> alreadyPacked)
    {
        var reasons = new List<string>();
        if (requested is null || requested.Count < 2)
        {
            reasons.Add("Enter at least two invoices. One shipment can carry many invoices for the same customer.");
            return reasons;
        }

        if (requested.Count > MaxInvoices)
        {
            reasons.Add("A shipping pack lists at most " + MaxInvoices.ToString(CultureInfo.InvariantCulture) + " invoices.");
            return reasons;
        }

        var seen = new HashSet<long>();
        var trip = new Trip();
        var byId = (sources ?? []).ToDictionary(item => item.Id);
        foreach (var id in requested)
        {
            if (!seen.Add(id))
            {
                reasons.Add("Invoice " + id.ToString(CultureInfo.InvariantCulture) + " is listed twice.");
                continue;
            }

            if (!byId.TryGetValue(id, out var source))
            {
                reasons.Add("Invoice " + id.ToString(CultureInfo.InvariantCulture) + " was not found.");
                continue;
            }

            reasons.AddRange(Gaps(source, trip, alreadyPacked));
        }

        return reasons;
    }

    public static IReadOnlyList<string> RefuseShipment(int packages, decimal weightKg)
    {
        var reasons = new List<string>();
        if (packages < 0)
        {
            reasons.Add("Packages cannot be negative.");
        }

        if (weightKg < 0m)
        {
            reasons.Add("Weight cannot be negative.");
        }

        return reasons;
    }

    public static Built Compose(IReadOnlyList<Source> invoices)
    {
        var list = invoices ?? [];
        var lines = new List<Line>();
        decimal net = 0m;
        decimal vat = 0m;
        decimal gross = 0m;
        decimal qty = 0m;
        var name = string.Empty;
        foreach (var invoice in list)
        {
            net = RoundMoney(net + invoice.Net);
            vat = RoundMoney(vat + invoice.Vat);
            gross = RoundMoney(gross + invoice.Gross);
            if (name.Length == 0 && !string.IsNullOrWhiteSpace(invoice.CustomerName))
            {
                name = invoice.CustomerName.Trim();
            }

            if (invoice.Lines is { Count: > 0 })
            {
                foreach (var line in invoice.Lines)
                {
                    var copy = line with
                    {
                        InvoiceNumber = string.IsNullOrWhiteSpace(line.InvoiceNumber) ? invoice.Number : line.InvoiceNumber,
                        Qty = RoundQty(line.Qty),
                        UnitPrice = RoundPrice(line.UnitPrice),
                        Net = RoundMoney(line.Net),
                        TaxRate = RoundMoney(line.TaxRate),
                        Vat = RoundMoney(line.Vat),
                        Gross = RoundMoney(line.Gross),
                    };
                    qty = RoundQty(qty + copy.Qty);
                    lines.Add(copy);
                }

                continue;
            }

            var placeholder = new Line(
                invoice.Id,
                invoice.Number,
                1,
                string.IsNullOrWhiteSpace(invoice.Number) ? "Invoice " + invoice.Id.ToString(CultureInfo.InvariantCulture) : invoice.Number,
                "Shipped as invoiced",
                1m,
                "C62",
                invoice.Net,
                invoice.Net,
                invoice.Net == 0m ? 0m : RoundMoney(invoice.Vat / invoice.Net * 100m),
                invoice.Vat,
                invoice.Gross);
            qty = RoundQty(qty + 1m);
            lines.Add(placeholder);
        }

        var first = list.Count > 0 ? list[0] : null;
        return new Built(
            first?.CustomerId ?? 0,
            name.Length == 0 ? "Customer " + (first?.CustomerId ?? 0).ToString(CultureInfo.InvariantCulture) : name,
            first is null ? "AED" : MoneyCode(first.Currency),
            net,
            vat,
            gross,
            qty,
            list,
            lines);
    }

    public static Built Sample()
    {
        var first = new Source(100, 7, "SI-100", "AED", "validated", "tax_invoice", true, 200m, 10m, 210m, "Harbour Trading",
            [new Line(100, "SI-100", 1, "Filter", "Oil filter", 2m, "C62", 100m, 200m, 5m, 10m, 210m)]);
        var second = new Source(101, 7, "SI-101", "AED", "validated", "tax_invoice", true, 50m, 2.50m, 52.50m, "Harbour Trading",
            [new Line(101, "SI-101", 1, "Belt", "Drive belt", 1m, "C62", 50m, 50m, 5m, 2.50m, 52.50m)]);
        return Compose([first, second]);
    }

    public static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal RoundQty(decimal value)
        => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    public static decimal RoundPrice(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static List<string> Gaps(Source source, Trip trip, IReadOnlySet<long> alreadyPacked)
    {
        var reasons = new List<string>();
        var where = "Invoice " + Label(source);
        if (!source.Active)
        {
            reasons.Add(where + " is not active.");
            return reasons;
        }

        if (!CustomerInvoice(source.Category))
        {
            reasons.Add(where + " is not a customer invoice. A shipping pack lists tax invoices and commercial invoices.");
            return reasons;
        }

        if (!Shippable(source.Status))
        {
            reasons.Add(where + " is " + (string.IsNullOrWhiteSpace(source.Status) ? "not ready" : source.Status.Trim().ToLowerInvariant()) + ". Ship a validated invoice.");
            return reasons;
        }

        if (source.CustomerId <= 0)
        {
            reasons.Add(where + " has no customer. A shipping pack is for one customer.");
            return reasons;
        }

        if (trip.Customer == 0)
        {
            trip.Customer = source.CustomerId;
        }
        else if (trip.Customer != source.CustomerId)
        {
            reasons.Add(where + " belongs to another customer. One shipment is for one customer.");
        }

        var code = MoneyCode(source.Currency);
        if (trip.Currency.Length == 0)
        {
            trip.Currency = code;
        }
        else if (!string.Equals(trip.Currency, code, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(where + " is in " + code + ". The shipment is in " + trip.Currency + ".");
        }

        if (alreadyPacked is not null && alreadyPacked.Contains(source.Id))
        {
            reasons.Add(where + " is already on a shipping pack. Release that pack before preparing another.");
        }

        return reasons;
    }

    private sealed class Trip
    {
        public long Customer;
        public string Currency = string.Empty;
    }

    private static bool CustomerInvoice(string? category)
    {
        var value = (category ?? string.Empty).Trim();
        return value.Equals("tax_invoice", StringComparison.OrdinalIgnoreCase)
            || value.Equals("commercial_invoice", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Shippable(string? status)
    {
        var value = (status ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return false;
        }

        return !value.Equals("draft", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("cancelled", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("canceled", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("void", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("rejected", StringComparison.OrdinalIgnoreCase);
    }

    private static string Label(Source source)
        => string.IsNullOrWhiteSpace(source.Number)
            ? source.Id.ToString(CultureInfo.InvariantCulture)
            : source.Number.Trim() + " (" + source.Id.ToString(CultureInfo.InvariantCulture) + ")";

    private static string MoneyCode(string? currency)
    {
        var code = (currency ?? string.Empty).Trim().ToUpperInvariant();
        return code.Length == 0 ? "AED" : code;
    }
}
