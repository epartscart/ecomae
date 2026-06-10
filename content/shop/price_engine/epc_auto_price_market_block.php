<?php
/**
 * Storefront partial — Auto Price AI market price comparison block.
 */
defined('_ASTEXE_') or die('No access');

if (empty($product_id) || (int) $product_id <= 0) {
	return;
}

if (function_exists('epc_apai_storefront_hide_market_sourcing') && epc_apai_storefront_hide_market_sourcing()) {
	return;
}

if (!is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_storefront.php')) {
	return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_storefront.php';

$pdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$pdo instanceof PDO) {
	return;
}

epc_ape_ensure_schema($pdo);
$siteKey = epc_apai_resolve_storefront_site_key();
if ($siteKey === '') {
	return;
}

if (empty($GLOBALS['epc_apai_market_css'])) {
	$GLOBALS['epc_apai_market_css'] = true;
	echo '<link rel="stylesheet" href="/content/general_pages/epc_auto_price_engine_css.php?v=20260606ape3" />';
}

$ourPrice = isset($price) ? (float) $price : 0.0;
echo epc_apai_render_market_prices_block($pdo, $siteKey, (int) $product_id, $ourPrice);
