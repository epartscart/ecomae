<?php
/**
 * Marketing Broadcast — JS config bootstrap.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
if ($docRoot === '') {
	echo 'window.EPC_MB={};';
	exit;
}

require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once $docRoot . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

$backend = trim((string) $DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$shopName = 'Your shop';
$shopUrl = '/';
try {
	require_once $docRoot . '/content/shop/marketing/epc_marketing_broadcast_helpers.php';
	if (function_exists('epc_mb_shop_context')) {
		$shop = epc_mb_shop_context($DP_Config);
		$shopName = (string) ($shop['shop_name'] ?? $shopName);
		$shopUrl = (string) ($shop['shop_url'] ?? $shopUrl);
	}
} catch (Throwable $e) {
	// keep defaults
}

echo 'window.EPC_MB=' . json_encode(array(
	'ajaxUrl' => '/content/general_pages/ajax_epc_marketing_broadcast.php',
	'shopName' => $shopName,
	'shopUrl' => $shopUrl,
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
