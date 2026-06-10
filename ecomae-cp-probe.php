<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

echo 'host=' . ($_SERVER['HTTP_HOST'] ?? '') . "\n";
echo 'docroot=' . ($_SERVER['DOCUMENT_ROOT'] ?? '') . "\n";
echo 'script=' . __FILE__ . "\n";
$cfgPath = __DIR__ . '/config.local.php';
echo 'config.local=' . (is_file($cfgPath) ? 'yes' : 'no') . "\n";
if (is_file($cfgPath)) {
	$epc_config_local = null;
	require $cfgPath;
	echo 'cfg_db=' . ($epc_config_local['db'] ?? '') . ' user=' . ($epc_config_local['user'] ?? '') . ' pass_len=' . strlen((string) ($epc_config_local['password'] ?? '')) . "\n";
	echo 'domain_path=' . ($epc_config_local['domain_path'] ?? '') . "\n";
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . ($epc_config_local['db'] ?? 'ecomae'),
			(string) ($epc_config_local['user'] ?? 'ecomae'),
			(string) ($epc_config_local['password'] ?? '')
		);
		echo 'pdo=ok tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
	} catch (Exception $e) {
		echo 'pdo=fail ' . $e->getMessage() . "\n";
	}
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);
echo 'applied db=' . $DP_Config->db . ' user=' . $DP_Config->user . ' pass_len=' . strlen($DP_Config->password) . "\n";
try {
	$pdo2 = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	echo 'applied_pdo=ok' . "\n";
} catch (Exception $e) {
	echo 'applied_pdo=fail ' . $e->getMessage() . "\n";
}
