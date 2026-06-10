<?php
/**
 * CP route shop/finance/erp — eval-safe wrapper.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
if (!epc_erp_is_shell_request()) {
	epc_erp_cp_shell_page_redirect();
	return;
}
$GLOBALS['epc_erp_shell_mode'] = true;

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
	$login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
		&& function_exists('epc_portal_demo_cp_login_url') && function_exists('epc_portal_demo_cp_site_key')) {
		$key = epc_portal_demo_cp_site_key();
		if ($key !== '') {
			$login = epc_portal_demo_cp_login_url($key);
		}
	}
	echo '<div class="alert alert-warning">Please <a href="' . htmlspecialchars($login, ENT_QUOTES, 'UTF-8') . '">log in to the control panel</a> to open ERP.</div>';
	return;
}

$platformErpRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
if (is_file($platformErpRouter)) {
	require_once $platformErpRouter;
}
$isPlatformErpRequest = function_exists('epc_platform_erp_is_request') && epc_platform_erp_is_request();
if ($isPlatformErpRequest) {
	if (is_object($GLOBALS['DP_Config']) && function_exists('epc_platform_erp_apply_config')) {
		epc_platform_erp_apply_config($GLOBALS['DP_Config']);
	}
} else {
	$sharedErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedErpFile)
		&& function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()
		&& function_exists('epc_portal_is_cp_request') && epc_portal_is_cp_request()) {
		require_once $sharedErpFile;
		if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()) {
			$demoRow = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
			if (is_array($demoRow) && is_object($GLOBALS['DP_Config']) && function_exists('epc_portal_demo_apply_cp_config')
				&& function_exists('epc_portal_demo_cp_site_key')) {
				epc_portal_demo_apply_cp_config($GLOBALS['DP_Config'], $demoRow, epc_portal_demo_cp_site_key());
			}
		} elseif (function_exists('epc_portal_shared_erp_infer_tenant_from_session')) {
			epc_portal_shared_erp_infer_tenant_from_session();
		}
		if (!(function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only())) {
			$tenantRow = function_exists('epc_portal_shared_erp_active_tenant')
				? epc_portal_shared_erp_active_tenant()
				: null;
			if ($tenantRow === null) {
				$loginUrl = function_exists('epc_client_erp_login_url') ? epc_client_erp_login_url('asap') : '/cp/client-erp/asap/';
				$logout = function_exists('epc_cp_logout_redirect_url')
					? epc_cp_logout_redirect_url() . (strpos(epc_cp_logout_redirect_url(), '?') !== false ? '&' : '?') . 'logout=1'
					: '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/?logout=1';
				echo '<div class="alert alert-danger" style="margin:16px;max-width:960px">';
				echo '<strong>Company ERP session required</strong>';
				echo '<p>Sign in at the client ERP URL for your company — not Super CP <code>/cp/</code>.</p>';
				echo '<p><a class="btn btn-warning btn-sm" href="' . htmlspecialchars($logout, ENT_QUOTES, 'UTF-8') . '">Log out</a> '
					. 'then open <a href="' . htmlspecialchars($loginUrl, ENT_QUOTES, 'UTF-8') . '">' . htmlspecialchars($loginUrl, ENT_QUOTES, 'UTF-8') . '</a>.</p>';
				echo '</div>';
				return;
			}
			if (is_object($GLOBALS['DP_Config'])) {
				if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
					$tenantRow = $GLOBALS['epc_client_erp_tenant_row'] ?? null;
					if (!is_array($tenantRow) && function_exists('epc_client_erp_tenant_row')) {
						$tenantRow = epc_client_erp_tenant_row();
					}
					if (is_array($tenantRow) && function_exists('epc_portal_shared_erp_apply_row_config')) {
						epc_portal_shared_erp_apply_row_config($GLOBALS['DP_Config'], $tenantRow);
					} elseif (function_exists('epc_portal_shared_erp_apply_config')) {
						epc_portal_shared_erp_apply_config($GLOBALS['DP_Config']);
					}
				} elseif (function_exists('epc_portal_shared_erp_apply_config')) {
					epc_portal_shared_erp_apply_config($GLOBALS['DP_Config']);
				}
			}
		}
	}
}

if (function_exists('epc_erp_rebind_db_link')) {
	$rebound = epc_erp_rebind_db_link();
	if ($rebound instanceof PDO) {
		$db_link = $rebound;
	}
}

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
	. '/content/shop/finance/erp/erp_main.php';

if (!is_file($include)) {
	echo '<div class="alert alert-danger"><strong>ERP module file not found.</strong></div>';
	return;
}

include $include;

// Close PHP before template HTML when this file is embedded in CP template eval (dp_core.php).
?>
