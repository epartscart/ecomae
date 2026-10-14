namespace EcomAE.Platform.Presentation;

/// <summary>
/// Numbered operating steps from the finance, HR, payroll, leave, and attendance
/// user guides. Each step opens a form that already exists.
/// </summary>
public static class ErpManualProcessCatalog
{
    public sealed record Step(string Label, string Href);

    public sealed record Guide(string Key, string Title, IReadOnlyList<Step> Steps);

    private static readonly IReadOnlyDictionary<string, string> AreaKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gl"] = "gl",
            ["finance"] = "gl",
            ["budgeting"] = "budget",
            ["payroll_area"] = "payroll",
            ["payroll"] = "payroll",
            ["leave_abs"] = "leave",
            ["time_att"] = "biometric",
            ["people"] = "hr",
        };

    public static readonly IReadOnlyList<Guide> All =
    [
        new("gl", "General ledger",
        [
            new("Main accounts", "/erp/coa-accounts-app"),
            new("General journal, periodic journal, and allocation journal", "/erp/gl-journals-app"),
            new("Reverse a posted journal", "/erp/gl-journals/reverse"),
            new("Trial balance and trial balance by dimension", "/erp/trial-balance-app"),
        ]),
        new("monthly_close", "Monthly close",
        [
            new("Accounts payable", "/erp/payables-workspace-app"),
            new("Accounts receivable", "/erp/receivables-workspace-app"),
            new("Cash and bank", "/erp/cash-bank-workspace-app"),
            new("Fixed assets", "/erp/fixed-assets-workspace-app"),
            new("Inventory", "/erp/inventory-workspace-app"),
            new("VAT", "/erp/vat-app"),
            new("General ledger, then set the period status", "/erp/period-close-app"),
        ]),
        new("year_end", "Year-end close",
        [
            new("Ledger parameters and the fiscal calendar", "/erp/general-ledger-workspace-app"),
            new("Year-end close on the open fiscal year", "/erp/period-close-app"),
            new("Trial balance after the close", "/erp/trial-balance-app"),
        ]),
        new("budget", "Budgeting",
        [
            new("Budget parameters and over-budget permission", "/erp/budgeting-workspace-app"),
            new("Recurring budget entry", "/erp/budgets-app"),
            new("Allocate to dimensions and to periods", "/erp/budgets-app"),
            new("Update the budget balance", "/erp/budgets-app"),
        ]),
        new("payroll", "Payroll",
        [
            new("Pay transactions, airfare, and leave encashment", "/erp/payroll-app"),
            new("Salary review, loans, and advances", "/erp/payroll-workspace-app"),
            new("Generate the pay cycle", "/erp/payroll-app"),
            new("Gratuity and end of service", "/erp/payroll-app"),
            new("WPS, payslip, calendar, and public holidays", "/erp/payroll-workspace-app"),
        ]),
        new("leave", "Leave",
        [
            new("Leave types, plans, and the company calendar", "/erp/leave-absence-app"),
            new("Requests, approval, and accrual", "/erp/leave-absence-app"),
            new("Leave encashment on payroll", "/erp/payroll-app"),
        ]),
        new("biometric", "Time and attendance",
        [
            new("Comp-off setup and attendance", "/erp/time-attendance-app"),
            new("Process the timesheet and check missed login or logout", "/erp/time-attendance-app"),
            new("Approve overtime and review the comp-off balance", "/erp/time-attendance-app"),
            new("Overtime and short-time journals", "/erp/gl-journals-app"),
            new("Transfer approved time to payroll", "/erp/payroll-app"),
        ]),
        new("hr", "Human resources",
        [
            new("Organization, positions, and reports-to", "/erp/hr-workspace-app"),
            new("Manpower requisition and recruitment", "/erp/hr-workspace-app"),
            new("Employee master", "/erp/staff-app"),
            new("Contracts", "/erp/contracts-app"),
            new("Performance", "/erp/performance-app"),
            new("Leave and payroll enrollment", "/erp/leave-absence-app"),
        ]),
    ];

    public static IReadOnlyList<Guide> For(string? area, string? keys)
    {
        var wanted = new List<string>();
        if (!string.IsNullOrWhiteSpace(keys))
        {
            foreach (var part in keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                wanted.Add(part);
            }
        }
        else if (!string.IsNullOrWhiteSpace(area)
            && AreaKeys.TryGetValue(area.Trim(), out var mapped))
        {
            wanted.Add(mapped);
        }

        if (wanted.Count == 0)
        {
            return [];
        }

        var guides = new List<Guide>();
        foreach (var key in wanted)
        {
            var guide = All.FirstOrDefault(g => g.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (guide is not null)
            {
                guides.Add(guide);
            }
        }

        return guides;
    }
}
