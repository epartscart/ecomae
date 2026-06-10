<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

foreach (array('www.ecomae.com', 'www.epartscart.com') as $host) {
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = ($host === 'www.epartscart.com') ? 'www.ecomae.com' : $host;
	$_SERVER['HTTPS'] = 'on';
	$_SERVER['DOCUMENT_ROOT'] = __DIR__;
	define('_ASTEXE_', 1);
	require_once __DIR__ . '/config.php';
	$DP_Config = new DP_Config();
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	epc_portal_apply_config($DP_Config);
	echo "=== {$host} ===\n";
	echo "db={$DP_Config->db} user={$DP_Config->user} pass_len=" . strlen($DP_Config->password) . "\n";
	echo "domain_path={$DP_Config->domain_path}\n";
	try {
		$pdo = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
		$n = $pdo->query('SHOW TABLES')->rowCount();
		echo "pdo=ok tables={$n}\n";
		$tpl = $pdo->prepare('SELECT id,name FROM templates WHERE current=1 AND is_frontend=1 LIMIT 1');
		$tpl->execute();
		$row = $tpl->fetch(PDO::FETCH_ASSOC);
		echo 'frontend_tpl=' . ($row ? $row['name'] : 'NONE') . "\n";
	} catch (Exception $e) {
		echo 'pdo=fail ' . $e->getMessage() . "\n";
	}
	echo "\n";
}
