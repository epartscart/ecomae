using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards new-customer → order journey twins for epartscart (PHP reference parity).</summary>
[Collection(PreferAspNetAppsCollection.Name)]
public sealed class EpartscartCustomerJourneyParityTests
{
    [Fact]
    public void RegistrationAndLoginAliasesExist()
    {
        Assert.Equal("/storefront/register-app", StorefrontAspNetCanonical.Registration);
        Assert.Equal("/en/users/registration", StorefrontPhpCanonical.Registration);
        Assert.Equal("/storefront/login", StorefrontAspNetCanonical.Login);

        var reg = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontRegisterApp.razor"));
        Assert.Contains("@page \"/storefront/register-app\"", reg, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/users/registration\"", reg, StringComparison.Ordinal);
        Assert.Contains("/php-reference/en/users/register", reg, StringComparison.Ordinal);
        Assert.Contains("id=\"regform\"", reg, StringComparison.Ordinal);
        Assert.Contains("name=\"reg_contact\"", reg, StringComparison.Ordinal);
        Assert.Contains("name=\"reg_contact_type\"", reg, StringComparison.Ordinal);
        Assert.Contains("name=\"password_repeat\"", reg, StringComparison.Ordinal);
        Assert.Contains("epc-auth-page", reg, StringComparison.Ordinal);

        var login = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontLoginApp.razor"));
        Assert.Contains("@page \"/en/users/login\"", login, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.Registration", login, StringComparison.Ordinal);

        var forgot = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontForgotPasswordApp.razor"));
        Assert.Contains("id=\"forgot_password_contact_select\"", forgot, StringComparison.Ordinal);
        Assert.Contains("id=\"forgot_password_contact_input\"", forgot, StringComparison.Ordinal);
        Assert.Contains("name=\"forgot_password_contact\"", forgot, StringComparison.Ordinal);
        Assert.Contains("epc-auth-page", forgot, StringComparison.Ordinal);
    }

    [Fact]
    public void ChromeRegisterUsesSurfaceLinksNotDeadEnPath()
    {
        var chrome = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("StorefrontSurfaceLinks.Registration", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/en/users/registration\"", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckoutAndOrdersKeepPhpCanonicalAliases()
    {
        var checkout = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCheckoutApp.razor"));
        Assert.Contains("@page \"/en/shop/checkout/how_get\"", checkout, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/shop/checkout/confirm\"", checkout, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.CheckoutHowGet", checkout, StringComparison.Ordinal);
        Assert.Contains("panel panel-primary", checkout, StringComparison.Ordinal);
        Assert.Contains("login_offer", checkout, StringComparison.Ordinal);
        Assert.Contains("Continue as guest", checkout, StringComparison.Ordinal);
        Assert.Contains("class=\"lead\"", checkout, StringComparison.Ordinal);
        Assert.Contains("how_get_radio_", checkout, StringComparison.Ordinal);
        Assert.Contains("radio_how_get", checkout, StringComparison.Ordinal);
        Assert.Contains("label_how_get", checkout, StringComparison.Ordinal);
        Assert.Contains("id=\"how_get_options_div\"", checkout, StringComparison.Ordinal);
        Assert.Contains("name=\"how_get_radio\"", checkout, StringComparison.Ordinal);
        Assert.Contains("office_box", checkout, StringComparison.Ordinal);
        Assert.Contains("list-group", checkout, StringComparison.Ordinal);
        Assert.Contains("office_info", checkout, StringComparison.Ordinal);
        Assert.Contains("BuildCpOfficesDigestAsync", checkout, StringComparison.Ordinal);
        Assert.Contains("name=\"office_id\"", checkout, StringComparison.Ordinal);
        Assert.Contains("name=\"officeId\"", checkout, StringComparison.Ordinal);
        Assert.Contains("onHowGetChanged", checkout, StringComparison.Ordinal);
        Assert.Contains("EpcObtainModes.GetInOffice", checkout, StringComparison.Ordinal);
        Assert.Contains("EpcObtainModes.EpcCarriers", checkout, StringComparison.Ordinal);
        Assert.Contains("showOfficeInfo", checkout, StringComparison.Ordinal);
        Assert.Contains("epcCarrierNext", checkout, StringComparison.Ordinal);
        Assert.Contains("id=\"message_textarea\"", checkout, StringComparison.Ordinal);
        Assert.Contains("id=\"confirm_btn\"", checkout, StringComparison.Ordinal);
        Assert.Contains("EpcObtainModes.HasCustomerInterface", checkout, StringComparison.Ordinal);
        Assert.Contains("class=\"table\"", checkout, StringComparison.Ordinal);
        Assert.Contains("product_div_first", checkout, StringComparison.Ordinal);
        Assert.Contains("product_div_last", checkout, StringComparison.Ordinal);
        Assert.Contains("product_div_single", checkout, StringComparison.Ordinal);

        var orders = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOrdersApp.razor"));
        Assert.Contains("@page \"/en/shop/orders\"", orders, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/shop/orders/order\"", orders, StringComparison.Ordinal);
        Assert.Contains("panel panel-primary", orders, StringComparison.Ordinal);
        Assert.Contains("id=\"time_from\"", orders, StringComparison.Ordinal);
        Assert.Contains("id=\"time_from_show\"", orders, StringComparison.Ordinal);
        Assert.Contains("id=\"order_id\"", orders, StringComparison.Ordinal);
        Assert.Contains("id=\"status-select\"", orders, StringComparison.Ordinal);
        Assert.Contains("box_btn_filter", orders, StringComparison.Ordinal);
        Assert.Contains("filterOrders", orders, StringComparison.Ordinal);
        Assert.Contains("my_orders_filter", orders, StringComparison.Ordinal);
        Assert.Contains("class=\"table\"", orders, StringComparison.Ordinal);
        Assert.Contains("id=\"pay_form\"", orders, StringComparison.Ordinal);
        Assert.Contains("pay_on_place", orders, StringComparison.Ordinal);
        Assert.Contains("car_tr_", orders, StringComparison.Ordinal);
        Assert.Contains("PhpCustomerWrites.GarageCheckCarHref", orders, StringComparison.Ordinal);
        Assert.Contains("function check_car", orders, StringComparison.Ordinal);

        var cart = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCartApp.razor"));
        Assert.Contains("id=\"cart_area\"", cart, StringComparison.Ordinal);
        Assert.Contains("id=\"check_uncheck_all\"", cart, StringComparison.Ordinal);
        Assert.Contains("count_need_", cart, StringComparison.Ordinal);
        Assert.Contains("epc-wa-share-btn", cart, StringComparison.Ordinal);
        Assert.Contains("btn btn-ar btn-primary", cart, StringComparison.Ordinal);
        Assert.Contains("CheckoutHowGet", cart, StringComparison.Ordinal);

        var profile = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontProfileApp.razor"));
        Assert.Contains("panel panel-primary", profile, StringComparison.Ordinal);
        Assert.Contains("id=\"phone_code_store\"", profile, StringComparison.Ordinal);
        Assert.Contains("id=\"email_work\"", profile, StringComparison.Ordinal);
        Assert.Contains("id=\"phone_work\"", profile, StringComparison.Ordinal);
        Assert.Contains("id=\"regform\"", profile, StringComparison.Ordinal);
        Assert.Contains("id=\"additional_fields_div\"", profile, StringComparison.Ordinal);
        Assert.Contains("id=\"RegVariantsSelector\"", profile, StringComparison.Ordinal);
        Assert.Contains("id=\"reg_variant_selector\"", profile, StringComparison.Ordinal);
        Assert.Contains("name=\"reg_variant\"", profile, StringComparison.Ordinal);
        Assert.Contains("id=\"password\"", profile, StringComparison.Ordinal);
        Assert.Contains("id=\"password_repeat\"", profile, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", profile, StringComparison.Ordinal);
        Assert.Contains("name=\"epc_reg_city\"", profile, StringComparison.Ordinal);
        Assert.Contains("class=\"table\"", profile, StringComparison.Ordinal);

        var news = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontNewsApp.razor"));
        Assert.Contains("news_block", news, StringComparison.Ordinal);
        Assert.Contains("id=\"bottom_pagination_div\"", news, StringComparison.Ordinal);

        var guestOrder = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontGuestOrderApp.razor"));
        Assert.Contains("panel panel-primary", guestOrder, StringComparison.Ordinal);
        Assert.Contains("name=\"order_id\"", guestOrder, StringComparison.Ordinal);
        Assert.Contains("name=\"email_not_auth\"", guestOrder, StringComparison.Ordinal);
        Assert.Contains("name=\"phone_not_auth\"", guestOrder, StringComparison.Ordinal);

        var offices = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOfficesApp.razor"));
        Assert.Contains("class=\"office_box list-group-item\"", offices, StringComparison.Ordinal);
        Assert.Contains("id=\"office_list\"", offices, StringComparison.Ordinal);

        var garage = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontGarageApp.razor"));
        Assert.Contains("epc-gl", garage, StringComparison.Ordinal);
        Assert.Contains("Garage Manager login", garage, StringComparison.Ordinal);
        Assert.Contains("/garage/login", garage, StringComparison.Ordinal);
        Assert.Contains("epc-garazh-gms", garage, StringComparison.Ordinal);
        Assert.Contains("id=\"garage_search_input\"", garage, StringComparison.Ordinal);
        Assert.Contains("car_div", garage, StringComparison.Ordinal);

        var product = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontProductApp.razor"));
        Assert.Contains("id=\"product_info_wrap_div\"", product, StringComparison.Ordinal);
        Assert.Contains("product_galery", product, StringComparison.Ordinal);
        Assert.Contains("product_genaral_info", product, StringComparison.Ordinal);

        var wishlist = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontWishlistApp.razor"));
        Assert.Contains("product_div_tile", wishlist, StringComparison.Ordinal);
        Assert.Contains("product_div_bookmark", wishlist, StringComparison.Ordinal);

        var compare = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCompareApp.razor"));
        Assert.Contains("id=\"category_select\"", compare, StringComparison.Ordinal);
        Assert.Contains("table-nonfluid", compare, StringComparison.Ordinal);
        Assert.Contains("product_div_compare", compare, StringComparison.Ordinal);
        Assert.Contains("id=\"work_area\"", compare, StringComparison.Ordinal);

        var chrome = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("navbar-fixed-bottom", chrome, StringComparison.Ordinal);
        Assert.Contains("id=\"compare_count\"", chrome, StringComparison.Ordinal);
        Assert.Contains("id=\"bookmarks_count\"", chrome, StringComparison.Ordinal);
        Assert.Contains("id=\"cart_items_count\"", chrome, StringComparison.Ordinal);

        var newsletter = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontNewsletterApp.razor"));
        Assert.Contains("epc-wc-newsletter", newsletter, StringComparison.Ordinal);
        Assert.Contains("epc-wc-newsletter__form", newsletter, StringComparison.Ordinal);
        Assert.Contains("name=\"email\"", newsletter, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", newsletter, StringComparison.Ordinal);

        var returns = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontReturnsApp.razor"));
        Assert.Contains("panel panel-primary", returns, StringComparison.Ordinal);
        Assert.Contains("id=\"search-text\"", returns, StringComparison.Ordinal);
        Assert.Contains("id=\"orders_returns_table\"", returns, StringComparison.Ordinal);
        Assert.Contains("return_options_data", returns, StringComparison.Ordinal);
        Assert.Contains("id=\"chat_block\"", returns, StringComparison.Ordinal);
        Assert.Contains("id=\"new_message_area\"", returns, StringComparison.Ordinal);
        Assert.Contains("name=\"order_id\"", returns, StringComparison.Ordinal);
        Assert.Contains("name=\"item_id\"", returns, StringComparison.Ordinal);
        Assert.Contains("name=\"reason_id\"", returns, StringComparison.Ordinal);
        Assert.Contains("name=\"comment\"", returns, StringComparison.Ordinal);

        var account = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontAccountSummaryApp.razor"));
        Assert.Contains("panel panel-primary", account, StringComparison.Ordinal);
        Assert.Contains("id=\"money_value\"", account, StringComparison.Ordinal);
        Assert.Contains("id=\"epc_pay_handler\"", account, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", account, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", account, StringComparison.Ordinal);
        Assert.Contains("class=\"table", account, StringComparison.Ordinal);
        Assert.Contains("my_account_operations_filter", account, StringComparison.Ordinal);
        Assert.Contains("filterOperations", account, StringComparison.Ordinal);
        Assert.Contains("id=\"time_from\"", account, StringComparison.Ordinal);
        Assert.Contains("id=\"operation_code\"", account, StringComparison.Ordinal);
        Assert.Contains("id=\"id_sorter\"", account, StringComparison.Ordinal);

        var payment = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPaymentApp.razor"));
        Assert.Contains("panel panel-primary", payment, StringComparison.Ordinal);
        Assert.Contains("id=\"money_value\"", payment, StringComparison.Ordinal);
        Assert.Contains("id=\"epc_pay_handler\"", payment, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", payment, StringComparison.Ordinal);
        Assert.Contains("name=\"pay_handler\"", payment, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", payment, StringComparison.Ordinal);
        Assert.Contains("action=\"/storefront/payment/create-operation\"", payment, StringComparison.Ordinal);

        var seller = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSellerRequestApp.razor"));
        Assert.Contains("id=\"requestSeller\"", seller, StringComparison.Ordinal);
        Assert.Contains("request-seller", seller, StringComparison.Ordinal);
        Assert.Contains("section-form", seller, StringComparison.Ordinal);
        Assert.Contains("name=\"@field.Name\"", seller, StringComparison.Ordinal);
        Assert.Contains("PhpSellerRequest.Fields", seller, StringComparison.Ordinal);
        Assert.Contains("name=\"client_parts\"", seller, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", seller, StringComparison.Ordinal);
        var sellerFields = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/PhpSellerRequest.cs"));
        Assert.Contains("client_vin", sellerFields, StringComparison.Ordinal);
        Assert.Contains("client_parts", seller, StringComparison.Ordinal);

        var requests = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCustomerRequestsApp.razor"));
        Assert.Contains("panel panel-primary", requests, StringComparison.Ordinal);
        Assert.Contains("box_btn_filter", requests, StringComparison.Ordinal);
        Assert.Contains("id=\"chat_block\"", requests, StringComparison.Ordinal);
        Assert.Contains("id=\"new_message_area\"", requests, StringComparison.Ordinal);
        Assert.Contains("name=\"vin_id\"", requests, StringComparison.Ordinal);
        Assert.Contains("class=\"table\"", requests, StringComparison.Ordinal);

        var print = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCustomerPrintApp.razor"));
        Assert.Contains("panel panel-primary", print, StringComparison.Ordinal);
        Assert.Contains("name=\"order_id\"", print, StringComparison.Ordinal);
        Assert.Contains("name=\"doc_name\"", print, StringComparison.Ordinal);
        Assert.Contains("btn btn-ar btn-primary", print, StringComparison.Ordinal);

        var special = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSpecialSearchApp.razor"));
        Assert.Contains("list-group", special, StringComparison.Ordinal);
        Assert.Contains("list-group-item", special, StringComparison.Ordinal);

        var garageMgr = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontGarageManagerApp.razor"));
        Assert.Contains("panel panel-primary", garageMgr, StringComparison.Ordinal);
        Assert.Contains("name=\"customer_name\"", garageMgr, StringComparison.Ordinal);
        Assert.Contains("name=\"plate\"", garageMgr, StringComparison.Ordinal);

        var brochure = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontBrochureApp.razor"));
        Assert.Contains("panel panel-default", brochure, StringComparison.Ordinal);

        var workshop = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontAutoWorkshopApp.razor"));
        Assert.Contains("panel panel-primary", workshop, StringComparison.Ordinal);
        Assert.Contains("name=\"customer_name\"", workshop, StringComparison.Ordinal);
        Assert.Contains("name=\"plate\"", workshop, StringComparison.Ordinal);
        Assert.Contains("name=\"complaint\"", workshop, StringComparison.Ordinal);
        Assert.Contains("name=\"ref\"", workshop, StringComparison.Ordinal);
        Assert.Contains("id=\"book\"", workshop, StringComparison.Ordinal);
        Assert.Contains("id=\"track\"", workshop, StringComparison.Ordinal);

        var sitemap = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSitemapApp.razor"));
        Assert.Contains("list-group", sitemap, StringComparison.Ordinal);
        Assert.Contains("list-group-item", sitemap, StringComparison.Ordinal);

        var vendorReg = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVendorRegisterApp.razor"));
        Assert.Contains("id=\"epc-vendor-register-form\"", vendorReg, StringComparison.Ordinal);
        Assert.Contains("epc-vp__card--wide", vendorReg, StringComparison.Ordinal);
        Assert.Contains("name=\"email\"", vendorReg, StringComparison.Ordinal);
        Assert.Contains("name=\"legal_name\"", vendorReg, StringComparison.Ordinal);
        Assert.Contains("name=\"trn\"", vendorReg, StringComparison.Ordinal);
        Assert.Contains("name=\"vendor_short\"", vendorReg, StringComparison.Ordinal);
        Assert.Contains("name=\"postal_code\"", vendorReg, StringComparison.Ordinal);
        Assert.Contains("id=\"epc_vendor_emirate\"", vendorReg, StringComparison.Ordinal);

        var vendorPortal = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVendorPortalApp.razor"));
        Assert.Contains("id=\"epc-vendor-portal\"", vendorPortal, StringComparison.Ordinal);
        Assert.Contains("epc-vp__login panel panel-primary", vendorPortal, StringComparison.Ordinal);
        Assert.Contains("epc-vp__dash", vendorPortal, StringComparison.Ordinal);
        Assert.Contains("Not a vendor yet", vendorPortal, StringComparison.Ordinal);
        Assert.Contains("Account pending", vendorPortal, StringComparison.Ordinal);
        Assert.Contains("UAE e-invoice seller profile", vendorPortal, StringComparison.Ordinal);
        Assert.Contains("epc-login-html-form", File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Shared/LegacyAdminLoginForm.razor")), StringComparison.Ordinal);

        var vendorUpload = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVendorUploadApp.razor"));
        Assert.Contains("id=\"epc-vendor-upload\"", vendorUpload, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-vp-upload-form\"", vendorUpload, StringComparison.Ordinal);
        Assert.Contains("name=\"data_type\"", vendorUpload, StringComparison.Ordinal);
        Assert.Contains("name=\"price_file\"", vendorUpload, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-vp-submit\"", vendorUpload, StringComparison.Ordinal);

        var partsExpert = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontAiPartsExpertApp.razor"));
        Assert.Contains("id=\"epc-ai-expert-form\"", partsExpert, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-ai-expert-article\"", partsExpert, StringComparison.Ordinal);
        Assert.Contains("name=\"article\"", partsExpert, StringComparison.Ordinal);
        Assert.Contains("name=\"brand\"", partsExpert, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantSqlPrefersShopDatabaseForWwwAlias()
    {
        var sql = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Data/PortalTenantSql.cs"));
        Assert.Contains("IFNULL(TRIM(`db_name`), '') <> ''", sql, StringComparison.Ordinal);
        Assert.Contains("`erp_only_shared` ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`erp_only_shared` DESC", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void JourneyRecoverScriptExists()
    {
        Assert.True(File.Exists(Find("scripts/cloudpanel_EPARTSCART_CUSTOMER_JOURNEY_RECOVER.sh")));
        Assert.True(File.Exists(Find("docs/migration/evidence/storefront/epartscart-customer-journey-parity.json")));
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
