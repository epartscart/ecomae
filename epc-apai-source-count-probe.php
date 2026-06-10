<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_country_sources.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
epc_disc_ensure_schema($pdo);
$industry = epc_apai_resolve_industry($pdo, $siteKey);
$country = epc_apai_tenant_country($siteKey, $pdo);
$pack = epc_apai_country_sources_for_tenant($pdo, $siteKey, $industry);
$added = epc_apai_install_country_sources($pdo, $siteKey);
$stmt = $pdo->prepare('SELECT `domain`, `label`, `auth_type` FROM `epc_discovery_sources` WHERE `site_key` = ? ORDER BY `priority`');
$stmt->execute(array($siteKey));
$db = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
echo json_encode(array(
	'ok' => true,
	'site_key' => $siteKey,
	'industry' => $industry,
	'country' => $country,
	'pack_count' => count($pack),
	'pack_domains' => array_column($pack, 'domain'),
	'db_count' => count($db),
	'newly_added' => $added,
	'db_domains' => array_column($db, 'domain'),
	'taxonomy_nodes' => epc_apai_tax_count($pdo),
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
