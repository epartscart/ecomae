using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// UAE VAT and Corporate Tax treatment for an advance and for a supply.
/// A free zone is inside the UAE for VAT unless the Cabinet has named it a
/// designated zone. A designated zone takes goods outside VAT only when they
/// are not consumed. Services stay taxable. Corporate Tax at 0% is only for
/// qualifying income of a Qualifying Free Zone Person.
/// </summary>
public static class ErpUaeSupplyTax
{
    public const string Mainland = "mainland";
    public const string FreeZone = "free_zone";
    public const string DesignatedZone = "designated_zone";
    public const string OutsideUae = "outside_uae";

    public const string Goods = "goods";
    public const string Services = "services";
    public const string RealEstate = "real_estate";

    public const string TaxInvoice = "tax_invoice";
    public const string TaxCreditNote = "tax_credit_note";
    public const string CommercialInvoice = "commercial_invoice";
    public const string CreditNote = "credit_note";

    public const string Customer = "customer";
    public const string Vendor = "vendor";

    public const decimal StandardVatRate = 5m;
    public const decimal CtRate = 0.09m;
    public const decimal CtBand = 375000m;
    public const decimal DeMinimisCap = 5000000m;
    public const decimal DeMinimisPercent = 0.05m;
    public const int DisqualifiedFollowingPeriods = 4;

    public sealed record Supply(
        string SellerZone,
        string BuyerZone,
        string Kind,
        bool? Consumed,
        bool LeavesDesignatedZone,
        bool Export,
        bool IncorporatedIntoUnconsumedGoods);

    public sealed record DocumentDecision(
        string Category,
        string CreditCategory,
        decimal VatRate,
        string Reason);

    public sealed record AdvancePosting(
        string Side,
        decimal Gross,
        decimal Net,
        decimal Vat,
        decimal Rate,
        string DebitAccount,
        string CreditNetAccount,
        string VatAccount,
        string DocumentCategory,
        string CreditCategory,
        string Reason);

    public sealed record Settlement(decimal InvoiceVat, decimal AdvanceVat, decimal Credit, decimal Remaining);

    public sealed record CtPosition(
        bool FreeZone,
        bool ElectedStandard,
        bool AdequateSubstance,
        bool ArmsLength,
        bool TransferPricingDocs,
        bool AuditedStatements,
        bool AlreadyDisqualified,
        decimal QualifyingRevenue,
        decimal NonQualifyingRevenue,
        decimal QualifyingIncome,
        decimal OtherIncome);

    public sealed record CtResult(
        bool QualifyingFreeZonePerson,
        decimal Tax,
        decimal ZeroRatedIncome,
        decimal TaxedIncome,
        string Label,
        IReadOnlyList<string> Reasons);

    public static DocumentDecision FromTreatment(string? treatment)
        => (treatment ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "zero_rated" => Document(new Supply(Mainland, OutsideUae, Goods, false, false, true, false)),
            "designated_goods" => Document(new Supply(DesignatedZone, DesignatedZone, Goods, false, false, false, false)),
            "designated_consumed" => Document(new Supply(DesignatedZone, DesignatedZone, Goods, true, false, false, false)),
            "designated_incorporated" => Document(new Supply(DesignatedZone, DesignatedZone, Goods, true, false, false, true)),
            "designated_unconfirmed" => Document(new Supply(DesignatedZone, DesignatedZone, Goods, null, false, false, false)),
            "designated_service" => Document(new Supply(DesignatedZone, DesignatedZone, Services, false, false, false, false)),
            "designated_real_estate" => Document(new Supply(DesignatedZone, DesignatedZone, RealEstate, false, false, false, false)),
            "free_zone" => Document(new Supply(FreeZone, Mainland, Goods, false, false, false, false)),
            "designated_exit" => Document(new Supply(DesignatedZone, Mainland, Goods, false, true, false, false)),
            _ => Document(new Supply(Mainland, Mainland, Goods, false, false, false, false)),
        };

    public static DocumentDecision Document(Supply supply)
    {
        ArgumentNullException.ThrowIfNull(supply);
        var seller = Zone(supply.SellerZone);
        var buyer = Zone(supply.BuyerZone);
        var kind = KindOf(supply.Kind);
        var export = supply.Export && buyer == OutsideUae;

        if (kind == Services)
        {
            if (export)
            {
                return Tax(0m, "Exported services are zero-rated. The document is still a tax invoice.");
            }

            if (seller == DesignatedZone)
            {
                return Tax(StandardVatRate, "Services in a designated zone stay inside the UAE. Issue a tax invoice at 5 percent.");
            }

            if (seller == FreeZone)
            {
                return Tax(StandardVatRate, "A free zone is inside the UAE for VAT unless the Cabinet has named it a designated zone. A service is still a tax invoice at 5 percent.");
            }

            return Tax(StandardVatRate, "This is a taxable supply of services in the UAE. Issue a tax invoice at 5 percent. A later adjustment is a tax credit note.");
        }

        if (kind == RealEstate && seller == DesignatedZone && !supply.LeavesDesignatedZone)
        {
            return Commercial("A sale or lease of real estate inside a designated zone is outside VAT. Issue a commercial invoice and, for a later adjustment, a credit note.");
        }

        if (export && kind == Goods)
        {
            return Tax(0m, "Exported goods are zero-rated. A movement into a designated zone is not an export. The document is still a tax invoice.");
        }

        if (seller == DesignatedZone && buyer == DesignatedZone && kind == Goods && !supply.LeavesDesignatedZone)
        {
            if (supply.Consumed is null)
            {
                return Tax(StandardVatRate, "Goods in a designated zone stay taxable until the buyer confirms they will not be consumed. Issue a tax invoice.");
            }

            if (supply.Consumed == true && !supply.IncorporatedIntoUnconsumedGoods)
            {
                return Tax(StandardVatRate, "Goods consumed in a designated zone are taxable. Issue a tax invoice at 5 percent.");
            }

            return Commercial("Goods that stay in a designated zone and are not consumed are outside VAT. Issue a commercial invoice, not a tax invoice. A later adjustment is a credit note, not a tax credit note.");
        }

        if (supply.LeavesDesignatedZone || (seller == DesignatedZone && buyer != DesignatedZone && kind == Goods))
        {
            return Tax(StandardVatRate, "Goods that leave a designated zone for the mainland are not outside VAT. Issue a tax invoice. Import VAT is recorded separately.");
        }

        if (seller == FreeZone)
        {
            return Tax(StandardVatRate, "A free zone is inside the UAE for VAT unless the Cabinet has named it a designated zone. Issue a tax invoice at 5 percent.");
        }

        return Tax(StandardVatRate, "This is a taxable supply in the UAE. Issue a tax invoice at 5 percent. A later adjustment is a tax credit note.");
    }

    public static AdvancePosting Advance(string? side, decimal amountInclusive, DocumentDecision document, bool supplierTaxInvoiceHeld, decimal standardRate = StandardVatRate)
    {
        ArgumentNullException.ThrowIfNull(document);
        var customer = !string.Equals((side ?? string.Empty).Trim(), Vendor, StringComparison.OrdinalIgnoreCase);
        var gross = Round(amountInclusive < 0m ? 0m : amountInclusive);
        var rate = document.VatRate > 0m ? Round(standardRate < 0m ? 0m : standardRate) : 0m;
        var recover = customer || supplierTaxInvoiceHeld;
        if (!recover)
        {
            rate = 0m;
        }

        var net = rate <= 0m ? gross : Round(gross / (1m + (rate / 100m)));
        var vat = Round(gross - net);
        var reason = document.Reason;
        if (!customer && !supplierTaxInvoiceHeld && document.VatRate > 0m)
        {
            reason = "Input VAT on a vendor advance waits until the supplier tax invoice is held. The whole payment stays on the supplier advance until then.";
        }
        else if (customer && vat > 0m)
        {
            reason = "VAT is due on the customer advance to the extent of the payment, on the earlier of receipt and the tax invoice. Issue the tax invoice within 14 days. The final invoice takes this VAT off so it is not charged twice. " + document.Reason;
        }
        else if (!customer && vat > 0m)
        {
            reason = "Input VAT on the vendor advance is recoverable now because the supplier tax invoice is held. The purchase invoice recovers only the VAT that is not already on this advance. " + document.Reason;
        }

        return new AdvancePosting(
            customer ? Customer : Vendor,
            gross,
            net,
            vat,
            vat > 0m ? rate : 0m,
            customer ? "Bank or cash" : "2060 Supplier advances",
            customer ? "2050 Customer advances" : "Bank or cash",
            vat <= 0m ? string.Empty : customer ? "2100 Output VAT" : "1150 Input VAT",
            document.Category,
            document.CreditCategory,
            reason);
    }

    public static Settlement Settle(decimal invoiceVat, decimal openAdvanceVat)
    {
        var invoice = Round(invoiceVat < 0m ? 0m : invoiceVat);
        var open = Round(openAdvanceVat < 0m ? 0m : openAdvanceVat);
        var credit = Round(Math.Min(invoice, open));
        return new Settlement(invoice, open, credit, Round(invoice - credit));
    }

    public static CtResult CorporateTax(CtPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        var reasons = new List<string>();
        var qualifyingIncome = Round(position.QualifyingIncome);
        var otherIncome = Round(position.OtherIncome);
        var qualifyingRevenue = Round(position.QualifyingRevenue);
        var otherRevenue = Round(position.NonQualifyingRevenue);
        if (qualifyingIncome < 0m || otherIncome < 0m || qualifyingRevenue < 0m || otherRevenue < 0m)
        {
            return new CtResult(false, 0m, 0m, 0m, "Check the figures", ["Revenue and income cannot be negative."]);
        }

        var taxable = Round(qualifyingIncome + otherIncome);
        if (!position.FreeZone)
        {
            var band = StandardTax(taxable);
            reasons.Add("A mainland person pays 0 percent on taxable income up to AED 375,000 and 9 percent above that.");
            return new CtResult(false, band, taxable <= CtBand ? taxable : CtBand, Round(Math.Max(0m, taxable - CtBand)), "Standard Corporate Tax", reasons);
        }

        if (position.ElectedStandard)
        {
            reasons.Add("This free zone person elected the standard Corporate Tax rates.");
        }

        if (position.AlreadyDisqualified)
        {
            reasons.Add("This free zone person already lost Qualifying Free Zone status. The loss runs for that tax period and the next four tax periods.");
        }

        if (!position.AdequateSubstance)
        {
            reasons.Add("Adequate substance in a free zone is missing.");
        }

        if (!position.ArmsLength)
        {
            reasons.Add("Related-party transactions are not at arm's length.");
        }

        if (!position.TransferPricingDocs)
        {
            reasons.Add("Transfer pricing documentation is missing.");
        }

        if (!position.AuditedStatements)
        {
            reasons.Add("Audited financial statements are missing.");
        }

        var totalRevenue = Round(qualifyingRevenue + otherRevenue);
        var ceiling = DeMinimisCeiling(totalRevenue);
        var deMinimis = otherRevenue <= ceiling + 0.001m;
        if (!deMinimis)
        {
            reasons.Add("Non-qualifying revenue is above the de minimis ceiling of AED " + Money(ceiling) + ", the lower of AED 5,000,000 and 5 percent of total revenue.");
        }

        var conditions = !position.ElectedStandard
            && !position.AlreadyDisqualified
            && position.AdequateSubstance
            && position.ArmsLength
            && position.TransferPricingDocs
            && position.AuditedStatements
            && deMinimis;
        if (!conditions)
        {
            var failedBesidesElection = position.AlreadyDisqualified
                || !position.AdequateSubstance
                || !position.ArmsLength
                || !position.TransferPricingDocs
                || !position.AuditedStatements
                || !deMinimis;
            if (failedBesidesElection && !position.AlreadyDisqualified)
            {
                reasons.Add("Qualifying Free Zone status is lost from the start of this tax period and for the next four tax periods.");
            }

            var band = StandardTax(taxable);
            reasons.Add("The standard rates then apply: 0 percent up to AED 375,000 and 9 percent above that.");
            return new CtResult(false, band, taxable <= CtBand ? taxable : CtBand, Round(Math.Max(0m, taxable - CtBand)), "Standard Corporate Tax", reasons);
        }

        var otherTax = Round(otherIncome * CtRate);
        reasons.Add("Qualifying income is at 0 percent. Other taxable income is at 9 percent from the first dirham. The AED 375,000 band is not used.");
        reasons.Add("A free zone does not remove VAT. A tax invoice is still issued for a taxable supply.");
        return new CtResult(true, otherTax, qualifyingIncome, otherIncome, "Qualifying Free Zone Person", reasons);
    }

    public static decimal DeMinimisCeiling(decimal totalRevenue)
    {
        var total = Round(totalRevenue < 0m ? 0m : totalRevenue);
        var percent = Round(total * DeMinimisPercent);
        return Round(Math.Min(DeMinimisCap, percent));
    }

    public static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string Money(decimal value)
        => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static decimal StandardTax(decimal taxable)
        => Round(Math.Max(0m, taxable - CtBand) * CtRate);

    private static DocumentDecision Tax(decimal rate, string reason)
        => new(TaxInvoice, TaxCreditNote, rate, reason);

    private static DocumentDecision Commercial(string reason)
        => new(CommercialInvoice, CreditNote, 0m, reason);

    private static string Zone(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            FreeZone => FreeZone,
            DesignatedZone => DesignatedZone,
            OutsideUae => OutsideUae,
            _ => Mainland,
        };

    private static string KindOf(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Services => Services,
            RealEstate => RealEstate,
            _ => Goods,
        };
}
