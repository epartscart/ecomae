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
}
