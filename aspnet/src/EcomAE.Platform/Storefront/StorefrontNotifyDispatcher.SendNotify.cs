using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/notifications/send_notify.php</c>, the HTTP form of the dispatcher that the password recovery page,
/// the contact confirmation script and the <c>send_notify()</c> fallback post to. Unlike
/// the inline <c>docpart_dispatch_notification()</c> it uses the bare translations, gates e-mail as well as SMS on
/// <c>status_ref</c>, marks SMS as tried without an active operator, upserts <c>debug_results</c>, and echoes
/// <c>persons</c> with the status fields added.
/// </summary>
public sealed partial class StorefrontNotifyDispatcher
{
    public const string SendNotifyPath = "/content/notifications/send_notify.php";

    private static readonly string[] StatusRefNames =
        ["order_status_to_manager", "order_status_to_customer", "order_item_status_to_manager", "order_item_status_to_customer"];

    /// <summary>The <c>json_encode($answer)</c> text, or <c>null</c> where PHP 8 stops with a fatal error.</summary>
    public async Task<string?> SendNotifyHttpAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> post,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(config);

        var translator = new StorefrontPhpTranslator(connection);
        if (!post.ContainsKey("name") && !post.ContainsKey("vars") && !post.ContainsKey("persons"))
        {
            return Failure(await translator.RawAsync("4072", cancellationToken).ConfigureAwait(false));
        }

        var vars = DecodeArray(post.GetValueOrDefault("vars"));
        var persons = DecodeArray(post.GetValueOrDefault("persons"));
        if (vars is null && persons is null)
        {
            return Failure(await translator.RawAsync("4073", cancellationToken).ConfigureAwait(false));
        }

        Dictionary<string, string>? row = null;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT * FROM `notifications_settings` WHERE `name` = ?");
            ErpDb.AddParameters(command, post.TryGetValue("name", out var posted) ? posted : DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
        }
        catch (DbException)
        {
            return null;
        }

        if (row is null)
        {
            return Failure(await translator.RawAsync("4074", cancellationToken).ConfigureAwait(false));
        }

        if (persons is null)
        {
            return null;
        }

        var name = row.GetValueOrDefault("name") ?? string.Empty;
        var textVars = new Dictionary<string, string>(StringComparer.Ordinal);
        if (vars is JsonObject varsObject)
        {
            foreach (var pair in varsObject)
            {
                textVars[pair.Key] = PhpString(pair.Value);
            }
        }

        var varsJson = row.GetValueOrDefault("vars") ?? string.Empty;
        var subject = Render(await translator.TextAsync(row.GetValueOrDefault("email_subject"), cancellationToken).ConfigureAwait(false), varsJson, textVars);
        var body = Render(await translator.TextAsync(row.GetValueOrDefault("email_body"), cancellationToken).ConfigureAwait(false), varsJson, textVars);
        var smsBody = Render(await translator.TextAsync(row.GetValueOrDefault("sms_body"), cancellationToken).ConfigureAwait(false), varsJson, textVars);
        var sendForNotConfirmed = Truthy(row.GetValueOrDefault("send_for_not_confirmed"));
        var emailOn = !LooseZero(row.GetValueOrDefault("email_on"));
        var smsOn = !LooseZero(row.GetValueOrDefault("sms_on"));
        var (emailAllowed, smsAllowed) = HttpStatusRefAllows(name, vars, config);
        var smsApi = await ActiveSmsApiAsync(connection, cancellationToken).ConfigureAwait(false);
        var waNotify = new StorefrontWhatsappNotify(name, emailOn, smsOn);
        string? wrapped = null;

        var list = persons is JsonArray array ? array : new JsonArray(((JsonObject)persons).Select(p => p.Value?.DeepClone()).ToArray());
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is null)
            {
                list[i] = new JsonObject();
            }

            if (list[i] is not JsonObject person)
            {
                return null;
            }

            var type = person["type"] is JsonValue t ? PhpString(t) : string.Empty;
            if (type == StorefrontNotifyPerson.UserIdType)
            {
                person["contacts"] = new JsonObject { ["phone"] = new JsonObject(), ["email"] = new JsonObject() };
            }

            if (person["contacts"] is not JsonObject contacts)
            {
                contacts = new JsonObject();
                person["contacts"] = contacts;
            }

            var emailNode = Channel(contacts, "email");
            emailNode["tried_to_send"] = false;
            emailNode["email_confirmed"] = false;
            emailNode["status"] = false;
            var phoneNode = Channel(contacts, "phone");
            phoneNode["tried_to_send"] = false;
            phoneNode["phone_confirmed"] = false;
            phoneNode["status"] = false;

            var email = string.Empty;
            JsonNode emailConfirmed = false;
            var emailToSend = false;
            var phone = string.Empty;
            JsonNode phoneConfirmed = false;
            var phoneToSend = false;
            if (type == StorefrontNotifyPerson.UserIdType)
            {
                var user = await UserRowAsync(connection, person["user_id"], cancellationToken).ConfigureAwait(false);
                if (user is not null)
                {
                    if (!PhpEmpty(user.GetValueOrDefault("email")) && (!PhpEmpty(user.GetValueOrDefault("email_confirmed")) || sendForNotConfirmed))
                    {
                        email = PhpString(user["email"]);
                        emailConfirmed = user.GetValueOrDefault("email_confirmed")?.DeepClone() ?? JsonValue.Create(string.Empty);
                        emailToSend = true;
                    }

                    if (!PhpEmpty(user.GetValueOrDefault("phone")) && (!PhpEmpty(user.GetValueOrDefault("phone_confirmed")) || sendForNotConfirmed))
                    {
                        phone = PhpString(user["phone"]);
                        phoneConfirmed = user.GetValueOrDefault("phone_confirmed")?.DeepClone() ?? JsonValue.Create(string.Empty);
                        phoneToSend = true;
                    }
                }
            }
            else if (type == StorefrontNotifyPerson.DirectContactType)
            {
                if (!PhpEmpty(emailNode["value"]) && sendForNotConfirmed)
                {
                    email = PhpString(emailNode["value"]);
                    emailToSend = true;
                }

                if (!PhpEmpty(phoneNode["value"]) && sendForNotConfirmed)
                {
                    phone = PhpString(phoneNode["value"]);
                    phoneToSend = true;
                }
            }

            emailToSend &= emailOn && emailAllowed;
            phoneToSend &= smsOn && smsAllowed;

            if (emailToSend)
            {
                emailNode["tried_to_send"] = true;
                emailNode["email_confirmed"] = emailConfirmed.DeepClone();
                wrapped ??= await WrapAsync(connection, translator, subject, body, cancellationToken).ConfigureAwait(false);
                var sent = await _mailer.SendHtmlAsync(email, subject, wrapped, cancellationToken).ConfigureAwait(false);
                emailNode["status"] = sent.Ok;
                await RecordDebugAsync(connection, "email", sent.Ok, sent.Message, cancellationToken).ConfigureAwait(false);
            }

            if (phoneToSend)
            {
                phoneNode["tried_to_send"] = true;
                phoneNode["phone_confirmed"] = phoneConfirmed.DeepClone();
                phone = SmsPhone(phone);
                bool ok;
                string debug;
                if (smsApi is { } api)
                {
                    var sms = _sms is null
                        ? CpSmsSendOutcome.Fail(string.Empty)
                        : await _sms.SendAsync(api.Handler, api.Parameters, phone, smsBody, cancellationToken, CpSmsHandlerContext.For(connection, config)).ConfigureAwait(false);
                    ok = sms.Ok;
                    debug = ok
                        ? await translator.TextAsync(4075, cancellationToken).ConfigureAwait(false)
                        : sms.Message.Length > 0 ? sms.Message : await translator.TextAsync(4076, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ok = false;
                    debug = await translator.TextAsync(4077, cancellationToken).ConfigureAwait(false);
                }

                phoneNode["status"] = ok;
                await RecordDebugAsync(connection, "sms", ok, debug, cancellationToken).ConfigureAwait(false);
            }

            if (contacts["whatsapp"] is null)
            {
                contacts["whatsapp"] = new JsonObject { ["tried_to_send"] = false, ["status"] = false, ["error"] = string.Empty };
            }

            if (phone.Length > 0 && _whatsapp is not null)
            {
                var wa = await _whatsapp.DispatchForPersonAsync(connection, config, waNotify, textVars, smsBody, body, phone, cancellationToken).ConfigureAwait(false);
                contacts["whatsapp"] = new JsonObject { ["tried_to_send"] = wa.TriedToSend, ["status"] = wa.Status, ["error"] = wa.Error };
            }
        }

        var json = new StringBuilder("{\"status\":true,\"message\":\"\",\"persons\":");
        PhpEncode(list, json);
        return json.Append('}').ToString();
    }

    /// <summary>
    /// PHP <c>isset($DP_Config->orders_statuses_notifications_settings)</c> and <c>$vars['status_ref'][key] == 0</c>
    /// for the four status notifications: a missing flag counts as 0, so it stops that channel.
    /// </summary>
    private static (bool Email, bool Sms) HttpStatusRefAllows(string name, JsonNode? vars, IReadOnlyDictionary<string, string> config)
    {
        if (!config.ContainsKey("orders_statuses_notifications_settings") || Array.IndexOf(StatusRefNames, name) < 0)
        {
            return (true, true);
        }

        var who = name.EndsWith("_to_manager", StringComparison.Ordinal) ? "manager" : "customer";
        var reference = vars is JsonObject o ? o["status_ref"] as JsonObject : null;
        return (!LooseZero(reference?["to_" + who + "_email"]), !LooseZero(reference?["to_" + who + "_sms"]));
    }

    private static async Task RecordDebugAsync(DbConnection connection, string channel, bool ok, string detail, CancellationToken cancellationToken)
    {
        try
        {
            await CpCommunicationsTestService.RecordDebugAsync(connection, channel, ok, detail, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    /// <summary>The <c>users</c> row as PHP 8.1+ PDO fetches it: integer columns as numbers, the rest as strings, SQL NULL as null.</summary>
    private static async Task<Dictionary<string, JsonNode?>?> UserRowAsync(DbConnection connection, JsonNode? userId, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT * FROM `users` WHERE `user_id` = ?");
            ErpDb.AddParameters(command, userId is null ? DBNull.Value : PhpString(userId));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var row = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i)
                    ? null
                    : reader.GetValue(i) switch
                    {
                        sbyte or byte or short or ushort or int or uint or long => JsonValue.Create(Convert.ToInt64(reader.GetValue(i), CultureInfo.InvariantCulture)),
                        ulong u => JsonValue.Create(u),
                        var other => JsonValue.Create(Convert.ToString(other, CultureInfo.InvariantCulture) ?? string.Empty),
                    };
            }

            return row;
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static JsonObject Channel(JsonObject contacts, string key)
    {
        if (contacts[key] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        contacts[key] = created;
        return created;
    }

    private static string Failure(string? message)
        => "{\"status\":false,\"message\":" + (message is null ? "null" : OAuthStart.PhpJsonString(message)) + "}";

    /// <summary><c>json_decode($raw, true)</c> when the result is a PHP array; scalars, invalid JSON and a missing field are <c>null</c>.</summary>
    private static JsonNode? DecodeArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(raw) is { } node && node is JsonObject or JsonArray ? node : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>PHP string conversion of a decoded JSON value (<c>true</c> is "1", <c>false</c> and <c>null</c> are "", arrays "Array").</summary>
    private static string PhpString(JsonNode? node) => node switch
    {
        null => string.Empty,
        JsonObject or JsonArray => "Array",
        JsonValue value => value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Number => value.ToJsonString(),
            JsonValueKind.True => "1",
            _ => string.Empty,
        },
        _ => string.Empty,
    };

    /// <summary>PHP <c>empty()</c>.</summary>
    private static bool PhpEmpty(JsonNode? node) => node switch
    {
        null => true,
        JsonObject o => o.Count == 0,
        JsonArray a => a.Count == 0,
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.String => PhpEmpty(v.GetValue<string>()),
            JsonValueKind.Number => v.ToJsonString().Trim('0', '.', '-').Length == 0,
            JsonValueKind.True => false,
            _ => true,
        },
        _ => true,
    };

    private static bool PhpEmpty(string? value) => string.IsNullOrEmpty(value) || value == "0";

    private static bool Truthy(string? value) => !PhpEmpty(value);

    /// <summary>PHP 8 <c>$value == 0</c> for a column string (SQL NULL reads as "") or a decoded JSON value.</summary>
    private static bool LooseZero(string? value)
        => string.IsNullOrEmpty(value) || (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == 0);

    private static bool LooseZero(JsonNode? node) => node switch
    {
        null => true,
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.String => double.TryParse(v.GetValue<string>().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d == 0
                : v.GetValue<string>() == "0",
            JsonValueKind.Number => v.GetValue<double>() == 0,
            JsonValueKind.False or JsonValueKind.Null => true,
            _ => false,
        },
        _ => false,
    };

    /// <summary>PHP <c>json_encode()</c>: <c>\/</c> and <c>\uXXXX</c> escapes, and an empty PHP array as <c>[]</c>.</summary>
    private static void PhpEncode(JsonNode? node, StringBuilder json)
    {
        switch (node)
        {
            case null:
                json.Append("null");
                break;
            case JsonObject o when o.Count == 0:
                json.Append("[]");
                break;
            case JsonObject o:
                json.Append('{');
                var first = true;
                foreach (var pair in o)
                {
                    json.Append(first ? string.Empty : ",").Append(OAuthStart.PhpJsonString(pair.Key)).Append(':');
                    PhpEncode(pair.Value, json);
                    first = false;
                }

                json.Append('}');
                break;
            case JsonArray a:
                json.Append('[');
                for (var i = 0; i < a.Count; i++)
                {
                    json.Append(i == 0 ? string.Empty : ",");
                    PhpEncode(a[i], json);
                }

                json.Append(']');
                break;
            case JsonValue v:
                json.Append(v.GetValueKind() switch
                {
                    JsonValueKind.String => OAuthStart.PhpJsonString(v.GetValue<string>()),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Number => v.ToJsonString(),
                    _ => "null",
                });
                break;
        }
    }
}
