using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Downloadable investment / construction board pack: visual HTML presentation,
/// colour PDF, and Word. Available from every tab of the real-estate workspace.
/// </summary>
public static class ErpRealEstateReportPack
{
    public static string FileStem(string siteKey, DateTime utc)
        => "ecomae-real-estate-board-pack-"
           + ClipKey(siteKey)
           + "-"
           + utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static byte[] Pdf(ErpRealEstateSnapshot snap, string title, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var pages = ErpRealEstateReportVisual.RenderPages(snap, title, utc.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture));
        return ErpRealEstateReportVisual.ToPdf(pages);
    }

    public static byte[] Docx(ErpRealEstateSnapshot snap, string title, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var asOf = utc.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture);
        var cover = ErpRealEstateReportVisual.SkylineJpeg();
        var build = ErpRealEstateReportVisual.ConstructionJpeg();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes());
            Write(zip, "_rels/.rels", RelsRoot());
            Write(zip, "word/_rels/document.xml.rels", RelsDoc());
            Write(zip, "word/document.xml", DocumentXml(snap, title, asOf));
            WriteBytes(zip, "word/media/skyline.jpg", cover);
            WriteBytes(zip, "word/media/construction.jpg", build);
        }

        return ms.ToArray();
    }

    public static string Html(ErpRealEstateSnapshot snap, string title, string pdfHref, string docxHref, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var asOf = utc.ToString("dd MMMM yyyy 'UTC'", CultureInfo.InvariantCulture);
        var sky = Convert.ToBase64String(ErpRealEstateReportVisual.SkylineJpeg());
        var build = Convert.ToBase64String(ErpRealEstateReportVisual.ConstructionJpeg());
        var lobby = Convert.ToBase64String(ErpRealEstateReportVisual.LobbyJpeg());
        var k = snap.Kpis;
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.Append("<title>").Append(E(title)).Append(" · board pack</title>");
        sb.Append("<style>");
        sb.Append("body{margin:0;background:#0b1f3a;color:#f6f1e7;font:16px/1.5 'Georgia','Times New Roman',serif}");
        sb.Append("a.btn{display:inline-block;background:#c4a35a;color:#0b1f3a;text-decoration:none;padding:10px 16px;border-radius:4px;font:600 14px/1 system-ui,sans-serif;margin:0 8px 8px 0}");
        sb.Append(".dock{position:sticky;top:0;z-index:20;background:linear-gradient(90deg,#0b1f3a,#163a6b);padding:14px 28px;border-bottom:3px solid #c4a35a;display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:12px}");
        sb.Append(".hero{position:relative;min-height:360px;background:#0b1f3a}");
        sb.Append(".hero img{width:100%;height:420px;object-fit:cover;display:block}");
        sb.Append(".hero .cap{position:absolute;left:40px;bottom:36px;right:40px}");
        sb.Append("h1{font-size:42px;margin:0 0 8px;letter-spacing:.02em} h1 span{color:#c4a35a}");
        sb.Append(".wrap{max-width:1100px;margin:0 auto;padding:28px 24px 64px}");
        sb.Append(".kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px;margin:24px 0}");
        sb.Append(".kpi{background:#12263f;border:1px solid #c4a35a;border-radius:8px;padding:14px 16px}");
        sb.Append(".kpi b{display:block;font:600 13px/1 system-ui,sans-serif;color:#c4a35a;text-transform:uppercase;letter-spacing:.08em}");
        sb.Append(".kpi span{display:block;font:700 26px/1.2 system-ui,sans-serif;margin-top:8px}");
        sb.Append("h2{color:#c4a35a;font-size:22px;margin:36px 0 12px;border-bottom:1px solid #c4a35a;padding-bottom:8px}");
        sb.Append("table{width:100%;border-collapse:collapse;background:#12263f;font:14px/1.4 system-ui,sans-serif}");
        sb.Append("th{text-align:left;background:#163a6b;color:#c4a35a;padding:8px 10px} td{padding:8px 10px;border-top:1px solid #1e3a5f}");
        sb.Append(".photo{width:100%;height:220px;object-fit:cover;border-radius:8px;margin:12px 0;border:1px solid #c4a35a}");
        sb.Append(".note{color:#cbd5e1;font:15px/1.55 Georgia,serif}");
        sb.Append(".bars{display:grid;gap:8px;margin:16px 0}");
        sb.Append(".bar{display:grid;grid-template-columns:180px 1fr 110px;gap:10px;align-items:center;font:14px system-ui,sans-serif}");
        sb.Append(".bar i{display:block;height:14px;background:linear-gradient(90deg,#c4a35a,#163a6b);border-radius:4px}");
        sb.Append("@media print{.dock a{display:none} body{background:#fff;color:#0b1f3a} table,.kpi{background:#fff;color:#0b1f3a}}");
        sb.Append("</style></head><body>");
        sb.Append("<div class=\"dock\"><div><strong>Board pack</strong> · ").Append(E(title)).Append(" · ").Append(E(asOf)).Append("</div><div>");
        sb.Append("<a class=\"btn\" href=\"").Append(E(pdfHref)).Append("\">Download PDF</a>");
        sb.Append("<a class=\"btn\" href=\"").Append(E(docxHref)).Append("\">Download Word</a>");
        sb.Append("</div></div>");
        sb.Append("<div class=\"hero\"><img alt=\"Dusk skyline\" src=\"data:image/jpeg;base64,").Append(sky).Append("\"/>");
        sb.Append("<div class=\"cap\"><div style=\"color:#c4a35a;letter-spacing:.16em;font:600 13px system-ui,sans-serif\">ECOMAE · REAL ESTATE</div>");
        sb.Append("<h1>Investment, development<br/><span>&amp; construction</span></h1>");
        sb.Append("<p class=\"note\">Live tenant figures · IAS 40 / IFRS 15 / IFRS 18 · AED</p></div></div>");
        sb.Append("<div class=\"wrap\">");
        sb.Append("<h2>Executive summary</h2>");
        sb.Append("<p class=\"note\">Pipeline opportunities stay off both statements. Owned properties, escrow cash and contractor certificates sit on the balance sheet. Rent, IAS 40 fair-value movements (through P&amp;L, not OCI) and recognised IFRS 15 unit sales sit on the income statement. Off-plan collections are a contract liability until sold. Bounce after clearance keeps the prior cash journal intact.</p>");
        sb.Append("<div class=\"kpis\">");
        Kpi(sb, "Owned properties", k.OwnedProperties.ToString(CultureInfo.InvariantCulture));
        Kpi(sb, "Fair value", Money(k.PortfolioFair));
        Kpi(sb, "Portfolio cost", Money(k.PortfolioCost));
        Kpi(sb, "Unrealized", Money(k.Unrealized));
        Kpi(sb, "Gross yield", Pct(k.GrossYield));
        Kpi(sb, "Occupancy", Pct(k.Occupancy));
        Kpi(sb, "Escrow held", Money(k.EscrowBalance));
        Kpi(sb, "Cost to complete", Money(k.CostToComplete));
        sb.Append("</div>");
        var max = new[] { k.PortfolioCost, k.PortfolioFair, Math.Abs(k.Unrealized), k.AnnualRent, k.EscrowBalance, k.CostToComplete }.Max();
        if (max <= 0)
        {
            max = 1;
        }

        sb.Append("<div class=\"bars\">");
        Bar(sb, "Portfolio cost", k.PortfolioCost, max);
        Bar(sb, "Fair value", k.PortfolioFair, max);
        Bar(sb, "Unrealized", Math.Abs(k.Unrealized), max);
        Bar(sb, "Annual rent", k.AnnualRent, max);
        Bar(sb, "Escrow held", k.EscrowBalance, max);
        Bar(sb, "Cost to complete", k.CostToComplete, max);
        sb.Append("</div>");
        sb.Append("<img class=\"photo\" alt=\"Lobby\" src=\"data:image/jpeg;base64,").Append(lobby).Append("\"/>");
        Table(sb, "Property register", snap.Properties, "Cost / value");
        Table(sb, "Valuation history", snap.Valuations, "Approved fair value");
        Table(sb, "Acquisitions", snap.Acquisitions, "Purchase");
        Table(sb, "Disposals", snap.Disposals, "Sale");
        sb.Append("<img class=\"photo\" alt=\"Construction\" src=\"data:image/jpeg;base64,").Append(build).Append("\"/>");
        Table(sb, "Development projects", snap.Projects, "Budget");
        Table(sb, "Escrow movements", snap.Escrow, "Amount");
        Table(sb, "BOQ / tender lines", snap.BoqLines, "Amount");
        Table(sb, "Contracts", snap.Contracts, "Original");
        Table(sb, "Certificates", snap.Certificates, "Net payable");
        Table(sb, "Unit sales", snap.Sales, "Sale price");
        Table(sb, "Off-plan instalments", snap.Instalments, "Amount");
        Table(sb, "Shared-cost allocations", snap.Allocations, "Allocated");
        Table(sb, "Cheque maturity", snap.Cheques, "Amount");
        Table(sb, "Deposits", snap.Deposits, "Held");
        sb.Append("<h2>IFRS statement map</h2><p class=\"note\"><strong>Balance sheet:</strong> 1510 investment property, 1520 CIP, 1140 restricted escrow cash, 1120 cheques under collection, 1130 rent receivable, 2200 tenant deposits, 2210 contractor payable, 2220 off-plan buyer advances.</p>");
        sb.Append("<p class=\"note\"><strong>Income statement:</strong> 4100 rental income, 4110 development revenue when sold, 4200 / 6200 fair-value through P&amp;L, 6210 property opex, 4300 / 6220 disposal.</p>");
        sb.Append("<p class=\"note\">Download this pack as PDF or Word from any tab of the real-estate workspace. Figures refresh from the live register each time.</p>");
        sb.Append("<p><a class=\"btn\" href=\"").Append(E(pdfHref)).Append("\">Download PDF</a><a class=\"btn\" href=\"").Append(E(docxHref)).Append("\">Download Word</a></p>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static void Kpi(StringBuilder sb, string label, string value)
        => sb.Append("<div class=\"kpi\"><b>").Append(E(label)).Append("</b><span>").Append(E(value)).Append("</span></div>");

    private static void Bar(StringBuilder sb, string label, decimal value, decimal max)
    {
        var pct = Math.Max(2, (int)Math.Round((double)(value / max) * 100));
        sb.Append("<div class=\"bar\"><span>").Append(E(label)).Append("</span><i style=\"width:")
            .Append(pct.ToString(CultureInfo.InvariantCulture)).Append("%\"></i><span>")
            .Append(E(Money(value))).Append("</span></div>");
    }

    private static void Table(StringBuilder sb, string title, IReadOnlyList<ErpRealEstateRow> rows, string amountLabel)
    {
        sb.Append("<h2>").Append(E(title)).Append("</h2>");
        if (rows.Count == 0)
        {
            sb.Append("<p class=\"note\">No rows on file yet.</p>");
            return;
        }

        sb.Append("<table><thead><tr><th>Id</th><th>Code</th><th>Name</th><th>Status</th><th>")
            .Append(E(amountLabel)).Append("</th><th>Detail</th></tr></thead><tbody>");
        foreach (var row in rows.Take(40))
        {
            sb.Append("<tr><td>").Append(row.Id.ToString(CultureInfo.InvariantCulture)).Append("</td><td>")
                .Append(E(row.Code)).Append("</td><td>").Append(E(row.Name)).Append("</td><td>")
                .Append(E(row.Status)).Append("</td><td>").Append(E(Money(row.Amount))).Append("</td><td>")
                .Append(E(row.Detail)).Append("</td></tr>");
        }

        sb.Append("</tbody></table>");
    }

    private static string DocumentXml(ErpRealEstateSnapshot snap, string title, string asOf)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
            <w:body>
            """);
        P(sb, "ECOMAE · REAL ESTATE BOARD PACK", true, "C4A35A", 28);
        P(sb, title + "  ·  " + asOf + "  ·  Currency AED", false, "0B1F3A", 16);
        sb.Append(Picture("rId4", 1, 5943600, 2209800));
        P(sb, "Executive summary", true, "0B1F3A", 22);
        P(sb, "This pack summarises the live investment, development and construction register. Opportunities stay off both statements. Owned properties, escrow and certificates are balance-sheet items. Rent, IAS 40 fair-value movements and recognised IFRS 15 unit sales are income-statement items. Off-plan collections remain a contract liability until sold.", false, "1E293B", 14);
        P(sb, "Owned " + snap.Kpis.OwnedProperties.ToString(CultureInfo.InvariantCulture)
            + "  ·  Fair value " + Money(snap.Kpis.PortfolioFair)
            + "  ·  Cost " + Money(snap.Kpis.PortfolioCost)
            + "  ·  Unrealized " + Money(snap.Kpis.Unrealized)
            + "  ·  Yield " + Pct(snap.Kpis.GrossYield)
            + "  ·  Escrow " + Money(snap.Kpis.EscrowBalance)
            + "  ·  Cost to complete " + Money(snap.Kpis.CostToComplete), true, "0B1F3A", 14);
        WordTable(sb, "Properties", snap.Properties);
        WordTable(sb, "Valuations", snap.Valuations);
        sb.Append(Picture("rId5", 2, 5943600, 1782000));
        WordTable(sb, "Projects", snap.Projects);
        WordTable(sb, "Escrow", snap.Escrow);
        WordTable(sb, "BOQ lines", snap.BoqLines);
        WordTable(sb, "Certificates", snap.Certificates);
        WordTable(sb, "Unit sales", snap.Sales);
        WordTable(sb, "Instalments", snap.Instalments);
        WordTable(sb, "Allocations", snap.Allocations);
        WordTable(sb, "Cheques", snap.Cheques);
        P(sb, "IFRS map — Balance sheet: 1510, 1520, 1140 escrow, 1120 PDC, 2200 deposits, 2210 payable, 2220 advances. Income statement: 4100 rent, 4110 development revenue (sold only), 4200/6200 fair value, 6210 opex.", false, "1E293B", 13);
        sb.Append("<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/></w:sectPr></w:body></w:document>");
        return sb.ToString();
    }

    private static void WordTable(StringBuilder sb, string title, IReadOnlyList<ErpRealEstateRow> rows)
    {
        P(sb, title, true, "C4A35A", 16);
        if (rows.Count == 0)
        {
            P(sb, "No rows on file yet.", false, "64748B", 12);
            return;
        }

        sb.Append("<w:tbl><w:tblPr><w:tblW w:w=\"5000\" w:type=\"pct\"/><w:tblBorders>");
        foreach (var edge in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
        {
            sb.Append("<w:").Append(edge).Append(" w:val=\"single\" w:sz=\"4\" w:color=\"C4A35A\"/>");
        }

        sb.Append("</w:tblBorders></w:tblPr>");
        Row(sb, ["Id", "Code", "Name", "Status", "Amount"], true);
        foreach (var row in rows.Take(25))
        {
            Row(sb, [row.Id.ToString(CultureInfo.InvariantCulture), row.Code, row.Name, row.Status, Money(row.Amount)], false);
        }

        sb.Append("</w:tbl>");
    }

    private static void Row(StringBuilder sb, IReadOnlyList<string> cells, bool header)
    {
        sb.Append("<w:tr>");
        foreach (var cell in cells)
        {
            sb.Append("<w:tc><w:p><w:r><w:rPr><w:color w:val=\"")
                .Append(header ? "0B1F3A" : "1E293B").Append("\"/><w:sz w:val=\"")
                .Append(header ? "21" : "18").Append("\"/>")
                .Append(header ? "<w:b/>" : "")
                .Append("</w:rPr><w:t xml:space=\"preserve\">")
                .Append(Xml(cell)).Append("</w:t></w:r></w:p></w:tc>");
        }

        sb.Append("</w:tr>");
    }

    private static void P(StringBuilder sb, string text, bool bold, string color, int halfPoints)
    {
        sb.Append("<w:p><w:r><w:rPr><w:color w:val=\"").Append(color).Append("\"/><w:sz w:val=\"")
            .Append((halfPoints * 2).ToString(CultureInfo.InvariantCulture)).Append("\"/>")
            .Append(bold ? "<w:b/>" : "")
            .Append("</w:rPr><w:t xml:space=\"preserve\">").Append(Xml(text)).Append("</w:t></w:r></w:p>");
    }

    private static string Picture(string relId, int docPrId, int cx, int cy)
        => $"""
            <w:p><w:r><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0">
            <wp:extent cx="{cx}" cy="{cy}"/><wp:docPr id="{docPrId}" name="photo{docPrId}"/>
            <a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
            <pic:pic><pic:nvPicPr><pic:cNvPr id="{docPrId}" name="photo{docPrId}"/><pic:cNvPicPr/></pic:nvPicPr>
            <pic:blipFill><a:blip r:embed="{relId}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>
            <pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm>
            <a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic>
            </a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>
            """;

    private static string ContentTypes()
        => """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Default Extension="jpg" ContentType="image/jpeg"/>
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

    private static string RelsDoc()
        => """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/skyline.jpg"/>
              <Relationship Id="rId5" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/construction.jpg"/>
            </Relationships>
            """;

    private static void Write(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteBytes(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string Money(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture) + " AED";

    private static string Pct(decimal rate) => (rate * 100m).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string Xml(string? value)
        => (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string ClipKey(string? value)
    {
        var text = new string((value ?? "real").Where(char.IsLetterOrDigit).ToArray());
        return text.Length == 0 ? "real" : text.ToLowerInvariant();
    }
}
