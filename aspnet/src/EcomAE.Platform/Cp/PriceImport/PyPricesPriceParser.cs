using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>
/// Port of the <c>price_parser</c> package used by pyprices <c>PriceRecord</c>
/// (<c>Price.fromstring(str(value)).amount</c>): <c>extract_price_text</c>, <c>get_decimal_separator</c>
/// and <c>parse_number</c>, so supplier price cells such as <c>1 234,56</c>, <c>1.234,56</c>,
/// <c>AED 12.50</c> or <c>35€99</c> resolve to the same amount on both runtimes.
/// </summary>
public static class PyPricesPriceParser
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    private static readonly Regex EuroPrice = new(
        @"[\d\s.,']*?\d\s*?€(\s*?)?\d(?(1)\d|\d*?)(?:$|[^\d])",
        RegexOptions.CultureInvariant);

    private static readonly Regex NumberText = new(
        @"([.]?\d[\d\s.,']*)\s*?(?:[^%\d]|$)",
        RegexOptions.CultureInvariant);

    private static readonly Regex DecimalSeparator = new(
        @"\d*([.,€])(?:\d{1,2}?|\d{4}\d*?)$",
        RegexOptions.CultureInvariant);

    /// <summary><c>Price.fromstring(text).amount</c>; <c>null</c> when no amount can be read.</summary>
    public static decimal? Parse(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var amountText = ExtractPriceText(text);
        return amountText is null ? null : ParseNumber(amountText);
    }

    public static string? ExtractPriceText(string price)
    {
        price = Whitespace.Replace(price, " ");

        if (CountOf(price, '€') == 1)
        {
            var euro = EuroPrice.Match(price);
            if (euro.Success)
            {
                return euro.Value.Replace(" ", string.Empty, StringComparison.Ordinal);
            }
        }

        var match = NumberText.Match(price);
        if (match.Success)
        {
            var priceText = match.Groups[1].Value.TrimEnd(',', '.');
            priceText = priceText.Replace("'", string.Empty, StringComparison.Ordinal);
            return CountOf(priceText, '.') == 1
                ? priceText.Trim()
                : priceText.TrimStart(',', '.').Trim();
        }

        return price.Contains("free", StringComparison.OrdinalIgnoreCase) ? "0" : null;
    }

    public static string? GetDecimalSeparator(string price)
    {
        var match = DecimalSeparator.Match(price);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static decimal? ParseNumber(string? num, string? decimalSeparator = null)
    {
        if (string.IsNullOrEmpty(num))
        {
            return null;
        }

        num = num.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        decimalSeparator ??= GetDecimalSeparator(num);
        num = decimalSeparator switch
        {
            null => num.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal),
            "." => num.Replace(",", string.Empty, StringComparison.Ordinal),
            "," => num.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal),
            _ => num.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal).Replace("€", ".", StringComparison.Ordinal),
        };

        return PythonDecimal(num);
    }

    /// <summary>Python <c>Decimal(str)</c> for the digit/point strings <see cref="ParseNumber"/> produces (Unicode digits included).</summary>
    private static decimal? PythonDecimal(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var ascii = new StringBuilder(text.Length);
        var points = 0;
        var digits = 0;
        foreach (var ch in text)
        {
            if (ch == '.')
            {
                points++;
                ascii.Append('.');
                continue;
            }

            if (!char.IsDigit(ch))
            {
                return null;
            }

            digits++;
            ascii.Append((char)('0' + (int)char.GetNumericValue(ch)));
        }

        if (points > 1 || digits == 0)
        {
            return null;
        }

        return decimal.TryParse(ascii.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int CountOf(string text, char ch)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (c == ch)
            {
                count++;
            }
        }

        return count;
    }
}
