using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EcomAE.Platform.Erp;

/// <summary>
/// One workbook for every real-estate sheet. Files stay out of the database;
/// the sheet stores the storage key and the relationship codes.
/// </summary>
public static class ErpRealEstateWorkbook
{
    public const int MaxRows = 20000;

    public static readonly string[] TemplateSheets =
    [
        "Owners", "Properties", "Buildings", "Units", "Tenants", "Agreements",
        "Rent_Schedule", "PDC_Cheques", "Collections", "Expenses", "Maintenance",
        "Deposits", "Investments", "Documents",
    ];

    public sealed record Sheet(
        string Name,
        bool Known,
        bool Applies,
        string HoldReason,
        IReadOnlyList<string> Errors,
        IReadOnlyList<IReadOnlyDictionary<string, string>> Records);

    public static byte[] Template()
    {
        var sheets = TemplateSheets.Select(name => (Name: name, Headers: Headers(name), Rows: new[] { Sample(name) })).ToArray();
        return Xlsx(sheets);
    }

    public static byte[] ExportSnapshot(ErpRealEstateSnapshot snap)
    {
        (string Name, string[] Headers, string[][] Rows) SheetOf(string name, IReadOnlyList<ErpRealEstateRow> rows)
            => (name, new[] { "Id", "Code", "Name", "Status", "Amount", "Amount2", "Detail" },
                rows.Select(row => new[]
                {
                    row.Id.ToString(CultureInfo.InvariantCulture),
                    row.Code,
                    row.Name,
                    row.Status,
                    row.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                    row.Amount2.ToString("0.00", CultureInfo.InvariantCulture),
                    row.Detail,
                }).ToArray());

        return Xlsx(
        [
            SheetOf("Properties", snap.Properties),
            SheetOf("Units", snap.Units),
            SheetOf("Tenants", snap.Tenants),
            SheetOf("Agreements", snap.Leases),
            SheetOf("PDC_Cheques", snap.Cheques),
            SheetOf("Deposits", snap.Deposits),
            SheetOf("Maintenance", snap.Maintenance),
            SheetOf("Documents", snap.Documents),
        ]);
    }

    public static byte[] ExportPackage(ErpRealEstateSnapshot snap)
    {
        var book = ExportSnapshot(snap);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var bookEntry = zip.CreateEntry("RealEstate_Registers.xlsx");
            using (var stream = bookEntry.Open())
            {
                stream.Write(book, 0, book.Length);
            }

            var manifest = new StringBuilder();
            manifest.AppendLine("Id,Code,Title,Status,Detail,RelativePath");
            foreach (var doc in snap.Documents)
            {
                manifest.Append(doc.Id.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Csv(doc.Code)).Append(',')
                    .Append(Csv(doc.Name)).Append(',')
                    .Append(Csv(doc.Status)).Append(',')
                    .Append(Csv(doc.Detail)).Append(',')
                    .Append(Csv("documents/" + doc.Id.ToString(CultureInfo.InvariantCulture) + "/" + doc.Code))
                    .AppendLine();
            }

            var manifestEntry = zip.CreateEntry("document-manifest.csv");
            using (var writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(manifest.ToString());
            }

            var readme = zip.CreateEntry("README.txt");
            using (var writer = new StreamWriter(readme.Open(), new UTF8Encoding(false)))
            {
                writer.Write("Registers and the document manifest. File bytes stay in secure storage under the storage key. This package is not a full restoration archive.");
            }
        }

        return ms.ToArray();
    }

    public static string SheetToCsv(Stream stream, string sheet)
    {
        if (!stream.CanSeek)
        {
            var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            stream = copy;
        }

        var want = Canon(sheet);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var workbook = ReadXml(zip, "xl/workbook.xml");
        var rels = ReadXml(zip, "xl/_rels/workbook.xml.rels");
        if (workbook is null || rels is null)
        {
            return string.Empty;
        }

        XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        var targets = rels.Descendants(relNs + "Relationship")
            .ToDictionary(
                row => (string?)row.Attribute("Id") ?? "",
                row => "xl/" + ((string?)row.Attribute("Target") ?? "").TrimStart('/'),
                StringComparer.Ordinal);
        XNamespace ss = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relAttr = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        string? path = null;
        foreach (var node in workbook.Descendants(ss + "sheet"))
        {
            var name = (string?)node.Attribute("name") ?? "";
            if (want.Length > 0 && Canon(name) != want)
            {
                continue;
            }

            var id = (string?)node.Attribute(relAttr + "id") ?? "";
            if (targets.TryGetValue(id, out var target))
            {
                path = target;
            }

            break;
        }

        if (path is null)
        {
            return string.Empty;
        }

        var shared = SharedStrings(zip);
        var sheetXml = ReadXml(zip, path);
        if (sheetXml is null)
        {
            return string.Empty;
        }

        var csv = new StringBuilder();
        foreach (var row in sheetXml.Descendants(ss + "row"))
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements(ss + "c"))
            {
                var index = ColumnIndex((string?)cell.Attribute("r") ?? "");
                while (cells.Count < index)
                {
                    cells.Add("");
                }

                cells.Add(SheetCell(cell, ss, shared));
            }

            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            csv.AppendLine(string.Join(",", cells.Select(Csv)));
        }

        return csv.ToString();
    }

    private static XDocument? ReadXml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? zip.GetEntry(path.Replace('\\', '/'));
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static IReadOnlyList<string> SharedStrings(ZipArchive zip)
    {
        var xml = ReadXml(zip, "xl/sharedStrings.xml");
        if (xml is null)
        {
            return [];
        }

        XNamespace ss = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return xml.Descendants(ss + "si").Select(si => string.Concat(si.Descendants(ss + "t").Select(t => t.Value))).ToArray();
    }

    private static string SheetCell(XElement cell, XNamespace ss, IReadOnlyList<string> shared)
    {
        var kind = (string?)cell.Attribute("t") ?? "";
        if (kind == "inlineStr")
        {
            return cell.Descendants(ss + "t").FirstOrDefault()?.Value?.Trim() ?? "";
        }

        var raw = cell.Element(ss + "v")?.Value?.Trim() ?? "";
        if (kind == "s" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < shared.Count)
        {
            return shared[index].Trim();
        }

        return raw;
    }

    private static int ColumnIndex(string reference)
    {
        var n = 0;
        foreach (var ch in reference)
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            n = (n * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return Math.Max(0, n - 1);
    }

    public static Sheet Parse(string? sheet, string? csv)
    {
        var canon = Canon(sheet);
        if (canon.Length == 0)
        {
            return new Sheet(sheet ?? "", false, false, "", ["Unknown sheet. Download the real-estate workbook and use one of its tabs."], []);
        }

        var display = Display(canon);
        var applies = Applies(canon);
        var hold = applies
            ? ""
            : "This sheet is checked here. Rent schedules are generated from the agreement, and collections, deposits and investments are posted from their own screens so they are not imported twice.";
        var required = Required(canon);
        var lines = (csv ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var headerIndex = -1;
        IReadOnlyList<string> headers = [];
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            headers = Split(lines[i]).Select(NormalizeHeader).ToArray();
            headerIndex = i;
            break;
        }

        if (headerIndex < 0)
        {
            return new Sheet(display, true, applies, hold, ["The sheet has no header row."], []);
        }

        var errors = new List<string>();
        foreach (var column in required)
        {
            if (!headers.Contains(column, StringComparer.Ordinal))
            {
                errors.Add("Missing column " + column + ".");
            }
        }

        if (errors.Count > 0)
        {
            return new Sheet(display, true, applies, hold, errors, []);
        }

        var records = new List<IReadOnlyDictionary<string, string>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            count++;
            if (count > MaxRows)
            {
                errors.Add("Split the sheet above " + MaxRows.ToString(CultureInfo.InvariantCulture) + " rows.");
                break;
            }

            var cells = Split(lines[i]);
            if (cells.Count == 0 || cells[0].StartsWith('#'))
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count && c < cells.Count; c++)
            {
                row[headers[c]] = cells[c];
            }

            if (row.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var rowNo = (i + 1).ToString(CultureInfo.InvariantCulture);
            errors.AddRange(ValidateRow(canon, row, rowNo, seen));
            records.Add(row);
        }

        if (records.Count == 0 && errors.Count == 0)
        {
            errors.Add("The sheet has no data rows.");
        }

        return new Sheet(display, true, applies, hold, errors, records);
    }

    public static string Canon(string? sheet)
        => (sheet ?? string.Empty).Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant() switch
        {
            "owners" or "owner" or "landlords" or "landlord" => "owners",
            "properties" or "property" => "properties",
            "buildings" or "building" => "buildings",
            "units" or "unit" => "units",
            "tenants" or "tenant" => "tenants",
            "agreements" or "agreement" or "leases" or "lease" => "agreements",
            "rent_schedule" or "schedule" => "rent_schedule",
            "pdc_cheques" or "pdcs" or "cheques" or "cheque" => "pdc_cheques",
            "collections" or "collection" or "receipts" => "collections",
            "expenses" or "expense" => "expenses",
            "maintenance" => "maintenance",
            "deposits" or "deposit" => "deposits",
            "investments" or "investment" => "investments",
            "documents" or "document" => "documents",
            _ => "",
        };

    private static string Display(string canon)
        => canon switch
        {
            "owners" => "Owners",
            "properties" => "Properties",
            "buildings" => "Buildings",
            "units" => "Units",
            "tenants" => "Tenants",
            "agreements" => "Agreements",
            "rent_schedule" => "Rent_Schedule",
            "pdc_cheques" => "PDC_Cheques",
            "collections" => "Collections",
            "expenses" => "Expenses",
            "maintenance" => "Maintenance",
            "deposits" => "Deposits",
            "investments" => "Investments",
            "documents" => "Documents",
            _ => canon,
        };

    private static bool Applies(string canon)
        => canon is "owners" or "properties" or "buildings" or "units" or "tenants" or "agreements" or "pdc_cheques" or "expenses" or "maintenance" or "documents";

    private static string[] Headers(string display)
        => Canon(display) switch
        {
            "owners" => ["Code*", "Name*", "Owner"],
            "properties" or "buildings" => ["Code*", "Name*", "Category", "Emirate", "City", "Community", "PortfolioCode"],
            "units" => ["PropertyCode*", "UnitCode*", "Name", "Type", "Area", "Floor"],
            "tenants" => ["Code*", "Name*", "Contact", "Type", "EmiratesId", "Passport", "Email", "Nationality", "IdExpiry"],
            "agreements" => ["Code*", "PropertyCode*", "TenantCode*", "UnitCode", "Start*", "End*", "AnnualRent*", "Frequency*", "Deposit", "Ejari"],
            "rent_schedule" => ["AgreementCode*", "DueDate*", "Amount*"],
            "pdc_cheques" => ["ChequeNo*", "Bank*", "Amount*", "ChequeDate*", "PropertyCode", "TenantCode", "AgreementCode", "Status"],
            "collections" => ["AgreementCode*", "Date*", "Amount*", "Method"],
            "expenses" => ["Code*", "PropertyCode*", "Category*", "Amount*", "Vat", "Capital", "Supplier", "ExpenseDate"],
            "maintenance" => ["PropertyCode*", "Description*", "Category", "Amount", "Capital", "Vendor", "Scheduled"],
            "deposits" => ["AgreementCode*", "Amount*", "Status"],
            "investments" => ["PropertyCode*", "PurchasePrice*", "Costs", "MarketValue"],
            "documents" => ["Code*", "Title*", "Type*", "RelatedKind", "RelatedCode", "StorageKey", "FileName", "Expiry"],
            _ => ["Code*"],
        };

    private static string[] Sample(string display)
        => Canon(display) switch
        {
            "owners" => ["OWN-ABC", "ABC Properties LLC", "ABC Properties LLC"],
            "properties" or "buildings" => ["BLD-A", "Building A", "residential", "Dubai", "Business Bay", "Business Bay", "OWN-ABC"],
            "units" => ["BLD-A", "A-101", "Apartment 101", "apartment", "85", "1"],
            "tenants" => ["TEN-1", "Sara Ali", "+971500000000", "individual", "784-0000-0000000-0", "", "sara@example.com", "AE", "2028-01-01"],
            "agreements" => ["AGR-101", "BLD-A", "TEN-1", "A-101", "2027-01-01", "2027-12-31", "120000", "quarterly", "5000", ""],
            "rent_schedule" => ["AGR-101", "2027-01-01", "30000"],
            "pdc_cheques" => ["000456", "ENBD", "30000", "2027-01-01", "BLD-A", "TEN-1", "AGR-101", "received"],
            "collections" => ["AGR-101", "2027-01-01", "30000", "cheque"],
            "expenses" => ["EXP-1", "BLD-A", "repairs", "1500", "75", "no", "Cooling Co", "2027-02-01"],
            "maintenance" => ["BLD-A", "AC service", "repairs", "800", "no", "Cooling Co", "2027-02-01"],
            "deposits" => ["AGR-101", "5000", "held"],
            "investments" => ["BLD-A", "2000000", "50000", "2300000"],
            "documents" => ["DOC-456", "Cheque 000456", "cheque_front", "cheque", "000456", "cheques/000456/front.jpg", "front.jpg", ""],
            _ => [""],
        };

    private static string[] Required(string canon)
        => canon switch
        {
            "owners" or "properties" or "buildings" => ["code", "name"],
            "units" => ["propertycode", "unitcode"],
            "tenants" => ["code", "name"],
            "agreements" => ["code", "propertycode", "tenantcode", "start", "end", "annualrent", "frequency"],
            "rent_schedule" => ["agreementcode", "duedate", "amount"],
            "pdc_cheques" => ["chequeno", "bank", "amount", "chequedate"],
            "collections" => ["agreementcode", "date", "amount"],
            "expenses" => ["code", "propertycode", "category", "amount"],
            "maintenance" => ["propertycode", "description"],
            "deposits" => ["agreementcode", "amount"],
            "investments" => ["propertycode", "purchaseprice"],
            "documents" => ["code", "title", "type"],
            _ => ["code"],
        };

    private static IEnumerable<string> ValidateRow(
        string canon,
        IReadOnlyDictionary<string, string> row,
        string rowNo,
        HashSet<string> seen)
    {
        string Get(string key) => row.TryGetValue(key, out var value) ? value.Trim() : "";
        var prefix = "Row " + rowNo + ": ";
        switch (canon)
        {
            case "owners":
            case "properties":
            case "buildings":
            case "tenants":
            case "expenses":
            case "documents":
                if (Get("code").Length == 0)
                {
                    yield return prefix + "code is required.";
                }
                else if (!seen.Add(canon + "|" + Get("code")))
                {
                    yield return prefix + "duplicate code " + Get("code") + ".";
                }

                if ((canon is "owners" or "properties" or "buildings" or "tenants") && Get("name").Length == 0)
                {
                    yield return prefix + "name is required.";
                }

                if (canon == "expenses")
                {
                    if (Get("propertycode").Length == 0)
                    {
                        yield return prefix + "property code is required.";
                    }

                    if (!MoneyOk(Get("amount")))
                    {
                        yield return prefix + "amount must be a number greater than zero.";
                    }
                }

                if (canon == "documents")
                {
                    if (Get("title").Length == 0 || Get("type").Length == 0)
                    {
                        yield return prefix + "title and type are required.";
                    }

                    if (IsInlineFile(Get("storagekey")))
                    {
                        yield return prefix + "store the file outside the database and keep only the storage key.";
                    }
                }

                break;
            case "units":
                if (Get("propertycode").Length == 0 || Get("unitcode").Length == 0)
                {
                    yield return prefix + "property code and unit code are required.";
                }
                else if (!seen.Add("unit|" + Get("propertycode") + "|" + Get("unitcode")))
                {
                    yield return prefix + "duplicate unit " + Get("unitcode") + ".";
                }

                break;
            case "agreements":
                if (Get("code").Length == 0 || Get("propertycode").Length == 0 || Get("tenantcode").Length == 0)
                {
                    yield return prefix + "agreement, property and tenant codes are required.";
                }
                else if (!seen.Add("lease|" + Get("code")))
                {
                    yield return prefix + "duplicate agreement " + Get("code") + ".";
                }

                if (!DateOk(Get("start")) || !DateOk(Get("end")) || string.CompareOrdinal(Get("end"), Get("start")) < 0)
                {
                    yield return prefix + "start and end dates must be yyyy-MM-dd, and the end must not be before the start.";
                }

                if (!MoneyOk(Get("annualrent")))
                {
                    yield return prefix + "annual rent must be greater than zero.";
                }

                var frequency = Get("frequency").ToLowerInvariant();
                if (frequency is not ("weekly" or "monthly" or "quarterly" or "semi_annual" or "annual"))
                {
                    yield return prefix + "frequency must be weekly, monthly, quarterly, semi_annual or annual.";
                }

                break;
            case "pdc_cheques":
                if (Get("chequeno").Length == 0 || Get("bank").Length == 0)
                {
                    yield return prefix + "cheque number and bank are required.";
                }
                else if (!seen.Add("chq|" + Get("chequeno") + "|" + Get("bank")))
                {
                    yield return prefix + "duplicate cheque " + Get("chequeno") + ".";
                }

                if (!MoneyOk(Get("amount")))
                {
                    yield return prefix + "amount must be greater than zero.";
                }

                if (!DateOk(Get("chequedate")))
                {
                    yield return prefix + "cheque date must be yyyy-MM-dd.";
                }

                var status = Get("status").ToLowerInvariant();
                if (status is "cleared" or "deposited")
                {
                    yield return prefix + "a cheque import cannot mark cash. Receive it, then clear it after the bank confirms.";
                }

                break;
            case "rent_schedule":
            case "collections":
            case "deposits":
                if (Get("agreementcode").Length == 0)
                {
                    yield return prefix + "agreement code is required.";
                }

                var amountKey = canon == "rent_schedule" || canon == "deposits" ? "amount" : "amount";
                if (!MoneyOk(Get(amountKey)))
                {
                    yield return prefix + "amount must be greater than zero.";
                }

                break;
            case "investments":
                if (Get("propertycode").Length == 0 || !MoneyOk(Get("purchaseprice")))
                {
                    yield return prefix + "property code and purchase price are required.";
                }

                break;
            case "maintenance":
                if (Get("propertycode").Length == 0 || Get("description").Length == 0)
                {
                    yield return prefix + "property code and description are required.";
                }

                break;
        }
    }

    private static bool MoneyOk(string raw)
    {
        var text = raw.Replace(",", "").Trim();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0;
    }

    private static bool DateOk(string raw)
        => DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static bool IsInlineFile(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains("data:", StringComparison.OrdinalIgnoreCase)
            || text.Contains(";base64", StringComparison.OrdinalIgnoreCase);
    }

    public static decimal Money(string? raw)
    {
        var text = (raw ?? string.Empty).Replace(",", "").Trim();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0;
    }

    private static string NormalizeHeader(string header)
        => header.Trim().TrimEnd('*').Replace(" ", "").Replace("_", "").ToLowerInvariant();

    public static IReadOnlyList<string> Split(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (c == ',' && !quoted)
            {
                cells.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        cells.Add(sb.ToString().Trim());
        return cells;
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n'))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        return text;
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
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + overrides + "</Types>");
            WriteEntry(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            WriteEntry(archive, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>"
                + sheetRefs + "</sheets></workbook>");
            WriteEntry(archive, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + rels + "</Relationships>");

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

                        var cellRef = ((char)('A' + c)).ToString() + (r + 1).ToString(CultureInfo.InvariantCulture);
                        data.Append("<c r=\"").Append(cellRef).Append("\" t=\"inlineStr\"><is><t>")
                            .Append(Xml(value)).Append("</t></is></c>");
                    }

                    data.Append("</row>");
                }

                WriteEntry(archive, "xl/worksheets/sheet" + (s + 1) + ".xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>"
                    + data + "</sheetData></worksheet>");
            }
        }

        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string Xml(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
