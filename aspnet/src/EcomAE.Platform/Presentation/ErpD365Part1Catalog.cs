namespace EcomAE.Platform.Presentation;

/// <summary>
/// Dynamics-style workspace pages taken from the Accounts payable, Accounts receivable,
/// Asset management, Audit workbench, Budgeting, Cash and bank, Common, Consolidations,
/// Cost accounting, Cost management, Credit and collections, Expense, Fixed assets,
/// General ledger, Human resources, Inventory, and Landed cost module menus.
/// Existing dedicated apps stay linked; missing pages land on the workspace writer.
/// </summary>
public static class ErpD365Part1Catalog
{
    public const string PayablesApp = "/erp/payables-workspace-app";
    public const string ReceivablesApp = "/erp/receivables-workspace-app";
    public const string AssetMgmtApp = "/erp/asset-management-app";
    public const string AuditApp = "/erp/audit-workbench-app";
    public const string BudgetingApp = "/erp/budgeting-workspace-app";
    public const string CashBankApp = "/erp/cash-bank-workspace-app";
    public const string CommonApp = "/erp/common-workspace-app";
    public const string ConsolidationsApp = "/erp/consolidations-workspace-app";
    public const string CostAcctApp = "/erp/cost-accounting-app";
    public const string CostMgmtApp = "/erp/cost-management-app";
    public const string CreditCollApp = "/erp/credit-collections-workspace-app";
    public const string ExpenseApp = "/erp/expense-workspace-app";
    public const string FixedAssetsApp = "/erp/fixed-assets-workspace-app";
    public const string GeneralLedgerApp = "/erp/general-ledger-workspace-app";
    public const string HrApp = "/erp/hr-workspace-app";
    public const string InventoryApp = "/erp/inventory-workspace-app";
    public const string LandedCostApp = "/erp/landed-cost-workspace-app";

    public static readonly IReadOnlyList<ErpD365Part3Catalog.Page> All = Build();

    public static IReadOnlyList<ErpD365Part3Catalog.Page> ForArea(string? area)
    {
        var key = (area ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return All;
        }

        return All.Where(p => p.Area.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static bool TryGet(string? tab, out ErpD365Part3Catalog.Page page)
    {
        page = All.FirstOrDefault(p => p.Tab.Equals((tab ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))!;
        return page is not null;
    }

    public static bool TryMapTab(string? tab, out string href)
    {
        href = string.Empty;
        if (!TryGet(tab, out var page))
        {
            return false;
        }

        href = page.ExistingHref ?? (page.AppPath + "?tab=" + Uri.EscapeDataString(page.Tab));
        return true;
    }

    public static string AppPathForArea(string? area)
        => (area ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "ap" => PayablesApp,
            "ar" => ReceivablesApp,
            "asset_mgmt" => AssetMgmtApp,
            "audit_wb" or "audit" => AuditApp,
            "budgeting" => BudgetingApp,
            "banking" => CashBankApp,
            "common" => CommonApp,
            "consolidations" => ConsolidationsApp,
            "cost_acct" => CostAcctApp,
            "cost_mgmt" => CostMgmtApp,
            "credit_coll" => CreditCollApp,
            "expense" => ExpenseApp,
            "fixed_assets" => FixedAssetsApp,
            "gl" or "finance" or "general_ledger" => GeneralLedgerApp,
            "people" => HrApp,
            "inventory_mgmt" or "inventory" => InventoryApp,
            "landed_cost_area" => LandedCostApp,
            _ => PayablesApp,
        };

    public static IReadOnlyList<PhpModuleCatalog.ModuleLink> AsModuleLinks()
        => All
            .Where(p => p.ExistingHref is null)
            .Select(p => new PhpModuleCatalog.ModuleLink(
                p.Area + "/" + p.Tab,
                p.Label,
                "/ERP/?epc_erp_shell=1&area=" + p.Area + "&tab=" + p.Tab,
                p.Icon,
                p.Area,
                p.Purpose,
                false,
                false,
                p.Group))
            .ToList();

    public static IReadOnlyList<string> RequiredLabels(string area)
        => ForArea(area).Select(p => p.Label).ToList();

    private static IReadOnlyList<ErpD365Part3Catalog.Page> Build()
    {
        var rows = new List<ErpD365Part3Catalog.Page>(600);

        rows.AddRange(PayablesPages());
        rows.AddRange(ReceivablesPages());
        rows.AddRange(CashBankPages());
        rows.AddRange(BudgetPages());
        rows.AddRange(AssetPages());
        rows.AddRange(AuditPages());
        rows.AddRange(CommonPages());
        rows.AddRange(ConsolidationPages());
        rows.AddRange(CostAcctPages());
        rows.AddRange(CostMgmtPages());
        rows.AddRange(CreditPages());
        rows.AddRange(ExpensePages());
        rows.AddRange(FixedAssetPages());
        rows.AddRange(GeneralLedgerPages());
        rows.AddRange(HrPages());
        rows.AddRange(InventoryPages());
        rows.AddRange(LandedCostPages());
        return rows;
    }

    private static ErpD365Part3Catalog.Page P(
        string area,
        string tab,
        string label,
        string group,
        string purpose,
        ErpD365Part3Catalog.FormKind kind,
        string app,
        string? existing = null,
        string icon = "fa-th-large")
        => new(area, tab, label, group, purpose, kind, app, existing, icon);

    private static IEnumerable<ErpD365Part3Catalog.Page> PayablesPages()
    {
        const string a = "ap";
        const string app = PayablesApp;
        yield return P(a, "ap_invoice_journal", "Invoice journal", "Invoices", "Enter vendor invoices in a journal before posting to payables.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/payables-app", "fa-book");
        yield return P(a, "ap_pending_invoices", "Pending vendor invoices", "Invoices", "Vendor invoices waiting for match, tax, or approval.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/payables-app", "fa-hourglass-half");
        yield return P(a, "ap_open_invoices", "Open vendor invoices", "Invoices", "Unsettled vendor invoices still on the payables ledger.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/payables-app", "fa-folder-open-o");
        yield return P(a, "ap_invoices_past_due", "Vendor invoices past due", "Invoices", "Open vendor invoices whose due date has passed.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/aging-app?tab=ap", "fa-exclamation-triangle");
        yield return P(a, "ap_invoice_entry", "Vendor invoice entry", "Invoices", "Key a vendor invoice header, lines, tax, and charges.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/payables-app", "fa-plus");
        yield return P(a, "ap_invoice_register", "Invoice register", "Invoices", "Register a vendor invoice before it is approved for payment.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-list-alt");
        yield return P(a, "ap_invoice_pool", "Invoice pool", "Invoices", "Hold registered invoices until they are approved.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-inbox");
        yield return P(a, "ap_invoice_approval", "Invoice approval", "Invoices", "Approve registered invoices so they can be paid.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "ap_invoice_templates", "Vendor invoice templates", "Invoices", "Reusable invoice headers for recurring vendor bills.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-copy");
        yield return P(a, "ap_recurring_invoices", "Recurring invoices", "Invoices", "Recurring vendor invoice schedules.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-repeat");
        yield return P(a, "ap_generate_recurring", "Generate recurring invoices", "Invoices", "Create the next invoice from an active recurring schedule.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-play");
        yield return P(a, "ap_invoice_automation", "Vendor invoice automation", "Invoices", "Import and route incoming vendor invoices.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-cogs");
        yield return P(a, "ap_invoice_matching", "Invoice matching", "Invoices", "Match invoice, receipt, and purchase order quantities and amounts.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/three-way-match-app", "fa-check-square-o");
        yield return P(a, "ap_unposted_invoices", "Vendor invoices not posted", "Invoices", "Draft vendor invoices that have not posted to the ledger.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-edit");
        yield return P(a, "ap_invoices_due_today", "Vendor invoices due today", "Invoices", "Open vendor invoices due on the session date.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-calendar");
        yield return P(a, "ap_invoices_assigned", "Pending vendor invoices assigned to me", "Invoices", "Invoices waiting on this worker for match or approval.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-user");
        yield return P(a, "ap_global_invoice_journal", "Global invoice journal", "Invoices", "Invoice journal shared across legal entities.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-globe");
        yield return P(a, "ap_payment_journal", "Vendor payment journal", "Payments", "Propose, approve, and post vendor payments.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/payment-batches-app", "fa-money");
        yield return P(a, "ap_payments_assigned", "Payment journal assigned to me", "Payments", "Payment journals waiting on this worker.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-user");
        yield return P(a, "ap_payment_proposal", "Vendor payment proposal", "Payments", "Build a payment proposal from open invoices and terms.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-list");
        yield return P(a, "ap_settled_invoices", "Settled invoices", "Payments", "Vendor invoices already settled by a payment.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check-circle");
        yield return P(a, "ap_payment_history", "History by transaction", "Payments", "Payment and settlement history for a vendor invoice.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "ap_promissory_notes", "Promissory notes", "Payments", "Promissory notes issued to vendors.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-file-text-o");
        yield return P(a, "ap_draw_pn", "Draw promissory note journal", "Payments", "Draw a promissory note against an open invoice.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-pencil");
        yield return P(a, "ap_redraw_pn", "Redraw promissory note journal", "Payments", "Redraw a promissory note that was not honoured.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-undo");
        yield return P(a, "ap_remit_pn", "Remittance journal", "Payments", "Remit promissory notes to the bank.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-university");
        yield return P(a, "ap_settle_pn", "Settle promissory note journal", "Payments", "Settle a promissory note when it is paid.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "ap_all_vendors", "All vendors", "Vendors", "Vendor master used on invoices, payments, and purchase orders.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/suppliers-app", "fa-truck");
        yield return P(a, "ap_vendors_hold", "Vendors on hold", "Vendors", "Vendors blocked from new invoices or payments.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/suppliers-app", "fa-pause");
        yield return P(a, "ap_vendors_past_due", "Vendors past due", "Vendors", "Vendors with at least one invoice past due.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app?tab=ap", "fa-exclamation-circle");
        yield return P(a, "ap_vendor_groups", "Vendor groups", "Vendors", "Posting and terms defaults shared by a vendor group.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "ap_vendor_exception", "Vendor exception groups", "Vendors", "Vendors that skip a matching or payment rule.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-ban");
        yield return P(a, "ap_vendor_price_groups", "Vendor price tolerance groups", "Vendors", "Price-tolerance groups used during invoice match.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "ap_purchase_orders", "Purchase orders", "Purchase orders", "Purchase orders that feed vendor invoices.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/purchase-orders-app", "fa-clipboard");
        yield return P(a, "ap_purchase_agreements", "Purchase agreements", "Purchase orders", "Quantity or value agreements with a vendor.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/procurement-workspace-app?tab=proc_agreements", "fa-file-text-o");
        yield return P(a, "ap_ic_purchase_orders", "Intercompany purchase orders", "Purchase orders", "Purchase orders between legal entities.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-exchange");
        yield return P(a, "ap_po_received_ni", "Purchase orders received not invoiced", "Purchase orders", "Receipts still waiting for a vendor invoice.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-truck");
        yield return P(a, "ap_prepayments", "Vendor prepayments", "Purchase orders", "Prepayments applied against a purchase order or invoice.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-forward");
        yield return P(a, "ap_vendor_aging", "Vendor aging report", "Inquiries and reports", "Open vendor balances by aging bucket.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app?tab=ap", "fa-bar-chart");
        yield return P(a, "ap_vendor_balance", "Vendor balance list", "Inquiries and reports", "Open balance per vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "ap_vendor_statement", "Vendor account statement", "Inquiries and reports", "Statement of invoices and payments for a vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-o");
        yield return P(a, "ap_vendor_transactions", "Vendor transactions", "Inquiries and reports", "Posted invoices, payments, and settlements for a vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-alt");
        yield return P(a, "ap_invoice_journal_report", "Vendor invoice journal report", "Inquiries and reports", "Posted invoice journal lines.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "ap_invoice_history", "Invoice history and matching details", "Inquiries and reports", "Match results and invoice history.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/three-way-match-app", "fa-search");
        yield return P(a, "ap_vendor_ledger_recon", "Vendor to ledger reconciliation", "Inquiries and reports", "Reconcile vendor subledger to the control account.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "ap_matching_setup", "Invoice matching setup", "Setup", "Company defaults for two-way and three-way match.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "ap_matching_policy", "Matching policy", "Setup", "Quantity and price matching policies.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-check-square-o");
        yield return P(a, "ap_invoice_policy", "Vendor invoice policy", "Setup", "Policy that routes or blocks a vendor invoice.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-gavel");
        yield return P(a, "ap_invoice_policy_types", "Vendor invoice policy types", "Setup", "Named policy types attached to vendors.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "ap_charges_setup", "Charges setup", "Setup", "Landing, freight, and miscellaneous charges on invoices.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-plus");
        yield return P(a, "ap_charge_codes", "Charge codes", "Setup", "Charge codes posted with a vendor invoice.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-barcode");
        yield return P(a, "ap_payment_setup", "Payment setup", "Setup", "Company defaults for vendor payments.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "ap_terms_payment", "Terms of payment", "Setup", "Due-date calculation used on vendor invoices.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calendar");
        yield return P(a, "ap_cash_discounts", "Cash discounts", "Setup", "Early-payment discounts offered by vendors.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-percent");
        yield return P(a, "ap_methods_payment", "Methods of payment", "Setup", "Bank, cheque, and electronic payment methods.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/payment-systems-app", "fa-credit-card");
        yield return P(a, "ap_payment_fee", "Payment fee", "Setup", "Bank fees added when a vendor payment is posted.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-money");
        yield return P(a, "ap_posting_profiles", "Vendor posting profiles", "Setup", "Ledger accounts used when a vendor invoice or payment posts.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/suppliers-app?tab=ap_setup", "fa-book");
        yield return P(a, "ap_auto_settlement", "Automatic settlement", "Setup", "Settle open invoices automatically when a payment posts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-magic");
        yield return P(a, "ap_vendor_bank", "Vendor bank account inquiry", "Setup", "Bank accounts used to pay a vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-university");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> ReceivablesPages()
    {
        const string a = "ar";
        const string app = ReceivablesApp;
        yield return P(a, "ar_all_customers", "All customers", "Customers", "Customer master used on invoices, payments, and orders.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/contacts-app", "fa-user");
        yield return P(a, "ar_customers_hold", "Customers on hold", "Customers", "Customers blocked from new invoices or deliveries.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/contacts-app", "fa-pause");
        yield return P(a, "ar_customers_past_due", "Customers past due", "Customers", "Customers with at least one invoice past due.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app?tab=ar", "fa-exclamation-circle");
        yield return P(a, "ar_customers_underage", "Customer underage", "Customers", "Customers whose credit used is below the limit warning.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-info-circle");
        yield return P(a, "ar_free_text_invoices", "Free text invoices", "Invoices", "Invoices that are not created from a sales order.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/invoices-app", "fa-file-text-o");
        yield return P(a, "ar_free_text_assigned", "Free text invoices assigned to me", "Invoices", "Free text invoices waiting on this worker.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-user");
        yield return P(a, "ar_open_invoices", "Open customer invoices", "Invoices", "Unsettled customer invoices still on the receivables ledger.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/receivables-app?view=open", "fa-folder-open-o");
        yield return P(a, "ar_invoices_past_due", "Customer invoices past due", "Invoices", "Open customer invoices whose due date has passed.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/aging-app?tab=ar", "fa-exclamation-triangle");
        yield return P(a, "ar_invoices_due_today", "Customer invoices due today", "Invoices", "Open customer invoices due on the session date.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-calendar");
        yield return P(a, "ar_recurring_invoices", "Recurring invoices", "Invoices", "Recurring free-text invoice schedules.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-repeat");
        yield return P(a, "ar_generate_recurring", "Generate recurring invoices", "Invoices", "Create the next invoice from an active recurring schedule.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-play");
        yield return P(a, "ar_invoice_journal", "Invoice journal", "Invoices", "Customer invoice journal before posting.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/invoices-app", "fa-book");
        yield return P(a, "ar_invoice_pool", "Invoice pool", "Invoices", "Hold customer invoices until they are posted.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-inbox");
        yield return P(a, "ar_invoice_templates", "Free text invoice templates", "Invoices", "Reusable free-text invoice headers.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-copy");
        yield return P(a, "ar_sales_orders", "All sales orders", "Orders", "Sales orders that create customer invoices.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/sales-orders-app", "fa-shopping-cart");
        yield return P(a, "ar_export_orders", "Export orders", "Orders", "Export sales orders with shipping documents.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-ship");
        yield return P(a, "ar_ic_sales_orders", "Intercompany sales orders", "Orders", "Sales orders between legal entities.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-exchange");
        yield return P(a, "ar_orders_not_invoiced", "Sales orders not invoiced", "Orders", "Delivered orders still waiting for an invoice.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-file-o");
        yield return P(a, "ar_delivery_note_asap", "Delivery note ASAP", "Orders", "Delivery notes ready to invoice.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/delivery-notes-app", "fa-truck");
        yield return P(a, "ar_payment_journal", "Customer payment journal", "Payments", "Register and settle customer payments.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/receivables-app?view=open", "fa-money");
        yield return P(a, "ar_payments_assigned", "Payment journal assigned to me", "Payments", "Payment journals waiting on this worker.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-user");
        yield return P(a, "ar_payment_proposal", "Customer payment proposal", "Payments", "Propose settlements from open invoices and remittances.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-list");
        yield return P(a, "ar_settled_invoices", "Settled invoices", "Payments", "Customer invoices already settled by a payment.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check-circle");
        yield return P(a, "ar_payment_history", "History by transaction", "Payments", "Payment and settlement history for a customer invoice.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "ar_bill_of_exchange", "Bills of exchange", "Payments", "Bills of exchange drawn on a customer.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-file-text-o");
        yield return P(a, "ar_draw_boe", "Draw bill of exchange journal", "Payments", "Draw a bill of exchange against an open invoice.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-pencil");
        yield return P(a, "ar_redraw_boe", "Redraw bill of exchange journal", "Payments", "Redraw a bill that was not honoured.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-undo");
        yield return P(a, "ar_protest_boe", "Protest bill of exchange journal", "Payments", "Protest a dishonoured bill of exchange.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-gavel");
        yield return P(a, "ar_remit_boe", "Remittance journal", "Payments", "Remit bills of exchange to the bank.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-university");
        yield return P(a, "ar_settle_boe", "Settle bill of exchange journal", "Payments", "Settle a bill of exchange when it is paid.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "ar_customer_aging", "Customer aging report", "Inquiries and reports", "Open customer balances by aging bucket.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app?tab=ar", "fa-bar-chart");
        yield return P(a, "ar_customer_balance", "Customer balance list", "Inquiries and reports", "Open balance per customer.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "ar_customer_statement", "Customer account statement", "Inquiries and reports", "Statement of invoices and payments for a customer.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-o");
        yield return P(a, "ar_customer_transactions", "Customer transactions", "Inquiries and reports", "Posted invoices, payments, and settlements for a customer.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-alt");
        yield return P(a, "ar_invoice_journal_report", "Invoice journal report", "Inquiries and reports", "Posted customer invoice journal lines.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "ar_sales_tax_report", "Sales tax specification report", "Inquiries and reports", "Tax posted on customer invoices.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/vat-app", "fa-percent");
        yield return P(a, "ar_customer_ledger_recon", "Customer to ledger reconciliation", "Inquiries and reports", "Reconcile customer subledger to the control account.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "ar_collections_workspace", "Collections", "Collections", "Open the collections workspace for past-due customers.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/collections-dunning-app", "fa-phone");
        yield return P(a, "ar_posting_profiles", "Customer posting profiles", "Setup", "Ledger accounts used when a customer invoice or payment posts.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/contacts-app?tab=ar_setup", "fa-book");
        yield return P(a, "ar_terms_payment", "Terms of payment", "Setup", "Due-date calculation used on customer invoices.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calendar");
        yield return P(a, "ar_cash_discounts", "Cash discounts", "Setup", "Early-payment discounts offered to customers.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-percent");
        yield return P(a, "ar_methods_payment", "Methods of payment", "Setup", "Bank, cheque, and electronic receipt methods.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/payment-systems-app", "fa-credit-card");
        yield return P(a, "ar_payment_fee", "Payment fee", "Setup", "Bank fees added when a customer payment is posted.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-money");
        yield return P(a, "ar_auto_settlement", "Automatic settlement", "Setup", "Settle open invoices automatically when a payment posts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-magic");
        yield return P(a, "ar_reason_codes", "Customer reason codes", "Setup", "Reason codes for write-off, credit, and return.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-comment");
        yield return P(a, "ar_writeoff_codes", "Customer write-off reason codes", "Setup", "Reason codes used when writing off a customer balance.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-minus-circle");
        yield return P(a, "ar_billing_codes", "Billing codes", "Setup", "Codes used on free-text invoice lines.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "ar_form_notes", "Form notes", "Setup", "Notes printed on customer invoices and statements.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sticky-note-o");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> CashBankPages()
    {
        const string a = "banking";
        const string app = CashBankApp;
        yield return P(a, "cb_bank_accounts", "Bank accounts", "Bank accounts", "Company bank accounts used for receipts and payments.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/cash-accounts-app", "fa-university");
        yield return P(a, "cb_bank_transfers", "Bank account transfers", "Bank accounts", "Transfer cash between company bank accounts.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/cash-entries-app", "fa-exchange");
        yield return P(a, "cb_bank_transactions", "Bank transactions", "Bank accounts", "Posted receipts, payments, and transfers on a bank account.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/cash-entries-app", "fa-list");
        yield return P(a, "cb_bank_statements", "Bank statements", "Bank statement reconciliation", "Imported or entered bank statements waiting for match.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/bank-reconciliation-app", "fa-file-text-o");
        yield return P(a, "cb_bank_recon", "Reconciliation", "Bank statement reconciliation", "Match statement lines to posted bank transactions.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/bank-reconciliation-app", "fa-check-square-o");
        yield return P(a, "cb_unreconciled", "Unreconciled bank transactions", "Bank statement reconciliation", "Posted bank lines that have not been matched.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-question-circle");
        yield return P(a, "cb_print_statement", "Print statement", "Bank statement reconciliation", "Print a bank statement after reconciliation.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-print");
        yield return P(a, "cb_cash_forecast", "Cash flow forecast", "Cash flow forecasting", "Projected receipts and payments by period.", ErpD365Part3Catalog.FormKind.Forecast, app, "/erp/cash-accounts-app?tab=cash_forecast", "fa-line-chart");
        yield return P(a, "cb_cash_forecast_setup", "Cash flow forecast setup", "Cash flow forecasting", "Sources and horizons used by the cash forecast.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "cb_cash_forecast_auto", "Cash flow forecast automation", "Cash flow forecasting", "Regenerate the cash forecast from open documents.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "cb_netting", "Customer and vendor balance netting", "Netting", "Net a customer and vendor that are the same party.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-balance-scale");
        yield return P(a, "cb_netting_agreements", "Netting agreements", "Netting", "Agreements that allow customer/vendor netting.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-handshake-o");
        yield return P(a, "cb_netting_rules", "Netting rules", "Netting", "Rules that pick invoices for a netting run.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-filter");
        yield return P(a, "cb_auto_netting", "Automatic netting", "Netting", "Run netting from open customer and vendor balances.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-magic");
        yield return P(a, "cb_deposit_slips", "Deposit slips", "Deposit slips", "Group receipts onto a bank deposit slip.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-inbox");
        yield return P(a, "cb_deposit_report", "Deposit slip report", "Deposit slips", "Printed deposit slip for the bank.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-print");
        yield return P(a, "cb_deposit_by_customer", "Deposit summary by customer report", "Deposit slips", "Deposits grouped by customer.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "cb_deposit_by_date", "Deposit summary by date report", "Deposit slips", "Deposits grouped by date.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calendar");
        yield return P(a, "cb_deposit_reversal", "Deposit slip payment reversal", "Payment reversals", "Reverse a deposit that was entered in error.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-undo");
        yield return P(a, "cb_check_reversals", "Check reversals", "Payment reversals", "Reverse a posted cheque payment.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-undo");
        yield return P(a, "cb_reversal_approval", "Payment reversal approval", "Payment reversals", "Approve a requested payment reversal.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "cb_letters_guarantee", "Letters of guarantee", "Letters of guarantee", "Bank guarantee facilities and issued guarantees.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-certificate");
        yield return P(a, "cb_bank_facility", "Bank facility agreements", "Letters of guarantee", "Facility limits with a bank.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-university");
        yield return P(a, "cb_loc_export", "Export letter of credit and import collection", "Letters of credit", "Export LC and import collection documents.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-file-text-o");
        yield return P(a, "cb_loc_import", "Import letter of credit and import collection", "Letters of credit", "Import LC documents tied to purchase orders.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-download");
        yield return P(a, "cb_positive_pay", "Positive pay file summary", "Positive pay", "Positive-pay file sent to the bank for issued cheques.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-shield");
        yield return P(a, "cb_payroll_positive_pay", "Payroll positive pay file summary", "Positive pay", "Positive-pay file for payroll cheques.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-shield");
        yield return P(a, "cb_balance_control", "Balance control", "Inquiries and reports", "Bank account balance versus ledger control.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "cb_account_recon_report", "Bank account reconciliation report", "Inquiries and reports", "Reconciled and outstanding items on a statement.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-o");
        yield return P(a, "cb_change_history", "Bank account change history", "Inquiries and reports", "Audit of bank-account master changes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "cb_bridged_tx", "Bridged transactions", "Inquiries and reports", "Transactions waiting in the bridging account.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-random");
        yield return P(a, "cb_check_stats", "Check statistics", "Inquiries and reports", "Issued, voided, and outstanding cheques.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "cb_fx_reval", "Foreign currency revaluation", "Periodic", "Revalue foreign-currency bank balances.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/multi-currency-gl-app", "fa-globe");
        yield return P(a, "cb_bank_to_ledger", "Bank to ledger reconciliation report", "Periodic", "Reconcile bank subledger to the ledger.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "cb_parameters", "Cash and bank management parameters", "Setup", "Company defaults for bank, deposit, and reconciliation.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/cash-accounts-app?tab=bank_setup", "fa-sliders");
        yield return P(a, "cb_workflows", "Cash and bank management workflows", "Setup", "Approval workflow for payments and reversals.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflow-app", "fa-sitemap");
        yield return P(a, "cb_statement_format", "Bank statement format", "Setup", "Import format for electronic bank statements.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-code-o");
        yield return P(a, "cb_tx_mapping", "Transaction code mapping", "Setup", "Map bank transaction codes to cash entry types.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-random");
        yield return P(a, "cb_recon_rules", "Reconciliation matching rules", "Setup", "Rules that auto-match statement lines.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-filter");
        yield return P(a, "cb_bank_groups", "Bank groups", "Setup", "Group bank accounts for reporting.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "cb_payment_purpose", "Payment purpose codes", "Setup", "Purpose codes required on cross-border payments.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "cb_iban", "IBAN structures", "Setup", "IBAN validation by country.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-barcode");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> BudgetPages()
    {
        const string a = "budgeting";
        const string app = BudgetingApp;
        yield return P(a, "bud_ledger_workspace", "Ledger budgets and forecasts", "Workspaces", "Workspace for ledger budget register entries and forecasts.", ErpD365Part3Catalog.FormKind.Plan, app, "/erp/budgets-app", "fa-pie-chart");
        yield return P(a, "bud_planning_workspace", "Budget planning", "Workspaces", "Workspace for budget plans, worksheets, and stages.", ErpD365Part3Catalog.FormKind.Plan, app, "/erp/budgets-app", "fa-sitemap");
        yield return P(a, "bud_register_entries", "Budget register entries", "Budget register", "Posted budget amounts by account and dimension.", ErpD365Part3Catalog.FormKind.Plan, app, "/erp/budgets-app", "fa-list");
        yield return P(a, "bud_draft_entries", "Draft budget register entries", "Budget register", "Budget entries that have not been posted.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-edit");
        yield return P(a, "bud_completed_entries", "Completed budget register entries", "Budget register", "Budget entries that finished the workflow.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-check");
        yield return P(a, "bud_reservations", "General budget reservations", "Budget register", "Amounts reserved against a budget before spend.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-lock");
        yield return P(a, "bud_plans", "Budget plans", "Budget planning", "Named budget plans for a cycle and organization.", ErpD365Part3Catalog.FormKind.Plan, app, "/erp/budgets-app", "fa-map");
        yield return P(a, "bud_forecast_positions", "Forecast positions", "Budget planning", "Workforce forecast positions that drive a budget plan.", ErpD365Part3Catalog.FormKind.Forecast, app, icon: "fa-users");
        yield return P(a, "bud_commitments", "Commitments", "Budget planning", "Encumbrances and commitments against a budget.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-bookmark");
        yield return P(a, "bud_actual_vs_budget", "Actual versus budget", "Inquiries and reports", "Compare posted actuals to the adopted budget.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "bud_actual_vs_period", "Actual versus budget by period", "Inquiries and reports", "Period-by-period actual versus budget.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-table");
        yield return P(a, "bud_entries_by_dim", "Budget register entries by dimension set", "Inquiries and reports", "Budget amounts sliced by a dimension set.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "bud_account_entries", "Budget account entries", "Inquiries and reports", "Budget amounts by main account.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "bud_financial_reports", "Financial reports", "Inquiries and reports", "Budget columns on financial reports.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/report-center-app", "fa-bar-chart");
        yield return P(a, "bud_plan_by_dim", "Budget plans by dimension set", "Inquiries and reports", "Budget plans sliced by a dimension set.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "bud_plan_lines", "Budget plan lines", "Inquiries and reports", "Worksheet lines inside a budget plan.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-alt");
        yield return P(a, "bud_plan_audit", "Budget plan audit report", "Inquiries and reports", "Who changed a budget plan and when.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-search");
        yield return P(a, "bud_forecast_summary", "Forecast position summary", "Inquiries and reports", "Headcount and cost of forecast positions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "bud_control_stats", "Budget control statistics", "Budget control", "Documents checked against budget and the result.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-dashboard");
        yield return P(a, "bud_control_by_period", "Budget control statistics by period", "Budget control", "Budget-control results by fiscal period.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calendar");
        yield return P(a, "bud_analysis", "Budget analysis", "Budget control", "Over/under analysis for a budget cycle.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-line-chart");
        yield return P(a, "bud_gen_from_gl", "Generate budget plan from general ledger", "Periodic", "Seed a budget plan from posted ledger actuals.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-download");
        yield return P(a, "bud_gen_from_plan", "Generate budget plan from budget plan", "Periodic", "Copy an existing budget plan into a new cycle.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-copy");
        yield return P(a, "bud_gen_from_fa", "Generate budget plan from fixed assets", "Periodic", "Seed depreciation budget from the asset book.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-building");
        yield return P(a, "bud_gen_from_forecast", "Generate budget plan from forecast positions", "Periodic", "Seed personnel budget from forecast positions.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-users");
        yield return P(a, "bud_gen_register", "Generate budget register entry", "Periodic", "Post a budget plan into the ledger budget register.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-play");
        yield return P(a, "bud_parameters", "Budgeting parameters", "Setup", "Company defaults for budget register and control.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "bud_workflows", "Budgeting workflows", "Setup", "Approval workflow for budget register entries and plans.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflow-app", "fa-sitemap");
        yield return P(a, "bud_dimensions", "Dimensions for budgeting", "Setup", "Which financial dimensions a budget entry may use.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "bud_models", "Budget models", "Setup", "Named budget models (original, revised, forecast).", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-clone");
        yield return P(a, "bud_codes", "Budget codes", "Setup", "Original, transfer, revision, and carry-forward codes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "bud_cycles", "Budget cycles", "Setup", "Fiscal cycles used by budget planning and control.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-refresh");
        yield return P(a, "bud_allocation_terms", "Budget allocation terms", "Setup", "How an annual budget is spread across periods.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-pie-chart");
        yield return P(a, "bud_transfer_rules", "Budget transfer rules", "Setup", "Which accounts may receive a budget transfer.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-exchange");
        yield return P(a, "bud_control_config", "Budget control configuration", "Setup", "Over-budget rule, source documents, and override.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-gavel");
        yield return P(a, "bud_reservation_types", "Reservation types", "Setup", "Types of general budget reservation.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-lock");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> AssetPages()
    {
        const string a = "asset_mgmt";
        const string app = AssetMgmtApp;
        yield return P(a, "am_all_assets", "All assets", "Assets", "Maintainable assets used on work orders and maintenance.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/fixed-assets-app", "fa-cube");
        yield return P(a, "am_active_assets", "Active assets", "Assets", "Assets currently in service.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check");
        yield return P(a, "am_my_assets", "My active assets", "Assets", "Assets assigned to the signed-in worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "am_asset_view", "Asset view", "Assets", "Asset header, BOM, meters, and documents.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-eye");
        yield return P(a, "am_functional_locations", "All functional locations", "Functional locations", "Locations where assets are installed.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-map-marker");
        yield return P(a, "am_active_locations", "Active functional locations", "Functional locations", "Locations currently in use.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check");
        yield return P(a, "am_my_locations", "My active functional locations", "Functional locations", "Locations assigned to the signed-in worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "am_all_requests", "All maintenance requests", "Maintenance requests", "Requests that may become a work order.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-inbox");
        yield return P(a, "am_active_requests", "Active maintenance requests", "Maintenance requests", "Open maintenance requests.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-wrench");
        yield return P(a, "am_my_requests", "My functional location maintenance requests", "Maintenance requests", "Requests on locations assigned to this worker.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-user");
        yield return P(a, "am_all_work_orders", "All work orders", "Work orders", "Corrective and preventive work orders.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-clipboard");
        yield return P(a, "am_active_work_orders", "Active work orders", "Work orders", "Work orders not yet completed.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-play");
        yield return P(a, "am_my_schedule", "My work order schedule", "Work orders", "Work orders assigned to the signed-in worker.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "am_unscheduled", "Work order jobs not scheduled", "Work orders", "Jobs that still need a worker and date.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-hourglass-half");
        yield return P(a, "am_my_jobs", "My work order maintenance jobs", "Work orders", "Jobs assigned to this worker.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-wrench");
        yield return P(a, "am_all_jobs", "All work order maintenance jobs", "Work orders", "Every maintenance job on a work order.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-list");
        yield return P(a, "am_work_pools", "Active work pools", "Work order pools", "Pools used to group work for a crew.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "am_downtime", "Active work order downtime activities", "Work order pools", "Downtime recorded against an asset.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-pause");
        yield return P(a, "am_procurement", "Work order purchase requisition", "Procurement", "Parts requisition raised from a work order.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/purchase-requests-app", "fa-shopping-cart");
        yield return P(a, "am_open_schedule", "Open maintenance schedule lines", "Maintenance schedule", "Scheduled preventive lines not yet converted.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar-check-o");
        yield return P(a, "am_active_schedule", "Active maintenance schedule", "Maintenance schedule", "Active preventive maintenance schedule.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "am_schedule_pools", "Open schedule pools", "Maintenance schedule", "Pools of scheduled work waiting to become orders.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-inbox");
        yield return P(a, "am_active_loans", "Active asset loans", "Asset loans", "Assets loaned to a worker or location.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-exchange");
        yield return P(a, "am_all_loans", "All asset loans", "Asset loans", "Loan history for maintainable assets.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "am_faults", "Asset faults", "Maintenance", "Fault codes recorded against an asset.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exclamation-triangle");
        yield return P(a, "am_condition", "Condition assessment", "Maintenance", "Condition scores and inspection results.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-heartbeat");
        yield return P(a, "am_consumption", "Asset consumption", "Maintenance", "Fuel, hours, and meter consumption.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tachometer");
        yield return P(a, "am_bom", "Asset BOM", "Maintenance", "Spare parts list attached to an asset.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cubes");
        yield return P(a, "am_counters", "Counters", "Maintenance", "Meter readings that trigger maintenance.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-dashboard");
        yield return P(a, "am_work_order_types", "Work order types", "Setup", "Corrective, preventive, and inspection work-order types.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "am_job_types", "Maintenance job types", "Setup", "Standard jobs used on work orders.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-wrench");
        yield return P(a, "am_fault_codes", "Fault codes", "Setup", "Symptom, cause, and remedy codes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bug");
        yield return P(a, "am_lifecycle", "Asset lifecycle models", "Setup", "States an asset moves through.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-recycle");
        yield return P(a, "am_parameters", "Asset management parameters", "Setup", "Company defaults for work orders and requests.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> AuditPages()
    {
        const string a = "audit_wb";
        const string app = AuditApp;
        yield return P(a, "aw_cases", "Audit cases", "Audit cases", "Audit cases opened against a policy or process.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/audit-trail-app", "fa-folder-open-o");
        yield return P(a, "aw_policies", "Audit policies", "Setup", "Policies that create an audit case when a rule is met.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-gavel");
        yield return P(a, "aw_case_grouping", "Case grouping criteria", "Setup", "How similar exceptions are grouped into one case.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "aw_rule_types", "Policy rule types", "Setup", "Rule types a policy may use.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "aw_audit_trail", "Audit trail", "Inquiries and reports", "Posted ERP audit log for documents and users.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/audit-trail-app", "fa-history");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> CommonPages()
    {
        const string a = "common";
        const string app = CommonApp;
        yield return P(a, "cmn_address_book", "Global address book", "Common", "Parties shared across customers, vendors, and workers.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/contacts-app", "fa-address-book-o");
        yield return P(a, "cmn_dashboard", "Default dashboard", "Common", "Landing dashboard for the signed-in worker.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/dashboard-summary-app", "fa-dashboard");
        yield return P(a, "cmn_session_datetime", "Session date and time", "Common", "Override the session date used by journals.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-clock-o");
        yield return P(a, "cmn_kpis", "All KPIs", "Common", "KPI definitions available on workspaces.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-line-chart");
        yield return P(a, "cmn_work_assigned", "Work items assigned to me", "Work items", "Workflow work items waiting on this worker.", ErpD365Part3Catalog.FormKind.Queue, app, "/erp/approvals-app", "fa-inbox");
        yield return P(a, "cmn_work_queues", "Work items assigned to queues I belong to", "Work items", "Queue work items this worker may pick.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-users");
        yield return P(a, "cmn_work_admin", "Administer work items", "Work items", "Reassign or cancel workflow work items.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-wrench");
        yield return P(a, "cmn_work_my_queues", "Work items assigned to my queues", "Work items", "Work sitting in queues this worker owns.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-sitemap");
        yield return P(a, "cmn_all_cases", "All cases", "Cases", "Cases opened across modules.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-folder-open-o");
        yield return P(a, "cmn_my_cases", "My cases", "Cases", "Cases owned by the signed-in worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "cmn_my_open_cases", "My open cases", "Cases", "Open cases owned by this worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-folder-open");
        yield return P(a, "cmn_open_cases", "Open cases", "Cases", "Every open case.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-inbox");
        yield return P(a, "cmn_overdue_cases", "My overdue cases", "Cases", "Cases past their due date.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exclamation-triangle");
        yield return P(a, "cmn_cases_workflow", "Cases assigned to me through workflow", "Cases", "Cases routed to this worker.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-random");
        yield return P(a, "cmn_cases_my_queues", "Cases assigned to my queues", "Cases", "Cases sitting in this worker's queues.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-users");
        yield return P(a, "cmn_all_capa", "All CAPA cases", "CAPA management", "Corrective and preventive action cases.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-medkit");
        yield return P(a, "cmn_my_capa", "My CAPA cases", "CAPA management", "CAPA cases owned by this worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "cmn_all_activities", "All activities", "Activities", "Tasks, calls, and appointments.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/agenda-app", "fa-calendar");
        yield return P(a, "cmn_my_activities", "My activities", "Activities", "Activities assigned to this worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "cmn_my_open_activities", "My open activities", "Activities", "Open activities assigned to this worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-folder-open");
        yield return P(a, "cmn_activities_due_week", "Activities due this week", "Activities", "Activities due in the current week.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calendar-check-o");
        yield return P(a, "cmn_activities_past_due", "Activities past due", "Activities", "Activities whose due date has passed.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exclamation-circle");
        yield return P(a, "cmn_clock", "Clock in/out", "Time and attendance", "Punch clock for the signed-in worker.", ErpD365Part3Catalog.FormKind.Payroll, app, icon: "fa-clock-o");
        yield return P(a, "cmn_electronic_card", "Electronic timecard", "Time and attendance", "Weekly timecard used by payroll.", ErpD365Part3Catalog.FormKind.Payroll, app, icon: "fa-table");
        yield return P(a, "cmn_knowledge", "Knowledge articles", "Document management", "Knowledge articles shared with workers.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/guide-app", "fa-book");
        yield return P(a, "cmn_documents", "Documents", "Document management", "Business documents attached to records.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/documents-app", "fa-file-text-o");
        yield return P(a, "cmn_doc_templates", "Document templates", "Document management", "Templates used to generate documents.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-copy");
        yield return P(a, "cmn_workflow_history", "Workflow history", "Inquiries", "Completed and cancelled workflow instances.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflow-app", "fa-history");
        yield return P(a, "cmn_my_batch", "My batch jobs", "Batch jobs", "Batch jobs submitted by this worker.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-cogs");
        yield return P(a, "cmn_generated_files", "Generated file list", "Batch jobs", "Files produced by a batch job.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-files-o");
        yield return P(a, "cmn_record_templates", "Record templates", "Setup", "Default values applied when a record is created.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-magic");
        yield return P(a, "cmn_user_options", "User options", "Setup", "Personal options for the signed-in worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> ConsolidationPages()
    {
        const string a = "consolidations";
        const string app = ConsolidationsApp;
        yield return P(a, "cons_online", "Consolidate online", "Consolidations", "Run a consolidation for the current period.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/consolidations-app", "fa-play");
        yield return P(a, "cons_import", "Consolidate with import", "Consolidations", "Import balances from a subsidiary and consolidate.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-download");
        yield return P(a, "cons_export", "Export company balances", "Consolidations", "Export balances for a parent to import.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-upload");
        yield return P(a, "cons_elimination", "Elimination journal", "Consolidations", "Elimination entries between consolidated entities.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/intercompany-app?tab=elimination", "fa-minus");
        yield return P(a, "cons_transactions", "Consolidation transactions", "Consolidations", "Posted consolidation and elimination journals.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/gl-journals-app", "fa-list");
        yield return P(a, "cons_financial_reports", "Financial reports", "Consolidations", "Consolidated financial reports.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/report-center-app?tab=balance_sheet&level=consolidation", "fa-bar-chart");
        yield return P(a, "cons_detect_missing", "Detect and recreate missing consolidation transactions", "Consolidations", "Find subsidiaries that did not consolidate and recreate.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-search");
        yield return P(a, "cons_elim_rules", "Elimination rules", "Setup", "Accounts eliminated when consolidating.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-filter");
        yield return P(a, "cons_account_groups", "Consolidation account groups", "Setup", "How subsidiary accounts map to the parent.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/consolidations-app?tab=consolidation_group", "fa-sitemap");
        yield return P(a, "cons_additional_accounts", "Additional consolidation accounts", "Setup", "Extra accounts included in a consolidation.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-plus");
        yield return P(a, "cons_elim_report", "Elimination rules definition report", "Setup", "Print the active elimination rules.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/report-center-app?tab=balance_sheet&level=consolidation", "fa-print");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> CostAcctPages()
    {
        const string a = "cost_acct";
        const string app = CostAcctApp;
        yield return P(a, "ca_cost_control", "Cost control", "Workspaces", "Workspace for cost entries, policies, and roll-up.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/cost-models-app", "fa-dashboard");
        yield return P(a, "ca_ledger_admin", "Cost accounting ledger administration", "Workspaces", "Administer cost-accounting ledgers and calendars.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "ca_get_started", "Get started", "Setup", "Checklist to stand up a cost-accounting ledger.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flag");
        yield return P(a, "ca_parameters", "Parameters", "Setup", "Company defaults for cost accounting.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "ca_stat_templates", "Statistical measure provider templates", "Setup", "Templates that import statistical measures.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-table");
        yield return P(a, "ca_ledgers", "Cost accounting ledgers", "Ledger setup", "Named cost ledgers beside the general ledger.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "ca_fiscal", "Fiscal calendars", "Ledger setup", "Calendars used by a cost ledger.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "ca_currencies", "Currencies", "Currencies", "Currencies a cost ledger may post in.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/multi-currency-gl-app", "fa-money");
        yield return P(a, "ca_exch_types", "Exchange rate types", "Currencies", "Rate types used to translate cost entries.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/multi-currency-gl-app", "fa-line-chart");
        yield return P(a, "ca_exch_rates", "Currency exchange rates", "Currencies", "Rates used by cost accounting.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/multi-currency-gl-app", "fa-exchange");
        yield return P(a, "ca_cost_elements", "Cost element dimensions", "Dimensions", "Cost-element dimension members.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "ca_cost_objects", "Cost object dimensions", "Dimensions", "Cost-object dimension members.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "ca_stat_dims", "Statistical dimensions", "Dimensions", "Statistical dimension members.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "ca_dim_hierarchies", "Dimension hierarchies", "Dimensions", "Hierarchies used to roll up cost.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "ca_predef_bases", "Predefined dimension member allocation bases", "Allocation bases", "Built-in allocation bases.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-pie-chart");
        yield return P(a, "ca_formula_bases", "Formula allocation bases", "Allocation bases", "Allocation bases calculated from a formula.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-calculator");
        yield return P(a, "ca_hierarchy_bases", "Hierarchy allocation bases", "Allocation bases", "Allocation bases taken from a hierarchy.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-sitemap");
        yield return P(a, "ca_behavior", "Cost behavior policies", "Policies", "Fixed, variable, and mixed cost behaviour.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-sliders");
        yield return P(a, "ca_distribution", "Cost distribution policies", "Policies", "How a cost pool is distributed.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-share-alt");
        yield return P(a, "ca_rollup", "Cost rollup policies", "Policies", "How cost rolls up a hierarchy.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-level-up");
        yield return P(a, "ca_allocation_pol", "Cost allocation policies", "Policies", "How cost is allocated to objects.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-random");
        yield return P(a, "ca_overhead", "Overhead rate policies", "Policies", "Overhead rates applied to cost objects.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-percent");
        yield return P(a, "ca_journals", "Journals", "Inquiries and reports", "Cost-accounting journals.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/gl-journals-app", "fa-book");
        yield return P(a, "ca_cost_entries", "Cost entries", "Inquiries and reports", "Posted cost entries.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/gl-journals-app", "fa-list");
        yield return P(a, "ca_stat_entries", "Statistical entries", "Inquiries and reports", "Posted statistical measures.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> CostMgmtPages()
    {
        const string a = "cost_mgmt";
        const string app = CostMgmtApp;
        yield return P(a, "cm_cost_admin", "Cost administration", "Workspaces", "Workspace for inventory and manufacturing costing.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/cost-models-app", "fa-cogs");
        yield return P(a, "cm_cost_analysis", "Cost analysis", "Workspaces", "Compare standard, actual, and manufactured cost.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/report-center-app?tab=pl&level=business_unit", "fa-line-chart");
        yield return P(a, "cm_released_products", "Released products", "Inventory accounting", "Items with a costing version and cost group.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/product-info-app", "fa-cube");
        yield return P(a, "cm_closing", "Closing and adjustment", "Inventory accounting", "Inventory closing and adjustment for a period.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-lock");
        yield return P(a, "cm_revaluation", "Revaluation for moving average", "Inventory accounting", "Revalue moving-average items.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "cm_costing_versions", "Costing versions", "Inventory accounting", "Named costing versions (planned or standard).", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-clone");
        yield return P(a, "cm_recalc_bom", "Recalculate BOM levels", "Inventory accounting", "Rebuild BOM levels before a cost roll-up.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-sitemap");
        yield return P(a, "cm_inventory_cleanup", "Inventory settlements cleanup", "Inventory accounting", "Purge old inventory settlements.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "cm_cost_calc", "Cost calculation", "Inventory accounting", "Roll up standard cost for a costing version.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-calculator");
        yield return P(a, "cm_inv_value_cleanup", "Inventory value report data cleanup", "Inventory accounting", "Clear staged inventory-value report data.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-eraser");
        yield return P(a, "cm_std_cost_tx", "Standard cost transactions", "Inventory accounting", "Posted standard-cost revaluations.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "cm_sales_profitability", "Sales profitability performance", "Inquiries and reports", "Power BI view of posted sales: revenue, cost, gross margin and margin percent by customer, item and month.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/sales-workspace-app?tab=sm_performance", "fa-bar-chart");
        yield return P(a, "cm_inv_value", "Inventory value", "Inquiries and reports", "On-hand inventory value by item and warehouse.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/inventory-report-app", "fa-bar-chart");
        yield return P(a, "cm_inv_value_std", "Inventory value with standard cost breakdown", "Inquiries and reports", "Standard-cost components of on-hand value.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-pie-chart");
        yield return P(a, "cm_production_orders", "Production orders", "Manufacturing accounting", "Production orders that absorb cost.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/production-overview-app", "fa-industry");
        yield return P(a, "cm_wip", "Work in process", "Manufacturing accounting", "WIP value on open production orders.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tasks");
        yield return P(a, "cm_indirect", "Indirect cost in process", "Manufacturing accounting", "Indirect cost absorbed into WIP.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-random");
        yield return P(a, "cm_variance", "Variance statement", "Manufacturing accounting", "Production variances versus standard.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "cm_cost_groups", "Cost groups", "Setup", "Material, labour, and overhead cost groups.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "cm_item_groups", "Item groups", "Setup", "Item groups that share a costing setup.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cubes");
        yield return P(a, "cm_costing_sheets", "Costing sheets", "Setup", "How cost components roll into a sheet.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-table");
        yield return P(a, "cm_parameters", "Inventory accounting parameters", "Setup", "Company defaults for inventory costing.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "cm_std_cost_conv", "Standard cost conversions", "Setup", "Convert an item to standard cost.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-exchange");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> CreditPages()
    {
        const string a = "credit_coll";
        const string app = CreditCollApp;
        yield return P(a, "cc_workspace", "Customer credit and collections", "Workspaces", "Workspace for aging, holds, and collection cases.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/collections-dunning-app", "fa-phone");
        yield return P(a, "cc_all_customers", "All customers", "Customers", "Customers reviewed for credit and collections.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/contacts-app", "fa-user");
        yield return P(a, "cc_customers_hold", "Customers on hold", "Customers", "Customers on credit hold.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-pause");
        yield return P(a, "cc_customers_past_due", "Customers past due", "Customers", "Customers with past-due invoices.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app?tab=ar", "fa-exclamation-circle");
        yield return P(a, "cc_credit_groups", "Customer credit groups", "Customers", "Shared credit limits and terms.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/customer-groups-app", "fa-users");
        yield return P(a, "cc_aged_balances", "Aged balances", "Collections", "Aging snapshot used by collectors.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app", "fa-bar-chart");
        yield return P(a, "cc_collection_cases", "Collections cases", "Collections", "Collection cases opened for a customer.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/collections-dunning-app", "fa-folder-open-o");
        yield return P(a, "cc_collection_activities", "Collections activities", "Collections", "Calls, promises, and follow-ups on a case.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-phone");
        yield return P(a, "cc_hold_list", "Credit management hold list", "Credit management", "Documents waiting on a credit hold.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-lock");
        yield return P(a, "cc_open_holds", "Open credit holds", "Credit management", "Active credit holds.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-ban");
        yield return P(a, "cc_closed_holds", "Closed credit holds", "Credit management", "Released credit holds.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-unlock");
        yield return P(a, "cc_credit_limit_adj", "Credit limit adjustments", "Credit management", "Temporary or permanent credit-limit changes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "cc_interest_codes", "Interest codes", "Interest", "Interest calculated on past-due invoices.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-percent");
        yield return P(a, "cc_create_interest", "Create interest notes", "Interest", "Generate interest notes from past-due invoices.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-play");
        yield return P(a, "cc_interest_journal", "Interest journal", "Interest", "Posted interest notes.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-book");
        yield return P(a, "cc_collection_letters", "Collection letters", "Collection letters", "Dunning letters generated for past-due customers.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-envelope-o");
        yield return P(a, "cc_letter_journal", "Collection letter journal", "Collection letters", "Posted collection-letter runs.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "cc_letter_notes", "Collection letter note report", "Collection letters", "Notes printed on a collection letter.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sticky-note-o");
        yield return P(a, "cc_writeoff_journal", "Write-off journals", "Periodic", "Write off uncollectable customer balances.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-minus-circle");
        yield return P(a, "cc_aging_report", "Customer aging report", "Inquiries and reports", "Open customer balances by aging bucket.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app?tab=ar", "fa-bar-chart");
        yield return P(a, "cc_customer_statement", "Customer account statement", "Inquiries and reports", "Statement sent with a collection letter.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-o");
        yield return P(a, "cc_parameters", "Accounts receivable parameters", "Setup", "Company defaults for credit and collections.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/contacts-app?tab=ar_setup", "fa-sliders");
        yield return P(a, "cc_workflows", "Credit management workflows", "Setup", "Approval workflow for credit holds and limit changes.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflow-app", "fa-sitemap");
        yield return P(a, "cc_interest_setup", "Set up interest codes", "Setup", "Interest calculation method and ledger.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cogs");
        yield return P(a, "cc_letter_sequence", "Set up collection letter sequence", "Setup", "Which letter is sent at each aging step.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-sort-numeric-asc");
        yield return P(a, "cc_blocking_rules", "Blocking rules", "Setup", "Rules that place a customer or order on hold.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-ban");
        yield return P(a, "cc_scoring", "Scoring groups", "Setup", "Risk scores used to set a credit limit.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-star");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> ExpensePages()
    {
        const string a = "expense";
        const string app = ExpenseApp;
        yield return P(a, "exp_workspace", "Expense management", "Workspaces", "Workspace for expense reports and cash advances.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/expense-reports-app", "fa-credit-card");
        yield return P(a, "exp_my_reports", "Expense reports", "My expenses", "Expense reports owned by the signed-in worker.", ErpD365Part3Catalog.FormKind.Request, app, "/erp/expense-reports-app", "fa-file-text-o");
        yield return P(a, "exp_cash_advances", "Cash advances", "My expenses", "Cash advances requested by this worker.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-money");
        yield return P(a, "exp_expenses", "Expenses", "My expenses", "Individual expense lines before they join a report.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-list");
        yield return P(a, "exp_matched", "Matched expenses", "My expenses", "Expenses matched to a credit-card transaction.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-link");
        yield return P(a, "exp_unattached", "Unattached receipts", "My expenses", "Receipts not yet attached to an expense.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-paperclip");
        yield return P(a, "exp_delegates", "Expense entry delegates", "My expenses", "Workers who may enter expenses on behalf of this worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "exp_travel_req", "Travel requisition", "My expenses", "Travel request that may become an expense report.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-plane");
        yield return P(a, "exp_all_reports", "All expense reports", "Process expense reports", "Every expense report regardless of status.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-list-alt");
        yield return P(a, "exp_approved_reports", "Approved expense reports", "Process expense reports", "Reports approved and waiting for payment.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-check");
        yield return P(a, "exp_in_review", "Expense reports in review", "Process expense reports", "Reports sitting with an approver.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-hourglass-half");
        yield return P(a, "exp_assigned_to_me", "Expense reports assigned to me", "Process expense reports", "Reports waiting on this approver.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-user");
        yield return P(a, "exp_assigned_queues", "Expense reports assigned to my queues", "Process expense reports", "Reports sitting in this worker's queues.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-users");
        yield return P(a, "exp_approved_advances", "Approved cash advances", "Process cash advances", "Cash advances approved and waiting for payment.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-check");
        yield return P(a, "exp_advances_review", "Cash advances for my review", "Process cash advances", "Cash advances waiting on this approver.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-user");
        yield return P(a, "exp_tax_recovery", "Expense tax recovery", "Process tax recovery", "Recover input tax from posted expense reports.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/vat-app", "fa-percent");
        yield return P(a, "exp_personal_report", "Personal expenses report", "Inquiries and reports", "Expenses marked personal and not reimbursed.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "exp_vendor_aging", "Vendor aging report", "Inquiries and reports", "Expense vendors still unpaid.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app?tab=ap", "fa-bar-chart");
        yield return P(a, "exp_parameters", "Expense management parameters", "Setup", "Company defaults for reports, tax, and payment.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "exp_workflows", "Expense management workflows", "Setup", "Approval workflow for reports and advances.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflow-app", "fa-sitemap");
        yield return P(a, "exp_categories", "Expense categories", "Setup", "Categories and default ledger accounts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "exp_per_diem", "Per diems", "Setup", "Per-diem rates by location and date.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cutlery");
        yield return P(a, "exp_mileage", "Mileage", "Setup", "Mileage rates used on expense lines.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-car");
        yield return P(a, "exp_payment_methods", "Payment methods", "Setup", "How an expense was paid (cash, card, company).", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-credit-card");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> FixedAssetPages()
    {
        const string a = "fixed_assets";
        const string app = FixedAssetsApp;
        yield return P(a, "fa_workspace", "Fixed asset management", "Workspaces", "Workspace for the asset register, books, and journals.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/fixed-assets-app", "fa-building");
        yield return P(a, "fa_assets", "Fixed assets", "Fixed assets", "Asset register: acquisition, book, and location.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/fixed-assets-app", "fa-cube");
        yield return P(a, "fa_not_acquired", "Fixed assets not acquired", "Fixed assets", "Assets created but not yet acquired.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-hourglass-half");
        yield return P(a, "fa_acquired", "Fixed assets acquired", "Fixed assets", "Assets that have an acquisition date.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check");
        yield return P(a, "fa_sold", "Fixed assets sold or scrapped", "Fixed assets", "Assets disposed by sale or scrap.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-trash");
        yield return P(a, "fa_journal", "Fixed assets journal", "Journal entries", "Acquisition, depreciation, and disposal journal.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-book");
        yield return P(a, "fa_acq_proposal", "Create acquisition proposal", "Journal entries", "Propose acquisitions from unposted assets.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-plus");
        yield return P(a, "fa_depr_proposal", "Create depreciation proposal", "Journal entries", "Propose periodic depreciation for a book.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-play");
        yield return P(a, "fa_inv_to_fa", "Inventory to fixed assets journal", "Journal entries", "Capitalize inventory into a fixed asset.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-exchange");
        yield return P(a, "fa_budget_journal", "Fixed asset budget journal", "Budget", "Budget depreciation and acquisitions.", ErpD365Part3Catalog.FormKind.Plan, app, "/erp/budgets-app", "fa-pie-chart");
        yield return P(a, "fa_transactions", "Fixed asset transactions", "Inquiries and reports", "Posted acquisition, depreciation, and disposal.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "fa_listing", "Fixed asset listing report", "Inquiries and reports", "Register listing with book value.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-print");
        yield return P(a, "fa_balances", "Fixed asset balances", "Inquiries and reports", "Acquisition, depreciation, and net book value.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "fa_statement", "Fixed asset statement", "Inquiries and reports", "Statement of activity for a book.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-o");
        yield return P(a, "fa_rollforward", "Fixed asset roll forward", "Inquiries and reports", "Opening, activity, and closing by asset.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-refresh");
        yield return P(a, "fa_parameters", "Fixed assets parameters", "Setup", "Company defaults for books, journals, and numbering.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "fa_books", "Books", "Setup", "Accounting and tax books attached to an asset.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "fa_posting_profiles", "Fixed asset posting profiles", "Setup", "Ledger accounts used by asset journals.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "fa_groups", "Fixed asset groups", "Setup", "Groups that share a book and depreciation profile.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "fa_depr_profiles", "Depreciation profiles", "Setup", "Straight-line, reducing-balance, and factor methods.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-line-chart");
        yield return P(a, "fa_major_types", "Major types", "Setup", "Major asset types used for reporting.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "fa_locations", "Fixed asset locations", "Setup", "Physical locations of assets.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-map-marker");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> GeneralLedgerPages()
    {
        const string a = "gl";
        const string app = GeneralLedgerApp;
        yield return P(a, "gl_account_recon", "Account reconciliation", "Workspaces", "Reconcile a main account to its subledger.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/trial-balance-app", "fa-balance-scale");
        yield return P(a, "gl_financial_period", "Financial period close", "Workspaces", "Period-close checklist and status.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/period-close-app", "fa-lock");
        yield return P(a, "gl_financial_analysis", "Financial analysis — all companies", "Workspaces", "Financial analysis across legal entities.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/report-center-app", "fa-line-chart");
        yield return P(a, "gl_journals", "General journals", "Journal entries", "Daily general journals before posting.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/gl-journals-app", "fa-book");
        yield return P(a, "gl_global_journals", "Global general journals", "Journal entries", "Journals shared across legal entities.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/gl-journals-app", "fa-globe");
        yield return P(a, "gl_post_journals", "Post journals", "Journal entries", "Post approved general journals.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/gl-journals-app", "fa-check");
        yield return P(a, "gl_periodic_journals", "Periodic journals", "Journal entries", "Recurring journals generated each period.", ErpD365Part3Catalog.FormKind.Plan, app, "/erp/fin-advanced-app", "fa-refresh");
        yield return P(a, "gl_allocation_journals", "Allocation journals", "Journal entries", "Allocate an amount across dimensions.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/fin-advanced-app", "fa-pie-chart");
        yield return P(a, "gl_reporting_adj", "Reporting currency adjustment journal", "Journal entries", "Adjust reporting-currency amounts.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/multi-currency-gl-app", "fa-globe");
        yield return P(a, "gl_trial_balance", "Trial balance", "Inquiries and reports", "Opening, period, and closing by main account and posting layer.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/trial-balance-app", "fa-balance-scale");
        yield return P(a, "gl_coa", "Chart of accounts", "Inquiries and reports", "Main accounts used on every posting.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/coa-accounts-app", "fa-sitemap");
        yield return P(a, "gl_voucher_tx", "Voucher transactions", "Inquiries and reports", "Posted voucher lines.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/gl-journals-app", "fa-list");
        yield return P(a, "gl_audit_trail", "Audit trail", "Inquiries and reports", "Posted ledger audit trail.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/audit-trail-app", "fa-history");
        yield return P(a, "gl_financial_reports", "Financial reports", "Inquiries and reports", "Balance sheet, income statement, and custom reports.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/report-center-app?tab=pl", "fa-bar-chart");
        yield return P(a, "gl_dimension_statement", "Dimension statement", "Inquiries and reports", "Balances by financial dimension.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/report-center-app?tab=pl&level=business_unit", "fa-th");
        yield return P(a, "gl_ledger_tx_list", "Ledger transaction list", "Inquiries and reports", "Posted ledger lines for a period.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/gl-journals-app", "fa-list-alt");
        yield return P(a, "gl_account_explorer", "Balance explorer", "Inquiries and reports", "Drill from a main-account balance into vouchers.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/trial-balance-app", "fa-search");
        yield return P(a, "gl_fx_reval", "Foreign currency revaluation", "Periodic", "Revalue foreign-currency ledger balances.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/multi-currency-gl-app", "fa-globe");
        yield return P(a, "gl_year_end", "Year end close", "Periodic", "Close income accounts into retained earnings.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/period-close-app", "fa-flag-checkered");
        yield return P(a, "gl_opening", "Opening balances", "Periodic", "Opening balances for a new fiscal year.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/opening-app", "fa-sign-in");
        yield return P(a, "gl_main_accounts", "Main accounts", "Chart of accounts", "Main account master: type, posting, and close.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/coa-accounts-app", "fa-book");
        yield return P(a, "gl_account_structures", "Account structures", "Chart of accounts", "Which dimensions a main account may use.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "gl_advanced_rules", "Advanced rule structures", "Chart of accounts", "Extra dimension rules for selected accounts.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/fin-advanced-app", "fa-random");
        yield return P(a, "gl_ledger", "Ledger", "Ledger setup", "Ledger, calendar, and accounting currency.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-university");
        yield return P(a, "gl_parameters", "Ledger parameters", "Ledger setup", "Company defaults for journals and posting.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "gl_fiscal_calendar", "Fiscal calendars", "Ledger setup", "Fiscal years and periods.", ErpD365Part3Catalog.FormKind.Calendar, app, "/erp/period-close-app", "fa-calendar");
        yield return P(a, "gl_currencies", "Currencies", "Currencies", "Accounting, reporting, and transaction currencies.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/multi-currency-gl-app", "fa-money");
        yield return P(a, "gl_exch_rates", "Exchange rates", "Currencies", "Currency exchange rates used by journals.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/multi-currency-gl-app", "fa-line-chart");
        yield return P(a, "gl_journal_names", "Journal names", "Journal setup", "Named journals with default voucher and offset.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "gl_journal_descriptions", "Journal descriptions", "Journal setup", "Default descriptions for a journal name.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-align-left");
        yield return P(a, "gl_posting_defs", "Posting definitions", "Posting setup", "Advanced posting definitions for selected documents.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cogs");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> HrPages()
    {
        const string a = "people";
        const string app = HrApp;
        yield return P(a, "hr_workspace", "Human resources", "Workspaces", "Workspace for workers, positions, and compensation.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/hr-overview-app", "fa-users");
        yield return P(a, "hr_employees", "Employees", "Workers", "Employee records used by payroll and leave.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/staff-app", "fa-id-badge");
        yield return P(a, "hr_workers", "Workers", "Workers", "Every worker (employee and contractor).", ErpD365Part3Catalog.FormKind.Master, app, "/erp/staff-app", "fa-user");
        yield return P(a, "hr_contractors", "Contractors", "Workers", "Contractors paid outside or through payroll.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user-o");
        yield return P(a, "hr_exworkers", "Ex-workers", "Workers", "Workers whose employment has ended.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sign-out");
        yield return P(a, "hr_pending_workers", "Pending workers", "Workers", "Workers not yet hired.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-hourglass-half");
        yield return P(a, "hr_positions", "Positions", "Positions", "Positions that carry a department, job, and grade.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "hr_all_positions", "All positions", "Positions", "Every position regardless of status.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "hr_open_positions", "Open positions", "Positions", "Positions that are vacant.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/recruitment-app", "fa-folder-open-o");
        yield return P(a, "hr_position_hierarchy", "Position hierarchy", "Positions", "Reporting hierarchy of positions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "hr_jobs", "Jobs", "Jobs", "Job definitions used by positions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-briefcase");
        yield return P(a, "hr_job_functions", "Job functions", "Jobs", "Functions used to classify jobs.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "hr_departments", "Departments", "Departments", "Departments used on positions and cost.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-building");
        yield return P(a, "hr_compensation", "Fixed compensation", "Compensation", "Base pay assigned to a worker or position.", ErpD365Part3Catalog.FormKind.Payroll, app, icon: "fa-money");
        yield return P(a, "hr_variable_comp", "Variable compensation", "Compensation", "Bonus and commission plans.", ErpD365Part3Catalog.FormKind.Payroll, app, icon: "fa-trophy");
        yield return P(a, "hr_benefits", "Benefits", "Benefits", "Benefit plans and worker enrollment.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-heart");
        yield return P(a, "hr_recruitment", "Recruitment projects", "Recruitment", "Open requisitions and applicants.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/recruitment-app", "fa-user-plus");
        yield return P(a, "hr_applications", "Applications", "Recruitment", "Applications received for a requisition.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/recruitment-app", "fa-inbox");
        yield return P(a, "hr_courses", "Courses", "Learning", "Courses and registrations.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-graduation-cap");
        yield return P(a, "hr_performance", "Performance", "Performance", "Reviews and goals.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/performance-app", "fa-star");
        yield return P(a, "hr_absence_codes", "Absence codes", "Absence", "Absence codes used beside leave plans.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "hr_parameters", "Human resources parameters", "Setup", "Company defaults for workers and positions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "hr_workflows", "Human resource workflows", "Setup", "Approval workflow for hire, position, and compensation.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflow-app", "fa-sitemap");
        yield return P(a, "hr_number_sequences", "Number sequences", "Setup", "Number sequences for workers, positions, and cases.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-ol");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> InventoryPages()
    {
        const string a = "inventory_mgmt";
        const string app = InventoryApp;
        yield return P(a, "inv_onhand", "On-hand", "Inventory", "On-hand quantity by item, warehouse, and batch.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/inventory-stock-app", "fa-cubes");
        yield return P(a, "inv_onhand_warehouse", "On-hand warehouse", "Inventory", "On-hand quantity by warehouse.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/warehouses-app", "fa-building");
        yield return P(a, "inv_movements", "Inventory movement", "Inventory", "Movement journals between locations.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/stock-movements-app", "fa-random");
        yield return P(a, "inv_adjustment", "Inventory adjustment", "Inventory", "Quantity or value adjustment journals.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-sliders");
        yield return P(a, "inv_transfer", "Transfer", "Inventory", "Transfer orders between warehouses.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/stock-transfers-app", "fa-exchange");
        yield return P(a, "inv_ownership", "Inventory ownership change", "Inventory", "Change consignment or ownership of on-hand stock.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-handshake-o");
        yield return P(a, "inv_counting", "Counting", "Inventory", "Counting journals and recounts.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-check-square-o");
        yield return P(a, "inv_tag_counting", "Tag counting", "Inventory", "Tag-based physical inventory.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-tags");
        yield return P(a, "inv_inbound", "Inbound orders", "Inbound", "Expected receipts from purchase or transfer.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-sign-in");
        yield return P(a, "inv_arrival", "Arrival overview", "Inbound", "Arrivals waiting to be registered.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-truck");
        yield return P(a, "inv_outbound", "Outbound orders", "Outbound", "Shipments from sales or transfer.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-sign-out");
        yield return P(a, "inv_shipments", "Shipments", "Outbound", "Warehouse shipments.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-ship");
        yield return P(a, "inv_picking", "Picking workbench", "Outbound", "Pick work generated from outbound orders.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-hand-paper-o");
        yield return P(a, "inv_quality_orders", "Quality orders", "Quality", "Quality orders against a receipt or production.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/quality-app", "fa-check");
        yield return P(a, "inv_nonconformance", "Non conformance", "Quality", "Nonconformance cases against an item or lot.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exclamation-triangle");
        yield return P(a, "inv_quarantine", "Quarantine orders", "Quality", "Lots held in quarantine.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-lock");
        yield return P(a, "inv_batches", "Batches", "Batches", "Batch / lot master.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-barcode");
        yield return P(a, "inv_serials", "Serial numbers", "Batches", "Serial-number master.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-ol");
        yield return P(a, "inv_warehouses", "Warehouses", "Setup", "Warehouse master and locations.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/warehouses-app", "fa-building-o");
        yield return P(a, "inv_sites", "Sites", "Setup", "Sites that own warehouses.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-map-marker");
        yield return P(a, "inv_item_groups", "Item groups", "Setup", "Item groups that share inventory policy.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "inv_storage_dims", "Storage dimension groups", "Setup", "Site, warehouse, location, and license-plate dimensions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "inv_tracking_dims", "Tracking dimension groups", "Setup", "Batch and serial tracking.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-barcode");
        yield return P(a, "inv_parameters", "Inventory and warehouse management parameters", "Setup", "Company defaults for journals, counting, and reservations.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "inv_reservation", "Reservation hierarchy", "Setup", "How on-hand is reserved against demand.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-lock");
        yield return P(a, "inv_forecast", "Inventory forecast", "Forecast", "Demand forecast used by planning and reorder.", ErpD365Part3Catalog.FormKind.Forecast, app, "/erp/inventory-forecast-app", "fa-line-chart");
        yield return P(a, "inv_report", "Inventory report", "Inquiries and reports", "On-hand quantity and value by item.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/inventory-report-app", "fa-bar-chart");
        yield return P(a, "inv_aging", "Inventory aging", "Inquiries and reports", "On-hand aged by receipt date.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app", "fa-hourglass");
        yield return P(a, "inv_value_report", "Inventory value", "Inquiries and reports", "Inventory value by costing method.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-money");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> LandedCostPages()
    {
        const string a = "landed_cost_area";
        const string app = LandedCostApp;
        yield return P(a, "lc_voyages", "All voyages", "Voyages", "Ocean or air voyages that carry inbound cost.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/landed-cost-app", "fa-ship");
        yield return P(a, "lc_received_voyages", "Received voyages", "Voyages", "Voyages whose goods have been received.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-check");
        yield return P(a, "lc_containers", "All shipping containers", "Shipping containers", "Containers on a voyage.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-archive");
        yield return P(a, "lc_folios", "All folios", "Folios", "Folios that group costs on a voyage.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-folder-open-o");
        yield return P(a, "lc_inbound_tracking", "Inbound tracking", "Tracking", "Track inbound shipments until receipt.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-map-marker");
        yield return P(a, "lc_item_tracking", "Item tracking", "Tracking", "Track a purchased item across voyages.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "lc_container_tracking", "Individual shipping container tracking", "Tracking", "Track one container.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-archive");
        yield return P(a, "lc_multi_container", "Multiple shipping container tracking", "Tracking", "Track several containers on one voyage.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "lc_over_under", "Over/under transactions", "Inquiries", "Quantity over or under versus the purchase order.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "lc_voyage_lines", "Voyage lines", "Inquiries", "Purchase lines attached to a voyage.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "lc_cost_estimates", "Cost estimates", "Inquiries", "Estimated landed cost before actuals arrive.", ErpD365Part3Catalog.FormKind.Forecast, app, icon: "fa-calculator");
        yield return P(a, "lc_outstanding_invoices", "Outstanding invoices", "Reports", "Vendor invoices still expected on a voyage.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/payables-app", "fa-file-o");
        yield return P(a, "lc_activity_by_activity", "Analysis of activity/provider by activity", "Reports", "Landed-cost activity amounts by provider.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "lc_activity_by_provider", "Analysis of activity/provider by provider", "Reports", "Landed-cost provider amounts by activity.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "lc_voyage_costing", "Voyage costing by individual cost", "Costing", "Allocate voyage costs to items.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/landed-cost-app", "fa-money");
        yield return P(a, "lc_voyage_costing_cat", "Voyage costing by reporting category", "Costing", "Voyage cost by reporting category.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-pie-chart");
        yield return P(a, "lc_goods_in_transit", "Goods in transit orders", "Periodic", "In-transit inventory for a voyage.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-truck");
        yield return P(a, "lc_cost_estimate_run", "Cost estimate", "Periodic", "Recalculate estimated landed cost.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-calculator");
        yield return P(a, "lc_item_cost_update", "Item cost price update", "Periodic", "Push landed cost into the item cost price.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "lc_po_cleanup", "Purchase order clean up", "Clean up", "Clear voyage links from closed purchase orders.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "lc_parameters", "Landed cost parameters", "Setup", "Company defaults for voyages, folios, and costing.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "lc_voyage_status", "Voyage statuses", "Setup", "Statuses a voyage moves through.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flag");
        yield return P(a, "lc_cost_type_codes", "Cost type codes", "Setup", "Freight, insurance, duty, and other cost types.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "lc_vendor_cost_groups", "Vendor cost type groups", "Setup", "Which cost types a vendor may charge.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "lc_item_cost_groups", "Item cost type groups", "Setup", "Which cost types an item absorbs.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cubes");
        yield return P(a, "lc_over_under_tol", "Over/under tolerances", "Setup", "Quantity tolerance before an over/under is created.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "lc_shipping_ports", "Shipping ports", "Setup", "Ports used on a voyage.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-anchor");
        yield return P(a, "lc_vessels", "Vessels", "Setup", "Vessels used on a voyage.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-ship");
        yield return P(a, "lc_journey_templates", "Journey templates", "Setup", "Multi-leg journey templates.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-map");
    }

}
