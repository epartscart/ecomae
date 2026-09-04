namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP tab chrome for residual / thin ERP modules: view tabs, create fields, grid columns.
/// Used so empty shop DB still shows the PHP page structure.
/// </summary>
public static class ErpPhpModuleChromeCatalog
{
    public sealed record Field(string Name, string Label, string Type = "text", string? Placeholder = null, IReadOnlyList<(string Value, string Text)>? Options = null);

    public sealed record Section(
        string Title,
        string? Intro,
        IReadOnlyList<string> Columns,
        IReadOnlyList<Field> Fields,
        string EmptyCopy);

    public sealed record ModuleChrome(
        string Tab,
        string Title,
        string Subtitle,
        IReadOnlyList<(string Key, string Label)> ViewTabs,
        IReadOnlyList<string> KpiLabels,
        IReadOnlyList<Section> Sections);

    public static ModuleChrome ForTab(string? tab)
    {
        var key = (tab ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "rfid" => new("rfid", "RFID System",
                "RFID-based inventory management — tag products, bulk scan for stocktaking, real-time tracking, and anti-theft gates.",
                [("dashboard", "Dashboard"), ("tags", "Tags"), ("scanners", "Scanners"), ("config", "Configuration")],
                ["Tagged items", "Active tags", "Scan sessions", "Missing (alert)"],
                [
                    new("RFID tag management", "Each product gets a unique RFID tag (UHF or HF). Tags link to the product barcode/SKU.",
                        ["EPC", "SKU", "Item", "WH", "Zone", "Status", "Last scan"],
                        [new("sku", "SKU"), new("epc", "EPC / tag id")],
                        "No RFID tags yet — register the first tag from this panel."),
                    new("Recent scans", "Handheld reader or fixed-gate sessions.",
                        ["Date/time", "Location", "Scanned", "Expected", "Discrepancy", "Duration"],
                        [],
                        "No scan sessions yet."),
                ]),
            "quality" => new("quality", "Quality management",
                "Enterprise test plans, quality orders (inspection results & verdict) and non-conformance (NCR) with corrective actions.",
                [("orders", "Quality orders"), ("plans", "Test plans"), ("ncr", "Non-conformance")],
                ["Test plans", "Quality orders", "Passed", "Failed", "Open NCRs"],
                [
                    new("New quality order", null, ["#", "Plan", "Ref", "Verdict"],
                        [new("plan_id", "Plan"), new("ref_type", "Ref type", "select", null, [("po", "PO"), ("so", "SO"), ("production", "Production"), ("item", "Item")]), new("ref_id", "Reference"), new("qty", "Qty", "number")],
                        "No quality orders."),
                    new("Test plans", null, ["Code", "Name", "Tests"],
                        [new("code", "Code"), new("name", "Name")],
                        "No test plans."),
                    new("Non-conformance", null, ["#", "Title", "Severity", "Disposition", "Status"],
                        [new("title", "Title"), new("severity", "Severity", "select", null, [("minor", "minor"), ("major", "major"), ("critical", "critical")])],
                        "No non-conformances."),
                ]),
            "staff" => new("staff", "Staff & departments",
                "Each department has its own ERP tabs and workflow queue. Dashboard centres (CEO, CFO, Sales, …) are controlled by staff profile.",
                [("directory", "Staff directory"), ("departments", "Department map")],
                ["Active staff", "Open workflow tasks", "Active campaigns", "Departments"],
                [
                    new("Department map", null, ["Department", "ERP tabs", "Open tasks", "Standard workflow"], [], "Department map is always available from the PHP staff config."),
                    new("Staff directory & dashboard centres", "Dashboard centre is resolved from: explicit override → job title → department default.",
                        ["Name", "Department", "Job title", "Dashboard centre", "Login e-mail"],
                        [new("display_name", "Name"), new("department_code", "Department"), new("job_title", "Job title")],
                        "No staff — run staff setup on the shop database."),
                ]),
            "hr" or "hr_law" or "hr_ops" => new("hr", "HR — employee records & salaries",
                "Basic + allowances = fixed monthly salary for a 30-day month. Set days worked before generating payroll. Salaries feed the Payroll tab.",
                [("salaries", "Salaries"), ("statutory", "End-of-service & leave")],
                ["Active employees", "Pending leave", "Payroll runs", "Attendance rows"],
                [
                    new("HR — employee records & salaries", "Pro-rata: salary ÷ 30 × days worked.",
                        ["Name", "Department", "Job title", "Fixed basic", "Allowances", "Monthly (30d)", "Days worked", "Est. pay", "Bank", "Leave"],
                        [new("display_name", "Name"), new("basic_salary", "Fixed basic", "number"), new("allowances", "Allowances", "number")],
                        "No HR records — add staff profiles on the shop database."),
                    new("End-of-service & statutory leave", "Gratuity and annual-leave entitlement follow United Arab Emirates labour law (country from company profile).",
                        ["Name", "Joined", "Service", "Gratuity (accrued)", "Annual leave", "Leave accrued", "Leave salary (accrued)"],
                        [],
                        "No HR records."),
                ]),
            "product_info" => new("product_info", "Product information",
                "Item master, field roles and variants. Sub-modules: Product dev kit, All products, Release product, Dimensions & variants, Field setup.",
                [("devkit", "Product dev kit"), ("all", "All products"), ("released", "Release product"), ("dimensions", "Dimensions & variants"), ("fields", "Field setup")],
                ["Items", "Active", "Fields"],
                [
                    new("Items", null, ["SKU", "Name", "Type", "Unit", "Sales price", "Status"],
                        [new("code", "SKU"), new("name", "Name")],
                        "No product items yet."),
                    new("Field setup", null, ["Key", "Label", "Type", "Role", "Status"], [], "No field definitions yet."),
                    new("Variants", null, ["Variant SKU", "Base", "Label", "Status"], [], "No variants yet."),
                ]),
            "customer_groups" => new("customer_groups", "Customer groups",
                "Wholesale, corporate and price-list tiers.",
                [("list", "Groups")],
                ["Groups", "Active", "Members"],
                [new("Customer groups", null, ["Code", "Name", "Type", "Discount %", "Credit", "Terms", "Members", "Status"],
                    [new("code", "Code"), new("name", "Name"), new("group_type", "Type", "select", null, [("wholesale", "Wholesale"), ("corporate", "Corporate"), ("retail", "Retail")])],
                    "No customer groups yet.")]),
            "on_premises" or "onpremises" => new("on_premises", "On-Premises Deployment",
                "Self-hosted license registry and installer pack.",
                [("licenses", "Licenses")],
                ["Licenses shown", "Active"],
                [new("Licenses", null, ["Customer", "Tier", "Users max", "Status", "Hostname", "License", "Expires"],
                    [new("customer_name", "Customer"), new("tier", "Tier"), new("hostname", "Hostname")],
                    "No on-premises licenses yet.")]),
            "inventory_report" => new("inventory_report", "Inventory report",
                "Category hierarchy and valuation snapshots.",
                [("categories", "Categories"), ("snapshots", "Snapshots")],
                ["Categories", "Snapshots", "Snapshot value"],
                [
                    new("Categories", null, ["Code", "Name", "Level", "Parent", "Status"], [new("code", "Code"), new("name", "Name")], "No inventory categories yet."),
                    new("Snapshots", null, ["Date", "Category", "SKUs", "Qty", "Value", "Avg age"], [], "No snapshots yet."),
                ]),
            _ => new(key.Length == 0 ? "module" : key, ToTitle(key),
                "PHP-parity module layout. Create the first record from the form — live rows bind when the shop database is available.",
                [("list", "Records")],
                ["Records"],
                [new(ToTitle(key), null, ["Code", "Name", "Status"],
                    [new("code", "Code"), new("name", "Name")],
                    "No records yet — use the form to add the first one.")]),
        };
    }

    private static string ToTitle(string tab)
    {
        if (string.IsNullOrWhiteSpace(tab)) return "ERP Module";
        return string.Join(' ', tab.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
