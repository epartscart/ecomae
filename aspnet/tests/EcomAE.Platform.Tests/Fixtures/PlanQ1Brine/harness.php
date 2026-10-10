<?php
// PHP 8.3 goldens for plan Q1-brine (portal ERP module registry). Leftover nav/staff/voucher parents stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function brine_stubs(): void
{
	if (!function_exists('epc_platform_erp_is_request')) {
		function epc_platform_erp_is_request(): bool
		{
			return !empty($GLOBALS['BRINE_PLATFORM_REQ']);
		}
	}
	if (!function_exists('epc_platform_erp_is_active')) {
		function epc_platform_erp_is_active(): bool
		{
			return !empty($GLOBALS['BRINE_PLATFORM_ACTIVE']);
		}
	}
	if (!function_exists('epc_portal_load_site_settings')) {
		function epc_portal_load_site_settings()
		{
			return $GLOBALS['BRINE_SETTINGS'] ?? array();
		}
	}
	if (!function_exists('epc_portal_resolve_access_mode')) {
		function epc_portal_resolve_access_mode($settings)
		{
			return (string) (($settings['access_mode'] ?? '') !== '' ? $settings['access_mode'] : ($GLOBALS['BRINE_ACCESS'] ?? 'full'));
		}
	}
	if (!function_exists('epc_erp_nav_areas_config')) {
		function epc_erp_nav_areas_config(): array
		{
			return $GLOBALS['BRINE_NAV'] ?? array(
				'overview' => array('tabs' => array('dashboard' => 1, 'workflow' => 1)),
				'sales' => array('tabs' => array('orders' => 1, 'invoices' => 1)),
				'finance' => array('tabs' => array('gl' => 1, 'cash_bank' => 1, 'procurement_link' => 1)),
				'tax' => array('tabs' => array('vat' => 1)),
				'setup' => array('tabs' => array('admin' => 1)),
				'enterprise' => array('tabs' => array('org' => 1)),
				'common' => array('tabs' => array('search' => 1)),
				'risk' => array('tabs' => array('risk' => 1)),
			);
		}
	}
	if (!function_exists('epc_erp_staff_all_tabs')) {
		function epc_erp_staff_all_tabs(): array
		{
			return array('dashboard', 'staff_all');
		}
	}
	if (!function_exists('epc_erp_filter_commerce_tabs')) {
		function epc_erp_filter_commerce_tabs(array $tabs, $settings = null): array
		{
			if (!empty($GLOBALS['BRINE_DROP_GL'])) {
				return array_values(array_filter($tabs, static function ($t) {
					return $t !== 'gl';
				}));
			}
			return array_values($tabs);
		}
	}
}

function brine_reset(): void
{
	$GLOBALS['BRINE_PLATFORM_REQ'] = 0;
	$GLOBALS['BRINE_PLATFORM_ACTIVE'] = 0;
	$GLOBALS['BRINE_ACCESS'] = 'full';
	$GLOBALS['BRINE_SETTINGS'] = array();
	$GLOBALS['BRINE_DROP_GL'] = 0;
}

function brine_include(): void
{
	brine_stubs();
	include_once $GLOBALS['BRINE_PAGE'];
}

function brine_view($value)
{
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[(string) $k] = brine_view($v);
		}
		return $out;
	}
	return $value;
}

function brine_run_names()
{
	brine_include();
	$reg = epc_portal_erp_modules_registry();
	$presets = epc_portal_erp_modules_presets();
	$map = epc_portal_industry_erp_modules_preset_map();
	$ui = epc_portal_erp_modules_presets_ui();
	$first = $reg['erp_overview'];
	return array(
		count($reg),
		array_keys($reg),
		$first['label'],
		$first['area'],
		!empty($first['default_erp_only']) ? 1 : 0,
		!empty($first['default_full']) ? 1 : 0,
		array_keys($presets),
		$presets['hr_only']['modules'],
		$presets['custom_shipping_only']['modules'],
		$map,
		epc_portal_industry_erp_modules_preset('hr_recruitment'),
		epc_portal_industry_erp_modules_preset('Nope!'),
		array_keys($ui),
		epc_portal_erp_modules_default_ids('full'),
		epc_portal_erp_modules_default_ids('erp_only'),
	);
}

function brine_run_normalize()
{
	brine_include();
	return array(
		epc_portal_erp_modules_normalize_list('["erp_sales","erp_finance","nope"]'),
		epc_portal_erp_modules_normalize_list('erp_people, ERP_FINANCE , junk'),
		epc_portal_erp_modules_normalize_list(array(array('id' => 'erp_overview'), 'erp_sales', 'ERP-SALES')),
		epc_portal_erp_modules_normalize_list(null),
		epc_portal_erp_modules_normalize_list(''),
		epc_portal_erp_modules_normalize_list(array('erp_enterprise', 'erp_enterprise')),
		epc_portal_erp_modules_from_post(array('erp_modules_preset' => 'hr_only')),
		epc_portal_erp_modules_from_post(array('erp_modules' => array('erp_sales', 'nope'))),
		epc_portal_erp_modules_from_post(array('erp_modules_preset' => 'missing', 'erp_modules' => array('erp_finance'))),
		epc_portal_erp_modules_detect_preset(array('erp_overview', 'erp_people', 'erp_collaboration')),
		epc_portal_erp_modules_detect_preset(array('erp_sales')),
	);
}

function brine_run_onboard()
{
	brine_include();
	return array(
		epc_portal_erp_modules_resolve_for_onboard(array('erp_modules' => array('erp_sales', 'erp_finance')), 'logistics', 'erp_only'),
		epc_portal_erp_modules_resolve_for_onboard(array('erp_modules_preset' => 'customs_logistics'), '', 'full'),
		epc_portal_erp_modules_resolve_for_onboard(array(), 'hr_recruitment', 'full'),
		epc_portal_erp_modules_resolve_for_onboard(array(), 'unknown_x', 'erp_only'),
		epc_portal_erp_modules_resolve_for_onboard(array('erp_modules' => array()), 'tax_advisory', 'full'),
		epc_portal_erp_modules_area_enabled('setup', array('erp_modules' => array('erp_sales'))),
		epc_portal_erp_modules_area_enabled('sales', array('erp_modules' => array('erp_sales'))),
		epc_portal_erp_modules_area_enabled('people', array('erp_modules' => array('erp_sales'))),
	);
}

function brine_run_enabled()
{
	brine_include();
	brine_reset();
	$stored = epc_portal_erp_modules_enabled(array('erp_modules' => array('erp_sales', 'erp_people')));
	$json = epc_portal_erp_modules_enabled(array('erp_modules_json' => '["erp_finance"]'));
	$defaults = epc_portal_erp_modules_enabled(array());
	$GLOBALS['BRINE_PLATFORM_ACTIVE'] = 1;
	$full = epc_portal_erp_modules_enabled(array('erp_modules' => array('erp_sales')));
	$GLOBALS['BRINE_PLATFORM_ACTIVE'] = 0;
	$GLOBALS['BRINE_PLATFORM_REQ'] = 1;
	$req = epc_portal_erp_modules_enabled(array('erp_modules' => array('erp_sales')));
	$GLOBALS['BRINE_PLATFORM_REQ'] = 0;
	$areas = epc_portal_erp_modules_enabled_areas(array('erp_modules' => array('erp_sales')));
	sort($areas);
	$tabs = epc_portal_erp_modules_allowed_tabs(array('erp_modules' => array('erp_sales')));
	$GLOBALS['BRINE_DROP_GL'] = 1;
	$filteredTabs = epc_portal_erp_modules_allowed_tabs(array('erp_modules' => array('erp_sales')));
	$GLOBALS['BRINE_DROP_GL'] = 0;
	$intersect = epc_erp_filter_tabs_by_tenant_modules(array('orders', 'gl', 'nope'), array('erp_modules' => array('erp_sales')));
	$emptyIntersect = epc_erp_filter_tabs_by_tenant_modules(array('nope'), array('erp_modules' => array('erp_sales')));
	return array(
		$stored,
		$json,
		$defaults,
		$full,
		$req,
		$areas,
		$tabs,
		$filteredTabs,
		$intersect,
		$emptyIntersect,
		epc_portal_erp_modules_full_access_context() ? 1 : 0,
	);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_brine_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	$code = file_get_contents($root . '/content/general_pages/epc_portal_erp_modules.php');
	file_put_contents($tmp . '/page.php', $code);
	$GLOBALS['BRINE_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	brine_reset();
	$fn = 'brine_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1brine_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1brine_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
