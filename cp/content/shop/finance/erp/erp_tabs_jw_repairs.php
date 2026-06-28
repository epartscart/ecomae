<?php
/**
 * Jewellery Repair Management — integrated into Service Management area.
 * Tracks repair lifecycle: received → in_progress → ready → delivered → invoiced.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
epc_jw_ensure_integration_schema($db_link);

$repairStatus = isset($_GET['repair_status']) ? (string)$_GET['repair_status'] : '';
$repairs = epc_jw_repair_list($db_link, $repairStatus, 200);

erp_page_header(
	'<i class="fa fa-wrench"></i> Jewellery repairs',
	'Receive items for repair, track workshop progress, deliver back to customer.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Repairs'),
	),
	array(
		array('label' => 'New repair receipt', 'icon' => 'fa-plus', 'class' => 'btn-primary', 'url' => '#jw_repair_form'),
	)
);
erp_d365_assets();
erp_action_pane_ribbon(array(
	array('label' => 'Repair', 'key' => 'repair', 'active' => true, 'groups' => array(
		array('label' => 'New', 'buttons' => array(
			array('label' => 'Repair receipt', 'icon' => 'fa-plus', 'class' => 'is-primary', 'target' => '#jw_repair_form'),
		)),
		array('label' => 'View', 'buttons' => array(
			array('label' => 'Refresh', 'icon' => 'fa-refresh', 'url' => epc_erp_tab_url($erpUrl, 'jw_repairs', $date_from_str, $date_to_str)),
		)),
	)),
));

$statusCounts = array('received' => 0, 'in_progress' => 0, 'ready' => 0, 'delivered' => 0);
foreach ($repairs as $rp) {
	$st = $rp['status'] ?? '';
	if (isset($statusCounts[$st])) $statusCounts[$st]++;
}
erp_stat_cards(array(
	array('label' => 'Received', 'value' => (string)$statusCounts['received']),
	array('label' => 'In progress', 'value' => (string)$statusCounts['in_progress']),
	array('label' => 'Ready', 'value' => (string)$statusCounts['ready']),
	array('label' => 'Delivered', 'value' => (string)$statusCounts['delivered']),
));
erp_filter_bar($erpUrl, 'jw_repairs', $date_from_str, $date_to_str,
	'<label>Status</label> <select name="repair_status" class="form-control input-sm"><option value="">All</option>'
	. '<option value="received">Received</option><option value="in_progress">In progress</option>'
	. '<option value="ready">Ready</option><option value="delivered">Delivered</option></select>'
);

ob_start();
if (empty($repairs)) {
	erp_empty_state('No repair jobs yet.', 'fa-wrench');
} else {
	erp_table_open(array(
		array('label' => '', 'class' => 'epc-d365-statcol'),
		array('label' => 'Repair #', 'sort' => 'text'),
		array('label' => 'Customer'),
		array('label' => 'Phone'),
		array('label' => 'Item description'),
		array('label' => 'Metal'),
		array('label' => 'Karat'),
		array('label' => 'Wt In (g)', 'class' => 'num'),
		array('label' => 'Repair type'),
		array('label' => 'Est. cost', 'class' => 'num'),
		array('label' => 'Status'),
		'Actions',
	));
	foreach ($repairs as $rp) {
		$tone = ($rp['status'] === 'ready') ? 'ok' : (($rp['status'] === 'received') ? 'warn' : 'neutral');
		echo '<tr>';
		echo '<td class="epc-d365-statcol">' . erp_status_dot($tone) . '</td>';
		echo '<td>' . epc_erp_h($rp['repair_no']) . '</td>';
		echo '<td>' . epc_erp_h($rp['customer_name']) . '</td>';
		echo '<td>' . epc_erp_h($rp['customer_phone']) . '</td>';
		echo '<td>' . epc_erp_h($rp['item_description']) . '</td>';
		echo '<td>' . epc_erp_h($rp['metal_type']) . '</td>';
		echo '<td>' . epc_erp_h($rp['karat']) . '</td>';
		echo '<td class="num">' . number_format((float)$rp['gross_wt_in'], 3) . '</td>';
		echo '<td>' . epc_erp_h($rp['repair_type']) . '</td>';
		echo '<td class="num">' . epc_erp_money($rp['estimated_cost']) . '</td>';
		echo '<td>' . erp_status_pill($rp['status']) . '</td>';
		echo '<td class="epc-erp-form-inline">';
		if ($rp['status'] === 'received') {
			echo '<form class="jw-repair-status"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="repair_id" value="' . (int)$rp['id'] . '"><input type="hidden" name="new_status" value="in_progress">';
			echo '<button type="submit" class="btn btn-xs btn-warning">Start</button></form>';
		}
		if ($rp['status'] === 'in_progress') {
			echo '<form class="jw-repair-status"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="repair_id" value="' . (int)$rp['id'] . '"><input type="hidden" name="new_status" value="ready">';
			echo '<button type="submit" class="btn btn-xs btn-success">Mark ready</button></form>';
		}
		if ($rp['status'] === 'ready') {
			echo '<form class="jw-repair-status"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="repair_id" value="' . (int)$rp['id'] . '"><input type="hidden" name="new_status" value="delivered">';
			echo '<button type="submit" class="btn btn-xs btn-primary">Deliver</button></form>';
		}
		echo '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Repair jobs', ob_get_clean(), array('icon' => 'fa-wrench'));

// New repair form
ob_start();
?>
<form id="jw_repair_form" class="form-horizontal" style="max-width:760px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Customer name</label><div class="col-sm-9"><input name="customer_name" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Phone</label><div class="col-sm-9"><input name="customer_phone" class="form-control input-sm"></div></div>
	<div class="form-group"><label class="col-sm-3">Item description</label><div class="col-sm-9"><input name="item_description" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Metal / Karat</label><div class="col-sm-9 form-inline">
		<select name="metal_type" class="form-control input-sm">
			<option value="Gold">Gold</option><option value="Silver">Silver</option><option value="Platinum">Platinum</option>
		</select>
		<input name="karat" class="form-control input-sm" placeholder="e.g. 22K" style="width:80px;">
	</div></div>
	<div class="form-group"><label class="col-sm-3">Gross / Net weight (g)</label><div class="col-sm-9 form-inline">
		<input name="gross_wt_in" type="number" step="0.001" class="form-control input-sm" placeholder="Gross wt">
		<input name="net_wt_in" type="number" step="0.001" class="form-control input-sm" placeholder="Net wt">
	</div></div>
	<div class="form-group"><label class="col-sm-3">Repair type</label><div class="col-sm-9">
		<select name="repair_type" class="form-control input-sm">
			<option value="Ring Resize">Ring Resize</option><option value="Clasp Repair">Clasp Repair</option>
			<option value="Stone Setting">Stone Setting</option><option value="Polish & Clean">Polish & Clean</option>
			<option value="Chain Repair">Chain Repair</option><option value="Engraving">Engraving</option>
			<option value="Rhodium Plating">Rhodium Plating</option><option value="Other">Other</option>
		</select>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Estimated cost (AED)</label><div class="col-sm-9"><input name="estimated_cost" type="number" step="0.01" class="form-control input-sm"></div></div>
	<div class="form-group"><label class="col-sm-3">Stone details</label><div class="col-sm-9"><textarea name="stone_details" class="form-control input-sm" rows="2"></textarea></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Create repair receipt</button></div></div>
</form>
<?php
erp_fasttab_open('New repair receipt', array('open' => false, 'icon' => 'fa-plus'));
echo ob_get_clean();
erp_fasttab_close();
?>
<script>
(function(){
	document.querySelectorAll('.jw-repair-status').forEach(function(f){
		f.addEventListener('submit',function(e){
			e.preventDefault();
			var fd = new FormData(f);
			fd.append('action','jw_repair_update_status');
			fetch(<?php echo json_encode($erpAjaxEndpoint); ?>,{method:'POST',body:fd,credentials:'same-origin'})
				.then(function(r){return r.json()})
				.then(function(j){if(typeof showMsg==='function')showMsg(!!j.status,j.message);if(j.status)setTimeout(function(){location.reload()},600)});
		});
	});
	var rf=document.getElementById('jw_repair_form');
	if(rf)rf.addEventListener('submit',function(e){
		e.preventDefault();
		var fd=new FormData(rf);
		fd.append('action','jw_repair_create');
		fetch(<?php echo json_encode($erpAjaxEndpoint); ?>,{method:'POST',body:fd,credentials:'same-origin'})
			.then(function(r){return r.json()})
			.then(function(j){if(typeof showMsg==='function')showMsg(!!j.status,j.message);if(j.status)setTimeout(function(){location.reload()},600)});
	});
})();
</script>
