namespace EcomAE.Platform.Presentation;

/// <summary>
/// Dynamics-style workspace pages taken from the Leave, Master planning, Material handling,
/// Organization administration, Payroll, and Procurement module menus.
/// Existing dedicated apps stay linked; missing pages land on the workspace writer.
/// </summary>
public static class ErpD365Part3Catalog
{
    public const string LeaveApp = "/erp/leave-absence-app";
    public const string PlanningApp = "/erp/master-planning-app";
    public const string MheiApp = "/erp/mhei-app";
    public const string OrgApp = "/erp/organization-admin-app";
    public const string PayrollApp = "/erp/payroll-workspace-app";
    public const string ProcurementApp = "/erp/procurement-workspace-app";
    public const string SavePath = "/erp/d365-workspace/save";

    public enum FormKind
    {
        Request,
        Plan,
        Calendar,
        Order,
        Forecast,
        Queue,
        Master,
        Action,
        Payroll,
        Procurement,
    }

    public sealed record Page(
        string Area,
        string Tab,
        string Label,
        string Group,
        string Purpose,
        FormKind Kind,
        string AppPath,
        string? ExistingHref = null,
        string Icon = "fa-th-large");

    public static readonly IReadOnlyList<Page> All = Build();

    public static IReadOnlyList<Page> ForArea(string? area)
    {
        var key = (area ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return All;
        }

        return All.Where(p => p.Area.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static bool TryGet(string? tab, out Page page)
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
            "leave_abs" => LeaveApp,
            "master_planning_area" => PlanningApp,
            "mhei" => MheiApp,
            "enterprise" => OrgApp,
            "payroll_area" => PayrollApp,
            "purchasing" => ProcurementApp,
            _ => LeaveApp,
        };

    public static string DefaultTab(string? area)
        => ForArea(area).FirstOrDefault()?.Tab ?? "time_off_requests";

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

    private static IReadOnlyList<Page> Build()
    {
        var rows = new List<Page>(220);
        rows.AddRange(LeavePages());
        rows.AddRange(PlanningPages());
        rows.AddRange(MheiPages());
        rows.AddRange(OrgPages());
        rows.AddRange(PayrollPages());
        rows.AddRange(ProcurementPages());
        return rows;
    }

    private static Page P(
        string area,
        string tab,
        string label,
        string group,
        string purpose,
        FormKind kind,
        string app,
        string? existing = null,
        string icon = "fa-th-large")
        => new(area, tab, label, group, purpose, kind, app, existing, icon);

    private static IEnumerable<Page> LeavePages()
    {
        const string a = "leave_abs";
        const string app = LeaveApp;
        yield return P(a, "leave_accrue", "Accrue leave and absence plans", "Manage leave", "Run accrual for enrolled workers against the active leave plan.", FormKind.Action, app, icon: "fa-plus-circle");
        yield return P(a, "leave_delete_accruals", "Delete leave and absence plan accruals", "Manage leave", "Reverse a posted accrual batch when the plan or period was wrong.", FormKind.Action, app, icon: "fa-minus-circle");
        yield return P(a, "leave_accrual_audit", "Leave accrual audit", "Manage leave", "Inspect posted accrual units, source plan, and worker balances.", FormKind.Master, app, icon: "fa-search");
        yield return P(a, "time_off_requests", "Time off requests", "Manage leave", "Request, approve, and track time off against the leave plan.", FormKind.Request, app, icon: "fa-calendar-check-o");
        yield return P(a, "loa_requests", "Leave of absence requests", "Manage leave", "Longer leave of absence with start, expected return, and status.", FormKind.Request, app, icon: "fa-suitcase");
        yield return P(a, "buy_sell_leave", "Buy and sell leave requests", "Manage leave", "Buy extra leave or sell unused units under the policy.", FormKind.Request, app, icon: "fa-exchange");
        yield return P(a, "leave_company_calendar", "Leave and absence company calendar", "Manage leave", "Company-wide leave calendar used by request validation.", FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "leave_accrual_suspend", "Leave and absence accrual suspensions", "Manage leave", "Pause accrual for a worker while they are inactive or on unpaid leave.", FormKind.Request, app, icon: "fa-pause");
        yield return P(a, "leave_employees", "Employees", "Enrollment", "Enroll employees on leave plans and track remaining balances.", FormKind.Master, app, "/erp/staff-app", "fa-id-badge");
        yield return P(a, "leave_contractors", "Contractors", "Enrollment", "Enroll contractors when the leave policy covers them.", FormKind.Master, app, icon: "fa-user");
        yield return P(a, "leave_plans", "Leave and absence plans", "Setup", "Plan header: accrual method, carry-forward, and grant units.", FormKind.Plan, app, icon: "fa-list-alt");
        yield return P(a, "leave_types", "Leave and absence types", "Setup", "Annual, sick, unpaid, maternity, and custom leave types.", FormKind.Plan, app, icon: "fa-tags");
        yield return P(a, "leave_parameters", "Leave and absence parameters", "Setup", "Company defaults for request approval and accrual frequency.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "leave_hr_parameters", "Human resources parameters", "Setup", "Shared HR defaults used by leave enrollment and calendars.", FormKind.Master, app, icon: "fa-cogs");
        yield return P(a, "leave_hr_workflows", "Human resource workflows", "Setup", "Approval workflow for time-off and leave-of-absence requests.", FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "buy_sell_policy", "Buy and sell leave policy", "Setup", "Caps, price, and eligibility for buying or selling leave.", FormKind.Plan, app, icon: "fa-balance-scale");
        yield return P(a, "leave_calendars", "Calendars", "Calendars", "Working calendars that drive leave day calculations.", FormKind.Calendar, app, icon: "fa-calendar-o");
        yield return P(a, "leave_holidays", "Holidays and closures", "Calendars", "Public holidays and company closures excluded from leave days.", FormKind.Calendar, app, icon: "fa-flag");
        yield return P(a, "leave_nonwork", "Non-work time", "Calendars", "Recurring non-work windows such as weekends or shutdowns.", FormKind.Calendar, app, icon: "fa-clock-o");
    }

    private static IEnumerable<Page> PlanningPages()
    {
        const string a = "master_planning_area";
        const string app = PlanningApp;
        yield return P(a, "mp_workspace", "Master planning", "Workspaces", "Workspace for planned orders, coverage, and firming.", FormKind.Order, app, "/erp/order-planning-app?tab=master_planning", "fa-random");
        yield return P(a, "ddmrp_workspace", "Demand driven MRP", "Workspaces", "Decoupling-point buffers, net flow, and DDMRP planned orders.", FormKind.Order, app, icon: "fa-line-chart");
        yield return P(a, "planned_orders", "Planned orders", "Planned orders", "Regenerated planned purchase and production orders.", FormKind.Order, app, icon: "fa-list");
        yield return P(a, "planned_orders_simple", "Planned orders simplified", "Planned orders", "Short list of planned orders for buyer or planner review.", FormKind.Order, app, icon: "fa-list-ul");
        yield return P(a, "calculated_delays", "Calculated delays", "Planned orders", "Orders that cannot meet the required date after netting.", FormKind.Order, app, icon: "fa-hourglass-half");
        yield return P(a, "po_actions", "Actions for purchase orders", "Actions", "Advance, postpone, or cancel messages against purchase orders.", FormKind.Action, app, icon: "fa-bolt");
        yield return P(a, "po_actions_advance", "Actions for purchase orders to advance", "Actions", "Action messages that pull a purchase order earlier.", FormKind.Action, app, icon: "fa-forward");
        yield return P(a, "supply_schedule", "Supply schedule", "Actions", "Time-phased supply versus demand for a coverage item.", FormKind.Forecast, app, icon: "fa-table");
        yield return P(a, "sequenced_planned", "Sequenced planned orders", "Actions", "Planned orders assigned to a sequence group and value.", FormKind.Order, app, icon: "fa-sort-amount-asc");
        yield return P(a, "mp_run", "Master planning run", "Run", "Regenerate planned orders from demand, coverage, and on-hand.", FormKind.Action, app, icon: "fa-play");
        yield return P(a, "ic_master_planning", "Intercompany master planning", "Run", "Plan across legal entities using intercompany demand.", FormKind.Action, app, icon: "fa-exchange");
        yield return P(a, "planned_firming", "Planned order firming", "Run", "Firm selected planned orders into production or purchase.", FormKind.Action, app, icon: "fa-check");
        yield return P(a, "safety_stock_calc", "Safety stock calculation", "Run", "Recalculate safety-stock quantities from demand history.", FormKind.Action, app, icon: "fa-shield");
        yield return P(a, "kanban_qty", "Calculate kanban quantity", "Run", "Size kanban quantities from average demand and lead time.", FormKind.Action, app, icon: "fa-cubes");
        yield return P(a, "sequencing_gen", "Sequencing generation", "Run", "Generate sequence values for planned orders.", FormKind.Action, app, icon: "fa-sort");
        yield return P(a, "continuity_sched", "Continuity scheduling", "Run", "Keep a continuous flow across sequenced operations.", FormKind.Action, app, icon: "fa-repeat");
        yield return P(a, "dp_netflow", "Decoupling point status by net flow", "Demand driven MRP", "Net-flow position versus red, yellow, and green buffers.", FormKind.Order, app, icon: "fa-area-chart");
        yield return P(a, "dp_onhand", "Decoupling point status by on-hand", "Demand driven MRP", "On-hand versus buffer zones at each decoupling point.", FormKind.Order, app, icon: "fa-database");
        yield return P(a, "dp_planned", "Planned orders for decoupling points", "Demand driven MRP", "Planned replenishment created from buffer breaches.", FormKind.Order, app, icon: "fa-list");
        yield return P(a, "buffer_calc", "Calculate buffer values", "Demand driven MRP", "Recalculate ADU, lead time, and buffer zones.", FormKind.Action, app, icon: "fa-calculator");
        yield return P(a, "buffer_cleanup", "Cleanup decoupling point buffer values", "Demand driven MRP", "Remove stale buffer values after an item leaves DDMRP.", FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "stat_forecast", "Generate statistical baseline forecast", "Forecasting", "Build a statistical baseline from history.", FormKind.Forecast, app, icon: "fa-line-chart");
        yield return P(a, "adj_demand_forecast", "Adjusted demand forecast", "Forecasting", "Planner adjustments on top of the baseline.", FormKind.Forecast, app, icon: "fa-pencil");
        yield return P(a, "auth_adj_forecast", "Authorize adjusted demand forecast", "Forecasting", "Authorize adjusted lines so planning can consume them.", FormKind.Action, app, icon: "fa-check-square-o");
        yield return P(a, "forecast_hist", "Statistical baseline forecast generation history", "Forecasting", "History of baseline generation jobs.", FormKind.Master, app, icon: "fa-history");
        yield return P(a, "demand_forecast_lines", "Demand forecast lines", "Manual forecast entry", "Enter demand by item, period, and quantity.", FormKind.Forecast, app, icon: "fa-list");
        yield return P(a, "mp_items", "Items", "Manual forecast entry", "Item coverage keys used when entering forecast lines.", FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "mp_customers", "Customers", "Manual forecast entry", "Customer-dimension forecast entry.", FormKind.Master, app, icon: "fa-user");
        yield return P(a, "mp_customer_groups", "Customer groups", "Manual forecast entry", "Forecast by customer group.", FormKind.Master, app, icon: "fa-users");
        yield return P(a, "mp_vendors", "Vendors", "Manual forecast entry", "Supply forecast by vendor.", FormKind.Master, app, icon: "fa-truck");
        yield return P(a, "mp_vendor_groups", "Vendor groups", "Manual forecast entry", "Supply forecast by vendor group.", FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "supply_forecast_lines", "Supply forecast lines", "Manual forecast entry", "Planned inbound supply that planning should honour.", FormKind.Forecast, app, icon: "fa-download");
        yield return P(a, "forecast_planning", "Forecast planning", "Manual forecast entry", "Run planning against authorized forecast only.", FormKind.Action, app, icon: "fa-play");
        yield return P(a, "forecast_import", "Parallelized forecast import", "Manual forecast entry", "Import forecast lines from a workbook or feed.", FormKind.Action, app, icon: "fa-upload");
        yield return P(a, "item_req_stats", "Item requirement statistics", "Inquiries and reports", "Pegged requirements after the last planning run.", FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "cap_req_stats", "Capacity requirement statistics", "Inquiries and reports", "Capacity load implied by planned production.", FormKind.Master, app, icon: "fa-tachometer");
        yield return P(a, "unfinished_planning", "Unfinished planning processes", "Inquiries and reports", "Planning jobs that did not finish cleanly.", FormKind.Action, app, icon: "fa-exclamation-triangle");
        yield return P(a, "firming_history", "Firming history", "Inquiries and reports", "Audit of planned orders that were firmed.", FormKind.Master, app, icon: "fa-history");
        yield return P(a, "ic_inbound_demand", "Incoming planned intercompany demand", "Intercompany master planning", "Demand received from another legal entity.", FormKind.Order, app, icon: "fa-sign-in");
        yield return P(a, "ic_outbound_demand", "Outbound planned intercompany demand", "Intercompany master planning", "Demand sent to another legal entity.", FormKind.Order, app, icon: "fa-sign-out");
        yield return P(a, "mp_parameters", "Master planning parameters", "Setup", "Company defaults for regeneration, firming, and coverage.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "item_coverage", "Item coverage", "Setup", "Per-item coverage warehouse, lead time, and method.", FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "coverage_groups", "Coverage groups", "Setup", "Shared coverage settings applied to items.", FormKind.Plan, app, icon: "fa-object-group");
        yield return P(a, "minmax_keys", "Minimum/maximum keys", "Setup", "Time-phased min and max inventory keys.", FormKind.Plan, app, icon: "fa-arrows-v");
        yield return P(a, "reduction_keys", "Reduction keys", "Setup", "Reduce forecast as actual orders consume it.", FormKind.Plan, app, icon: "fa-minus");
        yield return P(a, "master_plans", "Master plans", "Setup", "Named plans (static, dynamic, or forecast-only).", FormKind.Plan, app, icon: "fa-map");
        yield return P(a, "forecast_plans", "Forecast plans", "Setup", "Forecast models attached to a master plan.", FormKind.Plan, app, icon: "fa-line-chart");
        yield return P(a, "continuity_plans", "Continuity plans", "Setup", "Continuity scheduling plan headers.", FormKind.Plan, app, icon: "fa-repeat");
        yield return P(a, "safety_stock_journals", "Safety stock journal names", "Setup", "Journal names used when posting safety-stock changes.", FormKind.Master, app, icon: "fa-book");
        yield return P(a, "item_alloc_keys", "Item allocation keys", "Setup", "Split forecast across items or variants.", FormKind.Plan, app, icon: "fa-pie-chart");
        yield return P(a, "forecast_models", "Forecast models", "Setup", "Named forecast models consumed by planning.", FormKind.Plan, app, icon: "fa-area-chart");
        yield return P(a, "demand_fc_params", "Demand forecasting parameters", "Setup", "History horizon, granularity, and authorization rules.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "fc_algo_params", "Forecasting algorithm parameters", "Setup", "Algorithm, seasonality, and outlier handling.", FormKind.Master, app, icon: "fa-cogs");
        yield return P(a, "outlier_removal", "Outlier removal", "Setup", "Mark history outliers so they do not distort the baseline.", FormKind.Action, app, icon: "fa-filter");
        yield return P(a, "period_alloc_cats", "Period allocation categories", "Setup", "Spread a bucketed forecast into finer periods.", FormKind.Plan, app, icon: "fa-calendar");
        yield return P(a, "ic_planning_groups", "Intercompany planning groups", "Setup", "Legal entities that plan together.", FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "mp_sched_params", "Scheduling parameters", "Setup", "Finite or infinite capacity scheduling defaults.", FormKind.Master, app, icon: "fa-clock-o");
        yield return P(a, "mp_sched_params_site", "Scheduling parameters by site", "Setup", "Override scheduling defaults per site.", FormKind.Master, app, icon: "fa-building");
        yield return P(a, "mp_sequences", "Sequences", "Setup", "Sequence definitions for planned orders.", FormKind.Master, app, icon: "fa-list-ol");
        yield return P(a, "mp_sequence_value", "Sequence value", "Setup", "Values inside a sequence (colour, option, size).", FormKind.Master, app, icon: "fa-tag");
        yield return P(a, "mp_sequence_group", "Sequence group", "Setup", "Group sequences that planning applies together.", FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "decoupled_lead", "Decoupled lead time", "Setup", "Lead time used at a decoupling point.", FormKind.Master, app, icon: "fa-hourglass");
        yield return P(a, "ic_supply_demand", "Intercompany supply and demand", "Setup", "Which entities supply which demand.", FormKind.Master, app, icon: "fa-exchange");
        yield return P(a, "plan_groups", "Plan groups", "Setup", "Group master plans for batch regeneration.", FormKind.Plan, app, icon: "fa-folder");
        yield return P(a, "planned_cost", "Planned order cost calculation", "Setup", "Cost roll-up used when ranking planned orders.", FormKind.Action, app, icon: "fa-money");
        yield return P(a, "deprecated_features", "Deprecated features", "Setup", "Legacy planning features kept for cutover only.", FormKind.Master, app, icon: "fa-archive");
        yield return P(a, "planning_opt_params", "Planning optimization parameters", "Setup", "Planning Optimization service defaults.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "planning_priority", "Planning priority models", "Setup", "Priority used when supply is short.", FormKind.Plan, app, icon: "fa-sort-numeric-asc");
    }

    private static IEnumerable<Page> MheiPages()
    {
        const string a = "mhei";
        const string app = MheiApp;
        yield return P(a, "mhei_queue_mgr", "Queue manager", "Workspaces", "Inbound and outbound equipment messages in one workspace.", FormKind.Queue, app, icon: "fa-tasks");
        yield return P(a, "mhei_inbound", "Inbound queue", "Common", "Messages received from warehouse equipment.", FormKind.Queue, app, icon: "fa-sign-in");
        yield return P(a, "mhei_outbound", "Outbound queue", "Common", "Instructions sent to warehouse equipment.", FormKind.Queue, app, icon: "fa-sign-out");
        yield return P(a, "mhei_reprocess", "Reprocess errored inbound queue", "Periodic", "Replay inbound messages that failed validation.", FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "mhei_reset_outbound", "Reset status of outbound queue", "Periodic", "Reset a stuck outbound message so it can be sent again.", FormKind.Action, app, icon: "fa-undo");
        yield return P(a, "mhei_inbound_cleanup", "Inbound queue cleanup", "Cleanup", "Purge processed inbound messages after the retention period.", FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "mhei_outbound_cleanup", "Outbound queue cleanup", "Cleanup", "Purge processed outbound messages after the retention period.", FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "mhei_parameters", "Material handling equipment interface parameters", "Setup", "Endpoint, retry, and mapping defaults for equipment.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "mhei_subscriptions", "Subscriptions", "Setup", "Which work types an equipment controller subscribes to.", FormKind.Master, app, icon: "fa-rss");
    }

    private static IEnumerable<Page> OrgPages()
    {
        const string a = "enterprise";
        const string app = OrgApp;
        yield return P(a, "org_resource_lifecycle", "Resource lifecycle management", "Workspaces", "Track organization resources from create through retire.", FormKind.Master, app, icon: "fa-recycle");
        yield return P(a, "org_business_docs", "Business document management", "Workspaces", "Business document templates and generated files.", FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "org_my_processes", "All my processes", "Workspaces", "Workflows assigned to the signed-in worker.", FormKind.Master, app, icon: "fa-tasks");
        yield return P(a, "org_session_datetime", "Session date and time", "Workspaces", "Override the session date used by journals and calendars.", FormKind.Master, app, icon: "fa-clock-o");
        yield return P(a, "org_number_sequences", "Number sequences", "Workspaces", "Number sequence codes, segments, and next value.", FormKind.Master, app, icon: "fa-list-ol");
        yield return P(a, "org_number_sequences_list", "Number sequences list", "Workspaces", "Browse every number sequence and its current status.", FormKind.Master, app, icon: "fa-list");
        yield return P(a, "org_segment_config", "Segment configuration", "Workspaces", "Segments that make up a number sequence.", FormKind.Master, app, icon: "fa-puzzle-piece");
        yield return P(a, "org_chrono_ns", "Chronological number sequence groups", "Workspaces", "Keep document numbers chronological per period.", FormKind.Master, app, icon: "fa-sort-numeric-asc");
        yield return P(a, "legal_entities", "Legal entities", "Organizations", "Legal entity master used on every posting.", FormKind.Master, app, "/erp/organization-app?tab=legal_entities", "fa-bank");
        yield return P(a, "org_entities", "Legal entity register", "Organizations", "Registered legal entities with consolidation scope (SHAM perimeter).", FormKind.Master, app, icon: "fa-building");
        yield return P(a, "org_business_units", "Business units", "Organizations", "Entity x BU units (ASAPL-WSL/RTL/CLS/CLP/BOOK) with branch and cost centre.", FormKind.Master, app, icon: "fa-th");
        yield return P(a, "org_group_members", "Consolidation group", "Organizations", "Group membership, ownership and consolidation method per entity.", FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "org_user_access", "User access matrix", "Organizations", "User x legal entity x BU x role x action grants (backend enforced).", FormKind.Master, app, icon: "fa-lock");
        yield return P(a, "org_operating_units", "Operating units", "Organizations", "Departments, cost centres, and value streams.", FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "org_position_hierarchy", "Position hierarchy", "Organizations", "Reporting hierarchy of positions.", FormKind.Master, app, icon: "fa-users");
        yield return P(a, "org_hierarchy_purposes", "Organizational hierarchy purposes", "Organizations", "Why a hierarchy exists (security, budget, or reporting).", FormKind.Master, app, icon: "fa-flag");
        yield return P(a, "org_teams", "Teams", "Organizations", "Cross-functional teams used by workflows.", FormKind.Master, app, icon: "fa-users");
        yield return P(a, "org_gab", "Global address book", "Global address book", "Parties shared across customers, vendors, and workers.", FormKind.Master, app, "/erp/contacts-app", "fa-address-book-o");
        yield return P(a, "org_name_affix", "Name title and suffix", "Global address book", "Titles and suffixes used on party names.", FormKind.Master, app, icon: "fa-font");
        yield return P(a, "org_addresses", "Addresses", "Global address book", "Postal addresses attached to parties.", FormKind.Master, app, icon: "fa-map-marker");
        yield return P(a, "org_name_sequences", "Name sequences", "Global address book", "How a formatted name is built.", FormKind.Master, app, icon: "fa-text-width");
        yield return P(a, "org_relationship_types", "Relationship types", "Global address book", "How two parties relate (employer, subsidiary, contact).", FormKind.Master, app, icon: "fa-link");
        yield return P(a, "org_address_purpose", "Address and contact information purpose", "Global address book", "Invoice, delivery, or statement purpose on an address.", FormKind.Master, app, icon: "fa-tag");
        yield return P(a, "org_translations", "Translations", "Global address book", "Translated labels for address and party fields.", FormKind.Master, app, icon: "fa-language");
        yield return P(a, "org_confirm_templates", "Confirm location of templates", "Global address book", "Confirm where generated address templates live.", FormKind.Action, app, icon: "fa-check");
        yield return P(a, "org_address_setup", "Address setup", "Global address book", "Country, state, county, and city setup.", FormKind.Master, app, icon: "fa-globe");
        yield return P(a, "org_import_postal", "Import ZIP/postal codes", "Global address book", "Load postal codes for address validation.", FormKind.Action, app, icon: "fa-upload");
        yield return P(a, "org_reg_types", "Registration types", "Global address book", "TRN, trade licence, and other registration types.", FormKind.Master, app, icon: "fa-id-card");
        yield return P(a, "org_reg_categories", "Registration categories", "Global address book", "Group registration types for reporting.", FormKind.Master, app, icon: "fa-folder");
        yield return P(a, "org_issuing_agencies", "Issuing agencies", "Global address book", "Authorities that issue registrations.", FormKind.Master, app, icon: "fa-university");
        yield return P(a, "org_delete_inactive_addr", "Delete inactivated addresses", "Global address book", "Purge addresses marked inactive.", FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "org_workflow_params", "Workflow parameters", "Workflow", "Company defaults for workflow notifications.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "org_workflows", "Organization workflows", "Workflow", "Workflow definitions for organization documents.", FormKind.Master, app, "/erp/workflow-app", "fa-sitemap");
        yield return P(a, "org_work_item_queue", "Work item queue", "Workflow", "Queue that holds assigned work items.", FormKind.Master, app, icon: "fa-inbox");
        yield return P(a, "org_work_item_rules", "Work item queue assignment rules", "Workflow", "Rules that route work into a queue.", FormKind.Master, app, icon: "fa-random");
        yield return P(a, "org_workflow_history", "Workflow history", "Workflow", "Completed and cancelled workflow instances.", FormKind.Master, app, icon: "fa-history");
        yield return P(a, "org_active_doc_tables", "Active document tables", "Document management", "Which tables allow attachments.", FormKind.Master, app, icon: "fa-table");
        yield return P(a, "org_doc_types", "Document types", "Document management", "File, note, and image document types.", FormKind.Master, app, icon: "fa-files-o");
        yield return P(a, "org_doc_params", "Document management parameters", "Document management", "Storage and size defaults for attachments.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "org_export_attachments", "Export attachments", "Document management", "Export attachments for a table or period.", FormKind.Action, app, icon: "fa-download");
        yield return P(a, "org_er_configs", "Electronic reporting configurations", "Electronic reporting", "ER format and model configurations.", FormKind.Master, app, "/erp/electronic-reporting-app", "fa-file-code-o");
        yield return P(a, "org_er_debug", "Configuration debug log", "Electronic reporting", "Debug output from the last ER run.", FormKind.Master, app, icon: "fa-bug");
        yield return P(a, "org_er_providers", "Configuration provider table", "Electronic reporting", "Providers that own ER configurations.", FormKind.Master, app, icon: "fa-building");
        yield return P(a, "org_er_destination", "Electronic reporting destination", "Electronic reporting", "Where generated files are sent.", FormKind.Master, app, icon: "fa-share");
        yield return P(a, "org_er_archive", "Electronic reporting archived jobs", "Electronic reporting", "Archived ER jobs and outputs.", FormKind.Master, app, icon: "fa-archive");
        yield return P(a, "org_er_file_types", "File types for the sources", "Electronic reporting", "Allowed source file types.", FormKind.Master, app, icon: "fa-file");
        yield return P(a, "org_er_filters", "File entity filter for templates", "Electronic reporting", "Filter which entities a template can use.", FormKind.Master, app, icon: "fa-filter");
        yield return P(a, "org_er_restore", "Restore broken templates", "Electronic reporting", "Repair templates that failed to load.", FormKind.Action, app, icon: "fa-wrench");
        yield return P(a, "org_printable_archive", "Printable archive", "Inquiries and reports", "Archived printable documents.", FormKind.Master, app, icon: "fa-print");
        yield return P(a, "org_report_archive", "Report archive", "Inquiries and reports", "Archived report outputs.", FormKind.Master, app, icon: "fa-folder-open-o");
        yield return P(a, "org_capacity_res", "Capacity reservations", "Resources", "Reserved capacity against a resource.", FormKind.Master, app, icon: "fa-lock");
        yield return P(a, "org_capacity_load", "Graphical capacity load", "Resources", "Load picture for a resource or department.", FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "org_dept_capacity", "Departmental capacity reservations report", "Resources", "Capacity reserved by department.", FormKind.Master, app, icon: "fa-building");
        yield return P(a, "org_batch", "Batch processing", "Periodic", "Batch jobs that maintain organization data.", FormKind.Action, app, icon: "fa-cogs");
        yield return P(a, "org_calendar_cleanup", "Calendar cleanup", "Periodic", "Remove expired calendar days.", FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "org_email_templates", "Organization email templates", "Setup", "Email templates used by workflows and alerts.", FormKind.Master, app, icon: "fa-envelope-o");
        yield return P(a, "org_network_printers", "Network printers", "Setup", "Printers available to the organization.", FormKind.Master, app, icon: "fa-print");
        yield return P(a, "org_financial_years", "Financial years", "Setup", "Fiscal years used by calendars and periods.", FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "org_barcodes", "Barcodes", "Setup", "Barcode setups shared across documents.", FormKind.Master, app, icon: "fa-barcode");
        yield return P(a, "org_default_desc", "Default descriptions", "Setup", "Default voucher and journal descriptions.", FormKind.Master, app, icon: "fa-align-left");
        yield return P(a, "org_activity_phases", "Activity phases", "Activities", "Phases used on cases and projects.", FormKind.Master, app, icon: "fa-flag-checkered");
        yield return P(a, "org_activity_plans", "Activity plans", "Activities", "Reusable activity plans.", FormKind.Plan, app, icon: "fa-list-alt");
        yield return P(a, "org_activity_types", "Activity types", "Activities", "Meeting, task, and call activity types.", FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "org_process_templates", "Process templates", "Process templates", "Reusable case and process templates.", FormKind.Plan, app, icon: "fa-copy");
        yield return P(a, "org_case_categories", "Case categories", "Process templates", "Categories for cases.", FormKind.Master, app, icon: "fa-folder");
        yield return P(a, "org_case_security", "Case category type security", "Process templates", "Who can open a case category.", FormKind.Master, app, icon: "fa-lock");
        yield return P(a, "org_case_processes", "Case processes", "Process templates", "Process attached to a case category.", FormKind.Plan, app, icon: "fa-sitemap");
        yield return P(a, "org_case_workflow", "Case workflow", "Process templates", "Workflow that drives a case.", FormKind.Master, app, icon: "fa-random");
        yield return P(a, "org_esig_params", "Electronic signature parameters", "Electronic signature", "When a signature is required.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "org_esig_req", "Electronic signature requirements", "Electronic signature", "Documents that require a signature.", FormKind.Master, app, icon: "fa-pencil-square-o");
        yield return P(a, "org_esig_reasons", "Electronic signature reason codes", "Electronic signature", "Reason codes captured with a signature.", FormKind.Master, app, icon: "fa-comment");
        yield return P(a, "org_excel_designer", "Excel workbook designer", "Office integration", "Design workbooks that open organization data.", FormKind.Master, app, icon: "fa-file-excel-o");
        yield return P(a, "org_migration_cleanup", "Migration cleanup", "Office integration", "Clean leftover rows after a data migration.", FormKind.Action, app, icon: "fa-eraser");
        yield return P(a, "org_signing_schema", "Apply the selected legal entity signing schema", "Office integration", "Apply a signing schema to electronic reporting.", FormKind.Action, app, icon: "fa-certificate");
        yield return P(a, "org_external_feed", "Configure external feed", "Office integration", "External feed used by office add-ins.", FormKind.Master, app, icon: "fa-rss");
        yield return P(a, "org_group_assignment", "Group assignment", "Office integration", "Assign workers to office integration groups.", FormKind.Master, app, icon: "fa-users");
        yield return P(a, "org_sched_params", "Scheduling parameters", "Scheduling", "Default scheduling calendar and horizon.", FormKind.Master, app, icon: "fa-clock-o");
        yield return P(a, "org_sched_site", "Scheduling parameters by site", "Scheduling", "Site overrides for scheduling.", FormKind.Master, app, icon: "fa-building");
        yield return P(a, "org_sched_cockpit", "Scheduling tracking cockpit", "Scheduling", "Watch in-flight scheduling jobs.", FormKind.Master, app, icon: "fa-dashboard");
        yield return P(a, "org_signing_limits", "Signing limits", "Signing", "Amount a worker may sign.", FormKind.Master, app, icon: "fa-gavel");
        yield return P(a, "org_signing_policies", "Signing limit policies", "Signing", "Policies that assign signing limits.", FormKind.Plan, app, icon: "fa-balance-scale");
        yield return P(a, "org_exch_sync", "Microsoft Exchange Server synchronization", "Microsoft Exchange", "Sync calendars and contacts with Exchange.", FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "org_exch_params", "Microsoft Exchange Server parameters", "Microsoft Exchange", "Exchange endpoint and mailbox defaults.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "org_exch_map", "Microsoft Exchange Server mapping", "Microsoft Exchange", "Map organization users to mailboxes.", FormKind.Master, app, icon: "fa-exchange");
        yield return P(a, "org_unit_conv", "Unit conversions", "Units", "Convert between units of measure.", FormKind.Master, app, icon: "fa-exchange");
        yield return P(a, "org_units", "Units", "Units", "Units of measure used on documents.", FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "org_calendars", "Calendars", "Calendars", "Working calendars for the organization.", FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "org_period_types", "Period types", "Calendars", "Day, week, month, and custom period types.", FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "org_period_templates", "Period templates", "Calendars", "Reusable fiscal period templates.", FormKind.Plan, app, icon: "fa-calendar-o");
        yield return P(a, "org_working_time", "Working time templates", "Calendars", "Daily working-time templates.", FormKind.Plan, app, icon: "fa-clock-o");
        yield return P(a, "org_date_dimensions", "Date dimensions", "Calendars", "Date attributes used in reports.", FormKind.Master, app, icon: "fa-table");
        yield return P(a, "org_branding_images", "Branding images", "Branding", "Logos used on documents.", FormKind.Master, app, icon: "fa-picture-o");
        yield return P(a, "org_document_brands", "Document brands", "Branding", "Brand applied to printable documents.", FormKind.Master, app, icon: "fa-id-badge");
    }

    private static IEnumerable<Page> PayrollPages()
    {
        const string a = "payroll_area";
        const string app = PayrollApp;
        yield return P(a, "payroll", "Payroll runs", "Payroll management", "Generate, approve, and pay a payroll period.", FormKind.Payroll, app, "/erp/payroll-app", "fa-money");
        yield return P(a, "pay_workers", "Workers", "Enrollment", "Workers included in payroll.", FormKind.Master, app, "/erp/staff-app", "fa-id-badge");
        yield return P(a, "pay_contractors", "Contractors", "Enrollment", "Contractors paid through payroll.", FormKind.Master, app, icon: "fa-user");
        yield return P(a, "pay_positions", "Positions", "Enrollment", "Positions that carry a pay cycle and grade.", FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "pay_jobs", "Jobs", "Enrollment", "Job definitions used by positions.", FormKind.Master, app, icon: "fa-briefcase");
        yield return P(a, "pay_cycles", "Pay cycles", "Enrollment", "Monthly, weekly, or custom pay cycles.", FormKind.Plan, app, icon: "fa-refresh");
        yield return P(a, "pay_allowances", "Allowances and deductions", "Enrollment", "Recurring earnings and deduction codes on a worker.", FormKind.Payroll, app, icon: "fa-plus-square");
        yield return P(a, "pay_accruals", "Accruals", "Enrollment", "Leave, indemnity, and ticket accrual balances.", FormKind.Payroll, app, icon: "fa-hourglass-half");
        yield return P(a, "pay_generate_cycle", "Generate pay cycle", "Enrollment", "Open the next pay cycle for a group of workers.", FormKind.Action, app, icon: "fa-play");
        yield return P(a, "pay_validate_cycle", "Pay cycle validate", "Enrollment", "Validate a cycle before generate.", FormKind.Action, app, icon: "fa-check");
        yield return P(a, "pay_all_accruals", "All accrual transactions", "Enrollment", "Every posted accrual transaction.", FormKind.Payroll, app, icon: "fa-list");
        yield return P(a, "pay_indemnity", "Indemnity accrual transactions", "Enrollment", "End-of-service indemnity accrual lines.", FormKind.Payroll, app, icon: "fa-gavel");
        yield return P(a, "pay_ticket", "Ticket accrual transactions", "Enrollment", "Air-ticket accrual lines.", FormKind.Payroll, app, icon: "fa-plane");
        yield return P(a, "pay_social", "Social security accrual transactions", "Enrollment", "Social-security accrual lines.", FormKind.Payroll, app, icon: "fa-shield");
        yield return P(a, "pay_gosi", "GOSI accrual transactions", "Enrollment", "GOSI accrual lines for eligible workers.", FormKind.Payroll, app, icon: "fa-university");
        yield return P(a, "pay_leave_mgmt", "Leave management", "Leave", "Leave balances that payroll consumes.", FormKind.Request, app, LeaveApp + "?tab=time_off_requests", "fa-calendar");
        yield return P(a, "pay_leave_types", "Leave types", "Leave", "Leave types that affect pay.", FormKind.Plan, app, icon: "fa-tags");
        yield return P(a, "pay_leave_plans", "Leave plans", "Leave", "Plans that grant units into payroll.", FormKind.Plan, app, icon: "fa-list-alt");
        yield return P(a, "pay_leave_request", "Leave request", "Leave", "Leave request that will reduce payable days.", FormKind.Request, app, icon: "fa-calendar-check-o");
        yield return P(a, "pay_leave_planner", "Leave planner", "Leave", "Team leave planner used before a cycle.", FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "pay_leave_tx", "Leave transactions", "Leave", "Posted leave units consumed by payroll.", FormKind.Payroll, app, icon: "fa-list");
        yield return P(a, "pay_ess", "ESS requests", "Leave", "Self-service leave and certificate requests.", FormKind.Request, app, icon: "fa-user");
        yield return P(a, "pay_clearance", "Clearance form", "Leave", "Clearance before final settlement.", FormKind.Request, app, icon: "fa-check-square-o");
        yield return P(a, "pay_leave_return", "Leave return registration", "Leave", "Register return from leave so pay resumes.", FormKind.Request, app, icon: "fa-sign-in");
        yield return P(a, "pay_loans", "Loan management", "Loans", "Worker loans and instalments recovered from pay.", FormKind.Payroll, app, icon: "fa-credit-card");
        yield return P(a, "pay_gratuity", "Gratuity", "Gratuity", "Gratuity / end-of-service calculation.", FormKind.Payroll, app, icon: "fa-gift");
        yield return P(a, "pay_eos", "End of service request", "End of service", "Final settlement request for a leaver.", FormKind.Request, app, icon: "fa-sign-out");
        yield return P(a, "pay_air_ticket", "Air ticket allowance", "Air ticket", "Ticket entitlement and payment.", FormKind.Payroll, app, icon: "fa-plane");
        yield return P(a, "pay_biometric", "Biometric management", "Time and attendance", "Biometric device and punch import.", FormKind.Master, app, icon: "fa-fingerprint");
        yield return P(a, "pay_attendance", "Attendance", "Time and attendance", "Daily attendance used to set days worked.", FormKind.Payroll, app, icon: "fa-clock-o");
        yield return P(a, "pay_timesheet", "Time sheet", "Time and attendance", "Approved time sheets that feed overtime.", FormKind.Payroll, app, icon: "fa-table");
        yield return P(a, "pay_overtime", "Overtime", "Time and attendance", "Overtime hours and the earning code they post to.", FormKind.Payroll, app, icon: "fa-plus");
        yield return P(a, "pay_short_time", "Short time", "Time and attendance", "Short-time / unpaid hours that reduce pay.", FormKind.Payroll, app, icon: "fa-minus");
        yield return P(a, "pay_performance", "Performance", "Performance", "Rating imported into a bonus calculation.", FormKind.Payroll, app, "/erp/performance-app", "fa-star");
        yield return P(a, "pay_bonus", "Bonus", "Bonus", "Bonus calculation and posting.", FormKind.Payroll, app, icon: "fa-trophy");
        yield return P(a, "pay_wps", "WPS", "WPS", "Wage Protection System SIF file and salary register.", FormKind.Payroll, app, icon: "fa-file-text-o");
        yield return P(a, "pay_earnings", "Earnings statements", "Earnings", "Per-worker earnings statement for a period.", FormKind.Payroll, app, icon: "fa-file-o");
        yield return P(a, "pay_payment_stmt", "Payment statements", "Payment statements", "Bank payment file and payslip register.", FormKind.Payroll, app, icon: "fa-university");
        yield return P(a, "pay_retro", "Retroactive earnings", "Payment statements", "Back-dated earning adjustments.", FormKind.Payroll, app, icon: "fa-history");
        yield return P(a, "pay_tax", "Tax processing", "Tax", "Payroll tax groups, codes, and regions.", FormKind.Master, app, icon: "fa-percent");
        yield return P(a, "pay_benefits", "Benefits", "Benefits", "Benefit elements and contribution rates.", FormKind.Plan, app, icon: "fa-heart");
        yield return P(a, "pay_reports", "Payroll reports", "Reports", "Salary, indemnity, overtime, and comparison reports.", FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "pay_parameters", "Payroll parameters", "Setup", "Company payroll defaults, rounding, and WPS bank.", FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "pay_hr_params", "Human resources parameters", "Setup", "Shared HR defaults used by payroll.", FormKind.Master, app, icon: "fa-cogs");
        yield return P(a, "pay_grades", "Employee grades", "Setup", "Grades and salary scales.", FormKind.Plan, app, icon: "fa-signal");
        yield return P(a, "pay_calendars", "Calendars", "Setup", "Payroll working calendars.", FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "pay_uploads", "Uploads", "Uploads", "Worker, attendance, and earning upload batches.", FormKind.Action, app, icon: "fa-upload");
        yield return P(a, "pay_email_alerts", "Email alerts", "Setup", "Payslip and WPS email alerts.", FormKind.Master, app, icon: "fa-envelope-o");
    }

    private static IEnumerable<Page> ProcurementPages()
    {
        const string a = "purchasing";
        const string app = ProcurementApp;
        yield return P(a, "purchase_orders", "Purchase orders", "Purchase orders", "Draft, approve, receive, and invoice purchase orders.", FormKind.Procurement, app, "/erp/purchase-orders-app", "fa-clipboard");
        yield return P(a, "purchase_requisitions", "Purchase requisitions", "Purchase requisitions", "Internal requisitions with policy approval.", FormKind.Procurement, app, "/erp/purchase-requests-app", "fa-list-alt");
        yield return P(a, "rfq", "Request for quotation", "Quotations", "RFQ to vendors and quotation follow-up.", FormKind.Procurement, app, "/erp/rfq-app", "fa-envelope-o");
        yield return P(a, "purchases", "Purchases", "Purchase orders", "Posted purchase invoices.", FormKind.Procurement, app, "/erp/purchases-app", "fa-file-text-o");
        yield return P(a, "three_way_match", "3-way match", "Purchase orders", "Match PO, receipt, and invoice.", FormKind.Procurement, app, "/erp/three-way-match-app", "fa-check-square-o");
        yield return P(a, "procurement_categories", "Categories and policies", "Setup", "Category tree and spending policies.", FormKind.Master, app, "/erp/procurement-categories-app", "fa-sitemap");
        yield return P(a, "supplier_portal", "Vendors", "Vendors", "Vendor master and portal access.", FormKind.Master, app, "/erp/suppliers-app?tab=supplier_portal", "fa-handshake-o");
        yield return P(a, "proc_po_prep", "Purchase order preparation", "Workspaces", "Workspace to prepare purchase orders before submit.", FormKind.Procurement, app, icon: "fa-pencil");
        yield return P(a, "proc_po_receipt", "Purchase order receipt and follow-up", "Workspaces", "Receipts, delayed lines, and confirmation follow-up.", FormKind.Procurement, app, icon: "fa-truck");
        yield return P(a, "proc_supply_risk", "Supply risk assessment", "Workspaces", "Score vendors and categories for supply risk.", FormKind.Master, app, icon: "fa-exclamation-triangle");
        yield return P(a, "proc_all_pos", "All purchase orders", "Purchase orders", "Every purchase order regardless of status.", FormKind.Procurement, app, icon: "fa-list");
        yield return P(a, "proc_local_orders", "Local orders", "Purchase orders", "Domestic purchase orders.", FormKind.Procurement, app, icon: "fa-home");
        yield return P(a, "proc_import_orders", "Import orders", "Purchase orders", "Import purchase orders with landed-cost linkage.", FormKind.Procurement, app, icon: "fa-ship");
        yield return P(a, "proc_ic_orders", "Intercompany purchase orders", "Purchase orders", "Purchase orders between legal entities.", FormKind.Procurement, app, icon: "fa-exchange");
        yield return P(a, "proc_po_entry", "Purchase order entry", "Purchase orders", "Enter a purchase order header and lines.", FormKind.Procurement, app, icon: "fa-plus");
        yield return P(a, "proc_not_submitted", "Not submitted purchase orders", "Purchase orders", "Draft orders that have not been submitted.", FormKind.Procurement, app, icon: "fa-edit");
        yield return P(a, "proc_pending", "Pending purchase orders", "Purchase orders", "Orders waiting for approval.", FormKind.Procurement, app, icon: "fa-hourglass-half");
        yield return P(a, "proc_not_sent", "Purchase orders not sent", "Purchase orders", "Approved orders not yet sent to the vendor.", FormKind.Procurement, app, icon: "fa-paper-plane");
        yield return P(a, "proc_followup_lines", "Purchase order follow-up lines", "Purchase order follow-up", "Open lines still awaiting receipt or confirmation.", FormKind.Procurement, app, icon: "fa-list");
        yield return P(a, "proc_delayed_receipts", "Delayed receipts", "Purchase order follow-up", "Receipts past the confirmed delivery date.", FormKind.Procurement, app, icon: "fa-clock-o");
        yield return P(a, "proc_confirmations", "Purchase order confirmations", "Purchase order follow-up", "Vendor confirmations against a purchase order.", FormKind.Procurement, app, icon: "fa-check");
        yield return P(a, "proc_receipts", "Product receipt", "Purchase order follow-up", "Warehouse product receipts.", FormKind.Procurement, app, icon: "fa-sign-in");
        yield return P(a, "proc_req_all", "All requisitions", "Purchase requisitions", "Every requisition regardless of status.", FormKind.Procurement, app, icon: "fa-list-alt");
        yield return P(a, "proc_req_approved", "Purchase requisitions approved by me", "Purchase requisitions", "Requisitions this worker approved.", FormKind.Procurement, app, icon: "fa-check-square-o");
        yield return P(a, "proc_req_assigned", "Purchase requisitions assigned to me", "Purchase requisitions", "Requisitions waiting on this worker.", FormKind.Procurement, app, icon: "fa-user");
        yield return P(a, "proc_rfq_open", "Open requests for quotations", "Quotations", "RFQs still waiting for vendor replies.", FormKind.Procurement, app, icon: "fa-inbox");
        yield return P(a, "proc_rfq_followup", "Request for quotations follow-up", "Quotations", "Compare replies and award an RFQ.", FormKind.Procurement, app, icon: "fa-balance-scale");
        yield return P(a, "proc_agreements", "Purchase agreements", "Agreements", "Quantity or value agreements with a vendor.", FormKind.Procurement, app, icon: "fa-file-text-o");
        yield return P(a, "proc_rebates", "Vendor rebates", "Vendors", "Rebate agreements and claims.", FormKind.Procurement, app, icon: "fa-percent");
        yield return P(a, "proc_catalogs", "Procurement catalogs", "Catalogs", "Internal and vendor catalogs used on requisitions.", FormKind.Master, app, icon: "fa-book");
        yield return P(a, "proc_price_disc", "Prices and discounts", "Prices and discounts", "Vendor price and discount groups.", FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "proc_vendor_stats", "Vendor statistics", "Inquiries and reports", "Spend, lead time, and receipt performance.", FormKind.Master, app, icon: "fa-bar-chart");
    }
}
