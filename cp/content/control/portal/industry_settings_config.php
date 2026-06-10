<?php
/**
 * Industry settings — runtime config JS (loaded from CP footer, outside .row).
 */
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_cp_menu.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_theme_templates.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
epc_portal_apply_config($DP_Config);

$dbHost = trim((string) $DP_Config->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
try {
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo 'window.EPC_PS={};';
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo 'window.EPC_PS={};';
	exit;
}

$settings = epc_portal_load_site_settings($db_link);
$menuPolicy = epc_portal_cp_menu_policy($settings);
$industries = epc_portal_settings_industries();
$erpModulePresets = epc_portal_erp_modules_presets_ui();
$showDeploy = epc_portal_can_deploy_portal_package();
$backend = trim((string) $DP_Config->backend_dir, '/');
$defaults = array();
$erpDefaults = array();
foreach ($industries as $code => $ind) {
	$defaults[$code] = isset($ind['cp_packs']) ? $ind['cp_packs'] : array('core');
	$erpDefaults[$code] = epc_portal_industry_erp_modules_preset((string) $code);
}

echo 'window.EPC_PS = ' . json_encode(array(
	'ajaxUrl' => '/' . $backend . '/content/control/portal/ajax_portal.php',
	'hiddenGroups' => array_values($menuPolicy['hidden_groups']),
	'hiddenItems' => array_values($menuPolicy['hidden_items']),
	'erpModulePresets' => $erpModulePresets,
	'industryDefaults' => $defaults,
	'industryErpDefaults' => $erpDefaults,
	'styleTemplatesAll' => epc_portal_style_templates_for_js(),
	'showDeploy' => (bool) $showDeploy,
	'activeThemeTemplate' => (string) ($settings['theme_template'] ?? 'classic'),
), JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP) . ';';
