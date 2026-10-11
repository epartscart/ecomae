<?php
// PHP 8.3 goldens for plan Q1-hope (BOC vendor + warehouse wrappers). Leftover unique
// portal, leftover unique console parent, leftover unique user helper, and Super-CP host stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function hope_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	file_put_contents($dest, $code);
}

function hope_password(): string
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	return ($pw === false || $pw === '') ? 'local-throwaway-pw' : $pw;
}

function hope_pdo(): PDO
{
	if (!empty($GLOBALS['HOPE_PDO']) && $GLOBALS['HOPE_PDO'] instanceof PDO) {
		return $GLOBALS['HOPE_PDO'];
	}
	$password = hope_password();
	$name = 'ecomae_cpw_hope_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=127.0.0.1;port=3306', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$GLOBALS['HOPE_DBNAME'] = $name;
	$GLOBALS['HOPE_ADMIN_PDO'] = $admin;
	$GLOBALS['HOPE_PDO'] = $pdo;
	return $pdo;
}

function hope_drop(): void
{
	if (!empty($GLOBALS['HOPE_DBNAME']) && !empty($GLOBALS['HOPE_ADMIN_PDO']) && $GLOBALS['HOPE_ADMIN_PDO'] instanceof PDO) {
		$GLOBALS['HOPE_ADMIN_PDO']->exec('DROP DATABASE IF EXISTS `' . $GLOBALS['HOPE_DBNAME'] . '`');
	}
}

function hope_stubs(bool $withSuperFn, bool $withNavForUser): void
{
	if ($withSuperFn && !function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['HOPE_SUPER']);
		}
	}
	if (!function_exists('epc_boc_h')) {
		function epc_boc_h($v): string
		{
			return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
		}
	}
	if (!function_exists('epc_boc_nav')) {
		function epc_boc_nav(): array
		{
			$GLOBALS['HOPE_NAV_USED'] = 'fleet';
			return array('fleet' => 1);
		}
	}
	if ($withNavForUser && !function_exists('epc_boc_nav_for_user')) {
		function epc_boc_nav_for_user($db, $userId): array
		{
			$GLOBALS['HOPE_NAV_USED'] = 'user:' . (int) $userId;
			return array('user' => (int) $userId);
		}
	}
	if (!function_exists('epc_boc_console_open')) {
		function epc_boc_console_open($ctx = array()): void
		{
			$ctx = is_array($ctx) ? $ctx : array();
			echo 'CONSOLE_OPEN:'
				. (string) ($ctx['active'] ?? '') . '|'
				. (string) ($ctx['title'] ?? '') . '|'
				. (string) ($ctx['base'] ?? '') . '|'
				. (string) ($ctx['operator'] ?? '') . '|'
				. (string) ($ctx['scope'] ?? '') . '|'
				. (string) ($GLOBALS['HOPE_NAV_USED'] ?? '');
		}
	}
	if (!function_exists('epc_boc_render_vendor_control')) {
		function epc_boc_render_vendor_control($db, $base, $rollup = null): void
		{
			echo 'VENDOR:' . (string) $base;
		}
	}
	if (!function_exists('epc_boc_render_warehouse_control')) {
		function epc_boc_render_warehouse_control($db, $base, $rollup = null): void
		{
			echo 'WAREHOUSE:' . (string) $base;
		}
	}
	if (!function_exists('epc_boc_console_close')) {
		function epc_boc_console_close(): void
		{
			echo 'CONSOLE_CLOSE';
		}
	}
}

function hope_docroot_files(string $doc, bool $withUserClass): void
{
	$pages = $doc . '/content/general_pages';
	$users = $doc . '/content/users';
	@mkdir($pages, 0777, true);
	@mkdir($users, 0777, true);
	foreach (array('epc_portal.php', 'epc_boc_kernel.php', 'epc_boc_console.php', 'epc_boc_advanced.php', 'epc_portal_tenant_control.php') as $name) {
		file_put_contents($pages . '/' . $name, "<?php\n");
	}
	if ($withUserClass) {
		file_put_contents($users . '/dp_user.php', <<<'PHP'
<?php
class DP_User {
	public static function isAdmin(): bool
	{
		return !empty($GLOBALS['HOPE_ADMIN']);
	}
	public static function getName(): string
	{
		return (string) ($GLOBALS['HOPE_NAME'] ?? '');
	}
	public static function getUserId(): int
	{
		return (int) ($GLOBALS['HOPE_UID'] ?? 0);
	}
}
PHP);
	} else {
		file_put_contents($users . '/dp_user.php', "<?php\n");
	}
}

function hope_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	hope_stubs(!empty($GLOBALS['HOPE_SUPER_FN']), !empty($GLOBALS['HOPE_NAV_FOR_USER']));
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['HOPE_DOCROOT'];
	if (array_key_exists('DP_Config', $GLOBALS)) {
		$DP_Config = $GLOBALS['DP_Config'];
	}
	if (array_key_exists('db_link', $GLOBALS)) {
		$db_link = $GLOBALS['db_link'];
	} else {
		unset($db_link);
	}
	require $path;
}

function hope_render(string $page, mixed $config, mixed $super, bool $superFn, bool $admin, mixed $db, string $name, int $uid, bool $navForUser): string
{
	$GLOBALS['HOPE_SUPER'] = $super;
	$GLOBALS['HOPE_SUPER_FN'] = $superFn;
	$GLOBALS['HOPE_ADMIN'] = $admin;
	$GLOBALS['HOPE_NAME'] = $name;
	$GLOBALS['HOPE_UID'] = $uid;
	$GLOBALS['HOPE_NAV_FOR_USER'] = $navForUser;
	$GLOBALS['HOPE_NAV_USED'] = '';
	if ($config === '__unset__') {
		unset($GLOBALS['DP_Config']);
	} else {
		$GLOBALS['DP_Config'] = $config;
	}
	if ($db === '__unset__') {
		unset($GLOBALS['db_link']);
	} else {
		$GLOBALS['db_link'] = $db;
	}
	$tmp = sys_get_temp_dir() . '/ecomae_hope_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['HOPE_PAGES'][$page], $tmp);
	ob_start();
	hope_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function hope_run_empty(): array
{
	$vNone = hope_render('vendor', '__unset__', 0, false, false, '__unset__', '', 0, false);
	$vTenant = hope_render('vendor', (object) array('backend_dir' => 'acme'), 0, true, true, '__unset__', 'Ada', 7, false);
	$wNone = hope_render('warehouse', '__unset__', 0, false, false, '__unset__', '', 0, false);
	$wTenant = hope_render('warehouse', (object) array('backend_dir' => 'acme'), 0, true, true, '__unset__', 'Ada', 7, false);
	return array($vNone, $vTenant, $wNone, $wTenant);
}

function hope_run_gates(): array
{
	$vLogin = hope_render('vendor', (object) array('backend_dir' => 'acme'), 1, true, false, '__unset__', 'Ada', 7, false);
	$vEscape = hope_render('vendor', (object) array('backend_dir' => 'acme"><x'), 1, true, false, '__unset__', 'Ada', 7, false);
	$vNoPdo = hope_render('vendor', (object) array('backend_dir' => 'cp'), 1, true, true, '__unset__', 'Ada', 7, false);
	$vNotPdo = hope_render('vendor', (object) array('backend_dir' => 'cp'), 1, true, true, 'nope', 'Ada', 7, false);
	$wLogin = hope_render('warehouse', (object) array('backend_dir' => 'acme'), 1, true, false, '__unset__', 'Ada', 7, false);
	return array($vLogin, $vEscape, $vNoPdo, $vNotPdo, $wLogin);
}

function hope_run_live(): array
{
	$pdo = hope_pdo();
	$cfg = (object) array('backend_dir' => 'cp');
	$vFleet = hope_render('vendor', $cfg, 1, true, true, $pdo, '', 0, false);
	$vNamed = hope_render('vendor', $cfg, 1, true, true, $pdo, 'Ada', 7, true);
	$vEmpty = hope_render('vendor', (object) array('backend_dir' => ''), 1, true, true, $pdo, 'Ada', 7, true);
	$wNamed = hope_render('warehouse', $cfg, 1, true, true, $pdo, 'Ada', 7, true);
	return array($vFleet, $vNamed, $vEmpty, $wNamed);
}

function hope_run_tenant(): array
{
	$pdo = hope_pdo();
	$vAcme = hope_render('vendor', (object) array('backend_dir' => 'acme'), 1, true, true, $pdo, 'Ada', 7, true);
	$vBeta = hope_render('vendor', (object) array('backend_dir' => 'beta'), 1, true, true, $pdo, 'Ada', 7, true);
	$vDenied = hope_render('vendor', (object) array('backend_dir' => 'acme'), 0, true, true, $pdo, 'Ada', 7, true);
	$wAcme = hope_render('warehouse', (object) array('backend_dir' => 'acme'), 1, true, true, $pdo, 'Ada', 7, true);
	$wDenied = hope_render('warehouse', (object) array('backend_dir' => 'acme'), 0, true, true, $pdo, 'Ada', 7, true);
	return array($vAcme, $vBeta, $vDenied, $wAcme, $wDenied);
}

register_shutdown_function('hope_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_hope_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	hope_patch($root . '/cp/content/control/portal/epc_boc_vendor_control.php', $tmp . '/vendor.php');
	hope_patch($root . '/cp/content/control/portal/epc_boc_warehouse_control.php', $tmp . '/warehouse.php');
	$GLOBALS['HOPE_PAGES'] = array('vendor' => $tmp . '/vendor.php', 'warehouse' => $tmp . '/warehouse.php');
	$GLOBALS['HOPE_DOCROOT'] = $tmp;
	hope_docroot_files($tmp, $case['name'] !== 'empty');
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'hope_run_' . $case['name'];
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1hope_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1hope_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
