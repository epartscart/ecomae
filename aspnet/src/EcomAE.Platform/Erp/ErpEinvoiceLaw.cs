namespace EcomAE.Platform.Erp;

public sealed record ErpEinvoiceCorner(int Number, string Title, string Role);

public sealed record ErpEinvoicePhase(string When, string Who, string Duty);

public sealed record ErpEinvoiceScopeCell(string Supplier, string Buyer, string Code, bool InScope);

/// <summary>
/// UAE Electronic Invoicing System as stated in Ministry of Finance Guidelines version 1.1
/// (1 June 2026) and the 10 May 2026 amendment of Ministerial Decision No. 244 of 2025.
/// </summary>
public static class ErpEinvoiceLaw
{
    public const string LargeRevenue = "AED 50 million";

    public const string AmendedAspDate = "30 October 2026";

    public const string EarlierAspDate = "31 July 2026";

    public static IReadOnlyList<ErpEinvoiceCorner> Corners { get; } =
    [
        new(1, "Supplier", "Sends the invoice data from this company."),
        new(2, "Supplier provider", "Checks the PINT-AE file and sends it on Peppol."),
        new(3, "Buyer provider", "Receives the file for the customer."),
        new(4, "Buyer", "The business or government customer."),
        new(5, "Federal Tax Authority", "Receives the tax data."),
    ];

    public static IReadOnlyList<ErpEinvoicePhase> Phases { get; } =
    [
        new("1 July 2026", "Pilot and voluntary use", "The pilot includes only a person who agrees in writing. Anyone may start voluntarily. Electronic invoicing penalties start on that person's mandatory date."),
        new(AmendedAspDate, "Revenue of " + LargeRevenue + " or more", "Appoint one accredited service provider. The 10 May 2026 amendment of Ministerial Decision No. 244 of 2025 moved this date from " + EarlierAspDate + "."),
        new("1 January 2027", "Revenue of " + LargeRevenue + " or more", "Use the system for every business transaction that is in scope."),
        new("31 March 2027", "Revenue under " + LargeRevenue + ", and government", "Appoint one accredited service provider."),
        new("1 July 2027", "Revenue under " + LargeRevenue, "Use the system."),
        new("1 October 2027", "Government entities", "Use the system."),
    ];

    public static IReadOnlyList<ErpEinvoiceScopeCell> Scope { get; } =
    [
        new("Business", "Business", "B2B", true),
        new("Business", "Government", "B2G", true),
        new("Business", "Consumer", "B2C", false),
        new("Government", "Business", "G2B", true),
        new("Government", "Government", "G2G", true),
        new("Government", "Consumer", "G2C", false),
        new("Consumer", "Business", "C2B", false),
        new("Consumer", "Government", "C2G", false),
        new("Consumer", "Consumer", "C2C", false),
    ];

    public static string Endpoint(string? tin)
    {
        var digits = new string((tin ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 10)
        {
            return string.Empty;
        }

        return "0235:" + digits[..10];
    }
}
