using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-site helpers. PHP identifiers kept for the inventory:
/// <c>epc_site_context_cache_key</c>, <c>epc_site_context_reset</c>,
/// <c>epc_site_context</c>, <c>epc_site_domain</c>, <c>epc_site_url</c>,
/// <c>epc_site_host</c>, <c>epc_site_trade_name</c>, <c>epc_site_from_email</c>,
/// <c>epc_site_admin_email</c>, <c>epc_site_contact_phone</c>,
/// <c>epc_site_apply_contact_overrides</c>, <c>epc_site_apply_config</c>,
/// <c>epc_site_document_company_defaults</c>,
/// <c>epc_supplier_h</c>, <c>epc_order_item_storage_id</c>,
/// <c>epc_build_supplier_lpo_html</c>, <c>epc_storage_supplier_order_email</c>,
/// <c>epc_send_supplier_lpo_notifications</c>,
/// <c>epc_cp_acl_content_url</c>, <c>epc_cp_acl_preload</c>,
/// <c>epc_cp_acl_expand_groups</c>, <c>is_anable</c>.
/// </summary>
public static class PhpPlanQ1Site
{
    public const string SiteContextPath = "content/general_pages/epc_site_context.php";
    public const string SupplierNotificationsPath = "content/shop/usefull/epc_supplier_notifications.php";
    public const string ControlHelperPath = "cp/content/control/control_helper.php";

    private static readonly Regex DemoKeyKeep = new(@"[^a-z0-9_]", RegexOptions.CultureInvariant);
    private static readonly Regex WwwPrefix = new(@"^www\.", RegexOptions.CultureInvariant);

    public static string HttpHost { get; set; } = "";
    public static string ServerName { get; set; } = "";
    public static Dictionary<string, object?> PortalProfile { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, object?> PortalIndustry { get; set; } = new(StringComparer.Ordinal);
    public static string? PortalGuess { get; set; }
    public static bool PortalAuto { get; set; }
    public static string PortalHome { get; set; } = "auto_parts";
    public static string? DemoStorefrontSiteKey { get; set; }
    public static string? DemoCpSiteKey { get; set; }
    public static bool DemoStorefrontContext { get; set; }
    public static bool CacheBust { get; set; }
    public static string BackendDir { get; set; } = "cp";

    private static readonly Dictionary<string, Dictionary<string, object?>> CtxCache = new(StringComparer.Ordinal);
    public static Dictionary<string, int> ContentIdByUrl { get; } = new(StringComparer.Ordinal);
    public static Dictionary<int, List<int>> GroupsByContent { get; } = new();
    public static Dictionary<string, List<int>> ExpandedGroups { get; } = new(StringComparer.Ordinal);

    public static string EpcSiteContextCacheKey()
    {
        var key = PortalHost();
        if (!string.IsNullOrEmpty(DemoStorefrontSiteKey))
        {
            key += ":demo:" + DemoKeyKeep.Replace(DemoStorefrontSiteKey, "");
        }
        else if (!string.IsNullOrEmpty(DemoCpSiteKey))
        {
            key += ":demo-cp:" + DemoKeyKeep.Replace(DemoCpSiteKey, "");
        }

        return key;
    }

    public static void EpcSiteContextReset() => CacheBust = true;

    public static Dictionary<string, object?> EpcSiteContext(SiteConfig? config = null)
    {
        if (CacheBust)
        {
            CtxCache.Clear();
            CacheBust = false;
        }

        var cacheKey = EpcSiteContextCacheKey();
        if (CtxCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var profile = PortalProfile;
        var host = PortalHost();
        var industry = PortalIndustry;
        var contact = DefaultContact(profile);
        if (profile.TryGetValue("contact", out var contactObj) && contactObj is IReadOnlyDictionary<string, object?> extra)
        {
            foreach (var kv in extra)
            {
                contact[kv.Key] = kv.Value is null ? "" : Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? "";
            }
        }

        var domain = "";
        var profileDomain = Str(profile, "domain_path");
        if (profileDomain.Length > 0)
        {
            domain = profileDomain.TrimEnd('/');
        }
        else if (config is not null && !string.IsNullOrEmpty(config.DomainPath))
        {
            domain = config.DomainPath.TrimEnd('/');
        }

        if (domain.Length == 0 || domain.Contains("localhost", StringComparison.Ordinal))
        {
            var guessed = PortalGuess ?? "";
            if (guessed.Length > 0)
            {
                domain = guessed.TrimEnd('/');
            }
        }

        if (config is not null)
        {
            if (Str(contact, "from_email").Length == 0 && !string.IsNullOrEmpty(config.FromEmail))
            {
                contact["from_email"] = config.FromEmail;
            }

            if (Str(contact, "from_name").Length == 0 && !string.IsNullOrEmpty(config.FromName))
            {
                contact["from_name"] = config.FromName;
            }

            if (Str(contact, "contact_phone").Length == 0 && !string.IsNullOrEmpty(config.EpcContactPhone))
            {
                contact["contact_phone"] = config.EpcContactPhone;
            }

            if (Str(contact, "whatsapp_number").Length == 0 && !string.IsNullOrEmpty(config.EpcWhatsappNumber))
            {
                contact["whatsapp_number"] = config.EpcWhatsappNumber;
            }

            if (Str(contact, "head_office_email").Length == 0 && !string.IsNullOrEmpty(config.EpcHeadOfficeEmail))
            {
                contact["head_office_email"] = config.EpcHeadOfficeEmail;
            }

            if (Str(contact, "head_office_address").Length == 0 && !string.IsNullOrEmpty(config.EpcHeadOfficeAddress))
            {
                contact["head_office_address"] = config.EpcHeadOfficeAddress;
            }
        }

        if (Str(contact, "admin_email").Length == 0)
        {
            contact["admin_email"] = Str(contact, "from_email");
        }

        if (Str(contact, "head_office_email").Length == 0)
        {
            contact["head_office_email"] = Str(contact, "from_email");
        }

        if (DemoStorefrontContext && !string.IsNullOrEmpty(DemoStorefrontSiteKey))
        {
            domain = "https://www.ecomae.com/demo/" + DemoKeyKeep.Replace(DemoStorefrontSiteKey, "");
        }

        var ctx = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["host"] = host,
            ["domain_path"] = domain.Length > 0 ? domain + "/" : "",
            ["domain"] = domain,
            ["industry_code"] = industry.TryGetValue("code", out var code) && code is not null ? Convert.ToString(code, CultureInfo.InvariantCulture) ?? "auto_parts" : "auto_parts",
            ["industry_name"] = industry.TryGetValue("name", out var iname) && iname is not null ? Convert.ToString(iname, CultureInfo.InvariantCulture) ?? "Commerce" : "Commerce",
            ["system_name"] = BrandSystemName(),
            ["hub_name"] = BrandHubName(),
            ["tagline"] = Str(profile, "tagline"),
            ["trade_name"] = Str(contact, "trade_name"),
            ["contact"] = contact,
            ["from_name"] = Str(contact, "from_name"),
            ["from_email"] = Str(contact, "from_email"),
            ["admin_email"] = Str(contact, "admin_email"),
            ["contact_phone"] = Str(contact, "contact_phone"),
            ["whatsapp_number"] = Str(contact, "whatsapp_number"),
            ["head_office_address"] = Str(contact, "head_office_address"),
            ["head_office_email"] = Str(contact, "head_office_email"),
            ["city"] = Str(contact, "city"),
            ["country"] = Str(contact, "country"),
            ["is_auto_parts"] = PortalAuto,
            ["home_mode"] = PortalHome
        };
        CtxCache[cacheKey] = ctx;
        return ctx;
    }

    public static string EpcSiteDomain() => Convert.ToString(EpcSiteContext()["domain"], CultureInfo.InvariantCulture) ?? "";

    public static string EpcSiteUrl(string path = "")
    {
        var baseUrl = EpcSiteDomain();
        if (baseUrl.Length == 0)
        {
            return path ?? "";
        }

        path = (path ?? "").TrimStart('/');
        return path.Length == 0 ? baseUrl : baseUrl + "/" + path;
    }

    public static string EpcSiteHost() => Convert.ToString(EpcSiteContext()["host"], CultureInfo.InvariantCulture) ?? "";

    public static string EpcSiteTradeName() => Convert.ToString(EpcSiteContext()["trade_name"], CultureInfo.InvariantCulture) ?? "";

    public static string EpcSiteFromEmail() => Convert.ToString(EpcSiteContext()["from_email"], CultureInfo.InvariantCulture) ?? "";

    public static string EpcSiteAdminEmail() => Convert.ToString(EpcSiteContext()["admin_email"], CultureInfo.InvariantCulture) ?? "";

    public static string EpcSiteContactPhone() => Convert.ToString(EpcSiteContext()["contact_phone"], CultureInfo.InvariantCulture) ?? "";

    public static void EpcSiteApplyContactOverrides(SiteConfig? config)
    {
        if (config is null)
        {
            return;
        }

        var ctx = EpcSiteContext(config);
        if (!string.IsNullOrEmpty(Convert.ToString(ctx["domain_path"], CultureInfo.InvariantCulture)))
        {
            config.DomainPath = Convert.ToString(ctx["domain_path"], CultureInfo.InvariantCulture) ?? "";
        }

        if (!string.IsNullOrEmpty(Convert.ToString(ctx["from_name"], CultureInfo.InvariantCulture)))
        {
            config.FromName = Convert.ToString(ctx["from_name"], CultureInfo.InvariantCulture) ?? "";
        }

        if (!string.IsNullOrEmpty(Convert.ToString(ctx["from_email"], CultureInfo.InvariantCulture)))
        {
            config.FromEmail = Convert.ToString(ctx["from_email"], CultureInfo.InvariantCulture) ?? "";
        }

        if (!string.IsNullOrEmpty(Convert.ToString(ctx["contact_phone"], CultureInfo.InvariantCulture)))
        {
            config.EpcContactPhone = Convert.ToString(ctx["contact_phone"], CultureInfo.InvariantCulture) ?? "";
        }

        if (!string.IsNullOrEmpty(Convert.ToString(ctx["whatsapp_number"], CultureInfo.InvariantCulture)))
        {
            config.EpcWhatsappNumber = Convert.ToString(ctx["whatsapp_number"], CultureInfo.InvariantCulture) ?? "";
        }

        if (!string.IsNullOrEmpty(Convert.ToString(ctx["head_office_address"], CultureInfo.InvariantCulture)))
        {
            config.EpcHeadOfficeAddress = Convert.ToString(ctx["head_office_address"], CultureInfo.InvariantCulture) ?? "";
        }

        if (!string.IsNullOrEmpty(Convert.ToString(ctx["head_office_email"], CultureInfo.InvariantCulture)))
        {
            config.EpcHeadOfficeEmail = Convert.ToString(ctx["head_office_email"], CultureInfo.InvariantCulture) ?? "";
        }
    }

    public static void EpcSiteApplyConfig(SiteConfig? config) => EpcSiteApplyContactOverrides(config);

    public static Dictionary<string, string> EpcSiteDocumentCompanyDefaults()
    {
        var ctx = EpcSiteContext();
        var addr = (Convert.ToString(ctx["head_office_address"], CultureInfo.InvariantCulture) ?? "").Trim();
        var city = Convert.ToString(ctx["city"], CultureInfo.InvariantCulture) ?? "";
        var country = Convert.ToString(ctx["country"], CultureInfo.InvariantCulture) ?? "";
        if (addr.Length == 0 && (city.Length > 0 || country.Length > 0))
        {
            addr = (city + ", " + country).Trim().Trim(',');
            addr = addr.Trim();
        }

        var hub = Convert.ToString(ctx["hub_name"], CultureInfo.InvariantCulture) ?? "";
        var trade = Convert.ToString(ctx["trade_name"], CultureInfo.InvariantCulture) ?? "";
        var headEmail = Convert.ToString(ctx["head_office_email"], CultureInfo.InvariantCulture) ?? "";
        var fromEmail = Convert.ToString(ctx["from_email"], CultureInfo.InvariantCulture) ?? "";
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["legal_name"] = hub.Length > 0 ? hub : trade,
            ["trade_name"] = trade,
            ["address_line1"] = addr.Length > 0 ? addr : city,
            ["city"] = city.Length > 0 ? city : "Dubai",
            ["country"] = country,
            ["phone"] = Convert.ToString(ctx["contact_phone"], CultureInfo.InvariantCulture) ?? "",
            ["email"] = headEmail.Length > 0 ? headEmail : fromEmail,
            ["website"] = EpcSiteUrl()
        };
    }

    public static string EpcSupplierH(object? value)
        => PhpPlanQ1Plus.EpcChannelH(value);

    public static int EpcOrderItemStorageId(SupplierStore? db, IReadOnlyDictionary<string, object?> item)
    {
        var storageId = IntVal(item, "t2_storage_id");
        if (storageId > 0)
        {
            return storageId;
        }

        if (db is null)
        {
            return 0;
        }

        var itemId = IntVal(item, "id");
        var row = db.Details.Where(d => d.OrderItemId == itemId).OrderBy(d => d.Id).FirstOrDefault();
        return row?.StorageId ?? 0;
    }

    public static string EpcBuildSupplierLpoHtml(SiteConfig? config, int orderId, int storageId, string storageName, IReadOnlyList<IReadOnlyDictionary<string, object?>> items)
    {
        var domain = config is not null ? (config.DomainPath ?? "").TrimEnd('/') : "";
        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Calibri,Arial,sans-serif;font-size:14px;color:#111;\">");
        html.Append("<p style=\"margin:0 0 12px;\">Dear supplier,</p>");
        html.Append("<p style=\"margin:0 0 12px;\">Please supply the following parts for our customer order. Use <strong>LPO / PO number <span style=\"color:#b45309;\">")
            .Append(orderId).Append("</span></strong> on your invoice and delivery note (this is our customer order number).</p>");
        html.Append("<table style=\"border-collapse:collapse;margin:0 0 16px;font-size:14px;\">");
        html.Append("<tr><td style=\"padding:4px 16px 4px 0;font-weight:bold;\">LPO number</td><td>").Append(orderId).Append("</td></tr>");
        html.Append("<tr><td style=\"padding:4px 16px 4px 0;font-weight:bold;\">Warehouse</td><td>").Append(EpcSupplierH(storageName)).Append("</td></tr>");
        html.Append("</table>");
        html.Append("<table style=\"border-collapse:collapse;width:100%;max-width:720px;font-size:13px;\" border=\"1\" cellpadding=\"6\" cellspacing=\"0\">");
        html.Append("<thead><tr style=\"background:#f1f5f9;\"><th align=\"left\">Brand</th><th align=\"left\">Part no.</th><th align=\"left\">Description</th><th align=\"right\">Qty</th></tr></thead><tbody>");
        foreach (var item in items)
        {
            var brand = Str(item, "t2_manufacturer");
            var article = Str(item, "t2_article_show");
            if (article.Length == 0)
            {
                article = Str(item, "t2_article");
            }

            var name = Str(item, "t2_name");
            var qty = IntVal(item, "count_need");
            if (qty <= 0)
            {
                continue;
            }

            html.Append("<tr><td>").Append(EpcSupplierH(brand)).Append("</td><td>").Append(EpcSupplierH(article)).Append("</td><td>").Append(EpcSupplierH(name)).Append("</td><td align=\"right\">").Append(qty).Append("</td></tr>");
        }

        html.Append("</tbody></table>");
        html.Append("<p style=\"margin:16px 0 0;font-size:13px;color:#475569;\">Reply to this e-mail if any line is unavailable. Reference LPO <strong>").Append(orderId).Append("</strong> on all correspondence.</p>");
        if (domain.Length > 0)
        {
            html.Append("<p style=\"margin:8px 0 0;font-size:12px;color:#64748b;\">").Append(EpcSupplierH(domain)).Append("</p>");
        }

        html.Append("</div>");
        return html.ToString();
    }

    public static string EpcStorageSupplierOrderEmail(SupplierStore db, int storageId)
    {
        if (storageId <= 0)
        {
            return "";
        }

        var row = db.Storages.Find(s => s.Id == storageId);
        if (row is null)
        {
            return "";
        }

        return row.OrderEmail ?? "";
    }

    public static void EpcSendSupplierLpoNotifications(SupplierStore db, int orderId)
    {
        if (orderId <= 0)
        {
            return;
        }

        db.Sent.Add(orderId);
    }

    public static string EpcCpAclContentUrl(IReadOnlyDictionary<string, object?> item)
    {
        var url = Str(item, "url");
        var q = url.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            url = url[..q];
        }

        var backend = (BackendDir ?? "cp").Trim('/');
        url = url.Replace("/" + backend + "/", "", StringComparison.Ordinal);
        return url.TrimStart('/');
    }

    public static void EpcCpAclPreload(AclStore? db, IReadOnlyList<IReadOnlyDictionary<string, object?>> items)
    {
        if (db is null || items.Count == 0)
        {
            return;
        }

        var urls = new List<string>();
        foreach (var item in items)
        {
            var url = EpcCpAclContentUrl(item);
            if (url.Length == 0 || ContentIdByUrl.ContainsKey(url))
            {
                continue;
            }

            urls.Add(url);
        }

        foreach (var chunk in urls.Chunk(200))
        {
            var contentIds = new List<int>();
            foreach (var url in chunk)
            {
                var row = db.Content.Find(c => c.Url == url);
                if (row is not null)
                {
                    ContentIdByUrl[url] = row.Id;
                    contentIds.Add(row.Id);
                    if (!GroupsByContent.ContainsKey(row.Id))
                    {
                        GroupsByContent[row.Id] = new List<int>();
                    }
                }
            }

            foreach (var url in chunk)
            {
                if (!ContentIdByUrl.ContainsKey(url))
                {
                    ContentIdByUrl[url] = 0;
                }
            }

            foreach (var id in contentIds)
            {
                GroupsByContent[id] = db.Access.Where(a => a.ContentId == id).Select(a => a.GroupId).ToList();
            }
        }
    }

    public static List<int> EpcCpAclExpandGroups(IReadOnlyList<object?> explicitGroups)
    {
        var values = explicitGroups.Select(v => Convert.ToInt32(ToFloat(v), CultureInfo.InvariantCulture)).Distinct().ToList();
        values.Sort();
        var cacheKey = string.Join(",", values);
        if (ExpandedGroups.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        ExpandedGroups[cacheKey] = values;
        return values;
    }

    public static bool IsAnable(IReadOnlyDictionary<string, object?>? item)
    {
        if (item is null || item.Count == 0)
        {
            return false;
        }

        var url = EpcCpAclContentUrl(item);
        return url.Length > 0 && ContentIdByUrl.TryGetValue(url, out var id) && id > 0;
    }

    public static void ResetAcl()
    {
        ContentIdByUrl.Clear();
        GroupsByContent.Clear();
        ExpandedGroups.Clear();
    }

    public static void ResetSite()
    {
        CtxCache.Clear();
        CacheBust = false;
        HttpHost = "";
        ServerName = "";
        PortalProfile = new Dictionary<string, object?>(StringComparer.Ordinal);
        PortalIndustry = new Dictionary<string, object?>(StringComparer.Ordinal);
        PortalGuess = null;
        PortalAuto = false;
        PortalHome = "auto_parts";
        DemoStorefrontSiteKey = null;
        DemoCpSiteKey = null;
        DemoStorefrontContext = false;
        BackendDir = "cp";
    }

    private static string PortalHost()
    {
        var host = (HttpHost ?? "").ToLowerInvariant();
        var colon = host.IndexOf(':');
        if (colon >= 0)
        {
            host = host[..colon];
        }

        if (host.Length == 0)
        {
            host = (ServerName ?? "").ToLowerInvariant();
        }

        return host;
    }

    private static Dictionary<string, object?> DefaultContact(IReadOnlyDictionary<string, object?> profile)
    {
        var host = PortalHost();
        var trade = Str(profile, "trade_name");
        if (trade.Length == 0)
        {
            trade = Str(profile, "hub_name");
        }

        if (trade.Length == 0 && host.Length > 0)
        {
            trade = WwwPrefix.Replace(host, "");
            trade = Ucfirst(trade.Replace(".com", "", StringComparison.Ordinal).Replace(".", " ", StringComparison.Ordinal));
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["trade_name"] = trade,
            ["from_name"] = trade,
            ["from_email"] = Str(profile, "from_email"),
            ["admin_email"] = Str(profile, "admin_email"),
            ["contact_phone"] = Str(profile, "contact_phone"),
            ["whatsapp_number"] = Str(profile, "whatsapp_number"),
            ["head_office_title"] = "Head Office",
            ["head_office_address"] = Str(profile, "head_office_address"),
            ["head_office_email"] = Str(profile, "head_office_email"),
            ["city"] = Str(profile, "city"),
            ["country"] = profile.ContainsKey("country") ? Str(profile, "country") : "United Arab Emirates"
        };
    }

    private static string BrandSystemName()
    {
        var name = Str(PortalProfile, "system_name");
        return name.Length > 0 ? name : "ECOM AE portal";
    }

    private static string BrandHubName()
    {
        var name = Str(PortalProfile, "hub_name");
        return name.Length > 0 ? name : "ecomae";
    }

    private static string Ucfirst(string value)
        => value.Length == 0 ? "" : char.ToUpperInvariant(value[0]) + value[1..];

    private static string Str(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            : "";

    private static int IntVal(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var value) ? Convert.ToInt32(ToFloat(value), CultureInfo.InvariantCulture) : 0;

    private static double ToFloat(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        if (value is double d)
        {
            return d;
        }

        if (value is int i)
        {
            return i;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    public sealed class SiteConfig
    {
        public string? DomainPath { get; set; }
        public string? FromName { get; set; }
        public string? FromEmail { get; set; }
        public string? EpcContactPhone { get; set; }
        public string? EpcWhatsappNumber { get; set; }
        public string? EpcHeadOfficeEmail { get; set; }
        public string? EpcHeadOfficeAddress { get; set; }
    }

    public sealed class SupplierStore
    {
        public List<SupplierDetailRow> Details { get; } = new();
        public List<SupplierStorageRow> Storages { get; } = new();
        public List<int> Sent { get; } = new();
    }

    public sealed class SupplierDetailRow
    {
        public int Id { get; set; }
        public int OrderItemId { get; set; }
        public int StorageId { get; set; }
    }

    public sealed class SupplierStorageRow
    {
        public int Id { get; set; }
        public string? OrderEmail { get; set; }
    }

    public sealed class AclStore
    {
        public List<AclContentRow> Content { get; } = new();
        public List<AclAccessRow> Access { get; } = new();
    }

    public sealed class AclContentRow
    {
        public int Id { get; set; }
        public string Url { get; set; } = "";
    }

    public sealed class AclAccessRow
    {
        public int ContentId { get; set; }
        public int GroupId { get; set; }
    }
}
