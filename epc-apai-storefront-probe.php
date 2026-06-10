<?php
/**
 * Render Auto Price AI market block for a product (deploy verify).
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_storefront.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'electronicae'))));
$productId = max(0, (int) ($_GET['product_id'] ?? 106));

$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	(string) $cfg->user,
	(string) $cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
epc_ape_ensure_schema($pdo);

header('Content-Type: text/html; charset=utf-8');
echo '<!DOCTYPE html><html><head><link rel="stylesheet" href="/content/general_pages/epc_auto_price_engine_css.php?v=20260606ape3" /></head><body>';
echo '<h1>Market block probe: ' . htmlspecialchars($siteKey) . ' #' . $productId . '</h1>';
echo epc_apai_render_market_prices_block($pdo, $siteKey, $productId, 199.0);
echo '</body></html>';
