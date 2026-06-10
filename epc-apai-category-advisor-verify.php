<?php
/**
 * Auto Price AI — category advisor verification probe.
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
require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$platformPdo = epc_portal_platform_pdo();
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'electronicae'))));

$row = null;
foreach (epc_portal_list_tenants($platformPdo) as $t) {
	if ((string) ($t['site_key'] ?? '') === $siteKey) {
		$row = $t;
		break;
	}
}
if (!$row) {
	exit(json_encode(array('ok' => false, 'message' => 'Tenant not found: ' . $siteKey)));
}

$cred = epc_portal_tenant_setup_credentials($row);
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . (string) $cred['db'] . ';charset=utf8',
	(string) ($cred['user'] ?: $cfg->user),
	(string) ($cred['pass'] ?: $cfg->password),
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
epc_ape_ensure_schema($pdo);
epc_apai_taxonomy_migrate_schema($pdo);

$industryKey = epc_apai_resolve_industry($pdo, $siteKey);

$samples = array(
	'iphone' => array(
		'title' => 'Apple iPhone 15 Pro Max 256GB Natural Titanium',
		'description' => 'Latest iPhone with A17 Pro chip, unlocked for UAE networks.',
		'specs_json' => json_encode(array('Storage' => '256GB', 'Color' => 'Natural Titanium')),
		'source_domain' => 'sharafdg.com',
	),
	'unknown_gadget' => array(
		'title' => 'Zephyr X9 Portable UV Sterilizer Wand 2024 Model',
		'description' => 'Handheld UV-C sterilizer for travel and home use.',
		'specs_json' => json_encode(array('Type' => 'UV sterilizer', 'Power' => 'USB-C')),
		'source_domain' => 'noon.com',
	),
);

$advisories = array();
foreach ($samples as $key => $sample) {
	$sample['industry_key'] = $industryKey;
	$advisories[$key] = epc_apai_advise_category($pdo, $siteKey, $sample);
}

$queueAdvisories = array();
$queueStmt = $pdo->prepare(
	'SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'suggested\' ORDER BY `id` DESC LIMIT 5'
);
$queueStmt->execute(array($siteKey));
foreach ($queueStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $qRow) {
	$queueAdvisories[] = array(
		'queue_id' => (int) ($qRow['id'] ?? 0),
		'title' => (string) ($qRow['title'] ?? ''),
		'advisory' => epc_apai_advise_category($pdo, $siteKey, $qRow),
	);
}

$catList = epc_apai_list_industry_categories($pdo, $siteKey, $industryKey);

$orphanCheck = array();
$orphanStmt = $pdo->prepare(
	'SELECT scp.`id`, scp.`caption`, scp.`category_id`, scc.`value` AS cat_name, scc.`alias` AS cat_alias
	 FROM `shop_catalogue_products` scp
	 LEFT JOIN `shop_catalogue_categories` scc ON scc.`id` = scp.`category_id`
	 WHERE scp.`id` IN (
		SELECT `product_id` FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'imported\' AND `product_id` > 0
	 )
	 ORDER BY scp.`id` DESC LIMIT 10'
);
$orphanStmt->execute(array($siteKey));
foreach ($orphanStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $p) {
	$orphanCheck[] = array(
		'product_id' => (int) ($p['id'] ?? 0),
		'title' => (string) ($p['caption'] ?? ''),
		'category_id' => (int) ($p['category_id'] ?? 0),
		'category_name' => (string) ($p['cat_name'] ?? ''),
		'category_alias' => (string) ($p['cat_alias'] ?? ''),
		'is_orphan_shiny' => ((int) ($p['category_id'] ?? 0) === 62),
	);
}

echo json_encode(array(
	'ok' => true,
	'site_key' => $siteKey,
	'industry_key' => $industryKey,
	'category_dropdown_count' => count($catList),
	'sample_advisories' => $advisories,
	'queue_advisories' => $queueAdvisories,
	'imported_category_check' => $orphanCheck,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
