using System.Globalization;
using System.IO.Compression;
using System.Text;
using SkiaSharp;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PDF and Word copies of the AML programme. Each download is stamped with the
/// preparation time and the reporting clocks counted from that moment.
/// </summary>
public static class ErpAmlDocumentPack
{
    public const int PageWidth = 1240;
    public const int PageHeight = 1754;

    public static string FileStem(string? code, DateTime utc)
    {
        var key = string.IsNullOrWhiteSpace(code) ? "full-pack" : code.Trim().ToLowerInvariant();
        var safe = new string(key.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        if (safe.Length == 0)
        {
            safe = "full-pack";
        }

        return "ecomae-aml-" + safe + "-" + utc.ToString("yyyyMMdd-HHmm'Z'", CultureInfo.InvariantCulture);
    }

    public static IReadOnlyList<ErpAmlDocument> Select(string? code)
    {
        var key = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length == 0 || key is "all" or "pack" or "full")
        {
            return ErpAmlProgramme.Documents;
        }

        var one = ErpAmlProgramme.Find(key);
        return one is null ? ErpAmlProgramme.Documents : [one];
    }

    public static byte[] Pdf(string? code, DateTime utc)
    {
        var pages = RenderPages(code, utc);
        return ToPdf(pages);
    }

    public static byte[] Docx(string? code, DateTime utc)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes());
            Write(zip, "_rels/.rels", RelsRoot());
            Write(zip, "word/_rels/document.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"/>");
            Write(zip, "word/document.xml", DocumentXml(code, utc));
        }

        return ms.ToArray();
    }

    private static List<byte[]> RenderPages(string? code, DateTime utc)
    {
        var blocks = Blocks(code, utc);
        var pages = new List<SKBitmap>();
        var page = NewPage();
        var canvas = new SKCanvas(page);
        canvas.Clear(SKColors.White);
        DrawChrome(canvas, 1);
        var y = 118f;
        foreach (var block in blocks)
        {
            using var font = Font(block.Size, block.Bold);
            var lines = Wrap(font, block.Text, PageWidth - 128);
            var lineHeight = block.Size + 8;
            foreach (var line in lines)
            {
                if (y + lineHeight > PageHeight - 78)
                {
                    canvas.Dispose();
                    pages.Add(page);
                    page = NewPage();
                    canvas = new SKCanvas(page);
                    canvas.Clear(SKColors.White);
                    DrawChrome(canvas, pages.Count + 1);
                    y = 118f;
                }

                using var paint = new SKPaint { Color = block.Color, IsAntialias = true };
                canvas.DrawText(line, 64, y, SKTextAlign.Left, font, paint);
                y += lineHeight;
            }

            y += block.Gap;
        }

        canvas.Dispose();
        pages.Add(page);
        var total = pages.Count;
        var encoded = new List<byte[]>(total);
        for (var i = 0; i < total; i++)
        {
            using var finished = new SKCanvas(pages[i]);
            DrawFooter(finished, utc, i + 1, total);
            encoded.Add(Encode(pages[i]));
        }

        return encoded;
    }

    private static List<Block> Blocks(string? code, DateTime utc)
    {
        var docs = Select(code);
        var clocks = ErpAmlProgramme.Clocks(utc);
        var blocks = new List<Block>
        {
            new("AML / CFT / PF programme", 28, true, new SKColor(29, 39, 64), 8),
            new(docs.Count == 1 ? docs[0].Title : "Full document pack", 22, true, new SKColor(31, 78, 121), 6),
            new("Ready " + clocks[0].When + "  ·  pack " + ErpAmlProgramme.PackVersion, 14, false, new SKColor(71, 85, 105), 10),
            new("This file is the workspace pack for every tenant where AML applies, including a dealer in precious metals and stones. It is prepared for the tenant file and for the Ministry of Economy and Tourism when the supervisor asks. The clocks below are counted from the preparation time.", 14, false, new SKColor(29, 39, 64), 12),
            new("Time frames", 18, true, new SKColor(29, 39, 64), 6),
        };
        foreach (var frame in ErpAmlProgramme.TimeFrames)
        {
            blocks.Add(new(frame.Duty + " — " + frame.Due + " (" + frame.Clock + ")", 13, false, new SKColor(29, 39, 64), 2));
        }

        blocks.Add(new("Dates counted from this download", 18, true, new SKColor(29, 39, 64), 6));
        foreach (var clock in clocks)
        {
            blocks.Add(new(clock.Label + ": " + clock.When, 13, false, new SKColor(29, 39, 64), 2));
        }

        foreach (var doc in docs)
        {
            blocks.Add(new(doc.Title, 20, true, new SKColor(31, 78, 121), 4));
            blocks.Add(new(doc.AppliesTo + ". " + doc.Lead, 13, true, new SKColor(29, 39, 64), 6));
            foreach (var section in doc.Sections)
            {
                blocks.Add(new(section.Heading, 15, true, new SKColor(29, 39, 64), 3));
                blocks.Add(new(section.Body, 13, false, new SKColor(51, 65, 85), 8));
            }
        }

        blocks.Add(new("Invoice countries in this pack", 18, true, new SKColor(29, 39, 64), 4));
        blocks.Add(new("Refused: " + string.Join(", ", ErpAmlProgramme.Countermeasures.Select(item => item.Code + " " + item.Name)) + ".", 13, false, new SKColor(29, 39, 64), 3));
        blocks.Add(new("Held: " + string.Join(", ", ErpAmlProgramme.CallForAction.Select(item => item.Code + " " + item.Name)) + ".", 13, false, new SKColor(29, 39, 64), 3));
        blocks.Add(new("Enhanced due diligence, not refused: " + string.Join(", ", ErpAmlProgramme.IncreasedMonitoring.Select(item => item.Code)) + ".", 13, false, new SKColor(29, 39, 64), 8));
        blocks.Add(new(ErpAmlProgramme.Decree + ". " + ErpAmlProgramme.Resolution + ". " + ErpAmlProgramme.FatfStatement + ".", 13, false, new SKColor(71, 85, 105), 4));
        return blocks;
    }

    private static string DocumentXml(string? code, DateTime utc)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
            <w:body>
            """);
        foreach (var block in Blocks(code, utc))
        {
            sb.Append("<w:p><w:r><w:rPr><w:color w:val=\"")
                .Append(block.Bold ? "1D2740" : "334155")
                .Append("\"/><w:sz w:val=\"")
                .Append((block.Size * 2).ToString(CultureInfo.InvariantCulture))
                .Append("\"/>");
            if (block.Bold)
            {
                sb.Append("<w:b/>");
            }

            sb.Append("</w:rPr><w:t xml:space=\"preserve\">")
                .Append(Xml(block.Text))
                .Append("</w:t></w:r></w:p>");
        }

        sb.Append("<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/></w:sectPr></w:body></w:document>");
        return sb.ToString();
    }

    private static void DrawChrome(SKCanvas canvas, int page)
    {
        using var navy = new SKPaint { Color = new SKColor(29, 39, 64), IsAntialias = true };
        using var gold = new SKPaint { Color = new SKColor(196, 163, 90), IsAntialias = true };
        canvas.DrawRect(new SKRect(0, 0, PageWidth, 86), navy);
        canvas.DrawRect(new SKRect(0, 86, PageWidth, 92), gold);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var font = Font(16, true);
        canvas.DrawText("AML programme  ·  ready for the tenant and the Ministry", 64, 52, SKTextAlign.Left, font, paint);
        _ = page;
    }

    private static void DrawFooter(SKCanvas canvas, DateTime utc, int page, int total)
    {
        using var navy = new SKPaint { Color = new SKColor(29, 39, 64), IsAntialias = true };
        canvas.DrawRect(new SKRect(0, PageHeight - 48, PageWidth, PageHeight), navy);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var font = Font(12, false);
        var stamp = utc.ToString("dd MMMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);
        canvas.DrawText("Pack " + ErpAmlProgramme.PackVersion + "  ·  " + stamp, 64, PageHeight - 18, SKTextAlign.Left, font, paint);
        canvas.DrawText(page.ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture), PageWidth - 110, PageHeight - 18, SKTextAlign.Left, font, paint);
    }

    private static List<string> Wrap(SKFont font, string text, float maxWidth)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var trial = current.Length == 0 ? word : current + " " + word;
            if (font.MeasureText(trial) <= maxWidth)
            {
                current = trial;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }

            current = word;
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        if (lines.Count == 0)
        {
            lines.Add("");
        }

        return lines;
    }

    private static SKFont Font(float size, bool bold)
        => new(Face.Value, size) { Embolden = bold, Edging = SKFontEdging.Antialias };

    private static SKBitmap NewPage()
        => new(new SKImageInfo(PageWidth, PageHeight, SKColorType.Rgba8888, SKAlphaType.Premul));

    private static byte[] Encode(SKBitmap bitmap)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 82))
        {
            return data.ToArray();
        }
    }

    private static byte[] ToPdf(IReadOnlyList<byte[]> jpegs)
    {
        var w = 595.0;
        var h = 842.0;
        var kids = Enumerable.Range(0, jpegs.Count).Select(i => 5 + (i * 3)).ToList();
        var catalog = "<< /Type /Catalog /Pages 2 0 R >>";
        var pages = "<< /Type /Pages /Count " + kids.Count.ToString(CultureInfo.InvariantCulture)
            + " /Kids [" + string.Join(" ", kids.Select(id => id.ToString(CultureInfo.InvariantCulture) + " 0 R")) + "] >>";
        using var ms = new MemoryStream();
        void WriteAscii(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            ms.Write(bytes, 0, bytes.Length);
        }

        var xref = new List<long> { 0 };
        void StartObj(int id)
        {
            xref.Add(ms.Length);
            WriteAscii(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
        }

        WriteAscii("%PDF-1.4\n");
        StartObj(1);
        WriteAscii(catalog + "\nendobj\n");
        StartObj(2);
        WriteAscii(pages + "\nendobj\n");
        var next = 3;
        foreach (var jpeg in jpegs)
        {
            var imgId = next++;
            var contentId = next++;
            var pageId = next++;
            var content = "q\n" + w.ToString("0.###", CultureInfo.InvariantCulture) + " 0 0 "
                + h.ToString("0.###", CultureInfo.InvariantCulture) + " 0 0 cm\n/Im"
                + imgId.ToString(CultureInfo.InvariantCulture) + " Do\nQ\n";
            StartObj(imgId);
            WriteAscii("<< /Type /XObject /Subtype /Image /Width " + PageWidth.ToString(CultureInfo.InvariantCulture)
                + " /Height " + PageHeight.ToString(CultureInfo.InvariantCulture)
                + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length "
                + jpeg.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
            ms.Write(jpeg, 0, jpeg.Length);
            WriteAscii("\nendstream\nendobj\n");
            StartObj(contentId);
            WriteAscii("<< /Length " + content.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n" + content + "endstream\nendobj\n");
            StartObj(pageId);
            WriteAscii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 "
                + w.ToString("0.###", CultureInfo.InvariantCulture) + " "
                + h.ToString("0.###", CultureInfo.InvariantCulture)
                + "] /Contents " + contentId.ToString(CultureInfo.InvariantCulture)
                + " 0 R /Resources << /XObject << /Im" + imgId.ToString(CultureInfo.InvariantCulture)
                + " " + imgId.ToString(CultureInfo.InvariantCulture) + " 0 R >> >> >>\nendobj\n");
        }

        var xrefPos = ms.Length;
        WriteAscii("xref\n0 " + xref.Count.ToString(CultureInfo.InvariantCulture) + "\n");
        WriteAscii("0000000000 65535 f \n");
        for (var i = 1; i < xref.Count; i++)
        {
            WriteAscii(xref[i].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        WriteAscii("trailer\n<< /Size " + xref.Count.ToString(CultureInfo.InvariantCulture)
            + " /Root 1 0 R >>\nstartxref\n" + xrefPos.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        return ms.ToArray();
    }

    private static readonly Lazy<SKTypeface> Face = new(() =>
        SKTypeface.FromFamilyName("DejaVu Sans")
        ?? SKTypeface.FromFamilyName("Liberation Sans")
        ?? SKTypeface.Default);

    private static void Write(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string ContentTypes()
        => """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
            </Types>
            """;

    private static string RelsRoot()
        => """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
            </Relationships>
            """;

    private static string Xml(string? value)
        => (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private sealed record Block(string Text, float Size, bool Bold, SKColor Color, float Gap);
}
