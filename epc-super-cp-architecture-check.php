<?php
/**
 * Super CP vs Client CP architecture diagnostic.
 * https://www.ecomae.com/epc-super-cp-architecture-check.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_cp_menu.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/finance/epc_crm_access.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$host = function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? '');
$isPlatformOp = function_exists('epc_portal_is_platform_operator_host') && epc_portal_is_platform_operator_host($host);
$isClient = function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname($host);
$prevUri = isset($_SERVER['REQUEST_URI']) ? $_SERVER['REQUEST_URI'] : '';
$_SERVER['REQUEST_URI'] = '/' . (isset($cfg->backend_dir) ? $cfg->backend_dir : 'cp') . '/';
$isSuperCp = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$_SERVER['REQUEST_URI'] = $prevUri;

echo "=== ECOM AE Super CP architecture check ===\n";
echo 'hostname: ' . $host . "\n";
echo 'is_super_cp: ' . ($isSuperCp ? 'yes' : 'no') . " (simulated /cp/ request)\n";
echo 'is_platform_operator_host: ' . ($isPlatformOp ? 'yes' : 'no') . "\n";
echo 'is_client: ' . ($isClient ? 'yes' : 'no') . "\n";
echo 'db: ' . $cfg->db . "\n";
echo 'industry (config): ' . (isset($cfg->epc_portal_industry) ? $cfg->epc_portal_industry : 'n/a') . "\n\n";

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	exit('DB connect failed: ' . $e->getMessage() . "\n");
}

epc_portal_db_ensure($pdo);
$settings = epc_portal_load_site_settings($pdo);
$packs = isset($settings['enabled_packs']) ? $settings['enabled_packs'] : array();
$accessMode = isset($settings['access_mode']) ? $settings['access_mode'] : 'full';

echo "enabled_packs: " . json_encode(array_values($packs)) . "\n";
echo 'access_mode: ' . $accessMode . "\n";
echo 'industry_code: ' . ($settings['industry_code'] ?? '') . "\n";
echo 'crm_pack_enabled: ' . (epc_crm_pack_enabled() ? 'yes' : 'no') . "\n\n";

echo "=== CP menu (CRM / ERP / tenant hub) ===\n";
$needles = array('tenant_hub', 'shop/crm/crm', 'shop/finance/erp');
foreach ($needles as $needle) {
	$st = $pdo->prepare(
		'SELECT `id`, `url`, `caption` FROM `control_items` WHERE `url` LIKE ? ORDER BY `id` ASC LIMIT 5'
	);
	$st->execute(array('%' . $needle . '%'));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	echo $needle . ': ' . (count($rows) > 0 ? 'registered (' . count($rows) . ' item(s))' : 'NOT FOUND') . "\n";
	foreach ($rows as $r) {
		echo '  #' . $r['id'] . ' ' . $r['url'] . "\n";
	}
}

echo "\n=== Content routes ===\n";
foreach (array('shop/crm/crm', 'shop/finance/erp', 'shop/tenant_hub/tenant_hub', 'shop/document_control/document_control') as $curl) {
	$st = $pdo->prepare('SELECT `id`, `url`, `published_flag` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($curl));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	echo $curl . ': ' . ($row ? 'id=' . $row['id'] . ' published=' . $row['published_flag'] : 'missing') . "\n";
}

if ($isPlatformOp) {
	echo "\n=== Platform tenants (ecomae registry) ===\n";
	$tenants = epc_portal_list_tenants($pdo);
	echo 'tenant_count: ' . count($tenants) . "\n";
	foreach (array_slice($tenants, 0, 12) as $t) {
		echo '  ' . ($t['site_key'] ?? '') . ' | ' . ($t['hostname'] ?? '') . ' | ' . ($t['status'] ?? '') . ' | db=' . ($t['db_name'] ?? '') . "\n";
	}
	if (count($tenants) > 12) {
		echo '  … +' . (count($tenants) - 12) . " more\n";
	}
} else {
	echo "\n(tenant registry is on platform DB — run this script on www.ecomae.com for tenant_count)\n";
}

echo "\n=== Expected ===\n";
echo "Super CP: www.ecomae.com or cp.ecomae.com → is_super_cp=yes, super_platform pack, tenant hub registered.\n";
echo "Client CP: www.CLIENT.com → is_client=yes, no super_platform, CRM/ERP if packs include erp/crm/professional.\n";
echo "Done.\n";
