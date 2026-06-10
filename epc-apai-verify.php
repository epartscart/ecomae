<?php
/**
 * Auto Price AI — post-deploy verification probe.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_storefront.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$platformPdo = epc_portal_platform_pdo();

$out = array('tenants' => array(), 'storefront_checks' => array());

foreach (epc_portal_list_tenants($platformPdo) as $row) {
	if ((string) ($row['status'] ?? '') !== 'live') {
		continue;
	}
	$siteKey = (string) ($row['site_key'] ?? '');
	$cred = epc_portal_tenant_setup_credentials($row);
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . (string) $cred['db'] . ';charset=utf8',
			(string) ($cred['user'] ?: $cfg->user),
			(string) ($cred['pass'] ?: $cfg->password),
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		continue;
	}
	epc_ape_ensure_schema($pdo);
	$industry = epc_apai_resolve_industry($pdo, $siteKey);
	$imported = $pdo->prepare(
		'SELECT q.`product_id`, q.`title`, q.`specs_json`, scp.`alias`, scp.`category_id`
		 FROM `epc_product_discovery_queue` q
		 LEFT JOIN `shop_catalogue_products` scp ON scp.`id` = q.`product_id`
		 WHERE q.`site_key` = ? AND q.`status` = \'imported\' AND q.`product_id` > 0
		 ORDER BY q.`id` DESC LIMIT 3'
	);
	$imported->execute(array($siteKey));
	$products = $imported->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$compare = epc_apai_imported_compare_matrix($pdo, $siteKey);
	$out['tenants'][$siteKey] = array(
		'db' => (string) ($cred['db'] ?? ''),
		'industry' => $industry,
		'category_map_count' => epc_apai_category_count($pdo, $siteKey, $industry),
		'imported_count' => count($products),
		'compare_products' => count($compare),
		'spec_compare_sample' => array_slice($compare, 0, 1),
		'products' => array(),
	);

	$host = (string) ($row['hostname'] ?? '');
	foreach ($products as $p) {
		$pid = (int) ($p['product_id'] ?? 0);
		$url = epc_ape_catalogue_product_url($pdo, $pid);
		if ($host !== '' && strpos($url, 'http') !== 0) {
			$url = 'https://' . $host . $url;
		}
		$srcCount = (int) $pdo->prepare('SELECT COUNT(*) FROM `epc_product_source_prices` WHERE `site_key` = ? AND `product_id` = ?')
			->execute(array($siteKey, $pid)) ?: 0;
		$cntStmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_product_source_prices` WHERE `site_key` = ? AND `product_id` = ?');
		$cntStmt->execute(array($siteKey, $pid));
		$srcCount = (int) $cntStmt->fetchColumn();
		$out['tenants'][$siteKey]['products'][] = array(
			'product_id' => $pid,
			'title' => (string) ($p['title'] ?? ''),
			'category_id' => (int) ($p['category_id'] ?? 0),
			'specs' => json_decode((string) ($p['specs_json'] ?? ''), true),
			'source_prices' => $srcCount,
			'storefront_url' => $url,
		);
		if ($host !== '' && $pid > 0 && in_array($siteKey, array('electronicae', 'epartscart'), true)) {
			$html = @file_get_contents($url, false, stream_context_create(array(
				'http' => array('timeout' => 15, 'user_agent' => 'EPC-Verify/1.0'),
				'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
			)));
			$out['storefront_checks'][] = array(
				'site_key' => $siteKey,
				'url' => $url,
				'has_market_block' => is_string($html) && strpos($html, 'epc-apai-market-prices') !== false,
				'has_market_title' => is_string($html) && strpos($html, 'Market prices (UAE)') !== false,
			);
		}
	}
}

echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
