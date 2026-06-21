<?php
/**
 * ECOM AE public REST API — Phase 1 (read-only, tenant-scoped via X-API-Key).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';

function epc_api_v1_json(int $status, array $payload): void
{
	if (!headers_sent()) {
		http_response_code($status);
		header('Content-Type: application/json; charset=utf-8');
		header('Cache-Control: no-store');
		header('X-ECOM-API-Version: v1');
	}
	echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
}

function epc_api_v1_error(int $status, string $code, string $message): void
{
	epc_api_v1_json($status, array(
		'ok' => false,
		'error' => array('code' => $code, 'message' => $message),
	));
}

function epc_api_v1_ok(array $data, int $status = 200): void
{
	epc_api_v1_json($status, array_merge(array('ok' => true), $data));
}

function epc_api_v1_route_path(): string
{
	$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		$path = '/';
	}
	$path = '/' . trim(str_replace('\\', '/', $path), '/');
	if (preg_match('#^/epc-api/v1(?:/(.*))?$#', $path, $m)) {
		return trim((string) ($m[1] ?? ''), '/');
	}
	return '';
}

function epc_api_v1_ensure_keys_table(PDO $pdo): void
{
	epc_portal_db_ensure($pdo);
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_api_keys` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`tenant_site_key` VARCHAR(64) NOT NULL,
			`key_hash` CHAR(64) NOT NULL,
			`key_prefix` VARCHAR(16) NOT NULL DEFAULT \'\',
			`label` VARCHAR(120) NOT NULL DEFAULT \'\',
			`scopes_json` TEXT NOT NULL,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`created_at` INT NOT NULL DEFAULT 0,
			`last_used_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `key_hash` (`key_hash`),
			KEY `tenant_site_key` (`tenant_site_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

function epc_api_v1_platform_pdo(): ?PDO
{
	$pdo = epc_portal_platform_pdo();
	if ($pdo instanceof PDO) {
		return $pdo;
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

function epc_api_v1_extract_key(): string
{
	$hdr = '';
	if (!empty($_SERVER['HTTP_X_API_KEY'])) {
		$hdr = (string) $_SERVER['HTTP_X_API_KEY'];
	} elseif (!empty($_SERVER['HTTP_AUTHORIZATION']) && preg_match('/^Bearer\s+(\S+)/i', (string) $_SERVER['HTTP_AUTHORIZATION'], $m)) {
		$hdr = $m[1];
	}
	return trim($hdr);
}

function epc_api_v1_parse_scopes(string $json): array
{
	$scopes = json_decode($json, true);
	if (!is_array($scopes)) {
		return array();
	}
	return array_values(array_filter(array_map('strval', $scopes)));
}

function epc_api_v1_scope_allowed(array $scopes, string $need): bool
{
	if (in_array('*', $scopes, true) || in_array('read:*', $scopes, true)) {
		return true;
	}
	return in_array($need, $scopes, true);
}

function epc_api_v1_auth(PDO $platformPdo, ?string $requiredScope = null): ?array
{
	$raw = epc_api_v1_extract_key();
	if ($raw === '') {
		epc_api_v1_error(401, 'missing_api_key', 'Send X-API-Key header with a valid tenant API key.');
		return null;
	}
	epc_api_v1_ensure_keys_table($platformPdo);
	$hash = hash('sha256', $raw);
	$st = $platformPdo->prepare(
		'SELECT * FROM `epc_api_keys` WHERE `key_hash` = ? AND `active` = 1 LIMIT 1'
	);
	$st->execute(array($hash));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		epc_api_v1_error(401, 'invalid_api_key', 'API key not recognized or revoked.');
		return null;
	}
	$scopes = epc_api_v1_parse_scopes((string) ($row['scopes_json'] ?? '[]'));
	if ($requiredScope !== null && !epc_api_v1_scope_allowed($scopes, $requiredScope)) {
		epc_api_v1_error(403, 'insufficient_scope', 'This key lacks scope: ' . $requiredScope);
		return null;
	}
	$platformPdo->prepare('UPDATE `epc_api_keys` SET `last_used_at` = ? WHERE `id` = ?')->execute(array(time(), (int) $row['id']));
	$tenant = epc_portal_tenant_get($platformPdo, (string) $row['tenant_site_key']);
	if (!$tenant) {
		epc_api_v1_error(403, 'tenant_not_found', 'Tenant linked to this API key is not registered.');
		return null;
	}
	return array(
		'key' => $row,
		'scopes' => $scopes,
		'tenant' => $tenant,
	);
}

function epc_api_v1_tenant_pdo(array $tenantRow): ?PDO
{
	if (function_exists('epc_portal_shared_erp_tenant_pdo')) {
		$pdo = epc_portal_shared_erp_tenant_pdo($tenantRow);
		if ($pdo instanceof PDO) {
			return $pdo;
		}
	}
	$db = trim((string) ($tenantRow['db_name'] ?? ''));
	if ($db === '') {
		return null;
	}
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		$user = trim((string) ($tenantRow['db_user'] ?? ''));
		$pass = (string) ($tenantRow['db_password'] ?? '');
		if ($user === '') {
			$user = (string) $cfg->user;
		}
		if ($pass === '') {
			$pass = (string) $cfg->password;
		}
		return new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		return null;
	}
}

function epc_api_v1_tenant_access_mode(PDO $platformPdo, array $tenantRow): string
{
	$host = trim((string) ($tenantRow['hostname'] ?? ''));
	if ($host === '') {
		return 'full';
	}
	$st = $platformPdo->prepare('SELECT `access_mode` FROM `epc_portal_site_settings` WHERE `host` = ? OR `host` = ? LIMIT 1');
	$st->execute(array($host, preg_replace('/^www\./', '', $host)));
	$mode = (string) $st->fetchColumn();
	return $mode !== '' ? $mode : 'full';
}

function epc_api_v1_handle_health(): void
{
	epc_api_v1_ok(array(
		'service' => 'epc-api',
		'version' => 'v1',
		'phase' => 1,
		'mode' => 'read-only',
		'platform' => 'ECOM AE',
		'time' => gmdate('c'),
		'endpoints' => array(
			'/epc-api/v1/health',
			'/epc-api/v1/capabilities',
			'/epc-api/v1/openapi.json',
			'/epc-api/v1/tenant/info',
			'/epc-api/v1/orders',
			'/epc-api/v1/products/search',
			'/epc-api/v1/erp/dashboard-summary',
		),
	));
}

function epc_api_v1_handle_capabilities(): void
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_data.php';
	$categories = epc_ecomae_platform_super_cp_capability_categories();
	$areas = array();
	foreach (epc_ecomae_platform_super_cp_capability_categories() as $cat => $count) {
		$areas[] = array('area' => $cat, 'capability_count' => $count);
	}
	epc_api_v1_ok(array(
		'api_phase' => 1,
		'capability_areas' => $areas,
		'total_capabilities' => epc_ecomae_platform_super_cp_capability_count(),
		'integrations' => array(
			'public_rest' => 'Phase 1 read-only JSON at /epc-api/v1/',
			'erp_ajax' => 'Internal CP session — not public',
			'future' => array('webhooks', 'e-invoice submit', 'd365 sync', 'marketplace write APIs v2'),
		),
		'docs' => 'https://www.ecomae.com/platform/api-documentation',
	));
}

function epc_api_v1_handle_openapi(): void
{
	$specPath = $_SERVER['DOCUMENT_ROOT'] . '/docs/epc-api-v1-openapi.json';
	if (!is_file($specPath)) {
		epc_api_v1_error(404, 'spec_missing', 'OpenAPI spec file not deployed.');
		return;
	}
	if (!headers_sent()) {
		header('Content-Type: application/json; charset=utf-8');
		header('Cache-Control: public, max-age=300');
		header('X-ECOM-API-Version: v1');
	}
	readfile($specPath);
}

function epc_api_v1_handle_tenant_info(PDO $platformPdo): void
{
	$auth = epc_api_v1_auth($platformPdo, 'read:tenant');
	if ($auth === null) {
		return;
	}
	$t = $auth['tenant'];
	epc_api_v1_ok(array(
		'tenant' => array(
			'site_key' => (string) ($t['site_key'] ?? ''),
			'trade_name' => (string) ($t['trade_name'] ?? ''),
			'hostname' => (string) ($t['hostname'] ?? ''),
			'industry_code' => (string) ($t['industry_code'] ?? ''),
			'status' => (string) ($t['status'] ?? ''),
			'access_mode' => epc_api_v1_tenant_access_mode($platformPdo, $t),
			'erp_only_shared' => !empty($t['erp_only_shared']),
		),
		'key_label' => (string) ($auth['key']['label'] ?? ''),
		'scopes' => $auth['scopes'],
	));
}

function epc_api_v1_handle_orders(PDO $platformPdo): void
{
	$auth = epc_api_v1_auth($platformPdo, 'read:orders');
	if ($auth === null) {
		return;
	}
	$tenantPdo = epc_api_v1_tenant_pdo($auth['tenant']);
	if (!$tenantPdo instanceof PDO) {
		epc_api_v1_error(503, 'tenant_db_unavailable', 'Could not connect to tenant database.');
		return;
	}
	$limit = min(20, max(1, (int) ($_GET['limit'] ?? 20)));
	$statusNameSql = function_exists('epc_erp_order_status_name_sql')
		? epc_erp_order_status_name_sql($tenantPdo)
		: '\'\'';
	$st = $tenantPdo->query(
		'SELECT `id`, `time`, `user_id`, `paid`, `paid_type`, `successfully_created`,
			' . $statusNameSql . ' AS status_name
		 FROM `shop_orders`
		 WHERE `successfully_created` = 1
		 ORDER BY `id` DESC
		 LIMIT ' . (int) $limit
	);
	$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
	$orders = array();
	foreach ($rows as $row) {
		$orders[] = array(
			'id' => (int) ($row['id'] ?? 0),
			'time' => isset($row['time']) ? gmdate('c', (int) $row['time']) : null,
			'user_id' => (int) ($row['user_id'] ?? 0),
			'paid' => !empty($row['paid']),
			'paid_type' => (int) ($row['paid_type'] ?? 0),
			'status_name' => (string) ($row['status_name'] ?? ''),
		);
	}
	epc_api_v1_ok(array(
		'tenant_site_key' => (string) ($auth['tenant']['site_key'] ?? ''),
		'count' => count($orders),
		'limit' => $limit,
		'orders' => $orders,
	));
}

function epc_api_v1_handle_products_search(PDO $platformPdo): void
{
	$auth = epc_api_v1_auth($platformPdo, 'read:products');
	if ($auth === null) {
		return;
	}
	$q = trim((string) ($_GET['q'] ?? ''));
	if ($q === '') {
		epc_api_v1_error(400, 'missing_query', 'Provide q= search term.');
		return;
	}
	$tenantPdo = epc_api_v1_tenant_pdo($auth['tenant']);
	if (!$tenantPdo instanceof PDO) {
		epc_api_v1_error(503, 'tenant_db_unavailable', 'Could not connect to tenant database.');
		return;
	}
	$limit = min(20, max(1, (int) ($_GET['limit'] ?? 20)));
	$like = '%' . $q . '%';
	$st = $tenantPdo->prepare(
		'SELECT `id`, `caption`, `alias`, `category_id`, `published_flag`
		 FROM `shop_catalogue_products`
		 WHERE `published_flag` = 1 AND (`caption` LIKE ? OR `alias` LIKE ?)
		 ORDER BY `caption` ASC
		 LIMIT ' . (int) $limit
	);
	$st->execute(array($like, $like));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	$products = array();
	foreach ($rows as $row) {
		$products[] = array(
			'id' => (int) ($row['id'] ?? 0),
			'caption' => (string) ($row['caption'] ?? ''),
			'alias' => (string) ($row['alias'] ?? ''),
			'category_id' => (int) ($row['category_id'] ?? 0),
		);
	}
	epc_api_v1_ok(array(
		'tenant_site_key' => (string) ($auth['tenant']['site_key'] ?? ''),
		'query' => $q,
		'count' => count($products),
		'products' => $products,
	));
}

function epc_api_v1_handle_erp_dashboard(PDO $platformPdo): void
{
	$auth = epc_api_v1_auth($platformPdo, 'read:erp');
	if ($auth === null) {
		return;
	}
	$tenantPdo = epc_api_v1_tenant_pdo($auth['tenant']);
	if (!$tenantPdo instanceof PDO) {
		epc_api_v1_error(503, 'tenant_db_unavailable', 'Could not connect to tenant database.');
		return;
	}
	if (!function_exists('epc_erp_dashboard')) {
		epc_api_v1_error(503, 'erp_unavailable', 'ERP helpers not available on this stack.');
		return;
	}
	$dash = epc_erp_dashboard($tenantPdo);
	epc_api_v1_ok(array(
		'tenant_site_key' => (string) ($auth['tenant']['site_key'] ?? ''),
		'period' => array(
			'from' => gmdate('c', (int) ($dash['date_from'] ?? 0)),
			'to' => gmdate('c', (int) ($dash['date_to'] ?? 0)),
		),
		'kpis' => array(
			'order_count' => (int) ($dash['order_count'] ?? 0),
			'revenue_ex_vat' => round((float) ($dash['revenue_ex_vat'] ?? 0), 2),
			'profit_ex_vat' => round((float) ($dash['profit_ex_vat'] ?? 0), 2),
			'receivable_due_orders' => round((float) ($dash['receivable_due_orders'] ?? 0), 2),
			'customer_ledger_balance' => round((float) ($dash['customer_ledger_balance'] ?? 0), 2),
			'payable_balance' => round((float) ($dash['payable_balance'] ?? 0), 2),
			'cash_bank_total' => round((float) ($dash['cash_bank_total'] ?? 0), 2),
			'vat_net_payable' => round((float) ($dash['vat_net_payable'] ?? 0), 2),
			'vat_net_status' => (string) ($dash['vat_net_status'] ?? ''),
		),
	));
}

function epc_api_v1_dispatch(): void
{
	$route = epc_api_v1_route_path();
	$platformPdo = epc_api_v1_platform_pdo();
	if (!$platformPdo instanceof PDO) {
		epc_api_v1_error(503, 'platform_db_unavailable', 'Platform database unavailable.');
		return;
	}

	switch ($route) {
		case '':
		case 'health':
			epc_api_v1_handle_health();
			return;
		case 'capabilities':
			epc_api_v1_handle_capabilities();
			return;
		case 'openapi.json':
			epc_api_v1_handle_openapi();
			return;
		case 'tenant/info':
			epc_api_v1_handle_tenant_info($platformPdo);
			return;
		case 'orders':
			epc_api_v1_handle_orders($platformPdo);
			return;
		case 'products/search':
			epc_api_v1_handle_products_search($platformPdo);
			return;
		case 'erp/dashboard-summary':
			epc_api_v1_handle_erp_dashboard($platformPdo);
			return;
		default:
			epc_api_v1_error(404, 'not_found', 'Unknown API route: ' . $route);
	}
}
