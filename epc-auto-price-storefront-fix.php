<?php
/**
 * Auto Price AI — sync taxonomy categories + fix imported product storefront URLs.
 * GET /epc-auto-price-storefront-fix.php?token=…&site_key=electronicae&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
global $DP_Config;
$DP_Config = $cfg;

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'electronicae'))));
$apply = !empty($_GET['apply']);
$productIds = array();
foreach (explode(',', (string) ($_GET['product_ids'] ?? '106,107,108,100')) as $p) {
	$pid = (int) trim($p);
	if ($pid > 0) {
		$productIds[] = $pid;
	}
}

function epc_apai_fix_probe_url(string $url): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$flat = is_string($body) ? $body : '';
	return array(
		'code' => $code,
		'has_photo' => stripos($flat, 'auto_price/') !== false || stripos($flat, 'products_images') !== false,
		'is_404' => stripos($flat, '404 Page not found') !== false,
	);
}

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_ape_ensure_schema($pdo);

	$out = array(
		'ok' => true,
		'site_key' => $siteKey,
		'apply' => $apply,
		'product_ids' => $productIds,
	);

	if ($apply) {
		$out['fixup'] = epc_apai_fixup_imported_products($pdo, $siteKey, $productIds);
	}

	$verify = array();
	foreach ($productIds as $pid) {
		$url = epc_ape_catalogue_product_url($pdo, $pid);
		$verify[$pid] = array(
			'storefront_url' => $url,
			'http' => $url !== '' ? epc_apai_fix_probe_url($url) : array('code' => 0),
		);
	}
	$out['verify'] = $verify;

	echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
