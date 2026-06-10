<?php
/**
 * ePartsCart — dedicated spare parts search (brand + part number, warehouse only).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_automotive_spareparts_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/epc_spare_parts_warehouse.php';

global $db_link, $DP_Config;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$pdo instanceof PDO) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}

$lang = epc_asp_home_lang();
$brands = epc_spare_parts_oem_brands($pdo);

$prefBrand = isset($_GET['brand']) ? trim((string) $_GET['brand']) : '';
$prefArticle = isset($_GET['article']) ? trim((string) $_GET['article']) : '';
$path = parse_url((string) ($_SERVER['REQUEST_URI'] ?? ''), PHP_URL_PATH);
if (is_string($path) && preg_match('#/spare-parts/([^/]+)/([^/]+)/?$#', $path, $m)) {
	if ($prefBrand === '') {
		$prefBrand = rawurldecode($m[1]);
	}
	if ($prefArticle === '') {
		$prefArticle = rawurldecode($m[2]);
	}
}

$inlineResult = null;
if ($prefBrand !== '' && $prefArticle !== '') {
	$inlineResult = epc_spare_parts_warehouse_search($prefBrand, $prefArticle, $pdo, $DP_Config);
}

$searchApi = '/content/shop/epc_spare_parts_search.php';
?>
<section class="epc-sp epc-asp-home-section" id="epc-spare-parts" aria-labelledby="epc_sp_title">
	<div class="container">
		<div class="epc-sp__head">
			<h1 class="epc-sp__title" id="epc_sp_title">Spare parts search</h1>
			<p class="epc-sp__lead">Search our UAE warehouse by <strong>brand</strong> and <strong>part number</strong> only — e.g. Toyota · 1310154101, Bosch · P3310. No description browsing.</p>
		</div>

		<form class="epc-sp__form" id="epc-sp-form" autocomplete="off">
			<label class="sr-only" for="epc-sp-brand">Brand</label>
			<select class="form-control epc-sp__brand" id="epc-sp-brand" name="brand" required>
				<option value="">Select brand…</option>
				<?php foreach ($brands as $b) {
					$val = (string) ($b['value'] ?? '');
					$sel = (strcasecmp($val, $prefBrand) === 0) ? ' selected' : '';
					?>
				<option value="<?php echo htmlspecialchars($val, ENT_QUOTES, 'UTF-8'); ?>"<?php echo $sel; ?>><?php echo htmlspecialchars((string) ($b['label'] ?? $val), ENT_QUOTES, 'UTF-8'); ?></option>
				<?php } ?>
			</select>
			<label class="sr-only" for="epc-sp-article">Part number</label>
			<input type="text" class="form-control epc-sp__article" id="epc-sp-article" name="article"
				value="<?php echo htmlspecialchars($prefArticle, ENT_QUOTES, 'UTF-8'); ?>"
				placeholder="Part number — e.g. 1310154101, P3310, C110J" maxlength="64" required />
			<button type="submit" class="btn btn-primary epc-sp__submit" id="epc-sp-submit">
				<i class="fa fa-search" aria-hidden="true"></i> Search warehouse
			</button>
		</form>

		<p class="epc-sp__hint" id="epc-sp-hint" aria-live="polite">Results show warehouse stock and price — not external supplier feeds.</p>
		<div class="epc-sp__results" id="epc-sp-results" aria-live="polite"></div>
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
	var API = <?php echo json_encode($searchApi, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var lang = <?php echo json_encode($lang, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var form = document.getElementById('epc-sp-form');
	var brandEl = document.getElementById('epc-sp-brand');
	var articleEl = document.getElementById('epc-sp-article');
	var resultsEl = document.getElementById('epc-sp-results');
	var hintEl = document.getElementById('epc-sp-hint');
	var submitBtn = document.getElementById('epc-sp-submit');
	if (!form || !resultsEl) { return; }

	function esc(s) {
		return String(s == null ? '' : s).replace(/[&<>"']/g, function (ch) {
			return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' })[ch];
		});
	}
	function fmtPrice(n, cur) {
		var v = parseFloat(n);
		if (!isFinite(v) || v <= 0) { return '—'; }
		return v.toFixed(2) + ' ' + esc(cur || 'AED');
	}
	function render(data) {
		if (!data || !data.ok) {
			resultsEl.innerHTML = '<div class="epc-sp-card epc-sp-card--miss"><p class="epc-sp-card__empty">' + esc((data && data.message) || 'Search failed.') + '</p></div>';
			return;
		}
		if (data.redirect_url) {
			location.href = data.redirect_url;
			return;
		}
		var inWh = data.in_warehouse && (parseFloat(data.qty) > 0 || (data.warehouse_rows && data.warehouse_rows.length));
		var cls = inWh ? 'epc-sp-card' : 'epc-sp-card epc-sp-card--miss';
		var label = esc(data.brand) + ' · ' + esc(data.article);
		var html = '<div class="' + cls + '">'
			+ '<p class="epc-sp-card__id"><span>' + esc(data.brand) + '</span> · ' + esc(data.article) + '</p>'
			+ '<div class="epc-sp-card__meta">'
			+ '<span>In warehouse: <strong>' + (inWh ? 'Yes' : 'No') + '</strong></span>'
			+ (inWh ? '<span>Qty: <strong>' + esc(data.qty) + '</strong></span>' : '')
			+ '</div>';
		if (parseFloat(data.sell_price) > 0) {
			html += '<p class="epc-sp-card__price">Price: ' + fmtPrice(data.sell_price, data.currency) + '</p>';
		} else if (!inWh) {
			html += '<p class="epc-sp-card__empty">' + esc(data.message || 'Not in stock — contact us.') + '</p>';
		}
		html += '<div class="epc-sp-card__actions">';
		if (data.product_url) {
			html += '<a class="btn btn-primary" href="' + esc(data.product_url) + '">View product</a>';
		} else if (data.parts_url && inWh) {
			html += '<a class="btn btn-default" href="' + esc(data.parts_url) + '">Open parts page</a>';
		}
		html += '<a class="btn btn-default" href="' + esc(lang) + '/kontakty">Contact us</a>';
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
	<?php if (is_array($inlineResult) && !empty($inlineResult['ok'])) { ?>
	render(<?php echo json_encode($inlineResult, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>);
	<?php } elseif (is_array($inlineResult) && $prefBrand !== '' && $prefArticle !== '') { ?>
	render(<?php echo json_encode($inlineResult, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>);
	<?php } ?>
})();
</script>
