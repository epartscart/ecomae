using System.Data.Common;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Bos;
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

        var payActivateInvalid = await new CpPaymentsWriteService(new ConfiguredNeverOpened())
            .ActivateAsync("");
        Assert.False(payActivateInvalid.Succeeded);
        Assert.Equal("invalid", payActivateInvalid.Code);

        var payActivateStripped = await new CpPaymentsWriteService(new ConfiguredNeverOpened())
            .ActivateAsync("STRIPE!");
        Assert.False(payActivateStripped.Succeeded);
        Assert.Equal("invalid", payActivateStripped.Code);

        var payActivateDb = await new CpPaymentsWriteService(new UnconfiguredConnections())
            .ActivateAsync("stripe");
        Assert.False(payActivateDb.Succeeded);
        Assert.Equal("db", payActivateDb.Code);

        var paySettleInvalid = await new CpPaymentsWriteService(new ConfiguredNeverOpened())
            .MarkSettlementAsync(0, "paid_out");
        Assert.False(paySettleInvalid.Succeeded);
        Assert.Equal("invalid", paySettleInvalid.Code);

        var paySettleDb = await new CpPaymentsWriteService(new UnconfiguredConnections())
            .MarkSettlementAsync(12, "paid_out");
        Assert.False(paySettleDb.Succeeded);
        Assert.Equal("db", paySettleDb.Code);

        var crmInvalid = await new CpCrmWriteService(new ConfiguredNeverOpened())
            .SaveLeadAsync(-1, "Acme", "Ali", "", "", "web", "new", 1, 0, "");
        Assert.False(crmInvalid.Succeeded);
        Assert.Equal("invalid", crmInvalid.Code);

        var crmDb = await new CpCrmWriteService(new UnconfiguredConnections())
            .SaveLeadAsync(0, "Acme", "Ali", "", "", "web", "new", 1, 0, "");
        Assert.False(crmDb.Succeeded);
        Assert.Equal("db", crmDb.Code);

        var crmDelInvalid = await new CpCrmWriteService(new ConfiguredNeverOpened())
            .DeleteLeadAsync(0);
        Assert.False(crmDelInvalid.Succeeded);
        Assert.Equal("invalid", crmDelInvalid.Code);

        var crmDelDb = await new CpCrmWriteService(new UnconfiguredConnections())
            .DeleteLeadAsync(3);
        Assert.False(crmDelDb.Succeeded);
        Assert.Equal("db", crmDelDb.Code);

        var mktInvalid = await new CpMarketingGrowthWriteService(new ConfiguredNeverOpened())
            .SaveReviewAsync("", "weekly", 3, "", 1);
        Assert.False(mktInvalid.Succeeded);
        Assert.Equal("invalid", mktInvalid.Code);

        var mktDb = await new CpMarketingGrowthWriteService(new UnconfiguredConnections())
            .SaveReviewAsync("seo", "weekly", 3, "", 1);
        Assert.False(mktDb.Succeeded);
        Assert.Equal("db", mktDb.Code);

        var mktTaskInvalid = await new CpMarketingGrowthWriteService(new ConfiguredNeverOpened())
            .ToggleTaskAsync("seo", "", true);
        Assert.False(mktTaskInvalid.Succeeded);
        Assert.Equal("invalid", mktTaskInvalid.Code);
        Assert.Equal("Invalid task", mktTaskInvalid.Message);

        var mktTaskDb = await new CpMarketingGrowthWriteService(new UnconfiguredConnections())
            .ToggleTaskAsync("seo", "audit_titles", true);
        Assert.False(mktTaskDb.Succeeded);
        Assert.Equal("db", mktTaskDb.Code);

        var mktKpiInvalid = await new CpMarketingGrowthWriteService(new ConfiguredNeverOpened())
            .SaveKpiAsync("invented", "monthly_sessions", "10", "", 1);
        Assert.False(mktKpiInvalid.Succeeded);
        Assert.Equal("invalid", mktKpiInvalid.Code);
        Assert.Equal("Invalid KPI", mktKpiInvalid.Message);

        var mktKpiDb = await new CpMarketingGrowthWriteService(new UnconfiguredConnections())
            .SaveKpiAsync("seo", "indexed_pages", "12", "note", 1);
        Assert.False(mktKpiDb.Succeeded);
        Assert.Equal("db", mktKpiDb.Code);

        var oppInvalid = await new CpCrmOpportunityWriteService(new ConfiguredNeverOpened())
            .UpdateStageAsync(0, "won");
        Assert.False(oppInvalid.Succeeded);
        Assert.Equal("invalid", oppInvalid.Code);

        var oppStage = await new CpCrmOpportunityWriteService(new ConfiguredNeverOpened())
            .UpdateStageAsync(3, "nope");
        Assert.False(oppStage.Succeeded);
        Assert.Equal("invalid", oppStage.Code);

        var oppDb = await new CpCrmOpportunityWriteService(new UnconfiguredConnections())
            .UpdateStageAsync(3, "won");
        Assert.False(oppDb.Succeeded);
        Assert.Equal("db", oppDb.Code);

        Assert.Equal("prospect", CpCrmOpportunityWriteService.NormalizeStage("nope"));
        Assert.Equal("Opportunity", CpCrmOpportunityWriteService.NormalizeTitle(""));
        Assert.Equal(0, CpCrmOpportunityWriteService.ParseCloseDate(""));
        Assert.True(CpCrmOpportunityWriteService.ParseCloseDate("2026-09-09") > 0);

        var oppSaveBad = await new CpCrmOpportunityWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCrmOpportunitySaveRequest(-1, 0, "Deal", "prospect", 10, 10, "0", 1, 0, ""));
        Assert.False(oppSaveBad.Succeeded);
        Assert.Equal("invalid", oppSaveBad.Code);

        var oppSaveDb = await new CpCrmOpportunityWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpCrmOpportunitySaveRequest(0, 0, "Deal", "prospect", 10, 10, "0", 1, 0, ""));
        Assert.False(oppSaveDb.Succeeded);
        Assert.Equal("db", oppSaveDb.Code);

        var actInvalid = await new CpCrmActivityWriteService(new ConfiguredNeverOpened())
            .ToggleDoneAsync(0, true);
        Assert.False(actInvalid.Succeeded);
        Assert.Equal("invalid", actInvalid.Code);

        var actDb = await new CpCrmActivityWriteService(new UnconfiguredConnections())
            .ToggleDoneAsync(3, true);
        Assert.False(actDb.Succeeded);
        Assert.Equal("db", actDb.Code);

        Assert.Equal("task", CpCrmActivityWriteService.NormalizeActivityType("nope"));
        Assert.Equal("lead", CpCrmActivityWriteService.NormalizeRelatedType("nope"));
        Assert.True(CpCrmActivityWriteService.ParseDueDate("") > 0);
        Assert.True(CpCrmActivityWriteService.ParseDueDate("2026-09-09") > 0);

        var actSaveBad = await new CpCrmActivityWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCrmActivitySaveRequest(-1, "task", "lead", 0, "", false, 1, ""));
        Assert.False(actSaveBad.Succeeded);
        Assert.Equal("invalid", actSaveBad.Code);

        var actSaveDb = await new CpCrmActivityWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpCrmActivitySaveRequest(0, "task", "lead", 0, "", false, 1, ""));
        Assert.False(actSaveDb.Succeeded);
        Assert.Equal("db", actSaveDb.Code);

        var tixInvalid = await new CpCrmTicketWriteService(new ConfiguredNeverOpened())
            .UpdateStatusAsync(0, "open", "normal", "", 1);
        Assert.False(tixInvalid.Succeeded);
        Assert.Equal("invalid", tixInvalid.Code);

        var tixDb = await new CpCrmTicketWriteService(new UnconfiguredConnections())
            .UpdateStatusAsync(3, "open", "normal", "", 1);
        Assert.False(tixDb.Succeeded);
        Assert.Equal("db", tixDb.Code);

        Assert.Equal("open", CpCrmTicketWriteService.NormalizeStatus("nope"));
        Assert.Equal("normal", CpCrmTicketWriteService.NormalizePriority("medium"));
        Assert.Equal("Support request", CpCrmTicketWriteService.NormalizeSubject(""));

        var tixSaveBad = await new CpCrmTicketWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCrmTicketSaveRequest(-1, 0, 0, "Help", "open", "normal", 1, "", 1));
        Assert.False(tixSaveBad.Succeeded);
        Assert.Equal("invalid", tixSaveBad.Code);

        var tixSaveDb = await new CpCrmTicketWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpCrmTicketSaveRequest(0, 0, 0, "Help", "open", "normal", 1, "", 1));
        Assert.False(tixSaveDb.Succeeded);
        Assert.Equal("db", tixSaveDb.Code);

        Assert.Equal("draft", CpCrmQuoteWriteService.NormalizeStatus("nope"));
        Assert.Equal("Q-202609-0001", CpCrmQuoteWriteService.NextQuoteNumber(1, new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero)));

        var quoteSaveBad = await new CpCrmQuoteWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCrmQuoteSaveRequest(-1, 0, 0, 0, "", "draft", "", "", 1, 0));
        Assert.False(quoteSaveBad.Succeeded);
        Assert.Equal("invalid", quoteSaveBad.Code);

        var quoteSaveDb = await new CpCrmQuoteWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpCrmQuoteSaveRequest(0, 0, 0, 0, "", "draft", "", "", 1, 0));
        Assert.False(quoteSaveDb.Succeeded);
        Assert.Equal("db", quoteSaveDb.Code);

        Assert.Equal("planned", CpCrmProjectWriteService.NormalizeStatus("nope"));
        Assert.Equal("Project", CpCrmProjectWriteService.NormalizeName(""));

        var projSaveBad = await new CpCrmProjectWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCrmProjectSaveRequest(-1, "Job", 0, 0, "planned", 0, "", "", 1, ""));
        Assert.False(projSaveBad.Succeeded);
        Assert.Equal("invalid", projSaveBad.Code);

        var projSaveDb = await new CpCrmProjectWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpCrmProjectSaveRequest(0, "Job", 0, 0, "planned", 0, "", "", 1, ""));
        Assert.False(projSaveDb.Succeeded);
        Assert.Equal("db", projSaveDb.Code);

        Assert.Equal("todo", CpCrmProjectWriteService.NormalizeTaskStatus("nope"));
        Assert.Equal("Task", CpCrmProjectWriteService.NormalizeTaskTitle(""));

        var projTaskBad = await new CpCrmProjectWriteService(new ConfiguredNeverOpened())
            .SaveTaskAsync(new CpCrmProjectTaskSaveRequest(0, "Wire", "todo", 0, 1, ""));
        Assert.False(projTaskBad.Succeeded);
        Assert.Equal("invalid", projTaskBad.Code);

        var projTaskDb = await new CpCrmProjectWriteService(new UnconfiguredConnections())
            .SaveTaskAsync(new CpCrmProjectTaskSaveRequest(3, "Wire", "todo", 0, 1, ""));
        Assert.False(projTaskDb.Succeeded);
        Assert.Equal("db", projTaskDb.Code);

        Assert.Equal("draft", CpCrmContractWriteService.NormalizeStatus("nope"));
        Assert.Equal("monthly", CpCrmContractWriteService.NormalizeInterval("weekly"));
        Assert.Equal("Contract", CpCrmContractWriteService.NormalizeTitle(""));

        var contractSaveBad = await new CpCrmContractWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCrmContractSaveRequest(-1, 1, "Retainer", 10, "monthly", "", "draft", ""));
        Assert.False(contractSaveBad.Succeeded);
        Assert.Equal("invalid", contractSaveBad.Code);

        var contractSaveDb = await new CpCrmContractWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpCrmContractSaveRequest(0, 1, "Retainer", 10, "monthly", "", "draft", ""));
        Assert.False(contractSaveDb.Succeeded);
        Assert.Equal("db", contractSaveDb.Code);

        Assert.Equal("draft", CpCrmExpenseWriteService.NormalizeStatus("nope"));
        Assert.Equal("travel", CpCrmExpenseWriteService.NormalizeCategory(""));

        var expenseSaveBad = await new CpCrmExpenseWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCrmExpenseSaveRequest(-1, 1, 25, "travel", "draft", ""));
        Assert.False(expenseSaveBad.Succeeded);
        Assert.Equal("invalid", expenseSaveBad.Code);

        var expenseSaveDb = await new CpCrmExpenseWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpCrmExpenseSaveRequest(0, 1, 25, "travel", "draft", ""));
        Assert.False(expenseSaveDb.Succeeded);
        Assert.Equal("db", expenseSaveDb.Code);

        Assert.Equal("eParts", CpDocumentControlWriteService.Clip("  eParts  ", 255));

        var dcCompanyDb = await new CpDocumentControlWriteService(new UnconfiguredConnections())
            .SaveCompanyAsync(new CpDocumentCompanySaveRequest(0, "Co", "", "", "", "", "", "", "", "", "", "", "", "", "", null));
        Assert.False(dcCompanyDb.Succeeded);
        Assert.Equal("db", dcCompanyDb.Code);

        Assert.Equal("invoice", CpDocumentControlWriteService.NormalizeTemplateCode(" invoice "));

        var dcTemplateBad = await new CpDocumentControlWriteService(new ConfiguredNeverOpened())
            .SaveTemplateAsync(new CpDocumentTemplateSaveRequest("", "Invoice", "", "", "", "", "", true, null));
        Assert.False(dcTemplateBad.Succeeded);
        Assert.Equal("invalid", dcTemplateBad.Code);

        var dcTemplateDb = await new CpDocumentControlWriteService(new UnconfiguredConnections())
            .SaveTemplateAsync(new CpDocumentTemplateSaveRequest("invoice", "Invoice", "", "", "", "", "", true, null));
        Assert.False(dcTemplateDb.Succeeded);
        Assert.Equal("db", dcTemplateDb.Code);

        Assert.Equal("epartscart", CpAutoPriceWriteService.NormalizeSiteKey(" ePartsCart! "));
        Assert.Equal(1, CpAutoPriceWriteService.NextEnabled(0, null));

        var apToggleBad = await new CpAutoPriceWriteService(new ConfiguredNeverOpened())
            .ToggleSourceAsync(new CpAutoPriceSourceToggleRequest(0, "", null));
        Assert.False(apToggleBad.Succeeded);
        Assert.Equal("invalid", apToggleBad.Code);

        var apToggleDb = await new CpAutoPriceWriteService(new UnconfiguredConnections())
            .ToggleSourceAsync(new CpAutoPriceSourceToggleRequest(3, "", null));
        Assert.False(apToggleDb.Succeeded);
        Assert.Equal("db", apToggleDb.Code);

        var apDeleteBad = await new CpAutoPriceWriteService(new ConfiguredNeverOpened())
            .DeleteSourceAsync(0, "");
        Assert.False(apDeleteBad.Succeeded);
        Assert.Equal("invalid", apDeleteBad.Code);

        var apDeleteDb = await new CpAutoPriceWriteService(new UnconfiguredConnections())
            .DeleteSourceAsync(3, "");
        Assert.False(apDeleteDb.Succeeded);
        Assert.Equal("db", apDeleteDb.Code);

        var apAddBad = await new CpAutoPriceWriteService(new ConfiguredNeverOpened())
            .AddSourceAsync(new CpAutoPriceSourceAddRequest(0, "", "", "", null, null, ""));
        Assert.False(apAddBad.Succeeded);
        Assert.Equal("invalid", apAddBad.Code);

        var apAddOwn = await new CpAutoPriceWriteService(new ConfiguredNeverOpened())
            .AddSourceAsync(new CpAutoPriceSourceAddRequest(0, "https://www.epartscart.com/shop", "", "", null, null, ""));
        Assert.False(apAddOwn.Succeeded);
        Assert.Equal("forbidden", apAddOwn.Code);

        var apAddDb = await new CpAutoPriceWriteService(new UnconfiguredConnections())
            .AddSourceAsync(new CpAutoPriceSourceAddRequest(0, "parts.example.com", "Example", "", true, 100, "cp.local"));
        Assert.False(apAddDb.Succeeded);
        Assert.Equal("db", apAddDb.Code);

        var apSkipBad = await new CpAutoPriceWriteService(new ConfiguredNeverOpened())
            .SkipSourceAsync(new CpAutoPriceSourceSkipRequest(0, "epartscart", 24));
        Assert.False(apSkipBad.Succeeded);
        Assert.Equal("invalid", apSkipBad.Code);

        var apSkipDb = await new CpAutoPriceWriteService(new UnconfiguredConnections())
            .SkipSourceAsync(new CpAutoPriceSourceSkipRequest(3, "epartscart", 24));
        Assert.False(apSkipDb.Succeeded);
        Assert.Equal("db", apSkipDb.Code);

        var bulkReviewBad = await new CpBulkUploadWriteService(new ConfiguredNeverOpened())
            .MarkReviewedAsync(new CpBulkUploadMarkReviewedRequest(0, 1, "Reviewed"));
        Assert.False(bulkReviewBad.Succeeded);
        Assert.Equal("invalid", bulkReviewBad.Code);

        var bulkReviewDb = await new CpBulkUploadWriteService(new UnconfiguredConnections())
            .MarkReviewedAsync(new CpBulkUploadMarkReviewedRequest(3, 1, "Reviewed"));
        Assert.False(bulkReviewDb.Succeeded);
        Assert.Equal("db", bulkReviewDb.Code);

        var ftBad = await new CpFreeToolsWriteService(new ConfiguredNeverOpened())
            .ToggleAsync(new CpFreeToolsToggleRequest("", true));
        Assert.False(ftBad.Succeeded);
        Assert.Equal("invalid", ftBad.Code);

        var ftDb = await new CpFreeToolsWriteService(new UnconfiguredConnections())
            .ToggleAsync(new CpFreeToolsToggleRequest("vat", true));
        Assert.False(ftDb.Succeeded);
        Assert.Equal("db", ftDb.Code);

        var govBad = await new CpPlatformGovernanceWriteService(new ConfiguredNeverOpened())
            .SaveRuleAsync(new CpPlatformGovernanceSaveRuleRequest("", true, "required"));
        Assert.False(govBad.Succeeded);
        Assert.Equal("invalid", govBad.Code);

        var govDb = await new CpPlatformGovernanceWriteService(new UnconfiguredConnections())
            .SaveRuleAsync(new CpPlatformGovernanceSaveRuleRequest("data_retention", true, "required"));
        Assert.False(govDb.Succeeded);
        Assert.Equal("db", govDb.Code);

        var mobileDb = await new CpMobileAppsWriteService(new UnconfiguredConnections())
            .SaveMobileAsync(new CpMobileAppsSaveRequest(true, "App", "com.app", "", "", "", "", "", true, "", false));
        Assert.False(mobileDb.Succeeded);
        Assert.Equal("db", mobileDb.Code);

        var featBad = await new CpTenantFeaturesWriteService(new ConfiguredNeverOpened())
            .SaveFlagsAsync(new CpTenantFeaturesSaveRequest("", new Dictionary<string, bool> { ["email_smtp"] = true }));
        Assert.False(featBad.Succeeded);
        Assert.Equal("invalid", featBad.Code);

        var featDb = await new CpTenantFeaturesWriteService(new UnconfiguredConnections())
            .SaveFlagsAsync(new CpTenantFeaturesSaveRequest("epartscart", new Dictionary<string, bool> { ["email_smtp"] = true }));
        Assert.False(featDb.Succeeded);
        Assert.Equal("db", featDb.Code);

        var tenantBad = await new CpTenantsWriteService(new ConfiguredNeverOpened())
            .SetActiveAsync(new CpTenantsSetActiveRequest("", true));
        Assert.False(tenantBad.Succeeded);
        Assert.Equal("invalid", tenantBad.Code);

        var tenantDb = await new CpTenantsWriteService(new UnconfiguredConnections())
            .SetActiveAsync(new CpTenantsSetActiveRequest("epartscart", true));
        Assert.False(tenantDb.Succeeded);
        Assert.Equal("db", tenantDb.Code);

        var socialBad = await new CpSocialHubWriteService(new ConfiguredNeverOpened())
            .SaveDraftAsync(new CpSocialHubSaveDraftRequest(0, "", "instagram", "Hi", "", "", "", false, ""));
        Assert.False(socialBad.Succeeded);
        Assert.Equal("invalid", socialBad.Code);

        var socialDb = await new CpSocialHubWriteService(new UnconfiguredConnections())
            .SaveDraftAsync(new CpSocialHubSaveDraftRequest(0, "epartscart", "instagram", "Hi", "", "", "", false, "www.epartscart.com"));
        Assert.False(socialDb.Succeeded);
        Assert.Equal("db", socialDb.Code);

        var infoBad = await new CpInfoBlocksWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpInfoBlockSaveRequest(0, "", "", "platform", "", "homepage", "", "en", true, 0));
        Assert.False(infoBad.Succeeded);
        Assert.Equal("invalid", infoBad.Code);

        var infoDb = await new CpInfoBlocksWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpInfoBlockSaveRequest(0, "promo", "Summer", "platform", "", "homepage", "", "en", true, 0));
        Assert.False(infoDb.Succeeded);
        Assert.Equal("db", infoDb.Code);

        var infoDelBad = await new CpInfoBlocksWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(infoDelBad.Succeeded);
        Assert.Equal("invalid", infoDelBad.Code);

        var infoDelDb = await new CpInfoBlocksWriteService(new UnconfiguredConnections())
            .DeleteAsync(3);
        Assert.False(infoDelDb.Succeeded);
        Assert.Equal("db", infoDelDb.Code);

        var commBad = await new CpPlatformCommunicationWriteService(new ConfiguredNeverOpened())
            .SaveTaskAsync(new CpPlatformCommunicationSaveTaskRequest(0, "", "", 0, "", "", "support", "open", "normal", 0, 1));
        Assert.False(commBad.Succeeded);
        Assert.Equal("invalid", commBad.Code);

        var commDb = await new CpPlatformCommunicationWriteService(new UnconfiguredConnections())
            .SaveTaskAsync(new CpPlatformCommunicationSaveTaskRequest(0, "Follow up", "", 0, "", "platform", "support", "open", "normal", 0, 1));
        Assert.False(commDb.Succeeded);
        Assert.Equal("db", commDb.Code);

        var commDelBad = await new CpPlatformCommunicationWriteService(new ConfiguredNeverOpened())
            .DeleteTaskAsync(0);
        Assert.False(commDelBad.Succeeded);
        Assert.Equal("invalid", commDelBad.Code);

        var commDelDb = await new CpPlatformCommunicationWriteService(new UnconfiguredConnections())
            .DeleteTaskAsync(4);
        Assert.False(commDelDb.Succeeded);
        Assert.Equal("db", commDelDb.Code);

        var commSetDb = await new CpPlatformCommunicationWriteService(new UnconfiguredConnections())
            .SaveSettingsAsync(new CpPlatformCommunicationSaveSettingsRequest("Ops", "ops@ecomae.com", "", "6", true, true, true, true, false));
        Assert.False(commSetDb.Succeeded);
        Assert.Equal("db", commSetDb.Code);

        var priceCfgBad = await new CpPriceConfigsWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpPriceConfigSaveRequest(0, "", "platform", "", "all", "", 0, 0, "AED", 100, true, ""));
        Assert.False(priceCfgBad.Succeeded);
        Assert.Equal("invalid", priceCfgBad.Code);

        var priceCfgDb = await new CpPriceConfigsWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpPriceConfigSaveRequest(0, "Default markup", "platform", "", "all", "", 5, 0, "AED", 100, true, ""));
        Assert.False(priceCfgDb.Succeeded);
        Assert.Equal("db", priceCfgDb.Code);

        var priceCfgDelBad = await new CpPriceConfigsWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(priceCfgDelBad.Succeeded);
        Assert.Equal("invalid", priceCfgDelBad.Code);

        var priceCfgDelDb = await new CpPriceConfigsWriteService(new UnconfiguredConnections())
            .DeleteAsync(5);
        Assert.False(priceCfgDelDb.Succeeded);
        Assert.Equal("db", priceCfgDelDb.Code);

        var wfToggleBad = await new CpWorkflowsWriteService(new ConfiguredNeverOpened())
            .ToggleAsync(0, true);
        Assert.False(wfToggleBad.Succeeded);
        Assert.Equal("invalid", wfToggleBad.Code);

        var wfToggleDb = await new CpWorkflowsWriteService(new UnconfiguredConnections())
            .ToggleAsync(3, true);
        Assert.False(wfToggleDb.Succeeded);
        Assert.Equal("db", wfToggleDb.Code);

        var pbiBad = await new CpPowerBiWriteService(new ConfiguredNeverOpened())
            .SaveConfigAsync(new CpPowerBiSaveConfigRequest("", "", "", "", "", "", "none", ""));
        Assert.False(pbiBad.Succeeded);
        Assert.Equal("invalid", pbiBad.Code);

        var pbiDb = await new CpPowerBiWriteService(new UnconfiguredConnections())
            .SaveConfigAsync(new CpPowerBiSaveConfigRequest("epartscart", "ws", "", "", "", "", "none", ""));
        Assert.False(pbiDb.Succeeded);
        Assert.Equal("db", pbiDb.Code);

        var pbiRepBad = await new CpPowerBiWriteService(new ConfiguredNeverOpened())
            .AddReportAsync(new CpPowerBiAddReportRequest("", "r1", "Sales", "", "finance", ""));
        Assert.False(pbiRepBad.Succeeded);
        Assert.Equal("invalid", pbiRepBad.Code);

        var pbiRepDb = await new CpPowerBiWriteService(new UnconfiguredConnections())
            .AddReportAsync(new CpPowerBiAddReportRequest("epartscart", "r1", "Sales", "", "finance", ""));
        Assert.False(pbiRepDb.Succeeded);
        Assert.Equal("db", pbiRepDb.Code);

        var mbBad = await new CpMetabaseWriteService(new ConfiguredNeverOpened())
            .SaveConfigAsync(new CpMetabaseSaveConfigRequest("", "https://mb.example"));
        Assert.False(mbBad.Succeeded);
        Assert.Equal("invalid", mbBad.Code);

        var mbDb = await new CpMetabaseWriteService(new UnconfiguredConnections())
            .SaveConfigAsync(new CpMetabaseSaveConfigRequest("epartscart", "https://mb.example"));
        Assert.False(mbDb.Succeeded);
        Assert.Equal("db", mbDb.Code);

        var mbDashBad = await new CpMetabaseWriteService(new ConfiguredNeverOpened())
            .AddDashboardAsync(new CpMetabaseAddDashboardRequest("", 12, "Sales", "finance"));
        Assert.False(mbDashBad.Succeeded);
        Assert.Equal("invalid", mbDashBad.Code);

        var mbDashIdBad = await new CpMetabaseWriteService(new ConfiguredNeverOpened())
            .AddDashboardAsync(new CpMetabaseAddDashboardRequest("epartscart", 0, "Sales", "finance"));
        Assert.False(mbDashIdBad.Succeeded);
        Assert.Equal("invalid", mbDashIdBad.Code);

        var mbDashDb = await new CpMetabaseWriteService(new UnconfiguredConnections())
            .AddDashboardAsync(new CpMetabaseAddDashboardRequest("epartscart", 12, "Sales", "finance"));
        Assert.False(mbDashDb.Succeeded);
        Assert.Equal("db", mbDashDb.Code);

        var cartDelBad = await new CpAbandonedCartsWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(cartDelBad.Succeeded);
        Assert.Equal("invalid", cartDelBad.Code);

        var cartDelDb = await new CpAbandonedCartsWriteService(new UnconfiguredConnections())
            .DeleteAsync(9);
        Assert.False(cartDelDb.Succeeded);
        Assert.Equal("db", cartDelDb.Code);

        var nlBad = await new CpNlReportingWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpNlReportingSaveRequest(0, "", "Sales", "", "custom", "manual", "csv", true));
        Assert.False(nlBad.Succeeded);
        Assert.Equal("invalid", nlBad.Code);

        var nlNameBad = await new CpNlReportingWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpNlReportingSaveRequest(0, "epartscart", "", "", "custom", "manual", "csv", true));
        Assert.False(nlNameBad.Succeeded);
        Assert.Equal("invalid", nlNameBad.Code);

        var nlDb = await new CpNlReportingWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpNlReportingSaveRequest(0, "epartscart", "Sales", "", "custom", "manual", "csv", true));
        Assert.False(nlDb.Succeeded);
        Assert.Equal("db", nlDb.Code);

        var nlDelBad = await new CpNlReportingWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(nlDelBad.Succeeded);
        Assert.Equal("invalid", nlDelBad.Code);

        var nlDelDb = await new CpNlReportingWriteService(new UnconfiguredConnections())
            .DeleteAsync(4);
        Assert.False(nlDelDb.Succeeded);
        Assert.Equal("db", nlDelDb.Code);

        var sandPromoteBad = await new CpConfigSandboxWriteService(new ConfiguredNeverOpened())
            .PromoteAsync(0);
        Assert.False(sandPromoteBad.Succeeded);
        Assert.Equal("invalid", sandPromoteBad.Code);

        var sandPromoteDb = await new CpConfigSandboxWriteService(new UnconfiguredConnections())
            .PromoteAsync(3);
        Assert.False(sandPromoteDb.Succeeded);
        Assert.Equal("db", sandPromoteDb.Code);

        var sandDiscardBad = await new CpConfigSandboxWriteService(new ConfiguredNeverOpened())
            .DiscardAsync(0);
        Assert.False(sandDiscardBad.Succeeded);
        Assert.Equal("invalid", sandDiscardBad.Code);

        var sandDiscardDb = await new CpConfigSandboxWriteService(new UnconfiguredConnections())
            .DiscardAsync(3);
        Assert.False(sandDiscardDb.Succeeded);
        Assert.Equal("db", sandDiscardDb.Code);

        var mktInstBad = await new CpMarketplaceAppsWriteService(new ConfiguredNeverOpened())
            .InstallAsync(new CpMarketplaceInstallRequest(0, "epartscart"));
        Assert.False(mktInstBad.Succeeded);
        Assert.Equal("invalid", mktInstBad.Code);

        var mktInstSiteBad = await new CpMarketplaceAppsWriteService(new ConfiguredNeverOpened())
            .InstallAsync(new CpMarketplaceInstallRequest(4, ""));
        Assert.False(mktInstSiteBad.Succeeded);
        Assert.Equal("invalid", mktInstSiteBad.Code);

        var mktInstDb = await new CpMarketplaceAppsWriteService(new UnconfiguredConnections())
            .InstallAsync(new CpMarketplaceInstallRequest(4, "epartscart"));
        Assert.False(mktInstDb.Succeeded);
        Assert.Equal("db", mktInstDb.Code);

        var mktUninstBad = await new CpMarketplaceAppsWriteService(new ConfiguredNeverOpened())
            .UninstallAsync(new CpMarketplaceInstallRequest(0, "epartscart"));
        Assert.False(mktUninstBad.Succeeded);
        Assert.Equal("invalid", mktUninstBad.Code);

        var mktUninstDb = await new CpMarketplaceAppsWriteService(new UnconfiguredConnections())
            .UninstallAsync(new CpMarketplaceInstallRequest(4, "epartscart"));
        Assert.False(mktUninstDb.Succeeded);
        Assert.Equal("db", mktUninstDb.Code);

        var mktRevBad = await new CpMarketplaceAppsWriteService(new ConfiguredNeverOpened())
            .AddReviewAsync(new CpMarketplaceReviewRequest(0, "epartscart", 5, "ok", "text", "Ada"));
        Assert.False(mktRevBad.Succeeded);
        Assert.Equal("invalid", mktRevBad.Code);

        var mktRevSiteBad = await new CpMarketplaceAppsWriteService(new ConfiguredNeverOpened())
            .AddReviewAsync(new CpMarketplaceReviewRequest(4, "", 5, "ok", "text", "Ada"));
        Assert.False(mktRevSiteBad.Succeeded);
        Assert.Equal("invalid", mktRevSiteBad.Code);

        var mktRevDb = await new CpMarketplaceAppsWriteService(new UnconfiguredConnections())
            .AddReviewAsync(new CpMarketplaceReviewRequest(4, "epartscart", 5, "ok", "text", "Ada"));
        Assert.False(mktRevDb.Succeeded);
        Assert.Equal("db", mktRevDb.Code);

        var tokBad = await new CpDesignTokensWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpDesignTokenSaveRequest("", "brand_primary", "#111"));
        Assert.False(tokBad.Succeeded);
        Assert.Equal("invalid", tokBad.Code);

        var tokKeyBad = await new CpDesignTokensWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpDesignTokenSaveRequest("epartscart", "smtp_password", "x"));
        Assert.False(tokKeyBad.Succeeded);
        Assert.Equal("invalid", tokKeyBad.Code);

        var tokDb = await new CpDesignTokensWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpDesignTokenSaveRequest("epartscart", "brand_primary", "#111"));
        Assert.False(tokDb.Succeeded);
        Assert.Equal("db", tokDb.Code);

        var crmConvInvalid = await new CpCrmConvertWriteService(new ConfiguredNeverOpened())
            .ConvertLeadAsync(0, 1);
        Assert.False(crmConvInvalid.Succeeded);
        Assert.Equal("invalid", crmConvInvalid.Code);

        var crmConvDb = await new CpCrmConvertWriteService(new UnconfiguredConnections())
            .ConvertLeadAsync(3, 1);
        Assert.False(crmConvDb.Succeeded);
        Assert.Equal("db", crmConvDb.Code);

        var wsAssign = await new CpWorkshopWriteService(new ConfiguredNeverOpened())
            .AssignAsync(0, 1, 1);
        Assert.False(wsAssign.Succeeded);
        Assert.Equal("invalid", wsAssign.Code);

        var wsBay = await new CpWorkshopWriteService(new ConfiguredNeverOpened())
            .SaveBayAsync(0, "", "Bay A", 1, 0);
        Assert.False(wsBay.Succeeded);
        Assert.Equal("invalid", wsBay.Code);

        var wsTechDb = await new CpWorkshopWriteService(new UnconfiguredConnections())
            .SaveTechAsync(0, "Ali", "", "", 1);
        Assert.False(wsTechDb.Succeeded);
        Assert.Equal("db", wsTechDb.Code);

        var wsStatus = await new CpWorkshopWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(0, "approved");
        Assert.False(wsStatus.Succeeded);
        Assert.Equal("invalid", wsStatus.Code);

        var wsStatusBad = await new CpWorkshopWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(wsStatusBad.Succeeded);
        Assert.Equal("invalid", wsStatusBad.Code);

        var wsStatusDb = await new CpWorkshopWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "approved");
        Assert.False(wsStatusDb.Succeeded);
        Assert.Equal("db", wsStatusDb.Code);

        var wsCreateDb = await new CpWorkshopWriteService(new UnconfiguredConnections())
            .CreateJobAsync(new CpWorkshopCreateJobRequest(CustomerName: "Ali", Plate: "A12345"));
        Assert.False(wsCreateDb.Succeeded);
        Assert.Equal("db", wsCreateDb.Code);

        var wsLineInvalid = await new CpWorkshopWriteService(new ConfiguredNeverOpened())
            .AddLineAsync(new CpWorkshopAddLineRequest(0, "part", "Pad"));
        Assert.False(wsLineInvalid.Succeeded);
        Assert.Equal("invalid", wsLineInvalid.Code);

        var wsLineDb = await new CpWorkshopWriteService(new UnconfiguredConnections())
            .AddLineAsync(new CpWorkshopAddLineRequest(9, "part", "Pad"));
        Assert.False(wsLineDb.Succeeded);
        Assert.Equal("db", wsLineDb.Code);

        var wsApptDb = await new CpWorkshopWriteService(new UnconfiguredConnections())
            .CreateAppointmentAsync(new CpWorkshopCreateAppointmentRequest(CustomerName: "Ali"));
        Assert.False(wsApptDb.Succeeded);
        Assert.Equal("db", wsApptDb.Code);

        var wsConvertInvalid = await new CpWorkshopWriteService(new ConfiguredNeverOpened())
            .ConvertAppointmentAsync(0);
        Assert.False(wsConvertInvalid.Succeeded);
        Assert.Equal("invalid", wsConvertInvalid.Code);

        var wsConvertDb = await new CpWorkshopWriteService(new UnconfiguredConnections())
            .ConvertAppointmentAsync(3);
        Assert.False(wsConvertDb.Succeeded);
        Assert.Equal("db", wsConvertDb.Code);

        var posWalkinDb = await new CpPosWriteService(new UnconfiguredConnections())
            .EnsureWalkinUserAsync();
        Assert.False(posWalkinDb.Succeeded);
        Assert.Equal("db", posWalkinDb.Code);

        var priceAdd = await new CpPricesEditWriteService(new ConfiguredNeverOpened())
            .AddAsync(1, "", "Bosch", "Pad", 1, 12.5m, 1, "WH1", 1);
        Assert.False(priceAdd.Succeeded);
        Assert.Equal("invalid", priceAdd.Code);

        var priceSave = await new CpPricesEditWriteService(new ConfiguredNeverOpened())
            .SaveAsync(0, 1, "ABC", "Bosch", "Pad", 1, 12.5m, 1, "WH1", 1);
        Assert.False(priceSave.Succeeded);
        Assert.Equal("invalid", priceSave.Code);

        var priceDel = await new CpPricesEditWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(priceDel.Succeeded);
        Assert.Equal("invalid", priceDel.Code);

        var priceDb = await new CpPricesEditWriteService(new UnconfiguredConnections())
            .AddAsync(1, "ABC", "Bosch", "Pad", 1, 12.5m, 1, "WH1", 1);
        Assert.False(priceDb.Succeeded);
        Assert.Equal("db", priceDb.Code);

        var priceDelSearch = await new CpPricesEditWriteService(new ConfiguredNeverOpened())
            .DeleteSearchAsync(0, null, null, false, false, "ab");
        Assert.False(priceDelSearch.Succeeded);
        Assert.Equal("invalid", priceDelSearch.Code);

        var priceDelSearchDb = await new CpPricesEditWriteService(new UnconfiguredConnections())
            .DeleteSearchAsync(0, "ABC", null, false, false, null);
        Assert.False(priceDelSearchDb.Succeeded);
        Assert.Equal("db", priceDelSearchDb.Code);

        var ccyInvalid = await new CpCurrencyWriteService(new ConfiguredNeverOpened())
            .SetRateAsync("US", 3.67m);
        Assert.False(ccyInvalid.Succeeded);
        Assert.Equal("invalid", ccyInvalid.Code);

        var ccyRate = await new CpCurrencyWriteService(new ConfiguredNeverOpened())
            .SetRateAsync("USD", 0);
        Assert.False(ccyRate.Succeeded);
        Assert.Equal("invalid", ccyRate.Code);

        var ccyDb = await new CpCurrencyWriteService(new UnconfiguredConnections())
            .SetRateAsync("USD", 3.67m);
        Assert.False(ccyDb.Succeeded);
        Assert.Equal("db", ccyDb.Code);

        Assert.Equal(new[] { "USD", "EUR" }, CpCurrencyWriteService.ParseIsoList("usd, eur"));
        Assert.Equal(new[] { "USD", "784" }, CpCurrencyWriteService.ParseIsoList("[\"USD\",784]"));

        var ccyAvailEmpty = await new CpCurrencyWriteService(new ConfiguredNeverOpened())
            .SetAvailableAsync("", 1, null);
        Assert.False(ccyAvailEmpty.Succeeded);
        Assert.Equal("invalid", ccyAvailEmpty.Code);

        var ccyAvailFlag = await new CpCurrencyWriteService(new ConfiguredNeverOpened())
            .SetAvailableAsync("USD", 2, null);
        Assert.False(ccyAvailFlag.Succeeded);
        Assert.Equal("invalid", ccyAvailFlag.Code);

        var ccyAvailShop = await new CpCurrencyWriteService(new ConfiguredNeverOpened())
            .SetAvailableAsync("AED", 0, "AED");
        Assert.False(ccyAvailShop.Succeeded);
        Assert.Equal("shop", ccyAvailShop.Code);

        var ccyAvailDb = await new CpCurrencyWriteService(new UnconfiguredConnections())
            .SetAvailableAsync("USD", 1, null);
        Assert.False(ccyAvailDb.Succeeded);
        Assert.Equal("db", ccyAvailDb.Code);

        var ccySchedTz = await new CpCurrencyWriteService(new ConfiguredNeverOpened())
            .SaveScheduleAsync(1, "Not/AZone", 2);
        Assert.False(ccySchedTz.Succeeded);
        Assert.Equal("invalid", ccySchedTz.Code);

        var ccySchedHour = await new CpCurrencyWriteService(new ConfiguredNeverOpened())
            .SaveScheduleAsync(1, "UTC", 24);
        Assert.False(ccySchedHour.Succeeded);
        Assert.Equal("invalid", ccySchedHour.Code);

        var ccySchedDb = await new CpCurrencyWriteService(new UnconfiguredConnections())
            .SaveScheduleAsync(1, "UTC", 2);
        Assert.False(ccySchedDb.Succeeded);
        Assert.Equal("db", ccySchedDb.Code);
        Assert.False(ccyAvailDb.Succeeded);
        Assert.Equal("db", ccyAvailDb.Code);

        var meCode = await new ErpMultiEntityWriteService(new ConfiguredNeverOpened())
            .CreateGroupAsync("BAD CODE", "HoldCo", "", "AED", "12-31");
        Assert.False(meCode.Succeeded);
        Assert.Equal("invalid", meCode.Code);

        var meMember = await new ErpMultiEntityWriteService(new ConfiguredNeverOpened())
            .AddMemberAsync(0, "epartscart", "eParts", 100, "AED", "full");
        Assert.False(meMember.Succeeded);
        Assert.Equal("invalid", meMember.Code);

        var meConsol = await new ErpMultiEntityWriteService(new ConfiguredNeverOpened())
            .AddMemberAsync(9, "epartscart", "eParts", 100, "AED", "nope");
        Assert.False(meConsol.Succeeded);
        Assert.Equal("invalid", meConsol.Code);

        var meIc = await new ErpMultiEntityWriteService(new ConfiguredNeverOpened())
            .RecordIntercompanyAsync(9, "a", "b", 0, "x");
        Assert.False(meIc.Succeeded);
        Assert.Equal("invalid", meIc.Code);

        var meDb = await new ErpMultiEntityWriteService(new UnconfiguredConnections())
            .EliminateAsync(9);
        Assert.False(meDb.Succeeded);
        Assert.Equal("db", meDb.Code);

        Assert.Contains("proportional", ErpMultiEntityWriteService.AllowedConsolidations);
        var fxInvalid = await new ErpMultiCurrencyGlWriteService(new ConfiguredNeverOpened())
            .SetRateAsync("US", "AED", 3.67m, "2026-09-04", "manual");
        Assert.False(fxInvalid.Succeeded);
        Assert.Equal("invalid", fxInvalid.Code);

        var fxRate = await new ErpMultiCurrencyGlWriteService(new ConfiguredNeverOpened())
            .SetRateAsync("USD", "AED", 0, "2026-09-04", "manual");
        Assert.False(fxRate.Succeeded);
        Assert.Equal("invalid", fxRate.Code);

        var fxDate = await new ErpMultiCurrencyGlWriteService(new ConfiguredNeverOpened())
            .SetRateAsync("USD", "AED", 3.67m, "04-09-2026", "manual");
        Assert.False(fxDate.Succeeded);
        Assert.Equal("invalid", fxDate.Code);

        var fxDb = await new ErpMultiCurrencyGlWriteService(new UnconfiguredConnections())
            .SetRateAsync("USD", "AED", 3.67m, "2026-09-04", "manual");
        Assert.False(fxDb.Succeeded);
        Assert.Equal("db", fxDb.Code);

        Assert.Contains("KWD", ErpMultiCurrencyGlWriteService.AllowedCurrencies);

        var catEnable = await new CpCatalogueWriteService(new ConfiguredNeverOpened())
            .SetMinLimitEnableAsync(0, 1);
        Assert.False(catEnable.Succeeded);
        Assert.Equal("invalid", catEnable.Code);

        var catValueDb = await new CpCatalogueWriteService(new UnconfiguredConnections())
            .SetMinLimitValueAsync(9, 2);
        Assert.False(catValueDb.Succeeded);
        Assert.Equal("db", catValueDb.Code);

        var synAdd = await new CpManufacturerSynonymWriteService(new ConfiguredNeverOpened())
            .AddManufacturerAsync("   ");
        Assert.False(synAdd.Succeeded);
        Assert.Equal("invalid", synAdd.Code);

        var synSave = await new CpManufacturerSynonymWriteService(new ConfiguredNeverOpened())
            .SaveManufacturerAsync(0, "Bosch");
        Assert.False(synSave.Succeeded);
        Assert.Equal("invalid", synSave.Code);

        var synDel = await new CpManufacturerSynonymWriteService(new ConfiguredNeverOpened())
            .DeleteManufacturerAsync(0);
        Assert.False(synDel.Succeeded);
        Assert.Equal("invalid", synDel.Code);

        var synAddSyn = await new CpManufacturerSynonymWriteService(new ConfiguredNeverOpened())
            .AddSynonymAsync(0, "BOSCH");
        Assert.False(synAddSyn.Succeeded);
        Assert.Equal("invalid", synAddSyn.Code);

        var synSaveSyn = await new CpManufacturerSynonymWriteService(new ConfiguredNeverOpened())
            .SaveSynonymAsync(3, "\t");
        Assert.False(synSaveSyn.Succeeded);
        Assert.Equal("invalid", synSaveSyn.Code);

        var synDelSyn = await new CpManufacturerSynonymWriteService(new ConfiguredNeverOpened())
            .DeleteSynonymAsync(0);
        Assert.False(synDelSyn.Succeeded);
        Assert.Equal("invalid", synDelSyn.Code);

        var synDb = await new CpManufacturerSynonymWriteService(new UnconfiguredConnections())
            .AddManufacturerAsync("Bosch");
        Assert.False(synDb.Succeeded);
        Assert.Equal("db", synDb.Code);

        var crossSave = await new CpCrossWriteService(new ConfiguredNeverOpened())
            .SaveAsync(0, "ABC", "Bosch", "XYZ", "Febi");
        Assert.False(crossSave.Succeeded);
        Assert.Equal("invalid", crossSave.Code);

        var crossSaveEmpty = await new CpCrossWriteService(new ConfiguredNeverOpened())
            .SaveAsync(9, "ABC", "", "XYZ", "Febi");
        Assert.False(crossSaveEmpty.Succeeded);
        Assert.Equal("invalid", crossSaveEmpty.Code);

        var crossDel = await new CpCrossWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(crossDel.Succeeded);
        Assert.Equal("invalid", crossDel.Code);

        var crossDb = await new CpCrossWriteService(new UnconfiguredConnections())
            .SaveAsync(9, "ABC", "Bosch", "XYZ", "Febi");
        Assert.False(crossDb.Succeeded);
        Assert.Equal("db", crossDb.Code);

        var crossAddEmpty = await new CpCrossWriteService(new ConfiguredNeverOpened())
            .AddAsync("", "Bosch", "XYZ", "Febi");
        Assert.False(crossAddEmpty.Succeeded);
        Assert.Equal("invalid", crossAddEmpty.Code);

        Assert.Equal("TOYOTA", CpCrossWriteService.InferBrandFromArticle("90915-10001"));
        Assert.Equal("HONDA", CpCrossWriteService.InferBrandFromArticle("15400ABC"));
        Assert.Equal("NISSAN", CpCrossWriteService.InferBrandFromArticle("15208-123"));
        Assert.Equal("VAG", CpCrossWriteService.InferBrandFromArticle("1K0123456"));
        Assert.Equal(string.Empty, CpCrossWriteService.InferBrandFromArticle("ZZZ999"));

        var crossDelSearch = await new CpCrossWriteService(new ConfiguredNeverOpened())
            .DeleteSearchAsync(null, null, false, 0, 0);
        Assert.False(crossDelSearch.Succeeded);
        Assert.Equal("invalid", crossDelSearch.Code);

        var crossAddDb = await new CpCrossWriteService(new UnconfiguredConnections())
            .AddAsync("ABC", "Bosch", "XYZ", "Febi");
        Assert.False(crossAddDb.Succeeded);
        Assert.Equal("db", crossAddDb.Code);

        var crossDelSearchDb = await new CpCrossWriteService(new UnconfiguredConnections())
            .DeleteSearchAsync("ABC", null, false, 0, 0);
        Assert.False(crossDelSearchDb.Succeeded);
        Assert.Equal("db", crossDelSearchDb.Code);

        var retStatus = await new CpReturnWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(0, 0);
        Assert.False(retStatus.Succeeded);
        Assert.Equal("invalid", retStatus.Code);

        var retDecide = await new CpReturnWriteService(new ConfiguredNeverOpened())
            .DecideLineAsync(9, 0, 3, 1);
        Assert.False(retDecide.Succeeded);
        Assert.Equal("invalid", retDecide.Code);

        var retFinalize = await new CpReturnWriteService(new ConfiguredNeverOpened())
            .FinalizeAsync(0, 1);
        Assert.False(retFinalize.Succeeded);
        Assert.Equal("invalid", retFinalize.Code);

        var retFinalizeDb = await new CpReturnWriteService(new UnconfiguredConnections())
            .FinalizeAsync(9, 1);
        Assert.False(retFinalizeDb.Succeeded);
        Assert.Equal("db", retFinalizeDb.Code);

        var favAuth = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .AddAsync(0, "overview", "dashboard");
        Assert.False(favAuth.Succeeded);
        Assert.Equal("auth", favAuth.Code);

        var favTab = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .AddAsync(1, "overview", "   ");
        Assert.False(favTab.Succeeded);
        Assert.Equal("invalid", favTab.Code);

        var favRemoveTab = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .RemoveAsync(1, "");
        Assert.False(favRemoveTab.Succeeded);
        Assert.Equal("invalid", favRemoveTab.Code);

        var favDb = await new ErpWorkspaceFavoritesWriteService(new UnconfiguredConnections())
            .AddAsync(1, "overview", "dashboard");
        Assert.False(favDb.Succeeded);
        Assert.Equal("db", favDb.Code);

        var reorderInvalid = await new ErpInventoryReorderWriteService(new ConfiguredNeverOpened())
            .SetReorderLevelAsync(0, 4);
        Assert.False(reorderInvalid.Succeeded);
        Assert.Equal("invalid", reorderInvalid.Code);

        var reorderDb = await new ErpInventoryReorderWriteService(new UnconfiguredConnections())
            .SetReorderLevelAsync(9, 4);
        Assert.False(reorderDb.Succeeded);
        Assert.Equal("db", reorderDb.Code);

        var mvInvalid = await new ErpInventoryMovementWriteService(new ConfiguredNeverOpened())
            .RecordMovementAsync(new ErpInventoryMovementWriteRequest(1, "adjustment", 0, 9, 1));
        Assert.False(mvInvalid.Succeeded);
        Assert.Equal("invalid", mvInvalid.Code);

        var mvDb = await new ErpInventoryMovementWriteService(new UnconfiguredConnections())
            .RecordMovementAsync(new ErpInventoryMovementWriteRequest(1, "purchase_in", 1, 9, 2, 1.5m));
        Assert.False(mvDb.Succeeded);
        Assert.Equal("db", mvDb.Code);

        var trInvalid = await new ErpInventoryMovementWriteService(new ConfiguredNeverOpened())
            .TransferAsync(new ErpInventoryTransferWriteRequest(1, 1, 1, 9, 2));
        Assert.False(trInvalid.Succeeded);
        Assert.Equal("invalid", trInvalid.Code);

        var trDb = await new ErpInventoryMovementWriteService(new UnconfiguredConnections())
            .TransferAsync(new ErpInventoryTransferWriteRequest(1, 1, 2, 9, 2));
        Assert.False(trDb.Succeeded);
        Assert.Equal("db", trDb.Code);

        var whInvalid = await new ErpInventoryMovementWriteService(new ConfiguredNeverOpened())
            .CreateWarehouseAsync("  ", "Main");
        Assert.False(whInvalid.Succeeded);
        Assert.Equal("invalid", whInvalid.Code);

        var invWhDb = await new ErpInventoryMovementWriteService(new UnconfiguredConnections())
            .CreateWarehouseAsync("MAIN2", "Main 2");
        Assert.False(invWhDb.Succeeded);
        Assert.Equal("db", invWhDb.Code);

        var itemInvalid = await new ErpInventoryMovementWriteService(new ConfiguredNeverOpened())
            .CreateItemAsync(new ErpInventoryItemWriteRequest("SKU", "  "));
        Assert.False(itemInvalid.Succeeded);
        Assert.Equal("invalid", itemInvalid.Code);

        var itemDb = await new ErpInventoryMovementWriteService(new UnconfiguredConnections())
            .CreateItemAsync(new ErpInventoryItemWriteRequest("SKU-1", "Pad"));
        Assert.False(itemDb.Succeeded);
        Assert.Equal("db", itemDb.Code);

        var closeDb = await new ErpInventoryMovementWriteService(new UnconfiguredConnections())
            .RunClosingAsync("2026-09-30", 0);
        Assert.False(closeDb.Succeeded);
        Assert.Equal("db", closeDb.Code);

        var syncDb = await new ErpInventoryMovementWriteService(new UnconfiguredConnections())
            .SyncWarehousesAsync();
        Assert.False(syncDb.Succeeded);
        Assert.Equal("db", syncDb.Code);

        var csvDb = await new ErpInventoryMovementWriteService(new UnconfiguredConnections())
            .ImportCsvAsync(new ErpInventoryCsvImportRequest(1, "sku,qty\nE2E-FILTER,1", 1, "purchase_in"));
        Assert.False(csvDb.Succeeded);
        Assert.Equal("db", csvDb.Code);

        var csvParsed = ErpInventoryMovementWriteService.ParseCsvText(
            "sku,qty,unit_cost,movement_type,warehouse_code\nE2E-FILTER,2,1.5,purchase_in,MAIN\n# skip\n,0,,,",
            1,
            "purchase_in");
        Assert.Single(csvParsed);
        Assert.Equal("E2E-FILTER", csvParsed[0]["sku"]);
        Assert.Equal("2", csvParsed[0]["qty"]);

        var dimInvalid = await new ErpDimensionWriteService(new ConfiguredNeverOpened())
            .SaveAsync("  ", 1, new Dictionary<string, long> { ["business_unit"] = 1 });
        Assert.False(dimInvalid.Succeeded);
        Assert.Equal("invalid", dimInvalid.Code);

        var dimDb = await new ErpDimensionWriteService(new UnconfiguredConnections())
            .SaveAsync("inventory_item", 1, new Dictionary<string, long> { ["business_unit"] = 1 });
        Assert.False(dimDb.Succeeded);
        Assert.Equal("db", dimDb.Code);

        var masterInvalid = await new ErpCustomerMasterWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpCustomerMasterWriteRequest(0, CustomerName: "Acme"));
        Assert.False(masterInvalid.Succeeded);
        Assert.Equal("invalid", masterInvalid.Code);

        var masterDb = await new ErpCustomerMasterWriteService(new UnconfiguredConnections())
            .SaveAsync(new ErpCustomerMasterWriteRequest(9, CustomerName: "Acme", CreditLimit: 5000m));
        Assert.False(masterDb.Succeeded);
        Assert.Equal("db", masterDb.Code);

        var tplDel = await new CpCatalogueWriteService(new ConfiguredNeverOpened())
            .DeleteCategoryTemplateAsync(0);
        Assert.False(tplDel.Succeeded);
        Assert.Equal("invalid", tplDel.Code);

        var tplDb = await new CpCatalogueWriteService(new UnconfiguredConnections())
            .DeleteCategoryTemplateAsync(9);
        Assert.False(tplDb.Succeeded);
        Assert.Equal("db", tplDb.Code);

        var tplCreateCaption = await new CpCatalogueWriteService(new ConfiguredNeverOpened())
            .CreateCategoryTemplateAsync(new CpCategoryTemplateCreateRequest(Caption: " ", CategoryObject: "{}"));
        Assert.False(tplCreateCaption.Succeeded);
        Assert.Equal("invalid", tplCreateCaption.Code);

        var tplCreateJson = await new CpCatalogueWriteService(new ConfiguredNeverOpened())
            .CreateCategoryTemplateAsync(new CpCategoryTemplateCreateRequest(Caption: "Tires", CategoryObject: "{"));
        Assert.False(tplCreateJson.Succeeded);
        Assert.Equal("invalid", tplCreateJson.Code);

        var tplCreateArray = await new CpCatalogueWriteService(new ConfiguredNeverOpened())
            .CreateCategoryTemplateAsync(new CpCategoryTemplateCreateRequest(Caption: "Tires", CategoryObject: "[]"));
        Assert.False(tplCreateArray.Succeeded);
        Assert.Equal("invalid", tplCreateArray.Code);

        var tplCreateImage = await new CpCatalogueWriteService(new ConfiguredNeverOpened())
            .CreateCategoryTemplateAsync(new CpCategoryTemplateCreateRequest(Caption: "Tires", CategoryObject: "{}", ImageBase64: "%%%"));
        Assert.False(tplCreateImage.Succeeded);
        Assert.Equal("invalid", tplCreateImage.Code);

        var tplCreateDb = await new CpCatalogueWriteService(new UnconfiguredConnections())
            .CreateCategoryTemplateAsync(new CpCategoryTemplateCreateRequest(Caption: "Tires", CategoryObject: """{"id":1,"value":"Root"}"""));
        Assert.False(tplCreateDb.Succeeded);
        Assert.Equal("db", tplCreateDb.Code);

        Assert.Equal("create", CpCatalogueWriteService.NormalizeAction("save_create"));
        Assert.Equal("delete", CpCatalogueWriteService.NormalizeAction("del"));
        Assert.Equal("&lt;b&gt;", CpCatalogueWriteService.HtmlEncode(" <b> "));
        Assert.Equal("category_object is not valid JSON.", CpCatalogueWriteService.TryParseCategoryObject("{").Error);
        Assert.Equal("category_object must be a JSON object.", CpCatalogueWriteService.TryParseCategoryObject("[]").Error);
        var okTpl = CpCatalogueWriteService.TryParseCategoryObject("""{"id":1}""");
        Assert.Null(okTpl.Error);
        Assert.Equal("""{"id":1}""", okTpl.Json);
        Assert.Null(CpCatalogueWriteService.TryDecodeImage("").Error);
        Assert.Equal("image must be base64.", CpCatalogueWriteService.TryDecodeImage("%%%").Error);
        var okImg = CpCatalogueWriteService.TryDecodeImage("data:image/png;base64,QQ==");
        Assert.Null(okImg.Error);
        Assert.Equal(new byte[] { 0x41 }, okImg.Bytes);

        var skuSaveMissing = await new CpSkuMediaWriteService(new ConfiguredNeverOpened())
            .SaveProfileAsync(new CpSkuMediaProfileRequest(Brand: "Bosch", Article: ""));
        Assert.False(skuSaveMissing.Succeeded);
        Assert.Equal("invalid", skuSaveMissing.Code);

        var skuEnsureMissing = await new CpSkuMediaWriteService(new ConfiguredNeverOpened())
            .EnsureAsync(new CpSkuMediaProfileRequest());
        Assert.False(skuEnsureMissing.Succeeded);
        Assert.Equal("invalid", skuEnsureMissing.Code);

        var skuDelId = await new CpSkuMediaWriteService(new ConfiguredNeverOpened())
            .DeleteProfileAsync(0);
        Assert.False(skuDelId.Succeeded);
        Assert.Equal("invalid", skuDelId.Code);

        var skuSaveDb = await new CpSkuMediaWriteService(new UnconfiguredConnections())
            .SaveProfileAsync(new CpSkuMediaProfileRequest(Brand: "Bosch", Article: "0 986 494 053"));
        Assert.False(skuSaveDb.Succeeded);
        Assert.Equal("db", skuSaveDb.Code);

        var skuEnsureDb = await new CpSkuMediaWriteService(new UnconfiguredConnections())
            .EnsureAsync(new CpSkuMediaProfileRequest(Brand: "Bosch", Article: "0986494053"));
        Assert.False(skuEnsureDb.Succeeded);
        Assert.Equal("db", skuEnsureDb.Code);

        var skuDelDb = await new CpSkuMediaWriteService(new UnconfiguredConnections())
            .DeleteProfileAsync(4);
        Assert.False(skuDelDb.Succeeded);
        Assert.Equal("db", skuDelDb.Code);

        Assert.Equal("save_profile", CpSkuMediaWriteService.NormalizeAction("edit"));
        Assert.Equal("ensure", CpSkuMediaWriteService.NormalizeAction("ensure_profile"));
        Assert.Equal("delete_profile", CpSkuMediaWriteService.NormalizeAction("del"));
        Assert.Equal("BOSCH", CpSkuMediaWriteService.NormalizeBrand("  bosch  "));
        Assert.Equal("0986494053", CpSkuMediaWriteService.NormalizeArticle("0 986 494 053"));
        Assert.Equal("active", CpSkuMediaWriteService.NormalizeStatus(""));
        Assert.Equal("draft", CpSkuMediaWriteService.NormalizeStatus("Draft"));
        Assert.Equal("add_spec_group", CpSkuMediaWriteService.NormalizeAction("create_spec_group"));
        Assert.Equal("update_spec_row", CpSkuMediaWriteService.NormalizeAction("save_spec_row"));
        Assert.Equal("update_photo", CpSkuMediaWriteService.NormalizeAction("save_photo"));
        Assert.Equal("technical_specs", CpSkuMediaWriteService.NormalizeGroupCode("Technical Specs"));
        Assert.Equal("number", CpSkuMediaWriteService.NormalizeValueType("NUMBER"));
        Assert.Equal("text", CpSkuMediaWriteService.NormalizeValueType("nope"));
        Assert.Equal("diagram", CpSkuMediaWriteService.NormalizePhotoType("diagram"));
        Assert.Equal("product", CpSkuMediaWriteService.NormalizePhotoType("nope"));
        Assert.Equal("0", CpSkuMediaWriteService.NormalizeBoolValue("false"));
        Assert.Equal("1", CpSkuMediaWriteService.NormalizeBoolValue("yes"));

        var specGroupName = await new CpSkuMediaWriteService(new ConfiguredNeverOpened())
            .AddSpecGroupAsync(new CpSkuMediaSpecGroupRequest(ProfileId: 1, Name: " "));
        Assert.False(specGroupName.Succeeded);
        Assert.Equal("invalid", specGroupName.Code);

        var specGroupMissing = await new CpSkuMediaWriteService(new ConfiguredNeverOpened())
            .AddSpecGroupAsync(new CpSkuMediaSpecGroupRequest(ProfileId: 0, Name: "Technical"));
        Assert.False(specGroupMissing.Succeeded);
        Assert.Equal("invalid", specGroupMissing.Code);

        var specGroupDb = await new CpSkuMediaWriteService(new UnconfiguredConnections())
            .AddSpecGroupAsync(new CpSkuMediaSpecGroupRequest(ProfileId: 1, Name: "Technical"));
        Assert.False(specGroupDb.Succeeded);
        Assert.Equal("db", specGroupDb.Code);

        var specRowLabel = await new CpSkuMediaWriteService(new ConfiguredNeverOpened())
            .AddSpecRowAsync(new CpSkuMediaSpecRowRequest(GroupId: 1, Label: " "));
        Assert.False(specRowLabel.Succeeded);
        Assert.Equal("invalid", specRowLabel.Code);

        var specRowDb = await new CpSkuMediaWriteService(new UnconfiguredConnections())
            .AddSpecRowAsync(new CpSkuMediaSpecRowRequest(GroupId: 1, Label: "Voltage"));
        Assert.False(specRowDb.Succeeded);
        Assert.Equal("db", specRowDb.Code);

        var specRowUpdate = await new CpSkuMediaWriteService(new ConfiguredNeverOpened())
            .UpdateSpecRowAsync(new CpSkuMediaSpecRowRequest(RowId: 0, Label: "Voltage"));
        Assert.False(specRowUpdate.Succeeded);
        Assert.Equal("invalid", specRowUpdate.Code);

        var specDelDb = await new CpSkuMediaWriteService(new UnconfiguredConnections())
            .DeleteSpecGroupAsync(3);
        Assert.False(specDelDb.Succeeded);
        Assert.Equal("db", specDelDb.Code);

        var photoUpdate = await new CpSkuMediaWriteService(new ConfiguredNeverOpened())
            .UpdatePhotoAsync(new CpSkuMediaPhotoMetaRequest(PhotoId: 0));
        Assert.False(photoUpdate.Succeeded);
        Assert.Equal("invalid", photoUpdate.Code);

        var photoDelDb = await new CpSkuMediaWriteService(new UnconfiguredConnections())
            .DeletePhotoAsync(4);
        Assert.False(photoDelDb.Succeeded);
        Assert.Equal("db", photoDelDb.Code);

        var homeJson = await new CpMainPageProductsWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpMainPageProductsSaveRequest(TreeJson: "{"));
        Assert.False(homeJson.Succeeded);
        Assert.Equal("invalid", homeJson.Code);

        var homeValue = await new CpMainPageProductsWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpMainPageProductsSaveRequest(TreeJson: """[{"show_caption":1,"active":1,"data":[]}]"""));
        Assert.False(homeValue.Succeeded);
        Assert.Equal("invalid", homeValue.Code);

        var homeProduct = await new CpMainPageProductsWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpMainPageProductsSaveRequest(TreeJson: """[{"value":"Featured","data":[{"product_id":0}]}]"""));
        Assert.False(homeProduct.Succeeded);
        Assert.Equal("invalid", homeProduct.Code);

        var homeDb = await new CpMainPageProductsWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpMainPageProductsSaveRequest(TreeJson: """[{"value":"Featured","show_caption":1,"active":1,"data":[{"product_id":9}]}]"""));
        Assert.False(homeDb.Succeeded);
        Assert.Equal("db", homeDb.Code);

        Assert.Equal("save", CpMainPageProductsWriteService.NormalizeAction("save_tree"));
        Assert.Equal("&lt;b&gt;", CpMainPageProductsWriteService.HtmlEncode(" <b> "));
        var emptyHome = CpMainPageProductsWriteService.ParseTree("");
        Assert.Null(emptyHome.Error);
        Assert.Empty(emptyHome.Groups);
        Assert.Equal("tree_json is not valid JSON.", CpMainPageProductsWriteService.ParseTree("{").Error);
        var okHome = CpMainPageProductsWriteService.ParseTree("""[{"value":"Featured","show_caption":1,"active":1,"data":[{"product_id":9}]}]""");
        Assert.Null(okHome.Error);
        Assert.Equal("Featured", okHome.Groups[0].Value);
        Assert.Equal(1, okHome.Groups[0].ShowCaption);
        Assert.Equal(9, okHome.Groups[0].Products[0].ProductId);

        var searchJson = await new CpSpecialSearchWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpSpecialSearchSaveRequest(TreeJson: "{"));
        Assert.False(searchJson.Succeeded);
        Assert.Equal("invalid", searchJson.Code);

        var searchType = await new CpSpecialSearchWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpSpecialSearchSaveRequest(TreeJson: """[{"value":"Brand","type":3,"objects":[1]}]"""));
        Assert.False(searchType.Succeeded);
        Assert.Equal("invalid", searchType.Code);

        var searchExisting = await new CpSpecialSearchWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpSpecialSearchSaveRequest(SearchId: 9, TreeJson: """[{"value":"Brand","type":1,"is_new":false}]"""));
        Assert.False(searchExisting.Succeeded);
        Assert.Equal("invalid", searchExisting.Code);

        var searchDeleted = await new CpSpecialSearchWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpSpecialSearchSaveRequest(DeletedStepsJson: "{"));
        Assert.False(searchDeleted.Succeeded);
        Assert.Equal("invalid", searchDeleted.Code);

        var searchDb = await new CpSpecialSearchWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpSpecialSearchSaveRequest(
                Caption: "Brakes",
                TreeJson: """[{"value":"Brand","alias":"brand","type":1,"objects":[1],"is_new":true}]"""));
        Assert.False(searchDb.Succeeded);
        Assert.Equal("db", searchDb.Code);

        var searchDeleteEmpty = await new CpSpecialSearchWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("[]");
        Assert.False(searchDeleteEmpty.Succeeded);
        Assert.Equal("invalid", searchDeleteEmpty.Code);

        var searchDeleteJson = await new CpSpecialSearchWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("{");
        Assert.False(searchDeleteJson.Succeeded);
        Assert.Equal("invalid", searchDeleteJson.Code);

        var searchDeleteDb = await new CpSpecialSearchWriteService(new UnconfiguredConnections())
            .DeleteAsync("[9]");
        Assert.False(searchDeleteDb.Succeeded);
        Assert.Equal("db", searchDeleteDb.Code);

        Assert.Equal("save", CpSpecialSearchWriteService.NormalizeAction("create"));
        Assert.Equal("delete", CpSpecialSearchWriteService.NormalizeAction("delete_special_searches"));
        Assert.Equal("&lt;b&gt;", CpSpecialSearchWriteService.HtmlEncode(" <b> "));
        Assert.Equal("brakes.png", CpSpecialSearchWriteService.SanitizeImageName("../brakes.png"));
        Assert.True(CpSpecialSearchWriteService.HasAllowedImageExtension("brakes.png"));
        var searchImg = await new CpSpecialSearchWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpSpecialSearchSaveRequest(
                Caption: "Brakes",
                ImageName: "brakes.exe",
                TreeJson: """[{"value":"Brand","alias":"brand","type":1,"objects":[1],"is_new":true}]"""));
        Assert.False(searchImg.Succeeded);
        Assert.Equal("invalid", searchImg.Code);
        var emptySearch = CpSpecialSearchWriteService.ParseSteps("");
        Assert.Null(emptySearch.Error);
        Assert.Empty(emptySearch.Steps);
        Assert.Equal("tree_json is not valid JSON.", CpSpecialSearchWriteService.ParseSteps("{").Error);
        var okSearch = CpSpecialSearchWriteService.ParseSteps("""[{"value":"Brand","alias":"brand","type":"2","objects":[9],"is_new":true,"levels":[{"value":"L1","h1":"H"}]}]""");
        Assert.Null(okSearch.Error);
        Assert.Equal("Brand", okSearch.Steps[0].Value);
        Assert.Equal(2, okSearch.Steps[0].Type);
        Assert.Equal(9, okSearch.Steps[0].Objects[0]);
        Assert.True(okSearch.Steps[0].IsNew);
        Assert.Equal("L1", okSearch.Steps[0].Levels[0].Value);
        var okIds = CpSpecialSearchWriteService.ParseIds("""[1,"2"]""");
        Assert.Null(okIds.Error);
        Assert.Equal(new long[] { 1, 2 }, okIds.Ids.ToArray());

        var catEmpty = await new CpCatalogueEditorWriteService(new ConfiguredNeverOpened())
            .SaveTreeAsync(new CpCatalogueEditorSaveRequest(TreeJson: ""));
        Assert.False(catEmpty.Succeeded);
        Assert.Equal("invalid", catEmpty.Code);

        var catJson = await new CpCatalogueEditorWriteService(new ConfiguredNeverOpened())
            .SaveTreeAsync(new CpCatalogueEditorSaveRequest(TreeJson: "{"));
        Assert.False(catJson.Succeeded);
        Assert.Equal("invalid", catJson.Code);

        var catId = await new CpCatalogueEditorWriteService(new ConfiguredNeverOpened())
            .SaveTreeAsync(new CpCatalogueEditorSaveRequest(TreeJson: """[{"value":"Tires"}]"""));
        Assert.False(catId.Succeeded);
        Assert.Equal("invalid", catId.Code);

        var catDb = await new CpCatalogueEditorWriteService(new UnconfiguredConnections())
            .SaveTreeAsync(new CpCatalogueEditorSaveRequest(TreeJson: """[{"id":9,"value":"Tires","published_flag":1}]"""));
        Assert.False(catDb.Succeeded);
        Assert.Equal("db", catDb.Code);

        Assert.Equal("save", CpCatalogueEditorWriteService.NormalizeAction("save_tree"));
        Assert.Equal("&lt;b&gt;", CpCatalogueEditorWriteService.HtmlEncode(" <b> "));
        Assert.Equal("tree_json is not valid JSON.", CpCatalogueEditorWriteService.ParseTree("{").Error);
        var okCat = CpCatalogueEditorWriteService.ParseTree("""[{"id":9,"value":"Tires","alias":"tires","published_flag":1,"$level":1,"$parent":0,"data":[{"id":10,"value":"Winter","$level":2,"$parent":9}]}]""");
        Assert.Null(okCat.Error);
        Assert.Equal(2, okCat.Categories.Count);
        Assert.Equal("Tires", okCat.Categories[0].Value);
        Assert.Equal(9, okCat.Categories[0].Id);
        Assert.Equal(10, okCat.Categories[1].Id);
        Assert.Equal(9, okCat.Categories[1].Parent);
        Assert.Equal(1, okCat.Categories[0].PublishedFlag);

        var prodAction = await new CpCatalogueProductWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCatalogueProductSaveRequest(Action: "upload", CategoryId: 1, Caption: "Pad"));
        Assert.False(prodAction.Succeeded);
        Assert.Equal("invalid", prodAction.Code);

        var prodCat = await new CpCatalogueProductWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCatalogueProductSaveRequest(Action: "create", Caption: "Pad"));
        Assert.False(prodCat.Succeeded);
        Assert.Equal("invalid", prodCat.Code);

        var prodId = await new CpCatalogueProductWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCatalogueProductSaveRequest(Action: "edit", ProductId: 0, Caption: "Pad"));
        Assert.False(prodId.Succeeded);
        Assert.Equal("invalid", prodId.Code);

        var prodProps = await new CpCatalogueProductWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCatalogueProductSaveRequest(Action: "create", CategoryId: 1, PropertiesJson: "{"));
        Assert.False(prodProps.Succeeded);
        Assert.Equal("invalid", prodProps.Code);

        var prodDb = await new CpCatalogueProductWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpCatalogueProductSaveRequest(Action: "create", CategoryId: 1, Caption: "Pad"));
        Assert.False(prodDb.Succeeded);
        Assert.Equal("db", prodDb.Code);

        Assert.Equal("create", CpCatalogueProductWriteService.NormalizeAction("save", 0));
        Assert.Equal("edit", CpCatalogueProductWriteService.NormalizeAction("save", 9));
        Assert.Equal("Pad", CpCatalogueProductWriteService.SanitizePlain(" Pa'd\n"));
        Assert.Equal("[CODE]x[/CODE]", CpCatalogueProductWriteService.StripPhp("<?x?>"));
        var okProps = CpCatalogueProductWriteService.ParseProperties("""[{"property_id":1,"property_type_id":5,"list_type":2,"manual_input":"Ceramic","value":[9]}]""");
        Assert.Null(okProps.Error);
        Assert.Equal(9, okProps.Properties[0].OptionIds[0]);
        Assert.Equal(2, okProps.Properties[0].ListType);
        Assert.Equal("Ceramic", okProps.Properties[0].ManualInput);
        Assert.Equal(new[] { "Ceramic;Pads" }, CpCatalogueProductWriteService.SplitManualInput("Ceramic;Pads", 1).ToArray());
        Assert.Equal(new[] { "A", "B" }, CpCatalogueProductWriteService.SplitManualInput("A; B", 2).ToArray());
        var sortItems = new List<(long Id, string Caption, bool IsNew)> { (2, "b", false), (1, "a", false) };
        CpCatalogueProductWriteService.SortLineListItems(sortItems, "asc", "text");
        Assert.Equal("a", sortItems[0].Caption);
        Assert.Equal("images_list is not valid JSON.", CpCatalogueProductWriteService.ParseImages("{").Error);
        var noImages = CpCatalogueProductWriteService.ParseImages("");
        Assert.False(noImages.Touched);
        var okImages = CpCatalogueProductWriteService.ParseImages("""[{"name":"../pad.png","image_of_template":1,"server_id":0}]""");
        Assert.True(okImages.Touched);
        Assert.Equal("pad.png", okImages.Images[0].Name);
        Assert.True(okImages.Images[0].ImageOfTemplate);
        var prodImages = await new CpCatalogueProductWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpCatalogueProductSaveRequest(Action: "create", CategoryId: 1, Caption: "Pad", ImagesJson: "{"));
        Assert.False(prodImages.Succeeded);
        Assert.Equal("invalid", prodImages.Code);

        var reviewId = await new CpCatalogueReviewWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(reviewId.Succeeded);
        Assert.Equal("invalid", reviewId.Code);

        var reviewDb = await new CpCatalogueReviewWriteService(new UnconfiguredConnections())
            .DeleteAsync(9);
        Assert.False(reviewDb.Succeeded);
        Assert.Equal("db", reviewDb.Code);

        Assert.Equal("delete", CpCatalogueReviewWriteService.NormalizeAction("delete_review"));
        Assert.Equal("save", CpCatalogueReviewWriteService.NormalizeAction("save"));

        var prodDelEmpty = await new CpCatalogueProductsDeleteService(new ConfiguredNeverOpened())
            .DeleteAsync(new CpCatalogueProductsDeleteRequest());
        Assert.False(prodDelEmpty.Succeeded);
        Assert.Equal("invalid", prodDelEmpty.Code);

        var prodDelJson = await new CpCatalogueProductsDeleteService(new ConfiguredNeverOpened())
            .DeleteAsync(new CpCatalogueProductsDeleteRequest(ProductsJson: "{"));
        Assert.False(prodDelJson.Succeeded);
        Assert.Equal("invalid", prodDelJson.Code);

        var prodDelDb = await new CpCatalogueProductsDeleteService(new UnconfiguredConnections())
            .DeleteAsync(new CpCatalogueProductsDeleteRequest(ProductsJson: "[9]"));
        Assert.False(prodDelDb.Succeeded);
        Assert.Equal("db", prodDelDb.Code);

        Assert.Equal("delete", CpCatalogueProductsDeleteService.NormalizeAction("delete_products"));
        Assert.Equal("products_list is not valid JSON.", CpCatalogueProductsDeleteService.ParseIds("{").Error);
        var okDelIds = CpCatalogueProductsDeleteService.ParseIds("""[1,"2",1]""");
        Assert.Null(okDelIds.Error);
        Assert.Equal(new long[] { 1, 2 }, okDelIds.Ids.ToArray());
        Assert.Null(okProps.Error);
        Assert.Equal(9, okProps.Properties[0].OptionIds[0]);

        var jwCreate = await new ErpJwRepairWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpJwRepairSaveRequest());
        Assert.False(jwCreate.Succeeded);
        Assert.Equal("invalid", jwCreate.Code);

        var jwKarat = await new ErpJwKaratWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwKaratSaveRequest());
        Assert.False(jwKarat.Succeeded);
        Assert.Equal("invalid", jwKarat.Code);

        var jwRate = await new ErpJwRateTypeWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwRateTypeSaveRequest());
        Assert.False(jwRate.Succeeded);
        Assert.Equal("invalid", jwRate.Code);

        var jwCurrency = await new ErpJwCurrencyWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwCurrencySaveRequest());
        Assert.False(jwCurrency.Succeeded);
        Assert.Equal("invalid", jwCurrency.Code);

        var jwDiamond = await new ErpJwDiamondWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwDiamondSaveRequest());
        Assert.False(jwDiamond.Succeeded);
        Assert.Equal("invalid", jwDiamond.Code);

        var jwDesign = await new ErpJwDesignWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwDesignSaveRequest());
        Assert.False(jwDesign.Succeeded);
        Assert.Equal("invalid", jwDesign.Code);

        var jwPearl = await new ErpJwPearlWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwPearlSaveRequest());
        Assert.False(jwPearl.Succeeded);
        Assert.Equal("invalid", jwPearl.Code);

        var jwColorStone = await new ErpJwColorStoneWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwColorStoneSaveRequest());
        Assert.False(jwColorStone.Succeeded);
        Assert.Equal("invalid", jwColorStone.Code);

        var jwBarcode = await new ErpJwBarcodeWriteService(new ConfiguredNeverOpened())
            .GenerateAsync(new ErpJwBarcodeGenerateRequest());
        Assert.False(jwBarcode.Succeeded);
        Assert.Equal("invalid", jwBarcode.Code);

        var jwTag = await new ErpJwTagWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpJwTagCreateRequest());
        Assert.False(jwTag.Succeeded);
        Assert.Equal("invalid", jwTag.Code);

        var jwTagSell = await new ErpJwTagWriteService(new ConfiguredNeverOpened())
            .SellAsync(new ErpJwTagSellRequest());
        Assert.False(jwTagSell.Succeeded);
        Assert.Equal("invalid", jwTagSell.Code);

        var jwScheme = await new ErpJwGoldSchemeWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpJwGoldSchemeCreateRequest());
        Assert.False(jwScheme.Succeeded);
        Assert.Equal("invalid", jwScheme.Code);

        var jwEnroll = await new ErpJwGoldSchemeWriteService(new ConfiguredNeverOpened())
            .EnrollAsync(new ErpJwGoldSchemeEnrollRequest());
        Assert.False(jwEnroll.Succeeded);
        Assert.Equal("invalid", jwEnroll.Code);

        var jwPay = await new ErpJwGoldSchemeWriteService(new ConfiguredNeverOpened())
            .PayAsync(new ErpJwGoldSchemePayRequest());
        Assert.False(jwPay.Succeeded);
        Assert.Equal("invalid", jwPay.Code);

        var jwFixUnfix = await new ErpJwFixUnfixWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpJwFixUnfixCreateRequest());
        Assert.False(jwFixUnfix.Succeeded);
        Assert.Equal("invalid", jwFixUnfix.Code);

        var jwSettle = await new ErpJwFixUnfixWriteService(new ConfiguredNeverOpened())
            .SettleAsync(new ErpJwFixUnfixSettleRequest());
        Assert.False(jwSettle.Succeeded);
        Assert.Equal("invalid", jwSettle.Code);

        var jwBarcodePurchase = await new ErpJwBarcodePurchaseWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpJwBarcodePurchaseCreateRequest());
        Assert.False(jwBarcodePurchase.Succeeded);
        Assert.Equal("invalid", jwBarcodePurchase.Code);

        var jwBarcodeSell = await new ErpJwBarcodePurchaseWriteService(new ConfiguredNeverOpened())
            .SellAsync(new ErpJwBarcodePurchaseSellRequest());
        Assert.False(jwBarcodeSell.Succeeded);
        Assert.Equal("invalid", jwBarcodeSell.Code);

        var slaCreate = await new ErpSlaWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpSlaCreateRequest());
        Assert.False(slaCreate.Succeeded);
        Assert.Equal("invalid", slaCreate.Code);

        var touristRefund = await new ErpTouristRefundWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpTouristRefundCreateRequest());
        Assert.False(touristRefund.Succeeded);
        Assert.Equal("invalid", touristRefund.Code);
        var touristRefundDb = await new ErpTouristRefundWriteService(new UnconfiguredConnections())
            .CreateAsync(new ErpTouristRefundCreateRequest(TouristName: "Ada"));
        Assert.False(touristRefundDb.Succeeded);
        Assert.Equal("db", touristRefundDb.Code);
        var touristValidate = await new ErpTouristRefundWriteService(new ConfiguredNeverOpened())
            .ValidateAsync(new ErpTouristRefundValidateRequest());
        Assert.False(touristValidate.Succeeded);
        Assert.Equal("invalid", touristValidate.Code);
        var touristValidateDb = await new ErpTouristRefundWriteService(new UnconfiguredConnections())
            .ValidateAsync(new ErpTouristRefundValidateRequest("TR-1"));
        Assert.False(touristValidateDb.Succeeded);
        Assert.Equal("db", touristValidateDb.Code);

        var rfidReg = await new ErpRfidRegisterWriteService(new ConfiguredNeverOpened())
            .RegisterAsync(new ErpRfidRegisterRequest());
        Assert.False(rfidReg.Succeeded);
        Assert.Equal("invalid", rfidReg.Code);
        var rfidDb = await new ErpRfidRegisterWriteService(new UnconfiguredConnections())
            .RegisterAsync(new ErpRfidRegisterRequest(RfidEpc: "EPC-1"));
        Assert.False(rfidDb.Succeeded);
        Assert.Equal("db", rfidDb.Code);
        var rfidSessionDb = await new ErpRfidScanWriteService(new UnconfiguredConnections())
            .StartSessionAsync(new ErpRfidStartSessionRequest(WarehouseId: 1));
        Assert.False(rfidSessionDb.Succeeded);
        Assert.Equal("db", rfidSessionDb.Code);
        var rfidScan = await new ErpRfidScanWriteService(new ConfiguredNeverOpened())
            .ProcessScanAsync(new ErpRfidProcessScanRequest());
        Assert.False(rfidScan.Succeeded);
        Assert.Equal("invalid", rfidScan.Code);
        var rfidScanDb = await new ErpRfidScanWriteService(new UnconfiguredConnections())
            .ProcessScanAsync(new ErpRfidProcessScanRequest(SessionId: 1, RfidEpc: "EPC-1"));
        Assert.False(rfidScanDb.Succeeded);
        Assert.Equal("db", rfidScanDb.Code);

        var goldRate = await new ErpGoldRateSetWriteService(new ConfiguredNeverOpened())
            .SetAsync(new ErpGoldRateSetRequest());
        Assert.False(goldRate.Succeeded);
        Assert.Equal("invalid", goldRate.Code);
        var goldRateCcy = await new ErpGoldRateSetWriteService(new ConfiguredNeverOpened())
            .SetAsync(new ErpGoldRateSetRequest(BuyRate: 240, Currency: "AE"));
        Assert.False(goldRateCcy.Succeeded);
        Assert.Equal("invalid", goldRateCcy.Code);
        var goldRateDb = await new ErpGoldRateSetWriteService(new UnconfiguredConnections())
            .SetAsync(new ErpGoldRateSetRequest(BuyRate: 240, Currency: "AED"));
        Assert.False(goldRateDb.Succeeded);
        Assert.Equal("db", goldRateDb.Code);

        var amlKyc = await new ErpAmlKycSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpAmlKycSaveWriteRequest());
        Assert.False(amlKyc.Succeeded);
        Assert.Equal("invalid", amlKyc.Code);
        var amlKycDb = await new ErpAmlKycSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(new ErpAmlKycSaveWriteRequest(CustomerName: "Ada"));
        Assert.False(amlKycDb.Succeeded);
        Assert.Equal("db", amlKycDb.Code);

        var amlAlert = await new ErpAmlAlertStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(new ErpAmlAlertStatusWriteRequest());
        Assert.False(amlAlert.Succeeded);
        Assert.Equal("invalid", amlAlert.Code);
        var amlAlertStatus = await new ErpAmlAlertStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(new ErpAmlAlertStatusWriteRequest(Id: 3, TargetStatus: "nope"));
        Assert.False(amlAlertStatus.Succeeded);
        Assert.Equal("invalid", amlAlertStatus.Code);
        var amlAlertDb = await new ErpAmlAlertStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(new ErpAmlAlertStatusWriteRequest(Id: 3, TargetStatus: "reviewed"));
        Assert.False(amlAlertDb.Succeeded);
        Assert.Equal("db", amlAlertDb.Code);

        var ticketCreate = await new ErpTicketsWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpTicketsCreateRequest());
        Assert.False(ticketCreate.Succeeded);
        Assert.Equal("invalid", ticketCreate.Code);
        var ticketReply = await new ErpTicketsWriteService(new ConfiguredNeverOpened())
            .ReplyAsync(new ErpTicketsReplyRequest());
        Assert.False(ticketReply.Succeeded);
        Assert.Equal("invalid", ticketReply.Code);
        var ticketReplyDb = await new ErpTicketsWriteService(new UnconfiguredConnections())
            .ReplyAsync(new ErpTicketsReplyRequest(TicketId: 1, Message: "Noted"));
        Assert.False(ticketReplyDb.Succeeded);
        Assert.Equal("db", ticketReplyDb.Code);

        var groupCreate = await new ErpCustomerGroupsWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpCustomerGroupCreateRequest());
        Assert.False(groupCreate.Succeeded);
        Assert.Equal("invalid", groupCreate.Code);

        var groupAssign = await new ErpCustomerGroupsWriteService(new ConfiguredNeverOpened())
            .AssignAsync(new ErpCustomerGroupAssignRequest());
        Assert.False(groupAssign.Succeeded);
        Assert.Equal("invalid", groupAssign.Code);

        var reportSched = await new ErpReportSchedulerWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpReportScheduleCreateRequest());
        Assert.False(reportSched.Succeeded);
        Assert.Equal("invalid", reportSched.Code);

        var vwhCreate = await new ErpVirtualWarehouseWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpVirtualWarehouseCreateRequest());
        Assert.False(vwhCreate.Succeeded);
        Assert.Equal("invalid", vwhCreate.Code);

        var vwhTransfer = await new ErpVirtualWarehouseWriteService(new ConfiguredNeverOpened())
            .TransferAsync(new ErpVirtualWarehouseTransferRequest());
        Assert.False(vwhTransfer.Succeeded);
        Assert.Equal("invalid", vwhTransfer.Code);

        var jwMetal = await new ErpJwMetalStockWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwMetalStockSaveRequest());
        Assert.False(jwMetal.Succeeded);
        Assert.Equal("invalid", jwMetal.Code);

        var jwFixing = await new ErpJwFixingWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwFixingSaveRequest());
        Assert.False(jwFixing.Succeeded);
        Assert.Equal("invalid", jwFixing.Code);

        var jwVoucher = await new ErpJwVoucherWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwVoucherSaveRequest());
        Assert.False(jwVoucher.Succeeded);
        Assert.Equal("invalid", jwVoucher.Code);

        var jwPetty = await new ErpJwPettyCashWriteService(new ErpJwVoucherWriteService(new ConfiguredNeverOpened()))
            .SaveAsync(new ErpJwPettyCashSaveRequest());
        Assert.False(jwPetty.Succeeded);
        Assert.Equal("invalid", jwPetty.Code);

        var jwTourist = await new ErpJwTouristVatWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwTouristVatSaveRequest());
        Assert.False(jwTourist.Succeeded);
        Assert.Equal("invalid", jwTourist.Code);

        var jwReceipt = await new ErpJwRepairReceiptWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwRepairReceiptSaveRequest());
        Assert.False(jwReceipt.Succeeded);
        Assert.Equal("invalid", jwReceipt.Code);

        var jwXfer = await new ErpJwRepairTransferWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwRepairTransferSaveRequest());
        Assert.False(jwXfer.Succeeded);
        Assert.Equal("invalid", jwXfer.Code);

        var jwWs = await new ErpJwWorkshopReceiveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwWorkshopReceiveSaveRequest());
        Assert.False(jwWs.Succeeded);
        Assert.Equal("invalid", jwWs.Code);

        var jwDel = await new ErpJwRepairDeliveryWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwRepairDeliverySaveRequest());
        Assert.False(jwDel.Succeeded);
        Assert.Equal("invalid", jwDel.Code);

        var jwSv = await new ErpJwStockVerifyWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpJwStockVerifySaveRequest());
        Assert.False(jwSv.Succeeded);
        Assert.Equal("invalid", jwSv.Code);

        var asResolve = await new ErpAftersalesRmaWriteService(new ConfiguredNeverOpened())
            .ResolveAsync(new ErpAftersalesRmaResolveRequest());
        Assert.False(asResolve.Succeeded);
        Assert.Equal("invalid", asResolve.Code);

        var asWarranty = await new ErpAftersalesWarrantyWriteService(new ConfiguredNeverOpened())
            .RegisterAsync(new ErpAftersalesWarrantyRegisterRequest());
        Assert.False(asWarranty.Succeeded);
        Assert.Equal("invalid", asWarranty.Code);

        var asJob = await new ErpAftersalesJobWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpAftersalesJobCreateRequest());
        Assert.False(asJob.Succeeded);
        Assert.Equal("invalid", asJob.Code);

        var asJobLine = await new ErpAftersalesJobWriteService(new ConfiguredNeverOpened())
            .AddLineAsync(new ErpAftersalesJobAddLineRequest());
        Assert.False(asJobLine.Succeeded);
        Assert.Equal("invalid", asJobLine.Code);

        var asJobClose = await new ErpAftersalesJobWriteService(new ConfiguredNeverOpened())
            .CloseAsync(new ErpAftersalesJobCloseRequest());
        Assert.False(asJobClose.Succeeded);
        Assert.Equal("invalid", asJobClose.Code);

        var jwInvalid = await new ErpJwRepairWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(0, "ready");
        Assert.False(jwInvalid.Succeeded);
        Assert.Equal("invalid", jwInvalid.Code);

        var jwStatus = await new ErpJwRepairWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(jwStatus.Succeeded);
        Assert.Equal("invalid", jwStatus.Code);

        var jwDb = await new ErpJwRepairWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "ready");
        Assert.False(jwDb.Succeeded);
        Assert.Equal("db", jwDb.Code);

        var scAuth = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .DeleteShortcutAsync(0, 9);
        Assert.False(scAuth.Succeeded);
        Assert.Equal("auth", scAuth.Code);

        var scId = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .DeleteShortcutAsync(1, 0);
        Assert.False(scId.Succeeded);
        Assert.Equal("invalid", scId.Code);

        var scDb = await new ErpWorkspaceFavoritesWriteService(new UnconfiguredConnections())
            .DeleteShortcutAsync(1, 9);
        Assert.False(scDb.Succeeded);
        Assert.Equal("db", scDb.Code);

        var hrInvalid = await new ErpHrDaysWriteService(new ConfiguredNeverOpened())
            .SetDaysWorkedAsync(0, 22);
        Assert.False(hrInvalid.Succeeded);
        Assert.Equal("invalid", hrInvalid.Code);

        var hrDb = await new ErpHrDaysWriteService(new UnconfiguredConnections())
            .SetDaysWorkedAsync(9, 22);
        Assert.False(hrDb.Succeeded);
        Assert.Equal("db", hrDb.Code);

        var scKey = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .DeleteShortcutByKeyAsync(1, "!!!", "");
        Assert.False(scKey.Succeeded);
        Assert.Equal("invalid", scKey.Code);

        var scKeyDb = await new ErpWorkspaceFavoritesWriteService(new UnconfiguredConnections())
            .DeleteShortcutByKeyAsync(1, "dashboard", "erp");
        Assert.False(scKeyDb.Succeeded);
        Assert.Equal("db", scKeyDb.Code);

        var scResetAuth = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .ResetShortcutsAsync(0, "erp");
        Assert.False(scResetAuth.Succeeded);
        Assert.Equal("auth", scResetAuth.Code);

        var scResetDb = await new ErpWorkspaceFavoritesWriteService(new UnconfiguredConnections())
            .ResetShortcutsAsync(1, "");
        Assert.False(scResetDb.Succeeded);
        Assert.Equal("db", scResetDb.Code);

        var scAddAuth = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .AddShortcutAsync(0, "Orders", "/erp/sales-orders-app");
        Assert.False(scAddAuth.Succeeded);
        Assert.Equal("auth", scAddAuth.Code);

        var scAddLabel = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .AddShortcutAsync(1, "  ", "/erp/sales-orders-app");
        Assert.False(scAddLabel.Succeeded);
        Assert.Equal("invalid", scAddLabel.Code);

        var scAddJs = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .AddShortcutAsync(1, "XSS", "javascript:alert(1)");
        Assert.False(scAddJs.Succeeded);
        Assert.Equal("invalid", scAddJs.Code);

        var scAddData = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .AddShortcutAsync(1, "XSS", "data:text/html,hi");
        Assert.False(scAddData.Succeeded);
        Assert.Equal("invalid", scAddData.Code);

        var scAddDb = await new ErpWorkspaceFavoritesWriteService(new UnconfiguredConnections())
            .AddShortcutAsync(1, "Orders", "/erp/sales-orders-app", "sales_orders", "erp");
        Assert.False(scAddDb.Succeeded);
        Assert.Equal("db", scAddDb.Code);

        var scReorderAuth = await new ErpWorkspaceFavoritesWriteService(new ConfiguredNeverOpened())
            .ReorderShortcutsAsync(0, [1, 2]);
        Assert.False(scReorderAuth.Succeeded);
        Assert.Equal("auth", scReorderAuth.Code);

        var scReorderDb = await new ErpWorkspaceFavoritesWriteService(new UnconfiguredConnections())
            .ReorderShortcutsAsync(1, [1, 2]);
        Assert.False(scReorderDb.Succeeded);
        Assert.Equal("db", scReorderDb.Code);

        var grpAdd = await new CpStorageGroupWriteService(new ConfiguredNeverOpened())
            .AddAsync("  ", "1,2");
        Assert.False(grpAdd.Succeeded);
        Assert.Equal("invalid", grpAdd.Code);

        var grpDel = await new CpStorageGroupWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(grpDel.Succeeded);
        Assert.Equal("invalid", grpDel.Code);

        var grpDb = await new CpStorageGroupWriteService(new UnconfiguredConnections())
            .DeleteAsync(9);
        Assert.False(grpDb.Succeeded);
        Assert.Equal("db", grpDb.Code);

        var whCreate = await new CpStorageWriteService(new ConfiguredNeverOpened())
            .CreateAsync("", "WH", 1, 1, null, null, null, 0, 0);
        Assert.False(whCreate.Succeeded);
        Assert.Equal("invalid", whCreate.Code);

        var whEdit = await new CpStorageWriteService(new ConfiguredNeverOpened())
            .UpdateAsync(0, "Pad warehouse", "WH", 1, 1, null, null, null, 0, 0);
        Assert.False(whEdit.Succeeded);
        Assert.Equal("invalid", whEdit.Code);

        var whBadJson = await new CpStorageWriteService(new ConfiguredNeverOpened())
            .CreateAsync("Pad warehouse", "WH", 1, 1, null, "{", null, 0, 0);
        Assert.False(whBadJson.Succeeded);
        Assert.Equal("invalid", whBadJson.Code);

        var whDb = await new CpStorageWriteService(new UnconfiguredConnections())
            .CreateAsync("Pad warehouse", "WH", 1, 1, "1", "{}", null, 0, 0);
        Assert.False(whDb.Succeeded);
        Assert.Equal("db", whDb.Code);

        var memInvalid = await new CpStorageWriteService(new ConfiguredNeverOpened())
            .SaveMembershipAsync(0, "[]");
        Assert.False(memInvalid.Succeeded);
        Assert.Equal("invalid", memInvalid.Code);

        var memBadJson = await new CpStorageWriteService(new ConfiguredNeverOpened())
            .SaveMembershipAsync(1, "{");
        Assert.False(memBadJson.Succeeded);
        Assert.Equal("invalid", memBadJson.Code);

        var memDb = await new CpStorageWriteService(new UnconfiguredConnections())
            .SaveMembershipAsync(1, "[]");
        Assert.False(memDb.Succeeded);
        Assert.Equal("db", memDb.Code);

        var memParsed = CpStorageWriteService.ParseStoragesList(
            """[{"id":1,"checked":true,"time_to_shop":2,"groups":[{"id":2,"prices_ranges":[{"max_point":-1,"markup":5}]}]}]""");
        Assert.Null(memParsed.Error);
        Assert.Single(memParsed.Rows);
        Assert.Equal(1, memParsed.Rows[0].StorageId);
        Assert.Equal(2, memParsed.Rows[0].GroupId);
        Assert.Equal(0, memParsed.Rows[0].MinPoint);
        Assert.Equal(999999999999m, memParsed.Rows[0].MaxPoint);
        Assert.Equal(5, memParsed.Rows[0].Markup);
        Assert.Equal(2, memParsed.Rows[0].AdditionalTime);

        var memSkip = CpStorageWriteService.ParseStoragesList(
            """[{"id":1,"checked":false,"groups":[{"id":2,"prices_ranges":[{"max_point":10,"markup":1}]}]}]""");
        Assert.Null(memSkip.Error);
        Assert.Empty(memSkip.Rows);

        var officeCreate = await new CpOfficeWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new CpOfficeSaveRequest(Caption: "  "));
        Assert.False(officeCreate.Succeeded);
        Assert.Equal("invalid", officeCreate.Code);

        var officeEdit = await new CpOfficeWriteService(new ConfiguredNeverOpened())
            .UpdateAsync(new CpOfficeSaveRequest(OfficeId: 0, Caption: "Main"));
        Assert.False(officeEdit.Succeeded);
        Assert.Equal("invalid", officeEdit.Code);

        var officeUsers = await new CpOfficeWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new CpOfficeSaveRequest(Caption: "Main", UsersJson: "["));
        Assert.False(officeUsers.Succeeded);
        Assert.Equal("invalid", officeUsers.Code);

        var officeDb = await new CpOfficeWriteService(new UnconfiguredConnections())
            .CreateAsync(new CpOfficeSaveRequest(Caption: "Main", UsersJson: "1"));
        Assert.False(officeDb.Succeeded);
        Assert.Equal("db", officeDb.Code);

        var officeDelEmpty = await new CpOfficeWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("");
        Assert.False(officeDelEmpty.Succeeded);
        Assert.Equal("invalid", officeDelEmpty.Code);

        var officeDelJson = await new CpOfficeWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("{");
        Assert.False(officeDelJson.Succeeded);
        Assert.Equal("invalid", officeDelJson.Code);

        var officeDelDb = await new CpOfficeWriteService(new UnconfiguredConnections())
            .DeleteAsync("[1]");
        Assert.False(officeDelDb.Succeeded);
        Assert.Equal("db", officeDelDb.Code);

        var officeIds = CpOfficeWriteService.ParseOfficeIds("[1,2,2]");
        Assert.Null(officeIds.Error);
        Assert.Equal(new long[] { 1, 2 }, officeIds.Ids);
        Assert.Equal("Main office", CpOfficeWriteService.SanitizeField("Main office"));
        Assert.Contains("_1_", CpOfficeWriteService.NextStrKey("http://www.epartscart.com/", 1));

        var geoInvalid = await new CpOfficeWriteService(new ConfiguredNeverOpened())
            .SaveGeoAsync(0, "[]");
        Assert.False(geoInvalid.Succeeded);
        Assert.Equal("invalid", geoInvalid.Code);

        var geoBad = await new CpOfficeWriteService(new ConfiguredNeverOpened())
            .SaveGeoAsync(1, "[");
        Assert.False(geoBad.Succeeded);
        Assert.Equal("invalid", geoBad.Code);

        var geoDb = await new CpOfficeWriteService(new UnconfiguredConnections())
            .SaveGeoAsync(1, "[]");
        Assert.False(geoDb.Succeeded);
        Assert.Equal("db", geoDb.Code);

        var geoParsed = CpOfficeWriteService.ParseGeoIds("[1,2,2]");
        Assert.Null(geoParsed.Error);
        Assert.Equal(new long[] { 1, 2 }, geoParsed.Ids);
        Assert.Empty(CpOfficeWriteService.ParseGeoIds("").Ids);
        Assert.Empty(CpOfficeWriteService.ParseGeoIds("[]").Ids);

        var omAvail = await new CpObtainingModeWriteService(new ConfiguredNeverOpened())
            .SetAvailableAsync(0, 1);
        Assert.False(omAvail.Succeeded);
        Assert.Equal("invalid", omAvail.Code);

        var omSave = await new CpObtainingModeWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpObtainingModeSaveRequest(ModeId: 0, Caption: "Pickup"));
        Assert.False(omSave.Succeeded);
        Assert.Equal("invalid", omSave.Code);

        var omJson = await new CpObtainingModeWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpObtainingModeSaveRequest(ModeId: 1, Caption: "Pickup", ParametersValues: "{"));
        Assert.False(omJson.Succeeded);
        Assert.Equal("invalid", omJson.Code);

        var omDb = await new CpObtainingModeWriteService(new UnconfiguredConnections())
            .SetAvailableAsync(1, 1);
        Assert.False(omDb.Succeeded);
        Assert.Equal("db", omDb.Code);

        Assert.Equal("{}", CpObtainingModeWriteService.NormalizeParameters(null).Json);
        Assert.Null(CpObtainingModeWriteService.NormalizeParameters("""{"demo":"1"}""").Error);

        var geoTreeEmpty = await new CpGeoTreeWriteService(new ConfiguredNeverOpened())
            .SaveTreeAsync("[]", "en", "http://www.epartscart.com/");
        Assert.False(geoTreeEmpty.Succeeded);
        Assert.Equal("invalid", geoTreeEmpty.Code);

        var geoTreeBad = await new CpGeoTreeWriteService(new ConfiguredNeverOpened())
            .SaveTreeAsync("{", "en", "http://www.epartscart.com/");
        Assert.False(geoTreeBad.Succeeded);
        Assert.Equal("invalid", geoTreeBad.Code);

        var geoTreeDb = await new CpGeoTreeWriteService(new UnconfiguredConnections())
            .SaveTreeAsync("""[{"id":1,"level":1,"value":"UAE","from_server":1}]""", "en", "http://www.epartscart.com/");
        Assert.False(geoTreeDb.Succeeded);
        Assert.Equal("db", geoTreeDb.Code);

        var geoTreeParsed = CpGeoTreeWriteService.ParseTree(
            """[{"id":1,"value":"UAE","value_lang_str_id":"0","from_server":1,"$level":1,"$parent":0,"$count":1,"data":[{"id":2,"value":"Dubai","from_server":1,"$level":2,"$parent":1,"$count":0}]}]""");
        Assert.Null(geoTreeParsed.Error);
        Assert.Equal(2, geoTreeParsed.Nodes.Count);
        Assert.Equal(1, geoTreeParsed.Nodes[0].Id);
        Assert.Equal(1, geoTreeParsed.Nodes[0].Count);
        Assert.Equal(2, geoTreeParsed.Nodes[1].Id);
        Assert.Equal(1, geoTreeParsed.Nodes[1].Parent);
        Assert.Equal("UAE", geoTreeParsed.Nodes[0].Value);
        var geoTreeLinear = CpGeoTreeWriteService.ParseTree(
            """[{"id":1,"level":1,"value":"UAE","parent":0,"from_server":1,"count":0}]""");
        Assert.Null(geoTreeLinear.Error);
        Assert.Equal(1, geoTreeLinear.Nodes[0].Order);

        var tabAvail = await new CpSearchTabWriteService(new ConfiguredNeverOpened())
            .SetEnabledAsync(0, 1);
        Assert.False(tabAvail.Succeeded);
        Assert.Equal("invalid", tabAvail.Code);

        var tabSave = await new CpSearchTabWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpSearchTabSaveRequest(TabId: 0, Caption: "VIN"));
        Assert.False(tabSave.Succeeded);
        Assert.Equal("invalid", tabSave.Code);

        var tabJson = await new CpSearchTabWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpSearchTabSaveRequest(TabId: 1, Caption: "VIN", ParametersValues: "{"));
        Assert.False(tabJson.Succeeded);
        Assert.Equal("invalid", tabJson.Code);

        var tabDb = await new CpSearchTabWriteService(new UnconfiguredConnections())
            .SetEnabledAsync(1, 1);
        Assert.False(tabDb.Succeeded);
        Assert.Equal("db", tabDb.Code);

        Assert.Equal("{}", CpSearchTabWriteService.NormalizeParameters(null).Json);
        Assert.Null(CpSearchTabWriteService.NormalizeParameters("""{"demo":"1"}""").Error);
        Assert.Equal(1, CpSearchTabWriteService.ParseEnabled("tab_enabled"));
        Assert.Equal(0, CpSearchTabWriteService.ParseEnabled(""));
        Assert.Equal(1, CpSearchTabWriteService.ParseEnabled(null, 1));

        var extraEmpty = await new CpAdditionalTextWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpAdditionalTextSaveRequest(Url: ""));
        Assert.False(extraEmpty.Succeeded);
        Assert.Equal("invalid", extraEmpty.Code);

        var extraDelEmpty = await new CpAdditionalTextWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("");
        Assert.False(extraDelEmpty.Succeeded);
        Assert.Equal("invalid", extraDelEmpty.Code);

        var extraDelJson = await new CpAdditionalTextWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("{");
        Assert.False(extraDelJson.Succeeded);
        Assert.Equal("invalid", extraDelJson.Code);

        var extraDb = await new CpAdditionalTextWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpAdditionalTextSaveRequest(Url: "/shop/demo", Content: "Hi"));
        Assert.False(extraDb.Succeeded);
        Assert.Equal("db", extraDb.Code);

        Assert.Equal("[CODE]php[/CODE]", CpAdditionalTextWriteService.StripPhp("<?php?>"));
        Assert.Equal("Hello", CpAdditionalTextWriteService.HtmlEncode("Hello"));
        var extraIds = CpAdditionalTextWriteService.ParseIds("[1,2,2]");
        Assert.Null(extraIds.Error);
        Assert.Equal(new long[] { 1, 2 }, extraIds.Ids);

        var slideEmpty = await new CpSliderWriteService(new ConfiguredNeverOpened())
            .AddAsync("", "/shop");
        Assert.False(slideEmpty.Succeeded);
        Assert.Equal("invalid", slideEmpty.Code);

        var slideMove = await new CpSliderWriteService(new ConfiguredNeverOpened())
            .MoveAsync(0, true);
        Assert.False(slideMove.Succeeded);
        Assert.Equal("invalid", slideMove.Code);

        var slideDel = await new CpSliderWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(slideDel.Succeeded);
        Assert.Equal("invalid", slideDel.Code);

        var slideDb = await new CpSliderWriteService(new UnconfiguredConnections())
            .SaveSettingsAsync(1, 1, 1, 3);
        Assert.False(slideDb.Succeeded);
        Assert.Equal("db", slideDb.Code);

        var filtSave = await new CpProductFilterWriteService(new ConfiguredNeverOpened())
            .SaveAsync(0, "BOSCH", "0986", "Pad");
        Assert.False(filtSave.Succeeded);
        Assert.Equal("invalid", filtSave.Code);

        var filtDel = await new CpProductFilterWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(filtDel.Succeeded);
        Assert.Equal("invalid", filtDel.Code);

        var filtScope = await new CpProductFilterWriteService(new ConfiguredNeverOpened())
            .SaveStoragesAsync(1, "{", "10", "20", "1", "2");
        Assert.False(filtScope.Succeeded);
        Assert.Equal("invalid", filtScope.Code);

        var filtDb = await new CpProductFilterWriteService(new UnconfiguredConnections())
            .AddAsync("BOSCH", "0986-AB", "Pad");
        Assert.False(filtDb.Succeeded);
        Assert.Equal("db", filtDb.Code);

        Assert.Equal("0986AB", CpProductFilterWriteService.NormalizeArticle("0986-AB"));
        Assert.Equal("BOSCH", CpProductFilterWriteService.NormalizeManufacturer(" bosch "));
        var storages = CpProductFilterWriteService.ParseStorages("[1,\"2\",2]");
        Assert.Null(storages.Error);
        Assert.Equal("[1,2]", storages.Json);
        Assert.True(CpProductFilterWriteService.TryParseMoney("12,5", out var money, out _));
        Assert.Equal(12.5m, money);
        Assert.Equal(1, CpProductFilterWriteService.ParseFlag("on", 0));

        var stEmpty = CpOrderStatusWriteService.ParseOrderStatuses("");
        Assert.Equal("orders_statuses is required.", stEmpty.Error);
        var stBad = CpOrderStatusWriteService.ParseOrderStatuses("{");
        Assert.Equal("orders_statuses is not valid JSON.", stBad.Error);
        var stOk = CpOrderStatusWriteService.ParseOrderStatuses("""[{"id":1,"value":"New","color":"#00f","for_created":1,"created_earlier":1}]""");
        Assert.Null(stOk.Error);
        Assert.Equal("New", stOk.Rows[0].Name);
        Assert.Equal("#00f", stOk.Rows[0].Color);
        var itEmpty = CpOrderStatusWriteService.ParseItemStatuses("[]");
        Assert.Equal("At least one item status is required.", itEmpty.Error);
        var stDb = await new CpOrderStatusWriteService(new UnconfiguredConnections())
            .SaveAsync("""[{"value":"New"}]""", """[{"value":"Line"}]""", "en", "http://localhost/");
        Assert.False(stDb.Succeeded);
        Assert.Equal("db", stDb.Code);
        Assert.Equal("#777777", CpOrderStatusWriteService.NormalizeColor(""));

        Assert.Equal("[1,2]", CpStorageWriteService.NormalizeUsers("1,2").Json);
        Assert.Equal("[]", CpStorageWriteService.NormalizeUsers(null).Json);
        var opts = CpStorageWriteService.NormalizeConnectionOptions(
            """{"probability":"95 %","subdomain":"HTTPS://Shop.public.api.abcp.ru/"}""",
            "abcp");
        Assert.Null(opts.Error);
        Assert.Contains("\"probability\":\"95\"", opts.Json);
        Assert.Contains("\"subdomain\":\"shop\"", opts.Json);

        var doneInvalid = await new CpPricesUploadWriteService(new ConfiguredNeverOpened())
            .CompleteSessionAsync(0);
        Assert.False(doneInvalid.Succeeded);
        Assert.Equal("invalid", doneInvalid.Code);

        var doneDb = await new CpPricesUploadWriteService(new UnconfiguredConnections())
            .CompleteSessionAsync(9);
        Assert.False(doneDb.Succeeded);
        Assert.Equal("db", doneDb.Code);

        Assert.True(CpPricesUploadWriteService.ParseRestrict(null));
        Assert.True(CpPricesUploadWriteService.ParseRestrict("1"));
        Assert.False(CpPricesUploadWriteService.ParseRestrict("0"));
        Assert.False(CpPricesUploadWriteService.ParseRestrict("off"));
        Assert.Equal(new[] { 2L, 5L }, CpPricesUploadWriteService.ParseIdList("2,5"));
        Assert.Equal(new[] { 12L, 18L }, CpPricesUploadWriteService.ParseIdList("[12,18]"));

        var aclDb = await new CpPricesUploadWriteService(new UnconfiguredConnections())
            .SaveMinPriceAclAsync("1", "2,5", "12", 1);
        Assert.False(aclDb.Succeeded);
        Assert.Equal("db", aclDb.Code);

        Assert.Equal("price_list", CpPricesUploadWriteService.ParseEntityType("price_list"));
        Assert.Equal("storage", CpPricesUploadWriteService.ParseEntityType(""));
        Assert.True(CpPricesUploadWriteService.ParseStorefrontEnabled("1"));
        Assert.False(CpPricesUploadWriteService.ParseStorefrontEnabled("0"));

        var toggleInvalid = await new CpPricesUploadWriteService(new ConfiguredNeverOpened())
            .SetStorefrontToggleAsync("storage", 0, "1", 1, "admin");
        Assert.False(toggleInvalid.Succeeded);
        Assert.Equal("invalid", toggleInvalid.Code);

        var toggleDb = await new CpPricesUploadWriteService(new UnconfiguredConnections())
            .SetStorefrontToggleAsync("storage", 9, "1", 1, "admin");
        Assert.False(toggleDb.Succeeded);
        Assert.Equal("db", toggleDb.Code);

        Assert.Equal("ACME", CpPricesUploadWriteService.SanitizeShort(" ACME/# "));
        Assert.Equal("", CpPricesUploadWriteService.SanitizeShort("///"));

        var mvVendorInvalid = await new CpPricesUploadWriteService(new ConfiguredNeverOpened())
            .SaveVendorCodeAsync(0, "ACME", "Acme");
        Assert.False(mvVendorInvalid.Succeeded);
        Assert.Equal("invalid", mvVendorInvalid.Code);

        var mvVendorEmpty = await new CpPricesUploadWriteService(new ConfiguredNeverOpened())
            .SaveVendorCodeAsync(9, "///", "");
        Assert.False(mvVendorEmpty.Succeeded);
        Assert.Equal("invalid", mvVendorEmpty.Code);

        var mvVendorDb = await new CpPricesUploadWriteService(new UnconfiguredConnections())
            .SaveVendorCodeAsync(9, "ACME", "Acme");
        Assert.False(mvVendorDb.Succeeded);
        Assert.Equal("db", mvVendorDb.Code);

        Assert.True(CpPartsAgentWriteService.ParseEnabled("1"));
        Assert.False(CpPartsAgentWriteService.ParseEnabled("0"));
        Assert.False(CpPartsAgentWriteService.ParseEnabled("yes"));

        var agentDb = await new CpPartsAgentWriteService(new UnconfiguredConnections())
            .SaveConfigAsync("1", "Parts", "", "", "", "", "", "", "");
        Assert.False(agentDb.Succeeded);
        Assert.Equal("db", agentDb.Code);

        var bosNotifMissing = await new BosNotificationWriteService(new ConfiguredNeverOpened())
            .MarkReadAsync("", "__platform__");
        Assert.False(bosNotifMissing.Succeeded);
        Assert.Equal("invalid", bosNotifMissing.Code);
        Assert.Equal(BosNotificationWriteService.MissingIdsMessage, bosNotifMissing.Message);

        var bosNotifEmptyJson = await new BosNotificationWriteService(new ConfiguredNeverOpened())
            .MarkReadAsync("[]", "__platform__");
        Assert.False(bosNotifEmptyJson.Succeeded);
        Assert.Equal("invalid", bosNotifEmptyJson.Code);

        var bosNotifDb = await new BosNotificationWriteService(new UnconfiguredConnections())
            .MarkReadAsync("[1]", "__platform__");
        Assert.False(bosNotifDb.Succeeded);
        Assert.Equal("db", bosNotifDb.Code);

        var bosDismissDb = await new BosNotificationWriteService(new UnconfiguredConnections())
            .DismissAsync(9, "__platform__");
        Assert.False(bosDismissDb.Succeeded);
        Assert.Equal("db", bosDismissDb.Code);

        var quoteNote = await new CpQuoteWriteService(new ConfiguredNeverOpened())
            .SaveAdminNoteAsync(0, "note");
        Assert.False(quoteNote.Succeeded);
        Assert.Equal("invalid", quoteNote.Code);

        var quoteSend = await new CpQuoteWriteService(new ConfiguredNeverOpened())
            .SendQuoteAsync(0);
        Assert.False(quoteSend.Succeeded);
        Assert.Equal("invalid", quoteSend.Code);

        var quoteDb = await new CpQuoteWriteService(new UnconfiguredConnections())
            .SaveAdminNoteAsync(9, "note");
        Assert.False(quoteDb.Succeeded);
        Assert.Equal("db", quoteDb.Code);

        var quoteLinesEmpty = await new CpQuoteWriteService(new ConfiguredNeverOpened())
            .SaveLinesAsync(9, "note", "[]");
        Assert.False(quoteLinesEmpty.Succeeded);
        Assert.Equal("invalid", quoteLinesEmpty.Code);

        var quoteLinesAlt = CpQuoteWriteService.ParseLines("""[{"id":1,"offerAlternative":true,"altManufacturer":"","altArticle":"ABC"}]""");
        Assert.Equal("Alternative offer on line #1 needs brand and article", quoteLinesAlt.Error);

        var quoteLinesDb = await new CpQuoteWriteService(new UnconfiguredConnections())
            .SaveLinesAsync(9, "note", """[{"id":1,"quotedPrice":12.5}]""");
        Assert.False(quoteLinesDb.Succeeded);
        Assert.Equal("db", quoteLinesDb.Code);

        var vendorInvalid = await new CpVendorApprovalWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(0, "approve");
        Assert.False(vendorInvalid.Succeeded);
        Assert.Equal("invalid", vendorInvalid.Code);

        var vendorAction = await new CpVendorApprovalWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(vendorAction.Succeeded);
        Assert.Equal("invalid", vendorAction.Code);

        var vendorDb = await new CpVendorApprovalWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "approve");
        Assert.False(vendorDb.Succeeded);
        Assert.Equal("db", vendorDb.Code);

        Assert.Equal("PadWH1", CpVendorApprovalWriteService.SanitizeShort(" Pad/WH#1 "));
        Assert.Equal("Pad warehouse", CpVendorApprovalWriteService.SanitizeFull("  Pad   warehouse  "));
        Assert.Equal("PADWH · Pad warehouse", CpVendorApprovalWriteService.ListBaseName("PADWH", "Pad warehouse"));
        Assert.Equal("EPC_VENDOR", CpVendorApprovalWriteService.VendorGroupKey);

        var apiInvalid = await new CpApiClientWriteService(new ConfiguredNeverOpened())
            .SetActiveAsync(0, 1);
        Assert.False(apiInvalid.Succeeded);
        Assert.Equal("invalid", apiInvalid.Code);

        var apiDb = await new CpApiClientWriteService(new UnconfiguredConnections())
            .SetActiveAsync(9, 0);
        Assert.False(apiDb.Succeeded);
        Assert.Equal("db", apiDb.Code);

        var ruleInvalid = await new CpPriceStorageRuleWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("nope", 9);
        Assert.False(ruleInvalid.Succeeded);
        Assert.Equal("invalid", ruleInvalid.Code);

        var ruleDb = await new CpPriceStorageRuleWriteService(new UnconfiguredConnections())
            .DeleteAsync("delete_storage_rule", 9);
        Assert.False(ruleDb.Succeeded);
        Assert.Equal("db", ruleDb.Code);

        var ruleSaveInvalid = await new CpPriceStorageRuleWriteService(new ConfiguredNeverOpened())
            .ApplyAsync("save_storage_rule", 0, 0, null, null, "10", 1);
        Assert.False(ruleSaveInvalid.Succeeded);
        Assert.Equal("invalid", ruleSaveInvalid.Code);

        var ruleSaveMargin = await new CpPriceStorageRuleWriteService(new ConfiguredNeverOpened())
            .ApplyAsync("save_storage_brand_rule", 0, 4, "bosch", null, "2000", 1);
        Assert.False(ruleSaveMargin.Succeeded);
        Assert.Equal("invalid", ruleSaveMargin.Code);

        var ruleSaveDb = await new CpPriceStorageRuleWriteService(new UnconfiguredConnections())
            .ApplyAsync("save_storage_rule", 0, 4, null, null, "10,5", 1);
        Assert.False(ruleSaveDb.Succeeded);
        Assert.Equal("db", ruleSaveDb.Code);

        var cashInvalid = await new ErpOfficesCashWriteService(new ConfiguredNeverOpened())
            .AddEntryAsync(1, 0, 1, 10, 3, "note");
        Assert.False(cashInvalid.Succeeded);
        Assert.Equal("invalid", cashInvalid.Code);

        var cashAmount = await new ErpOfficesCashWriteService(new ConfiguredNeverOpened())
            .AddEntryAsync(1, 2, 1, 0, 3, "note");
        Assert.False(cashAmount.Succeeded);
        Assert.Equal("invalid", cashAmount.Code);

        var cashDb = await new ErpOfficesCashWriteService(new UnconfiguredConnections())
            .AddEntryAsync(1, 2, 1, 10, 3, "note");
        Assert.False(cashDb.Succeeded);
        Assert.Equal("db", cashDb.Code);

        var addCodeName = await new ErpOfficesCashWriteService(new ConfiguredNeverOpened())
            .AddCodeAsync(1, 2, 1, "", null, "en", "http://www.epartscart.com/");
        Assert.False(addCodeName.Succeeded);
        Assert.Equal("invalid", addCodeName.Code);

        var addCodeOffice = await new ErpOfficesCashWriteService(new ConfiguredNeverOpened())
            .AddCodeAsync(1, 0, 1, "Sale", null, "en", "http://www.epartscart.com/");
        Assert.False(addCodeOffice.Succeeded);
        Assert.Equal("invalid", addCodeOffice.Code);

        var addCodeDb = await new ErpOfficesCashWriteService(new UnconfiguredConnections())
            .AddCodeAsync(1, 2, 1, "Sale", null, "en", "http://www.epartscart.com/");
        Assert.False(addCodeDb.Succeeded);
        Assert.Equal("db", addCodeDb.Code);

        Assert.Equal("Sale", ErpOfficesCashWriteService.SanitizeCaption(" Sale\n"));
        Assert.Contains("_1_", ErpOfficesCashWriteService.NextStrKey("http://www.epartscart.com/", 1), StringComparison.Ordinal);

        var cashCodeInvalid = await new ErpOfficesCashWriteService(new ConfiguredNeverOpened())
            .DeleteCodeAsync(1, 2, 0);
        Assert.False(cashCodeInvalid.Succeeded);
        Assert.Equal("invalid", cashCodeInvalid.Code);

        var cashCodeDb = await new ErpOfficesCashWriteService(new UnconfiguredConnections())
            .DeleteCodeAsync(1, 2, 9);
        Assert.False(cashCodeDb.Succeeded);
        Assert.Equal("db", cashCodeDb.Code);

        var contentInvalid = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SetPublishedAsync(0, 1);
        Assert.False(contentInvalid.Succeeded);
        Assert.Equal("invalid", contentInvalid.Code);

        var contentDb = await new CpContentManagerWriteService(new UnconfiguredConnections())
            .SetPublishedAsync(9, 1);
        Assert.False(contentDb.Succeeded);
        Assert.Equal("db", contentDb.Code);

        var contentMainDb = await new CpContentManagerWriteService(new UnconfiguredConnections())
            .SetMainAsync(9, 1);
        Assert.False(contentMainDb.Succeeded);
        Assert.Equal("db", contentMainDb.Code);

        var contentBodyId = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SaveBodyAsync(new CpContentBodySaveRequest(0, "text", "Hello"));
        Assert.False(contentBodyId.Succeeded);
        Assert.Equal("invalid", contentBodyId.Code);

        var contentBodyType = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SaveBodyAsync(new CpContentBodySaveRequest(9, "html", "Hello"));
        Assert.False(contentBodyType.Succeeded);
        Assert.Equal("invalid", contentBodyType.Code);

        var contentBodyPhp = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SaveBodyAsync(new CpContentBodySaveRequest(9, "php", "not-a-php-path"));
        Assert.False(contentBodyPhp.Succeeded);
        Assert.Equal("invalid", contentBodyPhp.Code);

        var contentBodyDb = await new CpContentManagerWriteService(new UnconfiguredConnections())
            .SaveBodyAsync(new CpContentBodySaveRequest(9, "text", "<?php?>"));
        Assert.False(contentBodyDb.Succeeded);
        Assert.Equal("db", contentBodyDb.Code);

        Assert.True(CpContentManagerWriteService.TryNormalizeType("PHP", out var bodyType));
        Assert.Equal("php", bodyType);
        Assert.False(CpContentManagerWriteService.TryNormalizeType("html", out _));
        Assert.True(CpContentManagerWriteService.TryNormalizePhpPath("templates/page.php", out var phpPath, out _));
        Assert.Equal("templates/page.php", phpPath);
        Assert.False(CpContentManagerWriteService.TryNormalizePhpPath("../x.php", out _, out _));
        Assert.Equal("[CODE]php[/CODE]", CpContentManagerWriteService.StripPhp("<?php?>"));

        var contentMetaAlias = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SaveMetaAsync(new CpContentMetaSaveRequest(Alias: "", ContentType: "text", Content: "Hi"));
        Assert.False(contentMetaAlias.Succeeded);
        Assert.Equal("invalid", contentMetaAlias.Code);

        var contentMetaType = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SaveMetaAsync(new CpContentMetaSaveRequest(Alias: "about", ContentType: "html", Content: "Hi"));
        Assert.False(contentMetaType.Succeeded);
        Assert.Equal("invalid", contentMetaType.Code);

        var contentMetaPhp = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SaveMetaAsync(new CpContentMetaSaveRequest(Alias: "about", ContentType: "php", Content: "not-php"));
        Assert.False(contentMetaPhp.Succeeded);
        Assert.Equal("invalid", contentMetaPhp.Code);

        var contentMetaHash = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SaveMetaAsync(new CpContentMetaSaveRequest(Alias: "about", ContentType: "text", Content: "Hi", CheckHash: "deadbeef", SecretSuccession: "secret"));
        Assert.False(contentMetaHash.Succeeded);
        Assert.Equal("invalid", contentMetaHash.Code);

        var contentMetaDb = await new CpContentManagerWriteService(new UnconfiguredConnections())
            .SaveMetaAsync(new CpContentMetaSaveRequest(Alias: "about", ContentType: "text", Content: "Hi"));
        Assert.False(contentMetaDb.Succeeded);
        Assert.Equal("db", contentMetaDb.Code);

        Assert.True(CpContentManagerWriteService.TryNormalizeAlias("about-us", out var alias, out _));
        Assert.Equal("about-us", alias);
        Assert.False(CpContentManagerWriteService.TryNormalizeAlias("a/b", out _, out _));
        var groups = CpContentManagerWriteService.ParseGroups("[1,\"2\",2]");
        Assert.Null(groups.Error);
        Assert.Equal(new long[] { 1, 2 }, groups.Ids);
        Assert.Equal("groups_access is not valid.", CpContentManagerWriteService.ParseGroups("{").Error);
        Assert.Equal(32, CpContentManagerWriteService.ComputeCheckHash(0, 1, "secret").Length);

        var treeEmpty = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SaveTreeAsync(new CpContentTreeSaveRequest(TreeJson: ""));
        Assert.False(treeEmpty.Succeeded);
        Assert.Equal("invalid", treeEmpty.Code);

        var treeBad = await new CpContentManagerWriteService(new ConfiguredNeverOpened())
            .SaveTreeAsync(new CpContentTreeSaveRequest(TreeJson: "{"));
        Assert.False(treeBad.Succeeded);
        Assert.Equal("invalid", treeBad.Code);

        var treeDb = await new CpContentManagerWriteService(new UnconfiguredConnections())
            .SaveTreeAsync(new CpContentTreeSaveRequest(TreeJson: """[{"id":9,"alias":"home","value":"Home","$count":0,"$level":1,"$parent":0}]"""));
        Assert.False(treeDb.Succeeded);
        Assert.Equal("db", treeDb.Code);

        var treeOk = CpContentManagerWriteService.ParseTree("""[{"id":9,"alias":"Home","value":"Home","$count":0,"$level":1,"$parent":0,"groups_access":[3]}]""");
        Assert.Null(treeOk.Error);
        Assert.Equal(9, treeOk.Nodes[0].Id);
        Assert.Equal("home", treeOk.Nodes[0].Alias);
        Assert.Equal(new long[] { 3 }, treeOk.Nodes[0].Groups);
        Assert.Equal("tree_json is not valid JSON.", CpContentManagerWriteService.ParseTree("{").Error);

        var menuAction = await new CpMenuWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpMenuSaveRequest(Action: "rename", Caption: "Main"));
        Assert.False(menuAction.Succeeded);
        Assert.Equal("invalid", menuAction.Code);

        var menuUpdateId = await new CpMenuWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpMenuSaveRequest(Action: "update", MenuId: 0, Caption: "Main", TreeJson: "[]"));
        Assert.False(menuUpdateId.Succeeded);
        Assert.Equal("invalid", menuUpdateId.Code);

        var menuTree = await new CpMenuWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpMenuSaveRequest(Action: "create", Caption: "Main", TreeJson: "{"));
        Assert.False(menuTree.Succeeded);
        Assert.Equal("invalid", menuTree.Code);

        var menuDb = await new CpMenuWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpMenuSaveRequest(Action: "create", Caption: "Main", TreeJson: "[]"));
        Assert.False(menuDb.Succeeded);
        Assert.Equal("db", menuDb.Code);

        var menuDelEmpty = await new CpMenuWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("");
        Assert.False(menuDelEmpty.Succeeded);
        Assert.Equal("invalid", menuDelEmpty.Code);

        var menuDelJson = await new CpMenuWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("{");
        Assert.False(menuDelJson.Succeeded);
        Assert.Equal("invalid", menuDelJson.Code);

        var menuDelDb = await new CpMenuWriteService(new UnconfiguredConnections())
            .DeleteAsync("[1]");
        Assert.False(menuDelDb.Succeeded);
        Assert.Equal("db", menuDelDb.Code);

        Assert.Equal("create", CpMenuWriteService.NormalizeAction("save_create"));
        Assert.Equal("update", CpMenuWriteService.NormalizeAction("save_update"));
        Assert.Equal("Hello", CpMenuWriteService.HtmlEncode("Hello"));
        Assert.Equal("&lt;b&gt;", CpMenuWriteService.HtmlEncode("<b>"));
        var emptyTree = CpMenuWriteService.TryParseTree("");
        Assert.Null(emptyTree.Error);
        Assert.NotNull(emptyTree.Tree);
        Assert.Equal("menu_tree is not valid JSON.", CpMenuWriteService.TryParseTree("{").Error);
        var okTree = CpMenuWriteService.TryParseTree("""[{"value":"Home","link_mode":"url","href":"/"}]""");
        Assert.Null(okTree.Error);
        var menuIds = CpMenuWriteService.ParseIds("[1,2,2]");
        Assert.Null(menuIds.Error);
        Assert.Equal(new long[] { 1, 2 }, menuIds.Ids);
        Assert.Equal("menu_list JSON is not valid.", CpMenuWriteService.ParseIds("{").Error);

        var moduleAction = await new CpModuleWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpModuleSaveRequest(Action: "rename", ContentType: "text", Content: "Hi"));
        Assert.False(moduleAction.Succeeded);
        Assert.Equal("invalid", moduleAction.Code);

        var moduleType = await new CpModuleWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpModuleSaveRequest(Action: "create", ContentType: "html", Content: "Hi"));
        Assert.False(moduleType.Succeeded);
        Assert.Equal("invalid", moduleType.Code);

        var modulePhp = await new CpModuleWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpModuleSaveRequest(Action: "create", ContentType: "php", Content: "not-php"));
        Assert.False(modulePhp.Succeeded);
        Assert.Equal("invalid", modulePhp.Code);

        var moduleData = await new CpModuleWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpModuleSaveRequest(Action: "create", ContentType: "text", Content: "Hi", DataJson: "{"));
        Assert.False(moduleData.Succeeded);
        Assert.Equal("invalid", moduleData.Code);

        var moduleDb = await new CpModuleWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpModuleSaveRequest(Action: "create", ContentType: "text", Content: "Hi", DataJson: "[]"));
        Assert.False(moduleDb.Succeeded);
        Assert.Equal("db", moduleDb.Code);

        var moduleActEmpty = await new CpModuleWriteService(new ConfiguredNeverOpened())
            .SetActivatedAsync("", 1);
        Assert.False(moduleActEmpty.Succeeded);
        Assert.Equal("invalid", moduleActEmpty.Code);

        var moduleDelDb = await new CpModuleWriteService(new UnconfiguredConnections())
            .DeleteAsync("[1]", 1);
        Assert.False(moduleDelDb.Succeeded);
        Assert.Equal("db", moduleDelDb.Code);

        Assert.Equal("create", CpModuleWriteService.NormalizeAction("module_create"));
        Assert.Equal("[]", CpModuleWriteService.TryEncodeData("").Json);
        Assert.Equal("data_value is not valid JSON.", CpModuleWriteService.TryEncodeData("{").Error);
        Assert.Equal("modules_list JSON is not valid.", CpModuleWriteService.ParseIds("{").Error);

        var lineAction = await new CpLineListWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpLineListSaveRequest(Action: "rename", Caption: "Colors"));
        Assert.False(lineAction.Succeeded);
        Assert.Equal("invalid", lineAction.Code);

        var lineEditId = await new CpLineListWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpLineListSaveRequest(Action: "edit", ListId: 0, Caption: "Colors", ItemsJson: "[]"));
        Assert.False(lineEditId.Succeeded);
        Assert.Equal("invalid", lineEditId.Code);

        var lineItems = await new CpLineListWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpLineListSaveRequest(Action: "create", Caption: "Colors", ItemsJson: "{"));
        Assert.False(lineItems.Succeeded);
        Assert.Equal("invalid", lineItems.Code);

        var lineDb = await new CpLineListWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpLineListSaveRequest(Action: "create", Caption: "Colors", ItemsJson: """[{"value":"Red"}]"""));
        Assert.False(lineDb.Succeeded);
        Assert.Equal("db", lineDb.Code);

        var lineDelEmpty = await new CpLineListWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("");
        Assert.False(lineDelEmpty.Succeeded);
        Assert.Equal("invalid", lineDelEmpty.Code);

        var lineDelMfr = await new CpLineListWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("[10]");
        Assert.False(lineDelMfr.Succeeded);
        Assert.Equal("invalid", lineDelMfr.Code);

        var lineDelJson = await new CpLineListWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("{");
        Assert.False(lineDelJson.Succeeded);
        Assert.Equal("invalid", lineDelJson.Code);

        var lineDelDb = await new CpLineListWriteService(new UnconfiguredConnections())
            .DeleteAsync("[1]");
        Assert.False(lineDelDb.Succeeded);
        Assert.Equal("db", lineDelDb.Code);

        Assert.Equal("create", CpLineListWriteService.NormalizeAction("save_create"));
        Assert.Equal("edit", CpLineListWriteService.NormalizeAction("update"));
        Assert.Equal("delete", CpLineListWriteService.NormalizeAction("delete_line_lists"));
        Assert.Equal("Hello", CpLineListWriteService.HtmlEncode("Hello"));
        Assert.Equal("&lt;b&gt;", CpLineListWriteService.HtmlEncode("<b>"));
        var emptyItems = CpLineListWriteService.ParseItems("");
        Assert.Null(emptyItems.Error);
        Assert.Empty(emptyItems.Items);
        Assert.Equal("tree_json must be a JSON array.", CpLineListWriteService.ParseItems("{").Error);
        Assert.Equal("tree_json is not valid JSON.", CpLineListWriteService.ParseItems("[").Error);
        var okItems = CpLineListWriteService.ParseItems("""[{"id":2,"value":"Red","value_lang_str_id":"k","is_new":0},{"value":"Blue","is_new":1}]""");
        Assert.Null(okItems.Error);
        Assert.Equal(2, okItems.Items.Count);
        Assert.Equal(2, okItems.Items[0].Id);
        Assert.False(okItems.Items[0].IsNew);
        Assert.True(okItems.Items[1].IsNew);
        var lineIds = CpLineListWriteService.ParseIds("[1,2,2]");
        Assert.Null(lineIds.Error);
        Assert.Equal(new long[] { 1, 2 }, lineIds.Ids);
        Assert.Equal("line_lists JSON is not valid.", CpLineListWriteService.ParseIds("{").Error);

        var treeListAction = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpTreeListSaveRequest(Action: "rename", Caption: "Makes"));
        Assert.False(treeListAction.Succeeded);
        Assert.Equal("invalid", treeListAction.Code);

        var treeListEditId = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpTreeListSaveRequest(Action: "edit", ListId: 0, Caption: "Makes", TreeJson: "[]"));
        Assert.False(treeListEditId.Succeeded);
        Assert.Equal("invalid", treeListEditId.Code);

        var treeListJsonBad = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpTreeListSaveRequest(Action: "create", Caption: "Makes", TreeJson: "{"));
        Assert.False(treeListJsonBad.Succeeded);
        Assert.Equal("invalid", treeListJsonBad.Code);

        var treeListDb = await new CpTreeListWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpTreeListSaveRequest(Action: "create", Caption: "Makes", TreeJson: """[{"id":1,"value":"Root","$count":0,"$level":1,"$parent":0}]"""));
        Assert.False(treeListDb.Succeeded);
        Assert.Equal("db", treeListDb.Code);

        var treeListDelEmpty = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("");
        Assert.False(treeListDelEmpty.Succeeded);
        Assert.Equal("invalid", treeListDelEmpty.Code);

        var treeListDelJson = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .DeleteAsync("{");
        Assert.False(treeListDelJson.Succeeded);
        Assert.Equal("invalid", treeListDelJson.Code);

        var treeListDelDb = await new CpTreeListWriteService(new UnconfiguredConnections())
            .DeleteAsync("[1]");
        Assert.False(treeListDelDb.Succeeded);
        Assert.Equal("db", treeListDelDb.Code);

        Assert.Equal("create", CpTreeListWriteService.NormalizeAction("save_create"));
        Assert.Equal("edit", CpTreeListWriteService.NormalizeAction("update"));
        Assert.Equal("delete", CpTreeListWriteService.NormalizeAction("delete_tree_lists"));
        Assert.Equal("&lt;b&gt;", CpTreeListWriteService.HtmlEncode("<b>"));
        var emptyTreeList = CpTreeListWriteService.ParseTree("");
        Assert.Null(emptyTreeList.Error);
        Assert.Empty(emptyTreeList.Nodes);
        Assert.Equal("tree_json is not valid JSON.", CpTreeListWriteService.ParseTree("{").Error);
        Assert.Equal("Each tree item needs a positive id.", CpTreeListWriteService.ParseTree("""[{"value":"Root"}]""").Error);
        var okTreeList = CpTreeListWriteService.ParseTree("""[{"id":1,"value":"Root","$count":1,"$level":1,"$parent":0,"data":[{"id":2,"value":"Child","$count":0,"$level":2,"$parent":1,"is_new":1}]}]""");
        Assert.Null(okTreeList.Error);
        Assert.Equal(2, okTreeList.Nodes.Count);
        Assert.Equal(1, okTreeList.Nodes[0].Id);
        Assert.Equal(1, okTreeList.Nodes[0].Count);
        Assert.Equal(2, okTreeList.Nodes[1].Id);
        Assert.True(okTreeList.Nodes[1].IsNew);
        Assert.Equal("root.png", CpTreeListWriteService.SanitizeImageName("../root.png"));
        Assert.True(CpTreeListWriteService.HasAllowedImageExtension("root.png"));
        var treeImg = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpTreeListSaveRequest(
                Action: "create",
                Caption: "Makes",
                TreeJson: """[{"id":1,"value":"Root","$count":0,"$level":1,"$parent":0,"image":"root.exe"}]"""));
        Assert.False(treeImg.Succeeded);
        Assert.Equal("invalid", treeImg.Code);
        var okTreeImg = CpTreeListWriteService.ParseTree("""[{"id":1,"value":"Root","$count":0,"$level":1,"$parent":0,"image":"root.png"}]""");
        Assert.Null(okTreeImg.Error);
        Assert.Equal("root.png", okTreeImg.Nodes[0].Image);
        var treeListIds = CpTreeListWriteService.ParseIds("[1,2,2]");
        Assert.Null(treeListIds.Error);
        Assert.Equal(new long[] { 1, 2 }, treeListIds.Ids);
        Assert.Equal("tree_lists JSON is not valid.", CpTreeListWriteService.ParseIds("{").Error);

        var brunchAction = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .SaveBranchAsync(new CpTreeListBranchSaveRequest(Action: "rename", Caption: "Branch"));
        Assert.False(brunchAction.Succeeded);
        Assert.Equal("invalid", brunchAction.Code);

        var brunchEditId = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .SaveBranchAsync(new CpTreeListBranchSaveRequest(Action: "branch_edit", ListId: 0, Caption: "Branch", ItemsJson: "[]"));
        Assert.False(brunchEditId.Succeeded);
        Assert.Equal("invalid", brunchEditId.Code);

        var brunchJsonBad = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .SaveBranchAsync(new CpTreeListBranchSaveRequest(Action: "branch_create", Caption: "Branch", ItemsJson: "{"));
        Assert.False(brunchJsonBad.Succeeded);
        Assert.Equal("invalid", brunchJsonBad.Code);

        var brunchDb = await new CpTreeListWriteService(new UnconfiguredConnections())
            .SaveBranchAsync(new CpTreeListBranchSaveRequest(Action: "branch_create", Caption: "Branch", ItemsJson: """[{"id":1,"value":"Sib"}]"""));
        Assert.False(brunchDb.Succeeded);
        Assert.Equal("db", brunchDb.Code);

        Assert.Equal("branch_create", CpTreeListWriteService.NormalizeAction("brunch_create"));
        Assert.Equal("branch_edit", CpTreeListWriteService.NormalizeAction("save_branch_edit"));
        var emptyBrunch = CpTreeListWriteService.ParseBranchItems("");
        Assert.Null(emptyBrunch.Error);
        Assert.Empty(emptyBrunch.Items);
        Assert.Equal("tree_json must be a JSON array.", CpTreeListWriteService.ParseBranchItems("{").Error);
        Assert.Equal("Each brunch item needs a positive id.", CpTreeListWriteService.ParseBranchItems("""[{"value":"Sib"}]""").Error);
        var okBrunch = CpTreeListWriteService.ParseBranchItems("""[{"id":5,"value":"Sib","alias":"sib","url":"/sib","is_new":1}]""");
        var brunchImg = await new CpTreeListWriteService(new ConfiguredNeverOpened())
            .SaveBranchAsync(new CpTreeListBranchSaveRequest(
                Action: "branch_create",
                Caption: "Branch",
                ItemsJson: """[{"id":1,"value":"Sib","image":"sib.exe"}]"""));
        Assert.False(brunchImg.Succeeded);
        Assert.Equal("invalid", brunchImg.Code);
        var okBrunchImg = CpTreeListWriteService.ParseBranchItems("""[{"id":5,"value":"Sib","image":"sib.png"}]""");
        Assert.Null(okBrunchImg.Error);
        Assert.Equal("sib.png", okBrunchImg.Items[0].Image);
        Assert.Null(okBrunch.Error);
        Assert.Equal(5, okBrunch.Items[0].Id);
        Assert.True(okBrunch.Items[0].IsNew);
        Assert.Equal("sib", okBrunch.Items[0].Alias);

        var wmsInvalid = await new ErpWmsLocationWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(wmsInvalid.Succeeded);
        Assert.Equal("invalid", wmsInvalid.Code);

        var wmsDb = await new ErpWmsLocationWriteService(new UnconfiguredConnections())
            .DeleteAsync(4);
        Assert.False(wmsDb.Succeeded);
        Assert.Equal("db", wmsDb.Code);

        var wmsSaveInvalid = await new ErpWmsLocationWriteService(new ConfiguredNeverOpened())
            .SaveAsync("  ", "MAIN", "", "pick", 0, 1, 0, 0);
        Assert.False(wmsSaveInvalid.Succeeded);
        Assert.Equal("invalid", wmsSaveInvalid.Code);

        var wmsSaveDb = await new ErpWmsLocationWriteService(new UnconfiguredConnections())
            .SaveAsync("A-01-01", "MAIN", "A", "pick", 0, 1, 0, 0);
        Assert.False(wmsSaveDb.Succeeded);
        Assert.Equal("db", wmsSaveDb.Code);

        var brandMissing = await new CpPriceStorageRuleWriteService(new ConfiguredNeverOpened())
            .ApplyAsync("save_storage_article_rule", 0, 4, "bosch", "---", "10", 1);
        Assert.False(brandMissing.Succeeded);
        Assert.Equal("invalid", brandMissing.Code);

        var subInvalid = await new ErpSubscriptionStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(0, "active");
        Assert.False(subInvalid.Succeeded);
        Assert.Equal("invalid", subInvalid.Code);

        var subStatus = await new ErpSubscriptionStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "expired");
        Assert.False(subStatus.Succeeded);
        Assert.Equal("invalid", subStatus.Code);

        var subDb = await new ErpSubscriptionStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "paused");
        Assert.False(subDb.Succeeded);
        Assert.Equal("db", subDb.Code);

        var subSaveInvalid = await new ErpSubscriptionSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync("SUB-1", "", "", 0, "AED", "monthly", 12, null, 0);
        Assert.False(subSaveInvalid.Succeeded);
        Assert.Equal("invalid", subSaveInvalid.Code);

        var subSaveDb = await new ErpSubscriptionSaveWriteService(new UnconfiguredConnections())
            .SaveAsync("SUB-1", "Acme", "Pro", 99, "AED", "monthly", 12, null, 0);
        Assert.False(subSaveDb.Succeeded);
        Assert.Equal("db", subSaveDb.Code);

        var ctrInvalid = await new ErpContractStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(ctrInvalid.Succeeded);
        Assert.Equal("invalid", ctrInvalid.Code);

        var ctrDb = await new ErpContractStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "active");
        Assert.False(ctrDb.Succeeded);
        Assert.Equal("db", ctrDb.Code);

        var ctrSaveInvalid = await new ErpContractSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync("CTR-1", "", "", 0, "AED", null, null, null, 0);
        Assert.False(ctrSaveInvalid.Succeeded);
        Assert.Equal("invalid", ctrSaveInvalid.Code);

        var ctrSaveDb = await new ErpContractSaveWriteService(new UnconfiguredConnections())
            .SaveAsync("CTR-1", "MSA", "Acme", 10, "AED", null, null, null, 0);
        Assert.False(ctrSaveDb.Succeeded);
        Assert.Equal("db", ctrSaveDb.Code);

        var wfInvalid = await new ErpWorkflowStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(wfInvalid.Succeeded);
        Assert.Equal("invalid", wfInvalid.Code);

        var wfDb = await new ErpWorkflowStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "done");
        Assert.False(wfDb.Succeeded);
        Assert.Equal("db", wfDb.Code);

        var collInvalid = await new ErpCollectionsCaseStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(0, "new");
        Assert.False(collInvalid.Succeeded);
        Assert.Equal("invalid", collInvalid.Code);

        var collStatus = await new ErpCollectionsCaseStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "bogus");
        Assert.False(collStatus.Succeeded);
        Assert.Equal("invalid", collStatus.Code);

        var collDb = await new ErpCollectionsCaseStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "escalated");
        Assert.False(collDb.Succeeded);
        Assert.Equal("db", collDb.Code);

        var collSaveInvalid = await new ErpCollectionsCaseSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(-1, "new", 10, 0, null, "Aisha", "", 0, 0);
        Assert.False(collSaveInvalid.Succeeded);
        Assert.Equal("invalid", collSaveInvalid.Code);

        var collSaveDb = await new ErpCollectionsCaseSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(501, "new", 12000, 0, null, "Aisha", "", 0, 0);
        Assert.False(collSaveDb.Succeeded);
        Assert.Equal("db", collSaveDb.Code);

        var collPromiseInvalid = await new ErpCollectionsCasePromiseWriteService(new ConfiguredNeverOpened())
            .PromiseAsync(new ErpCollectionsCasePromiseWriteRequest());
        Assert.False(collPromiseInvalid.Succeeded);
        Assert.Equal("invalid", collPromiseInvalid.Code);
        Assert.Equal("id must be positive.", collPromiseInvalid.Message);

        var collPromiseDb = await new ErpCollectionsCasePromiseWriteService(new UnconfiguredConnections())
            .PromiseAsync(new ErpCollectionsCasePromiseWriteRequest(Id: 4, Amount: 5000, PromiseDate: "2026-09-11"));
        Assert.False(collPromiseDb.Succeeded);
        Assert.Equal("db", collPromiseDb.Code);

        var collActivityInvalid = await new ErpCollectionsActivityLogWriteService(new ConfiguredNeverOpened())
            .LogAsync(new ErpCollectionsActivityLogWriteRequest());
        Assert.False(collActivityInvalid.Succeeded);
        Assert.Equal("invalid", collActivityInvalid.Code);
        Assert.Equal("id must be positive.", collActivityInvalid.Message);

        var collActivityDb = await new ErpCollectionsActivityLogWriteService(new UnconfiguredConnections())
            .LogAsync(new ErpCollectionsActivityLogWriteRequest(CaseId: 4, Type: "call", Outcome: "Left voicemail"));
        Assert.False(collActivityDb.Succeeded);
        Assert.Equal("db", collActivityDb.Code);

        var collHoldInvalid = await new ErpCollectionsHoldSetWriteService(new ConfiguredNeverOpened())
            .SetHoldAsync(new ErpCollectionsHoldSetWriteRequest());
        Assert.False(collHoldInvalid.Succeeded);
        Assert.Equal("invalid", collHoldInvalid.Code);
        Assert.Equal("customerId must be positive.", collHoldInvalid.Message);

        var collHoldDb = await new ErpCollectionsHoldSetWriteService(new UnconfiguredConnections())
            .SetHoldAsync(new ErpCollectionsHoldSetWriteRequest(CustomerId: 501, Place: true, Reason: "Overdue"));
        Assert.False(collHoldDb.Succeeded);
        Assert.Equal("db", collHoldDb.Code);

        var collDunningInvalid = await new ErpCollectionsDunningRunWriteService(new ConfiguredNeverOpened())
            .RunAsync(new ErpCollectionsDunningRunWriteRequest());
        Assert.False(collDunningInvalid.Succeeded);
        Assert.Equal("invalid", collDunningInvalid.Code);

        var collDunningDb = await new ErpCollectionsDunningRunWriteService(new UnconfiguredConnections())
            .RunAsync(new ErpCollectionsDunningRunWriteRequest("502|500|0|0|0", 1));
        Assert.False(collDunningDb.Succeeded);
        Assert.Equal("db", collDunningDb.Code);

        var procSaveInvalid = await new ErpProcurementReqSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync("", 0, "laptops", null, 0, 0);
        Assert.False(procSaveInvalid.Succeeded);
        Assert.Equal("invalid", procSaveInvalid.Code);

        var procSaveDb = await new ErpProcurementReqSaveWriteService(new UnconfiguredConnections())
            .SaveAsync("Sara", 7, "Laptops", null, 1, 0);
        Assert.False(procSaveDb.Succeeded);
        Assert.Equal("db", procSaveDb.Code);

        var procAddLineInvalid = await new ErpProcurementReqAddLineWriteService(new ConfiguredNeverOpened())
            .AddLineAsync(new ErpProcurementReqAddLineWriteRequest());
        Assert.False(procAddLineInvalid.Succeeded);
        Assert.Equal("invalid", procAddLineInvalid.Code);
        Assert.Equal("id must be positive.", procAddLineInvalid.Message);

        var procAddLineDb = await new ErpProcurementReqAddLineWriteService(new UnconfiguredConnections())
            .AddLineAsync(new ErpProcurementReqAddLineWriteRequest(ReqId: 4, ItemCode: "SKU-1", Qty: 2, UnitPrice: 10));
        Assert.False(procAddLineDb.Succeeded);
        Assert.Equal("db", procAddLineDb.Code);

        var procSubmitInvalid = await new ErpProcurementReqWriteService(new ConfiguredNeverOpened())
            .SubmitAsync(0);
        Assert.False(procSubmitInvalid.Succeeded);
        Assert.Equal("invalid", procSubmitInvalid.Code);

        var procSubmitDb = await new ErpProcurementReqWriteService(new UnconfiguredConnections())
            .SubmitAsync(9);
        Assert.False(procSubmitDb.Succeeded);
        Assert.Equal("db", procSubmitDb.Code);

        var procDecideInvalid = await new ErpProcurementReqWriteService(new ConfiguredNeverOpened())
            .DecideAsync(0, true, "admin", "ok");
        Assert.False(procDecideInvalid.Succeeded);
        Assert.Equal("invalid", procDecideInvalid.Code);

        var procDecideDb = await new ErpProcurementReqWriteService(new UnconfiguredConnections())
            .DecideAsync(9, false, "admin", "no");
        Assert.False(procDecideDb.Succeeded);
        Assert.Equal("db", procDecideDb.Code);

        var waveInvalid = await new ErpWmsWaveReleaseWriteService(new ConfiguredNeverOpened())
            .ReleaseAsync(0);
        Assert.False(waveInvalid.Succeeded);
        Assert.Equal("invalid", waveInvalid.Code);

        var waveDb = await new ErpWmsWaveReleaseWriteService(new UnconfiguredConnections())
            .ReleaseAsync(9);
        Assert.False(waveDb.Succeeded);
        Assert.Equal("db", waveDb.Code);

        var receiveItemInvalid = await new ErpWmsReceiveWriteService(new ConfiguredNeverOpened())
            .ReceiveAsync("", 10, 1, 2, null, null, 0);
        Assert.False(receiveItemInvalid.Succeeded);
        Assert.Equal("invalid", receiveItemInvalid.Code);
        Assert.Equal("Item is required", receiveItemInvalid.Message);

        var receiveQtyInvalid = await new ErpWmsReceiveWriteService(new ConfiguredNeverOpened())
            .ReceiveAsync("WIDGET", 0, 1, 2, null, null, 0);
        Assert.False(receiveQtyInvalid.Succeeded);
        Assert.Equal("invalid", receiveQtyInvalid.Code);
        Assert.Equal("qty must be positive.", receiveQtyInvalid.Message);

        var receiveDb = await new ErpWmsReceiveWriteService(new UnconfiguredConnections())
            .ReceiveAsync("WIDGET", 10, 1, 2, "ASN-1", "LP-TEST", 0);
        Assert.False(receiveDb.Succeeded);
        Assert.Equal("db", receiveDb.Code);

        var workCompleteInvalid = await new ErpWmsWorkCompleteWriteService(new ConfiguredNeverOpened())
            .CompleteAsync(0);
        Assert.False(workCompleteInvalid.Succeeded);
        Assert.Equal("invalid", workCompleteInvalid.Code);
        Assert.Equal("id must be positive.", workCompleteInvalid.Message);

        var workCompleteDb = await new ErpWmsWorkCompleteWriteService(new UnconfiguredConnections())
            .CompleteAsync(4);
        Assert.False(workCompleteDb.Succeeded);
        Assert.Equal("db", workCompleteDb.Code);

        var insInvalid = await new ErpInsClaimStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(insInvalid.Succeeded);
        Assert.Equal("invalid", insInvalid.Code);

        var insDb = await new ErpInsClaimStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "settled");
        Assert.False(insDb.Succeeded);
        Assert.Equal("db", insDb.Code);

        var insAddInvalid = await new ErpInsClaimAddWriteService(new ConfiguredNeverOpened())
            .SaveAsync(-1, 0, "CL-1", null, null, null, null, 0, 0, null, null, null);
        Assert.False(insAddInvalid.Succeeded);
        Assert.Equal("invalid", insAddInvalid.Code);

        var insAddDb = await new ErpInsClaimAddWriteService(new UnconfiguredConnections())
            .SaveAsync(0, 3, "CL-1", "2026-09-04", null, null, "loss", 10, 0, null, "notified", null);
        Assert.False(insAddDb.Succeeded);
        Assert.Equal("db", insAddDb.Code);

        var vatInvalid = await new ErpBosVatRefundStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(vatInvalid.Succeeded);
        Assert.Equal("invalid", vatInvalid.Code);

        var vatDb = await new ErpBosVatRefundStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "refunded");
        Assert.False(vatDb.Succeeded);
        Assert.Equal("db", vatDb.Code);

        var vatSaveInvalid = await new ErpBosVatRefundSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(-1, null, null, null, null, null, 0, null, null, null, null, 1);
        Assert.False(vatSaveInvalid.Succeeded);
        Assert.Equal("invalid", vatSaveInvalid.Code);
        Assert.Equal("A refund id must be >= 0.", vatSaveInvalid.Message);

        var vatSaveDb = await new ErpBosVatRefundSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(0, "TAG-1", "SI-1", null, null, null, 250, null, null, null, null, 1);
        Assert.False(vatSaveDb.Succeeded);
        Assert.Equal("db", vatSaveDb.Code);

        var invInvalid = await new ErpSubInvoicePaidWriteService(new ConfiguredNeverOpened())
            .MarkPaidAsync(0);
        Assert.False(invInvalid.Succeeded);
        Assert.Equal("invalid", invInvalid.Code);

        var invDb = await new ErpSubInvoicePaidWriteService(new UnconfiguredConnections())
            .MarkPaidAsync(9);
        Assert.False(invDb.Succeeded);
        Assert.Equal("db", invDb.Code);

        var subGenInvalid = await new ErpSubGenerateWriteService(new ConfiguredNeverOpened())
            .GenerateAsync(0);
        Assert.False(subGenInvalid.Succeeded);
        Assert.Equal("invalid", subGenInvalid.Code);
        Assert.Equal("Subscription not found", subGenInvalid.Message);

        var subGenDb = await new ErpSubGenerateWriteService(new UnconfiguredConnections())
            .GenerateAsync(9);
        Assert.False(subGenDb.Succeeded);
        Assert.Equal("db", subGenDb.Code);

        var pfInvalid = await new ErpPfCaseCancelWriteService(new ConfiguredNeverOpened())
            .CancelAsync(0);
        Assert.False(pfInvalid.Succeeded);
        Assert.Equal("invalid", pfInvalid.Code);

        var pfDb = await new ErpPfCaseCancelWriteService(new UnconfiguredConnections())
            .CancelAsync(9);
        Assert.False(pfDb.Succeeded);
        Assert.Equal("db", pfDb.Code);

        var pfStartInvalid = await new ErpPfCaseStartWriteService(new ConfiguredNeverOpened())
            .StartAsync(new ErpPfCaseStartWriteRequest(4, ""));
        Assert.False(pfStartInvalid.Succeeded);
        Assert.Equal("invalid", pfStartInvalid.Code);
        Assert.Equal("Case title is required", pfStartInvalid.Message);

        var pfStartDb = await new ErpPfCaseStartWriteService(new UnconfiguredConnections())
            .StartAsync(new ErpPfCaseStartWriteRequest(4, "Launch case"));
        Assert.False(pfStartDb.Succeeded);
        Assert.Equal("db", pfStartDb.Code);

        var pfActInvalid = await new ErpPfCaseActWriteService(new ConfiguredNeverOpened())
            .ActAsync(new ErpPfCaseActWriteRequest(0, "approve"));
        Assert.False(pfActInvalid.Succeeded);
        Assert.Equal("invalid", pfActInvalid.Code);
        Assert.Equal("Case not found", pfActInvalid.Message);

        var pfActDb = await new ErpPfCaseActWriteService(new UnconfiguredConnections())
            .ActAsync(new ErpPfCaseActWriteRequest(9, "approve"));
        Assert.False(pfActDb.Succeeded);
        Assert.Equal("db", pfActDb.Code);

        var convInvalid = await new ErpProcurementReqWriteService(new ConfiguredNeverOpened())
            .ConvertAsync(0);
        Assert.False(convInvalid.Succeeded);
        Assert.Equal("invalid", convInvalid.Code);

        var convDb = await new ErpProcurementReqWriteService(new UnconfiguredConnections())
            .ConvertAsync(9);
        Assert.False(convDb.Succeeded);
        Assert.Equal("db", convDb.Code);

        var stepInvalid = await new ErpPfStepDeleteWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(stepInvalid.Succeeded);
        Assert.Equal("invalid", stepInvalid.Code);

        var stepDb = await new ErpPfStepDeleteWriteService(new UnconfiguredConnections())
            .DeleteAsync(9);
        Assert.False(stepDb.Succeeded);
        Assert.Equal("db", stepDb.Code);

        var wfRuleInvalid = await new ErpBosWfDisableRuleWriteService(new ConfiguredNeverOpened())
            .DisableAsync(0);
        Assert.False(wfRuleInvalid.Succeeded);
        Assert.Equal("invalid", wfRuleInvalid.Code);

        var wfRuleDb = await new ErpBosWfDisableRuleWriteService(new UnconfiguredConnections())
            .DisableAsync(9);
        Assert.False(wfRuleDb.Succeeded);
        Assert.Equal("db", wfRuleDb.Code);

        var oblInvalid = await new ErpBosComplianceDisableObligationWriteService(new ConfiguredNeverOpened())
            .DisableAsync(0);
        Assert.False(oblInvalid.Succeeded);
        Assert.Equal("invalid", oblInvalid.Code);

        var oblDb = await new ErpBosComplianceDisableObligationWriteService(new UnconfiguredConnections())
            .DisableAsync(9);
        Assert.False(oblDb.Succeeded);
        Assert.Equal("db", oblDb.Code);

        var leaveReqInvalid = await new ErpHrLeaveRequestWriteService(new ConfiguredNeverOpened())
            .RequestAsync(0, "annual", 2, null, null);
        Assert.False(leaveReqInvalid.Succeeded);
        Assert.Equal("invalid", leaveReqInvalid.Code);

        var leaveReqDb = await new ErpHrLeaveRequestWriteService(new UnconfiguredConnections())
            .RequestAsync(9, "annual", 2, "2026-09-04", "2026-09-08");
        Assert.False(leaveReqDb.Succeeded);
        Assert.Equal("db", leaveReqDb.Code);

        var leaveInvalid = await new ErpHrStatusWriteService(new ConfiguredNeverOpened())
            .SetLeaveStatusAsync(0, "approved");
        Assert.False(leaveInvalid.Succeeded);
        Assert.Equal("invalid", leaveInvalid.Code);

        var leaveEmpty = await new ErpHrStatusWriteService(new ConfiguredNeverOpened())
            .SetLeaveStatusAsync(9, "  ");
        Assert.False(leaveEmpty.Succeeded);
        Assert.Equal("invalid", leaveEmpty.Code);

        var leaveDb = await new ErpHrStatusWriteService(new UnconfiguredConnections())
            .SetLeaveStatusAsync(9, "approved");
        Assert.False(leaveDb.Succeeded);
        Assert.Equal("db", leaveDb.Code);

        var expInvalid = await new ErpHrStatusWriteService(new ConfiguredNeverOpened())
            .SetExpenseStatusAsync(0, "approved");
        Assert.False(expInvalid.Succeeded);
        Assert.Equal("invalid", expInvalid.Code);

        var expDb = await new ErpHrStatusWriteService(new UnconfiguredConnections())
            .SetExpenseStatusAsync(9, "paid");
        Assert.False(expDb.Succeeded);
        Assert.Equal("db", expDb.Code);

        var expSaveInvalid = await new ErpHrExpenseSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(0, "Taxi", [new ErpHrExpenseLine("taxi", 12)]);
        Assert.False(expSaveInvalid.Succeeded);
        Assert.Equal("invalid", expSaveInvalid.Code);

        var expSaveDb = await new ErpHrExpenseSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(9, "Taxi", [new ErpHrExpenseLine("taxi", 12)]);
        Assert.False(expSaveDb.Succeeded);
        Assert.Equal("db", expSaveDb.Code);

        var consEntInvalid = await new ErpConsDeleteWriteService(new ConfiguredNeverOpened())
            .DeleteEntityAsync(0);
        Assert.False(consEntInvalid.Succeeded);
        Assert.Equal("invalid", consEntInvalid.Code);

        var consEntDb = await new ErpConsDeleteWriteService(new UnconfiguredConnections())
            .DeleteEntityAsync(9);
        Assert.False(consEntDb.Succeeded);
        Assert.Equal("db", consEntDb.Code);

        var consSaveInvalid = await new ErpConsEntitySaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(0, "", "Sub", "AED", 100, false, null);
        Assert.False(consSaveInvalid.Succeeded);
        Assert.Equal("invalid", consSaveInvalid.Code);

        var consSaveDb = await new ErpConsEntitySaveWriteService(new UnconfiguredConnections())
            .SaveAsync(0, "SUB1", "Sub one", "AED", 100, false, null);
        Assert.False(consSaveDb.Succeeded);
        Assert.Equal("db", consSaveDb.Code);

        var prjInvalid = await new ErpPrjSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpPrjSaveWriteRequest());
        Assert.False(prjInvalid.Succeeded);
        Assert.Equal("invalid", prjInvalid.Code);
        Assert.Equal("Project code is required", prjInvalid.Message);

        var prjDb = await new ErpPrjSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(new ErpPrjSaveWriteRequest(Code: "PRJ-001", Name: "Pilot"));
        Assert.False(prjDb.Succeeded);
        Assert.Equal("db", prjDb.Code);

        var consIcInvalid = await new ErpConsDeleteWriteService(new ConfiguredNeverOpened())
            .DeleteIcAsync(0);
        Assert.False(consIcInvalid.Succeeded);
        Assert.Equal("invalid", consIcInvalid.Code);

        var consIcDb = await new ErpConsDeleteWriteService(new UnconfiguredConnections())
            .DeleteIcAsync(9);
        Assert.False(consIcDb.Succeeded);
        Assert.Equal("db", consIcDb.Code);

        var insDocInvalid = await new ErpInsDocDeleteWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(insDocInvalid.Succeeded);
        Assert.Equal("invalid", insDocInvalid.Code);

        var insDocDb = await new ErpInsDocDeleteWriteService(new UnconfiguredConnections())
            .DeleteAsync(9);
        Assert.False(insDocDb.Succeeded);
        Assert.Equal("db", insDocDb.Code);

        var fyReopenInvalid = await new ErpFyWriteService(new ConfiguredNeverOpened())
            .ReopenYearAsync(0);
        Assert.False(fyReopenInvalid.Succeeded);
        Assert.Equal("invalid", fyReopenInvalid.Code);

        var fyReopenDb = await new ErpFyWriteService(new UnconfiguredConnections())
            .ReopenYearAsync(9);
        Assert.False(fyReopenDb.Succeeded);
        Assert.Equal("db", fyReopenDb.Code);

        var fyPeriodInvalid = await new ErpFyWriteService(new ConfiguredNeverOpened())
            .SetPeriodStatusAsync(9, 1, "nope");
        Assert.False(fyPeriodInvalid.Succeeded);
        Assert.Equal("invalid", fyPeriodInvalid.Code);

        var fyPeriodNoYear = await new ErpFyWriteService(new ConfiguredNeverOpened())
            .SetPeriodStatusAsync(0, 1, "open");
        Assert.False(fyPeriodNoYear.Succeeded);
        Assert.Equal("invalid", fyPeriodNoYear.Code);

        var fyPeriodNoNo = await new ErpFyWriteService(new ConfiguredNeverOpened())
            .SetPeriodStatusAsync(9, 0, "open");
        Assert.False(fyPeriodNoNo.Succeeded);
        Assert.Equal("invalid", fyPeriodNoNo.Code);

        var fyPeriodDb = await new ErpFyWriteService(new UnconfiguredConnections())
            .SetPeriodStatusAsync(9, 1, "open");
        Assert.False(fyPeriodDb.Succeeded);
        Assert.Equal("db", fyPeriodDb.Code);

        var finPeriodInvalid = await new ErpFinPeriodStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(1, 2026, 1, "nope");
        Assert.False(finPeriodInvalid.Succeeded);
        Assert.Equal("invalid", finPeriodInvalid.Code);
        Assert.Equal("Invalid period status", finPeriodInvalid.Message);

        var finPeriodFy = await new ErpFinPeriodStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(1, 0, 1, "open");
        Assert.False(finPeriodFy.Succeeded);
        Assert.Equal("invalid", finPeriodFy.Code);

        var finPeriodDb = await new ErpFinPeriodStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(1, 2026, 1, "closed");
        Assert.False(finPeriodDb.Succeeded);
        Assert.Equal("db", finPeriodDb.Code);

        var whtSettleInvalid = await new ErpWhtSettleWriteService(new ConfiguredNeverOpened())
            .SettleAsync(0);
        Assert.False(whtSettleInvalid.Succeeded);
        Assert.Equal("invalid", whtSettleInvalid.Code);

        var whtSettleDb = await new ErpWhtSettleWriteService(new UnconfiguredConnections())
            .SettleAsync(9);
        Assert.False(whtSettleDb.Succeeded);
        Assert.Equal("db", whtSettleDb.Code);

        var whtCodeInvalid = await new ErpWhtCodeSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpWhtCodeSaveWriteRequest());
        Assert.False(whtCodeInvalid.Succeeded);
        Assert.Equal("invalid", whtCodeInvalid.Code);

        var whtCodeDb = await new ErpWhtCodeSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(new ErpWhtCodeSaveWriteRequest(Code: "WHT5", Name: "Services 5%", Rate: 5));
        Assert.False(whtCodeDb.Succeeded);
        Assert.Equal("db", whtCodeDb.Code);

        var whtRecordInvalid = await new ErpWhtRecordWriteService(new ConfiguredNeverOpened())
            .RecordAsync(new ErpWhtRecordWriteRequest());
        Assert.False(whtRecordInvalid.Succeeded);
        Assert.Equal("invalid", whtRecordInvalid.Code);

        var whtRecordDb = await new ErpWhtRecordWriteService(new UnconfiguredConnections())
            .RecordAsync(new ErpWhtRecordWriteRequest(CodeId: 1, BaseAmount: 10000));
        Assert.False(whtRecordDb.Succeeded);
        Assert.Equal("db", whtRecordDb.Code);

        var whtCertInvalid = await new ErpWhtCertificateWriteService(new ConfiguredNeverOpened())
            .IssueAsync(new ErpWhtCertificateWriteRequest());
        Assert.False(whtCertInvalid.Succeeded);
        Assert.Equal("invalid", whtCertInvalid.Code);

        var whtCertDb = await new ErpWhtCertificateWriteService(new UnconfiguredConnections())
            .IssueAsync(new ErpWhtCertificateWriteRequest(Id: 1));
        Assert.False(whtCertDb.Succeeded);
        Assert.Equal("db", whtCertDb.Code);

        var hrEmpInvalid = await new ErpHrEmpSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpHrEmpSaveWriteRequest());
        Assert.False(hrEmpInvalid.Succeeded);
        Assert.Equal("invalid", hrEmpInvalid.Code);
        Assert.Equal("Code and name are required", hrEmpInvalid.Message);

        var hrEmpDb = await new ErpHrEmpSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(new ErpHrEmpSaveWriteRequest(Code: "E001", Name: "Ahmed"));
        Assert.False(hrEmpDb.Succeeded);
        Assert.Equal("db", hrEmpDb.Code);

        var hrAttInvalid = await new ErpHrAttendanceWriteService(new ConfiguredNeverOpened())
            .LogAsync(new ErpHrAttendanceWriteRequest());
        Assert.False(hrAttInvalid.Succeeded);
        Assert.Equal("invalid", hrAttInvalid.Code);
        Assert.Equal("Select an employee", hrAttInvalid.Message);

        var hrAttDb = await new ErpHrAttendanceWriteService(new UnconfiguredConnections())
            .LogAsync(new ErpHrAttendanceWriteRequest(EmployeeId: 1, Hours: 8));
        Assert.False(hrAttDb.Succeeded);
        Assert.Equal("db", hrAttDb.Code);

        var hrPayInvalid = await new ErpHrPayrollRunWriteService(new ConfiguredNeverOpened())
            .GenerateAsync(new ErpHrPayrollRunWriteRequest(Period: "not-a-period"));
        Assert.False(hrPayInvalid.Succeeded);
        Assert.Equal("invalid", hrPayInvalid.Code);
        Assert.Equal("Invalid period (use YYYY-MM)", hrPayInvalid.Message);

        var hrPayDb = await new ErpHrPayrollRunWriteService(new UnconfiguredConnections())
            .GenerateAsync(new ErpHrPayrollRunWriteRequest(Period: "2026-01"));
        Assert.False(hrPayDb.Succeeded);
        Assert.Equal("db", hrPayDb.Code);
    }

    [Fact]
    public void Wishlist_and_compare_cookie_helpers_match_php_json_int_array()
    {
        var parsed = StorefrontIntListCookie.Parse("[12,0,-3,12,44]", StorefrontIntListCookie.BookmarksMax);
        Assert.Equal(new[] { 12, 44 }, parsed);

        var added = StorefrontIntListCookie.Add(parsed, 99, StorefrontIntListCookie.BookmarksMax);
        Assert.Equal(new[] { 12, 44, 99 }, added);
        Assert.Equal(added, StorefrontIntListCookie.Add(added, 99, StorefrontIntListCookie.BookmarksMax));

        var removed = StorefrontIntListCookie.Remove(added, 44);
        Assert.Equal(new[] { 12, 99 }, removed);
        Assert.Equal("[12,99]", StorefrontIntListCookie.Serialize(removed));

        Assert.Empty(StorefrontIntListCookie.Parse("not-json", 8));
        Assert.Empty(StorefrontIntListCookie.Add([], 0, 8));

        Assert.Equal("[42,99]", StorefrontIntListCookie.ExtractFromHeader("bookmarks=[42,99]; path=/", "bookmarks"));
        Assert.Equal("[42,99]", StorefrontIntListCookie.ExtractFromHeader("session=x; bookmarks=[42,99]", "bookmarks"));
        Assert.Equal("[12,99]", StorefrontIntListCookie.ExtractFromHeader("bookmarks=%5B12%2C99%5D", "bookmarks"));
        Assert.Equal(new[] { 42, 99 }, StorefrontIntListCookie.Parse(StorefrontIntListCookie.ExtractFromHeader("bookmarks=[42,99]", "bookmarks"), 80));
    }

    [Fact]
    public void Profile_field_allow_list_rejects_password_email_and_docs()
    {
        Assert.False(StorefrontCustomerWriteService.IsAllowedProfileKey("password"));
        Assert.False(StorefrontCustomerWriteService.IsAllowedProfileKey("email"));
        Assert.False(StorefrontCustomerWriteService.IsAllowedProfileKey("phone"));
        Assert.False(StorefrontCustomerWriteService.IsAllowedProfileKey("epc_doc_trade_licence"));
        Assert.True(StorefrontCustomerWriteService.IsAllowedProfileKey("name"));
        Assert.True(StorefrontCustomerWriteService.IsAllowedProfileKey("company_name"));
        Assert.True(StorefrontCustomerWriteService.IsAllowedProfileKey("epc_custom_trade_name"));
        Assert.False(StorefrontCustomerWriteService.IsAllowedProfileKey("confirmWrites"));
        Assert.False(StorefrontCustomerWriteService.IsAllowedProfileKey("reg_variant"));

        var clean = StorefrontCustomerWriteService.NormalizeProfileFields(new Dictionary<string, string>
        {
            ["name"] = "Ada <b>Lovelace</b>",
            ["password"] = "nope",
            ["email"] = "ada@example.com",
            ["  "] = "x",
        });
        Assert.Single(clean);
        Assert.Equal("Ada &lt;b&gt;Lovelace&lt;/b&gt;", clean["name"]);
    }

    [Fact]
    public async Task Garage_check_car_and_pay_refund_and_password_validate_without_db()
    {
        var garageAuth = await new StorefrontGarageWriteService(new UnconfiguredConnections())
            .CheckCarAsync(0, 1, 1);
        Assert.False(garageAuth.Succeeded);
        Assert.Equal("auth", garageAuth.Code);

        var garageInvalid = await new StorefrontGarageWriteService(new ConfiguredNeverOpened())
            .CheckCarAsync(1, 0, 0);
        Assert.False(garageInvalid.Succeeded);
        Assert.Equal("invalid", garageInvalid.Code);

        var garageDb = await new StorefrontGarageWriteService(new UnconfiguredConnections())
            .CheckCarAsync(1, 3, 11);
        Assert.False(garageDb.Succeeded);
        Assert.Equal("db", garageDb.Code);

        var refundInvalid = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .PayRefundAsync(0, true, null, 1);
        Assert.False(refundInvalid.Succeeded);
        Assert.Equal("forbidden", refundInvalid.Code);

        var refundDb = await new CpOmsWriteService(new UnconfiguredConnections())
            .PayRefundAsync(42, true, null, 1);
        Assert.False(refundDb.Succeeded);
        Assert.Equal("db", refundDb.Code);

        var refreshInvalid = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .RefreshItemCostAsync(0, 0, 1);
        Assert.False(refreshInvalid.Succeeded);
        Assert.Equal("invalid", refreshInvalid.Code);

        var refreshDb = await new CpOmsWriteService(new UnconfiguredConnections())
            .RefreshItemCostAsync(9, 3, 1);
        Assert.False(refreshDb.Succeeded);
        Assert.Equal("db", refreshDb.Code);

        var pwdAuth = await new StorefrontCustomerWriteService(new UnconfiguredConnections())
            .ChangePasswordAsync(0, "secret", "succ");
        Assert.False(pwdAuth.Succeeded);
        Assert.Equal("auth", pwdAuth.Code);

        var pwdInvalid = await new StorefrontCustomerWriteService(new ConfiguredNeverOpened())
            .ChangePasswordAsync(1, "", "succ");
        Assert.False(pwdInvalid.Succeeded);
        Assert.Equal("invalid", pwdInvalid.Code);

        var pwdConfig = await new StorefrontCustomerWriteService(new ConfiguredNeverOpened())
            .ChangePasswordAsync(1, "secret", "");
        Assert.False(pwdConfig.Succeeded);
        Assert.Equal("config", pwdConfig.Code);

        var pwdDb = await new StorefrontCustomerWriteService(new UnconfiguredConnections())
            .ChangePasswordAsync(1, "secret", "succ");
        Assert.False(pwdDb.Succeeded);
        Assert.Equal("db", pwdDb.Code);

        Assert.Equal(32, LegacyPasswordVerifier.Md5Hex("secret" + "succ").Length);

        Assert.Equal("upload", CpAccessoriesPhotoWriteService.NormalizeAction("add"));
        Assert.Equal("delete", CpAccessoriesPhotoWriteService.NormalizeAction("del"));
        Assert.Equal("set_primary", CpAccessoriesPhotoWriteService.NormalizeAction("primary"));
        Assert.Equal("cover.webp", CpAccessoriesPhotoWriteService.SanitizeImageName("../cover.webp"));
        Assert.True(CpAccessoriesPhotoWriteService.HasAllowedImageExtension("cover.webp"));
        Assert.Equal("/content/files/images/accessories/cover.webp", CpAccessoriesPhotoWriteService.PublicUrl("cover.webp"));
        var accBadAction = await new CpAccessoriesPhotoWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesPhotoWriteRequest(Action: "rename"));
        Assert.False(accBadAction.Succeeded);
        Assert.Equal("invalid", accBadAction.Code);
        var accNoListing = await new CpAccessoriesPhotoWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesPhotoWriteRequest(Action: "upload", FileName: "cover.png"));
        Assert.False(accNoListing.Succeeded);
        Assert.Equal("invalid", accNoListing.Code);
        var accBadExt = await new CpAccessoriesPhotoWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesPhotoWriteRequest(Action: "upload", ListingId: 1, FileName: "cover.exe"));
        Assert.False(accBadExt.Succeeded);
        Assert.Equal("invalid", accBadExt.Code);
        var accUploadDb = await new CpAccessoriesPhotoWriteService(new UnconfiguredConnections())
            .WriteAsync(new CpAccessoriesPhotoWriteRequest(Action: "upload", ListingId: 1, FileName: "cover.png"));
        Assert.False(accUploadDb.Succeeded);
        Assert.Equal("db", accUploadDb.Code);
        var accDeleteEmpty = await new CpAccessoriesPhotoWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesPhotoWriteRequest(Action: "delete", ListingId: 1, PhotoId: 0));
        Assert.False(accDeleteEmpty.Succeeded);
        Assert.Equal("invalid", accDeleteEmpty.Code);
        var accDeleteDb = await new CpAccessoriesPhotoWriteService(new UnconfiguredConnections())
            .WriteAsync(new CpAccessoriesPhotoWriteRequest(Action: "delete", ListingId: 1, PhotoId: 9));
        Assert.False(accDeleteDb.Succeeded);
        Assert.Equal("db", accDeleteDb.Code);

        Assert.Equal("save", CpAccessoriesListingWriteService.NormalizeAction("create"));
        Assert.Equal("set_status", CpAccessoriesListingWriteService.NormalizeAction("status"));
        Assert.Equal("delete", CpAccessoriesListingWriteService.NormalizeAction("del"));
        Assert.Equal("", CpAccessoriesListingWriteService.SanitizeExternalUrl("/en/accessories-spare-parts"));
        Assert.Equal("/en/accessories?id=9", CpAccessoriesListingWriteService.SanitizeExternalUrl("/en/accessories?id=9"));
        var accListingInvalid = await new CpAccessoriesListingWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesListingWriteRequest(Action: "save", Title: "", CategoryId: 0));
        Assert.False(accListingInvalid.Succeeded);
        Assert.Equal("invalid", accListingInvalid.Code);
        Assert.Contains("Title and category", accListingInvalid.Message, StringComparison.OrdinalIgnoreCase);
        var accListingStatusInvalid = await new CpAccessoriesListingWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesListingWriteRequest(Action: "set_status", ListingId: 0, Status: "published"));
        Assert.False(accListingStatusInvalid.Succeeded);
        Assert.Equal("invalid", accListingStatusInvalid.Code);
        var accListingDeleteInvalid = await new CpAccessoriesListingWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesListingWriteRequest(Action: "delete", ListingId: 0));
        Assert.False(accListingDeleteInvalid.Succeeded);
        Assert.Equal("invalid", accListingDeleteInvalid.Code);
        var accListingDb = await new CpAccessoriesListingWriteService(new UnconfiguredConnections())
            .WriteAsync(new CpAccessoriesListingWriteRequest(
                Action: "save",
                CategoryId: 1,
                Title: "Floor mats",
                Make: "OEM",
                Price: 99,
                Currency: "AED",
                Status: "published"));
        Assert.False(accListingDb.Succeeded);
        Assert.Equal("db", accListingDb.Code);

        Assert.Equal("save_category", CpAccessoriesTaxonomyWriteService.NormalizeAction("add_category"));
        Assert.Equal("save_term", CpAccessoriesTaxonomyWriteService.NormalizeAction("term"));
        Assert.Equal("floor-mats", CpAccessoriesTaxonomyWriteService.Slugify("Floor Mats"));
        Assert.Equal("make", CpAccessoriesTaxonomyWriteService.SanitizeTermType("MAKE!"));
        var accTaxInvalid = await new CpAccessoriesTaxonomyWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesTaxonomyWriteRequest(Action: "save_category", Label: ""));
        Assert.False(accTaxInvalid.Succeeded);
        Assert.Equal("invalid", accTaxInvalid.Code);
        Assert.Contains("Category label", accTaxInvalid.Message, StringComparison.OrdinalIgnoreCase);
        var accTermInvalid = await new CpAccessoriesTaxonomyWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesTaxonomyWriteRequest(Action: "save_term", TermType: "make", Label: ""));
        Assert.False(accTermInvalid.Succeeded);
        Assert.Equal("invalid", accTermInvalid.Code);
        var accTaxDeleteInvalid = await new CpAccessoriesTaxonomyWriteService(new ConfiguredNeverOpened())
            .WriteAsync(new CpAccessoriesTaxonomyWriteRequest(Action: "delete_category", Id: 0));
        Assert.False(accTaxDeleteInvalid.Succeeded);
        Assert.Equal("invalid", accTaxDeleteInvalid.Code);
        var accTaxDb = await new CpAccessoriesTaxonomyWriteService(new UnconfiguredConnections())
            .WriteAsync(new CpAccessoriesTaxonomyWriteRequest(Action: "save_category", Label: "Interior"));
        Assert.False(accTaxDb.Succeeded);
        Assert.Equal("db", accTaxDb.Code);

        Assert.Equal("AE", ErpEinvoiceProfileWriteService.NormalizeCountry("UAE"));
        Assert.True(ErpEinvoiceProfileWriteService.TrnValid("100123456789012"));
        Assert.Equal("1001234567", ErpEinvoiceProfileWriteService.TinFromTrn("100123456789012"));
        var einvCountry = await new ErpEinvoiceProfileWriteService(new ConfiguredNeverOpened())
            .SaveSellerAsync(new ErpEinvoiceSellerWriteRequest(SellerName: "Co", SellerTrn: "100123456789012", SellerCountryCode: "US"));
        Assert.False(einvCountry.Succeeded);
        Assert.Equal("invalid", einvCountry.Code);
        var einvTrn = await new ErpEinvoiceProfileWriteService(new ConfiguredNeverOpened())
            .SaveSellerAsync(new ErpEinvoiceSellerWriteRequest(SellerName: "Co", SellerTrn: "123", SellerCountryCode: "AE"));
        Assert.False(einvTrn.Succeeded);
        Assert.Equal("invalid", einvTrn.Code);
        var einvBuyer = await new ErpEinvoiceProfileWriteService(new ConfiguredNeverOpened())
            .SaveBuyerAsync(new ErpEinvoiceBuyerWriteRequest(UserId: 0, BuyerName: "Buyer"));
        Assert.False(einvBuyer.Succeeded);
        Assert.Equal("invalid", einvBuyer.Code);
        var einvDb = await new ErpEinvoiceProfileWriteService(new UnconfiguredConnections())
            .SaveSellerAsync(new ErpEinvoiceSellerWriteRequest(SellerName: "ePartsCart LLC", SellerTrn: "100123456789012", SellerCountryCode: "AE"));
        Assert.False(einvDb.Succeeded);
        Assert.Equal("db", einvDb.Code);
    }

    [Fact]
    public async Task Marketing_rejects_unconfigured_db_and_defaults_like_php()
    {
        var missingDb = await new ErpMarketingWriteService(new UnconfiguredConnections())
            .CreateAsync("Spring", "digital", 100, "active", "2026-09-04", "2026-10-04", "");
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);

        Assert.Contains("active", ErpMarketingWriteService.AllowedStatuses);
        Assert.Contains("completed", ErpMarketingWriteService.AllowedStatuses);
        Assert.Equal(1_788_480_000, ErpMarketingWriteService.ResolveStartUnix("2026-09-04", 1));
        Assert.Equal(1_788_566_399, ErpMarketingWriteService.ResolveEndUnix("2026-09-04", 1));
    }

    [Fact]
    public async Task Workflow_create_rejects_empty_title_and_unconfigured_db()
    {
        var invalid = await new ErpWorkflowCreateWriteService(new ConfiguredNeverOpened())
            .CreateAsync("  ", "admin", "normal", 0, "", "", 0, "", 1);
        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid", invalid.Code);

        var missingDb = await new ErpWorkflowCreateWriteService(new UnconfiguredConnections())
            .CreateAsync("Pick parts", "warehouse", "high", 9, "", "", 0, "", 1);
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }

    [Fact]
    public async Task Prj_task_save_rejects_missing_project_and_unconfigured_db()
    {
        var invalid = await new ErpPrjTaskSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpPrjTaskSaveWriteRequest());
        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid", invalid.Code);
        Assert.Equal("Select a project", invalid.Message);

        var missingDb = await new ErpPrjTaskSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(new ErpPrjTaskSaveWriteRequest(ProjectId: 4, Name: "Design"));
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }

    [Fact]
    public async Task Wms_wave_create_rejects_invalid_item_qty_and_unconfigured_db()
    {
        var invalid = await new ErpWmsWaveCreateWriteService(new ConfiguredNeverOpened())
            .CreateWithPickAsync("  ", 0, "SO-1", 0, 0, 0);
        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid", invalid.Code);

        var missingDb = await new ErpWmsWaveCreateWriteService(new UnconfiguredConnections())
            .CreateWithPickAsync("SKU-1", 2, "SO-1", 0, 0, 0);
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }

    [Fact]
    public async Task Cons_figures_save_rejects_missing_entity_and_unconfigured_db()
    {
        var invalid = await new ErpConsFiguresSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpConsFiguresSaveWriteRequest());
        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid", invalid.Code);
        Assert.Equal("Entity is required", invalid.Message);

        var missingDb = await new ErpConsFiguresSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(new ErpConsFiguresSaveWriteRequest(EntityCode: "SUB1", Revenue: 100));
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }

    [Fact]
    public async Task Prj_log_time_rejects_missing_project_and_unconfigured_db()
    {
        var invalid = await new ErpPrjLogTimeWriteService(new ConfiguredNeverOpened())
            .LogAsync(new ErpPrjLogTimeWriteRequest());
        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid", invalid.Code);
        Assert.Equal("Select a project", invalid.Message);

        var missingDb = await new ErpPrjLogTimeWriteService(new UnconfiguredConnections())
            .LogAsync(new ErpPrjLogTimeWriteRequest(ProjectId: 4, Hours: 2));
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }
    [Fact]
    public async Task Bos_wf_raise_rejects_unconfigured_db()
    {
        var missingDb = await new ErpBosWfRaiseWriteService(new UnconfiguredConnections())
            .RaiseAsync(new ErpBosWfRaiseWriteRequest("purchase_order", 9, "PO-9", 12000));
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }

    [Fact]
    public async Task Fy_create_rejects_invalid_dates_and_unconfigured_db()
    {
        var invalid = await new ErpFyCreateWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpFyCreateWriteRequest("FY26"));
        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid", invalid.Code);
        Assert.Equal("Valid start and end dates are required", invalid.Message);

        var inverted = await new ErpFyCreateWriteService(new ConfiguredNeverOpened())
            .CreateAsync(new ErpFyCreateWriteRequest("FY26", 1798761599, 1767225600));
        Assert.False(inverted.Succeeded);
        Assert.Equal("invalid", inverted.Code);
        Assert.Equal("Valid start and end dates are required", inverted.Message);

        var missingDb = await new ErpFyCreateWriteService(new UnconfiguredConnections())
            .CreateAsync(new ErpFyCreateWriteRequest("FY26", 1767225600, 1798761599, true));
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }

    [Fact]
    public async Task Bos_wf_decide_rejects_missing_request_and_unconfigured_db()
    {
        var invalid = await new ErpBosWfDecideWriteService(new ConfiguredNeverOpened())
            .DecideAsync(new ErpBosWfDecideWriteRequest());
        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid", invalid.Code);
        Assert.Equal("Request not pending", invalid.Message);

        var missingDb = await new ErpBosWfDecideWriteService(new UnconfiguredConnections())
            .DecideAsync(new ErpBosWfDecideWriteRequest(4, "approve"));
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }

    [Fact]
    public async Task Cons_ic_save_rejects_invalid_pair_amount_and_unconfigured_db()
    {
        var missing = await new ErpConsIcSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpConsIcSaveWriteRequest());
        Assert.False(missing.Succeeded);
        Assert.Equal("invalid", missing.Code);
        Assert.Equal("From and to entities are required", missing.Message);

        var same = await new ErpConsIcSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpConsIcSaveWriteRequest(FromEntity: "HOME", ToEntity: "home", Amount: 10));
        Assert.False(same.Succeeded);
        Assert.Equal("Intercompany needs two different entities", same.Message);

        var zero = await new ErpConsIcSaveWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new ErpConsIcSaveWriteRequest(FromEntity: "HOME", ToEntity: "SUB1", Amount: 0));
        Assert.False(zero.Succeeded);
        Assert.Equal("Amount must be positive", zero.Message);

        var missingDb = await new ErpConsIcSaveWriteService(new UnconfiguredConnections())
            .SaveAsync(new ErpConsIcSaveWriteRequest(FromEntity: "HOME", ToEntity: "SUB1", Amount: 25));
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }

    [Fact]
    public async Task Promo_save_rejects_missing_code_and_unconfigured_db()
    {
        var missingCode = await new CpPromoWriteService(new ConfiguredNeverOpened())
            .SaveAsync(new CpPromoSaveRequest(0, "", "10% off", "percent", 10, 100, 0, 0, 1));
        Assert.False(missingCode.Succeeded);
        Assert.Equal("invalid", missingCode.Code);
        Assert.Equal("A promotion code is required.", missingCode.Message);

        var missingDb = await new CpPromoWriteService(new UnconfiguredConnections())
            .SaveAsync(new CpPromoSaveRequest(0, "SAVE10", "10% off", "percent", 10, 100, 0, 0, 1));
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);
    }

}
