<?php
// Runs PHP content/shop/order_process/my_order_not_authorized.php (GET) for every case on throwaway MariaDB databases,
// each case in its own process (the page ends with exit). A temporary DOCUMENT_ROOT holds a DP_User stub (guest, with
// the case csrf_guard_key as the session), the real orders_background.php and actions_alert.php, and a marker for
// content/shop/payments/epc_payment_method_picker.php.
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=... php harness.php /workspace > golden.json
ini_set('display_errors', 'stderr');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$name = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$doc = sys_get_temp_dir() . '/' . $name;
	$admin->exec('CREATE DATABASE `' . $name . '`');
	$cleanup = function () use ($admin, $name, $doc) {
		$admin->exec('DROP DATABASE IF EXISTS `' . $name . '`');
		@unlink($doc . '/content/users/dp_user.php');
		@unlink($doc . '/content/shop/order_process/orders_background.php');
		@unlink($doc . '/content/general/actions_alert.php');
		@unlink($doc . '/content/shop/payments/epc_payment_method_picker.php');
		foreach (array('/content/users', '/content/shop/order_process', '/content/shop/payments', '/content/shop', '/content/general', '/content', '') as $dir) {
			@rmdir($doc . $dir);
		}
	};
	mkdir($doc . '/content/users', 0777, true);
	mkdir($doc . '/content/shop/order_process', 0777, true);
	mkdir($doc . '/content/shop/payments', 0777, true);
	mkdir($doc . '/content/general', 0777, true);
	symlink($root . '/content/shop/order_process/orders_background.php', $doc . '/content/shop/order_process/orders_background.php');
	symlink($root . '/content/general/actions_alert.php', $doc . '/content/general/actions_alert.php');
	file_put_contents($doc . '/content/users/dp_user.php', '<?php if (!class_exists("DP_User")) { class DP_User { public static function getUserId() { return 0; } public static function getUserSession() { return $GLOBALS["__case_csrf"] === null ? false : array("csrf_guard_key" => $GLOBALS["__case_csrf"]); } } }');
	file_put_contents($doc . '/content/shop/payments/epc_payment_method_picker.php', '[[PAY_PICKER]]');
	$db_link = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach (array_merge($spec['schema'], $spec['base']) as $sql) {
		$db_link->exec($sql);
	}
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$GLOBALS['__case_csrf'] = $case['csrf'] ?? null;
	$_GET = $case['get'];
	$_POST = array();
	$DP_Config = new stdClass();
	foreach ($case['config'] as $key => $value) {
		$DP_Config->$key = $value;
	}
	$multilang_params = array('lang_href' => array_key_exists('lang_href', $case) ? $case['lang_href'] : '/en');
	function translate_str_by_id($key) { return '{' . $key . '}'; }
	define('_ASTEXE_', 1);
	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$cleanup();
		echo json_encode(array('name' => $case['name'], 'html' => $html), JSON_UNESCAPED_UNICODE);
	});
	ob_start();
	include $root . '/content/shop/order_process/my_order_not_authorized.php';
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$out = shell_exec(escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i);
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'timezone' => date_default_timezone_get(), 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE), "\n";
