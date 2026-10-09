using System.Text.Json;

namespace EcomAE.Platform.Storefront;

/// <summary>Body assets emitted by the two PHP helpers required by <c>cart.php</c>.</summary>
public static class StorefrontCartAssets
{
    public const string PriceStyles =
        "<style>"
        + ".epc-price-login-cta{display:inline-block;font-size:12px;line-height:1.35;color:#64748b}"
        + ".epc-price-login-cta a{font-weight:600;color:#2b78d6;text-decoration:none}"
        + ".epc-price-login-cta a:hover{text-decoration:underline}"
        + ".epc-price-login-cta__sep{color:#94a3b8}"
        + ".epc-price-login-cta__hint{color:#64748b}"
        + ".td_price .epc-price-login-cta{max-width:140px}"
        + ".epc-commerce-login-cta{display:flex;flex-direction:column;align-items:flex-start;gap:6px;max-width:180px}"
        + ".epc-commerce-login-cta .btn{margin:0}"
        + ".epc-commerce-login-cta__sep{font-size:12px;color:#94a3b8}"
        + ".epc-commerce-login-cta__hint{font-size:11px;line-height:1.35;color:#64748b}"
        + ".epc-commerce-login-cta--inline{display:inline-flex;flex-direction:row;flex-wrap:nowrap;align-items:center;gap:4px;max-width:none;white-space:nowrap;font-size:12px;line-height:1.2;color:#64748b}"
        + ".epc-commerce-login-cta--inline a{font-weight:700;color:#2563eb;text-decoration:none}"
        + ".epc-commerce-login-cta--inline a:hover{text-decoration:underline}"
        + ".epc-product-actions--guest{flex-wrap:nowrap;width:auto}"
        + ".epc-product-actions__tools--guest{flex-wrap:nowrap;white-space:nowrap;gap:8px}"
        + "#all_table_products .td_add_to_cart .epc-product-actions--guest{justify-content:flex-start}"
        + ".epc-cart-login-gate{max-width:520px;margin:32px auto;padding:28px 24px;text-align:center;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px}"
        + ".epc-cart-login-gate h2{margin:0 0 10px;font-size:22px;color:#0f172a}"
        + ".epc-cart-login-gate p{margin:0 0 18px;color:#475569}"
        + ".epc-cart-login-gate .epc-commerce-login-cta{align-items:center;max-width:none;flex-direction:row;flex-wrap:wrap;justify-content:center}"
        + "</style>";

    public const string WhatsAppStyles =
        "<style>.epc-wa-share-btn{background:#25D366!important;border-color:#1da851!important;color:#fff!important;margin:2px 4px 2px 0;}"
        + ".epc-wa-share-btn:hover,.epc-wa-share-btn:focus{background:#1da851!important;color:#fff!important;}"
        + ".epc-wa-share-row{margin:8px 0 0;display:flex;flex-wrap:wrap;gap:6px;align-items:center;}"
        + ".epc-wa-share-panel{background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:12px 14px;margin:12px 0;}"
        + ".epc-wa-share-panel h5{margin:0 0 8px;font-size:14px;font-weight:700;color:#166534;}"
        + ".epc-wa-share-panel .text-muted{font-size:12px;margin-bottom:8px;}"
        + ".epc-product-actions .epc-wa-share-btn{padding:6px 10px;font-size:11px;text-decoration:none!important;display:inline-flex;align-items:center;gap:4px;white-space:nowrap;}"
        + ".epc-product-actions{display:inline-flex;flex-direction:row;flex-wrap:nowrap;gap:6px;align-items:center;justify-content:flex-start;}"
        + ".epc-product-actions__tools,.epc-product-actions__buy{display:inline-flex;flex-wrap:nowrap;gap:6px;align-items:center;justify-content:flex-start;width:auto;flex:0 0 auto;}"
        + ".epc-product-actions__tools .epc-wa-share-btn,.epc-product-actions__tools .epc-fitment-check-btn--row{flex:0 0 auto;}"
        + "#all_table_products .td_add_to_cart .epc-product-actions{flex-wrap:nowrap;justify-content:flex-start;overflow:visible;}"
        + "#all_table_products .td_price .epc-price-value{display:block;font-weight:700;white-space:nowrap;line-height:1.3;}</style>";

    public static string WhatsAppScript(string salesDigits, string salesDisplay, string site, string domain)
        => """
<script>
window.epcWaShare = {
	sales: {{SALES}},
	salesDisplay: {{DISPLAY}},
	site: {{SITE}},
	domain: {{DOMAIN}}
};
function epcWaOpen(digits, text) {
	digits = String(digits || '').replace(/\D/g, '');
	if (!digits) { return; }
	window.open('https://wa.me/' + digits + '?text=' + encodeURIComponent(text || ''), '_blank', 'noopener,noreferrer');
}
function epcWaBilingual(en, ar) {
	en = (en || '').trim();
	ar = (ar || '').trim();
	if (!en) { return ar; }
	if (!ar) { return en; }
	return en + '\n' + ar;
}
function epcWaShareCart() {
	var s = window.epcWaShare || {};
	if (typeof cart_records === 'undefined' || !cart_records || !cart_records.length) { return; }
	var lines = [];
	var total = 0;
	for (var i = 0; i < cart_records.length && lines.length < 15; i++) {
		var r = cart_records[i];
		var line = (r.manufacturer || '') + ' ' + (r.article || '');
		if (r.name) { line += ' — ' + r.name; }
		if (r.count_need) { line += ' ×' + r.count_need; }
		lines.push(line.trim());
		if (r.price && r.count_need) { total += parseFloat(r.price) * parseInt(r.count_need, 10); }
	}
	var en = 'Hello ' + (s.site || 'eParts Cart') + ', please assist with my cart:\n\n' + lines.join('\n');
	if (total > 0) { en += '\n\nEstimated total: ' + total.toFixed(2) + ' AED'; }
	en += '\n\n' + (s.domain || '') + '/shop/cart';
	var ar = 'مرحباً، أرجو المساعدة في سلة التسوق:\n\n' + lines.join('\n');
	epcWaOpen(s.sales, epcWaBilingual(en, ar));
}
</script>
"""
            .Replace("{{SALES}}", JsonSerializer.Serialize(salesDigits), StringComparison.Ordinal)
            .Replace("{{DISPLAY}}", JsonSerializer.Serialize(salesDisplay), StringComparison.Ordinal)
            .Replace("{{SITE}}", JsonSerializer.Serialize(site), StringComparison.Ordinal)
            .Replace("{{DOMAIN}}", JsonSerializer.Serialize(domain.TrimEnd('/')), StringComparison.Ordinal);
}
