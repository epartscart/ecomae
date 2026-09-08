using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_uae_fta_fetch_legislation_updates</c> / ajax <c>uae_tax_fta_fetch</c> twin.
/// Fetches tax.gov.ae legislation.aspx, classifies VAT/CT/e-invoice/excise, UPSERTs
/// <c>epc_uae_tax_legislation_items</c> and cache. Does not CREATE tables. PDF/KB seed stay PHP.
/// </summary>
public interface IErpUaeTaxFtaFetchWriteService
{
    Task<ErpUaeTaxFtaFetchWriteResult> FetchAsync(
        ErpUaeTaxFtaFetchWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpUaeTaxFtaFetchWriteRequest(bool Force = false);

public sealed record ErpUaeTaxFtaFetchWriteResult(
    bool Succeeded,
    string Code,
    string Message,
    int Writes,
    int ItemCount,
    int Synced,
    int NewCount,
    int ChangedCount,
    int PageCount,
    bool CacheHit,
    IReadOnlyList<string> Errors)
{
    public static ErpUaeTaxFtaFetchWriteResult Fail(string code, string message, IReadOnlyList<string>? errors = null) =>
        new(false, code, message, 0, 0, 0, 0, 0, 0, false, errors ?? []);

    public static ErpUaeTaxFtaFetchWriteResult FromCache(int itemCount, string message) =>
        new(true, "ok", message, 0, itemCount, 0, 0, 0, 0, true, []);

    public static ErpUaeTaxFtaFetchWriteResult Ok(
        string message,
        int writes,
        int itemCount,
        int synced,
        int newCount,
        int changedCount,
        int pageCount,
        IReadOnlyList<string> errors) =>
        new(true, "ok", message, writes, itemCount, synced, newCount, changedCount, pageCount, false, errors);
}

public sealed class ErpUaeTaxFtaFetchWriteService : IErpUaeTaxFtaFetchWriteService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpUaeFtaHttpClient _http;

    public ErpUaeTaxFtaFetchWriteService(IErpWriteConnectionFactory connections, IErpUaeFtaHttpClient http)
    {
        _connections = connections;
        _http = http;
    }

    public async Task<ErpUaeTaxFtaFetchWriteResult> FetchAsync(
        ErpUaeTaxFtaFetchWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpUaeTaxFtaFetchWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_uae_tax_compliance_cache", "cache_key", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_uae_tax_legislation_items", "item_key", cancellationToken).ConfigureAwait(false))
        {
            return ErpUaeTaxFtaFetchWriteResult.Fail("invalid", "UAE tax legislation tables are not provisioned");
        }

        var url = ErpUaeTaxFtaLegislation.LegislationUrl;
        var cachedJson = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `payload_json` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1"),
            cancellationToken,
            ErpUaeTaxFtaLegislation.CacheKey).ConfigureAwait(false);
        var cachedTime = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `time_fetched` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1"),
            cancellationToken,
            ErpUaeTaxFtaLegislation.CacheKey).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!request.Force
            && cachedTime > 0
            && (now - cachedTime) < ErpUaeTaxFtaLegislation.CacheMaxAgeSeconds
            && ErpUaeTaxFtaLegislation.CacheHasLegislation(cachedJson))
        {
            var count = CountLegislation(cachedJson);
            return ErpUaeTaxFtaFetchWriteResult.FromCache(
                count,
                "Using cached FTA legislation (" + count + " item(s); younger than 24h). Tick Force to refetch.");
        }

        var prevJson = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `payload_json` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1"),
            cancellationToken,
            ErpUaeTaxFtaLegislation.CacheKeyPrev).ConfigureAwait(false);
        var prevKeys = ErpUaeTaxFtaLegislation.PrevKeysFromPayloadJson(prevJson);

        var fetch = await FetchAllPagesAsync(url, cancellationToken).ConfigureAwait(false);
        var raw = fetch.Items.ToList();
        ErpUaeTaxFtaLegislation.MarkDiffFlags(raw, prevKeys);
        var enriched = raw.Select(ErpUaeTaxFtaLegislation.Enrich).ToList();
        var newCount = enriched.Count(i => i.IsNew);
        var changedCount = enriched.Count(i => i.IsChanged || i.IsUpdated);
        var synced = await SyncItemsAsync(connection, enriched, now, cancellationToken).ConfigureAwait(false);
        var overall = ErpUaeTaxFtaLegislation.OverallSummaries(enriched);
        var payload = BuildPayload(enriched, overall, fetch, newCount, changedCount, synced, url, now);

        if (!string.IsNullOrEmpty(cachedJson))
        {
            await UpsertCacheAsync(
                connection,
                ErpUaeTaxFtaLegislation.CacheKeyPrev,
                cachedJson,
                url,
                cachedTime > 0 ? cachedTime : now,
                cancellationToken).ConfigureAwait(false);
        }

        var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);
        var overallJson = JsonSerializer.Serialize(overall, JsonOpts);
        await UpsertCacheAsync(connection, ErpUaeTaxFtaLegislation.CacheKey, payloadJson, url, now, cancellationToken).ConfigureAwait(false);
        await UpsertCacheAsync(connection, ErpUaeTaxFtaLegislation.CacheKeySite, payloadJson, url, now, cancellationToken).ConfigureAwait(false);
        await UpsertCacheAsync(connection, ErpUaeTaxFtaLegislation.CacheKeyOverall, overallJson, url, now, cancellationToken).ConfigureAwait(false);

        var ok = enriched.Count > 0;
        var message = ok
            ? ("Fetched " + enriched.Count + " legislation item(s) from legislation.aspx"
                + (newCount > 0 ? " — " + newCount + " new since last fetch" : "")
                + (changedCount > 0 ? " — " + changedCount + " updated" : "") + ".")
            : ("Could not parse legislation from " + url + ". " + string.Join("; ", fetch.Errors));
        var writes = synced + 3;
        return ok
            ? ErpUaeTaxFtaFetchWriteResult.Ok(message, writes, enriched.Count, synced, newCount, changedCount, fetch.PageCount, fetch.Errors)
            : ErpUaeTaxFtaFetchWriteResult.Fail("empty", message, fetch.Errors);
    }

    internal async Task<(IReadOnlyList<ErpUaeTaxFtaItem> Items, IReadOnlyList<string> Errors, int TotalReported, int PageCount)> FetchAllPagesAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var html = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(html))
        {
            return ([], ["GET failed for " + url], 0, 0);
        }

        var (totalReported, pageCount) = ErpUaeTaxFtaLegislation.ReadPagerMeta(html);
        pageCount = Math.Clamp(pageCount, 1, 50);
        var all = new List<ErpUaeTaxFtaItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in ErpUaeTaxFtaLegislation.ParseListHtml(html))
        {
            if (item.ItemKey.Length > 0 && seen.Add(item.ItemKey))
            {
                all.Add(item);
            }
        }

        var fields = ErpUaeTaxFtaLegislation.ExtractFormFields(html);
        var page = 1;
        while (page < pageCount)
        {
            fields["__EVENTTARGET"] = ErpUaeTaxFtaLegislation.NextEventTarget;
            fields["__EVENTARGUMENT"] = "";
            fields.Remove("ctl00$ctrlContentArea$ctlOpenData$btnSearch");
            html = await _http.PostFormAsync(url, fields, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(html))
            {
                errors.Add("POST page " + (page + 1) + " failed");
                break;
            }

            page++;
            foreach (var item in ErpUaeTaxFtaLegislation.ParseListHtml(html))
            {
                if (item.ItemKey.Length > 0 && seen.Add(item.ItemKey))
                {
                    all.Add(item);
                }
            }

            fields = ErpUaeTaxFtaLegislation.ExtractFormFields(html);
        }

        return (all, errors, totalReported, pageCount);
    }

    private static async Task<int> SyncItemsAsync(
        DbConnection connection,
        IReadOnlyList<ErpUaeTaxFtaItem> items,
        long now,
        CancellationToken cancellationToken)
    {
        var n = 0;
        foreach (var item in items)
        {
            if (item.ItemKey.Length == 0) continue;
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

    private static async Task UpsertCacheAsync(
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

    private static Dictionary<string, object?> BuildPayload(
        IReadOnlyList<ErpUaeTaxFtaItem> legislation,
        Dictionary<string, object> overall,
        (IReadOnlyList<ErpUaeTaxFtaItem> Items, IReadOnlyList<string> Errors, int TotalReported, int PageCount) fetch,
        int newCount,
        int changedCount,
        int synced,
        string url,
        long now)
    {
        var rows = legislation.Select(ToDict).ToList();
        return new Dictionary<string, object?>
        {
            ["ok"] = legislation.Count > 0,
            ["status"] = legislation.Count > 0,
            ["time_fetched"] = now,
            ["source_url"] = url,
            ["total_reported"] = fetch.TotalReported,
            ["page_count"] = fetch.PageCount,
            ["legislation"] = rows,
            ["items"] = rows,
            ["overall_summaries"] = overall,
            ["items_synced"] = synced,
            ["new_count"] = newCount,
            ["changed_count"] = changedCount,
            ["summaries_need_regen"] = false,
            ["errors"] = fetch.Errors,
        };
    }

    private static Dictionary<string, object?> ToDict(ErpUaeTaxFtaItem item) => new()
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

    private static int CountLegislation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("legislation", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return arr.GetArrayLength();
            }

            if (doc.RootElement.TryGetProperty("items", out arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return arr.GetArrayLength();
            }
        }
        catch (JsonException)
        {
            return 0;
        }

        return 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
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
}
