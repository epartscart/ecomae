using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>storage.php</c> create / edit twins.</summary>
public interface ICpStorageWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        string? name,
        string? shortName,
        int currency,
        int interfaceType,
        string? usersJson,
        string? connectionOptionsJson,
        string? handlerFolder,
        int hidden,
        int bgLineColor,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> UpdateAsync(
        long storageId,
        string? name,
        string? shortName,
        int currency,
        int interfaceType,
        string? usersJson,
        string? connectionOptionsJson,
        string? handlerFolder,
        int hidden,
        int bgLineColor,
        CancellationToken cancellationToken = default);
}

public sealed class CpStorageWriteService : ICpStorageWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpStorageWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public Task<ErpSimpleWriteResult> CreateAsync(
        string? name,
        string? shortName,
        int currency,
        int interfaceType,
        string? usersJson,
        string? connectionOptionsJson,
        string? handlerFolder,
        int hidden,
        int bgLineColor,
        CancellationToken cancellationToken = default)
        => SaveAsync(
            0,
            name,
            shortName,
            currency,
            interfaceType,
            usersJson,
            connectionOptionsJson,
            handlerFolder,
            hidden,
            bgLineColor,
            cancellationToken);

    public Task<ErpSimpleWriteResult> UpdateAsync(
        long storageId,
        string? name,
        string? shortName,
        int currency,
        int interfaceType,
        string? usersJson,
        string? connectionOptionsJson,
        string? handlerFolder,
        int hidden,
        int bgLineColor,
        CancellationToken cancellationToken = default)
    {
        if (storageId <= 0)
        {
            return Task.FromResult(ErpSimpleWriteResult.Fail("invalid", "A warehouse id is required."));
        }

        return SaveAsync(
            storageId,
            name,
            shortName,
            currency,
            interfaceType,
            usersJson,
            connectionOptionsJson,
            handlerFolder,
            hidden,
            bgLineColor,
            cancellationToken);
    }

    private async Task<ErpSimpleWriteResult> SaveAsync(
        long storageId,
        string? name,
        string? shortName,
        int currency,
        int interfaceType,
        string? usersJson,
        string? connectionOptionsJson,
        string? handlerFolder,
        int hidden,
        int bgLineColor,
        CancellationToken cancellationToken)
    {
        var caption = HtmlEntities((name ?? string.Empty).Trim());
        if (caption.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Warehouse name is required.");
        }

        if (caption.Length > 255)
        {
            caption = caption[..255];
        }

        var shortCaption = HtmlEntities((shortName ?? string.Empty).Trim());
        if (shortCaption.Length > 64)
        {
            shortCaption = shortCaption[..64];
        }

        var users = NormalizeUsers(usersJson);
        if (users.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", users.Error);
        }

        var options = NormalizeConnectionOptions(connectionOptionsJson, handlerFolder);
        if (options.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", options.Error);
        }

        var type = interfaceType > 0 ? interfaceType : 1;
        var currencyId = currency > 0 ? currency : 1;
        var hiddenFlag = hidden == 1 ? 1 : 0;

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (storageId == 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `shop_storages` (`name`, `interface_type`, `users`, `connection_options`, `currency`, `short_name`, `hidden`, `bg_line_color`)
                    VALUES (?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                caption, type, users.Json, options.Json, currencyId, shortCaption, hiddenFlag, bgLineColor).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return id > 0
                ? new ErpSimpleWriteResult(true, "ok", "Warehouse created.", id, 1)
                : ErpSimpleWriteResult.Fail("invalid", "Could not create the warehouse.");
        }

        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                UPDATE `shop_storages`
                SET `name` = ?, `interface_type` = ?, `users` = ?, `connection_options` = ?,
                    `currency` = ?, `short_name` = ?, `hidden` = ?, `bg_line_color` = ?
                WHERE `id` = ?
                """),
            cancellationToken,
            caption, type, users.Json, options.Json, currencyId, shortCaption, hiddenFlag, bgLineColor, storageId).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok("Warehouse saved.", storageId)
            : ErpSimpleWriteResult.Fail("not_found", "Warehouse was not updated.");
    }

    /// <summary>PHP <c>storage.php</c> connection_options trim / probability / ABCP subdomain.</summary>
    public static (string Json, string? Error) NormalizeConnectionOptions(string? raw, string? handlerFolder)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ("{}", null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                return ("", "Connection options must be a JSON object.");
            }

            var abcp = string.Equals((handlerFolder ?? string.Empty).Trim(), "abcp", StringComparison.OrdinalIgnoreCase);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return (doc.RootElement.GetRawText(), null);
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    WriteNormalizedValue(writer, prop.Name, prop.Value, abcp);
                }

                writer.WriteEndObject();
            }

            return (System.Text.Encoding.UTF8.GetString(stream.ToArray()), null);
        }
        catch (JsonException)
        {
            return ("", "Connection options JSON is not valid.");
        }
    }

    public static (string Json, string? Error) NormalizeUsers(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ("[]", null);
        }

        if (text[0] != '[')
        {
            var ids = new List<long>();
            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
                {
                    ids.Add(id);
                }
            }

            return (JsonSerializer.Serialize(ids.Distinct().Take(80)), null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ("", "Storekeepers must be a JSON array of user ids.");
            }

            var ids = new List<long>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var id = item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var n)
                    ? n
                    : long.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : 0;
                if (id > 0)
                {
                    ids.Add(id);
                }
            }

            return (JsonSerializer.Serialize(ids.Distinct().Take(80)), null);
        }
        catch (JsonException)
        {
            return ("", "Storekeepers JSON is not valid.");
        }
    }

    private static void WriteNormalizedValue(Utf8JsonWriter writer, string key, JsonElement value, bool abcp)
    {
        if (value.ValueKind == JsonValueKind.String || value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            var text = value.ValueKind == JsonValueKind.String
                ? (value.GetString() ?? string.Empty)
                : value.ToString();
            text = text.Trim();
            if (string.Equals(key, "probability", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("%", string.Empty, StringComparison.Ordinal);
            }

            if (abcp && string.Equals(key, "subdomain", StringComparison.OrdinalIgnoreCase))
            {
                text = text.ToLowerInvariant()
                    .Replace("http://", string.Empty, StringComparison.Ordinal)
                    .Replace("https://", string.Empty, StringComparison.Ordinal)
                    .Replace(".public.api.abcp.ru", string.Empty, StringComparison.Ordinal)
                    .Replace("/", string.Empty, StringComparison.Ordinal);
            }

            writer.WriteStringValue(text);
            return;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            writer.WriteNullValue();
            return;
        }

        value.WriteTo(writer);
    }

    private static string HtmlEntities(string value)
        => WebUtility.HtmlEncode(value);
}
