<?php
defined('_ASTEXE_') or die('No access');
$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') ? rtrim($multilang_params['lang_href'], '/') : '/en';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_partsapi_config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cata_config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_cata_bridge.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_eparts_product_route.php';
if (!epc_partsapi_enabled_for_request()) {
	echo '<div class="alert alert-warning">EParts Mod is not available on this site.</div>';
	return;
}
$epc_em_vin_label = epc_storefront_vin_label();
$epc_em_chpu_on = !empty($DP_Config->chpu_search_config['chpu_search_on']);
$epc_em_chpu_parts_url = !empty($DP_Config->chpu_search_config['level_1']['url']) ? $DP_Config->chpu_search_config['level_1']['url'] : 'parts';
$epc_em_chpu_brands_url = !empty($DP_Config->chpu_search_config['level_2']['mode_1']['url']) ? $DP_Config->chpu_search_config['level_2']['mode_1']['url'] : 'brands';
$epc_em_chpu_slash_code = isset($DP_Config->chpu_search_config['slash_code']) ? $DP_Config->chpu_search_config['slash_code'] : '%2F';
$epc_em_configured = epc_partsapi_credentials_configured();
$epc_em_method_shops = epc_partsapi_method_shop_client_map();
$epc_em_action_shops = epc_partsapi_action_shop_client_map();
$epc_em_cata_bridge = epc_cata_bridge_js_config();
$epc_em_pres = epc_cata_presentation_js_config();
$epc_em_default_section = (string) ($epc_em_pres['defaultSection'] ?? 'passenger');
$epc_em_sections_enabled = is_array($epc_em_pres['sectionsEnabled'] ?? null) ? $epc_em_pres['sectionsEnabled'] : array('passenger' => true, 'commercial' => true, 'motorbike' => true);
?>
<link rel="stylesheet" href="/content/general_pages/epc_vc_catalog.css?v=<?php echo rawurlencode(EPC_CATA_VERSION); ?>">
<link rel="stylesheet" href="/content/general_pages/epc_car_mod_theme.css?v=<?php echo rawurlencode(EPC_CATA_VERSION); ?>">
<script src="/content/general_pages/epc_vc_catalog_ui.js?v=<?php echo rawurlencode(EPC_CATA_VERSION); ?>"></script>

<div class="epc-vc epc-cm" id="epc-em-root" data-lang-href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>">
	<div class="epc-cm-toolbar">
		<a href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>/shop/cart" class="epc-cm-cart" title="Cart"><i class="fa fa-shopping-cart"></i> Cart</a>
		<a href="#" onclick="return false;" title="My Garage (coming soon)"><i class="fa fa-car"></i> My Garage</a>
	</div>
	<div class="epc-vc-hero-grid epc-vc-hero-grid--single">
		<div class="epc-cm-dashboard">
			<h2 class="epc-cm-dashboard-title">Select a vehicle</h2>
			<div id="epc-em-step-picker"></div>
			<div class="epc-cm-select-hidden">
				<select class="form-control" id="epc-em-make"><option value="">Make</option></select>
				<select class="form-control" id="epc-em-model" disabled><option value="">Model</option></select>
				<select class="form-control" id="epc-em-car" disabled><option value="">Engine / modification</option></select>
			</div>
			<div class="epc-cm-vin-row" id="epc-em-vin-row"<?php if (empty($epc_em_pres['enableVinSearch']) && empty($epc_em_pres['enablePlateSearch'])) { echo ' style="display:none"'; } ?>>
				<?php if (!empty($epc_em_pres['enableVinSearch'])) { ?>
				<input type="search" class="form-control" id="epc-em-vin" placeholder="<?php echo htmlspecialchars($epc_em_vin_label, ENT_QUOTES, 'UTF-8'); ?> / Engine / Chassis" maxlength="17" autocomplete="off" style="text-transform:uppercase;">
				<?php } ?>
				<?php if (!empty($epc_em_pres['enablePlateSearch'])) { ?>
				<div class="epc-cm-vin-plate"><span>GB</span><input type="text" id="epc-em-plate" placeholder="AA12 BBB" maxlength="8" autocomplete="off"></div>
				<?php } ?>
			</div>
			<div class="epc-cm-part-search">
				<button type="button" class="epc-cm-part-search-btn" id="epc-em-search-go" aria-label="Search"><i class="fa fa-search"></i></button>
				<input type="search" class="form-control" id="epc-em-part" placeholder="Number, Article, OE, 5w30…." autocomplete="off">
			</div>
			<div class="epc-cm-help"><a href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>/contacts">Need help?</a></div>
			<?php if (!$epc_em_configured) { ?>
				<div class="alert alert-info" style="margin-top:12px;font-size:12px;">Add your EParts API key in <code>config.epc-partsapi.php</code> on the server.</div>
			<?php } ?>
			<div class="epc-vc-message" id="epc-em-subscription-banner" style="display:none;margin-top:12px;font-size:12px;"></div>
		</div>
	</div>
	<div id="epc-em-flow" class="epc-vc-flow" style="display:none;"></div>
	<div id="epc-em-vehicle-bar" style="display:none;"></div>
	<div id="epc-em-demo-banner" style="display:none;margin:0 0 12px;padding:10px 14px;border-radius:10px;background:#fffbeb;border:1px solid #fcd34d;font-size:12px;font-weight:700;color:#92400e;"></div>
	<div id="epc-em-output"><div class="epc-vc-loader">Select a vehicle type, then choose a make…</div></div>
</div>

<script>
(function () {
	'use strict';
	var root = document.getElementById('epc-em-root');
	if (!root) { return; }
	var langHref = root.getAttribute('data-lang-href') || '';
	var epcEmChpuOn = <?php echo $epc_em_chpu_on ? 'true' : 'false'; ?>;
	var epcEmChpuPartsUrl = <?php echo json_encode($epc_em_chpu_parts_url, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var epcEmChpuBrandsUrl = <?php echo json_encode($epc_em_chpu_brands_url, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var epcEmChpuSlashCode = <?php echo json_encode($epc_em_chpu_slash_code, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var epcVinLabel = <?php echo json_encode($epc_em_vin_label, JSON_UNESCAPED_UNICODE); ?>;
	var output = document.getElementById('epc-em-output');
	var vehicleBar = document.getElementById('epc-em-vehicle-bar');
	var flowBar = document.getElementById('epc-em-flow');
	var makeSelect = document.getElementById('epc-em-make');
	var modelSelect = document.getElementById('epc-em-model');
	var carSelect = document.getElementById('epc-em-car');
	var stepPicker = document.getElementById('epc-em-step-picker');
	var partInput = document.getElementById('epc-em-part');
	var vinInput = document.getElementById('epc-em-vin');
	var SHOP_URL = 'https://partsapi.ru/account/shop';
	var METHOD_SHOPS = <?php echo json_encode($epc_em_method_shops, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var ACTION_SHOPS = <?php echo json_encode($epc_em_action_shops, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var CATA_BRIDGE = <?php echo json_encode($epc_em_cata_bridge, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var PRESENTATION = <?php echo json_encode($epc_em_pres, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var SUBSCRIPTION_MSG = 'An EParts API subscription is required for vehicle models (getModels). Add the method key to config.epc-partsapi.php (method_keys) after subscribing.';
	var demoLimit = (function () {
		var m = /[?&]demo=(\d+)/i.exec(window.location.search || '');
		return m ? Math.max(1, Math.min(200, parseInt(m[1], 10) || 0)) : 0;
	})();
	var state = { section: PRESENTATION.defaultSection || 'passenger', manufacturers: [], models: [], cars: [], categories: [], make: null, model: null, car: null, catalogReady: null, shopUrl: SHOP_URL, lastAction: '', view: 'dashboard', currentCategory: '', currentSubcategory: '', currentStrId: '', pendingCategory: null, demoMode: demoLimit > 0, demoPayload: null, demoArticles: [], articleView: PRESENTATION.defaultArticleView || 'list', articleSort: 'brand', articleBrands: [], articleTerm: '', articleRows: [], mselectPanel: '' };
	var pendingCategoryFromUrl = (function () {
		try {
			var qs = new URLSearchParams(window.location.search);
			var id = qs.get('category') || qs.get('strId') || qs.get('str_id');
			if (!id) { return null; }
			return {
				id: String(id),
				name: String(qs.get('category_name') || qs.get('categoryName') || ''),
				subName: String(qs.get('subcategory_name') || qs.get('subcategoryName') || '')
			};
		} catch (eUrlCat) { return null; }
	})();
	if (pendingCategoryFromUrl) { state.pendingCategory = pendingCategoryFromUrl; }
	var API_MS = state.demoMode ? 120000 : 8000;
	var DEMO_OFFLINE_TREE = {
		'16': {
			models: [
				{ MS_ID: 5601, MODEL_SERIES: '1 Series (E81)', CI_FROM: '2007', CI_TO: '2011' },
				{ MS_ID: 5602, MODEL_SERIES: '3 Series (F30)', CI_FROM: '2012', CI_TO: '2018' }
			],
			cars: {
				'5601': [
					{ ID: 1001, carId: 1001, MODIFICATION: '116i 1.6', POWER_KW: '85', FUEL_TYPE: 'Petrol' },
					{ ID: 1002, carId: 1002, MODIFICATION: '118i 2.0', POWER_KW: '100', FUEL_TYPE: 'Petrol' }
				],
				'5602': [
					{ ID: 1003, carId: 1003, MODIFICATION: '320i 2.0', POWER_KW: '135', FUEL_TYPE: 'Petrol' },
					{ ID: 1004, carId: 1004, MODIFICATION: '318d 2.0', POWER_KW: '105', FUEL_TYPE: 'Diesel' }
				]
			}
		}
	};

	function text(v) { return String(v == null ? '' : v); }
	function categoriesWithSubcategories(cats) {
		var map = PRESENTATION.subcategoriesMap || {};
		return (cats || []).map(function (c) {
			var sid = String(c.STR_ID || c.CATEGORY_ID || c.id || '');
			if (!c.children && map[sid]) {
				return Object.assign({}, c, { children: map[sid] });
			}
			return c;
		});
	}
	function filterRowsBySubcategory(rows, subName) {
		if (!subName) { return rows; }
		var term = subName.toLowerCase();
		var filtered = (rows || []).filter(function (row) {
			var group = text(row.PRODUCT_GROUP || row.name || row.ART_PRODUCT_NAME || row.partname || '');
			return group.toLowerCase().indexOf(term) !== -1 || term.indexOf(group.toLowerCase()) !== -1;
		});
		return filtered.length ? filtered : rows;
	}
	function esc(v) { return text(v).replace(/[&<>"']/g, function (c) { return ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'})[c]; }); }
	function apiItems(d) { return Array.isArray(d) ? d : (d && Array.isArray(d.data) ? d.data : []); }
	function isSubscriptionErr(e) { return !!(e && (e.subscription_required || e.http_status === 401 || e.http_status === 403)); }
	function shopUrlFor(action, err) {
		if (err && err.shop_url) { return err.shop_url; }
		if (err && err.method && METHOD_SHOPS[err.method]) { return METHOD_SHOPS[err.method].shop_url; }
		if (action && ACTION_SHOPS[action]) { return ACTION_SHOPS[action].shop_url; }
		return state.shopUrl || SHOP_URL;
	}
	function subscriptionMessage(e, action) {
		if (e && e.error) { return e.error; }
		var act = action || state.lastAction || '';
		var meta = ACTION_SHOPS[act] || null;
		if (meta) {
			return 'An EParts API subscription is required for ' + meta.label + ' (' + meta.method + '). Add the key to config.epc-partsapi.php under method_keys after subscribing.';
		}
		if (state.catalogReady === false) { return SUBSCRIPTION_MSG; }
		return SUBSCRIPTION_MSG;
	}
	function applyCatalogGate(statusPayload) {
		state.catalogReady = !!statusPayload.catalog_ready;
		state.shopUrl = statusPayload.shop_url || SHOP_URL;
		if (statusPayload.subscription_message) { SUBSCRIPTION_MSG = statusPayload.subscription_message; }
		renderSubscriptionsPanel(statusPayload);
			if (!state.catalogReady) {
				modelSelect.innerHTML = '<option value="">Model (subscribe to getModels)</option>';
				modelSelect.disabled = true;
				carSelect.innerHTML = '<option value="">Engine / modification</option>';
				carSelect.disabled = true;
			}
	}
	function renderSubscriptionsPanel(statusPayload) {
		var methods = statusPayload && statusPayload.methods ? statusPayload.methods : null;
		if (!methods) { return; }
		Array.prototype.forEach.call(document.querySelectorAll('[data-sub-status]'), function (badge) {
			var method = badge.getAttribute('data-sub-status');
			var meta = methods[method];
			if (!meta) { return; }
			if (meta.subscribed) { badge.textContent = 'Active'; badge.className = 'epc-pa-sub-badge is-active'; return; }
			if (meta.subscription_required) { badge.textContent = 'Subscribe'; badge.className = 'epc-pa-sub-badge is-required'; return; }
			badge.textContent = meta.key_configured ? 'Key set' : 'Check';
			badge.className = 'epc-pa-sub-badge is-unknown';
		});
	}
	function showSubscriptionBanner(msg, shopUrl, action) {
		var el = document.getElementById('epc-em-subscription-banner');
		if (!el) { return; }
		var url = shopUrl || shopUrlFor(action || state.lastAction, null);
		el.style.display = '';
		el.innerHTML = esc(msg || subscriptionMessage(null, action || state.lastAction)) + ' <a href="' + esc(url) + '" target="_blank" rel="noopener">Subscribe at EParts API</a>';
	}
	function loading(msg) { if (window.epcProgress) { window.epcProgress.start(msg || 'Loading…'); } output.innerHTML = '<div class="epc-vc-loader">' + esc(msg || 'Loading…') + '</div>'; }
	function done() { if (window.epcProgress) { window.epcProgress.done(); } }
	function showSubscriptionErr(e, action) {
		var act = action || state.lastAction || '';
		var url = shopUrlFor(act, e);
		var msg = subscriptionMessage(e, act);
		done();
		output.innerHTML = '<div class="epc-vc-message">' + esc(msg) + ' <a href="' + esc(url) + '" target="_blank" rel="noopener">Subscribe at EParts API</a></div>';
		showSubscriptionBanner(msg, url, act);
	}
	function showErr(e, action) {
		if (isSubscriptionErr(e)) { showSubscriptionErr(e, action); return; }
		done();
		output.innerHTML = '<div class="epc-vc-message">' + esc((e && (e.error || e.message)) || 'Request failed.') + '</div>';
	}
	function ui() { return window.epcVcCatalogUi; }
	function renderStepPickerUi(activeStep) {
		var mod = ui();
		if (!mod || !stepPicker) { return; }
		var steps = [
			{ key: 'make', label: 'Brand', value: state.make ? state.make.MANUFACTURER : '' },
			{ key: 'model', label: 'Model', value: state.model ? state.model.MODEL_SERIES : '' },
			{ key: 'car', label: 'Engine', value: state.car ? (state.car.MODIFICATION || state.car.carName) : '' }
		];
		var pickerOpts = {};
		if (state.mselectPanel === 'make' && state.manufacturers.length) {
			pickerOpts.brandPanel = mod.renderBrandPickerDropdown(state.manufacturers, {
				section: state.section,
				sections: mod.DEFAULT_VEHICLE_SECTIONS || [
					{ key: 'passenger', label: 'Passengers' },
					{ key: 'commercial', label: 'Commercial' },
					{ key: 'motorbike', label: 'Motorcycles' }
				]
			});
		}
		if (state.mselectPanel === 'car' && state.cars.length) {
			pickerOpts.enginePanel = mod.renderEnginePickerDropdown(state.cars, {
				selectedId: state.car && (state.car.ID || state.car.carId)
			});
		}
		stepPicker.innerHTML = mod.renderStepPicker(steps, activeStep - 1, pickerOpts);
		mod.bindStepPicker(stepPicker, function (idx) {
			if (idx === 0) {
				state.mselectPanel = state.mselectPanel === 'make' ? '' : 'make';
				renderStepPickerUi(1);
				if (state.mselectPanel === 'make' && state.manufacturers.length) {
					mod.bindBrandPickerDropdown(stepPicker, state.manufacturers, function (item, itemIdx) {
						state.mselectPanel = '';
						makeSelect.value = String(itemIdx);
						state.make = item;
						setStep(2);
						if (state.catalogReady === false) {
							showSubscriptionBanner(SUBSCRIPTION_MSG, shopUrlFor('models', null), 'models');
							return;
						}
						loadModelsForMake(item, itemIdx);
					}, { section: state.section, onSection: function (sec) {
						state.section = sec;
						loadManufacturers();
					}});
				}
				return;
			}
			if (idx === 1 && state.make) {
				state.mselectPanel = '';
				setStep(2);
				loadModelsForMake(state.make, state.manufacturers.indexOf(state.make));
			} else if (idx === 2 && state.model) {
				state.mselectPanel = state.mselectPanel === 'car' ? '' : 'car';
				renderStepPickerUi(3);
				if (state.mselectPanel === 'car' && state.cars.length) {
					mod.bindEnginePickerDropdown(stepPicker, state.cars, function (carItem, carIdx) {
						state.mselectPanel = '';
						carSelect.value = String(carIdx);
						state.car = carItem;
						renderVehicleBar();
						renderStepPickerUi(3);
						loadCategories();
					});
				} else if (!state.mselectPanel) {
					loadCarsForModel(state.model);
				}
			}
		});
	}
	function applyFilter(term) {
		term = (term || '').toLowerCase();
		Array.prototype.forEach.call(output.querySelectorAll('[data-search]'), function (n) {
			n.style.display = n.getAttribute('data-search').toLowerCase().indexOf(term) === -1 ? 'none' : '';
		});
	}
	function applyFilterFromOutput() {
		var search = output.querySelector('.epc-vc-make-search, .epc-vc-cat-search');
		applyFilter(search ? search.value : '');
	}
	function partSearchUrl(brand, article) {
		var a = text(article).replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
		var b = text(brand).trim();
		if (!a) { return langHref + '/shop/part_search'; }
		if (epcEmChpuOn) {
			if (!b) { return langHref + '/' + epcEmChpuPartsUrl + '/' + epcEmChpuBrandsUrl + '/' + encodeURIComponent(a); }
			return langHref + '/' + epcEmChpuPartsUrl + '/' + encodeURIComponent(b.split('/').join(epcEmChpuSlashCode)) + '/' + encodeURIComponent(a);
		}
		var u = langHref + '/shop/part_search?article=' + encodeURIComponent(a);
		if (b) { u += '&brend=' + encodeURIComponent(b); }
		return u;
	}
	function productDetailUrl(brand, article) {
		var a = text(article).replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
		var slug = text(brand).toUpperCase().replace(/[^A-Z0-9]/g, '');
		if (!slug || !a) { return langHref + '/eparts-product'; }
		return langHref + '/product/' + encodeURIComponent(slug) + '/' + encodeURIComponent(a);
	}
	function bindArticleRowNavigation(container) {
		if (!container) { return; }
		Array.prototype.forEach.call(container.querySelectorAll('.epc-vc-article-row.is-clickable'), function (row) {
			row.onclick = function (e) {
				if (e.target && e.target.closest && e.target.closest('a.btn')) { return; }
				var href = row.getAttribute('data-product-url');
				if (href) { window.location.href = href; }
			};
		});
	}
	function cartUrl() { return langHref + '/shop/cart'; }
	function renderDemoDashboard(payload) {
		var mod = ui();
		var banner = document.getElementById('epc-em-demo-banner');
		if (banner) {
			banner.style.display = '';
			banner.innerHTML = 'Demo mode — ' + (payload.manufacturer_count || 0) + ' makes, '
				+ (payload.category_count || 0) + ' categories, ' + (payload.article_count || 0) + ' sample parts. '
				+ 'Flow: Dashboard → Vehicle → Categories → Parts → Cart. '
				+ '<a href="' + esc(window.location.pathname) + '">Exit demo</a>';
		}
		if (flowBar) {
			flowBar.style.display = '';
			flowBar.innerHTML = '<span class="is-current">Dashboard</span><span>›</span><span>Vehicle</span><span>›</span><span>Categories</span><span>›</span><span>Parts</span><span>›</span><span>Cart</span>';
		}
		state.manufacturers = (payload.manufacturers || []).map(function (row) {
			return {
				MFA_ID: row.ext_id || row.MFA_ID,
				MANUFACTURER: row.name || row.MANUFACTURER,
				IS_LOGO: row.has_logo || row.IS_LOGO || 0,
				has_logo: row.has_logo || 0,
				logo_url: row.logo_url || '',
				POPULAR_PC: row.popular || row.POPULAR_PC || '',
				source: row.source || 'cata'
			};
		});
		state.categories = payload.categories || [];
		var html = '';
		if (mod && state.manufacturers.length) {
			html += '<div class="epc-cm-brand-head"><h2><em>Brand</em> — ' + esc(mod.vehicleSectionLabel ? mod.vehicleSectionLabel(state.section) : 'Passengers') + '</h2></div>';
			html += mod.renderMakeGrid(state.manufacturers, { selectedId: null });
		}
		if (mod && state.categories.length) {
			html += mod.renderCategoryGrid(state.categories, { showBar: false, carModHeading: true });
		}
		var articles = payload.articles || [];
		if (mod && articles.length) {
			var sampleOpts = {
				view: 'list',
				sort: 'brand',
				activeBrands: [],
				term: '',
				partSearchUrl: partSearchUrl,
				productDetailUrl: productDetailUrl,
				carModLayout: true,
				partsListLayout: true,
				categoryName: 'Service parts',
				warehouseOnly: true,
				checkPriceLabel: 'Check price',
				cartLabel: 'Cart'
			};
			var sampleBuilt = mod.renderArticlesPanel(articles, sampleOpts);
			html += '<div class="epc-vc-section-bar" style="margin-top:18px;">Sample parts — Service parts</div>';
			html += '<div id="epc-em-demo-parts">' + sampleBuilt.html + '</div>';
		} else {
			html += '<div class="epc-vc-section-bar" style="margin-top:18px;">Sample parts</div>';
			html += '<p class="epc-vc-list-count">' + articles.length + ' parts</p>';
			html += '<div class="epc-vc-articles">' + articles.map(function (r) { return renderArticleRow(r); }).join('') + '</div>';
		}
		output.innerHTML = html;
		if (mod && state.manufacturers.length) {
			mod.bindMakeGrid(output, state.manufacturers, function (item, idx) {
				makeSelect.value = String(idx);
				state.make = item;
				setStep(2);
				if (state.catalogReady === false) {
					showSubscriptionBanner(SUBSCRIPTION_MSG, shopUrlFor('models', null), 'models');
					return;
				}
				loadModelsForMake(item, idx);
			});
		}
		if (mod && state.categories.length) {
			mod.bindCategoryGrid(output, function () {
				output.insertAdjacentHTML('afterbegin', '<div class="epc-vc-message" style="margin-bottom:12px;">Select a vehicle (Brand → Model → Engine) to browse parts in this category.</div>');
			});
		}
		bindArticleRowNavigation(output);
		renderStepPickerUi(1);
		applyFilterFromOutput();
		done();
	}
	function fallbackDemoPayload() {
		var mod = ui();
		var cats = mod && mod.CAR_MOD_CATEGORIES ? mod.CAR_MOD_CATEGORIES.map(function (c) {
			return { STR_ID: c.id, CATEGORY_ID: c.id, ICON_ID: c.id, CATEGORY_NAME: c.name, source: 'default_tree' };
		}) : [];
		return {
			ok: true,
			demo: true,
			manufacturer_count: 12,
			category_count: cats.length,
			article_count: 5,
			manufacturers: [
				{ ext_id: 16, name: 'BMW', has_logo: 1, popular: '1' },
				{ ext_id: 74, name: 'MERCEDES-BENZ', has_logo: 1, popular: '1' },
				{ ext_id: 88, name: 'VW', has_logo: 1, popular: '1' },
				{ ext_id: 5, name: 'AUDI', has_logo: 1, popular: '1' },
				{ ext_id: 36, name: 'FORD', has_logo: 1, popular: '1' },
				{ ext_id: 45, name: 'HONDA', has_logo: 1, popular: '1' },
				{ ext_id: 47, name: 'HYUNDAI', has_logo: 1, popular: '1' },
				{ ext_id: 52, name: 'KIA', has_logo: 1, popular: '1' },
				{ ext_id: 55, name: 'MAZDA', has_logo: 1, popular: '1' },
				{ ext_id: 65, name: 'NISSAN', has_logo: 1, popular: '1' },
				{ ext_id: 84, name: 'TOYOTA', has_logo: 1, popular: '1' },
				{ ext_id: 93, name: 'VOLVO', has_logo: 1, popular: '1' }
			],
			categories: cats,
			articles: [
				{ ART_ARTICLE_NR: '1J0971972', ART_SUP_BRAND: 'VAG', PRODUCT_GROUP: 'Connector', source: 'demo' },
				{ ART_ARTICLE_NR: '11427566327', ART_SUP_BRAND: 'BMW', PRODUCT_GROUP: 'Oil Filter', source: 'demo' },
				{ ART_ARTICLE_NR: 'W712/95', ART_SUP_BRAND: 'MANN-FILTER', PRODUCT_GROUP: 'Oil Filter', source: 'demo' },
				{ ART_ARTICLE_NR: '0986424590', ART_SUP_BRAND: 'BOSCH', PRODUCT_GROUP: 'Brake Pad Set', source: 'demo' },
				{ ART_ARTICLE_NR: 'K015598XS', ART_SUP_BRAND: 'BREMBO', PRODUCT_GROUP: 'Brake Disc', source: 'demo' }
			]
		};
	}
	function loadDemoMode() {
		loading('Loading demo catalog…');
		var q = new URLSearchParams();
		q.set('action', 'demo');
		q.set('section', state.section);
		q.set('demo', String(demoLimit));
		var ctrl = new AbortController();
		var timer = setTimeout(function () { ctrl.abort(); }, API_MS);
		fetch((CATA_BRIDGE.cata_api || '/api/eparts_cata_proxy.php') + '?' + q.toString(), { credentials: 'same-origin', signal: ctrl.signal })
			.then(function (r) { clearTimeout(timer); return r.json(); })
			.then(function (payload) {
				if (!payload || !payload.ok) { throw payload; }
				renderDemoDashboard(payload);
			})
			.catch(function () {
				renderDemoDashboard(fallbackDemoPayload());
			});
	}
	function pendingCategoryBanner() {
		if (!state.pendingCategory || !state.pendingCategory.id) { return ''; }
		var label = state.pendingCategory.name || ('Category ' + state.pendingCategory.id);
		return '<div class="epc-vc-message" style="margin-bottom:12px;">Category <strong>' + esc(label) + '</strong> selected. Choose make, model and engine to browse parts.</div>';
	}
	function consumePendingCategory() {
		if (!state.pendingCategory || !state.pendingCategory.id || !state.car) { return false; }
		var pid = String(state.pendingCategory.id);
		var catMatch = null;
		for (var i = 0; i < state.categories.length; i++) {
			var c = state.categories[i];
			if (String(c.STR_ID || c.CATEGORY_ID || c.id || '') === pid) {
				catMatch = c;
				break;
			}
		}
		var catName = catMatch ? (catMatch.CATEGORY_NAME || catMatch.name || state.pendingCategory.name) : state.pendingCategory.name;
		if (!catMatch && !catName) { return false; }
		var subName = state.pendingCategory.subName || '';
		state.pendingCategory = null;
		loadArticles(pid, catName || ('Category ' + pid), subName);
		return true;
	}
	function renderMakeDashboard() {
		var mod = ui();
		if (!mod || !state.manufacturers.length) {
			output.innerHTML = pendingCategoryBanner() + '<div class="epc-vc-message">Choose a make, then model and engine/modification to browse product categories.</div>';
			renderStepPickerUi(1);
			return;
		}
		output.innerHTML = pendingCategoryBanner() + '<div class="epc-cm-brand-head"><h2><em>Brand</em> — ' + esc(mod.vehicleSectionLabel ? mod.vehicleSectionLabel(state.section) : (state.section === 'commercial' ? 'Commercial' : (state.section === 'motorbike' ? 'Motorcycles' : 'Passengers'))) + '</h2></div>' +
			mod.renderMakeGrid(state.manufacturers, { selectedId: state.make && state.make.MFA_ID });
		mod.bindMakeGrid(output, state.manufacturers, function (item, idx) {
			makeSelect.value = String(idx);
			state.make = item;
			setStep(2);
			if (state.catalogReady === false) {
				showSubscriptionBanner(SUBSCRIPTION_MSG, shopUrlFor('models', null), 'models');
				return;
			}
			loadModelsForMake(item, idx);
		});
		var makeSearch = output.querySelector('.epc-vc-make-search');
		if (makeSearch) {
			makeSearch.oninput = function () { applyFilter(makeSearch.value); };
		}
		renderStepPickerUi(1);
		applyFilterFromOutput();
	}
	function loadModelsForMake(makeItem, makeIdx) {
		loading('Loading models…');
		api('models', { MFA_ID: makeItem.MFA_ID }).then(function (payload) {
			state.models = apiItems(payload);
			modelSelect.innerHTML = '<option value="">Model</option>' + state.models.map(function (m, i) {
				return '<option value="' + i + '">' + esc(m.MODEL_SERIES) + '</option>';
			}).join('');
			modelSelect.disabled = false;
			carSelect.innerHTML = '<option value="">Engine / modification</option>';
			carSelect.disabled = true;
			state.model = state.car = null;
			var mod = ui();
			if (mod && state.models.length) {
				output.innerHTML = mod.renderModelGrid(state.models, {
					makeName: makeItem.MANUFACTURER,
					makeItem: makeItem,
					homeHref: langHref + '/eparts_mod_catalog',
					carModLayout: true,
					letterFilter: true,
					letterIndex: true,
					yearFilter: true,
					columns: 8
				});
				mod.bindModelGrid(output, state.models, function (modelItem, modelIdx) {
					modelSelect.value = String(modelIdx);
					state.model = modelItem;
					setStep(3);
					loadCarsForModel(modelItem);
				});
			} else {
				output.innerHTML = '<div class="epc-vc-message">No models for this make.</div>';
			}
			renderStepPickerUi(2);
			done();
			applyFilterFromOutput();
		}).catch(function (err) {
			if (isSubscriptionErr(err)) {
				modelSelect.innerHTML = '<option value="">Model (subscription required)</option>';
				modelSelect.disabled = true;
				carSelect.innerHTML = '<option value="">Engine / modification</option>';
				carSelect.disabled = true;
				state.model = state.car = null;
				setStep(1);
				showSubscriptionErr(err, 'models');
				return;
			}
			showErr(err);
		});
	}
	function loadCarsForModel(modelItem) {
		loading('Loading vehicles…');
		api('modifications', { MS_ID: modelItem.MS_ID, MFA_ID: state.make.MFA_ID }).then(function (payload) {
			var mod = ui();
			state.cars = mod ? mod.dedupeModifications(mod.normalizeModifications(apiItems(payload))) : apiItems(payload);
			carSelect.innerHTML = '<option value="">Engine / modification</option>' + state.cars.map(function (c, i) {
				return '<option value="' + i + '">' + esc(mod ? [
					mod.modificationLiter(c),
					mod.modificationTrimName(c),
					mod.modificationYearDisplay(c),
					mod.modificationPowerDisplay(c)
				].filter(Boolean).join(' · ') : (c.MODIFICATION || c.carName)) + '</option>';
			}).join('');
			carSelect.disabled = false;
			state.car = null;
			if (mod && state.cars.length) {
				output.innerHTML = mod.renderModificationGrid(state.cars, {
					makeName: state.make && (state.make.MANUFACTURER || state.make.name || ''),
					modelName: modelItem.MODEL_SERIES || modelItem.name || '',
					modelItem: modelItem,
					groupByFuel: true,
					selectedId: state.car && (state.car.ID || state.car.carId)
				});
				mod.bindModificationGrid(output, state.cars, function (carItem, carIdx) {
					carSelect.value = String(carIdx);
					state.car = carItem;
					renderVehicleBar();
					renderStepPickerUi(3);
					loadCategories();
				}, { onBack: function () {
					setStep(2);
					loadModelsForMake(state.make, state.manufacturers.indexOf(state.make));
				} });
			} else {
				output.innerHTML = '<div class="epc-vc-message">No vehicles for this model.</div>';
			}
			renderStepPickerUi(3);
			done();
		}).catch(showErr);
	}
	function renderArticleRow(r) {
		var mod = ui();
		if (mod) {
			return mod.renderArticleRow(r, { partSearchUrl: partSearchUrl, productDetailUrl: productDetailUrl, carModLayout: true, cartLabel: 'Cart' });
		}
		var art = r.ART_ARTICLE_NR || r.sku || r.partNumber || r.crossNumber || '';
		var brand = r.ART_SUP_BRAND || r.SUP_BRAND || r.brand || r.crossBrand || '';
		var group = r.PRODUCT_GROUP || r.ART_PRODUCT_NAME || r.partname || '';
		var shopUrl = partSearchUrl(brand, art);
		return '<div class="epc-vc-article-row" data-search="' + esc(art + ' ' + brand + ' ' + group) + '"><div><span class="epc-vc-article-oem">' + esc(art) + '</span> <span class="epc-vc-article-brand">' + esc(brand) + '</span></div><a class="btn btn-xs btn-cart" href="' + esc(shopUrl) + '">Shop</a></div>';
	}
	function renderFlowBar() {
		if (!flowBar) { return; }
		var steps = ['Dashboard', 'Vehicle', 'Categories', 'Parts', 'Cart'];
		var active = 0;
		if (state.make) { active = 1; }
		if (state.car) { active = 2; }
		if (state.view === 'articles') { active = 3; }
		flowBar.style.display = state.make ? '' : 'none';
		flowBar.innerHTML = steps.map(function (label, i) {
			return (i ? '<span>›</span>' : '') + '<span class="' + (i === active ? 'is-current' : '') + '">' + label + '</span>';
		}).join('');
	}
	function cataApi(action, params) {
		var cataAction = action === 'cars' ? 'modifications' : (action === 'part_search' ? 'search' : action);
		var cq = new URLSearchParams();
		cq.set('action', cataAction);
		cq.set('section', state.section);
		if (action === 'categories' || action === 'articles' || action === 'products') {
			cq.set('source', 'eparts_api');
		}
		Object.keys(params || {}).forEach(function (k) {
			if (params[k] == null || params[k] === '') { return; }
			if (k === 'makeId' || k === 'MFA_ID') { cq.set('mfa_id', params[k]); return; }
			if (k === 'modelId' || k === 'MS_ID') { cq.set('ms_id', params[k]); return; }
			if (k === 'carId' || k === 'ID') { cq.set('carId', params[k]); cq.set('ID', params[k]); return; }
			if (k === 'strId' || k === 'CATEGORY_ID') { cq.set('strId', params[k]); cq.set('CATEGORY_ID', params[k]); return; }
			cq.set(k, params[k]);
		});
		var cctrl = new AbortController();
		var ctimer = setTimeout(function () { cctrl.abort(); }, API_MS);
		return fetch((CATA_BRIDGE.cata_api || '/api/eparts_cata_proxy.php') + '?' + cq.toString(), { credentials: 'same-origin', signal: cctrl.signal })
			.then(function (r) { clearTimeout(ctimer); return r.json(); })
			.then(function (payload) {
				if (payload.ok === false) { throw payload; }
				if (action === 'manufacturers' && (payload.data || []).length) {
					payload.data = (payload.data || []).map(function (row) {
						return {
							MFA_ID: row.ext_id,
							MANUFACTURER: row.name,
							IS_LOGO: row.has_logo || 0,
							has_logo: row.has_logo || 0,
							logo_url: row.logo_url || '',
							source: row.source || 'cata'
						};
					});
				}
				if (action === 'models' && (payload.data || []).length) {
					payload.data = (payload.data || []).map(function (row) {
						return {
							MS_ID: row.ext_id,
							MFA_ID: row.mfa_ext_id,
							MODEL_SERIES: row.name,
							CI_FROM: row.year_from || '',
							CI_TO: row.year_to || '',
							image_url: row.image_url || ''
						};
					});
				}
				if ((action === 'modifications' || action === 'cars') && (payload.data || []).length) {
					payload.data = (payload.data || []).map(function (row) {
						var title = row.title || row.name || row.MODIFICATION || '';
						return { ID: row.ext_id, carId: row.ext_id, MODIFICATION: title, carName: title };
					});
				}
				return payload;
			})
			.catch(function (err) { clearTimeout(ctimer); throw err; });
	}
	function api(action, params) {
		state.lastAction = action;
		var browseActions = { manufacturers: 1, models: 1, modifications: 1, cars: 1 };
		var cataCatalogActions = { categories: 1, articles: 1, products: 1 };
		if (CATA_BRIDGE.bridge && cataCatalogActions[action]) {
			return cataApi(action, params);
		}
		if (CATA_BRIDGE.use_cata_first && browseActions[action]) {
			return cataApi(action, params).then(function (payload) {
				if ((payload.data || []).length) {
					payload.source = payload.source || 'cata';
					return payload;
				}
				return partsApi(action, params);
			}).catch(function () { return partsApi(action, params); });
		}
		return partsApi(action, params);
	}
	function partsApi(action, params) {
		var q = new URLSearchParams();
		q.set('action', action);
		q.set('section', state.section);
		q.set('language', 'en');
		if (CATA_BRIDGE.cata_sync) { q.set('cata_sync', '1'); }
		Object.keys(params || {}).forEach(function (k) { if (params[k] != null && params[k] !== '') { q.set(k, params[k]); } });
		var ctrl = new AbortController();
		var timer = setTimeout(function () { ctrl.abort(); }, API_MS);
		return fetch((CATA_BRIDGE.partsapi_api || '/api/partsapi_proxy.php') + '?' + q.toString(), { credentials: 'same-origin', signal: ctrl.signal })
			.then(function (r) { clearTimeout(timer); return r.json().then(function (d) {
				if (!r.ok || d.ok === false) {
					if (!d.http_status) { d.http_status = r.status; }
					throw d;
				}
				return d;
			}); })
			.catch(function (err) { clearTimeout(timer); if (err && err.name === 'AbortError') { throw { error: 'Request timed out (' + (API_MS / 1000) + 's budget).' }; } throw err; });
	}
	function setStep(n) {
		renderStepPickerUi(n);
		renderFlowBar();
	}
	function renderVehicleBar() {
		if (!state.car) { vehicleBar.style.display = 'none'; renderFlowBar(); return; }
		var title = [state.make && state.make.MANUFACTURER, state.model && state.model.MODEL_SERIES, state.car.MODIFICATION || state.car.carName].filter(Boolean).join(' · ');
		var meta = [state.car.POWER_KW ? state.car.POWER_KW + ' kW' : '', state.car.FUEL_TYPE || ''].filter(Boolean).join(' · ');
		vehicleBar.style.display = '';
		vehicleBar.className = 'epc-vc-vehicle-bar';
		vehicleBar.innerHTML = '<div><strong>' + esc(title) + '</strong><span>' + esc(meta) + '</span></div>' +
			'<a class="btn btn-default btn-sm" href="' + esc(cartUrl()) + '"><i class="fa fa-shopping-cart"></i> Cart</a>';
		renderFlowBar();
	}
	function renderCategories() {
		state.view = 'categories';
		state.currentCategory = '';
		var mod = ui();
		if (!state.categories.length) {
			var catShop = shopUrlFor('categories', null);
			output.innerHTML = '<div class="epc-vc-message">No categories returned. An EParts API subscription for product categories (getSearchTree) is required. <a href="' + esc(catShop) + '" target="_blank" rel="noopener">Subscribe at EParts API</a></div>';
			done();
			renderFlowBar();
			return;
		}
		var barLabel = [state.make && state.make.MANUFACTURER, state.model && state.model.MODEL_SERIES].filter(Boolean).join(' · ') || 'Catalog';
		var cats = categoriesWithSubcategories(state.categories);
		var catOpts = {
			barLabel: barLabel,
			carModHeading: true,
			showIcons: PRESENTATION.showCategoryIcons !== false,
			iconBase: PRESENTATION.categoryIconBase || '/content/files/epc-cata/category-icons/',
			sortByOrder: true,
			subcategoriesMap: PRESENTATION.subcategoriesMap || {},
			showBar: false,
			search: true,
			initialLimit: 0
		};
		if (mod) {
			if (mod.setCategoryIconBase) { mod.setCategoryIconBase(catOpts.iconBase); }
			var ctx = mod.buildVehicleContext(state.make, state.model, state.car, { langHref: langHref, homeHref: langHref + '/eparts-mod' });
			output.innerHTML = mod.renderCategoryWorkspace(cats, Object.assign({ vehicleCtx: ctx }, catOpts));
			mod.bindCategoryWorkspace(output, cats, catOpts, function (strId, label, el, subId) {
				var subName = (el && el.classList && (el.classList.contains('epc-cm-cat-tree-subitem') || el.classList.contains('epc-cm-cat-flyout-item'))) ? label : (el && el.getAttribute ? el.getAttribute('data-sub-name') : '') || '';
				var catName = label;
				if (subName) {
					for (var i = 0; i < state.categories.length; i++) {
						if (String(state.categories[i].STR_ID || state.categories[i].CATEGORY_ID) === String(strId)) {
							catName = state.categories[i].CATEGORY_NAME || state.categories[i].name || catName;
							break;
						}
					}
				}
				loadArticles(strId, catName, subName);
			});
		} else {
			output.innerHTML = '<div class="epc-vc-section-bar">' + esc(barLabel) + ' · Categories</div><div class="epc-vc-cat-grid">' + state.categories.map(function (c) {
				return '<div class="epc-vc-cat-card" data-str="' + esc(c.STR_ID) + '"><strong>' + esc(c.CATEGORY_NAME || '') + '</strong></div>';
			}).join('') + '</div>';
		}
		done();
		if (consumePendingCategory()) { return; }
		applyFilterFromOutput();
		renderStepPickerUi(3);
		renderFlowBar();
	}
	function renderArticlesList(rows, catName, strId, subName) {
		var mod = ui();
		state.articleRows = rows;
		state.articleBrands = [];
		state.articleTerm = '';
		state.currentStrId = strId || '';
		state.currentSubcategory = subName || '';
		var partsListLayout = mod && mod.isServicePartsCategory ? mod.isServicePartsCategory(catName) : /service/i.test(catName || '');
		var panelOpts = {
			view: state.articleView || 'list',
			sort: state.articleSort || 'brand',
			activeBrands: state.articleBrands,
			term: state.articleTerm,
			partSearchUrl: partSearchUrl,
			productDetailUrl: productDetailUrl,
			carModLayout: true,
			partsListLayout: partsListLayout,
			categoryName: catName,
			filterTitle: partsListLayout ? 'Filter by brand' : 'Manufacturers',
			warehouseOnly: PRESENTATION.warehouseOnlyPrices !== false,
			checkPriceLabel: 'Check price',
			cartLabel: 'Cart',
			bindRows: function (container) {
				bindArticleRowNavigation(container);
				Array.prototype.forEach.call(container.querySelectorAll('.epc-cm-tdlist-item.is-clickable, .epc-cm-article-card, .epc-cm-article-compact'), function (card) {
					card.onclick = function (e) {
						if (e.target && e.target.closest && e.target.closest('a.btn, a.epc-cm-list-ask')) { return; }
						var href = card.getAttribute('data-product-url');
						if (!href) {
							var link = card.querySelector('a.epc-cm-tdname, a.epc-vc-article-link, .epc-cm-compact-oem a');
							href = link ? link.href : '';
						}
						if (href) { window.location.href = href; }
					};
				});
			}
		};
		if (mod && state.categories.length && state.car) {
			var ctx = mod.buildVehicleContext(state.make, state.model, state.car, { langHref: langHref });
			var built = mod.renderArticlesWorkspace(state.categories, rows, {
				vehicleCtx: ctx,
				activeStrId: state.currentStrId,
				activeSubName: state.currentSubcategory,
				subcategoriesMap: PRESENTATION.subcategoriesMap || {},
				homeHref: langHref + '/eparts-mod',
				panelOptions: panelOpts,
				bindRows: panelOpts.bindRows,
				onPanelChange: function (_visible, opts) {
					state.articleView = opts.view;
					state.articleSort = opts.sort;
					state.articleBrands = opts.activeBrands || [];
					state.articleTerm = opts.term || '';
				}
			});
			output.innerHTML = '<div id="epc-em-articles-workspace">' + built.html + '</div>';
			mod.bindArticlesWorkspace(output, state.categories, rows, {
				subcategoriesMap: PRESENTATION.subcategoriesMap || {},
				panelOptions: panelOpts,
				bindRows: panelOpts.bindRows,
				onPanelChange: function (_visible, opts) {
					state.articleView = opts.view;
					state.articleSort = opts.sort;
					state.articleBrands = opts.activeBrands || [];
					state.articleTerm = opts.term || '';
				}
			}, function (sid, label, el) {
				var sub = (el && el.classList && (el.classList.contains('epc-cm-cat-tree-subitem') || el.classList.contains('epc-cm-cat-flyout-item'))) ? label : '';
				var cname = label;
				if (sub) {
					for (var j = 0; j < state.categories.length; j++) {
						if (String(state.categories[j].STR_ID || state.categories[j].CATEGORY_ID) === String(sid)) {
							cname = state.categories[j].CATEGORY_NAME || state.categories[j].name || cname;
							break;
						}
					}
				}
				loadArticles(sid, cname, sub);
			});
		} else {
			var built = mod ? mod.renderArticlesPanel(rows, panelOpts) : { html: '<div class="epc-vc-articles">' + rows.map(renderArticleRow).join('') + '</div>' };
			output.innerHTML = '<div class="epc-vc-breadcrumb"><button type="button" id="epc-em-back-cat">← Categories</button> <span>/ ' + esc(catName) + '</span></div>' +
				'<div class="epc-vc-section-bar">' + esc(catName) + '</div>' +
				'<div id="epc-em-articles-panel">' + built.html + '</div>';
			document.getElementById('epc-em-back-cat').onclick = function () { loadCategories(); };
			if (mod) {
				mod.bindArticlesPanel(document.getElementById('epc-em-articles-panel'), rows, panelOpts, function (_visible, opts) {
					state.articleView = opts.view;
					state.articleSort = opts.sort;
					state.articleBrands = opts.activeBrands || [];
					state.articleTerm = opts.term || '';
				});
			} else {
				bindArticleRowNavigation(output);
			}
		}
		done();
		renderStepPickerUi(3);
		renderFlowBar();
	}
	function loadArticles(strId, catName, subName) {
		state.view = 'articles';
		state.currentCategory = catName || '';
		state.currentSubcategory = subName || '';
		state.currentStrId = strId || '';
		var displayName = subName ? (catName + ' · ' + subName) : catName;
		loading('Loading parts in ' + displayName + '…');
		api('articles', { carId: state.car.carId || state.car.ID, strId: strId }).then(function (payload) {
			var rows = filterRowsBySubcategory(apiItems(payload), subName);
			if (!rows.length) {
				output.innerHTML = '<div class="epc-vc-message">No articles in this category.</div>';
				done();
				renderFlowBar();
				return;
			}
			renderArticlesList(rows, displayName, strId, subName);
		}).catch(showErr);
	}
	function loadCategories() {
		if (!state.car) { return; }
		loading('Loading categories…');
		api('categories', { carId: state.car.carId || state.car.ID }).then(function (payload) {
			state.categories = categoriesWithSubcategories(apiItems(payload));
			renderCategories();
		}).catch(showErr);
	}
	function loadManufacturers() {
		state.mselectPanel = '';
		loading('Loading makes…');
		api('manufacturers', {}).then(function (payload) {
			state.manufacturers = apiItems(payload).sort(function (a, b) { return text(a.MANUFACTURER).localeCompare(text(b.MANUFACTURER)); });
			makeSelect.innerHTML = '<option value="">Make</option>' + state.manufacturers.map(function (m, i) {
				return '<option value="' + i + '">' + esc(m.MANUFACTURER) + '</option>';
			}).join('');
			makeSelect.disabled = false;
			if (state.catalogReady === false) {
				modelSelect.innerHTML = '<option value="">Model (subscribe to getModels)</option>';
				modelSelect.disabled = true;
				carSelect.innerHTML = '<option value="">Engine / modification</option>';
				carSelect.disabled = true;
				var modelsShop = shopUrlFor('models', null);
				showSubscriptionBanner(SUBSCRIPTION_MSG, modelsShop, 'models');
				state.make = state.model = state.car = null;
				vehicleBar.style.display = 'none';
				renderMakeDashboard();
				done();
				setStep(1);
				return;
			}
			modelSelect.innerHTML = '<option value="">Model</option>';
			modelSelect.disabled = true;
			carSelect.innerHTML = '<option value="">Engine / modification</option>';
			carSelect.disabled = true;
			state.make = state.model = state.car = null;
			vehicleBar.style.display = 'none';
			renderMakeDashboard();
			done();
			setStep(1);
		}).catch(showErr);
	}
	makeSelect.onchange = function () {
		var idx = makeSelect.value;
		if (idx === '') { state.make = null; setStep(1); renderMakeDashboard(); return; }
		if (state.catalogReady === false) {
			showSubscriptionBanner(SUBSCRIPTION_MSG, shopUrlFor('models', null), 'models');
			return;
		}
		state.make = state.manufacturers[parseInt(idx, 10)];
		setStep(2);
		loadModelsForMake(state.make, parseInt(idx, 10));
	};
	modelSelect.onchange = function () {
		var idx = modelSelect.value;
		if (idx === '') { state.model = null; setStep(2); if (state.make) { loadModelsForMake(state.make, state.manufacturers.indexOf(state.make)); } return; }
		state.model = state.models[parseInt(idx, 10)];
		setStep(3);
		loadCarsForModel(state.model);
	};
	carSelect.onchange = function () {
		var idx = carSelect.value;
		if (idx === '') { state.car = null; vehicleBar.style.display = 'none'; return; }
		state.car = state.cars[parseInt(idx, 10)];
		renderVehicleBar();
		loadCategories();
	};
	Array.prototype.forEach.call(document.querySelectorAll('#epc-em-tabs .epc-vc-tab'), function (tab) {
		tab.onclick = function () {
			Array.prototype.forEach.call(document.querySelectorAll('#epc-em-tabs .epc-vc-tab'), function (t) { t.classList.remove('active'); });
			tab.classList.add('active');
			state.section = tab.getAttribute('data-section');
			if (state.demoMode) { loadDemoMode(); }
			else { loadManufacturers(); }
		};
	});
	document.getElementById('epc-em-search-go').onclick = function () {
		var part = (partInput.value || '').trim();
		var vin = (vinInput && PRESENTATION.enableVinSearch !== false) ? (vinInput.value || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase() : '';
		if (part) {
			loading('Searching part number…');
			api('part_search', { article: part }).then(function (payload) {
				var rows = apiItems(payload);
				if (!rows.length) {
					return api('crosses', { article: part }).then(function (crossPayload) {
						var crosses = apiItems(crossPayload);
						if (!crosses.length) { throw { error: 'No matches or crosses found.' }; }
						output.innerHTML = '<div class="epc-vc-section-bar">Cross references</div><h3 class="epc-vc-section-title">Cross references for ' + esc(part) + '</h3><div class="epc-vc-articles">' + crosses.map(renderArticleRow).join('') + '</div>';
						bindArticleRowNavigation(output);
						done();
					});
				}
				output.innerHTML = '<div class="epc-vc-section-bar">Search results</div><h3 class="epc-vc-section-title">Search results</h3><div class="epc-vc-articles">' + rows.map(renderArticleRow).join('') + '</div>';
				bindArticleRowNavigation(output);
				done();
			}).catch(showErr);
			return;
		}
		if (vin.length >= 11) {
			loading('Decoding ' + epcVinLabel + '…');
			api('vin', { vin: vin }).then(function (payload) {
				var data = payload.data || payload;
				var vehicles = apiItems(data.matchingVehicles);
				if (!vehicles.length) { throw { error: 'No vehicle found for this ' + epcVinLabel + '.' }; }
				output.innerHTML = '<div class="epc-vc-section-bar">' + epcVinLabel + ' match</div><h3 class="epc-vc-section-title">' + epcVinLabel + ' match</h3><div class="epc-vc-articles">' + vehicles.map(function (v, i) {
					return '<div class="epc-vc-article-row" data-search="' + esc(v.carName || '') + '"><div class="epc-vc-article-thumb"><i class="fa fa-car"></i></div><div><strong>' + esc(v.carName || 'Vehicle') + '</strong><br><small>carId ' + esc(v.carId) + '</small></div><div class="epc-vc-article-actions"><button type="button" class="btn btn-default btn-sm" data-vin-car="' + i + '">Use vehicle</button></div></div>';
				}).join('') + '</div>';
				Array.prototype.forEach.call(output.querySelectorAll('[data-vin-car]'), function (btn) {
					btn.onclick = function () {
						var v = vehicles[parseInt(btn.getAttribute('data-vin-car'), 10)];
						state.car = { carId: v.carId, ID: v.carId, MODIFICATION: v.carName, carName: v.carName };
						state.make = { MFA_ID: v.manuId, MANUFACTURER: '' };
						state.model = { MS_ID: v.modelId, MODEL_SERIES: '' };
						renderVehicleBar();
						loadCategories();
					};
				});
				done();
			}).catch(showErr);
			return;
		}
		alert('Enter a part number or ' + epcVinLabel + ' (11+ characters).');
	};
	if (state.demoMode) {
		renderStepPickerUi(1);
		loadDemoMode();
	} else if (<?php echo $epc_em_configured ? 'true' : 'false'; ?>) {
		loading('Checking EParts API subscriptions…');
		api('capabilities', {}).then(function (statusPayload) {
			applyCatalogGate(statusPayload);
			loadManufacturers();
		}).catch(function () {
			state.catalogReady = null;
			loadManufacturers();
		});
	} else {
		output.innerHTML = '<div class="epc-vc-message">EParts API key not configured on server.</div>';
	}
})();
</script>
