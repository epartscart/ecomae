using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>One operator credential field of PHP <c>sms_api.parameters</c> (list form or normalised associative map).</summary>
public sealed record CpSmsOperatorField(
    string Name,
    string Type,
    string Caption,
    IReadOnlyList<CpSmsOperatorOption> Options,
    string Value,
    bool IsSecret,
    bool HasStoredValue)
{
    /// <summary>PHP highlights the sender field and pre-fills it with the default From number.</summary>
    public bool IsSender => Name is "sender_number" or "sender" or "from" or "sender_id";
}

public sealed record CpSmsOperatorOption(string Value, string Caption);

public sealed record CpSmsOperatorCard(
    long Id,
    string Name,
    string Handler,
    string Description,
    bool Active,
    bool IsMena,
    IReadOnlyList<CpSmsOperatorField> Fields)
{
    /// <summary>PHP option groups: <c>epc_*</c> handlers are the UAE / GCC / Pakistan block, everything else is legacy.</summary>
    public string Group => IsMena ? "UAE / GCC / Pakistan" : "Other operators";
}

public sealed record CpSmsOperatorsDeskView(
    IReadOnlyList<CpSmsOperatorCard> Operators,
    long SelectedId,
    string DefaultSender,
    string ActiveSender,
    string Source,
    string Error)
{
    public CpSmsOperatorCard? Selected => Operators.FirstOrDefault(o => o.Id == SelectedId);

    public CpSmsOperatorCard? ActiveOperator => Operators.FirstOrDefault(o => o.Active);

    public static CpSmsOperatorsDeskView Empty(string source, string error)
        => new([], 0, CpSmsOperatorsDeskService.DefaultSenderNumber, CpSmsOperatorsDeskService.DefaultSenderNumber, source, error);
}

public interface ICpSmsOperatorsDeskService
{
    Task<CpSmsOperatorsDeskView> LoadAsync(long? selectedId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Typed twin of PHP <c>cp/content/control/sms_turning.php</c>: the <c>sms_api</c> operator catalogue with its
/// JSON <c>parameters</c> descriptors and saved <c>parameters_values</c>, so credentials can be edited in CP
/// instead of only toggling which operator is active. Secret-typed fields are never echoed back to the browser.
/// </summary>
public sealed class CpSmsOperatorsDeskService : ICpSmsOperatorsDeskService
{
    /// <summary>PHP <c>EPC_SMS_DEFAULT_SENDER</c> from <c>content/sms/epc_sms_helpers.php</c>.</summary>
    public const string DefaultSenderNumber = "+971567607011";

    private static readonly string[] SenderKeys = ["sender_number", "from", "sender", "sender_id"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpSmsOperatorsDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>epc_sms_sender_number()</c>: first non-empty sender-ish value, else the default.</summary>
    public static string ResolveSender(IReadOnlyDictionary<string, string> values)
    {
        foreach (var key in SenderKeys)
        {
            if (values.TryGetValue(key, out var candidate) && candidate.Trim().Length > 0)
            {
                return candidate.Trim();
            }
        }

        return DefaultSenderNumber;
    }

    /// <summary>Credential-bearing fields are shown as empty password inputs; the stored value stays in the DB.</summary>
    public static bool IsSecretField(string name, string type)
    {
        if (string.Equals(type, "password", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.Contains("key", StringComparison.OrdinalIgnoreCase)
            || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("token", StringComparison.OrdinalIgnoreCase)
            || name.Contains("password", StringComparison.OrdinalIgnoreCase)
            || name.Contains("appsid", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>PHP normalises an associative <c>{name: caption}</c> map into the list descriptor form.</summary>
    public static IReadOnlyList<CpSmsOperatorField> ParseFields(string? parametersJson, string? valuesJson)
    {
        var values = ParseValues(valuesJson);
        var fields = new List<CpSmsOperatorField>();
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return fields;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(parametersJson);
        }
        catch (JsonException)
        {
            return fields;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    var caption = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? property.Name
                        : property.Name;
                    fields.Add(BuildField(property.Name, "text", caption, [], values));
                }

                return fields;
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                return fields;
            }

            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = ReadString(item, "name");
                if (name.Length == 0)
                {
                    continue;
                }

                var type = ReadString(item, "type");
                if (type.Length == 0)
                {
                    type = "text";
                }

                var caption = ReadString(item, "caption");
                if (caption.Length == 0)
                {
                    caption = name;
                }

                var options = new List<CpSmsOperatorOption>();
                if (item.TryGetProperty("options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var option in optionsElement.EnumerateArray())
                    {
                        if (option.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var optionValue = ReadString(option, "value");
                        var optionCaption = ReadString(option, "caption");
                        options.Add(new CpSmsOperatorOption(optionValue, optionCaption.Length == 0 ? optionValue : optionCaption));
                    }
                }

                fields.Add(BuildField(name, type, caption, options, values));
            }
        }

        return fields;
    }

    public static IReadOnlyDictionary<string, string> ParseValues(string? valuesJson)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(valuesJson))
        {
            return values;
        }

        try
        {
            using var document = JsonDocument.Parse(valuesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "1",
                    JsonValueKind.False => "0",
                    _ => string.Empty,
                };
            }
        }
        catch (JsonException)
        {
            return values;
        }

        return values;
    }

    public async Task<CpSmsOperatorsDeskView> LoadAsync(long? selectedId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpSmsOperatorsDeskView.Empty("unconfigured", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var operators = await ListAsync(connection, cancellationToken).ConfigureAwait(false);
            var active = operators.FirstOrDefault(o => o.Active);
            var selected = selectedId is long id && operators.Any(o => o.Id == id)
                ? id
                : active?.Id ?? 0;
            var activeSender = active is null
                ? DefaultSenderNumber
                : ResolveSender(SenderValues(active));

            return new CpSmsOperatorsDeskView(operators, selected, DefaultSenderNumber, activeSender, "database", string.Empty);
        }
        catch (DbException ex)
        {
            return CpSmsOperatorsDeskView.Empty("database-error", ex.Message);
        }
    }

    private static Dictionary<string, string> SenderValues(CpSmsOperatorCard card)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in card.Fields)
        {
            if (field.IsSender && field.Value.Length > 0)
            {
                values[field.Name] = field.Value;
            }
        }

        return values;
    }

    private static async Task<IReadOnlyList<CpSmsOperatorCard>> ListAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var cards = new List<CpSmsOperatorCard>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`, `name`, `handler`, `description`, `active`, `parameters`, `parameters_values` "
            + "FROM `sms_api` WHERE IFNULL(`control_available`, 0) = ? "
            + "ORDER BY CASE WHEN `handler` LIKE ? THEN 0 ELSE 1 END, `id` ASC");
        ErpDb.AddParameters(command, 1, "epc_%");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var handler = ReadText(reader, 2);
            cards.Add(new CpSmsOperatorCard(
                ReadLong(reader, 0),
                ReadText(reader, 1),
                handler,
                ReadText(reader, 3),
                ReadLong(reader, 4) == 1,
                handler.StartsWith("epc_", StringComparison.Ordinal),
                ParseFields(ReadText(reader, 5), ReadText(reader, 6))));
        }

        return cards;
    }

    private static CpSmsOperatorField BuildField(
        string name,
        string type,
        string caption,
        IReadOnlyList<CpSmsOperatorOption> options,
        IReadOnlyDictionary<string, string> values)
    {
        var stored = values.TryGetValue(name, out var value) ? value : string.Empty;
        var secret = IsSecretField(name, type);
        var visible = secret ? string.Empty : stored;
        if (!secret
            && visible.Length == 0
            && SenderKeys.Contains(name, StringComparer.Ordinal))
        {
            visible = DefaultSenderNumber;
        }

        return new CpSmsOperatorField(name, type, caption, options, visible, secret, stored.Length > 0);
    }

    private static string ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string ReadText(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal)?.ToString() ?? string.Empty;

    private static long ReadLong(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal)
            ? 0
            : long.TryParse(reader.GetValue(ordinal)?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
}
