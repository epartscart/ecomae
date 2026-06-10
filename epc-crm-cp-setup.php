<?php
/**
 * CRM schema + helpers only. CRM UI lives under ERP Finance tab (not /shop/crm/crm).
 *
 * https://www.ecomae.com/epc-crm-cp-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/crm/epc_crm_schema.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
$setupHost = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
$isPlatform = function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname($setupHost);

epc_crm_ensure_schema($pdo);

/** Unpublish standalone CRM CP routes and remove sidebar items. */
function epc_crm_hide_standalone_cp(PDO $pdo)
{
	$pdo->exec(
		"UPDATE `content` SET `published_flag` = 0
		 WHERE `is_frontend` = 0 AND (`url` = 'shop/crm' OR `url` LIKE 'shop/crm/%')"
	);
	$pdo->exec("DELETE FROM `control_items` WHERE `url` LIKE '%/shop/crm/%' OR `caption` IN ('epc_crm_cp', 'epc_cp_group_crm')");
}

epc_crm_hide_standalone_cp($pdo);

if ($isPlatform) {
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	if (function_exists('epc_portal_db_ensure')) {
		epc_portal_db_ensure($pdo);
	}
	$portalHost = isset(epc_portal_sites()[$setupHost]) ? $setupHost : 'www.ecomae.com';
	$settings = epc_portal_load_site_settings_for_host($pdo, $portalHost);
	$packs = isset($settings['enabled_packs']) ? (array) $settings['enabled_packs'] : array('core');
	foreach (array('erp', 'professional', 'super_platform') as $pack) {
		if (!in_array($pack, $packs, true)) {
			$packs[] = $pack;
		}
	}
	$packs = array_values(array_diff($packs, array('crm')));
	$settings['enabled_packs'] = array_values(array_unique($packs));
	epc_portal_save_site_settings($pdo, $settings);
	echo "Platform packs updated: " . implode(', ', $settings['enabled_packs']) . "\n";
}

$backend = $cfg->backend_dir;
echo "CRM schema ready (ERP-embedded only).\n";
echo "database: " . $cfg->db . "\n";
echo "host: {$setupHost}" . ($isPlatform ? " (platform)\n" : " (tenant)\n");
echo "Standalone /shop/crm routes unpublished; control_items CRM entries removed.\n";
echo "CRM URL: https://www.ecomae.com/{$backend}/shop/finance/erp?tab=crm\n";
if ($isPlatform) {
	echo "Super CP: https://www.ecomae.com/{$backend}/\n";
}
