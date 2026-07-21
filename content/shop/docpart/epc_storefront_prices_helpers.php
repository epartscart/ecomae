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
 * Placeholder shown instead of availability / term / info / price when access is denied.
 */
function epc_storefront_sensitive_mask(): string
{
	return '**';
}

/**
 * Resolve current storefront customer id (0 = guest).
 */
function epc_storefront_prices_resolve_user_id(?int $userId = null): int
{
	if ($userId !== null) {
		return (int) $userId;
	}
	if (!class_exists('DP_User')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	}
	return (int) DP_User::getUserId();
}

/**
 * Access state for sensitive offer columns (qty / term / info / price).
 *
 * @return 'ok'|'guest'|'pending'|'rejected'
 */
function epc_storefront_prices_access_state(?int $userId = null): string
{
	$userId = epc_storefront_prices_resolve_user_id($userId);
	if ($userId <= 0) {
		return epc_storefront_prices_hide_for_guests_enabled() ? 'guest' : 'ok';
	}

	global $db_link;
	$tradeFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
	if (is_file($tradeFile)) {
		require_once $tradeFile;
	}
	if (!function_exists('epc_trade_approval_status') || !($db_link instanceof PDO)) {
		// Fail open for tenants without the trade module.
		return 'ok';
	}
	$status = epc_trade_approval_status($db_link, $userId);
	if ($status === 'pending') {
		return 'pending';
	}
	if ($status === 'rejected') {
		return 'rejected';
	}
	return 'ok';
}

/**
 * True when the current (or given) user may see storefront prices,
 * availability qty, term, and warehouse info.
 *
 * Guests: hidden on warehouse_supplier tenants (epartscart).
 * Logged-in: retail is auto-approved; wholesale must be CP-approved.
 */
function epc_storefront_prices_visible_for_user(?int $userId = null): bool
{
	return epc_storefront_prices_access_state($userId) === 'ok';
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

function epc_storefront_prices_login_cta_html(?array $multilang_params = null, ?int $userId = null): string
{
	if ($multilang_params === null && !empty($GLOBALS['multilang_params']) && is_array($GLOBALS['multilang_params'])) {
		$multilang_params = $GLOBALS['multilang_params'];
	}
	$state = epc_storefront_prices_access_state($userId);
	if ($state === 'pending') {
		return '<span class="epc-price-login-cta epc-price-login-cta--pending">'
			. '<span class="epc-price-login-cta__hint">Wholesale account pending manager approval — prices unlock after CP approval</span>'
			. '</span>';
	}
	if ($state === 'rejected') {
		return '<span class="epc-price-login-cta epc-price-login-cta--rejected">'
			. '<span class="epc-price-login-cta__hint">Trade account not approved — contact support</span>'
			. '</span>';
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

function epc_storefront_commerce_login_cta_html(?array $multilang_params = null, bool $compact = false, ?int $userId = null): string
{
	if ($multilang_params === null && !empty($GLOBALS['multilang_params']) && is_array($GLOBALS['multilang_params'])) {
		$multilang_params = $GLOBALS['multilang_params'];
	}
	$state = epc_storefront_prices_access_state($userId);
	if ($state === 'pending') {
		$msg = 'Awaiting wholesale approval';
		if ($compact) {
			return '<span class="epc-commerce-login-cta epc-commerce-login-cta--inline epc-commerce-login-cta--pending">'
				. htmlspecialchars($msg, ENT_QUOTES, 'UTF-8')
				. '</span>';
		}
		return '<div class="epc-commerce-login-cta epc-commerce-login-cta--pending">'
			. '<div class="epc-commerce-login-cta__hint">' . htmlspecialchars($msg, ENT_QUOTES, 'UTF-8')
			. ' — a manager must approve your account in Control Panel before you can see prices or order.</div>'
			. '</div>';
	}
	if ($state === 'rejected') {
		$msg = 'Account not approved';
		if ($compact) {
			return '<span class="epc-commerce-login-cta epc-commerce-login-cta--inline epc-commerce-login-cta--rejected">'
				. htmlspecialchars($msg, ENT_QUOTES, 'UTF-8')
				. '</span>';
		}
		return '<div class="epc-commerce-login-cta epc-commerce-login-cta--rejected">'
			. '<div class="epc-commerce-login-cta__hint">' . htmlspecialchars($msg, ENT_QUOTES, 'UTF-8')
			. ' — contact support for trade access.</div>'
			. '</div>';
	}
	$login = htmlspecialchars(epc_storefront_auth_login_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$signup = htmlspecialchars(epc_storefront_auth_signup_url($multilang_params), ENT_QUOTES, 'UTF-8');
	// Search-result rows: one-line text next to Fitment (no stacked buttons).
	if ($compact) {
		return '<span class="epc-commerce-login-cta epc-commerce-login-cta--inline">'
			. '<a href="' . $login . '">Log in</a>'
			. '<span class="epc-commerce-login-cta__sep"> or </span>'
			. '<a href="' . $signup . '">register</a>'
			. '</span>';
	}
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
		. '.epc-commerce-login-cta--inline{display:inline-flex;flex-direction:row;flex-wrap:nowrap;align-items:center;gap:4px;max-width:none;white-space:nowrap;font-size:12px;line-height:1.2;color:#64748b}'
		. '.epc-commerce-login-cta--inline a{font-weight:700;color:#2563eb;text-decoration:none}'
		. '.epc-commerce-login-cta--inline a:hover{text-decoration:underline}'
		. '.epc-product-actions--guest{flex-wrap:nowrap;width:auto}'
		. '.epc-product-actions__tools--guest{flex-wrap:nowrap;white-space:nowrap;gap:8px}'
		. '#all_table_products .td_add_to_cart .epc-product-actions--guest{justify-content:flex-start}'
		. '.epc-cart-login-gate{max-width:520px;margin:32px auto;padding:28px 24px;text-align:center;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px}'
		. '.epc-cart-login-gate h2{margin:0 0 10px;font-size:22px;color:#0f172a}'
		. '.epc-cart-login-gate p{margin:0 0 18px;color:#475569}'
		. '.epc-cart-login-gate .epc-commerce-login-cta{align-items:center;max-width:none;flex-direction:row;flex-wrap:wrap;justify-content:center}'
		. '</style>';
}

function epc_storefront_prices_agent_guest_rules(): string
{
	$state = epc_storefront_prices_access_state();
	if ($state === 'ok') {
		return '';
	}
	if ($state === 'pending' || $state === 'rejected') {
		return "IMPORTANT — wholesale account not yet approved for prices:\n"
			. "- NEVER quote prices, stock qty, lead times, or warehouse names\n"
			. "- Say availability and pricing unlock after manager approval in Control Panel\n"
			. "- Do NOT offer add to cart, add to quote, or WhatsApp ordering\n"
			. "- You may discuss brands and part numbers only";
	}
	$mask = epc_storefront_sensitive_mask();
	return "IMPORTANT — guest (not logged in):\n"
		. "- NEVER quote specific prices, currency amounts, or markups\n"
		. "- NEVER reveal stock qty, lead time/term, or warehouse/info labels\n"
		. "- Show those fields only as {$mask} until the customer logs in\n"
		. "- Say prices and availability details are available after login or registration\n"
		. "- Retail registration is approved instantly; wholesale needs manager approval before prices\n"
		. "- Do NOT offer add to cart, add to quote, or WhatsApp ordering for guests\n"
		. "- You may confirm brands and part numbers only\n"
		. "- Direct them to log in / register to see prices and place orders";
}

/**
 * Strip price + stock/term/warehouse fields from a product row (ajax / API).
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
			} elseif ($key === 'check_hash') {
				// Empty string (not 0): cart treats "0" as a real hash and rejects as expired.
				$product[$key] = '';
			} else {
				$product[$key] = 0;
			}
		}
	}
	// Redact availability / term / warehouse so guests cannot read values from JSON or DevTools.
	// Keep exist as 1 when originally in stock so client-side "in stock" filters still include the row;
	// the UI shows the sensitive mask for the qty cell when prices are not visible.
	if (array_key_exists('exist', $product)) {
		$product['exist'] = ((float) $product['exist'] > 0) ? 1 : 0;
	}
	if (array_key_exists('time_to_exe', $product)) {
		$product['time_to_exe'] = 0;
	}
	if (array_key_exists('time_to_exe_guaranteed', $product)) {
		$product['time_to_exe_guaranteed'] = 0;
	}
	if (array_key_exists('probability', $product)) {
		$product['probability'] = 0;
	}
	if (array_key_exists('storage_caption', $product)) {
		$product['storage_caption'] = '';
	}
	if (array_key_exists('office_caption', $product)) {
		$product['office_caption'] = '';
	}
	if (array_key_exists('storage', $product)) {
		$product['storage'] = '';
	}
	if (array_key_exists('storage_id', $product)) {
		$product['storage_id'] = 0;
	}
	if (array_key_exists('office_id', $product)) {
		$product['office_id'] = 0;
	}
	if (array_key_exists('min_order', $product)) {
		$product['min_order'] = 0;
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
 * Redact brand-parts / manufacturer-browse rows (price, stock, term, warehouse).
 *
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
		if (array_key_exists('exist', $row)) {
			$row['exist'] = null;
		}
		if (array_key_exists('time_to_exe', $row)) {
			$row['time_to_exe'] = null;
		}
		if (array_key_exists('storage', $row)) {
			$row['storage'] = '';
		}
		if (array_key_exists('storage_id', $row)) {
			$row['storage_id'] = 0;
		}
		if (array_key_exists('storage_caption', $row)) {
			$row['storage_caption'] = '';
		}
	}
	unset($row);
}

/**
 * Strip warehouse name maps so guests cannot resolve storage_id → warehouse.
 *
 * @param array<int|string,mixed> $storages id => label
 * @param array<int|string,array<string,mixed>> $storagesInfo id => info
 */
function epc_storefront_prices_redact_storage_maps(array &$storages, array &$storagesInfo): void
{
	$mask = epc_storefront_sensitive_mask();
	foreach ($storages as $id => $_) {
		$storages[$id] = $mask;
	}
	foreach ($storagesInfo as $id => $info) {
		if (!is_array($info)) {
			$storagesInfo[$id] = array('name' => $mask);
			continue;
		}
		$info['name'] = $mask;
		$info['short_name'] = '';
		$info['full_name'] = '';
		$storagesInfo[$id] = $info;
	}
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
 * True when a groups.value / display label is an ERP department role, not a pricing profile.
 */
function epc_storefront_group_label_is_erp($label): bool
{
	$label = trim((string) $label);
	if ($label === '') {
		return false;
	}
	// Stored codes: EPC_ERP_DEPT_IT, EPC_ERP_TEAM, …
	if (preg_match('/^EPC_ERP_/i', $label)) {
		return true;
	}
	// Translated labels: "Information Technology (ERP)", …
	return (bool) preg_match('/\(\s*ERP\s*\)/i', $label);
}

/**
 * True when this groups row is an ERP role (by code, backend-tree child, or translated label).
 */
function epc_storefront_group_row_is_erp(array $row): bool
{
	$value = isset($row['value']) ? (string) $row['value'] : '';
	if (epc_storefront_group_label_is_erp($value)) {
		return true;
	}
	if (function_exists('translate_str_by_id') && $value !== '') {
		$translated = (string) translate_str_by_id($value);
		if ($translated !== '' && epc_storefront_group_label_is_erp($translated)) {
			return true;
		}
	}
	return false;
}

/**
 * Storefront pricing profiles for the admin "view as" margin dropdown.
 * Keeps Visitors / All users / Administrators / Retail / Wholesale / CIS / GCC.
 * Excludes ERP department roles (EPC_ERP_*).
 *
 * @return array<int, array<string, mixed>>
 */
function epc_storefront_pricing_profile_groups(PDO $db): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}

	$out = array();
	try {
		$q = $db->query('SELECT * FROM `groups` ORDER BY `order` ASC, `id` ASC');
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$id = (int) ($row['id'] ?? 0);
			if ($id < 1) {
				continue;
			}
			if (epc_storefront_group_row_is_erp($row)) {
				continue;
			}
			$value = isset($row['value']) ? (string) $row['value'] : '';
			$is_named_profile = (bool) preg_match('/^EPC_PROFILE_/i', $value);
			$is_customer_flag = !empty($row['for_guests']) || !empty($row['for_registrated']);
			$is_admin_root = !empty($row['for_backend']);
			$is_percentage_viewer = !empty($row['for_percentage']);
			// id=1 is typically "All users" (legacy pricing root shown in the switcher).
			$is_all_users_root = ($id === 1);
			if ($is_named_profile || $is_customer_flag || $is_admin_root || $is_percentage_viewer || $is_all_users_root) {
				$out[] = $row;
			}
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
