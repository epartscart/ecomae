namespace EcomAE.Platform.Presentation;

/// <summary>
/// Per-module study used by organization and system administrators:
/// functionality, setup style, configuration layers, process flow,
/// and the customization points already present on the workspace.
/// </summary>
public static class ErpD365ModuleStudyCatalog
{
    public const string StudySavePath = "/erp/d365-study/save";

    public static readonly IReadOnlyList<string> Topics =
    [
        "functionality",
        "setup",
        "configuration",
        "process",
        "customization",
    ];

    public sealed record ModuleStudy(
        string Area,
        string Label,
        string Audience,
        string Functionality,
        IReadOnlyList<string> SetupChecklist,
        IReadOnlyList<string> ConfigLayers,
        IReadOnlyList<string> ProcessCodes,
        IReadOnlyList<string> CustomizationAspects,
        IReadOnlyList<string> DedicatedHrefs);

    public static readonly IReadOnlyList<ModuleStudy> All = Build();

    public static bool TryGet(string? area, out ModuleStudy study)
    {
        study = All.FirstOrDefault(s => s.Area.Equals((area ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))!;
        return study is not null;
    }

    public static IReadOnlyList<ModuleStudy> ForAudience(string? area)
    {
        if (string.Equals(area, "setup", StringComparison.OrdinalIgnoreCase))
        {
            return All;
        }

        if (string.Equals(area, "enterprise", StringComparison.OrdinalIgnoreCase))
        {
            return All.Where(s => s.Audience is "organization" or "both").ToList();
        }

        if (TryGet(area, out var study))
        {
            return [study];
        }

        return [];
    }

    public static bool IsOfficialTopic(string? topic)
        => Topics.Contains((topic ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<ModuleStudy> Build() =>
    [
        M("purchasing", "Procurement and sourcing", "both",
            "Buy goods and services. Raise a requisition, invite vendors on an RFQ, award a purchase order, receive the goods, match the vendor invoice, and pay. Vendor catalogs, trade agreements, and purchase agreements set price and quantity before the order.",
            [
                "Open Procurement and sourcing parameters (overdelivery, underdelivery, delivery date control, RFQ reply days, change management).",
                "Create number sequences for requisition, RFQ, and purchase order.",
                "Set purchase workflows for requisition, RFQ, and purchase-order approval.",
                "Create vendor groups, then vendors from the global address book.",
                "Load catalogs, trade agreements, and purchase agreements before the first order.",
            ],
            [
                "Company: procurement parameters and number sequences.",
                "Group: vendor group posting, terms, and price group.",
                "Document: purchase-order header overdelivery, delivery date, and vendor.",
            ],
            ["procure_to_pay", "landed_cost"],
            [
                "Company parameters on this hub (official captions only).",
                "Vendor master and AP setup on the dedicated vendor form.",
                "RFQ, purchase order, receipt, and three-way match stay on dedicated forms.",
                "Follow-next copies party, item, qty, amount, and note onto the next document.",
            ],
            ["/erp/rfq-app", "/erp/purchase-orders-app", "/erp/purchase-requests-app", "/erp/purchases-app", "/erp/three-way-match-app", "/erp/suppliers-app"]),
        M("ap", "Accounts payable", "both",
            "Record vendor invoices, match them to the purchase order and receipt, approve payment, and settle. Invoice register holds a bill before approval. Payment proposal builds the payment journal from open invoices and terms.",
            [
                "Open invoice matching setup (two-way or three-way) and Check for invoice used.",
                "Set vendor posting profiles and terms of payment.",
                "Create vendor groups, then vendors.",
                "Turn on automatic settlement only after posting profiles are correct.",
            ],
            [
                "Company: matching policy and settlement.",
                "Group: vendor posting profile and cash discount.",
                "Document: invoice header match and payment method.",
            ],
            ["accounts_payable", "procure_to_pay"],
            [
                "Invoice matching and settlement parameters on this hub.",
                "Vendor invoice and payment stay on payables and payment-batch forms.",
                "Settlement writes the same vendor, amount, and invoice reference forward.",
            ],
            ["/erp/payables-app", "/erp/payment-batches-app", "/erp/three-way-match-app", "/erp/aging-app?tab=ap", "/erp/suppliers-app"]),
        M("sales", "Sales and marketing", "both",
            "Qualify an opportunity, confirm a quotation, ship a sales order, invoice, and collect. Credit limit type and credit limit check run before confirm. Packing slip / delivery note is the ship document; invoice follows it.",
            [
                "Open Sales and marketing parameters (credit limit check, overdelivery, underdelivery, delivery date control).",
                "Create customers from the global address book and assign a customer group.",
                "Set sales workflows for quotation and order approval.",
                "Confirm price/discount (trade agreements) before the first quotation.",
            ],
            [
                "Company: sales parameters and credit limit check.",
                "Group: customer group terms, discount, and credit.",
                "Document: sales-order header delivery and credit override.",
            ],
            ["order_to_cash"],
            [
                "Sales parameters on this hub.",
                "Opportunity, quotation, sales order, packing slip, and invoice stay dedicated.",
                "Follow-next copies party, item, qty, and amount into the next sales document.",
            ],
            ["/erp/crm-opportunities-app", "/erp/sales-quotations-app", "/erp/sales-orders-app", "/erp/delivery-notes-app", "/erp/invoices-app", "/erp/contacts-app"]),
        M("ar", "Accounts receivable", "both",
            "Invoice customers (from a sales order or a free-text invoice), collect payment, and settle. Terms of payment set the due date. Automatic settlement matches a payment to open invoices. Past-due balances open collections.",
            [
                "Set customer posting profiles, terms of payment, and cash discounts.",
                "Set credit limit type and whether payment terms are mandatory.",
                "Create customers and customer groups.",
                "Choose manual or automatic settlement before the first payment journal.",
            ],
            [
                "Company: credit limit type, settlement, and mandatory terms.",
                "Group: customer group posting profile and terms.",
                "Document: invoice due date and payment journal settlement.",
            ],
            ["accounts_receivable", "order_to_cash", "credit_collections"],
            [
                "Receivable parameters on this hub and on credit and collections.",
                "Free-text invoice, open invoices, and payment stay on dedicated forms.",
                "Settlement carries customer, amount, and invoice reference.",
            ],
            ["/erp/receivables-app", "/erp/invoices-app", "/erp/aging-app?tab=ar", "/erp/contacts-app"]),
        M("gl", "General ledger", "both",
            "Hold the chart of accounts, enter general journals, require them to balance, post, and inquire the trial balance. Accounting currency and fiscal calendar are company-level. Posting layer (current, operations, tax) is on the journal.",
            [
                "Set ledger parameters: accounting currency, fiscal calendar, must be balanced, default posting layer.",
                "Confirm the chart of accounts and financial dimensions before journals.",
                "Create journal names, then enter the first general journal.",
                "Post only when debit equals credit if must be balanced is yes.",
                "Use the same journal for periodic lines, allocation rules, and a reversing provision.",
                "Reverse a posted journal from the correction entry, then read the trial balance by account and dimension.",
            ],
            [
                "Company: ledger parameters and fiscal calendar.",
                "Journal name: default voucher and posting layer.",
                "Voucher: account, dimension, and posting layer override.",
            ],
            ["general_ledger"],
            [
                "Ledger parameters on this hub.",
                "General journal and trial balance stay on dedicated forms.",
                "Posted journals update trial-balance opening, debit, credit, and closing.",
            ],
            ["/erp/coa-accounts-app", "/erp/gl-journals-app", "/erp/gl-journals/reverse", "/erp/trial-balance-app", "/erp/period-close-app"]),
        M("inventory_mgmt", "Inventory management", "both",
            "Track on-hand by item, warehouse, and batch. Reserve demand, transfer between warehouses, and count. Reservation hierarchy decides how on-hand is reserved. Warehouse mandatory and negative inventory are company switches.",
            [
                "Open inventory parameters: reservation, warehouse mandatory, negative inventory.",
                "Set the reservation hierarchy.",
                "Create warehouses and item-warehouse coverage.",
                "Use on-hand inquiry before the first transfer or count.",
            ],
            [
                "Company: reservation and warehouse mandatory.",
                "Warehouse: default receipt and issue locations.",
                "Journal line: warehouse, batch, and quantity.",
            ],
            ["inventory"],
            [
                "Inventory parameters on this hub.",
                "On-hand, transfer, and warehouse stay on dedicated stock forms.",
                "Reservation and transfer copy item and qty only.",
            ],
            ["/erp/inventory-stock-app", "/erp/stock-transfers-app", "/erp/warehouses-app"]),
        M("warehouse", "Warehouse management", "both",
            "Plan inbound and outbound work, build waves from loads, pick, pack, and ship. Work templates create work; wave templates decide how a wave is built. Work creation method and wave processing are company parameters.",
            [
                "Open warehouse management parameters (work creation method, wave processing).",
                "Create work classes, work templates, and wave templates.",
                "Map location directives before the first wave.",
                "Use work planning to build the first waved load.",
            ],
            [
                "Company: work creation and wave processing.",
                "Template: wave methods and work class.",
                "Load: wave override and work inquiry.",
            ],
            ["warehouse"],
            [
                "Warehouse parameters on this hub.",
                "Work planning stays on the dedicated warehouse form.",
                "Packing slip and product receipt create warehouse work through official edges.",
            ],
            ["/erp/warehouse-wms-app"]),
        M("production", "Production control", "both",
            "Firm a planned production order, release it, start it, pick material, report as finished, end it, and cost it. Reservation and automatic BOM consumption are company parameters. Route and BOM stay on the product.",
            [
                "Open production control parameters (reservation, automatic BOM consumption).",
                "Confirm BOM, route, and working calendars.",
                "Set production units and pools.",
                "Firm planned orders only after coverage is correct.",
            ],
            [
                "Company: reservation and BOM consumption.",
                "Production unit: calendar and resource.",
                "Order: item, qty, and status (planned → released → started → finished).",
            ],
            ["production", "master_planning"],
            [
                "Production parameters on this hub.",
                "All production orders stay on the dedicated production form.",
                "Follow-next copies item, qty, and code through release, start, pick, and finish.",
            ],
            ["/erp/production-overview-app", "/erp/quality-app"]),
        M("master_planning_area", "Master planning", "organization",
            "Cover demand with planned purchase and production orders. Coverage groups and item coverage set warehouse, lead time, and method. Firming can be manual or automatic. Firming a planned order creates a purchase order or a production order.",
            [
                "Open master planning parameters and firming.",
                "Create coverage groups, then item coverage per warehouse.",
                "Create the master plan and forecast plan.",
                "Regenerate planned orders, then firm.",
            ],
            [
                "Company: planning parameters and firming.",
                "Coverage group: method and lead time.",
                "Item coverage: warehouse, min, and max.",
            ],
            ["master_planning"],
            [
                "Firming parameter on this hub.",
                "Planned orders firm to the dedicated purchase-order or production form.",
            ],
            ["/erp/order-planning-app?tab=master_planning", "/erp/purchase-orders-app", "/erp/production-overview-app"]),
        M("tax", "Tax", "both",
            "Define sales tax codes and groups, apply them on sales and purchase documents, then settle and post for a period. Inclusive sales tax and sales tax calculation (line or total) are company calculation parameters.",
            [
                "Set tax calculation parameters (inclusive sales tax, line or total).",
                "Create sales tax codes with rate, ledger, and settlement period.",
                "Create sales tax groups and item sales tax groups.",
                "Settle and post only after the period is closed for invoices.",
            ],
            [
                "Company: calculation method and inclusive tax.",
                "Code: rate, ledger, and settlement period.",
                "Document line: sales tax group override.",
            ],
            ["tax"],
            [
                "Tax calculation parameters on this hub.",
                "Sales tax codes and settle-and-post stay on the dedicated tax form.",
            ],
            ["/erp/vat-app", "/erp/withholding-app"]),
        M("payroll_area", "Payroll", "organization",
            "Collect attendance and time, generate a pay cycle, validate, pay, and file WPS. Pay cycle and payment method are company parameters. Grades and calendars sit under setup. Leave and time and attendance feed days and overtime.",
            [
                "Open payroll parameters (pay cycle, payment method).",
                "Create grades, calendars, and earning codes.",
                "Enroll workers, then import attendance or time sheets.",
                "Generate, validate, pay, then file WPS.",
                "Enter pay transactions for airfare, leave encashment, bonus, loans, and advances.",
                "Review salary, transfer the new scale, then process gratuity and end of service.",
                "Send the payslip after the calendar and public holidays are on the cycle.",
            ],
            [
                "Company: pay cycle and payment method.",
                "Grade: salary scale.",
                "Worker: earning, allowance, and bank override.",
            ],
            ["payroll", "time_attendance", "leave"],
            [
                "Payroll parameters on this hub.",
                "Transfer to pay from time and attendance uses the dedicated payroll form.",
                "Attendance, time sheet, cycle, payment statement, and WPS stay linked.",
            ],
            ["/erp/payroll-app", "/erp/staff-app", "/erp/performance-app"]),
        M("time_att", "Time and attendance", "organization",
            "Register time, approve it, and transfer approved time to payroll. Approval is the control step; transfer to pay writes hours into the next pay cycle.",
            [
                "Decide who approves registrations.",
                "Approve, then transfer to pay.",
                "Confirm the earning map on payroll before the first transfer.",
                "Load attendance, process the timesheet, and check missed login or logout.",
                "Approve overtime, post the overtime and short-time journals, and read the comp-off balance.",
            ],
            [
                "Company: approval required.",
                "Approver: queue of registrations.",
                "Transfer: earning code and pay cycle.",
            ],
            ["time_attendance", "payroll"],
            [
                "Approve on this hub; transfer opens the dedicated payroll form.",
            ],
            ["/erp/time-attendance-app", "/erp/payroll-app", "/erp/gl-journals-app"]),
        M("projects", "Project management and accounting", "both",
            "Quote a project, create the project and contract, assign resources, capture timesheets, propose an invoice, and invoice the customer. Item needs raise project purchase orders and sales orders. Posted transactions support profit and loss.",
            [
                "Create the project type and the billing contract first.",
                "Assign resources and a forecast.",
                "Capture timesheets, then build the invoice proposal.",
                "Invoice from the proposal; do not invoice timesheets directly.",
            ],
            [
                "Project type: default ledger and invoice method.",
                "Contract: billing rule and customer.",
                "Timesheet and proposal: hours, amount, and period.",
            ],
            ["projects"],
            [
                "Project, contract, sales order, purchase order, and invoice stay dedicated.",
                "Follow-next copies party, worker, period, qty, and amount.",
            ],
            ["/erp/projects-overview-app", "/erp/project-accounting-app", "/erp/sales-quotations-app", "/erp/invoices-app"]),
        M("fixed_assets", "Fixed assets", "both",
            "Register an asset, acquire it, depreciate it by book, and dispose of it. Books and depreciation profiles are setup. Depreciation convention is a company parameter. Journals post acquisition, depreciation, and disposal to the ledger.",
            [
                "Open fixed assets parameters and depreciation convention.",
                "Create books, depreciation profiles, and asset groups.",
                "Create the asset, then run an acquisition proposal.",
                "Run depreciation only after the book and profile are attached.",
            ],
            [
                "Company: journal names and depreciation convention.",
                "Group: default book and profile.",
                "Asset book: acquisition date and method override.",
            ],
            ["fixed_assets"],
            [
                "Fixed assets parameters on this hub.",
                "Asset register stays on the dedicated fixed-assets form.",
            ],
            ["/erp/fixed-assets-app"]),
        M("banking", "Cash and bank management", "both",
            "Hold company bank accounts, post transfers, import statements, and reconcile. Reconciliation matching can be manual or automatic. Balance control compares the bank subledger to the ledger.",
            [
                "Open cash and bank parameters and reconciliation matching.",
                "Create bank accounts and statement formats.",
                "Post cash, then import or enter the statement.",
                "Reconcile before the next period close.",
            ],
            [
                "Company: reconciliation matching and deposit defaults.",
                "Account: currency and ledger.",
                "Statement line: matching rule override.",
            ],
            ["cash_bank"],
            [
                "Bank parameters on this hub.",
                "Accounts, transfers, statements, and reconciliation stay dedicated.",
            ],
            ["/erp/cash-accounts-app", "/erp/cash-entries-app", "/erp/bank-reconciliation-app"]),
        M("budgeting", "Budgeting", "both",
            "Build a budget plan, post it to the budget register, and control spend. Over budget permission (prevent, warn, allow) is the company control. Actual versus budget is the inquiry after post.",
            [
                "Open budgeting parameters and over-budget permission.",
                "Create budget models, cycles, and allocation terms.",
                "Configure budget control and the source documents it checks.",
                "Generate a register entry from the adopted plan.",
                "Enter a recurring budget, allocate it to dimensions and to periods, then update the budget balance.",
            ],
            [
                "Company: model, cycle, and over-budget permission.",
                "Plan: organization and dimension.",
                "Document: budget check result.",
            ],
            ["budgeting"],
            [
                "Budget parameters on this hub.",
                "Plans and register entries stay on the dedicated budget form.",
            ],
            ["/erp/budgets-app"]),
        M("leave_abs", "Leave and absence", "organization",
            "Define leave types and plans, enroll workers, take time-off requests, approve them, and accrue. Accrual frequency and request approval are company parameters. Payroll consumes the same balances.",
            [
                "Open leave parameters (accrual frequency, request approval).",
                "Create leave types and plans (grant, carry-forward).",
                "Enroll employees, then open the company leave calendar.",
                "Approve requests before running accrue.",
                "Leave encashment is a pay transaction on payroll, using the same balance.",
            ],
            [
                "Company: accrual frequency and approval.",
                "Plan: grant units and carry-forward.",
                "Worker enrollment: remaining balance.",
            ],
            ["leave", "payroll", "human_resources"],
            [
                "Leave parameters on this hub.",
                "Employees open the dedicated staff form.",
                "Approved requests accrue and feed payroll leave.",
            ],
            ["/erp/leave-absence-app", "/erp/staff-app", "/erp/payroll-app"]),
        M("expense", "Expense management", "organization",
            "Workers enter expense reports and cash advances. Report approval is a company parameter. An approved report is reimbursed through the vendor payment journal.",
            [
                "Open expense parameters and report approval.",
                "Create expense categories and tax defaults.",
                "Workers enter reports; approvers release them.",
                "Reimburse from the payment journal.",
            ],
            [
                "Company: report approval and tax default.",
                "Category: ledger and tax.",
                "Report line: amount and receipt.",
            ],
            ["expense"],
            [
                "Expense parameters on this hub.",
                "Reports stay on the dedicated expense form; payment uses payables.",
            ],
            ["/erp/expense-reports-app", "/erp/payment-batches-app"]),
        M("landed_cost_area", "Landed cost", "both",
            "Attach inbound freight and duty to an import purchase order through a voyage. Costing method (estimated or actual) is a company parameter. Voyage lines receive the allocated cost.",
            [
                "Open landed cost parameters and costing method.",
                "Create voyage statuses and cost types.",
                "Attach import purchase-order lines to a voyage.",
                "Cost the voyage before the vendor invoice.",
            ],
            [
                "Company: costing method.",
                "Cost type: allocation basis.",
                "Voyage line: item, qty, and allocated amount.",
            ],
            ["landed_cost", "procure_to_pay"],
            [
                "Landed cost parameters on this hub.",
                "Voyages stay on the dedicated landed-cost form.",
            ],
            ["/erp/landed-cost-app", "/erp/purchase-orders-app"]),
        M("people", "Human resources", "organization",
            "Hire workers, require a position when the parameter is yes, and feed leave and payroll. Workers come from the global address book. Positions carry compensation that payroll grades consume.",
            [
                "Open human resources parameters (position required).",
                "Create positions and the position hierarchy.",
                "Create workers from the address book.",
                "Enroll workers on leave plans and payroll.",
                "Raise a manpower requisition, recruit, then open the employee master and contract.",
                "Set the reports-to position and record performance before payroll enrollment.",
            ],
            [
                "Company: position required.",
                "Position: compensation and manager.",
                "Worker: employment and assignment.",
            ],
            ["human_resources", "leave", "payroll"],
            [
                "HR parameters on this hub.",
                "Workers stay on the dedicated staff form.",
            ],
            ["/erp/hr-workspace-app", "/erp/hr-overview-app", "/erp/staff-app", "/erp/contracts-app", "/erp/performance-app", "/erp/leave-absence-app"]),
        M("credit_coll", "Credit and collections", "both",
            "Set credit limit type, block risky customers, open collection cases, and print collection letters. Aging drives the letter sequence. Credit limit check on sales uses the same type.",
            [
                "Open accounts receivable parameters (credit limit type).",
                "Set blocking rules and the collection letter sequence.",
                "Review aged balances before opening a case.",
                "Print letters from the case, not from the invoice list.",
            ],
            [
                "Company: credit limit type and letter sequence.",
                "Customer group: shared limit.",
                "Document: credit hold on the sales order.",
            ],
            ["credit_collections", "accounts_receivable"],
            [
                "Credit parameters on this hub.",
                "Customers, aging, and collections stay dedicated.",
            ],
            ["/erp/collections-dunning-app", "/erp/aging-app?tab=ar", "/erp/contacts-app"]),
        M("enterprise", "Organization administration", "organization",
            "Legal entities, number sequences, and the global address book. Every other module posts in a legal entity and numbers documents from a sequence. Parties in the address book become vendors, customers, and workers.",
            [
                "Create the legal entity (currency and fiscal calendar).",
                "Create number sequences and chronological groups.",
                "Load the global address book and address setup.",
                "Study every module on this hub, then customize parameters on the module that owns them.",
            ],
            [
                "Legal entity: currency, calendar, and address.",
                "Number sequence: scope and next value.",
                "Party: shared name and address used by all modules.",
            ],
            ["organization"],
            [
                "This hub is the organization-admin study surface for every module.",
                "Save a study note per topic (functionality, setup, configuration, process, customization).",
                "Legal entities stay on the dedicated organization form.",
            ],
            ["/erp/organization-app?tab=legal_entities", "/erp/contacts-app"]),
        M("setup", "System administration", "system",
            "Users, security roles, workflows, and company system parameters. System administrators study every module here, then grant roles so organization administrators can customize parameters and process notes.",
            [
                "Create users and assign security roles.",
                "Turn on the workflows each module needs.",
                "Set system parameters for the legal entity.",
                "Walk the process study for every module before go-live.",
            ],
            [
                "System: users, roles, and batch.",
                "Workflow: document type and approver.",
                "Company: system parameters inherited by modules.",
            ],
            ["organization"],
            [
                "This hub is the system-admin study surface for every module.",
                "Users and security stay on the dedicated user and tenant-config forms.",
                "Save study notes so the next administrator can rebuild the same way.",
            ],
            ["/erp/users-app", "/erp/tenant-config-app", "/erp/workflows-app", "/erp/approvals-app"]),
    ];

    private static ModuleStudy M(
        string area,
        string label,
        string audience,
        string functionality,
        string[] setup,
        string[] config,
        string[] processes,
        string[] custom,
        string[] hrefs)
        => new(area, label, audience, functionality, setup, config, processes, custom, hrefs);
}
