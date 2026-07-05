<?php
/**
 * ecomae ERP — Automated Backup Script
 *
 * Run via cron: 0 2 * * * php /path/to/backup.php
 *
 * Creates:
 *   - MySQL dump (compressed .sql.gz)
 *   - Storage files archive (.tar.gz)
 *   - Optional encryption (AES-256-CBC)
 *   - Retention management (deletes backups older than N days)
 *   - Optional remote upload (S3, FTP, or custom URL)
 */

set_time_limit(0);

$backupDir = getenv('BACKUP_DIR') ?: '/var/www/backups';
$retentionDays = (int)(getenv('BACKUP_RETENTION_DAYS') ?: 30);
$encrypt = filter_var(getenv('BACKUP_ENCRYPT') ?: 'false', FILTER_VALIDATE_BOOLEAN);
$remoteUrl = getenv('BACKUP_REMOTE_URL') ?: '';

$dbHost = getenv('DB_HOST') ?: 'db';
$dbPort = getenv('DB_PORT') ?: '3306';
$dbName = getenv('DB_DATABASE') ?: 'ecomae_erp';
$dbUser = getenv('DB_USERNAME') ?: 'ecomae';
$dbPass = getenv('DB_PASSWORD') ?: '';

$timestamp = date('Y-m-d_His');
$prefix = "ecomae-backup-{$timestamp}";

echo "[" . date('Y-m-d H:i:s') . "] Starting backup...\n";

// Ensure backup directory
if (!is_dir($backupDir)) {
    mkdir($backupDir, 0700, true);
}

// Step 1: MySQL dump
$dumpFile = "{$backupDir}/{$prefix}-db.sql.gz";
$dumpCmd = sprintf(
    'mysqldump -h %s -P %s -u %s -p%s --single-transaction --routines --triggers --events %s | gzip > %s',
    escapeshellarg($dbHost),
    escapeshellarg($dbPort),
    escapeshellarg($dbUser),
    escapeshellarg($dbPass),
    escapeshellarg($dbName),
    escapeshellarg($dumpFile)
);

$exitCode = 0;
system($dumpCmd, $exitCode);

if ($exitCode !== 0) {
    echo "[ERROR] MySQL dump failed with exit code {$exitCode}\n";
    exit(1);
}

$dbSize = filesize($dumpFile);
echo "[OK] Database dump: " . round($dbSize / (1024 * 1024), 2) . " MB\n";

// Step 2: Storage files
$storageDir = getenv('STORAGE_DIR') ?: '/var/www/storage';
$storageFile = "{$backupDir}/{$prefix}-storage.tar.gz";

if (is_dir($storageDir)) {
    $tarCmd = sprintf(
        'tar -czf %s -C %s .',
        escapeshellarg($storageFile),
        escapeshellarg($storageDir)
    );
    system($tarCmd, $exitCode);
    if ($exitCode === 0) {
        $storageSize = filesize($storageFile);
        echo "[OK] Storage archive: " . round($storageSize / (1024 * 1024), 2) . " MB\n";
    } else {
        echo "[WARN] Storage archive failed\n";
    }
}

// Step 3: Encryption
if ($encrypt) {
    $encryptionKey = getenv('BACKUP_ENCRYPTION_KEY') ?: hash('sha256', $dbPass . 'ecomae-backup');

    foreach ([$dumpFile, $storageFile] as $file) {
        if (!is_file($file)) continue;

        $encFile = $file . '.enc';
        $iv = openssl_random_pseudo_bytes(16);
        $data = file_get_contents($file);
        $encrypted = openssl_encrypt($data, 'aes-256-cbc', $encryptionKey, OPENSSL_RAW_DATA, $iv);

        file_put_contents($encFile, $iv . $encrypted);
        unlink($file);
        echo "[OK] Encrypted: " . basename($encFile) . "\n";
    }
}

// Step 4: Remote upload
if (!empty($remoteUrl)) {
    $files = glob("{$backupDir}/{$prefix}*");
    foreach ($files as $file) {
        $ch = curl_init($remoteUrl);
        curl_setopt_array($ch, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => ['file' => new CURLFile($file)],
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 600,
        ]);
        $result = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        if ($httpCode >= 200 && $httpCode < 300) {
            echo "[OK] Uploaded: " . basename($file) . "\n";
        } else {
            echo "[WARN] Upload failed for " . basename($file) . " (HTTP {$httpCode})\n";
        }
    }
}

// Step 5: Retention cleanup
$cutoff = time() - ($retentionDays * 86400);
$allBackups = glob("{$backupDir}/ecomae-backup-*");
$deleted = 0;

foreach ($allBackups as $file) {
    if (filemtime($file) < $cutoff) {
        unlink($file);
        $deleted++;
    }
}

if ($deleted > 0) {
    echo "[OK] Cleaned up {$deleted} old backup(s) (>{$retentionDays} days)\n";
}

// Step 6: Write manifest
$manifest = [
    'timestamp' => $timestamp,
    'database' => basename($dumpFile) . ($encrypt ? '.enc' : ''),
    'storage' => is_file($storageFile) ? basename($storageFile) . ($encrypt ? '.enc' : '') : null,
    'encrypted' => $encrypt,
    'size_mb' => round(($dbSize + ($storageSize ?? 0)) / (1024 * 1024), 2),
    'retention_days' => $retentionDays,
];

file_put_contents("{$backupDir}/{$prefix}-manifest.json", json_encode($manifest, JSON_PRETTY_PRINT));

echo "[" . date('Y-m-d H:i:s') . "] Backup complete: {$prefix}\n";
