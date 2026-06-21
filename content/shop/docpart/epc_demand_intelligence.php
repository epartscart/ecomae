<?php
/**
 * Demand intelligence: country tags on articles (no per-country qty).
 * Stock is read from the global UAE price pool; crosses come from the cross-search API.
 */

require_once __DIR__ . '/docpart_article_match.php';
$_epc_iso_path = __DIR__ . '/epc_demand_country_iso.php';
if (is_readable($_epc_iso_path)) {
	require_once $_epc_iso_path;
} elseif (!function_exists('epc_demand_country_registry')) {
	/** @return array<string, array{code:string, name:string, iso2?:string}> */
	function epc_demand_country_registry(): array
	{
		return array(
			'SDN' => array('code' => 'SDN', 'name' => 'Sudan', 'iso2' => 'SD'),
			'DZA' => array('code' => 'DZA', 'name' => 'Algeria', 'iso2' => 'DZ'),
			'KEN' => array('code' => 'KEN', 'name' => 'Kenya', 'iso2' => 'KE'),
			'ARE' => array('code' => 'ARE', 'name' => 'United Arab Emirates', 'iso2' => 'AE'),
			'EGY' => array('code' => 'EGY', 'name' => 'Egypt', 'iso2' => 'EG'),
			'NGA' => array('code' => 'NGA', 'name' => 'Nigeria', 'iso2' => 'NG'),
			'SAU' => array('code' => 'SAU', 'name' => 'Saudi Arabia', 'iso2' => 'SA'),
		);
	}
	function epc_demand_iso2_to_iso3_map(): array
	{
		return array('SD' => 'SDN', 'DZ' => 'DZA', 'KE' => 'KEN', 'AE' => 'ARE', 'EG' => 'EGY', 'NG' => 'NGA', 'SA' => 'SAU');
	}
	function epc_demand_is_stock_pool_country_code(string $code): bool
	{
		return in_array($code, array('ARE', 'AE'), true);
	}
	function epc_demand_normalize_country_code(string $code): string
	{
		$code = strtoupper(preg_replace('/[^A-Z]/', '', trim($code)));
		if ($code === '') {
			return '';
		}
		$registry = epc_demand_country_registry();
		if (isset($registry[$code])) {
			return $code;
		}
		if (strlen($code) === 2) {
			$map = epc_demand_iso2_to_iso3_map();
			return isset($map[$code]) ? $map[$code] : '';
		}
		return '';
	}
	function epc_demand_migrate_country_codes_to_iso3(PDO $db): void
	{
	}
}

/**
 * Ensure global $db_link exists (AJAX scripts often only open a local PDO).
 */
function epc_demand_bootstrap_db_link(): void
{
	global $db_link;
	if (isset($db_link) && $db_link) {
		return;
	}
	if (!class_exists('DP_Config')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	}
	$cfg = new DP_Config();
	try {
		$db_link = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password
		);
		$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	} catch (Throwable $e) {
		$db_link = null;
	}
}

function epc_demand_load_dp_user(): void
{
	if (!class_exists('DP_User')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	}
}

/**
 * Storefront customer user id (0 if guest).
 */
function epc_demand_customer_user_id(): int
{
	static $checked = false;
	static $user_id = 0;
	if ($checked) {
		return $user_id;
	}
	$checked = true;
	epc_demand_bootstrap_db_link();
	epc_demand_load_dp_user();
	global $db_link;
	if (!isset($db_link) || !$db_link) {
		return 0;
	}
	try {
		$user_id = (int)DP_User::getUserId();
	} catch (Throwable $e) {
		$user_id = 0;
	}
	return $user_id;
}

/**
 * Customer session or CP admin session on the storefront.
 */
function epc_demand_is_logged_in(): bool
{
	epc_demand_bootstrap_db_link();
	epc_demand_load_dp_user();
	global $db_link;
	if (!isset($db_link) || !$db_link) {
		return false;
	}
	try {
		if ((int)DP_User::getUserId() > 0) {
			return true;
		}
		if ((int)DP_User::getAdminId() > 0) {
			return true;
		}
	} catch (Throwable $e) {
		return false;
	}
	return false;
}

function epc_demand_require_customer_login(bool $json = true): void
{
	if (epc_demand_is_logged_in()) {
		return;
	}
	if ($json) {
		header('Content-Type: application/json; charset=utf-8');
		echo json_encode(array(
			'status' => false,
			'code' => 'auth',
			'message' => 'Please sign in to use Country - Vehicle & Parts Intelligence AI.',
		), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		exit;
	}
}

/**
 * Staff / CP admin: may pick any demand country in the wizard.
 */
function epc_demand_user_can_see_all_countries(): bool
{
	epc_demand_bootstrap_db_link();
	epc_demand_load_dp_user();
	global $db_link;
	if (!isset($db_link) || !$db_link) {
		return false;
	}
	try {
		if (DP_User::isAdmin()) {
			return true;
		}
		if ((int)DP_User::getAdminId() > 0) {
			return true;
		}
		if (DP_User::isAdminGroup()) {
			return true;
		}
		if (DP_User::isBackendGroup()) {
			return true;
		}
		$user_id = (int)DP_User::getUserId();
		if ($user_id > 0) {
			if (DP_User::isBackendGroupById($user_id)) {
				return true;
			}
			if (epc_demand_user_in_administrator_groups($user_id)) {
				return true;
			}
			if (epc_demand_profile_allows_all_countries($user_id)) {
				return true;
			}
		}
	} catch (Throwable $e) {
	}
	return false;
}

/**
 * Administrator group (RU / EN naming in `groups.value`).
 */
function epc_demand_user_in_administrator_groups(int $user_id): bool
{
	global $db_link;
	if ($user_id <= 0 || !isset($db_link) || !$db_link) {
		return false;
	}
	try {
		$group_stmt = $db_link->query(
			'SELECT `id` FROM `groups`
			 WHERE `value` LIKE \'%Администратор%\' OR `value` LIKE \'%Administrator%\''
		);
		$group_ids = $group_stmt ? $group_stmt->fetchAll(PDO::FETCH_COLUMN) : array();
		if (!$group_ids) {
			return false;
		}
		$placeholders = implode(',', array_fill(0, count($group_ids), '?'));
		$bind_stmt = $db_link->prepare(
			'SELECT COUNT(*) FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` IN (' . $placeholders . ')'
		);
		$bind_stmt->execute(array_merge(array($user_id), array_map('intval', $group_ids)));
		return (int)$bind_stmt->fetchColumn() > 0;
	} catch (Throwable $e) {
		return false;
	}
}

/**
 * Per-user override via profile (set `epc_demand_all_countries` = 1 in users_profiles).
 */
function epc_demand_profile_allows_all_countries(int $user_id): bool
{
	global $db_link;
	if ($user_id <= 0 || !isset($db_link) || !$db_link) {
		return false;
	}
	$keys = array('epc_demand_all_countries', 'demand_intelligence_admin');
	try {
		$placeholders = implode(',', array_fill(0, count($keys), '?'));
		$stmt = $db_link->prepare(
			'SELECT `data_value` FROM `users_profiles`
			 WHERE `user_id` = ? AND `data_key` IN (' . $placeholders . ')
			 LIMIT 1'
		);
		$stmt->execute(array_merge(array($user_id), $keys));
		$val = strtolower(trim((string)$stmt->fetchColumn()));
		return in_array($val, array('1', 'yes', 'true', 'on'), true);
	} catch (Throwable $e) {
		return false;
	}
}

function epc_demand_normalize_user_country_value(string $raw): string
{
	$raw = trim($raw);
	if ($raw === '') {
		return '';
	}
	$code = epc_demand_normalize_country_code($raw);
	if ($code !== '' && !epc_demand_is_stock_pool_country_code($code)) {
		return $code;
	}
	$upper = mb_strtoupper($raw, 'UTF-8');
	$registry = epc_demand_country_registry();
	foreach ($registry as $c => $meta) {
		$name = mb_strtoupper((string)$meta['name'], 'UTF-8');
		if ($upper === $name || $upper === $c) {
			return $c;
		}
		if (strpos($upper, $name) !== false) {
			return $c;
		}
	}
	$aliases = array(
		'SUDAN' => 'SDN',
		'ALGERIA' => 'DZA',
		'KENYA' => 'KEN',
		'EGYPT' => 'EGY',
		'NIGERIA' => 'NGA',
		'SAUDI' => 'SAU',
		'SAUDI ARABIA' => 'SAU',
	);
	if (isset($aliases[$upper])) {
		return $aliases[$upper];
	}
	return '';
}

/**
 * @return array<int, string> Profile keys tried for demand country (first match wins).
 */
function epc_demand_user_country_profile_keys(): array
{
	return array('epc_demand_country', 'demand_country', 'country_code', 'country', 'market_country');
}

function epc_demand_read_user_country_from_profile(int $user_id): string
{
	global $db_link;
	if ($user_id <= 0 || !isset($db_link) || !$db_link) {
		return '';
	}
	$keys = epc_demand_user_country_profile_keys();
	$placeholders = implode(',', array_fill(0, count($keys), '?'));
	$params = array_merge(array($user_id), $keys);
	try {
		$stmt = $db_link->prepare(
			'SELECT `data_key`, `data_value` FROM `users_profiles`
			 WHERE `user_id` = ? AND `data_key` IN (' . $placeholders . ')'
		);
		$stmt->execute($params);
		$found = array();
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$found[(string)$row['data_key']] = (string)$row['data_value'];
		}
		foreach ($keys as $key) {
			if (!isset($found[$key])) {
				continue;
			}
			$code = epc_demand_normalize_user_country_value($found[$key]);
			if ($code !== '') {
				return $code;
			}
		}
	} catch (Throwable $e) {
	}
	return '';
}

function epc_demand_read_user_country_from_table(PDO $db, int $user_id): string
{
	if ($user_id <= 0) {
		return '';
	}
	try {
		epc_demand_ensure_schema($db);
		$stmt = $db->prepare('SELECT `country_code` FROM `epc_user_demand_country` WHERE `user_id` = ? LIMIT 1');
		$stmt->execute(array($user_id));
		$code = (string)$stmt->fetchColumn();
		return epc_demand_normalize_user_country_value($code);
	} catch (Throwable $e) {
		return '';
	}
}

function epc_demand_get_user_country_code(?PDO $db = null): string
{
	if (epc_demand_user_can_see_all_countries()) {
		return '';
	}
	$user_id = epc_demand_customer_user_id();
	if ($user_id <= 0) {
		return '';
	}
	$code = '';
	if ($db instanceof PDO) {
		$code = epc_demand_read_user_country_from_table($db, $user_id);
	}
	if ($code === '') {
		$code = epc_demand_read_user_country_from_profile($user_id);
	}
	return $code;
}

/**
 * Demand countries available in the UI (UAE excluded — stock pool only).
 *
 * @return array<int, string>
 */
function epc_demand_selectable_country_codes(): array
{
	$codes = array();
	foreach (epc_demand_country_registry() as $code => $meta) {
		if (epc_demand_is_stock_pool_country_code($code)) {
			continue;
		}
		$codes[] = $code;
	}
	return $codes;
}

/**
 * @return array{
 *   is_admin: bool,
 *   country_locked: bool,
 *   user_country: string,
 *   user_country_name: string,
 *   allowed_codes: array<int, string>,
 *   allowed_countries: array<int, array{code:string, name:string}>,
 *   default_country: string
 * }
 */
function epc_demand_access_context(?PDO $db = null): array
{
	$registry = epc_demand_country_registry();
	$is_admin = epc_demand_user_can_see_all_countries();
	$user_code = epc_demand_get_user_country_code($db);
	$user_name = ($user_code !== '' && isset($registry[$user_code])) ? (string)$registry[$user_code]['name'] : '';

	if ($is_admin) {
		$allowed_codes = epc_demand_selectable_country_codes();
	} elseif ($user_code !== '') {
		$allowed_codes = array($user_code);
	} else {
		$allowed_codes = array();
	}

	$allowed_countries = array();
	foreach ($allowed_codes as $code) {
		if (!isset($registry[$code])) {
			continue;
		}
		$allowed_countries[] = array(
			'code' => $code,
			'name' => (string)$registry[$code]['name'],
		);
	}

	$default = '';
	if (count($allowed_codes) === 1) {
		$default = $allowed_codes[0];
	} elseif ($is_admin && in_array('SDN', $allowed_codes, true)) {
		$default = 'SDN';
	} elseif (!empty($allowed_codes[0])) {
		$default = $allowed_codes[0];
	}

	return array(
		'is_admin' => $is_admin,
		'country_locked' => (!$is_admin && count($allowed_codes) === 1),
		'user_country' => $user_code,
		'user_country_name' => $user_name,
		'allowed_codes' => $allowed_codes,
		'allowed_countries' => $allowed_countries,
		'default_country' => $default,
	);
}

function epc_demand_assert_country_allowed(?PDO $db, string $country_code, bool $json = true): string
{
	$country_code = epc_demand_normalize_country_code($country_code);
	$ctx = epc_demand_access_context($db);
	if ($country_code !== '' && in_array($country_code, $ctx['allowed_codes'], true)) {
		return $country_code;
	}
	if ($json) {
		header('Content-Type: application/json; charset=utf-8');
		$msg = $ctx['is_admin']
			? 'Unknown country code.'
			: ($ctx['user_country'] !== ''
				? 'You can only view demand intelligence for ' . ($ctx['user_country_name'] ?: $ctx['user_country']) . '.'
				: 'No demand country is assigned to your account. Contact support.');
		echo json_encode(array(
			'status' => false,
			'code' => 'forbidden',
			'message' => $msg,
			'access' => $ctx,
		), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		exit;
	}
	return '';
}

function epc_demand_ensure_schema(PDO $db): void
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_demand_country` (
		`code` CHAR(3) NOT NULL,
		`name` VARCHAR(128) NOT NULL,
		`sort_order` INT NOT NULL DEFAULT 0,
		PRIMARY KEY (`code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_article_demand` (
		`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
		`manufacturer` VARCHAR(128) NOT NULL,
		`article_norm` VARCHAR(64) NOT NULL,
		`country_code` CHAR(3) NOT NULL,
		`source` VARCHAR(64) NOT NULL DEFAULT 'manual',
		`notes` VARCHAR(255) NOT NULL DEFAULT '',
		`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `uq_part_country` (`manufacturer`, `article_norm`, `country_code`),
		KEY `idx_article` (`article_norm`),
		KEY `idx_country` (`country_code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_price_list_demand` (
		`price_id` INT UNSIGNED NOT NULL,
		`country_code` CHAR(3) NOT NULL,
		PRIMARY KEY (`price_id`, `country_code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_user_demand_country` (
		`user_id` INT UNSIGNED NOT NULL,
		`country_code` CHAR(3) NOT NULL,
		`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
		PRIMARY KEY (`user_id`),
		KEY `idx_country` (`country_code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

	foreach (array(
		'epc_demand_country' => 'code',
		'epc_article_demand' => 'country_code',
		'epc_price_list_demand' => 'country_code',
		'epc_user_demand_country' => 'country_code',
	) as $table => $column) {
		try {
			$db->exec("ALTER TABLE `{$table}` MODIFY `{$column}` CHAR(3) NOT NULL");
		} catch (Throwable $e) {
		}
	}

	$registry = epc_demand_country_registry();
	$stmt = $db->prepare(
		'INSERT INTO `epc_demand_country` (`code`, `name`, `sort_order`) VALUES (?, ?, ?)
		 ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `sort_order` = VALUES(`sort_order`)'
	);
	$order = 0;
	foreach ($registry as $row) {
		$order += 10;
		$stmt->execute(array($row['code'], $row['name'], $order));
	}
	epc_demand_migrate_country_codes_to_iso3($db);
}

function epc_demand_seed_part_countries(PDO $db, string $manufacturer, string $article, array $country_codes, string $source = 'showcase_seed', string $notes = ''): int
{
	epc_demand_ensure_schema($db);
	$article_norm = docpart_normalize_article_for_price($article);
	$manufacturer = trim($manufacturer);
	if ($article_norm === '' || $manufacturer === '' || empty($country_codes)) {
		return 0;
	}
	$now = time();
	$stmt = $db->prepare(
		'INSERT INTO `epc_article_demand` (`manufacturer`, `article_norm`, `country_code`, `source`, `notes`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `source` = VALUES(`source`), `notes` = VALUES(`notes`)'
	);
	$inserted = 0;
	foreach ($country_codes as $code) {
		$code = epc_demand_normalize_country_code((string)$code);
		if ($code === '' || epc_demand_is_stock_pool_country_code($code)) {
			continue;
		}
		$stmt->execute(array($manufacturer, $article_norm, $code, $source, $notes, $now));
		$inserted++;
	}
	return $inserted;
}

function epc_demand_seed_demo(PDO $db, string $manufacturer = 'TOYOTA', string $article = '1310154101'): int
{
	return epc_demand_seed_part_countries(
		$db,
		$manufacturer,
		$article,
		array('SDN', 'DZA', 'KEN'),
		'demo_seed',
		'Planning demand tag (Sudan, Algeria, Kenya)'
	);
}

function epc_demand_showcase_country_pools(): array
{
	return array(
		array('SDN', 'DZA', 'KEN'),
		array('DZA', 'EGY'),
		array('KEN', 'NGA'),
		array('SDN', 'EGY', 'SAU'),
		array('DZA', 'KEN'),
		array('NGA', 'SAU'),
		array('SDN', 'NGA'),
		array('KEN', 'EGY'),
		array('DZA', 'NGA', 'KEN'),
		array('SDN', 'DZA', 'EGY', 'KEN'),
	);
}

function epc_demand_engine_keywords(): array
{
	return array(
		'engine',
		'piston',
		'gasket',
		'oil filter',
		'oil pump',
		'timing belt',
		'timing chain',
		'valve',
		'bearing',
		'cylinder',
		'head gasket',
		'crankshaft',
		'camshaft',
		'turbo',
		'injector',
		'water pump',
		'thermostat',
		'ring set',
		'overhaul',
		'conrod',
		'connecting rod',
		'liner',
		'filter element',
	);
}

function epc_demand_engine_name_sql(PDO $db): string
{
	$parts = array();
	foreach (epc_demand_engine_keywords() as $keyword) {
		$parts[] = 'LOWER(IFNULL(`name`, \'\')) LIKE ' . $db->quote('%' . mb_strtolower($keyword, 'UTF-8') . '%');
	}
	return '(' . implode(' OR ', $parts) . ')';
}

/**
 * @return array<int, array{brand:string, article:string, article_show:string, name:string, qty:float, price:mixed, warehouse:string}>
 */
function epc_demand_map_stock_rows(array $rows): array
{
	$out = array();
	foreach ($rows as $row) {
		$brand = trim((string)($row['brand'] ?? ''));
		$article = trim((string)($row['article_show'] ?? $row['article'] ?? ''));
		if ($brand === '' || $article === '') {
			continue;
		}
		$out[] = array(
			'brand' => $brand,
			'article' => $article,
			'article_show' => $article,
			'name' => trim((string)($row['name'] ?? '')),
			'qty' => (float)($row['qty'] ?? 0),
			'price' => $row['price'] ?? '',
			'warehouse' => trim((string)($row['warehouse'] ?? '')),
			'article_norm' => docpart_normalize_article_for_price($row['article'] ?? $article),
		);
	}
	return $out;
}

function epc_demand_fetch_stock_lines_query(PDO $db, ?string $extra_where, int $fetch_limit = 80): array
{
	require_once __DIR__ . '/epc_storefront_storage_flags.php';
	$article_show_expr = "COALESCE(NULLIF(TRIM(`article_show`), ''), TRIM(`article`))";
	$sql = 'SELECT TRIM(`manufacturer`) AS `brand`, TRIM(`article`) AS `article`, ' . $article_show_expr . ' AS `article_show`, '
		. 'TRIM(IFNULL(`name`, \'\')) AS `name`, IFNULL(`exist`, 0) AS `qty`, IFNULL(`price`, 0) AS `price`, '
		. 'TRIM(IFNULL(`storage`, \'\')) AS `warehouse`, `price_id` '
		. 'FROM `shop_docpart_prices_data` '
		. 'WHERE IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0 '
		. 'AND TRIM(IFNULL(`manufacturer`, \'\')) != \'\' '
		. 'AND TRIM(`article`) != \'\' '
		. 'AND ' . epc_ssf_price_data_active_sql() . ' ';
	if ($extra_where !== null && $extra_where !== '') {
		$sql .= 'AND (' . $extra_where . ') ';
	}
	$sql .= 'ORDER BY IFNULL(`exist`, 0) DESC, IFNULL(`price`, 0) ASC LIMIT ' . (int)max($fetch_limit * 8, 120);
	try {
		$stmt = $db->query($sql);
		$raw = $stmt->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		return array();
	}
	$merged = array();
	foreach (epc_demand_map_stock_rows($raw) as $row) {
		if ((float)($row['qty'] ?? 0) > 50000) {
			continue;
		}
		$key = mb_strtoupper(trim((string)$row['brand']), 'UTF-8') . '|' . docpart_normalize_article_for_price($row['article']);
		if ($key === '|') {
			continue;
		}
		if (!isset($merged[$key])) {
			$merged[$key] = $row;
			continue;
		}
		$merged[$key]['qty'] = (float)$merged[$key]['qty'] + (float)$row['qty'];
		if ((float)$row['qty'] > 0 && ((float)$merged[$key]['price'] <= 0 || (float)$row['price'] < (float)$merged[$key]['price'])) {
			$merged[$key]['price'] = $row['price'];
			$merged[$key]['warehouse'] = $row['warehouse'];
		}
		if ($merged[$key]['name'] === '' && $row['name'] !== '') {
			$merged[$key]['name'] = $row['name'];
		}
	}
	$rows = array_values($merged);
	usort($rows, function ($a, $b) {
		return ($b['qty'] <=> $a['qty']);
	});
	return array_slice($rows, 0, $fetch_limit);
}

function epc_demand_fetch_engine_stock_lines(PDO $db, int $fetch_limit = 80): array
{
	$name_filter = epc_demand_engine_name_sql($db);
	$article_hints = array('PISTON', 'GASKET', 'FILTER', 'PUMP', 'BEARING', 'VALVE', 'RING', 'BELT', 'CHAIN', 'TURBO', 'INJECT');
	$article_parts = array();
	foreach ($article_hints as $hint) {
		$article_parts[] = 'UPPER(TRIM(`article`)) LIKE ' . $db->quote('%' . $hint . '%');
	}
	$article_filter = '(' . implode(' OR ', $article_parts) . ')';
	$combined = '(' . $name_filter . ' OR ' . $article_filter . ')';
	return epc_demand_fetch_stock_lines_query($db, $combined, $fetch_limit);
}

function epc_demand_fetch_top_stock_lines(PDO $db, int $fetch_limit = 80): array
{
	return epc_demand_fetch_stock_lines_query($db, null, $fetch_limit);
}

function epc_demand_merge_stock_rows(array $primary, array $secondary): array
{
	$seen = array();
	$out = array();
	foreach (array_merge($primary, $secondary) as $row) {
		$key = mb_strtoupper(trim((string)$row['brand']), 'UTF-8') . '|' . docpart_normalize_article_for_price($row['article'] ?? '');
		if ($key === '|' || isset($seen[$key])) {
			continue;
		}
		$seen[$key] = true;
		$out[] = $row;
	}
	return $out;
}

function epc_demand_pin_showcase_part(array $rows, string $brand, string $article_norm): array
{
	$brand_upper = mb_strtoupper(trim($brand), 'UTF-8');
	$pinned = null;
	$rest = array();
	foreach ($rows as $row) {
		$row_brand = mb_strtoupper(trim((string)$row['brand']), 'UTF-8');
		$row_norm = isset($row['article_norm']) ? (string)$row['article_norm'] : docpart_normalize_article_for_price($row['article']);
		if ($pinned === null && $row_brand === $brand_upper && $row_norm === $article_norm) {
			$pinned = $row;
			continue;
		}
		$rest[] = $row;
	}
	if ($pinned === null) {
		return $rows;
	}
	return array_merge(array($pinned), $rest);
}

/**
 * @param array<int, array<string, mixed>> $rows
 * @return array<int, array<string, mixed>>
 */
function epc_demand_pinned_toyota_demo_row(PDO $db): array
{
	$lines = epc_demand_anchor_stock_from_db($db, 'TOYOTA', '1310154101');
	$qty = 0;
	$name = 'Piston 13101-54101 (OE planning example)';
	$price = '';
	$warehouse = '';
	if (!empty($lines)) {
		foreach ($lines as $line) {
			$qty = max($qty, (float)($line['qty'] ?? 0));
			if ($name === '' && !empty($line['name'])) {
				$name = (string)$line['name'];
			}
			if ($price === '' && !empty($line['price'])) {
				$price = $line['price'];
			}
			if ($warehouse === '' && !empty($line['warehouse'])) {
				$warehouse = (string)$line['warehouse'];
			}
		}
	}
	return array(
		'brand' => 'TOYOTA',
		'article' => '1310154101',
		'article_show' => '1310154101',
		'name' => $name,
		'qty' => $qty,
		'price' => $price,
		'warehouse' => $warehouse,
		'article_norm' => '1310154101',
	);
}

function epc_demand_ensure_toyota_demo_in_showcase(PDO $db, array $parts, int $limit): array
{
	$has = false;
	foreach ($parts as $part) {
		if (mb_strtoupper(trim((string)$part['brand']), 'UTF-8') === 'TOYOTA'
			&& docpart_normalize_article_for_price($part['article'] ?? '') === '1310154101') {
			$has = true;
			break;
		}
	}
	if ($has) {
		return $parts;
	}
	array_unshift($parts, epc_demand_pinned_toyota_demo_row($db));
	return array_slice($parts, 0, $limit);
}

function epc_demand_pick_diverse_showcase(array $rows, int $limit = 10): array
{
	$rows = epc_demand_pin_showcase_part($rows, 'TOYOTA', '1310154101');
	$picked = array();
	$seen_brands = array();
	foreach ($rows as $row) {
		$brand_key = mb_strtoupper(trim((string)$row['brand']), 'UTF-8');
		if ($brand_key === '' || isset($seen_brands[$brand_key])) {
			continue;
		}
		$seen_brands[$brand_key] = true;
		$picked[] = $row;
		if (count($picked) >= $limit) {
			break;
		}
	}
	if (count($picked) < $limit) {
		foreach ($rows as $row) {
			$norm = docpart_normalize_article_for_price($row['article'] ?? '');
			$dup = false;
			foreach ($picked as $existing) {
				$existing_norm = docpart_normalize_article_for_price($existing['article'] ?? '');
				if ($existing_norm === $norm && mb_strtoupper($existing['brand'], 'UTF-8') === mb_strtoupper($row['brand'], 'UTF-8')) {
					$dup = true;
					break;
				}
			}
			if ($dup) {
				continue;
			}
			$picked[] = $row;
			if (count($picked) >= $limit) {
				break;
			}
		}
	}
	return $picked;
}

function epc_demand_count_stock_brands_for_article(PDO $db, string $article_norm): int
{
	if ($article_norm === '') {
		return 0;
	}
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	try {
		$stmt = $db->prepare(
			'SELECT COUNT(DISTINCT UPPER(TRIM(`manufacturer`))) FROM `shop_docpart_prices_data`
			 WHERE ' . $art_expr . ' = ? AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0'
		);
		$stmt->execute(array($article_norm));
		return (int)$stmt->fetchColumn();
	} catch (Exception $e) {
		return 0;
	}
}

function epc_demand_seed_showcase_catalog(PDO $db, array $parts): int
{
	$pools = epc_demand_showcase_country_pools();
	$seeded = 0;
	foreach ($parts as $index => $part) {
		$codes = $pools[$index % count($pools)];
		$seeded += epc_demand_seed_part_countries(
			$db,
			(string)$part['brand'],
			(string)$part['article'],
			$codes,
			'showcase_seed',
			'Engine showcase demand tags'
		);
	}
	return $seeded;
}

function epc_demand_build_showcase_summary(PDO $db, $DP_Config, array $part): array
{
	$brand = trim((string)($part['brand'] ?? ''));
	$article = trim((string)($part['article'] ?? ''));
	$article_norm = docpart_normalize_article_for_price($article);
	$anchor_lines = epc_demand_anchor_stock_from_db($db, $brand, $article_norm);
	$anchor_qty = (float)($part['qty'] ?? 0);
	foreach ($anchor_lines as $line) {
		$anchor_qty = max($anchor_qty, (float)($line['qty'] ?? 0));
	}
	$demand_countries = epc_demand_get_countries_for_part($db, $brand, $article_norm);
	return array(
		'brand' => $brand,
		'article' => $article,
		'article_norm' => $article_norm,
		'name' => (string)($part['name'] ?? ''),
		'qty' => $anchor_qty,
		'price' => $part['price'] ?? '',
		'warehouse' => (string)($part['warehouse'] ?? ''),
		'demand_countries' => $demand_countries,
		'demand_codes' => array_map(function ($c) {
			return $c['code'];
		}, $demand_countries),
		'anchor_in_stock' => $anchor_qty > 0,
		'stock_brands_for_article' => epc_demand_count_stock_brands_for_article($db, $article_norm),
		'part_url' => epc_demand_chpu_part_url($DP_Config, $brand, $article),
	);
}

/**
 * @return array{status:bool, parts:array, seeded:int, source:string}
 */
function epc_demand_build_showcase(PDO $db, $DP_Config, int $limit = 10, bool $reseed = true): array
{
	epc_demand_ensure_schema($db);
	$fetch_limit = max(50, $limit * 8);
	$engine_rows = epc_demand_fetch_engine_stock_lines($db, $fetch_limit);
	$source = 'uae_engine_stock';
	$rows = $engine_rows;
	if (count($rows) < $limit) {
		$rows = epc_demand_merge_stock_rows($rows, epc_demand_fetch_top_stock_lines($db, $fetch_limit));
		$source = count($engine_rows) > 0 ? 'uae_engine_stock_plus_top' : 'uae_top_stock';
	}
	$parts = epc_demand_pick_diverse_showcase($rows, $limit);
	$parts = epc_demand_ensure_toyota_demo_in_showcase($db, $parts, $limit);
	if (empty($parts)) {
		$parts = array(
			array('brand' => 'TOYOTA', 'article' => '1310154101', 'article_show' => '1310154101', 'name' => 'Piston (demo)', 'qty' => 0, 'price' => '', 'warehouse' => '', 'article_norm' => '1310154101'),
		);
	}
	$seeded = 0;
	if ($reseed) {
		$seeded = epc_demand_seed_showcase_catalog($db, $parts);
	}
	$summaries = array();
	foreach ($parts as $part) {
		$summaries[] = epc_demand_build_showcase_summary($db, $DP_Config, $part);
	}
	return array(
		'status' => true,
		'parts' => $summaries,
		'seeded' => $seeded,
		'source' => $source,
		'limit' => $limit,
		'generated_at' => time(),
	);
}

function epc_demand_get_countries_for_part(PDO $db, string $manufacturer, string $article_norm): array
{
	$registry = epc_demand_country_registry();
	$manufacturer = trim($manufacturer);
	if ($article_norm === '' || $manufacturer === '') {
		return array();
	}
	try {
		$stmt = $db->prepare(
			'SELECT `country_code` FROM `epc_article_demand`
			 WHERE `article_norm` = ? AND UPPER(`manufacturer`) = UPPER(?)
			 ORDER BY `country_code`'
		);
		$stmt->execute(array($article_norm, $manufacturer));
		$rows = $stmt->fetchAll(PDO::FETCH_COLUMN);
	} catch (Exception $e) {
		$rows = array();
	}
	$out = array();
	foreach ($rows as $code) {
		$norm = epc_demand_normalize_country_code((string)$code);
		if ($norm === '' || !isset($registry[$norm])) {
			continue;
		}
		$out[] = $registry[$norm];
	}
	return $out;
}

function epc_demand_site_base($DP_Config): string
{
	$base = '';
	if (is_object($DP_Config) && !empty($DP_Config->domain_path)) {
		$base = rtrim((string)$DP_Config->domain_path, '/');
	}
	if ($base === '' && !empty($_SERVER['HTTP_HOST'])) {
		$scheme = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'http';
		$base = $scheme . '://' . $_SERVER['HTTP_HOST'];
	}
	return $base;
}

function epc_demand_http_json(string $url, int $timeout = 25): ?array
{
	$ctx = stream_context_create(array(
		'http' => array(
			'timeout' => $timeout,
			'ignore_errors' => true,
			'header' => "Accept: application/json\r\n",
		),
		'ssl' => array(
			'verify_peer' => true,
			'verify_peer_name' => true,
		),
	));
	$raw = @file_get_contents($url, false, $ctx);
	if ($raw === false || $raw === '') {
		return null;
	}
	$data = json_decode($raw, true);
	return is_array($data) ? $data : null;
}

function epc_demand_fetch_cross_payload($DP_Config, string $brand, string $article_norm): ?array
{
	$base = epc_demand_site_base($DP_Config);
	if ($base === '' || $article_norm === '') {
		return null;
	}
	$url = $base . '/content/shop/docpart/ajax_epc_cross_search.php?'
		. http_build_query(array('article' => $article_norm, 'brand' => $brand));
	return epc_demand_http_json($url, 90);
}

function epc_demand_stock_key(string $brand, string $article_norm): string
{
	return mb_strtoupper(trim($brand) . '|' . docpart_normalize_article_for_price($article_norm), 'UTF-8');
}

function epc_demand_index_stock(array $stock_rows): array
{
	$index = array();
	foreach ($stock_rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$brand = isset($row['brand']) ? trim((string)$row['brand']) : '';
		$norm = isset($row['article_norm']) ? (string)$row['article_norm'] : '';
		if ($norm === '' && isset($row['article'])) {
			$norm = docpart_normalize_article_for_price($row['article']);
		}
		$key = epc_demand_stock_key($brand, $norm);
		if ($key === '|') {
			continue;
		}
		$qty = isset($row['qty']) ? (float)$row['qty'] : 0;
		if (!isset($index[$key]) || $qty > (float)$index[$key]['qty']) {
			$index[$key] = $row;
		}
	}
	return $index;
}

function epc_demand_anchor_stock_from_db(PDO $db, string $brand, string $article_norm): array
{
	$lines = array();
	if ($article_norm === '') {
		return $lines;
	}
	require_once __DIR__ . '/epc_storefront_storage_flags.php';
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	try {
		$stmt = $db->prepare(
			'SELECT * FROM `shop_docpart_prices_data`
			 WHERE ' . $art_expr . ' = ?
			 AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
			 AND ' . epc_ssf_price_data_active_sql() . '
			 ORDER BY `manufacturer`, `price` ASC
			 LIMIT 40'
		);
		$stmt->execute(array($article_norm));
		$brand_upper = mb_strtoupper(trim($brand), 'UTF-8');
		while ($product = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$product_brand = isset($product['manufacturer']) ? trim((string)$product['manufacturer']) : '';
			if ($brand_upper !== '' && mb_strtoupper($product_brand, 'UTF-8') !== $brand_upper) {
				continue;
			}
			$lines[] = array(
				'brand' => $product_brand,
				'article' => !empty($product['article_show']) ? $product['article_show'] : (isset($product['article']) ? $product['article'] : ''),
				'article_norm' => docpart_normalize_article_for_price(isset($product['article']) ? $product['article'] : ''),
				'name' => isset($product['name']) ? (string)$product['name'] : '',
				'price' => isset($product['price']) ? $product['price'] : '',
				'currency' => isset($product['currency']) ? (string)$product['currency'] : '',
				'qty' => isset($product['exist']) ? (float)$product['exist'] : 0,
				'warehouse' => isset($product['storage']) ? (string)$product['storage'] : '',
				'price_id' => isset($product['price_id']) ? (int)$product['price_id'] : 0,
			);
		}
	} catch (Exception $e) {
	}
	return epc_ssf_filter_agent_stock_lines($db, $lines);
}

function epc_demand_chpu_part_url($DP_Config, string $brand, string $article): string
{
	$article_norm = docpart_normalize_article_for_price($article);
	$lang = '/en';
	if (is_object($DP_Config) && !empty($DP_Config->chpu_search_config) && is_array($DP_Config->chpu_search_config)) {
		// Default English storefront path used on production.
	}
	$parts_seg = 'parts';
	$slash = '---';
	if (is_object($DP_Config) && !empty($DP_Config->chpu_search_config['level_1']['url'])) {
		$parts_seg = (string)$DP_Config->chpu_search_config['level_1']['url'];
	}
	if (is_object($DP_Config) && !empty($DP_Config->chpu_search_config['slash_code'])) {
		$slash = (string)$DP_Config->chpu_search_config['slash_code'];
	}
	$brand_alias = str_replace('/', $slash, trim($brand));
	if ($brand_alias === '') {
		return $lang . '/' . $parts_seg . '/brands/' . rawurlencode($article_norm);
	}
	return $lang . '/' . $parts_seg . '/' . rawurlencode($brand_alias) . '/' . rawurlencode($article_norm);
}

function epc_demand_fitment_summary($DP_Config, string $brand, string $article): array
{
	$base = epc_demand_site_base($DP_Config);
	$summary = array(
		'part_name' => '',
		'product_group' => '',
		'vehicle_count' => 0,
		'vehicles_sample' => array(),
		'fitment_source' => '',
	);
	if ($base === '' || trim($brand) === '' || trim($article) === '') {
		return $summary;
	}
	$analogs = epc_demand_http_json(
		$base . '/api/umapi_proxy.php?' . http_build_query(array(
			'action' => 'analogs',
			'article' => $article,
			'brand' => $brand,
			'limit' => 30,
			'offset' => 0,
			'language' => 'en',
			'vehicle_type' => 'PC',
		)),
		20
	);
	$rows = array();
	if (is_array($analogs)) {
		$rows = isset($analogs['data']) && is_array($analogs['data']) ? $analogs['data'] : (is_array($analogs) ? $analogs : array());
	}
	$brand_cmp = mb_strtoupper(preg_replace('/[^A-Z0-9]/', '', $brand), 'UTF-8');
	$article_cmp = docpart_normalize_article_for_price($article);
	$target = null;
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$row_brand = isset($row['BRAND']) ? (string)$row['BRAND'] : (isset($row['SUP_BRAND']) ? (string)$row['SUP_BRAND'] : '');
		$row_article = isset($row['ARTICLE_NR']) ? (string)$row['ARTICLE_NR'] : (isset($row['ART_ARTICLE_NR']) ? (string)$row['ART_ARTICLE_NR'] : '');
		$b = mb_strtoupper(preg_replace('/[^A-Z0-9]/', '', $row_brand), 'UTF-8');
		$a = docpart_normalize_article_for_price($row_article);
		if ($b === $brand_cmp && $a === $article_cmp) {
			$target = $row;
			break;
		}
	}
	if ($target === null) {
		foreach ($rows as $row) {
			if (!is_array($row)) {
				continue;
			}
			$row_brand = isset($row['BRAND']) ? (string)$row['BRAND'] : (isset($row['SUP_BRAND']) ? (string)$row['SUP_BRAND'] : '');
			$b = mb_strtoupper(preg_replace('/[^A-Z0-9]/', '', $row_brand), 'UTF-8');
			if ($b === $brand_cmp) {
				$target = $row;
				break;
			}
		}
	}
	if ($target === null && !empty($rows[0]) && is_array($rows[0])) {
		$target = $rows[0];
	}
	if ($target === null || empty($target['ART_ID'])) {
		return $summary;
	}
	$art_id = (int)$target['ART_ID'];
	$article_detail = epc_demand_http_json(
		$base . '/api/umapi_proxy.php?' . http_build_query(array('action' => 'article', 'id' => $art_id, 'language' => 'en')),
		15
	);
	if (is_array($article_detail)) {
		$summary['part_name'] = trim((string)(
			$article_detail['COMPLETE_DES'] ?? $article_detail['DES'] ?? $article_detail['ART_PRODUCT_NAME'] ?? ''
		));
		$summary['product_group'] = trim((string)(
			$article_detail['PT_DES'] ?? $article_detail['PRODUCT_GROUP'] ?? ''
		));
	}
	$links = epc_demand_http_json(
		$base . '/api/umapi_proxy.php?' . http_build_query(array('action' => 'article_links', 'id' => $art_id, 'language' => 'en')),
		25
	);
	if (!is_array($links)) {
		return $summary;
	}
	$summary['fitment_source'] = 'umapi_article_links';
	$sections = array('PC', 'CV', 'Motorcycle');
	$all = array();
	foreach ($sections as $sec) {
		if (!empty($links[$sec]) && is_array($links[$sec])) {
			foreach ($links[$sec] as $row) {
				if (is_array($row)) {
					$all[] = $row;
				}
			}
		}
	}
	$summary['vehicle_count'] = count($all);
	$seen = array();
	foreach ($all as $row) {
		$make = trim((string)($row['MANUFACTURER'] ?? ''));
		$model = trim((string)($row['MODEL_SERIES'] ?? ''));
		$from = trim((string)($row['CI_FROM'] ?? ''));
		$to = trim((string)($row['CI_TO'] ?? ''));
		$years = ($from && $to) ? ($from . ' – ' . $to) : ($from ? ($from . ' – now') : $to);
		$engine = trim(implode(' / ', array_filter(array(
			$row['CAPACITY_TECH'] ?? $row['CAPACITY_LT'] ?? '',
			$row['FUEL_TYPE'] ?? '',
			$row['BODY_TYPE'] ?? $row['PLATFORM_TYPE'] ?? '',
		))));
		$mod = trim((string)($row['PASSENGER_CAR'] ?? $row['COMMERCIAL_VEHICLE'] ?? $row['MOTORBIKE'] ?? ''));
		$dedupe = mb_strtoupper($make . '|' . $model . '|' . $years . '|' . $mod, 'UTF-8');
		if (isset($seen[$dedupe])) {
			continue;
		}
		$seen[$dedupe] = true;
		$summary['vehicles_sample'][] = array(
			'make' => $make,
			'model' => $model,
			'modification' => $mod,
			'years' => $years,
			'engine' => $engine,
		);
		if (count($summary['vehicles_sample']) >= 8) {
			break;
		}
	}
	return $summary;
}

function epc_demand_resolve_umapi_art_id($DP_Config, string $brand, string $article): int
{
	$base = epc_demand_site_base($DP_Config);
	if ($base === '' || trim($brand) === '' || trim($article) === '') {
		return 0;
	}
	$analogs = epc_demand_http_json(
		$base . '/api/umapi_proxy.php?' . http_build_query(array(
			'action' => 'analogs',
			'article' => $article,
			'brand' => $brand,
			'limit' => 30,
			'offset' => 0,
			'language' => 'en',
			'vehicle_type' => 'PC',
		)),
		20
	);
	$rows = array();
	if (is_array($analogs)) {
		$rows = isset($analogs['data']) && is_array($analogs['data']) ? $analogs['data'] : (is_array($analogs) ? $analogs : array());
	}
	$brand_cmp = mb_strtoupper(preg_replace('/[^A-Z0-9]/', '', $brand), 'UTF-8');
	$article_cmp = docpart_normalize_article_for_price($article);
	$target = null;
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$row_brand = isset($row['BRAND']) ? (string)$row['BRAND'] : (isset($row['SUP_BRAND']) ? (string)$row['SUP_BRAND'] : '');
		$row_article = isset($row['ARTICLE_NR']) ? (string)$row['ARTICLE_NR'] : (isset($row['ART_ARTICLE_NR']) ? (string)$row['ART_ARTICLE_NR'] : '');
		$b = mb_strtoupper(preg_replace('/[^A-Z0-9]/', '', $row_brand), 'UTF-8');
		$a = docpart_normalize_article_for_price($row_article);
		if ($b === $brand_cmp && $a === $article_cmp) {
			$target = $row;
			break;
		}
	}
	if ($target === null) {
		foreach ($rows as $row) {
			if (!is_array($row)) {
				continue;
			}
			$row_brand = isset($row['BRAND']) ? (string)$row['BRAND'] : (isset($row['SUP_BRAND']) ? (string)$row['SUP_BRAND'] : '');
			$b = mb_strtoupper(preg_replace('/[^A-Z0-9]/', '', $row_brand), 'UTF-8');
			if ($b === $brand_cmp) {
				$target = $row;
				break;
			}
		}
	}
	if ($target === null && !empty($rows[0]) && is_array($rows[0])) {
		$target = $rows[0];
	}
	return ($target !== null && !empty($target['ART_ID'])) ? (int)$target['ART_ID'] : 0;
}

function epc_demand_vehicle_row_key(array $row): string
{
	$id = (int)($row['PC_ID'] ?? ($row['CV_ID'] ?? ($row['MTB_ID'] ?? 0)));
	if ($id > 0) {
		return 'id:' . $id;
	}
	$make = mb_strtoupper(trim((string)($row['MANUFACTURER'] ?? '')), 'UTF-8');
	$model = mb_strtoupper(trim((string)($row['MODEL_SERIES'] ?? '')), 'UTF-8');
	$mod = mb_strtoupper(trim((string)($row['PASSENGER_CAR'] ?? $row['COMMERCIAL_VEHICLE'] ?? $row['MOTORBIKE'] ?? '')), 'UTF-8');
	$from = trim((string)($row['CI_FROM'] ?? ''));
	$to = trim((string)($row['CI_TO'] ?? ''));
	return 'txt:' . $make . '|' . $model . '|' . $mod . '|' . $from . '|' . $to;
}

function epc_demand_vehicle_type_from_row(array $row): string
{
	if (!empty($row['PC_ID'])) {
		return 'PC';
	}
	if (!empty($row['CV_ID'])) {
		return 'CV';
	}
	if (!empty($row['MTB_ID'])) {
		return 'MTB';
	}
	return 'PC';
}

/**
 * Flat vehicle rows from UMAPI article_links payload.
 *
 * @return array<int, array<string, mixed>>
 */
function epc_demand_flatten_article_links(?array $links): array
{
	if (!is_array($links)) {
		return array();
	}
	$all = array();
	foreach (array('PC', 'CV', 'Motorcycle') as $sec) {
		if (!empty($links[$sec]) && is_array($links[$sec])) {
			foreach ($links[$sec] as $row) {
				if (is_array($row)) {
					$all[] = $row;
				}
			}
		}
	}
	return $all;
}

function epc_demand_fetch_article_links_by_id($DP_Config, int $art_id): array
{
	if ($art_id <= 0) {
		return array();
	}
	$base = epc_demand_site_base($DP_Config);
	if ($base === '') {
		return array();
	}
	$links = epc_demand_http_json(
		$base . '/api/umapi_proxy.php?' . http_build_query(array('action' => 'article_links', 'id' => $art_id, 'language' => 'en')),
		25
	);
	return epc_demand_flatten_article_links(is_array($links) ? $links : null);
}

/**
 * Flat vehicle rows from UMAPI article_links for one brand+article.
 *
 * @return array<int, array<string, mixed>>
 */
function epc_demand_vehicle_links_for_part($DP_Config, string $brand, string $article): array
{
	$art_id = epc_demand_resolve_umapi_art_id($DP_Config, $brand, $article);
	return epc_demand_fetch_article_links_by_id($DP_Config, $art_id);
}

function epc_demand_infer_product_group(string $umapi_group, string $part_name): string
{
	$group = trim($umapi_group);
	if ($group !== '') {
		return $group;
	}
	$hay = mb_strtolower($part_name, 'UTF-8');
	$rules = array(
		'Piston' => array('piston'),
		'Gasket' => array('gasket'),
		'Oil filter' => array('oil filter', 'ölfilter'),
		'Air filter' => array('air filter'),
		'Fuel filter' => array('fuel filter', 'diesel filter'),
		'Filter' => array('filter'),
		'Brake' => array('brake pad', 'brake disc', 'brake shoe', 'brake rotor'),
		'Clutch' => array('clutch'),
		'Bearing' => array('bearing'),
		'Belt' => array('timing belt', 'drive belt', 'v-belt', 'serpentine'),
		'Pump' => array('water pump', 'oil pump', 'fuel pump'),
		'Spark / glow plug' => array('spark plug', 'glow plug'),
		'Turbocharger' => array('turbo'),
		'Radiator' => array('radiator'),
		'Engine' => array('engine', 'cylinder', 'crank', 'camshaft', 'valve', 'liner'),
		'Sensor' => array('sensor'),
		'Mount' => array('mount', 'bushing'),
	);
	foreach ($rules as $label => $needles) {
		foreach ($needles as $needle) {
			if ($needle !== '' && strpos($hay, $needle) !== false) {
				return $label;
			}
		}
	}
	return $part_name !== '' ? 'Other parts' : 'Uncategorized';
}

/**
 * One part: UMAPI product group + fitment vehicles (single analogs lookup).
 *
 * @param array{brand:string, article:string, article_norm?:string, qty?:float, name?:string} $part
 * @return array{brand:string, article:string, article_norm:string, name:string, product_group:string, qty:float, vehicles:array<int, array>}
 */
function epc_demand_scan_country_part($DP_Config, array $part): array
{
	$brand = trim((string)($part['brand'] ?? ''));
	$article = trim((string)($part['article'] ?? ''));
	$article_norm = trim((string)($part['article_norm'] ?? docpart_normalize_article_for_price($article)));
	$qty = (float)($part['qty'] ?? 0);
	$part_name = trim((string)($part['name'] ?? ''));
	$product_group = '';
	$vehicles = array();

	$art_id = epc_demand_resolve_umapi_art_id($DP_Config, $brand, $article);
	if ($art_id > 0) {
		$base = epc_demand_site_base($DP_Config);
		if ($base !== '') {
			$detail = epc_demand_http_json(
				$base . '/api/umapi_proxy.php?' . http_build_query(array('action' => 'article', 'id' => $art_id, 'language' => 'en')),
				15
			);
			if (is_array($detail)) {
				$product_group = trim((string)($detail['PT_DES'] ?? $detail['PRODUCT_GROUP'] ?? ''));
				$catalog_name = trim((string)(
					$detail['COMPLETE_DES'] ?? $detail['DES'] ?? $detail['ART_PRODUCT_NAME'] ?? ''
				));
				if ($catalog_name !== '') {
					$part_name = $catalog_name;
				}
			}
		}
		$vehicles = epc_demand_fetch_article_links_by_id($DP_Config, $art_id);
	}

	$product_group = epc_demand_infer_product_group($product_group, $part_name);

	return array(
		'brand' => $brand,
		'article' => $article,
		'article_norm' => $article_norm,
		'name' => $part_name,
		'product_group' => $product_group,
		'qty' => $qty,
		'vehicles' => $vehicles,
	);
}

/**
 * @param array<int, array<string, mixed>> $part_lines
 * @return array<int, array{brand:string, parts_count:int, total_qty:float, product_groups:array<int, string>}>
 */
function epc_demand_job_build_brands_summary(array $part_lines): array
{
	$brands = array();
	foreach ($part_lines as $line) {
		$brand = trim((string)($line['brand'] ?? ''));
		if ($brand === '') {
			continue;
		}
		$key = mb_strtoupper($brand, 'UTF-8');
		if (!isset($brands[$key])) {
			$brands[$key] = array(
				'brand' => $brand,
				'parts_count' => 0,
				'total_qty' => 0.0,
				'product_groups' => array(),
			);
		}
		$brands[$key]['parts_count'] = (int)$brands[$key]['parts_count'] + 1;
		$brands[$key]['total_qty'] = (float)$brands[$key]['total_qty'] + (float)($line['qty'] ?? 0);
		$group_label = trim((string)($line['product_group'] ?? ''));
		if ($group_label !== '' && !in_array($group_label, $brands[$key]['product_groups'], true)) {
			$brands[$key]['product_groups'][] = $group_label;
		}
	}
	$list = array_values($brands);
	usort($list, function ($a, $b) {
		$ca = (int)($a['parts_count'] ?? 0);
		$cb = (int)($b['parts_count'] ?? 0);
		if ($ca !== $cb) {
			return $cb <=> $ca;
		}
		return strcmp((string)($a['brand'] ?? ''), (string)($b['brand'] ?? ''));
	});
	return $list;
}

/**
 * @param array<string, array<string, mixed>> $product_groups_map
 * @return array<int, array<string, mixed>>
 */
function epc_demand_job_products_list(array $product_groups_map): array
{
	$list = array();
	foreach ($product_groups_map as $group) {
		$brands_map = isset($group['brands']) && is_array($group['brands']) ? $group['brands'] : array();
		$brands_list = array_values($brands_map);
		usort($brands_list, function ($a, $b) {
			$qa = (float)($a['total_qty'] ?? 0);
			$qb = (float)($b['total_qty'] ?? 0);
			if ($qa !== $qb) {
				return $qb <=> $qa;
			}
			$ca = (int)($a['parts_count'] ?? 0);
			$cb = (int)($b['parts_count'] ?? 0);
			if ($ca !== $cb) {
				return $cb <=> $ca;
			}
			return strcmp((string)($a['brand'] ?? ''), (string)($b['brand'] ?? ''));
		});
		$parts = isset($group['parts']) && is_array($group['parts']) ? array_values($group['parts']) : array();
		$list[] = array(
			'label' => (string)($group['label'] ?? ''),
			'parts_count' => (int)($group['parts_count'] ?? 0),
			'total_qty' => (float)($group['total_qty'] ?? 0),
			'samples' => isset($group['samples']) && is_array($group['samples']) ? $group['samples'] : array(),
			'parts' => $parts,
			'brands' => $brands_list,
		);
	}
	usort($list, function ($a, $b) {
		$ca = (int)($a['parts_count'] ?? 0);
		$cb = (int)($b['parts_count'] ?? 0);
		if ($ca !== $cb) {
			return $cb <=> $ca;
		}
		return strcmp((string)($a['label'] ?? ''), (string)($b['label'] ?? ''));
	});
	return $list;
}

/**
 * @param array<string, mixed> $job
 * @param array<int, array<string, mixed>> $vehicles_list
 * @return array<string, mixed>
 */
function epc_demand_job_build_summary(array $job, array $vehicles_list): array
{
	$registry = epc_demand_country_registry();
	$code = strtoupper(trim((string)($job['country_code'] ?? '')));
	$part_lines = isset($job['part_lines']) && is_array($job['part_lines']) ? $job['part_lines'] : array();
	$product_groups = isset($job['product_groups']) && is_array($job['product_groups']) ? $job['product_groups'] : array();
	$makes = array();
	foreach ($vehicles_list as $vehicle) {
		$make = trim((string)($vehicle['MANUFACTURER'] ?? ''));
		if ($make !== '') {
			$makes[$make] = true;
		}
	}
	$total_qty = 0.0;
	foreach ($part_lines as $line) {
		$total_qty += (float)($line['qty'] ?? 0);
	}
	$brands_summary = epc_demand_job_build_brands_summary($part_lines);

	return array(
		'country_code' => $code,
		'country_name' => isset($registry[$code]['name']) ? (string)$registry[$code]['name'] : $code,
		'parts_count' => count($part_lines),
		'vehicles_count' => count($vehicles_list),
		'product_groups_count' => count($product_groups),
		'makes_count' => count($makes),
		'brands_count' => count($brands_summary),
		'total_stock_qty' => $total_qty,
		'brands' => $brands_summary,
	);
}

/**
 * @param array<string, mixed> $job
 * @return array{job: array, vehicles: array<int, array>, part_lines: array<int, array>, products: array<int, array>, summary: array, done: bool}
 */
function epc_demand_vehicle_job_payload(array $job, array $vehicles_list): array
{
	return array(
		'job' => $job,
		'vehicles' => $vehicles_list,
		'part_lines' => isset($job['part_lines']) && is_array($job['part_lines']) ? array_values($job['part_lines']) : array(),
		'products' => epc_demand_job_products_list(isset($job['product_groups']) && is_array($job['product_groups']) ? $job['product_groups'] : array()),
		'summary' => epc_demand_job_build_summary($job, $vehicles_list),
		'done' => !empty($job['done']),
	);
}

/**
 * Parts with demand tag for country that exist in UAE price list (qty &gt; 0).
 *
 * @return array<int, array{brand:string, article:string, article_norm:string, qty:float, name:string}>
 */
function epc_demand_country_price_list_parts(PDO $db, $DP_Config, string $country_code, int $limit = 40, bool $require_stock = true): array
{
	$view = epc_demand_build_country_view($db, $DP_Config, $country_code, max(1, min($limit, 80)));
	if (empty($view['status'])) {
		return array();
	}
	$parts = array();
	foreach ($view['parts'] as $row) {
		$qty = (float)($row['qty'] ?? 0);
		if ($require_stock && $qty <= 0) {
			continue;
		}
		$parts[] = array(
			'brand' => (string)($row['brand'] ?? ''),
			'article' => (string)($row['article'] ?? ''),
			'article_norm' => (string)($row['article_norm'] ?? ''),
			'qty' => $qty,
			'name' => (string)($row['name'] ?? ''),
		);
	}
	return $parts;
}

function epc_demand_vehicle_job_dir(): string
{
	$dir = rtrim(sys_get_temp_dir(), DIRECTORY_SEPARATOR) . DIRECTORY_SEPARATOR . 'epc_demand_vehicle_jobs';
	if (!is_dir($dir)) {
		@mkdir($dir, 0700, true);
	}
	return $dir;
}

function epc_demand_vehicle_job_path(string $job_id): string
{
	$safe = preg_replace('/[^a-zA-Z0-9_\-]/', '', $job_id);
	if ($safe === '' || strlen($safe) > 64) {
		return '';
	}
	return epc_demand_vehicle_job_dir() . DIRECTORY_SEPARATOR . $safe . '.json';
}

/**
 * @return array<string, mixed>|null
 */
function epc_demand_vehicle_job_read(string $job_id): ?array
{
	$path = epc_demand_vehicle_job_path($job_id);
	if ($path === '' || !is_file($path)) {
		return null;
	}
	$raw = @file_get_contents($path);
	if ($raw === false || $raw === '') {
		return null;
	}
	$data = json_decode($raw, true);
	return is_array($data) ? $data : null;
}

function epc_demand_vehicle_job_write(string $job_id, array $data): bool
{
	$path = epc_demand_vehicle_job_path($job_id);
	if ($path === '') {
		return false;
	}
	$json = json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	if ($json === false) {
		return false;
	}
	return @file_put_contents($path, $json, LOCK_EX) !== false;
}

/**
 * @param array<string, mixed> $job
 * @return array{job: array, vehicles: array<int, array>, part_lines: array<int, array>, products: array<int, array>, summary: array, done: bool}
 */
function epc_demand_vehicle_job_step(array $job, $DP_Config, int $batch_size = 2): array
{
	$parts = isset($job['parts']) && is_array($job['parts']) ? $job['parts'] : array();
	$cursor = isset($job['cursor']) ? (int)$job['cursor'] : 0;
	$total = count($parts);
	$vehicles_map = isset($job['vehicles']) && is_array($job['vehicles']) ? $job['vehicles'] : array();
	$part_lines = isset($job['part_lines']) && is_array($job['part_lines']) ? $job['part_lines'] : array();
	$product_groups = isset($job['product_groups']) && is_array($job['product_groups']) ? $job['product_groups'] : array();
	$batch_size = max(1, min($batch_size, 5));
	$end = min($cursor + $batch_size, $total);
	$current_label = '';

	for ($i = $cursor; $i < $end; $i++) {
		$scan = epc_demand_scan_country_part($DP_Config, $parts[$i]);
		$brand = (string)$scan['brand'];
		$article = (string)$scan['article'];
		$current_label = trim($brand . ' ' . $article);

		$part_lines[] = array(
			'brand' => $brand,
			'article' => $article,
			'article_norm' => (string)$scan['article_norm'],
			'name' => (string)$scan['name'],
			'product_group' => (string)$scan['product_group'],
			'qty' => (float)$scan['qty'],
		);

		$group_key = mb_strtoupper((string)$scan['product_group'], 'UTF-8');
		if ($group_key === '') {
			$group_key = 'OTHER';
		}
		if (!isset($product_groups[$group_key])) {
			$product_groups[$group_key] = array(
				'label' => (string)$scan['product_group'],
				'parts_count' => 0,
				'total_qty' => 0.0,
				'samples' => array(),
				'parts' => array(),
				'brands' => array(),
			);
		}
		$part_row = array(
			'brand' => $brand,
			'article' => $article,
			'article_norm' => (string)$scan['article_norm'],
			'name' => (string)$scan['name'],
			'qty' => (float)$scan['qty'],
		);
		$product_groups[$group_key]['parts'][] = $part_row;
		$product_groups[$group_key]['parts_count'] = (int)$product_groups[$group_key]['parts_count'] + 1;
		$product_groups[$group_key]['total_qty'] = (float)$product_groups[$group_key]['total_qty'] + (float)$scan['qty'];
		if (count($product_groups[$group_key]['samples']) < 5) {
			$product_groups[$group_key]['samples'][] = array(
				'brand' => $brand,
				'article' => $article,
				'name' => (string)$scan['name'],
			);
		}
		$brand_key = mb_strtoupper($brand, 'UTF-8');
		if (!isset($product_groups[$group_key]['brands'][$brand_key])) {
			$product_groups[$group_key]['brands'][$brand_key] = array(
				'brand' => $brand,
				'parts_count' => 0,
				'total_qty' => 0.0,
			);
		}
		$product_groups[$group_key]['brands'][$brand_key]['parts_count'] = (int)$product_groups[$group_key]['brands'][$brand_key]['parts_count'] + 1;
		$product_groups[$group_key]['brands'][$brand_key]['total_qty'] = (float)$product_groups[$group_key]['brands'][$brand_key]['total_qty'] + (float)$scan['qty'];

		foreach ($scan['vehicles'] as $row) {
			$key = epc_demand_vehicle_row_key($row);
			if (!isset($vehicles_map[$key])) {
				$row['_vehicle_type'] = epc_demand_vehicle_type_from_row($row);
				$row['_parts_count'] = 1;
				$vehicles_map[$key] = $row;
			} else {
				$vehicles_map[$key]['_parts_count'] = (int)($vehicles_map[$key]['_parts_count'] ?? 1) + 1;
			}
		}
	}

	$job['cursor'] = $end;
	$job['vehicles'] = $vehicles_map;
	$job['part_lines'] = $part_lines;
	$job['product_groups'] = $product_groups;
	$job['current_part'] = $current_label;
	$job['done'] = ($end >= $total);
	$job['updated_at'] = time();

	$list = array_values($vehicles_map);
	usort($list, function ($a, $b) {
		$ma = mb_strtoupper((string)($a['MANUFACTURER'] ?? ''), 'UTF-8');
		$mb = mb_strtoupper((string)($b['MANUFACTURER'] ?? ''), 'UTF-8');
		if ($ma !== $mb) {
			return strcmp($ma, $mb);
		}
		$mo = strcmp((string)($a['MODEL_SERIES'] ?? ''), (string)($b['MODEL_SERIES'] ?? ''));
		if ($mo !== 0) {
			return $mo;
		}
		return strcmp((string)($a['PASSENGER_CAR'] ?? $a['COMMERCIAL_VEHICLE'] ?? $a['MOTORBIKE'] ?? ''), (string)($b['PASSENGER_CAR'] ?? $b['COMMERCIAL_VEHICLE'] ?? $b['MOTORBIKE'] ?? ''));
	});

	return epc_demand_vehicle_job_payload($job, $list);
}

function epc_demand_country_overview(PDO $db): array
{
	epc_demand_ensure_schema($db);
	$registry = epc_demand_country_registry();
	$counts = array();
	try {
		$stmt = $db->query(
			'SELECT `country_code`, COUNT(DISTINCT CONCAT(UPPER(`manufacturer`), "|", `article_norm`)) AS `parts`
			 FROM `epc_article_demand` GROUP BY `country_code`'
		);
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$code = epc_demand_normalize_country_code((string)($row['country_code'] ?? ''));
			if ($code !== '') {
				$counts[$code] = (int)($row['parts'] ?? 0) + (isset($counts[$code]) ? $counts[$code] : 0);
			}
		}
	} catch (Exception $e) {
	}
	$out = array();
	foreach ($registry as $code => $meta) {
		if (epc_demand_is_stock_pool_country_code($code)) {
			continue;
		}
		$out[] = array(
			'code' => $code,
			'name' => $meta['name'],
			'parts_count' => isset($counts[$code]) ? $counts[$code] : 0,
		);
	}
	return $out;
}

function epc_demand_get_demand_codes_for_part(PDO $db, string $manufacturer, string $article_norm): array
{
	$manufacturer = trim($manufacturer);
	if ($article_norm === '' || $manufacturer === '') {
		return array();
	}
	try {
		$stmt = $db->prepare(
			'SELECT `country_code` FROM `epc_article_demand`
			 WHERE `article_norm` = ? AND UPPER(`manufacturer`) = UPPER(?)
			 ORDER BY `country_code`'
		);
		$stmt->execute(array($article_norm, $manufacturer));
		$rows = $stmt->fetchAll(PDO::FETCH_COLUMN);
	} catch (Exception $e) {
		return array();
	}
	$codes = array();
	foreach ($rows as $code) {
		$norm = epc_demand_normalize_country_code((string)$code);
		if ($norm !== '') {
			$codes[] = $norm;
		}
	}
	return array_values(array_unique($codes));
}

/**
 * Demand tags for same article number across all brands + overlap stats.
 */
function epc_demand_get_demand_statistics(PDO $db, string $manufacturer, string $article_norm): array
{
	$registry = epc_demand_country_registry();
	$manufacturer = trim($manufacturer);
	$article_norm = docpart_normalize_article_for_price($article_norm);
	$this_brand_codes = epc_demand_get_demand_codes_for_part($db, $manufacturer, $article_norm);

	$by_brand = array();
	$country_brand_counts = array();
	try {
		$stmt = $db->prepare(
			'SELECT `manufacturer`, `country_code` FROM `epc_article_demand` WHERE `article_norm` = ? ORDER BY `manufacturer`, `country_code`'
		);
		$stmt->execute(array($article_norm));
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$b = trim((string)($row['manufacturer'] ?? ''));
			$c = epc_demand_normalize_country_code((string)($row['country_code'] ?? ''));
			if ($b === '' || $c === '' || !isset($registry[$c])) {
				continue;
			}
			if (!isset($by_brand[$b])) {
				$by_brand[$b] = array();
			}
			$by_brand[$b][$c] = true;
		}
	} catch (Exception $e) {
	}

	$brand_rows = array();
	foreach ($by_brand as $b => $codes_map) {
		$codes = array_keys($codes_map);
		sort($codes);
		foreach ($codes as $c) {
			$country_brand_counts[$c] = ($country_brand_counts[$c] ?? 0) + 1;
		}
		$brand_rows[] = array(
			'brand' => $b,
			'country_codes' => $codes,
			'countries' => array_map(function ($code) use ($registry) {
				return $registry[$code];
			}, $codes),
			'is_anchor' => (mb_strtoupper($b, 'UTF-8') === mb_strtoupper($manufacturer, 'UTF-8')),
		);
	}
	usort($brand_rows, function ($a, $b) {
		if (!empty($a['is_anchor']) !== !empty($b['is_anchor'])) {
			return !empty($a['is_anchor']) ? -1 : 1;
		}
		return strcmp($a['brand'], $b['brand']);
	});

	$shared_countries = array();
	foreach ($country_brand_counts as $code => $brand_count) {
		if ($brand_count < 1) {
			continue;
		}
		$shared_countries[] = array(
			'code' => $code,
			'name' => $registry[$code]['name'],
			'brands_with_demand' => $brand_count,
			'in_this_brand' => in_array($code, $this_brand_codes, true),
		);
	}
	usort($shared_countries, function ($a, $b) {
		return ($b['brands_with_demand'] <=> $a['brands_with_demand']);
	});

	return array(
		'this_brand' => $manufacturer,
		'article_norm' => $article_norm,
		'this_brand_country_codes' => $this_brand_codes,
		'this_brand_countries' => epc_demand_get_countries_for_part($db, $manufacturer, $article_norm),
		'brands_with_same_article' => $brand_rows,
		'shared_country_stats' => $shared_countries,
		'total_demand_brands' => count($brand_rows),
	);
}

function epc_demand_extract_cross_gaps(?array $cross, array $stock_index, string $anchor_key, $DP_Config, int $max = 25): array
{
	$gaps = array();
	if (!is_array($cross) || empty($cross['references']) || !is_array($cross['references'])) {
		return $gaps;
	}
	foreach ($cross['references'] as $ref) {
		if (!is_array($ref)) {
			continue;
		}
		$ref_brand = isset($ref['brand']) ? trim((string)$ref['brand']) : (isset($ref['manufacturer']) ? trim((string)$ref['manufacturer']) : '');
		$ref_norm = isset($ref['article_norm']) ? (string)$ref['article_norm'] : '';
		if ($ref_norm === '' && isset($ref['article'])) {
			$ref_norm = docpart_normalize_article_for_price($ref['article']);
		}
		if ($ref_brand === '' || $ref_norm === '') {
			continue;
		}
		$key = epc_demand_stock_key($ref_brand, $ref_norm);
		if ($key === $anchor_key || isset($stock_index[$key])) {
			continue;
		}
		$gaps[] = array(
			'brand' => $ref_brand,
			'article' => isset($ref['article']) ? (string)$ref['article'] : $ref_norm,
			'article_norm' => $ref_norm,
			'name' => isset($ref['name']) ? (string)$ref['name'] : '',
			'url' => epc_demand_chpu_part_url($DP_Config, $ref_brand, isset($ref['article']) ? (string)$ref['article'] : $ref_norm),
		);
		if (count($gaps) >= $max) {
			break;
		}
	}
	return $gaps;
}

function epc_demand_supply_status(bool $anchor_in_stock, int $in_stock_cross_count, int $gap_count): string
{
	if ($anchor_in_stock) {
		return 'oe_in_stock';
	}
	if ($in_stock_cross_count > 0) {
		return 'sell_crosses';
	}
	if ($gap_count > 0) {
		return 'cross_gap';
	}
	return 'no_stock_signal';
}

function epc_demand_enrich_part_row(PDO $db, $DP_Config, string $brand, string $article, string $selected_country = ''): array
{
	$article_norm = docpart_normalize_article_for_price($article);
	$summary = epc_demand_build_showcase_summary($db, $DP_Config, array(
		'brand' => $brand,
		'article' => $article,
		'name' => '',
		'qty' => 0,
		'price' => '',
		'warehouse' => '',
	));
	$codes = $summary['demand_codes'] ?? array();
	$other = array();
	foreach ($codes as $code) {
		if ($selected_country !== '' && $code === $selected_country) {
			continue;
		}
		$other[] = $code;
	}
	$in_selected = ($selected_country === '') || in_array($selected_country, $codes, true);
	return array_merge($summary, array(
		'selected_country' => $selected_country,
		'in_selected_country' => $in_selected,
		'other_demand_codes' => $other,
		'supply_status' => !empty($summary['anchor_in_stock']) ? 'oe_in_stock' : 'needs_cross_or_gap',
	));
}

/**
 * @return array{status:bool, country:array, parts:array, total:int}
 */
function epc_demand_build_country_view(PDO $db, $DP_Config, string $country_code, int $limit = 50): array
{
	epc_demand_ensure_schema($db);
	$country_code = epc_demand_normalize_country_code($country_code);
	$registry = epc_demand_country_registry();
	if ($country_code === '' || !isset($registry[$country_code])) {
		return array('status' => false, 'message' => 'Unknown country code');
	}

	$rows = array();
	try {
		$stmt = $db->prepare(
			'SELECT `manufacturer`, `article_norm`, MIN(`notes`) AS `notes`
			 FROM `epc_article_demand` WHERE `country_code` = ?
			 GROUP BY `manufacturer`, `article_norm`
			 ORDER BY `manufacturer`, `article_norm`
			 LIMIT ' . (int)max(1, min($limit, 100))
		);
		$stmt->execute(array($country_code));
		$rows = $stmt->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		return array('status' => false, 'message' => 'Query failed');
	}

	$parts = array();
	foreach ($rows as $row) {
		$brand = trim((string)($row['manufacturer'] ?? ''));
		$article_norm = trim((string)($row['article_norm'] ?? ''));
		if ($brand === '' || $article_norm === '') {
			continue;
		}
		$lines = epc_demand_anchor_stock_from_db($db, $brand, $article_norm);
		$article_show = $article_norm;
		$name = '';
		$qty = 0;
		$price = '';
		$warehouse = '';
		if (!empty($lines)) {
			$best = $lines[0];
			foreach ($lines as $line) {
				if ((float)$line['qty'] > (float)$best['qty']) {
					$best = $line;
				}
			}
			$article_show = $best['article'] ?: $article_norm;
			$name = $best['name'];
			$qty = (float)$best['qty'];
			$price = $best['price'];
			$warehouse = $best['warehouse'];
		}
		$demand_codes = epc_demand_get_demand_codes_for_part($db, $brand, $article_norm);
		$other = array_values(array_filter($demand_codes, function ($c) use ($country_code) {
			return $c !== $country_code;
		}));
		$parts[] = array(
			'brand' => $brand,
			'article' => $article_show,
			'article_norm' => $article_norm,
			'name' => $name,
			'qty' => $qty,
			'anchor_in_stock' => $qty > 0,
			'price' => $price,
			'warehouse' => $warehouse,
			'demand_codes' => $demand_codes,
			'other_demand_codes' => $other,
			'selected_country' => $country_code,
			'in_selected_country' => true,
			'supply_status' => $qty > 0 ? 'oe_in_stock' : 'needs_cross_or_gap',
			'part_url' => epc_demand_chpu_part_url($DP_Config, $brand, $article_show),
			'shared_country_count' => count($demand_codes),
		);
	}

	return array(
		'status' => true,
		'country' => $registry[$country_code],
		'country_code' => $country_code,
		'parts' => $parts,
		'total' => count($parts),
		'generated_at' => time(),
	);
}

/**
 * @param array<string>|null $demand_override Country codes e.g. ['SDN','DZA','KEN']
 */
function epc_demand_build_card(PDO $db, $DP_Config, string $brand, string $article, ?array $demand_override = null, string $selected_country = ''): array
{
	epc_demand_ensure_schema($db);
	$article_norm = docpart_normalize_article_for_price($article);
	$brand = trim($brand);
	$registry = epc_demand_country_registry();

	$demand_countries = array();
	if (is_array($demand_override) && !empty($demand_override)) {
		foreach ($demand_override as $code) {
			$code = strtoupper(trim((string)$code));
			if ($code !== '' && isset($registry[$code])) {
				$demand_countries[] = $registry[$code];
			}
		}
	} else {
		$demand_countries = epc_demand_get_countries_for_part($db, $brand, $article_norm);
	}

	$cross = epc_demand_fetch_cross_payload($DP_Config, $brand, $article_norm);
	$stock_rows = (is_array($cross) && !empty($cross['stock']) && is_array($cross['stock'])) ? $cross['stock'] : array();
	$stock_index = epc_demand_index_stock($stock_rows);

	$anchor_key = epc_demand_stock_key($brand, $article_norm);
	$anchor_row = isset($stock_index[$anchor_key]) ? $stock_index[$anchor_key] : null;
	$anchor_lines = epc_demand_anchor_stock_from_db($db, $brand, $article_norm);
	if ($anchor_row === null && !empty($anchor_lines)) {
		$best = $anchor_lines[0];
		foreach ($anchor_lines as $line) {
			if ((float)$line['qty'] > (float)$best['qty']) {
				$best = $line;
			}
		}
		$anchor_row = $best;
	}
	$anchor_qty = $anchor_row ? (float)($anchor_row['qty'] ?? 0) : 0;
	foreach ($anchor_lines as $line) {
		$anchor_qty = max($anchor_qty, (float)($line['qty'] ?? 0));
	}

	$sellable_crosses = array();
	$in_stock_cross_count = 0;
	if (is_array($cross) && !empty($cross['references']) && is_array($cross['references'])) {
		foreach ($cross['references'] as $ref) {
			if (!is_array($ref)) {
				continue;
			}
			$ref_brand = isset($ref['brand']) ? trim((string)$ref['brand']) : (isset($ref['manufacturer']) ? trim((string)$ref['manufacturer']) : '');
			$ref_norm = isset($ref['article_norm']) ? (string)$ref['article_norm'] : '';
			if ($ref_norm === '' && isset($ref['article'])) {
				$ref_norm = docpart_normalize_article_for_price($ref['article']);
			}
			if ($ref_norm === '' || $ref_brand === '') {
				continue;
			}
			$key = epc_demand_stock_key($ref_brand, $ref_norm);
			if ($key === $anchor_key) {
				continue;
			}
			if (!isset($stock_index[$key])) {
				continue;
			}
			$in_stock_cross_count++;
			$row = $stock_index[$key];
			$sellable_crosses[] = array(
				'brand' => isset($row['brand']) ? $row['brand'] : $ref_brand,
				'article' => isset($row['article']) ? $row['article'] : (isset($ref['article']) ? $ref['article'] : $ref_norm),
				'article_norm' => $ref_norm,
				'name' => isset($row['name']) ? $row['name'] : (isset($ref['name']) ? $ref['name'] : ''),
				'qty' => (float)($row['qty'] ?? 0),
				'price' => $row['price'] ?? '',
				'currency' => isset($row['currency']) ? $row['currency'] : '',
				'warehouse' => isset($row['warehouse']) ? $row['warehouse'] : '',
				'url' => epc_demand_chpu_part_url($DP_Config, isset($row['brand']) ? $row['brand'] : $ref_brand, isset($row['article']) ? $row['article'] : $ref_norm),
				'is_oe' => false,
			);
		}
	}
	usort($sellable_crosses, function ($a, $b) {
		return ($b['qty'] <=> $a['qty']);
	});
	$sellable_crosses = array_slice($sellable_crosses, 0, 25);

	$cross_gaps = epc_demand_extract_cross_gaps($cross, $stock_index, $anchor_key, $DP_Config, 25);
	$gap_total = 0;
	if (is_array($cross) && !empty($cross['references']) && is_array($cross['references'])) {
		foreach ($cross['references'] as $ref) {
			if (!is_array($ref)) {
				continue;
			}
			$ref_brand = isset($ref['brand']) ? trim((string)$ref['brand']) : (isset($ref['manufacturer']) ? trim((string)$ref['manufacturer']) : '');
			$ref_norm = isset($ref['article_norm']) ? (string)$ref['article_norm'] : '';
			if ($ref_norm === '' && isset($ref['article'])) {
				$ref_norm = docpart_normalize_article_for_price($ref['article']);
			}
			if ($ref_brand === '' || $ref_norm === '') {
				continue;
			}
			$key = epc_demand_stock_key($ref_brand, $ref_norm);
			if ($key !== $anchor_key && !isset($stock_index[$key])) {
				$gap_total++;
			}
		}
	}

	$selected_country = epc_demand_normalize_country_code($selected_country);
	$demand_codes = array_map(function ($c) {
		return $c['code'];
	}, $demand_countries);
	$demand_statistics = epc_demand_get_demand_statistics($db, $brand, $article_norm);
	$supply_status = epc_demand_supply_status($anchor_qty > 0, $in_stock_cross_count, count($cross_gaps));

	$fitment = epc_demand_fitment_summary($DP_Config, $brand, $article);
	$fit_brand = $brand;
	$fit_article = $article;
	if (!$anchor_row && !empty($sellable_crosses)) {
		$fit_brand = $sellable_crosses[0]['brand'];
		$fit_article = $sellable_crosses[0]['article'];
	}
	if (empty($fitment['vehicles_sample'])) {
		$fitment = epc_demand_fitment_summary($DP_Config, $fit_brand, $fit_article);
	}

	$base = epc_demand_site_base($DP_Config);
	return array(
		'status' => true,
		'brand' => $brand,
		'article' => $article,
		'article_norm' => $article_norm,
		'demand_countries' => $demand_countries,
		'stock_region' => array(
			'code' => 'ARE',
			'name' => 'United Arab Emirates',
			'note' => 'Single UAE warehouse pool (R-UAE / S-UAE). Demand countries are planning tags only.',
		),
		'anchor' => array(
			'in_stock' => $anchor_qty > 0,
			'qty' => $anchor_qty,
			'price' => $anchor_row['price'] ?? null,
			'currency' => $anchor_row['currency'] ?? '',
			'name' => $anchor_row['name'] ?? '',
			'lines' => $anchor_lines,
		),
		'cross_summary' => array(
			'total_catalog' => is_array($cross) ? (int)($cross['total'] ?? $cross['total_catalog'] ?? 0) : 0,
			'references_loaded' => is_array($cross) ? (int)($cross['reference_count'] ?? $cross['references_loaded'] ?? 0) : 0,
			'stock_lines' => count($stock_rows),
			'in_stock_cross_count' => $in_stock_cross_count,
			'source' => is_array($cross) ? (string)($cross['source'] ?? '') : '',
			'cross_available' => is_array($cross) && !empty($cross['status']),
		),
		'sellable_crosses' => $sellable_crosses,
		'cross_gaps' => $cross_gaps,
		'cross_gaps_count' => $gap_total,
		'cross_gaps_shown' => count($cross_gaps),
		'supply_status' => $supply_status,
		'selected_country' => $selected_country,
		'in_selected_country_demand' => ($selected_country === '') || in_array($selected_country, $demand_codes, true),
		'demand_statistics' => $demand_statistics,
		'fitment' => $fitment,
		'part_url' => epc_demand_chpu_part_url($DP_Config, $brand, $article),
		'part_url_absolute' => $base . epc_demand_chpu_part_url($DP_Config, $brand, $article),
		'generated_at' => time(),
	);
}
