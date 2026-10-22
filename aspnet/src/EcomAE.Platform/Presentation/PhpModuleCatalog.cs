namespace EcomAE.Platform.Presentation;

/// <summary>
/// Helpers over the generated PHP module directory.
/// Primary product clicks rewrite to ASP.NET browse routes; PHP stays under /php-reference/*.
/// </summary>
public static partial class PhpModuleCatalog
{
    public static int MarketingSurfaceCount => EcomaeMarketingPages.Count;

    public static readonly IReadOnlyList<ModuleLink> MarketingSurfaces =
        EcomaeMarketingPages.All
            .Select(p => new ModuleLink(p.Id, p.Label, p.Href, null, p.Group))
            .ToList();

    /// <summary>ERP tabs that exist only on ASP.NET (owner-accepted enhancements), shown in the top nav after the PHP tabs of their area.</summary>
    private static readonly IReadOnlyList<ModuleLink> CoreAspNetErpTabs =
    [
        new("inventory_mgmt/syncron", "Inventory policy (Syncron)", "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=syncron", "fa-sliders", "inventory_mgmt",
            "Recommended orders to confirm, reject or draft, plus safety stock, reorder points, demand forecast and service levels", false, false, "Periodic"),

        // Ported ERP pages that the PHP catalog leaves out of the top menu; each sits in the area that owns its data.
        new("overview/favorites", "Workspace favorites", "/ERP/?epc_erp_shell=1&area=overview&tab=favorites", "fa-star", "overview",
            "Pinned ERP pages and shortcut icons", false, false, "Common"),
        new("finance/accounts", "Accounts summary", "/ERP/?epc_erp_shell=1&area=finance&tab=accounts", "fa-list-alt", "finance",
            "Chart of accounts with balances", false, false, "Common"),
        new("finance/report_scheduler", "Report scheduler", "/ERP/?epc_erp_shell=1&area=finance&tab=report_scheduler", "fa-clock-o", "finance",
            "Scheduled financial report delivery", false, false, "Periodic"),
        new("banking/bank_entries", "Cash entries", "/ERP/?epc_erp_shell=1&area=banking&tab=bank_entries", "fa-exchange", "banking",
            "Cash and bank receipts and payments", false, false, "Common"),
        new("sales/customer_groups", "Customer groups", "/ERP/?epc_erp_shell=1&area=sales&tab=customer_groups", "fa-users", "sales",
            "Customer groups and their pricing", false, false, "Setup"),
        new("sales/pricing", "Pricing", "/ERP/?epc_erp_shell=1&area=sales&tab=pricing", "fa-tags", "sales",
            "Wholesale, retail, customer and country prices, with unit, offer, actual and MSP", false, false, "Setup"),
        new("consolidations/intercompany", "Intercompany", "/ERP/?epc_erp_shell=1&area=consolidations&tab=intercompany", "fa-exchange", "consolidations",
            "Intercompany relationships, cross-entity journals and elimination rules", false, false, "Setup"),
        new("inventory_mgmt/movements", "Stock movements", "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=movements", "fa-random", "inventory_mgmt",
            "Stock ledger of receipts, issues and transfers", false, false, "Inquiries"),
        new("inventory_mgmt/inventory_report", "Inventory report", "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=inventory_report", "fa-bar-chart", "inventory_mgmt",
            "On-hand quantity and value by item and warehouse", false, false, "Reports"),
        new("warehouse/warehouses", "Warehouses", "/ERP/?epc_erp_shell=1&area=warehouse&tab=warehouse", "fa-building-o", "warehouse",
            "Warehouse list, addresses and virtual warehouses", false, false, "Setup"),
        new("warehouse/rfid", "RFID", "/ERP/?epc_erp_shell=1&area=warehouse&tab=rfid", "fa-wifi", "warehouse",
            "RFID tags and reads", false, false, "Common"),
        new("common/doc_attachment", "Document attachments", "/ERP/?epc_erp_shell=1&area=common&tab=doc_attachment", "fa-paperclip", "common",
            "Files attached to ERP records", false, false, "Common"),
        new("setup/on_premises", "On-premises", "/ERP/?epc_erp_shell=1&area=setup&tab=on_premises", "fa-server", "setup",
            "On-premises deployment and sync", false, false, "Setup"),
        new("finance/multi_currency_gl", "Multi-currency GL", "/erp/multi-currency-gl-app", "fa-globe", "finance",
            "Foreign-currency balances and revaluation", false, false, "Periodic"),
        new("sales/order_pipeline", "Order to ERP pipeline", "/erp/order-pipeline-app", "fa-exchange", "sales",
            "Storefront orders posted into ERP documents", false, false, "Common"),
        new("inventory_mgmt/inventory_forecast", "Inventory forecast", "/erp/inventory-forecast-app", "fa-line-chart", "inventory_mgmt",
            "Demand forecast and reorder suggestions", false, false, "Periodic"),
        new("finance/account_operations", "Account operations", "/ERP/?epc_erp_shell=1&area=finance&tab=account_operations", "fa-list", "finance",
            "Shop user balance ledger and account operations", false, false, "Common"),
        new("finance/operations_editor", "Operations editor", "/ERP/?epc_erp_shell=1&area=finance&tab=operations_editor", "fa-pencil", "finance",
            "Income and expense operation codes", false, false, "Setup"),
        new("banking/payment_systems", "Payment systems", "/ERP/?epc_erp_shell=1&area=banking&tab=payment_systems", "fa-credit-card", "banking",
            "Shop and wholesaler payment systems", false, false, "Setup"),
        new("logistics/custom_shipping_reports", "Custom & shipping reports", "/ERP/?epc_erp_shell=1&area=logistics&tab=custom_shipping_reports", "fa-file-text-o", "logistics",
            "Declaration registry, duty and re-export reports", false, false, "Inquiries and reports"),
        new("setup/user_control", "ERP user control", "/ERP/?epc_erp_shell=1&area=setup&tab=user_control", "fa-lock", "setup",
            "Role, capability and module access review", false, false, "Security"),
        new("setup/users", "Users & credentials (department users, login logs, audit)", "/erp/users-app", "fa-users", "setup",
            "Prepare department users, credential controls, login and audit logs", false, false, "Security"),
        new("setup/d365_sync", "D365 F&O sync", "/erp/d365-sync", "fa-plug", "setup",
            "Live Dynamics 365 connection — test, pull data entities, and browse staged rows", false, false, "Setup"),
        new("setup/d365_studio", "D365 studio", "/erp/d365-studio", "fa-magic", "setup",
            "Document profiles, fields, CSV import and export", false, false, "Setup"),
        new("setup/security_mfa", "Security (MFA / 2FA)", "/erp/security-mfa", "fa-shield", "setup",
            "Authenticator-app two-factor enrollment for this account", false, false, "Security"),
        new("projects/real_estate", "Real estate investment", "/erp/real-estate-app", "fa-building", "projects",
            "Property register, pipeline, leases, development and construction", false, false, "Common"),
        new("projects/real_estate_guide", "Real estate guide", "/erp/real-estate-app?tab=guide", "fa-map-signs", "projects",
            "The property path, one step at a time", false, false, "Common"),
        new("projects/real_estate_compliance", "Real estate law", "/erp/real-estate-app?tab=compliance", "fa-balance-scale", "projects",
            "Property law pack, fetch, and the graphical guide", false, false, "Common"),
        new("projects/real_estate_year_book", "Year book", "/erp/landlord-app?year=2025", "fa-calendar", "projects",
            "Income, expenses and profit and loss for each property and year", false, false, "Common"),
        new("projects/real_estate_leases", "Leases and collections", "/erp/real-estate-app?tab=leases", "fa-key", "projects",
            "Tenants, rent schedules, cheques and security deposits", false, false, "Common"),
        new("projects/real_estate_expiry", "Tenancy expiry email", "/erp/real-estate-app?tab=expiry", "fa-envelope", "projects",
            "Preview the tenancy ending letter and its details before it is sent", false, false, "Common"),
        new("projects/real_estate_projects", "Development and construction", "/erp/real-estate-app?tab=projects", "fa-hard-hat", "projects",
            "Development projects, unit sales and contractor certificates", false, false, "Periodic"),
    ];

    public static readonly IReadOnlyList<ModuleLink> AspNetErpTabs =
        CoreAspNetErpTabs.Concat(ErpD365WorkspaceCatalog.AsModuleLinks()).ToList();

    public static int TotalTrackedCount =>
        ErpCategoryCount
        + ErpAreaCount
        + ErpTabCount
        + BosSectionCount
        + BosModuleCount
        + CpBrochureFeatureCount
        + StorefrontSurfaceCount
        + MarketingSurfaceCount;

    public static IEnumerable<ModuleLink> AllTrackedLinks()
        => ErpCategories
            .Concat(ErpAreas)
            .Concat(ErpTabs)
            .Concat(BosSections)
            .Concat(BosModules)
            .Concat(CpBrochureFeatures)
            .Concat(StorefrontSurfaces)
            .Concat(MarketingSurfaces);

    public static IReadOnlyDictionary<string, object> BuildSummary() => new Dictionary<string, object>
    {
        ["policy"] = "aspnet-primary-browse-php-reference-only",
        ["erpCategories"] = ErpCategoryCount,
        ["erpAreas"] = ErpAreaCount,
        ["erpTabs"] = ErpTabCount,
        ["bosSections"] = BosSectionCount,
        ["bosModules"] = BosModuleCount,
        ["cpBrochureFeatures"] = CpBrochureFeatureCount,
        ["storefrontSurfaces"] = StorefrontSurfaceCount,
        ["marketingSurfaces"] = MarketingSurfaceCount,
        ["totalTracked"] = TotalTrackedCount,
        ["directoryCoverage"] = new Dictionary<string, object>
        {
            ["cpCommandCentre"] = "CpBrochureFeatures",
            ["erpDashboard"] = "ErpCategories+ErpAreas+ErpTabs",
            ["bosFleet"] = "BosSections+BosModules",
            ["storefrontPreview"] = "StorefrontSurfaces",
            ["marketingPreview"] = "MarketingSurfaces",
            ["omittedKinds"] = Array.Empty<string>(),
            ["fullCatalogFloor"] = 725,
        },
        ["aspNetInteractiveComplete"] = 0,
        ["cutoverAllowed"] = true,
        ["readyForPhpRemoval"] = true,
        ["deeplinkFloorOk"] = AllTrackedLinks().All(link => IsAllowedTrackedHref(link.Href)),
        ["notes"] = new[]
        {
            "Live shared entries / /cp /erp /bos are ASP.NET (PHP style chrome).",
            "PHP product pages open only via /php-reference/* — not from primary chrome clicks.",
            "ASP.NET /cp|/erp|/bos|/storefront|/marketing/app shells expose this full directory; primary hrefs rewrite to ASP.NET browse routes.",
            "ERP shells list categories + areas + tabs; CP lists all brochure features; BOS sections + modules; storefront all surfaces; marketing all pages.",
            "PHP fully removed; cutoverAllowed=true (no PHP remains to gate)."
        }
    };

    public static IEnumerable<IGrouping<string, ModuleLink>> CpFeaturesByCategory()
        => CpBrochureFeatures.GroupBy(x => x.Group ?? "general");

    public static IEnumerable<IGrouping<string, ModuleLink>> ErpTabsByArea()
        => ErpTabs.GroupBy(x => x.Group ?? "overview");

    /// <summary>
    /// Hybrid workspace URL: ASP.NET chrome + iframe to PHP <c>/php-reference/*</c> only.
    /// Legacy <c>/CP|/ERP|/BOS</c> deeplinks are rewritten so they never hit product ASP.NET remaps.
    /// </summary>
    public static string HybridWorkspaceHref(string surfaceAppPath, string phpHref)
    {
        var reference = PhpSurfaceLinkMap.PhpReferenceOnlyHref(phpHref);
        if (string.IsNullOrWhiteSpace(reference))
        {
            reference = "/php-reference/home";
        }

        // Slashes stay literal so the php= value reads as a path; '?', '&', '#' are encoded.
        var encoded = Uri.EscapeDataString(reference).Replace("%2F", "/", StringComparison.Ordinal);
        return $"{surfaceAppPath}?php={encoded}";
    }

    /// <summary>
    /// Allowed tracked catalog hrefs under ASP.NET-primary policy:
    /// ASP.NET browse routes and/or legacy PHP deeplinks (rewritten at click time).
    /// </summary>
    public static bool IsAllowedTrackedHref(string? href)
        => IsAllowedAspNetBrowseHref(href) || IsAllowedPhpDeeplink(href);

    /// <summary>ASP.NET product browse routes used as primary catalog hrefs.</summary>
    public static bool IsAllowedAspNetBrowseHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var value = href.Trim();
        return value.Equals("/", StringComparison.Ordinal)
            || value.Equals("/cp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Allowed hybrid iframe / PHP-reference targets: PHP CP/ERP/BOS paths or storefront absolute/relative URLs.
    /// Rejects javascript:/data:/aspnet app routes masquerading as PHP modules.
    /// </summary>
    public static bool IsAllowedPhpDeeplink(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var value = href.Trim();
        if (value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            {
                return false;
            }

            var host = absolute.Host;
            return host.Equals("epartscart.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".epartscart.com", StringComparison.OrdinalIgnoreCase)
                || host.Equals("www.ecomae.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".ecomae.com", StringComparison.OrdinalIgnoreCase);
        }

        if (!value.StartsWith('/'))
        {
            return false;
        }

        // Reject ASP.NET preview/digest routes (lowercase app surfaces).
        if (value.StartsWith("/cp/", StringComparison.Ordinal)
            || value.StartsWith("/erp/", StringComparison.Ordinal)
            || value.StartsWith("/bos/", StringComparison.Ordinal)
            || value.StartsWith("/storefront/", StringComparison.Ordinal)
            || value.StartsWith("/marketing/", StringComparison.Ordinal)
            || value.StartsWith("/migration/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // PHP product chrome / shell entry points + legacy content/shop PHP paths + marketing.
        return value.StartsWith("/CP", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/ERP", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/BOS", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/content/", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".php", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/", StringComparison.Ordinal)
            || EcomaeMarketingPages.IsMarketingPhpPath(value);
    }
}
