using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Workspace pack for property and real-estate law. The wording is original.
/// Federal Law No. 25 of 2025, in force from 1 June 2026, is the civil code.
/// Dubai and Abu Dhabi keep their own registration, tenancy, and off-plan rules.
/// A fetch stamps this pack. It does not replace the text with a web page.
/// </summary>
public static class ErpRealEstateLaw
{
    public const string PackVersion = "2026-06-01";
    public const string Tab = "compliance";
    public const string CivilCode = "Federal Law No. 25 of 2025";
    public const string CivilCodeFrom = "1 June 2026";
    public const string FormerCivilCode = "Federal Law No. 5 of 1985";
    public const string VatLaw = "Federal Decree-Law No. 8 of 2017";
    public const string DubaiTenancy = "Law No. 26 of 2007";
    public const string DubaiTenancyAmend = "Law No. 33 of 2008";
    public const string DubaiRent = "Decree No. 43 of 2013";
    public const string DubaiTitle = "Law No. 7 of 2006";
    public const string DubaiEscrow = "Law No. 8 of 2007";
    public const string DubaiOffplan = "Law No. 13 of 2008";
    public const string DubaiJoint = "Law No. 6 of 2019";
    public const string DubaiDisputes = "Decree No. 26 of 2013";
    public const string AbuDhabiSector = "Law No. 3 of 2015";
    public const string AbuDhabiCap = "Executive Council Resolution No. 14 of 2016";
    public const string AmlDecree = "Federal Decree-Law No. 10 of 2025";
    public const string AmlResolution = "Cabinet Resolution No. 134 of 2025";
    public const decimal AmlLineAed = 55_000m;

    public static readonly ErpRealEstateLawSource[] Sources =
    [
        new("Dubai legislation", "https://dlp.dubai.gov.ae/", "Legislation"),
        new("Federal tax legislation", "https://tax.gov.ae/en/legislation.aspx", "VAT"),
    ];

    public static readonly ErpRealEstateLawStep[] Steps =
    [
        new(1, "Identify the property", "Plot, unit, emirate, and whether the buyer may own it there."),
        new(2, "Register the right", "A sale is registered. A short lease is registered. A long lease is a real right."),
        new(3, "Price and tax", "Rent follows the emirate cap. VAT follows the use of the building."),
        new(4, "Hold the money", "Off-plan collections in Dubai go to the escrow account for that project."),
        new(5, "Keep the file", "Title, contract, registration, VAT, and the AML file stay with the property."),
    ];

    public static readonly ErpRealEstateLawCard[] Cards =
    [
        new("Civil code", CivilCode + ", in force from " + CivilCodeFrom + ", replaces " + FormerCivilCode + ". Negotiate in good faith and disclose what the other party needs. The landlord keeps the property fit for the agreed use unless the lease lawfully moves that duty. If the property is totally destroyed, the lease ends. Expropriation is only for a public benefit, and compensation is paid. Usufruct and musataha no longer have the old statutory ceilings of 99 and 50 years. The term that is registered, and the emirate rule, decide how long the right lasts."),
        new("Dubai title and lease", DubaiTitle + " is how a right in Dubai land is registered at the Land Department. A lease of less than ten years is registered on Ejari. A lease of ten years or more is a real right and is registered at the Land Department. " + DubaiTenancy + ", as amended by " + DubaiTenancyAmend + ", is the landlord and tenant law. A change of rent, or a refusal to renew, is notified 90 days before the lease ends. Taking the property back for the landlord's own use, or for a first-degree relative, needs 12 months' notice. After that, the property is not let to someone else for two years if it is residential, or three years if it is not. " + DubaiDisputes + " is the rental disputes centre."),
        new("Dubai rent", DubaiRent + " caps the increase when a lease is renewed. The cap is read from the Real Estate Regulatory Agency rent index. No increase when the rent is not more than 10 percent below similar units. Five percent when it is 11 to 20 percent below. Ten percent when it is 21 to 30 percent below. Fifteen percent when it is 31 to 40 percent below. Twenty percent when it is more than 40 percent below. The decree applies across Dubai, including special development zones and free zones such as the Dubai International Financial Centre."),
        new("Abu Dhabi", "A transfer of land is registered at the Department of Municipalities and Transport. A lease under four years uses the department's mandatory form. A lease of 25 years or more can be a real right. " + AbuDhabiSector + " regulates the sector. " + AbuDhabiCap + " stops a renewal increase above 5 percent a year. The Abu Dhabi Global Market has its own real-estate rules."),
        new("Off-plan and common parts", "An off-plan sale is registered on the interim register. That registration is not ownership. Ownership is registered when the unit is complete. In Dubai the buyer's money goes to a project escrow account under " + DubaiEscrow + ", and the interim register is " + DubaiOffplan + ". The developer repairs structural defects for ten years from the completion certificate and is liable for latent defects for one year from that certificate. " + DubaiJoint + " is jointly owned property: common parts, service charges, and the owners' committee. A licensed hotel or serviced apartment is outside the ordinary tenancy law."),
        new("Tax and AML", VatLaw + " puts the place of supply of a service related to real estate where the property is. Bare land is exempt. A residential lease is exempt. The first supply of a new residential building, within three years of completion, is zero-rated. A later residential sale is exempt. A commercial sale or lease, and a hotel or serviced apartment, are standard-rated at 5 percent. The landlord states whether the rent includes VAT and accounts for the tax. A broker or agent also keeps the AML pack under " + AmlDecree + " and " + AmlResolution + ". A transaction of AED 55,000 or more is inside that line."),
    ];

    public static readonly ErpRealEstateVatTile[] VatTiles =
    [
        new("Bare land", "Exempt", "exempt"),
        new("Residential lease", "Exempt", "exempt"),
        new("First residential sale", "Zero-rated within 3 years of completion", "zero"),
        new("Later residential sale", "Exempt", "exempt"),
        new("Commercial", "5 percent", "standard"),
        new("Hotel or serviced", "5 percent", "standard"),
    ];

    public static readonly ErpRealEstateRentRung[] DubaiLadder =
    [
        new("Up to 10% below", "No increase"),
        new("11% to 20% below", "5%"),
        new("21% to 30% below", "10%"),
        new("31% to 40% below", "15%"),
        new("More than 40% below", "20%"),
    ];

    public static ErpRealEstateLawDecision Check(ErpRealEstateLawFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var findings = new List<ErpRealEstateLawFinding>();
        var emirate = Norm(file.Emirate);
        var kind = Norm(file.Kind);
        var use = Norm(file.Use);
        var vat = VatTreatment(use, kind, Norm(file.FirstSupply));

        if (emirate is "dubai" or "abu_dhabi")
        {
            if (!file.Registered)
            {
                findings.Add(new("block", emirate == "dubai"
                    ? "Register the right. A Dubai sale is registered at the Land Department under " + DubaiTitle + ". A lease under ten years is registered on Ejari. A lease of ten years or more is registered as a real right."
                    : "Register the right at the Department of Municipalities and Transport. A lease under four years uses the department's mandatory form."));
            }
            else if (emirate == "dubai" && kind == "lease" && file.TermYears >= 10)
            {
                findings.Add(new("clear", "A Dubai lease of ten years or more is a real right. Keep the Land Department registration, not only Ejari."));
            }
            else if (emirate == "abu_dhabi" && kind == "lease" && file.TermYears >= 25)
            {
                findings.Add(new("clear", "An Abu Dhabi lease of 25 years or more can be a real right. Keep the Department of Municipalities and Transport registration."));
            }
        }
        else
        {
            findings.Add(new("watch", "This pack states the federal rules and the Dubai and Abu Dhabi rules. Confirm that emirate's own registration and tenancy law before the file is closed."));
        }

        if (file.ForeignBuyer && Norm(file.Designated) == "no")
        {
            findings.Add(new("block", "Absolute ownership for a person who is not a UAE national is limited to a designated investment area. This file says the plot is outside those areas."));
        }
        else if (file.ForeignBuyer && Norm(file.Designated) != "yes")
        {
            findings.Add(new("watch", "Confirm that the plot is inside a designated investment area before absolute ownership is transferred to a person who is not a UAE national."));
        }

        if (kind == "offplan")
        {
            findings.Add(new("clear", "Register the off-plan contract on the interim register. That registration is not ownership."));
            if (emirate == "dubai" && Norm(file.Escrow) == "no")
            {
                findings.Add(new("block", "Dubai off-plan money is paid into the project escrow account under " + DubaiEscrow + "."));
            }
            else if (emirate == "dubai" && Norm(file.Escrow) != "yes")
            {
                findings.Add(new("watch", "Confirm the RERA escrow account for this Dubai project under " + DubaiEscrow + "."));
            }
            else if (emirate != "dubai")
            {
                findings.Add(new("watch", "Confirm the project account rule of that emirate. Dubai's rule is " + DubaiEscrow + "."));
            }

            findings.Add(new("clear", "The developer repairs structural defects for ten years from the completion certificate and is liable for latent defects for one year from that certificate."));
        }

        if (kind == "lease" && use == "hotel")
        {
            findings.Add(new("watch", "A licensed hotel or serviced apartment is outside the ordinary landlord and tenant law, unless the unit is let as an ordinary home."));
        }

        if (kind == "lease" && file.ProposedRent > file.CurrentRent && file.CurrentRent > 0m)
        {
            if (emirate == "dubai")
            {
                var cap = DubaiIncreaseRate(file.CurrentRent, file.AverageRent);
                if (cap < 0m)
                {
                    findings.Add(new("watch", "Enter the rent-index average before a Dubai renewal increase is accepted. " + DubaiRent + " sets the cap."));
                }
                else
                {
                    var proposed = (file.ProposedRent - file.CurrentRent) / file.CurrentRent;
                    var ceiling = Round(file.CurrentRent * (1m + cap));
                    if (proposed > cap + 0.0000001m)
                    {
                        findings.Add(new("block", "The renewal is " + Percent(proposed) + ". " + DubaiRent + " allows " + Percent(cap) + " for this gap to the rent index, so the rent cannot go above " + Money(ceiling) + "."));
                    }
                    else
                    {
                        findings.Add(new("clear", DubaiRent + " allows up to " + Percent(cap) + " on this renewal. The proposed rent is inside that cap."));
                    }
                }
            }
            else if (emirate == "abu_dhabi")
            {
                var proposed = (file.ProposedRent - file.CurrentRent) / file.CurrentRent;
                if (proposed > 0.05m + 0.0000001m)
                {
                    findings.Add(new("block", "The renewal is " + Percent(proposed) + ". " + AbuDhabiCap + " stops an Abu Dhabi renewal increase above 5 percent, which is " + Money(Round(file.CurrentRent * 1.05m)) + "."));
                }
                else
                {
                    findings.Add(new("clear", "The Abu Dhabi renewal is inside the 5 percent cap of " + AbuDhabiCap + "."));
                }
            }
        }

        if (Norm(file.ClaimedVat).Length > 0 && !string.Equals(Norm(file.ClaimedVat), vat.Code, StringComparison.Ordinal))
        {
            findings.Add(new("block", "The file claims " + Label(file.ClaimedVat) + " VAT. This supply is " + vat.Label + " under " + VatLaw + "."));
        }
        else
        {
            findings.Add(new("clear", "VAT: " + vat.Label + ". " + vat.Why));
        }

        if (file.AmountAed >= AmlLineAed)
        {
            findings.Add(new("watch", "AED " + Money(file.AmountAed) + " is inside the AML line of AED 55,000. A broker or agent keeps the AML pack under " + AmlDecree + " and " + AmlResolution + "."));
        }

        var blocked = findings.Any(item => item.Level == "block");
        return new ErpRealEstateLawDecision(!blocked, vat.Label, findings);
    }

    public static decimal DubaiIncreaseRate(decimal current, decimal average)
    {
        if (current <= 0m || average <= 0m)
        {
            return -1m;
        }

        var shortfall = (average - current) / average;
        if (shortfall <= 0.10m)
        {
            return 0m;
        }

        if (shortfall <= 0.20m)
        {
            return 0.05m;
        }

        if (shortfall <= 0.30m)
        {
            return 0.10m;
        }

        if (shortfall <= 0.40m)
        {
            return 0.15m;
        }

        return 0.20m;
    }

    public static ErpRealEstateVatAnswer VatTreatment(string? use, string? kind, string? firstSupply)
    {
        var usage = Norm(use);
        var deal = Norm(kind);
        if (usage is "bare" or "bare_land" or "land")
        {
            return new("exempt", "Exempt", "Bare land is exempt.");
        }

        if (usage is "hotel" or "serviced")
        {
            return new("standard", "Standard-rated at 5 percent", "A hotel or serviced apartment is not a residential building for the exemption.");
        }

        if (usage == "commercial")
        {
            return new("standard", "Standard-rated at 5 percent", "A commercial sale or lease is standard-rated.");
        }

        if (deal is "lease")
        {
            return new("exempt", "Exempt", "A residential lease is exempt.");
        }

        if (Norm(firstSupply) == "yes")
        {
            return new("zero", "Zero-rated", "The first supply of a new residential building, within three years of completion, is zero-rated.");
        }

        if (Norm(firstSupply) == "no")
        {
            return new("exempt", "Exempt", "A residential sale after the first supply is exempt.");
        }

        return new("unknown", "Confirm the completion date", "A residential sale is zero-rated only for the first supply within three years of completion. Otherwise it is exempt.");
    }

    private static string Norm(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

    private static string Label(string? code) => Norm(code) switch
    {
        "exempt" => "exempt",
        "zero" => "zero-rated",
        "standard" => "standard-rated",
        _ => code ?? string.Empty,
    };

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Money(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static string Percent(decimal rate) => (rate * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}

public sealed record ErpRealEstateLawSource(string Name, string Url, string Marker);

public sealed record ErpRealEstateLawStep(int Number, string Title, string Body);

public sealed record ErpRealEstateLawCard(string Title, string Body);

public sealed record ErpRealEstateVatTile(string Use, string Treatment, string Tone);

public sealed record ErpRealEstateRentRung(string Gap, string Cap);

public sealed record ErpRealEstateVatAnswer(string Code, string Label, string Why);

public sealed record ErpRealEstateLawFinding(string Level, string Text);

public sealed record ErpRealEstateLawFile(
    string? Emirate,
    string? Kind,
    string? Use,
    bool Registered,
    int TermYears,
    decimal CurrentRent,
    decimal AverageRent,
    decimal ProposedRent,
    string? Escrow,
    bool ForeignBuyer,
    string? Designated,
    string? FirstSupply,
    string? ClaimedVat,
    decimal AmountAed);

public sealed record ErpRealEstateLawDecision(bool Allowed, string Vat, IReadOnlyList<ErpRealEstateLawFinding> Findings)
{
    public string Message => Allowed
        ? "The file fits this pack. " + string.Join(" ", Findings.Where(item => item.Level != "clear").Select(item => item.Text))
        : "Property file blocked. " + string.Join(" ", Findings.Where(item => item.Level == "block").Select(item => item.Text));
}
