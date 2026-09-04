using System.Data.Common;
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

        var updateReprice = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .UpdateItemAsync(9, new CpOmsItemWritePatch(3, 12m, 2, Manufacturer: "Bosch", Article: "0986", RepriceFromWarehouse: true), 1);
        Assert.False(updateReprice.Succeeded);
        Assert.Equal("not_implemented", updateReprice.Code);

        var updateItems = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .UpdateItemsAsync(0, [], 1);
        Assert.False(updateItems.Succeeded);
        Assert.Equal("invalid", updateItems.Code);

        var updateItemsDb = await new CpOmsWriteService(new UnconfiguredConnections())
            .UpdateItemsAsync(9, [new CpOmsItemWritePatch(3, 12m, 2, Manufacturer: "Bosch", Article: "0986")], 1);
        Assert.False(updateItemsDb.Succeeded);
        Assert.Equal("db", updateItemsDb.Code);

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

        var tplDel = await new CpCatalogueWriteService(new ConfiguredNeverOpened())
            .DeleteCategoryTemplateAsync(0);
        Assert.False(tplDel.Succeeded);
        Assert.Equal("invalid", tplDel.Code);

        var tplDb = await new CpCatalogueWriteService(new UnconfiguredConnections())
            .DeleteCategoryTemplateAsync(9);
        Assert.False(tplDb.Succeeded);
        Assert.Equal("db", tplDb.Code);

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

        var doneInvalid = await new CpPricesUploadWriteService(new ConfiguredNeverOpened())
            .CompleteSessionAsync(0);
        Assert.False(doneInvalid.Succeeded);
        Assert.Equal("invalid", doneInvalid.Code);

        var doneDb = await new CpPricesUploadWriteService(new UnconfiguredConnections())
            .CompleteSessionAsync(9);
        Assert.False(doneDb.Succeeded);
        Assert.Equal("db", doneDb.Code);

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

        var vendorInvalid = await new CpVendorApprovalWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "approve");
        Assert.False(vendorInvalid.Succeeded);
        Assert.Equal("invalid", vendorInvalid.Code);

        var vendorDb = await new CpVendorApprovalWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "suspend");
        Assert.False(vendorDb.Succeeded);
        Assert.Equal("db", vendorDb.Code);

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

        var wmsInvalid = await new ErpWmsLocationWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(0);
        Assert.False(wmsInvalid.Succeeded);
        Assert.Equal("invalid", wmsInvalid.Code);

        var wmsDb = await new ErpWmsLocationWriteService(new UnconfiguredConnections())
            .DeleteAsync(4);
        Assert.False(wmsDb.Succeeded);
        Assert.Equal("db", wmsDb.Code);

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

        var ctrInvalid = await new ErpContractStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(ctrInvalid.Succeeded);
        Assert.Equal("invalid", ctrInvalid.Code);

        var ctrDb = await new ErpContractStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "active");
        Assert.False(ctrDb.Succeeded);
        Assert.Equal("db", ctrDb.Code);

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

        var insInvalid = await new ErpInsClaimStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(insInvalid.Succeeded);
        Assert.Equal("invalid", insInvalid.Code);

        var insDb = await new ErpInsClaimStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "settled");
        Assert.False(insDb.Succeeded);
        Assert.Equal("db", insDb.Code);

        var vatInvalid = await new ErpBosVatRefundStatusWriteService(new ConfiguredNeverOpened())
            .SetStatusAsync(9, "nope");
        Assert.False(vatInvalid.Succeeded);
        Assert.Equal("invalid", vatInvalid.Code);

        var vatDb = await new ErpBosVatRefundStatusWriteService(new UnconfiguredConnections())
            .SetStatusAsync(9, "refunded");
        Assert.False(vatDb.Succeeded);
        Assert.Equal("db", vatDb.Code);

        var invInvalid = await new ErpSubInvoicePaidWriteService(new ConfiguredNeverOpened())
            .MarkPaidAsync(0);
        Assert.False(invInvalid.Succeeded);
        Assert.Equal("invalid", invInvalid.Code);

        var invDb = await new ErpSubInvoicePaidWriteService(new UnconfiguredConnections())
            .MarkPaidAsync(9);
        Assert.False(invDb.Succeeded);
        Assert.Equal("db", invDb.Code);

        var pfInvalid = await new ErpPfCaseCancelWriteService(new ConfiguredNeverOpened())
            .CancelAsync(0);
        Assert.False(pfInvalid.Succeeded);
        Assert.Equal("invalid", pfInvalid.Code);

        var pfDb = await new ErpPfCaseCancelWriteService(new UnconfiguredConnections())
            .CancelAsync(9);
        Assert.False(pfDb.Succeeded);
        Assert.Equal("db", pfDb.Code);

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

        var consEntInvalid = await new ErpConsDeleteWriteService(new ConfiguredNeverOpened())
            .DeleteEntityAsync(0);
        Assert.False(consEntInvalid.Succeeded);
        Assert.Equal("invalid", consEntInvalid.Code);

        var consEntDb = await new ErpConsDeleteWriteService(new UnconfiguredConnections())
            .DeleteEntityAsync(9);
        Assert.False(consEntDb.Succeeded);
        Assert.Equal("db", consEntDb.Code);

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

        var whtSettleInvalid = await new ErpWhtSettleWriteService(new ConfiguredNeverOpened())
            .SettleAsync(0);
        Assert.False(whtSettleInvalid.Succeeded);
        Assert.Equal("invalid", whtSettleInvalid.Code);

        var whtSettleDb = await new ErpWhtSettleWriteService(new UnconfiguredConnections())
            .SettleAsync(9);
        Assert.False(whtSettleDb.Succeeded);
        Assert.Equal("db", whtSettleDb.Code);
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
}
