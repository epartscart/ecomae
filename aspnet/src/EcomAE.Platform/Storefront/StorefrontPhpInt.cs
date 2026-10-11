using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP 8.3 <c>(int)</c> / <c>intval</c> of a request value. A numeric string (optional sign, digits, optional fraction,
/// optional exponent) is converted as a float and then truncated toward zero; any other string uses the leading
/// optional sign plus digits only. A non-empty PHP array becomes 1; an empty array becomes 0. Values outside
/// <see cref="long"/> become <see cref="long.MaxValue"/> or <see cref="long.MinValue"/> like PHP_INT_MAX / PHP_INT_MIN.
/// </summary>
public static partial class StorefrontPhpInt
{
    [GeneratedRegex(@"^[ \t\n\r\v\f]*[+-]?((\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumeric();

    public static long Cast(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return 0;
        }

        var match = LeadingNumeric().Match(raw);
        if (!match.Success)
        {
            return 0;
        }

        var token = match.Value.Trim(' ', '\t', '\n', '\r', '\v', '\f');
        if (token.AsSpan().IndexOfAny('.', 'e', 'E') >= 0
            && double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
        {
            if (double.IsNaN(real) || double.IsInfinity(real))
            {
                return 0;
            }

            if (real >= long.MaxValue)
            {
                return long.MaxValue;
            }

            if (real <= long.MinValue)
            {
                return long.MinValue;
            }

            return (long)real;
        }

        return long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)
            ? whole
            : Overflow(token);
    }

    public static long CastArray(bool present, bool empty) => !present || empty ? 0 : 1;

    private static long Overflow(string token)
        => token.StartsWith('-') ? long.MinValue : long.MaxValue;
}
