<?php
/**
 * Run connectivity / import tests for all price upload channels.
 * GET/POST: token=epartscart-deploy-2026, key=<tech_key>
 * Optional: modes=deploy,pyprices_db,cron,email,ftp,url (comma-separated; default all)
 * Optional: price_id=N — test only that list for pyprices modes
 * Optional: wait_seconds=90 — poll task completion (pyprices)
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
set_time_limit(300);

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

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_price_upload_diagnostics.php';

$modesFilter = null;
if (!empty($_GET['modes']) || !empty($_POST['modes'])) {
    $modesFilter = array_map('trim', explode(',', (string)($_GET['modes'] ?? $_POST['modes'])));
}
$onlyPriceId = (int)($_GET['price_id'] ?? $_POST['price_id'] ?? 0);
$waitSeconds = max(0, min(180, (int)($_GET['wait_seconds'] ?? $_POST['wait_seconds'] ?? 60)));

try {
    $db = new PDO(
        'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
        $DP_Config->user,
        $DP_Config->password,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
    $db->query('SET NAMES utf8');
} catch (Throwable $e) {
    exit(json_encode(['status' => false, 'message' => 'DB error', 'error' => $e->getMessage()]));
}

$config = [
    'backend_dir' => $DP_Config->backend_dir,
    'domain_path' => $DP_Config->domain_path,
    'tech_key' => $DP_Config->tech_key,
    'tmp_dir_prices_upload' => $DP_Config->tmp_dir_prices_upload,
];

$report = [
    'status' => true,
    'tested_at' => date('c'),
    'site' => $DP_Config->domain_path,
    'tests' => [],
    'price_lists' => [],
    'summary' => ['passed' => 0, 'failed' => 0, 'skipped' => 0],
];

function epc_test_should_run(?array $filter, string $mode): bool
{
    return $filter === null || in_array($mode, $filter, true);
}

function epc_test_record(array &$report, string $name, string $result, array $extra = []): void
{
    $entry = array_merge(['name' => $name, 'result' => $result], $extra);
    $report['tests'][] = $entry;
    if ($result === 'pass') {
        $report['summary']['passed']++;
    } elseif ($result === 'skip') {
        $report['summary']['skipped']++;
    } else {
        $report['summary']['failed']++;
    }
}

function epc_build_pyprices_task(array $price, string $source, PDO $db): ?array
{
    $task = [
        'price_id' => (int)$price['id'],
        'price_name' => (string)$price['name'],
        'file_name_substring' => (string)$price['file_name_substring'],
        'file_name_substring_arch' => (string)$price['file_name_substring_arch'],
        'file_encoding' => (string)$price['encoding'],
        'cols_delimiter' => str_replace('\t', "\t", (string)$price['separator']),
        'clear_old_records' => (int)$price['clean_before'],
        'rows_per_query' => 1000,
        'col_name' => (int)$price['name_col'],
        'col_article' => (int)$price['article_col'],
        'col_manufacturer' => (int)$price['manufacturer_col'],
        'col_price' => (int)$price['price_col'],
        'col_exist' => (int)$price['exist_col'],
        'col_storage' => (int)$price['storage_col'],
        'col_min_order' => (int)$price['min_order_col'],
        'col_time_to_exe' => (int)$price['time_to_exe_col'],
        'cols_to_left' => (int)$price['strings_to_left'],
        'source' => $source,
        'completed' => false,
    ];

    if ($source === 'email') {
        $task['email_price_sender'] = (string)$price['sender_email'];
        $task['email_message_header_substring'] = (string)$price['message_header_substring'];
        $task['not_mark_seen_email_messages'] = (int)$price['not_mark_seen_email_messages'];
    } elseif ($source === 'ftp') {
        $task['ftp_host'] = (string)$price['ftp_host'];
        $task['ftp_username'] = (string)$price['ftp_user'];
        $task['ftp_password'] = (string)$price['ftp_password'];
        $task['ftp_folder'] = (string)$price['ftp_folder'];
    } elseif ($source === 'url') {
        $task['url'] = (string)$price['link'];
    } else {
        return null;
    }

    $ins = $db->prepare('INSERT INTO `shop_docpart_pyprices_tasks` (`time_created`, `price_id`) VALUES (?, ?)');
    if (!$ins->execute([time(), (int)$price['id']])) {
        return null;
    }
    $taskId = (int)$db->lastInsertId();
    if ($taskId <= 0) {
        return null;
    }
    $task['client_task_id'] = $taskId;
    return $task;
}

function epc_call_pyprices(array $config, array $listToHandle, int $timeout = 120): array
{
    $postdata = http_build_query([
        'key' => $config['tech_key'],
        'list_to_handle' => json_encode($listToHandle, JSON_UNESCAPED_UNICODE),
    ]);
    $ch = curl_init(rtrim($config['domain_path'], '/') . '/pyprices/pyprices-api.php');
    curl_setopt_array($ch, [
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_POST => true,
        CURLOPT_POSTFIELDS => $postdata,
        CURLOPT_TIMEOUT => $timeout,
        CURLOPT_SSL_VERIFYHOST => 0,
        CURLOPT_SSL_VERIFYPEER => 0,
        CURLOPT_FOLLOWLOCATION => true,
    ]);
    $body = (string)curl_exec($ch);
    $code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
    $err = curl_error($ch);
    curl_close($ch);
    $json = json_decode(trim($body), true);
    return [
        'http_code' => $code,
        'curl_error' => $err,
        'body_preview' => substr($body, 0, 800),
        'json' => is_array($json) ? $json : null,
    ];
}

function epc_poll_task(PDO $db, int $clientTaskId, int $waitSeconds): ?array
{
    $deadline = time() + $waitSeconds;
    while (time() < $deadline) {
        $q = $db->prepare(
            'SELECT l.`id`, l.`passed`, l.`is_normal_exit`, l.`normal_exit_status`, l.`time_start`, l.`time_end`,
                    LEFT(l.`answer`, 2000) AS `answer_snip`
             FROM `shop_docpart_pyprices_tasks` t
             INNER JOIN `shop_docpart_pyprices_launches` l ON l.`id` = t.`pyprices_launche_id`
             WHERE t.`id` = ? ORDER BY l.`id` DESC LIMIT 1'
        );
        $q->execute([$clientTaskId]);
        $row = $q->fetch(PDO::FETCH_ASSOC);
        if ($row && (int)$row['passed'] === 1) {
            return $row;
        }
        sleep(3);
    }
    return null;
}

// --- Health ---
if (epc_test_should_run($modesFilter, 'health') || $modesFilter === null) {
    $health = epc_price_upload_run_health_checks($config);
    foreach ($health['checks'] as $checkName => $check) {
        epc_test_record(
            $report,
            'health_' . $checkName,
            !empty($check['ok']) ? 'pass' : 'fail',
            ['detail' => $check['detail'] ?? '']
        );
    }
}

if (epc_test_should_run($modesFilter, 'pyprices_db') || ($modesFilter === null)) {
    $pyDb = epc_price_upload_curl_json(
        rtrim($config['domain_path'], '/') . '/pyprices/pyprices-api.php',
        ['key' => $config['tech_key'], 'just_test_db' => 'yes'],
        30
    );
    epc_test_record(
        $report,
        'pyprices_database',
        !empty($pyDb['status']) ? 'pass' : 'fail',
        ['response' => $pyDb]
    );
}

if (epc_test_should_run($modesFilter, 'cron') || $modesFilter === null) {
    $cronUrl = rtrim($config['domain_path'], '/') . '/' . $config['backend_dir']
        . '/content/shop/prices_upload/for_pyprices/for_cron/cron_crutch.php?key='
        . urlencode($config['tech_key']);
    $cron = epc_price_upload_curl_raw($cronUrl, 45);
    epc_test_record(
        $report,
        'cron_crutch_wget',
        ($cron['http_code'] >= 200 && $cron['http_code'] < 500) ? 'pass' : 'fail',
        ['http_code' => $cron['http_code'], 'body' => substr($cron['body'], 0, 300)]
    );
}

// --- Price list inventory ---
$prices = [];
$sql = 'SELECT p.*, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `records_count`
        FROM `shop_docpart_prices` p';
$params = [];
if ($onlyPriceId > 0) {
    $sql .= ' WHERE p.`id` = ?';
    $params[] = $onlyPriceId;
}
$sql .= ' ORDER BY p.`name`';
$st = $db->prepare($sql);
$st->execute($params);
while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
    $prices[] = $row;
    $report['price_lists'][] = [
        'id' => (int)$row['id'],
        'name' => (string)$row['name'],
        'load_mode' => (int)$row['load_mode'],
        'records_count' => (int)$row['records_count'],
        'last_updated' => (string)$row['last_updated'],
        'has_email' => trim((string)$row['sender_email']) !== '',
        'has_ftp' => trim((string)$row['ftp_host']) !== '',
        'has_url' => trim((string)$row['link']) !== '',
    ];
}

// --- IMAP / mailbox (required for e-mail price updates) ---
if (epc_test_should_run($modesFilter, 'email') || $modesFilter === null) {
    $imapConfigured = trim((string)$DP_Config->prices_email_server) !== ''
        && trim((string)$DP_Config->prices_email_username) !== '';
    epc_test_record($report, 'email_imap_configured', $imapConfigured ? 'pass' : 'fail', [
        'prices_email_server' => (string)$DP_Config->prices_email_server,
        'prices_email_port' => (string)$DP_Config->prices_email_port,
        'prices_email_username' => (string)$DP_Config->prices_email_username,
        'detail' => $imapConfigured
            ? 'Mailbox settings present in config.php'
            : 'NOT CONFIGURED — set prices_email_server, port, username, password in CP/site config (Почта для загрузки прайс-листов)',
    ]);
    $imapTestUrl = rtrim($config['domain_path'], '/') . '/epc-test-imap.php?token='
        . urlencode($deployToken) . '&key=' . urlencode($config['tech_key']);
    $imapTestRaw = epc_price_upload_curl_raw($imapTestUrl, 30);
    $imapTest = json_decode(trim($imapTestRaw['body']), true);
    $imapOk = is_array($imapTest) && !empty($imapTest['status']);
    epc_test_record($report, 'email_imap_connection', $imapOk ? 'pass' : 'fail', [
        'detail' => is_array($imapTest) ? ($imapTest['message'] ?? '') : 'Could not run epc-test-imap.php',
        'connection_error' => $imapTest['connection']['error'] ?? null,
        'password_length' => $imapTest['config']['password_length'] ?? null,
        'settings_match_gmail' => $imapTest['settings_match_gmail'] ?? null,
        'hints' => $imapTest['hints'] ?? [],
    ]);
    $pyImapStatus = $imapTest['pyprices']['imap_status'] ?? null;
    epc_test_record($report, 'email_imap_pyprices', ($pyImapStatus === true) ? 'pass' : 'fail', [
        'imap_status' => $pyImapStatus,
        'message' => $imapTest['pyprices']['message'] ?? '',
        'note' => 'pyprices imap_status when list_to_handle is empty',
    ]);
}

// --- Deploy API (verify recent EPC-* test lists imported rows) ---
if (epc_test_should_run($modesFilter, 'deploy') || $modesFilter === null) {
    $deployQ = $db->query(
        "SELECT `id`, `name`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `cnt`
         FROM `shop_docpart_prices` p WHERE p.`name` LIKE 'EPC-%' ORDER BY p.`id` DESC LIMIT 5"
    );
    $deployRows = $deployQ->fetchAll(PDO::FETCH_ASSOC);
    $deployOk = false;
    foreach ($deployRows as $dr) {
        if ((int)$dr['cnt'] > 0) {
            $deployOk = true;
            break;
        }
    }
    epc_test_record($report, 'deploy_api_epc_upload', $deployOk ? 'pass' : 'fail', [
        'detail' => $deployOk
            ? 'Deploy API created test price list(s) with imported rows (see EPC-* lists)'
            : 'No successful EPC-* deploy test lists found — POST from external client may return HTTP 500 but import can still work',
        'recent_epc_lists' => $deployRows,
    ]);
}

// --- Pyprices file from PC (local_path) ---
if (epc_test_should_run($modesFilter, 'pyprices_pc') || $modesFilter === null) {
    $pcCandidates = array_values(array_filter($prices, static function ($p) use ($onlyPriceId) {
        if ($onlyPriceId > 0 && (int)$p['id'] !== $onlyPriceId) {
            return false;
        }
        return (int)$p['load_mode'] === 1 && (int)$p['article_col'] > 0;
    }));
    if (count($pcCandidates) === 0) {
        $pcCandidates = array_values(array_filter($prices, static fn($p) => (int)$p['load_mode'] === 1));
    }
    if (count($pcCandidates) === 0) {
        epc_test_record($report, 'pyprices_pc_local_path', 'skip', ['detail' => 'No manual load_mode price list']);
    } else {
        usort($pcCandidates, static fn($a, $b) => (int)$b['records_count'] <=> (int)$a['records_count']);
        $price = $pcCandidates[0];
        $tmpDir = $_SERVER['DOCUMENT_ROOT'] . '/' . $config['backend_dir'] . $config['tmp_dir_prices_upload']
            . '/epc_test_' . time();
        mkdir($tmpDir, 0755, true);
        $csvPath = $tmpDir . '/pc_test.csv';
        file_put_contents($csvPath, "Brand,Number,Name,Qty,Price,Delivery\nTESTPC,PC001,Pyprices PC test,2,15.50,1\n");
        $ins = $db->prepare('INSERT INTO `shop_docpart_pyprices_tasks` (`time_created`, `price_id`) VALUES (?, ?)');
        $ins->execute([time(), (int)$price['id']]);
        $clientTaskId = (int)$db->lastInsertId();
        $task = [
            'price_id' => (int)$price['id'],
            'price_name' => (string)$price['name'],
            'file_name_substring' => (string)$price['file_name_substring'],
            'file_name_substring_arch' => (string)$price['file_name_substring_arch'],
            'file_encoding' => (string)$price['encoding'],
            'cols_delimiter' => str_replace('\t', "\t", (string)$price['separator']),
            'clear_old_records' => 0,
            'rows_per_query' => 1000,
            'col_name' => (int)$price['name_col'],
            'col_article' => (int)$price['article_col'],
            'col_manufacturer' => (int)$price['manufacturer_col'],
            'col_price' => (int)$price['price_col'],
            'col_exist' => (int)$price['exist_col'],
            'col_storage' => (int)$price['storage_col'],
            'col_min_order' => (int)$price['min_order_col'],
            'col_time_to_exe' => (int)$price['time_to_exe_col'],
            'cols_to_left' => (int)$price['strings_to_left'],
            'source' => 'local_path',
            'local_path' => $csvPath,
            'del_file_from_local_path' => true,
            'completed' => false,
            'client_task_id' => $clientTaskId,
        ];
        $pyResp = epc_call_pyprices($config, [$task], 120);
        $launch = epc_poll_task($db, $clientTaskId, $waitSeconds);
        $taskOk = $launch && (int)$launch['is_normal_exit'] === 1 && (int)$launch['normal_exit_status'] === 1;
        epc_test_record($report, 'pyprices_pc_local_path', $taskOk ? 'pass' : ($pyResp['http_code'] === 200 ? 'fail' : 'fail'), [
            'price_id' => (int)$price['id'],
            'price_name' => (string)$price['name'],
            'client_task_id' => (int)$task['client_task_id'],
            'pyprices_http' => $pyResp['http_code'],
            'task_completion' => $launch,
        ]);
    }
}

// --- Pyprices per source ---
$sourceTests = [
    'email' => ['load_mode' => 3, 'check' => static fn($p) => trim((string)$p['sender_email']) !== ''],
    'ftp' => ['load_mode' => 2, 'check' => static fn($p) => trim((string)$p['ftp_host']) !== ''],
    'url' => ['load_mode' => 4, 'check' => static fn($p) => trim((string)$p['link']) !== ''],
];

foreach ($sourceTests as $source => $meta) {
    if (!epc_test_should_run($modesFilter, $source) && $modesFilter !== null) {
        continue;
    }

    $candidates = array_values(array_filter($prices, static function ($p) use ($meta, $onlyPriceId) {
        if ($onlyPriceId > 0 && (int)$p['id'] !== $onlyPriceId) {
            return false;
        }
        return (int)$p['load_mode'] === $meta['load_mode'] && $meta['check']($p);
    }));

    if (count($candidates) === 0) {
        epc_test_record($report, 'pyprices_' . $source, 'skip', [
            'detail' => 'No price list with load_mode=' . $meta['load_mode'] . ' and required settings',
        ]);
        continue;
    }

    $price = $candidates[0];
    $recordsBefore = (int)$price['records_count'];
    $task = epc_build_pyprices_task($price, $source, $db);
    if ($task === null) {
        epc_test_record($report, 'pyprices_' . $source, 'fail', [
            'price_id' => (int)$price['id'],
            'price_name' => (string)$price['name'],
            'detail' => 'Could not create shop_docpart_pyprices_tasks row',
        ]);
        continue;
    }

    $pyResp = epc_call_pyprices($config, [$task], $source === 'email' ? 180 : 120);
    $accepted = $pyResp['http_code'] === 200
        && ($pyResp['json'] === null || !isset($pyResp['json']['status']) || $pyResp['json']['status'] !== false);

    $launch = epc_poll_task($db, (int)$task['client_task_id'], $waitSeconds);

    $recordsAfter = 0;
    $rc = $db->prepare('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?');
    $rc->execute([(int)$price['id']]);
    $recordsAfter = (int)$rc->fetchColumn();

    $taskOk = false;
    $taskDetail = '';
    if ($launch) {
        $taskOk = ((int)$launch['is_normal_exit'] === 1 && (int)$launch['normal_exit_status'] === 1);
        $taskDetail = 'exit=' . $launch['is_normal_exit'] . ' status=' . $launch['normal_exit_status'];
        if (!$taskOk && !empty($launch['answer_snip'])) {
            $taskDetail .= ' answer=' . $launch['answer_snip'];
        }
    } else {
        $taskDetail = 'Task did not complete within ' . $waitSeconds . 's (may still be running)';
    }

    $imported = $recordsAfter > $recordsBefore;
    $result = 'fail';
    if ($accepted && $taskOk) {
        $result = 'pass';
    } elseif ($accepted && $launch && !$taskOk) {
        $result = 'fail';
    } elseif ($accepted && !$launch) {
        $result = 'pass'; // API accepted; long-running
        $taskDetail .= ' (pyprices accepted request)';
    }

    epc_test_record($report, 'pyprices_' . $source, $result, [
        'price_id' => (int)$price['id'],
        'price_name' => (string)$price['name'],
        'client_task_id' => (int)$task['client_task_id'],
        'sender_email' => $source === 'email' ? (string)$price['sender_email'] : null,
        'ftp_host' => $source === 'ftp' ? (string)$price['ftp_host'] : null,
        'url' => $source === 'url' ? substr((string)$price['link'], 0, 120) : null,
        'records_before' => $recordsBefore,
        'records_after' => $recordsAfter,
        'records_changed' => $recordsAfter !== $recordsBefore,
        'pyprices_http' => $pyResp['http_code'],
        'pyprices_response' => $pyResp['json'] ?? $pyResp['body_preview'],
        'task_completion' => $taskDetail,
        'launch' => $launch,
    ]);
}

$report['status'] = $report['summary']['failed'] === 0;
$report['snapshot'] = epc_price_upload_diagnostics_snapshot($db, $config);

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
