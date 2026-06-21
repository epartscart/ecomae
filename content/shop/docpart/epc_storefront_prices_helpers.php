<?php
/**
 * Storefront price visibility — hide prices for guests on warehouse_supplier tenants (epartscart first).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_auth_links.php';

function epc_storefront_prices_resolve_site_key(): string
{
	if (function_exists('epc_apai_resolve_storefront_site_key')) {
		if (!function_exists('epc_ape_tenant_config_get')) {
			$engine = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_engine.php';
			if (is_file($engine)) {
				require_once $engine;
			}
		}
		if (function_exists('epc_apai_resolve_storefront_site_key')) {
			$key = (string) epc_apai_resolve_storefront_site_key();
			if ($key !== '') {
				return preg_replace('/[^a-z0-9_]/', '', strtolower($key));
			}
		}
	}
	$host = function_exists('epc_portal_host')
		? strtolower((string) epc_portal_host())
		: strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	foreach (array('epartscart', 'electronicae', 'stylenlook', 'thejewellerytrend', 'taxofinca') as $needle) {
		if (strpos($host, $needle) !== false) {
			return $needle;
		}
	}
	return '';
}

/**
 * Whether this tenant hides storefront prices for guests (default: warehouse_supplier tenants).
 */
function epc_storefront_prices_hide_for_guests_enabled(): bool
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}

	$siteKey = epc_storefront_prices_resolve_site_key();
	if ($siteKey === '') {
		$cached = false;
		return false;
	}

	global $db_link;
	$pdo = ($db_link instanceof PDO) ? $db_link : null;
	if (!$pdo instanceof PDO) {
		$cached = ($siteKey === 'epartscart');
		return $cached;
	}

	if (!function_exists('epc_ape_tenant_config_get')) {
		$engine = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_engine.php';
		if (is_file($engine)) {
			require_once $engine;
		}
	}

	$config = array();
	if (function_exists('epc_ape_tenant_config_get')) {
		$tenantCfg = epc_ape_tenant_config_get($pdo, $siteKey);
		$config = is_array($tenantCfg['config'] ?? null) ? $tenantCfg['config'] : array();
		if (array_key_exists('hide_storefront_prices_for_guests', $config)) {
			$cached = !empty($config['hide_storefront_prices_for_guests']);
			return $cached;
		}
		if (array_key_exists('show_storefront_prices_to_guests', $config)) {
			$cached = empty($config['show_storefront_prices_to_guests']);
			return $cached;
		}
		$profile = (string) ($tenantCfg['profile'] ?? '');
		$cached = ($profile === 'warehouse_supplier');
		return $cached;
	}

	$cached = ($siteKey === 'epartscart');
	return $cached;
}

/**
 * True when the current (or given) user may see storefront prices.
 */
function epc_storefront_prices_visible_for_user(?int $userId = null): bool
{
	if ($userId === null) {
		if (!class_exists('DP_User')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		}
		$userId = (int) DP_User::getUserId();
	}
	if ($userId > 0) {
		return true;
	}
	if (!epc_storefront_prices_hide_for_guests_enabled()) {
		return true;
	}
	return false;
}

function epc_storefront_prices_login_cta_html(?array $multilang_params = null): string
{
	if ($multilang_params === null && !empty($GLOBALS['multilang_params']) && is_array($GLOBALS['multilang_params'])) {
		$multilang_params = $GLOBALS['multilang_params'];
	}
	$login = htmlspecialchars(epc_storefront_auth_login_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$signup = htmlspecialchars(epc_storefront_auth_signup_url($multilang_params), ENT_QUOTES, 'UTF-8');
	return '<span class="epc-price-login-cta">'
		. '<a href="' . $login . '">Log in</a>'
		. '<span class="epc-price-login-cta__sep"> or </span>'
		. '<a href="' . $signup . '">register</a>'
		. '<span class="epc-price-login-cta__hint"> to see prices</span>'
		. '</span>';
}

function epc_storefront_prices_login_cta_plain(?array $multilang_params = null): string
{
	if ($multilang_params === null && !empty($GLOBALS['multilang_params']) && is_array($GLOBALS['multilang_params'])) {
		$multilang_params = $GLOBALS['multilang_params'];
	}
	return 'Log in or register to see prices: '
		. epc_storefront_auth_login_url($multilang_params);
}

function epc_storefront_prices_styles(): string
{
	return '<style>'
		. '.epc-price-login-cta{display:inline-block;font-size:12px;line-height:1.35;color:#64748b}'
		. '.epc-price-login-cta a{font-weight:600;color:#2b78d6;text-decoration:none}'
		. '.epc-price-login-cta a:hover{text-decoration:underline}'
		. '.epc-price-login-cta__sep{color:#94a3b8}'
		. '.epc-price-login-cta__hint{color:#64748b}'
		. '.td_price .epc-price-login-cta{max-width:140px}'
		. '</style>';
}

function epc_storefront_prices_agent_guest_rules(): string
{
	if (epc_storefront_prices_visible_for_user()) {
		return '';
	}
	return "IMPORTANT — guest (not logged in):\n"
		. "- NEVER quote specific prices, currency amounts, or markups\n"
		. "- Say prices are available after login or registration\n"
		. "- You may confirm stock, brands, part numbers, and availability\n"
		. "- Direct them to log in / register to see retail or wholesale pricing";
}

/**
 * Strip price fields from a product row (ajax / API).
 *
 * @param array<string,mixed> $product
 */
function epc_storefront_prices_redact_product(array &$product): void
{
	$priceKeys = array(
		'price', 'price_purchase', 'price_crossed_out', 'customer_price',
		'min_price', 'max_price', 'groups_price', 'groups_markup', 'groups_check_hash', 'check_hash',
	);
	foreach ($priceKeys as $key) {
		if (array_key_exists($key, $product)) {
			if ($key === 'groups_price' || $key === 'groups_markup' || $key === 'groups_check_hash') {
				$product[$key] = array();
			} else {
				$product[$key] = 0;
			}
		}
	}
}

/**
 * @param array<int,array<string,mixed>> $products
 */
function epc_storefront_prices_redact_products(array &$products): void
{
	foreach ($products as &$product) {
		if (is_array($product)) {
			epc_storefront_prices_redact_product($product);
		}
	}
	unset($product);
}

/**
 * @param array<int,array<string,mixed>> $rows
 */
function epc_storefront_prices_redact_brand_parts_rows(array &$rows): void
{
	foreach ($rows as &$row) {
		if (!is_array($row)) {
			continue;
		}
		if (array_key_exists('price', $row)) {
			$row['price'] = null;
		}
	}
	unset($row);
}
