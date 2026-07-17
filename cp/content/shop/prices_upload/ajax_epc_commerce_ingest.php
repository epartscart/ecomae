<?php
/**
 * CP AJAX: commerce sales/purchase/inventory Excel → S/P/L warehouse price lists.
 *
 * Actions (POST):
 *   upload (default) — file and/or source_url
 *   list_sources
 *   refresh_url — price_id
 *   refresh_all
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

$action = strtolower(trim((string) ($_POST['action'] ?? 'upload')));

if ($action === 'list_sources') {
	$urlOnly = (($_POST['url_only'] ?? '') === '1');
	$sources = epc_commerce_list_sources($db_link, $urlOnly);
	exit(json_encode(array(
		'status' => true,
		'count' => count($sources),
		'sources' => $sources,
	), JSON_UNESCAPED_UNICODE));
}

if ($action === 'refresh_all') {
	$result = epc_commerce_refresh_all_linked($db_link);
	$result['action'] = 'refresh_all';
	exit(json_encode($result, JSON_UNESCAPED_UNICODE));
}

if ($action === 'refresh_url') {
	$priceId = (int) ($_POST['price_id'] ?? 0);
	$marginRaw = $_POST['margin_percent'] ?? null;
	$marginOverride = ($marginRaw === null || $marginRaw === '') ? null : (float) $marginRaw;
	$result = epc_commerce_refresh_price_id($db_link, $priceId, $marginOverride);
	exit(json_encode($result, JSON_UNESCAPED_UNICODE));
}

$role = strtolower(trim((string) ($_POST['role'] ?? '')));
$baseName = trim((string) ($_POST['base_name'] ?? 'EPC'));
$margin = (float) ($_POST['margin_percent'] ?? 0);
$sourceUrl = trim((string) ($_POST['source_url'] ?? ''));

$hasFile = !empty($_FILES['price_file']) && (int) $_FILES['price_file']['error'] === UPLOAD_ERR_OK;
if (!$hasFile && $sourceUrl === '') {
	exit(json_encode(array('status' => false, 'message' => 'Choose an Excel/CSV file, or paste a file URL')));
}

$tmpPath = '';
$origName = '';
if ($hasFile) {
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
} else {
	$dl = epc_commerce_download_url($sourceUrl);
	if (empty($dl['ok'])) {
		exit(json_encode(array('status' => false, 'message' => (string) $dl['message'])));
	}
	$tmpPath = (string) $dl['path'];
	$origName = basename((string) (parse_url($sourceUrl, PHP_URL_PATH) ?: 'url-source.csv'));
}

$result = epc_commerce_ingest_file($db_link, $tmpPath, $role, $baseName, $margin, $sourceUrl);
$result['original_filename'] = $origName;
$result['ingest_mode'] = $hasFile ? 'upload' : 'url';
@unlink($tmpPath);

exit(json_encode($result, JSON_UNESCAPED_UNICODE));
