<?php
/**
 * Auto Price AI — storefront visibility probe (deploy token).
 * GET /epc-auto-price-storefront-probe.php?token=…&site_key=electronicae&product_ids=106,107,108
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
if (is_file(__DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php')) {
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php';
}

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
global $DP_Config;
$DP_Config = $cfg;

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'electronicae'))));
$productIds = array();
foreach (explode(',', (string) ($_GET['product_ids'] ?? '106,107,108,100')) as $p) {
	$pid = (int) trim($p);
	if ($pid > 0) {
		$productIds[] = $pid;
	}
}
if (!$productIds) {
	$productIds = array(106, 107, 108, 100);
}

function epc_apai_probe_http(string $url): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 15, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$flat = is_string($body) ? $body : '';
	return array(
		'code' => $code,
		'has_product_page' => stripos($flat, 'product_page') !== false || stripos($flat, 'div_product_img_big') !== false,
		'has_img' => stripos($flat, 'auto_price/') !== false || stripos($flat, 'products_images') !== false,
		'snippet' => substr(preg_replace('/\s+/', ' ', strip_tags($flat)), 0, 120),
	);
}

function epc_apai_chpu_product_url(array $productRow, string $productUrlMode = 'alias'): string
{
	$catUrl = trim((string) ($productRow['category_url'] ?? ''), '/');
	$slug = ($productUrlMode === 'id')
		? (string) (int) ($productRow['id'] ?? 0)
		: (string) ($productRow['alias'] ?? '');
	if ($catUrl === '' || $slug === '') {
		return '';
	}
	return '/' . $catUrl . '/' . $slug;
}

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_ape_ensure_schema($pdo);

	$productUrlMode = (string) ($cfg->product_url ?? 'alias');
	$domain = rtrim((string) ($cfg->domain_path ?? ''), '/');
	$host = function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? '');

	$out = array(
		'ok' => true,
		'site_key' => $siteKey,
		'host' => $host,
		'db' => (string) $cfg->db,
		'product_url_mode' => $productUrlMode,
		'domain_path' => $domain,
		'products' => array(),
		'categories' => array(),
	);

	$placeholders = implode(',', array_fill(0, count($productIds), '?'));
	$stmt = $pdo->prepare(
		'SELECT scp.*, scc.`url` AS `category_url`, scc.`alias` AS `category_alias`, scc.`count` AS `category_count`,
		        scc.`published_flag` AS `category_published`, scc.`parent` AS `category_parent`, scc.`value` AS `category_name`
		 FROM `shop_catalogue_products` scp
		 LEFT JOIN `shop_catalogue_categories` scc ON scc.`id` = scp.`category_id`
		 WHERE scp.`id` IN (' . $placeholders . ')'
	);
	$stmt->execute($productIds);
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$pid = (int) $row['id'];
		$imgStmt = $pdo->prepare('SELECT `file_name` FROM `shop_products_images` WHERE `product_id` = ?');
		$imgStmt->execute(array($pid));
		$images = $imgStmt->fetchAll(PDO::FETCH_COLUMN) ?: array();
		$priceStmt = $pdo->prepare('SELECT MIN(`price`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price` > 0');
		$priceStmt->execute(array($pid));
		$price = (float) $priceStmt->fetchColumn();

		$langPrefix = function_exists('epc_apai_storefront_lang_prefix') ? epc_apai_storefront_lang_prefix() : '';
		$legacyUrl = epc_ape_catalogue_product_url($pdo, $pid);
		$chpuPath = epc_apai_chpu_product_url($row, $productUrlMode);
		$chpuFull = $domain . $langPrefix . $chpuPath;

		$out['products'][$pid] = array(
			'id' => $pid,
			'caption' => (string) ($row['caption'] ?? ''),
			'alias' => (string) ($row['alias'] ?? ''),
			'published_flag' => (int) ($row['published_flag'] ?? 0),
			'category_id' => (int) ($row['category_id'] ?? 0),
			'category_url' => (string) ($row['category_url'] ?? ''),
			'category_count' => (int) ($row['category_count'] ?? 0),
			'category_published' => (int) ($row['category_published'] ?? 0),
			'category_parent' => (int) ($row['category_parent'] ?? 0),
			'price' => $price,
			'images' => $images,
			'legacy_storefront_url' => $legacyUrl,
			'chpu_path' => $chpuPath,
			'chpu_full_url' => $chpuFull,
			'http_legacy' => epc_apai_probe_http($legacyUrl),
			'http_chpu' => $chpuFull !== '' ? epc_apai_probe_http($chpuFull) : array('code' => 0),
		);
	}

	$catStmt = $pdo->query(
		'SELECT `id`, `parent`, `url`, `alias`, `count`, `published_flag`, `level`, `value`
		 FROM `shop_catalogue_categories`
		 WHERE `alias` LIKE \'apai-%\' OR `id` = 62
		 ORDER BY `level`, `order` LIMIT 40'
	);
	$out['categories'] = $catStmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	if (!empty($_GET['sync_categories'])) {
		$sync = epc_apai_sync_categories($pdo, $siteKey);
		$out['category_sync'] = $sync;
	}

	echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
