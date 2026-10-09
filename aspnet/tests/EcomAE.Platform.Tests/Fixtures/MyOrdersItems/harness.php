<?php
// Runs PHP 8.3 content/shop/order_process/my_orders_items.php for every fixture on an isolated ecomae_cpw_* database.
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=... php harness.php /workspace > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int)$argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$name = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$doc = sys_get_temp_dir() . '/' . $name;
	$admin->exec('CREATE DATABASE `' . $name . '`');
	try {
		mkdir($doc . '/content/users', 0777, true);
		mkdir($doc . '/content/shop/order_process', 0777, true);
		mkdir($doc . '/modules/login', 0777, true);
		symlink($root . '/content/shop/order_process/orders_background.php', $doc . '/content/shop/order_process/orders_background.php');
		file_put_contents($doc . '/content/users/dp_user.php', '<?php class DP_User { public static function getUserId(){return $GLOBALS["__uid"];} }');
		file_put_contents($doc . '/modules/login/login_form_general.php', '[[LOGIN_FORM:<?php echo $login_form_postfix; ?>|<?php echo isset($login_form_target) ? $login_form_target : ""; ?>]]');
		$db_link = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		foreach (array_merge($spec['schema'], $spec['base'], $case['sql'] ?? array()) as $sql) { $db_link->exec($sql); }
		$_SERVER['DOCUMENT_ROOT'] = $doc;
		$_SERVER['REQUEST_METHOD'] = 'GET';
		$_GET = $case['get'];
		$_POST = array();
		$_COOKIE = $case['cookies'];
		$GLOBALS['__uid'] = (int)$case['user_id'];
		$DP_Config = new stdClass();
		foreach ($case['config'] as $key => $value) { $DP_Config->$key = $value; }
		$DP_Content = new stdClass();
		$DP_Content->url = 'shop/orders/items';
		$multilang_params = array('lang_href' => $case['lang_href'] ?? '/en');
		function translate_str_by_id($key) { return '{' . $key . '}'; }
		define('_ASTEXE_', 1);
		ob_start();
		include $root . '/content/shop/order_process/my_orders_items.php';
		$html = ob_get_clean();
		echo json_encode(array('name'=>$case['name'],'html'=>$html), JSON_UNESCAPED_UNICODE);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $name . '`');
		foreach (array('/content/shop/order_process/orders_background.php','/content/users/dp_user.php','/modules/login/login_form_general.php') as $file) { @unlink($doc . $file); }
		foreach (array('/content/shop/order_process','/content/shop','/content/users','/content','/modules/login','/modules','') as $dir) { @rmdir($doc . $dir); }
	}
	exit;
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$out = shell_exec(escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i);
	$row = json_decode((string)$out, true);
	if (!is_array($row)) { fwrite(STDERR, "case {$case['name']} failed:\n$out\n"); exit(1); }
	$results[] = $row;
}
echo json_encode(array('php'=>PHP_VERSION,'timezone'=>date_default_timezone_get(),'results'=>$results), JSON_PRETTY_PRINT|JSON_UNESCAPED_UNICODE), "\n";
