<?php
if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
    exit('Forbidden');
}
header('Content-Type: text/plain; charset=utf-8');

try {
    require_once __DIR__ . '/config.php';
    $cfg = new DP_Config();
    $pdo = new PDO(
        'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
        $cfg->user,
        $cfg->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
    );

    $pdo->exec("UPDATE `shop_payment_systems` SET `anable` = 1 WHERE `handler` <> '';");
    $pdo->exec("UPDATE `sms_api` SET `control_available` = 1 WHERE `handler` <> '';");
    $pdo->exec("UPDATE `shop_storages_interfaces_types` SET `control_available` = 1 WHERE `handler_folder` <> '';");

    echo "Payment systems selectable: " . (int)$pdo->query("SELECT COUNT(*) FROM `shop_payment_systems` WHERE `anable` = 1;")->fetchColumn() . "\n";
    echo "SMS operators selectable: " . (int)$pdo->query("SELECT COUNT(*) FROM `sms_api` WHERE `control_available` = 1;")->fetchColumn() . "\n";
    echo "Storage/ERP interfaces selectable: " . (int)$pdo->query("SELECT COUNT(*) FROM `shop_storages_interfaces_types` WHERE `control_available` = 1;")->fetchColumn() . "\n";
    echo "Done\n";
} catch (Exception $e) {
    echo "Error: " . $e->getMessage() . "\n";
}

@unlink(__FILE__);
?>
