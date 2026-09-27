using EcomAE.Platform.Cp;

namespace EcomAE.Platform.Migration;

/// <summary>PHP <c>epc_integrations_categories()</c> entry.</summary>
public sealed record CpIntegrationsHubCategory(string Key, string Label, string Icon, string Blurb);

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

    /// <summary>PHP <c>epc_integrations_categories()</c> — order drives the page sections.</summary>
    public static IReadOnlyList<CpIntegrationsHubCategory> Categories { get; } =
    [
        new("identity", "Identity & messaging", "fa-id-badge", "Login, email delivery, and customer messaging channels."),
        new("commerce", "Commerce & payments", "fa-shopping-bag", "Checkout, POS, tax, and settlement rails."),
        new("growth", "Marketing & growth", "fa-bullhorn", "Broadcast, social, tracking, and storefront content."),
        new("catalog", "Catalog & AI", "fa-cubes", "Pricing intelligence and parts expert assistants."),
        new("data", "Data & APIs", "fa-database", "REST keys, Power BI datasets, and analytics embeds."),
        new("platform", "Platform", "fa-server", "Mobile shells and multi-tenant control."),
    ];

    public static IReadOnlyList<CpIntegrationsHubCatalogEntry> All => Catalog;

    public static string CategoryLabel(string category)
        => Categories.FirstOrDefault(c => string.Equals(c.Key, category, StringComparison.OrdinalIgnoreCase))?.Label
           ?? category;

    /// <summary>
    /// PHP <c>epc_integrations_resolve_guide()</c>: catalog guide values are already CP paths;
    /// anything else collapses onto the master guide with the feature key as anchor.
    /// </summary>
    public static string ResolveGuide(string guide, string key)
    {
        var master = $"/{Be}/control/portal/epc_integrations_guide";
        var value = (guide ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return key.Length > 0 ? master + "#" + Uri.EscapeDataString(key) : master;
        }

        if (value.StartsWith("http://", StringComparison.Ordinal)
            || value.StartsWith("https://", StringComparison.Ordinal)
            || value.StartsWith('/'))
        {
            return value;
        }

        var anchor = key.Length > 0 ? key : value.TrimStart('#');
        return master + "#" + Uri.EscapeDataString(anchor);
    }

    /// <summary>
    /// PHP <c>epc_integrations_hub_rows()</c>: Super CP shows every entry with a super URL and treats
    /// all of them as active; tenant CP hides super-only entries without a tenant URL, points super-only
    /// configuration back at the hub and falls back from the Super-only API docs guide.
    /// </summary>
    public static IReadOnlyList<CpIntegrationsHubCard> BuildHubCards(
        bool isSuper,
        IReadOnlyDictionary<string, bool>? featureFlags = null)
    {
        var hub = $"/{Be}/control/portal/epc_integrations_hub";
        var rows = new List<CpIntegrationsHubCard>(Catalog.Length);
        foreach (var meta in Catalog)
        {
            if (isSuper && string.IsNullOrWhiteSpace(meta.SuperUrl))
            {
                continue;
            }

            if (!isSuper && string.IsNullOrWhiteSpace(meta.TenantUrl) && meta.SuperOnly)
            {
                continue;
            }

            var enabled = isSuper
                || (featureFlags is not null && featureFlags.TryGetValue(meta.Key, out var flag)
                    ? flag
                    : meta.DefaultEnabled);

            var configureUrl = isSuper
                ? meta.SuperUrl
                : (string.IsNullOrWhiteSpace(meta.TenantUrl) ? meta.SuperUrl : meta.TenantUrl);
            if (!isSuper && meta.SuperOnly)
            {
                configureUrl = hub;
            }

            var guide = meta.Guide;
            if (!isSuper && guide.Contains("epc_api_documentation_guide", StringComparison.Ordinal))
            {
                guide = hub.Replace("epc_integrations_hub", "epc_integrations_guide", StringComparison.Ordinal)
                    + "#api_integrations";
            }

            rows.Add(new CpIntegrationsHubCard(
                meta.Key,
                meta.Label,
                meta.Icon,
                meta.Color,
                meta.Category,
                meta.Blurb,
                enabled,
                configureUrl,
                ResolveGuide(guide, meta.Key),
                meta.SuperOnly));
        }

        return rows;
    }

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
