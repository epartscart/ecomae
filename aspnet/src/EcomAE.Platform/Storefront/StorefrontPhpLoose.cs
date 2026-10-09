using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP 8 loose-typing helpers for values decoded by <c>json_decode($s, true)</c>. A <see langword="null"/> element is
/// an absent key, which PHP reads as <c>null</c>.
/// </summary>
internal static partial class StorefrontPhpLoose
{
    [GeneratedRegex(@"^[ \t\n\r\v\f]*[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?[ \t\n\r\v\f]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericString();

    [GeneratedRegex(@"^[ \t\n\r\v\f]*[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumber();

    public static JsonElement? Get(JsonElement obj, string key)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null
            ? value
            : null;

    public static bool IsNull(JsonElement? value)
        => value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    public static bool Truthy(JsonElement? value)
    {
        if (IsNull(value))
        {
            return false;
        }

        var element = value!.Value;
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.GetDouble() != 0d,
            JsonValueKind.String => element.GetString() is { Length: > 0 } text && !string.Equals(text, "0", StringComparison.Ordinal),
            JsonValueKind.Array => element.GetArrayLength() > 0,
            JsonValueKind.Object => element.EnumerateObject().Any(),
            _ => false
        };
    }

    public static bool Equal(JsonElement? left, JsonElement? right) => Compare(left, right) == 0;

    public static bool Equal(JsonElement? left, double number)
        => Compare(left, JsonSerializer.SerializeToElement(number)) == 0;

    public static bool EqualsString(JsonElement? left, string text)
        => Compare(left, JsonSerializer.SerializeToElement(text)) == 0;

    /// <summary>PHP 8 <c>&lt;=&gt;</c> for decoded JSON scalars (arrays compare greater than any scalar).</summary>
    public static int Compare(JsonElement? left, JsonElement? right)
    {
        var leftNull = IsNull(left);
        var rightNull = IsNull(right);
        if (leftNull && rightNull)
        {
            return 0;
        }

        if (leftNull && right!.Value.ValueKind == JsonValueKind.String)
        {
            return Math.Sign(string.CompareOrdinal(string.Empty, right.Value.GetString()));
        }

        if (rightNull && left!.Value.ValueKind == JsonValueKind.String)
        {
            return Math.Sign(string.CompareOrdinal(left.Value.GetString(), string.Empty));
        }

        if (leftNull || rightNull || IsBool(left!.Value) || IsBool(right!.Value))
        {
            return Truthy(left).CompareTo(Truthy(right));
        }

        var a = left!.Value;
        var b = right!.Value;
        if (a.ValueKind is JsonValueKind.Array or JsonValueKind.Object || b.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            if (a.ValueKind is JsonValueKind.Array or JsonValueKind.Object && b.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                return ArrayCount(a).CompareTo(ArrayCount(b));
            }

            return a.ValueKind is JsonValueKind.Array or JsonValueKind.Object ? 1 : -1;
        }

        if (a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number)
        {
            return a.GetDouble().CompareTo(b.GetDouble());
        }

        if (a.ValueKind == JsonValueKind.Number)
        {
            return CompareNumberToString(a, b.GetString() ?? string.Empty);
        }

        if (b.ValueKind == JsonValueKind.Number)
        {
            return -CompareNumberToString(b, a.GetString() ?? string.Empty);
        }

        var leftText = a.GetString() ?? string.Empty;
        var rightText = b.GetString() ?? string.Empty;
        if (TryNumeric(leftText, out var leftNumber) && TryNumeric(rightText, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return Math.Sign(string.CompareOrdinal(leftText, rightText));
    }

    /// <summary>PHP <c>floatval()</c>.</summary>
    public static double Floatval(JsonElement? value)
    {
        if (IsNull(value))
        {
            return 0d;
        }

        var element = value!.Value;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.GetDouble();
            case JsonValueKind.True:
                return 1d;
            case JsonValueKind.String:
                var match = LeadingNumber().Match(element.GetString() ?? string.Empty);
                return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
            case JsonValueKind.Array:
            case JsonValueKind.Object:
                return Truthy(value) ? 1d : 0d;
            default:
                return 0d;
        }
    }

    /// <summary>A float interpolated into a SQL string by PHP (<c>precision=14</c>).</summary>
    public static string FloatSql(double value)
        => value.ToString("G14", CultureInfo.InvariantCulture);

    public static string FloatSql(string? databaseDecimal)
        => double.TryParse(databaseDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? FloatSql(parsed) : "0";

    private static bool IsBool(JsonElement element) => element.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static int ArrayCount(JsonElement element)
        => element.ValueKind == JsonValueKind.Array ? element.GetArrayLength() : element.EnumerateObject().Count();

    internal static bool TryNumeric(string text, out double value)
    {
        value = 0d;
        return NumericString().IsMatch(text)
            && double.TryParse(text.Trim(' ', '\t', '\n', '\r', '\v', '\f'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static int CompareNumberToString(JsonElement number, string text)
    {
        if (TryNumeric(text, out var parsed))
        {
            return number.GetDouble().CompareTo(parsed);
        }

        return Math.Sign(string.CompareOrdinal(number.GetRawText(), text));
    }
}
