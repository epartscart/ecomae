using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>epc_bulk_read_input_lines</c> twin: CSV / TSV / TXT / XLSX.
/// Old binary <c>.xls</c> is rejected with a clear save-as-xlsx message.
/// </summary>
public static class StorefrontBulkUploadFileParser
{
    public const int MaxFileBytes = 8 * 1024 * 1024;
    private static readonly Regex HeaderArticle = new("part|article|number|номер", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly byte[] OleMagic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static IReadOnlyList<StorefrontBulkUploadLine> Read(Stream stream, string fileName, out string? error)
    {
        error = null;
        ArgumentNullException.ThrowIfNull(stream);

        byte[] bytes;
        using (var copy = new MemoryStream())
        {
            stream.CopyTo(copy);
            bytes = copy.ToArray();
        }

        if (bytes.Length == 0)
        {
            error = "Upload file is required.";
            return [];
        }

        if (bytes.Length > MaxFileBytes)
        {
            error = "File is larger than 8 MB. Split the list or save as CSV.";
            return [];
        }

        if (LooksLikeOle(bytes))
        {
            error = "Old Excel .xls is not supported. Save the sheet as .xlsx or CSV and upload again.";
            return [];
        }

        var ext = Path.GetExtension(fileName ?? "").Trim().ToLowerInvariant();
        IReadOnlyList<IReadOnlyList<string>> raw;
        try
        {
            raw = ext == ".xlsx"
                ? ParseXlsx(bytes)
                : ParseDelimited(bytes);
        }
        catch (Exception ex)
        {
            error = "Could not read the file. Use Brand, Part Number, Qty columns in CSV or .xlsx. " + ex.Message;
            return [];
        }

        var items = new List<StorefrontBulkUploadLine>();
        foreach (var row in raw)
        {
            var brand = Cell(row, 0);
            var article = Cell(row, 1);
            if (article.Length == 0 || HeaderArticle.IsMatch(article))
            {
                continue;
            }

            var qtyRaw = Cell(row, 2);
            var qtyDigits = new string(qtyRaw.Where(char.IsDigit).ToArray());
            var qty = int.TryParse(qtyDigits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : 1;
            items.Add(new StorefrontBulkUploadLine(
                brand,
                article,
                qty,
                Cell(row, 3),
                Cell(row, 4),
                Cell(row, 5)));
            if (items.Count >= StorefrontBulkUploadMatcher.MaxRows)
            {
                break;
            }
        }

        if (items.Count == 0)
        {
            error = "No valid rows found. Use Brand, Part Number, Qty columns.";
        }

        return items;
    }

    public static string SampleCsv()
        => "Brand,Part Number,Qty,Target Price,Required Delivery,Comment\n"
           + "BOSCH,0986424795,2,45.00,1,Front brake pads\n"
           + "MANN,W71275,1,,,Oil filter\n"
           + "NGK,BKR6E,4,,,Spark plugs\n";

    private static bool LooksLikeOle(byte[] bytes)
    {
        if (bytes.Length < OleMagic.Length)
        {
            return false;
        }

        for (var i = 0; i < OleMagic.Length; i++)
        {
            if (bytes[i] != OleMagic[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string Cell(IReadOnlyList<string> row, int index)
        => index < row.Count ? (row[index] ?? string.Empty).Trim() : string.Empty;

    private static IReadOnlyList<IReadOnlyList<string>> ParseDelimited(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var lines = Regex.Split(text, "\r\n|\r|\n");
        var rows = new List<IReadOnlyList<string>>();
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var delimiter = DetectDelimiter(line);
            rows.Add(SplitCsv(line, delimiter));
        }

        return rows;
    }

    private static char DetectDelimiter(string line)
    {
        var commas = line.Count(c => c == ',');
        var semis = line.Count(c => c == ';');
        var tabs = line.Count(c => c == '\t');
        if (tabs > commas && tabs > semis)
        {
            return '\t';
        }

        return semis > commas ? ';' : ',';
    }

    private static List<string> SplitCsv(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseXlsx(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, leaveOpen: false);
        var shared = ReadSharedStrings(zip);
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")
            ?? zip.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        if (sheet is null)
        {
            return [];
        }

        using var sheetStream = sheet.Open();
        var xml = XDocument.Load(sheetStream);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var rowEl in xml.Descendants().Where(e => e.Name.LocalName == "row"))
        {
            var cells = new Dictionary<int, string>();
            foreach (var cell in rowEl.Elements().Where(e => e.Name.LocalName == "c"))
            {
                var r = (string?)cell.Attribute("r") ?? "";
                var col = ColumnIndex(r);
                var type = (string?)cell.Attribute("t") ?? "";
                var valueEl = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "v");
                var raw = valueEl?.Value ?? "";
                if (type == "s" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)
                    && idx >= 0 && idx < shared.Count)
                {
                    raw = shared[idx];
                }
                else if (type == "inlineStr")
                {
                    var t = cell.Descendants().FirstOrDefault(e => e.Name.LocalName == "t");
                    raw = t?.Value ?? raw;
                }

                cells[col] = raw;
            }

            if (cells.Count == 0)
            {
                continue;
            }

            var max = cells.Keys.Max();
            var line = new string[max + 1];
            for (var i = 0; i <= max; i++)
            {
                line[i] = cells.TryGetValue(i, out var v) ? v : "";
            }

            rows.Add(line);
        }

        return rows;
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var xml = XDocument.Load(stream);
        var shared = new List<string>();
        foreach (var si in xml.Descendants().Where(e => e.Name.LocalName == "si"))
        {
            var texts = si.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value);
            shared.Add(string.Concat(texts));
        }

        return shared;
    }

    private static int ColumnIndex(string cellRef)
    {
        var n = 0;
        foreach (var ch in cellRef)
        {
            if (ch < 'A' || ch > 'Z')
            {
                if (ch >= 'a' && ch <= 'z')
                {
                    n = n * 26 + (ch - 'a' + 1);
                    continue;
                }

                break;
            }

            n = n * 26 + (ch - 'A' + 1);
        }

        return Math.Max(0, n - 1);
    }
}
