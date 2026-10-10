using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-sail tenant industry-template catalogue. PHP identifiers kept for the inventory:
/// <c>epc_th_templates_guide_steps</c>, <c>epc_th_portal_codes_for_template</c>,
/// <c>epc_th_default_industry_for_template</c>, <c>epc_th_industry_templates_catalog</c>,
/// <c>epc_th_storefront_packages_catalog</c>, <c>epc_th_apply_industry_template</c>.
/// Path: <c>content/shop/tenant_hub/epc_tenant_templates_catalog.php</c>.
/// GET never mints a session cookie. Leftover portal / live-bridge / theme / finance-pack parents stay injected.
/// </summary>
public static class PhpPlanQ1Sail
{
    public const string TenantTemplatesCatalogPath = "content/shop/tenant_hub/epc_tenant_templates_catalog.php";

    public static Func<Dictionary<string, Dictionary<string, object?>>>? LiveDefs { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? Groups { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? Packages { get; set; }
    public static Func<string, string>? PackageForIndustry { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? PortalIndustries { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? ErpPacks { get; set; }
    public static Func<string, string>? SeoHost { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>, Dictionary<string, object?>>? ApplyTheme { get; set; }
    public static Action<string, string>? PersistPack { get; set; }

    public static void Reset()
    {
        LiveDefs = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        Groups = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        Packages = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        PackageForIndustry = _ => "";
        PortalIndustries = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        ErpPacks = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        SeoHost = tk => tk + ".ecomae.com";
        ApplyTheme = (_, _, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "Applied" };
        PersistPack = (_, _) => { };
    }

    public static List<Dictionary<string, string>> EpcThTemplatesGuideSteps()
        =>
        [
            Step("What is a template?", "Each industry on <a href=\"https://www.ecomae.com/platform/industries\" target=\"_blank\" rel=\"noopener\">/platform/industries</a> maps to a shared <strong>template key</strong> (e.g. <code>automotive</code>, <code>jewellery</code>). That key drives the public 3D hub (<code>{key}.ecomae.com</code>), the shared demo CP/ERP at <code>/cp/demo/{key}/</code>, and the default storefront chrome + ERP pack when you apply it to a client."),
            Step("Layers that get applied", "<ol style=\"margin:6px 0 0;padding-left:18px;\"><li><strong>Industry code</strong> — portal onboard code (auto_parts, jewellery, …)</li><li><strong>Theme + storefront package</strong> — colours, header/home, CP module packs</li><li><strong>ERP industry pack</strong> — costing, UOM, process flow (apply in client ERP → Setup after theme)</li><li><strong>Demo showcase</strong> — read-only shared demo; does not change client data until you Apply</li></ol>"),
            Step("Preview before apply", "Use <strong>Live hub</strong> for the marketing site, <strong>Demo CP</strong> for the control panel, and <strong>Demo ERP</strong> for the finance shell. Login for shared demos: <code>demo@ecomae.com</code> / <code>demo2026</code>."),
            Step("Apply to a tenant", "On this Templates tab: pick a client from the list → <strong>Apply to tenant</strong>. That writes industry + theme + storefront package into portal site settings and (when live) syncs CP packs to the client database. Then open the client ERP → Setup → Industry pack and apply the suggested pack if fields/COA need seeding."),
            Step("New clients", "Prefer <strong>Onboard client</strong> with the industry selected — that seeds the same template profile during registration. Use Templates when an existing tenant needs a restyle or industry switch."),
            Step("Where else to edit", "Per-site fine tuning: Super CP → <em>Industry settings</em>. ERP field blueprints: client ERP → Tenant config / Setup. Fleet module packs (BOS): <code>/bos/</code> Industry packs. This Templates tab is the single <em>show + apply + guide</em> surface.")
        ];

    public static List<string> EpcThPortalCodesForTemplate(string templateKey)
    {
        var tk = Sanitize(templateKey);
        var outList = new List<string>();
        foreach (var (code, def) in LiveDefs != null ? LiveDefs() : [])
        {
            if (Str(def, "template_key") == tk)
            {
                outList.Add(code);
            }
        }

        return outList;
    }

    public static string EpcThDefaultIndustryForTemplate(string templateKey, Dictionary<string, object?>? group = null)
    {
        _ = group;
        var codes = EpcThPortalCodesForTemplate(templateKey);
        var defs = LiveDefs != null ? LiveDefs() : new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var code in codes)
        {
            if (defs.TryGetValue(code, out var def) && Str(def, "mode") == "hub_root")
            {
                return code;
            }
        }

        if (codes.Count > 0)
        {
            return codes[0];
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["automotive"] = "auto_parts",
            ["electronics"] = "electronics",
            ["fashion"] = "fashion",
            ["jewellery"] = "jewellery",
            ["food_beverage"] = "food_beverage",
            ["healthcare"] = "medical",
            ["professional"] = "consultancy",
            ["retail"] = "grocery_retail",
            ["hospitality"] = "hospitality_travel",
            ["education"] = "education_training",
            ["logistics"] = "logistics_freight",
            ["finance"] = "financial_services",
            ["energy"] = "energy_utilities",
            ["manufacturing"] = "manufacturing_industrial",
            ["construction"] = "construction_contracting",
            ["rental"] = "rental"
        };
        var tk = Sanitize(templateKey);
        return map.TryGetValue(tk, out var mapped) ? mapped : "auto_parts";
    }

    public static List<Dictionary<string, object?>> EpcThIndustryTemplatesCatalog()
    {
        var groups = Groups != null ? Groups() : new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var packages = Packages != null ? Packages() : new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var portalIndustries = PortalIndustries != null ? PortalIndustries() : new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var erpPacks = ErpPacks != null ? ErpPacks() : new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var output = new List<Dictionary<string, object?>>();
        foreach (var (gid, g) in groups)
        {
            var tk = Str(g, "template_key");
            if (tk == "")
            {
                tk = gid;
            }

            var industryCode = EpcThDefaultIndustryForTemplate(tk, g);
            var portalCodes = EpcThPortalCodesForTemplate(tk);
            var pkgId = PackageForIndustry != null ? PackageForIndustry(industryCode) : "";
            Dictionary<string, object?>? pkg = pkgId != "" && packages.TryGetValue(pkgId, out var found) ? found : null;
            var erpBase = Str(g, "erp_base");
            var erpLabel = erpBase != "" && erpPacks.TryGetValue(erpBase, out var pack)
                ? (Str(pack, "label") != "" ? Str(pack, "label") : erpBase)
                : erpBase;
            var host = SeoHost != null ? SeoHost(tk) : tk + ".ecomae.com";
            var liveUrl = "https://" + host + "/";
            var demoCp = "https://www.ecomae.com/cp/demo/" + Uri.EscapeDataString(tk) + "/";
            var colors = g.TryGetValue("color_scheme", out var cs) && cs is Dictionary<string, object?> cdict ? cdict : new Dictionary<string, object?>(StringComparer.Ordinal);
            var portalNames = new List<string>();
            foreach (var code in portalCodes)
            {
                if (portalIndustries.TryGetValue(code, out var pi) && Str(pi, "name") != "")
                {
                    portalNames.Add(Str(pi, "name"));
                }
                else if (portalIndustries.TryGetValue(code, out pi) && Str(pi, "label") != "")
                {
                    portalNames.Add(Str(pi, "label"));
                }
                else
                {
                    portalNames.Add(code);
                }
            }

            output.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = gid,
                ["template_key"] = tk,
                ["label"] = Str(g, "label") != "" ? Str(g, "label") : tk,
                ["description"] = Str(g, "description"),
                ["icon"] = Str(g, "icon") != "" ? Str(g, "icon") : "fa-industry",
                ["primary"] = Str(colors, "primary") != "" ? Str(colors, "primary") : "#0f766e",
                ["accent"] = Str(colors, "accent") != "" ? Str(colors, "accent") : "#14b8a6",
                ["industry_code"] = industryCode,
                ["portal_codes"] = portalCodes,
                ["portal_names"] = portalNames,
                ["storefront_package"] = pkgId,
                ["storefront_label"] = pkg != null ? (Str(pkg, "label") != "" ? Str(pkg, "label") : pkgId) : "",
                ["theme_template"] = pkg != null ? (Str(pkg, "theme_template") != "" ? Str(pkg, "theme_template") : "classic") : "classic",
                ["erp_pack"] = erpBase,
                ["erp_pack_label"] = erpLabel,
                ["sub_areas"] = g.TryGetValue("available_sub_areas", out var sa) && sa is System.Collections.IEnumerable ? sa : Array.Empty<string>(),
                ["live_url"] = liveUrl,
                ["demo_cp_url"] = demoCp,
                ["demo_erp_url"] = demoCp + "shop/finance/erp?epc_erp_shell=1",
                ["platform_url"] = "https://www.ecomae.com/platform/industry/" + Uri.EscapeDataString(industryCode.Replace('_', '-')),
                ["has_storefront_package"] = pkgId != ""
            });
        }

        output.Sort((a, b) => string.Compare(Convert.ToString(a["label"]), Convert.ToString(b["label"]), StringComparison.OrdinalIgnoreCase));
        return output;
    }

    public static List<Dictionary<string, object?>> EpcThStorefrontPackagesCatalog()
    {
        var outList = new List<Dictionary<string, object?>>();
        foreach (var (id, pkg) in Packages != null ? Packages() : [])
        {
            var codes = pkg.TryGetValue("industry_codes", out var ic) && ic is IList<string> list && list.Count > 0 ? list[0] : "";
            if (codes == "" && ic is IList<object?> obj && obj.Count > 0)
            {
                codes = Convert.ToString(obj[0]) ?? "";
            }

            outList.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = id,
                ["label"] = Str(pkg, "label") != "" ? Str(pkg, "label") : id,
                ["desc"] = Str(pkg, "desc"),
                ["industry_code"] = codes,
                ["theme_template"] = Str(pkg, "theme_template") != "" ? Str(pkg, "theme_template") : "classic",
                ["implemented"] = !pkg.TryGetValue("implemented", out var impl) || !Equals(impl, false)
            });
        }

        return outList;
    }

    public static Dictionary<string, object?> EpcThApplyIndustryTemplate(MySqlConnection db, string siteKey, string templateKey, Dictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var tk = Sanitize(templateKey);
        if (tk == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Invalid template key" };
        }

        var catalog = EpcThIndustryTemplatesCatalog();
        Dictionary<string, object?>? card = null;
        foreach (var row in catalog)
        {
            if (Convert.ToString(row["template_key"]) == tk || Convert.ToString(row["id"]) == tk)
            {
                card = row;
                break;
            }
        }

        if (card == null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Template not found in catalogue" };
        }

        var industryCode = Sanitize(opts.TryGetValue("industry_code", out var ic) ? Convert.ToString(ic) ?? "" : Convert.ToString(card["industry_code"]) ?? "");
        if (industryCode == "")
        {
            industryCode = Convert.ToString(card["industry_code"]) ?? "";
        }

        var applyOpts = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["industry_code"] = industryCode,
            ["push_client"] = !opts.TryGetValue("push_client", out var pc) || !PhpEmpty(pc)
        };
        var pkg = opts.TryGetValue("storefront_package", out var sp) && !PhpEmpty(sp) ? Convert.ToString(sp) : Convert.ToString(card["storefront_package"]);
        if (!string.IsNullOrEmpty(pkg))
        {
            applyOpts["storefront_package"] = pkg;
        }

        var theme = opts.TryGetValue("theme_template", out var tt) && !PhpEmpty(tt) ? Convert.ToString(tt) : Convert.ToString(card["theme_template"]);
        if (!string.IsNullOrEmpty(theme))
        {
            applyOpts["theme_template"] = theme;
        }

        var result = ApplyTheme != null
            ? ApplyTheme(db, siteKey, applyOpts)
            : new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "" };
        if (PhpEmpty(result.TryGetValue("ok", out var ok) ? ok : null))
        {
            return result;
        }

        var erpPack = Convert.ToString(card["erp_pack"]) ?? "";
        if (erpPack != "")
        {
            PersistPack?.Invoke(siteKey, erpPack);
        }

        var extra = "";
        var erpLabel = Convert.ToString(card["erp_pack_label"]) ?? "";
        if (erpLabel != "")
        {
            extra = " · Next: client ERP → Setup → apply pack “" + erpLabel + "”";
        }

        result["message"] = (Convert.ToString(result.TryGetValue("message", out var m) ? m : "Applied") ?? "Applied") + extra;
        result["template_key"] = card["template_key"];
        result["erp_pack"] = card["erp_pack"];
        return result;
    }

    private static Dictionary<string, string> Step(string title, string body)
        => new(StringComparer.Ordinal) { ["title"] = title, ["body"] = body };

    private static string Sanitize(string value)
        => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9_]", "");

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? Convert.ToString(v) ?? "" : "";

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d
            || (value is string s && (s == "" || s == "0"));
}
