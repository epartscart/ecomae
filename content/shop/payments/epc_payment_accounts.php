<?php
/**
 * Individual payment accounts — office / vendor / platform receive funds.
 * Supports direct merchant credentials + connected-account IDs + payout bank details.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', true);
}

require_once __DIR__ . '/epc_payment_helpers.php';

function epc_pay_accounts_ensure_schema(PDO $db): void
{
	$db->exec(
		"CREATE TABLE IF NOT EXISTS `epc_payment_accounts` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`owner_type` VARCHAR(16) NOT NULL DEFAULT 'platform',
			`owner_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`title` VARCHAR(190) NOT NULL DEFAULT '',
			`handler` VARCHAR(64) NOT NULL DEFAULT '',
			`pay_system_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`mode` VARCHAR(16) NOT NULL DEFAULT 'direct',
			`credentials` MEDIUMTEXT NULL,
			`connected_account_id` VARCHAR(190) NOT NULL DEFAULT '',
			`payout_iban` VARCHAR(64) NOT NULL DEFAULT '',
			`payout_bank` VARCHAR(120) NOT NULL DEFAULT '',
			`payout_name` VARCHAR(190) NOT NULL DEFAULT '',
			`platform_fee_pct` DECIMAL(5,2) NOT NULL DEFAULT 0.00,
			`status` VARCHAR(16) NOT NULL DEFAULT 'active',
			`demo_mode` TINYINT(1) NOT NULL DEFAULT 1,
			`is_default` TINYINT(1) NOT NULL DEFAULT 0,
			`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
			`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `idx_owner` (`owner_type`, `owner_id`),
			KEY `idx_handler` (`handler`),
			KEY `idx_status` (`status`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
	);

	$db->exec(
		"CREATE TABLE IF NOT EXISTS `epc_payment_settlements` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`operation_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`order_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`account_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`owner_type` VARCHAR(16) NOT NULL DEFAULT '',
			`owner_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`handler` VARCHAR(64) NOT NULL DEFAULT '',
			`gross_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
			`fee_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
			`net_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
			`currency` VARCHAR(8) NOT NULL DEFAULT 'AED',
			`status` VARCHAR(24) NOT NULL DEFAULT 'pending',
			`note` VARCHAR(255) NOT NULL DEFAULT '',
			`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
			`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `idx_op` (`operation_id`),
			KEY `idx_order` (`order_id`),
			KEY `idx_account` (`account_id`),
			KEY `idx_status` (`status`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
	);

	// Optional link on accounting ops (best-effort; ignore if no ALTER rights)
	try {
		$st = $db->query("SHOW COLUMNS FROM `shop_users_accounting` LIKE 'epc_payment_account_id'");
		if ($st && !$st->fetchColumn()) {
			$db->exec('ALTER TABLE `shop_users_accounting` ADD COLUMN `epc_payment_account_id` INT UNSIGNED NOT NULL DEFAULT 0');
		}
	} catch (Throwable $e) {
	}
}

function epc_pay_accounts_owner_types(): array
{
	return array(
		'platform' => 'Platform (store default)',
		'office' => 'Shop / office',
		'vendor' => 'Marketplace vendor',
	);
}

function epc_pay_accounts_list_offices(PDO $db): array
{
	try {
		$rows = $db->query('SELECT `id`, `caption` FROM `shop_offices` ORDER BY `id` ASC')->fetchAll(PDO::FETCH_ASSOC);
		return is_array($rows) ? $rows : array();
	} catch (Throwable $e) {
		return array();
	}
}

function epc_pay_accounts_list_vendors(PDO $db): array
{
	try {
		$st = $db->query("SHOW TABLES LIKE 'epc_vendor_accounts'");
		if (!$st || !$st->fetchColumn()) {
			return array();
		}
		$rows = $db->query(
			"SELECT `id`, `vendor_full`, `vendor_short`, `storage_id`, `status`, `billing_email`
			 FROM `epc_vendor_accounts` ORDER BY `id` DESC LIMIT 500"
		)->fetchAll(PDO::FETCH_ASSOC);
		return is_array($rows) ? $rows : array();
	} catch (Throwable $e) {
		return array();
	}
}

function epc_pay_accounts_list(PDO $db, $ownerType = '', $ownerId = 0): array
{
	epc_pay_accounts_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_payment_accounts` WHERE 1=1';
	$bind = array();
	if ($ownerType !== '') {
		$sql .= ' AND `owner_type` = ?';
		$bind[] = $ownerType;
	}
	if ($ownerId > 0) {
		$sql .= ' AND `owner_id` = ?';
		$bind[] = (int)$ownerId;
	}
	$sql .= ' ORDER BY `is_default` DESC, `id` DESC';
	$st = $db->prepare($sql);
	$st->execute($bind);
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_pay_accounts_get(PDO $db, int $id): ?array
{
	epc_pay_accounts_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_payment_accounts` WHERE `id` = ? LIMIT 1');
	$st->execute(array($id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_pay_accounts_decode_credentials($raw): array
{
	if (is_array($raw)) {
		return $raw;
	}
	$j = json_decode((string)$raw, true);
	return is_array($j) ? $j : array();
}

function epc_pay_accounts_save(PDO $db, array $data): int
{
	epc_pay_accounts_ensure_schema($db);
	$id = (int)($data['id'] ?? 0);
	$ownerType = preg_replace('/[^a-z_]/', '', (string)($data['owner_type'] ?? 'platform'));
	if (!isset(epc_pay_accounts_owner_types()[$ownerType])) {
		$ownerType = 'platform';
	}
	$ownerId = (int)($data['owner_id'] ?? 0);
	$handler = preg_replace('/[^a-z0-9_]/', '', (string)($data['handler'] ?? ''));
	$mode = preg_replace('/[^a-z_]/', '', (string)($data['mode'] ?? 'direct'));
	if (!in_array($mode, array('direct', 'connected', 'payout'), true)) {
		$mode = 'direct';
	}
	$title = trim((string)($data['title'] ?? ''));
	$status = preg_replace('/[^a-z_]/', '', (string)($data['status'] ?? 'active'));
	if (!in_array($status, array('active', 'pending', 'disabled'), true)) {
		$status = 'active';
	}
	$creds = $data['credentials'] ?? array();
	if (is_string($creds)) {
		$decoded = json_decode($creds, true);
		$creds = is_array($decoded) ? $decoded : array();
	}
	if (!is_array($creds)) {
		$creds = array();
	}
	$paySystemId = (int)($data['pay_system_id'] ?? 0);
	if ($paySystemId <= 0 && $handler !== '') {
		$st = $db->prepare('SELECT `id` FROM `shop_payment_systems` WHERE `handler` = ? LIMIT 1');
		$st->execute(array($handler));
		$paySystemId = (int)$st->fetchColumn();
	}
	$now = time();
	$isDefault = !empty($data['is_default']) ? 1 : 0;
	$demo = array_key_exists('demo_mode', $data) ? (!empty($data['demo_mode']) ? 1 : 0) : 1;
	$displayTitle = $title !== '' ? $title : ($ownerType . ' #' . $ownerId);
	$credsJson = json_encode($creds, JSON_UNESCAPED_UNICODE);
	$connected = trim((string)($data['connected_account_id'] ?? ''));
	$iban = trim((string)($data['payout_iban'] ?? ''));
	$bank = trim((string)($data['payout_bank'] ?? ''));
	$pname = trim((string)($data['payout_name'] ?? ''));
	$feePct = round((float)($data['platform_fee_pct'] ?? 0), 2);

	if ($isDefault) {
		$db->prepare('UPDATE `epc_payment_accounts` SET `is_default` = 0 WHERE `owner_type` = ? AND `owner_id` = ?')
			->execute(array($ownerType, $ownerId));
	}

	if ($id > 0) {
		$db->prepare(
			'UPDATE `epc_payment_accounts` SET
			`owner_type`=?, `owner_id`=?, `title`=?, `handler`=?, `pay_system_id`=?, `mode`=?,
			`credentials`=?, `connected_account_id`=?, `payout_iban`=?, `payout_bank`=?, `payout_name`=?,
			`platform_fee_pct`=?, `status`=?, `demo_mode`=?, `is_default`=?, `updated_at`=?
			WHERE `id`=?'
		)->execute(array(
			$ownerType, $ownerId, $displayTitle, $handler, $paySystemId, $mode,
			$credsJson, $connected, $iban, $bank, $pname,
			$feePct, $status, $demo, $isDefault, $now, $id,
		));
	} else {
		$db->prepare(
			'INSERT INTO `epc_payment_accounts`
			(`owner_type`,`owner_id`,`title`,`handler`,`pay_system_id`,`mode`,`credentials`,`connected_account_id`,
			 `payout_iban`,`payout_bank`,`payout_name`,`platform_fee_pct`,`status`,`demo_mode`,`is_default`,`created_at`,`updated_at`)
			VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
		)->execute(array(
			$ownerType, $ownerId, $displayTitle, $handler, $paySystemId, $mode,
			$credsJson, $connected, $iban, $bank, $pname,
			$feePct, $status, $demo, $isDefault, $now, $now,
		));
		$id = (int)$db->lastInsertId();
	}

	// Keep legacy office columns in sync for wholesaler path
	if ($ownerType === 'office' && $ownerId > 0 && $status === 'active') {
		try {
			$db->prepare('UPDATE `shop_offices` SET `pay_system_id` = ?, `pay_system_parameters` = ? WHERE `id` = ?')
				->execute(array($paySystemId, json_encode($creds, JSON_UNESCAPED_UNICODE), $ownerId));
		} catch (Throwable $e) {
		}
	}

	return $id;
}

function epc_pay_accounts_find_for_owner(PDO $db, string $ownerType, int $ownerId): ?array
{
	epc_pay_accounts_ensure_schema($db);
	$st = $db->prepare(
		"SELECT * FROM `epc_payment_accounts`
		 WHERE `owner_type` = ? AND `owner_id` = ? AND `status` = 'active'
		 ORDER BY `is_default` DESC, `id` DESC LIMIT 1"
	);
	$st->execute(array($ownerType, $ownerId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

/**
 * Resolve which individual account should receive an order payment.
 * Prefers vendor (by storage) → office → platform default.
 */
function epc_pay_accounts_resolve_for_order(PDO $db, int $orderId): ?array
{
	epc_pay_accounts_ensure_schema($db);
	if ($orderId <= 0) {
		return epc_pay_accounts_find_for_owner($db, 'platform', 0);
	}

	// Vendor via dominant storage on order lines
	try {
		$st = $db->prepare(
			"SELECT `t2_storage_id` AS sid, SUM(`price` * `count_need`) AS amt
			 FROM `shop_orders_items` WHERE `order_id` = ? AND `t2_storage_id` > 0
			 GROUP BY `t2_storage_id` ORDER BY amt DESC LIMIT 1"
		);
		$st->execute(array($orderId));
		$line = $st->fetch(PDO::FETCH_ASSOC);
		if ($line && (int)$line['sid'] > 0) {
			$v = $db->prepare('SELECT `id` FROM `epc_vendor_accounts` WHERE `storage_id` = ? AND `status` IN (\'approved\',\'active\') LIMIT 1');
			$v->execute(array((int)$line['sid']));
			$vendorId = (int)$v->fetchColumn();
			if ($vendorId > 0) {
				$acc = epc_pay_accounts_find_for_owner($db, 'vendor', $vendorId);
				if ($acc) {
					return $acc;
				}
			}
		}
	} catch (Throwable $e) {
	}

	// Office on order
	try {
		$st = $db->prepare('SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1');
		$st->execute(array($orderId));
		$officeId = (int)$st->fetchColumn();
		if ($officeId > 0) {
			$acc = epc_pay_accounts_find_for_owner($db, 'office', $officeId);
			if ($acc) {
				return $acc;
			}
			// Legacy office pay_system_* as virtual account
			$o = $db->prepare('SELECT `id`, `caption`, `pay_system_id`, `pay_system_parameters` FROM `shop_offices` WHERE `id` = ? LIMIT 1');
			$o->execute(array($officeId));
			$office = $o->fetch(PDO::FETCH_ASSOC);
			if ($office && (int)$office['pay_system_id'] > 0) {
				$ps = $db->prepare('SELECT `handler` FROM `shop_payment_systems` WHERE `id` = ? LIMIT 1');
				$ps->execute(array((int)$office['pay_system_id']));
				$handler = (string)$ps->fetchColumn();
				return array(
					'id' => 0,
					'owner_type' => 'office',
					'owner_id' => $officeId,
					'title' => (string)$office['caption'],
					'handler' => $handler,
					'pay_system_id' => (int)$office['pay_system_id'],
					'mode' => 'direct',
					'credentials' => (string)$office['pay_system_parameters'],
					'platform_fee_pct' => 0,
					'demo_mode' => 1,
					'status' => 'active',
					'_virtual' => 1,
				);
			}
		}
	} catch (Throwable $e) {
	}

	return epc_pay_accounts_find_for_owner($db, 'platform', 0);
}

function epc_pay_accounts_credentials_array(array $account): array
{
	$creds = epc_pay_accounts_decode_credentials($account['credentials'] ?? array());
	if (!isset($creds['demo_mode']) && isset($account['demo_mode'])) {
		$creds['demo_mode'] = (int)$account['demo_mode'];
	}
	if (!empty($account['connected_account_id'])) {
		$creds['connected_account_id'] = (string)$account['connected_account_id'];
		$creds['stripe_account'] = (string)$account['connected_account_id'];
	}
	return $creds;
}

function epc_pay_accounts_create_settlement(PDO $db, array $opts): int
{
	epc_pay_accounts_ensure_schema($db);
	$gross = round((float)($opts['gross_amount'] ?? 0), 2);
	$feePct = round((float)($opts['platform_fee_pct'] ?? 0), 2);
	$fee = round($gross * ($feePct / 100), 2);
	$net = round($gross - $fee, 2);
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_payment_settlements`
		(`operation_id`,`order_id`,`account_id`,`owner_type`,`owner_id`,`handler`,`gross_amount`,`fee_amount`,`net_amount`,`currency`,`status`,`note`,`created_at`,`updated_at`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		(int)($opts['operation_id'] ?? 0),
		(int)($opts['order_id'] ?? 0),
		(int)($opts['account_id'] ?? 0),
		(string)($opts['owner_type'] ?? ''),
		(int)($opts['owner_id'] ?? 0),
		(string)($opts['handler'] ?? ''),
		$gross,
		$fee,
		$net,
		(string)($opts['currency'] ?? 'AED'),
		(string)($opts['status'] ?? 'pending'),
		(string)($opts['note'] ?? ''),
		$now,
		$now,
	));
	return (int)$db->lastInsertId();
}

function epc_pay_accounts_list_settlements(PDO $db, int $limit = 40): array
{
	epc_pay_accounts_ensure_schema($db);
	$limit = max(1, min(200, $limit));
	$st = $db->query(
		'SELECT * FROM `epc_payment_settlements` ORDER BY `id` DESC LIMIT ' . $limit
	);
	return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
}

function epc_pay_accounts_mark_settlement(PDO $db, int $id, string $status): void
{
	$status = preg_replace('/[^a-z_]/', '', $status);
	$db->prepare('UPDATE `epc_payment_settlements` SET `status` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array($status, time(), $id));
}

function epc_pay_accounts_seed_platform(PDO $db): void
{
	epc_pay_accounts_ensure_schema($db);
	$existing = epc_pay_accounts_find_for_owner($db, 'platform', 0);
	if ($existing) {
		return;
	}
	$st = $db->query('SELECT `id`, `handler`, `parameters_values` FROM `shop_payment_systems` WHERE `active` = 1 LIMIT 1');
	$row = $st ? $st->fetch(PDO::FETCH_ASSOC) : null;
	if (!$row) {
		$st = $db->query('SELECT `id`, `handler`, `parameters_values` FROM `shop_payment_systems` WHERE `anable` = 1 ORDER BY `id` ASC LIMIT 1');
		$row = $st ? $st->fetch(PDO::FETCH_ASSOC) : null;
	}
	if (!$row) {
		return;
	}
	epc_pay_accounts_save($db, array(
		'owner_type' => 'platform',
		'owner_id' => 0,
		'title' => 'Platform default',
		'handler' => (string)$row['handler'],
		'pay_system_id' => (int)$row['id'],
		'mode' => 'direct',
		'credentials' => epc_pay_accounts_decode_credentials($row['parameters_values']),
		'status' => 'active',
		'demo_mode' => 1,
		'is_default' => 1,
		'platform_fee_pct' => 0,
	));
}

/** Split order lines by vendor storage for settlement preview/records */
function epc_pay_accounts_order_splits(PDO $db, int $orderId): array
{
	$out = array();
	try {
		$st = $db->prepare(
			"SELECT `t2_storage_id` AS sid, SUM(`price` * `count_need`) AS amt
			 FROM `shop_orders_items` WHERE `order_id` = ?
			 GROUP BY `t2_storage_id`"
		);
		$st->execute(array($orderId));
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$sid = (int)$row['sid'];
			$amt = round((float)$row['amt'], 2);
			$vendorId = 0;
			if ($sid > 0) {
				$v = $db->prepare('SELECT `id`, `vendor_full` FROM `epc_vendor_accounts` WHERE `storage_id` = ? LIMIT 1');
				$v->execute(array($sid));
				$vr = $v->fetch(PDO::FETCH_ASSOC);
				if ($vr) {
					$vendorId = (int)$vr['id'];
				}
			}
			$out[] = array(
				'storage_id' => $sid,
				'vendor_id' => $vendorId,
				'amount' => $amt,
				'account' => $vendorId > 0 ? epc_pay_accounts_find_for_owner($db, 'vendor', $vendorId) : null,
			);
		}
	} catch (Throwable $e) {
	}
	return $out;
}
