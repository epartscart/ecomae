namespace EcomAE.Platform.Presentation;

/// <summary>
/// Fields an empty ERP menu should show, taken from the AX 2012 client study.
/// Column names reuse the workspace row. Other fields are stored with the row.
/// </summary>
public static class ErpAxMenuFields
{
    public sealed record Field(string Key, string Label, string Column, string Input = "text");

    public static IReadOnlyList<Field> For(ErpD365Part3Catalog.Page page)
        => Family(page) switch
        {
            "quotation" => Quotation(),
            "sales" => Sales(),
            "requisition" => Requisition(),
            "rfq" => Rfq(),
            "purchase" => Purchase(),
            "receipt" => Receipt(),
            "invoice" => Invoice(page),
            "vendor" => Party("Vendor account", "Vendor name"),
            "customer" => Party("Customer account", "Customer name"),
            "journal" => Journal(),
            "bank" => Bank(),
            "asset" => Asset(),
            "tracking" => Tracking(),
            "transfer" => Transfer(),
            "item" => Item(),
            "project" => Project(),
            "production" => Production(),
            "leave" => Leave(),
            "worker" => Worker(),
            "plan" => Plan(),
            _ => ByKind(page.Kind),
        };

    public static string Caption(ErpD365Part3Catalog.Page page, string column, string fallback)
    {
        foreach (var field in For(page))
        {
            if (field.Column.Equals(column, StringComparison.Ordinal))
            {
                return field.Label;
            }
        }

        return fallback;
    }

    public static IReadOnlyList<Field> Extras(ErpD365Part3Catalog.Page page)
    {
        var extras = new List<Field>();
        foreach (var field in For(page))
        {
            if (field.Column.Length == 0)
            {
                extras.Add(field);
            }
        }

        return extras;
    }

    public static string Pack(IReadOnlyDictionary<string, string> values, ErpD365Part3Catalog.Page page)
    {
        var lines = new List<string>();
        foreach (var field in Extras(page))
        {
            if (!values.TryGetValue(field.Key, out var raw))
            {
                continue;
            }

            var value = (raw ?? string.Empty).Trim().Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
            if (value.Length == 0)
            {
                continue;
            }

            if (value.Length > 80)
            {
                value = value[..80];
            }

            lines.Add(field.Key + "=" + value);
        }

        var packed = string.Join('\n', lines);
        return packed.Length <= 4000 ? packed : packed[..4000];
    }

    public static string Display(string? packed, ErpD365Part3Catalog.Page page)
    {
        if (string.IsNullOrWhiteSpace(packed))
        {
            return "";
        }

        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in Extras(page))
        {
            labels[field.Key] = field.Label;
        }

        var parts = new List<string>();
        foreach (var line in packed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cut = line.IndexOf('=');
            if (cut <= 0)
            {
                continue;
            }

            var key = line[..cut];
            var value = line[(cut + 1)..];
            if (value.Length == 0)
            {
                continue;
            }

            parts.Add((labels.TryGetValue(key, out var label) ? label : key) + ": " + value);
        }

        return string.Join(" · ", parts);
    }

    private static string Family(ErpD365Part3Catalog.Page page)
    {
        var hay = (page.Tab + " " + page.Label).ToLowerInvariant();
        if (hay.Contains("quotation", StringComparison.Ordinal)) return "quotation";
        if (hay.Contains("return order", StringComparison.Ordinal) || hay.Contains("sales_order", StringComparison.Ordinal) || hay.Contains("sales order", StringComparison.Ordinal)) return "sales";
        if (hay.Contains("requisition", StringComparison.Ordinal)) return "requisition";
        if (hay.Contains("rfq", StringComparison.Ordinal) || hay.Contains("request for quotation", StringComparison.Ordinal)) return "rfq";
        if (hay.Contains("invoice", StringComparison.Ordinal)) return "invoice";
        if (hay.Contains("product receipt", StringComparison.Ordinal) || hay.Contains("packing slip", StringComparison.Ordinal) || hay.Contains("goods receipt", StringComparison.Ordinal)) return "receipt";
        if (hay.Contains("purchase", StringComparison.Ordinal) || hay.Contains("purch", StringComparison.Ordinal)) return "purchase";
        if (hay.Contains("vendor", StringComparison.Ordinal) || hay.Contains("vend", StringComparison.Ordinal) || hay.Contains("supplier", StringComparison.Ordinal)) return "vendor";
        if (hay.Contains("customer", StringComparison.Ordinal) || hay.Contains("cust", StringComparison.Ordinal)) return "customer";
        if (hay.Contains("journal", StringComparison.Ordinal)) return "journal";
        if (hay.Contains("bank", StringComparison.Ordinal)) return "bank";
        if (hay.Contains("fixed asset", StringComparison.Ordinal) || hay.Contains("asset", StringComparison.Ordinal)) return "asset";
        if (hay.Contains("track", StringComparison.Ordinal) || hay.Contains("batch", StringComparison.Ordinal) || hay.Contains("serial", StringComparison.Ordinal) || hay.Contains("dimension", StringComparison.Ordinal)) return "tracking";
        if (hay.Contains("transfer", StringComparison.Ordinal)) return "transfer";
        if (hay.Contains("released product", StringComparison.Ordinal) || hay.Contains("item", StringComparison.Ordinal) || hay.Contains("product", StringComparison.Ordinal)) return "item";
        if (hay.Contains("project", StringComparison.Ordinal)) return "project";
        if (hay.Contains("production", StringComparison.Ordinal) || hay.Contains("bom", StringComparison.Ordinal)) return "production";
        if (hay.Contains("leave", StringComparison.Ordinal) || hay.Contains("time_off", StringComparison.Ordinal) || hay.Contains("absence", StringComparison.Ordinal)) return "leave";
        if (hay.Contains("worker", StringComparison.Ordinal) || hay.Contains("employee", StringComparison.Ordinal) || hay.Contains("position", StringComparison.Ordinal)) return "worker";
        if (hay.Contains("forecast", StringComparison.Ordinal) || hay.Contains("planned", StringComparison.Ordinal)) return "plan";
        return "document";
    }

    private static Field F(string key, string label, string column = "", string input = "text")
        => new(key, label, column, input);

    private static IReadOnlyList<Field> Quotation() =>
    [
        F("quotation", "Quotation", "code"),
        F("quotation_name", "Quotation name", "name"),
        F("customer", "Customer", "party"),
        F("status", "Status", "status"),
        F("delivery_date", "Delivery date", "date_from"),
        F("customer_ref", "Customer reference"),
        F("delivery_mode", "Delivery mode"),
        F("delivery_terms", "Delivery terms"),
        F("site", "Site"),
        F("warehouse", "Warehouse"),
        F("currency", "Currency"),
        F("discount", "Discount percent"),
        F("tax_group", "Tax group"),
    ];

    private static IReadOnlyList<Field> Sales() =>
    [
        F("sales_id", "Sales order", "code"),
        F("sales_name", "Delivery name", "name"),
        F("customer", "Customer", "party"),
        F("status", "Status", "status"),
        F("delivery_date", "Delivery date", "date_from"),
        F("qty", "Quantity", "qty"),
        F("customer_ref", "Customer reference"),
        F("customer_po", "Customer purchase order"),
        F("invoice_account", "Invoice account"),
        F("delivery_mode", "Delivery mode"),
        F("delivery_terms", "Delivery terms"),
        F("site", "Site"),
        F("warehouse", "Warehouse"),
        F("currency", "Currency"),
        F("payment_terms", "Payment terms"),
        F("tax_group", "Tax group"),
    ];

    private static IReadOnlyList<Field> Requisition() =>
    [
        F("requisition", "Requisition", "code"),
        F("name", "Name", "name"),
        F("requester", "Requester", "worker"),
        F("needed", "Needed by", "date_from"),
        F("item", "Item"),
        F("qty", "Quantity", "qty"),
        F("unit", "Unit"),
        F("site", "Site"),
        F("warehouse", "Warehouse"),
    ];

    private static IReadOnlyList<Field> Rfq() =>
    [
        F("rfq", "Request for quotation", "code"),
        F("name", "Name", "name"),
        F("vendor", "Vendor", "party"),
        F("expiry", "Reply by", "date_from"),
        F("item", "Item"),
        F("qty", "Quantity", "qty"),
        F("currency", "Currency"),
        F("delivery_mode", "Delivery mode"),
    ];

    private static IReadOnlyList<Field> Purchase() =>
    [
        F("purch_id", "Purchase order", "code"),
        F("name", "Name", "name"),
        F("vendor", "Vendor", "party"),
        F("status", "Status", "status"),
        F("delivery_date", "Delivery date", "date_from"),
        F("qty", "Quantity", "qty"),
        F("amount", "Amount", "amount"),
        F("site", "Site"),
        F("warehouse", "Warehouse"),
        F("currency", "Currency"),
        F("delivery_mode", "Delivery mode"),
        F("payment_terms", "Payment terms"),
        F("tax_group", "Tax group"),
    ];

    private static IReadOnlyList<Field> Receipt() =>
    [
        F("receipt", "Product receipt", "code"),
        F("vendor", "Vendor", "party"),
        F("received", "Receipt date", "date_from"),
        F("qty", "Quantity", "qty"),
        F("purchase_order", "Purchase order"),
        F("item", "Item"),
        F("warehouse", "Warehouse"),
        F("remain", "Remaining"),
    ];

    private static IReadOnlyList<Field> Invoice(ErpD365Part3Catalog.Page page)
    {
        var vendor = (page.Tab + page.Label).Contains("vend", StringComparison.OrdinalIgnoreCase)
            || (page.Tab + page.Area).Contains("ap", StringComparison.OrdinalIgnoreCase);
        return
        [
            F("invoice", "Invoice", "code"),
            F("description", "Description", "name"),
            F("party", vendor ? "Vendor" : "Customer", "party"),
            F("invoice_date", "Invoice date", "date_from"),
            F("due_date", "Due date", "date_to"),
            F("amount", "Amount", "amount"),
            F("currency", "Currency"),
            F("payment_terms", "Payment terms"),
            F("tax_group", "Tax group"),
            F("match", "Match status"),
        ];
    }

    private static IReadOnlyList<Field> Party(string account, string name) =>
    [
        F("account", account, "code"),
        F("name", name, "name"),
        F("status", "Status", "status"),
        F("group", "Group"),
        F("currency", "Currency"),
        F("tax_group", "Tax group"),
        F("payment_terms", "Payment terms"),
        F("delivery_terms", "Delivery terms"),
    ];

    private static IReadOnlyList<Field> Journal() =>
    [
        F("journal", "Journal", "code"),
        F("description", "Description", "name"),
        F("date", "Date", "date_from"),
        F("amount", "Amount", "amount"),
        F("account", "Account"),
        F("offset", "Offset account"),
        F("debit", "Debit"),
        F("credit", "Credit"),
        F("currency", "Currency"),
    ];

    private static IReadOnlyList<Field> Bank() =>
    [
        F("account", "Bank account", "code"),
        F("name", "Bank name", "name"),
        F("currency", "Currency"),
        F("iban", "IBAN"),
        F("swift", "SWIFT"),
        F("ledger", "Ledger account"),
    ];

    private static IReadOnlyList<Field> Asset() =>
    [
        F("number", "Asset number", "code"),
        F("name", "Name", "name"),
        F("status", "Status", "status"),
        F("group", "Group"),
        F("acquired", "Acquisition date"),
        F("price", "Acquisition price"),
        F("book", "Depreciation book"),
    ];

    private static IReadOnlyList<Field> Tracking() =>
    [
        F("group", "Tracking group", "code"),
        F("description", "Description", "name"),
        F("batch_active", "Batch active"),
        F("serial_active", "Serial active"),
        F("serial_control", "Serial number control"),
        F("physical", "Physical inventory"),
        F("financial", "Financial inventory"),
        F("blank_receipt", "Blank receipt allowed"),
        F("blank_issue", "Blank issue allowed"),
        F("coverage", "Coverage plan by dimension"),
    ];

    private static IReadOnlyList<Field> Transfer() =>
    [
        F("transfer", "Transfer", "code"),
        F("item", "Item", "name"),
        F("ship_date", "Ship date", "date_from"),
        F("qty", "Quantity", "qty"),
        F("from_warehouse", "From warehouse"),
        F("to_warehouse", "To warehouse"),
        F("status", "Status"),
    ];

    private static IReadOnlyList<Field> Item() =>
    [
        F("item", "Item number", "code"),
        F("name", "Product name", "name"),
        F("search_name", "Search name"),
        F("item_group", "Item group"),
        F("unit", "Unit"),
        F("sales_unit", "Sales unit"),
        F("purchase_unit", "Purchase unit"),
        F("tracking_group", "Tracking group"),
        F("tax_group", "Tax group"),
    ];

    private static IReadOnlyList<Field> Project() =>
    [
        F("project", "Project", "code"),
        F("name", "Name", "name"),
        F("customer", "Customer", "party"),
        F("status", "Status", "status"),
        F("start", "Start", "date_from"),
        F("end", "End", "date_to"),
        F("contract", "Project contract"),
        F("group", "Project group"),
    ];

    private static IReadOnlyList<Field> Production() =>
    [
        F("order", "Production order", "code"),
        F("item", "Item", "name"),
        F("qty", "Quantity", "qty"),
        F("start", "Start", "date_from"),
        F("warehouse", "Warehouse"),
        F("bom", "Bill of materials"),
        F("route", "Route"),
    ];

    private static IReadOnlyList<Field> Leave() =>
    [
        F("worker", "Worker", "worker"),
        F("type", "Leave type", "kind"),
        F("status", "Status", "status"),
        F("from", "From", "date_from"),
        F("to", "To", "date_to"),
        F("days", "Days", "qty"),
        F("reason", "Reason"),
        F("approver", "Approver"),
    ];

    private static IReadOnlyList<Field> Worker() =>
    [
        F("worker", "Worker", "code"),
        F("name", "Name", "name"),
        F("position", "Position"),
        F("department", "Department"),
        F("employment", "Employment start"),
    ];

    private static IReadOnlyList<Field> Plan() =>
    [
        F("plan", "Plan", "code"),
        F("item", "Item", "name"),
        F("qty", "Quantity", "qty"),
        F("needed", "Requirement date", "date_from"),
        F("site", "Site"),
        F("warehouse", "Warehouse"),
        F("vendor", "Vendor"),
    ];

    private static IReadOnlyList<Field> ByKind(ErpD365Part3Catalog.FormKind kind) => kind switch
    {
        ErpD365Part3Catalog.FormKind.Request =>
        [
            F("worker", "Worker", "worker"),
            F("type", "Type", "kind"),
            F("status", "Status", "status"),
            F("from", "From", "date_from"),
            F("to", "To", "date_to"),
            F("qty", "Quantity", "qty"),
            F("reason", "Reason"),
            F("approver", "Approver"),
        ],
        ErpD365Part3Catalog.FormKind.Order =>
        [
            F("number", "Number", "code"),
            F("item", "Item", "name"),
            F("party", "Party", "party"),
            F("status", "Status", "status"),
            F("needed", "Needed by", "date_from"),
            F("qty", "Quantity", "qty"),
            F("site", "Site"),
            F("warehouse", "Warehouse"),
            F("unit", "Unit"),
        ],
        ErpD365Part3Catalog.FormKind.Procurement =>
        [
            F("number", "Number", "code"),
            F("name", "Name", "name"),
            F("vendor", "Vendor", "party"),
            F("status", "Status", "status"),
            F("amount", "Amount", "amount"),
            F("qty", "Quantity", "qty"),
            F("currency", "Currency"),
            F("tax_group", "Tax group"),
            F("payment_terms", "Payment terms"),
        ],
        ErpD365Part3Catalog.FormKind.Forecast =>
        [
            F("item", "Item", "name"),
            F("party", "Party", "party"),
            F("period", "Period", "period"),
            F("qty", "Quantity", "qty"),
            F("site", "Site"),
            F("warehouse", "Warehouse"),
        ],
        ErpD365Part3Catalog.FormKind.Queue =>
        [
            F("reference", "Reference", "code"),
            F("party", "Party", "party"),
            F("status", "Status", "status"),
            F("warehouse", "Warehouse"),
            F("document", "Source document"),
        ],
        ErpD365Part3Catalog.FormKind.Action =>
        [
            F("reference", "Reference", "code"),
            F("name", "Name", "name"),
            F("status", "Status", "status"),
            F("document", "Document"),
            F("result", "Result"),
        ],
        ErpD365Part3Catalog.FormKind.Payroll =>
        [
            F("worker", "Worker", "worker"),
            F("period", "Period", "period"),
            F("earning", "Earning", "kind"),
            F("status", "Status", "status"),
            F("qty", "Hours", "qty"),
            F("amount", "Amount", "amount"),
            F("pay_cycle", "Pay cycle"),
        ],
        ErpD365Part3Catalog.FormKind.Calendar =>
        [
            F("code", "Code", "code"),
            F("name", "Calendar", "name"),
            F("from", "From", "date_from"),
            F("to", "To", "date_to"),
            F("country", "Country"),
            F("working_time", "Working time"),
        ],
        ErpD365Part3Catalog.FormKind.Plan =>
        [
            F("code", "Code", "code"),
            F("name", "Name", "name"),
            F("status", "Status", "status"),
            F("method", "Method"),
            F("site", "Site"),
        ],
        _ =>
        [
            F("code", "Code", "code"),
            F("name", "Name", "name"),
            F("status", "Status", "status"),
            F("group", "Group"),
            F("currency", "Currency"),
        ],
    };
}
