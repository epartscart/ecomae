<?php
/**
 * Platform governance — surface health probes (JSON for Super CP).
 * GET https://www.ecomae.com/epc-platform-governance-health-api.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
set_time_limit(120);

define('_ASTEXE_', 1);

require_once __DIR__ . '/content/general_pages/epc_platform_governance.php';

function epc_pgh_probe(string $url, int $timeout = 16): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => $timeout, 'ignore_errors' => true, 'header' => "Accept-Encoding: gzip\r\n"),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$ct = '';
	foreach ((array) $http_response_header as $h) {
		if (stripos($h, 'content-type:') === 0) {
			$ct = trim(substr($h, 13));
		}
	}
	return array(
		'url' => $url,
		'http' => $code ?: null,
		'ms' => $ms,
		'ok' => $code >= 200 && $code < 400,
		'content_type' => $ct,
		'bytes' => is_string($body) ? strlen($body) : 0,
		'is_json' => stripos($ct, 'json') !== false || (is_string($body) && $body !== '' && $body[0] === '{'),
	);
}

$surfaces = array();

$surfaces[] = array_merge(
	array('surface' => 'owner_marketing', 'label' => 'Owner platform (ecomae.com)'),
	epc_pgh_probe('https://www.ecomae.com/')
);

$surfaces[] = array_merge(
	array('surface' => 'tenant_storefront_epartscart', 'label' => 'Tenant storefront (epartscart.com)'),
	epc_pgh_probe('https://www.epartscart.com/en/')
);

$surfaces[] = array_merge(
	array('surface' => 'super_cp', 'label' => 'Super CP (www.ecomae.com/cp/)'),
	epc_pgh_probe('https://www.ecomae.com/cp/')
);

$surfaces[] = array_merge(
	array('surface' => 'super_cp_host', 'label' => 'Super CP host (cp.ecomae.com)'),
	epc_pgh_probe('https://cp.ecomae.com/cp/')
);

$surfaces[] = array_merge(
	array('surface' => 'tenant_cp', 'label' => 'Tenant CP (epartscart.com/cp/)'),
	epc_pgh_probe('https://www.epartscart.com/cp/')
);

$surfaces[] = array_merge(
	array('surface' => 'public_api_root', 'label' => 'Public API root'),
	epc_pgh_probe('https://www.ecomae.com/api/')
);

$surfaces[] = array_merge(
	array('surface' => 'platform_status', 'label' => 'Platform status JSON'),
	epc_pgh_probe('https://www.ecomae.com/epc-platform-status.php')
);

$fta = epc_pgh_probe('https://tax.gov.ae/en/legislation.aspx', 20);
$surfaces[] = array_merge(
	array('surface' => 'fta_legislation', 'label' => 'FTA legislation.aspx'),
	$fta
);

$umapiUrl = 'https://www.epartscart.com/content/shop/docpart/ajax_get_article_list.php';
$umapi = epc_pgh_probe($umapiUrl);
$umapi['note'] = 'Catalog proxy — expect JSON when POST params valid; HEAD may 405';
$surfaces[] = array_merge(array('surface' => 'umapi_proxy_sample', 'label' => 'UMAPI/catalog proxy (sample)'), $umapi);

$demoSurfaces = array();
if (is_file(__DIR__ . '/content/general_pages/epc_portal_demo.php')) {
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
	$platformPdo = epc_portal_platform_pdo();
	if ($platformPdo instanceof PDO) {
		epc_portal_demo_ensure_schema($platformPdo);
		$st = $platformPdo->query(
			"SELECT t.`site_key`, t.`intro_json`, r.`status` AS req_status
			 FROM `epc_portal_tenants` t
			 LEFT JOIN `epc_portal_demo_requests` r ON r.`site_key` = t.`site_key`
			 WHERE t.`is_demo` = 1 AND t.`status` = 'live' AND t.`site_key` != ''
			 ORDER BY t.`id` DESC LIMIT 5"
		);
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$key = preg_replace('/[^a-z0-9_]/', '', (string) $row['site_key']);
			if ($key === '') {
				continue;
			}
			$intro = json_decode((string) ($row['intro_json'] ?? '{}'), true);
			$erpOnly = is_array($intro) && !empty($intro['demo_erp_only']);
			if ($erpOnly) {
				$cpUrl = 'https://www.ecomae.com/cp/demo/' . $key . '/';
				$probe = epc_pgh_probe($cpUrl);
				$probe['note'] = 'ERP-only demo — storefront 503 expected; probed demo CP instead';
				$probe['ok'] = ($probe['http'] ?? 0) >= 200 && ($probe['http'] ?? 0) < 400;
				$demoSurfaces[] = array_merge(
					array('surface' => 'demo_erp_only_cp', 'label' => 'Demo ERP-only CP: ' . $key, 'site_key' => $key),
					$probe
				);
				continue;
			}
			$url = 'https://www.ecomae.com/demo/' . $key . '/en/';
			$demoSurfaces[] = array_merge(
				array('surface' => 'demo_storefront', 'label' => 'Demo storefront: ' . $key, 'site_key' => $key),
				epc_pgh_probe($url)
			);
		}
	}
}
if ($demoSurfaces === array()) {
	$demoSurfaces[] = array(
		'surface' => 'demo_storefront',
		'label' => 'Demo storefronts',
		'url' => 'https://www.ecomae.com/demo/{site_key}/en/',
		'http' => null,
		'ok' => false,
		'note' => 'No active demos in platform DB (static demo_epartscart paths are invalid)',
	);
}
$surfaces = array_merge($surfaces, $demoSurfaces);

$erpOnly = epc_pgh_probe('https://www.ecomae.com/cp/shop/finance/erp');
$surfaces[] = array_merge(
	array('surface' => 'erp_hub', 'label' => 'ERP hub (Super CP)'),
	$erpOnly
);

$failures = 0;
foreach ($surfaces as $idx => $s) {
	if (!empty($s['ok'])) {
		continue;
	}
	if (($s['surface'] ?? '') === 'super_cp_host' && (int) ($s['http'] ?? 0) === 525) {
		$surfaces[$idx]['note'] = trim((string) ($s['note'] ?? '') . ' Canonical Super CP: https://www.ecomae.com/cp/');
		$surfaces[$idx]['advisory'] = true;
		continue;
	}
	$failures++;
}

$seeded = 0;
try {
	require_once __DIR__ . '/config.php';
	$cfg = new DP_Config();
	$pdo = epc_portal_platform_pdo();
	if (!$pdo instanceof PDO) {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	}
	$seeded = (int) $pdo->query('SELECT COUNT(*) FROM `epc_platform_governance_rules`')->fetchColumn();
} catch (Exception $e) {
	$seeded = 0;
}

echo json_encode(array(
	'time' => date('c'),
	'overall_ok' => $failures === 0,
	'failure_count' => $failures,
	'rules_in_db' => $seeded,
	'surfaces' => $surfaces,
	'protocol_checks' => array(
		'cp_login' => $surfaces[2]['ok'] ?? false,
		'fta_legislation' => $fta['ok'] ?? false,
		'umapi_json_hint' => !empty($umapi['is_json']) || (($umapi['http'] ?? 0) === 405),
	),
), JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
