<?php
/**
 * Power BI integration — what we can ship without Azure AD credentials.
 *
 * Phase A (live now):
 *   - Tenant-scoped JSON/CSV datasets at /epc-api/v1/powerbi/*
 *   - Power BI Desktop/Service connects via Web connector + X-API-Key
 *
 * Phase B (config shell — ready when customer provides workspace IDs):
 *   - Store workspace / report / dataset IDs per site_key
 *   - Optional publish-to-web or secure embed URL iframe in CP
 *   - Azure AD app + embed-token flow reserved for later (needs their Pro)
 *
 * Parallel: Metabase JWT embed remains in epc_metabase_embed.php
 */
declare(strict_types=1);

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

define('EPC_POWER_BI_VERSION', '1.0.0');

function epc_power_bi_ensure_schema(PDO $pdo): void
{
	static $done = false;
	if ($done) {
		return;
	}

	$driver = '';
	try {
		$driver = (string) $pdo->getAttribute(PDO::ATTR_DRIVER_NAME);
	} catch (Throwable $e) {
		$driver = 'mysql';
	}

	if ($driver === 'sqlite') {
		$pdo->exec("
			CREATE TABLE IF NOT EXISTS epc_power_bi_config (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				site_key TEXT NOT NULL UNIQUE,
				workspace_id TEXT NOT NULL DEFAULT '',
				azure_tenant_id TEXT NOT NULL DEFAULT '',
				default_report_id TEXT NOT NULL DEFAULT '',
				default_dataset_id TEXT NOT NULL DEFAULT '',
				embed_url TEXT NOT NULL DEFAULT '',
				embed_mode TEXT NOT NULL DEFAULT 'none',
				notes TEXT NOT NULL DEFAULT '',
				active INTEGER NOT NULL DEFAULT 0,
				created_at TEXT DEFAULT CURRENT_TIMESTAMP,
				updated_at TEXT
			)
		");
		$pdo->exec("
			CREATE TABLE IF NOT EXISTS epc_power_bi_reports (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				site_key TEXT NOT NULL,
				report_id TEXT NOT NULL DEFAULT '',
				report_name TEXT NOT NULL DEFAULT '',
				dataset_id TEXT NOT NULL DEFAULT '',
				category TEXT NOT NULL DEFAULT 'finance',
				embed_url TEXT NOT NULL DEFAULT '',
				active INTEGER NOT NULL DEFAULT 1,
				created_at TEXT DEFAULT CURRENT_TIMESTAMP
			)
		");
	} else {
		$pdo->exec("
			CREATE TABLE IF NOT EXISTS `epc_power_bi_config` (
				`id`                INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
				`site_key`          VARCHAR(64)    NOT NULL DEFAULT '__platform__',
				`workspace_id`      VARCHAR(64)    NOT NULL DEFAULT '',
				`azure_tenant_id`   VARCHAR(64)    NOT NULL DEFAULT '',
				`default_report_id` VARCHAR(64)    NOT NULL DEFAULT '',
				`default_dataset_id` VARCHAR(64)   NOT NULL DEFAULT '',
				`embed_url`         VARCHAR(512)   NOT NULL DEFAULT '',
				`embed_mode`        VARCHAR(16)    NOT NULL DEFAULT 'none',
				`notes`             VARCHAR(512)   NOT NULL DEFAULT '',
				`active`            TINYINT(1)     NOT NULL DEFAULT 0,
				`created_at`        DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
				`updated_at`        DATETIME       NULL ON UPDATE CURRENT_TIMESTAMP,
				UNIQUE KEY `site` (`site_key`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
		");

		$pdo->exec("
			CREATE TABLE IF NOT EXISTS `epc_power_bi_reports` (
				`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
				`site_key`        VARCHAR(64)    NOT NULL,
				`report_id`       VARCHAR(64)    NOT NULL DEFAULT '',
				`report_name`     VARCHAR(128)   NOT NULL DEFAULT '',
				`dataset_id`      VARCHAR(64)    NOT NULL DEFAULT '',
				`category`        VARCHAR(32)    NOT NULL DEFAULT 'finance',
				`embed_url`       VARCHAR(512)   NOT NULL DEFAULT '',
				`active`          TINYINT(1)     NOT NULL DEFAULT 1,
				`created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
				INDEX `idx_site` (`site_key`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
		");
	}

	$done = true;
}

/**
 * Datasets Power BI can refresh via Web connector.
 *
 * @return list<array<string,mixed>>
 */
function epc_power_bi_dataset_catalog(string $baseUrl = ''): array
{
	$base = rtrim($baseUrl !== '' ? $baseUrl : 'https://www.ecomae.com', '/');
	$root = $base . '/epc-api/v1/powerbi';

	return array(
		array(
			'id' => 'catalog',
			'name' => 'Dataset catalog',
			'description' => 'Lists all Power BI–ready endpoints for this tenant.',
			'path' => $root . '/catalog',
			'formats' => array('json'),
			'scope' => 'read:bi',
			'refresh' => 'manual',
		),
		array(
			'id' => 'kpis',
			'name' => 'ERP KPI snapshot',
			'description' => 'Flat KPI rows (revenue, AR, AP, cash, VAT) for cards and scorecards.',
			'path' => $root . '/kpis',
			'formats' => array('json', 'csv'),
			'scope' => 'read:bi',
			'refresh' => 'scheduled',
		),
		array(
			'id' => 'orders',
			'name' => 'Recent orders',
			'description' => 'Successfully created shop orders (tabular).',
			'path' => $root . '/orders',
			'formats' => array('json', 'csv'),
			'scope' => 'read:bi',
			'refresh' => 'scheduled',
			'params' => array('limit' => '1–200 (default 100)'),
		),
		array(
			'id' => 'sales',
			'name' => 'Sales register',
			'description' => 'Completed order sales ex-VAT with paid/due amounts.',
			'path' => $root . '/sales',
			'formats' => array('json', 'csv'),
			'scope' => 'read:bi',
			'refresh' => 'scheduled',
			'params' => array('from' => 'YYYY-MM-DD', 'to' => 'YYYY-MM-DD'),
		),
		array(
			'id' => 'stock',
			'name' => 'Inventory stock',
			'description' => 'On-hand qty, average cost, and stock value by SKU/warehouse.',
			'path' => $root . '/stock',
			'formats' => array('json', 'csv'),
			'scope' => 'read:bi',
			'refresh' => 'scheduled',
		),
		array(
			'id' => 'gl',
			'name' => 'GL trial balance',
			'description' => 'Chart-of-accounts trial balance lines.',
			'path' => $root . '/gl',
			'formats' => array('json', 'csv'),
			'scope' => 'read:bi',
			'refresh' => 'scheduled',
			'params' => array('to' => 'YYYY-MM-DD optional as-of date'),
		),
		array(
			'id' => 'metrics',
			'name' => 'BI metric snapshots',
			'description' => 'Latest materialized KPI snapshots from the BI metrics engine (when computed).',
			'path' => $root . '/metrics',
			'formats' => array('json', 'csv'),
			'scope' => 'read:bi',
			'refresh' => 'scheduled',
		),
	);
}

/**
 * CP step-by-step guide (Portal → Power BI guide).
 *
 * @return list<array{title:string,body:string,tips?:list<string>}>
 */
function epc_power_bi_guide_steps(): array
{
	return array(
		array(
			'title' => 'Step 1 — What Power BI does on ECOM AE',
			'body' => 'Power BI reads live ERP/commerce data from your tenant database through the public API. '
				. 'You build charts in Microsoft Power BI Desktop or Service; ECOM AE only supplies the data. '
				. 'No Azure AD is required for Desktop refresh. Native ERP dashboards in CP continue to work as before.',
			'tips' => array(
				'Each API key is locked to one site_key — tenants never see each other’s data.',
				'Phase A = Web connector (JSON/CSV). Azure secure embed is Phase B when you supply Microsoft credentials.',
			),
		),
		array(
			'title' => 'Step 2 — Open the Power BI page in CP',
			'body' => 'In Control Panel go to <strong>Portal → Power BI</strong> '
				. '(<code>/cp/control/portal/epc_power_bi</code>). '
				. 'Super CP operators can pick any tenant; tenant CP users see only their own site_key. '
				. 'Also open <strong>Portal → Integrations hub</strong> and confirm the Power BI feature is enabled.',
			'tips' => array(
				'If the menu item is missing, run <code>epc-power-bi-setup.php</code> on the platform host once.',
			),
		),
		array(
			'title' => 'Step 3 — Issue a tenant API key',
			'body' => 'Go to <strong>Portal → API documentation guide</strong> (Super CP) and issue a key for the tenant, '
				. 'or ask a platform operator. The key needs scope <code>read:bi</code> '
				. '(existing <code>read:erp</code> or <code>read:*</code> keys also work). '
				. 'Copy the plain key once — only the SHA-256 hash is stored in the database.',
			'tips' => array(
				'Never paste live keys into tickets, chat, or marketing pages.',
				'Revoke by setting <code>active = 0</code> on the key row if compromised.',
			),
		),
		array(
			'title' => 'Step 4 — Smoke-test the dataset URL',
			'body' => 'From a terminal, confirm the key returns data before opening Power BI:',
			'tips' => array(
				'curl -s -H "X-API-Key: YOUR_KEY" https://www.ecomae.com/epc-api/v1/powerbi/catalog',
				'curl -s -H "X-API-Key: YOUR_KEY" "https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv"',
				'Expect HTTP 401 without the header. CSV is easiest for Power BI Web connector.',
			),
		),
		array(
			'title' => 'Step 5 — Connect Power BI Desktop',
			'body' => 'Open <strong>Power BI Desktop → Get data → Web → Advanced</strong>. '
				. 'Paste a dataset URL (example below). Under HTTP request header parameters add '
				. '<code>X-API-Key</code> = your tenant key. Load the table, build visuals, then save the <code>.pbix</code>.',
			'tips' => array(
				'KPI cards: https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv',
				'Orders: …/powerbi/orders?format=csv&amp;limit=200',
				'Sales (date range): …/powerbi/sales?format=csv&amp;from=2026-01-01&amp;to=2026-07-17',
				'Stock: …/powerbi/stock?format=csv',
				'GL trial balance: …/powerbi/gl?format=csv',
			),
		),
		array(
			'title' => 'Step 6 — Publish &amp; schedule refresh',
			'body' => 'Publish the report to Power BI Service (app.powerbi.com). '
				. 'Open the dataset → <strong>Settings → Data source credentials</strong> and keep the same '
				. '<code>X-API-Key</code> header. Turn on scheduled refresh (e.g. hourly or daily).',
			'tips' => array(
				'If refresh fails with 401, the key was revoked or lacks read:bi / read:erp.',
				'If refresh fails with 503, the tenant DB was unavailable — retry or contact platform ops.',
			),
		),
		array(
			'title' => 'Step 7 — Save workspace IDs in CP (optional)',
			'body' => 'On <strong>Portal → Power BI</strong>, paste your Power BI <em>Workspace ID</em>, '
				. '<em>Report ID</em>, and <em>Dataset ID</em> (from the report URL in the browser). '
				. 'Click <strong>Save config</strong>. This does not call Microsoft yet — it stores IDs for operators and for future Azure embed.',
			'tips' => array(
				'Workspace GUID is in the Power BI Service URL path after /groups/.',
				'Register named reports with Add report for a tidy inventory per tenant.',
			),
		),
		array(
			'title' => 'Step 8 — Optional URL embed in CP',
			'body' => 'Set <strong>Embed mode = URL iframe</strong> and paste a share / publish-to-web link that starts with '
				. '<code>https://app.powerbi.com/</code> (or <code>*.powerbi.com</code> / <code>*.powerbi.us</code>). '
				. 'Save — the preview iframe appears on the same CP page. '
				. 'Publish-to-web is public; prefer secure share links for sensitive finance data.',
			'tips' => array(
				'Non-powerbi.com hosts are rejected automatically.',
				'Embed mode = Azure stays blocked until you provide Azure AD app credentials (Step 9).',
			),
		),
		array(
			'title' => 'Step 9 — Later: Azure secure embed (needs your credentials)',
			'body' => 'When you want in-app embed without publish-to-web, create an Azure AD app in your Microsoft tenant, '
				. 'grant Power BI API permissions, and have Power BI Pro / Premium / Embedded capacity. '
				. 'Send the client id/secret and Azure tenant ID to platform ops. '
				. 'We already store workspace/report IDs; token minting activates when those secrets are configured.',
			'tips' => array(
				'Until then, use Desktop + Service refresh (Steps 5–6) — that path is fully live.',
			),
		),
	);
}

/**
 * Capabilities matrix — honest about what needs Microsoft credentials.
 *
 * @return array<string,mixed>
 */
function epc_power_bi_capabilities(): array
{
	return array(
		'version' => EPC_POWER_BI_VERSION,
		'available_now' => array(
			'web_connector_json' => true,
			'web_connector_csv' => true,
			'api_key_auth' => true,
			'tenant_isolation' => true,
			'workspace_config_storage' => true,
			'url_embed_iframe' => true,
			'native_erp_dashboard' => true,
			'metabase_embed_parallel' => true,
		),
		'needs_customer_credentials' => array(
			'azure_ad_app' => 'Azure AD application (client id/secret) in their Microsoft tenant',
			'power_bi_pro_or_embedded' => 'Power BI Pro / Premium / Embedded capacity for secure embed tokens',
			'workspace_access' => 'Workspace + report IDs from their Power BI service',
		),
		'not_in_scope_phase_a' => array(
			'azure_embed_token_generation',
			'power_bi_rest_admin_apis',
			'row_level_security_via_azure',
		),
		'connect_guide' => array(
			'1. Issue a tenant API key with scope read:bi (or read:erp / read:*).',
			'2. In Power BI Desktop → Get data → Web → Advanced.',
			'3. URL example: https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv',
			'4. HTTP header: X-API-Key = <tenant key>.',
			'5. Schedule refresh in Power BI Service with the same header.',
		),
	);
}

function epc_power_bi_configure(PDO $pdo, string $siteKey, array $data): array
{
	epc_power_bi_ensure_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_\-]/', '', strtolower($siteKey));
	$mode = (string) ($data['embed_mode'] ?? 'none');
	if (!in_array($mode, array('none', 'url', 'azure'), true)) {
		$mode = 'none';
	}
	$pdo->prepare(
		'INSERT INTO `epc_power_bi_config`
			(`site_key`,`workspace_id`,`azure_tenant_id`,`default_report_id`,`default_dataset_id`,`embed_url`,`embed_mode`,`notes`,`active`)
		 VALUES (?,?,?,?,?,?,?,?,1)
		 ON DUPLICATE KEY UPDATE
			`workspace_id`=VALUES(`workspace_id`),
			`azure_tenant_id`=VALUES(`azure_tenant_id`),
			`default_report_id`=VALUES(`default_report_id`),
			`default_dataset_id`=VALUES(`default_dataset_id`),
			`embed_url`=VALUES(`embed_url`),
			`embed_mode`=VALUES(`embed_mode`),
			`notes`=VALUES(`notes`),
			`active`=1'
	)->execute(array(
		$siteKey,
		substr((string) ($data['workspace_id'] ?? ''), 0, 64),
		substr((string) ($data['azure_tenant_id'] ?? ''), 0, 64),
		substr((string) ($data['default_report_id'] ?? ''), 0, 64),
		substr((string) ($data['default_dataset_id'] ?? ''), 0, 64),
		substr((string) ($data['embed_url'] ?? ''), 0, 512),
		$mode,
		substr((string) ($data['notes'] ?? ''), 0, 512),
	));
	return array('ok' => true, 'site_key' => $siteKey);
}

function epc_power_bi_config_get(PDO $pdo, string $siteKey): ?array
{
	epc_power_bi_ensure_schema($pdo);
	$st = $pdo->prepare('SELECT * FROM `epc_power_bi_config` WHERE `site_key` = ? LIMIT 1');
	$st->execute(array($siteKey));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_power_bi_register_report(PDO $pdo, string $siteKey, array $report): array
{
	epc_power_bi_ensure_schema($pdo);
	$pdo->prepare(
		'INSERT INTO `epc_power_bi_reports`
			(`site_key`,`report_id`,`report_name`,`dataset_id`,`category`,`embed_url`,`active`)
		 VALUES (?,?,?,?,?,?,1)'
	)->execute(array(
		$siteKey,
		substr((string) ($report['report_id'] ?? ''), 0, 64),
		substr((string) ($report['report_name'] ?? 'Report'), 0, 128),
		substr((string) ($report['dataset_id'] ?? ''), 0, 64),
		substr((string) ($report['category'] ?? 'finance'), 0, 32),
		substr((string) ($report['embed_url'] ?? ''), 0, 512),
	));
	return array('ok' => true);
}

function epc_power_bi_reports_list(PDO $pdo, string $siteKey): array
{
	epc_power_bi_ensure_schema($pdo);
	$st = $pdo->prepare(
		'SELECT * FROM `epc_power_bi_reports` WHERE `site_key` = ? AND `active` = 1 ORDER BY `category`, `report_name`'
	);
	$st->execute(array($siteKey));
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/** True when URL is an https Power BI host suitable for iframe embed. */
function epc_power_bi_embed_url_allowed(string $url): bool
{
	$url = trim($url);
	if ($url === '') {
		return false;
	}
	return (bool) preg_match('#^https://([a-z0-9-]+\.)*powerbi\.(com|us)(/|$)#i', $url);
}

/**
 * Resolve an iframe-safe embed URL when mode=url (publish-to-web / shared link).
 * Azure embed tokens are not generated until customer supplies AAD credentials.
 */
function epc_power_bi_embed_resolve(PDO $pdo, string $siteKey, ?int $reportRowId = null): array
{
	epc_power_bi_ensure_schema($pdo);
	$config = epc_power_bi_config_get($pdo, $siteKey);
	if (!$config || empty($config['active'])) {
		$config = epc_power_bi_config_get($pdo, '__platform__');
	}
	if (!$config || empty($config['active'])) {
		return array(
			'ok' => false,
			'error' => 'Power BI not configured. Save workspace/report IDs or an embed URL in CP → Power BI.',
			'phase' => 'config_missing',
		);
	}

	$url = '';
	$mode = (string) ($config['embed_mode'] ?? 'none');

	if ($reportRowId !== null) {
		$st = $pdo->prepare('SELECT * FROM `epc_power_bi_reports` WHERE `id` = ? AND `site_key` = ? AND `active` = 1 LIMIT 1');
		$st->execute(array($reportRowId, $siteKey));
		$rep = $st->fetch(PDO::FETCH_ASSOC);
		if ($rep && !empty($rep['embed_url'])) {
			$url = (string) $rep['embed_url'];
			$mode = 'url';
		}
	}

	if ($url === '' && !empty($config['embed_url'])) {
		$url = (string) $config['embed_url'];
	}

	if ($mode === 'azure') {
		return array(
			'ok' => false,
			'error' => 'Azure embed mode needs customer Azure AD app + Power BI capacity. Store IDs now; token minting ships when credentials are provided.',
			'phase' => 'needs_azure',
			'workspace_id' => (string) ($config['workspace_id'] ?? ''),
			'report_id' => (string) ($config['default_report_id'] ?? ''),
			'azure_tenant_id' => (string) ($config['azure_tenant_id'] ?? ''),
		);
	}

	if ($url === '' || $mode === 'none') {
		return array(
			'ok' => false,
			'error' => 'No embed URL saved. Use Power BI publish-to-web / secure share link, or connect Desktop to /epc-api/v1/powerbi/* datasets.',
			'phase' => 'url_missing',
			'workspace_id' => (string) ($config['workspace_id'] ?? ''),
			'datasets' => epc_power_bi_dataset_catalog(),
		);
	}

	if (!epc_power_bi_embed_url_allowed($url)) {
		return array('ok' => false, 'error' => 'Embed URL must be an https://*.powerbi.com link.', 'phase' => 'url_invalid');
	}

	return array(
		'ok' => true,
		'url' => $url,
		'mode' => 'url',
		'workspace_id' => (string) ($config['workspace_id'] ?? ''),
		'report_id' => (string) ($config['default_report_id'] ?? ''),
	);
}

/**
 * @param list<string> $headers
 * @param list<list<mixed>> $rows
 */
function epc_power_bi_emit_csv(array $headers, array $rows, string $filename = 'powerbi.csv'): void
{
	if (!headers_sent()) {
		header('Content-Type: text/csv; charset=utf-8');
		header('Cache-Control: no-store');
		header('X-ECOM-API-Version: v1');
		header('Content-Disposition: attachment; filename="' . preg_replace('/[^a-zA-Z0-9._-]/', '_', $filename) . '"');
	}
	$out = fopen('php://output', 'w');
	if ($out === false) {
		return;
	}
	fprintf($out, chr(0xEF) . chr(0xBB) . chr(0xBF));
	fputcsv($out, $headers);
	foreach ($rows as $row) {
		fputcsv($out, $row);
	}
	fclose($out);
}

function epc_power_bi_wants_csv(): bool
{
	$fmt = strtolower(trim((string) ($_GET['format'] ?? '')));
	$accept = strtolower((string) ($_SERVER['HTTP_ACCEPT'] ?? ''));
	return $fmt === 'csv' || strpos($accept, 'text/csv') !== false;
}

/**
 * Build KPI table rows from ERP dashboard helper.
 *
 * @return array{headers:list<string>,rows:list<list<mixed>>,meta:array}
 */
function epc_power_bi_dataset_kpis(PDO $tenantPdo, string $siteKey): array
{
	$headers = array('site_key', 'metric', 'value', 'period_from', 'period_to', 'unit');
	$rows = array();
	$meta = array('source' => 'epc_erp_dashboard');

	if (!function_exists('epc_erp_dashboard')) {
		$helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
		if (is_file($helpers)) {
			require_once $helpers;
		}
	}
	if (!function_exists('epc_erp_dashboard')) {
		return array('headers' => $headers, 'rows' => $rows, 'meta' => array('source' => 'unavailable'));
	}

	$dash = epc_erp_dashboard($tenantPdo);
	$from = !empty($dash['date_from']) ? gmdate('Y-m-d', (int) $dash['date_from']) : '';
	$to = !empty($dash['date_to']) ? gmdate('Y-m-d', (int) $dash['date_to']) : '';
	$map = array(
		'order_count' => array((int) ($dash['order_count'] ?? 0), 'count'),
		'revenue_ex_vat' => array(round((float) ($dash['revenue_ex_vat'] ?? 0), 2), 'currency'),
		'profit_ex_vat' => array(round((float) ($dash['profit_ex_vat'] ?? 0), 2), 'currency'),
		'receivable_due_orders' => array(round((float) ($dash['receivable_due_orders'] ?? 0), 2), 'currency'),
		'customer_ledger_balance' => array(round((float) ($dash['customer_ledger_balance'] ?? 0), 2), 'currency'),
		'payable_balance' => array(round((float) ($dash['payable_balance'] ?? 0), 2), 'currency'),
		'cash_bank_total' => array(round((float) ($dash['cash_bank_total'] ?? 0), 2), 'currency'),
		'vat_net_payable' => array(round((float) ($dash['vat_net_payable'] ?? 0), 2), 'currency'),
	);
	foreach ($map as $metric => $pair) {
		$rows[] = array($siteKey, $metric, $pair[0], $from, $to, $pair[1]);
	}
	$meta['period_from'] = $from;
	$meta['period_to'] = $to;
	return array('headers' => $headers, 'rows' => $rows, 'meta' => $meta);
}

/**
 * @return array{headers:list<string>,rows:list<list<mixed>>,meta:array}
 */
function epc_power_bi_dataset_orders(PDO $tenantPdo, string $siteKey, int $limit = 100): array
{
	$limit = min(200, max(1, $limit));
	$headers = array('site_key', 'order_id', 'order_time', 'user_id', 'paid', 'paid_type', 'status_name');
	$rows = array();
	$statusNameSql = function_exists('epc_erp_order_status_name_sql')
		? epc_erp_order_status_name_sql($tenantPdo)
		: '\'\'';
	try {
		$st = $tenantPdo->query(
			'SELECT `id`, `time`, `user_id`, `paid`, `paid_type`, `successfully_created`,
				' . $statusNameSql . ' AS status_name
			 FROM `shop_orders`
			 WHERE `successfully_created` = 1
			 ORDER BY `id` DESC
			 LIMIT ' . (int) $limit
		);
		$raw = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		foreach ($raw as $row) {
			$rows[] = array(
				$siteKey,
				(int) ($row['id'] ?? 0),
				isset($row['time']) ? gmdate('c', (int) $row['time']) : '',
				(int) ($row['user_id'] ?? 0),
				!empty($row['paid']) ? 1 : 0,
				(int) ($row['paid_type'] ?? 0),
				(string) ($row['status_name'] ?? ''),
			);
		}
	} catch (Throwable $e) {
		return array('headers' => $headers, 'rows' => array(), 'meta' => array('error' => 'orders_unavailable'));
	}
	return array('headers' => $headers, 'rows' => $rows, 'meta' => array('limit' => $limit, 'count' => count($rows)));
}

/**
 * @return array{headers:list<string>,rows:list<list<mixed>>,meta:array}
 */
function epc_power_bi_dataset_report(PDO $tenantPdo, string $type, int $dateFrom, int $dateTo): array
{
	$phase8 = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
	if (is_file($phase8)) {
		require_once $phase8;
	}
	if (!function_exists('epc_erp_reports_export')) {
		return array('headers' => array('error'), 'rows' => array(), 'meta' => array('error' => 'export_unavailable'));
	}
	try {
		$export = epc_erp_reports_export($tenantPdo, $type, $dateFrom, $dateTo);
		return array(
			'headers' => array_map('strval', $export['headers'] ?? array()),
			'rows' => $export['rows'] ?? array(),
			'meta' => array(
				'type' => $type,
				'from' => $dateFrom > 0 ? gmdate('Y-m-d', $dateFrom) : '',
				'to' => $dateTo > 0 ? gmdate('Y-m-d', $dateTo) : '',
				'count' => count($export['rows'] ?? array()),
			),
		);
	} catch (Throwable $e) {
		return array('headers' => array('error'), 'rows' => array(), 'meta' => array('error' => $e->getMessage()));
	}
}

/**
 * @return array{headers:list<string>,rows:list<list<mixed>>,meta:array}
 */
function epc_power_bi_dataset_metrics(PDO $pdo, string $siteKey): array
{
	$headers = array('site_key', 'metric_key', 'value', 'previous_value', 'change_pct', 'period_start', 'computed_at');
	$bi = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_bi_metrics.php';
	if (is_file($bi)) {
		require_once $bi;
	}
	if (!function_exists('epc_bi_latest_all')) {
		return array('headers' => $headers, 'rows' => array(), 'meta' => array('error' => 'bi_unavailable'));
	}
	try {
		$latest = epc_bi_latest_all($pdo, $siteKey);
		$rows = array();
		foreach ($latest as $key => $row) {
			$rows[] = array(
				$siteKey,
				(string) $key,
				(float) ($row['value'] ?? 0),
				(float) ($row['previous_value'] ?? 0),
				(float) ($row['change_pct'] ?? 0),
				(string) ($row['period_start'] ?? ''),
				(string) ($row['computed_at'] ?? ''),
			);
		}
		return array('headers' => $headers, 'rows' => $rows, 'meta' => array('count' => count($rows)));
	} catch (Throwable $e) {
		return array('headers' => $headers, 'rows' => array(), 'meta' => array('error' => 'bi_query_failed'));
	}
}

function epc_power_bi_parse_date_param(string $key, ?int $fallbackTs = null): int
{
	$raw = trim((string) ($_GET[$key] ?? ''));
	if ($raw !== '' && preg_match('/^\d{4}-\d{2}-\d{2}$/', $raw)) {
		$ts = strtotime($raw . ' UTC');
		return $ts !== false ? $ts : (int) ($fallbackTs ?? time());
	}
	return (int) ($fallbackTs ?? time());
}

function epc_power_bi_fleet_stats(PDO $pdo): array
{
	epc_power_bi_ensure_schema($pdo);
	$configs = $pdo->query('SELECT `site_key`, `workspace_id`, `embed_mode`, `active` FROM `epc_power_bi_config`')
		->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$reports = $pdo->query('SELECT `site_key`, COUNT(*) AS `count` FROM `epc_power_bi_reports` WHERE `active`=1 GROUP BY `site_key`')
		->fetchAll(PDO::FETCH_KEY_PAIR) ?: array();
	return array(
		'configs' => $configs,
		'reports_per_tenant' => $reports,
		'capabilities' => epc_power_bi_capabilities(),
	);
}
