<?php
/**
 * Update menu + page title strings. Safe to run anytime.
 * https://www.epartscart.com/epc-rename-vehicle-intelligence.php
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$keys = array(
	'epc_demand_intelligence_title' => array('Country - Vehicle & Parts Intelligence AI', 'Страна — авто и запчасти (AI)'),
	'epc_menu_demand_intelligence' => array('Country - Vehicle & Parts Intelligence AI', 'Страна — авто и запчасти (AI)'),
);

$stmt = $pdo->prepare(
	'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?)
	 ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
);

foreach ($keys as $key => $labels) {
	$stmt->execute(array($key, 'en', $labels[0]));
	$stmt->execute(array($key, 'ru', $labels[1]));
	echo "Updated {$key}\n";
}

echo "Done. Menu and browser title should show Country - Vehicle & Parts Intelligence AI.\n";
