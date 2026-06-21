<?php
defined('_ASTEXE_') or die('No access');

/**
 * BOS Industry Intelligence pillar — per-industry KPIs, operational dashboards
 * and recommended controls.
 *
 * Config-driven: the active industry (from the tenant's industry profile and/or
 * industry pack) selects which KPI set and control checklist apply. Universal
 * finance KPIs are computed for every tenant from live ERP data; industry packs
 * layer extra operational controls on top. Control check-off state is stored
 * per tenant. No KPI, threshold or control is hard-coded to one tenant.
 */

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_industry.php';

if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_industry_packs.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_industry_packs.php';
}

function epc_bos_intel_admin_id(): int
{
	return function_exists('epc_erp_admin_id') ? (int) epc_erp_admin_id() : 0;
}

/**
 * Resolve the active industry context for the tenant: catalog profile (drives
 * product fields) and, when present, the richer industry pack (drives operational
 * features/process flow). Both are per-tenant settings.
 *
 * @return array<string,mixed>
 */
function epc_bos_intel_context(PDO $db): array
{
	$current = function_exists('epc_erp_industry_current') ? epc_erp_industry_current($db) : array('key' => '', 'label' => '');
	$packKey = '';
	$pack = null;
	if (function_exists('epc_erp_adv_get_setting')) {
		$packKey = trim((string) epc_erp_adv_get_setting($db, 'erp_industry_pack', ''));
	}
	if ($packKey !== '' && function_exists('epc_erp_industry_pack')) {
		$pack = epc_erp_industry_pack($packKey);
	}
	return array(
		'profile_key' => (string) ($current['key'] ?? ''),
		'profile_label' => (string) ($current['label'] ?? ''),
		'pack_key' => $packKey,
		'pack' => $pack,
		'pack_features' => is_array($pack) && !empty($pack['features']) ? $pack['features'] : array(),
		'pack_label' => is_array($pack) ? (string) ($pack['label'] ?? '') : '',
	);
}

/**
 * Compute the universal finance/operational KPI set from live ERP data for the
 * given period. Each KPI has value, format, benchmark hint and a health flag.
 *
 * @return array<int,array<string,mixed>>
 */
function epc_bos_intel_kpis(PDO $db, int $dateFrom, int $dateTo): array
{
	// Per-request memoization: the KPI set is rendered both on the home
	// dashboard portlet and the Industry Intelligence tab, and aggregates GL +
	// P&L + inventory. Cache by period so it is computed at most once per load.
	static $memo = array();
	$key = $dateFrom . ':' . $dateTo;
	if (isset($memo[$key])) {
		return $memo[$key];
	}
	$dash = epc_erp_dashboard($db, $dateFrom, $dateTo);
	$pl = function_exists('epc_erp_gl_pl_report') ? epc_erp_gl_pl_report($db, $dateFrom, $dateTo) : array();
	$invValue = 0.0;
	if (function_exists('epc_erp_inventory_valuation_total')) {
		try {
			$iv = epc_erp_inventory_valuation_total($db);
			$invValue = is_array($iv) ? (float) ($iv['total_value'] ?? $iv['value'] ?? 0) : (float) $iv;
		} catch (Throwable $e) {
			$invValue = 0.0;
		}
	}

	$revenue = (float) ($dash['revenue_ex_vat'] ?? 0);
	$purchases = (float) ($dash['purchase_ex_vat'] ?? 0);
	$profit = (float) ($dash['profit_ex_vat'] ?? 0);
	$ar = (float) ($dash['customer_ledger_balance'] ?? 0);
	$ap = (float) ($dash['payable_balance'] ?? 0);
	$cash = (float) ($dash['cash_bank_total'] ?? 0);

	$days = max(1, (int) round(($dateTo - $dateFrom) / 86400));
	$grossMargin = $revenue > 0 ? ($profit / $revenue) * 100 : 0.0;
	$dso = $revenue > 0 ? ($ar / ($revenue / $days)) : 0.0;
	$dpo = $purchases > 0 ? ($ap / ($purchases / $days)) : 0.0;
	$invTurnover = $invValue > 0 ? ($purchases / $invValue) : 0.0;
	$currentAssets = $cash + $ar + $invValue;
	$currentRatio = $ap > 0 ? ($currentAssets / $ap) : 0.0;

	$flag = function ($val, $good, $warn, $higherBetter = true) {
		if ($higherBetter) {
			if ($val >= $good) return 'good';
			if ($val >= $warn) return 'warn';
			return 'bad';
		}
		if ($val <= $good) return 'good';
		if ($val <= $warn) return 'warn';
		return 'bad';
	};

	$memo[$key] = array(
		array('key' => 'revenue', 'label' => 'Revenue (period)', 'value' => $revenue, 'format' => 'money', 'health' => $revenue > 0 ? 'good' : 'warn', 'hint' => 'Net sales excl. VAT'),
		array('key' => 'gross_margin', 'label' => 'Gross margin %', 'value' => $grossMargin, 'format' => 'pct', 'health' => $flag($grossMargin, 25, 12, true), 'hint' => 'Profit / revenue'),
		array('key' => 'dso', 'label' => 'DSO (days sales outstanding)', 'value' => $dso, 'format' => 'days', 'health' => $flag($dso, 30, 60, false), 'hint' => 'Lower is faster collection'),
		array('key' => 'dpo', 'label' => 'DPO (days payable outstanding)', 'value' => $dpo, 'format' => 'days', 'health' => $flag($dpo, 45, 20, true), 'hint' => 'Supplier payment cycle'),
		array('key' => 'inv_turnover', 'label' => 'Inventory turnover', 'value' => $invTurnover, 'format' => 'x', 'health' => $flag($invTurnover, 4, 2, true), 'hint' => 'Purchases / inventory value'),
		array('key' => 'current_ratio', 'label' => 'Current ratio (approx)', 'value' => $currentRatio, 'format' => 'x', 'health' => $flag($currentRatio, 1.5, 1.0, true), 'hint' => '(Cash+AR+Inv) / AP'),
		array('key' => 'ar', 'label' => 'AR outstanding', 'value' => $ar, 'format' => 'money', 'health' => 'info', 'hint' => 'Customer ledger balance'),
		array('key' => 'ap', 'label' => 'AP outstanding', 'value' => $ap, 'format' => 'money', 'health' => 'info', 'hint' => 'Supplier ledger balance'),
		array('key' => 'cash', 'label' => 'Cash & bank', 'value' => $cash, 'format' => 'money', 'health' => $cash >= 0 ? 'good' : 'bad', 'hint' => 'Liquidity position'),
		array('key' => 'inventory', 'label' => 'Inventory value', 'value' => $invValue, 'format' => 'money', 'health' => 'info', 'hint' => 'Stock at weighted-avg cost'),
	);
	return $memo[$key];
}

/**
 * Recommended-controls library. Generic controls apply to every industry; the
 * per-industry map and the active pack's feature set add specialised controls.
 * Returns a flat de-duplicated list of controls for the tenant's context.
 *
 * @return array<int,array<string,string>>
 */
function epc_bos_intel_controls(PDO $db, array $ctx): array
{
	$generic = array(
		array('code' => 'monthly_close', 'title' => 'Monthly close & reconciliation', 'desc' => 'Reconcile bank, AR, AP and inventory each month before reporting.'),
		array('code' => 'segregation', 'title' => 'Segregation of duties', 'desc' => 'Separate who raises, approves and pays a transaction.'),
		array('code' => 'approval_thresholds', 'title' => 'Approval thresholds enforced', 'desc' => 'High-value POs/payments routed through the approval engine.'),
		array('code' => 'aging_review', 'title' => 'AR/AP aging review', 'desc' => 'Review overdue receivables and payables weekly.'),
		array('code' => 'vat_filed', 'title' => 'Tax returns filed on time', 'desc' => 'No overdue items in the compliance filing calendar.'),
	);

	$byProfile = array(
		'general' => array(
			array('code' => 'stock_count', 'title' => 'Periodic stock count', 'desc' => 'Cycle-count fast movers; full count at period end.'),
		),
		'jewellery_diamond' => array(
			array('code' => 'metal_weighbridge', 'title' => 'Daily metal weight reconciliation', 'desc' => 'Reconcile gold/stone weight in vs out by purity.'),
			array('code' => 'making_margin', 'title' => 'Making-charge margin check', 'desc' => 'Verify making charges billed vs job-work cost.'),
		),
		'oil_gas' => array(
			array('code' => 'jv_split', 'title' => 'JV cost split reconciliation', 'desc' => 'Confirm partner working-interest % allocations.'),
			array('code' => 'royalty_accrual', 'title' => 'Royalty accrual review', 'desc' => 'Royalty accrued vs production revenue.'),
		),
		'construction_contracting' => array(
			array('code' => 'wip_vs_billing', 'title' => 'WIP vs progress billing', 'desc' => 'Compare cost-to-date and certified billing per project.'),
			array('code' => 'retention_track', 'title' => 'Retention receivable tracking', 'desc' => 'Track retention held and release dates.'),
		),
		'manufacturing' => array(
			array('code' => 'bom_variance', 'title' => 'BOM / material variance', 'desc' => 'Standard vs actual material consumption.'),
			array('code' => 'wip_fg_ratio', 'title' => 'WIP vs finished-goods ratio', 'desc' => 'Monitor WIP build-up against output.'),
		),
		'retail_pos' => array(
			array('code' => 'till_recon', 'title' => 'Daily till / shift reconciliation', 'desc' => 'Cash over/short posted per shift Z-report.'),
		),
		'pharma_healthcare' => array(
			array('code' => 'expiry_control', 'title' => 'Batch & expiry control', 'desc' => 'FEFO picking; quarantine near-expiry stock.'),
		),
	);

	// Feature → control map for industry packs.
	$byFeature = array(
		'landed_cost' => array('code' => 'landed_cost_alloc', 'title' => 'Landed-cost allocation', 'desc' => 'Capitalise freight/duty/insurance into stock cost.'),
		'multi_currency' => array('code' => 'fx_revaluation', 'title' => 'FX revaluation', 'desc' => 'Revalue foreign-currency AR/AP at period end.'),
		'serial_tracking' => array('code' => 'serial_audit', 'title' => 'Serial / IMEI audit', 'desc' => 'Reconcile serialised units in stock vs system.'),
		'loyalty' => array('code' => 'loyalty_liability', 'title' => 'Loyalty liability reconciliation', 'desc' => 'Points accrued vs redeemed liability.'),
		'joint_venture' => array('code' => 'jv_partner_billing', 'title' => 'JV partner billing', 'desc' => 'Bill partners their working-interest share.'),
	);

	$controls = $generic;
	$profileKey = (string) ($ctx['profile_key'] ?? '');
	$packKey = (string) ($ctx['pack_key'] ?? '');
	foreach (array($profileKey, $packKey) as $k) {
		if ($k !== '' && isset($byProfile[$k])) {
			$controls = array_merge($controls, $byProfile[$k]);
		}
	}
	foreach ((array) ($ctx['pack_features'] ?? array()) as $feat) {
		if (isset($byFeature[$feat])) {
			$controls[] = $byFeature[$feat];
		}
	}
	// De-duplicate by code.
	$seen = array();
	$out = array();
	foreach ($controls as $c) {
		if (isset($seen[$c['code']])) {
			continue;
		}
		$seen[$c['code']] = true;
		$out[] = $c;
	}
	return $out;
}

/** @return array<string,int> control_code => 1 when ticked (per tenant). */
function epc_bos_intel_control_state(PDO $db): array
{
	if (!function_exists('epc_erp_adv_get_setting')) {
		return array();
	}
	$json = epc_erp_adv_get_setting($db, 'bos_intel_controls', '');
	$arr = json_decode((string) $json, true);
	return is_array($arr) ? $arr : array();
}

function epc_bos_intel_set_control(PDO $db, string $code, bool $checked): void
{
	if (!function_exists('epc_erp_adv_set_setting')) {
		return;
	}
	$state = epc_bos_intel_control_state($db);
	if ($checked) {
		$state[$code] = 1;
	} else {
		unset($state[$code]);
	}
	epc_erp_adv_set_setting($db, 'bos_intel_controls', json_encode($state));
}

function epc_bos_intel_format(float $value, string $format): string
{
	switch ($format) {
		case 'pct': return number_format($value, 1) . '%';
		case 'days': return number_format($value, 0) . ' d';
		case 'x': return number_format($value, 2) . 'x';
		case 'money': return number_format($value, 2);
		default: return number_format($value, 2);
	}
}
