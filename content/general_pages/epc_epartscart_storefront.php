<?php
/**
 * ePartsCart warehouse_supplier storefront — legacy catalogue only (no APAI category tree on customer pages).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_storefront.php';

function epc_epartscart_storefront_active(PDO $pdo): bool
{
	return epc_apai_is_warehouse_auto_parts_storefront($pdo);
}

function epc_epartscart_lang_href(): string
{
	if (function_exists('epc_apai_storefront_lang_prefix')) {
		return rtrim(epc_apai_storefront_lang_prefix(), '/');
	}
	global $multilang_params;
	if (is_array($multilang_params) && !empty($multilang_params['lang_href'])) {
		return rtrim((string) $multilang_params['lang_href'], '/');
	}
	return '/en';
}

function epc_epartscart_is_apai_alias(string $alias): bool
{
	$alias = strtolower(trim($alias));
	return $alias !== '' && (strpos($alias, 'apai-') === 0 || strpos($alias, 'apai_') === 0);
}

function epc_epartscart_is_apai_url(string $urlRoute): bool
{
	$urlRoute = trim($urlRoute, '/');
	if ($urlRoute === '') {
		return false;
	}
	if (strpos($urlRoute, 'apai-') === 0 || strpos($urlRoute, 'apai_') === 0) {
		return true;
	}
	return (bool) preg_match('#(^|/)apai[-_]#', $urlRoute);
}

/**
 * Redirect APAI-synced catalogue URLs to the standard EN storefront root (legacy left-panel catalog).
 */
function epc_epartscart_apai_category_redirect(PDO $pdo, string $urlRoute): string
{
	if (!epc_epartscart_storefront_active($pdo) || !epc_epartscart_is_apai_url($urlRoute)) {
		return '';
	}
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_categories.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_categories.php';
		$productUrlMode = 'alias';
		global $DP_Config;
		if (is_object($DP_Config) && !empty($DP_Config->product_url)) {
			$productUrlMode = (string) $DP_Config->product_url;
		}
		$resolved = epc_apai_resolve_catalogue_product_route($pdo, $urlRoute, $productUrlMode);
		if (is_array($resolved) && !empty($resolved['product'])) {
			return '';
		}
	}
	return epc_epartscart_lang_href() . '/';
}

/**
 * Neutral autoparts icon for epartscart (no Russian "Нет изображения" from legacy no_image.png).
 */
function epc_epartscart_catalog_placeholder_url(PDO $pdo = null): string
{
	if ($pdo instanceof PDO && epc_epartscart_storefront_active($pdo)) {
		return '/content/files/images/epc_autoparts_placeholder.svg';
	}
	return '/content/files/images/no_image.png';
}

/** ePartsCart storefront shows a neutral icon instead of product photos or Russian no_image.png. */
function epc_epartscart_use_neutral_product_image(PDO $pdo): bool
{
	return epc_epartscart_storefront_active($pdo);
}

/**
 * Clean, language-neutral catalogue placeholder chosen from a context hint
 * (category url / alias / industry key). Replaces the legacy Russian no_image.png
 * for any storefront so products without a photo still look intentional.
 */
function epc_storefront_catalog_placeholder_for_hint(string $hint): string
{
	$base = '/content/files/images/';
	$h = strtolower($hint);
	if (strpos($h, 'electronic') !== false) {
		return $base . 'epc_electronics_placeholder.svg';
	}
	if (strpos($h, 'fashion') !== false || strpos($h, 'apparel') !== false || strpos($h, 'clothing') !== false) {
		return $base . 'epc_fashion_placeholder.svg';
	}
	if (strpos($h, 'jewel') !== false) {
		return $base . 'epc_jewellery_placeholder.svg';
	}
	if (strpos($h, 'svc-') !== false || strpos($h, 'tax') !== false || strpos($h, 'advisor') !== false
		|| strpos($h, 'audit') !== false || strpos($h, 'accounting') !== false || strpos($h, 'consult') !== false
		|| strpos($h, 'service') !== false) {
		return $base . 'epc_services_placeholder.svg';
	}
	if (strpos($h, 'auto') !== false || strpos($h, 'part') !== false) {
		return $base . 'epc_autoparts_placeholder.svg';
	}
	return $base . 'epc_catalog_placeholder.svg';
}

/**
 * Hide APAI branches from the left-panel catalogue tree; keep legacy roots (Tires, Rims, …).
 *
 * @param array<int,array<string,mixed>> $tree
 * @return array<int,array<string,mixed>>
 */
function epc_epartscart_filter_menu_tree(PDO $pdo, array $tree): array
{
	if (!epc_epartscart_storefront_active($pdo)) {
		return $tree;
	}
	$out = array();
	foreach ($tree as $node) {
		$alias = (string) ($node['alias'] ?? '');
		if (epc_epartscart_is_apai_alias($alias)) {
			continue;
		}
		$children = (array) ($node['data'] ?? array());
		if ($children) {
			$node['data'] = epc_epartscart_filter_menu_tree($pdo, $children);
		}
		$out[] = $node;
	}
	return $out;
}
