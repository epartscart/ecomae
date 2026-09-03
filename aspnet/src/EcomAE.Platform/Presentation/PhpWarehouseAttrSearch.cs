using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>epc_price_extra_search_options</c> / <c>epc_price_extra_normalize_value</c>
/// for warehouse “more info” search (<c>/en/shop/warehouse-search</c>).
/// </summary>
public static class PhpWarehouseAttrSearch
{
    public static readonly (string Key, string Label)[] Fields =
    [
        ("all", "All fields"),
        ("engine_code", "Engine code"),
        ("country_code", "Country code"),
        ("size", "Size"),
        ("cross_reference", "Cross reference"),
        ("oe_number", "OE number"),
        ("color", "Color"),
        ("weight", "Weight"),
        ("model", "Model"),
        ("year", "Year"),
        ("position", "Position"),
        ("material", "Material"),
        ("voltage", "Voltage"),
        ("other", "Other information"),
    ];

    public static string LabelFor(string? key)
    {
        var normalized = NormalizeField(key);
        foreach (var field in Fields)
        {
            if (string.Equals(field.Key, normalized, StringComparison.Ordinal))
            {
                return field.Label;
            }
        }

        return string.IsNullOrWhiteSpace(key)
            ? "All fields"
            : CultureInfoTitle(key);
    }

    public static string NormalizeField(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0 || value is "all")
        {
            return "all";
        }

        return Regex.IsMatch(value, "^[a-z0-9_]{1,48}$", RegexOptions.CultureInvariant)
            ? value
            : "all";
    }

    /// <summary>PHP <c>epc_price_extra_normalize_value</c>: uppercase, strip non-alphanumeric, max 191.</summary>
    public static string NormalizeValue(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        if (builder.Length == 0)
        {
            return string.Empty;
        }

        return builder.Length > 191 ? builder.ToString(0, 191) : builder.ToString();
    }

    private static string CultureInfoTitle(string? key)
    {
        var text = (key ?? string.Empty).Replace('_', ' ').Trim();
        return text.Length == 0 ? "All fields" : char.ToUpperInvariant(text[0]) + text[1..];
    }
}
