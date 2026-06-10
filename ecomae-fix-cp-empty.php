<?php
/**
 * Fix empty Super CP: menu packs, tenant hub, index /cp/* routing.
 * https://www.ecomae.com/ecomae-fix-cp-empty.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$result = array('ok' => true, 'steps' => array());

if (is_file(__DIR__ . '/ecomae-fix-cp-delegate.php')) {
	require_once __DIR__ . '/ecomae-fix-cp-delegate.php';
} elseif (is_file(__DIR__ . '/ecomae-super-cp-setup.php')) {
	require_once __DIR__ . '/ecomae-super-cp-setup.php';
}
if (function_exists('ecomae_fix_cp_delegate_index')) {
	$result['steps']['index_delegate'] = ecomae_fix_cp_delegate_index(__DIR__);
}

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

epc_portal_db_ensure($pdo);

if (is_file(__DIR__ . '/ecomae-super-cp-setup.php')) {
	require_once __DIR__ . '/ecomae-super-cp-setup.php';
	if (function_exists('ecomae_super_cp_materialize_files')) {
		$result['steps']['materialized'] = ecomae_super_cp_materialize_files(__DIR__);
	}
}

$cpSettings = epc_portal_default_site_settings('cp.ecomae.com');
$cpSettings['host'] = 'cp.ecomae.com';
$cpSettings['enabled_packs'] = array_values(array_unique(array_merge(
	$cpSettings['enabled_packs'],
	array('core', 'commerce', 'professional', 'marketing', 'super_platform', 'erp', 'catalogue')
)));
epc_portal_save_site_settings($pdo, $cpSettings);
$result['steps']['cp_site_settings'] = $cpSettings['enabled_packs'];

$result['steps']['tenant_hub_menu'] = epc_cp_super_platform_menu_apply($pdo);
$result['steps']['control_items'] = (int) $pdo->query('SELECT COUNT(*) FROM `control_items`')->fetchColumn();
$result['steps']['control_groups'] = (int) $pdo->query('SELECT COUNT(*) FROM `control_groups`')->fetchColumn();
$result['steps']['tenant_hub_items'] = $pdo->query("SELECT id, url, show_anyway FROM `control_items` WHERE `url` LIKE '%tenant_hub%'")->fetchAll(PDO::FETCH_ASSOC);

$_SERVER['HTTP_HOST'] = 'cp.ecomae.com';
$result['steps']['runtime_packs'] = epc_portal_load_site_settings()['enabled_packs'] ?? array();

$result['urls'] = array(
	'super_cp' => 'https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard',
	'login' => 'https://cp.ecomae.com/cp/',
);

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
