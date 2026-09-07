using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>filter/ajax_operations.php</c> add / save / del / active / active_all / save_storages twins.</summary>
public interface ICpProductFilterWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(string? manufacturer, string? article, string? name, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(long id, string? manufacturer, string? article, string? name, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetActiveAsync(long id, int flag, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetActiveAllAsync(int flag, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveStoragesAsync(
        long id,
        string? storagesJson,
        string? minPrice,
        string? maxPrice,
        string? minTime,
        string? maxTime,
        CancellationToken cancellationToken = default);
}

public sealed class CpProductFilterWriteService : ICpProductFilterWriteService
{
    private const int MaxText = 255;
    private const int MaxStorages = 200;
    private readonly IErpWriteConnectionFactory _connections;

    public CpProductFilterWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        string? manufacturer,
        string? article,
        string? name,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var brand = NormalizeManufacturer(manufacturer);
        var sku = NormalizeArticle(article);
        var caption = NormalizeName(name);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_docpart_filter` (`manufacturer`, `article`, `name`, `list_storages`, `min_price`, `max_price`, `min_time`, `max_time`, `active`) VALUES (?, ?, ?, '[]', 0, 0, 0, 0, 1)"),
            cancellationToken,
            brand,
            sku,
            caption).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Product filter added.", id);
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        string? manufacturer,
        string? article,
        string? name,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A product filter id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var brand = NormalizeManufacturer(manufacturer);
        var sku = NormalizeArticle(article);
        var caption = NormalizeName(name);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_filter` SET `manufacturer` = ?, `article` = ?, `name` = ? WHERE `id` = ?"),
            cancellationToken,
            brand,
            sku,
            caption,
            id).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok("Product filter saved.", id)
            : ErpSimpleWriteResult.Fail("not_found", "Product filter was not updated.");
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A product filter id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_docpart_filter` WHERE `id` = ?"),
            cancellationToken,
            id).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok("Product filter deleted.", id)
            : ErpSimpleWriteResult.Fail("not_found", "Product filter was not found.");
    }

    public async Task<ErpSimpleWriteResult> SetActiveAsync(long id, int flag, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A product filter id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var on = flag == 1 ? 1 : 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_filter` SET `active` = ? WHERE `id` = ?"),
            cancellationToken,
            on,
            id).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok(on == 1 ? "Product filter activated." : "Product filter deactivated.", id)
            : ErpSimpleWriteResult.Fail("not_found", "Product filter was not updated.");
    }

    public async Task<ErpSimpleWriteResult> SetActiveAllAsync(int flag, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var on = flag == 1 ? 1 : 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_filter` SET `active` = ?"),
            cancellationToken,
            on).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(on == 1 ? "All product filters activated." : "All product filters deactivated.", 0);
    }

    public async Task<ErpSimpleWriteResult> SaveStoragesAsync(
        long id,
        string? storagesJson,
        string? minPrice,
        string? maxPrice,
        string? minTime,
        string? maxTime,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A product filter id is required.");
        }

        var storages = ParseStorages(storagesJson);
        if (storages.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", storages.Error);
        }

        if (!TryParseMoney(minPrice, out var loPrice, out var priceError)
            || !TryParseMoney(maxPrice, out var hiPrice, out priceError))
        {
            return ErpSimpleWriteResult.Fail("invalid", priceError ?? "Price range is invalid.");
        }

        if (!TryParseTime(minTime, out var loTime, out var timeError)
            || !TryParseTime(maxTime, out var hiTime, out timeError))
        {
            return ErpSimpleWriteResult.Fail("invalid", timeError ?? "Time range is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `shop_docpart_filter` SET `list_storages` = ?, `min_price` = ?, `max_price` = ?, `min_time` = ?, `max_time` = ? WHERE `id` = ?"),
            cancellationToken,
            storages.Json,
            loPrice,
            hiPrice,
            loTime,
            hiTime,
            id).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok("Product filter scope saved.", id)
            : ErpSimpleWriteResult.Fail("not_found", "Product filter was not updated.");
    }

    /// <summary>PHP article strip: keep letters/digits then upper.</summary>
    public static string NormalizeArticle(string? article)
        => EpcPricing.NormalizeArticle(article);

    /// <summary>PHP <c>mb_strtoupper(trim)</c> for manufacturer.</summary>
    public static string NormalizeManufacturer(string? manufacturer)
    {
        var text = (manufacturer ?? string.Empty).Trim().ToUpperInvariant();
        return text.Length <= MaxText ? text : text[..MaxText];
    }

    public static string NormalizeName(string? name)
    {
        var text = (name ?? string.Empty).Trim();
        return text.Length <= MaxText ? text : text[..MaxText];
    }

    public static (string Json, string? Error) ParseStorages(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0 || text is "[]" or "null")
        {
            return ("[]", null);
        }

        if (text[0] != '[')
        {
            var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var fromCsv = new List<long>();
            foreach (var part in parts)
            {
                if (!long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
                {
                    return ("", "list_storages must be a JSON array of storage ids.");
                }

                if (!fromCsv.Contains(id))
                {
                    fromCsv.Add(id);
                }

                if (fromCsv.Count > MaxStorages)
                {
                    return ("", "Too many storage ids.");
                }
            }

            return (JsonSerializer.Serialize(fromCsv), null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ("", "list_storages must be a JSON array of storage ids.");
            }

            var ids = new List<long>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                long id;
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out id))
                {
                }
                else if (item.ValueKind == JsonValueKind.String
                    && long.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                {
                }
                else
                {
                    return ("", "list_storages must be a JSON array of storage ids.");
                }

                if (id <= 0)
                {
                    continue;
                }

                if (!ids.Contains(id))
                {
                    ids.Add(id);
                }

                if (ids.Count > MaxStorages)
                {
                    return ("", "Too many storage ids.");
                }
            }

            return (JsonSerializer.Serialize(ids), null);
        }
        catch (JsonException)
        {
            return ("", "list_storages is not valid JSON.");
        }
    }

    public static bool TryParseMoney(string? raw, out decimal value, out string? error)
    {
        value = 0;
        error = null;
        var text = (raw ?? string.Empty).Trim().Replace(',', '.');
        if (text.Length == 0)
        {
            return true;
        }

        if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value) || value < 0)
        {
            error = "Price must be zero or greater.";
            value = 0;
            return false;
        }

        if (value > 999999999m)
        {
            error = "Price is too large.";
            value = 0;
            return false;
        }

        return true;
    }

    public static bool TryParseTime(string? raw, out int value, out string? error)
    {
        value = 0;
        error = null;
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0)
        {
            error = "Time must be zero or greater.";
            value = 0;
            return false;
        }

        if (value > 100000)
        {
            error = "Time is too large.";
            value = 0;
            return false;
        }

        return true;
    }

    public static int ParseFlag(string? raw, int fallback)
    {
        var text = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (text is "1" or "true" or "on" or "yes" or "active")
        {
            return 1;
        }

        if (text is "0" or "false" or "off" or "no")
        {
            return 0;
        }

        return fallback == 1 ? 1 : 0;
    }
}
