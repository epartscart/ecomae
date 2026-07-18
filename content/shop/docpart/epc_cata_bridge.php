<?php
/**
 * Minimal CATA bridge config for EParts Mod storefront.
 * Full sync bridge can be restored later; this unblocks page boot.
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_cata_bridge_js_config')) {
	function epc_cata_bridge_js_config(): array
	{
		return array(
			'bridge' => false,
			'use_cata_first' => false,
			'cata_sync' => false,
			'cata_api' => '/api/eparts_cata_proxy.php',
			'partsapi_api' => '/api/partsapi_proxy.php',
		);
	}
}
