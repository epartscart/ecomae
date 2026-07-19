<?php
/**
 * CP page: Multi-vendor Excel → auto-create warehouses + price lists.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

$user_session = DP_User::getAdminSession();
$csrf = htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8');
$backend_raw = trim((string) $DP_Config->backend_dir, '/');
if ($backend_raw === '') {
	$backend_raw = 'cp';
}
$backend = htmlspecialchars($backend_raw, ENT_QUOTES, 'UTF-8');
// Boot JSON (not a <script>) survives CP script relocation; JS merges it into EPC_MULTIVENDOR_CP.
$epc_mv_sample = '/' . $backend_raw . '/content/shop/prices_upload/epc_multivendor_sample_file.php';
$epc_mv_inline = array(
	'ajaxUrl' => '/' . $backend_raw . '/content/shop/prices_upload/ajax_epc_multivendor_ingest.php',
	'sampleUrl' => $epc_mv_sample,
	'csrfKey' => (string) ($user_session['csrf_guard_key'] ?? ''),
	'backend' => $backend_raw,
	'pricesUrl' => '/' . $backend_raw . '/shop/prices',
	'storagesUrl' => '/' . $backend_raw . '/shop/logistics/storages',
);
$epc_mv_boot_json = json_encode($epc_mv_inline, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
?>
<div id="epc-multivendor-boot" style="display:none"><?php echo htmlspecialchars((string) $epc_mv_boot_json, ENT_QUOTES, 'UTF-8'); ?></div>
<script>
window.EPC_MULTIVENDOR_CP = Object.assign({}, window.EPC_MULTIVENDOR_CP || {}, <?php echo $epc_mv_boot_json; ?>);
</script>
<div class="epc-multivendor-page">
	<div class="epc-multivendor-hero">
		<div class="epc-multivendor-hero__text">
			<p class="epc-multivendor-kicker">Shop · Price lists</p>
			<h2>Multi-vendor price upload</h2>
			<p>Upload one Excel/CSV with many vendors. The system creates a warehouse and price list per vendor automatically. Customers see only the short warehouse name; the full vendor name stays in the control panel.</p>
		</div>
		<div class="epc-multivendor-hero__actions">
			<a class="btn btn-default" href="/<?php echo $backend; ?>/shop/prices"><i class="fas fa-list"></i> Price lists</a>
			<a class="btn btn-default" href="/<?php echo $backend; ?>/shop/logistics/storages"><i class="fas fa-warehouse"></i> Warehouses</a>
			<a class="btn btn-default" href="/<?php echo $backend; ?>/shop/prices/guide"><i class="fas fa-book"></i> Upload guide</a>
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
			<h3>Min + max price only</h3>
			<p>If the same key repeats with different prices, only the <strong>lowest</strong> and <strong>highest</strong> prices are kept.</p>
		</article>
	</div>

	<div class="hpanel epc-multivendor-panel">
		<div class="panel-heading hbuilt">
			<i class="fas fa-upload"></i> Upload multi-vendor Excel / CSV
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
						<input class="form-control" type="file" name="price_file" id="epcMultivendorFile" accept=".csv,.txt,.xls,.xlsx" required />
						<small class="help-block">Required: Brand, Article, Price, <strong>Vendor full name</strong>, <strong>Vendor short/code</strong>. Optional: Data type, Name, Qty, Delivery.</small>
					</div>
				</div>
				<div class="epc-multivendor-form-actions">
					<button type="button" class="btn btn-primary btn-lg" id="epcMultivendorUploadBtn">
						<i class="fas fa-cloud-upload-alt"></i> Upload &amp; create warehouses
					</button>
					<a class="btn btn-default" id="epcMultivendorSampleBtn"
						href="<?php echo htmlspecialchars($epc_mv_sample, ENT_QUOTES, 'UTF-8'); ?>"
						download="epc-multivendor-sample.csv"
						target="_blank" rel="noopener">
						<i class="fas fa-download"></i> Download sample CSV
					</a>
				</div>
			</form>
			<div id="epcMultivendorResult" class="epc-multivendor-result" aria-live="polite"></div>
		</div>
	</div>

	<div class="hpanel epc-multivendor-panel">
		<div class="panel-heading hbuilt">
			<i class="fas fa-table"></i> Expected columns
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
							<td>Yes</td>
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
							<td>Yes</td>
							<td>Price, Sales price</td>
							<td>Shelf / offer price</td>
						</tr>
						<tr>
							<td><strong>Vendor full name</strong></td>
							<td>Yes</td>
							<td>Vendor full name, Supplier full, Company name</td>
							<td>Backend warehouse name only</td>
						</tr>
						<tr>
							<td><strong>Vendor short / code</strong></td>
							<td>Yes</td>
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
