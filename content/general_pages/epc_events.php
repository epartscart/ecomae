<?php
/**
 * Platform Event Bus — structured events for webhooks, audit, and integrations.
 *
 * Usage:
 *   require_once __DIR__ . '/epc_events.php';
 *   epc_event_emit($pdo, 'order.placed', ['order_id' => 123, 'total' => 500.00], 'epartscart');
 *
 * Supported event types:
 *   order.placed, order.updated, order.cancelled, order.shipped,
 *   invoice.posted, invoice.paid, invoice.credit_note,
 *   stock.below, stock.adjusted, stock.received,
 *   tenant.created, tenant.updated, tenant.suspended,
 *   user.login, user.mfa_enrolled, user.role_changed,
 *   erp.voucher_posted, erp.period_closed,
 *   audit.isolation_check, audit.mfa_event
 */
declare(strict_types=1);
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

/* ─────────────────── Schema ─────────────────── */

function epc_events_ensure_schema(PDO $pdo): void
{
	static $done = false;
	if ($done) { return; }
	$done = true;

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_events` (
			`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`event_type` VARCHAR(64) NOT NULL,
			`tenant_key` VARCHAR(64) NOT NULL DEFAULT \'__platform__\',
			`payload_json` MEDIUMTEXT NOT NULL,
			`actor_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`actor_type` ENUM(\'user\',\'system\',\'cron\',\'api\') NOT NULL DEFAULT \'system\',
			`idempotency_key` VARCHAR(128) NULL,
			`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			INDEX `idx_type_created` (`event_type`, `created_at`),
			INDEX `idx_tenant_created` (`tenant_key`, `created_at`),
			INDEX `idx_created` (`created_at`),
			UNIQUE KEY `idx_idempotency` (`idempotency_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);
}

/* ─────────────────── Emit ─────────────────── */

function epc_event_emit(PDO $pdo, string $eventType, array $payload = array(), string $tenantKey = '__platform__', int $actorId = 0, string $actorType = 'system', string $idempotencyKey = ''): int
{
	epc_events_ensure_schema($pdo);

	$payload['_event_type'] = $eventType;
	$payload['_tenant_key'] = $tenantKey;
	$payload['_timestamp'] = date('c');

	$idemKey = $idempotencyKey !== '' ? $idempotencyKey : null;

	try {
		$st = $pdo->prepare(
			'INSERT INTO `epc_events` (`event_type`, `tenant_key`, `payload_json`, `actor_id`, `actor_type`, `idempotency_key`)
			 VALUES (?, ?, ?, ?, ?, ?)'
		);
		$st->execute(array($eventType, $tenantKey, json_encode($payload, JSON_UNESCAPED_UNICODE), $actorId, $actorType, $idemKey));
		$eventId = (int) $pdo->lastInsertId();
	} catch (PDOException $e) {
		if (strpos($e->getMessage(), 'Duplicate entry') !== false) {
			return 0; // Idempotent duplicate — silently skip
		}
		throw $e;
	}

	// Dispatch to webhooks asynchronously
	epc_webhooks_dispatch($pdo, $eventId, $eventType, $payload, $tenantKey);

	return $eventId;
}

/* ─────────────────── Query ─────────────────── */

function epc_events_list(PDO $pdo, array $filters = array(), int $limit = 50, int $offset = 0): array
{
	epc_events_ensure_schema($pdo);

	$where = array('1=1');
	$params = array();

	if (!empty($filters['event_type'])) {
		$where[] = '`event_type` = ?';
		$params[] = $filters['event_type'];
	}
	if (!empty($filters['tenant_key'])) {
		$where[] = '`tenant_key` = ?';
		$params[] = $filters['tenant_key'];
	}
	if (!empty($filters['since'])) {
		$where[] = '`created_at` >= ?';
		$params[] = $filters['since'];
	}
	if (!empty($filters['until'])) {
		$where[] = '`created_at` <= ?';
		$params[] = $filters['until'];
	}

	$sql = 'SELECT * FROM `epc_events` WHERE ' . implode(' AND ', $where)
		. ' ORDER BY `id` DESC LIMIT ' . (int) $limit . ' OFFSET ' . (int) $offset;

	$st = $pdo->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_events_count(PDO $pdo, array $filters = array()): int
{
	epc_events_ensure_schema($pdo);

	$where = array('1=1');
	$params = array();

	if (!empty($filters['event_type'])) {
		$where[] = '`event_type` = ?';
		$params[] = $filters['event_type'];
	}
	if (!empty($filters['tenant_key'])) {
		$where[] = '`tenant_key` = ?';
		$params[] = $filters['tenant_key'];
	}

	$st = $pdo->prepare('SELECT COUNT(*) FROM `epc_events` WHERE ' . implode(' AND ', $where));
	$st->execute($params);
	return (int) $st->fetchColumn();
}

function epc_events_type_summary(PDO $pdo, string $since = ''): array
{
	epc_events_ensure_schema($pdo);
	if ($since === '') {
		$since = date('Y-m-d H:i:s', time() - 86400 * 7);
	}
	$st = $pdo->prepare(
		'SELECT `event_type`, COUNT(*) AS `count`, MAX(`created_at`) AS `last_at`
		 FROM `epc_events` WHERE `created_at` >= ?
		 GROUP BY `event_type` ORDER BY `count` DESC'
	);
	$st->execute(array($since));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

/* ─────────────────── ERP Emitters ─────────────────── */

/**
 * Emit event when an ERP invoice is posted.
 */
function epc_event_emit_invoice_posted(PDO $pdo, int $invoiceId, float $total, string $tenantKey, int $userId = 0): int
{
	return epc_event_emit($pdo, 'invoice.posted', array(
		'invoice_id' => $invoiceId,
		'total' => $total,
		'currency' => 'AED',
	), $tenantKey, $userId, 'user', 'inv-post-' . $invoiceId);
}

/**
 * Emit event when an ERP invoice is paid.
 */
function epc_event_emit_invoice_paid(PDO $pdo, int $invoiceId, float $amount, string $tenantKey, int $userId = 0): int
{
	return epc_event_emit($pdo, 'invoice.paid', array(
		'invoice_id' => $invoiceId,
		'amount' => $amount,
	), $tenantKey, $userId, 'user', 'inv-paid-' . $invoiceId . '-' . time());
}

/**
 * Emit event when a credit note is issued.
 */
function epc_event_emit_credit_note(PDO $pdo, int $invoiceId, float $amount, string $tenantKey, int $userId = 0): int
{
	return epc_event_emit($pdo, 'invoice.credit_note', array(
		'invoice_id' => $invoiceId,
		'amount' => $amount,
	), $tenantKey, $userId, 'user', 'inv-cn-' . $invoiceId);
}

/**
 * Emit event when a sales order is placed.
 */
function epc_event_emit_order_placed(PDO $pdo, int $orderId, float $total, string $tenantKey, int $userId = 0): int
{
	return epc_event_emit($pdo, 'order.placed', array(
		'order_id' => $orderId,
		'total' => $total,
	), $tenantKey, $userId, $userId > 0 ? 'user' : 'system', 'order-placed-' . $orderId);
}

/**
 * Emit event when an order is shipped.
 */
function epc_event_emit_order_shipped(PDO $pdo, int $orderId, string $trackingNo, string $tenantKey, int $userId = 0): int
{
	return epc_event_emit($pdo, 'order.shipped', array(
		'order_id' => $orderId,
		'tracking' => $trackingNo,
	), $tenantKey, $userId, 'system', 'order-shipped-' . $orderId);
}

/**
 * Emit event when stock goes below reorder level.
 */
function epc_event_emit_stock_below(PDO $pdo, string $sku, int $qty, int $reorderLevel, string $tenantKey): int
{
	return epc_event_emit($pdo, 'stock.below', array(
		'sku' => $sku,
		'qty' => $qty,
		'reorder_level' => $reorderLevel,
	), $tenantKey, 0, 'system', 'stock-below-' . $sku . '-' . date('Ymd'));
}

/**
 * Emit event when stock is adjusted.
 */
function epc_event_emit_stock_adjusted(PDO $pdo, string $sku, int $oldQty, int $newQty, string $reason, string $tenantKey, int $userId = 0): int
{
	return epc_event_emit($pdo, 'stock.adjusted', array(
		'sku' => $sku,
		'old_qty' => $oldQty,
		'new_qty' => $newQty,
		'reason' => $reason,
	), $tenantKey, $userId, 'user');
}

/**
 * Emit event when a payment is received.
 */
function epc_event_emit_payment_received(PDO $pdo, int $paymentId, float $amount, string $method, string $tenantKey, int $userId = 0): int
{
	return epc_event_emit($pdo, 'payment.received', array(
		'payment_id' => $paymentId,
		'amount' => $amount,
		'method' => $method,
	), $tenantKey, $userId, 'system', 'pmt-' . $paymentId);
}

/**
 * Emit event when an ERP voucher/journal is posted.
 */
function epc_event_emit_voucher_posted(PDO $pdo, int $voucherId, string $type, float $amount, string $tenantKey, int $userId = 0): int
{
	return epc_event_emit($pdo, 'erp.voucher_posted', array(
		'voucher_id' => $voucherId,
		'type' => $type,
		'amount' => $amount,
	), $tenantKey, $userId, 'user', 'voucher-' . $voucherId);
}

/**
 * Emit event when a GL period is closed.
 */
function epc_event_emit_period_closed(PDO $pdo, string $yearMonth, string $tenantKey, int $userId = 0): int
{
	return epc_event_emit($pdo, 'erp.period_closed', array(
		'year_month' => $yearMonth,
	), $tenantKey, $userId, 'user', 'period-close-' . $yearMonth);
}

/**
 * Emit event when tenant is created or updated.
 */
function epc_event_emit_tenant(PDO $pdo, string $action, string $tenantKey, array $details = array()): int
{
	return epc_event_emit($pdo, 'tenant.' . $action, $details, $tenantKey, 0, 'system');
}
