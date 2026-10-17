using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpDocumentDesignTests
{
    [Fact]
    public void Every_law_sample_passes_its_own_marks()
    {
        foreach (var kind in ErpDocumentDesign.Kinds)
        {
            var html = ErpDocumentDesign.SampleHtml(kind.Code);
            var missing = ErpDocumentDesign.Check(kind.Code, html).Where(item => !item.Met).Select(item => item.Label).ToArray();
            Assert.True(missing.Length == 0, kind.Code + ": " + string.Join(", ", missing));
        }
    }

    [Fact]
    public void Tax_invoice_sample_carries_vat_and_einvoice_marks()
    {
        var html = ErpDocumentDesign.SampleHtml("invoice");
        Assert.Contains("Tax Invoice", html, StringComparison.Ordinal);
        Assert.Contains("0235:1000000000", html, StringComparison.Ordinal);
        Assert.Contains("No QR code", html, StringComparison.Ordinal);
        Assert.Contains("Preceding invoice", html, StringComparison.Ordinal);
        Assert.Contains("Exchange rate", html, StringComparison.Ordinal);
        Assert.Contains("Unit price", html, StringComparison.Ordinal);
        Assert.DoesNotContain("QR code preview", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Stored_sales_invoice_wording_is_missing_einvoice_marks()
    {
        var row = ErpDocumentControlRender.DefaultTemplates.First(item => item.Code == "fta_tax_invoice");
        var findings = ErpDocumentDesign.Check("invoice", row.HeaderHtml + row.BodyHtml + row.FooterHtml);
        Assert.Contains(findings, item => item.Label == "UUID" && !item.Met);
        Assert.Contains(findings, item => item.Label == "Electronic address" && !item.Met);
        Assert.Contains(findings, item => item.Label == "Discount" && !item.Met);
        Assert.Contains(findings, item => item.Label == "Exchange rate" && !item.Met);
        Assert.Contains(findings, item => item.Label == "Preceding invoice" && !item.Met);
        Assert.Contains(findings, item => item.Label == "No QR code" && !item.Met);
        Assert.Contains(findings, item => item.Label == "Tax Invoice" && item.Met);
        Assert.Contains(findings, item => item.Label == "TRN" && item.Met);
    }

    [Fact]
    public void Sales_tax_invoice_print_passes_the_law_check()
    {
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
                ["buyer_name"] = "Buyer LLC",
                ["buyer_trn"] = "100398631900003",
                ["buyer_address_line1"] = "Abu Dhabi",
            },
            1_700_000_000,
            1_700_000_000,
            1_700_086_400,
            "AED",
            "380",
            40,
            [new StorefrontPhpAjax.TaxInvoicePrintLine(1, "Filter", 1, 100, 100, 5, 5, 105)],
            100,
            5,
            105,
            105,
            "Due on receipt",
            "11111111-1111-1111-1111-111111111111"));
        var missing = ErpDocumentDesign.Check("invoice", html).Where(item => !item.Met).Select(item => item.Label).ToArray();
        Assert.True(missing.Length == 0, string.Join(", ", missing));
        Assert.Contains("0235:1001234567", html, StringComparison.Ordinal);
        Assert.DoesNotContain("QR code preview", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Purchase_tax_invoice_print_passes_the_law_check()
    {
        var html = ErpPurchaseDocumentPrint.Render(new ErpPurchasePrintDoc(
            ErpPurchasePrintKind.PurchaseInvoice,
            "PI-9",
            "03/10/2026",
            "Cash",
            "Buyer",
            "AED",
            string.Empty,
            true,
            "confirmed",
            new ErpPurchasePrintParty("", "INDUS JEWELLERS L.L.C", "Al Fahidi, Dubai, UAE", "04 3516668", "accounts@example.com", "100123456789012"),
            new ErpPurchasePrintParty("RJ50012", "Rizan Jewellery L.L.C", "Bur Dubai", "04 1234567", "rizan@example.com", "100398631900003"),
            [new ErpPurchasePrintLine(1, "Filter kit", 1, 100, 100, 5, 5, 105)],
            [],
            100,
            5,
            105,
            string.Empty,
            "LPO to purchase invoice"));
        var missing = ErpDocumentDesign.Check("invoice", html).Where(item => !item.Met).Select(item => item.Label).ToArray();
        Assert.True(missing.Length == 0, string.Join(", ", missing));
        Assert.Contains("TAX INVOICE / PURCHASE", html, StringComparison.Ordinal);
        Assert.DoesNotContain("DRAFT", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Designer_page_keeps_the_guide_editor_and_preview_inside()
    {
        var razor = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPrintDesignerApp.razor"));
        Assert.Contains("Visual guide", razor, StringComparison.Ordinal);
        Assert.Contains("Preview", razor, StringComparison.Ordinal);
        Assert.Contains("New print template", razor, StringComparison.Ordinal);
        Assert.Contains("srcdoc=\"@PreviewDocument\"", razor, StringComparison.Ordinal);
        Assert.Contains("srcdoc=\"@PreviewWindow\"", razor, StringComparison.Ordinal);
        Assert.Contains("srcdoc=\"@SampleDocument\"", razor, StringComparison.Ordinal);
        Assert.Contains("full HTML/CSS bodies", razor, StringComparison.Ordinal);
        Assert.Contains("{{company_name}}", razor, StringComparison.Ordinal);
        Assert.Contains("{{printed_date}}", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"header_html\"", razor, StringComparison.Ordinal);
        Assert.Contains("payment_voucher", razor, StringComparison.Ordinal);
        Assert.Contains("posted_journal", razor, StringComparison.Ordinal);
        Assert.Contains("posted_report", razor, StringComparison.Ordinal);
        Assert.Contains("No QR code", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/einvoice-documents-app?tab=guide", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", razor, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
