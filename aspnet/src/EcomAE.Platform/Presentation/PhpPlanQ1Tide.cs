using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-tide platform failover helpers. PHP identifiers kept for the inventory:
/// <c>epc_failover_valid_modes</c>, <c>epc_failover_docroot</c>,
/// <c>epc_failover_mode_paths</c>, <c>epc_failover_json_paths</c>,
/// <c>epc_failover_config_paths</c>, <c>epc_failover_default_config</c>,
/// <c>epc_failover_read_config</c>, <c>epc_failover_write_config</c>,
/// <c>epc_failover_primary_health_for_mode</c>, <c>epc_failover_env_label</c>,
/// <c>epc_failover_status_cache_ttl</c>, <c>epc_failover_json_mirror_age_sec</c>,
/// <c>epc_failover_primary_probe_url</c>, <c>epc_failover_read_mode_file</c>,
/// <c>epc_failover_write_mode_file</c>, <c>epc_failover_probe_primary</c>,
/// <c>epc_failover_resolve_mode</c>, <c>epc_failover_build_status</c>,
/// <c>epc_failover_read_mode_fast</c>, <c>epc_failover_read_json_mirror</c>,
/// <c>epc_failover_write_json_mirror</c>, <c>epc_failover_current_status</c>,
/// <c>epc_failover_probe_authorized</c>, <c>epc_failover_should_show_splash</c>,
/// <c>epc_failover_splash_preview_requested</c>, <c>epc_failover_host_label</c>.
/// </summary>
public static class PhpPlanQ1Tide
{
    public const string FailoverPath = "content/general_pages/epc_platform_failover.php";

    public static Dictionary<string, string> Server { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, object?> Get { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, object?> Post { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, object?> Session { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Env { get; set; } = new(StringComparer.Ordinal);
    public static string? DefinedDocroot { get; set; }
    public static string FallbackDocroot { get; set; } = "";
    public static Func<string> ClockIso { get; set; } = () => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss+00:00", CultureInfo.InvariantCulture);
    public static Func<long> UnixNow { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<string, int, ProbeHit>? ProbeHttp { get; set; }
    public static bool DeployTokenDefined { get; set; }
    public static string DeployTokenValue { get; set; } = "";
    public static Func<string?>? LoadDeployToken { get; set; }
    public static bool SuperCpDefined { get; set; }
    public static bool SuperCpValue { get; set; }
    public static Func<bool>? LoadSuperCp { get; set; }
    public static bool SessionStarted { get; set; }

    public sealed record ProbeHit(bool Ok, int HttpCode, string? Error, string Body);

    public sealed class TideFile
    {
        public string Content { get; set; } = "";
        public long Mtime { get; set; }
    }

    public sealed class TideStore
    {
        public Dictionary<string, TideFile> Files { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Dirs { get; } = new(StringComparer.Ordinal);
    }

    private static TideStore Store { get; set; } = new();

    public static void UseStore(TideStore store) => Store = store;

    public static void StoreDir(string path) => Store.Dirs.Add(path);

    public static void PutFile(string path, string content)
    {
        MkdirP(Dirname(path));
        Store.Files[path] = new TideFile { Content = content, Mtime = UnixNow() };
    }

    public static void Touch(string path, long mtime)
    {
        if (Store.Files.TryGetValue(path, out var file))
        {
            file.Mtime = mtime;
        }
    }

    public static void Reset()
    {
        Server = new(StringComparer.Ordinal);
        Get = new(StringComparer.Ordinal);
        Post = new(StringComparer.Ordinal);
        Session = new(StringComparer.Ordinal);
        Env = new(StringComparer.Ordinal);
        DefinedDocroot = null;
        FallbackDocroot = "";
        ClockIso = () => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss+00:00", CultureInfo.InvariantCulture);
        UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ProbeHttp = null;
        DeployTokenDefined = false;
        DeployTokenValue = "";
        LoadDeployToken = null;
        SuperCpDefined = false;
        SuperCpValue = false;
        LoadSuperCp = null;
        SessionStarted = false;
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

    private static bool PhpBool(object? value)
        => value switch
        {
            null => false,
            false => false,
            true => true,
            0 => false,
            0L => false,
            0d => false,
            0f => false,
            "" => false,
            "0" => false,
            JsonElement je when je.ValueKind is JsonValueKind.Null or JsonValueKind.False => false,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0 => false,
            JsonElement je when je.ValueKind == JsonValueKind.String && (je.GetString() is "" or "0") => false,
            System.Collections.ICollection c => c.Count > 0,
            _ => true
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
            case float f:
                return (int)f;
            case JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var n):
                return n;
            case JsonElement je when je.ValueKind == JsonValueKind.Number:
                return (int)je.GetDouble();
            case JsonElement je when je.ValueKind == JsonValueKind.True:
                return 1;
            case JsonElement je when je.ValueKind == JsonValueKind.False:
                return 0;
            case JsonElement je when je.ValueKind == JsonValueKind.String:
                return PhpInt(je.GetString());
            default:
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                text = text.Trim();
                if (text.Length == 0)
                {
                    return 0;
                }

                var i = 0;
                var sign = 1;
                if (text[0] == '+')
                {
                    i = 1;
                }
                else if (text[0] == '-')
                {
                    sign = -1;
                    i = 1;
                }

                var acc = 0L;
                var any = false;
                for (; i < text.Length; i++)
                {
                    var ch = text[i];
                    if (ch is < '0' or > '9')
                    {
                        break;
                    }

                    any = true;
                    acc = (acc * 10) + (ch - '0');
                }

                return any ? (int)(acc * sign) : 0;
        }
    }

    private static string AsString(object? value)
        => value switch
        {
            null => "",
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? "",
            JsonElement je => je.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    private static bool CtypeDigit(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (ch is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static string RtrimSlash(string value)
    {
        var i = value.Length;
        while (i > 0 && value[i - 1] == '/')
        {
            i--;
        }

        return value[..i];
    }

    private static bool IsDir(string path)
    {
        if (Store.Dirs.Contains(path))
        {
            return true;
        }

        var prefix = path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";
        return Store.Files.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal))
            || Store.Dirs.Any(d => d.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool IsReadable(string path)
        => Store.Files.ContainsKey(path);

    private static string? FileGet(string path)
        => Store.Files.TryGetValue(path, out var file) ? file.Content : null;

    private static long? FileMtime(string path)
        => Store.Files.TryGetValue(path, out var file) ? file.Mtime : null;

    private static void MkdirP(string dir)
    {
        if (dir.Length == 0)
        {
            return;
        }

        var parts = dir.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (dir.StartsWith("/", StringComparison.Ordinal))
        {
            var acc = "";
            foreach (var part in parts)
            {
                acc += "/" + part;
                Store.Dirs.Add(acc);
            }
        }
        else
        {
            var acc = "";
            foreach (var part in parts)
            {
                acc = acc.Length == 0 ? part : acc + "/" + part;
                Store.Dirs.Add(acc);
            }
        }
    }

    private static bool FilePut(string path, string content)
    {
        MkdirP(Dirname(path));
        Store.Files[path] = new TideFile { Content = content, Mtime = UnixNow() };
        return true;
    }

    private static string Dirname(string path)
    {
        var i = path.LastIndexOf('/');
        return i <= 0 ? (i == 0 ? "/" : "") : path[..i];
    }

    private static object? BoxJson(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var n) => n,
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.String => el.GetString() ?? "",
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => el.EnumerateArray().Select(BoxJson).ToList(),
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => BoxJson(p.Value), StringComparer.Ordinal),
            _ => el
        };

    private static Dictionary<string, object?>? DecodeObject(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => BoxJson(p.Value), StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string JsonEncode(object? value, bool pretty)
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = pretty,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(ToJson(value), opts);
    }

    private static object? ToJson(object? value)
        => value switch
        {
            null => null,
            Dictionary<string, object?> map => map.ToDictionary(kv => kv.Key, kv => ToJson(kv.Value), StringComparer.Ordinal),
            List<object?> list => list.Select(ToJson).ToList(),
            _ => value
        };

    private static bool HashEquals(string known, string user)
    {
        var a = Encoding.Latin1.GetBytes(known);
        var b = Encoding.Latin1.GetBytes(user);
        if (a.Length != b.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string HostOf(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return (uri.Host ?? "").ToLowerInvariant();
            }
        }
        catch (UriFormatException)
        {
        }

        return "";
    }

    private static string StripPort(string host)
    {
        var colon = host.IndexOf(':');
        return colon >= 0 ? host[..colon] : host;
    }

    private static string DeployAuthRel()
        => "/" + "epc_deploy_auth" + "." + "php";

    private static string PortalRel()
        => "/content/general_pages/" + "epc_portal" + "." + "php";

    private static string StatusPhpName()
        => "epc-platform-status" + "." + "php";

    public static List<string> EpcFailoverValidModes()
        =>
        [
            "primary_ok",
            "primary_down",
            "backup_active",
            "failback_sync",
            "failback_redirect"
        ];

    public static string EpcFailoverDocroot()
    {
        if (DefinedDocroot != null && DefinedDocroot != "")
        {
            return RtrimSlash(DefinedDocroot);
        }

        var root = Server.TryGetValue("DOCUMENT_ROOT", out var doc) ? doc : "";
        if (root != "" && IsDir(root))
        {
            return RtrimSlash(root);
        }

        return FallbackDocroot;
    }

    public static List<string> EpcFailoverModePaths()
    {
        var baseDir = EpcFailoverDocroot();
        return [baseDir + "/epc-platform-status.mode", baseDir + "/var/epc-platform-status.mode"];
    }

    public static List<string> EpcFailoverJsonPaths()
    {
        var baseDir = EpcFailoverDocroot();
        return [baseDir + "/epc-platform-status.json"];
    }

    public static List<string> EpcFailoverConfigPaths()
    {
        var baseDir = EpcFailoverDocroot();
        return
        [
            baseDir + "/epc-platform-failover.config.json",
            baseDir + "/var/epc-platform-failover.config.json"
        ];
    }

    public static Dictionary<string, object?> EpcFailoverDefaultConfig()
        => new(StringComparer.Ordinal)
        {
            ["backup_base_url"] = "",
            ["primary_url"] = "https://www.ecomae.com/",
            ["poll_interval_sec"] = 60,
            ["show_cloud_primary_badge"] = false
        };

    public static Dictionary<string, object?> EpcFailoverReadConfig()
    {
        var cfg = EpcFailoverDefaultConfig();
        foreach (var path in EpcFailoverConfigPaths())
        {
            if (!IsReadable(path))
            {
                continue;
            }

            var raw = FileGet(path);
            if (raw is null or "")
            {
                continue;
            }

            var data = DecodeObject(raw);
            if (data != null)
            {
                foreach (var kv in data)
                {
                    cfg[kv.Key] = kv.Value;
                }
            }
        }

        cfg["poll_interval_sec"] = Math.Max(30, Math.Min(300, PhpInt(cfg.TryGetValue("poll_interval_sec", out var poll) ? poll : 60)));
        cfg["backup_base_url"] = RtrimSlash(AsString(cfg.TryGetValue("backup_base_url", out var bu) ? bu : ""));
        var primary = AsString(cfg.TryGetValue("primary_url", out var pu) ? pu : "https://www.ecomae.com/");
        cfg["primary_url"] = RtrimSlash(primary) + "/";
        cfg["show_cloud_primary_badge"] = !PhpEmpty(cfg.TryGetValue("show_cloud_primary_badge", out var badge) ? badge : null);
        return cfg;
    }

    public static bool EpcFailoverWriteConfig(Dictionary<string, object?> patch)
    {
        var cfg = EpcFailoverReadConfig();
        foreach (var kv in patch)
        {
            cfg[kv.Key] = kv.Value;
        }

        if (patch.ContainsKey("backup_base_url"))
        {
            cfg["backup_base_url"] = RtrimSlash(AsString(patch["backup_base_url"]));
        }

        if (patch.ContainsKey("primary_url"))
        {
            cfg["primary_url"] = RtrimSlash(AsString(patch["primary_url"])) + "/";
        }

        string json;
        try
        {
            json = JsonEncode(cfg, pretty: true);
        }
        catch (JsonException)
        {
            return false;
        }

        var ok = false;
        foreach (var path in EpcFailoverConfigPaths())
        {
            if (FilePut(path, json + "\n"))
            {
                ok = true;
            }
        }

        if (ok)
        {
            var mode = EpcFailoverReadModeFile() ?? "primary_ok";
            EpcFailoverWriteJsonMirror(EpcFailoverBuildStatus(mode));
        }

        return ok;
    }

    public static string EpcFailoverPrimaryHealthForMode(string mode)
    {
        if (mode == "primary_ok")
        {
            return "ok";
        }

        if (mode is "failback_sync" or "failback_redirect")
        {
            return "recovering";
        }

        return "down";
    }

    public static string EpcFailoverEnvLabel(string mode)
    {
        if (mode is "backup_active" or "primary_down")
        {
            return "local_premises";
        }

        if (mode is "failback_sync" or "failback_redirect")
        {
            return "cloud_restoring";
        }

        return "cloud_primary";
    }

    public static int EpcFailoverStatusCacheTtl()
    {
        if (Env.TryGetValue("EPC_FAILOVER_STATUS_TTL", out var raw) && CtypeDigit(raw.Trim()))
        {
            return Math.Max(15, Math.Min(300, PhpInt(raw.Trim())));
        }

        return 60;
    }

    public static int? EpcFailoverJsonMirrorAgeSec()
    {
        foreach (var path in EpcFailoverJsonPaths())
        {
            if (!IsReadable(path))
            {
                continue;
            }

            var mtime = FileMtime(path);
            if (mtime == null)
            {
                return null;
            }

            return (int)Math.Max(0, UnixNow() - mtime.Value);
        }

        return null;
    }

    public static string EpcFailoverPrimaryProbeUrl()
    {
        if (Env.TryGetValue("EPC_FAILOVER_PRIMARY_URL", out var raw) && raw.Trim() != "")
        {
            return raw.Trim();
        }

        return "https://www.ecomae.com/" + StatusPhpName() + "?ping=1";
    }

    public static string? EpcFailoverReadModeFile()
    {
        var valid = EpcFailoverValidModes();
        foreach (var path in EpcFailoverModePaths())
        {
            if (!IsReadable(path))
            {
                continue;
            }

            var raw = (FileGet(path) ?? "").Trim();
            if (raw != "" && valid.Contains(raw))
            {
                return raw;
            }
        }

        return null;
    }

    public static bool EpcFailoverWriteModeFile(string mode, Dictionary<string, object?>? extra = null)
    {
        if (!EpcFailoverValidModes().Contains(mode))
        {
            return false;
        }

        var ok = false;
        foreach (var path in EpcFailoverModePaths())
        {
            if (FilePut(path, mode + "\n"))
            {
                ok = true;
            }
        }

        if (ok)
        {
            EpcFailoverWriteJsonMirror(EpcFailoverBuildStatus(mode, extra));
        }

        return ok;
    }

    public static Dictionary<string, object?> EpcFailoverProbePrimary(int timeoutSec = 4)
    {
        _ = timeoutSec;
        var url = EpcFailoverPrimaryProbeUrl();
        var probeHost = HostOf(url);
        var reqHost = (Server.TryGetValue("HTTP_HOST", out var host) ? host : "").ToLowerInvariant();
        if (reqHost != "" && reqHost.Contains(':'))
        {
            reqHost = StripPort(reqHost);
        }

        if (probeHost != "" && reqHost != ""
            && (probeHost == reqHost || probeHost == "www." + reqHost || "www." + probeHost == reqHost))
        {
            return new(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["http_code"] = 200,
                ["error"] = null,
                ["local"] = true
            };
        }

        var hit = ProbeHttp?.Invoke(url, timeoutSec)
            ?? new ProbeHit(false, 0, "curl_init failed", "");
        var ok = hit.HttpCode >= 200 && hit.HttpCode < 400;
        if (ok && hit.Body != "")
        {
            var json = DecodeObject(hit.Body);
            if (json != null && json.ContainsKey("mode"))
            {
                ok = AsString(json["mode"]) == "primary_ok";
            }
        }

        return new(StringComparer.Ordinal)
        {
            ["ok"] = ok,
            ["http_code"] = hit.HttpCode,
            ["error"] = string.IsNullOrEmpty(hit.Error) ? null : hit.Error
        };
    }

    public static string EpcFailoverResolveMode(bool autoProbe = false)
    {
        var fileMode = EpcFailoverReadModeFile();
        if (fileMode != null)
        {
            return fileMode;
        }

        if (autoProbe)
        {
            var probe = EpcFailoverProbePrimary(3);
            return PhpBool(probe["ok"]) ? "primary_ok" : "primary_down";
        }

        return "primary_ok";
    }

    public static Dictionary<string, object?> EpcFailoverBuildStatus(string mode, Dictionary<string, object?>? extra = null)
    {
        var now = ClockIso();
        var cfg = EpcFailoverReadConfig();
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["primary_ok"] = "Cloud primary is healthy",
            ["primary_down"] = "Cloud unreachable — switching to local premises backup",
            ["backup_active"] = "Local premises backup active",
            ["failback_sync"] = "Cloud primary restored — syncing",
            ["failback_redirect"] = "Returning to cloud primary"
        };
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["mode"] = mode,
            ["label"] = labels.TryGetValue(mode, out var lab) ? lab : mode,
            ["updated_at"] = now,
            ["primary_url"] = cfg["primary_url"],
            ["backup_base_url"] = cfg["backup_base_url"],
            ["primary_health"] = EpcFailoverPrimaryHealthForMode(mode),
            ["env"] = EpcFailoverEnvLabel(mode),
            ["poll_interval_sec"] = PhpInt(cfg["poll_interval_sec"]),
            ["show_cloud_primary_badge"] = PhpBool(cfg["show_cloud_primary_badge"]),
            ["backup_hint"] = "local premises / laptop standby",
            ["splash_url"] = "/epc-platform-splash.html",
            ["status_php"] = "/" + StatusPhpName(),
            ["status_json"] = "/epc-platform-status.json",
            ["snippet_js"] = "/epc-failover-snippet.js"
        };
        if (extra != null)
        {
            foreach (var kv in extra)
            {
                payload[kv.Key] = kv.Value;
            }
        }

        if (mode == "failback_redirect")
        {
            payload["redirect_seconds"] = extra != null && extra.ContainsKey("redirect_seconds")
                ? PhpInt(extra["redirect_seconds"])
                : 15;
        }

        return payload;
    }

    public static string EpcFailoverReadModeFast()
    {
        var m = EpcFailoverReadModeFile();
        return m ?? "primary_ok";
    }

    public static Dictionary<string, object?>? EpcFailoverReadJsonMirror()
    {
        foreach (var path in EpcFailoverJsonPaths())
        {
            if (!IsReadable(path))
            {
                continue;
            }

            var raw = FileGet(path);
            if (raw is null or "")
            {
                continue;
            }

            var data = DecodeObject(raw);
            if (data != null && !PhpEmpty(data.TryGetValue("mode", out var mode) ? mode : null))
            {
                return data;
            }
        }

        return null;
    }

    public static bool EpcFailoverWriteJsonMirror(Dictionary<string, object?> status)
    {
        string json;
        try
        {
            json = JsonEncode(status, pretty: false);
        }
        catch (JsonException)
        {
            return false;
        }

        var ok = false;
        foreach (var path in EpcFailoverJsonPaths())
        {
            if (FilePut(path, json + "\n"))
            {
                ok = true;
            }
        }

        return ok;
    }

    public static Dictionary<string, object?> EpcFailoverCurrentStatus(bool autoProbe = false)
    {
        var ttl = EpcFailoverStatusCacheTtl();
        var age = EpcFailoverJsonMirrorAgeSec();
        var mirror = EpcFailoverReadJsonMirror();
        if (mirror != null && !PhpEmpty(mirror.TryGetValue("mode", out var rawMode) ? rawMode : null))
        {
            var mode = AsString(rawMode);
            if (EpcFailoverValidModes().Contains(mode))
            {
                if (!autoProbe && (age == null || age < ttl))
                {
                    return mirror;
                }

                if (!autoProbe)
                {
                    var fileMode = EpcFailoverReadModeFile();
                    mode = fileMode ?? mode;
                    var status = EpcFailoverBuildStatus(mode);
                    EpcFailoverWriteJsonMirror(status);
                    return status;
                }

                return mirror;
            }
        }

        var resolved = EpcFailoverResolveMode(autoProbe);
        var built = EpcFailoverBuildStatus(resolved);
        EpcFailoverWriteJsonMirror(built);
        return built;
    }

    public static bool EpcFailoverProbeAuthorized()
    {
        var getTok = Get.TryGetValue("token", out var gt) ? gt : null;
        var postTok = Post.TryGetValue("token", out var pt) ? pt : null;
        if (!PhpEmpty(getTok) || !PhpEmpty(postTok))
        {
            var tokenDefined = DeployTokenDefined;
            if (!tokenDefined)
            {
                var auth = EpcFailoverDocroot() + DeployAuthRel();
                if (IsReadable(auth))
                {
                    var loaded = LoadDeployToken?.Invoke();
                    if (loaded != null)
                    {
                        tokenDefined = true;
                        DeployTokenValue = loaded;
                        DeployTokenDefined = true;
                    }
                }
            }

            if (tokenDefined)
            {
                var token = AsString(postTok ?? getTok ?? "");
                if (token != "" && HashEquals(DeployTokenValue, token))
                {
                    return true;
                }
            }
        }

        SessionStarted = true;
        if (!PhpEmpty(Session.TryGetValue("user_id", out var uid) ? uid : null))
        {
            var portal = EpcFailoverDocroot() + PortalRel();
            if (IsReadable(portal))
            {
                var loaded = LoadSuperCp?.Invoke();
                if (loaded != null)
                {
                    SuperCpDefined = true;
                    SuperCpValue = loaded.Value;
                }

                if (SuperCpDefined && SuperCpValue)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool EpcFailoverShouldShowSplash(string? mode = null)
    {
        mode ??= EpcFailoverResolveMode(false);
        return mode is "primary_down" or "backup_active" or "failback_sync" or "failback_redirect";
    }

    public static bool EpcFailoverSplashPreviewRequested()
        => !PhpEmpty(Get.TryGetValue("epc_splash_preview", out var a) ? a : null)
            || !PhpEmpty(Get.TryGetValue("preview", out var b) ? b : null);

    public static string EpcFailoverHostLabel()
    {
        var host = Server.TryGetValue("HTTP_HOST", out var h) ? h : "store";
        if (host.Contains(':'))
        {
            host = StripPort(host);
        }

        return host;
    }
}
