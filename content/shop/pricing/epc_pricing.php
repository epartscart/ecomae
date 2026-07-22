<?php
if (!function_exists('epc_pricing_normalize_brand')) {
	function epc_pricing_normalize_brand($brand)
	{
		return mb_strtoupper(trim((string)$brand), 'UTF-8');
	}
}

if (!function_exists('epc_pricing_normalize_article')) {
	function epc_pricing_normalize_article($article)
	{
		return mb_strtoupper(preg_replace('/[^a-zA-Z0-9А-Яа-яёЁ]+/ui', '', (string)$article), 'UTF-8');
	}
}

if (!function_exists('epc_pricing_get_setting')) {
	function epc_pricing_get_setting($db_link, $key, $default = '')
	{
		try {
			$stmt = $db_link->prepare('SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1;');
			$stmt->execute(array($key));
			$value = $stmt->fetchColumn();
			return ($value === false) ? $default : $value;
		} catch (Exception $e) {
			return $default;
		}
	}
}

if (!function_exists('epc_pricing_default_guest_retail_margin')) {
	/** Hard floor for guest + retail when CP margin is missing/zero. */
	function epc_pricing_default_guest_retail_margin(): float
	{
		return 40.0;
	}
}

if (!function_exists('epc_pricing_profile_code_for_group')) {
	function epc_pricing_profile_code_for_group($db_link, $group_id): string
	{
		static $cache = array();
		$group_id = (int) $group_id;
		if ($group_id <= 0) {
			return '';
		}
		if (isset($cache[$group_id])) {
			return $cache[$group_id];
		}
		$cache[$group_id] = '';
		try {
			$stmt = $db_link->prepare('SELECT `code` FROM `epc_price_profiles` WHERE `group_id` = ? LIMIT 1;');
			$stmt->execute(array($group_id));
			$code = strtolower(trim((string) $stmt->fetchColumn()));
			$cache[$group_id] = $code;
		} catch (Exception $e) {
		}
		return $cache[$group_id];
	}
}

if (!function_exists('epc_pricing_get_profile_margin_percent')) {
	function epc_pricing_get_profile_margin_percent($db_link, $group_id)
	{
		static $cache = array();
		$group_id = (int)$group_id;
		if ($group_id <= 0) {
			return 0.0;
		}
		if (isset($cache[$group_id])) {
			return $cache[$group_id];
		}
		$cache[$group_id] = 0.0;
		try {
			$stmt = $db_link->prepare('SELECT `margin_percent` FROM `epc_price_profiles` WHERE `group_id` = ? LIMIT 1;');
			$stmt->execute(array($group_id));
			$value = $stmt->fetchColumn();
			if ($value !== false && $value !== null && $value !== '') {
				$cache[$group_id] = (float)$value;
			}
		} catch (Exception $e) {
		}
		// Retail must never sell at cost — enforce 40% when profile margin is unset/zero.
		if ($cache[$group_id] <= 0.0 && epc_pricing_profile_code_for_group($db_link, $group_id) === 'retail') {
			$cache[$group_id] = epc_pricing_default_guest_retail_margin();
		}
		return $cache[$group_id];
	}
}

if (!function_exists('epc_pricing_get_brand_rule')) {
	function epc_pricing_get_brand_rule($db_link, $group_id, $brand)
	{
		static $cache = array();
		$group_id = (int)$group_id;
		$brand = epc_pricing_normalize_brand($brand);
		$key = $group_id . '|' . $brand;
		if (isset($cache[$key])) {
			return $cache[$key];
		}
		$rule = array('visible' => 1, 'margin_percent' => 0);
		if ($group_id <= 0 || $brand === '') {
			$cache[$key] = $rule;
			return $rule;
		}
		try {
			$stmt = $db_link->prepare('SELECT `visible`, `margin_percent` FROM `epc_price_profile_brand_rules` WHERE `group_id` = ? AND `manufacturer` = ? LIMIT 1;');
			$stmt->execute(array($group_id, $brand));
			$row = $stmt->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				$rule['visible'] = (int)$row['visible'];
				$rule['margin_percent'] = (float)$row['margin_percent'];
			}
		} catch (Exception $e) {
		}
		$cache[$key] = $rule;
		return $rule;
	}
}

if (!function_exists('epc_pricing_get_article_rule')) {
	function epc_pricing_get_article_rule($db_link, $group_id, $brand, $article)
	{
		static $cache = array();
		$group_id = (int)$group_id;
		$brand = epc_pricing_normalize_brand($brand);
		$article = epc_pricing_normalize_article($article);
		$key = $group_id . '|' . $brand . '|' . $article;
		if ($article === '') {
			return array('visible' => 1, 'margin_percent' => 0, 'matched' => false);
		}
		if (isset($cache[$key])) {
			return $cache[$key];
		}
		$rule = array('visible' => 1, 'margin_percent' => 0, 'matched' => false);
		if ($group_id <= 0 || $brand === '') {
			$cache[$key] = $rule;
			return $rule;
		}
		try {
			$stmt = $db_link->prepare('SELECT `visible`, `margin_percent` FROM `epc_price_profile_article_rules` WHERE `group_id` = ? AND `manufacturer` = ? AND `article` = ? LIMIT 1;');
			$stmt->execute(array($group_id, $brand, $article));
			$row = $stmt->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				$rule['visible'] = (int)$row['visible'];
				$rule['margin_percent'] = (float)$row['margin_percent'];
				$rule['matched'] = true;
			}
		} catch (Exception $e) {
		}
		$cache[$key] = $rule;
		return $rule;
	}
}

if (!function_exists('epc_pricing_is_guest_group')) {
	function epc_pricing_is_guest_group($db_link, $group_id)
	{
		static $cache = array();
		$group_id = (int)$group_id;
		if ($group_id <= 0) {
			return false;
		}
		if (isset($cache[$group_id])) {
			return $cache[$group_id];
		}
		$cache[$group_id] = false;
		try {
			$stmt = $db_link->prepare('SELECT `for_guests` FROM `groups` WHERE `id` = ? LIMIT 1;');
			$stmt->execute(array($group_id));
			$cache[$group_id] = ((int)$stmt->fetchColumn() === 1);
		} catch (Exception $e) {
		}
		return $cache[$group_id];
	}
}

if (!function_exists('epc_pricing_get_guest_margin_percent')) {
	function epc_pricing_get_guest_margin_percent($db_link)
	{
		$v = (float) epc_pricing_get_setting($db_link, 'guest_margin_percent', '40.00');
		// Guests must never see cost — floor at 40% when unset/zero.
		if ($v <= 0.0) {
			return epc_pricing_default_guest_retail_margin();
		}
		return $v;
	}
}

if (!function_exists('epc_pricing_resolve_customer_group_id')) {
	/**
	 * Prefer assigned price-profile group for approved customers; else first user group / guest.
	 */
	function epc_pricing_resolve_customer_group_id($db_link, $user_id, $fallback_group_id = 0): int
	{
		$user_id = (int) $user_id;
		$fallback_group_id = (int) $fallback_group_id;
		if ($user_id > 0) {
			$profile_gid = epc_pricing_get_user_profile_group_id($db_link, $user_id);
			if ($profile_gid > 0) {
				return $profile_gid;
			}
		}
		if ($fallback_group_id > 0) {
			return $fallback_group_id;
		}
		if ($user_id <= 0) {
			try {
				$gid = (int) $db_link->query('SELECT `id` FROM `groups` WHERE `for_guests` = 1 ORDER BY `id` ASC LIMIT 1')->fetchColumn();
				if ($gid > 0) {
					return $gid;
				}
			} catch (Exception $e) {
			}
		}
		return 0;
	}
}

if (!function_exists('epc_pricing_ensure_storage_schema')) {
	/**
	 * Warehouse / supplier margin tables (top → bottom: supplier → brand → article).
	 */
	function epc_pricing_ensure_storage_schema($db_link): void
	{
		static $done = false;
		if ($done) {
			return;
		}
		$done = true;
		try {
			$db_link->exec(
				"CREATE TABLE IF NOT EXISTS `epc_price_storage_rules` (
					`id` int(11) NOT NULL AUTO_INCREMENT,
					`storage_id` int(11) NOT NULL,
					`margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
					`visible` tinyint(1) NOT NULL DEFAULT 1,
					`updated_at` int(11) NOT NULL DEFAULT 0,
					PRIMARY KEY (`id`),
					UNIQUE KEY `x_storage` (`storage_id`)
				) ENGINE=InnoDB DEFAULT CHARSET=utf8;"
			);
			$db_link->exec(
				"CREATE TABLE IF NOT EXISTS `epc_price_storage_brand_rules` (
					`id` int(11) NOT NULL AUTO_INCREMENT,
					`storage_id` int(11) NOT NULL,
					`manufacturer` varchar(255) NOT NULL,
					`margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
					`visible` tinyint(1) NOT NULL DEFAULT 1,
					`updated_at` int(11) NOT NULL DEFAULT 0,
					PRIMARY KEY (`id`),
					UNIQUE KEY `x_storage_brand` (`storage_id`, `manufacturer`),
					KEY `x_manufacturer` (`manufacturer`)
				) ENGINE=InnoDB DEFAULT CHARSET=utf8;"
			);
			$db_link->exec(
				"CREATE TABLE IF NOT EXISTS `epc_price_storage_article_rules` (
					`id` int(11) NOT NULL AUTO_INCREMENT,
					`storage_id` int(11) NOT NULL,
					`manufacturer` varchar(255) NOT NULL,
					`article` varchar(64) NOT NULL,
					`margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
					`visible` tinyint(1) NOT NULL DEFAULT 1,
					`updated_at` int(11) NOT NULL DEFAULT 0,
					PRIMARY KEY (`id`),
					UNIQUE KEY `x_storage_brand_article` (`storage_id`, `manufacturer`, `article`),
					KEY `x_article` (`article`)
				) ENGINE=InnoDB DEFAULT CHARSET=utf8;"
			);
		} catch (Exception $e) {
		}
	}
}

if (!function_exists('epc_pricing_get_storage_rule')) {
	function epc_pricing_get_storage_rule($db_link, $storage_id)
	{
		static $cache = array();
		$storage_id = (int) $storage_id;
		if ($storage_id <= 0) {
			return array('visible' => 1, 'margin_percent' => 0.0, 'matched' => false);
		}
		if (isset($cache[$storage_id])) {
			return $cache[$storage_id];
		}
		$rule = array('visible' => 1, 'margin_percent' => 0.0, 'matched' => false);
		try {
			epc_pricing_ensure_storage_schema($db_link);
			$stmt = $db_link->prepare(
				'SELECT `visible`, `margin_percent` FROM `epc_price_storage_rules` WHERE `storage_id` = ? LIMIT 1;'
			);
			$stmt->execute(array($storage_id));
			$row = $stmt->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				$rule['visible'] = (int) $row['visible'];
				$rule['margin_percent'] = (float) $row['margin_percent'];
				$rule['matched'] = true;
			}
		} catch (Exception $e) {
		}
		$cache[$storage_id] = $rule;
		return $rule;
	}
}

if (!function_exists('epc_pricing_get_storage_brand_rule')) {
	function epc_pricing_get_storage_brand_rule($db_link, $storage_id, $brand)
	{
		static $cache = array();
		$storage_id = (int) $storage_id;
		$brand = epc_pricing_normalize_brand($brand);
		$key = $storage_id . '|' . $brand;
		if ($storage_id <= 0 || $brand === '') {
			return array('visible' => 1, 'margin_percent' => 0.0, 'matched' => false);
		}
		if (isset($cache[$key])) {
			return $cache[$key];
		}
		$rule = array('visible' => 1, 'margin_percent' => 0.0, 'matched' => false);
		try {
			epc_pricing_ensure_storage_schema($db_link);
			$stmt = $db_link->prepare(
				'SELECT `visible`, `margin_percent` FROM `epc_price_storage_brand_rules`
				 WHERE `storage_id` = ? AND `manufacturer` = ? LIMIT 1;'
			);
			$stmt->execute(array($storage_id, $brand));
			$row = $stmt->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				$rule['visible'] = (int) $row['visible'];
				$rule['margin_percent'] = (float) $row['margin_percent'];
				$rule['matched'] = true;
			}
		} catch (Exception $e) {
		}
		$cache[$key] = $rule;
		return $rule;
	}
}

if (!function_exists('epc_pricing_get_storage_article_rule')) {
	function epc_pricing_get_storage_article_rule($db_link, $storage_id, $brand, $article)
	{
		static $cache = array();
		$storage_id = (int) $storage_id;
		$brand = epc_pricing_normalize_brand($brand);
		$article = epc_pricing_normalize_article($article);
		$key = $storage_id . '|' . $brand . '|' . $article;
		if ($storage_id <= 0 || $brand === '' || $article === '') {
			return array('visible' => 1, 'margin_percent' => 0.0, 'matched' => false);
		}
		if (isset($cache[$key])) {
			return $cache[$key];
		}
		$rule = array('visible' => 1, 'margin_percent' => 0.0, 'matched' => false);
		try {
			epc_pricing_ensure_storage_schema($db_link);
			$stmt = $db_link->prepare(
				'SELECT `visible`, `margin_percent` FROM `epc_price_storage_article_rules`
				 WHERE `storage_id` = ? AND `manufacturer` = ? AND `article` = ? LIMIT 1;'
			);
			$stmt->execute(array($storage_id, $brand, $article));
			$row = $stmt->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				$rule['visible'] = (int) $row['visible'];
				$rule['margin_percent'] = (float) $row['margin_percent'];
				$rule['matched'] = true;
			}
		} catch (Exception $e) {
		}
		$cache[$key] = $rule;
		return $rule;
	}
}

if (!function_exists('epc_pricing_apply_sell_from_purchase')) {
	/**
	 * Authoritative sell price from warehouse purchase using CP price-management stack.
	 * Stack: supplier → supplier brand → supplier article → profile → brand → article → guest.
	 *
	 * @return array{visible:bool, price:float, markup_percent:int, markup_decimal:float, purchase:float}
	 */
	function epc_pricing_apply_sell_from_purchase($db_link, $group_id, $brand, $purchase, $article = '', $storage_id = 0): array
	{
		$purchase = (float) $purchase;
		$group_id = (int) $group_id;
		$storage_id = (int) $storage_id;
		$result = epc_pricing_apply_price_rules($db_link, $group_id, $brand, $purchase, 0.0, $article, $storage_id);
		$price = (float) ($result['price'] ?? $purchase);
		$markup_decimal = (float) ($result['markup_decimal'] ?? 0.0);
		// Safety net: guest/retail/unprofiled customers never leave at cost.
		// Wholesale keeps the approved profile margin only (may be below retail floor).
		if ($purchase > 0 && $price <= $purchase) {
			$floor = 0.0;
			$code = epc_pricing_profile_code_for_group($db_link, $group_id);
			if (epc_pricing_is_guest_group($db_link, $group_id)) {
				$floor = epc_pricing_get_guest_margin_percent($db_link);
			} elseif ($code === 'wholesale') {
				$floor = epc_pricing_get_profile_margin_percent($db_link, $group_id);
			} else {
				// retail profile, registered-but-unprofiled, or other non-B2B groups
				$floor = epc_pricing_get_profile_margin_percent($db_link, $group_id);
				if ($floor <= 0.0) {
					$floor = epc_pricing_default_guest_retail_margin();
				}
			}
			if ($floor > 0) {
				list($price, $markup_decimal) = epc_pricing_apply_margin_step($purchase, 0.0, $floor);
			}
		}
		return array(
			'visible' => !empty($result['visible']),
			'price' => (float) $price,
			'markup_percent' => (int) round($markup_decimal * 100),
			'markup_decimal' => $markup_decimal,
			'purchase' => $purchase,
			'breakdown' => isset($result['breakdown']) ? $result['breakdown'] : array(),
		);
	}
}

if (!function_exists('epc_pricing_line_has_positive_margin')) {
	/** True when sell is strictly above purchase (positive margin). */
	function epc_pricing_line_has_positive_margin($sell_price, $purchase_price): bool
	{
		$sell = (float) $sell_price;
		$purchase = (float) $purchase_price;
		if ($sell <= 0 || $purchase <= 0) {
			return false;
		}
		return $sell > $purchase + 0.0001;
	}
}

if (!function_exists('epc_pricing_customer_safe_no_margin_message')) {
	/** Storefront-safe copy — never expose markup %, purchase, or B2B profile policy. */
	function epc_pricing_customer_safe_no_margin_message(string $context = 'cart'): string
	{
		if ($context === 'checkout') {
			return 'Unable to place this order right now. Please refresh the page, remove any unavailable items, and try again. If the problem continues, contact support.';
		}
		return 'Unable to add this item to your cart right now. Please refresh the page and try again. If the problem continues, contact support.';
	}
}

if (!function_exists('epc_pricing_offer_allows_cart')) {
	/**
	 * Cart/checkout gate that does not false-fail when cost is redacted or unknown.
	 * - markup > 0 ⇒ pricing already applied on the offer
	 * - purchase <= 0 ⇒ cost unknown (guest redaction / CHPU seed) — do not block retail
	 * - sell > purchase ⇒ positive margin
	 * - sell ≈ purchase with no markup ⇒ treat as seed/clone (not a trusted cost), allow
	 * - sell < purchase ⇒ reject (below cost)
	 */
	function epc_pricing_offer_allows_cart($sell_price, $purchase_price, $markup_percent = 0): bool
	{
		$sell = (float) $sell_price;
		$purchase = (float) $purchase_price;
		$markup = (float) $markup_percent;
		if ($sell <= 0) {
			return false;
		}
		if ($markup > 0) {
			return true;
		}
		if ($purchase <= 0) {
			return true;
		}
		if ($sell > $purchase + 0.0001) {
			return true;
		}
		// purchase cloned from sell (common on CHPU stock rows) — not a real cost leak signal
		if (abs($sell - $purchase) <= 0.0001) {
			return true;
		}
		return false;
	}
}

if (!function_exists('epc_pricing_get_profile_vat_percent')) {
	function epc_pricing_get_profile_vat_percent($db_link, $group_id)
	{
		$default_vat = (float)epc_pricing_get_setting($db_link, 'vat_percent', '5.00');
		$group_id = (int)$group_id;
		if ($group_id <= 0) {
			return $default_vat;
		}
		try {
			$stmt = $db_link->prepare('SELECT `vat_percent` FROM `epc_price_profiles` WHERE `group_id` = ? LIMIT 1;');
			$stmt->execute(array($group_id));
			$value = $stmt->fetchColumn();
			if ($value !== false && $value !== null && $value !== '') {
				return (float)$value;
			}
		} catch (Exception $e) {
		}
		return $default_vat;
	}
}

if (!function_exists('epc_pricing_get_user_profile_group_id')) {
	function epc_pricing_get_user_profile_group_id($db_link, $user_id)
	{
		$user_id = (int)$user_id;
		if ($user_id <= 0) {
			return 0;
		}
		try {
			$stmt = $db_link->prepare('SELECT `users_groups_bind`.`group_id` FROM `users_groups_bind` INNER JOIN `epc_price_profiles` ON `epc_price_profiles`.`group_id` = `users_groups_bind`.`group_id` WHERE `users_groups_bind`.`user_id` = ? ORDER BY `users_groups_bind`.`record_id` DESC LIMIT 1;');
			$stmt->execute(array($user_id));
			return (int)$stmt->fetchColumn();
		} catch (Exception $e) {
			return 0;
		}
	}
}

if (!function_exists('epc_pricing_apply_margin_step')) {
	function epc_pricing_apply_margin_step($price, $markup_decimal, $margin_percent)
	{
		$margin_percent = (float)$margin_percent;
		if ($margin_percent == 0.0) {
			return array((float)$price, (float)$markup_decimal);
		}
		$price = (float)$price + ((float)$price * ($margin_percent / 100));
		$markup_decimal = (float)$markup_decimal + ($margin_percent / 100);
		return array($price, $markup_decimal);
	}
}

/**
 * Apply warehouse + profile margins. Returns breakdown for CP demo.
 *
 * Order (top → bottom):
 *   Supplier overall → Supplier brand → Supplier article
 *   → Profile overall → Profile brand → Profile article → Guest
 *
 * @return array{visible:bool, price:float, markup_decimal:float, brand_margin_percent:float, breakdown:array}
 */
if (!function_exists('epc_pricing_apply_price_rules')) {
	function epc_pricing_apply_price_rules($db_link, $group_id, $brand, $price, $markup_decimal, $article = '', $storage_id = 0)
	{
		$group_id = (int)$group_id;
		$storage_id = (int)$storage_id;
		$brand = epc_pricing_normalize_brand($brand);
		$article = epc_pricing_normalize_article($article);
		$base_price = (float)$price;
		$breakdown = array(
			'base_price' => round($base_price, 2),
			'steps' => array(),
			'final_price' => round($base_price, 2),
			'total_margin_percent' => 0.0,
			'storage_id' => $storage_id,
		);

		// --- Warehouse / supplier visibility (before any margin) ---
		$storage_rule = epc_pricing_get_storage_rule($db_link, $storage_id);
		if (!empty($storage_rule['matched']) && (int) $storage_rule['visible'] === 0) {
			return array(
				'visible' => false,
				'price' => $base_price,
				'markup_decimal' => (float)$markup_decimal,
				'brand_margin_percent' => 0,
				'breakdown' => $breakdown,
				'hidden_reason' => 'Supplier / warehouse hidden',
			);
		}
		$storage_brand_rule = epc_pricing_get_storage_brand_rule($db_link, $storage_id, $brand);
		if (!empty($storage_brand_rule['matched']) && (int) $storage_brand_rule['visible'] === 0) {
			return array(
				'visible' => false,
				'price' => $base_price,
				'markup_decimal' => (float)$markup_decimal,
				'brand_margin_percent' => 0,
				'breakdown' => $breakdown,
				'hidden_reason' => 'Brand hidden for this supplier',
			);
		}
		$storage_article_rule = epc_pricing_get_storage_article_rule($db_link, $storage_id, $brand, $article);
		if (!empty($storage_article_rule['matched']) && (int) $storage_article_rule['visible'] === 0) {
			return array(
				'visible' => false,
				'price' => $base_price,
				'markup_decimal' => (float)$markup_decimal,
				'brand_margin_percent' => 0,
				'breakdown' => $breakdown,
				'hidden_reason' => 'Article hidden for this supplier',
			);
		}

		$brand_rule = epc_pricing_get_brand_rule($db_link, $group_id, $brand);
		if ((int)$brand_rule['visible'] === 0) {
			return array(
				'visible' => false,
				'price' => $base_price,
				'markup_decimal' => (float)$markup_decimal,
				'brand_margin_percent' => 0,
				'breakdown' => $breakdown,
				'hidden_reason' => 'Brand hidden for this profile',
			);
		}

		$article_rule = epc_pricing_get_article_rule($db_link, $group_id, $brand, $article);
		if (!empty($article_rule['matched']) && (int)$article_rule['visible'] === 0) {
			return array(
				'visible' => false,
				'price' => $base_price,
				'markup_decimal' => (float)$markup_decimal,
				'brand_margin_percent' => (float)$brand_rule['margin_percent'],
				'breakdown' => $breakdown,
				'hidden_reason' => 'Article hidden for this profile',
			);
		}

		// --- 1–3: Supplier stack (warehouse → brand → article) ---
		if (!empty($storage_rule['matched'])) {
			$storage_margin = (float) $storage_rule['margin_percent'];
			if ($storage_margin != 0.0) {
				list($price, $markup_decimal) = epc_pricing_apply_margin_step($price, $markup_decimal, $storage_margin);
				$breakdown['steps'][] = array(
					'type' => 'supplier',
					'label' => 'Supplier overall margin',
					'percent' => $storage_margin,
					'price_after' => round((float)$price, 2),
				);
			}
		}
		if (!empty($storage_brand_rule['matched'])) {
			$sb_margin = (float) $storage_brand_rule['margin_percent'];
			if ($sb_margin != 0.0) {
				list($price, $markup_decimal) = epc_pricing_apply_margin_step($price, $markup_decimal, $sb_margin);
				$breakdown['steps'][] = array(
					'type' => 'supplier_brand',
					'label' => 'Supplier brand margin (' . $brand . ')',
					'percent' => $sb_margin,
					'price_after' => round((float)$price, 2),
				);
			}
		}
		if (!empty($storage_article_rule['matched'])) {
			$sa_margin = (float) $storage_article_rule['margin_percent'];
			if ($sa_margin != 0.0) {
				list($price, $markup_decimal) = epc_pricing_apply_margin_step($price, $markup_decimal, $sa_margin);
				$breakdown['steps'][] = array(
					'type' => 'supplier_article',
					'label' => 'Supplier article margin (' . $brand . ' ' . $article . ')',
					'percent' => $sa_margin,
					'price_after' => round((float)$price, 2),
				);
			}
		}

		// --- 4–7: Customer profile stack ---
		$profile_margin = epc_pricing_get_profile_margin_percent($db_link, $group_id);
		if ($profile_margin != 0.0) {
			list($price, $markup_decimal) = epc_pricing_apply_margin_step($price, $markup_decimal, $profile_margin);
			$breakdown['steps'][] = array('type' => 'profile', 'label' => 'Profile overall margin', 'percent' => $profile_margin, 'price_after' => round((float)$price, 2));
		}

		$brand_margin_percent = (float)$brand_rule['margin_percent'];
		if ($brand_margin_percent != 0.0) {
			list($price, $markup_decimal) = epc_pricing_apply_margin_step($price, $markup_decimal, $brand_margin_percent);
			$breakdown['steps'][] = array('type' => 'brand', 'label' => 'Brand margin (' . $brand . ')', 'percent' => $brand_margin_percent, 'price_after' => round((float)$price, 2));
		}

		if (!empty($article_rule['matched'])) {
			$article_margin = (float)$article_rule['margin_percent'];
			if ($article_margin != 0.0) {
				list($price, $markup_decimal) = epc_pricing_apply_margin_step($price, $markup_decimal, $article_margin);
				$breakdown['steps'][] = array('type' => 'article', 'label' => 'Article margin (' . $brand . ' ' . $article . ')', 'percent' => $article_margin, 'price_after' => round((float)$price, 2));
			}
		}

		$guest_margin_percent = epc_pricing_is_guest_group($db_link, $group_id) ? epc_pricing_get_guest_margin_percent($db_link) : 0.0;
		if ($guest_margin_percent != 0.0) {
			list($price, $markup_decimal) = epc_pricing_apply_margin_step($price, $markup_decimal, $guest_margin_percent);
			$breakdown['steps'][] = array('type' => 'guest', 'label' => 'Guest / non-login margin', 'percent' => $guest_margin_percent, 'price_after' => round((float)$price, 2));
		}

		$breakdown['final_price'] = round((float)$price, 2);
		if ($base_price > 0) {
			$breakdown['total_margin_percent'] = round((($breakdown['final_price'] - $base_price) / $base_price) * 100, 2);
		}

		return array(
			'visible' => true,
			'price' => (float)$price,
			'markup_decimal' => (float)$markup_decimal,
			'brand_margin_percent' => $brand_margin_percent,
			'breakdown' => $breakdown,
			'hidden_reason' => '',
		);
	}
}

if (!function_exists('epc_pricing_apply_brand_rule')) {
	function epc_pricing_apply_brand_rule($db_link, $group_id, $brand, $price, $markup_decimal, $article = '', $storage_id = 0)
	{
		$result = epc_pricing_apply_price_rules($db_link, $group_id, $brand, $price, $markup_decimal, $article, $storage_id);
		return array(
			'visible' => $result['visible'],
			'price' => $result['price'],
			'markup_decimal' => $result['markup_decimal'],
			'brand_margin_percent' => $result['brand_margin_percent'],
			'breakdown' => isset($result['breakdown']) ? $result['breakdown'] : array(),
			'hidden_reason' => isset($result['hidden_reason']) ? $result['hidden_reason'] : '',
		);
	}
}
?>
