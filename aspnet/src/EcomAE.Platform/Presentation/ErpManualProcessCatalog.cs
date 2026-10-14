namespace EcomAE.Platform.Presentation;

/// <summary>
/// Numbered operating steps from the finance, HR, payroll, leave, and attendance
/// user guides. Each step opens a form that already exists.
/// </summary>
public static class ErpManualProcessCatalog
{
    public sealed record Step(string Label, string Href, string Detail = "");

    public sealed record Guide(string Key, string Title, IReadOnlyList<Step> Steps, string Lead = "");

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
            ["sales"] = "sales",
            ["ar"] = "ar",
            ["cost_mgmt"] = "profitability",
            ["inventory_mgmt"] = "syncron",
            ["purchasing"] = "purchase",
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
        new("sales", "Sales",
        [
            new("Quotation", "/erp/sales-quotations-app", "Raise the quotation and send it to the customer."),
            new("Sales order", "/erp/sales-orders-app", "Confirm the order. Posted lines feed revenue and cost."),
            new("Profitability", "/erp/sales-workspace-app?tab=sm_performance", "Read revenue, unit cost, and gross margin for the period."),
        ], "From quotation to the posted margin."),
        new("syncron", "Recommended orders",
        [
            new("Set the policy", "/erp/syncron-app?sv=policies", "Name the policy, pick the demand method, and set service level, lead time, and order multiples."),
            new("Read the suggestion", "/erp/syncron-app?sv=recommendations", "Each row is one item and warehouse. Demand type comes from the last 90 days of movement."),
            new("Change the quantity", "/erp/syncron-app?sv=recommendations", "Type the quantity you want. Save line keeps your number. Confirm accepts the suggestion."),
            new("Draft the purchase order", "/erp/syncron-app?sv=recommendations", "Select the confirmed rows and draft one purchase order. Reject drops a line you will not buy."),
            new("Check the forecast", "/erp/syncron-app?sv=forecast", "The next 30 days show the daily forecast behind the suggestion."),
        ], "Review the suggested order, change it, then confirm or draft a purchase order."),
        new("profitability", "Sales profitability",
        [
            new("Choose the period", "/erp/sales-workspace-app?tab=sm_performance", "Set from and to, then filter by customer or item. The figures use posted sales lines."),
            new("Read the margin", "/erp/sales-workspace-app?tab=sm_performance", "Revenue minus unit cost is the gross margin. The percent is margin divided by revenue."),
            new("Open it in Power BI", "/cp/power-bi-app", "Copy the dataset address and the API key, then use Get Data, Web, in Power BI Desktop."),
            new("Keep the report here", "/erp/cost-management-app?tab=cm_sales_profitability", "Paste a published report address into the embed slot when you want the same view inside this page."),
        ], "Posted sales lines, unit cost, and gross margin, on this page and in Power BI."),
        new("ar", "Customer receivables",
        [
            new("Customer balance", "/erp/receivables-app?ar=balance", "See who owes you. Open is invoices and debit notes minus settled amounts and unapplied receipts."),
            new("Account statement", "/erp/receivables-app?ar=statement", "Pick one customer. The running balance walks every invoice, credit note, and receipt in date order."),
            new("Transactions", "/erp/receivables-app?ar=transactions", "The same movements as a flat list, with the amount and the running balance."),
            new("Mark and settle", "/erp/receivables-app?ar=open", "Tick the open invoices, enter the amount and the cash account, then post the settlement."),
            new("Settled invoices", "/erp/receivables-app?ar=settled", "Each settlement shows the invoice, the amount, and the receipt or credit note that cleared it."),
            new("Ledger reconciliation", "/erp/receivables-app?ar=reconcile", "Compare the customer subledger with the trade-receivables accounts in the ledger."),
        ], "From the balance list to the statement, then mark the open invoices and settle them."),
        new("purchase", "Purchase",
        [
            new("Request for quotation", "/erp/rfq-app", "Create the request, send it to suppliers, and keep the replies on the same document."),
            new("Purchase order", "/erp/purchase-orders-app", "Turn the accepted quotation into a purchase order and approve it."),
            new("Supplier invoice", "/erp/purchases-app", "Invoice what was received. The posted invoice is the accounts-payable balance."),
            new("Suggested replenishment", "/erp/syncron-app?sv=recommendations", "When a stock policy suggests a buy, confirm the quantity and draft the purchase order from that row."),
        ], "From the supplier quotation to the purchase order, then the invoice."),
        new("jewellery", "Jewellery retail",
        [
            new("Daily gold rate", "/erp/jewellery-masters-app?tab=gold_rate", "Enter today's metal rate before the counter sale uses it."),
            new("Masters", "/erp/jewellery-masters-app", "Keep karat, rate type, design, and stone masters before you sell or tag a piece."),
            new("Retail sale", "/erp/jewellery-retail-app?tab=jw_retail_sales", "Sell at the counter. Metal rate, tag, and payment stay on the voucher."),
            new("Metal stock", "/erp/jewellery-stock-verification-app?tab=jw_metal_stock", "Read the metal on hand by karat and location."),
            new("Fixing", "/erp/jewellery-fixing-app", "Fix or unfix a metal position, then settle it."),
            new("Repairs", "/erp/jewellery-repairs-app", "Receive the repair, move it through the workshop, and deliver it back."),
        ], "Masters, the counter sale, metal stock, fixing, and repairs."),
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
