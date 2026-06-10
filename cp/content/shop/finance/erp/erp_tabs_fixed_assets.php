<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fixed_assets.php';

epc_erp_fa_ensure_schema($db_link);
$assets = epc_erp_fa_list_assets($db_link);
$summary = epc_erp_fa_summary($db_link);
$categories = $db_link->query('SELECT * FROM `epc_erp_fa_categories` WHERE `active` = 1 ORDER BY `name`')->fetchAll(PDO::FETCH_ASSOC);
$methods = epc_erp_fa_depreciation_methods();
$runs = $db_link->query('SELECT * FROM `epc_erp_fa_depreciation_runs` ORDER BY `period_month` DESC LIMIT 12')->fetchAll(PDO::FETCH_ASSOC);
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
	<div class="form-group"><label class="col-sm-3">Location / tracking</label><div class="col-sm-4"><input name="location" class="form-control input-sm" placeholder="Location"></div><div class="col-sm-5"><input name="tracking_id" class="form-control input-sm" placeholder="Tracking / barcode ID"></div></div>
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
