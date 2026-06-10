<?php
/**
 * Token-authed smoke test — add example.ae custom source + run merged discovery.
 * GET /epc-apai-verify-custom-source.php?token=…&site_key=electronicae&taxonomy=cell-phones
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_country_sources.php';

$cfg = new DP_Config();
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'electronicae'))));
$taxonomy = preg_replace('/[^a-z0-9\-]/', '', strtolower(trim((string) ($_GET['taxonomy'] ?? 'cell-phones'))));

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_ape_ensure_schema($pdo);

	$addedId = epc_disc_source_save($pdo, $siteKey, array(
		'domain' => 'example.ae',
		'label' => 'Example Custom Shop',
		'source_type' => 'custom_website',
		'created_by_tenant' => 1,
		'enabled' => 1,
		'priority' => 50,
	));

	$all = array_map('epc_disc_source_format_row', epc_disc_sources_list($pdo, $siteKey, false));
	$custom = array_values(array_filter($all, function ($r) {
		return ($r['origin'] ?? '') === 'custom';
	}));

	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$node = epc_apai_tax_by_slug($pdo, $industryKey, $taxonomy);
	$nodeId = $node ? (int) $node['id'] : 0;
	$forSearch = epc_disc_sources_for_search($pdo, $siteKey, $nodeId, $taxonomy, true);
	$domains = array_column(epc_disc_sources_to_domain_list($forSearch), 'domain');

	$disc = epc_disc_run_for_taxonomy($pdo, $siteKey, $taxonomy, '');

	echo json_encode(array(
		'ok' => true,
		'site_key' => $siteKey,
		'custom_source_id' => $addedId,
		'custom_sources' => $custom,
		'has_example_ae' => in_array('example.ae', $domains, true),
		'merged_domains' => $domains,
		'discovery' => $disc,
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
