<?php
// PHP 8.3 goldens for plan Q1-reed (storefront customer-balance module). Leftover unique user helper stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['REED_DBS'] = array();

function reed_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = preg_replace(
		'/require_once\(\s*\$_SERVER\["DOCUMENT_ROOT"\]\s*\.\s*["\']\/content\/users\/dp_user\.php["\']\s*\)\s*;/',
		'// leftover user injected',
		$code
	);
	file_put_contents($dest, $code);
}

function reed_pdo(string $suffix): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_reed_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['REED_DBS'][] = $name;
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE `shop_currencies` (`iso_code` VARCHAR(8) NOT NULL, `sign` VARCHAR(16) NOT NULL, `caption_short` VARCHAR(32) NOT NULL)');
	$pdo->exec('CREATE TABLE `shop_users_accounting` (
		`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`user_id` INT NOT NULL,
		`amount` DECIMAL(12,2) NOT NULL,
		`income` TINYINT NOT NULL,
		`active` TINYINT NOT NULL
	)');
	return $pdo;
}

function reed_drop(): void
{
	if (empty($GLOBALS['REED_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['REED_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['REED_DBS'] = array();
}

function reed_stubs(): void
{
	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id)
		{
			$map = $GLOBALS['REED_IDS'] ?? array();
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
			public static function getUserId()
			{
				return (int) ($GLOBALS['REED_USER'] ?? 0);
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

function reed_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	reed_stubs();
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['REED_DOCROOT'];
	$db_link = $GLOBALS['db_link'];
	$DP_Config = $GLOBALS['DP_Config'];
	require $path;
}

function reed_render(PDO $pdo, int $userId, string $iso, string $mode, string $sign, string $short): string
{
	$GLOBALS['REED_USER'] = $userId;
	$cfg = new DP_Config();
	$cfg->shop_currency = $iso;
	$cfg->currency_show_mode = $mode;
	$GLOBALS['DP_Config'] = $cfg;
	$GLOBALS['db_link'] = $pdo;
	$ins = $pdo->prepare('INSERT INTO `shop_currencies` (`iso_code`,`sign`,`caption_short`) VALUES (?,?,?)');
	$pdo->exec('DELETE FROM `shop_currencies`');
	$ins->execute(array($iso, $sign, $short));
	$tmp = sys_get_temp_dir() . '/ecomae_reed_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['REED_PAGE'], $tmp);
	ob_start();
	reed_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function reed_seed_rows(PDO $pdo, array $rows): void
{
	$pdo->exec('DELETE FROM `shop_users_accounting`');
	$ins = $pdo->prepare('INSERT INTO `shop_users_accounting` (`user_id`,`amount`,`income`,`active`) VALUES (?,?,?,?)');
	foreach ($rows as $row) {
		$ins->execute($row);
	}
}

function reed_run_guest(): array
{
	$GLOBALS['REED_IDS'] = array(4655 => 'Balance');
	$pdo = reed_pdo('guest');
	$guest = reed_render($pdo, 0, 'AED', 'sign_after', 'د.إ', 'AED');
	$zeroUser = reed_render($pdo, 0, 'USD', 'sign_before', '$', 'USD');
	return array($guest, $zeroUser);
}

function reed_run_signed(): array
{
	$GLOBALS['REED_IDS'] = array(4655 => 'Balance');
	$pdo = reed_pdo('signed');
	reed_seed_rows($pdo, array());
	$empty = reed_render($pdo, 7, 'AED', 'sign_after', 'د.إ', 'AED');
	reed_seed_rows($pdo, array(
		array(7, 10.00, 1, 1),
		array(7, 2.50, 0, 1),
		array(7, 99.00, 1, 0),
		array(8, 50.00, 1, 1),
	));
	$net = reed_render($pdo, 7, 'AED', 'sign_after', 'د.إ', 'AED');
	reed_seed_rows($pdo, array(
		array(7, 0.00, 1, 1),
	));
	$zero = reed_render($pdo, 7, 'AED', 'sign_after', 'د.إ', 'AED');
	return array($empty, $net, $zero);
}

function reed_run_currency(): array
{
	$GLOBALS['REED_IDS'] = array(4655 => 'رصيد');
	$pdo = reed_pdo('currency');
	reed_seed_rows($pdo, array(array(7, 12.5, 1, 1)));
	$before = reed_render($pdo, 7, 'AED', 'sign_before', 'د.إ', 'AED');
	$after = reed_render($pdo, 7, 'AED', 'sign_after', 'د.إ', 'AED');
	$none = reed_render($pdo, 7, 'AED', 'no', 'د.إ', 'AED');
	$shortAfter = reed_render($pdo, 7, 'AED', 'short_name_after', 'د.إ', 'AED');
	$shortBefore = reed_render($pdo, 7, 'AED', 'short_name_before', 'د.إ', 'AED');
	return array($before, $after, $none, $shortAfter, $shortBefore);
}

function reed_run_tenant(): array
{
	$GLOBALS['REED_IDS'] = array(4655 => 'Balance');
	$acme = reed_pdo('acme');
	reed_seed_rows($acme, array(array(7, 100.00, 1, 1)));
	$acmeHtml = reed_render($acme, 7, 'AED', 'sign_after', 'د.إ', 'AED');
	$beta = reed_pdo('beta');
	reed_seed_rows($beta, array(array(8, 5.00, 1, 1)));
	$betaHtml = reed_render($beta, 8, 'USD', 'sign_before', '$', 'USD');
	return array($acmeHtml, $betaHtml);
}

register_shutdown_function('reed_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_reed_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	reed_patch($root . '/modules/shop/balance/module.php', $tmp . '/page.php');
	$GLOBALS['REED_PAGE'] = $tmp . '/page.php';
	$GLOBALS['REED_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	reed_stubs();
	$fn = 'reed_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		reed_drop();
		exit(1);
	}
	reed_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1reed_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1reed_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
