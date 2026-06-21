<?php
/**
 * CRM access — ERP team, CP admin, or portal CRM pack.
 */
defined('_ASTEXE_') or die('No access');

function epc_crm_cp_url()
{
	return 'shop/finance/erp';
}

function epc_crm_user_can_access(PDO $db)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';

	if (DP_User::isAdmin() && epc_erp_has_content_access($db, epc_crm_cp_url(), 0)) {
		return true;
	}
	if (epc_erp_user_can_access($db)) {
		return true;
	}
	if (DP_User::isBackendGroup() && epc_erp_has_content_access($db, epc_crm_cp_url(), 0)) {
		return true;
	}
	return false;
}

function epc_crm_pack_enabled()
{
	if (!function_exists('epc_portal_enabled_packs')) {
		return true;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	$packs = epc_portal_enabled_packs();
	return in_array('erp', $packs, true) || in_array('super_platform', $packs, true) || in_array('professional', $packs, true);
}
