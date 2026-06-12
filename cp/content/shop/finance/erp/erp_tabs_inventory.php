<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';

epc_erp_inventory_ensure_schema($db_link);
$whFilter = (int)($_GET['wh'] ?? 0);
$warehouses = epc_erp_inventory_list_warehouses($db_link);
$items = epc_erp_inventory_list_items($db_link);
$stock = epc_erp_inventory_stock_report($db_link, $whFilter);
$valuation = epc_erp_inventory_valuation_total($db_link, $whFilter);
$fieldDefs = $db_link->query('SELECT * FROM `epc_erp_inv_field_defs` WHERE `active` = 1 ORDER BY `sort_order`')->fetchAll(PDO::FETCH_ASSOC);
?>
<div class="epc-erp-hero">
	<h3><i class="fa fa-cubes"></i> Inventory management</h3>
	<p>Multi-warehouse stock, <strong>weighted average cost</strong>, purchase receipts, sales issues, period closing, perishables (expiry/batch), and up to five custom fields per SKU.</p>
</div>
<div class="epc-erp-kpi" style="margin-bottom:14px;">
	<div class="kpi"><div class="lbl">Stock valuation (avg cost)</div><div class="val green"><?php echo epc_erp_money($valuation); ?> AED</div></div>
	<div class="kpi"><div class="lbl">Warehouses</div><div class="val"><?php echo count($warehouses); ?></div></div>
	<div class="kpi"><div class="lbl">Active SKUs</div><div class="val"><?php echo count($items); ?></div></div>
	<div class="kpi"><div class="lbl">Stock lines</div><div class="val"><?php echo count($stock); ?></div></div>
</div>
<p>
	<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($storagesUrl); ?>"><i class="fa fa-archive"></i> Legacy shop storages</a>
	<button type="button" class="btn btn-info btn-sm" id="epc_inv_sync_wh"><i class="fa fa-refresh"></i> Sync warehouses from shop storages</button>
</p>

<h4>Add warehouse</h4>
<form id="epc_inv_form_wh" class="form-inline" style="margin-bottom:16px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="text" name="code" class="form-control input-sm" placeholder="Code e.g. WH-DXB" required>
	<input type="text" name="name" class="form-control input-sm" placeholder="Warehouse name" required>
	<button type="submit" class="btn btn-sm btn-primary">Create</button>
</form>

<h4>New inventory item (SKU)</h4>
<form id="epc_inv_form_item" class="form-horizontal" style="max-width:720px;margin-bottom:18px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">SKU</label><div class="col-sm-9"><input name="sku" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Name</label><div class="col-sm-9"><input name="name" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Type</label><div class="col-sm-9">
		<select name="item_type" class="form-control input-sm">
			<option value="standard">Standard</option>
			<option value="perishable">Perishable (expiry)</option>
			<option value="serialized">Serialized</option>
		</select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Unit of measure</label><div class="col-sm-9">
		<select name="unit" class="form-control input-sm">
			<optgroup label="General">
				<option value="pcs" selected>pcs (pieces)</option>
				<option value="pair">pair</option>
				<option value="set">set</option>
				<option value="box">box</option>
				<option value="kg">kg</option>
				<option value="litre">litre</option>
				<option value="metre">metre</option>
				<option value="hour">hour</option>
			</optgroup>
			<optgroup label="Jewellery &amp; bullion">
				<option value="gram">gram (g)</option>
				<option value="carat">carat (ct)</option>
				<option value="tola">tola</option>
				<option value="troy_oz">troy oz</option>
			</optgroup>
		</select>
		<span class="help-block" style="margin:4px 0 0;font-size:11px;">Jewellery items: pick <strong>gram</strong> (gold/silver by weight), <strong>carat</strong> (diamonds/stones) or <strong>tola</strong>. Quantities and weighted-average cost are tracked in this unit.</span>
	</div></div>
	<?php foreach ($fieldDefs as $fd): ?>
	<div class="form-group"><label class="col-sm-3"><?php echo epc_erp_h($fd['label']); ?></label>
		<div class="col-sm-9"><input name="custom_<?php echo epc_erp_h($fd['field_key']); ?>" class="form-control input-sm" placeholder="<?php echo epc_erp_h($fd['field_type']); ?>"></div>
	</div>
	<?php endforeach; ?>
	<?php echo epc_erp_dim_render_fields($db_link); ?>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-sm btn-success">Create item</button></div></div>
</form>

<h4>Warehouse transfer <small>(paired out + in at source average cost)</small></h4>
<form id="epc_inv_form_transfer" class="form-inline" style="margin-bottom:18px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<select name="from_warehouse_id" class="form-control input-sm" required>
		<option value="">From warehouse</option>
		<?php foreach ($warehouses as $w): ?>
		<option value="<?php echo (int)$w['id']; ?>"><?php echo epc_erp_h($w['name']); ?></option>
		<?php endforeach; ?>
	</select>
	<select name="to_warehouse_id" class="form-control input-sm" required>
		<option value="">To warehouse</option>
		<?php foreach ($warehouses as $w): ?>
		<option value="<?php echo (int)$w['id']; ?>"><?php echo epc_erp_h($w['name']); ?></option>
		<?php endforeach; ?>
	</select>
	<select name="item_id" class="form-control input-sm" required>
		<option value="">Item</option>
		<?php foreach ($items as $it): ?>
		<option value="<?php echo (int)$it['id']; ?>"><?php echo epc_erp_h($it['sku']); ?></option>
		<?php endforeach; ?>
	</select>
	<input type="number" step="0.001" name="qty" class="form-control input-sm" placeholder="Qty" required>
	<input type="text" name="batch_no" class="form-control input-sm" placeholder="Batch">
	<input type="text" name="reference" class="form-control input-sm" placeholder="Ref">
	<button type="submit" class="btn btn-sm btn-info">Transfer stock</button>
</form>

<h4>Bulk upload (CSV)</h4>
<p class="text-muted" style="font-size:12px;">Columns: <code>sku,qty,unit_cost,batch_no,expiry_date,movement_type,warehouse_code,to_warehouse_code,reference</code>. Use <code>movement_type=transfer</code> with <code>to_warehouse_code</code> for inter-warehouse moves. <strong>Negative <code>qty</code></strong> on <code>purchase_in</code>/<code>opening</code> rows is treated as an <code>adjustment</code> stock reduction; use <code>movement_type=adjustment</code> with negative qty for explicit corrections. Template:</p>
<pre style="font-size:11px;background:#f8f9fa;padding:8px;border-radius:4px;"><?php echo epc_erp_h(epc_erp_inventory_csv_template()); ?></pre>
<form id="epc_inv_form_csv" enctype="multipart/form-data" class="form-inline" style="margin-bottom:18px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<select name="warehouse_id" class="form-control input-sm">
		<option value="0">Default warehouse (or use warehouse_code column)</option>
		<?php foreach ($warehouses as $w): ?>
		<option value="<?php echo (int)$w['id']; ?>"><?php echo epc_erp_h($w['code']); ?></option>
		<?php endforeach; ?>
	</select>
	<select name="default_movement_type" class="form-control input-sm">
		<option value="purchase_in">Default: purchase in</option>
		<option value="adjustment">adjustment</option>
		<option value="opening">opening</option>
	</select>
	<input type="file" name="csv_file" accept=".csv,text/csv" class="form-control input-sm">
	<textarea name="csv_text" class="form-control input-sm" rows="3" placeholder="Or paste CSV here" style="min-width:280px;vertical-align:top;"></textarea>
	<button type="submit" class="btn btn-sm btn-success">Import CSV</button>
</form>

<h4>Record movement (purchase / sale / adjustment)</h4>
<form id="epc_inv_form_move" class="form-inline" style="margin-bottom:18px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<select name="movement_type" class="form-control input-sm">
		<option value="purchase_in">Purchase in</option>
		<option value="sale_out">Sale out</option>
		<option value="adjustment">Adjustment (+/− qty sign)</option>
	</select>
	<select name="warehouse_id" class="form-control input-sm" required>
		<option value="">Warehouse</option>
		<?php foreach ($warehouses as $w): ?>
		<option value="<?php echo (int)$w['id']; ?>"><?php echo epc_erp_h($w['name']); ?></option>
		<?php endforeach; ?>
	</select>
	<select name="item_id" class="form-control input-sm" required>
		<option value="">Item</option>
		<?php foreach ($items as $it): ?>
		<option value="<?php echo (int)$it['id']; ?>"><?php echo epc_erp_h($it['sku'] . ' — ' . $it['name']); ?></option>
		<?php endforeach; ?>
	</select>
	<input type="number" step="0.001" name="qty" class="form-control input-sm" placeholder="Qty" required>
	<input type="number" step="0.0001" name="unit_cost" class="form-control input-sm" placeholder="Unit cost (purchases)">
	<input type="text" name="batch_no" class="form-control input-sm" placeholder="Batch">
	<input type="date" name="expiry_date" class="form-control input-sm" placeholder="Expiry">
	<input type="text" name="reference" class="form-control input-sm" placeholder="Ref">
	<button type="submit" class="btn btn-sm btn-primary">Post movement</button>
</form>

<h4>Period closing snapshot</h4>
<form id="epc_inv_form_close" class="form-inline" style="margin-bottom:18px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="date" name="period_end" class="form-control input-sm" value="<?php echo epc_erp_h(date('Y-m-t')); ?>" required>
	<select name="warehouse_id" class="form-control input-sm">
		<option value="0">All warehouses</option>
		<?php foreach ($warehouses as $w): ?>
		<option value="<?php echo (int)$w['id']; ?>"><?php echo epc_erp_h($w['name']); ?></option>
		<?php endforeach; ?>
	</select>
	<button type="submit" class="btn btn-sm btn-warning">Run closing</button>
</form>

<h4>Stock on hand <small>(weighted average)</small>
	<?php if ($whFilter): ?> — filtered<?php endif; ?>
	<a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str)); ?>">All WH</a>
</h4>
<table class="table table-bordered table-condensed table-striped">
	<thead><tr><th>Warehouse</th><th>SKU</th><th>Name</th><th>Type</th><th>Unit</th><th>Qty</th><th>Avg cost</th><th>Value</th><th>Batch</th><th>Expiry</th></tr></thead>
	<tbody>
	<?php foreach ($stock as $s):
		$val = (float)$s['qty_on_hand'] * (float)$s['avg_unit_cost'];
	?>
		<tr>
			<td><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str) . '&wh=' . (int)$s['warehouse_id']); ?>"><?php echo epc_erp_h($s['warehouse_name']); ?></a></td>
			<td><?php echo epc_erp_h($s['sku']); ?></td>
			<td><?php echo epc_erp_h($s['name']); ?></td>
			<td><?php echo epc_erp_h($s['item_type']); ?></td>
			<td><?php echo epc_erp_h($s['unit'] ?? 'pcs'); ?></td>
			<td><?php echo epc_erp_h(number_format((float)$s['qty_on_hand'], 3)); ?></td>
			<td><?php echo epc_erp_money($s['avg_unit_cost']); ?></td>
			<td><?php echo epc_erp_money($val); ?></td>
			<td><?php echo epc_erp_h($s['batch_no'] ?? '—'); ?></td>
			<td><?php echo !empty($s['expiry_date']) ? epc_erp_h($s['expiry_date']) : '—'; ?></td>
		</tr>
	<?php endforeach; ?>
	<?php if (empty($stock)): ?><tr><td colspan="10" class="text-muted">No stock yet — sync warehouses, create items, post opening or purchase movements.</td></tr><?php endif; ?>
	</tbody>
</table>
<script>
(function(){
	function bind(id, action) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(e){
			e.preventDefault();
			if (typeof postAction === 'function') postAction(action, f);
		});
	}
	bind('epc_inv_form_wh', 'inv_create_warehouse');
	bind('epc_inv_form_item', 'inv_create_item');
	bind('epc_inv_form_transfer', 'inv_transfer');
	bind('epc_inv_form_move', 'inv_record_movement');
	bind('epc_inv_form_close', 'inv_run_closing');
	var csvForm = document.getElementById('epc_inv_form_csv');
	if (csvForm) {
		csvForm.addEventListener('submit', function(e){
			e.preventDefault();
			var fd = new FormData(csvForm);
			fd.append('action', 'inv_import_csv');
			fetch(<?php echo json_encode($erpAjaxEndpoint); ?>, { method:'POST', body:fd, credentials:'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){ if (typeof showMsg === 'function') showMsg(!!j.status, j.message); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
		});
	}
	var syncBtn = document.getElementById('epc_inv_sync_wh');
	if (syncBtn) syncBtn.addEventListener('click', function(){
		var fd = new FormData();
		fd.append('action', 'inv_sync_warehouses');
		fd.append('csrf_guard_key', <?php echo json_encode($csrf); ?>);
		fetch(<?php echo json_encode($erpAjaxEndpoint); ?>, { method:'POST', body:fd, credentials:'same-origin' })
			.then(function(r){ return r.json(); })
			.then(function(j){ if (typeof showMsg === 'function') showMsg(!!j.status, j.message); if (j.status) setTimeout(function(){ location.reload(); }, 600); });
	});
})();
</script>
