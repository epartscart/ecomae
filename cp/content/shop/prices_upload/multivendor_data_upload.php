<?php
/**
 * CP page: Multi-vendor Excel → auto-create warehouses + price lists.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

$user_session = DP_User::getAdminSession();
$csrf = htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8');
$backend = htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8');
?>
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
			<span class="epc-multivendor-badge">Backend</span>
			<h3>Vendor full name</h3>
			<p>Stored as warehouse <code>name</code> for CP / logistics only. Never shown to storefront customers.</p>
		</article>
		<article>
			<span class="epc-multivendor-badge epc-multivendor-badge--short">Storefront</span>
			<h3>Vendor short name</h3>
			<p>Stored as warehouse <code>short_name</code>. This is the warehouse label customers see in search results.</p>
		</article>
		<article>
			<span class="epc-multivendor-badge epc-multivendor-badge--auto">Auto</span>
			<h3>One warehouse per short name</h3>
			<p>Rows with the same short name go into one price list. Re-upload replaces that vendor’s stock/prices.</p>
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
					<div class="epc-multivendor-field epc-multivendor-field--wide">
						<label for="epcMultivendorFile">Excel / CSV file</label>
						<input class="form-control" type="file" name="price_file" id="epcMultivendorFile" accept=".csv,.txt,.xls,.xlsx" required />
						<small class="help-block">Required columns: Brand, Article, Qty, Price, <strong>Vendor full name</strong>, <strong>Vendor short</strong>. Optional: Name, Delivery days.</small>
					</div>
				</div>
				<div class="epc-multivendor-form-actions">
					<button type="button" class="btn btn-primary btn-lg" id="epcMultivendorUploadBtn">
						<i class="fas fa-cloud-upload-alt"></i> Upload &amp; create warehouses
					</button>
					<button type="button" class="btn btn-default" id="epcMultivendorSampleBtn">
						<i class="fas fa-download"></i> Download sample CSV
					</button>
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
							<td><strong>Vendor short</strong></td>
							<td>Yes</td>
							<td>Vendor short, Warehouse, Short name, WH</td>
							<td>Customer-facing warehouse</td>
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
			<p class="epc-multivendor-muted">Example: 100 vendors in one file → up to 100 warehouses created/updated, each with its own price list. Storefront search shows <em>S-UAE</em>, not <em>S-UAE Trading LLC</em>.</p>
		</div>
	</div>
</div>
