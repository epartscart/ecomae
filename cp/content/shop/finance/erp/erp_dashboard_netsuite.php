<?php
/**
 * NetSuite-style ERP home dashboard (portlet layout).
 *
 * Renders the /erp/ door home as role/config-driven portlets:
 *   - colour Tiles (quick documents)
 *   - Reminders portlet (live counts from this tenant DB)
 *   - Navigation Shortcut Group
 *   - Key Performance Indicators table (current vs previous period + change)
 *   - Financials snapshot
 *   - KPI Meter gauge (cash & bank)
 *   - A/R Aging bar chart
 *
 * All figures come from the existing tenant-scoped data layer ($dashboard,
 * $dashboard_pl and epc_erp_ar_aging); nothing is hard-coded. The portlet set
 * is resolved per user role so an accounting role and a sales role can see a
 * different home (NetSuite-style role centres).
 *
 * Expected in scope from erp_main.php: $db_link, $dashboard, $dashboard_pl,
 * $erpUrl, $date_from, $date_to, $date_from_str, $date_to_str, $csrf.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_aging.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_intelligence.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_dashboard_profiles.php';

/** Profile centre for this signed-in user (CEO / CFO / Sales / …). */
$nsProfile = epc_erp_dashboard_profile_meta($db_link);
$nsRole = (string) ($nsProfile['key'] ?? 'finance');
$nsCan = function (string $cap) use ($nsProfile): bool {
	return epc_erp_dashboard_can($nsProfile, $cap);
};

// ---- Operational KPIs (BOS industry-intelligence engine, tenant-scoped) ----
// Reuse $dashboard / $dashboard_pl already computed in erp_main.php.
$opKpis = array();
if ($nsCan('op_kpis')) {
	try {
		$opKpis = epc_bos_intel_kpis(
			$db_link,
			(int) $date_from,
			(int) $date_to,
			isset($dashboard) && is_array($dashboard) ? $dashboard : null,
			isset($dashboard_pl) && is_array($dashboard_pl) ? $dashboard_pl : null
		);
	} catch (Throwable $e) {
		$opKpis = array();
	}
	$allowedOpKeys = isset($nsProfile['op_kpi_keys']) && is_array($nsProfile['op_kpi_keys'])
		? $nsProfile['op_kpi_keys']
		: array();
	if (!empty($allowedOpKeys)) {
		$opKpis = array_values(array_filter($opKpis, function ($k) use ($allowedOpKeys) {
			return in_array((string) ($k['key'] ?? ''), $allowedOpKeys, true);
		}));
	}
	// Defence in depth: never surface profit/margin cards without capability.
	if (!$nsCan('profit')) {
		$opKpis = array_values(array_filter($opKpis, function ($k) {
			$key = (string) ($k['key'] ?? '');
			return $key !== 'gross_margin' && stripos((string) ($k['label'] ?? ''), 'margin') === false
				&& stripos((string) ($k['label'] ?? ''), 'profit') === false;
		}));
	}
}

$nsCurrency = '';
$nsCo = array();
if (function_exists('epc_co_profile_get')) {
	try {
		$nsCo = epc_co_profile_get($db_link);
		if (!is_array($nsCo)) { $nsCo = array(); }
		$nsCurrency = isset($nsCo['base_currency']) ? (string) $nsCo['base_currency'] : '';
	} catch (Exception $e) {
		$nsCurrency = '';
		$nsCo = array();
	}
}
if ($nsCurrency === '') {
	$nsCurrency = 'AED';
}

/** Previous equal-length period, for trend arrows. */
$nsSpan = max(1, (int) $date_to - (int) $date_from);
$nsPrevTo = (int) $date_from - 1;
$nsPrevFrom = $nsPrevTo - $nsSpan;
$nsPrev = array();
if (function_exists('epc_erp_dashboard')) {
	try {
		// Light path: trend arrows need order/cash/AR/AP only — skip VAT + CC tiles.
		$nsPrev = epc_erp_dashboard($db_link, $nsPrevFrom, $nsPrevTo, true);
	} catch (Exception $e) {
		$nsPrev = array();
	}
}

/** A/R aging buckets (real, tenant-scoped). */
$nsAr = array('labels' => array('Not due', '1-30', '31-60', '61-90', '90+'), 'totals' => array(0, 0, 0, 0, 0), 'grand' => 0.0);
try {
	$nsArFull = epc_erp_ar_aging($db_link);
	$nsAr['labels'] = $nsArFull['labels'];
	$nsAr['totals'] = array_map('floatval', array_values($nsArFull['totals']));
	$nsAr['grand'] = (float) $nsArFull['grand'];
} catch (Exception $e) {
}

$nsMoney = function ($v) {
	return number_format((float) $v, 2);
};
$nsUrl = function ($tab, $area = '') use ($erpUrl, $date_from_str, $date_to_str) {
	return epc_erp_h(epc_erp_tab_url($erpUrl, $tab, $date_from_str, $date_to_str, $area));
};
/** Deep-link straight into an External Reporting report (cat + rep, auto-fetch). */
$nsExtUrl = function ($cat, $rep) use ($nsUrl) {
	return $nsUrl('ext_reports', 'regrep')
		. '&amp;cat=' . urlencode($cat)
		. '&amp;rep=' . urlencode($rep)
		. '&amp;fetch=1';
};
/** Change badge: returns html for % change current vs previous. */
$nsChange = function ($cur, $prev, $goodWhenUp = true) {
	$cur = (float) $cur;
	$prev = (float) $prev;
	if (abs($prev) < 0.005) {
		return '<span class="ns-chg ns-flat">—</span>';
	}
	$pct = (($cur - $prev) / abs($prev)) * 100.0;
	$up = $pct >= 0;
	$good = $up === $goodWhenUp;
	$arrow = $up ? '&#9650;' : '&#9660;';
	$cls = $good ? 'ns-up' : 'ns-down';
	return '<span class="ns-chg ' . $cls . '">' . $arrow . ' ' . number_format(abs($pct), 1) . '%</span>';
};

// ---- Reminders (live counts; defensive) ----
$nsCount = function (string $sql) use ($db_link): int {
	try {
		return (int) $db_link->query($sql)->fetchColumn();
	} catch (Exception $e) {
		return 0;
	}
};
$remDraftSO = $nsCount("SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'draft'");
$remConfirmSO = $nsCount("SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'confirmed'");
$remOpenPO = $nsCount("SELECT COUNT(*) FROM `epc_erp_purchase_orders` WHERE `status` IN ('draft','sent','confirmed')");
$remInvDue = $nsCount("SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE `active` = 1 AND `status` <> 'cancelled' AND (`total_incl_vat` - `paid_amount`) > 0.005");

$reminders = array();
if ($nsCan('sales')) {
	$reminders[] = array('n' => $remDraftSO, 'label' => 'Draft sales orders to confirm', 'url' => $nsUrl('sales_orders', 'sales'));
	$reminders[] = array('n' => $remConfirmSO, 'label' => 'Confirmed orders to invoice', 'url' => $nsUrl('sales_orders', 'sales'));
}
if ($nsCan('purchases')) {
	$reminders[] = array('n' => $remOpenPO, 'label' => 'Open purchase orders', 'url' => $nsUrl('purchase_orders', 'purchasing'));
}
if ($nsCan('ar')) {
	$reminders[] = array('n' => $remInvDue, 'label' => 'Invoices with balance due', 'url' => $nsUrl('aging', 'finance') . '&amp;aging_view=ar');
}

// ---- Tile / quick-action catalogues (keys referenced by profile config) ----
$tileCatalog = array(
	'balance_sheet' => array('label' => 'Balance Sheet', 'icon' => 'fa-balance-scale', 'tone' => 'gold', 'url' => $nsUrl('balance_sheet', 'insights'), 'need' => 'gl'),
	'gl' => array('label' => 'General Journal', 'icon' => 'fa-book', 'tone' => 'green', 'url' => $nsUrl('gl', 'finance'), 'need' => 'gl'),
	'bank_recon' => array('label' => 'Reconcile Bank', 'icon' => 'fa-university', 'tone' => 'rust', 'url' => $nsUrl('bank_recon', 'finance'), 'need' => 'cash'),
	'pl' => array('label' => 'Income Statement', 'icon' => 'fa-line-chart', 'tone' => 'slate', 'url' => $nsUrl('pl', 'insights'), 'need' => 'profit'),
	'sales_orders' => array('label' => 'New Sales Order', 'icon' => 'fa-shopping-cart', 'tone' => 'gold', 'url' => $nsUrl('sales_orders', 'sales'), 'need' => 'sales'),
	'crm' => array('label' => 'CRM Pipeline', 'icon' => 'fa-handshake-o', 'tone' => 'green', 'url' => $nsUrl('crm', 'sales'), 'need' => 'sales'),
	'receivables' => array('label' => 'Receivables', 'icon' => 'fa-users', 'tone' => 'rust', 'url' => $nsUrl('receivables', 'sales'), 'need' => 'ar'),
	'aging_ar' => array('label' => 'A/R Aging', 'icon' => 'fa-hourglass-half', 'tone' => 'slate', 'url' => $nsUrl('aging', 'finance') . '&amp;aging_view=ar', 'need' => 'aging_ar'),
	'purchase_orders' => array('label' => 'New Purchase Order', 'icon' => 'fa-clipboard', 'tone' => 'gold', 'url' => $nsUrl('purchase_orders', 'purchasing'), 'need' => 'purchases'),
	'payables' => array('label' => 'Payables', 'icon' => 'fa-truck', 'tone' => 'green', 'url' => $nsUrl('payables', 'purchasing'), 'need' => 'ap'),
	'three_way_match' => array('label' => '3-way Match', 'icon' => 'fa-check-square-o', 'tone' => 'rust', 'url' => $nsUrl('three_way_match', 'purchasing'), 'need' => 'purchases'),
	'aging_ap' => array('label' => 'A/P Aging', 'icon' => 'fa-hourglass-half', 'tone' => 'slate', 'url' => $nsUrl('aging', 'finance') . '&amp;aging_view=ap', 'need' => 'aging_ap'),
	'cash_bank' => array('label' => 'Cash &amp; bank', 'icon' => 'fa-money', 'tone' => 'green', 'url' => $nsUrl('cash_bank', 'finance'), 'need' => 'cash'),
	'vat_return' => array('label' => 'VAT Return', 'icon' => 'fa-percent', 'tone' => 'rust', 'url' => $nsUrl('vat_return', 'finance'), 'need' => 'vat'),
	'inventory' => array('label' => 'Inventory', 'icon' => 'fa-cubes', 'tone' => 'slate', 'url' => $nsUrl('inventory', 'operations'), 'need' => 'inventory'),
	'fulfilment' => array('label' => 'Fulfilment', 'icon' => 'fa-truck', 'tone' => 'gold', 'url' => $nsUrl('fulfilment', 'sales'), 'need' => 'sales'),
	'hr' => array('label' => 'Human resources', 'icon' => 'fa-users', 'tone' => 'green', 'url' => $nsUrl('hr', 'people'), 'need' => 'hr_tasks'),
	'payroll' => array('label' => 'Payroll', 'icon' => 'fa-credit-card', 'tone' => 'rust', 'url' => $nsUrl('payroll', 'people'), 'need' => 'hr_tasks'),
	'staff' => array('label' => 'Staff directory', 'icon' => 'fa-id-badge', 'tone' => 'slate', 'url' => $nsUrl('staff', 'people'), 'need' => 'hr_tasks'),
	'workflow' => array('label' => 'Workflow', 'icon' => 'fa-tasks', 'tone' => 'gold', 'url' => $nsUrl('workflow', 'overview'), 'need' => 'hr_tasks'),
	'marketing' => array('label' => 'Marketing', 'icon' => 'fa-bullhorn', 'tone' => 'rust', 'url' => $nsUrl('marketing', 'sales'), 'need' => 'sales'),
	'processflow' => array('label' => 'Process flow', 'icon' => 'fa-sitemap', 'tone' => 'green', 'url' => $nsUrl('processflow', 'overview'), 'need' => 'hr_tasks'),
	'dashboard' => array('label' => 'Home', 'icon' => 'fa-home', 'tone' => 'slate', 'url' => $nsUrl('dashboard', 'overview'), 'need' => 'sales'),
);
$tiles = array();
foreach ((array) ($nsProfile['tiles'] ?? array()) as $tileKey) {
	if (!isset($tileCatalog[$tileKey])) {
		continue;
	}
	$t = $tileCatalog[$tileKey];
	if (!$nsCan((string) ($t['need'] ?? 'sales'))) {
		continue;
	}
	$tiles[] = $t;
}
if (empty($tiles)) {
	$tiles[] = $tileCatalog['dashboard'];
}

// ---- Navigation shortcut group (capability-filtered) ----
$navGroups = array();
$navLists = array();
if ($nsCan('inventory')) {
	$navLists[] = array('label' => 'Items', 'icon' => 'fa-cubes', 'url' => $nsUrl('inventory', 'operations'));
}
if ($nsCan('ar') || $nsCan('sales')) {
	$navLists[] = array('label' => 'Customers', 'icon' => 'fa-users', 'url' => $nsUrl('receivables', 'sales'));
}
if ($nsCan('ap') || $nsCan('purchases')) {
	$navLists[] = array('label' => 'Vendors', 'icon' => 'fa-truck', 'url' => $nsUrl('payables', 'purchasing'));
}
$navLists[] = array('label' => 'Contacts', 'icon' => 'fa-address-book-o', 'url' => $nsUrl('contacts', 'collaboration'));
if (!empty($navLists)) {
	$navGroups['Lists'] = $navLists;
}
$navTx = array();
if ($nsCan('sales')) {
	$navTx[] = array('label' => 'Sales order', 'icon' => 'fa-shopping-cart', 'url' => $nsUrl('sales_orders', 'sales'));
}
if ($nsCan('purchases')) {
	$navTx[] = array('label' => 'Purchase order', 'icon' => 'fa-clipboard', 'url' => $nsUrl('purchase_orders', 'purchasing'));
}
if ($nsCan('cash')) {
	$navTx[] = array('label' => 'Receipt voucher', 'icon' => 'fa-money', 'url' => $nsUrl('cash_bank', 'finance'));
}
if ($nsCan('gl')) {
	$navTx[] = array('label' => 'General ledger', 'icon' => 'fa-book', 'url' => $nsUrl('gl', 'finance'));
}
if (!empty($navTx)) {
	$navGroups['Transactions'] = $navTx;
}
$navRep = array();
if ($nsCan('gl') || $nsCan('profit')) {
	$navRep[] = array('label' => 'Financial report (IFRS)', 'icon' => 'fa-file-text-o', 'url' => $nsExtUrl('audit', 'audit__external_audit_report'));
}
if ($nsCan('vat')) {
	$navRep[] = array('label' => 'VAT return (VAT 201)', 'icon' => 'fa-percent', 'url' => $nsExtUrl('tax', 'tax__vat_return'));
	$navRep[] = array('label' => 'Corporate tax return', 'icon' => 'fa-balance-scale', 'url' => $nsExtUrl('tax', 'tax__corporate_income_tax_return'));
}
if ($nsCan('profit')) {
	$navRep[] = array('label' => 'Profit &amp; loss', 'icon' => 'fa-line-chart', 'url' => $nsUrl('pl', 'insights'));
}
if (!empty($navRep)) {
	$navGroups['Reports'] = $navRep;
}

$quickCatalog = array(
	'ext_ifrs' => array('label' => 'Financial Report (IFRS)', 'icon' => 'fa-file-text-o', 'tone' => 'qa-indigo', 'url' => $nsExtUrl('audit', 'audit__external_audit_report'), 'need' => 'profit'),
	'ext_vat' => array('label' => 'VAT Return (VAT 201)', 'icon' => 'fa-percent', 'tone' => 'qa-green', 'url' => $nsExtUrl('tax', 'tax__vat_return'), 'need' => 'vat'),
	'ext_ct' => array('label' => 'Corporate Tax Return', 'icon' => 'fa-balance-scale', 'tone' => 'qa-rust', 'url' => $nsExtUrl('tax', 'tax__corporate_income_tax_return'), 'need' => 'vat'),
	'sales_orders' => array('label' => 'New Sales Order', 'icon' => 'fa-shopping-cart', 'tone' => 'qa-blue', 'url' => $nsUrl('sales_orders', 'sales'), 'need' => 'sales'),
	'purchase_orders' => array('label' => 'New Purchase Order', 'icon' => 'fa-clipboard', 'tone' => 'qa-indigo', 'url' => $nsUrl('purchase_orders', 'purchasing'), 'need' => 'purchases'),
	'inventory' => array('label' => 'New Item', 'icon' => 'fa-cubes', 'tone' => 'qa-amber', 'url' => $nsUrl('inventory', 'operations'), 'need' => 'inventory'),
	'customers' => array('label' => 'New Customer', 'icon' => 'fa-user-plus', 'tone' => 'qa-pink', 'url' => $nsUrl('receivables', 'sales'), 'need' => 'ar'),
	'vendors' => array('label' => 'New Vendor', 'icon' => 'fa-truck', 'tone' => 'qa-teal', 'url' => $nsUrl('payables', 'purchasing'), 'need' => 'ap'),
	'cash_bank' => array('label' => 'Receipt Voucher', 'icon' => 'fa-money', 'tone' => 'qa-green', 'url' => $nsUrl('cash_bank', 'finance'), 'need' => 'cash'),
	'gl' => array('label' => 'General Ledger', 'icon' => 'fa-book', 'tone' => 'qa-slate', 'url' => $nsUrl('gl', 'finance'), 'need' => 'gl'),
	'vat_return' => array('label' => 'VAT Return', 'icon' => 'fa-percent', 'tone' => 'qa-rust', 'url' => $nsUrl('vat_return', 'finance'), 'need' => 'vat'),
	'pl' => array('label' => 'Profit &amp; Loss', 'icon' => 'fa-line-chart', 'tone' => 'qa-indigo', 'url' => $nsUrl('pl', 'insights'), 'need' => 'profit'),
	'receivables' => array('label' => 'Receivables', 'icon' => 'fa-users', 'tone' => 'qa-blue', 'url' => $nsUrl('receivables', 'sales'), 'need' => 'ar'),
	'payables' => array('label' => 'Payables', 'icon' => 'fa-truck', 'tone' => 'qa-teal', 'url' => $nsUrl('payables', 'purchasing'), 'need' => 'ap'),
	'crm' => array('label' => 'CRM Pipeline', 'icon' => 'fa-handshake-o', 'tone' => 'qa-pink', 'url' => $nsUrl('crm', 'sales'), 'need' => 'sales'),
	'coa' => array('label' => 'Chart of accounts', 'icon' => 'fa-list', 'tone' => 'qa-slate', 'url' => $nsUrl('coa', 'finance'), 'need' => 'gl'),
	'balance_sheet' => array('label' => 'Balance Sheet', 'icon' => 'fa-balance-scale', 'tone' => 'qa-amber', 'url' => $nsUrl('balance_sheet', 'insights'), 'need' => 'gl'),
	'hr' => array('label' => 'HR', 'icon' => 'fa-users', 'tone' => 'qa-blue', 'url' => $nsUrl('hr', 'people'), 'need' => 'hr_tasks'),
	'payroll' => array('label' => 'Payroll', 'icon' => 'fa-credit-card', 'tone' => 'qa-green', 'url' => $nsUrl('payroll', 'people'), 'need' => 'hr_tasks'),
	'staff' => array('label' => 'Staff', 'icon' => 'fa-id-badge', 'tone' => 'qa-slate', 'url' => $nsUrl('staff', 'people'), 'need' => 'hr_tasks'),
	'workflow' => array('label' => 'Workflow', 'icon' => 'fa-tasks', 'tone' => 'qa-indigo', 'url' => $nsUrl('workflow', 'overview'), 'need' => 'hr_tasks'),
	'marketing' => array('label' => 'Marketing', 'icon' => 'fa-bullhorn', 'tone' => 'qa-pink', 'url' => $nsUrl('marketing', 'sales'), 'need' => 'sales'),
	'leads' => array('label' => 'Leads', 'icon' => 'fa-user-plus', 'tone' => 'qa-amber', 'url' => $nsUrl('leads', 'sales'), 'need' => 'sales'),
	'processflow' => array('label' => 'Process flow', 'icon' => 'fa-sitemap', 'tone' => 'qa-teal', 'url' => $nsUrl('processflow', 'overview'), 'need' => 'hr_tasks'),
	'fulfilment' => array('label' => 'Fulfilment', 'icon' => 'fa-truck', 'tone' => 'qa-blue', 'url' => $nsUrl('fulfilment', 'sales'), 'need' => 'sales'),
);
$quickActions = array();
foreach ((array) ($nsProfile['quick'] ?? array()) as $qKey) {
	if (!isset($quickCatalog[$qKey])) {
		continue;
	}
	$qa = $quickCatalog[$qKey];
	if (!$nsCan((string) ($qa['need'] ?? 'sales'))) {
		continue;
	}
	$quickActions[] = $qa;
}

// Per-user customizable Quick actions (add/remove on the dashboard).
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_shortcut_icons.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_dash_shortcuts_ui.php';
$erpShortcutAjax = isset($erpAjaxEndpoint) ? (string) $erpAjaxEndpoint : '';
if ($erpShortcutAjax === '' && function_exists('epc_erp_configure_portal_urls')) {
	$erpShortcutUrls = epc_erp_configure_portal_urls(
		(isset($epc_erp_portal) && $epc_erp_portal === 'frontend') ? 'frontend' : 'cp'
	);
	$erpShortcutAjax = (string) ($erpShortcutUrls['erpAjaxUrl'] ?? '');
}
$erpShortcutCsrf = isset($csrf) ? (string) $csrf : '';
$erpShortcutCatalog = epc_shortcuts_catalog_erp($nsUrl);
// Keep catalogue aligned with role capabilities (hide locked modules).
foreach ($erpShortcutCatalog as $scKey => $scItem) {
	if (!isset($quickCatalog[$scKey])) {
		continue;
	}
	$need = (string) ($quickCatalog[$scKey]['need'] ?? '');
	if ($need !== '' && !$nsCan($need)) {
		unset($erpShortcutCatalog[$scKey]);
	}
}
$erpShortcutDefaults = array();
foreach ((array) ($nsProfile['quick'] ?? array()) as $qKey) {
	$qKey = (string) $qKey;
	if ($qKey !== '' && isset($erpShortcutCatalog[$qKey])) {
		$erpShortcutDefaults[] = $qKey;
	}
}
if ($erpShortcutDefaults === []) {
	$erpShortcutDefaults = array_slice(array_keys($erpShortcutCatalog), 0, 8);
}
$erpShortcutUid = epc_shortcuts_user_id();
$erpShortcutItems = array();
if ($db_link instanceof PDO && $erpShortcutUid > 0) {
	epc_shortcuts_seed_defaults($db_link, $erpShortcutUid, 'erp', $erpShortcutDefaults, $erpShortcutCatalog);
	$erpShortcutItems = epc_shortcuts_as_tiles(epc_shortcuts_list_for_surface($db_link, $erpShortcutUid, 'erp'));
} else {
	foreach ($erpShortcutDefaults as $dk) {
		if (!isset($erpShortcutCatalog[$dk])) {
			continue;
		}
		$c = $erpShortcutCatalog[$dk];
		$erpShortcutItems[] = array(
			'id' => 0,
			'key' => $dk,
			'label' => $c['label'],
			'icon' => preg_replace('/^fa\s+/', '', $c['icon']),
			'color' => $c['color'],
			'url' => $c['url'],
			'tone' => $c['tone'] ?? 'blue',
		);
	}
}

// ---- KPI table values (capability-gated) ----
$kpiRows = array();
if ($nsCan('ap')) {
	$kpiRows[] = array('name' => 'Payables', 'cur' => (float) ($dashboard['payable_balance'] ?? 0), 'prev' => (float) ($nsPrev['payable_balance'] ?? 0), 'goodUp' => false);
}
if ($nsCan('sales')) {
	$kpiRows[] = array('name' => 'Sales (ex VAT)', 'cur' => (float) ($dashboard['revenue_ex_vat'] ?? 0), 'prev' => (float) ($nsPrev['revenue_ex_vat'] ?? 0), 'goodUp' => true);
}
if ($nsCan('purchases')) {
	$kpiRows[] = array('name' => 'Expenses (purchases)', 'cur' => (float) ($dashboard['purchase_ex_vat'] ?? 0), 'prev' => (float) ($nsPrev['purchase_ex_vat'] ?? 0), 'goodUp' => false);
}
if ($nsCan('ar')) {
	$kpiRows[] = array('name' => 'Receivables', 'cur' => (float) ($dashboard['customer_ledger_balance'] ?? 0), 'prev' => (float) ($nsPrev['customer_ledger_balance'] ?? 0), 'goodUp' => true);
}
if ($nsCan('cash')) {
	$kpiRows[] = array('name' => 'Total bank balance', 'cur' => (float) ($dashboard['cash_bank_total'] ?? 0), 'prev' => (float) ($nsPrev['cash_bank_total'] ?? 0), 'goodUp' => true);
}
if ($nsCan('profit')) {
	$kpiRows[] = array('name' => 'Gross profit (ex VAT)', 'cur' => (float) ($dashboard['profit_ex_vat'] ?? 0), 'prev' => (float) ($nsPrev['profit_ex_vat'] ?? 0), 'goodUp' => true);
}

// ---- Gauge (cash & bank) ----
$gaugeVal = (float) ($dashboard['cash_bank_total'] ?? 0);
$gaugeScale = max(abs($gaugeVal), abs((float) ($dashboard['revenue_ex_vat'] ?? 0)), 1.0) * 1.25;
$gaugeFrac = max(0.0, min(1.0, ($gaugeVal + $gaugeScale) / (2 * $gaugeScale))); // -scale..+scale mapped 0..1
$gaugeAngle = -90 + ($gaugeFrac * 180); // degrees for needle

// ---- Hero metric strip (primary financial pulse) ----
$nsHeroEntity = 'Operations';
if (!empty($nsCo) && is_array($nsCo)) {
	$nsHeroEntity = (string) (($nsCo['legal_name'] ?? '') !== '' ? $nsCo['legal_name'] : (($nsCo['trade_name'] ?? '') !== '' ? $nsCo['trade_name'] : $nsHeroEntity));
}
$nsHeroCatalog = array(
	'cash' => array('label' => 'Cash & bank', 'cur' => (float) ($dashboard['cash_bank_total'] ?? 0), 'prev' => (float) ($nsPrev['cash_bank_total'] ?? 0), 'goodUp' => true, 'need' => 'cash'),
	'sales' => array('label' => 'Sales (ex VAT)', 'cur' => (float) ($dashboard['revenue_ex_vat'] ?? 0), 'prev' => (float) ($nsPrev['revenue_ex_vat'] ?? 0), 'goodUp' => true, 'need' => 'sales'),
	'profit' => array('label' => 'Gross profit', 'cur' => (float) ($dashboard['profit_ex_vat'] ?? 0), 'prev' => (float) ($nsPrev['profit_ex_vat'] ?? 0), 'goodUp' => true, 'need' => 'profit'),
	'ar' => array('label' => 'Receivables', 'cur' => (float) ($dashboard['customer_ledger_balance'] ?? 0), 'prev' => (float) ($nsPrev['customer_ledger_balance'] ?? 0), 'goodUp' => true, 'need' => 'ar'),
	'ap' => array('label' => 'Payables', 'cur' => (float) ($dashboard['payable_balance'] ?? 0), 'prev' => (float) ($nsPrev['payable_balance'] ?? 0), 'goodUp' => false, 'need' => 'ap'),
	'purchases' => array('label' => 'Purchases (ex VAT)', 'cur' => (float) ($dashboard['purchase_ex_vat'] ?? 0), 'prev' => (float) ($nsPrev['purchase_ex_vat'] ?? 0), 'goodUp' => false, 'need' => 'purchases'),
	'vat' => array('label' => 'Net VAT', 'cur' => (float) ($dashboard['vat_net_payable'] ?? 0), 'prev' => (float) ($nsPrev['vat_net_payable'] ?? 0), 'goodUp' => false, 'need' => 'vat'),
	'orders' => array('label' => 'Completed orders', 'cur' => (float) ($dashboard['order_count'] ?? 0), 'prev' => (float) ($nsPrev['order_count'] ?? 0), 'goodUp' => true, 'need' => 'sales', 'money' => false),
	'due' => array('label' => 'Due on orders', 'cur' => (float) ($dashboard['receivable_due_orders'] ?? 0), 'prev' => (float) ($nsPrev['receivable_due_orders'] ?? 0), 'goodUp' => true, 'need' => 'ar'),
	'inventory' => array('label' => 'Inventory value', 'cur' => (float) ($dashboard['inventory_value'] ?? 0), 'prev' => 0.0, 'goodUp' => true, 'need' => 'inventory'),
	'open_po' => array('label' => 'Open purchase orders', 'cur' => (float) $remOpenPO, 'prev' => 0.0, 'goodUp' => false, 'need' => 'purchases', 'money' => false),
	'staff_open' => array('label' => 'Open tasks', 'cur' => 0.0, 'prev' => 0.0, 'goodUp' => false, 'need' => 'hr_tasks', 'money' => false),
	'staff_done' => array('label' => 'Tasks completed', 'cur' => 0.0, 'prev' => 0.0, 'goodUp' => true, 'need' => 'hr_tasks', 'money' => false),
	'staff_overdue' => array('label' => 'Overdue tasks', 'cur' => 0.0, 'prev' => 0.0, 'goodUp' => false, 'need' => 'hr_tasks', 'money' => false),
	'staff_busy' => array('label' => 'Staff busy now', 'cur' => 0.0, 'prev' => 0.0, 'goodUp' => true, 'need' => 'hr_tasks', 'money' => false),
);
// Fill HR hero metrics early when needed (process-flow summary is loaded later for exec too).
if ($nsCan('hr_tasks')) {
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
		$pfRangeHero = array('from' => (int) $date_from, 'to' => (int) $date_to);
		if (function_exists('epc_pf_monitor_summary')) {
			$pfH = epc_pf_monitor_summary($db_link, $pfRangeHero);
			$nsHeroCatalog['staff_open']['cur'] = (float) ($pfH['open'] ?? 0);
			$nsHeroCatalog['staff_done']['cur'] = (float) ($pfH['done'] ?? 0);
			$nsHeroCatalog['staff_overdue']['cur'] = (float) ($pfH['overdue'] ?? 0);
		}
		if (function_exists('epc_pf_workforce_data')) {
			$wfH = epc_pf_workforce_data($db_link, $pfRangeHero);
			$nsHeroCatalog['staff_busy']['cur'] = (float) ($wfH['busy'] ?? 0);
			if ($nsHeroCatalog['staff_done']['cur'] <= 0) {
				$nsHeroCatalog['staff_done']['cur'] = (float) ($wfH['doneTotal'] ?? 0);
			}
		}
	} catch (Throwable $e) {
	}
}
$nsHeroMetrics = array();
foreach ((array) ($nsProfile['hero'] ?? array()) as $hKey) {
	if (!isset($nsHeroCatalog[$hKey])) {
		continue;
	}
	$hm = $nsHeroCatalog[$hKey];
	if (!$nsCan((string) ($hm['need'] ?? 'sales'))) {
		continue;
	}
	$hm['money'] = array_key_exists('money', $hm) ? (bool) $hm['money'] : true;
	$nsHeroMetrics[] = $hm;
}
$nsRoleLabel = (string) ($nsProfile['label'] ?? 'Finance centre');
$nsRoleSub = (string) ($nsProfile['subtitle'] ?? 'Live KPIs for your role and the selected period.');
// Theme URL — prefer PHP proxy on platform hosts (nginx often 404s /cp/*.css).
$nsCssHref = '/content/shop/finance/epc_erp_dashboard_premium_css.php';
if (function_exists('epc_erp_shell_asset_href')) {
	$nsCssHref = epc_erp_shell_asset_href(
		'/cp/content/shop/finance/erp/theme/erp_dashboard_premium.css',
		'/content/shop/finance/epc_erp_dashboard_premium_css.php'
	);
} elseif (function_exists('epc_erp_shell_use_asset_proxies') && epc_erp_shell_use_asset_proxies()) {
	$nsCssHref = '/content/shop/finance/epc_erp_dashboard_premium_css.php?v=20260720colors2';
} else {
	$nsCssCandidates = array(
		'/cp/content/shop/finance/erp/theme/erp_dashboard_premium.css',
		'/content/shop/finance/erp/theme/erp_dashboard_premium.css',
		'/content/shop/finance/epc_erp_dashboard_premium.css',
	);
	foreach ($nsCssCandidates as $c) {
		$abs = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/') . $c;
		if (is_file($abs)) { $nsCssHref = $c . '?v=20260720colors2'; break; }
	}
	if (strpos($nsCssHref, '?') === false) {
		$nsCssHref .= (strpos($nsCssHref, '.php') !== false ? '?v=20260720colors2' : '?v=20260720colors2');
	}
}
?>
<link rel="stylesheet" href="<?php echo epc_erp_h($nsCssHref); ?>">

<div class="ns-dash" data-dashboard-profile="<?php echo epc_erp_h($nsRole); ?>">
	<div class="ns-hero">
		<div class="ns-hero-panel">
			<div class="ns-hero-kicker"><i class="fa <?php echo epc_erp_h((string) ($nsProfile['icon'] ?? 'fa-dashboard')); ?>"></i> <?php echo epc_erp_h($nsRoleLabel); ?></div>
			<h2 class="ns-hero-title"><?php echo epc_erp_h($nsHeroEntity); ?></h2>
			<p class="ns-hero-sub"><?php echo epc_erp_h($nsRoleSub); ?></p>
			<div class="ns-hero-meta">
				<span class="ns-chip"><i class="fa fa-calendar"></i> <?php echo epc_erp_h($date_from_str); ?> → <?php echo epc_erp_h($date_to_str); ?></span>
				<span class="ns-chip"><i class="fa fa-money"></i> <?php echo epc_erp_h($nsCurrency); ?></span>
				<span class="ns-chip"><i class="fa fa-user"></i> <?php echo epc_erp_h($nsRoleLabel); ?></span>
				<?php if (!$nsCan('profit')): ?>
				<span class="ns-chip" title="Profit and margin are hidden for this profile"><i class="fa fa-lock"></i> Profit restricted</span>
				<?php endif; ?>
			</div>
		</div>
		<div class="ns-hero-metrics">
			<?php foreach ($nsHeroMetrics as $hm): ?>
				<div class="ns-metric3d">
					<div class="ml"><?php echo epc_erp_h($hm['label']); ?></div>
					<div class="mv" data-ns-count="<?php echo epc_erp_h((string) $hm['cur']); ?>" data-ns-prefix=""><?php
						echo !empty($hm['money']) ? $nsMoney($hm['cur']) : number_format((float) $hm['cur'], 0);
					?></div>
					<div class="md"><?php echo $nsChange($hm['cur'], $hm['prev'], $hm['goodUp']); ?> vs prior period</div>
				</div>
			<?php endforeach; ?>
		</div>
	</div>

	<div class="ns-tiles">
		<?php foreach ($tiles as $t): ?>
			<a class="ns-tile <?php echo epc_erp_h($t['tone']); ?>" href="<?php echo $t['url']; ?>">
				<i class="fa <?php echo epc_erp_h($t['icon']); ?> ic"></i>
				<span class="tl"><?php echo epc_erp_h($t['label']); ?></span>
			</a>
		<?php endforeach; ?>
	</div>

	<div class="ns-port">
		<div class="bd">
			<?php
			echo epc_dash_shortcuts_render(array(
				'surface' => 'erp',
				'variant' => 'erp',
				'title' => 'Quick actions',
				'ajax_url' => $erpShortcutAjax,
				'csrf' => $erpShortcutCsrf,
				'catalog' => $erpShortcutCatalog,
				'items' => $erpShortcutItems,
			));
			?>
		</div>
	</div>

	<?php if (!empty($opKpis)): ?>
	<div class="ns-port">
		<h4><i class="fa fa-tachometer"></i> Operational KPIs <span style="font-weight:400;color:var(--ns-muted);font-size:11px;">— live, period <?php echo epc_erp_h($date_from_str); ?> to <?php echo epc_erp_h($date_to_str); ?></span>
			<a href="#ns-industry-controls" style="float:right;font-weight:400;font-size:11px;">Industry controls &darr;</a>
		</h4>
		<div class="bd">
			<div class="ns-kpi-grid">
				<?php foreach ($opKpis as $k): $hc = epc_erp_h($k['health']); ?>
					<div class="ns-kpi-card <?php echo $hc; ?>" title="<?php echo epc_erp_h($k['hint']); ?>">
						<span class="dot"></span>
						<div class="kl"><?php echo epc_erp_h($k['label']); ?></div>
						<div class="kv"><?php echo epc_erp_h(epc_bos_intel_format((float) $k['value'], (string) $k['format'])); ?></div>
						<div class="kh"><?php echo epc_erp_h($k['hint']); ?></div>
					</div>
				<?php endforeach; ?>
			</div>
		</div>
	</div>
	<?php endif; ?>

	<div class="ns-grid">
		<!-- LEFT: reminders + nav shortcuts -->
		<div class="ns-col-left">
			<div class="ns-port">
				<h4><i class="fa fa-bell-o"></i> Reminders</h4>
				<div class="bd">
					<ul class="ns-rem">
						<?php foreach ($reminders as $r): ?>
							<li>
								<span class="cnt<?php echo $r['n'] === 0 ? ' zero' : ''; ?>"><?php echo (int) $r['n']; ?></span>
								<a href="<?php echo $r['url']; ?>"><?php echo epc_erp_h($r['label']); ?></a>
							</li>
						<?php endforeach; ?>
					</ul>
				</div>
			</div>
			<div class="ns-port ns-nav">
				<h4><i class="fa fa-bars"></i> Navigation shortcuts</h4>
				<div class="bd">
					<?php
					$navTones = array('Lists' => 'ns-mi-teal', 'Transactions' => 'ns-mi-blue', 'Reports' => 'ns-mi-amber');
					foreach ($navGroups as $grp => $links):
						$tone = $navTones[$grp] ?? 'ns-mi-blue';
					?>
						<h5><?php echo $grp; ?></h5>
						<div class="ns-mini-grid">
							<?php foreach ($links as $l): ?>
								<a class="ns-mini" href="<?php echo $l['url']; ?>"><span class="mi <?php echo $tone; ?>"><i class="fa <?php echo epc_erp_h($l['icon']); ?>"></i></span><span class="ml"><?php echo $l['label']; ?></span></a>
							<?php endforeach; ?>
						</div>
					<?php endforeach; ?>
				</div>
			</div>
		</div>

		<!-- CENTER: KPI table + financials -->
		<div class="ns-col-mid">
			<?php if (!empty($kpiRows)): ?>
			<div class="ns-port">
				<h4><i class="fa fa-tachometer"></i> Key performance indicators</h4>
				<div class="bd" style="padding:0">
					<table class="ns-kpi-tbl">
						<thead><tr><th>Indicator</th><th>Current</th><th>Previous</th><th>Change</th></tr></thead>
						<tbody>
							<?php foreach ($kpiRows as $k): ?>
								<tr>
									<td><?php echo epc_erp_h($k['name']); ?></td>
									<td><?php echo $nsMoney($k['cur']); ?></td>
									<td><?php echo $nsMoney($k['prev']); ?></td>
									<td><?php echo $nsChange($k['cur'], $k['prev'], $k['goodUp']); ?></td>
								</tr>
							<?php endforeach; ?>
						</tbody>
					</table>
				</div>
			</div>
			<?php endif; ?>
			<?php if ($nsCan('financials')): ?>
			<div class="ns-port">
				<h4><i class="fa fa-money"></i> Financials</h4>
				<div class="bd">
					<?php
					$rev = (float) ($dashboard['revenue_ex_vat'] ?? 0);
					$prof = (float) ($dashboard['profit_ex_vat'] ?? 0);
					$gpPct = $rev > 0.005 ? ($prof / $rev) * 100.0 : 0.0;
					$netPl = (float) ($dashboard_pl['net_profit'] ?? 0);
					$fin = array();
					if ($nsCan('profit')) {
						$fin[] = array('l' => 'Gross profit %', 'v' => number_format($gpPct, 1) . '%');
						$fin[] = array('l' => 'Margin (ex VAT)', 'v' => $nsMoney($prof) . ' ' . $nsCurrency);
						$fin[] = array('l' => 'GL net profit', 'v' => $nsMoney($netPl) . ' ' . $nsCurrency);
					}
					if ($nsCan('cash')) {
						$fin[] = array('l' => 'Cash &amp; bank', 'v' => $nsMoney((float) ($dashboard['cash_bank_total'] ?? 0)) . ' ' . $nsCurrency);
					}
					if ($nsCan('vat')) {
						$fin[] = array('l' => 'Net VAT', 'v' => $nsMoney((float) ($dashboard['vat_net_payable'] ?? 0)) . ' ' . $nsCurrency);
					}
					if ($nsCan('sales')) {
						$fin[] = array('l' => 'Sales incl. VAT', 'v' => $nsMoney((float) ($dashboard['sales_incl_vat'] ?? 0)) . ' ' . $nsCurrency);
						$fin[] = array('l' => 'Completed orders', 'v' => (string) (int) ($dashboard['order_count'] ?? 0));
					}
					if ($nsCan('ar')) {
						$fin[] = array('l' => 'Due on orders', 'v' => $nsMoney((float) ($dashboard['receivable_due_orders'] ?? 0)) . ' ' . $nsCurrency);
					}
					?>
					<div class="ns-fin">
						<?php foreach ($fin as $f): ?>
							<div class="cell"><div class="l"><?php echo $f['l']; ?></div><div class="v"><?php echo $f['v']; ?></div></div>
						<?php endforeach; ?>
					</div>
				</div>
			</div>
			<?php endif; ?>
		</div>

		<!-- RIGHT: gauge + aging chart -->
		<div class="ns-col-right">
			<?php if ($nsCan('gauge') && $nsCan('cash')): ?>
			<div class="ns-port">
				<h4><i class="fa fa-dashboard"></i> KPI meter — Total bank balance</h4>
				<div class="bd">
					<div class="ns-gauge">
						<svg viewBox="0 0 200 120" width="100%" height="120">
							<defs>
								<linearGradient id="nsGaugeGrad" x1="0%" y1="0%" x2="100%" y2="0%">
									<stop offset="0%" stop-color="#0f766e"/>
									<stop offset="55%" stop-color="#1d4f91"/>
									<stop offset="100%" stop-color="#b45309"/>
								</linearGradient>
							</defs>
							<path d="M20 110 A80 80 0 0 1 180 110" fill="none" stroke="#d9e4ec" stroke-width="16" stroke-linecap="round"/>
							<path d="M20 110 A80 80 0 0 1 180 110" fill="none" stroke="url(#nsGaugeGrad)" stroke-width="16" stroke-linecap="round"
								stroke-dasharray="<?php echo number_format($gaugeFrac * 251.3, 1); ?> 251.3"/>
							<g transform="rotate(<?php echo number_format($gaugeAngle, 1); ?> 100 110)">
								<line x1="100" y1="110" x2="100" y2="40" stroke="#102a43" stroke-width="3"/>
							</g>
							<circle cx="100" cy="110" r="7" fill="#102a43"/>
							<circle cx="100" cy="110" r="3" fill="#1d4f91"/>
						</svg>
						<div class="gval" style="color:<?php echo $gaugeVal >= 0 ? '#1a7f4b' : '#b42318'; ?>"><?php echo $nsMoney($gaugeVal); ?></div>
						<div class="gsub"><?php echo $nsCurrency; ?> · live cash &amp; bank position</div>
					</div>
				</div>
			</div>
			<?php endif; ?>
			<?php if ($nsCan('aging_ar')): ?>
			<div class="ns-port">
				<h4><i class="fa fa-bar-chart"></i> A/R aging — graphical</h4>
				<div class="bd">
					<div class="ns-chart-wrap"><canvas id="nsChartAr" aria-label="A/R aging chart"></canvas></div>
					<div class="ns-total">Total receivable: <strong><?php echo $nsMoney($nsAr['grand']); ?> <?php echo $nsCurrency; ?></strong></div>
				</div>
			</div>
			<?php endif; ?>
		</div>
	</div>
</div>

<?php
/* ------------------------------------------------------------------ *
 * Unified executive + industry-intelligence cockpit.
 * Folds the former Executive dashboard and Industry intelligence tabs
 * into the main dashboard so /erp/ is the single analytics view.
 * All figures are tenant-scoped; empty tenants render empty, not error.
 * ------------------------------------------------------------------ */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_exec_dashboard.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_intelligence.php';

$nsCsrf = isset($csrf) ? (string) $csrf : '';
$nsTrend = array();
$nsTopSup = array();
$nsAlerts = array('danger' => 0, 'warning' => 0, 'info' => 0, 'default' => 0, 'total' => 0);
$nsControls = array();
$nsCtrlState = array();
$nsIntelCtx = array('pack_label' => '', 'profile_label' => '');
try { $nsTrend = epc_exec_trend($db_link, 6); } catch (Throwable $e) { $nsTrend = array(); }
try { $nsTopSup = epc_exec_top_suppliers($db_link, 5); } catch (Throwable $e) { $nsTopSup = array(); }
try { $nsAlerts = epc_exec_planning_alerts($db_link); } catch (Throwable $e) {}
try {
	$nsIntelCtx = epc_bos_intel_context($db_link);
	$nsControls = epc_bos_intel_controls($db_link, $nsIntelCtx);
	$nsCtrlState = epc_bos_intel_control_state($db_link);
} catch (Throwable $e) { $nsControls = array(); }

$nsTrendMax = 0.0;
foreach ($nsTrend as $t) {
	$nsTrendMax = max($nsTrendMax, (float) $t['revenue'], (float) $t['profit']);
}
if ($nsTrendMax <= 0) { $nsTrendMax = 1.0; }

$nsCtrlDone = 0;
foreach ($nsControls as $c) {
	if (!empty($nsCtrlState[$c['code']])) { $nsCtrlDone++; }
}
$nsIndustryLabel = '';
if (!empty($nsIntelCtx['pack_label'])) { $nsIndustryLabel = (string) $nsIntelCtx['pack_label']; }
elseif (!empty($nsIntelCtx['profile_label'])) { $nsIndustryLabel = (string) $nsIntelCtx['profile_label']; }
if ($nsIndustryLabel === '') { $nsIndustryLabel = 'General (no industry pack applied)'; }
$nsCtrlHealth = array('good' => '#107c10', 'warn' => '#c19c00', 'bad' => '#a4262c', 'info' => '#0078d4');

/* ---- Process-flow task analytics (defensive; degrades to empty) ---- */
$pfSummary = array('open' => 0, 'done' => 0, 'overdue' => 0, 'avg_cycle_hours' => 0.0, 'by_department' => array(), 'headcount' => 0);
$pfTop = array();
$pfBusy = 0;
$pfDoneTotal = 0;
$pfDeptName = function ($code) { return $code === '' ? 'Unassigned' : ucfirst((string) $code); };
try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
	if (function_exists('epc_erp_staff_department_name')) {
		$pfDeptName = function ($code) {
			$code = (string) $code;
			return $code === '' ? 'Unassigned' : (epc_erp_staff_department_name($code) ?: ucfirst($code));
		};
	}
	$pfRange = array('from' => (int) $date_from, 'to' => (int) $date_to);
	if (function_exists('epc_pf_monitor_summary')) {
		$pfSummary = epc_pf_monitor_summary($db_link, $pfRange);
	}
	if (function_exists('epc_pf_workforce_data')) {
		$wf = epc_pf_workforce_data($db_link, $pfRange);
		$pfBusy = (int) ($wf['busy'] ?? 0);
		$pfDoneTotal = (int) ($wf['doneTotal'] ?? 0);
		$people = $wf['staff'] ?? array();
		usort($people, function ($a, $b) { return (int) $b['done'] <=> (int) $a['done']; });
		foreach ($people as $p) {
			if ((int) $p['done'] <= 0) { continue; }
			$pfTop[] = $p;
			if (count($pfTop) >= 8) { break; }
		}
	}
} catch (Throwable $e) {
}
$pfDeptRows = $pfSummary['by_department'] ?? array();
arsort($pfDeptRows);
$pfDeptMax = $pfDeptRows ? max($pfDeptRows) : 0;
$pfTopMax = $pfTop ? max(array_map(function ($p) { return (int) $p['done']; }, $pfTop)) : 0;
$pfHasTasks = (((int) $pfSummary['open']) + ((int) $pfSummary['done']) + ((int) $pfSummary['overdue']) + $pfDoneTotal) > 0;
$pfUrl = function ($view = 'monitor') use ($erpUrl, $date_from_str, $date_to_str) {
	return epc_erp_h(epc_erp_tab_url($erpUrl, 'processflow', $date_from_str, $date_to_str) . '&pf_view=' . $view);
};
$nsTrendLabels = array();
$nsTrendRev = array();
$nsTrendProf = array();
foreach ($nsTrend as $t) {
	$nsTrendLabels[] = (string) ($t['label'] ?? '');
	$nsTrendRev[] = (float) ($t['revenue'] ?? 0);
	$nsTrendProf[] = (float) ($t['profit'] ?? 0);
}
$nsShowExec = $nsCan('exec');
$nsShowProfitTrend = $nsCan('profit');
$nsShowSuppliers = $nsCan('suppliers');
$nsShowDemoSeed = $nsCan('demo_seed');
$nsShowHrTasks = $nsCan('hr_tasks');
?>

<?php if ($nsShowExec): ?>
<div class="ns-dash ns-exec">
	<h3 class="ns-exec-h"><i class="fa fa-dashboard"></i> Executive cockpit — full-system analytics</h3>
	<div id="epc_erp_msg" class="alert" style="display:none;"></div>
	<?php if ($nsShowDemoSeed): ?>
	<div style="margin-bottom:14px;">
		<form data-bos-action="demo_seed_sales" style="display:inline-block;margin:0;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($nsCsrf); ?>">
			<button type="submit" class="btn btn-sm btn-primary"><i class="fa fa-database"></i> Generate sample sales</button>
		</form>
		<form data-bos-action="demo_clear_sales" style="display:inline-block;margin:0;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($nsCsrf); ?>">
			<button type="submit" class="btn btn-sm btn-default"><i class="fa fa-eraser"></i> Clear sample sales</button>
		</form>
		<span class="text-muted" style="margin-left:8px;font-size:12px;">Seeds 6 months of completed orders (tagged, re-runnable) so the revenue trend and KPIs populate.</span>
	</div>
	<?php endif; ?>

	<div class="ns-exec-grid">
		<div class="ns-port">
			<h4><i class="fa fa-line-chart"></i> <?php echo $nsShowProfitTrend ? 'Revenue &amp; profit — last 6 months' : 'Revenue — last 6 months'; ?></h4>
			<div class="bd">
				<div class="ns-chart-wrap tall"><canvas id="nsChartTrend" aria-label="Revenue trend"></canvas></div>
				<div class="ns-leg"><span class="sq" style="background:#1d4f91;"></span>Revenue<?php if ($nsShowProfitTrend): ?> &nbsp; <span class="sq" style="background:#0f766e;"></span>Profit (ex-VAT)<?php endif; ?></div>
			</div>
		</div>
		<div class="ns-port">
			<h4><i class="fa fa-exclamation-triangle"></i> Planning alerts</h4>
			<div class="bd">
				<table class="table table-condensed table-bordered" style="margin-bottom:8px;">
					<tbody>
						<tr class="danger"><th>Stock-out / critical</th><td class="text-right"><?php echo (int) $nsAlerts['danger']; ?></td></tr>
						<tr class="warning"><th>Below safety stock</th><td class="text-right"><?php echo (int) $nsAlerts['warning']; ?></td></tr>
						<tr class="info"><th>Excess stock</th><td class="text-right"><?php echo (int) $nsAlerts['info']; ?></td></tr>
						<tr><th>Dead stock</th><td class="text-right"><?php echo (int) $nsAlerts['default']; ?></td></tr>
						<tr><th>Total exceptions</th><td class="text-right"><strong><?php echo (int) $nsAlerts['total']; ?></strong></td></tr>
					</tbody>
				</table>
				<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'order_planning', $date_from_str, $date_to_str) . '&opl_view=exceptions'); ?>">View exceptions &raquo;</a>
			</div>
		</div>
	</div>

	<div class="ns-exec-grid" style="margin-top:16px;">
		<?php if ($nsShowSuppliers): ?>
		<div class="ns-port">
			<h4><i class="fa fa-truck"></i> Top suppliers by spend</h4>
			<div class="bd">
				<?php if (empty($nsTopSup)): ?>
					<p class="text-muted">No supplier spend recorded yet.</p>
				<?php else: ?>
				<table class="table table-condensed table-bordered table-hover" style="margin-bottom:0;">
					<thead><tr><th>Supplier</th><th class="text-center">Rating</th><th class="text-right">Spend</th><th class="text-right">POs</th><th class="text-right">Score</th></tr></thead>
					<tbody>
					<?php foreach ($nsTopSup as $s): ?>
						<tr>
							<td><?php echo epc_erp_h($s['name']); ?></td>
							<td class="text-center"><?php echo epc_erp_h($s['rating']); ?></td>
							<td class="text-right"><?php echo epc_erp_money($s['spend']); ?></td>
							<td class="text-right"><?php echo (int) $s['po_count']; ?></td>
							<td class="text-right"><?php echo epc_erp_money($s['score']); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				<?php endif; ?>
			</div>
		</div>
		<?php endif; ?>
		<div class="ns-port">
			<h4><i class="fa fa-link"></i> Quick links</h4>
			<div class="bd">
				<div class="list-group" style="margin-bottom:0;">
					<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'ai_advisor', $date_from_str, $date_to_str)); ?>"><i class="fa fa-magic"></i> AI advisor &amp; forecasts</a>
					<?php if ($nsShowProfitTrend): ?>
					<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'pl', $date_from_str, $date_to_str)); ?>"><i class="fa fa-bar-chart"></i> Profit &amp; loss</a>
					<?php endif; ?>
					<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'order_planning', $date_from_str, $date_to_str)); ?>"><i class="fa fa-cubes"></i> Order planning</a>
					<?php if ($nsShowSuppliers): ?>
					<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'supplier_portal', $date_from_str, $date_to_str)); ?>"><i class="fa fa-handshake-o"></i> Supplier portal</a>
					<?php endif; ?>
				</div>
			</div>
		</div>
	</div>

	<?php if ($nsShowHrTasks): ?>
	<h3 class="ns-exec-h" id="ns-task-analytics" style="margin-top:22px;"><i class="fa fa-sitemap"></i> Task analytics — process flow across every department
		<small class="text-muted" style="font-weight:400;">· auto-tracked customer orders, purchase orders &amp; your own processes</small>
		<a href="<?php echo $pfUrl('monitor'); ?>" class="btn btn-xs btn-default" style="float:right;"><i class="fa fa-external-link"></i> Open process flow</a>
	</h3>
	<?php if (!$pfHasTasks): ?>
	<div class="ns-port"><div class="bd">
		<p class="text-muted" style="margin:0;">No tasks tracked yet. Customer orders, purchase orders and your own processes auto-create cases and appear here as soon as work starts flowing. <a href="<?php echo $pfUrl('monitor'); ?>">Open process flow &raquo;</a></p>
	</div></div>
	<?php else: ?>
	<div class="ns-pf-kpis">
		<div class="ns-pf-card"><div class="v"><?php echo (int) $pfSummary['open']; ?></div><div class="l">Open tasks</div></div>
		<div class="ns-pf-card <?php echo ((int) $pfSummary['overdue'] > 0) ? 'bad' : ''; ?>"><div class="v"><?php echo (int) $pfSummary['overdue']; ?></div><div class="l">Overdue (SLA)</div></div>
		<div class="ns-pf-card good"><div class="v"><?php echo (int) $pfDoneTotal; ?></div><div class="l">Tasks completed in period</div></div>
		<div class="ns-pf-card"><div class="v"><?php echo (int) round((float) $pfSummary['avg_cycle_hours']); ?><small>h</small></div><div class="l">Avg cycle time</div></div>
		<div class="ns-pf-card"><div class="v"><?php echo (int) $pfBusy; ?><small>/<?php echo (int) ($pfSummary['headcount'] ?: 0); ?></small></div><div class="l">Staff busy now</div></div>
	</div>
	<div class="ns-exec-grid" style="margin-top:16px;">
		<div class="ns-port">
			<h4><i class="fa fa-building-o"></i> Open workload by department</h4>
			<div class="bd">
				<?php if (empty($pfDeptRows)): ?>
					<p class="text-muted" style="margin:0;">No open tasks.</p>
				<?php else: foreach ($pfDeptRows as $code => $cnt): $w = $pfDeptMax > 0 ? max(4, round(($cnt / $pfDeptMax) * 100)) : 0; ?>
					<div class="ns-pf-bar">
						<div class="nm"><?php echo epc_erp_h($pfDeptName((string) $code)); ?></div>
						<div class="tr"><div class="fl" style="width:<?php echo (int) $w; ?>%;"></div></div>
						<div class="ct"><?php echo (int) $cnt; ?></div>
					</div>
				<?php endforeach; endif; ?>
			</div>
		</div>
		<div class="ns-port">
			<h4><i class="fa fa-trophy"></i> Top performers <small class="text-muted" style="font-weight:400;">(tasks completed)</small></h4>
			<div class="bd">
				<?php if (empty($pfTop)): ?>
					<p class="text-muted" style="margin:0;">No completed tasks in this period yet.</p>
				<?php else: $rank = 0; foreach ($pfTop as $p): $rank++; $w = $pfTopMax > 0 ? max(4, round(((int) $p['done'] / $pfTopMax) * 100)) : 0; ?>
					<div class="ns-pf-perf">
						<span class="rk"><?php echo (int) $rank; ?></span>
						<img src="<?php echo epc_erp_h((string) $p['avatar']); ?>" alt="" />
						<div class="who"><span class="n"><?php echo epc_erp_h((string) $p['name']); ?></span><span class="d"><?php echo epc_erp_h((string) $p['deptName'] . ($p['location'] !== '' ? ' · ' . $p['location'] : '')); ?></span></div>
						<div class="tr"><div class="fl" style="width:<?php echo (int) $w; ?>%;"></div></div>
						<span class="ct"><?php echo (int) $p['done']; ?></span>
					</div>
				<?php endforeach; endif; ?>
			</div>
		</div>
	</div>
	<?php endif; /* pfHasTasks */ ?>
	<?php endif; /* nsShowHrTasks inside exec */ ?>

	<?php if (!empty($nsControls)): ?>
	<h3 class="ns-exec-h" id="ns-industry-controls" style="margin-top:22px;"><i class="fa fa-check-square-o"></i> Industry intelligence — recommended controls
		<small class="text-muted" style="font-weight:400;">· <?php echo epc_erp_h($nsIndustryLabel); ?> · <?php echo $nsCtrlDone; ?>/<?php echo count($nsControls); ?> in place</small>
	</h3>
	<div class="ns-port">
		<div class="bd">
			<p class="text-muted" style="margin-top:0;">Best-practice operational controls for your industry. Tick the ones you have in place; the checklist is saved per tenant.</p>
			<table class="table table-bordered table-condensed" style="margin-bottom:0;">
				<thead><tr><th style="width:70px;">In place</th><th>Control</th><th>What to do</th></tr></thead>
				<tbody>
				<?php foreach ($nsControls as $c): $checked = !empty($nsCtrlState[$c['code']]); ?>
					<tr class="<?php echo $checked ? 'success' : ''; ?>">
						<td style="text-align:center;">
							<form data-bos-action="bos_intel_toggle_control" style="margin:0;">
								<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($nsCsrf); ?>">
								<input type="hidden" name="code" value="<?php echo epc_erp_h($c['code']); ?>">
								<input type="hidden" name="checked" value="<?php echo $checked ? '0' : '1'; ?>">
								<button type="submit" class="btn btn-xs <?php echo $checked ? 'btn-success' : 'btn-default'; ?>">
									<i class="fa fa-<?php echo $checked ? 'check-square-o' : 'square-o'; ?>"></i>
								</button>
							</form>
						</td>
						<td><strong><?php echo epc_erp_h($c['title']); ?></strong></td>
						<td><small class="text-muted"><?php echo epc_erp_h($c['desc']); ?></small></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		</div>
	</div>
	<?php endif; ?>
</div>
<?php endif; /* nsShowExec */ ?>

<?php if (!$nsShowExec && $nsShowHrTasks): ?>
<div class="ns-dash ns-exec">
	<h3 class="ns-exec-h" id="ns-task-analytics"><i class="fa fa-sitemap"></i> Task analytics
		<a href="<?php echo $pfUrl('monitor'); ?>" class="btn btn-xs btn-default" style="float:right;"><i class="fa fa-external-link"></i> Open process flow</a>
	</h3>
	<div class="ns-pf-kpis">
		<div class="ns-pf-card"><div class="v"><?php echo (int) $pfSummary['open']; ?></div><div class="l">Open tasks</div></div>
		<div class="ns-pf-card <?php echo ((int) $pfSummary['overdue'] > 0) ? 'bad' : ''; ?>"><div class="v"><?php echo (int) $pfSummary['overdue']; ?></div><div class="l">Overdue (SLA)</div></div>
		<div class="ns-pf-card good"><div class="v"><?php echo (int) $pfDoneTotal; ?></div><div class="l">Tasks completed in period</div></div>
		<div class="ns-pf-card"><div class="v"><?php echo (int) $pfBusy; ?></div><div class="l">Staff busy now</div></div>
	</div>
</div>
<?php endif; ?>

<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js" crossorigin="anonymous"></script>
<script>
(function () {
	var arLabels = <?php echo json_encode(array_values($nsAr['labels'])); ?>;
	var arTotals = <?php echo json_encode(array_map('floatval', array_values($nsAr['totals']))); ?>;
	var trendLabels = <?php echo json_encode($nsTrendLabels); ?>;
	var trendRev = <?php echo json_encode($nsTrendRev); ?>;
	var trendProf = <?php echo json_encode($nsShowProfitTrend ? $nsTrendProf : array()); ?>;
	var showProfitTrend = <?php echo $nsShowProfitTrend ? 'true' : 'false'; ?>;
	var currency = <?php echo json_encode($nsCurrency); ?>;

	function nsFmt(n) {
		try { return Number(n).toLocaleString(undefined, { maximumFractionDigits: 0 }); }
		catch (e) { return String(n); }
	}

	function drawCharts() {
		if (typeof Chart === 'undefined') { return; }
		Chart.defaults.font.family = 'Sora, ui-sans-serif, sans-serif';
		Chart.defaults.color = '#5c6b7a';
		var grid = 'rgba(16,42,67,0.06)';

		var arEl = document.getElementById('nsChartAr');
		if (arEl) {
			new Chart(arEl.getContext('2d'), {
				type: 'bar',
				data: {
					labels: arLabels,
					datasets: [{
						data: arTotals,
						backgroundColor: ['#047857', '#2563eb', '#0f766e', '#b45309', '#b91c1c'],
						borderRadius: 8,
						borderSkipped: false,
						maxBarThickness: 36
					}]
				},
				options: {
					responsive: true,
					maintainAspectRatio: false,
					plugins: {
						legend: { display: false },
						tooltip: {
							callbacks: {
								label: function (ctx) { return currency + ' ' + nsFmt(ctx.parsed.y); }
							}
						}
					},
					scales: {
						x: { grid: { display: false } },
						y: { grid: { color: grid }, ticks: { callback: function (v) { return nsFmt(v); } } }
					},
					animation: { duration: 1100, easing: 'easeOutCubic' }
				}
			});
		}

		var trEl = document.getElementById('nsChartTrend');
		if (trEl) {
			var ctx = trEl.getContext('2d');
			var g = ctx.createLinearGradient(0, 0, 0, 200);
			g.addColorStop(0, 'rgba(29,79,145,0.22)');
			g.addColorStop(1, 'rgba(29,79,145,0.02)');
			var trendDatasets = [
				{
					label: 'Revenue',
					data: trendRev,
					borderColor: '#1d4f91',
					backgroundColor: g,
					fill: true,
					tension: 0.35,
					borderWidth: 2.5,
					pointRadius: 3,
					pointBackgroundColor: '#1d4f91'
				}
			];
			if (showProfitTrend) {
				trendDatasets.push({
					label: 'Profit',
					data: trendProf,
					borderColor: '#0f766e',
					backgroundColor: 'transparent',
					fill: false,
					tension: 0.35,
					borderWidth: 2.5,
					pointRadius: 3,
					pointBackgroundColor: '#0f766e'
				});
			}
			new Chart(ctx, {
				type: 'line',
				data: {
					labels: trendLabels,
					datasets: trendDatasets
				},
				options: {
					responsive: true,
					maintainAspectRatio: false,
					plugins: {
						legend: { display: false },
						tooltip: {
							callbacks: {
								label: function (ctx) { return ctx.dataset.label + ': ' + currency + ' ' + nsFmt(ctx.parsed.y); }
							}
						}
					},
					scales: {
						x: { grid: { color: grid } },
						y: { grid: { color: grid }, ticks: { callback: function (v) { return nsFmt(v); } } }
					},
					animation: { duration: 1200, easing: 'easeOutCubic' }
				}
			});
		}
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', drawCharts);
	} else {
		drawCharts();
	}
})();
</script>
