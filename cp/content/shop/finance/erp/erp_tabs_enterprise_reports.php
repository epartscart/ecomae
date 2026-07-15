<?php
/**
 * Module: Report.
 * Sub-modules: Trial balance (BU-wise + combined), Audit trail,
 * Dimension reporting, COA shared across all BU.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_gl.php';
epc_erp_pm_inline_assets();

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'trial';
$subs = array(
	'trial' => 'Trial balance',
	'dimension' => 'Dimension reporting',
	'coa' => 'Shared COA',
);

echo '<div class="epc-erp-related-links" style="margin-bottom:14px;padding:8px 12px;background:#f9f9f9;border:1px solid #e3e3e3;border-radius:4px;">'
	. '<strong style="font-size:12px;color:#555;">Related:</strong>'
	. ' <a href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'gl', $date_from_str, $date_to_str, 'finance')) . '" style="margin-left:8px;font-size:12px;">General ledger / Journals</a>'
	. ' <a href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'pl', $date_from_str, $date_to_str, 'finance')) . '" style="margin-left:8px;font-size:12px;">Profit &amp; loss</a>'
	. ' <a href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'balance_sheet', $date_from_str, $date_to_str, 'finance')) . '" style="margin-left:8px;font-size:12px;">Balance sheet</a>'
	. '</div>';
echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-table"></i> Reports</h3>';
echo '<p class="text-muted">Trial balance (combined &amp; per business unit), financial-dimension reporting, audit trail and the chart of accounts shared across all business units. Per-tenant.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'enterprise_reports', 'insights', $date_from_str, $date_to_str, $subs, $view);

if ($view === 'dimension') {
	echo '<div class="epc-erp-section"><h4><i class="fa fa-tags"></i> Dimension reporting</h4>';
	echo '<p class="text-muted">Financial dimensions configured for this tenant. Postings can be analysed by these dimensions (department, project, cost centre, ...).</p>';
	try {
		$dims = epc_erp_pm_list($db_link, 'epc_erp_pm_dimensions', true);
		$vals = epc_erp_pm_list($db_link, 'epc_erp_pm_dimension_values', true);
	} catch (Exception $e) {
		$dims = array();
		$vals = array();
	}
	$byDim = array();
	foreach ($vals as $v) {
		$byDim[(int) $v['dimension_id']][] = $v;
	}
	if (empty($dims)) {
		echo '<p class="text-muted">No dimensions configured — add them under Enterprise → Business Unit → Financial dimensions.</p>';
	} else {
		echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>Dimension</th><th>Type</th><th>Values</th></tr></thead><tbody>';
		foreach ($dims as $d) {
			$vs = $byDim[(int) $d['id']] ?? array();
			$names = array_map(function ($x) {
				return $x['code'] . '·' . $x['name'];
			}, $vs);
			echo '<tr><td>' . epc_erp_h($d['code'] . ' · ' . $d['name']) . '</td><td>' . epc_erp_h((string) $d['dim_type']) . '</td><td>' . epc_erp_h($names ? implode(', ', $names) : '—') . '</td></tr>';
		}
		echo '</tbody></table></div>';
	}
	echo '</div>';
} elseif ($view === 'coa') {
	echo '<div class="epc-erp-section"><h4><i class="fa fa-list"></i> Chart of accounts (shared across all business units)</h4>';
	echo '<p class="text-muted">A single COA is shared across every business unit in this tenant, so consolidation and inter-BU reporting line up on the same accounts.</p>';
	try {
		$coa = epc_erp_gl_list_coa($db_link);
	} catch (Exception $e) {
		$coa = array();
	}
	echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>Code</th><th>Account</th><th>Type</th><th style="text-align:right;">Balance</th></tr></thead><tbody>';
	foreach ($coa as $a) {
		echo '<tr><td>' . epc_erp_h((string) $a['code']) . '</td><td>' . epc_erp_h((string) $a['name']) . '</td><td>' . epc_erp_h((string) ($a['account_type'] ?? '')) . '</td><td style="text-align:right;">' . epc_erp_money((float) ($a['balance'] ?? 0)) . '</td></tr>';
	}
	echo '</tbody></table></div>';
	echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'coa', $date_from_str, $date_to_str, 'finance')) . '">Manage COA</a> ';
	echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'audit', $date_from_str, $date_to_str, 'insights')) . '"><i class="fa fa-history"></i> Audit trail</a>';
	echo '</div>';
} else {
	// Trial balance (combined). BU selector is shown; postings share one COA.
	$buOpts = array('0' => 'Combined (all business units)');
	try {
		foreach (epc_erp_pm_list($db_link, 'epc_erp_pm_business_units', true) as $bu) {
			$buOpts[(string) $bu['id']] = $bu['code'] . ' · ' . $bu['name'];
		}
	} catch (Exception $e) {
	}
	$selBu = (string) ($_GET['bu'] ?? '0');
	echo '<div class="epc-erp-section"><h4><i class="fa fa-balance-scale"></i> Trial balance <small class="text-muted">' . epc_erp_h($date_from_str) . ' → ' . epc_erp_h($date_to_str) . '</small></h4>';
	echo '<form method="get" class="form-inline" style="margin-bottom:10px;"><input type="hidden" name="area" value="insights"><input type="hidden" name="tab" value="enterprise_reports"><input type="hidden" name="pm_view" value="trial"><input type="hidden" name="from" value="' . epc_erp_h($date_from_str) . '"><input type="hidden" name="to" value="' . epc_erp_h($date_to_str) . '"><label>Business unit</label> <select name="bu" class="form-control input-sm" onchange="this.form.submit()">';
	foreach ($buOpts as $v => $t) {
		$sel = ((string) $v === $selBu) ? ' selected' : '';
		echo '<option value="' . epc_erp_h((string) $v) . '"' . $sel . '>' . epc_erp_h((string) $t) . '</option>';
	}
	echo '</select></form>';

	try {
		$coa = epc_erp_gl_list_coa($db_link);
	} catch (Exception $e) {
		$coa = array();
	}
	$from = strtotime($date_from_str . ' 00:00:00') ?: 0;
	$to = strtotime($date_to_str . ' 23:59:59') ?: time();
	echo '<div class="table-responsive"><table class="table table-striped table-bordered table-condensed"><thead><tr><th>Code</th><th>Account</th><th style="text-align:right;">Debit</th><th style="text-align:right;">Credit</th></tr></thead><tbody>';
	$td = 0.0;
	$tc = 0.0;
	foreach ($coa as $a) {
		$act = epc_erp_gl_coa_activity($db_link, (int) $a['id'], $from, $to);
		$d = (float) $act['debits'];
		$c = (float) $act['credits'];
		if ($d == 0.0 && $c == 0.0) {
			continue;
		}
		$td += $d;
		$tc += $c;
		echo '<tr><td>' . epc_erp_h((string) $a['code']) . '</td><td>' . epc_erp_h((string) $a['name']) . '</td><td style="text-align:right;">' . epc_erp_money($d) . '</td><td style="text-align:right;">' . epc_erp_money($c) . '</td></tr>';
	}
	echo '<tr><th colspan="2" style="text-align:right;">Totals</th><th style="text-align:right;">' . epc_erp_money($td) . '</th><th style="text-align:right;">' . epc_erp_money($tc) . '</th></tr>';
	echo '</tbody></table></div>';
	$bal = abs($td - $tc) < 0.01;
	echo '<p>' . ($bal ? '<span class="label label-success">In balance</span>' : '<span class="label label-danger">Out of balance by ' . epc_erp_money(abs($td - $tc)) . '</span>') . ($selBu !== '0' ? ' <span class="text-muted">Postings share one COA across BUs; per-BU tagging applies once journals are dimensioned to the selected business unit.</span>' : '') . '</p>';
	echo '</div>';
}
