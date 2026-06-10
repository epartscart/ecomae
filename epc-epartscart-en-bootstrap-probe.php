<?php
/**
 * Simulate epartscart /en/ bootstrap — pinpoints No DB connect source.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['REQUEST_URI'] = '/en/';
$_SERVER['DOCUMENT_ROOT'] = '/home/ecomae/htdocs/www.ecomae.com';

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

echo 'docroot=' . $_SERVER['DOCUMENT_ROOT'] . "\n";
echo 'db=' . $DP_Config->db . ' user=' . $DP_Config->user . ' pass_len=' . strlen($DP_Config->password) . "\n";
echo 'domain_path=' . $DP_Config->domain_path . "\n";

try {
	$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	echo "pdo=ok\n";
	$isFrontMode = 1;
	$q = $db_link->prepare('SELECT COUNT(*) FROM `templates` WHERE `current` = ? AND `is_frontend` = ?');
	$q->execute(array(1, $isFrontMode));
	$cnt = (int) $q->fetchColumn();
	echo "frontend_templates={$cnt}\n";
	if ($cnt === 0) {
		echo "FAIL: no current frontend template (dp_core exits No DB connect here)\n";
	}
} catch (Exception $e) {
	echo 'pdo=fail ' . $e->getMessage() . "\n";
}
