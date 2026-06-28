<?php
/**
 * Jewellery Repair Management — Suntech ef-window style.
 * Tracks repair lifecycle: received → in_progress → ready → delivered → invoiced.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
include __DIR__ . '/erp_entry_form_css.php';
epc_jw_ensure_integration_schema($db_link);

$repairStatus = isset($_GET['repair_status']) ? (string)$_GET['repair_status'] : '';
$repairs = epc_jw_repair_list($db_link, $repairStatus, 200);
$csrfLocal = isset($csrf) ? $csrf : '';
$erpAjaxEndpoint = isset($erpAjaxUrl) ? $erpAjaxUrl : '';

$statusCounts = array('received' => 0, 'in_progress' => 0, 'ready' => 0, 'delivered' => 0);
foreach ($repairs as $rp) {
	$st = $rp['status'] ?? '';
	if (isset($statusCounts[$st])) $statusCounts[$st]++;
}

erp_page_header(
	'<i class="fa fa-wrench"></i> Jewellery repairs',
	'Receive items for repair, track workshop progress, deliver back to customer.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Service management'),
		array('label' => 'Repairs'),
	)
);

$statusColors = array('received' => '#e65100', 'in_progress' => '#1565c0', 'ready' => '#2e7d32', 'delivered' => '#6a1b9a');
?>
<div style="display:flex;gap:10px;margin-bottom:12px;flex-wrap:wrap">
	<?php foreach ($statusCounts as $st => $cnt):
		$col = $statusColors[$st] ?? '#666';
		$label = ucfirst(str_replace('_', ' ', $st));
	?>
	<div style="flex:1;min-width:120px;padding:10px 14px;background:#f0f4f7;border:1px solid #8faabc;border-left:4px solid <?php echo $col; ?>;border-radius:3px;text-align:center">
		<div style="font-size:22px;font-weight:700;color:<?php echo $col; ?>"><?php echo $cnt; ?></div>
		<div style="font-size:11px;color:#4a6a7a;font-weight:600"><?php echo $label; ?></div>
	</div>
	<?php endforeach; ?>
</div>

<div class="ef-window">
	<div class="ef-title"><i class="fa fa-wrench"></i> Repair Jobs</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_repair_form_box').style.display='block'"><i class="fa fa-plus"></i> New Repair</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
		<span style="flex:1"></span>
		<label style="font-size:11px;font-weight:600;color:#4a6a7a;margin:0">Status:</label>
		<select onchange="window.location.href=this.value" style="font-size:11px;padding:2px 4px;border:1px solid #8fb8cc;background:#eaf6fb;border-radius:2px">
			<option value="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'jw_repairs', $date_from_str, $date_to_str)); ?>"<?php echo $repairStatus === '' ? ' selected' : ''; ?>>All</option>
			<option value="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'jw_repairs', $date_from_str, $date_to_str) . '&repair_status=received'); ?>"<?php echo $repairStatus === 'received' ? ' selected' : ''; ?>>Received</option>
			<option value="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'jw_repairs', $date_from_str, $date_to_str) . '&repair_status=in_progress'); ?>"<?php echo $repairStatus === 'in_progress' ? ' selected' : ''; ?>>In progress</option>
			<option value="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'jw_repairs', $date_from_str, $date_to_str) . '&repair_status=ready'); ?>"<?php echo $repairStatus === 'ready' ? ' selected' : ''; ?>>Ready</option>
			<option value="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'jw_repairs', $date_from_str, $date_to_str) . '&repair_status=delivered'); ?>"<?php echo $repairStatus === 'delivered' ? ' selected' : ''; ?>>Delivered</option>
		</select>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Repair #</th><th>Customer</th><th>Phone</th>
				<th>Item</th><th>Metal</th><th>Karat</th>
				<th style="text-align:right">Wt In (g)</th><th>Repair Type</th>
				<th style="text-align:right">Est. Cost</th><th>Status</th><th>Actions</th>
			</tr></thead>
			<tbody>
			<?php if (empty($repairs)): ?>
				<tr><td colspan="12" style="text-align:center;color:#999">No repair jobs yet</td></tr>
			<?php else: $n=1; foreach ($repairs as $rp):
				$stCol = $statusColors[$rp['status']] ?? '#666';
			?>
				<tr>
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($rp['repair_no']); ?></strong></td>
					<td><?php echo epc_erp_h($rp['customer_name']); ?></td>
					<td><?php echo epc_erp_h($rp['customer_phone']); ?></td>
					<td><?php echo epc_erp_h($rp['item_description']); ?></td>
					<td><?php echo epc_erp_h($rp['metal_type']); ?></td>
					<td><?php echo epc_erp_h($rp['karat']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$rp['gross_wt_in'], 3); ?></td>
					<td><?php echo epc_erp_h($rp['repair_type']); ?></td>
					<td style="text-align:right"><?php echo epc_erp_money($rp['estimated_cost']); ?></td>
					<td><span style="display:inline-block;padding:1px 8px;border-radius:3px;font-size:10px;font-weight:600;background:<?php echo $stCol; ?>;color:#fff"><?php echo ucfirst(str_replace('_',' ',$rp['status'])); ?></span></td>
					<td>
						<?php if ($rp['status'] === 'received'): ?>
						<form class="jw-repair-status" style="display:inline"><input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
							<input type="hidden" name="repair_id" value="<?php echo (int)$rp['id']; ?>"><input type="hidden" name="new_status" value="in_progress">
							<button type="submit" class="btn btn-xs btn-warning">Start</button></form>
						<?php elseif ($rp['status'] === 'in_progress'): ?>
						<form class="jw-repair-status" style="display:inline"><input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
							<input type="hidden" name="repair_id" value="<?php echo (int)$rp['id']; ?>"><input type="hidden" name="new_status" value="ready">
							<button type="submit" class="btn btn-xs btn-success">Mark ready</button></form>
						<?php elseif ($rp['status'] === 'ready'): ?>
						<form class="jw-repair-status" style="display:inline"><input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
							<input type="hidden" name="repair_id" value="<?php echo (int)$rp['id']; ?>"><input type="hidden" name="new_status" value="delivered">
							<button type="submit" class="btn btn-xs btn-primary">Deliver</button></form>
						<?php endif; ?>
					</td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_repair_form_box" style="display:none;margin-top:12px;">
			<div class="ef-section">
				<span class="ef-section-title">New Repair Receipt</span>
				<form id="jw_repair_form">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<div class="ef-row">
						<div class="ef-field ef-field-wide"><label>Customer Name</label><input name="customer_name" required></div>
						<div class="ef-field"><label>Phone</label><input name="customer_phone"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field ef-field-wide"><label>Item Description</label><input name="item_description" required></div>
						<div class="ef-field"><label>Metal</label>
							<select name="metal_type"><option value="Gold">Gold</option><option value="Silver">Silver</option><option value="Platinum">Platinum</option></select>
						</div>
						<div class="ef-field"><label>Karat</label><input name="karat" placeholder="22K" style="width:60px"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Gross Wt (g)</label><input name="gross_wt_in" type="number" step="0.001" value="0.000"></div>
						<div class="ef-field"><label>Net Wt (g)</label><input name="net_wt_in" type="number" step="0.001" value="0.000"></div>
						<div class="ef-field"><label>Repair Type</label>
							<select name="repair_type">
								<option value="Ring Resize">Ring Resize</option><option value="Clasp Repair">Clasp Repair</option>
								<option value="Stone Setting">Stone Setting</option><option value="Polish & Clean">Polish &amp; Clean</option>
								<option value="Chain Repair">Chain Repair</option><option value="Engraving">Engraving</option>
								<option value="Rhodium Plating">Rhodium Plating</option><option value="Other">Other</option>
							</select>
						</div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Est. Cost (AED)</label><input name="estimated_cost" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field ef-field-wide"><label>Stone Details</label><input name="stone_details"></div>
					</div>
					<div class="ef-actions">
						<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Create</button>
						<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_repair_form_box').style.display='none'">Cancel</button>
					</div>
				</form>
			</div>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Total repairs: <?php echo count($repairs); ?></span>
	</div>
</div>
<script>
(function(){
	var ajaxUrl = <?php echo json_encode($erpAjaxEndpoint); ?>;
	document.querySelectorAll('.jw-repair-status').forEach(function(f){
		f.addEventListener('submit',function(e){
			e.preventDefault();
			var fd = new FormData(f);
			fd.append('action','jw_repair_update_status');
			fetch(ajaxUrl,{method:'POST',body:fd,credentials:'same-origin'})
				.then(function(r){return r.json()})
				.then(function(j){if(j.status)setTimeout(function(){location.reload()},400)});
		});
	});
	var rf=document.getElementById('jw_repair_form');
	if(rf)rf.addEventListener('submit',function(e){
		e.preventDefault();
		var fd=new FormData(rf);
		fd.append('action','jw_repair_create');
		fetch(ajaxUrl,{method:'POST',body:fd,credentials:'same-origin'})
			.then(function(r){return r.json()})
			.then(function(j){if(j.status)setTimeout(function(){location.reload()},400)});
	});
})();
</script>
