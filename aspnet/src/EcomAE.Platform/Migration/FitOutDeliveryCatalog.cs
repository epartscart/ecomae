namespace EcomAE.Platform.Migration;

public sealed record FitOutDeliveryStep(
    string Id,
    string Phase,
    string Name,
    string Status,
    string Surface);

public static class FitOutDeliveryCatalog
{
    public static IReadOnlyList<FitOutDeliveryStep> Steps { get; } =
    [
        new("P1-01", "P1 core", "Lead and customer handoff", "partial", "CRM/ERP"),
        new("P1-02", "P1 core", "Estimate and BOQ header", "partial", "ERP"),
        new("P1-03", "P1 core", "BOQ sections and line items", "partial", "ERP"),
        new("P1-04", "P1 core", "Material, labour, subcontract, equipment and overhead rates", "partial", "ERP"),
        new("P1-05", "P1 core", "Markup and estimate revisions", "partial", "ERP"),
        new("P1-06", "P1 core", "Estimate import/export contract", "partial", "ERP"),
        new("P1-07", "P1 core", "Quotation issue and revision", "partial", "ERP"),
        new("P1-08", "P1 core", "Contract commercial terms", "partial", "ERP contracts"),
        new("P1-09", "P1 core", "Advance, retention and warranty rules", "partial", "ERP"),
        new("P1-10", "P1 core", "Project and hierarchical cost codes", "partial", "ERP projects"),
        new("P1-11", "P1 core", "Budget, committed, actual and forecast ledger", "partial", "ERP project accounting"),
        new("P1-12", "P1 core", "Numbering and approval thresholds", "partial", "ERP workflow"),
        new("P2 operations", "P2 operations", "Purchase requisition and RFQ linkage", "partial", "ERP procurement"),
        new("P2 operations", "P2 operations", "Purchase order and GRN linkage", "partial", "ERP procurement"),
        new("P2 operations", "P2 operations", "Three-way match and tolerance", "partial", "ERP procurement"),
        new("P2 operations", "P2 operations", "Material issue and return", "partial", "ERP inventory"),
        new("P2 operations", "P2 operations", "Subcontract order and measurement", "partial", "ERP subcontract"),
        new("P2 operations", "P2 operations", "Subcontract certification", "partial", "ERP subcontract"),
        new("P2 operations", "P2 operations", "Site daily report and photo evidence", "partial", "ERP mobile"),
        new("P2 operations", "P2 operations", "Equipment usage and timesheets", "partial", "ERP projects"),
        new("P2 operations", "P2 operations", "RFI and drawing revisions", "partial", "ERP quality"),
        new("P2 operations", "P2 operations", "QA/QC inspection and snag lifecycle", "partial", "ERP quality"),
        new("P3 finance", "P3 finance", "Approved variations and contract value revision", "partial", "ERP projects"),
        new("P3 finance", "P3 finance", "Weighted BOQ progress", "partial", "ERP projects"),
        new("P3 finance", "P3 finance", "Progress claim and certification", "partial", "ERP projects"),
        new("P3 finance", "P3 finance", "Retention and advance recovery", "partial", "ERP finance"),
        new("P3 finance", "P3 finance", "VAT and invoice bridge", "partial", "ERP tax"),
        new("P3 finance", "P3 finance", "Project P&L and forecast profit", "partial", "ERP project accounting"),
        new("P4 advanced", "P4 advanced", "CEO, PM, finance and procurement dashboards", "partial", "ERP dashboards"),
        new("P4 advanced", "P4 advanced", "Sales, estimation and procurement reports", "pending", "ERP reporting"),
        new("P4 advanced", "P4 advanced", "Inventory, subcontract and finance reports", "pending", "ERP reporting"),
        new("P4 advanced", "P4 advanced", "Fit-out scenario acceptance evidence", "pending", "Migration gates")
    ];

    public static object BuildReport() => new
    {
        pack = "fit-out-interior-contracting",
        tenantCountryDriven = true,
        phases = Steps
            .GroupBy(step => step.Phase, StringComparer.Ordinal)
            .Select(group => new
            {
                phase = group.Key,
                total = group.Count(),
                completed = group.Count(step => step.Status == "complete"),
                partial = group.Count(step => step.Status == "partial"),
                pending = group.Count(step => step.Status == "pending")
            }),
        steps = Steps
    };
}
