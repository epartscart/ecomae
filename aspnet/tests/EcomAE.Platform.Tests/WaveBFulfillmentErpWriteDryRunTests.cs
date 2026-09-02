using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class WaveBFulfillmentErpWriteDryRunTests
{
    private static CpQuoteRequestsRowDigest Quote(long id, long userId, string status = "draft") =>
        new(id, userId, 0, status, 1_700_000_000, 1_700_000_000, 0, 0);

    private static StorefrontGarageVehicleDigest Car(long id) =>
        new(id, "Car", "Toyota", "Camry", "2020", "VIN", 0);

    private static StorefrontOrderDigest SfOrder(long id) =>
        new(id, 1_700_000_000, 0, 1, 1);

    private static CpShopOrderDigest CpOrder(long id) =>
        new(id, 1, 9, 1, 0, 0, 1, 1, 1, 10m);

    private static ErpPurchaseDigest Purchase(long id, string status = "posted") =>
        new(id, 1, "Supplier", 1_700_000_000, "INV-1", 100m, status, 0, []);

    private static ErpSalesOrderDigest So(long id, string status = "draft") =>
        new(id, $"SO-{id}", 5, 100m, status, 1_700_000_000, []);

    [Fact]
    public void QuoteAddManualRequiresBrandArticle()
    {
        var r = StorefrontQuoteAddManualDryRun.EvaluateAgainstQuotes(
            5, [], new StorefrontQuoteAddManualRequest("", "0986"));
        Assert.Equal("brand_article_required", r.ValidationCode);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void QuoteAddManualValidated()
    {
        var r = StorefrontQuoteAddManualDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5)], new StorefrontQuoteAddManualRequest("Bosch", "0986", 2));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.False(r.WouldCreateDraft);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void GarageCheckCarValidated()
    {
        var r = StorefrontGarageCheckCarDryRun.EvaluateAgainstDigests(
            5, [Car(3)], [SfOrder(11)], new StorefrontGarageCheckCarRequest(3, 11));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void GarageCheckCarRequiresOwnedCar()
    {
        var r = StorefrontGarageCheckCarDryRun.EvaluateAgainstDigests(
            5, [Car(3)], [SfOrder(11)], new StorefrontGarageCheckCarRequest(99, 11));
        Assert.Equal("garage_not_owned", r.ValidationCode);
    }

    [Fact]
    public void FulfillmentSetStageValidated()
    {
        var r = CpOmsFulfillmentSetStageDryRun.EvaluateAgainstOrders(
            [CpOrder(7)],
            new CpOmsFulfillmentSetStageRequest(7, "wh:1", "packing"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void FulfillmentSetStageRejectsUnknown()
    {
        var r = CpOmsFulfillmentSetStageDryRun.EvaluateAgainstOrders(
            [CpOrder(7)],
            new CpOmsFulfillmentSetStageRequest(7, "wh:1", "not-a-stage"));
        Assert.Equal("unknown_stage", r.ValidationCode);
    }

    [Fact]
    public void FulfillmentAdvanceValidated()
    {
        var r = CpOmsFulfillmentAdvanceDryRun.EvaluateAgainstOrders(
            [CpOrder(7)],
            new CpOmsFulfillmentAdvanceRequest(7, "wh:1"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void PurchaseAmendValidated()
    {
        var r = ErpPurchaseAmendDryRun.EvaluateAgainstPurchases(
            [Purchase(4)],
            new ErpPurchaseAmendRequest(4, "INV-2", "wave-b"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
    }

    [Fact]
    public void SalesOrderDeleteDraftOnly()
    {
        var bad = ErpSalesOrderDeleteDryRun.EvaluateAgainstOrders(
            [So(3, "confirmed")], new ErpSalesOrderDeleteRequest(3));
        Assert.Equal("not_draft", bad.ValidationCode);

        var ok = ErpSalesOrderDeleteDryRun.EvaluateAgainstOrders(
            [So(3, "draft")], new ErpSalesOrderDeleteRequest(3));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.False(ok.CutoverAllowed);
    }

    [Fact]
    public void CustomerMasterSaveRequiresId()
    {
        var r = new ErpCustomerMasterSaveDryRun().Evaluate(new ErpCustomerMasterSaveRequest(0));
        Assert.Equal("customer_required", r.ValidationCode);
    }

    [Fact]
    public void CustomerMasterSaveValidated()
    {
        var r = new ErpCustomerMasterSaveDryRun().Evaluate(
            new ErpCustomerMasterSaveRequest(9, "Acme", 5000m, 45));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void AsRmaCreateRequiresLines()
    {
        var r = new ErpAsRmaCreateDryRun().Evaluate(new ErpAsRmaCreateRequest(1, Lines: []));
        Assert.Equal("lines_required", r.ValidationCode);
    }

    [Fact]
    public void AsRmaCreateValidated()
    {
        var r = new ErpAsRmaCreateDryRun().Evaluate(new ErpAsRmaCreateRequest(
            1, 5, Reason: "defective",
            Lines: [new ErpAsRmaCreateLine(10, 1, 12m)]));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(1, r.LineCount);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void ConfirmWritesRefusedAcrossBatch()
    {
        Assert.Equal("dry-run-confirm-refused",
            StorefrontQuoteAddManualDryRun.EvaluateAgainstQuotes(
                5, [], new StorefrontQuoteAddManualRequest("A", "B", 1, true)).Status);
        Assert.Equal("dry-run-confirm-refused",
            CpOmsFulfillmentAdvanceDryRun.EvaluateAgainstOrders(
                [CpOrder(1)], new CpOmsFulfillmentAdvanceRequest(1, "k", true)).Status);
        Assert.Equal("dry-run-confirm-refused",
            new ErpAsRmaCreateDryRun().Evaluate(
                new ErpAsRmaCreateRequest(1, Lines: [new ErpAsRmaCreateLine(1, 1)], ConfirmWrites: true)).Status);
    }
}
