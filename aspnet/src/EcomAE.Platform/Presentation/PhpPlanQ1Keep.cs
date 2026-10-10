using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-keep helpers. PHP identifiers kept for the inventory:
/// <c>epc_industry_seo_sub_slug</c>, <c>epc_industry_seo_sub_presentation</c>,
/// <c>epc_industry_seo_template_sub_categories</c>, <c>epc_industry_seo_host_map</c>,
/// <c>epc_industry_seo_primary_host</c>, <c>epc_industry_seo_site_url_for_template</c>,
/// <c>epc_industry_seo_template_sub_industries</c>, <c>epc_industry_seo_sitemap_entries</c>,
/// <c>epc_industry_seo_match_request_sub</c>, <c>epc_industry_seo_request_host</c>,
/// <c>epc_industry_seo_is_industry_host</c>, <c>epc_industry_seo_request_host_base</c>,
/// <c>epc_boc_platform_group_ids</c>, <c>epc_boc_tenant_group_ids</c>,
/// <c>epc_boc_tenant_area_ids</c>, <c>epc_boc_tenant_mode_platform_strip</c>,
/// <c>epc_boc_session_boot</c>, <c>epc_boc_active_tenant</c>,
/// <c>epc_boc_set_active_tenant</c>, <c>epc_boc_clear_active_tenant</c>,
/// <c>epc_boc_tenant_module_url</c>, <c>epc_boc_switcher_tenants</c>,
/// <c>epc_boc_handle_tenant_switch</c>, <c>epc_boc_nav_apply_tenant_scope</c>,
/// <c>epc_boc_scope_label</c>, <c>epc_boc_area_href</c>,
/// <c>epc_boc_render_tenant_switcher_html</c>.
/// </summary>
public static class PhpPlanQ1Keep
{
    public const string IndustrySeoPath = "content/general_pages/epc_industry_seo.php";
    public const string BocScopePath = "content/general_pages/epc_boc_tenant_scope.php";

    private static readonly Regex NonSlug = new("[^a-z0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex HostSlugKeep = new("[^a-z0-9-]", RegexOptions.CultureInvariant);
    private static readonly Regex TemplateKeyKeep = new("[^a-z0-9_]", RegexOptions.CultureInvariant);
    private static readonly Regex SiteKeyKeep = new("[^a-z0-9_]", RegexOptions.CultureInvariant);
    private static readonly Regex ReservedPath = new("^(cp|erp|api|platform|documentation|sitemap\\.xml|robots\\.txt|epc-)", RegexOptions.CultureInvariant);
    private static readonly Regex IndustryHost = new("^([a-z0-9][a-z0-9_-]*)\\.ecomae\\.com$", RegexOptions.CultureInvariant);
    private static readonly Regex Quoted = new("'((?:\\\\'|[^'])*)'", RegexOptions.CultureInvariant);
    private static readonly string[] ReservedHosts = ["www", "cp", "api", "mail", "smtp", "ftp", "ns1", "ns2", "cdn", "admin", "www1", "asap"];

    public static string DocumentRoot { get; set; } = "";
    public static Dictionary<string, string> Server { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, string> Get { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, object?> Session { get; } = new(StringComparer.Ordinal);
    public static bool HeadersSent { get; set; } = true;
    public static bool SuperCpHost { get; set; }
    public static Func<IEnumerable<Dictionary<string, object?>>> LiveDefs { get; set; } = () => Array.Empty<Dictionary<string, object?>>();
    public static Func<IEnumerable<Dictionary<string, object?>>> IndustryGroups { get; set; } = () => Array.Empty<Dictionary<string, object?>>();
    public static Func<object?, IEnumerable<Dictionary<string, object?>>> TenantList { get; set; } = _ => Array.Empty<Dictionary<string, object?>>();
    public static object? GlobalPdo { get; set; }

    private static readonly Dictionary<string, List<string>> SubIndustryCache = new(StringComparer.Ordinal);
    private static bool _switchDone;
    private static bool _sessionBooted;

    public static void Reset()
    {
        DocumentRoot = "";
        Server.Clear();
        Get.Clear();
        Session.Clear();
        HeadersSent = true;
        SuperCpHost = false;
        LiveDefs = () => Array.Empty<Dictionary<string, object?>>();
        IndustryGroups = () => Array.Empty<Dictionary<string, object?>>();
        TenantList = _ => Array.Empty<Dictionary<string, object?>>();
        GlobalPdo = null;
        SubIndustryCache.Clear();
        _switchDone = false;
        _sessionBooted = false;
    }

    public static string EpcIndustrySeoSubSlug(string label)
    {
        var s = label.Trim().ToLowerInvariant();
        s = s.Replace("&", " ", StringComparison.Ordinal).Replace("+", " ", StringComparison.Ordinal);
        s = NonSlug.Replace(s, "-");
        return s.Trim('-');
    }

    public static Dictionary<string, object?> EpcIndustrySeoSubPresentation(string slug)
    {
        Dictionary<string, object?>[] variants =
        [
            new(StringComparer.Ordinal) { ["key"] = "atelier", ["label"] = "Atelier", ["tone"] = "warm" },
            new(StringComparer.Ordinal) { ["key"] = "ledger", ["label"] = "Ledger", ["tone"] = "ink" },
            new(StringComparer.Ordinal) { ["key"] = "mosaic", ["label"] = "Mosaic", ["tone"] = "bright" },
            new(StringComparer.Ordinal) { ["key"] = "dock", ["label"] = "Dock", ["tone"] = "cool" }
        ];
        var idx = (int)(BrochureProcessPhoto.PhpCrc32(slug.ToLowerInvariant()) % (uint)variants.Length);
        if (idx < 0)
        {
            idx = -idx;
        }

        return variants[idx];
    }

    public static List<string> EpcIndustrySeoTemplateSubCategories(string templateKey, string subLabel)
    {
        templateKey = TemplateKeyKeep.Replace(templateKey, "");
        subLabel = subLabel.Trim();
        if (templateKey == "" || subLabel == "")
        {
            return [];
        }

        var root = DocRoot();
        var file = root + "/content/general_pages/industry_templates/" + templateKey + ".php";
        if (!File.Exists(file))
        {
            return [];
        }

        var src = File.ReadAllText(file);
        var escaped = Regex.Escape(subLabel);
        var m = Regex.Match(
            src,
            "'" + escaped + "'\\s*=>\\s*array\\s*\\([\\s\\S]{0,2500}?'categories'\\s*=>\\s*array\\s*\\((.*?)\\)",
            RegexOptions.CultureInvariant);
        if (!m.Success)
        {
            return [];
        }

        var outList = new List<string>();
        foreach (Match label in Quoted.Matches(m.Groups[1].Value))
        {
            var text = StripCSlashes(label.Groups[1].Value).Trim();
            if (text != "")
            {
                outList.Add(text);
            }
        }

        return outList.Count == 0 ? LiveCategories(templateKey, subLabel) : outList;
    }

    public static Dictionary<string, string> EpcIndustrySeoHostMap()
        => new(StringComparer.Ordinal)
        {
            ["automotive"] = "automotive",
            ["healthcare"] = "healthcare",
            ["food_beverage"] = "food",
            ["fashion"] = "fashion",
            ["jewellery"] = "jewellery",
            ["electronics"] = "electronics",
            ["construction"] = "construction",
            ["manufacturing"] = "manufacturing",
            ["professional"] = "professional",
            ["education"] = "education",
            ["hospitality"] = "hospitality",
            ["beauty"] = "beauty",
            ["retail"] = "retail",
            ["agriculture"] = "agriculture",
            ["logistics"] = "logistics",
            ["energy"] = "energy",
            ["finance"] = "finance",
            ["it_software"] = "technology",
            ["media"] = "media",
            ["sports"] = "sports",
            ["home_living"] = "homeliving",
            ["wholesale"] = "wholesale",
            ["rental"] = "rental",
            ["nonprofit"] = "nonprofit",
            ["cleaning"] = "cleaning",
            ["pet"] = "pet",
            ["printing"] = "printing",
            ["security"] = "security"
        };

    public static string EpcIndustrySeoPrimaryHost(string group)
    {
        var aliasToPrimary = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["realestate"] = "construction",
            ["consulting"] = "professional",
            ["legal"] = "professional",
            ["environmental"] = "energy",
            ["telecom"] = "electronics",
            ["government"] = "nonprofit",
            ["aerospace"] = "manufacturing",
            ["mining"] = "manufacturing",
            ["food"] = "food",
            ["technology"] = "technology",
            ["homeliving"] = "homeliving"
        };
        var slug = aliasToPrimary.TryGetValue(group, out var aliased) ? aliased : group;
        var map = EpcIndustrySeoHostMap();
        if (map.TryGetValue(slug, out var mapped))
        {
            slug = mapped;
        }

        slug = HostSlugKeep.Replace(slug.ToLowerInvariant(), "");
        if (slug == "")
        {
            slug = "retail";
        }

        return slug + ".ecomae.com";
    }

    public static string EpcIndustrySeoSiteUrlForTemplate(string templateKey)
    {
        var map = EpcIndustrySeoHostMap();
        var slug = map.TryGetValue(templateKey, out var mapped) ? mapped : templateKey.Replace("_", "", StringComparison.Ordinal);
        slug = HostSlugKeep.Replace(slug.ToLowerInvariant(), "");
        if (slug == "")
        {
            slug = "retail";
        }

        return "https://" + slug + ".ecomae.com";
    }

    public static List<string> EpcIndustrySeoTemplateSubIndustries(string templateKey)
    {
        templateKey = TemplateKeyKeep.Replace(templateKey, "");
        if (templateKey == "")
        {
            return [];
        }

        if (SubIndustryCache.TryGetValue(templateKey, out var cached))
        {
            return cached;
        }

        var root = DocRoot();
        var file = root + "/content/general_pages/industry_templates/" + templateKey + ".php";
        var subs = new List<string>();
        if (File.Exists(file))
        {
            var src = File.ReadAllText(file);
            var m = Regex.Match(src, "'sub_industries'\\s*=>\\s*array\\s*\\((.*?)\\)\\s*,", RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (m.Success)
            {
                foreach (Match label in Quoted.Matches(m.Groups[1].Value))
                {
                    var text = StripCSlashes(label.Groups[1].Value).Trim();
                    if (text != "")
                    {
                        subs.Add(text);
                    }
                }
            }
        }

        foreach (var def in LiveDefs())
        {
            if (Str(def, "template_key") != templateKey || Str(def, "mode") != "inject")
            {
                continue;
            }

            var label = Str(def, "sub_label").Trim();
            if (label != "" && !subs.Contains(label, StringComparer.Ordinal))
            {
                subs.Insert(0, label);
            }
        }

        SubIndustryCache[templateKey] = subs;
        return subs;
    }

    public static List<object?[]> EpcIndustrySeoSitemapEntries()
    {
        var entries = new List<object?[]>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ginfo in IndustryGroups())
        {
            var tk = Str(ginfo, "template_key");
            if (tk == "")
            {
                continue;
            }

            var baseUrl = EpcIndustrySeoSiteUrlForTemplate(tk).TrimEnd('/');
            if (baseUrl == "" || seen.Contains(baseUrl))
            {
                continue;
            }

            seen.Add(baseUrl);
            entries.Add([baseUrl + "/", "0.85", "weekly"]);
            var subs = EpcIndustrySeoTemplateSubIndustries(tk);
            if (subs.Count == 0 && ginfo.TryGetValue("available_sub_areas", out var areas) && areas is IEnumerable<object?> list)
            {
                subs = list.Select(v => Convert.ToString(v) ?? "").ToList();
            }

            foreach (var label in subs)
            {
                var slug = EpcIndustrySeoSubSlug(label ?? "");
                if (slug == "")
                {
                    continue;
                }

                var url = baseUrl + "/" + slug;
                if (!seen.Add(url))
                {
                    continue;
                }

                entries.Add([url, "0.7", "monthly"]);
            }
        }

        return entries;
    }

    public static Dictionary<string, object?>? EpcIndustrySeoMatchRequestSub(IEnumerable<string> subIndustries)
    {
        var raw = Field(Server, "REQUEST_URI", "/");
        var path = ParseUrlPath(raw);
        if (path == "")
        {
            path = "/";
        }

        path = path.Trim('/').ToLowerInvariant();
        if (path == "" || path == "index.php")
        {
            return null;
        }

        if (ReservedPath.IsMatch(path))
        {
            return null;
        }

        var seg = path.Split('/', 2)[0];
        seg = HostSlugKeep.Replace(seg, "");
        if (seg == "")
        {
            return null;
        }

        var labels = subIndustries.ToList();
        foreach (var label in labels)
        {
            var canon = EpcIndustrySeoSubSlug(label);
            if (canon == seg)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = label, ["slug"] = canon };
            }
        }

        foreach (var label in labels)
        {
            var canon = EpcIndustrySeoSubSlug(label);
            var parts = canon.Split('-', StringSplitOptions.RemoveEmptyEntries).Where(p => p != "0");
            if (parts.Contains(seg, StringComparer.Ordinal) && Encoding.UTF8.GetByteCount(seg) >= 4)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = label, ["slug"] = canon };
            }
        }

        return null;
    }

    public static string EpcIndustrySeoRequestHost()
    {
        var host = Field(Server, "HTTP_HOST").Trim().ToLowerInvariant();
        if (host != "" && host.Contains(':', StringComparison.Ordinal))
        {
            host = host.Split(':', 2)[0];
        }

        return host;
    }

    public static bool EpcIndustrySeoIsIndustryHost(string? host = null)
    {
        host = (host ?? EpcIndustrySeoRequestHost()).Trim().ToLowerInvariant();
        if (host == "" || host == "www.ecomae.com" || host == "ecomae.com")
        {
            return false;
        }

        var m = IndustryHost.Match(host);
        return m.Success && !ReservedHosts.Contains(m.Groups[1].Value, StringComparer.Ordinal);
    }

    public static string EpcIndustrySeoRequestHostBase()
    {
        var host = EpcIndustrySeoRequestHost();
        if (!EpcIndustrySeoIsIndustryHost(host))
        {
            return "";
        }

        var https = Field(Server, "HTTPS");
        var secure = (!PhpEmpty(https) && https != "off")
            || Field(Server, "HTTP_X_FORWARDED_PROTO") == "https"
            || Field(Server, "SERVER_PORT") == "443";
        return (secure ? "https://" : "http://") + host;
    }

    public static List<string> EpcBocPlatformGroupIds()
        => ["command", "lifecycle", "reliability", "supply", "commerce", "finance", "growth", "identity", "platform", "knowledge"];

    public static List<string> EpcBocTenantGroupIds()
        => ["shop", "catalogue", "logistics", "erp", "professional"];

    public static List<string> EpcBocTenantAreaIds()
        => ["cp_marketing", "cp_seo", "erp_finance", "uae_tax", "insights_erp", "fulfillment_queue"];

    public static List<string> EpcBocTenantModePlatformStrip()
        => ["command", "lifecycle", "reliability"];

    public static void EpcBocSessionBoot()
    {
        if (!_sessionBooted && !HeadersSent)
        {
            _sessionBooted = true;
        }
    }

    public static Dictionary<string, object?>? EpcBocActiveTenant()
    {
        EpcBocSessionBoot();
        if (!Session.TryGetValue("epc_boc_active_tenant", out var raw) || raw is not Dictionary<string, object?> t)
        {
            return null;
        }

        var key = SiteKeyKeep.Replace(Str(t, "site_key").ToLowerInvariant(), "");
        var host = Str(t, "hostname").Trim().ToLowerInvariant();
        if (key == "" || host == "")
        {
            return null;
        }

        var label = Str(t, "label").Trim();
        if (label == "")
        {
            label = key;
        }

        var type = Str(t, "type", "commerce");
        var cpUrl = Str(t, "cp_url").Trim();
        if (cpUrl == "")
        {
            cpUrl = "https://www." + host + "/cp/";
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = key,
            ["hostname"] = host,
            ["label"] = label,
            ["type"] = type,
            ["cp_url"] = cpUrl.TrimEnd('/') + "/"
        };
    }

    public static void EpcBocSetActiveTenant(Dictionary<string, object?> tenant)
    {
        EpcBocSessionBoot();
        Session["epc_boc_active_tenant"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = SiteKeyKeep.Replace(Str(tenant, "site_key").ToLowerInvariant(), ""),
            ["hostname"] = Str(tenant, "hostname").Trim().ToLowerInvariant(),
            ["label"] = Str(tenant, "label").Trim(),
            ["type"] = Str(tenant, "type", "commerce"),
            ["cp_url"] = Str(tenant, "cp_url")
        };
    }

    public static void EpcBocClearActiveTenant()
    {
        EpcBocSessionBoot();
        Session.Remove("epc_boc_active_tenant");
    }

    public static string EpcBocTenantModuleUrl(Dictionary<string, object?> tenant, string path)
    {
        path = path.TrimStart('/');
        var type = Str(tenant, "type", "commerce");
        var key = Str(tenant, "site_key");
        var host = Str(tenant, "hostname");
        if (type == "demo" || key.StartsWith("demo_", StringComparison.Ordinal))
        {
            var fallback = "/cp/demo/" + key + "/";
            var raw = tenant.TryGetValue("cp_url", out var cp) && cp is not null ? Convert.ToString(cp) ?? "" : fallback;
            var baseUrl = raw.TrimEnd('/') + "/";
            if (!baseUrl.StartsWith("http", StringComparison.Ordinal))
            {
                baseUrl = "https://www.ecomae.com" + (baseUrl.StartsWith('/') ? baseUrl : "/" + baseUrl);
            }

            return baseUrl + path;
        }

        if (type == "erp_only")
        {
            return "https://www.ecomae.com/cp/client-erp/" + Uri.EscapeDataString(key) + "/" + path;
        }

        return host == "" ? "/cp/" + path : "https://www." + host + "/cp/" + path;
    }

    public static List<Dictionary<string, object?>> EpcBocSwitcherTenants(object? pdo = null)
    {
        pdo ??= GlobalPdo;
        if (pdo is null)
        {
            return [];
        }

        var outList = new List<Dictionary<string, object?>>();
        try
        {
            foreach (var row in TenantList(pdo))
            {
                var key = Str(row, "site_key");
                var host = Str(row, "hostname");
                if (key == "" || key == "ecomae" || host == "" || host == "ecomae.com" || host == "www.ecomae.com")
                {
                    continue;
                }

                if (Str(row, "type") == "platform")
                {
                    continue;
                }

                var type = Str(row, "type", "commerce");
                var label = Str(row, "trade_name").Trim();
                if (label == "")
                {
                    label = Str(row, "hub_name").Trim();
                }

                if (label == "")
                {
                    label = key;
                }

                var bare = Regex.Replace(host, "^www\\.", "", RegexOptions.CultureInvariant);
                var cpUrl = "https://www." + bare + "/cp/";
                if (type == "demo")
                {
                    cpUrl = "https://www.ecomae.com/cp/demo/" + Uri.EscapeDataString(key) + "/";
                }
                else if (type == "erp_only")
                {
                    cpUrl = "https://www.ecomae.com/cp/client-erp/" + Uri.EscapeDataString(key) + "/";
                }

                outList.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["site_key"] = key,
                    ["hostname"] = bare,
                    ["label"] = label,
                    ["type"] = type,
                    ["cp_url"] = cpUrl,
                    ["status"] = Str(row, "status")
                });
            }
        }
        catch
        {
            return outList;
        }

        return outList;
    }

    public static void EpcBocHandleTenantSwitch(object? pdo = null)
    {
        if (_switchDone)
        {
            return;
        }

        _switchDone = true;
        if (!SuperCpHost)
        {
            return;
        }

        if (!PhpEmpty(Field(Get, "epc_boc_exit_tenant")))
        {
            EpcBocClearActiveTenant();
            return;
        }

        var key = SiteKeyKeep.Replace(Field(Get, "epc_boc_tenant").Trim().ToLowerInvariant(), "");
        if (key == "")
        {
            return;
        }

        foreach (var t in EpcBocSwitcherTenants(pdo))
        {
            if (Str(t, "site_key") == key)
            {
                EpcBocSetActiveTenant(t);
                return;
            }
        }
    }

    public static Dictionary<string, object?> EpcBocNavApplyTenantScope(Dictionary<string, object?> nav, Dictionary<string, object?>? tenant = null)
    {
        tenant ??= EpcBocActiveTenant();
        var tenantGroupIds = EpcBocTenantGroupIds().ToHashSet(StringComparer.Ordinal);
        var tenantAreaIds = EpcBocTenantAreaIds().ToHashSet(StringComparer.Ordinal);
        var stripIds = EpcBocTenantModePlatformStrip().ToHashSet(StringComparer.Ordinal);
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (gidRaw, groupRaw) in nav)
        {
            var gid = gidRaw;
            if (groupRaw is not Dictionary<string, object?> gSrc)
            {
                continue;
            }

            var g = CopyGroup(gSrc);
            var areas = AreasOf(g);
            if (tenant is null)
            {
                if (tenantGroupIds.Contains(gid))
                {
                    continue;
                }

                foreach (var id in areas.Keys.ToList())
                {
                    if (tenantAreaIds.Contains(id))
                    {
                        areas.Remove(id);
                    }
                }

                if (areas.Count == 0)
                {
                    continue;
                }

                g["areas"] = areas;
                output[gid] = g;
                continue;
            }

            if (!tenantGroupIds.Contains(gid) && !stripIds.Contains(gid))
            {
                var keep = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var (id, areaRaw) in areas)
                {
                    if (areaRaw is not Dictionary<string, object?> areaSrc || !tenantAreaIds.Contains(id))
                    {
                        continue;
                    }

                    var area = CopyArea(areaSrc);
                    var path = Str(area, "path");
                    area["url_override"] = EpcBocTenantModuleUrl(tenant, path);
                    area["hint"] = (Str(area, "hint") + " · " + Str(tenant, "label")).Trim();
                    keep[id] = area;
                }

                if (keep.Count == 0)
                {
                    continue;
                }

                g["areas"] = keep;
                var grp = GroupMeta(g);
                grp["label"] = Str(grp, "label", gid) + " · " + Str(tenant, "label");
                g["group"] = grp;
                output[gid] = g;
                continue;
            }

            if (tenantGroupIds.Contains(gid))
            {
                foreach (var id in areas.Keys.ToList())
                {
                    if (areas[id] is not Dictionary<string, object?> areaSrc)
                    {
                        continue;
                    }

                    var area = CopyArea(areaSrc);
                    area["url_override"] = EpcBocTenantModuleUrl(tenant, Str(area, "path"));
                    area["hint"] = (Str(area, "hint") + " · " + Str(tenant, "label")).Trim();
                    areas[id] = area;
                }

                g["areas"] = areas;
                var grp = GroupMeta(g);
                grp["label"] = Str(grp, "label", gid) + " · " + Str(tenant, "label");
                g["group"] = grp;
            }

            output[gid] = g;
        }

        return output;
    }

    public static string EpcBocScopeLabel(Dictionary<string, object?>? tenant = null)
    {
        tenant ??= EpcBocActiveTenant();
        return tenant is null ? "Platform · All tenants" : "Tenant · " + Str(tenant, "label");
    }

    public static string EpcBocAreaHref(Dictionary<string, object?> area, string baseUrl)
    {
        if (!PhpEmpty(area.TryGetValue("url_override", out var over) ? over : null))
        {
            return Convert.ToString(over) ?? "";
        }

        return baseUrl.TrimEnd('/') + "/" + Str(area, "path").TrimStart('/');
    }

    public static string EpcBocRenderTenantSwitcherHtml(object? pdo = null)
    {
        if (!SuperCpHost)
        {
            return "";
        }

        EpcBocHandleTenantSwitch(pdo);
        var active = EpcBocActiveTenant();
        var tenants = EpcBocSwitcherTenants(pdo);
        if (tenants.Count == 0)
        {
            return "";
        }

        var reqPath = ParseUrlPath(Field(Server, "REQUEST_URI", "/cp/control"));
        var self = reqPath != "" ? reqPath : "/cp/control";
        var html = new StringBuilder();
        html.Append("<div class=\"epc-boc__tenant-switch\" data-epc-boc-tenant-switch=\"1\">");
        html.Append("<button type=\"button\" class=\"epc-boc__tenant-switch-btn\" data-boc-tenant-toggle=\"1\" aria-expanded=\"false\">");
        html.Append("<i class=\"fa fa-building\"></i> ");
        html.Append(active is null
            ? "<span class=\"epc-boc__tenant-switch-label\">Select tenant CP</span>"
            : "<span class=\"epc-boc__tenant-switch-label\">" + H(Str(active, "label")) + "</span>");
        html.Append(" <i class=\"fa fa-caret-down\"></i></button>");
        html.Append("<div class=\"epc-boc__tenant-switch-panel\" hidden>");
        html.Append("<div class=\"epc-boc__tenant-switch-head\">Operate as tenant CP</div>");
        html.Append("<a class=\"epc-boc__tenant-switch-item" + (active is not null ? "" : " is-active") + "\" href=\"" + H(self + "?epc_boc_exit_tenant=1") + "\"><i class=\"fa fa-globe\"></i> Platform fleet (ecomae)</a>");
        foreach (var t in tenants)
        {
            var isOn = active is not null && Str(active, "site_key") == Str(t, "site_key");
            var href = self + "?epc_boc_tenant=" + Uri.EscapeDataString(Str(t, "site_key"));
            html.Append("<a class=\"epc-boc__tenant-switch-item" + (isOn ? " is-active" : "") + "\" href=\"" + H(href) + "\">");
            html.Append("<i class=\"fa fa-external-link\"></i> " + H(Str(t, "label")));
            html.Append("<small>" + H(Str(t, "hostname")) + " · " + H(Str(t, "type")) + "</small>");
            html.Append("</a>");
            if (isOn)
            {
                html.Append("<a class=\"epc-boc__tenant-switch-open\" href=\"" + H(Str(t, "cp_url")) + "\" target=\"_blank\" rel=\"noopener\">Open full tenant CP ↗</a>");
            }
        }

        html.Append("</div></div>");
        return html.ToString();
    }

    private static List<string> LiveCategories(string templateKey, string subLabel)
    {
        foreach (var def in LiveDefs())
        {
            if (Str(def, "template_key") != templateKey || Str(def, "mode") != "inject" || Str(def, "sub_label") != subLabel)
            {
                continue;
            }

            if (def.TryGetValue("categories", out var cats) && cats is IEnumerable<object?> list && !PhpEmpty(cats))
            {
                return list.Select(v => Convert.ToString(v) ?? "").Where(s => s != "").ToList();
            }
        }

        return [];
    }

    private static Dictionary<string, object?> CopyGroup(Dictionary<string, object?> src)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in src)
        {
            copy[k] = v is Dictionary<string, object?> nested ? new Dictionary<string, object?>(nested, StringComparer.Ordinal) : v;
        }

        if (copy.TryGetValue("areas", out var areas) && areas is Dictionary<string, object?> areaMap)
        {
            var areasCopy = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (id, area) in areaMap)
            {
                areasCopy[id] = area is Dictionary<string, object?> a ? CopyArea(a) : area;
            }

            copy["areas"] = areasCopy;
        }

        return copy;
    }

    private static Dictionary<string, object?> CopyArea(Dictionary<string, object?> src)
        => new(src, StringComparer.Ordinal);

    private static Dictionary<string, object?> AreasOf(Dictionary<string, object?> group)
    {
        if (group.TryGetValue("areas", out var areas) && areas is Dictionary<string, object?> map)
        {
            return map;
        }

        var empty = new Dictionary<string, object?>(StringComparer.Ordinal);
        group["areas"] = empty;
        return empty;
    }

    private static Dictionary<string, object?> GroupMeta(Dictionary<string, object?> group)
    {
        if (group.TryGetValue("group", out var meta) && meta is Dictionary<string, object?> map)
        {
            return map;
        }

        var created = new Dictionary<string, object?>(StringComparer.Ordinal);
        group["group"] = created;
        return created;
    }

    private static string DocRoot()
    {
        var root = DocumentRoot;
        return root.TrimEnd('/', '\\');
    }

    private static string Field(Dictionary<string, string> bag, string key, string fallback = "")
        => bag.TryGetValue(key, out var value) ? value : fallback;

    private static string Str(Dictionary<string, object?> row, string key, string fallback = "")
        => row.TryGetValue(key, out var value) && value is not null ? Convert.ToString(value) ?? fallback : fallback;

    private static string H(string value)
        => (value ?? "")
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            bool b => !b,
            string s => s == "" || s == "0",
            int i => i == 0,
            long l => l == 0,
            double d => d == 0,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string ParseUrlPath(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return "";
        }

        if (uri.Contains("://", StringComparison.Ordinal))
        {
            return Uri.TryCreate(uri, UriKind.Absolute, out var abs) ? abs.AbsolutePath : uri;
        }

        var cut = uri.IndexOfAny(['?', '#']);
        return cut < 0 ? uri : uri[..cut];
    }

    private static string StripCSlashes(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                sb.Append(value[++i]);
                continue;
            }

            sb.Append(value[i]);
        }

        return sb.ToString();
    }
}
