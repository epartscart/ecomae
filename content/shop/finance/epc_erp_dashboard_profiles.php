<?php
/**
 * Role / profile dashboards for the ERP home.
 *
 * Resolves a dashboard centre (CEO, CFO, Finance, Accounts, Sales, Purchase, …)
 * from the signed-in staff profile, then exposes capability flags so sensitive
 * figures (profit, cash, GL) stay hidden from teams that must not see them.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Dashboard centre definitions.
 *
 * @return array<string,array>
 */
function epc_erp_dashboard_profiles_config(): array
{
	return array(
		'ceo' => array(
			'label' => 'CEO centre',
			'subtitle' => 'Company-wide performance, cash and profit at a glance.',
			'icon' => 'fa-briefcase',
			'capabilities' => array(
				'profit', 'cash', 'sales', 'purchases', 'ar', 'ap', 'vat', 'gl',
				'inventory', 'aging_ar', 'aging_ap', 'exec', 'suppliers', 'hr_tasks',
				'demo_seed', 'financials', 'op_kpis', 'gauge',
			),
			'hero' => array('cash', 'sales', 'profit', 'ar'),
			'op_kpi_keys' => array('revenue', 'gross_margin', 'dso', 'dpo', 'current_ratio', 'ar', 'ap', 'cash', 'inventory'),
			'tiles' => array('pl', 'balance_sheet', 'cash_bank', 'sales_orders'),
			'quick' => array('ext_ifrs', 'ext_vat', 'ext_ct', 'sales_orders', 'purchase_orders', 'gl', 'pl'),
		),
		'cfo' => array(
			'label' => 'CFO centre',
			'subtitle' => 'Liquidity, margins, tax and financial control.',
			'icon' => 'fa-university',
			'capabilities' => array(
				'profit', 'cash', 'sales', 'purchases', 'ar', 'ap', 'vat', 'gl',
				'inventory', 'aging_ar', 'aging_ap', 'exec', 'suppliers',
				'demo_seed', 'financials', 'op_kpis', 'gauge',
			),
			'hero' => array('cash', 'profit', 'ar', 'ap'),
			'op_kpi_keys' => array('revenue', 'gross_margin', 'dso', 'dpo', 'current_ratio', 'ar', 'ap', 'cash', 'inventory'),
			'tiles' => array('pl', 'balance_sheet', 'bank_recon', 'gl'),
			'quick' => array('ext_ifrs', 'ext_vat', 'ext_ct', 'gl', 'vat_return', 'cash_bank', 'pl'),
		),
		'finance' => array(
			'label' => 'Finance centre',
			'subtitle' => 'Cash, AR/AP and statutory finance operations.',
			'icon' => 'fa-money',
			'capabilities' => array(
				'profit', 'cash', 'sales', 'purchases', 'ar', 'ap', 'vat', 'gl',
				'inventory', 'aging_ar', 'aging_ap', 'exec', 'suppliers',
				'demo_seed', 'financials', 'op_kpis', 'gauge',
			),
			'hero' => array('cash', 'sales', 'ar', 'ap'),
			'op_kpi_keys' => array('revenue', 'gross_margin', 'dso', 'dpo', 'ar', 'ap', 'cash', 'inventory'),
			'tiles' => array('balance_sheet', 'gl', 'bank_recon', 'pl'),
			'quick' => array('ext_ifrs', 'ext_vat', 'ext_ct', 'cash_bank', 'gl', 'vat_return', 'receivables', 'payables'),
		),
		'accounts' => array(
			'label' => 'Accounts centre',
			'subtitle' => 'Ledgers, VAT and period close.',
			'icon' => 'fa-book',
			'capabilities' => array(
				'profit', 'cash', 'sales', 'purchases', 'ar', 'ap', 'vat', 'gl',
				'aging_ar', 'aging_ap', 'financials', 'op_kpis', 'gauge',
			),
			'hero' => array('cash', 'sales', 'purchases', 'vat'),
			'op_kpi_keys' => array('revenue', 'gross_margin', 'ar', 'ap', 'cash', 'dso', 'dpo'),
			'tiles' => array('gl', 'pl', 'balance_sheet', 'vat_return'),
			'quick' => array('gl', 'pl', 'balance_sheet', 'vat_return', 'coa', 'cash_bank'),
		),
		'sales' => array(
			'label' => 'Sales centre',
			'subtitle' => 'Orders, pipeline and collections — commercial figures only.',
			'icon' => 'fa-line-chart',
			// Intentionally no profit / cash / GL / purchases / VAT / exec profit view.
			'capabilities' => array(
				'sales', 'ar', 'aging_ar', 'op_kpis',
			),
			'hero' => array('sales', 'ar', 'orders', 'due'),
			'op_kpi_keys' => array('revenue', 'dso', 'ar'),
			'tiles' => array('sales_orders', 'crm', 'receivables', 'aging_ar'),
			'quick' => array('sales_orders', 'crm', 'receivables', 'customers'),
		),
		'purchase' => array(
			'label' => 'Purchase centre',
			'subtitle' => 'Procurement, suppliers and payables.',
			'icon' => 'fa-shopping-basket',
			'capabilities' => array(
				'purchases', 'ap', 'inventory', 'aging_ap', 'suppliers', 'op_kpis',
			),
			'hero' => array('purchases', 'ap', 'inventory', 'open_po'),
			'op_kpi_keys' => array('dpo', 'ap', 'inventory', 'inv_turnover'),
			'tiles' => array('purchase_orders', 'payables', 'three_way_match', 'aging_ap'),
			'quick' => array('purchase_orders', 'payables', 'vendors', 'inventory'),
		),
		'logistics' => array(
			'label' => 'Logistics centre',
			'subtitle' => 'Fulfilment, stock and open order movement.',
			'icon' => 'fa-truck',
			'capabilities' => array(
				'inventory', 'sales', 'purchases', 'op_kpis',
			),
			'hero' => array('orders', 'open_po', 'inventory', 'sales'),
			'op_kpi_keys' => array('inventory', 'inv_turnover', 'revenue'),
			'tiles' => array('fulfilment', 'inventory', 'sales_orders', 'purchase_orders'),
			'quick' => array('fulfilment', 'inventory', 'sales_orders', 'purchase_orders'),
		),
		'hr' => array(
			'label' => 'HR centre',
			'subtitle' => 'People, payroll and department workload.',
			'icon' => 'fa-users',
			'capabilities' => array(
				'hr_tasks', 'op_kpis',
			),
			'hero' => array('staff_open', 'staff_done', 'staff_overdue', 'staff_busy'),
			'op_kpi_keys' => array(),
			'tiles' => array('hr', 'payroll', 'staff', 'workflow'),
			'quick' => array('hr', 'payroll', 'staff', 'workflow'),
		),
		'marketing' => array(
			'label' => 'Marketing centre',
			'subtitle' => 'Campaigns and commercial pipeline support.',
			'icon' => 'fa-bullhorn',
			'capabilities' => array(
				'sales', 'op_kpis',
			),
			'hero' => array('sales', 'orders', 'ar', 'due'),
			'op_kpi_keys' => array('revenue', 'ar'),
			'tiles' => array('marketing', 'crm', 'sales_orders', 'workflow'),
			'quick' => array('marketing', 'crm', 'sales_orders', 'leads'),
		),
		'it' => array(
			'label' => 'IT centre',
			'subtitle' => 'Access, workflow and system health.',
			'icon' => 'fa-laptop',
			'capabilities' => array(
				'hr_tasks',
			),
			'hero' => array('staff_open', 'staff_overdue', 'staff_done', 'staff_busy'),
			'op_kpi_keys' => array(),
			'tiles' => array('workflow', 'staff', 'processflow', 'dashboard'),
			'quick' => array('workflow', 'staff', 'processflow'),
		),
		'admin' => array(
			'label' => 'Administration centre',
			'subtitle' => 'Full operational and financial command view.',
			'icon' => 'fa-shield',
			'capabilities' => array(
				'profit', 'cash', 'sales', 'purchases', 'ar', 'ap', 'vat', 'gl',
				'inventory', 'aging_ar', 'aging_ap', 'exec', 'suppliers', 'hr_tasks',
				'demo_seed', 'financials', 'op_kpis', 'gauge',
			),
			'hero' => array('cash', 'sales', 'profit', 'ar'),
			'op_kpi_keys' => array('revenue', 'gross_margin', 'dso', 'dpo', 'current_ratio', 'ar', 'ap', 'cash', 'inventory'),
			'tiles' => array('pl', 'balance_sheet', 'gl', 'sales_orders'),
			'quick' => array('ext_ifrs', 'ext_vat', 'ext_ct', 'sales_orders', 'purchase_orders', 'gl', 'staff'),
		),
	);
}

/**
 * Map ERP department codes → default dashboard profile.
 *
 * @return array<string,string>
 */
function epc_erp_dashboard_dept_default_map(): array
{
	return array(
		'admin' => 'admin',
		'finance' => 'finance',
		'accounts' => 'accounts',
		'sales' => 'sales',
		'purchase' => 'purchase',
		'logistics' => 'logistics',
		'hr' => 'hr',
		'marketing' => 'marketing',
		'it' => 'it',
	);
}

/**
 * Infer executive profile from free-text job title.
 */
function epc_erp_dashboard_profile_from_job_title(string $jobTitle): string
{
	$t = strtolower(trim($jobTitle));
	if ($t === '') {
		return '';
	}
	if (preg_match('/\b(ceo|chief\s*executive|managing\s*director|\bmd\b|general\s*manager|\bgm\b)\b/', $t)) {
		return 'ceo';
	}
	if (preg_match('/\b(cfo|chief\s*financial|finance\s*director|financial\s*controller)\b/', $t)) {
		return 'cfo';
	}
	if (preg_match('/\b(coo|chief\s*operating)\b/', $t)) {
		return 'ceo';
	}
	if (preg_match('/\b(sales\s*director|head\s*of\s*sales|commercial\s*director)\b/', $t)) {
		return 'sales';
	}
	if (preg_match('/\b(purchase\s*director|procurement\s*director|head\s*of\s*procurement)\b/', $t)) {
		return 'purchase';
	}
	return '';
}

/**
 * Ensure staff profiles can store an explicit dashboard centre override.
 */
function epc_erp_dashboard_profiles_ensure_schema(PDO $db): void
{
	if (!function_exists('epc_erp_staff_ensure_schema')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
	}
	epc_erp_staff_ensure_schema($db);
	try {
		$cols = $db->query('SHOW COLUMNS FROM `epc_erp_staff_profiles` LIKE ' . $db->quote('dashboard_profile'))->fetchAll();
		if (empty($cols)) {
			$db->exec("ALTER TABLE `epc_erp_staff_profiles` ADD COLUMN `dashboard_profile` varchar(32) DEFAULT NULL AFTER `job_title`");
		}
	} catch (Throwable $e) {
		// Table may be unavailable on some doors — resolve still works via dept/title.
	}
}

/**
 * Load staff profile row used for dashboard resolution.
 *
 * @return array{department_code:string,job_title:string,dashboard_profile:string,display_name:string}
 */
function epc_erp_dashboard_staff_context(PDO $db, int $userId = 0): array
{
	$out = array(
		'department_code' => '',
		'job_title' => '',
		'dashboard_profile' => '',
		'display_name' => '',
	);
	try {
		epc_erp_dashboard_profiles_ensure_schema($db);
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		if ($userId <= 0) {
			$userId = (int) DP_User::getUserId();
		}
		if ($userId <= 0) {
			return $out;
		}
		$st = $db->prepare('SELECT `department_code`, `job_title`, `dashboard_profile`, `display_name` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? AND `active` = 1 LIMIT 1');
		$st->execute(array($userId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (is_array($row)) {
			$out['department_code'] = (string) ($row['department_code'] ?? '');
			$out['job_title'] = (string) ($row['job_title'] ?? '');
			$out['dashboard_profile'] = (string) ($row['dashboard_profile'] ?? '');
			$out['display_name'] = (string) ($row['display_name'] ?? '');
		}
		if ($out['department_code'] === '' && function_exists('epc_erp_staff_primary_department')) {
			$out['department_code'] = (string) epc_erp_staff_primary_department($db, $userId);
		}
	} catch (Throwable $e) {
	}
	return $out;
}

/**
 * Resolve the active dashboard profile key for a user.
 */
function epc_erp_dashboard_resolve_profile(PDO $db, int $userId = 0): string
{
	$cfg = epc_erp_dashboard_profiles_config();
	$ctx = epc_erp_dashboard_staff_context($db, $userId);
	$explicit = strtolower(trim($ctx['dashboard_profile']));
	if ($explicit !== '' && isset($cfg[$explicit])) {
		return $explicit;
	}
	$fromTitle = epc_erp_dashboard_profile_from_job_title($ctx['job_title']);
	if ($fromTitle !== '' && isset($cfg[$fromTitle])) {
		return $fromTitle;
	}
	// Full admins without a staff profile get the admin centre.
	if (function_exists('epc_erp_staff_user_is_full_admin')) {
		try {
			if (epc_erp_staff_user_is_full_admin($db, $userId)) {
				$dept = strtolower($ctx['department_code']);
				if ($dept === '' || $dept === 'admin') {
					return 'admin';
				}
			}
		} catch (Throwable $e) {
		}
	}
	$map = epc_erp_dashboard_dept_default_map();
	$dept = strtolower(trim($ctx['department_code']));
	if ($dept !== '' && isset($map[$dept]) && isset($cfg[$map[$dept]])) {
		return $map[$dept];
	}
	return 'finance';
}

/**
 * @return array profile config row + key
 */
function epc_erp_dashboard_profile_meta(PDO $db, int $userId = 0): array
{
	$cfg = epc_erp_dashboard_profiles_config();
	$key = epc_erp_dashboard_resolve_profile($db, $userId);
	// Admins may preview another centre: ?dash_profile=sales
	$preview = strtolower(trim((string) ($_GET['dash_profile'] ?? '')));
	if ($preview !== '' && isset($cfg[$preview]) && function_exists('epc_erp_staff_user_is_full_admin')) {
		try {
			if (epc_erp_staff_user_is_full_admin($db, $userId)) {
				$key = $preview;
			}
		} catch (Throwable $e) {
		}
	}
	$meta = $cfg[$key] ?? $cfg['finance'];
	$meta['key'] = $key;
	$ctx = epc_erp_dashboard_staff_context($db, $userId);
	$meta['department_code'] = $ctx['department_code'];
	$meta['job_title'] = $ctx['job_title'];
	$meta['display_name'] = $ctx['display_name'];
	return $meta;
}

function epc_erp_dashboard_can(array $profileMeta, string $capability): bool
{
	$caps = isset($profileMeta['capabilities']) && is_array($profileMeta['capabilities'])
		? $profileMeta['capabilities']
		: array();
	return in_array($capability, $caps, true);
}

/**
 * Persist an explicit dashboard centre on a staff profile (admin control).
 */
function epc_erp_dashboard_profile_set(PDO $db, int $staffUserId, string $profileKey): bool
{
	$cfg = epc_erp_dashboard_profiles_config();
	$profileKey = strtolower(trim($profileKey));
	if ($profileKey !== '' && !isset($cfg[$profileKey])) {
		return false;
	}
	epc_erp_dashboard_profiles_ensure_schema($db);
	$st = $db->prepare('UPDATE `epc_erp_staff_profiles` SET `dashboard_profile` = ? WHERE `user_id` = ?');
	$val = ($profileKey === '') ? null : $profileKey;
	return $st->execute(array($val, $staffUserId));
}
