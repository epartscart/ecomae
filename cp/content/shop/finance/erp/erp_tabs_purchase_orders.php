<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
epc_jw_ensure_integration_schema($db_link);
$epcJwMode = epc_jw_is_jewellery_tenant($db_link);

$poStatus = isset($_GET['po_status']) ? (string) $_GET['po_status'] : '';
$poListLimit = max(50, min(2000, (int) ($_GET['po_limit'] ?? 100)));
$pos = epc_erp_po_list($db_link, $poStatus, $poListLimit);
if (!isset($suppliers)) {
	$suppliers = epc_erp_list_suppliers($db_link);
}
$epcPoDimMap = epc_erp_dim_load_bulk($db_link, 'purchase_order', array_column($pos, 'id'));

// Bulk-load structured line items for the listed POs (POs created via the "Add line"
// grid). Legacy lump-sum POs simply have no rows here and keep the old single-click
// approve/receive flow untouched.
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
epc_erp_order_fulfillment_ensure_schema($db_link);
$epcPoLinesMap = array();
if (!empty($pos)) {
	$epcPoIdsForLines = array_column($pos, 'id');
	$epcPoLinesPh = implode(',', array_fill(0, count($epcPoIdsForLines), '?'));
	$epcPoLinesSt = $db_link->prepare(
		'SELECT * FROM `epc_erp_po_lines` WHERE `po_id` IN (' . $epcPoLinesPh . ') ORDER BY `po_id`, `line_no`'
	);
	$epcPoLinesSt->execute($epcPoIdsForLines);
	foreach ($epcPoLinesSt->fetchAll(PDO::FETCH_ASSOC) as $epcPoLineRow) {
		$epcPoLinesMap[(int) $epcPoLineRow['po_id']][] = $epcPoLineRow;
	}
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
erp_d365_assets();
erp_action_pane_ribbon(array(
	array('label' => 'Purchase order', 'key' => 'po', 'active' => true, 'groups' => array(
		array('label' => 'New', 'buttons' => array(
			array('label' => 'Purchase order', 'icon' => 'fa-plus', 'class' => 'is-primary', 'target' => '#epc_erp_form_po'),
		)),
		array('label' => 'View', 'buttons' => array(
			array('label' => 'Refresh', 'icon' => 'fa-refresh', 'url' => epc_erp_tab_url($erpUrl, 'purchase_orders', $date_from_str, $date_to_str)),
		)),
	)),
	array('label' => 'Purchase', 'key' => 'purch', 'groups' => array(
		array('label' => 'Actions', 'buttons' => array(
			array('label' => 'Approve', 'icon' => 'fa-check', 'target' => '#epc_erp_po_tbl'),
			array('label' => 'Receive', 'icon' => 'fa-download', 'target' => '#epc_erp_po_tbl'),
		)),
	)),
	array('label' => 'Invoice', 'key' => 'inv', 'groups' => array(
		array('label' => 'Generate', 'buttons' => array(
			array('label' => 'Generate invoice', 'icon' => 'fa-file-text-o', 'class' => 'is-primary', 'target' => '#epc_erp_po_tbl'),
		)),
	)),
));
erp_tabstrip(array(
	array('label' => 'Lines', 'target' => '#epc_erp_po_lines', 'active' => true, 'icon' => 'fa-list'),
	array('label' => 'Header', 'target' => '#epc_erp_po_header', 'icon' => 'fa-id-card-o'),
), 'epc_erp_po_view');
erp_tabpanel_open('epc_erp_po_lines', 'epc_erp_po_view', true);
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
erp_list_toolbar(array(
	'views' => array('My view', 'All purchase orders'),
	'search' => array('placeholder' => 'Filter list', 'target' => '#epc_erp_po_tbl'),
));
ob_start();
if (empty($pos)) {
	erp_empty_state('No purchase orders yet.', 'fa-clipboard');
} else {
	$epcPoCols = array(
		array('label' => '', 'class' => 'epc-d365-statcol'),
		array('label' => 'PO #', 'sort' => 'text'),
		array('label' => 'Supplier', 'sort' => 'text'),
		array('label' => 'Title', 'sort' => 'text'),
		array('label' => 'Total AED', 'sort' => 'num', 'class' => 'num'),
		'Dimensions',
	);
	if ($epcJwMode) {
		$epcPoCols[] = array('label' => 'Karat');
		$epcPoCols[] = array('label' => 'Weight (g)', 'class' => 'num');
		$epcPoCols[] = array('label' => 'Rate/g', 'class' => 'num');
	}
	$epcPoCols[] = array('label' => 'Status', 'sort' => 'text');
	$epcPoCols[] = 'Lines';
	$epcPoCols[] = 'Actions';
	erp_table_open($epcPoCols, 'table table-bordered table-condensed table-epc epc-erp-table', 'epc_erp_po_tbl');
	$epcPoSum = 0.0;
	foreach ($pos as $p) {
		$epcPoSum += (float) $p['total_amount'];
		$epcPoId = (int) $p['id'];
		$epcPoLines = $epcPoLinesMap[$epcPoId] ?? array();
		echo '<tr><td class="epc-d365-statcol">' . erp_status_dot(erp_status_tone($p['status'])) . '</td>';
		echo '<td>' . epc_erp_h($p['po_no']) . '</td><td>' . epc_erp_h($p['supplier_name']) . '</td>';
		echo '<td>' . epc_erp_h($p['title']) . '</td><td class="num">' . epc_erp_money($p['total_amount']) . '</td>';
		echo '<td>' . epc_erp_dim_badges_render($db_link, $epcPoDimMap[$epcPoId] ?? array()) . '</td>';
		if ($epcJwMode) {
			echo '<td>' . epc_erp_h($p['jw_karat'] ?? '') . '</td>';
			echo '<td class="num">' . epc_erp_h(number_format((float)($p['jw_metal_weight_gm'] ?? 0), 3)) . '</td>';
			echo '<td class="num">' . epc_erp_h(number_format((float)($p['jw_rate_per_gram'] ?? 0), 2)) . '</td>';
		}
		echo '<td>' . erp_status_pill($p['status']) . '</td>';
		if ($epcPoLines) {
			$epcPoQtyTotal = array_sum(array_column($epcPoLines, 'qty'));
			$epcPoQtyRecv = array_sum(array_column($epcPoLines, 'qty_received'));
			echo '<td><a href="#" class="epc-erp-po-lines-toggle" data-po="' . $epcPoId . '">'
				. count($epcPoLines) . ' line' . (count($epcPoLines) === 1 ? '' : 's') . ' &middot; '
				. number_format($epcPoQtyRecv, 0) . '/' . number_format($epcPoQtyTotal, 0) . ' received</a></td>';
		} else {
			echo '<td class="text-muted">—</td>';
		}
		echo '<td class="epc-erp-form-inline">';
		if ($p['status'] === 'draft') {
			echo '<form class="epc-erp-po-status"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="po_id" value="' . $epcPoId . '"><input type="hidden" name="status" value="approved">';
			echo '<button type="submit" class="btn btn-xs btn-success">Approve</button></form>';
		}
		if (in_array($p['status'], array('approved', 'partial'), true)) {
			if ($epcPoLines) {
				echo '<button type="button" class="btn btn-xs btn-default epc-erp-po-lines-toggle" data-po="' . $epcPoId . '">Receive</button> ';
			} else {
				echo '<form class="epc-erp-po-status"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
				echo '<input type="hidden" name="po_id" value="' . $epcPoId . '"><input type="hidden" name="status" value="received">';
				echo '<button type="submit" class="btn btn-xs btn-default">Mark received</button></form> ';
			}
		}
		if (in_array($p['status'], array('approved', 'partial', 'received'), true) && empty($p['purchase_id'])) {
			echo '<form class="epc-erp-po-invoice"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="po_id" value="' . $epcPoId . '">';
			echo '<button type="submit" class="btn btn-xs btn-primary">→ Purchase invoice</button></form>';
		} elseif (!empty($p['purchase_id'])) {
			echo '<span class="text-muted">PI linked #' . (int) $p['purchase_id'] . '</span>';
		}
		echo '</td></tr>';
		if ($epcPoLines) {
			$epcPoCanReceive = in_array($p['status'], array('approved', 'partial'), true);
			echo '<tr class="epc-erp-po-lines-row" id="epc_erp_po_lines_row_' . $epcPoId . '" style="display:none;"><td></td><td colspan="' . (count($epcPoCols) - 1) . '">';
			echo '<form class="epc-erp-po-receive" data-po="' . $epcPoId . '"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">';
			echo '<input type="hidden" name="po_id" value="' . $epcPoId . '"><input type="hidden" name="received_json" class="epc-erp-po-received-json">';
			echo '<table class="table table-condensed" style="margin:6px 0;background:#fff;"><thead><tr>'
				. '<th>Description</th><th class="num">Qty</th><th class="num">Unit cost</th><th class="num">Received</th>'
				. ($epcPoCanReceive ? '<th class="num">Receive now</th>' : '') . '</tr></thead><tbody>';
			foreach ($epcPoLines as $epcPl) {
				$epcPlMax = max(0.0, (float) $epcPl['qty'] - (float) $epcPl['qty_cancelled']);
				echo '<tr><td>' . epc_erp_h($epcPl['description']) . '</td>';
				echo '<td class="num">' . number_format((float) $epcPl['qty'], 3) . '</td>';
				echo '<td class="num">' . epc_erp_money($epcPl['unit_cost_ex_vat']) . '</td>';
				echo '<td class="num">' . number_format((float) $epcPl['qty_received'], 3) . ' / ' . number_format($epcPlMax, 3) . '</td>';
				if ($epcPoCanReceive) {
					echo '<td class="num"><input type="number" step="0.001" min="0" max="' . epc_erp_h((string) $epcPlMax) . '"'
						. ' class="form-control input-sm epc-erp-po-recv-input" data-line="' . (int) $epcPl['id'] . '"'
						. ' style="width:100px;display:inline-block;" value="' . epc_erp_h((string) $epcPl['qty_received']) . '"></td>';
				}
				echo '</tr>';
			}
			echo '</tbody></table>';
			if ($epcPoCanReceive) {
				echo '<button type="submit" class="btn btn-xs btn-success">Save receipt</button> ';
			}
			echo '<button type="button" class="btn btn-xs btn-link epc-erp-po-lines-toggle" data-po="' . $epcPoId . '">Close</button>';
			echo '</form></td></tr>';
		}
	}
	$epcPoFoot = '<tr class="epc-d365-sumrow"><td class="epc-d365-statcol"></td>'
		. '<td colspan="3">Sum (' . count($pos) . ' orders)</td>'
		. '<td class="num">' . epc_erp_money($epcPoSum) . '</td>'
		. '<td colspan="' . (count($epcPoCols) - 5) . '"></td></tr>';
	erp_table_close($epcPoFoot);
	if (count($pos) >= $poListLimit) {
		$epcPoMoreUrl = epc_erp_tab_url($erpUrl, 'purchase_orders', $date_from_str, $date_to_str)
			. '&po_status=' . rawurlencode($poStatus) . '&po_limit=' . (int) min(2000, $poListLimit + 200);
		echo '<p class="text-muted" style="margin-top:8px;"><a href="' . epc_erp_h($epcPoMoreUrl) . '">Show more (currently ' . (int) $poListLimit . ')</a></p>';
	}
}
erp_section_card('PO list', ob_get_clean(), array('icon' => 'fa-list'));
ob_start();
?>
<form id="epc_erp_form_po" class="form-horizontal" style="max-width:760px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Supplier</label><div class="col-sm-9"><select name="supplier_id" class="form-control input-sm" required><option value="">—</option>
	<?php foreach ($suppliers as $s): ?><option value="<?php echo (int) $s['id']; ?>"><?php echo epc_erp_h($s['name']); ?></option><?php endforeach; ?>
	</select></div></div>
	<div class="form-group"><label class="col-sm-3">Title</label><div class="col-sm-9">
		<input name="title" class="form-control input-sm" required placeholder="Description"></div></div>
	<div class="form-group"><label class="col-sm-3">Line items</label><div class="col-sm-9">
		<table class="table table-condensed" id="epc_erp_po_lines_grid" style="margin-bottom:6px;background:#fff;">
			<thead><tr><th>Description</th><th class="num" style="width:90px;">Qty</th><th class="num" style="width:130px;">Unit cost ex VAT</th><th style="width:26px;"></th></tr></thead>
			<tbody></tbody>
		</table>
		<button type="button" class="btn btn-xs btn-default" id="epc_erp_po_add_line"><i class="fa fa-plus"></i> Add line</button>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Amount ex VAT <span class="text-muted">(only if no lines above)</span></label><div class="col-sm-9">
		<input name="amount_ex_vat" type="number" step="0.01" class="form-control input-sm" placeholder="AED ex VAT"></div></div>
	<?php echo epc_erp_dim_render_fields($db_link); ?>
	<?php echo epc_jw_purchase_order_fields_html($db_link); ?>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Create draft PO</button></div></div>
</form>
<script>
(function(){
	var grid = document.getElementById('epc_erp_po_lines_grid');
	var addBtn = document.getElementById('epc_erp_po_add_line');
	if (!grid || !addBtn) return;
	function addRow() {
		var tbody = grid.querySelector('tbody');
		var tr = document.createElement('tr');
		tr.innerHTML = '<td><input type="text" name="po_line_desc[]" class="form-control input-sm"></td>'
			+ '<td><input type="number" step="0.001" min="0" name="po_line_qty[]" class="form-control input-sm" value="1"></td>'
			+ '<td><input type="number" step="0.0001" min="0" name="po_line_unit[]" class="form-control input-sm" value="0"></td>'
			+ '<td><button type="button" class="btn btn-xs btn-danger epc-erp-po-rm-line">&times;</button></td>';
		tbody.appendChild(tr);
	}
	addBtn.addEventListener('click', addRow);
	grid.addEventListener('click', function(ev){
		if (ev.target.classList.contains('epc-erp-po-rm-line')) {
			ev.target.closest('tr').remove();
		}
	});
})();
</script>
<?php
$epcPoFormHtml = ob_get_clean();
erp_fasttab_open('New purchase order', array('open' => false, 'icon' => 'fa-plus'));
erp_tabstrip(array(
	array('label' => 'General', 'target' => '#epc_erp_po_ld_gen', 'active' => true),
	array('label' => 'Setup', 'target' => '#epc_erp_po_ld_setup'),
	array('label' => 'Price and discount', 'target' => '#epc_erp_po_ld_price'),
), 'epc_erp_po_ld', array('variant' => 'sub'));
erp_tabpanel_open('epc_erp_po_ld_gen', 'epc_erp_po_ld', true);
echo $epcPoFormHtml;
erp_tabpanel_close();
erp_tabpanel_open('epc_erp_po_ld_setup', 'epc_erp_po_ld');
echo '<p class="text-muted">Delivery, financial dimensions and approval routing default from the supplier master and the purchasing policy.</p>';
erp_tabpanel_close();
erp_tabpanel_open('epc_erp_po_ld_price', 'epc_erp_po_ld');
echo '<p class="text-muted">Net amount is computed from the amount ex VAT; input VAT is applied per the tenant country profile.</p>';
erp_tabpanel_close();
erp_fasttab_close();
erp_tabpanel_close(); // Lines view

// Header view — "Header" toggle.
erp_tabpanel_open('epc_erp_po_header', 'epc_erp_po_view');
$epcPoHead = !empty($pos) ? $pos[0] : null;
echo '<div class="epc-d365-titleblock"><div><span class="epc-d365-recid">' . epc_erp_h($epcPoHead ? ($epcPoHead['po_no'] ?? 'New PO') : 'New PO') . '</span>'
	. ($epcPoHead ? '<span class="epc-d365-recsub">' . epc_erp_h($epcPoHead['supplier_name'] ?? '') . '</span>' : '') . '</div>'
	. ($epcPoHead ? erp_status_pill($epcPoHead['status']) : '') . '</div>';
erp_fasttab_open('General', array('open' => true, 'icon' => 'fa-id-card-o'));
echo '<div class="epc-d365-subhd">Vendor</div>';
echo '<div class="epc-d365-fieldgrid">'
	. '<div class="epc-d365-field"><span class="lbl">Vendor account</span><span class="val">' . epc_erp_h($epcPoHead ? ($epcPoHead['supplier_name'] ?? '—') : '—') . '</span></div>'
	. '<div class="epc-d365-field"><span class="lbl">Purchase status</span><span class="val">' . ($epcPoHead ? erp_status_pill($epcPoHead['status']) : '—') . '</span></div>'
	. '</div>';
echo '<p class="text-muted" style="margin-top:8px;">Switch back to <strong>Lines</strong> to edit the PO grid. This Header view mirrors the standard purchase order header.</p>';
erp_fasttab_close();
erp_tabpanel_close(); // Header view
