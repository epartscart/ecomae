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
        new("Jewellery", "jewellery", "Metal and diamond masters, karat, workshop, tourist VAT, and verification", "/cp/jewellery-masters-app", "fa-diamond", "gold"),
        new("HR / payroll", "people", "People, leave, attendance, payroll, and recruitment", "/erp/payroll-app", "fa-users", "teal"),
        new("Administration", "administration", "Setup, users, roles, module packs, audit, and integrations", "/erp/user-control-app", "fa-shield", "blue"),
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

        if (session.Modules.Count == 0 || session.Modules.Any(m => m.OpenAccess))
        {
            return true;
        }

        return session.Modules.Any(module => ModuleMatches(module.Caption, groupKey));
    }

    public static bool CanAction(LegacySessionContext session, string groupKey, string action)
    {
        if (!CanAccess(session, groupKey))
        {
            return false;
        }

        if (session.Permissions.Contains(EcomAePermissions.SuperErpAccess)
            || action is "View" or "New" or "Edit" or "Submit" or "Print" or "Export")
        {
            return true;
        }

        return session.Modules.Any(module =>
            ModuleMatches(module.Caption, groupKey)
            && module.Caption.Contains(action, StringComparison.OrdinalIgnoreCase));
    }

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
