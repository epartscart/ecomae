<?php
// PHP 8.3 goldens for plan Q1-hike (Super-CP customer board). Leftover unique
// portal, leftover unique operator-guide page, leftover unique user helper,
// and leftover portal PDO stay injected. Real Super-CP platform parent is
// not included — helpers already closed in Gulf are stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function hike_helpers(): void
{
	if (!function_exists('epc_scp_h')) {
		function epc_scp_h($v): string
		{
			return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
		}
	}
	if (!function_exists('epc_scp_backend')) {
		function epc_scp_backend(): string
		{
			return trim((string) ($GLOBALS['HIKE_BACKEND'] ?? 'cp'), '/');
		}
	}
	if (!function_exists('epc_scp_operator_guide_url')) {
		function epc_scp_operator_guide_url(): string
		{
			return '/' . epc_scp_backend() . '/control/portal/epc_super_cp_operator_guide';
		}
	}
	if (!function_exists('epc_scp_guard_super_admin')) {
		function epc_scp_guard_super_admin(): bool
		{
			if (empty($GLOBALS['HIKE_SUPER'])) {
				echo '<div class="alert alert-warning">This module is available on ECOM AE Super CP only.</div>';
				return false;
			}
			if (empty($GLOBALS['HIKE_ADMIN'])) {
				echo '<div class="alert alert-warning">Please <a href="/' . epc_scp_h($GLOBALS['HIKE_BACKEND'] ?? 'cp') . '/">log in to Super CP</a>.</div>';
				return false;
			}
			return true;
		}
	}
	if (!function_exists('epc_scp_customer_board_search')) {
		function epc_scp_customer_board_search($pdo, $search, $tenantFilter, $page, $perPage): array
		{
			$board = isset($GLOBALS['HIKE_BOARD']) && is_array($GLOBALS['HIKE_BOARD'])
				? $GLOBALS['HIKE_BOARD']
				: array('rows' => array(), 'total' => 0, 'page' => 1, 'per_page' => 50, 'stats' => array());
			return $board;
		}
	}
	if (!function_exists('epc_scp_tenant_options')) {
		function epc_scp_tenant_options($pdo): array
		{
			return isset($GLOBALS['HIKE_TENANTS']) && is_array($GLOBALS['HIKE_TENANTS'])
				? $GLOBALS['HIKE_TENANTS']
				: array();
		}
	}
	if (!function_exists('epc_scp_render_hero')) {
		function epc_scp_render_hero($badge, $title, $sub, $actions = array()): void
		{
			$first = (isset($actions[0]) && is_array($actions[0])) ? (string) ($actions[0]['url'] ?? '') : '';
			echo 'HERO:' . (string) $badge . '|' . (string) $title . '|' . count($actions) . '|' . $first;
		}
	}
	if (!function_exists('epc_scp_render_workspace_intro')) {
		function epc_scp_render_workspace_intro($module): void
		{
			echo 'INTRO:' . (string) $module;
		}
	}
	if (!function_exists('epc_scp_render_empty_state')) {
		function epc_scp_render_empty_state($title, $body, $actions = array()): void
		{
			echo 'EMPTY:' . (string) $title . '|' . (string) $body . '|' . count($actions);
		}
	}
}

function hike_docroot_files(string $doc): void
{
	$pages = $doc . '/content/general_pages';
	@mkdir($pages, 0777, true);
	file_put_contents($pages . '/epc_super_cp_platform.php', "<?php\n");
}

function hike_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	hike_helpers();
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['HIKE_DOCROOT'];
	$_GET = isset($GLOBALS['HIKE_GET']) && is_array($GLOBALS['HIKE_GET']) ? $GLOBALS['HIKE_GET'] : array();
	if (!empty($GLOBALS['HIKE_HAS_PDO'])) {
		if (!class_exists('HikeDummyPdo', false)) {
			eval('class HikeDummyPdo extends PDO { public function __construct() {} }');
		}
		$GLOBALS['db_link'] = new HikeDummyPdo();
		$db_link = $GLOBALS['db_link'];
	} else {
		unset($GLOBALS['db_link']);
		unset($db_link);
	}
	require $path;
}

function hike_render(
	string $backend,
	bool $super,
	bool $admin,
	bool $hasPdo,
	array $get,
	array $board,
	array $tenants
): string {
	$GLOBALS['HIKE_BACKEND'] = $backend;
	$GLOBALS['HIKE_SUPER'] = $super;
	$GLOBALS['HIKE_ADMIN'] = $admin;
	$GLOBALS['HIKE_HAS_PDO'] = $hasPdo;
	$GLOBALS['HIKE_GET'] = $get;
	$GLOBALS['HIKE_BOARD'] = $board;
	$GLOBALS['HIKE_TENANTS'] = $tenants;
	$tmp = sys_get_temp_dir() . '/ecomae_hike_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['HIKE_PAGE'], $tmp);
	ob_start();
	hike_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function hike_empty_board(): array
{
	return array(
		'rows' => array(),
		'total' => 0,
		'page' => 1,
		'per_page' => 50,
		'stats' => array(),
	);
}

function hike_run_empty(): array
{
	$none = hike_render('cp', false, false, false, array(), hike_empty_board(), array());
	$login = hike_render('acme', true, false, false, array(), hike_empty_board(), array());
	return array($none, $login);
}

function hike_run_gates(): array
{
	$noSuper = hike_render('cp', false, true, true, array(), hike_empty_board(), array());
	$noDb = hike_render('cp', true, true, false, array(), hike_empty_board(), array());
	$emptyBoard = hike_render('cp', true, true, true, array(), hike_empty_board(), array());
	$filteredEmpty = hike_render('cp', true, true, true, array('q' => 'nobody', 'tenant' => 'platform'), hike_empty_board(), array(
		array('site_key' => 'acme', 'label' => 'Acme Parts'),
	));
	$escape = hike_render('acme"><x', true, true, true, array('q' => 'a&b', 'tenant' => 'acme"><x'), hike_empty_board(), array(
		array('site_key' => 'acme"><x', 'label' => 'Acme & Co'),
	));
	return array($noSuper, $noDb, $emptyBoard, $filteredEmpty, $escape);
}

function hike_run_live(): array
{
	$row = array(
		'name' => 'Ada Lovelace',
		'user_id' => 7,
		'email' => 'ada@example.com',
		'phone' => '+971 50 000 0007',
		'source_label' => 'Platform',
		'source' => 'platform',
		'hostname' => 'www.ecomae.com',
		'time_reg' => 1700000000,
		'links' => array(
			'crm' => '/cp/crm/ada',
			'erp' => '/cp/erp/ada',
			'cp' => '/cp/users/7',
		),
	);
	$board = array(
		'rows' => array($row),
		'total' => 1,
		'page' => 1,
		'per_page' => 50,
		'stats' => array('platform' => 1, 'tenants_scanned' => 2, 'tenants_with_hits' => 0),
	);
	$defaults = hike_render('cp', true, true, true, array(), $board, array(
		array('site_key' => 'acme', 'label' => 'Acme Parts'),
		array('site_key' => 'beta', 'label' => 'Beta Town'),
	));
	$escapedRow = $row;
	$escapedRow['name'] = 'Ada & Co';
	$escapedRow['email'] = 'ada+<x>@example.com';
	$escapedRow['phone'] = '';
	$escapedRow['hostname'] = '';
	$escapedRow['links'] = array('crm' => '/cp/crm?q=a&b');
	$filled = hike_render('cp', true, true, true, array('q' => 'Ada & Co'), array(
		'rows' => array($escapedRow),
		'total' => 1,
		'page' => 1,
		'per_page' => 50,
		'stats' => array('platform' => 1, 'tenants_scanned' => 1, 'tenants_with_hits' => 1),
	), array(
		array('site_key' => 'acme', 'label' => 'Acme & Co'),
	));
	$pages = array();
	for ($i = 1; $i <= 3; $i++) {
		$pages[] = array(
			'name' => 'User ' . $i,
			'user_id' => $i,
			'email' => 'u' . $i . '@example.com',
			'source' => 'acme',
			'source_label' => 'Acme',
			'time_reg' => 0,
			'links' => array(),
		);
	}
	$paged = hike_render('cp', true, true, true, array('q' => 'user', 'tenant' => 'acme', 'page' => '2'), array(
		'rows' => $pages,
		'total' => 60,
		'page' => 2,
		'per_page' => 50,
		'stats' => array('platform' => 0, 'tenants_scanned' => 1, 'tenants_with_hits' => 1),
	), array(
		array('site_key' => 'acme', 'label' => 'Acme Parts'),
	));
	$blankTime = hike_render('cp', true, true, true, array(), array(
		'rows' => array(array('name' => 'Bea', 'user_id' => 9, 'source' => 'beta')),
		'total' => 1,
		'page' => 1,
		'per_page' => 50,
		'stats' => array(),
	), array());
	return array($defaults, $filled, $paged, $blankTime);
}

function hike_run_tenant(): array
{
	$acmeBoard = array(
		'rows' => array(array(
			'name' => 'Ada Acme',
			'user_id' => 11,
			'email' => 'ada@acme.test',
			'source_label' => 'Acme',
			'source' => 'acme',
			'hostname' => 'acme.example.com',
			'time_reg' => 1700000000,
			'links' => array('crm' => '/acme/crm/11', 'erp' => '/acme/erp/11'),
		)),
		'total' => 1,
		'page' => 1,
		'per_page' => 50,
		'stats' => array('platform' => 0, 'tenants_scanned' => 1, 'tenants_with_hits' => 1),
	);
	$betaBoard = array(
		'rows' => array(array(
			'name' => 'Bea Beta',
			'user_id' => 22,
			'email' => 'bea@beta.test',
			'source_label' => 'Beta',
			'source' => 'beta',
			'hostname' => 'beta.example.com',
			'time_reg' => 1700000000,
			'links' => array('crm' => '/beta/crm/22'),
		)),
		'total' => 1,
		'page' => 1,
		'per_page' => 50,
		'stats' => array('platform' => 0, 'tenants_scanned' => 1, 'tenants_with_hits' => 1),
	);
	$acme = hike_render('acme', true, true, true, array('tenant' => 'acme'), $acmeBoard, array(
		array('site_key' => 'acme', 'label' => 'Acme Parts'),
	));
	$beta = hike_render('beta', true, true, true, array('tenant' => 'beta'), $betaBoard, array(
		array('site_key' => 'beta', 'label' => 'Beta Town'),
	));
	$denied = hike_render('acme', true, false, true, array(), hike_empty_board(), array());
	return array($acme, $beta, $denied);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_hike_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	$GLOBALS['HIKE_PAGE'] = $root . '/cp/content/control/portal/epc_super_cp_customer_board.php';
	$GLOBALS['HIKE_DOCROOT'] = $tmp;
	hike_docroot_files($tmp);
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'hike_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		exit(1);
	}
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1hike_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1hike_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
