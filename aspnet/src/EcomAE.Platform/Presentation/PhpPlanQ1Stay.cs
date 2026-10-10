using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-stay CP page assets. PHP identifiers kept for the inventory:
/// <c>epc_cp_page_asset_version</c>, <c>epc_cp_page_asset_url_map</c>,
/// <c>epc_erp_shell_nav_js_src</c>, <c>epc_cp_page_assets_for_url</c>,
/// <c>epc_cp_page_head_assets</c>, <c>epc_cp_apai_discover_tab_key</c>,
/// <c>epc_cp_apai_inline_discover_config_script</c>,
/// <c>epc_cp_apai_shell_config_script</c>, <c>epc_cp_page_footer_scripts</c>.
/// GET never mints a session cookie. Leftover version / ajax / host / ERP-nav
/// parents stay injected.
/// </summary>
public static class PhpPlanQ1Stay
{
    public const string PageAssetsPath = "content/general_pages/epc_cp_page_assets.php";

    private const string Php = ".php";

    private static readonly Regex ErpUrl = new(@"^shop/finance/erp(?:/|$)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Func<string>? Version { get; set; }
    public static Func<string>? ErpNavJs { get; set; }
    public static Func<string, string>? AjaxUrl { get; set; }
    public static Func<string>? PortalHost { get; set; }
    public static string? BackendDir { get; set; }
    public static string Host { get; set; } = "";
    public static Dictionary<string, string> Get { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, Dictionary<string, object?>>? ExtraAssets { get; set; }

    public static void Reset()
    {
        Version = null;
        ErpNavJs = () => "/content/shop/finance/erp-shell-nav.js?v=nav1";
        AjaxUrl = null;
        PortalHost = () => Host;
        BackendDir = null;
        Host = "";
        Get = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtraAssets = null;
    }

    public static string EpcCpPageAssetVersion()
        => Version != null ? Version() : "20260607ssftoggle1";

    public static string EpcErpShellNavJsSrc()
        => ErpNavJs != null ? ErpNavJs() : "";

    public static Dictionary<string, Dictionary<string, List<string>>> EpcCpPageAssetUrlMap()
    {
        var ver = EpcCpPageAssetVersion();
        var backend = Backend();
        string Gp(string stem, string suffix = "")
            => "/content/general_pages/" + stem + Php + "?v=" + Raw(ver + suffix);
        string ShopPhp(string stem, string suffix = "")
            => "/content/shop/" + stem + Php + "?v=" + Raw(ver + suffix);
        string B(string path, string suffix = "")
            => "/" + backend + path + "?v=" + Raw(ver + suffix);
        string Bp(string path, string suffix = "")
            => "/" + backend + path + Php + "?v=" + Raw(ver + suffix);
        string Css(string path, string suffix = "")
            => path + "?v=" + Raw(ver + suffix);

        var ordersConfig = Bp("/content/shop/order_process/orders_config", "omsAlt1");
        if (Get.ContainsKey("order_id"))
        {
            ordersConfig += "&order_id=" + PhpInt(Get["order_id"]).ToString();
        }

        if (Get.ContainsKey("status_id"))
        {
            ordersConfig += "&status_id=" + PhpInt(Get["status_id"]).ToString();
        }

        return new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal)
        {
            ["control/communications"] = Pack([Gp("epc_comms_notify_css", "cn1")], [B("/content/control/epc_comms_notify.js", "cn1")]),
            ["control/notifications_settings"] = Pack([Gp("epc_comms_notify_css", "cn1")], [B("/content/control/epc_comms_notify.js", "cn1")]),
            ["control/notifications_settings/notification"] = Pack([Gp("epc_comms_notify_css", "cn1")], [B("/content/control/epc_comms_notify.js", "cn1")]),
            ["control/config"] = Pack(
                [Gp("epc_config_edit_css", "cfg6")],
                [B("/content/control/epc_config_edit.js", "cfg6"), B("/content/control/epc_config_edit_status.js", "cfg6")]),
            ["control/cp-guideline"] = Pack([Gp("epc_cp_guideline_css")]),
            ["control/portal/industry_settings"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_industry_settings_css", "inds3")],
                [Bp("/content/control/portal/industry_settings_config", "inds3"), B("/content/control/portal/industry_settings.js", "inds3")]),
            ["control/portal/epc_tax_toolkit_manage"] = Pack([Gp("epc_portal_settings_css"), Gp("epc_tax_toolkit_manage_css")]),
            ["control/portal/epc_visual_page_editor"] = Pack([Gp("epc_visual_page_editor_css", "vpe1")]),
            ["control/portal/epc_pos_tenant_manage"] = Pack(
                [Css("/content/shop/finance/epc_erp_ui.css"), ShopPhp("pos/epc_pos_css")],
                [ShopPhp("pos/epc_pos_tenant_manage_js")]),
            ["control/portal/epc_platform_governance"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")],
                [Bp("/content/control/portal/epc_platform_governance_config"), B("/content/control/portal/epc_platform_governance.js")]),
            ["control/portal/epc_platform_health_checkup"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")],
                [Bp("/content/control/portal/epc_platform_health_checkup_config"), B("/content/control/portal/epc_platform_health_checkup.js")]),
            ["shop/statistics/web_tracker"] = Pack(
                [Gp("epc_web_tracker_cp_css", "wt6")],
                [Bp("/content/control/portal/epc_web_tracker_config", "wt6"), B("/content/control/portal/epc_web_tracker_cp.js", "wt6")]),
            ["control/portal/epc_web_tracker"] = Pack(
                [Gp("epc_web_tracker_cp_css", "wt6")],
                [Bp("/content/control/portal/epc_web_tracker_config", "wt6"), B("/content/control/portal/epc_web_tracker_cp.js", "wt6")]),
            ["control/portal/epc_power_bi"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")],
                []),
            ["control/portal/epc_power_bi_guide"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")],
                []),
            ["control/portal/epc_api_documentation_guide"] = Pack([Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")]),
            ["control/portal/epc_platform_failover_guide"] = Pack([Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")]),
            ["control/portal/epc_auto_price_engine"] = Pack(
                [Gp("epc_auto_price_engine_css", "disc1")],
                [
                    Gp("epc_auto_price_shell_js", "disc1"),
                    B("/content/control/portal/epc_auto_price_discover.js", "disc1"),
                    B("/content/control/portal/epc_auto_price_imports.js", "disc1"),
                    B("/content/control/portal/epc_auto_price_product_lines.js", "disc1"),
                    B("/content/control/portal/epc_auto_price_sources.js", "disc1"),
                    B("/content/control/portal/epc_auto_price_compare.js", "disc1")
                ]),
            ["control/portal/epc_auto_price_guide"] = Pack([Gp("epc_auto_price_engine_css", "disc1")]),
            ["control/portal/epc_erp_only_onboard_guide"] = Pack([Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")]),
            ["control/portal/epc_custom_shipping_guide"] = Pack([Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")]),
            ["control/portal/epc_api_clients_manage"] = Pack([Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")]),
            ["control/portal/epc_integrations_hub"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css"), Gp("epc_integrations_hub_css", "inthub3")],
                [
                    Bp("/content/control/portal/epc_integrations_hub_config"),
                    B("/content/control/portal/epc_integrations_hub.js"),
                    B("/content/control/portal/epc_integrations_hub_ui.js", "inthub3")
                ]),
            ["control/portal/epc_integrations_guide"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css"), Gp("epc_integrations_hub_css", "inthub3")],
                []),
            ["control/portal/epc_social_media_hub"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css"), Gp("epc_social_media_hub_css", "socPub2")],
                [Gp("epc_social_media_hub_config", "socPub2"), Gp("epc_social_media_hub_js", "socPub2")]),
            ["control/portal/epc_marketing_broadcast"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css"), Gp("epc_marketing_broadcast_css", "mb2")],
                [Gp("epc_marketing_broadcast_config", "mb2"), Gp("epc_marketing_broadcast_js", "mb2")]),
            ["shop/marketing/marketing"] = Pack(
                [Css("/content/shop/finance/epc_erp_ui.css"), Gp("epc_marketing_hub_css", "mktHub2")],
                [Gp("epc_marketing_hub_js", "mktHub2")]),
            ["shop/payments/payments"] = Pack(js: [Gp("epc_payments_cp_js", "payAcc2")]),
            ["control/portal/epc_mobile_apps"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")],
                [Bp("/content/control/portal/epc_mobile_apps_config"), B("/content/control/portal/epc_integrations_hub.js")]),
            ["control/portal/epc_tenant_features"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")],
                [Bp("/content/control/portal/epc_tenant_features_config"), B("/content/control/portal/epc_integrations_hub.js")]),
            ["control/portal/epc_tenant_email_settings"] = Pack(
                [Gp("epc_portal_settings_css"), Gp("epc_portal_module_pages_css")],
                [Bp("/content/control/portal/epc_tenant_email_settings_config"), B("/content/control/portal/epc_integrations_hub.js")]),
            ["shop/tenant_hub/tenant_hub"] = Pack([Gp("epc_portal_settings_css")]),
            ["shop/pos/terminal"] = Pack(
                [Css("/content/shop/finance/epc_erp_ui.css"), ShopPhp("pos/epc_pos_css", "posui1")],
                [ShopPhp("pos/epc_pos_terminal_js", "posui1")]),
            ["shop/orders/items"] = Pack(
                [
                    "/lib/datetimepicker/jquery.datetimepicker.css",
                    "/lib/multiple_select/multiple-select.css",
                    "/" + backend + "/content/users/statistics/assets/modal.css",
                    Gp("epc_orders_cp_css", "omsForms2")
                ],
                [
                    "/lib/datetimepicker/jquery.datetimepicker.js",
                    "/lib/multiple_select/jquery.multiple.select.js",
                    "/" + backend + "/content/users/statistics/assets/main.js",
                    Bp("/content/shop/order_process/orders_items_config", "omsForms2"),
                    B("/content/shop/order_process/orders_items_cp.js", "omsForms2")
                ]),
            ["shop/orders/items/edit"] = Pack(js:
            [
                Bp("/content/shop/order_process/orders_items_edit_config"),
                B("/content/shop/order_process/orders_items_edit_cp.js")
            ]),
            ["shop/orders/oms-guide"] = Pack([Gp("epc_orders_cp_css", "omsGuide4")]),
            ["shop/orders/orders"] = Pack(
                [
                    "/lib/datetimepicker/jquery.datetimepicker.css",
                    "/lib/multiple_select/multiple-select.css",
                    "/" + backend + "/content/users/statistics/assets/modal.css",
                    Gp("epc_orders_cp_css", "omsAlt1")
                ],
                [
                    "/lib/datetimepicker/jquery.datetimepicker.js",
                    "/lib/multiple_select/jquery.multiple.select.js",
                    "/" + backend + "/content/users/statistics/assets/main.js",
                    ordersConfig,
                    B("/content/shop/order_process/orders_cp.js", "omsAlt1"),
                    B("/content/shop/order_process/epc_orders_fulfillment.js", "omsAlt1"),
                    B("/content/shop/order_process/epc_orders_supplier_fulfillment.js", "omsAlt1")
                ]),
            ["shop/finance/account_operations"] = Pack(
                [
                    "/lib/datetimepicker/jquery.datetimepicker.css",
                    "/" + backend + "/content/users/statistics/assets/modal.css",
                    B("/content/shop/finance/epc_account_operations.css", "aoFix3")
                ],
                [
                    "/lib/datetimepicker/jquery.datetimepicker.js",
                    "/" + backend + "/content/users/statistics/assets/main.js",
                    B("/content/shop/finance/account_operations_cp.js", "aoFix3")
                ]),
            ["shop/orders/statuses"] = Pack([Gp("epc_statuses_cp_css")]),
            ["shop/orders/order"] = Pack(
                [Gp("epc_orders_cp_css", "omsForms2")],
                [Bp("/content/shop/order_process/order_card_config", "omsForms2"), B("/content/shop/order_process/order_card_cp.js", "omsForms2")]),
            ["shop/channels/channels"] = Pack(
                [Css("/content/general_pages/epc_channels_hub.css", "chHub1")],
                [B("/content/shop/channels/channels_hub.js", "chHub1")]),
            ["shop/logistics/carriers"] = Pack(
                [Css("/content/shop/finance/epc_erp_ui.css", "lc3"), Css("/content/general_pages/epc_logistics_carriers.css", "lc3")],
                [B("/content/shop/logistics/logistics_carriers.js", "lc3")]),
            ["shop/logistics/storages"] = Pack(js: [B("/content/shop/epc_cp_table_filter.js", "cpFull2")]),
            ["shop/logistics/storages/storage"] = Pack(
                ["/lib/multiple_select/multiple-select.css"],
                ["/lib/multiple_select/jquery.multiple.select.js", B("/content/shop/logistics/storage_users_ms.js", "storMs1")]),
            ["shop/quote-requests"] = Pack(js: [B("/content/shop/quote_requests/quote_alt_offer.js", "qAlt2")]),
            ["shop/bulk_upload"] = Pack(
                [Css("/content/general_pages/epc_bulk_cp.css", "bu1")],
                [B("/content/shop/bulk_upload/bulk_upload_hub.js", "bu1")]),
            ["shop/parts_agent_chats"] = Pack(
                [Gp("epc_agent_cp_css", "pacFix3")],
                [Css("/content/shop/parts_agent/parts_agent_chats_config" + Php, "pacFix3"), Gp("epc_agent_cp_js", "pacFix3")]),
            ["shop/accessories"] = Pack([Gp("epc_accessories_cp_css", "accPhotos1")]),
            ["shop/crosses"] = Pack([Gp("epc_crosses_cp_css", "cx1")]),
            ["shop/prices"] = Pack(
                [Gp("epc_prices_cp_css")],
                [
                    B("/content/shop/epc_cp_table_filter.js", "cpFull2"),
                    Bp("/content/shop/prices_upload/epc_prices_upload_history_config"),
                    B("/content/shop/prices_upload/epc_prices_upload_history.js"),
                    Bp("/content/shop/prices_upload/epc_storefront_storage_toggle_config"),
                    B("/content/shop/prices_upload/epc_storefront_storage_toggle.js")
                ]),
            ["shop/prices/price"] = Pack(
                [Gp("epc_prices_cp_css")],
                [
                    Bp("/content/shop/prices_upload/epc_prices_upload_history_config"),
                    B("/content/shop/prices_upload/epc_prices_upload_history.js")
                ]),
            ["shop/prices/guide"] = Pack(
                [Gp("epc_prices_cp_css")],
                [
                    Bp("/content/shop/prices_upload/epc_prices_upload_history_config"),
                    B("/content/shop/prices_upload/epc_prices_upload_history.js")
                ]),
            ["shop/prices/multivendor"] = Pack(
                [Gp("epc_prices_cp_css", "mvMin1")],
                [
                    Bp("/content/shop/prices_upload/epc_multivendor_cp_config", "mvMin1"),
                    B("/content/shop/prices_upload/epc_multivendor_cp.js", "mvMin1")
                ]),
            ["shop/catalogue/sku_media"] = Pack(
                [Css("/content/shop/catalogue/epc_sku_media.css", "skuMedia4")],
                [
                    Bp("/content/shop/catalogue/epc_sku_media_cp_config", "skuMedia4"),
                    Css("/content/shop/catalogue/epc_sku_media.js", "skuMedia4")
                ]),
            ["filemanager"] = Pack(
                [
                    "/" + backend + "/lib/elfinder/css/elfinder.min.css",
                    "/" + backend + "/lib/elfinder/css/theme.css",
                    B("/content/filemanager/epc_filemanager.css", "fm5")
                ],
                [
                    "/lib/jquery_browser/jquery.browser.js",
                    "/" + backend + "/lib/elfinder/js/elfinder.min.js",
                    Bp("/content/filemanager/epc_filemanager_config", "fm5"),
                    B("/content/filemanager/epc_filemanager.js", "fm5")
                ]),
            ["shop/catalogue/catalogue_editor"] = Pack([Css("/content/shop/catalogue/epc_catalogue_editor.css", "catEd1")]),
            ["shop/prices/upload"] = Pack(js:
            [
                Bp("/content/shop/prices_upload/epc_prices_upload_history_config"),
                B("/content/shop/prices_upload/epc_prices_upload_history.js")
            ]),
            ["shop/prices/prices_edit"] = Pack([Gp("epc_prices_edit_css")]),
            ["shop/document_control/document_control"] = Pack(
                [Css("/content/shop/finance/epc_erp_ui.css"), Gp("epc_document_control_cp_css")],
                [
                    Bp("/content/shop/document_control/epc_document_control_config"),
                    B("/content/shop/document_control/epc_document_control.js")
                ])
        };
    }

    public static Dictionary<string, object?> EpcCpPageAssetsForUrl(string contentUrl)
    {
        var (css, js) = AssetsRaw(contentUrl);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["css"] = Box(css),
            ["js"] = Box(js)
        };
    }

    public static string EpcCpPageHeadAssets(string contentUrl)
    {
        var (css, _) = AssetsRaw(contentUrl);
        var sb = new System.Text.StringBuilder();
        foreach (var href in css.Keys)
        {
            sb.Append("<link rel=\"stylesheet\" href=\"").Append(H(href)).Append("\" />\n");
        }

        return sb.ToString();
    }

    public static string EpcCpApaiDiscoverTabKey()
    {
        var tab = Get.TryGetValue("tab", out var raw) ? raw : "discover";
        return tab switch
        {
            "discovery" => "discover",
            "taxonomy" => "product_lines",
            "disc_sources" => "uae_sources",
            "market_sources" => "uae_sources",
            "settings" => "rules",
            "dashboard" => "discover",
            "my_imports" => "imports",
            _ => tab
        };
    }

    public static string EpcCpApaiInlineDiscoverConfigScript()
    {
        var backend = Backend();
        var siteKey = SiteKeyFromGet();
        var tab = EpcCpApaiDiscoverTabKey();
        var ajax = ResolveAjax(backend);
        var disc = JsonPhp(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ajaxUrl"] = ajax,
            ["siteKey"] = siteKey,
            ["tab"] = tab,
            ["backend"] = backend,
            ["active"] = tab == "discover"
        });
        var sources = JsonPhp(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ajaxUrl"] = ajax,
            ["siteKey"] = siteKey,
            ["tab"] = tab,
            ["backend"] = backend,
            ["active"] = tab == "uae_sources"
        });
        var lines = JsonPhp(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ajaxUrl"] = ajax,
            ["siteKey"] = siteKey,
            ["tab"] = tab,
            ["backend"] = backend,
            ["active"] = tab == "product_lines"
        });
        var importsFilter = Get.TryGetValue("imports_filter", out var filterRaw) ? filterRaw : "new";
        if (importsFilter is not ("new" or "price_changes" or "duplicates"))
        {
            importsFilter = "new";
        }

        var imports = JsonPhp(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ajaxUrl"] = ajax,
            ["siteKey"] = siteKey,
            ["tab"] = tab,
            ["filter"] = importsFilter,
            ["active"] = tab == "imports",
            ["backend"] = backend,
            ["pageBase"] = "/" + backend + "/control/portal/epc_auto_price_engine"
        });
        return "<script>window.EPC_APAI_DISCOVER=" + disc
            + ";window.EPC_APAI_SOURCES=" + sources
            + ";window.EPC_APAI_PRODUCT_LINES=" + lines
            + ";window.EPC_APAI_IMPORTS=" + imports
            + ";</script>\n";
    }

    public static string EpcCpApaiShellConfigScript()
    {
        var backend = Backend();
        if (backend == "")
        {
            backend = "cp";
        }

        var siteKey = SiteKeyFromGet();
        if (siteKey == "" && PortalHost != null)
        {
            var host = PortalHost().ToLowerInvariant();
            if (host.Contains("epartscart", StringComparison.Ordinal))
            {
                siteKey = "epartscart";
            }
            else if (host.Contains("electronicae", StringComparison.Ordinal))
            {
                siteKey = "electronicae";
            }
        }

        var tab = EpcCpApaiDiscoverTabKey();
        var ajax = ResolveAjax(backend);
        var shell = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["active"] = true,
            ["ajaxUrl"] = ajax,
            ["pageBase"] = "/" + backend + "/control/portal/epc_auto_price_engine",
            ["backend"] = backend,
            ["siteKey"] = siteKey,
            ["tab"] = tab
        };
        var view = Get.TryGetValue("view", out var viewRaw) ? viewRaw : "";
        var taxonomy = Get.ContainsKey("taxonomy_id") ? PhpInt(Get["taxonomy_id"]) : 0;
        if (taxonomy < 0)
        {
            taxonomy = 0;
        }

        if (tab == "discover" && view.Trim() == "" && taxonomy == 0)
        {
            shell["discoverInlined"] = true;
        }

        return "<script>window.EPC_APAI_SHELL=" + JsonPhp(shell) + ";</script>\n";
    }

    public static string EpcCpPageFooterScripts(string contentUrl)
    {
        contentUrl = contentUrl.Trim('/');
        var (_, js) = AssetsRaw(contentUrl);
        var sb = new System.Text.StringBuilder();
        if (contentUrl == "control/portal/epc_auto_price_engine")
        {
            sb.Append(EpcCpApaiShellConfigScript());
            sb.Append(EpcCpApaiInlineDiscoverConfigScript());
        }

        foreach (var src in js.Keys)
        {
            sb.Append("<script src=\"").Append(H(src)).Append("\"></script>\n");
        }

        if (contentUrl == "control/portal/epc_visual_page_editor")
        {
            var backend = Backend();
            var ver = EpcCpPageAssetVersion() + "vpe1";
            var siteKey = SiteKeyFromGet();
            var pageKey = Get.TryGetValue("page_key", out var pageRaw)
                ? Regex.Replace(pageRaw.ToLowerInvariant(), "[^a-z0-9_-]", "")
                : "";
            var qs = "v=" + Raw(ver);
            if (siteKey != "")
            {
                qs += "&site_key=" + Raw(siteKey);
            }

            if (pageKey != "")
            {
                qs += "&page_key=" + Raw(pageKey);
            }

            var configSrc = "/" + backend + "/content/control/portal/epc_visual_page_editor_config" + Php + "?" + qs;
            var jsSrc = "/" + backend + "/content/control/portal/epc_visual_page_editor.js?v=" + Raw(ver);
            if (PhpEmptyJs(js, configSrc))
            {
                sb.Append("<script src=\"").Append(H(configSrc)).Append("\"></script>\n");
            }

            if (PhpEmptyJs(js, jsSrc))
            {
                sb.Append("<script src=\"").Append(H(jsSrc)).Append("\"></script>\n");
            }
        }

        return sb.ToString();
    }

    private static (Dictionary<string, bool> Css, Dictionary<string, bool> Js) AssetsRaw(string contentUrl)
    {
        contentUrl = contentUrl.Trim('/');
        var map = EpcCpPageAssetUrlMap();
        var css = new Dictionary<string, bool>(StringComparer.Ordinal);
        var js = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (ErpUrl.IsMatch(contentUrl))
        {
            js[EpcErpShellNavJsSrc()] = true;
        }

        if (map.TryGetValue(contentUrl, out var row))
        {
            if (row.TryGetValue("css", out var cssList))
            {
                foreach (var href in cssList)
                {
                    css[href] = true;
                }
            }

            if (row.TryGetValue("js", out var jsList))
            {
                foreach (var src in jsList)
                {
                    js[src] = true;
                }
            }
        }

        if (ExtraAssets != null)
        {
            Merge(css, "css");
            Merge(js, "js");
        }

        return (css, js);
    }

    private static void Merge(Dictionary<string, bool> dest, string kind)
    {
        if (ExtraAssets == null || !ExtraAssets.TryGetValue(kind, out var row))
        {
            return;
        }

        foreach (var href in row.Keys)
        {
            dest[href] = true;
        }
    }

    private static object Box(Dictionary<string, bool> d)
        => d.Count == 0 ? new List<object?>() : d;

    private static Dictionary<string, List<string>> Pack(string[]? css = null, string[]? js = null)
    {
        var row = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (css != null)
        {
            row["css"] = [.. css];
        }

        if (js != null)
        {
            row["js"] = [.. js];
        }

        return row;
    }

    private static string Backend()
        => BackendDir is null ? "cp" : BackendDir.Trim('/');

    private static string ResolveAjax(string backend)
        => AjaxUrl != null ? AjaxUrl(backend) : "/" + backend + "/control/portal/ajax_auto_price";

    private static string SiteKeyFromGet()
        => Get.TryGetValue("site_key", out var raw)
            ? Regex.Replace(raw.ToLowerInvariant(), "[^a-z0-9_]", "")
            : "";

    private static int PhpInt(string? raw)
    {
        if (raw is null)
        {
            return 0;
        }

        var s = raw.TrimStart();
        if (s.Length == 0)
        {
            return 0;
        }

        var sign = 1;
        var i = 0;
        if (s[0] == '+')
        {
            i++;
        }
        else if (s[0] == '-')
        {
            sign = -1;
            i++;
        }

        var n = 0;
        var any = false;
        while (i < s.Length && char.IsDigit(s[i]))
        {
            any = true;
            n = n * 10 + (s[i] - '0');
            i++;
        }

        return any ? sign * n : 0;
    }

    private static bool PhpEmptyJs(Dictionary<string, bool> js, string src)
        => !js.TryGetValue(src, out var value) || PhpEmpty(value);

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            0d => true,
            "" => true,
            "0" => true,
            _ => false
        };

    private static string Raw(string value)
        => Uri.EscapeDataString(value);

    private static string JsonPhp(object value)
        => JsonSerializer.Serialize(value, JsonOpts);

    private static string H(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
