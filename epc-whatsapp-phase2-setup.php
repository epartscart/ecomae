<?php
/**
 * WhatsApp Phase 2 — Cloud API config, notify log table, SMS bodies for order WA.
 * Run: https://www.epartscart.com/epc-whatsapp-phase2-setup.php?token=epartscart-deploy-2026
 * Test send: &test=1&phone=971567607011
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$configPath = __DIR__ . '/config.php';
$config = file_get_contents($configPath);
$configKeys = array(
	'epc_whatsapp_api_enabled' => "\tpublic \$epc_whatsapp_api_enabled = '0';/*WhatsApp Cloud API automated notifications*/\n",
	'epc_whatsapp_api_token' => "\tpublic \$epc_whatsapp_api_token = '';/*Meta WhatsApp permanent access token*/\n",
	'epc_whatsapp_phone_number_id' => "\tpublic \$epc_whatsapp_phone_number_id = '';/*WhatsApp Business phone_number_id*/\n",
	'epc_whatsapp_api_version' => "\tpublic \$epc_whatsapp_api_version = 'v21.0';/*Graph API version*/\n",
	'epc_whatsapp_notify_names' => "\tpublic \$epc_whatsapp_notify_names = 'new_order_to_user,new_order_to_manager,order_status_to_customer,order_status_to_manager,order_message_to_customer,order_message_to_manager,epc_customer_login';/*Notify names for WA*/\n",
	'epc_whatsapp_bilingual_notify' => "\tpublic \$epc_whatsapp_bilingual_notify = '1';/*Append Arabic line to automated WA*/\n",
);
$configAdded = array();
foreach ($configKeys as $key => $line) {
	if (strpos($config, '$' . $key) === false) {
		$configAdded[] = $key;
	}
}
if (!empty($configAdded)) {
	$insert = '';
	foreach ($configAdded as $key) {
		$insert .= $configKeys[$key];
	}
	$config = preg_replace("/\n}\s*\?>\s*$/", "\n" . $insert . "}\n?>", $config);
	file_put_contents($configPath, $config);
}

$cfgItems = array(
	array('epc_whatsapp_api_enabled', 'WhatsApp API automated notifications', '0', 26, '1=send order/status WA when token configured'),
	array('epc_whatsapp_api_token', 'WhatsApp Cloud API token', '', 27, 'Meta Business → WhatsApp → API setup'),
	array('epc_whatsapp_phone_number_id', 'WhatsApp phone_number_id', '', 28, 'From Meta developer console'),
	array('epc_whatsapp_api_version', 'WhatsApp Graph API version', 'v21.0', 29, ''),
	array('epc_whatsapp_notify_names', 'WhatsApp notify event names', 'new_order_to_user,new_order_to_manager,order_status_to_customer,order_status_to_manager,order_message_to_customer,order_message_to_manager,epc_customer_login', 30, 'Comma-separated notifications_settings.name'),
	array('epc_whatsapp_bilingual_notify', 'WhatsApp bilingual notify (EN+AR)', '1', 31, ''),
);
$stmt = $pdo->prepare(
	"INSERT INTO `config_items` (`config_group`, `name`, `caption`, `type`, `options`, `order`, `hint`, `visible`, `default_value`, `htmlentities`)
	 VALUES (1, ?, ?, 'text', '', ?, ?, 1, ?, 0)
	 ON DUPLICATE KEY UPDATE `caption` = VALUES(`caption`), `order` = VALUES(`order`), `hint` = VALUES(`hint`), `visible` = 1"
);
foreach ($cfgItems as $item) {
	$stmt->execute(array($item[0], $item[1], $item[3], $item[4], $item[2]));
}

$pdo->exec(
	'CREATE TABLE IF NOT EXISTS `epc_whatsapp_notify_log` (
		`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
		`created_at` INT UNSIGNED NOT NULL,
		`notify_name` VARCHAR(64) NOT NULL,
		`phone` VARCHAR(32) NOT NULL,
		`status` TINYINT(1) NOT NULL DEFAULT 0,
		`message_preview` VARCHAR(500) NOT NULL DEFAULT \'\',
		`response` TEXT,
		PRIMARY KEY (`id`),
		KEY `created_at` (`created_at`),
		KEY `notify_name` (`notify_name`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8'
);

$smsUpdates = array(
	'new_order_to_user' => 'eParts Cart: your order #%order_id% is received. We will confirm shortly.',
	'order_status_to_customer' => 'eParts Cart: order #%order_id% status updated. Check your account or reply here.',
);
foreach ($smsUpdates as $name => $sms) {
	$pdo->prepare('UPDATE `notifications_settings` SET `sms_body` = ? WHERE `name` = ? AND (`sms_body` IS NULL OR `sms_body` = \'\')')
		->execute(array($sms, $name));
}

$testResult = null;
require_once __DIR__ . '/content/notifications/epc_whatsapp_notify.php';

if (!empty($_GET['test']) && !empty($_GET['phone'])) {
	$cfg2 = new DP_Config();
	$testResult = epc_wa_api_send_text(
		(string)$_GET['phone'],
		'eParts Cart WhatsApp Phase 2 test — automated notifications are configured.',
		$cfg2
	);
}

$apiReady = epc_wa_api_enabled(new DP_Config());
$logCount = (int)$pdo->query('SELECT COUNT(*) FROM `epc_whatsapp_notify_log`')->fetchColumn();

echo json_encode(array(
	'status' => true,
	'message' => 'WhatsApp Phase 2 installed',
	'config_keys_added' => $configAdded,
	'api_ready' => $apiReady,
	'api_enabled_config' => (string)(new DP_Config())->epc_whatsapp_api_enabled,
	'notify_log_rows' => $logCount,
	'test' => $testResult,
	'urls' => array(
		'cp_config' => rtrim($cfg->domain_path, '/') . '/' . $cfg->backend_dir . '/control/config',
		'wa_guide' => rtrim($cfg->domain_path, '/') . '/' . $cfg->backend_dir . '/shop/orders/whatsapp-guide',
		'test_send' => rtrim($cfg->domain_path, '/') . '/epc-whatsapp-phase2-setup.php?token=epartscart-deploy-2026&test=1&phone=971567607011',
	),
	'hint' => 'Set epc_whatsapp_api_token + epc_whatsapp_phone_number_id in CP Configuration, then set epc_whatsapp_api_enabled=1. Until then Phase 1 wa.me buttons still work.',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
