using EcomAE.Platform.Auth;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP jewellery industry tabs dumped into the five CP jewellery apps.
/// Company=2 / jewellery hosts show these modules; MAIN hides <c>jw_*</c> in topnav.
/// </summary>
public static class ErpJewelleryModuleChrome
{
    public sealed record TabSpec(string Key, string Label, string Title, string Subtitle, IReadOnlyList<string> Columns, string EmptyCopy);

    public static readonly IReadOnlyList<(string Key, string Label)> RepairTabs =
    [
        ("jw_repairs", "Repair jobs"),
        ("jw_repair_receipt", "Receipt"),
        ("jw_repair_register", "Register"),
        ("jw_repair_search", "Search"),
        ("jw_repair_sale", "Repair sale"),
        ("jw_repair_transfer", "Transfer"),
        ("jw_workshop_receive", "Workshop"),
        ("jw_repair_delivery", "Delivery"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> MasterTabs =
    [
        ("jw_karat", "Karat master"),
        ("gold_rate", "Gold rate"),
        ("gold_scheme", "Gold scheme"),
        ("jewellery_tag", "Jewellery tags"),
        ("jw_barcode", "Barcode"),
        ("jw_diamond", "Diamond"),
        ("jw_pearl", "Pearl"),
        ("jw_color_stone", "Color stone"),
        ("jw_design", "Design"),
        ("jw_currency", "Currency"),
        ("jw_rate_type", "Rate type"),
        ("jw_seed_data", "Seed data"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> FixingTabs =
    [
        ("jw_purchase_fixing", "Purchase fixing"),
        ("jw_metal_purchase", "Metal purchase"),
        ("jw_diamond_purchase", "Diamond purchase"),
        ("jw_sales_fixing", "Sales fixing"),
        ("jw_purchase_window", "Purchase window"),
        ("fix_unfix", "Fix / unfix"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> RetailTabs =
    [
        ("jw_retail_sales", "Retail POS"),
        ("jewellery", "Jewellery"),
        ("jw_metal_sales", "Metal sales"),
        ("jw_sales_return", "Sales return"),
        ("jw_sales_analysis", "Sales analysis"),
        ("retail_barcode", "Retail barcode"),
        ("retail_commerce", "Retail commerce"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> StockTabs =
    [
        ("jw_stock_verification", "Stock verification"),
        ("jw_metal_stock", "Metal stock"),
        ("jw_stock_balance", "Stock balance"),
    ];

    public static readonly IReadOnlyList<string> RepairJobColumns =
        ["No.", "Repair #", "Customer", "Phone", "Item", "Metal", "Karat", "Wt In (g)", "Repair Type", "Est. Cost", "Status", "Company", "Branch", "Actions"];

    public static readonly IReadOnlyList<string> RepairReceiptColumns =
        ["No.", "Job No", "Receipt Date", "Customer", "Phone", "Item", "Promise Date", "Status"];

    public static readonly IReadOnlyList<string> KaratColumns =
        ["No.", "Karat Code", "Desc", "Std. Purity", "Gravity", "Division"];

    public static readonly IReadOnlyList<string> GoldRateHistoryColumns =
        ["Date", "Gold 24K", "Gold 22K", "Gold 18K", "Silver", "Change"];

    public static readonly IReadOnlyList<string> TagColumns =
        ["TAG #", "Item description", "Category", "Karat", "Gross wt", "Net wt", "Stone wt", "Cost", "Location", "Status"];

    public static readonly IReadOnlyList<string> FixingColumns =
        ["No.", "Fix No", "Fix Date", "Ref Voc", "Party Code", "Metal", "Fixed Rate", "Net Wt", "Amount", "Status"];

    public static readonly IReadOnlyList<string> RetailSaleColumns =
        ["No.", "Inv No", "Inv Date", "Customer", "Salesman", "Items", "Net Amt", "Payment"];

    public static readonly IReadOnlyList<string> StockVerificationColumns =
        ["Id", "Branch", "Type", "Date", "No", "Location", "Total", "Scanned", "Status"];

    public static readonly IReadOnlyList<string> MetalStockColumns =
        ["No.", "Metal", "Item Code", "Description", "Karat", "Purity", "Type", "Stock Pcs", "Stock Gms"];

    public static readonly IReadOnlyList<(string Key, string Label, string Color)> RepairStatuses =
    [
        ("received", "Received", "#e65100"),
        ("in_progress", "In progress", "#1565c0"),
        ("ready", "Ready", "#2e7d32"),
        ("delivered", "Delivered", "#6a1b9a"),
    ];

    public static bool HasJewelleryStaffAccess(LegacySessionContext session)
        => session.Kind == LegacySessionKind.Admin
           && (session.Capabilities.Contains("erp") || session.Capabilities.Contains("cp"));

    public static string NormalizeTab(string? raw, IReadOnlyList<(string Key, string Label)> tabs, string fallback)
    {
        var key = (raw ?? string.Empty).Trim();
        if (key.Length == 0) return fallback;
        foreach (var tab in tabs)
        {
            if (string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase))
                return tab.Key;
        }

        return fallback;
    }

    public static string AppHref(string path, int company, params (string Name, string Value)[] query)
    {
        var parts = new List<string>();
        if (company > 0)
            parts.Add("company=" + company.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var (name, value) in query)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                continue;
            parts.Add(Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(value));
        }

        if (parts.Count == 0) return path;
        return path + "?" + string.Join("&", parts);
    }

    public static int CompanyFromQuery(string? raw)
    {
        if (int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0)
            return id;
        return 0;
    }

    public static string StatusColor(string? status)
    {
        var key = (status ?? string.Empty).Trim();
        foreach (var row in RepairStatuses)
        {
            if (string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase))
                return row.Color;
        }

        return "#64748b";
    }

    public static string StatusLabel(string? status)
    {
        var key = (status ?? string.Empty).Trim().Replace('_', ' ');
        return key.Length == 0 ? "—" : char.ToUpperInvariant(key[0]) + key[1..];
    }

    public static TabSpec MasterSpec(string tab) => tab switch
    {
        "gold_rate" => new("gold_rate", "Gold rate", "Gold Rate (Live)",
            "Fetch real-time gold, silver, and platinum rates from configured API providers. Rates bind when the shop rate board is available — demo numbers are not shown as live KPIs.",
            GoldRateHistoryColumns, "No rate history yet — configure a provider and refresh."),
        "gold_scheme" => new("gold_scheme", "Gold scheme", "Gold scheme",
            "Savings / gold-scheme enrolments against karat and tenure.",
            ["Scheme #", "Customer", "Karat", "Tenure", "Instalment", "Status"],
            "No gold-scheme enrolments yet."),
        "jewellery_tag" => new("jewellery_tag", "Jewellery tags", "Jewellery TAG System",
            "Unique TAG per jewellery item — generated at barcode level. Used for invoicing, purchase tracking, sales, and audit trail.",
            TagColumns, "No jewellery tags yet — generate the first tag from this panel."),
        "jw_barcode" => new("jw_barcode", "Barcode", "Jewellery barcode",
            "Barcode formats and prefix rules for tagged jewellery items.",
            ["Code", "Prefix", "Format", "Division", "Status"],
            "No barcode formats yet."),
        "jw_diamond" => new("jw_diamond", "Diamond", "Diamond master",
            "Diamond shape, color, clarity and carat masters.",
            ["Code", "Shape", "Color", "Clarity", "Carat", "Status"],
            "No diamond masters yet."),
        "jw_pearl" => new("jw_pearl", "Pearl", "Pearl master",
            "Pearl type, size and origin masters.",
            ["Code", "Type", "Size", "Origin", "Status"],
            "No pearl masters yet."),
        "jw_color_stone" => new("jw_color_stone", "Color stone", "Color stone master",
            "Colored-stone type and quality masters.",
            ["Code", "Stone", "Quality", "Origin", "Status"],
            "No color-stone masters yet."),
        "jw_design" => new("jw_design", "Design", "Design master",
            "Jewellery design codes used on tags and invoices.",
            ["Design", "Name", "Category", "Status"],
            "No design codes yet."),
        "jw_currency" => new("jw_currency", "Currency", "Jewellery currency",
            "Rate currencies used on metal purchase and POS.",
            ["Code", "Name", "Symbol", "Rate", "Status"],
            "No jewellery currencies yet."),
        "jw_rate_type" => new("jw_rate_type", "Rate type", "Rate type master",
            "Purchase / sales / making rate types.",
            ["Code", "Name", "Applies to", "Status"],
            "No rate types yet."),
        "jw_seed_data" => new("jw_seed_data", "Seed data", "Jewellery seed data",
            "Load default karat, rate type and division rows for a new jewellery company.",
            ["Pack", "Rows", "Status"],
            "No seed pack applied yet."),
        _ => new("jw_karat", "Karat master", "Karat Master",
            "Karat codes with standard purity, ranges and gravity.",
            KaratColumns, "No records"),
    };

    public static TabSpec FixingSpec(string tab) => tab switch
    {
        "jw_metal_purchase" => new("jw_metal_purchase", "Metal purchase", "Metal purchase",
            "Unfixed and fixed metal purchase vouchers.",
            ["Voc #", "Date", "Party", "Metal", "Karat", "Net Wt", "Amount", "Status"],
            "No metal purchases yet."),
        "jw_diamond_purchase" => new("jw_diamond_purchase", "Diamond purchase", "Diamond purchase",
            "Diamond purchase vouchers with stone details.",
            ["Voc #", "Date", "Party", "Stones", "Carat", "Amount", "Status"],
            "No diamond purchases yet."),
        "jw_sales_fixing" => new("jw_sales_fixing", "Sales fixing", "Metal sales fixing",
            "Fix metal rates against unfixed sales vouchers.",
            FixingColumns, "No sales fixings yet."),
        "jw_purchase_window" => new("jw_purchase_window", "Purchase window", "Purchase window",
            "Open purchase window for unfixed metal lots.",
            ["Window", "From", "To", "Metal", "Open lots", "Status"],
            "No purchase windows yet."),
        "fix_unfix" => new("fix_unfix", "Fix / unfix", "Fix / unfix",
            "Toggle a metal lot between fixed and unfixed rate.",
            ["Lot", "Voc", "Metal", "Rate", "State"],
            "No lots to fix or unfix."),
        _ => new("jw_purchase_fixing", "Purchase fixing", "Metal Purchase Fixing",
            "Fix metal rates against unfixed purchase vouchers.",
            FixingColumns, "No records"),
    };

    public static TabSpec RetailSpec(string tab) => tab switch
    {
        "jewellery" => new("jewellery", "Jewellery", "Jewellery retail",
            "Jewellery retail workspace — invoices, tags and metal stock.",
            RetailSaleColumns, "No jewellery retail vouchers yet."),
        "jw_metal_sales" => new("jw_metal_sales", "Metal sales", "Metal sales",
            "Bullion / metal sales by weight and rate.",
            ["Voc #", "Date", "Party", "Metal", "Karat", "Wt", "Rate", "Amount"],
            "No metal sales yet."),
        "jw_sales_return" => new("jw_sales_return", "Sales return", "Sales return",
            "Jewellery sales returns against retail invoices.",
            ["Return #", "Date", "Invoice", "Customer", "Amount", "Status"],
            "No sales returns yet."),
        "jw_sales_analysis" => new("jw_sales_analysis", "Sales analysis", "Sales analysis",
            "Retail jewellery sales by metal, karat and salesman.",
            ["Period", "Metal", "Karat", "Invoices", "Net Amt"],
            "No sales analysis rows yet."),
        "retail_barcode" => new("retail_barcode", "Retail barcode", "Retail barcode",
            "Scan barcodes at jewellery POS.",
            ["Barcode", "Tag", "Item", "Karat", "Status"],
            "No retail barcodes yet."),
        "retail_commerce" => new("retail_commerce", "Retail commerce", "Retail commerce",
            "Commerce / e-retail jewellery orders.",
            RetailSaleColumns, "No retail commerce vouchers yet."),
        _ => new("jw_retail_sales", "Retail POS", "Retail Sales (POS)",
            "Point-of-sale retail jewellery sales with metal rates and payments.",
            RetailSaleColumns, "No records"),
    };

    public static TabSpec StockSpec(string tab) => tab switch
    {
        "jw_metal_stock" => new("jw_metal_stock", "Metal stock", "Metal Stock Master",
            "Metal items with pricing, barcode and stock details.",
            MetalStockColumns, "No metal stock items yet."),
        "jw_stock_balance" => new("jw_stock_balance", "Stock balance", "Jewellery stock balance",
            "On-hand weight and value by metal and karat.",
            ["Metal", "Karat", "Pcs", "Gross wt", "Net wt", "Value"],
            "No stock balance rows yet."),
        _ => new("jw_stock_verification", "Stock verification", "Stock Verification",
            "Physical count vs computer stock, barcode scanning.",
            StockVerificationColumns, "No records"),
    };

    public static TabSpec RepairSpec(string tab) => tab switch
    {
        "jw_repair_receipt" => new("jw_repair_receipt", "Receipt", "Repair Receipt",
            "Customer repair job intake — receive items for repair.",
            RepairReceiptColumns, "No records"),
        "jw_repair_register" => new("jw_repair_register", "Register", "Repair register",
            "All repair jobs for the jewellery company.",
            RepairJobColumns, "No repair jobs yet"),
        "jw_repair_search" => new("jw_repair_search", "Search", "Repair search",
            "Find a repair by number, customer or phone.",
            RepairJobColumns, "No matching repair jobs."),
        "jw_repair_sale" => new("jw_repair_sale", "Repair sale", "Repair sale",
            "Invoice a completed repair back to the customer.",
            ["Repair #", "Customer", "Item", "Est. Cost", "Invoice", "Status"],
            "No repair sales yet."),
        "jw_repair_transfer" => new("jw_repair_transfer", "Transfer", "Repair transfer",
            "Transfer a job to workshop or another branch.",
            ["Repair #", "From", "To", "Item", "Status"],
            "No repair transfers yet."),
        "jw_workshop_receive" => new("jw_workshop_receive", "Workshop", "Workshop receive",
            "Workshop intake of transferred repair jobs.",
            ["Repair #", "Received", "Item", "Metal", "Status"],
            "No workshop receipts yet."),
        "jw_repair_delivery" => new("jw_repair_delivery", "Delivery", "Repair delivery",
            "Deliver repaired items back to the customer.",
            RepairReceiptColumns, "No repair deliveries yet."),
        _ => new("jw_repairs", "Repair jobs", "Jewellery repairs",
            "Receive items for repair, track workshop progress, deliver back to customer.",
            RepairJobColumns, "No repair jobs yet"),
    };
}
