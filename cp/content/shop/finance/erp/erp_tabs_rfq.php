<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$rfqStatus = isset($_GET['rfq_status']) ? (string)$_GET['rfq_status'] : '';
$rfqs = epc_erp_rfq_list($db_link, $rfqStatus);
if (!isset($suppliers)) {
	$suppliers = epc_erp_list_suppliers($db_link);
}

erp_page_header(
	'<i class="fa fa-envelope-o"></i> Supplier RFQ &amp; proposals',
	'Request for quotation workflow — link to suppliers and optional orders.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Payables', 'url' => epc_erp_tab_url($erpUrl, 'payables', $date_from_str, $date_to_str)),
		array('label' => 'RFQ'),
	),
	array(
		array('label' => 'New RFQ', 'icon' => 'fa-plus', 'class' => 'btn-primary', 'url' => '#epc_erp_form_rfq'),
	)
);
erp_filter_bar($erpUrl, 'rfq', $date_from_str, $date_to_str,
	'<label>Status</label> <select name="rfq_status" class="form-control input-sm"><option value="">All</option>'
	. '<option value="draft">Draft</option><option value="sent">Sent</option><option value="quoted">Quoted</option>'
	. '<option value="accepted">Accepted</option><option value="rejected">Rejected</option></select>'
);
ob_start();
if (empty($rfqs)) {
	erp_empty_state('No RFQs yet. Create a supplier proposal below.');
} else {
	erp_table_open(array('RFQ #', 'Title', 'Supplier', 'Est. AED', 'Status', 'Due', 'Order'));
	foreach ($rfqs as $r) {
		echo '<tr><td>' . epc_erp_h($r['rfq_no']) . '</td><td>' . epc_erp_h($r['title']) . '</td>';
		echo '<td>' . epc_erp_h($r['supplier_name'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_money($r['amount_est']) . '</td>';
		echo '<td><span class="label label-info">' . epc_erp_h($r['status']) . '</span></td>';
		echo '<td>' . ((int)$r['due_date'] > 0 ? epc_erp_h(date('Y-m-d', (int)$r['due_date'])) : '—') . '</td>';
		echo '<td>' . ((int)$r['order_id'] > 0 ? '#' . (int)$r['order_id'] : '—') . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('RFQ list', ob_get_clean(), array('icon' => 'fa-list'));
ob_start();
?>
<form id="epc_erp_form_rfq" class="form-horizontal" style="max-width:800px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Title</label><div class="col-sm-9"><input name="title" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Supplier</label><div class="col-sm-9"><select name="supplier_id" class="form-control input-sm"><option value="0">—</option>
	<?php foreach ($suppliers as $s): ?><option value="<?php echo (int)$s['id']; ?>"><?php echo epc_erp_h($s['name']); ?></option><?php endforeach; ?>
	</select></div></div>
	<div class="form-group"><label class="col-sm-3">Est. amount / Due</label><div class="col-sm-9 form-inline">
		<input name="amount_est" type="number" step="0.01" class="form-control input-sm" placeholder="AED">
		<input name="due_date" type="date" class="form-control input-sm"></div></div>
	<div class="form-group"><label class="col-sm-3">Order ID</label><div class="col-sm-9"><input name="order_id" type="number" class="form-control input-sm"></div></div>
	<div class="form-group"><label class="col-sm-3">Description</label><div class="col-sm-9"><textarea name="description" class="form-control input-sm" rows="3"></textarea></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Create RFQ</button></div></div>
</form>
<?php
erp_section_card('New RFQ', ob_get_clean(), array('icon' => 'fa-plus'));
