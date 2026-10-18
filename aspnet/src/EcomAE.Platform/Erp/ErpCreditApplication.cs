using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Credit-application papers for a customer. A company attaches a trade license
/// and, for each partner, a passport, a visa, and an Emirates ID. An individual
/// attaches those three papers for themselves. A paper or the application period
/// blocks an invoice when it is missing, expired, or inside one month of expiry.
/// </summary>
public static class ErpCreditApplication
{
    public const string Tab = "cc_credit_application";
    public const string Company = "company";
    public const string Individual = "individual";
    public const string TradeLicense = "trade_license";
    public const string Passport = "passport";
    public const string Visa = "visa";
    public const string EmiratesId = "emirates_id";
    public const int MonthDays = 30;

    public static readonly string[] PersonKinds = [Passport, Visa, EmiratesId];

    public sealed record Partner(long Id, string Name);

    public sealed record Paper(long PartnerId, string Kind, string Number, long ExpiryUnix, bool Attached);

    public sealed record File(
        bool Filed,
        string Party,
        long ValidUntilUnix,
        IReadOnlyList<Partner> Partners,
        IReadOnlyList<Paper> Papers);

    public static bool IsIndividual(string? party)
        => string.Equals((party ?? string.Empty).Trim(), Individual, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeParty(string? party)
        => IsIndividual(party) ? Individual : Company;

    public static bool KnownKind(string? kind)
    {
        var value = (kind ?? string.Empty).Trim();
        return value.Equals(TradeLicense, StringComparison.OrdinalIgnoreCase)
            || PersonKinds.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    public static string Label(string? kind)
    {
        var value = (kind ?? string.Empty).Trim();
        if (value.Equals(TradeLicense, StringComparison.OrdinalIgnoreCase))
        {
            return "Trade license";
        }

        if (value.Equals(Passport, StringComparison.OrdinalIgnoreCase))
        {
            return "Passport";
        }

        if (value.Equals(Visa, StringComparison.OrdinalIgnoreCase))
        {
            return "Visa";
        }

        if (value.Equals(EmiratesId, StringComparison.OrdinalIgnoreCase))
        {
            return "Emirates ID";
        }

        return "Document";
    }

    public static IReadOnlyList<string> Gaps(File? file, long asOfUnix)
    {
        if (file is not { Filed: true })
        {
            return ["A credit application with current documents is required."];
        }

        var reasons = new List<string>();
        var period = ExpiryReason("The credit application", file.ValidUntilUnix, asOfUnix);
        if (period is not null)
        {
            reasons.Add(period);
        }

        if (!IsIndividual(file.Party))
        {
            reasons.AddRange(PaperGaps(file, 0, string.Empty, TradeLicense, asOfUnix));
            var partners = (file.Partners ?? []).Where(partner => partner.Id > 0 && !string.IsNullOrWhiteSpace(partner.Name)).ToArray();
            if (partners.Length == 0)
            {
                reasons.Add("Add each partner. Passport, visa, and Emirates ID are attached separately for every partner.");
            }

            foreach (var partner in partners)
            {
                foreach (var kind in PersonKinds)
                {
                    reasons.AddRange(PaperGaps(file, partner.Id, partner.Name.Trim(), kind, asOfUnix));
                }
            }
        }
        else
        {
            foreach (var kind in PersonKinds)
            {
                reasons.AddRange(PaperGaps(file, 0, string.Empty, kind, asOfUnix));
            }
        }

        return reasons;
    }

    public static ErpCreditBlock.Decision Apply(ErpCreditBlock.Decision credit, bool documentsRule, File? file, long asOfUnix)
    {
        ArgumentNullException.ThrowIfNull(credit);
        if (!documentsRule)
        {
            return credit;
        }

        var gaps = Gaps(file, asOfUnix);
        if (gaps.Count == 0)
        {
            return credit;
        }

        var reasons = credit.Reasons.Concat(gaps).ToArray();
        return credit with { Allowed = false, Reasons = reasons };
    }

    public static string? ExpiryReason(string label, long expiryUnix, long asOfUnix)
    {
        var days = DaysUntil(expiryUnix, asOfUnix);
        if (days is null)
        {
            return label + " has no expiry date.";
        }

        if (days < 0)
        {
            return label + " expired on " + ErpCreditBlock.Day(expiryUnix) + ".";
        }

        if (days <= MonthDays)
        {
            return label + " expires on " + ErpCreditBlock.Day(expiryUnix) + ", inside one month.";
        }

        return null;
    }

    public static int? DaysUntil(long expiryUnix, long asOfUnix)
    {
        if (expiryUnix <= 0)
        {
            return null;
        }

        var expiryDay = DayStart(expiryUnix);
        var asOfDay = asOfUnix <= 0 ? 0 : asOfUnix - (asOfUnix % 86400L);
        return (int)((expiryDay - asOfDay) / 86400L);
    }

    private static IEnumerable<string> PaperGaps(File file, long partnerId, string partnerName, string kind, long asOfUnix)
    {
        var label = string.IsNullOrWhiteSpace(partnerName) ? Label(kind) : Label(kind) + " for " + partnerName.Trim();
        var paper = (file.Papers ?? []).FirstOrDefault(item =>
            item.PartnerId == partnerId
            && item.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));
        if (paper is not { Attached: true })
        {
            yield return label + " is not attached.";
            yield break;
        }

        if (string.IsNullOrWhiteSpace(paper.Number))
        {
            yield return label + " has no document number.";
            yield break;
        }

        var expiry = ExpiryReason(label, paper.ExpiryUnix, asOfUnix);
        if (expiry is not null)
        {
            yield return expiry;
        }
    }

    private static long DayStart(long unix)
    {
        if (unix > 86399 && unix % 86400L == 86399)
        {
            return unix - 86399;
        }

        return unix - (unix % 86400L);
    }
}
