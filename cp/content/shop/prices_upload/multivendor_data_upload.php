<?php
/**
 * CP page: Multi-vendor Excel → auto-create warehouses + price lists.
 *
 * Eval-safe: no inline <script>/<style> in the main pane (those render as
 * plain text under CP <base href>). Config + JS load via footer page assets;
 * CSRF/URLs also sit on data-* attributes as a fallback.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

$user_session = DP_User::getAdminSession();
$csrf = htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8');
$backend_raw = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($backend_raw === '') {
	$backend_raw = 'cp';
}
$backend = htmlspecialchars($backend_raw, ENT_QUOTES, 'UTF-8');

$epc_mv_ajax = '/' . $backend_raw . '/content/shop/prices_upload/ajax_epc_multivendor_ingest.php';
$epc_mv_sample = '/' . $backend_raw . '/content/shop/prices_upload/epc_multivendor_sample_file.php';
$epc_mv_prices = '/' . $backend_raw . '/shop/prices';
$epc_mv_storages = '/' . $backend_raw . '/shop/logistics/storages';
$epc_mv_guide = '/' . $backend_raw . '/shop/prices/guide';

$assetVer = (function_exists('epc_cp_page_asset_version') ? epc_cp_page_asset_version() : '20260721') . 'mvCombine1';
epc_cp_register_page_assets(
	array('/content/general_pages/epc_prices_cp_css.php?v=' . rawurlencode($assetVer)),
	array(
		'/' . $backend_raw . '/content/shop/prices_upload/epc_multivendor_cp_config.php?v=' . rawurlencode($assetVer),
		'/' . $backend_raw . '/content/shop/prices_upload/epc_multivendor_cp.js?v=' . rawurlencode($assetVer),
	)
);
?>
<div class="col-lg-12 epc-cp-page-frame">
<div
	id="epcMultivendorRoot"
	class="epc-multivendor-page"
	data-ajax-url="<?php echo htmlspecialchars($epc_mv_ajax, ENT_QUOTES, 'UTF-8'); ?>"
	data-sample-url="<?php echo htmlspecialchars($epc_mv_sample, ENT_QUOTES, 'UTF-8'); ?>"
	data-csrf-key="<?php echo $csrf; ?>"
	data-backend="<?php echo $backend; ?>"
	data-prices-url="<?php echo htmlspecialchars($epc_mv_prices, ENT_QUOTES, 'UTF-8'); ?>"
	data-storages-url="<?php echo htmlspecialchars($epc_mv_storages, ENT_QUOTES, 'UTF-8'); ?>"
>
	<div class="epc-multivendor-hero">
		<div class="epc-multivendor-hero__visual" aria-hidden="true">
			<span class="epc-multivendor-hero__icon"><i class="fa fa-handshake-o"></i></span>
		</div>
		<div class="epc-multivendor-hero__text">
			<p class="epc-multivendor-kicker">Shop · Price lists · Multivendor</p>
			<h2>Multi-vendor price upload</h2>
			<p>Upload <strong>one Excel/CSV</strong> with many vendors and mixed data types (inventory, sales, purchase). Each row’s <code>Data type</code> column routes the line — no need to upload three times. Warehouses are created automatically; customers see only the short code.</p>
		</div>
		<div class="epc-multivendor-hero__actions">
			<a class="btn btn-default" href="<?php echo htmlspecialchars($epc_mv_prices, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-list"></i> Price lists</a>
			<a class="btn btn-default" href="<?php echo htmlspecialchars($epc_mv_storages, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-building"></i> Warehouses</a>
			<a class="btn btn-default" href="<?php echo htmlspecialchars($epc_mv_guide, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-book"></i> Upload guide</a>
		</div>
	</div>

	<div class="epc-multivendor-stats">
		<div class="epc-multivendor-stat">
			<span class="epc-multivendor-stat__icon"><i class="fa fa-file-excel-o"></i></span>
			<span class="epc-multivendor-stat__lbl">One file</span>
			<span class="epc-multivendor-stat__val">Many vendors</span>
		</div>
		<div class="epc-multivendor-stat">
			<span class="epc-multivendor-stat__icon"><i class="fa fa-building"></i></span>
			<span class="epc-multivendor-stat__lbl">Auto warehouses</span>
			<span class="epc-multivendor-stat__val">Short + full</span>
		</div>
		<div class="epc-multivendor-stat">
			<span class="epc-multivendor-stat__icon"><i class="fa fa-tags"></i></span>
			<span class="epc-multivendor-stat__lbl">Combine</span>
			<span class="epc-multivendor-stat__val">Inv · Sales · Buy</span>
		</div>
	</div>

	<div class="epc-multivendor-rules">
		<article>
			<span class="epc-multivendor-badge">Match key</span>
			<h3>Brand + Article + Vendor name + Code</h3>
			<p>Matching uses <strong>data type</strong> + brand + article + <strong>vendor full name</strong> + <strong>vendor code</strong>. Same code with a different name is treated as a separate vendor for min/max.</p>
		</article>
		<article>
			<span class="epc-multivendor-badge epc-multivendor-badge--short">Inventory</span>
			<h3>No repeats</h3>
			<p>Same key stays unique. Quantities are summed into one stock row.</p>
		</article>
		<article>
			<span class="epc-multivendor-badge epc-multivendor-badge--auto">Sales / Purchase</span>
			<h3>Min + max price</h3>
			<p>Repeats keep <strong>lowest</strong> and <strong>highest</strong> prices within the same vendor name + code. <strong>QTY is combined</strong> as the total across all source rows and applied to both min and max. Customers see the code; CP shows the real vendor name.</p>
		</article>
	</div>

	<div class="hpanel epc-multivendor-panel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-barcode"></i> Vendor codes
			<button type="button" class="btn btn-default btn-xs pull-right" id="epcMvVendorCodesRefreshBtn" style="margin-top:-2px;">
				<i class="fa fa-refresh"></i> Refresh
			</button>
		</div>
		<div class="panel-body">
			<p class="epc-multivendor-muted" style="margin-top:0;">
				Customers always see the <strong>vendor code</strong> on the storefront.
				In CP you see the <strong>actual vendor name</strong>.
				Change a code anytime — the storefront label updates for that warehouse. Name + code together identify the vendor for min/max pricing.
			</p>
			<div class="table-responsive">
				<table class="table table-striped table-bordered table-condensed" id="epcMvVendorCodesTable">
					<thead>
						<tr>
							<th style="width:70px;">ID</th>
							<th>Vendor name (CP)</th>
							<th style="width:180px;">Vendor code (storefront)</th>
							<th style="width:90px;">Price list</th>
							<th style="width:110px;"></th>
						</tr>
					</thead>
					<tbody id="epcMvVendorCodesBody">
						<tr><td colspan="5" class="text-muted">Loading vendor codes…</td></tr>
					</tbody>
				</table>
			</div>
			<div id="epcMvVendorCodesResult" class="epc-multivendor-result" aria-live="polite"></div>
		</div>
	</div>

	<div class="hpanel epc-multivendor-panel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-lock"></i> Sales / Purchase minimum price access
		</div>
		<div class="panel-body">
			<p class="epc-multivendor-muted" style="margin-top:0;">
				For <strong>Sales</strong> and <strong>Purchase</strong> uploads, the system keeps the lowest and highest price.
				The <strong>minimum</strong> offer is hidden from general customers. Choose which customer groups or specific customers may see it.
				<strong>Administrators always see minimum prices.</strong> Re-upload after changing rules so new files are tagged correctly; existing tagged rows apply immediately.
			</p>
			<form id="epcMultivendorMinAclForm" onsubmit="return false;">
				<div class="epc-multivendor-acl">
					<label class="epc-multivendor-acl__toggle">
						<input type="checkbox" id="epcMvMinRestrict" checked />
						<span>Restrict sales/purchase minimum prices (recommended)</span>
					</label>
					<div class="epc-multivendor-form-grid">
						<div class="epc-multivendor-field epc-multivendor-field--wide">
							<label for="epcMvMinGroups">Allowed customer groups</label>
							<select class="form-control" id="epcMvMinGroups" multiple size="6"></select>
							<small class="help-block">Hold Ctrl/Cmd to select multiple groups. Leave empty for administrators only.</small>
						</div>
						<div class="epc-multivendor-field epc-multivendor-field--wide">
							<label for="epcMvMinUsers">Allowed customer IDs</label>
							<input class="form-control" type="text" id="epcMvMinUsers" placeholder="e.g. 12, 45, 108" autocomplete="off" />
							<small class="help-block">Comma-separated storefront user IDs that may see minimum prices.</small>
						</div>
					</div>
					<div class="epc-multivendor-form-actions">
						<button type="button" class="btn btn-primary" id="epcMvMinAclSaveBtn">
							<i class="fa fa-save"></i> Save minimum price access
						</button>
					</div>
					<div id="epcMvMinAclResult" class="epc-multivendor-result" aria-live="polite"></div>
				</div>
			</form>
		</div>
	</div>

	<div class="hpanel epc-multivendor-panel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-cloud-upload"></i> Upload multi-vendor Excel / CSV
		</div>
		<div class="panel-body">
			<form id="epcMultivendorIngestForm" enctype="multipart/form-data" onsubmit="return false;">
				<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
				<div class="epc-multivendor-form-grid">
					<div class="epc-multivendor-field">
						<label for="epcMultivendorDataType">Data type mode</label>
						<select class="form-control" name="data_type" id="epcMultivendorDataType">
							<option value="combine" selected>Combine (one file — inventory + sales + purchase)</option>
							<option value="inventory">Inventory only (unique stock)</option>
							<option value="sales">Sales only (min + max price, total QTY)</option>
							<option value="purchase">Purchase only (min + max price, total QTY)</option>
						</select>
						<small class="help-block"><strong>Combine</strong> (default): put <code>inventory</code>, <code>sales</code>, or <code>purchase</code> in each row’s Data type column. Single-type options apply that type to the whole file when the column is missing.</small>
					</div>
					<div class="epc-multivendor-field epc-multivendor-field--wide">
						<label for="epcMultivendorFile">Excel / CSV file</label>
						<div class="epc-multivendor-drop" id="epcMultivendorDrop">
							<input class="form-control" type="file" name="price_file" id="epcMultivendorFile" accept=".csv,.txt,.xls,.xlsx" required />
							<p class="epc-multivendor-drop__hint"><i class="fa fa-paperclip"></i> Choose a file — required columns below</p>
						</div>
						<small class="help-block">Required: Brand, Article, Price, <strong>Vendor full name</strong>, <strong>Vendor short/code</strong>, and <strong>Data type</strong> (for Combine). Optional: Name, Qty, Delivery.</small>
					</div>
				</div>
				<div class="epc-multivendor-form-actions">
					<button type="button" class="btn btn-primary btn-lg epc-multivendor-btn-primary" id="epcMultivendorUploadBtn">
						<i class="fa fa-cloud-upload"></i> Upload &amp; create warehouses
					</button>
					<a class="btn btn-default" id="epcMultivendorSampleBtn"
						href="<?php echo htmlspecialchars($epc_mv_sample, ENT_QUOTES, 'UTF-8'); ?>"
						download="epc-multivendor-sample.csv"
						target="_blank" rel="noopener">
						<i class="fa fa-download"></i> Download sample CSV
					</a>
				</div>
			</form>
			<div id="epcMultivendorResult" class="epc-multivendor-result" aria-live="polite"></div>
		</div>
	</div>

	<div class="hpanel epc-multivendor-panel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-table"></i> Expected columns
		</div>
		<div class="panel-body">
			<div class="table-responsive">
				<table class="table table-striped table-bordered epc-multivendor-cols">
					<thead>
						<tr>
							<th>Column</th>
							<th>Required</th>
							<th>Accepted header names</th>
							<th>Purpose</th>
						</tr>
					</thead>
					<tbody>
						<tr>
							<td><strong>Brand</strong></td>
							<td>Recommended</td>
							<td>Brand, Manufacturer, Mfr</td>
							<td>Part manufacturer</td>
						</tr>
						<tr>
							<td><strong>Article</strong></td>
							<td><span class="label label-danger">Yes</span></td>
							<td>Article, SKU, Number, OEM</td>
							<td>Part number</td>
						</tr>
						<tr>
							<td><strong>Name</strong></td>
							<td>No</td>
							<td>Name, Description</td>
							<td>Product title</td>
						</tr>
						<tr>
							<td><strong>Qty</strong></td>
							<td>Recommended</td>
							<td>Qty, Stock, Exist</td>
							<td>Availability</td>
						</tr>
						<tr>
							<td><strong>Price</strong></td>
							<td><span class="label label-danger">Yes</span></td>
							<td>Price, Sales price</td>
							<td>Shelf / offer price</td>
						</tr>
						<tr>
							<td><strong>Vendor full name</strong></td>
							<td><span class="label label-danger">Yes</span></td>
							<td>Vendor full name, Supplier full, Company name</td>
							<td>Backend warehouse name (actual vendor in CP)</td>
						</tr>
						<tr>
							<td><strong>Vendor short / code</strong></td>
							<td><span class="label label-danger">Yes</span></td>
							<td>Vendor short, Vendor code, Warehouse, WH</td>
							<td>Customer-facing warehouse code (same code + different name = separate vendor)</td>
						</tr>
						<tr>
							<td><strong>Data type</strong></td>
							<td><span class="label label-danger">Yes</span> (Combine)</td>
							<td>Data type, Type, Role, Channel</td>
							<td>Per row: inventory / sales / purchase — one file loads all three</td>
						</tr>
						<tr>
							<td><strong>Delivery</strong></td>
							<td>No</td>
							<td>Delivery, Days, Term</td>
							<td>Lead time (0 = in warehouse)</td>
						</tr>
					</tbody>
				</table>
			</div>
			<p class="epc-multivendor-muted">Lists are unique per vendor name + code (e.g. <em>S-UAE · S-UAE Trading LLC</em>). Storefront warehouse label stays the short code only.</p>
		</div>
	</div>
</div>
</div>
