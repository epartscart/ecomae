using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-sound Control Panel guideline. PHP identifiers kept for the inventory:
/// <c>epc_cpg_h</c>, <c>epc_cpg_item_label</c>, <c>epc_cpg_page_hints</c>,
/// <c>epc_cpg_hint_for_url</c>, <c>epc_cpg_load_menu_tabs</c>.
/// Path: <c>cp/content/control/cp_guideline.php</c>.
/// GET never mints a session cookie. Leftover unique user helper stays injected.
/// </summary>
public static class PhpPlanQ1Sound
{
    public const string CpGuidelinePath = "cp/content/control/cp_guideline.php";

    public static Func<Dictionary<string, object?>?>? GetAdminSession { get; set; }
    public static Dictionary<string, object?>? UserSession { get; set; }
    public static Func<string>? Backend { get; set; }
    public static Func<string, string>? TranslateByKey { get; set; }
    public static Func<int, string>? TranslateById { get; set; }
    public static Func<Dictionary<string, object?>, bool>? IsAnable { get; set; }
    public static Func<List<Dictionary<string, object?>>>? QueryGroups { get; set; }
    public static Func<List<Dictionary<string, object?>>>? QueryItems { get; set; }
    public static Func<bool>? IsSuperCp { get; set; }
    public static Action<Dictionary<string, object?>>? FrameOpen { get; set; }
    public static Action? FrameClose { get; set; }
    public static Func<string>? Today { get; set; }
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        GetAdminSession = null;
        UserSession = null;
        Backend = null;
        TranslateByKey = null;
        TranslateById = null;
        IsAnable = null;
        QueryGroups = null;
        QueryItems = null;
        IsSuperCp = null;
        FrameOpen = null;
        FrameClose = null;
        Today = null;
        LastOutput = "";
    }

    public static string EpcCpgH(object? value) => H(value);

    public static string EpcCpgItemLabel(object? caption)
    {
        var text = Str(caption);
        if (TranslateByKey is not null && Regex.IsMatch(text, "^[A-Za-z0-9_]+$"))
        {
            return TranslateByKey(text);
        }

        if (IsPhpNumeric(text) && TranslateById is not null)
        {
            return TranslateById(ToInt(text.Trim()));
        }

        return text;
    }

    public static Dictionary<string, string> EpcCpgPageHints()
        => new(StringComparer.Ordinal)
        {
            ["/shop/price-management"] = "Supplier/warehouse margins (overall → brand → article), customer profiles, guest margin, brand & article rules, VAT. Live calculator.",
            ["/shop/prices/guide"] = "Step-by-step: upload CSV price lists, multi-vendor Excel, map columns, import to warehouse stock.",
            ["/shop/prices/prices_edit"] = "Browse imported price rows, preview site price per customer profile, edit lines.",
            ["/shop/prices"] = "Price list manager — upload, schedule, and monitor supplier price files.",
            ["/shop/orders/oms-guide"] = "Daily OMS: open queue → console areas (items, pay, docs, status, messages) → complete.",
            ["/shop/orders/guide"] = "Full order workflow: registration → checkout → emails → supplier LPO → CP processing.",
            ["/shop/orders/whatsapp-guide"] = "WhatsApp Phase 1: wa.me share buttons — customer quotes, staff order/LPO messages (EN+AR).",
            ["/shop/marketing/marketing"] = "Marketing & growth — 10 strategies (SEO, ads, marketplaces, WhatsApp, trust, international, email, B2B) with follow/review/KPI tracking.",
            ["/shop/orders/orders"] = "OMS one-page console: open orders, manage items/payment/docs/status/messages.",
            ["/shop/parts_agent_chats"] = "Review AI Parts Expert chat sessions — customer details, country, full transcript.",
            ["/shop/logistics"] = "Hub for warehouses, pickup points, delivery modes, and stock management.",
            ["/shop/logistics/storages"] = "Warehouses — address, LPO e-mail for supplier purchase orders.",
            ["/shop/logistics/stock"] = "Stock quantities by category and product.",
            ["/shop/crosses"] = "Cross-reference (interchange) management for part numbers.",
            ["/shop/demand_countries"] = "Export demand countries and vehicle tags for market intelligence.",
            ["/users/customer_approvals"] = "Approve new Retail / Wholesale registrations and assign currency.",
            ["/users/customer_mgmt"] = "Customer directory — orders, invoices, advances, returns, e-invoice profile.",
            ["/users/usermanager"] = "Search customers, edit profile, assign price profile group.",
            ["/control/config"] = "Settings redesign: jump-nav groups, Frontend/Backend impact chips for storefront-facing values (contacts, currency, search, refunds).",
            ["/control/notifications_settings"] = "E-mail and SMS notification templates for orders and system events.",
            ["/control/communications"] = "Test e-mail / SMS delivery from the control panel.",
            ["/control/cp-guideline"] = "This page — visual CP map, daily workflows, and Settings guide.",
            ["/shop/finance/erp"] = "ERP: sales revenue, customer receivables, supplier payables, purchases, cash & bank entries.",
            ["/shop/finance/erp/guide"] = "Step-by-step ERP guide — sales, AR, AP, cash/bank, COA, GL, P&L, balance sheet.",
            ["/shop/document_control/document_control"] = "Company docs, letterheads, and document templates used on invoices and PDFs.",
            ["/shop/logistics/whatsapp-guide"] = "WhatsApp logistics guide — share and notify flows for warehouse / delivery staff."
        };

    public static string EpcCpgHintForUrl(object? url, Dictionary<string, string>? hints = null)
    {
        var text = Str(url);
        hints ??= EpcCpgPageHints();
        foreach (var (pattern, hint) in hints)
        {
            if (text.Contains(pattern, StringComparison.Ordinal))
            {
                return hint;
            }
        }

        return "Open this section from the left menu. Access depends on your admin group.";
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcCpgLoadMenuTabs(string backend)
    {
        var tabs = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var group in QueryGroups?.Invoke() ?? [])
        {
            var id = Str(group.GetValueOrDefault("id"));
            tabs[id] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["caption"] = EpcCpgItemLabel(group.GetValueOrDefault("caption")),
                ["items"] = new List<Dictionary<string, object?>>()
            };
        }

        foreach (var raw in QueryItems?.Invoke() ?? [])
        {
            var item = new Dictionary<string, object?>(raw, StringComparer.Ordinal)
            {
                ["url"] = Str(raw.GetValueOrDefault("url")).Replace("<backend>", backend, StringComparison.Ordinal)
            };
            var showAnyway = ToInt(item.GetValueOrDefault("show_anyway"));
            if (!(IsAnable?.Invoke(item) ?? true) && showAnyway != 1)
            {
                continue;
            }

            var gid = Str(item.GetValueOrDefault("items_group"));
            if (!tabs.ContainsKey(gid))
            {
                tabs[gid] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["caption"] = "Other",
                    ["items"] = new List<Dictionary<string, object?>>()
                };
            }

            ((List<Dictionary<string, object?>>)tabs[gid]["items"]!).Add(item);
        }

        return tabs;
    }

    public static bool EpcCpgRequireSession()
    {
        var session = UserSession;
        if (session is null)
        {
            session = GetAdminSession?.Invoke();
            UserSession = session;
        }

        if (session is null || session.Count == 0)
        {
            var login = "/" + H(Backend?.Invoke() ?? "cp") + "/";
            Echo("<div class=\"alert alert-warning\">Please <a href=\"" + login
                 + "\">log in to the control panel</a> to view this guide.</div>");
            return false;
        }

        return true;
    }

    public static void EpcCpgRenderPage()
    {
        var backend = Backend?.Invoke() ?? "cp";
        var hints = EpcCpgPageHints();
        var menuTabs = EpcCpgLoadMenuTabs(backend);
        var baseCp = "/" + backend;
        var isSuper = IsSuperCp?.Invoke() == true;
        var badge = isSuper ? "Super CP" : "Control panel";
        if (FrameOpen is not null)
        {
            FrameOpen(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["class"] = "epc-cpg-dashboard",
                ["hero"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["badge"] = badge }
            });
        }
        else
        {
            Echo("FRAME_OPEN:epc-cpg-dashboard:" + badge);
        }

        string A(string path) => H(baseCp + path);

        Echo("\n<div class=\"epc-cpg-roles\">\n");
        Echo("\t<span class=\"epc-cpg-roles__label\">Roles</span>\n");
        Echo("\t<span class=\"epc-cpg-chip\"><i class=\"fa fa-car\"></i> epartscart.com — spare parts</span>\n");
        Echo("\t<span class=\"epc-cpg-chip\"><i class=\"fa fa-building\"></i> ecomae.com/cp — platform</span>\n");
        Echo("\t<span class=\"epc-cpg-chip\"><i class=\"fa fa-bars\"></i> Sidebar: SYSTEM · SHOP · CATALOG · USERS</span>\n");
        Echo("\t<a class=\"epc-cpg-chip\" href=\"/brochure\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-file-text-o\"></i> Product brochure</a>\n");
        Echo("</div>\n\n");
        Echo("<div class=\"epc-cpg-quick\">\n");
        Echo("\t<a href=\"" + A("/control/config") + "\">\n");
        Echo("\t\t<span class=\"epc-cpg-quick__ico\"><i class=\"fa fa-sliders\"></i></span>\n");
        Echo("\t\t<strong>Settings</strong>\n");
        Echo("\t\t<span>Jump-nav groups + Frontend / Backend impact chips for storefront values.</span>\n");
        Echo("\t</a>\n");
        Echo("\t<a href=\"" + A("/shop/price-management") + "\">\n");
        Echo("\t\t<span class=\"epc-cpg-quick__ico\"><i class=\"fa fa-percent\"></i></span>\n");
        Echo("\t\t<strong>Price profiles</strong>\n");
        Echo("\t\t<span>Retail / Wholesale margins, guest %, brand &amp; article rules.</span>\n");
        Echo("\t</a>\n");
        Echo("\t<a href=\"" + A("/shop/prices/guide") + "\">\n");
        Echo("\t\t<span class=\"epc-cpg-quick__ico\"><i class=\"fa fa-upload\"></i></span>\n");
        Echo("\t\t<strong>Price upload</strong>\n");
        Echo("\t\t<span>Import supplier CSV / multi-vendor Excel into warehouses.</span>\n");
        Echo("\t</a>\n");
        Echo("\t<a href=\"" + A("/shop/orders/oms-guide") + "\">\n");
        Echo("\t\t<span class=\"epc-cpg-quick__ico\"><i class=\"fa fa-list-alt\"></i></span>\n");
        Echo("\t\t<strong>OMS daily</strong>\n");
        Echo("\t\t<span>Queue → items → pay → docs → status → messages → done.</span>\n");
        Echo("\t</a>\n");
        Echo("\t<a href=\"" + A("/shop/orders/guide") + "\">\n");
        Echo("\t\t<span class=\"epc-cpg-quick__ico\"><i class=\"fa fa-truck\"></i></span>\n");
        Echo("\t\t<strong>Fulfilment</strong>\n");
        Echo("\t\t<span>Checkout → e-mails → supplier LPO → staff processing.</span>\n");
        Echo("\t</a>\n");
        Echo("\t<a href=\"" + A("/shop/orders/whatsapp-guide") + "\">\n");
        Echo("\t\t<span class=\"epc-cpg-quick__ico\"><i class=\"fa fa-whatsapp\"></i></span>\n");
        Echo("\t\t<strong>WhatsApp</strong>\n");
        Echo("\t\t<span>Quotes, cart share, staff order &amp; LPO messages (EN + AR).</span>\n");
        Echo("\t</a>\n");
        Echo("\t<a href=\"" + A("/users/customer_mgmt") + "\">\n");
        Echo("\t\t<span class=\"epc-cpg-quick__ico\"><i class=\"fa fa-users\"></i></span>\n");
        Echo("\t\t<strong>Customers</strong>\n");
        Echo("\t\t<span>Directory, orders, invoices, advances, returns.</span>\n");
        Echo("\t</a>\n");
        Echo("\t<a href=\"" + A("/users/customer_approvals") + "\">\n");
        Echo("\t\t<span class=\"epc-cpg-quick__ico\"><i class=\"fa fa-check-square-o\"></i></span>\n");
        Echo("\t\t<strong>Trade approvals</strong>\n");
        Echo("\t\t<span>Approve Retail / Wholesale registrations &amp; currency.</span>\n");
        Echo("\t</a>\n");
        Echo("</div>\n\n");
        Echo("<div class=\"epc-cpg-block\">\n");
        Echo("\t<div class=\"epc-cpg-block__head\">\n\t\t<div>\n");
        Echo("\t\t\t<h3>Daily workflows</h3>\n");
        Echo("\t\t\t<p>Follow the arrows left → right. Each step links into the live CP page.</p>\n");
        Echo("\t\t</div>\n\t</div>\n");
        Echo("\t<div class=\"epc-cpg-flows\">\n");
        EchoFlow("1", "Prices &amp; stock", "Shop", [
            ("1", "Upload list", A("/shop/prices/guide"), "Price upload guide"),
            ("2", "Verify rows", A("/shop/prices/prices_edit"), "Prices edit preview"),
            ("3", "Set margins", A("/shop/price-management"), "Price management"),
            ("4", "Assign profile", "", "Retail / Wholesale / guest")
        ]);
        Echo("\n");
        EchoFlow("2", "Orders &amp; fulfilment", "Shop", [
            ("1", "Approve trade", A("/users/customer_approvals"), "Customer approvals"),
            ("2", "Customer orders", "", "Search → cart → checkout"),
            ("3", "Process in OMS", A("/shop/orders/orders"), "Orders console"),
            ("4", "Share / LPO", A("/shop/orders/whatsapp-guide"), "WhatsApp guide")
        ]);
        Echo("\n");
        EchoFlow("3", "Customers &amp; profiles", "Users", [
            ("1", "Find customer", A("/users/customer_mgmt"), "Customer management"),
            ("2", "Approve account", A("/users/customer_approvals"), "Trade approvals"),
            ("3", "Assign margin", A("/shop/price-management"), "Price profiles"),
            ("4", "Storefront prices", "", "Customer sees their profile %")
        ]);
        Echo("\n");
        EchoFlow("4", "AI Parts Expert", "Shop", [
            ("1", "Enable agent", A("/control/config"), "Settings → agent toggle"),
            ("2", "Customers chat", "", "Storefront AI widget"),
            ("3", "Review chats", A("/shop/parts_agent_chats"), "AI agent chats")
        ]);
        Echo("\n");
        EchoFlow("5", "System &amp; notifications", "System", [
            ("1", "Site Settings", A("/control/config"), "Contacts, SMTP, shop"),
            ("2", "Templates", A("/control/notifications_settings"), "Notification settings"),
            ("3", "Send a test", A("/control/communications"), "Communications test")
        ]);
        Echo("\t</div>\n</div>\n\n");
        Echo("<div class=\"epc-cpg-block\">\n\t<div class=\"epc-cpg-block__head\">\n\t\t<div>\n");
        Echo("\t\t\t<h3>Settings page — what changed</h3>\n");
        Echo("\t\t\t<p>Open <a href=\"" + A("/control/config") + "\">Settings</a>. Blue chips mark values that affect the public storefront.</p>\n");
        Echo("\t\t</div>\n\t</div>\n");
        Echo("\t<div class=\"epc-cpg-settings\">\n");
        Echo("\t\t<div class=\"epc-cpg-settings__visual\">\n");
        Echo("\t\t\t<h4><i class=\"fa fa-desktop\"></i> Frontend vs Backend</h4>\n");
        Echo("\t\t\t<p>Use the left jump nav to open a group. Read the blue “Effect on frontend” line before you save.</p>\n");
        Echo("\t\t\t<div class=\"epc-cpg-settings__pills\">\n");
        Echo("\t\t\t\t<span>Frontend — customers see it</span>\n");
        Echo("\t\t\t\t<span>Backend — CP / integrations only</span>\n");
        Echo("\t\t\t</div>\n\t\t</div>\n");
        Echo("\t\t<ul class=\"epc-cpg-settings__list\">\n");
        Echo("\t\t\t<li><i class=\"fa fa-phone\"></i><span><strong>Contacts &amp; footer</strong> — phone, WhatsApp, offices appear on the public site.</span></li>\n");
        Echo("\t\t\t<li><i class=\"fa fa-shopping-cart\"></i><span><strong>Online store</strong> — currency, rounding, guest checkout, partial payment.</span></li>\n");
        Echo("\t\t\t<li><i class=\"fa fa-search\"></i><span><strong>Article search</strong> — results table, filters, async search layout.</span></li>\n");
        Echo("\t\t\t<li><i class=\"fa fa-undo\"></i><span><strong>Refunds</strong> — customer return requests and withholding text.</span></li>\n");
        Echo("\t\t\t<li><i class=\"fa fa-envelope\"></i><span><strong>E-mail / updates mailbox</strong> — mostly backend; customers only feel SMTP “from” name.</span></li>\n");
        Echo("\t\t</ul>\n\t</div>\n</div>\n\n");
        Echo("<div class=\"epc-cpg-block\">\n\t<div class=\"epc-cpg-block__head\">\n\t\t<div>\n");
        Echo("\t\t\t<h3>Menu map</h3>\n");
        Echo("\t\t\t<p>Every item from your left sidebar, grouped, with a short “what it does”.</p>\n");
        Echo("\t\t</div>\n\t</div>\n");
        Echo("\t<div class=\"epc-cpg-menu-grid\">\n\t\t");
        foreach (var tab in menuTabs.Values)
        {
            var items = tab.GetValueOrDefault("items") as List<Dictionary<string, object?>> ?? [];
            if (items.Count == 0)
            {
                continue;
            }

            Echo("\t\t<div class=\"epc-cpg-menu-card\">\n");
            Echo("\t\t\t<div class=\"epc-cpg-menu-card__head\">\n");
            Echo("\t\t\t\t" + H(tab.GetValueOrDefault("caption")) + "\t\t\t\t<small>" + items.Count + " pages</small>\n");
            Echo("\t\t\t</div>\n");
            foreach (var item in items)
            {
                var label = EpcCpgItemLabel(item.GetValueOrDefault("caption"));
                var url = Str(item.GetValueOrDefault("url"));
                var hint = EpcCpgHintForUrl(url, hints);
                var icon = Str(item.GetValueOrDefault("fontawesome_class"));
                Echo("\t\t\t\t\t\t<div class=\"epc-cpg-menu-row\">\n");
                Echo("\t\t\t\t<div>\n");
                Echo("\t\t\t\t\t<div class=\"epc-cpg-menu-row__title\">\n");
                Echo("\t\t\t\t\t\t");
                if (!PhpEmpty(icon))
                {
                    Echo("<i class=\"" + H(icon) + "\"></i>");
                }

                Echo("\t\t\t\t\t\t" + H(label) + "\t\t\t\t\t</div>\n");
                Echo("\t\t\t\t\t<span class=\"epc-cpg-menu-row__hint\">" + H(hint) + "</span>\n");
                Echo("\t\t\t\t</div>\n");
                Echo("\t\t\t\t<a class=\"btn btn-primary btn-xs\" href=\"" + H(url) + "\">Open</a>\n");
                Echo("\t\t\t</div>\n\t\t\t");
            }

            Echo("\t\t</div>\n\t\t");
        }

        Echo("\t</div>\n</div>\n\n");
        Echo("<div class=\"epc-cpg-block\">\n\t<div class=\"epc-cpg-block__head\">\n\t\t<div>\n");
        Echo("\t\t\t<h3>Margin stack</h3>\n");
        Echo("\t\t\t<p>Applied in order on the storefront. Full demo: <a href=\"" + A("/shop/price-management") + "\">Price management</a>.</p>\n");
        Echo("\t\t</div>\n\t</div>\n");
        Echo("\t<div class=\"epc-cpg-margin\">\n");
        Echo("\t\t<div class=\"epc-cpg-margin__item\">\n\t\t\t<strong>1 · Profile</strong>\n\t\t\t<span>Wholesale +5% on all brands</span>\n\t\t</div>\n");
        Echo("\t\t<div class=\"epc-cpg-margin__item\">\n\t\t\t<strong>2 · Brand</strong>\n\t\t\t<span>Retail MAZDA +15%</span>\n\t\t</div>\n");
        Echo("\t\t<div class=\"epc-cpg-margin__item\">\n\t\t\t<strong>3 · Article</strong>\n\t\t\t<span>TOYOTA 1140051020 +20%</span>\n\t\t</div>\n");
        Echo("\t\t<div class=\"epc-cpg-margin__item\">\n\t\t\t<strong>4 · Guest</strong>\n\t\t\t<span>+40% when not logged in</span>\n\t\t</div>\n");
        Echo("\t</div>\n</div>\n\n");
        Echo("<div class=\"epc-cpg-block\">\n\t<div class=\"epc-cpg-block__head\">\n\t\t<div>\n");
        Echo("\t\t\t<h3>Troubleshooting</h3>\n");
        Echo("\t\t\t<p>Quick fixes for the most common CP issues.</p>\n");
        Echo("\t\t</div>\n\t</div>\n");
        Echo("\t<div class=\"epc-cpg-trouble\">\n");
        Echo("\t\t<div class=\"epc-cpg-trouble__item\">\n\t\t\t<strong>Cannot open a page / privileges</strong>\n");
        Echo("\t\t\tYour admin group needs access under Content → access rights.\n\t\t</div>\n");
        Echo("\t\t<div class=\"epc-cpg-trouble__item\">\n\t\t\t<strong>Prices wrong on storefront</strong>\n");
        Echo("\t\t\tCheck profile rules in <a href=\"" + A("/shop/price-management") + "\">Price management</a>; preview in Prices edit.\n\t\t</div>\n");
        Echo("\t\t<div class=\"epc-cpg-trouble__item\">\n\t\t\t<strong>Order e-mails not sent</strong>\n");
        Echo("\t\t\t<a href=\"" + A("/control/communications") + "\">Communications test</a> + SMTP under <a href=\"" + A("/control/config") + "\">Settings</a> → E-mail.\n\t\t</div>\n");
        Echo("\t\t<div class=\"epc-cpg-trouble__item\">\n\t\t\t<strong>AI agent missing on site</strong>\n");
        Echo("\t\t\tSettings → enable <code>epc_parts_agent_enabled</code>, then hard-refresh the storefront.\n\t\t</div>\n");
        Echo("\t\t<div class=\"epc-cpg-trouble__item\">\n\t\t\t<strong>Red “Email/SMS not working” banner</strong>\n");
        Echo("\t\t\tFix SMTP / SMS under Settings and Communications before other work.\n\t\t</div>\n");
        Echo("\t\t<div class=\"epc-cpg-trouble__item\">\n\t\t\t<strong>Changed a contact but site unchanged</strong>\n");
        Echo("\t\t\tIn Settings, confirm the blue Frontend chip, Save, then reload the public page.\n\t\t</div>\n");
        Echo("\t</div>\n</div>\n\n");
        Echo("<p class=\"epc-cpg-foot\">\n");
        Echo("\tLast updated: " + H(Today?.Invoke() ?? "2026-10-10") + " · eParts Cart CP guideline ·\n");
        Echo("\t<a href=\"" + H(baseCp) + "\">Control panel home</a>\n");
        Echo("</p>\n");
        if (FrameClose is not null)
        {
            FrameClose();
        }
        else
        {
            Echo("FRAME_CLOSE");
        }
    }

    public static string Capture(Action render)
    {
        LastOutput = "";
        render();
        return LastOutput;
    }

    public static List<object> DumpTabs(Dictionary<string, Dictionary<string, object?>> tabs)
    {
        var hints = EpcCpgPageHints();
        var outList = new List<object>();
        foreach (var (gid, tab) in tabs)
        {
            var items = new List<object>();
            foreach (var item in tab.GetValueOrDefault("items") as List<Dictionary<string, object?>> ?? [])
            {
                items.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = ToInt(item.GetValueOrDefault("id")),
                    ["caption"] = EpcCpgItemLabel(item.GetValueOrDefault("caption")),
                    ["url"] = Str(item.GetValueOrDefault("url")),
                    ["hint"] = EpcCpgHintForUrl(item.GetValueOrDefault("url"), hints),
                    ["icon"] = Str(item.GetValueOrDefault("fontawesome_class"))
                });
            }

            outList.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["gid"] = gid,
                ["caption"] = Str(tab.GetValueOrDefault("caption")),
                ["count"] = items.Count,
                ["items"] = items
            });
        }

        return outList;
    }

    private static void EchoFlow(string num, string title, string badge, (string N, string Strong, string Href, string Text)[] steps)
    {
        Echo("\t\t<div class=\"epc-cpg-flow\">\n");
        Echo("\t\t\t<div class=\"epc-cpg-flow__top\">\n");
        Echo("\t\t\t\t<span class=\"epc-cpg-flow__num\">" + num + "</span>\n");
        Echo("\t\t\t\t<h4>" + title + "</h4>\n");
        Echo("\t\t\t\t<span class=\"epc-cpg-flow__badge\">" + badge + "</span>\n");
        Echo("\t\t\t</div>\n");
        Echo("\t\t\t<div class=\"epc-cpg-steps\">\n");
        foreach (var step in steps)
        {
            Echo("\t\t\t\t<div class=\"epc-cpg-step\">\n");
            Echo("\t\t\t\t\t<span class=\"epc-cpg-step__n\">Step " + step.N + "</span>\n");
            Echo("\t\t\t\t\t<strong>" + step.Strong + "</strong>\n");
            if (step.Href != "")
            {
                Echo("\t\t\t\t\t<a href=\"" + step.Href + "\">" + step.Text + "</a>\n");
            }
            else
            {
                Echo("\t\t\t\t\t<span>" + step.Text + "</span>\n");
            }

            Echo("\t\t\t\t</div>\n");
        }

        Echo("\t\t\t</div>\n\t\t</div>\n");
    }

    private static void Echo(string html) => LastOutput += html;

    private static string H(object? value)
    {
        var s = Str(value);
        return s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or "0" or 0 or 0L;

    private static bool IsPhpNumeric(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length > 0 && double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static int ToInt(object? value)
    {
        if (value is null or false or "")
        {
            return 0;
        }

        try
        {
            return Convert.ToInt32(Convert.ToDouble(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static string Str(object? value, string fallback = "")
        => value is null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
}
