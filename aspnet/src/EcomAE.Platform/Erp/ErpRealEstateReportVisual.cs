using System.Globalization;
using System.Text;
using SkiaSharp;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Premium investment-real-estate board-pack pages (portfolio, yield, leases).
/// Photographs are painted (dusk skyline, lobby, construction) so the pack
/// always has colour imagery even when the tenant has not uploaded photos.
/// </summary>
internal static class ErpRealEstateReportVisual
{
    public const int PageWidth = 1240;
    public const int PageHeight = 1754;

    private static readonly SKColor Navy = new(11, 31, 58);
    private static readonly SKColor NavyMid = new(22, 58, 107);
    private static readonly SKColor Gold = new(196, 163, 90);
    private static readonly SKColor Cream = new(246, 241, 231);
    private static readonly SKColor Ink = new(30, 41, 59);
    private static readonly SKColor Muted = new(100, 116, 139);
    private static readonly SKColor Card = new(255, 255, 255);
    private static readonly SKColor Teal = new(14, 116, 144);
    private static readonly SKColor Rust = new(180, 83, 9);

    public static IReadOnlyList<byte[]> RenderPages(ErpRealEstateSnapshot snap, string title, string asOf)
    {
        return
        [
            Encode(PaintCover(snap, title, asOf)),
            Encode(PaintSummary(snap, asOf)),
            Encode(PaintPortfolio(snap)),
            Encode(PaintIncome(snap)),
            Encode(PaintPipelineAndIfrs(snap)),
        ];
    }

    public static byte[] SkylineJpeg() => Encode(PaintSkyline(1400, 520));

    public static byte[] ConstructionJpeg() => Encode(PaintConstructionPhoto(1400, 420));

    public static byte[] LobbyJpeg() => Encode(PaintLobby(1400, 420));

    public static byte[] ToPdf(IReadOnlyList<byte[]> jpegs)
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

        WriteAscii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
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

    private static byte[] Encode(SKBitmap bitmap)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 84))
        {
            return data.ToArray();
        }
    }

    private static readonly Lazy<SKTypeface> Face = new(() =>
        SKTypeface.FromFamilyName("DejaVu Sans")
        ?? SKTypeface.FromFamilyName("Liberation Sans")
        ?? SKTypeface.Default);

    private static SKBitmap NewPage()
    {
        var bmp = new SKBitmap(PageWidth, PageHeight);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(Cream);
        return bmp;
    }

    private static void Header(SKCanvas c, string kicker, string heading)
    {
        using var navy = new SKPaint { Color = Navy, IsAntialias = true };
        using var gold = new SKPaint { Color = Gold, IsAntialias = true };
        c.DrawRect(new SKRect(0, 0, PageWidth, 132), navy);
        c.DrawRect(new SKRect(0, 132, PageWidth, 138), gold);
        Text(c, kicker, 48, 52, 16, Gold, false);
        Text(c, heading, 48, 98, 32, SKColors.White, true);
    }

    private static void Footer(SKCanvas c, string asOf, int page, int total)
    {
        using var navy = new SKPaint { Color = Navy, IsAntialias = true };
        using var gold = new SKPaint { Color = Gold, IsAntialias = true };
        c.DrawRect(new SKRect(0, PageHeight - 56, PageWidth, PageHeight), navy);
        c.DrawRect(new SKRect(0, PageHeight - 62, PageWidth, PageHeight - 56), gold);
        Text(c, "Confidential investment report  ·  IAS 40 / IFRS 15 / IFRS 18  ·  " + asOf, 48, PageHeight - 24, 13, Cream, false);
        Text(c, page.ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture), PageWidth - 90, PageHeight - 24, 13, Gold, true);
    }

    private static void Text(SKCanvas c, string text, float x, float y, float size, SKColor color, bool bold)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var font = new SKFont(Face.Value, size) { Embolden = bold, Edging = SKFontEdging.Antialias };
        c.DrawText(text ?? "", x, y, SKTextAlign.Left, font, paint);
    }

    private static SKBitmap PaintCover(ErpRealEstateSnapshot snap, string title, string asOf)
    {
        var bmp = NewPage();
        using var c = new SKCanvas(bmp);
        c.Clear(Navy);
        using (var skyline = PaintSkyline(PageWidth, 760))
        {
            c.DrawBitmap(skyline, 0, 0);
        }

        using var veil = new SKPaint { Color = new SKColor(11, 31, 58, 150), IsAntialias = true };
        c.DrawRect(new SKRect(0, 520, PageWidth, 760), veil);
        using var gold = new SKPaint { Color = Gold, IsAntialias = true };
        c.DrawRect(new SKRect(48, 700, 220, 706), gold);
        Text(c, "ECOMAE  ·  INVESTMENT REAL ESTATE", 48, 690, 16, Gold, true);
        Text(c, "Investment real estate", 48, 820, 44, SKColors.White, true);
        Text(c, "portfolio board pack", 48, 876, 44, Gold, true);
        Text(c, title, 48, 940, 22, Cream, false);
        Wrap(c, "Prepared " + asOf + "  ·  Currency AED  ·  Download anytime from any workspace tab", 48, 980, PageWidth - 96, 16, new SKColor(203, 213, 225));

        DrawKpiStrip(c, snap, 48, 1080);
        Wrap(c, "Photographs are corporate visualisations of a UAE investment skyline and lobby — the figures are live tenant portfolio data (IAS 40 fair value, rent, occupancy).", 48, 1548, PageWidth - 96, 13, new SKColor(148, 163, 184));
        Footer(c, asOf, 1, 5);
        return bmp;
    }

    private static SKBitmap PaintSummary(ErpRealEstateSnapshot snap, string asOf)
    {
        var bmp = NewPage();
        using var c = new SKCanvas(bmp);
        Header(c, "01  EXECUTIVE SUMMARY", "Investment portfolio at a glance");
        using (var lobby = PaintLobby(PageWidth - 96, 220))
        {
            c.DrawBitmap(lobby, 48, 160);
        }

        Wrap(c, "This is the investment real-estate register. Screening opportunities stay off both statements. Completed acquisitions sit on the balance sheet as IAS 40 investment property. Rent, occupancy and collection are income-statement operations. Fair-value movements go through P&L, not OCI.", 48, 404, PageWidth - 96, 15, Ink);
        DrawKpiStrip(c, snap, 48, 500);

        var bars = new (string Label, decimal Value, SKColor Color)[]
        {
            ("Portfolio cost", snap.Kpis.PortfolioCost, NavyMid),
            ("Fair value", snap.Kpis.PortfolioFair, Gold),
            ("Unrealized", Math.Abs(snap.Kpis.Unrealized), snap.Kpis.Unrealized >= 0 ? Teal : Rust),
            ("Annual rent", snap.Kpis.AnnualRent, new SKColor(15, 118, 110)),
        };
        DrawBars(c, bars, 48, 680, PageWidth - 96, 360);
        Wrap(c, "Gross yield " + Pct(snap.Kpis.GrossYield) + "   ·   Occupancy " + Pct(snap.Kpis.Occupancy)
            + "   ·   Collection " + Pct(snap.Kpis.Collection)
            + "   ·   Open opportunities " + snap.Kpis.Opportunities.ToString(CultureInfo.InvariantCulture),
            48, 1080, PageWidth - 96, 16, Ink);

        var narrative = "Owned investment properties " + snap.Kpis.OwnedProperties.ToString(CultureInfo.InvariantCulture)
            + ", units " + snap.Kpis.Units.ToString(CultureInfo.InvariantCulture)
            + ", active leases " + snap.Kpis.ActiveLeases.ToString(CultureInfo.InvariantCulture)
            + ", open rent cheques " + snap.Kpis.OpenCheques.ToString(CultureInfo.InvariantCulture)
            + ". Acquisition cost is never overwritten. A later valuation creates a new IAS 40 fair-value journal.";
        Wrap(c, narrative, 48, 1140, PageWidth - 96, 16, Muted);
        Footer(c, asOf, 2, 5);
        return bmp;
    }

    private static SKBitmap PaintPortfolio(ErpRealEstateSnapshot snap)
    {
        var bmp = NewPage();
        using var c = new SKCanvas(bmp);
        Header(c, "02  PORTFOLIO & VALUATION", "Owned investment property, buys, sells and fair value");
        DrawTable(c, 48, 168, "Owned / property register",
            ["Id", "Code", "Name", "Status", "Cost / value", "Detail"],
            snap.Properties.Take(8).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), r.Code, Clip(r.Name, 28), r.Status, Money(r.Amount), Clip(r.Detail, 28) }));
        DrawTable(c, 48, 620, "Valuation timeline (acquisition cost is never overwritten)",
            ["Id", "Code", "Approved FV", "Estimated", "Detail"],
            snap.Valuations.OrderBy(v => v.Id).Take(7).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), r.Code, Money(r.Amount), Money(r.Amount2), Clip(r.Detail, 36) }));
        DrawTable(c, 48, 1080, "Acquisitions and disposals",
            ["Kind", "Code", "Counterparty", "Status", "Amount"],
            snap.Acquisitions.Concat(snap.Disposals).Take(6).Select(r => new[] { r.Kind, r.Code, Clip(r.Name, 22), r.Status, Money(r.Amount) }));
        Footer(c, DateTime.UtcNow.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), 3, 5);
        return bmp;
    }

    private static SKBitmap PaintIncome(ErpRealEstateSnapshot snap)
    {
        var bmp = NewPage();
        using var c = new SKCanvas(bmp);
        Header(c, "03  INCOME, LEASES & COLLECTIONS", "Rent roll, occupancy and cheque calendar");
        using (var lobby = PaintLobby(PageWidth - 96, 180))
        {
            c.DrawBitmap(lobby, 48, 158);
        }

        DrawTable(c, 48, 360, "Active leases (Ejari registration is a separate status)",
            ["Id", "Code", "Tenant", "Status", "Annual rent"],
            snap.Leases.Take(6).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), r.Code, Clip(r.Name, 22), r.Status, Money(r.Amount) }));
        DrawTable(c, 48, 700, "Tenants",
            ["Id", "Code", "Name", "Status", "Detail"],
            snap.Tenants.Take(5).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), r.Code, Clip(r.Name, 24), r.Status, Clip(r.Detail, 28) }));
        DrawTable(c, 48, 1020, "Rent cheques (not cash until bank clearance)",
            ["Id", "Cheque", "Status", "Amount", "Bank / date"],
            snap.Cheques.Take(6).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), r.Code, r.Status, Money(r.Amount), Clip(r.Detail, 28) }));
        DrawTable(c, 48, 1360, "Tenant deposits (liability 2200)",
            ["Id", "Code", "Tenant", "Status", "Held"],
            snap.Deposits.Take(4).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), r.Code, Clip(r.Name, 22), r.Status, Money(r.Amount) }));
        Footer(c, DateTime.UtcNow.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), 4, 5);
        return bmp;
    }

    private static SKBitmap PaintPipelineAndIfrs(ErpRealEstateSnapshot snap)
    {
        var bmp = NewPage();
        using var c = new SKCanvas(bmp);
        Header(c, "04  PIPELINE & IFRS MAP", "Opportunities stay off statements until acquired");
        DrawTable(c, 48, 168, "Investment opportunities (off both statements)",
            ["Id", "Code", "Name", "Status", "Ask"],
            snap.Opportunities.Take(5).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), r.Code, Clip(r.Name, 24), r.Status, Money(r.Amount) }));
        DrawTable(c, 48, 500, "Development held as investment pipeline",
            ["Id", "Code", "Name", "Status", "Budget"],
            snap.Projects.Take(4).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), r.Code, Clip(r.Name, 22), r.Status, Money(r.Amount) }));
        DrawTable(c, 48, 800, "Unit sales (reserved stays off statements)",
            ["Id", "Buyer", "Status", "Price", "Collected"],
            snap.Sales.Take(5).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), Clip(r.Name, 24), r.Status, Money(r.Amount), Money(r.Amount2) }));
        DrawTable(c, 48, 1120, "Off-plan instalments (contract liability until sold)",
            ["Id", "Sale", "Status", "Amount", "Collected"],
            snap.Instalments.Take(4).Select(r => new[] { r.Id.ToString(CultureInfo.InvariantCulture), Clip(r.Name, 20), r.Status, Money(r.Amount), Money(r.Amount2) }));

        using var card = new SKPaint { Color = Navy, IsAntialias = true };
        c.DrawRoundRect(new SKRect(48, 1460, PageWidth - 48, 1688), 10, 10, card);
        Text(c, "Investment balance sheet", 72, 1500, 16, Gold, true);
        Wrap(c, "1510 investment property · 1130 rent receivable · 1120 cheques under collection · 2200 tenant deposits · 1140 escrow · 2220 buyer advances", 72, 1528, PageWidth - 160, 13, Cream);
        Text(c, "Investment income statement", 72, 1588, 16, Gold, true);
        Wrap(c, "4100 rental income · 4200 / 6200 IAS 40 fair value through P&L · 6210 property opex · 4300 / 6220 disposal · 4110 development revenue only when sold", 72, 1616, PageWidth - 160, 13, Cream);
        Footer(c, DateTime.UtcNow.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), 5, 5);
        return bmp;
    }

    private static void DrawKpiStrip(SKCanvas c, ErpRealEstateSnapshot snap, float x, float y)
    {
        var cards = new (string Label, string Value)[]
        {
            ("Owned", snap.Kpis.OwnedProperties.ToString(CultureInfo.InvariantCulture)),
            ("Fair value", Compact(snap.Kpis.PortfolioFair)),
            ("Unrealized", Compact(snap.Kpis.Unrealized)),
            ("Gross yield", Pct(snap.Kpis.GrossYield)),
            ("Occupancy", Pct(snap.Kpis.Occupancy)),
            ("Annual rent", Compact(snap.Kpis.AnnualRent)),
        };
        var width = (PageWidth - 96 - 20) / 6f;
        for (var i = 0; i < cards.Length; i++)
        {
            var left = x + i * (width + 4);
            using var fill = new SKPaint { Color = i % 2 == 0 ? Card : new SKColor(255, 251, 235), IsAntialias = true };
            using var stroke = new SKPaint { Color = Gold, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
            var rect = new SKRect(left, y, left + width, y + 118);
            c.DrawRoundRect(rect, 8, 8, fill);
            c.DrawRoundRect(rect, 8, 8, stroke);
            Text(c, cards[i].Label.ToUpperInvariant(), left + 12, y + 36, 12, Muted, true);
            Text(c, cards[i].Value, left + 12, y + 84, 22, Navy, true);
        }
    }

    private static void DrawBars(SKCanvas c, IReadOnlyList<(string Label, decimal Value, SKColor Color)> rows, float x, float y, float width, float height)
    {
        var max = rows.Max(r => r.Value);
        if (max <= 0)
        {
            max = 1;
        }

        var rowH = height / rows.Count;
        for (var i = 0; i < rows.Count; i++)
        {
            var top = y + i * rowH;
            Text(c, rows[i].Label, x, top + 22, 14, Ink, false);
            var barW = (float)((double)rows[i].Value / (double)max) * (width - 280);
            using var paint = new SKPaint { Color = rows[i].Color, IsAntialias = true };
            c.DrawRoundRect(new SKRect(x + 220, top + 8, x + 220 + Math.Max(8, barW), top + 36), 4, 4, paint);
            Text(c, Money(rows[i].Value), x + 230 + Math.Max(8, barW), top + 30, 13, Muted, false);
        }
    }

    private static void DrawTable(SKCanvas c, float x, float y, string title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        Text(c, title, x, y, 16, Navy, true);
        using var head = new SKPaint { Color = Navy, IsAntialias = true };
        using var alt = new SKPaint { Color = new SKColor(238, 242, 247), IsAntialias = true };
        using var line = new SKPaint { Color = new SKColor(203, 213, 225), IsAntialias = true };
        var top = y + 12;
        var width = PageWidth - 96;
        var colW = width / headers.Count;
        c.DrawRect(new SKRect(x, top, x + width, top + 32), head);
        for (var i = 0; i < headers.Count; i++)
        {
            Text(c, headers[i], x + 8 + i * colW, top + 22, 12, Gold, true);
        }

        var r = 0;
        foreach (var row in rows)
        {
            var yy = top + 32 + r * 28;
            if (r % 2 == 1)
            {
                c.DrawRect(new SKRect(x, yy, x + width, yy + 28), alt);
            }

            for (var i = 0; i < headers.Count && i < row.Count; i++)
            {
                Text(c, row[i], x + 8 + i * colW, yy + 20, 12, Ink, false);
            }

            r++;
        }

        if (r == 0)
        {
            Text(c, "No rows on file yet.", x + 8, top + 54, 13, Muted, false);
        }
    }

    private static void Wrap(SKCanvas c, string text, float x, float y, float width, float size, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var font = new SKFont(Face.Value, size) { Edging = SKFontEdging.Antialias };
        var words = (text ?? "").Split(' ');
        var line = "";
        var yy = y;
        foreach (var word in words)
        {
            var trial = line.Length == 0 ? word : line + " " + word;
            if (font.MeasureText(trial) > width)
            {
                c.DrawText(line, x, yy, SKTextAlign.Left, font, paint);
                line = word;
                yy += size + 6;
            }
            else
            {
                line = trial;
            }
        }

        if (line.Length > 0)
        {
            c.DrawText(line, x, yy, SKTextAlign.Left, font, paint);
        }
    }

    private static SKBitmap PaintSkyline(int width, int height)
    {
        var bmp = new SKBitmap(width, height);
        using var c = new SKCanvas(bmp);
        using var sky = new SKPaint { IsAntialias = true };
        sky.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, height),
            [new SKColor(8, 18, 40), new SKColor(122, 64, 48), new SKColor(232, 168, 92)],
            [0f, 0.55f, 1f],
            SKShaderTileMode.Clamp);
        c.DrawRect(new SKRect(0, 0, width, height), sky);

        using var water = new SKPaint { Color = new SKColor(18, 40, 64, 180), IsAntialias = true };
        c.DrawRect(new SKRect(0, height * 0.72f, width, height), water);

        var rng = new Random(2026);
        using var tower = new SKPaint { Color = new SKColor(12, 24, 42), IsAntialias = true };
        using var window = new SKPaint { Color = new SKColor(255, 214, 140), IsAntialias = true };
        using var gold = new SKPaint { Color = new SKColor(196, 163, 90, 80), IsAntialias = true };
        for (var i = 0; i < 18; i++)
        {
            var left = 20 + i * (width / 17f);
            var tw = 28 + rng.Next(18, 56);
            var th = 140 + rng.Next(80, 320);
            var top = height * 0.72f - th;
            c.DrawRect(new SKRect(left, top, left + tw, height * 0.72f), tower);
            for (var wy = top + 10; wy < height * 0.70f; wy += 12)
            {
                for (var wx = left + 4; wx < left + tw - 6; wx += 8)
                {
                    if (rng.NextDouble() > 0.45)
                    {
                        c.DrawRect(new SKRect(wx, wy, wx + 3, wy + 5), window);
                    }
                }
            }
        }

        c.DrawCircle(width * 0.82f, height * 0.18f, 36, gold);
        return bmp;
    }

    private static SKBitmap PaintConstructionPhoto(int width, int height)
    {
        var bmp = new SKBitmap(width, height);
        using var c = new SKCanvas(bmp);
        using var sky = new SKPaint { IsAntialias = true };
        sky.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, height),
            [new SKColor(186, 210, 224), new SKColor(244, 236, 220)],
            null,
            SKShaderTileMode.Clamp);
        c.DrawRect(new SKRect(0, 0, width, height), sky);
        using var steel = new SKPaint { Color = new SKColor(71, 85, 105), IsAntialias = true, StrokeWidth = 6, Style = SKPaintStyle.Stroke };
        using var fill = new SKPaint { Color = new SKColor(51, 65, 85), IsAntialias = true };
        using var crane = new SKPaint { Color = Gold, IsAntialias = true, StrokeWidth = 5, Style = SKPaintStyle.Stroke };
        c.DrawRect(new SKRect(80, height * 0.35f, 260, height), fill);
        c.DrawRect(new SKRect(300, height * 0.22f, 520, height), fill);
        c.DrawRect(new SKRect(560, height * 0.40f, 780, height), fill);
        for (var y = height * 0.25f; y < height; y += 28)
        {
            c.DrawLine(80, y, 780, y, steel);
        }

        c.DrawLine(980, height, 980, 30, crane);
        c.DrawLine(980, 40, 1240 < width ? width - 40 : 1180, 40, crane);
        c.DrawLine(1100, 40, 1100, 120, crane);
        using var hook = new SKPaint { Color = Rust, IsAntialias = true };
        c.DrawRect(new SKRect(1088, 120, 1112, 150), hook);
        return bmp;
    }

    private static SKBitmap PaintLobby(int width, int height)
    {
        var bmp = new SKBitmap(width, height);
        using var c = new SKCanvas(bmp);
        using var wall = new SKPaint { IsAntialias = true };
        wall.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(width, height),
            [new SKColor(28, 25, 23), new SKColor(68, 48, 32), new SKColor(196, 163, 90)],
            [0f, 0.55f, 1f],
            SKShaderTileMode.Clamp);
        c.DrawRect(new SKRect(0, 0, width, height), wall);
        using var marble = new SKPaint { Color = new SKColor(246, 241, 231, 40), IsAntialias = true };
        for (var i = 0; i < 12; i++)
        {
            c.DrawLine(0, i * (height / 12f), width, i * (height / 12f) + 20, marble);
        }

        using var lamp = new SKPaint { Color = new SKColor(255, 236, 179), IsAntialias = true };
        c.DrawCircle(width * 0.25f, 40, 16, lamp);
        c.DrawCircle(width * 0.5f, 28, 18, lamp);
        c.DrawCircle(width * 0.75f, 40, 16, lamp);
        return bmp;
    }

    private static string Money(decimal value)
        => value.ToString("N0", CultureInfo.InvariantCulture) + " AED";

    private static string Compact(decimal value)
    {
        var abs = Math.Abs(value);
        var sign = value < 0 ? "-" : "";
        if (abs >= 1_000_000m)
        {
            return sign + (abs / 1_000_000m).ToString("0.00", CultureInfo.InvariantCulture) + "m";
        }

        if (abs >= 1_000m)
        {
            return sign + (abs / 1_000m).ToString("0.0", CultureInfo.InvariantCulture) + "k";
        }

        return sign + abs.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string Pct(decimal rate)
        => (rate * 100m).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}
