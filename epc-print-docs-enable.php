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
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
    $pdo->exec("UPDATE `shop_print_docs` SET `control_available` = 1 WHERE `name` IN ('sales_receipt', 'invoice_for_payment', 'torg_12', 'upd', 'upd_2021');");
    $count = $pdo->query("SELECT COUNT(*) FROM `shop_print_docs` WHERE `control_available` = 1;")->fetchColumn();
    echo "Enabled print documents: " . (int)$count . "\n";
} catch (Exception $e) {
    echo "Error: " . $e->getMessage() . "\n";
    exit;
}

@unlink(__FILE__);
echo "Done\n";
?>
