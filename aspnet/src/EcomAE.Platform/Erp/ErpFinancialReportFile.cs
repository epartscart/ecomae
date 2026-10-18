using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>On-screen print, PDF, and Excel for a financial report.</summary>
public static class ErpFinancialReportFile
{
    public static string Html(ErpFinancialReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var body = new StringBuilder();
        body.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>")
            .Append(WebUtility.HtmlEncode(document.Title))
            .Append("</title><style>")
            .Append("body{margin:24px;color:#1d2740;font:14px/1.45 Helvetica,Arial,sans-serif;background:#fff}")
            .Append("h1{font-size:22px;margin:0 0 8px}p{max-width:760px}")
            .Append("table{border-collapse:collapse;width:100%;max-width:860px;margin:0 0 28px}")
            .Append("th,td{padding:6px 8px;border-bottom:1px solid #d5dde6;text-align:left;color:#1d2740}")
            .Append("th{background:#e8eef5}.num{text-align:right;font-variant-numeric:tabular-nums}")
            .Append("tr.section td{background:#e8eef5;font-weight:700}")
            .Append("tr.subtotal td{font-weight:700;border-top:2px solid #1f4e79}")
            .Append("tr.total td{font-weight:800;border-top:2px solid #1d2740;border-bottom:3px double #1d2740}")
            .Append("button{background:#1f4e79;color:#fff;border:0;border-radius:4px;padding:8px 14px}")
            .Append("@media print{.no-print{display:none}body{margin:0}}")
            .Append("</style></head><body><p class=\"no-print\"><button type=\"button\" onclick=\"window.print()\">Print</button></p><h1>")
            .Append(WebUtility.HtmlEncode(document.Title))
            .Append("</h1><p>")
            .Append(WebUtility.HtmlEncode(document.Intro))
            .Append("</p>");
        foreach (var sheet in document.Sheets)
        {
            body.Append("<h2>").Append(WebUtility.HtmlEncode(sheet.Name)).Append("</h2><table><thead><tr>");
            foreach (var header in sheet.Headers)
            {
                body.Append("<th>").Append(WebUtility.HtmlEncode(header)).Append("</th>");
            }

            body.Append("</tr></thead><tbody>");
            foreach (var row in sheet.Rows)
            {
                body.Append("<tr class=\"").Append(WebUtility.HtmlEncode(row.Kind)).Append("\"><td>")
                    .Append(WebUtility.HtmlEncode(row.Code)).Append("</td><td>")
                    .Append(WebUtility.HtmlEncode(row.Label)).Append("</td><td class=\"num\">")
                    .Append(Money(row.Accounting)).Append("</td><td class=\"num\">")
                    .Append(Money(row.Reporting)).Append("</td></tr>");
            }

            body.Append("</tbody></table>");
        }

        body.Append("</body></html>");
        return body.ToString();
    }

    public static byte[] Pdf(ErpFinancialReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var lines = new List<(string Text, bool Bold)>();
        lines.Add((document.Title, true));
        foreach (var part in Wrap(document.Intro, 90))
        {
            lines.Add((part, false));
        }

        lines.Add(("", false));
        foreach (var sheet in document.Sheets)
        {
            lines.Add((sheet.Name, true));
            lines.Add((string.Join(" | ", sheet.Headers), true));
            foreach (var row in sheet.Rows)
            {
                if (row.Kind == "note")
                {
                    foreach (var part in Wrap(row.Label, 90))
                    {
                        lines.Add((part, false));
                    }

                    continue;
                }

                var amount = row.Accounting is null ? "" : Money(row.Accounting);
                var reporting = row.Reporting is null ? "" : Money(row.Reporting);
                var label = row.Kind == "section" ? row.Label : (row.Code + "  " + row.Label).Trim();
                lines.Add((Fit(label, 58) + " " + Fit(amount, 14) + " " + reporting, row.Kind is "subtotal" or "total"));
            }

            lines.Add(("", false));
        }

        return BuildPdf(lines);
    }

    public static byte[] Xlsx(ErpFinancialReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var count = Math.Max(1, document.Sheets.Count);
            var overrides = new StringBuilder();
            var sheets = new StringBuilder();
            var rels = new StringBuilder();
            for (var i = 1; i <= count; i++)
            {
                overrides.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i)
                    .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                var name = i <= document.Sheets.Count ? document.Sheets[i - 1].Name : "Report";
                sheets.Append("<sheet name=\"").Append(Xml(Clip(name, 31))).Append("\" sheetId=\"").Append(i)
                    .Append("\" r:id=\"rId").Append(i).Append("\"/>");
                rels.Append("<Relationship Id=\"rId").Append(i)
                    .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
                    .Append(i).Append(".xml\"/>");
            }

            Write(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + overrides + "</Types>");
            Write(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Write(archive, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>"
                + sheets + "</sheets></workbook>");
            Write(archive, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + rels + "</Relationships>");
            for (var i = 0; i < count; i++)
            {
                var sheet = i < document.Sheets.Count ? document.Sheets[i] : document.Sheets[0];
                Write(archive, "xl/worksheets/sheet" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".xml", SheetXml(document, sheet, i == 0));
            }
        }

        return ms.ToArray();
    }

    public static ErpFinancialReportDocument Table(string title, string intro, string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var paddedHeaders = headers.Concat(Enumerable.Repeat(string.Empty, Math.Max(0, 4 - headers.Count))).Take(4).ToList();
        while (paddedHeaders.Count < 4)
        {
            paddedHeaders.Add(string.Empty);
        }

        var face = new List<ErpIfrs18Row>();
        foreach (var row in rows)
        {
            var cells = row.ToList();
            while (cells.Count < 4)
            {
                cells.Add(string.Empty);
            }

            face.Add(new ErpIfrs18Row("line", cells[0], cells.Count > 1 ? cells[1] : "", Parse(cells, 2), Parse(cells, 3)));
        }

        return new ErpFinancialReportDocument(title, intro, [new ErpFinancialReportSheet(sheetName, paddedHeaders, face)]);
    }

    private static decimal? Parse(IReadOnlyList<string> cells, int index)
    {
        if (index >= cells.Count)
        {
            return null;
        }

        return decimal.TryParse(cells[index], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    }

    private static string SheetXml(ErpFinancialReportDocument document, ErpFinancialReportSheet sheet, bool includeIntro)
    {
        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        var row = 1;
        if (includeIntro)
        {
            xml.Append(TextRow(row++, [document.Title, "", "", ""]));
            xml.Append(TextRow(row++, [document.Intro, "", "", ""]));
            row++;
        }

        xml.Append(TextRow(row++, sheet.Headers.Take(4).ToList()));
        foreach (var line in sheet.Rows)
        {
            xml.Append("<row r=\"").Append(row).Append("\">");
            xml.Append(Inline(row, 0, line.Code));
            xml.Append(Inline(row, 1, line.Label));
            xml.Append(line.Accounting is null ? Inline(row, 2, "") : Number(row, 2, line.Accounting.Value));
            xml.Append(line.Reporting is null ? Inline(row, 3, "") : Number(row, 3, line.Reporting.Value));
            xml.Append("</row>");
            row++;
        }

        xml.Append("</sheetData></worksheet>");
        return xml.ToString();
    }

    private static string TextRow(int row, IReadOnlyList<string> cells)
    {
        var xml = new StringBuilder();
        xml.Append("<row r=\"").Append(row).Append("\">");
        for (var i = 0; i < cells.Count && i < 4; i++)
        {
            xml.Append(Inline(row, i, cells[i]));
        }

        xml.Append("</row>");
        return xml.ToString();
    }

    private static string Inline(int row, int column, string? text)
        => "<c r=\"" + Cell(row, column) + "\" t=\"inlineStr\"><is><t>" + Xml(text) + "</t></is></c>";

    private static string Number(int row, int column, decimal value)
        => "<c r=\"" + Cell(row, column) + "\"><v>" + value.ToString("0.00", CultureInfo.InvariantCulture) + "</v></c>";

    private static string Cell(int row, int column)
        => ((char)('A' + column)).ToString() + row.ToString(CultureInfo.InvariantCulture);

    private static byte[] BuildPdf(IReadOnlyList<(string Text, bool Bold)> lines)
    {
        const int pageLines = 46;
        var pages = new List<IReadOnlyList<(string Text, bool Bold)>>();
        for (var i = 0; i < lines.Count; i += pageLines)
        {
            pages.Add(lines.Skip(i).Take(pageLines).ToList());
        }

        if (pages.Count == 0)
        {
            pages.Add([("Financial report", true)]);
        }

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "",
        };
        var kids = new StringBuilder();
        for (var p = 0; p < pages.Count; p++)
        {
            var pageObject = 3 + (p * 2);
            kids.Append(pageObject).Append(" 0 R ");
        }

        objects[1] = "<< /Type /Pages /Count " + pages.Count.ToString(CultureInfo.InvariantCulture) + " /Kids [" + kids + "] >>";
        for (var p = 0; p < pages.Count; p++)
        {
            var stream = PageStream(pages[p]);
            objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents " + (4 + (p * 2)).ToString(CultureInfo.InvariantCulture)
                + " 0 R /Resources << /Font << /F1 " + (3 + (pages.Count * 2)).ToString(CultureInfo.InvariantCulture)
                + " 0 R /F2 " + (4 + (pages.Count * 2)).ToString(CultureInfo.InvariantCulture) + " 0 R >> >> >>");
            objects.Add("<< /Length " + stream.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n" + stream + "\nendstream");
        }

        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
        var pdf = new StringBuilder();
        pdf.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append("xref\n0 ").Append(objects.Count + 1).Append("\n0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            pdf.Append(offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        pdf.Append("trailer << /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xref).Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string PageStream(IReadOnlyList<(string Text, bool Bold)> lines)
    {
        var stream = new StringBuilder();
        var y = 800;
        foreach (var line in lines)
        {
            stream.Append("BT /F").Append(line.Bold ? "2" : "1").Append(" 10 Tf 40 ").Append(y)
                .Append(" Td (").Append(PdfText(line.Text)).Append(") Tj ET\n");
            y -= 16;
        }

        return stream.ToString();
    }

    private static string PdfText(string? text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text ?? string.Empty)
        {
            if (ch is '(' or ')' or '\\')
            {
                sb.Append('\\');
            }

            sb.Append(ch is >= ' ' and <= '~' ? ch : ' ');
        }

        return sb.ToString();
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }

    private static string Fit(string text, int width)
    {
        var value = text ?? string.Empty;
        if (value.Length > width)
        {
            value = value[..width];
        }

        return value.PadRight(width);
    }

    private static string Money(decimal? value)
        => value is null ? "" : value.Value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Xml(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static void Write(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
