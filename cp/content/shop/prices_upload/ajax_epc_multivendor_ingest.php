<?php
/**
 * CP AJAX: multi-vendor Excel → auto warehouses + price lists.
 *
 * Actions (POST/GET):
 *   upload (default)
 *   sample — JSON {csv,filename} or raw CSV when download=1 / format=csv
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
set_time_limit(0);

try {
	require_once __DIR__ . '/epc_prices_ajax_init.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

	if (!DP_User::isAdmin()) {
		http_response_code(403);
		exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_multivendor_price_ingest.php';

	$action = strtolower(trim((string) ($_POST['action'] ?? $_GET['action'] ?? 'upload')));
	$wantFile = !empty($_GET['download']) || !empty($_POST['download'])
		|| strtolower(trim((string) ($_GET['format'] ?? $_POST['format'] ?? ''))) === 'csv';

	if ($action === 'sample') {
		$csv = epc_multivendor_sample_csv();
		if ($wantFile) {
			header_remove('Content-Type');
			header('Content-Type: text/csv; charset=utf-8');
			header('Content-Disposition: attachment; filename="epc-multivendor-sample.csv"');
			header('Cache-Control: no-store, no-cache, must-revalidate');
			exit($csv);
		}
		exit(json_encode(array(
			'status' => true,
			'filename' => 'epc-multivendor-sample.csv',
			'csv' => $csv,
		), JSON_UNESCAPED_UNICODE));
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_multivendor_min_price_acl.php';

	if ($action === 'min_price_acl_get') {
		$acl = epc_mv_min_price_acl_get($db_link);
		$groups = epc_mv_min_price_list_customer_groups($db_link);
		exit(json_encode(array(
			'status' => true,
			'acl' => $acl,
			'groups' => $groups,
		), JSON_UNESCAPED_UNICODE));
	}

	if ($action === 'min_price_acl_save') {
		$rawGroups = $_POST['group_ids'] ?? $_POST['group_ids_json'] ?? array();
		if (is_string($rawGroups)) {
			$decoded = json_decode($rawGroups, true);
			$rawGroups = is_array($decoded) ? $decoded : preg_split('/[\s,;]+/', $rawGroups);
		}
		$rawUsers = $_POST['user_ids'] ?? $_POST['user_ids_json'] ?? array();
		if (is_string($rawUsers)) {
			$decoded = json_decode($rawUsers, true);
			$rawUsers = is_array($decoded) ? $decoded : preg_split('/[\s,;]+/', $rawUsers);
		}
		$restrictRaw = $_POST['restrict'] ?? $_POST['restrict_min'] ?? '1';
		$acl = array(
			'restrict' => !in_array(strtolower(trim((string) $restrictRaw)), array('0', 'false', 'no', 'off'), true),
			'group_ids' => is_array($rawGroups) ? $rawGroups : array(),
			'user_ids' => is_array($rawUsers) ? $rawUsers : array(),
		);
		$adminId = 0;
		if (method_exists('DP_User', 'getAdminId')) {
			$adminId = (int) DP_User::getAdminId();
		}
		$ok = epc_mv_min_price_acl_save($db_link, $acl, $adminId);
		exit(json_encode(array(
			'status' => $ok,
			'message' => $ok ? 'Minimum price access saved' : 'Could not save minimum price access',
			'acl' => epc_mv_min_price_acl_get($db_link),
		), JSON_UNESCAPED_UNICODE));
	}

	$hasFile = !empty($_FILES['price_file']) && (int) $_FILES['price_file']['error'] === UPLOAD_ERR_OK;
	if (!$hasFile) {
		exit(json_encode(array('status' => false, 'message' => 'Choose an Excel/CSV file with multiple vendors')));
	}

	$origName = basename((string) $_FILES['price_file']['name']);
	$ext = strtolower(pathinfo($origName, PATHINFO_EXTENSION));
	if (!in_array($ext, array('csv', 'txt', 'xls', 'xlsx'), true)) {
		exit(json_encode(array('status' => false, 'message' => 'Use CSV, TXT, XLS or XLSX')));
	}

	$tmpDir = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . $DP_Config->tmp_dir_prices_upload;
	if (!is_dir($tmpDir)) {
		@mkdir($tmpDir, 0755, true);
	}
	$tmpPath = $tmpDir . '/multivendor_' . time() . '_' . mt_rand(1000, 9999) . '.' . $ext;
	if (!move_uploaded_file((string) $_FILES['price_file']['tmp_name'], $tmpPath)) {
		exit(json_encode(array('status' => false, 'message' => 'Could not store upload')));
	}

	$dataType = epc_multivendor_normalize_data_type((string) ($_POST['data_type'] ?? $_GET['data_type'] ?? 'inventory'));
	$result = epc_multivendor_ingest_file($db_link, $tmpPath, $origName, $dataType);
	$result['ingest_mode'] = 'upload';
	$result['data_type_default'] = $dataType;
	@unlink($tmpPath);

	exit(json_encode($result, JSON_UNESCAPED_UNICODE));
} catch (Throwable $e) {
	if (!headers_sent()) {
		header('Content-Type: application/json; charset=utf-8');
	}
	http_response_code(500);
	exit(json_encode(array(
		'status' => false,
		'message' => 'Sample/upload endpoint error',
	)));
}
