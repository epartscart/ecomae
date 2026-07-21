<?php
/**
 * BOC home — Business Operation Control operator console (control-room shell +
 * Operations Command Center). Renders on the Super CP host only.
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
	return;
}

$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (is_file($portalFile)) {
	require_once $portalFile;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_console.php';
$helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php';
if (is_file($helpers)) {
	require_once $helpers;
}

$backend = trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
$base = '/' . $backend;

$operatorName = 'Operator';
if (class_exists('DP_User') && method_exists('DP_User', 'getName')) {
	$maybe = (string) DP_User::getName();
	if ($maybe !== '') { $operatorName = $maybe; }
}

global $db_link;
if (!isset($db_link) || !($db_link instanceof PDO)) {
	echo '<div class="alert alert-danger">Platform database unavailable.</div>';
	return;
}

epc_boc_console_open(array(
	'active'   => 'command_center',
	'title'    => 'Command Center',
	'base'     => $base,
	'operator' => $operatorName,
	'env'      => 'Production',
	'layout'   => 'top', // ERP-style top mega-menu (no left rail)
));
epc_boc_render_command_center($db_link, $base);
epc_boc_console_close();

// Signal control.php to skip legacy statistics + control_items tabs.
// Those paths still run N+1 is_anable checks and can fatal mid-render on PHP 8,
// leaving /cp/control as HTTP 500 with a truncated HTML body.
$GLOBALS['epc_super_cp_dashboard_shown'] = true;
