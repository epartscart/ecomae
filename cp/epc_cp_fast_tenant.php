<?php
/**
 * epartscart (and similar large warehouse tenants) — keep authenticated CP near ~1s.
 * Skips unused ERP router bootstrap on bare /cp/* and marks the request for lean shell work.
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_is_fast_tenant_host(): bool
{
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	$host = preg_replace('/^www\./', '', $host);
	return $host === 'epartscart.com' || strpos($host, 'epartscart.') === 0;
}

/**
 * Bare tenant CP (not /cp/client-erp/..., not platform ERP) — skip ERP router stack.
 */
function epc_cp_should_skip_erp_routers(): bool
{
	if (!epc_cp_is_fast_tenant_host()) {
		return false;
	}
	if (!empty($GLOBALS['epc_demo_cp_context'])) {
		return false;
	}
	$route = function_exists('epc_cp_request_route') ? epc_cp_request_route() : '';
	if ($route !== '' && (strpos($route, 'client-erp/') === 0 || strpos($route, 'platform-erp/') === 0)) {
		return false;
	}
	return true;
}

function epc_cp_fast_tenant_init(): void
{
	if (!epc_cp_is_fast_tenant_host()) {
		return;
	}
	$GLOBALS['epc_cp_fast_tenant'] = true;
	// Cap runaway work; Cloudflare 524 is ~100s — fail faster and free FPM workers.
	if (PHP_SAPI !== 'cli') {
		@set_time_limit(25);
		@ini_set('max_execution_time', '25');
		@ini_set('default_socket_timeout', '3');
	}
}

function epc_cp_fast_tenant_active(): bool
{
	return !empty($GLOBALS['epc_cp_fast_tenant']);
}
