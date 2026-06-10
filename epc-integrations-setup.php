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
} catch (Exception $e) {
    echo "DB connection error: " . $e->getMessage() . "\n";
    exit;
}

function epc_run_seed($pdo, $path, $label)
{
    if (!is_file($path)) {
        echo $label . " seed missing\n";
        return;
    }
    $sql = file_get_contents($path);
    if ($sql === false || trim($sql) === '') {
        echo $label . " seed empty\n";
        return;
    }
    $pdo->exec($sql);
    echo $label . " seed applied\n";
}

function epc_count_table($pdo, $table)
{
    try {
        return (int)$pdo->query("SELECT COUNT(*) FROM `" . str_replace('`', '``', $table) . "`;")->fetchColumn();
    } catch (Exception $e) {
        return 0;
    }
}

$pdo->exec("CREATE TABLE IF NOT EXISTS `shop_payment_systems` (
    `id` int(11) NOT NULL AUTO_INCREMENT,
    `name` text,
    `parameters` text,
    `parameters_values` text,
    `active` tinyint(1) DEFAULT 0,
    `description` text,
    `is_selectable` tinyint(1) DEFAULT 0,
    `handler` varchar(255) DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

$pdo->exec("CREATE TABLE IF NOT EXISTS `sms_api` (
    `id` int(11) NOT NULL AUTO_INCREMENT COMMENT 'ID',
    `name` text COMMENT 'Name',
    `parameters` text COMMENT 'Parameters description',
    `parameters_values` text COMMENT 'Parameter values',
    `description` text COMMENT 'Description',
    `active` tinyint(1) DEFAULT 0,
    `handler` varchar(255) DEFAULT NULL,
    `is_selectable` tinyint(1) DEFAULT 0,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

$pdo->exec("CREATE TABLE IF NOT EXISTS `shop_storages_interfaces_types` (
    `id` int(11) NOT NULL AUTO_INCREMENT COMMENT 'ID',
    `product_type` int(11) DEFAULT NULL,
    `name` text,
    `description` text,
    `parameters` text,
    `parameters_values` text,
    `is_selectable` tinyint(1) DEFAULT 1,
    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

epc_run_seed($pdo, __DIR__ . '/epc-shop_payment_systems-seed.sql', 'Payment systems');
epc_run_seed($pdo, __DIR__ . '/epc-sms_api-seed.sql', 'SMS operators');
epc_run_seed($pdo, __DIR__ . '/epc-shop_storages_interfaces_types-seed.sql', 'Storage/ERP interfaces');

$pdo->exec("UPDATE `shop_payment_systems` SET `is_selectable` = 1 WHERE `handler` <> '';");
$pdo->exec("UPDATE `sms_api` SET `is_selectable` = 1 WHERE `handler` <> '';");
$pdo->exec("UPDATE `shop_storages_interfaces_types` SET `is_selectable` = 1 WHERE `name` <> '';");

echo "Payment systems total: " . epc_count_table($pdo, 'shop_payment_systems') . "\n";
echo "SMS operators total: " . epc_count_table($pdo, 'sms_api') . "\n";
echo "Storage/ERP interface types total: " . epc_count_table($pdo, 'shop_storages_interfaces_types') . "\n";

@unlink(__DIR__ . '/epc-shop_payment_systems-seed.sql');
@unlink(__DIR__ . '/epc-sms_api-seed.sql');
@unlink(__DIR__ . '/epc-shop_storages_interfaces_types-seed.sql');
@unlink(__FILE__);
echo "Done\n";
?>
