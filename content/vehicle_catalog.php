<?php
defined('_ASTEXE_') or die('No access');
$lang_href = (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') ? rtrim($multilang_params['lang_href'], '/') : '/en';
$epc_vc_chpu_on = !empty($DP_Config->chpu_search_config['chpu_search_on']);
$epc_vc_chpu_parts_url = !empty($DP_Config->chpu_search_config['level_1']['url']) ? $DP_Config->chpu_search_config['level_1']['url'] : 'parts';
$epc_vc_chpu_brands_url = !empty($DP_Config->chpu_search_config['level_2']['mode_1']['url']) ? $DP_Config->chpu_search_config['level_2']['mode_1']['url'] : 'brands';
$epc_vc_chpu_slash_code = isset($DP_Config->chpu_search_config['slash_code']) ? $DP_Config->chpu_search_config['slash_code'] : '%2F';
?>
<style>
.epc-vc { margin: 0 0 36px; }
.epc-vc-searchbar {
	background: #fff;
	border: 1px solid #e2e8f0;
	border-radius: 14px;
	box-shadow: 0 8px 24px rgba(15,23,42,.06);
	display: grid;
	gap: 10px;
	grid-template-columns: 1fr 1fr auto;
	margin-bottom: 16px;
	padding: 12px;
}
.epc-vc-searchbar .form-control { border-radius: 10px; }
.epc-vc-hero-grid {
	display: grid;
	gap: 16px;
	grid-template-columns: minmax(280px, 380px) minmax(0, 1fr);
	margin-bottom: 16px;
}
.epc-vc-picker {
	background: #fff;
	border: 1px solid #e2e8f0;
	border-radius: 16px;
	box-shadow: 0 12px 32px rgba(15,23,42,.08);
	padding: 18px;
}
.epc-vc-picker h2 {
	font-size: 20px;
	font-weight: 900;
	margin: 0 0 14px;
}
.epc-vc-step-row {
	align-items: center;
	display: grid;
	gap: 10px;
	grid-template-columns: 28px minmax(0, 1fr);
	margin-bottom: 10px;
}
.epc-vc-step-num {
	align-items: center;
	background: #f97316;
	border-radius: 50%;
	color: #fff;
	display: inline-flex;
	font-size: 12px;
	font-weight: 900;
	height: 28px;
	justify-content: center;
	width: 28px;
}
.epc-vc-step-row.is-muted .epc-vc-step-num { background: #cbd5e1; color: #64748b; }
.epc-vc-step-row select { border-radius: 10px; }
.epc-vc-picker-help {
	color: #f97316;
	font-size: 12px;
	font-weight: 800;
	margin-top: 8px;
	text-align: right;
}
.epc-vc-promo {
	background: linear-gradient(135deg, #0d9488, #115e59);
	border-radius: 16px;
	color: #fff;
	min-height: 260px;
	overflow: hidden;
	padding: 22px;
	position: relative;
}
.epc-vc-promo strong {
	background: #facc15;
	border-radius: 8px;
	color: #0f172a;
	display: inline-block;
	font-size: 18px;
	margin-bottom: 10px;
	padding: 6px 10px;
}
.epc-vc-promo-body p {
	margin: 0;
	max-width: 420px;
}
.epc-vc-promo-vin-badge {
	background: #facc15;
	border-radius: 8px;
	color: #0f172a;
	display: inline-block;
	font-size: 12px;
	font-weight: 900;
	letter-spacing: .04em;
	margin-bottom: 8px;
	padding: 5px 10px;
	text-transform: uppercase;
}
.epc-vc-promo-vin-title {
	font-size: 17px;
	font-weight: 900;
	line-height: 1.25;
	margin: 0 0 8px;
}
.epc-vc-promo-vin-meta {
	color: rgba(255, 255, 255, .88);
	font-size: 12px;
	line-height: 1.45;
	margin: 0 0 10px;
}
.epc-vc-promo-vin-list {
	display: grid;
	gap: 8px;
	margin-top: 10px;
}
.epc-vc-promo-vin-btn {
	background: rgba(255, 255, 255, .1);
	border: 1px solid rgba(255, 255, 255, .35);
	border-radius: 10px;
	color: #fff !important;
	cursor: pointer;
	font-size: 12px;
	font-weight: 800;
	padding: 10px 12px;
	text-align: left;
	transition: background .15s ease, border-color .15s ease;
	width: 100%;
}
.epc-vc-promo-vin-btn:hover {
	background: rgba(255, 255, 255, .18);
	border-color: #fff;
}
.epc-vc-promo-vin-btn strong {
	background: transparent;
	color: #fff !important;
	display: block;
	font-size: 13px;
	margin: 0 0 3px;
	padding: 0;
}
.epc-vc-promo-vin-btn span {
	color: rgba(255, 255, 255, .85);
	display: block;
	font-size: 11px;
	font-weight: 700;
}
.epc-vc-promo-vin-msg {
	background: rgba(255, 255, 255, .12);
	border: 1px dashed rgba(255, 255, 255, .45);
	border-radius: 10px;
	color: #fff !important;
	font-size: 12px;
	font-weight: 700;
	margin-top: 10px;
	padding: 10px 12px;
}
.epc-vc-promo-vin-reset {
	background: transparent !important;
	border-color: rgba(255, 255, 255, .55) !important;
	color: #fff !important;
	font-size: 11px;
	font-weight: 800;
	margin-top: 10px;
	padding: 5px 12px !important;
}
.epc-vc-tabs { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 14px; }
.epc-vc-tab {
	background: #fff;
	border: 1px solid #d7dee9;
	border-radius: 999px;
	cursor: pointer;
	font-weight: 800;
	padding: 8px 14px;
}
.epc-vc-tab.active { background: #f97316; border-color: #f97316; color: #fff; }
.epc-vc-vehicle-bar {
	align-items: center;
	background: linear-gradient(90deg, #1d4ed8, #2563eb);
	border-radius: 12px;
	color: #fff;
	display: flex;
	flex-wrap: wrap;
	gap: 12px;
	justify-content: space-between;
	margin-bottom: 14px;
	padding: 12px 16px;
}
.epc-vc-vehicle-bar strong { font-size: 16px; }
.epc-vc-info-panels {
	display: grid;
	gap: 12px;
	grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
	margin-bottom: 16px;
}
.epc-vc-info-panel {
	background: #fff;
	border: 2px solid #e2e8f0;
	border-radius: 14px;
	padding: 14px;
}
.epc-vc-info-panel.is-engine { border-color: #94a3b8; }
.epc-vc-info-panel h4 { font-size: 13px; font-weight: 900; margin: 0 0 8px; text-transform: uppercase; }
.epc-vc-info-panel p { color: #475569; font-size: 12px; line-height: 1.5; margin: 0; }
.epc-vc-info-icons {
	display: grid;
	gap: 8px;
	grid-template-columns: repeat(4, minmax(0, 1fr));
	margin-top: 10px;
}
.epc-vc-info-icons span {
	align-items: center;
	background: #f8fafc;
	border: 1px solid #e2e8f0;
	border-radius: 8px;
	color: #64748b;
	display: flex;
	font-size: 10px;
	font-weight: 700;
	justify-content: center;
	min-height: 44px;
	padding: 6px;
	text-align: center;
}
.epc-vc-section-title {
	font-size: 18px;
	font-weight: 900;
	margin: 0 0 12px;
	text-align: center;
	text-transform: uppercase;
}
.epc-vc-cat-search { margin-bottom: 12px; max-width: 320px; }
.epc-vc-cat-grid {
	display: grid;
	gap: 12px;
	grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
	margin-bottom: 18px;
}
.epc-vc-cat-card {
	background: #fff;
	border: 1px solid #e2e8f0;
	border-radius: 14px;
	box-shadow: 0 6px 18px rgba(15,23,42,.05);
	cursor: pointer;
	padding: 12px;
	text-align: center;
	transition: transform .15s ease, box-shadow .15s ease;
}
.epc-vc-cat-card:hover {
	border-color: #f97316;
	box-shadow: 0 10px 24px rgba(249,115,22,.15);
	transform: translateY(-2px);
}
.epc-vc-cat-card .epc-vc-cat-icon {
	align-items: center;
	background: linear-gradient(145deg, #fff7ed, #ffedd5);
	border-radius: 12px;
	color: #ea580c;
	display: flex;
	font-size: 28px;
	height: 72px;
	justify-content: center;
	margin: 0 auto 10px;
	width: 72px;
}
.epc-vc-cat-card strong {
	color: #0f172a;
	display: block;
	font-size: 12px;
	line-height: 1.35;
}
.epc-vc-layout {
	align-items: start;
	display: grid;
	gap: 16px;
	grid-template-columns: 260px minmax(0, 1fr);
}
.epc-vc-sidebar,
.epc-vc-main {
	background: #fff;
	border: 1px solid #e2e8f0;
	border-radius: 14px;
	min-height: 360px;
	padding: 14px;
}
.epc-vc-sidebar h3,
.epc-vc-main h3 { font-size: 15px; font-weight: 900; margin: 0 0 10px; }
.epc-vc-tree ul { list-style: none; margin: 0; padding-left: 14px; }
.epc-vc-tree > ul { padding-left: 0; }
.epc-vc-tree li { margin: 3px 0; }
.epc-vc-tree button {
	background: transparent;
	border: 0;
	color: #1d4ed8;
	font-size: 13px;
	font-weight: 700;
	padding: 3px 0;
	text-align: left;
}
.epc-vc-tree button.active { color: #f97316; }
.epc-vc-cards {
	display: grid;
	gap: 10px;
	grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
}
.epc-vc-card {
	background: #f8fafc;
	border: 1px solid #e2e8f0;
	border-radius: 12px;
	cursor: pointer;
	padding: 12px;
	text-align: left;
	transition: border-color .15s ease;
}
.epc-vc-card:hover { border-color: #2563eb; }
.epc-vc-card strong { color: #0f172a; display: block; font-size: 13px; }
.epc-vc-card small { color: #64748b; }
.epc-vc-articles { display: grid; gap: 10px; }
.epc-vc-article-row {
	align-items: center;
	background: #fff;
	border: 1px solid #e2e8f0;
	border-radius: 12px;
	cursor: pointer;
	display: grid;
	gap: 12px;
	grid-template-columns: 56px minmax(0, 1fr) auto;
	padding: 10px 12px;
}
.epc-vc-article-row:hover { border-color: #2563eb; }
.epc-vc-article-row.is-detail-open {
	border-color: #2563eb;
	box-shadow: 0 0 0 1px #2563eb;
}
.epc-vc-article-actions {
	align-items: stretch;
	display: flex;
	flex-direction: column;
	gap: 6px;
	min-width: 72px;
}
.epc-vc-article-actions .btn { white-space: nowrap; }
.epc-vc-inline-detail {
	background: #fff;
	border: 2px solid #2563eb;
	border-radius: 14px;
	box-shadow: 0 14px 36px rgba(15, 23, 42, .14);
	display: none;
	grid-column: 1 / -1;
	margin: 0 0 12px;
	overflow: hidden;
}
.epc-vc-inline-detail.is-open { display: block; }
.epc-vc-inline-detail__head {
	align-items: center;
	background: linear-gradient(180deg, #eff6ff 0%, #fff 100%);
	border-bottom: 1px solid #dbeafe;
	display: flex;
	gap: 10px;
	justify-content: space-between;
	padding: 12px 14px;
}
.epc-vc-inline-detail__title {
	color: #0f172a;
	font-size: 14px;
	font-weight: 900;
	line-height: 1.3;
	margin: 0;
}
.epc-vc-inline-detail__close {
	flex-shrink: 0;
}
.epc-vc-inline-detail__body {
	max-height: min(68vh, 560px);
	overflow: auto;
	padding: 14px;
}
.epc-vc-inline-detail .epc-vc-product-grid {
	grid-template-columns: 1fr;
}
@media (min-width: 900px) {
	.epc-vc-inline-detail .epc-vc-product-grid {
		grid-template-columns: minmax(140px, 200px) minmax(0, 1fr) minmax(160px, 200px);
	}
}
.epc-vc-article-thumb {
	align-items: center;
	background: #fff;
	border: 1px solid #e2e8f0;
	border-radius: 8px;
	display: flex;
	height: 48px;
	justify-content: center;
	overflow: hidden;
	width: 56px;
}
.epc-vc-article-thumb img { max-height: 44px; max-width: 52px; object-fit: contain; }
.epc-vc-message {
	background: #eff6ff;
	border: 1px dashed #bfdbfe;
	border-radius: 12px;
	color: #1e3a8a;
	font-weight: 700;
	padding: 14px;
}
.epc-vc-loader { color: #64748b; padding: 24px; text-align: center; }
.epc-vc-breadcrumb { color: #64748b; font-size: 12px; font-weight: 700; margin-bottom: 10px; }
.epc-vc-breadcrumb button {
	background: none;
	border: 0;
	color: #2563eb;
	font-weight: 700;
	padding: 0;
}
.epc-vc-modal {
	align-items: flex-start;
	background: rgba(15,23,42,.55);
	box-sizing: border-box;
	display: none;
	inset: 0;
	justify-content: center;
	overflow-x: hidden;
	overflow-y: auto;
	padding: 12px;
	position: fixed;
	z-index: 10050;
}
.epc-vc-modal.is-open { display: flex; }
.epc-vc-modal.is-anchored {
	align-items: stretch;
	justify-content: stretch;
	padding: 0;
}
.epc-vc-modal.is-anchored .epc-vc-modal__panel {
	left: 50%;
	margin: 0;
	position: absolute;
	top: var(--epc-vc-modal-anchor-top, 12px);
	transform: translateX(-50%);
}
.epc-vc-modal__panel {
	background: #fff;
	border-radius: 16px;
	box-shadow: 0 24px 60px rgba(15,23,42,.25);
	flex: 0 0 auto;
	margin: 24px auto 32px;
	max-height: calc(100vh - 48px);
	max-width: 1080px;
	overflow: auto;
	padding: 18px;
	width: min(1080px, calc(100vw - 24px));
}
body.epc-vc-modal-open {
	overflow: hidden;
	width: 100%;
}
.epc-vc-modal__head {
	align-items: center;
	border-bottom: 1px solid #e2e8f0;
	display: flex;
	gap: 10px;
	justify-content: space-between;
	margin-bottom: 14px;
	padding-bottom: 12px;
}
.epc-vc-product-grid {
	display: grid;
	gap: 18px;
	grid-template-columns: 280px minmax(0, 1fr) 220px;
}
.epc-vc-product-media img {
	border: 1px solid #e2e8f0;
	border-radius: 10px;
	max-height: 220px;
	max-width: 100%;
	object-fit: contain;
}
.epc-vc-spec-table { border-collapse: collapse; width: 100%; }
.epc-vc-spec-table th,
.epc-vc-spec-table td { border: 1px solid #e2e8f0; font-size: 13px; padding: 8px 10px; }
.epc-vc-spec-table th { background: #f8fafc; color: #64748b; font-weight: 700; text-align: left; width: 42%; }
.epc-vc-oe-list { font-size: 12px; line-height: 1.6; max-height: 200px; overflow: auto; }
.epc-vc-tabs-inner { display: flex; gap: 8px; margin: 12px 0; }
.epc-vc-tabs-inner button {
	background: #f1f5f9;
	border: 0;
	border-radius: 8px;
	font-weight: 800;
	padding: 8px 12px;
}
.epc-vc-tabs-inner button.active { background: #2563eb; color: #fff; }
.epc-vc-price-box {
	background: #f0fdf4;
	border: 1px solid #bbf7d0;
	border-radius: 12px;
	padding: 14px;
}
.epc-vc-price-box .btn { margin-top: 10px; width: 100%; }
@media (max-width: 991px) {
	.epc-vc-hero-grid { grid-template-columns: 1fr; }
	.epc-vc-searchbar { grid-template-columns: 1fr; }
	.epc-vc-layout { grid-template-columns: 1fr; }
	.epc-vc-product-grid { grid-template-columns: 1fr; }
}
@media (max-width: 575px) {
	.epc-vc-cat-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
</style>

<div class="epc-vc" id="epc-vehicle-catalog" data-lang-href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>">
	<div class="epc-vc-searchbar">
		<input type="search" class="form-control" id="epc-vc-search-part" placeholder="Part number, OE number, article">
		<input type="search" class="form-control" id="epc-vc-search-vin" placeholder="VIN or engine code">
		<button type="button" class="btn btn-warning" id="epc-vc-search-go"><i class="fa fa-search"></i> Search</button>
	</div>

	<div class="epc-vc-tabs">
		<button type="button" class="epc-vc-tab active" data-section="passenger">Passenger</button>
		<button type="button" class="epc-vc-tab" data-section="commercial">Commercial</button>
		<button type="button" class="epc-vc-tab" data-section="motorbike">Motorbike</button>
	</div>

	<div class="epc-vc-hero-grid">
		<div class="epc-vc-picker">
			<h2>Select your vehicle</h2>
			<div class="epc-vc-step-row" id="epc-vc-step-year">
				<span class="epc-vc-step-num">1</span>
				<select id="epc-vc-year" class="form-control"><option value="">Model year</option></select>
			</div>
			<div class="epc-vc-step-row is-muted" id="epc-vc-step-make">
				<span class="epc-vc-step-num">2</span>
				<select id="epc-vc-make" class="form-control"><option value="">Make</option></select>
			</div>
			<div class="epc-vc-step-row is-muted" id="epc-vc-step-model">
				<span class="epc-vc-step-num">3</span>
				<select id="epc-vc-model" class="form-control" disabled><option value="">Model</option></select>
			</div>
			<div class="epc-vc-step-row is-muted" id="epc-vc-step-engine">
				<span class="epc-vc-step-num">4</span>
				<select id="epc-vc-engine" class="form-control" disabled><option value="">Engine / modification</option></select>
			</div>
			<div class="epc-vc-picker-help">Need help? <a href="<?php echo htmlspecialchars($lang_href, ENT_QUOTES, 'UTF-8'); ?>/kontakty">Contact us</a></div>
		</div>
		<div class="epc-vc-promo" id="epc-vc-promo">
			<div class="epc-vc-promo-body" id="epc-vc-promo-body">
				<strong>GLOBAL PARTS CATALOG</strong>
				<p>Browse OEM-style categories, product groups and articles with technical measurements from the eparts catalog. Prices are checked in the shop after you pick a part.</p>
				<p class="epc-vc-promo-vin-meta" style="margin-top:12px;">Enter a VIN or engine code (e.g. 3L, 12R, 5L) above and press Search — decode the vehicle or open parts by engine here.</p>
			</div>
		</div>
	</div>

	<div id="epc-vc-workspace" style="display:none;">
		<div class="epc-vc-vehicle-bar" id="epc-vc-vehicle-bar"></div>
		<div class="epc-vc-info-panels" id="epc-vc-info-panels"></div>

		<p class="epc-vc-section-title">Catalog — parts by assembly</p>
		<input type="search" class="form-control epc-vc-cat-search" id="epc-vc-cat-filter" placeholder="Find category…">
		<div class="epc-vc-cat-grid" id="epc-vc-cat-grid"></div>

		<div class="epc-vc-layout">
			<aside class="epc-vc-sidebar">
				<h3>All categories</h3>
				<div id="epc-vc-sidebar-body"><div class="epc-vc-message">Loading categories…</div></div>
			</aside>
			<main class="epc-vc-main">
				<div class="epc-vc-breadcrumb" id="epc-vc-breadcrumb"></div>
				<h3 id="epc-vc-main-title">Product groups</h3>
				<input type="search" class="form-control" id="epc-vc-filter" placeholder="Filter list" style="max-width:280px;margin-bottom:10px;">
				<div id="epc-vc-main-body"><div class="epc-vc-message">Choose a category to see product groups and articles.</div></div>
			</main>
		</div>
	</div>
</div>

<div class="epc-vc-modal" id="epc-vc-modal" aria-hidden="true">
	<div class="epc-vc-modal__panel" tabindex="-1">
		<div class="epc-vc-modal__head">
			<h3 id="epc-vc-modal-title" style="margin:0;">Part details</h3>
			<button type="button" class="btn btn-default" id="epc-vc-modal-close">Close</button>
		</div>
		<div id="epc-vc-modal-body"><div class="epc-vc-loader">Loading…</div></div>
	</div>
</div>

<script>
(function () {
	'use strict';
	var root = document.getElementById('epc-vehicle-catalog');
	if (!root) { return; }

	var yearSelect = document.getElementById('epc-vc-year');
	var makeSelect = document.getElementById('epc-vc-make');
	var modelSelect = document.getElementById('epc-vc-model');
	var engineSelect = document.getElementById('epc-vc-engine');
	var workspace = document.getElementById('epc-vc-workspace');
	var vehicleBar = document.getElementById('epc-vc-vehicle-bar');
	var infoPanels = document.getElementById('epc-vc-info-panels');
	var catGrid = document.getElementById('epc-vc-cat-grid');
	var catFilter = document.getElementById('epc-vc-cat-filter');
	var sidebarBody = document.getElementById('epc-vc-sidebar-body');
	var mainBody = document.getElementById('epc-vc-main-body');
	var mainTitle = document.getElementById('epc-vc-main-title');
	var breadcrumb = document.getElementById('epc-vc-breadcrumb');
	var filterInput = document.getElementById('epc-vc-filter');
	var modal = document.getElementById('epc-vc-modal');
	var modalPanel = modal ? modal.querySelector('.epc-vc-modal__panel') : null;
	var modalBody = document.getElementById('epc-vc-modal-body');
	var modalTitle = document.getElementById('epc-vc-modal-title');
	var inlineDetail = null;
	var activeDetailRow = null;
	var promoBody = document.getElementById('epc-vc-promo-body');
	var promoDefaultHtml = promoBody ? promoBody.innerHTML : '';
	var vinInput = document.getElementById('epc-vc-search-vin');
	var langHref = root.getAttribute('data-lang-href') || '';
	var epcVcChpuOn = <?php echo $epc_vc_chpu_on ? 'true' : 'false'; ?>;
	var epcVcChpuPartsUrl = <?php echo json_encode($epc_vc_chpu_parts_url, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var epcVcChpuBrandsUrl = <?php echo json_encode($epc_vc_chpu_brands_url, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var epcVcChpuSlashCode = <?php echo json_encode($epc_vc_chpu_slash_code, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
	var savedScrollY = 0;

	var state = {
		section: 'passenger',
		manufacturers: [],
		models: [],
		modifications: [],
		manufacturer: null,
		model: null,
		modification: null,
		categories: null,
		currentCategoryId: '',
		currentCategoryLabel: '',
		currentProductLabel: '',
		vehicleType: '',
		engineCatalogMode: false
	};

	var CATEGORY_ICONS = [
		{ re: /filter|oil/i, icon: 'fa-filter' },
		{ re: /brake/i, icon: 'fa-stop-circle' },
		{ re: /suspension|shock|damp/i, icon: 'fa-compress' },
		{ re: /belt|timing/i, icon: 'fa-circle-o' },
		{ re: /clutch/i, icon: 'fa-cog' },
		{ re: /ignition|spark|glow/i, icon: 'fa-bolt' },
		{ re: /engine/i, icon: 'fa-cogs' },
		{ re: /body|bumper|wing/i, icon: 'fa-car' },
		{ re: /electric|light|lamp/i, icon: 'fa-lightbulb-o' },
		{ re: /wheel|tyre|tire/i, icon: 'fa-life-ring' },
		{ re: /cool|radiat|heat/i, icon: 'fa-thermometer-half' },
		{ re: /exhaust/i, icon: 'fa-cloud' },
		{ re: /steer/i, icon: 'fa-exchange' },
		{ re: /fuel/i, icon: 'fa-tint' },
		{ re: /wiper|glass/i, icon: 'fa-eye' }
	];

	function text(v) { return String(v == null ? '' : v); }
	function esc(v) {
		return text(v).replace(/[&<>"']/g, function (ch) {
			return ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'})[ch];
		});
	}
	function parseCiDate(v) {
		v = text(v).trim();
		if (!v) { return null; }
		var parts = v.split('-');
		if (parts.length < 2) { return null; }
		var y = parseInt(parts[0], 10);
		var m = parseInt(parts[1], 10) || 1;
		var d = parseInt(parts[2], 10) || 1;
		if (!y) { return null; }
		return new Date(y, m - 1, d);
	}
	function yearFromDate(v) {
		var dt = parseCiDate(v);
		return dt ? dt.getFullYear() : 0;
	}
	function productionEndDate(item) {
		var to = parseCiDate(item.CI_TO);
		return to || new Date(9999, 11, 31, 23, 59, 59);
	}
	function formatYearRange(item) {
		var from = yearFromDate(item.CI_FROM);
		var to = yearFromDate(item.CI_TO);
		if (!from && !to) { return ''; }
		if (!to) { return from ? String(from) + ' –' : ''; }
		return from === to ? String(from) : from + ' – ' + to;
	}
	function cleanDate(v) {
		var y = yearFromDate(v);
		return y ? String(y) : '';
	}
	function yearMatches(item, year) {
		if (!year) { return false; }
		var from = parseCiDate(item.CI_FROM);
		if (!from) { return false; }
		var to = productionEndDate(item);
		var yearStart = new Date(year, 0, 1);
		var yearEnd = new Date(year, 11, 31, 23, 59, 59);
		return from.getTime() <= yearEnd.getTime() && to.getTime() >= yearStart.getTime();
	}
	function filterByYear(items, year) {
		var list = Array.isArray(items) ? items : [];
		if (!year) { return list; }
		return list.filter(function (row) { return yearMatches(row, year); });
	}
	function selectedYear() {
		var y = parseInt(yearSelect.value, 10);
		return y > 0 ? y : 0;
	}
	function setStepState(step) {
		var steps = ['year', 'make', 'model', 'engine'];
		steps.forEach(function (name, index) {
			var row = document.getElementById('epc-vc-step-' + name);
			if (!row) { return; }
			row.classList.toggle('is-muted', index > step);
		});
	}
	function api(action, params) {
		var q = new URLSearchParams();
		q.set('action', action);
		q.set('section', state.section);
		q.set('language', 'en');
		q.set('region', 'WWW');
		if (state.vehicleType && ['models', 'modifications', 'categories', 'products', 'articles', 'article', 'engine_search'].indexOf(action) !== -1) {
			q.set('vehicle_type', state.vehicleType);
		}
		Object.keys(params || {}).forEach(function (key) {
			if (params[key] !== undefined && params[key] !== null && params[key] !== '') {
				q.set(key, params[key]);
			}
		});
		return fetch('/api/umapi_proxy.php?' + q.toString(), { credentials: 'same-origin' }).then(function (res) {
			return res.json().then(function (data) {
				if (!res.ok && !(data && (data.data || data.root || data.matchingVehicles))) {
					throw data;
				}
				return data;
			});
		});
	}
	function apiItems(data) {
		if (Array.isArray(data)) {
			return data;
		}
		if (data && Array.isArray(data.data)) {
			return data.data;
		}
		return [];
	}
	function apiObject(data) {
		if (!data || typeof data !== 'object') {
			return data;
		}
		if (data.data && typeof data.data === 'object' && !Array.isArray(data.data)) {
			return data.data;
		}
		return data;
	}
	function fillSelect(select, placeholder, rows, labelFn, disabled) {
		select.innerHTML = '<option value="">' + esc(placeholder) + '</option>' +
			rows.map(function (row, index) {
				return '<option value="' + index + '">' + esc(labelFn(row)) + '</option>';
			}).join('');
		select.disabled = !!disabled;
	}
	function getModificationId(item) {
		if (!item) { return ''; }
		if (state.engineCatalogMode) {
			return item.ENG_ID || item.ID || '';
		}
		return item.PC_ID || item.CV_ID || item.MTB_ID || item.ID || item.MOD_ID || '';
	}
	function getModificationTitle(item) {
		return item.PASSENGER_CAR || item.COMMERCIAL_VEHICLE || item.MOTORBIKE || item.MODIFICATION || item.DES || item.MODEL_SERIES || 'Vehicle';
	}
	function chooseVehicleType(item) {
		var types = item && item.EPART_TYPES ? item.EPART_TYPES : [];
		return types.length ? types[0] : '';
	}
	function categoryIcon(name) {
		var label = text(name);
		for (var i = 0; i < CATEGORY_ICONS.length; i++) {
			if (CATEGORY_ICONS[i].re.test(label)) {
				return CATEGORY_ICONS[i].icon;
			}
		}
		return 'fa-wrench';
	}
	function articleImageUrl(item) {
		var file = item.MEDIA_FILE || '';
		var sup = item.SUP_ID || '';
		if (!file || !sup) { return ''; }
		return 'https://image.umapi.ru/IMAGE/' + encodeURIComponent(sup) + '/' + encodeURIComponent(file);
	}
	function normalizeArticleForUrl(article) {
		return String(article || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
	}
	function shopSearchUrl(brand, article) {
		var articleNorm = normalizeArticleForUrl(article);
		var brandName = String(brand || '').trim();
		if (!articleNorm) {
			return langHref + '/shop/part_search';
		}
		if (epcVcChpuOn) {
			if (!brandName) {
				return langHref + '/' + epcVcChpuPartsUrl + '/' + epcVcChpuBrandsUrl + '/' + encodeURIComponent(articleNorm);
			}
			var manufacturerAlias = brandName.split('/').join(epcVcChpuSlashCode);
			return langHref + '/' + epcVcChpuPartsUrl + '/' + encodeURIComponent(manufacturerAlias) + '/' + encodeURIComponent(articleNorm);
		}
		var legacyUrl = langHref + '/shop/part_search?article=' + encodeURIComponent(articleNorm);
		if (brandName) {
			legacyUrl += '&brend=' + encodeURIComponent(brandName);
		}
		return legacyUrl;
	}
	function applyFilter(container) {
		var term = (filterInput.value || '').toLowerCase();
		if (!container) { return; }
		Array.prototype.forEach.call(container.querySelectorAll('[data-search]'), function (node) {
			node.style.display = node.getAttribute('data-search').toLowerCase().indexOf(term) === -1 ? 'none' : '';
		});
	}
	function applyCategoryFilter() {
		var term = (catFilter.value || '').toLowerCase();
		Array.prototype.forEach.call(catGrid.querySelectorAll('.epc-vc-cat-card'), function (node) {
			node.style.display = node.getAttribute('data-search').toLowerCase().indexOf(term) === -1 ? 'none' : '';
		});
	}
	function buildYearOptions() {
		var current = new Date().getFullYear();
		var html = '<option value="">All years (optional)</option>';
		for (var y = current + 1; y >= 1985; y--) {
			html += '<option value="' + y + '">' + y + '</option>';
		}
		yearSelect.innerHTML = html;
		setStepState(1);
	}
	function resetWorkspace() {
		workspace.style.display = 'none';
		state.categories = null;
		state.currentCategoryId = '';
	}
	function renderVehicleBar() {
		if (!state.modification) { return; }
		var title;
		var meta;
		if (state.engineCatalogMode) {
			var m = state.modification;
			title = [m.MANUFACTURER, m.ENGINE_CODE || m.ENG_CODE].filter(Boolean).join(' ');
			meta = [
				formatYearRange(m),
				m.CAPACITY_CCM_START ? m.CAPACITY_CCM_START + ' ccm' : '',
				m.POWER_PS_START ? m.POWER_PS_START + ' hp' : (m.POWER_PS ? m.POWER_PS + ' hp' : ''),
				m.POWER_KW_START ? m.POWER_KW_START + ' kW' : (m.POWER_KW ? m.POWER_KW + ' kW' : ''),
				m.FUEL_TYPE || '',
				m.NUMBER_OF_CYLINDERS ? m.NUMBER_OF_CYLINDERS + ' cyl' : ''
			].filter(Boolean).join(' · ');
		} else {
			if (!state.manufacturer || !state.model) { return; }
			title = [
				state.manufacturer.MANUFACTURER,
				state.model.MODEL_SERIES,
				getModificationTitle(state.modification)
			].filter(Boolean).join(' ');
			meta = [
				selectedYear() ? String(selectedYear()) : 'All years',
				formatYearRange(state.modification),
				state.modification.POWER_PS ? state.modification.POWER_PS + ' hp' : '',
				state.modification.POWER_KW ? state.modification.POWER_KW + ' kW' : '',
				state.modification.FUEL_TYPE || ''
			].filter(Boolean).join(' · ');
		}
		vehicleBar.innerHTML = '<div><strong>' + esc(title) + '</strong><span>' + esc(meta) + '</span></div>' +
			'<a class="btn btn-default btn-sm" href="' + esc(langHref + '/umapi_catalog') + '">Open Epart Catalog</a>';
	}
	function renderInfoPanels() {
		if (!state.modification) { return; }
		var m = state.modification;
		if (state.engineCatalogMode) {
			var engineText = [
				m.ENGINE_CODE || m.ENG_CODE,
				m.CAPACITY_CCM_START ? m.CAPACITY_CCM_START + ' ccm' : '',
				m.POWER_KW_START ? m.POWER_KW_START + ' kW' : (m.POWER_KW ? m.POWER_KW + ' kW' : ''),
				m.POWER_PS_START ? m.POWER_PS_START + ' hp' : (m.POWER_PS ? m.POWER_PS + ' hp' : ''),
				m.FUEL_TYPE,
				m.NUMBER_OF_CYLINDERS ? m.NUMBER_OF_CYLINDERS + ' cyl' : ''
			].filter(Boolean).join(', ');
			infoPanels.innerHTML =
				'<div class="epc-vc-info-panel is-engine">' +
					'<h4>Engine code</h4>' +
					'<p>' + esc(engineText || 'Engine') + '</p>' +
					'<div class="epc-vc-info-icons">' +
						'<span>Oil filter</span><span>Timing</span><span>Glow plug</span><span>Turbo</span>' +
					'</div>' +
				'</div>';
			return;
		}
		var vehicleText = (state.manufacturer.MANUFACTURER || '') + ' ' + (state.model.MODEL_SERIES || '') + ' ' + formatYearRange(m);
		var engineText = [
			m.ENGINE_TYPE,
			m.POWER_KW ? m.POWER_KW + ' kW / ' + (m.POWER_PS || '?') + ' Hp' : '',
			m.FUEL_TYPE,
			m.CAPACITY_LT ? m.CAPACITY_LT + ' L' : '',
			m.NUMBER_OF_CYLINDERS ? m.NUMBER_OF_CYLINDERS + ' cyl' : ''
		].filter(Boolean).join(', ');
		infoPanels.innerHTML =
			'<div class="epc-vc-info-panel">' +
				'<h4>Vehicle</h4>' +
				'<p>' + esc(vehicleText.trim()) + '</p>' +
				'<div class="epc-vc-info-icons">' +
					'<span>Filters</span><span>Brakes</span><span>Battery</span><span>Service</span>' +
				'</div>' +
			'<div class="epc-vc-info-panel is-engine">' +
				'<h4>Engine</h4>' +
				'<p>' + esc(engineText || getModificationTitle(m)) + '</p>' +
				'<div class="epc-vc-info-icons">' +
					'<span>Oil filter</span><span>Timing</span><span>Glow plug</span><span>Turbo</span>' +
				'</div>' +
			'</div>';
	}
	function collectQuickCategories(data) {
		var list = [];
		var quick = data.quic || [];
		quick.forEach(function (item) {
			var id = item.CATEGORY_IDS && item.CATEGORY_IDS.length ? item.CATEGORY_IDS[0] : item.CATEGORY_ID;
			if (id) {
				list.push({ id: id, label: item.DES || 'Category' });
			}
		});
		if (list.length < 12 && data.root) {
			function walk(nodes, depth) {
				if (!nodes || depth > 2) { return; }
				nodes.forEach(function (node) {
					if (list.length >= 12) { return; }
					if (node.CATEGORY_ID && node.DES) {
						var exists = list.some(function (x) { return String(x.id) === String(node.CATEGORY_ID); });
						if (!exists) {
							list.push({ id: node.CATEGORY_ID, label: node.DES });
						}
					}
					walk(node.CHILD || node.children || [], depth + 1);
				});
			}
			walk(data.root, 0);
		}
		return list;
	}
	function renderCategoryGrid(list) {
		catGrid.innerHTML = list.map(function (item) {
			var icon = categoryIcon(item.label);
			return '<button type="button" class="epc-vc-cat-card" data-category="' + esc(item.id) + '" data-label="' + esc(item.label) + '" data-search="' + esc(item.label) + '">' +
				'<div class="epc-vc-cat-icon"><i class="fa ' + icon + '"></i></div>' +
				'<strong>' + esc(item.label) + '</strong></button>';
		}).join('');
		Array.prototype.forEach.call(catGrid.querySelectorAll('[data-category]'), function (btn) {
			btn.onclick = function () {
				state.currentCategoryId = btn.getAttribute('data-category');
				state.currentCategoryLabel = btn.getAttribute('data-label') || '';
				loadProducts(state.currentCategoryId);
				highlightTreeCategory(state.currentCategoryId);
			};
		});
		applyCategoryFilter();
	}
	function renderCategoryTree(nodes, level) {
		if (!nodes || !nodes.length || level > 5) { return ''; }
		return '<ul>' + nodes.map(function (node) {
			var children = node.CHILD || node.children || [];
			var active = String(node.CATEGORY_ID) === String(state.currentCategoryId) ? ' class="active"' : '';
			return '<li data-search="' + esc(node.DES || '') + '">' +
				'<button type="button" data-category="' + esc(node.CATEGORY_ID) + '" data-label="' + esc(node.DES || '') + '"' + active + '>' + esc(node.DES || 'Category') + '</button>' +
				renderCategoryTree(children, level + 1) + '</li>';
		}).join('') + '</ul>';
	}
	function highlightTreeCategory(categoryId) {
		Array.prototype.forEach.call(sidebarBody.querySelectorAll('[data-category]'), function (btn) {
			btn.classList.toggle('active', String(btn.getAttribute('data-category')) === String(categoryId));
		});
	}
	function renderCategories(data) {
		data = apiObject(data);
		if (!data || (!data.root && !data.quic)) {
			sidebarBody.innerHTML = '<div class="epc-vc-message">No categories found for this vehicle.</div>';
			catGrid.innerHTML = '';
			return;
		}
		state.categories = data;
		var quickList = collectQuickCategories(data);
		renderCategoryGrid(quickList);
		sidebarBody.innerHTML = '<div class="epc-vc-tree">' + renderCategoryTree(data.root || [], 0) + '</div>';
		Array.prototype.forEach.call(sidebarBody.querySelectorAll('[data-category]'), function (btn) {
			btn.onclick = function () {
				state.currentCategoryId = btn.getAttribute('data-category');
				state.currentCategoryLabel = btn.getAttribute('data-label') || '';
				highlightTreeCategory(state.currentCategoryId);
				loadProducts(state.currentCategoryId);
			};
		});
		mainTitle.textContent = 'Product groups';
		mainBody.innerHTML = '<div class="epc-vc-message">Select a category above or on the left.</div>';
		updateBreadcrumb();
	}
	function updateBreadcrumb() {
		var parts = [];
		if (state.currentCategoryLabel) {
			parts.push('<button type="button" data-bc="category">' + esc(state.currentCategoryLabel) + '</button>');
		}
		if (state.currentProductLabel) {
			parts.push('<span>' + esc(state.currentProductLabel) + '</span>');
		}
		breadcrumb.innerHTML = parts.length ? parts.join(' › ') : '';
		Array.prototype.forEach.call(breadcrumb.querySelectorAll('[data-bc="category"]'), function (btn) {
			btn.onclick = function () {
				state.currentProductLabel = '';
				if (state.currentCategoryId) { loadProducts(state.currentCategoryId); }
			};
		});
	}
	function loadCategories() {
		if (state.engineCatalogMode && state.modification) {
			workspace.style.display = '';
			renderVehicleBar();
			renderInfoPanels();
			sidebarBody.innerHTML = '<div class="epc-vc-loader">Loading categories…</div>';
			mainBody.innerHTML = '<div class="epc-vc-loader">Loading categories…</div>';
			api('categories', { ID: getModificationId(state.modification) }).then(renderCategories).catch(function () {
				sidebarBody.innerHTML = '<div class="epc-vc-message">Categories could not be loaded for this engine.</div>';
			});
			return;
		}
		var idx = engineSelect.value;
		if (idx === '') { return; }
		state.engineCatalogMode = false;
		state.modification = state.modifications[parseInt(idx, 10)];
		workspace.style.display = '';
		renderVehicleBar();
		renderInfoPanels();
		sidebarBody.innerHTML = '<div class="epc-vc-loader">Loading categories…</div>';
		mainBody.innerHTML = '<div class="epc-vc-loader">Loading categories…</div>';
		api('categories', { ID: getModificationId(state.modification) }).then(renderCategories).catch(function () {
			sidebarBody.innerHTML = '<div class="epc-vc-message">Categories could not be loaded.</div>';
		});
	}
	function loadProducts(categoryId) {
		if (!state.modification) { return; }
		state.currentProductLabel = '';
		mainTitle.textContent = state.currentCategoryLabel || 'Product groups';
		mainBody.innerHTML = '<div class="epc-vc-loader">Loading product groups…</div>';
		updateBreadcrumb();
		api('products', { CATEGORY_ID: categoryId, ID: getModificationId(state.modification) }).then(function (items) {
			var rows = apiItems(items);
			if (!rows.length) {
				mainBody.innerHTML = '<div class="epc-vc-message">No product groups in this category.</div>';
				return;
			}
			mainBody.innerHTML = '<div class="epc-vc-cards">' + rows.map(function (item, index) {
				var label = item.PT_DES || item.DES || item.PRODUCT_GROUP || item.PRODUCT || 'Group';
				return '<button type="button" class="epc-vc-card" data-index="' + index + '" data-search="' + esc(label) + '">' +
					'<strong>' + esc(label) + '</strong></button>';
			}).join('') + '</div>';
			Array.prototype.forEach.call(mainBody.querySelectorAll('.epc-vc-card'), function (btn) {
				btn.onclick = function () {
					var item = rows[parseInt(btn.getAttribute('data-index'), 10)];
					state.currentProductLabel = item.PT_DES || item.DES || 'Articles';
					loadArticles(item.PT_ID || item.PT_IDS || item.ID);
				};
			});
			applyFilter(mainBody);
		}).catch(function () {
			mainBody.innerHTML = '<div class="epc-vc-message">Product groups could not be loaded.</div>';
		});
	}
	function closeInlineDetail() {
		if (activeDetailRow) {
			activeDetailRow.classList.remove('is-detail-open');
			activeDetailRow = null;
		}
		if (inlineDetail && inlineDetail.parentNode) {
			inlineDetail.parentNode.removeChild(inlineDetail);
		}
		inlineDetail = null;
	}
	function ensureInlineDetail() {
		if (inlineDetail) { return inlineDetail; }
		inlineDetail = document.createElement('div');
		inlineDetail.className = 'epc-vc-inline-detail';
		inlineDetail.innerHTML =
			'<div class="epc-vc-inline-detail__head">' +
				'<h4 class="epc-vc-inline-detail__title"></h4>' +
				'<button type="button" class="btn btn-default btn-xs epc-vc-inline-detail__close">Close</button>' +
			'</div>' +
			'<div class="epc-vc-inline-detail__body"></div>';
		inlineDetail.querySelector('.epc-vc-inline-detail__close').onclick = closeInlineDetail;
		return inlineDetail;
	}
	function openInlineDetail(anchorRow) {
		closeInlineDetail();
		var panel = ensureInlineDetail();
		activeDetailRow = anchorRow && anchorRow.classList && anchorRow.classList.contains('epc-vc-article-row')
			? anchorRow
			: (anchorRow && anchorRow.closest ? anchorRow.closest('.epc-vc-article-row') : null);
		if (activeDetailRow) {
			activeDetailRow.classList.add('is-detail-open');
			activeDetailRow.insertAdjacentElement('afterend', panel);
		} else if (mainBody) {
			mainBody.appendChild(panel);
		}
		panel.classList.add('is-open');
		requestAnimationFrame(function () {
			panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
		});
		return {
			panel: panel,
			title: panel.querySelector('.epc-vc-inline-detail__title'),
			body: panel.querySelector('.epc-vc-inline-detail__body')
		};
	}
	function renderArticleList(items) {
		closeInlineDetail();
		mainTitle.textContent = state.currentProductLabel || 'Articles';
		updateBreadcrumb();
		mainBody.innerHTML = '<div class="epc-vc-articles">' + items.map(function (item, index) {
			var brand = item.SUP_BRAND || item.BRAND || '';
			var article = item.ART_ARTICLE_NR || item.ARTICLE || '';
			var name = item.ART_PRODUCT_NAME || item.DES || item.NAME || '';
			var img = articleImageUrl(item);
			var thumb = img
				? '<img src="' + esc(img) + '" alt="" loading="lazy" onerror="this.parentNode.innerHTML=\'<i class=\\\'fa fa-image\\\'></i>\';">'
				: '<i class="fa fa-image"></i>';
			var priceUrl = shopSearchUrl(brand, article);
			return '<div class="epc-vc-article-row" data-index="' + index + '" data-search="' + esc([brand, article, name].join(' ')) + '">' +
				'<div class="epc-vc-article-thumb">' + thumb + '</div>' +
				'<div><strong>' + esc(brand) + ' ' + esc(article) + '</strong><br><small>' + esc(name) + '</small></div>' +
				'<div class="epc-vc-article-actions">' +
				'<a class="btn btn-xs btn-success" href="' + esc(priceUrl) + '" title="Search ' + esc(brand) + ' ' + esc(article) + ' in shop"><i class="fa fa-search"></i> Price</a>' +
				'<button type="button" class="btn btn-xs btn-default epc-vc-details-btn">Details</button></div></div>';
		}).join('') + '</div>';
		Array.prototype.forEach.call(mainBody.querySelectorAll('.epc-vc-article-row'), function (row) {
			var btn = row.querySelector('.epc-vc-details-btn');
			if (!btn) { return; }
			btn.onclick = function (e) {
				e.preventDefault();
				e.stopPropagation();
				var item = items[parseInt(row.getAttribute('data-index'), 10)];
				openArticleDetail(item, btn);
			};
		});
		applyFilter(mainBody);
	}
	function loadArticles(productId) {
		mainBody.innerHTML = '<div class="epc-vc-loader">Loading articles…</div>';
		api('articles', { PT_IDS: productId, ID: getModificationId(state.modification), limit: 80, offset: 0 }).then(function (data) {
			var items = apiItems(data);
			if (!items.length) {
				mainBody.innerHTML = '<div class="epc-vc-message">No articles found for this product group.</div>';
				return;
			}
			renderArticleList(items);
		}).catch(function () {
			mainBody.innerHTML = '<div class="epc-vc-message">Articles could not be loaded.</div>';
		});
	}
	function criteriaRows(detail) {
		var rows = [];
		['CRITERIAS', 'LA_CRITERIAS', 'criterias', 'la_criterias'].forEach(function (key) {
			var list = detail[key];
			if (Array.isArray(list)) {
				list.forEach(function (c) { rows.push(c); });
			}
		});
		return rows;
	}
	function oeRows(detail) {
		var rows = [];
		var list = detail.OE_CODES || detail.oe_codes || [];
		if (Array.isArray(list)) {
			list.forEach(function (oe) {
				if (typeof oe === 'string') {
					rows.push({ brand: '', number: oe });
				} else {
					rows.push({
						brand: oe.BRAND || oe.MANUFACTURER || oe.MFA_BRAND || '',
						number: oe.OE || oe.OEN || oe.ARTICLE || oe.NUMBER || ''
					});
				}
			});
		}
		return rows;
	}
	function renderArticleDetail(detail, preview, targetBody, targetTitle) {
		var bodyEl = targetBody || modalBody;
		var titleEl = targetTitle || modalTitle;
		var brand = detail.SUP_BRAND || detail.BRAND || preview.SUP_BRAND || '';
		var article = detail.ART_ARTICLE_NR || detail.ARTICLE || preview.ART_ARTICLE_NR || '';
		var name = detail.ART_PRODUCT_NAME || detail.COMPLETE_DES || detail.DES || preview.ART_PRODUCT_NAME || '';
		var img = articleImageUrl(detail.SUP_ID ? detail : preview);
		var specs = criteriaRows(detail);
		var oes = oeRows(detail);
		var specHtml = specs.length
			? '<table class="epc-vc-spec-table"><tbody>' + specs.map(function (c) {
				var label = c.CRI_SHORT_DES || c.CRI_DES || c.DES || 'Specification';
				var val = c.VALUE || '';
				var unit = c.CRI_UNIT_DES || '';
				return '<tr><th>' + esc(label) + '</th><td>' + esc(val + (unit ? ' ' + unit : '')) + '</td></tr>';
			}).join('') + '</tbody></table>'
			: '<p class="epc-vc-message">No technical measurements returned for this article.</p>';
		var oeHtml = oes.length
			? '<div class="epc-vc-oe-list">' + oes.map(function (oe) {
				return '<div><strong>' + esc(oe.brand || 'OE') + '</strong> ' + esc(oe.number) + '</div>';
			}).join('') + '</div>'
			: '<p class="epc-vc-message">No OE numbers found in Epart catalog for this article.</p>';
		if (titleEl) {
			titleEl.textContent = brand + ' ' + article + (name ? ' — ' + name : '');
		}
		if (!bodyEl) { return; }
		bodyEl.innerHTML =
			'<div class="epc-vc-product-grid">' +
				'<div class="epc-vc-product-media">' +
					(img ? '<img src="' + esc(img) + '" alt="">' : '<div class="epc-vc-message">No image</div>') +
				'</div>' +
				'<div>' +
					'<h4 style="margin:0 0 8px;">' + esc(name || 'Part') + '</h4>' +
					'<p style="color:#64748b;font-size:13px;margin:0 0 10px;">' + esc(brand) + ' · ' + esc(article) + '</p>' +
					'<h5 style="font-weight:900;margin:0 0 8px;">Specifications &amp; measurements</h5>' +
					specHtml +
					'<div class="epc-vc-tabs-inner"><button type="button" class="active" data-tab="oe">OE numbers</button><button type="button" data-tab="info">Info</button></div>' +
					'<div id="epc-vc-tab-oe">' + oeHtml + '</div>' +
					'<div id="epc-vc-tab-info" style="display:none;">' +
						(Array.isArray(detail.INFO) && detail.INFO.length
							? detail.INFO.map(function (i) { return '<p>' + esc(i.TEXT || i.DES || JSON.stringify(i)) + '</p>'; }).join('')
							: '<p class="epc-vc-message">No extra info.</p>') +
					'</div>' +
				'</div>' +
				'<div class="epc-vc-price-box">' +
					'<strong style="display:block;font-size:14px;margin-bottom:6px;">Shop price</strong>' +
					'<p style="font-size:12px;color:#475569;margin:0 0 8px;">Check live stock and price in the shop catalog.</p>' +
					'<a class="btn btn-success" href="' + esc(shopSearchUrl(brand, article)) + '"><i class="fa fa-search"></i> Search price</a>' +
					'<button type="button" class="btn btn-default" id="epc-vc-fitment-btn" style="margin-top:8px;">Vehicle fitment</button>' +
				'</div>' +
			'</div>';
		Array.prototype.forEach.call(bodyEl.querySelectorAll('.epc-vc-tabs-inner button'), function (tabBtn) {
			tabBtn.onclick = function () {
				Array.prototype.forEach.call(bodyEl.querySelectorAll('.epc-vc-tabs-inner button'), function (b) { b.classList.remove('active'); });
				tabBtn.classList.add('active');
				document.getElementById('epc-vc-tab-oe').style.display = tabBtn.getAttribute('data-tab') === 'oe' ? '' : 'none';
				document.getElementById('epc-vc-tab-info').style.display = tabBtn.getAttribute('data-tab') === 'info' ? '' : 'none';
			};
		});
		var fitBtn = document.getElementById('epc-vc-fitment-btn');
		if (fitBtn) {
			fitBtn.onclick = function () {
				if (window.epcOpenApplicability && article) {
					window.epcOpenApplicability(article);
					closeInlineDetail();
				}
			};
		}
	}
	function positionModal(anchorEl) {
		if (!modal || !modalPanel) { return; }
		modal.classList.remove('is-anchored');
		modal.style.removeProperty('--epc-vc-modal-anchor-top');
		modal.scrollTop = 0;
		if (!anchorEl || !anchorEl.getBoundingClientRect) { return; }
		var rect = anchorEl.getBoundingClientRect();
		var panelMax = Math.min(window.innerHeight * 0.85, 720);
		var top = rect.top;
		if (top + panelMax > window.innerHeight - 12) {
			top = Math.max(12, window.innerHeight - panelMax - 12);
		}
		top = Math.max(12, top);
		modal.classList.add('is-anchored');
		modal.style.setProperty('--epc-vc-modal-anchor-top', top + 'px');
	}
	function lockPageScroll() {
		savedScrollY = window.scrollY || window.pageYOffset || 0;
		document.body.classList.add('epc-vc-modal-open');
		document.body.style.position = 'fixed';
		document.body.style.top = '-' + savedScrollY + 'px';
		document.body.style.left = '0';
		document.body.style.right = '0';
		document.body.style.width = '100%';
	}
	function unlockPageScroll() {
		document.body.classList.remove('epc-vc-modal-open');
		document.body.style.position = '';
		document.body.style.top = '';
		document.body.style.left = '';
		document.body.style.right = '';
		document.body.style.width = '';
		window.scrollTo(0, savedScrollY);
	}
	function openModal(anchorEl) {
		lockPageScroll();
		modal.classList.add('is-open');
		modal.setAttribute('aria-hidden', 'false');
		positionModal(anchorEl);
		requestAnimationFrame(function () {
			if (modalPanel) {
				modalPanel.focus({ preventScroll: true });
			}
		});
	}
	function closeModal() {
		modal.classList.remove('is-open', 'is-anchored');
		modal.setAttribute('aria-hidden', 'true');
		modal.style.removeProperty('--epc-vc-modal-anchor-top');
		modal.scrollTop = 0;
		unlockPageScroll();
	}
	function openArticleDetail(preview, anchorEl) {
		var targets = openInlineDetail(anchorEl);
		var artId = preview.ART_ID;
		var brandLabel = preview.SUP_BRAND || preview.BRAND || '';
		var articleLabel = preview.ART_ARTICLE_NR || preview.ARTICLE || '';
		if (targets.title) {
			targets.title.textContent = brandLabel + ' ' + articleLabel;
		}
		if (targets.body) {
			targets.body.innerHTML = '<div class="epc-vc-loader">Loading part details and measurements…</div>';
		}
		if (!artId) {
			renderArticleDetail(preview, preview, targets.body, targets.title);
			return;
		}
		api('article', { id: artId }).then(function (detail) {
			renderArticleDetail(detail, preview, targets.body, targets.title);
		}).catch(function () {
			renderArticleDetail(preview, preview, targets.body, targets.title);
		});
	}
	function resetVehicleSelectors(keepMake) {
		if (!keepMake) {
			makeSelect.innerHTML = '<option value="">Make</option>';
			state.manufacturer = null;
		}
		makeSelect.disabled = false;
		modelSelect.innerHTML = '<option value="">Model</option>';
		modelSelect.disabled = true;
		engineSelect.innerHTML = '<option value="">Engine / modification</option>';
		engineSelect.disabled = true;
		state.model = null;
		state.modification = null;
		resetWorkspace();
		setStepState(keepMake && state.manufacturer ? 2 : 1);
	}
	function loadManufacturers() {
		resetVehicleSelectors(false);
		api('manufacturers', {}).then(function (items) {
			state.manufacturers = apiItems(items);
			state.manufacturers.sort(function (a, b) {
				return text(a.MANUFACTURER).localeCompare(text(b.MANUFACTURER));
			});
			fillSelect(makeSelect, 'Make', state.manufacturers, function (row) {
				return row.MANUFACTURER || 'Brand';
			}, false);
			setStepState(1);
		}).catch(function () {
			makeSelect.innerHTML = '<option value="">Could not load makes</option>';
		});
	}
	function onYearChange() {
		engineSelect.innerHTML = '<option value="">Engine / modification</option>';
		engineSelect.disabled = true;
		modelSelect.innerHTML = '<option value="">Model</option>';
		modelSelect.disabled = true;
		state.model = null;
		state.modification = null;
		resetWorkspace();
		makeSelect.disabled = false;
		setStepState(1);
		if (makeSelect.value !== '') {
			onMakeChange();
		}
	}
	function onMakeChange() {
		var idx = makeSelect.value;
		if (idx === '') {
			resetVehicleSelectors(true);
			return;
		}
		state.manufacturer = state.manufacturers[parseInt(idx, 10)];
		state.vehicleType = chooseVehicleType(state.manufacturer);
		modelSelect.disabled = true;
		engineSelect.disabled = true;
		state.model = null;
		state.modification = null;
		resetWorkspace();
		setStepState(2);
		api('models', { MFA_ID: state.manufacturer.MFA_ID }).then(function (items) {
			state.models = filterByYear(apiItems(items), selectedYear());
			fillSelect(modelSelect, 'Model', state.models, function (row) {
				var years = cleanDate(row.CI_FROM) + (row.CI_TO ? ' - ' + cleanDate(row.CI_TO) : '');
				return (row.MODEL_SERIES || 'Model') + (years ? ' (' + years + ')' : '');
			}, false);
			setStepState(2);
		}).catch(function () {
			modelSelect.innerHTML = '<option value="">Could not load models</option>';
		});
	}
	function onModelChange() {
		var idx = modelSelect.value;
		if (idx === '') {
			engineSelect.innerHTML = '<option value="">Engine / modification</option>';
			engineSelect.disabled = true;
			state.model = null;
			state.modification = null;
			resetWorkspace();
			setStepState(2);
			return;
		}
		state.model = state.models[parseInt(idx, 10)];
		engineSelect.disabled = true;
		state.modification = null;
		resetWorkspace();
		setStepState(3);
		api('modifications', { MS_ID: state.model.MS_ID }).then(function (items) {
			state.modifications = filterByYear(apiItems(items), selectedYear());
			fillSelect(engineSelect, 'Engine / modification', state.modifications, function (row) {
				var meta = [
					cleanDate(row.CI_FROM) + (row.CI_TO ? ' - ' + cleanDate(row.CI_TO) : ''),
					row.POWER_KW ? row.POWER_KW + ' kW' : '',
					row.FUEL_TYPE || ''
				].filter(Boolean).join(' | ');
				return getModificationTitle(row) + (meta ? ' — ' + meta : '');
			}, false);
			setStepState(3);
		}).catch(function () {
			engineSelect.innerHTML = '<option value="">Could not load engines</option>';
		});
	}

	function normalizeVin(value) {
		return String(value || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
	}
	function normalizeEngineCode(value) {
		return normalizeVin(value);
	}
	function isLikelyVin(value) {
		var v = normalizeVin(value);
		return v.length >= 11 && v.length <= 17;
	}
	function isLikelyEngineCode(value) {
		var code = normalizeEngineCode(value);
		if (!code || code.length < 2 || code.length > 12) { return false; }
		if (isLikelyVin(code)) { return false; }
		return /^[A-Z0-9]+$/.test(code);
	}
	function engineLabel(row) {
		var code = row.ENGINE_CODE || row.ENG_CODE || 'Engine';
		var meta = [
			row.MANUFACTURER,
			formatYearRange(row),
			row.CAPACITY_CCM_START ? row.CAPACITY_CCM_START + ' ccm' : '',
			row.POWER_PS_START ? row.POWER_PS_START + ' hp' : (row.POWER_PS ? row.POWER_PS + ' hp' : ''),
			row.FUEL_TYPE || ''
		].filter(Boolean).join(' · ');
		return code + (meta ? ' — ' + meta : '');
	}
	function vinArray(node) {
		if (!node) { return []; }
		if (Array.isArray(node)) { return node; }
		if (node.array && Array.isArray(node.array)) { return node.array; }
		return [];
	}
	function mapVinSection(vehicle) {
		var type = String((vehicle && (vehicle.linkageTargetType || vehicle.subLinkageTargetType)) || 'P').toUpperCase();
		if (type === 'V' || type === 'CV' || type === 'C') { return 'commercial'; }
		if (type === 'M' || type === 'MTB' || type === 'B') { return 'motorbike'; }
		return 'passenger';
	}
	function resetPromo() {
		if (!promoBody) { return; }
		promoBody.innerHTML = promoDefaultHtml;
	}
	function showPromoVinMessage(message, isError) {
		if (!promoBody) { return; }
		promoBody.innerHTML =
			'<span class="epc-vc-promo-vin-badge">VIN search</span>' +
			'<h3 class="epc-vc-promo-vin-title">' + (isError ? 'Could not decode VIN' : 'VIN search') + '</h3>' +
			'<p class="epc-vc-promo-vin-meta">' + esc(message) + '</p>' +
			'<button type="button" class="btn btn-default btn-xs epc-vc-promo-vin-reset">Back</button>';
		var resetBtn = promoBody.querySelector('.epc-vc-promo-vin-reset');
		if (resetBtn) { resetBtn.onclick = resetPromo; }
	}
	function showPromoVinLoading(vin) {
		if (!promoBody) { return; }
		promoBody.innerHTML =
			'<span class="epc-vc-promo-vin-badge">VIN search</span>' +
			'<h3 class="epc-vc-promo-vin-title">Decoding ' + esc(vin) + '</h3>' +
			'<p class="epc-vc-promo-vin-meta">Looking up manufacturer, model and engine in the eparts catalog…</p>' +
			'<div class="epc-vc-promo-vin-msg">Please wait</div>';
	}
	function showPromoVinIdentified(vehicle, manufacturers, models, vin) {
		if (!promoBody) { return; }
		var manu = manufacturers.find(function (item) { return String(item.manuId) === String(vehicle.manuId); }) || {};
		var model = models.find(function (item) { return String(item.modelId) === String(vehicle.modelId); }) || {};
		var title = vehicle.carName || [manu.manuName, model.modelName, vehicle.vehicleTypeDescription].filter(Boolean).join(' ');
		promoBody.innerHTML =
			'<span class="epc-vc-promo-vin-badge">VIN matched</span>' +
			'<h3 class="epc-vc-promo-vin-title">' + esc(title || 'Vehicle found') + '</h3>' +
			'<p class="epc-vc-promo-vin-meta">VIN <strong>' + esc(vin) + '</strong> — loading catalog categories below.</p>' +
			'<button type="button" class="btn btn-default btn-xs epc-vc-promo-vin-reset">Search another VIN</button>';
		var resetBtn = promoBody.querySelector('.epc-vc-promo-vin-reset');
		if (resetBtn) {
			resetBtn.onclick = function () {
				resetPromo();
				if (vinInput) { vinInput.value = ''; }
			};
		}
	}
	function renderPromoVinPicker(vehicles, manufacturers, models, vin) {
		if (!promoBody) { return; }
		promoBody.innerHTML =
			'<span class="epc-vc-promo-vin-badge">VIN search</span>' +
			'<h3 class="epc-vc-promo-vin-title">Select your vehicle</h3>' +
			'<p class="epc-vc-promo-vin-meta">VIN <strong>' + esc(vin) + '</strong> matches more than one configuration. Choose the correct one:</p>' +
			'<div class="epc-vc-promo-vin-list">' + vehicles.map(function (vehicle, index) {
				var label = vehicle.carName || vehicle.vehicleTypeDescription || ('Vehicle ' + (index + 1));
				var meta = [manufacturers.find(function (m) { return String(m.manuId) === String(vehicle.manuId); }).manuName, models.find(function (m) { return String(m.modelId) === String(vehicle.modelId); }).modelName].filter(Boolean).join(' · ');
				return '<button type="button" class="epc-vc-promo-vin-btn" data-vin-index="' + index + '"><strong>' + esc(label) + '</strong><span>' + esc(meta) + '</span></button>';
			}).join('') + '</div>' +
			'<button type="button" class="btn btn-default btn-xs epc-vc-promo-vin-reset">Cancel</button>';
		Array.prototype.forEach.call(promoBody.querySelectorAll('.epc-vc-promo-vin-btn'), function (btn) {
			btn.onclick = function () {
				applyVinVehicle(vehicles[parseInt(btn.getAttribute('data-vin-index'), 10)], manufacturers, models, vin);
			};
		});
		var resetBtn = promoBody.querySelector('.epc-vc-promo-vin-reset');
		if (resetBtn) { resetBtn.onclick = resetPromo; }
	}
	function syncSectionTabs() {
		Array.prototype.forEach.call(document.querySelectorAll('.epc-vc-tab'), function (tab) {
			tab.classList.toggle('active', tab.getAttribute('data-section') === state.section);
		});
	}
	function applyVinVehicle(vehicle, manufacturers, models, vin) {
		var manu = manufacturers.find(function (item) { return String(item.manuId) === String(vehicle.manuId); }) || {};
		state.section = mapVinSection(vehicle);
		syncSectionTabs();
		showPromoVinIdentified(vehicle, manufacturers, models, vin);
		closeInlineDetail();
		api('manufacturers', {}).then(function (items) {
			state.manufacturers = apiItems(items);
			state.manufacturers.sort(function (a, b) {
				return text(a.MANUFACTURER).localeCompare(text(b.MANUFACTURER));
			});
			var makeIdx = state.manufacturers.findIndex(function (row) {
				return String(row.MFA_ID) === String(vehicle.manuId);
			});
			if (makeIdx < 0) {
				throw new Error('Manufacturer not found');
			}
			state.manufacturer = state.manufacturers[makeIdx];
			state.vehicleType = chooseVehicleType(state.manufacturer) || (state.section === 'commercial' ? 'CV' : (state.section === 'motorbike' ? 'Motorcycle' : 'PC'));
			fillSelect(makeSelect, 'Make', state.manufacturers, function (row) {
				return row.MANUFACTURER || 'Brand';
			}, false);
			makeSelect.value = String(makeIdx);
			setStepState(2);
			return api('models', { MFA_ID: state.manufacturer.MFA_ID });
		}).then(function (items) {
			state.models = filterByYear(apiItems(items), selectedYear());
			var modelIdx = state.models.findIndex(function (row) {
				return String(row.MS_ID) === String(vehicle.modelId);
			});
			if (modelIdx < 0) {
				throw new Error('Model not found');
			}
			state.model = state.models[modelIdx];
			fillSelect(modelSelect, 'Model', state.models, function (row) {
				var years = cleanDate(row.CI_FROM) + (row.CI_TO ? ' - ' + cleanDate(row.CI_TO) : '');
				return (row.MODEL_SERIES || 'Model') + (years ? ' (' + years + ')' : '');
			}, false);
			modelSelect.value = String(modelIdx);
			setStepState(3);
			return api('modifications', { MS_ID: state.model.MS_ID });
		}).then(function (items) {
			state.modifications = filterByYear(apiItems(items), selectedYear());
			var modIdx = state.modifications.findIndex(function (row) {
				return String(getModificationId(row)) === String(vehicle.carId);
			});
			if (modIdx < 0) {
				state.modifications.unshift({
					PC_ID: vehicle.carId,
					PASSENGER_CAR: vehicle.vehicleTypeDescription || vehicle.carName || 'Vehicle',
					MS_ID: vehicle.modelId,
					MFA_ID: vehicle.manuId,
					TYPE: state.vehicleType
				});
				modIdx = 0;
			}
			fillSelect(engineSelect, 'Engine / modification', state.modifications, function (row) {
				var meta = [
					cleanDate(row.CI_FROM) + (row.CI_TO ? ' - ' + cleanDate(row.CI_TO) : ''),
					row.POWER_KW ? row.POWER_KW + ' kW' : '',
					row.FUEL_TYPE || ''
				].filter(Boolean).join(' | ');
				return getModificationTitle(row) + (meta ? ' — ' + meta : '');
			}, false);
			engineSelect.value = String(modIdx);
			setStepState(4);
			loadCategories();
			if (workspace) {
				workspace.scrollIntoView({ behavior: 'smooth', block: 'start' });
			}
		}).catch(function () {
			showPromoVinMessage('Vehicle was decoded but catalog data could not be loaded. Try the manual picker on the left.', true);
		});
	}
	function showPromoEngineMessage(message, isError) {
		if (!promoBody) { return; }
		promoBody.innerHTML =
			'<span class="epc-vc-promo-vin-badge">Engine code</span>' +
			'<h3 class="epc-vc-promo-vin-title">' + (isError ? 'Engine not found' : 'Engine search') + '</h3>' +
			'<p class="epc-vc-promo-vin-meta">' + esc(message) + '</p>' +
			'<button type="button" class="btn btn-default btn-xs epc-vc-promo-vin-reset">Back</button>';
		var resetBtn = promoBody.querySelector('.epc-vc-promo-vin-reset');
		if (resetBtn) { resetBtn.onclick = resetPromo; }
	}
	function showPromoEngineLoading(code) {
		if (!promoBody) { return; }
		promoBody.innerHTML =
			'<span class="epc-vc-promo-vin-badge">Engine code</span>' +
			'<h3 class="epc-vc-promo-vin-title">Searching ' + esc(code) + '</h3>' +
			'<p class="epc-vc-promo-vin-meta">Looking up engine codes in the eparts catalog (Toyota 3L, 12R, 5L, etc.)…</p>' +
			'<div class="epc-vc-promo-vin-msg">Please wait</div>';
	}
	function applyEngineCatalog(engine, code) {
		state.engineCatalogMode = true;
		state.manufacturer = null;
		state.model = null;
		state.modification = engine;
		state.vehicleType = 'Engine';
		if (vinInput) { vinInput.value = code || (engine.ENGINE_CODE || engine.ENG_CODE || ''); }
		if (promoBody) {
			promoBody.innerHTML =
				'<span class="epc-vc-promo-vin-badge">Engine matched</span>' +
				'<h3 class="epc-vc-promo-vin-title">' + esc(engineLabel(engine)) + '</h3>' +
				'<p class="epc-vc-promo-vin-meta">Loading OEM-style parts catalog for this engine below.</p>' +
				'<button type="button" class="btn btn-default btn-xs epc-vc-promo-vin-reset">Search another engine</button>';
		}
		var resetBtn = promoBody ? promoBody.querySelector('.epc-vc-promo-vin-reset') : null;
		if (resetBtn) {
			resetBtn.textContent = 'Search another engine';
			resetBtn.onclick = function () {
				state.engineCatalogMode = false;
				state.vehicleType = '';
				state.modification = null;
				resetWorkspace();
				resetPromo();
				if (vinInput) { vinInput.value = ''; }
			};
		}
		loadCategories();
		if (workspace) {
			workspace.scrollIntoView({ behavior: 'smooth', block: 'start' });
		}
	}
	function renderPromoEnginePicker(engines, code) {
		if (!promoBody) { return; }
		promoBody.innerHTML =
			'<span class="epc-vc-promo-vin-badge">Engine code</span>' +
			'<h3 class="epc-vc-promo-vin-title">Select engine</h3>' +
			'<p class="epc-vc-promo-vin-meta">Code <strong>' + esc(code) + '</strong> matches more than one engine. Choose the correct one:</p>' +
			'<div class="epc-vc-promo-vin-list">' + engines.map(function (engine, index) {
				return '<button type="button" class="epc-vc-promo-vin-btn" data-engine-index="' + index + '"><strong>' + esc(engineLabel(engine)) + '</strong><span>' + esc(engine.MANUFACTURER || '') + '</span></button>';
			}).join('') + '</div>' +
			'<button type="button" class="btn btn-default btn-xs epc-vc-promo-vin-reset">Cancel</button>';
		Array.prototype.forEach.call(promoBody.querySelectorAll('.epc-vc-promo-vin-btn'), function (btn) {
			btn.onclick = function () {
				applyEngineCatalog(engines[parseInt(btn.getAttribute('data-engine-index'), 10)], code);
			};
		});
		var resetBtn = promoBody.querySelector('.epc-vc-promo-vin-reset');
		if (resetBtn) { resetBtn.onclick = resetPromo; }
	}
	function engineSearch() {
		var code = normalizeEngineCode(vinInput ? vinInput.value : '');
		if (!isLikelyEngineCode(code)) {
			if (vinInput) { vinInput.focus(); }
			showPromoEngineMessage('Enter a valid engine code (2–12 characters, e.g. 3L, 12R, 5L).', true);
			return;
		}
		if (vinInput) { vinInput.value = code; }
		state.engineCatalogMode = false;
		showPromoEngineLoading(code);
		api('engine_search', { code: code }).then(function (payload) {
			var engines = apiItems(payload);
			if (!engines.length) {
				var hint = payload && payload.truncated
					? 'No match in the first pass of brands. Pick make (year → Toyota) and try again, or contact us.'
					: 'No engine found for this code in Epart catalog.';
				showPromoEngineMessage(hint, true);
				return;
			}
			if (engines.length === 1) {
				applyEngineCatalog(engines[0], code);
				return;
			}
			renderPromoEnginePicker(engines, code);
		}).catch(function (err) {
			var msg = err && err.message ? err.message : 'Engine lookup failed. Please try again.';
			showPromoEngineMessage(msg, true);
		});
	}
	function vinOrEngineSearch() {
		var raw = vinInput ? vinInput.value : '';
		if (isLikelyVin(raw)) {
			vinSearch();
			return;
		}
		if (isLikelyEngineCode(raw)) {
			engineSearch();
			return;
		}
		if (vinInput) { vinInput.focus(); }
		showPromoVinMessage('Enter a VIN (11–17 characters) or engine code (e.g. 3L, 12R, 5L).', true);
	}
	function vinSearch() {
		var vin = normalizeVin(vinInput ? vinInput.value : '');
		if (vin.length < 11) {
			if (vinInput) { vinInput.focus(); }
			showPromoVinMessage('Enter a valid VIN (11–17 characters).', true);
			return;
		}
		state.engineCatalogMode = false;
		if (vinInput) { vinInput.value = vin; }
		showPromoVinLoading(vin);
		api('vin', { vin: vin }).then(function (payload) {
			var data = payload && payload.data ? payload.data : payload;
			var vehicles = vinArray(data && data.matchingVehicles);
			var manufacturers = vinArray(data && data.matchingManufacturers);
			var models = vinArray(data && data.matchingModels);
			if (!vehicles.length) {
				showPromoVinMessage('No vehicle found for this VIN in Epart catalog.', true);
				return;
			}
			if (vehicles.length === 1) {
				applyVinVehicle(vehicles[0], manufacturers, models, vin);
				return;
			}
			renderPromoVinPicker(vehicles, manufacturers, models, vin);
		}).catch(function (err) {
			var msg = err && err.message ? err.message : 'VIN lookup failed. Please try again.';
			showPromoVinMessage(msg, true);
		});
	}
	function initVinFromQuery() {
		var params = new URLSearchParams(location.search);
		var engine = normalizeEngineCode(params.get('engine') || '');
		if (engine && isLikelyEngineCode(engine)) {
			if (vinInput) { vinInput.value = engine; }
			engineSearch();
			return;
		}
		var vin = normalizeVin(params.get('vin') || '');
		if (!vin) { return; }
		if (vinInput) { vinInput.value = vin; }
		vinSearch();
	}
	document.getElementById('epc-vc-search-go').onclick = function () {
		var article = (document.getElementById('epc-vc-search-part').value || '').trim();
		var secondary = vinInput ? vinInput.value : '';
		if (article) {
			location.href = shopSearchUrl('', article);
			return;
		}
		if (String(secondary).trim()) {
			vinOrEngineSearch();
		}
	};
	if (vinInput) {
		vinInput.onkeydown = function (event) {
			if (event.key === 'Enter') {
				event.preventDefault();
				vinOrEngineSearch();
			}
		};
	}
	document.getElementById('epc-vc-modal-close').onclick = closeModal;
	modal.onclick = function (e) {
		if (e.target === modal) { closeModal(); }
	};

	yearSelect.onchange = onYearChange;
	makeSelect.onchange = onMakeChange;
	modelSelect.onchange = onModelChange;
	engineSelect.onchange = loadCategories;
	filterInput.oninput = function () {
		applyFilter(sidebarBody);
		applyFilter(mainBody);
	};
	catFilter.oninput = applyCategoryFilter;

	Array.prototype.forEach.call(document.querySelectorAll('.epc-vc-tab'), function (tab) {
		tab.onclick = function () {
			Array.prototype.forEach.call(document.querySelectorAll('.epc-vc-tab'), function (t) { t.classList.remove('active'); });
			tab.classList.add('active');
			state.section = tab.getAttribute('data-section');
			loadManufacturers();
		};
	});

	buildYearOptions();
	loadManufacturers();
	initVinFromQuery();
})();
</script>
