namespace EcomAE.Platform.Migration;

public static class ErpTenantAcceptanceCatalog
{
    public static IReadOnlyList<ErpTenantAcceptanceScenario> Scenarios { get; } =
    [
        new("B1", "workflow", "Workflow, approvals, agenda, and dashboard"),
        new("B2", "order-to-cash", "Order, fulfilment, delivery, invoice, and return"),
        new("B3", "procure-to-pay", "Requisition, RFQ, PO, receipt, invoice, and settlement"),
        new("B4", "inventory", "Warehouse, stock, quality, WMS, and planning"),
        new("B5", "record-to-report", "GL, dimensions, budgets, consolidation, and close"),
        new("B6", "reporting", "IFRS, tax reports, drill-down, and source evidence"),
        new("B7", "treasury", "Cash, bank, payment batches, collections, and reconciliation"),
        new("B8", "tax-compliance", "VAT, corporate tax, e-invoice, and country obligations"),
        new("BJ", "jewellery", "Jewellery masters, stock, sales, repair, and finance"),
        new("BF", "fit-out", "Estimate, project, procurement, progress, and project P&L")
    ];

    public static IReadOnlyList<string> PromotionGates { get; } =
    [
        "php-route-parity",
        "functional-happy-path",
        "validation-and-denial",
        "tenant-isolation",
        "persisted-readback",
        "browser-parity",
        "recovery-and-rollback",
        "uat-and-production"
    ];

    public static bool CanPromoteTenant(IReadOnlySet<string> passedGates) =>
        PromotionGates.All(passedGates.Contains);
}

public sealed record ErpTenantAcceptanceScenario(
    string Id,
    string Area,
    string Description);
