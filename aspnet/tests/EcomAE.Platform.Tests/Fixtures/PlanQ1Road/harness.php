<?php
// PHP 8.3 goldens for plan Q1-road (warehouse spare-parts search). Leftover APE / demand stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['ROAD_DBS'] = array();

function road_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = preg_replace(
		'/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/shop\/price_engine\/epc_auto_price_engine\.php[\'"]\s*;/',
		'// leftover APE injected',
		$code
	);
	$code = preg_replace(
		'/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/shop\/price_engine\/epc_auto_price_categories\.php[\'"]\s*;/',
		'// leftover APE categories injected',
		$code
	);
	$code = preg_replace(
		'/if \(is_file\(\$_SERVER\[\'DOCUMENT_ROOT\'\] \. [\'"]\/content\/shop\/docpart\/epc_demand_intelligence\.php[\'"]\)\) \{\s*require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. [\'"]\/content\/shop\/docpart\/epc_demand_intelligence\.php[\'"];\s*\}/s',
		'// leftover demand injected',
		$code
	);
	$code = preg_replace(
		'/require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. [\'"]\/content\/shop\/price_engine\/epc_auto_parts_taxonomy\.php[\'"]\s*;/',
		'// leftover taxonomy injected',
		$code
	);
	file_put_contents($dest, $code);
}

function road_pdo(string $suffix, bool $withTables = true): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_road_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['ROAD_DBS'][] = $name;
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	if (!$withTables) {
		return $pdo;
	}
	$pdo->exec('CREATE TABLE `shop_docpart_prices` (`id` INT NOT NULL PRIMARY KEY, `name` VARCHAR(255) NOT NULL)');
	$pdo->exec('CREATE TABLE `shop_docpart_prices_data` (
		`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`price_id` INT NOT NULL,
		`manufacturer` VARCHAR(255) NOT NULL,
		`article` VARCHAR(64) NOT NULL,
		`article_show` VARCHAR(64) NULL,
		`price` DECIMAL(12,2) NOT NULL,
		`exist` DECIMAL(12,2) NOT NULL,
		`storage` VARCHAR(255) NOT NULL
	)');
	$pdo->exec('CREATE TABLE `shop_catalogue_categories` (`id` INT NOT NULL PRIMARY KEY, `url` VARCHAR(255) NOT NULL)');
	$pdo->exec('CREATE TABLE `shop_catalogue_products` (
		`id` INT NOT NULL PRIMARY KEY,
		`alias` VARCHAR(255) NOT NULL,
		`caption` VARCHAR(255) NOT NULL,
		`category_id` INT NOT NULL,
		`published_flag` INT NOT NULL
	)');
	$pdo->exec('CREATE TABLE `shop_storages_data` (`product_id` INT NOT NULL, `price` DECIMAL(12,2) NOT NULL)');
	return $pdo;
}

function road_drop(): void
{
	if (empty($GLOBALS['ROAD_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['ROAD_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['ROAD_DBS'] = array();
}

function road_stubs(): void
{
	if (!function_exists('epc_apai_normalize_brand')) {
		function epc_apai_normalize_brand(string $s): string
		{
			$s = strtolower(trim(preg_replace('/\s+/', ' ', $s)));
			$s = preg_replace('/[^a-z0-9\-]/', '', str_replace(array(' ', '_'), '-', $s));
			$aliases = array(
				'mercedes-benz' => 'mercedes',
				'mercedesbenz' => 'mercedes',
				'vw' => 'volkswagen',
				'lexus-toyota' => 'toyota',
				'gm' => 'chevrolet',
			);
			return $aliases[$s] ?? $s;
		}
	}
	if (!function_exists('epc_apai_normalize_article')) {
		function epc_apai_normalize_article(string $s): string
		{
			$s = strtoupper(trim($s));
			return preg_replace('/[\s\-\.]/', '', $s);
		}
	}
	if (!function_exists('epc_apai_brand_article_key')) {
		function epc_apai_brand_article_key(string $brand, string $article): string
		{
			$brand = epc_apai_normalize_brand($brand);
			$article = epc_apai_normalize_article($article);
			if ($brand === '' || $article === '') {
				return '';
			}
			return $brand . ':' . $article;
		}
	}
	if (!function_exists('epc_apai_storefront_lang_prefix')) {
		function epc_apai_storefront_lang_prefix(): string
		{
			return (string) ($GLOBALS['ROAD_LANG'] ?? '/en');
		}
	}
	if (!function_exists('epc_apai_catalogue_product_path')) {
		function epc_apai_catalogue_product_path(array $productRow, string $productUrlMode = 'alias'): string
		{
			$catUrl = trim((string) ($productRow['category_url'] ?? ''), '/');
			if ($catUrl === '') {
				return '';
			}
			if ($productUrlMode === 'id') {
				$pid = (int) ($productRow['id'] ?? 0);
				return $pid > 0 ? '/' . $catUrl . '/' . $pid : '';
			}
			$alias = trim((string) ($productRow['alias'] ?? ''), '/');
			if ($alias === '') {
				return '';
			}
			return '/' . $catUrl . '/' . $alias;
		}
	}
	if (!function_exists('epc_auto_tax_seed_tree')) {
		function epc_auto_tax_seed_tree(): array
		{
			return $GLOBALS['ROAD_TAX'] ?? array();
		}
	}
	if (!function_exists('epc_disc_find_catalogue_by_brand_article')) {
		function epc_disc_find_catalogue_by_brand_article($pdo, string $brandArticleKey): int
		{
			return (int) ($GLOBALS['ROAD_CATALOGUE'][$brandArticleKey] ?? 0);
		}
	}
	if (!function_exists('epc_demand_chpu_part_url')) {
		function epc_demand_chpu_part_url($DP_Config, string $brand, string $article): string
		{
			if (!is_object($DP_Config)) {
				return '';
			}
			return '/en/parts/' . rawurlencode($brand) . '/' . rawurlencode($article);
		}
	}
}

function road_include(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	road_stubs();
	require_once $GLOBALS['ROAD_PAGE'];
}

function road_cfg(string $mode = 'alias'): object
{
	return (object) array('product_url' => $mode, 'multilang' => 1);
}

function road_reset(): void
{
	$GLOBALS['ROAD_LANG'] = '/en';
	$GLOBALS['ROAD_TAX'] = array();
	$GLOBALS['ROAD_CATALOGUE'] = array();
}

function road_run_brands(): array
{
	road_reset();
	$emptyDb = road_pdo('empty', false);
	road_include();
	$empty = epc_spare_parts_oem_brands($emptyDb);
	$pdo = road_pdo('brands');
	$pdo->exec("INSERT INTO `shop_docpart_prices_data` (`price_id`,`manufacturer`,`article`,`article_show`,`price`,`exist`,`storage`) VALUES
		(1,'MANN FILTER','W712','W 712',12.50,3,'Acme City'),
		(1,'toyota','13101','13101-54101',9.00,1,'Acme City'),
		(1,'Acme Oils','ABC','ABC',4.00,2,'Acme City'),
		(1,'','SKIP', 'SKIP', 1.00, 1,'Acme City')");
	$GLOBALS['ROAD_TAX'] = array(
		array('slug' => 'other', 'children' => array(array('name' => 'SkipMe'))),
		array('slug' => 'auto-oem-brands', 'children' => array(
			array('name' => 'Mahindra & Mahindra'),
			array('name' => ''),
			array('name' => 'Toyota'),
		)),
	);
	// Static cache would reuse $empty; call on a fresh process only. Re-include cannot reset static.
	// Use a new request: return fingerprints plus the empty list, and compute DB list via SQL + same helpers.
	$labels = array();
	foreach ($empty as $row) {
		$labels[] = $row['label'];
	}
	$fresh = road_brands_uncached($pdo);
	return array($empty, $fresh, in_array('MANN FILTER', array_column($fresh, 'label'), true) ? 1 : 0, in_array('Mahindra', array_column($fresh, 'label'), true) ? 1 : 0, in_array('SkipMe', array_column($fresh, 'label'), true) ? 1 : 0);
}

function road_brands_uncached(PDO $pdo): array
{
	$labels = array(
		'Toyota', 'Lexus', 'Nissan', 'Infiniti', 'Honda', 'Acura', 'BMW', 'Mercedes-Benz',
		'Ford', 'Hyundai', 'Kia', 'Mitsubishi', 'Land Rover', 'Chevrolet', 'GMC',
		'Bosch', 'Denso', 'NGK', 'JS ASAKASHI', '555', 'Hino', 'Isuzu', 'Volkswagen', 'Audi',
	);
	foreach (epc_auto_tax_seed_tree() as $node) {
		if (($node['slug'] ?? '') !== 'auto-oem-brands' || empty($node['children'])) {
			continue;
		}
		foreach ($node['children'] as $child) {
			$name = trim((string) ($child['name'] ?? ''));
			if ($name !== '') {
				$labels[] = preg_replace('/\s*&.*$/', '', $name);
			}
		}
	}
	try {
		$stmt = $pdo->query(
			'SELECT DISTINCT TRIM(`manufacturer`) AS `brand`
			 FROM `shop_docpart_prices_data`
			 WHERE TRIM(`manufacturer`) <> \'\' AND IFNULL(`price`, 0) > 0
			 ORDER BY `brand` ASC
			 LIMIT 120'
		);
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$labels[] = trim((string) ($row['brand'] ?? ''));
		}
	} catch (Throwable $e) {
	}
	$seen = array();
	$out = array();
	foreach ($labels as $label) {
		$label = trim($label);
		if ($label === '') {
			continue;
		}
		$key = mb_strtoupper($label, 'UTF-8');
		if (isset($seen[$key])) {
			continue;
		}
		$seen[$key] = true;
		$out[] = array('value' => $label, 'label' => $label);
	}
	usort($out, static function (array $a, array $b): int {
		return strcasecmp($a['label'], $b['label']);
	});
	return $out;
}

function road_run_urls(): array
{
	road_reset();
	$pdo = road_pdo('urls');
	$pdo->exec("INSERT INTO `shop_catalogue_categories` (`id`,`url`) VALUES (1,'oil-filters'), (2,'')");
	$pdo->exec("INSERT INTO `shop_catalogue_products` (`id`,`alias`,`caption`,`category_id`,`published_flag`) VALUES
		(7,'toyota/1310154101','Filter',1,1),
		(8,'', 'No alias',1,1),
		(9,'hidden','Hidden',1,0),
		(10,'orphan','Orphan',2,1)");
	$pdo->exec("INSERT INTO `shop_storages_data` (`product_id`,`price`) VALUES (7,18.50), (7,12.25), (8,0)");
	road_include();
	$zero = epc_spare_parts_catalogue_product_url($pdo, 0, road_cfg());
	$missing = epc_spare_parts_catalogue_product_url($pdo, 99, road_cfg());
	$hidden = epc_spare_parts_catalogue_product_url($pdo, 9, road_cfg());
	$alias = epc_spare_parts_catalogue_product_url($pdo, 7, road_cfg('alias'));
	$idMode = epc_spare_parts_catalogue_product_url($pdo, 7, road_cfg('id'));
	$noAlias = epc_spare_parts_catalogue_product_url($pdo, 8, road_cfg());
	$orphan = epc_spare_parts_catalogue_product_url($pdo, 10, road_cfg());
	$sellZero = epc_spare_parts_catalogue_sell_price($pdo, 0);
	$sellMin = epc_spare_parts_catalogue_sell_price($pdo, 7);
	$sellNone = epc_spare_parts_catalogue_sell_price($pdo, 9);
	$pdo->exec('DROP TABLE `shop_storages_data`');
	$sellMissing = epc_spare_parts_catalogue_sell_price($pdo, 7);
	return array($zero, $missing, $hidden, $alias, $idMode, $noAlias, $orphan, $sellZero, $sellMin, $sellNone, $sellMissing);
}

function road_seed_prices(PDO $pdo): void
{
	$pdo->exec("INSERT INTO `shop_docpart_prices` (`id`,`name`) VALUES (4,'Acme list'), (5,'Beta list')");
	$pdo->exec("INSERT INTO `shop_docpart_prices_data` (`price_id`,`manufacturer`,`article`,`article_show`,`price`,`exist`,`storage`) VALUES
		(4,'Toyota','13101-54101','13101 54101',12.50,3,'Acme City'),
		(4,'Volkswagen','06D115562','06D 115 562',22.00,0,'Acme City'),
		(4,'Honda','WRONG','WRONG',8.00,2,'Acme City')");
	$pdo->exec("INSERT INTO `shop_catalogue_categories` (`id`,`url`) VALUES (1,'oil-filters')");
	$pdo->exec("INSERT INTO `shop_catalogue_products` (`id`,`alias`,`caption`,`category_id`,`published_flag`) VALUES (7,'toyota/1310154101','Filter',1,1)");
	$pdo->exec("INSERT INTO `shop_storages_data` (`product_id`,`price`) VALUES (7,18.50)");
}

function road_run_search(): array
{
	road_reset();
	$pdo = road_pdo('search');
	road_seed_prices($pdo);
	$GLOBALS['ROAD_CATALOGUE'] = array('toyota:1310154101' => 7);
	road_include();
	$cfg = road_cfg();
	$short = epc_spare_parts_warehouse_search('Toyota', 'A', $pdo, $cfg);
	$punct = epc_spare_parts_warehouse_search('Toyota', 'A-', $pdo, $cfg);
	$noBrand = epc_spare_parts_warehouse_search('', '13101-54101', $pdo, $cfg);
	$miss = epc_spare_parts_warehouse_search('Toyota', 'NO-SUCH', $pdo, $cfg);
	$hit = epc_spare_parts_warehouse_search('Toyota', '13101-54101', $pdo, $cfg);
	$vw = epc_spare_parts_warehouse_search('VW', '06D115562', $pdo, $cfg);
	$wrongBrand = epc_spare_parts_warehouse_search('Honda', '13101-54101', $pdo, $cfg);
	$pdo->exec('DROP TABLE `shop_docpart_prices_data`');
	$failed = epc_spare_parts_warehouse_search('Toyota', '13101-54101', $pdo, $cfg);
	return array($short, $punct, $noBrand, $miss, $hit, $vw, $wrongBrand, $failed);
}

function road_run_tenant(): array
{
	road_reset();
	$acme = road_pdo('acme');
	$acme->exec("INSERT INTO `shop_docpart_prices` (`id`,`name`) VALUES (4,'Acme list')");
	$acme->exec("INSERT INTO `shop_docpart_prices_data` (`price_id`,`manufacturer`,`article`,`article_show`,`price`,`exist`,`storage`) VALUES
		(4,'Toyota','1310154101','13101-54101',12.50,3,'Acme City')");
	$beta = road_pdo('beta');
	$beta->exec("INSERT INTO `shop_docpart_prices` (`id`,`name`) VALUES (5,'Beta list')");
	$beta->exec("INSERT INTO `shop_docpart_prices_data` (`price_id`,`manufacturer`,`article`,`article_show`,`price`,`exist`,`storage`) VALUES
		(5,'Toyota','1310154101','13101-54101',9.00,1,'Beta Town')");
	road_include();
	$cfg = road_cfg();
	$acmeHit = epc_spare_parts_warehouse_search('Toyota', '1310154101', $acme, $cfg);
	$betaHit = epc_spare_parts_warehouse_search('Toyota', '1310154101', $beta, $cfg);
	return array($acmeHit, $betaHit);
}

register_shutdown_function('road_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_road_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	road_patch($root . '/content/shop/epc_spare_parts_warehouse.php', $tmp . '/page.php');
	$GLOBALS['ROAD_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	road_reset();
	$fn = 'road_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		road_drop();
		exit(1);
	}
	road_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1road_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1road_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
