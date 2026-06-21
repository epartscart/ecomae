<?php
/**
 * External Catalog / Price PRO API clients — platform DB (ecomae), X-API-Key auth.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
if (!function_exists('epc_portal_platform_pdo')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
}

function epc_api_clients_json_error(int $status, string $code, string $message): void
{
	if (!headers_sent()) {
		http_response_code($status);
		header('Content-Type: application/json; charset=utf-8');
		header('Cache-Control: no-store');
		header('X-ECOM-API-Client: 1');
	}
	echo json_encode(array(
		'ok' => false,
		'error' => array('code' => $code, 'message' => $message),
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

function epc_api_clients_platform_pdo(): ?PDO
{
	if (function_exists('epc_api_v1_platform_pdo')) {
		$p = epc_api_v1_platform_pdo();
		if ($p instanceof PDO) {
			return $p;
		}
	}
	$p = epc_portal_platform_pdo();
	if ($p instanceof PDO) {
		return $p;
	}
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		return new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		return null;
	}
}

function epc_api_clients_ensure_table(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_api_clients` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`client_key_hash` CHAR(64) NOT NULL,
			`client_key_prefix` VARCHAR(32) NOT NULL DEFAULT \'\',
			`product` ENUM(\'catalog\',\'price_pro\',\'both\') NOT NULL DEFAULT \'catalog\',
			`label` VARCHAR(120) NOT NULL DEFAULT \'\',
			`contact_email` VARCHAR(190) NOT NULL DEFAULT \'\',
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`daily_limit` INT NOT NULL DEFAULT 1000,
			`calls_today` INT NOT NULL DEFAULT 0,
			`calls_reset_date` DATE NULL,
			`allowed_actions_json` TEXT NOT NULL,
			`time_created` INT NOT NULL DEFAULT 0,
			`time_updated` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `client_key_hash` (`client_key_hash`),
			KEY `product_active` (`product`, `active`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	try {
		$pdo->query('SELECT `client_id` FROM `epc_umapi_usage_log` LIMIT 0');
	} catch (Exception $e) {
		try {
			$pdo->exec('ALTER TABLE `epc_umapi_usage_log` ADD COLUMN `client_id` INT UNSIGNED NULL DEFAULT NULL AFTER `source`, ADD KEY `client_date` (`client_id`, `usage_date`)');
		} catch (Exception $e2) {
		}
	}
}

function epc_api_clients_extract_key(): string
{
	if (!empty($_SERVER['HTTP_X_API_KEY'])) {
		return trim((string) $_SERVER['HTTP_X_API_KEY']);
	}
	if (!empty($_SERVER['HTTP_AUTHORIZATION']) && preg_match('/^Bearer\s+(\S+)/i', (string) $_SERVER['HTTP_AUTHORIZATION'], $m)) {
		return trim($m[1]);
	}
	return '';
}

function epc_api_clients_catalog_actions(): array
{
	return array(
		'manufacturers', 'models', 'modifications', 'categories', 'products', 'articles',
		'article', 'analogs', 'brands', 'vin', 'engines', 'engine_search', 'status',
	);
}

function epc_api_clients_parse_allowed_actions(string $json): array
{
	$json = trim($json);
	if ($json === '' || $json === '*') {
		return array();
	}
	if ($json[0] === '[') {
		$list = json_decode($json, true);
		if (!is_array($list)) {
			return array();
		}
		return array_values(array_filter(array_map(function ($v) {
			return strtolower(preg_replace('/[^a-z0-9_]/', '', (string) $v));
		}, $list)));
	}
	$parts = preg_split('/[\s,]+/', $json, -1, PREG_SPLIT_NO_EMPTY);
	return array_values(array_filter(array_map(function ($v) {
		return strtolower(preg_replace('/[^a-z0-9_]/', '', (string) $v));
	}, $parts ?: array())));
}

function epc_api_clients_is_internal_umapi_request(): bool
{
	if (!empty($GLOBALS['epc_api_client_skip_internal_gate'])) {
		return false;
	}
	$ua = isset($_SERVER['HTTP_USER_AGENT']) ? (string) $_SERVER['HTTP_USER_AGENT'] : '';
	if (stripos($ua, 'ePartsCart offline warm') !== false
		|| stripos($ua, 'epc-site-performance-probe') !== false
		|| stripos($ua, 'offline-resilience-probe') !== false) {
		return true;
	}
	$referer = isset($_SERVER['HTTP_REFERER']) ? (string) $_SERVER['HTTP_REFERER'] : '';
	if ($referer !== '') {
		$patterns = array('/cp/', 'umapi_catalog', 'vehicle_catalog', 'part_search', 'epc_fitment', 'demand_intelligence', 'epc_parts_agent', 'available_brands');
		foreach ($patterns as $p) {
			if (stripos($referer, $p) !== false) {
				return true;
			}
		}
	}
	$source = isset($_REQUEST['source']) ? strtolower(trim((string) $_REQUEST['source'])) : '';
	if (in_array($source, array('cp', 'catalog_ui', 'warm_script', 'probe', 'part_search', 'demand_intel', 'parts_agent'), true)) {
		return true;
	}
	return false;
}

function epc_api_clients_product_for_key(string $raw): ?string
{
	if (preg_match('/^epc_catalog_[a-z0-9_]+$/i', $raw)) {
		return 'catalog';
	}
	if (preg_match('/^epc_pricepro_[a-z0-9_]+$/i', $raw)) {
		return 'price_pro';
	}
	return null;
}

function epc_api_clients_reset_daily_if_needed(PDO $pdo, array $row): void
{
	$today = date('Y-m-d');
	$reset = (string) ($row['calls_reset_date'] ?? '');
	if ($reset === $today) {
		return;
	}
	$pdo->prepare(
		'UPDATE `epc_api_clients` SET `calls_today` = 0, `calls_reset_date` = ?, `time_updated` = ? WHERE `id` = ?'
	)->execute(array($today, time(), (int) $row['id']));
}

function epc_api_clients_fetch_by_hash(PDO $pdo, string $hash): ?array
{
	epc_api_clients_ensure_table($pdo);
	$st = $pdo->prepare('SELECT * FROM `epc_api_clients` WHERE `client_key_hash` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($hash));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_api_clients_product_allowed(array $row, string $needProduct): bool
{
	$product = (string) ($row['product'] ?? 'catalog');
	if ($product === 'both') {
		return true;
	}
	return $product === $needProduct;
}

function epc_api_clients_action_allowed(array $row, string $action): bool
{
	$allowed = epc_api_clients_parse_allowed_actions((string) ($row['allowed_actions_json'] ?? ''));
	if (!$allowed) {
		return true;
	}
	return in_array($action, $allowed, true);
}

function epc_api_clients_consume_quota(PDO $pdo, array $row): bool
{
	epc_api_clients_reset_daily_if_needed($pdo, $row);
	$id = (int) $row['id'];
	$limit = max(1, (int) ($row['daily_limit'] ?? 1000));
	$st = $pdo->prepare(
		'UPDATE `epc_api_clients` SET `calls_today` = `calls_today` + 1, `time_updated` = ?
		 WHERE `id` = ? AND `calls_today` < ?'
	);
	$st->execute(array(time(), $id, $limit));
	return $st->rowCount() > 0;
}

function epc_api_clients_log_usage(PDO $pdo, int $clientId, array $meta): void
{
	try {
		$ip = isset($_SERVER['REMOTE_ADDR']) ? substr((string) $_SERVER['REMOTE_ADDR'], 0, 45) : '';
		$st = $pdo->prepare(
			'INSERT INTO `epc_umapi_usage_log`
			(`usage_date`, `created_at`, `action`, `section`, `source`, `client_id`, `request_path`, `http_status`, `from_cache`, `quota_blocked`, `is_live`, `message`, `ip`)
			VALUES (CURDATE(), ?, ?, ?, ?, ?, ?, ?, 0, ?, 1, ?, ?)'
		);
		$st->execute(array(
			time(),
			substr((string) ($meta['action'] ?? 'unknown'), 0, 40),
			substr((string) ($meta['section'] ?? ''), 0, 20),
			substr((string) ($meta['source'] ?? 'api_client'), 0, 40),
			$clientId > 0 ? $clientId : null,
			substr((string) ($meta['request_path'] ?? ''), 0, 255),
			(int) ($meta['http_status'] ?? 200),
			!empty($meta['quota_blocked']) ? 1 : 0,
			isset($meta['message']) ? substr((string) $meta['message'], 0, 255) : null,
			$ip,
		));
	} catch (Exception $e) {
	}
}

/**
 * Authenticate external client; stores row in $GLOBALS['epc_api_client'].
 */
function epc_api_client_require_auth(string $needProduct, ?string $action = null): array
{
	$pdo = epc_api_clients_platform_pdo();
	if (!$pdo instanceof PDO) {
		epc_api_clients_json_error(503, 'platform_db_unavailable', 'API client registry unavailable.');
	}
	$raw = epc_api_clients_extract_key();
	if ($raw === '') {
		epc_api_clients_json_error(401, 'missing_api_key', 'Send X-API-Key: epc_catalog_… or epc_pricepro_… (issued on onboarding).');
	}
	$keyProduct = epc_api_clients_product_for_key($raw);
	if ($keyProduct === null) {
		epc_api_clients_json_error(401, 'invalid_key_format', 'Key must start with epc_catalog_ or epc_pricepro_.');
	}
	$row = epc_api_clients_fetch_by_hash($pdo, hash('sha256', $raw));
	if (!$row) {
		epc_api_clients_json_error(401, 'invalid_api_key', 'API key not recognized or revoked.');
	}
	if ($keyProduct !== $needProduct && $needProduct !== 'both' && (string) ($row['product'] ?? '') !== 'both') {
		epc_api_clients_json_error(403, 'wrong_product_key', 'This endpoint requires a ' . $needProduct . ' client key.');
	}
	if (!epc_api_clients_product_allowed($row, $needProduct)) {
		epc_api_clients_json_error(403, 'product_not_enabled', 'This key is not enabled for ' . $needProduct . '.');
	}
	if ($action !== null && $action !== '' && !epc_api_clients_action_allowed($row, $action)) {
		epc_api_clients_json_error(403, 'action_not_allowed', 'Action not permitted for this client: ' . $action);
	}
	epc_api_clients_reset_daily_if_needed($pdo, $row);
	$row = epc_api_clients_fetch_by_hash($pdo, hash('sha256', $raw)) ?: $row;
	$limit = max(1, (int) ($row['daily_limit'] ?? 1000));
	$used = (int) ($row['calls_today'] ?? 0);
	if ($used >= $limit) {
		epc_api_clients_log_usage($pdo, (int) $row['id'], array(
			'action' => $action ?: 'auth',
			'quota_blocked' => 1,
			'http_status' => 429,
			'message' => 'Daily quota exceeded',
		));
		epc_api_clients_json_error(429, 'daily_quota_exceeded', 'Daily API quota exceeded. Contact support to raise your limit.');
	}
	if (!epc_api_clients_consume_quota($pdo, $row)) {
		epc_api_clients_json_error(429, 'daily_quota_exceeded', 'Daily API quota exceeded.');
	}
	$GLOBALS['epc_api_client'] = $row;
	$GLOBALS['epc_api_client_key_product'] = $keyProduct;
	return $row;
}

/**
 * Gate for umapi_proxy.php — tenant storefront works without X-API-Key; validate key only when sent.
 */
function epc_api_client_umapi_gate(): void
{
	if (!empty($GLOBALS['epc_api_client_umapi_authed'])) {
		return;
	}
	$action = strtolower(isset($_REQUEST['action']) ? trim((string) $_REQUEST['action']) : '');
	if ($action === '') {
		$action = 'unknown';
	}
	if (in_array($action, array('usage_report'), true)) {
		if (!epc_api_clients_is_internal_umapi_request()) {
			epc_api_clients_json_error(403, 'forbidden', 'usage_report is not available for external API clients.');
		}
		return;
	}
	// Embedded storefront / same-origin fetch: no client key required.
	if (epc_api_clients_extract_key() === '') {
		return;
	}
	$client = epc_api_client_require_auth('catalog', $action);
	$GLOBALS['epc_api_client_umapi_authed'] = true;
	$GLOBALS['epc_umapi_client_daily_limit'] = max(1, (int) ($client['daily_limit'] ?? 1000));
}

function epc_api_clients_status_overlay(array $payload): array
{
	if (empty($GLOBALS['epc_api_client']) || !is_array($GLOBALS['epc_api_client'])) {
		return $payload;
	}
	$c = $GLOBALS['epc_api_client'];
	$limit = max(1, (int) ($c['daily_limit'] ?? 1000));
	$used = (int) ($c['calls_today'] ?? 0);
	$payload['api_client'] = array(
		'label' => (string) ($c['label'] ?? ''),
		'key_prefix' => (string) ($c['client_key_prefix'] ?? ''),
		'product' => (string) ($c['product'] ?? 'catalog'),
		'daily_limit' => $limit,
		'calls_today' => $used,
		'remaining' => max(0, $limit - $used),
	);
	return $payload;
}

function epc_api_clients_make_key(string $product): string
{
	$prefix = $product === 'price_pro' ? 'epc_pricepro_' : 'epc_catalog_';
	return $prefix . bin2hex(random_bytes(12));
}
