using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Shared load / UPSERT helpers for UAE tax legislation Ask and Regen.
/// Does not CREATE tables. PDF excerpt / KB seed stay PHP.
/// </summary>
public static class ErpUaeTaxLegislationLibrary
{
    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static async Task<IReadOnlyList<ErpUaeTaxFtaItem>> LoadSearchItemsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var items = new List<ErpUaeTaxFtaItem>();
        if (await ColumnExistsAsync(connection, "epc_uae_tax_legislation_items", "item_key", cancellationToken).ConfigureAwait(false))
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT `item_key`, `slug`, `title`, `issue_date`, `publish_date`, `category`, `tax_category`, `pdf_url`,
                       `erp_summary`, `compliance_actions_json`, `pattern_key`, `is_new`, `is_updated`
                  FROM `epc_uae_tax_legislation_items`
                 ORDER BY `issue_date` DESC, `id` ASC
                """;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var raw = FromReader(reader);
                if (string.IsNullOrWhiteSpace(raw.ErpSummary) || raw.ComplianceActions is null || raw.ComplianceActions.Count == 0)
                {
                    items.Add(ErpUaeTaxFtaLegislation.Enrich(raw));
                }
                else
                {
                    items.Add(raw);
                }
            }
        }

        if (items.Count > 0)
        {
            return items;
        }

        if (!await ColumnExistsAsync(connection, "epc_uae_tax_compliance_cache", "cache_key", cancellationToken).ConfigureAwait(false))
        {
            return items;
        }

        var cachedJson = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `payload_json` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1"),
            cancellationToken,
            ErpUaeTaxFtaLegislation.CacheKey).ConfigureAwait(false);
        foreach (var raw in ErpUaeTaxFtaLegislation.ItemsFromCacheJson(cachedJson))
        {
            items.Add(string.IsNullOrWhiteSpace(raw.ErpSummary) || raw.ComplianceActions is null || raw.ComplianceActions.Count == 0
                ? ErpUaeTaxFtaLegislation.Enrich(raw)
                : raw);
        }

        return items;
    }

    public static async Task<int> SyncItemsAsync(
        DbConnection connection,
        IReadOnlyList<ErpUaeTaxFtaItem> items,
        long now,
        CancellationToken cancellationToken)
    {
        var n = 0;
        foreach (var item in items)
        {
            if (item.ItemKey.Length == 0)
            {
                continue;
            }

            var actions = JsonSerializer.Serialize(item.ComplianceActions ?? [], JsonOpts);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_uae_tax_legislation_items`
                    (`item_key`, `slug`, `title`, `issue_date`, `publish_date`, `category`, `tax_category`, `pdf_url`,
                     `erp_summary`, `compliance_actions_json`, `pattern_key`, `is_new`, `is_updated`, `time_synced`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    ON DUPLICATE KEY UPDATE
                     `slug` = VALUES(`slug`), `title` = VALUES(`title`), `issue_date` = VALUES(`issue_date`),
                     `publish_date` = VALUES(`publish_date`), `category` = VALUES(`category`), `tax_category` = VALUES(`tax_category`),
                     `pdf_url` = VALUES(`pdf_url`), `erp_summary` = VALUES(`erp_summary`),
                     `compliance_actions_json` = VALUES(`compliance_actions_json`), `pattern_key` = VALUES(`pattern_key`),
                     `is_new` = VALUES(`is_new`), `is_updated` = VALUES(`is_updated`), `time_synced` = VALUES(`time_synced`)
                    """),
                cancellationToken,
                item.ItemKey,
                item.Slug,
                item.Title,
                item.IssueDate,
                item.PublishDate,
                item.Category,
                item.TaxCategory,
                item.PdfUrl,
                item.ErpSummary,
                actions,
                item.PatternKey,
                item.IsNew ? 1 : 0,
                (item.IsUpdated || item.IsChanged) ? 1 : 0,
                now).ConfigureAwait(false);
            n++;
        }

        return n;
    }

    public static async Task UpsertCacheAsync(
        DbConnection connection,
        string cacheKey,
        string payloadJson,
        string url,
        long timeFetched,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_uae_tax_compliance_cache` (`cache_key`, `payload_json`, `source_url`, `time_fetched`)
                VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `payload_json` = VALUES(`payload_json`), `source_url` = VALUES(`source_url`), `time_fetched` = VALUES(`time_fetched`)
                """),
            cancellationToken,
            cacheKey,
            payloadJson,
            url,
            timeFetched).ConfigureAwait(false);
    }

    public static Dictionary<string, object?> ItemToDict(ErpUaeTaxFtaItem item) => new()
    {
        ["slug"] = item.Slug,
        ["item_key"] = item.ItemKey,
        ["title"] = item.Title,
        ["issue_date"] = item.IssueDate,
        ["publish_date"] = item.PublishDate,
        ["category"] = item.Category,
        ["tax_category"] = item.TaxCategory,
        ["pdf_url"] = item.PdfUrl,
        ["pattern_key"] = item.PatternKey,
        ["erp_summary"] = item.ErpSummary,
        ["compliance_actions"] = item.ComplianceActions ?? [],
        ["is_new"] = item.IsNew,
        ["is_updated"] = item.IsUpdated || item.IsChanged,
        ["is_changed"] = item.IsChanged,
    };

    public static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }

    static ErpUaeTaxFtaItem FromReader(DbDataReader reader)
    {
        var actions = ParseActionsJson(ReadString(reader, "compliance_actions_json"));
        return new ErpUaeTaxFtaItem(
            Slug: ReadString(reader, "slug"),
            ItemKey: ReadString(reader, "item_key"),
            Title: ReadString(reader, "title"),
            IssueDate: ReadString(reader, "issue_date"),
            PublishDate: ReadString(reader, "publish_date"),
            Category: ReadString(reader, "category"),
            TaxCategory: ReadString(reader, "tax_category"),
            PdfUrl: ReadString(reader, "pdf_url"),
            IsNew: ReadBool(reader, "is_new"),
            IsUpdated: ReadBool(reader, "is_updated"),
            PatternKey: ReadString(reader, "pattern_key"),
            ErpSummary: ReadString(reader, "erp_summary"),
            ComplianceActions: actions);
    }

    static IReadOnlyList<string> ParseActionsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return doc.RootElement.EnumerateArray()
                .Select(a => a.ValueKind == JsonValueKind.String ? a.GetString() ?? "" : a.ToString())
                .Where(a => a.Length > 0)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    static string ReadString(DbDataReader reader, string name)
    {
        var i = reader.GetOrdinal(name);
        if (reader.IsDBNull(i))
        {
            return "";
        }

        return Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "";
    }

    static bool ReadBool(DbDataReader reader, string name)
    {
        var i = reader.GetOrdinal(name);
        if (reader.IsDBNull(i))
        {
            return false;
        }

        var value = reader.GetValue(i);
        return value switch
        {
            bool b => b,
            byte n => n != 0,
            sbyte n => n != 0,
            short n => n != 0,
            int n => n != 0,
            long n => n != 0,
            _ => !string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), "0", StringComparison.Ordinal)
                && !string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), "false", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture))
        };
    }
}
