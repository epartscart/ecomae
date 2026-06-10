<?php
/**
 * Archive an upload file and register it in epc_price_upload_history (no re-import).
 * POST: token, key, price_id, price_file[, upload_source][, set_active=1]
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_POST['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_POST['key'] ?? '') !== $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Invalid tech_key']));
}

if (empty($_FILES['price_file']) || $_FILES['price_file']['error'] !== UPLOAD_ERR_OK) {
    exit(json_encode(['status' => false, 'message' => 'price_file required']));
}

$priceId = (int)($_POST['price_id'] ?? 0);
if ($priceId <= 0) {
    exit(json_encode(['status' => false, 'message' => 'price_id required']));
}

try {
$db = new PDO(
        'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
        $DP_Config->user,
        $DP_Config->password,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
$db->query('SET NAMES utf8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';

$pn = $db->prepare('SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;');
$pn->execute([$priceId]);
$priceName = (string)$pn->fetchColumn();
if ($priceName === '') {
    exit(json_encode(['status' => false, 'message' => 'Price list not found']));
}

$origName = basename((string)$_FILES['price_file']['name']);
$tmp = $_FILES['price_file']['tmp_name'];
$storedRel = epc_price_history_archive_file($tmp, $priceId, $origName);
if ($storedRel === '') {
    exit(json_encode(['status' => false, 'message' => 'Could not archive file to price_upload_history']));
}

$items = epc_price_history_count_items($db, $priceId);
$brands = epc_price_history_count_brands($db, $priceId);
$source = trim((string)($_POST['upload_source'] ?? 'deploy_api'));
if ($source === '') {
    $source = 'deploy_api';
}

$historyId = epc_price_history_save($db, [
    'price_id' => $priceId,
    'price_name' => $priceName,
    'upload_source' => $source,
    'original_filename' => $origName,
    'stored_relpath' => $storedRel,
    'file_size' => (int)filesize($tmp),
    'rows_imported' => $items,
    'rows_in_db' => $items,
    'brands_count' => $brands,
    'items_count' => $items,
    'status' => 'ok',
    'error_text' => '',
]);

if ($historyId > 0 && (int)($_POST['set_active'] ?? 1) === 1) {
    epc_price_history_set_active($db, $priceId, $historyId);
}

echo json_encode([
    'status' => true,
    'history_id' => $historyId,
    'price_id' => $priceId,
    'price_name' => $priceName,
    'stored_relpath' => $storedRel,
    'download_path' => $storedRel,
    'items_count' => $items,
], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
    http_response_code(500);
    echo json_encode([
        'status' => false,
        'message' => $e->getMessage(),
        'file' => $e->getFile(),
        'line' => $e->getLine(),
    ], JSON_UNESCAPED_UNICODE);
}
