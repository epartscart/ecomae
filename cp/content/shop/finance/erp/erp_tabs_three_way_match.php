<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$rows = epc_erp_three_way_match_rows($db_link);

erp_page_header(
	'<i class="fa fa-check-square-o"></i> 3-way match',
	'Compare purchase order, goods receipt, and supplier invoice before payment.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => '3-way match'),
	)
);
erp_stat_cards(array(
	array('label' => 'POs to review', 'value' => (string) count($rows)),
));
ob_start();
if (empty($rows)) {
	erp_empty_state('No approved POs awaiting match. Approve POs and record purchases to see hints here.', 'fa-check-square-o');
} else {
	erp_table_open(array('PO', 'PO total', 'Receipts', 'Invoice', 'Invoice total', 'Match hint'));
	foreach ($rows as $r) {
		$hint = 'Pending';
		$cls = 'label-warning';
		if ((int) $r['receipt_count'] > 0 && (int) $r['purchase_id'] > 0) {
			$poT = (float) $r['po_total'];
			$invT = (float) $r['invoice_total'];
			if (abs($poT - $invT) < 0.02) {
				$hint = 'Matched';
				$cls = 'label-success';
			} else {
				$hint = 'Variance ' . epc_erp_money($invT - $poT);
				$cls = 'label-danger';
			}
		} elseif ((int) $r['receipt_count'] === 0) {
			$hint = 'Awaiting receipt';
		} elseif ((int) $r['purchase_id'] === 0) {
			$hint = 'Awaiting invoice';
		}
		echo '<tr><td>' . epc_erp_h($r['po_no']) . '</td><td>' . epc_erp_money($r['po_total']) . '</td>';
		echo '<td>' . (int) $r['receipt_count'] . '</td>';
		echo '<td>' . epc_erp_h($r['invoice_number'] ?: '—') . '</td>';
		echo '<td>' . ((int) $r['purchase_id'] > 0 ? epc_erp_money($r['invoice_total']) : '—') . '</td>';
		echo '<td><span class="label ' . $cls . '">' . epc_erp_h($hint) . '</span></td></tr>';
	}
	erp_table_close();
}
erp_section_card('Match overview', ob_get_clean(), array('icon' => 'fa-table'));
