<?php
// Executes PHP 8.3 checkout_confirm.php on one isolated ecomae_cpw_* schema per case.
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=... php harness.php /workspace > golden.json
ini_set('display_errors', 'stderr');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int)$argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$database_name = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$doc = sys_get_temp_dir() . '/' . $database_name;
	$admin->exec('CREATE DATABASE `' . $database_name . '` CHARACTER SET utf8mb4');
	foreach (array(
		'/content/users',
		'/content/shop/order_process',
		'/content/shop/pricing',
		'/content/shop/docpart',
		'/content/shop/obtaining_modes/fixture_handler'
	) as $dir) {
		mkdir($doc . $dir, 0777, true);
	}
	file_put_contents($doc . '/content/users/dp_user.php', <<<'PHP'
<?php
class DP_User {
	public static function getUserId(){ return (int)$GLOBALS['__case']['user_id']; }
	public static function getUserSession(){ return $GLOBALS['__case']['session']; }
}
PHP);
	file_put_contents($doc . '/content/shop/pricing/epc_customer_trade.php', <<<'PHP'
<?php
function epc_trade_can_place_order($db,$uid){ return ($GLOBALS['__case']['trade'] ?? 'approved') === 'approved'; }
function epc_trade_checkout_block_message($db,$uid){ return (string)($GLOBALS['__case']['trade_message'] ?? ''); }
function epc_trade_profile_get($db,$uid,$key,$default=''){
	$q=$db->prepare('SELECT data_value FROM users_profiles WHERE user_id=? AND data_key=? LIMIT 1');
	$q->execute(array($uid,$key)); $v=$q->fetchColumn(); return $v===false ? $default : trim((string)$v);
}
PHP);
	file_put_contents($doc . '/content/shop/docpart/epc_complementary_parts.php', <<<'PHP'
<?php
function epc_complementary_suggest_for_cart($db,$uid,$sid,$limit=8){ return array(); }
function epc_complementary_render_html($suggestions,$heading=''){ return (string)($GLOBALS['__case']['complementary_html'] ?? ''); }
PHP);
	file_put_contents($doc . '/content/shop/obtaining_modes/fixture_handler/show_details.php', <<<'PHP'
<?php
echo '[[DETAILS:' . $obtain_mode['handler'] . ':' . json_encode($how_get_json, JSON_UNESCAPED_UNICODE) . ']]';
PHP);
	copy($root . '/content/users/users_agreement_module.php', $doc . '/content/users/users_agreement_module.php');

	$db_link = new PDO(
		'mysql:host=127.0.0.1;port=3306;dbname=' . $database_name . ';charset=utf8mb4',
		'ecomae',
		$password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	foreach (array_merge($spec['schema'], $spec['base'], $case['sql'] ?? array()) as $sql) {
		$db_link->exec($sql);
	}
	$GLOBALS['__case'] = $case;
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['HTTP_HOST'] = 'fixture.example';
	$_GET = $_POST = array();
	$_COOKIE = array('how_get' => $case['how_get']);
	$DP_Config = new stdClass();
	foreach ($case['config'] as $key => $value) {
		$DP_Config->$key = $value;
	}
	$multilang_params = array('lang_href' => $case['lang_href']);
	function translate_str_by_id($key){ return '{' . $key . '}'; }
	define('_ASTEXE_', 1);

	$before = hash('sha256', json_encode($db_link->query('SELECT * FROM shop_carts ORDER BY id')->fetchAll(PDO::FETCH_ASSOC)));
	$done = false;
	function checkout_fixture_finish()
	{
		global $done, $db_link, $admin, $database_name, $doc, $case, $before;
		if ($done) return;
		$done = true;
		$html = ob_get_level() > 0 ? ob_get_clean() : '';
		$after = hash('sha256', json_encode($db_link->query('SELECT * FROM shop_carts ORDER BY id')->fetchAll(PDO::FETCH_ASSOC)));
		$result = array(
			'name' => $case['name'],
			'html' => $html,
			'redirect' => in_array($case['name'], array(
				'missing_how_get_redirect','invalid_how_get_redirect','unavailable_mode_redirect','missing_handler_redirect'
			), true) ? $case['lang_href'] . '/shop/checkout/how_get' : null,
			'cart_before' => $before,
			'cart_after' => $after
		);
		foreach (array_keys($GLOBALS) as $global_key) {
			if (($GLOBALS[$global_key] ?? null) instanceof PDOStatement) {
				unset($GLOBALS[$global_key]);
			}
		}
		$db_link->exec('USE `mysql`');
		$db_link = null;
		$admin->exec('DROP DATABASE IF EXISTS `' . $database_name . '`');
		echo json_encode($result, JSON_UNESCAPED_UNICODE);
		foreach (array(
			'/content/users/dp_user.php',
			'/content/users/users_agreement_module.php',
			'/content/shop/pricing/epc_customer_trade.php',
			'/content/shop/docpart/epc_complementary_parts.php',
			'/content/shop/obtaining_modes/fixture_handler/show_details.php'
		) as $file) { @unlink($doc . $file); }
		foreach (array(
			'/content/shop/obtaining_modes/fixture_handler','/content/shop/obtaining_modes',
			'/content/shop/order_process','/content/shop/pricing','/content/shop/docpart','/content/shop',
			'/content/users','/content',''
		) as $dir) { @rmdir($doc . $dir); }
	}
	register_shutdown_function('checkout_fixture_finish');
	ob_start();
	include $root . '/content/shop/order_process/checkout_confirm.php';
	checkout_fixture_finish();
	exit;
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$out = shell_exec(
		'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string)getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i
	);
	$row = json_decode((string)$out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n$out\n");
		exit(1);
	}
	$html = (string)$row['html'];
	$results[] = array(
		'name' => $row['name'],
		'html_sha256' => hash('sha256', $html),
		'html_length' => strlen($html),
		'redirect' => $row['redirect'],
		'cart_before' => $row['cart_before'],
		'cart_after' => $row['cart_after']
	);
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT|JSON_UNESCAPED_UNICODE), "\n";
