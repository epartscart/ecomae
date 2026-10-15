using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Fit-out master workbook. Sheet names and headers follow the supplied reference files.
/// </summary>
public static class ErpFitOutWorkbook
{
    public static readonly string[] TemplateSheets =
    [
        "Customers", "Projects", "BOQ", "Budgets", "Contracts", "Purchase_Orders",
        "Goods_Receipts", "Material_Issues", "Customer_Invoices", "Collections",
        "PDC_Cheques", "Expenses", "Petty_Cash", "Employees", "Items",
        "Opening_Stock", "Opening_AR_AP", "Fixed_Assets", "Historical_Costs", "Documents",
    ];

    public sealed record Sheet(
        string Name,
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyDictionary<string, string>> Records);

    public static byte[] Template()
    {
        var sheets = TemplateSheets.Select(name => (name, Headers(name), new[] { Sample(name) })).ToArray();
        return Xlsx(sheets);
    }

    public static byte[] ExportPackage()
    {
        var book = Template();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("FITO_MASTER_IMPORT_TEMPLATE.xlsx");
            using (var stream = entry.Open())
            {
                stream.Write(book, 0, book.Length);
            }

            var readme = zip.CreateEntry("README.txt");
            using var writer = new StreamWriter(readme.Open(), new UTF8Encoding(false));
            writer.Write("Fit-out workbook plus a document manifest. Cheque images and drawings stay as files named in Documents.Relative_ZIP_Path. This package is not a full restoration archive.");
        }

        return ms.ToArray();
    }

    public static IReadOnlyList<Sheet> Read(byte[] xlsx)
    {
        using var ms = new MemoryStream(xlsx);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var workbook = Load(zip, "xl/workbook.xml");
        var rels = Load(zip, "xl/_rels/workbook.xml.rels");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace rNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var targets = rels.Root!.Elements(relNs + "Relationship")
            .ToDictionary(el => (string?)el.Attribute("Id") ?? "", el => (string?)el.Attribute("Target") ?? "", StringComparer.Ordinal);
        var sheets = new List<Sheet>();
        foreach (var sheet in workbook.Root!.Element(ns + "sheets")!.Elements(ns + "sheet"))
        {
            var name = (string?)sheet.Attribute("name") ?? "";
            var id = (string?)sheet.Attribute(rNs + "id") ?? "";
            if (!targets.TryGetValue(id, out var target))
            {
                continue;
            }

            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
            var rows = Rows(Load(zip, path));
            if (rows.Count == 0)
            {
                sheets.Add(new Sheet(name, [], []));
                continue;
            }

            var headers = rows[0];
            var records = new List<Dictionary<string, string>>();
            for (var i = 1; i < rows.Count; i++)
            {
                var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var c = 0; c < headers.Count; c++)
                {
                    record[headers[c]] = c < rows[i].Count ? rows[i][c] : "";
                }

                if (record.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                records.Add(record);
            }

            sheets.Add(new Sheet(name, headers, records));
        }

        return sheets;
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<Sheet> sheets)
    {
        var errors = new List<string>();
        foreach (var expected in TemplateSheets)
        {
            var sheet = sheets.FirstOrDefault(item => string.Equals(item.Name, expected, StringComparison.OrdinalIgnoreCase));
            if (sheet is null)
            {
                errors.Add(expected + " sheet is missing.");
                continue;
            }

            foreach (var header in Headers(expected))
            {
                if (!sheet.Headers.Contains(header, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(expected + " is missing " + header + ".");
                }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var idHeader = Headers(expected)[0];
            foreach (var row in sheet.Records)
            {
                var id = Cell(row, idHeader);
                if (id.Length == 0)
                {
                    errors.Add(expected + " has a row without " + idHeader + ".");
                    continue;
                }

                if (!seen.Add(id))
                {
                    errors.Add(expected + " repeats " + id + ".");
                }

                if (id.StartsWith("SAMPLE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ValidateRow(expected, id, row, errors);
            }
        }

        return errors;
    }

    private static void ValidateRow(string sheet, string id, IReadOnlyDictionary<string, string> row, List<string> errors)
    {
        if (sheet == "BOQ")
        {
            var qty = Num(row, "Quantity");
            var direct = ErpFitOutCalc.TotalDirectCost(
                qty,
                ErpFitOutCalc.DirectUnitCost(Num(row, "Material_Unit_Cost_AED"), Num(row, "Labour_Unit_Cost_AED"), Num(row, "Equipment_Unit_Cost_AED"), Num(row, "Subcontract_Unit_Cost_AED")),
                0);
            if (Math.Abs(direct - Num(row, "Direct_Cost_AED")) > 0.02m)
            {
                errors.Add("BOQ " + id + " direct cost does not match quantity times unit costs.");
            }

            var estimated = ErpFitOutCalc.EstimatedTotalCost(direct, Num(row, "Overhead_AED"), 0);
            var selling = ErpFitOutCalc.SellingFromMargin(estimated, Num(row, "Target_Margin_Pct"));
            var unit = qty == 0 ? 0 : ErpFitOutCalc.Money(selling / qty);
            if (Math.Abs(unit - Num(row, "Selling_Unit_Rate_AED")) > 0.05m)
            {
                errors.Add("BOQ " + id + " selling rate is not cost divided by one minus margin.");
            }
        }
        else if (sheet == "Budgets")
        {
            var current = ErpFitOutCalc.CurrentBudget(Num(row, "Original_Budget_AED"), Num(row, "Approved_Revisions_AED"));
            if (Math.Abs(current - Num(row, "Current_Budget_AED")) > 0.02m)
            {
                errors.Add("Budget " + id + " current budget is not original plus approved revisions.");
            }
        }
        else if (sheet == "Purchase_Orders")
        {
            var line = ErpFitOutCalc.Money(Num(row, "Quantity") * Num(row, "Unit_Price_AED"));
            if (Math.Abs(line - Num(row, "Line_Total_AED")) > 0.02m)
            {
                errors.Add("PO " + id + " line total is not quantity times unit price.");
            }
        }
        else if (sheet == "Customer_Invoices")
        {
            var net = ErpFitOutCalc.ProgressNet(Num(row, "Gross_Amount_AED"), Num(row, "Retention_AED"), Num(row, "Advance_Recovery_AED"), Num(row, "VAT_AED"));
            if (Math.Abs(net - Num(row, "Net_Receivable_AED")) > 0.02m)
            {
                errors.Add("Invoice " + id + " net receivable is not gross less retention and advance plus VAT.");
            }
        }
        else if (sheet == "Petty_Cash")
        {
            var balance = ErpFitOutCalc.PettyBalance(Num(row, "Advance_AED"), Num(row, "Settled_AED"));
            if (Math.Abs(balance - Num(row, "Balance_AED")) > 0.02m)
            {
                errors.Add("Petty cash " + id + " balance is not advance less settled.");
            }
        }
        else if (sheet == "PDC_Cheques" && ErpFitOutCalc.CountsAsBankCash(Cell(row, "Status")) && Cell(row, "Cleared_Date").Length == 0)
        {
            errors.Add("Cheque " + id + " is cleared without a cleared date.");
        }
    }

    public static string[] Headers(string sheet) => sheet switch
    {
        "Customers" => ["Customer_ID", "Customer_Name", "Customer_Type", "TRN", "Contact_Name", "Email", "Mobile", "Billing_Address", "Payment_Terms_Days", "Status"],
        "Projects" => ["Project_ID", "Customer_ID", "Project_Name", "Location", "Project_Manager", "Start_Date", "Planned_End_Date", "Contract_Value_AED", "Approved_Budget_AED", "Status"],
        "BOQ" => ["BOQ_ID", "Project_ID", "Revision", "Section", "Item_Code", "Description", "UOM", "Quantity", "Material_Unit_Cost_AED", "Labour_Unit_Cost_AED", "Equipment_Unit_Cost_AED", "Subcontract_Unit_Cost_AED", "Overhead_AED", "Target_Margin_Pct", "Direct_Cost_AED", "Selling_Unit_Rate_AED", "Line_Total_AED"],
        "Budgets" => ["Budget_ID", "Project_ID", "Cost_Code", "Category", "Original_Budget_AED", "Approved_Revisions_AED", "Current_Budget_AED"],
        "Contracts" => ["Contract_ID", "Project_ID", "Quotation_ID", "Signed_Date", "Start_Date", "End_Date", "Original_Value_AED", "Retention_Pct", "Advance_AED", "Status"],
        "Purchase_Orders" => ["PO_ID", "Project_ID", "Supplier_ID", "PO_Date", "Item_ID", "Cost_Code", "Quantity", "Unit_Price_AED", "Line_Total_AED", "Status"],
        "Goods_Receipts" => ["GRN_ID", "PO_ID", "Project_ID", "Item_ID", "Receipt_Date", "Quantity_Received", "Warehouse_ID", "Status"],
        "Material_Issues" => ["Issue_ID", "Project_ID", "Item_ID", "Warehouse_ID", "Issue_Date", "Quantity", "Unit_Cost_AED", "Cost_Code", "Status"],
        "Customer_Invoices" => ["Invoice_ID", "Project_ID", "Contract_ID", "Invoice_Date", "Due_Date", "Gross_Amount_AED", "Retention_AED", "Advance_Recovery_AED", "VAT_AED", "Net_Receivable_AED", "Status"],
        "Collections" => ["Receipt_ID", "Invoice_ID", "Project_ID", "Customer_ID", "Receipt_Date", "Payment_Method", "Amount_AED", "Bank_Ref", "Status"],
        "PDC_Cheques" => ["Cheque_ID", "Project_ID", "Customer_ID", "Invoice_ID", "Cheque_Number", "Bank", "Cheque_Date", "Amount_AED", "Received_Date", "Deposit_Date", "Cleared_Date", "Status", "Document_ID"],
        "Expenses" => ["Expense_ID", "Project_ID", "Cost_Code", "Expense_Date", "Expense_Category", "Supplier_ID", "Description", "Net_Amount_AED", "VAT_AED", "Status", "Document_ID"],
        "Petty_Cash" => ["PettyCash_ID", "Project_ID", "Employee_ID", "Date", "Description", "Advance_AED", "Settled_AED", "Balance_AED", "Status"],
        "Employees" => ["Employee_ID", "Full_Name", "Trade", "Job_Title", "Hourly_Cost_AED", "Joining_Date", "Status"],
        "Items" => ["Item_ID", "Description", "Category", "UOM", "Brand", "Specification", "Standard_Cost_AED", "Reorder_Level", "Status"],
        "Opening_Stock" => ["Stock_Ref", "Warehouse_ID", "Item_ID", "Opening_Date", "Opening_Quantity", "Unit_Cost_AED"],
        "Opening_AR_AP" => ["Opening_ID", "Account_Type", "Party_ID", "Project_ID", "Reference", "Opening_Date", "Debit_AED", "Credit_AED"],
        "Fixed_Assets" => ["Asset_ID", "Asset_Name", "Category", "Acquisition_Date", "Cost_AED", "Useful_Life_Months", "Project_ID", "Status"],
        "Historical_Costs" => ["Cost_Transaction_ID", "Project_ID", "Cost_Code", "Transaction_Date", "Source_Type", "Source_ID", "Description", "Amount_AED", "Posting_Status"],
        "Documents" => ["Document_ID", "Entity_Type", "Entity_ID", "Project_ID", "Document_Category", "File_Name", "Relative_ZIP_Path", "Revision", "Issue_Date", "Expiry_Date", "Confidentiality"],
        _ => ["Id"],
    };

    private static string[] Sample(string sheet)
    {
        var headers = Headers(sheet);
        var row = headers.Select(header => header.EndsWith("_AED", StringComparison.Ordinal) || header is "Quantity" or "Revision" or "Target_Margin_Pct" or "Retention_Pct" or "Hourly_Cost_AED"
            ? "0"
            : header.EndsWith("_Date", StringComparison.Ordinal) || header is "Date"
                ? "2026-10-15"
                : header is "Status" or "Posting_Status" ? "Draft" : header is "UOM" ? "Nos" : header.EndsWith("_ID", StringComparison.Ordinal) || header is "Stock_Ref" || header is "Reference" || header is "Bank_Ref" ? "SAMPLE-001" : "SAMPLE — replace").ToArray();
        return row;
    }

    private static string Cell(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value.Trim() : "";

    private static decimal Num(IReadOnlyDictionary<string, string> row, string key)
        => decimal.TryParse(Cell(row, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static XDocument Load(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new InvalidDataException("Workbook is missing " + path + ".");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static List<List<string>> Rows(XDocument sheet)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<List<string>>();
        foreach (var row in sheet.Descendants(ns + "row"))
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? "A";
                var column = 0;
                foreach (var ch in reference)
                {
                    if (!char.IsLetter(ch))
                    {
                        break;
                    }

                    column = (column * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
                }

                while (cells.Count < column - 1)
                {
                    cells.Add("");
                }

                cells.Add(cell.Descendants(ns + "t").FirstOrDefault()?.Value ?? "");
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static byte[] Xlsx(IReadOnlyList<(string Name, string[] Headers, string[][] Rows)> sheets)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var overrides = new StringBuilder();
            var sheetRefs = new StringBuilder();
            var rels = new StringBuilder();
            for (var s = 1; s <= sheets.Count; s++)
            {
                overrides.Append("<Override PartName=\"/xl/worksheets/sheet").Append(s)
                    .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                sheetRefs.Append("<sheet name=\"").Append(Xml(sheets[s - 1].Name)).Append("\" sheetId=\"").Append(s)
                    .Append("\" r:id=\"rId").Append(s).Append("\"/>");
                rels.Append("<Relationship Id=\"rId").Append(s)
                    .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
                    .Append(s).Append(".xml\"/>");
            }

            WriteEntry(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" + overrides + "</Types>");
            WriteEntry(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            WriteEntry(archive, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" + sheetRefs + "</sheets></workbook>");
            WriteEntry(archive, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + rels + "</Relationships>");
            for (var s = 0; s < sheets.Count; s++)
            {
                var data = new StringBuilder();
                var allRows = new List<string[]> { sheets[s].Headers };
                allRows.AddRange(sheets[s].Rows);
                for (var r = 0; r < allRows.Count; r++)
                {
                    data.Append("<row r=\"").Append(r + 1).Append("\">");
                    for (var c = 0; c < allRows[r].Length && c < 26; c++)
                    {
                        var value = allRows[r][c] ?? "";
                        if (value.Length == 0)
                        {
                            continue;
                        }

                        data.Append("<c r=\"").Append((char)('A' + c)).Append(r + 1).Append("\" t=\"inlineStr\"><is><t>")
                            .Append(Xml(value)).Append("</t></is></c>");
                    }

                    data.Append("</row>");
                }

                WriteEntry(archive, "xl/worksheets/sheet" + (s + 1) + ".xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" + data + "</sheetData></worksheet>");
            }
        }

        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? "";
}
