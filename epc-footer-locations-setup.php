<?php
if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
    exit('Forbidden');
}
header('Content-Type: text/plain; charset=utf-8');

try {
    require_once __DIR__ . '/config.php';
    $cfg = new DP_Config();
    $pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
} catch (Exception $e) {
    echo "DB connection error: " . $e->getMessage() . "\n";
    exit;
}

$configPath = __DIR__ . '/config.php';
$config = file_get_contents($configPath);
$props = array(
    "public \$epc_head_office_title = 'Head Office';/*Footer head office title*/",
    "public \$epc_head_office_address = 'Dubai, United Arab Emirates';/*Footer head office address*/",
    "public \$epc_head_office_email = 'partsdoc2025@gmail.com';/*Footer head office email*/",
    "public \$epc_head_office_map_url = '';/*Footer head office map URL*/",
    "public \$epc_global_locations_summary = '15 countries, multiple locations';/*Footer global locations summary*/",
    "public \$epc_global_locations_countries = '';/*Footer global countries and locations*/",
    "public \$epc_global_locations_map_url = '';/*Footer global locations map URL*/",
);
$changed = false;
foreach ($props as $prop) {
    if (preg_match('/public\s+\$([a-zA-Z0-9_]+)/', $prop, $match) && strpos($config, 'public $' . $match[1]) === false) {
        $inserted = false;
        if ($match[1] === 'epc_head_office_address' || $match[1] === 'epc_head_office_email') {
            $config_new = preg_replace('/(\tpublic \$epc_head_office_title[^\n]*\n)/', '$1' . "\t" . $prop . "\n", $config, 1);
            $inserted = $config_new !== null && $config_new !== $config;
            if ($inserted) {
                $config = $config_new;
            }
        }
        if (!$inserted && strpos($config, "public \$epc_whatsapp_number") !== false) {
            $config_new = preg_replace('/(\tpublic \$epc_whatsapp_number[^\n]*\n)/', '$1' . "\t" . $prop . "\n", $config, 1);
            $inserted = $config_new !== null && $config_new !== $config;
            if ($inserted) {
                $config = $config_new;
            }
        }
        if (!$inserted) {
            $config = preg_replace('/(\n\})\s*\?>\s*$/', "\n\t" . $prop . '$1' . "\n?>", $config, 1);
        }
        $changed = true;
        echo "Added " . $match[1] . " to config.php\n";
    }
}
if ($changed) {
    file_put_contents($configPath, $config);
}

$items = array(
    array('epc_contact_phone', 'Frontend phone number', '+971-567607011', 24, 'text'),
    array('epc_whatsapp_number', 'Frontend WhatsApp number', '+971-567607011', 25, 'text'),
    array('epc_head_office_title', 'Footer head office title', 'Head Office', 26, 'text'),
    array('epc_head_office_address', 'Footer head office address', 'Dubai, United Arab Emirates', 27, 'textarea'),
    array('epc_head_office_email', 'Footer head office email', 'partsdoc2025@gmail.com', 28, 'text'),
    array('epc_head_office_map_url', 'Footer head office map URL', '', 29, 'text'),
    array('epc_global_locations_summary', 'Footer global locations summary', '15 countries, multiple locations', 30, 'text'),
    array('epc_global_locations_countries', 'Footer countries / locations text', '', 31, 'textarea'),
    array('epc_global_locations_map_url', 'Footer global locations map URL', '', 32, 'text'),
);

try {
    $stmt = $pdo->prepare("INSERT INTO `config_items` (`config_group`, `name`, `caption`, `type`, `options`, `order`, `hint`, `visible`, `default_value`, `htmlentities`) VALUES (1, ?, ?, ?, '', ?, '0', 1, ?, 0) ON DUPLICATE KEY UPDATE `config_group` = VALUES(`config_group`), `caption` = VALUES(`caption`), `type` = VALUES(`type`), `order` = VALUES(`order`), `visible` = 1, `default_value` = VALUES(`default_value`);");
    foreach ($items as $item) {
        $stmt->execute(array($item[0], $item[1], $item[4], $item[3], $item[2]));
        echo "Configured " . $item[0] . "\n";
    }
} catch (Exception $e) {
    echo "Config item error: " . $e->getMessage() . "\n";
    exit;
}

@unlink(__FILE__);
echo "Done\n";
?>
