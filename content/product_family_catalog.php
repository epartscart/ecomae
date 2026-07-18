<?php
defined('_ASTEXE_') or die('No access');
$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') ? rtrim($multilang_params['lang_href'], '/') : '/en';
$epc_pf_chpu_on = !empty($DP_Config->chpu_search_config['chpu_search_on']);
$epc_pf_chpu_parts_url = !empty($DP_Config->chpu_search_config['level_1']['url']) ? $DP_Config->chpu_search_config['level_1']['url'] : 'parts';
$epc_pf_chpu_brands_url = !empty($DP_Config->chpu_search_config['level_2']['mode_1']['url']) ? $DP_Config->chpu_search_config['level_2']['mode_1']['url'] : 'brands';
$epc_pf_chpu_slash_code = isset($DP_Config->chpu_search_config['slash_code']) ? $DP_Config->chpu_search_config['slash_code'] : '%2F';
$epc_pf_theme_ver = '20260718pfCat1';
?>
<link rel="stylesheet" href="/content/general_pages/epc_vc_catalog.css?v=<?php echo rawurlencode($epc_pf_theme_ver); ?>">
<link rel="stylesheet" href="/content/general_pages/epc_car_mod_theme.css?v=<?php echo rawurlencode($epc_pf_theme_ver); ?>">
<style>
/* Compact drill-down tables keep working under the car-mod category shell */
.epc-pf-cat-page .epc-pf-panel {
	background: #fff;
	border: 1px solid #e2e8f0;
	border-radius: 12px;
	margin-top: 12px;
	padding: 14px 16px;
}
.epc-pf-cat-page .epc-pf-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.epc-pf-cat-page .epc-pf-table th,
.epc-pf-cat-page .epc-pf-table td { border: 1px solid #e1e7ef; padding: 8px; vertical-align: top; }
.epc-pf-cat-page .epc-pf-table th { background: #f5f7fa; color: #64748b; }
.epc-pf-cat-page .epc-pf-brand-row { cursor: pointer; }
.epc-pf-cat-page .epc-pf-brand-row:hover { background: #fff7ed; }
.epc-pf-cat-page .epc-pf-brand-row.is-active { background: #ffedd5; }
.epc-pf-cat-page .epc-pf-loader,
.epc-pf-cat-page .epc-pf-msg { padding: 28px; text-align: center; color: #64748b; }
.epc-pf-cat-page .epc-pf-msg { background: #fff8e1; border: 1px solid #f0d98a; border-radius: 10px; }
.epc-pf-cat-page .epc-pf-section-title { margin: 0 0 10px; font-size: 16px; font-weight: 800; color: #0f172a; }
.epc-pf-cat-page .epc-pf-lead { margin: 0 0 12px; color: #64748b; font-size: 13px; }
.epc-pf-cat-page .epc-pf-toolbar { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 12px; align-items: center; }
.epc-pf-cat-page .epc-vc-cat-card small {
	color: #64748b;
	display: block;
	font-size: 11px;
	font-weight: 600;
	margin-top: 4px;
}
</style>

<div class="epc-pf-cat-page epc-cm" id="epc-product-family"
	data-lang="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>"
	data-api="/content/shop/docpart/ajax_epc_product_family.php"
	data-icon-base="/content/files/epc-cata/category-icons/">
	<div id="epc-pf-main">
		<div class="epc-pf-loader" id="epc-pf-loading"><i class="fa fa-spinner fa-spin"></i> Loading product families…</div>
	</div>
</div>

<script>
(function () {
	var root = document.getElementById('epc-product-family');
	if (!root) { return; }
	try {
		if (/\/product-family(?:\/|$|\?)/i.test(window.location.pathname || '')) {
			document.documentElement.classList.add('epc-pf-cat-active');
		}
	} catch (eActive) { /* ignore */ }

	var mainEl = document.getElementById('epc-pf-main');
	var api = root.getAttribute('data-api') || '';
	var langHref = root.getAttribute('data-lang') || '/en';
	var iconBase = (root.getAttribute('data-icon-base') || '/content/files/epc-cata/category-icons/').replace(/\/?$/, '/');
	var epcPfChpuOn = <?php echo $epc_pf_chpu_on ? 'true' : 'false'; ?>;
	var epcPfPartsUrl = <?php echo json_encode($epc_pf_chpu_parts_url, JSON_UNESCAPED_UNICODE); ?>;
	var epcPfBrandsUrl = <?php echo json_encode($epc_pf_chpu_brands_url, JSON_UNESCAPED_UNICODE); ?>;
	var epcPfSlash = <?php echo json_encode($epc_pf_chpu_slash_code, JSON_UNESCAPED_UNICODE); ?>;

	var ICON_MAP = [
		{ re: /cabin.?air|pollen/i, id: 2, fa: 'fa-filter' },
		{ re: /oil.?filter/i, id: 2, fa: 'fa-filter' },
		{ re: /filter/i, id: 2, fa: 'fa-filter' },
		{ re: /brake/i, id: 5, fa: 'fa-stop-circle' },
		{ re: /spark|glow|ignition/i, id: 10, fa: 'fa-bolt' },
		{ re: /timing.?belt|belt/i, id: 8, fa: 'fa-circle-o' },
		{ re: /fuel.?pump|fuel/i, id: 17, fa: 'fa-tint' },
		{ re: /radiat|coolant|cool/i, id: 19, fa: 'fa-thermometer-half' },
		{ re: /ball.?joint|propshaft|joint|bearing/i, id: 4, fa: 'fa-compress' },
		{ re: /piston|gasket|engine/i, id: 3, fa: 'fa-cogs' },
		{ re: /other|uncategor/i, id: 1, fa: 'fa-wrench' }
	];

	var state = {
		products: [],
		summary: null,
		selectedLabel: '',
		selectedBrand: '',
		groupDetail: null
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
	function familyIcon(label) {
		var name = String(label || '');
		for (var i = 0; i < ICON_MAP.length; i++) {
			if (ICON_MAP[i].re.test(name)) {
				return ICON_MAP[i];
			}
		}
		return { id: 1, fa: 'fa-wrench' };
	}
	function iconHtml(label) {
		var meta = familyIcon(label);
		var fa = meta.fa || 'fa-wrench';
		if (meta.id > 0) {
			return '<img src="' + esc(iconBase + meta.id + '.png') + '" alt="" loading="lazy" decoding="async" onerror="this.parentNode.innerHTML=\'<i class=\\\'fa ' + fa + '\\\'></i>\'">';
		}
		return '<i class="fa ' + fa + '"></i>';
	}
	function fetchJson(url, timeoutMs) {
		timeoutMs = timeoutMs || 120000;
		var ctrl = typeof AbortController !== 'undefined' ? new AbortController() : null;
		var timer = ctrl ? setTimeout(function () { ctrl.abort(); }, timeoutMs) : null;
		return fetch(url, { credentials: 'same-origin', signal: ctrl ? ctrl.signal : undefined })
			.then(function (r) {
				if (timer) { clearTimeout(timer); }
				if (!r.ok) { throw new Error('Server returned HTTP ' + r.status); }
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
		limit = limit || 6;
		var list = (p.brands_top && p.brands_top.length) ? p.brands_top.slice() : (p.brands || []).slice();
		if (!p.brands_top || !p.brands_top.length) {
			list.sort(function (a, b) { return (parseFloat(b.total_qty) || 0) - (parseFloat(a.total_qty) || 0); });
			list = list.slice(0, limit);
		}
		return list.map(function (b) { return String(b.brand || ''); }).filter(Boolean).join(', ');
	}
	function renderProductGrid() {
		var products = state.products || [];
		if (!products.length) {
			return '<div class="epc-pf-msg">No in-stock parts found in the UAE price list.</div>';
		}
		var cards = products.map(function (p, i) {
			var brandsLine = topBrandsPreview(p, 6);
			var search = [p.label, brandsLine].join(' ');
			var active = state.selectedLabel === p.label ? ' is-active' : '';
			return '<button type="button" class="epc-vc-cat-card' + active + '" data-product-i="' + i + '" data-search="' + esc(search) + '">'
				+ '<div class="epc-vc-cat-icon">' + iconHtml(p.label) + '</div>'
				+ '<strong>' + esc(p.label || 'Other') + '</strong>'
				+ '<small>' + esc(p.parts_count || 0) + ' lines · qty ' + esc(Math.round(p.total_qty || 0)) + '</small>'
				+ '</button>';
		}).join('');
		return ''
			+ '<div class="epc-cm-catalog-head"><h1><em>Catalog</em> by category</h1></div>'
			+ '<div class="epc-cm-cat-filter">'
			+ '<i class="fa fa-search" aria-hidden="true"></i>'
			+ '<input type="search" class="form-control" id="epc-pf-grid-filter" placeholder="Search for a category." autocomplete="off">'
			+ '</div>'
			+ '<div class="epc-vc-cat-grid" id="epc-pf-cat-grid">' + cards + '</div>'
			+ '<div class="epc-cm-show-all-wrap">'
			+ '<button type="button" class="epc-cm-show-all-sections" id="epc-pf-refresh"><i class="fa fa-refresh"></i> Refresh catalog</button>'
			+ '</div>';
	}
	function renderGroupDetail() {
		var g = state.groupDetail;
		if (!g) { return ''; }
		var brands = g.brands || [];
		var parts = g.parts || [];
		var brandActive = state.selectedBrand;
		var html = '<div class="epc-pf-cat-backbar">'
			+ '<button type="button" class="btn btn-default btn-sm" id="epc-pf-back-families"><i class="fa fa-arrow-left"></i> All categories</button>'
			+ (brandActive ? '<button type="button" class="btn btn-default btn-sm" id="epc-pf-back-brands"><i class="fa fa-arrow-left"></i> All brands in ' + esc(g.label) + '</button>' : '')
			+ '<span class="epc-pf-cat-meta">' + esc(g.parts_count || parts.length) + ' part line(s) · UAE qty ' + esc(Math.round(g.total_qty || 0)) + '</span>'
			+ '</div>'
			+ '<div class="epc-cm-catalog-head"><h1><em>' + esc(g.label) + '</em></h1></div>'
			+ '<div class="epc-pf-panel">';

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
				+ '<input type="search" class="form-control" id="epc-pf-parts-filter" placeholder="Filter by article or name…" style="margin-bottom:10px;max-width:360px;">'
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
		mainEl.innerHTML = (state.selectedLabel && state.groupDetail) ? renderGroupDetail() : renderProductGrid();
		bindMain();
	}
	function bindGridFilter() {
		var inp = document.getElementById('epc-pf-grid-filter');
		if (!inp) { return; }
		inp.oninput = function () {
			var q = String(inp.value || '').toLowerCase();
			Array.prototype.forEach.call(document.querySelectorAll('#epc-pf-cat-grid .epc-vc-cat-card'), function (card) {
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
		Array.prototype.forEach.call(document.querySelectorAll('#epc-pf-cat-grid .epc-vc-cat-card'), function (card) {
			card.onclick = function () {
				var i = parseInt(card.getAttribute('data-product-i'), 10);
				var p = state.products[i];
				if (p) { openFamily(p.label); }
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
				el.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Loading product families… (large UAE catalog, please wait)';
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
			return;
		}
		if (qs.get('family')) {
			loadCatalog(false);
			setTimeout(function () { openFamily(String(qs.get('family'))); }, 400);
			return;
		}
	} catch (eQs) { /* ignore */ }
	loadCatalog(false);
})();
</script>
