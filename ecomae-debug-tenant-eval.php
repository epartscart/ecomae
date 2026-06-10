<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');
$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['REQUEST_URI'] = '/cp/shop/tenant_hub/tenant_hub?tab=onboard';
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);
$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8', $DP_Config->user, $DP_Config->password);
$st = $db_link->prepare('SELECT content FROM content WHERE url=? AND is_frontend=0');
$st->execute(array('shop/tenant_hub/tenant_hub'));
$contentPath = (string) $st->fetchColumn();
$php_path = str_replace(array('<backend_dir>'), $DP_Config->backend_dir, $_SERVER['DOCUMENT_ROOT'] . $contentPath);
echo "path=$php_path exists=" . (is_file($php_path) ? 'yes' : 'no') . "\n";
$mainPhp = file_get_contents($php_path);
echo "main_len=" . strlen($mainPhp) . "\n";
$tpl = file_get_contents(__DIR__ . '/cp/templates/bootstrap_admin/desktop.php');
$tpl = str_replace('<docpart type="main" name="main" />', $mainPhp, $tpl);
echo "tpl_len=" . strlen($tpl) . "\n";
try {
	eval(" ?>" . $tpl . "<?php ");
	echo "eval ok\n";
} catch (Throwable $e) {
	echo 'eval fail: ' . $e->getMessage() . ' @ ' . $e->getLine() . "\n";
}
