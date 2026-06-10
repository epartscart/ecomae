<?php
/**
 * Social Media Marketing hub — Super CP + Tenant CP.
 * /cp/control/portal/epc_social_media_hub
 * Guide: ?tab=guide
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/social_media/epc_social_media_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';

$ver = epc_cp_page_asset_version();
epc_cp_register_page_assets(
	array(
		'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
		'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
		'/content/general_pages/epc_social_media_hub_css.php?v=' . rawurlencode($ver),
	),
	array(
		'/content/general_pages/epc_social_media_hub_config.php?v=' . rawurlencode($ver),
		'/content/general_pages/epc_social_media_hub_js.php?v=' . rawurlencode($ver),
	)
);

global $DP_Config;
$backend = epc_social_backend();
$panelPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_social_media_hub_panel.php';
if (!is_file($panelPath)) {
	echo '<div class="alert alert-danger">Social media hub panel missing. Deploy <code>cp/content/control/portal/epc_social_media_hub_panel.php</code>.</div>';
	return;
}
require_once $panelPath;

$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
epc_social_media_render_hub(array('is_super' => $isSuper));
?>
