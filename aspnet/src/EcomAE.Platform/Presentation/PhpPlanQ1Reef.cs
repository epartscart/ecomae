using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-reef CP prices manager performance helpers. PHP identifiers kept for the inventory:
/// <c>epc_prices_is_platform_operator_request</c>, <c>epc_prices_is_large_tenant_host</c>,
/// <c>epc_prices_should_run_tables_cleaner</c>, <c>epc_prices_add_index_if_missing</c>,
/// <c>epc_prices_ensure_listing_indexes</c>, <c>epc_prices_table_has_column</c>,
/// <c>epc_prices_live_counts_map</c>, <c>epc_prices_persist_records_counts</c>,
/// <c>epc_prices_fetch_lists_query</c>, <c>epc_prices_fetch_lists_rows</c>,
/// <c>epc_prices_defer_inline_update_history</c>, <c>epc_prices_external_poll_interval_ms</c>,
/// <c>epc_pyprices_health_check</c>.
/// </summary>
public static class PhpPlanQ1Reef
{
    public const string PricesPerfPath = "cp/content/shop/prices_upload/epc_prices_manager_perf.php";

    private static readonly Regex Ident = new("^[a-zA-Z0-9_]+$", RegexOptions.CultureInvariant);

    public static Dictionary<string, string> Server { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, object?> Get { get; set; } = new(StringComparer.Ordinal);
    public static Func<bool>? IsPlatformHostname { get; set; }
    public static Func<long> UnixNow { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static string TempDir { get; set; } = Path.GetTempPath();
    public static Func<string, string, int, HealthHit>? HealthHttp { get; set; }

    private static bool _indexPassDone;
    private static Dictionary<string, bool> _columnCache = new(StringComparer.Ordinal);

    public sealed record HealthHit(bool BodyOk, string Body, string Error);

    public sealed class PriceRow
    {
        public int Id { get; set; }
        public int RecordsCount { get; set; }
        public Dictionary<string, object?> Extra { get; } = new(StringComparer.Ordinal);
    }

    public sealed class ReefStore
    {
        public string DatabaseName { get; set; } = "db";
        public bool PricesMissing { get; set; }
        public bool DataMissing { get; set; }
        public bool CronMissing { get; set; }
        public bool HasRecordsCount { get; set; } = true;
        public HashSet<string> Indexes { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Columns { get; } = new(StringComparer.Ordinal);
        public List<PriceRow> Prices { get; } = [];
        public List<int> PriceDataIds { get; } = [];
        public List<int> CronPriceIds { get; } = [];
        public string LastSql { get; set; } = "";
        public List<string> Alters { get; } = [];
        public string? LastCacheFile { get; set; }
    }

    private static ReefStore Store { get; set; } = new();

    public static void UseStore(ReefStore store) => Store = store;

    public static void Reset()
    {
        Server = new(StringComparer.Ordinal);
        Get = new(StringComparer.Ordinal);
        IsPlatformHostname = null;
        UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        TempDir = Path.GetTempPath();
        HealthHttp = null;
        _indexPassDone = false;
        _columnCache = new(StringComparer.Ordinal);
        Store = new();
    }

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            0d => true,
            0f => true,
            "" => true,
            "0" => true,
            JsonElement je when je.ValueKind is JsonValueKind.Null or JsonValueKind.False => true,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0 => true,
            JsonElement je when je.ValueKind == JsonValueKind.String && (je.GetString() is "" or "0") => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static int PhpInt(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case bool b:
                return b ? 1 : 0;
            case int n:
                return n;
            case long l:
                return (int)l;
            case double d:
                return (int)d;
            default:
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                _ = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
                return parsed;
        }
    }

    private static string Host()
    {
        var host = (Server.TryGetValue("HTTP_HOST", out var h) ? h : "").ToLowerInvariant();
        var colon = host.IndexOf(':');
        return colon >= 0 ? host[..colon] : host;
    }

    private static string PypricesApiName()
        => "pyprices-api" + "." + "php";

    public static bool EpcPricesIsPlatformOperatorRequest()
    {
        if (IsPlatformHostname != null && IsPlatformHostname())
        {
            return true;
        }

        var host = Host();
        return host is "www.ecomae.com" or "ecomae.com" or "cp.ecomae.com";
    }

    public static bool EpcPricesIsLargeTenantHost()
    {
        var host = Host();
        if (host == "")
        {
            return false;
        }

        return host.Contains("epartscart", StringComparison.Ordinal) || EpcPricesIsPlatformOperatorRequest();
    }

    public static bool EpcPricesShouldRunTablesCleaner()
        => Get.ContainsKey("epc_clean_pyprices");

    public static void EpcPricesAddIndexIfMissing(ReefStore db, string table, string indexName, string columnsSpec)
    {
        if (!Ident.IsMatch(table) || !Ident.IsMatch(indexName))
        {
            return;
        }

        var key = table + "." + indexName;
        if (db.Indexes.Contains(key))
        {
            return;
        }

        db.Alters.Add("ALTER TABLE `" + table + "` ADD INDEX `" + indexName + "` " + columnsSpec);
        db.Indexes.Add(key);
    }

    public static void EpcPricesEnsureListingIndexes(ReefStore db, bool allowAlter = false)
    {
        if (_indexPassDone)
        {
            return;
        }

        _indexPassDone = true;
        if (!allowAlter)
        {
            return;
        }

        string dbName;
        try
        {
            if (db.PricesMissing && db.DataMissing)
            {
                throw new InvalidOperationException("missing");
            }

            dbName = db.DatabaseName;
        }
        catch (Exception)
        {
            dbName = "db";
        }

        var cacheFile = TempDir.TrimEnd('/') + "/epc_prices_listing_idx_" + Md5Hex(dbName) + ".ok";
        db.LastCacheFile = cacheFile;
        if (File.Exists(cacheFile) && UnixNow() - new DateTimeOffset(File.GetLastWriteTimeUtc(cacheFile)).ToUnixTimeSeconds() < 86400)
        {
            return;
        }

        bool needData;
        bool needCron;
        try
        {
            if (db.DataMissing)
            {
                throw new InvalidOperationException("missing data");
            }

            needData = !db.Indexes.Contains("shop_docpart_prices_data.x_price_id");
            needCron = !db.Indexes.Contains("shop_docpart_pyprices_crontab_prices.x_price_id");
        }
        catch (Exception)
        {
            return;
        }

        if (needData)
        {
            EpcPricesAddIndexIfMissing(db, "shop_docpart_prices_data", "x_price_id", "(`price_id`)");
        }

        if (needCron)
        {
            EpcPricesAddIndexIfMissing(db, "shop_docpart_pyprices_crontab_prices", "x_price_id", "(`price_id`)");
        }

        File.WriteAllText(cacheFile, "ok");
    }

    public static bool EpcPricesTableHasColumn(ReefStore db, string table, string column)
    {
        var key = table + "." + column;
        if (_columnCache.TryGetValue(key, out var hit))
        {
            return hit;
        }

        if (!Ident.IsMatch(table) || !Ident.IsMatch(column))
        {
            _columnCache[key] = false;
            return false;
        }

        try
        {
            if (db.PricesMissing && table == "shop_docpart_prices")
            {
                throw new InvalidOperationException("missing");
            }

            var ok = db.Columns.Contains(key) || (table == "shop_docpart_prices" && column == "records_count" && db.HasRecordsCount);
            _columnCache[key] = ok;
            return ok;
        }
        catch (Exception)
        {
            _columnCache[key] = false;
            return false;
        }
    }

    public static Dictionary<int, int>? EpcPricesLiveCountsMap(ReefStore db)
    {
        try
        {
            if (db.DataMissing)
            {
                throw new InvalidOperationException("missing");
            }

            var map = new Dictionary<int, int>();
            foreach (var pid in db.PriceDataIds)
            {
                map[pid] = map.TryGetValue(pid, out var n) ? n + 1 : 1;
            }

            return map;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void EpcPricesPersistRecordsCounts(ReefStore db, Dictionary<int, int> counts)
    {
        if (!EpcPricesTableHasColumn(db, "shop_docpart_prices", "records_count"))
        {
            return;
        }

        try
        {
            if (db.PricesMissing)
            {
                throw new InvalidOperationException("missing");
            }

            foreach (var kv in counts)
            {
                var row = db.Prices.FirstOrDefault(p => p.Id == kv.Key);
                if (row != null)
                {
                    row.RecordsCount = kv.Value;
                }
            }
        }
        catch (Exception)
        {
        }
    }

    public static List<Dictionary<string, object?>> EpcPricesFetchListsQuery(ReefStore db)
    {
        var hasRecordsCount = EpcPricesTableHasColumn(db, "shop_docpart_prices", "records_count");
        Exception? last = null;
        try
        {
            if (db.PricesMissing)
            {
                throw new InvalidOperationException("prices");
            }

            if (!db.CronMissing)
            {
                db.LastSql = "cron_join";
                return ProjectPrices(db, hasRecordsCount, withCron: true);
            }

            throw new InvalidOperationException("cron");
        }
        catch (Exception ex)
        {
            last = ex;
        }

        try
        {
            if (db.PricesMissing)
            {
                throw new InvalidOperationException("prices");
            }

            db.LastSql = "prices_only";
            return ProjectPrices(db, hasRecordsCount, withCron: false);
        }
        catch (Exception ex)
        {
            last = ex;
        }

        try
        {
            if (db.PricesMissing)
            {
                throw last ?? new InvalidOperationException("prices");
            }

            db.LastSql = "last_resort";
            return ProjectPrices(db, hasRecordsCount: false, withCron: false);
        }
        catch (Exception)
        {
            throw last ?? new InvalidOperationException("prices");
        }
    }

    public static List<Dictionary<string, object?>> EpcPricesFetchListsRows(ReefStore db)
    {
        var rows = EpcPricesFetchListsQuery(db);
        if (rows.Count == 0)
        {
            return rows;
        }

        var allZero = true;
        foreach (var row in rows)
        {
            if (PhpInt(row.TryGetValue("records_count", out var rc) ? rc : 0) > 0)
            {
                allZero = false;
                break;
            }
        }

        if (!allZero)
        {
            return rows;
        }

        var live = EpcPricesLiveCountsMap(db);
        if (live == null || live.Count == 0)
        {
            return rows;
        }

        foreach (var row in rows)
        {
            var pid = PhpInt(row.TryGetValue("id", out var id) ? id : 0);
            if (pid > 0 && live.TryGetValue(pid, out var cnt))
            {
                row["records_count"] = cnt;
            }
        }

        EpcPricesPersistRecordsCounts(db, live);
        return rows;
    }

    public static bool EpcPricesDeferInlineUpdateHistory()
        => true;

    public static int EpcPricesExternalPollIntervalMs()
        => EpcPricesIsLargeTenantHost() ? 20000 : 8000;

    public static Dictionary<string, object?> EpcPypricesHealthCheck(string domainPath, string techKey, int timeoutSec = 5)
    {
        var url = domainPath.TrimEnd('/') + "/pyprices/" + PypricesApiName();
        var post = "key=" + Uri.EscapeDataString(techKey) + "&just_test_db=yes";
        var hit = HealthHttp?.Invoke(url, post, timeoutSec)
            ?? new HealthHit(false, "", "curl_init failed");
        if (!hit.BodyOk || hit.Body == "")
        {
            return new(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = hit.Error != "" ? hit.Error : "Empty response",
                ["raw"] = ""
            };
        }

        Dictionary<string, object?>? json = null;
        try
        {
            using var doc = JsonDocument.Parse(hit.Body.Trim());
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                json = doc.RootElement.EnumerateObject().ToDictionary(
                    p => p.Name,
                    p => (object?)(p.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number when p.Value.TryGetInt32(out var n) => n,
                        JsonValueKind.String => p.Value.GetString() ?? "",
                        _ => p.Value.ToString()
                    }),
                    StringComparer.Ordinal);
            }
        }
        catch (JsonException)
        {
            json = null;
        }

        if (json != null && !PhpEmpty(json.TryGetValue("status", out var st) ? st : null))
        {
            return new(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["message"] = Convert.ToString(json.TryGetValue("message", out var msg) ? msg : "OK", CultureInfo.InvariantCulture) ?? "OK"
            };
        }

        return new(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["message"] = json != null
                ? Convert.ToString(json.TryGetValue("message", out var fail) ? fail : "pyprices check failed", CultureInfo.InvariantCulture) ?? "pyprices check failed"
                : Clip(hit.Body, 200),
            ["raw"] = Clip(hit.Body, 500)
        };
    }

    private static List<Dictionary<string, object?>> ProjectPrices(ReefStore db, bool hasRecordsCount, bool withCron)
    {
        var cronCounts = new Dictionary<int, int>();
        if (withCron)
        {
            foreach (var pid in db.CronPriceIds)
            {
                cronCounts[pid] = cronCounts.TryGetValue(pid, out var n) ? n + 1 : 1;
            }
        }

        var rows = new List<Dictionary<string, object?>>();
        foreach (var price in db.Prices.OrderBy(p => p.Id))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = price.Id,
                ["records_count"] = hasRecordsCount ? price.RecordsCount : 0,
                ["cron_tasks_count"] = withCron && cronCounts.TryGetValue(price.Id, out var c) ? c : 0
            };
            foreach (var kv in price.Extra)
            {
                row[kv.Key] = kv.Value;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static string Md5Hex(string value)
    {
        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
