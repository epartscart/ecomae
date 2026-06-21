<?php
/**
 * Visual page editor — runtime config JS (loaded from CP footer, outside .row).
 */
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
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
} catch (Throwable $e) {
	echo 'window.EPC_VPE={};';
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_visual_page_editor.php';

if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo 'window.EPC_VPE={};';
	exit;
}

$pdo = $db_link;
$allowed = epc_vpe_allowed_site_keys($pdo);
$siteKey = epc_vpe_normalize_site_key((string) ($_GET['site_key'] ?? ''));
if ($siteKey === '' || !in_array($siteKey, $allowed, true)) {
	$siteKey = $allowed[0] ?? 'platform';
}

$layout = epc_vpe_layout_load($pdo, $siteKey);
$backend = epc_scp_backend();
$pageUrl = '/' . $backend . '/control/portal/epc_visual_page_editor';
$ajaxUrl = '/' . $backend . '/content/control/portal/ajax_visual_page_editor.php';
$lib = epc_vpe_block_library();

echo 'window.EPC_VPE = ' . json_encode(array(
	'ajaxUrl' => $ajaxUrl,
	'pageUrl' => $pageUrl,
	'siteKey' => $siteKey,
	'blocks' => $layout['blocks'],
	'brand' => $layout['brand'],
	'blockLibrary' => $lib,
), JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP) . ';';
