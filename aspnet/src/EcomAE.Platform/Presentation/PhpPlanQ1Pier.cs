using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-pier platform job queue. PHP identifiers kept for the inventory:
/// <c>epc_platform_jobs_pdo</c>, <c>epc_platform_jobs_ensure_schema</c>,
/// <c>epc_platform_jobs_enqueue</c>, <c>epc_platform_jobs_claim</c>,
/// <c>epc_platform_jobs_complete</c>, <c>epc_platform_jobs_fail</c>,
/// <c>epc_platform_jobs_dispatch</c>, <c>epc_platform_jobs_register_handler</c>,
/// <c>epc_platform_jobs_handle_tenant_health_ping</c>,
/// <c>epc_platform_jobs_handle_tenant_warmup</c>,
/// <c>epc_platform_jobs_run_batch</c>.
/// </summary>
public static class PhpPlanQ1Pier
{
    public const string PlatformJobsPath = "content/general_pages/epc_platform_jobs.php";

    public static Func<PierStore?>? PlatformPdo { get; set; }
    public static DateTime Clock { get; set; } = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);
    public static string HostName { get; set; } = "host";
    public static int Pid { get; set; } = 1;
    public static Func<string, Dictionary<string, object?>?>? TenantGet { get; set; }
    public static Func<Dictionary<string, object?>, (bool Ok, string Error, bool Dedicated, string DbName)>? TenantPing { get; set; }
    public static Func<string, Dictionary<string, object?>, Dictionary<string, object?>, Dictionary<string, object?>>? BlockchainBatch { get; set; }
    public static Dictionary<string, Func<string, Dictionary<string, object?>, Dictionary<string, object?>, object?>> Handlers { get; set; } = new(StringComparer.Ordinal);

    public sealed class JobRow
    {
        public long Id { get; set; }
        public string JobType { get; set; } = "";
        public string TenantKey { get; set; } = "";
        public string PayloadJson { get; set; } = "";
        public string Status { get; set; } = "queued";
        public int Priority { get; set; } = 100;
        public int Attempts { get; set; }
        public int MaxAttempts { get; set; } = 5;
        public DateTime AvailableAt { get; set; }
        public string? LockedBy { get; set; }
        public string? LastError { get; set; }
        public string? ResultJson { get; set; }
    }

    public sealed class PierStore
    {
        public bool Missing { get; set; }
        public bool FailWrites { get; set; }
        public long NextId { get; set; } = 1;
        public List<JobRow> Jobs { get; } = [];
    }

    private static PierStore Store { get; set; } = new();

    public static void UseStore(PierStore store) => Store = store;

    public static void Reset()
    {
        PlatformPdo = null;
        Clock = new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);
        HostName = "host";
        Pid = 1;
        TenantGet = null;
        TenantPing = null;
        BlockchainBatch = null;
        Handlers = new(StringComparer.Ordinal);
        Store = new();
    }

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            "" => true,
            "0" => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string PhpString(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static PierStore? Pdo()
    {
        if (PlatformPdo != null)
        {
            return PlatformPdo();
        }

        return Store.Missing ? null : Store;
    }

    /// <summary>PHP <c>epc_platform_jobs_pdo</c>.</summary>
    public static PierStore? EpcPlatformJobsPdo() => Pdo();

    /// <summary>PHP <c>epc_platform_jobs_ensure_schema</c>.</summary>
    public static void EpcPlatformJobsEnsureSchema(PierStore? db = null)
    {
        db ??= Pdo();
        _ = db;
    }

    /// <summary>PHP <c>epc_platform_jobs_enqueue</c>.</summary>
    public static int EpcPlatformJobsEnqueue(string jobType, string tenantKey = "", Dictionary<string, object?>? payload = null, Dictionary<string, object?>? opts = null)
    {
        var db = Pdo();
        if (db == null || db.FailWrites)
        {
            return 0;
        }

        jobType = jobType.ToLowerInvariant().Trim();
        tenantKey = tenantKey.ToLowerInvariant().Trim();
        if (jobType == "")
        {
            return 0;
        }

        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var priority = opts.TryGetValue("priority", out var pr) ? Convert.ToInt32(pr, CultureInfo.InvariantCulture) : 100;
        var maxAttempts = opts.TryGetValue("max_attempts", out var ma) ? Math.Max(1, Convert.ToInt32(ma, CultureInfo.InvariantCulture)) : 5;
        var delaySec = opts.TryGetValue("delay_sec", out var ds) ? Math.Max(0, Convert.ToInt32(ds, CultureInfo.InvariantCulture)) : 0;
        var dedupe = !PhpEmpty(opts.TryGetValue("dedupe", out var du) ? du : null);
        if (dedupe)
        {
            var existing = db.Jobs.Where(j => j.JobType == jobType && j.TenantKey == tenantKey && j.Status is "queued" or "running")
                .OrderByDescending(j => j.Id).Select(j => j.Id).FirstOrDefault();
            if (existing > 0)
            {
                return (int)existing;
            }
        }

        var row = new JobRow
        {
            Id = db.NextId++,
            JobType = jobType,
            TenantKey = tenantKey,
            PayloadJson = payload == null || payload.Count == 0
                ? "[]"
                : JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }),
            Status = "queued",
            Priority = priority,
            MaxAttempts = maxAttempts,
            AvailableAt = Clock.AddSeconds(delaySec)
        };
        db.Jobs.Add(row);
        return (int)row.Id;
    }

    /// <summary>PHP <c>epc_platform_jobs_claim</c>.</summary>
    public static List<Dictionary<string, object?>> EpcPlatformJobsClaim(string workerId, int limit = 5)
    {
        var db = Pdo();
        if (db == null)
        {
            return [];
        }

        workerId = WorkerId(workerId);
        limit = Math.Max(1, Math.Min(50, limit));
        var ids = db.Jobs
            .Where(j => j.Status == "queued" && j.AvailableAt <= Clock)
            .OrderBy(j => j.Priority).ThenBy(j => j.Id)
            .Take(limit)
            .Select(j => j.Id)
            .ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        foreach (var id in ids)
        {
            var row = db.Jobs.First(j => j.Id == id);
            if (row.Status != "queued")
            {
                continue;
            }

            row.Status = "running";
            row.Attempts++;
            row.LockedBy = workerId;
        }

        return db.Jobs
            .Where(j => ids.Contains(j.Id) && j.Status == "running" && j.LockedBy == workerId)
            .Select(JobDict)
            .ToList();
    }

    /// <summary>PHP <c>epc_platform_jobs_complete</c>.</summary>
    public static void EpcPlatformJobsComplete(int jobId, Dictionary<string, object?>? result = null)
    {
        var db = Pdo();
        if (db == null || jobId <= 0)
        {
            return;
        }

        var row = db.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (row == null)
        {
            return;
        }

        row.Status = "done";
        row.ResultJson = JsonSerializer.Serialize(result ?? new Dictionary<string, object?>(StringComparer.Ordinal), new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        row.LastError = null;
    }

    /// <summary>PHP <c>epc_platform_jobs_fail</c>.</summary>
    public static void EpcPlatformJobsFail(int jobId, string error, bool retry = true)
    {
        var db = Pdo();
        if (db == null || jobId <= 0)
        {
            return;
        }

        var row = db.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (row == null)
        {
            return;
        }

        var canRetry = retry && row.Attempts < row.MaxAttempts;
        var clipped = error.Length <= 2000 ? error : error[..2000];
        if (canRetry)
        {
            var delay = (int)Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, row.Attempts - 1)));
            row.Status = "queued";
            row.AvailableAt = Clock.AddSeconds(delay);
            row.LastError = clipped;
            row.LockedBy = null;
        }
        else
        {
            row.Status = "failed";
            row.LastError = clipped;
            row.LockedBy = null;
        }
    }

    /// <summary>PHP <c>epc_platform_jobs_dispatch</c>.</summary>
    public static Dictionary<string, object?> EpcPlatformJobsDispatch(Dictionary<string, object?> job)
    {
        var type = PhpString(job.TryGetValue("job_type", out var t) ? t : "").ToLowerInvariant().Trim();
        var tenantKey = PhpString(job.TryGetValue("tenant_key", out var k) ? k : "").ToLowerInvariant().Trim();
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!PhpEmpty(job.TryGetValue("payload_json", out var raw) ? raw : null))
        {
            try
            {
                var decoded = JsonSerializer.Deserialize<Dictionary<string, object?>>(PhpString(raw));
                if (decoded != null)
                {
                    payload = decoded;
                }
            }
            catch (JsonException)
            {
                payload = new Dictionary<string, object?>(StringComparer.Ordinal);
            }
        }

        if (Handlers.TryGetValue(type, out var handler))
        {
            try
            {
                var outVal = handler(tenantKey, payload, job);
                if (outVal is Dictionary<string, object?> d && d.ContainsKey("ok"))
                {
                    return d;
                }

                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = true,
                    ["result"] = outVal is Dictionary<string, object?> r ? r : new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = outVal }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = ex.Message };
            }
        }

        if (type == "tenant_health_ping")
        {
            return EpcPlatformJobsHandleTenantHealthPing(tenantKey, payload);
        }

        if (type == "tenant_warmup_pdo")
        {
            return EpcPlatformJobsHandleTenantWarmup(tenantKey, payload);
        }

        if (type == "blockchain_anchor_batch")
        {
            return BlockchainBatch?.Invoke(tenantKey, payload, job)
                ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["anchored"] = 1 } };
        }

        if (type == "noop")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["echo"] = payload }
            };
        }

        if (type == "erp_automation_tick")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Unknown job_type: erp_automation_tick" };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Unknown job_type: " + type };
    }

    /// <summary>PHP <c>epc_platform_jobs_register_handler</c>.</summary>
    public static void EpcPlatformJobsRegisterHandler(string jobType, Func<string, Dictionary<string, object?>, Dictionary<string, object?>, object?> fn)
    {
        jobType = jobType.ToLowerInvariant().Trim();
        if (jobType == "")
        {
            return;
        }

        Handlers[jobType] = fn;
    }

    /// <summary>PHP <c>epc_platform_jobs_handle_tenant_health_ping</c>.</summary>
    public static Dictionary<string, object?> EpcPlatformJobsHandleTenantHealthPing(string tenantKey, Dictionary<string, object?> payload)
    {
        if (tenantKey == "")
        {
            return Fail("tenant_key required");
        }

        if (Pdo() == null)
        {
            return Fail("Platform DB unavailable");
        }

        var row = TenantGet?.Invoke(tenantKey);
        if (row == null)
        {
            return Fail("Tenant not found");
        }

        var ping = TenantPing?.Invoke(row) ?? (true, "", !PhpEmpty(row.TryGetValue("dedicated_db", out var dd) ? dd : null), PhpString(row.TryGetValue("db_name", out var dn) ? dn : ""));
        if (!ping.Ok)
        {
            return Fail(ping.Error != "" ? ping.Error : "DB connect failed");
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tenant_key"] = tenantKey,
                ["dedicated_db"] = ping.Dedicated ? 1 : 0,
                ["db_name"] = ping.DbName,
                ["ping"] = "ok"
            }
        };
    }

    /// <summary>PHP <c>epc_platform_jobs_handle_tenant_warmup</c>.</summary>
    public static Dictionary<string, object?> EpcPlatformJobsHandleTenantWarmup(string tenantKey, Dictionary<string, object?> payload)
    {
        var ping = EpcPlatformJobsHandleTenantHealthPing(tenantKey, payload);
        if (PhpEmpty(ping.TryGetValue("ok", out var ok) ? ok : null))
        {
            return ping;
        }

        var result = ping.TryGetValue("result", out var r) && r is Dictionary<string, object?> d
            ? new Dictionary<string, object?>(d, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        result["warmup"] = true;
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["result"] = result };
    }

    /// <summary>PHP <c>epc_platform_jobs_run_batch</c>.</summary>
    public static Dictionary<string, object?> EpcPlatformJobsRunBatch(int limit = 10, string workerId = "")
    {
        if (workerId == "")
        {
            workerId = "cron-" + Md5Short(HostName + "-" + Pid.ToString(CultureInfo.InvariantCulture), 10);
        }

        var jobs = EpcPlatformJobsClaim(workerId, limit);
        var done = 0;
        var failed = 0;
        var errors = new List<string>();
        foreach (var job in jobs)
        {
            var id = Convert.ToInt32(job["id"], CultureInfo.InvariantCulture);
            var output = EpcPlatformJobsDispatch(job);
            if (!PhpEmpty(output.TryGetValue("ok", out var ok) ? ok : null))
            {
                var result = output.TryGetValue("result", out var res) && res is Dictionary<string, object?> rd
                    ? rd
                    : new Dictionary<string, object?>(StringComparer.Ordinal);
                EpcPlatformJobsComplete(id, result);
                done++;
            }
            else
            {
                var err = PhpString(output.TryGetValue("error", out var e) ? e : "Job failed");
                EpcPlatformJobsFail(id, err, true);
                failed++;
                errors.Add("#" + id.ToString(CultureInfo.InvariantCulture) + " " + err);
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["claimed"] = jobs.Count,
            ["done"] = done,
            ["failed"] = failed,
            ["errors"] = errors,
            ["worker"] = workerId
        };
    }

    private static string WorkerId(string raw)
    {
        var cleaned = Regex.Replace(raw, "[^a-zA-Z0-9_\\-\\.]", "");
        if (cleaned == "")
        {
            cleaned = "worker";
        }

        return cleaned.Length <= 64 ? cleaned : cleaned[..64];
    }

    private static Dictionary<string, object?> JobDict(JobRow row)
        => new(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["job_type"] = row.JobType,
            ["tenant_key"] = row.TenantKey,
            ["payload_json"] = row.PayloadJson,
            ["status"] = row.Status,
            ["priority"] = row.Priority,
            ["attempts"] = row.Attempts,
            ["max_attempts"] = row.MaxAttempts,
            ["locked_by"] = row.LockedBy,
            ["last_error"] = row.LastError,
            ["result_json"] = row.ResultJson
        };

    private static Dictionary<string, object?> Fail(string error)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = error };

    private static string Md5Short(string value, int len)
    {
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant()[..len];
    }
}
