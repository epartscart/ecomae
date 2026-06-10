<?php
/**
 * Diagnose epartscart PDO bootstrap (why storefront shows "No DB connect").
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
$_SERVER['SERVER_NAME'] = 'www.epartscart.com';

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();

$epcTenantDbFile = __DIR__ . '/config.tenant-db.php';
if (is_file($epcTenantDbFile)) {
	$epc_tenant_db = null;
	require $epcTenantDbFile;
	if (isset($epc_tenant_db) && is_array($epc_tenant_db)) {
		foreach (array('db', 'user', 'password') as $k) {
			if (!empty($epc_tenant_db[$k]) && property_exists($DP_Config, $k)) {
				$DP_Config->$k = $epc_tenant_db[$k];
			}
		}
	}
}

require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

echo "docroot=" . ($_SERVER['DOCUMENT_ROOT'] ?? '') . "\n";
echo "host_cfg=" . $DP_Config->host . " db=" . $DP_Config->db . " user=" . $DP_Config->user . " pass_len=" . strlen((string) $DP_Config->password) . "\n";
echo "tenant_host_db=" . (is_file(__DIR__ . '/config.tenant-host-db.php') ? 'yes' : 'no') . "\n";
echo "tenant_db=" . (is_file(__DIR__ . '/config.tenant-db.php') ? 'yes' : 'no') . "\n";
echo "config_local=" . (is_file(__DIR__ . '/config.local.php') ? 'yes' : 'no') . "\n";

foreach (array('localhost', '127.0.0.1') as $h) {
	try {
		$pdo = new PDO('mysql:host=' . $h . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
		$n = (int) $pdo->query('SELECT COUNT(*) FROM shop_catalogue_categories')->fetchColumn();
		echo "pdo_{$h}=OK categories={$n}\n";
	} catch (Throwable $e) {
		echo "pdo_{$h}=FAIL " . $e->getMessage() . "\n";
	}
}

if (is_file(__DIR__ . '/config.local.php')) {
	$epc_config_local = null;
	include __DIR__ . '/config.local.php';
	if (isset($epc_config_local['host'])) {
		echo 'config_local_host=' . (string) $epc_config_local['host'] . "\n";
	}
}

$ctx = stream_context_create(array(
	'http' => array('timeout' => 12, 'ignore_errors' => true, 'header' => "Host: www.epartscart.com\r\n"),
));
$body = @file_get_contents('http://127.0.0.1/en/', false, $ctx);
$code = 0;
if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
	$code = (int) $m[1];
}
$snippet = is_string($body) ? substr($body, 0, 120) : '';
echo "origin_en_http={$code} snippet=" . trim(preg_replace('/\s+/', ' ', $snippet)) . "\n";

$ctx2 = stream_context_create(array(
	'http' => array(
		'timeout' => 20,
		'ignore_errors' => true,
		'follow_location' => 1,
		'max_redirects' => 5,
		'header' => "Host: www.epartscart.com\r\n",
	),
));
$body2 = @file_get_contents('http://127.0.0.1/en/', false, $ctx2);
$code2 = 0;
if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
	$code2 = (int) $m[1];
}
$snippet2 = is_string($body2) ? substr($body2, 0, 160) : '';
$bad2 = is_string($body2) && stripos($body2, 'No DB connect') !== false;
echo 'origin_en_follow=' . $code2 . ($bad2 ? ' [no-db]' : ' [ok]') . ' snippet=' . trim(preg_replace('/\s+/', ' ', $snippet2)) . "\n";
