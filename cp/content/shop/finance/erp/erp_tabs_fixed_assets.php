<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fixed_assets.php';

epc_erp_fa_ensure_schema($db_link);
$assets = epc_erp_fa_list_assets($db_link);
$summary = epc_erp_fa_summary($db_link);
$categories = $db_link->query('SELECT * FROM `epc_erp_fa_categories` WHERE `active` = 1 ORDER BY `name`')->fetchAll(PDO::FETCH_ASSOC);
$methods = epc_erp_fa_depreciation_methods();
$runs = $db_link->query('SELECT * FROM `epc_erp_fa_depreciation_runs` ORDER BY `period_month` DESC LIMIT 12')->fetchAll(PDO::FETCH_ASSOC);
$faLeOpts = array();
$faBuOpts = array();
try {
	foreach ($db_link->query("SELECT `id`,`code`,`name` FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) as $le) {
		$faLeOpts[(int) $le['id']] = $le['code'] . ' · ' . $le['name'];
	}
} catch (Exception $e) {
}
try {
	foreach ($db_link->query("SELECT `id`,`code`,`name` FROM `epc_erp_pm_business_units` WHERE `active`=1 ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) as $bu) {
		$faBuOpts[(int) $bu['id']] = $bu['code'] . ' · ' . $bu['name'];
	}
} catch (Exception $e) {
}
?>
<div class="epc-erp-hero">
	<h3><i class="fa fa-building"></i> Fixed assets &amp; depreciation</h3>
	<p>Asset register, tracking ID/location, accumulated depreciation, book value, and monthly runs (straight line, declining balance, double declining, units of production).</p>
</div>
<div class="epc-erp-kpi" style="margin-bottom:14px;">
	<div class="kpi"><div class="lbl">Assets</div><div class="val"><?php echo (int)$summary['count']; ?></div></div>
	<div class="kpi"><div class="lbl">Original cost</div><div class="val"><?php echo epc_erp_money($summary['total_cost']); ?> AED</div></div>
	<div class="kpi"><div class="lbl">Accumulated depreciation</div><div class="val red"><?php echo epc_erp_money($summary['total_accumulated']); ?> AED</div></div>
	<div class="kpi"><div class="lbl">Net book value</div><div class="val green"><?php echo epc_erp_money($summary['total_book_value']); ?> AED</div></div>
</div>

<h4>Register new asset</h4>
<form id="epc_fa_form_asset" class="form-horizontal" style="max-width:800px;margin-bottom:18px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Asset code</label><div class="col-sm-9"><input name="asset_code" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Name</label><div class="col-sm-9"><input name="name" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Category</label><div class="col-sm-9">
		<select name="category_id" class="form-control input-sm">
			<option value="0">—</option>
			<?php foreach ($categories as $c): ?>
			<option value="<?php echo (int)$c['id']; ?>"><?php echo epc_erp_h($c['name']); ?></option>
			<?php endforeach; ?>
		</select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Acquisition date</label><div class="col-sm-9"><input type="date" name="acquisition_date" class="form-control input-sm" value="<?php echo epc_erp_h(date('Y-m-d')); ?>"></div></div>
	<div class="form-group"><label class="col-sm-3">Cost (AED)</label><div class="col-sm-9"><input type="number" step="0.01" name="cost" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Salvage value</label><div class="col-sm-9"><input type="number" step="0.01" name="salvage_value" class="form-control input-sm" value="0"></div></div>
	<div class="form-group"><label class="col-sm-3">Useful life (months)</label><div class="col-sm-9"><input type="number" name="useful_life_months" class="form-control input-sm" value="60"></div></div>
	<div class="form-group"><label class="col-sm-3">Method</label><div class="col-sm-9">
		<select name="depreciation_method" class="form-control input-sm">
			<?php foreach ($methods as $k => $lbl): ?>
			<option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($lbl); ?></option>
			<?php endforeach; ?>
		</select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Opening accumulated dep.</label><div class="col-sm-9"><input type="number" step="0.01" name="accumulated_depreciation" class="form-control input-sm" value="0" title="For migrated businesses"></div></div>
	<div class="form-group"><label class="col-sm-3">Asset group</label><div class="col-sm-9"><input name="asset_group" class="form-control input-sm" placeholder="e.g. VEH / IT / FURN"></div></div>
	<div class="form-group"><label class="col-sm-3">Asset type</label><div class="col-sm-9">
		<select name="asset_type" class="form-control input-sm"><option value="tangible">Tangible</option><option value="intangible">Intangible</option></select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Major type / Property type</label><div class="col-sm-4"><input name="major_type" class="form-control input-sm" placeholder="Major type"></div><div class="col-sm-5"><input name="property_type" class="form-control input-sm" placeholder="Property type"></div></div>
	<div class="form-group"><label class="col-sm-3">Legal entity</label><div class="col-sm-9">
		<select name="legal_entity_id" class="form-control input-sm"><option value="0">— none —</option>
		<?php foreach ($faLeOpts as $lid => $llbl): ?><option value="<?php echo (int) $lid; ?>"><?php echo epc_erp_h($llbl); ?></option><?php endforeach; ?>
		</select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Business unit</label><div class="col-sm-9">
		<select name="business_unit_id" class="form-control input-sm"><option value="0">— none —</option>
		<?php foreach ($faBuOpts as $bid => $blbl): ?><option value="<?php echo (int) $bid; ?>"><?php echo epc_erp_h($blbl); ?></option><?php endforeach; ?>
		</select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Quantity</label><div class="col-sm-9"><input type="number" step="0.001" name="quantity" class="form-control input-sm" value="1"></div></div>
	<div class="form-group"><label class="col-sm-3">Placed in service / Disposal</label><div class="col-sm-4"><input type="date" name="placed_in_service_date" class="form-control input-sm"></div><div class="col-sm-5"><input type="date" name="disposal_date" class="form-control input-sm"></div></div>
	<div class="form-group"><label class="col-sm-3">Depreciation convention</label><div class="col-sm-9">
		<select name="depreciation_convention" class="form-control input-sm"><option value="">—</option><option value="full_month">Full month</option><option value="mid_month">Mid month</option><option value="half_year">Half year</option><option value="full_year">Full year</option></select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Posting profile</label><div class="col-sm-9"><input name="posting_profile" class="form-control input-sm" placeholder="e.g. CURRENT"></div></div>
	<div class="form-group"><label class="col-sm-3">Location / Tracking ID</label><div class="col-sm-4"><input name="location" class="form-control input-sm" placeholder="Location"></div><div class="col-sm-5"><input name="tracking_id" class="form-control input-sm" placeholder="Tracking ID"></div></div>
	<div class="form-group"><label class="col-sm-3">Barcode / Serial no.</label><div class="col-sm-4"><input name="barcode" class="form-control input-sm" placeholder="Barcode"></div><div class="col-sm-5"><input name="serial_no" class="form-control input-sm" placeholder="Serial no."></div></div>
	<div class="form-group"><label class="col-sm-3">Make / Model / Manufacturer</label><div class="col-sm-3"><input name="make" class="form-control input-sm" placeholder="Make"></div><div class="col-sm-3"><input name="model" class="form-control input-sm" placeholder="Model"></div><div class="col-sm-3"><input name="manufacturer" class="form-control input-sm" placeholder="Manufacturer"></div></div>
	<div class="form-group"><label class="col-sm-3">Supplier vendor ID / PO ref</label><div class="col-sm-4"><input type="number" name="supplier_vendor_id" class="form-control input-sm" placeholder="Vendor ID"></div><div class="col-sm-5"><input name="purchase_invoice_ref" class="form-control input-sm" placeholder="Purchase invoice / PO ref"></div></div>
	<div class="form-group"><label class="col-sm-3">Insurance policy / Insured value</label><div class="col-sm-4"><input name="insurance_policy_no" class="form-control input-sm" placeholder="Policy no."></div><div class="col-sm-5"><input type="number" step="0.01" name="insured_value" class="form-control input-sm" placeholder="Insured value"></div></div>
	<div class="form-group"><label class="col-sm-3">Warranty expiry / Custodian</label><div class="col-sm-4"><input type="date" name="warranty_expiry" class="form-control input-sm"></div><div class="col-sm-5"><input name="custodian" class="form-control input-sm" placeholder="Person responsible"></div></div>
	<div class="form-group"><label class="col-sm-3">GL accounts (asset / depr / accum)</label><div class="col-sm-3"><input name="gl_asset_account" class="form-control input-sm" placeholder="Asset a/c"></div><div class="col-sm-3"><input name="gl_depreciation_account" class="form-control input-sm" placeholder="Depr. a/c"></div><div class="col-sm-3"><input name="gl_accum_depr_account" class="form-control input-sm" placeholder="Accum. a/c"></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-sm btn-success">Register asset</button></div></div>
</form>

<h4>Post monthly depreciation</h4>
<form id="epc_fa_form_dep" class="form-inline" style="margin-bottom:18px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="month" name="period_month" class="form-control input-sm" value="<?php echo epc_erp_h(date('Y-m')); ?>" required>
	<input type="text" name="note" class="form-control input-sm" placeholder="Note">
	<button type="submit" class="btn btn-sm btn-warning">Run depreciation</button>
</form>

<h4>Asset register</h4>
<table class="table table-bordered table-condensed table-striped">
	<thead><tr><th>Code</th><th>Name</th><th>Category</th><th>Cost</th><th>Accum. dep.</th><th>Book value</th><th>Method</th><th>Status</th><th>Tracking</th></tr></thead>
	<tbody>
	<?php foreach ($assets as $a): ?>
		<tr>
			<td><?php echo epc_erp_h($a['asset_code']); ?></td>
			<td><?php echo epc_erp_h($a['name']); ?></td>
			<td><?php echo epc_erp_h($a['category_name'] ?? '—'); ?></td>
			<td><?php echo epc_erp_money($a['cost']); ?></td>
			<td><?php echo epc_erp_money($a['accumulated_depreciation']); ?></td>
			<td><?php echo epc_erp_money($a['book_value']); ?></td>
			<td><?php echo epc_erp_h($methods[$a['depreciation_method']] ?? $a['depreciation_method']); ?></td>
			<td><?php echo epc_erp_h($a['status']); ?></td>
			<td><?php echo epc_erp_h($a['tracking_id'] ?: '—'); ?></td>
		</tr>
	<?php endforeach; ?>
	<?php if (empty($assets)): ?><tr><td colspan="9" class="text-muted">No assets — register items or use Opening balances tab for migration.</td></tr><?php endif; ?>
	</tbody>
</table>

<h4>Recent depreciation runs</h4>
<table class="table table-bordered table-condensed">
	<thead><tr><th>Period</th><th>Total</th><th>Run date</th><th>Note</th></tr></thead>
	<tbody>
	<?php foreach ($runs as $r): ?>
		<tr>
			<td><?php echo epc_erp_h($r['period_month']); ?></td>
			<td><?php echo epc_erp_money($r['total_amount']); ?> AED</td>
			<td><?php echo epc_erp_h(date('Y-m-d H:i', (int)$r['run_date'])); ?></td>
			<td><?php echo epc_erp_h($r['note']); ?></td>
		</tr>
	<?php endforeach; ?>
	</tbody>
</table>
<script>
(function(){
	function bind(id, action) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(e){ e.preventDefault(); if (typeof postAction === 'function') postAction(action, f); });
	}
	bind('epc_fa_form_asset', 'fa_create_asset');
	bind('epc_fa_form_dep', 'fa_run_depreciation');
})();
</script>
