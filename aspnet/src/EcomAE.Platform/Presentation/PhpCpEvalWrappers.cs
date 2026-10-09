using System.Globalization;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Eval-safe Control Panel page wrappers closed in this slice (session gate + include-or-alert),
/// verified against PHP 8.3 by <c>Fixtures/CpSmallPages/golden.json</c>.
/// Each method is the wrapper only; the included module body stays its own gap until that file is ported.
/// </summary>
public static class PhpCpEvalWrappers
{
    public const string WebTrackerPath = "cp/content/shop/statistics/web_tracker.php";
    public const string BulkUploadPath = "cp/content/shop/bulk_upload/bulk_upload_hub_page.php";
    public const string PaymentsGuidePath = "cp/content/shop/payments/payments_guide_page.php";
    public const string PaymentsMainPath = "cp/content/shop/payments/payments_main_page.php";
    public const string PosTerminalPath = "cp/content/shop/pos/epc_pos_terminal_page.php";
    public const string ChannelsMainPath = "cp/content/shop/channels/channels_main_page.php";
    public const string MarketingMainPath = "cp/content/shop/marketing/marketing_main_page.php";
    public const string TenantHubPath = "cp/content/shop/tenant_hub/tenant_hub_main_page.php";
    public const string CustomerMgmtMainPath = "cp/content/shop/customer_mgmt/customer_mgmt_main_page.php";
    public const string ProcurementMainPath = "cp/content/shop/procurement/procurement_main_page.php";
    public const string MultivendorUploadPath = "cp/content/shop/prices_upload/multivendor_upload_page.php";
    public const string LogisticsCarriersPath = "cp/content/shop/logistics/logistics_carriers_page.php";
    public const string DocumentControlMainPath = "cp/content/shop/document_control/document_control_main_page.php";
    public const string GuidelinePath = "cp/content/control/cp_guideline_page.php";
    public const string ChannelsGuidePath = "cp/content/shop/channels/channels_guide_page.php";
    public const string LogisticsGuidePath = "cp/content/shop/logistics/logistics_guide_page.php";
    public const string PricesGuidePath = "cp/content/shop/prices_upload/prices_guide_page.php";
    public const string PricesUploadGuideWrapPath = "cp/content/shop/prices_upload/guide.php";
    public const string OmsGuidePath = "cp/content/shop/order_process/oms_daily_guide_page.php";
    public const string FulfilmentGuidePath = "cp/content/shop/order_process/order_fulfilment_guide_page.php";
    public const string WhatsappGuidePath = "cp/content/shop/order_process/whatsapp_guide_page.php";
    public const string RelatedProductsPath = "cp/content/shop/catalogue/related_products.php";
    public const string CustomerMgmtGuideShopPath = "cp/content/shop/customer_mgmt/customer_mgmt_guide.php";
    public const string CustomerMgmtGuideUsersPath = "cp/content/users/customer_mgmt_guide.php";
    public const string AuthSettingsPath = "cp/content/control/portal/epc_cp_auth_settings.php";
    public const string OfficeManagerPath = "content/shop/obtaining_modes/get_in_office/manager_interface.php";
    public const string OfficeActualPath = "content/shop/obtaining_modes/get_in_office/show_actual_info.php";
    public const string OfficeDetailsPath = "content/shop/obtaining_modes/get_in_office/show_details.php";

    public static string LoginUrl(string? backendDir)
        => "/" + StorefrontTinyPages.HtmlSpecialChars(backendDir ?? "") + "/";

    public static string SessionWarning(string? backendDir, string linkText, string afterLink)
        => "<div class=\"alert alert-warning\">Please <a href=\"" + LoginUrl(backendDir) + "\">"
            + linkText + "</a>" + afterLink + "</div>";

    public static string Danger(string innerHtml)
        => "<div class=\"alert alert-danger\">" + innerHtml + "</div>";

    public static string RequireSessionThenInclude(string? backendDir, bool sessionOk, bool includePresent, string includeBody, string linkText, string afterLink, string missingInner)
    {
        if (!sessionOk)
        {
            return SessionWarning(backendDir, linkText, afterLink);
        }

        return includePresent ? includeBody : Danger(missingInner);
    }

    public static string LoadSessionThenInclude(bool includePresent, string includeBody, string missingInner)
        => includePresent ? includeBody : Danger(missingInner);

    public static string PosTerminal(string? backendDir, bool sessionOk, bool includePresent, string includeBody)
    {
        if (!sessionOk)
        {
            return SessionWarning(backendDir, "log in to the control panel", " to open POS Terminal.");
        }

        return includePresent
            ? includeBody
            : Danger("<strong>POS module not found.</strong> Run epc-pos-setup.php on the server.");
    }

    public static string DocumentControl(string? backendDir, bool sessionOk, bool includePresent, string includeBody)
    {
        if (!sessionOk)
        {
            return SessionWarning(backendDir, "log in to the control panel", " to open Document Control.");
        }

        return includePresent
            ? includeBody
            : Danger("<strong>Document Control module not found.</strong> Deploy CP files and run <code>epc-document-control-cp-setup.php?token=…</code> or <code>epc-document-control-cp-setup-all.php?apply=1</code>.");
    }

    public static string EpartsCata(bool shellPresent, string includeBody)
        => shellPresent
            ? includeBody
            : "<div class=\"error_message\">EParts catalog shell is not installed on this host.</div>";

    public static string WebTracker(string includeBody) => includeBody;

    public static string RelatedProducts(string includeBody, Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        // PHP 8 eats the newline after each `?>`; this file is CRLF.
        return "\r\n<div class=\"col-lg-12\">\r\n\t<div class=\"hpanel\">\r\n\t\t<div class=\"panel-heading hbuilt\">\r\n\t\t\t"
            + T(2307) + "\t\t</div>\r\n\t\t<div class=\"panel-body\">\r\n\t\t\t\r\n\t\t\t"
            + T(2996) + "<br/>\r\n\t\t\t" + T(2997) + "<br/>\r\n\t\t\t" + T(2998)
            + "\t\t\t\r\n\t\t</div>\r\n\t</div>\r\n</div>\r\n\r\n" + includeBody;
    }

    public static string PricesUploadGuideTry(bool includePresent, string includeBody, string errorMessage)
        => includePresent
            ? includeBody
            : Danger("<strong>Guide could not load:</strong> " + StorefrontTinyPages.HtmlSpecialChars(errorMessage));

    public static string AuthSettings(bool superHost, bool isAdmin, bool includePresent, string includeBody, string? backendDir = "cp")
    {
        if (!superHost)
        {
            return "<div class=\"col-lg-12\"><div class=\"alert alert-warning\">Modern auth settings are available on ECOM AE Super CP only.</div></div>";
        }

        var denied = PhpCpSmallScripts.PageRequireAdmin(isAdmin, "Modern auth settings", backendDir);
        if (denied is not null)
        {
            return denied;
        }

        return PhpCpSmallScripts.PageInclude(
            includePresent,
            includeBody,
            "Modern auth settings module missing. Deploy via <code>tools/push_one.py</code>.");
    }

    public static string OfficeObtain(string kind, string captionId, string includeBody, Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        var caption = "{" + captionId + "}";
        return kind == "manager"
            ? "<p>" + T(3507) + " - <b>" + caption + "</b></p>\r\n" + includeBody
            : "<p class=\"lead\">" + T(3507) + " - " + caption + "</p>\r\n" + includeBody;
    }

    public static string CustomerMgmtGuide(string approvalsUrl, string ordersUrl, string erpUrl)
    {
        string H(string value) => StorefrontTinyPages.HtmlSpecialChars(value);
        return "\n<h4><i class=\"fa fa-sitemap\"></i> Customer lifecycle</h4>\n<ol>\n"
            + "\t<li><strong>Registration</strong> — Customer registers on shop. B2B: approve via <a href=\""
            + H(approvalsUrl) + "\">Approvals</a> tab.</li>\n"
            + "\t<li><strong>Customer profile</strong> — Tab <em>Customers</em>: buyer name, TRN, address, Peppol endpoint (UAE e-invoicing mandatory fields for B2B).</li>\n"
            + "\t<li><strong>Orders</strong> — Tab <em>Orders</em> or <a href=\""
            + H(ordersUrl) + "\">CP Orders</a>. Sale prices ex VAT; 5% output VAT on UAE sales.</li>\n"
            + "\t<li><strong>Advance payment</strong> — Tab <em>Advances</em>: record customer prepayment (credit on customer ledger).</li>\n"
            + "\t<li><strong>Tax invoice</strong> — Tab <em>Invoices</em>: generate UAE e-invoice (PINT-AE) from order. Full ASP submission in <a href=\""
            + H(erpUrl) + "?tab=einvoice\">ERP E-Invoicing</a>.</li>\n"
            + "\t<li><strong>Returns</strong> — Tab <em>Returns</em>: view return requests; process in Orders CP.</li>\n"
            + "</ol>\n\n<h4><i class=\"fa fa-file-text-o\"></i> Mandatory e-invoice buyer fields</h4>\n"
            + "<p>For B2B UAE customers, complete on the customer profile: buyer name, TRN, legal registration, address line 1, city, emirate, country AE, Peppol electronic address (0235:TIN).</p>\n\n"
            + "<h4>Where this differs from shop menus</h4>\n"
            + "<p>Customer-related settings were scattered across Users, Orders, and Finance. This panel centralises customer master data, orders overview, invoices, advances, and returns in one place.</p>\n";
    }
}
