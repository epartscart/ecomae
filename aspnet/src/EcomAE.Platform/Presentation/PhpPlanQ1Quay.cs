using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-quay tenant PDO pool. PHP identifiers kept for the inventory:
/// <c>epc_tenant_pdo</c>, <c>epc_tenant_pdo_resolve_host</c>,
/// <c>epc_tenant_pdo_from_row</c>, <c>epc_tenant_row_uses_dedicated_db</c>,
/// <c>epc_tenant_pdo_pool_stats</c>.
/// Live opens stay injectable so goldens never touch production.
/// </summary>
public static class PhpPlanQ1Quay
{
    public const string TenantPdoPath = "content/general_pages/epc_tenant_pdo.php";
    public const int DefaultPoolMax = 24;

    public sealed class PdoHandle
    {
        public int Id { get; set; }
        public string Host { get; set; } = "";
        public string Db { get; set; } = "";
        public string User { get; set; } = "";
        public bool Dead { get; set; }
    }

    private sealed class PoolEntry
    {
        public PdoHandle Pdo { get; set; } = new();
        public double Touched { get; set; }
        public long Seq { get; set; }
    }

    private static readonly Dictionary<string, PoolEntry> Pool = new(StringComparer.Ordinal);
    private static long _seq;
    private static double _clock = 1;

    public static int PoolMax { get; set; } = DefaultPoolMax;
    public static Func<double> Now { get; set; } = () => _clock++;
    public static Func<string, string, string, string, int, (PdoHandle? Pdo, string Error)> Open { get; set; }
        = (_, _, _, _, _) => (null, "SQLSTATE");
    public static Func<PdoHandle, bool> Ping { get; set; } = handle => !handle.Dead;
    public static Func<Dictionary<string, object?>?>? ResolveSharedCreds { get; set; }
    public static Func<string?>? DpConfigHost { get; set; }
    public static Func<string?>? DocumentConfigHost { get; set; }

    public static void Reset()
    {
        Pool.Clear();
        _seq = 0;
        _clock = 1;
        PoolMax = DefaultPoolMax;
        Now = () => _clock++;
        Open = (_, _, _, _, _) => (null, "SQLSTATE");
        Ping = handle => !handle.Dead;
        ResolveSharedCreds = null;
        DpConfigHost = null;
        DocumentConfigHost = null;
    }

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            0d => true,
            "" => true,
            "0" => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string PhpString(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static int PhpInt(object? value)
    {
        if (value is null || value is false)
        {
            return 0;
        }

        if (value is true)
        {
            return 1;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return (int)l;
        }

        if (value is double d)
        {
            return (int)d;
        }

        var text = PhpString(value).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return n;
        }

        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var f))
        {
            return (int)f;
        }

        return 0;
    }

    private static object? RowGet(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>PHP <c>epc_tenant_pdo_resolve_host</c>.</summary>
    public static string EpcTenantPdoResolveHost(Dictionary<string, object?> row)
    {
        var host = PhpString(RowGet(row, "db_host")).Trim();
        if (host != "")
        {
            return host;
        }

        try
        {
            var globalHost = DpConfigHost?.Invoke();
            if (!PhpEmpty(globalHost))
            {
                return PhpString(globalHost);
            }

            var fileHost = DocumentConfigHost?.Invoke();
            if (!PhpEmpty(fileHost))
            {
                return PhpString(fileHost);
            }
        }
        catch (Exception)
        {
            // fall through
        }

        return "127.0.0.1";
    }

    /// <summary>PHP <c>epc_tenant_row_uses_dedicated_db</c>.</summary>
    public static bool EpcTenantRowUsesDedicatedDb(Dictionary<string, object?> row)
    {
        if (PhpInt(RowGet(row, "dedicated_db")) == 1)
        {
            return true;
        }

        var policy = PhpString(RowGet(row, "scale_policy")).Trim().ToLowerInvariant();
        if (policy == "dedicated_mysql")
        {
            return true;
        }

        if (!PhpEmpty(RowGet(row, "erp_only_shared")))
        {
            return true;
        }

        var db = PhpString(RowGet(row, "db_name") ?? RowGet(row, "db")).Trim().ToLowerInvariant();
        return db != "" && db != "docpart";
    }

    /// <summary>PHP <c>epc_tenant_pdo_pool_stats</c>.</summary>
    public static Dictionary<string, object?> EpcTenantPdoPoolStats()
        => new(StringComparer.Ordinal)
        {
            ["max"] = PoolMax,
            ["note"] = "Process-local pool; size is not globally exposed across PHP-FPM workers."
        };

    /// <summary>PHP <c>epc_tenant_pdo_from_row</c>.</summary>
    public static (PdoHandle? Pdo, string Error) EpcTenantPdoFromRow(Dictionary<string, object?> row, Dictionary<string, object?>? opts = null)
    {
        var host = EpcTenantPdoResolveHost(row);
        var db = PhpString(RowGet(row, "db_name") ?? RowGet(row, "db")).Trim();
        var user = PhpString(RowGet(row, "db_user") ?? RowGet(row, "user")).Trim();
        var pass = PhpString(RowGet(row, "db_password") ?? RowGet(row, "db_pass") ?? RowGet(row, "password"));

        if (db == "docpart" && pass == "" && ResolveSharedCreds != null)
        {
            var creds = ResolveSharedCreds() ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            if (creds.TryGetValue("user", out var credUser) && credUser != null)
            {
                user = PhpString(credUser);
            }

            pass = creds.TryGetValue("password", out var credPass) ? PhpString(credPass) : "";
            if (db == "" || db == "docpart")
            {
                db = creds.TryGetValue("db", out var credDb) ? PhpString(credDb) : "docpart";
            }
        }

        if (host == "" || db == "" || user == "")
        {
            return (null, "Tenant DB credentials incomplete");
        }

        return EpcTenantPdo(host, db, user, pass, opts);
    }

    /// <summary>PHP <c>epc_tenant_pdo</c>.</summary>
    public static (PdoHandle? Pdo, string Error) EpcTenantPdo(string host, string db, string user, string pass, Dictionary<string, object?>? opts = null)
    {
        host = host.Trim();
        db = db.Trim();
        user = user.Trim();
        if (host == "" || db == "" || user == "")
        {
            return (null, "Missing DB connection fields");
        }

        var key = host.ToLowerInvariant() + "|" + db.ToLowerInvariant() + "|" + user;
        if (Pool.TryGetValue(key, out var hit) && hit.Pdo != null)
        {
            try
            {
                if (!Ping(hit.Pdo))
                {
                    throw new InvalidOperationException("dead");
                }

                hit.Touched = Now();
                return (hit.Pdo, "");
            }
            catch (Exception)
            {
                Pool.Remove(key);
            }
        }

        if (Pool.Count >= PoolMax)
        {
            var drop = Pool
                .OrderBy(kv => kv.Value.Touched)
                .ThenBy(kv => kv.Value.Seq)
                .Select(kv => kv.Key)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(drop))
            {
                Pool.Remove(drop);
            }
        }

        var timeout = 8;
        if (opts != null && opts.ContainsKey("timeout") && opts["timeout"] != null)
        {
            timeout = Math.Max(1, PhpInt(opts["timeout"]));
        }

        try
        {
            var opened = Open(host, db, user, pass ?? "", timeout);
            if (opened.Pdo == null)
            {
                return (null, opened.Error == "" ? "SQLSTATE" : opened.Error);
            }

            Pool[key] = new PoolEntry
            {
                Pdo = opened.Pdo,
                Touched = Now(),
                Seq = ++_seq
            };
            return (opened.Pdo, "");
        }
        catch (Exception ex)
        {
            return (null, string.IsNullOrEmpty(ex.Message) ? "SQLSTATE" : ex.Message);
        }
    }
}
