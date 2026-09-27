namespace EcomAE.Platform.Migration;

/// <summary>
/// Typed twin of the PHP Industry-settings registries: <c>epc_portal_industries()</c> / <c>epc_portal_ecosystems()</c>
/// (content/general_pages/epc_portal.php), <c>epc_portal_pack_definitions()</c> (epc_portal_db.php),
/// <c>epc_portal_erp_modules_registry()</c> + presets (epc_portal_erp_modules.php),
/// <c>epc_portal_industry_style_templates()</c> (epc_portal_theme_templates.php) and
/// <c>epc_storefront_layouts_for_industry()</c> (epc_storefront_layouts.php).
/// Generated from the PHP reference so /cp/control/portal/industry_settings offers exactly the same choices.
/// </summary>
public sealed record PortalThemePalette(
    string Primary,
    string PrimaryDark,
    string Accent,
    string SidebarFrom,
    string SidebarTo,
    string HeroFrom,
    string HeroTo);

public sealed record PortalStyleTemplate(string Id, string Label, string Desc, PortalThemePalette Theme);

public sealed record PortalSettingsIndustry(
    string Code,
    string Name,
    string Ecosystem,
    string Icon,
    IReadOnlyList<string> CpPacks,
    IReadOnlyList<PortalStyleTemplate> StyleTemplates,
    IReadOnlyList<PortalStorefrontLayout> Layouts);

public sealed record PortalStorefrontLayout(string Id, string Label, string Desc, bool Default);

public sealed record PortalEcosystem(string Code, string Name);

public sealed record PortalCpPack(string Code, string Label, string Desc, string Icon);

public sealed record PortalErpModule(
    string Id,
    string Label,
    string Desc,
    string Area,
    string Icon,
    bool DefaultErpOnly,
    bool DefaultFull);

public sealed record PortalErpModulePreset(string Id, string Label, string Desc, IReadOnlyList<string> Modules);

public static class PortalSettingsCatalog
{
    public static IReadOnlyList<PortalEcosystem> Ecosystems { get; } =
    [
        new("commerce", "Commerce ecosystem"),
        new("business_services", "Business Services ecosystem"),
        new("lifestyle_consumer", "Lifestyle & Consumer ecosystem"),
        new("asset_sharing", "Asset & Sharing ecosystem"),
        new("digital_technology", "Digital & Technology ecosystem"),
    ];

    /// <summary>PHP <c>epc_cp_translate_language_options()</c> (content/general_pages/epc_cp_translate.php).</summary>
    public static IReadOnlyList<(string Code, string Label)> CpLanguages { get; } =
    [
        ("en", "English"),
        ("ar", "العربية"),
        ("ru", "Русский"),
        ("fr", "Français"),
        ("de", "Deutsch"),
        ("hi", "हिन्दी"),
        ("ur", "اردو"),
        ("es", "Español"),
        ("pt", "Português"),
        ("it", "Italiano"),
        ("tr", "Türkçe"),
        ("zh-CN", "中文（简体）"),
        ("ja", "日本語"),
        ("ko", "한국어"),
        ("nl", "Nederlands"),
        ("pl", "Polski"),
        ("fa", "فارسی"),
        ("bn", "বাংলা"),
        ("ta", "தமிழ்"),
        ("vi", "Tiếng Việt"),
        ("id", "Indonesia"),
        ("th", "ไทย"),
        ("uk", "Українська"),
        ("he", "עברית"),
        ("sv", "Svenska"),
        ("cs", "Čeština"),
        ("ro", "Română"),
    ];

    public static IReadOnlyList<PortalCpPack> Packs { get; } =
    [
        new("core", "Core CP", "Content, users, templates, print docs", "fa-cog"),
        new("commerce", "Commerce", "Orders, cart, payments, channels", "fa-shopping-cart"),
        new("catalogue", "Catalogue", "Product catalogue and bulk upload", "fa-th-large"),
        new("auto_parts", "Auto parts", "Prices, crosses, parts search, procurement", "fa-car"),
        new("logistics", "Logistics", "Warehouses, storages, offices — shared module", "fa-truck"),
        new("erp", "ERP & finance", "GL, VAT, e-invoicing, payroll — shared module", "fa-university"),
        new("crm", "CRM & pipeline", "Native leads, opportunities, activities & kanban pipeline", "fa-handshake-o"),
        new("professional", "Professional services", "Customers, approvals, demand intelligence", "fa-briefcase"),
        new("marketing", "Marketing", "Campaigns and analytics", "fa-bullhorn"),
        new("super_platform", "Super CP (ecomae)", "Tenant hub — all client sites", "fa-cloud"),
    ];

    public static IReadOnlyList<PortalErpModule> ErpModules { get; } =
    [
        new("erp_overview", "Overview", "Dashboard and cross-department workflow", "overview", "fa-th-large", true, true),
        new("erp_sales", "Sales", "CRM, proposals, orders, revenue, receivables, fulfilment, delivery, invoices", "sales", "fa-line-chart", true, true),
        new("erp_purchasing", "Purchasing", "Suppliers, RFQ, POs, payables, procurement link", "purchasing", "fa-shopping-basket", true, true),
        new("erp_finance", "Finance", "Treasury, GL, COA, VAT, e-invoicing, opening balances", "finance", "fa-university", true, true),
        new("erp_operations", "Operations", "Inventory, fixed assets, manufacturing", "operations", "fa-cubes", true, true),
        new("erp_custom_shipping", "Custom & Shipping", "UAE customs declarations and logistics documentation", "custom_shipping", "fa-ship", true, true),
        new("erp_people", "People", "HR, payroll, staff profiles, expense reports", "people", "fa-users", true, true),
        new("erp_insights", "Insights", "Reports, marketing campaigns, knowledge base, multi-entity, audit", "insights", "fa-bar-chart", true, true),
        new("erp_collaboration", "Collaboration", "Agenda, contacts, documents", "collaboration", "fa-calendar", true, true),
        new("erp_enterprise", "Enterprise", "Business units, financial dimensions, budgeting and listings", "enterprise", "fa-building-o", true, true),
        new("erp_jewellery", "Jewellery industry pack", "Jewellery-specific fields in Inventory, Purchase, Sales, Service, Finance (shown when industry = jewellery)", "setup", "fa-diamond", true, true),
    ];

    public static IReadOnlyList<PortalErpModulePreset> ErpPresets { get; } =
    [
        new("full_erp", "Full ERP", "All ERP areas enabled", ["erp_overview", "erp_sales", "erp_purchasing", "erp_finance", "erp_operations", "erp_custom_shipping", "erp_people", "erp_insights", "erp_collaboration", "erp_enterprise", "erp_jewellery"]),
        new("hr_only", "HR only", "Overview + HR / payroll / staff", ["erp_overview", "erp_people", "erp_collaboration"]),
        new("customs_logistics", "Customs + Logistics", "Overview + customs declarations & shipping docs", ["erp_overview", "erp_custom_shipping", "erp_collaboration"]),
        new("finance_einvoice", "Finance + e-invoice", "Overview + finance + sales invoices", ["erp_overview", "erp_finance", "erp_sales", "erp_collaboration"]),
    ];

    public static IReadOnlyList<PortalSettingsIndustry> Industries { get; } =
    [
        new("auto_parts", "Auto spare parts", "commerce", "fa-car",
            ["core", "commerce", "auto_parts", "logistics", "erp", "professional", "marketing"],
            [
                new("classic", "Racing red", "Bold motorsport red, charcoal nav, amber highlights", new("#dc2626", "#991b1b", "#f97316", "#0f172a", "#1e293b", "#0b1220", "#1e3a5f")),
                new("modern", "Carbon silver", "Clean gunmetal grey with orange call-to-action", new("#ea580c", "#c2410c", "#fbbf24", "#1e293b", "#475569", "#0f172a", "#334155")),
                new("midnight", "Night track", "Dark garage UI with neon red and cyan accents", new("#ef4444", "#b91c1c", "#22d3ee", "#020617", "#0f172a", "#020617", "#1e293b")),
                new("signature", "Torch amber", "High-contrast amber primary for parts counters", new("#f59e0b", "#d97706", "#ef4444", "#292524", "#44403c", "#1c1917", "#78350f")),
            ],
            []),
        new("automotive_full_vehicles", "Vehicle sales & showrooms", "commerce", "fa-car",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Vehicle sales & showrooms — Classic", "Default brand colours for this industry", new("#b91c1c", "#7f1d1d", "#f59e0b", "#111827", "#1f2937", "#0f172a", "#7f1d1d")),
                new("modern", "Vehicle sales & showrooms — Bright", "Lighter panels and stronger contrast", new("#b91c1c", "#7f1d1d", "#f59e0b", "#334155", "#475569", "#0f172a", "#7f1d1d")),
                new("midnight", "Vehicle sales & showrooms — Midnight", "Dark navigation with vivid accents", new("#f59e0b", "#b91c1c", "#b91c1c", "#020617", "#111827", "#020617", "#1f2937")),
                new("signature", "Vehicle sales & showrooms — Accent", "Accent-led hero and sidebar highlights", new("#f59e0b", "#7f1d1d", "#b91c1c", "#111827", "#1f2937", "#0f172a", "#7f1d1d")),
            ],
            []),
        new("electronics", "Electronics & gadgets", "commerce", "fa-microchip",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Circuit blue", "Tech retail blue — trusted electronics look", new("#2563eb", "#1d4ed8", "#38bdf8", "#0f172a", "#1e3a8a", "#0f172a", "#1e40af")),
                new("modern", "Arctic steel", "Light grey chrome with sky-blue highlights", new("#3b82f6", "#2563eb", "#7dd3fc", "#1e293b", "#475569", "#0f172a", "#1d4ed8")),
                new("midnight", "Virgin red", "Bold retail red, black nav, white panels — megastore look", new("#e10a0a", "#b00808", "#000000", "#000000", "#1a1a1a", "#000000", "#2d2d2d")),
                new("signature", "Volt cyan", "Cyan-forward gaming & gadget energy", new("#06b6d4", "#0891b2", "#a78bfa", "#083344", "#164e63", "#020617", "#312e81")),
            ],
            [
                new("hero_carousel", "Hero Carousel", "Full-width sliding hero banner with deal sections below — Virgin Megastore style", true),
                new("category_grid", "Category Grid First", "Category tiles on top, trending products below — Amazon style navigation", false),
                new("product_showcase", "Product Showcase", "Clean product grid with filters — minimal, product-focused storefront", false),
                new("brand_focused", "Brand Focused", "Brand logos first, shop by brand — Best Buy style brand-centric", false),
            ]),
        new("it_hardware_accessories", "IT hardware & accessories", "commerce", "fa-laptop",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "IT hardware & accessories — Classic", "Default brand colours for this industry", new("#2563eb", "#1d4ed8", "#38bdf8", "#0f172a", "#1e3a8a", "#0c4a6e", "#1e40af")),
                new("modern", "IT hardware & accessories — Bright", "Lighter panels and stronger contrast", new("#2563eb", "#1d4ed8", "#38bdf8", "#334155", "#475569", "#0c4a6e", "#1e40af")),
                new("midnight", "IT hardware & accessories — Midnight", "Dark navigation with vivid accents", new("#38bdf8", "#2563eb", "#2563eb", "#020617", "#0f172a", "#020617", "#1e3a8a")),
                new("signature", "IT hardware & accessories — Accent", "Accent-led hero and sidebar highlights", new("#38bdf8", "#1d4ed8", "#2563eb", "#0f172a", "#1e3a8a", "#0c4a6e", "#1e40af")),
            ],
            []),
        new("fashion", "Fashion & apparel", "commerce", "fa-shopping-bag",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Rose couture", "Magenta editorial — boutique fashion default", new("#be185d", "#9d174d", "#ec4899", "#1f1020", "#4a1942", "#1f1020", "#701a75")),
                new("modern", "Blush studio", "Soft pink on cool grey — lookbook ready", new("#db2777", "#be185d", "#f9a8d4", "#374151", "#4b5563", "#1f1020", "#9d174d")),
                new("midnight", "Noir runway", "Dark luxe with neon pink accent", new("#f472b6", "#db2777", "#e879f9", "#18181b", "#3f3f46", "#09090b", "#4a044e")),
                new("signature", "Lavender dusk", "Muted violet hero with rose gold accent", new("#a855f7", "#7e22ce", "#f9a8d4", "#3b0764", "#581c87", "#1e1b4b", "#6b21a8")),
            ],
            [
                new("editorial", "Editorial Lookbook", "Full-screen lifestyle imagery with collection links — Namshi/ASOS style", true),
                new("collection_grid", "Collection Grid", "Visual collection tiles with hover effects — Pinterest style discovery", false),
                new("minimal_boutique", "Minimal Boutique", "Clean white space, large product photography — luxury boutique feel", false),
                new("trend_feed", "Trend Feed", "Social-media style scrollable feed with shoppable posts — Gen Z appeal", false),
            ]),
        new("jewellery", "Jewellery & luxury goods", "commerce", "fa-diamond",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Champagne gold", "Luxury gold on black — jewellery default", new("#b45309", "#92400e", "#fbbf24", "#1c1917", "#44403c", "#1c1917", "#78350f")),
                new("modern", "Ivory luxe", "Cream storefront with rich amber CTAs", new("#d97706", "#b45309", "#fcd34d", "#292524", "#44403c", "#1c1917", "#92400e")),
                new("midnight", "Onyx gallery", "Pure black gallery with gold foil accent", new("#fbbf24", "#d97706", "#fef3c7", "#0c0a09", "#292524", "#000000", "#78350f")),
                new("signature", "Rose gold", "Blush metal tone for fine jewellery houses", new("#e11d48", "#be123c", "#fbbf24", "#4c0519", "#881337", "#1c1917", "#9f1239")),
            ],
            [
                new("luxury_showcase", "Luxury Showcase", "Full-width hero with gold accents, collection highlights — Kiyasha style", true),
                new("collection_gallery", "Collection Gallery", "Visual collection cards (Bridal, Gold, Diamond) as main navigation", false),
                new("catalog_filter", "Catalog with Filters", "Product grid with karat/weight/price filters — search-first approach", false),
                new("editorial_luxury", "Editorial Luxury", "Magazine-style with lifestyle imagery and storytelling — Tiffany style", false),
            ]),
        new("medical", "Medical supplies", "commerce", "fa-medkit",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Clinical sky", "Hospital-grade blue — clean and reassuring", new("#0284c7", "#0369a1", "#22d3ee", "#0c4a6e", "#164e63", "#0c4a6e", "#155e75")),
                new("modern", "Care blue", "Soft sky panels for patient-friendly portals", new("#0ea5e9", "#0284c7", "#7dd3fc", "#1e3a8a", "#334155", "#0c4a6e", "#0369a1")),
                new("midnight", "ICU navy", "Deep navy with mint safety accent", new("#38bdf8", "#0284c7", "#34d399", "#082f49", "#0c4a6e", "#042f2e", "#164e63")),
                new("signature", "Healing mint", "Mint green primary for wellness suppliers", new("#14b8a6", "#0d9488", "#38bdf8", "#134e4a", "#115e59", "#042f2e", "#0e7490")),
            ],
            []),
        new("pharmacy_retail", "Pharmacy & drugstore", "commerce", "fa-plus-square",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Pharmacy & drugstore — Classic", "Default brand colours for this industry", new("#0d9488", "#0f766e", "#5eead4", "#134e4a", "#115e59", "#042f2e", "#0f766e")),
                new("modern", "Pharmacy & drugstore — Bright", "Lighter panels and stronger contrast", new("#0d9488", "#0f766e", "#5eead4", "#334155", "#475569", "#042f2e", "#0f766e")),
                new("midnight", "Pharmacy & drugstore — Midnight", "Dark navigation with vivid accents", new("#5eead4", "#0d9488", "#0d9488", "#020617", "#134e4a", "#020617", "#115e59")),
                new("signature", "Pharmacy & drugstore — Accent", "Accent-led hero and sidebar highlights", new("#5eead4", "#0f766e", "#0d9488", "#134e4a", "#115e59", "#042f2e", "#0f766e")),
            ],
            []),
        new("nutrition_supplements", "Food supplements & nutrition", "commerce", "fa-leaf",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Food supplements & nutrition — Classic", "Default brand colours for this industry", new("#15803d", "#166534", "#86efac", "#14532d", "#166534", "#052e16", "#15803d")),
                new("modern", "Food supplements & nutrition — Bright", "Lighter panels and stronger contrast", new("#15803d", "#166534", "#86efac", "#334155", "#475569", "#052e16", "#15803d")),
                new("midnight", "Food supplements & nutrition — Midnight", "Dark navigation with vivid accents", new("#86efac", "#15803d", "#15803d", "#020617", "#14532d", "#020617", "#166534")),
                new("signature", "Food supplements & nutrition — Accent", "Accent-led hero and sidebar highlights", new("#86efac", "#166534", "#15803d", "#14532d", "#166534", "#052e16", "#15803d")),
            ],
            []),
        new("food_beverage", "Food & beverage / restaurants", "commerce", "fa-cutlery",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Food & beverage / restaurants — Classic", "Default brand colours for this industry", new("#ea580c", "#c2410c", "#fb923c", "#7c2d12", "#9a3412", "#431407", "#c2410c")),
                new("modern", "Food & beverage / restaurants — Bright", "Lighter panels and stronger contrast", new("#ea580c", "#c2410c", "#fb923c", "#334155", "#475569", "#431407", "#c2410c")),
                new("midnight", "Food & beverage / restaurants — Midnight", "Dark navigation with vivid accents", new("#fb923c", "#ea580c", "#ea580c", "#020617", "#7c2d12", "#020617", "#9a3412")),
                new("signature", "Food & beverage / restaurants — Accent", "Accent-led hero and sidebar highlights", new("#fb923c", "#c2410c", "#ea580c", "#7c2d12", "#9a3412", "#431407", "#c2410c")),
            ],
            []),
        new("grocery_retail", "Grocery & supermarket", "commerce", "fa-shopping-cart",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Grocery & supermarket — Classic", "Default brand colours for this industry", new("#16a34a", "#15803d", "#facc15", "#14532d", "#166534", "#052e16", "#15803d")),
                new("modern", "Grocery & supermarket — Bright", "Lighter panels and stronger contrast", new("#16a34a", "#15803d", "#facc15", "#334155", "#475569", "#052e16", "#15803d")),
                new("midnight", "Grocery & supermarket — Midnight", "Dark navigation with vivid accents", new("#facc15", "#16a34a", "#16a34a", "#020617", "#14532d", "#020617", "#166534")),
                new("signature", "Grocery & supermarket — Accent", "Accent-led hero and sidebar highlights", new("#facc15", "#15803d", "#16a34a", "#14532d", "#166534", "#052e16", "#15803d")),
            ],
            []),
        new("furniture_interiors", "Furniture & interiors shop", "commerce", "fa-home",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Furniture & interiors shop — Classic", "Default brand colours for this industry", new("#92400e", "#78350f", "#d6d3d1", "#1c1917", "#44403c", "#292524", "#78716c")),
                new("modern", "Furniture & interiors shop — Bright", "Lighter panels and stronger contrast", new("#92400e", "#78350f", "#d6d3d1", "#334155", "#475569", "#292524", "#78716c")),
                new("midnight", "Furniture & interiors shop — Midnight", "Dark navigation with vivid accents", new("#d6d3d1", "#92400e", "#92400e", "#020617", "#1c1917", "#020617", "#44403c")),
                new("signature", "Furniture & interiors shop — Accent", "Accent-led hero and sidebar highlights", new("#d6d3d1", "#78350f", "#92400e", "#1c1917", "#44403c", "#292524", "#78716c")),
            ],
            []),
        new("building_materials", "Building materials trading", "commerce", "fa-cubes",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Building materials trading — Classic", "Default brand colours for this industry", new("#78716c", "#57534e", "#f59e0b", "#1c1917", "#292524", "#0c0a09", "#44403c")),
                new("modern", "Building materials trading — Bright", "Lighter panels and stronger contrast", new("#78716c", "#57534e", "#f59e0b", "#334155", "#475569", "#0c0a09", "#44403c")),
                new("midnight", "Building materials trading — Midnight", "Dark navigation with vivid accents", new("#f59e0b", "#78716c", "#78716c", "#020617", "#1c1917", "#020617", "#292524")),
                new("signature", "Building materials trading — Accent", "Accent-led hero and sidebar highlights", new("#f59e0b", "#57534e", "#78716c", "#1c1917", "#292524", "#0c0a09", "#44403c")),
            ],
            []),
        new("fmcg_wholesale", "FMCG & general trading", "commerce", "fa-truck",
            ["core", "commerce", "catalogue", "logistics", "erp"],
            [
                new("classic", "FMCG & general trading — Classic", "Default brand colours for this industry", new("#0369a1", "#075985", "#38bdf8", "#0c4a6e", "#075985", "#082f49", "#0c4a6e")),
                new("modern", "FMCG & general trading — Bright", "Lighter panels and stronger contrast", new("#0369a1", "#075985", "#38bdf8", "#334155", "#475569", "#082f49", "#0c4a6e")),
                new("midnight", "FMCG & general trading — Midnight", "Dark navigation with vivid accents", new("#38bdf8", "#0369a1", "#0369a1", "#020617", "#0c4a6e", "#020617", "#075985")),
                new("signature", "FMCG & general trading — Accent", "Accent-led hero and sidebar highlights", new("#38bdf8", "#075985", "#0369a1", "#0c4a6e", "#075985", "#082f49", "#0c4a6e")),
            ],
            []),
        new("industrial_equipment", "Industrial equipment trading", "commerce", "fa-cogs",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Industrial equipment trading — Classic", "Default brand colours for this industry", new("#475569", "#334155", "#94a3b8", "#0f172a", "#1e293b", "#020617", "#334155")),
                new("modern", "Industrial equipment trading — Bright", "Lighter panels and stronger contrast", new("#475569", "#334155", "#94a3b8", "#334155", "#475569", "#020617", "#334155")),
                new("midnight", "Industrial equipment trading — Midnight", "Dark navigation with vivid accents", new("#94a3b8", "#475569", "#475569", "#020617", "#0f172a", "#020617", "#1e293b")),
                new("signature", "Industrial equipment trading — Accent", "Accent-led hero and sidebar highlights", new("#94a3b8", "#334155", "#475569", "#0f172a", "#1e293b", "#020617", "#334155")),
            ],
            []),
        new("agricultural_products", "Agriculture & farm products", "commerce", "fa-pagelines",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Agriculture & farm products — Classic", "Default brand colours for this industry", new("#65a30d", "#4d7c0f", "#a3e635", "#365314", "#3f6212", "#1a2e05", "#4d7c0f")),
                new("modern", "Agriculture & farm products — Bright", "Lighter panels and stronger contrast", new("#65a30d", "#4d7c0f", "#a3e635", "#334155", "#475569", "#1a2e05", "#4d7c0f")),
                new("midnight", "Agriculture & farm products — Midnight", "Dark navigation with vivid accents", new("#a3e635", "#65a30d", "#65a30d", "#020617", "#365314", "#020617", "#3f6212")),
                new("signature", "Agriculture & farm products — Accent", "Accent-led hero and sidebar highlights", new("#a3e635", "#4d7c0f", "#65a30d", "#365314", "#3f6212", "#1a2e05", "#4d7c0f")),
            ],
            []),
        new("perfume_cosmetics", "Perfume & cosmetics trading", "commerce", "fa-magic",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Perfume & cosmetics trading — Classic", "Default brand colours for this industry", new("#db2777", "#be185d", "#f9a8d4", "#500724", "#831843", "#500724", "#9d174d")),
                new("modern", "Perfume & cosmetics trading — Bright", "Lighter panels and stronger contrast", new("#db2777", "#be185d", "#f9a8d4", "#334155", "#475569", "#500724", "#9d174d")),
                new("midnight", "Perfume & cosmetics trading — Midnight", "Dark navigation with vivid accents", new("#f9a8d4", "#db2777", "#db2777", "#020617", "#500724", "#020617", "#831843")),
                new("signature", "Perfume & cosmetics trading — Accent", "Accent-led hero and sidebar highlights", new("#f9a8d4", "#be185d", "#db2777", "#500724", "#831843", "#500724", "#9d174d")),
            ],
            []),
        new("pet_services", "Pet shop & animal services", "commerce", "fa-paw",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Pet shop & animal services — Classic", "Default brand colours for this industry", new("#d97706", "#b45309", "#fcd34d", "#78350f", "#92400e", "#451a03", "#b45309")),
                new("modern", "Pet shop & animal services — Bright", "Lighter panels and stronger contrast", new("#d97706", "#b45309", "#fcd34d", "#334155", "#475569", "#451a03", "#b45309")),
                new("midnight", "Pet shop & animal services — Midnight", "Dark navigation with vivid accents", new("#fcd34d", "#d97706", "#d97706", "#020617", "#78350f", "#020617", "#92400e")),
                new("signature", "Pet shop & animal services — Accent", "Accent-led hero and sidebar highlights", new("#fcd34d", "#b45309", "#d97706", "#78350f", "#92400e", "#451a03", "#b45309")),
            ],
            []),
        new("health", "Health & wellness", "lifestyle_consumer", "fa-heartbeat",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Forest wellness", "Natural green — supplements & lifestyle", new("#16a34a", "#15803d", "#4ade80", "#14532d", "#166534", "#14532d", "#15803d")),
                new("modern", "Fresh meadow", "Bright leaf green on soft sage backgrounds", new("#22c55e", "#16a34a", "#86efac", "#1e3a2f", "#365314", "#14532d", "#166534")),
                new("midnight", "Deep zen", "Dark forest UI with lime highlights", new("#4ade80", "#16a34a", "#a3e635", "#052e16", "#14532d", "#022c22", "#166534")),
                new("signature", "Citrus vitality", "Lime & orange zest for active brands", new("#84cc16", "#65a30d", "#fb923c", "#365314", "#3f6212", "#1a2e05", "#15803d")),
            ],
            []),
        new("beauty_skincare", "Beauty salon & skincare", "lifestyle_consumer", "fa-female",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Beauty salon & skincare — Classic", "Default brand colours for this industry", new("#e11d48", "#be123c", "#fb7185", "#4c0519", "#881337", "#4c0519", "#9f1239")),
                new("modern", "Beauty salon & skincare — Bright", "Lighter panels and stronger contrast", new("#e11d48", "#be123c", "#fb7185", "#334155", "#475569", "#4c0519", "#9f1239")),
                new("midnight", "Beauty salon & skincare — Midnight", "Dark navigation with vivid accents", new("#fb7185", "#e11d48", "#e11d48", "#020617", "#4c0519", "#020617", "#881337")),
                new("signature", "Beauty salon & skincare — Accent", "Accent-led hero and sidebar highlights", new("#fb7185", "#be123c", "#e11d48", "#4c0519", "#881337", "#4c0519", "#9f1239")),
            ],
            []),
        new("fitness_training", "Gym & fitness centres", "lifestyle_consumer", "fa-heartbeat",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Gym & fitness centres — Classic", "Default brand colours for this industry", new("#dc2626", "#b91c1c", "#fbbf24", "#450a0a", "#7f1d1d", "#450a0a", "#991b1b")),
                new("modern", "Gym & fitness centres — Bright", "Lighter panels and stronger contrast", new("#dc2626", "#b91c1c", "#fbbf24", "#334155", "#475569", "#450a0a", "#991b1b")),
                new("midnight", "Gym & fitness centres — Midnight", "Dark navigation with vivid accents", new("#fbbf24", "#dc2626", "#dc2626", "#020617", "#450a0a", "#020617", "#7f1d1d")),
                new("signature", "Gym & fitness centres — Accent", "Accent-led hero and sidebar highlights", new("#fbbf24", "#b91c1c", "#dc2626", "#450a0a", "#7f1d1d", "#450a0a", "#991b1b")),
            ],
            []),
        new("hospitality_travel", "Hospitality, hotels & travel", "lifestyle_consumer", "fa-building",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Hospitality, hotels & travel — Classic", "Default brand colours for this industry", new("#0ea5e9", "#0284c7", "#fbbf24", "#0c4a6e", "#075985", "#082f49", "#0369a1")),
                new("modern", "Hospitality, hotels & travel — Bright", "Lighter panels and stronger contrast", new("#0ea5e9", "#0284c7", "#fbbf24", "#334155", "#475569", "#082f49", "#0369a1")),
                new("midnight", "Hospitality, hotels & travel — Midnight", "Dark navigation with vivid accents", new("#fbbf24", "#0ea5e9", "#0ea5e9", "#020617", "#0c4a6e", "#020617", "#075985")),
                new("signature", "Hospitality, hotels & travel — Accent", "Accent-led hero and sidebar highlights", new("#fbbf24", "#0284c7", "#0ea5e9", "#0c4a6e", "#075985", "#082f49", "#0369a1")),
            ],
            []),
        new("clinics_telemedicine", "Clinics & telemedicine", "lifestyle_consumer", "fa-stethoscope",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "Clinics & telemedicine — Classic", "Default brand colours for this industry", new("#0891b2", "#0e7490", "#67e8f9", "#164e63", "#155e75", "#083344", "#0e7490")),
                new("modern", "Clinics & telemedicine — Bright", "Lighter panels and stronger contrast", new("#0891b2", "#0e7490", "#67e8f9", "#334155", "#475569", "#083344", "#0e7490")),
                new("midnight", "Clinics & telemedicine — Midnight", "Dark navigation with vivid accents", new("#67e8f9", "#0891b2", "#0891b2", "#020617", "#164e63", "#020617", "#155e75")),
                new("signature", "Clinics & telemedicine — Accent", "Accent-led hero and sidebar highlights", new("#67e8f9", "#0e7490", "#0891b2", "#164e63", "#155e75", "#083344", "#0e7490")),
            ],
            []),
        new("tax_advisory", "Tax & advisory", "business_services", "fa-balance-scale",
            ["core", "commerce", "professional", "tax_advisory", "erp", "logistics", "marketing"],
            [
                new("classic", "Trust teal", "Professional teal — default for tax & advisory firms", new("#0d9488", "#0f766e", "#14b8a6", "#042f2e", "#134e4a", "#042f2e", "#115e59")),
                new("modern", "Ocean blue", "Bright coastal blue, calm client-portal feel", new("#0891b2", "#0e7490", "#67e8f9", "#164e63", "#334155", "#0c4a6e", "#155e75")),
                new("midnight", "Midnight counsel", "Deep green-black with gold trust accents", new("#2dd4bf", "#0d9488", "#fbbf24", "#022c22", "#064e3b", "#022c22", "#134e4a")),
                new("signature", "Sandstone gold", "Warm stone neutrals with teal CTAs", new("#0f766e", "#115e59", "#d4a574", "#44403c", "#57534e", "#292524", "#134e4a")),
            ],
            [
                new("professional_services", "Professional Services", "Service cards with pricing tiers, trust signals — PrimeInvest style", true),
                new("calculator_led", "Calculator Led", "Interactive calculators (VAT, CT) as hero — conversion-focused", false),
                new("corporate_clean", "Corporate Clean", "Minimal corporate with team photos, credentials — Big 4 inspired", false),
            ]),
        new("consultancy", "Consultancy", "business_services", "fa-briefcase",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "Royal purple", "Consulting purple — authority and clarity", new("#7c3aed", "#6d28d9", "#a78bfa", "#2e1065", "#4c1d95", "#2e1065", "#5b21b6")),
                new("modern", "Slate advisory", "Cool violet on professional grey", new("#8b5cf6", "#7c3aed", "#c4b5fd", "#312e81", "#4c1d95", "#1e1b4b", "#6d28d9")),
                new("midnight", "Executive noir", "Dark boardroom with violet edge light", new("#a78bfa", "#7c3aed", "#f472b6", "#1e1b4b", "#312e81", "#0f0a1a", "#4c1d95")),
                new("signature", "Rose quartz", "Soft pink accent for boutique consultancies", new("#db2777", "#be185d", "#a78bfa", "#4a044e", "#701a75", "#1e1b4b", "#6d28d9")),
            ],
            [
                new("professional_services", "Professional Services", "Service cards with pricing, trust signals", true),
                new("calculator_led", "Calculator Led", "Interactive calculators as hero — conversion-focused", false),
                new("corporate_clean", "Corporate Clean", "Minimal corporate with team photos", false),
            ]),
        new("legal_services", "Legal services", "business_services", "fa-gavel",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "Legal services — Classic", "Default brand colours for this industry", new("#1e3a8a", "#1e40af", "#93c5fd", "#172554", "#1e3a8a", "#0f172a", "#1e3a8a")),
                new("modern", "Legal services — Bright", "Lighter panels and stronger contrast", new("#1e3a8a", "#1e40af", "#93c5fd", "#334155", "#475569", "#0f172a", "#1e3a8a")),
                new("midnight", "Legal services — Midnight", "Dark navigation with vivid accents", new("#93c5fd", "#1e3a8a", "#1e3a8a", "#020617", "#172554", "#020617", "#1e3a8a")),
                new("signature", "Legal services — Accent", "Accent-led hero and sidebar highlights", new("#93c5fd", "#1e40af", "#1e3a8a", "#172554", "#1e3a8a", "#0f172a", "#1e3a8a")),
            ],
            []),
        new("accounting_auditing", "Accounting & auditing", "business_services", "fa-calculator",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "Accounting & auditing — Classic", "Default brand colours for this industry", new("#0f766e", "#115e59", "#5eead4", "#042f2e", "#134e4a", "#022c22", "#0f766e")),
                new("modern", "Accounting & auditing — Bright", "Lighter panels and stronger contrast", new("#0f766e", "#115e59", "#5eead4", "#334155", "#475569", "#022c22", "#0f766e")),
                new("midnight", "Accounting & auditing — Midnight", "Dark navigation with vivid accents", new("#5eead4", "#0f766e", "#0f766e", "#020617", "#042f2e", "#020617", "#134e4a")),
                new("signature", "Accounting & auditing — Accent", "Accent-led hero and sidebar highlights", new("#5eead4", "#115e59", "#0f766e", "#042f2e", "#134e4a", "#022c22", "#0f766e")),
            ],
            []),
        new("hr_recruitment", "HR & recruitment", "business_services", "fa-users",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "HR & recruitment — Classic", "Default brand colours for this industry", new("#7c3aed", "#6d28d9", "#c4b5fd", "#2e1065", "#4c1d95", "#1e1b4b", "#5b21b6")),
                new("modern", "HR & recruitment — Bright", "Lighter panels and stronger contrast", new("#7c3aed", "#6d28d9", "#c4b5fd", "#334155", "#475569", "#1e1b4b", "#5b21b6")),
                new("midnight", "HR & recruitment — Midnight", "Dark navigation with vivid accents", new("#c4b5fd", "#7c3aed", "#7c3aed", "#020617", "#2e1065", "#020617", "#4c1d95")),
                new("signature", "HR & recruitment — Accent", "Accent-led hero and sidebar highlights", new("#c4b5fd", "#6d28d9", "#7c3aed", "#2e1065", "#4c1d95", "#1e1b4b", "#5b21b6")),
            ],
            []),
        new("marketing_digital", "Digital marketing & media agency", "business_services", "fa-bullhorn",
            ["core", "professional", "marketing", "commerce"],
            [
                new("classic", "Digital marketing & media agency — Classic", "Default brand colours for this industry", new("#db2777", "#be185d", "#f472b6", "#500724", "#9d174d", "#500724", "#be185d")),
                new("modern", "Digital marketing & media agency — Bright", "Lighter panels and stronger contrast", new("#db2777", "#be185d", "#f472b6", "#334155", "#475569", "#500724", "#be185d")),
                new("midnight", "Digital marketing & media agency — Midnight", "Dark navigation with vivid accents", new("#f472b6", "#db2777", "#db2777", "#020617", "#500724", "#020617", "#9d174d")),
                new("signature", "Digital marketing & media agency — Accent", "Accent-led hero and sidebar highlights", new("#f472b6", "#be185d", "#db2777", "#500724", "#9d174d", "#500724", "#be185d")),
            ],
            []),
        new("education_training", "Education & training", "business_services", "fa-graduation-cap",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "Education & training — Classic", "Default brand colours for this industry", new("#4f46e5", "#4338ca", "#a5b4fc", "#1e1b4b", "#312e81", "#1e1b4b", "#3730a3")),
                new("modern", "Education & training — Bright", "Lighter panels and stronger contrast", new("#4f46e5", "#4338ca", "#a5b4fc", "#334155", "#475569", "#1e1b4b", "#3730a3")),
                new("midnight", "Education & training — Midnight", "Dark navigation with vivid accents", new("#a5b4fc", "#4f46e5", "#4f46e5", "#020617", "#1e1b4b", "#020617", "#312e81")),
                new("signature", "Education & training — Accent", "Accent-led hero and sidebar highlights", new("#a5b4fc", "#4338ca", "#4f46e5", "#1e1b4b", "#312e81", "#1e1b4b", "#3730a3")),
            ],
            []),
        new("cleaning_facilities", "Cleaning & facilities management", "business_services", "fa-paint-brush",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "Cleaning & facilities management — Classic", "Default brand colours for this industry", new("#059669", "#047857", "#6ee7b7", "#064e3b", "#065f46", "#022c22", "#047857")),
                new("modern", "Cleaning & facilities management — Bright", "Lighter panels and stronger contrast", new("#059669", "#047857", "#6ee7b7", "#334155", "#475569", "#022c22", "#047857")),
                new("midnight", "Cleaning & facilities management — Midnight", "Dark navigation with vivid accents", new("#6ee7b7", "#059669", "#059669", "#020617", "#064e3b", "#020617", "#065f46")),
                new("signature", "Cleaning & facilities management — Accent", "Accent-led hero and sidebar highlights", new("#6ee7b7", "#047857", "#059669", "#064e3b", "#065f46", "#022c22", "#047857")),
            ],
            []),
        new("security_services", "Security & safety services", "business_services", "fa-shield",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "Security & safety services — Classic", "Default brand colours for this industry", new("#1e293b", "#0f172a", "#38bdf8", "#020617", "#1e293b", "#020617", "#334155")),
                new("modern", "Security & safety services — Bright", "Lighter panels and stronger contrast", new("#1e293b", "#0f172a", "#38bdf8", "#334155", "#475569", "#020617", "#334155")),
                new("midnight", "Security & safety services — Midnight", "Dark navigation with vivid accents", new("#38bdf8", "#1e293b", "#1e293b", "#020617", "#020617", "#020617", "#1e293b")),
                new("signature", "Security & safety services — Accent", "Accent-led hero and sidebar highlights", new("#38bdf8", "#0f172a", "#1e293b", "#020617", "#1e293b", "#020617", "#334155")),
            ],
            []),
        new("printing_signage", "Printing & signage", "business_services", "fa-print",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Printing & signage — Classic", "Default brand colours for this industry", new("#ea580c", "#c2410c", "#fdba74", "#7c2d12", "#9a3412", "#431407", "#c2410c")),
                new("modern", "Printing & signage — Bright", "Lighter panels and stronger contrast", new("#ea580c", "#c2410c", "#fdba74", "#334155", "#475569", "#431407", "#c2410c")),
                new("midnight", "Printing & signage — Midnight", "Dark navigation with vivid accents", new("#fdba74", "#ea580c", "#ea580c", "#020617", "#7c2d12", "#020617", "#9a3412")),
                new("signature", "Printing & signage — Accent", "Accent-led hero and sidebar highlights", new("#fdba74", "#c2410c", "#ea580c", "#7c2d12", "#9a3412", "#431407", "#c2410c")),
            ],
            []),
        new("construction_contracting", "Construction & contracting", "business_services", "fa-building",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Construction & contracting — Classic", "Default brand colours for this industry", new("#b45309", "#92400e", "#fbbf24", "#1c1917", "#44403c", "#0c0a09", "#78350f")),
                new("modern", "Construction & contracting — Bright", "Lighter panels and stronger contrast", new("#b45309", "#92400e", "#fbbf24", "#334155", "#475569", "#0c0a09", "#78350f")),
                new("midnight", "Construction & contracting — Midnight", "Dark navigation with vivid accents", new("#fbbf24", "#b45309", "#b45309", "#020617", "#1c1917", "#020617", "#44403c")),
                new("signature", "Construction & contracting — Accent", "Accent-led hero and sidebar highlights", new("#fbbf24", "#92400e", "#b45309", "#1c1917", "#44403c", "#0c0a09", "#78350f")),
            ],
            []),
        new("logistics_freight", "Logistics & freight", "business_services", "fa-ship",
            ["core", "commerce", "logistics", "erp", "professional"],
            [
                new("classic", "Logistics & freight — Classic", "Default brand colours for this industry", new("#0369a1", "#075985", "#38bdf8", "#0c4a6e", "#075985", "#082f49", "#0284c7")),
                new("modern", "Logistics & freight — Bright", "Lighter panels and stronger contrast", new("#0369a1", "#075985", "#38bdf8", "#334155", "#475569", "#082f49", "#0284c7")),
                new("midnight", "Logistics & freight — Midnight", "Dark navigation with vivid accents", new("#38bdf8", "#0369a1", "#0369a1", "#020617", "#0c4a6e", "#020617", "#075985")),
                new("signature", "Logistics & freight — Accent", "Accent-led hero and sidebar highlights", new("#38bdf8", "#075985", "#0369a1", "#0c4a6e", "#075985", "#082f49", "#0284c7")),
            ],
            []),
        new("financial_services", "Financial services", "business_services", "fa-line-chart",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "Financial services — Classic", "Default brand colours for this industry", new("#047857", "#065f46", "#34d399", "#022c22", "#064e3b", "#022c22", "#047857")),
                new("modern", "Financial services — Bright", "Lighter panels and stronger contrast", new("#047857", "#065f46", "#34d399", "#334155", "#475569", "#022c22", "#047857")),
                new("midnight", "Financial services — Midnight", "Dark navigation with vivid accents", new("#34d399", "#047857", "#047857", "#020617", "#022c22", "#020617", "#064e3b")),
                new("signature", "Financial services — Accent", "Accent-led hero and sidebar highlights", new("#34d399", "#065f46", "#047857", "#022c22", "#064e3b", "#022c22", "#047857")),
            ],
            []),
        new("erp_standalone", "ERP standalone (no storefront)", "business_services", "fa-university",
            ["core", "erp", "professional", "logistics"],
            [
                new("classic", "ERP standalone (no storefront) — Classic", "Default brand colours for this industry", new("#0369a1", "#075985", "#0ea5e9", "#0c4a6e", "#075985", "#082f49", "#0c4a6e")),
                new("modern", "ERP standalone (no storefront) — Bright", "Lighter panels and stronger contrast", new("#0369a1", "#075985", "#0ea5e9", "#334155", "#475569", "#082f49", "#0c4a6e")),
                new("midnight", "ERP standalone (no storefront) — Midnight", "Dark navigation with vivid accents", new("#0ea5e9", "#0369a1", "#0369a1", "#020617", "#0c4a6e", "#020617", "#075985")),
                new("signature", "ERP standalone (no storefront) — Accent", "Accent-led hero and sidebar highlights", new("#0ea5e9", "#075985", "#0369a1", "#0c4a6e", "#075985", "#082f49", "#0c4a6e")),
            ],
            []),
        new("nonprofit_government", "Non-profit & government", "business_services", "fa-university",
            ["core", "professional", "erp"],
            [
                new("classic", "Non-profit & government — Classic", "Default brand colours for this industry", new("#334155", "#1e293b", "#94a3b8", "#020617", "#0f172a", "#020617", "#1e293b")),
                new("modern", "Non-profit & government — Bright", "Lighter panels and stronger contrast", new("#334155", "#1e293b", "#94a3b8", "#334155", "#475569", "#020617", "#1e293b")),
                new("midnight", "Non-profit & government — Midnight", "Dark navigation with vivid accents", new("#94a3b8", "#334155", "#334155", "#020617", "#020617", "#020617", "#0f172a")),
                new("signature", "Non-profit & government — Accent", "Accent-led hero and sidebar highlights", new("#94a3b8", "#1e293b", "#334155", "#020617", "#0f172a", "#020617", "#1e293b")),
            ],
            []),
        new("rental", "Rental & leasing", "asset_sharing", "fa-key",
            ["core", "commerce", "catalogue"],
            [
                new("classic", "Fleet gold", "Equipment rental — amber fleet identity", new("#ca8a04", "#a16207", "#facc15", "#422006", "#713f12", "#422006", "#854d0e")),
                new("modern", "Sandstone lease", "Warm stone neutrals for property & assets", new("#eab308", "#ca8a04", "#fde047", "#44403c", "#57534e", "#292524", "#a16207")),
                new("midnight", "Charcoal fleet", "Dark logistics with gold status chips", new("#facc15", "#ca8a04", "#fb923c", "#1c1917", "#422006", "#0c0a09", "#713f12")),
                new("signature", "Sunset hire", "Orange sunset gradient heroes for bookings", new("#f97316", "#ea580c", "#facc15", "#7c2d12", "#9a3412", "#431407", "#c2410c")),
            ],
            []),
        new("vehicle_leasing", "Vehicle leasing & rental", "asset_sharing", "fa-car",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Vehicle leasing & rental — Classic", "Default brand colours for this industry", new("#ca8a04", "#a16207", "#fde047", "#422006", "#713f12", "#1c1917", "#a16207")),
                new("modern", "Vehicle leasing & rental — Bright", "Lighter panels and stronger contrast", new("#ca8a04", "#a16207", "#fde047", "#334155", "#475569", "#1c1917", "#a16207")),
                new("midnight", "Vehicle leasing & rental — Midnight", "Dark navigation with vivid accents", new("#fde047", "#ca8a04", "#ca8a04", "#020617", "#422006", "#020617", "#713f12")),
                new("signature", "Vehicle leasing & rental — Accent", "Accent-led hero and sidebar highlights", new("#fde047", "#a16207", "#ca8a04", "#422006", "#713f12", "#1c1917", "#a16207")),
            ],
            []),
        new("machinery_rental", "Machinery & equipment rental", "asset_sharing", "fa-wrench",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Machinery & equipment rental — Classic", "Default brand colours for this industry", new("#78716c", "#57534e", "#fbbf24", "#1c1917", "#292524", "#0c0a09", "#44403c")),
                new("modern", "Machinery & equipment rental — Bright", "Lighter panels and stronger contrast", new("#78716c", "#57534e", "#fbbf24", "#334155", "#475569", "#0c0a09", "#44403c")),
                new("midnight", "Machinery & equipment rental — Midnight", "Dark navigation with vivid accents", new("#fbbf24", "#78716c", "#78716c", "#020617", "#1c1917", "#020617", "#292524")),
                new("signature", "Machinery & equipment rental — Accent", "Accent-led hero and sidebar highlights", new("#fbbf24", "#57534e", "#78716c", "#1c1917", "#292524", "#0c0a09", "#44403c")),
            ],
            []),
        new("it_services_saas_support", "IT services & SaaS", "digital_technology", "fa-cloud",
            ["core", "professional", "erp", "commerce"],
            [
                new("classic", "IT services & SaaS — Classic", "Default brand colours for this industry", new("#2563eb", "#1d4ed8", "#60a5fa", "#0f172a", "#1e3a8a", "#020617", "#1d4ed8")),
                new("modern", "IT services & SaaS — Bright", "Lighter panels and stronger contrast", new("#2563eb", "#1d4ed8", "#60a5fa", "#334155", "#475569", "#020617", "#1d4ed8")),
                new("midnight", "IT services & SaaS — Midnight", "Dark navigation with vivid accents", new("#60a5fa", "#2563eb", "#2563eb", "#020617", "#0f172a", "#020617", "#1e3a8a")),
                new("signature", "IT services & SaaS — Accent", "Accent-led hero and sidebar highlights", new("#60a5fa", "#1d4ed8", "#2563eb", "#0f172a", "#1e3a8a", "#020617", "#1d4ed8")),
            ],
            []),
        new("media_entertainment", "Media & entertainment", "digital_technology", "fa-film",
            ["core", "commerce", "marketing", "professional"],
            [
                new("classic", "Media & entertainment — Classic", "Default brand colours for this industry", new("#c026d3", "#a21caf", "#e879f9", "#4a044e", "#701a75", "#3b0764", "#a21caf")),
                new("modern", "Media & entertainment — Bright", "Lighter panels and stronger contrast", new("#c026d3", "#a21caf", "#e879f9", "#334155", "#475569", "#3b0764", "#a21caf")),
                new("midnight", "Media & entertainment — Midnight", "Dark navigation with vivid accents", new("#e879f9", "#c026d3", "#c026d3", "#020617", "#4a044e", "#020617", "#701a75")),
                new("signature", "Media & entertainment — Accent", "Accent-led hero and sidebar highlights", new("#e879f9", "#a21caf", "#c026d3", "#4a044e", "#701a75", "#3b0764", "#a21caf")),
            ],
            []),
        new("energy_utilities", "Energy & utilities", "digital_technology", "fa-bolt",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Energy & utilities — Classic", "Default brand colours for this industry", new("#eab308", "#ca8a04", "#fef08a", "#422006", "#713f12", "#1c1917", "#a16207")),
                new("modern", "Energy & utilities — Bright", "Lighter panels and stronger contrast", new("#eab308", "#ca8a04", "#fef08a", "#334155", "#475569", "#1c1917", "#a16207")),
                new("midnight", "Energy & utilities — Midnight", "Dark navigation with vivid accents", new("#fef08a", "#eab308", "#eab308", "#020617", "#422006", "#020617", "#713f12")),
                new("signature", "Energy & utilities — Accent", "Accent-led hero and sidebar highlights", new("#fef08a", "#ca8a04", "#eab308", "#422006", "#713f12", "#1c1917", "#a16207")),
            ],
            []),
        new("manufacturing_industrial", "Manufacturing & industrial", "digital_technology", "fa-industry",
            ["core", "commerce", "catalogue", "erp", "professional"],
            [
                new("classic", "Manufacturing & industrial — Classic", "Default brand colours for this industry", new("#64748b", "#475569", "#cbd5e1", "#0f172a", "#1e293b", "#020617", "#334155")),
                new("modern", "Manufacturing & industrial — Bright", "Lighter panels and stronger contrast", new("#64748b", "#475569", "#cbd5e1", "#334155", "#475569", "#020617", "#334155")),
                new("midnight", "Manufacturing & industrial — Midnight", "Dark navigation with vivid accents", new("#cbd5e1", "#64748b", "#64748b", "#020617", "#0f172a", "#020617", "#1e293b")),
                new("signature", "Manufacturing & industrial — Accent", "Accent-led hero and sidebar highlights", new("#cbd5e1", "#475569", "#64748b", "#0f172a", "#1e293b", "#020617", "#334155")),
            ],
            []),
        new("platform_host", "Platform host (ecomae)", "platform", "fa-cloud",
            ["core", "professional", "erp", "marketing", "super_platform"],
            [
                new("classic", "Cloud cyan", "ECOM AE operator cyan — platform default", new("#0ea5e9", "#0284c7", "#38bdf8", "#0c4a6e", "#075985", "#082f49", "#0c4a6e")),
                new("modern", "Horizon blue", "Light SaaS blue for marketing & demos", new("#06b6d4", "#0891b2", "#67e8f9", "#164e63", "#0e7490", "#083344", "#155e75")),
                new("midnight", "Operator dark", "Super CP dark shell with cyan glow", new("#22d3ee", "#0ea5e9", "#a5f3fc", "#020617", "#0c4a6e", "#020617", "#075985")),
                new("signature", "Aurora mint", "Mint + cyan aurora for ecomae branding", new("#2dd4bf", "#14b8a6", "#38bdf8", "#042f2e", "#0f766e", "#020617", "#164e63")),
            ],
            []),
    ];

    public static PortalSettingsIndustry? Industry(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Industries.FirstOrDefault(i => string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>PHP <c>epc_portal_settings_industries()</c> — client hosts never see the platform industry.</summary>
    public static IReadOnlyList<PortalSettingsIndustry> IndustriesForSettings(bool isClientHost) =>
        isClientHost
            ? Industries.Where(i => i.Code != "platform_host").ToList()
            : Industries;

    /// <summary>PHP <c>epc_portal_settings_packs()</c> — the Super CP pack is platform-only.</summary>
    public static IReadOnlyList<PortalCpPack> PacksForSettings(bool isClientHost) =>
        isClientHost
            ? Packs.Where(p => p.Code != "super_platform").ToList()
            : Packs;

    /// <summary>PHP <c>epc_portal_industries_grouped()</c> — ecosystem order with its industries.</summary>
    public static IReadOnlyList<(PortalEcosystem Ecosystem, IReadOnlyList<PortalSettingsIndustry> Industries)> Grouped(
        IReadOnlyList<PortalSettingsIndustry> industries)
    {
        ArgumentNullException.ThrowIfNull(industries);
        return Ecosystems
            .Select(eco => (eco, (IReadOnlyList<PortalSettingsIndustry>)industries
                .Where(i => string.Equals(i.Ecosystem, eco.Code, StringComparison.Ordinal))
                .ToList()))
            .Where(pair => pair.Item2.Count > 0)
            .ToList();
    }

    /// <summary>PHP <c>epc_portal_erp_modules_default_ids()</c>.</summary>
    public static IReadOnlyList<string> ErpModuleDefaults(string accessMode) =>
        ErpModules
            .Where(m => accessMode == "erp_only" ? m.DefaultErpOnly : m.DefaultFull)
            .Select(m => m.Id)
            .ToList();

    /// <summary>PHP <c>epc_portal_normalize_theme_template()</c> including the legacy aliases.</summary>
    public static string NormalizeThemeTemplate(string? industryCode, string? templateId)
    {
        var tid = (templateId ?? string.Empty).Trim().ToLowerInvariant();
        tid = tid switch
        {
            "default" or "standard" => "classic",
            "light" => "modern",
            "dark" => "midnight",
            _ => tid,
        };
        var industry = Industry(industryCode);
        if (industry is null)
        {
            return tid.Length == 0 ? "classic" : tid;
        }
        return industry.StyleTemplates.Any(t => t.Id == tid) ? tid : "classic";
    }
}
