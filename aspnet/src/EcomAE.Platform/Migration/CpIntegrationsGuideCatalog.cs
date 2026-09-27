namespace EcomAE.Platform.Migration;

/// <summary>PHP <c>epc_integrations_guide.php</c> section link.</summary>
public sealed record CpIntegrationsGuideLink(string Label, string Url);

/// <summary>PHP <c>epc_integrations_guide.php</c> rendered section.</summary>
public sealed record CpIntegrationsGuideSection(
    string Key,
    string Label,
    string Icon,
    string Category,
    string Summary,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Tips,
    string ConfigureUrl,
    string DedicatedGuide,
    IReadOnlyList<CpIntegrationsGuideLink> Links);

/// <summary>
/// Same-to-same operator handbook behind PHP <c>cp/content/control/portal/epc_integrations_guide.php</c>:
/// the PHP <c>$sections</c> copy plus its ordering, tenant filtering, and action-row rules.
/// </summary>
public static class CpIntegrationsGuideCatalog
{
    private const string Be = "cp";

    private static readonly IReadOnlyList<string> FallbackSteps =
    [
        "Open Configure from the Integrations hub.",
        "Save settings.",
        "Run any built-in test action.",
    ];

    private sealed record Section(
        string Summary,
        IReadOnlyList<string> Steps,
        IReadOnlyList<string> Tips,
        IReadOnlyList<CpIntegrationsGuideLink> Links,
        bool SuperOnlyLastLink = false);

    private static CpIntegrationsGuideLink Link(string label, string url) => new(label, url);

    private static readonly IReadOnlyDictionary<string, Section> Sections =
        new Dictionary<string, Section>(StringComparer.Ordinal)
        {
            ["email_smtp"] = new(
                "Deliver order confirmations, OTP codes, and staff alerts through SMTP.",
                [
                    "Open Email / SMTP settings (tenant page for shops; Super CP auth settings for platform defaults).",
                    "Enter host, port, encryption, username, and password. Save.",
                    "Use Send test email — confirm delivery before go-live.",
                    "Place a test order and verify the customer receipt arrives.",
                ],
                [
                    "Prefer a dedicated mailbox (orders@…) with SPF/DKIM aligned to your domain.",
                    "If tests fail, check firewall allow-lists for outbound 465/587.",
                ],
                [
                    Link("Tenant SMTP", $"/{Be}/control/portal/epc_tenant_email_settings"),
                    Link("Auth settings", $"/{Be}/control/portal/epc_cp_auth_settings"),
                ]),
            ["oauth"] = new(
                "Google / Microsoft (and related) OAuth for CP or storefront login — Super CP configures app credentials.",
                [
                    "Create OAuth clients in Google Cloud / Microsoft Entra with redirect URIs for your hosts.",
                    "Paste Client ID / Secret under Super CP → Auth settings.",
                    "Enable the providers you want, then test login in an incognito window.",
                    "Toggle the oauth feature per tenant under Tenant features if a shop should not use it.",
                ],
                ["Redirect URI mismatches are the #1 failure — copy exact https://host/… callback paths."],
                [
                    Link("Auth settings", $"/{Be}/control/portal/epc_cp_auth_settings"),
                    Link("Tenant features", $"/{Be}/control/portal/epc_tenant_features"),
                ]),
            ["registration_enhanced"] = new(
                "Stronger signup and verification policies controlled from Super CP auth settings.",
                [
                    "Review registration fields and verification requirements under Auth settings.",
                    "Ensure SMTP works so verification emails send.",
                    "Create a test customer account on the tenant storefront.",
                    "Confirm the account appears in Users and can place an order.",
                ],
                ["Keep OTP / email verification on for B2B tenants handling trade accounts."],
                [Link("Auth settings", $"/{Be}/control/portal/epc_cp_auth_settings")]),
            ["whatsapp"] = new(
                "Phase 1 wa.me sharing — staff open WhatsApp with bilingual EN/AR prefilled order text.",
                [
                    "Open the WhatsApp sharing guide and confirm sales display name / phone.",
                    "From Orders, use Share on WhatsApp on a sample order.",
                    "Verify the message opens with English + Arabic text and correct order link.",
                    "Train the desk to send from phone or desktop WhatsApp (no Business API required yet).",
                ],
                ["For broadcast campaigns, use Marketing broadcast instead of one-off order shares."],
                [
                    Link("WhatsApp guide", $"/{Be}/shop/orders/whatsapp-guide"),
                    Link("Orders", $"/{Be}/shop/orders/orders"),
                ]),
            ["payment_gateways"] = new(
                "Checkout rails: Telr, GCC BNPL (Tabby, Tamara, MyFatoorah, Tap, HyperPay…), Pakistan wallets, crypto, and settlement accounts.",
                [
                    "Open Shop → Payments and enable the methods you need.",
                    "Enter merchant keys / store IDs; save each gateway.",
                    "Optional: Payments → Accounts to set who receives funds (platform / office / vendor).",
                    "Run a small live or sandbox purchase and confirm notify / settlement rows.",
                ],
                [
                    "Keep at least one card gateway + one local method for GCC shoppers.",
                    "Crypto (NOWPayments) needs a live IPN URL reachable from the internet.",
                ],
                [
                    Link("Payments", $"/{Be}/shop/payments/payments"),
                    Link("Accounts tab", $"/{Be}/shop/payments/payments?tab=accounts"),
                ]),
            ["pos"] = new(
                "Counter terminal for walk-in sales linked to ERP stock and receipts.",
                [
                    "Confirm the POS feature is enabled for the tenant.",
                    "Open POS Terminal, select storage / cashier context.",
                    "Ring a test sale (cash or card tender) and print/preview the receipt.",
                    "Verify the order and stock movement in ERP / Orders.",
                ],
                ["Use a dedicated browser profile on the counter PC to avoid session mix-ups."],
                [Link("POS Terminal", $"/{Be}/shop/pos/terminal")]),
            ["tax_toolkit"] = new(
                "Market tax / VAT profiles driven by the tenant country registration (Super CP configures toolkit).",
                [
                    "Confirm the tenant market/country under Tenant hub / country profile.",
                    "Super CP: review Tax Toolkit manage for available profiles.",
                    "In ERP, verify tax lines on a sample invoice match the market rate.",
                    "Document any exemptions for B2B / free-zone customers.",
                ],
                ["Tax follows market — ask Super CP if the registered country must change."],
                [Link("ERP", $"/{Be}/shop/finance/erp")]),
            ["custom_shipping"] = new(
                "Customs declarations, LGP warehouse intake, and declaration reports inside ERP.",
                [
                    "Read the Custom & Shipping operator guide for declaration types.",
                    "Open ERP → Custom & Shipping and create a sample declaration.",
                    "Run one of the built-in reports to confirm filters and export.",
                    "Train ops on which declaration type maps to each shipment lane.",
                ],
                ["Keep HS codes and consignee details accurate — reports inherit declaration fields."],
                [
                    Link("Shipping guide", $"/{Be}/control/portal/epc_custom_shipping_guide"),
                    Link("ERP module", $"/{Be}/shop/finance/erp?area=custom_shipping&tab=custom_shipping&epc_erp_shell=1"),
                ]),
            ["social_media_hub"] = new(
                "Plan, generate, and publish social content with account links and calendars.",
                [
                    "Open Social media hub and connect / list your brand accounts.",
                    "Read the in-hub Guide tab for posting specs per network.",
                    "Create a draft post, review AI suggestions, schedule or copy out.",
                    "Track which posts shipped and update weekly.",
                ],
                ["Reuse Marketing broadcast for WhatsApp/email blasts; keep Social hub for public networks."],
                [
                    Link("Social hub", $"/{Be}/control/portal/epc_social_media_hub"),
                    Link("Guide tab", $"/{Be}/control/portal/epc_social_media_hub?tab=guide"),
                ]),
            ["marketing_broadcast"] = new(
                "Bulk email and WhatsApp campaigns with audience segments.",
                [
                    "Open Marketing broadcast and complete the Guide tab once.",
                    "Build or select an audience segment.",
                    "Compose a campaign, send a test to yourself, then launch.",
                    "Review delivery stats before the next blast.",
                ],
                ["SMTP must be healthy before email campaigns; WhatsApp uses the sharing / API path configured for the tenant."],
                [
                    Link("Broadcast hub", $"/{Be}/control/portal/epc_marketing_broadcast"),
                    Link("Guide", $"/{Be}/control/portal/epc_marketing_broadcast?tab=guide"),
                ]),
            ["web_tracker"] = new(
                "Wire GA4, Meta, TikTok (and similar) pixels / tags into the storefront.",
                [
                    "Open Web tracker and paste measurement IDs / pixel IDs.",
                    "Save and hard-refresh the storefront.",
                    "Use browser tag assistants / debug views to confirm page_view fires.",
                    "Place a test add-to-cart / purchase if conversion events are enabled.",
                ],
                ["Never put secrets in page HTML — only public pixel IDs belong in the tracker UI."],
                [Link("Web tracker", $"/{Be}/control/portal/epc_web_tracker")]),
            ["visual_page_editor"] = new(
                "Compose landing and content blocks for the storefront without deploying PHP.",
                [
                    "Open Visual page editor and pick the page / template to edit.",
                    "Add blocks, set images and CTAs, preview on mobile width.",
                    "Publish and verify the live URL.",
                    "Keep one owner for homepage changes to avoid overwrite conflicts.",
                ],
                ["Prefer one hero composition — avoid stacking promo cards in the first viewport."],
                [Link("Visual editor", $"/{Be}/control/portal/epc_visual_page_editor")]),
            ["auto_price_ai"] = new(
                "Discover competitive parts prices, compare lines, and import into your catalog.",
                [
                    "Open Auto Price AI and complete the CP guide once.",
                    "Run discovery for your market / product lines.",
                    "Compare results, mark winners, and import selected rows.",
                    "Re-check margins in ERP before publishing price changes.",
                ],
                ["Discovery sources follow the tenant country profile — wrong market = wrong comps."],
                [
                    Link("Auto Price engine", $"/{Be}/control/portal/epc_auto_price_engine"),
                    Link("Auto Price guide", $"/{Be}/control/portal/epc_auto_price_guide"),
                ]),
            ["parts_agent"] = new(
                "AI parts expert chats for staff (and optional storefront assist).",
                [
                    "Confirm the feature is enabled under Tenant features.",
                    "Open Parts agent chats and ask for a known OEM / aftermarket part.",
                    "Validate suggested OEMs / cross refs against your catalog.",
                    "Document which staff roles may use the agent with customers.",
                ],
                ["Treat suggestions as assistive — always confirm fitment before quoting."],
                [Link("Parts agent", $"/{Be}/shop/parts_agent_chats")]),
            ["api_integrations"] = new(
                "Catalog & Price PRO client keys and tenant-scoped REST API access.",
                [
                    "Open API clients manage to issue or rotate Catalog & Price PRO keys.",
                    "Super CP: follow the API documentation guide for /epc-api/v1 scopes.",
                    "Call /epc-api/v1/health then a scoped endpoint with X-API-Key.",
                    "Store plain keys in a password manager — hashes only live in DB.",
                ],
                ["Power BI keys need read:bi (or read:erp / read:*)."],
                [
                    Link("API clients", $"/{Be}/control/portal/epc_api_clients_manage"),
                    Link("API docs guide", $"/{Be}/control/portal/epc_api_documentation_guide"),
                ],
                SuperOnlyLastLink: true),
            ["power_bi"] = new(
                "Pull JSON/CSV datasets into Power BI Desktop; optional workspace embed in CP.",
                [
                    "Create an API key with read:bi (or read:erp / read:*).",
                    "Follow Portal → Power BI guide — load KPIs CSV first.",
                    "Publish the report and confirm scheduled refresh once.",
                    "Optional: save workspace / report IDs under Power BI settings for embed preview.",
                ],
                ["Use HTTPS + header X-API-Key; never put keys in the report URL query string for production."],
                [
                    Link("Power BI settings", $"/{Be}/control/portal/epc_power_bi"),
                    Link("Power BI guide", $"/{Be}/control/portal/epc_power_bi_guide"),
                ]),
            ["mobile_apps"] = new(
                "Installable PWA today; Capacitor native shells for CP, ERP, and storefront.",
                [
                    "Open Mobile apps and confirm store URLs / package IDs.",
                    "On a phone: open CP → Add to Home Screen; verify offline shell loads.",
                    "For native builds, follow the Level 1 mobile guide (Capacitor targets).",
                    "Test push / deep links only after PWA baseline works.",
                ],
                ["PWA uses the real responsive CP — no separate mobile UI to maintain."],
                [Link("Mobile apps", $"/{Be}/control/portal/epc_mobile_apps")]),
            ["tenant_registry"] = new(
                "Super CP registry of live tenants, hosts, DB credentials, and feature flags.",
                [
                    "Open Tenant hub and locate the shop by site_key / hostname.",
                    "Confirm status is live and credentials resolve.",
                    "Use Tenant features to enable/disable integrations for that shop.",
                    "Smoke-test the tenant CP Integrations hub after flag changes.",
                ],
                ["Never paste production DB passwords into tickets — use the hub + vault."],
                [
                    Link("Tenant hub", $"/{Be}/shop/tenant_hub/tenant_hub"),
                    Link("Tenant features", $"/{Be}/control/portal/epc_tenant_features"),
                ]),
        };

    /// <summary>
    /// PHP guide body: catalog order inside each category, tenant CP drops super-only entries that
    /// have no tenant URL, configure follows the Super/Tenant URL, and the dedicated-guide button only
    /// shows when the resolved guide is not this page.
    /// </summary>
    public static IReadOnlyList<CpIntegrationsGuideSection> Build(bool isSuper)
    {
        var sections = new List<CpIntegrationsGuideSection>(CpIntegrationsHubCatalog.All.Count);
        foreach (var category in CpIntegrationsHubCatalog.Categories)
        {
            foreach (var meta in CpIntegrationsHubCatalog.All)
            {
                if (!string.Equals(meta.Category, category.Key, StringComparison.Ordinal) || Hidden(meta, isSuper))
                {
                    continue;
                }

                var configureUrl = isSuper
                    ? meta.SuperUrl
                    : (string.IsNullOrWhiteSpace(meta.TenantUrl) ? meta.SuperUrl : meta.TenantUrl);
                var canConfigure = configureUrl.Length > 0 && (isSuper || !meta.SuperOnly);

                var dedicated = CpIntegrationsHubCatalog.ResolveGuide(meta.Guide, meta.Key);
                if (dedicated.Contains("epc_integrations_guide", StringComparison.Ordinal))
                {
                    dedicated = "";
                }

                Sections.TryGetValue(meta.Key, out var php);
                IReadOnlyList<CpIntegrationsGuideLink> phpLinks = php?.Links ?? [];
                if (php is { SuperOnlyLastLink: true } && !isSuper)
                {
                    phpLinks = phpLinks.Take(phpLinks.Count - 1).ToList();
                }

                var links = phpLinks
                    .Where(link => link.Url.Length > 0 && !string.Equals(link.Url, configureUrl, StringComparison.Ordinal))
                    .ToList();

                sections.Add(new CpIntegrationsGuideSection(
                    meta.Key,
                    meta.Label,
                    meta.Icon,
                    meta.Category,
                    php?.Summary ?? meta.Blurb,
                    php?.Steps ?? FallbackSteps,
                    php?.Tips ?? [],
                    canConfigure ? configureUrl : "",
                    dedicated,
                    links));
            }
        }

        return sections;
    }

    /// <summary>PHP table of contents — catalog order, same tenant filter as the body.</summary>
    public static IReadOnlyList<(string Key, string Label)> Contents(bool isSuper)
        => CpIntegrationsHubCatalog.All
            .Where(meta => !Hidden(meta, isSuper))
            .Select(meta => (meta.Key, meta.Label))
            .ToList();

    private static bool Hidden(CpIntegrationsHubCatalogEntry meta, bool isSuper)
        => !isSuper && meta.SuperOnly && string.IsNullOrWhiteSpace(meta.TenantUrl);
}
