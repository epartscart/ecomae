<?php
/**
 * Storefront price visibility — hide prices for guests on warehouse_supplier tenants (epartscart first).
 * Also gates guest cart / quote / product WhatsApp on the same tenants.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

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

/**
 * Cart / quote / product WhatsApp — same guest gate as storefront prices.
 */
function epc_storefront_commerce_allowed_for_user(?int $userId = null): bool
{
	return epc_storefront_prices_visible_for_user($userId);
}

/**
 * True when current guest must not use cart, quote, or product WhatsApp.
 */
function epc_storefront_guest_commerce_blocked(?int $userId = null): bool
{
	return !epc_storefront_commerce_allowed_for_user($userId);
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

function epc_storefront_commerce_login_cta_html(?array $multilang_params = null): string
{
	if ($multilang_params === null && !empty($GLOBALS['multilang_params']) && is_array($GLOBALS['multilang_params'])) {
		$multilang_params = $GLOBALS['multilang_params'];
	}
	$login = htmlspecialchars(epc_storefront_auth_login_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$signup = htmlspecialchars(epc_storefront_auth_signup_url($multilang_params), ENT_QUOTES, 'UTF-8');
	return '<div class="epc-commerce-login-cta">'
		. '<a class="btn btn-sm btn-primary" href="' . $login . '">Log in</a>'
		. '<span class="epc-commerce-login-cta__sep"> or </span>'
		. '<a class="btn btn-sm btn-default" href="' . $signup . '">register</a>'
		. '<div class="epc-commerce-login-cta__hint">to buy, request a quote, or WhatsApp</div>'
		. '</div>';
}

/**
 * JSON error payload for AJAX cart/quote endpoints when guest commerce is blocked.
 *
 * @return array{status:bool,code:string,message:string,login_url:string}
 */
function epc_storefront_guest_commerce_denied_payload(?array $multilang_params = null): array
{
	if ($multilang_params === null && !empty($GLOBALS['multilang_params']) && is_array($GLOBALS['multilang_params'])) {
		$multilang_params = $GLOBALS['multilang_params'];
	}
	return array(
		'status' => false,
		'code' => 'auth',
		'message' => 'Please log in or register to continue.',
		'login_url' => epc_storefront_auth_login_url($multilang_params),
	);
}

/**
 * Delete guest session cart rows (used when guest commerce is blocked).
 */
function epc_storefront_clear_guest_cart(PDO $db, int $sessionId): void
{
	if ($sessionId <= 0) {
		return;
	}
	try {
		$ids = array();
		$q = $db->prepare('SELECT `id` FROM `shop_carts` WHERE `user_id` = 0 AND `session_id` = ?');
		$q->execute(array($sessionId));
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$ids[] = (int) $row['id'];
		}
		if ($ids === array()) {
			return;
		}
		$ph = implode(',', array_fill(0, count($ids), '?'));
		$db->prepare('DELETE FROM `shop_carts_details` WHERE `cart_record_id` IN (' . $ph . ')')->execute($ids);
		$db->prepare('DELETE FROM `shop_carts` WHERE `user_id` = 0 AND `session_id` = ?')->execute(array($sessionId));
	} catch (Throwable $e) {
		// Best-effort cleanup; UI gate still applies.
	}
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
		. '.epc-commerce-login-cta{display:flex;flex-direction:column;align-items:flex-start;gap:6px;max-width:180px}'
		. '.epc-commerce-login-cta .btn{margin:0}'
		. '.epc-commerce-login-cta__sep{font-size:12px;color:#94a3b8}'
		. '.epc-commerce-login-cta__hint{font-size:11px;line-height:1.35;color:#64748b}'
		. '.epc-cart-login-gate{max-width:520px;margin:32px auto;padding:28px 24px;text-align:center;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px}'
		. '.epc-cart-login-gate h2{margin:0 0 10px;font-size:22px;color:#0f172a}'
		. '.epc-cart-login-gate p{margin:0 0 18px;color:#475569}'
		. '.epc-cart-login-gate .epc-commerce-login-cta{align-items:center;max-width:none;flex-direction:row;flex-wrap:wrap;justify-content:center}'
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
		. "- Do NOT offer add to cart, add to quote, or WhatsApp ordering for guests\n"
		. "- You may confirm stock, brands, part numbers, and availability\n"
		. "- Direct them to log in / register to see prices and place orders";
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

/**
 * Ensure every product row has a customer-facing warehouse (storage) caption.
 *
 * @param array<int,array<string,mixed>> $products
 * @param PDO $db
 */
function epc_storefront_fill_warehouse_captions(array &$products, PDO $db): void
{
	if ($products === array()) {
		return;
	}
	$needIds = array();
	foreach ($products as $product) {
		if (!is_array($product)) {
			continue;
		}
		$caption = trim((string) ($product['storage_caption'] ?? ''));
		$sid = (int) ($product['storage_id'] ?? 0);
		if ($caption === '' && $sid > 0) {
			$needIds[$sid] = true;
		}
	}
	if ($needIds === array()) {
		return;
	}
	$ids = array_keys($needIds);
	$ph = implode(',', array_fill(0, count($ids), '?'));
	$map = array();
	try {
		$q = $db->prepare(
			'SELECT `id`, `name`, `short_name` FROM `shop_storages` WHERE `id` IN (' . $ph . ')'
		);
		$q->execute($ids);
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$label = trim((string) ($row['short_name'] ?? ''));
			if ($label === '') {
				$label = trim((string) ($row['name'] ?? ''));
			}
			$map[(int) $row['id']] = $label;
		}
	} catch (Throwable $e) {
		return;
	}
	foreach ($products as &$product) {
		if (!is_array($product)) {
			continue;
		}
		if (trim((string) ($product['storage_caption'] ?? '')) !== '') {
			continue;
		}
		$sid = (int) ($product['storage_id'] ?? 0);
		if ($sid > 0 && !empty($map[$sid])) {
			$product['storage_caption'] = $map[$sid];
		}
	}
	unset($product);
}

/**
 * Backend / ERP department group IDs (root for_backend=1 + descendants).
 *
 * @return array<int, int>
 */
function epc_storefront_backend_group_ids(PDO $db): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$erp_access = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
	if (is_file($erp_access)) {
		require_once $erp_access;
		if (function_exists('epc_erp_backend_group_ids')) {
			$cached = array_map('intval', epc_erp_backend_group_ids($db));
			return $cached;
		}
	}
	$ids = array();
	try {
		$st = $db->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
		$root = $st ? $st->fetch(PDO::FETCH_ASSOC) : false;
		if (!$root) {
			$cached = array();
			return $cached;
		}
		$collect = function ($parentId) use ($db, &$collect, &$ids) {
			$ids[(int) $parentId] = (int) $parentId;
			$ch = $db->prepare('SELECT `id`, `count` FROM `groups` WHERE `parent` = ?');
			$ch->execute(array((int) $parentId));
			while ($row = $ch->fetch(PDO::FETCH_ASSOC)) {
				$ids[(int) $row['id']] = (int) $row['id'];
				if ((int) $row['count'] > 0) {
					$collect((int) $row['id']);
				}
			}
		};
		$collect((int) $root['id']);
	} catch (Throwable $e) {
		$ids = array();
	}
	$cached = array_values($ids);
	return $cached;
}

/**
 * True when a groups.value label is an ERP department role, not a pricing profile.
 */
function epc_storefront_group_label_is_erp($label): bool
{
	$label = trim((string) $label);
	if ($label === '') {
		return false;
	}
	return (bool) preg_match('/\(\s*ERP\s*\)/i', $label);
}

/**
 * Storefront pricing profiles for the admin "view as" margin dropdown.
 * Excludes ERP department roles under the backend tree; keeps customer pricing
 * groups (Retail, Wholesale, CIS, GCC, Visitors, etc.) and the Administrators root.
 *
 * @return array<int, array<string, mixed>>
 */
function epc_storefront_pricing_profile_groups(PDO $db): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}

	$backend_ids = array_fill_keys(epc_storefront_backend_group_ids($db), true);
	$out = array();
	try {
		$q = $db->query('SELECT * FROM `groups` ORDER BY `order` ASC, `id` ASC');
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$id = (int) ($row['id'] ?? 0);
			if ($id <= 1) {
				continue;
			}
			$label = isset($row['value']) ? (string) $row['value'] : '';
			if (epc_storefront_group_label_is_erp($label)) {
				continue;
			}
			// Drop ERP/backend department children; keep the for_backend root (Administrators).
			if (isset($backend_ids[$id]) && empty($row['for_backend'])) {
				continue;
			}
			$out[] = $row;
		}
	} catch (Throwable $e) {
		$out = array();
	}
	$cached = $out;
	return $cached;
}

function epc_storefront_is_pricing_profile_group_id(PDO $db, $group_id): bool
{
	$group_id = (int) $group_id;
	if ($group_id <= 0) {
		return false;
	}
	foreach (epc_storefront_pricing_profile_groups($db) as $row) {
		if ((int) $row['id'] === $group_id) {
			return true;
		}
	}
	return false;
}

/**
 * @return array<int, int>
 */
function epc_storefront_pricing_profile_group_ids(PDO $db): array
{
	$ids = array();
	foreach (epc_storefront_pricing_profile_groups($db) as $row) {
		$ids[] = (int) $row['id'];
	}
	return $ids;
}
