using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EcomAE.Platform.Storefront;

/// <summary>PHP <c>ajax_checkout_create.php</c> <c>$_COOKIE["how_get"]</c> + <c>prepare_json_htmlentities</c>.</summary>
public static class StorefrontHowGetCookie
{
    public const int MaxLength = 16_384;

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public static string? ReadRaw(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Cookies.TryGetValue("how_get", out var howGet) && !string.IsNullOrWhiteSpace(howGet))
        {
            return Unescape(howGet);
        }

        if (request.Cookies.TryGetValue("how_get_epc_carriers", out var carriers) && !string.IsNullOrWhiteSpace(carriers))
        {
            return Unescape(carriers);
        }

        return null;
    }

    public static int ReadInt(string? cookieJson, string name)
    {
        if (!TryParseObject(cookieJson, out var root) || root[name] is null)
        {
            return 0;
        }

        var node = root[name];
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var n))
            {
                return n;
            }

            if (value.TryGetValue<long>(out var l))
            {
                return l is > int.MinValue and <= int.MaxValue ? (int)l : 0;
            }

            if (value.TryGetValue<string>(out var s)
                && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    public static string BuildHowGetJson(int mode, int officeId, string? cookieJson)
    {
        var root = TryParseObject(cookieJson, out var parsed) ? parsed : new JsonObject();
        root["mode"] = mode;
        if (officeId > 0)
        {
            root["office_id"] = officeId;
        }

        return Encode(root).ToJsonString(CompactJson);
    }

    private static bool TryParseObject(string? raw, out JsonObject root)
    {
        root = new JsonObject();
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > MaxLength)
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(text) is JsonObject obj)
            {
                root = obj;
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static string Unescape(string raw)
    {
        if (!raw.Contains('%', StringComparison.Ordinal))
        {
            return raw;
        }

        try
        {
            return Uri.UnescapeDataString(raw);
        }
        catch (UriFormatException)
        {
            return raw;
        }
    }

    private static JsonNode Encode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                var encoded = new JsonObject();
                foreach (var kv in obj)
                {
                    if (kv.Value is null)
                    {
                        encoded[kv.Key] = null;
                        continue;
                    }

                    encoded[kv.Key] = Encode(kv.Value);
                }

                return encoded;
            case JsonArray arr:
                var list = new JsonArray();
                foreach (var item in arr)
                {
                    list.Add(item is null ? null : Encode(item));
                }

                return list;
            case JsonValue value when value.TryGetValue<string>(out var text):
                return JsonValue.Create(StorefrontGuestSessionService.HtmlEntities(text));
            default:
                return node.DeepClone();
        }
    }
}
