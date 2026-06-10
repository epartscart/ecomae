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

    $item = $pdo->prepare("SELECT `caption` FROM `control_items` WHERE `url` = ? LIMIT 1;");
    $item->execute(array('/<backend>/shop/finance/platezhnye-sistemy'));
    $captionId = $item->fetchColumn();

    if (!$captionId) {
        echo "Payment menu item not found\n";
        @unlink(__FILE__);
        exit;
    }

    $up = $pdo->prepare("UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?;");
    $up->execute(array('Payment gateway / Payment systems', $captionId, 'en'));
    $up->execute(array('Платежный шлюз / платежные системы', $captionId, 'ru'));

    echo "Updated caption string ID: " . $captionId . "\n";
    echo "Menu path: CP > Shop > Finance > Payment gateway / Payment systems\n";
} catch (Exception $e) {
    echo "Error: " . $e->getMessage() . "\n";
}

@unlink(__FILE__);
?>
