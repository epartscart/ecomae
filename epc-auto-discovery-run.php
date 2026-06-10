<?php
/**
 * Product discovery job runner — taxonomy/category crawl + .ae source fetch.
 * GET /epc-auto-discovery-run.php?token=…&site_key=electronicae&taxonomy=cell-phones&keyword=
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';
require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$host = function_exists('epc_portal_host') ? strtolower(epc_portal_host()) : '';
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
if ($siteKey === '') {
	if (strpos($host, 'electronicae') !== false) {
		$siteKey = 'electronicae';
	} elseif (strpos($host, 'epartscart') !== false) {
		$siteKey = 'epartscart';
	} elseif (strpos($host, 'stylenlook') !== false) {
		$siteKey = 'stylenlook';
	} elseif (strpos($host, 'thejewellerytrend') !== false) {
		$siteKey = 'thejewellerytrend';
	} elseif (strpos($host, 'taxofinca') !== false) {
		$siteKey = 'taxofinca';
	} elseif (function_exists('epc_portal_site_key_from_hostname') && $host !== '') {
		require_once __DIR__ . '/content/general_pages/epc_portal_tenant_intro.php';
		$siteKey = epc_portal_site_key_from_hostname($host);
	} else {
		$siteKey = 'platform';
	}
}

$taxonomy = preg_replace('/[^a-z0-9\-]/', '', strtolower(trim((string) ($_GET['taxonomy'] ?? ''))));
$keyword = trim((string) ($_GET['keyword'] ?? ''));

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_ape_ensure_schema($pdo);
	$result = epc_disc_run_for_taxonomy($pdo, $siteKey, $taxonomy, $keyword);
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	echo json_encode(array(
		'ok' => !empty($result['ok']),
		'site_key' => $siteKey,
		'industry_key' => $industryKey,
		'taxonomy' => $taxonomy,
		'keyword' => $keyword,
		'host' => $host,
		'sources_used' => (int) ($result['sources_used'] ?? 0),
		'source_domains' => (array) ($result['source_domains'] ?? array()),
		'result' => $result,
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
