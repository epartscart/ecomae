<?php
// Runs the real PHP 8.3 my_order.php on one isolated ecomae_cpw_* MariaDB database per case.
// Page dependencies that are independently migrated (picker and VAT engine) are deterministic fixture stubs.
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
		foreach (array(
			'/content/users', '/content/shop/order_process', '/content/shop/pricing', '/content/shop/payments',
			'/content/shop/finance', '/content/general'
		) as $dir) {
			mkdir($doc . $dir, 0777, true);
		}
		symlink($root . '/content/shop/order_process/orders_background.php', $doc . '/content/shop/order_process/orders_background.php');
		symlink($root . '/content/general/actions_alert.php', $doc . '/content/general/actions_alert.php');
		symlink($root . '/content/shop/pricing/epc_currency.php', $doc . '/content/shop/pricing/epc_currency.php');
		file_put_contents($doc . '/content/users/dp_user.php', '<?php class DP_User { public static function getUserId(){return $GLOBALS["__uid"];} public static function getUserSession(){return array("csrf_guard_key"=>$GLOBALS["__csrf"]);} }');
		file_put_contents($doc . '/content/shop/pricing/epc_customer_trade.php', '<?php function epc_trade_user_currency_iso($db,$uid){return "";}');
		file_put_contents($doc . '/content/shop/pricing/epc_pricing.php', '<?php');
		file_put_contents($doc . '/content/shop/payments/epc_payment_method_picker.php', '[[PAY_PICKER]]');
		file_put_contents($doc . '/content/shop/finance/epc_uae_customer_vat.php', <<<'PHP'
<?php
function epc_uae_customer_vat_order_line($db,$uid,$price,$qty,$flags=array()){ $gross=round($price*$qty,2); return array('line_net'=>$gross,'vat_amount'=>0.0,'gross'=>$gross,'tax_rate'=>0.0); }
function epc_uae_customer_vat_resolve($db,$uid){ return array('vat_type_label'=>'UAE retail (B2C) — prices incl. VAT','display_mode'=>'inclusive'); }
PHP);
		$db_link = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		foreach (array_merge($spec['schema'], $spec['base']) as $sql) {
			$db_link->exec($sql);
		}
		foreach ($case['sql'] ?? array() as $sql) {
			$db_link->exec($sql);
		}
		$_SERVER['DOCUMENT_ROOT'] = $doc;
		$_SERVER['REQUEST_METHOD'] = 'GET';
		$_GET = $case['get'];
		$_POST = array();
		$_COOKIE = array();
		$GLOBALS['__uid'] = (int)$case['user_id'];
		$GLOBALS['__csrf'] = $case['csrf'] ?? '';
		$DP_Config = new stdClass();
		$DP_Config->domain_path = '/';
		$DP_Config->client_overdraft = '0';
		$DP_Config->client_overdraft_value = '0';
		foreach ($case['config'] as $key => $value) { $DP_Config->$key = $value; }
		$multilang_params = array('lang_href' => $case['lang_href'] ?? '/en');
		function translate_str_by_id($key) { return '{' . $key . '}'; }
		define('_ASTEXE_', 1);
		ob_start();
		include $root . '/content/shop/order_process/my_order.php';
		$html = ob_get_clean();
		$unread = (int)$db_link->query('SELECT COUNT(*) FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0')->fetchColumn();
		echo json_encode(array('name'=>$case['name'],'html'=>$html,'unread_after'=>$unread), JSON_UNESCAPED_UNICODE);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $name . '`');
		foreach (array(
			'/content/users/dp_user.php', '/content/shop/order_process/orders_background.php', '/content/general/actions_alert.php',
			'/content/shop/pricing/epc_currency.php', '/content/shop/pricing/epc_customer_trade.php', '/content/shop/pricing/epc_pricing.php',
			'/content/shop/payments/epc_payment_method_picker.php', '/content/shop/finance/epc_uae_customer_vat.php'
		) as $file) { @unlink($doc . $file); }
		foreach (array('/content/users','/content/shop/order_process','/content/shop/pricing','/content/shop/payments','/content/shop/finance','/content/shop','/content/general','/content','') as $dir) { @rmdir($doc . $dir); }
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
