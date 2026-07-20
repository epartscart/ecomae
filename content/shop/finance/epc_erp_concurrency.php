<?php
/**
 * ERP multi-user concurrency — edit locks, optimistic versions, idempotency, presence.
 *
 * Designed for many concurrent users on the same tenant (same company DB):
 * - Soft edit locks prevent two people silently overwriting the same record
 * - Optimistic row_version detects stale saves (409 conflict)
 * - Idempotency keys absorb double-clicks / retried POSTs
 * - Presence registry shows who is active in ERP
 * - Atomic claim helpers for status transitions (e.g. e-invoice submit)
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_concurrency_ensure_schema(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$db->exec(
		"CREATE TABLE IF NOT EXISTS `epc_erp_edit_locks` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`entity_type` varchar(48) NOT NULL,
			`entity_id` varchar(64) NOT NULL,
			`user_id` int(11) NOT NULL DEFAULT 0,
			`user_label` varchar(128) NOT NULL DEFAULT '',
			`session_token` varchar(64) NOT NULL DEFAULT '',
			`lock_token` char(40) NOT NULL,
			`acquired_at` int(11) NOT NULL DEFAULT 0,
			`heartbeat_at` int(11) NOT NULL DEFAULT 0,
			`expires_at` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `x_entity` (`entity_type`, `entity_id`),
			KEY `x_user` (`user_id`),
			KEY `x_expires` (`expires_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP soft edit locks (multi-user)'"
	);
	$db->exec(
		"CREATE TABLE IF NOT EXISTS `epc_erp_presence` (
			`user_id` int(11) NOT NULL,
			`session_token` varchar(64) NOT NULL DEFAULT '',
			`user_label` varchar(128) NOT NULL DEFAULT '',
			`tab` varchar(64) NOT NULL DEFAULT '',
			`area` varchar(64) NOT NULL DEFAULT '',
			`entity_type` varchar(48) NOT NULL DEFAULT '',
			`entity_id` varchar(64) NOT NULL DEFAULT '',
			`last_seen` int(11) NOT NULL DEFAULT 0,
			`ip_address` varchar(45) DEFAULT NULL,
			PRIMARY KEY (`user_id`),
			KEY `x_seen` (`last_seen`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP active user presence'"
	);
	$db->exec(
		"CREATE TABLE IF NOT EXISTS `epc_erp_idempotency` (
			`idem_key` varchar(80) NOT NULL,
			`user_id` int(11) NOT NULL DEFAULT 0,
			`action` varchar(64) NOT NULL DEFAULT '',
			`response_json` mediumtext,
			`time_created` int(11) NOT NULL DEFAULT 0,
			`expires_at` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`idem_key`),
			KEY `x_expires` (`expires_at`),
			KEY `x_user` (`user_id`, `time_created`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP POST idempotency (double-submit guard)'"
	);

	// Optimistic versions on hot shared tables (best-effort ALTER).
	if (!function_exists('epc_erp_schema_add_column_if_missing')) {
		require_once __DIR__ . '/epc_erp_schema.php';
	}
	$versioned = array(
		'epc_einvoice_documents',
		'epc_erp_purchases',
		'epc_erp_purchase_orders',
		'epc_document_company',
		'epc_document_templates',
		'epc_erp_suppliers',
		'epc_erp_contacts',
	);
	foreach ($versioned as $table) {
		try {
			$chk = $db->query("SHOW TABLES LIKE " . $db->quote($table));
			if ($chk && $chk->fetchColumn()) {
				epc_erp_schema_add_column_if_missing($db, $table, 'row_version', 'int(11) NOT NULL DEFAULT 1');
			}
		} catch (Throwable $e) {
			// ignore missing tables
		}
	}
	$done = true;
}

function epc_erp_concurrency_user_id(): int
{
	if (!function_exists('epc_erp_admin_id')) {
		require_once __DIR__ . '/epc_erp_helpers.php';
	}
	return (int) epc_erp_admin_id();
}

function epc_erp_concurrency_user_label(int $userId = 0): string
{
	if ($userId <= 0) {
		$userId = epc_erp_concurrency_user_id();
	}
	if ($userId <= 0) {
		return 'User';
	}
	try {
		global $db_link;
		if ($db_link instanceof PDO) {
			$st = $db_link->prepare('SELECT `email`, `name` FROM `users` WHERE `user_id` = ? LIMIT 1');
			$st->execute(array($userId));
			$row = $st->fetch(PDO::FETCH_ASSOC) ?: array();
			$name = trim((string) ($row['name'] ?? ''));
			$email = trim((string) ($row['email'] ?? ''));
			if ($name !== '') {
				return $name;
			}
			if ($email !== '') {
				return $email;
			}
		}
	} catch (Throwable $e) {
		// fall through
	}
	return 'User #' . $userId;
}

function epc_erp_concurrency_session_token(): string
{
	foreach (array('session', 'admin_session') as $ck) {
		if (!empty($_COOKIE[$ck])) {
			return substr(hash('sha256', (string) $_COOKIE[$ck]), 0, 40);
		}
	}
	$ua = (string) ($_SERVER['HTTP_USER_AGENT'] ?? '');
	$ip = (string) ($_SERVER['REMOTE_ADDR'] ?? '');
	return substr(hash('sha256', $ua . '|' . $ip . '|' . epc_erp_concurrency_user_id()), 0, 40);
}

function epc_erp_concurrency_purge(PDO $db): void
{
	$now = time();
	try {
		$db->prepare('DELETE FROM `epc_erp_edit_locks` WHERE `expires_at` < ?')->execute(array($now));
		$db->prepare('DELETE FROM `epc_erp_presence` WHERE `last_seen` < ?')->execute(array($now - 180));
		$db->prepare('DELETE FROM `epc_erp_idempotency` WHERE `expires_at` < ?')->execute(array($now));
	} catch (Throwable $e) {
		// best-effort
	}
}

/**
 * @return array{ok:bool,lock?:array,conflict?:array,message:string}
 */
function epc_erp_edit_lock_acquire(
	PDO $db,
	string $entityType,
	string $entityId,
	int $ttlSeconds = 120,
	bool $force = false
): array {
	epc_erp_concurrency_ensure_schema($db);
	epc_erp_concurrency_purge($db);
	$entityType = preg_replace('/[^a-z0-9_\-]/i', '', $entityType);
	$entityId = substr(preg_replace('/[^\w\-\.:]/', '', $entityId), 0, 64);
	if ($entityType === '' || $entityId === '') {
		return array('ok' => false, 'message' => 'Invalid entity for edit lock');
	}
	$userId = epc_erp_concurrency_user_id();
	if ($userId <= 0) {
		return array('ok' => false, 'message' => 'Sign in required for edit locks');
	}
	$label = epc_erp_concurrency_user_label($userId);
	$session = epc_erp_concurrency_session_token();
	$now = time();
	$expires = $now + max(30, min(600, $ttlSeconds));
	$token = sha1($entityType . '|' . $entityId . '|' . $userId . '|' . $session . '|' . $now . '|' . bin2hex(random_bytes(4)));

	$st = $db->prepare('SELECT * FROM `epc_erp_edit_locks` WHERE `entity_type` = ? AND `entity_id` = ? LIMIT 1');
	$st->execute(array($entityType, $entityId));
	$existing = $st->fetch(PDO::FETCH_ASSOC);

	if ($existing) {
		$sameUser = ((int) $existing['user_id'] === $userId);
		$expired = ((int) $existing['expires_at'] < $now);
		if (!$sameUser && !$expired && !$force) {
			return array(
				'ok' => false,
				'message' => 'Record is being edited by ' . ($existing['user_label'] ?: ('user #' . $existing['user_id'])),
				'conflict' => array(
					'user_id' => (int) $existing['user_id'],
					'user_label' => (string) $existing['user_label'],
					'expires_at' => (int) $existing['expires_at'],
					'heartbeat_at' => (int) $existing['heartbeat_at'],
				),
			);
		}
		$db->prepare(
			'UPDATE `epc_erp_edit_locks` SET `user_id`=?, `user_label`=?, `session_token`=?, `lock_token`=?,
			 `acquired_at`=?, `heartbeat_at`=?, `expires_at`=? WHERE `entity_type`=? AND `entity_id`=?'
		)->execute(array($userId, $label, $session, $token, $now, $now, $expires, $entityType, $entityId));
	} else {
		try {
			$db->prepare(
				'INSERT INTO `epc_erp_edit_locks`
				(`entity_type`,`entity_id`,`user_id`,`user_label`,`session_token`,`lock_token`,`acquired_at`,`heartbeat_at`,`expires_at`)
				VALUES (?,?,?,?,?,?,?,?,?)'
			)->execute(array($entityType, $entityId, $userId, $label, $session, $token, $now, $now, $expires));
		} catch (Throwable $e) {
			// Race: another user inserted first
			$st->execute(array($entityType, $entityId));
			$existing = $st->fetch(PDO::FETCH_ASSOC);
			if ($existing && (int) $existing['user_id'] !== $userId && (int) $existing['expires_at'] >= $now) {
				return array(
					'ok' => false,
					'message' => 'Record is being edited by ' . ($existing['user_label'] ?: ('user #' . $existing['user_id'])),
					'conflict' => array(
						'user_id' => (int) $existing['user_id'],
						'user_label' => (string) $existing['user_label'],
						'expires_at' => (int) $existing['expires_at'],
					),
				);
			}
			$db->prepare(
				'UPDATE `epc_erp_edit_locks` SET `user_id`=?, `user_label`=?, `session_token`=?, `lock_token`=?,
				 `acquired_at`=?, `heartbeat_at`=?, `expires_at`=? WHERE `entity_type`=? AND `entity_id`=?'
			)->execute(array($userId, $label, $session, $token, $now, $now, $expires, $entityType, $entityId));
		}
	}

	if (function_exists('epc_erp_audit_log')) {
		epc_erp_audit_log($db, 'edit_lock_acquire', $entityType, (int) $entityId, 'Edit lock acquired by ' . $label, array(
			'lock_token' => substr($token, 0, 12),
			'expires_at' => $expires,
			'force' => $force ? 1 : 0,
		));
	}

	return array(
		'ok' => true,
		'message' => 'Edit lock acquired',
		'lock' => array(
			'entity_type' => $entityType,
			'entity_id' => $entityId,
			'lock_token' => $token,
			'expires_at' => $expires,
			'user_id' => $userId,
			'user_label' => $label,
		),
	);
}

function epc_erp_edit_lock_heartbeat(PDO $db, string $entityType, string $entityId, string $lockToken, int $ttlSeconds = 120): array
{
	epc_erp_concurrency_ensure_schema($db);
	$userId = epc_erp_concurrency_user_id();
	$now = time();
	$expires = $now + max(30, min(600, $ttlSeconds));
	$st = $db->prepare(
		'UPDATE `epc_erp_edit_locks` SET `heartbeat_at`=?, `expires_at`=?
		 WHERE `entity_type`=? AND `entity_id`=? AND `lock_token`=? AND `user_id`=?'
	);
	$st->execute(array($now, $expires, $entityType, $entityId, $lockToken, $userId));
	if ($st->rowCount() < 1) {
		return array('ok' => false, 'message' => 'Edit lock lost or expired — reload before saving');
	}
	return array('ok' => true, 'message' => 'Lock refreshed', 'expires_at' => $expires);
}

function epc_erp_edit_lock_release(PDO $db, string $entityType, string $entityId, string $lockToken = ''): array
{
	epc_erp_concurrency_ensure_schema($db);
	$userId = epc_erp_concurrency_user_id();
	if ($lockToken !== '') {
		$db->prepare(
			'DELETE FROM `epc_erp_edit_locks` WHERE `entity_type`=? AND `entity_id`=? AND `lock_token`=? AND `user_id`=?'
		)->execute(array($entityType, $entityId, $lockToken, $userId));
	} else {
		$db->prepare(
			'DELETE FROM `epc_erp_edit_locks` WHERE `entity_type`=? AND `entity_id`=? AND `user_id`=?'
		)->execute(array($entityType, $entityId, $userId));
	}
	return array('ok' => true, 'message' => 'Edit lock released');
}

/**
 * Assert caller holds the lock (or none exists / expired). Used before mutations.
 */
function epc_erp_edit_lock_assert(PDO $db, string $entityType, string $entityId, string $lockToken = ''): void
{
	epc_erp_concurrency_ensure_schema($db);
	epc_erp_concurrency_purge($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_edit_locks` WHERE `entity_type`=? AND `entity_id`=? LIMIT 1');
	$st->execute(array($entityType, $entityId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return; // no lock — allow (optimistic version still protects)
	}
	$now = time();
	if ((int) $row['expires_at'] < $now) {
		$db->prepare('DELETE FROM `epc_erp_edit_locks` WHERE `id`=?')->execute(array((int) $row['id']));
		return;
	}
	$userId = epc_erp_concurrency_user_id();
	if ((int) $row['user_id'] === $userId) {
		if ($lockToken !== '' && (string) $row['lock_token'] !== $lockToken) {
			throw new Exception('Your edit lock token is invalid — reload the page');
		}
		return;
	}
	throw new Exception(
		'Cannot save — ' . ($row['user_label'] ?: ('user #' . $row['user_id']))
		. ' is editing this record. Wait or ask them to release the lock.'
	);
}

/**
 * Read-only version check (does not mutate). Use in ajax preflight.
 * @throws Exception on conflict
 */
function epc_erp_version_assert(PDO $db, string $table, int $id, int $expectedVersion, string $idColumn = 'id'): void
{
	if ($expectedVersion <= 0) {
		return;
	}
	$current = epc_erp_version_get($db, $table, $id, $idColumn);
	if ($current > 0 && $current !== $expectedVersion) {
		throw new Exception(
			'Version conflict — another user saved this record'
			. ' (their version ' . $current . ', yours ' . $expectedVersion . ')'
			. '. Reload and re-apply your changes.'
		);
	}
}

/**
 * Optimistic concurrency: bump row_version when expected matches.
 * Call inside the same DB transaction as the business write.
 * @throws Exception on conflict
 */
function epc_erp_version_assert_and_bump(PDO $db, string $table, int $id, int $expectedVersion, string $idColumn = 'id'): int
{
	$table = preg_replace('/[^a-z0-9_]/i', '', $table);
	$idColumn = preg_replace('/[^a-z0-9_]/i', '', $idColumn);
	if ($table === '' || $id <= 0) {
		throw new Exception('Invalid version target');
	}
	if (!function_exists('epc_erp_schema_add_column_if_missing')) {
		require_once __DIR__ . '/epc_erp_schema.php';
	}
	epc_erp_schema_add_column_if_missing($db, $table, 'row_version', 'int(11) NOT NULL DEFAULT 1');

	if ($expectedVersion <= 0) {
		$db->prepare('UPDATE `' . $table . '` SET `row_version` = `row_version` + 1 WHERE `' . $idColumn . '` = ?')
			->execute(array($id));
		$st = $db->prepare('SELECT `row_version` FROM `' . $table . '` WHERE `' . $idColumn . '` = ? LIMIT 1');
		$st->execute(array($id));
		return (int) $st->fetchColumn();
	}

	$st = $db->prepare(
		'UPDATE `' . $table . '` SET `row_version` = `row_version` + 1
		 WHERE `' . $idColumn . '` = ? AND `row_version` = ?'
	);
	$st->execute(array($id, $expectedVersion));
	if ($st->rowCount() < 1) {
		$cur = $db->prepare('SELECT `row_version` FROM `' . $table . '` WHERE `' . $idColumn . '` = ? LIMIT 1');
		$cur->execute(array($id));
		$current = (int) $cur->fetchColumn();
		throw new Exception(
			'Version conflict — another user saved this record'
			. ($current > 0 ? (' (their version ' . $current . ', yours ' . $expectedVersion . ')') : '')
			. '. Reload and re-apply your changes.'
		);
	}
	return $expectedVersion + 1;
}

function epc_erp_version_get(PDO $db, string $table, int $id, string $idColumn = 'id'): int
{
	$table = preg_replace('/[^a-z0-9_]/i', '', $table);
	$idColumn = preg_replace('/[^a-z0-9_]/i', '', $idColumn);
	try {
		$st = $db->prepare('SELECT `row_version` FROM `' . $table . '` WHERE `' . $idColumn . '` = ? LIMIT 1');
		$st->execute(array($id));
		$v = $st->fetchColumn();
		return $v === false ? 0 : (int) $v;
	} catch (Throwable $e) {
		return 0;
	}
}

/**
 * Atomic status claim — prevents double submit/post.
 * @return bool true if this caller won the claim
 */
function epc_erp_claim_status(
	PDO $db,
	string $table,
	int $id,
	array $fromStatuses,
	string $toStatus,
	string $idColumn = 'id',
	string $statusColumn = 'status'
): bool {
	$table = preg_replace('/[^a-z0-9_]/i', '', $table);
	$idColumn = preg_replace('/[^a-z0-9_]/i', '', $idColumn);
	$statusColumn = preg_replace('/[^a-z0-9_]/i', '', $statusColumn);
	if ($table === '' || $id <= 0 || !$fromStatuses) {
		return false;
	}
	$placeholders = implode(',', array_fill(0, count($fromStatuses), '?'));
	$params = array_merge(array($toStatus, time(), $id), array_values($fromStatuses));
	$st = $db->prepare(
		'UPDATE `' . $table . '` SET `' . $statusColumn . '` = ?, `time_updated` = ?
		 WHERE `' . $idColumn . '` = ? AND `' . $statusColumn . '` IN (' . $placeholders . ')'
	);
	$st->execute($params);
	return $st->rowCount() > 0;
}

function epc_erp_presence_heartbeat(PDO $db, array $ctx = array()): array
{
	epc_erp_concurrency_ensure_schema($db);
	$userId = epc_erp_concurrency_user_id();
	if ($userId <= 0) {
		return array('ok' => false, 'active' => array(), 'count' => 0);
	}
	$now = time();
	$label = epc_erp_concurrency_user_label($userId);
	$session = epc_erp_concurrency_session_token();
	$ip = function_exists('epc_erp_audit_client_ip') ? epc_erp_audit_client_ip() : (string) ($_SERVER['REMOTE_ADDR'] ?? '');
	$db->prepare(
		'INSERT INTO `epc_erp_presence`
		(`user_id`,`session_token`,`user_label`,`tab`,`area`,`entity_type`,`entity_id`,`last_seen`,`ip_address`)
		VALUES (?,?,?,?,?,?,?,?,?)
		ON DUPLICATE KEY UPDATE
		 `session_token`=VALUES(`session_token`), `user_label`=VALUES(`user_label`),
		 `tab`=VALUES(`tab`), `area`=VALUES(`area`),
		 `entity_type`=VALUES(`entity_type`), `entity_id`=VALUES(`entity_id`),
		 `last_seen`=VALUES(`last_seen`), `ip_address`=VALUES(`ip_address`)'
	)->execute(array(
		$userId,
		$session,
		$label,
		substr((string) ($ctx['tab'] ?? $_GET['tab'] ?? $_POST['tab'] ?? ''), 0, 64),
		substr((string) ($ctx['area'] ?? $_GET['area'] ?? $_POST['area'] ?? ''), 0, 64),
		substr((string) ($ctx['entity_type'] ?? ''), 0, 48),
		substr((string) ($ctx['entity_id'] ?? ''), 0, 64),
		$now,
		substr($ip, 0, 45),
	));
	epc_erp_concurrency_purge($db);
	$st = $db->prepare('SELECT `user_id`,`user_label`,`tab`,`area`,`entity_type`,`entity_id`,`last_seen` FROM `epc_erp_presence` WHERE `last_seen` >= ? ORDER BY `last_seen` DESC LIMIT 200');
	$st->execute(array($now - 120));
	$active = $st->fetchAll(PDO::FETCH_ASSOC);
	return array('ok' => true, 'active' => $active, 'count' => count($active), 'self_user_id' => $userId);
}

/**
 * Idempotency: return cached response array if key already completed.
 * @return array|null
 */
function epc_erp_idempotency_get(PDO $db, string $key): ?array
{
	$key = substr(trim($key), 0, 80);
	if ($key === '') {
		return null;
	}
	epc_erp_concurrency_ensure_schema($db);
	$st = $db->prepare('SELECT `response_json`, `expires_at` FROM `epc_erp_idempotency` WHERE `idem_key` = ? LIMIT 1');
	$st->execute(array($key));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row || (int) $row['expires_at'] < time()) {
		return null;
	}
	$decoded = json_decode((string) $row['response_json'], true);
	return is_array($decoded) ? $decoded : null;
}

function epc_erp_idempotency_store(PDO $db, string $key, string $action, array $response, int $ttlSeconds = 600): void
{
	$key = substr(trim($key), 0, 80);
	if ($key === '') {
		return;
	}
	epc_erp_concurrency_ensure_schema($db);
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_erp_idempotency` (`idem_key`,`user_id`,`action`,`response_json`,`time_created`,`expires_at`)
		 VALUES (?,?,?,?,?,?)
		 ON DUPLICATE KEY UPDATE `response_json`=VALUES(`response_json`), `expires_at`=VALUES(`expires_at`)'
	)->execute(array(
		$key,
		epc_erp_concurrency_user_id(),
		substr($action, 0, 64),
		json_encode($response, JSON_UNESCAPED_UNICODE),
		$now,
		$now + max(60, min(3600, $ttlSeconds)),
	));
}

/**
 * Map ajax action → entity for lock enforcement (when POST carries an id).
 * @return array{0:string,1:string}|null [entity_type, entity_id]
 */
function epc_erp_concurrency_action_entity(string $action, array $post): ?array
{
	$map = array(
		'invoice_save' => array('invoice', array('id', 'invoice_id', 'document_id')),
		'einvoice_submit' => array('invoice', array('document_id', 'id')),
		'einvoice_credit_note' => array('invoice', array('document_id', 'id')),
		'einvoice_save_seller' => array('seller_profile', array('_fixed' => '1')),
		'einvoice_save_asp' => array('asp_settings', array('_fixed' => '1')),
		'save_company' => array('document_company', array('_fixed' => '1')),
		'save_template' => array('document_template', array('code')),
		'po_save' => array('purchase_order', array('id', 'po_id')),
		'po_status' => array('purchase_order', array('po_id', 'id')),
		'create_purchase' => null, // create — idempotency only
		'supplier_payment' => array('purchase', array('purchase_id', 'id')),
		'document_delete' => array('erp_document', array('doc_id', 'id')),
		'gl_reverse' => array('gl_journal', array('journal_id', 'id')),
	);
	if (!isset($map[$action])) {
		return null;
	}
	$spec = $map[$action];
	if ($spec === null) {
		return null;
	}
	$type = $spec[0];
	$keys = $spec[1];
	if (isset($keys['_fixed'])) {
		return array($type, (string) $keys['_fixed']);
	}
	foreach ($keys as $k) {
		if (!empty($post[$k])) {
			return array($type, (string) $post[$k]);
		}
	}
	// Creates without id — no lock needed
	return null;
}

/** Actions that must not run twice on network retry. */
function epc_erp_concurrency_idempotent_actions(): array
{
	return array(
		'create_purchase', 'create_supplier', 'einvoice_create', 'einvoice_submit',
		'einvoice_credit_note', 'invoice_save', 'invoice_from_order', 'po_save',
		'supplier_payment', 'gl_post_journal', 'document_upload', 'upload_attachment',
	);
}

/**
 * Pre-flight for ajax mutations. Throws Exception on lock conflict.
 * Returns cached idempotent response array if replay, else null.
 */
function epc_erp_concurrency_preflight(PDO $db, string $action, array $post): ?array
{
	epc_erp_concurrency_ensure_schema($db);

	$idemKey = trim((string) ($post['idempotency_key'] ?? $post['idem_key'] ?? ''));
	if ($idemKey !== '' && in_array($action, epc_erp_concurrency_idempotent_actions(), true)) {
		$cached = epc_erp_idempotency_get($db, $idemKey);
		if ($cached !== null) {
			$cached['_idempotent_replay'] = true;
			return $cached;
		}
	}

	$entity = epc_erp_concurrency_action_entity($action, $post);
	if ($entity) {
		$lockToken = (string) ($post['edit_lock_token'] ?? $post['lock_token'] ?? '');
		epc_erp_edit_lock_assert($db, $entity[0], $entity[1], $lockToken);
	}

	// Optimistic version check only (bump happens inside domain writers).
	$expected = (int) ($post['expected_version'] ?? 0);
	if ($expected > 0 && $entity) {
		$tableMap = array(
			'invoice' => 'epc_einvoice_documents',
			'purchase' => 'epc_erp_purchases',
			'purchase_order' => 'epc_erp_purchase_orders',
			'document_company' => 'epc_document_company',
			'document_template' => 'epc_document_templates',
		);
		if (isset($tableMap[$entity[0]]) && ctype_digit((string) $entity[1])) {
			epc_erp_version_assert($db, $tableMap[$entity[0]], (int) $entity[1], $expected);
		}
	}

	return null;
}
