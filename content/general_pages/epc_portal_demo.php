<?php
/**
 * ECOM AE demo sandbox — provision, expire, manage isolated tenant demos on www.ecomae.com.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_portal.php';
require_once __DIR__ . '/epc_portal_db.php';
require_once __DIR__ . '/epc_portal_tenant.php';

function epc_portal_demo_max_active(): int
{
	return 30;
}

function epc_portal_demo_days(): int
{
	return 3;
}

function epc_portal_demo_industry_presets(): array
{
	return array(
		'auto_parts' => array(
			'industry_code' => 'auto_parts',
			'storefront_package' => 'automotive_spareparts_pro',
			'theme_template' => 'classic',
			'label' => 'Auto spare parts (eParts Cart style)',
			'cp_packs' => array('core', 'commerce', 'auto_parts', 'logistics', 'erp', 'professional', 'marketing'),
		),
		'fashion' => array(
			'industry_code' => 'fashion',
			'storefront_package' => 'fashion_retail_namshi',
			'theme_template' => 'classic',
			'label' => 'Fashion retail (Stylenlook style)',
			'cp_packs' => array('core', 'commerce', 'catalogue', 'erp', 'professional', 'marketing'),
		),
		'erp_only' => array(
			'industry_code' => 'erp_only',
			'registry_industry' => 'erp_standalone',
			'storefront_package' => 'none',
			'theme_template' => 'classic',
			'label' => 'ERP only (no storefront)',
			'cp_packs' => array('core', 'erp', 'professional'),
			'demo_erp_only' => true,
		),
	);
}

/** True for Layla ERP-only sandbox demos (isolated DB, no commerce storefront). */
function epc_portal_demo_row_is_erp_only(?array $row): bool
{
	if (!is_array($row) || empty($row['is_demo'])) {
		return false;
	}
	if (!empty($row['intro_json'])) {
		$intro = json_decode((string) $row['intro_json'], true);
		if (is_array($intro) && !empty($intro['demo_erp_only'])) {
			return true;
		}
	}
	$ind = (string) ($row['industry_code'] ?? '');
	return $ind === 'erp_only';
}

function epc_portal_demo_ensure_schema(PDO $pdo): void
{
	epc_portal_db_ensure($pdo);
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_portal_demo_requests` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`email` VARCHAR(120) NOT NULL,
			`contact_name` VARCHAR(120) NOT NULL DEFAULT \'\',
			`company` VARCHAR(120) NOT NULL DEFAULT \'\',
			`industry_code` VARCHAR(32) NOT NULL DEFAULT \'\',
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`status` VARCHAR(24) NOT NULL DEFAULT \'pending\',
			`ip_address` VARCHAR(45) NOT NULL DEFAULT \'\',
			`contact_phone` VARCHAR(32) NOT NULL DEFAULT \'\',
			`notes` TEXT NULL,
			`created_at` INT NOT NULL DEFAULT 0,
			`provisioned_at` INT NOT NULL DEFAULT 0,
			INDEX `email_created` (`email`, `created_at`),
			INDEX `site_key` (`site_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$reqPhone = $pdo->query("SHOW COLUMNS FROM `epc_portal_demo_requests` LIKE 'contact_phone'")->fetch(PDO::FETCH_ASSOC);
	if (!$reqPhone) {
		$pdo->exec("ALTER TABLE `epc_portal_demo_requests` ADD COLUMN `contact_phone` VARCHAR(32) NOT NULL DEFAULT '' AFTER `company`");
	}
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_portal_demo_db_pool` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`db_name` VARCHAR(32) NOT NULL,
			`db_user` VARCHAR(32) NOT NULL,
			`db_password` VARCHAR(120) NOT NULL,
			`status` VARCHAR(16) NOT NULL DEFAULT \'ready\',
			`claimed_by_site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`created_at` INT NOT NULL DEFAULT 0,
			`claimed_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `db_name` (`db_name`),
			INDEX `status` (`status`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	foreach (array(
		'is_demo' => 'TINYINT(1) NOT NULL DEFAULT 0 AFTER `erp_only_shared`',
		'demo_expires_at' => 'INT NOT NULL DEFAULT 0 AFTER `is_demo`',
		'demo_contact_email' => "VARCHAR(120) NOT NULL DEFAULT '' AFTER `demo_expires_at`",
		'demo_contact_phone' => "VARCHAR(32) NOT NULL DEFAULT '' AFTER `demo_contact_email`",
		'demo_extended_count' => 'INT NOT NULL DEFAULT 0 AFTER `demo_contact_phone`',
	) as $col => $def) {
		$chk = $pdo->query("SHOW COLUMNS FROM `epc_portal_tenants` LIKE " . $pdo->quote($col))->fetch(PDO::FETCH_ASSOC);
		if (!$chk) {
			$pdo->exec('ALTER TABLE `epc_portal_tenants` ADD COLUMN `' . $col . '` ' . $def);
		}
	}
}

function epc_portal_demo_platform_pdo(): ?PDO
{
	return epc_portal_platform_pdo();
}

function epc_portal_demo_tenant_pdo(array $row): ?PDO
{
	$db = trim((string) ($row['db_name'] ?? ''));
	$user = trim((string) ($row['db_user'] ?? ''));
	$pass = (string) ($row['db_password'] ?? '');
	if ($db === '' || $user === '' || $pass === '') {
		return null;
	}
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
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

function epc_portal_demo_count_active(PDO $pdo): int
{
	epc_portal_demo_ensure_schema($pdo);
	$st = $pdo->query(
		'SELECT COUNT(*) FROM `epc_portal_tenants`
		 WHERE `is_demo` = 1 AND `status` IN (\'dns_pending\', \'live\')
		   AND (`demo_expires_at` = 0 OR `demo_expires_at` > ' . (int) time() . ')'
	);
	return (int) $st->fetchColumn();
}

function epc_portal_demo_rate_limited(PDO $pdo, string $email): bool
{
	$email = strtolower(trim($email));
	if ($email === '') {
		return true;
	}
	$since = time() - 86400;
	$st = $pdo->prepare(
		'SELECT COUNT(*) FROM `epc_portal_demo_requests`
		 WHERE `email` = ? AND `status` = \'provisioned\' AND `created_at` > ?'
	);
	$st->execute(array($email, $since));
	return ((int) $st->fetchColumn()) >= 1;
}

function epc_portal_demo_slug(string $text): string
{
	$text = strtolower(trim($text));
	$text = preg_replace('/[^a-z0-9]+/', '_', $text);
	$text = trim($text, '_');
	if ($text === '') {
		$text = 'demo';
	}
	return substr($text, 0, 24);
}

function epc_portal_demo_generate_site_key(PDO $pdo, string $company, string $industry): string
{
	$indAbbr = array('auto_parts' => 'ap', 'fashion' => 'fs', 'erp_only' => 'eo');
	$suffix = $indAbbr[$industry] ?? substr($industry, 0, 2);
	$base = 'demo_' . date('ymd') . '_' . $suffix;
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($base));
	$try = $key;
	$n = 0;
	while (true) {
		$st = $pdo->prepare('SELECT COUNT(*) FROM `epc_portal_tenants` WHERE `site_key` = ?');
		$st->execute(array($try));
		if ((int) $st->fetchColumn() === 0) {
			return $try;
		}
		$n++;
		$try = $key . '_' . $n;
	}
}

/** CloudPanel MySQL database names must stay short (≈16 chars max on this host). */
function epc_portal_demo_db_name(string $siteKey): string
{
	$hash = substr(md5($siteKey), 0, 8);
	$name = 'dm' . $hash;
	return substr(preg_replace('/[^a-z0-9_]/', '', strtolower($name)), 0, 16);
}

function epc_portal_demo_normalize_phone(string $phone): string
{
	$phone = trim($phone);
	if ($phone === '') {
		return '';
	}
	$plus = (strpos($phone, '+') === 0);
	$digits = preg_replace('/\D+/', '', $phone);
	if ($digits === null || $digits === '') {
		return '';
	}
	return ($plus ? '+' : '') . $digits;
}

function epc_portal_demo_phone_valid(string $phone): bool
{
	$digits = preg_replace('/\D+/', '', $phone);
	return is_string($digits) && strlen($digits) >= 7 && strlen($digits) <= 15;
}

/** Strip invalid UTF-8 / huge shell blobs so provision APIs always return JSON. */
function epc_portal_demo_sanitize_for_json($value, int $depth = 0)
{
	if ($depth > 8) {
		return '…';
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = epc_portal_demo_sanitize_for_json($v, $depth + 1);
		}
		return $out;
	}
	if (!is_string($value)) {
		return $value;
	}
	if (!mb_check_encoding($value, 'UTF-8')) {
		$value = mb_convert_encoding($value, 'UTF-8', 'UTF-8');
	}
	if (strlen($value) > 2000) {
		return substr($value, 0, 2000) . '…';
	}
	return $value;
}

function epc_portal_demo_json_out(array $result, int $httpCode = 200): void
{
	$result = epc_portal_demo_sanitize_for_json($result);
	http_response_code($httpCode);
	$json = json_encode($result, JSON_UNESCAPED_UNICODE | (defined('JSON_INVALID_UTF8_SUBSTITUTE') ? JSON_INVALID_UTF8_SUBSTITUTE : 0));
	if ($json === false) {
		$json = json_encode(array(
			'ok' => !empty($result['ok']),
			'message' => (string) ($result['message'] ?? 'Provision response encoding failed'),
		), JSON_UNESCAPED_UNICODE);
	}
	echo $json;
}

function epc_portal_demo_pool_db_name(): string
{
	return 'dp' . substr(bin2hex(random_bytes(4)), 0, 6);
}

function epc_portal_demo_pool_claim(PDO $pdo, string $siteKey): ?array
{
	epc_portal_demo_ensure_schema($pdo);
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	$st = $pdo->query(
		"SELECT * FROM `epc_portal_demo_db_pool` WHERE `status` = 'ready' ORDER BY `id` ASC LIMIT 1"
	);
	$row = $st ? $st->fetch(PDO::FETCH_ASSOC) : false;
	if (!$row) {
		return null;
	}
	$dbName = (string) ($row['db_name'] ?? '');
	$dbUser = (string) ($row['db_user'] ?? '');
	$dbPass = (string) ($row['db_password'] ?? '');
	if ($dbName === '' || !epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
		$pdo->prepare('UPDATE `epc_portal_demo_db_pool` SET `status` = ? WHERE `id` = ?')->execute(array('broken', (int) $row['id']));
		return null;
	}
	$pdo->prepare(
		'UPDATE `epc_portal_demo_db_pool` SET `status` = ?, `claimed_by_site_key` = ?, `claimed_at` = ? WHERE `id` = ? AND `status` = ?'
	)->execute(array('claimed', $key, time(), (int) $row['id'], 'ready'));
	return array('db_name' => $dbName, 'db_user' => $dbUser, 'db_password' => $dbPass, 'pool_id' => (int) $row['id']);
}

function epc_portal_demo_pool_register(PDO $pdo, string $dbName, string $dbUser, string $dbPass): array
{
	epc_portal_demo_ensure_schema($pdo);
	if (!epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
		return array('ok' => false, 'message' => 'Database not reachable: ' . $dbName);
	}
	$st = $pdo->prepare(
		'INSERT INTO `epc_portal_demo_db_pool` (`db_name`, `db_user`, `db_password`, `status`, `created_at`)
		 VALUES (?, ?, ?, \'ready\', ?)
		 ON DUPLICATE KEY UPDATE
		 `db_user` = VALUES(`db_user`), `db_password` = VALUES(`db_password`),
		 `status` = IF(`status` = \'claimed\', `status`, \'ready\'), `created_at` = VALUES(`created_at`)'
	);
	$st->execute(array($dbName, $dbUser, $dbPass, time()));
	return array('ok' => true, 'db_name' => $dbName);
}

function epc_portal_demo_pool_seed(PDO $pdo, int $count = 3): array
{
	$count = max(1, min(10, $count));
	$created = array();
	$errors = array();
	for ($i = 0; $i < $count; $i++) {
		$dbName = epc_portal_demo_pool_db_name();
		$dbUser = $dbName;
		$dbPass = epc_portal_demo_temp_password() . bin2hex(random_bytes(4));
		$prov = epc_portal_demo_provision_via_clp_web($dbName, $dbUser, $dbPass);
		if (empty($prov['ok'])) {
			$prov = epc_portal_demo_provision_database_raw($dbName, $dbUser, $dbPass);
		}
		if (empty($prov['ok'])) {
			$errors[] = $dbName . ': ' . epc_portal_demo_provision_failure_hint($prov['log'] ?? array());
			break;
		}
		$reg = epc_portal_demo_pool_register($pdo, $dbName, $dbUser, $dbPass);
		if (!empty($reg['ok'])) {
			$created[] = $dbName;
		}
	}
	return array(
		'ok' => count($created) > 0,
		'created' => $created,
		'errors' => $errors,
		'ready_count' => (int) $pdo->query("SELECT COUNT(*) FROM `epc_portal_demo_db_pool` WHERE `status` = 'ready'")->fetchColumn(),
	);
}

/** CloudPanel admin password for demo DB creation (env or config.demo-clp.php). */
function epc_portal_demo_clp_password(): string
{
	static $resolved = null;
	if ($resolved !== null) {
		return $resolved;
	}
	$clpPass = '';
	if (!empty($GLOBALS['epc_demo_clp_pass'])) {
		$clpPass = (string) $GLOBALS['epc_demo_clp_pass'];
	}
	if ($clpPass === '') {
		$env = getenv('EPC_DEMO_CLP_PASS');
		if ($env !== false && $env !== '') {
			$clpPass = (string) $env;
		}
	}
	if ($clpPass === '') {
		$env = getenv('EPC_CLP_PASS');
		if ($env !== false && $env !== '') {
			$clpPass = (string) $env;
		}
	}
	if ($clpPass === '') {
		$env = getenv('CLP_PASS');
		if ($env !== false && $env !== '') {
			$clpPass = (string) $env;
		}
	}
	if ($clpPass === '' && is_file($_SERVER['DOCUMENT_ROOT'] . '/config.demo-clp.php')) {
		$epc_demo_clp_pass = null;
		include $_SERVER['DOCUMENT_ROOT'] . '/config.demo-clp.php';
		if (!empty($epc_demo_clp_pass)) {
			$clpPass = (string) $epc_demo_clp_pass;
		}
	}
	$resolved = $clpPass;
	return $resolved;
}

function epc_portal_demo_pool_ready_count(PDO $pdo): int
{
	epc_portal_demo_ensure_schema($pdo);
	return (int) $pdo->query("SELECT COUNT(*) FROM `epc_portal_demo_db_pool` WHERE `status` = 'ready'")->fetchColumn();
}

/** Top up pre-provisioned DB pool when slots run low (CloudPanel web UI). */
function epc_portal_demo_pool_replenish(PDO $pdo, int $target = 3): array
{
	$ready = epc_portal_demo_pool_ready_count($pdo);
	if ($ready >= $target) {
		return array('ok' => true, 'skipped' => true, 'ready_count' => $ready, 'created' => array());
	}
	$created = array();
	$errors = array();
	for ($i = $ready; $i < $target; $i++) {
		$dbName = epc_portal_demo_pool_db_name();
		$dbUser = $dbName;
		$dbPass = epc_portal_demo_temp_password() . bin2hex(random_bytes(4));
		$prov = epc_portal_demo_provision_via_clp_web($dbName, $dbUser, $dbPass);
		if (empty($prov['ok'])) {
			$errors[] = $dbName . ': ' . epc_portal_demo_provision_failure_hint($prov['log'] ?? array());
			break;
		}
		$reg = epc_portal_demo_pool_register($pdo, $dbName, $dbUser, $dbPass);
		if (!empty($reg['ok'])) {
			$created[] = $dbName;
		}
	}
	return array(
		'ok' => count($created) > 0 || $ready > 0,
		'created' => $created,
		'errors' => $errors,
		'ready_count' => epc_portal_demo_pool_ready_count($pdo),
	);
}

function epc_portal_demo_provision_failure_hint(array $log): string
{
	$text = strtolower(implode("\n", array_map('strval', $log)));
	if (strpos($text, 'cloudpanel password not configured') !== false) {
		return 'CloudPanel credentials missing on server (set EPC_DEMO_CLP_PASS or config.demo-clp.php)';
	}
	if (strpos($text, 'cloudpanel web login failed') !== false) {
		return 'CloudPanel web login failed — verify EPC_DEMO_CLP_PASS / config.demo-clp.php';
	}
	if (strpos($text, 'db form not found') !== false) {
		return 'CloudPanel database form unavailable — check panel at :8443';
	}
	if (strpos($text, 'access denied') !== false || strpos($text, 'privilege') !== false) {
		return 'MySQL user lacks CREATE DATABASE privilege (use CloudPanel web UI or pre-provisioned pool)';
	}
	if (strpos($text, 'command "db:add" is not defined') !== false || strpos($text, 'db:add') !== false) {
		return 'CloudPanel db:add CLI not available on this host (web UI path required)';
	}
	if (strpos($text, 'clpctl not available') !== false) {
		return 'clpctl not available on host';
	}
	return 'All DB creation methods failed';
}

function epc_portal_demo_cp_path_prefix(): string
{
	return '/cp/demo/';
}

/** Cookie that pins demo CP navigation to /cp/demo/{site_key}/ after autologin. */
function epc_portal_demo_cp_scope_cookie_name(): string
{
	return 'epc_demo_cp_site_key';
}

function epc_portal_demo_cp_set_scope_cookie(string $siteKey): void
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return;
	}
	$secure = !empty($_SERVER['HTTPS']);
	setcookie(epc_portal_demo_cp_scope_cookie_name(), $key, 0, '/', '', $secure, true);
	$_COOKIE[epc_portal_demo_cp_scope_cookie_name()] = $key;
}

function epc_portal_demo_cp_clear_scope_cookie(): void
{
	$name = epc_portal_demo_cp_scope_cookie_name();
	$secure = !empty($_SERVER['HTTPS']);
	setcookie($name, '', time() - 3600, '/', '', $secure, true);
	unset($_COOKIE[$name]);
}

function epc_portal_demo_cp_scope_from_cookie(): string
{
	$name = epc_portal_demo_cp_scope_cookie_name();
	return isset($_COOKIE[$name]) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_COOKIE[$name])) : '';
}

/**
 * Rewrite bare /cp/… navigation paths to tenant-scoped /cp/demo/{site_key}/…
 * (leaves /cp/templates/, /cp/content/, etc. unchanged).
 */
function epc_portal_demo_cp_scope_cp_path(string $path, ?string $siteKey = null): string
{
	if ($path === '' || $path[0] !== '/') {
		return $path;
	}
	if ($siteKey === null || $siteKey === '') {
		$siteKey = epc_portal_demo_cp_site_key();
	}
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $siteKey));
	if ($key === '') {
		return $path;
	}
	$scope = rtrim(epc_portal_demo_cp_tenant_base($key), '/');
	$lower = strtolower($path);
	foreach (array('/cp/templates/', '/cp/content/', '/cp/plugins/', '/cp/modules/', '/cp/lib/', '/cp/js/') as $assetPrefix) {
		if (strpos($lower, $assetPrefix) === 0) {
			return $path;
		}
	}
	if ($lower === strtolower($scope) || strpos($lower, strtolower($scope) . '/') === 0) {
		return $path;
	}
	if (preg_match('#^/cp/demo/[^/]+(?:/|$)#', $path)) {
		return $path;
	}
	$demoStoreCp = '/demo/' . $key . '/cp/';
	if (stripos($path, $demoStoreCp) === 0) {
		return $scope . '/' . ltrim(substr($path, strlen($demoStoreCp)), '/');
	}
	if (stripos($path, '/cp/') === 0) {
		return $scope . substr($path, strlen('/cp'));
	}
	return $path;
}

/** Redirect stray /cp/… hits back into demo scope when scope cookie is set. */
function epc_portal_demo_cp_maybe_redirect_bare_path(): void
{
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return;
	}
	$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return;
	}
	if (strpos($path, '/cp/demo/') === 0) {
		return;
	}
	foreach (array('/cp/templates/', '/cp/content/', '/cp/plugins/', '/cp/modules/', '/cp/lib/', '/cp/js/') as $assetPrefix) {
		if (strpos($path, $assetPrefix) === 0) {
			return;
		}
	}
	if ($path !== '/cp' && strpos($path, '/cp/') !== 0) {
		return;
	}
	$key = epc_portal_demo_cp_scope_from_cookie();
	if ($key === '') {
		return;
	}
	$row = epc_portal_demo_load_live_row($key);
	if ($row === null) {
		epc_portal_demo_cp_clear_scope_cookie();
		return;
	}
	$scoped = epc_portal_demo_cp_scope_cp_path($path, $key);
	if ($scoped === $path || headers_sent()) {
		return;
	}
	$query = parse_url($uri, PHP_URL_QUERY);
	header('Location: ' . $scoped . ($query !== null && $query !== '' ? '?' . $query : ''), true, 302);
	exit;
}

function epc_portal_demo_cp_rewrite_nav_urls_attr_cb(array $m)
{
	$path = isset($m[3]) ? (string)$m[3] : '';
	return $m[1] . '=' . $m[2] . epc_portal_demo_cp_scope_cp_path($path);
}

function epc_portal_demo_cp_rewrite_nav_urls_location_cb(array $m)
{
	$path = isset($m[2]) ? (string)$m[2] : '';
	return 'location=' . $m[1] . epc_portal_demo_cp_scope_cp_path($path);
}

/** Rewrite CP HTML/JS navigation URLs so sidebar clicks stay in demo scope. */
function epc_portal_demo_cp_rewrite_nav_urls(string $html): string
{
	if (!epc_portal_demo_is_cp_context()) {
		return $html;
	}
	$key = epc_portal_demo_cp_site_key();
	if ($key === '') {
		return $html;
	}
	$scope = epc_portal_demo_cp_tenant_base($key);
	$scopedAbs = 'https://www.ecomae.com' . $scope;
	$html = str_replace('https://www.ecomae.com/demo/' . $key . '/cp/', $scopedAbs, $html);
	$rewritten = preg_replace_callback(
		'#\b(href|action)\s*=\s*(["\'])(/[^"\']*)#i',
		'epc_portal_demo_cp_rewrite_nav_urls_attr_cb',
		$html
	);
	if (is_string($rewritten)) {
		$html = $rewritten;
	}
	$rewritten = preg_replace_callback(
		'#\blocation\s*=\s*(["\'])(/[^"\']*)#i',
		'epc_portal_demo_cp_rewrite_nav_urls_location_cb',
		$html
	);
	if (is_string($rewritten)) {
		$html = $rewritten;
	}
	return $html;
}

function epc_portal_demo_is_cp_context(): bool
{
	return !empty($GLOBALS['epc_demo_cp_context']);
}

/** Auto-parts commerce demo (storefront or CP) — mirror epartscart.com, not ECOM AE sandbox chrome. */
function epc_portal_demo_is_autoparts_parity(): bool
{
	if (epc_portal_demo_is_cp_context()) {
		if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()) {
			return false;
		}
		$row = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
		return is_array($row) && (string) ($row['industry_code'] ?? '') === 'auto_parts';
	}
	if (!empty($GLOBALS['epc_demo_storefront_context'])) {
		$row = $GLOBALS['epc_demo_storefront_tenant_row'] ?? null;
		if (is_array($row) && (string) ($row['industry_code'] ?? '') === 'auto_parts') {
			return true;
		}
		return (string) ($GLOBALS['epc_demo_storefront_industry'] ?? '') === 'auto_parts';
	}
	return false;
}

/** Docpart tables cloned read-only into auto-parts demo tenant DBs (content, modules, lang, shop). */
function epc_portal_demo_docpart_seed_tables(): array
{
	if (function_exists('epc_demo_autoparts_bootstrap_clone_tables')) {
		return epc_demo_autoparts_bootstrap_clone_tables();
	}
	return array(
		'lang_languages', 'lang_text_strings', 'lang_text_strings_translation',
		'groups', 'users_groups_bind', 'shop_offices', 'shop_geo', 'shop_offices_geo_map',
		'templates', 'content', 'modules', 'plugins',
		'menu',
		'shop_storages', 'shop_storages_data',
		'shop_catalogue_categories',
	);
}

/**
 * Re-sync header chrome from docpart (Dubai geo, hours, menu, catalog) — see epc_demo_autoparts_bootstrap.php.
 */
function epc_portal_demo_repair_header_parity(PDO $tenantPdo, bool $force = false): array
{
	require_once dirname(__DIR__, 2) . '/epc_demo_autoparts_bootstrap.php';
	return epc_demo_autoparts_bootstrap_apply($tenantPdo, $force);
}

/** Layla auto_parts provision: clone full epartscart header theme from docpart (read-only). */
function epc_portal_demo_apply_autoparts_theme_bootstrap(PDO $tenantPdo, bool $force = true): array
{
	require_once dirname(__DIR__, 2) . '/epc_demo_autoparts_bootstrap.php';
	return epc_demo_autoparts_bootstrap_apply($tenantPdo, $force);
}

/** CP sidebar tables cloned read-only from docpart when demo tenant menu is empty. */
function epc_portal_demo_cp_menu_tables(): array
{
	return array(
		'control_groups',
		'control_items',
		'plugins',
	);
}

/**
 * Clone commerce CP menu + backend plugins from docpart when tenant control_items is sparse.
 */
function epc_portal_demo_repair_cp_menu(PDO $tenantPdo, bool $force = false): array
{
	$out = array('ok' => true, 'force' => $force, 'cloned' => array(), 'counts' => array());
	try {
		$itemCnt = (int) $tenantPdo->query('SELECT COUNT(*) FROM `control_items`')->fetchColumn();
		$groupCnt = (int) $tenantPdo->query('SELECT COUNT(*) FROM `control_groups`')->fetchColumn();
	} catch (Exception $e) {
		return array('ok' => false, 'message' => 'CP menu tables missing: ' . $e->getMessage());
	}
	$out['counts']['before'] = array('control_items' => $itemCnt, 'control_groups' => $groupCnt);
	if (!$force && $itemCnt >= 40 && $groupCnt >= 4) {
		$out['message'] = 'CP menu already populated';
		$out['counts']['after'] = $out['counts']['before'];
		return $out;
	}
	try {
		require_once __DIR__ . '/epc_portal_tenant.php';
		$srcCreds = epc_portal_resolve_tenant_db_credentials();
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		$docPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $srcCreds['db'] . ';charset=utf8',
			$srcCreds['user'],
			$srcCreds['password'],
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		return array('ok' => false, 'message' => 'Storefront database connect failed: ' . $e->getMessage());
	}
	$clone = epc_portal_demo_php_clone_tables($docPdo, $tenantPdo, epc_portal_demo_cp_menu_tables(), false);
	$out['cloned'] = $clone;
	try {
		$out['counts']['after'] = array(
			'control_items' => (int) $tenantPdo->query('SELECT COUNT(*) FROM `control_items`')->fetchColumn(),
			'control_groups' => (int) $tenantPdo->query('SELECT COUNT(*) FROM `control_groups`')->fetchColumn(),
			'plugins' => (int) $tenantPdo->query('SELECT COUNT(*) FROM `plugins` WHERE `is_frontend` = 0')->fetchColumn(),
		);
	} catch (Exception $e) {
		$out['counts']['after'] = array();
	}
	$out['ok'] = !empty($clone['ok']) && ($out['counts']['after']['control_items'] ?? 0) > 0;
	$out['message'] = $out['ok'] ? 'CP menu cloned from docpart' : 'CP menu clone incomplete';
	return $out;
}

/**
 * Push commerce CP packs + industry into demo tenant DB (never skips is_demo tenants).
 */
function epc_portal_demo_sync_cp_packs(PDO $platformPdo, string $siteKey, ?array $preset = null): array
{
	require_once __DIR__ . '/epc_portal_tenant_intro.php';
	require_once __DIR__ . '/epc_portal_cp_menu.php';
	$row = epc_portal_tenant_get($platformPdo, $siteKey);
	if ($row === null) {
		return array('ok' => false, 'message' => 'Tenant not found');
	}
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$tenantPdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Tenant DB connect failed');
	}
	$industry = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['industry_code'] ?? 'auto_parts')));
	if ($preset === null) {
		$presets = epc_portal_demo_industry_presets();
		$preset = $presets[$industry] ?? $presets['auto_parts'];
	}
	$siteKeySafe = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	$demoUrl = 'https://www.ecomae.com/demo/' . $siteKeySafe . '/';
	$settings = null;
	if ($industry === 'auto_parts' && function_exists('epc_portal_demo_epartscart_theme_source')) {
		$settings = epc_portal_demo_epartscart_theme_source();
	}
	if (!is_array($settings) || $settings === array()) {
		$settings = epc_portal_default_site_settings('www.epartscart.com');
	}
	$settings['host'] = 'www.ecomae.com';
	$settings['industry_code'] = (string) ($preset['industry_code'] ?? $industry);
	$settings['access_mode'] = epc_portal_demo_row_is_erp_only($row) ? 'erp_only' : 'full';
	$settings['enabled_packs'] = array_values(array_unique(array_filter(
		(array) ($preset['cp_packs'] ?? array('core', 'commerce', 'auto_parts')),
		function ($p) {
			return $p !== 'super_platform';
		}
	)));
	$settings['domain_path'] = $demoUrl;
	if ($industry === 'auto_parts') {
		$settings['system_name'] = (string) ($settings['system_name'] ?? 'eParts Cart');
		$settings['hub_name'] = (string) ($settings['hub_name'] ?? 'Electronic World Group');
		if (!empty($settings['cp_menu']) && is_array($settings['cp_menu'])) {
			// keep epartscart menu policy
		}
	} else {
		$trade = (string) ($row['trade_name'] ?? 'Demo');
		$settings['system_name'] = $trade . ' Demo';
		$settings['hub_name'] = $trade;
	}
	epc_portal_save_site_settings($tenantPdo, $settings);
	$menu = epc_portal_demo_repair_cp_menu($tenantPdo, false);
	return array(
		'ok' => true,
		'message' => 'Demo CP packs synced',
		'db' => (string) ($row['db_name'] ?? ''),
		'industry_code' => $settings['industry_code'],
		'enabled_packs' => $settings['enabled_packs'],
		'menu' => $menu,
	);
}

/** True when URI (before rewrite) is /cp/demo/{site_key}/… */
function epc_portal_demo_is_cp_request(?string $uri = null): bool
{
	return epc_portal_demo_parse_cp_path($uri) !== null;
}

function epc_portal_demo_cp_original_uri(): string
{
	return (string) ($GLOBALS['epc_demo_cp_original_uri'] ?? '');
}

/** Platform operator session in ecomae registry — ignores demo CP context guard. */
function epc_portal_platform_operator_session_active(): bool
{
	$shouldCheck = (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host())
		|| (function_exists('epc_portal_demo_is_cp_request') && epc_portal_demo_is_cp_request());
	if (!$shouldCheck) {
		return false;
	}
	$sharedErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedErpFile)) {
		require_once $sharedErpFile;
		if (function_exists('epc_portal_is_shared_erp_cp_session') && epc_portal_is_shared_erp_cp_session()) {
			return false;
		}
		if (function_exists('epc_portal_shared_erp_infer_tenant_from_session')) {
			if (epc_portal_shared_erp_infer_tenant_from_session() !== null) {
				return false;
			}
		}
	}
	$session = isset($_COOKIE['admin_session']) ? (string) $_COOKIE['admin_session'] : '';
	$userId = isset($_COOKIE['admin_u_id']) ? (int) $_COOKIE['admin_u_id'] : 0;
	if ($session === '' || $userId <= 0) {
		return false;
	}
	$pdo = function_exists('epc_portal_platform_operator_pdo') ? epc_portal_platform_operator_pdo() : null;
	if (!$pdo instanceof PDO) {
		return false;
	}
	try {
		$st = $pdo->prepare('SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?');
		$st->execute(array($session, $userId));
		return ((int) $st->fetchColumn()) === 1;
	} catch (Exception $e) {
		return false;
	}
}

function epc_portal_demo_clear_cp_login_cookies(): void
{
	$secure = !empty($_SERVER['HTTPS']);
	setcookie('admin_session', '', time() - 3600, '/', '', $secure, true);
	setcookie('admin_u_id', '', time() - 3600, '/', '', $secure, true);
	unset($_COOKIE['admin_session'], $_COOKIE['admin_u_id']);
}

function epc_portal_demo_reapply_cp_config($DP_Config): void
{
	if (!is_object($DP_Config) || empty($GLOBALS['epc_demo_cp_context'])) {
		return;
	}
	$row = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
	$key = (string) ($GLOBALS['epc_demo_cp_site_key'] ?? '');
	if (!is_array($row) || $key === '') {
		return;
	}
	epc_portal_demo_apply_cp_config($DP_Config, $row, $key);
}

function epc_portal_demo_cp_site_key(): string
{
	return (string) ($GLOBALS['epc_demo_cp_site_key'] ?? '');
}

/** True when current request is an ERP-only demo CP sandbox (not commerce demo). */
function epc_portal_demo_cp_is_erp_only(): bool
{
	if (!epc_portal_demo_is_cp_context()) {
		return false;
	}
	$row = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
	return epc_portal_demo_row_is_erp_only(is_array($row) ? $row : null);
}

/** Tenant-scoped demo CP base path `/cp/demo/{site_key}/` (trailing slash). */
function epc_portal_demo_cp_tenant_base(string $siteKey = ''): string
{
	if ($siteKey === '' && function_exists('epc_portal_demo_cp_site_key')) {
		$siteKey = epc_portal_demo_cp_site_key();
	}
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	return epc_portal_demo_cp_path_prefix() . $key . '/';
}

/** ERP professional shell URL for ERP-only demo CP (keeps /cp/demo/{key}/ prefix). */
function epc_portal_demo_erp_shell_url(string $siteKey): string
{
	return epc_portal_demo_cp_tenant_base($siteKey) . 'shop/finance/erp?epc_erp_shell=1';
}

/** ERP module base URL under demo CP (no query string). */
function epc_portal_demo_erp_module_url(string $siteKey = ''): string
{
	return epc_portal_demo_cp_tenant_base($siteKey) . 'shop/finance/erp';
}

function epc_portal_demo_cp_login_url(string $siteKey): string
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	return epc_portal_demo_cp_path_prefix() . $key . '/';
}

function epc_portal_demo_row_is_demo(?array $row): bool
{
	return is_array($row) && !empty($row['is_demo']);
}

function epc_portal_demo_load_live_row(string $siteKey, ?PDO $platformPdo = null): ?array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return null;
	}
	if ($platformPdo === null) {
		$platformPdo = epc_portal_platform_pdo();
	}
	if (!$platformPdo instanceof PDO) {
		return null;
	}
	epc_portal_demo_ensure_schema($platformPdo);
	$st = $platformPdo->prepare(
		'SELECT * FROM `epc_portal_tenants`
		 WHERE `site_key` = ? AND `is_demo` = 1 AND `status` = \'live\' LIMIT 1'
	);
	$st->execute(array($key));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	require_once __DIR__ . '/epc_portal_tenant_control.php';
	if (!epc_portal_tenant_control_row_is_active($row)) {
		return null;
	}
	$exp = (int) ($row['demo_expires_at'] ?? 0);
	if ($exp > 0 && $exp < time()) {
		return null;
	}
	return $row;
}

function epc_portal_demo_urls(string $siteKey, ?array $row = null): array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	$base = 'https://www.ecomae.com';
	$cpPath = epc_portal_demo_cp_path_prefix() . $key . '/';
	$out = array(
		'storefront' => $base . '/demo/' . $key . '/en/',
		'cp' => $base . $cpPath,
		'client_erp_legacy' => $base . '/cp/client-erp/' . $key . '/',
	);
	if ($row === null) {
		$pdo = epc_portal_platform_pdo();
		if ($pdo instanceof PDO) {
			$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
			$st->execute(array($key));
			$row = $st->fetch(PDO::FETCH_ASSOC) ?: null;
		}
	}
	if (is_array($row) && epc_portal_demo_row_is_erp_only($row)) {
		$out['storefront'] = '';
		$out['storefront_disabled'] = $base . '/demo/' . $key . '/en/';
		$out['cp'] = $base . epc_portal_demo_erp_shell_url($key);
	}
	return $out;
}

function epc_portal_demo_db_exists(string $dbName, string $dbUser, string $dbPass): bool
{
	$candidates = array($dbName, 'ecomae_' . $dbName, 'demo_' . $dbName);
	foreach (array_unique($candidates) as $tryDb) {
		try {
			$pdo = new PDO('mysql:host=127.0.0.1;dbname=' . $tryDb . ';charset=utf8', $dbUser, $dbPass);
			if (((int) $pdo->query('SHOW TABLES')->rowCount()) >= 0) {
				return true;
			}
		} catch (Exception $e) {
		}
		try {
			$pdo = new PDO('mysql:host=127.0.0.1;charset=utf8', $dbUser, $dbPass);
			$st = $pdo->query("SHOW DATABASES LIKE " . $pdo->quote($tryDb));
			if ($st && $st->fetch()) {
				return true;
			}
		} catch (Exception $e) {
		}
	}
	return false;
}

function epc_portal_demo_resolve_db_names(string $dbName): array
{
	$names = array($dbName);
	if (strpos($dbName, 'ecomae_') !== 0) {
		$names[] = 'ecomae_' . $dbName;
	}
	return array_values(array_unique($names));
}

function epc_portal_demo_provision_database(string $dbName, string $dbUser, string $dbPass): array
{
	return epc_portal_demo_provision_database_raw($dbName, $dbUser, $dbPass);
}

/** Primary path on Hostinger CloudPanel: web UI (db:add CLI is often missing). */
function epc_portal_demo_provision_via_clp_web(string $dbName, string $dbUser, string $dbPass, array $log = array()): array
{
	if (epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
		return array('ok' => true, 'log' => array_merge($log, array("DB {$dbName} already exists")));
	}
	$clpPass = epc_portal_demo_clp_password();
	if ($clpPass === '') {
		$log[] = 'CloudPanel password not configured (EPC_DEMO_CLP_PASS / config.demo-clp.php)';
		return array(
			'ok' => false,
			'log' => $log,
			'hint' => epc_portal_demo_provision_failure_hint($log),
		);
	}
	require_once __DIR__ . '/epc_cloudpanel_helpers.php';
	$cookie = '';
	$login = epc_clp_web_login('admin', $clpPass, $cookie);
	if (empty($login['ok'])) {
		$log[] = 'CloudPanel web login failed';
		if (!empty($login['detail']) && is_array($login['detail'])) {
			$log[] = json_encode($login['detail']);
		}
		return array('ok' => false, 'log' => $log, 'hint' => epc_portal_demo_provision_failure_hint($log));
	}
	$webDb = epc_clp_web_add_database($cookie, 'www.ecomae.com', $dbName, $dbUser, $dbPass);
	$log = array_merge($log, $webDb['log'] ?? array());
	if (!empty($webDb['response']) && stripos((string) $webDb['response'], 'Redirecting') !== false) {
		$log[] = 'CloudPanel redirect OK';
	}
	for ($wait = 0; $wait < 10; $wait++) {
		if ($wait > 0) {
			sleep(2);
		}
		if (epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
			$log[] = 'provisioned via CloudPanel web UI (wait ' . ($wait * 2) . 's)';
			return array('ok' => true, 'log' => $log);
		}
		foreach (epc_portal_demo_resolve_db_names($dbName) as $altDb) {
			if ($altDb === $dbName) {
				continue;
			}
			if (epc_portal_demo_db_exists($altDb, $dbUser, $dbPass)) {
				$log[] = "found CloudPanel DB as {$altDb}";
				return array('ok' => true, 'log' => $log, 'db_name' => $altDb);
			}
		}
	}
	$log[] = 'CloudPanel web DB create timed out waiting for MySQL';
	return array('ok' => false, 'log' => $log, 'hint' => epc_portal_demo_provision_failure_hint($log));
}

function epc_portal_demo_provision_database_raw(string $dbName, string $dbUser, string $dbPass): array
{
	if (epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
		return array('ok' => true, 'log' => array("DB {$dbName} already exists"));
	}
	$web = epc_portal_demo_provision_via_clp_web($dbName, $dbUser, $dbPass);
	if (!empty($web['ok'])) {
		return $web;
	}
	$log = $web['log'] ?? array();
	require_once __DIR__ . '/epc_cloudpanel_helpers.php';
	$prov = epc_clp_provision_database(array(
		'domain' => 'www.ecomae.com',
		'database_name' => $dbName,
		'database_user' => $dbUser,
		'database_password' => $dbPass,
	));
	if (!empty($prov['ok'])) {
		return $prov;
	}
	$log = array_merge($log, $prov['log'] ?? array());
	$dbEsc = str_replace('`', '', $dbName);
	$userEsc = str_replace("'", "''", $dbUser);
	$passEsc = str_replace("'", "''", $dbPass);
	$sql = "CREATE DATABASE IF NOT EXISTS `{$dbEsc}` CHARACTER SET utf8 COLLATE utf8_general_ci;"
		. " CREATE USER IF NOT EXISTS '{$userEsc}'@'localhost' IDENTIFIED BY '{$passEsc}';"
		. " GRANT ALL PRIVILEGES ON `{$dbEsc}`.* TO '{$userEsc}'@'localhost';"
		. " FLUSH PRIVILEGES;";
	foreach (array(
		'mysql -e ' . escapeshellarg($sql),
		'sudo mysql -e ' . escapeshellarg($sql),
		'mariadb -e ' . escapeshellarg($sql),
	) as $cmd) {
		$out = epc_clp_run_cmd($cmd);
		$log[] = $cmd;
		$log[] = $out['output'];
		if ($out['code'] === 0 && epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
			return array('ok' => true, 'log' => $log);
		}
	}
	try {
		$platformPdo = epc_portal_platform_pdo();
		if ($platformPdo instanceof PDO) {
			$platformPdo->exec("CREATE DATABASE IF NOT EXISTS `{$dbEsc}` CHARACTER SET utf8 COLLATE utf8_general_ci");
			try {
				$platformPdo->exec("CREATE USER IF NOT EXISTS '{$userEsc}'@'localhost' IDENTIFIED BY '{$passEsc}'");
			} catch (Exception $e) {
				try {
					$platformPdo->exec("CREATE USER '{$userEsc}'@'localhost' IDENTIFIED BY '{$passEsc}'");
				} catch (Exception $e2) {
				}
			}
			$platformPdo->exec("GRANT ALL PRIVILEGES ON `{$dbEsc}`.* TO '{$userEsc}'@'localhost'");
			$platformPdo->exec('FLUSH PRIVILEGES');
			if (epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
				$log[] = 'provisioned via platform PDO admin';
				return array('ok' => true, 'log' => $log);
			}
		}
	} catch (Exception $e) {
		$log[] = 'platform PDO provision: ' . $e->getMessage();
	}
	$hint = (string) ($web['hint'] ?? epc_portal_demo_provision_failure_hint($log));
	error_log('epc_portal_demo: DB provision failed for ' . $dbName . ' — ' . $hint);
	return array('ok' => false, 'log' => $log, 'hint' => $hint);
}

function epc_portal_demo_schema_source_db(): string
{
	return 'docpart';
}

function epc_portal_demo_tenant_schema_ok(PDO $tenantPdo): bool
{
	foreach (array('lang_languages', 'templates', 'content', 'modules') as $tbl) {
		try {
			$tenantPdo->query('SELECT 1 FROM `' . $tbl . '` LIMIT 1');
		} catch (Exception $e) {
			return false;
		}
	}
	return true;
}

/**
 * Ensure demo tenant has full storefront schema (--no-data clone from docpart, never copies production rows).
 */
/**
 * Copy essential storefront tables from docpart (structure + seed rows) — read-only on production.
 */
function epc_portal_demo_clone_essential_from_docpart(PDO $destPdo, string $destDb): array
{
	$srcDb = epc_portal_demo_schema_source_db();
	$destEsc = str_replace('`', '', $destDb);
	$essential = array(
		'lang_languages', 'lang_texts', 'groups', 'users_groups_bind',
		'shop_offices', 'shop_geo', 'shop_offices_geo_map', 'shop_storages', 'shop_storages_data',
		'templates', 'content', 'content_fields_values', 'modules', 'modules_sites',
	);
	$copied = array();
	$errors = array();
	foreach ($essential as $tbl) {
		$tblEsc = str_replace('`', '', $tbl);
		try {
			$destPdo->exec('CREATE TABLE IF NOT EXISTS `' . $tblEsc . '` LIKE `' . $srcDb . '`.`' . $tblEsc . '`');
			$destPdo->exec('DELETE FROM `' . $tblEsc . '`');
			$destPdo->exec('INSERT INTO `' . $tblEsc . '` SELECT * FROM `' . $srcDb . '`.`' . $tblEsc . '`');
			$copied[] = $tblEsc;
		} catch (Exception $e) {
			$errors[$tblEsc] = $e->getMessage();
		}
	}
	return array('ok' => count($copied) > 0, 'tables' => $copied, 'errors' => $errors);
}

/** Clone empty table structures from docpart (read-only on source). */
function epc_portal_demo_php_clone_schema_structure(PDO $srcPdo, PDO $destPdo): array
{
	$created = array();
	$skipped = array();
	$errors = array();
	$tables = $srcPdo->query('SHOW TABLES')->fetchAll(PDO::FETCH_COLUMN);
	foreach ($tables as $tbl) {
		$tblEsc = str_replace('`', '', (string) $tbl);
		try {
			$destPdo->query('SELECT 1 FROM `' . $tblEsc . '` LIMIT 1');
			$skipped[] = $tblEsc;
			continue;
		} catch (Exception $e) {
			// table missing — create below
		}
		try {
			$create = $srcPdo->query('SHOW CREATE TABLE `' . $tblEsc . '`')->fetch(PDO::FETCH_ASSOC);
			if (!$create || empty($create['Create Table'])) {
				throw new RuntimeException('SHOW CREATE failed');
			}
			$destPdo->exec((string) $create['Create Table']);
			$created[] = $tblEsc;
		} catch (Exception $e) {
			$errors[$tblEsc] = $e->getMessage();
		}
	}
	return array('ok' => count($created) > 0 || count($skipped) > 0, 'created' => $created, 'skipped' => $skipped, 'errors' => $errors);
}

/** PHP row copy docpart → demo (read-only on source). */
function epc_portal_demo_php_clone_tables(PDO $srcPdo, PDO $destPdo, array $tables, bool $structureOnly = false): array
{
	$copied = array();
	$errors = array();
	foreach ($tables as $tbl) {
		$tblEsc = str_replace('`', '', $tbl);
		try {
			$create = $srcPdo->query('SHOW CREATE TABLE `' . $tblEsc . '`')->fetch(PDO::FETCH_ASSOC);
			if (!$create || empty($create['Create Table'])) {
				throw new RuntimeException('SHOW CREATE failed');
			}
			try {
				$destPdo->query('SELECT 1 FROM `' . $tblEsc . '` LIMIT 1');
			} catch (Exception $e) {
				$destPdo->exec((string) $create['Create Table']);
			}
			if ($structureOnly) {
				$copied[] = $tblEsc;
				continue;
			}
			$destPdo->exec('DELETE FROM `' . $tblEsc . '`');
			$rows = $srcPdo->query('SELECT * FROM `' . $tblEsc . '`')->fetchAll(PDO::FETCH_ASSOC);
			if (count($rows) > 0) {
				$cols = array_keys($rows[0]);
				$colList = '`' . implode('`,`', $cols) . '`';
				$ins = $destPdo->prepare('INSERT INTO `' . $tblEsc . '` (' . $colList . ') VALUES (' . implode(',', array_fill(0, count($cols), '?')) . ')');
				foreach ($rows as $r) {
					$ins->execute(array_values($r));
				}
			}
			$copied[] = $tblEsc;
		} catch (Exception $e) {
			$errors[$tblEsc] = $e->getMessage();
		}
	}
	return array('ok' => count($copied) > 0, 'tables' => $copied, 'errors' => $errors);
}

function epc_portal_demo_repair_storefront_schema(array $row, bool $forceReseed = false): array
{
	$destDb = trim((string) ($row['db_name'] ?? ''));
	if ($destDb === '') {
		return array('ok' => false, 'message' => 'Missing demo DB credentials');
	}
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$forceReseed && $tenantPdo instanceof PDO && epc_portal_demo_tenant_schema_ok($tenantPdo)) {
		return array('ok' => true, 'repaired' => false, 'message' => 'Schema OK');
	}
	try {
		$destUser = trim((string) ($row['db_user'] ?? ''));
		$platformPdo = epc_portal_platform_pdo();
		if (!$platformPdo instanceof PDO) {
			return array('ok' => false, 'message' => 'Platform DB unavailable for schema repair');
		}
		if ($destUser !== '') {
			$userEsc = str_replace("'", "''", $destUser);
			try {
				$platformPdo->exec("GRANT SELECT ON `docpart`.* TO '{$userEsc}'@'localhost'");
				$platformPdo->exec('FLUSH PRIVILEGES');
			} catch (Exception $e) {
				// continue — tenant user may already have cross-db read
			}
		}
		$tenantPdo = epc_portal_demo_tenant_pdo($row);
		if (!$tenantPdo instanceof PDO) {
			return array('ok' => false, 'message' => 'Demo DB connect failed');
		}
		require_once __DIR__ . '/epc_portal_tenant.php';
		$srcCreds = epc_portal_resolve_tenant_db_credentials();
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		$docPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $srcCreds['db'] . ';charset=utf8',
			$srcCreds['user'],
			$srcCreds['password'],
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$structure = epc_portal_demo_php_clone_schema_structure($docPdo, $tenantPdo);
		$seedTables = epc_portal_demo_docpart_seed_tables();
		$clone = epc_portal_demo_php_clone_tables($docPdo, $tenantPdo, $seedTables);
		foreach (array(
			'shop_orders', 'shop_orders_items', 'sessions',
			'epc_erp_invoices', 'epc_erp_purchase_orders', 'epc_erp_journal_entries',
			'epc_crm_leads', 'epc_crm_opportunities',
		) as $tbl) {
			try {
				$tenantPdo->exec('DELETE FROM `' . str_replace('`', '', $tbl) . '`');
			} catch (Exception $e) {
			}
		}
		if (!$tenantPdo instanceof PDO || !epc_portal_demo_tenant_schema_ok($tenantPdo)) {
			return array('ok' => false, 'message' => 'Schema clone incomplete', 'structure' => $structure, 'clone' => $clone);
		}
		return array('ok' => true, 'repaired' => true, 'message' => 'Storefront schema cloned from docpart', 'structure' => $structure, 'clone' => $clone);
	} catch (Exception $e) {
		return array('ok' => false, 'message' => $e->getMessage());
	}
}

function epc_portal_demo_clone_schema(string $srcDb, string $destDb, string $destUser, string $destPass): array
{
	require_once __DIR__ . '/epc_cloudpanel_helpers.php';
	$log = array();
	$cmd = 'mysqldump --single-transaction --no-data ' . escapeshellarg($srcDb)
		. ' 2>/dev/null | mysql -u ' . escapeshellarg($destUser)
		. ' -p' . escapeshellarg($destPass) . ' ' . escapeshellarg($destDb) . ' 2>&1';
	$out = epc_clp_run_cmd($cmd);
	$log[] = 'schema clone';
	$log[] = $out['output'];
	if ($out['code'] !== 0) {
		$dumpGz = '/tmp/epc-demo-schema-' . time() . '.sql.gz';
		@unlink($dumpGz);
		$exp = epc_clp_run_cmd('/usr/bin/clpctl db:export --databaseName=' . escapeshellarg($srcDb) . ' --file=' . escapeshellarg($dumpGz));
		$log[] = $exp['output'];
		if (is_file($dumpGz)) {
			$imp = epc_clp_run_cmd('/usr/bin/clpctl db:import --databaseName=' . escapeshellarg($destDb) . ' --file=' . escapeshellarg($dumpGz));
			$log[] = $imp['output'];
		}
	}
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $destDb . ';charset=utf8',
			$destUser,
			$destPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		if (!epc_portal_demo_tenant_schema_ok($pdo)) {
			$log[] = 'mysqldump clone incomplete — PHP fallback';
			$repair = epc_portal_demo_repair_storefront_schema(array(
				'db_name' => $destDb,
				'db_user' => $destUser,
				'db_password' => $destPass,
			));
			$log[] = (string) ($repair['message'] ?? '');
			if (empty($repair['ok'])) {
				return array('ok' => false, 'log' => $log, 'message' => (string) ($repair['message'] ?? 'Schema clone incomplete'));
			}
			$pdo = new PDO(
				'mysql:host=127.0.0.1;dbname=' . $destDb . ';charset=utf8',
				$destUser,
				$destPass,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
		}
		foreach (array(
			'shop_orders', 'shop_orders_items', 'sessions',
			'epc_erp_invoices', 'epc_erp_purchase_orders', 'epc_erp_journal_entries',
			'epc_crm_leads', 'epc_crm_opportunities',
		) as $tbl) {
			try {
				$pdo->exec('DELETE FROM `' . str_replace('`', '', $tbl) . '`');
			} catch (Exception $e) {
			}
		}
		if (!epc_portal_demo_tenant_schema_ok($pdo)) {
			return array('ok' => false, 'log' => $log, 'message' => 'Schema clone incomplete — missing core tables');
		}
	} catch (Exception $e) {
		return array('ok' => false, 'log' => array_merge($log, array($e->getMessage())), 'message' => $e->getMessage());
	}
	return array('ok' => true, 'log' => $log);
}

/**
 * ERP-only demo: essential CP tables + empty ERP/commerce structures (no storefront catalogue seed).
 */
function epc_portal_demo_clone_schema_erp_only(string $destDb, string $destUser, string $destPass): array
{
	$log = array('mode' => 'erp_only_minimal');
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $destDb . ';charset=utf8',
			$destUser,
			$destPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		return array('ok' => false, 'log' => array_merge($log, array($e->getMessage())));
	}
	$essential = epc_portal_demo_clone_essential_from_docpart($pdo, $destDb);
	$log[] = $essential;
	require_once __DIR__ . '/epc_portal_tenant.php';
	$srcCreds = epc_portal_resolve_tenant_db_credentials();
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	try {
		$docPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $srcCreds['db'] . ';charset=utf8',
			$srcCreds['user'],
			$srcCreds['password'],
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$structureTables = array(
			'sessions', 'users', 'users_profiles', 'plugins', 'shop_orders', 'shop_orders_items',
			'shop_catalogue_products', 'shop_catalogue_categories',
			'epc_erp_invoices', 'epc_erp_purchase_orders', 'epc_erp_journal_entries',
			'epc_crm_leads', 'epc_crm_opportunities',
		);
		$structure = epc_portal_demo_php_clone_tables($docPdo, $pdo, $structureTables, true);
		$log[] = $structure;
	} catch (Exception $e) {
		$log[] = 'structure_clone_error: ' . $e->getMessage();
	}
	if (!epc_portal_demo_tenant_schema_ok($pdo)) {
		$repair = epc_portal_demo_repair_storefront_schema(array(
			'db_name' => $destDb, 'db_user' => $destUser, 'db_password' => $destPass,
		));
		$log[] = $repair;
	}
	epc_portal_demo_seed_erp_cp_content($pdo);
	return array('ok' => epc_portal_demo_tenant_schema_ok($pdo), 'log' => $log);
}

/**
 * Ensure finance/ERP CP menu rows exist (copy from docpart template when missing).
 */
function epc_portal_demo_seed_erp_cp_content(PDO $tenantPdo): array
{
	require_once __DIR__ . '/epc_portal_tenant.php';
	$srcCreds = epc_portal_resolve_tenant_db_credentials();
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$out = array('ok' => true, 'seeded' => array());
	try {
		$docPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $srcCreds['db'] . ';charset=utf8',
			$srcCreds['user'],
			$srcCreds['password'],
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		return array('ok' => false, 'message' => $e->getMessage());
	}
	$erpUrls = array(
		'shop/finance/erp', 'shop/finance', 'shop/finance/erp/guide',
		'shop/finance/erp/invoices', 'shop/finance/erp/purchase_orders',
	);
	foreach ($erpUrls as $url) {
		try {
			$st = $tenantPdo->prepare('SELECT COUNT(*) FROM `content` WHERE `url` = ? AND `is_frontend` = 0');
			$st->execute(array($url));
			if ((int) $st->fetchColumn() > 0) {
				continue;
			}
			$src = $docPdo->prepare('SELECT * FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
			$src->execute(array($url));
			$row = $src->fetch(PDO::FETCH_ASSOC);
			if (!$row) {
				continue;
			}
			unset($row['id']);
			$cols = array_keys($row);
			$colList = '`' . implode('`,`', $cols) . '`';
			$ins = $tenantPdo->prepare(
				'INSERT INTO `content` (' . $colList . ') VALUES (' . implode(',', array_fill(0, count($cols), '?')) . ')'
			);
			$ins->execute(array_values($row));
			$out['seeded'][] = $url;
		} catch (Exception $e) {
			$out['errors'][$url] = $e->getMessage();
		}
	}
	$dcInstall = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_cp_install.php';
	if (is_file($dcInstall)) {
		require_once $dcInstall;
		try {
			$out['document_control'] = epc_document_control_cp_install($tenantPdo, 'cp');
		} catch (Throwable $e) {
			$out['document_control_error'] = $e->getMessage();
		}
	}
	return $out;
}

function epc_portal_demo_temp_password(): string
{
	return substr(bin2hex(random_bytes(4)), 0, 4) . 'Demo' . random_int(10, 99) . '!';
}

/** All backend-capable group IDs in tenant DB (root + nested). */
function epc_portal_demo_ensure_tenant_backend_groups(PDO $tenantPdo): void
{
	try {
		$has = (int) $tenantPdo->query('SELECT COUNT(*) FROM `groups` WHERE `for_backend` = 1')->fetchColumn();
		if ($has > 0) {
			return;
		}
		$maxId = (int) $tenantPdo->query('SELECT IFNULL(MAX(`id`), 0) FROM `groups`')->fetchColumn();
		$gid = $maxId > 0 ? $maxId + 1 : 3;
		$tenantPdo->prepare(
			'INSERT INTO `groups` (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`)
			 VALUES (?, ?, 0, 2, 1, 1, 0, 0, 1, 0, ?, 90)'
		)->execute(array($gid, 'DEMO_CP_ADMIN', 'Demo CP administrators'));
	} catch (Throwable $e) {
	}
}

function epc_portal_demo_cp_backend_group_ids(PDO $tenantPdo): array
{
	epc_portal_demo_ensure_tenant_backend_groups($tenantPdo);
	$ids = array();
	try {
		$st = $tenantPdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 ORDER BY `id` ASC');
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$ids[] = (int) $row['id'];
		}
	} catch (Throwable $e) {
	}
	if ($ids === array()) {
		$ids = array(3);
	}
	return array_values(array_unique($ids));
}

/** Ensure demo CP user has backend group bind (self-heal missing users_groups_bind). */
function epc_portal_demo_ensure_cp_user_backend_groups(PDO $tenantPdo, int $userId): array
{
	$groups = epc_portal_demo_cp_backend_group_ids($tenantPdo);
	$ins = $tenantPdo->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
	foreach ($groups as $gid) {
		$ins->execute(array($userId, (int) $gid));
	}
	return $groups;
}

function epc_portal_demo_cp_autologin_secret(): string
{
	$authFile = $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
	if (is_file($authFile)) {
		require_once $authFile;
		if (function_exists('epc_deploy_token')) {
			return epc_deploy_token();
		}
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	return (string) $cfg->secret_succession;
}

function epc_portal_demo_cp_autologin_sign(string $siteKey, int $ts, int $operatorUserId): string
{
	return hash_hmac('sha256', $siteKey . '|' . $ts . '|' . $operatorUserId, epc_portal_demo_cp_autologin_secret());
}

/** Signed one-click demo CP URL for Super CP operators (60s token). */
function epc_portal_demo_cp_autologin_url(string $siteKey, ?int $operatorUserId = null): string
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return '';
	}
	$operatorId = $operatorUserId ?? 0;
	if ($operatorId <= 0 && class_exists('DP_User')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		if (DP_User::isAdmin()) {
			$operatorId = (int) DP_User::getAdminId();
		}
	}
	if ($operatorId <= 0) {
		return 'https://www.ecomae.com' . epc_portal_demo_cp_login_url($key);
	}
	$ts = time();
	$sig = epc_portal_demo_cp_autologin_sign($key, $ts, $operatorId);
	$qs = http_build_query(array(
		'site_key' => $key,
		'ts' => $ts,
		'uid' => $operatorId,
		'sig' => $sig,
	));
	return 'https://www.ecomae.com/epc-demo-cp-autologin.php?' . $qs;
}

/** Create tenant CP session cookies (admin_session / admin_u_id). */
function epc_portal_demo_cp_establish_session(PDO $tenantPdo, int $userId, string $contactType = 'email'): string
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$st = $tenantPdo->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$st->execute(array($userId));
	$user = $st->fetch(PDO::FETCH_ASSOC);
	if (!$user) {
		return '';
	}
	$authContact = $contactType === 'phone' ? (string) ($user['phone'] ?? '') : (string) ($user['email'] ?? '');
	if ($authContact === '') {
		$authContact = (string) ($user['email'] ?? '');
		$contactType = 'email';
	}
	$time = time();
	$sessionSuccession = md5($authContact . $time . $cfg->secret_succession);
	$csrfGuardKey = sha1($cfg->secret_succession . $sessionSuccession . ($_SERVER['REMOTE_ADDR'] ?? '') . ($_SERVER['HTTP_USER_AGENT'] ?? ''));
	$tenantPdo->prepare(
		'INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `type`, `contact_type`, `csrf_guard_key`) VALUES (?,?,?,?,?,?,?)'
	)->execute(array($sessionSuccession, $userId, $time, '', 1, $contactType, $csrfGuardKey));
	$secure = !empty($_SERVER['HTTPS']);
	setcookie('admin_session', $sessionSuccession, 0, '/', '', $secure, true);
	setcookie('admin_u_id', (string) $userId, 0, '/', '', $secure, true);
	$_COOKIE['admin_session'] = $sessionSuccession;
	$_COOKIE['admin_u_id'] = (string) $userId;
	if (!empty($GLOBALS['epc_demo_cp_site_key'])) {
		epc_portal_demo_cp_set_scope_cookie((string) $GLOBALS['epc_demo_cp_site_key']);
	}
	return $sessionSuccession;
}

function epc_portal_demo_cp_post_login_url(string $siteKey, ?array $row = null): string
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($row === null && function_exists('epc_portal_demo_load_live_row')) {
		$row = epc_portal_demo_load_live_row($key);
	}
	if (is_array($row) && epc_portal_demo_row_is_erp_only($row)) {
		return epc_portal_demo_erp_shell_url($key);
	}
	return epc_portal_demo_cp_tenant_base($key) . 'shop/orders';
}

function epc_portal_demo_create_cp_user(PDO $tenantPdo, string $email, string $password, string $name): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$groups = epc_portal_demo_cp_backend_group_ids($tenantPdo);
	// Use bcrypt for new/reset passwords; fall back to MD5 only if bcrypt unavailable
	$upgFile = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_password_upgrade.php';
	if (is_file($upgFile)) {
		require_once $upgFile;
	}
	$hash = function_exists('epc_password_hash') ? epc_password_hash($password) : md5($password . $cfg->secret_succession);
	$st = $tenantPdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($email));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		$tenantPdo->prepare(
			'INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `admin_created`)
			 VALUES (?, 1, ?, 1, 1, ?, 1)'
		)->execute(array($email, $hash, (string) time()));
		$userId = (int) $tenantPdo->lastInsertId();
		@$tenantPdo->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')
			->execute(array($userId, 'name', $name));
		epc_portal_demo_ensure_cp_user_backend_groups($tenantPdo, $userId);
		return array('created' => true, 'user_id' => $userId, 'groups_refreshed' => $groups);
	}
	$userId = (int) $row['user_id'];
	$tenantPdo->prepare('UPDATE `users` SET `password` = ?, `unlocked` = 1, `email_confirmed` = 1 WHERE `user_id` = ?')
		->execute(array($hash, $userId));
	epc_portal_demo_ensure_cp_user_backend_groups($tenantPdo, $userId);
	return array('created' => false, 'user_id' => $userId, 'password_reset' => true, 'groups_refreshed' => $groups);
}

function epc_portal_demo_seed_products(PDO $tenantPdo, string $industry, string $tradeName): void
{
	$now = time();
	if ($industry === 'auto_parts') {
		$products = array(
			array('Brake pad set — front axle', 'BP-4410-A', 189.00),
			array('Oil filter — universal fit', 'OF-2201', 45.00),
			array('Spark plug iridium (4-pack)', 'SP-9004', 120.00),
			array('Air filter — cabin', 'AF-C102', 65.00),
			array('Wiper blade pair 24"/18"', 'WB-2418', 55.00),
		);
	} else {
		$products = array(
			array('Linen blend shirt — white', 'SN-LN-WHT', 149.00),
			array('Slim fit chinos — navy', 'SN-CH-NVY', 199.00),
			array('Running sneakers — grey', 'SN-SN-GRY', 299.00),
			array('Crossbody bag — tan', 'SN-BG-TAN', 179.00),
			array('Sunglasses — polarized', 'SN-SG-BLK', 89.00),
		);
	}
	foreach ($products as $i => $p) {
		try {
			$tenantPdo->prepare(
				'INSERT INTO `shop_catalogue_products` (`caption`, `alias`, `published_flag`, `price`, `time_created`)
				 SELECT ?, ?, 1, ?, ? FROM DUAL
				 WHERE NOT EXISTS (SELECT 1 FROM `shop_catalogue_products` WHERE `alias` = ? LIMIT 1)'
			)->execute(array($p[0], $p[1], $p[2], $now + $i, $p[1]));
		} catch (Exception $e) {
			// schema may differ — seed is best-effort
		}
	}
}

function epc_portal_is_demo_storefront_context(): bool
{
	return !empty($GLOBALS['epc_demo_storefront_context']);
}

/** Path prefix for demo storefront URLs, e.g. `/demo/demo_260601_ap_1`. */
function epc_portal_demo_path_prefix(): string
{
	if (empty($GLOBALS['epc_demo_storefront_context']) || empty($GLOBALS['epc_demo_storefront_site_key'])) {
		return '';
	}
	return '/demo/' . preg_replace('/[^a-z0-9_]/', '', (string) $GLOBALS['epc_demo_storefront_site_key']);
}

/** Web-root prefix for static assets on demo paths (same as path prefix). */
function epc_portal_demo_asset_prefix(): string
{
	return epc_portal_demo_path_prefix();
}

function epc_portal_demo_lock_domain_path($DP_Config): void
{
	if (!is_object($DP_Config) || epc_portal_demo_path_prefix() === '') {
		return;
	}
	$DP_Config->domain_path = 'https://www.ecomae.com' . epc_portal_demo_path_prefix() . '/';
}

function epc_portal_demo_patch_multilang_params(array $params): array
{
	$prefix = epc_portal_demo_path_prefix();
	if ($prefix === '') {
		return $params;
	}
	if (!empty($params['lang_href']) && strpos((string) $params['lang_href'], $prefix) !== 0) {
		$params['lang_href'] = $prefix . $params['lang_href'];
	}
	if (!empty($params['page_url_with_lang_tag']) && strpos((string) $params['page_url_with_lang_tag'], $prefix) !== 0) {
		$params['page_url_with_lang_tag'] = $prefix . $params['page_url_with_lang_tag'];
	}
	return $params;
}

/**
 * Re-lock demo domain_path and lang_href after portal/config or multilang init.
 */
function epc_portal_demo_finalize_runtime($DP_Config, ?array &$multilang_params = null): void
{
	if (!epc_portal_is_demo_storefront_context()) {
		return;
	}
	if (function_exists('epc_site_context_reset')) {
		epc_site_context_reset();
	}
	epc_portal_demo_lock_domain_path($DP_Config);
	if ($multilang_params !== null) {
		$multilang_params = epc_portal_demo_patch_multilang_params($multilang_params);
		$GLOBALS['multilang_params'] = $multilang_params;
	}
}

function epc_portal_demo_filter_output_html(string $html): string
{
	$prefix = epc_portal_demo_path_prefix();
	if ($prefix === '') {
		return $html;
	}
	$demoBase = 'https://www.ecomae.com' . $prefix;
	$langHome = $demoBase . '/en/';
	if (!empty($GLOBALS['multilang_params']['lang_href'])) {
		$langHome = rtrim((string) $GLOBALS['multilang_params']['lang_href'], '/') . '/';
	}
	$replacements = array(
		'https://www.epartscart.com/' => $demoBase . '/',
		'http://www.epartscart.com/' => $demoBase . '/',
		'https://www.epartscart.com' => $demoBase,
		'http://www.epartscart.com' => $demoBase,
		'https://www.ecomae.com/en/' => $demoBase . '/en/',
		'href="/en/' => 'href="' . $prefix . '/en/',
		"href='/en/" => "href='" . $prefix . "/en/",
		'action="/en/' => 'action="' . $prefix . '/en/',
		"action='/en/" => "action='" . $prefix . "/en/",
		'class="header-home-btn" href="/"' => 'class="header-home-btn" href="' . $langHome . '"',
		'<a href="/">Home</a>' => '<a href="' . $langHome . '">Home</a>',
		'<li class="active"><a href="/">Home</a>' => '<li class="active"><a href="' . $langHome . '">Home</a>',
	);
	foreach ($replacements as $from => $to) {
		$html = str_replace($from, $to, $html);
	}
	$html = str_replace($prefix . $prefix, $prefix, $html);
	$html = str_replace($demoBase . $prefix, $demoBase, $html);
	$html = epc_portal_demo_patch_desktop_top_menu_html($html);
	return epc_portal_demo_rewrite_static_asset_urls($html);
}

/**
 * Desktop top-menu docpart can render empty while mobile nav has items (stale template / module bind).
 * Copy mobile navbar into desktop header when auto-parts demo parity requires it.
 */
function epc_portal_demo_patch_desktop_top_menu_html(string $html): string
{
	if (!epc_portal_is_demo_storefront_context() || !epc_portal_demo_is_autoparts_parity()) {
		return $html;
	}
	if (strpos($html, '<header class="hidden-xs">') === false) {
		return $html;
	}
	if (preg_match(
		'#<header class="hidden-xs">.*?top-menu-line.*?<nav class="navbar[^>]*>\s*<ul class="nav navbar-nav"#s',
		$html
	)) {
		return $html;
	}
	if (!preg_match(
		'#id="bs-example-navbar-collapse-1">\s*(<ul class="nav navbar-nav">.*?</ul>)#s',
		$html,
		$m
	)) {
		return $html;
	}
	$menuUl = $m[1];
	$patched = preg_replace(
		'#(<header class="hidden-xs">.*?top-menu-line.*?<nav class="navbar[^"]*"[^>]*>)\s*(</nav>)#s',
		'$1' . "\n\t\t\t\t\t\t\t" . $menuUl . '$2',
		$html,
		1
	);
	return is_string($patched) ? $patched : $html;
}

/**
 * Route storefront CSS/JS/images through epc-static.php when nginx 404s docroot paths on www.ecomae.com.
 */
function epc_portal_demo_static_gateway_href(string $relPath): string
{
	$relPath = str_replace('\\', '/', $relPath);
	$fragment = '';
	if (strpos($relPath, '#') !== false) {
		$parts = explode('#', $relPath, 2);
		$relPath = $parts[0];
		$fragment = '#' . $parts[1];
	}
	$cacheBust = '';
	if (strpos($relPath, '?') !== false) {
		$parts = explode('?', $relPath, 2);
		$relPath = $parts[0];
		if ($parts[1] !== '') {
			$bv = preg_replace('/^v=/', '', $parts[1]);
			$cacheBust = '&bv=' . rawurlencode($bv);
		}
	}
	$relPath = ltrim($relPath, '/');
	return '/epc-static.php?f=' . rawurlencode($relPath) . $cacheBust . $fragment;
}

function epc_portal_demo_rewrite_static_asset_urls(string $html): string
{
	$html = preg_replace_callback(
		'#\b(href|src)=(["\'])(?!https?://|//|data:|/epc-static\.php)([^"\']+)#i',
		static function (array $m): string {
			$target = $m[3];
			if (preg_match('#^favicon\.(svg|ico)#i', $target)) {
				return $m[0];
			}
			if (!preg_match('#(?:^|/)(?:[^/\s]+\.(?:css|js|png|jpe?g|gif|svg|webp|woff2?|ico|ttf|map)|assets/|css/)#i', $target)) {
				return $m[0];
			}
			if (strpos($target, 'assets/') === 0 || strpos($target, 'css/') === 0) {
				$target = 'templates/nero/' . $target;
			} elseif ($target[0] === '/') {
				$target = ltrim($target, '/');
			}
			return $m[1] . '=' . $m[2] . epc_portal_demo_static_gateway_href($target);
		},
		$html
	);
	$html = preg_replace_callback(
		"#url\\(['\"]?(/?(?:content|templates|modules)/[^'\"\\)]+\\.(?:png|jpe?g|gif|svg|webp))['\"]?\\)#i",
		static function (array $m): string {
			return "url('" . epc_portal_demo_static_gateway_href(ltrim($m[1], '/')) . "')";
		},
		$html
	);
	return $html;
}

function epc_portal_demo_start_output_buffer(): void
{
	if (!epc_portal_is_demo_storefront_context()) {
		return;
	}
	ob_start('epc_portal_demo_filter_output_html');
}

/**
 * Rewrite cloned docpart/epartscart absolute URLs in demo tenant DB content.
 */
function epc_portal_demo_rewrite_tenant_content_urls(PDO $tenantPdo, string $siteKey): array
{
	$key = preg_replace('/[^a-z0-9_]/', '', $siteKey);
	$pathPrefix = '/demo/' . $key;
	$demoBase = 'https://www.ecomae.com' . $pathPrefix;
	$stats = array('site_key' => $key, 'columns' => array(), 'domain_path' => false);

	$replacements = array(
		'https://www.epartscart.com/' => $demoBase . '/',
		'http://www.epartscart.com/' => $demoBase . '/',
		'https://www.epartscart.com' => $demoBase,
		'http://www.epartscart.com' => $demoBase,
		'https://www.ecomae.com/en/' => $demoBase . '/en/',
		'http://www.ecomae.com/en/' => $demoBase . '/en/',
		'href="/en/' => 'href="' . $pathPrefix . '/en/',
		"href='/en/" => "href='" . $pathPrefix . "/en/",
		'action="/en/' => 'action="' . $pathPrefix . '/en/',
		"action='/en/" => "action='" . $pathPrefix . "/en/",
		"location='/en/" => "location='" . $pathPrefix . "/en/",
		'location="/en/' => 'location="' . $pathPrefix . '/en/',
	);

	try {
		require_once __DIR__ . '/epc_portal_db.php';
		$settings = epc_portal_load_site_settings_for_host($tenantPdo, 'www.ecomae.com');
		$settings['domain_path'] = $demoBase . '/';
		epc_portal_save_site_settings($tenantPdo, $settings);
		$stats['domain_path'] = true;
	} catch (Exception $e) {
		$stats['domain_path_error'] = $e->getMessage();
	}

	try {
		$colSt = $tenantPdo->query('SHOW COLUMNS FROM `content`');
		$cols = $colSt ? $colSt->fetchAll(PDO::FETCH_COLUMN) : array();
		$textCols = array_values(array_intersect($cols, array(
			'content', 'description', 'meta_description', 'meta_keywords', 'meta_title', 'title_tag', 'url',
		)));
		foreach ($textCols as $col) {
			$updated = 0;
			foreach ($replacements as $from => $to) {
				$st = $tenantPdo->prepare(
					'UPDATE `content` SET `' . $col . '` = REPLACE(`' . $col . '`, ?, ?)'
				);
				$st->execute(array($from, $to));
				$updated += (int) $st->rowCount();
			}
			if ($updated > 0) {
				$stats['columns'][$col] = $updated;
			}
		}
	} catch (Exception $e) {
		$stats['content_error'] = $e->getMessage();
	}

	return $stats;
}

/**
 * Read-only clone of live epartscart.com theme from shared docpart DB — never writes to production.
 */
function epc_portal_demo_epartscart_theme_source(): ?array
{
	require_once __DIR__ . '/epc_portal_tenant.php';
	$resolved = epc_portal_resolve_tenant_db_credentials();
	if (empty($resolved['db']) || empty($resolved['user'])) {
		return null;
	}
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		$docPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $resolved['db'] . ';charset=utf8',
			$resolved['user'],
			$resolved['password'],
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		require_once __DIR__ . '/epc_portal_db.php';
		return epc_portal_load_site_settings_for_host($docPdo, 'www.epartscart.com');
	} catch (Exception $e) {
		return null;
	}
}

function epc_portal_demo_push_tenant_settings(PDO $platformPdo, string $siteKey, array $preset, string $tradeName, string $email): array
{
	require_once __DIR__ . '/epc_portal_tenant_intro.php';
	$row = epc_portal_tenant_get($platformPdo, $siteKey);
	if ($row === null) {
		return array('ok' => false, 'message' => 'Tenant not found');
	}
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$tenantPdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Tenant DB connect failed');
	}
	require_once __DIR__ . '/epc_portal_theme_templates.php';
	$industry = (string) $preset['industry_code'];
	$demoUrl = 'https://www.ecomae.com/demo/' . $siteKey . '/';
	$settings = null;
	if ($industry === 'auto_parts') {
		$settings = epc_portal_demo_epartscart_theme_source();
	}
	if (!is_array($settings) || $settings === array()) {
		$settings = epc_portal_default_site_settings('www.epartscart.com');
	}
	$epcClone = $settings;
	$settings['host'] = 'www.ecomae.com';
	$settings['industry_code'] = $industry;
	if ($industry === 'auto_parts') {
		$settings['system_name'] = (string) ($epcClone['system_name'] ?? 'eParts Cart');
		$settings['hub_name'] = (string) ($epcClone['hub_name'] ?? 'Electronic World Group');
		$settings['tagline'] = (string) ($epcClone['tagline'] ?? 'Auto parts & commerce');
	} else {
		$settings['system_name'] = $tradeName . ' Demo';
		$settings['hub_name'] = $tradeName;
		$settings['tagline'] = 'Designed by ecomae';
	}
	$settings['access_mode'] = 'full';
	$settings['enabled_packs'] = $preset['cp_packs'];
	$settings['theme_template'] = (string) ($preset['theme_template'] ?? ($settings['theme_template'] ?? 'classic'));
	if (empty($settings['theme']) || !is_array($settings['theme'])) {
		$settings['theme'] = epc_portal_style_template_theme($industry, $settings['theme_template']);
	}
	$settings['domain_path'] = $demoUrl;
	$contact = isset($settings['contact']) && is_array($settings['contact'])
		? $settings['contact']
		: epc_portal_default_contact(array('trade_name' => $tradeName));
	if ($industry === 'auto_parts') {
		$contact['trade_name'] = (string) ($epcClone['contact']['trade_name'] ?? 'eParts Cart');
		$contact['from_name'] = (string) ($epcClone['contact']['from_name'] ?? $contact['trade_name']);
	} else {
		$contact['trade_name'] = $tradeName;
	}
	$contact['storefront_package'] = (string) $preset['storefront_package'];
	$contact['contact_email'] = $email;
	if ($industry === 'auto_parts') {
		$contact['use_animated_hub_logo'] = false;
		$contact['use_tenant_brand'] = false;
	} else {
		$contact['use_animated_hub_logo'] = true;
	}
	$settings['contact'] = $contact;
	if ($industry === 'auto_parts' && !empty($epcClone['cp_menu']) && is_array($epcClone['cp_menu'])) {
		$settings['cp_menu'] = $epcClone['cp_menu'];
	}
	epc_portal_save_site_settings($tenantPdo, $settings);
	if ($industry === 'auto_parts') {
		$bootstrap = epc_portal_demo_apply_autoparts_theme_bootstrap($tenantPdo, true);
		epc_portal_demo_rewrite_tenant_content_urls($tenantPdo, $siteKey);
	} else {
		$bootstrap = null;
	}
	epc_portal_demo_seed_products($tenantPdo, $industry, $tradeName);
	$sync = epc_portal_demo_sync_cp_packs($platformPdo, $siteKey, $preset);
	if (empty($row['is_demo'])) {
		require_once __DIR__ . '/epc_portal_cp_menu.php';
		$sync = epc_portal_sync_tenant_packs_to_client_db($platformPdo, $siteKey);
	}
	return array(
		'ok' => true,
		'message' => 'Settings seeded',
		'sync' => $sync,
		'storefront_package' => (string) $preset['storefront_package'],
		'theme_cloned_from' => ($industry === 'auto_parts' ? 'www.epartscart.com (read-only)' : 'preset'),
		'autoparts_bootstrap' => $bootstrap,
	);
}

function epc_portal_demo_push_tenant_settings_erp_only(PDO $platformPdo, string $siteKey, array $preset, string $tradeName, string $email, string $phone = ''): array
{
	require_once __DIR__ . '/epc_portal_tenant_intro.php';
	$row = epc_portal_tenant_get($platformPdo, $siteKey);
	if ($row === null) {
		return array('ok' => false, 'message' => 'Tenant not found');
	}
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$tenantPdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Tenant DB connect failed');
	}
	$settings = epc_portal_default_site_settings('www.ecomae.com');
	$settings['host'] = 'www.ecomae.com';
	$settings['industry_code'] = (string) ($preset['registry_industry'] ?? 'erp_standalone');
	$settings['system_name'] = $tradeName . ' ERP Demo';
	$settings['hub_name'] = $tradeName;
	$settings['access_mode'] = 'erp_only';
	$settings['commerce_enabled'] = 0;
	$settings['enabled_packs'] = function_exists('epc_portal_erp_only_packs')
		? epc_portal_erp_only_packs()
		: (array) ($preset['cp_packs'] ?? array('core', 'erp', 'professional'));
	$settings['theme_template'] = 'classic';
	$settings['domain_path'] = 'https://www.ecomae.com' . epc_portal_demo_cp_path_prefix() . preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey)) . '/';
	$contact = epc_portal_default_contact(array('trade_name' => $tradeName));
	$contact['trade_name'] = $tradeName;
	$contact['storefront_package'] = 'none';
	$contact['contact_email'] = $email;
	if ($phone !== '') {
		$contact['phone'] = $phone;
	}
	$contact['use_tenant_brand'] = true;
	$contact['use_animated_hub_logo'] = false;
	$settings['contact'] = $contact;
	epc_portal_save_site_settings($tenantPdo, $settings);
	epc_portal_demo_seed_erp_cp_content($tenantPdo);
	require_once __DIR__ . '/epc_portal_cp_menu.php';
	$sync = epc_portal_sync_tenant_packs_to_client_db($platformPdo, $siteKey);
	return array(
		'ok' => true,
		'message' => 'ERP-only settings seeded',
		'sync' => $sync,
		'storefront_package' => 'none',
		'access_mode' => 'erp_only',
	);
}

function epc_portal_demo_build_email(array $result): array
{
	$urls = $result['urls'] ?? array();
	$expires = isset($result['expires_at']) ? date('l, j F Y H:i T', (int) $result['expires_at']) : '';
	$erpOnly = !empty($result['demo_erp_only']);
	$subject = $erpOnly ? 'Your ECOM AE ERP-only sandbox demo' : 'Your ECOM AE sandbox demo';
	if ($erpOnly) {
		$plain = "Your ECOM AE ERP-only sandbox demo\n\n"
			. "Company: " . ($result['company'] ?? '') . "\n"
			. "Package: ERP / CRM / Finance — no e-commerce storefront\n\n"
			. "ERP login (Demo CP): " . ($urls['cp'] ?? '') . "\n\n"
			. "Login email: " . ($result['email'] ?? '') . "\n"
			. "Temporary password: " . ($result['temp_password'] ?? '') . "\n\n"
			. "Expires: {$expires}\n\n"
			. "This is sandbox only — not production.\n\n— ECOM AE Platform\nhello@ecomae.com";
		$html = '<!DOCTYPE html><html><body style="font-family:Arial,sans-serif;line-height:1.5;color:#0f172a">'
			. '<h2 style="color:#0284c7">Your ERP-only sandbox demo</h2>'
			. '<p>Your <strong>ERP-only</strong> demo is ready — finance, CRM, and operations modules. No storefront is included.</p>'
			. '<table cellpadding="6" style="border-collapse:collapse">'
			. '<tr><td><strong>ERP login (Demo CP)</strong></td><td><a href="' . htmlspecialchars((string) ($urls['cp'] ?? ''), ENT_QUOTES) . '">' . htmlspecialchars((string) ($urls['cp'] ?? ''), ENT_QUOTES) . '</a></td></tr>'
			. '<tr><td><strong>Login email</strong></td><td>' . htmlspecialchars((string) ($result['email'] ?? ''), ENT_QUOTES) . '</td></tr>'
			. '<tr><td><strong>Temp password</strong></td><td><code>' . htmlspecialchars((string) ($result['temp_password'] ?? ''), ENT_QUOTES) . '</code></td></tr>'
			. '<tr><td><strong>Expires</strong></td><td>' . htmlspecialchars($expires, ENT_QUOTES) . '</td></tr>'
			. '</table>'
			. '<p style="margin-top:16px;padding:12px;background:#fef3c7;border-radius:6px"><strong>Sandbox only</strong> — not production.</p>'
			. '<p style="color:#64748b;font-size:13px">ECOM AE · hello@ecomae.com</p></body></html>';
		return array('subject' => $subject, 'plain' => $plain, 'html' => $html);
	}
	$plain = "Your ECOM AE sandbox demo\n\n"
		. "Company: " . ($result['company'] ?? '') . "\n"
		. "Industry: " . ($result['industry_label'] ?? '') . "\n\n"
		. "Storefront: " . ($urls['storefront'] ?? '') . "\n"
		. "Demo CP: " . ($urls['cp'] ?? '') . "\n\n"
		. "Login email: " . ($result['email'] ?? '') . "\n"
		. "Temporary password: " . ($result['temp_password'] ?? '') . "\n\n"
		. "Expires: {$expires} (3 days)\n\n"
		. "This is sandbox only — not production. Do not use real customer data.\n\n"
		. "— ECOM AE Platform\nhello@ecomae.com";
	$html = '<!DOCTYPE html><html><body style="font-family:Arial,sans-serif;line-height:1.5;color:#0f172a">'
		. '<h2 style="color:#0284c7">Your ECOM AE sandbox demo</h2>'
		. '<p>Your <strong>' . htmlspecialchars((string) ($result['industry_label'] ?? ''), ENT_QUOTES, 'UTF-8') . '</strong> demo is ready.</p>'
		. '<table cellpadding="6" style="border-collapse:collapse">'
		. '<tr><td><strong>Storefront</strong></td><td><a href="' . htmlspecialchars((string) ($urls['storefront'] ?? ''), ENT_QUOTES) . '">' . htmlspecialchars((string) ($urls['storefront'] ?? ''), ENT_QUOTES) . '</a></td></tr>'
		. '<tr><td><strong>Demo CP</strong></td><td><a href="' . htmlspecialchars((string) ($urls['cp'] ?? ''), ENT_QUOTES) . '">' . htmlspecialchars((string) ($urls['cp'] ?? ''), ENT_QUOTES) . '</a></td></tr>'
		. '<tr><td><strong>Login email</strong></td><td>' . htmlspecialchars((string) ($result['email'] ?? ''), ENT_QUOTES) . '</td></tr>'
		. '<tr><td><strong>Temp password</strong></td><td><code>' . htmlspecialchars((string) ($result['temp_password'] ?? ''), ENT_QUOTES) . '</code></td></tr>'
		. '<tr><td><strong>Expires</strong></td><td>' . htmlspecialchars($expires, ENT_QUOTES) . '</td></tr>'
		. '</table>'
		. '<p style="margin-top:16px;padding:12px;background:#fef3c7;border-radius:6px"><strong>Sandbox only</strong> — not production. Do not enter real customer or payment data.</p>'
		. '<p style="color:#64748b;font-size:13px">ECOM AE · hello@ecomae.com</p></body></html>';
	return array('subject' => $subject, 'plain' => $plain, 'html' => $html);
}

function epc_portal_demo_send_email(array $result): bool
{
	$email = trim((string) ($result['email'] ?? ''));
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return false;
	}
	$built = epc_portal_demo_build_email($result);
	$headers = "MIME-Version: 1.0\r\nContent-Type: text/html; charset=UTF-8\r\nFrom: ECOM AE Demo <hello@ecomae.com>\r\nReply-To: hello@ecomae.com\r\n";
	return @mail($email, $built['subject'], $built['html'], $headers);
}

/**
 * Main provision flow — isolated DB per demo, never docpart/ecomae production data.
 */
function epc_portal_demo_provision(PDO $pdo, array $params): array
{
	epc_portal_demo_ensure_schema($pdo);
	$name = trim((string) ($params['contact_name'] ?? $params['name'] ?? ''));
	$email = strtolower(trim((string) ($params['contact_email'] ?? $params['email'] ?? '')));
	$phone = epc_portal_demo_normalize_phone((string) ($params['contact_phone'] ?? $params['phone'] ?? ''));
	$company = trim((string) ($params['company'] ?? ''));
	$countryCode = strtoupper(substr(preg_replace('/[^A-Za-z]/', '', (string) ($params['country_code'] ?? '')), 0, 2));
	if ($countryCode === '' && !empty($params['country'])) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
		$countryCode = epc_countries_normalize_code((string) $params['country']);
	}
	if ($countryCode === '') {
		$countryCode = 'AE';
	}
	$industry = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($params['industry_code'] ?? $params['industry'] ?? '')));
	$notes = trim((string) ($params['notes'] ?? ''));
	$terms = !empty($params['terms']) || !empty($params['accept_terms']);

	$presets = epc_portal_demo_industry_presets();
	if ($name === '') {
		return array('ok' => false, 'message' => 'Name is required');
	}
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'Valid email is required');
	}
	if (!epc_portal_demo_phone_valid($phone)) {
		return array('ok' => false, 'message' => 'Valid phone number is required (7–15 digits)');
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
	$countryNames = epc_countries_iso3166_alpha2();
	if ($countryCode === '' || !isset($countryNames[$countryCode])) {
		return array('ok' => false, 'message' => 'Please select your country');
	}
	if (!$terms) {
		return array('ok' => false, 'message' => 'You must accept the demo terms');
	}
	if (!isset($presets[$industry])) {
		if ($industry !== '') {
			return array('ok' => false, 'message' => 'Industry not available — choose auto parts, fashion, or ERP only');
		}
		return array('ok' => false, 'message' => 'Select an industry (auto_parts, fashion, or erp_only)');
	}
	if (epc_portal_demo_count_active($pdo) >= epc_portal_demo_max_active()) {
		return array('ok' => false, 'message' => 'Demo capacity reached (' . epc_portal_demo_max_active() . ' active). Try again later or contact hello@ecomae.com');
	}
	if (epc_portal_demo_rate_limited($pdo, $email)) {
		return array('ok' => false, 'message' => 'One demo per email per 24 hours. Check your inbox or try again tomorrow.');
	}

	$preset = $presets[$industry];
	$isErpOnly = !empty($preset['demo_erp_only']);
	$registryIndustry = (string) ($preset['registry_industry'] ?? $industry);
	$tradeName = $company !== '' ? $company : ($name . ' Demo');
	$siteKey = epc_portal_demo_generate_site_key($pdo, $company !== '' ? $company : $name, $industry);
	$dbName = epc_portal_demo_db_name($siteKey);
	$dbUser = $dbName;
	$dbPass = epc_portal_demo_temp_password() . bin2hex(random_bytes(4));
	$fromPool = false;
	$pool = epc_portal_demo_pool_claim($pdo, $siteKey);
	if ($pool !== null) {
		$dbName = (string) $pool['db_name'];
		$dbUser = (string) $pool['db_user'];
		$dbPass = (string) $pool['db_password'];
		$fromPool = true;
	}
	$tempPass = epc_portal_demo_temp_password();
	$expiresAt = time() + (epc_portal_demo_days() * 86400);
	$srcDb = epc_portal_demo_schema_source_db();

	$reqSt = $pdo->prepare(
		'INSERT INTO `epc_portal_demo_requests`
		 (`email`, `contact_name`, `company`, `contact_phone`, `industry_code`, `site_key`, `status`, `ip_address`, `notes`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?, \'pending\', ?, ?, ?)'
	);
	$reqSt->execute(array(
		$email, $name, $company, $phone, $industry, $siteKey,
		function_exists('epc_deploy_client_ip') ? epc_deploy_client_ip() : '',
		$notes, time(),
	));
	$requestId = (int) $pdo->lastInsertId();

	if ($fromPool) {
		$prov = array('ok' => true, 'log' => array('claimed pre-provisioned DB from pool'), 'from_pool' => true);
	} else {
		$prov = epc_portal_demo_provision_database($dbName, $dbUser, $dbPass);
	}
	if (empty($prov['ok'])) {
		$pdo->prepare('UPDATE `epc_portal_demo_requests` SET `status` = ? WHERE `id` = ?')->execute(array('failed', $requestId));
		$hint = (string) ($prov['hint'] ?? epc_portal_demo_provision_failure_hint($prov['log'] ?? array()));
		$poolReady = epc_portal_demo_pool_ready_count($pdo);
		if ($poolReady === 0 && epc_portal_demo_clp_password() === '') {
			$hint = 'Demo DB pool empty and CloudPanel password not configured (EPC_DEMO_CLP_PASS / config.demo-clp.php)';
		} elseif ($poolReady === 0) {
			$hint .= ' — demo pool empty; operator should run epc-demo-pool-seed.php';
		}
		error_log('epc_portal_demo_provision failed site_key=' . $siteKey . ' email=' . $email . ' hint=' . $hint . ' pool_ready=' . $poolReady);
		$userMsg = 'Could not create your demo sandbox right now.';
		if (strpos($hint, 'EPC_DEMO_CLP_PASS') !== false || strpos($hint, 'config.demo-clp') !== false) {
			$userMsg .= ' Our team has been notified — please email hello@ecomae.com and we will activate your demo shortly.';
		} else {
			$userMsg .= ' ' . $hint . ' Please retry in a few minutes or email hello@ecomae.com.';
		}
		return array('ok' => false, 'message' => $userMsg);
	}
	if (!empty($prov['db_name'])) {
		$dbName = (string) $prov['db_name'];
		$dbUser = $dbName;
	}
	if ($isErpOnly) {
		$clone = epc_portal_demo_clone_schema_erp_only($dbName, $dbUser, $dbPass);
	} else {
		$clone = epc_portal_demo_clone_schema($srcDb, $dbName, $dbUser, $dbPass);
	}
	if (empty($clone['ok'])) {
		$pdo->prepare('UPDATE `epc_portal_demo_requests` SET `status` = ? WHERE `id` = ?')->execute(array('failed', $requestId));
		return array(
			'ok' => false,
			'message' => 'Schema clone failed — ' . (string) ($clone['message'] ?? 'could not copy storefront tables from template'),
		);
	}

	$countryName = $countryNames[$countryCode];
	$introJson = $isErpOnly ? array(
		'demo_erp_only' => 1,
		'commerce_enabled' => 0,
		'storefront_package' => 'none',
		'access_mode' => 'erp_only',
		'contact_phone' => $phone,
		'admin_cp_email' => $email,
		'country' => $countryName,
		'country_code' => $countryCode,
	) : array(
		'contact_phone' => $phone,
		'admin_cp_email' => $email,
		'country' => $countryName,
		'country_code' => $countryCode,
	);

	$save = epc_portal_save_tenant($pdo, array(
		'site_key' => $siteKey,
		'hostname' => 'www.ecomae.com',
		'industry_code' => $registryIndustry,
		'status' => 'live',
		'trade_name' => $tradeName,
		'hub_name' => $tradeName,
		'from_email' => $email,
		'db_name' => $dbName,
		'db_user' => $dbUser,
		'db_password' => $dbPass,
		'hosted_on' => 'platform',
		'erp_only_shared' => 0,
		'intro_json' => $introJson,
		'notes' => $isErpOnly ? 'AI demo sandbox — ERP-only (no storefront)' : 'AI demo sandbox — auto provisioned',
	));
	if (empty($save['ok'])) {
		$pdo->prepare('UPDATE `epc_portal_demo_requests` SET `status` = ? WHERE `id` = ?')->execute(array('failed', $requestId));
		return array('ok' => false, 'message' => 'Tenant registry failed: ' . ($save['message'] ?? ''));
	}

	$pdo->prepare(
		'UPDATE `epc_portal_tenants` SET
		 `is_demo` = 1, `demo_expires_at` = ?, `demo_contact_email` = ?, `demo_contact_phone` = ?, `demo_extended_count` = 0,
		 `operator_temp_password` = ?, `is_active` = 1
		 WHERE `site_key` = ?'
	)->execute(array($expiresAt, $email, $phone, $tempPass, $siteKey));

	$tenantPdo = epc_portal_demo_tenant_pdo(array(
		'db_name' => $dbName, 'db_user' => $dbUser, 'db_password' => $dbPass,
	));
	if ($tenantPdo instanceof PDO) {
		epc_portal_demo_create_cp_user($tenantPdo, $email, $tempPass, $name);
	}
	if ($isErpOnly) {
		epc_portal_demo_push_tenant_settings_erp_only($pdo, $siteKey, $preset, $tradeName, $email, $phone);
	} else {
		epc_portal_demo_push_tenant_settings($pdo, $siteKey, $preset, $tradeName, $email);
		if ($industry === 'auto_parts' && $tenantPdo instanceof PDO) {
			epc_portal_demo_rewrite_tenant_content_urls($tenantPdo, $siteKey);
		}
	}

	$pdo->prepare(
		'UPDATE `epc_portal_demo_requests` SET `status` = \'provisioned\', `provisioned_at` = ? WHERE `id` = ?'
	)->execute(array(time(), $requestId));

	$profileFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_country_profile.php';
	if (is_readable($profileFile)) {
		require_once $profileFile;
		epc_tenant_apply_country_profile($siteKey, $countryCode, $pdo);
	}

	require_once __DIR__ . '/epc_portal_tenant_intro.php';
	$tenantRow = epc_portal_tenant_get($pdo, $siteKey);
	$urls = epc_portal_demo_urls($siteKey, is_array($tenantRow) ? $tenantRow : null);
	$result = array(
		'ok' => true,
		'message' => $isErpOnly
			? 'ERP-only demo provisioned — check your email for CP login.'
			: 'Demo provisioned — check your email for login details.',
		'site_key' => $siteKey,
		'email' => $email,
		'temp_password' => $tempPass,
		'company' => $tradeName,
		'industry' => $industry,
		'industry_label' => $preset['label'],
		'demo_erp_only' => $isErpOnly ? 1 : 0,
		'expires_at' => $expiresAt,
		'urls' => $urls,
	);
	epc_portal_demo_send_email($result);
	if ($fromPool || epc_portal_demo_pool_ready_count($pdo) < 3) {
		@epc_portal_demo_pool_replenish($pdo, 3);
	}
	return $result;
}

function epc_portal_demo_list(PDO $pdo): array
{
	epc_portal_demo_ensure_schema($pdo);
	$rows = $pdo->query(
		'SELECT * FROM `epc_portal_tenants` WHERE `is_demo` = 1 ORDER BY `demo_expires_at` ASC, `created_at` DESC'
	)->fetchAll(PDO::FETCH_ASSOC);
	$now = time();
	foreach ($rows as &$r) {
		$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($r['site_key'] ?? '')));
		$exp = (int) ($r['demo_expires_at'] ?? 0);
		$r['days_left'] = $exp > 0 ? max(0, (int) ceil(($exp - $now) / 86400)) : 0;
		$r['expired'] = $exp > 0 && $exp < $now;
		$r['suspended'] = array_key_exists('is_active', $r) && (int) $r['is_active'] === 0;
		$r['admin_email'] = trim((string) ($r['demo_contact_email'] ?? ''));
		$r['stored_password'] = trim((string) ($r['operator_temp_password'] ?? ''));
		$r['created_at_ts'] = (int) ($r['created_at'] ?? 0);
		$r['demo_hostname'] = $key !== '' ? ('www.ecomae.com/demo/' . $key) : '';
		$r['urls'] = epc_portal_demo_urls($key, $r);
		$r['urls']['cp_autologin'] = epc_portal_demo_cp_autologin_url($key);
		$r['urls']['cp_login'] = epc_portal_demo_cp_login_url($key);
	}
	unset($r);
	return $rows;
}

function epc_portal_demo_get_by_email(PDO $pdo, string $email): ?array
{
	$email = strtolower(trim($email));
	if ($email === '') {
		return null;
	}
	epc_portal_demo_ensure_schema($pdo);
	$st = $pdo->prepare(
		'SELECT * FROM `epc_portal_tenants` WHERE `is_demo` = 1 AND `demo_contact_email` = ? ORDER BY `created_at` DESC LIMIT 1'
	);
	$st->execute(array($email));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	$row['urls'] = epc_portal_demo_urls((string) $row['site_key']);
	$row['days_left'] = max(0, (int) ceil(((int) ($row['demo_expires_at'] ?? 0) - time()) / 86400));
	return $row;
}

function epc_portal_demo_extend(PDO $pdo, string $siteKey, int $days = 3): array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? AND `is_demo` = 1 LIMIT 1');
	$st->execute(array($key));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'message' => 'Demo tenant not found');
	}
	$base = max(time(), (int) ($row['demo_expires_at'] ?? time()));
	$newExp = $base + ($days * 86400);
	$pdo->prepare(
		'UPDATE `epc_portal_tenants` SET `demo_expires_at` = ?, `demo_extended_count` = `demo_extended_count` + 1, `updated_at` = ? WHERE `site_key` = ?'
	)->execute(array($newExp, time(), $key));
	return array('ok' => true, 'message' => 'Extended +' . $days . ' days until ' . date('Y-m-d H:i', $newExp), 'expires_at' => $newExp);
}

function epc_portal_demo_convert(PDO $pdo, string $siteKey): array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? AND `is_demo` = 1 LIMIT 1');
	$st->execute(array($key));
	if (!$st->fetch(PDO::FETCH_ASSOC)) {
		return array('ok' => false, 'message' => 'Demo tenant not found');
	}
	$pdo->prepare(
		'UPDATE `epc_portal_tenants` SET `is_demo` = 0, `demo_expires_at` = 0, `status` = \'dns_pending\', `updated_at` = ? WHERE `site_key` = ?'
	)->execute(array(time(), $key));
	return array('ok' => true, 'message' => 'Converted to live tenant draft — assign client domain in Tenant hub');
}

function epc_portal_demo_drop_database(array $row): array
{
	$db = trim((string) ($row['db_name'] ?? ''));
	$user = trim((string) ($row['db_user'] ?? ''));
	if ($db === '' || in_array($db, array('docpart', 'ecomae', 'epartscart'), true)) {
		return array('ok' => false, 'message' => 'Refusing to drop protected database');
	}
	require_once __DIR__ . '/epc_cloudpanel_helpers.php';
	$r = epc_clp_run('db:delete --databaseName=' . escapeshellarg($db));
	$log = array($r['output']);
	if ($r['code'] === 0) {
		return array('ok' => true, 'log' => $log);
	}
	$dbEsc = str_replace('`', '', $db);
	$userEsc = str_replace("'", "''", $user);
	$sql = "DROP DATABASE IF EXISTS `{$dbEsc}`;";
	if ($userEsc !== '' && $userEsc !== $dbEsc) {
		$sql .= " DROP USER IF EXISTS '{$userEsc}'@'localhost';";
	}
	$sql .= ' FLUSH PRIVILEGES;';
	foreach (array('mysql -e ' . escapeshellarg($sql), 'sudo mysql -e ' . escapeshellarg($sql)) as $cmd) {
		$out = epc_clp_run_cmd($cmd);
		$log[] = $out['output'];
		if ($out['code'] === 0) {
			return array('ok' => true, 'log' => $log);
		}
	}
	try {
		$platformPdo = epc_portal_platform_pdo();
		if ($platformPdo instanceof PDO) {
			$platformPdo->exec("DROP DATABASE IF EXISTS `{$dbEsc}`");
			if ($userEsc !== '') {
				try {
					$platformPdo->exec("DROP USER IF EXISTS '{$userEsc}'@'localhost'");
				} catch (Exception $e) {
				}
			}
			return array('ok' => true, 'log' => array_merge($log, array('dropped via platform PDO')));
		}
	} catch (Exception $e) {
		$log[] = $e->getMessage();
	}
	return array('ok' => false, 'log' => $log);
}

function epc_portal_demo_force_delete(PDO $pdo, string $siteKey): array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? AND `is_demo` = 1 LIMIT 1');
	$st->execute(array($key));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'message' => 'Demo tenant not found');
	}
	$drop = epc_portal_demo_drop_database($row);
	$pdo->prepare('DELETE FROM `epc_portal_tenants` WHERE `site_key` = ?')->execute(array($key));
	$pdo->prepare('UPDATE `epc_portal_demo_requests` SET `status` = \'deleted\' WHERE `site_key` = ?')->execute(array($key));
	return array(
		'ok' => true,
		'message' => 'Demo deleted: ' . $key . ($drop['ok'] ? ' (DB dropped)' : ' (DB drop: ' . ($drop['log'] ?? 'skipped') . ')'),
	);
}

function epc_portal_demo_expire_cron(PDO $pdo, bool $sendReminders = true): array
{
	epc_portal_demo_ensure_schema($pdo);
	$now = time();
	$report = array('deleted' => array(), 'reminded' => array(), 'skipped_extended' => 0, 'errors' => array());
	$rows = $pdo->query(
		'SELECT * FROM `epc_portal_tenants` WHERE `is_demo` = 1 AND `demo_expires_at` > 0 AND `demo_expires_at` < ' . (int) $now
	)->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as $row) {
		$del = epc_portal_demo_force_delete($pdo, (string) $row['site_key']);
		if (!empty($del['ok'])) {
			$report['deleted'][] = (string) $row['site_key'];
		} else {
			$report['errors'][] = (string) ($del['message'] ?? 'delete failed');
		}
	}
	if ($sendReminders) {
		$tomorrow = $now + 86400;
		$rem = $pdo->query(
			'SELECT * FROM `epc_portal_tenants` WHERE `is_demo` = 1 AND `demo_expires_at` > ' . (int) $now
			. ' AND `demo_expires_at` <= ' . (int) $tomorrow
		)->fetchAll(PDO::FETCH_ASSOC);
		foreach ($rem as $row) {
			$email = trim((string) ($row['demo_contact_email'] ?? ''));
			if ($email === '') {
				continue;
			}
			$urls = epc_portal_demo_urls((string) $row['site_key']);
			@mail(
				$email,
				'ECOM AE demo expires tomorrow',
				"Your sandbox demo expires tomorrow.\n\nStorefront: {$urls['storefront']}\n\nContact hello@ecomae.com to convert to live rental.\n",
				'From: hello@ecomae.com'
			);
			$report['reminded'][] = (string) $row['site_key'];
		}
	}
	return $report;
}

/**
 * Storefront bootstrap: /demo/{site_key}/ → tenant DB on www.ecomae.com.
 */
function epc_portal_demo_parse_storefront_path(?string $uri = null): ?array
{
	if ($uri === null) {
		$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	}
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return null;
	}
	if (!preg_match('#^/demo/([a-z0-9_]+)(?:/|$)#', $path, $m)) {
		return null;
	}
	$siteKey = $m[1];
	$rest = preg_replace('#^/demo/' . preg_quote($siteKey, '#') . '#', '', $path);
	if ($rest === '' || $rest === false) {
		$rest = '/';
	}
	return array('site_key' => $siteKey, 'sub_path' => $rest);
}

function epc_portal_demo_try_bootstrap_storefront($DP_Config): bool
{
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return false;
	}
	$parsed = epc_portal_demo_parse_storefront_path();
	if ($parsed === null) {
		return false;
	}
	$pdo = epc_portal_platform_pdo();
	if (!$pdo instanceof PDO) {
		return false;
	}
	epc_portal_demo_ensure_schema($pdo);
	$st = $pdo->prepare(
		'SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? AND `is_demo` = 1 AND `status` = \'live\' LIMIT 1'
	);
	$st->execute(array($parsed['site_key']));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		http_response_code(404);
		header('Content-Type: text/html; charset=utf-8');
		echo '<!DOCTYPE html><html><body style="font-family:sans-serif;padding:24px"><h1>Demo not found</h1><p><a href="https://www.ecomae.com/platform/demo">Request a demo</a></p></body></html>';
		exit;
	}
	if (epc_portal_demo_row_is_erp_only($row)) {
		$cpUrl = 'https://www.ecomae.com' . epc_portal_demo_cp_path_prefix() . preg_replace('/[^a-z0-9_]/', '', (string) $parsed['site_key']) . '/';
		http_response_code(503);
		header('Content-Type: text/html; charset=utf-8');
		echo '<!DOCTYPE html><html><body style="font-family:sans-serif;padding:24px;background:#0f172a;color:#e2e8f0">';
		echo '<h1>ERP-only demo — no storefront</h1>';
		echo '<p>This sandbox includes ERP, CRM, and finance modules only. Use your demo control panel to sign in.</p>';
		echo '<p><a href="' . htmlspecialchars($cpUrl, ENT_QUOTES) . '" style="color:#38bdf8">Open demo CP login →</a></p>';
		echo '<p><a href="https://www.ecomae.com/platform/demo" style="color:#94a3b8">Request another demo</a></p></body></html>';
		exit;
	}
	require_once __DIR__ . '/epc_portal_tenant_control.php';
	if (!epc_portal_tenant_control_row_is_active($row)) {
		epc_portal_tenant_control_render_blocked(
			'Demo temporarily unavailable',
			'This demo sandbox has been disabled by the platform operator.',
			503
		);
	}
	$exp = (int) ($row['demo_expires_at'] ?? 0);
	if ($exp > 0 && $exp < time()) {
		http_response_code(410);
		header('Content-Type: text/html; charset=utf-8');
		echo '<!DOCTYPE html><html><body style="font-family:sans-serif;padding:24px"><h1>Demo expired</h1><p><a href="https://www.ecomae.com/platform/demo">Request a new demo</a></p></body></html>';
		exit;
	}
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedFile)) {
		require_once $sharedFile;
		if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
			epc_portal_shared_erp_clear_tenant_cookie();
		}
	}
	$GLOBALS['epc_demo_storefront_context'] = true;
	$GLOBALS['epc_demo_storefront_site_key'] = $parsed['site_key'];
	$GLOBALS['epc_demo_storefront_industry'] = (string) ($row['industry_code'] ?? 'auto_parts');
	$GLOBALS['epc_demo_storefront_tenant_row'] = $row;
	if (is_object($DP_Config)) {
		$DP_Config->db = (string) $row['db_name'];
		$DP_Config->user = (string) $row['db_user'];
		$DP_Config->password = (string) $row['db_password'];
		$DP_Config->epc_portal_industry = (string) ($row['industry_code'] ?? 'auto_parts');
		$DP_Config->domain_path = 'https://www.ecomae.com/demo/' . $parsed['site_key'] . '/';
	}
	$query = parse_url($_SERVER['REQUEST_URI'] ?? '', PHP_URL_QUERY);
	$sub = $parsed['sub_path'];
	if ($sub === '' || $sub === '/') {
		$sub = '/';
	}
	$_SERVER['REQUEST_URI'] = $sub . ($query !== null && $query !== '' ? '?' . $query : '');
	return true;
}

/**
 * Demo tenant CP: /cp/demo/{site_key}/ → isolated tenant DB + standard shop CP (not Platform/Client ERP).
 */
function epc_portal_demo_parse_cp_path(?string $uri = null): ?array
{
	if ($uri === null) {
		$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	}
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return null;
	}
	$prefix = epc_portal_demo_cp_path_prefix();
	if (strpos($path, $prefix) !== 0) {
		return null;
	}
	$rest = substr($path, strlen($prefix));
	$segments = explode('/', trim($rest, '/'));
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($segments[0] ?? '')));
	if ($siteKey === '') {
		return null;
	}
	$subPath = implode('/', array_slice($segments, 1));
	return array(
		'site_key' => $siteKey,
		'sub_path' => $subPath,
		'is_login_root' => $subPath === '',
	);
}

function epc_portal_demo_apply_cp_config($DP_Config, array $row, string $siteKey): void
{
	if (!is_object($DP_Config)) {
		return;
	}
	$DP_Config->db = (string) $row['db_name'];
	$DP_Config->user = (string) $row['db_user'];
	$DP_Config->password = (string) $row['db_password'];
	$DP_Config->epc_portal_industry = (string) ($row['industry_code'] ?? 'auto_parts');
	$keySafe = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if (epc_portal_demo_row_is_erp_only($row)) {
		$DP_Config->domain_path = 'https://www.ecomae.com' . epc_portal_demo_cp_path_prefix() . $keySafe . '/';
	} else {
		$DP_Config->domain_path = 'https://www.ecomae.com/demo/' . $keySafe . '/';
	}
	$DP_Config->epc_demo_cp_login_path = epc_portal_demo_cp_path_prefix() . $keySafe . '/';
	$DP_Config->epc_demo_cp_site_key = $siteKey;
	$DP_Config->epc_demo_cp_trade_name = (string) ($row['trade_name'] ?? '');
}

/**
 * Friendly HTML fatal for demo CP (never bare "No DB connect" on /cp/demo/{key}/).
 */
function epc_portal_demo_cp_render_fatal(string $title, string $message, int $httpCode = 503, ?string $siteKey = null): void
{
	$key = $siteKey !== null && $siteKey !== '' ? $siteKey : '';
	if ($key === '' && function_exists('epc_portal_demo_cp_site_key')) {
		$key = epc_portal_demo_cp_site_key();
	}
	if ($key === '' && function_exists('epc_portal_demo_parse_cp_path')) {
		$parsed = epc_portal_demo_parse_cp_path(epc_portal_demo_cp_original_uri() !== ''
			? epc_portal_demo_cp_original_uri()
			: null);
		if (is_array($parsed)) {
			$key = (string) ($parsed['site_key'] ?? '');
		}
	}
	$trade = '';
	if (!empty($GLOBALS['epc_demo_cp_tenant_row']['trade_name'])) {
		$trade = (string) $GLOBALS['epc_demo_cp_tenant_row']['trade_name'];
	}
	http_response_code($httpCode);
	header('Content-Type: text/html; charset=utf-8');
	echo '<!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">';
	echo '<title>' . htmlspecialchars($title, ENT_QUOTES, 'UTF-8') . '</title>';
	echo '<style>body{font-family:Inter,Segoe UI,sans-serif;background:#0f172a;color:#e2e8f0;margin:0;padding:32px 20px}';
	echo '.card{max-width:520px;margin:0 auto;background:#1e293b;border:1px solid #334155;border-radius:12px;padding:24px}';
	echo 'h1{font-size:1.35rem;margin:0 0 8px;color:#fff}.badge{display:inline-block;background:#2563eb;color:#fff;font-size:12px;padding:4px 10px;border-radius:999px;margin-bottom:12px}';
	echo 'p{line-height:1.55;color:#94a3b8;margin:0 0 16px}a{color:#38bdf8}</style></head><body><div class="card">';
	echo '<span class="badge">Demo CP</span><h1>' . htmlspecialchars($title, ENT_QUOTES, 'UTF-8') . '</h1>';
	if ($trade !== '') {
		echo '<p><strong style="color:#cbd5e1">' . htmlspecialchars($trade, ENT_QUOTES, 'UTF-8') . '</strong></p>';
	}
	echo '<p>' . htmlspecialchars($message, ENT_QUOTES, 'UTF-8') . '</p>';
	if ($key !== '') {
		echo '<p style="font-size:13px">Sandbox key: <code style="color:#e2e8f0">' . htmlspecialchars($key, ENT_QUOTES, 'UTF-8') . '</code></p>';
	}
	echo '<p><a href="https://www.ecomae.com/platform/demo">Request a new demo</a> · <a href="mailto:hello@ecomae.com">hello@ecomae.com</a></p>';
	echo '</div></body></html>';
	exit;
}

/** @param Throwable|string|null $cause */
function epc_portal_demo_cp_exit_db_error($cause = null, ?array $tenantRow = null): void
{
	$inCtx = !empty($GLOBALS['epc_demo_cp_context']);
	$siteKey = '';
	if (is_array($tenantRow) && !empty($tenantRow['site_key'])) {
		$siteKey = (string) $tenantRow['site_key'];
	}
	if (!$inCtx && $siteKey === '') {
		exit('No DB connect');
	}
	$detail = '';
	if ($cause instanceof Throwable) {
		$detail = $cause->getMessage();
	} elseif (is_string($cause) && $cause !== '') {
		$detail = $cause;
	}
	$msg = 'The sandbox database is not reachable. If this persists, contact hello@ecomae.com to reprovision the demo.';
	if ($detail !== '' && stripos($detail, 'access denied') !== false) {
		$msg = 'Database credentials for this sandbox are invalid or expired. Contact hello@ecomae.com to repair the demo.';
	}
	epc_portal_demo_cp_render_fatal('Demo CP database unavailable', $msg, 503, $siteKey !== '' ? $siteKey : null);
}

function epc_portal_demo_cp_exit_schema_error(): void
{
	if (empty($GLOBALS['epc_demo_cp_context'])) {
		exit('No DB connect');
	}
	epc_portal_demo_cp_render_fatal(
		'Demo CP setup incomplete',
		'The sandbox database is connected but missing control-panel tables. Run demo schema repair from Super CP or contact hello@ecomae.com.',
		503
	);
}

function epc_portal_demo_try_bootstrap_cp($DP_Config): bool
{
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return false;
	}
	if (!function_exists('epc_portal_is_cp_request') || !epc_portal_is_cp_request()) {
		return false;
	}
	$parsed = epc_portal_demo_parse_cp_path();
	if ($parsed === null) {
		return false;
	}
	$platformPdo = epc_portal_platform_pdo();
	if ($platformPdo instanceof PDO) {
		require_once __DIR__ . '/epc_portal_tenant_control.php';
		$regRow = epc_portal_tenant_control_get_row($platformPdo, $parsed['site_key']);
		if ($regRow !== null && !empty($regRow['is_demo']) && !epc_portal_tenant_control_row_is_active($regRow)) {
			epc_portal_tenant_control_render_blocked(
				'Demo CP temporarily unavailable',
				'This demo sandbox has been disabled by the platform operator.',
				503
			);
		}
	}
	$row = epc_portal_demo_load_live_row($parsed['site_key']);
	if ($row === null) {
		http_response_code(404);
		header('Content-Type: text/html; charset=utf-8');
		echo '<!DOCTYPE html><html><head><title>Demo CP not found</title></head><body style="font-family:sans-serif;padding:24px">';
		echo '<h1>Demo CP not found</h1><p>No active sandbox is registered for <code>' . htmlspecialchars($parsed['site_key'], ENT_QUOTES, 'UTF-8') . '</code>.</p>';
		echo '<p><a href="https://www.ecomae.com/platform/demo">Request a demo</a></p></body></html>';
		exit;
	}
	$GLOBALS['epc_demo_cp_original_uri'] = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	$hadPlatformOpSession = epc_portal_platform_operator_session_active();
	if (function_exists('epc_platform_erp_clear_cookie')) {
		require_once __DIR__ . '/epc_platform_erp_router.php';
		epc_platform_erp_clear_cookie();
	}
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedFile)) {
		require_once $sharedFile;
		if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
			epc_portal_shared_erp_clear_tenant_cookie();
		}
	}
	if ($hadPlatformOpSession) {
		epc_portal_demo_clear_cp_login_cookies();
	}
	$GLOBALS['epc_demo_cp_context'] = true;
	$GLOBALS['epc_demo_cp_site_key'] = $parsed['site_key'];
	$GLOBALS['epc_demo_cp_tenant_row'] = $row;
	epc_portal_demo_apply_cp_config($DP_Config, $row, $parsed['site_key']);
	epc_portal_demo_cp_set_scope_cookie($parsed['site_key']);
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$tenantPdo instanceof PDO) {
		epc_portal_demo_cp_exit_db_error(null, $row);
	}
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	$query = parse_url($_SERVER['REQUEST_URI'] ?? '', PHP_URL_QUERY);
	if (!$parsed['is_login_root']) {
		$newPath = '/' . $backend . '/';
		if ($parsed['sub_path'] !== '') {
			$newPath .= ltrim($parsed['sub_path'], '/');
		}
		$_SERVER['REQUEST_URI'] = $newPath . ($query !== null && $query !== '' ? '?' . $query : '');
	} else {
		$_SERVER['REQUEST_URI'] = '/' . $backend . '/' . ($query !== null && $query !== '' ? '?' . $query : '');
	}
	return true;
}

function epc_portal_demo_wizard_reply(string $message, array $context = array()): array
{
	$openaiKey = getenv('OPENAI_API_KEY');
	if ($openaiKey === false || trim($openaiKey) === '') {
		return array('reply' => $message, 'ai' => false);
	}
	$payload = json_encode(array(
		'model' => 'gpt-4o-mini',
		'messages' => array(
			array('role' => 'system', 'content' => 'You are the ECOM AE demo assistant. Be concise. Phase 1-2 industries: auto_parts, fashion only. Never mention Super CP URLs to prospects.'),
			array('role' => 'user', 'content' => $message),
		),
		'max_tokens' => 120,
	));
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => 'POST',
			'header' => "Content-Type: application/json\r\nAuthorization: Bearer {$openaiKey}\r\n",
			'content' => $payload,
			'timeout' => 8,
			'ignore_errors' => true,
		),
	));
	$raw = @file_get_contents('https://api.openai.com/v1/chat/completions', false, $ctx);
	if ($raw === false) {
		return array('reply' => $message, 'ai' => false);
	}
	$data = json_decode($raw, true);
	$reply = $data['choices'][0]['message']['content'] ?? '';
	if ($reply === '') {
		return array('reply' => $message, 'ai' => false);
	}
	return array('reply' => trim($reply), 'ai' => true);
}
