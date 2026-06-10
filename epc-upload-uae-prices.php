<?php
/**
 * Deploy-only: upload UAE supplier price files to matching Docpart price lists.
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

try {
    $db = new PDO(
        'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
        $DP_Config->user,
        $DP_Config->password,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
} catch (PDOException $e) {
    exit(json_encode(['status' => false, 'message' => 'DB connect failed']));
}
$db->query('SET NAMES utf8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';

$action = (string)($_GET['action'] ?? $_POST['action'] ?? '');

if ($action === 'list_prices') {
    $rows = $db->query('SELECT `id`, `name`, `last_updated`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = `shop_docpart_prices`.`id`) AS `records_count` FROM `shop_docpart_prices` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
    exit(json_encode(['status' => true, 'prices' => $rows], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

if ($action === 'list_latest_uploads') {
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';
    $map = epc_price_history_get_latest_map($db);
    $uploads = [];
    foreach ($map as $pid => $row) {
        $uploads[] = [
            'price_id' => (int)$pid,
            'history_id' => (int)$row['id'],
            'price_name' => (string)$row['price_name'],
            'original_filename' => (string)$row['original_filename'],
            'stored_relpath' => (string)$row['stored_relpath'],
            'file_size' => (int)$row['file_size'],
            'created_at' => (string)$row['created_at'],
            'status' => (string)$row['status'],
            'rows_imported' => (int)$row['rows_imported'],
            'is_active' => (int)$row['is_active'],
        ];
    }
    exit(json_encode(['status' => true, 'uploads' => $uploads], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

if ($action === 'reupload_latest') {
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';

    $priceId = (int)($_POST['price_id'] ?? $_GET['price_id'] ?? 0);
    $priceName = trim((string)($_POST['price_name'] ?? $_GET['price_name'] ?? ''));
    if ($priceId <= 0 && $priceName === '') {
        exit(json_encode(['status' => false, 'message' => 'price_id or price_name required']));
    }

    $price = epc_price_resolve_or_create_list($db, $priceId, $priceName);
    if (!$price) {
        exit(json_encode(['status' => false, 'message' => 'Price list not found']));
    }
    $priceId = (int)$price['id'];
    $priceName = (string)$price['name'];

    $histRow = epc_price_history_get_active($db, $priceId);
    if (!$histRow) {
        exit(json_encode(['status' => false, 'message' => 'No downloadable upload in CP history for this price list']));
    }

    $sourcePath = epc_price_history_file_absolute_path($histRow);
    if ($sourcePath === '' || !is_file($sourcePath)) {
        exit(json_encode(['status' => false, 'message' => 'Archived file missing on server', 'stored_relpath' => (string)$histRow['stored_relpath']]));
    }

    epc_price_link_storage_to_list($db, $priceName, $priceId);

    $origName = (string)$histRow['original_filename'];
    if ($origName === '') {
        $origName = basename($sourcePath);
    }

    $storedRel = epc_price_history_archive_file($sourcePath, $priceId, $origName);
    $fileSize = (int)filesize($sourcePath);

    $historyId = epc_price_history_save($db, [
        'price_id' => $priceId,
        'price_name' => $priceName,
        'upload_source' => 'deploy_reupload',
        'original_filename' => $origName,
        'stored_relpath' => $storedRel,
        'file_size' => $fileSize,
        'status' => 'pending',
        'source_ref' => 'from_history_' . (int)$histRow['id'],
    ]);

    $delimiterOverride = null;
    $srcStats = json_decode((string)($histRow['stats_json'] ?? ''), true);
    if (is_array($srcStats) && !empty($srcStats['delimiter'])) {
        $delimiterOverride = (string)$srcStats['delimiter'];
    }
    if ($delimiterOverride === null || $delimiterOverride === '') {
        $delimiterOverride = epc_detect_csv_delimiter($sourcePath);
    }

    $importError = '';
    try {
        $result = epc_import_csv($db, $price, $sourcePath, $delimiterOverride);
    } catch (Throwable $e) {
        $importError = $e->getMessage();
        $result = ['status' => false, 'message' => $importError, 'records_handled' => 0, 'rows_skipped' => 0, 'import_issues' => []];
    }

    $countQ = $db->prepare('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?');
    $countQ->execute([$priceId]);
    $recordsInDb = (int)$countQ->fetchColumn();

    $rowsSkipped = (int)($result['rows_skipped'] ?? 0);
    $importIssues = is_array($result['import_issues'] ?? null) ? $result['import_issues'] : [];
    $importOk = $importError === '' && !empty($result['status']);
    $historyStatus = $importOk ? (($rowsSkipped > 0 || count($importIssues) > 0) ? 'partial' : 'ok') : 'failed';

    $historyUpdate = [
        'rows_imported' => (int)($result['records_handled'] ?? 0),
        'rows_skipped' => $rowsSkipped,
        'rows_in_db' => $recordsInDb,
        'brands_count' => epc_price_history_count_brands($db, $priceId),
        'items_count' => $recordsInDb,
        'status' => $historyStatus,
        'error_text' => $importOk ? '' : (string)($result['message'] ?? $importError),
        'stats_json' => json_encode([
            'delimiter' => $result['delimiter'] ?? $delimiterOverride,
            'reupload_from_history_id' => (int)$histRow['id'],
        ], JSON_UNESCAPED_UNICODE),
    ];
    if ($historyId > 0) {
        if (count($importIssues) > 0 && count($importIssues) <= 5000) {
            $historyUpdate['import_issues'] = $importIssues;
        }
        epc_price_history_update_by_id($db, $historyId, $historyUpdate);
        if ($importOk) {
            epc_price_history_set_active($db, $priceId, $historyId);
        }
    }

    exit(json_encode([
        'status' => $importOk,
        'message' => $result['message'] ?? '',
        'action' => 'reupload_latest',
        'price_id' => $priceId,
        'price_name' => $priceName,
        'records_handled' => (int)($result['records_handled'] ?? 0),
        'rows_skipped' => $rowsSkipped,
        'records_in_db' => $recordsInDb,
        'history_id' => $historyId,
        'source_history_id' => (int)$histRow['id'],
        'source_filename' => $origName,
        'stored_file' => $storedRel,
    ], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

if ($action !== '' && $action !== 'upload') {
    exit(json_encode(['status' => false, 'message' => 'Unknown action']));
}

if (empty($_FILES['price_file']) || $_FILES['price_file']['error'] !== UPLOAD_ERR_OK) {
    exit(json_encode(['status' => false, 'message' => 'price_file upload required']));
}

function epc_prepare_string(string $string): string
{
    $sweep = ["/", "#", "\r\n", "\r", "\n", "\t", "'", '"', "\\"];
    return str_replace($sweep, '', $string);
}

function epc_clip(string $value, int $max = 255): string
{
    if (function_exists('mb_substr')) {
        return mb_substr($value, 0, $max, 'UTF-8');
    }
    return substr($value, 0, $max);
}

function epc_parse_time_to_exe($value): int
{
    $value = trim((string)$value);
    if ($value === '') {
        return 0;
    }
    if (ctype_digit($value)) {
        return (int)$value;
    }
    if (preg_match('/(\d+)/', $value, $m)) {
        return (int)$m[1];
    }
    return 0;
}

function epc_detect_csv_delimiter(string $filePath): string
{
    $fh = fopen($filePath, 'rb');
    if ($fh === false) {
        return ',';
    }
    $line = fgets($fh);
    fclose($fh);
    if ($line === false || trim($line) === '') {
        return ',';
    }
    $counts = [
        ';' => substr_count($line, ';'),
        ',' => substr_count($line, ','),
        "\t" => substr_count($line, "\t"),
    ];
    arsort($counts);
    foreach ($counts as $char => $count) {
        if ($count > 0) {
            return $char;
        }
    }
    return ',';
}

function epc_normalize_csv_delimiter(string $delimiter): string
{
    if ($delimiter === '\t' || $delimiter === '\\t') {
        return "\t";
    }
    if ($delimiter === '') {
        return ',';
    }
    return $delimiter;
}

function epc_import_csv(PDO $db, array $price, string $filePath, ?string $delimiterOverride = null): array
{
    $priceId = (int)$price['id'];
    $skipRows = max(0, (int)$price['strings_to_left']);
    if ($delimiterOverride !== null && $delimiterOverride !== '') {
        $delimiter = epc_normalize_csv_delimiter($delimiterOverride);
    } else {
        $delimiter = epc_normalize_csv_delimiter((string)$price['separator']);
    }

    $cols = epc_price_operational_cols_from_config($price);
    epc_ensure_prices_data_exist_column_int($db);

    if ((int)$price['clean_before'] === 1) {
        $db->prepare('DELETE FROM `shop_docpart_prices_data` WHERE `price_id` = ?')->execute([$priceId]);
    }

    $fh = fopen($filePath, 'rb');
    if ($fh === false) {
        return ['status' => false, 'message' => 'Cannot open CSV', 'records_handled' => 0];
    }

    $headerRow = null;
    if ($skipRows > 0) {
        $headerRow = fgetcsv($fh, 0, $delimiter);
        for ($i = 1; $i < $skipRows; $i++) {
            fgetcsv($fh, 0, $delimiter);
        }
    }
    $columnLabels = epc_price_build_source_column_labels(is_array($headerRow) ? $headerRow : null, $cols);
    $importIssues = [];
    $lineNo = $skipRows;

    $db->exec('ALTER TABLE `shop_docpart_prices_data` DISABLE KEYS');

    $nextId = (int)$db->query('SELECT COALESCE(MAX(`id`), 0) FROM `shop_docpart_prices_data`')->fetchColumn();

    $sqlHead = 'INSERT INTO `shop_docpart_prices_data` (`id`,`price_id`,`manufacturer`,`article`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order`) VALUES ';
    $batch = 1000;
    $valuesSql = '';
    $bind = [];
    $inserted = 0;
    $skipped = 0;

    $flush = static function () use (&$valuesSql, &$bind, &$inserted, $db, $sqlHead): void {
        if ($valuesSql === '') {
            return;
        }
        $stmt = $db->prepare($sqlHead . $valuesSql);
        $stmt->execute($bind);
        $inserted += (int)substr_count($valuesSql, '(');
        $valuesSql = '';
        $bind = [];
    };

    while (($row = fgetcsv($fh, 0, $delimiter)) !== false) {
        $lineNo++;
        if (count($row) > count($columnLabels)) {
            $columnLabels = epc_price_build_source_column_labels(
                is_array($headerRow) ? $headerRow : null,
                $cols,
                count($row)
            );
        }

        if (!$row || (count($row) === 1 && trim((string)$row[0]) === '')) {
            $skipped++;
            $importIssues[] = epc_price_history_issue_detail(
                $lineNo,
                'skipped',
                'empty_row',
                is_array($row) ? $row : [],
                $columnLabels
            );
            continue;
        }

        $manufacturer = '';
        if ($cols['manufacturer'] > 0) {
            $manufacturer = epc_clip(trim(epc_prepare_string((string)($row[$cols['manufacturer'] - 1] ?? ''))));
        }

        $articleShow = '';
        $article = '';
        if ($cols['article'] > 0) {
            $articleShow = epc_clip(epc_prepare_string((string)($row[$cols['article'] - 1] ?? '')));
            $article = epc_clip(strtoupper(str_replace(
                [' ', '-', '_', '`', '/', "'", '"', '\\', '.', ',', '#', "\r\n", "\r", "\n", "\t"],
                '',
                (string)($row[$cols['article'] - 1] ?? '')
            )));
        }

        $name = '';
        if ($cols['name'] > 0) {
            $name = epc_clip(trim(epc_prepare_string((string)($row[$cols['name'] - 1] ?? ''))));
        }

        if ($article === '') {
            $skipped++;
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
                    'exist' => (string)($cols['exist'] > 0 ? ($row[$cols['exist'] - 1] ?? '') : ''),
                    'price' => (string)($cols['price'] > 0 ? ($row[$cols['price'] - 1] ?? '') : ''),
                    'details' => 'Article column value: "' . $articleShow . '"',
                ]
            );
            continue;
        }

        $exist = 0;
        if ($cols['exist'] > 0) {
            $exist = epc_parse_stock_quantity($row[$cols['exist'] - 1] ?? '');
        }

        $priceVal = 0.0;
        if ($cols['price'] > 0) {
            $rawPrice = str_replace([' ', ','], ['', '.'], (string)($row[$cols['price'] - 1] ?? '0'));
            $priceVal = (float)$rawPrice;
        }

        if ($priceVal <= 0) {
            $skipped++;
            $priceRaw = $cols['price'] > 0 ? (string)($row[$cols['price'] - 1] ?? '') : '';
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
                    'exist' => (string)($cols['exist'] > 0 ? ($row[$cols['exist'] - 1] ?? '') : ''),
                    'price' => $priceRaw,
                    'details' => 'Price column value: "' . $priceRaw . '"',
                ]
            );
            continue;
        }

        $timeToExe = 0;
        if ($cols['time_to_exe'] > 0) {
            $timeToExe = epc_parse_time_to_exe($row[$cols['time_to_exe'] - 1] ?? '');
        }

        $storage = '';
        if ($cols['storage'] > 0) {
            $storage = epc_clip(trim(epc_prepare_string((string)($row[$cols['storage'] - 1] ?? ''))));
        }

        $minOrder = 0;
        if ($cols['min_order'] > 0) {
            $minOrder = (int)preg_replace('/[^0-9]/', '', (string)($row[$cols['min_order'] - 1] ?? '0'));
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
            $manufacturer,
            $article,
            $articleShow,
            $name,
            $exist,
            $priceVal,
            $timeToExe,
            $storage,
            $minOrder
        );

        if (substr_count($valuesSql, '(') >= $batch) {
            $flush();
        }
    }

    fclose($fh);
    $flush();
    $db->exec('ALTER TABLE `shop_docpart_prices_data` ENABLE KEYS');
    $db->prepare('UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?')->execute([time(), $priceId]);

    return [
        'status' => $inserted > 0,
        'message' => $inserted > 0 ? 'Import completed' : 'No valid rows imported',
        'records_handled' => $inserted,
        'rows_skipped' => $skipped,
        'delimiter' => $delimiter,
        'import_issues' => $importIssues,
    ];
}

$priceId = (int)($_POST['price_id'] ?? 0);
$priceName = trim((string)($_POST['price_name'] ?? ''));

$price = epc_price_resolve_or_create_list($db, $priceId, $priceName);
if (!$price) {
    exit(json_encode(['status' => false, 'message' => 'Price list not found and could not be created']));
}

epc_price_link_storage_to_list($db, (string)$price['name'], (int)$price['id']);

$priceId = (int)$price['id'];
$priceName = (string)$price['name'];

$db->prepare('UPDATE `shop_docpart_prices` SET `file_name_substring` = ? WHERE `id` = ?')
    ->execute([$priceName, $priceId]);

$origName = basename((string)$_FILES['price_file']['name']);
$ext = strtolower(pathinfo($origName, PATHINFO_EXTENSION));
if (!in_array($ext, ['csv', 'txt', 'xlsx', 'xls'], true)) {
    exit(json_encode(['status' => false, 'message' => 'Unsupported file type']));
}

$tmpRoot = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . $DP_Config->tmp_dir_prices_upload;
if (!is_dir($tmpRoot)) {
    mkdir($tmpRoot, 0755, true);
}

$targetPath = $tmpRoot . '/' . $priceName . '_' . time() . '.csv';
if (!move_uploaded_file($_FILES['price_file']['tmp_name'], $targetPath)) {
    exit(json_encode(['status' => false, 'message' => 'Could not save uploaded file']));
}

$storedRel = epc_price_history_archive_file($targetPath, $priceId, $origName);
$fileSize = is_file($targetPath) ? (int)filesize($targetPath) : 0;

// Save history immediately so the file is downloadable even if import fails mid-run.
$historyId = epc_price_history_save($db, [
    'price_id' => $priceId,
    'price_name' => $priceName,
    'upload_source' => 'deploy_api',
    'original_filename' => $origName,
    'stored_relpath' => $storedRel,
    'file_size' => $fileSize,
    'status' => 'pending',
]);

$importError = '';
try {
    $result = epc_import_csv($db, $price, $targetPath);
} catch (Throwable $e) {
    $importError = $e->getMessage();
    $result = ['status' => false, 'message' => $importError, 'records_handled' => 0, 'rows_skipped' => 0, 'import_issues' => []];
}
@unlink($targetPath);

$countQ = $db->prepare('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?');
$countQ->execute([$priceId]);
$recordsInDb = (int)$countQ->fetchColumn();

$rowsSkipped = (int)($result['rows_skipped'] ?? 0);
$importIssues = is_array($result['import_issues'] ?? null) ? $result['import_issues'] : [];
$importOk = $importError === '' && !empty($result['status']);
$historyStatus = $importOk ? (($rowsSkipped > 0 || count($importIssues) > 0) ? 'partial' : 'ok') : 'failed';

$historyUpdate = [
    'rows_imported' => (int)($result['records_handled'] ?? 0),
    'rows_skipped' => $rowsSkipped,
    'rows_in_db' => $recordsInDb,
    'brands_count' => epc_price_history_count_brands($db, $priceId),
    'items_count' => $recordsInDb,
    'status' => $historyStatus,
    'error_text' => $importOk ? '' : (string)($result['message'] ?? $importError),
    'stats_json' => json_encode(['delimiter' => $result['delimiter'] ?? ''], JSON_UNESCAPED_UNICODE),
];
if ($historyId > 0) {
    if (count($importIssues) > 0 && count($importIssues) <= 5000) {
        $historyUpdate['import_issues'] = $importIssues;
    }
    epc_price_history_update_by_id($db, $historyId, $historyUpdate);
} elseif ($storedRel !== '') {
    $historyId = epc_price_history_save($db, array_merge([
        'price_id' => $priceId,
        'price_name' => $priceName,
        'upload_source' => 'deploy_api',
        'original_filename' => $origName,
        'stored_relpath' => $storedRel,
        'file_size' => $fileSize,
    ], $historyUpdate));
}

echo json_encode([
    'status' => !empty($result['status']),
    'message' => $result['message'] ?? '',
    'price_id' => $priceId,
    'price_name' => $priceName,
    'records_handled' => (int)($result['records_handled'] ?? 0),
    'rows_skipped' => (int)($result['rows_skipped'] ?? 0),
    'records_in_db' => $recordsInDb,
    'brands_count' => epc_price_history_count_brands($db, $priceId),
    'items_count' => $recordsInDb,
    'history_id' => $historyId,
    'delimiter' => $result['delimiter'] ?? '',
    'file_name_substring' => $priceName,
    'stored_file' => $storedRel,
], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
