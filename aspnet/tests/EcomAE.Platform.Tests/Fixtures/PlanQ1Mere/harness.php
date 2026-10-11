<?php
// PHP 8.3 goldens for plan Q1-mere (storefront cart module). Leftover unique user helper stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['MERE_DBS'] = array();

function mere_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = preg_replace(
		'/require_once\(\s*\$_SERVER\[[\'"]DOCUMENT_ROOT[\'"]\]\s*\.\s*[\'"]\/content\/users\/dp_user\.php[\'"]\s*\)\s*;/',
		'// leftover user injected',
		$code
	);
	file_put_contents($dest, $code);
}

function mere_pdo(string $suffix): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_mere_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['MERE_DBS'][] = $name;
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE `shop_currencies` (`iso_code` VARCHAR(8) NOT NULL, `sign` VARCHAR(16) NOT NULL, `caption_short` VARCHAR(32) NOT NULL)');
	$pdo->exec('CREATE TABLE `shop_carts` (
		`id` INT NOT NULL PRIMARY KEY,
		`user_id` INT NOT NULL,
		`price` DECIMAL(12,2) NOT NULL,
		`count_need` INT NOT NULL
	)');
	return $pdo;
}

function mere_drop(): void
{
	if (empty($GLOBALS['MERE_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['MERE_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['MERE_DBS'] = array();
}

function mere_stubs(): void
{
	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id)
		{
			$map = $GLOBALS['MERE_IDS'] ?? array();
			$s = (string) $id;
			if (isset($map[$s])) {
				return $map[$s];
			}
			$i = (int) $s;
			if (isset($map[$i])) {
				return $map[$i];
			}
			return $s;
		}
	}
	if (!class_exists('DP_User', false)) {
		class DP_User
		{
			public static function getUserSession()
			{
				return $GLOBALS['MERE_SESSION'] ?? array('csrf_guard_key' => 'tok-1');
			}
			public static function getUserId()
			{
				return (int) ($GLOBALS['MERE_USER'] ?? 0);
			}
		}
	}
	if (!class_exists('DP_Config', false)) {
		class DP_Config
		{
			public $shop_currency = 'AED';
			public $currency_show_mode = 'sign_after';
		}
	}
}

function mere_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	mere_stubs();
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['MERE_DOCROOT'];
	$db_link = $GLOBALS['db_link'];
	$DP_Config = $GLOBALS['DP_Config'];
	$multilang_params = $GLOBALS['MERE_LANG'] ?? array('lang_href' => '/en');
	require $path;
}

function mere_render(PDO $pdo, int $userId, string $iso, string $mode, string $sign, string $short, ?string $cookie): string
{
	$GLOBALS['MERE_USER'] = $userId;
	$cfg = new DP_Config();
	$cfg->shop_currency = $iso;
	$cfg->currency_show_mode = $mode;
	$GLOBALS['DP_Config'] = $cfg;
	$GLOBALS['db_link'] = $pdo;
	$_COOKIE = array();
	if ($cookie !== null) {
		$_COOKIE['products_in_cart'] = $cookie;
	}
	$pdo->exec('DELETE FROM `shop_currencies`');
	$pdo->prepare('INSERT INTO `shop_currencies` (`iso_code`,`sign`,`caption_short`) VALUES (?,?,?)')->execute(array($iso, $sign, $short));
	$tmp = sys_get_temp_dir() . '/ecomae_mere_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['MERE_PAGE'], $tmp);
	ob_start();
	mere_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function mere_seed_rows(PDO $pdo, array $rows): void
{
	$pdo->exec('DELETE FROM `shop_carts`');
	$ins = $pdo->prepare('INSERT INTO `shop_carts` (`id`,`user_id`,`price`,`count_need`) VALUES (?,?,?,?)');
	foreach ($rows as $row) {
		$ins->execute($row);
	}
}

function mere_run_guest(): array
{
	$GLOBALS['MERE_IDS'] = array(4495 => 'Items');
	$GLOBALS['MERE_LANG'] = array('lang_href' => '/en');
	$pdo = mere_pdo('guest');
	mere_seed_rows($pdo, array(
		array(1, 0, 10.00, 2),
		array(2, 0, 5.50, 1),
		array(3, 7, 99.00, 1),
	));
	$none = mere_render($pdo, 0, 'AED', 'sign_after', 'د.إ', 'AED', null);
	$cookie = mere_render($pdo, 0, 'AED', 'sign_after', 'د.إ', 'AED', '[1,2]');
	return array($none, $cookie);
}

function mere_run_signed(): array
{
	$GLOBALS['MERE_IDS'] = array(4495 => 'Items');
	$GLOBALS['MERE_LANG'] = array('lang_href' => '/en');
	$pdo = mere_pdo('signed');
	mere_seed_rows($pdo, array());
	$empty = mere_render($pdo, 7, 'AED', 'sign_after', 'د.إ', 'AED', '[1,2]');
	mere_seed_rows($pdo, array(
		array(1, 7, 10.00, 2),
		array(2, 7, 5.50, 1),
		array(3, 8, 99.00, 1),
	));
	$net = mere_render($pdo, 7, 'AED', 'sign_after', 'د.إ', 'AED', '[3]');
	return array($empty, $net);
}

function mere_run_currency(): array
{
	$GLOBALS['MERE_IDS'] = array(4495 => 'Позиции');
	$GLOBALS['MERE_LANG'] = array('lang_href' => '/ar');
	$pdo = mere_pdo('currency');
	mere_seed_rows($pdo, array(array(1, 7, 12.5, 1)));
	$before = mere_render($pdo, 7, 'AED', 'sign_before', 'د.إ', 'AED', null);
	$after = mere_render($pdo, 7, 'AED', 'sign_after', 'د.إ', 'AED', null);
	$none = mere_render($pdo, 7, 'AED', 'no', 'د.إ', 'AED', null);
	$short = mere_render($pdo, 7, 'AED', 'short_name_after', 'د.إ', 'AED', null);
	return array($before, $after, $none, $short);
}

function mere_run_tenant(): array
{
	$GLOBALS['MERE_IDS'] = array(4495 => 'Items');
	$GLOBALS['MERE_LANG'] = array('lang_href' => '/en');
	$acme = mere_pdo('acme');
	mere_seed_rows($acme, array(array(1, 7, 40.00, 2)));
	$acmeHtml = mere_render($acme, 7, 'AED', 'sign_after', 'د.إ', 'AED', null);
	$GLOBALS['MERE_LANG'] = array('lang_href' => '/ar');
	$beta = mere_pdo('beta');
	mere_seed_rows($beta, array(array(2, 8, 5.00, 1)));
	$betaHtml = mere_render($beta, 8, 'USD', 'sign_before', '$', 'USD', null);
	return array($acmeHtml, $betaHtml);
}

register_shutdown_function('mere_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_mere_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	mere_patch($root . '/modules/shop/cart/cart.php', $tmp . '/page.php');
	$GLOBALS['MERE_PAGE'] = $tmp . '/page.php';
	$GLOBALS['MERE_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	mere_stubs();
	$fn = 'mere_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		mere_drop();
		exit(1);
	}
	mere_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1mere_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1mere_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
