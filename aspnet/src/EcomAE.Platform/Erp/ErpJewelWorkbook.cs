using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Jewellery master workbook. Headers follow the supplied reference files.
/// Tag weights are checked from gross, stone, and other grams. Melting variance is checked and the input weight is left as entered.
/// </summary>
public static class ErpJewelWorkbook
{
    public static readonly string[] TemplateSheets =
    [
        "Companies", "Branches", "Customers", "Metal_Masters", "Metal_Rates", "Design_Masters",
        "Items_Tags", "Item_Stones", "Assay_Results", "Buyback_Exchange", "Inventory_Movements",
        "Jobwork_Making", "Melting_Refining", "Consignment", "Gold_Ledger_Opening", "AR_AP_Opening",
        "GL_Opening", "Expenses", "Documents", "AML_KYC",
    ];

    public sealed record Sheet(
        string Name,
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyDictionary<string, string>> Records);

    public static byte[] Template()
        => Xlsx(TemplateSheets.Select(name => (name, Headers(name), SheetRows(name))).ToArray());

    public static byte[] ExportPackage()
    {
        var book = Template();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("INDJ_JEWELLERY_MASTER_IMPORT_TEMPLATE.xlsx");
            using (var stream = entry.Open())
            {
                stream.Write(book, 0, book.Length);
            }

            var readme = zip.CreateEntry("README.txt");
            using var writer = new StreamWriter(readme.Open(), new UTF8Encoding(false));
            writer.Write("Jewellery workbook. Net metal is gross minus stone grams minus other grams. Pure metal is net times purity per thousand. One carat is 0.2 gram and is not stored in the metal columns. A melt variance does not replace the original tag weight. Photos stay as files named in Documents.Relative_ZIP_Path. This package is not a full restoration archive.");
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
            var sheet = Find(sheets, expected);
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
            foreach (var row in sheet.Records)
            {
                var id = Identity(expected, row);
                if (id.Length == 0 || id.StartsWith('|'))
                {
                    errors.Add(expected + " has a row without its reference id.");
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

                ValidateRow(expected, id, row, sheets, errors);
            }
        }

        return errors;
    }

    private static void ValidateRow(string sheet, string id, IReadOnlyDictionary<string, string> row, IReadOnlyList<Sheet> sheets, List<string> errors)
    {
        switch (sheet)
        {
            case "Items_Tags":
                RequireRef(errors, sheets, sheet, id, "Design_Masters", "Design_ID", Cell(row, "Design_ID"));
                RequireRef(errors, sheets, sheet, id, "Metal_Masters", "Metal_ID", Cell(row, "Metal_ID"));
                RequireRef(errors, sheets, sheet, id, "Branches", "Branch_ID", Cell(row, "Branch_ID"));
                CheckWeights(errors, "Tag " + id, Num(row, "Gross_Gram"), Num(row, "Stone_Gram"), Num(row, "Other_Gram"), Num(row, "Net_Metal_Gram"), Num(row, "Purity_PPT"), Num(row, "Pure_Gram"));
                if (Num(row, "Stone_Carat") > 0 && Num(row, "Stone_Gram") > 0 && !ErpJewelCalc.WithinTolerance(Num(row, "Stone_Gram"), ErpJewelCalc.StoneGrams(Num(row, "Stone_Carat")), 0.02m))
                {
                    errors.Add("Tag " + id + " stone grams are not carats times 0.2.");
                }

                break;
            case "Item_Stones":
                RequireRef(errors, sheets, sheet, id, "Items_Tags", "Tag_ID", Cell(row, "Tag_ID"));
                if (Num(row, "Carats") > 0 && Num(row, "Weight_Gram") > 0 && !ErpJewelCalc.WithinTolerance(Num(row, "Weight_Gram"), ErpJewelCalc.StoneGrams(Num(row, "Carats")), 0.02m))
                {
                    errors.Add("Stone " + id + " gram weight is not carats times 0.2.");
                }

                break;
            case "Buyback_Exchange":
                RequireRef(errors, sheets, sheet, id, "Customers", "Customer_ID", Cell(row, "Customer_ID"));
                RequireRef(errors, sheets, sheet, id, "Branches", "Branch_ID", Cell(row, "Branch_ID"));
                var pure = ErpJewelCalc.PureMetal(Num(row, "Old_Gold_Gram"), Num(row, "Assayed_Purity_PPT"));
                if (!ErpJewelCalc.WithinTolerance(Num(row, "Pure_Gram"), pure, 0.0001m))
                {
                    errors.Add("Buyback " + id + " pure grams are not net gold times purity.");
                }

                var value = ErpJewelCalc.Money(pure * Num(row, "Rate_AED"));
                if (Math.Abs(value - Num(row, "Buyback_Value_AED")) > 0.02m)
                {
                    errors.Add("Buyback " + id + " value is not pure grams times the approved rate.");
                }

                break;
            case "Melting_Refining":
                var outputPure = ErpJewelCalc.PureMetal(Num(row, "Output_Gross_Gram"), Num(row, "Output_Purity_PPT"));
                if (!ErpJewelCalc.WithinTolerance(Num(row, "Output_Pure_Gram"), outputPure, 0.0001m))
                {
                    errors.Add("Melt " + id + " output pure weight does not match output gross and purity.");
                }

                var variance = ErpJewelCalc.MeltVariance(Num(row, "Output_Pure_Gram"), 0, Num(row, "Input_Pure_Gram"));
                if (!ErpJewelCalc.WithinTolerance(Num(row, "Pure_Variance_Gram"), variance, 0.0001m))
                {
                    errors.Add("Melt " + id + " variance is not output pure minus input pure.");
                }

                if (ErpJewelCalc.MeltNeedsApproval(variance, ErpJewelCalc.DefaultToleranceGrams) && Cell(row, "Approved_By").Length == 0)
                {
                    errors.Add("Melt " + id + " variance needs approval.");
                }

                break;
            case "Jobwork_Making":
                var loss = ErpJewelCalc.Grams(Num(row, "Input_Metal_Gram") - Num(row, "Actual_Output_Gram"));
                if (!ErpJewelCalc.WithinTolerance(Num(row, "Loss_Gram"), loss, 0.0001m))
                {
                    errors.Add("Job " + id + " loss is not input grams minus actual output.");
                }

                break;
            case "Inventory_Movements":
                RequireRef(errors, sheets, sheet, id, "Items_Tags", "Tag_ID", Cell(row, "Tag_ID"));
                RequireRef(errors, sheets, sheet, id, "Branches", "Branch_ID", Cell(row, "From_Branch"));
                RequireRef(errors, sheets, sheet, id, "Branches", "Branch_ID", Cell(row, "To_Branch"));
                break;
            case "Consignment":
                RequireRef(errors, sheets, sheet, id, "Items_Tags", "Tag_ID", Cell(row, "Tag_ID"));
                break;
            case "Documents" when Cell(row, "Entity_Type").Equals("Item", StringComparison.OrdinalIgnoreCase):
                RequireRef(errors, sheets, sheet, id, "Items_Tags", "Tag_ID", Cell(row, "Entity_ID"));
                break;
            case "Branches":
                RequireRef(errors, sheets, sheet, id, "Companies", "Company_ID", Cell(row, "Company_ID"));
                break;
            case "Design_Masters":
                RequireRef(errors, sheets, sheet, id, "Metal_Masters", "Metal_ID", Cell(row, "Metal_ID"));
                break;
            case "AML_KYC" when Cell(row, "Party_Type").Equals("Customer", StringComparison.OrdinalIgnoreCase):
                RequireRef(errors, sheets, sheet, id, "Customers", "Customer_ID", Cell(row, "Party_ID"));
                break;
        }
    }

    private static void CheckWeights(List<string> errors, string label, decimal gross, decimal stone, decimal other, decimal net, decimal purity, decimal pure)
    {
        var expectedNet = ErpJewelCalc.NetMetal(gross, stone, other);
        if (expectedNet < 0)
        {
            errors.Add(label + " stone and other weight exceed gross weight.");
            return;
        }

        if (!ErpJewelCalc.WithinTolerance(net, expectedNet, 0.0001m))
        {
            errors.Add(label + " net metal is not gross minus stone and other grams.");
        }

        if (purity < 0 || purity > 1000)
        {
            errors.Add(label + " purity is outside 0 to 1000.");
        }

        var expectedPure = ErpJewelCalc.PureMetal(expectedNet, purity);
        if (!ErpJewelCalc.WithinTolerance(pure, expectedPure, 0.0001m))
        {
            errors.Add(label + " pure grams are not net metal times purity per thousand.");
        }
    }

    private static void RequireRef(List<string> errors, IReadOnlyList<Sheet> sheets, string sheet, string id, string refSheet, string refHeader, string value)
    {
        if (value.Length == 0 || value.StartsWith("SAMPLE", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var known = Find(sheets, refSheet);
        if (known is null)
        {
            return;
        }

        if (!known.Records.Any(row => string.Equals(Cell(row, refHeader), value, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(sheet + " " + id + " references missing " + refHeader + " " + value + ".");
        }
    }

    public static string[] Headers(string sheet) => sheet switch
    {
        "Companies" => ["Company_ID", "Legal_Name", "TRN", "Emirate", "Currency", "Branch_ID", "Status"],
        "Branches" => ["Branch_ID", "Company_ID", "Branch_Name", "Emirate", "Manager", "Stock_Location"],
        "Customers" => ["Customer_ID", "Name", "Type", "Mobile", "Email", "TRN", "KYC_Reference", "AML_Risk"],
        "Metal_Masters" => ["Metal_ID", "Metal", "Purity_PPT", "Karat", "Unit", "Tolerance_Gram"],
        "Metal_Rates" => ["Rate_ID", "Rate_Date", "Metal", "Purity", "AED_Per_Gram", "Source", "Approved_By"],
        "Design_Masters" => ["Design_ID", "Category", "Design_Name", "Metal_ID", "Style", "Brand", "Making_Method"],
        "Items_Tags" => ["Tag_ID", "SKU", "Design_ID", "Metal_ID", "Branch_ID", "Status", "Gross_Gram", "Stone_Gram", "Other_Gram", "Net_Metal_Gram", "Purity_PPT", "Pure_Gram", "Stone_Carat", "Cost_AED", "Image_Path"],
        "Item_Stones" => ["Tag_ID", "Stone_Line_ID", "Stone_ID", "Pieces", "Carats", "Weight_Gram", "Cost_AED", "Certificate_No"],
        "Assay_Results" => ["Assay_ID", "Source_Type", "Source_ID", "Sample_ID", "Test_Method", "Declared_Purity", "Tested_Purity", "Tester", "Test_Date", "Attachment_Path"],
        "Buyback_Exchange" => ["Buyback_ID", "Date", "Customer_ID", "Branch_ID", "Type", "Old_Gold_Gram", "Assayed_Purity_PPT", "Pure_Gram", "Rate_AED", "Buyback_Value_AED", "New_Sale_ID", "Settlement_Method"],
        "Inventory_Movements" => ["Movement_ID", "Date", "Tag_ID", "From_Branch", "To_Branch", "Movement_Type", "Gross_Gram", "Pure_Gram", "Value_AED", "Reference_ID"],
        "Jobwork_Making" => ["Job_ID", "Vendor_ID", "Input_Metal_Gram", "Input_Pure_Gram", "Stone_Issued_Carat", "Expected_Output_Gram", "Actual_Output_Gram", "Loss_Gram", "Making_Charge_AED", "Status"],
        "Melting_Refining" => ["Melt_ID", "Date", "Batch_ID", "Input_Gross_Gram", "Input_Pure_Gram", "Output_Gross_Gram", "Output_Purity_PPT", "Output_Pure_Gram", "Pure_Variance_Gram", "Approved_By"],
        "Consignment" => ["Consignment_ID", "Party_ID", "Direction", "Tag_ID", "Date_In", "Date_Due", "Gross_Gram", "Pure_Gram", "Status"],
        "Gold_Ledger_Opening" => ["Opening_ID", "Owner_Type", "Owner_ID", "Metal_ID", "As_Of_Date", "Gross_Gram", "Pure_Gram", "Value_AED"],
        "AR_AP_Opening" => ["Balance_ID", "Party_Type", "Party_ID", "Invoice_Ref", "Date", "Due_Date", "Balance_AED"],
        "GL_Opening" => ["Account_Code", "Company_ID", "Branch_ID", "As_Of_Date", "Debit_AED", "Credit_AED"],
        "Expenses" => ["Expense_ID", "Date", "Branch_ID", "Category", "Supplier_ID", "Description", "Amount_AED", "VAT_AED", "GL_Code", "Attachment_Path"],
        "Documents" => ["Document_ID", "Entity_Type", "Entity_ID", "Document_Type", "File_Name", "Relative_ZIP_Path", "Expiry_Date", "Version"],
        "AML_KYC" => ["Review_ID", "Party_Type", "Party_ID", "Review_Date", "Risk_Rating", "ID_Document_Ref", "Screening_Result", "Approved_By"],
        _ => ["Id"],
    };

    private static string[][] SheetRows(string sheet) => sheet switch
    {
        "Companies" => [["INDJ-001", "INDJ Jewellery", "100000000000003", "Dubai", "AED", "DXB01", "Active"]],
        "Branches" => [["DXB01", "INDJ-001", "Dubai Showroom", "Dubai", "Amina", "Vault"]],
        "Customers" => [["CUST-001", "Layla Hassan", "Individual", "0500000000", "layla@example.com", "", "KYC-001", "Low"]],
        "Metal_Masters" => [["AU916", "Gold", "916", "22K", "g", "0.01"]],
        "Metal_Rates" => [["RATE-001", "2026-10-15", "Gold", "916", "300", "Board", "Manager"]],
        "Design_Masters" => [["DES-001", "Ring", "Classic Band", "AU916", "Classic", "INDJ", "Casting"]],
        "Items_Tags" =>
        [
            ["TAG-0001", "SKU-001", "DES-001", "AU916", "DXB01", "In Stock", "12.5", "1.2", "0", "11.3", "916", "10.3508", "0", "4300", "items/TAG-0001.jpg"],
            ["TAG-0002", "SKU-002", "DES-001", "AU916", "DXB01", "On Approval", "5", "0", "0", "5", "916", "4.58", "0", "1500", ""],
        ],
        "Item_Stones" => [["TAG-0001", "1", "DIA-001", "1", "1.5", "0.3", "800", "GIA-001"]],
        "Assay_Results" => [["ASSAY-001", "Buyback", "BUY-001", "SAMP-001", "Fire assay", "916", "916", "Lab", "2026-10-15", "assays/ASSAY-001.pdf"]],
        "Buyback_Exchange" => [["BUY-001", "2026-10-15", "CUST-001", "DXB01", "Exchange", "11.3", "916", "10.3508", "300", "3105.24", "SALE-001", "Cash"]],
        "Inventory_Movements" => [["MOV-001", "2026-10-15", "TAG-0001", "DXB01", "DXB01", "Showroom", "12.5", "10.3508", "4300", "TAG-0001"]],
        "Jobwork_Making" => [["JOB-001", "VEND-001", "10", "9.16", "0", "9.5", "9.4", "0.6", "150", "Open"]],
        "Melting_Refining" => [["MELT-001", "46310", "BATCH-001", "100", "91.6", "92", "995", "91.54", "-0.06", "Manager"]],
        "Consignment" => [["CONS-001", "SUP-001", "In", "TAG-0002", "2026-10-15", "2026-11-15", "5", "4.58", "On Approval"]],
        "Gold_Ledger_Opening" =>
        [
            ["GLD-001", "Company", "INDJ-001", "AU916", "2026-10-15", "12.5", "10.3508", "4300"],
            ["GLD-002", "Customer", "CUST-001", "AU916", "2026-10-15", "5", "4.58", "0"],
        ],
        "AR_AP_Opening" => [["AR-001", "Customer", "CUST-001", "SALE-001", "2026-10-15", "2026-11-15", "5000"]],
        "GL_Opening" => [["1200", "INDJ-001", "DXB01", "2026-10-15", "4300", "0"]],
        "Expenses" => [["EXP-001", "2026-10-15", "DXB01", "Showroom", "SUP-001", "Utilities", "100", "5", "6100", ""]],
        "Documents" => [["DOC-001", "Item", "TAG-0001", "Product Photo", "TAG-0001.jpg", "items/TAG-0001.jpg", "", "1"]],
        "AML_KYC" => [["KYC-001", "Customer", "CUST-001", "2026-10-15", "Low", "EID-001", "Clear", "Manager"]],
        _ => [["SAMPLE-001"]],
    };

    private static string Identity(string sheet, IReadOnlyDictionary<string, string> row) => sheet switch
    {
        "Item_Stones" => Cell(row, "Tag_ID") + "|" + Cell(row, "Stone_Line_ID"),
        "GL_Opening" => Cell(row, "Account_Code") + "|" + Cell(row, "Company_ID") + "|" + Cell(row, "Branch_ID"),
        _ => Cell(row, Headers(sheet)[0]),
    };

    private static Sheet? Find(IReadOnlyList<Sheet> sheets, string name)
        => sheets.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

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
