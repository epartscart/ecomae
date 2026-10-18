using System.Globalization;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

public sealed record ErpSheetField(
    string Name,
    string Label,
    string Type,
    bool Wide = false,
    string Placeholder = "",
    IReadOnlyList<(string Value, string Text)>? Options = null,
    string MasterKind = "",
    string ValueMode = "id");

public sealed record ErpSheetDefinition(
    string Key,
    string Title,
    string Intro,
    IReadOnlyList<ErpSheetField> Fields);

/// <summary>Entry fields for ERP pages that were a posted list with no form.</summary>
public static class ErpSheetEntryCatalog
{
    public const string DataMigration = "data_migration";
    public const string StockTransfer = "stock_transfer";
    public const string StockMovement = "stock_movement";
    public const string StaffProfile = "staff_profile";
    public const string InventoryReport = "inventory_report";
    public const string FinanceClose = "finance_close";

    private static readonly IReadOnlyList<(string Value, string Text)> MigrationTypes =
    [
        ("open_balance", "Open balance"),
        ("full_transaction", "Full transaction"),
        ("master_data", "Master data"),
    ];

    private static readonly IReadOnlyList<(string Value, string Text)> Entities =
    [
        ("customers", "Customers"),
        ("suppliers", "Suppliers"),
        ("products", "Products"),
        ("coa", "Chart of accounts"),
        ("open_balances", "Open balances"),
        ("invoices", "Invoices"),
        ("inventory", "Inventory"),
    ];

    private static readonly IReadOnlyList<(string Value, string Text)> MovementTypes =
    [
        ("opening", "Opening"),
        ("purchase_in", "Purchase in"),
        ("sale_out", "Sale out"),
        ("adjustment", "Adjustment"),
        ("return_in", "Return in"),
        ("return_out", "Return out"),
        ("transfer_in", "Transfer in"),
        ("transfer_out", "Transfer out"),
    ];

    private static readonly IReadOnlyList<(string Value, string Text)> OpeningModules =
    [
        ("combined", "Combined"),
        ("coa", "Chart of accounts"),
        ("cash_bank", "Cash and bank"),
        ("inventory", "Inventory"),
        ("fixed_assets", "Fixed assets"),
    ];

    private static readonly IReadOnlyDictionary<string, ErpSheetDefinition> Sheets =
        new Dictionary<string, ErpSheetDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [DataMigration] = new(
                DataMigration,
                "New import",
                "Paste the CSV. Validate checks the template. Post stores the job and its rows. Open a posted job to review mapping and row status. This post does not create customer, supplier, or item masters.",
                [
                    new("migration_type", "Migration type", "select", Options: MigrationTypes),
                    new("entity_type", "Entity", "select", Options: Entities),
                    new("file_name", "File name", "text", Placeholder: "customers.csv"),
                    new("csv", "CSV", "textarea", Wide: true, Placeholder: "name,email\nAcme Trading,acme@example.com"),
                ]),
            [StockTransfer] = new(
                StockTransfer,
                "New stock transfer",
                "Validate checks the warehouses and quantity. Post saves a draft transfer. Open the posted transfer to review it.",
                [
                    new("from_warehouse_id", "From warehouse", "master", MasterKind: "warehouse"),
                    new("to_warehouse_id", "To warehouse", "master", MasterKind: "warehouse"),
                    new("sku", "Item", "master", MasterKind: "item", ValueMode: "code"),
                    new("qty", "Quantity", "number", Placeholder: "0"),
                    new("reason", "Reason", "text", Placeholder: "Replenishment"),
                    new("notes", "Notes", "text"),
                ]),
            [StockMovement] = new(
                StockMovement,
                "New stock movement",
                "Validate checks warehouse, item, and quantity. Post writes the inventory movement ledger. Open a posted movement to review the note.",
                [
                    new("movement_type", "Movement", "select", Options: MovementTypes),
                    new("warehouse_id", "Warehouse", "master", MasterKind: "warehouse"),
                    new("item_id", "Item", "master", MasterKind: "item"),
                    new("qty", "Quantity", "number"),
                    new("unit_cost", "Unit cost", "number"),
                    new("reference", "Reference", "text"),
                    new("movement_date", "Date", "date"),
                    new("note", "Note", "text", Wide: true),
                ]),
            [StaffProfile] = new(
                StaffProfile,
                "New staff profile",
                "Validate checks the user, name, and department. Post saves the staff profile. Open a posted profile to review it. Email and phone stay off the open pane.",
                [
                    new("user_id", "User", "master", MasterKind: "customer"),
                    new("display_name", "Name", "text"),
                    new("department_code", "Department", "select", Options: ErpStaffDepartmentCatalog.All.Select(d => (d.Code, d.Name)).ToArray()),
                    new("job_title", "Job title", "text"),
                    new("email", "Email", "text"),
                    new("phone", "Phone", "text"),
                ]),
            [InventoryReport] = new(
                InventoryReport,
                "New inventory report row",
                "Choose a category or a snapshot. Validate checks the required fields. Post saves the row. Open the posted list to review it.",
                [
                    new("kind", "Record", "select", Options: [("snapshot", "Snapshot"), ("category", "Category")]),
                    new("code", "Category code", "text"),
                    new("name", "Category name", "text"),
                    new("snapshot_date", "Snapshot date", "date"),
                    new("category_id", "Category id", "number", Placeholder: "0"),
                    new("total_skus", "SKUs", "number"),
                    new("total_qty", "Quantity", "number"),
                    new("total_value", "Value", "number"),
                ]),
            [FinanceClose] = new(
                FinanceClose,
                "New opening batch",
                "Validate checks the date, account, and amount. Post saves a draft opening batch and one chart line. Ledger post of that batch stays on the opening journal.",
                [
                    new("module", "Module", "select", Options: OpeningModules),
                    new("as_of_date", "As of date", "date"),
                    new("reference", "Reference", "text"),
                    new("account_id", "Chart account", "master", MasterKind: "coa"),
                    new("debit", "Debit", "number"),
                    new("credit", "Credit", "number"),
                    new("note", "Note", "text", Wide: true),
                ]),
        };

    public static bool TryGet(string? key, out ErpSheetDefinition sheet)
    {
        if (!string.IsNullOrWhiteSpace(key) && Sheets.TryGetValue(key.Trim(), out var found))
        {
            sheet = found;
            return true;
        }

        sheet = Sheets[DataMigration];
        return false;
    }

    public static ErpSheetCheck Check(string? sheetKey, IReadOnlyDictionary<string, string> fields)
    {
        if (!TryGet(sheetKey, out var sheet))
        {
            return ErpSheetCheck.Fail("Unknown entry.");
        }

        return sheet.Key switch
        {
            DataMigration => CheckMigration(fields),
            StockTransfer => CheckTransfer(fields),
            StockMovement => CheckMovement(fields),
            StaffProfile => CheckStaff(fields),
            InventoryReport => CheckInventory(fields),
            FinanceClose => CheckOpening(fields),
            _ => ErpSheetCheck.Fail("Unknown entry."),
        };
    }

    private static ErpSheetCheck CheckMigration(IReadOnlyDictionary<string, string> fields)
    {
        var inspect = ErpDataMigrationCsv.Inspect(
            Text(fields, "migration_type"),
            Text(fields, "entity_type"),
            Text(fields, "file_name"),
            Text(fields, "csv"));
        return inspect.Ok ? ErpSheetCheck.Ready(inspect.Message) : ErpSheetCheck.Fail(inspect.Message);
    }

    private static ErpSheetCheck CheckTransfer(IReadOnlyDictionary<string, string> fields)
    {
        var from = Long(fields, "from_warehouse_id");
        var to = Long(fields, "to_warehouse_id");
        if (from <= 0 || to <= 0)
        {
            return ErpSheetCheck.Fail("From and to warehouse ids are required.");
        }

        if (from == to)
        {
            return ErpSheetCheck.Fail("From and to warehouses must differ.");
        }

        if (string.IsNullOrWhiteSpace(Text(fields, "sku")))
        {
            return ErpSheetCheck.Fail("SKU is required.");
        }

        if (Dec(fields, "qty") <= 0)
        {
            return ErpSheetCheck.Fail("Quantity must be greater than zero.");
        }

        return ErpSheetCheck.Ready("Transfer is ready to post.");
    }

    private static ErpSheetCheck CheckMovement(IReadOnlyDictionary<string, string> fields)
    {
        var type = Text(fields, "movement_type");
        if (type.Length == 0)
        {
            type = "adjustment";
        }

        if (type is not ("opening" or "purchase_in" or "sale_out" or "adjustment" or "return_in" or "return_out" or "transfer_in" or "transfer_out"))
        {
            return ErpSheetCheck.Fail("Choose a movement type.");
        }

        if (Long(fields, "warehouse_id") <= 0 || Long(fields, "item_id") <= 0)
        {
            return ErpSheetCheck.Fail("Warehouse and item are required.");
        }

        if (Dec(fields, "qty") == 0)
        {
            return ErpSheetCheck.Fail("Quantity is required.");
        }

        return ErpSheetCheck.Ready("Movement is ready to post.");
    }

    private static ErpSheetCheck CheckStaff(IReadOnlyDictionary<string, string> fields)
    {
        if (Long(fields, "user_id") <= 0)
        {
            return ErpSheetCheck.Fail("User id is required.");
        }

        if (string.IsNullOrWhiteSpace(Text(fields, "display_name")))
        {
            return ErpSheetCheck.Fail("Name is required.");
        }

        var department = Text(fields, "department_code");
        if (ErpStaffDepartmentCatalog.All.All(d => !string.Equals(d.Code, department, StringComparison.OrdinalIgnoreCase)))
        {
            return ErpSheetCheck.Fail("Choose a department.");
        }

        return ErpSheetCheck.Ready("Staff profile is ready to post.");
    }

    private static ErpSheetCheck CheckInventory(IReadOnlyDictionary<string, string> fields)
    {
        var kind = Text(fields, "kind");
        if (kind == "category")
        {
            if (string.IsNullOrWhiteSpace(Text(fields, "code")) || string.IsNullOrWhiteSpace(Text(fields, "name")))
            {
                return ErpSheetCheck.Fail("Category code and name are required.");
            }

            return ErpSheetCheck.Ready("Category is ready to post.");
        }

        if (!DateTime.TryParseExact(Text(fields, "snapshot_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return ErpSheetCheck.Fail("Snapshot date is required.");
        }

        return ErpSheetCheck.Ready("Snapshot is ready to post.");
    }

    private static ErpSheetCheck CheckOpening(IReadOnlyDictionary<string, string> fields)
    {
        if (!DateTime.TryParseExact(Text(fields, "as_of_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return ErpSheetCheck.Fail("As of date is required.");
        }

        if (Long(fields, "account_id") <= 0)
        {
            return ErpSheetCheck.Fail("Account id is required.");
        }

        if (Dec(fields, "debit") == 0 && Dec(fields, "credit") == 0)
        {
            return ErpSheetCheck.Fail("Debit or credit is required.");
        }

        return ErpSheetCheck.Ready("Opening batch is ready to post.");
    }

    public static string Text(IReadOnlyDictionary<string, string> fields, string name)
        => fields.TryGetValue(name, out var value) ? value.Trim() : "";

    public static long Long(IReadOnlyDictionary<string, string> fields, string name)
        => long.TryParse(Text(fields, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public static decimal Dec(IReadOnlyDictionary<string, string> fields, string name)
        => decimal.TryParse(Text(fields, name), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;
}

public sealed record ErpSheetCheck(bool Ok, string Message)
{
    public static ErpSheetCheck Ready(string message) => new(true, message);

    public static ErpSheetCheck Fail(string message) => new(false, message);
}
