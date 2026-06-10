<?php
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

@set_time_limit(0);
@ini_set('memory_limit', '1536M');

$backupDir = __DIR__ . '/.epc-backups';
if (!is_dir($backupDir)) {
    if (!@mkdir($backupDir, 0700, true) && !is_dir($backupDir)) {
        $backupDir = sys_get_temp_dir() . '/epartscart-production-backups';
        if (!is_dir($backupDir)) {
            @mkdir($backupDir, 0700, true);
        }
    }
}

function epc_backup_json($payload, $status = 200)
{
    http_response_code($status);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode($payload, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

function epc_backup_safe_name($name)
{
    return preg_replace('/[^a-zA-Z0-9_.-]/', '', (string)$name);
}

function epc_backup_sql_value($pdo, $value)
{
    if ($value === null) {
        return 'NULL';
    }
    return $pdo->quote((string)$value);
}

function epc_backup_table_stats($pdo)
{
    $stats = array();
    $tables = $pdo->query('SHOW TABLES')->fetchAll(PDO::FETCH_NUM);
    foreach ($tables as $tableRow) {
        $table = $tableRow[0];
        $quotedTable = '`' . str_replace('`', '``', $table) . '`';
        $count = (int)$pdo->query('SELECT COUNT(*) FROM ' . $quotedTable)->fetchColumn();
        $stats[] = array('table' => $table, 'rows' => $count);
    }
    usort($stats, function ($a, $b) {
        return strcmp($a['table'], $b['table']);
    });
    return $stats;
}

function epc_backup_dump_database($pdo, $sqlPath)
{
    $fh = fopen($sqlPath, 'wb');
    if (!$fh) {
        throw new Exception('Cannot create SQL dump file');
    }

    fwrite($fh, "-- EpartsCart production database backup\n");
    fwrite($fh, "-- Created: " . gmdate('c') . "\n");
    fwrite($fh, "SET SQL_MODE = \"NO_AUTO_VALUE_ON_ZERO\";\n");
    fwrite($fh, "SET time_zone = \"+00:00\";\n");
    fwrite($fh, "SET FOREIGN_KEY_CHECKS = 0;\n\n");

    $tables = $pdo->query('SHOW TABLES')->fetchAll(PDO::FETCH_NUM);
    foreach ($tables as $tableRow) {
        $table = $tableRow[0];
        $quotedTable = '`' . str_replace('`', '``', $table) . '`';
        $createRow = $pdo->query('SHOW CREATE TABLE ' . $quotedTable)->fetch(PDO::FETCH_ASSOC);
        $createSql = isset($createRow['Create Table']) ? $createRow['Create Table'] : array_values($createRow)[1];

        fwrite($fh, "\n-- Table structure for {$quotedTable}\n");
        fwrite($fh, "DROP TABLE IF EXISTS {$quotedTable};\n");
        fwrite($fh, $createSql . ";\n\n");
        fwrite($fh, "-- Data for {$quotedTable}\n");

        $stmt = $pdo->query('SELECT * FROM ' . $quotedTable);
        $columns = array();
        for ($i = 0; $i < $stmt->columnCount(); $i++) {
            $meta = $stmt->getColumnMeta($i);
            $columns[] = '`' . str_replace('`', '``', $meta['name']) . '`';
        }
        $columnSql = implode(',', $columns);
        $batch = array();
        $batchSize = 50;
        while ($row = $stmt->fetch(PDO::FETCH_NUM)) {
            $values = array();
            foreach ($row as $value) {
                $values[] = epc_backup_sql_value($pdo, $value);
            }
            $batch[] = '(' . implode(',', $values) . ')';
            if (count($batch) >= $batchSize) {
                fwrite($fh, "INSERT INTO {$quotedTable} ({$columnSql}) VALUES\n" . implode(",\n", $batch) . ";\n");
                $batch = array();
            }
        }
        if (count($batch) > 0) {
            fwrite($fh, "INSERT INTO {$quotedTable} ({$columnSql}) VALUES\n" . implode(",\n", $batch) . ";\n");
        }
        fwrite($fh, "\n");
    }

    fwrite($fh, "SET FOREIGN_KEY_CHECKS = 1;\n");
    fclose($fh);
}

function epc_backup_add_dir($zip, $root, $dir, $exclude, &$fileCount, &$byteCount)
{
    $items = scandir($dir);
    if ($items === false) {
        return;
    }
    foreach ($items as $item) {
        if ($item === '.' || $item === '..') {
            continue;
        }
        $path = $dir . DIRECTORY_SEPARATOR . $item;
        $relative = ltrim(str_replace('\\', '/', substr($path, strlen($root))), '/');
        foreach ($exclude as $skip) {
            if ($relative === $skip || strpos($relative, rtrim($skip, '/') . '/') === 0) {
                continue 2;
            }
        }
        if (is_dir($path)) {
            $zip->addEmptyDir('site/' . $relative);
            epc_backup_add_dir($zip, $root, $path, $exclude, $fileCount, $byteCount);
        } else if (is_file($path)) {
            $zip->addFile($path, 'site/' . $relative);
            $fileCount++;
            $byteCount += (int)filesize($path);
        }
    }
}

function epc_backup_restore_instructions($cfg, $stamp, $tableStats, $fileCount, $sqlBytes)
{
    $lines = array();
    $lines[] = 'EpartsCart — disaster recovery restore guide';
    $lines[] = 'Backup stamp (UTC): ' . $stamp;
    $lines[] = '';
    $lines[] = 'This archive contains:';
    $lines[] = '  site/                  Full production document root (' . $fileCount . ' files)';
    $lines[] = '  database.sql           MySQL dump (' . number_format($sqlBytes) . ' bytes)';
    $lines[] = '  manifest.txt           Environment and table statistics';
    $lines[] = '  restore-instructions.txt  This file';
    $lines[] = '';
    $lines[] = 'Recorded environment:';
    $lines[] = '  Domain: ' . (isset($cfg->domain_path) ? $cfg->domain_path : '');
    $lines[] = '  DB host: ' . (isset($cfg->host) ? $cfg->host : '');
    $lines[] = '  DB name: ' . (isset($cfg->db) ? $cfg->db : '');
    $lines[] = '  DB user: ' . (isset($cfg->user) ? $cfg->user : '');
    $lines[] = '  PHP: ' . PHP_VERSION;
    $lines[] = '';
    $lines[] = 'Database tables (' . count($tableStats) . '):';
    foreach ($tableStats as $row) {
        $lines[] = '  ' . $row['table'] . ': ' . number_format($row['rows']) . ' rows';
    }
    $lines[] = '';
    $lines[] = 'Restore steps:';
    $lines[] = '1. Provision new hosting with PHP 7.4+ (match current if possible), MySQL/MariaDB, mod_rewrite.';
    $lines[] = '2. Create empty MySQL database and user with full privileges.';
    $lines[] = '3. Import database.sql: mysql -u USER -p DBNAME < database.sql';
    $lines[] = '4. Upload all files from site/ to the new document root (preserve permissions).';
    $lines[] = '5. Edit site/config.php — update host, db, user, password, domain_path if changed.';
    $lines[] = '6. Point DNS (www.epartscart.com) to the new server; wait for SSL certificate.';
    $lines[] = '7. Test: homepage, part search, cart/checkout, CP login, orders, payments, WhatsApp links.';
    $lines[] = '8. Re-run setup scripts if needed (channels, logistics, WhatsApp phase 2) using production-backup.php token URLs.';
    $lines[] = '';
    $lines[] = 'Security: config.php and database.sql contain secrets. Store this ZIP offline and encrypted.';
    return implode("\n", $lines) . "\n";
}

$mode = isset($_GET['mode']) ? $_GET['mode'] : 'create';

if ($mode === 'list') {
    $files = array();
    foreach (glob($backupDir . '/modelc-*-backup-*.zip') ?: array() as $path) {
        $files[] = array(
            'file' => basename($path),
            'size' => filesize($path),
            'sha256' => hash_file('sha256', $path),
            'modified_utc' => gmdate('c', filemtime($path)),
        );
    }
    foreach (glob($backupDir . '/epartscart-production-backup-*.zip') ?: array() as $path) {
        $files[] = array(
            'file' => basename($path),
            'size' => filesize($path),
            'sha256' => hash_file('sha256', $path),
            'modified_utc' => gmdate('c', filemtime($path)),
        );
    }
    usort($files, function ($a, $b) {
        return strcmp($b['file'], $a['file']);
    });
    epc_backup_json(array('ok' => true, 'backups' => $files, 'dir' => $backupDir));
}

if ($mode === 'download') {
    $file = epc_backup_safe_name(isset($_GET['file']) ? $_GET['file'] : '');
    $path = $backupDir . '/' . $file;
    if ($file === '' || !is_file($path)) {
        http_response_code(404);
        exit('Backup file not found');
    }
    header('Content-Type: application/zip');
    header('Content-Length: ' . filesize($path));
    header('Content-Disposition: attachment; filename="' . basename($path) . '"');
    readfile($path);
    exit;
}

if ($mode === 'delete') {
    $file = epc_backup_safe_name(isset($_GET['file']) ? $_GET['file'] : '');
    $path = $backupDir . '/' . $file;
    $deleted = false;
    if ($file !== '' && is_file($path)) {
        $deleted = @unlink($path);
    }
    epc_backup_json(array('ok' => true, 'deleted' => $deleted, 'file' => $file));
}

try {
    if (!class_exists('ZipArchive')) {
        throw new Exception('ZipArchive extension is not available');
    }
    require_once __DIR__ . '/config.php';
    $cfg = new DP_Config();
    $portalFile = __DIR__ . '/content/general_pages/epc_portal.php';
    if (is_file($portalFile)) {
        require_once $portalFile;
        if (function_exists('epc_portal_apply_config')) {
            epc_portal_apply_config($cfg);
        }
    }

    $scope = isset($_GET['scope']) ? strtolower((string) $_GET['scope']) : 'full';
    if ($scope !== 'database' && $scope !== 'full') {
        $scope = 'full';
    }

    $host = isset($_SERVER['HTTP_HOST']) ? strtolower((string) $_SERVER['HTTP_HOST']) : 'unknown';
    $systemLabel = 'site';
    if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()) {
        $systemLabel = 'platform-ecomae-super-cp';
    } elseif (function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname()) {
        $systemLabel = 'client-' . preg_replace('/[^a-z0-9_-]/', '', strtolower((string) $cfg->db));
    } else {
        $systemLabel = preg_replace('/[^a-z0-9_-]/', '', str_replace('.', '-', $host));
    }

    $pdo = new PDO(
        'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
        $cfg->user,
        $cfg->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
    );

    $stamp = gmdate('Ymd-His');
    $baseName = 'modelc-' . $systemLabel . '-backup-' . $stamp;
    $zipPath = $backupDir . '/' . $baseName . '.zip';
    $sqlPath = $backupDir . '/' . $baseName . '-database.sql';
    $manifestPath = $backupDir . '/' . $baseName . '-manifest.txt';
    $restorePath = $backupDir . '/' . $baseName . '-restore.txt';

    $tableStats = epc_backup_table_stats($pdo);
    epc_backup_dump_database($pdo, $sqlPath);
    $sqlBytes = (int)filesize($sqlPath);

    $mysqlVersion = (string)$pdo->query('SELECT VERSION()')->fetchColumn();
    $manifest = "Model C system backup\n";
    $manifest .= "System label: " . $systemLabel . "\n";
    $manifest .= "HTTP host: " . $host . "\n";
    $manifest .= "Scope: " . $scope . "\n";
    $manifest .= "Created UTC: " . gmdate('c') . "\n";
    $manifest .= "Domain: " . (isset($cfg->domain_path) ? $cfg->domain_path : '') . "\n";
    $manifest .= "Document root: " . __DIR__ . "\n";
    $manifest .= "Database host: " . (isset($cfg->host) ? $cfg->host : '') . "\n";
    $manifest .= "Database name: " . (isset($cfg->db) ? $cfg->db : '') . "\n";
    $manifest .= "Database user: " . (isset($cfg->user) ? $cfg->user : '') . "\n";
    $manifest .= "MySQL version: " . $mysqlVersion . "\n";
    $manifest .= "PHP version: " . PHP_VERSION . "\n";
    $manifest .= "SQL dump bytes: " . $sqlBytes . "\n";
    $manifest .= "Table count: " . count($tableStats) . "\n\n";
    $manifest .= "Table row counts:\n";
    foreach ($tableStats as $row) {
        $manifest .= $row['table'] . "\t" . $row['rows'] . "\n";
    }
    file_put_contents($manifestPath, $manifest);

    $fileCount = 0;
    $byteCount = 0;
    $zip = new ZipArchive();
    if ($zip->open($zipPath, ZipArchive::CREATE | ZipArchive::OVERWRITE) !== true) {
        throw new Exception('Cannot create backup zip');
    }
    if ($scope === 'full') {
        $exclude = array(
            'epc-production-backup.php',
            'production-backup.php',
        );
        epc_backup_add_dir($zip, __DIR__, __DIR__, $exclude, $fileCount, $byteCount);
    }
    $restoreText = epc_backup_restore_instructions($cfg, $stamp, $tableStats, $fileCount, $sqlBytes);
    $restoreText = str_replace('EpartsCart — disaster recovery restore guide', 'Model C — ' . $systemLabel . ' restore guide', $restoreText);
    if ($scope === 'database') {
        $restoreText .= "\nNote: database-only backup — reuse site files from the client full backup (same docroot on Model C).\n";
    }
    file_put_contents($restorePath, $restoreText);
    $zip->addFile($sqlPath, 'database.sql');
    $zip->addFile($manifestPath, 'manifest.txt');
    $zip->addFile($restorePath, 'restore-instructions.txt');
    $zip->close();

    @unlink($sqlPath);
    @unlink($manifestPath);
    @unlink($restorePath);

    epc_backup_json(array(
        'ok' => true,
        'file' => basename($zipPath),
        'system_label' => $systemLabel,
        'scope' => $scope,
        'http_host' => $host,
        'database_name' => isset($cfg->db) ? $cfg->db : '',
        'size' => filesize($zipPath),
        'sha256' => hash_file('sha256', $zipPath),
        'created_utc' => gmdate('c'),
        'site_files' => $fileCount,
        'site_bytes' => $byteCount,
        'sql_bytes' => $sqlBytes,
        'table_count' => count($tableStats),
        'mysql_version' => $mysqlVersion,
        'php_version' => PHP_VERSION,
        'server_path' => $zipPath,
        'download_url' => rtrim($cfg->domain_path, '/') . '/production-backup.php?token=REDACTED&mode=download&file=' . basename($zipPath),
    ));
} catch (Exception $e) {
    epc_backup_json(array('ok' => false, 'error' => $e->getMessage()), 500);
}
