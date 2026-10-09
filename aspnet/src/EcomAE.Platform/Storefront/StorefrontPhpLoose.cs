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

    /// <summary>
    /// A PHP 8 arithmetic operand: <c>int</c> (<see cref="IsFloat"/> false, <see cref="Whole"/>) or <c>float</c> (<see cref="Real"/>).
    /// </summary>
    public readonly record struct PhpNumber(bool IsFloat, long Whole, double Real)
    {
        public static PhpNumber FromInt(long whole) => new(false, whole, whole);

        public static PhpNumber FromFloat(double real) => new(true, 0, real);

        public bool IsZero => IsFloat ? Real == 0d : Whole == 0;
    }

    /// <summary>
    /// A decoded JSON value used as an operand of <c>*</c> in PHP 8: <c>null</c>/<c>false</c> are 0, <c>true</c> is 1, numeric strings
    /// (surrounding whitespace allowed) and leading-numeric strings ("4abc", with a warning) convert, every other string and every array
    /// throws a <c>TypeError</c> (<see langword="false"/> here).
    /// </summary>
    public static bool TryArithmeticOperand(JsonElement? value, out PhpNumber number)
    {
        number = PhpNumber.FromInt(0);
        if (IsNull(value))
        {
            return true;
        }

        var element = value!.Value;
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                number = PhpNumber.FromInt(1);
                return true;
            case JsonValueKind.False:
                return true;
            case JsonValueKind.Number:
                var raw = element.GetRawText();
                if (raw.AsSpan().IndexOfAny('.', 'e', 'E') < 0 && long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
                {
                    number = PhpNumber.FromInt(whole);
                    return true;
                }

                number = PhpNumber.FromFloat(element.GetDouble());
                return true;
            case JsonValueKind.String:
                var match = LeadingNumber().Match(element.GetString() ?? string.Empty);
                if (!match.Success)
                {
                    return false;
                }

                var text = match.Value.Trim(' ', '\t', '\n', '\r', '\v', '\f');
                if (text.AsSpan().IndexOfAny('.', 'e', 'E') < 0 && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var textWhole))
                {
                    number = PhpNumber.FromInt(textWhole);
                    return true;
                }

                number = PhpNumber.FromFloat(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
                return true;
            default:
                return false;
        }
    }

    /// <summary>PHP <c>int * int</c> overflows into a float; any float operand gives a float.</summary>
    public static PhpNumber Multiply(PhpNumber left, PhpNumber right)
    {
        if (!left.IsFloat && !right.IsFloat)
        {
            try
            {
                return PhpNumber.FromInt(checked(left.Whole * right.Whole));
            }
            catch (OverflowException)
            {
                return PhpNumber.FromFloat((double)left.Whole * right.Whole);
            }
        }

        return PhpNumber.FromFloat((left.IsFloat ? left.Real : left.Whole) * (right.IsFloat ? right.Real : right.Whole));
    }

    /// <summary>
    /// The value PHP writes into <c>LIMIT $a, $b</c> when the number is a valid unsigned integer literal; <see langword="null"/> when the
    /// interpolated text is not one (negative, fractional, <c>1.0E+25</c>, <c>-0</c>), which MariaDB rejects with a syntax error.
    /// </summary>
    public static long? LimitLiteral(PhpNumber number)
    {
        if (!number.IsFloat)
        {
            return number.Whole >= 0 ? number.Whole : null;
        }

        var real = number.Real;
        if (double.IsNaN(real) || double.IsInfinity(real) || double.IsNegative(real) || Math.Abs(real) >= 1e15 || Math.Floor(real) != real)
        {
            return null;
        }

        return (long)real;
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
