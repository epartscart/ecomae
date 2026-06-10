<?php
/**
 * One-shot: import Desktop R-UAE.csv into price list R-UAE and link warehouse.
 * POST multipart: token, key, price_file (or place R-UAE.csv in site root).
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

$priceName = 'R-UAE';

function epc_import_clean_field(string $value): string
{
    $value = str_replace(["\xC2\xA0", "\xA0"], ' ', $value);
    $value = preg_replace('/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/u', '', $value) ?? '';
    if (function_exists('mb_convert_encoding')) {
        $value = mb_convert_encoding($value, 'UTF-8', 'UTF-8');
    }
    return trim($value);
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

$q = $db->prepare('SELECT * FROM `shop_docpart_prices` WHERE UPPER(`name`) = UPPER(?) LIMIT 1');
$q->execute([$priceName]);
$price = $q->fetch(PDO::FETCH_ASSOC);

if (!$price) {
    $db->prepare(
        'INSERT INTO `shop_docpart_prices`
        (`name`,`load_mode`,`strings_to_left`,`manufacturer_col`,`article_col`,`name_col`,`exist_col`,`price_col`,`time_to_exe_col`,`storage_col`,`min_order_col`,`clean_before`,`file_name_substring`,`encoding`,`separator`,`h_time`)
        VALUES (?, 1, 1, 1, 2, 3, 4, 5, 7, 0, 0, 1, ?, ?, ?, ?)'
    )->execute([$priceName, $priceName, 'utf-8', ',', '0']);
    $q->execute([$priceName]);
    $price = $q->fetch(PDO::FETCH_ASSOC);
}

if (!$price) {
    exit(json_encode(['status' => false, 'message' => 'Could not resolve price list']));
}

$priceId = (int)$price['id'];

$sq = $db->prepare('SELECT `id`, `connection_options` FROM `shop_storages` WHERE UPPER(`name`) = UPPER(?) LIMIT 1');
$sq->execute([$priceName]);
$storage = $sq->fetch(PDO::FETCH_ASSOC);
if ($storage) {
    $opts = json_decode((string)$storage['connection_options'], true);
    if (!is_array($opts)) {
        $opts = [];
    }
    $opts['price_id'] = (string)$priceId;
    if (!isset($opts['probability'])) {
        $opts['probability'] = '100';
    }
    $db->prepare('UPDATE `shop_storages` SET `connection_options` = ? WHERE `id` = ?')
        ->execute([json_encode($opts, JSON_UNESCAPED_UNICODE), (int)$storage['id']]);
}

$csvPath = '';
$origName = 'R-UAE.csv';
if (!empty($_FILES['price_file']) && $_FILES['price_file']['error'] === UPLOAD_ERR_OK) {
    $origName = basename((string)$_FILES['price_file']['name']);
    $csvPath = sys_get_temp_dir() . '/r-uae-' . time() . '.csv';
    move_uploaded_file($_FILES['price_file']['tmp_name'], $csvPath);
} elseif (is_file(__DIR__ . '/R-UAE.csv')) {
    $csvPath = __DIR__ . '/R-UAE.csv';
}

if ($csvPath === '' || !is_file($csvPath)) {
    exit(json_encode(['status' => false, 'message' => 'CSV file required']));
}

$archiveDir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/price_upload_history/' . $priceId;
if (!is_dir($archiveDir)) {
    mkdir($archiveDir, 0755, true);
}
$archivePath = $archiveDir . '/' . time() . '_' . preg_replace('/[^a-zA-Z0-9._-]+/', '_', $origName);
copy($csvPath, $archivePath);

$db->prepare('DELETE FROM `shop_docpart_prices_data` WHERE `price_id` = ?')->execute([$priceId]);

$delimiter = ',';
$fh = fopen($csvPath, 'rb');
$headerRow = fgetcsv($fh, 0, $delimiter);
$rUaeCols = [
    'manufacturer' => 1,
    'article' => 2,
    'name' => 3,
    'exist' => 4,
    'price' => 5,
    'time_to_exe' => 7,
];
$columnLabels = epc_price_build_source_column_labels(is_array($headerRow) ? $headerRow : null, $rUaeCols);

$db->exec('ALTER TABLE `shop_docpart_prices_data` DISABLE KEYS');
$nextId = (int)$db->query('SELECT COALESCE(MAX(`id`), 0) FROM `shop_docpart_prices_data`')->fetchColumn();

$sqlHead = 'INSERT INTO `shop_docpart_prices_data` (`id`,`price_id`,`manufacturer`,`article`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order`) VALUES ';
$valuesSql = '';
$bind = [];
$inserted = 0;
$skipped = 0;
$importIssues = [];
$lineNo = 1;

$flush = static function () use (&$valuesSql, &$bind, &$inserted, $db, $sqlHead): void {
    if ($valuesSql === '') {
        return;
    }
    $db->prepare($sqlHead . $valuesSql)->execute($bind);
    $inserted += substr_count($valuesSql, '(');
    $valuesSql = '';
    $bind = [];
};

while (($row = fgetcsv($fh, 0, $delimiter)) !== false) {
    $lineNo++;
    if (count($row) > count($columnLabels)) {
        $columnLabels = epc_price_build_source_column_labels(
            is_array($headerRow) ? $headerRow : null,
            $rUaeCols,
            count($row)
        );
    }
    if (!$row || (count($row) === 1 && trim((string)$row[0]) === '')) {
        $importIssues[] = epc_price_history_issue_detail(
            $lineNo,
            'skipped',
            'empty_row',
            is_array($row) ? $row : [],
            $columnLabels
        );
        $skipped++;
        continue;
    }
    $manufacturer = epc_import_clean_field((string)($row[0] ?? ''));
    $articleShow = epc_import_clean_field((string)($row[1] ?? ''));
    $name = epc_import_clean_field((string)($row[2] ?? ''));
    $article = strtoupper(preg_replace('/[^A-Z0-9]/', '', (string)($row[1] ?? '')));
    if ($article === '') {
        $importIssues[] = epc_price_history_issue_detail(
            $lineNo,
            'skipped',
            'empty_article',
            $row,
            $columnLabels,
            [
                'manufacturer' => $manufacturer,
                'article_show' => $articleShow,
                'name' => $name,
                'exist' => (string)($row[3] ?? ''),
                'price' => (string)($row[4] ?? ''),
                'details' => 'Article column value: "' . $articleShow . '"',
            ]
        );
        $skipped++;
        continue;
    }
    $priceVal = (float)str_replace([' ', ','], ['', '.'], (string)($row[4] ?? '0'));
    if ($priceVal <= 0) {
        $priceRaw = (string)($row[4] ?? '');
        $importIssues[] = epc_price_history_issue_detail(
            $lineNo,
            'skipped',
            'invalid_price',
            $row,
            $columnLabels,
            [
                'manufacturer' => $manufacturer,
                'article' => $article,
                'article_show' => $articleShow,
                'name' => $name,
                'exist' => (string)($row[3] ?? ''),
                'price' => $priceRaw,
                'details' => 'Price column value: "' . $priceRaw . '"',
            ]
        );
        $skipped++;
        continue;
    }
    $timeRaw = (string)($row[6] ?? '');
    $timeToExe = 0;
    if (preg_match('/(\d+)/', $timeRaw, $m)) {
        $timeToExe = (int)$m[1];
    }
    $nextId++;
    if ($valuesSql !== '') {
        $valuesSql .= ',';
    }
    $valuesSql .= '(?,?,?,?,?,?,?,?,?,?,?)';
    array_push(
        $bind,
        $nextId,
        $priceId,
        epc_import_clean_field((string)($row[0] ?? '')),
        $article,
        epc_import_clean_field((string)($row[1] ?? '')),
        epc_import_clean_field((string)($row[2] ?? '')),
        min(999999, max(0, (int)preg_replace('/\D/', '', (string)($row[3] ?? '0')))),
        $priceVal,
        $timeToExe,
        '',
        0
    );
    if (substr_count($valuesSql, '(') >= 1000) {
        $flush();
    }
}
fclose($fh);
$flush();
$db->exec('ALTER TABLE `shop_docpart_prices_data` ENABLE KEYS');
$db->prepare('UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?')->execute([time(), $priceId]);

$cnt = $db->prepare('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?');
$cnt->execute([$priceId]);
$items = (int)$cnt->fetchColumn();

$brands = (int)$db->query(
    "SELECT COUNT(DISTINCT `manufacturer`) FROM `shop_docpart_prices_data` WHERE `price_id` = {$priceId} AND TRIM(`manufacturer`) <> ''"
)->fetchColumn();

$db->exec("CREATE TABLE IF NOT EXISTS `epc_price_upload_history` (
    `id` INT(11) NOT NULL AUTO_INCREMENT,
    `price_id` INT(11) NOT NULL DEFAULT 0,
    `price_name` VARCHAR(255) NOT NULL DEFAULT '',
    `upload_source` VARCHAR(32) NOT NULL DEFAULT '',
    `source_ref` VARCHAR(64) NOT NULL DEFAULT '',
    `original_filename` VARCHAR(255) NOT NULL DEFAULT '',
    `stored_relpath` VARCHAR(512) NOT NULL DEFAULT '',
    `file_size` BIGINT NOT NULL DEFAULT 0,
    `rows_imported` INT(11) NOT NULL DEFAULT 0,
    `rows_skipped` INT(11) NOT NULL DEFAULT 0,
    `rows_in_db` INT(11) NOT NULL DEFAULT 0,
    `brands_count` INT(11) NOT NULL DEFAULT 0,
    `items_count` INT(11) NOT NULL DEFAULT 0,
    `status` VARCHAR(16) NOT NULL DEFAULT 'ok',
    `error_text` TEXT NULL,
    `stats_json` LONGTEXT NULL,
    `uploaded_by` INT(11) NOT NULL DEFAULT 0,
    `created_at` DATETIME NOT NULL,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8");

$rel = '/content/files/price_upload_history/' . $priceId . '/' . basename($archivePath);
$db->prepare('UPDATE `epc_price_upload_history` SET `is_active` = 0 WHERE `price_id` = ?')->execute([$priceId]);
$historyStatus = $inserted > 0 ? ($skipped > 0 ? 'partial' : 'ok') : 'failed';
$historyId = 0;
$db->prepare(
    'INSERT INTO `epc_price_upload_history`
    (`price_id`,`price_name`,`upload_source`,`original_filename`,`stored_relpath`,`file_size`,`rows_imported`,`rows_skipped`,`rows_in_db`,`brands_count`,`items_count`,`status`,`is_active`,`created_at`)
    VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,NOW())'
)->execute([
    $priceId,
    $priceName,
    'deploy_r_uae',
    $origName,
    $rel,
    (int)filesize($archivePath),
    $inserted,
    $skipped,
    $items,
    $brands,
    $items,
    $historyStatus,
    $inserted > 0 ? 1 : 0,
]);
$historyId = (int)$db->lastInsertId();
if ($historyId > 0 && count($importIssues) > 0) {
    epc_price_history_attach_issues($db, $historyId, $priceId, $importIssues);
}

echo json_encode([
    'status' => $inserted > 0,
    'price_id' => $priceId,
    'price_name' => $priceName,
    'records_handled' => $inserted,
    'rows_skipped' => $skipped,
    'records_in_db' => $items,
    'brands_count' => $brands,
    'storage_id' => $storage ? (int)$storage['id'] : null,
    'stored_file' => $rel,
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
