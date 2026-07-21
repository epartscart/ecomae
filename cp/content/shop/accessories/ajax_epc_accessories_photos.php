<?php
/**
 * CP AJAX: accessories listing photo upload / list / delete / set primary.
 */
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');

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
require_once $root . '/content/shop/docpart/epc_accessories_db.php';

$out = array('ok' => false, 'error' => 'Unauthorized');
try {
	if (!class_exists('DP_User') || !method_exists('DP_User', 'getAdminId') || (int) DP_User::getAdminId() <= 0) {
		echo json_encode($out);
		exit;
	}
	$session = DP_User::getAdminSession();
	$csrf = is_array($session) ? (string) ($session['csrf_guard_key'] ?? '') : '';
	$posted = (string) ($_POST['csrf_guard_key'] ?? $_GET['csrf_guard_key'] ?? '');
	if ($csrf !== '' && ($posted === '' || !hash_equals($csrf, $posted))) {
		echo json_encode(array('ok' => false, 'error' => 'CSRF mismatch'));
		exit;
	}

	/** @var PDO $db_link */
	global $db_link;
	epc_acc_ensure_schema($db_link);

	$action = (string) ($_POST['action'] ?? $_GET['action'] ?? '');
	$listingId = (int) ($_POST['listing_id'] ?? $_GET['listing_id'] ?? 0);

	switch ($action) {
		case 'list':
			if ($listingId <= 0) {
				$out = array('ok' => false, 'error' => 'listing_id required');
				break;
			}
			$out = array(
				'ok' => true,
				'listing_id' => $listingId,
				'photos' => epc_acc_photos_list($db_link, $listingId),
			);
			break;

		case 'upload':
			if ($listingId <= 0) {
				$out = array('ok' => false, 'error' => 'Save the listing first, then upload photos.');
				break;
			}
			$file = null;
			$filesBag = null;
			if (!empty($_FILES['photo']) && is_array($_FILES['photo'])) {
				$file = $_FILES['photo'];
			} elseif (!empty($_FILES['photos']) && is_array($_FILES['photos'])) {
				$filesBag = $_FILES['photos'];
			} elseif (!empty($_FILES['photos_']) && is_array($_FILES['photos_'])) {
				$filesBag = $_FILES['photos_'];
			}
			// FormData append('photos[]') may arrive as key "photos" or literally "photos[]".
			if ($filesBag === null) {
				foreach ($_FILES as $key => $bag) {
					if (!is_array($bag)) {
						continue;
					}
					if ($key === 'photos[]' || substr((string) $key, -2) === '[]') {
						$filesBag = $bag;
						break;
					}
				}
			}
			if ($filesBag !== null) {
				if (is_array($filesBag['name'] ?? null)) {
					$uploaded = array();
					$errors = array();
					$n = count($filesBag['name']);
					for ($i = 0; $i < $n; $i++) {
						$one = array(
							'name' => $filesBag['name'][$i] ?? '',
							'type' => $filesBag['type'][$i] ?? '',
							'tmp_name' => $filesBag['tmp_name'][$i] ?? '',
							'error' => $filesBag['error'][$i] ?? UPLOAD_ERR_NO_FILE,
							'size' => $filesBag['size'][$i] ?? 0,
						);
						if ((int) $one['error'] === UPLOAD_ERR_NO_FILE) {
							continue;
						}
						$res = epc_acc_photos_add($db_link, $listingId, $one, false);
						if (!empty($res['ok'])) {
							$uploaded[] = $res['photo'] ?? null;
						} else {
							$errors[] = (string) ($res['error'] ?? 'Upload failed');
						}
					}
					$out = array(
						'ok' => count($uploaded) > 0,
						'photos' => epc_acc_photos_list($db_link, $listingId),
						'uploaded' => array_values(array_filter($uploaded)),
						'errors' => $errors,
						'error' => count($uploaded) > 0 ? '' : (string) ($errors[0] ?? 'Upload failed'),
					);
					break;
				}
				$file = $filesBag;
			}
			if (!$file) {
				$out = array('ok' => false, 'error' => 'No file');
				break;
			}
			$asPrimary = !empty($_POST['as_primary']);
			$out = epc_acc_photos_add($db_link, $listingId, $file, $asPrimary);
			if (!empty($out['ok'])) {
				$out['photos'] = epc_acc_photos_list($db_link, $listingId);
			}
			break;

		case 'delete':
			$photoId = (int) ($_POST['photo_id'] ?? 0);
			$out = epc_acc_photos_delete($db_link, $listingId, $photoId);
			break;

		case 'set_primary':
			$photoId = (int) ($_POST['photo_id'] ?? 0);
			$out = epc_acc_photos_set_primary($db_link, $listingId, $photoId);
			break;

		default:
			$out = array('ok' => false, 'error' => 'Unknown action');
	}
} catch (Throwable $e) {
	$out = array('ok' => false, 'error' => 'Server error');
}

echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
