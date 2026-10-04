using System.IO.Compression;
using System.Text;

namespace EcomAE.Platform.Tests;

/// <summary>Generated supplier price files (no binary fixtures in the repo).</summary>
internal static class PriceTestFiles
{
    public static void AddEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }

    /// <summary>Minimal SpreadsheetML workbook (inline strings) so the reader is exercised without binary fixtures.</summary>
    public static void WriteXlsx(string path, params string[][][] sheets)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var overrides = new StringBuilder();
        var sheetRefs = new StringBuilder();
        var rels = new StringBuilder();
        for (var s = 1; s <= sheets.Length; s++)
        {
            overrides.Append("<Override PartName=\"/xl/worksheets/sheet").Append(s)
                .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            sheetRefs.Append("<sheet name=\"Sheet").Append(s).Append("\" sheetId=\"").Append(s).Append("\" r:id=\"rId").Append(s).Append("\"/>");
            rels.Append("<Relationship Id=\"rId").Append(s)
                .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet").Append(s).Append(".xml\"/>");
        }

        AddEntry(archive, "[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
            + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
            + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
            + overrides + "</Types>");
        AddEntry(archive, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        AddEntry(archive, "xl/workbook.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" + sheetRefs + "</sheets></workbook>");
        AddEntry(archive, "xl/_rels/workbook.xml.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + rels + "</Relationships>");

        for (var s = 0; s < sheets.Length; s++)
        {
            var data = new StringBuilder();
            for (var r = 0; r < sheets[s].Length; r++)
            {
                data.Append("<row r=\"").Append(r + 1).Append("\">");
                for (var c = 0; c < sheets[s][r].Length; c++)
                {
                    var value = sheets[s][r][c];
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    var cellRef = (char)('A' + c) + (r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) && !value.StartsWith('0'))
                    {
                        data.Append("<c r=\"").Append(cellRef).Append("\"><v>").Append(value).Append("</v></c>");
                    }
                    else
                    {
                        data.Append("<c r=\"").Append(cellRef).Append("\" t=\"inlineStr\"><is><t>")
                            .Append(System.Security.SecurityElement.Escape(value)).Append("</t></is></c>");
                    }
                }

                data.Append("</row>");
            }

            AddEntry(archive, "xl/worksheets/sheet" + (s + 1) + ".xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>"
                + data + "</sheetData></worksheet>");
        }
    }
}
