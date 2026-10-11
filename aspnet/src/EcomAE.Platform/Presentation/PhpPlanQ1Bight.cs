using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-bight ePartsCart spare-parts page and warehouse search ajax.
/// PHP identifiers kept for the inventory: <c>esc</c>, <c>fmtPrice</c>, <c>render</c>, <c>runSearch</c>.
/// Paths: <c>content/general_pages/epc_epartscart_spare_parts.php</c>,
/// <c>content/shop/epc_spare_parts_search.php</c>.
/// GET never mints a session cookie. Leftover automotive-data stays injected.
/// Warehouse helpers are the Road twin.
/// </summary>
public static class PhpPlanQ1Bight
{
    public const string EpartscartSparePartsPath = "content/general_pages/epc_epartscart_spare_parts.php";
    public const string SparePartsSearchPath = "content/shop/epc_spare_parts_search.php";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Func<bool>? HasPdo { get; set; }
    public static Func<bool>? AjaxPdoOk { get; set; }
    public static Func<string>? LangPrefix { get; set; }
    public static Func<List<Dictionary<string, object?>>>? Brands { get; set; }
    public static Func<string, string, Dictionary<string, object?>>? Search { get; set; }
    public static Dictionary<string, string> Query { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Request { get; set; } = new(StringComparer.Ordinal);
    public static string RequestUri { get; set; } = "/en/spare-parts";
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        HasPdo = null;
        AjaxPdoOk = null;
        LangPrefix = null;
        Brands = null;
        Search = null;
        Query = new Dictionary<string, string>(StringComparer.Ordinal);
        Request = new Dictionary<string, string>(StringComparer.Ordinal);
        RequestUri = "/en/spare-parts";
        LastOutput = "";
    }

    public static string EpcEpartscartSparePartsPage()
    {
        LastOutput = "";
        if (HasPdo?.Invoke() != true)
        {
            LastOutput = "<div class=\"alert alert-danger\">Database unavailable.</div>";
            return LastOutput;
        }

        var lang = LangPrefix?.Invoke() ?? "/en";
        var brands = Brands?.Invoke() ?? [];
        var prefBrand = Query.TryGetValue("brand", out var qb) ? qb.Trim() : "";
        var prefArticle = Query.TryGetValue("article", out var qa) ? qa.Trim() : "";
        var path = PathOf(RequestUri);
        var match = Regex.Match(path, @"/spare-parts/([^/]+)/([^/]+)/?$");
        if (match.Success)
        {
            if (prefBrand == "")
            {
                prefBrand = Uri.UnescapeDataString(match.Groups[1].Value);
            }

            if (prefArticle == "")
            {
                prefArticle = Uri.UnescapeDataString(match.Groups[2].Value);
            }
        }

        Dictionary<string, object?>? inline = null;
        if (prefBrand != "" && prefArticle != "")
        {
            inline = Search?.Invoke(prefBrand, prefArticle)
                ?? new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = false,
                    ["message"] = "Enter a valid part number (at least 2 characters)."
                };
        }

        var options = "";
        foreach (var row in brands)
        {
            var val = Str(row.GetValueOrDefault("value"));
            var label = Str(row.GetValueOrDefault("label"), val);
            var sel = string.Equals(val, prefBrand, StringComparison.OrdinalIgnoreCase) ? " selected" : "";
            options += "\t\t\t\t\t\t\t\t<option value=\"" + H(val) + "\"" + sel + ">" + H(label) + "</option>\n";
        }

        options += "\t\t\t\t\t\t\t";
        var inlineJs = "";
        if (inline is not null)
        {
            inlineJs = "\t\trender(" + Json(inline) + ");\n";
        }

        LastOutput = PageTemplate
            .Replace("var lang = \"/en\";", "var lang = " + Json(lang) + ";", StringComparison.Ordinal)
            .Replace("__OPTIONS__", options, StringComparison.Ordinal)
            .Replace("__ARTICLE__", H(prefArticle), StringComparison.Ordinal)
            .Replace("__INLINE__", inlineJs, StringComparison.Ordinal);
        return LastOutput;
    }

    public static object[] EpcSparePartsSearchAjax()
    {
        var brand = Request.TryGetValue("brand", out var b) ? b.Trim() : "";
        var article = Request.TryGetValue("article", out var a) ? a.Trim() : "";
        if (brand == "" && Request.TryGetValue("manufacturer", out var m))
        {
            brand = m.Trim();
        }

        if (AjaxPdoOk?.Invoke() != true)
        {
            return ["{\"ok\":false,\"message\":\"Database unavailable.\"}", 503];
        }

        var result = Search?.Invoke(brand, article)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Enter a valid part number (at least 2 characters)."
            };
        return [Json(result), false];
    }

    public static string Capture(Func<string> render)
    {
        LastOutput = "";
        return render();
    }

    private static string PathOf(string uri)
    {
        var q = uri.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            uri = uri[..q];
        }

        return uri;
    }

    private static string Json(object? value)
        => JsonSerializer.Serialize(value, JsonOpts);

    private static string H(object? value)
    {
        var s = Str(value);
        return s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string Str(object? value, string fallback = "")
        => value is null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;

    private const string PageTemplate = @"<section class=""epc-sp epc-asp-home-section"" id=""epc-spare-parts"" aria-labelledby=""epc_sp_title"">
	<div class=""container"">
		<div class=""epc-sp__head"">
			<h1 class=""epc-sp__title"" id=""epc_sp_title"">Spare parts search</h1>
			<p class=""epc-sp__lead"">Search our UAE warehouse by <strong>brand</strong> and <strong>part number</strong> only — e.g. Toyota · 1310154101, Bosch · P3310. No description browsing.</p>
		</div>

		<form class=""epc-sp__form"" id=""epc-sp-form"" autocomplete=""off"">
			<label class=""sr-only"" for=""epc-sp-brand"">Brand</label>
			<select class=""form-control epc-sp__brand"" id=""epc-sp-brand"" name=""brand"" required>
				<option value="""">Select brand…</option>
__OPTIONS__</select>
			<label class=""sr-only"" for=""epc-sp-article"">Part number</label>
			<input type=""text"" class=""form-control epc-sp__article"" id=""epc-sp-article"" name=""article""
				value=""__ARTICLE__""
				placeholder=""Part number — e.g. 1310154101, P3310, C110J"" maxlength=""64"" required />
			<button type=""submit"" class=""btn btn-primary epc-sp__submit"" id=""epc-sp-submit"">
				<i class=""fa fa-search"" aria-hidden=""true""></i> Search warehouse
			</button>
		</form>

		<p class=""epc-sp__hint"" id=""epc-sp-hint"" aria-live=""polite"">Results show warehouse stock and price — not external supplier feeds.</p>
		<div class=""epc-sp__results"" id=""epc-sp-results"" aria-live=""polite""></div>
	</div>
</section>

<style>
.epc-sp{margin:24px 0 40px}
.epc-sp__head{margin-bottom:18px}
.epc-sp__title{margin:0;font-size:28px;font-weight:900;letter-spacing:-.02em}
.epc-sp__lead{margin:8px 0 0;color:#64748b;max-width:720px}
.epc-sp__form{display:grid;grid-template-columns:minmax(160px,220px) minmax(180px,1fr) auto;gap:10px;align-items:center;margin-top:18px}
.epc-sp__brand,.epc-sp__article{height:46px;border-radius:10px;border:1px solid #cfd8e6}
.epc-sp__submit{height:46px;border-radius:10px;font-weight:800;background:linear-gradient(135deg,#ef4444,#dc2626);border-color:#dc2626}
.epc-sp__hint{font-size:13px;color:#64748b;margin:12px 0 0}
.epc-sp__results{margin-top:18px}
.epc-sp-card{background:#fff;border:1px solid #e5eaf2;border-radius:16px;padding:20px 22px;box-shadow:0 12px 32px rgba(15,23,42,.08)}
.epc-sp-card__id{font-size:20px;font-weight:900;color:#0f172a;margin:0 0 8px}
.epc-sp-card__id span{color:#dc2626}
.epc-sp-card__meta{display:flex;flex-wrap:wrap;gap:12px 20px;margin:0 0 14px;color:#334155;font-size:15px}
.epc-sp-card__meta strong{font-weight:800}
.epc-sp-card__price{font-size:22px;font-weight:900;color:#0f172a;margin:0 0 16px}
.epc-sp-card__actions{display:flex;flex-wrap:wrap;gap:10px}
.epc-sp-card__empty{color:#64748b;padding:16px 0}
.epc-sp-card--miss{border-color:#fed7aa;background:#fffbeb}
@media (max-width:767px){.epc-sp__form{grid-template-columns:1fr}}
</style>

<script>
(function () {
	var API = ""/content/shop/epc_spare_parts_search.php"";
	var lang = ""/en"";
	var form = document.getElementById('epc-sp-form');
	var brandEl = document.getElementById('epc-sp-brand');
	var articleEl = document.getElementById('epc-sp-article');
	var resultsEl = document.getElementById('epc-sp-results');
	var hintEl = document.getElementById('epc-sp-hint');
	var submitBtn = document.getElementById('epc-sp-submit');
	if (!form || !resultsEl) { return; }

	function esc(s) {
		return String(s == null ? '' : s).replace(/[&<>""']/g, function (ch) {
			return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '""': '&quot;', ""'"": '&#039;' })[ch];
		});
	}
	function fmtPrice(n, cur) {
		var v = parseFloat(n);
		if (!isFinite(v) || v <= 0) { return '—'; }
		return v.toFixed(2) + ' ' + esc(cur || 'AED');
	}
	function render(data) {
		if (!data || !data.ok) {
			resultsEl.innerHTML = '<div class=""epc-sp-card epc-sp-card--miss""><p class=""epc-sp-card__empty"">' + esc((data && data.message) || 'Search failed.') + '</p></div>';
			return;
		}
		if (data.redirect_url) {
			location.href = data.redirect_url;
			return;
		}
		var inWh = data.in_warehouse && (parseFloat(data.qty) > 0 || (data.warehouse_rows && data.warehouse_rows.length));
		var cls = inWh ? 'epc-sp-card' : 'epc-sp-card epc-sp-card--miss';
		var label = esc(data.brand) + ' · ' + esc(data.article);
		var html = '<div class=""' + cls + '"">'
			+ '<p class=""epc-sp-card__id""><span>' + esc(data.brand) + '</span> · ' + esc(data.article) + '</p>'
			+ '<div class=""epc-sp-card__meta"">'
			+ '<span>In warehouse: <strong>' + (inWh ? 'Yes' : 'No') + '</strong></span>'
			+ (inWh ? '<span>Qty: <strong>' + esc(data.qty) + '</strong></span>' : '')
			+ '</div>';
		if (parseFloat(data.sell_price) > 0) {
			html += '<p class=""epc-sp-card__price"">Price: ' + fmtPrice(data.sell_price, data.currency) + '</p>';
		} else if (!inWh) {
			html += '<p class=""epc-sp-card__empty"">' + esc(data.message || 'Not in stock — contact us.') + '</p>';
		}
		html += '<div class=""epc-sp-card__actions"">';
		if (data.product_url) {
			html += '<a class=""btn btn-primary"" href=""' + esc(data.product_url) + '"">View product</a>';
		} else if (data.parts_url && inWh) {
			html += '<a class=""btn btn-default"" href=""' + esc(data.parts_url) + '"">Open parts page</a>';
		}
		html += '<a class=""btn btn-default"" href=""' + esc(lang) + '/kontakty"">Contact us</a>';
		html += '</div></div>';
		resultsEl.innerHTML = html;
		if (hintEl) {
			hintEl.textContent = inWh ? 'Warehouse match — price from our stock list.' : (data.message || 'No warehouse stock for this brand / part number.');
		}
		var u = lang + '/spare-parts/' + encodeURIComponent(data.brand || '') + '/' + encodeURIComponent(data.article || '');
		if (history.replaceState) {
			history.replaceState(null, label, u);
		}
	}
	function runSearch() {
		var brand = brandEl ? brandEl.value.trim() : '';
		var article = articleEl ? articleEl.value.trim() : '';
		if (!brand || !article) { return; }
		if (submitBtn) { submitBtn.disabled = true; }
		if (hintEl) { hintEl.textContent = 'Searching warehouse…'; }
		resultsEl.innerHTML = '';
		var url = API + '?brand=' + encodeURIComponent(brand) + '&article=' + encodeURIComponent(article);
		fetch(url, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
			.then(function (r) { return r.json(); })
			.then(render)
			.catch(function () {
				render({ ok: false, message: 'Search request failed. Try again.' });
			})
			.finally(function () {
				if (submitBtn) { submitBtn.disabled = false; }
			});
	}
	form.addEventListener('submit', function (e) {
		e.preventDefault();
		runSearch();
	});
__INLINE__	})();
</script>
";

}
