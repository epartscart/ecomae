<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';

if (!epc_erp_has_commerce_integration()) {
	erp_page_header(
		'<i class="fa fa-truck"></i> Delivery notes',
		'Delivery notes linked to storefront orders are not used for ERP-only tenants. Issue invoices from Sales orders instead.',
		array(
			array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
			array('label' => 'Delivery notes'),
		),
		array(
			array('label' => 'Sales orders', 'url' => epc_erp_tab_url($erpUrl, 'sales_orders', $date_from_str, $date_to_str, 'sales'), 'class' => 'btn-primary', 'icon' => 'fa-shopping-cart'),
		)
	);
	return;
}

$notes = epc_erp_delivery_notes_list($db_link, $date_from, $date_to);

erp_page_header(
	'<i class="fa fa-truck"></i> Delivery notes',
	'Shipment documents linked to shop orders — generated from fulfilment.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Fulfilment', 'url' => epc_erp_tab_url($erpUrl, 'fulfilment', $date_from_str, $date_to_str)),
		array('label' => 'Delivery notes'),
	)
);
erp_stat_cards(array(
	array('label' => 'Notes in period', 'value' => (string) count($notes)),
	array('label' => 'Shipped', 'value' => (string) count(array_filter($notes, function ($n) {
		return ($n['status'] ?? '') === 'shipped';
	}))),
));
ob_start();
if (empty($notes)) {
	erp_empty_state('No delivery notes in this period. Create one from an order below or via Fulfilment.', 'fa-truck');
} else {
	erp_table_open(array('Note #', 'Order', 'Customer', 'Carrier', 'Tracking', 'Status', 'PDF'));
	foreach ($notes as $n) {
		echo '<tr><td>' . epc_erp_h($n['note_no']) . '</td><td>#' . (int) $n['order_id'] . '</td>';
		echo '<td>' . epc_erp_h($n['customer_email'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_h($n['carrier'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_h($n['tracking_no'] ?: '—') . '</td>';
		echo '<td><span class="label label-info">' . epc_erp_h($n['status']) . '</span></td>';
		echo '<td>' . (!empty($n['pdf_path']) ? '<a href="' . epc_erp_h($n['pdf_path']) . '" target="_blank">View</a>' : '—') . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Delivery notes', ob_get_clean(), array('icon' => 'fa-list'));
ob_start();
?>
<form id="epc_erp_form_delivery_note" class="form-horizontal" style="max-width:720px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Order ID</label><div class="col-sm-9"><input type="number" name="order_id" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Carrier / tracking</label><div class="col-sm-9 form-inline">
		<input name="carrier" class="form-control input-sm" placeholder="Carrier">
		<input name="tracking_no" class="form-control input-sm" placeholder="Tracking"></div></div>
	<div class="form-group"><label class="col-sm-3">Notes</label><div class="col-sm-9"><textarea name="notes" class="form-control input-sm" rows="2"></textarea></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><label><input type="checkbox" name="mark_shipped" value="1"> Mark shipped now</label></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Create delivery note</button></div></div>
</form>
<?php
erp_section_card('New delivery note', ob_get_clean(), array('icon' => 'fa-plus'));
