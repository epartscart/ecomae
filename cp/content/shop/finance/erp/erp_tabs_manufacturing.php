<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_manufacturing.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_erp_inventory_ensure_schema($db_link);
epc_mfg_ensure_schema($db_link);

$mfgSummary    = epc_mfg_summary($db_link);
$mfgBoms       = epc_mfg_bom_list($db_link);
$mfgWorkOrders = epc_mfg_wo_list($db_link, 200);
$mfgItems      = epc_erp_inventory_list_items($db_link, 1000);
$mfgWarehouses = epc_erp_inventory_list_warehouses($db_link);
$csrfLocal     = isset($csrf) ? $csrf : '';

erp_page_header(
	'<i class="fa fa-cogs"></i> Manufacturing &mdash; BOM &amp; work orders',
	'Bills of material, work orders, material issue/backflush and finished-goods costing (materials + labour + overhead).',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Manufacturing'),
	)
);

erp_stat_cards(array(
	array('label' => 'Bills of material', 'value' => (string) $mfgSummary['boms']),
	array('label' => 'Open work orders', 'value' => (string) $mfgSummary['wo_open']),
	array('label' => 'Completed', 'value' => (string) $mfgSummary['wo_done']),
	array('label' => 'WIP value', 'value' => epc_erp_money($mfgSummary['wip_value']) . ' AED'),
));
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<?php if (empty($mfgItems)): ?>
	<?php erp_empty_state('Add inventory items first (Operations &rarr; Inventory) — BOM products and components are drawn from your item master.', 'fa-cubes'); ?>
<?php else: ?>

<div class="row">
	<div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-sitemap"></i> 1. Define a bill of material</h5>
			<form id="epc_mfg_bom" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="form-group">
					<label>Finished product</label>
					<select name="product_item_id" class="form-control input-sm" required>
						<option value="">— select item —</option>
						<?php foreach ($mfgItems as $it): ?>
							<option value="<?php echo (int) $it['id']; ?>"><?php echo epc_erp_h($it['sku'] . ' · ' . $it['name']); ?></option>
						<?php endforeach; ?>
					</select>
				</div>
				<div class="row">
					<div class="col-xs-4 form-group"><label>Output qty</label><input type="number" step="0.0001" name="output_qty" class="form-control input-sm" value="1"></div>
					<div class="col-xs-4 form-group"><label>Labour</label><input type="number" step="0.01" name="labour_cost" class="form-control input-sm" value="0"></div>
					<div class="col-xs-4 form-group"><label>Overhead</label><input type="number" step="0.01" name="overhead_cost" class="form-control input-sm" value="0"></div>
				</div>
				<div class="form-group">
					<label>Name / revision (optional)</label>
					<input type="text" name="name" class="form-control input-sm" placeholder="e.g. Assembly v1">
				</div>
				<label>Components</label>
				<table class="table table-condensed" style="margin-bottom:6px;">
					<thead><tr><th>Item</th><th style="width:90px;">Qty/unit</th><th style="width:80px;">Scrap %</th><th style="width:30px;"></th></tr></thead>
					<tbody id="epc_mfg_bom_lines"></tbody>
				</table>
				<button type="button" class="btn btn-default btn-xs" id="epc_mfg_add_line"><i class="fa fa-plus"></i> Add component</button>
				<hr style="margin:10px 0;">
				<button type="submit" class="btn btn-primary btn-sm">Save BOM</button>
			</form>
		</div>

		<div class="well well-sm">
			<h5><i class="fa fa-wrench"></i> 2. Create a work order</h5>
			<form id="epc_mfg_wo" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="form-group">
					<label>Bill of material</label>
					<select name="bom_id" class="form-control input-sm" required>
						<option value="">— select BOM —</option>
						<?php foreach ($mfgBoms as $b): ?>
							<option value="<?php echo (int) $b['id']; ?>"><?php echo epc_erp_h(($b['product_name'] ?: ('BOM #' . $b['id'])) . ($b['name'] ? ' (' . $b['name'] . ')' : '')); ?></option>
						<?php endforeach; ?>
					</select>
				</div>
				<div class="row">
					<div class="col-xs-6 form-group"><label>Qty to build</label><input type="number" step="0.0001" name="qty_planned" class="form-control input-sm" value="1" required></div>
					<div class="col-xs-6 form-group">
						<label>Warehouse</label>
						<select name="warehouse_id" class="form-control input-sm">
							<option value="0">— none —</option>
							<?php foreach ($mfgWarehouses as $w): ?>
								<option value="<?php echo (int) $w['id']; ?>"><?php echo epc_erp_h($w['name']); ?></option>
							<?php endforeach; ?>
						</select>
					</div>
				</div>
				<button type="submit" class="btn btn-primary btn-sm"<?php echo empty($mfgBoms) ? ' disabled' : ''; ?>>Create work order</button>
				<?php if (empty($mfgBoms)): ?><p class="text-muted small" style="margin-top:6px;">Define a BOM first.</p><?php endif; ?>
			</form>
		</div>
	</div>

	<div class="col-md-7">
		<h5>Bills of material</h5>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>#</th><th>Product</th><th>Name</th><th>Output</th><th>Components</th><th>Labour</th><th>Overhead</th></tr></thead>
			<tbody>
			<?php if (empty($mfgBoms)): ?>
				<tr><td colspan="7" class="text-muted">No BOMs yet.</td></tr>
			<?php else: foreach ($mfgBoms as $b): ?>
				<tr>
					<td><?php echo (int) $b['id']; ?></td>
					<td><?php echo epc_erp_h($b['product_name'] ?: ('item #' . $b['product_item_id'])); ?></td>
					<td><?php echo epc_erp_h($b['name']); ?></td>
					<td><?php echo epc_erp_h(number_format((float) $b['output_qty'], 3)); ?></td>
					<td><?php echo (int) ($b['line_count'] ?? 0); ?></td>
					<td><?php echo epc_erp_money($b['labour_cost']); ?></td>
					<td><?php echo epc_erp_money($b['overhead_cost']); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<h5 style="margin-top:18px;">Work orders</h5>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>WO</th><th>Product</th><th>Plan</th><th>Made</th><th>Status</th><th>Cost</th><th>Action</th></tr></thead>
			<tbody>
			<?php if (empty($mfgWorkOrders)): ?>
				<tr><td colspan="7" class="text-muted">No work orders yet.</td></tr>
			<?php else: foreach ($mfgWorkOrders as $w):
				$totCost = (float) $w['material_cost'] + (float) $w['labour_cost'] + (float) $w['overhead_cost'];
				$lbl = $w['status'] === 'completed' ? 'success' : ($w['status'] === 'in_progress' ? 'info' : 'default'); ?>
				<tr>
					<td><strong><?php echo epc_erp_h($w['wo_no']); ?></strong></td>
					<td><?php echo epc_erp_h($w['product_name'] ?: ('item #' . $w['product_item_id'])); ?></td>
					<td><?php echo epc_erp_h(number_format((float) $w['qty_planned'], 3)); ?></td>
					<td><?php echo epc_erp_h(number_format((float) $w['qty_produced'], 3)); ?></td>
					<td><span class="label label-<?php echo $lbl; ?>"><?php echo epc_erp_h($w['status']); ?></span></td>
					<td><?php echo epc_erp_money($totCost); ?></td>
					<td>
						<?php if ($w['status'] === 'planned'): ?>
							<button class="btn btn-default btn-xs epc-mfg-issue" data-id="<?php echo (int) $w['id']; ?>">Issue materials</button>
						<?php elseif ($w['status'] === 'in_progress'): ?>
							<button class="btn btn-success btn-xs epc-mfg-complete" data-id="<?php echo (int) $w['id']; ?>" data-plan="<?php echo epc_erp_h((float) $w['qty_planned']); ?>">Complete</button>
						<?php else: ?>
							<span class="text-muted">—</span>
						<?php endif; ?>
					</td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div>
</div>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	var items = <?php echo json_encode(array_map(function ($i) { return array('id' => (int) $i['id'], 'label' => $i['sku'] . ' · ' . $i['name']); }, $mfgItems)); ?>;
	function post(action, fd) {
		fd.append('action', action);
		return fetch(url, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function(r){ return r.json(); });
	}
	function msg(j) {
		var el = document.getElementById('epc_erp_msg');
		if (!el) return;
		el.className = 'alert alert-' + (j.status ? 'success' : 'danger');
		el.textContent = j.message || '';
		el.style.display = 'block';
		el.scrollIntoView({ behavior: 'smooth', block: 'center' });
	}
	function optionsHtml() {
		var h = '<option value="">— item —</option>';
		items.forEach(function(it){ h += '<option value="' + it.id + '">' + it.label.replace(/</g,'&lt;') + '</option>'; });
		return h;
	}
	function addLine() {
		var tb = document.getElementById('epc_mfg_bom_lines');
		var tr = document.createElement('tr');
		tr.innerHTML = '<td><select class="form-control input-sm epc-mfg-comp">' + optionsHtml() + '</select></td>' +
			'<td><input type="number" step="0.0001" class="form-control input-sm epc-mfg-qty" value="1"></td>' +
			'<td><input type="number" step="0.001" class="form-control input-sm epc-mfg-scrap" value="0"></td>' +
			'<td><button type="button" class="btn btn-link btn-xs epc-mfg-del" style="color:#c00;">&times;</button></td>';
		tb.appendChild(tr);
	}
	var addBtn = document.getElementById('epc_mfg_add_line');
	if (addBtn) { addBtn.addEventListener('click', addLine); addLine(); }
	document.addEventListener('click', function(ev){
		if (ev.target && ev.target.classList.contains('epc-mfg-del')) {
			var row = ev.target.closest('tr'); if (row) row.remove();
		}
	});

	var bomForm = document.getElementById('epc_mfg_bom');
	if (bomForm) bomForm.addEventListener('submit', function(ev){
		ev.preventDefault();
		var fd = new FormData(bomForm);
		var n = 0;
		document.querySelectorAll('#epc_mfg_bom_lines tr').forEach(function(row){
			var comp = row.querySelector('.epc-mfg-comp');
			var qty = row.querySelector('.epc-mfg-qty');
			var scrap = row.querySelector('.epc-mfg-scrap');
			if (comp && comp.value) {
				fd.append('lines[' + n + '][component_item_id]', comp.value);
				fd.append('lines[' + n + '][qty_per]', qty ? qty.value : '0');
				fd.append('lines[' + n + '][scrap_percent]', scrap ? scrap.value : '0');
				n++;
			}
		});
		if (n === 0) { msg({ status:false, message:'Add at least one component.' }); return; }
		post('mfg_bom_save', fd).then(function(j){ msg(j); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
	});

	var woForm = document.getElementById('epc_mfg_wo');
	if (woForm) woForm.addEventListener('submit', function(ev){
		ev.preventDefault();
		post('mfg_wo_create', new FormData(woForm)).then(function(j){ msg(j); if (j.status) setTimeout(function(){ location.reload(); }, 800); });
	});

	document.querySelectorAll('.epc-mfg-issue').forEach(function(btn){
		btn.addEventListener('click', function(){
			if (!confirm('Issue (consume) the BOM components for this work order?')) return;
			var fd = new FormData(); fd.append('csrf_guard_key', csrf); fd.append('wo_id', btn.getAttribute('data-id'));
			post('mfg_wo_issue', fd).then(function(j){ msg(j); if (j.status) setTimeout(function(){ location.reload(); }, 900); });
		});
	});
	document.querySelectorAll('.epc-mfg-complete').forEach(function(btn){
		btn.addEventListener('click', function(){
			var def = btn.getAttribute('data-plan') || '1';
			var q = prompt('Quantity produced (finished goods received):', def);
			if (q === null) return;
			var fd = new FormData(); fd.append('csrf_guard_key', csrf); fd.append('wo_id', btn.getAttribute('data-id')); fd.append('qty_produced', q);
			post('mfg_wo_complete', fd).then(function(j){ msg(j); if (j.status) setTimeout(function(){ location.reload(); }, 900); });
		});
	});
})();
</script>
<?php endif; ?>
