using System.Data.Common;
using System.Globalization;
using System.Text;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// <c>/content/sms/handlers/&lt;handler&gt;/send_sms.php</c>: the POST the notification dispatcher, password recovery and
/// contact confirmation send to the SMS operator. <c>check</c> must be the <c>secret_succession</c>; the operator row
/// comes from <c>sms_api</c> by handler (not only the active one), and the answer is the handler's JSON.
/// </summary>
public static class StorefrontSmsHandlers
{
    public const string PathPattern = "/content/sms/handlers/{handler}/send_sms.php";

    private static readonly Dictionary<string, string> GccLabels = new(StringComparer.Ordinal)
    {
        ["epc_unifonic"] = "Unifonic operator not configured",
        ["epc_etisalat"] = "Etisalat operator not configured",
        ["epc_du"] = "du operator not configured",
        ["epc_pakistan"] = "Pakistan SMS operator not configured",
    };

    public sealed record Answer(string Body, string ContentType);

    public static bool IsHandler(string handler)
        => GccLabels.ContainsKey(handler) || CpSmsLegacyOperators.Handlers.Contains(handler);

    /// <param name="connection">The tenant database, or <c>null</c> when it could not be opened.</param>
    public static async Task<Answer> RunAsync(
        string handler,
        IReadOnlyDictionary<string, string> post,
        IReadOnlyDictionary<string, string> config,
        DbConnection? connection,
        ICpSmsGateway gateway,
        IHttpClientFactory clients,
        CancellationToken cancellationToken)
    {
        var secret = config.TryGetValue("secret_succession", out var s) ? s : string.Empty;
        string? Post(string key) => post.TryGetValue(key, out var value) ? value : null;

        if (GccLabels.TryGetValue(handler, out var notConfigured))
        {
            if (connection is null)
            {
                return Gcc(false, "Database error");
            }

            var check = Post("check") ?? string.Empty;
            if (secret.Length == 0 || check.Length == 0 || check != secret)
            {
                return Gcc(false, "Forbidden");
            }

            var stored = await OperatorAsync(connection, handler, cancellationToken).ConfigureAwait(false);
            if (stored is null)
            {
                return Gcc(false, notConfigured);
            }

            var parameters = new Dictionary<string, string>(stored, StringComparer.Ordinal);
            if (Post("parameters_values") is { Length: > 0 } posted && posted != "0")
            {
                foreach (var (key, value) in CpCommunicationsTestService.ParseParameters(posted))
                {
                    parameters[key] = value;
                }
            }

            var sent = await gateway.SendAsync(handler, parameters, Post("main_field") ?? string.Empty, Post("body") ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return Gcc(sent.Ok, sent.Ok ? string.Empty : sent.Message);
        }

        if (connection is null)
        {
            return Legacy(CpSmsHandlerAnswer.Fail("Error"));
        }

        if (!LooseEquals(Post("check"), secret))
        {
            return Legacy(CpSmsHandlerAnswer.Fail("Forbidden"));
        }

        var values = await OperatorAsync(connection, handler, cancellationToken).ConfigureAwait(false) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var answer = await CpSmsLegacyOperators.SendAsync(
            clients,
            handler,
            values,
            Post("main_field") ?? string.Empty,
            Post("body") ?? string.Empty,
            CpSmsHandlerContext.For(connection, config),
            cancellationToken).ConfigureAwait(false);
        return Legacy(answer);
    }

    private static async Task<IReadOnlyDictionary<string, string>?> OperatorAsync(DbConnection connection, string handler, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT IFNULL(`parameters_values`, '') FROM `sms_api` WHERE `handler` = ? LIMIT 1");
        ErpDb.AddParameters(command, handler);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return CpCommunicationsTestService.ParseParameters(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static Answer Legacy(CpSmsHandlerAnswer answer)
        => new(CpSmsLegacyOperators.AnswerJson(answer), "text/html; charset=utf-8");

    /// <summary>PHP <c>epc_sms_exit_json</c>: <c>JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES</c>.</summary>
    private static Answer Gcc(bool ok, string message)
        => new("{\"status\":" + (ok ? "true" : "false") + ",\"message\":" + UnescapedJsonString(message) + "}", "application/json; charset=utf-8");

    private static string UnescapedJsonString(string value)
    {
        var sb = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20 || c is '\u2028' or '\u2029')
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

        return sb.Append('"').ToString();
    }

    /// <summary>PHP 8 <c>$_POST['check'] == $secret</c>: a missing field equals only an empty secret, numeric strings compare as numbers.</summary>
    public static bool LooseEquals(string? posted, string secret)
    {
        if (posted is null)
        {
            return secret.Length == 0;
        }

        if (Numeric(posted, out var a) && Numeric(secret, out var b))
        {
            return a == b;
        }

        return string.Equals(posted, secret, StringComparison.Ordinal);
    }

    private static bool Numeric(string value, out double number)
    {
        number = 0;
        var trimmed = value.TrimStart(' ', '\t', '\n', '\r', '\v', '\f').TrimEnd(' ', '\t', '\n', '\r', '\v', '\f');
        return trimmed.Length > 0
            && (char.IsAsciiDigit(trimmed[^1]) || trimmed[^1] == '.')
            && double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}
