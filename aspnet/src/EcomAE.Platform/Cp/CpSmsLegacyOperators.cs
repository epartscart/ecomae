using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;

namespace EcomAE.Platform.Cp;

/// <summary>What a PHP <c>send_sms.php</c> handler prints: <c>{"status":…,"message":…}</c>, or nothing at all.</summary>
/// <param name="MessageJson">The JSON of a message PHP prints as something other than a string (<c>null</c>, a number).</param>
public sealed record CpSmsHandlerAnswer(bool Status, string Message, bool Silent = false, string? MessageJson = null)
{
    public static CpSmsHandlerAnswer Fail(string message) => new(false, message);
}

/// <summary>Translation and site settings a legacy handler reads (<c>translate_str_by_id</c>, <c>DP_Config</c>).</summary>
public sealed record CpSmsHandlerContext(Func<int, CancellationToken, Task<string>> Translate, string DomainPath)
{
    public static CpSmsHandlerContext None { get; } = new((_, _) => Task.FromResult(string.Empty), string.Empty);

    /// <summary>A handler run by HTTP from the server carries no language, so PHP's <c>multilang_init</c> lands on English.</summary>
    public static CpSmsHandlerContext For(System.Data.Common.DbConnection connection, IReadOnlyDictionary<string, string> config)
    {
        var translator = new Storefront.StorefrontPhpTranslator(connection);
        return new((id, token) => translator.TextAsync(id, token), config.TryGetValue("domain_path", out var domain) ? domain : string.Empty);
    }
}

/// <summary>
/// The legacy operator handlers in <c>content/sms/handlers</c> (iqsms, rocketsms_by, semysms, smsaero, smsgorod_ru,
/// smsimple, sms_ru, smstraffic, smsvizitka_com, terasms_ru): the same provider request, and the same reading of the
/// reply, including PHP's loose comparisons, as each <c>send_sms.php</c>.
/// </summary>
public static class CpSmsLegacyOperators
{
    public const string NoRedirectClient = "epc-sms-no-redirect";

    public static IReadOnlyList<string> Handlers { get; } =
        ["iqsms", "rocketsms_by", "semysms", "smsaero", "smsgorod_ru", "smsimple", "sms_ru", "smstraffic", "smsvizitka_com", "terasms_ru"];

    private static readonly Regex SmsTrafficId = new(@"<sms_id>(\d+)</sms_id>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex SmsTrafficDescription = new(@"<description>(.+?)</description>", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private sealed record Reply(int Status, string? ReasonPhrase, string Version, string HeaderBlock, string? Body);

    public static async Task<CpSmsHandlerAnswer> SendAsync(
        IHttpClientFactory clients,
        string handler,
        IReadOnlyDictionary<string, string> parameters,
        string phone,
        string body,
        CpSmsHandlerContext context,
        CancellationToken cancellationToken)
    {
        string P(string key) => parameters.TryGetValue(key, out var value) ? value : string.Empty;
        Task<string> T(int id) => context.Translate(id, cancellationToken);
        var noPlus = phone.Replace("+", string.Empty, StringComparison.Ordinal);

        switch (handler)
        {
            case "sms_ru":
            {
                var url = "https://sms.ru/sms/send?api_id=" + OAuthStart.PhpUrlEncode(P("api_id"))
                    + "&to=" + OAuthStart.PhpUrlEncode(noPlus)
                    + "&msg=" + OAuthStart.PhpUrlEncode(body)
                    + "&json=1&translit=" + P("translit");
                var reply = await SendHttpAsync(clients, NoRedirectClient, HttpMethod.Get, url, null, null, [], 25, cancellationToken).ConfigureAwait(false);
                var text = StripWhitespace(reply?.Body ?? string.Empty);
                var json = Decode(text);
                if (!Truthy(json))
                {
                    return CpSmsHandlerAnswer.Fail(await T(4682).ConfigureAwait(false) + " " + text);
                }

                if (!LooseEqualsString(Field(json, "status"), "OK"))
                {
                    return CpSmsHandlerAnswer.Fail(await T(4680).ConfigureAwait(false) + " " + Concat(Field(json, "status_code")) + ", " + Concat(Field(json, "status_text")));
                }

                if (Field(json, "sms") is not { ValueKind: JsonValueKind.Object or JsonValueKind.Array } sms || !First(sms, out var first))
                {
                    return new CpSmsHandlerAnswer(false, string.Empty, Silent: true);
                }

                return LooseEqualsString(Field(first, "status"), "OK")
                    ? new CpSmsHandlerAnswer(true, string.Empty)
                    : CpSmsHandlerAnswer.Fail(await T(4683).ConfigureAwait(false) + " " + Concat(Field(json, "status_code")) + ", " + Concat(Field(json, "status_text")));
            }

            case "iqsms":
            {
                var url = "http://api.iqsms.ru/messages/v2/send/?phone=+7" + OAuthStart.PhpUrlEncode(phone)
                    + "&text=" + OAuthStart.PhpUrlEncode(body)
                    + "&login=" + OAuthStart.PhpUrlEncode(P("login"))
                    + "&password=" + OAuthStart.PhpUrlEncode(P("password"));
                var reply = await SendHttpAsync(clients, NoRedirectClient, HttpMethod.Get, url, null, null, [], 25, cancellationToken).ConfigureAwait(false);
                var text = reply?.Body ?? string.Empty;
                var parts = text.Split(';');
                if (parts.Length != 2)
                {
                    return CpSmsHandlerAnswer.Fail(await T(4678).ConfigureAwait(false) + ". " + text);
                }

                int? code = parts[1] switch
                {
                    "accepted" => 4670,
                    "invalid mobile phone" => 4671,
                    "text is empty" => 4672,
                    "sender address invalid" => 4673,
                    "wapurl invalid" => 4674,
                    "invalid schedule time format" => 4675,
                    "invalid status queue name" => 4676,
                    "not enough credits" => 4677,
                    _ => null,
                };
                var message = await T(code ?? 4679).ConfigureAwait(false);
                return new CpSmsHandlerAnswer(true, code == 4671 ? message + " 71234567890)" : message);
            }

            case "rocketsms_by":
            {
                var form = "username=" + P("login") + "&password=" + P("pass")
                    + "&phone=" + OAuthStart.PhpUrlEncode(noPlus) + "&text=" + OAuthStart.PhpUrlEncode(body);
                var reply = await SendHttpAsync(clients, NoRedirectClient, HttpMethod.Post, "http://api.rocketsms.by/json/send", form, "application/x-www-form-urlencoded", [], 25, cancellationToken).ConfigureAwait(false);
                var json = Decode(reply?.Body ?? string.Empty);
                if (Truthy(json) && IsSet(Field(json, "id")))
                {
                    return new CpSmsHandlerAnswer(true, string.Empty);
                }

                if (Truthy(json) && IsSet(Field(json, "error")))
                {
                    return CpSmsHandlerAnswer.Fail(await T(4680).ConfigureAwait(false) + " " + Concat(Field(json, "error")));
                }

                return CpSmsHandlerAnswer.Fail(await T(4681).ConfigureAwait(false) + ". Service error");
            }

            case "semysms":
            {
                var to = phone.StartsWith('+') ? phone[1..] : phone;
                if (Encoding.UTF8.GetByteCount(to) == 10)
                {
                    to = "7" + to;
                }
                else if (to.StartsWith('8'))
                {
                    to = "7" + to[1..];
                }

                var query = new List<string>();
                foreach (var (key, value) in new[] { ("token", Optional(parameters, "token")), ("device", Optional(parameters, "device")), ("phone", to), ("msg", body) })
                {
                    if (value is not null)
                    {
                        query.Add(key + "=" + OAuthStart.PhpUrlEncode(value));
                    }
                }

                var reply = await SendHttpAsync(clients, "epc-sms", HttpMethod.Get, "https://semysms.net/api/3/sms.php?" + string.Join("&", query), null, null, [], 25, cancellationToken).ConfigureAwait(false);
                var json = Decode(reply?.Body ?? string.Empty);
                if (!Truthy(json))
                {
                    return CpSmsHandlerAnswer.Fail(await T(4682).ConfigureAwait(false) + " ");
                }

                return LooseEqualsZero(Field(json, "code"))
                    ? new CpSmsHandlerAnswer(true, string.Empty)
                    : CpSmsHandlerAnswer.Fail(await T(4680).ConfigureAwait(false) + " " + Concat(Field(json, "code")) + ", " + Concat(Field(json, "error")));
            }

            case "smsaero":
            {
                var url = "https://gate.smsaero.ru/v2/sms/send?text=" + RawUrlEncode(body)
                    + "&sign=" + OAuthStart.PhpUrlEncode(P("from"))
                    + "&number=" + OAuthStart.PhpUrlEncode(noPlus);
                var auth = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(P("login") + ":" + P("password")));
                var reply = await SendHttpAsync(clients, "epc-sms", HttpMethod.Get, url, null, null, [("Authorization", auth)], 25, cancellationToken).ConfigureAwait(false);
                var json = Decode(reply?.Body ?? string.Empty);
                return Truthy(Field(json, "success"))
                    ? new CpSmsHandlerAnswer(true, string.Empty)
                    : MessageOf(Field(json, "message"));
            }

            case "smsgorod_ru":
            {
                var text = PhpTrim(body);
                var channel = PhpTrim(P("channel"));
                if (channel != "digit" && channel != "char")
                {
                    channel = "digit";
                }

                if (PhpIntval(PhpTrim(P("domain"))) != 0)
                {
                    text = context.DomainPath
                        .Replace("https://", string.Empty, StringComparison.Ordinal)
                        .Replace("http://", string.Empty, StringComparison.Ordinal)
                        .Replace("/", string.Empty, StringComparison.Ordinal) + ": " + text;
                }

                var sms = new StringBuilder()
                    .Append("{\"phone\":").Append(OAuthStart.PhpJsonString(RussianPhone(phone)))
                    .Append(",\"text\":").Append(OAuthStart.PhpJsonString(text))
                    .Append(",\"channel\":").Append(OAuthStart.PhpJsonString(channel));
                if (channel == "char")
                {
                    sms.Append(",\"sender\":").Append(OAuthStart.PhpJsonString(PhpTrim(P("sender"))));
                }

                var payload = "{\"apiKey\":" + OAuthStart.PhpJsonString(PhpTrim(P("api-key"))) + ",\"sms\":[" + sms.Append('}') + "]}";
                var reply = await SendHttpAsync(clients, "epc-sms", HttpMethod.Post, "https://new.smsgorod.ru/apiSms/create", payload, "application/json", [("accept", "application/json")], 20, cancellationToken).ConfigureAwait(false);
                var json = Decode(reply?.Body ?? string.Empty);
                if (!LooseEqualsString(Field(json, "status"), "success"))
                {
                    return CpSmsHandlerAnswer.Fail(await T(4681).ConfigureAwait(false) + ".");
                }

                var line = Index(Field(json, "data"), 0);
                return LooseEqualsString(Field(line, "status"), "sent")
                    ? new CpSmsHandlerAnswer(true, string.Empty)
                    : MessageOf(Field(line, "errorDescription"));
            }

            case "smstraffic":
            {
                var form = "login=" + OAuthStart.PhpUrlEncode(P("login")) + "&password=" + OAuthStart.PhpUrlEncode(P("password"))
                    + "&want_sms_ids=1&phones=" + OAuthStart.PhpUrlEncode(noPlus) + "&message=" + OAuthStart.PhpUrlEncode(body) + "&max_parts=5&rus=5";
                var reply = await SendHttpAsync(clients, "epc-sms", HttpMethod.Post, "https://api.smstraffic.ru/multi.php", form, "application/x-www-form-urlencoded", [("User-Agent", "sms.php class 1.0 (curl https)")], 5, cancellationToken).ConfigureAwait(false);
                var response = reply is { Status: 200 } ? reply.Body ?? string.Empty : string.Empty;
                if (response.IndexOf("<result>OK</result>", StringComparison.Ordinal) > 0)
                {
                    return SmsTrafficId.IsMatch(response)
                        ? new CpSmsHandlerAnswer(true, string.Empty)
                        : CpSmsHandlerAnswer.Fail(await T(4684).ConfigureAwait(false));
                }

                var description = SmsTrafficDescription.Match(response);
                return description.Success
                    ? CpSmsHandlerAnswer.Fail(description.Groups[1].Value)
                    : CpSmsHandlerAnswer.Fail(await T(4685).ConfigureAwait(false));
            }

            case "smsvizitka_com":
            {
                var url = "http://crm.smsvizitka.com/api/send-sms?api_key=" + OAuthStart.PhpUrlEncode(P("api_key"))
                    + "&to=" + OAuthStart.PhpUrlEncode(noPlus) + "&text=" + OAuthStart.PhpUrlEncode(body);
                var reply = await SendHttpAsync(clients, "epc-sms", HttpMethod.Get, url, null, null, [], 25, cancellationToken).ConfigureAwait(false);
                var raw = reply is null
                    ? string.Empty
                    : "HTTP/" + reply.Version + " " + reply.Status.ToString(CultureInfo.InvariantCulture) + " " + reply.ReasonPhrase + "\r\n" + reply.HeaderBlock + "\r\n" + reply.Body;
                return raw.IndexOf("200 OK", StringComparison.Ordinal) > 0
                    ? new CpSmsHandlerAnswer(true, string.Empty)
                    : CpSmsHandlerAnswer.Fail(await T(4681).ConfigureAwait(false) + ".");
            }

            case "terasms_ru":
            {
                var payload = "{\"login\":" + OAuthStart.PhpJsonString(PhpTrim(P("login")))
                    + ",\"password\":" + OAuthStart.PhpJsonString(PhpTrim(P("password")))
                    + ",\"target\":" + OAuthStart.PhpJsonString(RussianPhone(PhpTrim(phone)))
                    + ",\"message\":" + OAuthStart.PhpJsonString(PhpTrim(body))
                    + ",\"sender\":" + OAuthStart.PhpJsonString(PhpTrim(P("sender"))) + "}";
                var reply = await SendHttpAsync(clients, "epc-sms", HttpMethod.Post, "https://auth.terasms.ru/outbox/send/json", payload, "application/json", [("accept", "application/json")], 20, cancellationToken).ConfigureAwait(false);
                var json = Decode(reply?.Body ?? string.Empty);
                return LooseAtLeastZero(Field(json, "status"))
                    ? new CpSmsHandlerAnswer(true, string.Empty)
                    : CpSmsHandlerAnswer.Fail(await T(4681).ConfigureAwait(false) + ". " + Concat(Field(json, "status_description")));
            }

            case "smsimple":
                return await SmsimpleAsync(clients, parameters, "7" + phone, body, cancellationToken).ConfigureAwait(false);

            default:
                return CpSmsHandlerAnswer.Fail("Operator '" + handler + "' is not implemented on ASP.NET yet");
        }
    }

    /// <summary>
    /// <c>smsimple.class.php</c> over XML-RPC: <c>pajm.user.auth</c>, then <c>pajm.sms.send</c> with the session and
    /// <c>signature_id</c>; a fault or a non-empty <c>info</c> is the message.
    /// </summary>
    private static async Task<CpSmsHandlerAnswer> SmsimpleAsync(
        IHttpClientFactory clients,
        IReadOnlyDictionary<string, string> parameters,
        string phone,
        string body,
        CancellationToken cancellationToken)
    {
        string P(string key) => parameters.TryGetValue(key, out var value) ? value : string.Empty;
        var (authOk, auth, authError) = await XmlRpcAsync(
            clients,
            "pajm.user.auth",
            [("username", XmlRpcString(P("login"))), ("password", XmlRpcString(P("password")))],
            cancellationToken).ConfigureAwait(false);
        if (!authOk)
        {
            return CpSmsHandlerAnswer.Fail(authError);
        }

        var session = auth is null ? string.Empty : XmlRpcMember(auth, "session_id");
        if (auth is null || !XmlRpcTruthy(auth))
        {
            return CpSmsHandlerAnswer.Fail("Invalid API username or password");
        }

        var signature = P("signature_id");
        var (sendOk, _, sendError) = await XmlRpcAsync(
            clients,
            "pajm.sms.send",
            [
                ("session_id", XmlRpcString(session)),
                ("origin_id", signature.Length == 0 ? "<value><nil/></value>" : XmlRpcString(signature)),
                ("phone", XmlRpcString(phone)),
                ("message", XmlRpcString(body)),
                ("multiple", "<value><boolean>0</boolean></value>"),
            ],
            cancellationToken).ConfigureAwait(false);
        return sendOk ? new CpSmsHandlerAnswer(true, string.Empty) : CpSmsHandlerAnswer.Fail(sendError);
    }

    private static async Task<(bool Ok, System.Xml.Linq.XElement? Result, string Error)> XmlRpcAsync(
        IHttpClientFactory clients,
        string method,
        IReadOnlyList<(string Name, string Value)> members,
        CancellationToken cancellationToken)
    {
        var request = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?><methodCall><methodName>")
            .Append(System.Security.SecurityElement.Escape(method))
            .Append("</methodName><params><param><value><struct>");
        foreach (var (name, value) in members)
        {
            request.Append("<member><name>").Append(name).Append("</name>").Append(value).Append("</member>");
        }

        request.Append("</struct></value></param></params></methodCall>");
        var reply = await SendHttpAsync(clients, "epc-sms", HttpMethod.Post, "http://api.smsimple.ru/", request.ToString(), "text/xml", [], 25, cancellationToken).ConfigureAwait(false);
        System.Xml.Linq.XElement root;
        try
        {
            root = System.Xml.Linq.XElement.Parse(reply?.Body ?? string.Empty);
        }
        catch (System.Xml.XmlException)
        {
            return (true, null, string.Empty);
        }

        var fault = root.Element("fault");
        if (fault is not null)
        {
            return (false, null, XmlRpcMember(fault.Element("value")?.Element("struct") ?? fault, "faultString"));
        }

        var response = root.Element("params")?.Element("param")?.Element("value")?.Element("struct");
        if (response is null)
        {
            return (true, null, string.Empty);
        }

        var info = XmlRpcMember(response, "info");
        if (info.Length > 0 && info != "0")
        {
            return (false, null, info);
        }

        var result = response.Elements("member").FirstOrDefault(m => (string?)m.Element("name") == "result")?.Element("value");
        return (true, result?.Element("struct") ?? result, string.Empty);
    }

    private static string XmlRpcString(string value)
        => "<value><string>" + System.Security.SecurityElement.Escape(value) + "</string></value>";

    private static string XmlRpcMember(System.Xml.Linq.XElement container, string name)
    {
        var value = container.Elements("member").FirstOrDefault(m => (string?)m.Element("name") == name)?.Element("value");
        return value is null ? string.Empty : value.Value;
    }

    private static bool XmlRpcTruthy(System.Xml.Linq.XElement value)
        => value.Name.LocalName switch
        {
            "struct" => value.Elements("member").Any(),
            _ when value.Element("array") is { } array => array.Element("data")?.Elements("value").Any() == true,
            _ => value.Value is not ("" or "0"),
        };

    private static async Task<Reply?> SendHttpAsync(
        IHttpClientFactory clients,
        string clientName,
        HttpMethod method,
        string url,
        string? content,
        string? contentType,
        IReadOnlyList<(string Name, string Value)> headers,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        if (content is not null)
        {
            request.Content = new StringContent(content, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/x-www-form-urlencoded");
        }

        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            var client = clients.CreateClient(clientName);
            using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var block = new StringBuilder();
            foreach (var header in response.Headers.Concat(response.Content.Headers))
            {
                block.Append(header.Key).Append(": ").Append(string.Join(", ", header.Value)).Append("\r\n");
            }

            return new Reply(
                (int)response.StatusCode,
                response.ReasonPhrase,
                response.Version.Major.ToString(CultureInfo.InvariantCulture) + (response.Version.Major < 2 ? "." + response.Version.Minor.ToString(CultureInfo.InvariantCulture) : string.Empty),
                block.ToString(),
                text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static CpSmsHandlerAnswer MessageOf(JsonElement? value)
        => value is { ValueKind: JsonValueKind.String } text
            ? CpSmsHandlerAnswer.Fail(text.GetString() ?? string.Empty)
            : new CpSmsHandlerAnswer(false, value is { } other ? Concat(other) : string.Empty, MessageJson: value is { } raw ? raw.GetRawText() : "null");

    private static JsonElement? Decode(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? Field(JsonElement? container, string name)
        => container is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(name, out var value) ? value : null;

    private static JsonElement? Index(JsonElement? container, int index)
    {
        if (container is { ValueKind: JsonValueKind.Array } array && array.GetArrayLength() > index)
        {
            return array[index];
        }

        return container is { ValueKind: JsonValueKind.Object } obj ? Field(obj, index.ToString(CultureInfo.InvariantCulture)) : null;
    }

    private static bool First(JsonElement container, out JsonElement first)
    {
        if (container.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in container.EnumerateArray())
            {
                first = item;
                return true;
            }
        }
        else
        {
            foreach (var property in container.EnumerateObject())
            {
                first = property.Value;
                return true;
            }
        }

        first = default;
        return false;
    }

    private static string? Optional(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) ? value : null;

    private static bool IsSet(JsonElement? value) => value is { ValueKind: not JsonValueKind.Null };

    /// <summary>PHP truthiness of a decoded JSON value.</summary>
    public static bool Truthy(JsonElement? value) => value switch
    {
        null => false,
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.Number } n => n.GetDouble() != 0,
        { ValueKind: JsonValueKind.String } s => s.GetString() is { Length: > 0 } t && t != "0",
        { ValueKind: JsonValueKind.Array } a => a.GetArrayLength() > 0,
        { ValueKind: JsonValueKind.Object } o => o.EnumerateObject().Any(),
        _ => false,
    };

    /// <summary>PHP 8 <c>$value == 'literal'</c> for a non-numeric literal.</summary>
    public static bool LooseEqualsString(JsonElement? value, string literal) => value switch
    {
        { ValueKind: JsonValueKind.String } s => s.GetString() == literal,
        { ValueKind: JsonValueKind.True } => literal.Length > 0 && literal != "0",
        { ValueKind: JsonValueKind.False } => literal.Length == 0 || literal == "0",
        null or { ValueKind: JsonValueKind.Null } => literal.Length == 0,
        _ => false,
    };

    /// <summary>PHP 8 <c>$value == 0</c>.</summary>
    public static bool LooseEqualsZero(JsonElement? value) => value switch
    {
        null or { ValueKind: JsonValueKind.Null or JsonValueKind.False } => true,
        { ValueKind: JsonValueKind.Number } n => n.GetDouble() == 0,
        { ValueKind: JsonValueKind.String } s => NumericString(s.GetString(), out var d) ? d == 0 : s.GetString() == "0",
        _ => false,
    };

    /// <summary>PHP 8 <c>$value &gt;= 0</c>.</summary>
    public static bool LooseAtLeastZero(JsonElement? value) => value switch
    {
        null or { ValueKind: JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Array or JsonValueKind.Object } => true,
        { ValueKind: JsonValueKind.Number } n => n.GetDouble() >= 0,
        { ValueKind: JsonValueKind.String } s => NumericString(s.GetString(), out var d) ? d >= 0 : string.CompareOrdinal(s.GetString(), "0") >= 0,
        _ => true,
    };

    /// <summary>PHP string concatenation of a decoded JSON scalar (<c>null</c>/<c>false</c> are empty, <c>true</c> is 1).</summary>
    public static string Concat(JsonElement? value) => value switch
    {
        { ValueKind: JsonValueKind.String } s => s.GetString() ?? string.Empty,
        { ValueKind: JsonValueKind.Number } n => n.TryGetInt64(out var l) ? l.ToString(CultureInfo.InvariantCulture) : n.GetDouble().ToString("G17", CultureInfo.InvariantCulture),
        { ValueKind: JsonValueKind.True } => "1",
        { ValueKind: JsonValueKind.Array or JsonValueKind.Object } => "Array",
        _ => string.Empty,
    };

    private static bool NumericString(string? value, out double number)
    {
        number = 0;
        var trimmed = (value ?? string.Empty).TrimStart(' ', '\t', '\n', '\r', '\v', '\f');
        return trimmed.Length > 0
            && double.TryParse(trimmed.TrimEnd(' ', '\t', '\n', '\r', '\v', '\f'), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>The smsgorod/terasms phone: spaces, <c>+7</c>, brackets, dashes and underscores removed, a leading digit of an 11-digit number dropped, then <c>7</c>.</summary>
    public static string RussianPhone(string phone)
    {
        var value = phone;
        foreach (var token in new[] { " ", "+7", "(", ")", "-", "_" })
        {
            value = value.Replace(token, string.Empty, StringComparison.Ordinal);
        }

        if (Encoding.UTF8.GetByteCount(value) == 11)
        {
            value = value[1..];
        }

        return "7" + value;
    }

    private static string StripWhitespace(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is not (' ' or '\t' or '\n' or '\r'))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public static string PhpTrim(string value) => value.Trim(' ', '\t', '\n', '\r', '\0', '\v');

    private static long PhpIntval(string value)
    {
        var i = 0;
        var negative = false;
        if (i < value.Length && value[i] is '+' or '-')
        {
            negative = value[i] == '-';
            i++;
        }

        long result = 0;
        while (i < value.Length && char.IsAsciiDigit(value[i]))
        {
            result = result * 10 + (value[i] - '0');
            i++;
        }

        return negative ? -result : result;
    }

    /// <summary>PHP <c>rawurlencode</c> / <c>curl_escape</c>.</summary>
    public static string RawUrlEncode(string value)
    {
        var sb = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }

    /// <summary>The PHP <c>json_encode</c> of a handler answer.</summary>
    public static string AnswerJson(CpSmsHandlerAnswer answer)
        => answer.Silent
            ? string.Empty
            : "{\"status\":" + (answer.Status ? "true" : "false") + ",\"message\":" + (answer.MessageJson ?? OAuthStart.PhpJsonString(answer.Message)) + "}";
}
