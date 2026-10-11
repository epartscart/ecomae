<?php
// PHP 8.3 goldens for plan Q1-reach (storefront geo picker). Leftover unique user helper stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['REACH_DBS'] = array();

function reach_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = preg_replace(
		'/require_once\(\s*\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*["\']\/content\/users\/dp_user\.php["\']\s*\)\s*;/',
		'// leftover user injected',
		$code
	);
	$code = preg_replace(
		'/if\(\s*isset\(\$_COOKIE\["my_city"\]\)\s*\)/',
		"function epc_geo_render_module()\n{\n\tglobal \$db_link, \$user_session;\n\tif( isset(\$_COOKIE[\"my_city\"]) )",
		$code,
		1
	);
	$code = preg_replace(
		'/\n\/\/ \*{10,}.*\n\/\/Рекурсивная функция/s',
		"\n}\n\n// Recursive geo tree\n//Рекурсивная функция",
		$code
	);
	file_put_contents($dest, $code);
}

function reach_pdo(string $suffix, array $rows): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_reach_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['REACH_DBS'][] = $name;
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE `shop_geo` (`id` INT NOT NULL, `value` VARCHAR(255) NOT NULL, `level` INT NOT NULL, `order` INT NOT NULL)');
	$ins = $pdo->prepare('INSERT INTO `shop_geo` (`id`,`value`,`level`,`order`) VALUES (?,?,?,?)');
	foreach ($rows as $row) {
		$ins->execute($row);
	}
	return $pdo;
}

function reach_drop(): void
{
	if (empty($GLOBALS['REACH_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['REACH_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['REACH_DBS'] = array();
}

function reach_stubs(): void
{
	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id)
		{
			$map = $GLOBALS['REACH_IDS'] ?? array();
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
				return $GLOBALS['REACH_SESSION'] ?? array('csrf_guard_key' => 'tok-1');
			}
		}
	}
}

function reach_include(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	reach_stubs();
	$user_session = $GLOBALS['user_session'] ?? DP_User::getUserSession();
	$GLOBALS['user_session'] = $user_session;
	$db_link = $GLOBALS['db_link'];
	require_once $GLOBALS['REACH_PAGE'];
}

function reach_capture(callable $fn): string
{
	ob_start();
	$fn();
	return (string) ob_get_clean();
}

function reach_reset(): void
{
	$GLOBALS['REACH_SESSION'] = array('csrf_guard_key' => 'tok-1');
	$GLOBALS['REACH_IDS'] = array(
		4771 => 'Choose your city',
		100 => "O'Reilly",
		12 => 'Acme City',
	);
	$GLOBALS['user_session'] = $GLOBALS['REACH_SESSION'];
	$GLOBALS['db_link'] = null;
	$_COOKIE = array();
}

function reach_run_flat(): array
{
	reach_reset();
	$GLOBALS['db_link'] = reach_pdo('empty', array());
	reach_include();
	$empty = reach_capture(static function () {
		epc_geo_render_flat_list($GLOBALS['db_link']);
	});
	$GLOBALS['db_link']->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_SILENT);
	$GLOBALS['db_link']->exec('DROP TABLE `shop_geo`');
	$missing = reach_capture(static function () {
		epc_geo_render_flat_list($GLOBALS['db_link']);
	});
	$GLOBALS['db_link'] = reach_pdo('flat', array(
		array(1, '100', 1, 10),
		array(2, 'Dubai', 2, 20),
		array(12, "O'Reilly <x>", 3, 30),
	));
	$rows = reach_capture(static function () {
		epc_geo_render_flat_list($GLOBALS['db_link']);
	});
	return array($empty, $missing, $rows);
}

function reach_run_tree(): array
{
	reach_reset();
	$GLOBALS['db_link'] = reach_pdo('tree', array());
	reach_include();
	$bad = reach_capture(static function () {
		printGeoNodes('nope');
	});
	$deep = reach_capture(static function () {
		printGeoNodes(array(array('id' => 1, 'value' => 'X', 'level' => 1)), 33);
	});
	$tree = reach_capture(static function () {
		printGeoNodes(array(
			'skip',
			array('id' => 1, 'value' => 'UAE', 'level' => 1, 'data' => array(
				array('id' => 2, 'value' => 'Dubai', 'level' => 2),
				array('id' => 12, 'value' => "O'Reilly <x>", 'level' => 3),
			)),
		));
	});
	return array($bad, $deep, $tree);
}

function reach_run_page(): array
{
	reach_reset();
	$GLOBALS['db_link'] = reach_pdo('acme', array(
		array(1, '100', 1, 10),
		array(12, '12', 3, 20),
	));
	reach_include();
	$noCookie = reach_capture(static function () {
		epc_geo_render_module();
	});
	$_COOKIE['my_city'] = '12';
	$acme = reach_capture(static function () {
		epc_geo_render_module();
	});
	$_COOKIE['my_city'] = '99';
	$invalid = reach_capture(static function () {
		epc_geo_render_module();
	});
	$GLOBALS['db_link'] = reach_pdo('beta', array(
		array(1, 'Beta Town', 3, 10),
		array(2, 'Beta Port', 3, 20),
	));
	$GLOBALS['REACH_IDS'] = array(4771 => 'Choose your city');
	$_COOKIE['my_city'] = '1';
	$beta = reach_capture(static function () {
		epc_geo_render_module();
	});
	return array($noCookie, $acme, $invalid, $beta);
}

function reach_run_single(): array
{
	reach_reset();
	$GLOBALS['db_link'] = reach_pdo('one', array(
		array(7, 'Solo', 3, 1),
	));
	reach_include();
	$auto = reach_capture(static function () {
		epc_geo_render_module();
	});
	$_COOKIE['my_city'] = '7';
	$kept = reach_capture(static function () {
		epc_geo_render_module();
	});
	return array($auto, $kept);
}

register_shutdown_function('reach_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_reach_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	reach_patch($root . '/modules/shop/geo/point_geo_node.php', $tmp . '/page.php');
	$GLOBALS['REACH_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	reach_reset();
	$fn = 'reach_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		reach_drop();
		exit(1);
	}
	reach_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1reach_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1reach_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
