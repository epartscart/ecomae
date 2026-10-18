using System.Globalization;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Erp;

public enum ErpPurchasePrintKind
{
    Lpo,
    PurchaseOrder,
    PurchaseInvoice,
    MetalPurchase,
}

public sealed record ErpPurchasePrintParty(
    string Code,
    string Name,
    string Address,
    string Phone,
    string Email,
    string Trn);

public sealed record ErpPurchasePrintLine(
    int Number,
    string Description,
    decimal Qty,
    decimal Rate,
    decimal AmountExVat,
    decimal VatRate,
    decimal VatAmount,
    decimal AmountInclVat,
    decimal GrossWt = 0,
    decimal Purity = 0,
    decimal PureWt = 0,
    decimal MakingRate = 0,
    decimal MakingAmount = 0,
    decimal MetalAmount = 0);

public sealed record ErpPurchasePrintAddon(string Label, decimal Amount);

public sealed record ErpPurchasePrintDoc(
    ErpPurchasePrintKind Kind,
    string DocumentNumber,
    string DocumentDate,
    string PaymentTerms,
    string Salesman,
    string Currency,
    string MetalRate,
    bool Official,
    string Status,
    ErpPurchasePrintParty Issuer,
    ErpPurchasePrintParty Party,
    IReadOnlyList<ErpPurchasePrintLine> Lines,
    IReadOnlyList<ErpPurchasePrintAddon> Addons,
    decimal AmountExVat,
    decimal VatAmount,
    decimal TotalAmount,
    string Notes,
    string ProcessNote);

/// <summary>
/// Official purchase-process print (LPO → PO → PI) in the jewellery tax-invoice
/// letterhead. Generic ERP lines use qty/rate/VAT; metal columns appear when weights exist.
/// </summary>
public static class ErpPurchaseDocumentPrint
{
    public static bool IsOfficialPo(string? status)
        => status is "approved" or "partial" or "received";

    public static bool IsOfficialPi(string? status)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value is not ("draft" or "voided" or "cancelled" or "void");
    }

    public static bool IsOfficialJewellery(string? status, bool authorized)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (authorized && value is not ("voided" or "cancelled" or "void"))
        {
            return true;
        }

        return value is "posted" or "confirmed" or "authorized" or "approved";
    }

    public static string Title(ErpPurchasePrintKind kind, bool metal)
        => kind switch
        {
            ErpPurchasePrintKind.Lpo => "LOCAL PURCHASE ORDER",
            ErpPurchasePrintKind.PurchaseOrder => "PURCHASE ORDER",
            ErpPurchasePrintKind.MetalPurchase => metal
                ? "TAX INVOICE / METAL PURCHASE (FIXED)"
                : "TAX INVOICE / METAL PURCHASE",
            _ => metal ? "TAX INVOICE / METAL PURCHASE (FIXED)" : "TAX INVOICE / PURCHASE",
        };

    public static string Render(ErpPurchasePrintDoc doc)
    {
        var metal = doc.Lines.Any(line => line.GrossWt > 0 || line.Purity > 0 || line.MetalAmount > 0);
        var title = Title(doc.Kind, metal);
        var words = ErpDocumentControlRender.AmountWordsEn(doc.TotalAmount);
        var issuer = doc.Issuer;
        var party = doc.Party;
        var html = new StringBuilder();
        html.Append("""
            <!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><title>
            """);
        html.Append(E(title)).Append(' ').Append(E(doc.DocumentNumber));
        html.Append("""
            </title>
            <style>
            @page { size: A4; margin: 10mm; }
            body { margin: 0; color: #111; }
            .epc-pi { font-family: Arial, Helvetica, sans-serif; font-size: 11px; max-width: 210mm; margin: 0 auto; padding: 8px 12px 16px; }
            .epc-pi__co { text-align: center; line-height: 1.35; }
            .epc-pi__co h1 { font-size: 17px; letter-spacing: .08em; margin: 0 0 4px; text-transform: uppercase; }
            .epc-pi__rule { border: 0; border-top: 3px solid #b91c1c; margin: 8px 0 6px; }
            .epc-pi__banner { display: table; width: 100%; margin-bottom: 8px; }
            .epc-pi__banner h2 { display: table-cell; font-size: 14px; margin: 0; letter-spacing: .04em; }
            .epc-pi__copy { display: table-cell; text-align: right; font-size: 11px; font-weight: 700; }
            .epc-pi__boxes { display: table; width: 100%; border-collapse: collapse; margin-bottom: 8px; }
            .epc-pi__box { display: table-cell; width: 50%; border: 1px solid #222; padding: 7px 8px; vertical-align: top; }
            .epc-pi__box strong { display: block; margin-bottom: 2px; }
            table.epc-pi__lines { width: 100%; border-collapse: collapse; table-layout: fixed; }
            table.epc-pi__lines th, table.epc-pi__lines td { border: 1px solid #222; padding: 3px 4px; vertical-align: top; word-wrap: break-word; }
            table.epc-pi__lines th { background: #f3f4f6; font-size: 10px; }
            .r { text-align: right; }
            .epc-pi__tot { width: 46%; margin-left: auto; border-collapse: collapse; margin-top: 6px; }
            .epc-pi__tot td { border: 1px solid #222; padding: 3px 6px; }
            .epc-pi__words { margin: 10px 0 4px; }
            .epc-pi__sign { display: table; width: 100%; margin-top: 28px; }
            .epc-pi__sign span { display: table-cell; width: 33%; text-align: center; border-top: 1px solid #222; padding-top: 6px; font-size: 10px; }
            .epc-pi__draft { position: fixed; inset: 28% 8%; text-align: center; font-size: 64px; color: rgba(185,28,28,.18); transform: rotate(-18deg); pointer-events: none; font-weight: 800; letter-spacing: .2em; }
            .epc-pi__note { background: #fef3c7; border: 1px solid #d97706; padding: 6px 8px; margin-bottom: 8px; }
            .no-print { margin-bottom: 10px; }
            @media print { .no-print { display: none !important; } .epc-pi { padding: 0; } }
            </style></head><body>
            <div class="no-print">
            <button type="button" onclick="window.print()">Print / Save as PDF</button>
            <button type="button" onclick="window.close()">Close</button>
            </div>
            <div class="epc-pi" data-kind="
            """);
        html.Append(doc.Kind.ToString().ToLowerInvariant())
            .Append("\" data-official=\"").Append(doc.Official ? "1" : "0")
            .Append("\" data-status=\"").Append(E(doc.Status)).Append("\">");
        if (!doc.Official)
        {
            html.Append("<div class=\"epc-pi__draft\">DRAFT</div>");
            html.Append("<div class=\"epc-pi__note\">This document is not posted yet. Approve the PO or confirm the purchase invoice to print the official copy.</div>");
        }

        var taxInvoice = doc.Kind is ErpPurchasePrintKind.PurchaseInvoice or ErpPurchasePrintKind.MetalPurchase;
        html.Append("<div class=\"epc-pi__co\"><h1>").Append(E(Blank(issuer.Name, "ECOM AE"))).Append("</h1>");
        if (taxInvoice)
        {
            html.Append("<div>Buyer. Address: ").Append(E(Blank(issuer.Address))).Append("</div>");
        }
        else if (issuer.Address.Length > 0)
        {
            html.Append("<div>").Append(E(issuer.Address)).Append("</div>");
        }

        var contact = Join(" | ",
            issuer.Phone.Length > 0 ? "Tel: " + issuer.Phone : "",
            issuer.Email.Length > 0 ? "Email: " + issuer.Email : "",
            issuer.Trn.Length > 0 ? "TRN: " + issuer.Trn : "");
        if (contact.Length > 0)
        {
            html.Append("<div>").Append(E(contact)).Append("</div>");
        }

        html.Append("</div><hr class=\"epc-pi__rule\" />");
        html.Append("<div class=\"epc-pi__banner\"><h2>").Append(E(title))
            .Append("</h2><div class=\"epc-pi__copy\">ACCOUNT'S COPY</div></div>");

        html.Append("<div class=\"epc-pi__boxes\"><div class=\"epc-pi__box\">");
        if (taxInvoice)
        {
            html.Append("<div>Supplier</div>");
        }

        html.Append("<strong>").Append(E(Join(" - ", party.Code, party.Name))).Append("</strong>");
        if (party.Address.Length > 0)
        {
            html.Append("<div>").Append(E(party.Address)).Append("</div>");
        }

        if (party.Phone.Length > 0)
        {
            html.Append("<div>Tel: ").Append(E(party.Phone)).Append("</div>");
        }

        if (party.Email.Length > 0)
        {
            html.Append("<div>Email: ").Append(E(party.Email)).Append("</div>");
        }

        if (party.Trn.Length > 0)
        {
            html.Append("<div>TRN: ").Append(E(party.Trn)).Append("</div>");
        }

        html.Append("</div><div class=\"epc-pi__box\">");
        Meta(html, doc.Kind == ErpPurchasePrintKind.Lpo ? "LPO NO" : doc.Kind == ErpPurchasePrintKind.PurchaseOrder ? "PO NO" : "PUR NO", doc.DocumentNumber);
        Meta(html, "Date", doc.DocumentDate);
        Meta(html, "Payment terms", doc.PaymentTerms);
        Meta(html, "Salesman / buyer", doc.Salesman);
        if (doc.MetalRate.Length > 0)
        {
            Meta(html, "Metal rate", doc.MetalRate);
        }

        Meta(html, "Currency", string.IsNullOrWhiteSpace(doc.Currency) ? "AED" : doc.Currency);
        html.Append("</div></div>");

        html.Append("<table class=\"epc-pi__lines\"><thead><tr>");
        html.Append("<th style=\"width:4%\">No</th><th>Description</th>");
        if (metal)
        {
            html.Append("<th class=\"r\">Gross wt</th><th class=\"r\">Purity</th><th class=\"r\">Pure wt</th>");
            html.Append("<th class=\"r\">Making</th><th class=\"r\">Metal amt</th>");
        }
        else
        {
            html.Append("<th class=\"r\">Qty</th><th class=\"r\">Rate</th>");
        }

        html.Append("<th class=\"r\">Net (ex VAT)</th><th class=\"r\">VAT %</th><th class=\"r\">VAT</th><th class=\"r\">Gross</th>");
        html.Append("</tr></thead><tbody>");
        var n = 0;
        foreach (var line in doc.Lines)
        {
            n++;
            html.Append("<tr><td class=\"r\">").Append(line.Number > 0 ? line.Number : n).Append("</td><td>")
                .Append(E(Blank(line.Description))).Append("</td>");
            if (metal)
            {
                html.Append("<td class=\"r\">").Append(Q(line.GrossWt > 0 ? line.GrossWt : line.Qty)).Append("</td>");
                html.Append("<td class=\"r\">").Append(P(line.Purity)).Append("</td>");
                html.Append("<td class=\"r\">").Append(Q(line.PureWt)).Append("</td>");
                html.Append("<td class=\"r\">").Append(M(line.MakingAmount)).Append("</td>");
                html.Append("<td class=\"r\">").Append(M(line.MetalAmount > 0 ? line.MetalAmount : line.AmountExVat)).Append("</td>");
            }
            else
            {
                html.Append("<td class=\"r\">").Append(Q(line.Qty)).Append("</td>");
                html.Append("<td class=\"r\">").Append(M(line.Rate)).Append("</td>");
            }

            html.Append("<td class=\"r\">").Append(M(line.AmountExVat)).Append("</td>");
            html.Append("<td class=\"r\">").Append(P(line.VatRate)).Append("</td>");
            html.Append("<td class=\"r\">").Append(M(line.VatAmount)).Append("</td>");
            html.Append("<td class=\"r\">").Append(M(line.AmountInclVat)).Append("</td></tr>");
        }

        if (doc.Lines.Count == 0)
        {
            html.Append("<tr><td colspan=\"").Append(metal ? 11 : 8).Append("\">No lines.</td></tr>");
        }

        html.Append("</tbody></table>");
        html.Append("<table class=\"epc-pi__tot\"><tbody>");
        html.Append("<tr><td>").Append(doc.Lines.Count.ToString(CultureInfo.InvariantCulture)).Append(" item(s)</td><td class=\"r\">")
            .Append(M(doc.AmountExVat)).Append("</td></tr>");
        foreach (var addon in doc.Addons)
        {
            html.Append("<tr><td>").Append(E(addon.Label)).Append("</td><td class=\"r\">").Append(M(addon.Amount)).Append("</td></tr>");
        }

        html.Append("<tr><td>VAT</td><td class=\"r\">").Append(M(doc.VatAmount)).Append("</td></tr>");
        html.Append("<tr><th>Total invoice amount</th><th class=\"r\">").Append(M(doc.TotalAmount)).Append("</th></tr>");
        html.Append("</tbody></table>");
        html.Append("<p class=\"epc-pi__words\"><strong>Amount in words:</strong> ").Append(E(words)).Append("</p>");
        if (taxInvoice)
        {
            var endpoint = ErpEinvoiceLaw.Endpoint(issuer.Trn);
            if (endpoint.Length == 0)
            {
                endpoint = "0235:";
            }

            var currency = string.IsNullOrWhiteSpace(doc.Currency) ? "AED" : doc.Currency;
            html.Append("<p>Invoice number ").Append(E(doc.DocumentNumber))
                .Append(". Issue date ").Append(E(doc.DocumentDate))
                .Append(". Supply date ").Append(E(doc.DocumentDate))
                .Append(". Payment due ").Append(E(Blank(doc.PaymentTerms, "on receipt")))
                .Append(". Currency ").Append(E(currency))
                .Append(". Discount 0.00. Quantity is the qty or the weight. Unit price is the rate. Tax amount in AED ")
                .Append(M(doc.VatAmount)).Append(" AED. Exchange rate 1.</p>");
            html.Append("<p>Legal registration —. Authority Federal Tax Authority. Electronic address ")
                .Append(E(endpoint))
                .Append(". UUID assigned when the electronic invoice is sent. Preceding invoice reference none. No QR code.</p>");
        }
        if (!string.IsNullOrWhiteSpace(doc.Notes))
        {
            html.Append("<p>").Append(E(doc.Notes).Replace("\n", "<br>", StringComparison.Ordinal)).Append("</p>");
        }

        if (!string.IsNullOrWhiteSpace(doc.ProcessNote))
        {
            html.Append("<p class=\"muted\">").Append(E(doc.ProcessNote)).Append("</p>");
        }

        html.Append("<p style=\"margin-top:18px\">Confirmed on behalf of ").Append(E(Blank(issuer.Name, "ECOM AE")))
            .Append(" / ").Append(E(Blank(party.Name, "the supplier"))).Append(".</p>");
        html.Append("<div class=\"epc-pi__sign\"><span>PARTY'S SIGNATURE</span><span>CHECKED BY</span><span>AUTHORISED SIGNATORY</span></div>");
        html.Append("</div></body></html>");
        return html.ToString();
    }

    private static void Meta(StringBuilder html, string label, string value)
        => html.Append("<div><strong>").Append(E(label)).Append(":</strong> ").Append(E(Blank(value))).Append("</div>");

    private static string Blank(string? value, string fallback = "—")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Join(string sep, params string[] parts)
        => string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string M(decimal value)
        => value.ToString("N2", CultureInfo.InvariantCulture);

    private static string Q(decimal value)
        => value.ToString("N3", CultureInfo.InvariantCulture);

    private static string P(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);
}
