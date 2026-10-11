using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EcomAE.Platform.Auth;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-slip BOS login. PHP identifiers kept for the inventory:
/// <c>epc_bos_ajax_login_secure</c>.
/// Config / PDO / session / upgrade / audit / unified context stay injected
/// so leftover parents are not mentioned.
/// </summary>
public static class PhpPlanQ1Slip
{
    public const string BosAjaxLoginPath = "content/general_pages/epc_bos_ajax_login.php";

    public sealed class UserRow
    {
        public string Table { get; set; } = "users";
        public Dictionary<string, object?> Fields { get; } = new(StringComparer.Ordinal);
    }

    public sealed class PdoStore
    {
        public bool Missing { get; set; }
        public bool ThrowOnOpen { get; set; }
        public List<UserRow> Users { get; } = [];
        public PhpPlanQ1Sec.SecStore Sec { get; } = new();
    }

    public static Dictionary<string, string> Post { get; set; } = new(StringComparer.Ordinal);
    public static string SecretSuccession { get; set; } = "";
    public static bool ConfigThrows { get; set; }
    public static PdoStore? MainPdo { get; set; }
    public static Func<PdoStore?>? PlatformPdo { get; set; }
    public static Func<PhpPlanQ1Sec.SecStore, Dictionary<string, object?>, string, Dictionary<string, object?>>? ResolveRole { get; set; }
    public static Func<string>? Csrf { get; set; }
    public static Action<Dictionary<string, object?>>? SetContext { get; set; }
    public static Action? SessionRegen { get; set; }
    public static Action<PdoStore, int, string, string>? PasswordUpgrade { get; set; }
    public static bool UpgradeFilePresent { get; set; }
    public static bool SessionFilePresent { get; set; }
    public static Action<PdoStore, int, string, string>? AuditLog { get; set; }
    public static bool AuditFilePresent { get; set; }
    public static Dictionary<string, object?>? LastContext { get; set; }
    public static int RegenCount { get; set; }
    public static int UpgradeCount { get; set; }
    public static int AuditCount { get; set; }

    public static void Reset()
    {
        Post = new(StringComparer.Ordinal);
        SecretSuccession = "";
        ConfigThrows = false;
        MainPdo = null;
        PlatformPdo = null;
        ResolveRole = null;
        Csrf = null;
        SetContext = null;
        SessionRegen = null;
        PasswordUpgrade = null;
        UpgradeFilePresent = false;
        SessionFilePresent = false;
        AuditLog = null;
        AuditFilePresent = false;
        LastContext = null;
        RegenCount = 0;
        UpgradeCount = 0;
        AuditCount = 0;
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

    private static int PhpInt(object? value)
        => value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    private static string Md5Hex(string value)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool PasswordOk(string password, string candidate, string secret)
    {
        if (candidate == "")
        {
            return false;
        }

        try
        {
            if (BCrypt.Net.BCrypt.Verify(password, candidate))
            {
                return true;
            }
        }
        catch (Exception)
        {
            // not bcrypt
        }

        if (secret != "" && Md5Hex(password + secret) == candidate)
        {
            return true;
        }

        return Md5Hex(password) == candidate;
    }

    private static Dictionary<string, object?>? FindUser(PdoStore store, string email, string password, string secret, out string storedPass)
    {
        storedPass = "";
        if (store.Missing || store.ThrowOnOpen)
        {
            return null;
        }

        foreach (var table in new[] { "users", "admin", "epc_cp_users" })
        {
            foreach (var row in store.Users.Where(u => u.Table == table))
            {
                var rowEmail = PhpString(row.Fields.TryGetValue("email", out var em) ? em : "");
                if (!string.Equals(rowEmail, email, StringComparison.Ordinal))
                {
                    continue;
                }

                var candidate = PhpString(row.Fields.TryGetValue("password", out var pw) ? pw : row.Fields.TryGetValue("pass", out var pass) ? pass : "");
                if (candidate == "")
                {
                    continue;
                }

                if (!PasswordOk(password, candidate, secret))
                {
                    continue;
                }

                var found = new Dictionary<string, object?>(row.Fields, StringComparer.Ordinal)
                {
                    ["_table"] = table
                };
                storedPass = candidate;
                return found;
            }
        }

        return null;
    }

    /// <summary>PHP <c>epc_bos_ajax_login_secure</c>.</summary>
    public static Dictionary<string, object?> EpcBosAjaxLoginSecure()
    {
        var email = (Post.TryGetValue("email", out var e) ? e : "").Trim();
        var password = Post.TryGetValue("password", out var p) ? p : "";
        if (email == "" || password == "")
        {
            return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Email and password required" };
        }

        PdoStore? mainPdo = null;
        var secret = "";
        try
        {
            if (ConfigThrows)
            {
                throw new InvalidOperationException("config");
            }

            secret = SecretSuccession;
            mainPdo = MainPdo is { Missing: false } ? MainPdo : null;
        }
        catch (Exception)
        {
            mainPdo = null;
        }

        PdoStore? platformPdo = null;
        try
        {
            platformPdo = PlatformPdo?.Invoke();
        }
        catch (Exception)
        {
            platformPdo = null;
        }

        if (mainPdo == null && platformPdo == null)
        {
            return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Platform database unavailable" };
        }

        Dictionary<string, object?>? userRow = null;
        PdoStore? authPdo = null;
        var storedPass = "";
        var passOk = false;
        foreach (var tryPdo in new[] { mainPdo, platformPdo })
        {
            if (tryPdo == null)
            {
                continue;
            }

            var found = FindUser(tryPdo, email, password, secret, out storedPass);
            if (found == null)
            {
                continue;
            }

            userRow = found;
            authPdo = tryPdo;
            passOk = true;
            break;
        }

        if (userRow == null || !passOk)
        {
            return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Invalid credentials" };
        }

        var roleStore = (authPdo ?? mainPdo)?.Sec ?? new PhpPlanQ1Sec.SecStore();
        var roleInfo = ResolveRole?.Invoke(roleStore, userRow, email)
            ?? PhpPlanQ1Sec.EpcSecBosResolveRole(roleStore, userRow, email);
        if (PhpEmpty(roleInfo.TryGetValue("allowed", out var allowed) ? allowed : null))
        {
            return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Access denied — operator credentials required" };
        }

        if (UpgradeFilePresent)
        {
            var upgradeId = PhpInt(userRow.TryGetValue("id", out var id) ? id : userRow.TryGetValue("ID", out var id2) ? id2 : userRow.TryGetValue("user_id", out var id3) ? id3 : 0);
            if (upgradeId > 0 && authPdo != null && LegacyPasswordVerifier.IsLegacyMd5(storedPass))
            {
                PasswordUpgrade?.Invoke(authPdo, upgradeId, password, storedPass);
                UpgradeCount++;
            }
        }

        var userId = PhpInt(userRow.TryGetValue("id", out var uid) ? uid : userRow.TryGetValue("ID", out var uid2) ? uid2 : userRow.TryGetValue("user_id", out var uid3) ? uid3 : 0);
        var role = PhpString(roleInfo.TryGetValue("role", out var roleObj) ? roleObj : "");
        var tenantSiteKey = PhpString(roleInfo.TryGetValue("tenant_key", out var tk) ? tk : "");

        if (SessionFilePresent)
        {
            SessionRegen?.Invoke();
        }
        else
        {
            SessionRegen?.Invoke();
        }

        RegenCount++;
        var ctx = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["role"] = role,
            ["user_id"] = userId,
            ["email"] = email,
            ["tenant_key"] = tenantSiteKey
        };
        LastContext = ctx;
        SetContext?.Invoke(ctx);

        var csrf = Csrf?.Invoke() ?? "csrf";
        if (AuditFilePresent)
        {
            var auditPdo = platformPdo ?? mainPdo;
            if (auditPdo != null)
            {
                try
                {
                    AuditLog?.Invoke(auditPdo, userId, role, email);
                    AuditCount++;
                }
                catch (Exception)
                {
                }
            }
        }

        var redirect = "/bos/";
        if (tenantSiteKey != "")
        {
            redirect = "/bos/?t=" + Uri.EscapeDataString(tenantSiteKey);
        }

        return new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["redirect"] = redirect,
            ["role"] = role,
            ["csrf"] = csrf
        };
    }
}
