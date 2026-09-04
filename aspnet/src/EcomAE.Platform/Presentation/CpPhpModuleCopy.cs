namespace EcomAE.Platform.Presentation;

/// <summary>PHP-module purpose copy for CP digest apps (replaces generic hero text).</summary>
public static class CpPhpModuleCopy
{
    private const string GenericLong = "Manage store operations, catalogue, and partner integrations from one control panel.";
    private const string GenericShort = "Manage store operations from the control panel.";

    public static bool IsGeneric(string? text)
        => string.Equals(text?.Trim(), GenericLong, StringComparison.Ordinal)
           || string.Equals(text?.Trim(), GenericShort, StringComparison.Ordinal);

    public static string PurposeFor(string? uriOrPath)
    {
        var path = PathOnly(uriOrPath);
        if (path.Contains("/hr-overview", StringComparison.OrdinalIgnoreCase))
            return "Basic + allowances = fixed monthly salary for a 30-day month. Set days worked before generating payroll. End-of-service and statutory leave follow the company country (UAE default).";
        if (path.Contains("/po-approvals", StringComparison.OrdinalIgnoreCase))
            return "Purchase-order approval queue. Approve or reject pending POs — writes stay on the ASP.NET approval endpoints.";
        if (path.Contains("/credit-limits", StringComparison.OrdinalIgnoreCase))
            return "Customer credit limits and exposure. Set a limit per account; orders over the cap stay blocked until credit is raised.";
        if (path.Contains("/landed-cost", StringComparison.OrdinalIgnoreCase))
            return "Landed-cost worksheets: freight, duty and charges rolled into inventory unit cost.";
        if (path.Contains("/jewellery", StringComparison.OrdinalIgnoreCase))
            return "Jewellery masters, fixing, repairs and stock verification — karat, gold rate, barcode and workshop.";
        if (path.Contains("/crm-", StringComparison.OrdinalIgnoreCase) || path.Contains("/tickets", StringComparison.OrdinalIgnoreCase))
            return "CRM tickets, opportunities and SLA. Raise a ticket, assign an owner, and track first-response time.";
        if (path.Contains("/warehouse-wms", StringComparison.OrdinalIgnoreCase))
            return "Warehouse WMS: locations, pick/pack queues and virtual warehouse transfers.";
        if (path.Contains("/fulfillment", StringComparison.OrdinalIgnoreCase))
            return "Fulfilment queue: pick, pack, carrier label and delivery confirmation.";
        if (path.Contains("/returns-rma", StringComparison.OrdinalIgnoreCase))
            return "Returns and RMA: receive, inspect, refund or restock.";
        if (path.Contains("/einvoice", StringComparison.OrdinalIgnoreCase))
            return "E-invoice documents: seller/buyer ASP setup, submit and credit notes.";
        if (path.Contains("/uae-tax", StringComparison.OrdinalIgnoreCase) || path.Contains("/tax-toolkit", StringComparison.OrdinalIgnoreCase))
            return "UAE VAT / corporate-tax compliance packs and tourist refund.";
        if (path.Contains("/aml-compliance", StringComparison.OrdinalIgnoreCase))
            return "AML screening and compliance cases.";
        if (path.Contains("/soc2", StringComparison.OrdinalIgnoreCase))
            return "SOC 2 control evidence and review status.";
        if (path.Contains("/insurance", StringComparison.OrdinalIgnoreCase))
            return "Insurance policies, claims and compliance documents.";
        if (path.Contains("/doc-expiry", StringComparison.OrdinalIgnoreCase) || path.Contains("/document-control", StringComparison.OrdinalIgnoreCase))
            return "Document control and expiry reminders (trade licence, visas, certificates).";
        if (path.Contains("/abandoned-carts", StringComparison.OrdinalIgnoreCase))
            return "Abandoned carts: recover checkout sessions that never completed.";
        if (path.Contains("/promotions", StringComparison.OrdinalIgnoreCase))
            return "Promotions and discount campaigns.";
        if (path.Contains("/price-lists", StringComparison.OrdinalIgnoreCase) || path.Contains("/prices-", StringComparison.OrdinalIgnoreCase))
            return "Price lists and supplier price upload.";
        if (path.Contains("/purchase-requests", StringComparison.OrdinalIgnoreCase))
            return "Purchase requisitions raised from sales or warehouse demand.";
        if (path.Contains("/quote-requests", StringComparison.OrdinalIgnoreCase))
            return "Customer quote requests awaiting a sales response.";
        if (path.Contains("/pos-overview", StringComparison.OrdinalIgnoreCase))
            return "POS overview: tills, card readers and jewellery POS advances.";
        if (path.Contains("/production", StringComparison.OrdinalIgnoreCase))
            return "Manufacturing: BOM, work orders and MRP planning.";
        if (path.Contains("/projects", StringComparison.OrdinalIgnoreCase))
            return "Projects: open jobs, contract count and timesheets.";
        if (path.Contains("/collections", StringComparison.OrdinalIgnoreCase))
            return "Collections and dunning: overdue invoices and reminder stages.";
        if (path.Contains("/workflows", StringComparison.OrdinalIgnoreCase))
            return "Workflow automation: department queues and approval steps.";
        if (path.Contains("/tenant-config", StringComparison.OrdinalIgnoreCase) || path.Contains("/tenant-features", StringComparison.OrdinalIgnoreCase))
            return "Tenant configuration, feature flags and ERP setup.";
        if (path.Contains("/consolidations", StringComparison.OrdinalIgnoreCase) || path.Contains("/business-units", StringComparison.OrdinalIgnoreCase))
            return "Multi-entity consolidations and business units.";
        if (path.Contains("/budgets", StringComparison.OrdinalIgnoreCase))
            return "Budget planning: headers and lines for the fiscal year.";
        if (path.Contains("/cost-models", StringComparison.OrdinalIgnoreCase))
            return "Cost models used for landed cost and inventory valuation.";
        if (path.Contains("/electronic-reporting", StringComparison.OrdinalIgnoreCase) || path.Contains("/nl-reporting", StringComparison.OrdinalIgnoreCase))
            return "Electronic / statutory reporting packs.";
        if (path.Contains("/blockchain", StringComparison.OrdinalIgnoreCase))
            return "Blockchain proof register for invoice and shipment hashes.";
        if (path.Contains("/marketing", StringComparison.OrdinalIgnoreCase) || path.Contains("/social-hub", StringComparison.OrdinalIgnoreCase))
            return "Marketing campaigns, broadcasts and social hub.";
        if (path.Contains("/orders-app", StringComparison.OrdinalIgnoreCase))
            return "Shop orders: status, fulfilment and item-level OMS.";
        if (path.Contains("/product-catalogue", StringComparison.OrdinalIgnoreCase))
            return "Product catalogue: SKUs, names and publish state.";
        if (path.Contains("/pages-app", StringComparison.OrdinalIgnoreCase) || path.Contains("/page-builder", StringComparison.OrdinalIgnoreCase))
            return "CMS pages and page builder.";
        if (path.Contains("/menus-app", StringComparison.OrdinalIgnoreCase))
            return "Storefront menus and navigation.";
        if (path.Contains("/seo-app", StringComparison.OrdinalIgnoreCase) || path.Contains("/sitemap", StringComparison.OrdinalIgnoreCase))
            return "SEO titles, meta and sitemap.";
        if (path.Contains("/integrations", StringComparison.OrdinalIgnoreCase) || path.Contains("/marketplace", StringComparison.OrdinalIgnoreCase))
            return "Marketplace channels and catalogue integrations.";
        if (path.Contains("/payment-gateways", StringComparison.OrdinalIgnoreCase))
            return "Payment gateway credentials and live/test mode.";
        if (path.Contains("/carriers", StringComparison.OrdinalIgnoreCase))
            return "Carriers and custom shipping methods.";
        if (path.Contains("/offices", StringComparison.OrdinalIgnoreCase) || path.Contains("/storages", StringComparison.OrdinalIgnoreCase))
            return "Offices and storage locations.";
        if (path.Contains("/data-migrations", StringComparison.OrdinalIgnoreCase) || path.Contains("/bulk-upload", StringComparison.OrdinalIgnoreCase))
            return "Data migrations and catalogue bulk upload.";
        if (path.Contains("/demo-tenants", StringComparison.OrdinalIgnoreCase) || path.Contains("/tenants-app", StringComparison.OrdinalIgnoreCase))
            return "Tenant fleet and demo tenant registry.";
        if (path.Contains("/ai-service", StringComparison.OrdinalIgnoreCase) || path.Contains("/power-bi", StringComparison.OrdinalIgnoreCase) || path.Contains("/metabase", StringComparison.OrdinalIgnoreCase))
            return "Analytics and AI service connectors.";
        if (path.Contains("/sms-whatsapp", StringComparison.OrdinalIgnoreCase) || path.Contains("/notifications", StringComparison.OrdinalIgnoreCase))
            return "SMS, WhatsApp and notification templates.";
        if (path.Contains("/sso-saml", StringComparison.OrdinalIgnoreCase))
            return "SSO / SAML identity providers.";
        if (path.Contains("/industry-packs", StringComparison.OrdinalIgnoreCase))
            return "Industry packs that release product fields into Product information.";
        if (path.Contains("/finance-close", StringComparison.OrdinalIgnoreCase) || path.Contains("/fin-advanced", StringComparison.OrdinalIgnoreCase))
            return "Period close, accounting automation and advanced finance.";
        if (path.Contains("/demand-intelligence", StringComparison.OrdinalIgnoreCase))
            return "Demand intelligence and forecast inputs.";
        if (path.Contains("/ops-guides", StringComparison.OrdinalIgnoreCase))
            return "Operations guides for CP and ERP staff.";

        var slug = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "module";
        slug = slug.Replace("-app", "", StringComparison.OrdinalIgnoreCase).Replace('-', ' ');
        return char.ToUpperInvariant(slug[0]) + slug[1..] + " — PHP-parity control-panel module. Create or review records here; live rows bind when the shop database is available.";
    }

    private static string PathOnly(string? uriOrPath)
    {
        if (string.IsNullOrWhiteSpace(uriOrPath)) return "";
        var raw = uriOrPath.Trim();
        if (raw.StartsWith("http", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(raw, UriKind.Absolute, out var abs))
            return abs.AbsolutePath;
        var q = raw.IndexOf('?', StringComparison.Ordinal);
        return q >= 0 ? raw[..q] : raw;
    }
}
