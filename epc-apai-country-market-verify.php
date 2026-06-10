<?php
/**
 * Verify tenant country resolution + buy sources + sell marketplaces.
 * GET /epc-apai-country-market-verify.php?token=…&site_key=electronicae
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
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_country_sources.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_marketplace_channels.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'electronicae'))));
$runInstall = !empty($_GET['install']) && (string) $_GET['install'] === '1';

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'Platform registry unavailable')));
}
epc_portal_db_ensure($platformPdo);

$row = null;
foreach (epc_portal_list_tenants($platformPdo) as $t) {
	if ((string) ($t['site_key'] ?? '') === $siteKey) {
		$row = $t;
		break;
	}
}
if (!$row) {
	http_response_code(404);
	exit(json_encode(array('ok' => false, 'error' => 'tenant_not_found', 'site_key' => $siteKey)));
}

$pdo = epc_auto_price_setup_connect(array(
	'db' => (string) ($row['db_name'] ?? ''),
	'user' => (string) ($row['db_user'] ?? ''),
	'pass' => (string) ($row['db_password'] ?? ''),
), $cfg);
if (!$pdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'db_connect_failed')));
}

epc_ape_ensure_schema($pdo);
epc_disc_ensure_schema($pdo);

$country = epc_apai_tenant_country($siteKey, $pdo);
$meta = epc_apai_country_meta($country);
$industry = epc_apai_resolve_industry($pdo, $siteKey);
$buyPack = epc_apai_country_sources_for_tenant($pdo, $siteKey, $industry);
$sellPack = epc_apai_sell_marketplaces_for_country($country);
$channels = epc_apai_marketplace_channels_for_tenant($pdo, $siteKey);

$installed = null;
if ($runInstall) {
	$installed = array(
		'sources_added' => epc_apai_install_country_sources($pdo, $siteKey),
		'marketplaces_added' => epc_apai_install_sell_marketplaces($pdo, $siteKey),
	);
}

$discCount = 0;
$st = $pdo->prepare('SELECT COUNT(*) FROM `epc_discovery_sources` WHERE `site_key` = ? AND `enabled` = 1');
$st->execute(array($siteKey));
$discCount = (int) $st->fetchColumn();

echo json_encode(array(
	'ok' => true,
	'site_key' => $siteKey,
	'country_code' => $country,
	'country_label' => $meta['label'],
	'industry_key' => $industry,
	'buy_source_pack_count' => count($buyPack),
	'buy_source_sample' => array_slice(array_column($buyPack, 'domain'), 0, 8),
	'sell_marketplace_count' => count($sellPack),
	'sell_marketplaces' => array_column($sellPack, 'domain'),
	'channels_sell' => (array) ($channels['sell_domains'] ?? array()),
	'enabled_discovery_sources' => $discCount,
	'install' => $installed,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
