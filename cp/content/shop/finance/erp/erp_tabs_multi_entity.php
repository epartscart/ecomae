<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_org.php';

$enabled = epc_erp_multi_entity_enabled($db_link);
$org = epc_bos_org_tree($db_link);

erp_page_header(
	'<i class="fa fa-sitemap"></i> Organization structure',
	'Group companies, legal entities, business units, departments, teams and the approval hierarchy — assembled from your live masters.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Organization'),
	)
);

erp_stat_cards(array(
	array('label' => 'Legal entities', 'value' => (string) $org['counts']['entities']),
	array('label' => 'Business units', 'value' => (string) $org['counts']['business_units']),
	array('label' => 'Departments', 'value' => (string) count($org['departments'])),
	array('label' => 'Financial dimensions', 'value' => (string) $org['counts']['dimensions']),
));

/* Recursive business-unit renderer. */
if (!function_exists('epc_org_render_units')) {
	function epc_org_render_units(array $units, $depth = 0)
	{
		if (!$units) { return ''; }
		$h = '<ul style="list-style:none;margin:0 0 0 ' . ($depth ? 18 : 0) . 'px;padding:0;">';
		foreach ($units as $u) {
			$h .= '<li style="padding:3px 0;">'
				. '<i class="fa fa-cube text-muted"></i> <strong>' . epc_erp_h($u['code']) . '</strong> · ' . epc_erp_h($u['name'])
				. ((string) ($u['manager'] ?? '') !== '' ? ' <span class="text-muted">(' . epc_erp_h($u['manager']) . ')</span>' : '');
			if (!empty($u['children'])) { $h .= epc_org_render_units($u['children'], $depth + 1); }
			$h .= '</li>';
		}
		$h .= '</ul>';
		return $h;
	}
}

/* ---- Group / legal-entity → business-unit tree ---- */
ob_start();
if (!$org['entities'] && !$org['orphan_units']) {
	erp_empty_state('No legal entities or business units yet. Add them in the Business Unit module.', 'fa-building-o');
} else {
	foreach ($org['entities'] as $node) {
		$e = $node['entity'];
		echo '<div style="margin-bottom:14px;padding:10px 12px;border:1px solid #e2e8f0;border-radius:6px;">';
		echo '<div style="font-size:14px;"><i class="fa fa-building"></i> <strong>' . epc_erp_h($e['code']) . ' · ' . epc_erp_h($e['name']) . '</strong>';
		$meta = array();
		if ((string) ($e['country_code'] ?? '') !== '') { $meta[] = epc_erp_h($e['country_code']); }
		if ((string) ($e['currency_code'] ?? '') !== '') { $meta[] = epc_erp_h($e['currency_code']); }
		if ((string) ($e['trn'] ?? '') !== '') { $meta[] = 'TRN ' . epc_erp_h($e['trn']); }
		if ($meta) { echo ' <span class="text-muted" style="font-size:12px;">— ' . implode(' · ', $meta) . '</span>'; }
		echo ' <span class="label label-default">' . (int) $node['unit_count'] . ' BU</span></div>';
		echo $node['units'] ? epc_org_render_units($node['units']) : '<p class="text-muted" style="margin:6px 0 0;font-size:12px;">No business units under this entity.</p>';
		echo '</div>';
	}
	if ($org['orphan_units']) {
		echo '<div style="margin-bottom:14px;padding:10px 12px;border:1px dashed #e2e8f0;border-radius:6px;">';
		echo '<div style="font-size:14px;"><i class="fa fa-cubes"></i> <strong>Unassigned business units</strong> <span class="text-muted" style="font-size:12px;">(not linked to a legal entity)</span></div>';
		echo epc_org_render_units($org['orphan_units']);
		echo '</div>';
	}
}
erp_section_card('Group → legal entity → business unit', ob_get_clean(), array('icon' => 'fa-sitemap'));

/* ---- Departments / teams ---- */
ob_start();
echo '<div class="row">';
foreach ($org['departments'] as $d) {
	echo '<div class="col-sm-4" style="margin-bottom:10px;"><div style="padding:10px;border:1px solid #eef2f7;border-radius:6px;border-left:4px solid ' . epc_erp_h($d['color']) . ';">';
	echo '<strong><i class="fa ' . epc_erp_h($d['icon']) . '"></i> ' . epc_erp_h($d['name']) . '</strong> <span class="label label-default">' . (int) $d['staff'] . ' staff</span>';
	if (!empty($d['workflows'])) {
		echo '<div class="text-muted" style="font-size:11px;margin-top:4px;">' . epc_erp_h(implode(' · ', array_slice($d['workflows'], 0, 3))) . '</div>';
	}
	echo '</div></div>';
}
echo '</div>';
erp_section_card('Departments & teams', ob_get_clean(), array('icon' => 'fa-users'));

/* ---- Approval hierarchy ---- */
ob_start();
echo '<table class="table table-condensed table-bordered" style="font-size:12px;"><thead><tr><th>Document type</th><th class="text-right">Approval rules</th><th>Chain</th></tr></thead><tbody>';
foreach ($org['approval_hierarchy'] as $a) {
	$chain = '';
	if ($a['rule_count'] > 0) {
		$bits = array();
		foreach (array_slice($a['rules'], 0, 4) as $r) {
			$lvl = (string) ($r['name'] ?? $r['approver_role'] ?? ('rule #' . (int) ($r['id'] ?? 0)));
			$thr = isset($r['threshold_amount']) && (float) $r['threshold_amount'] > 0 ? ' ≥' . epc_erp_money($r['threshold_amount']) : '';
			$bits[] = epc_erp_h($lvl . $thr);
		}
		$chain = implode(' → ', $bits);
	} else {
		$chain = '<span class="text-muted">auto-approved (no rule)</span>';
	}
	echo '<tr><td>' . epc_erp_h($a['label']) . '</td><td class="text-right">' . (int) $a['rule_count'] . '</td><td>' . $chain . '</td></tr>';
}
echo '</tbody></table>';
echo '<p class="text-muted" style="font-size:11px;">Approval chains are configured in the Approvals / Workflow module.</p>';
erp_section_card('Approval hierarchy', ob_get_clean(), array('icon' => 'fa-check-square-o'));

/* ---- Inter-company / consolidation toggle (existing) ---- */
?>
<div class="epc-erp-section-card">
	<div class="epc-erp-section-card-bd">
		<div class="alert alert-info" style="margin-bottom:12px;">
			<strong>Inter-company / consolidation</strong> across separate tenant databases is a platform-level
			feature on <a href="https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub" target="_blank" rel="noopener">ecomae Super CP</a>.
			Within a single tenant, the structure above and the <em>Consolidation</em> tab cover group reporting by business unit.
		</div>
		<form id="epc_erp_form_multi_entity" class="form-inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<label class="checkbox-inline"><input type="checkbox" name="enabled" value="1"<?php echo $enabled ? ' checked' : ''; ?>> Enable cross-tenant multi-entity mode</label>
			<button type="submit" class="btn btn-default btn-sm">Save preference</button>
		</form>
	</div>
</div>
