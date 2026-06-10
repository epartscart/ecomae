<?php
declare(strict_types=1);
$token = 'epartscart-deploy-2026';
if (($_GET['token'] ?? '') !== $token) {
    http_response_code(403);
    exit('Forbidden');
}
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);
$sqlPath = '/tmp/docpart-epartscart-db.sql';
if (!is_file($sqlPath)) {
    exit('Missing docpart_db.sql in site root');
}
require __DIR__ . '/config.php';
$cfg = new DP_Config();
$mysqli = new mysqli($cfg->host, $cfg->user, $cfg->password, $cfg->db);
if ($mysqli->connect_errno) {
    exit('DB connect failed: ' . $mysqli->connect_error);
}
$sql = file_get_contents($sqlPath);
if (!$mysqli->multi_query($sql)) {
    exit('Import failed: ' . $mysqli->error);
}
do {
    if ($result = $mysqli->store_result()) {
        $result->free();
    }
} while ($mysqli->more_results() && $mysqli->next_result());
echo 'Database import OK';
