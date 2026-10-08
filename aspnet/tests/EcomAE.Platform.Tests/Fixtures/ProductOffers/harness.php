<?php
// Runs the offers block of PHP content/shop/catalogue/product_page_for_customer.php and the
// content/shop/order_process/common_add_to_basket.php script for every case on throwaway MariaDB databases.
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=... php harness.php /workspace > golden.json
// Each case runs in a child process because epc_pricing.php keeps static per-request caches.
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
const FIXED_NOW = 1700000000;
ini_set("display_errors", "stderr");

if (isset($argv[2])) {
	echo json_encode(run_case($root, $spec, $spec['cases'][(int) $argv[2]]), JSON_UNESCAPED_UNICODE);
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
$script = render_script($root, 'CSRF_KEY_1');
echo json_encode(array('php' => PHP_VERSION, 'now' => FIXED_NOW, 'csrf' => 'CSRF_KEY_1', 'script' => $script, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE), "\n";

function translate_str_by_id($key)
{
	return '{' . $key . '}';
}

function render_script($root, $csrf)
{
	$source = file_get_contents($root . '/content/shop/order_process/common_add_to_basket.php');
	$body = substr($source, strpos($source, '?>') + 2);
	$user_session = array('csrf_guard_key' => $csrf);
	ob_start();
	eval('?>' . $body);
	return ob_get_clean();
}

function run_case($root, $spec, $case)
{
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$name = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $name . '`');
	try {
		$db_link = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		foreach (array_merge($spec['schema'], $spec['base'], $case['setup']) as $sql) {
			$db_link->exec($sql);
		}
		return array('name' => $case['name'], 'html' => render_offers($root, $db_link, $case));
	} finally {
		$admin->exec('DROP DATABASE `' . $name . '`');
	}
}

function profile_first_group($db_link, $user_id)
{
	if ($user_id == 0) {
		$q = $db_link->prepare('SELECT * FROM `groups` WHERE `for_guests` = ?;');
		$q->execute(array(1));
		$r = $q->fetch();
		return $r ? $r['id'] : null;
	}
	$q = $db_link->prepare('SELECT * FROM `users_groups_bind` WHERE `user_id` = ?;');
	$q->execute(array($user_id));
	if ($r = $q->fetch()) {
		return $r['group_id'];
	}
	$r = $db_link->query('SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1;')->fetch();
	return $r ? (int) $r['id'] : null;
}

function render_offers($root, $db_link, $case)
{
	require_once $root . '/content/shop/pricing/epc_pricing.php';
	$DP_Config = new stdClass();
	$DP_Config->price_rounding = $case['config']['price_rounding'];
	$DP_Config->tech_key = $case['config']['tech_key'];
	$_COOKIE = array();
	if ($case['cookie'] !== null) {
		$_COOKIE['my_city'] = $case['cookie'];
	}
	$user_id = (int) $case['user_id'];
	$group_id = profile_first_group($db_link, $user_id);
	$group_id = epc_pricing_resolve_customer_group_id($db_link, $user_id, (int) $group_id);
	$product_id = (string) ($case['product_id'] ?? 7);
	$min_order = array_key_exists('min_order', $case) ? $case['min_order'] : '1';
	$multilang_params = array('lang_href' => $case['lang_href'] ?? '/en');
	include $root . '/content/shop/order_process/get_customer_offices.php';

	$source = file_get_contents($root . '/content/shop/catalogue/product_page_for_customer.php');
	$start = strpos($source, '//Подстрока для умножение');
	$end = strpos($source, '<div class="col-md-12">', $start);
	$snippet = str_replace('time()', 'FIXED_NOW', substr($source, $start, $end - $start));
	ob_start();
	eval($snippet);
	return ob_get_clean();
}
