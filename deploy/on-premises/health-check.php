<?php
/**
 * ecomae ERP — Health Check Script
 *
 * Run via cron: 0 *\/6 * * * php /path/to/health-check.php
 * OR called by Docker HEALTHCHECK
 *
 * Checks:
 *   - PHP is running
 *   - MySQL is reachable and responsive
 *   - Redis is reachable
 *   - Disk space above threshold
 *   - License is valid
 *   - Last backup within 48 hours
 *   - Reports to BOS if connector enabled
 */

$checks = [];
$overallHealthy = true;

// 1. PHP version
$checks['php'] = [
    'status' => 'ok',
    'version' => PHP_VERSION,
    'memory_limit' => ini_get('memory_limit'),
];

// 2. MySQL
$dbHost = getenv('DB_HOST') ?: 'db';
$dbPort = getenv('DB_PORT') ?: '3306';
$dbName = getenv('DB_DATABASE') ?: 'ecomae_erp';
$dbUser = getenv('DB_USERNAME') ?: 'ecomae';
$dbPass = getenv('DB_PASSWORD') ?: '';

try {
    $dsn = "mysql:host={$dbHost};port={$dbPort};dbname={$dbName}";
    $pdo = new PDO($dsn, $dbUser, $dbPass, [PDO::ATTR_TIMEOUT => 5]);
    $version = $pdo->query('SELECT VERSION()')->fetchColumn();
    $size = $pdo->query("SELECT ROUND(SUM(data_length + index_length) / 1024 / 1024, 2) AS size_mb FROM information_schema.tables WHERE table_schema = '{$dbName}'")->fetchColumn();
    $checks['mysql'] = ['status' => 'ok', 'version' => $version, 'size_mb' => (float)$size];
} catch (PDOException $e) {
    $checks['mysql'] = ['status' => 'error', 'error' => $e->getMessage()];
    $overallHealthy = false;
}

// 3. Redis
$redisHost = getenv('REDIS_HOST') ?: 'redis';
$redisPort = (int)(getenv('REDIS_PORT') ?: 6379);

$redis = @fsockopen($redisHost, $redisPort, $errno, $errstr, 3);
if ($redis) {
    fwrite($redis, "PING\r\n");
    $response = trim(fgets($redis));
    fclose($redis);
    $checks['redis'] = ['status' => $response === '+PONG' ? 'ok' : 'error', 'response' => $response];
    if ($response !== '+PONG') $overallHealthy = false;
} else {
    $checks['redis'] = ['status' => 'error', 'error' => "{$errstr} ({$errno})"];
    $overallHealthy = false;
}

// 4. Disk space
$freeBytes = disk_free_space('/');
$totalBytes = disk_total_space('/');
$freeGB = round($freeBytes / (1024 ** 3), 1);
$usedPercent = round((1 - ($freeBytes / $totalBytes)) * 100, 1);
$diskOk = $freeGB > 5;

$checks['disk'] = [
    'status' => $diskOk ? 'ok' : 'warning',
    'free_gb' => $freeGB,
    'used_percent' => $usedPercent,
];
if (!$diskOk) $overallHealthy = false;

// 5. License
$licensePath = ($_SERVER['DOCUMENT_ROOT'] ?? '/var/www/html') . '/deploy/on-premises/epc_license_manager.php';
if (is_file($licensePath)) {
    if (!defined('_ASTEXE_')) define('_ASTEXE_', true);
    require_once $licensePath;
    $licInfo = epc_license_info();
    $checks['license'] = [
        'status' => $licInfo['valid'] ? 'ok' : 'error',
        'tier' => $licInfo['tier'],
        'expires' => $licInfo['expires'],
        'license_status' => $licInfo['status'],
    ];
    if (!$licInfo['valid']) $overallHealthy = false;
} else {
    $checks['license'] = ['status' => 'warning', 'message' => 'License manager not found'];
}

// 6. Last backup
$backupDir = getenv('BACKUP_DIR') ?: '/var/www/backups';
$backupFiles = is_dir($backupDir) ? glob("{$backupDir}/ecomae-backup-*-manifest.json") : [];

if (!empty($backupFiles)) {
    $latestBackup = max(array_map('filemtime', $backupFiles));
    $hoursSinceBackup = (time() - $latestBackup) / 3600;
    $backupOk = $hoursSinceBackup < 48;
    $checks['backup'] = [
        'status' => $backupOk ? 'ok' : 'warning',
        'last_backup' => date('Y-m-d H:i', $latestBackup),
        'hours_ago' => round($hoursSinceBackup, 1),
    ];
} else {
    $checks['backup'] = ['status' => 'warning', 'message' => 'No backups found'];
}

// 7. System resources
$checks['system'] = [
    'status' => 'ok',
    'uptime' => is_readable('/proc/uptime') ? trim(file_get_contents('/proc/uptime')) : 'unknown',
    'load_avg' => function_exists('sys_getloadavg') ? sys_getloadavg() : null,
    'memory_usage_mb' => round(memory_get_usage(true) / (1024 * 1024), 2),
    'cpu_cores' => (int)(@shell_exec('nproc 2>/dev/null') ?: 1),
];

// Output
$result = [
    'healthy' => $overallHealthy,
    'timestamp' => date('c'),
    'hostname' => gethostname(),
    'checks' => $checks,
];

// Report to BOS if enabled
$bosUrl = getenv('BOS_CONNECTOR_URL');
$bosToken = getenv('BOS_CONNECTOR_TOKEN');
$syncMode = getenv('BOS_SYNC_MODE') ?: 'disabled';

if ($syncMode !== 'disabled' && !empty($bosUrl) && !empty($bosToken)) {
    $ch = curl_init($bosUrl . '/api/v1/on-premises/health');
    curl_setopt_array($ch, [
        CURLOPT_POST => true,
        CURLOPT_POSTFIELDS => json_encode($result),
        CURLOPT_HTTPHEADER => ['Content-Type: application/json', 'Authorization: Bearer ' . $bosToken],
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_TIMEOUT => 15,
    ]);
    curl_exec($ch);
    curl_close($ch);
}

// CLI output
if (php_sapi_name() === 'cli') {
    echo json_encode($result, JSON_PRETTY_PRINT) . "\n";
    exit($overallHealthy ? 0 : 1);
}

// HTTP output (for Docker HEALTHCHECK or monitoring)
header('Content-Type: application/json');
http_response_code($overallHealthy ? 200 : 503);
echo json_encode($result, JSON_PRETTY_PRINT);
