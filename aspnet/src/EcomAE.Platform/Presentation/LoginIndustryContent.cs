namespace EcomAE.Platform.Presentation;

/// <summary>
/// Industry-scoped login hero copy for tenant hosts.
/// Super-CP/ERP keep platform fleet messaging; tenants never see fleet-wide Super ERP claims.
/// </summary>
public static class LoginIndustryContent
{
    public sealed record Cap(string Icon, string Title, string Detail);

    public sealed record Stat(int Value, string Label);

    public sealed record Content(
        string IndustryCode,
        string HeroTitle,
        string HeroTagline,
        string FormTitle,
        string FormLead,
        string SubmitLabel,
        string SecureBadge,
        IReadOnlyList<Cap> Capabilities,
        IReadOnlyList<Stat> Stats,
        IReadOnlyList<string> PanelFeatures);

    public static Content For(LoginHostBrand.Brand brand, string surface)
    {
        var surfaceKey = (surface ?? "cp").Trim().ToLowerInvariant();
        if (brand.LogoKind == LoginHostBrand.Kind.Platform)
        {
            return Platform(surfaceKey);
        }

        return brand.SiteKey.ToLowerInvariant() switch
        {
            "epartscart" => AutoParts(brand, surfaceKey),
            "electronicae" => Electronics(brand, surfaceKey),
            "stylenlook" => Fashion(brand, surfaceKey),
            "thejewellerytrend" => Jewellery(brand, surfaceKey),
            "taxofinca" => FinanceTax(brand, surfaceKey),
            _ => TenantFallback(brand, surfaceKey),
        };
    }

    private static Content AutoParts(LoginHostBrand.Brand brand, string surface) => surface == "erp"
        ? new(
            "auto_parts",
            brand.Label,
            "Parts commerce ERP · stock, VIN & workshop ops",
            "Sign in to Parts ERP",
            "Warehouse, purchasing, and workshop finance for your auto-parts business",
            "Enter Parts ERP",
            "Tenant-isolated · UAE VAT ready · Parts catalogue linked",
            [
                new("fa-cogs", "Parts catalogue ERP", "SKU, brand, OEM & cross-references"),
                new("fa-truck", "Warehouse & transfers", "Bins, receipts, pick & pack"),
                new("fa-wrench", "Workshop & garage", "Job cards, labour & fittings"),
                new("fa-line-chart", "Purchasing & margin", "Supplier POs, landed cost, GP"),
            ],
            [
                new(12, "Warehouses"),
                new(48, "Brand lines"),
                new(95, "ERP modules"),
                new(24, "Supplier feeds"),
            ],
            [
                "Parts inventory with bin & batch control",
                "Supplier purchase orders & goods receipt",
                "Sales invoices linked to storefront orders",
                "VIN / vehicle garage linkage",
                "UAE VAT & e-invoicing ready",
            ])
        : new(
            "auto_parts",
            brand.Label,
            "Control panel · automotive spare parts",
            "Sign in to Control Panel",
            "Manage catalogue, storefront, and orders for your parts business",
            "Enter Control Panel",
            "Tenant-isolated · Catalogue synced · Storefront ready",
            [
                new("fa-car", "Vehicle & VIN search", "Garage, modifications & fitment"),
                new("fa-shopping-cart", "Parts storefront", "Cart, checkout & customer accounts"),
                new("fa-tags", "Pricing & offers", "Brand rules, promotions & AI price"),
                new("fa-cubes", "Catalogue ops", "Articles, analogs, engines & brands"),
            ],
            [
                new(50, "k+ SKUs"),
                new(120, "Brands"),
                new(15, "Markets"),
                new(7, "Channels"),
            ],
            [
                "Catalogue & brand management",
                "Storefront merchandising & CMS",
                "Orders, returns & customer garage",
                "Delivery methods & payment gateways",
                "Integrations & marketplace channels",
            ]);

    private static Content Electronics(LoginHostBrand.Brand brand, string surface) => surface == "erp"
        ? new(
            "electronics",
            brand.Label,
            "Electronics ERP · serials, RMA & retail ops",
            "Sign in to Electronics ERP",
            "Inventory, serial tracking, and retail finance for tech & gaming",
            "Enter Electronics ERP",
            "Serial-tracked · RMA ready · Multi-channel stock",
            [
                new("fa-microchip", "Serial & IMEI stock", "Track units from PO to sale"),
                new("fa-gamepad", "Gaming & accessories", "Bundles, warranties & kits"),
                new("fa-refresh", "RMA & returns", "Repair tickets & credit notes"),
                new("fa-shopping-bag", "Omni retail", "Walk-in, web & marketplace"),
            ],
            [
                new(8, "Stores"),
                new(36, "Categories"),
                new(64, "ERP modules"),
                new(12, "Channels"),
            ],
            [
                "Serialised inventory & warranties",
                "Purchase & vendor returns",
                "POS / retail cash-up",
                "Marketplace channel sync",
                "RMA & service desk",
            ])
        : new(
            "electronics",
            brand.Label,
            "Control panel · tech & gaming retail",
            "Sign in to Control Panel",
            "Run your electronics storefront, catalogue, and promotions",
            "Enter Control Panel",
            "Tenant-isolated · Channel ready · Promo engine",
            [
                new("fa-laptop", "Product catalogue", "Specs, variants & bundles"),
                new("fa-bolt", "Flash deals", "Timed offers & gaming drops"),
                new("fa-picture-o", "Content & banners", "Landing pages & launches"),
                new("fa-users", "Customers & loyalty", "Accounts, wishlists & CRM"),
            ],
            [
                new(18, "Collections"),
                new(40, "Brands"),
                new(10, "Channels"),
                new(6, "Stores"),
            ],
            [
                "Catalogue & variant management",
                "Promotions & launch campaigns",
                "Orders & fulfilment routing",
                "Payment & delivery setup",
                "Marketplace integrations",
            ]);

    private static Content Fashion(LoginHostBrand.Brand brand, string surface) => surface == "erp"
        ? new(
            "fashion",
            brand.Label,
            "Fashion ERP · size grids, seasons & retail",
            "Sign in to Fashion ERP",
            "Seasonal buying, size/colour stock, and boutique finance",
            "Enter Fashion ERP",
            "Size-grid stock · Season plans · Boutique ready",
            [
                new("fa-female", "Style & size matrix", "Colourways, sizes & replenishment"),
                new("fa-calendar", "Season planning", "Buy plans, drops & markdowns"),
                new("fa-building", "Boutique stores", "Transfer, count & cash-up"),
                new("fa-credit-card", "Retail finance", "AP, AR & daily settlement"),
            ],
            [
                new(6, "Seasons"),
                new(24, "Collections"),
                new(52, "ERP modules"),
                new(9, "Boutiques"),
            ],
            [
                "Size / colour inventory matrix",
                "Purchase & allocation to stores",
                "Markdown & promotion journals",
                "POS settlement & bank deposit",
                "Returns & exchange workflow",
            ])
        : new(
            "fashion",
            brand.Label,
            "Control panel · fashion & beauty",
            "Sign in to Control Panel",
            "Merchandising, looks, and storefront for your fashion brand",
            "Enter Control Panel",
            "Tenant-isolated · Lookbooks · Beauty & apparel",
            [
                new("fa-heart", "Looks & collections", "Campaigns, lookbooks & drops"),
                new("fa-shopping-bag", "Fashion storefront", "Fit guides, wishlists & checkout"),
                new("fa-magic", "Beauty & care", "Kits, shade finders & routines"),
                new("fa-star", "Loyalty & CRM", "Members, points & VIP tiers"),
            ],
            [
                new(32, "Looks"),
                new(18, "Brands"),
                new(8, "Channels"),
                new(5, "Boutiques"),
            ],
            [
                "Collection & lookbook CMS",
                "Variant merchandising",
                "Orders, exchanges & gifts",
                "Beauty routines & kits",
                "Loyalty & CRM tools",
            ]);

    private static Content Jewellery(LoginHostBrand.Brand brand, string surface) => surface == "erp"
        ? new(
            "jewellery",
            brand.Label,
            "Jewellery ERP · gold rate, tags & hallmark",
            "Sign in to Jewellery ERP",
            "Gold rate, tag printing, and luxury inventory for your showroom",
            "Enter Jewellery ERP",
            "Gold-rate linked · Tag ready · Hallmark compliant",
            [
                new("fa-diamond", "Gold & metal rates", "Live rate boards & valuation"),
                new("fa-barcode", "Jewellery tags", "RFID / barcode tag print"),
                new("fa-university", "Showroom stock", "Trays, safes & transfers"),
                new("fa-balance-scale", "Making & purity", "Hallmark, wastage & labour"),
            ],
            [
                new(4, "Metals"),
                new(16, "Trays"),
                new(40, "ERP modules"),
                new(3, "Showrooms"),
            ],
            [
                "Gold rate & valuation journals",
                "Tag / barcode jewellery stock",
                "Making charges & purity control",
                "Showroom sales & approvals",
                "Repair & custom order tracking",
            ])
        : new(
            "jewellery",
            brand.Label,
            "Control panel · jewellery & luxury",
            "Sign in to Control Panel",
            "Curate collections, appointments, and luxury storefront",
            "Enter Control Panel",
            "Tenant-isolated · Luxury catalogue · Appointments",
            [
                new("fa-diamond", "Luxury catalogue", "Collections, metals & stones"),
                new("fa-calendar-check-o", "Appointments", "Private viewing & try-ons"),
                new("fa-gift", "Bridal & gifting", "Sets, packages & engraving"),
                new("fa-camera", "Look & media", "360° media & storytelling"),
            ],
            [
                new(20, "Collections"),
                new(8, "Metals"),
                new(5, "Showrooms"),
                new(4, "Channels"),
            ],
            [
                "Luxury catalogue & media",
                "Appointment booking",
                "Bridal & gift packages",
                "Storefront & VIP lists",
                "Payment & delivery options",
            ]);

    private static Content FinanceTax(LoginHostBrand.Brand brand, string surface) => surface == "erp"
        ? new(
            "finance",
            brand.Label,
            "Tax & accounting ERP · ledgers, filings & clients",
            "Sign in to Tax ERP",
            "Client ledgers, VAT filings, and practice finance workspace",
            "Enter Tax ERP",
            "Practice-isolated · Filing ready · Client ledgers",
            [
                new("fa-book", "Client ledgers", "COA, journals & period close"),
                new("fa-percent", "VAT & filings", "Returns, e-invoicing & audits"),
                new("fa-briefcase", "Practice ops", "Engagements, retainers & WIP"),
                new("fa-bank", "Cash & bank", "Reconcile, payroll & payouts"),
            ],
            [
                new(120, "Clients"),
                new(28, "Return types"),
                new(70, "ERP modules"),
                new(12, "Entities"),
            ],
            [
                "Multi-client chart of accounts",
                "VAT / corporate tax filings",
                "Bank reconciliation & payroll",
                "Engagement WIP & billing",
                "Document vault & approvals",
            ])
        : new(
            "finance",
            brand.Label,
            "Control panel · tax & accounting practice",
            "Sign in to Control Panel",
            "Client portal settings, documents, and practice automation",
            "Enter Control Panel",
            "Tenant-isolated · Client portal · Compliance tools",
            [
                new("fa-id-card", "Client portal", "Secure uploads & status"),
                new("fa-file-text", "Document packs", "KYC, filings & archives"),
                new("fa-bell", "Deadline alerts", "Filing calendars & reminders"),
                new("fa-cogs", "Practice automation", "Tasks, SLAs & templates"),
            ],
            [
                new(120, "Clients"),
                new(36, "Templates"),
                new(18, "Workflows"),
                new(8, "Teams"),
            ],
            [
                "Client portal configuration",
                "Document & e-sign packs",
                "Filing calendars",
                "Task & SLA automation",
                "Team roles & access",
            ]);

    private static Content TenantFallback(LoginHostBrand.Brand brand, string surface) => surface == "erp"
        ? new(
            "general",
            brand.Label,
            $"Company ERP · {brand.Tagline}",
            "Sign in to ERP",
            $"Access {brand.Label} finance and operations",
            "Enter ERP",
            "Tenant-isolated · Company workspace · Audit logged",
            [
                new("fa-university", "Company books", "GL, AP, AR & cash"),
                new("fa-cubes", "Inventory", "Stock, warehouses & transfers"),
                new("fa-shopping-cart", "Sales & purchase", "Orders, invoices & POs"),
                new("fa-users", "Workforce", "HR, payroll & attendance"),
            ],
            [
                new(1, "Company"),
                new(4, "Departments"),
                new(40, "ERP modules"),
                new(3, "Warehouses"),
            ],
            [
                "General ledger & reporting",
                "Accounts payable & receivable",
                "Inventory & purchasing",
                "Sales orders & invoicing",
                "HR & payroll basics",
            ])
        : new(
            "general",
            brand.Label,
            $"Control panel · {brand.Tagline}",
            "Sign in to Control Panel",
            $"Manage {brand.Label} storefront and operations",
            "Enter Control Panel",
            "Tenant-isolated · Role-scoped · Audit logged",
            [
                new("fa-shopping-cart", "Storefront", "Catalogue, cart & checkout"),
                new("fa-tags", "Merchandising", "Offers, CMS & banners"),
                new("fa-truck", "Fulfilment", "Orders, delivery & returns"),
                new("fa-cogs", "Settings", "Users, payments & integrations"),
            ],
            [
                new(1, "Store"),
                new(8, "Modules"),
                new(4, "Channels"),
                new(2, "Teams"),
            ],
            [
                "Catalogue & CMS",
                "Orders & customers",
                "Payments & delivery",
                "Users & permissions",
                "Integrations",
            ]);

    private static Content Platform(string surface) => surface == "erp"
        ? new(
            "platform",
            "ERP",
            "Enterprise Resource Planning",
            "ERP Access",
            "Access your enterprise resource planning system",
            "Access ERP System",
            "Database isolated · Country-compliant · Enterprise grade",
            [
                new("fa-university", "Enterprise ERP", "Finance, HR, Inventory, Production"),
                new("fa-shopping-cart", "Sales & purchase", "Orders, invoices, quotations"),
                new("fa-globe", "Worldwide Compliance", "Auto-localized per country"),
                new("fa-building", "Multi-company", "Industry packs per legal entity"),
            ],
            [
                new(225, "Tenants"),
                new(11, "Industries"),
                new(PhpModuleCatalog.ErpAreaCount, "ERP Areas"),
                new(PhpModuleCatalog.ErpTabCount, "Tabs"),
            ],
            [
                "General Ledger & Financial Reporting",
                "Accounts Payable & Receivable",
                "HR, Payroll & Workforce Management",
                "Inventory, Production & Warehouse",
                "Tax Compliance & E-Invoicing",
            ])
        : new(
            "platform",
            "CP",
            "Control Panel · Operator Console",
            "Operator Access",
            "Sign in to the control panel for this platform",
            "Sign In to Control Panel",
            "256-bit encrypted · Session isolated · Audit logged",
            [
                new("fa-globe", "Fleet command", "Commerce, ERP-only & demo tenants"),
                new("fa-rocket", "Tenant onboarding", "Templates, packs & provisioning"),
                new("fa-shield", "Platform governance", "Health, failover & isolation"),
                new("fa-th-large", $"{PhpModuleCatalog.CpBrochureFeatureCount} CP features", "Operator modules catalogue"),
            ],
            [
                new(225, "Tenants"),
                new(11, "Industries"),
                new(95, "ERP Modules"),
                new(15, "Countries"),
            ],
            [
                "Role-scoped sessions · audit-ready",
                "Tenant CP switch without mixing DBs",
                "Jump to Super BOS from the console",
                "Provisioning & health tools",
                "Platform integrations",
            ]);
}
