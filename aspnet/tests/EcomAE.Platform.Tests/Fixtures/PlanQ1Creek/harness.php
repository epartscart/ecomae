<?php
// PHP 8.3 goldens for plan Q1-creek (ePartsCart home product lines). Storefront + perf-cache stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['CREEK_DBS'] = array();

function creek_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = str_replace(
		"require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';",
		'// leftover storefront injected',
		$code
	);
	$code = str_replace(
		"require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';",
		'// leftover perf-cache injected',
		$code
	);
	$code = preg_replace(
		'/function epc_ep_pl_href\(\$lang, \$path\)\s*\{.*?\}/s',
		'// leftover page href injected',
		$code
	);
	file_put_contents($dest, $code);
}

function creek_pdo(string $suffix): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_creek_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['CREEK_DBS'][] = $name;
	return new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function creek_drop(): void
{
	if (empty($GLOBALS['CREEK_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['CREEK_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['CREEK_DBS'] = array();
}

function creek_stubs(): void
{
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
			return (string) ($GLOBALS['CREEK_ALL'] ?? '/catalogue/lines');
		}
	}
	if (!function_exists('epc_electronicae_product_line_tiles')) {
		function epc_electronicae_product_line_tiles($pdo, $siteKey, $limit = 8)
		{
			$GLOBALS['CREEK_QUERY_SITE'] = $siteKey;
			$GLOBALS['CREEK_QUERY_LIMIT'] = $limit;
			return $GLOBALS['CREEK_TILES'] ?? array();
		}
	}
	if (!function_exists('epc_perf_cache_get')) {
		function epc_perf_cache_get($key)
		{
			$GLOBALS['CREEK_CACHE_KEY'] = $key;
			if (array_key_exists('CREEK_CACHE', $GLOBALS)) {
				return $GLOBALS['CREEK_CACHE'];
			}
			return null;
		}
	}
	if (!function_exists('epc_ep_pl_href')) {
		function epc_ep_pl_href($lang, $path)
		{
			return htmlspecialchars(epc_electronicae_href($path, $lang), ENT_QUOTES, 'UTF-8');
		}
	}
}

function creek_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	creek_stubs();
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['CREEK_DOCROOT'];
	$db_link = $GLOBALS['db_link'] ?? null;
	$multilang_params = $GLOBALS['CREEK_LANG'] ?? array();
	require $path;
}

function creek_render(mixed $pdo, array $lang, mixed $cache, mixed $fast, mixed $tiles, string $all): string
{
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['CREEK_LANG'] = $lang;
	$GLOBALS['CREEK_CACHE'] = $cache;
	$GLOBALS['epc_home_perf_fast'] = $fast;
	$GLOBALS['CREEK_TILES'] = $tiles;
	$GLOBALS['CREEK_ALL'] = $all;
	$GLOBALS['CREEK_QUERY_SITE'] = null;
	$GLOBALS['CREEK_CACHE_KEY'] = null;
	$tmp = sys_get_temp_dir() . '/ecomae_creek_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['CREEK_PAGE'], $tmp);
	ob_start();
	creek_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function creek_sample_tiles(): array
{
	return array(
		array('name' => 'Brakes', 'href' => '/brakes', 'image' => '/img/brakes.jpg', 'icon' => 'fa-car', 'product_count' => 2),
		array('name' => 'Engine', 'href' => '/engine', 'image' => '', 'icon' => 'fa-cogs', 'product_count' => 1),
		array('name' => 'Soon', 'href' => '/soon', 'product_count' => 0),
	);
}

function creek_run_empty(): array
{
	$none = creek_render(null, array('lang_href' => '/en'), null, null, creek_sample_tiles(), '/catalogue/lines');
	$pdo = creek_pdo('empty');
	$blank = creek_render($pdo, array(), null, null, array(), '/catalogue/lines');
	$fast = creek_render($pdo, array('lang_href' => '/en'), null, 1, creek_sample_tiles(), '/catalogue/lines');
	return array($none, $blank, $fast);
}

function creek_run_tiles(): array
{
	$pdo = creek_pdo('tiles');
	$live = creek_render($pdo, array('lang_href' => '/en'), null, null, creek_sample_tiles(), '/catalogue/lines');
	$cached = creek_render($pdo, array('lang_href' => '/en'), creek_sample_tiles(), null, array(array('name' => 'Ignored')), '/all');
	$zeroLang = creek_render($pdo, array('lang_href' => ''), null, '0', creek_sample_tiles(), '/catalogue/lines');
	return array($live, $cached, $zeroLang);
}

function creek_run_escape(): array
{
	$pdo = creek_pdo('escape');
	$tiles = array(
		array('name' => 'TV & "Audio"', 'href' => '/tv?q=a&b=1', 'image' => '/img/a".jpg', 'icon' => 'fa-tv', 'product_count' => 3),
	);
	$html = creek_render($pdo, array('lang_href' => '/ar'), null, null, $tiles, '/lines?x=1&y=2');
	return array($html);
}

function creek_run_tenant(): array
{
	$acme = creek_pdo('acme');
	$acmeTiles = array(
		array('name' => 'Acme Brakes', 'href' => '/acme-brakes', 'image' => '/img/acme-brakes.jpg', 'icon' => 'fa-car', 'product_count' => 4),
	);
	$acmeHtml = creek_render($acme, array('lang_href' => '/en'), $acmeTiles, null, array(), '/acme/lines');
	$beta = creek_pdo('beta');
	$betaTiles = array(
		array('name' => 'Beta Engine', 'href' => '/beta-engine', 'image' => '', 'icon' => 'fa-cogs', 'product_count' => 0),
	);
	$betaHtml = creek_render($beta, array('lang_href' => '/ar'), null, null, $betaTiles, '/beta/lines');
	return array($acmeHtml, $betaHtml);
}

register_shutdown_function('creek_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_creek_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	creek_patch($root . '/content/general_pages/epc_epartscart_home_product_lines.php', $tmp . '/page.php');
	$GLOBALS['CREEK_PAGE'] = $tmp . '/page.php';
	$GLOBALS['CREEK_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	creek_stubs();
	$fn = 'creek_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		creek_drop();
		exit(1);
	}
	creek_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1creek_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1creek_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
