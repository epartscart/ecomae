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
}

public sealed record ErpCapabilityGroup(
    string Label,
    string Key,
    string Description,
    string Href,
    string Icon,
    string Tone);
