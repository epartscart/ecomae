using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// CSV checks for <c>epc_data_mig_validate</c>. Customers require name. Invoices require amount.
/// A clean file is ready to post onto <c>epc_data_migrations</c>. Masters are not created here.
/// </summary>
public static class ErpDataMigrationCsv
{
    public const int MaxRows = 2000;
    public const int MaxChars = 1_000_000;

    public static readonly string[] MigrationTypes = ["open_balance", "full_transaction", "master_data"];

    public static readonly IReadOnlyDictionary<string, string[]> Templates =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["customers"] = ["name", "email", "phone", "address", "city", "country", "tax_id", "group", "credit_limit", "payment_terms"],
            ["suppliers"] = ["name", "email", "phone", "address", "city", "country", "tax_id", "payment_terms", "bank_account"],
            ["products"] = ["sku", "name", "category", "unit", "cost_price", "sell_price", "tax_rate", "barcode", "weight", "stock_qty"],
            ["coa"] = ["account_code", "account_name", "account_type", "parent_code", "currency", "opening_balance"],
            ["open_balances"] = ["account_code", "customer_or_supplier", "debit", "credit", "currency", "reference", "date"],
            ["invoices"] = ["invoice_no", "customer", "date", "due_date", "amount", "tax", "total", "status", "reference"],
            ["inventory"] = ["sku", "warehouse", "location", "qty_on_hand", "cost_price", "batch_no", "expiry_date"],
        };

    public static ErpDataMigrationInspect Inspect(string? migrationType, string? entityType, string? fileName, string? csv)
    {
        var type = NormalizeType(migrationType);
        var entity = (entityType ?? "").Trim().ToLowerInvariant();
        if (!Templates.ContainsKey(entity))
        {
            return ErpDataMigrationInspect.Fail("Choose a migration entity.");
        }

        var name = string.IsNullOrWhiteSpace(fileName) ? entity + ".csv" : fileName.Trim();
        if (name.Length > 300)
        {
            name = name[..300];
        }

        var text = csv ?? "";
        if (text.Length > MaxChars)
        {
            return ErpDataMigrationInspect.Fail("The CSV is too large to check on this page.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return ErpDataMigrationInspect.Fail("Paste a CSV with a header row.");
        }

        var parsed = ParseRows(text);
        if (parsed.Count == 0)
        {
            return ErpDataMigrationInspect.Fail("Paste a CSV with a header row.");
        }

        var headers = parsed[0].Select(h => h.Trim()).ToArray();
        if (headers.All(string.IsNullOrWhiteSpace))
        {
            return ErpDataMigrationInspect.Fail("The first row must be column names.");
        }

        var headerKeys = headers
            .Where(h => h.Length > 0)
            .Select(NormalizeHeader)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var required = RequiredColumns(entity);
        foreach (var column in required)
        {
            if (!headerKeys.Contains(column))
            {
                return ErpDataMigrationInspect.Fail(
                    entity + " import needs a " + column + " column. Template: " + string.Join(", ", Templates[entity]) + ".");
            }
        }

        if (parsed.Count - 1 > MaxRows)
        {
            return ErpDataMigrationInspect.Fail("Keep the file to " + MaxRows.ToString(CultureInfo.InvariantCulture) + " data rows.");
        }

        var rows = new List<ErpDataMigrationRowDraft>();
        var valid = 0;
        var errors = new List<string>();
        for (var i = 1; i < parsed.Count; i++)
        {
            var cells = parsed[i];
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Length; c++)
            {
                if (headers[c].Length == 0)
                {
                    continue;
                }

                var value = c < cells.Length ? cells[c].Trim() : "";
                raw[headers[c]] = value;
                mapped[NormalizeHeader(headers[c])] = value;
            }

            var rowErrors = RowErrors(entity, mapped);
            var status = rowErrors.Count == 0 ? "valid" : "error";
            if (status == "valid")
            {
                valid++;
            }
            else
            {
                var line = "Row " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + string.Join("; ", rowErrors);
                if (errors.Count < 8)
                {
                    errors.Add(line);
                }
            }

            rows.Add(new ErpDataMigrationRowDraft(
                i + 1,
                JsonSerializer.Serialize(raw),
                JsonSerializer.Serialize(mapped),
                status,
                Clip(string.Join("; ", rowErrors), 500)));
        }

        var errorCount = rows.Count - valid;
        if (rows.Count == 0)
        {
            return ErpDataMigrationInspect.Fail("The CSV has a header and no data rows.");
        }

        if (errorCount > 0)
        {
            return new ErpDataMigrationInspect(
                false,
                errorCount.ToString(CultureInfo.InvariantCulture) + " row"
                    + (errorCount == 1 ? "" : "s") + " need a correction. " + string.Join(" ", errors),
                entity,
                type,
                name,
                headers,
                rows,
                valid,
                errorCount);
        }

        return new ErpDataMigrationInspect(
            true,
            rows.Count.ToString(CultureInfo.InvariantCulture) + " rows ready to post.",
            entity,
            type,
            name,
            headers,
            rows,
            valid,
            0);
    }

    public static string NormalizeType(string? migrationType)
    {
        var raw = (migrationType ?? "").Trim();
        return MigrationTypes.Contains(raw, StringComparer.Ordinal) ? raw : "open_balance";
    }

    public static string NormalizeHeader(string header)
        => header.Trim().ToLowerInvariant().Replace(' ', '_');

    public static IReadOnlyList<string[]> ParseRows(string csv)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var text = csv.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }

                continue;
            }

            if (ch == '"' && cell.Length == 0)
            {
                quoted = true;
                continue;
            }

            if (ch == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
                continue;
            }

            if (ch == '\n')
            {
                row.Add(cell.ToString());
                cell.Clear();
                if (row.Any(part => part.Trim().Length > 0))
                {
                    rows.Add(row.ToArray());
                }

                row.Clear();
                continue;
            }

            cell.Append(ch);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            if (row.Any(part => part.Trim().Length > 0))
            {
                rows.Add(row.ToArray());
            }
        }

        return rows;
    }

    private static IReadOnlyList<string> RequiredColumns(string entity)
        => entity switch
        {
            "products" => ["sku"],
            "customers" or "suppliers" => ["name"],
            "coa" or "open_balances" => ["account_code"],
            "invoices" => ["amount"],
            "inventory" => ["sku"],
            _ => [],
        };

    private static List<string> RowErrors(string entity, IReadOnlyDictionary<string, string> mapped)
    {
        var errors = new List<string>();
        if ((entity is "customers" or "suppliers") && string.IsNullOrWhiteSpace(Value(mapped, "name")))
        {
            errors.Add(entity == "customers" ? "Customer name is required" : "Supplier name is required");
        }

        if (entity == "products" && string.IsNullOrWhiteSpace(Value(mapped, "sku")) && string.IsNullOrWhiteSpace(Value(mapped, "name")))
        {
            errors.Add("SKU or name is required");
        }
        else if (entity == "products" && string.IsNullOrWhiteSpace(Value(mapped, "sku")))
        {
            errors.Add("SKU is required");
        }

        if (entity is "coa" or "open_balances" && string.IsNullOrWhiteSpace(Value(mapped, "account_code")))
        {
            errors.Add("Account code is required");
        }

        if (entity == "invoices" && string.IsNullOrWhiteSpace(Value(mapped, "amount")))
        {
            errors.Add("Amount is required");
        }

        if (entity == "inventory" && string.IsNullOrWhiteSpace(Value(mapped, "sku")))
        {
            errors.Add("SKU is required");
        }

        return errors;
    }

    private static string Value(IReadOnlyDictionary<string, string> mapped, string key)
        => mapped.TryGetValue(key, out var value) ? value : "";

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];
}

public sealed record ErpDataMigrationRowDraft(
    int RowNumber,
    string RawJson,
    string MappedJson,
    string Status,
    string Error);

public sealed record ErpDataMigrationInspect(
    bool Ok,
    string Message,
    string Entity,
    string MigrationType,
    string FileName,
    IReadOnlyList<string> Headers,
    IReadOnlyList<ErpDataMigrationRowDraft> Rows,
    int ValidRows,
    int ErrorRows)
{
    public static ErpDataMigrationInspect Fail(string message)
        => new(false, message, "", "", "", [], [], 0, 0);
}
