<?php
// PHP 8.3 goldens for plan Q1-site (site context / supplier leftovers / CP ACL).
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1t_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/general_pages', 0777, true);
	@mkdir($doc . '/content/shop/usefull', 0777, true);
	@mkdir($doc . '/cp/content/control', 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$name = (string) $case['name'];
	$loads = array();
	if (in_array($name, array('site_key', 'site_ctx', 'site_apply'), true)) {
		file_put_contents($doc . '/content/general_pages/epc_portal.php', <<<'PHP'
<?php
function epc_portal_host() {
	$host = '';
	if (!empty($_SERVER['HTTP_HOST'])) { $host = strtolower((string) $_SERVER['HTTP_HOST']); }
	if ($host !== '' && strpos($host, ':') !== false) { $host = explode(':', $host, 2)[0]; }
	if ($host === '' && !empty($_SERVER['SERVER_NAME'])) { $host = strtolower((string) $_SERVER['SERVER_NAME']); }
	return $host;
}
function epc_portal_site_profile() {
	return isset($GLOBALS['__portal_profile']) && is_array($GLOBALS['__portal_profile']) ? $GLOBALS['__portal_profile'] : array();
}
function epc_portal_industry() {
	return isset($GLOBALS['__portal_industry']) && is_array($GLOBALS['__portal_industry']) ? $GLOBALS['__portal_industry'] : array();
}
function epc_portal_default_contact($profile = array()) {
	$host = epc_portal_host();
	$trade = isset($profile['trade_name']) ? (string) $profile['trade_name'] : '';
	if ($trade === '' && isset($profile['hub_name'])) { $trade = (string) $profile['hub_name']; }
	if ($trade === '' && $host !== '') {
		$trade = preg_replace('/^www\./', '', $host);
		$trade = ucfirst(str_replace(array('.com', '.'), array('', ' '), $trade));
	}
	return array(
		'trade_name' => $trade,
		'from_name' => $trade,
		'from_email' => isset($profile['from_email']) ? (string) $profile['from_email'] : '',
		'admin_email' => isset($profile['admin_email']) ? (string) $profile['admin_email'] : '',
		'contact_phone' => isset($profile['contact_phone']) ? (string) $profile['contact_phone'] : '',
		'whatsapp_number' => isset($profile['whatsapp_number']) ? (string) $profile['whatsapp_number'] : '',
		'head_office_title' => 'Head Office',
		'head_office_address' => isset($profile['head_office_address']) ? (string) $profile['head_office_address'] : '',
		'head_office_email' => isset($profile['head_office_email']) ? (string) $profile['head_office_email'] : '',
		'city' => isset($profile['city']) ? (string) $profile['city'] : '',
		'country' => isset($profile['country']) ? (string) $profile['country'] : 'United Arab Emirates',
	);
}
function epc_portal_guess_domain_path($host = null) {
	if (isset($GLOBALS['__portal_guess'])) { return (string) $GLOBALS['__portal_guess']; }
	if ($host === null) { $host = epc_portal_host(); }
	if ($host === '' || $host === 'localhost' || $host === '127.0.0.1') { return ''; }
	return 'http://' . $host . '/';
}
function epc_portal_is_auto_parts_site() { return !empty($GLOBALS['__portal_auto']); }
function epc_portal_home_mode() { return isset($GLOBALS['__portal_home']) ? (string) $GLOBALS['__portal_home'] : 'auto_parts'; }
PHP
		);
		file_put_contents($doc . '/content/general_pages/epc_branding.php', <<<'PHP'
<?php
function epc_brand_system_name() {
	$site = epc_portal_site_profile();
	return isset($site['system_name']) && $site['system_name'] !== '' ? $site['system_name'] : 'ECOM AE portal';
}
function epc_brand_hub_name() {
	$site = epc_portal_site_profile();
	return isset($site['hub_name']) && $site['hub_name'] !== '' ? $site['hub_name'] : 'ecomae';
}
PHP
		);
		copy($root . '/content/general_pages/epc_site_context.php', $doc . '/content/general_pages/epc_site_context.php');
		$loads[] = $doc . '/content/general_pages/epc_site_context.php';
	} elseif ($name === 'supplier') {
		copy($root . '/content/shop/usefull/epc_supplier_notifications.php', $doc . '/content/shop/usefull/epc_supplier_notifications.php');
		$loads[] = $doc . '/content/shop/usefull/epc_supplier_notifications.php';
	} elseif ($name === 'acl') {
		copy($root . '/cp/content/control/control_helper.php', $doc . '/cp/content/control/control_helper.php');
		$loads[] = $doc . '/cp/content/control/control_helper.php';
	}
	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$GLOBALS['db_link'] = $pdo;
	foreach ($loads as $php) {
		require $php;
	}
	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$cleanup();
		echo json_encode(array('name' => $case['name'], 'output' => $html, 'result' => $GLOBALS['__result'] ?? null), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	});
	ob_start();
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1t_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1t_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
