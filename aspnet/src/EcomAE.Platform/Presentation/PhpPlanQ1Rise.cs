using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-rise CP sidebar menu helpers. PHP identifiers kept for the inventory:
/// <c>epc_cp_mm_lang</c>, <c>epc_cp_mm_find_shop_group</c>,
/// <c>epc_cp_mm_group_id</c>, <c>epc_cp_mm_ensure_group</c>,
/// <c>epc_cp_mm_item_id_for_url</c>, <c>epc_cp_mm_ensure_item</c>,
/// <c>epc_cp_mainstream_menu_apply</c>, <c>epc_cp_payments_menu_apply</c>,
/// <c>epc_cp_erp_menu_cleanup</c>, <c>epc_cp_payments_menu_cleanup</c>,
/// <c>epc_cp_marketing_menu_apply</c>, <c>epc_cp_procurement_menu_apply</c>,
/// <c>epc_cp_pos_menu_apply</c>, <c>epc_cp_customer_mgmt_menu_apply</c>,
/// <c>epc_cp_menu_parity_registry</c>, <c>epc_cp_menu_parity_apply</c>,
/// <c>epc_cp_document_control_menu_apply</c>, <c>epc_cp_super_platform_menu_apply</c>,
/// <c>epc_cp_super_cp_operator_menu_apply</c>, <c>epc_cp_integrations_menu_apply</c>,
/// <c>epc_cp_portal_menu_apply</c>, <c>epc_cp_shop_catalogue_prices_menu_apply</c>,
/// <c>epc_cp_shop_orders_menu_apply</c>, <c>epc_cp_oms_menu_cleanup</c>,
/// <c>epc_cp_system_menu_hidden_url_patterns</c>, <c>epc_cp_system_menu_hidden_labels</c>,
/// <c>epc_cp_system_menu_item_hidden</c>, <c>epc_cp_system_menu_item_label</c>,
/// <c>epc_cp_system_menu_cleanup</c>.
/// </summary>
public static class PhpPlanQ1Rise
{
    public const string MainstreamMenuPath = "epc_cp_mainstream_menu.php";

    private static readonly Regex QueryTail = new(@"\?.*$", RegexOptions.CultureInvariant);
    private static readonly Regex AlnumKey = new(@"^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant);
    private static readonly Regex StatusesUrl = new(@"/shop/orders/statuses/?$", RegexOptions.CultureInvariant);
    private static readonly Regex ItemsUrl = new(@"/shop/orders/items/?$", RegexOptions.CultureInvariant);

    public static Func<object?, object?>? CacheBust { get; set; }
    public static Func<object?, object?>? CacheBustPrefix { get; set; }

    public static void Reset()
    {
        CacheBust = null;
        CacheBustPrefix = null;
    }

    public sealed class GroupRow
    {
        public int Id { get; set; }
        public string Caption { get; set; } = "";
        public int Order { get; set; }
    }

    public sealed class ItemRow
    {
        public int Id { get; set; }
        public int ItemsGroup { get; set; }
        public string Caption { get; set; } = "";
        public string Url { get; set; } = "";
        public string Img { get; set; } = "";
        public int Order { get; set; }
        public string BackgroundColor { get; set; } = "";
        public string FontawesomeClass { get; set; } = "";
        public string Target { get; set; } = "";
        public int ShowAnyway { get; set; }
    }

    public sealed class LangRow
    {
        public int Id { get; set; }
        public string StrKey { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class LangTrRow
    {
        public string StrKey { get; set; } = "";
        public string LangCode { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public sealed class RiseStore
    {
        public int NextGroupId { get; set; } = 1;
        public int NextItemId { get; set; } = 1;
        public int NextLangId { get; set; } = 1;
        public List<GroupRow> Groups { get; } = [];
        public List<ItemRow> Items { get; } = [];
        public List<LangRow> Langs { get; } = [];
        public List<LangTrRow> LangTr { get; } = [];

        public object Snapshot()
        {
            var g = Groups.OrderBy(x => x.Id).Select(x => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = x.Id,
                ["caption"] = x.Caption,
                ["order"] = x.Order
            }).ToList();
            var i = Items.OrderBy(x => x.Id).Select(x => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = x.Id,
                ["items_group"] = x.ItemsGroup,
                ["caption"] = x.Caption,
                ["url"] = x.Url,
                ["order"] = x.Order,
                ["background_color"] = x.BackgroundColor,
                ["fontawesome_class"] = x.FontawesomeClass,
                ["show_anyway"] = x.ShowAnyway
            }).ToList();
            return new object[] { g, i };
        }
    }

    public static bool Like(string? value, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("%", ".*").Replace("_", ".") + "$";
        return Regex.IsMatch(value ?? "", regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }

    private static bool PhpEmpty(object? value)
    {
        return value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            "" => true,
            "0" => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };
    }

    public static void EpcCpMmLang(RiseStore db, string key, string en, string ru)
    {
        if (!db.Langs.Any(l => l.StrKey == key))
        {
            db.Langs.Add(new LangRow { Id = db.NextLangId++, StrKey = key, Description = en });
        }

        UpsertTr(db, key, "en", en);
        UpsertTr(db, key, "ru", ru);
    }

    private static void UpsertTr(RiseStore db, string key, string lang, string value)
    {
        var row = db.LangTr.FirstOrDefault(t => t.StrKey == key && t.LangCode == lang);
        if (row is null)
        {
            db.LangTr.Add(new LangTrRow { StrKey = key, LangCode = lang, Value = value });
        }
        else
        {
            row.Value = value;
        }
    }

    public static Dictionary<string, object?> EpcCpMmFindShopGroup(RiseStore db)
    {
        var byCaption = db.Groups.FirstOrDefault(g => g.Caption == "744");
        if (byCaption is not null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = byCaption.Id, ["order"] = byCaption.Order };
        }

        var ranked = db.Groups
            .Select(g => new { g.Id, g.Order, Cnt = db.Items.Count(i => i.ItemsGroup == g.Id && Like(i.Url, "%<backend>/shop/%")) })
            .Where(x => x.Cnt > 0)
            .OrderByDescending(x => x.Cnt)
            .ThenBy(x => x.Id)
            .FirstOrDefault();
        if (ranked is not null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = ranked.Id, ["order"] = ranked.Order };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 6, ["order"] = 5 };
    }

    public static int EpcCpMmGroupId(RiseStore db, string captionKey)
    {
        var row = db.Groups.FirstOrDefault(g => g.Caption == captionKey);
        return row?.Id ?? 0;
    }

    public static int EpcCpMmEnsureGroup(RiseStore db, string captionKey, string en, string ru, int order)
    {
        EpcCpMmLang(db, captionKey, en, ru);
        var id = EpcCpMmGroupId(db, captionKey);
        if (id > 0)
        {
            var row = db.Groups.First(g => g.Id == id);
            row.Order = order;
            return id;
        }

        var created = new GroupRow { Id = db.NextGroupId++, Caption = captionKey, Order = order };
        db.Groups.Add(created);
        return created.Id;
    }

    public static int EpcCpMmItemIdForUrl(RiseStore db, string url)
    {
        var exact = db.Items.FirstOrDefault(i => i.Url == url);
        if (exact is not null)
        {
            return exact.Id;
        }

        var baseUrl = QueryTail.Replace(url ?? "", "");
        if (baseUrl == "" || baseUrl == url)
        {
            return 0;
        }

        var row = db.Items.FirstOrDefault(i => i.Url == baseUrl || Like(i.Url, baseUrl + "?%"));
        return row?.Id ?? 0;
    }

    public static int EpcCpMmEnsureItem(RiseStore db, int groupId, string captionKey, string url, int order, string color, string icon, int showAnyway = 0)
    {
        var id = EpcCpMmItemIdForUrl(db, url);
        if (id > 0)
        {
            var row = db.Items.First(i => i.Id == id);
            row.ItemsGroup = groupId;
            row.Caption = captionKey;
            row.Order = order;
            row.BackgroundColor = color;
            row.FontawesomeClass = icon;
            row.ShowAnyway = showAnyway;
            return id;
        }

        var created = new ItemRow
        {
            Id = db.NextItemId++,
            ItemsGroup = groupId,
            Caption = captionKey,
            Url = url,
            Img = "",
            Order = order,
            BackgroundColor = color,
            FontawesomeClass = icon,
            Target = "",
            ShowAnyway = showAnyway
        };
        db.Items.Add(created);
        return created.Id;
    }

    private static void ShiftGroupOrders(RiseStore db, int fromOrder, int delta, int? exceptId = null)
    {
        foreach (var g in db.Groups)
        {
            if (g.Order >= fromOrder && (exceptId is null || g.Id != exceptId.Value))
            {
                g.Order += delta;
            }
        }
    }

    public static Dictionary<string, object?> EpcCpMainstreamMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_cp_group_channels", "Channels", "Каналы");
        EpcCpMmLang(db, "epc_cp_group_logistics", "Logistics", "Логистика");
        EpcCpMmLang(db, "epc_cp_group_erp", "ERP Suite", "ERP — бизнес");
        EpcCpMmLang(db, "epc_erp_suite_cp", "ERP &amp; Business", "ERP и бизнес");
        EpcCpMmLang(db, "epc_cp_group_ai", "AI Agent", "AI агент");
        EpcCpMmLang(db, "epc_logistics_cp", "Logistics hub", "Логистика — обзор");
        EpcCpMmLang(db, "epc_logistics_carriers_cp", "Carriers & shipments", "Перевозчики и отправки");
        EpcCpMmLang(db, "epc_logistics_guide_cp", "Logistics guide", "Гид по логистике");
        EpcCpMmLang(db, "epc_logistics_obtain_cp", "Delivery methods", "Способы доставки");
        EpcCpMmLang(db, "epc_logistics_orders_cp", "Customer orders", "Заказы клиентов");
        EpcCpMmLang(db, "epc_whatsapp_guide", "WhatsApp guide", "Гид WhatsApp");
        EpcCpMmLang(db, "epc_whatsapp_guide_cp", "WhatsApp guide", "Гид WhatsApp");
        EpcCpMmLang(db, "epc_custom_shipping_cp", "Custom & Shipping", "Таможня и доставка");
        EpcCpMmLang(db, "epc_custom_shipping_guide_cp", "Custom & Shipping guide", "Гид: таможня и доставка");

        var shop = EpcCpMmFindShopGroup(db);
        var shopId = Convert.ToInt32(shop["id"], CultureInfo.InvariantCulture);
        var slot = Convert.ToInt32(shop["order"], CultureInfo.InvariantCulture) + 1;

        var keys = new[] { "epc_cp_group_channels", "epc_cp_group_logistics", "epc_cp_group_erp", "epc_cp_group_ai" };
        var missing = keys.Count(k => EpcCpMmGroupId(db, k) <= 0);
        if (missing >= 3)
        {
            ShiftGroupOrders(db, slot, 4, shopId);
        }

        var channelsGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_channels", "Channels", "Каналы", slot);
        var logisticsGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_logistics", "Logistics", "Логистика", slot + 1);
        var erpGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_erp", "ERP Suite", "ERP — бизнес", slot + 2);
        var aiGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_ai", "AI Agent", "AI агент", slot + 3);

        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["channels_hub"] = EpcCpMmEnsureItem(db, channelsGroup, "epc_channels_cp", "/<backend>/shop/channels/channels", 10, "#2563eb", "fas fa-plug", 1),
            ["channels_guide"] = EpcCpMmEnsureItem(db, channelsGroup, "epc_channels_guide_cp", "/<backend>/shop/channels/guide", 20, "#2563eb", "fas fa-book", 0),
            ["logistics_hub"] = EpcCpMmEnsureItem(db, logisticsGroup, "epc_logistics_cp", "/<backend>/shop/logistics", 10, "#0f766e", "fas fa-th-large", 1),
            ["logistics_carriers"] = EpcCpMmEnsureItem(db, logisticsGroup, "epc_logistics_carriers_cp", "/<backend>/shop/logistics/carriers", 20, "#0f766e", "fas fa-shipping-fast", 0),
            ["logistics_guide"] = EpcCpMmEnsureItem(db, logisticsGroup, "epc_logistics_guide_cp", "/<backend>/shop/logistics/guide", 30, "#0f766e", "fas fa-book", 0),
            ["logistics_obtain"] = EpcCpMmEnsureItem(db, logisticsGroup, "epc_logistics_obtain_cp", "/<backend>/shop/logistics/sposoby-polucheniya", 40, "#64748b", "fas fa-truck", 0),
            ["logistics_orders"] = EpcCpMmEnsureItem(db, logisticsGroup, "epc_logistics_orders_cp", "/<backend>/shop/orders/orders", 50, "#64748b", "fas fa-shopping-cart", 0),
            ["whatsapp_guide"] = EpcCpMmEnsureItem(db, logisticsGroup, "epc_whatsapp_guide", "/<backend>/shop/orders/whatsapp-guide", 55, "#25D366", "fab fa-whatsapp", 1)
        };

        var oldObtain = db.Items.FirstOrDefault(i => i.Caption == "epc_cp_channels_obtain_modes");
        if (oldObtain is not null && oldObtain.ItemsGroup == channelsGroup)
        {
            db.Items.Remove(oldObtain);
        }

        items["erp_hub"] = EpcCpMmEnsureItem(db, erpGroup, "epc_erp_suite_cp", "/<backend>/shop/finance/erp?epc_erp_shell=1", 10, "#1e3a5f", "fas fa-briefcase", 1);
        items["erp_guide"] = EpcCpMmEnsureItem(db, erpGroup, "epc_erp_guide_cp", "/<backend>/shop/finance/erp/guide?epc_erp_shell=1", 20, "#27ae60", "fas fa-book", 0);
        items["custom_shipping"] = EpcCpMmEnsureItem(db, erpGroup, "epc_custom_shipping_cp", "/<backend>/shop/finance/erp?area=custom_shipping&tab=custom_shipping&epc_erp_shell=1", 15, "#0f766e", "fas fa-ship", 0);
        items["custom_shipping_guide"] = EpcCpMmEnsureItem(db, erpGroup, "epc_custom_shipping_guide_cp", "/<backend>/shop/finance/erp/custom-shipping-guide?epc_erp_shell=1", 25, "#0f766e", "fas fa-book", 0);
        items["uae_tax_compliance"] = EpcCpMmEnsureItem(db, erpGroup, "epc_uae_tax_compliance_cp", "/<backend>/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1", 26, "#7c3aed", "fas fa-gavel", 0);
        EpcCpErpMenuCleanup(db, erpGroup, items);

        items["ai_hub"] = EpcCpMmEnsureItem(db, aiGroup, "epc_parts_agent_chats_cp", "/<backend>/shop/parts_agent_chats", 10, "#8e44ad", "fas fa-robot", 1);

        var shopOrdersMenu = EpcCpShopOrdersMenuApply(db);
        if (!PhpEmpty(shopOrdersMenu["shop_orders_item"]))
        {
            items["shop_orders"] = Convert.ToInt32(shopOrdersMenu["shop_orders_item"], CultureInfo.InvariantCulture);
        }

        var shopCataloguePrices = EpcCpShopCataloguePricesMenuApply(db);
        if (shopCataloguePrices["items"] is Dictionary<string, object?> catItems)
        {
            foreach (var kv in catItems)
            {
                items["shop_" + kv.Key] = Convert.ToInt32(kv.Value ?? 0, CultureInfo.InvariantCulture);
            }
        }

        var customersMenu = EpcCpCustomerMgmtMenuApply(db);
        if (customersMenu["items"] is Dictionary<string, object?> custItems)
        {
            foreach (var kv in custItems)
            {
                items["customers_" + kv.Key] = Convert.ToInt32(kv.Value ?? 0, CultureInfo.InvariantCulture);
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["shop_group"] = shopId,
            ["channels_group"] = channelsGroup,
            ["logistics_group"] = logisticsGroup,
            ["erp_group"] = erpGroup,
            ["ai_group"] = aiGroup,
            ["customers_group"] = Convert.ToInt32(customersMenu["customers_group"] ?? 0, CultureInfo.InvariantCulture),
            ["menu_orders"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["channels"] = slot,
                ["logistics"] = slot + 1,
                ["erp"] = slot + 2,
                ["ai"] = slot + 3
            },
            ["items"] = items
        };
    }

    public static Dictionary<string, object?> EpcCpPaymentsMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_cp_group_payments", "Payment gateways", "Платёжные системы");
        var shop = EpcCpMmFindShopGroup(db);
        var channelsOrder = EpcCpMmGroupId(db, "epc_cp_group_channels");
        var slot = channelsOrder > 0
            ? db.Groups.First(g => g.Id == channelsOrder).Order
            : Convert.ToInt32(shop["order"], CultureInfo.InvariantCulture) + 1;

        if (EpcCpMmGroupId(db, "epc_cp_group_payments") <= 0)
        {
            ShiftGroupOrders(db, slot, 1);
        }

        var paymentsGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_payments", "Payment gateways", "Платёжные системы", slot);
        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["payments_hub"] = EpcCpMmEnsureItem(db, paymentsGroup, "epc_payments_cp", "/<backend>/shop/payments/payments", 10, "#7c3aed", "fas fa-credit-card", 1),
            ["payments_guide"] = EpcCpMmEnsureItem(db, paymentsGroup, "epc_payments_guide_cp", "/<backend>/shop/payments/payments/guide", 20, "#7c3aed", "fas fa-book", 0)
        };
        EpcCpPaymentsMenuCleanup(db, paymentsGroup, items);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["payments_group"] = paymentsGroup,
            ["items"] = items
        };
    }

    public static int EpcCpErpMenuCleanup(RiseStore db, int erpGroup, Dictionary<string, object?> items)
    {
        var keep = new HashSet<int>();
        foreach (var itemIdObj in items.Values)
        {
            var itemId = Convert.ToInt32(itemIdObj ?? 0, CultureInfo.InvariantCulture);
            if (itemId > 0)
            {
                keep.Add(itemId);
            }
        }

        if (keep.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        foreach (var row in db.Items.Where(i => i.ItemsGroup == erpGroup).ToList())
        {
            if (!keep.Contains(row.Id))
            {
                db.Items.Remove(row);
                removed++;
            }
        }

        const string erpBase = "/<backend>/shop/finance/erp";
        const string guideBase = "/<backend>/shop/finance/erp/guide";
        foreach (var row in db.Items.Where(i => Like(i.Url, "%/shop/finance/erp%")).OrderBy(i => i.Id).ToList())
        {
            if (keep.Contains(row.Id))
            {
                continue;
            }

            var url = QueryTail.Replace(row.Url ?? "", "");
            if (url == erpBase || url == guideBase)
            {
                db.Items.Remove(row);
                removed++;
            }
        }

        return removed;
    }

    public static int EpcCpPaymentsMenuCleanup(RiseStore db, int paymentsGroup, Dictionary<string, object?> items)
    {
        var keep = new HashSet<int>();
        foreach (var itemIdObj in items.Values)
        {
            var itemId = Convert.ToInt32(itemIdObj ?? 0, CultureInfo.InvariantCulture);
            if (itemId > 0)
            {
                keep.Add(itemId);
            }
        }

        if (keep.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        foreach (var row in db.Items.Where(i => i.ItemsGroup == paymentsGroup).ToList())
        {
            if (!keep.Contains(row.Id))
            {
                db.Items.Remove(row);
                removed++;
            }
        }

        return removed;
    }

    public static Dictionary<string, object?> EpcCpMarketingMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_cp_group_marketing", "Marketing", "Маркетинг");
        EpcCpMmLang(db, "epc_marketing_cp", "Marketing & growth", "Маркетинг и рост");
        var aiOrder = EpcCpMmGroupId(db, "epc_cp_group_ai");
        var slot = aiOrder > 0
            ? db.Groups.First(g => g.Id == aiOrder).Order + 1
            : Convert.ToInt32(EpcCpMmFindShopGroup(db)["order"], CultureInfo.InvariantCulture) + 5;
        if (EpcCpMmGroupId(db, "epc_cp_group_marketing") <= 0)
        {
            ShiftGroupOrders(db, slot, 1);
        }

        var marketingGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_marketing", "Marketing", "Маркетинг", slot);
        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["marketing_hub"] = EpcCpMmEnsureItem(db, marketingGroup, "epc_marketing_cp", "/<backend>/shop/marketing/marketing", 10, "#db2777", "fas fa-bullhorn", 1)
        };
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["marketing_group"] = marketingGroup,
            ["items"] = items
        };
    }

    public static Dictionary<string, object?> EpcCpProcurementMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_cp_group_procurement", "Procurement", "Закупки");
        EpcCpMmLang(db, "epc_procurement_cp", "Procurement & suppliers", "Закупки и поставщики");
        var erpOrder = EpcCpMmGroupId(db, "epc_cp_group_erp");
        var slot = erpOrder > 0
            ? db.Groups.First(g => g.Id == erpOrder).Order + 1
            : Convert.ToInt32(EpcCpMmFindShopGroup(db)["order"], CultureInfo.InvariantCulture) + 3;
        if (EpcCpMmGroupId(db, "epc_cp_group_procurement") <= 0)
        {
            ShiftGroupOrders(db, slot, 1);
        }

        var procGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_procurement", "Procurement", "Закупки", slot);
        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["procurement_hub"] = EpcCpMmEnsureItem(db, procGroup, "epc_procurement_cp", "/<backend>/shop/procurement/procurement", 10, "#1e4d3a", "fas fa-truck-loading", 1)
        };
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["procurement_group"] = procGroup,
            ["items"] = items
        };
    }

    public static Dictionary<string, object?> EpcCpPosMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_pos_terminal_cp", "POS Terminal", "Касса POS");
        var erpGroup = EpcCpMmGroupId(db, "epc_cp_group_erp");
        if (erpGroup <= 0)
        {
            var shop = EpcCpMmFindShopGroup(db);
            erpGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_erp", "ERP Suite", "ERP — бизнес", Convert.ToInt32(shop["order"], CultureInfo.InvariantCulture) + 2);
        }

        var itemId = EpcCpMmEnsureItem(db, erpGroup, "epc_pos_terminal_cp", "/<backend>/shop/pos/terminal", 12, "#2563eb", "fa-cash-register", 1);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_group"] = erpGroup,
            ["items"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["pos_terminal"] = itemId }
        };
    }

    public static Dictionary<string, object?> EpcCpCustomerMgmtMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_cp_group_customers", "Customers & accounts", "Клиенты и учётные записи");
        EpcCpMmLang(db, "epc_customer_mgmt_cp", "Customer management", "Управление клиентами");
        EpcCpMmLang(db, "epc_user_accounts_cp", "User accounts", "Учётные записи");
        EpcCpMmLang(db, "epc_user_groups_cp", "User groups", "Группы пользователей");
        EpcCpMmLang(db, "epc_user_create_cp", "Create user", "Создать пользователя");
        EpcCpMmLang(db, "epc_customer_approvals_cp", "Customer approvals", "Одобрение клиентов");
        EpcCpMmLang(db, "epc_vendor_approvals_cp", "Vendor approvals", "Одобрение поставщиков");
        EpcCpMmLang(db, "epc_reg_fields_cp", "Registration fields", "Поля регистрации");
        EpcCpMmLang(db, "epc_reg_variants_cp", "Registration variants", "Варианты регистрации");

        foreach (var oldUrl in new[] { "/<backend>/users/customer_mgmt", "/<backend>/shop/customer_mgmt/customer_mgmt" })
        {
            var row = db.Items.FirstOrDefault(i => i.Url == oldUrl);
            if (row is not null && row.ItemsGroup != 0)
            {
                var grp = db.Groups.FirstOrDefault(g => g.Id == row.ItemsGroup);
                var cap = grp?.Caption ?? "";
                if (cap != "epc_cp_group_customers")
                {
                    db.Items.Remove(row);
                }
            }
        }

        var procGroupId = EpcCpMmGroupId(db, "epc_cp_group_procurement");
        var slot = procGroupId > 0
            ? db.Groups.First(g => g.Id == procGroupId).Order + 1
            : Convert.ToInt32(EpcCpMmFindShopGroup(db)["order"], CultureInfo.InvariantCulture) + 2;
        if (EpcCpMmGroupId(db, "epc_cp_group_customers") <= 0)
        {
            ShiftGroupOrders(db, slot, 1);
        }

        var customersGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_customers", "Customers & accounts", "Клиенты и учётные записи", slot);
        var specs = new (string Key, string Caption, string Url, int Order, string Color, string Icon)[]
        {
            ("customer_mgmt", "epc_customer_mgmt_cp", "/<backend>/shop/customer_mgmt/customer_mgmt", 10, "#2563eb", "fas fa-address-book"),
            ("user_accounts", "epc_user_accounts_cp", "/<backend>/users/usermanager", 20, "#1d4ed8", "fas fa-user-alt"),
            ("user_groups", "epc_user_groups_cp", "/<backend>/users/usergroups", 30, "#1d4ed8", "fas fa-users"),
            ("user_create", "epc_user_create_cp", "/<backend>/users/usermanager/user", 40, "#3b82f6", "fas fa-user-plus"),
            ("customer_approvals", "epc_customer_approvals_cp", "/<backend>/users/customer_approvals", 50, "#059669", "fas fa-user-check"),
            ("vendor_approvals", "epc_vendor_approvals_cp", "/<backend>/users/vendor_approvals", 55, "#0f766e", "fas fa-store"),
            ("reg_fields", "epc_reg_fields_cp", "/<backend>/users/polya-registracii", 60, "#64748b", "far fa-address-card"),
            ("reg_variants", "epc_reg_variants_cp", "/<backend>/users/registracionnye-varianty", 70, "#64748b", "fas fa-users-cog")
        };
        var items = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var spec in specs)
        {
            items[spec.Key] = EpcCpMmEnsureItem(db, customersGroup, spec.Caption, spec.Url, spec.Order, spec.Color, spec.Icon, 1);
        }

        var legacyUsersId = EpcCpMmGroupId(db, "741");
        if (legacyUsersId > 0 && legacyUsersId != customersGroup)
        {
            foreach (var row in db.Items.Where(i => i.ItemsGroup == legacyUsersId && Like(i.Url, "%/users/%")))
            {
                row.ItemsGroup = customersGroup;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["customers_group"] = customersGroup,
            ["items"] = items,
            ["customer_mgmt_item"] = Convert.ToInt32(items["customer_mgmt"] ?? 0, CultureInfo.InvariantCulture)
        };
    }

    public static Dictionary<string, object?> EpcCpMenuParityRegistry()
        => new(StringComparer.Ordinal)
        {
            ["mainstream"] = "epc_cp_mainstream_menu_apply",
            ["customers_accounts"] = "epc_cp_customer_mgmt_menu_apply",
            ["documents"] = "epc_cp_document_control_menu_apply",
            ["payments"] = "epc_cp_payments_menu_apply",
            ["marketing"] = "epc_cp_marketing_menu_apply",
            ["procurement"] = "epc_cp_procurement_menu_apply",
            ["pos"] = "epc_cp_pos_menu_apply",
            ["portal"] = "epc_cp_portal_menu_apply",
            ["integrations"] = "epc_cp_integrations_menu_apply"
        };

    public static Dictionary<string, object?> EpcCpMenuParityApply(RiseStore db)
    {
        var outRow = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["packs"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["ok"] = true
        };
        var packs = (Dictionary<string, object?>)outRow["packs"]!;
        foreach (var kv in EpcCpMenuParityRegistry())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pack"] = kv.Key,
                ["fn"] = kv.Value,
                ["ok"] = false
            };
            try
            {
                row["result"] = DispatchPack(db, (string)kv.Value!);
                row["ok"] = true;
            }
            catch (Exception ex)
            {
                row["error"] = ex.Message;
                outRow["ok"] = false;
            }

            packs[kv.Key] = row;
        }

        if (CacheBust is not null)
        {
            outRow["cache_bust"] = CacheBust(db);
        }
        else if (CacheBustPrefix is not null)
        {
            outRow["cache_bust"] = CacheBustPrefix("epc_cp_menu_rows");
        }

        return outRow;
    }

    private static object DispatchPack(RiseStore db, string fn)
        => fn switch
        {
            "epc_cp_mainstream_menu_apply" => EpcCpMainstreamMenuApply(db),
            "epc_cp_customer_mgmt_menu_apply" => EpcCpCustomerMgmtMenuApply(db),
            "epc_cp_document_control_menu_apply" => EpcCpDocumentControlMenuApply(db),
            "epc_cp_payments_menu_apply" => EpcCpPaymentsMenuApply(db),
            "epc_cp_marketing_menu_apply" => EpcCpMarketingMenuApply(db),
            "epc_cp_procurement_menu_apply" => EpcCpProcurementMenuApply(db),
            "epc_cp_pos_menu_apply" => EpcCpPosMenuApply(db),
            "epc_cp_portal_menu_apply" => EpcCpPortalMenuApply(db),
            "epc_cp_integrations_menu_apply" => EpcCpIntegrationsMenuApply(db),
            _ => throw new InvalidOperationException("missing_function")
        };

    public static Dictionary<string, object?> EpcCpDocumentControlMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_cp_group_documents", "Documents", "Документы");
        EpcCpMmLang(db, "epc_document_control_cp", "Document Control", "Управление документами");
        foreach (var oldUrl in new[] { "/<backend>/shop/modul-pechati-dokumentov", "/<backend>/shop/document_control/document_control" })
        {
            var row = db.Items.FirstOrDefault(i => i.Url == oldUrl);
            if (row is not null && row.ItemsGroup != 0)
            {
                var grp = db.Groups.FirstOrDefault(g => g.Id == row.ItemsGroup);
                if ((grp?.Caption ?? "") != "epc_cp_group_documents")
                {
                    db.Items.Remove(row);
                }
            }
        }

        var customersGroupId = EpcCpMmGroupId(db, "epc_cp_group_customers");
        var slot = customersGroupId > 0
            ? db.Groups.First(g => g.Id == customersGroupId).Order + 1
            : Convert.ToInt32(EpcCpMmFindShopGroup(db)["order"], CultureInfo.InvariantCulture) + 3;
        if (EpcCpMmGroupId(db, "epc_cp_group_documents") <= 0)
        {
            ShiftGroupOrders(db, slot, 1);
        }

        var documentsGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_documents", "Documents", "Документы", slot);
        var itemId = EpcCpMmEnsureItem(db, documentsGroup, "epc_document_control_cp", "/<backend>/shop/document_control/document_control", 10, "#0f766e", "fas fa-file-invoice", 1);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["documents_group"] = documentsGroup,
            ["document_control_item"] = itemId
        };
    }

    public static Dictionary<string, object?> EpcCpSuperPlatformMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_cp_group_tenant_hub", "Platform", "Платформа");
        EpcCpMmLang(db, "epc_tenant_hub_cp", "Tenant hub", "Центр клиентов");
        var portalGroup = EpcCpMmGroupId(db, "epc_cp_group_portal");
        var slot = portalGroup > 0 ? db.Groups.First(g => g.Id == portalGroup).Order : 1;
        if (EpcCpMmGroupId(db, "epc_cp_group_tenant_hub") <= 0)
        {
            ShiftGroupOrders(db, slot, 1);
        }

        var hubGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_tenant_hub", "Platform", "Платформа", slot);
        var itemId = EpcCpMmEnsureItem(db, hubGroup, "epc_tenant_hub_cp", "/<backend>/shop/tenant_hub/tenant_hub", 1, "#0369a1", "fas fa-cloud", 1);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_hub_group"] = hubGroup,
            ["tenant_hub_item"] = itemId
        };
    }

    public static Dictionary<string, object?> EpcCpSuperCpOperatorMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_cp_group_operator", "Operator", "Оператор");
        EpcCpMmLang(db, "epc_cp_group_operator_desc", "Cross-tenant platform tools", "Инструменты платформы");
        EpcCpMmLang(db, "epc_super_cp_operator_guide", "Operator guide", "Гид оператора");
        EpcCpMmLang(db, "epc_super_cp_customer_board", "Customer board", "Клиенты платформы");
        EpcCpMmLang(db, "epc_super_cp_price_configs", "Price configs", "Генерация цен");
        EpcCpMmLang(db, "epc_super_cp_info_blocks", "Info blocks", "Инфо-блоки");
        EpcCpMmLang(db, "epc_visual_page_editor", "Visual page editor", "Визуальный редактор");
        EpcCpMmLang(db, "epc_super_cp_communication", "Communication", "Коммуникации");
        var hubGroup = EpcCpMmGroupId(db, "epc_cp_group_tenant_hub");
        var slot = hubGroup > 0 ? db.Groups.First(g => g.Id == hubGroup).Order + 1 : 2;
        if (EpcCpMmGroupId(db, "epc_cp_group_operator") <= 0)
        {
            ShiftGroupOrders(db, slot, 1);
        }

        var operatorGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_operator", "Operator", "Оператор", slot);
        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["operator_guide"] = EpcCpMmEnsureItem(db, operatorGroup, "epc_super_cp_operator_guide", "/<backend>/control/portal/epc_super_cp_operator_guide", 1, "#2563eb", "fas fa-book", 1),
            ["customer_board"] = EpcCpMmEnsureItem(db, operatorGroup, "epc_super_cp_customer_board", "/<backend>/control/portal/epc_super_cp_customer_board", 2, "#2563eb", "fas fa-users", 1),
            ["price_configs"] = EpcCpMmEnsureItem(db, operatorGroup, "epc_super_cp_price_configs", "/<backend>/control/portal/epc_super_cp_price_configs", 3, "#1d4ed8", "fas fa-tags", 1),
            ["info_blocks"] = EpcCpMmEnsureItem(db, operatorGroup, "epc_super_cp_info_blocks", "/<backend>/control/portal/epc_super_cp_info_blocks", 4, "#0ea5e9", "fas fa-th-large", 1),
            ["visual_editor"] = EpcCpMmEnsureItem(db, operatorGroup, "epc_visual_page_editor", "/<backend>/control/portal/epc_visual_page_editor", 5, "#7c3aed", "fas fa-magic", 1),
            ["communication"] = EpcCpMmEnsureItem(db, operatorGroup, "epc_super_cp_communication", "/<backend>/control/portal/epc_super_cp_communication", 6, "#0369a1", "fas fa-envelope", 1)
        };
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal) { ["operator_group"] = operatorGroup };
        foreach (var kv in items)
        {
            merged[kv.Key] = kv.Value;
        }

        return merged;
    }

    public static Dictionary<string, object?> EpcCpIntegrationsMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_cp_group_integrations", "Integrations", "Интеграции");
        EpcCpMmLang(db, "epc_cp_group_integrations_desc", "Email, mobile, payments & more", "Почта, мобильные, платежи");
        EpcCpMmLang(db, "epc_integrations_hub_cp", "Integrations hub", "Хаб интеграций");
        EpcCpMmLang(db, "epc_mobile_apps_cp", "Mobile apps", "Мобильные приложения");
        EpcCpMmLang(db, "epc_tenant_features_cp", "Tenant features", "Функции клиентов");
        EpcCpMmLang(db, "epc_tenant_email_cp", "Email / SMTP", "Email / SMTP");
        var portalGroup = EpcCpMmGroupId(db, "epc_cp_group_portal");
        var slot = portalGroup > 0 ? db.Groups.First(g => g.Id == portalGroup).Order + 1 : 3;
        if (EpcCpMmGroupId(db, "epc_cp_group_integrations") <= 0)
        {
            ShiftGroupOrders(db, slot, 1);
        }

        var intGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_integrations", "Integrations", "Интеграции", slot);
        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["integrations_hub"] = EpcCpMmEnsureItem(db, intGroup, "epc_integrations_hub_cp", "/<backend>/control/portal/epc_integrations_hub", 1, "#059669", "fas fa-plug", 1),
            ["mobile_apps"] = EpcCpMmEnsureItem(db, intGroup, "epc_mobile_apps_cp", "/<backend>/control/portal/epc_mobile_apps", 2, "#dc2626", "fas fa-mobile-alt", 1),
            ["tenant_email"] = EpcCpMmEnsureItem(db, intGroup, "epc_tenant_email_cp", "/<backend>/control/portal/epc_tenant_email_settings", 3, "#33cc33", "far fa-envelope", 0),
            ["tenant_features"] = EpcCpMmEnsureItem(db, intGroup, "epc_tenant_features_cp", "/<backend>/control/portal/epc_tenant_features", 4, "#2563eb", "fas fa-sliders", 1)
        };
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal) { ["integrations_group"] = intGroup };
        foreach (var kv in items)
        {
            merged[kv.Key] = kv.Value;
        }

        return merged;
    }

    public static Dictionary<string, object?> EpcCpPortalMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_visual_page_editor", "Visual page editor", "Визуальный редактор страниц");
        EpcCpMmLang(db, "epc_social_media_hub_cp", "Social media hub", "Соцсети — хаб");
        EpcCpMmLang(db, "epc_marketing_broadcast_cp", "Marketing broadcast", "Рассылка — маркетинг");
        var portalGroup = EpcCpMmEnsureGroup(db, "epc_cp_group_portal", "Portal", "Портал", 2);
        EpcCpPosMenuApply(db);
        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["visual_editor"] = EpcCpMmEnsureItem(db, portalGroup, "epc_visual_page_editor", "/<backend>/control/portal/epc_visual_page_editor", 16, "#7c3aed", "fas fa-magic", 1),
            ["social_media_hub"] = EpcCpMmEnsureItem(db, portalGroup, "epc_social_media_hub_cp", "/<backend>/control/portal/epc_social_media_hub", 17, "#e1306c", "fas fa-share-alt", 1),
            ["marketing_broadcast"] = EpcCpMmEnsureItem(db, portalGroup, "epc_marketing_broadcast_cp", "/<backend>/control/portal/epc_marketing_broadcast", 18, "#db2777", "fas fa-bullhorn", 1)
        };
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["portal_group"] = portalGroup,
            ["items"] = items
        };
    }

    public static Dictionary<string, object?> EpcCpShopCataloguePricesMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "epc_sku_media_manager", "SKU photos & specs", "Фото и характеристики SKU");
        EpcCpMmLang(db, "epc_prices_multivendor_cp", "Multi-vendor upload", "Мульти-вендор загрузка");
        EpcCpMmLang(db, "epc_prices_guide_cp", "Price upload guide", "Гид по загрузке цен");
        var shop = EpcCpMmFindShopGroup(db);
        var shopGroupId = Convert.ToInt32(shop["id"] ?? 0, CultureInfo.InvariantCulture);
        var outRow = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["shop_group"] = shopGroupId,
            ["items"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["removed_commerce"] = 0
        };
        if (shopGroupId <= 0)
        {
            return outRow;
        }

        var commerce = db.Items.Where(i => Like(i.Url, "%/shop/prices/commerce%") || i.Caption == "epc_prices_commerce_cp").ToList();
        foreach (var row in commerce)
        {
            db.Items.Remove(row);
        }

        outRow["removed_commerce"] = commerce.Count;

        var orderBase = 14;
        var priceOrder = db.Items
            .Where(i => i.ItemsGroup == shopGroupId && (Like(i.Url, "%/shop/prices") || i.Caption == "771"))
            .OrderBy(i => i.Order)
            .Select(i => i.Order)
            .FirstOrDefault();
        if (priceOrder > 0)
        {
            orderBase = priceOrder + 1;
        }

        var defs = new Dictionary<string, (string Caption, string Url, int Order, string Color, string Icon)>(StringComparer.Ordinal)
        {
            ["sku_media"] = ("epc_sku_media_manager", "/<backend>/shop/catalogue/sku_media", orderBase, "#0f766e", "fa-picture-o"),
            ["multivendor"] = ("epc_prices_multivendor_cp", "/<backend>/shop/prices/multivendor", orderBase + 1, "#0891b2", "fa-handshake-o"),
            ["prices_guide"] = ("epc_prices_guide_cp", "/<backend>/shop/prices/guide", orderBase + 2, "#26ad5f", "fas fa-book")
        };
        var items = (Dictionary<string, object?>)outRow["items"]!;
        foreach (var kv in defs)
        {
            items[kv.Key] = EpcCpMmEnsureItem(db, shopGroupId, kv.Value.Caption, kv.Value.Url, kv.Value.Order, kv.Value.Color, kv.Value.Icon, 1);
        }

        var orphanGroup = EpcCpMmGroupId(db, "epc_cp_group_commerce");
        if (orphanGroup > 0)
        {
            var keepIds = items.Values.Select(v => Convert.ToInt32(v ?? 0, CultureInfo.InvariantCulture)).Where(id => id != 0).ToList();
            foreach (var row in db.Items.Where(i => i.ItemsGroup == orphanGroup).ToList())
            {
                if (keepIds.Contains(row.Id))
                {
                    continue;
                }

                var url = row.Url ?? "";
                if (url.Contains("sku_media", StringComparison.Ordinal)
                    || url.Contains("prices/multivendor", StringComparison.Ordinal)
                    || url.Contains("prices/commerce", StringComparison.Ordinal)
                    || url.Contains("prices/guide", StringComparison.Ordinal))
                {
                    db.Items.Remove(row);
                }
            }

            if (db.Items.Count(i => i.ItemsGroup == orphanGroup) == 0)
            {
                db.Groups.RemoveAll(g => g.Id == orphanGroup);
            }
        }

        return outRow;
    }

    public static Dictionary<string, object?> EpcCpShopOrdersMenuApply(RiseStore db)
    {
        EpcCpMmLang(db, "282", "OMS · Orders", "OMS · Заказы");
        EpcCpMmLang(db, "284", "OMS · Orders", "OMS · Заказы");
        EpcCpMmLang(db, "epc_shop_orders_cp", "OMS · Orders", "OMS · Заказы");
        EpcCpMmLang(db, "epc_oms_orders_cp", "OMS · Orders", "OMS · Заказы");
        EpcCpMmLang(db, "epc_oms_guide_cp", "OMS daily guide", "OMS — ежедневный гид");
        EpcCpMmLang(db, "epc_logistics_orders_cp", "OMS · Orders", "OMS · Заказы");
        var shop = EpcCpMmFindShopGroup(db);
        var shopGroupId = Convert.ToInt32(shop["id"] ?? 0, CultureInfo.InvariantCulture);
        if (shopGroupId <= 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["shop_group"] = 0,
                ["shop_orders_item"] = 0,
                ["order"] = 0,
                ["removed"] = 0
            };
        }

        const string ordersUrl = "/<backend>/shop/orders/orders";
        var existing = db.Items.FirstOrDefault(i => i.ItemsGroup == shopGroupId && i.Url == ordersUrl);
        var itemId = existing?.Id ?? 0;
        var orderSlot = 25;
        var statusOrder = db.Items
            .Where(i => i.ItemsGroup == shopGroupId && (Like(i.Url, "%/shop/orders/statuses%") || i.Caption is "279" or "281"))
            .OrderBy(i => i.Order)
            .Select(i => i.Order)
            .FirstOrDefault();
        if (statusOrder > 0)
        {
            orderSlot = Math.Max(1, statusOrder - 1);
        }
        else
        {
            var whOrder = db.Items
                .Where(i => i.ItemsGroup == shopGroupId && Like(i.Url, "%/storages%"))
                .OrderByDescending(i => i.Order)
                .Select(i => i.Order)
                .FirstOrDefault();
            if (whOrder > 0)
            {
                orderSlot = whOrder + 1;
            }
        }

        if (itemId > 0)
        {
            var row = db.Items.First(i => i.Id == itemId);
            row.Caption = "epc_oms_orders_cp";
            row.Order = orderSlot;
            row.BackgroundColor = "#0f766e";
            row.FontawesomeClass = "fas fa-columns";
            row.ShowAnyway = 0;
        }
        else
        {
            var created = new ItemRow
            {
                Id = db.NextItemId++,
                ItemsGroup = shopGroupId,
                Caption = "epc_oms_orders_cp",
                Url = ordersUrl,
                Img = "",
                Order = orderSlot,
                BackgroundColor = "#0f766e",
                FontawesomeClass = "fas fa-columns",
                Target = "",
                ShowAnyway = 0
            };
            db.Items.Add(created);
            itemId = created.Id;
        }

        var removed = EpcCpOmsMenuCleanup(db, shopGroupId, itemId);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["shop_group"] = shopGroupId,
            ["shop_orders_item"] = itemId,
            ["order"] = orderSlot,
            ["removed"] = removed
        };
    }

    public static int EpcCpOmsMenuCleanup(RiseStore db, int shopGroupId, int keepOrdersItemId)
    {
        var removed = 0;
        foreach (var row in db.Items.Where(i => Like(i.Url, "%/shop/orders/statuses%") || (Like(i.Url, "%/shop/orders/items%") && !Like(i.Url, "%/shop/orders/items/%"))).ToList())
        {
            if (row.Id == keepOrdersItemId)
            {
                continue;
            }

            var url = QueryTail.Replace(row.Url ?? "", "");
            if (StatusesUrl.IsMatch(url) || ItemsUrl.IsMatch(url))
            {
                db.Items.Remove(row);
                removed++;
            }
        }

        foreach (var row in db.Items.Where(i => i.Url == "/<backend>/shop/orders/orders" && i.Id != keepOrdersItemId))
        {
            row.Caption = "epc_oms_orders_cp";
            row.FontawesomeClass = "fas fa-columns";
            row.BackgroundColor = "#0f766e";
        }

        _ = shopGroupId;
        return removed;
    }

    public static List<string> EpcCpSystemMenuHiddenUrlPatterns()
        =>
        [
            "/control/o-programme",
            "/control/obnovleniya",
            "/control/izmeneniya",
            "/content/usefull/changes_fc",
            "changes_fc" + ".php",
            "/version_control/about_program",
            "/version_control/updates"
        ];

    public static List<string> EpcCpSystemMenuHiddenLabels()
        =>
        [
            "about program",
            "docpart changes",
            "updates",
            "история изменений",
            "изменения docpart",
            "изменения",
            "о программе",
            "обновления"
        ];

    public static bool EpcCpSystemMenuItemHidden(string url, string caption = "")
    {
        url = QueryTail.Replace(url ?? "", "").ToLowerInvariant();
        foreach (var pat in EpcCpSystemMenuHiddenUrlPatterns())
        {
            if (pat != "" && url.Contains(pat.ToLowerInvariant(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        var label = (caption ?? "").Trim().ToLowerInvariant();
        if (label != "")
        {
            foreach (var needle in EpcCpSystemMenuHiddenLabels())
            {
                if (needle != "" && label == needle)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static string EpcCpSystemMenuItemLabel(RiseStore db, string caption)
    {
        caption = (caption ?? "").Trim();
        if (caption == "")
        {
            return "";
        }

        if (AlnumKey.IsMatch(caption))
        {
            var val = (db.LangTr.FirstOrDefault(t => t.StrKey == caption && t.LangCode == "en")?.Value ?? "").Trim();
            if (val != "")
            {
                return val;
            }
        }

        if (caption.Length > 0 && caption.All(char.IsDigit))
        {
            var id = int.Parse(caption, CultureInfo.InvariantCulture);
            var lang = db.Langs.FirstOrDefault(l => l.Id == id);
            if (lang is not null)
            {
                var val = (db.LangTr.FirstOrDefault(t => t.StrKey == lang.StrKey && t.LangCode == "en")?.Value ?? "").Trim();
                if (val != "")
                {
                    return val;
                }
            }
        }

        return caption;
    }

    public static Dictionary<string, object?> EpcCpSystemMenuCleanup(RiseStore db)
    {
        var removed = new List<Dictionary<string, object?>>();
        foreach (var row in db.Items.OrderBy(i => i.Id).ToList())
        {
            var label = EpcCpSystemMenuItemLabel(db, row.Caption);
            if (!EpcCpSystemMenuItemHidden(row.Url, label))
            {
                continue;
            }

            db.Items.Remove(row);
            removed.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = row.Id,
                ["url"] = row.Url,
                ["caption"] = row.Caption,
                ["label"] = label
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["removed"] = removed.Count,
            ["items"] = removed
        };
    }
}
