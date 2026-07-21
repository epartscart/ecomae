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

$assetVer = (function_exists('epc_cp_page_asset_version') ? epc_cp_page_asset_version() : '20260721') . 'mvMin1';
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
			<p>Upload one Excel/CSV with many vendors. The system creates a warehouse and price list per vendor automatically. Customers see only the short warehouse name; the full vendor name stays in the control panel.</p>
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
			<span class="epc-multivendor-stat__lbl">Data types</span>
			<span class="epc-multivendor-stat__val">Inv · Sales · Buy</span>
		</div>
	</div>

	<div class="epc-multivendor-rules">
		<article>
			<span class="epc-multivendor-badge">Match key</span>
			<h3>Brand + Article + Vendor</h3>
			<p>Matching uses <strong>data type</strong> + brand + article + vendor full name + vendor short/code.</p>
		</article>
		<article>
			<span class="epc-multivendor-badge epc-multivendor-badge--short">Inventory</span>
			<h3>No repeats</h3>
			<p>Same key stays unique. Quantities are summed into one stock row.</p>
		</article>
		<article>
			<span class="epc-multivendor-badge epc-multivendor-badge--auto">Sales / Purchase</span>
			<h3>Min + max price</h3>
			<p>Repeats keep <strong>lowest</strong> and <strong>highest</strong> prices. Minimum is restricted to selected groups/customers; administrators always see it.</p>
		</article>
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
						<label for="epcMultivendorDataType">Default data type</label>
						<select class="form-control" name="data_type" id="epcMultivendorDataType">
							<option value="inventory" selected>Inventory (unique stock)</option>
							<option value="sales">Sales (keep min + max price)</option>
							<option value="purchase">Purchase (keep min + max price)</option>
						</select>
						<small class="help-block">Used when the file has no <code>Data type</code> column. Per-row column overrides this.</small>
					</div>
					<div class="epc-multivendor-field epc-multivendor-field--wide">
						<label for="epcMultivendorFile">Excel / CSV file</label>
						<div class="epc-multivendor-drop" id="epcMultivendorDrop">
							<input class="form-control" type="file" name="price_file" id="epcMultivendorFile" accept=".csv,.txt,.xls,.xlsx" required />
							<p class="epc-multivendor-drop__hint"><i class="fa fa-paperclip"></i> Choose a file — required columns below</p>
						</div>
						<small class="help-block">Required: Brand, Article, Price, <strong>Vendor full name</strong>, <strong>Vendor short/code</strong>. Optional: Data type, Name, Qty, Delivery.</small>
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
							<td>Backend warehouse name only</td>
						</tr>
						<tr>
							<td><strong>Vendor short / code</strong></td>
							<td><span class="label label-danger">Yes</span></td>
							<td>Vendor short, Vendor code, Warehouse, WH</td>
							<td>Customer-facing warehouse code</td>
						</tr>
						<tr>
							<td><strong>Data type</strong></td>
							<td>No</td>
							<td>Data type, Type, Role, Channel</td>
							<td>inventory / sales / purchase (per row or form default)</td>
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
			<p class="epc-multivendor-muted">Lists: inventory uses <em>S-UAE</em>; sales uses <em>S-UAE · Sales</em>; purchase uses <em>S-UAE · Purchase</em>. Storefront warehouse label stays the short code.</p>
		</div>
	</div>
</div>
</div>
