namespace EcomAE.Platform.Presentation;

/// <summary>
/// Operating cycle for every ERP module area. Built once from the module directory.
/// The home draws a single area at a time; nothing here reads the database.
/// </summary>
public static class ErpProcessFlowCatalog
{
    public const string DefaultAreaId = "purchasing";

    public sealed record Step(string Label, string Icon, string Href);

    public sealed record AreaFlow(
        string Id,
        string CategoryId,
        string CategoryLabel,
        string Label,
        string Icon,
        string Summary,
        IReadOnlyList<Step> Steps,
        IReadOnlyList<Step> JewellerySteps)
    {
        public IReadOnlyList<Step> Visible(bool jewellery)
        {
            if (!jewellery || JewellerySteps.Count == 0)
            {
                return Steps;
            }

            var all = new Step[Steps.Count + JewellerySteps.Count];
            for (var i = 0; i < Steps.Count; i++)
            {
                all[i] = Steps[i];
            }

            for (var i = 0; i < JewellerySteps.Count; i++)
            {
                all[Steps.Count + i] = JewellerySteps[i];
            }

            return all;
        }
    }

    public sealed record FlowGroup(string Id, string Label, IReadOnlyList<AreaFlow> Areas);

    private static readonly IReadOnlyDictionary<string, string[]> CategoryAreas =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = ["overview"],
            ["record_to_report"] = ["finance", "budgeting", "consolidations", "cost_acct", "audit_wb", "fixed_assets"],
            ["procure_to_pay"] = ["purchasing", "ap", "landed_cost_area", "expense", "rebate_mgmt"],
            ["order_to_cash"] = ["sales", "ar", "credit_coll", "retail", "service_mgmt"],
            ["cash_treasury"] = ["banking"],
            ["inventory_fulfilment"] =
            [
                "inventory_mgmt", "pim", "cost_mgmt", "production", "warehouse", "mhei", "logistics",
                "master_planning_area", "asset_mgmt", "transportation",
            ],
            ["hr_payroll"] = ["people", "payroll_area", "leave_abs", "projects", "time_att"],
            ["compliance_tax"] = ["tax", "risk"],
            ["setup_admin"] = ["setup", "enterprise", "common", "questionnaire"],
        };

    private static readonly Dictionary<string, Step[]> Spine = new(StringComparer.OrdinalIgnoreCase)
    {
        ["overview"] =
        [
            S("Dashboard", "fa-dashboard", "/erp/dashboard-summary-app"),
            S("Workflow", "fa-tasks", "/erp/workflow-app"),
            S("Approvals", "fa-check-square-o", "/erp/approvals-app"),
            S("Automation", "fa-magic", "/erp/workflows-app"),
            S("Favourites", "fa-star", "/erp/workspace-favorites-app"),
        ],
        ["finance"] =
        [
            S("Chart of accounts", "fa-list", "/erp/coa-accounts-app"),
            S("Opening balances", "fa-flag-o", "/erp/opening-app"),
            S("Journals", "fa-book", "/erp/gl-journals-app"),
            S("Trial balance", "fa-table", "/erp/trial-balance-app"),
            S("Profit and loss", "fa-bar-chart", "/erp/report-center-app?tab=pl"),
            S("Balance sheet", "fa-balance-scale", "/erp/report-center-app?tab=balance_sheet"),
            S("Period close", "fa-calendar-check-o", "/erp/period-close-app"),
        ],
        ["budgeting"] =
        [
            S("Budget plan", "fa-line-chart", "/erp/budgets-app"),
            S("Budget register", "fa-pie-chart", "/erp/budgeting-workspace-app"),
            S("Actuals", "fa-bar-chart", "/erp/report-center-app?tab=pl"),
            S("Budget control", "fa-sliders", "/erp/fin-advanced-app"),
        ],
        ["consolidations"] =
        [
            S("Legal entities", "fa-building-o", "/erp/organization-app?tab=legal_entities"),
            S("Consolidate", "fa-sitemap", "/erp/consolidations-app"),
            S("Eliminations", "fa-exchange", "/erp/intercompany-app?tab=elimination"),
            S("Group reports", "fa-table", "/erp/trial-balance-app"),
        ],
        ["cost_acct"] =
        [
            S("Allocations", "fa-sliders", "/erp/cost-accounting-app"),
            S("Accruals", "fa-calculator", "/erp/fin-advanced-app"),
            S("Cost analysis", "fa-bar-chart", "/erp/report-center-app"),
            S("Ledger", "fa-book", "/erp/gl-journals-app"),
        ],
        ["audit_wb"] =
        [
            S("Audit policies", "fa-gavel", "/erp/audit-workbench-app"),
            S("Audit trail", "fa-history", "/erp/audit-trail-app"),
            S("Proofs", "fa-link", "/erp/blockchain-proofs-app"),
            S("Close review", "fa-check", "/erp/period-close-app"),
        ],
        ["fixed_assets"] =
        [
            S("Asset register", "fa-building", "/erp/fixed-assets-app"),
            S("Acquire", "fa-plus", "/erp/fixed-assets-workspace-app"),
            S("Depreciate", "fa-line-chart", "/erp/fixed-assets-app?tab=depreciate"),
            S("Dispose", "fa-sign-out", "/erp/fixed-assets-workspace-app?tab=disposal"),
        ],
        ["purchasing"] =
        [
            S("Suppliers", "fa-handshake-o", "/erp/suppliers-app"),
            S("Requisitions", "fa-list-alt", "/erp/purchase-requests-app"),
            S("Request for quote", "fa-envelope-o", "/erp/rfq-app"),
            S("Purchase orders", "fa-clipboard", "/erp/purchase-orders-app"),
            S("Receive and bill", "fa-cube", "/erp/purchases-app"),
            S("Three-way match", "fa-check-square-o", "/erp/three-way-match-app"),
            S("Pay bills", "fa-money", "/erp/payables-app"),
        ],
        ["ap"] =
        [
            S("Vendor setup", "fa-credit-card", "/erp/suppliers-app?tab=ap_setup"),
            S("Enter bills", "fa-file-text", "/erp/purchases-app"),
            S("Match invoices", "fa-check-square-o", "/erp/three-way-match-app"),
            S("Pay bills", "fa-money", "/erp/payables-app"),
            S("Vendor aging", "fa-hourglass-half", "/erp/aging-app?tab=ap"),
        ],
        ["landed_cost_area"] =
        [
            S("Voyage", "fa-ship", "/erp/landed-cost-app"),
            S("Charges", "fa-plus", "/erp/landed-cost-workspace-app"),
            S("Allocate", "fa-balance-scale", "/erp/landed-cost-app?tab=allocate"),
            S("Inventory cost", "fa-cubes", "/erp/cost-models-app"),
        ],
        ["expense"] =
        [
            S("Expense report", "fa-credit-card", "/erp/expense-reports-app"),
            S("Approve", "fa-check", "/erp/approvals-app"),
            S("Reimburse", "fa-money", "/erp/payment-batches-app"),
            S("Post to ledger", "fa-book", "/erp/gl-journals-app"),
        ],
        ["rebate_mgmt"] =
        [
            S("Rebate deal", "fa-percent", "/erp/rebate-workspace-app"),
            S("Accrue", "fa-calculator", "/erp/rebate-workspace-app?tab=accrue"),
            S("Claim", "fa-file-text-o", "/erp/purchases-app"),
            S("Settle", "fa-money", "/erp/payables-app"),
        ],
        ["sales"] =
        [
            S("Prospects", "fa-user-plus", "/erp/crm-board-app"),
            S("Opportunities", "fa-filter", "/erp/crm-opportunities-app"),
            S("Estimates", "fa-file-text", "/erp/sales-quotations-app"),
            S("Sales orders", "fa-shopping-cart", "/erp/sales-orders-app"),
            S("Delivery", "fa-truck", "/erp/delivery-notes-app"),
            S("Create invoices", "fa-file-text-o", "/erp/invoices-app"),
            S("Receive payments", "fa-credit-card", "/erp/receivables-app"),
            S("Statements", "fa-files-o", "/erp/aging-app"),
        ],
        ["ar"] =
        [
            S("Customer setup", "fa-handshake-o", "/erp/contacts-app?tab=ar_setup"),
            S("Invoices", "fa-file-text-o", "/erp/invoices-app"),
            S("Receive payments", "fa-money", "/erp/receivables-app"),
            S("Statements", "fa-file-o", "/erp/aging-app"),
            S("Collections", "fa-phone", "/erp/collections-dunning-app"),
        ],
        ["credit_coll"] =
        [
            S("Credit review", "fa-user", "/erp/contacts-app"),
            S("Collections", "fa-gavel", "/erp/collections-dunning-app"),
            S("Aging", "fa-hourglass-half", "/erp/aging-app?tab=ar"),
            S("Statements", "fa-file-o", "/erp/aging-app"),
        ],
        ["retail"] =
        [
            S("Channels", "fa-share-alt", "/erp/retail-commerce-app?tab=channels"),
            S("Barcode", "fa-barcode", "/erp/retail-commerce-app?tab=barcode"),
            S("Daily sales", "fa-shopping-cart", "/erp/cash-entries-app"),
            S("Card payments", "fa-credit-card", "/erp/payment-batches-app"),
        ],
        ["service_mgmt"] =
        [
            S("Agreements", "fa-file-text-o", "/erp/contracts-app"),
            S("Service orders", "fa-life-ring", "/erp/service-workspace-app"),
            S("Returns", "fa-undo", "/erp/returns-rma-app"),
            S("Invoice", "fa-file-text-o", "/erp/invoices-app"),
        ],
        ["banking"] =
        [
            S("Bank accounts", "fa-university", "/erp/cash-accounts-app"),
            S("Record deposits", "fa-plus-square", "/erp/cash-entries-app"),
            S("Payments", "fa-send", "/erp/payment-batches-app"),
            S("Petty cash", "fa-money", "/erp/cash-accounts-app?tab=petty_cash"),
            S("Reconcile", "fa-check-square", "/erp/bank-reconciliation-app"),
            S("Cash forecast", "fa-area-chart", "/erp/cash-accounts-app?tab=cash_forecast"),
        ],
        ["inventory_mgmt"] =
        [
            S("Items and services", "fa-barcode", "/erp/product-info-app"),
            S("Receive stock", "fa-cube", "/erp/inventory-stock-app"),
            S("Movements", "fa-random", "/erp/stock-movements-app"),
            S("Transfers", "fa-exchange", "/erp/stock-transfers-app"),
            S("Plan orders", "fa-cubes", "/erp/order-planning-app"),
            S("Stock report", "fa-bar-chart", "/erp/inventory-report-app"),
        ],
        ["pim"] =
        [
            S("Product information", "fa-cube", "/erp/product-info-app"),
            S("Variants", "fa-th", "/erp/pim-workspace-app"),
            S("Barcodes", "fa-barcode", "/erp/retail-commerce-app?tab=barcode"),
            S("Publish", "fa-check", "/erp/pim-workspace-app?tab=release"),
        ],
        ["cost_mgmt"] =
        [
            S("Cost models", "fa-balance-scale", "/erp/cost-models-app"),
            S("Inventory value", "fa-cubes", "/erp/inventory-report-app"),
            S("Analysis", "fa-bar-chart", "/erp/cost-management-app"),
            S("Ledger", "fa-book", "/erp/gl-journals-app"),
        ],
        ["production"] =
        [
            S("Plan", "fa-calendar", "/erp/production-overview-app?tab=mfg_planning"),
            S("Production order", "fa-cogs", "/erp/production-overview-app"),
            S("Quality", "fa-check-circle", "/erp/quality-app"),
            S("Receive finished", "fa-cube", "/erp/inventory-stock-app"),
        ],
        ["warehouse"] =
        [
            S("Warehouses", "fa-building-o", "/erp/warehouses-app"),
            S("Warehouse work", "fa-cubes", "/erp/warehouse-wms-app"),
            S("Waves", "fa-random", "/erp/warehouse-workspace-app"),
            S("RFID", "fa-wifi", "/erp/rfid-app"),
        ],
        ["mhei"] =
        [
            S("Equipment", "fa-cubes", "/erp/mhei-app"),
            S("Direct work", "fa-cogs", "/erp/warehouse-wms-app"),
            S("Confirm", "fa-check", "/erp/warehouse-workspace-app"),
            S("Stock", "fa-cube", "/erp/inventory-stock-app"),
        ],
        ["logistics"] =
        [
            S("Shipping", "fa-ship", "/erp/carriers-app"),
            S("Customs", "fa-file-text-o", "/erp/custom-shipping-reports"),
            S("Inbound", "fa-download", "/erp/purchase-orders-app"),
            S("Outbound", "fa-truck", "/erp/delivery-notes-app"),
        ],
        ["master_planning_area"] =
        [
            S("Forecast", "fa-line-chart", "/erp/inventory-forecast-app"),
            S("Master plan", "fa-random", "/erp/master-planning-app"),
            S("Planned orders", "fa-clipboard", "/erp/order-planning-app"),
            S("Purchase orders", "fa-shopping-basket", "/erp/purchase-orders-app"),
        ],
        ["asset_mgmt"] =
        [
            S("Asset register", "fa-wrench", "/erp/asset-management-app"),
            S("Maintain", "fa-cogs", "/erp/fixed-assets-app"),
            S("Work orders", "fa-wrench", "/erp/asset-management-app?tab=maintenance"),
            S("History", "fa-history", "/erp/audit-trail-app"),
        ],
        ["transportation"] =
        [
            S("Loads", "fa-truck", "/erp/transportation-workspace-app"),
            S("Shipments", "fa-ship", "/erp/carriers-app"),
            S("Freight bill", "fa-file-text-o", "/erp/purchases-app"),
            S("Delivery", "fa-map-marker", "/erp/delivery-notes-app"),
        ],
        ["people"] =
        [
            S("Workers", "fa-id-badge", "/erp/staff-app"),
            S("Recruit", "fa-user-plus", "/erp/recruitment-app"),
            S("Performance", "fa-star-half-o", "/erp/performance-app"),
            S("Leave", "fa-calendar-minus-o", "/erp/leave-absence-app"),
            S("Compliance", "fa-gavel", "/erp/hr-overview-app?hv=statutory"),
        ],
        ["payroll_area"] =
        [
            S("Time", "fa-clock-o", "/erp/time-attendance-app"),
            S("Payroll", "fa-users", "/erp/payroll-app"),
            S("Pay employees", "fa-money", "/erp/payroll-workspace-app"),
            S("Pay liabilities", "fa-bank", "/erp/payroll-workspace-app?tab=liabilities"),
            S("Insurance", "fa-shield", "/erp/insurance-compliance-app"),
        ],
        ["leave_abs"] =
        [
            S("Leave plans", "fa-calendar", "/erp/leave-absence-app"),
            S("Requests", "fa-calendar-minus-o", "/erp/hr-overview-app?tab=hr_ops"),
            S("Approve", "fa-check", "/erp/approvals-app"),
            S("Payroll", "fa-money", "/erp/payroll-app"),
        ],
        ["projects"] =
        [
            S("Projects", "fa-tasks", "/erp/projects-overview-app"),
            S("Budget and WIP", "fa-pie-chart", "/erp/project-accounting-app"),
            S("Time", "fa-clock-o", "/erp/time-attendance-app"),
            S("Invoice", "fa-file-text-o", "/erp/invoices-app"),
            S("Property", "fa-building", "/erp/real-estate-app"),
        ],
        ["time_att"] =
        [
            S("Register time", "fa-clock-o", "/erp/time-attendance-app"),
            S("Approve", "fa-check", "/erp/approvals-app"),
            S("Transfer to payroll", "fa-exchange", "/erp/payroll-app"),
            S("Attendance", "fa-calendar", "/erp/hr-overview-app"),
        ],
        ["tax"] =
        [
            S("E-invoice", "fa-file-code-o", "/erp/einvoice-documents-app"),
            S("VAT return", "fa-percent", "/erp/vat-app?tab=vat_return"),
            S("Withholding", "fa-scissors", "/erp/withholding-app"),
            S("Compliance", "fa-gavel", "/erp/uae-tax-compliance-app"),
            S("File return", "fa-shield", "/erp/tax-workspace-app"),
        ],
        ["risk"] =
        [
            S("Policies", "fa-shield", "/erp/insurance-compliance-app"),
            S("Claims", "fa-file-text-o", "/erp/insurance-compliance-app?tab=claims"),
            S("Document expiry", "fa-calendar-times-o", "/erp/doc-expiry-app"),
            S("Review", "fa-check", "/erp/audit-trail-app"),
        ],
        ["setup"] =
        [
            S("Company", "fa-sliders", "/erp/tenant-config-app"),
            S("Users", "fa-users", "/erp/users-app"),
            S("Security", "fa-lock", "/erp/user-control-app"),
            S("Import", "fa-upload", "/erp/data-migrations-app"),
            S("Print designs", "fa-paint-brush", "/erp/print-designer-app"),
        ],
        ["enterprise"] =
        [
            S("Business unit", "fa-sitemap", "/erp/organization-app?tab=business_units"),
            S("Legal entities", "fa-building-o", "/erp/organization-app?tab=legal_entities"),
            S("Contracts", "fa-file-text-o", "/erp/contracts-app"),
            S("Documents", "fa-files-o", "/erp/documents-app"),
        ],
        ["common"] =
        [
            S("Agenda", "fa-calendar", "/erp/agenda-app"),
            S("Contacts", "fa-address-book-o", "/erp/contacts-app"),
            S("Documents", "fa-folder-open-o", "/erp/documents-app"),
            S("Attachments", "fa-paperclip", "/erp/doc-attachments-app"),
        ],
        ["questionnaire"] =
        [
            S("Design", "fa-question-circle", "/erp/questionnaire-workspace-app"),
            S("Contacts", "fa-address-book-o", "/erp/contacts-app"),
            S("Distribute", "fa-envelope-o", "/erp/questionnaire-workspace-app?tab=distribute"),
            S("Results", "fa-bar-chart", "/erp/questionnaire-workspace-app?tab=results"),
        ],
    };

    static ErpProcessFlowCatalog()
    {
        var areasById = PhpModuleCatalog.ErpAreas.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        var tabsByArea = new Dictionary<string, List<PhpModuleCatalog.ModuleLink>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in PhpModuleCatalog.ErpTabs)
        {
            var group = tab.Group ?? string.Empty;
            if (!tabsByArea.TryGetValue(group, out var bucket))
            {
                bucket = [];
                tabsByArea[group] = bucket;
            }

            bucket.Add(tab);
        }

        var groups = new List<FlowGroup>();
        var flat = new List<AreaFlow>();
        var seenAreas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in PhpModuleCatalog.ErpCategories)
        {
            if (!CategoryAreas.TryGetValue(category.Id, out var areaIds))
            {
                continue;
            }

            var flows = new List<AreaFlow>();
            foreach (var areaId in areaIds)
            {
                if (!seenAreas.Add(areaId) || !areasById.TryGetValue(areaId, out var area))
                {
                    continue;
                }

                tabsByArea.TryGetValue(areaId, out var tabs);
                flows.Add(BuildArea(category.Id, category.Label, area, tabs));
            }

            if (flows.Count > 0)
            {
                groups.Add(new FlowGroup(category.Id, category.Label, flows));
                flat.AddRange(flows);
            }
        }

        var leftovers = new List<AreaFlow>();
        foreach (var area in PhpModuleCatalog.ErpAreas)
        {
            if (!seenAreas.Add(area.Id))
            {
                continue;
            }

            tabsByArea.TryGetValue(area.Id, out var tabs);
            leftovers.Add(BuildArea("setup_admin", "Setup & Administration", area, tabs));
        }

        if (leftovers.Count > 0)
        {
            groups.Add(new FlowGroup("more", "More areas", leftovers));
            flat.AddRange(leftovers);
        }

        Groups = groups;
        Areas = flat;
    }

    public static IReadOnlyList<FlowGroup> Groups { get; }

    public static IReadOnlyList<AreaFlow> Areas { get; }

    private static AreaFlow BuildArea(
        string categoryId,
        string categoryLabel,
        PhpModuleCatalog.ModuleLink area,
        List<PhpModuleCatalog.ModuleLink>? tabs)
    {
        var steps = new List<Step>();
        var jewellery = new List<Step>();
        var seenHref = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenLabel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Spine.TryGetValue(area.Id, out var spine))
        {
            foreach (var step in spine)
            {
                AddStep(steps, seenHref, seenLabel, step);
            }
        }

        if (tabs is not null)
        {
            foreach (var tab in tabs)
            {
                var step = S(tab.Label, Icon(tab.Icon), MapHref(tab.Href));
                if (tab.IsJewellery)
                {
                    AddStep(jewellery, seenHref, seenLabel, step);
                }
                else
                {
                    AddStep(steps, seenHref, seenLabel, step);
                }
            }
        }

        if (steps.Count == 0)
        {
            AddStep(steps, seenHref, seenLabel, S(area.Label, Icon(area.Icon), MapHref(area.Href)));
        }

        return new AreaFlow(
            area.Id,
            categoryId,
            categoryLabel,
            area.Label,
            Icon(area.Icon),
            string.IsNullOrWhiteSpace(area.Description) ? area.Label : area.Description!,
            steps,
            jewellery);
    }

    private static void AddStep(List<Step> steps, HashSet<string> seenHref, HashSet<string> seenLabel, Step step)
    {
        if (string.IsNullOrWhiteSpace(step.Label) || string.IsNullOrWhiteSpace(step.Href))
        {
            return;
        }

        if (!step.Href.StartsWith("/erp", StringComparison.OrdinalIgnoreCase)
            || step.Href.Contains("epc_erp_shell", StringComparison.OrdinalIgnoreCase)
            || step.Href.Contains("php-reference", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (seenHref.Contains(step.Href) || seenLabel.Contains(step.Label))
        {
            return;
        }

        seenHref.Add(step.Href);
        seenLabel.Add(step.Label);
        steps.Add(step);
    }

    private static Step S(string label, string icon, string href)
        => new(label.Trim(), Icon(icon), href.Trim());

    private static string Icon(string? icon)
    {
        var value = (icon ?? string.Empty).Trim();
        if (value.StartsWith("fa ", StringComparison.Ordinal))
        {
            value = value[3..].Trim();
        }

        return value.StartsWith("fa-", StringComparison.Ordinal) ? value : "fa-circle-o";
    }

    private static string MapHref(string? phpHref)
    {
        var href = PhpSurfaceLinkMap.AspNetPrimaryHref(phpHref);
        href = ErpPhpTabRouteMap.PreferErpSurface(href);
        return string.IsNullOrWhiteSpace(href) ? "/erp" : href;
    }
}
