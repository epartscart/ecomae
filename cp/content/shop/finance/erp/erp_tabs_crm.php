<?php
/**
 * ERP tab: full CRM UI embedded (no standalone /shop/crm/crm route).
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_crm_pack_enabled') || !epc_crm_pack_enabled()) {
	echo '<div class="alert alert-warning"><strong>CRM</strong> requires the ERP Finance pack. Enable it in Industry / portal settings.</div>';
	return;
}

if (!epc_crm_user_can_access($db_link)) {
	echo '<div class="alert alert-danger"><strong>Access denied.</strong> CRM is available to ERP administrators and finance users.</div>';
	return;
}

$GLOBALS['epc_crm_embed_in_erp'] = true;

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . (isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp')
	. '/content/shop/crm/crm_main.php';

if (!is_file($include)) {
	echo '<div class="alert alert-danger"><strong>CRM module file not found.</strong> Deploy <code>cp/content/shop/crm/crm_main.php</code> and run setup.</div>';
	return;
}

include $include;
