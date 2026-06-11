<?php
/**
 * One-off: give the ecomae demo HR records realistic multi-year tenures so the
 * country-aware end-of-service gratuity / leave figures render with real values.
 * Token-gated, ecomae-only (host-resolved DB). Idempotent.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
@require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
@require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
header('Content-Type: text/plain; charset=utf-8');

$cfg = new DP_Config();
if (function_exists('epc_portal_apply_config')) {
    epc_portal_apply_config($cfg);
}
try {
    $pdo = new PDO(
        'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
        $cfg->user,
        $cfg->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
} catch (PDOException $e) {
    exit('DB connect failed: ' . $e->getMessage() . "\n");
}

echo "DB: {$cfg->db} @ {$cfg->host}\n";

try {
    $rows = $pdo->query('SELECT `id` FROM `epc_erp_hr_records` ORDER BY `id`')->fetchAll(PDO::FETCH_COLUMN);
} catch (PDOException $e) {
    exit('No epc_erp_hr_records table: ' . $e->getMessage() . "\n");
}
echo 'HR records: ' . count($rows) . "\n";

$tenures = array(7.5, 6.2, 4.8, 3.5, 9.1, 2.3, 5.6, 1.4, 8.0, 0.7, 11.0, 3.0, 12.5, 4.0);
$now = time();
$up = $pdo->prepare('UPDATE `epc_erp_hr_records` SET `hire_date` = ? WHERE `id` = ?');
$i = 0;
foreach ($rows as $id) {
    $yrs = $tenures[$i % count($tenures)];
    $hire = $now - (int) round($yrs * 31557600);
    $up->execute(array($hire, (int) $id));
    echo '  #' . (int) $id . ' -> ' . number_format($yrs, 1) . ' yr (' . date('Y-m-d', $hire) . ")\n";
    $i++;
}
echo "Done.\n";
