using System.Data.Common;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Renders the D365-style wholesale "TAX INVOICE" and delivery-note PDFs for any tenant.
/// The chrome comes from the per-legal-entity document profile
/// (<see cref="IErpD365DocCycleService.EntityDocProfileAsync"/>, falling back to the
/// tenant-wide row 0) so every legal entity can carry its own legal name, TRN, address,
/// logo, and footer exactly as the SHAM printed invoices do.
/// Layout mirrors the attached wholesale invoice: customer block + invoice meta block,
/// S.No|Part No|Description|Brand|Stock No+Size|Qty|Unit Price|Gross|Vat|Net grid,
/// totals + amount-in-words, receiver/signatory strip, E. &amp; O. E. and Page X of Y.
/// </summary>
public static class ErpD365DocPdf
{
    private const int PageW = 794;   // A4 @96dpi
    private const int PageH = 1123;
    private const float Margin = 36f;
    private const float Scale = 1.5f; // render scale for crisp text inside JPEG

    private static readonly Lazy<SKTypeface> Face = new(() =>
        SKTypeface.FromFamilyName("DejaVu Sans")
        ?? SKTypeface.FromFamilyName("Liberation Sans")
        ?? SKTypeface.Default);

    private static SKFont Font(float size, bool bold = false)
        => new(Face.Value, size * Scale) { Embolden = bold, Edging = SKFontEdging.Antialias };

    private static SKPaint Ink(SKColor c) => new() { Color = c, IsAntialias = true };

    private static readonly SKColor Black = new(30, 30, 30);
    private static readonly SKColor Grey = new(90, 90, 90);
    private static readonly SKColor LineColor = new(60, 60, 60);
    private static readonly SKColor HeadFill = new(235, 235, 235);

    /// <summary>Invoice data for the renderer.</summary>
    public sealed record InvoiceModel(
        IReadOnlyDictionary<string, string?> Profile,
        string InvoiceNumber,
        string InvoiceDate,          // dd-mm-yyyy
        string CustomerCode,
        string CustomerName,
        string CustomerAddress,
        string CustomerAreaCountry,
        string CustomerPhoneFax,
        string CustomerTrn,
        string LpoNo,
        string PaymentTerms,
        string UserId,
        string RefNo,
        string Currency,
        IReadOnlyList<Line> Lines,
        decimal Discount,
        decimal Total,
        decimal Vat,
        decimal Net,
        string AmountWords,
        string Salesman);

    public sealed record Line(
        string PartNo, string Description, string Brand, string StockNoSize,
        decimal Qty, decimal UnitPrice, decimal Gross, decimal VatAmount, decimal Net);

    public sealed record DeliveryModel(
        IReadOnlyDictionary<string, string?> Profile,
        string DocumentNo, string Warehouse, string Site,
        string CustomerAccount, string CustomerName, string SalesOrderNo, string Status,
        string InvoiceName, string InvoiceAddress, string SalesOrderRef,
        string BuyerName, string BuyerAddress, string BuyerCountry, string BuyerPhone,
        string CargoCompany, string ContactPerson, string ContactPhone, string Location,
        string Remarks, string CartonMark, string CartonQty, string PackageType, string ContainerNumber,
        string DeliveryBy, string DeliveryPhone, string DeliveryDate,
        string ReceiverName, string ReceiverPhone, string HelperName, string VehicleNumber,
        IReadOnlyList<(string Item, string Name, decimal Qty, string Unit)> Items);

    /// <param name="docTitle">Printed document title ("TAX INVOICE", "SALES QUOTATION", ...).</param>
    /// <param name="numLabel">Label before the document number (default "Invoice Number").</param>
    public static byte[] InvoicePdf(InvoiceModel m, string docTitle = "TAX INVOICE", string numLabel = "Invoice Number")
    {
        ArgumentNullException.ThrowIfNull(m);
        var jpegs = RenderPaged(m, PageH, docTitle, numLabel);
        return ErpRealEstateReportVisual.ToPdf(jpegs);
    }

    /// <summary>Quotation print = invoice layout with quotation chrome (D365 print view).</summary>
    public static byte[] QuotationPdf(InvoiceModel m) => InvoicePdf(m, "SALES QUOTATION", "Quotation Number");

    public static byte[] DeliveryNotePdf(DeliveryModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var jpegs = RenderDelivery(m);
        return ErpRealEstateReportVisual.ToPdf(jpegs);
    }

    private static IReadOnlyList<byte[]> RenderPaged(InvoiceModel m, int pageH, string docTitle = "TAX INVOICE", string numLabel = "Invoice Number")
    {
        const float contentTop = Margin + 118;   // below letterhead + two meta columns
        const float gridHeadH = 26;
        const float rowH = 18;
        const float footerTop = PageH - Margin - 118;
        var rowsPerPage = (int)((footerTop - contentTop - gridHeadH) / rowH) - 1;
        if (rowsPerPage < 6) rowsPerPage = 6;

        var pages = new List<byte[]>();
        var pageCount = (int)Math.Ceiling(m.Lines.Count / (double)rowsPerPage);
        if (pageCount == 0) pageCount = 1;

        for (var p = 0; p < pageCount; p++)
        {
            var slice = m.Lines.Skip(p * rowsPerPage).Take(rowsPerPage).ToList();
            using var bmp = RenderInvoicePage(m, slice, p, pageCount, p == pageCount - 1, docTitle, numLabel);
            pages.Add(Encode(bmp));
        }

        return pages;
    }

    private static SKBitmap RenderInvoicePage(InvoiceModel m, List<Line> lines, int page, int pageCount, bool last, string docTitle, string numLabel)
    {
        var bmp = new SKBitmap(new SKImageInfo((int)(PageW * Scale), (int)(PageH * Scale), SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        canvas.Scale(Scale);

        var x0 = Margin;
        var x1 = PageW - Margin;
        var y = Margin;

        // ----- letterhead -----
        y = Letterhead(canvas, m.Profile, docTitle, x0, x1, y);

        // ----- meta block: customer (left) / invoice (right) -----
        var midX = x0 + (x1 - x0) * 0.52f;
        var metaTop = y;
        var ly = metaTop;
        ly = MetaRow(canvas, x0, ly, "Customer Code", m.CustomerCode, true);
        ly = MetaRow(canvas, x0, ly, "Name", m.CustomerName, true);
        ly = MetaRow(canvas, x0, ly, "Address", m.CustomerAddress, true);
        ly = MetaRow(canvas, x0, ly, "Area - Country", m.CustomerAreaCountry, true);
        ly = MetaRow(canvas, x0, ly, "Phone - Fax", m.CustomerPhoneFax, true);
        ly = MetaRow(canvas, x0, ly, "Customer TRN", m.CustomerTrn, true);

        var ry = metaTop;
        var rx = midX;
        ry = MetaRow(canvas, rx, ry, "Invoice Date", m.InvoiceDate, false);
        ry = MetaRow(canvas, rx, ry, numLabel, m.InvoiceNumber, false);
        ry = MetaRow(canvas, rx, ry, "LPO NO", m.LpoNo, false);
        ry = MetaRow(canvas, rx, ry, "Payment", m.PaymentTerms, false);
        ry = MetaRow(canvas, rx, ry, "User ID", m.UserId, false);
        ry = MetaRow(canvas, rx, ry, "Ref No", m.RefNo, false);
        ry = MetaRow(canvas, rx, ry, "Currency", m.Currency, false);
        ry = MetaRow(canvas, rx, ry, "TRN", P(m.Profile, "trn"), false);

        y = Math.Max(ly, ry) + 6;
        HRule(canvas, x0, x1, y, 1.2f);

        // ----- grid -----
        float[] cw = [34, 92, 190, 60, 92, 40, 62, 66, 52, 70]; // sums ~ PageW-2*Margin
        string[] heads = ["S.No", "Part No", "Description", "Brand", "Stock No+Size", "Qty", "Unit Price", "Gross", "Vat", "Net"];
        var gy = y + 4;
        // header band
        canvas.DrawRect(SKRect.Create(x0, gy, x1 - x0, 24), Ink(HeadFill));
        canvas.DrawRect(SKRect.Create(x0, gy, x1 - x0, 24), new SKPaint { Color = LineColor, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f });
        using (var hf = Font(7.2f, true))
        {
            var cx = x0;
            for (var i = 0; i < heads.Length; i++)
            {
                var tw = cw[i];
                var tx = i >= 5 ? cx + tw - 4 : cx + 4;
                var align = i >= 5 ? SKTextAlign.Right : SKTextAlign.Left;
                canvas.DrawText(heads[i], tx, gy + 16, align, hf, Ink(Black));
                cx += tw;
            }
        }

        gy += 24;
        using (var rf = Font(7.2f))
        {
            var sn = page * 999_999;
            for (var i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                var cx = x0;
                var ry2 = gy + 13;
                void Cell(int idx, string text)
                {
                    var align = idx >= 5 ? SKTextAlign.Right : SKTextAlign.Left;
                    var tx = idx >= 5 ? cx + cw[idx] - 4 : cx + 4;
                    canvas.DrawText(Clip(text, rf, cw[idx] - 8), tx, ry2, align, rf, Ink(Black));
                    cx += cw[idx];
                }
                Cell(0, (sn + i + 1).ToString(CultureInfo.InvariantCulture));
                Cell(1, l.PartNo);
                Cell(2, l.Description);
                Cell(3, l.Brand);
                Cell(4, l.StockNoSize);
                Cell(5, Num(l.Qty, "0.###"));
                Cell(6, Num(l.UnitPrice));
                Cell(7, Num(l.Gross));
                Cell(8, Num(l.VatAmount));
                Cell(9, Num(l.Net));
                gy += 18;
            }
        }
        canvas.DrawRect(SKRect.Create(x0, y + 4, x1 - x0, gy - y - 4), new SKPaint { Color = LineColor, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f });
        // column separators (light)
        {
            var cx = x0;
            using var thin = new SKPaint { Color = new SKColor(190, 190, 190), Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
            for (var i = 0; i < cw.Length - 1; i++)
            {
                cx += cw[i];
                canvas.DrawLine(cx, y + 4, cx, gy, thin);
            }
        }

        if (last)
        {
            gy += 6;
            // ----- totals + amount in words -----
            var tx = x1 - 210f;
            gy = TotalRow(canvas, tx, x1, gy, "Total", m.Total + m.Discount);
            gy = TotalRow(canvas, tx, x1, gy, "Discount", m.Discount);
            gy = TotalRow(canvas, tx, x1, gy, "Total", m.Total);
            gy = TotalRow(canvas, tx, x1, gy, "Vat", m.Vat);
            gy = TotalRow(canvas, tx, x1, gy, "Net", m.Net, true);
            using (var wf = Font(7.4f, true))
            {
                canvas.DrawText("*** " + m.AmountWords, x0, gy + 2, wf, Ink(Black));
            }
            gy += 22;

            // ----- signature strip -----
            var sigY = PageH - Margin - 96;
            HRule(canvas, x0, x1, sigY, 0.9f);
            using var sf = Font(7.6f, true);
            using var sf2 = Font(7.2f);
            canvas.DrawText("Receiver's Name", x0 + 4, sigY + 16, sf, Ink(Black));
            canvas.DrawText("& Signature", x0 + 4, sigY + 28, sf2, Ink(Black));
            var forName = P(m.Profile, "legal_name");
            canvas.DrawText("For " + (forName.Length > 0 ? forName : "Company"), x1 - 250, sigY + 16, sf, Ink(Black));
            canvas.DrawText("Salesman & Signature" + (m.Salesman.Length > 0 ? ": " + m.Salesman : ""), x1 - 250, sigY + 28, sf2, Ink(Black));
            HRule(canvas, x0, x1, sigY + 40, 0.9f);
            canvas.DrawText("E. & O. E.", x0 + 4, sigY + 54, sf2, Ink(Grey));
        }

        // ----- footer -----
        Letterfoot(canvas, m.Profile, x0, x1, page, pageCount);
        return bmp;
    }

    private static IReadOnlyList<byte[]> RenderDelivery(DeliveryModel m)
    {
        var bmp = new SKBitmap(new SKImageInfo((int)(PageW * Scale), (int)(PageH * Scale), SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        canvas.Scale(Scale);
        var x0 = Margin; var x1 = PageW - Margin; var y = Margin;
        y = Letterhead(canvas, m.Profile, "DELIVERY NOTE", x0, x1, y);

        using var lf = Font(7.6f, true);
        using var vf = Font(7.4f);
        float LabelVal(float x, float yy, string label, string value)
        {
            canvas.DrawText(label, x, yy, lf, Ink(Grey));
            canvas.DrawText(Clip(value, vf, 300), x + 132, yy, vf, Ink(Black));
            return yy + 15;
        }

        var mid = x0 + (x1 - x0) * 0.5f;
        var lyy = y; var ryy = y;
        canvas.DrawText("DOCUMENT", x0, lyy + 8, Font(8.4f, true), Ink(Black)); lyy += 22;
        lyy = LabelVal(x0, lyy, "Document No", m.DocumentNo);
        lyy = LabelVal(x0, lyy, "Warehouse", m.Warehouse);
        lyy = LabelVal(x0, lyy, "Site", m.Site);
        lyy = LabelVal(x0, lyy, "Customer account", m.CustomerAccount);
        lyy = LabelVal(x0, lyy, "Name", m.CustomerName);
        lyy = LabelVal(x0, lyy, "Sales order", m.SalesOrderNo);
        lyy = LabelVal(x0, lyy, "Status", m.Status);

        canvas.DrawText("ADDRESS DETAILS", mid, ryy + 8, Font(8.4f, true), Ink(Black)); ryy += 22;
        ryy = LabelVal(mid, ryy, "Invoice name", m.InvoiceName);
        ryy = LabelVal(mid, ryy, "Invoice address", m.InvoiceAddress);
        ryy = LabelVal(mid, ryy, "Buyer name", m.BuyerName);
        ryy = LabelVal(mid, ryy, "Buyer address", m.BuyerAddress);
        ryy = LabelVal(mid, ryy, "Country", m.BuyerCountry);
        ryy = LabelVal(mid, ryy, "Phone", m.BuyerPhone);

        y = Math.Max(lyy, ryy) + 10;
        HRule(canvas, x0, x1, y, 0.9f); y += 16;
        canvas.DrawText("CONSIGNMENT & PACKING", x0, y, Font(8.4f, true), Ink(Black)); y += 16;
        lyy = y; ryy = y;
        lyy = LabelVal(x0, lyy, "Cargo company", m.CargoCompany);
        lyy = LabelVal(x0, lyy, "Contact person", m.ContactPerson);
        lyy = LabelVal(x0, lyy, "Contact phone", m.ContactPhone);
        lyy = LabelVal(x0, lyy, "Location", m.Location);
        lyy = LabelVal(x0, lyy, "Remarks", m.Remarks);
        ryy = LabelVal(mid, ryy, "Carton mark", m.CartonMark);
        ryy = LabelVal(mid, ryy, "Carton qty", m.CartonQty);
        ryy = LabelVal(mid, ryy, "Package type", m.PackageType);
        ryy = LabelVal(mid, ryy, "Container number", m.ContainerNumber);

        y = Math.Max(lyy, ryy) + 8;
        HRule(canvas, x0, x1, y, 0.9f); y += 16;
        canvas.DrawText("DELIVERY DETAILS", x0, y, Font(8.4f, true), Ink(Black)); y += 16;
        lyy = y; ryy = y;
        lyy = LabelVal(x0, lyy, "Delivery by", m.DeliveryBy);
        lyy = LabelVal(x0, lyy, "Phone", m.DeliveryPhone);
        lyy = LabelVal(x0, lyy, "Date", m.DeliveryDate);
        lyy = LabelVal(x0, lyy, "Receiver name", m.ReceiverName);
        ryy = LabelVal(mid, ryy, "Receiver phone", m.ReceiverPhone);
        ryy = LabelVal(mid, ryy, "Helper name", m.HelperName);
        ryy = LabelVal(mid, ryy, "Vehicle number", m.VehicleNumber);
        y = Math.Max(lyy, ryy) + 8;

        // items grid
        if (m.Items.Count > 0)
        {
            HRule(canvas, x0, x1, y, 0.9f); y += 6;
            float[] cw = [40, 140, 300, 60, 80];
            string[] heads = ["S.No", "Item", "Description", "Qty", "Unit"];
            canvas.DrawRect(SKRect.Create(x0, y, x1 - x0, 22), Ink(HeadFill));
            canvas.DrawRect(SKRect.Create(x0, y, x1 - x0, 22), new SKPaint { Color = LineColor, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f });
            using (var hf = Font(7.2f, true))
            {
                var cx = x0;
                for (var i = 0; i < heads.Length; i++)
                { canvas.DrawText(heads[i], cx + 4, y + 15, hf, Ink(Black)); cx += cw[i]; }
            }
            y += 22;
            for (var idx = 0; idx < m.Items.Count; idx++)
            {
                var (item, name, qty, unit) = m.Items[idx];
                var cx = x0;
                string[] cells = [(idx + 1).ToString(CultureInfo.InvariantCulture), item, name, Num(qty, "0.###"), unit];
                for (var c = 0; c < cells.Length; c++)
                {
                    canvas.DrawText(Clip(cells[c], vf, cw[c] - 8), cx + 4, y + 13, vf, Ink(Black));
                    cx += cw[c];
                }
                y += 16;
            }
        }

        // signature
        var sigY = PageH - Margin - 96;
        HRule(canvas, x0, x1, sigY, 0.9f);
        canvas.DrawText("Receiver's Name & Signature", x0 + 4, sigY + 16, lf, Ink(Black));
        var forName = P(m.Profile, "legal_name");
        canvas.DrawText("For " + (forName.Length > 0 ? forName : "Company"), x1 - 250, sigY + 16, lf, Ink(Black));
        HRule(canvas, x0, x1, sigY + 40, 0.9f);
        canvas.DrawText("E. & O. E.", x0 + 4, sigY + 54, vf, Ink(Grey));
        Letterfoot(canvas, m.Profile, x0, x1, 0, 1);
        return [Encode(bmp)];
    }

    private static float Letterhead(SKCanvas canvas, IReadOnlyDictionary<string, string?> p, string docTitle, float x0, float x1, float y)
    {
        var name = P(p, "legal_name");
        var trn = P(p, "trn");
        var addr = JoinNonEmpty(", ", P(p, "address_line1"), P(p, "address_line2"), P(p, "city"), P(p, "emirate"), P(p, "country_code"));
        var contact = JoinNonEmpty("  ", "Tel: " + P(p, "phone"), "Fax: " + P(p, "fax"));
        var email = P(p, "email");
        using var tf = Font(13f, true);
        using var sf = Font(8.4f);
        var cx = (x0 + x1) / 2f;
        canvas.DrawText(name.Length > 0 ? name : "Company", cx, y + 16, SKTextAlign.Center, tf, Ink(Black));
        var yy = y + 32;
        if (addr.Length > 0) { canvas.DrawText(addr, cx, yy, SKTextAlign.Center, sf, Ink(Grey)); yy += 12; }
        if (contact.Length > 0 || email.Length > 0)
        {
            canvas.DrawText(JoinNonEmpty("   ", contact, "Email: " + email), cx, yy, SKTextAlign.Center, sf, Ink(Grey)); yy += 12;
        }
        if (trn.Length > 0) { canvas.DrawText("TRN: " + trn, cx, yy, SKTextAlign.Center, sf, Ink(Grey)); yy += 12; }
        var note = P(p, "doc_header_note");
        if (note.Length > 0) { canvas.DrawText(note, cx, yy, SKTextAlign.Center, sf, Ink(Grey)); yy += 12; }
        yy += 4;
        HRule(canvas, x0, x1, yy, 1.4f);
        using var df = Font(12f, true);
        canvas.DrawText(docTitle, cx, yy + 18, SKTextAlign.Center, df, Ink(Black));
        return yy + 30;
    }

    private static void Letterfoot(SKCanvas canvas, IReadOnlyDictionary<string, string?> p, float x0, float x1, int page, int pages)
    {
        var y = PageH - Margin - 26;
        var legal = P(p, "legal_footer");
        var note = P(p, "doc_footer_note");
        using var f = Font(7.2f);
        if (legal.Length > 0 || note.Length > 0)
        {
            var cx = (x0 + x1) / 2f;
            if (legal.Length > 0) { canvas.DrawText(legal, cx, y, SKTextAlign.Center, f, Ink(Grey)); y += 11; }
            if (note.Length > 0) { canvas.DrawText(note, cx, y, SKTextAlign.Center, f, Ink(Grey)); y += 11; }
        }
        var bank = JoinNonEmpty("   ", P(p, "bank_name"), "IBAN: " + P(p, "bank_iban"));
        if (bank.Trim().Length > 0) { canvas.DrawText(bank, (x0 + x1) / 2f, y, SKTextAlign.Center, f, Ink(Grey)); }
        using var pf = Font(7.2f);
        canvas.DrawText($"Page {page + 1} of {pages}", x1, PageH - Margin + 2, SKTextAlign.Right, pf, Ink(Grey));
    }

    private static float MetaRow(SKCanvas canvas, float x, float y, string label, string value, bool left)
    {
        using var lf = Font(7.2f, true);
        using var vf = Font(7.4f);
        canvas.DrawText(label, x, y + 10, lf, Ink(Grey));
        canvas.DrawText(Clip(value, vf, 250), x + 96, y + 10, vf, Ink(Black));
        return y + 14.5f;
    }

    private static float TotalRow(SKCanvas canvas, float tx, float x1, float y, string label, decimal amount, bool bold = false)
    {
        using var f = Font(7.8f, bold);
        canvas.DrawText(label, tx, y + 12, f, Ink(Black));
        canvas.DrawText(Num(amount), x1 - 4, y + 12, SKTextAlign.Right, f, Ink(Black));
        return y + 16f;
    }

    private static void HRule(SKCanvas canvas, float x0, float x1, float y, float w)
        => canvas.DrawLine(x0, y, x1, y, new SKPaint { Color = LineColor, StrokeWidth = w });

    private static string P(IReadOnlyDictionary<string, string?> p, string key)
        => p.TryGetValue(key, out var v) ? (v ?? "") : "";

    private static string JoinNonEmpty(string sep, params string[] parts)
        => string.Join(sep, parts.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string Num(decimal v, string fmt = "0.00") => v.ToString(fmt, CultureInfo.InvariantCulture);

    private static string Clip(string s, SKFont font, float maxW)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var w = font.MeasureText(s);
        if (w <= maxW) return s;
        while (s.Length > 1 && font.MeasureText(s + "…") > maxW) s = s[..^1];
        return s + "…";
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 92))
        {
            return data.ToArray();
        }
    }

    /// <summary>
    /// Load an invoice model from the tenant DB. Reads the e-invoice document
    /// (<c>epc_einvoice_documents</c> + <c>epc_einvoice_lines</c>), the buyer profile
    /// (<c>epc_einvoice_buyer_profiles</c>), the source sales order for entity/LPO/salesman,
    /// and the per-entity document profile (row 0 fallback).
    /// </summary>
    public static async Task<InvoiceModel?> LoadInvoiceModelAsync(
        DbConnection connection,
        IErpD365DocCycleService d365,
        long invoiceId,
        CancellationToken cancellationToken = default)
    {
        await d365.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var doc = await ErpD365DocCycleService.FirstRowAsync(
            connection,
            "SELECT `id`, `invoice_number`, `order_id`, `user_id`, `issue_date`, `payment_terms`, `currency_code`,"
            + " `subtotal_ex_vat`, `total_vat`, `total_incl_vat`, `paid_amount`"
            + " FROM `epc_einvoice_documents` WHERE `id` = ? LIMIT 1",
            cancellationToken,
            invoiceId).ConfigureAwait(false);
        if (doc is null) return null;

        string G(IReadOnlyDictionary<string, string?> r, string k)
            => r.TryGetValue(k, out var v) && v is not null ? v : "";
        static long GL(IReadOnlyDictionary<string, string?> r, string k)
            => r.TryGetValue(k, out var v) && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        static decimal GD(IReadOnlyDictionary<string, string?> r, string k)
            => r.TryGetValue(k, out var v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 0;

        var buyer = await ErpD365DocCycleService.FirstRowAsync(
            connection,
            "SELECT `buyer_name`, `trn`, `legal_reg_no`, `address_line1`, `city`, `emirate`, `country_code`, `phone`, `email`"
            + " FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1",
            cancellationToken,
            GL(doc, "user_id")).ConfigureAwait(false) ?? new Dictionary<string, string?>();

        // Source sales order carries entity + LPO + salesman for the doc profile + meta block.
        var order = await ErpD365DocCycleService.FirstRowAsync(
            connection,
            "SELECT `entity_id`, `lpo_no`, `ref_no`, `user_id_text`, `salesman`, `customer_account`"
            + " FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1",
            cancellationToken,
            GL(doc, "order_id")).ConfigureAwait(false) ?? new Dictionary<string, string?>();

        var profile = await d365.EntityDocProfileAsync(connection, GL(order, "entity_id"), cancellationToken).ConfigureAwait(false);

        var lines = new List<Line>();
        var rows = await ErpD365DocCycleService.RowsAsync(
            connection,
            "SELECT l.`item_name`, l.`item_description`, l.`quantity`, l.`unit_price`, l.`line_net`, l.`tax_rate`, l.`tax_amount`,"
            + " fb.`value` AS brand, fo.`value` AS oem_part_no, fs.`value` AS stock_no_size"
            + " FROM `epc_einvoice_lines` l"
            + " LEFT JOIN `epc_erp_inv_items` i ON i.`sku` = l.`item_name` OR i.`name` = l.`item_name`"
            + " LEFT JOIN `epc_erp_inv_item_fields` fb ON fb.`item_id` = i.`id` AND fb.`field_key` = 'brand'"
            + " LEFT JOIN `epc_erp_inv_item_fields` fo ON fo.`item_id` = i.`id` AND fo.`field_key` = 'oem_part_no'"
            + " LEFT JOIN `epc_erp_inv_item_fields` fs ON fs.`item_id` = i.`id` AND fs.`field_key` IN ('stock_no_size','size_stock')"
            + " WHERE l.`document_id` = ? ORDER BY l.`line_no`, l.`id`",
            cancellationToken,
            GL(doc, "id")).ConfigureAwait(false);
        foreach (var r in rows)
        {
            var net2 = GD(r, "line_net");
            var vat = GD(r, "tax_amount");
            lines.Add(new Line(
                G(r, "oem_part_no").Length > 0 ? G(r, "oem_part_no") : G(r, "item_name"),
                G(r, "item_description").Length > 0 ? G(r, "item_description") : G(r, "item_name"),
                G(r, "brand"), G(r, "stock_no_size"),
                GD(r, "quantity"), GD(r, "unit_price"), net2, vat, net2 + vat));
        }

        var total = GD(doc, "total_incl_vat");
        var issueDate = GL(doc, "issue_date") > 0
            ? DateTimeOffset.FromUnixTimeSeconds(GL(doc, "issue_date")).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)
            : "";
        var currency = G(doc, "currency_code");
        return new InvoiceModel(
            profile,
            G(doc, "invoice_number"), issueDate,
            G(order, "customer_account").Length > 0 ? G(order, "customer_account") : G(doc, "user_id"),
            G(buyer, "buyer_name"),
            JoinNonEmpty(", ", G(buyer, "address_line1"), G(buyer, "city")),
            JoinNonEmpty(" - ", G(buyer, "emirate"), G(buyer, "country_code")),
            G(buyer, "phone"), G(buyer, "trn"),
            G(order, "lpo_no"),
            G(doc, "payment_terms"),
            G(order, "user_id_text"),
            G(order, "ref_no"),
            currency,
            lines,
            Discount: 0m,
            Total: GD(doc, "subtotal_ex_vat"),
            Vat: GD(doc, "total_vat"),
            Net: total,
            AmountWords: ErpDocumentControlRender.AmountWordsEn(total) + (currency.Length > 0 ? " " + currency : ""),
            Salesman: G(order, "salesman"));
    }

    /// <summary>Load a delivery-note model from the tenant DB (note + order + entity profile).</summary>
    public static async Task<DeliveryModel?> LoadDeliveryModelAsync(
        DbConnection connection,
        IErpD365DocCycleService d365,
        long noteId,
        CancellationToken cancellationToken = default)
    {
        await d365.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var note = await ErpD365DocCycleService.FirstRowAsync(
            connection,
            "SELECT * FROM `epc_erp_delivery_notes` WHERE `id` = ? LIMIT 1",
            cancellationToken,
            noteId).ConfigureAwait(false);
        if (note is null) return null;

        string G(IReadOnlyDictionary<string, string?> r, string k)
            => r.TryGetValue(k, out var v) && v is not null ? v : "";
        static long GL(IReadOnlyDictionary<string, string?> r, string k)
            => r.TryGetValue(k, out var v) && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

        var order = await ErpD365DocCycleService.FirstRowAsync(
            connection,
            "SELECT `entity_id`, `so_no`, `customer_account`, `customer_name` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1",
            cancellationToken,
            GL(note, "order_id")).ConfigureAwait(false) ?? new Dictionary<string, string?>();

        var profile = await d365.EntityDocProfileAsync(connection, GL(note, "entity_id") > 0 ? GL(note, "entity_id") : GL(order, "entity_id"), cancellationToken).ConfigureAwait(false);

        var items = new List<(string, string, decimal, string)>();
        var lines = await ErpD365DocCycleService.RowsAsync(
            connection,
            "SELECT `item_code` AS item_number, `description` AS product_name, `qty`, `unit` FROM `epc_erp_sales_order_lines` WHERE `sales_order_id` = ? ORDER BY `id`",
            cancellationToken,
            GL(note, "order_id")).ConfigureAwait(false);
        foreach (var l in lines)
        {
            var qty = l.TryGetValue("qty", out var q) && decimal.TryParse(q, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 0;
            items.Add((G(l, "item_number"), G(l, "product_name"), qty, G(l, "unit")));
        }

        return new DeliveryModel(
            profile,
            G(note, "note_no"), G(note, "warehouse"), G(note, "site"),
            G(note, "customer_account").Length > 0 ? G(note, "customer_account") : G(order, "customer_account"),
            G(note, "customer_name").Length > 0 ? G(note, "customer_name") : G(order, "customer_name"),
            G(order, "so_no"), G(note, "status_ext").Length > 0 ? G(note, "status_ext") : G(note, "status"),
            G(note, "invoice_name"), G(note, "invoice_address"), G(order, "so_no"),
            G(note, "buyer_name"), G(note, "buyer_address"), G(note, "buyer_country"), G(note, "buyer_phone"),
            G(note, "cargo_company"), G(note, "contact_person"), G(note, "contact_phone"), G(note, "location"),
            G(note, "remarks"), G(note, "carton_mark"), G(note, "carton_qty"), G(note, "package_type"), G(note, "container_number"),
            G(note, "delivery_by"), G(note, "delivery_phone"), G(note, "delivery_date"),
            G(note, "receiver_name"), G(note, "receiver_phone"), G(note, "helper_name"), G(note, "vehicle_number"),
            items);
    }


    /// <summary>
    /// Load a sales-quotation model from epc_erp_sales_quotations(+lines) for the print view.
    /// Same wholesale chrome as the tax invoice; title = SALES QUOTATION.
    /// </summary>
    public static async Task<InvoiceModel?> LoadQuotationModelAsync(
        DbConnection connection,
        IErpD365DocCycleService d365,
        long quotationId,
        CancellationToken cancellationToken = default)
    {
        await d365.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var doc = await ErpD365DocCycleService.FirstRowAsync(
            connection,
            "SELECT * FROM `epc_erp_sales_quotations` WHERE `id` = ? LIMIT 1",
            cancellationToken,
            quotationId).ConfigureAwait(false);
        if (doc is null) return null;

        static string G(IReadOnlyDictionary<string, string?> r, string k)
            => r.TryGetValue(k, out var v) && v is not null ? v : "";
        static decimal GD(IReadOnlyDictionary<string, string?> r, string k)
            => r.TryGetValue(k, out var v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 0m;
        static long Gun(IReadOnlyDictionary<string, string?> r, string k)
            => r.TryGetValue(k, out var v) && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

        var profile = await d365.EntityDocProfileAsync(connection, Gun(doc, "entity_id"), cancellationToken).ConfigureAwait(false);

        var rows = await ErpD365DocCycleService.RowsAsync(
            connection,
            "SELECT * FROM `epc_erp_sales_quotation_lines` WHERE `quotation_id` = ? ORDER BY `line_no`, `id`",
            cancellationToken,
            quotationId).ConfigureAwait(false);
        var lines = new List<Line>();
        decimal net = 0m, vat = 0m;
        foreach (var r in rows)
        {
            var lineNet = GD(r, "net_amount");
            var lineVat = lineNet * GD(r, "vat_pct") / 100m;
            net += lineNet; vat += lineVat;
            lines.Add(new Line(
                G(r, "item_number"),
                G(r, "product_name"),
                "", G(r, "size_stock_no").Length > 0 ? G(r, "size_stock_no") : "",
                GD(r, "qty"), GD(r, "unit_price"), lineNet, lineVat, lineNet + lineVat));
        }

        var total = net + vat - GD(doc, "total_discount_amount");
        var currency = G(doc, "currency_code");
        return new InvoiceModel(
            profile,
            G(doc, "quote_no"), "",
            G(doc, "customer_account").Length > 0 ? G(doc, "customer_account") : G(doc, "prospect_account"),
            G(doc, "customer_name").Length > 0 ? G(doc, "customer_name") : G(doc, "prospect_name"),
            G(doc, "delivery_address"), "", "", "",
            LpoNo: G(doc, "customer_requisition"),
            PaymentTerms: G(doc, "payment_terms"),
            UserId: G(doc, "salesman"),
            RefNo: G(doc, "sales_quotation_ref"),
            Currency: currency,
            Lines: lines,
            Discount: GD(doc, "total_discount_amount"),
            Total: net,
            Vat: vat,
            Net: total,
            AmountWords: ErpDocumentControlRender.AmountWordsEn(total) + (currency.Length > 0 ? " " + currency : ""),
            Salesman: G(doc, "salesman"));
    }
}
