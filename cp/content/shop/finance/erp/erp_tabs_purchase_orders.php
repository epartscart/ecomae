<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$poStatus = isset($_GET['po_status']) ? (string) $_GET['po_status'] : '';
$pos = epc_erp_po_list($db_link, $poStatus);
if (!isset($suppliers)) {
	$suppliers = epc_erp_list_suppliers($db_link);
}

erp_page_header(
	'<i class="fa fa-clipboard"></i> Purchase orders',
	'Draft → approved → received workflow before supplier invoice.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Purchase orders'),
	),
	array(
		array('label' => 'New purchase order', 'icon' => 'fa-plus', 'class' => 'btn-primary', 'url' => '#epc_erp_form_po'),
	)
);
erp_stat_cards(array(
	array('label' => 'Open POs', 'value' => (string) count(array_filter($pos, function ($p) {
		return in_array($p['status'] ?? '', array('draft', 'approved', 'partial'), true);
	}))),
	array('label' => 'Received', 'value' => (string) count(array_filter($pos, function ($p) {
		return ($p['status'] ?? '') === 'received';
	}))),
));
erp_filter_bar($erpUrl, 'purchase_orders', $date_from_str, $date_to_str,
	'<label>Status</label> <select name="po_status" class="form-control input-sm"><option value="">All</option>'
	. '<option value="draft">Draft</option><option value="approved">Approved</option><option value="received">Received</option></select>'
);
ob_start();
if (empty($pos)) {
	erp_empty_state('No purchase orders yet.', 'fa-clipboard');
} else {
	erp_table_open(array('PO #', 'Supplier', 'Title', 'Total AED', 'Status', 'Actions'));
	foreach ($pos as $p) {
		echo '<tr><td>' . epc_erp_h($p['po_no']) . '</td><td>' . epc_erp_h($p['supplier_name']) . '</td>';
		echo '<td>' . epc_erp_h($p['title']) . '</td><td>' . epc_erp_money($p['total_amount']) . '</td>';
		echo '<td><span class="label label-info">' . epc_erp_h($p['status']) . '</span></td><td class="epc-erp-form-inline">';
		if ($p['status'] === 'draft') {
			echo '<form class="epc-erp-po-status"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="po_id" value="' . (int) $p['id'] . '"><input type="hidden" name="status" value="approved">';
			echo '<button type="submit" class="btn btn-xs btn-success">Approve</button></form>';
		}
		if ($p['status'] === 'approved') {
			echo '<form class="epc-erp-po-status"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="po_id" value="' . (int) $p['id'] . '"><input type="hidden" name="status" value="received">';
			echo '<button type="submit" class="btn btn-xs btn-default">Mark received</button></form> ';
		}
		if (in_array($p['status'], array('approved', 'partial', 'received'), true) && empty($p['purchase_id'])) {
			echo '<form class="epc-erp-po-invoice"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="po_id" value="' . (int) $p['id'] . '">';
			echo '<button type="submit" class="btn btn-xs btn-primary">→ Purchase invoice</button></form>';
		} elseif (!empty($p['purchase_id'])) {
			echo '<span class="text-muted">PI linked #' . (int) $p['purchase_id'] . '</span>';
		}
		echo '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('PO list', ob_get_clean(), array('icon' => 'fa-list'));
ob_start();
?>
<form id="epc_erp_form_po" class="form-horizontal" style="max-width:760px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Supplier</label><div class="col-sm-9"><select name="supplier_id" class="form-control input-sm" required><option value="">—</option>
	<?php foreach ($suppliers as $s): ?><option value="<?php echo (int) $s['id']; ?>"><?php echo epc_erp_h($s['name']); ?></option><?php endforeach; ?>
	</select></div></div>
	<div class="form-group"><label class="col-sm-3">Title / amount ex VAT</label><div class="col-sm-9 form-inline">
		<input name="title" class="form-control input-sm" required placeholder="Description">
		<input name="amount_ex_vat" type="number" step="0.01" class="form-control input-sm" placeholder="AED ex VAT"></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Create draft PO</button></div></div>
</form>
<?php
erp_section_card('New purchase order', ob_get_clean(), array('icon' => 'fa-plus'));
