using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-safe helpers. PHP identifiers kept for the inventory:
/// <c>epc_sitemap_warehouse_cache_dir</c>, <c>epc_sitemap_warehouse_shard_size</c>,
/// <c>epc_sitemap_warehouse_max_shards</c>, <c>epc_sitemap_warehouse_stale_path</c>,
/// <c>epc_sitemap_warehouse_is_stale</c>, <c>epc_sitemap_warehouse_mark_stale</c>,
/// <c>epc_sitemap_warehouse_clear_stale</c>, <c>epc_sitemap_warehouse_cache_path</c>,
/// <c>epc_sitemap_warehouse_public_path</c>, <c>epc_sitemap_warehouse_meta_path</c>,
/// <c>epc_sitemap_warehouse_meta_read</c>, <c>epc_sitemap_warehouse_meta_write</c>,
/// <c>epc_sitemap_warehouse_price_filters</c>, <c>epc_sitemap_warehouse_url_xml</c>,
/// <c>epc_sitemap_warehouse_write_shard_file</c>, <c>epc_sitemap_warehouse_regenerate_shard</c>,
/// <c>epc_sitemap_warehouse_meta_refresh_from_files</c>,
/// <c>epc_sitemap_warehouse_existing_shard_count</c>,
/// <c>epc_sitemap_warehouse_regenerate_all</c>, <c>epc_sitemap_warehouse_serve_cached</c>,
/// <c>epc_sitemap_warehouse_estimate_shards</c>,
/// <c>epc_tdp_check_access</c>, <c>epc_tdp_enforce_access</c>,
/// <c>epc_tdp_log_access</c>, <c>epc_tdp_log_violation</c>,
/// <c>epc_tdp_redact_sensitive</c>, <c>epc_tdp_apply_security_headers</c>,
/// <c>epc_tdp_verify_isolation</c>, <c>epc_tdp_verify_all_tenants</c>,
/// <c>epc_tdp_retention_policy</c>, <c>epc_tdp_classify_data</c>,
/// <c>epc_tdp_ensure_audit_table</c>, <c>epc_tdp_client_ip</c>,
/// <c>epc_tdp_platform_pdo</c>,
/// <c>epc_cm_h</c>, <c>epc_cm_money</c>, <c>epc_cm_dashboard</c>,
/// <c>epc_cm_count_customers</c>, <c>epc_cm_list_customers</c>,
/// <c>epc_cm_customer_display_name</c>, <c>epc_cm_customer_initials</c>,
/// <c>epc_cm_customer_orders</c>, <c>epc_cm_customer_advances</c>,
/// <c>epc_cm_customer_einvoices</c>, <c>epc_cm_recent_returns</c>,
/// <c>epc_cm_save_customer_profile</c>, <c>epc_cm_get_customer</c>,
/// <c>epc_cm_recent_orders</c>, <c>epc_cm_tab_url</c>.
/// </summary>
public static class PhpPlanQ1Safe
{
    public const string SitemapWarehousePath = "content/general_pages/epc_sitemap_warehouse.php";
    public const string TenantDataProtectionPath = "content/general_pages/epc_tenant_data_protection.php";
    public const string CustomerMgmtHelpersPath = "content/shop/customer_mgmt/epc_customer_mgmt_helpers.php";

    public const string EpcTdpVersion = "1.0.0";
    public const int EpcTdpAccessNone = 0;
    public const int EpcTdpAccessMetadata = 1;
    public const int EpcTdpAccessRead = 2;
    public const int EpcTdpAccessWrite = 4;
    public const int EpcTdpAccessAdmin = 8;
    public const int EpcTdpAccessExport = 16;
    public const int EpcTdpAccessDelete = 32;
    public const int EpcTdpProviderDefault = EpcTdpAccessMetadata | EpcTdpAccessRead | EpcTdpAccessAdmin;
    public const int EpcTdpTenantDefault = EpcTdpAccessMetadata | EpcTdpAccessRead | EpcTdpAccessWrite | EpcTdpAccessExport;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex SiteKeyJunk = new("[^a-z0-9_]", RegexOptions.CultureInvariant);
    private static readonly Regex InitialSplit = new(@"[\s@._-]+", RegexOptions.CultureInvariant);
    private static readonly Regex NonDigit = new(@"\D", RegexOptions.CultureInvariant);
    private static readonly Regex ArticleKeep = new("[^A-Za-z0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex PublicShardName = new(@"sitemap-warehouse-(\d+)\.xml$", RegexOptions.CultureInvariant);

    public static string DocumentRoot { get; set; } = "";
    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
    public static bool NeedPrice { get; set; } = true;
    public static string StorefrontPriceFilter { get; set; } = "";
    public static Func<string, string, string, string, string> PartLoc { get; set; } = DefaultPartLoc;
    public static Func<string, Dictionary<string, object?>?> TenantRow { get; set; } = _ => null;
    public static Func<Dictionary<string, object?>, (bool Ok, string Error)> TenantConnect { get; set; } = _ => (false, "denied");
    public static Func<IReadOnlyList<Dictionary<string, object?>>> ListTenants { get; set; } = () => Array.Empty<Dictionary<string, object?>>();
    public static bool HasPlatformPdo { get; set; }
    public static bool HeadersSent { get; set; } = true;
    public static bool ReturnsTableExists { get; set; } = true;
    public static bool VatFileExists { get; set; } = true;
    public static Dictionary<string, string> Server { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static List<string> LastHeaders { get; } = new();
    public static string LastBody { get; set; } = "";
    public static int LastStatus { get; set; }
    public static bool LastExited { get; set; }
    public static List<Dictionary<string, object?>> SavedBuyers { get; } = new();
    public static List<object?[]> TradeSets { get; } = new();
    public static List<int> VatSyncs { get; } = new();

    public static void Reset()
    {
        DocumentRoot = "";
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        UtcNow = () => DateTime.UtcNow;
        NeedPrice = true;
        StorefrontPriceFilter = "";
        PartLoc = DefaultPartLoc;
        TenantRow = _ => null;
        TenantConnect = _ => (false, "denied");
        ListTenants = () => Array.Empty<Dictionary<string, object?>>();
        HasPlatformPdo = false;
        HeadersSent = true;
        ReturnsTableExists = true;
        VatFileExists = true;
        Server.Clear();
        LastHeaders.Clear();
        LastBody = "";
        LastStatus = 0;
        LastExited = false;
        SavedBuyers.Clear();
        TradeSets.Clear();
        VatSyncs.Clear();
    }

    public sealed class TdpStore
    {
        public bool Schema { get; set; }
        public int NextAuditId { get; set; } = 1;
        public int NextViolationId { get; set; } = 1;
        public List<AuditRow> Audit { get; } = new();
        public List<ViolationRow> Violations { get; } = new();
    }

    public sealed class AuditRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string Module { get; set; } = "";
        public string Action { get; set; } = "";
        public int ActorId { get; set; }
        public string ActorIp { get; set; } = "";
        public string ActorUa { get; set; } = "";
        public string? MetaJson { get; set; }
        public long CreatedAt { get; set; }
    }

    public sealed class ViolationRow
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public int ActorId { get; set; }
        public string ActorIp { get; set; } = "";
        public string TargetTenant { get; set; } = "";
        public string? DetailsJson { get; set; }
        public long CreatedAt { get; set; }
    }

    public sealed class SmStore
    {
        public List<PriceRow> Prices { get; } = new();
    }

    public sealed class PriceRow
    {
        public string Manufacturer { get; set; } = "";
        public string Article { get; set; } = "";
        public double Exist { get; set; }
        public double Price { get; set; }
    }

    public sealed class CmStore
    {
        public int NextProfileId { get; set; } = 1;
        public int NextOrderId { get; set; } = 1;
        public int NextReturnId { get; set; } = 1;
        public List<UserRow> Users { get; } = new();
        public List<ProfileRow> Profiles { get; } = new();
        public List<OrderRow> Orders { get; } = new();
        public List<OrderItemRow> Items { get; } = new();
        public List<AdvanceRow> Advances { get; } = new();
        public List<BuyerRow> Buyers { get; } = new();
        public List<EinvoiceRow> Einvoices { get; } = new();
        public List<ReturnRow> Returns { get; } = new();
    }

    public sealed class UserRow
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public long TimeRegistered { get; set; }
    }

    public sealed class ProfileRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string DataKey { get; set; } = "";
        public string DataValue { get; set; } = "";
    }

    public sealed class OrderRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SuccessfullyCreated { get; set; }
        public int Paid { get; set; }
        public long Time { get; set; }
    }

    public sealed class OrderItemRow
    {
        public int OrderId { get; set; }
        public double Price { get; set; }
        public int CountNeed { get; set; }
    }

    public sealed class AdvanceRow
    {
        public int UserId { get; set; }
        public double Amount { get; set; }
        public int Active { get; set; }
        public int Income { get; set; }
        public long Time { get; set; }
    }

    public sealed class BuyerRow
    {
        public int UserId { get; set; }
        public string Trn { get; set; } = "";
        public string PeppolEndpoint { get; set; } = "";
        public int BuyerOnboarded { get; set; }
        public string BuyerName { get; set; } = "";
        public string City { get; set; } = "";
        public string CountryCode { get; set; } = "";
    }

    public sealed class EinvoiceRow
    {
        public int UserId { get; set; }
        public int Active { get; set; }
        public string IssueDate { get; set; } = "";
        public string Doc { get; set; } = "";
    }

    public sealed class ReturnRow
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
    }

    public static int EpcSitemapWarehouseShardSize() => 5000;
    public static int EpcSitemapWarehouseMaxShards() => 80;

    public static string EpcSitemapWarehouseCacheDir()
    {
        var dir = Path.Combine(DocRoot(), "sitemap_cache");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string EpcSitemapWarehouseStalePath()
        => Path.Combine(EpcSitemapWarehouseCacheDir(), "warehouse-stale.json");

    public static bool EpcSitemapWarehouseIsStale()
        => File.Exists(EpcSitemapWarehouseStalePath());

    public static void EpcSitemapWarehouseMarkStale(string reason = "", int priceId = 0)
    {
        Directory.CreateDirectory(EpcSitemapWarehouseCacheDir());
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["stale"] = true,
            ["reason"] = reason,
            ["price_id"] = priceId,
            ["marked_at"] = UtcNow().ToString("yyyy-MM-ddTHH:mm:ss+00:00")
        };
        File.WriteAllText(EpcSitemapWarehouseStalePath(), JsonSerializer.Serialize(payload, PrettyJson()));
    }

    public static void EpcSitemapWarehouseClearStale()
    {
        var path = EpcSitemapWarehouseStalePath();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static string EpcSitemapWarehouseCachePath(int shard)
        => Path.Combine(EpcSitemapWarehouseCacheDir(), "warehouse-" + shard.ToString(CultureInfo.InvariantCulture) + ".xml");

    public static string EpcSitemapWarehousePublicPath(int shard)
        => Path.Combine(DocRoot(), "sitemap-warehouse-" + shard.ToString(CultureInfo.InvariantCulture) + ".xml");

    public static string EpcSitemapWarehouseMetaPath()
        => Path.Combine(EpcSitemapWarehouseCacheDir(), "warehouse-meta.json");

    public static Dictionary<string, object?> EpcSitemapWarehouseMetaRead()
    {
        var defaults = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["shards"] = 0,
            ["urls"] = 0,
            ["generated_at"] = "",
            ["shard_size"] = EpcSitemapWarehouseShardSize()
        };
        var path = EpcSitemapWarehouseMetaPath();
        if (!File.Exists(path))
        {
            return defaults;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return defaults;
            }

            var raw = doc.RootElement;
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["shards"] = raw.TryGetProperty("shards", out var s) ? s.GetInt32() : 0,
                ["urls"] = raw.TryGetProperty("urls", out var u) ? u.GetInt32() : 0,
                ["generated_at"] = raw.TryGetProperty("generated_at", out var g) ? g.GetString() ?? "" : "",
                ["shard_size"] = raw.TryGetProperty("shard_size", out var z) ? z.GetInt32() : EpcSitemapWarehouseShardSize()
            };
        }
        catch (JsonException)
        {
            return defaults;
        }
    }

    public static void EpcSitemapWarehouseMetaWrite(Dictionary<string, object?> meta)
    {
        if (!meta.ContainsKey("generated_at") || meta["generated_at"] is null)
        {
            meta["generated_at"] = UtcNow().ToString("yyyy-MM-ddTHH:mm:ss+00:00");
        }

        if (!meta.ContainsKey("shard_size") || meta["shard_size"] is null)
        {
            meta["shard_size"] = EpcSitemapWarehouseShardSize();
        }

        File.WriteAllText(EpcSitemapWarehouseMetaPath(), JsonSerializer.Serialize(meta, PrettyJson()));
    }

    public static string[] EpcSitemapWarehousePriceFilters(SmStore? db = null)
    {
        var priceClause = NeedPrice ? " AND IFNULL(`price`, 0) > 0" : "";
        if (priceClause != "" && !priceClause.Contains("d.`price`", StringComparison.Ordinal))
        {
            priceClause = priceClause.Replace("`price`", "d.`price`", StringComparison.Ordinal);
        }

        return [priceClause, StorefrontPriceFilter];
    }

    public static string EpcSitemapWarehouseUrlXml(object? cfg, string lang, string brand, string article, string lastmod)
    {
        var loc = PartLoc(cfg is null ? "" : "cfg", lang, brand, article);
        if (loc == "" || loc.Contains("/parts/brands/", StringComparison.Ordinal))
        {
            return "";
        }

        return "\t<url>\n"
            + "\t\t<loc>" + loc + "</loc>\n"
            + "\t\t<lastmod>" + XmlH(lastmod) + "</lastmod>\n"
            + "\t\t<changefreq>weekly</changefreq>\n"
            + "\t\t<priority>0.6</priority>\n"
            + "\t</url>\n";
    }

    public static void EpcSitemapWarehouseWriteShardFile(int shard, string body)
    {
        File.WriteAllText(EpcSitemapWarehouseCachePath(shard), body);
        File.WriteAllText(EpcSitemapWarehousePublicPath(shard), body);
    }

    public static Dictionary<string, object?> EpcSitemapWarehouseRegenerateShard(object? cfg, SmStore db, int shard)
    {
        if (shard < 0 || shard >= EpcSitemapWarehouseMaxShards())
        {
            return ShardResult(shard, 0, 0, "invalid shard", true);
        }

        var lastmod = UtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var shardSize = EpcSitemapWarehouseShardSize();
        var offset = shard * shardSize;
        var pairs = StockPairs(db);
        var slice = pairs.Skip(offset).Take(shardSize).ToList();
        var body = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";
        var urls = 0;
        foreach (var pair in slice)
        {
            var xml = EpcSitemapWarehouseUrlXml(cfg, "en", pair.Mfr, pair.Article, lastmod);
            if (xml == "")
            {
                continue;
            }

            body += xml;
            urls++;
        }

        body += "</urlset>\n";
        if (urls == 0)
        {
            TryDelete(EpcSitemapWarehouseCachePath(shard));
            TryDelete(EpcSitemapWarehousePublicPath(shard));
            EpcSitemapWarehouseMetaRefreshFromFiles();
            return ShardResult(shard, 0, 0, "", true);
        }

        EpcSitemapWarehouseWriteShardFile(shard, body);
        EpcSitemapWarehouseMetaRefreshFromFiles();
        var done = urls < shardSize;
        if (done)
        {
            EpcSitemapWarehouseClearStale();
        }

        return ShardResult(shard, urls, Encoding.UTF8.GetByteCount(body), "", done);
    }

    public static void EpcSitemapWarehouseMetaRefreshFromFiles()
    {
        var files = Directory.Exists(DocRoot())
            ? Directory.GetFiles(DocRoot(), "sitemap-warehouse-*.xml")
            : Array.Empty<string>();
        var shards = 0;
        var urls = 0;
        foreach (var file in files)
        {
            var m = PublicShardName.Match(Path.GetFileName(file));
            if (!m.Success)
            {
                continue;
            }

            shards = Math.Max(shards, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + 1);
            urls += CountUrlTags(File.ReadAllText(file));
        }

        EpcSitemapWarehouseMetaWrite(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["shards"] = shards,
            ["urls"] = urls,
            ["generated_at"] = UtcNow().ToString("yyyy-MM-ddTHH:mm:ss+00:00"),
            ["shard_size"] = EpcSitemapWarehouseShardSize()
        });
    }

    public static int EpcSitemapWarehouseExistingShardCount()
    {
        if (!Directory.Exists(DocRoot()))
        {
            return 0;
        }

        var max = -1;
        foreach (var file in Directory.GetFiles(DocRoot(), "sitemap-warehouse-*.xml"))
        {
            var m = PublicShardName.Match(Path.GetFileName(file));
            if (m.Success)
            {
                max = Math.Max(max, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            }
        }

        return max >= 0 ? max + 1 : 0;
    }

    public static Dictionary<string, object?> EpcSitemapWarehouseRegenerateAll(object? cfg, SmStore db)
    {
        var lastmod = UtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var shardSize = EpcSitemapWarehouseShardSize();
        var maxShards = EpcSitemapWarehouseMaxShards();
        var cacheDir = EpcSitemapWarehouseCacheDir();
        foreach (var old in Directory.GetFiles(cacheDir, "warehouse-*.xml"))
        {
            TryDelete(old);
        }

        if (Directory.Exists(DocRoot()))
        {
            foreach (var old in Directory.GetFiles(DocRoot(), "sitemap-warehouse-*.xml"))
            {
                TryDelete(old);
            }
        }

        var pairs = StockPairs(db);
        var shard = 0;
        var inShard = 0;
        var totalUrls = 0;
        var buffers = new Dictionary<int, string>();
        buffers[0] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";
        foreach (var pair in pairs)
        {
            var xml = EpcSitemapWarehouseUrlXml(cfg, "en", pair.Mfr, pair.Article, lastmod);
            if (xml == "")
            {
                continue;
            }

            if (inShard >= shardSize)
            {
                FlushShard(buffers, shard);
                if (shard + 1 >= maxShards)
                {
                    break;
                }

                shard++;
                inShard = 0;
                buffers[shard] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                    + "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";
            }

            buffers[shard] += xml;
            inShard++;
            totalUrls++;
        }

        FlushShard(buffers, shard);
        var shardCount = totalUrls > 0 ? shard + 1 : 0;
        EpcSitemapWarehouseMetaWrite(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["shards"] = shardCount,
            ["urls"] = totalUrls,
            ["generated_at"] = UtcNow().ToString("yyyy-MM-ddTHH:mm:ss+00:00"),
            ["shard_size"] = shardSize
        });
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["shards"] = shardCount,
            ["urls"] = totalUrls,
            ["error"] = ""
        };
    }

    public static bool EpcSitemapWarehouseServeCached(int shard)
    {
        if (shard < 0)
        {
            return false;
        }

        var path = EpcSitemapWarehousePublicPath(shard);
        if (!File.Exists(path) || new FileInfo(path).Length < 40)
        {
            path = EpcSitemapWarehouseCachePath(shard);
        }

        if (!File.Exists(path) || new FileInfo(path).Length < 40)
        {
            return false;
        }

        LastHeaders.Clear();
        LastHeaders.Add("Content-Type: application/xml; charset=utf-8");
        LastHeaders.Add("X-Sitemap-Cache: hit");
        LastHeaders.Add("Cache-Control: public, max-age=3600");
        LastBody = File.ReadAllText(path);
        return true;
    }

    public static int EpcSitemapWarehouseEstimateShards(SmStore db)
    {
        var meta = EpcSitemapWarehouseMetaRead();
        if (Convert.ToInt32(meta["shards"], CultureInfo.InvariantCulture) > 0)
        {
            return Convert.ToInt32(meta["shards"], CultureInfo.InvariantCulture);
        }

        var approx = StockRows(db).Count;
        if (approx <= 0)
        {
            return 0;
        }

        var shards = (int)Math.Ceiling(Math.Min(approx, 200000) / (double)EpcSitemapWarehouseShardSize());
        if (shards > EpcSitemapWarehouseMaxShards())
        {
            shards = EpcSitemapWarehouseMaxShards();
        }

        return Math.Max(1, shards);
    }

    public static Dictionary<string, object?> EpcTdpCheckAccess(string siteKey, int requiredAccess, IReadOnlyDictionary<string, object?>? context = null)
    {
        siteKey = SiteKeyJunk.Replace((siteKey ?? "").ToLowerInvariant(), "");
        if (siteKey == "")
        {
            return Access(false, "Invalid tenant key", EpcTdpAccessNone);
        }

        context ??= new Dictionary<string, object?>();
        var role = Str(context, "role", "guest");
        var userId = Int(context, "user_id");
        var sessionTenant = Str(context, "tenant_key");
        if (role == "guest" || userId <= 0)
        {
            return Access(false, "Authentication required", EpcTdpAccessNone);
        }

        if (role == "provider")
        {
            var allowed = (EpcTdpProviderDefault & requiredAccess) == requiredAccess;
            return Access(allowed, allowed ? "Provider access granted" : "Insufficient provider permissions for this operation", EpcTdpProviderDefault);
        }

        if (role == "tenant")
        {
            if (sessionTenant != siteKey)
            {
                EpcTdpLogViolation(new TdpStore(), "cross_tenant_attempt", userId, siteKey, new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["session_tenant"] = sessionTenant,
                    ["attempted_tenant"] = siteKey,
                    ["ip"] = EpcTdpClientIp()
                });
                return Access(false, "Access denied — tenant users can only access their own tenant data", EpcTdpAccessNone);
            }

            var allowed = (EpcTdpTenantDefault & requiredAccess) == requiredAccess;
            return Access(allowed, allowed ? "Tenant user access granted" : "Insufficient tenant permissions", EpcTdpTenantDefault);
        }

        return Access(false, "Unknown role: " + role, EpcTdpAccessNone);
    }

    public static void EpcTdpEnforceAccess(string siteKey, int requiredAccess, IReadOnlyDictionary<string, object?>? context = null, TdpStore? db = null)
    {
        var result = EpcTdpCheckAccess(siteKey, requiredAccess, context);
        if (!Truthy(result["allowed"]))
        {
            EpcTdpLogViolation(db ?? new TdpStore(), "access_denied", Int(context, "user_id"), siteKey, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["reason"] = result["reason"],
                ["required"] = requiredAccess,
                ["granted"] = result["access_level"],
                ["ip"] = EpcTdpClientIp()
            });
            LastStatus = 403;
            LastHeaders.Clear();
            LastHeaders.Add("Content-Type: application/json; charset=utf-8");
            LastBody = "{\"error\":\"Access denied\",\"code\":\"TDP_FORBIDDEN\"}";
            LastExited = true;
        }
    }

    public static void EpcTdpLogAccess(TdpStore db, string siteKey, string module, string action, int actorId, IReadOnlyDictionary<string, object?>? meta = null)
    {
        if (!HasPlatformPdo)
        {
            return;
        }

        EpcTdpEnsureAuditTable(db);
        db.Audit.Add(new AuditRow
        {
            Id = db.NextAuditId++,
            SiteKey = siteKey,
            Module = ByteSubstr(module, 64),
            Action = ByteSubstr(action, 64),
            ActorId = actorId,
            ActorIp = EpcTdpClientIp(),
            ActorUa = ByteSubstr(Server.TryGetValue("HTTP_USER_AGENT", out var ua) ? ua : "", 255),
            MetaJson = meta is { Count: > 0 } ? JsonSerializer.Serialize(meta, JsonOpts) : null,
            CreatedAt = Clock()
        });
    }

    public static void EpcTdpLogViolation(TdpStore db, string type, int actorId, string targetTenant, IReadOnlyDictionary<string, object?>? details = null)
    {
        if (!HasPlatformPdo)
        {
            return;
        }

        EpcTdpEnsureAuditTable(db);
        db.Violations.Add(new ViolationRow
        {
            Id = db.NextViolationId++,
            Type = ByteSubstr(type, 64),
            ActorId = actorId,
            ActorIp = EpcTdpClientIp(),
            TargetTenant = targetTenant,
            DetailsJson = JsonSerializer.Serialize(details ?? new Dictionary<string, object?>(), JsonOpts),
            CreatedAt = Clock()
        });
    }

    public static Dictionary<string, object?> EpcTdpRedactSensitive(IReadOnlyDictionary<string, object?> data, IReadOnlyList<string>? sensitiveFields = null)
    {
        var defaults = new[]
        {
            "password", "db_password", "db_pass", "secret", "token",
            "bank_account", "account_number", "iban", "swift", "routing_number",
            "tax_id", "trn", "vat_number", "ssn", "national_id", "passport",
            "credit_card", "card_number", "cvv", "expiry",
            "salary", "compensation", "bonus",
            "api_key", "api_secret", "private_key", "access_token", "refresh_token"
        };
        var all = defaults.Concat(sensitiveFields ?? Array.Empty<string>()).ToList();
        var redacted = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in data)
        {
            var keyLower = key.ToLowerInvariant();
            var isSensitive = all.Any(sf => keyLower.Contains(sf.ToLowerInvariant(), StringComparison.Ordinal));
            if (isSensitive)
            {
                redacted[key] = "***REDACTED***";
            }
            else if (value is IReadOnlyDictionary<string, object?> nested)
            {
                redacted[key] = EpcTdpRedactSensitive(nested, sensitiveFields);
            }
            else
            {
                redacted[key] = value;
            }
        }

        return redacted;
    }

    public static void EpcTdpApplySecurityHeaders()
    {
        if (HeadersSent)
        {
            return;
        }

        LastHeaders.Clear();
        LastHeaders.Add("Cache-Control: no-store, no-cache, must-revalidate, private");
        LastHeaders.Add("Pragma: no-cache");
        LastHeaders.Add("Expires: 0");
        LastHeaders.Add("X-Content-Type-Options: nosniff");
        LastHeaders.Add("X-Frame-Options: SAMEORIGIN");
        LastHeaders.Add("Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdnjs.cloudflare.com; style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; img-src 'self' data: https:; connect-src 'self'; frame-ancestors 'self'");
        LastHeaders.Add("Referrer-Policy: strict-origin-when-cross-origin");
        LastHeaders.Add("Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()");
    }

    public static Dictionary<string, object?> EpcTdpVerifyIsolation(TdpStore? _, string siteKey)
    {
        var report = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant"] = siteKey,
            ["timestamp"] = UtcNow().ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture),
            ["checks"] = new List<Dictionary<string, object?>>(),
            ["passed"] = true
        };
        var checks = (List<Dictionary<string, object?>>)report["checks"]!;
        var row = TenantRow(siteKey);
        if (row is null)
        {
            checks.Add(Check("tenant_exists", false, "Tenant not found in registry"));
            report["passed"] = false;
            return report;
        }

        var dbName = Str(row, "db_name");
        var dedicatedOk = dbName != "" && dbName != "ecomae";
        checks.Add(Check("dedicated_database", dedicatedOk, dbName != "" ? "Database: " + dbName : "No dedicated database assigned"));
        if (!dedicatedOk)
        {
            report["passed"] = false;
        }

        var hasCredentials = !IsEmpty(row.TryGetValue("db_password", out var pw) ? pw : null) || dbName == "docpart";
        checks.Add(Check("credentials_stored", hasCredentials, hasCredentials ? "Database credentials present in registry" : "Missing database credentials"));
        if (!hasCredentials)
        {
            report["passed"] = false;
        }

        var conn = TenantConnect(row);
        checks.Add(Check("db_connectivity", conn.Ok, conn.Ok ? "Successfully connected to tenant database" : "Connection failed: " + conn.Error));
        if (!conn.Ok)
        {
            report["passed"] = false;
        }

        var hostname = Str(row, "hostname");
        var isShared = !IsEmpty(row.TryGetValue("erp_only_shared", out var shared) ? shared : null);
        var hostnameOk = hostname != "" && (isShared || hostname != "www.ecomae.com");
        checks.Add(Check("hostname_scoped", hostnameOk, isShared ? "Shared ERP tenant on platform host (expected)" : "Hostname: " + hostname));

        var valid = new HashSet<string>(StringComparer.Ordinal) { "draft", "dns_pending", "live", "suspended" };
        var status = Str(row, "status");
        checks.Add(Check("valid_status", valid.Contains(status), "Status: " + status));

        var noShare = dbName != "ecomae";
        checks.Add(Check("no_platform_db_sharing", noShare, noShare ? "Tenant uses dedicated database (not platform registry)" : "CRITICAL: tenant is using the platform registry database!"));
        if (!noShare)
        {
            report["passed"] = false;
        }

        return report;
    }

    public static Dictionary<string, object?> EpcTdpVerifyAllTenants(TdpStore? db = null)
    {
        var reports = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var t in ListTenants())
        {
            var key = Str(t, "site_key");
            if (key != "")
            {
                reports[key] = EpcTdpVerifyIsolation(db, key);
            }
        }

        return reports;
    }

    public static Dictionary<string, object?> EpcTdpRetentionPolicy()
        => new(StringComparer.Ordinal)
        {
            ["audit_logs"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["retention_days"] = 2555, ["description"] = "Platform audit logs — 7 years (legal/tax requirement)" },
            ["violation_logs"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["retention_days"] = 2555, ["description"] = "Security violation logs — 7 years" },
            ["erp_transactions"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["retention_days"] = 2555, ["description"] = "ERP financial records — 7 years (legal requirement)" },
            ["customer_data"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["retention_days"] = 1825, ["description"] = "Customer PII — 5 years after last activity" },
            ["session_logs"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["retention_days"] = 90, ["description"] = "Login/session records — 90 days" },
            ["temp_exports"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["retention_days"] = 7, ["description"] = "Temporary data exports — 7 days" }
        };

    public static Dictionary<string, object?> EpcTdpClassifyData(string module, string field = "")
    {
        var classifications = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["gl"] = Class("highly_confidential", "Financial — General Ledger"),
            ["ap"] = Class("highly_confidential", "Financial — Accounts Payable"),
            ["ar"] = Class("highly_confidential", "Financial — Accounts Receivable"),
            ["cash_bank"] = Class("highly_confidential", "Financial — Cash & Bank"),
            ["payroll"] = Class("highly_confidential", "HR — Payroll & Compensation"),
            ["tax"] = Class("highly_confidential", "Tax — Filings & Compliance"),
            ["hr"] = Class("confidential", "HR — Employee Records"),
            ["orders"] = Class("confidential", "Commerce — Orders"),
            ["customers"] = Class("confidential", "Commerce — Customer Data"),
            ["vendors"] = Class("confidential", "Supply Chain — Vendor Data"),
            ["inventory"] = Class("confidential", "Warehouse — Inventory"),
            ["pricing"] = Class("confidential", "Commerce — Pricing"),
            ["products"] = Class("internal", "Catalogue — Products"),
            ["cms"] = Class("internal", "Content — CMS"),
            ["settings"] = Class("internal", "Configuration — Settings"),
            ["marketing"] = Class("internal", "Marketing — Campaigns"),
            ["storefront"] = Class("public", "Storefront — Public Catalogue")
        };
        return classifications.TryGetValue(module.ToLowerInvariant(), out var hit)
            ? hit
            : Class("internal", "Unclassified — " + module);
    }

    public static void EpcTdpEnsureAuditTable(TdpStore db) => db.Schema = true;

    public static string EpcTdpClientIp()
    {
        foreach (var h in new[] { "HTTP_CF_CONNECTING_IP", "HTTP_X_FORWARDED_FOR", "HTTP_X_REAL_IP", "REMOTE_ADDR" })
        {
            if (Server.TryGetValue(h, out var val))
            {
                val = val.Trim();
                if (val != "")
                {
                    return val.Split(',')[0].Trim();
                }
            }
        }

        return "0.0.0.0";
    }

    public static object? EpcTdpPlatformPdo() => HasPlatformPdo ? new object() : null;

    public static string EpcCmH(object? value) => H(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");

    public static string EpcCmMoney(object? n)
        => PhpFloat(n).ToString("N2", CultureInfo.InvariantCulture);

    public static Dictionary<string, object?> EpcCmDashboard(CmStore db)
    {
        var customers = db.Users.Count(u => u.UserId > 0);
        var since = Clock() - 30L * 86400;
        var orders30 = db.Orders.Count(o => o.SuccessfullyCreated == 1 && o.Time >= since);
        var openOrders = db.Orders.Count(o => o.SuccessfullyCreated == 1 && o.Paid != 1);
        var buyersWithTrn = db.Buyers.Count(b => b.Trn != "");
        var einvoices = db.Einvoices.Count(e => e.Active == 1);
        var returns = ReturnsTableExists ? db.Returns.Count : 0;
        var advances = db.Advances.Where(a => a.Active == 1 && a.Income == 1).Sum(a => a.Amount);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["customers"] = customers,
            ["orders_30d"] = orders30,
            ["open_orders"] = openOrders,
            ["buyers_with_trn"] = buyersWithTrn,
            ["einvoices"] = einvoices,
            ["returns"] = returns,
            ["customer_ledger_balance"] = advances
        };
    }

    public static int EpcCmCountCustomers(CmStore db, string search = "")
    {
        if (search == "")
        {
            return db.Users.Count(u => u.UserId > 0);
        }

        var q = search;
        return db.Users.Where(u => u.UserId > 0 && CustomerMatches(db, u, q, false)).Select(u => u.UserId).Distinct().Count();
    }

    public static List<Dictionary<string, object?>> EpcCmListCustomers(CmStore db, string search = "", int limit = 50, int offset = 0)
    {
        limit = Math.Max(1, Math.Min(200, limit));
        offset = Math.Max(0, offset);
        var rows = db.Users.Where(u => u.UserId > 0).OrderByDescending(u => u.UserId).ToList();
        var digit = search != "" && search.All(char.IsDigit);
        if (search != "")
        {
            rows = rows.Where(u => CustomerMatches(db, u, search, digit)).ToList();
        }

        var page = rows.Skip(offset).Take(limit).Select(u => CustomerListRow(db, u)).ToList();
        foreach (var row in page)
        {
            row["order_count"] = 0;
            row["display_name"] = EpcCmCustomerDisplayName(row);
        }

        if (page.Count == 0)
        {
            return page;
        }

        var ids = page.Select(r => Convert.ToInt32(r["user_id"], CultureInfo.InvariantCulture)).ToHashSet();
        var counts = db.Orders.Where(o => o.SuccessfullyCreated == 1 && ids.Contains(o.UserId))
            .GroupBy(o => o.UserId)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var row in page)
        {
            var uid = Convert.ToInt32(row["user_id"], CultureInfo.InvariantCulture);
            row["order_count"] = counts.TryGetValue(uid, out var c) ? c : 0;
        }

        return page;
    }

    public static string EpcCmCustomerDisplayName(IReadOnlyDictionary<string, object?> row)
    {
        var company = Str(row, "company").Trim();
        if (company != "")
        {
            return company;
        }

        var buyer = Str(row, "buyer_name").Trim();
        if (buyer != "")
        {
            return buyer;
        }

        var name = (Str(row, "fname") + " " + Str(row, "sname")).Trim();
        if (name != "")
        {
            return name;
        }

        var email = Str(row, "email").Trim();
        if (email != "")
        {
            var at = email.IndexOf('@');
            return at > 0 ? email[..at] : email;
        }

        return "Customer #" + Int(row, "user_id").ToString(CultureInfo.InvariantCulture);
    }

    public static string EpcCmCustomerInitials(IReadOnlyDictionary<string, object?> row)
    {
        var label = EpcCmCustomerDisplayName(row);
        var parts = InitialSplit.Split(label);
        var letters = "";
        foreach (var p in parts)
        {
            var part = p.Trim();
            if (part == "")
            {
                continue;
            }

            letters += part.EnumerateRunes().First().ToString().ToUpperInvariant();
            if (letters.EnumerateRunes().Count() >= 2)
            {
                break;
            }
        }

        return letters != "" ? letters : "C";
    }

    public static List<Dictionary<string, object?>> EpcCmCustomerOrders(CmStore db, int userId, int limit = 50)
    {
        if (userId <= 0)
        {
            return new();
        }

        return db.Orders.Where(o => o.UserId == userId && o.SuccessfullyCreated == 1)
            .OrderByDescending(o => o.Time).ThenBy(o => o.Id)
            .Take(limit)
            .Select(o => OrderDict(db, o))
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcCmCustomerAdvances(CmStore db, int userId, int limit = 50)
    {
        if (userId <= 0)
        {
            return new();
        }

        return db.Advances.Where(a => a.UserId == userId && a.Active == 1)
            .OrderByDescending(a => a.Time)
            .Take(limit)
            .Select(a => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["user_id"] = a.UserId,
                ["amount"] = a.Amount,
                ["active"] = a.Active,
                ["income"] = a.Income,
                ["time"] = a.Time
            })
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcCmCustomerEinvoices(CmStore db, int userId, int limit = 30)
    {
        if (userId <= 0)
        {
            return new();
        }

        return db.Einvoices.Where(e => e.UserId == userId && e.Active == 1)
            .OrderByDescending(e => e.IssueDate)
            .Take(limit)
            .Select(e => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["user_id"] = e.UserId,
                ["active"] = e.Active,
                ["issue_date"] = e.IssueDate,
                ["doc"] = e.Doc
            })
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcCmRecentReturns(CmStore db, int userId = 0, int limit = 30)
    {
        if (!ReturnsTableExists)
        {
            return new();
        }

        var q = db.Returns.AsEnumerable();
        if (userId > 0)
        {
            var ids = db.Orders.Where(o => o.UserId == userId).Select(o => o.Id).ToHashSet();
            q = q.Where(r => ids.Contains(r.OrderId));
        }

        return q.OrderByDescending(r => r.Id).Take(limit)
            .Select(r =>
            {
                var order = db.Orders.FirstOrDefault(o => o.Id == r.OrderId);
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = r.Id,
                    ["order_id"] = r.OrderId,
                    ["user_id"] = order?.UserId
                };
            })
            .ToList();
    }

    public static void EpcCmSaveCustomerProfile(CmStore db, IReadOnlyDictionary<string, object?> data)
    {
        var userId = Int(data, "user_id");
        if (userId <= 0)
        {
            throw new InvalidOperationException("Invalid customer");
        }

        SavedBuyers.Add(data.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal));
        if (data.ContainsKey("country_code"))
        {
            TradeSets.Add([userId, "epc_reg_country", Str(data, "country_code").Trim().ToUpperInvariant()]);
        }

        if (!IsEmpty(data.TryGetValue("trn", out var trn) ? trn : null))
        {
            TradeSets.Add([userId, "epc_reg_trn", NonDigit.Replace(Convert.ToString(trn, CultureInfo.InvariantCulture) ?? "", "")]);
        }

        if (VatFileExists)
        {
            VatSyncs.Add(userId);
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["company"] = "company",
            ["address"] = "address_line1",
            ["city"] = "city",
            ["phone"] = "phone"
        };
        foreach (var (profileKey, postKey) in fields)
        {
            if (!data.ContainsKey(postKey) && !data.ContainsKey(profileKey))
            {
                continue;
            }

            var val = (data.TryGetValue(postKey, out var a) ? Convert.ToString(a, CultureInfo.InvariantCulture) : null)
                ?? (data.TryGetValue(profileKey, out var b) ? Convert.ToString(b, CultureInfo.InvariantCulture) : null)
                ?? "";
            val = val.Trim();
            var existing = db.Profiles.FirstOrDefault(p => p.UserId == userId && p.DataKey == profileKey);
            if (existing is not null)
            {
                existing.DataValue = val;
            }
            else if (val != "")
            {
                db.Profiles.Add(new ProfileRow { Id = db.NextProfileId++, UserId = userId, DataKey = profileKey, DataValue = val });
            }
        }
    }

    public static Dictionary<string, object?>? EpcCmGetCustomer(CmStore db, int userId)
    {
        if (userId <= 0)
        {
            return null;
        }

        var user = db.Users.FirstOrDefault(u => u.UserId == userId);
        if (user is null)
        {
            return null;
        }

        var row = CustomerListRow(db, user);
        var buyer = db.Buyers.FirstOrDefault(b => b.UserId == userId);
        if (buyer is not null)
        {
            row["trn"] = buyer.Trn;
            row["peppol_endpoint"] = buyer.PeppolEndpoint;
            row["buyer_onboarded"] = buyer.BuyerOnboarded;
            row["buyer_name"] = buyer.BuyerName;
            row["city"] = buyer.City;
            row["country_code"] = buyer.CountryCode;
        }

        row["display_name"] = EpcCmCustomerDisplayName(row);
        row["order_count"] = db.Orders.Count(o => o.SuccessfullyCreated == 1 && o.UserId == userId);
        return row;
    }

    public static List<Dictionary<string, object?>> EpcCmRecentOrders(CmStore db, int limit = 50)
        => db.Orders.Where(o => o.SuccessfullyCreated == 1)
            .OrderByDescending(o => o.Time).ThenByDescending(o => o.Id)
            .Take(limit)
            .Select(o =>
            {
                var d = OrderDict(db, o);
                var user = db.Users.FirstOrDefault(u => u.UserId == o.UserId);
                d["email"] = user?.Email;
                return d;
            })
            .ToList();

    public static string EpcCmTabUrl(string baseUrl, string tab, string extra = "")
    {
        var url = baseUrl.TrimEnd('?') + "?tab=" + Uri.EscapeDataString(tab);
        if (extra != "")
        {
            url += "&" + extra.TrimStart('&');
        }

        return url;
    }

    private static string DefaultPartLoc(string cfg, string lang, string brand, string article)
    {
        if (string.Equals(brand.Trim(), "brands", StringComparison.OrdinalIgnoreCase))
        {
            return XmlH("https://www.epartscart.com/" + lang + "/parts/brands/" + Uri.EscapeDataString(article));
        }

        var b = Uri.EscapeDataString((brand ?? "").Trim().ToUpperInvariant());
        var a = Uri.EscapeDataString(ArticleKeep.Replace(article ?? "", "").ToUpperInvariant());
        return XmlH("https://www.epartscart.com/" + lang + "/parts/" + b + "/" + a);
    }

    private static List<(string Mfr, string Article)> StockPairs(SmStore db)
        => StockRows(db)
            .GroupBy(r => (r.Manufacturer.Trim(), r.Article.Trim()))
            .Select(g => (g.Key.Item1, g.Key.Item2))
            .OrderBy(p => p.Item1, StringComparer.Ordinal)
            .ThenBy(p => p.Item2, StringComparer.Ordinal)
            .ToList();

    private static List<PriceRow> StockRows(SmStore db)
    {
        var filters = EpcSitemapWarehousePriceFilters(db);
        var needPrice = filters[0].Contains("price", StringComparison.Ordinal);
        return db.Prices.Where(r =>
            r.Manufacturer.Trim() != "" &&
            r.Article.Trim() != "" &&
            r.Exist > 0 &&
            (!needPrice || r.Price > 0)).ToList();
    }

    private static Dictionary<string, object?> ShardResult(int shard, int urls, int bytes, string error, bool done)
        => new(StringComparer.Ordinal)
        {
            ["shard"] = shard,
            ["urls"] = urls,
            ["bytes"] = bytes,
            ["error"] = error,
            ["done"] = done
        };

    private static void FlushShard(Dictionary<int, string> buffers, int n)
    {
        if (!buffers.TryGetValue(n, out var body))
        {
            return;
        }

        EpcSitemapWarehouseWriteShardFile(n, body + "</urlset>\n");
        buffers.Remove(n);
    }

    private static bool CustomerMatches(CmStore db, UserRow user, string search, bool digit)
    {
        var like = search;
        var profiles = db.Profiles.Where(p => p.UserId == user.UserId).Select(p => p.DataValue);
        var buyer = db.Buyers.FirstOrDefault(b => b.UserId == user.UserId);
        if (digit)
        {
            return user.UserId.ToString(CultureInfo.InvariantCulture) == search
                || Contains(user.Email, like)
                || Contains(user.Phone, like)
                || profiles.Any(v => Contains(v, like));
        }

        return Contains(user.Email, like)
            || Contains(user.Phone, like)
            || profiles.Any(v => Contains(v, like))
            || (buyer is not null && (Contains(buyer.Trn, like) || Contains(buyer.BuyerName, like)));
    }

    private static Dictionary<string, object?> CustomerListRow(CmStore db, UserRow user)
    {
        string? Field(string key) => db.Profiles.FirstOrDefault(p => p.UserId == user.UserId && p.DataKey == key)?.DataValue;
        var buyer = db.Buyers.FirstOrDefault(b => b.UserId == user.UserId);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = user.UserId,
            ["email"] = user.Email,
            ["phone"] = user.Phone,
            ["time_registered"] = user.TimeRegistered,
            ["fname"] = Field("name"),
            ["sname"] = Field("surname"),
            ["company"] = Field("company"),
            ["trn"] = buyer?.Trn ?? "",
            ["peppol_endpoint"] = buyer?.PeppolEndpoint,
            ["buyer_onboarded"] = buyer?.BuyerOnboarded,
            ["buyer_name"] = buyer?.BuyerName,
            ["buyer_city"] = buyer?.City,
            ["buyer_country"] = buyer?.CountryCode
        };
    }

    private static Dictionary<string, object?> OrderDict(CmStore db, OrderRow o)
    {
        var sale = db.Items.Where(i => i.OrderId == o.Id).Sum(i => i.Price * i.CountNeed);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = o.Id,
            ["user_id"] = o.UserId,
            ["successfully_created"] = o.SuccessfullyCreated,
            ["paid"] = o.Paid,
            ["time"] = o.Time,
            ["sale_ex"] = sale == 0 && !db.Items.Any(i => i.OrderId == o.Id) ? 0d : sale
        };
    }

    private static Dictionary<string, object?> Access(bool allowed, string reason, int level)
        => new(StringComparer.Ordinal) { ["allowed"] = allowed, ["reason"] = reason, ["access_level"] = level };

    private static Dictionary<string, object?> Check(string name, bool passed, string detail)
        => new(StringComparer.Ordinal) { ["check"] = name, ["passed"] = passed, ["detail"] = detail };

    private static Dictionary<string, object?> Class(string level, string label)
        => new(StringComparer.Ordinal) { ["level"] = level, ["label"] = label };

    private static string H(string value)
        => (value ?? "")
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string XmlH(string value)
        => (value ?? "")
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string DocRoot()
        => string.IsNullOrEmpty(DocumentRoot) ? Directory.GetCurrentDirectory() : DocumentRoot;

    private static string Str(IReadOnlyDictionary<string, object?> row, string key, string fallback = "")
        => row.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback
            : fallback;

    private static int Int(IReadOnlyDictionary<string, object?>? row, string key)
    {
        if (row is null || !row.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool IsEmpty(object? value)
        => value is null or false or 0 or 0L or 0d or "" or "0";

    private static bool Truthy(object? value) => !IsEmpty(value) && value is not false;

    private static double PhpFloat(object? value)
    {
        if (value is null or false)
        {
            return 0;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static bool Contains(string? hay, string needle)
        => (hay ?? "").Contains(needle, StringComparison.Ordinal);

    private static string ByteSubstr(string value, int max)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");
        return bytes.Length <= max ? value ?? "" : Encoding.UTF8.GetString(bytes, 0, max);
    }

    private static int CountUrlTags(string raw)
    {
        var n = 0;
        var i = 0;
        while (true)
        {
            var j = raw.IndexOf("<url>", i, StringComparison.Ordinal);
            if (j < 0)
            {
                return n;
            }

            n++;
            i = j + 5;
        }
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static JsonSerializerOptions PrettyJson()
        => new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
}
