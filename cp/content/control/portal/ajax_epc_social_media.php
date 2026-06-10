<?php
/**
 * Social Media Hub — AJAX (generate caption, save draft).
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
if (!headers_sent()) {
	header('Content-Type: application/json; charset=utf-8');
}

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
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
	echo json_encode(array('ok' => false, 'message' => 'DB unavailable'));
	exit;
}

require_once $docRoot . '/content/users/dp_user.php';
require_once $docRoot . '/content/social_media/epc_social_media_helpers.php';

if (!DP_User::isAdmin()) {
	echo json_encode(array('ok' => false, 'message' => 'Admin required'));
	exit;
}

if (!epc_social_verify_csrf()) {
	echo json_encode(array('ok' => false, 'message' => 'CSRF failed'));
	exit;
}

global $db_link;
$pdo = epc_social_pdo($db_link instanceof PDO ? $db_link : null);
if (!$pdo instanceof PDO) {
	echo json_encode(array('ok' => false, 'message' => 'DB unavailable'));
	exit;
}

$siteKey = epc_social_resolve_site_key($pdo);
$brand = epc_social_brand_context($siteKey, $pdo);
$action = (string) ($_POST['action'] ?? '');

if ($action === 'generate_caption') {
	$platform = preg_replace('/[^a-z_]/', '', strtolower((string) ($_POST['platform'] ?? 'instagram')));
	$product = trim((string) ($_POST['product_line'] ?? ''));
	$result = epc_social_generate_caption($brand, $platform, $product);
	echo json_encode(array('ok' => true) + $result);
	exit;
}

if ($action === 'save_draft') {
	$result = epc_social_save_draft($pdo, $siteKey, $_POST);
	echo json_encode($result);
	exit;
}

echo json_encode(array('ok' => false, 'message' => 'Unknown action'));
