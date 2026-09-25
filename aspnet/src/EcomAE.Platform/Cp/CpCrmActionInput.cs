using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Flattened request fields for the PHP <c>ajax_crm.php</c> twin: the CRM JS posts
/// <c>FormData</c>, native SSR forms post urlencoded, and API clients post JSON. All three
/// are read into one case-sensitive map so the dispatcher binds PHP field names uniformly.
/// </summary>
public sealed class CpCrmActionInput
{
    private readonly Dictionary<string, string> _values;

    private CpCrmActionInput(Dictionary<string, string> values)
    {
        _values = values;
    }

    public static CpCrmActionInput Empty => new(new Dictionary<string, string>(StringComparer.Ordinal));

    public static CpCrmActionInput FromForm(IFormCollection form)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in form)
        {
            values[pair.Key] = pair.Value.ToString();
        }

        return new CpCrmActionInput(values);
    }

    public static async Task<CpCrmActionInput> FromJsonAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var contentType = context.Request.ContentType ?? string.Empty;
        if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return new CpCrmActionInput(values);
        }

        try
        {
            using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    values[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        JsonValueKind.True => "1",
                        JsonValueKind.False => "0",
                        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                        _ => prop.Value.GetRawText(),
                    };
                }
            }
        }
        catch (JsonException)
        {
        }

        return new CpCrmActionInput(values);
    }

    public bool Has(params string[] names)
    {
        foreach (var name in names)
        {
            if (_values.ContainsKey(name))
            {
                return true;
            }
        }

        return false;
    }

    public string Text(params string[] names)
    {
        foreach (var name in names)
        {
            if (_values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    public string? TextOrNull(params string[] names)
    {
        foreach (var name in names)
        {
            if (_values.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    public long Long(params string[] names)
    {
        var raw = Text(names);
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    public int Int(params string[] names) => (int)Math.Clamp(Long(names), int.MinValue, int.MaxValue);

    public decimal Dec(params string[] names)
    {
        var raw = Text(names).Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : 0m;
    }

    /// <summary>PHP <c>!empty($post[...])</c>: "1", "true", "on", "yes" are truthy; "0" and "" are not.</summary>
    public bool Flag(params string[] names)
    {
        var raw = Text(names);
        return raw.Length > 0
               && raw != "0"
               && !raw.Equals("false", StringComparison.OrdinalIgnoreCase)
               && !raw.Equals("off", StringComparison.OrdinalIgnoreCase)
               && !raw.Equals("no", StringComparison.OrdinalIgnoreCase);
    }
}
