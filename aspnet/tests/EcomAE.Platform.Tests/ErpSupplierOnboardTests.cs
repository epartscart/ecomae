using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSupplierOnboardTests
{
    [Fact]
    public void NextCodeIncrementsVPrefix()
    {
        Assert.Equal("V-0001", ErpSupplierOnboard.NextSupplierCode([]));
        Assert.Equal("V-0003", ErpSupplierOnboard.NextSupplierCode(["V-0001", "V-0002", "LOCAL"]));
    }

    [Fact]
    public void UaeTrnMustBeFifteenDigits()
    {
        Assert.True(ErpSupplierOnboard.IsUaeTrn("100123456789003"));
        Assert.True(ErpSupplierOnboard.IsUaeTrn("100-1234-5678900-3"));
        Assert.False(ErpSupplierOnboard.IsUaeTrn("123"));
        Assert.False(ErpSupplierOnboard.IsUaeTrn(""));
    }

    [Fact]
    public void CompletenessBlocksApproveUntilPackIsWhole()
    {
        var incomplete = ErpSupplierOnboard.Evaluate(new(
            "", "", 0, "", "", "", "", "", "", "", "", "", true, "", [], []));
        Assert.False(ErpSupplierOnboard.CanApprove(incomplete));
        Assert.Contains("Supplier name", ErpSupplierOnboard.MissingSummary(incomplete), StringComparison.Ordinal);

        var ready = ErpSupplierOnboard.Evaluate(new(
            "Gold House LLC",
            "V-0004",
            21,
            "AED",
            "",
            "Sheikh Zayed Rd",
            "Dubai",
            "Fatima Ali",
            "+971501234567",
            "Local",
            "Jewellery",
            "AE",
            true,
            "100123456789003",
            [new("Fatima Ali", 60m, "AE", "784-1990", "owner"), new("Omar Ali", 40m, "AE", "784-1991", "partner")],
            [ErpSupplierOnboard.DocTradeLicense, ErpSupplierOnboard.DocPartnerId]));
        Assert.True(ErpSupplierOnboard.CanApprove(ready));
    }

    [Fact]
    public void SharesMustTotalOneHundredAndExtraPartnersNeedDocuments()
    {
        var owners = new[] { new ErpSupplierOnboardOwner("A", 70m), new ErpSupplierOnboardOwner("B", 20m) };
        var checks = ErpSupplierOnboard.Evaluate(Base() with { Owners = owners, Documents = [ErpSupplierOnboard.DocTradeLicense] });
        Assert.Contains(checks, c => c.Key == "shares" && !c.Ok);
        Assert.Contains(checks, c => c.Key == "partner_docs" && !c.Ok);

        var ok = ErpSupplierOnboard.Evaluate(Base() with
        {
            Owners = [new("A", 60m), new("B", 40m)],
            Documents = [ErpSupplierOnboard.DocTradeLicense, ErpSupplierOnboard.DocPartnerId],
        });
        Assert.True(ErpSupplierOnboard.CanApprove(ok));
    }

    [Fact]
    public void VatNotRegisteredDoesNotRequireTrn()
    {
        var checks = ErpSupplierOnboard.Evaluate(Base() with { VatRegistered = false, Trn = "" });
        Assert.Contains(checks, c => c.Key == "trn" && c.Ok);
    }

    private static ErpSupplierOnboardState Base() => new(
        "Gold House LLC",
        "V-0004",
        21,
        "AED",
        "Warehouse 4",
        "",
        "Dubai",
        "Fatima Ali",
        "+971501234567",
        "Local",
        "Jewellery",
        "AE",
        true,
        "100123456789003",
        [new("Fatima Ali", 100m)],
        [ErpSupplierOnboard.DocTradeLicense]);
}
