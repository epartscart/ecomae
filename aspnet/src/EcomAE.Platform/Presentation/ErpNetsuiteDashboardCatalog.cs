using System.Globalization;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>erp_dashboard_netsuite.php</c> + <c>epc_erp_dashboard_profiles.php</c> catalogues
/// for the company ERP home. Role preview via <c>?dash_profile=</c> (admin / Super).
/// Jewellery tiles stay off unless the active company is jewellery.
/// </summary>
public static class ErpNetsuiteDashboardCatalog
{
    public sealed record Tile(string Key, string Label, string Icon, string Tone, string Href);

    public sealed record NavLink(string Label, string Icon, string Href);

    public sealed record NavGroup(string Title, string Tone, IReadOnlyList<NavLink> Links);

    public sealed record HeroMetric(string Label, decimal Current, decimal Previous, bool GoodWhenUp, bool Money);

    public sealed record KpiRow(string Name, decimal Current, decimal Previous, bool GoodWhenUp);

    public sealed record FinCell(string Label, string Value);

    public sealed record ChangeBadge(string Css, string Text);

    public sealed record IndustryControl(string Code, string Title, string Desc);

    public sealed record Profile(
        string Key,
        string Label,
        string Subtitle,
        string Icon,
        IReadOnlySet<string> Capabilities,
        IReadOnlyList<string> HeroKeys,
        IReadOnlyList<string> OpKpiKeys,
        IReadOnlyList<string> TileKeys,
        IReadOnlyList<string> QuickKeys);

    public static readonly IReadOnlyDictionary<string, Tile> Tiles = new Dictionary<string, Tile>(StringComparer.OrdinalIgnoreCase)
    {
        ["balance_sheet"] = new("balance_sheet", "Balance Sheet", "fa-balance-scale", "gold", "/erp/report-center-app?tab=balance_sheet"),
        ["gl"] = new("gl", "General Journal", "fa-book", "green", "/erp/gl-journals-app"),
        ["bank_recon"] = new("bank_recon", "Reconcile Bank", "fa-university", "rust", "/erp/bank-reconciliation-app"),
        ["pl"] = new("pl", "Income Statement", "fa-line-chart", "slate", "/erp/report-center-app?tab=pl"),
        ["sales_orders"] = new("sales_orders", "New Sales Order", "fa-shopping-cart", "gold", "/erp/sales-orders-app"),
        ["crm"] = new("crm", "CRM Pipeline", "fa-handshake-o", "green", "/cp/crm-tickets-app"),
        ["receivables"] = new("receivables", "Receivables", "fa-users", "rust", "/erp/receivables-app"),
        ["aging_ar"] = new("aging_ar", "A/R Aging", "fa-hourglass-half", "slate", "/erp/aging-app"),
        ["purchase_orders"] = new("purchase_orders", "New Purchase Order", "fa-clipboard", "gold", "/erp/purchase-orders-app"),
        ["payables"] = new("payables", "Payables", "fa-truck", "green", "/erp/payables-app"),
        ["three_way_match"] = new("three_way_match", "3-way Match", "fa-check-square-o", "rust", "/erp/three-way-match-app"),
        ["aging_ap"] = new("aging_ap", "A/P Aging", "fa-hourglass-half", "slate", "/erp/aging-app"),
        ["cash_bank"] = new("cash_bank", "Cash & bank", "fa-money", "green", "/erp/cash-accounts-app"),
        ["vat_return"] = new("vat_return", "VAT Return", "fa-percent", "rust", "/erp/vat-app?tab=vat_return"),
        ["inventory"] = new("inventory", "Inventory", "fa-cubes", "slate", "/erp/inventory-stock-app"),
        ["fulfilment"] = new("fulfilment", "Fulfilment", "fa-truck", "gold", "/cp/fulfillment-queue-app"),
        ["hr"] = new("hr", "Human resources", "fa-users", "green", "/cp/hr-overview-app"),
        ["payroll"] = new("payroll", "Payroll", "fa-credit-card", "rust", "/erp/payroll-app"),
        ["staff"] = new("staff", "Staff directory", "fa-id-badge", "slate", "/erp/staff-app"),
        ["workflow"] = new("workflow", "Workflow", "fa-tasks", "gold", "/erp/workflow-app"),
        ["marketing"] = new("marketing", "Marketing", "fa-bullhorn", "rust", "/erp/marketing-app"),
        ["processflow"] = new("processflow", "Process flow", "fa-sitemap", "green", "/erp/process-flow-tasks-app"),
        ["dashboard"] = new("dashboard", "Home", "fa-home", "slate", "/erp"),
        ["ext_ifrs"] = new("ext_ifrs", "Financial Report (IFRS)", "fa-file-text-o", "qa-indigo", "/erp/tax-external-reporting-app?cat=audit&rep=audit__external_audit_report&fetch=1"),
        ["ext_vat"] = new("ext_vat", "VAT Return (VAT 201)", "fa-percent", "qa-green", "/erp/tax-external-reporting-app?cat=tax&rep=tax__vat_return&fetch=1"),
        ["ext_ct"] = new("ext_ct", "Corporate Tax Return", "fa-balance-scale", "qa-rust", "/erp/tax-external-reporting-app?cat=tax&rep=tax__corporate_income_tax_return&fetch=1"),
        ["customers"] = new("customers", "New Customer", "fa-user-plus", "qa-pink", "/erp/receivables-app"),
        ["vendors"] = new("vendors", "New Vendor", "fa-truck", "qa-teal", "/erp/suppliers-app"),
        ["coa"] = new("coa", "Chart of accounts", "fa-list", "qa-slate", "/erp/coa-accounts-app"),
        ["leads"] = new("leads", "Leads", "fa-user-plus", "qa-amber", "/cp/crm-board-app"),
    };

    private static readonly IReadOnlyDictionary<string, string> TileNeed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["balance_sheet"] = "gl",
        ["gl"] = "gl",
        ["bank_recon"] = "cash",
        ["pl"] = "profit",
        ["sales_orders"] = "sales",
        ["crm"] = "sales",
        ["receivables"] = "ar",
        ["aging_ar"] = "aging_ar",
        ["purchase_orders"] = "purchases",
        ["payables"] = "ap",
        ["three_way_match"] = "purchases",
        ["aging_ap"] = "aging_ap",
        ["cash_bank"] = "cash",
        ["vat_return"] = "vat",
        ["inventory"] = "inventory",
        ["fulfilment"] = "sales",
        ["hr"] = "hr_tasks",
        ["payroll"] = "hr_tasks",
        ["staff"] = "hr_tasks",
        ["workflow"] = "hr_tasks",
        ["marketing"] = "sales",
        ["processflow"] = "hr_tasks",
        ["dashboard"] = "sales",
        ["ext_ifrs"] = "profit",
        ["ext_vat"] = "vat",
        ["ext_ct"] = "vat",
        ["customers"] = "ar",
        ["vendors"] = "ap",
        ["coa"] = "gl",
        ["leads"] = "sales",
    };

    public static readonly IReadOnlyDictionary<string, Profile> Profiles = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase)
    {
        ["ceo"] = P("ceo", "CEO centre", "Company-wide performance, cash and profit at a glance.", "fa-briefcase",
            Caps("profit", "cash", "sales", "purchases", "ar", "ap", "vat", "gl", "inventory", "aging_ar", "aging_ap", "exec", "suppliers", "hr_tasks", "financials", "op_kpis", "gauge"),
            ["cash", "sales", "profit", "ar"],
            ["revenue", "gross_margin", "dso", "dpo", "current_ratio", "ar", "ap", "cash", "inventory"],
            ["pl", "balance_sheet", "cash_bank", "sales_orders"],
            ["ext_ifrs", "ext_vat", "ext_ct", "sales_orders", "purchase_orders", "gl", "pl"]),
        ["cfo"] = P("cfo", "CFO centre", "Liquidity, margins, tax and financial control.", "fa-university",
            Caps("profit", "cash", "sales", "purchases", "ar", "ap", "vat", "gl", "inventory", "aging_ar", "aging_ap", "exec", "suppliers", "financials", "op_kpis", "gauge"),
            ["cash", "profit", "ar", "ap"],
            ["revenue", "gross_margin", "dso", "dpo", "current_ratio", "ar", "ap", "cash", "inventory"],
            ["pl", "balance_sheet", "bank_recon", "gl"],
            ["ext_ifrs", "ext_vat", "ext_ct", "gl", "vat_return", "cash_bank", "pl"]),
        ["finance"] = P("finance", "Finance centre", "Cash, AR/AP and statutory finance operations.", "fa-money",
            Caps("profit", "cash", "sales", "purchases", "ar", "ap", "vat", "gl", "inventory", "aging_ar", "aging_ap", "exec", "suppliers", "financials", "op_kpis", "gauge"),
            ["cash", "sales", "ar", "ap"],
            ["revenue", "gross_margin", "dso", "dpo", "ar", "ap", "cash", "inventory"],
            ["balance_sheet", "gl", "bank_recon", "pl"],
            ["ext_ifrs", "ext_vat", "ext_ct", "cash_bank", "gl", "vat_return", "receivables", "payables"]),
        ["accounts"] = P("accounts", "Accounts centre", "Ledgers, VAT and period close.", "fa-book",
            Caps("profit", "cash", "sales", "purchases", "ar", "ap", "vat", "gl", "aging_ar", "aging_ap", "financials", "op_kpis", "gauge"),
            ["cash", "sales", "purchases", "vat"],
            ["revenue", "gross_margin", "ar", "ap", "cash", "dso", "dpo"],
            ["gl", "pl", "balance_sheet", "vat_return"],
            ["gl", "pl", "balance_sheet", "vat_return", "coa", "cash_bank"]),
        ["sales"] = P("sales", "Sales centre", "Orders, pipeline and collections — commercial figures only.", "fa-line-chart",
            Caps("sales", "ar", "aging_ar", "op_kpis"),
            ["sales", "ar", "orders", "due"],
            ["revenue", "dso", "ar"],
            ["sales_orders", "crm", "receivables", "aging_ar"],
            ["sales_orders", "crm", "receivables", "customers"]),
        ["purchase"] = P("purchase", "Purchase centre", "Procurement, suppliers and payables.", "fa-shopping-basket",
            Caps("purchases", "ap", "inventory", "aging_ap", "suppliers", "op_kpis"),
            ["purchases", "ap", "inventory", "open_po"],
            ["dpo", "ap", "inventory", "inv_turnover"],
            ["purchase_orders", "payables", "three_way_match", "aging_ap"],
            ["purchase_orders", "payables", "vendors", "inventory"]),
        ["logistics"] = P("logistics", "Logistics centre", "Fulfilment, stock and open order movement.", "fa-truck",
            Caps("inventory", "sales", "purchases", "op_kpis"),
            ["orders", "open_po", "inventory", "sales"],
            ["inventory", "revenue", "inv_turnover"],
            ["fulfilment", "inventory", "sales_orders", "purchase_orders"],
            ["fulfilment", "inventory", "sales_orders", "purchase_orders"]),
        ["hr"] = P("hr", "HR centre", "People, payroll and department workload.", "fa-users",
            Caps("hr_tasks", "op_kpis"),
            ["staff_open", "staff_done", "staff_overdue", "staff_busy"],
            [],
            ["hr", "payroll", "staff", "workflow"],
            ["hr", "payroll", "staff", "workflow"]),
        ["marketing"] = P("marketing", "Marketing centre", "Campaigns and commercial pipeline support.", "fa-bullhorn",
            Caps("sales", "op_kpis"),
            ["sales", "orders", "ar", "due"],
            ["revenue", "ar"],
            ["marketing", "crm", "sales_orders", "workflow"],
            ["marketing", "crm", "sales_orders", "leads"]),
        ["it"] = P("it", "IT centre", "Access, workflow and system health.", "fa-laptop",
            Caps("hr_tasks"),
            ["staff_open", "staff_overdue", "staff_done", "staff_busy"],
            [],
            ["workflow", "staff", "processflow", "dashboard"],
            ["workflow", "staff", "processflow"]),
        ["admin"] = P("admin", "Administration centre", "Full operational and financial command view.", "fa-shield",
            Caps("profit", "cash", "sales", "purchases", "ar", "ap", "vat", "gl", "inventory", "aging_ar", "aging_ap", "exec", "suppliers", "hr_tasks", "financials", "op_kpis", "gauge"),
            ["cash", "sales", "profit", "ar"],
            ["revenue", "gross_margin", "dso", "dpo", "current_ratio", "ar", "ap", "cash", "inventory"],
            ["pl", "balance_sheet", "gl", "sales_orders"],
            ["ext_ifrs", "ext_vat", "ext_ct", "sales_orders", "purchase_orders", "gl", "staff"]),
    };

    public static Profile Resolve(string? requested, bool fullAdmin)
    {
        if (!string.IsNullOrWhiteSpace(requested)
            && Profiles.TryGetValue(requested.Trim(), out var preview)
            && fullAdmin)
        {
            return preview;
        }

        return fullAdmin ? Profiles["admin"] : Profiles["finance"];
    }

    public static bool Can(Profile profile, string capability)
        => profile.Capabilities.Contains(capability);

    public static IReadOnlyList<Tile> ResolveTiles(Profile profile)
    {
        var list = new List<Tile>();
        foreach (var key in profile.TileKeys)
        {
            if (!Tiles.TryGetValue(key, out var tile)) continue;
            if (!Can(profile, Need(key))) continue;
            list.Add(tile);
        }

        if (list.Count == 0)
        {
            list.Add(Tiles["dashboard"]);
        }

        return list;
    }

    public static IReadOnlyList<Tile> ResolveQuick(Profile profile)
    {
        var list = new List<Tile>();
        foreach (var key in profile.QuickKeys)
        {
            if (!Tiles.TryGetValue(key, out var tile)) continue;
            if (!Can(profile, Need(key))) continue;
            list.Add(tile with { Tone = ShortcutTone(tile.Tone) });
        }

        return list;
    }

    /// <summary>PHP shortcut strip uses <c>.ns-qa .qa-*</c>, not <c>.ns-tile.gold</c>.</summary>
    public static string ShortcutTone(string tone)
        => tone switch
        {
            "gold" => "qa-blue",
            "green" => "qa-teal",
            "rust" => "qa-rust",
            "slate" => "qa-slate",
            var t when t.StartsWith("qa-", StringComparison.Ordinal) => t,
            _ => "qa-slate",
        };

    public static IReadOnlyList<NavGroup> ResolveNav(Profile profile)
    {
        var groups = new List<NavGroup>();
        var lists = new List<NavLink>();
        if (Can(profile, "inventory")) lists.Add(new("Items", "fa-cubes", "/erp/inventory-stock-app"));
        if (Can(profile, "ar") || Can(profile, "sales")) lists.Add(new("Customers", "fa-users", "/erp/receivables-app"));
        if (Can(profile, "ap") || Can(profile, "purchases")) lists.Add(new("Vendors", "fa-truck", "/erp/payables-app"));
        lists.Add(new("Contacts", "fa-address-book-o", "/erp/contacts-app"));
        if (lists.Count > 0) groups.Add(new("Lists", "ns-mi-teal", lists));

        var tx = new List<NavLink>();
        if (Can(profile, "sales")) tx.Add(new("Sales order", "fa-shopping-cart", "/erp/sales-orders-app"));
        if (Can(profile, "purchases")) tx.Add(new("Purchase order", "fa-clipboard", "/erp/purchase-orders-app"));
        if (Can(profile, "cash")) tx.Add(new("Receipt voucher", "fa-money", "/erp/cash-accounts-app"));
        if (Can(profile, "gl")) tx.Add(new("General ledger", "fa-book", "/erp/gl-journals-app"));
        if (tx.Count > 0) groups.Add(new("Transactions", "ns-mi-blue", tx));

        var reports = new List<NavLink>();
        if (Can(profile, "gl") || Can(profile, "profit"))
        {
            reports.Add(new("Financial report (IFRS)", "fa-file-text-o", Tiles["ext_ifrs"].Href));
        }

        if (Can(profile, "vat"))
        {
            reports.Add(new("VAT return (VAT 201)", "fa-percent", Tiles["ext_vat"].Href));
            reports.Add(new("Corporate tax return", "fa-balance-scale", Tiles["ext_ct"].Href));
        }

        if (Can(profile, "profit")) reports.Add(new("Profit & loss", "fa-line-chart", Tiles["pl"].Href));
        if (reports.Count > 0) groups.Add(new("Reports", "ns-mi-amber", reports));

        return groups;
    }

    public static IReadOnlyList<HeroMetric> ResolveHero(Profile profile, ErpWorkspacePeriodKpis cur, ErpWorkspacePeriodKpis prev)
    {
        var catalog = new Dictionary<string, HeroMetric>(StringComparer.OrdinalIgnoreCase)
        {
            ["cash"] = new("Cash & bank", cur.CashPosition, prev.CashPosition, true, true),
            ["sales"] = new("Sales (ex VAT)", cur.RevenueExVat, prev.RevenueExVat, true, true),
            ["profit"] = new("Gross profit", cur.ProfitExVat, prev.ProfitExVat, true, true),
            ["ar"] = new("Receivables", cur.Receivables != 0 ? cur.Receivables : cur.ArBalance, prev.Receivables != 0 ? prev.Receivables : prev.ArBalance, true, true),
            ["ap"] = new("Payables", cur.Payables != 0 ? cur.Payables : cur.ApBalance, prev.Payables != 0 ? prev.Payables : prev.ApBalance, false, true),
            ["purchases"] = new("Purchases (ex VAT)", cur.PurchaseExVat, prev.PurchaseExVat, false, true),
            ["vat"] = new("Net VAT", cur.VatNetPayable, prev.VatNetPayable, false, true),
            ["orders"] = new("Completed orders", cur.OrdersCount, prev.OrdersCount, true, false),
            ["due"] = new("Due on orders", cur.ReceivableDueOrders, prev.ReceivableDueOrders, true, true),
            ["inventory"] = new("Inventory value", cur.StockValue, 0, true, true),
            ["open_po"] = new("Open purchase orders", cur.OpenPurchaseOrders, 0, false, false),
            ["staff_open"] = new("Open tasks", cur.ProcessOpen, 0, false, false),
            ["staff_done"] = new("Tasks completed", cur.ProcessDone, 0, true, false),
            ["staff_overdue"] = new("Overdue tasks", cur.ProcessOverdue, 0, false, false),
            ["staff_busy"] = new("Staff busy now", cur.ProcessBusy, 0, true, false),
        };

        var list = new List<HeroMetric>();
        foreach (var key in profile.HeroKeys)
        {
            if (!catalog.TryGetValue(key, out var metric)) continue;
            var need = key switch
            {
                "cash" => "cash",
                "sales" or "orders" => "sales",
                "profit" => "profit",
                "ar" or "due" => "ar",
                "ap" => "ap",
                "purchases" or "open_po" => "purchases",
                "vat" => "vat",
                "inventory" => "inventory",
                "staff_open" or "staff_done" or "staff_overdue" or "staff_busy" => "hr_tasks",
                _ => "sales",
            };
            if (!Can(profile, need)) continue;
            list.Add(metric);
        }

        return list;
    }

    public static IReadOnlyList<KpiRow> ResolveKpiRows(Profile profile, ErpWorkspacePeriodKpis cur, ErpWorkspacePeriodKpis prev)
    {
        var rows = new List<KpiRow>();
        if (Can(profile, "ap")) rows.Add(new("Payables", cur.Payables != 0 ? cur.Payables : cur.ApBalance, prev.Payables != 0 ? prev.Payables : prev.ApBalance, false));
        if (Can(profile, "sales")) rows.Add(new("Sales (ex VAT)", cur.RevenueExVat, prev.RevenueExVat, true));
        if (Can(profile, "purchases")) rows.Add(new("Expenses (purchases)", cur.PurchaseExVat, prev.PurchaseExVat, false));
        if (Can(profile, "ar")) rows.Add(new("Receivables", cur.Receivables != 0 ? cur.Receivables : cur.ArBalance, prev.Receivables != 0 ? prev.Receivables : prev.ArBalance, true));
        if (Can(profile, "cash")) rows.Add(new("Total bank balance", cur.CashPosition, prev.CashPosition, true));
        if (Can(profile, "profit")) rows.Add(new("Gross profit (ex VAT)", cur.ProfitExVat, prev.ProfitExVat, true));
        return rows;
    }

    public static IReadOnlyList<FinCell> ResolveFinancials(Profile profile, ErpWorkspacePeriodKpis cur, string currency)
    {
        var rev = cur.RevenueExVat;
        var prof = cur.ProfitExVat;
        var gpPct = rev > 0.005m ? (prof / rev) * 100m : 0m;
        var cells = new List<FinCell>();
        if (Can(profile, "profit"))
        {
            cells.Add(new("Gross profit %", gpPct.ToString("N1", CultureInfo.InvariantCulture) + "%"));
            cells.Add(new("Margin (ex VAT)", Money(prof) + " " + currency));
            cells.Add(new("GL net profit", Money(cur.GlNetProfit) + " " + currency));
        }

        if (Can(profile, "cash")) cells.Add(new("Cash & bank", Money(cur.CashPosition) + " " + currency));
        if (Can(profile, "vat")) cells.Add(new("Net VAT", Money(cur.VatNetPayable) + " " + currency));
        if (Can(profile, "sales"))
        {
            cells.Add(new("Sales incl. VAT", Money(cur.SalesInclVat) + " " + currency));
            cells.Add(new("Completed orders", cur.OrdersCount.ToString("N0", CultureInfo.InvariantCulture)));
        }

        if (Can(profile, "ar")) cells.Add(new("Due on orders", Money(cur.ReceivableDueOrders) + " " + currency));
        return cells;
    }

    public static ChangeBadge Change(decimal current, decimal previous, bool goodWhenUp)
    {
        if (Math.Abs((double)previous) < 0.005)
        {
            return new("ns-chg ns-flat", "—");
        }

        var pct = ((double)(current - previous) / Math.Abs((double)previous)) * 100.0;
        var up = pct >= 0;
        var good = up == goodWhenUp;
        var arrow = up ? "▲" : "▼";
        return new(good ? "ns-chg ns-up" : "ns-chg ns-down", arrow + " " + Math.Abs(pct).ToString("N1", CultureInfo.InvariantCulture) + "%");
    }

    public static string Money(decimal value)
        => value.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Inclusive calendar days for DSO/DPO. PHP <c>epc_bos_intelligence</c> uses
    /// <c>round((date_to - date_from) / 86400)</c> on unix bounds that include now,
    /// so the current calendar day counts. Midnight-to-midnight of yyyy-MM-dd strings
    /// would drop today.
    /// </summary>
    public static int PeriodDaysInclusive(string? fromYmd, string? toYmd)
    {
        if (DateTime.TryParse(fromYmd, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var from)
            && DateTime.TryParse(toYmd, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var to))
        {
            return Math.Max(1, (int)(to.Date - from.Date).TotalDays + 1);
        }

        return 1;
    }

    public static string DepartmentName(string? code)
    {
        var key = (code ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "" => "Unassigned",
            "sales" => "Sales",
            "purchase" or "purchasing" => "Purchasing",
            "finance" => "Finance",
            "accounts" => "Accounts",
            "logistics" => "Logistics",
            "warehouse" => "Warehouse",
            "hr" => "Human resources",
            "admin" => "Administration",
            "marketing" => "Marketing",
            "it" => "IT",
            _ => char.ToUpperInvariant(key[0]) + key[1..],
        };
    }

    public static IReadOnlyList<IndustryControl> IndustryControls(string industryCode, bool jewellery)
    {
        var list = new List<IndustryControl>
        {
            new("monthly_close", "Monthly close & reconciliation", "Reconcile bank, AR, AP and inventory each month before reporting."),
            new("segregation", "Segregation of duties", "Separate who raises, approves and pays a transaction."),
            new("approval_thresholds", "Approval thresholds enforced", "High-value POs/payments routed through the approval engine."),
            new("aging_review", "AR/AP aging review", "Review overdue receivables and payables weekly."),
            new("vat_filed", "Tax returns filed on time", "No overdue items in the compliance filing calendar."),
            new("stock_count", "Periodic stock count", "Cycle-count fast movers; full count at period end."),
        };

        if (string.Equals(industryCode, "auto_parts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(industryCode, "core", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new("vin_fitment", "VIN / fitment check on sales", "Confirm catalogue fitment before confirming a sales order."));
            list.Add(new("supersession", "Supersession review", "Keep superseded SKUs linked so stock and sales stay accurate."));
        }

        if (jewellery)
        {
            list.Add(new("metal_weighbridge", "Daily metal weight reconciliation", "Reconcile gold/stone weight in vs out by purity."));
        }

        return list;
    }

    public static IReadOnlyList<ErpWorkspaceOpKpi> OperationalKpis(Profile profile, ErpWorkspacePeriodKpis cur, int periodDays)
    {
        if (!Can(profile, "op_kpis")) return [];

        var days = Math.Max(1, periodDays); // inclusive calendar days (PHP round((to-from)/86400) includes today)
        var revenue = cur.RevenueExVat;
        var purchases = cur.PurchaseExVat;
        var profit = cur.ProfitExVat;
        var ar = cur.Receivables != 0 ? cur.Receivables : cur.ArBalance;
        var ap = cur.Payables != 0 ? cur.Payables : cur.ApBalance;
        var cash = cur.CashPosition;
        var inv = cur.StockValue;
        var grossMargin = revenue > 0 ? (profit / revenue) * 100m : 0m;
        var dso = revenue > 0 ? ar / (revenue / days) : 0m;
        var dpo = purchases > 0 ? ap / (purchases / days) : 0m;
        var currentRatio = ap > 0 ? (cash + ar + inv) / ap : 0m;

        var all = new Dictionary<string, ErpWorkspaceOpKpi>(StringComparer.OrdinalIgnoreCase)
        {
            ["revenue"] = new("revenue", "Revenue (period)", Money(revenue), "money", revenue > 0 ? "good" : "warn", "Net sales excl. VAT"),
            ["gross_margin"] = new("gross_margin", "Gross margin %", grossMargin.ToString("N1", CultureInfo.InvariantCulture) + "%", "pct", Health(grossMargin, 25, 12, true), "Profit / revenue"),
            ["dso"] = new("dso", "DSO (days sales outstanding)", dso.ToString("N0", CultureInfo.InvariantCulture), "days", Health(dso, 30, 60, false), "Lower is faster collection"),
            ["dpo"] = new("dpo", "DPO (days payable outstanding)", dpo.ToString("N0", CultureInfo.InvariantCulture), "days", Health(dpo, 45, 20, true), "Supplier payment cycle"),
            ["current_ratio"] = new("current_ratio", "Current ratio (approx)", currentRatio.ToString("N2", CultureInfo.InvariantCulture) + "x", "x", Health(currentRatio, 1.5m, 1.0m, true), "(Cash+AR+Inv) / AP"),
            ["ar"] = new("ar", "AR outstanding", Money(ar), "money", "info", "Customer ledger balance"),
            ["ap"] = new("ap", "AP outstanding", Money(ap), "money", "info", "Supplier ledger balance"),
            ["cash"] = new("cash", "Cash & bank", Money(cash), "money", cash >= 0 ? "good" : "bad", "Liquidity position"),
            ["inventory"] = new("inventory", "Inventory value", Money(inv), "money", "info", "Stock at weighted-avg cost"),
            ["inv_turnover"] = new("inv_turnover", "Inventory turnover",
                (inv > 0 ? purchases / inv : 0m).ToString("N2", CultureInfo.InvariantCulture) + "x",
                "x", Health(inv > 0 ? purchases / inv : 0m, 4, 2, true), "Purchases / inventory value"),
        };

        var keys = profile.OpKpiKeys.Count > 0 ? profile.OpKpiKeys : all.Keys.ToArray();
        var list = new List<ErpWorkspaceOpKpi>();
        foreach (var key in keys)
        {
            if (!all.TryGetValue(key, out var kpi)) continue;
            if (!Can(profile, "profit") && (key is "gross_margin" || kpi.Label.Contains("margin", StringComparison.OrdinalIgnoreCase) || kpi.Label.Contains("profit", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            list.Add(kpi);
        }

        return list;
    }

    private static string Health(decimal value, decimal good, decimal warn, bool higherBetter)
    {
        if (higherBetter)
        {
            if (value >= good) return "good";
            if (value >= warn) return "warn";
            return "bad";
        }

        if (value <= good) return "good";
        if (value <= warn) return "warn";
        return "bad";
    }

    private static string Need(string tileKey)
        => TileNeed.TryGetValue(tileKey, out var need) ? need : "sales";

    private static HashSet<string> Caps(params string[] keys)
        => new(keys, StringComparer.OrdinalIgnoreCase);

    private static Profile P(
        string key,
        string label,
        string subtitle,
        string icon,
        HashSet<string> caps,
        string[] hero,
        string[] op,
        string[] tiles,
        string[] quick)
        => new(key, label, subtitle, icon, caps, hero, op, tiles, quick);
}
