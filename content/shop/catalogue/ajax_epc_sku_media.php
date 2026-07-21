<?php
/**
 * AJAX API for SKU photos & multi-type specifications (CP).
 */
header('Content-Type: application/json; charset=utf-8');

$root = isset($_SERVER['DOCUMENT_ROOT']) ? (string) $_SERVER['DOCUMENT_ROOT'] : dirname(__DIR__, 4);
if (!is_file($root . '/config.php') && is_file(dirname(__DIR__, 3) . '/config.php')) {
	$root = dirname(__DIR__, 3);
}
$_SERVER['DOCUMENT_ROOT'] = $root;

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once $root . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

// Tenant DB overlay (same host may map to different schemas).
$tenantFile = $root . '/config.tenant-host-db.php';
if (is_file($tenantFile)) {
	$epc_tenant_host_db = null;
	require $tenantFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password', 'host') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($DP_Config, $epcTk)) {
				$DP_Config->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}
if (is_file($root . '/content/general_pages/epc_portal.php')) {
	require_once $root . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		try {
			epc_portal_apply_config($DP_Config);
		} catch (Throwable $e) {
		}
		$GLOBALS['DP_Config'] = $DP_Config;
	}
}

try {
	$dbHost = trim((string) ($DP_Config->host ?? ''));
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		(string) $DP_Config->user,
		(string) $DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
} catch (Throwable $e) {
	echo json_encode(array('ok' => false, 'error' => 'No database'));
	exit;
}

require_once $root . '/content/users/dp_user.php';
require_once $root . '/content/shop/catalogue/epc_sku_media.php';

$out = array('ok' => false, 'error' => 'Unauthorized');
try {
	if (!class_exists('DP_User') || !method_exists('DP_User', 'getAdminId') || (int) DP_User::getAdminId() <= 0) {
		echo json_encode($out);
		exit;
	}
	$session = DP_User::getAdminSession();
	$csrf = is_array($session) ? (string) ($session['csrf_guard_key'] ?? '') : '';
	$posted = (string) ($_POST['csrf_guard_key'] ?? $_GET['csrf_guard_key'] ?? '');
	if ($csrf !== '' && !hash_equals($csrf, $posted)) {
		$out['error'] = 'CSRF mismatch';
		echo json_encode($out);
		exit;
	}

	/** @var PDO $db_link */
	global $db_link;
	if (!($db_link instanceof PDO)) {
		$out['error'] = 'No database';
		echo json_encode($out);
		exit;
	}

	epc_sku_media_ensure_schema($db_link);
	$action = (string) ($_POST['action'] ?? $_GET['action'] ?? '');

	switch ($action) {
		case 'list':
			$q = (string) ($_GET['q'] ?? $_POST['q'] ?? '');
			$out = array('ok' => true, 'items' => epc_sku_media_list_profiles($db_link, $q, 120));
			break;

		case 'get':
			$profileId = (int) ($_GET['profile_id'] ?? $_POST['profile_id'] ?? 0);
			$productId = (int) ($_GET['product_id'] ?? $_POST['product_id'] ?? 0);
			$brand = (string) ($_GET['brand'] ?? $_POST['brand'] ?? '');
			$article = (string) ($_GET['article'] ?? $_POST['article'] ?? '');
			$profile = epc_sku_media_find_profile($db_link, $profileId, $productId, $brand, $article);
			if (!is_array($profile)) {
				$out = array('ok' => true, 'payload' => null);
				break;
			}
			$out = array('ok' => true, 'payload' => epc_sku_media_full_payload($db_link, (int) $profile['id']));
			break;

		case 'save_profile':
			$profile = epc_sku_media_upsert_profile($db_link, array(
				'id' => (int) ($_POST['profile_id'] ?? 0),
				'product_id' => (int) ($_POST['product_id'] ?? 0),
				'brand' => (string) ($_POST['brand'] ?? ''),
				'article' => (string) ($_POST['article'] ?? ''),
				'title' => (string) ($_POST['title'] ?? ''),
				'subtitle' => (string) ($_POST['subtitle'] ?? ''),
				'status' => (string) ($_POST['status'] ?? 'active'),
			));
			$out = array('ok' => true, 'payload' => epc_sku_media_full_payload($db_link, (int) $profile['id']));
			break;

		case 'delete_profile':
			$id = (int) ($_POST['profile_id'] ?? 0);
			$out = array('ok' => epc_sku_media_delete_profile($db_link, $id));
			break;

		case 'upload_photo':
			$profileId = (int) ($_POST['profile_id'] ?? 0);
			if ($profileId <= 0) {
				$out['error'] = 'Save the SKU profile first';
				break;
			}
			$file = $_FILES['photo'] ?? null;
			if (!is_array($file)) {
				$out['error'] = 'No file';
				break;
			}
			$res = epc_sku_media_add_photo($db_link, $profileId, $file, array(
				'alt' => (string) ($_POST['alt'] ?? ''),
				'caption' => (string) ($_POST['caption'] ?? ''),
				'photo_type' => (string) ($_POST['photo_type'] ?? 'product'),
				'is_primary' => !empty($_POST['is_primary']),
			));
			if (empty($res['ok'])) {
				$out['error'] = (string) ($res['error'] ?? 'Upload failed');
				break;
			}
			$out = array('ok' => true, 'photo_id' => (int) $res['id'], 'payload' => epc_sku_media_full_payload($db_link, $profileId));
			break;

		case 'update_photo':
			$photoId = (int) ($_POST['photo_id'] ?? 0);
			$ok = epc_sku_media_update_photo($db_link, $photoId, array(
				'alt' => (string) ($_POST['alt'] ?? ''),
				'caption' => (string) ($_POST['caption'] ?? ''),
				'photo_type' => (string) ($_POST['photo_type'] ?? 'product'),
				'sort_order' => (int) ($_POST['sort_order'] ?? 0),
				'is_primary' => !empty($_POST['is_primary']),
			));
			$profileId = (int) ($_POST['profile_id'] ?? 0);
			$out = array('ok' => $ok, 'payload' => $profileId > 0 ? epc_sku_media_full_payload($db_link, $profileId) : null);
			break;

		case 'delete_photo':
			$photoId = (int) ($_POST['photo_id'] ?? 0);
			$profileId = (int) ($_POST['profile_id'] ?? 0);
			$ok = epc_sku_media_delete_photo($db_link, $photoId);
			$out = array('ok' => $ok, 'payload' => $profileId > 0 ? epc_sku_media_full_payload($db_link, $profileId) : null);
			break;

		case 'add_spec_group':
			$profileId = (int) ($_POST['profile_id'] ?? 0);
			$res = epc_sku_media_add_spec_group($db_link, $profileId, array(
				'name' => (string) ($_POST['name'] ?? ''),
				'code' => (string) ($_POST['code'] ?? ''),
				'icon' => (string) ($_POST['icon'] ?? 'fa-list'),
			));
			if (empty($res['ok'])) {
				$out['error'] = (string) ($res['error'] ?? 'Failed');
				break;
			}
			$out = array('ok' => true, 'group_id' => (int) $res['id'], 'payload' => epc_sku_media_full_payload($db_link, $profileId));
			break;

		case 'delete_spec_group':
			$groupId = (int) ($_POST['group_id'] ?? 0);
			$profileId = (int) ($_POST['profile_id'] ?? 0);
			$ok = epc_sku_media_delete_spec_group($db_link, $groupId);
			$out = array('ok' => $ok, 'payload' => $profileId > 0 ? epc_sku_media_full_payload($db_link, $profileId) : null);
			break;

		case 'add_spec_row':
			$groupId = (int) ($_POST['group_id'] ?? 0);
			$profileId = (int) ($_POST['profile_id'] ?? 0);
			$res = epc_sku_media_add_spec_row($db_link, $groupId, array(
				'label' => (string) ($_POST['label'] ?? ''),
				'value' => (string) ($_POST['value'] ?? ''),
				'value_type' => (string) ($_POST['value_type'] ?? 'text'),
				'unit' => (string) ($_POST['unit'] ?? ''),
			));
			if (empty($res['ok'])) {
				$out['error'] = (string) ($res['error'] ?? 'Failed');
				break;
			}
			$out = array('ok' => true, 'row_id' => (int) $res['id'], 'payload' => epc_sku_media_full_payload($db_link, $profileId));
			break;

		case 'update_spec_row':
			$rowId = (int) ($_POST['row_id'] ?? 0);
			$profileId = (int) ($_POST['profile_id'] ?? 0);
			$ok = epc_sku_media_update_spec_row($db_link, $rowId, array(
				'label' => (string) ($_POST['label'] ?? ''),
				'value' => (string) ($_POST['value'] ?? ''),
				'value_type' => (string) ($_POST['value_type'] ?? 'text'),
				'unit' => (string) ($_POST['unit'] ?? ''),
				'sort_order' => (int) ($_POST['sort_order'] ?? 0),
			));
			$out = array('ok' => $ok, 'payload' => $profileId > 0 ? epc_sku_media_full_payload($db_link, $profileId) : null);
			break;

		case 'delete_spec_row':
			$rowId = (int) ($_POST['row_id'] ?? 0);
			$profileId = (int) ($_POST['profile_id'] ?? 0);
			$ok = epc_sku_media_delete_spec_row($db_link, $rowId);
			$out = array('ok' => $ok, 'payload' => $profileId > 0 ? epc_sku_media_full_payload($db_link, $profileId) : null);
			break;

		case 'meta':
			$out = array(
				'ok' => true,
				'photo_types' => epc_sku_media_photo_types(),
				'value_types' => epc_sku_media_value_types(),
				'default_spec_types' => epc_sku_media_default_spec_types(),
			);
			break;

		default:
			$out['error'] = 'Unknown action';
			break;
	}
} catch (Throwable $e) {
	$out = array('ok' => false, 'error' => $e->getMessage());
}

echo json_encode($out, JSON_UNESCAPED_UNICODE);
