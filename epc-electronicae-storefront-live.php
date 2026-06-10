<?php
/**
 * Electronicae storefront deploy + verify (product lines, categories, homepage).
 * GET /epc-electronicae-storefront-live.php?token=epartscart-deploy-2026&apply=1
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
require_once __DIR__ . '/content/general_pages/epc_electronicae_storefront.php';

$apply = !empty($_GET['apply']);
$siteKey = 'electronicae';

function epc_el_probe(string $url): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
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
		'has_product_lines' => stripos($flat, 'epc-el-product-lines') !== false,
		'has_unsplash' => stripos($flat, 'images.unsplash.com') !== false,
		'has_auto_price_img' => stripos($flat, 'auto_price/') !== false,
		'has_apai_root' => stripos($flat, 'apai-root-electronics') !== false,
		'has_tires' => stripos($flat, '>Tires<') !== false || stripos($flat, 'tires.svg') !== false,
	);
}

try {
	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$platformPdo = epc_portal_platform_pdo();
	$pdo = null;
	foreach (epc_portal_list_tenants($platformPdo) as $t) {
		if ((string) ($t['site_key'] ?? '') !== $siteKey) {
			continue;
		}
		$cred = epc_portal_tenant_setup_credentials($t);
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . (string) $cred['db'] . ';charset=utf8',
			(string) ($cred['user'] ?: $cfg->user),
			(string) ($cred['pass'] ?: $cfg->password),
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		break;
	}
	if (!$pdo) {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			(string) $cfg->user,
			(string) $cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	}
	epc_ape_ensure_schema($pdo);

	$out = array(
		'ok' => true,
		'site_key' => $siteKey,
		'apply' => $apply,
		'before' => epc_el_probe('https://www.electronicae.com/en/'),
	);

	if ($apply) {
		$out['category_sync'] = epc_apai_sync_categories($pdo, $siteKey);
		$out['fixup'] = epc_apai_fixup_imported_products($pdo, $siteKey, array(106, 107, 108, 100));
	}

	$tiles = epc_electronicae_product_line_tiles($pdo, $siteKey, 8);
	$out['product_lines'] = array_map(function ($t) {
		return array(
			'name' => $t['name'] ?? '',
			'href' => $t['href'] ?? '',
			'has_image' => !empty($t['image']),
			'product_count' => (int) ($t['product_count'] ?? 0),
		);
	}, $tiles);

	$sections = epc_electronicae_home_product_sections($pdo, $siteKey, 2, 3);
	$out['home_sections'] = count($sections);
	$out['after'] = epc_el_probe('https://www.electronicae.com/en/');

	echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
