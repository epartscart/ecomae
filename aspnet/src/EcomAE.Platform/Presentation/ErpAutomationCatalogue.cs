namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_erp_automation_catalogue</c> + workflow templates — presentation only (activate/tick stay PHP).</summary>
public static class ErpAutomationCatalogue
{
    public sealed record Item(
        string Id,
        string Category,
        string Name,
        string Icon,
        string Description,
        IReadOnlyList<string> Pipeline,
        string Tab,
        bool DefaultOn);

    public sealed record Template(
        string Id,
        string Name,
        string Description,
        string TriggerType,
        int StepCount);

    public static IReadOnlyList<Item> All { get; } =
    [
        new("order_to_erp", "accounting", "Order → ERP posting", "fa-shopping-cart",
            "When an order is placed: create AR invoice, post GL journal, deduct inventory, calculate VAT.",
            ["Validate", "AR Invoice", "GL Journal", "Inventory", "VAT", "Notify"], "receivables", true),
        new("period_close", "accounting", "Period close checklist", "fa-lock",
            "Soft-close → lock fiscal periods; block backdated journals after month-end.",
            ["Checklist", "Soft close", "Lock period", "Fiscal lock"], "year_end", true),
        new("year_end_close", "accounting", "Year-end P&L close", "fa-calendar-check-o",
            "Close revenue/expense to retained earnings and roll opening balances.",
            ["Trial balance", "P&L close", "RE posting", "Open next year"], "year_end", true),
        new("bank_recon", "accounting", "Bank reconciliation assist", "fa-university",
            "Import bank CSV, match statement lines to ledger, flag unmatched.",
            ["Import CSV", "Match lines", "Unmatched queue", "Reconcile"], "bank_recon", true),
        new("collections_dunning", "accounting", "Collections & dunning", "fa-gavel",
            "7-step overdue reminder sequence with escalation and credit holds.",
            ["Aging scan", "Day 7", "Day 30", "Day 60", "Day 90", "Escalate", "Hold"], "collections", true),
        new("report_scheduler", "accounting", "Report scheduler", "fa-clock-o",
            "Daily/weekly/monthly financial reports emailed to recipients.",
            ["Schedule", "Generate", "Format", "Email"], "report_scheduler", true),
        new("vat_reminder", "accounting", "VAT filing reminder", "fa-percent",
            "Remind tax owners 7 days before VAT return deadline.",
            ["Calendar", "7-day alert", "Notify", "Open return"], "vat_return", false),
        new("gl_auto_post", "accounting", "Document → GL auto-post", "fa-book",
            "Balanced journals from invoices, payments, receipts and stock moves.",
            ["Document", "Validate", "Balance check", "Post GL"], "gl", true),
        new("payment_reminder", "accounting", "AP payment due reminder", "fa-credit-card",
            "Alert treasury when supplier invoices approach due date.",
            ["Scan AP due", "Notify treasury", "Payment batch"], "payables", false),
        new("depreciation_run", "accounting", "Fixed-asset depreciation", "fa-building",
            "Monthly depreciation schedules posting to GL.",
            ["Schedule", "Compute", "Post GL", "Register update"], "fixed_assets", true),
        new("po_approval", "process", "PO approval chain", "fa-check-circle",
            "Manager → Finance → Director thresholds; auto-approve small POs.",
            ["PO created", "Amount check", "Manager", "Finance", "Director", "Approved"], "approvals", true),
        new("invoice_autosend", "process", "Invoice auto-send", "fa-paper-plane",
            "Email tax invoices to customers when order/invoice is completed.",
            ["Invoice posted", "Template", "Email", "Log"], "invoices", false),
        new("low_stock_alert", "process", "Low stock alert", "fa-exclamation-triangle",
            "Notify procurement and create reorder tasks below ROP.",
            ["Stock event", "Below ROP?", "Notify", "Create task"], "order_planning", false),
        new("employee_onboarding", "process", "Employee onboarding", "fa-user-plus",
            "Create onboarding tasks when a new employee record is created.",
            ["Hire", "IT setup", "HR docs", "Manager intro", "Done"], "hr", false),
        new("daily_sales_summary", "process", "Daily sales summary", "fa-bar-chart",
            "Email daily sales KPIs to management at end of day.",
            ["Schedule 18:00", "Aggregate", "Email"], "report_scheduler", false),
        new("aml_alert", "process", "AML compliance alert", "fa-shield",
            "Flag high-value transactions for compliance review.",
            ["Txn event", "Threshold", "Flag case", "Notify compliance"], "aml_compliance", true),
        new("process_flow_routing", "process", "Process flow routing", "fa-sitemap",
            "GPS-style chained task routing across departments with SLA tracking.",
            ["Start case", "Route step", "SLA", "Approve", "Next", "Done"], "processflow", true),
        new("three_way_match", "process", "3-way match (PO/GRN/Bill)", "fa-exchange",
            "Match purchase order, goods receipt and supplier bill before payment.",
            ["PO", "GRN", "Bill", "Match", "Pay"], "three_way_match", true),
        new("subscription_billing", "process", "Subscription recurring billing", "fa-refresh",
            "Auto-generate recurring invoices on billing cycles.",
            ["Cycle due", "Invoice", "Collect", "Renew"], "subscriptions", true),
        new("rma_warranty", "process", "Warranty / RMA workflow", "fa-wrench",
            "RMA request → approve → receive → inspect → refund/replace.",
            ["Request", "Approve", "Receive", "Inspect", "Resolve"], "aftersales", true),
        new("credit_check", "process", "Credit limit gate", "fa-ban",
            "Block or warn when order would exceed customer credit limit.",
            ["Order", "Credit check", "Allow/Hold", "Notify"], "collections", true),
        new("goods_receipt_notify", "process", "Goods receipt notify", "fa-truck",
            "Notify buyer and AP when GRN is posted against a PO.",
            ["GRN posted", "Notify buyer", "Notify AP"], "purchase_orders", false),
    ];

    public static IReadOnlyList<Template> Templates { get; } =
    [
        new("po_approval_chain", "PO Approval Chain", "Route POs through manager → finance → director by amount", "event", 3),
        new("invoice_auto_send", "Invoice Auto-Send", "Email invoice when order completes", "event", 2),
        new("low_stock_alert", "Low Stock Alert", "Notify procurement below reorder point", "event", 2),
        new("vat_filing_reminder", "VAT Filing Reminder", "Remind 7 days before VAT deadline", "schedule", 2),
        new("overdue_escalation", "Overdue Invoice Escalation", "Dunning sequence at 30/60/90 days", "schedule", 3),
        new("employee_onboarding", "Employee Onboarding", "Onboarding tasks for new hires", "event", 3),
        new("daily_sales_summary", "Daily Sales Summary", "Email daily sales to management", "schedule", 1),
        new("aml_compliance_alert", "AML Compliance Alert", "Flag transactions above AML threshold", "event", 2),
    ];

    public static IEnumerable<Item> Accounting => All.Where(i => i.Category == "accounting");
    public static IEnumerable<Item> Processes => All.Where(i => i.Category == "process");

    public static string OpenHref(Item item) => ErpPhpTabRouteMap.MapTabOrModuleApp(item.Tab);
}
