<?php
/**
 * CP AJAX: commerce sales/purchase/inventory Excel → S/P/L warehouse price lists.
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/epc_prices_ajax_init.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_commerce_price_ingest.php';

$role = strtolower(trim((string) ($_POST['role'] ?? '')));
$baseName = trim((string) ($_POST['base_name'] ?? 'EPC'));
$margin = (float) ($_POST['margin_percent'] ?? 0);
$sourceUrl = trim((string) ($_POST['source_url'] ?? ''));

if (empty($_FILES['price_file']) || (int) $_FILES['price_file']['error'] !== UPLOAD_ERR_OK) {
	exit(json_encode(array('status' => false, 'message' => 'Choose an Excel/CSV file to upload')));
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
$tmpPath = $tmpDir . '/commerce_' . time() . '_' . mt_rand(1000, 9999) . '.' . $ext;
if (!move_uploaded_file((string) $_FILES['price_file']['tmp_name'], $tmpPath)) {
	exit(json_encode(array('status' => false, 'message' => 'Could not store upload')));
}

$result = epc_commerce_ingest_file($db_link, $tmpPath, $role, $baseName, $margin, $sourceUrl);
$result['original_filename'] = $origName;
@unlink($tmpPath);

exit(json_encode($result, JSON_UNESCAPED_UNICODE));
