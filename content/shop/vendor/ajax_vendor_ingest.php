<?php
/**
 * Frontend vendor CSV/Excel upload (scoped to logged-in vendor account).
 */
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');

@set_time_limit(300);

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/vendor/epc_vendor_access.php';

$DP_Config = new DP_Config();
$userId = (int) DP_User::getUserId();
if ($userId <= 0) {
	echo json_encode(array('status' => false, 'message' => 'Please sign in'));
	exit;
}

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password
	);
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'));
	exit;
}

$account = epc_vendor_get_account($db, $userId);
if (!$account || !epc_vendor_user_can_upload($db, $userId)) {
	echo json_encode(array('status' => false, 'message' => 'Vendor account not approved for upload'));
	exit;
}

$session = DP_User::getUserSession();
$csrf = (string) ($_POST['csrf_guard_key'] ?? '');
if ($session && !empty($session['csrf_guard_key']) && $csrf !== '' && !hash_equals((string) $session['csrf_guard_key'], $csrf)) {
	echo json_encode(array('status' => false, 'message' => 'Invalid security token — refresh and try again'));
	exit;
}

if (empty($_FILES['price_file']) || !is_uploaded_file($_FILES['price_file']['tmp_name'])) {
	echo json_encode(array('status' => false, 'message' => 'Choose a CSV or Excel file'));
	exit;
}

$file = $_FILES['price_file'];
if (!empty($file['error'])) {
	echo json_encode(array('status' => false, 'message' => 'Upload error code ' . (int) $file['error']));
	exit;
}
$maxBytes = 12 * 1024 * 1024;
if ((int) $file['size'] > $maxBytes) {
	echo json_encode(array('status' => false, 'message' => 'File too large (max 12 MB)'));
	exit;
}

$orig = (string) ($file['name'] ?? 'upload.csv');
$ext = strtolower(pathinfo($orig, PATHINFO_EXTENSION));
if (!in_array($ext, array('csv', 'xls', 'xlsx', 'txt'), true)) {
	echo json_encode(array('status' => false, 'message' => 'Allowed types: CSV, XLS, XLSX'));
	exit;
}

$dataType = epc_multivendor_normalize_data_type((string) ($_POST['data_type'] ?? 'inventory'), 'inventory');
$tmp = sys_get_temp_dir() . '/epc_vp_' . $userId . '_' . time() . '_' . mt_rand(1000, 9999) . '.' . $ext;
if (!@move_uploaded_file($file['tmp_name'], $tmp)) {
	echo json_encode(array('status' => false, 'message' => 'Could not store upload'));
	exit;
}

try {
	if ((int) ($account['storage_id'] ?? 0) <= 0) {
		epc_vendor_provision_warehouse($db, $account);
		$account = epc_vendor_get_account($db, $userId) ?: $account;
	}
	$result = epc_multivendor_ingest_for_vendor(
		$db,
		$tmp,
		$orig,
		(string) $account['vendor_full'],
		(string) $account['vendor_short'],
		$dataType,
		$userId
	);
	// Refresh storage_id if created during ingest.
	$fresh = epc_vendor_get_account($db, $userId);
	if ($fresh && (int) $fresh['storage_id'] <= 0 && !empty($result['vendors'][0]['storage_id'])) {
		$sid = (int) $result['vendors'][0]['storage_id'];
		$db->prepare('UPDATE `epc_vendor_accounts` SET `storage_id` = ?, `updated_at` = ? WHERE `id` = ?')
			->execute(array($sid, time(), (int) $fresh['id']));
	}
	echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
} catch (Throwable $e) {
	error_log('[vendor_portal] ' . $e->getMessage());
	echo json_encode(array('status' => false, 'message' => 'Import failed'));
} finally {
	@unlink($tmp);
}
