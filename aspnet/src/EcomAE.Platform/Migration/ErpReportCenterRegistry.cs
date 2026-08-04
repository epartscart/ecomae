namespace EcomAE.Platform.Migration;

/// <summary>
/// Static mirror of PHP <c>epc_rc_registry()</c> metadata (keys/areas/names/descs).
/// Table peeks and computed peeks are read-only; CSV/export remain PHP-authoritative.
/// </summary>
public static class ErpReportCenterRegistry
{
    public enum SourceKind
    {
        Table,
        Computed,
    }

    public sealed record Entry(
        string Key,
        string Area,
        string Name,
        string Desc,
        SourceKind Kind = SourceKind.Table,
        string? Table = null,
        string? FallbackTable = null);

    /// <summary>All 33 PHP report-center registry entries.</summary>
    public static IReadOnlyList<Entry> All { get; } =
    [
        new("ap_vendor_list", "ap", "Vendor list", "All suppliers / vendors on file.", SourceKind.Computed),
        new("ap_withholding", "ap", "Withholding register", "Withholding tax applied on vendor payments.", SourceKind.Table, "epc_wht_txn"),
        new("ar_customer_list", "ar", "Customer master", "Customer master records with credit and terms.", SourceKind.Computed),
        new("bank_accounts", "banking", "Bank accounts", "Cash and bank accounts.", SourceKind.Computed),
        new("bank_instruments_rep", "banking", "Bank instruments", "Letters of credit, guarantees and SBLC with status.", SourceKind.Table, "epc_cft_instrument"),
        new("cash_forecasts_rep", "banking", "Cash flow forecasts", "Cash flow forecast headers.", SourceKind.Table, "epc_cft_forecast"),
        new("budget_plans", "budgeting", "Budget plans", "Budget plans by stage.", SourceKind.Table, "epc_bplan_plan"),
        new("hr_job_reqs", "people", "Job requisitions", "Open and closed recruitment requisitions.", SourceKind.Table, "epc_hrt_job"),
        new("hr_reviews", "people", "Performance reviews", "Performance reviews and overall ratings.", SourceKind.Table, "epc_hrt_review"),
        new("proc_requisitions", "purchasing", "Purchase requisitions", "Requisitions by status.", SourceKind.Table, "epc_proc_req"),
        new("proc_categories", "purchasing", "Procurement categories", "Category hierarchy.", SourceKind.Table, "epc_proc_category"),
        new("tax_withholding", "tax", "Withholding register", "Withholding tax transactions.", SourceKind.Table, "epc_wht_txn"),
        new("tax_er_runs", "tax", "Electronic reporting runs", "Generated electronic reporting runs.", SourceKind.Table, "epc_er_run"),
        new("gl_trial_balance", "finance", "Trial balance", "Account balances as of today.", SourceKind.Computed),
        new("gl_journals", "finance", "Journal register", "General ledger journals.", SourceKind.Table, "epc_erp_gl_journals"),
        new("proc_pos", "purchasing", "Purchase orders", "Purchase orders register.", SourceKind.Table, "epc_erp_purchase_orders"),
        new("sales_orders", "sales", "Sales orders", "Sales orders register.", SourceKind.Table, "epc_erp_sales_orders"),
        new("sales_leads", "sales", "Leads", "Prospect / lead pipeline.", SourceKind.Table, "epc_crm_leads"),
        new("sales_quotes", "sales", "Sales quotations", "Quotations / proposals.", SourceKind.Table, "epc_crm_quotes"),
        new("inv_items", "inventory_mgmt", "Item list", "Released products / items.", SourceKind.Table, "epc_erp_inv_items"),
        new("inv_stock", "inventory_mgmt", "On-hand stock", "Inventory on-hand by item.", SourceKind.Table, "epc_erp_inv_stock"),
        new("pim_items", "pim", "Products", "Product master records.", SourceKind.Table, "epc_erp_inv_items"),
        new("fa_assets", "fixed_assets", "Fixed asset register", "Fixed assets on file.", SourceKind.Table, "epc_erp_fa_assets"),
        new("eam_assets", "asset_mgmt", "Asset register", "Maintained / operational assets.", SourceKind.Table, "epc_eam_assets"),
        new("prj_projects", "projects", "Projects", "Projects register.", SourceKind.Table, "epc_crm_projects", "epc_prj_projects"),
        new("exp_reports", "expense", "Expense reports", "Submitted expense reports.", SourceKind.Table, "epc_erp_expense_reports", "epc_hr_expenses"),
        new("payroll_runs", "payroll_area", "Payroll runs", "Payroll run history.", SourceKind.Table, "epc_erp_payroll_runs", "epc_hr_payroll_runs"),
        new("cost_items", "cost_acct", "Cost items", "Cost accounting items.", SourceKind.Table, "epc_costm_item"),
        new("cost_mgmt_items", "cost_mgmt", "Costed items", "Inventory cost / valuation items.", SourceKind.Table, "epc_costm_item"),
        new("credit_holds", "credit_coll", "Customers on credit hold", "Customers currently on hold.", SourceKind.Computed),
        new("exec_working_capital", "finance", "Working capital analysis", "AR, AP, inventory, cash position and current ratio.", SourceKind.Computed),
        new("exec_ar_aging", "ar", "AR aging summary", "Receivables aging: current, 30, 60, 90, 90+ days.", SourceKind.Computed),
        new("exec_cash_forecast", "banking", "Cash flow forecast (3 months)", "Projected cash inflows and outflows for the next 3 months.", SourceKind.Computed),
    ];

    public static Entry? Find(string key) =>
        All.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
}
