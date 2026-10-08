<?php
// Runs PHP content/shop/order_process/my_quotes.php for every case on throwaway MariaDB databases, each case in its
// own process (the page declares a function and uses a top-level return). A temporary DOCUMENT_ROOT holds a DP_User
// stub (getUserId/getAdminId from the case), a marker for modules/login/login_form_general.php and the real
// content/shop/pricing directory (epc_currency.php, epc_customer_trade.php).
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
	try {
		mkdir($doc . '/content/users', 0777, true);
		mkdir($doc . '/content/shop', 0777, true);
		mkdir($doc . '/modules/login', 0777, true);
		symlink($root . '/content/shop/pricing', $doc . '/content/shop/pricing');
		file_put_contents($doc . '/content/users/dp_user.php', '<?php if (!class_exists("DP_User")) { class DP_User { public static function getUserId() { return $GLOBALS["__case_user"]; } public static function getAdminId() { return $GLOBALS["__case_admin"]; } } }');
		file_put_contents($doc . '/modules/login/login_form_general.php', '[[LOGIN_FORM:<?php echo $login_form_postfix; ?>]]');
		$db_link = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		foreach (array_merge($spec['schema'], $spec['base'], $case['setup'] ?? array()) as $sql) {
			$db_link->exec($sql);
		}
		$_SERVER['DOCUMENT_ROOT'] = $doc;
		$GLOBALS['__case_user'] = (int) $case['user_id'];
		$GLOBALS['__case_admin'] = (int) $case['admin_id'];
		$_GET = $case['get_id'] === null ? array() : array('id' => $case['get_id']);
		$_COOKIE = $case['cookies'] ?? array();
		$DP_Config = new stdClass();
		$DP_Config->backend_dir = 'cp';
		$DP_Config->shop_currency = '784';
		if (isset($case['currency_show_mode'])) {
			$DP_Config->currency_show_mode = $case['currency_show_mode'];
		}
		$multilang_params = array('lang_href' => array_key_exists('lang_href', $case) ? $case['lang_href'] : '/en');
		function translate_str_by_id($key) { return '{' . $key . '}'; }
		define('_ASTEXE_', 1);
		ob_start();
		include $root . '/content/shop/order_process/my_quotes.php';
		$html = ob_get_clean();
		echo json_encode(array('name' => $case['name'], 'html' => $html), JSON_UNESCAPED_UNICODE);
	} finally {
		$admin->exec('DROP DATABASE `' . $name . '`');
		@unlink($doc . '/content/shop/pricing');
		@unlink($doc . '/content/users/dp_user.php');
		@unlink($doc . '/modules/login/login_form_general.php');
		@rmdir($doc . '/content/users'); @rmdir($doc . '/content/shop'); @rmdir($doc . '/content'); @rmdir($doc . '/modules/login'); @rmdir($doc . '/modules'); @rmdir($doc);
	}
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
