using System.Net;

namespace EcomAE.Platform.Erp;

public sealed record ErpDocumentKind(string Code, string DocType, string Title, string Family, string Purpose);

public sealed record ErpDocumentFinding(string Label, bool Met);

public sealed record ErpDocumentParts(
    string DocType,
    string Name,
    string HeaderHtml,
    string FooterHtml,
    string TermsHtml,
    string BankHtml,
    string BodyColumns);

/// <summary>
/// Readable copies of invoices, vouchers, and posted results.
/// A tax invoice is checked against the VAT tax-invoice fields and the UAE electronic invoice.
/// </summary>
public static class ErpDocumentDesign
{
    private sealed record Mark(string Label, string[] Needles);

    public static IReadOnlyList<ErpDocumentKind> Kinds { get; } =
    [
        new("invoice", "invoice", "Tax invoice", "Invoice", "VAT tax invoice and the readable copy of the electronic invoice."),
        new("credit_note", "credit_note", "Credit note", "Invoice", "Reduces a tax invoice and names the preceding invoice."),
        new("receipt", "receipt_voucher", "Payment receipt", "Voucher", "Records money received. It is not a tax invoice."),
        new("delivery_note", "delivery_note", "Delivery note", "Voucher", "Proof of delivery. It is not a tax invoice."),
        new("packing_slip", "packing_slip", "Packing slip", "Voucher", "What was packed. It is not a tax invoice."),
        new("payment_voucher", "payment_voucher", "Payment voucher", "Voucher", "Records money paid, with the account and the amount."),
        new("receipt_voucher", "receipt_voucher", "Receipt voucher", "Voucher", "Records money received, with the account and the amount."),
        new("journal_voucher", "journal_voucher", "Journal voucher", "Voucher", "Debit and credit lines before or at posting."),
        new("posted_journal", "posted_journal", "Posted journal", "Report", "The posted voucher: accounts, debit, credit, and the balanced total."),
        new("posted_report", "posted_report", "Posted trial balance", "Report", "Posted journals only, with debit and credit by account."),
        new("jw_purchase", "jw_purchase", "Jewellery metal / diamond purchase", "Voucher", "Jewellery purchase voucher with weights, purity and VAT."),
        new("jw_fixing", "jw_fixing", "Jewellery fixing", "Voucher", "Metal purchase or sale fixing: pure weight, rate and amount."),
        new("jw_repair", "jw_repair", "Jewellery repair receipt", "Voucher", "Repair intake receipt with bag, stock code and weights."),
    ];

    public static ErpDocumentKind Kind(string? code)
    {
        var key = (code ?? string.Empty).Trim();
        return Kinds.FirstOrDefault(item => item.Code.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? Kinds[0];
    }

    public static ErpDocumentParts Parts(string? code)
    {
        var kind = Kind(code);
        var columns = kind.Family == "Invoice"
            ? InvoiceColumns
            : kind.Code is "posted_journal" or "posted_report" or "journal_voucher"
                ? JournalColumns
                : VoucherColumns;
        return new ErpDocumentParts(
            kind.DocType,
            kind.Title,
            Header(kind),
            "<p>Retain this document. A taxable person keeps tax records for 5 years after the tax period. Real estate records are kept for 7 years from the end of the calendar year.</p>",
            kind.Family == "Invoice"
                ? "<p>Payment terms: due on the payment due date. Discount is stated on the invoice.</p>"
                : "<p>This paper supports the posted books. It is not a tax invoice.</p>",
            kind.Family == "Invoice"
                ? "<p>Bank {{company_name}} · IBAN on the company profile.</p>"
                : string.Empty,
            columns);
    }

    public static string SampleHtml(string? code)
    {
        var kind = Kind(code);
        var parts = Parts(code);
        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>"
            + WebUtility.HtmlEncode(kind.Title)
            + "</title><style>body{font-family:Arial,sans-serif;font-size:12px;color:#111;margin:16px;}table{width:100%;border-collapse:collapse;margin:8px 0;}th,td{border:1px solid #ccc;padding:4px 6px;text-align:left;}th{background:#f4f4f4;}</style></head><body>"
            + parts.HeaderHtml
            + parts.TermsHtml
            + parts.BankHtml
            + parts.FooterHtml
            + "</body></html>";
    }

    public static IReadOnlyList<ErpDocumentFinding> Check(string? code, string? html)
    {
        var text = html ?? string.Empty;
        return Marks(Kind(code)).Select(mark => new ErpDocumentFinding(
            mark.Label,
            mark.Needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase)))).ToList();
    }

    public static bool Passes(string? code, string? html)
        => Check(code, html).All(item => item.Met);

    private static IReadOnlyList<Mark> Marks(ErpDocumentKind kind)
    {
        if (kind.Family == "Invoice")
        {
            var marks = new List<Mark>(InvoiceMarks);
            if (kind.Code == "credit_note")
            {
                marks.Insert(0, new("Credit note", ["credit note"]));
            }

            return marks;
        }

        if (kind.Code is "posted_journal" or "journal_voucher")
        {
            return
            [
                new(kind.Title, [kind.Title]),
                new("Number", ["number", "no:"]),
                new("Date", ["date"]),
                new("Account", ["account"]),
                new("Debit", ["debit"]),
                new("Credit", ["credit"]),
                new("Posted", ["posted"]),
                new("Not a tax invoice", ["not a tax invoice"]),
            ];
        }

        if (kind.Code == "posted_report")
        {
            return
            [
                new("Trial balance", ["trial balance"]),
                new("Posted", ["posted"]),
                new("Account", ["account"]),
                new("Debit", ["debit"]),
                new("Credit", ["credit"]),
                new("Period", ["period"]),
                new("Not a tax invoice", ["not a tax invoice"]),
            ];
        }

        return
        [
            new(kind.Title, [kind.Title]),
            new("Number", ["number", "no:"]),
            new("Date", ["date"]),
            new("Party", ["customer", "supplier", "received from", "paid to", "deliver to"]),
            new("Amount", ["amount", "quantity"]),
            new("Not a tax invoice", ["not a tax invoice"]),
        ];
    }

    private static readonly Mark[] InvoiceMarks =
    [
        new("Tax Invoice", ["tax invoice"]),
        new("Supplier", ["supplier", "seller"]),
        new("Buyer", ["buyer", "bill to"]),
        new("TRN", ["trn"]),
        new("Address", ["address"]),
        new("Invoice number", ["invoice number", "no:"]),
        new("Issue date", ["issue date"]),
        new("Supply date", ["supply date", "supply / vat"]),
        new("Description", ["description"]),
        new("Quantity", ["quantity", "qty"]),
        new("Unit price", ["unit price"]),
        new("Tax rate", ["tax rate", "vat %"]),
        new("Discount", ["discount"]),
        new("VAT amount", ["vat amount", "vat"]),
        new("Total", ["total"]),
        new("AED", ["aed"]),
        new("Exchange rate", ["exchange rate"]),
        new("UUID", ["uuid"]),
        new("Legal registration", ["legal registration", "legal reg"]),
        new("Authority", ["authority"]),
        new("Electronic address", ["0235"]),
        new("Currency", ["currency"]),
        new("Payment due", ["payment due", "due:"]),
        new("Preceding invoice", ["preceding invoice"]),
        new("No QR code", ["no qr code"]),
    ];

    private const string InvoiceColumns =
        """[{"key":"description","label":"Description","width":"34","align":"left"},{"key":"qty","label":"Quantity","align":"right"},{"key":"unit_price","label":"Unit price","align":"right"},{"key":"tax_rate","label":"Tax rate","align":"right"},{"key":"discount","label":"Discount","align":"right"},{"key":"vat","label":"VAT amount","align":"right"},{"key":"total","label":"Total","align":"right"}]""";

    private const string JournalColumns =
        """[{"key":"account","label":"Account","width":"40","align":"left"},{"key":"debit","label":"Debit","align":"right"},{"key":"credit","label":"Credit","align":"right"}]""";

    private const string VoucherColumns =
        """[{"key":"account","label":"Account","width":"40","align":"left"},{"key":"amount","label":"Amount","align":"right"}]""";

    private static string Header(ErpDocumentKind kind)
        => kind.Family == "Invoice" ? InvoiceHeader(kind) : OtherHeader(kind);

    private static string InvoiceHeader(ErpDocumentKind kind)
    {
        var title = kind.Code == "credit_note" ? "Credit note · Tax Invoice" : "Tax Invoice";
        return "<h1>" + title + "</h1>"
            + "<p>Supplier {{company_name}}. Address {{address}}. TRN {{trn}}. Legal registration TL-000. Authority Federal Tax Authority. Electronic address 0235:1000000000.</p>"
            + "<p>Buyer {{customer_name}}. Address {{address}}. TRN {{trn}}.</p>"
            + "<p>Invoice number {{doc_no}}. Issue date {{doc_date}}. Supply date {{doc_date}}. Payment due {{due_date}}. Currency {{currency}}.</p>"
            + "<p>UUID {{doc_no}}. Preceding invoice reference " + (kind.Code == "credit_note" ? "{{doc_no}}" : "none") + ". No QR code. The electronic invoice is the XML file.</p>"
            + "<table><thead><tr><th>Description</th><th>Quantity</th><th>Unit price</th><th>Tax rate</th><th>Discount</th><th>VAT amount</th><th>Total</th></tr></thead>"
            + "<tbody><tr><td>Sample line</td><td>1</td><td>100.00</td><td>5</td><td>0.00</td><td>5.00</td><td>105.00</td></tr></tbody></table>"
            + "<p>Discount 0.00. VAT amount 5.00 AED. Exchange rate 1. Total 105.00 AED.</p>";
    }

    private static string OtherHeader(ErpDocumentKind kind)
    {
        var party = kind.Code switch
        {
            "payment_voucher" => "Paid to {{supplier_name}}",
            "delivery_note" => "Deliver to {{customer_name}}",
            "packing_slip" => "Customer {{customer_name}}",
            _ => "Received from {{customer_name}}",
        };
        if (kind.Code == "posted_report")
        {
            return "<h1>Posted trial balance</h1>"
                + "<p>Period {{doc_date}}. This report uses posted journals only. Not a tax invoice.</p>"
                + "<table><thead><tr><th>Account</th><th>Debit</th><th>Credit</th></tr></thead>"
                + "<tbody><tr><td>Sales</td><td>0.00</td><td>105.00</td></tr><tr><td>Receivable</td><td>105.00</td><td>0.00</td></tr></tbody></table>"
                + "<p>Debit 105.00. Credit 105.00. Printed {{printed_date}}.</p>";
        }

        if (kind.Code is "posted_journal" or "journal_voucher")
        {
            return "<h1>" + kind.Title + "</h1>"
                + "<p>Number {{doc_no}}. Date {{doc_date}}. Posted. Not a tax invoice.</p>"
                + "<table><thead><tr><th>Account</th><th>Debit</th><th>Credit</th></tr></thead>"
                + "<tbody><tr><td>Receivable</td><td>105.00</td><td>0.00</td></tr><tr><td>Sales</td><td>0.00</td><td>105.00</td></tr></tbody></table>"
                + "<p>Debit 105.00. Credit 105.00.</p>";
        }

        return "<h1>" + kind.Title + "</h1>"
            + "<p>Number {{doc_no}}. Date {{doc_date}}. " + party + ". Not a tax invoice.</p>"
            + "<p>Amount {{total}} {{currency}}.</p>";
    }
}
