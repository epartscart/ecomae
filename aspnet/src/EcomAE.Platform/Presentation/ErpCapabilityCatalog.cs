using EcomAE.Platform.Auth;
using EcomAE.Platform.Security;

namespace EcomAE.Platform.Presentation;

public static class ErpCapabilityCatalog
{
    public static IReadOnlyList<ErpCapabilityGroup> Groups { get; } =
    [
        new("Finance", "finance", "GL, payables, receivables, cash, VAT, corporate tax, IFRS, and external reporting", "/erp/finance-app", "fa-money", "gold"),
        new("Purchasing", "purchasing", "Suppliers, RFQs, purchase requests, purchase orders, receiving, and landed cost", "/erp/purchases-app", "fa-shopping-basket", "green"),
        new("Sales", "sales", "Quotations, orders, fulfilment, invoices, collections, and returns", "/erp/sales-orders-app", "fa-line-chart", "rust"),
        new("Inventory", "inventory", "Warehouses, stock, transfers, counts, planning, and quality", "/erp/inventory-stock-app", "fa-cubes", "slate"),
        new("Projects / fit-out", "projects", "BOQ, contracts, cost codes, site reports, claims, and project P&L", "/erp/project-accounting-app", "fa-building", "amber"),
        new("Real estate", "real_estate", "Investment, property, leasing, development, construction and ROI", "/erp/real-estate-app", "fa-home", "teal"),
        new("Jewellery", "jewellery", "Metal and diamond masters, karat, workshop, tourist VAT, and verification", "/erp/jewellery-masters-app", "fa-diamond", "gold"),
        new("HR / payroll", "people", "People, leave, attendance, payroll, and recruitment", "/erp/payroll-app", "fa-users", "teal"),
        new("Administration", "administration", "Setup, users, roles, module packs, audit, and integrations", "/erp/users-app", "fa-shield", "blue"),
    ];

    public static IReadOnlyList<string> Actions { get; } =
    [
        "View",
        "New",
        "Edit",
        "Delete",
        "Void",
        "Submit",
        "Approve",
        "Reject",
        "Post",
        "Reverse",
        "Print",
        "Export",
    ];

    public static bool CanAccess(LegacySessionContext session, string groupKey)
    {
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
        {
            return false;
        }

        if (session.Permissions.Contains(EcomAePermissions.SuperErpAccess))
        {
            return true;
        }

        if (IsControlled(session))
        {
            return session.AreaGrants!.Any(grant =>
                GroupEquals(grant.GroupKey, groupKey)
                && (grant.AllActions || grant.Actions.Count > 0));
        }

        if (session.Modules.Count == 0 || session.Modules.Any(m => m.OpenAccess))
        {
            return true;
        }

        return session.Modules.Any(module => ModuleMatches(module.Caption, groupKey));
    }

    public static bool CanAction(LegacySessionContext session, string groupKey, string action)
    {
        if (session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains("erp"))
        {
            return false;
        }

        if (session.Permissions.Contains(EcomAePermissions.SuperErpAccess))
        {
            return true;
        }

        if (IsControlled(session))
        {
            return session.AreaGrants!.Any(grant =>
                GroupEquals(grant.GroupKey, groupKey)
                && (grant.AllActions || grant.Actions.Any(saved => saved.Equals(action, StringComparison.OrdinalIgnoreCase))));
        }

        if (!CanAccess(session, groupKey))
        {
            return false;
        }

        if (action is "View" or "New" or "Edit" or "Submit" or "Print" or "Export")
        {
            return true;
        }

        return session.Modules.Any(module =>
            ModuleMatches(module.Caption, groupKey)
            && module.Caption.Contains(action, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsControlled(LegacySessionContext session)
        => session.AreaGrants is { Count: > 0 };

    private static bool GroupEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool ModuleMatches(string caption, string groupKey)
    {
        var text = caption.Trim();
        var aliases = groupKey switch
        {
            "finance" => new[] { "finance", "account", "ledger", "vat", "tax", "invoice", "payment", "cash" },
            "purchasing" => new[] { "purchase", "procure", "supplier", "vendor", "rfq", "receiving" },
            "sales" => new[] { "sales", "invoice", "customer", "order", "quotation", "receivable" },
            "inventory" => new[] { "inventory", "warehouse", "stock", "transfer", "quality" },
            "projects" => new[] { "project", "fit-out", "contract", "boq", "claim" },
            "real_estate" => new[] { "real estate", "property", "lease", "tenant", "valuation" },
            "jewellery" => new[] { "jewel", "diamond", "karat", "metal", "workshop" },
            "people" => new[] { "hr", "human", "payroll", "staff", "employee", "leave", "attendance" },
            "administration" => new[] { "admin", "security", "role", "user", "integration", "audit" },
            _ => Array.Empty<string>(),
        };

        return aliases.Any(alias => text.Contains(alias, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record ErpCapabilityGroup(
    string Label,
    string Key,
    string Description,
    string Href,
    string Icon,
    string Tone);
