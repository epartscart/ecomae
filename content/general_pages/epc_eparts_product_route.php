<?php
/**
 * EParts product deep-link helpers (minimal restore stub).
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_storefront_vin_label')) {
	function epc_storefront_vin_label(): string
	{
		return 'VIN';
	}
}

if (!function_exists('epc_eparts_product_url')) {
	function epc_eparts_product_url(string $langHref, array $params = array()): string
	{
		$base = rtrim($langHref, '/') . '/eparts-product';
		if (!$params) {
			return $base;
		}
		return $base . '?' . http_build_query($params);
	}
}
