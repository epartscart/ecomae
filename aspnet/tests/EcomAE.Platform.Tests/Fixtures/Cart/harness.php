<?php
// Runs the real PHP 8.3 cart.php on one isolated ecomae_cpw_* MariaDB schema per case.
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=... php harness.php /workspace > golden.json
ini_set('display_errors', 'stderr');
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
		foreach (array('/content/users','/content/shop/order_process','/content/shop/docpart','/content/shop/pricing','/content/general_pages','/content/files/images/products_images') as $dir) {
			mkdir($doc . $dir, 0777, true);
		}
		file_put_contents($doc . '/content/files/images/products_images/exists.jpg', 'fixture');
		file_put_contents($doc . '/content/users/dp_user.php', <<<'PHP'
<?php
class DP_User {
	public static function getUserId(){ return (int)$GLOBALS['__case']['user_id']; }
	public static function getUserSession(){ return $GLOBALS['__case']['session']; }
}
PHP);
		file_put_contents($doc . '/content/shop/docpart/epc_storefront_prices_helpers.php', <<<'PHP'
<?php
function epc_storefront_prices_styles(){ return '[[PRICE_STYLES]]'; }
function epc_storefront_guest_commerce_blocked($uid=0){ return !empty($GLOBALS['__case']['blocked']); }
function epc_storefront_auth_login_url($m=null){ return (($m['lang_href'] ?? '/en') . '/login'); }
function epc_storefront_auth_signup_url($m=null){ return (($m['lang_href'] ?? '/en') . '/reg'); }
function epc_storefront_clear_guest_cart(PDO $db,int $sid){
	$q=$db->prepare('SELECT id FROM shop_carts WHERE user_id=0 AND session_id=?'); $q->execute(array($sid));
	$ids=$q->fetchAll(PDO::FETCH_COLUMN);
	if($ids){$p=implode(',',array_fill(0,count($ids),'?'));$db->prepare("DELETE FROM shop_carts_details WHERE cart_record_id IN ($p)")->execute($ids);}
	$db->prepare('DELETE FROM shop_carts WHERE user_id=0 AND session_id=?')->execute(array($sid));
}
PHP);
		file_put_contents($doc . '/content/general_pages/epc_whatsapp_share.php', <<<'PHP'
<?php
function epc_wa_styles(){ return '[[WA_STYLES]]'; }
function epc_wa_frontend_script($c){ return '[[WA_SCRIPT]]'; }
PHP);
		file_put_contents($doc . '/content/shop/pricing/epc_customer_trade.php', <<<'PHP'
<?php
function epc_trade_can_place_order($db,$uid){ return ($GLOBALS['__case']['trade'] ?? 'approved') === 'approved'; }
function epc_trade_checkout_block_message($db,$uid){
	if(isset($GLOBALS['__case']['trade_message'])) return $GLOBALS['__case']['trade_message'];
	return ($GLOBALS['__case']['trade'] ?? '') === 'pending' ? 'Awaiting manager approval.' : 'Trade account blocked.';
}
PHP);
		$db_link = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		foreach (array_merge($spec['schema'], $spec['base'], $case['sql'] ?? array()) as $sql) {
			$db_link->exec($sql);
		}
		$GLOBALS['__case'] = $case;
		$_SERVER['DOCUMENT_ROOT'] = $doc;
		$_SERVER['HTTP_HOST'] = 'fixture.example';
		$_GET = $_POST = $_COOKIE = array();
		$DP_Config = new stdClass();
		$DP_Config->domain_path = 'https://fixture.example';
		foreach ($case['config'] as $key => $value) { $DP_Config->$key = $value; }
		$multilang_params = array('lang_href' => $case['lang_href']);
		function translate_str_by_id($key){ return '{' . $key . '}'; }
		function translate_str_by_key($key){ return '{' . $key . '}'; }
		define('_ASTEXE_', 1);
		ob_start();
		include $root . '/content/shop/order_process/cart.php';
		$html = ob_get_clean();
		$state = $db_link->query('SELECT id,checked_for_order FROM shop_carts ORDER BY id')->fetchAll(PDO::FETCH_ASSOC);
		echo json_encode(array('name'=>$case['name'],'html'=>$html,'cart_after'=>$state), JSON_UNESCAPED_UNICODE);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $name . '`');
		foreach (array(
			'/content/files/images/products_images/exists.jpg',
			'/content/users/dp_user.php',
			'/content/shop/docpart/epc_storefront_prices_helpers.php',
			'/content/general_pages/epc_whatsapp_share.php',
			'/content/shop/pricing/epc_customer_trade.php'
		) as $file) { @unlink($doc . $file); }
		foreach (array('/content/files/images/products_images','/content/files/images','/content/files','/content/users','/content/shop/order_process','/content/shop/docpart','/content/shop/pricing','/content/shop','/content/general_pages','/content','') as $dir) { @rmdir($doc . $dir); }
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
echo json_encode(array('php'=>PHP_VERSION,'results'=>$results), JSON_PRETTY_PRINT|JSON_UNESCAPED_UNICODE), "\n";
