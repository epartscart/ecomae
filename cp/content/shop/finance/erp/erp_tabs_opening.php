<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_opening.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_gl.php';

epc_erp_opening_ensure_schema($db_link);
$batches = epc_erp_opening_list_batches($db_link);
$coa = $db_link->query('SELECT `id`,`code`,`name`,`opening_balance` FROM `epc_erp_coa_accounts` WHERE `active` = 1 ORDER BY `code` LIMIT 200')->fetchAll(PDO::FETCH_ASSOC);
$warehouses = epc_erp_inventory_list_warehouses($db_link);
$items = epc_erp_inventory_list_items($db_link);
$assets = epc_erp_fa_list_assets($db_link);
?>
<div class="epc-erp-hero">
	<h3><i class="fa fa-calendar-check-o"></i> Opening balances</h3>
	<p>For established businesses: post balances <strong>as of a specific date</strong> for chart of accounts, inventory quantities (weighted average cost), and fixed assets (cost + accumulated depreciation).</p>
</div>

<h4>Create opening batch</h4>
<form id="epc_ob_form_batch" class="form-inline" style="margin-bottom:16px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<select name="module" class="form-control input-sm">
		<option value="combined">Combined</option>
		<option value="coa">COA only</option>
		<option value="inventory">Inventory only</option>
		<option value="fixed_assets">Fixed assets only</option>
	</select>
	<input type="date" name="as_of_date" class="form-control input-sm" required value="<?php echo epc_erp_h(date('Y-01-01')); ?>">
	<input type="text" name="reference" class="form-control input-sm" placeholder="Reference e.g. FY2024 opening">
	<button type="submit" class="btn btn-sm btn-primary">Create draft batch</button>
</form>

<h4>Add COA opening line to batch</h4>
<form id="epc_ob_form_coa" class="form-inline" style="margin-bottom:16px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="number" name="batch_id" class="form-control input-sm" placeholder="Batch ID" required>
	<select name="entity_id" class="form-control input-sm" required>
		<option value="">COA account</option>
		<?php foreach ($coa as $c): ?>
		<option value="<?php echo (int)$c['id']; ?>"><?php echo epc_erp_h($c['code'] . ' ' . $c['name']); ?> (curr <?php echo epc_erp_money($c['opening_balance']); ?>)</option>
		<?php endforeach; ?>
	</select>
	<input type="number" step="0.01" name="debit" class="form-control input-sm" placeholder="Debit">
	<input type="number" step="0.01" name="credit" class="form-control input-sm" placeholder="Credit">
	<button type="submit" class="btn btn-sm btn-default">Add COA line</button>
</form>

<h4>Add inventory opening line</h4>
<form id="epc_ob_form_inv" class="form-inline" style="margin-bottom:16px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="number" name="batch_id" class="form-control input-sm" placeholder="Batch ID" required>
	<select name="warehouse_id" class="form-control input-sm" required>
		<?php foreach ($warehouses as $w): ?>
		<option value="<?php echo (int)$w['id']; ?>"><?php echo epc_erp_h($w['name']); ?></option>
		<?php endforeach; ?>
	</select>
	<select name="item_id" class="form-control input-sm" required>
		<?php foreach ($items as $it): ?>
		<option value="<?php echo (int)$it['id']; ?>"><?php echo epc_erp_h($it['sku']); ?></option>
		<?php endforeach; ?>
	</select>
	<input type="number" step="0.001" name="qty" class="form-control input-sm" placeholder="Qty" required>
	<input type="number" step="0.0001" name="unit_cost" class="form-control input-sm" placeholder="Unit cost" required>
	<input type="text" name="batch_no" class="form-control input-sm" placeholder="Batch">
	<input type="date" name="expiry_date" class="form-control input-sm">
	<button type="submit" class="btn btn-sm btn-default">Add inventory line</button>
</form>

<h4>Post batch (applies all lines)</h4>
<form id="epc_ob_form_post" class="form-inline" style="margin-bottom:18px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="number" name="batch_id" class="form-control input-sm" placeholder="Batch ID" required>
	<button type="submit" class="btn btn-sm btn-success">Post opening batch</button>
</form>

<h4>Recent batches</h4>
<table class="table table-bordered table-condensed table-striped">
	<thead><tr><th>ID</th><th>Module</th><th>As of</th><th>Status</th><th>Reference</th><th>Posted</th></tr></thead>
	<tbody>
	<?php foreach ($batches as $b): ?>
		<tr>
			<td><?php echo (int)$b['id']; ?></td>
			<td><?php echo epc_erp_h($b['module']); ?></td>
			<td><?php echo epc_erp_h($b['as_of_date']); ?></td>
			<td><span class="label label-<?php echo $b['status'] === 'posted' ? 'success' : 'default'; ?>"><?php echo epc_erp_h($b['status']); ?></span></td>
			<td><?php echo epc_erp_h($b['reference']); ?></td>
			<td><?php echo (int)$b['time_posted'] ? epc_erp_h(date('Y-m-d H:i', (int)$b['time_posted'])) : '—'; ?></td>
		</tr>
	<?php endforeach; ?>
	<?php if (empty($batches)): ?><tr><td colspan="6" class="text-muted">No opening batches yet.</td></tr><?php endif; ?>
	</tbody>
</table>
<p class="text-muted">After posting, inventory movements appear as type <em>opening</em>; COA accounts receive updated <code>opening_balance</code>. See <a href="<?php echo epc_erp_h($guideUrl); ?>">ERP guide</a> and docs/ERP_INVENTORY_ASSETS_GUIDE.md.</p>
<script>
(function(){
	function bind(id, action) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(e){ e.preventDefault(); var fn = (typeof window.epcErpPost === 'function') ? window.epcErpPost : (typeof postAction === 'function' ? postAction : null); if (fn) fn(action, f); });
	}
	bind('epc_ob_form_batch', 'opening_create_batch');
	bind('epc_ob_form_coa', 'opening_add_coa_line');
	bind('epc_ob_form_inv', 'opening_add_inv_line');
	bind('epc_ob_form_post', 'opening_post_batch');
})();
</script>
