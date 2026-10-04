using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpInvoiceFromOrderWriteServiceTests
{
    private static Dictionary<string, string> Seller(string countryCode = "AE", string trn = "100123456700003") => new(StringComparer.Ordinal)
    {
        ["seller_name"] = "Tenant FZ LLC",
        ["seller_trn"] = trn,
        ["seller_legal_reg_no"] = "TL-9911",
        ["seller_legal_reg_type"] = "TL",
        ["seller_address_line1"] = "Office 12, Business Bay",
        ["seller_city"] = "Dubai",
        ["seller_emirate"] = "Dubai",
        ["seller_country_code"] = countryCode,
        ["seller_peppol_endpoint"] = "0235:1001234567",
    };

    private static Dictionary<string, string> Buyer(string countryCode = "AE", string trn = "") => new(StringComparer.Ordinal)
    {
        ["buyer_name"] = "Customer LLC",
        ["buyer_trn"] = trn,
        ["buyer_address_line1"] = "Street 4",
        ["buyer_city"] = "Dubai",
        ["buyer_emirate"] = "Dubai",
        ["buyer_country_code"] = countryCode,
        ["buyer_peppol_endpoint"] = "0235:9900000098",
    };

    private static ErpInvoiceFromOrderLine StandardLine()
        => ErpInvoiceFromOrderWriteService.ComputeOrderLine(10m, 2m, "S", 5m, false, true, 1, "ACME 123", "Brake pad", "G");

    [Theory]
    [InlineData(false, "00000000")]
    [InlineData(true, "00000001")]
    public void TransactionCodeSetsOnlyTheExportsBit(bool exports, string expected)
        => Assert.Equal(expected, ErpInvoiceFromOrderWriteService.BuildTransactionTypeCode(exports));

    [Theory]
    [InlineData("AE", false, "S", 5)]
    [InlineData("AE", true, "Z", 0)]
    [InlineData("SA", false, "Z", 0)]
    [InlineData("GB", false, "Z", 0)]
    public void SupplyCategoryZeroRatesNonAeAndExports(string country, bool exports, string cat, int rate)
    {
        var (category, r) = ErpInvoiceFromOrderWriteService.SupplyCategory(country, exports, 5m);
        Assert.Equal(cat, category);
        Assert.Equal(rate, r);
    }

    [Fact]
    public void ExclusiveLineSplitsVatOnTopOfStoredPrice()
    {
        var line = ErpInvoiceFromOrderWriteService.ComputeOrderLine(100m, 2m, "S", 5m, inclusive: false, salesEnabled: true, 1, "P", "", "G");
        Assert.Equal(200.00m, line.LineNet);
        Assert.Equal(10.00m, line.TaxAmount);
        Assert.Equal(210.00m, line.GrossAmount);
        Assert.Equal(100m, line.UnitPrice);
        Assert.Equal("C62", line.UomCode);
        Assert.Equal("S", line.TaxCategory);
    }

    [Fact]
    public void InclusiveLineSplitsStoredGrossIntoNetAndVat()
    {
        var line = ErpInvoiceFromOrderWriteService.ComputeOrderLine(105m, 1m, "S", 5m, inclusive: true, salesEnabled: true, 1, "P", "", "G");
        Assert.Equal(100.00m, line.LineNet);
        Assert.Equal(5.00m, line.TaxAmount);
        Assert.Equal(105.00m, line.GrossAmount);
        Assert.Equal(100.0000m, line.UnitNet);
    }

    [Fact]
    public void ZeroRateOrDisabledSalesKeepStoredPriceUntouched()
    {
        var zeroRated = ErpInvoiceFromOrderWriteService.ComputeOrderLine(50m, 3m, "Z", 0m, false, true, 1, "P", "", "G");
        Assert.Equal(150.00m, zeroRated.LineNet);
        Assert.Equal(0m, zeroRated.TaxAmount);
        Assert.Equal(150.00m, zeroRated.GrossAmount);

        var disabled = ErpInvoiceFromOrderWriteService.ComputeOrderLine(50m, 3m, "S", 5m, false, salesEnabled: false, 1, "P", "", "G");
        Assert.Equal(0m, disabled.TaxAmount);
        Assert.Equal(0m, disabled.TaxRate);
    }

    [Fact]
    public void BlankItemNameFallsBackToPartLine()
    {
        var line = ErpInvoiceFromOrderWriteService.ComputeOrderLine(1m, 1m, "S", 0m, false, true, 7, "  ", "", "G");
        Assert.Equal("Part line 7", line.ItemName);
    }

    [Fact]
    public void ValidationRequiresSellerCountryAeAndFtaTrn()
    {
        var errors = ErpInvoiceFromOrderWriteService.ValidateTaxInvoice(
            Seller(countryCode: "SA"),
            Buyer(),
            [StandardLine()],
            10m,
            0.5m,
            tenantVatRegistered: true);
        Assert.Contains(errors, e => e.Contains("Seller country must be AE", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationRejectsUnregisteredTenantAndShortSellerTrn()
    {
        var errors = ErpInvoiceFromOrderWriteService.ValidateTaxInvoice(
            Seller(trn: "123"),
            Buyer(),
            [StandardLine()],
            10m,
            0.5m,
            tenantVatRegistered: false);
        Assert.Contains(errors, e => e.Contains("VAT-registered", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("15 digits", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationRejectsMalformedAeBuyerTrn()
    {
        var errors = ErpInvoiceFromOrderWriteService.ValidateTaxInvoice(
            Seller(),
            Buyer(trn: "999"),
            [StandardLine()],
            10m,
            0.5m,
            tenantVatRegistered: true);
        Assert.Contains(errors, e => e.Contains("UAE buyer TRN", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationPassesForCompleteDomesticTaxInvoice()
    {
        var errors = ErpInvoiceFromOrderWriteService.ValidateTaxInvoice(
            Seller(),
            Buyer(),
            [StandardLine()],
            10m,
            0.5m,
            tenantVatRegistered: true);
        Assert.Empty(errors);
    }

    [Fact]
    public void XmlContainsPintAeProfilePartiesAndFourDecimalLineNumbers()
    {
        var xml = ErpInvoiceFromOrderWriteService.BuildInvoiceXml(
            "uuid-1",
            "EINV-2026-00001",
            1740000000,
            1740604800,
            "30",
            "AE070331234567890123456",
            Seller(),
            Buyer(),
            [StandardLine()],
            200m,
            10m,
            210m,
            50m,
            160m,
            "S",
            5m);

        Assert.Contains("urn:peppol:pint:billing-1@ae-1", xml);
        Assert.Contains("urn:peppol:bis:billing", xml);
        Assert.Contains("<cbc:ID>EINV-2026-00001</cbc:ID>", xml);
        Assert.Contains("InvoicedQuantity unitCode=\"C62\">2.0000", xml);
        Assert.Contains("PriceAmount currencyID=\"AED\">10.0000", xml);
        Assert.Contains("PrepaidAmount currencyID=\"AED\">50.00", xml);
        Assert.Contains("PayableAmount currencyID=\"AED\">160.00", xml);
        Assert.Contains("Brake pad", xml);
    }
}
