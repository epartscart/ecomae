<?php
/**
 * Visual page editor — AJAX save/load.
 */
define('_ASTEXE_', 1);
header('Content-Type: application/json; charset=utf-8');

if (ob_get_level()) {
	ob_end_clean();
}

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
	exit(json_encode(array('status' => false, 'message' => 'Database connection failed')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_visual_page_editor.php';

if (!DP_User::isAdmin()) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Admin login required')));
}

$pdo = $db_link;
$action = (string) ($_POST['action'] ?? $_GET['action'] ?? '');
$siteKey = epc_vpe_normalize_site_key((string) ($_POST['site_key'] ?? $_GET['site_key'] ?? ''));
$pageKey = epc_vpe_normalize_page_key((string) ($_POST['page_key'] ?? $_GET['page_key'] ?? 'homepage'));
$allowed = epc_vpe_allowed_site_keys($pdo);
if ($siteKey === '' || !in_array($siteKey, $allowed, true)) {
	exit(json_encode(array('status' => false, 'message' => 'Invalid site')));
}

if ($action === 'load_layout') {
	$layout = epc_vpe_layout_load($pdo, $siteKey, $pageKey);
	exit(json_encode(array(
		'status' => true,
		'layout' => $layout,
		'preview_url' => epc_vpe_resolve_preview_url($pdo, $siteKey, $pageKey),
		'levels' => epc_vpe_frontend_levels(),
	)));
}

if ($action === 'save_layout') {
	$blocksRaw = (string) ($_POST['blocks_json'] ?? '[]');
	$brandRaw = (string) ($_POST['brand_json'] ?? '{}');
	$blocks = json_decode($blocksRaw, true);
	$brandIn = json_decode($brandRaw, true);
	if (!is_array($blocks)) {
		exit(json_encode(array('status' => false, 'message' => 'Invalid blocks JSON')));
	}
	$brand = epc_vpe_default_brand();
	if (is_array($brandIn)) {
		foreach (array('primary', 'accent', 'background', 'logo_url', 'tagline', 'footer_text', 'hero_headline', 'hero_subheadline') as $k) {
			if (array_key_exists($k, $brandIn)) {
				$brand[$k] = (string) $brandIn[$k];
			}
		}
	}
	$lib = epc_vpe_block_library();
	$sanitized = array();
	foreach ($blocks as $block) {
		if (!is_array($block) || empty($block['type'])) {
			continue;
		}
		$type = preg_replace('/[^a-z0-9_]/', '', (string) $block['type']);
		if (!isset($lib[$type])) {
			continue;
		}
		$props = is_array($block['props'] ?? null) ? $block['props'] : $lib[$type]['defaults'];
		$sanitized[] = array(
			'id' => preg_replace('/[^a-z0-9_]/', '', (string) ($block['id'] ?? ('blk_' . count($sanitized)))),
			'type' => $type,
			'props' => $props,
		);
	}
	$publish = !empty($_POST['publish']);
	$res = epc_vpe_layout_save($pdo, $siteKey, $sanitized, $brand, $publish, $pageKey);
	$level = epc_vpe_level_meta($pageKey);
	$msg = $publish
		? ('Published — ' . (string) ($level['label'] ?? $pageKey) . ' updated, cache cleared.')
		: ('Draft saved for ' . (string) ($level['label'] ?? $pageKey) . '.');
	exit(json_encode(array(
		'status' => true,
		'message' => $msg,
		'result' => $res,
		'layout' => epc_vpe_layout_load($pdo, $siteKey, $pageKey),
		'preview_url' => epc_vpe_resolve_preview_url($pdo, $siteKey, $pageKey),
	)));
}

exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
