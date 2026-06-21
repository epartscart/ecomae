<?php
/**
 * Social Media Hub — JS config (nginx-safe /content/ path).
 * Loaded as external script from CP footer — must bootstrap _ASTEXE_ + DB (not die on direct request).
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
if ($docRoot === '') {
	echo 'window.EPC_SOCIAL_HUB={};';
	exit;
}

require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once $docRoot . '/content/general_pages/epc_portal.php';
require_once $docRoot . '/content/general_pages/epc_portal_db.php';
require_once $docRoot . '/content/general_pages/epc_portal_tenant.php';
epc_portal_apply_config($DP_Config);

$dbHost = trim((string) $DP_Config->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
try {
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
} catch (Throwable $e) {
	echo 'window.EPC_SOCIAL_HUB={};';
	exit;
}

require_once $docRoot . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo 'window.EPC_SOCIAL_HUB={};';
	exit;
}

require_once $docRoot . '/content/social_media/epc_social_media_helpers.php';

$pdo = epc_social_pdo($db_link instanceof PDO ? $db_link : null);
$siteKey = epc_social_resolve_site_key($pdo);
$brand = epc_social_brand_context($siteKey, $pdo);

$config = array(
	'ajaxUrl' => '/content/general_pages/ajax_epc_social_media.php',
	'csrfToken' => epc_social_csrf_token(),
	'siteKey' => $siteKey,
	'brandName' => (string) $brand['brand_name'],
	'industry' => (string) $brand['industry'],
	'country' => (string) $brand['country'],
	'website' => (string) $brand['website'],
	'domain' => (string) $brand['domain'],
);

echo 'window.EPC_SOCIAL_HUB = ' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_HEX_TAG | JSON_HEX_AMP) . ';';
