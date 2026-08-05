namespace EcomAE.Platform.Migration;

/// <summary>
/// Static mirror of PHP <c>epc_rc_registry()</c> metadata (keys/areas/names/descs).
/// Report execution for table-backed keys is optional; generation/CSV remain PHP-authoritative.
/// </summary>
public static class ErpReportCenterRegistry
{
    public sealed record Entry(string Key, string Area, string Name, string Desc, string? Table = null);

    /// <summary>All 33 PHP report-center registry entries (run closures not ported).</summary>
    public static IReadOnlyList<Entry> All { get; } =
    [
        new("ap_vendor_list", "ap", "Vendor list", "All suppliers / vendors on file."),
        new("ap_withholding", "ap", "Withholding register", "Withholding tax applied on vendor payments."),
        new("ar_customer_list", "ar", "Customer master", "Customer master records with credit and terms."),
        new("bank_accounts", "banking", "Bank accounts", "Cash and bank accounts."),
        new("bank_instruments_rep", "banking", "Bank instruments", "Letters of credit, guarantees and SBLC with status."),
        new("cash_forecasts_rep", "banking", "Cash flow forecasts", "Cash flow forecast headers."),
        new("budget_plans", "budgeting", "Budget plans", "Budget plans by stage."),
        new("hr_job_reqs", "people", "Job requisitions", "Open and closed recruitment requisitions."),
        new("hr_reviews", "people", "Performance reviews", "Performance reviews and overall ratings."),
        new("proc_requisitions", "purchasing", "Purchase requisitions", "Requisitions by status."),
        new("proc_categories", "purchasing", "Procurement categories", "Category hierarchy."),
        new("tax_withholding", "tax", "Withholding register", "Withholding tax transactions."),
        new("tax_er_runs", "tax", "Electronic reporting runs", "Generated electronic reporting runs."),
        new("gl_trial_balance", "finance", "Trial balance", "Account balances as of today."),
        new("gl_journals", "finance", "Journal register", "General ledger journals.", "epc_erp_gl_journals"),
        new("proc_pos", "purchasing", "Purchase orders", "Purchase orders register.", "epc_erp_purchase_orders"),
        new("sales_orders", "sales", "Sales orders", "Sales orders register.", "epc_erp_sales_orders"),
        new("sales_leads", "sales", "Leads", "Prospect / lead pipeline.", "epc_crm_leads"),
        new("sales_quotes", "sales", "Sales quotations", "Quotations / proposals.", "epc_crm_quotes"),
        new("inv_items", "inventory_mgmt", "Item list", "Released products / items.", "epc_erp_inv_items"),
        new("inv_stock", "inventory_mgmt", "On-hand stock", "Inventory on-hand by item.", "epc_erp_inv_stock"),
        new("pim_items", "pim", "Products", "Product master records.", "epc_erp_inv_items"),
        new("fa_assets", "fixed_assets", "Fixed asset register", "Fixed assets on file.", "epc_erp_fa_assets"),
        new("eam_assets", "asset_mgmt", "Asset register", "Maintained / operational assets.", "epc_eam_assets"),
        new("prj_projects", "projects", "Projects", "Projects register.", "epc_crm_projects"),
        new("exp_reports", "expense", "Expense reports", "Submitted expense reports.", "epc_erp_expense_reports"),
        new("payroll_runs", "payroll_area", "Payroll runs", "Payroll run history.", "epc_erp_payroll_runs"),
        new("cost_items", "cost_acct", "Cost items", "Cost accounting items.", "epc_costm_item"),
        new("cost_mgmt_items", "cost_mgmt", "Costed items", "Inventory cost / valuation items.", "epc_costm_item"),
        new("credit_holds", "credit_coll", "Customers on credit hold", "Customers currently on hold."),
        new("exec_working_capital", "finance", "Working capital analysis", "AR, AP, inventory, cash position and current ratio."),
        new("exec_ar_aging", "ar", "AR aging summary", "Receivables aging: current, 30, 60, 90, 90+ days."),
        new("exec_cash_forecast", "banking", "Cash flow forecast (3 months)", "Projected cash inflows and outflows for the next 3 months."),
    ];

    public static Entry? Find(string key) =>
        All.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
}
