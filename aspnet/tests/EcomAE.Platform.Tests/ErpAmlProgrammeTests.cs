using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAmlProgrammeTests
{
    [Fact]
    public void Countermeasure_invoices_are_refused_and_the_call_for_action_is_held()
    {
        Assert.Contains("refused", ErpAmlProgramme.InvoiceRestriction("IR")!, StringComparison.Ordinal);
        Assert.Contains("refused", ErpAmlProgramme.InvoiceRestriction("kp")!, StringComparison.Ordinal);
        var held = ErpAmlProgramme.InvoiceRestriction("MM");
        Assert.Contains("held", held, StringComparison.Ordinal);
        Assert.DoesNotContain("embargo", held, StringComparison.OrdinalIgnoreCase);
        Assert.Null(ErpAmlProgramme.InvoiceRestriction("AE"));
        Assert.Null(ErpAmlProgramme.InvoiceRestriction("ARE"));
        Assert.Null(ErpAmlProgramme.InvoiceRestriction("KW"));
        Assert.Contains("increased monitoring", ErpAmlProgramme.ScreenSummary("KW"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not refused", ErpAmlProgramme.ScreenSummary("KW"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_pack_covers_registration_through_reporting()
    {
        var text = string.Join("\n", ErpAmlProgramme.Documents.SelectMany(doc => doc.Sections.Select(section => section.Heading + " " + section.Body)));
        Assert.Contains("Federal Decree-Law No. 10 of 2025", text, StringComparison.Ordinal);
        Assert.Contains("Cabinet Resolution No. 134 of 2025", text, StringComparison.Ordinal);
        Assert.Contains("AED 55,000", text, StringComparison.Ordinal);
        Assert.Contains("AED 100,000", text, StringComparison.Ordinal);
        Assert.Contains("goAML", text, StringComparison.Ordinal);
        Assert.Contains("24 hours", text, StringComparison.Ordinal);
        Assert.Contains("five business days", text, StringComparison.Ordinal);
        Assert.Contains("10 business days", text, StringComparison.Ordinal);
        Assert.Contains("five years", text, StringComparison.Ordinal);
        Assert.Contains("DPMSR", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kimberley", text, StringComparison.Ordinal);
        Assert.Contains("Cabinet Resolution No. 74 of 2020", text, StringComparison.Ordinal);
        Assert.Equal(13, ErpAmlProgramme.Documents.Count);
        Assert.Equal(6, ErpAmlProgramme.Steps.Count);
    }

    [Fact]
    public void Sales_invoice_refuses_a_countermeasure_buyer()
    {
        var buyer = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["buyer_name"] = "Customer LLC",
            ["buyer_trn"] = "",
            ["buyer_address_line1"] = "Street 4",
            ["buyer_city"] = "Tehran",
            ["buyer_emirate"] = "—",
            ["buyer_country_code"] = "IR",
            ["buyer_peppol_endpoint"] = "0235:9900000098",
        };
        var errors = ErpSalesInvoiceWriteService.ValidateTaxInvoiceContent(Seller(), buyer, Lines(), 5m);
        Assert.Contains(errors, error => error.Contains("refused", StringComparison.Ordinal) && error.Contains("Iran", StringComparison.Ordinal));
        Assert.Empty(ErpSalesInvoiceWriteService.ValidateTaxInvoiceContent(Seller(), Buyer("KW"), Lines(), 5m));
    }

    [Fact]
    public void The_aml_page_keeps_the_guide_the_fetch_and_the_existing_forms()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpAmlComplianceApp.razor"));
        Assert.Contains("Visual guide", text, StringComparison.Ordinal);
        Assert.Contains("Fetch AML legislation", text, StringComparison.Ordinal);
        Assert.Contains("/erp/aml/legislation-fetch", text, StringComparison.Ordinal);
        Assert.Contains("Save a KYC record", text, StringComparison.Ordinal);
        Assert.Contains("AED 55,000", text, StringComparison.Ordinal);
        Assert.Contains("/erp/suppliers-app", text, StringComparison.Ordinal);
        Assert.Contains("/erp/uae-tax/fta-fetch", text, StringComparison.Ordinal);
        Assert.Contains("No transactions yet.", text, StringComparison.Ordinal);
        Assert.Contains("class=\"hpanel\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-aml-hero", text, StringComparison.Ordinal);
        Assert.Contains("IErpAmlLegislationFetchWriteService", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Seller() => new(StringComparer.Ordinal)
    {
        ["seller_name"] = "Tenant FZ LLC",
        ["seller_trn"] = "100123456700003",
        ["seller_legal_reg_no"] = "TL-9911",
        ["seller_legal_reg_type"] = "TL",
        ["seller_address_line1"] = "Office 12, Business Bay",
        ["seller_city"] = "Dubai",
        ["seller_emirate"] = "Dubai",
        ["seller_country_code"] = "AE",
        ["seller_peppol_endpoint"] = "0235:1001234567",
    };

    private static Dictionary<string, string> Buyer(string country) => new(StringComparer.Ordinal)
    {
        ["buyer_name"] = "Customer LLC",
        ["buyer_trn"] = "",
        ["buyer_address_line1"] = "Street 4",
        ["buyer_city"] = "City",
        ["buyer_emirate"] = "—",
        ["buyer_country_code"] = country,
        ["buyer_peppol_endpoint"] = "0235:9900000098",
    };

    private static ErpSalesInvoiceLine[] Lines() =>
    [
        new ErpSalesInvoiceLine(1, "Widget", 2m, 50m, 100m, "S", 5m, 5m, 105m),
    ];

    private static string Find(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."), relative);
    }
}
