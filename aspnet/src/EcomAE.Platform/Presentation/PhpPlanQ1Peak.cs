using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-peak commerce isolation helpers. PHP identifiers kept for the inventory:
/// <c>epc_ci_tenant_price_ids</c>, <c>epc_ci_assert_price_ids</c>,
/// <c>epc_ci_log_violation</c>, <c>epc_ci_platform_pdo</c>,
/// <c>epc_ci_ensure_schema</c>, <c>epc_ci_run_full_audit</c>,
/// <c>epc_ci_audit_client_docpart_isolation</c>, <c>epc_ci_audit_erp_db_isolation</c>,
/// <c>epc_ci_audit_price_id_ownership</c>, <c>epc_ci_audit_orphan_price_data</c>,
/// <c>epc_ci_audit_query_scoping</c>, <c>epc_ci_audit_registry_credentials</c>,
/// <c>epc_ci_find_php_files</c>, <c>epc_ci_recent_violations</c>,
/// <c>epc_ci_latest_audit_run</c>, <c>epc_ci_scoped_query</c>,
/// <c>epc_ci_get_scoped_pdo</c>, <c>epc_ci_enforcement_scan</c>.
/// </summary>
public static class PhpPlanQ1Peak
{
    public const string IsolationPath = "content/general_pages/epc_commerce_isolation.php";

    private static readonly Regex SqlOnTable = new(@"\b(SELECT|UPDATE|DELETE)\b.*\bshop_docpart_prices_data\b", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex PriceIdFilter = new(@"\bprice_id\b.*\bIN\b|\bWHERE\b.*\bprice_id\b|\bAND\b.*\bprice_id\b", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex WhereWord = new("WHERE", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, List<int>> PriceCache = new(StringComparer.Ordinal);
    private static bool _schemaDone;
    private static PeakStore? _platform;

    public static Dictionary<string, string> Server { get; set; } = new(StringComparer.Ordinal);
    public static Func<string> Clock { get; set; } = () => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    public static Func<PeakStore?> PlatformPdo { get; set; } = () => _platform;
    public static Func<string, List<string>>? FindFiles { get; set; }
    public static Func<string, string?> ReadFile { get; set; } = path => File.Exists(path) ? File.ReadAllText(path) : null;
    public static Func<string, string, string, bool> ConnectTenant { get; set; } = (_, _, _) => false;
    public static string PhpSapi { get; set; } = "cli";
    public static Action<string>? ErrorLog { get; set; }

    public static void Reset()
    {
        Server = new(StringComparer.Ordinal);
        Clock = () => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        PlatformPdo = () => _platform;
        FindFiles = null;
        ReadFile = path => File.Exists(path) ? File.ReadAllText(path) : null;
        ConnectTenant = (_, _, _) => false;
        PhpSapi = "cli";
        ErrorLog = null;
        PriceCache.Clear();
        _schemaDone = false;
        _platform = null;
    }

    public sealed class StorageRow
    {
        public int Id { get; set; }
        public int InterfaceType { get; set; }
        public int Hidden { get; set; }
        public string ConnectionOptions { get; set; } = "";
    }

    public sealed class MapRow
    {
        public int StorageId { get; set; }
        public int OfficeId { get; set; }
    }

    public sealed class OfficeRow
    {
        public int Id { get; set; }
        public string Caption { get; set; } = "";
    }

    public sealed class PriceRow
    {
        public int Id { get; set; }
    }

    public sealed class PriceDataRow
    {
        public int PriceId { get; set; }
        public string Sku { get; set; } = "";
    }

    public sealed class TenantRow
    {
        public string SiteKey { get; set; } = "";
        public string Hostname { get; set; } = "";
        public string DbName { get; set; } = "";
        public string DbUser { get; set; } = "";
        public string DbPassword { get; set; } = "";
        public string TradeName { get; set; } = "";
        public string IndustryCode { get; set; } = "";
        public int DedicatedDb { get; set; }
        public string ScalePolicy { get; set; } = "";
        public int ErpOnlyShared { get; set; }
        public string HostedOn { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public sealed class ViolationRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string Actor { get; set; } = "";
        public string Detail { get; set; } = "";
        public string Ip { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }

    public sealed class AuditRunRow
    {
        public int Id { get; set; }
        public string RunAt { get; set; } = "";
        public int TotalTenants { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Warnings { get; set; }
        public string ReportJson { get; set; } = "";
        public string TriggeredBy { get; set; } = "";
    }

    public sealed class PeakStore
    {
        public int NextStorageId { get; set; } = 1;
        public int NextOfficeId { get; set; } = 1;
        public int NextViolationId { get; set; } = 1;
        public int NextAuditId { get; set; } = 1;
        public bool StoragesMissing { get; set; }
        public bool TenantsMissing { get; set; }
        public bool PricesMissing { get; set; }
        public List<StorageRow> Storages { get; } = [];
        public List<MapRow> Maps { get; } = [];
        public List<OfficeRow> Offices { get; } = [];
        public List<PriceRow> Prices { get; } = [];
        public List<PriceDataRow> PriceData { get; } = [];
        public List<TenantRow> Tenants { get; } = [];
        public List<ViolationRow> Violations { get; } = [];
        public List<AuditRunRow> AuditRuns { get; } = [];
        public List<Dictionary<string, object?>> LastQuery { get; } = [];
        public string LastSql { get; set; } = "";
        public List<object?> LastParams { get; } = [];
    }

    public static void UsePlatform(PeakStore store) => _platform = store;

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

    private static object BoxJson(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var n) => n,
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.String => el.GetString() ?? "",
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => "",
            _ => el
        };

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    public static List<int> EpcCiTenantPriceIds(PeakStore db, string siteKey)
    {
        if (PriceCache.TryGetValue(siteKey, out var hit))
        {
            return hit;
        }

        var ids = new Dictionary<int, bool>();
        try
        {
            if (db.StoragesMissing)
            {
                throw new InvalidOperationException("missing");
            }

            foreach (var storage in db.Storages.Where(s => s.InterfaceType == 2 && s.Hidden == 0))
            {
                if (!db.Maps.Any(m => m.StorageId == storage.Id && db.Offices.Any(o => o.Id == m.OfficeId)))
                {
                    continue;
                }

                object? opts = null;
                try
                {
                    opts = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(storage.ConnectionOptions ?? "");
                }
                catch (JsonException)
                {
                    opts = null;
                }

                if (opts is Dictionary<string, JsonElement> map && map.TryGetValue("price_id", out var pidEl))
                {
                    var boxed = BoxJson(pidEl);
                    if (!PhpEmpty(boxed))
                    {
                        var pid = Convert.ToInt32(boxed, CultureInfo.InvariantCulture);
                        ids[pid] = true;
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        var result = ids.Keys.ToList();
        result.Sort();
        PriceCache[siteKey] = result;
        return result;
    }

    public static bool EpcCiAssertPriceIds(PeakStore db, string siteKey, IEnumerable<object?> requestedPriceIds, string caller = "")
    {
        var allowed = EpcCiTenantPriceIds(db, siteKey);
        if (allowed.Count == 0)
        {
            return true;
        }

        var violations = new List<int>();
        foreach (var raw in requestedPriceIds)
        {
            var pid = Convert.ToInt32(raw ?? 0, CultureInfo.InvariantCulture);
            if (pid > 0 && !allowed.Contains(pid))
            {
                violations.Add(pid);
            }
        }

        if (violations.Count > 0)
        {
            var detail = "Cross-tenant price_id access attempt: requested [" + string.Join(",", violations)
                + "] but tenant " + siteKey + " only owns [" + string.Join(",", allowed) + "]";
            EpcCiLogViolation(siteKey, caller, detail);
            throw new InvalidOperationException("Commerce isolation violation: price_id not in tenant scope");
        }

        return true;
    }

    public static void EpcCiLogViolation(string siteKey, string actor, string detail)
    {
        try
        {
            var pdo = EpcCiPlatformPdo();
            if (pdo is null)
            {
                ErrorLog?.Invoke("[epc_ci] violation (no platform pdo): " + detail);
                return;
            }

            EpcCiEnsureSchema(pdo);
            pdo.Violations.Add(new ViolationRow
            {
                Id = pdo.NextViolationId++,
                SiteKey = siteKey,
                Actor = Clip(actor ?? "", 128),
                Detail = Clip(detail ?? "", 2000),
                Ip = Clip(Server.TryGetValue("REMOTE_ADDR", out var ip) ? ip : "", 45),
                CreatedAt = Clock()
            });
        }
        catch (Exception ex)
        {
            ErrorLog?.Invoke("[epc_ci] violation log error: " + ex.Message + " | " + detail);
        }
    }

    public static PeakStore? EpcCiPlatformPdo()
    {
        if (_platform is not null)
        {
            return _platform;
        }

        try
        {
            return PlatformPdo();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void EpcCiEnsureSchema(PeakStore db)
    {
        if (_schemaDone)
        {
            return;
        }

        _schemaDone = true;
        _ = db;
    }

    public static Dictionary<string, object?> EpcCiRunFullAudit(PeakStore platform, PeakStore commerce)
    {
        var results = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["timestamp"] = Clock(),
            ["overall"] = "PASS",
            ["checks"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["tenants"] = new List<object?>(),
            ["summary"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["passed"] = 0,
                ["failed"] = 0,
                ["warnings"] = 0
            }
        };
        var checks = (Dictionary<string, object?>)results["checks"]!;
        var summary = (Dictionary<string, object?>)results["summary"]!;

        void Score(string key, Dictionary<string, object?> check, bool warnSetsOverall)
        {
            checks[key] = check;
            var status = Convert.ToString(check["status"], CultureInfo.InvariantCulture);
            if (status == "FAIL")
            {
                results["overall"] = "FAIL";
                summary["failed"] = Convert.ToInt32(summary["failed"], CultureInfo.InvariantCulture) + 1;
            }
            else if (status == "WARN")
            {
                if (warnSetsOverall && Convert.ToString(results["overall"], CultureInfo.InvariantCulture) != "FAIL")
                {
                    results["overall"] = "WARN";
                }

                summary["warnings"] = Convert.ToInt32(summary["warnings"], CultureInfo.InvariantCulture) + 1;
            }
            else
            {
                summary["passed"] = Convert.ToInt32(summary["passed"], CultureInfo.InvariantCulture) + 1;
            }
        }

        Score("erp_db_isolation", EpcCiAuditErpDbIsolation(platform), false);
        Score("client_docpart_isolation", EpcCiAuditClientDocpartIsolation(platform), true);
        Score("price_id_ownership", EpcCiAuditPriceIdOwnership(commerce), false);
        Score("orphan_price_data", EpcCiAuditOrphanPriceData(commerce), false);
        Score("query_scoping", EpcCiAuditQueryScoping(), true);
        Score("registry_credentials", EpcCiAuditRegistryCredentials(platform), false);

        try
        {
            EpcCiEnsureSchema(platform);
            var passed = Convert.ToInt32(summary["passed"], CultureInfo.InvariantCulture);
            var failed = Convert.ToInt32(summary["failed"], CultureInfo.InvariantCulture);
            var warnings = Convert.ToInt32(summary["warnings"], CultureInfo.InvariantCulture);
            platform.AuditRuns.Add(new AuditRunRow
            {
                Id = platform.NextAuditId++,
                RunAt = Clock(),
                TotalTenants = passed + failed + warnings,
                Passed = passed,
                Failed = failed,
                Warnings = warnings,
                ReportJson = JsonSerializer.Serialize(results),
                TriggeredBy = PhpSapi == "cli" ? "cli" : "web"
            });
        }
        catch (Exception ex)
        {
            results["_save_error"] = ex.Message;
        }

        return results;
    }

    public static Dictionary<string, object?> EpcCiAuditClientDocpartIsolation(PeakStore platform)
    {
        var check = NewCheck("Client tenant docpart isolation");
        try
        {
            if (platform.TenantsMissing)
            {
                throw new InvalidOperationException("missing");
            }

            var details = (List<object?>)check["details"]!;
            foreach (var row in platform.Tenants.Where(t => t.Status is "live" or "dns_pending").OrderBy(t => t.SiteKey, StringComparer.Ordinal))
            {
                var siteKey = row.SiteKey ?? "";
                var host = (row.Hostname ?? "").Trim().ToLowerInvariant();
                var db = (row.DbName ?? "").Trim().ToLowerInvariant();
                if (siteKey == "epartscart")
                {
                    details.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["site_key"] = siteKey,
                        ["db_name"] = db,
                        ["ok"] = true,
                        ["note"] = "epartscart may share docpart"
                    });
                    continue;
                }

                if (!PhpEmpty(row.ErpOnlyShared) || (row.HostedOn ?? "") == "platform")
                {
                    continue;
                }

                if (host == "" || host == "www.ecomae.com" || host == "ecomae.com")
                {
                    continue;
                }

                var ok = db != "" && db != "docpart" && db != "ecomae";
                details.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["site_key"] = siteKey,
                    ["hostname"] = host,
                    ["db_name"] = db,
                    ["industry"] = row.IndustryCode ?? "",
                    ["dedicated_db"] = row.DedicatedDb,
                    ["scale_policy"] = row.ScalePolicy ?? "",
                    ["ok"] = ok,
                    ["note"] = ok ? "dedicated" : "FAIL shares docpart/platform DB — run " + "epc-client-tenant-db-isolate" + ".php"
                });
                if (!ok)
                {
                    check["status"] = "FAIL";
                }
            }
        }
        catch (Exception ex)
        {
            check["status"] = "FAIL";
            check["error"] = ex.Message;
        }

        return check;
    }

    public static Dictionary<string, object?> EpcCiAuditErpDbIsolation(PeakStore platform)
    {
        var check = NewCheck("ERP database isolation");
        try
        {
            if (platform.TenantsMissing)
            {
                throw new InvalidOperationException("missing");
            }

            var details = (List<object?>)check["details"]!;
            var forbidden = new HashSet<string>(StringComparer.Ordinal) { "docpart", "ecomae", "epartscart", "" };
            foreach (var row in platform.Tenants.Where(t => t.ErpOnlyShared == 1).OrderBy(t => t.SiteKey, StringComparer.Ordinal))
            {
                var db = row.DbName ?? "";
                var ok = !forbidden.Contains(db);
                details.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["site_key"] = row.SiteKey,
                    ["db_name"] = db,
                    ["trade_name"] = row.TradeName,
                    ["ok"] = ok
                });
                if (!ok)
                {
                    check["status"] = "FAIL";
                }
            }
        }
        catch (Exception ex)
        {
            check["status"] = "FAIL";
            check["error"] = ex.Message;
        }

        return check;
    }

    public static Dictionary<string, object?> EpcCiAuditPriceIdOwnership(PeakStore commerce)
    {
        var check = NewCheck("Price ID ownership uniqueness");
        try
        {
            if (commerce.StoragesMissing)
            {
                throw new InvalidOperationException("missing");
            }

            var pidToOffices = new Dictionary<int, List<Dictionary<string, object?>>>();
            foreach (var storage in commerce.Storages.Where(s => s.InterfaceType == 2))
            {
                foreach (var map in commerce.Maps.Where(m => m.StorageId == storage.Id))
                {
                    var office = commerce.Offices.FirstOrDefault(o => o.Id == map.OfficeId);
                    if (office is null)
                    {
                        continue;
                    }

                    Dictionary<string, JsonElement>? opts = null;
                    try
                    {
                        opts = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(storage.ConnectionOptions ?? "");
                    }
                    catch (JsonException)
                    {
                    }

                    if (opts is not null && opts.TryGetValue("price_id", out var pidEl))
                    {
                        var boxed = BoxJson(pidEl);
                        if (!PhpEmpty(boxed))
                        {
                            var pid = Convert.ToInt32(boxed, CultureInfo.InvariantCulture);
                            if (!pidToOffices.TryGetValue(pid, out var list))
                            {
                                list = [];
                                pidToOffices[pid] = list;
                            }

                            list.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                            {
                                ["office_id"] = office.Id,
                                ["office_name"] = office.Caption,
                                ["storage_id"] = storage.Id
                            });
                        }
                    }
                }
            }

            var shared = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in pidToOffices)
            {
                var officeIds = kv.Value.Select(o => Convert.ToInt32(o["office_id"], CultureInfo.InvariantCulture)).Distinct().ToList();
                if (officeIds.Count > 1)
                {
                    shared[kv.Key.ToString(CultureInfo.InvariantCulture)] = kv.Value;
                }
            }

            check["total_price_ids"] = pidToOffices.Count;
            if (shared.Count > 0)
            {
                check["status"] = "WARN";
                check["shared_price_ids"] = shared;
                check["message"] = shared.Count + " price_id(s) shared across multiple offices";
            }
            else
            {
                check["message"] = "All " + pidToOffices.Count + " price_ids have single-office ownership";
            }
        }
        catch (Exception ex)
        {
            check["status"] = "FAIL";
            check["error"] = ex.Message;
        }

        return check;
    }

    public static Dictionary<string, object?> EpcCiAuditOrphanPriceData(PeakStore commerce)
    {
        var check = NewCheck("Orphan price data rows");
        try
        {
            if (commerce.PricesMissing)
            {
                throw new InvalidOperationException("missing");
            }

            var orphans = commerce.PriceData
                .Where(d => commerce.Prices.All(p => p.Id != d.PriceId))
                .GroupBy(d => d.PriceId)
                .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["price_id"] = g.Key,
                    ["row_count"] = g.Count()
                })
                .OrderByDescending(r => Convert.ToInt32(r["row_count"], CultureInfo.InvariantCulture))
                .Take(50)
                .ToList();

            if (orphans.Count > 0)
            {
                var total = orphans.Sum(o => Convert.ToInt32(o["row_count"], CultureInfo.InvariantCulture));
                check["status"] = "WARN";
                check["orphan_price_ids"] = orphans;
                check["total_orphan_rows"] = total;
                check["message"] = total + " orphan rows across " + orphans.Count + " price_id(s)";
            }
            else
            {
                check["message"] = "No orphan rows found";
            }
        }
        catch (Exception ex)
        {
            check["status"] = "FAIL";
            check["error"] = ex.Message;
        }

        return check;
    }

    public static Dictionary<string, object?> EpcCiAuditQueryScoping()
    {
        var check = NewCheck("Commerce query scoping");
        var root = Server.TryGetValue("DOCUMENT_ROOT", out var doc) && doc != ""
            ? doc
            : Directory.GetCurrentDirectory();
        if (!Directory.Exists(root))
        {
            check["status"] = "WARN";
            check["message"] = "Cannot determine DOCUMENT_ROOT for static analysis";
            return check;
        }

        var adminPaths = AdminPaths();
        var phpFiles = EpcCiFindPhpFiles(root);
        var unscopedFiles = new List<Dictionary<string, object?>>();
        var scopedFiles = new List<Dictionary<string, object?>>();
        foreach (var file in phpFiles)
        {
            var content = ReadFile(file);
            if (content is null)
            {
                continue;
            }

            if (content.IndexOf("shop_docpart_prices_data", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var relPath = file.StartsWith(root + "/", StringComparison.Ordinal) ? file[(root.Length + 1)..] : file.Replace(root + "/", "");
            var isAdmin = adminPaths.Any(ap => relPath.IndexOf(ap, StringComparison.Ordinal) >= 0);
            if (!SqlOnTable.IsMatch(content))
            {
                continue;
            }

            if (PriceIdFilter.IsMatch(content))
            {
                scopedFiles.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["file"] = relPath, ["admin"] = isAdmin });
            }
            else
            {
                unscopedFiles.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["file"] = relPath,
                    ["admin"] = isAdmin,
                    ["risk"] = isAdmin ? "low" : "HIGH"
                });
            }
        }

        var highRisk = unscopedFiles.Where(f => Convert.ToString(f["risk"], CultureInfo.InvariantCulture) == "HIGH").ToList();
        check["total_files_scanned"] = phpFiles.Count;
        check["files_with_target_table"] = scopedFiles.Count + unscopedFiles.Count;
        check["properly_scoped"] = scopedFiles.Count;
        check["unscoped"] = unscopedFiles;
        check["high_risk_count"] = highRisk.Count;
        if (highRisk.Count > 0)
        {
            check["status"] = "WARN";
            check["message"] = highRisk.Count + " file(s) query shop_docpart_prices_data without price_id filter (non-admin)";
        }
        else
        {
            check["message"] = "All non-admin queries include price_id scoping";
        }

        return check;
    }

    public static Dictionary<string, object?> EpcCiAuditRegistryCredentials(PeakStore platform)
    {
        var check = NewCheck("Registry credential verification");
        try
        {
            if (platform.TenantsMissing)
            {
                throw new InvalidOperationException("missing");
            }

            var details = (List<object?>)check["details"]!;
            foreach (var row in platform.Tenants.Where(t => t.ErpOnlyShared == 1 && t.Status is "dns_pending" or "live").OrderBy(t => t.SiteKey, StringComparer.Ordinal))
            {
                var db = row.DbName ?? "";
                var user = row.DbUser ?? "";
                var pass = row.DbPassword ?? "";
                var entry = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["site_key"] = row.SiteKey,
                    ["db_name"] = db,
                    ["trade_name"] = row.TradeName
                };
                if (db == "" || user == "" || pass == "")
                {
                    entry["ok"] = false;
                    entry["error"] = "Missing credentials";
                    check["status"] = "FAIL";
                }
                else
                {
                    try
                    {
                        if (!ConnectTenant(db, user, pass))
                        {
                            throw new InvalidOperationException("Connection failed");
                        }

                        entry["ok"] = true;
                    }
                    catch (Exception ex)
                    {
                        entry["ok"] = false;
                        entry["error"] = ex.Message;
                        check["status"] = "FAIL";
                    }
                }

                details.Add(entry);
            }
        }
        catch (Exception ex)
        {
            check["status"] = "FAIL";
            check["error"] = ex.Message;
        }

        return check;
    }

    public static List<string> EpcCiFindPhpFiles(string root)
    {
        if (FindFiles is not null)
        {
            return FindFiles(root);
        }

        var skip = new HashSet<string>(StringComparer.Ordinal) { "vendor", "node_modules", ".git", "content/files", ".agents" };
        var files = new List<string>();
        if (!Directory.Exists(root))
        {
            return files;
        }

        void Walk(string dir)
        {
            foreach (var sub in Directory.GetDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (skip.Contains(name))
                {
                    continue;
                }

                Walk(sub);
            }

            foreach (var file in Directory.GetFiles(dir))
            {
                if (Path.GetExtension(file) == ".php")
                {
                    files.Add(file);
                }
            }
        }

        Walk(root);
        return files;
    }

    public static List<Dictionary<string, object?>> EpcCiRecentViolations(PeakStore db, int limit = 50)
    {
        EpcCiEnsureSchema(db);
        return db.Violations
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Take(limit)
            .Select(v => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = v.Id,
                ["site_key"] = v.SiteKey,
                ["actor"] = v.Actor,
                ["detail"] = v.Detail,
                ["ip"] = v.Ip,
                ["created_at"] = v.CreatedAt
            })
            .ToList();
    }

    public static Dictionary<string, object?>? EpcCiLatestAuditRun(PeakStore db)
    {
        EpcCiEnsureSchema(db);
        var row = db.AuditRuns.OrderByDescending(a => a.Id).FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["run_at"] = row.RunAt,
            ["total_tenants"] = row.TotalTenants,
            ["passed"] = row.Passed,
            ["failed"] = row.Failed,
            ["warnings"] = row.Warnings,
            ["triggered_by"] = row.TriggeredBy
        };
    }

    public static Dictionary<string, object?> EpcCiScopedQuery(PeakStore db, string sql, IEnumerable<object?> parameters, string siteKey)
    {
        var allowed = EpcCiTenantPriceIds(db, siteKey);
        var paramList = parameters.ToList();
        if (allowed.Count > 0)
        {
            var placeholders = string.Join(",", Enumerable.Repeat("?", allowed.Count));
            if (sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sql = WhereWord.Replace(sql, "WHERE `price_id` IN (" + placeholders + ") AND ", 1);
            }
            else
            {
                sql += " WHERE `price_id` IN (" + placeholders + ")";
            }

            paramList = allowed.Cast<object?>().Concat(paramList).ToList();
        }

        db.LastSql = sql;
        db.LastParams.Clear();
        db.LastParams.AddRange(paramList);
        var rows = RunSelect(db, sql, paramList);
        db.LastQuery.Clear();
        db.LastQuery.AddRange(rows);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sql"] = sql,
            ["params"] = paramList,
            ["rows"] = rows
        };
    }

    public static Dictionary<string, object?> EpcCiGetScopedPdoPrepare(PeakStore db, string siteKey, string sql)
    {
        var allowed = EpcCiTenantPriceIds(db, siteKey);
        if (allowed.Count > 0
            && sql.IndexOf("shop_docpart_prices_data", StringComparison.OrdinalIgnoreCase) >= 0
            && sql.IndexOf("price_id", StringComparison.OrdinalIgnoreCase) < 0)
        {
            var placeholders = string.Join(",", Enumerable.Repeat("?", allowed.Count));
            var inject = "`price_id` IN (" + placeholders + ")";
            if (sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sql = WhereWord.Replace(sql, "WHERE " + inject + " AND ", 1);
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sql"] = sql,
            ["allowed"] = allowed
        };
    }

    public static Dictionary<string, object?> EpcCiEnforcementScan(PeakStore db, string docroot)
    {
        _ = db;
        var results = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scanned"] = 0,
            ["violations"] = new List<object?>(),
            ["passed"] = 0
        };
        var targets = new[] { "shop_docpart_prices_data", "shop_docpart_prices" };
        var files = EpcCiFindPhpFiles(docroot);
        var violations = (List<object?>)results["violations"]!;
        foreach (var file in files)
        {
            var content = ReadFile(file);
            if (content is null)
            {
                continue;
            }

            foreach (var table in targets)
            {
                if (content.IndexOf(table, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                results["scanned"] = Convert.ToInt32(results["scanned"], CultureInfo.InvariantCulture) + 1;
                if (content.IndexOf("price_id", StringComparison.OrdinalIgnoreCase) >= 0
                    || content.IndexOf("site_key", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results["passed"] = Convert.ToInt32(results["passed"], CultureInfo.InvariantCulture) + 1;
                }
                else
                {
                    violations.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["file"] = file.Replace(docroot, ""),
                        ["table"] = table,
                        ["issue"] = "Query references " + table + " without price_id/site_key scope"
                    });
                }
            }
        }

        var scanned = Convert.ToInt32(results["scanned"], CultureInfo.InvariantCulture);
        var passed = Convert.ToInt32(results["passed"], CultureInfo.InvariantCulture);
        results["enforcement_pct"] = scanned > 0
            ? Math.Round(passed * 100.0 / scanned, 1, MidpointRounding.AwayFromZero)
            : 100.0;
        return results;
    }

    private static List<Dictionary<string, object?>> RunSelect(PeakStore db, string sql, List<object?> parameters)
    {
        var rows = db.PriceData.AsEnumerable();
        var idx = 0;
        var skuFilter = Regex.IsMatch(sql, @"\bsku\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (sql.IndexOf("`price_id` IN", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var allowed = new HashSet<int>();
            var qmarks = Regex.Matches(sql, @"\?").Count;
            var inCount = skuFilter ? Math.Max(0, qmarks - 1) : qmarks;
            for (var i = 0; i < inCount && i < parameters.Count; i++)
            {
                allowed.Add(Convert.ToInt32(parameters[i] ?? 0, CultureInfo.InvariantCulture));
            }

            idx = inCount;
            rows = rows.Where(r => allowed.Contains(r.PriceId));
        }

        if (skuFilter && idx < parameters.Count)
        {
            var sku = Convert.ToString(parameters[idx], CultureInfo.InvariantCulture) ?? "";
            rows = rows.Where(r => r.Sku == sku);
        }

        return rows.Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["price_id"] = r.PriceId,
            ["sku"] = r.Sku
        }).ToList();
    }

    private static Dictionary<string, object?> NewCheck(string name)
        => new(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["status"] = "PASS",
            ["details"] = new List<object?>()
        };

    private static List<string> AdminPaths()
        =>
        [
            "epc-site-health" + ".php",
            "epc-test-all-upload-paths" + ".php",
            "epc-upload-uae-prices" + ".php",
            "epc-price-list-reset" + ".php",
            "epc-import-r-uae" + ".php",
            "epc-seo-indexing-setup" + ".php",
            "epc-chpu-storefront-fix" + ".php",
            "epc-commerce-isolation-audit" + ".php",
            "ajax_5_import_csv_to_db" + ".php",
            "docpart_price_upload_history" + ".php",
            "epc_price_upload_diagnostics" + ".php",
            "epc_commerce_isolation" + ".php",
            "tests/"
        ];
}
