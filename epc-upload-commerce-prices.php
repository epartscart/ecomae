<?php
/**
 * Commerce Excel/CSV → warehouse price lists (S / P / L).
 *
 * POST upload:
 *   token=epartscart-deploy-2026
 *   key=<tech_key>
 *   role=sales|purchase|inventory
 *   base_name=MAIN          (optional; default EPC)
 *   margin_percent=15       (purchase/inventory; sales ignored)
 *   source_url=https://...  (optional recurring URL; OR use alone for URL-only ingest)
 *   price_file=<file>       CSV/TXT/XLS/XLSX (optional if source_url given)
 *
 * GET/POST actions:
 *   action=refresh_url&price_id=N[&margin_percent=15]
 *   action=refresh_all      — re-fetch every commerce list with stored URL (cron)
 *   action=list_sources     — JSON of *-S / *.P / *-L lists + link/margin meta
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
set_time_limit(0);

$deployToken = 'epartscart-deploy-2026';
if (($_POST['token'] ?? $_GET['token'] ?? '') !== $deployToken) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();

$requestKey = (string) ($_POST['key'] ?? $_GET['key'] ?? '');
if ($requestKey !== $DP_Config->tech_key) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Invalid tech_key')));
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

require_once __DIR__ . '/content/shop/docpart/epc_commerce_price_ingest.php';

$action = (string) ($_GET['action'] ?? $_POST['action'] ?? 'upload');

if ($action === 'list_sources') {
	$urlOnly = (($_GET['url_only'] ?? $_POST['url_only'] ?? '') === '1');
	$sources = epc_commerce_list_sources($db, $urlOnly);
	exit(json_encode(array(
		'status' => true,
		'count' => count($sources),
		'sources' => $sources,
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

if ($action === 'refresh_all') {
	$result = epc_commerce_refresh_all_linked($db);
	$result['action'] = 'refresh_all';
	exit(json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

if ($action === 'refresh_url') {
	$priceId = (int) ($_POST['price_id'] ?? $_GET['price_id'] ?? 0);
	$marginRaw = $_POST['margin_percent'] ?? $_GET['margin_percent'] ?? null;
	$marginOverride = ($marginRaw === null || $marginRaw === '') ? null : (float) $marginRaw;
	$result = epc_commerce_refresh_price_id($db, $priceId, $marginOverride);
	exit(json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

if ($action !== 'upload' && $action !== '') {
	exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
}

$role = strtolower(trim((string) ($_POST['role'] ?? $_GET['role'] ?? '')));
$baseName = trim((string) ($_POST['base_name'] ?? $_GET['base_name'] ?? 'EPC'));
$margin = (float) ($_POST['margin_percent'] ?? $_GET['margin_percent'] ?? 0);
$sourceUrl = trim((string) ($_POST['source_url'] ?? $_GET['source_url'] ?? ''));

$hasFile = !empty($_FILES['price_file']) && (int) $_FILES['price_file']['error'] === UPLOAD_ERR_OK;
if (!$hasFile && $sourceUrl === '') {
	exit(json_encode(array('status' => false, 'message' => 'price_file upload or source_url required')));
}

$tmpPath = '';
$origName = '';
if ($hasFile) {
	$origName = basename((string) $_FILES['price_file']['name']);
	$ext = strtolower(pathinfo($origName, PATHINFO_EXTENSION));
	if (!in_array($ext, array('csv', 'txt', 'xls', 'xlsx'), true)) {
		exit(json_encode(array('status' => false, 'message' => 'Unsupported file type')));
	}
	$tmpPath = sys_get_temp_dir() . '/epc_commerce_up_' . getmypid() . '_' . time() . '.' . $ext;
	if (!move_uploaded_file((string) $_FILES['price_file']['tmp_name'], $tmpPath)) {
		exit(json_encode(array('status' => false, 'message' => 'Could not store upload')));
	}
} else {
	$dl = epc_commerce_download_url($sourceUrl);
	if (empty($dl['ok'])) {
		exit(json_encode(array('status' => false, 'message' => (string) $dl['message'], 'http' => (int) $dl['http'])));
	}
	$tmpPath = (string) $dl['path'];
	$origName = basename(parse_url($sourceUrl, PHP_URL_PATH) ?: 'url-source.csv');
}

$result = epc_commerce_ingest_file($db, $tmpPath, $role, $baseName, $margin, $sourceUrl);
$result['original_filename'] = $origName;
$result['ingest_mode'] = $hasFile ? 'upload' : 'url';
@unlink($tmpPath);

exit(json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
