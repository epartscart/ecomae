<?php
// PHP 8.3 goldens for plan Q1-cape (epartscart storefront helper). APE parents stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function cape_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace(
		'/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/shop\/price_engine\/epc_auto_price_storefront\.php[\'"]\s*;/',
		'// leftover APE storefront injected',
		$code
	);
	file_put_contents($dest, $code);
}

function cape_stubs(): void
{
	if (!function_exists('epc_apai_is_warehouse_auto_parts_storefront')) {
		function epc_apai_is_warehouse_auto_parts_storefront($pdo): bool
		{
			return !empty($GLOBALS['CAPE_ACTIVE']);
		}
	}
	if (!function_exists('epc_apai_storefront_lang_prefix')) {
		function epc_apai_storefront_lang_prefix(): string
		{
			return (string) ($GLOBALS['CAPE_LANG'] ?? '/en/');
		}
	}
	if (!function_exists('epc_apai_resolve_catalogue_product_route')) {
		function epc_apai_resolve_catalogue_product_route($pdo, $url, $mode)
		{
			$GLOBALS['CAPE_RESOLVE_SEEN'][] = array($url, $mode);
			return $GLOBALS['CAPE_RESOLVE'] ?? null;
		}
	}
}

function cape_include(): void
{
	cape_stubs();
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	include_once $GLOBALS['CAPE_PAGE'];
}

function cape_pdo(): PDO
{
	static $pdo = null;
	if ($pdo instanceof PDO) {
		return $pdo;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	return $pdo;
}

function cape_run_names()
{
	cape_include();
	return array(
		epc_epartscart_is_apai_alias(''),
		epc_epartscart_is_apai_alias('Tires') ? 1 : 0,
		epc_epartscart_is_apai_alias('APAI-Oil') ? 1 : 0,
		epc_epartscart_is_apai_alias('apai_filters') ? 1 : 0,
		epc_epartscart_is_apai_alias('  APAI-x  ') ? 1 : 0,
		epc_epartscart_is_apai_url('') ? 1 : 0,
		epc_epartscart_is_apai_url('/apai-oil') ? 1 : 0,
		epc_epartscart_is_apai_url('apai_filters') ? 1 : 0,
		epc_epartscart_is_apai_url('shop/apai-oil') ? 1 : 0,
		epc_epartscart_is_apai_url('tires') ? 1 : 0,
		epc_storefront_catalog_placeholder_for_hint('Electronics UAE'),
		epc_storefront_catalog_placeholder_for_hint('Fashion / apparel'),
		epc_storefront_catalog_placeholder_for_hint('Jewellery'),
		epc_storefront_catalog_placeholder_for_hint('svc-tax-advisor'),
		epc_storefront_catalog_placeholder_for_hint('auto parts'),
		epc_storefront_catalog_placeholder_for_hint('other'),
	);
}

function cape_run_flags()
{
	cape_include();
	$pdo = cape_pdo();
	$GLOBALS['CAPE_ACTIVE'] = 0;
	$off = epc_epartscart_storefront_active($pdo) ? 1 : 0;
	$phOff = epc_epartscart_catalog_placeholder_url($pdo);
	$neuOff = epc_epartscart_use_neutral_product_image($pdo) ? 1 : 0;
	$GLOBALS['CAPE_ACTIVE'] = 1;
	$on = epc_epartscart_storefront_active($pdo) ? 1 : 0;
	$phOn = epc_epartscart_catalog_placeholder_url($pdo);
	$neuOn = epc_epartscart_use_neutral_product_image($pdo) ? 1 : 0;
	$phNull = epc_epartscart_catalog_placeholder_url(null);
	unset($GLOBALS['CAPE_LANG']);
	$langDefault = epc_epartscart_lang_href();
	$GLOBALS['CAPE_LANG'] = '/ar/';
	$langAr = epc_epartscart_lang_href();
	return array($off, $phOff, $neuOff, $on, $phOn, $neuOn, $phNull, $langDefault, $langAr);
}

function cape_place_categories(bool $present): void
{
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine';
	@mkdir($dir, 0777, true);
	$path = $dir . '/epc_auto_price_categories.php';
	if ($present) {
		file_put_contents($path, "<?php\n");
	} elseif (is_file($path)) {
		unlink($path);
	}
}

function cape_run_redirect()
{
	cape_include();
	$pdo = cape_pdo();
	$GLOBALS['CAPE_ACTIVE'] = 0;
	$GLOBALS['CAPE_LANG'] = '/en/';
	cape_place_categories(false);
	$inactive = epc_epartscart_apai_category_redirect($pdo, 'apai-oil');
	$GLOBALS['CAPE_ACTIVE'] = 1;
	$notApai = epc_epartscart_apai_category_redirect($pdo, 'tires');
	$noFile = epc_epartscart_apai_category_redirect($pdo, 'apai-oil');
	cape_place_categories(true);
	$GLOBALS['CAPE_RESOLVE'] = array('product' => 'OC47');
	$cfg = new stdClass();
	$cfg->product_url = 'alias';
	$GLOBALS['DP_Config'] = $cfg;
	$withProduct = epc_epartscart_apai_category_redirect($pdo, 'apai-oil');
	$GLOBALS['CAPE_RESOLVE'] = array('product' => '');
	$emptyProduct = epc_epartscart_apai_category_redirect($pdo, 'shop/apai_x');
	$GLOBALS['CAPE_RESOLVE'] = null;
	$miss = epc_epartscart_apai_category_redirect($pdo, '/apai-oil/');
	return array($inactive, $notApai, $noFile, $withProduct, $emptyProduct, $miss);
}

function cape_run_tree()
{
	cape_include();
	$pdo = cape_pdo();
	$tree = array(
		array('alias' => 'tires', 'data' => array(
			array('alias' => 'apai-summer', 'data' => array()),
			array('alias' => 'winter', 'data' => array()),
		)),
		array('alias' => 'apai_rims', 'data' => array()),
		array('alias' => 'batteries', 'data' => array()),
	);
	$GLOBALS['CAPE_ACTIVE'] = 0;
	$passthrough = epc_epartscart_filter_menu_tree($pdo, $tree);
	$GLOBALS['CAPE_ACTIVE'] = 1;
	$filtered = epc_epartscart_filter_menu_tree($pdo, $tree);
	$proj = static function (array $nodes): array {
		$out = array();
		foreach ($nodes as $n) {
			$kids = array();
			foreach ((array) ($n['data'] ?? array()) as $c) {
				$kids[] = (string) ($c['alias'] ?? '');
			}
			$out[] = array((string) ($n['alias'] ?? ''), $kids);
		}
		return $out;
	};
	return array($proj($passthrough), $proj($filtered));
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_cape_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	cape_patch($root . '/content/general_pages/epc_epartscart_storefront.php', $tmp . '/page.php');
	$GLOBALS['CAPE_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'cape_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1cape_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1cape_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
