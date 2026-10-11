<?php
// PHP 8.3 goldens for plan Q1-firth (Electronicae home product lines). Haven storefront helpers stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['FIRTH_DBS'] = array();

function firth_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = str_replace(
		"require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';",
		'// leftover storefront injected',
		$code
	);
	$code = preg_replace(
		'/function epc_el_pl_href\(\$lang, \$path\)\s*\{.*?\}/s',
		'// leftover page href injected',
		$code
	);
	file_put_contents($dest, $code);
}

function firth_pdo(string $suffix): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_firth_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['FIRTH_DBS'][] = $name;
	return new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function firth_drop(): void
{
	if (empty($GLOBALS['FIRTH_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['FIRTH_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['FIRTH_DBS'] = array();
}

function firth_stubs(): void
{
	if (!function_exists('epc_electronicae_site_key')) {
		function epc_electronicae_site_key($pdo)
		{
			return (string) ($GLOBALS['FIRTH_SITE'] ?? 'electronicae');
		}
	}
	if (!function_exists('epc_electronicae_href')) {
		function epc_electronicae_href($path, $lang = '')
		{
			if ($lang === '') {
				$lang = '/en';
			}
			$path = (string) $path;
			if ($path !== '' && $path[0] === '/') {
				return rtrim((string) $lang, '/') . $path;
			}
			return $path;
		}
	}
	if (!function_exists('epc_electronicae_all_lines_href')) {
		function epc_electronicae_all_lines_href($pdo, $siteKey)
		{
			return (string) ($GLOBALS['FIRTH_ALL'] ?? '/catalogue/lines');
		}
	}
	if (!function_exists('epc_electronicae_product_line_tiles')) {
		function epc_electronicae_product_line_tiles($pdo, $siteKey, $limit = 12)
		{
			return $GLOBALS['FIRTH_TILES'] ?? array();
		}
	}
	if (!function_exists('epc_el_pl_href')) {
		function epc_el_pl_href($lang, $path)
		{
			return htmlspecialchars(epc_electronicae_href($path, $lang), ENT_QUOTES, 'UTF-8');
		}
	}
}

function firth_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	firth_stubs();
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['FIRTH_DOCROOT'];
	$db_link = $GLOBALS['db_link'] ?? null;
	$multilang_params = $GLOBALS['FIRTH_LANG'] ?? array();
	require $path;
}

function firth_render(mixed $pdo, array $lang, string $site, mixed $tiles, string $all): string
{
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['FIRTH_LANG'] = $lang;
	$GLOBALS['FIRTH_SITE'] = $site;
	$GLOBALS['FIRTH_TILES'] = $tiles;
	$GLOBALS['FIRTH_ALL'] = $all;
	$tmp = sys_get_temp_dir() . '/ecomae_firth_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['FIRTH_PAGE'], $tmp);
	ob_start();
	firth_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function firth_run_empty(): array
{
	$none = firth_render(null, array('lang_href' => '/en'), 'electronicae', array(), '/catalogue/lines');
	$pdo = firth_pdo('empty');
	$blank = firth_render($pdo, array(), 'electronicae', array(), '/catalogue/lines');
	return array($none, $blank);
}

function firth_run_tiles(): array
{
	$pdo = firth_pdo('tiles');
	$tiles = array(
		array('name' => 'Phones', 'href' => '/phones', 'image' => '/img/phones.jpg', 'icon' => 'fa-mobile', 'accent' => '#1a73e8', 'product_count' => 2, 'trend' => 'hot'),
		array('name' => 'Laptops', 'href' => '/laptops', 'image' => '', 'icon' => 'fa-laptop', 'accent' => '#5f6368', 'product_count' => 1, 'trend' => ''),
		array('name' => 'Soon', 'href' => '/soon', 'product_count' => 0),
	);
	$html = firth_render($pdo, array('lang_href' => '/en'), 'electronicae', $tiles, '/catalogue/lines');
	$zeroLang = firth_render($pdo, array('lang_href' => ''), 'electronicae', $tiles, '/all');
	return array($html, $zeroLang);
}

function firth_run_escape(): array
{
	$pdo = firth_pdo('escape');
	$tiles = array(
		array('name' => 'TV & "Audio"', 'href' => '/tv?q=a&b=1', 'image' => '/img/a".jpg', 'icon' => 'fa-tv', 'accent' => 'red;alert(1)', 'product_count' => 3, 'trend' => 'hot'),
	);
	$html = firth_render($pdo, array('lang_href' => '/ar'), 'electronicae', $tiles, '/lines?x=1&y=2');
	return array($html);
}

function firth_run_tenant(): array
{
	$acme = firth_pdo('acme');
	$acmeTiles = array(
		array('name' => 'Acme Phones', 'href' => '/acme-phones', 'image' => '/img/acme-phones.jpg', 'icon' => 'fa-mobile', 'accent' => '#e10a0a', 'product_count' => 4, 'trend' => 'hot'),
	);
	$acmeHtml = firth_render($acme, array('lang_href' => '/en'), 'acme', $acmeTiles, '/acme/lines');
	$beta = firth_pdo('beta');
	$betaTiles = array(
		array('name' => 'Beta Laptops', 'href' => '/beta-laptops', 'image' => '', 'icon' => 'fa-laptop', 'accent' => '#202124', 'product_count' => 0, 'trend' => ''),
	);
	$betaHtml = firth_render($beta, array('lang_href' => '/ar'), 'beta', $betaTiles, '/beta/lines');
	return array($acmeHtml, $betaHtml);
}

register_shutdown_function('firth_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_firth_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	firth_patch($root . '/content/general_pages/epc_electronicae_home_product_lines.php', $tmp . '/page.php');
	$GLOBALS['FIRTH_PAGE'] = $tmp . '/page.php';
	$GLOBALS['FIRTH_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	firth_stubs();
	$fn = 'firth_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		firth_drop();
		exit(1);
	}
	firth_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1firth_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1firth_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
