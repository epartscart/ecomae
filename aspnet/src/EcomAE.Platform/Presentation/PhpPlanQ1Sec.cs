using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-sec helpers. PHP identifiers kept for the inventory:
/// <c>epc_sec_send_headers</c>, <c>epc_sec_lockdown_enabled</c>,
/// <c>epc_sec_script_risk_class</c>, <c>epc_sec_require_ops_access</c>,
/// <c>epc_sec_safe_error</c>, <c>epc_sec_csrf_token</c>,
/// <c>epc_sec_csrf_validate</c>, <c>epc_sec_require_csrf</c>,
/// <c>epc_sec_rate_limit</c>, <c>epc_sec_require_rate_limit</c>,
/// <c>epc_sec_user_has_backend_group</c>, <c>epc_sec_provider_email_allowlist</c>,
/// <c>epc_sec_email_is_provider_allowlisted</c>, <c>epc_sec_bos_resolve_role</c>.
/// </summary>
public static class PhpPlanQ1Sec
{
    public const string SecurityKernelPath = "content/general_pages/epc_security_kernel.php";

    private static readonly string[] SecretScripts =
    {
        "ecomae-find-db-pass.php",
        "ecomae-scan-db-pass.php",
        "ecomae-setup-super-admin.php",
        "epc-cp-trace.php",
        "epc-portal-setup.php",
        "chunk-receiver.php",
        "extract-zip.php"
    };

    public sealed class SecCtx
    {
        public bool HeadersSent { get; set; }
        public bool HeadersDone { get; set; }
        public string DocumentRoot { get; set; } = "";
        public string RequestUri { get; set; } = "";
        public string Method { get; set; } = "GET";
        public string RemoteAddr { get; set; } = "0";
        public string TempDir { get; set; } = Path.GetTempPath();
        public string? ProviderEmails { get; set; }
        public Dictionary<string, string> Session { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Post { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Server { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Headers { get; } = new();
        public int? HttpCode { get; set; }
        public bool Exited { get; set; }
        public string? ExitBody { get; set; }
        public Func<string>? ClientIp { get; set; }
        public Func<string[]>? AllowedIps { get; set; }
        public Func<bool>? RequireToken { get; set; }
        public Func<DateTimeOffset>? Clock { get; set; }
        public DateTimeOffset Now() => Clock?.Invoke() ?? DateTimeOffset.UtcNow;
    }

    public sealed class GroupRow
    {
        public int Id { get; set; }
        public int Parent { get; set; }
        public int ForBackend { get; set; }
    }

    public sealed class BindRow
    {
        public int UserId { get; set; }
        public int GroupId { get; set; }
    }

    public sealed class SecStore
    {
        public List<GroupRow> Groups { get; } = new();
        public List<BindRow> Binds { get; } = new();
        public bool ThrowOnQuery { get; set; }
    }

    public static void EpcSecSendHeaders(SecCtx ctx, string frame = "SAMEORIGIN")
    {
        if (ctx.HeadersSent || ctx.HeadersDone)
        {
            return;
        }

        ctx.HeadersDone = true;
        ctx.Headers.Add("X-Content-Type-Options: nosniff");
        ctx.Headers.Add("X-Frame-Options: " + frame);
        ctx.Headers.Add("Referrer-Policy: strict-origin-when-cross-origin");
        ctx.Headers.Add("Permissions-Policy: geolocation=(), microphone=(), camera=(), payment=()");
        ctx.Headers.Add("Cross-Origin-Opener-Policy: same-origin");
        ctx.Headers.Add("X-XSS-Protection: 0");
        if (Regex.IsMatch(ctx.RequestUri ?? "", @"^/(bos|cp|erp)(/|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            ctx.Headers.Add("X-Robots-Tag: noindex, nofollow, noarchive");
            ctx.Headers.Add("Cache-Control: no-store, no-cache, must-revalidate, private");
        }
    }

    public static bool EpcSecLockdownEnabled(SecCtx ctx)
    {
        var root = (ctx.DocumentRoot ?? "").TrimEnd('/');
        return root.Length > 0 && File.Exists(root + "/.epc-security-lockdown");
    }

    public static string EpcSecScriptRiskClass(string scriptBasename)
    {
        var baseName = Path.GetFileName(scriptBasename).ToLowerInvariant();
        if (SecretScripts.Contains(baseName, StringComparer.Ordinal))
        {
            return "secret";
        }

        return Regex.IsMatch(baseName, @"^(epc-|ecomae-).+\.php$", RegexOptions.CultureInvariant) ? "ops" : "app";
    }

    public static Dictionary<string, object?> EpcSecRequireOpsAccess(SecCtx ctx, string risk = "ops")
    {
        if (risk == "secret" && EpcSecLockdownEnabled(ctx))
        {
            var allowed = ctx.AllowedIps?.Invoke() ?? Array.Empty<string>();
            var ip = ctx.ClientIp?.Invoke() ?? ctx.RemoteAddr;
            if (allowed.Length == 0 || !allowed.Contains(ip, StringComparer.Ordinal))
            {
                ctx.HttpCode = 403;
                ctx.Headers.Add("Content-Type: application/json; charset=utf-8");
                ctx.Exited = true;
                ctx.ExitBody = JsonSerializer.Serialize(new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Forbidden" });
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Forbidden" };
            }
        }

        var ok = ctx.RequireToken?.Invoke() ?? false;
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = ok };
    }

    public static Dictionary<string, object?> EpcSecSafeError(SecCtx ctx, string publicMessage = "Request failed", int http = 400)
    {
        if (http >= 400 && !ctx.HeadersSent)
        {
            ctx.HttpCode = http;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = publicMessage };
    }

    public static string EpcSecCsrfToken(SecCtx ctx, string scope = "bos")
    {
        var key = "epc_csrf_" + Regex.Replace((scope ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");
        if (!ctx.Session.TryGetValue(key, out var token) || string.IsNullOrEmpty(token))
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            ctx.Session[key] = token;
        }

        return token;
    }

    public static bool EpcSecCsrfValidate(SecCtx ctx, string scope = "bos", string? token = null)
    {
        var key = "epc_csrf_" + Regex.Replace((scope ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");
        var expected = ctx.Session.TryGetValue(key, out var stored) ? stored : "";
        if (expected == "")
        {
            return false;
        }

        token ??= ctx.Post.TryGetValue("epc_csrf", out var post) ? post
            : ctx.Server.TryGetValue("HTTP_X_EPC_CSRF", out var hdr) ? hdr : "";
        return token != "" && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(token));
    }

    public static Dictionary<string, object?>? EpcSecRequireCsrf(SecCtx ctx, string scope = "bos")
    {
        var method = (ctx.Method ?? "GET").ToUpperInvariant();
        if (method is not ("POST" or "PUT" or "PATCH" or "DELETE"))
        {
            return null;
        }

        if (EpcSecCsrfValidate(ctx, scope))
        {
            return null;
        }

        ctx.HttpCode = 403;
        ctx.Headers.Add("Content-Type: application/json; charset=utf-8");
        ctx.Exited = true;
        ctx.ExitBody = JsonSerializer.Serialize(new Dictionary<string, object?> { ["ok"] = false, ["error"] = "CSRF validation failed" });
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "CSRF validation failed" };
    }

    public static bool EpcSecRateLimit(SecCtx ctx, string bucket, int maxAttempts = 20, int windowSec = 300)
    {
        var ip = ctx.ClientIp?.Invoke() ?? ctx.RemoteAddr ?? "0";
        var dir = Path.Combine(ctx.TempDir.TrimEnd('/'), "epc_rl");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, Sha256Hex(bucket + "|" + ip) + ".json");
        var now = (int)ctx.Now().ToUnixTimeSeconds();
        var dataT = now;
        var dataN = 0;
        if (File.Exists(file))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.TryGetProperty("t", out var tEl) && doc.RootElement.TryGetProperty("n", out var nEl))
                {
                    var t = tEl.GetInt32();
                    var n = nEl.GetInt32();
                    if (now - t <= windowSec)
                    {
                        dataT = t;
                        dataN = n;
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        dataN++;
        File.WriteAllText(file, JsonSerializer.Serialize(new Dictionary<string, int> { ["t"] = dataT, ["n"] = dataN }));
        return dataN <= maxAttempts;
    }

    public static Dictionary<string, object?>? EpcSecRequireRateLimit(SecCtx ctx, string bucket, int maxAttempts = 20, int windowSec = 300)
    {
        if (EpcSecRateLimit(ctx, bucket, maxAttempts, windowSec))
        {
            return null;
        }

        ctx.HttpCode = 429;
        ctx.Headers.Add("Content-Type: application/json; charset=utf-8");
        ctx.Exited = true;
        ctx.ExitBody = JsonSerializer.Serialize(new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Too many attempts — try again later" });
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Too many attempts — try again later" };
    }

    public static bool EpcSecUserHasBackendGroup(SecStore store, int userId)
    {
        if (userId <= 0)
        {
            return false;
        }

        if (store.ThrowOnQuery)
        {
            return false;
        }

        var ids = store.Groups.Where(g => g.ForBackend == 1).Select(g => g.Id).ToList();
        if (ids.Count == 0)
        {
            return false;
        }

        var all = new List<int>();
        foreach (var gid in ids)
        {
            all.Add(gid);
            all.AddRange(store.Groups.Where(g => g.Parent == gid).Select(g => g.Id));
        }

        all = all.Where(x => x != 0).Distinct().ToList();
        if (all.Count == 0)
        {
            return false;
        }

        return store.Binds.Count(b => b.UserId == userId && all.Contains(b.GroupId)) > 0;
    }

    public static string[] EpcSecProviderEmailAllowlist(SecCtx ctx)
    {
        var raw = ctx.ProviderEmails;
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = "ecomae.admin,admin@ecomae.com,hello@ecomae.com";
        }

        return raw.Split(',')
            .Select(p => p.Trim().ToLowerInvariant())
            .Where(p => p != "")
            .ToArray();
    }

    public static bool EpcSecEmailIsProviderAllowlisted(SecCtx ctx, string email)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        if (email == "")
        {
            return false;
        }

        foreach (var entry in EpcSecProviderEmailAllowlist(ctx))
        {
            if (entry == email)
            {
                return true;
            }

            if (!entry.Contains('@', StringComparison.Ordinal) && (email == entry || email.StartsWith(entry + "@", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    public static Dictionary<string, object?> EpcSecBosResolveRole(SecStore store, Dictionary<string, object?> userRow, string email)
    {
        var userId = ToInt(
            userRow.TryGetValue("id", out var id) ? id
            : userRow.TryGetValue("ID", out var id2) ? id2
            : userRow.TryGetValue("user_id", out var id3) ? id3 : 0);
        var siteKey = (Convert.ToString(userRow.TryGetValue("site_key", out var sk) ? sk : "", CultureInfo.InvariantCulture) ?? "").Trim();
        var table = Convert.ToString(userRow.TryGetValue("_table", out var tb) ? tb : "users", CultureInfo.InvariantCulture) ?? "users";
        if (siteKey != "")
        {
            return Role("tenant", siteKey, true, "tenant_site_key");
        }

        if (table is "admin" or "epc_cp_users")
        {
            return Role("provider", "", true, "admin_table");
        }

        var backend = EpcSecUserHasBackendGroup(store, userId);
        var allowlisted = EpcSecEmailIsProviderAllowlisted(new SecCtx(), email);
        if (backend || allowlisted)
        {
            return Role("provider", "", true, backend ? "backend_group" : "allowlist");
        }

        return Role("guest", "", false, "not_operator");
    }

    private static Dictionary<string, object?> Role(string role, string tenantKey, bool allowed, string reason)
        => new(StringComparer.Ordinal)
        {
            ["role"] = role,
            ["tenant_key"] = tenantKey,
            ["allowed"] = allowed,
            ["reason"] = reason
        };

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static int ToInt(object? value)
        => value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
}
