<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_audit.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_db_integrity.php';

$filterAction = isset($_GET['audit_action']) ? (string)$_GET['audit_action'] : '';
$rows = epc_erp_audit_list($db_link, $filterAction);
$integrity = epc_erp_integrity_scan($db_link);

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

// ---- Data integrity (referential-integrity scan + guarded FK applier) ----
ob_start();
$cntClean = 0; $cntDirty = 0; $cntExists = 0; $cntMissing = 0;
foreach ($integrity as $r) { ${'cnt' . ucfirst($r['status'])}++; }
echo '<p class="text-muted" style="font-size:12px;">Scans core child→parent relationships for orphaned rows. Foreign keys are applied <strong>only</strong> where the relationship is clean (zero orphans), so existing data is never broken.</p>';
echo '<p><input type="hidden" id="epc_integrity_csrf" value="' . epc_erp_h($csrf) . '">';
echo '<button type="button" class="btn btn-sm btn-info" id="epc_integrity_apply"><i class="fa fa-link"></i> Apply safe foreign keys</button> ';
echo '<span id="epc_integrity_out" style="margin-left:8px;"></span></p>';
echo '<table class="table table-condensed table-bordered" style="font-size:12px;"><thead><tr><th>Child</th><th>Column</th><th>Parent</th><th class="text-right">Orphans</th><th>Status</th></tr></thead><tbody>';
foreach ($integrity as $r) {
	$badge = array('clean' => 'success', 'dirty' => 'danger', 'exists' => 'primary', 'missing' => 'default');
	$lbl = array('clean' => 'clean — ready', 'dirty' => 'orphans found', 'exists' => 'FK active', 'missing' => 'n/a');
	echo '<tr><td><code>' . epc_erp_h($r['child']) . '</code></td><td>' . epc_erp_h($r['col']) . '</td>';
	echo '<td><code>' . epc_erp_h($r['parent']) . '</code></td>';
	echo '<td class="text-right">' . ($r['orphans'] < 0 ? '—' : (int)$r['orphans']) . '</td>';
	echo '<td><span class="label label-' . ($badge[$r['status']] ?? 'default') . '">' . epc_erp_h($lbl[$r['status']] ?? $r['status']) . '</span></td></tr>';
}
echo '</tbody><tfoot><tr><th colspan="5">' . (int)$cntExists . ' active &middot; ' . (int)$cntClean . ' ready &middot; ' . (int)$cntDirty . ' need cleanup &middot; ' . (int)$cntMissing . ' n/a</th></tr></tfoot></table>';
$integrityHtml = ob_get_clean();
erp_section_card('Database integrity', $integrityHtml, array('icon' => 'fa-shield'));
?>
<script>
(function(){
	var btn = document.getElementById('epc_integrity_apply');
	if (!btn) return;
	btn.addEventListener('click', function(){
		if (!confirm('Apply foreign keys to all clean relationships now?')) return;
		var out = document.getElementById('epc_integrity_out');
		out.innerHTML = 'Applying…';
		var fd = new FormData();
		fd.append('action', 'integrity_apply_fks');
		fd.append('csrf_guard_key', (document.getElementById('epc_integrity_csrf')||{}).value||'');
		fetch(<?php echo json_encode($erpAjaxEndpoint); ?>, { method:'POST', body:fd, credentials:'same-origin' })
			.then(function(r){ return r.json(); })
			.then(function(j){
				out.innerHTML = '<span class="'+(j.status?'text-success':'text-danger')+'">'+(j.message||'')+'</span>';
				if (j.status) setTimeout(function(){ location.reload(); }, 1400);
			});
	});
})();
</script>
<?php
