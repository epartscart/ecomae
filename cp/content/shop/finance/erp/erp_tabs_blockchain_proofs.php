<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$siteKey = epc_bc_bos_resolve_site_key();
$mode = $siteKey !== '' ? epc_bc_bos_tenant_mode($siteKey) : 'off';
$filterType = isset($_GET['bc_type']) ? strtolower(trim((string)$_GET['bc_type'])) : '';
$filterStatus = isset($_GET['bc_status']) ? strtolower(trim((string)$_GET['bc_status'])) : '';
$allowedTypes = array('invoice', 'credit_note', 'grn', 'rma');
if ($filterType !== '' && !in_array($filterType, $allowedTypes, true)) {
	$filterType = '';
}
if ($filterStatus !== '' && !in_array($filterStatus, array('pending', 'anchored'), true)) {
	$filterStatus = '';
}

$rows = array();
if ($siteKey !== '' && $mode !== 'off') {
	$rows = epc_bc_bos_list_proofs($siteKey, array(
		'record_type' => $filterType,
		'status' => $filterStatus,
		'limit' => 100,
	));
}

$anchored = 0;
$pending = 0;
foreach ($rows as $r) {
	if (($r['status'] ?? '') === 'anchored') {
		$anchored++;
	} else {
		$pending++;
	}
}

erp_page_header(
	'<i class="fa fa-link"></i> Blockchain proofs',
	'Cryptographic integrity proofs for invoices, credit notes, GRNs and RMAs — part of the Blockchain BOS Enterprise System.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Tax', 'url' => epc_erp_tab_url($erpUrl, 'einvoice', $date_from_str, $date_to_str)),
		array('label' => 'Blockchain proofs'),
	)
);

$filterHtml = '<label>Type</label> <select name="bc_type" class="form-control input-sm">'
	. '<option value="">All types</option>';
foreach ($allowedTypes as $t) {
	$sel = $filterType === $t ? ' selected' : '';
	$filterHtml .= '<option value="' . epc_erp_h($t) . '"' . $sel . '>' . epc_erp_h($t) . '</option>';
}
$filterHtml .= '</select> '
	. '<label style="margin-left:8px">Status</label> <select name="bc_status" class="form-control input-sm">'
	. '<option value="">All</option>'
	. '<option value="pending"' . ($filterStatus === 'pending' ? ' selected' : '') . '>pending</option>'
	. '<option value="anchored"' . ($filterStatus === 'anchored' ? ' selected' : '') . '>anchored</option>'
	. '</select>';

erp_filter_bar($erpUrl, 'blockchain_proofs', $date_from_str, $date_to_str, $filterHtml);

if ($siteKey === '') {
	echo '<div class="alert alert-warning">Tenant context not resolved — proofs cannot be listed for this session.</div>';
} elseif ($mode === 'off') {
	echo '<div class="alert alert-info">Blockchain mode is <strong>off</strong> for this tenant. Enable <code>blockchain_mode=anchor</code> in Super CP onboard to record proofs.</div>';
} else {
	erp_stat_cards(array(
		array('label' => 'Shown', 'value' => (string)count($rows)),
		array('label' => 'Anchored', 'value' => (string)$anchored),
		array('label' => 'Pending', 'value' => (string)$pending),
		array('label' => 'Mode', 'value' => strtoupper($mode)),
	));

	ob_start();
	if (empty($rows)) {
		erp_empty_state('No proofs yet. Validated invoices, GRNs and RMAs appear here after create.', 'fa-link');
	} else {
		erp_table_open(array('When', 'Type', 'Record', 'Status', 'Proof ID', 'Anchor', 'Verify'));
		foreach ($rows as $r) {
			$status = (string)($r['status'] ?? 'pending');
			$tone = $status === 'anchored' ? 'success' : 'warning';
			$uid = (string)($r['proof_uid'] ?? '');
			$verify = $uid !== '' ? epc_bc_bos_verify_url($uid) : '';
			$when = !empty($r['created_at']) ? (string)$r['created_at'] : '';
			$anchor = (string)($r['anchor_ref'] ?? '');
			if ($anchor === '' && !empty($r['merkle_root'])) {
				$anchor = (string)$r['merkle_root'];
			}
			echo '<tr>';
			echo '<td>' . epc_erp_h($when) . '</td>';
			echo '<td><code>' . epc_erp_h((string)($r['record_type'] ?? '')) . '</code></td>';
			echo '<td>' . epc_erp_h((string)($r['record_id'] ?? '')) . '</td>';
			echo '<td><span class="label label-' . $tone . '">' . epc_erp_h($status) . '</span></td>';
			echo '<td><small><code>' . epc_erp_h($uid) . '</code></small></td>';
			echo '<td style="font-size:11px;max-width:220px;word-break:break-all">' . ($anchor !== '' ? epc_erp_h($anchor) : '<span class="text-muted">—</span>') . '</td>';
			echo '<td>';
			if ($verify !== '') {
				echo '<a class="btn btn-default btn-xs" href="' . epc_erp_h($verify) . '" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Verify</a>';
			} else {
				echo '<span class="text-muted">—</span>';
			}
			echo '</td></tr>';
		}
		erp_table_close();
	}
	erp_section_card('Recent proofs · ' . epc_erp_h($siteKey), ob_get_clean(), array('icon' => 'fa-list'));

	echo '<p class="text-muted" style="font-size:12px;margin-top:12px">'
		. 'Public verify: <a href="/epc-blockchain-verify.php" target="_blank" rel="noopener"><code>/epc-blockchain-verify.php</code></a>. '
		. 'Anchoring runs via platform job <code>blockchain_anchor_batch</code>.'
		. '</p>';
}
