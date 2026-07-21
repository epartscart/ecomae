<?php
/**
 * ePartsCart — Accessories & Spare Parts marketplace.
 * Listings are stored in epc_acc_listings and filled over time.
 */
defined('_ASTEXE_') or die('No access');

$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '')
	? rtrim((string) $multilang_params['lang_href'], '/')
	: '/en';
$epc_acc_ver = '20260719accNoPw1';
?>
<link rel="stylesheet" href="/content/general_pages/epc_accessories.css?v=<?php echo rawurlencode($epc_acc_ver); ?>">

<section class="epc-acc" id="epc-accessories"
	data-api="/content/shop/docpart/ajax_epc_accessories_search.php"
	data-lang="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>">

	<div class="epc-acc__hero">
		<div class="container">
			<div class="epc-acc__brand">eParts<span>Cart</span></div>
			<h1>Car spare parts and accessories</h1>
			<p>Browse categories and filters. Listings are added into each category over time.</p>
			<div class="epc-acc__hero-cta">
				<form class="epc-acc__search" id="epc-acc-search-form" role="search">
					<label class="sr-only" for="epc-acc-q">Search accessories</label>
					<input id="epc-acc-q" name="q" type="search" placeholder="Search accessories and spare parts" maxlength="80" />
					<button type="submit"><i class="fa fa-search" aria-hidden="true"></i> Search</button>
				</form>
				<button type="button" class="epc-acc__btn epc-acc__btn--ghost" id="epc-acc-notify-open">Notify Me</button>
			</div>
		</div>
	</div>

	<div class="container">
		<div class="epc-acc__vehicle" id="epc-acc-vehicle">
			<div class="epc-acc__vehicle-head">
				<strong>Select Vehicle</strong>
				<span>Filter ads by make and model</span>
			</div>
			<div class="epc-acc__vehicle-row">
				<label>
					<span>Make</span>
					<select id="epc-acc-vehicle-make" aria-label="Vehicle make">
						<option value="">Any make</option>
					</select>
				</label>
				<label>
					<span>Model</span>
					<input type="text" id="epc-acc-vehicle-model" placeholder="e.g. Corolla, Civic" />
				</label>
				<label>
					<span>Year</span>
					<input type="text" id="epc-acc-vehicle-year" placeholder="e.g. 2018" maxlength="4" />
				</label>
				<button type="button" class="epc-acc__btn" id="epc-acc-vehicle-apply">Apply</button>
				<button type="button" class="epc-acc__btn epc-acc__btn-muted" id="epc-acc-vehicle-clear">Clear</button>
			</div>
			<div class="epc-acc__saved-bar" id="epc-acc-saved-bar" hidden>
				Saved ads: <strong id="epc-acc-saved-count">0</strong>
				<button type="button" id="epc-acc-show-saved">Show saved</button>
			</div>
		</div>

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

	<dialog class="epc-acc__dialog" id="epc-acc-notify-dialog">
		<form method="dialog" class="epc-acc__dialog-inner" id="epc-acc-notify-form">
			<h3>Notify Me</h3>
			<p>Get an alert when new ads match your current filters.</p>
			<label>
				<span>Email</span>
				<input type="email" id="epc-acc-notify-email" required placeholder="you@example.com" />
			</label>
			<div class="epc-acc__dialog-actions">
				<button type="submit" class="epc-acc__btn" value="ok">Save alert</button>
				<button type="submit" class="epc-acc__btn epc-acc__btn-muted" value="cancel">Cancel</button>
			</div>
			<p class="epc-acc__dialog-note" id="epc-acc-notify-note" hidden></p>
		</form>
	</dialog>
</section>

<script>
(function () {
	var root = document.getElementById('epc-accessories');
	if (!root) { return; }
	var api = root.getAttribute('data-api') || '';
	var SAVE_KEY = 'epc_acc_saved_ads';
	var NOTIFY_KEY = 'epc_acc_notify_alerts';
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
		browseCities: document.getElementById('epc-acc-browse-cities'),
		vMake: document.getElementById('epc-acc-vehicle-make'),
		vModel: document.getElementById('epc-acc-vehicle-model'),
		vYear: document.getElementById('epc-acc-vehicle-year'),
		vApply: document.getElementById('epc-acc-vehicle-apply'),
		vClear: document.getElementById('epc-acc-vehicle-clear'),
		savedBar: document.getElementById('epc-acc-saved-bar'),
		savedCount: document.getElementById('epc-acc-saved-count'),
		showSaved: document.getElementById('epc-acc-show-saved'),
		notifyOpen: document.getElementById('epc-acc-notify-open'),
		notifyDialog: document.getElementById('epc-acc-notify-dialog'),
		notifyForm: document.getElementById('epc-acc-notify-form'),
		notifyEmail: document.getElementById('epc-acc-notify-email'),
		notifyNote: document.getElementById('epc-acc-notify-note')
	};

	var state = {
		q: '', category: '', subcategory: '', make: '', model: '', city: '',
		condition: '', price_min: '', price_max: '', sort: 'updated-desc', page: 1, view: 'grid',
		year: '', showSavedOnly: false,
		taxonomy: [], makes: [], cities: []
	};

	function esc(s) {
		return String(s == null ? '' : s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
	}
	function money(amount, currency) {
		var n = Number(amount);
		if (!isFinite(n) || n <= 0) { return 'Price on request'; }
		return String(currency || 'AED') + ' ' + n.toLocaleString(undefined, { maximumFractionDigits: 0 });
	}
	function getSaved() {
		try {
			var raw = localStorage.getItem(SAVE_KEY);
			var arr = raw ? JSON.parse(raw) : [];
			return Array.isArray(arr) ? arr.map(String) : [];
		} catch (e) { return []; }
	}
	function setSaved(ids) {
		try { localStorage.setItem(SAVE_KEY, JSON.stringify(ids)); } catch (e) {}
		updateSavedBar();
	}
	function toggleSaved(id) {
		id = String(id);
		var ids = getSaved();
		var i = ids.indexOf(id);
		if (i >= 0) { ids.splice(i, 1); } else { ids.push(id); }
		setSaved(ids);
		return ids.indexOf(id) >= 0;
	}
	function isSaved(id) { return getSaved().indexOf(String(id)) >= 0; }
	function updateSavedBar() {
		var n = getSaved().length;
		if (els.savedCount) els.savedCount.textContent = String(n);
		if (els.savedBar) els.savedBar.hidden = n < 1 && !state.showSavedOnly;
	}
	function relativeUpdated(ts) {
		var t = Number(ts) || 0;
		if (!t) return '';
		var diff = Math.max(0, Math.floor(Date.now() / 1000) - t);
		if (diff < 3600) return Math.max(1, Math.floor(diff / 60)) + ' min ago';
		if (diff < 86400) return Math.floor(diff / 3600) + ' hours ago';
		if (diff < 604800) return Math.floor(diff / 86400) + ' days ago';
		return new Date(t * 1000).toLocaleDateString();
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
			if (els.vModel) els.vModel.value = state.model;
			if (els.vMake) els.vMake.value = state.make;
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

	function fillVehicleMakes(makes) {
		if (!els.vMake) return;
		var cur = state.make || els.vMake.value || '';
		var opts = '<option value="">Any make</option>';
		(makes || []).forEach(function (m) {
			var name = typeof m === 'string' ? m : (m.make || '');
			if (!name) return;
			opts += '<option value="' + esc(name) + '"' + (name === cur ? ' selected' : '') + '>' + esc(name) + '</option>';
		});
		els.vMake.innerHTML = opts;
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

		var makes = (facets && facets.makes) || [];
		facetList(els.makes, makes, 'make', 'make', state.make, function (v) {
			state.make = (state.make === v) ? '' : v;
			if (els.vMake) els.vMake.value = state.make;
			state.page = 1;
			load();
		}, 'All makes');
		fillVehicleMakes(makes);

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
			els.browseMakes.innerHTML = makes.map(function (m) {
				return '<button type="button" data-make="' + esc(m.make) + '">' + esc(m.make) + ' Parts and Accessories' + (m.count ? ' (' + m.count + ')' : '') + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseMakes.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.make = btn.getAttribute('data-make') || '';
					if (els.vMake) els.vMake.value = state.make;
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
		var savedIds = getSaved();
		if (state.showSavedOnly) {
			items = (items || []).filter(function (it) { return savedIds.indexOf(String(it.id)) >= 0; });
		}
		if (!items || !items.length) {
			els.results.innerHTML =
				'<div class="epc-acc__empty">'
				+ '<strong>' + (state.showSavedOnly ? 'No saved ads yet' : 'No ads in this search yet') + '</strong>'
				+ '<p>Browse by category, make, model, and city. Listings will appear here as they are added category by category.</p>'
				+ (emptyCatalog && !state.showSavedOnly ? '<p class="epc-acc__empty-hint">Catalog is ready — start by adding products under Car Care, Interior, Exterior, Brakes, and other categories.</p>' : '')
				+ '</div>';
			return;
		}
		els.results.innerHTML = items.map(function (item, idx) {
			var href = item.external_url || '#';
			var saved = isSaved(item.id);
			var img = item.image_url
				? '<img src="' + esc(item.image_url) + '" alt="" loading="lazy" />'
				: '<span class="epc-acc__media-mark">' + esc((item.make || item.category_label || 'AD').toString().slice(0, 3).toUpperCase()) + '</span>';
			var compareHtml = (item.compare_price && item.compare_price > item.price)
				? '<span class="epc-acc__compare">' + esc(money(item.compare_price, item.currency)) + '</span>'
				: '';
			var vehicleBits = [item.make, item.model, item.year].filter(Boolean).join(' · ');
			return '<article class="epc-acc__card' + (item.featured ? ' is-featured' : '') + '" style="animation-delay:' + (Math.min(idx, 8) * 0.03) + 's">'
				+ '<div class="epc-acc__media">'
				+ (item.featured ? '<span class="epc-acc__featured">Featured</span>' : '')
				+ '<span class="epc-acc__media-badge">' + esc(item.condition === 'used' ? 'Used' : 'New') + '</span>'
				+ '<span class="epc-acc__photos"><i class="fa fa-camera" aria-hidden="true"></i> ' + esc(item.photo_count || 1) + '</span>'
				+ img
				+ '</div><div class="epc-acc__body">'
				+ '<div class="epc-acc__meta">'
				+ (item.category_label ? '<span>' + esc(item.category_label) + '</span>' : '')
				+ (item.subcategory_label ? '<span>' + esc(item.subcategory_label) + '</span>' : '')
				+ (item.city ? '<span>' + esc(item.city) + '</span>' : '')
				+ '</div>'
				+ '<h3 class="epc-acc__title"><a href="' + esc(href) + '">' + esc(item.title) + '</a></h3>'
				+ '<p class="epc-acc__article">' + esc(vehicleBits) + (item.updated_at ? ' · ' + esc(relativeUpdated(item.updated_at)) : '') + '</p>'
				+ '<div class="epc-acc__price-row-card"><div class="epc-acc__price">' + esc(money(item.price, item.currency)) + compareHtml + '</div></div>'
				+ '<div class="epc-acc__actions">'
				+ '<a class="primary" href="' + esc(href) + '">View ad</a>'
				+ '<button type="button" class="secondary epc-acc__save' + (saved ? ' is-saved' : '') + '" data-id="' + esc(item.id) + '">'
				+ (saved ? 'Saved' : 'Save Ad') + '</button>'
				+ '</div></div></article>';
		}).join('');
		Array.prototype.forEach.call(els.results.querySelectorAll('.epc-acc__save'), function (btn) {
			btn.addEventListener('click', function () {
				var on = toggleSaved(btn.getAttribute('data-id'));
				btn.textContent = on ? 'Saved' : 'Save Ad';
				btn.classList.toggle('is-saved', on);
				if (state.showSavedOnly) { renderCards(items, emptyCatalog); }
			});
		});
	}

	function renderPager(page, pages) {
		if (!els.pager) return;
		if (pages <= 1 || state.showSavedOnly) { els.pager.innerHTML = ''; return; }
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
				var countLabel = data.total
					? ('<strong>' + esc(data.from) + ' – ' + esc(data.to) + '</strong> of <strong>' + esc(data.total) + '</strong> results')
					: '<strong>0</strong> results — categories ready, ads will be added category by category';
				if (state.showSavedOnly) {
					countLabel = 'Showing <strong>saved</strong> ads from this search';
				}
				els.count.innerHTML = countLabel;
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
			if (els.vModel) els.vModel.value = state.model;
			state.price_min = els.priceMin ? els.priceMin.value : '';
			state.price_max = els.priceMax ? els.priceMax.value : '';
			state.page = 1;
			load();
		});
	}
	if (els.reset) {
		els.reset.addEventListener('click', function () {
			state = { q:'', category:'', subcategory:'', make:'', model:'', city:'', condition:'', price_min:'', price_max:'', sort:'updated-desc', page:1, view:state.view, year:'', showSavedOnly:false, taxonomy:state.taxonomy, makes:state.makes, cities:state.cities };
			if (els.q) els.q.value = '';
			if (els.model) els.model.value = '';
			if (els.vMake) els.vMake.value = '';
			if (els.vModel) els.vModel.value = '';
			if (els.vYear) els.vYear.value = '';
			if (els.sort) els.sort.value = 'updated-desc';
			if (els.priceMin) els.priceMin.value = '';
			if (els.priceMax) els.priceMax.value = '';
			load();
		});
	}
	if (els.vApply) {
		els.vApply.addEventListener('click', function () {
			state.make = els.vMake ? els.vMake.value : '';
			state.model = els.vModel ? els.vModel.value.trim() : '';
			state.year = els.vYear ? els.vYear.value.trim() : '';
			if (els.model) els.model.value = state.model;
			if (state.year) {
				state.q = state.year;
				if (els.q) els.q.value = state.year;
			}
			state.page = 1;
			load();
		});
	}
	if (els.vClear) {
		els.vClear.addEventListener('click', function () {
			state.make = '';
			state.model = '';
			state.year = '';
			if (state.q && /^\d{4}$/.test(state.q)) {
				state.q = '';
				if (els.q) els.q.value = '';
			}
			if (els.vMake) els.vMake.value = '';
			if (els.vModel) els.vModel.value = '';
			if (els.vYear) els.vYear.value = '';
			if (els.model) els.model.value = '';
			state.page = 1;
			load();
		});
	}
	if (els.showSaved) {
		els.showSaved.addEventListener('click', function () {
			state.showSavedOnly = !state.showSavedOnly;
			els.showSaved.textContent = state.showSavedOnly ? 'Show all' : 'Show saved';
			load();
		});
	}
	if (els.notifyOpen && els.notifyDialog) {
		els.notifyOpen.addEventListener('click', function () {
			if (els.notifyNote) els.notifyNote.hidden = true;
			if (typeof els.notifyDialog.showModal === 'function') {
				els.notifyDialog.showModal();
			} else {
				var email = window.prompt('Email for Notify Me alerts');
				if (email) saveNotify(email);
			}
		});
	}
	function saveNotify(email) {
		var alert = {
			email: email,
			created_at: Date.now(),
			filters: {
				q: state.q, category: state.category, subcategory: state.subcategory,
				make: state.make, model: state.model, city: state.city,
				condition: state.condition, price_min: state.price_min, price_max: state.price_max
			}
		};
		try {
			var raw = localStorage.getItem(NOTIFY_KEY);
			var arr = raw ? JSON.parse(raw) : [];
			if (!Array.isArray(arr)) arr = [];
			arr.push(alert);
			localStorage.setItem(NOTIFY_KEY, JSON.stringify(arr));
		} catch (e) {}
		if (els.notifyNote) {
			els.notifyNote.hidden = false;
			els.notifyNote.textContent = 'Alert saved for ' + email + '. We will use this when notifications go live.';
		}
	}
	if (els.notifyForm) {
		els.notifyForm.addEventListener('submit', function (e) {
			var submitter = e.submitter || null;
			var val = submitter ? submitter.value : 'ok';
			if (val === 'cancel') return;
			e.preventDefault();
			var email = els.notifyEmail ? els.notifyEmail.value.trim() : '';
			if (!email) return;
			saveNotify(email);
			setTimeout(function () {
				if (els.notifyDialog && els.notifyDialog.open) els.notifyDialog.close();
			}, 900);
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
	updateSavedBar();
	setView(state.view || 'grid');
	load();
})();
</script>
