using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpTouristRefundLawTests
{
    [Fact]
    public void Fee_follows_decision_11_of_2026()
    {
        var quote = ErpTouristRefundLaw.Calculate(50m);
        Assert.Equal(39.90m, quote.Refund);
        Assert.Equal(3.60m, quote.FixedFee);
        Assert.Equal(10.10m, quote.Retained);
        var small = ErpTouristRefundLaw.Calculate(12.50m);
        Assert.Equal(7.28m, small.Refund);
        Assert.Equal(5.22m, small.Retained);
    }

    [Fact]
    public void Gates_follow_the_scheme()
    {
        Assert.Null(ErpTouristRefundLaw.Refusal(Sample()));
        Assert.Contains("Passport", ErpTouristRefundLaw.Refusal(Sample() with { PassportNo = "" }), StringComparison.Ordinal);
        Assert.Contains("UAE resident", ErpTouristRefundLaw.Refusal(Sample() with { Nationality = "AE" }), StringComparison.Ordinal);
        Assert.Contains("Services", ErpTouristRefundLaw.Refusal(Sample() with { SupplyKind = "services" }), StringComparison.Ordinal);
        Assert.Contains("courier", ErpTouristRefundLaw.Refusal(Sample() with { SupplyKind = "courier" }), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Article 5", ErpTouristRefundLaw.Refusal(Sample() with { TotalInclusive = 200m, VatAmount = 9.52m }), StringComparison.Ordinal);
        Assert.Contains("250 before VAT", ErpTouristRefundLaw.Refusal(Sample() with { TotalInclusive = 250m, VatAmount = 11.90m }), StringComparison.Ordinal);
        Assert.Contains("35,000", ErpTouristRefundLaw.Refusal(Sample() with { RefundMethod = "cash", VatAmount = 50_000m, TotalInclusive = 1_050_000m, CashRefundedInDay = 34_000m }), StringComparison.Ordinal);
        var late = Sample() with
        {
            SupplyDate = new DateOnly(2026, 1, 1),
            DepartureDate = new DateOnly(2026, 5, 1),
        };
        Assert.Contains("90 days", ErpTouristRefundLaw.Refusal(late), StringComparison.Ordinal);
    }

    [Fact]
    public void Barcode_is_code_128_and_prints_on_the_tax_invoice()
    {
        var bits = ErpTouristRefundLaw.EncodeCode128B("HI");
        Assert.StartsWith("11010010000", bits, StringComparison.Ordinal);
        Assert.EndsWith("1100011101011", bits, StringComparison.Ordinal);
        var svg = ErpTouristRefundLaw.BarcodeBlock(ErpTouristRefundLaw.SampleBarcode);
        Assert.Contains(ErpTouristRefundLaw.SampleBarcode, svg, StringComparison.Ordinal);
        Assert.Contains("<svg", svg, StringComparison.Ordinal);
        var html = StorefrontPhpAjax.TaxInvoicePrintHtml(new StorefrontPhpAjax.TaxInvoicePrint(
            "INV-1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["seller_name"] = "Acme LLC",
                ["seller_trn"] = "100123456700003",
                ["seller_address_line1"] = "Dubai",
                ["seller_authority_name"] = "Dubai Economy and Tourism",
                ["seller_legal_reg_type"] = "TL",
                ["seller_legal_reg_no"] = "100",
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["buyer_name"] = "Visitor",
                ["buyer_address_line1"] = "Hotel",
            },
            1_700_000_000,
            1_700_000_000,
            1_700_086_400,
            "AED",
            "380",
            40,
            [new StorefrontPhpAjax.TaxInvoicePrintLine(1, "Watch", 1, 1000, 1000, 5, 50, 1050)],
            1000,
            50,
            1050,
            1050,
            "Due on receipt",
            "11111111-1111-1111-1111-111111111111",
            ErpTouristRefundLaw.SampleBarcode));
        Assert.Contains("No QR code", html, StringComparison.Ordinal);
        Assert.Contains("Planet tax-free tag", html, StringComparison.Ordinal);
        Assert.Contains(ErpTouristRefundLaw.SampleBarcode, html, StringComparison.Ordinal);
        Assert.Contains("39.90", html, StringComparison.Ordinal);
        Assert.DoesNotContain("QR code preview", html, StringComparison.Ordinal);
        var missing = ErpDocumentDesign.Check("invoice", html).Where(item => !item.Met).Select(item => item.Label).ToArray();
        Assert.True(missing.Length == 0, string.Join(", ", missing));
    }

    [Fact]
    public void Vat_page_keeps_the_guide_the_barcode_and_the_books()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpVatApp.razor"));
        Assert.Contains("ErpTouristRefundLaw.Amendment", text, StringComparison.Ordinal);
        Assert.Contains("Decision No. 11 of 2026", ErpTouristRefundLaw.Amendment, StringComparison.Ordinal);
        Assert.Contains("epc-tr-steps", text, StringComparison.Ordinal);
        Assert.Contains("epc-tr-books", text, StringComparison.Ordinal);
        Assert.Contains("/erp/tourist-refund/planet-invoice", text, StringComparison.Ordinal);
        Assert.Contains("/erp/tourist-refund/planet-pay", text, StringComparison.Ordinal);
        Assert.Contains("Output VAT 2100", text, StringComparison.Ordinal);
        Assert.Contains("AED 35,000", text, StringComparison.Ordinal);
        Assert.Contains("90 days", text, StringComparison.Ordinal);
        Assert.Contains("Fetch FTA legislation", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", text, StringComparison.Ordinal);
    }

    private static ErpTouristRefundClaim Sample()
        => new("P1234567", "GB", 1_050m, 50m, "goods", "card", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20));

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
