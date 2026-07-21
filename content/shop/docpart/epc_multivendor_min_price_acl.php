<?php
/**
 * Multivendor sales/purchase MINIMUM price visibility.
 *
 * Min-tier offers (marked epc_mv_min on shop_docpart_prices_data.storage) are
 * hidden from the general storefront. Allowed viewers:
 *   - CP / backend administrators (always)
 *   - selected customer groups
 *   - selected customer (user) IDs
 *
 * Max-tier and inventory prices stay public.
 */
defined('_ASTEXE_') or die('No access');

const EPC_MV_MIN_TIER = 'epc_mv_min';
const EPC_MV_MAX_TIER = 'epc_mv_max';

function epc_mv_min_price_acl_ensure(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	try {
		$db->exec(
			"CREATE TABLE IF NOT EXISTS `epc_mv_min_price_acl` (
				`id` INT UNSIGNED NOT NULL PRIMARY KEY DEFAULT 1,
				`restrict_min` TINYINT(1) NOT NULL DEFAULT 1,
				`group_ids_json` TEXT NULL,
				`user_ids_json` TEXT NULL,
				`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
				`updated_by` INT UNSIGNED NOT NULL DEFAULT 0
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
		);
		$cnt = (int) $db->query('SELECT COUNT(*) FROM `epc_mv_min_price_acl`')->fetchColumn();
		if ($cnt === 0) {
			$db->prepare(
				'INSERT INTO `epc_mv_min_price_acl`
				 (`id`,`restrict_min`,`group_ids_json`,`user_ids_json`,`updated_at`,`updated_by`)
				 VALUES (1,1,?,?,?,0)'
			)->execute(array('[]', '[]', time()));
		}
	} catch (Throwable $e) {
		// Table create may fail on read-only replicas; callers fall back to defaults.
	}
}

/**
 * @return array{restrict:bool,group_ids:list<int>,user_ids:list<int>}
 */
function epc_mv_min_price_acl_defaults(): array
{
	return array(
		'restrict' => true,
		'group_ids' => array(),
		'user_ids' => array(),
	);
}

/**
 * @return array{restrict:bool,group_ids:list<int>,user_ids:list<int>}
 */
function epc_mv_min_price_acl_get(PDO $db): array
{
	$out = epc_mv_min_price_acl_defaults();
	epc_mv_min_price_acl_ensure($db);
	try {
		$row = $db->query('SELECT `restrict_min`,`group_ids_json`,`user_ids_json` FROM `epc_mv_min_price_acl` WHERE `id` = 1 LIMIT 1')
			->fetch(PDO::FETCH_ASSOC);
		if (!is_array($row)) {
			return $out;
		}
		$out['restrict'] = !empty($row['restrict_min']);
		$groups = json_decode((string) ($row['group_ids_json'] ?? '[]'), true);
		$users = json_decode((string) ($row['user_ids_json'] ?? '[]'), true);
		$out['group_ids'] = array();
		if (is_array($groups)) {
			foreach ($groups as $g) {
				$g = (int) $g;
				if ($g > 0) {
					$out['group_ids'][] = $g;
				}
			}
		}
		$out['user_ids'] = array();
		if (is_array($users)) {
			foreach ($users as $u) {
				$u = (int) $u;
				if ($u > 0) {
					$out['user_ids'][] = $u;
				}
			}
		}
	} catch (Throwable $e) {
		return $out;
	}
	return $out;
}

/**
 * @param array{restrict?:bool,group_ids?:list<int|string>,user_ids?:list<int|string>} $acl
 */
function epc_mv_min_price_acl_save(PDO $db, array $acl, int $updatedBy = 0): bool
{
	epc_mv_min_price_acl_ensure($db);
	$restrict = !empty($acl['restrict']) ? 1 : 0;
	$groupIds = array();
	foreach ((array) ($acl['group_ids'] ?? array()) as $g) {
		$g = (int) $g;
		if ($g > 0) {
			$groupIds[$g] = $g;
		}
	}
	$userIds = array();
	foreach ((array) ($acl['user_ids'] ?? array()) as $u) {
		$u = (int) $u;
		if ($u > 0) {
			$userIds[$u] = $u;
		}
	}
	try {
		$db->prepare(
			'INSERT INTO `epc_mv_min_price_acl`
			 (`id`,`restrict_min`,`group_ids_json`,`user_ids_json`,`updated_at`,`updated_by`)
			 VALUES (1,?,?,?,?,?)
			 ON DUPLICATE KEY UPDATE
			 `restrict_min`=VALUES(`restrict_min`),
			 `group_ids_json`=VALUES(`group_ids_json`),
			 `user_ids_json`=VALUES(`user_ids_json`),
			 `updated_at`=VALUES(`updated_at`),
			 `updated_by`=VALUES(`updated_by`)'
		)->execute(array(
			$restrict,
			json_encode(array_values($groupIds), JSON_UNESCAPED_UNICODE),
			json_encode(array_values($userIds), JSON_UNESCAPED_UNICODE),
			time(),
			max(0, $updatedBy),
		));
		return true;
	} catch (Throwable $e) {
		return false;
	}
}

function epc_mv_min_price_is_admin_viewer(): bool
{
	if (!class_exists('DP_User')) {
		$userFile = (isset($_SERVER['DOCUMENT_ROOT']) ? $_SERVER['DOCUMENT_ROOT'] : '') . '/content/users/dp_user.php';
		if (is_file($userFile)) {
			require_once $userFile;
		}
	}
	if (!class_exists('DP_User')) {
		return false;
	}
	try {
		if (method_exists('DP_User', 'isAdmin') && DP_User::isAdmin()) {
			return true;
		}
		if (method_exists('DP_User', 'isAdminGroup') && DP_User::isAdminGroup()) {
			return true;
		}
		if (method_exists('DP_User', 'getAdminId') && (int) DP_User::getAdminId() > 0) {
			return true;
		}
	} catch (Throwable $e) {
		return false;
	}
	return false;
}

/**
 * Whether the current (or given) storefront user may see sales/purchase MIN prices.
 */
function epc_mv_min_price_viewer_may_see(PDO $db, ?int $userId = null): bool
{
	if (epc_mv_min_price_is_admin_viewer()) {
		return true;
	}
	$acl = epc_mv_min_price_acl_get($db);
	if (empty($acl['restrict'])) {
		return true;
	}
	if ($userId === null) {
		if (class_exists('DP_User') && method_exists('DP_User', 'getUserId')) {
			$userId = (int) DP_User::getUserId();
		} else {
			$userId = 0;
		}
	}
	$userId = (int) $userId;
	if ($userId > 0 && in_array($userId, $acl['user_ids'], true)) {
		return true;
	}
	if ($userId > 0 && $acl['group_ids'] !== array()) {
		$profileGroups = array();
		if (class_exists('DP_User') && method_exists('DP_User', 'getUserProfileById')) {
			$profile = DP_User::getUserProfileById($userId);
			if (is_array($profile) && !empty($profile['groups']) && is_array($profile['groups'])) {
				foreach ($profile['groups'] as $g) {
					$profileGroups[] = (int) $g;
				}
			}
		}
		foreach ($acl['group_ids'] as $allowed) {
			if (in_array((int) $allowed, $profileGroups, true)) {
				return true;
			}
		}
	}
	return false;
}

function epc_mv_min_price_is_min_row(?string $storage): bool
{
	$s = strtolower(trim((string) $storage));
	return $s === EPC_MV_MIN_TIER || $s === 'min' || strpos($s, EPC_MV_MIN_TIER) === 0;
}

function epc_mv_min_price_is_max_row(?string $storage): bool
{
	$s = strtolower(trim((string) $storage));
	return $s === EPC_MV_MAX_TIER || $s === 'max' || strpos($s, EPC_MV_MAX_TIER) === 0;
}

/**
 * Storage value shown on storefront (hide internal tier markers).
 */
function epc_mv_min_price_display_storage(?string $storage): string
{
	$s = trim((string) $storage);
	if (epc_mv_min_price_is_min_row($s) || epc_mv_min_price_is_max_row($s)) {
		return '';
	}
	return $s;
}

/**
 * True when a price-list caption is a sales/purchase typed list.
 */
function epc_mv_min_price_list_is_typed(?string $listName): bool
{
	$name = (string) $listName;
	return (strpos($name, ' · Sales') !== false)
		|| (strpos($name, ' · Purchase') !== false)
		|| (stripos($name, ' sales') !== false)
		|| (stripos($name, ' purchase') !== false);
}

/**
 * Skip a prices_data row when it is a restricted min-tier offer.
 *
 * @param array<string,mixed> $row
 */
function epc_mv_min_price_should_hide_row(PDO $db, array $row, ?int $userId = null): bool
{
	if (!epc_mv_min_price_is_min_row((string) ($row['storage'] ?? ''))) {
		return false;
	}
	return !epc_mv_min_price_viewer_may_see($db, $userId);
}

/**
 * @return list<array{id:int,value:string}>
 */
function epc_mv_min_price_list_customer_groups(PDO $db): array
{
	$out = array();
	try {
		$st = $db->query('SELECT `id`, `value` FROM `groups` WHERE IFNULL(`for_backend`,0) = 0 ORDER BY `value` ASC');
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$id = (int) ($row['id'] ?? 0);
			$label = trim((string) ($row['value'] ?? ''));
			if ($id > 0 && $label !== '') {
				$out[] = array('id' => $id, 'value' => $label);
			}
		}
	} catch (Throwable $e) {
		return array();
	}
	return $out;
}
