<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_audit.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$filterAction = isset($_GET['audit_action']) ? (string)$_GET['audit_action'] : '';
$rows = epc_erp_audit_list($db_link, $filterAction);

erp_page_header(
	'<i class="fa fa-history"></i> Audit trail',
	'Immutable log of key ERP actions: purchases, GL, bank reconciliation, CRM, RFQ.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Audit'),
	)
);
erp_filter_bar($erpUrl, 'audit', $date_from_str, $date_to_str,
	'<label>Action</label> <input type="text" name="audit_action" class="form-control input-sm" value="' . epc_erp_h($filterAction) . '" placeholder="e.g. purchase_create">'
);
erp_stat_cards(array(array('label' => 'Events shown', 'value' => (string)count($rows))));
ob_start();
if (empty($rows)) {
	erp_empty_state('No audit events yet. Actions such as purchases, GL posts, and bank matches appear here.', 'fa-history');
} else {
	erp_table_open(array('When', 'Action', 'Entity', 'Summary', 'Admin'));
	foreach ($rows as $r) {
		echo '<tr><td>' . epc_erp_h(date('Y-m-d H:i', (int)$r['time'])) . '</td>';
		echo '<td><code>' . epc_erp_h($r['action']) . '</code></td>';
		echo '<td>' . epc_erp_h($r['entity_type'] . ($r['entity_id'] ? ' #' . (int)$r['entity_id'] : '')) . '</td>';
		echo '<td>' . epc_erp_h($r['summary']) . '</td>';
		echo '<td>' . (int)$r['admin_id'] . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Recent activity', ob_get_clean(), array('icon' => 'fa-list'));
