namespace EcomAE.Platform.Presentation;

/// <summary>
/// Official process lanes, document interlinks, and company-parameter keys
/// taken from the shared Dynamics module trees. Steps and edges only reference
/// existing workspace tabs. Parameter captions are official form labels.
/// Field maps use the workspace writer fields already on those pages.
/// </summary>
public static class ErpD365ProcessCatalog
{
    public const string ParameterSavePath = "/erp/d365-parameters/save";
    public const string LinkSavePath = "/erp/d365-link/save";
    public const string StudySavePath = ErpD365ModuleStudyCatalog.StudySavePath;

    public static readonly HashSet<string> OfficialFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "name", "worker", "party", "kind", "status", "period",
        "date_from", "date_to", "qty", "amount", "extra", "note",
    };

    public enum Stage
    {
        Setup,
        Masters,
        Documents,
        Post,
        Inquiry,
    }

    public sealed record ProcessStep(string Tab, Stage Stage, string Role);

    public sealed record ProcessLane(
        string Code,
        string Label,
        string Area,
        string Purpose,
        string SetupStyle,
        string ConfigWay,
        IReadOnlyList<ProcessStep> Steps);

    public sealed record InterlinkEdge(
        string FromTab,
        string ToTab,
        string Relation,
        string FieldMap,
        string Purpose);

    public sealed record ParameterDef(
        string Area,
        string Key,
        string Caption,
        string Default,
        string ValueKind,
        string Group,
        IReadOnlyList<string> AffectsTabs,
        IReadOnlyList<string>? Options = null);

    public static readonly IReadOnlyList<ProcessLane> Lanes = BuildLanes();
    public static readonly IReadOnlyList<InterlinkEdge> Edges = BuildEdges();
    public static readonly IReadOnlyList<ParameterDef> Parameters = BuildParameters();

    public static IReadOnlyList<ProcessLane> LanesForArea(string? area)
    {
        var key = (area ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return Lanes;
        }

        if (IsAdminStudyArea(key))
        {
            return Lanes;
        }

        return Lanes.Where(l => l.Area.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static IReadOnlyList<ProcessLane> LanesForTab(string? tab)
    {
        var key = (tab ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return [];
        }

        return Lanes.Where(l => l.Steps.Any(s => s.Tab.Equals(key, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public static IReadOnlyList<InterlinkEdge> EdgesFrom(string? tab)
        => Edges.Where(e => e.FromTab.Equals((tab ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

    public static IReadOnlyList<InterlinkEdge> EdgesTo(string? tab)
        => Edges.Where(e => e.ToTab.Equals((tab ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

    public static bool TryGetEdge(string? fromTab, string? toTab, out InterlinkEdge edge)
    {
        edge = Edges.FirstOrDefault(e =>
            e.FromTab.Equals((fromTab ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
            && e.ToTab.Equals((toTab ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))!;
        return edge is not null;
    }

    public static IReadOnlyList<ParameterDef> ParametersForArea(string? area)
        => Parameters.Where(p => p.Area.Equals((area ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

    public static IReadOnlyList<ParameterDef> ParametersForTab(string? tab)
    {
        var key = (tab ?? string.Empty).Trim();
        return Parameters.Where(p => p.AffectsTabs.Any(t => t.Equals(key, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public static bool TryGetParameter(string? area, string? key, out ParameterDef parameter)
    {
        parameter = Parameters.FirstOrDefault(p =>
            p.Area.Equals((area ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
            && p.Key.Equals((key ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))!;
        return parameter is not null;
    }

    public static bool IsAdminStudyArea(string? area)
    {
        var key = (area ?? string.Empty).Trim().ToLowerInvariant();
        return key is "enterprise" or "setup";
    }

    public static IReadOnlyList<string> ParseFieldMap(string? fieldMap)
        => (fieldMap ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(OfficialFields.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string HrefForTab(string? tab)
    {
        if (ErpD365WorkspaceCatalog.TryMapTab(tab, out var href) && !string.IsNullOrWhiteSpace(href))
        {
            return href;
        }

        return "/erp/d365-workspace-app?tab=" + Uri.EscapeDataString((tab ?? string.Empty).Trim());
    }

    private static IReadOnlyList<ProcessLane> BuildLanes() =>
    [
        Lane(
            "procure_to_pay",
            "Procure to pay",
            "purchasing",
            "Request, quote, order, receive, invoice, and pay a vendor.",
            "Open Procurement and sourcing parameters, then vendor groups, number sequences, and purchase workflows before documents.",
            "Company parameters first; vendor-group defaults next; purchase-order header overrides last.",
            S("ps_parameters", Stage.Setup, "Company purchase defaults"),
            S("ps_vendors", Stage.Masters, "Vendor master"),
            S("ps_number_seq", Stage.Setup, "Purchase number sequences"),
            S("ps_workflows", Stage.Setup, "Purchase workflows"),
            S("purchase_requisitions", Stage.Documents, "Purchase requisition"),
            S("rfq", Stage.Documents, "Request for quotation"),
            S("purchase_orders", Stage.Documents, "Purchase order"),
            S("proc_receipts", Stage.Post, "Product receipt"),
            S("three_way_match", Stage.Post, "Invoice matching"),
            S("purchases", Stage.Post, "Vendor invoice"),
            S("ap_payment_journal", Stage.Post, "Vendor payment"),
            S("ap_settled_invoices", Stage.Inquiry, "Settlement")),
        Lane(
            "order_to_cash",
            "Order to cash",
            "sales",
            "Qualify demand, quote, order, ship, invoice, and collect.",
            "Open Sales and marketing parameters, then customers, credit limits, and sales workflows before quotations.",
            "Company parameters first; customer-group credit and terms next; sales-order header overrides last.",
            S("sm_parameters", Stage.Setup, "Company sales defaults"),
            S("sm_all_customers", Stage.Masters, "Customer master"),
            S("sm_workflows", Stage.Setup, "Sales workflows"),
            S("sm_opportunities", Stage.Documents, "Opportunity"),
            S("sm_quotations", Stage.Documents, "Sales quotation"),
            S("sm_all_orders", Stage.Documents, "Sales order"),
            S("sm_packing", Stage.Post, "Packing slip"),
            S("sm_invoice", Stage.Post, "Customer invoice"),
            S("ar_payment_journal", Stage.Post, "Customer payment"),
            S("ar_settled_invoices", Stage.Inquiry, "Settlement"),
            S("ar_collections_workspace", Stage.Inquiry, "Collections")),
        Lane(
            "accounts_payable",
            "Accounts payable",
            "ap",
            "Register, match, approve, pay, and settle vendor invoices.",
            "Open invoice matching setup and vendor posting profiles before the invoice journal.",
            "Matching policy at company; vendor-group posting profile; invoice header match override.",
            S("ap_matching_setup", Stage.Setup, "Invoice matching"),
            S("ap_posting_profiles", Stage.Setup, "Vendor posting profiles"),
            S("ap_all_vendors", Stage.Masters, "Vendor master"),
            S("ap_purchase_orders", Stage.Documents, "Purchase order"),
            S("ap_invoice_register", Stage.Documents, "Invoice register"),
            S("ap_invoice_approval", Stage.Post, "Invoice approval"),
            S("ap_invoice_journal", Stage.Documents, "Invoice journal"),
            S("ap_invoice_matching", Stage.Post, "Three-way match"),
            S("ap_payment_proposal", Stage.Post, "Payment proposal"),
            S("ap_payment_journal", Stage.Post, "Vendor payment"),
            S("ap_settled_invoices", Stage.Inquiry, "Settled invoices"),
            S("ap_vendor_aging", Stage.Inquiry, "Vendor aging")),
        Lane(
            "accounts_receivable",
            "Accounts receivable",
            "ar",
            "Invoice customers, collect payment, and settle open balances.",
            "Open customer posting profiles, terms of payment, and credit limit type before invoices.",
            "Company credit and settlement; customer-group terms; invoice or payment override.",
            S("ar_posting_profiles", Stage.Setup, "Customer posting profiles"),
            S("ar_terms_payment", Stage.Setup, "Terms of payment"),
            S("ar_auto_settlement", Stage.Setup, "Automatic settlement"),
            S("ar_all_customers", Stage.Masters, "Customer master"),
            S("ar_sales_orders", Stage.Documents, "Sales order"),
            S("ar_free_text_invoices", Stage.Documents, "Free text invoice"),
            S("ar_invoice_journal", Stage.Documents, "Invoice journal"),
            S("ar_open_invoices", Stage.Inquiry, "Open invoices"),
            S("ar_payment_journal", Stage.Post, "Customer payment"),
            S("ar_settled_invoices", Stage.Inquiry, "Settled invoices"),
            S("ar_customer_aging", Stage.Inquiry, "Customer aging")),
        Lane(
            "general_ledger",
            "General ledger",
            "gl",
            "Enter journals, keep them balanced, post, and inquire balances.",
            "Set ledger parameters (accounting currency, fiscal calendar, must be balanced) before journals.",
            "Company ledger parameters; journal-name defaults; voucher-level posting layer.",
            S("gl_parameters", Stage.Setup, "Ledger parameters"),
            S("gl_journals", Stage.Documents, "General journal"),
            S("gl_trial_balance", Stage.Inquiry, "Trial balance")),
        Lane(
            "inventory",
            "Inventory",
            "inventory_mgmt",
            "Track on-hand, reserve demand, transfer, count, and adjust.",
            "Open inventory parameters for reservation, warehouse mandatory, and negative inventory first.",
            "Company reservation hierarchy; warehouse defaults; journal-line warehouse override.",
            S("inv_parameters", Stage.Setup, "Inventory parameters"),
            S("inv_reservation", Stage.Setup, "Reservation hierarchy"),
            S("inv_onhand", Stage.Inquiry, "On-hand"),
            S("inv_transfer", Stage.Documents, "Transfer"),
            S("inv_counting", Stage.Documents, "Counting")),
        Lane(
            "warehouse",
            "Warehouse",
            "warehouse",
            "Plan work, build waves, pick, pack, and ship.",
            "Set warehouse parameters, work templates, and wave templates before outbound work.",
            "Company warehouse parameters; wave and work templates; load-level wave override.",
            S("wh_parameters", Stage.Setup, "Warehouse parameters"),
            S("wh_wave_templates", Stage.Setup, "Wave templates"),
            S("wh_work_templates", Stage.Setup, "Work templates"),
            S("wh_work_plan", Stage.Documents, "Work planning"),
            S("wh_waved_loads", Stage.Post, "Waved loads"),
            S("wh_work_inq", Stage.Inquiry, "Work"),
            S("wh_wave_inq", Stage.Inquiry, "Wave report")),
        Lane(
            "production",
            "Production",
            "production",
            "Plan, release, pick, start, report as finished, and cost.",
            "Set production control parameters, then BOM, route, and calendars before orders.",
            "Company reservation and BOM consumption; production-unit defaults; order-level override.",
            S("prd_parameters", Stage.Setup, "Production parameters"),
            S("prd_planned", Stage.Documents, "Planned production order"),
            S("prd_all_orders", Stage.Documents, "Production order"),
            S("prd_release", Stage.Post, "Release"),
            S("prd_start", Stage.Post, "Start"),
            S("prd_picking_list", Stage.Post, "Picking list"),
            S("prd_raf", Stage.Post, "Report as finished"),
            S("prd_end", Stage.Post, "End"),
            S("prd_cost_est", Stage.Inquiry, "Costing"),
            S("prd_overview", Stage.Inquiry, "Production overview")),
        Lane(
            "master_planning",
            "Master planning",
            "master_planning_area",
            "Cover demand, regenerate planned orders, and firm supply.",
            "Set master planning parameters, coverage groups, and item coverage before regeneration.",
            "Company planning parameters; coverage group; item-coverage warehouse and lead time.",
            S("mp_parameters", Stage.Setup, "Master planning parameters"),
            S("coverage_groups", Stage.Setup, "Coverage groups"),
            S("item_coverage", Stage.Masters, "Item coverage"),
            S("planned_orders", Stage.Documents, "Planned orders"),
            S("prd_planned", Stage.Documents, "Planned production"),
            S("purchase_orders", Stage.Post, "Firmed purchase order")),
        Lane(
            "tax",
            "Tax",
            "tax",
            "Set tax codes, apply them on documents, then settle and post.",
            "Create sales tax codes and groups, then settlement periods, before journals.",
            "Tax code rate; sales-tax and item-sales-tax groups; document-line tax override.",
            S("tax_calc_params", Stage.Setup, "Tax calculation"),
            S("tax_sales_codes", Stage.Masters, "Sales tax codes"),
            S("tax_sales_groups", Stage.Masters, "Sales tax groups"),
            S("tax_settlement", Stage.Setup, "Settlement periods"),
            S("tax_settle_post", Stage.Post, "Settle and post")),
        Lane(
            "payroll",
            "Payroll",
            "payroll_area",
            "Collect time, generate a cycle, validate, pay, and file WPS.",
            "Set payroll parameters, grades, and calendars before generating a pay cycle.",
            "Company pay cycle and payment method; grade scale; worker earning override.",
            S("pay_parameters", Stage.Setup, "Payroll parameters"),
            S("pay_grades", Stage.Masters, "Employee grades"),
            S("pay_calendars", Stage.Setup, "Calendars"),
            S("pay_attendance", Stage.Documents, "Attendance"),
            S("pay_timesheet", Stage.Documents, "Time sheet"),
            S("pay_generate_cycle", Stage.Post, "Generate pay cycle"),
            S("pay_validate_cycle", Stage.Post, "Validate"),
            S("pay_payment_stmt", Stage.Post, "Payment statements"),
            S("pay_wps", Stage.Inquiry, "WPS")),
        Lane(
            "time_attendance",
            "Time and attendance",
            "time_att",
            "Register time, approve it, and transfer to pay.",
            "Approve registrations, then transfer approved time to payroll.",
            "Company time defaults; approval workflow; transfer-to-pay earning map.",
            S("tna_approve", Stage.Post, "Approve"),
            S("tna_transfer_pay", Stage.Post, "Transfer to pay")),
        Lane(
            "projects",
            "Projects",
            "projects",
            "Quote, staff, capture time, propose, and invoice a project.",
            "Create the project and contract first, then resources and forecast, then timesheets.",
            "Project-type defaults; contract billing rule; timesheet and invoice-proposal override.",
            S("pma_quotations", Stage.Documents, "Project quotation"),
            S("pma_all_projects", Stage.Masters, "Project"),
            S("pma_contracts", Stage.Masters, "Project contract"),
            S("pma_resources", Stage.Masters, "Resources"),
            S("pma_timesheets", Stage.Documents, "Timesheet"),
            S("pma_invoice_prop", Stage.Documents, "Invoice proposal"),
            S("pma_invoices", Stage.Post, "Project invoice"),
            S("pma_posted_txn", Stage.Inquiry, "Posted transactions"),
            S("pma_pl_actual", Stage.Inquiry, "Profit and loss")),
        Lane(
            "fixed_assets",
            "Fixed assets",
            "fixed_assets",
            "Acquire, depreciate, adjust, and dispose of assets.",
            "Set fixed assets parameters, books, and depreciation profiles before the register.",
            "Company book and journal names; asset-group profile; asset-book override.",
            S("fa_parameters", Stage.Setup, "Fixed assets parameters"),
            S("fa_books", Stage.Setup, "Books"),
            S("fa_depr_profiles", Stage.Setup, "Depreciation profiles"),
            S("fa_assets", Stage.Masters, "Fixed assets"),
            S("fa_acq_proposal", Stage.Documents, "Acquisition proposal"),
            S("fa_journal", Stage.Documents, "Asset journal"),
            S("fa_depr_proposal", Stage.Post, "Depreciation proposal"),
            S("fa_sold", Stage.Post, "Disposal"),
            S("fa_balances", Stage.Inquiry, "Balances")),
        Lane(
            "cash_bank",
            "Cash and bank",
            "banking",
            "Hold bank accounts, post cash, import statements, and reconcile.",
            "Set cash and bank parameters and bank accounts before journals and statements.",
            "Company bank defaults; account currency; statement matching rules.",
            S("cb_parameters", Stage.Setup, "Cash and bank parameters"),
            S("cb_bank_accounts", Stage.Masters, "Bank accounts"),
            S("cb_bank_transfers", Stage.Documents, "Bank transfers"),
            S("cb_bank_statements", Stage.Documents, "Bank statements"),
            S("cb_bank_recon", Stage.Post, "Reconciliation"),
            S("cb_balance_control", Stage.Inquiry, "Balance control")),
        Lane(
            "budgeting",
            "Budgeting",
            "budgeting",
            "Plan, register, control, and compare budget to actual.",
            "Set budgeting parameters, models, and control configuration before register entries.",
            "Company budget model and cycle; allocation terms; document-level budget check.",
            S("bud_parameters", Stage.Setup, "Budgeting parameters"),
            S("bud_models", Stage.Setup, "Budget models"),
            S("bud_control_config", Stage.Setup, "Budget control"),
            S("bud_plans", Stage.Documents, "Budget plans"),
            S("bud_register_entries", Stage.Documents, "Budget register"),
            S("bud_gen_register", Stage.Post, "Generate register entry"),
            S("bud_actual_vs_budget", Stage.Inquiry, "Actual versus budget")),
        Lane(
            "leave",
            "Leave and absence",
            "leave_abs",
            "Set plans, enroll workers, request, approve, and accrue.",
            "Set leave parameters and plans, then enroll workers, then take requests.",
            "Company accrual frequency; plan grant units; worker enrollment override.",
            S("leave_parameters", Stage.Setup, "Leave parameters"),
            S("leave_plans", Stage.Setup, "Leave plans"),
            S("leave_types", Stage.Setup, "Leave types"),
            S("leave_employees", Stage.Masters, "Employees"),
            S("time_off_requests", Stage.Documents, "Time off request"),
            S("leave_accrue", Stage.Post, "Accrue"),
            S("leave_accrual_audit", Stage.Inquiry, "Accrual audit")),
        Lane(
            "expense",
            "Expense",
            "expense",
            "Enter expenses, approve the report, post, and reimburse.",
            "Set expense parameters and categories before workers enter reports.",
            "Company expense defaults; category tax; report-line override.",
            S("exp_parameters", Stage.Setup, "Expense parameters"),
            S("exp_my_reports", Stage.Documents, "Expense report"),
            S("exp_cash_advances", Stage.Documents, "Cash advance")),
        Lane(
            "landed_cost",
            "Landed cost",
            "landed_cost_area",
            "Attach inbound cost to a voyage, estimate, cost, and invoice.",
            "Set landed cost parameters and voyage statuses before attaching purchase lines.",
            "Company voyage costing method; cost-type allocation; voyage-line override.",
            S("lc_parameters", Stage.Setup, "Landed cost parameters"),
            S("proc_import_orders", Stage.Documents, "Import purchase order"),
            S("lc_voyages", Stage.Documents, "Voyage"),
            S("lc_voyage_lines", Stage.Documents, "Voyage lines"),
            S("lc_voyage_costing", Stage.Post, "Voyage costing")),
        Lane(
            "human_resources",
            "Human resources",
            "people",
            "Hire workers, assign positions, and feed leave and payroll.",
            "Set human resources parameters before workers and positions.",
            "Company HR defaults; position compensation; worker override.",
            S("hr_parameters", Stage.Setup, "Human resources parameters"),
            S("hr_workers", Stage.Masters, "Workers")),
        Lane(
            "credit_collections",
            "Credit and collections",
            "credit_coll",
            "Set credit limits, hold risk, and collect past-due invoices.",
            "Set accounts receivable parameters, blocking rules, and letter sequence first.",
            "Company credit limit type; customer-group limit; document credit check.",
            S("cc_parameters", Stage.Setup, "Accounts receivable parameters"),
            S("cc_blocking_rules", Stage.Setup, "Blocking rules"),
            S("cc_letter_sequence", Stage.Setup, "Collection letter sequence"),
            S("cc_all_customers", Stage.Masters, "Customers"),
            S("cc_hold_list", Stage.Documents, "Credit holds"),
            S("cc_collection_cases", Stage.Documents, "Collections cases"),
            S("cc_collection_letters", Stage.Post, "Collection letters"),
            S("cc_aging_report", Stage.Inquiry, "Aged balances")),
        Lane(
            "organization",
            "Organization",
            "enterprise",
            "Legal entities, number sequences, and shared address book.",
            "Create the legal entity, then number sequences, then shared parties.",
            "Legal-entity calendar and currency; number-sequence scope; party shared across modules.",
            S("legal_entities", Stage.Setup, "Legal entities"),
            S("org_number_sequences", Stage.Setup, "Number sequences"),
            S("org_gab", Stage.Masters, "Global address book")),
    ];

    private static IReadOnlyList<InterlinkEdge> BuildEdges() =>
    [
        E("purchase_requisitions", "rfq", "creates", "party,name,qty,amount,note", "Requisition becomes an RFQ."),
        E("rfq", "purchase_orders", "awards", "party,name,qty,amount,note", "Awarded RFQ creates a purchase order."),
        E("ps_rfq_journals", "purchase_orders", "awards", "party,name,qty,amount,note", "RFQ journal awards a purchase order."),
        E("proc_rfq_followup", "purchase_orders", "awards", "party,name,qty,amount,note", "RFQ follow-up awards a purchase order."),
        E("proc_agreements", "purchase_orders", "creates", "party,name,qty,amount,note", "Purchase agreement releases a purchase order."),
        E("purchase_orders", "proc_confirmations", "confirms", "code,party,qty,amount,note", "Vendor confirms the purchase order."),
        E("purchase_orders", "proc_receipts", "receives", "code,party,name,qty,amount,note", "Product receipt against the purchase order."),
        E("proc_receipts", "three_way_match", "matches", "code,party,qty,amount,note", "Receipt enters invoice matching."),
        E("purchase_orders", "three_way_match", "matches", "code,party,qty,amount,note", "Purchase order enters invoice matching."),
        E("purchase_orders", "purchases", "invoices", "code,party,name,qty,amount,note", "Purchase order converts to a vendor invoice."),
        E("ap_purchase_orders", "ap_invoice_journal", "invoices", "code,party,name,qty,amount,note", "Purchase order feeds the invoice journal."),
        E("ap_invoice_register", "ap_invoice_approval", "approves", "code,party,amount,note", "Registered invoice waits for approval."),
        E("ap_invoice_approval", "ap_invoice_journal", "posts", "code,party,amount,note", "Approved invoice posts from the journal."),
        E("ap_invoice_journal", "ap_invoice_matching", "matches", "code,party,qty,amount,note", "Invoice journal line is matched."),
        E("purchases", "ap_payment_journal", "pays", "code,party,amount,note", "Vendor invoice is paid."),
        E("ap_invoice_journal", "ap_payment_proposal", "pays", "code,party,amount,note", "Open invoices enter a payment proposal."),
        E("ap_payment_proposal", "ap_payment_journal", "pays", "code,party,amount,note", "Proposal becomes a payment journal."),
        E("ap_payment_journal", "ap_settled_invoices", "settles", "code,party,amount,note", "Payment settles the vendor invoice."),
        E("proc_import_orders", "lc_voyages", "creates", "code,party,name,qty,amount,note", "Import purchase order attaches to a voyage."),
        E("lc_voyages", "lc_voyage_lines", "creates", "code,party,name,qty,amount,note", "Voyage carries purchase lines."),
        E("lc_voyage_lines", "lc_voyage_costing", "costs", "code,party,qty,amount,note", "Voyage lines receive landed cost."),
        E("sm_opportunities", "sm_quotations", "creates", "party,name,amount,note", "Opportunity becomes a sales quotation."),
        E("sm_quotations", "sm_all_orders", "creates", "party,name,qty,amount,note", "Quotation confirms to a sales order."),
        E("sm_all_orders", "sm_packing", "ships", "code,party,name,qty,amount,note", "Sales order posts a packing slip."),
        E("sm_packing", "sm_invoice", "invoices", "code,party,name,qty,amount,note", "Shipped order is invoiced."),
        E("sm_all_orders", "sm_invoice", "invoices", "code,party,name,qty,amount,note", "Sales order converts to an invoice."),
        E("ar_sales_orders", "ar_invoice_journal", "invoices", "code,party,name,qty,amount,note", "Sales order feeds the invoice journal."),
        E("ar_free_text_invoices", "ar_open_invoices", "posts", "code,party,amount,note", "Free text invoice posts to open invoices."),
        E("sm_invoice", "ar_payment_journal", "pays", "code,party,amount,note", "Customer invoice is paid."),
        E("ar_open_invoices", "ar_payment_journal", "pays", "code,party,amount,note", "Open invoice is paid."),
        E("ar_payment_journal", "ar_settled_invoices", "settles", "code,party,amount,note", "Payment settles the customer invoice."),
        E("ar_open_invoices", "ar_collections_workspace", "collects", "party,amount,note", "Past-due invoice opens collections."),
        E("ar_invoices_past_due", "cc_collection_cases", "collects", "party,amount,note", "Past-due invoice opens a collection case."),
        E("cc_collection_cases", "cc_collection_letters", "creates", "party,amount,note", "Collection case prints a letter."),
        E("inv_onhand", "inv_reservation", "reserves", "name,qty,note", "On-hand is reserved against demand."),
        E("inv_onhand", "inv_transfer", "transfers", "name,qty,note", "On-hand moves on a transfer."),
        E("inv_onhand", "inv_counting", "creates", "name,qty,note", "On-hand is counted."),
        E("sm_packing", "wh_work_plan", "creates", "code,party,name,qty,note", "Packing slip creates warehouse work."),
        E("proc_receipts", "wh_work_plan", "creates", "code,party,name,qty,note", "Product receipt creates inbound work."),
        E("wh_work_plan", "wh_waved_loads", "creates", "code,name,qty,note", "Work planning builds a wave."),
        E("wh_waved_loads", "wh_work_inq", "creates", "code,name,qty,note", "Wave creates warehouse work."),
        E("planned_orders", "purchase_orders", "firms", "party,name,qty,amount,note", "Planned purchase firms to a purchase order."),
        E("planned_orders", "prd_planned", "firms", "name,qty,note", "Planned production firms to a production order."),
        E("prd_planned", "prd_all_orders", "firms", "name,qty,note", "Planned production becomes an order."),
        E("prd_all_orders", "prd_release", "posts", "code,name,qty,note", "Production order is released."),
        E("prd_release", "prd_start", "posts", "code,name,qty,note", "Released order is started."),
        E("prd_start", "prd_picking_list", "creates", "code,name,qty,note", "Started order issues a picking list."),
        E("prd_picking_list", "prd_raf", "posts", "code,name,qty,note", "Picked order reports as finished."),
        E("prd_raf", "prd_end", "posts", "code,name,qty,note", "Finished order is ended."),
        E("prd_end", "prd_cost_est", "costs", "code,name,qty,amount,note", "Ended order is costed."),
        E("gl_journals", "gl_trial_balance", "posts", "code,period,amount,note", "Posted journal updates the trial balance."),
        E("tax_sales_codes", "tax_sales_groups", "creates", "code,name,note", "Tax codes join a sales tax group."),
        E("tax_sales_groups", "tax_settle_post", "posts", "code,period,amount,note", "Tax groups settle in a period."),
        E("time_off_requests", "leave_accrue", "accrues", "worker,kind,qty,note", "Approved time off is accrued."),
        E("leave_plans", "leave_employees", "creates", "code,name,note", "Plan enrolls employees."),
        E("pay_attendance", "pay_timesheet", "creates", "worker,period,qty,note", "Attendance feeds the time sheet."),
        E("pay_timesheet", "pay_generate_cycle", "creates", "worker,period,qty,amount,note", "Time sheet enters the pay cycle."),
        E("tna_approve", "tna_transfer_pay", "transfers", "worker,period,qty,note", "Approved time transfers to pay."),
        E("tna_transfer_pay", "pay_generate_cycle", "transfers", "worker,period,qty,amount,note", "Transferred time enters the pay cycle."),
        E("pay_generate_cycle", "pay_validate_cycle", "approves", "period,note", "Generated cycle is validated."),
        E("pay_validate_cycle", "pay_payment_stmt", "pays", "worker,period,amount,note", "Validated cycle creates payment statements."),
        E("pay_payment_stmt", "pay_wps", "posts", "period,amount,note", "Payment statements file WPS."),
        E("pma_quotations", "pma_all_projects", "creates", "party,name,amount,note", "Project quotation becomes a project."),
        E("pma_all_projects", "pma_contracts", "creates", "party,name,amount,note", "Project carries a billing contract."),
        E("pma_all_projects", "pma_timesheets", "creates", "worker,name,period,qty,note", "Project captures timesheets."),
        E("pma_timesheets", "pma_invoice_prop", "creates", "party,name,qty,amount,note", "Posted time becomes an invoice proposal."),
        E("pma_invoice_prop", "pma_invoices", "invoices", "party,name,amount,note", "Invoice proposal posts a project invoice."),
        E("pma_all_projects", "pma_purch_orders", "creates", "party,name,qty,amount,note", "Project raises a purchase order."),
        E("pma_all_projects", "pma_sales_orders", "creates", "party,name,qty,amount,note", "Project raises a sales order."),
        E("fa_assets", "fa_acq_proposal", "creates", "code,name,amount,note", "Asset enters an acquisition proposal."),
        E("fa_acq_proposal", "fa_journal", "posts", "code,name,amount,note", "Acquisition posts from the journal."),
        E("fa_assets", "fa_depr_proposal", "creates", "code,name,amount,period,note", "Asset enters a depreciation proposal."),
        E("fa_depr_proposal", "fa_journal", "posts", "code,name,amount,period,note", "Depreciation posts from the journal."),
        E("fa_journal", "fa_balances", "posts", "code,name,amount,note", "Posted journal updates asset balances."),
        E("cb_bank_accounts", "cb_bank_transfers", "transfers", "code,amount,note", "Bank account transfers cash."),
        E("cb_bank_accounts", "cb_bank_statements", "creates", "code,period,note", "Bank account receives a statement."),
        E("cb_bank_statements", "cb_bank_recon", "matches", "code,period,amount,note", "Statement is reconciled."),
        E("bud_plans", "bud_register_entries", "creates", "code,name,period,amount,note", "Budget plan posts to the register."),
        E("bud_register_entries", "bud_actual_vs_budget", "posts", "code,period,amount,note", "Register is compared to actual."),
        E("exp_my_reports", "ap_payment_journal", "pays", "worker,amount,note", "Approved expense is reimbursed."),
        E("hr_workers", "leave_employees", "creates", "worker,name,note", "Worker enrolls on a leave plan."),
        E("hr_workers", "pay_attendance", "creates", "worker,note", "Worker attendance feeds payroll."),
        E("legal_entities", "org_number_sequences", "creates", "code,name,note", "Legal entity owns number sequences."),
        E("org_gab", "ps_vendors", "creates", "party,name,note", "Address-book party becomes a vendor."),
        E("org_gab", "sm_all_customers", "creates", "party,name,note", "Address-book party becomes a customer."),
        E("org_gab", "hr_workers", "creates", "worker,name,note", "Address-book party becomes a worker."),
    ];

    private static IReadOnlyList<ParameterDef> BuildParameters() =>
    [
        P("purchasing", "accept_overdelivery", "Accept overdelivery", "yes", "yesno", "Delivery", ["purchase_orders", "proc_receipts", "ps_parameters"]),
        P("purchasing", "accept_underdelivery", "Accept underdelivery", "yes", "yesno", "Delivery", ["purchase_orders", "proc_receipts", "ps_parameters"]),
        P("purchasing", "delivery_date_control", "Delivery date control", "none", "list", "Delivery", ["purchase_orders", "ps_parameters"], ["none", "sales", "purchase"]),
        P("purchasing", "rfq_reply_days", "Request for quotation reply days", "7", "number", "RFQ", ["rfq", "ps_rfq_journals", "proc_rfq_followup", "ps_parameters"]),
        P("purchasing", "activate_change_management", "Activate change management", "no", "yesno", "Purchase orders", ["purchase_orders", "proc_all_pos", "ps_parameters"]),
        P("ap", "invoice_matching", "Invoice matching", "three_way", "list", "Invoices", ["ap_invoice_matching", "three_way_match", "ap_matching_setup"], ["none", "two_way", "three_way"]),
        P("ap", "check_for_invoice_used", "Check for invoice used", "yes", "yesno", "Invoices", ["ap_invoice_journal", "purchases", "ap_matching_setup"]),
        P("ap", "settlement", "Settlement", "automatic", "list", "Payments", ["ap_payment_journal", "ap_settled_invoices", "ap_auto_settlement"], ["manual", "automatic"]),
        P("ar", "credit_limit_type", "Credit limit type", "balance", "list", "Credit", ["ar_all_customers", "sm_all_orders", "cc_parameters"], ["none", "balance", "balance_plus_packing_slip", "balance_plus_all"]),
        P("ar", "credit_limit_check", "Credit limit check", "error", "list", "Credit", ["sm_all_orders", "ar_sales_orders", "cc_blocking_rules"], ["none", "warning", "error"]),
        P("ar", "mandatory_payment_terms", "Mandatory payment terms", "yes", "yesno", "Payments", ["ar_terms_payment", "ar_invoice_journal", "sm_all_orders"]),
        P("ar", "settlement", "Settlement", "automatic", "list", "Payments", ["ar_payment_journal", "ar_settled_invoices", "ar_auto_settlement"], ["manual", "automatic"]),
        P("sales", "credit_limit_check", "Credit limit check", "error", "list", "Orders", ["sm_all_orders", "sm_quotations", "sm_parameters"], ["none", "warning", "error"]),
        P("sales", "accept_overdelivery", "Accept overdelivery", "yes", "yesno", "Delivery", ["sm_all_orders", "sm_packing", "sm_parameters"]),
        P("sales", "accept_underdelivery", "Accept underdelivery", "yes", "yesno", "Delivery", ["sm_all_orders", "sm_packing", "sm_parameters"]),
        P("sales", "delivery_date_control", "Delivery date control", "none", "list", "Delivery", ["sm_all_orders", "sm_parameters"], ["none", "sales"]),
        P("gl", "accounting_currency", "Accounting currency", "AED", "text", "Ledger", ["gl_parameters", "gl_journals", "gl_trial_balance"]),
        P("gl", "fiscal_calendar", "Fiscal calendar", "standard", "text", "Ledger", ["gl_parameters", "gl_journals"]),
        P("gl", "must_be_balanced", "Must be balanced", "yes", "yesno", "Journals", ["gl_parameters", "gl_journals"]),
        P("gl", "default_posting_layer", "Default posting layer", "current", "list", "Journals", ["gl_parameters", "gl_journals", "gl_trial_balance"], ["current", "operations", "tax"]),
        P("inventory_mgmt", "reservation", "Reservation", "automatic", "list", "Reservation", ["inv_parameters", "inv_reservation", "inv_onhand"], ["manual", "automatic", "explosion"]),
        P("inventory_mgmt", "warehouse_mandatory", "Warehouse mandatory", "yes", "yesno", "Warehouse", ["inv_parameters", "inv_onhand", "inv_transfer"]),
        P("inventory_mgmt", "negative_inventory", "Negative inventory", "no", "yesno", "On-hand", ["inv_parameters", "inv_onhand"]),
        P("warehouse", "work_creation_method", "Work creation method", "process", "list", "Work", ["wh_parameters", "wh_work_templates", "wh_work_plan"], ["process", "manual"]),
        P("warehouse", "wave_processing", "Wave processing", "automatic", "list", "Waves", ["wh_parameters", "wh_wave_templates", "wh_waved_loads"], ["manual", "automatic"]),
        P("production", "reservation", "Reservation", "automatic", "list", "Material", ["prd_parameters", "prd_all_orders", "prd_picking_list"], ["manual", "automatic", "explosion"]),
        P("production", "automatic_bom_consumption", "Automatic BOM consumption", "yes", "yesno", "Material", ["prd_parameters", "prd_start", "prd_raf"]),
        P("tax", "inclusive_sales_tax", "Inclusive sales tax", "no", "yesno", "Calculation", ["tax_calc_params", "tax_sales_codes", "sm_invoice", "purchases"]),
        P("tax", "sales_tax_calculation", "Sales tax calculation", "line", "list", "Calculation", ["tax_calc_params", "tax_sales_codes"], ["line", "total"]),
        P("payroll_area", "pay_cycle", "Pay cycle", "monthly", "list", "Cycle", ["pay_parameters", "pay_generate_cycle"], ["weekly", "biweekly", "monthly"]),
        P("payroll_area", "payment_method", "Payment method", "bank", "list", "Payment", ["pay_parameters", "pay_payment_stmt", "pay_wps"], ["bank", "cheque", "cash"]),
        P("leave_abs", "accrual_frequency", "Accrual frequency", "monthly", "list", "Accrual", ["leave_parameters", "leave_plans", "leave_accrue"], ["pay_period", "monthly", "anniversary"]),
        P("leave_abs", "request_approval", "Request approval", "yes", "yesno", "Requests", ["leave_parameters", "time_off_requests"]),
        P("master_planning_area", "firming", "Firming", "manual", "list", "Planned orders", ["mp_parameters", "planned_orders"], ["manual", "automatic"]),
        P("expense", "report_approval", "Report approval", "yes", "yesno", "Reports", ["exp_parameters", "exp_my_reports"]),
        P("landed_cost_area", "costing_method", "Costing method", "actual", "list", "Voyages", ["lc_parameters", "lc_voyage_costing"], ["estimated", "actual"]),
        P("fixed_assets", "depreciation_convention", "Depreciation convention", "full_month", "list", "Books", ["fa_parameters", "fa_depr_proposal", "fa_depr_profiles"], ["full_month", "half_year", "mid_month"]),
        P("budgeting", "over_budget_permission", "Over budget permission", "prevent", "list", "Control", ["bud_parameters", "bud_control_config"], ["prevent", "warn", "allow"]),
        P("banking", "reconciliation_matching", "Reconciliation matching", "automatic", "list", "Statements", ["cb_parameters", "cb_bank_recon"], ["manual", "automatic"]),
        P("credit_coll", "credit_limit_type", "Credit limit type", "balance", "list", "Credit", ["cc_parameters", "cc_blocking_rules"], ["none", "balance", "balance_plus_packing_slip", "balance_plus_all"]),
        P("people", "position_required", "Position required", "yes", "yesno", "Workers", ["hr_parameters", "hr_workers"]),
    ];

    private static ProcessLane Lane(
        string code,
        string label,
        string area,
        string purpose,
        string setupStyle,
        string configWay,
        params ProcessStep[] steps)
        => new(code, label, area, purpose, setupStyle, configWay, steps);

    private static ProcessStep S(string tab, Stage stage, string role) => new(tab, stage, role);

    private static InterlinkEdge E(string from, string to, string relation, string fields, string purpose)
        => new(from, to, relation, fields, purpose);

    private static ParameterDef P(
        string area,
        string key,
        string caption,
        string @default,
        string kind,
        string group,
        string[] affects,
        string[]? options = null)
        => new(area, key, caption, @default, kind, group, affects, options);
}
