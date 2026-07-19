<?php
defined('_ASTEXE_') or die('No access');
$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') ? rtrim($multilang_params['lang_href'], '/') : '/en';
$default_country = isset($_GET['country']) ? strtoupper(trim((string)$_GET['country'])) : '';
$default_brand = isset($_GET['brand']) ? trim((string)$_GET['brand']) : '';
$default_article = isset($_GET['article']) ? trim((string)$_GET['article']) : '';
$epc_demand_chpu_on = !empty($DP_Config->chpu_search_config['chpu_search_on']);
$epc_demand_chpu_parts_url = !empty($DP_Config->chpu_search_config['level_1']['url']) ? $DP_Config->chpu_search_config['level_1']['url'] : 'parts';
$epc_demand_chpu_brands_url = !empty($DP_Config->chpu_search_config['level_2']['mode_1']['url']) ? $DP_Config->chpu_search_config['level_2']['mode_1']['url'] : 'brands';
$epc_demand_chpu_slash_code = isset($DP_Config->chpu_search_config['slash_code']) ? $DP_Config->chpu_search_config['slash_code'] : '%2F';

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_demand_intelligence.php';
epc_demand_bootstrap_db_link();
epc_demand_load_dp_user();
$epc_di_user_id = (int)DP_User::getUserId();
$epc_di_admin_id = (int)DP_User::getAdminId();
$epc_di_logged_in = $epc_di_user_id > 0 || $epc_di_admin_id > 0;
$epc_di_access = array(
	'is_admin' => false,
	'country_locked' => false,
	'user_country' => '',
	'user_country_name' => '',
	'allowed_codes' => array(),
	'allowed_countries' => array(),
	'default_country' => '',
);
if ($epc_di_logged_in) {
	global $db_link;
	epc_demand_bootstrap_db_link();
	if (isset($db_link) && $db_link) {
		try {
			epc_demand_ensure_schema($db_link);
		} catch (Throwable $e) {
		}
		$epc_di_access = epc_demand_access_context($db_link);
	} elseif ($epc_di_admin_id > 0) {
		try {
			$db = new PDO(
				'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
				$DP_Config->user,
				$DP_Config->password
			);
			epc_demand_ensure_schema($db);
			$epc_di_access = epc_demand_access_context($db);
		} catch (Throwable $e) {
		}
	}
}
$epc_di_default_country = isset($_GET['country']) ? strtoupper(trim((string)$_GET['country'])) : '';
if ($epc_di_default_country === '' || !in_array($epc_di_default_country, $epc_di_access['allowed_codes'], true)) {
	$epc_di_default_country = (string)$epc_di_access['default_country'];
}
?>
<style>
.epc-di { margin: 0 0 32px; }
.epc-di-title { margin: 0 0 8px; font-size: 24px; font-weight: 700; color: #172536; }
.epc-di-lead { margin: 0 0 16px; color: #657184; font-size: 14px; max-width: 900px; }
.epc-di-start { background: #fff; border: 1px solid #e1e7ef; border-radius: 10px; padding: 16px; margin-bottom: 16px; display: flex; flex-wrap: wrap; gap: 12px; align-items: flex-end; }
.epc-di-start label { font-weight: 700; display: block; margin-bottom: 4px; color: #172536; }
.epc-di-start .form-control { min-width: 200px; }
.epc-di-panel { background: #fff; border: 1px solid #e1e7ef; border-radius: 10px; padding: 16px; }
.epc-di-steps { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 14px; }
.epc-di-step { border: 1px solid #d7dee9; background: #f8fafc; border-radius: 20px; padding: 6px 12px; font-size: 13px; font-weight: 600; color: #64748b; }
.epc-di-step.active { background: #2b78d6; border-color: #2b78d6; color: #fff; }
.epc-di-step.done { background: #e8f5e9; border-color: #86efac; color: #166534; }
.epc-di-toolbar { display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 12px; }
.epc-di-tabs { display: flex; gap: 8px; margin-bottom: 12px; }
.epc-di-tab { border: 1px solid #d7dee9; background: #fff; border-radius: 6px; padding: 8px 12px; font-weight: 600; cursor: pointer; }
.epc-di-tab.active { background: #2b78d6; color: #fff; border-color: #2b78d6; }
.epc-di-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; }
.epc-di-card { border: 1px solid #e1e7ef; border-radius: 8px; padding: 12px; cursor: pointer; background: #fff; text-align: left; min-height: 72px; }
.epc-di-card:hover { border-color: #2b78d6; box-shadow: 0 4px 12px rgba(43,120,214,.12); }
.epc-di-card strong { display: block; color: #172536; }
.epc-di-card small { color: #64748b; }
.epc-di-list { display: grid; gap: 8px; }
.epc-di-row { border: 1px solid #e1e7ef; border-radius: 8px; padding: 10px 12px; cursor: pointer; background: #fff; }
.epc-di-row:hover { background: #f0f7ff; }
.epc-di-row.is-active { background: #dcecff; border-color: #2b78d6; }
.epc-di-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.epc-di-table th, .epc-di-table td { border: 1px solid #e1e7ef; padding: 8px; vertical-align: top; }
.epc-di-table th { background: #f5f7fa; color: #64748b; }
.epc-di-tag { display: inline-block; padding: 2px 8px; border-radius: 999px; font-size: 11px; font-weight: 700; background: #fef3c7; color: #92400e; }
.epc-di-tag--ok { background: #d1e7dd; color: #198754; }
.epc-di-tag--no { background: #f8d7da; color: #842029; }
.epc-di-tag--ae { background: #e8f5e9; color: #1b5e20; }
.epc-di-loader, .epc-di-msg { padding: 24px; text-align: center; color: #64748b; }
.epc-di-msg { background: #fff8e1; border: 1px solid #f0d98a; border-radius: 8px; }
.epc-di-cross { margin-top: 16px; border-top: 2px solid #e1e7ef; padding-top: 16px; }
.epc-di-section-title { margin: 0 0 10px; font-size: 16px; font-weight: 700; }
.epc-di-hidden { display: none !important; }
.epc-di-progress { background: #e8edf3; border-radius: 8px; height: 14px; overflow: hidden; margin: 12px 0 8px; }
.epc-di-progress__bar { background: linear-gradient(90deg, #2b78d6, #5b9ef0); height: 100%; width: 0; transition: width .25s ease; border-radius: 8px; }
.epc-di-progress__msg { font-size: 13px; color: #475569; margin: 0 0 8px; text-align: center; }
.epc-di-progress__meta { font-size: 12px; color: #94a3b8; text-align: center; margin-bottom: 12px; }
.epc-di-summary { display: grid; grid-template-columns: repeat(auto-fill, minmax(130px, 1fr)); gap: 10px; margin: 0 0 18px; }
.epc-di-summary__card { background: #f8fafc; border: 1px solid #e1e7ef; border-radius: 8px; padding: 12px 10px; text-align: center; }
.epc-di-summary__card strong { display: block; font-size: 22px; line-height: 1.2; color: #172536; }
.epc-di-summary__card span { font-size: 12px; color: #64748b; }
.epc-di-block { margin-bottom: 20px; }
.epc-di-product-card { border: 1px solid #e1e7ef; border-radius: 8px; padding: 12px; background: #fff; min-height: 100px; cursor: pointer; transition: border-color .15s, box-shadow .15s; }
.epc-di-product-card:hover { border-color: #2b78d6; box-shadow: 0 4px 12px rgba(43,120,214,.12); }
.epc-di-product-card.is-active { border-color: #2b78d6; background: #f0f7ff; }
.epc-di-product-card strong { display: block; color: #172536; margin-bottom: 6px; }
.epc-di-product-card small { display: block; color: #64748b; font-size: 12px; line-height: 1.4; }
.epc-di-product-card small.epc-di-brands-line { max-height: 3.6em; overflow: hidden; }
.epc-di-more-brands { color: #2b78d6; font-weight: 600; }
.epc-di-product-card__hint { display: block; margin-top: 8px; font-size: 11px; color: #2b78d6; font-weight: 600; }
.epc-di-brands-block { margin-bottom: 18px; }
.epc-di-parts-toggle { margin-bottom: 10px; }
.epc-di-downloads { display: flex; flex-wrap: wrap; gap: 8px; margin: 0 0 16px; padding: 12px; background: #f0f7ff; border: 1px solid #c5daf5; border-radius: 8px; }
.epc-di-downloads .btn { white-space: nowrap; }
.epc-di-vehicle-dl { margin: 0 0 12px; }
.epc-di-login-panel { background: #fff; border: 1px solid #e1e7ef; border-radius: 10px; padding: 20px; margin-bottom: 20px; }
.epc-di-login-panel h2 { margin: 0 0 8px; font-size: 20px; }
.epc-di-country-locked { padding: 10px 14px; background: #f0f7ff; border: 1px solid #c5daf5; border-radius: 8px; font-weight: 700; color: #172536; min-width: 200px; }
.epc-di-country-note { font-size: 12px; color: #64748b; margin-top: 4px; }
@media (max-width: 767px) {
	.epc-di-start .form-control { min-width: 100%; }
}
</style>

<div class="epc-di" id="epc-demand-wizard"
	data-lang="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>"
	data-card-api="/content/shop/docpart/ajax_epc_demand_card.php"
	data-tags-api="/content/shop/docpart/ajax_epc_demand_tags_index.php"
	data-vehicles-api="/content/shop/docpart/ajax_epc_demand_country_vehicles.php"
	data-deeplink-brand="<?php echo htmlspecialchars($default_brand, ENT_QUOTES, 'UTF-8'); ?>"
	data-deeplink-article="<?php echo htmlspecialchars($default_article, ENT_QUOTES, 'UTF-8'); ?>"
	data-logged-in="<?php echo $epc_di_logged_in ? '1' : '0'; ?>"
	data-access="<?php echo htmlspecialchars(json_encode($epc_di_access, JSON_UNESCAPED_UNICODE), ENT_QUOTES, 'UTF-8'); ?>"
	data-meta-api="/content/shop/docpart/ajax_epc_demand_meta.php">
	<h1 class="epc-di-title">Vehicle Parts intelligence AI</h1>
<?php if (!$epc_di_logged_in) { ?>
	<div class="epc-di-login-panel">
		<h2><i class="fa fa-lock"></i> Sign in required</h2>
		<p class="epc-di-lead">Vehicle Parts intelligence AI is available only for <strong>registered customers</strong>. Please log in to scan country demand, product lines, and vehicle fitment.</p>
		<div class="panel panel-primary">
		<?php
		$login_form_postfix = 'vehicle_intelligence';
		require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
		?>
		</div>
	</div>
<?php } else { ?>
	<p class="epc-di-lead" id="epc-di-intro-lead">
		<?php if (!empty($epc_di_access['is_admin'])) { ?>
		Choose a <strong>demand country</strong> (admin: all countries), press <strong>Start</strong>.
		<?php } elseif (!empty($epc_di_access['country_locked'])) { ?>
		Your account is linked to demand country <strong><?php echo htmlspecialchars($epc_di_access['user_country_name'], ENT_QUOTES, 'UTF-8'); ?></strong>. Press <strong>Start</strong> to load your market intelligence.
		<?php } else { ?>
		No demand country is assigned to your account. Please contact support.
		<?php } ?>
		The system scans <strong>price-list parts</strong>, loads <strong>vehicle fitment</strong>, then you pick vehicle → category → product → part → crosses (UAE stock).
	</p>

	<div class="epc-di-start">
		<div id="epc-di-country-wrap">
			<label for="epc-di-country">1 · Demand country</label>
			<?php if (!empty($epc_di_access['country_locked']) && !empty($epc_di_access['default_country'])) { ?>
			<div class="epc-di-country-locked" id="epc-di-country-locked"><?php echo htmlspecialchars($epc_di_access['user_country_name'] . ' (' . $epc_di_access['default_country'] . ')', ENT_QUOTES, 'UTF-8'); ?></div>
			<select class="form-control epc-di-hidden" id="epc-di-country" aria-hidden="true">
				<option value="<?php echo htmlspecialchars($epc_di_access['default_country'], ENT_QUOTES, 'UTF-8'); ?>" selected><?php echo htmlspecialchars($epc_di_access['user_country_name'], ENT_QUOTES, 'UTF-8'); ?></option>
			</select>
			<?php } else { ?>
			<select class="form-control" id="epc-di-country">
				<option value="">Select country…</option>
				<?php foreach ($epc_di_access['allowed_countries'] as $c) { ?>
				<option value="<?php echo htmlspecialchars($c['code'], ENT_QUOTES, 'UTF-8'); ?>"<?php echo $epc_di_default_country === $c['code'] ? ' selected' : ''; ?>><?php echo htmlspecialchars($c['name'] . ' (' . $c['code'] . ')', ENT_QUOTES, 'UTF-8'); ?></option>
				<?php } ?>
			</select>
			<?php } ?>
			<?php if (!empty($epc_di_access['is_admin'])) { ?>
			<p class="epc-di-country-note">Administrator: all demand countries visible.</p>
			<?php } ?>
		</div>
		<button type="button" class="btn btn-primary btn-lg" id="epc-di-start-btn"<?php echo empty($epc_di_access['allowed_codes']) ? ' disabled' : ''; ?>><i class="fa fa-play"></i> Start</button>
	</div>

	<div class="epc-di-panel epc-di-hidden" id="epc-di-workspace">
		<div class="epc-di-steps" id="epc-di-steps"></div>
		<div class="epc-di-tabs" id="epc-di-section-tabs">
			<button type="button" class="epc-di-tab active" data-section="passenger">Passenger</button>
			<button type="button" class="epc-di-tab" data-section="commercial">Commercial</button>
			<button type="button" class="epc-di-tab" data-section="motorbike">Motorbike</button>
		</div>
		<div class="epc-di-toolbar">
			<input type="search" class="form-control" id="epc-di-filter" placeholder="Filter current list" style="max-width:280px;">
			<button type="button" class="btn btn-default btn-sm" id="epc-di-back-btn"><i class="fa fa-arrow-left"></i> Back</button>
		</div>
		<div id="epc-di-main"><div class="epc-di-msg">Press Start after choosing a demand country.</div></div>
		<div class="epc-di-cross epc-di-hidden" id="epc-di-cross">
			<h2 class="epc-di-section-title" id="epc-di-cross-title">Crosses &amp; demand</h2>
			<div id="epc-di-cross-body"></div>
		</div>
	</div>
<?php } ?>
</div>

<script>
(function () {
	'use strict';
	var root = document.getElementById('epc-demand-wizard');
	if (!root) { return; }
	if (root.getAttribute('data-logged-in') !== '1') { return; }

	var langHref = root.getAttribute('data-lang') || '';
	var cardApi = root.getAttribute('data-card-api') || '';
	var tagsApi = root.getAttribute('data-tags-api') || '';
	var vehiclesApi = root.getAttribute('data-vehicles-api') || '';
	var countrySelect = document.getElementById('epc-di-country');
	var startBtn = document.getElementById('epc-di-start-btn');
	var workspace = document.getElementById('epc-di-workspace');
	var stepsEl = document.getElementById('epc-di-steps');
	var sectionTabs = document.getElementById('epc-di-section-tabs');
	var filterInput = document.getElementById('epc-di-filter');
	var backBtn = document.getElementById('epc-di-back-btn');
	var mainEl = document.getElementById('epc-di-main');
	var crossPanel = document.getElementById('epc-di-cross');
	var crossTitle = document.getElementById('epc-di-cross-title');
	var crossBody = document.getElementById('epc-di-cross-body');

	var epcDiChpuOn = <?php echo $epc_demand_chpu_on ? 'true' : 'false'; ?>;
	var epcDiPartsUrl = <?php echo json_encode($epc_demand_chpu_parts_url, JSON_UNESCAPED_UNICODE); ?>;
	var epcDiBrandsUrl = <?php echo json_encode($epc_demand_chpu_brands_url, JSON_UNESCAPED_UNICODE); ?>;
	var epcDiSlash = <?php echo json_encode($epc_demand_chpu_slash_code, JSON_UNESCAPED_UNICODE); ?>;

	var access = {
		is_admin: false,
		country_locked: false,
		allowed_codes: [],
		allowed_countries: [],
		default_country: '',
		user_country: '',
		user_country_name: ''
	};
	try {
		access = JSON.parse(root.getAttribute('data-access') || '{}');
	} catch (eAccess) { /* ignore */ }
	if (!access.allowed_codes) { access.allowed_codes = []; }
	if (!access.allowed_countries) { access.allowed_countries = []; }

	var state = {
		started: false,
		demandCountry: '',
		demandCountryName: '',
		step: 'vehicle',
		section: 'passenger',
		vehicleType: 'PC',
		manufacturer: null,
		model: null,
		modification: null,
		categories: null,
		categoryId: '',
		categoryLabel: '',
		productLabel: '',
		demandIndex: {},
		articles: [],
		lastProductId: '',
		countryVehicles: [],
		countryPartLines: [],
		countryProducts: [],
		countrySummary: null,
		vehiclePartsTotal: 0,
		showPartsTable: false,
		selectedProductLine: ''
	};

	function esc(s) {
		return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
	}
	function countryCodeFromUi() {
		return countrySelect ? (countrySelect.value || '') : '';
	}
	function countryNameForCode(code) {
		var list = access.allowed_countries || [];
		for (var i = 0; i < list.length; i++) {
			if (list[i].code === code) {
				return list[i].name + ' (' + list[i].code + ')';
			}
		}
		return code;
	}
	function isCountryAllowed(code) {
		return code && (access.allowed_codes || []).indexOf(code) !== -1;
	}
	function apiErrorMessage(d, fallback) {
		if (d && d.code === 'forbidden') { return d.message || 'Country not allowed for your account.'; }
		if (d && d.code === 'auth') { return d.message || 'Please sign in.'; }
		return fallback;
	}
	function normArticle(a) {
		return String(a || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
	}
	function partKey(brand, article) {
		return String(brand || '').trim().toUpperCase() + '|' + normArticle(article);
	}
	function shopUrl(brand, article) {
		var n = normArticle(article);
		var b = String(brand || '').trim();
		if (!n) { return langHref + '/shop/part_search'; }
		if (epcDiChpuOn) {
			if (!b) { return langHref + '/' + epcDiPartsUrl + '/' + epcDiBrandsUrl + '/' + encodeURIComponent(n); }
			return langHref + '/' + epcDiPartsUrl + '/' + encodeURIComponent(b.split('/').join(epcDiSlash)) + '/' + encodeURIComponent(n);
		}
		return langHref + '/shop/part_search?article=' + encodeURIComponent(n) + (b ? '&brend=' + encodeURIComponent(b) : '');
	}
	function loading(msg) {
		mainEl.innerHTML = '<div class="epc-di-loader">' + esc(msg || 'Loading…') + '</div>';
	}
	function showVehicleProgress(percent, message, meta) {
		var pct = Math.max(0, Math.min(100, parseInt(percent, 10) || 0));
		mainEl.innerHTML = '<h2 class="epc-di-section-title">2 · Vehicles for ' + esc(state.demandCountryName) + ' demand parts</h2>'
			+ '<p class="epc-di-lead">Scanning UAE price-list lines tagged for <strong>' + esc(state.demandCountry) + '</strong> — product lines, part names, and vehicle fitment…</p>'
			+ '<div class="epc-di-progress" aria-hidden="true"><div class="epc-di-progress__bar" style="width:' + pct + '%"></div></div>'
			+ '<p class="epc-di-progress__msg">' + esc(message || 'Working…') + '</p>'
			+ (meta ? '<p class="epc-di-progress__meta">' + esc(meta) + '</p>' : '');
	}
	function vehicleSectionFromRow(row) {
		if (row.PC_ID) { return 'PC'; }
		if (row.CV_ID) { return 'CV'; }
		if (row.MTB_ID) { return 'MTB'; }
		return row._vehicle_type || 'PC';
	}
	function vehicleTabSection() {
		if (state.section === 'commercial') { return 'CV'; }
		if (state.section === 'motorbike') { return 'MTB'; }
		return 'PC';
	}
	function vehicleYearRange(row) {
		var from = row.CI_FROM || '';
		var to = row.CI_TO || '';
		if (from && to) { return from + ' – ' + to; }
		if (from) { return from + ' – now'; }
		return to || '';
	}
	function vehicleModText(row) {
		return row.PASSENGER_CAR || row.COMMERCIAL_VEHICLE || row.MOTORBIKE || row.MODIFICATION || '';
	}
	function vehicleEngineText(row) {
		return [row.CAPACITY_TECH || row.CAPACITY_LT || '', row.FUEL_TYPE || '', row.BODY_TYPE || row.PLATFORM_TYPE || ''].filter(Boolean).join(' / ');
	}
	function filterVehiclesForTab(vehicles) {
		var want = vehicleTabSection();
		return (vehicles || []).filter(function (row) {
			return vehicleSectionFromRow(row) === want;
		});
	}
	function pickVehicle(row) {
		state.modification = row;
		state.vehicleType = vehicleSectionFromRow(row);
		state.manufacturer = {
			MANUFACTURER: row.MANUFACTURER || '',
			MFA_ID: row.MFA_ID || row.MFA_BRAND || ''
		};
		state.model = {
			MODEL_SERIES: row.MODEL_SERIES || '',
			MS_ID: row.MS_ID || ''
		};
		loadCategories();
	}
	function csvCell(value) {
		var text = String(value == null ? '' : value);
		if (/[",\r\n]/.test(text)) {
			return '"' + text.replace(/"/g, '""') + '"';
		}
		return text;
	}
	function downloadCsv(filename, headerRow, dataRows) {
		var lines = [headerRow].concat(dataRows || []);
		var csv = lines.map(function (row) {
			return row.map(csvCell).join(',');
		}).join('\r\n');
		var blob = new Blob(['\ufeff' + csv], { type: 'text/vnd.ms-excel;charset=utf-8;' });
		var link = document.createElement('a');
		link.href = URL.createObjectURL(blob);
		link.download = filename;
		document.body.appendChild(link);
		link.click();
		window.setTimeout(function () {
			URL.revokeObjectURL(link.href);
			if (link.parentNode) { link.parentNode.removeChild(link); }
		}, 100);
	}
	function countryFilePrefix() {
		return 'vehicle-intelligence-' + String(state.demandCountry || 'country').toLowerCase() + '-';
	}
	function brandsSummaryList() {
		var s = state.countrySummary || {};
		if (s.brands && s.brands.length) { return s.brands; }
		var map = {};
		(state.countryPartLines || []).forEach(function (line) {
			var b = String(line.brand || '').trim();
			if (!b) { return; }
			var key = b.toUpperCase();
			if (!map[key]) { map[key] = { brand: b, parts_count: 0, total_qty: 0, product_groups: [] }; }
			map[key].parts_count++;
			map[key].total_qty += parseFloat(line.qty) || 0;
			var g = line.product_group || '';
			if (g && map[key].product_groups.indexOf(g) === -1) { map[key].product_groups.push(g); }
		});
		return Object.keys(map).map(function (k) { return map[k]; }).sort(function (a, b) {
			return (b.parts_count || 0) - (a.parts_count || 0);
		});
	}
	function getProductByLabel(label) {
		var target = String(label || '');
		var list = state.countryProducts || [];
		for (var i = 0; i < list.length; i++) {
			if (list[i].label === target) { return list[i]; }
		}
		return null;
	}
	function partsForProduct(product) {
		if (product && product.parts && product.parts.length) { return product.parts; }
		var label = product ? product.label : state.selectedProductLine;
		return (state.countryPartLines || []).filter(function (line) {
			return line.product_group === label;
		});
	}
	function downloadSummaryExcel() {
		var s = state.countrySummary || {};
		var rows = [
			['Country', (s.country_name || '') + ' (' + (s.country_code || state.demandCountry) + ')'],
			['Parts in price list', s.parts_count || 0],
			['Product lines', s.product_groups_count || 0],
			['Brands (suppliers)', s.brands_count || brandsSummaryList().length],
			['Vehicles (fitment)', s.vehicles_count || 0],
			['Makes', s.makes_count || 0],
			['UAE stock qty', s.total_stock_qty != null ? s.total_stock_qty : ''],
			['Exported', new Date().toISOString().slice(0, 19).replace('T', ' ')]
		];
		brandsSummaryList().forEach(function (b) {
			rows.push(['Brand: ' + b.brand, (b.parts_count || 0) + ' lines, qty ' + Math.round(b.total_qty || 0)]);
		});
		downloadCsv(countryFilePrefix() + 'summary.csv', ['Metric', 'Value'], rows);
	}
	function downloadProductCategoryExcel(label) {
		var product = getProductByLabel(label);
		var parts = partsForProduct(product);
		var rows = parts.map(function (p) {
			return [p.brand || '', p.article || '', p.name || '', p.qty || 0];
		});
		var safe = String(label || 'category').replace(/[^\w\-]+/g, '_').toLowerCase();
		downloadCsv(countryFilePrefix() + 'product-' + safe + '.csv',
			['Brand', 'Article', 'Name', 'UAE qty'], rows);
	}
	function downloadProductsExcel() {
		var rows = (state.countryProducts || []).map(function (p) {
			var brands = (p.brands || []).map(function (b) { return b.brand; }).join('; ');
			var samples = (p.samples || []).map(function (s) { return (s.brand || '') + ' ' + (s.article || ''); }).join('; ');
			return [p.label || '', p.parts_count || 0, Math.round(p.total_qty || 0), brands, samples];
		});
		downloadCsv(countryFilePrefix() + 'product-lines.csv',
			['Product line', 'Part lines', 'UAE qty', 'Brands', 'Samples'],
			rows);
	}
	function downloadPartsExcel() {
		var rows = (state.countryPartLines || []).map(function (line) {
			return [line.brand || '', line.article || '', line.name || '', line.product_group || '', line.qty || 0];
		});
		downloadCsv(countryFilePrefix() + 'parts.csv',
			['Brand', 'Article', 'Name', 'Product line', 'UAE qty'],
			rows);
	}
	function vehicleRowsForExport(vehicles) {
		return (vehicles || []).map(function (row) {
			return [
				row.MANUFACTURER || '',
				row.MODEL_SERIES || '',
				vehicleModText(row),
				vehicleYearRange(row),
				vehicleEngineText(row),
				row._parts_count || 1,
				vehicleSectionFromRow(row)
			];
		});
	}
	function downloadVehiclesExcel(allTypes) {
		var vehicles = allTypes ? (state.countryVehicles || []) : filterVehiclesForTab(state.countryVehicles || []);
		var tab = allTypes ? 'all' : (state.section === 'commercial' ? 'commercial' : (state.section === 'motorbike' ? 'motorbike' : 'passenger'));
		if (!vehicles.length) {
			alert('No vehicles to export for this selection.');
			return;
		}
		downloadCsv(countryFilePrefix() + 'vehicles-' + tab + '.csv',
			['Make', 'Model', 'Modification', 'Years', 'Engine', 'Demand parts', 'Type'],
			vehicleRowsForExport(vehicles));
	}
	function vehicleTabLabel() {
		return state.section === 'commercial' ? 'Commercial' : (state.section === 'motorbike' ? 'Motorbike' : 'Passenger');
	}
	function renderVehicleFitmentDownloads() {
		var all = state.countryVehicles || [];
		var tabRows = filterVehiclesForTab(all);
		if (!all.length) { return ''; }
		return '<div class="epc-di-downloads epc-di-vehicle-dl" id="epc-di-vehicle-dl">'
			+ '<button type="button" class="btn btn-success btn-sm" data-vehicle-dl="tab"><i class="fa fa-file-excel-o"></i> Download Excel — '
			+ esc(vehicleTabLabel()) + ' (' + tabRows.length + ')</button>'
			+ '<button type="button" class="btn btn-success btn-sm" data-vehicle-dl="all"><i class="fa fa-file-excel-o"></i> Download Excel — all vehicles (' + all.length + ')</button>'
			+ '</div>';
	}
	function bindVehicleFitmentDownloads() {
		var bar = document.getElementById('epc-di-vehicle-dl');
		if (!bar) { return; }
		Array.prototype.forEach.call(bar.querySelectorAll('[data-vehicle-dl]'), function (btn) {
			btn.onclick = function () {
				downloadVehiclesExcel(btn.getAttribute('data-vehicle-dl') === 'all');
			};
		});
	}
	function renderDownloadToolbar() {
		var hasData = (state.countryPartLines && state.countryPartLines.length) || (state.countryVehicles && state.countryVehicles.length);
		if (!hasData) { return ''; }
		return '<div class="epc-di-downloads" id="epc-di-downloads">'
			+ '<button type="button" class="btn btn-success btn-sm" data-dl="summary"><i class="fa fa-file-excel-o"></i> Summary</button>'
			+ '<button type="button" class="btn btn-success btn-sm" data-dl="products"><i class="fa fa-file-excel-o"></i> Product lines</button>'
			+ '<button type="button" class="btn btn-success btn-sm" data-dl="parts"><i class="fa fa-file-excel-o"></i> All parts</button>'
			+ '<button type="button" class="btn btn-success btn-sm" data-dl="vehicles-tab"><i class="fa fa-file-excel-o"></i> Vehicles (' + esc(state.section === 'commercial' ? 'Commercial' : (state.section === 'motorbike' ? 'Motorbike' : 'Passenger')) + ')</button>'
			+ '<button type="button" class="btn btn-success btn-sm" data-dl="vehicles-all"><i class="fa fa-file-excel-o"></i> All vehicles</button>'
			+ '</div>';
	}
	function bindDownloadToolbar() {
		var bar = document.getElementById('epc-di-downloads');
		if (!bar) { return; }
		Array.prototype.forEach.call(bar.querySelectorAll('[data-dl]'), function (btn) {
			btn.onclick = function () {
				var kind = btn.getAttribute('data-dl');
				if (kind === 'summary') { downloadSummaryExcel(); return; }
				if (kind === 'products') { downloadProductsExcel(); return; }
				if (kind === 'parts') { downloadPartsExcel(); return; }
				if (kind === 'vehicles-tab') { downloadVehiclesExcel(false); return; }
				if (kind === 'vehicles-all') { downloadVehiclesExcel(true); return; }
			};
		});
	}
	function applyStep2Payload(payload) {
		if (!payload) { return; }
		if (payload.vehicles) { state.countryVehicles = payload.vehicles; }
		if (payload.part_lines) { state.countryPartLines = payload.part_lines; }
		if (payload.products) { state.countryProducts = payload.products; }
		if (payload.summary) { state.countrySummary = payload.summary; }
		if (payload.parts_total) { state.vehiclePartsTotal = payload.parts_total; }
	}
	function renderSummaryCards(summary) {
		var s = summary || state.countrySummary || {};
		var brands = brandsSummaryList();
		var cards = [
			{ n: s.parts_count || state.countryPartLines.length || 0, label: 'Parts in price list' },
			{ n: s.product_groups_count || state.countryProducts.length || 0, label: 'Product lines' },
			{ n: s.brands_count || brands.length || 0, label: 'Brands (suppliers)' },
			{ n: s.vehicles_count || state.countryVehicles.length || 0, label: 'Vehicles (fitment)' },
			{ n: s.makes_count || 0, label: 'Makes' },
			{ n: s.total_stock_qty != null ? Math.round(s.total_stock_qty) : '—', label: 'UAE stock qty' }
		];
		var html = '<div class="epc-di-summary">' + cards.map(function (c) {
			return '<div class="epc-di-summary__card"><strong>' + esc(c.n) + '</strong><span>' + esc(c.label) + '</span></div>';
		}).join('') + '</div>';
		if (!brands.length) { return html; }
		html += '<div class="epc-di-brands-block"><h3 class="epc-di-section-title">Brands in this country demand</h3>'
			+ '<p class="epc-di-lead">Supplier brands from your UAE price list for <strong>' + esc(state.demandCountry) + '</strong>.</p>'
			+ '<table class="epc-di-table"><thead><tr><th>Brand</th><th>Part lines</th><th>UAE qty</th><th>Product lines</th></tr></thead><tbody>'
			+ brands.map(function (b) {
				var groups = (b.product_groups || []).join(', ');
				var search = [b.brand, groups].join(' ');
				return '<tr data-search="' + esc(search) + '"><td><strong>' + esc(b.brand) + '</strong></td>'
					+ '<td>' + esc(b.parts_count || 0) + '</td><td>' + esc(Math.round(b.total_qty || 0)) + '</td>'
					+ '<td>' + esc(groups) + '</td></tr>';
			}).join('') + '</tbody></table></div>';
		return html;
	}
	function topBrandsPreviewDi(p, limit) {
		limit = limit || 10;
		var list = (p.brands || []).slice();
		list.sort(function (a, b) { return (parseFloat(b.total_qty) || 0) - (parseFloat(a.total_qty) || 0); });
		var top = list.slice(0, limit);
		var text = top.map(function (b) { return esc(b.brand); }).join(', ');
		var more = list.length - top.length;
		if (more > 0) {
			text += ' <span class="epc-di-more-brands">+' + more + ' more</span>';
		}
		return text;
	}
	function renderProductLinesBlock() {
		var products = state.countryProducts || [];
		if (!products.length) {
			return '<div class="epc-di-block"><h3 class="epc-di-section-title">Product lines you deal in — ' + esc(state.demandCountry) + '</h3>'
				+ '<p class="epc-di-lead">No product groups resolved yet.</p></div>';
		}
		return '<div class="epc-di-block" id="epc-di-product-grid-block"><h3 class="epc-di-section-title">Product lines you deal in — ' + esc(state.demandCountryName) + '</h3>'
			+ '<p class="epc-di-lead">Top 10 brands by UAE qty on each card (product type). Click to open for all brands and articles.</p>'
			+ '<div class="epc-di-grid">' + products.map(function (p, i) {
				var brandsLine = topBrandsPreviewDi(p, 10);
				var samples = (p.samples || []).map(function (s) {
					return esc(s.brand) + ' ' + esc(s.article);
				}).join(', ');
				var search = [p.label, brandsLine, samples].join(' ');
				var active = state.selectedProductLine === p.label ? ' is-active' : '';
				return '<div class="epc-di-product-card' + active + '" data-product-i="' + i + '" data-search="' + esc(search) + '" role="button" tabindex="0">'
					+ '<strong>' + esc(p.label || 'Other') + '</strong>'
					+ '<small>' + esc(p.parts_count || 0) + ' part line(s) · UAE qty ' + esc(Math.round(p.total_qty || 0)) + '</small>'
					+ (brandsLine ? '<small class="epc-di-brands-line">Brands: ' + brandsLine + '</small>' : (samples ? '<small>e.g. ' + samples + '</small>' : ''))
					+ '<span class="epc-di-product-card__hint">Click to open →</span></div>';
			}).join('') + '</div></div>';
	}
	function openProductCategory(label) {
		state.selectedProductLine = label || '';
		renderCountryVehicles();
	}
	function renderProductCategoryDetail() {
		var label = state.selectedProductLine;
		var product = getProductByLabel(label);
		if (!product && !label) {
			state.selectedProductLine = '';
			return '';
		}
		var parts = partsForProduct(product);
		var brands = product && product.brands && product.brands.length ? product.brands : [];
		if (!brands.length) {
			var bmap = {};
			parts.forEach(function (p) {
				var b = String(p.brand || '').trim();
				if (!b) { return; }
				var k = b.toUpperCase();
				if (!bmap[k]) { bmap[k] = { brand: b, parts_count: 0, total_qty: 0 }; }
				bmap[k].parts_count++;
				bmap[k].total_qty += parseFloat(p.qty) || 0;
			});
			brands = Object.keys(bmap).map(function (k) { return bmap[k]; });
		}
		var totalQty = product ? Math.round(product.total_qty || 0) : parts.reduce(function (s, p) { return s + (parseFloat(p.qty) || 0); }, 0);
		var html = '<div class="epc-di-block epc-di-product-detail">'
			+ '<button type="button" class="btn btn-default btn-sm" id="epc-di-product-back"><i class="fa fa-arrow-left"></i> Back to overview</button> '
			+ '<button type="button" class="btn btn-success btn-sm" id="epc-di-product-dl"><i class="fa fa-file-excel-o"></i> Download Excel</button>'
			+ '<h3 class="epc-di-section-title" style="margin-top:12px;">' + esc(label) + '</h3>'
			+ '<p class="epc-di-lead">' + esc(parts.length) + ' part line(s) · UAE qty <strong>' + esc(totalQty) + '</strong> · demand <strong>' + esc(state.demandCountry) + '</strong></p>';
		if (brands.length) {
			html += '<h4 class="epc-di-section-title">Brands in this category</h4>'
				+ '<table class="epc-di-table"><thead><tr><th>Brand</th><th>Lines</th><th>UAE qty</th></tr></thead><tbody>'
				+ brands.map(function (b) {
					return '<tr><td><strong>' + esc(b.brand) + '</strong></td><td>' + esc(b.parts_count || 0) + '</td><td>' + esc(Math.round(b.total_qty || 0)) + '</td></tr>';
				}).join('') + '</tbody></table>';
		}
		html += '<h4 class="epc-di-section-title">All parts in this category</h4>'
			+ '<table class="epc-di-table"><thead><tr><th>Brand</th><th>Article</th><th>Name</th><th>UAE qty</th><th></th></tr></thead><tbody>'
			+ parts.map(function (p, i) {
				var search = [p.brand, p.article, p.name].join(' ');
				return '<tr class="epc-di-part-open" data-part-i="' + i + '" data-search="' + esc(search) + '">'
					+ '<td><strong>' + esc(p.brand) + '</strong></td>'
					+ '<td><a href="' + esc(shopUrl(p.brand, p.article)) + '">' + esc(p.article) + '</a></td>'
					+ '<td>' + esc(p.name) + '</td><td>' + esc(p.qty) + '</td>'
					+ '<td><button type="button" class="btn btn-xs btn-primary epc-di-part-cross-btn">Crosses</button></td></tr>';
			}).join('') + '</tbody></table></div>';
		return html;
	}
	function bindProductCategoryDetail(parts) {
		var backBtn = document.getElementById('epc-di-product-back');
		if (backBtn) {
			backBtn.onclick = function () {
				state.selectedProductLine = '';
				renderCountryVehicles();
			};
		}
		var dlBtn = document.getElementById('epc-di-product-dl');
		if (dlBtn) {
			dlBtn.onclick = function () {
				downloadProductCategoryExcel(state.selectedProductLine);
			};
		}
		Array.prototype.forEach.call(mainEl.querySelectorAll('.epc-di-part-open'), function (tr) {
			var openCross = function () {
				var p = parts[parseInt(tr.getAttribute('data-part-i'), 10)];
				if (p) { loadCrosses(p.brand, p.article); }
			};
			tr.onclick = function (e) {
				if (e.target && e.target.closest && e.target.closest('.epc-di-part-cross-btn')) { return; }
			};
			var btn = tr.querySelector('.epc-di-part-cross-btn');
			if (btn) {
				btn.onclick = function (e) {
					e.stopPropagation();
					openCross();
				};
			}
		});
	}
	function bindProductLineCards() {
		var block = document.getElementById('epc-di-product-grid-block');
		if (!block) { return; }
		var products = state.countryProducts || [];
		Array.prototype.forEach.call(block.querySelectorAll('.epc-di-product-card'), function (card) {
			function activate() {
				var i = parseInt(card.getAttribute('data-product-i'), 10);
				var p = products[i];
				if (p) { openProductCategory(p.label); }
			}
			card.onclick = activate;
			card.onkeydown = function (e) {
				if (e.key === 'Enter' || e.key === ' ') {
					e.preventDefault();
					activate();
				}
			};
		});
	}
	function renderPartLinesBlock() {
		var lines = state.countryPartLines || [];
		if (!lines.length) { return ''; }
		var tableId = 'epc-di-parts-table';
		var hidden = state.showPartsTable ? '' : ' epc-di-hidden';
		var rows = lines.map(function (line, i) {
			var search = [line.brand, line.article, line.name, line.product_group].join(' ');
			return '<tr data-search="' + esc(search) + '"><td><strong>' + esc(line.brand) + '</strong></td>'
				+ '<td>' + esc(line.article) + '</td><td>' + esc(line.name) + '</td>'
				+ '<td>' + esc(line.product_group) + '</td><td>' + esc(line.qty) + '</td></tr>';
		}).join('');
		return '<div class="epc-di-block"><h3 class="epc-di-section-title">All parts for this country</h3>'
			+ '<button type="button" class="btn btn-default btn-sm epc-di-parts-toggle" id="epc-di-parts-toggle">'
			+ (state.showPartsTable ? 'Hide' : 'Show') + ' part list (' + lines.length + ')</button>'
			+ '<div id="' + tableId + '" class="' + hidden.trim() + '"><table class="epc-di-table"><thead><tr>'
			+ '<th>Brand</th><th>Article</th><th>Name</th><th>Product line</th><th>UAE qty</th></tr></thead><tbody>'
			+ rows + '</tbody></table></div></div>';
	}
	function renderCountryVehicles(payload) {
		state.step = 'vehicle';
		renderSteps();
		if (payload) { applyStep2Payload(payload); }
		if (state.selectedProductLine) {
			var detailParts = partsForProduct(getProductByLabel(state.selectedProductLine));
			mainEl.innerHTML = '<h2 class="epc-di-section-title">2 · Country overview — ' + esc(state.demandCountryName) + '</h2>'
				+ renderDownloadToolbar()
				+ renderProductCategoryDetail();
			bindDownloadToolbar();
			bindProductCategoryDetail(detailParts);
			applyFilter();
			return;
		}
		var vehicles = state.countryVehicles || [];
		var rows = filterVehiclesForTab(vehicles);
		var tabLabel = state.section === 'commercial' ? 'Commercial' : (state.section === 'motorbike' ? 'Motorbike' : 'Passenger');
		var html = '<h2 class="epc-di-section-title">2 · Country overview — ' + esc(state.demandCountryName) + '</h2>'
			+ '<p class="epc-di-lead">Summary of what you stock in UAE for <strong>' + esc(state.demandCountry) + '</strong> demand. Click a <strong>product line</strong> for brand detail, or choose a <strong>vehicle</strong> to continue.</p>'
			+ renderDownloadToolbar()
			+ renderSummaryCards(state.countrySummary)
			+ renderProductLinesBlock()
			+ renderPartLinesBlock();

		if (!vehicles.length && !(state.countryPartLines || []).length) {
			mainEl.innerHTML = html + '<div class="epc-di-msg">No parts or vehicles found. Ensure demand tags and UAE stock exist for this country.</div>';
			return;
		}

		if (!rows.length) {
			html += '<div class="epc-di-block"><h3 class="epc-di-section-title">Vehicles (fitment)</h3>'
				+ renderVehicleFitmentDownloads()
				+ '<p class="epc-di-msg">No <strong>' + esc(tabLabel) + '</strong> vehicles in this list. Try another tab — '
				+ '<strong>' + vehicles.length + '</strong> total across all types.</p></div>';
		} else {
			html += '<div class="epc-di-block"><h3 class="epc-di-section-title">Vehicles (fitment) — choose one</h3>'
				+ renderVehicleFitmentDownloads()
				+ '<p class="epc-di-lead">Showing <strong>' + rows.length + '</strong> ' + esc(tabLabel) + ' vehicle(s). '
				+ '“Parts” = how many demand lines fit this vehicle. Use <strong>Download Excel</strong> above for the full list.</p>'
				+ '<table class="epc-di-table"><thead><tr><th>Make</th><th>Model</th><th>Modification</th><th>Years</th><th>Engine</th><th>Parts</th></tr></thead><tbody>'
				+ rows.map(function (row, i) {
					var search = [row.MANUFACTURER, row.MODEL_SERIES, vehicleModText(row), vehicleYearRange(row), vehicleEngineText(row)].join(' ');
					var partsN = row._parts_count || 1;
					return '<tr class="epc-di-vehicle-row" data-i="' + i + '" data-search="' + esc(search) + '">'
						+ '<td><strong>' + esc(row.MANUFACTURER || '') + '</strong></td>'
						+ '<td>' + esc(row.MODEL_SERIES || '') + '</td>'
						+ '<td>' + esc(vehicleModText(row)) + '</td>'
						+ '<td>' + esc(vehicleYearRange(row)) + '</td>'
						+ '<td>' + esc(vehicleEngineText(row)) + '</td>'
						+ '<td><span class="epc-di-tag">' + esc(partsN) + '</span></td></tr>';
				}).join('') + '</tbody></table></div>';
		}

		mainEl.innerHTML = html;
		bindDownloadToolbar();
		bindVehicleFitmentDownloads();
		bindProductLineCards();
		var filtered = rows;
		Array.prototype.forEach.call(mainEl.querySelectorAll('.epc-di-vehicle-row'), function (tr) {
			tr.onclick = function () {
				pickVehicle(filtered[parseInt(tr.getAttribute('data-i'), 10)]);
			};
		});
		var partsToggle = document.getElementById('epc-di-parts-toggle');
		if (partsToggle) {
			partsToggle.onclick = function () {
				state.showPartsTable = !state.showPartsTable;
				renderCountryVehicles();
			};
		}
		applyFilter();
		if (pendingDeepLink.brand && pendingDeepLink.article) {
			loadCrosses(pendingDeepLink.brand, pendingDeepLink.article);
			pendingDeepLink.brand = '';
			pendingDeepLink.article = '';
		}
	}
	function runCountryVehicleJob(jobId) {
		function stepOnce() {
			return fetch(vehiclesApi + '?action=step&job_id=' + encodeURIComponent(jobId) + '&batch=2&_=' + Date.now(), { credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function (d) {
					if (d && (d.code === 'auth' || d.code === 'forbidden')) { throw new Error(apiErrorMessage(d, 'Access denied')); }
					if (!d || !d.status) { throw new Error(apiErrorMessage(d, 'Fitment scan failed')); }
					var meta = d.current_part ? ('Current: ' + d.current_part) : '';
					showVehicleProgress(d.progress, d.message, meta);
					applyStep2Payload(d);
					if (!d.done) { return stepOnce(); }
					return d;
				});
		}
		return stepOnce();
	}
	function buildCountryVehicles() {
		state.step = 'vehicle';
		renderSteps();
		showVehicleProgress(0, 'Loading price-list parts for ' + state.demandCountry + '…', '');
		var startUrl = vehiclesApi + '?action=start&country=' + encodeURIComponent(state.demandCountry)
			+ '&limit=40&seed=1&require_stock=1&_=' + Date.now();
		return fetch(startUrl, { credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (d) {
				if (d && (d.code === 'auth' || d.code === 'forbidden')) { throw new Error(apiErrorMessage(d, 'Access denied')); }
				if (!d || !d.status) { throw new Error(apiErrorMessage(d, 'Could not start fitment scan')); }
				state.vehiclePartsTotal = d.parts_total || 0;
				if (d.done || !d.job_id) {
					showVehicleProgress(100, d.message || 'Done', '');
					renderCountryVehicles(d);
					return d;
				}
				showVehicleProgress(d.progress || 0, d.message, '');
				return runCountryVehicleJob(d.job_id).then(function (finalData) {
					renderCountryVehicles(finalData || {});
					return finalData;
				});
			});
	}
	function applyFilter() {
		var term = (filterInput.value || '').toLowerCase();
		Array.prototype.forEach.call(mainEl.querySelectorAll('[data-search]'), function (node) {
			node.style.display = node.getAttribute('data-search').toLowerCase().indexOf(term) === -1 ? 'none' : '';
		});
	}
	function umapi(action, params) {
		var q = new URLSearchParams();
		q.set('action', action);
		q.set('section', state.section);
		q.set('language', 'en');
		q.set('region', 'WWW');
		if (state.vehicleType && ['models', 'modifications', 'categories', 'products', 'articles'].indexOf(action) !== -1) {
			q.set('vehicle_type', state.vehicleType);
		}
		Object.keys(params || {}).forEach(function (k) {
			if (params[k] !== undefined && params[k] !== null && params[k] !== '') {
				q.set(k, params[k]);
			}
		});
		return fetch('/api/umapi_proxy.php?' + q.toString(), { credentials: 'same-origin' }).then(function (r) {
			return r.json().then(function (d) { if (!r.ok) { throw d; } return d; });
		});
	}
	function modId(m) {
		return m.PC_ID || m.CV_ID || m.MTB_ID || m.ID || m.MOD_ID || '';
	}
	function modTitle(m) {
		return m.PASSENGER_CAR || m.COMMERCIAL_VEHICLE || m.MOTORBIKE || m.MODIFICATION || m.DES || 'Vehicle';
	}
	function renderSteps() {
		var labels = {
			vehicle: 'Vehicle',
			category: state.categoryLabel || 'Category',
			product: state.productLabel || 'Product',
			parts: 'Parts',
			crosses: 'Crosses'
		};
		var order = ['vehicle', 'category', 'product', 'parts', 'crosses'];
		var idx = order.indexOf(state.step);
		stepsEl.innerHTML = order.map(function (key, i) {
			var cls = 'epc-di-step';
			if (key === state.step) { cls += ' active'; }
			else if (i < idx) { cls += ' done'; }
			return '<span class="' + cls + '">' + esc(labels[key]) + '</span>';
		}).join('');
	}
	function hasDemandTag(brand, article) {
		var codes = state.demandIndex[partKey(brand, article)];
		return codes && codes.indexOf(state.demandCountry) !== -1;
	}

	function renderManufacturers(items) {
		state._mfrList = items || [];
		state.step = 'vehicle';
		renderSteps();
		if (!items || !items.length) {
			mainEl.innerHTML = '<div class="epc-di-msg">No manufacturers.</div>';
			return;
		}
		mainEl.innerHTML = '<h2 class="epc-di-section-title">2 · Choose make (vehicle fitment)</h2>'
			+ '<p class="epc-di-lead">Demand country: <strong>' + esc(state.demandCountryName) + '</strong> — stock checks use UAE only.</p>'
			+ '<div class="epc-di-grid">' + items.map(function (item, i) {
				return '<button type="button" class="epc-di-card" data-i="' + i + '" data-search="' + esc(item.MANUFACTURER) + '"><strong>' + esc(item.MANUFACTURER) + '</strong><small>' + esc(item.COUNTRY || '') + '</small></button>';
			}).join('') + '</div>';
		Array.prototype.forEach.call(mainEl.querySelectorAll('.epc-di-card'), function (btn) {
			btn.onclick = function () {
				state.manufacturer = items[parseInt(btn.getAttribute('data-i'), 10)];
				state.vehicleType = (state.manufacturer.EPART_TYPES && state.manufacturer.EPART_TYPES[0]) || (state.section === 'commercial' ? 'CV' : 'PC');
				loadModels();
			};
		});
		applyFilter();
	}
	function loadModels() {
		loading('Loading models…');
		umapi('models', { MFA_ID: state.manufacturer.MFA_ID }).then(function (items) {
			var rows = Array.isArray(items) ? items : (items.data || []);
			state.step = 'vehicle';
			renderSteps();
			mainEl.innerHTML = '<h2 class="epc-di-section-title">Choose model — ' + esc(state.manufacturer.MANUFACTURER) + '</h2><div class="epc-di-list">'
				+ rows.map(function (item, i) {
					return '<div class="epc-di-row" data-i="' + i + '" data-search="' + esc(item.MODEL_SERIES) + '"><strong>' + esc(item.MODEL_SERIES) + '</strong></div>';
				}).join('') + '</div>';
			Array.prototype.forEach.call(mainEl.querySelectorAll('.epc-di-row'), function (row) {
				row.onclick = function () {
					state.model = rows[parseInt(row.getAttribute('data-i'), 10)];
					loadModifications();
				};
			});
			applyFilter();
		}).catch(function () { mainEl.innerHTML = '<div class="epc-di-msg">Could not load models.</div>'; });
	}
	function loadModifications() {
		loading('Loading engines / modifications…');
		umapi('modifications', { MS_ID: state.model.MS_ID }).then(function (items) {
			var rows = Array.isArray(items) ? items : (items.data || []);
			state.modifications = rows;
			state.step = 'vehicle';
			renderSteps();
			mainEl.innerHTML = '<h2 class="epc-di-section-title">Choose engine / modification — ' + esc(state.model.MODEL_SERIES) + '</h2><div class="epc-di-list">'
				+ rows.map(function (item, i) {
					var meta = [item.CI_FROM, item.CI_TO].filter(Boolean).join('–');
					return '<div class="epc-di-row" data-i="' + i + '" data-search="' + esc(modTitle(item) + ' ' + meta) + '"><strong>' + esc(modTitle(item)) + '</strong><br><small>' + esc(meta) + '</small></div>';
				}).join('') + '</div>';
			Array.prototype.forEach.call(mainEl.querySelectorAll('.epc-di-row'), function (row) {
				row.onclick = function () {
					state.modification = rows[parseInt(row.getAttribute('data-i'), 10)];
					loadCategories();
				};
			});
			applyFilter();
		}).catch(function () { mainEl.innerHTML = '<div class="epc-di-msg">Could not load modifications.</div>'; });
	}
	function loadCategories() {
		state.step = 'category';
		state.categoryId = '';
		state.categoryLabel = '';
		renderSteps();
		loading('Loading part categories for this vehicle…');
		umapi('categories', { ID: modId(state.modification) }).then(function (data) {
			state.categories = data;
			var quick = (data && data.quic) ? data.quic : [];
			var cards = quick.map(function (item) {
				var id = item.CATEGORY_IDS && item.CATEGORY_IDS.length ? item.CATEGORY_IDS[0] : item.CATEGORY_ID;
				return { id: id, label: item.DES || 'Category' };
			}).filter(function (c) { return c.id; });
			if (!cards.length && data && data.root) {
				function walk(nodes, depth) {
					if (!nodes || depth > 2 || cards.length >= 24) { return; }
					nodes.forEach(function (n) {
						if (n.CATEGORY_ID && n.DES) { cards.push({ id: n.CATEGORY_ID, label: n.DES }); }
						walk(n.CHILD || n.children || [], depth + 1);
					});
				}
				walk(data.root, 0);
			}
			mainEl.innerHTML = '<h2 class="epc-di-section-title">3 · Choose category</h2>'
				+ '<p class="epc-di-lead">' + esc(state.manufacturer.MANUFACTURER) + ' ' + esc(state.model.MODEL_SERIES) + ' · ' + esc(modTitle(state.modification)) + '</p>'
				+ '<div class="epc-di-grid">' + cards.map(function (c, i) {
					return '<button type="button" class="epc-di-card" data-i="' + i + '" data-search="' + esc(c.label) + '"><strong>' + esc(c.label) + '</strong></button>';
				}).join('') + '</div>';
			Array.prototype.forEach.call(mainEl.querySelectorAll('.epc-di-card'), function (btn) {
				btn.onclick = function () {
					var c = cards[parseInt(btn.getAttribute('data-i'), 10)];
					state.categoryId = c.id;
					state.categoryLabel = c.label;
					loadProducts();
				};
			});
			applyFilter();
		}).catch(function () { mainEl.innerHTML = '<div class="epc-di-msg">Categories failed to load.</div>'; });
	}
	function loadProducts() {
		state.step = 'product';
		renderSteps();
		loading('Loading product groups…');
		umapi('products', { CATEGORY_ID: state.categoryId, ID: modId(state.modification) }).then(function (items) {
			var rows = Array.isArray(items) ? items : (items.data || []);
			mainEl.innerHTML = '<h2 class="epc-di-section-title">4 · Choose product group</h2><p class="epc-di-lead">' + esc(state.categoryLabel) + '</p><div class="epc-di-grid">'
				+ rows.map(function (item, i) {
					var label = item.PT_DES || item.DES || item.PRODUCT_GROUP || 'Group';
					return '<button type="button" class="epc-di-card" data-i="' + i + '" data-search="' + esc(label) + '"><strong>' + esc(label) + '</strong></button>';
				}).join('') + '</div>';
			Array.prototype.forEach.call(mainEl.querySelectorAll('.epc-di-card'), function (btn) {
				btn.onclick = function () {
					var item = rows[parseInt(btn.getAttribute('data-i'), 10)];
					state.lastProductId = item.PT_ID || item.PT_IDS || item.ID;
					state.productLabel = item.PT_DES || item.DES || 'Parts';
					loadArticles(state.lastProductId);
				};
			});
			applyFilter();
		}).catch(function () { mainEl.innerHTML = '<div class="epc-di-msg">Products failed to load.</div>'; });
	}
	function loadArticles(productId) {
		state.step = 'parts';
		renderSteps();
		loading('Loading parts for this vehicle &amp; product…');
		umapi('articles', { PT_IDS: productId, ID: modId(state.modification), limit: 80, offset: 0 }).then(function (data) {
			var items = Array.isArray(data) ? data : (data.data || []);
			state.articles = items;
			var rows = items.map(function (item, index) {
				var brand = item.SUP_BRAND || item.BRAND || '';
				var article = item.ART_ARTICLE_NR || item.ARTICLE || '';
				var name = item.ART_PRODUCT_NAME || item.DES || item.NAME || '';
				var tagged = hasDemandTag(brand, article);
				var tag = tagged
					? '<span class="epc-di-tag">Demand ' + esc(state.demandCountry) + '</span>'
					: '<span class="epc-di-tag epc-di-tag--no">No ' + esc(state.demandCountry) + ' tag</span>';
				return '<tr class="epc-di-part-row" data-index="' + index + '" data-search="' + esc([brand, article, name].join(' ')) + '">'
					+ '<td><strong>' + esc(brand) + '</strong></td><td>' + esc(article) + '</td><td>' + esc(name) + '</td><td>' + tag + '</td>'
					+ '<td><button type="button" class="btn btn-xs btn-primary epc-di-open-cross">Crosses</button></td></tr>';
			}).join('');
			mainEl.innerHTML = '<h2 class="epc-di-section-title">5 · Parts (fitment for selected vehicle)</h2>'
				+ '<p class="epc-di-lead">Yellow tag = this line is in demand for <strong>' + esc(state.demandCountryName) + '</strong>. Click Crosses for UAE stock &amp; cross gaps.</p>'
				+ '<table class="epc-di-table"><thead><tr><th>Brand</th><th>Article</th><th>Name</th><th>Demand</th><th></th></tr></thead><tbody>'
				+ (rows || '<tr><td colspan="5">No articles.</td></tr>') + '</tbody></table>';
			Array.prototype.forEach.call(mainEl.querySelectorAll('.epc-di-part-row'), function (tr) {
				function open() {
					var item = state.articles[parseInt(tr.getAttribute('data-index'), 10)];
					var brand = item.SUP_BRAND || item.BRAND || '';
					var article = item.ART_ARTICLE_NR || item.ARTICLE || '';
					loadCrosses(brand, article);
				}
				tr.onclick = open;
				var btn = tr.querySelector('.epc-di-open-cross');
				if (btn) { btn.onclick = function (e) { e.stopPropagation(); open(); }; }
			});
			applyFilter();
		}).catch(function () { mainEl.innerHTML = '<div class="epc-di-msg">Articles failed to load.</div>'; });
	}
	function loadCrosses(brand, article) {
		state.step = 'crosses';
		renderSteps();
		crossPanel.classList.remove('epc-di-hidden');
		crossTitle.textContent = '6 · Crosses — ' + brand + ' ' + article + ' · demand ' + state.demandCountry;
		crossBody.innerHTML = '<div class="epc-di-loader">Loading UAE stock, cross gaps, other countries…</div>';
		crossPanel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
		var url = cardApi + '?brand=' + encodeURIComponent(brand) + '&article=' + encodeURIComponent(article)
			+ '&country=' + encodeURIComponent(state.demandCountry) + '&seed=1&_=' + Date.now();
		fetch(url, { credentials: 'same-origin' }).then(function (r) { return r.json(); }).then(function (data) {
			if (!data || !data.status) {
				crossBody.innerHTML = '<div class="epc-di-msg">Could not load cross data.</div>';
				return;
			}
			var demand = (data.demand_countries || []).map(function (c) {
				var hi = c.code === state.demandCountry ? ' epc-di-tag' : ' epc-di-tag--ae';
				return '<span class="epc-di-tag' + hi + '">' + esc(c.code) + '</span>';
			}).join(' ');
			var sell = (data.sellable_crosses || []).map(function (r) {
				return '<tr><td>' + esc(r.brand) + '</td><td><a href="' + esc(r.url || shopUrl(r.brand, r.article)) + '">' + esc(r.article) + '</a></td><td>' + esc(r.qty) + '</td><td>' + esc(r.price) + '</td></tr>';
			}).join('') || '<tr><td colspan="4">None in UAE batch</td></tr>';
			var gaps = (data.cross_gaps || []).slice(0, 15).map(function (r) {
				return '<tr><td>' + esc(r.brand) + '</td><td>' + esc(r.article) + '</td><td>' + esc(r.name) + '</td></tr>';
			}).join('') || '<tr><td colspan="3">No gaps in batch</td></tr>';
			var stats = data.demand_statistics || {};
			var overlap = (stats.shared_country_stats || []).map(function (r) {
				return '<tr><td>' + esc(r.code) + '</td><td>' + esc(r.brands_with_demand) + ' brands</td><td>' + (r.in_this_brand ? 'Yes' : '—') + '</td></tr>';
			}).join('');
			var vehicles = ((data.fitment || {}).vehicles_sample || []).map(function (v) {
				return '<tr><td>' + esc(v.make) + '</td><td>' + esc(v.model) + '</td><td>' + esc(v.years) + '</td><td>' + esc(v.engine) + '</td></tr>';
			}).join('') || '<tr><td colspan="4">—</td></tr>';
			crossBody.innerHTML =
				'<p><span class="epc-di-tag--ae">UAE stock</span> '
				+ (data.anchor && data.anchor.in_stock ? '<span class="epc-di-tag epc-di-tag--ok">OE qty ' + esc(data.anchor.qty) + '</span>' : '<span class="epc-di-tag epc-di-tag--no">OE not in UAE</span>')
				+ ' · <a href="' + esc(data.part_url || shopUrl(brand, article)) + '">Shop page</a></p>'
				+ '<p><strong>Demand countries (this brand+number):</strong> ' + (demand || '—') + '</p>'
				+ '<h3 class="epc-di-section-title">Same number — demand in other countries</h3><table class="epc-di-table"><thead><tr><th>Country</th><th>Brands</th><th>This brand?</th></tr></thead><tbody>' + overlap + '</tbody></table>'
				+ '<h3 class="epc-di-section-title">In stock crosses (UAE)</h3><table class="epc-di-table"><thead><tr><th>Brand</th><th>Article</th><th>Qty</th><th>Price</th></tr></thead><tbody>' + sell + '</tbody></table>'
				+ '<h3 class="epc-di-section-title">Cross gaps (catalog, not in UAE) — ' + esc(data.cross_gaps_count || 0) + ' total</h3><table class="epc-di-table"><thead><tr><th>Brand</th><th>Article</th><th>Name</th></tr></thead><tbody>' + gaps + '</tbody></table>'
				+ '<h3 class="epc-di-section-title">Vehicle fitment sample</h3><table class="epc-di-table"><thead><tr><th>Make</th><th>Model</th><th>Years</th><th>Engine</th></tr></thead><tbody>' + vehicles + '</tbody></table>';
		}).catch(function () {
			crossBody.innerHTML = '<div class="epc-di-msg">Cross load failed.</div>';
		});
	}
	function loadDemandIndex() {
		return fetch(tagsApi + '?country=' + encodeURIComponent(state.demandCountry) + '&_=' + Date.now(), { credentials: 'same-origin' })
			.then(function (r) { return r.json(); })
			.then(function (d) {
				state.demandIndex = (d && d.index) ? d.index : {};
			}).catch(function () { state.demandIndex = {}; });
	}
	function startWizard() {
		var code = countryCodeFromUi();
		if (!code) {
			alert('Please choose a demand country first.');
			return;
		}
		if (!isCountryAllowed(code)) {
			alert(access.country_locked
				? ('Your account is limited to ' + (access.user_country_name || access.user_country) + '.')
				: 'This country is not available for your account.');
			return;
		}
		state.demandCountry = code;
		state.demandCountryName = countryNameForCode(code);
		state.started = true;
		workspace.classList.remove('epc-di-hidden');
		crossPanel.classList.add('epc-di-hidden');
		state.manufacturer = null;
		state.model = null;
		state.modification = null;
		state.selectedProductLine = '';
		loadDemandIndex().then(function () {
			return fetch('/content/shop/docpart/ajax_epc_demand_showcase.php?limit=10&seed=1', { credentials: 'same-origin' });
		}).then(function () {
			return buildCountryVehicles();
		}).catch(function (err) {
			mainEl.innerHTML = '<div class="epc-di-msg">' + esc(err && err.message ? err.message : 'Could not start. Try again.') + '</div>';
		});
	}
	function goBack() {
		if (state.step === 'crosses') {
			crossPanel.classList.add('epc-di-hidden');
			if (state.lastProductId) {
				state.step = 'parts';
				loadArticles(state.lastProductId);
			} else {
				state.step = 'vehicle';
				renderCountryVehicles();
			}
			return;
		}
		if (state.step === 'parts') { loadProducts(); return; }
		if (state.step === 'product') { loadCategories(); return; }
		if (state.step === 'category') {
			state.step = 'vehicle';
			state.selectedProductLine = '';
			renderCountryVehicles();
			return;
		}
		if (state.selectedProductLine) {
			state.selectedProductLine = '';
			renderCountryVehicles();
			return;
		}
	}
	startBtn.onclick = startWizard;
	backBtn.onclick = goBack;
	filterInput.oninput = applyFilter;
	Array.prototype.forEach.call(sectionTabs.querySelectorAll('.epc-di-tab'), function (tab) {
		tab.onclick = function () {
			Array.prototype.forEach.call(sectionTabs.querySelectorAll('.epc-di-tab'), function (t) { t.classList.remove('active'); });
			tab.classList.add('active');
			state.section = tab.getAttribute('data-section') || 'passenger';
			if (state.started && state.step === 'vehicle') {
				renderCountryVehicles();
				return;
			}
			if (state.started) { startWizard(); }
		};
	});
	var pendingDeepLink = {
		brand: (root.getAttribute('data-deeplink-brand') || '').trim(),
		article: (root.getAttribute('data-deeplink-article') || '').trim()
	};
	try {
		var qs = new URLSearchParams(window.location.search);
		if (qs.get('country') && countrySelect) {
			var qCountry = String(qs.get('country')).toUpperCase();
			if (isCountryAllowed(qCountry)) {
				countrySelect.value = qCountry;
			}
		}
		if (qs.get('brand')) { pendingDeepLink.brand = String(qs.get('brand')).trim(); }
		if (qs.get('article')) { pendingDeepLink.article = String(qs.get('article')).trim(); }
	} catch (e) { /* ignore */ }
	if (countrySelect && !countrySelect.value && access.default_country) {
		countrySelect.value = access.default_country;
	}

	function applyAccessPayload(next) {
		if (!next) { return; }
		access = next;
		if (!access.allowed_codes) { access.allowed_codes = []; }
		if (!access.allowed_countries) { access.allowed_countries = []; }
		if (!countrySelect || access.country_locked) { return; }
		var prev = countrySelect.value;
		countrySelect.innerHTML = '<option value="">Select country…</option>';
		(access.allowed_countries || []).forEach(function(c) {
			var opt = document.createElement('option');
			opt.value = c.code;
			opt.textContent = c.name + ' (' + c.code + ')';
			countrySelect.appendChild(opt);
		});
		if (prev && access.allowed_codes.indexOf(prev) !== -1) {
			countrySelect.value = prev;
		} else if (access.default_country) {
			countrySelect.value = access.default_country;
		}
		if (startBtn) {
			startBtn.disabled = !(access.allowed_codes && access.allowed_codes.length);
		}
		var lead = document.getElementById('epc-di-intro-lead');
		if (lead && access.is_admin) {
			lead.innerHTML = 'Choose a <strong>demand country</strong> (admin: all countries), press <strong>Start</strong>. The system scans <strong>price-list parts</strong>, loads <strong>vehicle fitment</strong>, then you pick vehicle → category → product → part → crosses (UAE stock).';
		}
	}

	var metaApi = root.getAttribute('data-meta-api') || '';
	if (root.getAttribute('data-logged-in') === '1' && metaApi && (!access.allowed_countries || access.allowed_countries.length === 0)) {
		fetch(metaApi, { credentials: 'same-origin' })
			.then(function(r) { return r.json(); })
			.then(function(d) {
				if (d && d.status && d.access && d.access.allowed_countries && d.access.allowed_countries.length) {
					applyAccessPayload(d.access);
				}
			})
			.catch(function() { /* ignore */ });
	}
})();
</script>
