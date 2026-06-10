<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
$hosts = array('www.ecomae.com', 'cp.ecomae.com', 'www.epartscart.com');
foreach ($hosts as $host) {
	echo "=== {$host} ===\n";
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['DOCUMENT_ROOT'] = __DIR__;
	require_once __DIR__ . '/config.php';
	$DP_Config = new DP_Config();
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	epc_portal_apply_config($DP_Config);
	echo "db={$DP_Config->db} user={$DP_Config->user} pass_len=" . strlen($DP_Config->password) . "\n";
	try {
		$pdo = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
		echo "connect=ok tables=" . $pdo->query('SHOW TABLES')->rowCount() . "\n";
	} catch (Exception $e) {
		echo "connect=fail " . $e->getMessage() . "\n";
	}
	echo "\n";
}
