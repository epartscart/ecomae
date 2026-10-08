using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// An ordered PHP array: insertion order, PHP key normalisation (<c>"5"</c> is the int key 5),
/// last write wins in place. Values are PHP scalars (<c>null</c>, <c>bool</c>, <c>long</c>, <c>double</c>,
/// <c>string</c>) or nested arrays.
/// </summary>
public sealed class PhpArray : IEnumerable<KeyValuePair<object, object?>>
{
    private readonly List<object> _keys = [];
    private readonly Dictionary<object, object?> _values = [];
    private long _nextIndex;

    public int Count => _keys.Count;

    public IEnumerable<object?> Values => _keys.Select(k => _values[k]);

    public object? this[string key]
    {
        get => _values.TryGetValue(NormalizeKey(key), out var v) ? v : null;
        set => Set(NormalizeKey(key), value);
    }

    public static PhpArray List(params object?[] items)
    {
        var list = new PhpArray();
        foreach (var item in items)
        {
            list.Append(item);
        }

        return list;
    }

    public void Add(string key, object? value) => this[key] = value;

    public void Append(object? value) => Set(_nextIndex, value);

    public bool Has(string key) => _values.ContainsKey(NormalizeKey(key));

    /// <summary>PHP <c>$a[$k] ?? null</c>: missing and null are the same.</summary>
    public object? Get(string key) => this[key];

    public bool IsList()
    {
        for (var i = 0; i < _keys.Count; i++)
        {
            if (_keys[i] is not long l || l != i)
            {
                return false;
            }
        }

        return true;
    }

    public IEnumerator<KeyValuePair<object, object?>> GetEnumerator()
        => _keys.Select(k => new KeyValuePair<object, object?>(k, _values[k])).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private void Set(object key, object? value)
    {
        if (!_values.ContainsKey(key))
        {
            _keys.Add(key);
        }

        _values[key] = value;
        if (key is long l && l >= _nextIndex)
        {
            _nextIndex = l + 1;
        }
    }

    private static object NormalizeKey(string key)
        => Regex.IsMatch(key, "^(0|-?[1-9][0-9]*)$") && long.TryParse(key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
            ? n
            : key;

    /// <summary>PHP <c>json_decode($raw, true)</c>; null when the text is not JSON.</summary>
    public static object? JsonDecode(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 512 });
            return FromElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static object? FromElement(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                var map = new PhpArray();
                foreach (var p in el.EnumerateObject())
                {
                    map[p.Name] = FromElement(p.Value);
                }

                return map;
            case JsonValueKind.Array:
                var list = new PhpArray();
                foreach (var item in el.EnumerateArray())
                {
                    list.Append(FromElement(item));
                }

                return list;
            case JsonValueKind.String:
                return el.GetString() ?? string.Empty;
            case JsonValueKind.Number:
                var rawNumber = el.GetRawText();
                return Regex.IsMatch(rawNumber, "^-?[0-9]+$") && long.TryParse(rawNumber, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)
                    ? whole
                    : (object)el.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }

    /// <summary>PHP <c>$_POST</c> from form fields, with the bracket syntax (<c>a[b]=1</c>, <c>a[]=1</c>).</summary>
    public static PhpArray FromForm(IEnumerable<KeyValuePair<string, string>> fields)
    {
        var root = new PhpArray();
        foreach (var (name, value) in fields)
        {
            var open = name.IndexOf('[', StringComparison.Ordinal);
            if (open <= 0)
            {
                root[name.Replace('.', '_').Replace(' ', '_')] = value;
                continue;
            }

            var parts = new List<string>();
            var rest = name[open..];
            while (rest.StartsWith('['))
            {
                var close = rest.IndexOf(']', StringComparison.Ordinal);
                if (close < 0)
                {
                    break;
                }

                parts.Add(rest[1..close]);
                rest = rest[(close + 1)..];
            }

            var target = root;
            var head = name[..open].Replace('.', '_').Replace(' ', '_');
            if (target[head] is not PhpArray child)
            {
                child = new PhpArray();
                target[head] = child;
            }

            target = child;
            for (var i = 0; i < parts.Count; i++)
            {
                var last = i == parts.Count - 1;
                if (last)
                {
                    if (parts[i].Length == 0)
                    {
                        target.Append(value);
                    }
                    else
                    {
                        target[parts[i]] = value;
                    }

                    break;
                }

                PhpArray next;
                if (parts[i].Length == 0)
                {
                    next = new PhpArray();
                    target.Append(next);
                }
                else if (target[parts[i]] is PhpArray existing)
                {
                    next = existing;
                }
                else
                {
                    next = new PhpArray();
                    target[parts[i]] = next;
                }

                target = next;
            }
        }

        return root;
    }
}

/// <summary>PHP scalar semantics the free tools rely on.</summary>
public static class FreeToolsPhp
{
    /// <summary>PHP <c>(string)$v</c>.</summary>
    public static string Str(object? v) => v switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "1" : string.Empty,
        long l => l.ToString(CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        double d => StorefrontPhpAjax.PhpFloatString(d),
        PhpArray => "Array",
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    /// <summary>PHP <c>(float)$v</c>.</summary>
    public static double Float(object? v) => v switch
    {
        null => 0d,
        bool b => b ? 1d : 0d,
        long l => l,
        int i => i,
        double d => d,
        string s => StorefrontPhpAjax.PhpFloatCast(s),
        PhpArray a => a.Count > 0 ? 1d : 0d,
        _ => 0d,
    };

    /// <summary>PHP <c>(int)$v</c> (64-bit, PHP 8: out-of-range floats wrap modulo 2^64).</summary>
    public static long Int(object? v) => v switch
    {
        null => 0,
        bool b => b ? 1 : 0,
        long l => l,
        int i => i,
        double d => IntFromDouble(d),
        string s => IntFromString(s),
        PhpArray a => a.Count > 0 ? 1 : 0,
        _ => 0,
    };

    public static long IntFromDouble(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
        {
            return 0;
        }

        if (d >= -9.2233720368547758E18 && d < 9.2233720368547758E18)
        {
            return (long)d;
        }

        const double two64 = 18446744073709551616.0;
        var dmod = d % two64;
        if (dmod < 0)
        {
            if (dmod < -9.2233720368547758E18)
            {
                dmod += two64;
            }
        }
        else if (dmod >= 9.2233720368547758E18)
        {
            dmod -= two64;
        }

        return (long)dmod;
    }

    /// <summary>PHP 8 <c>(int)"…"</c>: a leading integer, or the float form (<c>"9e1"</c> is 90).</summary>
    public static long IntFromString(string s)
    {
        var m = Regex.Match(s, "^[ \\t\\n\\r\\v\\f]*[+-]?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)(?:[eE][+-]?[0-9]+)?");
        if (!m.Success)
        {
            return 0;
        }

        var text = m.Value.TrimStart(' ', '\t', '\n', '\r', '\v', '\f');
        if (Regex.IsMatch(text, "^[+-]?[0-9]+$"))
        {
            return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)
                ? whole
                : (text.StartsWith('-') ? long.MinValue : long.MaxValue);
        }

        return IntFromDouble(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    /// <summary>PHP <c>trim()</c> default characters.</summary>
    public static string Trim(string s) => s.Trim(' ', '\t', '\n', '\r', '\0', '\x0B');

    public static string Lower(string s) => AsciiCase(s, upper: false);

    public static string Upper(string s) => AsciiCase(s, upper: true);

    private static string AsciiCase(string s, bool upper)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (upper && c is >= 'a' and <= 'z')
            {
                chars[i] = (char)(c - 32);
            }
            else if (!upper && c is >= 'A' and <= 'Z')
            {
                chars[i] = (char)(c + 32);
            }
        }

        return new string(chars);
    }

    /// <summary>PHP <c>round()</c>: half away from zero after PHP's pre-rounding to 15 significant digits.</summary>
    public static double Round(double value, int places = 0)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) >= 1e15)
        {
            return value;
        }

        var rounded = (double)Math.Round((decimal)value, places, MidpointRounding.AwayFromZero);
        return rounded == 0 && double.IsNegative(value) ? -0.0 : rounded;
    }

    /// <summary>PHP <c>number_format($v, $decimals)</c> with "," and ".".</summary>
    public static string NumberFormat(double value, int decimals)
    {
        var rounded = Round(value, decimals);
        if (rounded == 0)
        {
            rounded = 0;
        }

        var text = Math.Abs(rounded).ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        var whole = dot < 0 ? text : text[..dot];
        var frac = dot < 0 ? string.Empty : text[dot..];
        var sb = new StringBuilder();
        for (var i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % 3 == 0)
            {
                sb.Append(',');
            }

            sb.Append(whole[i]);
        }

        return (rounded < 0 ? "-" : string.Empty) + sb + frac;
    }

    /// <summary>PHP <c>date($format)</c> for the letters the tools use, in the server time zone.</summary>
    public static string Date(string format, long unix)
    {
        var (year, month, day) = LocalCivil(unix);
        var sb = new StringBuilder();
        foreach (var c in format)
        {
            sb.Append(c switch
            {
                'Y' => (year < 0 ? "-" : string.Empty) + Math.Abs(year).ToString("0000", CultureInfo.InvariantCulture),
                'y' => (Math.Abs(year) % 100).ToString("00", CultureInfo.InvariantCulture),
                'm' => month.ToString("00", CultureInfo.InvariantCulture),
                'n' => month.ToString(CultureInfo.InvariantCulture),
                'd' => day.ToString("00", CultureInfo.InvariantCulture),
                'j' => day.ToString(CultureInfo.InvariantCulture),
                'M' => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName((int)month),
                'F' => CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName((int)month),
                _ => c.ToString(),
            });
        }

        return sb.ToString();
    }

    /// <summary>The local calendar date of a Unix time, for any year.</summary>
    private static (long Year, long Month, long Day) LocalCivil(long unix)
    {
        var offset = TimeZoneInfo.Local.BaseUtcOffset;
        if (unix is > -62135596800L and < 253402300800L)
        {
            offset = TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(unix));
        }

        var z = (long)Math.Floor((unix + offset.TotalSeconds) / 86400.0) + 719468;
        var era = (z >= 0 ? z : z - 146096) / 146097;
        var doe = z - (era * 146097);
        var yoe = (doe - (doe / 1460) + (doe / 36524) - (doe / 146096)) / 365;
        var doy = doe - ((365 * yoe) + (yoe / 4) - (yoe / 100));
        var mp = ((5 * doy) + 2) / 153;
        var d = doy - (((153 * mp) + 2) / 5) + 1;
        var m = mp < 10 ? mp + 3 : mp - 9;
        return (yoe + (era * 400) + (m <= 2 ? 1 : 0), m, d);
    }

    /// <summary>PHP <c>mktime()</c> semantics: months and days overflow into the next unit; any year, proleptic Gregorian.</summary>
    public static long LocalUnix(long year, long month, long day, long hour = 0, long minute = 0, long second = 0)
    {
        year += (long)Math.Floor((month - 1) / 12.0);
        month = ((((month - 1) % 12) + 12) % 12) + 1;
        var wall = ((DaysFromCivil(year, month, 1) + day - 1) * 86400) + (hour * 3600) + (minute * 60) + second;
        var offset = TimeZoneInfo.Local.BaseUtcOffset;
        if (wall is > -62135596800L and < 253402300800L)
        {
            offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.SpecifyKind(DateTime.UnixEpoch.AddSeconds(wall), DateTimeKind.Unspecified));
        }

        return wall - (long)offset.TotalSeconds;
    }

    private static long DaysFromCivil(long y, long m, long d)
    {
        y -= m <= 2 ? 1 : 0;
        var era = (y >= 0 ? y : y - 399) / 400;
        var yoe = y - (era * 400);
        var doy = ((153 * (m + (m > 2 ? -3 : 9))) + 2) / 5 + d - 1;
        var doe = (yoe * 365) + (yoe / 4) - (yoe / 100) + doy;
        return (era * 146097) + doe - 719468;
    }

    private static readonly string[] MonthNames =
        ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    /// <summary>
    /// PHP <c>strtotime()</c> for the absolute date forms the tools receive: <c>Y-m-d</c>, <c>Y/m/d</c>
    /// (optional <c>H:i[:s]</c>), <c>m/d/Y</c>, <c>d-m-Y</c>, <c>d.m.Y</c>, <c>d M Y</c>, <c>M d[,] Y</c> and
    /// <c>today</c>/<c>now</c>/<c>tomorrow</c>/<c>yesterday</c>. Day overflow rolls over like PHP
    /// (<c>2026-02-31</c> is 3 March). Other forms answer null, which the tools show as "no date".
    /// </summary>
    public static long? StrToTime(string input, long now)
    {
        var s = Lower(Trim(input));
        if (s.Length == 0)
        {
            return null;
        }

        var nowLocal = DateTimeOffset.FromUnixTimeSeconds(now).ToLocalTime();
        switch (s)
        {
            case "now":
                return now;
            case "today":
            case "midnight":
                return LocalUnix(nowLocal.Year, nowLocal.Month, nowLocal.Day);
            case "tomorrow":
                return LocalUnix(nowLocal.Year, nowLocal.Month, nowLocal.Day + 1);
            case "yesterday":
                return LocalUnix(nowLocal.Year, nowLocal.Month, nowLocal.Day - 1);
        }

        const string time = @"(?:[ t]+(?<h>[01]?[0-9]|2[0-4]):(?<i>[0-5][0-9])(?::(?<s>[0-5][0-9]|60))?)?";
        Match m;
        if ((m = Regex.Match(s, @"^(?<y>[0-9]{4})-(?<m>0?[0-9]|1[0-2])-(?<d>[0-2]?[0-9]|3[01])" + time + "$")).Success
            || (m = Regex.Match(s, @"^(?<y>[0-9]{4})/(?<m>0?[0-9]|1[0-2])/(?<d>[0-2]?[0-9]|3[01])" + time + "$")).Success
            || (m = Regex.Match(s, @"^(?<m>0?[0-9]|1[0-2])/(?<d>[0-2]?[0-9]|3[01])/(?<y>[0-9]{4})" + time + "$")).Success
            || (m = Regex.Match(s, @"^(?<d>[0-2]?[0-9]|3[01])-(?<m>0?[0-9]|1[0-2])-(?<y>[0-9]{4})" + time + "$")).Success
            || (m = Regex.Match(s, @"^(?<d>[0-2]?[0-9]|3[01])\.(?<m>0?[0-9]|1[0-2])\.(?<y>[0-9]{4})" + time + "$")).Success)
        {
            return Compose(
                m,
                int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture));
        }

        var monthAlt = "(?<mon>" + string.Join("|", MonthNames.Select(n => n + "[a-z]*")) + ")";
        if ((m = Regex.Match(s, @"^(?<d>[0-2]?[0-9]|3[01])[ \-]+" + monthAlt + @"[ \-,.]+(?<y>[0-9]{4})" + time + "$")).Success
            || (m = Regex.Match(s, "^" + monthAlt + @"[ \-.]+(?<d>[0-2]?[0-9]|3[01])(?:st|nd|rd|th)?[ ,.]+(?<y>[0-9]{4})" + time + "$")).Success)
        {
            var word = m.Groups["mon"].Value;
            var index = Array.FindIndex(MonthNames, n => word.StartsWith(n, StringComparison.Ordinal));
            if (index < 0 || !IsMonthWord(word))
            {
                return null;
            }

            return Compose(m, index + 1, int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture));
        }

        if ((m = Regex.Match(s, @"^(?<d>[0-2]?[0-9]|3[01])[ \-.]+" + monthAlt + "$")).Success
            || (m = Regex.Match(s, "^" + monthAlt + @"[ \-.]+(?<d>[0-2]?[0-9]|3[01])(?:st|nd|rd|th)?$")).Success)
        {
            var word = m.Groups["mon"].Value;
            var index = Array.FindIndex(MonthNames, n => word.StartsWith(n, StringComparison.Ordinal));
            return index < 0 || !IsMonthWord(word)
                ? null
                : LocalUnix(nowLocal.Year, index + 1, int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }

    private static readonly string[] FullMonths =
        ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december", "sept"];

    private static bool IsMonthWord(string word)
        => word.Length == 3 || FullMonths.Contains(word, StringComparer.Ordinal);

    private static long? Compose(Match m, int month, int day)
    {
        var year = int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
        var hour = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture) : 0;
        var minute = m.Groups["i"].Success ? int.Parse(m.Groups["i"].Value, CultureInfo.InvariantCulture) : 0;
        var second = m.Groups["s"].Success ? int.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture) : 0;
        return LocalUnix(year, month, day, hour, minute, second);
    }

    /// <summary>PHP <c>strtotime('+N months', $ts)</c>: month arithmetic with day overflow.</summary>
    public static long AddMonths(long unix, int months)
    {
        var t = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
        var totalMonths = (t.Year * 12) + (t.Month - 1) + months;
        var year = totalMonths / 12;
        var month = (totalMonths % 12) + 1;
        return LocalUnix(year, month, t.Day, t.Hour, t.Minute, t.Second);
    }

    private static readonly Regex EmailPattern = new(
        "^(?!(?:(?:\\x22?\\x5C[\\x00-\\x7E]\\x22?)|(?:\\x22?[^\\x5C\\x22]\\x22?)){255,})(?!(?:(?:\\x22?\\x5C[\\x00-\\x7E]\\x22?)|(?:\\x22?[^\\x5C\\x22]\\x22?)){65,}@)(?:(?:[\\x21\\x23-\\x27\\x2A\\x2B\\x2D\\x2F-\\x39\\x3D\\x3F\\x5E-\\x7E]+)|(?:\\x22(?:[\\x01-\\x08\\x0B\\x0C\\x0E-\\x1F\\x21\\x23-\\x5B\\x5D-\\x7F]|(?:\\x5C[\\x00-\\x7F]))*\\x22))(?:\\.(?:(?:[\\x21\\x23-\\x27\\x2A\\x2B\\x2D\\x2F-\\x39\\x3D\\x3F\\x5E-\\x7E]+)|(?:\\x22(?:[\\x01-\\x08\\x0B\\x0C\\x0E-\\x1F\\x21\\x23-\\x5B\\x5D-\\x7F]|(?:\\x5C[\\x00-\\x7F]))*\\x22)))*@(?:(?:(?!.*[^.]{64,})(?:(?:(?:xn--)?[a-z0-9]+(?:-+[a-z0-9]+)*\\.){1,126}){1,}(?:(?:[a-z][a-z0-9]*)|(?:(?:xn--)[a-z0-9]+))(?:-+[a-z0-9]+)*)|(?:\\[(?:(?:IPv6:(?:(?:[a-f0-9]{1,4}(?::[a-f0-9]{1,4}){7})|(?:(?!(?:.*[a-f0-9][:\\]]){7,})(?:[a-f0-9]{1,4}(?::[a-f0-9]{1,4}){0,5})?::(?:[a-f0-9]{1,4}(?::[a-f0-9]{1,4}){0,5})?)))|(?:(?:IPv6:(?:(?:[a-f0-9]{1,4}(?::[a-f0-9]{1,4}){5}:)|(?:(?!(?:.*[a-f0-9]:){5,})(?:[a-f0-9]{1,4}(?::[a-f0-9]{1,4}){0,3})?::(?:[a-f0-9]{1,4}(?::[a-f0-9]{1,4}){0,3}:)?)))?(?:(?:25[0-5])|(?:2[0-4][0-9])|(?:1[0-9]{2})|(?:[1-9]?[0-9]))(?:\\.(?:(?:25[0-5])|(?:2[0-4][0-9])|(?:1[0-9]{2})|(?:[1-9]?[0-9]))){3}))\\]))\\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    /// <summary>PHP <c>filter_var($email, FILTER_VALIDATE_EMAIL)</c> (ASCII mode, 320-byte limit).</summary>
    public static bool IsEmail(string email)
    {
        if (email.Length == 0 || email.Length > 320 || email.Any(c => c > 0x7F))
        {
            return false;
        }

        try
        {
            return EmailPattern.IsMatch(email);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>PHP <c>substr($s, 0, $bytes)</c> on UTF-8, dropping a trailing partial character.</summary>
    public static string SubstrBytes(string s, int bytes)
    {
        var encoded = Encoding.UTF8.GetBytes(s);
        if (encoded.Length <= bytes)
        {
            return s;
        }

        var end = bytes;
        while (end > 0 && (encoded[end] & 0xC0) == 0x80)
        {
            end--;
        }

        return Encoding.UTF8.GetString(encoded, 0, end);
    }

    /// <summary>PHP <c>json_encode()</c> (serialize_precision -1); <paramref name="escapeSlashes"/> false is <c>JSON_UNESCAPED_SLASHES</c>.</summary>
    public static string JsonEncode(object? value, bool escapeSlashes = false)
    {
        var sb = new StringBuilder();
        Write(sb, value, escapeSlashes);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, object? value, bool escapeSlashes)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case long l:
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
                break;
            case int i:
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                break;
            case double d:
                sb.Append(JsonFloat(d));
                break;
            case string s:
                WriteString(sb, s, escapeSlashes);
                break;
            case PhpArray a when a.IsList():
                sb.Append('[');
                var first = true;
                foreach (var item in a.Values)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }

                    first = false;
                    Write(sb, item, escapeSlashes);
                }

                sb.Append(']');
                break;
            case PhpArray a:
                sb.Append('{');
                var firstKey = true;
                foreach (var (k, v) in a)
                {
                    if (!firstKey)
                    {
                        sb.Append(',');
                    }

                    firstKey = false;
                    WriteString(sb, Str(k), escapeSlashes);
                    sb.Append(':');
                    Write(sb, v, escapeSlashes);
                }

                sb.Append('}');
                break;
            default:
                WriteString(sb, Str(value), escapeSlashes);
                break;
        }
    }

    /// <summary>A float as PHP <c>json_encode</c> writes it: shortest round trip, exponent form only below 1e-4 or past 17 digits.</summary>
    public static string JsonFloat(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
        {
            return "0";
        }

        if (d == 0)
        {
            return double.IsNegative(d) ? "-0" : "0";
        }

        var digits = ShortestDigits(Math.Abs(d).ToString("R", CultureInfo.InvariantCulture), out var decpt);
        var sb = new StringBuilder();
        if (d < 0)
        {
            sb.Append('-');
        }

        if (decpt < -3 || decpt > 17)
        {
            var exp = decpt - 1;
            sb.Append(digits[0]).Append('.').Append(digits.Length > 1 ? digits[1..] : "0")
                .Append('e').Append(exp < 0 ? '-' : '+').Append(Math.Abs(exp).ToString(CultureInfo.InvariantCulture));
        }
        else if (decpt <= 0)
        {
            sb.Append("0.").Append('0', -decpt).Append(digits);
        }
        else if (digits.Length <= decpt)
        {
            sb.Append(digits).Append('0', decpt - digits.Length);
        }
        else
        {
            sb.Append(digits[..decpt]).Append('.').Append(digits[decpt..]);
        }

        return sb.ToString();
    }

    private static string ShortestDigits(string roundTrip, out int decpt)
    {
        var mantissa = roundTrip;
        var exponent = 0;
        var e = roundTrip.IndexOfAny(['E', 'e']);
        if (e >= 0)
        {
            mantissa = roundTrip[..e];
            exponent = int.Parse(roundTrip[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        var intPart = dot < 0 ? mantissa : mantissa[..dot];
        var fracPart = dot < 0 ? string.Empty : mantissa[(dot + 1)..];
        var all = intPart + fracPart;
        var lead = 0;
        while (lead < all.Length - 1 && all[lead] == '0')
        {
            lead++;
        }

        decpt = intPart.Length - lead + exponent;
        return all[lead..].TrimEnd('0') is { Length: > 0 } t ? t : "0";
    }

    private static void WriteString(StringBuilder sb, string s, bool escapeSlashes)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '/':
                    sb.Append(escapeSlashes ? "\\/" : "/");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < 0x20 || c > 0x7F)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
