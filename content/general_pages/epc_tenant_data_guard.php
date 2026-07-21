<?php
/**
 * Containment helpers when a non–eParts client is still on shared docpart.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_tenant_data_guard_active(): bool
{
	if (function_exists('epc_portal_tenant_db_is_degraded_shared') && epc_portal_tenant_db_is_degraded_shared()) {
		return true;
	}
	if (!function_exists('epc_portal_is_client_hostname') || !epc_portal_is_client_hostname()) {
		return false;
	}
	if (function_exists('epc_portal_client_may_share_docpart') && epc_portal_client_may_share_docpart()) {
		return false;
	}
	$db = '';
	if (isset($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config'])) {
		$db = strtolower(trim((string) ($GLOBALS['DP_Config']->db ?? '')));
	}
	return $db === 'docpart';
}

function epc_tenant_data_guard_banner(string $surface = 'orders'): string
{
	$label = $surface === 'bank' ? 'Bank / cash accounts' : 'Orders';
	return '<div class="alert alert-danger" style="margin:16px 0">'
		. '<strong>Tenant data isolation:</strong> ' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8')
		. ' from the shared spare-parts database are hidden on this site. '
		. 'Run <code>epc-client-tenant-db-isolate.php?site_key=…&amp;apply=1</code> '
		. '(or seed the demo DB pool) so this tenant gets its own MySQL.'
		. '</div>';
}
