<?php
/**
 * Webhooks v1 — HMAC-signed HTTP POST delivery with retry and dead-letter queue.
 *
 * Usage:
 *   require_once __DIR__ . '/epc_webhooks.php';
 *
 *   // Register webhook
 *   epc_webhooks_register($pdo, [
 *       'url' => 'https://example.com/webhook',
 *       'secret' => 'my-shared-secret',
 *       'events' => ['order.placed', 'invoice.posted'],
 *       'tenant_key' => 'epartscart',
 *   ]);
 *
 *   // Dispatch is called automatically by epc_event_emit()
 *   // Manual dispatch for testing:
 *   epc_webhooks_dispatch($pdo, $eventId, 'order.placed', $payload, 'epartscart');
 *
 * Delivery:
 *   POST {url}
 *   Content-Type: application/json
 *   X-EPC-Event: order.placed
 *   X-EPC-Delivery: {delivery_id}
 *   X-EPC-Signature: sha256={hmac}
 *   X-EPC-Timestamp: {unix_ts}
 *
 * Verification (receiver):
 *   $expected = 'sha256=' . hash_hmac('sha256', $rawBody, $secret);
 *   if (!hash_equals($expected, $_SERVER['HTTP_X_EPC_SIGNATURE'])) { abort(401); }
 */
declare(strict_types=1);
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

/* ─────────────────── Schema ─────────────────── */

function epc_webhooks_ensure_schema(PDO $pdo): void
{
	static $done = false;
	if ($done) { return; }
	$done = true;

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_webhooks` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`tenant_key` VARCHAR(64) NOT NULL DEFAULT \'__platform__\',
			`url` VARCHAR(512) NOT NULL,
			`secret_hash` VARCHAR(64) NOT NULL DEFAULT \'\',
			`secret_encrypted` VARCHAR(512) NOT NULL DEFAULT \'\',
			`events` TEXT NOT NULL,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`description` VARCHAR(255) NOT NULL DEFAULT \'\',
			`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			`updated_at` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
			INDEX `idx_tenant_active` (`tenant_key`, `active`),
			INDEX `idx_active_events` (`active`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_webhook_deliveries` (
			`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`webhook_id` INT UNSIGNED NOT NULL,
			`event_id` BIGINT UNSIGNED NOT NULL,
			`event_type` VARCHAR(64) NOT NULL,
			`status` ENUM(\'pending\',\'delivered\',\'failed\',\'dlq\') NOT NULL DEFAULT \'pending\',
			`http_status` SMALLINT NOT NULL DEFAULT 0,
			`response_body` TEXT NULL,
			`error_message` VARCHAR(512) NOT NULL DEFAULT \'\',
			`attempt` TINYINT NOT NULL DEFAULT 0,
			`max_attempts` TINYINT NOT NULL DEFAULT 5,
			`next_retry_at` DATETIME NULL,
			`delivered_at` DATETIME NULL,
			`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			INDEX `idx_status_retry` (`status`, `next_retry_at`),
			INDEX `idx_webhook` (`webhook_id`, `created_at`),
			INDEX `idx_event` (`event_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_webhook_dlq` (
			`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`delivery_id` BIGINT UNSIGNED NOT NULL,
			`webhook_id` INT UNSIGNED NOT NULL,
			`event_id` BIGINT UNSIGNED NOT NULL,
			`event_type` VARCHAR(64) NOT NULL,
			`payload_json` MEDIUMTEXT NOT NULL,
			`last_error` VARCHAR(512) NOT NULL DEFAULT \'\',
			`last_http_status` SMALLINT NOT NULL DEFAULT 0,
			`attempts` TINYINT NOT NULL DEFAULT 0,
			`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			`resolved_at` DATETIME NULL,
			`resolved` TINYINT(1) NOT NULL DEFAULT 0,
			INDEX `idx_resolved` (`resolved`, `created_at`),
			INDEX `idx_webhook` (`webhook_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);
}

/* ─────────────────── Registration ─────────────────── */

function epc_webhooks_register(PDO $pdo, array $data): array
{
	epc_webhooks_ensure_schema($pdo);

	$url = trim((string) ($data['url'] ?? ''));
	$secret = (string) ($data['secret'] ?? '');
	$events = $data['events'] ?? array('*');
	$tenantKey = (string) ($data['tenant_key'] ?? '__platform__');
	$description = (string) ($data['description'] ?? '');

	if ($url === '' || !filter_var($url, FILTER_VALIDATE_URL)) {
		return array('ok' => false, 'error' => 'Invalid webhook URL');
	}
	if (strpos($url, 'https://') !== 0) {
		return array('ok' => false, 'error' => 'Webhook URL must use HTTPS');
	}

	$secretHash = $secret !== '' ? hash('sha256', $secret) : '';
	$secretEnc = $secret !== '' ? epc_webhooks_encrypt_secret($secret) : '';
	$eventsJson = json_encode(is_array($events) ? $events : array($events));

	$pdo->prepare(
		'INSERT INTO `epc_webhooks` (`tenant_key`, `url`, `secret_hash`, `secret_encrypted`, `events`, `description`)
		 VALUES (?, ?, ?, ?, ?, ?)'
	)->execute(array($tenantKey, $url, $secretHash, $secretEnc, $eventsJson, $description));

	return array('ok' => true, 'webhook_id' => (int) $pdo->lastInsertId());
}

function epc_webhooks_update(PDO $pdo, int $webhookId, array $data): array
{
	epc_webhooks_ensure_schema($pdo);

	$sets = array();
	$params = array();

	if (isset($data['url'])) {
		$url = trim((string) $data['url']);
		if (!filter_var($url, FILTER_VALIDATE_URL) || strpos($url, 'https://') !== 0) {
			return array('ok' => false, 'error' => 'Invalid webhook URL');
		}
		$sets[] = '`url` = ?';
		$params[] = $url;
	}
	if (isset($data['events'])) {
		$sets[] = '`events` = ?';
		$params[] = json_encode($data['events']);
	}
	if (isset($data['active'])) {
		$sets[] = '`active` = ?';
		$params[] = (int) $data['active'];
	}
	if (isset($data['description'])) {
		$sets[] = '`description` = ?';
		$params[] = (string) $data['description'];
	}
	if (isset($data['secret'])) {
		$secret = (string) $data['secret'];
		$sets[] = '`secret_hash` = ?';
		$params[] = $secret !== '' ? hash('sha256', $secret) : '';
		$sets[] = '`secret_encrypted` = ?';
		$params[] = $secret !== '' ? epc_webhooks_encrypt_secret($secret) : '';
	}

	if (empty($sets)) {
		return array('ok' => false, 'error' => 'No fields to update');
	}

	$params[] = $webhookId;
	$pdo->prepare('UPDATE `epc_webhooks` SET ' . implode(', ', $sets) . ' WHERE `id` = ?')->execute($params);
	return array('ok' => true);
}

function epc_webhooks_delete(PDO $pdo, int $webhookId): array
{
	epc_webhooks_ensure_schema($pdo);
	$pdo->prepare('UPDATE `epc_webhooks` SET `active` = 0 WHERE `id` = ?')->execute(array($webhookId));
	return array('ok' => true);
}

function epc_webhooks_list(PDO $pdo, string $tenantKey = ''): array
{
	epc_webhooks_ensure_schema($pdo);
	if ($tenantKey !== '') {
		$st = $pdo->prepare('SELECT * FROM `epc_webhooks` WHERE `tenant_key` = ? AND `active` = 1 ORDER BY `id`');
		$st->execute(array($tenantKey));
	} else {
		$st = $pdo->query('SELECT * FROM `epc_webhooks` WHERE `active` = 1 ORDER BY `id`');
	}
	$hooks = $st->fetchAll(PDO::FETCH_ASSOC);
	foreach ($hooks as &$h) {
		$h['events'] = json_decode($h['events'] ?? '[]', true) ?: array();
		unset($h['secret_hash'], $h['secret_encrypted']);
	}
	return $hooks;
}

/* ─────────────────── Dispatch ─────────────────── */

function epc_webhooks_dispatch(PDO $pdo, int $eventId, string $eventType, array $payload, string $tenantKey = '__platform__'): int
{
	epc_webhooks_ensure_schema($pdo);

	// Find matching webhooks
	$st = $pdo->prepare(
		'SELECT * FROM `epc_webhooks` WHERE `active` = 1 AND (`tenant_key` = ? OR `tenant_key` = \'__platform__\')'
	);
	$st->execute(array($tenantKey));
	$hooks = $st->fetchAll(PDO::FETCH_ASSOC);

	$dispatched = 0;
	foreach ($hooks as $hook) {
		$events = json_decode($hook['events'] ?? '[]', true) ?: array();
		if (!in_array('*', $events, true) && !in_array($eventType, $events, true)) {
			continue;
		}

		// Create delivery record
		$pdo->prepare(
			'INSERT INTO `epc_webhook_deliveries` (`webhook_id`, `event_id`, `event_type`, `status`, `next_retry_at`)
			 VALUES (?, ?, ?, \'pending\', NOW())'
		)->execute(array($hook['id'], $eventId, $eventType));
		$deliveryId = (int) $pdo->lastInsertId();

		// Attempt immediate delivery
		$secret = '';
		if ($hook['secret_encrypted'] !== '') {
			$secret = epc_webhooks_decrypt_secret($hook['secret_encrypted']);
		}

		$result = epc_webhooks_deliver($hook['url'], $secret, $eventType, $deliveryId, $payload);

		if ($result['ok']) {
			$pdo->prepare(
				'UPDATE `epc_webhook_deliveries` SET `status` = \'delivered\', `http_status` = ?, `response_body` = ?,
				 `attempt` = 1, `delivered_at` = NOW() WHERE `id` = ?'
			)->execute(array($result['http_status'], substr((string) ($result['response'] ?? ''), 0, 2000), $deliveryId));
		} else {
			// Schedule retry with exponential backoff
			$nextRetry = date('Y-m-d H:i:s', time() + 60); // First retry in 1 minute
			$pdo->prepare(
				'UPDATE `epc_webhook_deliveries` SET `status` = \'failed\', `http_status` = ?, `error_message` = ?,
				 `attempt` = 1, `next_retry_at` = ? WHERE `id` = ?'
			)->execute(array($result['http_status'] ?? 0, substr((string) ($result['error'] ?? ''), 0, 512), $nextRetry, $deliveryId));
		}
		$dispatched++;
	}

	return $dispatched;
}

/* ─────────────────── HTTP Delivery ─────────────────── */

function epc_webhooks_deliver(string $url, string $secret, string $eventType, int $deliveryId, array $payload): array
{
	$body = json_encode($payload, JSON_UNESCAPED_UNICODE);
	$timestamp = time();

	$headers = array(
		'Content-Type: application/json',
		'X-EPC-Event: ' . $eventType,
		'X-EPC-Delivery: ' . $deliveryId,
		'X-EPC-Timestamp: ' . $timestamp,
		'User-Agent: ECOMAE-Webhooks/1.0',
	);

	if ($secret !== '') {
		$signPayload = $timestamp . '.' . $body;
		$signature = 'sha256=' . hash_hmac('sha256', $signPayload, $secret);
		$headers[] = 'X-EPC-Signature: ' . $signature;
	}

	$ch = curl_init();
	curl_setopt_array($ch, array(
		CURLOPT_URL            => $url,
		CURLOPT_POST           => true,
		CURLOPT_POSTFIELDS     => $body,
		CURLOPT_HTTPHEADER     => $headers,
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_TIMEOUT        => 10,
		CURLOPT_CONNECTTIMEOUT => 5,
		CURLOPT_SSL_VERIFYPEER => true,
		CURLOPT_FOLLOWLOCATION => false,
	));

	$response = curl_exec($ch);
	$httpStatus = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$curlError = curl_error($ch);
	curl_close($ch);

	if ($curlError !== '') {
		return array('ok' => false, 'error' => $curlError, 'http_status' => 0);
	}

	$ok = ($httpStatus >= 200 && $httpStatus < 300);
	return array('ok' => $ok, 'http_status' => $httpStatus, 'response' => $response, 'error' => $ok ? '' : 'HTTP ' . $httpStatus);
}

/* ─────────────────── Retry Processing (cron) ─────────────────── */

function epc_webhooks_process_retries(PDO $pdo, int $batchSize = 50): array
{
	epc_webhooks_ensure_schema($pdo);

	$st = $pdo->prepare(
		'SELECT d.*, w.`url`, w.`secret_encrypted`, w.`active` AS `webhook_active`
		 FROM `epc_webhook_deliveries` d
		 JOIN `epc_webhooks` w ON w.`id` = d.`webhook_id`
		 WHERE d.`status` = \'failed\'
		 AND d.`attempt` < d.`max_attempts`
		 AND d.`next_retry_at` <= NOW()
		 ORDER BY d.`next_retry_at` ASC
		 LIMIT ?'
	);
	$st->execute(array($batchSize));
	$deliveries = $st->fetchAll(PDO::FETCH_ASSOC);

	$stats = array('processed' => 0, 'delivered' => 0, 'failed' => 0, 'dlq' => 0);

	foreach ($deliveries as $del) {
		$stats['processed']++;

		if (!(int) $del['webhook_active']) {
			// Webhook disabled — move to DLQ
			epc_webhooks_move_to_dlq($pdo, $del);
			$stats['dlq']++;
			continue;
		}

		// Load event payload
		$evSt = $pdo->prepare('SELECT `payload_json` FROM `epc_events` WHERE `id` = ? LIMIT 1');
		$evSt->execute(array($del['event_id']));
		$payloadJson = $evSt->fetchColumn();
		$payload = json_decode((string) $payloadJson, true) ?: array();

		$secret = '';
		if ($del['secret_encrypted'] !== '') {
			$secret = epc_webhooks_decrypt_secret($del['secret_encrypted']);
		}

		$attempt = (int) $del['attempt'] + 1;
		$result = epc_webhooks_deliver($del['url'], $secret, $del['event_type'], (int) $del['id'], $payload);

		if ($result['ok']) {
			$pdo->prepare(
				'UPDATE `epc_webhook_deliveries` SET `status` = \'delivered\', `http_status` = ?, `response_body` = ?,
				 `attempt` = ?, `delivered_at` = NOW() WHERE `id` = ?'
			)->execute(array($result['http_status'], substr((string) ($result['response'] ?? ''), 0, 2000), $attempt, $del['id']));
			$stats['delivered']++;
		} else {
			if ($attempt >= (int) $del['max_attempts']) {
				// Max retries exhausted — move to DLQ
				$del['payload_json'] = $payloadJson;
				$del['last_error'] = $result['error'] ?? '';
				$del['last_http_status'] = $result['http_status'] ?? 0;
				$del['attempts'] = $attempt;
				epc_webhooks_move_to_dlq($pdo, $del);
				$pdo->prepare('UPDATE `epc_webhook_deliveries` SET `status` = \'dlq\', `attempt` = ? WHERE `id` = ?')
					->execute(array($attempt, $del['id']));
				$stats['dlq']++;
			} else {
				// Schedule next retry with exponential backoff: 1m, 5m, 30m, 2h, 12h
				$delays = array(60, 300, 1800, 7200, 43200);
				$delay = $delays[min($attempt - 1, count($delays) - 1)];
				$nextRetry = date('Y-m-d H:i:s', time() + $delay);

				$pdo->prepare(
					'UPDATE `epc_webhook_deliveries` SET `http_status` = ?, `error_message` = ?,
					 `attempt` = ?, `next_retry_at` = ? WHERE `id` = ?'
				)->execute(array($result['http_status'] ?? 0, substr((string) ($result['error'] ?? ''), 0, 512), $attempt, $nextRetry, $del['id']));
				$stats['failed']++;
			}
		}
	}

	return $stats;
}

/* ─────────────────── Dead Letter Queue ─────────────────── */

function epc_webhooks_move_to_dlq(PDO $pdo, array $delivery): void
{
	$pdo->prepare(
		'INSERT INTO `epc_webhook_dlq` (`delivery_id`, `webhook_id`, `event_id`, `event_type`, `payload_json`,
		 `last_error`, `last_http_status`, `attempts`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		$delivery['id'], $delivery['webhook_id'], $delivery['event_id'], $delivery['event_type'],
		$delivery['payload_json'] ?? '{}',
		$delivery['last_error'] ?? $delivery['error_message'] ?? '',
		$delivery['last_http_status'] ?? $delivery['http_status'] ?? 0,
		$delivery['attempts'] ?? $delivery['attempt'] ?? 0,
	));
}

function epc_webhooks_dlq_list(PDO $pdo, bool $unresolvedOnly = true, int $limit = 50): array
{
	epc_webhooks_ensure_schema($pdo);
	$where = $unresolvedOnly ? 'WHERE `resolved` = 0' : '';
	$st = $pdo->prepare('SELECT * FROM `epc_webhook_dlq` ' . $where . ' ORDER BY `created_at` DESC LIMIT ?');
	$st->execute(array($limit));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_webhooks_dlq_retry(PDO $pdo, int $dlqId): array
{
	epc_webhooks_ensure_schema($pdo);

	$st = $pdo->prepare('SELECT q.*, w.`url`, w.`secret_encrypted`, w.`active` FROM `epc_webhook_dlq` q JOIN `epc_webhooks` w ON w.`id` = q.`webhook_id` WHERE q.`id` = ? AND q.`resolved` = 0 LIMIT 1');
	$st->execute(array($dlqId));
	$item = $st->fetch(PDO::FETCH_ASSOC);
	if (!$item) {
		return array('ok' => false, 'error' => 'DLQ item not found or already resolved');
	}

	$payload = json_decode($item['payload_json'] ?? '{}', true) ?: array();
	$secret = $item['secret_encrypted'] !== '' ? epc_webhooks_decrypt_secret($item['secret_encrypted']) : '';
	$result = epc_webhooks_deliver($item['url'], $secret, $item['event_type'], (int) $item['delivery_id'], $payload);

	if ($result['ok']) {
		$pdo->prepare('UPDATE `epc_webhook_dlq` SET `resolved` = 1, `resolved_at` = NOW() WHERE `id` = ?')->execute(array($dlqId));
		$pdo->prepare('UPDATE `epc_webhook_deliveries` SET `status` = \'delivered\', `delivered_at` = NOW() WHERE `id` = ?')->execute(array($item['delivery_id']));
		return array('ok' => true, 'message' => 'DLQ item delivered successfully');
	}

	return array('ok' => false, 'error' => $result['error'] ?? 'Delivery failed', 'http_status' => $result['http_status'] ?? 0);
}

function epc_webhooks_dlq_resolve(PDO $pdo, int $dlqId): array
{
	epc_webhooks_ensure_schema($pdo);
	$pdo->prepare('UPDATE `epc_webhook_dlq` SET `resolved` = 1, `resolved_at` = NOW() WHERE `id` = ?')->execute(array($dlqId));
	return array('ok' => true);
}

/* ─────────────────── Stats ─────────────────── */

function epc_webhooks_delivery_stats(PDO $pdo, int $hours = 24): array
{
	epc_webhooks_ensure_schema($pdo);
	$since = date('Y-m-d H:i:s', time() - $hours * 3600);

	$st = $pdo->prepare(
		'SELECT `status`, COUNT(*) AS `count`
		 FROM `epc_webhook_deliveries`
		 WHERE `created_at` >= ?
		 GROUP BY `status`'
	);
	$st->execute(array($since));
	$rows = $st->fetchAll(PDO::FETCH_KEY_PAIR);

	$dlqSt = $pdo->query('SELECT COUNT(*) FROM `epc_webhook_dlq` WHERE `resolved` = 0');
	$dlqCount = (int) $dlqSt->fetchColumn();

	return array(
		'pending'   => (int) ($rows['pending'] ?? 0),
		'delivered' => (int) ($rows['delivered'] ?? 0),
		'failed'    => (int) ($rows['failed'] ?? 0),
		'dlq'       => $dlqCount,
		'period_hours' => $hours,
	);
}

/* ─────────────────── Secret Encryption ─────────────────── */

function epc_webhooks_encryption_key(): string
{
	// Derive key from deploy token — in production, use a dedicated key
	$base = 'epartscart-deploy-2026';
	return hash('sha256', 'epc-webhooks-secret-key:' . $base, true);
}

function epc_webhooks_encrypt_secret(string $plaintext): string
{
	$key = epc_webhooks_encryption_key();
	$iv = random_bytes(16);
	$encrypted = openssl_encrypt($plaintext, 'aes-256-cbc', $key, OPENSSL_RAW_DATA, $iv);
	if ($encrypted === false) {
		return '';
	}
	return base64_encode($iv . $encrypted);
}

function epc_webhooks_decrypt_secret(string $ciphertext): string
{
	if ($ciphertext === '') {
		return '';
	}
	$key = epc_webhooks_encryption_key();
	$data = base64_decode($ciphertext, true);
	if ($data === false || strlen($data) < 17) {
		return '';
	}
	$iv = substr($data, 0, 16);
	$encrypted = substr($data, 16);
	$decrypted = openssl_decrypt($encrypted, 'aes-256-cbc', $key, OPENSSL_RAW_DATA, $iv);
	return $decrypted !== false ? $decrypted : '';
}

/* ─────────────────── HMAC Verification Helper (for receivers) ─────────────────── */

function epc_webhooks_verify_signature(string $rawBody, string $secret, string $signatureHeader, string $timestampHeader = ''): bool
{
	if ($secret === '' || $signatureHeader === '') {
		return false;
	}

	$timestamp = $timestampHeader !== '' ? $timestampHeader : (string) time();
	$signPayload = $timestamp . '.' . $rawBody;
	$expected = 'sha256=' . hash_hmac('sha256', $signPayload, $secret);

	return hash_equals($expected, $signatureHeader);
}
