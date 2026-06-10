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

    $stmt = $pdo->prepare("UPDATE `content` SET `content_type` = 'php', `content` = ?, `published_flag` = 1 WHERE `is_frontend` = 0 AND `url` = 'shop/logistics' LIMIT 1;");
    $stmt->execute(array('/<backend_dir>/content/shop/logistics/logistics.php'));

    echo "Updated logistics dashboard routes: " . $stmt->rowCount() . "\n";
    echo "Dashboard: /cp/shop/logistics\n";
} catch (Exception $e) {
    echo "Error: " . $e->getMessage() . "\n";
}

@unlink(__FILE__);
?>
