<?php
/**
 * Commerce Excel/CSV → warehouse price lists (S / P / L).
 *
 * POST:
 *   token=epartscart-deploy-2026
 *   key=<tech_key>
 *   role=sales|purchase|inventory
 *   base_name=MAIN          (optional; default EPC)
 *   margin_percent=15       (purchase/inventory; sales ignored)
 *   source_url=https://...  (optional recurring URL stored on list load_mode=4)
 *   price_file=<file>       CSV/TXT/XLS/XLSX
 *
 * GET action=refresh_url&price_id=N  — re-fetch list.link and re-ingest by role from list name suffix
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

if ($action === 'refresh_url') {
	$priceId = (int) ($_POST['price_id'] ?? $_GET['price_id'] ?? 0);
	if ($priceId <= 0) {
		exit(json_encode(array('status' => false, 'message' => 'price_id required')));
	}
	$q = $db->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1');
	$q->execute(array($priceId));
	$price = $q->fetch(PDO::FETCH_ASSOC);
	if (!$price) {
		exit(json_encode(array('status' => false, 'message' => 'Price list not found')));
	}
	$url = trim((string) ($price['link'] ?? ''));
	if ($url === '' || !preg_match('#^https?://#i', $url)) {
		exit(json_encode(array('status' => false, 'message' => 'No http(s) link on this price list')));
	}
	$name = (string) $price['name'];
	$role = 'sales';
	$base = $name;
	if (preg_match('/\.P$/i', $name)) {
		$role = 'purchase';
		$base = preg_replace('/\.P$/i', '', $name);
	} elseif (preg_match('/-L$/i', $name)) {
		$role = 'inventory';
		$base = preg_replace('/-L$/i', '', $name);
	} elseif (preg_match('/-S$/i', $name)) {
		$role = 'sales';
		$base = preg_replace('/-S$/i', '', $name);
	}
	$tmp = sys_get_temp_dir() . '/epc_commerce_url_' . $priceId . '_' . time();
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 120,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_SSL_VERIFYHOST => 0,
	));
	$body = curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	if ($body === false || $code >= 400 || $body === '') {
		exit(json_encode(array('status' => false, 'message' => 'Failed to download link', 'http' => $code)));
	}
	// guess extension
	$ext = 'csv';
	$pathPart = strtolower((string) parse_url($url, PHP_URL_PATH));
	if (preg_match('/\.(xlsx|xls|csv|txt)$/', $pathPart, $m)) {
		$ext = $m[1];
	}
	$filePath = $tmp . '.' . $ext;
	file_put_contents($filePath, $body);
	$margin = (float) ($_POST['margin_percent'] ?? $_GET['margin_percent'] ?? 0);
	$result = epc_commerce_ingest_file($db, $filePath, $role, (string) $base, $margin, $url);
	@unlink($filePath);
	$result['action'] = 'refresh_url';
	$result['source_url'] = $url;
	exit(json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

if ($action !== 'upload' && $action !== '') {
	exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
}

if (empty($_FILES['price_file']) || (int) $_FILES['price_file']['error'] !== UPLOAD_ERR_OK) {
	exit(json_encode(array('status' => false, 'message' => 'price_file upload required')));
}

$role = strtolower(trim((string) ($_POST['role'] ?? $_GET['role'] ?? '')));
$baseName = trim((string) ($_POST['base_name'] ?? $_GET['base_name'] ?? 'EPC'));
$margin = (float) ($_POST['margin_percent'] ?? $_GET['margin_percent'] ?? 0);
$sourceUrl = trim((string) ($_POST['source_url'] ?? $_GET['source_url'] ?? ''));

$origName = basename((string) $_FILES['price_file']['name']);
$ext = strtolower(pathinfo($origName, PATHINFO_EXTENSION));
if (!in_array($ext, array('csv', 'txt', 'xls', 'xlsx'), true)) {
	exit(json_encode(array('status' => false, 'message' => 'Unsupported file type')));
}

$tmpPath = sys_get_temp_dir() . '/epc_commerce_up_' . getmypid() . '_' . time() . '.' . $ext;
if (!move_uploaded_file((string) $_FILES['price_file']['tmp_name'], $tmpPath)) {
	exit(json_encode(array('status' => false, 'message' => 'Could not store upload')));
}

$result = epc_commerce_ingest_file($db, $tmpPath, $role, $baseName, $margin, $sourceUrl);
$result['original_filename'] = $origName;
@unlink($tmpPath);

exit(json_encode($result, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
