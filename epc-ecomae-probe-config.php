<?php
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
error_reporting(E_ALL);
ini_set('display_errors', '1');

define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = __DIR__;

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

echo "host=" . (isset($_SERVER['HTTP_HOST']) ? $_SERVER['HTTP_HOST'] : '') . "\n";
echo "domain_path=" . $DP_Config->domain_path . "\n";
echo "domain_host=" . parse_url($DP_Config->domain_path, PHP_URL_HOST) . "\n";
echo "db=" . $DP_Config->db . " user=" . $DP_Config->user . "\n";
echo "config.local=" . (is_file(__DIR__ . '/config.local.php') ? 'yes' : 'no') . "\n";

try {
	$pdo = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	echo "db_connect=ok\n";
} catch (Exception $e) {
	echo "db_connect=fail " . $e->getMessage() . "\n";
}

echo "dp_core exists=" . (is_file(__DIR__ . '/core/dp_core.php') ? 'yes' : 'no') . "\n";
$snippet = file_get_contents(__DIR__ . '/core/dp_core.php');
echo "dp_core_has_fix=" . (strpos($snippet, 'epc_portal_apply_config($DP_Config)') !== false && strpos($snippet, 'if (!isset($DP_Config)') !== false ? 'yes' : 'no') . "\n";
