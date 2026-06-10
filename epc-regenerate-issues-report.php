<?php
/**
 * Re-scan an archived upload CSV and rebuild the detailed skipped/errors report.
 *
 * GET/POST: token=epartscart-deploy-2026, key=tech_key, history_id=NN
 * Optional: price_id=NN (validates history belongs to price)
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
set_time_limit(0);

$deployToken = 'epartscart-deploy-2026';
if (($_POST['token'] ?? $_GET['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;

$requestKey = (string)($_POST['key'] ?? $_GET['key'] ?? '');
if ($requestKey !== $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Invalid tech_key']));
}

$historyId = (int)($_POST['history_id'] ?? $_GET['history_id'] ?? 0);
$priceIdFilter = (int)($_POST['price_id'] ?? $_GET['price_id'] ?? 0);

if ($historyId <= 0) {
    exit(json_encode(['status' => false, 'message' => 'history_id required']));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';

try {
    $db = new PDO(
        'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
        $DP_Config->user,
        $DP_Config->password,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
    $db->query('SET NAMES utf8');
} catch (Throwable $e) {
    exit(json_encode(['status' => false, 'message' => 'DB error']));
}

$row = epc_price_history_get_row($db, $historyId);
if (!$row) {
    exit(json_encode(['status' => false, 'message' => 'History record not found']));
}

$priceId = (int)$row['price_id'];
if ($priceIdFilter > 0 && $priceIdFilter !== $priceId) {
    exit(json_encode(['status' => false, 'message' => 'history_id does not match price_id']));
}

$storedRel = (string)($row['stored_relpath'] ?? '');
if ($storedRel === '') {
    exit(json_encode(['status' => false, 'message' => 'No archived file for this upload']));
}

$filePath = $_SERVER['DOCUMENT_ROOT'] . $storedRel;
if (!is_file($filePath)) {
    exit(json_encode(['status' => false, 'message' => 'Archived file missing on disk', 'path' => $storedRel]);
}

$q = $db->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1');
$q->execute([$priceId]);
$price = $q->fetch(PDO::FETCH_ASSOC);
if (!$price) {
    exit(json_encode(['status' => false, 'message' => 'Price list not found']));
}

$scan = epc_price_scan_csv_issues($db, $price, $filePath);
$issues = $scan['issues'];

epc_price_history_attach_issues($db, $historyId, $priceId, $issues);

$db->prepare(
    'UPDATE `epc_price_upload_history` SET `rows_skipped` = ? WHERE `id` = ? LIMIT 1'
)->execute([(int)$scan['rows_skipped'], $historyId]);

$counts = epc_price_history_count_issue_types($issues);

echo json_encode([
    'status' => true,
    'history_id' => $historyId,
    'price_id' => $priceId,
    'price_name' => (string)$row['price_name'],
    'rows_skipped' => (int)$scan['rows_skipped'],
    'issues_written' => count($issues),
    'skipped_lines' => $counts['skipped'],
    'error_lines' => $counts['error'],
    'column_labels' => array_values($scan['column_labels']),
    'issues_file' => '/content/files/price_upload_history/' . $priceId . '/' . $historyId . '_issues.csv',
], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
