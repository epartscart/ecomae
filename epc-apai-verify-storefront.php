<?php
/**
 * Auto Price AI — storefront + catalogue verify (all tenants).
 * GET /epc-apai-verify-storefront.php?token=…&site_key=epartscart&apply_fixup=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php';
require_once __DIR__ . '/content/general_pages/epc_electronicae_storefront.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
global $DP_Config;
$DP_Config = $cfg;

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
$applyFixup = !empty($_GET['apply_fixup']);
$limit = max(1, min(20, (int) ($_GET['limit'] ?? 8)));

function epc_apai_vf_probe(string $url): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true),
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
		'ok' => $code >= 200 && $code < 400,
		'has_product' => stripos($flat, 'product_page') !== false || stripos($flat, 'div_product_img_big') !== false,
		'has_apai_block' => stripos($flat, 'epc-apai-market-prices') !== false,
		'has_brand_badge' => stripos($flat, 'epc-apai-part-identity') !== false,
		'is_404' => stripos($flat, '404 Page not found') !== false,
	);
}

try {
	$platformPdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$pdo = epc_ape_tenant_pdo($platformPdo, $siteKey);
	if (!$pdo instanceof PDO) {
		$pdo = $platformPdo;
	}
	epc_ape_ensure_schema($pdo);

	$out = array(
		'ok' => true,
		'site_key' => $siteKey,
		'industry' => epc_apai_resolve_industry($pdo, $siteKey),
		'apply_fixup' => $applyFixup,
		'db' => (string) $pdo->query('SELECT DATABASE()')->fetchColumn(),
	);

	$catSync = epc_apai_sync_categories($pdo, $siteKey);
	$out['category_sync'] = $catSync;

	if ($applyFixup) {
		$out['fixup'] = epc_apai_fixup_imported_products($pdo, $siteKey);
	}

	$importStmt = $pdo->prepare(
		'SELECT q.`product_id`, q.`title`, q.`brand_article_key`, scp.`alias`, scp.`published_flag`, scc.`url` AS `category_url`
		 FROM `epc_product_discovery_queue` q
		 INNER JOIN `shop_catalogue_products` scp ON scp.`id` = q.`product_id`
		 LEFT JOIN `shop_catalogue_categories` scc ON scc.`id` = scp.`category_id`
		 WHERE q.`site_key` = ? AND q.`status` = \'imported\' AND q.`product_id` > 0
		 ORDER BY q.`updated_at` DESC
		 LIMIT ' . (int) $limit
	);
	$importStmt->execute(array($siteKey));
	$products = array();
	while ($row = $importStmt->fetch(PDO::FETCH_ASSOC)) {
		$pid = (int) ($row['product_id'] ?? 0);
		$url = epc_ape_catalogue_product_url($pdo, $pid);
		$parsed = epc_apai_parse_product_chpu((string) ($row['alias'] ?? ''));
		$products[$pid] = array(
			'title' => (string) ($row['title'] ?? ''),
			'alias' => (string) ($row['alias'] ?? ''),
			'brand_article_key' => (string) ($row['brand_article_key'] ?? ''),
			'parsed_brand' => (string) ($parsed['brand'] ?? ''),
			'parsed_article' => (string) ($parsed['article'] ?? ''),
			'published' => (int) ($row['published_flag'] ?? 0),
			'category_url' => (string) ($row['category_url'] ?? ''),
			'storefront_url' => $url,
			'chpu_ok' => strpos((string) ($row['alias'] ?? ''), '/') !== false,
			'http' => $url !== '' ? epc_apai_vf_probe($url) : array('code' => 0),
		);
	}
	$out['imported_products'] = $products;

	$tiles = epc_electronicae_product_line_tiles($pdo, $siteKey, 6);
	$out['product_line_tiles'] = array_map(static function ($t) {
		return array(
			'name' => (string) ($t['name'] ?? ''),
			'href' => (string) ($t['href'] ?? ''),
			'product_count' => (int) ($t['product_count'] ?? 0),
		);
	}, $tiles);

	$hostMap = array(
		'epartscart' => 'https://www.epartscart.com',
		'electronicae' => 'https://www.electronicae.com',
	);
	$homeUrl = ($hostMap[$siteKey] ?? rtrim((string) $cfg->domain_path, '/')) . '/en/';
	$out['homepage_probe'] = epc_apai_vf_probe($homeUrl);
	$out['homepage_url'] = $homeUrl;

	echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
