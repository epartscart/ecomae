using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-dock tenant onboard kernel. PHP identifiers kept for the inventory:
/// <c>epc_portal_platform_ip</c>, <c>epc_portal_platform_hostnames</c>,
/// <c>epc_portal_is_platform_hostname</c>, <c>epc_portal_is_epartscart_hostname</c>,
/// <c>epc_portal_tenant_templates</c>, <c>epc_portal_runtime_host_db</c>,
/// <c>epc_portal_resolve_tenant_db_credentials</c>, <c>epc_portal_client_may_share_docpart</c>,
/// <c>epc_portal_resolve_tenant_db</c>, <c>epc_portal_tenant_db_is_degraded_shared</c>,
/// <c>epc_portal_tenant_storefront_pdo</c>, <c>epc_portal_platform_pdo</c>,
/// <c>epc_portal_tenant_statuses</c>, <c>epc_portal_tenant_is_shared_erp_row</c>,
/// <c>epc_portal_tenant_setup_credentials</c>, <c>epc_portal_tenant_row_to_profile</c>,
/// <c>epc_portal_load_tenant_by_host</c>, <c>epc_portal_tenant_registry_row</c>,
/// <c>epc_portal_list_tenants</c>, <c>epc_portal_save_tenant</c>,
/// <c>epc_portal_tenant_dns_instructions</c>.
/// </summary>
public static class PhpPlanQ1Dock
{
    public const string PortalTenantPath = "content/general_pages/epc_portal_tenant.php";

    public static string PlatformIpEnv { get; set; } = "";
    public static Dictionary<string, string> Server { get; set; } = new(StringComparer.Ordinal);
    public static string DocumentRoot { get; set; } = "";
    public static string RepoRoot { get; set; } = "";
    public static Func<long> UnixNow { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<string, bool>? IsIndustrySubdomain { get; set; }
    public static Func<string, bool>? IsClientHostname { get; set; }
    public static Func<string, Dictionary<string, string>?>? RuntimeHostDb { get; set; }
    public static Func<Dictionary<string, string>>? ResolveCreds { get; set; }
    public static Func<string>? GeneratePassword { get; set; }
    public static Func<string, string, string, Dictionary<string, object?>>? ProvisionDb { get; set; }
    public static Func<string, Dictionary<string, object?>>? DecodeIntro { get; set; }
    public static Action<DockStore, string, Dictionary<string, object?>, Dictionary<string, object?>>? ApplyIntro { get; set; }
    public static Func<string, Dictionary<string, object?>>? DefaultSettings { get; set; }
    public static Action<DockStore, Dictionary<string, object?>>? SaveSettings { get; set; }
    public static Func<DockStore, string, Dictionary<string, object?>>? SyncPacks { get; set; }
    public static Func<Dictionary<string, string>?, object?>? StorefrontPdo { get; set; }
    public static Func<object?>? PlatformPdo { get; set; }
    public static Func<Dictionary<string, object?>, bool>? RowIsActive { get; set; }
    public static bool DegradedShared { get; set; }

    public sealed class TenantRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string Hostname { get; set; } = "";
        public string IndustryCode { get; set; } = "auto_parts";
        public string Status { get; set; } = "draft";
        public string TradeName { get; set; } = "";
        public string HubName { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string DbName { get; set; } = "";
        public string DbUser { get; set; } = "";
        public string DbPassword { get; set; } = "";
        public string Notes { get; set; } = "";
        public string IntroJson { get; set; } = "";
        public string HostedOn { get; set; } = "client";
        public int ErpOnlyShared { get; set; }
        public int DedicatedDb { get; set; }
        public string ScalePolicy { get; set; } = "shared_docpart";
        public string BlockchainMode { get; set; } = "anchor";
        public int IsActive { get; set; } = 1;
        public int CreatedAt { get; set; }
        public int UpdatedAt { get; set; }
    }

    public sealed class DockStore
    {
        public bool TenantsMissing { get; set; }
        public int NextId { get; set; } = 1;
        public List<TenantRow> Tenants { get; } = [];
        public List<Dictionary<string, object?>> Settings { get; } = [];
        public List<Dictionary<string, object?>> AppliedIntros { get; } = [];
    }

    public sealed class ConfigBag
    {
        public string Db { get; set; } = "";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string Host { get; set; } = "127.0.0.1";
        public string IsolationError { get; set; } = "";
    }

    private static DockStore Store { get; set; } = new();

    public static void UseStore(DockStore store) => Store = store;

    public static void Reset()
    {
        PlatformIpEnv = "";
        Server = new(StringComparer.Ordinal);
        DocumentRoot = "";
        RepoRoot = "";
        UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        IsIndustrySubdomain = null;
        IsClientHostname = null;
        RuntimeHostDb = null;
        ResolveCreds = null;
        GeneratePassword = null;
        ProvisionDb = null;
        DecodeIntro = null;
        ApplyIntro = null;
        DefaultSettings = null;
        SaveSettings = null;
        SyncPacks = null;
        StorefrontPdo = null;
        PlatformPdo = null;
        RowIsActive = null;
        DegradedShared = false;
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
            "" => true,
            "0" => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string PhpString(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static string SiteKey(string raw)
        => Regex.Replace(raw.ToLowerInvariant(), "[^a-z0-9_]", "");

    private static string ByteSubstr(string value, int max)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= max)
        {
            return value;
        }

        return System.Text.Encoding.UTF8.GetString(bytes, 0, max);
    }

    /// <summary>PHP <c>epc_portal_platform_ip</c>.</summary>
    public static string EpcPortalPlatformIp()
    {
        var ip = PlatformIpEnv.Trim();
        return ip != "" ? ip : "31.97.216.247";
    }

    /// <summary>PHP <c>epc_portal_platform_hostnames</c>.</summary>
    public static string[] EpcPortalPlatformHostnames()
        => ["www.ecomae.com", "ecomae.com", "cp.ecomae.com"];

    /// <summary>PHP <c>epc_portal_is_platform_hostname</c>.</summary>
    public static bool EpcPortalIsPlatformHostname(string? host = null)
    {
        host ??= PortalHost();
        host = host.ToLowerInvariant().Trim();
        if (EpcPortalPlatformHostnames().Contains(host, StringComparer.Ordinal))
        {
            return true;
        }

        if (IsIndustrySubdomain?.Invoke(host) == true)
        {
            return true;
        }

        return Regex.IsMatch(host, "^[a-z0-9][a-z0-9_-]*\\.ecomae\\.com$")
            && host is not ("www.ecomae.com" or "cp.ecomae.com");
    }

    /// <summary>PHP <c>epc_portal_is_epartscart_hostname</c>.</summary>
    public static bool EpcPortalIsEpartscartHostname(string? host = null)
    {
        host ??= PortalHost();
        host = host.ToLowerInvariant().Trim();
        if (host.Contains(':', StringComparison.Ordinal))
        {
            host = host.Split(':', 2)[0];
        }

        host = Regex.Replace(host, "^www\\.", "");
        return host == "epartscart.com";
    }

    /// <summary>PHP <c>epc_portal_tenant_templates</c>.</summary>
    public static Dictionary<string, Dictionary<string, object?>> EpcPortalTenantTemplates()
        => new(StringComparer.Ordinal)
        {
            ["epartscart"] = Tpl("eParts Cart (auto parts)", "www.epartscart.com", "auto_parts", "eParts Cart", "epartscart@gmail.com"),
            ["taxofinca"] = Tpl("Taxofinca (tax advisory)", "www.taxofinca.com", "tax_advisory", "Taxofin", "info@taxofinca.com"),
            ["electronicae"] = Tpl("Electronicae (electronics)", "www.electronicae.com", "electronics", "Electronicae", "hello@electronicae.com"),
            ["stylenlook"] = Tpl("Stylenlook (fashion)", "www.stylenlook.com", "fashion", "Stylenlook", "hello@stylenlook.com"),
            ["thejewellerytrend"] = Tpl("The Jewellery Trend (jewellery)", "www.thejewellerytrend.com", "jewellery", "The Jewellery Trend", "hello@thejewellerytrend.com"),
            ["erp_only_demo"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = "ERP only — shared on ecomae.com",
                ["hostname"] = "www.ecomae.com",
                ["industry"] = "erp_standalone",
                ["trade_name"] = "Client ERP",
                ["hub_name"] = "Electronic World Group",
                ["from_email"] = "admin@client.com",
                ["access_mode"] = "erp_only",
                ["hosted_on"] = "platform",
                ["erp_only_shared"] = 1,
                ["cp_role"] = "platform_control"
            },
            ["asap"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = "ASAP (ERP only — shared on ecomae.com)",
                ["hostname"] = "www.ecomae.com",
                ["industry"] = "erp_standalone",
                ["trade_name"] = "ASAP",
                ["hub_name"] = "Electronic World Group",
                ["from_email"] = "admin@asap-ae.com",
                ["access_mode"] = "erp_only",
                ["hosted_on"] = "platform",
                ["erp_only_shared"] = 1
            }
        };

    /// <summary>PHP <c>epc_portal_runtime_host_db</c>.</summary>
    public static Dictionary<string, string>? EpcPortalRuntimeHostDb(string host)
    {
        host = host.ToLowerInvariant().Trim();
        return host == "" ? null : RuntimeHostDb?.Invoke(host);
    }

    /// <summary>PHP <c>epc_portal_resolve_tenant_db_credentials</c>.</summary>
    public static Dictionary<string, string> EpcPortalResolveTenantDbCredentials()
        => ResolveCreds?.Invoke() ?? new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["db"] = "docpart",
            ["user"] = "docpart",
            ["password"] = ""
        };

    /// <summary>PHP <c>epc_portal_client_may_share_docpart</c>.</summary>
    public static bool EpcPortalClientMayShareDocpart(string? host = null)
        => EpcPortalIsEpartscartHostname(host);

    /// <summary>PHP <c>epc_portal_resolve_tenant_db</c>.</summary>
    public static void EpcPortalResolveTenantDb(ConfigBag cfg)
    {
        if (IsClientHostname?.Invoke(PortalHost()) != true)
        {
            return;
        }

        var host = PortalHost();
        var db = "";
        var user = "";
        var pass = "";
        var usesDedicated = false;
        var allowShared = EpcPortalClientMayShareDocpart(host);
        var runtime = EpcPortalRuntimeHostDb(host);
        if (runtime != null)
        {
            var overrideDb = runtime.GetValueOrDefault("db", "").ToLowerInvariant().Trim();
            if (overrideDb != "" && (allowShared || overrideDb != "docpart"))
            {
                db = runtime.GetValueOrDefault("db", "");
                user = runtime.GetValueOrDefault("user", "");
                pass = runtime.GetValueOrDefault("password", "");
                if (overrideDb != "docpart")
                {
                    usesDedicated = true;
                }
            }
        }

        if (pass == "")
        {
            var resolved = EpcPortalResolveTenantDbCredentials();
            if (resolved.GetValueOrDefault("password", "") != "")
            {
                db = resolved.GetValueOrDefault("db", "");
                user = resolved.GetValueOrDefault("user", "");
                pass = resolved.GetValueOrDefault("password", "");
            }
        }

        if ((pass == "" || db == "" || user == "") && !usesDedicated)
        {
            var resolved = EpcPortalResolveTenantDbCredentials();
            db = resolved.GetValueOrDefault("db", "");
            user = resolved.GetValueOrDefault("user", "");
            pass = resolved.GetValueOrDefault("password", "");
        }

        DegradedShared = !allowShared && db.ToLowerInvariant().Trim() == "docpart";
        cfg.Db = db;
        cfg.User = user;
        cfg.Password = pass;
        cfg.Host = "localhost";
        if (DegradedShared)
        {
            cfg.IsolationError = "degraded_shared_docpart";
        }
    }

    /// <summary>PHP <c>epc_portal_tenant_db_is_degraded_shared</c>.</summary>
    public static bool EpcPortalTenantDbIsDegradedShared() => DegradedShared;

    /// <summary>PHP <c>epc_portal_tenant_storefront_pdo</c>.</summary>
    public static object? EpcPortalTenantStorefrontPdo(Dictionary<string, string>? credentials = null)
        => StorefrontPdo?.Invoke(credentials ?? EpcPortalResolveTenantDbCredentials());

    /// <summary>PHP <c>epc_portal_platform_pdo</c>.</summary>
    public static object? EpcPortalPlatformPdo() => PlatformPdo?.Invoke();

    /// <summary>PHP <c>epc_portal_tenant_statuses</c>.</summary>
    public static Dictionary<string, string> EpcPortalTenantStatuses()
        => new(StringComparer.Ordinal)
        {
            ["draft"] = "Draft — not published",
            ["dns_pending"] = "Awaiting GoDaddy DNS",
            ["live"] = "Live on platform",
            ["suspended"] = "Suspended"
        };

    /// <summary>PHP <c>epc_portal_tenant_is_shared_erp_row</c>.</summary>
    public static bool EpcPortalTenantIsSharedErpRow(Dictionary<string, object?> row)
        => !PhpEmpty(row.TryGetValue("erp_only_shared", out var s) ? s : null)
            || PhpString(row.TryGetValue("hosted_on", out var h) ? h : "") == "platform";

    /// <summary>PHP <c>epc_portal_tenant_setup_credentials</c>.</summary>
    public static Dictionary<string, object?> EpcPortalTenantSetupCredentials(Dictionary<string, object?> row)
    {
        var host = PhpString(row.TryGetValue("hostname", out var h) ? h : "").ToLowerInvariant().Trim();
        var registryDb = SiteKey(PhpString(row.TryGetValue("db_name", out var d) ? d : ""));
        var registryUser = PhpString(row.TryGetValue("db_user", out var u) ? u : "").Trim();
        var registryPass = PhpString(row.TryGetValue("db_password", out var p) ? p : "");
        if (host != "")
        {
            var runtime = EpcPortalRuntimeHostDb(host);
            if (runtime != null)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["db"] = runtime.GetValueOrDefault("db", ""),
                    ["user"] = runtime.GetValueOrDefault("user", ""),
                    ["pass"] = runtime.GetValueOrDefault("password", ""),
                    ["registry_db"] = registryDb,
                    ["source"] = "runtime_host_db"
                };
            }
        }

        var dedicated = !PhpEmpty(row.TryGetValue("dedicated_db", out var dd) ? dd : null)
            || PhpString(row.TryGetValue("scale_policy", out var sp) ? sp : "") == "dedicated_mysql"
            || !PhpEmpty(row.TryGetValue("erp_only_shared", out var es) ? es : null)
            || (registryDb != "" && registryDb != "docpart" && registryPass != "");
        if (dedicated && registryDb != "" && registryUser != "" && registryPass != "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["db"] = registryDb,
                ["user"] = registryUser,
                ["pass"] = registryPass,
                ["registry_db"] = registryDb,
                ["source"] = "registry_dedicated"
            };
        }

        if (host != "" && (IsClientHostname?.Invoke(host) ?? DefaultIsClient(host)))
        {
            var resolved = EpcPortalResolveTenantDbCredentials();
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["db"] = resolved.GetValueOrDefault("db", ""),
                ["user"] = resolved.GetValueOrDefault("user", ""),
                ["pass"] = resolved.GetValueOrDefault("password", ""),
                ["registry_db"] = registryDb,
                ["source"] = "client_runtime"
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["db"] = registryDb,
            ["user"] = registryUser,
            ["pass"] = registryPass,
            ["registry_db"] = registryDb,
            ["source"] = "registry"
        };
    }

    /// <summary>PHP <c>epc_portal_tenant_row_to_profile</c>.</summary>
    public static Dictionary<string, object?> EpcPortalTenantRowToProfile(Dictionary<string, object?> row)
    {
        var host = PhpString(row.TryGetValue("hostname", out var h) ? h : "");
        var dbName = PhpString(row.TryGetValue("db_name", out var d) ? d : "");
        var dedicated = !PhpEmpty(row.TryGetValue("dedicated_db", out var dd) ? dd : null)
            || !PhpEmpty(row.TryGetValue("erp_only_shared", out var es) ? es : null)
            || (dbName != "" && dbName != "docpart");
        var scale = PhpString(row.TryGetValue("scale_policy", out var sp) ? sp : "");
        if (scale == "")
        {
            scale = dedicated ? "dedicated_mysql" : "shared_docpart";
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_id"] = Convert.ToInt32(row.TryGetValue("id", out var id) ? id : 0, CultureInfo.InvariantCulture),
            ["tenant_status"] = PhpString(row.TryGetValue("status", out var st) ? st : ""),
            ["site_key"] = PhpString(row.TryGetValue("site_key", out var k) ? k : ""),
            ["industry"] = PhpString(row.TryGetValue("industry_code", out var ic) ? ic : ""),
            ["domain_path"] = "https://" + host + "/",
            ["db"] = dbName,
            ["user"] = PhpString(row.TryGetValue("db_user", out var u) ? u : ""),
            ["password"] = PhpString(row.TryGetValue("db_password", out var p) ? p : ""),
            ["dedicated_db"] = dedicated ? 1 : 0,
            ["scale_policy"] = scale,
            ["trade_name"] = PhpString(row.TryGetValue("trade_name", out var tn) ? tn : ""),
            ["hub_name"] = PhpString(row.TryGetValue("hub_name", out var hn) ? hn : ""),
            ["from_email"] = PhpString(row.TryGetValue("from_email", out var fe) ? fe : ""),
            ["system_name"] = "e-world Commerce System",
            ["tagline"] = "Designed by Electronic World Group"
        };
    }

    /// <summary>PHP <c>epc_portal_load_tenant_by_host</c>.</summary>
    public static Dictionary<string, object?>? EpcPortalLoadTenantByHost(DockStore db, string host)
    {
        host = host.ToLowerInvariant().Trim();
        if (host == "" || EpcPortalIsPlatformHostname(host) || EpcPortalIsEpartscartHostname(host))
        {
            return null;
        }

        if (db.TenantsMissing)
        {
            return null;
        }

        var row = db.Tenants.FirstOrDefault(t => t.Hostname == host && t.Status is "dns_pending" or "live");
        if (row == null)
        {
            return null;
        }

        var dict = RowDict(row);
        if (!(RowIsActive?.Invoke(dict) ?? row.IsActive != 0))
        {
            return null;
        }

        return EpcPortalTenantRowToProfile(dict);
    }

    /// <summary>PHP <c>epc_portal_tenant_registry_row</c>.</summary>
    public static Dictionary<string, object?>? EpcPortalTenantRegistryRow(DockStore db, string siteKey)
    {
        var key = SiteKey(siteKey.Trim());
        if (key == "")
        {
            return null;
        }

        var row = db.Tenants.FirstOrDefault(t => t.SiteKey == key);
        return row == null ? null : RowDict(row);
    }

    /// <summary>PHP <c>epc_portal_list_tenants</c>.</summary>
    public static List<Dictionary<string, object?>> EpcPortalListTenants(DockStore db)
        => db.TenantsMissing
            ? []
            : db.Tenants.OrderBy(t => t.Hostname, StringComparer.Ordinal).Select(RowDict).ToList();

    /// <summary>PHP <c>epc_portal_save_tenant</c>.</summary>
    public static Dictionary<string, object?> EpcPortalSaveTenant(DockStore db, Dictionary<string, object?> data)
    {
        var key = SiteKey(PhpString(data.TryGetValue("site_key", out var sk) ? sk : ""));
        var hostname = PhpString(data.TryGetValue("hostname", out var hn) ? hn : "").ToLowerInvariant().Trim();
        var erpOnlyShared = !PhpEmpty(data.TryGetValue("erp_only_shared", out var es) ? es : null)
            || PhpString(data.TryGetValue("hosted_on", out var ho) ? ho : "") == "platform";
        if (erpOnlyShared)
        {
            hostname = "www.ecomae.com";
            data["hosted_on"] = "platform";
            data["erp_only_shared"] = 1;
        }

        var scalePolicy = PhpString(data.TryGetValue("scale_policy", out var sp) ? sp : "").ToLowerInvariant().Trim();
        if (scalePolicy == "")
        {
            scalePolicy = !PhpEmpty(data.TryGetValue("dedicated_db", out var dd0) ? dd0 : null) || erpOnlyShared
                ? "dedicated_mysql"
                : "shared_docpart";
        }

        if (scalePolicy is not ("dedicated_mysql" or "shared_docpart"))
        {
            scalePolicy = "shared_docpart";
        }

        var dedicatedDb = erpOnlyShared
            || scalePolicy == "dedicated_mysql"
            || !PhpEmpty(data.TryGetValue("dedicated_db", out var dd) ? dd : null);
        if (dedicatedDb)
        {
            scalePolicy = "dedicated_mysql";
        }

        var blockchainMode = NormalizeChain(PhpString(data.TryGetValue("blockchain_mode", out var bm) ? bm : "anchor"));
        var industry = SiteKey(PhpString(data.TryGetValue("industry_code", out var ic) ? ic : "auto_parts"));
        var status = PhpString(data.TryGetValue("status", out var st) ? st : "draft");
        if (!EpcPortalTenantStatuses().ContainsKey(status))
        {
            status = "draft";
        }

        if (key == "")
        {
            return Fail("Site key is required");
        }

        if (!erpOnlyShared && (hostname == "" || !hostname.Contains('.', StringComparison.Ordinal)))
        {
            return Fail("Site key and full hostname are required");
        }

        if (!erpOnlyShared && EpcPortalIsPlatformHostname(hostname))
        {
            return Fail("Cannot register a platform hostname as tenant (use shared ERP flag for ecomae.com companies)");
        }

        var dbName = SiteKey(PhpString(data.TryGetValue("db_name", out var dn) ? dn : key));
        var dbUser = SiteKey(PhpString(data.TryGetValue("db_user", out var du) ? du : dbName));
        var dbPass = PhpString(data.TryGetValue("db_password", out var dp) ? dp : "");
        if (dedicatedDb && (dbName == "" || dbName == "docpart"))
        {
            dbName = key;
            if (dbUser == "" || dbUser == "docpart")
            {
                dbUser = key;
            }
        }

        if (dedicatedDb && dbName != "" && dbPass == "")
        {
            dbPass = GeneratePassword?.Invoke() ?? "abOp12!";
            if (dbUser == "")
            {
                dbUser = dbName;
            }

            var prov = ProvisionDb?.Invoke(dbName, dbUser, dbPass)
                ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["db_name"] = dbName };
            if (PhpEmpty(prov.TryGetValue("ok", out var pok) ? pok : null))
            {
                var hint = PhpString(prov.TryGetValue("hint", out var hi) ? hi : "");
                return Fail("Dedicated MySQL provision failed for `" + dbName + "`"
                    + (hint != "" ? ": " + hint : " — set db_password manually or run epc-erp-tenant-provision.php on server"));
            }

            if (!PhpEmpty(prov.TryGetValue("db_name", out var pdn) ? pdn : null))
            {
                dbName = PhpString(pdn);
            }

            data["db_password"] = dbPass;
        }

        if (dedicatedDb && dbName != "")
        {
            if (dbName is "docpart" or "ecomae" or "epartscart")
            {
                return Fail("Dedicated tenants must use their own database — not the shared commerce or platform registry database");
            }

            var other = db.Tenants.FirstOrDefault(t =>
                t.DbName == dbName && t.SiteKey != key && (t.DedicatedDb == 1 || t.ErpOnlyShared == 1));
            if (other != null)
            {
                return Fail("Database " + dbName + " already assigned to tenant " + other.SiteKey);
            }
        }

        var introJson = "";
        if (!PhpEmpty(data.TryGetValue("intro_json", out var introObj) ? introObj : null))
        {
            introJson = introObj is Dictionary<string, object?> or System.Collections.IDictionary
                ? JsonSerializer.Serialize(introObj, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
                : PhpString(introObj);
        }

        var now = (int)UnixNow();
        var hostedOn = erpOnlyShared ? "platform" : PhpString(data.TryGetValue("hosted_on", out var hosted) ? hosted : "client");
        var sharedFlag = erpOnlyShared ? 1 : (PhpEmpty(data.TryGetValue("erp_only_shared", out var es2) ? es2 : null) ? 0 : 1);
        var existing = db.Tenants.FirstOrDefault(t => t.SiteKey == key);
        db.NextId++;
        TenantRow row;
        if (existing == null)
        {
            row = new TenantRow { Id = db.NextId - 1, SiteKey = key, CreatedAt = now };
            db.Tenants.Add(row);
        }
        else
        {
            row = existing;
            if (dbPass == "")
            {
                dbName = row.DbName;
                dbUser = row.DbUser;
                dbPass = row.DbPassword;
            }

            if (introJson == "")
            {
                introJson = row.IntroJson;
            }
        }

        row.Hostname = hostname;
        row.IndustryCode = industry;
        row.Status = status;
        row.TradeName = ByteSubstr(PhpString(data.TryGetValue("trade_name", out var tn) ? tn : key), 120);
        row.HubName = ByteSubstr(PhpString(data.TryGetValue("hub_name", out var hb) ? hb : ""), 120);
        row.FromEmail = ByteSubstr(PhpString(data.TryGetValue("from_email", out var fe) ? fe : ""), 120);
        row.DbName = dbName;
        row.DbUser = dbUser;
        row.DbPassword = dbPass;
        row.Notes = ByteSubstr(PhpString(data.TryGetValue("notes", out var nt) ? nt : ""), 500);
        row.IntroJson = introJson;
        row.HostedOn = hostedOn;
        row.ErpOnlyShared = sharedFlag;
        row.DedicatedDb = dedicatedDb ? 1 : 0;
        row.ScalePolicy = scalePolicy;
        row.BlockchainMode = blockchainMode;
        row.UpdatedAt = now;

        var intro = DecodeIntro?.Invoke(introJson) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var rowLite = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = key,
            ["hostname"] = hostname,
            ["industry_code"] = industry,
            ["trade_name"] = row.TradeName,
            ["hub_name"] = row.HubName,
            ["from_email"] = row.FromEmail
        };
        if (!PhpEmpty(intro.TryGetValue("submitted_at", out var sub) ? sub : null))
        {
            (ApplyIntro ?? ((s, host, r, i) => s.AppliedIntros.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["host"] = host,
                ["site_key"] = r["site_key"],
                ["intro"] = i
            })))(db, hostname, rowLite, intro);
        }
        else
        {
            var settings = DefaultSettings?.Invoke(hostname) ?? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["host"] = hostname,
                ["industry_code"] = "auto_parts",
                ["hub_name"] = "Electronic World Group"
            };
            settings["host"] = hostname;
            settings["industry_code"] = industry;
            if (!PhpEmpty(data.TryGetValue("trade_name", out var trade) ? trade : null))
            {
                settings["hub_name"] = PhpString(trade);
            }

            settings["domain_path"] = "https://" + hostname + "/";
            (SaveSettings ?? ((s, set) => s.Settings.Add(set)))(db, settings);
        }

        var sync = new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "skipped" };
        if (status == "live" && dbName.Trim() != "" && SyncPacks != null)
        {
            sync = SyncPacks(db, hostname);
        }

        var msg = "Tenant saved: " + hostname;
        if (status == "live" && !PhpEmpty(sync.TryGetValue("ok", out var sok) ? sok : null))
        {
            msg += " — " + PhpString(sync.TryGetValue("message", out var sm) ? sm : "client packs synced");
        }
        else if (status == "live" && PhpEmpty(sync.TryGetValue("ok", out var sok2) ? sok2 : null))
        {
            msg += " — client pack sync: " + PhpString(sync.TryGetValue("message", out var sm2) ? sm2 : "failed");
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = msg,
            ["site_key"] = key,
            ["client_sync"] = sync
        };
    }

    /// <summary>PHP <c>epc_portal_tenant_dns_instructions</c>.</summary>
    public static Dictionary<string, object?> EpcPortalTenantDnsInstructions(string hostname)
    {
        var ip = EpcPortalPlatformIp();
        var bare = Regex.Replace(hostname, "^www\\.", "");
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ip"] = ip,
            ["hostname"] = hostname,
            ["steps"] = new object[]
            {
                "Log in to GoDaddy → DNS for " + bare,
                "Add A record @ → " + ip,
                "Add A record www → " + ip,
                "Remove old A records pointing to other hosts",
                "Wait 5–60 minutes for DNS propagation",
                "In CloudPanel: add " + hostname + " as domain alias on www.ecomae.com (same docroot, no extra disk)",
                "Issue Let's Encrypt SSL for the alias",
                "Set tenant status to Live in Super CP"
            }
        };
    }

    private static string PortalHost()
    {
        var host = "";
        if (Server.TryGetValue("HTTP_HOST", out var hh) && !PhpEmpty(hh))
        {
            host = hh.ToLowerInvariant();
        }

        if (host != "" && host.Contains(':', StringComparison.Ordinal))
        {
            host = host.Split(':', 2)[0];
        }

        if (host == "" && Server.TryGetValue("SERVER_NAME", out var sn) && !PhpEmpty(sn))
        {
            host = sn.ToLowerInvariant();
        }

        return host;
    }

    private static bool DefaultIsClient(string host)
        => host != "" && !EpcPortalPlatformHostnames().Contains(host, StringComparer.Ordinal)
            && !Regex.IsMatch(host, "^[a-z0-9][a-z0-9_-]*\\.ecomae\\.com$");

    private static string NormalizeChain(string mode)
    {
        mode = mode.ToLowerInvariant().Trim();
        return mode is "off" or "anchor" or "network" ? mode : "off";
    }

    private static Dictionary<string, object?> Tpl(string label, string hostname, string industry, string trade, string email)
        => new(StringComparer.Ordinal)
        {
            ["label"] = label,
            ["hostname"] = hostname,
            ["industry"] = industry,
            ["trade_name"] = trade,
            ["hub_name"] = "Electronic World Group",
            ["from_email"] = email
        };

    private static Dictionary<string, object?> RowDict(TenantRow row)
        => new(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["site_key"] = row.SiteKey,
            ["hostname"] = row.Hostname,
            ["industry_code"] = row.IndustryCode,
            ["status"] = row.Status,
            ["trade_name"] = row.TradeName,
            ["hub_name"] = row.HubName,
            ["from_email"] = row.FromEmail,
            ["db_name"] = row.DbName,
            ["db_user"] = row.DbUser,
            ["db_password"] = row.DbPassword,
            ["notes"] = row.Notes,
            ["intro_json"] = row.IntroJson,
            ["hosted_on"] = row.HostedOn,
            ["erp_only_shared"] = row.ErpOnlyShared,
            ["dedicated_db"] = row.DedicatedDb,
            ["scale_policy"] = row.ScalePolicy,
            ["blockchain_mode"] = row.BlockchainMode,
            ["is_active"] = row.IsActive,
            ["created_at"] = row.CreatedAt,
            ["updated_at"] = row.UpdatedAt
        };

    private static Dictionary<string, object?> Fail(string message)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["message"] = message };
}
