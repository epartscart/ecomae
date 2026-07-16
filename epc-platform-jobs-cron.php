<?php
/**
 * Platform background jobs cron worker.
 *
 * Suggested crontab (every minute):
 *   * * * * * php /var/www/ecomae/epc-platform-jobs-cron.php >/dev/null 2>&1
 *
 * Or via HTTP (if web cron is preferred):
 *   GET /epc-platform-jobs-cron.php?limit=10&key=YOUR_CRON_KEY
 *
 * Env / constants:
 *   EPC_PLATFORM_JOBS_CRON_KEY — optional shared secret for HTTP mode
 *   EPC_PLATFORM_JOBS_BATCH    — default batch size (default 10)
 */

declare(strict_types=1);

$isCli = (PHP_SAPI === 'cli' || PHP_SAPI === 'phpdbg');

if (!$isCli) {
    header('Content-Type: application/json; charset=utf-8');
    $expected = (string)(getenv('EPC_PLATFORM_JOBS_CRON_KEY') ?: (defined('EPC_PLATFORM_JOBS_CRON_KEY') ? EPC_PLATFORM_JOBS_CRON_KEY : ''));
    $got = (string)($_GET['key'] ?? $_SERVER['HTTP_X_EPC_CRON_KEY'] ?? '');
    if ($expected !== '' && !hash_equals($expected, $got)) {
        http_response_code(403);
        echo json_encode(['ok' => false, 'error' => 'Forbidden']);
        exit;
    }
}

require_once __DIR__ . '/content/general_pages/epc_platform_jobs.php';

$limit = $isCli
    ? (int)($argv[1] ?? (getenv('EPC_PLATFORM_JOBS_BATCH') ?: 10))
    : (int)($_GET['limit'] ?? (getenv('EPC_PLATFORM_JOBS_BATCH') ?: 10));
$limit = max(1, min(50, $limit));

$summary = epc_platform_jobs_run_batch($limit);

$out = [
    'ok' => true,
    'ts' => date('c'),
    'summary' => $summary,
];

if ($isCli) {
    echo json_encode($out, JSON_UNESCAPED_SLASHES) . PHP_EOL;
    exit(($summary['failed'] ?? 0) > 0 && ($summary['done'] ?? 0) === 0 ? 1 : 0);
}

echo json_encode($out, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
