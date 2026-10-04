using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSalesInvoiceWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Not configured.");
    }

    private static ErpSalesInvoiceWriteService Service(IErpWriteConnectionFactory connections)
    {
        var vouchers = new ErpVoucherNumberService();
        return new ErpSalesInvoiceWriteService(
            connections,
            vouchers,
            new ErpTaxAmountCalculator(),
            new ErpCashWriteService(
                connections,
                vouchers,
                new ErpGlPostingService(vouchers),
                new ErpAuditLogWriter(),
                new ErpSettlementAllocationService(),
                new ErpAdvanceVatService(new ErpGlPostingService(vouchers))),
            new ErpGlPostingService(vouchers),
            new ErpAuditLogWriter());
    }

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
        ["buyer_city"] = "Sharjah",
        ["buyer_emirate"] = "Sharjah",
        ["buyer_country_code"] = countryCode,
        ["buyer_peppol_endpoint"] = "0235:9900000098",
    };

    private static ErpSalesInvoiceLine[] Lines() =>
    [
        new ErpSalesInvoiceLine(1, "Widget", 2m, 50m, 100m, "S", 5m, 5m, 105m),
    ];

    [Fact]
    public async Task MissingSalesOrderIdIsRejectedBeforeAnyConnection()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(
            () => Service(new UnusedConnections()).ConvertSalesOrderAsync(0, adminId: 1));
        Assert.Equal("Sales order not found", ex.Message);
    }

    [Fact]
    public async Task UnconfiguredTenantDatabaseIsRefused()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(
            () => Service(new UnconfiguredConnections()).ConvertSalesOrderAsync(7, adminId: 1));
        Assert.Equal("No database", ex.Message);
    }

    [Fact]
    public void CompleteTaxInvoicePassesValidation()
        => Assert.Empty(ErpSalesInvoiceWriteService.ValidateTaxInvoice("SI-2026-00001", Seller(), Buyer(), Lines(), 5m));

    [Fact]
    public void MissingInvoiceNumberAndSellerFieldsAreReported()
    {
        var seller = Seller();
        seller["seller_city"] = string.Empty;
        var errors = ErpSalesInvoiceWriteService.ValidateTaxInvoice(" ", seller, Buyer(), Lines(), 5m);
        Assert.Contains("Invoice number is required", errors);
        Assert.Contains("Seller city is required", errors);
    }

    [Fact]
    public void AeTenantMustHaveFifteenDigitTrn()
    {
        var errors = ErpSalesInvoiceWriteService.ValidateTaxInvoice("SI-1", Seller(trn: "12345"), Buyer(), Lines(), 5m);
        Assert.Contains("Seller TRN must be exactly 15 digits (UAE FTA)", errors);
    }

    [Fact]
    public void NonAeTenantOnlyNeedsATaxRegistrationNumber()
    {
        var errors = ErpSalesInvoiceWriteService.ValidateTaxInvoice(
            "SI-1",
            Seller(countryCode: "SA", trn: "3001234567"),
            Buyer(countryCode: "SA", trn: "3009876543"),
            Lines(),
            15m);
        Assert.Empty(errors);
    }

    [Fact]
    public void SellerTaxRegistrationIsAlwaysRequired()
    {
        var errors = ErpSalesInvoiceWriteService.ValidateTaxInvoice("SI-1", Seller(countryCode: "IN", trn: ""), Buyer(countryCode: "IN"), Lines(), 0m);
        Assert.Contains("Seller tax identifier (TRN) is required", errors);
    }

    [Fact]
    public void LinesMustHaveNameQuantityAndTaxCategory()
    {
        var errors = ErpSalesInvoiceWriteService.ValidateTaxInvoice(
            "SI-1",
            Seller(),
            Buyer(),
            [new ErpSalesInvoiceLine(1, "  ", 0m, 0m, 0m, string.Empty, 0m, 0m, 0m)],
            0m);
        Assert.Contains("Line 1: item_name is required", errors);
        Assert.Contains("Line 1: quantity is required", errors);
        Assert.Contains("Line 1: tax_category is required", errors);
    }

    [Fact]
    public void EmptyLineSetIsRejected()
        => Assert.Contains(
            "At least one invoice line is required",
            ErpSalesInvoiceWriteService.ValidateTaxInvoice("SI-1", Seller(), Buyer(), [], 0m));

    [Fact]
    public void ConversionSettlesTheComputedInvoiceTotal()
    {
        var root = FindRepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpSalesInvoiceWriteService.cs"));

        Assert.Contains(
            "Amount = totalIncl",
            service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConversionRefusesWhenANonCancelledDocumentAlreadyReferencesTheSalesOrder()
    {
        var root = FindRepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpSalesInvoiceWriteService.cs"));

        Assert.Contains(
            "WHERE `sales_order_id` = ? AND `status` <> 'cancelled'",
            service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void InvoiceNumberIsAllocatedInsideTheInsertTransactionAfterValidation()
    {
        var root = FindRepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpSalesInvoiceWriteService.cs"));

        Assert.DoesNotContain("_vouchers.NextAsync(connection, null, \"SI\"", service, StringComparison.Ordinal);
        var validate = service.IndexOf("ValidateTaxInvoiceContent(seller, buyer, lines, totalVat)", StringComparison.Ordinal);
        var allocate = service.IndexOf("_vouchers.NextAsync(connection, transaction, \"SI\"", StringComparison.Ordinal);
        Assert.True(validate > 0 && allocate > validate);
    }

    [Fact]
    public void ContentValidationDoesNotRequireAnInvoiceNumber()
    {
        var errors = ErpSalesInvoiceWriteService.ValidateTaxInvoiceContent(Seller(), Buyer(), Lines(), 5m);
        Assert.Empty(errors);
        Assert.Contains("Invoice number is required", ErpSalesInvoiceWriteService.ValidateTaxInvoice(" ", Seller(), Buyer(), Lines(), 5m));
    }

    [Fact]
    public void ConversionPostsSalesRecognitionInsteadOfArSettlementJournal()
    {
        var root = FindRepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpSalesInvoiceWriteService.cs"));

        Assert.Contains("PostGl = false", service, StringComparison.Ordinal);
        Assert.Contains("_gl.PostSalesInvoiceAsync(", service, StringComparison.Ordinal);
        Assert.DoesNotContain("glJournalId = 0;", service, StringComparison.Ordinal);

        var gl = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpGlPostingService.cs"));
        Assert.Contains("'adjustment','sales_invoice')", gl, StringComparison.Ordinal);
        Assert.Contains("MODIFY `source_type` enum(", gl, StringComparison.Ordinal);
    }

    [Fact]
    public void SalesInvoiceJournalIsDrArCrRevenueCrVatOutput()
    {
        var posting = new ErpGlSalesInvoicePosting(7, "SI-2026-00007", 200m, 10m, 210m, 1_700_000_000);
        var lines = ErpGlPostingService.SalesInvoiceLines(11, 44, 21, posting);

        Assert.Collection(
            lines,
            ar => Assert.Equal((11L, 210m, 0m), (ar.CoaId, ar.Debit, ar.Credit)),
            rev => Assert.Equal((44L, 0m, 200m), (rev.CoaId, rev.Debit, rev.Credit)),
            vat => Assert.Equal((21L, 0m, 10m), (vat.CoaId, vat.Debit, vat.Credit)));
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));

        var noVatAccount = ErpGlPostingService.SalesInvoiceLines(11, 44, 0, posting);
        Assert.Equal(2, noVatAccount.Count);
        Assert.Equal(210m, noVatAccount[1].Credit);
    }

    [Theory]
    [InlineData("AE", "vat-decree-8-2017")]
    [InlineData("ae", "vat-decree-8-2017")]
    [InlineData("SA", "")]
    [InlineData("", "")]
    public void LegislationRefFollowsTenantCountry(string country, string expected)
        => Assert.Equal(expected, ErpSalesInvoiceWriteService.SalesLegislationRef(Seller(country)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
