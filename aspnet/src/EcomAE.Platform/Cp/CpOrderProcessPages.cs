namespace EcomAE.Platform.Cp;

/// <summary>
/// Control Panel order workspace page bodies from <c>cp/content/shop/order_process</c>.
/// The list, card, items, detail pane and guides render on <c>/cp/orders</c>.
/// OMS ajax stay unmapped on purpose (ERP lock).
/// </summary>
public static class CpOrderProcessPages
{
    public const string OrdersPath = "cp/content/shop/order_process/orders.php";
    public const string OrdersConfigPath = "cp/content/shop/order_process/orders_config.php";
    public const string OrderCardPath = "cp/content/shop/order_process/order_card.php";
    public const string OrderCardConfigPath = "cp/content/shop/order_process/order_card_config.php";
    public const string OrdersItemsPath = "cp/content/shop/order_process/orders_items.php";
    public const string OrdersItemsConfigPath = "cp/content/shop/order_process/orders_items_config.php";
    public const string OrdersItemsAddPath = "cp/content/shop/order_process/orders_items_add.php";
    public const string OrdersItemsEditPath = "cp/content/shop/order_process/orders_items_edit.php";
    public const string OrdersItemsEditConfigPath = "cp/content/shop/order_process/orders_items_edit_config.php";
    public const string OrdersItemsReloadModalPath = "cp/content/shop/order_process/orders_items_reload_modal.php";
    public const string OrdersBackgroundPath = "cp/content/shop/order_process/orders_background.php";
    public const string OrdersDetailPanePath = "cp/content/shop/order_process/epc_orders_detail_pane.php";
    public const string OrdersWorkspaceHelpersPath = "cp/content/shop/order_process/epc_orders_workspace_helpers.php";
    public const string OrderStaffSummaryPath = "cp/content/shop/order_process/epc_order_staff_summary.php";
    public const string WhatsappSharePath = "cp/content/shop/order_process/epc_order_whatsapp_share.php";
    public const string WhatsappGuidePath = "cp/content/shop/order_process/whatsapp_guide.php";
    public const string WhatsappGuidePagePath = "cp/content/shop/order_process/whatsapp_guide_page.php";
    public const string FulfilmentGuidePath = "cp/content/shop/order_process/order_fulfilment_guide.php";
    public const string FulfilmentGuidePagePath = "cp/content/shop/order_process/order_fulfilment_guide_page.php";
    public const string OmsDailyGuidePath = "cp/content/shop/order_process/oms_daily_guide.php";
    public const string OmsDailyGuidePagePath = "cp/content/shop/order_process/oms_daily_guide_page.php";
    public const string StatusesPath = "cp/content/shop/order_process/statuses.php";
    public const string CartsPath = "cp/content/shop/order_process/carts.php";
    public const string WorkspaceHref = "/cp/orders";
    public const string OrderCardHref = "/cp/shop/orders/order";
    public const string FulfilmentHref = "/cp/fulfillment-queue-app";
    public const string WhatsappGuideHref = "/cp/orders?guide=whatsapp";
    public const string OmsGuideHref = "/cp/orders?guide=oms";
    public const string FulfilmentGuideHref = "/cp/orders?guide=fulfilment";

    public const string str_replace_once = nameof(str_replace_once);
    public const string pagination = nameof(pagination);
    public const string get_email_user = nameof(get_email_user);
    public const string btn_modal_clicked = nameof(btn_modal_clicked);
    public const string retun_reload = nameof(retun_reload);
    public const string retun_next = nameof(retun_next);
    public const string epc_wa_guide_h = nameof(epc_wa_guide_h);
    public const string epc_orders_ws_h = nameof(epc_orders_ws_h);
    public const string epc_orders_ws_storage_label = nameof(epc_orders_ws_storage_label);
    public const string epc_orders_ws_usd_rate = nameof(epc_orders_ws_usd_rate);
    public const string epc_orders_ws_aed_usd = nameof(epc_orders_ws_aed_usd);
    public const string epc_orders_ws_badge_class = nameof(epc_orders_ws_badge_class);
    public const string epc_orders_ws_status_badge = nameof(epc_orders_ws_status_badge);
    public const string epc_orders_ws_paid_badge = nameof(epc_orders_ws_paid_badge);
    public const string epc_orders_ws_kpi = nameof(epc_orders_ws_kpi);
    public const string epc_orders_ws_in_process_status_ids = nameof(epc_orders_ws_in_process_status_ids);
    public const string epc_orders_ws_open_status_ids = nameof(epc_orders_ws_open_status_ids);
    public const string epc_orders_ws_completed_status_ids = nameof(epc_orders_ws_completed_status_ids);
    public const string epc_orders_ws_tab_from_cookie = nameof(epc_orders_ws_tab_from_cookie);
    public const string epc_orders_ws_filter_has_search = nameof(epc_orders_ws_filter_has_search);
    public const string epc_orders_ws_normalize_filter_for_tab = nameof(epc_orders_ws_normalize_filter_for_tab);
    public const string epc_orders_ws_count_by_statuses = nameof(epc_orders_ws_count_by_statuses);
    public const string addCommentToLog = nameof(addCommentToLog);
    public const string add_payment_to_order = nameof(add_payment_to_order);
    public const string doc_print = nameof(doc_print);
    public const string getOrderMessages = nameof(getOrderMessages);
    public const string refund = nameof(refund);
    public const string sendMessage = nameof(sendMessage);
    public const string setComment = nameof(setComment);
    public const string setOrderItemsStatus = nameof(setOrderItemsStatus);
    public const string setOrderStatus = nameof(setOrderStatus);
    public const string show_hide_storage_info = nameof(show_hide_storage_info);

    public static string GuideTitle(string? guide)
        => guide switch
        {
            "whatsapp" => "WhatsApp share",
            "oms" => "OMS daily",
            "fulfilment" => "Fulfilment",
            _ => string.Empty
        };

    public static string GuideBody(string? guide)
        => guide switch
        {
            "whatsapp" => "Share the order card on WhatsApp from the detail pane. Staff copy stays on the order; this is not a second messenger engine.",
            "oms" => "One-window OMS: filter, open the card, edit lines, fulfilment, payment and documents. Journal posting stays on ERP.",
            "fulfilment" => "Pick, pack and hand off from the fulfilment queue. Inventory journals stay on the ERP lock.",
            _ => string.Empty
        };
}
