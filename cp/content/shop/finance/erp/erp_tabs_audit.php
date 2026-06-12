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
	erp_table_open(array('When', 'Action', 'Entity', 'Summary', 'Changes (old → new)', 'Admin', 'IP / device'));
	foreach ($rows as $r) {
		$changeHtml = '<span class="text-muted">—</span>';
		$old = !empty($r['old_json']) ? json_decode((string)$r['old_json'], true) : null;
		$new = !empty($r['new_json']) ? json_decode((string)$r['new_json'], true) : null;
		if (is_array($new) && $new) {
			$parts = array();
			foreach ($new as $k => $nv) {
				$ov = is_array($old) && array_key_exists($k, $old) ? $old[$k] : '';
				$parts[] = '<div><code>' . epc_erp_h($k) . '</code>: <span class="text-danger">' . epc_erp_h((string)$ov)
					. '</span> → <span class="text-success">' . epc_erp_h((string)$nv) . '</span></div>';
			}
			$changeHtml = implode('', $parts);
		}
		$ua = (string)($r['user_agent'] ?? '');
		$device = $ua !== '' ? epc_erp_h(mb_substr($ua, 0, 60)) : '';
		$ipDevice = epc_erp_h((string)($r['ip_address'] ?? '')) . ($device !== '' ? '<br><small class="text-muted" title="' . epc_erp_h($ua) . '">' . $device . '</small>' : '');
		echo '<tr><td>' . epc_erp_h(date('Y-m-d H:i', (int)$r['time'])) . '</td>';
		echo '<td><code>' . epc_erp_h($r['action']) . '</code></td>';
		echo '<td>' . epc_erp_h($r['entity_type'] . ($r['entity_id'] ? ' #' . (int)$r['entity_id'] : '')) . '</td>';
		echo '<td>' . epc_erp_h($r['summary']) . '</td>';
		echo '<td style="font-size:11px;">' . $changeHtml . '</td>';
		echo '<td>' . (int)$r['admin_id'] . '</td>';
		echo '<td style="font-size:11px;">' . ($ipDevice !== '' ? $ipDevice : '<span class="text-muted">—</span>') . '</td></tr>';
	}
	erp_table_close();
}
erp_section_card('Recent activity', ob_get_clean(), array('icon' => 'fa-list'));
