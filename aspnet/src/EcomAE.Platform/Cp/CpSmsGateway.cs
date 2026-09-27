using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Typed twin of the PHP GCC/MENA SMS handlers (<c>content/sms/handlers/epc_*/send_sms.php</c>) and their shared
/// helpers in <c>content/sms/epc_sms_helpers.php</c>: sender precedence, MSISDN normalisation, the partner JSON
/// payload shape and the success parsing. Credentials come from <c>sms_api.parameters_values</c> and are never
/// echoed back to the browser.
/// </summary>
public sealed record CpSmsSendOutcome(bool Ok, string Message)
{
    public static CpSmsSendOutcome Fail(string message) => new(false, message);
}

public interface ICpSmsGateway
{
    Task<CpSmsSendOutcome> SendAsync(
        string handler,
        IReadOnlyDictionary<string, string> parameters,
        string phone,
        string body,
        CancellationToken cancellationToken = default);
}

public static class CpSmsMsisdn
{
    /// <summary>PHP <c>EPC_SMS_DEFAULT_SENDER</c>.</summary>
    public const string DefaultSender = "+971567607011";

    private static readonly Regex NonDial = new(@"[^\d+]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex UaeLocal = new(@"^05\d{8}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex UaeShort = new(@"^5\d{8}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PakLocal = new(@"^03\d{9}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PakShort = new(@"^3\d{9}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>PHP <c>epc_sms_normalize_msisdn()</c>.</summary>
    public static string Normalize(string phone, string defaultCountry = "AE")
    {
        var value = NonDial.Replace((phone ?? string.Empty).Trim(), string.Empty);
        if (value.StartsWith('+'))
        {
            value = value[1..];
        }

        if (value.StartsWith("00", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (UaeLocal.IsMatch(value))
        {
            return "971" + value[1..];
        }

        if (UaeShort.IsMatch(value) && defaultCountry == "AE")
        {
            return "971" + value;
        }

        if (PakLocal.IsMatch(value))
        {
            return "92" + value[1..];
        }

        if (PakShort.IsMatch(value) && defaultCountry == "PK")
        {
            return "92" + value;
        }

        return value;
    }

    /// <summary>PHP <c>epc_sms_sender_number()</c>.</summary>
    public static string Sender(IReadOnlyDictionary<string, string> parameters)
    {
        foreach (var key in CpCommunicationsDeskService.SenderKeys)
        {
            if (parameters.TryGetValue(key, out var value) && value.Trim().Length > 0)
            {
                return value.Trim();
            }
        }

        return DefaultSender;
    }

    /// <summary>PHP handler success parsing (<c>success</c> flag, textual/numeric <c>status</c>, Pakistan <c>code</c>).</summary>
    public static bool Succeeded(bool httpOk, JsonElement? json, bool allowCode)
    {
        if (json is not { ValueKind: JsonValueKind.Object } root)
        {
            return httpOk;
        }

        if (root.TryGetProperty("success", out var success))
        {
            return success.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => success.GetDouble() != 0,
                JsonValueKind.String => Truthy(success.GetString()),
                _ => false,
            };
        }

        if (root.TryGetProperty("status", out var status))
        {
            return status.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => status.GetDouble() == 1,
                JsonValueKind.String => Truthy(status.GetString()) || (allowCode && status.GetString() == "0"),
                _ => false,
            };
        }

        if (allowCode && root.TryGetProperty("code", out var code))
        {
            var text = code.ValueKind == JsonValueKind.Number
                ? code.GetDouble().ToString(CultureInfo.InvariantCulture)
                : code.GetString() ?? string.Empty;
            return text is "0" or "00";
        }

        return httpOk;
    }

    public static bool UnifonicSucceeded(bool httpOk, JsonElement? json)
    {
        if (!httpOk || json is not { ValueKind: JsonValueKind.Object } root
            || !root.TryGetProperty("success", out var success))
        {
            return false;
        }

        return success.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => success.GetDouble() != 0,
            JsonValueKind.String => Truthy(success.GetString()),
            _ => false,
        };
    }

    private static bool Truthy(string? value)
        => value is "success" or "ok" or "true" or "1";
}

public sealed class CpSmsGateway : ICpSmsGateway
{
    private readonly IHttpClientFactory _clients;

    public CpSmsGateway(IHttpClientFactory clients)
    {
        _clients = clients;
    }

    /// <summary>Handlers implemented natively on ASP.NET; every other PHP handler stays on the Classic twin.</summary>
    public static IReadOnlyList<string> NativeHandlers { get; } =
        ["epc_unifonic", "epc_etisalat", "epc_du", "epc_pakistan"];

    public async Task<CpSmsSendOutcome> SendAsync(
        string handler,
        IReadOnlyDictionary<string, string> parameters,
        string phone,
        string body,
        CancellationToken cancellationToken = default)
    {
        var slug = (handler ?? string.Empty).Trim().ToLowerInvariant();
        if (!NativeHandlers.Contains(slug))
        {
            return CpSmsSendOutcome.Fail(
                "Operator '" + slug + "' is not implemented on ASP.NET yet — send the test from the Classic twin.");
        }

        var message = (body ?? string.Empty).Trim();
        var to = CpSmsMsisdn.Normalize(phone ?? string.Empty, slug == "epc_pakistan" ? "PK" : "AE");
        if (to.Length == 0 || message.Length == 0)
        {
            return CpSmsSendOutcome.Fail("Recipient and message body are required");
        }

        var sender = CpSmsMsisdn.Sender(parameters);
        var apiKey = Value(parameters, "api_key");
        var apiSecret = Value(parameters, "api_secret");
        var apiUrl = Value(parameters, "api_url");

        Dictionary<string, object?> payload;
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var allowCode = false;

        switch (slug)
        {
            case "epc_unifonic":
            {
                var appSid = Value(parameters, "appsid");
                if (appSid.Length == 0)
                {
                    appSid = apiKey;
                }

                if (appSid.Length == 0)
                {
                    return CpSmsSendOutcome.Fail("Unifonic AppSid / API key is required");
                }

                if (apiUrl.Length == 0)
                {
                    apiUrl = "https://el.cloud.unifonic.com/rest/SMS/messages";
                }

                payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["AppSid"] = appSid,
                    ["SenderID"] = sender.TrimStart('+'),
                    ["Recipient"] = to,
                    ["Body"] = message,
                };
                break;
            }

            case "epc_pakistan":
            {
                var username = Value(parameters, "username");
                var password = Value(parameters, "password");
                if (apiUrl.Length == 0)
                {
                    return CpSmsSendOutcome.Fail("Pakistan SMS API URL is required");
                }

                if (apiKey.Length == 0 && (username.Length == 0 || password.Length == 0))
                {
                    return CpSmsSendOutcome.Fail("Provide api_key or username/password for the Pakistan gateway");
                }

                allowCode = true;
                payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["api_key"] = apiKey,
                    ["username"] = username,
                    ["password"] = password,
                    ["from"] = sender,
                    ["sender"] = sender.TrimStart('+'),
                    ["to"] = to,
                    ["mobilenum"] = to,
                    ["text"] = message,
                    ["message"] = message,
                    ["msg"] = message,
                };
                if (apiKey.Length > 0)
                {
                    headers["Authorization"] = "Bearer " + apiKey;
                }

                break;
            }

            default:
            {
                var label = slug == "epc_du" ? "du" : "Etisalat";
                if (apiUrl.Length == 0)
                {
                    return CpSmsSendOutcome.Fail(label + " API URL is required (from your partner contract)");
                }

                if (apiKey.Length == 0)
                {
                    return CpSmsSendOutcome.Fail(label + " API key is required");
                }

                payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["api_key"] = apiKey,
                    ["from"] = sender,
                    ["sender"] = sender,
                    ["to"] = to,
                    ["recipient"] = to,
                    ["text"] = message,
                    ["body"] = message,
                    ["message"] = message,
                };
                if (apiSecret.Length > 0)
                {
                    payload["api_secret"] = apiSecret;
                }

                var authHeader = Value(parameters, "auth_header");
                if (authHeader.Length > 0)
                {
                    headers["Authorization"] = authHeader;
                }
                else if (apiSecret.Length > 0)
                {
                    headers["Authorization"] = "Bearer " + apiKey;
                }

                break;
            }
        }

        if (!await IsSafeApiUrlAsync(apiUrl, cancellationToken).ConfigureAwait(false))
        {
            return CpSmsSendOutcome.Fail("SMS gateway URL must be a public HTTPS endpoint.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = JsonContent.Create(payload),
        };
        foreach (var header in headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var client = _clients.CreateClient("epc-sms");
        client.Timeout = TimeSpan.FromSeconds(25);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return CpSmsSendOutcome.Fail("SMS gateway is unreachable: " + ex.Message);
        }
        catch (TaskCanceledException)
        {
            return CpSmsSendOutcome.Fail("SMS gateway timed out");
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            JsonElement? json = null;
            try
            {
                using var document = JsonDocument.Parse(text.Length == 0 ? "{}" : text);
                json = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                json = null;
            }

            var succeeded = slug == "epc_unifonic"
                ? CpSmsMsisdn.UnifonicSucceeded(response.IsSuccessStatusCode, json)
                : CpSmsMsisdn.Succeeded(response.IsSuccessStatusCode, json, allowCode);
            if (succeeded)
            {
                return new CpSmsSendOutcome(true, string.Empty);
            }

            var detail = json is { ValueKind: JsonValueKind.Object } obj
                && obj.TryGetProperty("message", out var m)
                && m.ValueKind == JsonValueKind.String
                    ? m.GetString() ?? string.Empty
                    : text.Length > 240 ? text[..240] : text;
            var http = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            return CpSmsSendOutcome.Fail(
                SanitizeProviderDetail(detail.Length > 0 ? detail : "SMS send failed (HTTP " + http + ")"));
        }
    }

    private static string SanitizeProviderDetail(string value)
    {
        var sanitized = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return sanitized.Length > 240 ? sanitized[..240] : sanitized;
    }

    private static async Task<bool> IsSafeApiUrlAsync(string value, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.Port is not (-1 or 443))
        {
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            return IsPublicAddress(literal);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken).ConfigureAwait(false);
            return addresses.Length > 0 && addresses.All(IsPublicAddress);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool IsPublicAddress(IPAddress address)
        => !IPAddress.IsLoopback(address)
           && !address.Equals(IPAddress.Any)
           && !address.Equals(IPAddress.IPv6Any)
           && !address.Equals(IPAddress.Broadcast)
           && !address.Equals(IPAddress.IPv6None)
           && !address.IsIPv6LinkLocal
           && !address.IsIPv6SiteLocal
           && !IsPrivateAddress(address);

    private static bool IsPrivateAddress(IPAddress address)
    {
        var bytes = address.MapToIPv4().GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 127);
    }

    private static string Value(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
}
