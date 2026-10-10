using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-jib tenant showcase. PHP identifiers kept for the inventory:
/// <c>epc_ecomae_platform_tenant_showcase_themes</c>,
/// <c>epc_ecomae_platform_tenant_showcase_theme</c>,
/// <c>epc_ecomae_platform_tenant_showcase_rows</c>,
/// <c>epc_ecomae_platform_tenant_showcase_styles</c>,
/// <c>epc_ecomae_platform_tenant_animated_logo</c>,
/// <c>epc_ecomae_platform_tenant_mini_hero_visual</c>,
/// <c>epc_ecomae_platform_tenant_cp_modules</c>,
/// <c>epc_ecomae_platform_tenant_cp_preview</c>,
/// <c>epc_ecomae_platform_tenant_key_for_industry</c>,
/// <c>epc_ecomae_platform_tenant_storefront_screenshot</c>,
/// <c>epc_ecomae_platform_tenant_storefront_preview</c>,
/// <c>epc_ecomae_platform_tenant_showcase_card</c>,
/// <c>epc_ecomae_platform_tenant_showcase_section</c>,
/// <c>epc_ecomae_platform_industry_themed_previews</c>.
/// GET never mints a session cookie. Leftover marketing-data / home-page
/// parents stay injected.
/// </summary>
public static class PhpPlanQ1Jib
{
    public const string TenantShowcasePath = "content/general_pages/epc_ecomae_platform_tenant_showcase.php";

    private const string Php = ".php";
    private const string Styles = """
.epm-tenant-showcase{margin:28px 0 36px}
.epm-tenant-showcase__head{margin-bottom:22px}
.epm-tenant-showcase__grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:22px}
.epm-tenant-card{background:var(--epm-card);border:1px solid var(--epm-border);border-radius:18px;padding:18px 18px 20px;box-shadow:var(--epm-glow);display:flex;flex-direction:column;gap:14px}
.epm-tenant-card__head{display:flex;align-items:center;gap:12px;flex-wrap:wrap}
.epm-tenant-card__logo{height:44px;width:auto;max-width:160px;object-fit:contain}
.epm-tenant-card__meta{flex:1;min-width:140px}
.epm-tenant-card__meta h3{margin:0 0 4px;font-size:17px;color:#fff}
.epm-tenant-card__meta p{margin:0;font-size:12px;color:var(--epm-muted);line-height:1.45}
.epm-tenant-card__theme{display:inline-block;font-size:10px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;padding:4px 10px;border-radius:999px;border:1px solid rgba(255,255,255,.12);color:var(--epm-cyan)}
.epm-tenant-card__previews{display:grid;grid-template-columns:1fr 1fr;gap:10px}
@media(max-width:520px){.epm-tenant-card__previews{grid-template-columns:1fr}}
.epm-mini-browser{border-radius:12px;overflow:hidden;border:1px solid rgba(148,163,184,.22);background:#020617}
.epm-mini-browser__bar{display:flex;align-items:center;gap:6px;padding:7px 10px;background:rgba(15,23,42,.95);border-bottom:1px solid rgba(148,163,184,.15);font-size:9px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:var(--epm-muted)}
.epm-mini-browser__bar span{width:7px;height:7px;border-radius:50%;background:#334155}
.epm-mini-browser__bar span:nth-child(1){background:#ef4444}
.epm-mini-browser__bar span:nth-child(2){background:#eab308}
.epm-mini-browser__bar span:nth-child(3){background:#22c55e}
.epm-mini-browser__bar em{margin-left:auto;font-style:normal}
.epm-mini-hero{position:relative;height:118px;overflow:hidden;display:flex;align-items:center;justify-content:center}
.epm-mini-hero--live{height:auto;min-height:0;display:block;background:#fff}
.epm-mini-hero--live img{width:100%;height:auto;display:block;max-height:200px;object-fit:cover;object-position:top center}
.epm-mini-hero__copy{position:absolute;left:10px;bottom:10px;z-index:2;max-width:58%}
.epm-mini-hero__copy strong{display:block;font-size:11px;color:#fff;line-height:1.25;margin-bottom:2px;text-shadow:0 1px 8px rgba(0,0,0,.55)}
.epm-mini-hero__copy small{font-size:9px;color:rgba(255,255,255,.78);line-height:1.35}
.epm-mini-logo{position:absolute;top:8px;left:10px;z-index:3;transform:scale(.72);transform-origin:left top}
.epm-mini-cp{height:118px;display:grid;grid-template-columns:34% 1fr;font-size:9px;color:#e2e8f0}
.epm-mini-cp__side{padding:8px 6px;display:flex;flex-direction:column;gap:5px}
.epm-mini-cp__side strong{font-size:8px;letter-spacing:.08em;text-transform:uppercase;opacity:.85;margin-bottom:2px}
.epm-mini-cp__item{padding:4px 6px;border-radius:6px;background:rgba(255,255,255,.06);font-size:8px;line-height:1.3}
.epm-mini-cp__item.is-active{font-weight:700;box-shadow:inset 0 0 0 1px rgba(255,255,255,.18)}
.epm-mini-cp__main{padding:8px;background:rgba(2,6,23,.55);display:flex;flex-direction:column;gap:6px}
.epm-mini-cp__kpi{display:grid;grid-template-columns:repeat(3,1fr);gap:4px}
.epm-mini-cp__kpi span{background:rgba(255,255,255,.06);border-radius:6px;padding:5px 4px;text-align:center;font-size:7px;line-height:1.25}
.epm-mini-cp__kpi span b{display:block;font-size:9px;color:#fff}
.epm-mini-cp__rows{display:flex;flex-direction:column;gap:3px;flex:1}
.epm-mini-cp__row{height:7px;border-radius:999px;background:rgba(255,255,255,.08)}
.epm-mini-cp__row:nth-child(2){width:88%}
.epm-mini-cp__row:nth-child(3){width:72%}
/* Animated logos (marketing-scoped) */
.epm-mk-logo{display:inline-flex;align-items:center;gap:6px;font-weight:800;font-size:14px;letter-spacing:.02em}
.epm-mk-logo svg{display:block;height:34px;width:auto}
.epm-mk-logo__text{line-height:1}
.epm-mk-logo--parts .epm-mk-logo__text{color:#fff}
.epm-mk-logo--parts .epm-mk-logo__cart{animation:epmMkCartBob 2.4s ease-in-out infinite}
.epm-mk-logo--parts .epm-mk-logo__wheel{animation:epmMkWheelSpin 1.2s linear infinite;transform-origin:center}
.epm-mk-logo--consult .epm-mk-logo__text{color:#fff}
.epm-mk-logo--consult .epm-mk-logo__bar{animation:epmMkBarGrow 2.2s ease-in-out infinite;transform-origin:bottom}
.epm-mk-logo--consult .epm-mk-logo__bar:nth-child(2){animation-delay:.25s}
.epm-mk-logo--consult .epm-mk-logo__bar:nth-child(3){animation-delay:.5s}
.epm-mk-logo--electronics .epm-mk-logo__text{color:#fff}
.epm-mk-logo--electronics .epm-mk-logo__pulse{animation:epmMkPulse 1.6s ease-in-out infinite}
.epm-mk-logo--fashion .epm-mk-logo__text{color:#be185d}
.epm-mk-logo--fashion .epm-mk-logo__hanger{animation:epmMkSwing 2.8s ease-in-out infinite;transform-origin:top center}
.epm-mk-logo--jewellery .epm-mk-logo__text{color:#d4af37}
.epm-mk-logo--jewellery .epm-mk-logo__spark{animation:epmMkSparkle 1.8s ease-in-out infinite}
.epm-mk-logo--jewellery .epm-mk-logo__spark:nth-child(2){animation-delay:.4s}
.epm-mk-logo--jewellery .epm-mk-logo__spark:nth-child(3){animation-delay:.8s}
/* Mini hero animations */
.epm-mk-piston{display:flex;gap:8px;align-items:flex-end;height:72px;padding:0 12px}
.epm-mk-piston__cyl{width:22px;height:58px;border-radius:8px 8px 4px 4px;border:2px solid rgba(148,163,184,.55);background:#020617;position:relative;overflow:hidden}
.epm-mk-piston__p{position:absolute;left:3px;right:3px;height:18px;border-radius:6px;background:linear-gradient(180deg,#f8fafc,#94a3b8);animation:epmMkPistonA 1.05s linear infinite}
.epm-mk-piston__cyl:nth-child(2) .epm-mk-piston__p{animation-name:epmMkPistonB}
.epm-mk-piston__cyl:nth-child(3) .epm-mk-piston__p{animation-name:epmMkPistonA}
.epm-mk-consult{display:flex;align-items:flex-end;gap:8px;height:72px;padding:0 16px}
.epm-mk-consult__bar{width:16px;border-radius:4px 4px 0 0;background:linear-gradient(180deg,var(--mk-accent,#d4af37),var(--mk-primary,#1e40af));animation:epmMkBarGrow 2s ease-in-out infinite}
.epm-mk-consult__bar:nth-child(1){height:36px}
.epm-mk-consult__bar:nth-child(2){height:52px;animation-delay:.2s}
.epm-mk-consult__bar:nth-child(3){height:44px;animation-delay:.4s}
.epm-mk-consult__bar:nth-child(4){height:28px;animation-delay:.6s}
.epm-mk-electronics{position:relative;width:100%;height:72px;display:flex;align-items:center;justify-content:center;gap:6px}
.epm-mk-electronics__chip{width:48px;height:32px;border-radius:6px;border:2px solid rgba(225,10,10,.65);background:#111;position:relative;overflow:hidden}
.epm-mk-electronics__chip:before{content:"";position:absolute;inset:0;background:linear-gradient(90deg,transparent,rgba(225,10,10,.35),transparent);animation:epmMkScan 1.8s linear infinite}
.epm-mk-electronics__wave{display:flex;gap:3px;align-items:flex-end;height:36px}
.epm-mk-electronics__wave span{width:4px;background:#e10a0a;border-radius:2px;animation:epmMkWave 1.2s ease-in-out infinite}
.epm-mk-electronics__wave span:nth-child(2){animation-delay:.15s;height:22px}
.epm-mk-electronics__wave span:nth-child(3){animation-delay:.3s;height:30px}
.epm-mk-electronics__wave span:nth-child(4){animation-delay:.45s;height:18px}
.epm-mk-electronics__wave span:nth-child(1){height:26px}
.epm-mk-fashion{display:flex;align-items:center;justify-content:center;gap:14px;height:72px}
.epm-mk-fashion__chip{padding:6px 12px;border-radius:999px;background:rgba(236,72,153,.18);border:1px solid rgba(236,72,153,.45);color:#831843;font-size:9px;font-weight:700;animation:epmMkChipFloat 2.4s ease-in-out infinite}
.epm-mk-fashion__chip:nth-child(2){animation-delay:.5s;background:rgba(190,24,93,.15);color:#be185d}
.epm-mk-fashion__dress{width:34px;height:48px;border-radius:18px 18px 8px 8px;background:linear-gradient(180deg,#ec4899,#be185d);animation:epmMkDressSway 3s ease-in-out infinite;transform-origin:top center}
.epm-mk-jewellery{position:relative;width:100%;height:72px;display:flex;align-items:center;justify-content:center}
.epm-mk-jewellery__ring{width:46px;height:46px;border-radius:50%;border:5px solid #d4af37;box-shadow:0 0 18px rgba(212,175,55,.45);animation:epmMkRingGlow 2.2s ease-in-out infinite}
.epm-mk-jewellery__gem{position:absolute;width:10px;height:10px;background:#fff;border-radius:2px;transform:rotate(45deg);box-shadow:0 0 12px rgba(255,255,255,.8)}
@keyframes epmMkCartBob{0%,100%{transform:translateY(0)}50%{transform:translateY(-3px)}}
@keyframes epmMkWheelSpin{to{transform:rotate(360deg)}}
@keyframes epmMkBarGrow{0%,100%{transform:scaleY(.65);opacity:.7}50%{transform:scaleY(1);opacity:1}}
@keyframes epmMkPulse{0%,100%{opacity:.45;transform:scale(.92)}50%{opacity:1;transform:scale(1.05)}}
@keyframes epmMkSwing{0%,100%{transform:rotate(-6deg)}50%{transform:rotate(6deg)}}
@keyframes epmMkSparkle{0%,100%{opacity:.2;transform:scale(.6)}50%{opacity:1;transform:scale(1.2)}}
@keyframes epmMkPistonA{0%,100%{top:6px}50%{top:28px}}
@keyframes epmMkPistonB{0%,100%{top:28px}50%{top:6px}}
@keyframes epmMkScan{0%{transform:translateX(-100%)}100%{transform:translateX(100%)}}
@keyframes epmMkWave{0%,100%{transform:scaleY(.5)}50%{transform:scaleY(1)}}
@keyframes epmMkChipFloat{0%,100%{transform:translateY(0)}50%{transform:translateY(-4px)}}
@keyframes epmMkDressSway{0%,100%{transform:rotate(-4deg)}50%{transform:rotate(4deg)}}
@keyframes epmMkRingGlow{0%,100%{box-shadow:0 0 12px rgba(212,175,55,.35)}50%{box-shadow:0 0 24px rgba(212,175,55,.75)}}
@media(prefers-reduced-motion:reduce){
.epm-mk-logo *,.epm-mk-piston *,.epm-mk-consult *,.epm-mk-electronics *,.epm-mk-fashion *,.epm-mk-jewellery *{animation:none!important}
}
""";


    public static Func<List<Dictionary<string, object?>>>? CustomerResults { get; set; }
    public static Func<string, string>? Screenshot { get; set; }
    public static Func<string>? BaseUrl { get; set; }

    public static void Reset()
    {
        CustomerResults = () => [];
        Screenshot = _ => "";
        BaseUrl = () => "https://www.ecomae.com/";
    }

    public static Dictionary<string, Dictionary<string, string>> EpcEcomaePlatformTenantShowcaseThemes()
        => new(StringComparer.Ordinal)
        {
            ["auto_parts"] = Theme("auto_parts", "automotive_spareparts_pro", "Automotive spare parts pro", "#2563eb", "#ef4444", "#0f172a", "#111827", "#1e293b", "#3b82f6", "piston"),
            ["tax_advisory"] = Theme("tax_advisory", "consulting_primeinvest", "Prime Invest consulting", "#1e40af", "#d4af37", "#0f172a", "#1e3a5f", "#191919", "#227a40", "consultancy"),
            ["electronics"] = Theme("electronics", "electronics_retail_virgin", "Virgin-style electronics retail", "#e10a0a", "#000000", "#0a0a0a", "#1a1a1a", "#111111", "#e10a0a", "electronics"),
            ["fashion"] = Theme("fashion", "fashion_retail_namshi", "Namshi fashion & beauty", "#ec4899", "#be185d", "#fdf2f8", "#fce7f3", "#831843", "#ec4899", "fashion"),
            ["jewellery"] = Theme("jewellery", "jewellery_retail_kiyasha", "Kiyasha jewellery luxury", "#b8860b", "#92400e", "#1c1917", "#292524", "#1c1917", "#d4af37", "jewellery")
        };

    public static Dictionary<string, string> EpcEcomaePlatformTenantShowcaseTheme(string industryCode)
    {
        var themes = EpcEcomaePlatformTenantShowcaseThemes();
        var code = SanitizeIndustry(industryCode);
        return themes.TryGetValue(code, out var theme) ? theme : themes["auto_parts"];
    }

    public static List<Dictionary<string, object?>> EpcEcomaePlatformTenantShowcaseRows()
    {
        var tenants = CustomerResults != null ? CustomerResults() : [];
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["epartscart"] = "auto_parts",
            ["taxofinca"] = "tax_advisory",
            ["electronicae"] = "electronics",
            ["stylenlook"] = "fashion",
            ["thejewellerytrend"] = "jewellery"
        };
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var row in tenants)
        {
            var copy = new Dictionary<string, object?>(row, StringComparer.Ordinal);
            var key = Str(copy, "key");
            string industry;
            if (copy.ContainsKey("industry") && copy["industry"] != null)
            {
                industry = Convert.ToString(copy["industry"]) ?? "";
            }
            else
            {
                industry = map.TryGetValue(key, out var mapped) ? mapped : "auto_parts";
            }

            copy["industry"] = industry;
            copy["theme_meta"] = EpcEcomaePlatformTenantShowcaseTheme(industry);
            outRows.Add(copy);
        }

        return outRows;
    }

    public static string EpcEcomaePlatformTenantShowcaseStyles()
        => Styles;

    public static string EpcEcomaePlatformTenantAnimatedLogo(string industryCode, string label = "")
    {
        var theme = EpcEcomaePlatformTenantShowcaseTheme(industryCode);
        var type = theme.TryGetValue("hero_type", out var ht) ? ht : "piston";
        var name = label != "" ? label : theme["label"];
        var primary = H(theme["primary"]);
        var accent = H(theme["accent"]);
        if (type == "piston")
        {
            return "<span class=\"epm-mk-logo epm-mk-logo--parts\" aria-hidden=\"true\">\n"
                + "\t<span class=\"epm-mk-logo__text\">eparts</span>\n"
                + "\t<svg viewBox=\"0 0 88 40\" xmlns=\"http://www.w3.org/2000/svg\" class=\"epm-mk-logo__cart\" aria-hidden=\"true\">\n"
                + "\t\t<path fill=\"" + primary + "\" d=\"M8 8h44c3 0 5 2 5 5l-4 16H18L8 8z\"/>\n"
                + "\t\t<circle class=\"epm-mk-logo__wheel\" cx=\"22\" cy=\"32\" r=\"6\" fill=\"#334155\" stroke=\"#94a3b8\" stroke-width=\"2\"/>\n"
                + "\t\t<circle class=\"epm-mk-logo__wheel\" cx=\"44\" cy=\"32\" r=\"6\" fill=\"#334155\" stroke=\"#94a3b8\" stroke-width=\"2\"/>\n"
                + "\t\t<rect x=\"30\" y=\"14\" width=\"10\" height=\"8\" rx=\"2\" fill=\"#e2e8f0\"/>\n"
                + "\t</svg>\n"
                + "</span>\n\t\t";
        }

        if (type == "consultancy")
        {
            return "<span class=\"epm-mk-logo epm-mk-logo--consult\" aria-hidden=\"true\">\n"
                + "\t<svg viewBox=\"0 0 48 36\" xmlns=\"http://www.w3.org/2000/svg\" aria-hidden=\"true\">\n"
                + "\t\t<rect class=\"epm-mk-logo__bar\" x=\"4\" y=\"18\" width=\"8\" height=\"14\" rx=\"2\" fill=\"" + accent + "\"/>\n"
                + "\t\t<rect class=\"epm-mk-logo__bar\" x=\"18\" y=\"10\" width=\"8\" height=\"22\" rx=\"2\" fill=\"" + primary + "\"/>\n"
                + "\t\t<rect class=\"epm-mk-logo__bar\" x=\"32\" y=\"14\" width=\"8\" height=\"18\" rx=\"2\" fill=\"" + accent + "\"/>\n"
                + "\t</svg>\n"
                + "\t<span class=\"epm-mk-logo__text\">" + H(name) + "</span>\n"
                + "</span>\n\t\t";
        }

        if (type == "electronics")
        {
            return "<span class=\"epm-mk-logo epm-mk-logo--electronics\" aria-hidden=\"true\">\n"
                + "\t<svg viewBox=\"0 0 40 36\" xmlns=\"http://www.w3.org/2000/svg\" aria-hidden=\"true\">\n"
                + "\t\t<rect x=\"6\" y=\"8\" width=\"28\" height=\"20\" rx=\"4\" fill=\"#111\" stroke=\"" + primary + "\" stroke-width=\"2\"/>\n"
                + "\t\t<circle class=\"epm-mk-logo__pulse\" cx=\"20\" cy=\"18\" r=\"5\" fill=\"" + primary + "\"/>\n"
                + "\t</svg>\n"
                + "\t<span class=\"epm-mk-logo__text\">" + H(name) + "</span>\n"
                + "</span>\n\t\t";
        }

        if (type == "fashion")
        {
            return "<span class=\"epm-mk-logo epm-mk-logo--fashion\" aria-hidden=\"true\">\n"
                + "\t<svg viewBox=\"0 0 36 40\" xmlns=\"http://www.w3.org/2000/svg\" class=\"epm-mk-logo__hanger\" aria-hidden=\"true\">\n"
                + "\t\t<path d=\"M18 4c-2 0-3 1.5-3 3.5S16 11 18 11s3-1.5 3-3.5S20 4 18 4z\" fill=\"" + accent + "\"/>\n"
                + "\t\t<path d=\"M18 11 L6 28 L30 28 Z\" fill=\"" + primary + "\" opacity=\".9\"/>\n"
                + "\t</svg>\n"
                + "\t<span class=\"epm-mk-logo__text\">" + H(name) + "</span>\n"
                + "</span>\n\t\t";
        }

        return "<span class=\"epm-mk-logo epm-mk-logo--jewellery\" aria-hidden=\"true\">\n"
            + "\t<svg viewBox=\"0 0 44 40\" xmlns=\"http://www.w3.org/2000/svg\" aria-hidden=\"true\">\n"
            + "\t\t<circle cx=\"22\" cy=\"22\" r=\"14\" fill=\"none\" stroke=\"" + primary + "\" stroke-width=\"4\"/>\n"
            + "\t\t<path class=\"epm-mk-logo__spark\" d=\"M22 6 L24 12 L30 12 L25 16 L27 22 L22 18 L17 22 L19 16 L14 12 L20 12 Z\" fill=\"" + accent + "\"/>\n"
            + "\t</svg>\n"
            + "\t<span class=\"epm-mk-logo__text\">" + H(name) + "</span>\n"
            + "</span>\n\t\t";
    }

    public static string EpcEcomaePlatformTenantMiniHeroVisual(string industryCode)
    {
        var theme = EpcEcomaePlatformTenantShowcaseTheme(industryCode);
        var type = theme.TryGetValue("hero_type", out var ht) ? ht : "piston";
        var style = "--mk-primary:" + H(theme["primary"]) + ";--mk-accent:" + H(theme["accent"])
            + ";background:linear-gradient(135deg," + H(theme["bg_from"]) + "," + H(theme["bg_to"]) + ")";
        var body = type switch
        {
            "piston" => "\t<div class=\"epm-mk-piston\" aria-hidden=\"true\">\n"
                + "\t\t<div class=\"epm-mk-piston__cyl\"><span class=\"epm-mk-piston__p\"></span></div>\n"
                + "\t\t<div class=\"epm-mk-piston__cyl\"><span class=\"epm-mk-piston__p\"></span></div>\n"
                + "\t\t<div class=\"epm-mk-piston__cyl\"><span class=\"epm-mk-piston__p\"></span></div>\n"
                + "\t</div>\n",
            "consultancy" => "\t<div class=\"epm-mk-consult\" aria-hidden=\"true\">\n"
                + "\t\t<span class=\"epm-mk-consult__bar\"></span><span class=\"epm-mk-consult__bar\"></span><span class=\"epm-mk-consult__bar\"></span><span class=\"epm-mk-consult__bar\"></span>\n"
                + "\t</div>\n",
            "electronics" => "\t<div class=\"epm-mk-electronics\" aria-hidden=\"true\">\n"
                + "\t\t<span class=\"epm-mk-electronics__chip\"></span>\n"
                + "\t\t<span class=\"epm-mk-electronics__wave\"><span></span><span></span><span></span><span></span></span>\n"
                + "\t</div>\n",
            "fashion" => "\t<div class=\"epm-mk-fashion\" aria-hidden=\"true\">\n"
                + "\t\t<span class=\"epm-mk-fashion__chip\">New in</span>\n"
                + "\t\t<span class=\"epm-mk-fashion__dress\"></span>\n"
                + "\t\t<span class=\"epm-mk-fashion__chip\">Beauty</span>\n"
                + "\t</div>\n",
            _ => "\t<div class=\"epm-mk-jewellery\" aria-hidden=\"true\">\n"
                + "\t\t<span class=\"epm-mk-jewellery__ring\"></span>\n"
                + "\t\t<span class=\"epm-mk-jewellery__gem\"></span>\n"
                + "\t</div>\n"
        };
        return "<div class=\"epm-mini-hero\" style=\"" + style + "\">\n"
            + "\t<div class=\"epm-mini-logo\">" + EpcEcomaePlatformTenantAnimatedLogo(industryCode) + "</div>\n"
            + "\t" + body
            + "\t"
            + "\t<div class=\"epm-mini-hero__copy\">\n"
            + "\t\t<strong>" + H(theme["label"]) + "</strong>\n"
            + "\t\t<small>Industry-themed storefront hero</small>\n"
            + "\t</div>\n"
            + "</div>\n\t";
    }

    public static List<string> EpcEcomaePlatformTenantCpModules(string industryCode)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["auto_parts"] = ["Orders", "Prices", "Procurement", "Logistics"],
            ["tax_advisory"] = ["Clients", "VAT", "Documents", "ERP"],
            ["electronics"] = ["Catalogue", "Orders", "RMA", "Stock"],
            ["fashion"] = ["Collections", "Orders", "Campaigns", "Stock"],
            ["jewellery"] = ["Gallery", "Enquiries", "Certificates", "CRM"]
        };
        var code = SanitizeIndustry(industryCode);
        return map.TryGetValue(code, out var mods) ? mods : ["Orders", "Catalogue", "Finance", "Settings"];
    }

    public static string EpcEcomaePlatformTenantCpPreview(string industryCode, string label = "Client CP")
    {
        var theme = EpcEcomaePlatformTenantShowcaseTheme(industryCode);
        var mods = EpcEcomaePlatformTenantCpModules(industryCode);
        var sidebar = H(theme["cp_sidebar"]);
        var accent = H(theme["cp_accent"]);
        var items = new System.Text.StringBuilder();
        for (var i = 0; i < mods.Count; i++)
        {
            if (i == 0)
            {
                items.Append("\t\t\t\t\t\t<span class=\"epm-mini-cp__item is-active\" style=\"background:")
                    .Append(accent).Append(";color:#fff\">").Append(H(mods[i])).Append("</span>\n");
            }
            else
            {
                items.Append("\t\t\t\t\t\t<span class=\"epm-mini-cp__item\">").Append(H(mods[i])).Append("</span>\n");
            }
        }

        return "<div class=\"epm-mini-browser\">\n"
            + "\t<div class=\"epm-mini-browser__bar\"><span></span><span></span><span></span><em>" + H(label) + "</em></div>\n"
            + "\t<div class=\"epm-mini-cp\">\n"
            + "\t\t<div class=\"epm-mini-cp__side\" style=\"background:" + sidebar + "\">\n"
            + "\t\t\t<strong>Modules</strong>\n"
            + items
            + "\t\t\t\t\t</div>\n"
            + "\t\t<div class=\"epm-mini-cp__main\">\n"
            + "\t\t\t<div class=\"epm-mini-cp__kpi\">\n"
            + "\t\t\t\t<span><b>24</b>Today</span>\n"
            + "\t\t\t\t<span><b>AED</b>Revenue</span>\n"
            + "\t\t\t\t<span><b>Live</b>Status</span>\n"
            + "\t\t\t</div>\n"
            + "\t\t\t<div class=\"epm-mini-cp__rows\">\n"
            + "\t\t\t\t<span class=\"epm-mini-cp__row\"></span><span class=\"epm-mini-cp__row\"></span><span class=\"epm-mini-cp__row\"></span>\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t</div>\n"
            + "</div>\n\t";
    }

    public static string EpcEcomaePlatformTenantKeyForIndustry(string industryCode)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["auto_parts"] = "epartscart",
            ["tax_advisory"] = "taxofinca",
            ["electronics"] = "electronicae",
            ["fashion"] = "stylenlook",
            ["jewellery"] = "thejewellerytrend"
        };
        var code = SanitizeIndustry(industryCode);
        return map.TryGetValue(code, out var key) ? key : "";
    }

    public static string EpcEcomaePlatformTenantStorefrontScreenshot(string tenantKey)
    {
        var key = Regex.Replace((tenantKey ?? "").ToLowerInvariant(), "[^a-z0-9]", "");
        if (key == "")
        {
            return "";
        }

        var slug = "tenant-" + key + "-storefront";
        var disk = Screenshot != null ? Screenshot(slug) : "";
        if (disk == "")
        {
            return "";
        }

        var ext = "png";
        var m = Regex.Match(disk, @"\.([a-z0-9]+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            ext = m.Groups[1].Value.ToLowerInvariant();
        }

        return "/" + "epc-ecomae-tenant-asset" + Php + "?f=" + Uri.EscapeDataString(slug + "." + ext);
    }

    public static string EpcEcomaePlatformTenantStorefrontPreview(string industryCode, string label = "Storefront", string tenantKey = "")
    {
        if (tenantKey == "")
        {
            tenantKey = EpcEcomaePlatformTenantKeyForIndustry(industryCode);
        }

        var shot = EpcEcomaePlatformTenantStorefrontScreenshot(tenantKey);
        var inner = shot != ""
            ? "\t<div class=\"epm-mini-hero epm-mini-hero--live\">\n"
                + "\t\t<img src=\"" + H(shot) + "\" alt=\"" + H(label) + " — live storefront\" loading=\"lazy\" width=\"640\" height=\"360\" />\n"
                + "\t</div>\n"
                + "\t"
            : EpcEcomaePlatformTenantMiniHeroVisual(industryCode);
        return "<div class=\"epm-mini-browser\">\n"
            + "\t<div class=\"epm-mini-browser__bar\"><span></span><span></span><span></span><em>" + H(label) + "</em></div>\n"
            + "\t"
            + inner
            + "</div>\n\t";
    }

    public static string EpcEcomaePlatformTenantShowcaseCard(Dictionary<string, object?> tenant)
    {
        var industry = tenant.TryGetValue("industry", out var ind) && ind != null ? Convert.ToString(ind) ?? "auto_parts" : "auto_parts";
        Dictionary<string, string> theme;
        if (tenant.TryGetValue("theme_meta", out var meta) && meta is Dictionary<string, string> named)
        {
            theme = named;
        }
        else
        {
            theme = EpcEcomaePlatformTenantShowcaseTheme(industry);
        }

        var key = Str(tenant, "key");
        var name = Str(tenant, "name");
        var outcome = Str(tenant, "outcome");
        var siteUrl = tenant.ContainsKey("site_url") ? Str(tenant, "site_url") : "#";
        var portalUrl = tenant.ContainsKey("portal_url") ? Str(tenant, "portal_url") : "#";
        var logoUrl = Str(tenant, "logo_url");
        var logo = logoUrl != ""
            ? "\t\t\t\t<img class=\"epm-tenant-card__logo\" src=\"" + H(logoUrl) + "\" alt=\"" + H(name) + " logo\" loading=\"lazy\" />\n\t\t"
            : "\t\t\t\t<div class=\"epm-mini-logo\">" + EpcEcomaePlatformTenantAnimatedLogo(industry, name) + "</div>\n\t\t";
        return "<article class=\"epm-tenant-card\" data-industry=\"" + H(industry) + "\">\n"
            + "\t<div class=\"epm-tenant-card__head\">\n"
            + logo
            + "\t\t<div class=\"epm-tenant-card__meta\">\n"
            + "\t\t\t<h3>" + H(name) + "</h3>\n"
            + "\t\t\t<p>" + H(outcome) + "</p>\n"
            + "\t\t</div>\n"
            + "\t\t<span class=\"epm-tenant-card__theme\">" + H(theme["label"]) + "</span>\n"
            + "\t</div>\n"
            + "\t<div class=\"epm-tenant-card__previews\">\n"
            + "\t\t" + EpcEcomaePlatformTenantStorefrontPreview(industry, "Storefront", key)
            + EpcEcomaePlatformTenantCpPreview(industry, "Control panel")
            + "\t</div>\n"
            + "\t<div class=\"epm-cta\" style=\"margin-top:4px\">\n"
            + "\t\t<a class=\"epm-btn epm-btn--primary epm-btn--sm\" href=\"" + H(siteUrl) + "\" target=\"_blank\" rel=\"noopener\">Visit site <i class=\"fa fa-external-link\"></i></a>\n"
            + "\t\t<a class=\"epm-btn epm-btn--outline epm-btn--sm\" href=\"" + H(portalUrl) + "\" target=\"_blank\" rel=\"noopener\">Open /cp/</a>\n"
            + "\t</div>\n"
            + "</article>\n\t";
    }

    public static string EpcEcomaePlatformTenantShowcaseSection(string variant = "page")
    {
        var rows = EpcEcomaePlatformTenantShowcaseRows();
        var baseUrl = BaseUrl != null ? BaseUrl() : "https://www.ecomae.com/";
        var isHome = variant == "home";
        var sectionCls = "epm-tenant-showcase" + (isHome ? " epm-tenant-showcase--home" : "");
        var cards = new System.Text.StringBuilder();
        foreach (var tenant in rows)
        {
            cards.Append(EpcEcomaePlatformTenantShowcaseCard(tenant));
        }

        var homeCta = isHome
            ? "\t\t<div class=\"epm-cta\" style=\"margin-top:18px\">\n"
                + "\t\t\t<a class=\"epm-btn epm-btn--primary\" href=\"" + H(baseUrl) + "platform/customer-results\"><i class=\"fa fa-trophy\"></i> Full customer results</a>\n"
                + "\t\t\t<a class=\"epm-btn epm-btn--outline\" href=\"" + H(baseUrl) + "platform/industries\"><i class=\"fa fa-industry\"></i> Browse industries</a>\n"
                + "\t\t</div>\n"
                + "\t\t"
            : "";
        return "<section class=\"" + H(sectionCls) + "\" id=\"tenant-showcase\" aria-labelledby=\"epm-tenant-showcase-title\">\n"
            + "\t<div class=\"epm-wrap\">\n"
            + "\t\t<div class=\"epm-tenant-showcase__head\">\n"
            + "\t\t\t<div class=\"epm-badge\"><i class=\"fa fa-paint-brush\"></i> Live tenant themes</div>\n"
            + "\t\t\t<h2 class=\"epm-section-title\" id=\"epm-tenant-showcase-title\" style=\"margin-top:8px\">Industry storefronts in production</h2>\n"
            + "\t\t\t<p class=\"epm-section-lead\" style=\"max-width:860px;margin-bottom:0\">eParts Cart uses a live capture from <a href=\"https://www.epartscart.com/\" target=\"_blank\" rel=\"noopener\" style=\"color:var(--epm-cyan)\">epartscart.com</a> (piston hero, AI Parts Expert, automotive theme). Other tenants show animated industry heroes until storefront screenshots are added. CP cards use tenant colours, not generic gray mocks.</p>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"epm-tenant-showcase__grid\">\n"
            + "\t\t\t" + cards
            + "\t\t</div>\n"
            + "\t\t"
            + homeCta
            + "\t</div>\n"
            + "</section>\n\t";
    }

    public static string EpcEcomaePlatformIndustryThemedPreviews(string industryCode, string industryName)
    {
        var tenantKey = EpcEcomaePlatformTenantKeyForIndustry(industryCode);
        var hasLiveShot = EpcEcomaePlatformTenantStorefrontScreenshot(tenantKey) != "";
        var storeCaption = hasLiveShot
            ? "Live storefront capture from production — e.g. epartscart.com hero, search, and piston animation."
            : "Animated industry hero with tenant brand colours — marketing preview until a screenshot is added.";
        return "<div class=\"epm-area__shots\" style=\"margin-bottom:32px\">\n"
            + "\t<figure class=\"epm-preview\">\n"
            + "\t\t" + EpcEcomaePlatformTenantStorefrontPreview(industryCode, "Storefront — " + industryName, tenantKey)
            + "\t\t<figcaption><strong>Storefront — " + H(industryName) + "</strong><p>" + H(storeCaption) + "</p></figcaption>\n"
            + "\t</figure>\n"
            + "\t<figure class=\"epm-preview\">\n"
            + "\t\t" + EpcEcomaePlatformTenantCpPreview(industryCode, "Client CP — " + industryName)
            + "\t\t<figcaption><strong>Client CP — " + H(industryName) + "</strong><p>Industry-themed sidebar and module packs — not a generic login screen.</p></figcaption>\n"
            + "\t</figure>\n"
            + "</div>\n\t";
    }

    private static Dictionary<string, string> Theme(
        string industry, string theme, string label, string primary, string accent,
        string bgFrom, string bgTo, string sidebar, string cpAccent, string hero)
        => new(StringComparer.Ordinal)
        {
            ["industry"] = industry,
            ["theme"] = theme,
            ["label"] = label,
            ["primary"] = primary,
            ["accent"] = accent,
            ["bg_from"] = bgFrom,
            ["bg_to"] = bgTo,
            ["cp_sidebar"] = sidebar,
            ["cp_accent"] = cpAccent,
            ["hero_type"] = hero
        };

    private static string SanitizeIndustry(string value)
        => Regex.Replace((value ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var raw) && raw != null ? Convert.ToString(raw) ?? "" : "";

    private static string H(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
