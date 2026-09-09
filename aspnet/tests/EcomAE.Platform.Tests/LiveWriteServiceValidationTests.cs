using System.Data.Common;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LiveWriteServiceValidationTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }

    private sealed class ConfiguredNeverOpened : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task Cart_rejects_guest_and_invalid_qty_without_db()
    {
        var guest = await new StorefrontCartWriteService(new UnconfiguredConnections())
            .ChangeCountNeedAsync(0, 10, 2);
        Assert.False(guest.Ok);
        Assert.Equal("auth", guest.Code);

        var guestSession = await new StorefrontCartWriteService(new UnconfiguredConnections())
            .ChangeCountNeedAsync(0, 10, 2, sessionId: 9);
        Assert.False(guestSession.Ok);
        Assert.Equal("db", guestSession.Code);

        var missingDb = await new StorefrontCartWriteService(new UnconfiguredConnections())
            .ChangeCountNeedAsync(1, 10, 2);
        Assert.False(missingDb.Ok);
        Assert.Equal("db", missingDb.Code);

        var invalid = await new StorefrontCartWriteService(new ConfiguredNeverOpened())
            .ChangeCountNeedAsync(1, 0, 0);
        Assert.False(invalid.Ok);
        Assert.Equal("invalid", invalid.Code);

        var emptyDelete = await new StorefrontCartWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(1, []);
        Assert.False(emptyDelete.Ok);
        Assert.Equal("invalid", emptyDelete.Code);

        var payAuth = await new StorefrontPaymentWriteService(new UnconfiguredConnections())
            .CreateOperationAsync(0, 10, 0, "epc_demo");
        Assert.False(payAuth.Ok);
        Assert.Equal("auth", payAuth.Code);

        var payInvalid = await new StorefrontPaymentWriteService(new ConfiguredNeverOpened())
            .CreateOperationAsync(1, 0, 0, "epc_demo");
        Assert.False(payInvalid.Ok);
        Assert.Equal("invalid", payInvalid.Code);

        var payDb = await new StorefrontPaymentWriteService(new UnconfiguredConnections())
            .CreateOperationAsync(1, 10, 0, "epc_demo");
        Assert.False(payDb.Ok);
        Assert.Equal("db", payDb.Code);

        var payOnPlaceAuth = await new StorefrontPaymentWriteService(new UnconfiguredConnections())
            .PayOnPlaceAsync(0, 9);
        Assert.False(payOnPlaceAuth.Ok);
        Assert.Equal("auth", payOnPlaceAuth.Code);

        var payOnPlaceInvalid = await new StorefrontPaymentWriteService(new ConfiguredNeverOpened())
            .PayOnPlaceAsync(1, 0);
        Assert.False(payOnPlaceInvalid.Ok);
        Assert.Equal("invalid", payOnPlaceInvalid.Code);

        var payOnPlaceDb = await new StorefrontPaymentWriteService(new UnconfiguredConnections())
            .PayOnPlaceAsync(1, 9);
        Assert.False(payOnPlaceDb.Ok);
        Assert.Equal("db", payOnPlaceDb.Code);

        var notifyForbidden = await new StorefrontPaymentWriteService(new ConfiguredNeverOpened())
            .NotifyAsync(1, 9, 10, "bad-token", "epc_demo");
        Assert.False(notifyForbidden.Ok);
        Assert.Equal("forbidden", notifyForbidden.Code);

        var vinReqAuth = await new StorefrontVinRequestWriteService(new UnconfiguredConnections())
            .CreateAsync(0, new Dictionary<string, string> { ["client_vin"] = "WVWZZZ1JZXW000001" }, "pads");
        Assert.False(vinReqAuth.Ok);
        Assert.Equal("auth", vinReqAuth.Code);

        var vinReqInvalid = await new StorefrontVinRequestWriteService(new ConfiguredNeverOpened())
            .CreateAsync(1, new Dictionary<string, string>(), "");
        Assert.False(vinReqInvalid.Ok);
        Assert.Equal("invalid", vinReqInvalid.Code);

        var vinMsgAuth = await new StorefrontVinRequestWriteService(new UnconfiguredConnections())
            .SendMessageAsync(0, 9, "hello");
        Assert.False(vinMsgAuth.Ok);
        Assert.Equal("auth", vinMsgAuth.Code);
    }

    [Fact]
    public async Task Payroll_rejects_invalid_run_before_open()
    {
        var missingDb = await new ErpPayrollWriteService(new UnconfiguredConnections())
            .ApproveRunAsync(9);
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);

        var invalid = await new ErpPayrollWriteService(new ConfiguredNeverOpened())
            .ApproveRunAsync(0);
        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid", invalid.Code);

        var payDb = await new ErpPayrollPayWriteService(new UnconfiguredConnections())
            .PayRunAsync(1);
        Assert.False(payDb.Succeeded);
        Assert.Equal("db", payDb.Code);

        var payInvalid = await new ErpPayrollPayWriteService(new ConfiguredNeverOpened())
            .PayRunAsync(0);
        Assert.False(payInvalid.Succeeded);
        Assert.Equal("invalid", payInvalid.Code);
        Assert.Equal("Payroll run not found", payInvalid.Message);

        var daysDb = await new ErpPayrollUpdateDaysWriteService(new UnconfiguredConnections())
            .UpdateLineDaysAsync(1, 15);
        Assert.False(daysDb.Succeeded);
        Assert.Equal("db", daysDb.Code);

        var daysInvalid = await new ErpPayrollUpdateDaysWriteService(new ConfiguredNeverOpened())
            .UpdateLineDaysAsync(0, 15);
        Assert.False(daysInvalid.Succeeded);
        Assert.Equal("invalid", daysInvalid.Code);
        Assert.Equal("Cannot edit paid payroll line", daysInvalid.Message);

        var genInvalid = await new ErpPayrollGenerateWriteService(new ConfiguredNeverOpened())
            .GenerateAsync("not-a-period");
        Assert.False(genInvalid.Succeeded);
        Assert.Equal("invalid", genInvalid.Code);
        Assert.Equal("Invalid period (use YYYY-MM)", genInvalid.Message);

        var genDb = await new ErpPayrollGenerateWriteService(new UnconfiguredConnections())
            .GenerateAsync("2026-09");
        Assert.False(genDb.Succeeded);
        Assert.Equal("db", genDb.Code);
    }

    [Fact]
    public async Task Oms_credit_po_and_forecast_reject_invalid_input()
    {
        var oms = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .SetItemStatusAsync(0, 0, 0, 1);
        Assert.False(oms.Succeeded);
        Assert.Equal("invalid", oms.Code);

        var credit = await new CpCreditLimitWriteService(new ConfiguredNeverOpened())
            .SetLimitAsync("", 0, -1, "AED", 1);
        Assert.False(credit.Succeeded);
        Assert.Equal("invalid", credit.Code);

        var po = await new CpPoApprovalWriteService(new ConfiguredNeverOpened())
            .ApproveAsync(0, 0, 1, "");
        Assert.False(po.Succeeded);
        Assert.Equal("invalid", po.Code);

        var forecast = await new ErpInventoryForecastWriteService(new ConfiguredNeverOpened())
            .RecomputeSkuAsync("", "", 0, "", 7);
        Assert.False(forecast.Succeeded);
        Assert.Equal("invalid", forecast.Code);

        var message = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .SendMessageAsync(0, "", 0, 1);
        Assert.False(message.Succeeded);
        Assert.Equal("invalid", message.Code);

        var courier = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .SetCourierAsync(0, -1, null, 1);
        Assert.False(courier.Succeeded);
        Assert.Equal("invalid", courier.Code);

        var delete = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .DeleteUnpaidOrdersAsync([]);
        Assert.False(delete.Succeeded);
        Assert.Equal("invalid", delete.Code);
    }

    [Fact]
    public async Task Checkout_quote_and_garage_reject_before_open()
    {
        var guest = await new StorefrontCheckoutWriteService(new UnconfiguredConnections())
            .CreateAsync(0, new StorefrontCheckoutWriteRequest(1, 1, true));
        Assert.False(guest.Ok);
        Assert.Equal("auth", guest.Code);

        var guestPhone = await new StorefrontCheckoutWriteService(new UnconfiguredConnections())
            .CreateAsync(0, new StorefrontCheckoutWriteRequest(1, 1, true, SessionId: 9));
        Assert.False(guestPhone.Ok);
        Assert.Equal("phone_required", guestPhone.Code);

        var guestEmail = await new StorefrontCheckoutWriteService(new ConfiguredNeverOpened())
            .CreateAsync(0, new StorefrontCheckoutWriteRequest(1, 1, true, SessionId: 9, PhoneNotAuth: "+971501234567", EmailNotAuth: "not-an-email"));
        Assert.False(guestEmail.Ok);
        Assert.Equal("email_invalid", guestEmail.Code);

        var guestDb = await new StorefrontCheckoutWriteService(new UnconfiguredConnections())
            .CreateAsync(0, new StorefrontCheckoutWriteRequest(1, 1, true, SessionId: 9, PhoneNotAuth: "+971501234567"));
        Assert.False(guestDb.Ok);
        Assert.Equal("db", guestDb.Code);

        var missingDb = await new StorefrontCheckoutWriteService(new UnconfiguredConnections())
            .CreateAsync(1, new StorefrontCheckoutWriteRequest(1, 1, true));
        Assert.False(missingDb.Ok);
        Assert.Equal("db", missingDb.Code);

        var agreement = await new StorefrontCheckoutWriteService(new ConfiguredNeverOpened())
            .CreateAsync(1, new StorefrontCheckoutWriteRequest(1, 1, false));
        Assert.False(agreement.Ok);
        Assert.Equal("agreement", agreement.Code);

        var howGet = await new StorefrontCheckoutWriteService(new ConfiguredNeverOpened())
            .CreateAsync(1, new StorefrontCheckoutWriteRequest(0, 1, true));
        Assert.False(howGet.Ok);
        Assert.Equal("how_get_missing", howGet.Code);

        var pickup = await new StorefrontCheckoutWriteService(new ConfiguredNeverOpened())
            .CreateAsync(1, new StorefrontCheckoutWriteRequest(1, 0, true));
        Assert.False(pickup.Ok);
        Assert.Equal("office_required", pickup.Code);

        var quoteGuest = await new StorefrontQuoteWriteService(new UnconfiguredConnections())
            .SubmitAsync(0, 9, null);
        Assert.False(quoteGuest.Succeeded);
        Assert.Equal("auth", quoteGuest.Code);

        var quoteInvalid = await new StorefrontQuoteWriteService(new ConfiguredNeverOpened())
            .SubmitAsync(1, 0, null);
        Assert.False(quoteInvalid.Succeeded);
        Assert.Equal("invalid", quoteInvalid.Code);

        var garageInvalid = await new StorefrontGarageWriteService(new ConfiguredNeverOpened())
            .SetActiveAsync(1, 0);
        Assert.False(garageInvalid.Succeeded);
        Assert.Equal("invalid", garageInvalid.Code);

        var garageDelete = await new StorefrontGarageWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(1, 0);
        Assert.False(garageDelete.Succeeded);
        Assert.Equal("invalid", garageDelete.Code);
    }

    [Fact]
    public async Task Quote_accept_add_and_customer_writes_reject_before_open()
    {
        var acceptInvalid = await new StorefrontQuoteWriteService(new ConfiguredNeverOpened())
            .AcceptAsync(1, 0);
        Assert.False(acceptInvalid.Succeeded);
        Assert.Equal("invalid", acceptInvalid.Code);

        var addType = await new StorefrontQuoteWriteService(new ConfiguredNeverOpened())
            .AddItemAsync(1, new StorefrontQuoteAddItemWriteRequest(1, "Bosch", "F026400050"));
        Assert.False(addType.Succeeded);
        Assert.Equal("product_type_unsupported", addType.Code);

        var addMissing = await new StorefrontQuoteWriteService(new ConfiguredNeverOpened())
            .AddItemAsync(1, new StorefrontQuoteAddItemWriteRequest(2, "", ""));
        Assert.False(addMissing.Succeeded);
        Assert.Equal("invalid", addMissing.Code);

        var manual = await new StorefrontQuoteWriteService(new ConfiguredNeverOpened())
            .AddManualAsync(1, "", "ABC");
        Assert.False(manual.Succeeded);
        Assert.Equal("invalid", manual.Code);

        var notepad = await new StorefrontGarageWriteService(new ConfiguredNeverOpened())
            .AddNotepadAsync(1, 0, "Bosch", "", "Filter", 1, 10);
        Assert.False(notepad.Succeeded);
        Assert.Equal("invalid", notepad.Code);

        var review = await new StorefrontCustomerWriteService(new ConfiguredNeverOpened())
            .AddEvaluationAsync(1, 0, 5, "ok");
        Assert.False(review.Succeeded);
        Assert.Equal("invalid", review.Code);

        var message = await new StorefrontCustomerWriteService(new ConfiguredNeverOpened())
            .SendOrderMessageAsync(1, 9, "");
        Assert.False(message.Succeeded);
        Assert.Equal("invalid", message.Code);

        var vehicle = await new StorefrontGarageWriteService(new ConfiguredNeverOpened())
            .SaveVehicleAsync(1, new StorefrontGarageSaveRequest());
        Assert.False(vehicle.Succeeded);
        Assert.Equal("invalid", vehicle.Code);

        var newsletter = await new StorefrontCustomerWriteService(new ConfiguredNeverOpened())
            .SubscribeNewsletterAsync("not-an-email", null);
        Assert.False(newsletter.Succeeded);
        Assert.Equal("invalid", newsletter.Code);

        var option = await new StorefrontCustomerWriteService(new ConfiguredNeverOpened())
            .SetUserOptionAsync(1, "forbidden", "x");
        Assert.False(option.Succeeded);
        Assert.Equal("invalid", option.Code);

        var profileEmpty = await new StorefrontCustomerWriteService(new ConfiguredNeverOpened())
            .SaveProfileAsync(1, new Dictionary<string, string> { ["password"] = "secret" });
        Assert.False(profileEmpty.Succeeded);
        Assert.Equal("invalid", profileEmpty.Code);

        var profileGuest = await new StorefrontCustomerWriteService(new UnconfiguredConnections())
            .SaveProfileAsync(0, new Dictionary<string, string> { ["name"] = "Ada" });
        Assert.False(profileGuest.Succeeded);
        Assert.Equal("auth", profileGuest.Code);

        var profileDb = await new StorefrontCustomerWriteService(new UnconfiguredConnections())
            .SaveProfileAsync(1, new Dictionary<string, string> { ["name"] = "Ada" });
        Assert.False(profileDb.Succeeded);
        Assert.Equal("db", profileDb.Code);

        var bulkOms = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .SetItemsStatusAsync(0, 0, [], 1);
        Assert.False(bulkOms.Succeeded);
        Assert.Equal("invalid", bulkOms.Code);

        var bulkOmsDb = await new CpOmsWriteService(new UnconfiguredConnections())
            .SetItemsStatusAsync(9, 2, [11, 12], 1);
        Assert.False(bulkOmsDb.Succeeded);
        Assert.Equal("db", bulkOmsDb.Code);

        var comment = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .AddCommentAsync(0, "", 1);
        Assert.False(comment.Succeeded);
        Assert.Equal("invalid", comment.Code);

        var viewed = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .SetViewedAsync([], 2);
        Assert.False(viewed.Succeeded);
        Assert.Equal("invalid", viewed.Code);

        var stage = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .SetFulfillmentStageAsync(9, "s1", "not-a-stage", null, 1);
        Assert.False(stage.Succeeded);
        Assert.Equal("invalid", stage.Code);

        var advance = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .AdvanceFulfillmentAsync(0, "", 1);
        Assert.False(advance.Succeeded);
        Assert.Equal("invalid", advance.Code);

        var retMsg = await new StorefrontCustomerWriteService(new ConfiguredNeverOpened())
            .SendReturnMessageAsync(1, 0, "");
        Assert.False(retMsg.Succeeded);
        Assert.Equal("invalid", retMsg.Code);

        var updateItem = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .UpdateItemAsync(0, new CpOmsItemWritePatch(0, 0m, 0), 1);
        Assert.False(updateItem.Succeeded);
        Assert.Equal("invalid", updateItem.Code);

        var updatePrice = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .UpdateItemAsync(9, new CpOmsItemWritePatch(3, 0m, 2, Manufacturer: "Bosch", Article: "0986"), 1);
        Assert.False(updatePrice.Succeeded);
        Assert.Equal("invalid", updatePrice.Code);

        var updateReprice = await new CpOmsWriteService(new UnconfiguredConnections())
            .UpdateItemAsync(9, new CpOmsItemWritePatch(3, 12m, 2, Manufacturer: "Bosch", Article: "0986", RepriceFromWarehouse: true), 1);
        Assert.False(updateReprice.Succeeded);
        Assert.Equal("db", updateReprice.Code);

        var updateItems = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .UpdateItemsAsync(0, [], 1);
        Assert.False(updateItems.Succeeded);
        Assert.Equal("invalid", updateItems.Code);

        var updateItemsDb = await new CpOmsWriteService(new UnconfiguredConnections())
            .UpdateItemsAsync(9, [new CpOmsItemWritePatch(3, 12m, 2, Manufacturer: "Bosch", Article: "0986")], 1);
        Assert.False(updateItemsDb.Succeeded);
        Assert.Equal("db", updateItemsDb.Code);

        var fqTransition = await new CpFulfillmentQueueWriteService(new ConfiguredNeverOpened())
            .TransitionAsync(0, "picking");
        Assert.False(fqTransition.Succeeded);
        Assert.Equal("invalid", fqTransition.Code);

        var fqStatus = await new CpFulfillmentQueueWriteService(new ConfiguredNeverOpened())
            .TransitionAsync(9, "");
        Assert.False(fqStatus.Succeeded);
        Assert.Equal("invalid", fqStatus.Code);

        var fqAssign = await new CpFulfillmentQueueWriteService(new ConfiguredNeverOpened())
            .AssignAsync(0, 1, "Pat");
        Assert.False(fqAssign.Succeeded);
        Assert.Equal("invalid", fqAssign.Code);

        var fqPick = await new CpFulfillmentQueueWriteService(new ConfiguredNeverOpened())
            .PickItemAsync(0, 1, "picked");
        Assert.False(fqPick.Succeeded);
        Assert.Equal("invalid", fqPick.Code);

        var fqPickQty = await new CpFulfillmentQueueWriteService(new ConfiguredNeverOpened())
            .PickItemAsync(3, -1, "picked");
        Assert.False(fqPickQty.Succeeded);
        Assert.Equal("invalid", fqPickQty.Code);

        var fqPackDb = await new CpFulfillmentQueueWriteService(new UnconfiguredConnections())
            .PackItemAsync(3, 1);
        Assert.False(fqPackDb.Succeeded);
        Assert.Equal("db", fqPackDb.Code);

        var fqWave = await new CpFulfillmentQueueWriteService(new ConfiguredNeverOpened())
            .CreateWaveAsync("epartscart", []);
        Assert.False(fqWave.Succeeded);
        Assert.Equal("invalid", fqWave.Code);

        Assert.Equal(new[] { "picking", "cancelled" }, CpFulfillmentQueueWriteService.AllowedNextStatuses("queued"));
        Assert.Equal(new[] { "delivered" }, CpFulfillmentQueueWriteService.AllowedNextStatuses("SHIPPED"));
        Assert.Empty(CpFulfillmentQueueWriteService.AllowedNextStatuses("delivered"));

        var posOpenNeg = await new CpPosWriteService(new ConfiguredNeverOpened())
            .OpenSessionAsync(-1, 1, "Register 1");
        Assert.False(posOpenNeg.Succeeded);
        Assert.Equal("invalid", posOpenNeg.Code);

        var posOpenDb = await new CpPosWriteService(new UnconfiguredConnections())
            .OpenSessionAsync(10, 1, "Register 1");
        Assert.False(posOpenDb.Succeeded);
        Assert.Equal("db", posOpenDb.Code);

        var posCloseNeg = await new CpPosWriteService(new ConfiguredNeverOpened())
            .CloseSessionAsync(1, -1, "");
        Assert.False(posCloseNeg.Succeeded);
        Assert.Equal("invalid", posCloseNeg.Code);

        var posCloseDb = await new CpPosWriteService(new UnconfiguredConnections())
            .CloseSessionAsync(1, 10, "");
        Assert.False(posCloseDb.Succeeded);
        Assert.Equal("db", posCloseDb.Code);

        var posSaveDb = await new CpPosWriteService(new UnconfiguredConnections())
            .SaveSettingsAsync(true, "Register 1", 0, 0, 0, "", "");
        Assert.False(posSaveDb.Succeeded);
        Assert.Equal("db", posSaveDb.Code);

        var posEmpty = await new CpPosWriteService(new ConfiguredNeverOpened())
            .CompleteSaleAsync(new CpPosCompleteSaleWriteRequest(Lines: []), 1);
        Assert.False(posEmpty.Succeeded);
        Assert.Equal("invalid", posEmpty.Code);
        Assert.Equal("Cart is empty", posEmpty.Message);

        var posSaleDb = await new CpPosWriteService(new UnconfiguredConnections())
            .CompleteSaleAsync(new CpPosCompleteSaleWriteRequest(Lines: [new("Oil", 1, 8.50m)]), 1);
        Assert.False(posSaleDb.Succeeded);
        Assert.Equal("db", posSaleDb.Code);

        var parsed = CpPosWriteService.ParseLinesJson("""[{"name":"Oil filter","qty":2,"unit_price_ex":8.5,"line_discount_pct":10}]""");
        var lines = CpPosWriteService.ParseLines(parsed);
        Assert.Single(lines);
        Assert.Equal("Oil filter", lines[0].Name);
        Assert.Equal(2m, lines[0].Qty);
        Assert.Equal(8.5m, lines[0].UnitPriceEx);
        Assert.Equal(1.70m, lines[0].DiscountAmt);
        Assert.Equal(15.30m, lines[0].LineExVat);
        var priced = CpPosWriteService.ParseLines(CpPosWriteService.ParseLinesJson("""[{"name":"Pad","price":4}]"""));
        Assert.Equal(4m, priced[0].UnitPriceEx);

        var dunId = await new CpCollectionsDunningWriteService(new ConfiguredNeverOpened())
            .UpdateStatusAsync(0, "open", "", 1);
        Assert.False(dunId.Succeeded);
        Assert.Equal("invalid", dunId.Code);

        var dunStatus = await new CpCollectionsDunningWriteService(new ConfiguredNeverOpened())
            .UpdateStatusAsync(9, "nope", "", 1);
        Assert.False(dunStatus.Succeeded);
        Assert.Equal("invalid", dunStatus.Code);

        var dunPay = await new CpCollectionsDunningWriteService(new ConfiguredNeverOpened())
            .RecordPaymentAsync(9, 0, 1);
        Assert.False(dunPay.Succeeded);
        Assert.Equal("invalid", dunPay.Code);

        var dunDb = await new CpCollectionsDunningWriteService(new UnconfiguredConnections())
            .RecordPaymentAsync(9, 10, 1);
        Assert.False(dunDb.Succeeded);
        Assert.Equal("db", dunDb.Code);

        var createReturn = await new StorefrontCustomerWriteService(new ConfiguredNeverOpened())
            .CreateReturnAsync(1, 0, 0, 0, 0, null);
        Assert.False(createReturn.Succeeded);
        Assert.Equal("invalid", createReturn.Code);

        var createReturnGuest = await new StorefrontCustomerWriteService(new UnconfiguredConnections())
            .CreateReturnAsync(0, 9, 3, 1, 1, null);
        Assert.False(createReturnGuest.Succeeded);
        Assert.Equal("auth", createReturnGuest.Code);

        var createReturnDb = await new StorefrontCustomerWriteService(new UnconfiguredConnections())
            .CreateReturnAsync(1, 9, 3, 1, 1, "broken");
        Assert.False(createReturnDb.Succeeded);
        Assert.Equal("db", createReturnDb.Code);

        var staffComment = await new CpUserWriteService(new ConfiguredNeverOpened())
            .SetCommentAsync(0, "note");
        Assert.False(staffComment.Succeeded);
        Assert.Equal("invalid", staffComment.Code);

        var staffCommentDb = await new CpUserWriteService(new UnconfiguredConnections())
            .SetCommentAsync(4, "note");
        Assert.False(staffCommentDb.Succeeded);
        Assert.Equal("db", staffCommentDb.Code);

        var vinViewed = await new CpUserWriteService(new ConfiguredNeverOpened())
            .SetVinViewedAsync([], 2);
        Assert.False(vinViewed.Succeeded);
        Assert.Equal("invalid", vinViewed.Code);

        var vinViewedDb = await new CpUserWriteService(new UnconfiguredConnections())
            .SetVinViewedAsync([9], 1);
        Assert.False(vinViewedDb.Succeeded);
        Assert.Equal("db", vinViewedDb.Code);

        var unlockInvalid = await new CpUserWriteService(new ConfiguredNeverOpened())
            .SetUnlockedAsync(0, 1, 1);
        Assert.False(unlockInvalid.Succeeded);
        Assert.Equal("invalid", unlockInvalid.Code);

        var unlockSelf = await new CpUserWriteService(new ConfiguredNeverOpened())
            .SetUnlockedAsync(4, 0, 4);
        Assert.False(unlockSelf.Succeeded);
        Assert.Equal("self", unlockSelf.Code);

        var unlockDb = await new CpUserWriteService(new UnconfiguredConnections())
            .SetUnlockedAsync(9, 1, 1);
        Assert.False(unlockDb.Succeeded);
        Assert.Equal("db", unlockDb.Code);

        var createNoContact = await new CpUserWriteService(new ConfiguredNeverOpened())
            .CreateAsync(null, 0, null, 0, "secret1", 1, 1, null, null);
        Assert.False(createNoContact.Succeeded);
        Assert.Equal("invalid", createNoContact.Code);

        var createNoPassword = await new CpUserWriteService(new ConfiguredNeverOpened())
            .CreateAsync("staff@local.test", 1, null, 0, "", 1, 1, null, null);
        Assert.False(createNoPassword.Succeeded);
        Assert.Equal("invalid", createNoPassword.Code);

        var createBadFields = await new CpUserWriteService(new ConfiguredNeverOpened())
            .CreateAsync("staff@local.test", 1, null, 0, "secret1", 1, 1, "{", null);
        Assert.False(createBadFields.Succeeded);
        Assert.Equal("invalid", createBadFields.Code);

        var createDb = await new CpUserWriteService(new UnconfiguredConnections())
            .CreateAsync("staff@local.test", 1, null, 0, "secret1", 1, 1, null, "1");
        Assert.False(createDb.Succeeded);
        Assert.Equal("db", createDb.Code);

        var setPwInvalid = await new CpUserWriteService(new ConfiguredNeverOpened())
            .SetPasswordAsync(0, "secret1", null);
        Assert.False(setPwInvalid.Succeeded);
        Assert.Equal("invalid", setPwInvalid.Code);

        var setPwEmpty = await new CpUserWriteService(new ConfiguredNeverOpened())
            .SetPasswordAsync(9, "", null);
        Assert.False(setPwEmpty.Succeeded);
        Assert.Equal("invalid", setPwEmpty.Code);

        var setPwDb = await new CpUserWriteService(new UnconfiguredConnections())
            .SetPasswordAsync(9, "secret1", "keep-me");
        Assert.False(setPwDb.Succeeded);
        Assert.Equal("db", setPwDb.Code);

        var fields = CpUserWriteService.ParseProfileFields("""[{"name":"firstname","value":"Ada"}]""");
        Assert.Null(fields.Error);
        Assert.Equal("firstname", fields.Fields[0].Name);
        Assert.Equal("Ada", fields.Fields[0].Value);
        Assert.Equal(new[] { 1, 3 }, CpUserWriteService.ParseGroups("1,3").GroupIds);
        Assert.Equal(new[] { 2, 4 }, CpUserWriteService.ParseGroups("[2,4]").GroupIds);
        Assert.True(CpUserWriteService.ContactMatchesRegexp("ada@local.test", @"^[^@]+@[^@]+\.[^@]+$"));
        Assert.False(CpUserWriteService.ContactMatchesRegexp("not-email", @"^[^@]+@[^@]+\.[^@]+$"));
        var hash = CpUserWriteService.HashStaffPassword("secret1");
        Assert.StartsWith("$2", hash);
        Assert.True(BCrypt.Net.BCrypt.Verify("secret1", hash));

        var langCustom = await new CpLangWriteService(new ConfiguredNeverOpened())
            .SetIsCustomAsync("", 1);
        Assert.False(langCustom.Succeeded);
        Assert.Equal("invalid", langCustom.Code);

        var langError = await new CpLangWriteService(new ConfiguredNeverOpened())
            .SetIsErrorAsync("hello", 3);
        Assert.False(langError.Succeeded);
        Assert.Equal("invalid", langError.Code);

        var langSame = await new CpLangWriteService(new ConfiguredNeverOpened())
            .SetSameAsync("hello", "xx!");
        Assert.False(langSame.Succeeded);
        Assert.Equal("invalid", langSame.Code);

        var langUsed = await new CpLangWriteService(new UnconfiguredConnections())
            .SetUsedFoundAsync("hello", 2);
        Assert.False(langUsed.Succeeded);
        Assert.Equal("db", langUsed.Code);

        var langSameDb = await new CpLangWriteService(new UnconfiguredConnections())
            .SetSameAsync("hello", "no");
        Assert.False(langSameDb.Succeeded);
        Assert.Equal("db", langSameDb.Code);

        var langTranslation = await new CpLangWriteService(new ConfiguredNeverOpened())
            .SaveTranslationAsync("hello", "", "Hi");
        Assert.False(langTranslation.Succeeded);
        Assert.Equal("invalid", langTranslation.Code);

        var langTranslationEmpty = await new CpLangWriteService(new ConfiguredNeverOpened())
            .SaveTranslationAsync("hello", "en", "");
        Assert.False(langTranslationEmpty.Succeeded);
        Assert.Equal("invalid", langTranslationEmpty.Code);

        var langTranslationDb = await new CpLangWriteService(new UnconfiguredConnections())
            .SaveTranslationAsync("hello", "en", "Hi");
        Assert.False(langTranslationDb.Succeeded);
        Assert.Equal("db", langTranslationDb.Code);

        var langDescription = await new CpLangWriteService(new ConfiguredNeverOpened())
            .SaveDescriptionAsync("hello", "");
        Assert.False(langDescription.Succeeded);
        Assert.Equal("invalid", langDescription.Code);

        var langDescriptionDb = await new CpLangWriteService(new UnconfiguredConnections())
            .SaveDescriptionAsync("hello", "About this string");
        Assert.False(langDescriptionDb.Succeeded);
        Assert.Equal("db", langDescriptionDb.Code);

        var langDeleteDb = await new CpLangWriteService(new UnconfiguredConnections())
            .DeleteUnusedCustomAsync();
        Assert.False(langDeleteDb.Succeeded);
        Assert.Equal("db", langDeleteDb.Code);

        var langCreateEmpty = await new CpLangWriteService(new ConfiguredNeverOpened())
            .CreateStringAsync(new CpLangCreateStringWriteRequest(Description: ""));
        Assert.False(langCreateEmpty.Succeeded);
        Assert.Equal("invalid", langCreateEmpty.Code);

        var langCreateError = await new CpLangWriteService(new ConfiguredNeverOpened())
            .CreateStringAsync(new CpLangCreateStringWriteRequest(Description: "Hello", IsError: 3));
        Assert.False(langCreateError.Succeeded);
        Assert.Equal("invalid", langCreateError.Code);

        var langCreateUsed = await new CpLangWriteService(new ConfiguredNeverOpened())
            .CreateStringAsync(new CpLangCreateStringWriteRequest(Description: "Hello", UsedFound: 9));
        Assert.False(langCreateUsed.Succeeded);
        Assert.Equal("invalid", langCreateUsed.Code);

        var langCreateSame = await new CpLangWriteService(new ConfiguredNeverOpened())
            .CreateStringAsync(new CpLangCreateStringWriteRequest(Description: "Hello", Same: "xx!"));
        Assert.False(langCreateSame.Succeeded);
        Assert.Equal("invalid", langCreateSame.Code);

        var langCreateDb = await new CpLangWriteService(new UnconfiguredConnections())
            .CreateStringAsync(new CpLangCreateStringWriteRequest(Description: "Hello", Same: "no"));
        Assert.False(langCreateDb.Succeeded);
        Assert.Equal("db", langCreateDb.Code);

        Assert.True(CpLangWriteService.TryNormalizeSame("no", out var sameNo, out _));
        Assert.Null(sameNo);
        Assert.True(CpLangWriteService.TryNormalizeSame("en", out var sameEn, out _));
        Assert.Equal("en", sameEn);
        Assert.False(CpLangWriteService.TryNormalizeSame("xx!", out _, out _));
        var nextKey = CpLangWriteService.NextStrKey("http://www.epartscart.com/", 1);
        Assert.Contains("_1_", nextKey, StringComparison.Ordinal);

        var channelInvalid = await new CpChannelWriteService(new ConfiguredNeverOpened())
            .ToggleAsync("", 1);
        Assert.False(channelInvalid.Succeeded);
        Assert.Equal("invalid", channelInvalid.Code);

        var channelDb = await new CpChannelWriteService(new UnconfiguredConnections())
            .ToggleAsync("amazon", 1);
        Assert.False(channelDb.Succeeded);
        Assert.Equal("db", channelDb.Code);

        var carrierInvalid = await new CpLogisticsWriteService(new ConfiguredNeverOpened())
            .ToggleCarrierAsync("");
        Assert.False(carrierInvalid.Succeeded);
        Assert.Equal("invalid", carrierInvalid.Code);

        var carrierDb = await new CpLogisticsWriteService(new UnconfiguredConnections())
            .ToggleCarrierAsync("dhl");
        Assert.False(carrierDb.Succeeded);
        Assert.Equal("db", carrierDb.Code);


        var actInvalid = await new CpCrmActivityWriteService(new ConfiguredNeverOpened())
            .ToggleDoneAsync(0, true);
        Assert.False(actInvalid.Succeeded);
        Assert.Equal("invalid", actInvalid.Code);

        var actDb = await new CpCrmActivityWriteService(new UnconfiguredConnections())
            .ToggleDoneAsync(3, true);
        Assert.False(actDb.Succeeded);
        Assert.Equal("db", actDb.Code);

        var wsAssign = await new CpWorkshopWriteService(new ConfiguredNeverOpened())
            .AssignAsync(0, 1, 1);
        Assert.False(wsAssign.Succeeded);
        Assert.Equal("invalid", wsAssign.Code);
