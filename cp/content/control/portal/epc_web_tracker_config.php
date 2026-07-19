<?php
/**
 * CP Website tracker — JS boot config (loaded outside .row).
 */
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store');

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
if (is_file($docRoot . '/content/general_pages/epc_portal.php')) {
	require_once $docRoot . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		epc_portal_apply_config($DP_Config);
	}
}

$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($backend === '') {
	$backend = 'cp';
}

$isSuper = false;
if (function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator()) {
	$isSuper = true;
}
if (!$isSuper && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
	$isSuper = true;
}

$ajaxUrl = '/' . $backend . '/content/control/portal/ajax_epc_web_tracker.php';

echo 'window.EPC_WEB_TRACKER_CP=' . json_encode(array(
	'ajaxUrl' => $ajaxUrl,
	'isSuper' => $isSuper,
), JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE) . ";\n";
