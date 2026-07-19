<?php
/**
 * Frontend vendor portal access helpers.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_vendor_schema.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_multivendor_price_ingest.php';

function epc_vendor_lang_href(): string
{
	if (isset($GLOBALS['multilang_params']['lang_href']) && $GLOBALS['multilang_params']['lang_href'] !== '') {
		return rtrim((string) $GLOBALS['multilang_params']['lang_href'], '/');
	}
	return '/en';
}

function epc_vendor_urls(): array
{
	$lang = epc_vendor_lang_href();
	return array(
		'home' => $lang . '/vendor',
		'register' => $lang . '/vendor/register',
		'upload' => $lang . '/vendor/upload',
		'login_store' => $lang . '/users/login',
	);
}

function epc_vendor_group_id(PDO $db): int
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	epc_vendor_ensure_schema($db);
	$cached = epc_vendor_ensure_group($db);
	return $cached;
}

function epc_vendor_user_in_group(PDO $db, int $userId = 0): bool
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if ($userId <= 0) {
		$userId = (int) DP_User::getUserId();
	}
	if ($userId <= 0) {
		return false;
	}
	$gid = epc_vendor_group_id($db);
	if ($gid <= 0) {
		return false;
	}
	$st = $db->prepare('SELECT 1 FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` = ? LIMIT 1');
	$st->execute(array($userId, $gid));
	return (bool) $st->fetchColumn();
}

/**
 * @return array<string,mixed>|null
 */
function epc_vendor_get_account(PDO $db, int $userId = 0): ?array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if ($userId <= 0) {
		$userId = (int) DP_User::getUserId();
	}
	if ($userId <= 0) {
		return null;
	}
	epc_vendor_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_vendor_accounts` WHERE `user_id` = ? LIMIT 1');
	$st->execute(array($userId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_vendor_user_can_access(PDO $db, int $userId = 0): bool
{
	$acc = epc_vendor_get_account($db, $userId);
	if (!$acc) {
		return false;
	}
	return in_array((string) $acc['status'], array('approved', 'active'), true);
}

function epc_vendor_user_can_upload(PDO $db, int $userId = 0): bool
{
	return epc_vendor_user_can_access($db, $userId);
}

/**
 * Create warehouse + inventory price list for a vendor account.
 */
function epc_vendor_provision_warehouse(PDO $db, array &$account): int
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';
	$full = epc_multivendor_sanitize_full((string) ($account['vendor_full'] ?? ''));
	$short = epc_multivendor_sanitize_short((string) ($account['vendor_short'] ?? ''));
	if ($short === '') {
		return 0;
	}
	if ($full === '') {
		$full = $short;
	}
	$listName = $short;
	$price = epc_price_resolve_or_create_list($db, 0, $listName);
	if (!$price) {
		return 0;
	}
	$priceId = (int) $price['id'];
	$storageId = epc_multivendor_ensure_warehouse($db, $full, $short, $priceId, true);
	if ($storageId > 0) {
		$userId = (int) ($account['user_id'] ?? 0);
		if ($userId > 0) {
			try {
				$q = $db->prepare('SELECT `users` FROM `shop_storages` WHERE `id` = ? LIMIT 1');
				$q->execute(array($storageId));
				$raw = (string) $q->fetchColumn();
				$users = json_decode($raw, true);
				if (!is_array($users)) {
					$users = array();
				}
				if (!in_array($userId, $users, true) && !in_array((string) $userId, $users, true)) {
					$users[] = $userId;
					$db->prepare('UPDATE `shop_storages` SET `users` = ? WHERE `id` = ?')
						->execute(array(json_encode($users), $storageId));
				}
			} catch (Exception $e) {
			}
		}
		$db->prepare('UPDATE `epc_vendor_accounts` SET `storage_id` = ?, `updated_at` = ? WHERE `id` = ?')
			->execute(array($storageId, time(), (int) $account['id']));
		$account['storage_id'] = $storageId;
	}
	return $storageId;
}

/**
 * Approve vendor (or auto-approve on register).
 */
function epc_vendor_approve_account(PDO $db, int $accountId, int $approvedBy = 0): bool
{
	epc_vendor_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_vendor_accounts` WHERE `id` = ? LIMIT 1');
	$st->execute(array($accountId));
	$acc = $st->fetch(PDO::FETCH_ASSOC);
	if (!$acc) {
		return false;
	}
	$gid = epc_vendor_group_id($db);
	$db->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)')
		->execute(array((int) $acc['user_id'], $gid));
	$db->prepare(
		'UPDATE `epc_vendor_accounts` SET `status` = \'approved\', `approved_by` = ?, `approved_at` = ?, `updated_at` = ? WHERE `id` = ?'
	)->execute(array($approvedBy, time(), time(), $accountId));
	$acc['status'] = 'approved';
	epc_vendor_provision_warehouse($db, $acc);
	return true;
}

/**
 * @return list<int>
 */
function epc_vendor_price_ids(PDO $db, array $account): array
{
	$ids = array();
	$short = epc_multivendor_sanitize_short((string) ($account['vendor_short'] ?? ''));
	if ($short === '') {
		return $ids;
	}
	$names = array(
		$short,
		$short . ' · Sales',
		$short . ' · Purchase',
	);
	$st = $db->prepare('SELECT `id` FROM `shop_docpart_prices` WHERE `name` = ? LIMIT 1');
	foreach ($names as $name) {
		$st->execute(array($name));
		$id = (int) $st->fetchColumn();
		if ($id > 0) {
			$ids[] = $id;
		}
	}
	$storageId = (int) ($account['storage_id'] ?? 0);
	if ($storageId > 0) {
		try {
			$q = $db->prepare('SELECT `connection_options` FROM `shop_storages` WHERE `id` = ? LIMIT 1');
			$q->execute(array($storageId));
			$opts = json_decode((string) $q->fetchColumn(), true);
			if (is_array($opts)) {
				if (!empty($opts['price_id'])) {
					$ids[] = (int) $opts['price_id'];
				}
				if (!empty($opts['epc_typed_price_ids']) && is_array($opts['epc_typed_price_ids'])) {
					foreach ($opts['epc_typed_price_ids'] as $pid) {
						$ids[] = (int) $pid;
					}
				}
			}
		} catch (Exception $e) {
		}
	}
	$ids = array_values(array_unique(array_filter($ids)));
	return $ids;
}

/**
 * @return list<array<string,mixed>>
 */
function epc_vendor_upload_history(PDO $db, array $account, int $limit = 30): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';
	epc_price_history_ensure_schema($db);
	$priceIds = epc_vendor_price_ids($db, $account);
	$userId = (int) ($account['user_id'] ?? 0);
	$limit = max(1, min(100, $limit));
	if ($priceIds === array() && $userId <= 0) {
		return array();
	}
	if ($priceIds !== array()) {
		$in = implode(',', array_fill(0, count($priceIds), '?'));
		$sql = "SELECT * FROM `epc_price_upload_history`
			WHERE (`price_id` IN ($in) OR (`uploaded_by` = ? AND `upload_source` = 'vendor_portal'))
			ORDER BY `id` DESC LIMIT $limit";
		$bind = $priceIds;
		$bind[] = $userId;
	} else {
		$sql = "SELECT * FROM `epc_price_upload_history`
			WHERE `uploaded_by` = ? AND `upload_source` = 'vendor_portal'
			ORDER BY `id` DESC LIMIT $limit";
		$bind = array($userId);
	}
	$st = $db->prepare($sql);
	$st->execute($bind);
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
