namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_erp_departments_config</c> — static department map (not shop data).</summary>
public static class ErpStaffDepartmentCatalog
{
    public sealed record Department(
        string Code,
        string Name,
        string Icon,
        string Color,
        string TabsLabel,
        IReadOnlyList<string> Workflows);

    public static IReadOnlyList<Department> All { get; } =
    [
        new("admin", "Administration", "fa-shield", "#334155", "All modules",
            ["Month-end ERP checklist", "User access review", "Cross-department escalation"]),
        new("sales", "Sales", "fa-line-chart", "#2563eb", "dashboard, crm, leads, opportunities, proposals, sales_orders…",
            ["Qualify customer / credit check", "Confirm order & payment terms", "Track open receivable"]),
        new("logistics", "Logistics", "fa-truck", "#0d9488", "dashboard, fulfilment, aftersales, inventory, workflow",
            ["Reserve stock for order", "Pick, pack & carrier label", "Confirm delivery to customer"]),
        new("marketing", "Marketing", "fa-bullhorn", "#db2777", "dashboard, marketing, workflow, staff",
            ["Plan campaign & budget", "Launch ads / marketplace promo", "Track leads & conversion"]),
        new("finance", "Finance", "fa-money", "#16a34a", "dashboard, receivables, payables, cash_bank, payroll…",
            ["Approve & pay payroll run", "Supplier payment run", "Cash & bank reconciliation"]),
        new("hr", "Human Resources", "fa-users", "#9333ea", "dashboard, hr, payroll, staff, workflow",
            ["Prepare monthly payroll", "Onboard new staff ERP access", "Leave & attendance record"]),
        new("it", "Information Technology", "fa-laptop", "#0891b2", "dashboard, workflow, staff",
            ["ERP & CP user access", "System backup & security review", "Support staff ERP login issues"]),
        new("purchase", "Purchase", "fa-shopping-basket", "#ea580c", "dashboard, purchases, payables, inventory…",
            ["Raise PO / supplier order", "Match supplier invoice to order", "Record purchase in ERP"]),
        new("accounts", "Accounts", "fa-book", "#1d4ed8", "dashboard, revenue, gl, vat_return, payroll…",
            ["Post sales orders to GL", "Prepare P&L & balance sheet", "Trial balance & period close"]),
    ];

    public static string NameFor(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "—";
        foreach (var row in All)
        {
            if (string.Equals(row.Code, code, StringComparison.OrdinalIgnoreCase))
                return row.Name;
        }

        return code;
    }
}

/// <summary>PHP <c>epc_erp_dashboard_profiles_config</c> labels (override list).</summary>
public static class ErpDashboardProfileCatalog
{
    public static IReadOnlyList<(string Key, string Label)> All { get; } =
    [
        ("ceo", "CEO centre"),
        ("cfo", "CFO centre"),
        ("finance", "Finance centre"),
        ("accounts", "Accounts centre"),
        ("sales", "Sales centre"),
        ("purchase", "Purchase centre"),
        ("hr", "HR centre"),
        ("logistics", "Logistics centre"),
        ("marketing", "Marketing centre"),
        ("it", "IT centre"),
    ];
}
