using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-wave MFA helpers. PHP identifiers kept for the inventory:
/// <c>epc_mfa_ensure_schema</c>, <c>epc_mfa_generate_secret</c>,
/// <c>epc_mfa_base32_encode</c>, <c>epc_mfa_base32_decode</c>,
/// <c>epc_mfa_totp_code</c>, <c>epc_mfa_verify_totp</c>,
/// <c>epc_mfa_otpauth_uri</c>, <c>epc_mfa_enroll</c>,
/// <c>epc_mfa_confirm_enrollment</c>, <c>epc_mfa_verify</c>,
/// <c>epc_mfa_generate_backup_codes</c>, <c>epc_mfa_verify_backup_code</c>,
/// <c>epc_mfa_get_policy</c>, <c>epc_mfa_save_policy</c>,
/// <c>epc_mfa_is_enrolled</c>, <c>epc_mfa_session_verified</c>,
/// <c>epc_mfa_session_set_verified</c>, <c>epc_mfa_required_for_user</c>,
/// <c>epc_mfa_path_requires_mfa</c>, <c>epc_mfa_enforce_route_guard</c>,
/// <c>epc_mfa_user_status</c>, <c>epc_mfa_disable</c>,
/// <c>epc_mfa_log</c>, <c>epc_mfa_recent_activity</c>,
/// <c>epc_mfa_qr_data_uri</c>, <c>epc_mfa_handle_ajax</c>,
/// <c>epc_mfa_cp_auth_gate</c>, <c>epc_mfa_erp_finance_gate</c>,
/// <c>epc_mfa_update_policy</c>.
/// </summary>
public static class PhpPlanQ1Wave
{
    public const string AuthMfaPath = "content/general_pages/epc_auth_mfa.php";
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static bool _schemaDone;
    private static Queue<byte[]> _randomQueue = new();

    public static Dictionary<string, object?> Session { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Server { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Get { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Post { get; set; } = new(StringComparer.Ordinal);
    public static Func<long> UnixNow { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<string> DateYmd { get; set; } = () => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static Func<string> Clock { get; set; } = () => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    public static Func<int, byte[]> RandomBytes { get; set; } = RandomNumberGenerator.GetBytes;
    public static string? LastRedirect { get; set; }
    public static bool ExitCalled { get; set; }

    public static void Reset()
    {
        Session = new(StringComparer.Ordinal);
        Server = new(StringComparer.Ordinal);
        Get = new(StringComparer.Ordinal);
        Post = new(StringComparer.Ordinal);
        UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        DateYmd = () => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Clock = () => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        RandomBytes = RandomNumberGenerator.GetBytes;
        LastRedirect = null;
        ExitCalled = false;
        _schemaDone = false;
        _randomQueue = new();
    }

    public static void QueueRandom(params byte[][] chunks)
    {
        foreach (var chunk in chunks)
        {
            _randomQueue.Enqueue(chunk);
        }

        RandomBytes = n =>
        {
            if (_randomQueue.Count > 0)
            {
                var next = _randomQueue.Dequeue();
                if (next.Length == n)
                {
                    return next;
                }

                var copy = new byte[n];
                Buffer.BlockCopy(next, 0, copy, 0, Math.Min(n, next.Length));
                return copy;
            }

            return RandomNumberGenerator.GetBytes(n);
        };
    }

    public sealed class SecretRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Method { get; set; } = "totp";
        public string Secret { get; set; } = "";
        public int Confirmed { get; set; }
        public string Label { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string? LastUsedAt { get; set; }
    }

    public sealed class BackupRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string CodeHash { get; set; } = "";
        public int Used { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class PolicyRow
    {
        public int Id { get; set; }
        public string TenantKey { get; set; } = "__platform__";
        public string RequireMfaForRoles { get; set; } = "";
        public string RequireMfaForPaths { get; set; } = "";
        public int GracePeriodHours { get; set; } = 72;
    }

    public sealed class AuditRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Action { get; set; } = "";
        public int Success { get; set; } = 1;
        public string IpAddress { get; set; } = "";
        public string UserAgent { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }

    public sealed class GroupRow
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class UserRow
    {
        public int UserId { get; set; }
        public int Type { get; set; }
        public string Email { get; set; } = "";
    }

    public sealed class DeptRow
    {
        public int UserId { get; set; }
        public string Department { get; set; } = "";
    }

    public sealed class WaveStore
    {
        public int NextSecretId { get; set; } = 1;
        public int NextBackupId { get; set; } = 1;
        public int NextPolicyId { get; set; } = 1;
        public int NextAuditId { get; set; } = 1;
        public bool UsersMissing { get; set; }
        public bool GroupsMissing { get; set; }
        public bool DeptsMissing { get; set; }
        public List<SecretRow> Secrets { get; } = [];
        public List<BackupRow> Backups { get; } = [];
        public List<PolicyRow> Policies { get; } = [];
        public List<AuditRow> Audits { get; } = [];
        public List<GroupRow> Groups { get; } = [];
        public List<UserRow> Users { get; } = [];
        public List<DeptRow> Depts { get; } = [];
    }

    public static void UseStore(WaveStore store) => _ = store;

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            "" => true,
            "0" => true,
            JsonElement je when je.ValueKind is JsonValueKind.Null or JsonValueKind.False => true,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0 => true,
            JsonElement je when je.ValueKind == JsonValueKind.String && (je.GetString() is "" or "0") => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    public static void EpcMfaEnsureSchema(WaveStore db)
    {
        if (_schemaDone)
        {
            return;
        }

        _schemaDone = true;
        _ = db;
    }

    public static string EpcMfaBase32Encode(string data)
    {
        var binary = new StringBuilder();
        foreach (var ch in Encoding.Latin1.GetBytes(data))
        {
            binary.Append(Convert.ToString(ch, 2).PadLeft(8, '0'));
        }

        var result = new StringBuilder();
        for (var i = 0; i < binary.Length; i += 5)
        {
            var chunk = i + 5 <= binary.Length ? binary.ToString(i, 5) : binary.ToString(i, binary.Length - i);
            chunk = chunk.PadRight(5, '0');
            result.Append(Alphabet[Convert.ToInt32(chunk, 2)]);
        }

        return result.ToString();
    }

    public static string EpcMfaBase32Decode(string input)
    {
        input = input.ToUpperInvariant().TrimEnd('=');
        var binary = new StringBuilder();
        foreach (var ch in input)
        {
            var pos = Alphabet.IndexOf(ch);
            if (pos < 0)
            {
                continue;
            }

            binary.Append(Convert.ToString(pos, 2).PadLeft(5, '0'));
        }

        var result = new StringBuilder();
        for (var i = 0; i + 8 <= binary.Length; i += 8)
        {
            result.Append((char)Convert.ToInt32(binary.ToString(i, 8), 2));
        }

        return result.ToString();
    }

    public static string EpcMfaGenerateSecret(int length = 20)
        => EpcMfaBase32Encode(Encoding.Latin1.GetString(RandomBytes(length)));

    public static string EpcMfaTotpCode(string secret, int? timeSlice = null, int digits = 6)
    {
        var slice = timeSlice ?? (int)Math.Floor(UnixNow() / 30.0);
        var key = Encoding.Latin1.GetBytes(EpcMfaBase32Decode(secret));
        var time = new byte[8];
        time[4] = (byte)((slice >> 24) & 0xFF);
        time[5] = (byte)((slice >> 16) & 0xFF);
        time[6] = (byte)((slice >> 8) & 0xFF);
        time[7] = (byte)(slice & 0xFF);
        var hmac = HMACSHA1.HashData(key, time);
        var offset = hmac[^1] & 0x0F;
        var code = (
            ((hmac[offset] & 0x7F) << 24) |
            ((hmac[offset + 1] & 0xFF) << 16) |
            ((hmac[offset + 2] & 0xFF) << 8) |
            (hmac[offset + 3] & 0xFF)
        ) % (int)Math.Pow(10, digits);
        return code.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    public static bool EpcMfaVerifyTotp(string secret, string code, int window = 1)
    {
        code = code.Trim();
        if (code == "" || code.Length != 6)
        {
            return false;
        }

        var timeSlice = (int)Math.Floor(UnixNow() / 30.0);
        for (var i = -window; i <= window; i++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(EpcMfaTotpCode(secret, timeSlice + i)),
                    Encoding.ASCII.GetBytes(code)))
            {
                return true;
            }
        }

        return false;
    }

    public static string EpcMfaOtpauthUri(string secret, string email, string issuer = "ECOM AE")
    {
        var label = Uri.EscapeDataString(issuer) + ":" + Uri.EscapeDataString(email);
        return "otpauth://totp/" + label
            + "?secret=" + Uri.EscapeDataString(secret)
            + "&issuer=" + Uri.EscapeDataString(issuer)
            + "&digits=6&period=30&algorithm=SHA1";
    }

    public static Dictionary<string, object?> EpcMfaEnroll(WaveStore db, int userId, string email)
    {
        EpcMfaEnsureSchema(db);
        var existing = db.Secrets.FirstOrDefault(s => s.UserId == userId && s.Method == "totp");
        if (existing is not null && existing.Confirmed == 1)
        {
            return new(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "TOTP already enrolled. Disable first to re-enroll."
            };
        }

        if (existing is not null)
        {
            db.Secrets.Remove(existing);
        }

        var secret = EpcMfaGenerateSecret();
        var uri = EpcMfaOtpauthUri(secret, email);
        db.Secrets.Add(new SecretRow
        {
            Id = db.NextSecretId++,
            UserId = userId,
            Method = "totp",
            Secret = secret,
            Confirmed = 0,
            Label = "TOTP (" + DateYmd() + ")",
            CreatedAt = Clock()
        });
        var backupCodes = EpcMfaGenerateBackupCodes(db, userId);
        EpcMfaLog(db, userId, "enroll_start");
        return new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["secret"] = secret,
            ["qr_uri"] = uri,
            ["backup_codes"] = backupCodes,
            ["message"] = "Scan the QR code with your authenticator app, then enter a code to confirm."
        };
    }

    public static Dictionary<string, object?> EpcMfaConfirmEnrollment(WaveStore db, int userId, string code)
    {
        EpcMfaEnsureSchema(db);
        var row = db.Secrets.FirstOrDefault(s => s.UserId == userId && s.Method == "totp" && s.Confirmed == 0);
        if (row is null)
        {
            return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "No pending TOTP enrollment found." };
        }

        if (!EpcMfaVerifyTotp(row.Secret, code))
        {
            EpcMfaLog(db, userId, "enroll_confirm_fail", false);
            return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Invalid code. Check your authenticator app and try again." };
        }

        row.Confirmed = 1;
        row.LastUsedAt = Clock();
        EpcMfaLog(db, userId, "enroll_confirmed");
        return new(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "TOTP enrollment confirmed. MFA is now active on your account." };
    }

    public static Dictionary<string, object?> EpcMfaVerify(WaveStore db, int userId, string code)
    {
        EpcMfaEnsureSchema(db);
        if (code.Trim().Length == 10)
        {
            return EpcMfaVerifyBackupCode(db, userId, code);
        }

        var row = db.Secrets.FirstOrDefault(s => s.UserId == userId && s.Method == "totp" && s.Confirmed == 1);
        if (row is null)
        {
            return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "TOTP not enrolled." };
        }

        if (!EpcMfaVerifyTotp(row.Secret, code))
        {
            EpcMfaLog(db, userId, "verify_fail", false);
            return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Invalid code." };
        }

        row.LastUsedAt = Clock();
        EpcMfaLog(db, userId, "verify_ok");
        return new(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "MFA verified." };
    }

    public static List<string> EpcMfaGenerateBackupCodes(WaveStore db, int userId, int count = 8)
    {
        EpcMfaEnsureSchema(db);
        db.Backups.RemoveAll(b => b.UserId == userId);
        var codes = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var plain = Convert.ToHexString(RandomBytes(5)).ToUpperInvariant();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(plain))).ToLowerInvariant();
            db.Backups.Add(new BackupRow
            {
                Id = db.NextBackupId++,
                UserId = userId,
                CodeHash = hash,
                Used = 0,
                CreatedAt = Clock()
            });
            codes.Add(plain);
        }

        return codes;
    }

    public static Dictionary<string, object?> EpcMfaVerifyBackupCode(WaveStore db, int userId, string code)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(code.Trim().ToUpperInvariant()))).ToLowerInvariant();
        var row = db.Backups.FirstOrDefault(b => b.UserId == userId && b.CodeHash == hash && b.Used == 0);
        if (row is null)
        {
            EpcMfaLog(db, userId, "backup_code_fail", false);
            return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Invalid backup code." };
        }

        row.Used = 1;
        EpcMfaLog(db, userId, "backup_code_used");
        return new(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "Backup code accepted.", ["is_backup"] = true };
    }

    public static Dictionary<string, object?> EpcMfaGetPolicy(WaveStore db, string tenantKey = "__platform__")
    {
        EpcMfaEnsureSchema(db);
        var defaults = DefaultPolicy();
        var row = db.Policies.FirstOrDefault(p => p.TenantKey == tenantKey);
        if (row is null)
        {
            return defaults;
        }

        var roles = TryJsonArray(row.RequireMfaForRoles);
        var paths = TryJsonArray(row.RequireMfaForPaths);
        return new(StringComparer.Ordinal)
        {
            ["require_mfa_for_roles"] = roles ?? (List<string>)defaults["require_mfa_for_roles"]!,
            ["require_mfa_for_paths"] = paths ?? (List<string>)defaults["require_mfa_for_paths"]!,
            ["grace_period_hours"] = row.GracePeriodHours
        };
    }

    public static bool EpcMfaSavePolicy(WaveStore db, Dictionary<string, object?> policy, string tenantKey = "__platform__")
    {
        EpcMfaEnsureSchema(db);
        var roles = JsonSerializer.Serialize(policy.TryGetValue("require_mfa_for_roles", out var r) ? r : new List<string>());
        var paths = JsonSerializer.Serialize(policy.TryGetValue("require_mfa_for_paths", out var p) ? p : new List<string>());
        var grace = policy.TryGetValue("grace_period_hours", out var g) ? Convert.ToInt32(g, CultureInfo.InvariantCulture) : 72;
        var row = db.Policies.FirstOrDefault(x => x.TenantKey == tenantKey);
        if (row is null)
        {
            db.Policies.Add(new PolicyRow
            {
                Id = db.NextPolicyId++,
                TenantKey = tenantKey,
                RequireMfaForRoles = roles,
                RequireMfaForPaths = paths,
                GracePeriodHours = grace
            });
        }
        else
        {
            row.RequireMfaForRoles = roles;
            row.RequireMfaForPaths = paths;
            row.GracePeriodHours = grace;
        }

        return true;
    }

    public static bool EpcMfaIsEnrolled(WaveStore db, int userId)
    {
        EpcMfaEnsureSchema(db);
        return db.Secrets.Count(s => s.UserId == userId && s.Confirmed == 1) > 0;
    }

    public static bool EpcMfaSessionVerified()
    {
        Session.TryGetValue("mfa_verified", out var verified);
        Session.TryGetValue("mfa_verified_at", out var at);
        var atLong = Convert.ToInt64(at ?? 0, CultureInfo.InvariantCulture);
        return !PhpEmpty(verified) && atLong > UnixNow() - 86400;
    }

    public static void EpcMfaSessionSetVerified()
    {
        Session["mfa_verified"] = true;
        Session["mfa_verified_at"] = UnixNow();
    }

    public static bool EpcMfaRequiredForUser(WaveStore db, int userId, string tenantKey = "__platform__")
    {
        var policy = EpcMfaGetPolicy(db, tenantKey);
        var requiredRoles = (List<string>)policy["require_mfa_for_roles"]!;
        if (PhpEmpty(requiredRoles))
        {
            return false;
        }

        List<string> userGroups;
        try
        {
            if (db.GroupsMissing)
            {
                throw new InvalidOperationException("missing");
            }

            userGroups = db.Groups.Where(g => g.UserId == userId).Select(g => g.Name).ToList();
        }
        catch (Exception)
        {
            userGroups = [];
        }

        var isAdmin = false;
        try
        {
            if (db.UsersMissing)
            {
                throw new InvalidOperationException("missing");
            }

            var user = db.Users.FirstOrDefault(u => u.UserId == userId);
            isAdmin = (user?.Type ?? 0) == 1;
        }
        catch (Exception)
        {
        }

        if (isAdmin && requiredRoles.Contains("super_admin"))
        {
            return true;
        }

        foreach (var group in userGroups)
        {
            var normalized = group.Trim().ToLowerInvariant().Replace(' ', '_');
            if (requiredRoles.Contains(normalized))
            {
                return true;
            }
        }

        try
        {
            if (db.DeptsMissing)
            {
                throw new InvalidOperationException("missing");
            }

            foreach (var dept in db.Depts.Where(d => d.UserId == userId).Select(d => d.Department))
            {
                var normalized = dept.Trim().ToLowerInvariant().Replace(' ', '_');
                if (normalized.Contains("finance", StringComparison.Ordinal) && requiredRoles.Contains("finance_user"))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
        }

        return false;
    }

    public static bool EpcMfaPathRequiresMfa(string path, WaveStore db, string tenantKey = "__platform__")
    {
        var policy = EpcMfaGetPolicy(db, tenantKey);
        foreach (var guarded in (List<string>)policy["require_mfa_for_paths"]!)
        {
            if (path.IndexOf(guarded, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    public static Dictionary<string, object?> EpcMfaEnforceRouteGuard(WaveStore db, int userId, string currentPath)
    {
        if (userId <= 0 || EpcMfaSessionVerified())
        {
            return new(StringComparer.Ordinal) { ["redirect"] = null, ["exited"] = false };
        }

        var pathNeedsMfa = EpcMfaPathRequiresMfa(currentPath, db);
        var userNeedsMfa = EpcMfaRequiredForUser(db, userId);
        if (!pathNeedsMfa && !userNeedsMfa)
        {
            return new(StringComparer.Ordinal) { ["redirect"] = null, ["exited"] = false };
        }

        Session["mfa_redirect_after"] = currentPath;
        var mode = EpcMfaIsEnrolled(db, userId) ? "verify" : "enroll";
        LastRedirect = "/cp/shop/finance/erp?epc_mfa=" + mode + "&redirect=" + Uri.EscapeDataString(currentPath);
        ExitCalled = true;
        return new(StringComparer.Ordinal) { ["redirect"] = LastRedirect, ["exited"] = true };
    }

    public static Dictionary<string, object?> EpcMfaUserStatus(WaveStore db, int userId)
    {
        EpcMfaEnsureSchema(db);
        var methods = db.Secrets
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Confirmed)
            .ThenBy(s => s.CreatedAt, StringComparer.Ordinal)
            .Select(s => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["method"] = s.Method,
                ["confirmed"] = s.Confirmed,
                ["label"] = s.Label,
                ["last_used_at"] = s.LastUsedAt,
                ["created_at"] = s.CreatedAt
            })
            .ToList();
        var unused = db.Backups.Count(b => b.UserId == userId && b.Used == 0);
        return new(StringComparer.Ordinal)
        {
            ["enrolled"] = methods.Any(m => Convert.ToInt32(m["confirmed"], CultureInfo.InvariantCulture) == 1),
            ["methods"] = methods,
            ["backup_codes_left"] = unused,
            ["session_verified"] = EpcMfaSessionVerified()
        };
    }

    public static Dictionary<string, object?> EpcMfaDisable(WaveStore db, int userId)
    {
        EpcMfaEnsureSchema(db);
        db.Secrets.RemoveAll(s => s.UserId == userId);
        db.Backups.RemoveAll(b => b.UserId == userId);
        Session.Remove("mfa_verified");
        Session.Remove("mfa_verified_at");
        EpcMfaLog(db, userId, "disabled");
        return new(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "MFA has been disabled." };
    }

    public static void EpcMfaLog(WaveStore db, int userId, string action, bool success = true)
    {
        try
        {
            EpcMfaEnsureSchema(db);
            db.Audits.Add(new AuditRow
            {
                Id = db.NextAuditId++,
                UserId = userId,
                Action = action,
                Success = success ? 1 : 0,
                IpAddress = Server.TryGetValue("REMOTE_ADDR", out var ip) ? ip : "",
                UserAgent = Clip(Server.TryGetValue("HTTP_USER_AGENT", out var ua) ? ua : "", 255),
                CreatedAt = Clock()
            });
        }
        catch (Exception)
        {
        }
    }

    public static List<Dictionary<string, object?>> EpcMfaRecentActivity(WaveStore db, int userId, int limit = 20)
    {
        EpcMfaEnsureSchema(db);
        return db.Audits
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(limit)
            .Select(a => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["action"] = a.Action,
                ["success"] = a.Success,
                ["ip_address"] = a.IpAddress,
                ["created_at"] = a.CreatedAt
            })
            .ToList();
    }

    public static string EpcMfaQrDataUri(string otpauthUri)
        => "https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=" + Uri.EscapeDataString(otpauthUri);

    public static Dictionary<string, object?> EpcMfaHandleAjax(WaveStore db, int userId)
    {
        var action = Post.TryGetValue("mfa_action", out var pa) ? pa
            : Get.TryGetValue("mfa_action", out var ga) ? ga : "";
        switch (action)
        {
            case "status":
                return EpcMfaUserStatus(db, userId);
            case "enroll":
                var email = Post.TryGetValue("email", out var em) ? em : "";
                if (email == "")
                {
                    try
                    {
                        if (db.UsersMissing)
                        {
                            throw new InvalidOperationException("missing");
                        }

                        email = db.Users.FirstOrDefault(u => u.UserId == userId)?.Email ?? "";
                    }
                    catch (Exception)
                    {
                        email = "user@ecomae.com";
                    }
                }

                return EpcMfaEnroll(db, userId, email);
            case "confirm":
                return EpcMfaConfirmEnrollment(db, userId, Post.TryGetValue("code", out var cc) ? cc : "");
            case "verify":
                var result = EpcMfaVerify(db, userId, Post.TryGetValue("code", out var vc) ? vc : "");
                if (result.TryGetValue("ok", out var ok) && ok is true)
                {
                    EpcMfaSessionSetVerified();
                }

                return result;
            case "disable":
                return EpcMfaDisable(db, userId);
            case "activity":
                return new(StringComparer.Ordinal) { ["ok"] = true, ["activity"] = EpcMfaRecentActivity(db, userId) };
            case "regenerate_backup":
                var codes = EpcMfaGenerateBackupCodes(db, userId);
                EpcMfaLog(db, userId, "backup_codes_regenerated");
                return new(StringComparer.Ordinal) { ["ok"] = true, ["backup_codes"] = codes };
            default:
                return new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Unknown MFA action" };
        }
    }

    public static Dictionary<string, object?> EpcMfaCpAuthGate(WaveStore db, int userId, string currentPath)
    {
        if (EpcMfaSessionVerified())
        {
            return new(StringComparer.Ordinal) { ["granted"] = true, ["reason"] = "mfa_session_active" };
        }

        var status = EpcMfaUserStatus(db, userId);
        var policy = EpcMfaGetPolicy(db);
        var requiredPaths = ArrayFilter(ExplodeComma(PhpString(policy["require_mfa_for_paths"])));
        var requiredRoles = ArrayFilter(ExplodeComma(PhpString(policy["require_mfa_for_roles"])));
        var pathRequiresMfa = requiredPaths.Any(pattern => currentPath.IndexOf(pattern.Trim(), StringComparison.Ordinal) >= 0);
        if (!pathRequiresMfa && requiredRoles.Count == 0)
        {
            return new(StringComparer.Ordinal) { ["granted"] = true, ["reason"] = "mfa_not_required" };
        }

        if (!(bool)status["enrolled"]!)
        {
            return new(StringComparer.Ordinal)
            {
                ["granted"] = false,
                ["reason"] = "mfa_enrollment_required",
                ["redirect"] = "/cp/mfa/enroll",
                ["grace_hours"] = Convert.ToInt32(policy["grace_period_hours"] ?? 72, CultureInfo.InvariantCulture)
            };
        }

        return new(StringComparer.Ordinal)
        {
            ["granted"] = false,
            ["reason"] = "mfa_verification_required",
            ["redirect"] = "/cp/mfa/verify"
        };
    }

    public static Dictionary<string, object?> EpcMfaErpFinanceGate(WaveStore db, int userId, string erpTab)
    {
        var sensitive = new HashSet<string>(StringComparer.Ordinal) { "gl", "vat_return", "payroll", "einvoice", "cash_bank", "balance_sheet" };
        if (!sensitive.Contains(erpTab))
        {
            return new(StringComparer.Ordinal) { ["granted"] = true, ["reason"] = "tab_not_sensitive" };
        }

        if (EpcMfaSessionVerified())
        {
            return new(StringComparer.Ordinal) { ["granted"] = true, ["reason"] = "mfa_session_active" };
        }

        var status = EpcMfaUserStatus(db, userId);
        if (!(bool)status["enrolled"]!)
        {
            return new(StringComparer.Ordinal) { ["granted"] = false, ["reason"] = "mfa_required_for_finance", ["redirect"] = "/cp/mfa/enroll" };
        }

        return new(StringComparer.Ordinal) { ["granted"] = false, ["reason"] = "mfa_challenge_required", ["redirect"] = "/cp/mfa/verify" };
    }

    public static Dictionary<string, object?> EpcMfaUpdatePolicy(WaveStore db, string tenantKey, Dictionary<string, object?> data)
    {
        EpcMfaEnsureSchema(db);
        var roles = Convert.ToString(data.TryGetValue("require_mfa_for_roles", out var r) ? r : "", CultureInfo.InvariantCulture) ?? "";
        var paths = Convert.ToString(data.TryGetValue("require_mfa_for_paths", out var p) ? p : "", CultureInfo.InvariantCulture) ?? "";
        var grace = data.TryGetValue("grace_period_hours", out var g) ? Convert.ToInt32(g ?? 72, CultureInfo.InvariantCulture) : 72;
        var row = db.Policies.FirstOrDefault(x => x.TenantKey == tenantKey);
        if (row is null)
        {
            db.Policies.Add(new PolicyRow
            {
                Id = db.NextPolicyId++,
                TenantKey = tenantKey,
                RequireMfaForRoles = roles,
                RequireMfaForPaths = paths,
                GracePeriodHours = grace
            });
        }
        else
        {
            row.RequireMfaForRoles = roles;
            row.RequireMfaForPaths = paths;
            row.GracePeriodHours = grace;
        }

        return new(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "MFA policy updated" };
    }

    private static Dictionary<string, object?> DefaultPolicy()
        => new(StringComparer.Ordinal)
        {
            ["require_mfa_for_roles"] = new List<string> { "super_admin", "finance_admin", "finance_user" },
            ["require_mfa_for_paths"] = new List<string> { "/cp/shop/finance/", "/cp/content/shop/finance/" },
            ["grace_period_hours"] = 72
        };

    private static List<string>? TryJsonArray(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return doc.RootElement.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.GetRawText()).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string PhpString(object? value)
        => value switch
        {
            null => "",
            string s => s,
            List<string> => "Array",
            IEnumerable<string> => "Array",
            System.Collections.IDictionary => "Array",
            System.Collections.IEnumerable and not string => "Array",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    private static List<string> ExplodeComma(string raw)
        => raw == "" ? [""] : raw.Split(',').ToList();

    private static List<string> ArrayFilter(List<string> items)
        => items.Where(s => !PhpEmpty(s)).ToList();

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];
}
