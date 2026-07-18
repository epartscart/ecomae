<?php
/**
 * ePartsCart — Accessories & Spare Parts marketplace (PakWheels-style categories/filters).
 * Categories crawled from PakWheels IA. Listings are stored in epc_acc_listings and filled over time.
 */
defined('_ASTEXE_') or die('No access');

$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '')
	? rtrim((string) $multilang_params['lang_href'], '/')
	: '/en';
$epc_acc_ver = '20260718accPw2';
?>
<link rel="stylesheet" href="/content/general_pages/epc_accessories.css?v=<?php echo rawurlencode($epc_acc_ver); ?>">

<section class="epc-acc" id="epc-accessories"
	data-api="/content/shop/docpart/ajax_epc_accessories_search.php"
	data-lang="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>">

	<div class="epc-acc__hero">
		<div class="container">
			<div class="epc-acc__brand">eParts<span>Cart</span></div>
			<h1>Car spare parts and accessories</h1>
			<p>PakWheels-style category browse and filters. Categories are ready — listings are added into each category over time.</p>
			<div class="epc-acc__hero-cta">
				<form class="epc-acc__search" id="epc-acc-search-form" role="search">
					<label class="sr-only" for="epc-acc-q">Search accessories</label>
					<input id="epc-acc-q" name="q" type="search" placeholder="Search accessories and spare parts" maxlength="80" />
					<button type="submit"><i class="fa fa-search" aria-hidden="true"></i> Search</button>
				</form>
			</div>
		</div>
	</div>

	<div class="container">
		<div class="epc-acc__layout">
			<aside class="epc-acc__side" aria-label="Filters">
				<div class="epc-acc__facet">
					<h3>Category</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-cats"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Sub category</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-subs"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Make</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-makes"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Model</h3>
					<input type="text" id="epc-acc-model" class="epc-acc-input" placeholder="e.g. Corolla, Civic" />
				</div>
				<div class="epc-acc__facet">
					<h3>City</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-cities"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Condition</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-condition"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Price range</h3>
					<div class="epc-acc__price-row">
						<input type="number" id="epc-acc-price-min" min="0" step="1" placeholder="Min" />
						<input type="number" id="epc-acc-price-max" min="0" step="1" placeholder="Max" />
					</div>
					<button type="button" class="epc-acc__btn" id="epc-acc-apply-price">Apply filters</button>
					<button type="button" class="epc-acc__btn epc-acc__btn-muted" id="epc-acc-reset">Reset</button>
				</div>
			</aside>

			<div class="epc-acc__main">
				<div class="epc-acc__toolbar">
					<div class="epc-acc__count" id="epc-acc-count">Loading…</div>
					<div class="epc-acc__tools">
						<label>
							<span class="sr-only">Sort by</span>
							<select id="epc-acc-sort" aria-label="Sort by">
								<option value="updated-desc">Updated Date: Recent First</option>
								<option value="updated-asc">Updated Date: Oldest First</option>
								<option value="price-asc">Price: Low to High</option>
								<option value="price-desc">Price: High to Low</option>
								<option value="top-sales">Top Sales</option>
							</select>
						</label>
						<div class="epc-acc__view-toggle" role="group" aria-label="View mode">
							<button type="button" id="epc-acc-view-list" title="List">LIST</button>
							<button type="button" id="epc-acc-view-grid" class="is-active" title="Grid">GRID</button>
						</div>
					</div>
				</div>
				<div id="epc-acc-results" class="epc-acc__grid" aria-live="polite"></div>
				<div class="epc-acc__pager" id="epc-acc-pager"></div>
			</div>
		</div>

		<div class="epc-acc__browse">
			<div class="epc-acc__browse-block">
				<h2>View spare parts and accessories by category</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-cats"></div>
			</div>
			<div class="epc-acc__browse-block">
				<h2>View by sub category</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-subs"></div>
			</div>
			<div class="epc-acc__browse-block">
				<h2>View spare parts and accessories by make</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-makes"></div>
			</div>
			<div class="epc-acc__browse-block">
				<h2>View spare parts and accessories by city</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-cities"></div>
			</div>
		</div>
	</div>
</section>

<script>
(function () {
	var root = document.getElementById('epc-accessories');
	if (!root) { return; }
	var api = root.getAttribute('data-api') || '';
	var els = {
		q: document.getElementById('epc-acc-q'),
		form: document.getElementById('epc-acc-search-form'),
		cats: document.getElementById('epc-acc-cats'),
		subs: document.getElementById('epc-acc-subs'),
		makes: document.getElementById('epc-acc-makes'),
		cities: document.getElementById('epc-acc-cities'),
		condition: document.getElementById('epc-acc-condition'),
		model: document.getElementById('epc-acc-model'),
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
		browseSubs: document.getElementById('epc-acc-browse-subs'),
		browseMakes: document.getElementById('epc-acc-browse-makes'),
		browseCities: document.getElementById('epc-acc-browse-cities')
	};

	var state = {
		q: '', category: '', subcategory: '', make: '', model: '', city: '',
		condition: '', price_min: '', price_max: '', sort: 'updated-desc', page: 1, view: 'grid',
		taxonomy: [], makes: [], cities: []
	};

	function esc(s) {
		return String(s == null ? '' : s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
	}
	function money(amount, currency) {
		var n = Number(amount);
		if (!isFinite(n) || n <= 0) { return 'Price on request'; }
		return String(currency || 'PKR') + ' ' + n.toLocaleString(undefined, { maximumFractionDigits: 0 });
	}
	function syncUrl() {
		try {
			var u = new URL(window.location.href);
			['q','category','subcategory','make','model','city','condition','price_min','price_max','sort','page'].forEach(function (k) {
				var v = state[k];
				if (v && String(v) !== '' && !(k === 'page' && String(v) === '1') && !(k === 'sort' && v === 'updated-desc')) {
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
			['q','category','subcategory','make','model','city','condition','price_min','price_max','sort','page'].forEach(function (k) {
				if (u.searchParams.has(k)) { state[k] = u.searchParams.get(k) || ''; }
			});
			if (els.q) els.q.value = state.q;
			if (els.sort) els.sort.value = state.sort || 'updated-desc';
			if (els.model) els.model.value = state.model;
			if (els.priceMin) els.priceMin.value = state.price_min;
			if (els.priceMax) els.priceMax.value = state.price_max;
		} catch (e) {}
	}

	function facetList(target, rows, valueKey, labelKey, active, onPick, allLabel) {
		if (!target) return;
		var html = '<li><button type="button" class="' + (!active ? 'is-active' : '') + '" data-value="">' + esc(allLabel || 'All') + '</button></li>';
		(rows || []).forEach(function (row) {
			var value = row[valueKey];
			var label = row[labelKey] || value;
			var count = row.count != null ? row.count : null;
			var cls = String(value) === String(active) ? ' is-active' : '';
			html += '<li><button type="button" class="' + cls + '" data-value="' + esc(value) + '"><span>' + esc(label) + '</span>'
				+ (count != null ? '<span class="count">' + esc(count) + '</span>' : '') + '</button></li>';
		});
		target.innerHTML = html;
		Array.prototype.forEach.call(target.querySelectorAll('button'), function (btn) {
			btn.addEventListener('click', function () { onPick(btn.getAttribute('data-value') || ''); });
		});
	}

	function renderFacets(facets) {
		var cats = (facets && facets.categories) || state.taxonomy || [];
		facetList(els.cats, cats, 'slug', 'label', state.category, function (v) {
			state.category = (state.category === v) ? '' : v;
			state.subcategory = '';
			state.page = 1;
			load();
		}, 'All categories');

		var subs = [];
		cats.forEach(function (c) {
			if (state.category && c.slug !== state.category) return;
			(c.subs || c.children || []).forEach(function (s) { subs.push(s); });
		});
		facetList(els.subs, subs, 'slug', 'label', state.subcategory, function (v) {
			state.subcategory = (state.subcategory === v) ? '' : v;
			state.page = 1;
			load();
		}, 'All sub categories');

		facetList(els.makes, (facets && facets.makes) || [], 'make', 'make', state.make, function (v) {
			state.make = (state.make === v) ? '' : v;
			state.page = 1;
			load();
		}, 'All makes');

		facetList(els.cities, (facets && facets.cities) || [], 'city', 'city', state.city, function (v) {
			state.city = (state.city === v) ? '' : v;
			state.page = 1;
			load();
		}, 'All cities');

		facetList(els.condition, (facets && facets.conditions) || [
			{value:'new', label:'New'}, {value:'used', label:'Used'}
		], 'value', 'label', state.condition, function (v) {
			state.condition = (state.condition === v) ? '' : v;
			state.page = 1;
			load();
		}, 'Any condition');

		if (els.browseCats) {
			els.browseCats.innerHTML = cats.map(function (c) {
				return '<button type="button" data-cat="' + esc(c.slug) + '">' + esc(c.label) + (c.count ? ' (' + c.count + ')' : '') + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseCats.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.category = btn.getAttribute('data-cat') || '';
					state.subcategory = '';
					state.page = 1;
					load();
					root.scrollIntoView({behavior:'smooth', block:'start'});
				});
			});
		}
		if (els.browseSubs) {
			var allSubs = [];
			cats.forEach(function (c) {
				(c.subs || c.children || []).slice(0, 8).forEach(function (s) {
					allSubs.push({ slug: s.slug, label: s.label, parent: c.slug, count: s.count });
				});
			});
			els.browseSubs.innerHTML = allSubs.slice(0, 40).map(function (s) {
				return '<button type="button" data-cat="' + esc(s.parent) + '" data-sub="' + esc(s.slug) + '">' + esc(s.label) + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseSubs.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.category = btn.getAttribute('data-cat') || '';
					state.subcategory = btn.getAttribute('data-sub') || '';
					state.page = 1;
					load();
					root.scrollIntoView({behavior:'smooth', block:'start'});
				});
			});
		}
		if (els.browseMakes) {
			var makes = (facets && facets.makes) || [];
			els.browseMakes.innerHTML = makes.map(function (m) {
				return '<button type="button" data-make="' + esc(m.make) + '">' + esc(m.make) + ' Parts and Accessories' + (m.count ? ' (' + m.count + ')' : '') + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseMakes.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.make = btn.getAttribute('data-make') || '';
					state.page = 1;
					load();
				});
			});
		}
		if (els.browseCities) {
			var cities = (facets && facets.cities) || [];
			els.browseCities.innerHTML = cities.map(function (c) {
				return '<button type="button" data-city="' + esc(c.city) + '">Parts and Accessories in ' + esc(c.city) + (c.count ? ' (' + c.count + ')' : '') + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseCities.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.city = btn.getAttribute('data-city') || '';
					state.page = 1;
					load();
				});
			});
		}
	}

	function renderCards(items, emptyCatalog) {
		if (!els.results) return;
		if (!items || !items.length) {
			els.results.innerHTML =
				'<div class="epc-acc__empty">'
				+ '<strong>No ads in this search yet</strong>'
				+ '<p>Categories and filters match the PakWheels accessories structure. Listings will appear here as they are added category by category.</p>'
				+ (emptyCatalog ? '<p class="epc-acc__empty-hint">Catalog is ready — start by adding products under Car Care, Interior, Exterior, Brakes, and other categories.</p>' : '')
				+ '</div>';
			return;
		}
		els.results.innerHTML = items.map(function (item, idx) {
			var href = item.external_url || '#';
			var img = item.image_url
				? '<img src="' + esc(item.image_url) + '" alt="" loading="lazy" />'
				: '<span class="epc-acc__media-mark">' + esc((item.make || item.category_label || 'AD').toString().slice(0, 3).toUpperCase()) + '</span>';
			return '<article class="epc-acc__card" style="animation-delay:' + (Math.min(idx, 8) * 0.03) + 's">'
				+ '<div class="epc-acc__media">'
				+ '<span class="epc-acc__media-badge">' + esc(item.condition === 'used' ? 'Used' : 'New') + '</span>'
				+ img
				+ '</div><div class="epc-acc__body">'
				+ '<div class="epc-acc__meta">'
				+ (item.category_label ? '<span>' + esc(item.category_label) + '</span>' : '')
				+ (item.subcategory_label ? '<span>' + esc(item.subcategory_label) + '</span>' : '')
				+ (item.city ? '<span>' + esc(item.city) + '</span>' : '')
				+ '</div>'
				+ '<h3 class="epc-acc__title"><a href="' + esc(href) + '">' + esc(item.title) + '</a></h3>'
				+ '<p class="epc-acc__article">' + esc([item.make, item.model].filter(Boolean).join(' · ')) + '</p>'
				+ '<div class="epc-acc__price-row-card"><div class="epc-acc__price">' + esc(money(item.price, item.currency)) + '</div></div>'
				+ '<div class="epc-acc__actions">'
				+ '<a class="primary" href="' + esc(href) + '">View ad</a>'
				+ '<a class="secondary" href="' + esc(href) + '">Details</a>'
				+ '</div></div></article>';
		}).join('');
	}

	function renderPager(page, pages) {
		if (!els.pager) return;
		if (pages <= 1) { els.pager.innerHTML = ''; return; }
		var html = '';
		var start = Math.max(1, page - 2), end = Math.min(pages, page + 2);
		if (page > 1) html += '<button type="button" data-page="' + (page - 1) + '">Prev</button>';
		for (var p = start; p <= end; p++) {
			html += '<button type="button" class="' + (p === page ? 'is-active' : '') + '" data-page="' + p + '">' + p + '</button>';
		}
		if (page < pages) html += '<button type="button" data-page="' + (page + 1) + '">Next</button>';
		els.pager.innerHTML = html;
		Array.prototype.forEach.call(els.pager.querySelectorAll('button'), function (btn) {
			btn.addEventListener('click', function () {
				state.page = parseInt(btn.getAttribute('data-page'), 10) || 1;
				load();
			});
		});
	}

	function load() {
		if (!api || !els.results) return;
		els.results.innerHTML = '<div class="epc-acc__loading"><i class="fa fa-spinner fa-spin"></i> Loading…</div>';
		els.count.textContent = 'Loading…';
		syncUrl();
		var params = new URLSearchParams();
		['q','category','subcategory','make','model','city','condition','price_min','price_max','sort','page'].forEach(function (k) {
			if (state[k] !== '' && state[k] != null) params.set(k, state[k]);
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
				state.taxonomy = data.taxonomy || data.facets && data.facets.categories || [];
				els.count.innerHTML = data.total
					? ('<strong>' + esc(data.from) + ' – ' + esc(data.to) + '</strong> of <strong>' + esc(data.total) + '</strong> results')
					: '<strong>0</strong> results — categories ready, ads will be added category by category';
				renderFacets(data.facets || {});
				renderCards(data.items || [], !!data.empty_catalog);
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
			state.sort = els.sort.value || 'updated-desc';
			state.page = 1;
			load();
		});
	}
	if (els.applyPrice) {
		els.applyPrice.addEventListener('click', function () {
			state.model = els.model ? els.model.value.trim() : '';
			state.price_min = els.priceMin ? els.priceMin.value : '';
			state.price_max = els.priceMax ? els.priceMax.value : '';
			state.page = 1;
			load();
		});
	}
	if (els.reset) {
		els.reset.addEventListener('click', function () {
			state = { q:'', category:'', subcategory:'', make:'', model:'', city:'', condition:'', price_min:'', price_max:'', sort:'updated-desc', page:1, view:state.view, taxonomy:state.taxonomy, makes:state.makes, cities:state.cities };
			if (els.q) els.q.value = '';
			if (els.model) els.model.value = '';
			if (els.sort) els.sort.value = 'updated-desc';
			if (els.priceMin) els.priceMin.value = '';
			if (els.priceMax) els.priceMax.value = '';
			load();
		});
	}
	function setView(mode) {
		state.view = mode;
		if (els.results) els.results.classList.toggle('is-list', mode === 'list');
		if (els.viewGrid) els.viewGrid.classList.toggle('is-active', mode === 'grid');
		if (els.viewList) els.viewList.classList.toggle('is-active', mode === 'list');
	}
	if (els.viewGrid) els.viewGrid.addEventListener('click', function () { setView('grid'); });
	if (els.viewList) els.viewList.addEventListener('click', function () { setView('list'); });

	readUrl();
	setView(state.view || 'grid');
	load();
})();
</script>
