<?php
defined('_ASTEXE_') or die('No access');
$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') ? rtrim($multilang_params['lang_href'], '/') : '/en';
$epc_pf_chpu_on = !empty($DP_Config->chpu_search_config['chpu_search_on']);
$epc_pf_chpu_parts_url = !empty($DP_Config->chpu_search_config['level_1']['url']) ? $DP_Config->chpu_search_config['level_1']['url'] : 'parts';
$epc_pf_chpu_brands_url = !empty($DP_Config->chpu_search_config['level_2']['mode_1']['url']) ? $DP_Config->chpu_search_config['level_2']['mode_1']['url'] : 'brands';
$epc_pf_chpu_slash_code = isset($DP_Config->chpu_search_config['slash_code']) ? $DP_Config->chpu_search_config['slash_code'] : '%2F';
?>
<style>
.epc-pf { margin: 0 0 32px; }
.epc-pf-title { margin: 0 0 8px; font-size: 24px; font-weight: 700; color: #172536; }
.epc-pf-lead { margin: 0 0 16px; color: #657184; font-size: 14px; max-width: 920px; }
.epc-pf-panel { background: #fff; border: 1px solid #e1e7ef; border-radius: 10px; padding: 16px; margin-bottom: 16px; }
.epc-pf-searchbar { display: grid; grid-template-columns: 1fr 1fr auto; gap: 10px; align-items: end; margin-bottom: 16px; }
.epc-pf-searchbar label { font-weight: 700; display: block; margin-bottom: 4px; color: #172536; font-size: 13px; }
.epc-pf-steps { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 14px; }
.epc-pf-step { border: 1px solid #d7dee9; background: #f8fafc; border-radius: 20px; padding: 6px 12px; font-size: 13px; font-weight: 600; color: #64748b; }
.epc-pf-step.active { background: #2b78d6; border-color: #2b78d6; color: #fff; }
.epc-pf-step.done { background: #e8f5e9; border-color: #86efac; color: #166534; }
.epc-pf-summary { display: grid; grid-template-columns: repeat(auto-fill, minmax(130px, 1fr)); gap: 10px; margin: 0 0 18px; }
.epc-pf-summary__card { background: #f8fafc; border: 1px solid #e1e7ef; border-radius: 8px; padding: 12px 10px; text-align: center; }
.epc-pf-summary__card strong { display: block; font-size: 22px; line-height: 1.2; color: #172536; }
.epc-pf-summary__card span { font-size: 12px; color: #64748b; }
.epc-pf-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; }
.epc-pf-product-card { border: 1px solid #e1e7ef; border-radius: 8px; padding: 12px; background: #fff; min-height: 100px; cursor: pointer; transition: border-color .15s, box-shadow .15s; }
.epc-pf-product-card:hover { border-color: #2b78d6; box-shadow: 0 4px 12px rgba(43,120,214,.12); }
.epc-pf-product-card.is-active { border-color: #2b78d6; background: #f0f7ff; }
.epc-pf-product-card strong { display: block; color: #172536; margin-bottom: 6px; }
.epc-pf-product-card small { display: block; color: #64748b; font-size: 12px; line-height: 1.4; }
.epc-pf-product-card small.epc-pf-brands-line { max-height: 3.6em; overflow: hidden; }
.epc-pf-more-brands { color: #2b78d6; font-weight: 600; }
.epc-pf-product-card__hint { display: block; margin-top: 8px; font-size: 11px; color: #2b78d6; font-weight: 600; }
.epc-pf-section-title { margin: 0 0 10px; font-size: 16px; font-weight: 700; color: #172536; }
.epc-pf-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.epc-pf-table th, .epc-pf-table td { border: 1px solid #e1e7ef; padding: 8px; vertical-align: top; }
.epc-pf-table th { background: #f5f7fa; color: #64748b; }
.epc-pf-table tr.epc-pf-brand-row { cursor: pointer; }
.epc-pf-table tr.epc-pf-brand-row:hover { background: #f0f7ff; }
.epc-pf-table tr.epc-pf-brand-row.is-active { background: #dcecff; }
.epc-pf-loader, .epc-pf-msg { padding: 24px; text-align: center; color: #64748b; }
.epc-pf-msg { background: #fff8e1; border: 1px solid #f0d98a; border-radius: 8px; }
.epc-pf-hidden { display: none !important; }
.epc-pf-toolbar { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 12px; align-items: center; }
.epc-pf-filter { max-width: 280px; }
@media (max-width: 767px) {
	.epc-pf-searchbar { grid-template-columns: 1fr; }
}
</style>

<div class="epc-pf" id="epc-product-family"
	data-lang="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>"
	data-api="/content/shop/docpart/ajax_epc_product_family.php">
	<div id="epc-pf-main">
		<div class="epc-pf-loader" id="epc-pf-loading"><i class="fa fa-spinner fa-spin"></i> Loading product families…</div>
	</div>
</div>

<script>
(function () {
	var root = document.getElementById('epc-product-family');
	if (!root) { return; }
	var mainEl = document.getElementById('epc-pf-main');
	var api = root.getAttribute('data-api') || '';
	var langHref = root.getAttribute('data-lang') || '/en';
	var epcPfChpuOn = <?php echo $epc_pf_chpu_on ? 'true' : 'false'; ?>;
	var epcPfPartsUrl = <?php echo json_encode($epc_pf_chpu_parts_url, JSON_UNESCAPED_UNICODE); ?>;
	var epcPfBrandsUrl = <?php echo json_encode($epc_pf_chpu_brands_url, JSON_UNESCAPED_UNICODE); ?>;
	var epcPfSlash = <?php echo json_encode($epc_pf_chpu_slash_code, JSON_UNESCAPED_UNICODE); ?>;

	var state = {
		products: [],
		summary: null,
		selectedLabel: '',
		selectedBrand: '',
		groupDetail: null,
		filterText: ''
	};

	function esc(s) {
		return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
	}
	function normArticle(a) {
		return String(a || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
	}
	function shopUrl(brand, article) {
		var n = normArticle(article);
		var b = String(brand || '').trim();
		if (!b || !n) { return '#'; }
		if (epcPfChpuOn) {
			return langHref + '/' + epcPfPartsUrl + '/' + encodeURIComponent(b) + '/' + encodeURIComponent(n);
		}
		return langHref + '/shop/part_search?article=' + encodeURIComponent(n) + '&brand=' + encodeURIComponent(b);
	}
	function fetchJson(url, timeoutMs) {
		timeoutMs = timeoutMs || 120000;
		var ctrl = typeof AbortController !== 'undefined' ? new AbortController() : null;
		var timer = ctrl ? setTimeout(function () { ctrl.abort(); }, timeoutMs) : null;
		return fetch(url, { credentials: 'same-origin', signal: ctrl ? ctrl.signal : undefined })
			.then(function (r) {
				if (timer) { clearTimeout(timer); }
				if (!r.ok) {
					throw new Error('Server returned HTTP ' + r.status);
				}
				return r.json();
			})
			.catch(function (e) {
				if (timer) { clearTimeout(timer); }
				if (e && e.name === 'AbortError') {
					throw new Error('Catalog request timed out. Please refresh the page.');
				}
				throw e;
			});
	}
	function topBrandsPreview(p, limit) {
		limit = limit || 10;
		var list = (p.brands_top && p.brands_top.length) ? p.brands_top.slice() : (p.brands || []).slice();
		if (!p.brands_top || !p.brands_top.length) {
			list.sort(function (a, b) { return (parseFloat(b.total_qty) || 0) - (parseFloat(a.total_qty) || 0); });
			list = list.slice(0, limit);
		}
		var text = list.map(function (b) { return esc(b.brand); }).join(', ');
		var more = typeof p.brands_more_count === 'number' ? p.brands_more_count : Math.max(0, (p.brands || []).length - list.length);
		if (more > 0) {
			text += ' <span class="epc-pf-more-brands">+' + more + ' more</span>';
		}
		return text;
	}
	function renderProductGrid() {
		var products = state.products || [];
		if (!products.length) {
			return '<div class="epc-pf-msg">No in-stock parts found in the UAE price list.</div>';
		}
		return '<div class="epc-pf-panel" id="epc-pf-grid-block">'
			+ '<h2 class="epc-pf-section-title">Product families (UAE stock)</h2>'
			+ '<p class="epc-pf-lead">Top 10 brands by UAE quantity on each card. Click to open — full brand list and articles inside.</p>'
			+ '<div class="epc-pf-toolbar">'
			+ '<input type="text" class="form-control epc-pf-filter" id="epc-pf-grid-filter" placeholder="Filter families…">'
			+ '<button type="button" class="btn btn-default btn-sm" id="epc-pf-refresh"><i class="fa fa-refresh"></i> Refresh catalog</button>'
			+ '</div>'
			+ '<div class="epc-pf-grid">' + products.map(function (p, i) {
				var brandsLine = topBrandsPreview(p, 10);
				var samples = (p.samples || []).map(function (s) { return esc(s.brand) + ' ' + esc(s.article); }).join(', ');
				var search = [p.label, brandsLine, samples].join(' ');
				var active = state.selectedLabel === p.label ? ' is-active' : '';
				return '<div class="epc-pf-product-card' + active + '" data-product-i="' + i + '" data-search="' + esc(search) + '" role="button" tabindex="0">'
					+ '<strong>' + esc(p.label || 'Other') + '</strong>'
					+ '<small>' + esc(p.parts_count || 0) + ' part line(s) · qty ' + esc(Math.round(p.total_qty || 0)) + '</small>'
					+ (brandsLine ? '<small class="epc-pf-brands-line">Brands: ' + brandsLine + '</small>' : (samples ? '<small>e.g. ' + samples + '</small>' : ''))
					+ '<span class="epc-pf-product-card__hint">Click to open →</span></div>';
			}).join('') + '</div></div>';
	}
	function renderGroupDetail() {
		var g = state.groupDetail;
		if (!g) { return ''; }
		var brands = g.brands || [];
		var parts = g.parts || [];
		var brandActive = state.selectedBrand;
		var html = '<div class="epc-pf-panel" id="epc-pf-detail">'
			+ '<div class="epc-pf-toolbar">'
			+ '<button type="button" class="btn btn-default btn-sm" id="epc-pf-back-families"><i class="fa fa-arrow-left"></i> All families</button>'
			+ (brandActive ? '<button type="button" class="btn btn-default btn-sm" id="epc-pf-back-brands"><i class="fa fa-arrow-left"></i> All brands in ' + esc(g.label) + '</button>' : '')
			+ '</div>'
			+ '<h2 class="epc-pf-section-title">' + esc(g.label) + '</h2>'
			+ '<p class="epc-pf-lead">' + esc(g.parts_count || parts.length) + ' part line(s) · UAE qty <strong>' + esc(Math.round(g.total_qty || 0)) + '</strong></p>';

		if (!brandActive && brands.length) {
			html += '<h3 class="epc-pf-section-title">Brands in this family — click a row</h3>'
				+ '<table class="epc-pf-table"><thead><tr><th>Brand</th><th>Part lines</th><th>UAE qty</th></tr></thead><tbody>'
				+ brands.map(function (b) {
					return '<tr class="epc-pf-brand-row" data-brand="' + esc(b.brand) + '"><td><strong>' + esc(b.brand) + '</strong></td>'
						+ '<td>' + esc(b.parts_count || 0) + '</td><td>' + esc(Math.round(b.total_qty || 0)) + '</td></tr>';
				}).join('') + '</tbody></table>';
		}

		var showParts = brandActive ? parts.filter(function (p) {
			return String(p.brand || '').toUpperCase() === String(brandActive).toUpperCase();
		}) : parts;

		if (brandActive || showParts.length) {
			html += '<h3 class="epc-pf-section-title">' + (brandActive ? 'Articles — ' + esc(brandActive) : 'All parts in this family') + '</h3>'
				+ '<input type="text" class="form-control epc-pf-filter" id="epc-pf-parts-filter" placeholder="Filter by article or name…" style="margin-bottom:10px;">'
				+ '<table class="epc-pf-table"><thead><tr><th>Brand</th><th>Article</th><th>Name</th><th>Qty</th><th></th></tr></thead><tbody id="epc-pf-parts-body">'
				+ showParts.map(function (p) {
					var search = [p.brand, p.article, p.name].join(' ');
					return '<tr data-search="' + esc(search) + '"><td><strong>' + esc(p.brand) + '</strong></td>'
						+ '<td><a href="' + esc(shopUrl(p.brand, p.article)) + '">' + esc(p.article) + '</a></td>'
						+ '<td>' + esc(p.name) + '</td><td>' + esc(p.qty) + '</td>'
						+ '<td><a class="btn btn-xs btn-primary" href="' + esc(shopUrl(p.brand, p.article)) + '">Open</a></td></tr>';
				}).join('') + '</tbody></table>';
		}
		html += '</div>';
		return html;
	}
	function renderMain() {
		var html = '';
		if (state.selectedLabel && state.groupDetail) {
			html += renderGroupDetail();
		} else {
			html += renderProductGrid();
		}
		mainEl.innerHTML = html;
		bindMain();
	}
	function bindGridFilter() {
		var inp = document.getElementById('epc-pf-grid-filter');
		if (!inp) { return; }
		inp.oninput = function () {
			var q = String(inp.value || '').toLowerCase();
			Array.prototype.forEach.call(document.querySelectorAll('.epc-pf-product-card'), function (card) {
				var hay = (card.getAttribute('data-search') || '').toLowerCase();
				card.style.display = (!q || hay.indexOf(q) !== -1) ? '' : 'none';
			});
		};
	}
	function bindPartsFilter() {
		var inp = document.getElementById('epc-pf-parts-filter');
		if (!inp) { return; }
		inp.oninput = function () {
			var q = String(inp.value || '').toLowerCase();
			Array.prototype.forEach.call(document.querySelectorAll('#epc-pf-parts-body tr'), function (tr) {
				var hay = (tr.getAttribute('data-search') || '').toLowerCase();
				tr.style.display = (!q || hay.indexOf(q) !== -1) ? '' : 'none';
			});
		};
	}
	function openFamily(label) {
		state.selectedLabel = label;
		state.selectedBrand = '';
		state.groupDetail = null;
		mainEl.innerHTML = '<div class="epc-pf-loader"><i class="fa fa-spinner fa-spin"></i> Loading ' + esc(label) + '…</div>';
		fetchJson(api + '?action=group&label=' + encodeURIComponent(label))
			.then(function (d) {
				if (!d || !d.status || !d.group) {
					throw new Error((d && d.message) || 'Could not load family');
				}
				state.groupDetail = d.group;
				renderMain();
			})
			.catch(function (e) {
				mainEl.innerHTML = '<div class="epc-pf-msg">' + esc(e.message || 'Load failed') + '</div>';
			});
	}
	function openBrand(brand) {
		state.selectedBrand = brand;
		renderMain();
	}
	function bindMain() {
		var refresh = document.getElementById('epc-pf-refresh');
		if (refresh) {
			refresh.onclick = function () { loadCatalog(true); };
		}
		bindGridFilter();
		bindPartsFilter();
		Array.prototype.forEach.call(document.querySelectorAll('.epc-pf-product-card'), function (card) {
			function go() {
				var i = parseInt(card.getAttribute('data-product-i'), 10);
				var p = state.products[i];
				if (p) { openFamily(p.label); }
			}
			card.onclick = go;
			card.onkeydown = function (e) {
				if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); go(); }
			};
		});
		var backFam = document.getElementById('epc-pf-back-families');
		if (backFam) {
			backFam.onclick = function () {
				state.selectedLabel = '';
				state.selectedBrand = '';
				state.groupDetail = null;
				renderMain();
			};
		}
		var backBrands = document.getElementById('epc-pf-back-brands');
		if (backBrands) {
			backBrands.onclick = function () {
				state.selectedBrand = '';
				renderMain();
			};
		}
		Array.prototype.forEach.call(document.querySelectorAll('.epc-pf-brand-row'), function (tr) {
			tr.onclick = function () {
				openBrand(tr.getAttribute('data-brand') || '');
			};
		});
	}
	function loadCatalog(refresh) {
		mainEl.innerHTML = '<div class="epc-pf-loader" id="epc-pf-loading"><i class="fa fa-spinner fa-spin"></i> Loading product families…</div>';
		var slowMsg = setTimeout(function () {
			var el = document.getElementById('epc-pf-loading');
			if (el) {
				el.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Loading product families… (large UAE catalog, please wait up to 60 seconds)';
			}
		}, 5000);
		var url = api + '?action=summary';
		if (refresh) { url += '&refresh=1'; }
		fetchJson(url)
			.then(function (d) {
				clearTimeout(slowMsg);
				if (!d || !d.status) {
					throw new Error((d && d.message) || 'Catalog unavailable');
				}
				state.products = d.products || [];
				state.summary = d.summary || null;
				state.selectedLabel = '';
				state.selectedBrand = '';
				state.groupDetail = null;
				renderMain();
			})
			.catch(function (e) {
				clearTimeout(slowMsg);
				mainEl.innerHTML = '<div class="epc-pf-msg">' + esc(e.message || 'Failed to load catalog') + '</div>';
			});
	}
	try {
		var qs = new URLSearchParams(window.location.search);
		if (qs.get('brand') && qs.get('article')) {
			window.location.href = shopUrl(String(qs.get('brand')), String(qs.get('article')));
		} else if (qs.get('family')) {
			loadCatalog(false);
			setTimeout(function () { openFamily(String(qs.get('family'))); }, 400);
			return;
		}
	} catch (eQs) { /* ignore */ }
	loadCatalog(false);
})();
</script>
