<?php
/**
 * Multi-vendor Excel/CSV → auto-create warehouses + price lists.
 *
 * POST:
 *   token=epartscart-deploy-2026
 *   key=<tech_key>
 *   price_file=<file>   CSV/TXT/XLS/XLSX
 *
 * Columns required: Article, Price, Vendor full name, Vendor short
 * Recommended: Brand, Name, Qty, Delivery
 *
 * Vendor full name → shop_storages.name (backend only)
 * Vendor short     → shop_storages.short_name (storefront warehouse label)
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token(false);

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();

$requestKey = trim((string) ($_POST['key'] ?? $_GET['key'] ?? ''));
if ($requestKey !== '') {
	$tech = (string) ($DP_Config->tech_key ?? '');
	if ($tech === '' || !hash_equals($tech, $requestKey)) {
		http_response_code(403);
		exit(json_encode(array('status' => false, 'message' => 'Invalid tech_key')));
	}
}

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$db->query('SET NAMES utf8');
} catch (Throwable $e) {
	exit(json_encode(array('status' => false, 'message' => 'DB connect failed')));
}

require_once __DIR__ . '/content/shop/docpart/epc_multivendor_price_ingest.php';

$action = strtolower(trim((string) ($_POST['action'] ?? $_GET['action'] ?? 'upload')));
if ($action === 'sample') {
	exit(json_encode(array(
		'status' => true,
		'filename' => 'epc-multivendor-sample.csv',
		'csv' => epc_multivendor_sample_csv(),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

$hasFile = !empty($_FILES['price_file']) && (int) $_FILES['price_file']['error'] === UPLOAD_ERR_OK;
if (!$hasFile) {
	exit(json_encode(array('status' => false, 'message' => 'price_file upload required')));
}

$origName = basename((string) $_FILES['price_file']['name']);
$ext = strtolower(pathinfo($origName, PATHINFO_EXTENSION));
if (!in_array($ext, array('csv', 'txt', 'xls', 'xlsx'), true)) {
	exit(json_encode(array('status' => false, 'message' => 'Unsupported file type')));
}

$tmpPath = sys_get_temp_dir() . '/epc_mv_up_' . getmypid() . '_' . time() . '.' . $ext;
if (!move_uploaded_file((string) $_FILES['price_file']['tmp_name'], $tmpPath)) {
	exit(json_encode(array('status' => false, 'message' => 'Could not store upload')));
}

$dataType = epc_multivendor_normalize_data_type((string) ($_POST['data_type'] ?? $_GET['data_type'] ?? 'inventory'));
$result = epc_multivendor_ingest_file($db, $tmpPath, $origName, $dataType);
$result['ingest_mode'] = 'api_upload';
$result['data_type_default'] = $dataType;
@unlink($tmpPath);

exit(json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
