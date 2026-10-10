<?php
// PHP 8.3 goldens for plan Q1-talk (order communication test + storefront anti-crawl).
// Trade / notify / prices / session parents are stubbed, not copied.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1t_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/shop/usefull', 0777, true);
	@mkdir($doc . '/content/shop/docpart', 0777, true);
	@mkdir($doc . '/content/shop/pricing', 0777, true);
	@mkdir($doc . '/content/users', 0777, true);
	@mkdir($doc . '/content/files', 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	file_put_contents($doc . '/content/shop/pricing/epc_customer_trade.php', <<<'PHP'
<?php
function epc_trade_profile_set($db, $user_id, $key, $val) {
	$GLOBALS['__trade'][] = array($user_id, $key, $val);
}
function epc_trade_approval_status($db, $user_id) {
	return 'approved';
}
PHP
	);
	file_put_contents($doc . '/content/shop/docpart/epc_storefront_prices_helpers.php', <<<'PHP'
<?php
function epc_storefront_prices_visible_for_user($uid) {
	return !empty($GLOBALS['__prices_visible']);
}
function epc_storefront_sensitive_mask() {
	return isset($GLOBALS['__mask']) ? (string) $GLOBALS['__mask'] : '**';
}
PHP
	);
	file_put_contents($doc . '/content/users/' . 'dp' . '_user.php', <<<'PHP'
<?php
class DP_User {
	public static $id = 0;
	public static $profile = null;
	public static function getUserId() {
		return (int) self::$id;
	}
	public static function getUserProfile() {
		return self::$profile;
	}
}
PHP
	);
	copy($root . '/content/shop/usefull/epc_order_communication_test.php', $doc . '/content/shop/usefull/epc_order_communication_test.php');
	copy($root . '/content/shop/docpart/epc_storefront_anti_crawl.php', $doc . '/content/shop/docpart/epc_storefront_anti_crawl.php');
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
	$GLOBALS['__trade'] = array();
	$GLOBALS['__prices_visible'] = false;
	$GLOBALS['__mask'] = '**';
	$name = (string) $case['name'];
	if (str_starts_with($name, 'comm_')) {
		require $doc . '/content/shop/usefull/epc_order_communication_test.php';
	} else {
		require $doc . '/content/users/' . 'dp' . '_user.php';
		require $doc . '/content/shop/docpart/epc_storefront_anti_crawl.php';
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
