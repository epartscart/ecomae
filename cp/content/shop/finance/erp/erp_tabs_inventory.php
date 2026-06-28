<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';

epc_erp_inventory_ensure_schema($db_link);
epc_jw_ensure_integration_schema($db_link);
$epcJwMode = epc_jw_is_jewellery_tenant($db_link);
$whFilter = (int)($_GET['wh'] ?? 0);
$warehouses = epc_erp_inventory_list_warehouses($db_link);
$items = epc_erp_inventory_list_items($db_link);
$stock = epc_erp_inventory_stock_report($db_link, $whFilter);
$valuation = epc_erp_inventory_valuation_total($db_link, $whFilter);
$fieldDefs = $db_link->query('SELECT * FROM `epc_erp_inv_field_defs` WHERE `active` = 1 ORDER BY `sort_order`')->fetchAll(PDO::FETCH_ASSOC);
$ledgerItem = (int)($_GET['ledger_item'] ?? 0);
$ledgerRows = epc_erp_inventory_ledger($db_link, $ledgerItem, $whFilter, 200);
$serialRows = epc_erp_inventory_serials($db_link, $ledgerItem, '', '', 150);
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
	<?php if (!empty($storagesUrl)): ?>
	<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($storagesUrl); ?>"><i class="fa fa-archive"></i> Legacy shop storages</a>
	<button type="button" class="btn btn-info btn-sm" id="epc_inv_sync_wh"><i class="fa fa-refresh"></i> Sync warehouses from shop storages</button>
	<?php endif; ?>
</p>
<?php
erp_d365_assets();
erp_action_pane_ribbon(array(
	array('label' => 'Manage', 'key' => 'manage', 'active' => true, 'groups' => array(
		array('label' => 'New', 'buttons' => array(
			array('label' => 'Item', 'icon' => 'fa-plus', 'class' => 'is-primary', 'target' => '#epc_inv_form_item'),
			array('label' => 'Warehouse', 'icon' => 'fa-archive', 'target' => '#epc_inv_form_wh'),
		)),
		array('label' => 'View', 'buttons' => array(
			array('label' => 'Refresh', 'icon' => 'fa-refresh', 'url' => epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str)),
		)),
	)),
	array('label' => 'Transactions', 'key' => 'txn', 'groups' => array(
		array('label' => 'Inventory', 'buttons' => array(
			array('label' => 'Post movement', 'icon' => 'fa-exchange', 'target' => '#epc_inv_form_move'),
			array('label' => 'Transfer', 'icon' => 'fa-random', 'target' => '#epc_inv_form_transfer'),
			array('label' => 'Period closing', 'icon' => 'fa-lock', 'target' => '#epc_inv_form_close'),
		)),
	)),
	array('label' => 'Data', 'key' => 'data', 'groups' => array(
		array('label' => 'Tools', 'buttons' => array(
			array('label' => 'Import CSV', 'icon' => 'fa-upload', 'target' => '#epc_inv_form_csv'),
			array('label' => 'Scan / lookup', 'icon' => 'fa-barcode', 'target' => '#epc_inv_form_scan'),
		)),
	)),
));
erp_fasttab_open('Master data — warehouses & items', array('open' => false, 'icon' => 'fa-database'));
?>

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
	<div class="form-group"><label class="col-sm-3">Barcode <small>(EAN/UPC/QR)</small></label><div class="col-sm-9"><input name="barcode" class="form-control input-sm" placeholder="Scan or type barcode"></div></div>
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
	<div class="form-group"><label class="col-sm-3">Search name</label><div class="col-sm-9"><input name="search_name" class="form-control input-sm" placeholder="Short / alternate name"></div></div>
	<div class="form-group"><label class="col-sm-3">Product type</label><div class="col-sm-9">
		<select name="product_type" class="form-control input-sm"><option value="item">Item</option><option value="service">Service</option></select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Item group</label><div class="col-sm-9"><input name="item_group" class="form-control input-sm" placeholder="e.g. Finished goods / Raw material"></div></div>
	<div class="form-group"><label class="col-sm-3">Item model group</label><div class="col-sm-9"><input name="item_model_group" class="form-control input-sm" placeholder="e.g. FIFO / STD"></div></div>
	<div class="form-group"><label class="col-sm-3">Costing method</label><div class="col-sm-9">
		<select name="costing_method" class="form-control input-sm"><option value="">— inherit —</option><option value="weighted_avg">Weighted average</option><option value="fifo">FIFO</option><option value="lifo">LIFO</option><option value="standard">Standard cost</option></select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Storage dimension group</label><div class="col-sm-9"><input name="storage_dim_group" class="form-control input-sm" placeholder="e.g. Site-WH"></div></div>
	<div class="form-group"><label class="col-sm-3">Tracking dimension group</label><div class="col-sm-9"><input name="tracking_dim_group" class="form-control input-sm" placeholder="e.g. Batch / Serial"></div></div>
	<div class="form-group"><label class="col-sm-3">Purchase unit</label><div class="col-sm-9"><input name="purchase_unit" class="form-control input-sm" placeholder="e.g. box"></div></div>
	<div class="form-group"><label class="col-sm-3">Sales unit</label><div class="col-sm-9"><input name="sales_unit" class="form-control input-sm" placeholder="e.g. pcs"></div></div>
	<div class="form-group"><label class="col-sm-3">Default warehouse</label><div class="col-sm-9">
		<select name="default_warehouse_id" class="form-control input-sm"><option value="0">— none —</option>
		<?php foreach ($warehouses as $w): ?><option value="<?php echo (int) $w['id']; ?>"><?php echo epc_erp_h($w['code'] . ' · ' . $w['name']); ?></option><?php endforeach; ?>
		</select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Default vendor ID</label><div class="col-sm-9"><input type="number" name="default_vendor_id" class="form-control input-sm" placeholder="Vendor (supplier) ID"></div></div>
	<div class="form-group"><label class="col-sm-3">Sales tax group</label><div class="col-sm-9"><input name="sales_tax_group" class="form-control input-sm" placeholder="e.g. STD"></div></div>
	<div class="form-group"><label class="col-sm-3">Purchase tax group</label><div class="col-sm-9"><input name="purchase_tax_group" class="form-control input-sm" placeholder="e.g. STD"></div></div>
	<div class="form-group"><label class="col-sm-3">Buyer group</label><div class="col-sm-9"><input name="buyer_group" class="form-control input-sm"></div></div>
	<div class="form-group"><label class="col-sm-3">Coverage (planning) group</label><div class="col-sm-9"><input name="coverage_group" class="form-control input-sm" placeholder="e.g. Min/Max"></div></div>
	<div class="form-group"><label class="col-sm-3">ABC code</label><div class="col-sm-9">
		<select name="abc_code" class="form-control input-sm"><option value="">—</option><option value="A">A</option><option value="B">B</option><option value="C">C</option></select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Net / Gross / Tare weight</label><div class="col-sm-9" style="display:flex;gap:6px;">
		<input type="number" step="0.001" name="net_weight" class="form-control input-sm" placeholder="Net">
		<input type="number" step="0.001" name="gross_weight" class="form-control input-sm" placeholder="Gross">
		<input type="number" step="0.001" name="tare_weight" class="form-control input-sm" placeholder="Tare">
	</div></div>
	<div class="form-group"><label class="col-sm-3">Volume / Depth / Width / Height</label><div class="col-sm-9" style="display:flex;gap:6px;">
		<input type="number" step="0.001" name="volume" class="form-control input-sm" placeholder="Vol">
		<input type="number" step="0.001" name="gross_depth" class="form-control input-sm" placeholder="Depth">
		<input type="number" step="0.001" name="gross_width" class="form-control input-sm" placeholder="Width">
		<input type="number" step="0.001" name="gross_height" class="form-control input-sm" placeholder="Height">
	</div></div>
	<div class="form-group"><label class="col-sm-3">Standard cost / Sales / Purchase price</label><div class="col-sm-9" style="display:flex;gap:6px;">
		<input type="number" step="0.0001" name="standard_cost" class="form-control input-sm" placeholder="Std cost">
		<input type="number" step="0.0001" name="sales_price" class="form-control input-sm" placeholder="Sales price">
		<input type="number" step="0.0001" name="purchase_price" class="form-control input-sm" placeholder="Purchase price">
	</div></div>
	<div class="form-group"><label class="col-sm-3">Notes</label><div class="col-sm-9"><input name="notes" class="form-control input-sm"></div></div>
	<?php foreach ($fieldDefs as $fd): ?>
	<div class="form-group"><label class="col-sm-3"><?php echo epc_erp_h($fd['label']); ?></label>
		<div class="col-sm-9"><input name="custom_<?php echo epc_erp_h($fd['field_key']); ?>" class="form-control input-sm" placeholder="<?php echo epc_erp_h($fd['field_type']); ?>"></div>
	</div>
	<?php endforeach; ?>
	<?php echo epc_erp_dim_render_fields($db_link); ?>
	<?php echo epc_jw_inventory_item_fields_html($db_link); ?>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-sm btn-success">Create item</button></div></div>
</form>
<?php erp_fasttab_close(); ?>
<?php erp_fasttab_open('Stock operations — movements, transfers, import & closing', array('open' => false, 'icon' => 'fa-exchange')); ?>

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
	<input type="text" name="serial_no" class="form-control input-sm" placeholder="Serial no (serialized)">
	<input type="text" name="reference" class="form-control input-sm" placeholder="Ref">
	<button type="submit" class="btn btn-sm btn-primary">Post movement</button>
</form>

<h4><i class="fa fa-barcode"></i> Scan / lookup by barcode</h4>
<form id="epc_inv_form_scan" class="form-inline" style="margin-bottom:8px;">
	<input type="text" id="epc_inv_scan_code" class="form-control input-sm" placeholder="Scan barcode or type SKU" autocomplete="off" style="min-width:260px;">
	<button type="submit" class="btn btn-sm btn-default">Lookup</button>
	<span id="epc_inv_scan_out" style="margin-left:10px;"></span>
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
<?php erp_fasttab_close(); ?>

<h4>Stock on hand <small>(weighted average)</small>
	<?php if ($whFilter): ?> — filtered<?php endif; ?>
	<a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str)); ?>">All WH</a>
</h4>
<?php erp_list_toolbar(array(
	'views' => array('On-hand by warehouse', 'All items'),
	'search' => array('placeholder' => 'Filter stock', 'target' => '#epc_inv_stock_tbl'),
)); ?>
<table class="table table-bordered table-condensed table-striped epc-erp-table" id="epc_inv_stock_tbl">
	<thead><tr><th class="epc-d365-statcol"></th><th data-sort="text">Warehouse</th><th data-sort="text">SKU</th><th data-sort="text">Name</th><th data-sort="text">Type</th><th>Unit</th><?php if ($epcJwMode): ?><th>Metal</th><th>Karat</th><th class="num">Weight (g)</th><?php endif; ?><th class="num" data-sort="num">Qty</th><th class="num" data-sort="num">Avg cost</th><th class="num" data-sort="num">Value</th><th>Batch</th><th>Expiry</th></tr></thead>
	<tbody>
	<?php $epcInvVal = 0.0; foreach ($stock as $s):
		$val = (float)$s['qty_on_hand'] * (float)$s['avg_unit_cost'];
		$epcInvVal += $val;
		$epcInvTone = ((float)$s['qty_on_hand'] <= 0) ? 'bad' : ((float)$s['qty_on_hand'] < 5 ? 'warn' : 'ok');
	?>
		<tr>
			<td class="epc-d365-statcol"><?php echo erp_status_dot($epcInvTone); ?></td>
			<td><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str) . '&wh=' . (int)$s['warehouse_id']); ?>"><?php echo epc_erp_h($s['warehouse_name']); ?></a></td>
			<td><?php echo epc_erp_h($s['sku']); ?></td>
			<td><?php echo epc_erp_h($s['name']); ?></td>
			<td><?php echo epc_erp_h($s['item_type']); ?></td>
			<td><?php echo epc_erp_h($s['unit'] ?? 'pcs'); ?></td>
			<?php if ($epcJwMode): ?>
			<td><?php echo epc_erp_h($s['jw_metal_type'] ?? ''); ?></td>
			<td><?php echo epc_erp_h($s['jw_karat'] ?? ''); ?></td>
			<td class="num"><?php echo epc_erp_h(number_format((float)($s['jw_weight_on_hand'] ?? 0), 3)); ?></td>
			<?php endif; ?>
			<td class="num"><?php echo epc_erp_h(number_format((float)$s['qty_on_hand'], 3)); ?></td>
			<td class="num"><?php echo epc_erp_money($s['avg_unit_cost']); ?></td>
			<td class="num"><?php echo epc_erp_money($val); ?></td>
			<td><?php echo epc_erp_h($s['batch_no'] ?? '—'); ?></td>
			<td><?php echo !empty($s['expiry_date']) ? epc_erp_h($s['expiry_date']) : '—'; ?></td>
		</tr>
	<?php endforeach; ?>
	<?php $epcInvCols = $epcJwMode ? 14 : 11; ?>
	<?php if (empty($stock)): ?><tr><td colspan="<?php echo $epcInvCols; ?>" class="text-muted">No stock yet — sync warehouses, create items, post opening or purchase movements.</td></tr><?php endif; ?>
	</tbody>
	<?php if (!empty($stock)): ?>
	<tfoot><tr class="epc-d365-sumrow"><td class="epc-d365-statcol"></td><td colspan="<?php echo $epcJwMode ? 10 : 7; ?>">Sum (<?php echo count($stock); ?> stock lines)</td><td class="num"><?php echo epc_erp_money($epcInvVal); ?></td><td colspan="2"></td></tr></tfoot>
	<?php endif; ?>
</table>

<h4><i class="fa fa-list-alt"></i> Stock ledger <small>(every movement with running balance)</small>
	<form method="get" class="form-inline" style="display:inline-block;margin-left:10px;">
		<?php foreach ($_GET as $gk => $gv): if (in_array($gk, array('ledger_item'), true) || !is_scalar($gv)) continue; ?>
		<input type="hidden" name="<?php echo epc_erp_h($gk); ?>" value="<?php echo epc_erp_h((string)$gv); ?>">
		<?php endforeach; ?>
		<select name="ledger_item" class="form-control input-sm" onchange="this.form.submit()">
			<option value="0">All items</option>
			<?php foreach ($items as $it): ?>
			<option value="<?php echo (int)$it['id']; ?>" <?php echo $ledgerItem === (int)$it['id'] ? 'selected' : ''; ?>><?php echo epc_erp_h($it['sku'] . ' — ' . $it['name']); ?></option>
			<?php endforeach; ?>
		</select>
	</form>
</h4>
<table class="table table-bordered table-condensed table-striped epc-erp-table">
	<thead><tr><th>Date</th><th>Type</th><th>Warehouse</th><th>SKU</th><th>Batch</th><th>Serial</th><th class="text-right num">Qty</th><th class="text-right num">Unit cost</th><th class="text-right num">Balance</th><th>Ref</th></tr></thead>
	<tbody>
	<?php foreach ($ledgerRows as $m):
		$isIn = (float)$m['signed_qty'] >= 0; ?>
		<tr>
			<td><?php echo epc_erp_h(date('Y-m-d', (int)$m['movement_date'])); ?></td>
			<td><span class="label label-<?php echo $isIn ? 'success' : 'warning'; ?>"><?php echo epc_erp_h($m['movement_type']); ?></span></td>
			<td><?php echo epc_erp_h($m['warehouse_name'] ?? ''); ?></td>
			<td><?php echo epc_erp_h($m['sku'] ?? ''); ?></td>
			<td><?php echo epc_erp_h($m['batch_no'] ?? '—'); ?></td>
			<td><?php echo epc_erp_h(($m['serial_no'] ?? '') !== '' ? $m['serial_no'] : '—'); ?></td>
			<td class="text-right" style="color:<?php echo $isIn ? 'green' : '#c00'; ?>;"><?php echo epc_erp_h(number_format((float)$m['signed_qty'], 3)); ?></td>
			<td class="text-right"><?php echo epc_erp_money($m['unit_cost']); ?></td>
			<td class="text-right"><strong><?php echo epc_erp_h(number_format((float)$m['running_balance'], 3)); ?></strong></td>
			<td><?php echo epc_erp_h($m['reference'] ?? ''); ?></td>
		</tr>
	<?php endforeach; ?>
	<?php if (empty($ledgerRows)): ?><tr><td colspan="10" class="text-muted">No movements recorded yet.</td></tr><?php endif; ?>
	</tbody>
</table>

<h4><i class="fa fa-tags"></i> Serial register <small>(serialized units &amp; lifecycle)</small></h4>
<table class="table table-bordered table-condensed table-striped epc-erp-table">
	<thead><tr><th>Serial no</th><th>SKU</th><th>Item</th><th>Warehouse</th><th>Batch</th><th>Status</th><th class="text-right num">Unit cost</th><th>Updated</th></tr></thead>
	<tbody>
	<?php foreach ($serialRows as $sr):
		$stColor = array('in_stock' => 'success', 'sold' => 'default', 'returned' => 'info', 'scrapped' => 'danger', 'in_transit' => 'warning'); ?>
		<tr>
			<td><code><?php echo epc_erp_h($sr['serial_no']); ?></code></td>
			<td><?php echo epc_erp_h($sr['sku'] ?? ''); ?></td>
			<td><?php echo epc_erp_h($sr['item_name'] ?? ''); ?></td>
			<td><?php echo epc_erp_h($sr['warehouse_name'] ?? ''); ?></td>
			<td><?php echo epc_erp_h($sr['batch_no'] ?? '—'); ?></td>
			<td><span class="label label-<?php echo $stColor[$sr['status']] ?? 'default'; ?>"><?php echo epc_erp_h($sr['status']); ?></span></td>
			<td class="text-right"><?php echo epc_erp_money($sr['unit_cost']); ?></td>
			<td><?php echo epc_erp_h(date('Y-m-d', (int)$sr['time_updated'])); ?></td>
		</tr>
	<?php endforeach; ?>
	<?php if (empty($serialRows)): ?><tr><td colspan="8" class="text-muted">No serialized units yet — post a movement with a serial number.</td></tr><?php endif; ?>
	</tbody>
</table>
<script>
(function(){
	function bind(id, action) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(e){
			e.preventDefault();
			var fn = (typeof window.epcErpPost === 'function') ? window.epcErpPost : (typeof postAction === 'function' ? postAction : null);
			if (fn) fn(action, f);
		});
	}
	bind('epc_inv_form_wh', 'inv_create_warehouse');
	bind('epc_inv_form_item', 'inv_create_item');
	bind('epc_inv_form_transfer', 'inv_transfer');
	bind('epc_inv_form_move', 'inv_record_movement');
	bind('epc_inv_form_close', 'inv_run_closing');
	var scanForm = document.getElementById('epc_inv_form_scan');
	if (scanForm) {
		scanForm.addEventListener('submit', function(e){
			e.preventDefault();
			var code = (document.getElementById('epc_inv_scan_code') || {}).value || '';
			var out = document.getElementById('epc_inv_scan_out');
			var fd = new FormData();
			fd.append('action', 'inv_scan_lookup');
			fd.append('code', code);
			fd.append('csrf_guard_key', <?php echo json_encode($csrf); ?>);
			fetch(<?php echo json_encode($erpAjaxEndpoint); ?>, { method:'POST', body:fd, credentials:'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){
					if (j.status && j.item) {
						out.innerHTML = '<span class="label label-success">'+j.item.sku+'</span> '+j.item.name+
							' &middot; on hand: <strong>'+Number(j.on_hand||0).toFixed(3)+'</strong>'+
							(j.item.barcode ? ' &middot; barcode '+j.item.barcode : '');
						var mv = document.querySelector('#epc_inv_form_move select[name="item_id"]');
						if (mv) mv.value = j.item.id;
					} else { out.innerHTML = '<span class="text-danger">'+(j.message||'Not found')+'</span>'; }
				});
		});
	}
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
