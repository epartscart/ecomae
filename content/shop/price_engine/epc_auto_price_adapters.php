<?php
/**
 * EPC Auto Price — fetch adapters (manual, Amazon.ae, Noon, eBay).
 * MVP: og:meta parse + manual price fallback. Full SP-API / Trading API = roadmap.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_auto_price_engine.php';

function epc_ape_adapter_registry(): array
{
	return array(
		'manual' => 'epc_price_adapter_manual',
		'amazon_ae' => 'epc_price_adapter_amazon_ae',
		'noon' => 'epc_price_adapter_noon',
		'ebay' => 'epc_price_adapter_ebay',
		'warehouse' => 'epc_price_adapter_warehouse',
		'supplier' => 'epc_price_adapter_supplier',
	);
}

/**
 * @return array{ok:bool,price:float,currency:string,message:string,adapter:string}
 */
function epc_ape_adapter_fetch(string $sourceType, string $url, float $manualPrice = 0.0): array
{
	$registry = epc_ape_adapter_registry();
	$fn = $registry[$sourceType] ?? 'epc_price_adapter_manual';
	if (!function_exists($fn)) {
		return array('ok' => false, 'price' => 0, 'currency' => 'AED', 'message' => 'Unknown adapter', 'adapter' => $fn);
	}
	$result = $fn($url, $manualPrice);
	$result['adapter'] = $fn;
	return $result;
}

function epc_ape_adapter_parse_og_price(string $url): array
{
	$extract = epc_ape_extract_url_meta($url);
	if (empty($extract['ok'])) {
		return $extract;
	}
	$meta = $extract['meta'] ?? array();
	return array(
		'ok' => true,
		'price' => (float) ($meta['price'] ?? 0),
		'currency' => (string) ($meta['currency'] ?? 'AED'),
		'title' => (string) ($meta['title'] ?? ''),
		'message' => (float) ($meta['price'] ?? 0) > 0 ? 'Parsed og:price' : 'No price in meta — use manual entry',
	);
}

function epc_price_adapter_manual(string $url, float $manualPrice = 0.0): array
{
	if ($manualPrice > 0) {
		return array('ok' => true, 'price' => $manualPrice, 'currency' => 'AED', 'message' => 'Manual price');
	}
	if ($url !== '') {
		$parsed = epc_ape_adapter_parse_og_price($url);
		if (!empty($parsed['ok']) && (float) ($parsed['price'] ?? 0) > 0) {
			return array(
				'ok' => true,
				'price' => (float) $parsed['price'],
				'currency' => (string) ($parsed['currency'] ?? 'AED'),
				'message' => 'Manual URL meta parse',
			);
		}
	}
	return array('ok' => false, 'price' => 0, 'currency' => 'AED', 'message' => 'Enter competitor price manually (no API key)');
}

function epc_price_adapter_amazon_ae(string $url, float $manualPrice = 0.0): array
{
	if ($manualPrice > 0) {
		return array('ok' => true, 'price' => $manualPrice, 'currency' => 'AED', 'message' => 'Amazon.ae manual override');
	}
	if ($url === '' || stripos($url, 'amazon') === false) {
		return array('ok' => false, 'price' => 0, 'currency' => 'AED', 'message' => 'Amazon.ae: paste product URL or enter price (SP-API not configured)');
	}
	$parsed = epc_ape_adapter_parse_og_price($url);
	if (!empty($parsed['ok']) && (float) ($parsed['price'] ?? 0) > 0) {
		return array(
			'ok' => true,
			'price' => (float) $parsed['price'],
			'currency' => (string) ($parsed['currency'] ?? 'AED'),
			'message' => 'Amazon.ae og:meta (affiliate/API recommended for production)',
		);
	}
	return array(
		'ok' => false,
		'price' => 0,
		'currency' => 'AED',
		'message' => 'Amazon.ae blocked or no og:price — configure SP-API or enter price manually',
	);
}

function epc_price_adapter_noon(string $url, float $manualPrice = 0.0): array
{
	if ($manualPrice > 0) {
		return array('ok' => true, 'price' => $manualPrice, 'currency' => 'AED', 'message' => 'Noon manual override');
	}
	if ($url === '' || stripos($url, 'noon') === false) {
		return array('ok' => false, 'price' => 0, 'currency' => 'AED', 'message' => 'Noon: paste product URL or enter price (partner API not configured)');
	}
	$parsed = epc_ape_adapter_parse_og_price($url);
	if (!empty($parsed['ok']) && (float) ($parsed['price'] ?? 0) > 0) {
		return array(
			'ok' => true,
			'price' => (float) $parsed['price'],
			'currency' => (string) ($parsed['currency'] ?? 'AED'),
			'message' => 'Noon og:meta parse',
		);
	}
	return array(
		'ok' => false,
		'price' => 0,
		'currency' => 'AED',
		'message' => 'Noon: no price in page meta — enter manually or add partner API',
	);
}

function epc_price_adapter_ebay(string $url, float $manualPrice = 0.0): array
{
	if ($manualPrice > 0) {
		return array('ok' => true, 'price' => $manualPrice, 'currency' => 'AED', 'message' => 'eBay manual override');
	}
	if ($url === '') {
		return array('ok' => false, 'price' => 0, 'currency' => 'USD', 'message' => 'eBay: paste listing URL or price (Trading API stub)');
	}
	$parsed = epc_ape_adapter_parse_og_price($url);
	$currency = (string) ($parsed['currency'] ?? 'USD');
	if (!empty($parsed['ok']) && (float) ($parsed['price'] ?? 0) > 0) {
		return array(
			'ok' => true,
			'price' => (float) $parsed['price'],
			'currency' => $currency,
			'message' => 'eBay og:meta (Trading API for live sync — roadmap)',
		);
	}
	return array(
		'ok' => false,
		'price' => 0,
		'currency' => 'USD',
		'message' => 'eBay stub — configure Trading API or enter cross-list price manually',
	);
}

function epc_price_adapter_warehouse(string $url, float $manualPrice = 0.0): array
{
	return array(
		'ok' => true,
		'price' => $manualPrice,
		'currency' => 'AED',
		'message' => 'Warehouse cost from price list (sync via ERP)',
	);
}

function epc_price_adapter_supplier(string $url, float $manualPrice = 0.0): array
{
	return array(
		'ok' => true,
		'price' => $manualPrice,
		'currency' => 'AED',
		'message' => 'Supplier price list entry',
	);
}
