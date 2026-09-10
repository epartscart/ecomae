using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_6_complete_session</c> last_updated / records_count twin
/// and <c>min_price_acl_save</c> / <c>epc_mv_min_price_acl_save</c> UPSERT.
/// CSV import, file ingest, and sitemap stay Classic. This service does not invent a send.
/// </summary>
public interface ICpPricesUploadWriteService
{
    Task<ErpSimpleWriteResult> CompleteSessionAsync(long priceId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveMinPriceAclAsync(
        string? restrictRaw,
        string? groupIds,
        string? userIds,
        long updatedBy,
        CancellationToken cancellationToken = default);
}

public sealed class CpPricesUploadWriteService : ICpPricesUploadWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPricesUploadWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CompleteSessionAsync(
        long priceId,
        CancellationToken cancellationToken = default)
    {
        if (priceId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A price list id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            stamp, priceId);

        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?"),
                cancellationToken,
                priceId);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_docpart_prices` SET `records_count` = ? WHERE `id` = ?"),
                cancellationToken,
                count, priceId);
        }
        catch (Exception)
        {
            // PHP ignores a missing records_count column.
        }

        return ErpSimpleWriteResult.Ok("Price list session completed.", priceId);
    }

    public async Task<ErpSimpleWriteResult> SaveMinPriceAclAsync(
        string? restrictRaw,
        string? groupIds,
        string? userIds,
        long updatedBy,
        CancellationToken cancellationToken = default)
    {
        var restrict = ParseRestrict(restrictRaw) ? 1 : 0;
        var groups = ParseIdList(groupIds);
        var users = ParseIdList(userIds);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_mv_min_price_acl`
                     (`id`,`restrict_min`,`group_ids_json`,`user_ids_json`,`updated_at`,`updated_by`)
                     VALUES (1,?,?,?,?,?)
                     ON DUPLICATE KEY UPDATE
                     `restrict_min`=VALUES(`restrict_min`),
                     `group_ids_json`=VALUES(`group_ids_json`),
                     `user_ids_json`=VALUES(`user_ids_json`),
                     `updated_at`=VALUES(`updated_at`),
                     `updated_by`=VALUES(`updated_by`)
                    """),
                cancellationToken,
                restrict,
                JsonSerializer.Serialize(groups),
                JsonSerializer.Serialize(users),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Math.Max(0, updatedBy));
            return ErpSimpleWriteResult.Ok("Minimum price access saved", 1);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Min-price ACL table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP: restrict unless the raw value is 0 / false / no / off.</summary>
    public static bool ParseRestrict(string? raw)
    {
        var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return key is not ("0" or "false" or "no" or "off");
    }

    /// <summary>PHP group_ids / user_ids JSON array or comma/space split; positive ints only.</summary>
    public static IReadOnlyList<long> ParseIdList(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [];
        }

        var parsed = new List<long>();
        if (text.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        AddId(parsed, item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var n)
                            ? n
                            : long.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromText)
                                ? fromText
                                : 0);
                    }

                    return parsed;
                }
            }
            catch (JsonException)
            {
            }
        }

        foreach (var part in text.Split([',', ' ', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                AddId(parsed, id);
            }
        }

        return parsed;
    }

    private static void AddId(List<long> parsed, long id)
    {
        if (id <= 0 || parsed.Contains(id))
        {
            return;
        }

        if (parsed.Count >= 80)
        {
            return;
        }

        parsed.Add(id);
    }
}
