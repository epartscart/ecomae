<?php
/**
 * ePartsCart — Accessories & Spare Parts marketplace hub.
 * PakWheels-style browse UX (filters, sort, list/grid, category/make/region facets)
 * powered by UAE warehouse stock — not third-party ad scrapes.
 */
defined('_ASTEXE_') or die('No access');

$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '')
	? rtrim((string) $multilang_params['lang_href'], '/')
	: '/en';
$epc_acc_ver = '20260718acc1';
$epc_acc_chpu_on = !empty($DP_Config->chpu_search_config['chpu_search_on']);
$epc_acc_parts_url = !empty($DP_Config->chpu_search_config['level_1']['url'])
	? (string) $DP_Config->chpu_search_config['level_1']['url']
	: 'parts';
?>
<link rel="stylesheet" href="/content/general_pages/epc_accessories.css?v=<?php echo rawurlencode($epc_acc_ver); ?>">

<section class="epc-acc" id="epc-accessories"
	data-api="/content/shop/docpart/ajax_epc_accessories_search.php"
	data-lang="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>"
	data-parts-url="<?php echo htmlspecialchars($epc_acc_parts_url, ENT_QUOTES, 'UTF-8'); ?>"
	data-chpu="<?php echo !empty($epc_acc_chpu_on) ? '1' : '0'; ?>">

	<div class="epc-acc__hero">
		<div class="container">
			<div class="epc-acc__brand">eParts<span>Cart</span></div>
			<h1>Car spare parts &amp; accessories</h1>
			<p>Browse UAE warehouse stock by category, brand, and region — sort by price or availability, then open live prices for the exact part number.</p>
			<div class="epc-acc__hero-cta">
				<form class="epc-acc__search" id="epc-acc-search-form" role="search">
					<label class="sr-only" for="epc-acc-q">Search accessories</label>
					<input id="epc-acc-q" name="q" type="search" placeholder="Search part name, brand, or article — e.g. oil filter, NGK, C110J" maxlength="80" />
					<button type="submit"><i class="fa fa-search" aria-hidden="true"></i> Search</button>
				</form>
				<a class="epc-acc__btn epc-acc__btn--ghost" href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>/parts">Brand + article prices</a>
			</div>
		</div>
	</div>

	<div class="container">
		<div class="epc-acc__layout">
			<aside class="epc-acc__side" aria-label="Filters">
				<div class="epc-acc__facet">
					<h3>Categories</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-cats"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Sub categories</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-subs"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Make / brand</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-brands"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Warehouse region</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-regions"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Price range (AED)</h3>
					<div class="epc-acc__price-row">
						<input type="number" id="epc-acc-price-min" min="0" step="1" placeholder="Min" />
						<input type="number" id="epc-acc-price-max" min="0" step="1" placeholder="Max" />
					</div>
					<button type="button" class="epc-acc__btn" id="epc-acc-apply-price">Apply price</button>
					<button type="button" class="epc-acc__btn" id="epc-acc-reset" style="background:#334155;margin-top:8px">Reset filters</button>
				</div>
			</aside>

			<div class="epc-acc__main">
				<div class="epc-acc__toolbar">
					<div class="epc-acc__count" id="epc-acc-count">Loading results…</div>
					<div class="epc-acc__tools">
						<label>
							<span class="sr-only">Sort by</span>
							<select id="epc-acc-sort" aria-label="Sort by">
								<option value="price-desc">Price: High to Low</option>
								<option value="price-asc">Price: Low to High</option>
								<option value="qty-desc">Top stock / sales</option>
								<option value="name-asc">Name A–Z</option>
								<option value="updated-desc">Recently stocked</option>
							</select>
						</label>
						<div class="epc-acc__view-toggle" role="group" aria-label="View mode">
							<button type="button" id="epc-acc-view-grid" class="is-active" title="Grid"><i class="fa fa-th" aria-hidden="true"></i></button>
							<button type="button" id="epc-acc-view-list" title="List"><i class="fa fa-list" aria-hidden="true"></i></button>
						</div>
					</div>
				</div>
				<div id="epc-acc-results" class="epc-acc__grid" aria-live="polite"></div>
				<div class="epc-acc__pager" id="epc-acc-pager"></div>
			</div>
		</div>

		<div class="epc-acc__browse">
			<div class="epc-acc__browse-block">
				<h2>Browse by category</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-cats"></div>
			</div>
			<div class="epc-acc__browse-block">
				<h2>Browse by make</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-brands"></div>
			</div>
			<div class="epc-acc__browse-block">
				<h2>Browse by warehouse region</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-regions"></div>
			</div>
			<div class="epc-acc__trust">
				<div class="epc-acc__trust-item"><strong>UAE warehouse stock</strong><span>Live quantities from mapped price lists — not classified ads.</span></div>
				<div class="epc-acc__trust-item"><strong>Brand + article prices</strong><span>Open any card to see full warehouse offers and crosses.</span></div>
				<div class="epc-acc__trust-item"><strong>Secure checkout</strong><span>Quote or cart flows through your ePartsCart account.</span></div>
				<div class="epc-acc__trust-item"><strong>GCC delivery</strong><span>Regional shipping options shown on the order flow.</span></div>
			</div>
		</div>
	</div>
</section>

<script>
(function () {
	var root = document.getElementById('epc-accessories');
	if (!root) { return; }

	var api = root.getAttribute('data-api') || '';
	var lang = root.getAttribute('data-lang') || '/en';
	var partsUrl = root.getAttribute('data-parts-url') || 'parts';
	var chpuOn = root.getAttribute('data-chpu') === '1';

	var els = {
		q: document.getElementById('epc-acc-q'),
		form: document.getElementById('epc-acc-search-form'),
		cats: document.getElementById('epc-acc-cats'),
		subs: document.getElementById('epc-acc-subs'),
		brands: document.getElementById('epc-acc-brands'),
		regions: document.getElementById('epc-acc-regions'),
		priceMin: document.getElementById('epc-acc-price-min'),
		priceMax: document.getElementById('epc-acc-price-max'),
		applyPrice: document.getElementById('epc-acc-apply-price'),
		reset: document.getElementById('epc-acc-reset'),
		sort: document.getElementById('epc-acc-sort'),
		count: document.getElementById('epc-acc-count'),
		results: document.getElementById('epc-acc-results'),
		pager: document.getElementById('epc-acc-pager'),
		viewGrid: document.getElementById('epc-acc-view-grid'),
		viewList: document.getElementById('epc-acc-view-list'),
		browseCats: document.getElementById('epc-acc-browse-cats'),
		browseBrands: document.getElementById('epc-acc-browse-brands'),
		browseRegions: document.getElementById('epc-acc-browse-regions')
	};

	var state = {
		q: '',
		category: '',
		subcategory: '',
		brand: '',
		region: '',
		price_min: '',
		price_max: '',
		sort: 'price-desc',
		page: 1,
		view: 'grid',
		prices_visible: true,
		currency: 'AED',
		lastFacets: null,
		taxonomy: []
	};

	function esc(s) {
		return String(s == null ? '' : s)
			.replace(/&/g, '&amp;').replace(/</g, '&lt;')
			.replace(/>/g, '&gt;').replace(/"/g, '&quot;');
	}
	function productUrl(brand, article) {
		var b = String(brand || '').trim();
		var a = String(article || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
		if (!b || !a) { return '#'; }
		if (chpuOn) {
			return lang + '/' + partsUrl + '/' + encodeURIComponent(b) + '/' + encodeURIComponent(a);
		}
		return lang + '/shop/part_search?article=' + encodeURIComponent(a) + '&brand=' + encodeURIComponent(b);
	}
	function money(amount) {
		var n = Number(amount);
		if (!isFinite(n)) { return '—'; }
		return state.currency + ' ' + n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
	}
	function syncUrl() {
		try {
			var u = new URL(window.location.href);
			var keys = ['q','category','subcategory','brand','region','price_min','price_max','sort','page'];
			keys.forEach(function (k) {
				var v = state[k];
				if (v && String(v) !== '' && !(k === 'page' && String(v) === '1') && !(k === 'sort' && v === 'price-desc')) {
					u.searchParams.set(k, v);
				} else {
					u.searchParams.delete(k);
				}
			});
			window.history.replaceState({}, '', u.pathname + u.search);
		} catch (e) {}
	}
	function readUrl() {
		try {
			var u = new URL(window.location.href);
			['q','category','subcategory','brand','region','price_min','price_max','sort','page'].forEach(function (k) {
				if (u.searchParams.has(k)) {
					state[k] = u.searchParams.get(k) || '';
				}
			});
			if (els.q) { els.q.value = state.q; }
			if (els.sort) { els.sort.value = state.sort || 'price-desc'; }
			if (els.priceMin) { els.priceMin.value = state.price_min; }
			if (els.priceMax) { els.priceMax.value = state.price_max; }
		} catch (e) {}
	}

	function facetButtons(target, rows, keyName, labelKey, activeValue, onClick) {
		if (!target) { return; }
		if (!rows || !rows.length) {
			target.innerHTML = '<li style="padding:8px 10px;color:#94a3b8;font-size:13px">No options</li>';
			return;
		}
		target.innerHTML = rows.map(function (row) {
			var value = row[keyName];
			var label = row[labelKey] || value;
			var count = row.count != null ? row.count : '';
			var active = String(value) === String(activeValue) ? ' is-active' : '';
			return '<li><button type="button" class="' + active + '" data-value="' + esc(value) + '">'
				+ '<span>' + esc(label) + '</span>'
				+ (count !== '' ? '<span class="count">' + esc(count) + '</span>' : '')
				+ '</button></li>';
		}).join('');
		Array.prototype.forEach.call(target.querySelectorAll('button'), function (btn) {
			btn.addEventListener('click', function () {
				onClick(btn.getAttribute('data-value') || '');
			});
		});
	}

	function renderFacets(facets) {
		state.lastFacets = facets || {};
		var cats = (facets && facets.categories) || [];
		facetButtons(els.cats, [{ slug: '', label: 'All categories', count: '' }].concat(cats.map(function (c) {
			return { slug: c.slug, label: c.label, count: c.count };
		})), 'slug', 'label', state.category, function (v) {
			state.category = (state.category === v) ? '' : v;
			state.subcategory = '';
			state.page = 1;
			load();
		});

		var subs = [];
		cats.forEach(function (c) {
			if (state.category && c.slug !== state.category) { return; }
			(c.subs || []).forEach(function (s) {
				subs.push({ slug: s.slug, label: s.label, count: s.count });
			});
		});
		facetButtons(els.subs, [{ slug: '', label: 'All sub categories', count: '' }].concat(subs), 'slug', 'label', state.subcategory, function (v) {
			state.subcategory = (state.subcategory === v) ? '' : v;
			state.page = 1;
			load();
		});

		var brands = (facets && facets.brands) || [];
		facetButtons(els.brands, [{ brand: '', label: 'All brands', count: '' }].concat(brands.map(function (b) {
			return { brand: b.brand, label: b.brand, count: b.count };
		})), 'brand', 'label', state.brand, function (v) {
			state.brand = (state.brand === v) ? '' : v;
			state.page = 1;
			load();
		});

		var regions = (facets && facets.regions) || [];
		facetButtons(els.regions, [{ region: '', label: 'All regions', count: '' }].concat(regions.map(function (r) {
			return { region: r.region, label: r.region, count: r.count };
		})), 'region', 'label', state.region, function (v) {
			state.region = (state.region === v) ? '' : v;
			state.page = 1;
			load();
		});

		if (els.browseCats) {
			els.browseCats.innerHTML = (state.taxonomy || []).map(function (c) {
				return '<button type="button" data-cat="' + esc(c.slug) + '">' + esc(c.label) + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseCats.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.category = btn.getAttribute('data-cat') || '';
					state.subcategory = '';
					state.page = 1;
					load();
					root.scrollIntoView({ behavior: 'smooth', block: 'start' });
				});
			});
		}
		if (els.browseBrands) {
			els.browseBrands.innerHTML = brands.slice(0, 20).map(function (b) {
				return '<button type="button" data-brand="' + esc(b.brand) + '">' + esc(b.brand) + ' Parts (' + esc(b.count) + '+)</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseBrands.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.brand = btn.getAttribute('data-brand') || '';
					state.page = 1;
					load();
					root.scrollIntoView({ behavior: 'smooth', block: 'start' });
				});
			});
		}
		if (els.browseRegions) {
			els.browseRegions.innerHTML = regions.map(function (r) {
				return '<button type="button" data-region="' + esc(r.region) + '">Parts in ' + esc(r.region) + ' (' + esc(r.count) + '+)</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseRegions.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.region = btn.getAttribute('data-region') || '';
					state.page = 1;
					load();
					root.scrollIntoView({ behavior: 'smooth', block: 'start' });
				});
			});
		}
	}

	function renderCards(items) {
		if (!els.results) { return; }
		if (!items || !items.length) {
			els.results.innerHTML = '<div class="epc-acc__empty"><strong>No accessories found</strong><br>Try another keyword, brand, or clear filters.</div>';
			return;
		}
		els.results.innerHTML = items.map(function (item, idx) {
			var href = productUrl(item.brand, item.article);
			var mark = String(item.brand || '?').slice(0, 3);
			var priceHtml = state.prices_visible
				? ('<div class="epc-acc__price" data-epc-base-price="' + esc(item.price) + '">' + esc(money(item.price)) + '</div>')
				: '<div class="epc-acc__price"><a href="' + esc(lang) + '/users/login">Log in for prices</a></div>';
			return '<article class="epc-acc__card" style="animation-delay:' + (Math.min(idx, 8) * 0.03) + 's">'
				+ '<div class="epc-acc__media">'
				+ '<span class="epc-acc__media-badge">' + esc(item.category_label || 'Parts') + '</span>'
				+ '<span class="epc-acc__media-mark">' + esc(mark) + '</span>'
				+ '</div>'
				+ '<div class="epc-acc__body">'
				+ '<div class="epc-acc__meta"><span>' + esc(item.brand) + '</span><span>' + esc(item.subcategory_label || item.category_label) + '</span></div>'
				+ '<h3 class="epc-acc__title"><a href="' + esc(href) + '">' + esc(item.name) + '</a></h3>'
				+ '<p class="epc-acc__article">' + esc(item.article) + (item.warehouse ? (' · ' + esc(item.warehouse)) : '') + '</p>'
				+ '<div class="epc-acc__price-row-card">' + priceHtml
				+ '<div class="epc-acc__stock">' + esc(parseInt(item.qty, 10) || 0) + ' in stock</div></div>'
				+ '<div class="epc-acc__actions">'
				+ '<a class="primary" href="' + esc(href) + '">Open prices</a>'
				+ '<a class="secondary" href="' + esc(href) + '">View details</a>'
				+ '</div></div></article>';
		}).join('');
	}

	function renderPager(page, pages) {
		if (!els.pager) { return; }
		if (pages <= 1) {
			els.pager.innerHTML = '';
			return;
		}
		var html = '';
		var start = Math.max(1, page - 2);
		var end = Math.min(pages, page + 2);
		if (page > 1) {
			html += '<button type="button" data-page="' + (page - 1) + '">Prev</button>';
		}
		for (var p = start; p <= end; p++) {
			html += '<button type="button" class="' + (p === page ? 'is-active' : '') + '" data-page="' + p + '">' + p + '</button>';
		}
		if (page < pages) {
			html += '<button type="button" data-page="' + (page + 1) + '">Next</button>';
		}
		els.pager.innerHTML = html;
		Array.prototype.forEach.call(els.pager.querySelectorAll('button'), function (btn) {
			btn.addEventListener('click', function () {
				state.page = parseInt(btn.getAttribute('data-page'), 10) || 1;
				load();
				els.results.scrollIntoView({ behavior: 'smooth', block: 'start' });
			});
		});
	}

	function load() {
		if (!api || !els.results) { return; }
		els.results.innerHTML = '<div class="epc-acc__loading"><i class="fa fa-spinner fa-spin"></i> Loading accessories…</div>';
		els.count.textContent = 'Loading results…';
		syncUrl();
		var params = new URLSearchParams();
		['q','category','subcategory','brand','region','price_min','price_max','sort','page'].forEach(function (k) {
			if (state[k] !== '' && state[k] != null) { params.set(k, state[k]); }
		});
		params.set('per_page', '24');
		fetch(api + '?' + params.toString(), { credentials: 'same-origin', cache: 'no-store' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (!data || !data.status) {
					els.results.innerHTML = '<div class="epc-acc__empty">Could not load accessories catalog.</div>';
					els.count.textContent = '0 results';
					return;
				}
				state.prices_visible = !!data.prices_visible;
				state.currency = data.currency || 'AED';
				state.taxonomy = data.taxonomy || state.taxonomy || [];
				els.count.innerHTML = '<strong>' + esc(data.from) + ' – ' + esc(data.to) + '</strong> of <strong>' + esc(data.total) + '</strong> results';
				renderFacets(data.facets || {});
				renderCards(data.items || []);
				renderPager(data.page || 1, data.pages || 1);
			})
			.catch(function () {
				els.results.innerHTML = '<div class="epc-acc__empty">Network error loading accessories.</div>';
			});
	}

	if (els.form) {
		els.form.addEventListener('submit', function (e) {
			e.preventDefault();
			state.q = els.q ? els.q.value.trim() : '';
			state.page = 1;
			load();
		});
	}
	if (els.sort) {
		els.sort.addEventListener('change', function () {
			state.sort = els.sort.value || 'price-desc';
			state.page = 1;
			load();
		});
	}
	if (els.applyPrice) {
		els.applyPrice.addEventListener('click', function () {
			state.price_min = els.priceMin ? els.priceMin.value : '';
			state.price_max = els.priceMax ? els.priceMax.value : '';
			state.page = 1;
			load();
		});
	}
	if (els.reset) {
		els.reset.addEventListener('click', function () {
			state = {
				q: '', category: '', subcategory: '', brand: '', region: '',
				price_min: '', price_max: '', sort: 'price-desc', page: 1,
				view: state.view, prices_visible: state.prices_visible, currency: state.currency,
				lastFacets: null, taxonomy: state.taxonomy
			};
			if (els.q) { els.q.value = ''; }
			if (els.sort) { els.sort.value = 'price-desc'; }
			if (els.priceMin) { els.priceMin.value = ''; }
			if (els.priceMax) { els.priceMax.value = ''; }
			load();
		});
	}
	function setView(mode) {
		state.view = mode;
		if (els.results) {
			els.results.classList.toggle('is-list', mode === 'list');
		}
		if (els.viewGrid) { els.viewGrid.classList.toggle('is-active', mode === 'grid'); }
		if (els.viewList) { els.viewList.classList.toggle('is-active', mode === 'list'); }
	}
	if (els.viewGrid) { els.viewGrid.addEventListener('click', function () { setView('grid'); }); }
	if (els.viewList) { els.viewList.addEventListener('click', function () { setView('list'); }); }

	readUrl();
	setView(state.view || 'grid');
	load();
})();
</script>
