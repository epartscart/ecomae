namespace EcomAE.Platform.Migration;

/// <summary>
/// Same-to-same CP Integrations Hub catalog (mirrors PHP <c>epc_integrations_catalog</c> / hub rows).
/// Not <c>epc_webhooks</c>. Secrets omitted. PHP configure URLs remain authoritative for writes.
/// </summary>
public static class CpIntegrationsHubCatalog
{
    private const string Be = "cp";

    private static readonly CpIntegrationsHubCatalogEntry[] Catalog =
    [
        Entry("email_smtp", "Email / SMTP", "fa-envelope", "#059669", "identity",
            "Transactional mail (orders, OTP, alerts) via tenant or platform SMTP.",
            $"/{Be}/control/portal/epc_cp_auth_settings", $"/{Be}/control/portal/epc_tenant_email_settings",
            $"/{Be}/control/portal/epc_integrations_guide#email_smtp"),
        Entry("oauth", "OAuth (Google, Microsoft…)", "fa-sign-in", "#2563eb", "identity",
            "Social / Microsoft login for CP and storefront — configured on Super CP.",
            $"/{Be}/control/portal/epc_cp_auth_settings", $"/{Be}/control/portal/epc_integrations_hub",
            $"/{Be}/control/portal/epc_integrations_guide#oauth", superOnly: true),
        Entry("registration_enhanced", "Registration enhanced", "fa-user-plus", "#0891b2", "identity",
            "Stronger signup flows, verification, and auth policies for tenants.",
            $"/{Be}/control/portal/epc_cp_auth_settings", $"/{Be}/control/portal/epc_integrations_hub",
            $"/{Be}/control/portal/epc_integrations_guide#registration_enhanced", superOnly: true),
        Entry("whatsapp", "WhatsApp sharing", "fa-whatsapp", "#16a34a", "identity",
            "wa.me order sharing with bilingual EN/AR templates for sales desks.",
            $"/{Be}/shop/orders/whatsapp-guide", $"/{Be}/shop/orders/whatsapp-guide",
            $"/{Be}/shop/orders/whatsapp-guide"),
        Entry("payment_gateways", "Payment gateways", "fa-credit-card", "#0369a1", "commerce",
            "Telr, GCC BNPL, JazzCash/Easypaisa, crypto, and per-account settlements.",
            $"/{Be}/shop/payments/payments", $"/{Be}/shop/payments/payments",
            $"/{Be}/control/portal/epc_integrations_guide#payment_gateways"),
        Entry("pos", "POS Terminal", "fa-cash-register", "#1d4ed8", "commerce",
            "Counter sales, cash/card tender, and ERP-linked receipts.",
            $"/{Be}/control/portal/epc_pos_tenant_manage", $"/{Be}/shop/pos/terminal",
            $"/{Be}/control/portal/epc_integrations_guide#pos"),
        Entry("tax_toolkit", "Tax Toolkit", "fa-globe", "#0f766e", "commerce",
            "Market VAT / tax profiles that follow the tenant country registration.",
            $"/{Be}/control/portal/epc_tax_toolkit_manage", $"/{Be}/shop/finance/erp",
            $"/{Be}/control/portal/epc_integrations_guide#tax_toolkit", superOnly: true),
        Entry("custom_shipping", "Custom & shipping", "fa-ship", "#0e7490", "commerce",
            "Customs declarations, LGP intake, and shipping reports inside ERP.",
            $"/{Be}/control/portal/epc_custom_shipping_guide",
            $"/{Be}/shop/finance/erp?area=custom_shipping&tab=custom_shipping&epc_erp_shell=1",
            $"/{Be}/control/portal/epc_custom_shipping_guide"),
        Entry("social_media_hub", "Social media hub", "fa-share-alt", "#db2777", "growth",
            "Publish calendars, account links, and AI-assisted social posts.",
            $"/{Be}/control/portal/epc_social_media_hub", $"/{Be}/control/portal/epc_social_media_hub",
            $"/{Be}/control/portal/epc_social_media_hub?tab=guide"),
        Entry("marketing_broadcast", "Marketing broadcast", "fa-paper-plane", "#ea580c", "growth",
            "Bulk email and WhatsApp campaigns with audience segments.",
            $"/{Be}/control/portal/epc_marketing_broadcast", $"/{Be}/control/portal/epc_marketing_broadcast",
            $"/{Be}/control/portal/epc_marketing_broadcast?tab=guide"),
        Entry("web_tracker", "Web tracker", "fa-line-chart", "#0284c7", "growth",
            "GA4 / Meta / TikTok pixels and storefront event wiring.",
            $"/{Be}/control/portal/epc_web_tracker", $"/{Be}/control/portal/epc_web_tracker",
            $"/{Be}/control/portal/epc_integrations_guide#web_tracker"),
        Entry("visual_page_editor", "Visual page editor", "fa-paint-brush", "#be185d", "growth",
            "Drag-and-drop landing and content blocks for the storefront.",
            $"/{Be}/control/portal/epc_visual_page_editor", $"/{Be}/control/portal/epc_visual_page_editor",
            $"/{Be}/control/portal/epc_integrations_guide#visual_page_editor"),
        Entry("auto_price_ai", "Auto Price AI", "fa-magic", "#0f766e", "catalog",
            "Discover, compare, and import competitive parts pricing by market.",
            $"/{Be}/control/portal/epc_auto_price_engine", $"/{Be}/control/portal/epc_auto_price_engine",
            $"/{Be}/control/portal/epc_auto_price_guide"),
        Entry("parts_agent", "AI parts agent", "fa-robot", "#0e7490", "catalog",
            "Conversational parts expert for staff and storefront shoppers.",
            $"/{Be}/shop/parts_agent_chats", $"/{Be}/shop/parts_agent_chats",
            $"/{Be}/control/portal/epc_integrations_guide#parts_agent"),
        Entry("api_integrations", "API clients & keys", "fa-code", "#475569", "data",
            "Catalog & Price PRO clients plus tenant-scoped REST API keys.",
            $"/{Be}/control/portal/epc_api_clients_manage", $"/{Be}/control/portal/epc_api_clients_manage",
            $"/{Be}/control/portal/epc_api_documentation_guide"),
        Entry("power_bi", "Power BI", "fa-bar-chart", "#ca8a04", "data",
            "JSON/CSV datasets for Desktop refresh and optional report embed.",
            $"/{Be}/control/portal/epc_power_bi", $"/{Be}/control/portal/epc_power_bi",
            $"/{Be}/control/portal/epc_power_bi_guide"),
        Entry("mobile_apps", "Mobile apps (Android / iOS)", "fa-mobile-alt", "#dc2626", "platform",
            "PWA install plus Capacitor targets for CP, ERP, and storefront.",
            $"/{Be}/control/portal/epc_mobile_apps", $"/{Be}/control/portal/epc_mobile_apps",
            $"/{Be}/control/portal/epc_integrations_guide#mobile_apps"),
        Entry("tenant_registry", "Multi-tenant registry", "fa-sitemap", "#0369a1", "platform",
            "Live tenant hosts, DB credentials, and Super CP feature toggles.",
            $"/{Be}/shop/tenant_hub/tenant_hub", "",
            $"/{Be}/control/portal/epc_integrations_guide#tenant_registry", superOnly: true),
    ];

    public static IReadOnlyList<CpIntegrationsHubCatalogEntry> All => Catalog;

    /// <summary>Tenant hub rows (excludes super-only-config-only entries without tenant URL).</summary>
    public static IReadOnlyList<CpIntegrationDigest> BuildTenantDigests(
        IReadOnlyDictionary<string, bool>? featureFlags = null,
        int limit = 200)
    {
        var rows = new List<CpIntegrationDigest>();
        foreach (var meta in Catalog)
        {
            if (meta.SuperOnly && string.IsNullOrWhiteSpace(meta.TenantUrl))
            {
                continue;
            }

            var enabled = featureFlags is not null && featureFlags.TryGetValue(meta.Key, out var flag)
                ? flag
                : meta.DefaultEnabled;
            var configureUrl = string.IsNullOrWhiteSpace(meta.TenantUrl)
                ? $"/{Be}/control/portal/epc_integrations_hub"
                : meta.TenantUrl;
            if (meta.SuperOnly && !string.IsNullOrWhiteSpace(meta.TenantUrl) &&
                meta.TenantUrl.Contains("epc_integrations_hub", StringComparison.Ordinal))
            {
                configureUrl = $"/{Be}/control/portal/epc_integrations_hub";
            }

            rows.Add(new CpIntegrationDigest(
                meta.Key,
                meta.Label,
                meta.Blurb,
                meta.Category,
                enabled,
                configureUrl,
                meta.Guide,
                meta.Icon,
                meta.Color));
            if (rows.Count >= limit)
            {
                break;
            }
        }

        return rows;
    }

    public static CpIntegrationsSummary Summarize(IReadOnlyList<CpIntegrationDigest> rows, string source, string message)
    {
        var categories = rows.Select(r => r.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var guides = rows.Count(r => !string.IsNullOrWhiteSpace(r.Guide));
        var active = rows.Count(r => r.Active);
        return new(rows.Count, active, guides, categories, source, message);
    }

    private static CpIntegrationsHubCatalogEntry Entry(
        string key, string label, string icon, string color, string category, string blurb,
        string superUrl, string tenantUrl, string guide, bool superOnly = false, bool defaultEnabled = true) =>
        new(key, label, icon, color, category, blurb, superUrl, tenantUrl, guide, superOnly, defaultEnabled);
}

public sealed record CpIntegrationsHubCatalogEntry(
    string Key,
    string Label,
    string Icon,
    string Color,
    string Category,
    string Blurb,
    string SuperUrl,
    string TenantUrl,
    string Guide,
    bool SuperOnly,
    bool DefaultEnabled);
