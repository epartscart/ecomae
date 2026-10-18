using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpUaeSupplyTaxTests
{
    [Fact]
    public void Designated_zone_goods_use_a_commercial_invoice_only_when_they_are_not_consumed()
    {
        var kept = ErpUaeSupplyTax.FromTreatment("designated_goods");
        Assert.Equal(ErpUaeSupplyTax.CommercialInvoice, kept.Category);
        Assert.Equal(ErpUaeSupplyTax.CreditNote, kept.CreditCategory);
        Assert.Equal(0m, kept.VatRate);

        var consumed = ErpUaeSupplyTax.FromTreatment("designated_consumed");
        Assert.Equal(ErpUaeSupplyTax.TaxInvoice, consumed.Category);
        Assert.Equal(ErpUaeSupplyTax.TaxCreditNote, consumed.CreditCategory);
        Assert.Equal(5m, consumed.VatRate);

        var incorporated = ErpUaeSupplyTax.FromTreatment("designated_incorporated");
        Assert.Equal(ErpUaeSupplyTax.CommercialInvoice, incorporated.Category);
        Assert.Equal(0m, incorporated.VatRate);

        var unconfirmed = ErpUaeSupplyTax.FromTreatment("designated_unconfirmed");
        Assert.Equal(ErpUaeSupplyTax.TaxInvoice, unconfirmed.Category);
        Assert.Equal(5m, unconfirmed.VatRate);
        Assert.Contains("confirms", unconfirmed.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Services_exports_and_free_zones_keep_a_tax_invoice()
    {
        var service = ErpUaeSupplyTax.FromTreatment("designated_service");
        Assert.Equal(ErpUaeSupplyTax.TaxInvoice, service.Category);
        Assert.Equal(5m, service.VatRate);

        var freeZone = ErpUaeSupplyTax.FromTreatment("free_zone");
        Assert.Equal(ErpUaeSupplyTax.TaxInvoice, freeZone.Category);
        Assert.Equal(5m, freeZone.VatRate);
        Assert.Contains("designated zone", freeZone.Reason, StringComparison.OrdinalIgnoreCase);

        var estate = ErpUaeSupplyTax.FromTreatment("designated_real_estate");
        Assert.Equal(ErpUaeSupplyTax.CommercialInvoice, estate.Category);
        Assert.Equal(ErpUaeSupplyTax.CreditNote, estate.CreditCategory);
        Assert.Equal(0m, estate.VatRate);

        var exit = ErpUaeSupplyTax.FromTreatment("designated_exit");
        Assert.Equal(ErpUaeSupplyTax.TaxInvoice, exit.Category);
        Assert.Equal(5m, exit.VatRate);

        var export = ErpUaeSupplyTax.FromTreatment("zero_rated");
        Assert.Equal(ErpUaeSupplyTax.TaxInvoice, export.Category);
        Assert.Equal(0m, export.VatRate);
    }

    [Fact]
    public void Customer_and_vendor_advances_post_vat_only_to_the_extent_of_the_payment()
    {
        var customer = ErpUaeSupplyTax.Advance(
            ErpUaeSupplyTax.Customer,
            105m,
            ErpUaeSupplyTax.FromTreatment("taxable"),
            true);
        Assert.Equal(100m, customer.Net);
        Assert.Equal(5m, customer.Vat);
        Assert.Equal("2050 Customer advances", customer.CreditNetAccount);
        Assert.Equal("2100 Output VAT", customer.VatAccount);
        Assert.Contains("14 days", customer.Reason, StringComparison.OrdinalIgnoreCase);

        var zone = ErpUaeSupplyTax.Advance(
            ErpUaeSupplyTax.Customer,
            105m,
            ErpUaeSupplyTax.FromTreatment("designated_goods"),
            true);
        Assert.Equal(0m, zone.Vat);
        Assert.Equal(105m, zone.Net);
        Assert.Equal("2050 Customer advances", zone.CreditNetAccount);
        Assert.Equal(string.Empty, zone.VatAccount);
        Assert.Equal(ErpUaeSupplyTax.CommercialInvoice, zone.DocumentCategory);

        var vendor = ErpUaeSupplyTax.Advance(
            ErpUaeSupplyTax.Vendor,
            105m,
            ErpUaeSupplyTax.FromTreatment("taxable"),
            true);
        Assert.Equal(100m, vendor.Net);
        Assert.Equal(5m, vendor.Vat);
        Assert.Equal("2060 Supplier advances", vendor.DebitAccount);
        Assert.Equal("1150 Input VAT", vendor.VatAccount);

        var waiting = ErpUaeSupplyTax.Advance(
            ErpUaeSupplyTax.Vendor,
            105m,
            ErpUaeSupplyTax.FromTreatment("taxable"),
            false);
        Assert.Equal(0m, waiting.Vat);
        Assert.Equal(105m, waiting.Net);
        Assert.Contains("tax invoice", waiting.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_final_invoice_does_not_charge_advance_vat_again()
    {
        var partial = ErpUaeSupplyTax.Settle(50m, 10m);
        Assert.Equal(10m, partial.Credit);
        Assert.Equal(40m, partial.Remaining);

        var none = ErpUaeSupplyTax.Settle(0m, 10m);
        Assert.Equal(0m, none.Credit);
        Assert.Equal(0m, none.Remaining);
    }

    [Fact]
    public void De_minimis_uses_the_lower_of_five_percent_and_five_million()
    {
        Assert.True(Qualifies(9_500_000m, 500_000m));
        Assert.False(Qualifies(9_499_999.99m, 500_000.01m));
        Assert.True(Qualifies(195_000_000m, 5_000_000m));
        Assert.False(Qualifies(194_999_999.99m, 5_000_000.01m));
        Assert.Equal(500_000m, ErpUaeSupplyTax.DeMinimisCeiling(10_000_000m));
        Assert.Equal(5_000_000m, ErpUaeSupplyTax.DeMinimisCeiling(200_000_000m));
    }

    [Fact]
    public void Mainland_and_qualifying_free_zone_corporate_tax_follow_the_statute()
    {
        var mainland = Tax(false, 0m, 0m, 400_000m, 0m);
        Assert.False(mainland.QualifyingFreeZonePerson);
        Assert.Equal(2_250m, mainland.Tax);

        var band = Tax(false, 0m, 0m, 375_000m, 0m);
        Assert.Equal(0m, band.Tax);

        var zone = Tax(true, 10_000_000m, 0m, 1_000_000m, 100_000m, substance: true, arms: true, docs: true, audited: true);
        Assert.True(zone.QualifyingFreeZonePerson);
        Assert.Equal(9_000m, zone.Tax);
        Assert.Contains("first dirham", string.Join(" ", zone.Reasons), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not remove VAT", string.Join(" ", zone.Reasons), StringComparison.OrdinalIgnoreCase);

        var substance = Tax(true, 10_000_000m, 0m, 1_000_000m, 0m, substance: false, arms: true, docs: true, audited: true);
        Assert.False(substance.QualifyingFreeZonePerson);
        Assert.Contains("next four", string.Join(" ", substance.Reasons), StringComparison.OrdinalIgnoreCase);

        var elected = Tax(true, 10_000_000m, 0m, 400_000m, 0m, substance: true, arms: true, docs: true, audited: true, elected: true);
        Assert.False(elected.QualifyingFreeZonePerson);
        Assert.Equal(2_250m, elected.Tax);
        Assert.Contains("elected", string.Join(" ", elected.Reasons), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("substance", string.Join(" ", elected.Reasons), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("next four", string.Join(" ", elected.Reasons), StringComparison.OrdinalIgnoreCase);

        var negative = ErpUaeSupplyTax.CorporateTax(new ErpUaeSupplyTax.CtPosition(
            true, false, true, true, true, true, false, -1m, 0m, 0m, 0m));
        Assert.False(negative.QualifyingFreeZonePerson);
        Assert.Contains("negative", negative.Reasons[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_tax_page_and_cash_vouchers_carry_the_treatment()
    {
        var root = RepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpUaeTaxComplianceApp.razor"));
        var panel = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpUaeSupplyTaxPanel.razor"));
        var cash = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpCashEntriesApp.razor"));
        Assert.Contains("<ErpUaeSupplyTaxPanel />", page, StringComparison.Ordinal);
        Assert.Contains("supply_treatment", cash, StringComparison.Ordinal);
        Assert.Contains("designated_goods", cash, StringComparison.Ordinal);
        Assert.Contains("supplier_tax_invoice", cash, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", panel, StringComparison.Ordinal);
    }

    private static bool Qualifies(decimal qualifyingRevenue, decimal otherRevenue)
        => Tax(true, qualifyingRevenue, otherRevenue, qualifyingRevenue, 0m, true, true, true, true).QualifyingFreeZonePerson;

    private static ErpUaeSupplyTax.CtResult Tax(
        bool freeZone,
        decimal qualifyingRevenue,
        decimal otherRevenue,
        decimal qualifyingIncome,
        decimal otherIncome,
        bool substance = false,
        bool arms = false,
        bool docs = false,
        bool audited = false,
        bool elected = false)
        => ErpUaeSupplyTax.CorporateTax(new ErpUaeSupplyTax.CtPosition(
            freeZone,
            elected,
            substance,
            arms,
            docs,
            audited,
            false,
            qualifyingRevenue,
            otherRevenue,
            qualifyingIncome,
            otherIncome));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
