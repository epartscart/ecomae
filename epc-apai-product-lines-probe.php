<?php
/**
 * Product line rankings — deploy verification probe.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_product_line_rankings.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'electronicae'))));
if ($siteKey === '') {
	$siteKey = 'electronicae';
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$pdo instanceof PDO) {
	$cfg = new DP_Config();
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

epc_ape_ensure_schema($pdo);
$industry = epc_apai_resolve_industry($pdo, $siteKey);
$data = epc_apai_product_line_rankings($pdo, $siteKey, $industry);

$top5 = array();
foreach (array_slice($data['rankings'] ?? array(), 0, 5) as $line) {
	$top5[] = array(
		'rank' => (int) ($line['rank'] ?? 0),
		'name' => (string) ($line['name_en'] ?? ''),
		'slug' => (string) ($line['slug'] ?? ''),
		'score' => (int) ($line['score'] ?? 0),
		'source_coverage' => (int) ($line['source_coverage'] ?? 0),
		'suggested_count' => (int) ($line['suggested_count'] ?? 0),
		'trend' => (string) ($line['trend'] ?? ''),
		'sources' => array_column((array) ($line['source_domains'] ?? array()), 'label'),
		'price_range' => ($line['price_min'] ?? 0) > 0
			? round((float) $line['price_min']) . '-' . round((float) ($line['price_max'] ?? 0))
			: null,
	);
}

echo json_encode(array(
	'ok' => true,
	'site_key' => $siteKey,
	'industry_key' => $industry,
	'configured_sources' => (int) ($data['configured_sources'] ?? 0),
	'total_ranked' => count($data['rankings'] ?? array()),
	'top5' => $top5,
	'ui_url' => 'https://www.electronicae.com/cp/control/portal/epc_auto_price_engine?site_key=' . rawurlencode($siteKey) . '&tab=product_lines',
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
