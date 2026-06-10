<?php
header('Content-Type: application/json; charset=utf-8');
if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
    http_response_code(403);
    echo json_encode(array('error' => 'Forbidden'));
    exit;
}

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
$pdo = new PDO(
    'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
    $cfg->user,
    $cfg->password,
    array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
);

$configPath = __DIR__ . '/config.php';
$config = file_get_contents($configPath);
$insert = '';
if (strpos($config, '$epc_contact_phone') === false) {
    $insert .= "\tpublic \$epc_contact_phone = '+971-567607011';/*Frontend phone number*/\n";
}
if (strpos($config, '$epc_whatsapp_number') === false) {
    $insert .= "\tpublic \$epc_whatsapp_number = '+971-567607011';/*Frontend WhatsApp number*/\n";
}
if ($insert !== '') {
    $config = preg_replace("/\n}\s*\?>\s*$/", "\n" . $insert . "}\n?>", $config);
    file_put_contents($configPath, $config);
}

$items = array(
    array('epc_contact_phone', 'Frontend phone number', '+971-567607011', 24),
    array('epc_whatsapp_number', 'Frontend WhatsApp number', '+971-567607011', 25),
);
$stmt = $pdo->prepare("INSERT INTO `config_items` (`config_group`, `name`, `caption`, `type`, `options`, `order`, `hint`, `visible`, `default_value`, `htmlentities`)
    VALUES (1, ?, ?, 'text', '', ?, '0', 1, ?, 0)
    ON DUPLICATE KEY UPDATE `config_group` = VALUES(`config_group`), `caption` = VALUES(`caption`), `type` = VALUES(`type`), `order` = VALUES(`order`), `visible` = 1, `default_value` = VALUES(`default_value`);");
foreach ($items as $item) {
    $stmt->execute(array($item[0], $item[1], $item[3], $item[2]));
}

if (isset($_GET['delete'])) {
    @unlink(__FILE__);
}
echo json_encode(array('ok' => true, 'settings' => array('epc_contact_phone', 'epc_whatsapp_number')));
