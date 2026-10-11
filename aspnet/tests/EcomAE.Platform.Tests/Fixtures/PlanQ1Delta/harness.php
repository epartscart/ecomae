<?php
// PHP 8.3 goldens for plan Q1-delta (ePartsCart home new arrivals). Storefront + leftover APE parse stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['DELTA_DBS'] = array();

function delta_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = str_replace(
		"require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';",
		'// leftover storefront injected',
		$code
	);
	file_put_contents($dest, $code);
}

function delta_pdo(string $suffix): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_delta_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['DELTA_DBS'][] = $name;
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	delta_schema($pdo);
	return $pdo;
}

function delta_drop(): void
{
	if (empty($GLOBALS['DELTA_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['DELTA_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['DELTA_DBS'] = array();
}

function delta_schema(PDO $pdo): void
{
	$pdo->exec('CREATE TABLE `shop_catalogue_categories` (
		`id` INT PRIMARY KEY,
		`url` VARCHAR(255) NULL
	) ENGINE=InnoDB');
	$pdo->exec('CREATE TABLE `shop_catalogue_products` (
		`id` INT PRIMARY KEY,
		`alias` VARCHAR(255) NULL,
		`caption` VARCHAR(255) NULL,
		`category_id` INT NULL,
		`published_flag` TINYINT NOT NULL DEFAULT 0
	) ENGINE=InnoDB');
	$pdo->exec('CREATE TABLE `epc_product_discovery_queue` (
		`id` INT AUTO_INCREMENT PRIMARY KEY,
		`product_id` INT NOT NULL,
		`site_key` VARCHAR(64) NOT NULL,
		`status` VARCHAR(32) NOT NULL,
		`title` VARCHAR(255) NULL,
		`updated_at` DATETIME NOT NULL
	) ENGINE=InnoDB');
	$pdo->exec('CREATE TABLE `shop_storages_data` (
		`id` INT AUTO_INCREMENT PRIMARY KEY,
		`product_id` INT NOT NULL,
		`price` DECIMAL(12,4) NOT NULL DEFAULT 0
	) ENGINE=InnoDB');
	$pdo->exec('CREATE TABLE `shop_products_images` (
		`id` INT AUTO_INCREMENT PRIMARY KEY,
		`product_id` INT NOT NULL,
		`file_name` VARCHAR(255) NOT NULL
	) ENGINE=InnoDB');
}

function delta_product(
	PDO $pdo,
	int $id,
	string $alias,
	?string $caption,
	?string $title,
	?int $categoryId,
	?string $categoryUrl,
	int $published,
	string $siteKey,
	string $status,
	string $updatedAt,
	array $prices = array(),
	array $images = array()
): void {
	if ($categoryId !== null && $categoryUrl !== null) {
		$ins = $pdo->prepare('INSERT IGNORE INTO `shop_catalogue_categories` (`id`, `url`) VALUES (?, ?)');
		$ins->execute(array($categoryId, $categoryUrl));
	}
	$ins = $pdo->prepare('INSERT INTO `shop_catalogue_products` (`id`, `alias`, `caption`, `category_id`, `published_flag`) VALUES (?, ?, ?, ?, ?)');
	$ins->execute(array($id, $alias, $caption, $categoryId, $published));
	$ins = $pdo->prepare('INSERT INTO `epc_product_discovery_queue` (`product_id`, `site_key`, `status`, `title`, `updated_at`) VALUES (?, ?, ?, ?, ?)');
	$ins->execute(array($id, $siteKey, $status, $title, $updatedAt));
	foreach ($prices as $price) {
		$ins = $pdo->prepare('INSERT INTO `shop_storages_data` (`product_id`, `price`) VALUES (?, ?)');
		$ins->execute(array($id, $price));
	}
	foreach ($images as $file) {
		$ins = $pdo->prepare('INSERT INTO `shop_products_images` (`product_id`, `file_name`) VALUES (?, ?)');
		$ins->execute(array($id, $file));
	}
}

function delta_stubs(bool $withParse): void
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
	if (!function_exists('epc_electronicae_product_card')) {
		function epc_electronicae_product_card($pdo, array $row, string $productUrlMode = 'alias'): array
		{
			$pid = (int) ($row['product_id'] ?? $row['id'] ?? 0);
			$catUrl = trim((string) ($row['category_url'] ?? ''), '/');
			$slug = ($productUrlMode === 'id') ? (string) $pid : (string) ($row['alias'] ?? '');
			$href = ($catUrl !== '' && $slug !== '') ? '/' . $catUrl . '/' . $slug : '/shop/products/product?id=' . $pid;
			$image = !empty($row['file_name']) ? (string) $row['file_name'] : '';
			$price = (float) ($row['price'] ?? 0);
			$displayName = (string) ($row['caption'] ?? $row['title'] ?? '');
			return array(
				'id' => $pid,
				'brand' => '',
				'name' => $displayName,
				'price' => $price,
				'was' => 0,
				'image' => $image,
				'href' => $href,
				'alt' => $displayName,
				'sku' => '',
			);
		}
	}
	if ($withParse && !function_exists('epc_apai_parse_product_chpu')) {
		function epc_apai_parse_product_chpu($alias)
		{
			if (isset($GLOBALS['DELTA_PARSE'][(string) $alias])) {
				return $GLOBALS['DELTA_PARSE'][(string) $alias];
			}
			$alias = (string) $alias;
			$pos = strpos($alias, '-');
			if ($pos === false) {
				return array('brand' => '', 'article' => '');
			}
			return array(
				'brand' => substr($alias, 0, $pos),
				'article' => substr($alias, $pos + 1),
			);
		}
	}
}

function delta_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	delta_stubs(!empty($GLOBALS['DELTA_PARSE_ON']));
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['DELTA_DOCROOT'];
	$db_link = $GLOBALS['db_link'] ?? null;
	$DP_Config = $GLOBALS['DP_Config'] ?? null;
	$multilang_params = $GLOBALS['DELTA_LANG'] ?? array();
	require $path;
}

function delta_render(mixed $pdo, array $lang, mixed $config): string
{
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['DP_Config'] = $config;
	$GLOBALS['DELTA_LANG'] = $lang;
	$tmp = sys_get_temp_dir() . '/ecomae_delta_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['DELTA_PAGE'], $tmp);
	ob_start();
	delta_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function delta_cfg(string $mode = 'alias'): stdClass
{
	$cfg = new stdClass();
	$cfg->product_url = $mode;
	return $cfg;
}

function delta_seed_live(PDO $pdo): void
{
	delta_product($pdo, 10, 'bosch-0986424407', 'Brake pad', 'Brake pad title', 1, 'brakes', 1, 'epartscart', 'imported', '2026-10-11 12:00:00', array(0, 1234.5), array('a.jpg', 'b.jpg'));
	delta_product($pdo, 11, 'mann-filter', 'Oil filter', 'Oil filter title', 2, 'filters', 1, 'epartscart', 'imported', '2026-10-11 11:00:00', array(25), array());
	delta_product($pdo, 12, 'gasket', 'Gasket', 'Gasket title', null, null, 1, 'epartscart', 'imported', '2026-10-11 10:00:00', array(), array());
	delta_product($pdo, 13, 'hose-line', null, 'Hose', 3, 'hoses', 1, 'epartscart', 'imported', '2026-10-11 09:00:00', array(8.2), array('/img/hose.png'));
	delta_product($pdo, 14, 'old-one', 'Old stock', 'Old', 1, 'brakes', 1, 'epartscart', 'imported', '2026-10-10 08:00:00', array(3), array());
	delta_product($pdo, 15, 'fifth-part', 'Fifth', 'Fifth title', 1, 'brakes', 1, 'epartscart', 'imported', '2026-10-10 07:00:00', array(4), array());
	delta_product($pdo, 16, 'sixth-part', 'Sixth', 'Sixth title', 1, 'brakes', 1, 'epartscart', 'imported', '2026-10-10 06:00:00', array(5), array());
	delta_product($pdo, 17, 'seventh-part', 'Seventh', 'Seventh title', 1, 'brakes', 1, 'epartscart', 'imported', '2026-10-10 05:00:00', array(6), array());
	delta_product($pdo, 18, 'eighth-out', 'Eighth out', 'Should drop', 1, 'brakes', 1, 'epartscart', 'imported', '2026-10-10 04:00:00', array(7), array());
	delta_product($pdo, 19, 'other-site', 'Other tenant', 'Other', 1, 'brakes', 1, 'acme', 'imported', '2026-10-12 12:00:00', array(99), array('x.jpg'));
	delta_product($pdo, 20, 'pending-one', 'Pending', 'Pending', 1, 'brakes', 1, 'epartscart', 'pending', '2026-10-12 13:00:00', array(88), array('p.jpg'));
	delta_product($pdo, 21, 'hidden-one', 'Hidden', 'Hidden', 1, 'brakes', 0, 'epartscart', 'imported', '2026-10-12 14:00:00', array(77), array('h.jpg'));
}

function delta_run_empty(): array
{
	$GLOBALS['DELTA_PARSE_ON'] = false;
	$none = delta_render(null, array('lang_href' => '/en'), delta_cfg());
	$pdo = delta_pdo('empty');
	$blank = delta_render($pdo, array(), delta_cfg());
	delta_product($pdo, 1, 'skip-unpub', 'Skip', 'Skip', 1, 'brakes', 0, 'epartscart', 'imported', '2026-10-11 12:00:00', array(10), array('s.jpg'));
	delta_product($pdo, 2, 'skip-status', 'Skip', 'Skip', 1, 'brakes', 1, 'epartscart', 'queued', '2026-10-11 12:00:00', array(10), array('s.jpg'));
	delta_product($pdo, 3, 'skip-name', '', '', 1, 'brakes', 1, 'epartscart', 'imported', '2026-10-11 12:00:00', array(10), array('s.jpg'));
	delta_product($pdo, 4, 'skip-other', 'Other', 'Other', 1, 'brakes', 1, 'beta', 'imported', '2026-10-11 12:00:00', array(10), array('s.jpg'));
	$skip = delta_render($pdo, array('lang_href' => '/en'), delta_cfg());
	return array($none, $blank, $skip);
}

function delta_run_cards(): array
{
	$GLOBALS['DELTA_PARSE_ON'] = true;
	$pdo = delta_pdo('cards');
	delta_seed_live($pdo);
	$live = delta_render($pdo, array('lang_href' => '/en'), delta_cfg('alias'));
	$idMode = delta_render($pdo, array('lang_href' => '/en'), delta_cfg('id'));
	$zeroLang = delta_render($pdo, array('lang_href' => ''), null);
	return array($live, $idMode, $zeroLang);
}

function delta_run_escape(): array
{
	$GLOBALS['DELTA_PARSE_ON'] = true;
	$GLOBALS['DELTA_PARSE'] = array(
		'a&b' => array('brand' => 'ac & "me"', 'article' => 'sku<"1"'),
	);
	$pdo = delta_pdo('escape');
	delta_product($pdo, 30, 'a&b', 'TV & "Audio"', 'TV title', 9, 'tv', 1, 'epartscart', 'imported', '2026-10-11 12:00:00', array(99.1), array('/img/a".jpg'));
	$html = delta_render($pdo, array('lang_href' => '/ar'), delta_cfg());
	return array($html);
}

function delta_run_tenant(): array
{
	$GLOBALS['DELTA_PARSE_ON'] = true;
	$acme = delta_pdo('acme');
	delta_product($acme, 40, 'acme-pad', 'Acme Pad', 'Acme Pad', 1, 'acme-brakes', 1, 'epartscart', 'imported', '2026-10-11 12:00:00', array(12.5), array('/img/acme-pad.jpg'));
	delta_product($acme, 41, 'wrong-host', 'Wrong Host', 'Wrong', 1, 'acme-brakes', 1, 'beta', 'imported', '2026-10-12 12:00:00', array(99), array());
	$acmeHtml = delta_render($acme, array('lang_href' => '/en'), delta_cfg());
	$beta = delta_pdo('beta');
	delta_product($beta, 50, 'beta-rotor', 'Beta Rotor', 'Beta Rotor', 2, 'beta-engine', 1, 'epartscart', 'imported', '2026-10-11 12:00:00', array(0), array());
	$betaHtml = delta_render($beta, array('lang_href' => '/ar'), delta_cfg());
	return array($acmeHtml, $betaHtml);
}

register_shutdown_function('delta_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_delta_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	delta_patch($root . '/content/general_pages/epc_epartscart_home_new_arrivals.php', $tmp . '/page.php');
	$GLOBALS['DELTA_PAGE'] = $tmp . '/page.php';
	$GLOBALS['DELTA_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'delta_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		delta_drop();
		exit(1);
	}
	delta_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1delta_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1delta_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
