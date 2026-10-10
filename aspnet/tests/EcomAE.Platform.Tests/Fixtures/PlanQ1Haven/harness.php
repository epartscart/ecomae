<?php
// PHP 8.3 goldens for plan Q1-haven (electronicae storefront helper). Leftover portal + APE stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function haven_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace('/require_once\s+__DIR__\s*\.\s*[\'"]\/epc_portal\.php[\'"]\s*;/', '// leftover portal injected', $code);
	$code = preg_replace(
		'/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/shop\/price_engine\/[^\'"]+\.php[\'"]\s*;/',
		'// leftover APE injected',
		$code
	);
	$code = preg_replace(
		'/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_perf_cache\.php[\'"]\s*;/',
		'// leftover perf cache injected',
		$code
	);
	file_put_contents($dest, $code);
}

function haven_stubs(): void
{
	if (!function_exists('epc_portal_electronics_retail_enabled')) {
		function epc_portal_electronics_retail_enabled(): bool
		{
			return !empty($GLOBALS['HAVEN_ACTIVE']);
		}
	}
	if (!function_exists('epc_apai_resolve_storefront_site_key')) {
		function epc_apai_resolve_storefront_site_key(): string
		{
			return (string) ($GLOBALS['HAVEN_SITE'] ?? '');
		}
	}
	if (!function_exists('epc_apai_storefront_lang_prefix')) {
		function epc_apai_storefront_lang_prefix(): string
		{
			return (string) ($GLOBALS['HAVEN_LANG'] ?? '/en');
		}
	}
	if (!function_exists('epc_apai_resolve_industry')) {
		function epc_apai_resolve_industry($pdo, $siteKey): string
		{
			return (string) ($GLOBALS['HAVEN_INDUSTRY'] ?? 'electronics');
		}
	}
	if (!function_exists('epc_apai_category_slug')) {
		function epc_apai_category_slug($industryKey, $suffix): string
		{
			return (string) ($GLOBALS['HAVEN_ROOT_ALIAS'] ?? ('apai-' . $industryKey . '-root'));
		}
	}
	if (!function_exists('epc_apai_category_for_taxonomy')) {
		function epc_apai_category_for_taxonomy($pdo, $siteKey, $nodeId): int
		{
			$map = (array) ($GLOBALS['HAVEN_TAX_CAT'] ?? array());
			return (int) ($map[(string) $nodeId] ?? 0);
		}
	}
	if (!function_exists('epc_apai_tax_flat_for_industry')) {
		function epc_apai_tax_flat_for_industry($pdo, $industryKey): array
		{
			return (array) ($GLOBALS['HAVEN_FLAT'] ?? array());
		}
	}
	if (!function_exists('epc_apai_tax_descendant_map')) {
		function epc_apai_tax_descendant_map($flat): array
		{
			return (array) ($GLOBALS['HAVEN_DESC'] ?? array());
		}
	}
	if (!function_exists('epc_apai_sync_categories')) {
		function epc_apai_sync_categories($pdo, $siteKey): void
		{
			$GLOBALS['HAVEN_SYNCED'] = ($GLOBALS['HAVEN_SYNCED'] ?? 0) + 1;
		}
	}
	if (!function_exists('epc_apai_product_line_rankings')) {
		function epc_apai_product_line_rankings($pdo, $siteKey): array
		{
			return array('rankings' => (array) ($GLOBALS['HAVEN_RANKINGS'] ?? array()));
		}
	}
	if (!function_exists('epc_apai_catalogue_product_path')) {
		function epc_apai_catalogue_product_path($row, $mode): string
		{
			return (string) ($GLOBALS['HAVEN_PRODUCT_PATH'] ?? '');
		}
	}
	if (!function_exists('epc_product_image_url')) {
		function epc_product_image_url($path): string
		{
			return '/img/' . ltrim((string) $path, '/');
		}
	}
	if (!function_exists('epc_perf_cache_remember')) {
		function epc_perf_cache_remember($key, $ttl, $fn)
		{
			$GLOBALS['HAVEN_CACHE_KEY'] = $key;
			return $fn();
		}
	}
}

function haven_include(): void
{
	haven_stubs();
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	include_once $GLOBALS['HAVEN_PAGE'];
}

function haven_admin(): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function haven_schema(PDO $admin, string $suffix): array
{
	$name = 'ecomae_cpw_haven_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$admin->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE `shop_catalogue_categories` (
		`id` INT NOT NULL PRIMARY KEY,
		`parent` INT NOT NULL DEFAULT 0,
		`alias` VARCHAR(120) NOT NULL DEFAULT \'\',
		`url` VARCHAR(200) NOT NULL DEFAULT \'\',
		`published_flag` TINYINT NOT NULL DEFAULT 1
	)');
	$pdo->exec('CREATE TABLE `shop_catalogue_products` (
		`id` INT NOT NULL PRIMARY KEY,
		`category_id` INT NOT NULL DEFAULT 0,
		`alias` VARCHAR(120) NOT NULL DEFAULT \'\',
		`caption` VARCHAR(200) NOT NULL DEFAULT \'\',
		`published_flag` TINYINT NOT NULL DEFAULT 1
	)');
	$pdo->exec('CREATE TABLE `shop_products_images` (
		`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`product_id` INT NOT NULL,
		`file_name` VARCHAR(200) NOT NULL DEFAULT \'\'
	)');
	$pdo->exec('CREATE TABLE `shop_storages_data` (
		`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`product_id` INT NOT NULL,
		`price` DECIMAL(12,2) NOT NULL DEFAULT 0
	)');
	$pdo->exec('CREATE TABLE `epc_product_discovery_queue` (
		`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`site_key` VARCHAR(64) NOT NULL,
		`status` VARCHAR(24) NOT NULL,
		`product_id` INT NOT NULL DEFAULT 0,
		`taxonomy_node_id` INT NOT NULL DEFAULT 0,
		`title` VARCHAR(200) NOT NULL DEFAULT \'\',
		`updated_at` INT NOT NULL DEFAULT 0
	)');
	return array($name, $pdo);
}

function haven_run_names()
{
	haven_include();
	$GLOBALS['HAVEN_ACTIVE'] = 0;
	$off = epc_electronicae_storefront_active() ? 1 : 0;
	$GLOBALS['HAVEN_ACTIVE'] = 1;
	$on = epc_electronicae_storefront_active() ? 1 : 0;
	$GLOBALS['HAVEN_SITE'] = '';
	$fallback = epc_electronicae_site_key(haven_admin());
	$GLOBALS['HAVEN_SITE'] = 'acme_el';
	$site = epc_electronicae_site_key(haven_admin());
	unset($GLOBALS['HAVEN_LANG']);
	$langDef = epc_electronicae_lang_prefix();
	$GLOBALS['HAVEN_LANG'] = '/ar/';
	$langAr = epc_electronicae_lang_prefix();
	$hrefRoot = epc_electronicae_href('/');
	$hrefPath = epc_electronicae_href('/phones');
	$hrefRel = epc_electronicae_href('phones');
	$hrefLang = epc_electronicae_href('/x', '/en/');
	$visPhone = epc_electronicae_line_visual('cell-phones-5g');
	$visUnknown = epc_electronicae_line_visual('obscure-gadget');
	$visLaptop = epc_electronicae_line_visual('computers-laptops');
	$emptyImg = epc_electronicae_normalize_image_url('');
	$absImg = epc_electronicae_normalize_image_url('https://cdn.test/a.png');
	$relImg = epc_electronicae_normalize_image_url('files/a.png');
	$GLOBALS['HAVEN_LANG'] = '/en';
	$emptyHtml = epc_electronicae_render_empty_category();
	return array(
		$off, $on, $fallback, $site, $langDef, $langAr,
		$hrefRoot, $hrefPath, $hrefRel, $hrefLang,
		epc_electronicae_preferred_line_slugs(),
		$visPhone, $visUnknown, $visLaptop,
		$emptyImg, $absImg, $relImg, $emptyHtml,
	);
}

function haven_run_tree()
{
	haven_include();
	$pdo = haven_admin();
	$GLOBALS['HAVEN_SITE'] = 'acme_el';
	$GLOBALS['HAVEN_ROOT_ALIAS'] = 'apai-electronics-root';
	$tree = array(
		array('alias' => 'tires', 'data' => array()),
		array('alias' => 'apai-electronics-root', 'data' => array(
			array('alias' => 'apai-phones', 'data' => array()),
			array('alias' => 'apai-tv', 'data' => array()),
		)),
		array('alias' => 'orphans', 'data' => array()),
	);
	$extracted = array_map(static function ($n) { return (string) ($n['alias'] ?? ''); }, epc_electronicae_filter_menu_tree($pdo, $tree, 'acme_el'));
	$emptyRoot = array(
		array('alias' => 'apai-electronics-root', 'data' => array()),
		array('alias' => 'other', 'data' => array()),
	);
	$emptyKids = array_map(static function ($n) { return (string) ($n['alias'] ?? ''); }, epc_electronicae_filter_menu_tree($pdo, $emptyRoot, 'acme_el'));
	$fallback = array(
		array('alias' => 'tires', 'data' => array()),
		array('alias' => 'apai-phones', 'data' => array()),
		array('alias' => 'apai-tv', 'data' => array()),
	);
	$apaiOnly = array_map(static function ($n) { return (string) ($n['alias'] ?? ''); }, epc_electronicae_filter_menu_tree($pdo, $fallback, 'acme_el'));
	$passthrough = array(
		array('alias' => 'tires', 'data' => array()),
		array('alias' => 'batteries', 'data' => array()),
	);
	$pass = array_map(static function ($n) { return (string) ($n['alias'] ?? ''); }, epc_electronicae_filter_menu_tree($pdo, $passthrough, 'acme_el'));
	return array($extracted, $emptyKids, $apaiOnly, $pass);
}

function haven_run_catalog()
{
	haven_include();
	$admin = haven_admin();
	list($schema, $pdo) = haven_schema($admin, 'c');
	try {
		$pdo->exec("INSERT INTO `shop_catalogue_categories` (`id`,`parent`,`alias`,`url`,`published_flag`) VALUES
			(10,0,'apai-electronics-root','electronics',1),
			(11,10,'apai-phones','electronics/phones',1),
			(12,11,'apai-android','electronics/phones/android',1),
			(13,10,'apai-empty','electronics/empty',1),
			(14,13,'apai-empty-child','electronics/empty/child',0)");
		$pdo->exec("INSERT INTO `shop_catalogue_products` (`id`,`category_id`,`alias`,`caption`,`published_flag`) VALUES
			(21,11,'pixel-8','Pixel 8',1),
			(22,12,'pixel-8-pro','Pixel 8 Pro',0)");
		$GLOBALS['HAVEN_ROOT_ALIAS'] = 'apai-electronics-root';
		$GLOBALS['HAVEN_TAX_CAT'] = array('7' => 11, '8' => 0);
		$root = epc_electronicae_root_category_id($pdo, 'acme_el');
		$urlOk = epc_electronicae_category_url($pdo, 'acme_el', 7);
		$urlMiss = epc_electronicae_category_url($pdo, 'acme_el', 8);
		$ids = epc_electronicae_category_subtree_ids($pdo, 10);
		$sqlIn = epc_electronicae_category_sql_in($pdo, 10);
		$hasPhones = epc_electronicae_category_has_products($pdo, 11) ? 1 : 0;
		$hasEmpty = epc_electronicae_category_has_products($pdo, 13) ? 1 : 0;
		$hasZero = epc_electronicae_category_has_products($pdo, 0) ? 1 : 0;
		$prefKids = epc_electronicae_category_prefers_products($pdo, 11, 1) ? 1 : 0;
		$prefNone = epc_electronicae_category_prefers_products($pdo, 13, 0) ? 1 : 0;
		$prefEmpty = epc_electronicae_category_prefers_products($pdo, 13, 2) ? 1 : 0;
		$allHref = epc_electronicae_all_lines_href($pdo, 'acme_el');
		return array($root, $urlOk, $urlMiss, $ids, $sqlIn, $hasPhones, $hasEmpty, $hasZero, $prefKids, $prefNone, $prefEmpty, $allHref);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function haven_run_tiles()
{
	haven_include();
	$admin = haven_admin();
	list($schema, $pdo) = haven_schema($admin, 't');
	try {
		$pdo->exec("INSERT INTO `shop_catalogue_categories` (`id`,`parent`,`alias`,`url`,`published_flag`) VALUES
			(10,0,'apai-electronics-root','electronics',1),
			(11,10,'apai-phones','electronics/phones',1)");
		$pdo->exec("INSERT INTO `shop_catalogue_products` (`id`,`category_id`,`alias`,`caption`,`published_flag`) VALUES
			(21,11,'pixel-8','Pixel 8',1)");
		$pdo->exec("INSERT INTO `shop_products_images` (`product_id`,`file_name`) VALUES (21,'pixel.png')");
		$pdo->exec("INSERT INTO `shop_storages_data` (`product_id`,`price`) VALUES (21,1299.00),(21,0)");
		$pdo->exec("INSERT INTO `epc_product_discovery_queue` (`site_key`,`status`,`product_id`,`taxonomy_node_id`,`title`,`updated_at`) VALUES
			('acme_el','imported',21,7,'Pixel 8',50)");
		$GLOBALS['HAVEN_SITE'] = 'acme_el';
		$GLOBALS['HAVEN_ROOT_ALIAS'] = 'apai-electronics-root';
		$GLOBALS['HAVEN_TAX_CAT'] = array('7' => 11, '9' => 11);
		$GLOBALS['HAVEN_DESC'] = array(7 => array(7), 9 => array(9));
		$GLOBALS['HAVEN_RANKINGS'] = array(
			array('id' => 7, 'slug' => 'cell-phones-5g', 'name_en' => 'Phones', 'level' => 1, 'imported_count' => 2, 'trend' => 'up', 'score' => 9, 'preview_image' => ''),
			array('id' => 9, 'slug' => 'gaming-laptops', 'name_en' => '', 'level' => 1, 'imported_count' => 0, 'trend' => '', 'score' => 1, 'preview_image' => 'https://cdn.test/g.png'),
			array('id' => 8, 'slug' => 'cell-phones-cases', 'name_en' => 'Cases', 'level' => 2, 'imported_count' => 9, 'trend' => '', 'score' => 3),
			array('id' => 12, 'slug' => 'audio-kits', 'name_en' => 'Audio', 'level' => 1, 'imported_count' => 1, 'trend' => '', 'score' => 2),
		);
		$tiles = epc_electronicae_product_line_tiles($pdo, '', 12);
		$nav = epc_electronicae_mega_nav($pdo, 'acme_el');
		$hero = epc_electronicae_hero_slides($pdo, 'acme_el');
		$card = epc_electronicae_product_card($pdo, array(
			'id' => 21,
			'alias' => 'pixel-8',
			'caption' => 'Pixel 8',
			'category_url' => 'electronics/phones',
			'file_name' => 'pixel.png',
			'price' => '1299.5',
			'manufacturer' => '',
		), 'alias');
		$GLOBALS['HAVEN_PRODUCT_PATH'] = '/forced/path';
		$cardForced = epc_electronicae_product_card($pdo, array('id' => 21, 'caption' => 'Pixel 8', 'file_name' => ''), 'alias');
		$cfg = new stdClass();
		$cfg->product_url = 'alias';
		$GLOBALS['DP_Config'] = $cfg;
		$sections = epc_electronicae_home_product_sections($pdo, 'acme_el', 3, 6);
		return array(
			$GLOBALS['HAVEN_CACHE_KEY'] ?? '',
			$GLOBALS['HAVEN_SYNCED'] ?? 0,
			$tiles,
			$nav,
			$hero,
			$card,
			$cardForced,
			$sections,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_haven_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	haven_patch($root . '/content/general_pages/epc_electronicae_storefront.php', $tmp . '/page.php');
	$GLOBALS['HAVEN_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'haven_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1haven_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1haven_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
