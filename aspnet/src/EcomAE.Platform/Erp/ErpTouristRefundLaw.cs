using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// UAE Tax Refunds for Tourists Scheme. Planet is the operator.
/// Federal Tax Authority Decision No. 2 of 2018, as amended by Decision No. 9 of 2023,
/// Decision No. 2 of 2024, and Decision No. 11 of 2026 (effective 12 July 2026).
/// The electronic invoice file stays without a QR code. The Planet tag is a separate barcode on the tax invoice.
/// </summary>
public static class ErpTouristRefundLaw
{
    public const string Decision = "Federal Tax Authority Decision No. 2 of 2018";
    public const string Amendment = "Federal Tax Authority Decision No. 11 of 2026 (effective 12 July 2026)";
    public const string Operator = "Planet";
    public const decimal VatRate = 5m;
    public const decimal AdminFeeRate = 0.13m;
    public const decimal TouristShare = 0.87m;
    public const decimal FixedFeeAed = 3.60m;
    public const decimal LegalMinimumInclusive = 250m;
    public const decimal OperatorMinimumExclusive = 250m;
    public const decimal CashCapAed = 35_000m;
    public const int ExportDays = 90;
    public const int PlanetInvoiceCycleDays = 15;
    public const int PlanetPaymentDays = 15;
    public const string SampleBarcode = "TR20261017000001";
    public const string LegislationRef = "FTA-TR-2-2018";

    public static ErpTouristRefundQuote Calculate(decimal vatAmount)
    {
        var vat = Round2(vatAmount < 0 ? 0 : vatAmount);
        var refund = Round2(Math.Max(0, (vat * TouristShare) - FixedFeeAed));
        var retained = Round2(vat - refund);
        return new ErpTouristRefundQuote(vat, refund, FixedFeeAed, retained);
    }

    /// <summary>Blank when the claim can be tagged. Otherwise the reason it cannot.</summary>
    public static string? Refusal(ErpTouristRefundClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (string.IsNullOrWhiteSpace(claim.PassportNo))
        {
            return "Passport or GCC national ID is required. The name on the tax invoice must match it.";
        }

        if (IsUaeResident(claim.Nationality))
        {
            return "A UAE resident is outside the Tourist Refund Scheme.";
        }

        var kind = (claim.SupplyKind ?? string.Empty).Trim().ToLowerInvariant();
        if (kind is "service" or "services")
        {
            return "Services are outside the Tourist Refund Scheme. The scheme refunds goods the tourist exports.";
        }

        if (kind is "courier" or "couriered")
        {
            return "Goods sent by courier are outside the scheme. The tourist exports the goods in person.";
        }

        var total = Round2(claim.TotalInclusive < 0 ? 0 : claim.TotalInclusive);
        var vat = Round2(claim.VatAmount < 0 ? 0 : claim.VatAmount);
        if (vat <= 0 && total > 0)
        {
            vat = Round2(total - (total / 1.05m));
        }

        var exclusive = Round2(total - vat);
        if (total < LegalMinimumInclusive)
        {
            return "Article 5 requires tax-inclusive purchases of AED 250 or more from the same taxable person.";
        }

        if (exclusive < OperatorMinimumExclusive)
        {
            return "Planet issues a tax-free tag when the purchase is at least AED 250 before VAT. Article 5 still sets the legal floor at AED 250 tax-inclusive.";
        }

        var quote = Calculate(vat);
        var method = (claim.RefundMethod ?? string.Empty).Trim().ToLowerInvariant();
        if (method is "cash" or "cash refund")
        {
            var already = claim.CashRefundedInDay < 0 ? 0 : claim.CashRefundedInDay;
            if (already + quote.Refund > CashCapAed)
            {
                return "Cash refunds are limited to AED 35,000 per overseas tourist per 24 hours. Use a card for the rest.";
            }
        }

        if (claim.SupplyDate is not null && claim.DepartureDate is not null
            && claim.DepartureDate.Value > claim.SupplyDate.Value.AddDays(ExportDays))
        {
            return "The goods must be exported within 90 days of the supply date.";
        }

        return null;
    }

    public static bool IsUaeResident(string? nationality)
    {
        var key = (nationality ?? string.Empty).Trim().ToUpperInvariant().Replace(".", "", StringComparison.Ordinal);
        return key is "AE" or "UAE" or "UNITED ARAB EMIRATES" or "EMIRATI" or "UAE RESIDENT";
    }

    public static string Money(decimal amount)
        => amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string BarcodeBlock(string barcode)
    {
        var text = (barcode ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var svg = BarcodeSvg(text);
        return "<figure class=\"epc-tr-barcode\">" + svg
            + "<figcaption>" + Encode(text) + "</figcaption></figure>";
    }

    public static string InvoiceTagHtml(string barcode, decimal vatAmount)
    {
        var text = (barcode ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var quote = Calculate(vatAmount);
        var html = new StringBuilder();
        html.Append("<div class=\"planet-tag\" style=\"margin-top:18px;padding:12px 14px;border:1px dashed #1f4e79;background:#f8fbff;\">");
        html.Append("<strong style=\"color:#1f4e79;\">Planet tax-free tag</strong>");
        html.Append("<p style=\"margin:6px 0 8px;font-size:12px;color:#334155;\">Affix this tag to the back of the tax invoice, or issue the same barcode on the digital tax-free transaction. ");
        html.Append("Tourist refund under ").Append(Encode(Decision)).Append(", as amended by ").Append(Encode(Amendment)).Append(". ");
        html.Append("The tourist receives AED ").Append(Money(quote.Refund));
        html.Append(" (87% of the VAT minus AED 3.60). Export the goods within 90 days. ");
        html.Append("The electronic invoice file has no QR code. This barcode is the Planet tag only.</p>");
        html.Append(BarcodeBlock(text));
        html.Append("</div>");
        return html.ToString();
    }

    public static string BarcodeSvg(string barcode)
    {
        var bits = EncodeCode128B(barcode);
        const int module = 2;
        const int quiet = 10;
        var width = ((quiet * 2) + bits.Length) * module;
        const int height = 64;
        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"epc-tr-barcode-svg\" role=\"img\" aria-label=\"Planet tax-free tag ");
        sb.Append(Encode(barcode));
        sb.Append("\" width=\"").Append(width.ToString(CultureInfo.InvariantCulture));
        sb.Append("\" height=\"").Append(height.ToString(CultureInfo.InvariantCulture));
        sb.Append("\" viewBox=\"0 0 ").Append(width.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(height.ToString(CultureInfo.InvariantCulture)).Append("\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");
        var x = quiet * module;
        foreach (var bit in bits)
        {
            if (bit == '1')
            {
                sb.Append("<rect x=\"").Append(x.ToString(CultureInfo.InvariantCulture));
                sb.Append("\" y=\"4\" width=\"").Append(module.ToString(CultureInfo.InvariantCulture));
                sb.Append("\" height=\"46\" fill=\"#1d2740\"/>");
            }

            x += module;
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    public static string EncodeCode128B(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var values = new List<int> { 104 };
        foreach (var ch in text)
        {
            if (ch < 32 || ch > 126)
            {
                throw new ArgumentException("Planet tag barcode uses printable characters.", nameof(value));
            }

            values.Add(ch - 32);
        }

        var sum = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            sum += i * values[i];
        }

        values.Add(sum % 103);
        var bits = new StringBuilder();
        foreach (var code in values)
        {
            bits.Append(PatternBits(code));
        }

        bits.Append(PatternBits(106));
        return bits.ToString();
    }

    private static string PatternBits(int code)
    {
        var widths = Widths[code];
        var bits = new StringBuilder(code == 106 ? 13 : 11);
        var bar = true;
        foreach (var ch in widths)
        {
            var n = ch - '0';
            bits.Append(bar ? new string('1', n) : new string('0', n));
            bar = !bar;
        }

        return bits.ToString();
    }

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Encode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    /// <summary>Code 128 width patterns, values 0–106. 106 is the stop symbol.</summary>
    private static readonly string[] Widths =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112",
    ];
}

public sealed record ErpTouristRefundQuote(decimal Vat, decimal Refund, decimal FixedFee, decimal Retained);

public sealed record ErpTouristRefundClaim(
    string? PassportNo,
    string? Nationality,
    decimal TotalInclusive,
    decimal VatAmount,
    string? SupplyKind = "goods",
    string? RefundMethod = "card",
    DateOnly? SupplyDate = null,
    DateOnly? DepartureDate = null,
    decimal CashRefundedInDay = 0);
