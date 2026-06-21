<?php
/**
 * Super CP — Modern CP auth settings (eval-safe CP content stub).
 * Heavy logic lives in epc_cp_auth_settings_main.php (real include, not eval).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/' . ($GLOBALS['DP_Config']->backend_dir ?? 'cp') . '/content/control/epc_cp_page_guard.php';

if (!epc_portal_is_super_cp_host()) {
	echo '<div class="col-lg-12"><div class="alert alert-warning">Modern auth settings are available on ECOM AE Super CP only.</div></div>';
} elseif (epc_cp_page_require_admin('Modern auth settings')) {
	epc_cp_page_include(
		'content/control/portal/epc_cp_auth_settings_main.php',
		'Modern auth settings module missing. Deploy via <code>tools/push_one.py</code>.'
	);
}
?>