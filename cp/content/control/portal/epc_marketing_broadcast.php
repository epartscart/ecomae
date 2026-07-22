<?php
/**
 * Marketing Broadcast hub — bulk email & WhatsApp (tenant CP + Super CP).
 * /cp/control/portal/epc_marketing_broadcast
 * Guide: ?tab=guide
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/marketing/epc_marketing_broadcast_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';

$ver = epc_cp_page_asset_version() . 'mb2';
epc_cp_register_page_assets(
	array(
		'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
		'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
		'/content/general_pages/epc_marketing_broadcast_css.php?v=' . rawurlencode($ver),
	),
	array(
		'/content/general_pages/epc_marketing_broadcast_config.php?v=' . rawurlencode($ver),
		'/content/general_pages/epc_marketing_broadcast_js.php?v=' . rawurlencode($ver),
	)
);

global $DP_Config;
$backend = epc_mb_backend();
$panelPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_marketing_broadcast_panel.php';
if (!is_file($panelPath)) {
	echo '<div class="alert alert-danger">Marketing broadcast panel missing. Deploy <code>cp/content/control/portal/epc_marketing_broadcast_panel.php</code>.</div>';
	return;
}
require_once $panelPath;

epc_mb_render_hub();
?>
