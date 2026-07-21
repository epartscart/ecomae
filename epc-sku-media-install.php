<?php
/**
 * One-shot installer for SKU photos & specs CP route.
 * Usage: /epc-sku-media-install.php?token=...&apply=1
 */
declare(strict_types=1);

error_reporting(E_ALL);
ini_set('display_errors', '0');
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/catalogue/epc_sku_media_cp_install.php';

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);

$cfg = new DP_Config();
$epcTenantHostDbFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password', 'host') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($cfg, $epcTk)) {
				$cfg->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}
if (is_file(__DIR__ . '/content/general_pages/epc_portal.php')) {
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		epc_portal_apply_config($cfg);
	}
}

$dbHost = trim((string) ($cfg->host ?? '127.0.0.1'));
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
$pdo = new PDO(
	'mysql:host=' . $dbHost . ';dbname=' . $cfg->db . ';charset=utf8mb4',
	(string) $cfg->user,
	(string) $cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
if ($backend === '') {
	$backend = 'cp';
}

epc_sku_media_ensure_schema($pdo);
echo "schema=ok\n";
echo 'db=' . (string) $cfg->db . "\n";
echo 'apply=' . ($apply ? '1' : '0') . "\n";

$result = epc_sku_media_cp_install($pdo, $backend, $apply);
echo 'content_id=' . (int) $result['content_id'] . "\n";
echo 'menu_item_id=' . (int) $result['menu_item_id'] . "\n";
if ($apply) {
	echo "Installed CP route: /{$backend}/shop/catalogue/sku_media\n";
} else {
	echo "Pass apply=1 to register CP content + menu.\n";
}
