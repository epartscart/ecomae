<?php
// PHP 8.3 goldens for plan Q1-horn (BOC audit-log page). Leftover unique
// portal, leftover unique kernel parent, leftover unique user helper, and
// Super-CP host stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function horn_password(): string
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	return ($pw === false || $pw === '') ? 'local-throwaway-pw' : $pw;
}

function horn_pdo(): PDO
{
	if (!empty($GLOBALS['HORN_PDO']) && $GLOBALS['HORN_PDO'] instanceof PDO) {
		return $GLOBALS['HORN_PDO'];
	}
	$password = horn_password();
	$name = 'ecomae_cpw_horn_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=127.0.0.1;port=3306', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$GLOBALS['HORN_DBNAME'] = $name;
	$GLOBALS['HORN_ADMIN_PDO'] = $admin;
	$GLOBALS['HORN_PDO'] = $pdo;
	return $pdo;
}

function horn_drop(): void
{
	if (!empty($GLOBALS['HORN_DBNAME']) && !empty($GLOBALS['HORN_ADMIN_PDO']) && $GLOBALS['HORN_ADMIN_PDO'] instanceof PDO) {
		$GLOBALS['HORN_ADMIN_PDO']->exec('DROP DATABASE IF EXISTS `' . $GLOBALS['HORN_DBNAME'] . '`');
	}
}

function horn_audit_helpers(): void
{
	if (!function_exists('epc_boc_h')) {
		function epc_boc_h($v): string
		{
			return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
		}
	}
	if (!function_exists('epc_boc_brand')) {
		function epc_boc_brand(): array
		{
			return array(
				'name' => 'Business Operation System',
				'short' => 'BOS',
				'control' => 'Control',
				'legacy' => 'Super CP',
				'tagline' => 'One BOS for every tenant, ERP-only client and demo — operators control the whole fleet, each tenant sees only their own area.',
			);
		}
	}
	if (!function_exists('epc_boc_audit_ensure_schema')) {
		function epc_boc_audit_ensure_schema(PDO $db): void
		{
			$db->exec(
				'CREATE TABLE IF NOT EXISTS `epc_boc_audit` (' .
				'`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,' .
				'`ts` INT UNSIGNED NOT NULL,' .
				'`user_id` INT NOT NULL DEFAULT 0,' .
				'`actor` VARCHAR(190) NOT NULL DEFAULT \'\',' .
				'`area` VARCHAR(64) NOT NULL DEFAULT \'\',' .
				'`action` VARCHAR(96) NOT NULL DEFAULT \'\',' .
				'`target` VARCHAR(190) NOT NULL DEFAULT \'\',' .
				'`meta` TEXT NULL,' .
				'`ip` VARCHAR(45) NOT NULL DEFAULT \'\',' .
				'PRIMARY KEY (`id`),' .
				'KEY `ts` (`ts`),' .
				'KEY `area` (`area`),' .
				'KEY `user_id` (`user_id`)' .
				') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
			);
		}
	}
	if (!function_exists('epc_boc_audit_recent')) {
		function epc_boc_audit_recent(PDO $db, int $limit = 100, string $area = ''): array
		{
			try {
				epc_boc_audit_ensure_schema($db);
				$limit = max(1, min(1000, $limit));
				if ($area !== '') {
					$st = $db->prepare('SELECT * FROM `epc_boc_audit` WHERE `area` = ? ORDER BY `id` DESC LIMIT ' . $limit);
					$st->execute(array($area));
				} else {
					$st = $db->query('SELECT * FROM `epc_boc_audit` ORDER BY `id` DESC LIMIT ' . $limit);
				}
				$rows = $st->fetchAll(PDO::FETCH_ASSOC);
				return is_array($rows) ? $rows : array();
			} catch (Throwable $e) {
				return array();
			}
		}
	}
}

function horn_stubs(bool $withSuperFn): void
{
	if ($withSuperFn && !function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['HORN_SUPER']);
		}
	}
	horn_audit_helpers();
}

function horn_docroot_files(string $doc, bool $withUserClass): void
{
	$pages = $doc . '/content/general_pages';
	$users = $doc . '/content/users';
	@mkdir($pages, 0777, true);
	@mkdir($users, 0777, true);
	foreach (array('epc_portal.php', 'epc_boc_kernel.php') as $name) {
		file_put_contents($pages . '/' . $name, "<?php\n");
	}
	if ($withUserClass) {
		file_put_contents($users . '/dp_user.php', <<<'PHP'
<?php
class DP_User {
	public static function isAdmin(): bool
	{
		return !empty($GLOBALS['HORN_ADMIN']);
	}
}
PHP);
	} else {
		file_put_contents($users . '/dp_user.php', "<?php\n");
	}
}

function horn_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	horn_stubs(!empty($GLOBALS['HORN_SUPER_FN']));
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['HORN_DOCROOT'];
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

function horn_render(mixed $config, mixed $super, bool $superFn, bool $admin, mixed $db, mixed $area): string
{
	$GLOBALS['HORN_SUPER'] = $super;
	$GLOBALS['HORN_SUPER_FN'] = $superFn;
	$GLOBALS['HORN_ADMIN'] = $admin;
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
	if ($area === '__unset__') {
		unset($_GET['area']);
	} else {
		$_GET['area'] = $area;
	}
	$tmp = sys_get_temp_dir() . '/ecomae_horn_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['HORN_PAGE'], $tmp);
	ob_start();
	horn_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function horn_seed(PDO $pdo): void
{
	horn_audit_helpers();
	epc_boc_audit_ensure_schema($pdo);
	$st = $pdo->prepare('INSERT INTO `epc_boc_audit` (`ts`,`user_id`,`actor`,`area`,`action`,`target`,`meta`,`ip`) VALUES (?,?,?,?,?,?,?,?)');
	$st->execute(array(1700000000, 7, 'Ada', 'governance', 'toggle', 'acme', '{"via":"test","n":1}', '1.2.3.4'));
	$st->execute(array(1700000100, 0, '', 'tenant_control', 'reveal', 'beta', null, '10.0.0.1'));
	$st->execute(array(1700000200, 9, 'Ada & Co', 'governance', '<script>', '"', 'not-json', ''));
}

function horn_run_empty(): array
{
	$none = horn_render('__unset__', 0, false, false, '__unset__', '__unset__');
	$tenant = horn_render((object) array('backend_dir' => 'acme'), 0, true, true, '__unset__', 'governance');
	return array($none, $tenant);
}

function horn_run_gates(): array
{
	$login = horn_render((object) array('backend_dir' => 'acme'), 1, true, false, '__unset__', '__unset__');
	$escape = horn_render((object) array('backend_dir' => 'acme"><x'), 1, true, false, '__unset__', '__unset__');
	$noPdo = horn_render((object) array('backend_dir' => 'cp'), 1, true, true, '__unset__', '__unset__');
	$notPdo = horn_render((object) array('backend_dir' => 'cp'), 1, true, true, 'nope', '__unset__');
	$emptyDir = horn_render((object) array('backend_dir' => ''), 1, true, false, '__unset__', '__unset__');
	return array($login, $escape, $noPdo, $notPdo, $emptyDir);
}

function horn_run_live(): array
{
	$pdo = horn_pdo();
	$cfg = (object) array('backend_dir' => 'cp');
	$blank = horn_render($cfg, 1, true, true, $pdo, '__unset__');
	horn_seed($pdo);
	$all = horn_render($cfg, 1, true, true, $pdo, '__unset__');
	$gov = horn_render($cfg, 1, true, true, $pdo, 'governance');
	$dirty = horn_render($cfg, 1, true, true, $pdo, 'Governance!!');
	$tenantArea = horn_render($cfg, 1, true, true, $pdo, 'tenant_control');
	return array($blank, $all, $gov, $dirty, $tenantArea);
}

function horn_run_tenant(): array
{
	$pdo = horn_pdo();
	horn_seed($pdo);
	$acme = horn_render((object) array('backend_dir' => 'acme'), 1, true, true, $pdo, 'governance');
	$beta = horn_render((object) array('backend_dir' => 'beta'), 1, true, true, $pdo, 'governance');
	$denied = horn_render((object) array('backend_dir' => 'acme'), 0, true, true, $pdo, 'governance');
	return array($acme, $beta, $denied);
}

register_shutdown_function('horn_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_horn_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	$GLOBALS['HORN_PAGE'] = $root . '/cp/content/control/portal/epc_boc_audit_log.php';
	$GLOBALS['HORN_DOCROOT'] = $tmp;
	horn_docroot_files($tmp, $case['name'] !== 'empty');
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'horn_run_' . $case['name'];
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1horn_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1horn_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
