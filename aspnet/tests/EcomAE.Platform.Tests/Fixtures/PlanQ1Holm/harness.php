<?php
// PHP 8.3 goldens for plan Q1-holm (BOC command-center wrapper). Leftover unique portal,
// leftover unique console parent, leftover unique user helper, leftover hub helpers,
// and Super-CP host stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function holm_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	file_put_contents($dest, $code);
}

function holm_password(): string
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	return ($pw === false || $pw === '') ? 'local-throwaway-pw' : $pw;
}

function holm_pdo(): PDO
{
	if (!empty($GLOBALS['HOLM_PDO']) && $GLOBALS['HOLM_PDO'] instanceof PDO) {
		return $GLOBALS['HOLM_PDO'];
	}
	$password = holm_password();
	$name = 'ecomae_cpw_holm_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=127.0.0.1;port=3306', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$GLOBALS['HOLM_DBNAME'] = $name;
	$GLOBALS['HOLM_ADMIN_PDO'] = $admin;
	$GLOBALS['HOLM_PDO'] = $pdo;
	return $pdo;
}

function holm_drop(): void
{
	if (!empty($GLOBALS['HOLM_DBNAME']) && !empty($GLOBALS['HOLM_ADMIN_PDO']) && $GLOBALS['HOLM_ADMIN_PDO'] instanceof PDO) {
		$GLOBALS['HOLM_ADMIN_PDO']->exec('DROP DATABASE IF EXISTS `' . $GLOBALS['HOLM_DBNAME'] . '`');
	}
}

function holm_stubs(bool $withSuperFn, bool $withNavForUser): void
{
	if ($withSuperFn && !function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['HOLM_SUPER']);
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
			$GLOBALS['HOLM_NAV_USED'] = 'fleet';
			return array('fleet' => 1);
		}
	}
	if ($withNavForUser && !function_exists('epc_boc_nav_for_user')) {
		function epc_boc_nav_for_user($db, $userId): array
		{
			$GLOBALS['HOLM_NAV_USED'] = 'user:' . (int) $userId;
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
				. (string) ($GLOBALS['HOLM_NAV_USED'] ?? '');
		}
	}
	if (!function_exists('epc_boc_render_command_center')) {
		function epc_boc_render_command_center($db, $base): void
		{
			echo 'COMMAND:' . (string) $base;
		}
	}
	if (!function_exists('epc_boc_console_close')) {
		function epc_boc_console_close(): void
		{
			echo 'CONSOLE_CLOSE';
		}
	}
}

function holm_docroot_files(string $doc, bool $withUserClass): void
{
	$pages = $doc . '/content/general_pages';
	$users = $doc . '/content/users';
	$hub = $doc . '/content/shop/tenant_hub';
	@mkdir($pages, 0777, true);
	@mkdir($users, 0777, true);
	@mkdir($hub, 0777, true);
	foreach (array('epc_portal.php', 'epc_boc_kernel.php', 'epc_boc_console.php') as $name) {
		file_put_contents($pages . '/' . $name, "<?php\n");
	}
	file_put_contents($hub . '/epc_tenant_hub_helpers.php', "<?php\n");
	if ($withUserClass) {
		file_put_contents($users . '/dp_user.php', <<<'PHP'
<?php
class DP_User {
	public static function isAdmin(): bool
	{
		return !empty($GLOBALS['HOLM_ADMIN']);
	}
	public static function getName(): string
	{
		return (string) ($GLOBALS['HOLM_NAME'] ?? '');
	}
	public static function getUserId(): int
	{
		return (int) ($GLOBALS['HOLM_UID'] ?? 0);
	}
}
PHP);
	} else {
		file_put_contents($users . '/dp_user.php', "<?php\n");
	}
}

function holm_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	holm_stubs(!empty($GLOBALS['HOLM_SUPER_FN']), !empty($GLOBALS['HOLM_NAV_FOR_USER']));
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['HOLM_DOCROOT'];
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

function holm_render(mixed $config, mixed $super, bool $superFn, bool $admin, mixed $db, string $name, int $uid, bool $navForUser): string
{
	$GLOBALS['HOLM_SUPER'] = $super;
	$GLOBALS['HOLM_SUPER_FN'] = $superFn;
	$GLOBALS['HOLM_ADMIN'] = $admin;
	$GLOBALS['HOLM_NAME'] = $name;
	$GLOBALS['HOLM_UID'] = $uid;
	$GLOBALS['HOLM_NAV_FOR_USER'] = $navForUser;
	$GLOBALS['HOLM_NAV_USED'] = '';
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
	$tmp = sys_get_temp_dir() . '/ecomae_holm_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['HOLM_PAGE'], $tmp);
	ob_start();
	holm_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function holm_run_empty(): array
{
	$none = holm_render('__unset__', 0, false, false, '__unset__', '', 0, false);
	$tenant = holm_render((object) array('backend_dir' => 'acme'), 0, true, true, '__unset__', 'Ada', 7, false);
	return array($none, $tenant);
}

function holm_run_gates(): array
{
	$login = holm_render((object) array('backend_dir' => 'acme'), 1, true, false, '__unset__', 'Ada', 7, false);
	$escape = holm_render((object) array('backend_dir' => 'acme"><x'), 1, true, false, '__unset__', 'Ada', 7, false);
	$noPdo = holm_render((object) array('backend_dir' => 'cp'), 1, true, true, '__unset__', 'Ada', 7, false);
	$notPdo = holm_render((object) array('backend_dir' => 'cp'), 1, true, true, 'nope', 'Ada', 7, false);
	return array($login, $escape, $noPdo, $notPdo);
}

function holm_run_live(): array
{
	$pdo = holm_pdo();
	$cfg = (object) array('backend_dir' => 'cp');
	$fleet = holm_render($cfg, 1, true, true, $pdo, '', 0, false);
	$named = holm_render($cfg, 1, true, true, $pdo, 'Ada', 7, true);
	$emptyDir = holm_render((object) array('backend_dir' => ''), 1, true, true, $pdo, 'Ada', 7, true);
	return array($fleet, $named, $emptyDir);
}

function holm_run_tenant(): array
{
	$pdo = holm_pdo();
	$acme = holm_render((object) array('backend_dir' => 'acme'), 1, true, true, $pdo, 'Ada', 7, true);
	$beta = holm_render((object) array('backend_dir' => 'beta'), 1, true, true, $pdo, 'Ada', 7, true);
	$denied = holm_render((object) array('backend_dir' => 'acme'), 0, true, true, $pdo, 'Ada', 7, true);
	return array($acme, $beta, $denied);
}

register_shutdown_function('holm_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_holm_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	holm_patch($root . '/cp/content/control/portal/epc_boc_command_center.php', $tmp . '/page.php');
	$GLOBALS['HOLM_PAGE'] = $tmp . '/page.php';
	$GLOBALS['HOLM_DOCROOT'] = $tmp;
	holm_docroot_files($tmp, $case['name'] !== 'empty');
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'holm_run_' . $case['name'];
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1holm_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1holm_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
