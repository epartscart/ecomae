using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using SkiaSharp;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Board pack for the fit-out workspace reports tab: the same snapshot rendered
/// as HTML (in-page), PDF (downloaded) and DOCX (downloaded). Mirrors
/// <see cref="ErpRealEstateReportPack"/>.
/// </summary>
public static class ErpFitOutReportPack
{
    public static string FileStem(string projectKey, DateTime utc)
        => "ecomae-fitout-board-pack-"
           + ClipKey(projectKey)
           + "-"
           + utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static byte[] Pdf(ErpFitOutWorkspaceSnapshot snap, string title, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var pages = RenderPages(snap, title, utc.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture));
        return ErpRealEstateReportVisual.ToPdf(pages);
    }

    public static byte[] Docx(ErpFitOutWorkspaceSnapshot snap, string title, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var asOf = utc.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture);
        var cover = ErpRealEstateReportVisual.ConstructionJpeg();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes());
            Write(zip, "_rels/.rels", RelsRoot());
            Write(zip, "word/_rels/document.xml.rels", RelsDoc());
            Write(zip, "word/document.xml", DocumentXml(snap, title, asOf));
            WriteBytes(zip, "word/media/construction.jpg", cover);
        }

        return ms.ToArray();
    }

    public static string Html(ErpFitOutWorkspaceSnapshot snap, string title, string pdfHref, string docxHref, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var asOf = utc.ToString("dd MMMM yyyy 'UTC'", CultureInfo.InvariantCulture);
        var sb = new StringBuilder(8192);
        sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>")
            .Append(E(title)).Append("</title><style>")
            .Append("body{font-family:Segoe UI,Arial,sans-serif;color:#1e3a5f;margin:28px;background:#f6f9fc;}")
            .Append("h1{font-size:22px;margin:0 0 4px} h2{font-size:15px;margin:22px 0 8px;color:#0f766e;text-transform:uppercase;letter-spacing:.05em}")
            .Append(".muted{color:#5c708a;font-size:12px} .dl{margin:8px 0 16px} .dl a{color:#0f766e;font-weight:700;margin-right:14px}")
            .Append(".kpis{display:flex;flex-wrap:wrap;gap:10px;margin:12px 0}")
            .Append(".kpi{background:#fff;border:1px solid #d5e3f2;border-radius:10px;padding:10px 14px;min-width:120px}")
            .Append(".kpi b{display:block;font-size:18px} .kpi span{font-size:11px;color:#5c708a;text-transform:uppercase;letter-spacing:.05em}")
            .Append("table{border-collapse:collapse;width:100%;background:#fff;font-size:12px}")
            .Append("th,td{border:1px solid #d5e3f2;padding:5px 8px;text-align:left} th{background:#eef4fb;color:#0f766e}")
            .Append("td.num,th.num{text-align:right;font-variant-numeric:tabular-nums}")
            .Append("@media print{.dl{display:none}}")
            .Append("</style></head><body>");
        sb.Append("<h1>").Append(E(title)).Append("</h1>")
            .Append("<p class='muted'>Fit-out workspace board pack · as of ").Append(E(asOf)).Append("</p>")
            .Append("<p class='dl'><a href='").Append(E(docxHref)).Append("'>Download DOCX</a>")
            .Append("<a href='").Append(E(pdfHref)).Append("'>Download PDF</a></p>");

        var k = snap.Kpis;
        sb.Append("<div class='kpis'>");
        Kpi(sb, "Projects", k.Projects.ToString(CultureInfo.InvariantCulture));
        Kpi(sb, "Contract value", Money(k.ContractValue));
        Kpi(sb, "Committed", Money(k.Committed));
        Kpi(sb, "Actual cost", Money(k.Actual));
        Kpi(sb, "Certified", Money(k.Certified));
        Kpi(sb, "WIP", Money(k.Wip));
        Kpi(sb, "P&L", Money(k.ProfitLoss));
        Kpi(sb, "Pending approvals", k.PendingApprovals.ToString(CultureInfo.InvariantCulture));
        sb.Append("</div>");

        ProjectTable(sb, snap.Projects);
        Table(sb, "Cost ledger", snap.CostLedger, "Amount");
        Table(sb, "Committed / procurement", snap.ProcurementLinks, "Committed");
        Table(sb, "Material issues & returns", snap.MaterialMovements, "Amount");
        Table(sb, "Claims history", snap.Claims, "Amount");
        Table(sb, "POC recognition", snap.Recognitions, "%");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void ProjectTable(StringBuilder sb, IReadOnlyList<ErpFitOutProject> rows)
    {
        sb.Append("<h2>Project P&amp;L</h2><table><tr><th>Code</th><th>Project</th><th class='num'>Contract</th><th class='num'>Budget</th><th>Status</th></tr>");
        foreach (var r in rows)
        {
            sb.Append("<tr><td>").Append(E(r.Code)).Append("</td><td>").Append(E(r.Name))
                .Append("</td><td class='num'>").Append(E(Money(r.ContractValue)))
                .Append("</td><td class='num'>").Append(E(Money(r.BudgetCost)))
                .Append("</td><td>").Append(E(r.Status)).Append("</td></tr>");
        }

        sb.Append("</table>");
    }

    private static void Kpi(StringBuilder sb, string label, string value)
        => sb.Append("<div class='kpi'><b>").Append(E(value)).Append("</b><span>").Append(E(label)).Append("</span></div>");

    private static void Table(StringBuilder sb, string title, IReadOnlyList<ErpFitOutRow> rows, string amountLabel)
    {
        sb.Append("<h2>").Append(E(title)).Append("</h2><table><tr><th>Type</th><th>Ref</th><th>Name</th><th>Status</th><th class='num'>")
            .Append(E(amountLabel)).Append("</th><th class='num'>Qty/%</th><th>Detail</th></tr>");
        foreach (var r in rows.Take(60))
        {
            sb.Append("<tr><td>").Append(E(r.Kind)).Append("</td><td>").Append(E(r.Code))
                .Append("</td><td>").Append(E(r.Name)).Append("</td><td>").Append(E(r.Status))
                .Append("</td><td class='num'>").Append(E(amountLabel == "%" ? r.Amount.ToString("0.0", CultureInfo.InvariantCulture) : Money(r.Amount)))
                .Append("</td><td class='num'>").Append(E(r.Amount2.ToString("0.##", CultureInfo.InvariantCulture)))
                .Append("</td><td>").Append(E(r.Detail)).Append("</td></tr>");
        }

        sb.Append("</table>");
    }

    private static string DocumentXml(ErpFitOutWorkspaceSnapshot snap, string title, string asOf)
    {
        var sb = new StringBuilder(8192);
        sb.Append("<?xml version='1.0' encoding='UTF-8' standalone='yes'?>")
            .Append("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main' ")
            .Append("xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships' ")
            .Append("xmlns:wp='http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing' ")
            .Append("xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main' ")
            .Append("xmlns:pic='http://schemas.openxmlformats.org/drawingml/2006/picture'><w:body>");
        P(sb, title, true, "0F766E", 44);
        P(sb, "Fit-out workspace board pack · as of " + asOf, false, "5C708A", 20);
        Picture(sb, "rId1", 1, 5486400, 1645920);
        var k = snap.Kpis;
        P(sb, "Contract value " + Money(k.ContractValue)
              + " · Committed " + Money(k.Committed)
              + " · Actual " + Money(k.Actual)
              + " · Certified " + Money(k.Certified)
              + " · WIP " + Money(k.Wip)
              + " · P&L " + Money(k.ProfitLoss), false, "1E3A5F", 22);
        WordProjectTable(sb, snap.Projects);
        WordTable(sb, "Cost ledger", snap.CostLedger);
        WordTable(sb, "Committed / procurement", snap.ProcurementLinks);
        WordTable(sb, "Material issues & returns", snap.MaterialMovements);
        WordTable(sb, "Claims history", snap.Claims);
        sb.Append("</w:body></w:document>");
        return sb.ToString();
    }

    private static void WordProjectTable(StringBuilder sb, IReadOnlyList<ErpFitOutProject> rows)
    {
        P(sb, "Project P&L", true, "0F766E", 28);
        Row(sb, ["Code", "Project", "Contract", "Budget", "Status"], true);
        foreach (var r in rows)
        {
            Row(sb, [r.Code, r.Name, Money(r.ContractValue), Money(r.BudgetCost), r.Status], false);
        }
    }

    private static void WordTable(StringBuilder sb, string title, IReadOnlyList<ErpFitOutRow> rows)
    {
        P(sb, title, true, "0F766E", 28);
        Row(sb, ["Type", "Ref", "Name", "Status", "Amount"], true);
        foreach (var r in rows.Take(40))
        {
            Row(sb, [r.Kind, r.Code, r.Name, r.Status, Money(r.Amount)], false);
        }
    }

    private static void Row(StringBuilder sb, IReadOnlyList<string> cells, bool header)
    {
        sb.Append("<w:tr>");
        foreach (var cell in cells)
        {
            sb.Append("<w:tc><w:p><w:r>");
            if (header)
            {
                sb.Append("<w:rPr><w:b/></w:rPr>");
            }

            sb.Append("<w:t xml:space='preserve'>").Append(Xml(cell)).Append("</w:t></w:r></w:p></w:tc>");
        }

        sb.Append("</w:tr>");
    }

    private static void P(StringBuilder sb, string text, bool bold, string color, int halfPoints)
        => sb.Append("<w:p><w:r><w:rPr>")
            .Append(bold ? "<w:b/>" : "")
            .Append("<w:color w:val='").Append(color).Append("'/>")
            .Append("<w:sz w:val='").Append(halfPoints.ToString(CultureInfo.InvariantCulture)).Append("'/>")
            .Append("</w:rPr><w:t xml:space='preserve'>").Append(Xml(text)).Append("</w:t></w:r></w:p>");

    private static void Picture(StringBuilder sb, string relId, int docPrId, int cx, int cy)
        => sb.Append("<w:p><w:r><w:drawing><wp:inline><wp:extent cx='").Append(cx).Append("' cy='").Append(cy)
            .Append("'/><wp:docPr id='").Append(docPrId).Append("' name='p'/><a:graphic><a:graphicData uri='http://schemas.openxmlformats.org/drawingml/2006/picture'>")
            .Append("<pic:pic><pic:nvPicPr><pic:cNvPr id='").Append(docPrId).Append("' name='p'/><pic:cNvPicPr/></pic:nvPicPr>")
            .Append("<pic:blipFill><a:blip r:embed='").Append(relId).Append("'/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>")
            .Append("<pic:spPr><a:xfrm><a:off x='0' y='0'/><a:ext cx='").Append(cx).Append("' cy='").Append(cy).Append("'/></a:xfrm>")
            .Append("<a:prstGeom prst='rect'><a:avLst/></a:prstGeom></pic:spPr></pic:pic>")
            .Append("</a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>");

    private static IReadOnlyList<byte[]> RenderPages(ErpFitOutWorkspaceSnapshot snap, string title, string asOf)
    {
        var pages = new List<byte[]>();
        using (var bmp = new SKBitmap(ErpRealEstateReportVisual.PageWidth, ErpRealEstateReportVisual.PageHeight))
        {
            using var c = new SKCanvas(bmp);
            c.Clear(new SKColor(246, 249, 252));
            Text(c, "FIT-OUT WORKSPACE", 70, 100, 26, new SKColor(15, 118, 110), true);
            Text(c, title, 70, 160, 44, new SKColor(30, 58, 95), true);
            Text(c, "Board pack · as of " + asOf, 70, 200, 18, new SKColor(92, 112, 138), false);
            var k = snap.Kpis;
            var y = 280f;
            var metrics = new (string Label, string Value)[]
            {
                ("Contract value", Money(k.ContractValue)),
                ("Committed", Money(k.Committed)),
                ("Actual cost", Money(k.Actual)),
                ("Certified", Money(k.Certified)),
                ("WIP", Money(k.Wip)),
                ("P&L", Money(k.ProfitLoss)),
                ("Pending approvals", k.PendingApprovals.ToString(CultureInfo.InvariantCulture)),
            };
            var x = 70f;
            foreach (var (label, value) in metrics)
            {
                c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + 300, y + 110), 14), new SKPaint { Color = SKColors.White, IsAntialias = true });
                Text(c, value, x + 16, y + 52, 22, new SKColor(30, 58, 95), true);
                Text(c, label.ToUpperInvariant(), x + 16, y + 84, 12, new SKColor(92, 112, 138), false);
                x += 320;
                if (x > ErpRealEstateReportVisual.PageWidth - 300)
                {
                    x = 70;
                    y += 130;
                }
            }

            y += 40;
            Text(c, "PROJECT P&L", 70, y, 18, new SKColor(15, 118, 110), true);
            y += 30;
            foreach (var p in snap.Projects.Take(12))
            {
                Text(c, p.Code + "  " + p.Name, 70, y, 15, new SKColor(30, 58, 95), false);
                Text(c, Money(p.ContractValue) + "  /  " + Money(p.BudgetCost), 820, y, 15, new SKColor(15, 118, 110), true);
                y += 34;
            }

            Text(c, "Generated by EcomAE ERP", 70, ErpRealEstateReportVisual.PageHeight - 60, 12, new SKColor(92, 112, 138), false);
            pages.Add(Encode(bmp));
        }

        using (var bmp2 = new SKBitmap(ErpRealEstateReportVisual.PageWidth, ErpRealEstateReportVisual.PageHeight))
        {
            using var c = new SKCanvas(bmp2);
            c.Clear(new SKColor(246, 249, 252));
            Text(c, "COST LEDGER", 70, 100, 26, new SKColor(15, 118, 110), true);
            var y = 150f;
            foreach (var r in snap.CostLedger.Take(34))
            {
                Text(c, r.Kind + "  " + r.Name, 70, y, 13, new SKColor(30, 58, 95), false);
                Text(c, Money(r.Amount), 1000, y, 13, new SKColor(30, 58, 95), true);
                y += 34;
            }

            Text(c, "Generated by EcomAE ERP", 70, ErpRealEstateReportVisual.PageHeight - 60, 12, new SKColor(92, 112, 138), false);
            pages.Add(Encode(bmp2));
        }

        return pages;
    }

    private static void Text(SKCanvas c, string text, float x, float y, float size, SKColor color, bool bold)
    {
        using var font = new SKFont { Size = size, Typeface = bold ? SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold) : SKTypeface.FromFamilyName("DejaVu Sans") };
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        c.DrawText(text ?? "", x, y, SKTextAlign.Left, font, paint);
    }

    private static byte[] Encode(SKBitmap bmp)
    {
        using var data = bmp.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data.ToArray();
    }

    private static string ContentTypes()
        => """
           <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
           <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
             <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
             <Default Extension="xml" ContentType="application/xml"/>
             <Default Extension="jpg" ContentType="image/jpeg"/>
             <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
           </Types>
           """;

    private static string RelsRoot()
        => """
           <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
           <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
             <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
           </Relationships>
           """;

    private static string RelsDoc()
        => """
           <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
           <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
             <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/construction.jpg"/>
           </Relationships>
           """;

    private static void Write(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }

    private static void WriteBytes(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string Money(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture) + " AED";

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Xml(string? value)
        => (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string ClipKey(string? value)
    {
        var text = (value ?? "all").Trim().ToLowerInvariant();
        var cleaned = new string(text.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }

        return cleaned.Trim('-').Length == 0 ? "all" : cleaned.Trim('-');
    }
}
