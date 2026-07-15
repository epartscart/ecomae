<?php
/*
 * @ https://EasyToYou.eu - IonCube v11 Decoder Online
 * @ PHP 5.0 & 5.6
 * @ Decoder version: 1.1.5
 * @ Release: 12/09/2024
 */

// Decoded file for php version 56.
define("_ASTEXE_", 1);
if (PHP_SAPI !== 'cli') {
	@set_time_limit(30);
}
require_once $_SERVER["DOCUMENT_ROOT"] . "/config.php";
$DP_Config = new DP_Config();
require_once $_SERVER["DOCUMENT_ROOT"] . "/content/general_pages/epc_portal.php";

// Industry wildcard subdomains (*.ecomae.com) — bootstrap industry context
// early, same as the root index.php. Without this, /cp/ on an industry
// subdomain (e.g. industries.ecomae.com) never gets its domain_path set to
// the current host, so it falls back to the platform default and trips the
// dp_core "License error 1.02: Wrong value of domain_path field" check.
$__epcIndustrySubdomainRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_subdomain_router.php';
if (is_file($__epcIndustrySubdomainRouter)) {
	require_once $__epcIndustrySubdomainRouter;
	if (function_exists('epc_industry_subdomain_bootstrap')) {
		epc_industry_subdomain_bootstrap($DP_Config);
	}
}

$demoBootstrap = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
if (is_file($demoBootstrap)) {
	require_once $demoBootstrap;
	if (function_exists('epc_portal_demo_cp_maybe_redirect_bare_path')) {
		epc_portal_demo_cp_maybe_redirect_bare_path();
	}
	if (function_exists('epc_portal_demo_try_bootstrap_cp')) {
		epc_portal_demo_try_bootstrap_cp($DP_Config);
	}
}
epc_portal_apply_config($DP_Config);

require_once __DIR__ . '/epc_cp_bootstrap_light.php';
require_once __DIR__ . '/epc_cp_auth_gate.php';

$__epcTenantControl = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
if (is_file($__epcTenantControl)) {
	require_once $__epcTenantControl;
	if (function_exists('epc_portal_tenant_control_maybe_block')) {
		epc_portal_tenant_control_maybe_block();
	}
}
if (!empty($GLOBALS['epc_demo_cp_context'])) {
	$GLOBALS['DP_Config'] = $DP_Config;
	epc_portal_apply_config($DP_Config);
	if (function_exists('epc_portal_demo_reapply_cp_config')) {
		epc_portal_demo_reapply_cp_config($DP_Config);
	}
}

$clientErpRouterEarly = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
if (is_file($clientErpRouterEarly) && empty($GLOBALS['epc_demo_cp_context'])) {
	require_once $clientErpRouterEarly;
	if (function_exists('epc_client_erp_maybe_handle_bare_path')) {
		epc_client_erp_maybe_handle_bare_path();
	}
	if (function_exists('epc_client_erp_bootstrap')) {
		epc_client_erp_bootstrap();
	}
	if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
		$sharedErpEarly = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
		if (is_file($sharedErpEarly)) {
			require_once $sharedErpEarly;
			$tenantRow = $GLOBALS['epc_client_erp_tenant_row'] ?? null;
			if (!is_array($tenantRow) && function_exists('epc_client_erp_tenant_row')) {
				$tenantRow = epc_client_erp_tenant_row();
			}
			if (is_array($tenantRow) && function_exists('epc_portal_shared_erp_apply_row_config')) {
				epc_portal_shared_erp_apply_row_config($DP_Config, $tenantRow);
			}
		}
	}
}

epc_cp_bootstrap_light_init();
$epcCpLightBootstrap = epc_cp_bootstrap_light_active();

if (!$epcCpLightBootstrap) {
	$platformErpRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
	if (is_file($platformErpRouter)) {
		require_once $platformErpRouter;
		if (function_exists('epc_platform_erp_bootstrap')) {
			epc_platform_erp_bootstrap();
		}
	}

	$clientErpRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
	if (is_file($clientErpRouter)) {
		require_once $clientErpRouter;
		if (function_exists('epc_client_erp_maybe_handle_bare_path')) {
			epc_client_erp_maybe_handle_bare_path();
		}
		if (function_exists('epc_client_erp_bootstrap')) {
			epc_client_erp_bootstrap();
		}
		if (function_exists('epc_cp_logout_if_requested')) {
			epc_cp_logout_if_requested();
		}
		if (function_exists('epc_client_erp_block_platform_operator')) {
			epc_client_erp_block_platform_operator();
		}
		if (function_exists('epc_client_erp_block_tenant_on_bare_cp')) {
			epc_client_erp_block_tenant_on_bare_cp();
		}
		if (function_exists('epc_client_erp_redirect_legacy_shell')) {
			epc_client_erp_redirect_legacy_shell();
		}
	}

	if (is_file($platformErpRouter)) {
		if (function_exists('epc_cp_logout_if_requested')) {
			epc_cp_logout_if_requested();
		}
		if (function_exists('epc_platform_erp_block_client_session')) {
			epc_platform_erp_block_client_session();
		}
	}

	$sharedErpBootstrap = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedErpBootstrap)) {
		require_once $sharedErpBootstrap;
		if ((function_exists('epc_platform_erp_is_request') && epc_platform_erp_is_request())
			|| (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active())) {
			if (function_exists('epc_platform_erp_apply_config')) {
				epc_platform_erp_apply_config($DP_Config);
			}
		} elseif (!empty($GLOBALS['epc_demo_cp_context'])) {
			// Tenant DB already applied by epc_portal_demo_try_bootstrap_cp — never platform/shared ERP.
		} elseif (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
			$tenantRow = $GLOBALS['epc_client_erp_tenant_row'] ?? null;
			if (!is_array($tenantRow) && function_exists('epc_client_erp_tenant_row')) {
				$tenantRow = epc_client_erp_tenant_row();
			}
			if (is_array($tenantRow) && function_exists('epc_portal_shared_erp_apply_row_config')) {
				epc_portal_shared_erp_apply_row_config($DP_Config, $tenantRow);
			} elseif (function_exists('epc_portal_shared_erp_apply_config')) {
				epc_portal_shared_erp_apply_config($DP_Config);
			}
		}
		// Shared ERP tenant DB applies only under /cp/client-erp/{site_key}/ — never bare /cp/.
	}
}

epc_cp_auth_gate_run();

$epcCpAjaxRoute = function_exists('epc_cp_request_route') ? epc_cp_request_route() : '';
$epcCpAjaxMap = array(
	'control/portal/ajax_auto_price' => 'content/control/portal/ajax_auto_price.php',
);
if ($epcCpAjaxRoute !== '' && isset($epcCpAjaxMap[$epcCpAjaxRoute])) {
	$epcCpBackend = trim((string) $DP_Config->backend_dir, '/');
	if ($epcCpBackend === '') {
		$epcCpBackend = 'cp';
	}
	$epcCpAjaxFile = $_SERVER['DOCUMENT_ROOT'] . '/' . $epcCpBackend . '/' . $epcCpAjaxMap[$epcCpAjaxRoute];
	if (is_file($epcCpAjaxFile)) {
		require $epcCpAjaxFile;
		exit;
	}
}

$isFrontMode = 0;
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_helper.php";
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_content.php";
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_module.php";
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_template.php";
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_core.php";

?>
