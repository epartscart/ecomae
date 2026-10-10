using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-faq helpers. PHP identifiers kept for the inventory:
/// <c>epc_ecomae_faq_format_answer</c>, <c>epc_ecomae_faq_status_class</c>,
/// <c>epc_ecomae_faq_schema_json</c>, <c>epc_ecomae_faq_styles</c>,
/// <c>epc_ecomae_faq_render_page</c>, <c>showModule</c>, <c>filterFaq</c>.
/// </summary>
public static class PhpPlanQ1Faq
{
    public const string FaqPath = "content/general_pages/epc_ecomae_faq.php";

    public static string BaseUrl { get; set; } = "https://www.ecomae.com/";
    public static int DemoDays { get; set; } = 14;

    private static readonly Dictionary<string, string> StatusClass = new(StringComparer.Ordinal)
    {
        ["Yes"] = "epm-faq__status--yes",
        ["Partial"] = "epm-faq__status--partial",
        ["Planned"] = "epm-faq__status--planned",
        ["No"] = "epm-faq__status--no"
    };

    public static void Reset()
    {
        BaseUrl = "https://www.ecomae.com/";
        DemoDays = 14;
    }

    public static string EpcEcomaeFaqFormatAnswer(string answer)
    {
        var html = H(answer);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Catalog API"] = BaseUrl + "platform/api-services",
            ["Auto Price AI page"] = BaseUrl + "platform/auto-price-ai",
            ["Auto Price AI"] = BaseUrl + "platform/auto-price-ai",
            ["demo page"] = BaseUrl + "platform/demo",
            ["pricing page"] = BaseUrl + "platform/pricing",
            ["business continuity page"] = BaseUrl + "platform/business-continuity",
            ["platform overview"] = BaseUrl + "platform",
            ["vehicle catalogue capability pages"] = BaseUrl + "platform/capabilities?highlight=vehicle-catalogue",
            ["vehicle catalogue capability page"] = BaseUrl + "platform/capabilities?highlight=vehicle-catalogue"
        };
        foreach (var label in replacements.Keys.OrderByDescending(k => k.Length))
        {
            var link = "<a href=\"" + H(replacements[label]) + "\">" + H(label) + "</a>";
            html = html.Replace(H(label), link, StringComparison.Ordinal);
        }

        return html;
    }

    public static string EpcEcomaeFaqStatusClass(string status)
        => StatusClass.TryGetValue(status, out var cls) ? cls : "epm-faq__status--partial";

    public static string EpcEcomaeFaqSchemaJson()
    {
        var entities = new List<object>();
        foreach (var mod in PhpPlanQ1Pack.EpcEcomaeFaqModules().EnumerateArray())
        {
            foreach (var item in mod.GetProperty("items").EnumerateArray())
            {
                entities.Add(new Dictionary<string, object?>
                {
                    ["@type"] = "Question",
                    ["name"] = item.GetProperty("q").GetString() ?? "",
                    ["acceptedAnswer"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "Answer",
                        ["text"] = StripTags(EpcEcomaeFaqFormatAnswer(item.GetProperty("a").GetString() ?? ""))
                    }
                });
            }
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = entities
        }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    public static string EpcEcomaeFaqStyles() => Styles;

    public static string EpcEcomaeFaqRenderPage()
    {
        var modules = PhpPlanQ1Pack.EpcEcomaeFaqModules();
        var counts = PhpPlanQ1Pack.EpcEcomaeFaqStatusCounts();
        var total = counts.Values.Sum();
        var sb = new StringBuilder();
        sb.Append(EpcEcomaeFaqStyles());
        sb.Append("<script type=\"application/ld+json\">").Append(EpcEcomaeFaqSchemaJson()).Append("</script>");
        sb.Append("<div class=\"epm-wrap\">");
        sb.Append("<div class=\"epm-hero epm-faq-hero\">");
        sb.Append("<div class=\"epm-hero__content\">");
        sb.Append("<h1>Frequently Asked Questions</h1>");
        sb.Append("<div class=\"epm-faq-search\"><input type=\"search\" id=\"epm_faq_search\" /></div>");
        sb.Append("<div class=\"epm-faq-stats\">");
        sb.Append("<span class=\"epm-faq-stat\"><strong>").Append(counts["Yes"]).Append("</strong> Yes</span>");
        sb.Append("<span class=\"epm-faq-stat\"><strong>").Append(counts["Partial"]).Append("</strong> Partial</span>");
        sb.Append("<span class=\"epm-faq-stat\"><strong>").Append(counts["Planned"]).Append("</strong> Planned</span>");
        sb.Append("<span class=\"epm-faq-stat\"><strong>").Append(counts["No"]).Append("</strong> No / contact us</span>");
        sb.Append("</div></div></div>");
        sb.Append("<nav class=\"epm-faq-tabs\" id=\"epm_faq_tabs\">");
        var i = 0;
        foreach (var mod in modules.EnumerateArray())
        {
            var id = mod.GetProperty("id").GetString() ?? "";
            var icon = mod.GetProperty("icon").GetString() ?? "";
            var title = mod.GetProperty("title").GetString() ?? "";
            sb.Append("<button type=\"button\" class=\"epm-faq-tab").Append(i == 0 ? " is-active" : "").Append("\" data-module=\"").Append(H(id)).Append("\">");
            sb.Append("<i class=\"fa ").Append(H(icon)).Append("\"></i>").Append(H(title)).Append("</button>");
            i++;
        }

        sb.Append("</nav><div class=\"epm-faq-body\">");
        sb.Append("<p class=\"epm-faq-empty\" id=\"epm_faq_empty\">No matching questions — try another keyword or <a href=\"").Append(H(BaseUrl)).Append("platform/contact\">contact us</a>.</p>");
        i = 0;
        foreach (var mod in modules.EnumerateArray())
        {
            var id = mod.GetProperty("id").GetString() ?? "";
            var title = mod.GetProperty("title").GetString() ?? "";
            var range = mod.GetProperty("range").GetString() ?? "";
            var items = mod.GetProperty("items");
            sb.Append("<section class=\"epm-faq-module").Append(i == 0 ? " is-active" : "").Append("\" id=\"epm_faq_").Append(H(id)).Append("\" data-module=\"").Append(H(id)).Append("\">");
            sb.Append("<div class=\"epm-faq-module__head\"><h2>").Append(H(title)).Append("</h2><p>Questions ").Append(H(range)).Append(" · ").Append(items.GetArrayLength()).Append(" topics</p></div>");
            foreach (var item in items.EnumerateArray())
            {
                var status = item.GetProperty("status").GetString() ?? "";
                var q = item.GetProperty("q").GetString() ?? "";
                var a = item.GetProperty("a").GetString() ?? "";
                var num = item.GetProperty("num").GetInt32();
                sb.Append("<article class=\"epm-faq-item\" data-search=\"").Append(H((q + " " + a).ToLowerInvariant())).Append("\">");
                sb.Append("<button type=\"button\" class=\"epm-faq-item__q\" aria-expanded=\"false\">");
                sb.Append("<span class=\"epm-faq-item__num\">Q").Append(num).Append("</span><span>").Append(H(q)).Append("</span>");
                sb.Append("<i class=\"fa fa-chevron-down epm-faq-item__chev\" aria-hidden=\"true\"></i></button>");
                sb.Append("<div class=\"epm-faq-item__a\"><span class=\"epm-faq__status ").Append(H(EpcEcomaeFaqStatusClass(status))).Append("\">").Append(H(status)).Append("</span>");
                sb.Append("<div>").Append(EpcEcomaeFaqFormatAnswer(a)).Append("</div></div></article>");
            }

            sb.Append("</section>");
            i++;
        }

        sb.Append("</div><div class=\"epm-faq-cta\"><div class=\"epm-cta\">");
        sb.Append("<a class=\"epm-btn epm-btn--primary\" href=\"").Append(H(BaseUrl)).Append("platform/demo\">").Append(DemoDays).Append("-day demo</a>");
        sb.Append("<a href=\"").Append(H(BaseUrl)).Append("platform/capabilities\">Capabilities catalog</a>");
        sb.Append("<a href=\"").Append(H(BaseUrl)).Append("platform/contact\">Contact</a>");
        sb.Append("</div></div></div>");
        sb.Append("""
<script defer>
(function(){
	var tabs=document.querySelectorAll('.epm-faq-tab');
	var modules=document.querySelectorAll('.epm-faq-module');
	var search=document.getElementById('epm_faq_search');
	var empty=document.getElementById('epm_faq_empty');
	function showModule(id){
		for(var i=0;i<tabs.length;i++){tabs[i].classList.toggle('is-active',tabs[i].getAttribute('data-module')===id);}
		for(var j=0;j<modules.length;j++){modules[j].classList.toggle('is-active',modules[j].getAttribute('data-module')===id);}
	}
	for(var t=0;t<tabs.length;t++){
		tabs[t].addEventListener('click',function(){showModule(this.getAttribute('data-module'));});
	}
	document.querySelectorAll('.epm-faq-item__q').forEach(function(btn){
		btn.addEventListener('click',function(){
			var item=btn.closest('.epm-faq-item');
			var open=item.classList.toggle('is-open');
			btn.setAttribute('aria-expanded',open?'true':'false');
		});
	});
	function filterFaq(){
		var q=(search&&search.value||'').toLowerCase().trim();
		var anyVisible=false;
		document.querySelectorAll('.epm-faq-item').forEach(function(item){
			var hay=(item.getAttribute('data-search')||'');
			var match=!q||hay.indexOf(q)!==-1;
			item.classList.toggle('is-hidden',!match);
			if(match)anyVisible=true;
		});
		if(empty){empty.classList.toggle('is-visible',q&&!anyVisible);}
		if(q){
			for(var m=0;m<modules.length;m++){modules[m].classList.add('is-active');}
			for(var x=0;x<tabs.length;x++){tabs[x].classList.remove('is-active');}
		}
	}
	if(search){search.addEventListener('input',filterFaq);}
})();
</script>
""");
        return sb.ToString();
    }

    private static string H(object? value) => PhpPlanQ1Plus.EpcChannelH(value);

    private static string StripTags(string html)
    {
        var sb = new StringBuilder();
        var inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<')
            {
                inTag = true;
            }
            else if (ch == '>')
            {
                inTag = false;
            }
            else if (!inTag)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private const string Styles = """
<style>
.epm-faq-hero{padding:48px 24px 32px;text-align:center}
.epm-faq-search{max-width:640px;margin:24px auto 0;position:relative}
.epm-faq-search input{width:100%;padding:14px 48px 14px 18px;border-radius:12px;border:1px solid rgba(14,165,233,.35);background:rgba(10,10,10,.75);color:#e2e8f0;font-size:16px}
.epm-faq-search input:focus{outline:none;border-color:#0284c7;box-shadow:0 0 0 3px rgba(14,165,233,.2)}
.epm-faq-search i{position:absolute;right:18px;top:50%;transform:translateY(-50%);color:#64748b}
.epm-faq-stats{display:flex;flex-wrap:wrap;gap:10px;justify-content:center;margin:20px 0 8px}
.epm-faq-stat{font-size:13px;padding:6px 12px;border-radius:999px;background:rgba(23,23,23,.8);border:1px solid rgba(148,163,184,.2);color:#94a3b8}
.epm-faq-stat strong{color:#e2e8f0}
.epm-faq-tabs{display:flex;flex-wrap:wrap;gap:8px;justify-content:center;padding:0 16px 24px;border-bottom:1px solid rgba(148,163,184,.15)}
.epm-faq-tab{padding:10px 16px;border-radius:10px;border:1px solid rgba(14,165,233,.25);background:rgba(10,10,10,.5);color:#94a3b8;font-size:13px;cursor:pointer;transition:.15s}
.epm-faq-tab:hover,.epm-faq-tab.is-active{background:linear-gradient(135deg,#5a0f16,#8a131c);color:#fff;border-color:transparent}
.epm-faq-tab i{margin-right:6px}
.epm-faq-body{max-width:920px;margin:0 auto;padding:24px 16px 48px}
.epm-faq-module{display:none}
.epm-faq-module.is-active{display:block}
.epm-faq-module__head{margin-bottom:20px}
.epm-faq-module__head h2{font-size:1.35rem;margin:0 0 6px;color:#f1f5f9}
.epm-faq-module__head p{color:#94a3b8;margin:0;font-size:14px}
.epm-faq-item{border:1px solid rgba(148,163,184,.15);border-radius:12px;margin-bottom:10px;background:rgba(23,23,23,.55);overflow:hidden}
.epm-faq-item.is-hidden{display:none}
.epm-faq-item__q{width:100%;text-align:left;padding:16px 18px;background:transparent;border:0;color:#e2e8f0;font-size:15px;font-weight:600;cursor:pointer;display:flex;align-items:flex-start;gap:12px;line-height:1.45}
.epm-faq-item__q:hover{background:rgba(14,165,233,.06)}
.epm-faq-item__num{flex:0 0 auto;font-size:12px;color:#64748b;min-width:28px;padding-top:2px}
.epm-faq-item__chev{margin-left:auto;flex:0 0 auto;color:#64748b;transition:transform .2s}
.epm-faq-item.is-open .epm-faq-item__chev{transform:rotate(180deg)}
.epm-faq-item__a{display:none;padding:0 18px 18px 58px;color:#cbd5e1;font-size:14px;line-height:1.65}
.epm-faq-item.is-open .epm-faq-item__a{display:block}
.epm-faq-item__a a{color:#0284c7;text-decoration:underline}
.epm-faq__status{display:inline-block;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.04em;padding:3px 8px;border-radius:6px;margin-bottom:8px}
.epm-faq__status--yes{background:rgba(34,197,94,.15);color:#4ade80}
.epm-faq__status--partial{background:rgba(251,191,36,.12);color:#fbbf24}
.epm-faq__status--planned{background:rgba(14,165,233,.12);color:#0284c7}
.epm-faq__status--no{background:rgba(248,113,113,.12);color:#f87171}
.epm-faq-empty{text-align:center;padding:32px;color:#64748b;display:none}
.epm-faq-empty.is-visible{display:block}
.epm-faq-cta{text-align:center;padding:32px 16px 48px;border-top:1px solid rgba(148,163,184,.12)}
@media(max-width:640px){.epm-faq-item__a{padding-left:18px}.epm-faq-tabs{justify-content:flex-start;overflow-x:auto;flex-wrap:nowrap;padding-bottom:16px}}
</style>
""";
}
