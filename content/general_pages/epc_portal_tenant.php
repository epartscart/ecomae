<?php
/**
 * ecomae DNS-only tenants — one platform docroot, client domains via GoDaddy A records.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_portal.php';

function epc_portal_platform_ip(): string
{
	$ip = getenv('ECOMAE_PLATFORM_IP');
	if ($ip !== false && $ip !== '') {
		return trim($ip);
	}
	return '31.97.216.247';
}

function epc_portal_platform_hostnames(): array
{
	return array(
		'www.ecomae.com',
		'ecomae.com',
		'cp.ecomae.com',
	);
}

function epc_portal_is_platform_hostname(string $host = null): bool
{
	if ($host === null) {
		$host = epc_portal_host();
	}
	$host = strtolower(trim($host));
	foreach (epc_portal_platform_hostnames() as $platformHost) {
		if ($host === $platformHost) {
			return true;
		}
	}
	// Industry wildcard subdomains (*.ecomae.com) are platform-managed
	if (function_exists('epc_is_industry_subdomain') && epc_is_industry_subdomain()) {
		return true;
	}
	if (preg_match('/^[a-z0-9][a-z0-9_-]*\.ecomae\.com$/', $host)
		&& !in_array($host, array('www.ecomae.com', 'cp.ecomae.com'), true)) {
		return true;
	}
	return false;
}

function epc_portal_is_epartscart_hostname(string $host = null): bool
{
	if ($host === null) {
		$host = epc_portal_host();
	}
	$host = strtolower(trim($host));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	$host = preg_replace('/^www\./', '', $host);
	return $host === 'epartscart.com';
}

function epc_portal_tenant_templates(): array
{
	return array(
		'epartscart' => array(
			'label' => 'eParts Cart (auto parts)',
			'hostname' => 'www.epartscart.com',
			'industry' => 'auto_parts',
			'trade_name' => 'eParts Cart',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'epartscart@gmail.com',
			'storefront_package' => 'automotive_spareparts_pro',
			'theme_template' => 'classic',
			'onboard_notes' => 'Preset automotive_spareparts_pro: piston homepage, animated SVG logo (not hub/tenant brand). Colours via theme JSON in Industry Settings.',
		),
		'taxofinca' => array(
			'label' => 'Taxofinca (tax advisory)',
			'hostname' => 'www.taxofinca.com',
			'industry' => 'tax_advisory',
			'trade_name' => 'Taxofin',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'info@taxofinca.com',
		),
		'electronicae' => array(
			'label' => 'Electronicae (electronics)',
			'hostname' => 'www.electronicae.com',
			'industry' => 'electronics',
			'trade_name' => 'Electronicae',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'hello@electronicae.com',
		),
		'stylenlook' => array(
			'label' => 'Stylenlook (fashion)',
			'hostname' => 'www.stylenlook.com',
			'industry' => 'fashion',
			'trade_name' => 'Stylenlook',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'hello@stylenlook.com',
		),
		'thejewellerytrend' => array(
			'label' => 'The Jewellery Trend (jewellery)',
			'hostname' => 'www.thejewellerytrend.com',
			'industry' => 'jewellery',
			'trade_name' => 'The Jewellery Trend',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'hello@thejewellerytrend.com',
		),
		'erp_only_demo' => array(
			'label' => 'ERP only — shared on ecomae.com',
			'hostname' => 'www.ecomae.com',
			'industry' => 'erp_standalone',
			'trade_name' => 'Client ERP',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'admin@client.com',
			'access_mode' => 'erp_only',
			'hosted_on' => 'platform',
			'erp_only_shared' => 1,
			'onboard_notes' => 'Shared ERP on www.ecomae.com/cp/ — no client domain or DNS. Separate MySQL DB + CP users per company.',
		),
		'asap' => array(
			'label' => 'ASAP (ERP only — shared on ecomae.com)',
			'hostname' => 'www.ecomae.com',
			'industry' => 'erp_standalone',
			'trade_name' => 'ASAP',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'admin@asap-ae.com',
			'access_mode' => 'erp_only',
			'hosted_on' => 'platform',
			'erp_only_shared' => 1,
			'onboard_notes' => 'Shared ERP on www.ecomae.com/cp/ — no client domain. Full ERP modules. Login email maps to ASAP tenant DB.',
		),
	);
}

/**
 * Optional host-specific runtime DB overrides for emergency cutover.
 * File format: $epc_tenant_host_db['www.taxofinca.com'] = ['db'=>'ecomae','user'=>'ecomae','password'=>'...'];
 */
function epc_portal_runtime_host_db(string $host): ?array
{
	$host = strtolower(trim($host));
	if ($host === '') {
		return null;
	}
	$candidates = array();
	$docroot = isset($_SERVER['DOCUMENT_ROOT']) ? rtrim((string) $_SERVER['DOCUMENT_ROOT'], '/') : '';
	if ($docroot !== '') {
		$candidates[] = $docroot . '/config.tenant-host-db.php';
	}
	$candidates[] = dirname(__DIR__, 2) . '/config.tenant-host-db.php';
	foreach ($candidates as $path) {
		if (!is_file($path)) {
			continue;
		}
		$epc_tenant_host_db = null;
		include $path;
		if (!is_array($epc_tenant_host_db)) {
			continue;
		}
		if (!isset($epc_tenant_host_db[$host]) || !is_array($epc_tenant_host_db[$host])) {
			continue;
		}
		$row = $epc_tenant_host_db[$host];
		$db = strtolower(trim((string) ($row['db'] ?? '')));
		$user = trim((string) ($row['user'] ?? ''));
		$pass = (string) ($row['password'] ?? '');
		if ($db === '' || $user === '' || $pass === '') {
			continue;
		}
		return array('db' => $db, 'user' => $user, 'password' => $pass);
	}
	return null;
}

/**
 * Resolve shared tenant storefront DB credentials (docpart).
 * Prefers legacy epartscart config.local, then config.php — never platform config.local (ecomae).
 */
function epc_portal_resolve_tenant_db_credentials(): array
{
	static $resolved = null;
	if ($resolved !== null) {
		return $resolved;
	}
	$dbPass = '';
	$dbUser = 'docpart';
	$dbName = 'docpart';
	$platformDocroot = $_SERVER['DOCUMENT_ROOT'] ?? '';
	$legacyDocroot = '/home/epartscart/htdocs/www.epartscart.com';
	$candidates = array();
	if ($platformDocroot !== '') {
		$candidates[] = rtrim($platformDocroot, '/') . '/config.tenant-db.php';
	}
	$candidates[] = $legacyDocroot . '/config.local.php';
	$candidates[] = $legacyDocroot . '/config.php';
	if ($platformDocroot !== '') {
		$candidates[] = rtrim($platformDocroot, '/') . '/config.php';
	}
	$candidates[] = dirname(__DIR__, 2) . '/config.php';
	foreach ($candidates as $path) {
		if (!is_file($path)) {
			continue;
		}
		if (substr($path, -20) === 'config.tenant-db.php' || substr($path, -13) === 'config.local.php') {
			$epc_config_local = null;
			$epc_tenant_db = null;
			include $path;
			$src = null;
			if (isset($epc_tenant_db) && is_array($epc_tenant_db)) {
				$src = $epc_tenant_db;
			} elseif (isset($epc_config_local) && is_array($epc_config_local)) {
				$src = $epc_config_local;
			}
			if (is_array($src)) {
				if (!empty($src['password'])) {
					$dbPass = (string) $src['password'];
				}
				if (!empty($src['user'])) {
					$dbUser = (string) $src['user'];
				}
				if (!empty($src['db'])) {
					$dbName = (string) $src['db'];
				}
			}
			// Client storefronts must stay on shared docpart DB, never platform ecomae DB.
			if ($dbName !== 'docpart') {
				$dbPass = '';
				$dbUser = 'docpart';
				$dbName = 'docpart';
				continue;
			}
			if ($dbPass !== '') {
				break;
			}
			continue;
		}
		if (!class_exists('DP_Config', false)) {
			include_once $path;
		}
		if (!class_exists('DP_Config', false)) {
			continue;
		}
		$cfg = new DP_Config();
		$dbPass = (string) $cfg->password;
		$dbUser = (string) $cfg->user;
		$dbName = (string) $cfg->db;
		if ($dbName !== 'docpart') {
			$dbPass = '';
			$dbUser = 'docpart';
			$dbName = 'docpart';
			continue;
		}
		break;
	}
	if ($dbPass === '' && $dbName === 'docpart') {
		$dbUser = 'docpart';
	}
	$resolved = array('db' => $dbName, 'user' => $dbUser, 'password' => $dbPass);
	return $resolved;
}

/**
 * Apply tenant storefront DB credentials to DP_Config before any mysqli/PDO query on client hosts.
 */
function epc_portal_resolve_tenant_db($DP_Config): void
{
	if (!is_object($DP_Config) || !function_exists('epc_portal_is_client_hostname') || !epc_portal_is_client_hostname()) {
		return;
	}
	$host = epc_portal_host();
	$db = '';
	$user = '';
	$pass = '';
	$usesDedicated = false;
	$runtimeOverride = epc_portal_runtime_host_db($host);
	if (is_array($runtimeOverride)) {
		$db = (string) $runtimeOverride['db'];
		$user = (string) $runtimeOverride['user'];
		$pass = (string) $runtimeOverride['password'];
	}

	// Dedicated-DB tenants (1000+ scale path): use registry credentials, not shared docpart.
	if ($pass === '' && $host !== '' && function_exists('epc_portal_load_tenant_by_host')) {
		$tenant = epc_portal_load_tenant_by_host($host);
		if (
			$tenant !== null
			&& !empty($tenant['user'])
			&& isset($tenant['password'])
			&& (string) $tenant['password'] !== ''
			&& (
				!empty($tenant['dedicated_db'])
				|| (string) ($tenant['scale_policy'] ?? '') === 'dedicated_mysql'
				|| (
					(string) ($tenant['db'] ?? '') !== ''
					&& (string) ($tenant['db'] ?? '') !== 'docpart'
				)
			)
		) {
			$db = (string) $tenant['db'];
			$user = (string) $tenant['user'];
			$pass = (string) $tenant['password'];
			$usesDedicated = true;
		} elseif (
			$tenant !== null
			&& !empty($tenant['user'])
			&& isset($tenant['password'])
			&& (string) $tenant['password'] !== ''
			&& (string) ($tenant['db'] ?? '') === 'docpart'
		) {
			$db = (string) $tenant['db'];
			$user = (string) $tenant['user'];
			$pass = (string) $tenant['password'];
		}
	}

	// Legacy Model C storefronts share docpart — registry may hold stale ecomae operator creds.
	if ($pass === '' && !$usesDedicated) {
		$resolved = epc_portal_resolve_tenant_db_credentials();
		if ($resolved['password'] !== '') {
			$db = (string) $resolved['db'];
			$user = (string) $resolved['user'];
			$pass = (string) $resolved['password'];
		}
	}
	if (($pass === '' || $db === '' || $user === '') && !$usesDedicated) {
		$resolved = epc_portal_resolve_tenant_db_credentials();
		$db = $resolved['db'];
		$user = $resolved['user'];
		$pass = $resolved['password'];
	}
	if (property_exists($DP_Config, 'db')) {
		$DP_Config->db = $db;
	}
	if (property_exists($DP_Config, 'user')) {
		$DP_Config->user = $user;
	}
	if (property_exists($DP_Config, 'password')) {
		$DP_Config->password = $pass;
	}
	// Platform config.local may set host=127.0.0.1 — tenant storefronts must use localhost socket.
	if (property_exists($DP_Config, 'host')) {
		$DP_Config->host = 'localhost';
	}
}

/**
 * Reuse one PDO per tenant DB per request — avoids MySQL connection storms on client hostnames.
 *
 * @param array{db?:string,user?:string,password?:string}|null $credentials
 */
function epc_portal_tenant_storefront_pdo(?array $credentials = null): ?PDO
{
	if ($credentials === null) {
		$credentials = epc_portal_resolve_tenant_db_credentials();
	}
	$db = trim((string) ($credentials['db'] ?? ''));
	$user = trim((string) ($credentials['user'] ?? ''));
	$pass = (string) ($credentials['password'] ?? '');
	$host = trim((string) ($credentials['host'] ?? '127.0.0.1'));
	if ($host === '') {
		$host = '127.0.0.1';
	}
	if ($db === '' || $user === '' || $pass === '') {
		return null;
	}
	require_once __DIR__ . '/epc_tenant_pdo.php';
	[$pdo] = epc_tenant_pdo($host, $db, $user, $pass, array('timeout' => 2));
	return $pdo instanceof PDO ? $pdo : null;
}

function epc_portal_platform_pdo(): ?PDO
{
	static $pdo = null;
	static $failed = false;
	if ($failed) {
		return null;
	}
	if ($pdo instanceof PDO) {
		return $pdo;
	}
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		if (is_file($_SERVER['DOCUMENT_ROOT'] . '/config.local.php')) {
			$epc_config_local = null;
			require $_SERVER['DOCUMENT_ROOT'] . '/config.local.php';
			if (isset($epc_config_local) && is_array($epc_config_local)) {
				foreach ($epc_config_local as $key => $value) {
					if (property_exists($cfg, $key)) {
						$cfg->$key = $value;
					}
				}
			}
		}
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8;connect_timeout=2',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 2)
		);
	} catch (Exception $e) {
		$pdo = null;
		$failed = true;
	}
	return $pdo;
}

function epc_portal_tenant_statuses(): array
{
	return array(
		'draft' => 'Draft — not published',
		'dns_pending' => 'Awaiting GoDaddy DNS',
		'live' => 'Live on platform',
		'suspended' => 'Suspended',
	);
}

function epc_portal_tenant_is_shared_erp_row(array $row): bool
{
	if (!empty($row['erp_only_shared'])) {
		return true;
	}
	return (string) ($row['hosted_on'] ?? '') === 'platform';
}

/**
 * DB credentials for setup-all installers. Registry db_name may differ from runtime on client hosts
 * (e.g. epartscart registry=ecomae but www.epartscart.com config.php uses docpart).
 *
 * @return array{db:string,user:string,pass:string,registry_db:string,source:string}
 */
function epc_portal_tenant_setup_credentials(array $row): array
{
	$host = strtolower(trim((string) ($row['hostname'] ?? '')));
	$registryDb = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['db_name'] ?? '')));
	$registryUser = trim((string) ($row['db_user'] ?? ''));
	$registryPass = (string) ($row['db_password'] ?? '');

	if ($host !== '') {
		$runtimeOverride = epc_portal_runtime_host_db($host);
		if (is_array($runtimeOverride)) {
			return array(
				'db' => (string) $runtimeOverride['db'],
				'user' => (string) $runtimeOverride['user'],
				'pass' => (string) $runtimeOverride['password'],
				'registry_db' => $registryDb,
				'source' => 'runtime_host_db',
			);
		}
	}

	$dedicated = !empty($row['dedicated_db'])
		|| (string) ($row['scale_policy'] ?? '') === 'dedicated_mysql'
		|| (!empty($row['erp_only_shared']))
		|| ($registryDb !== '' && $registryDb !== 'docpart' && $registryPass !== '');

	if ($dedicated && $registryDb !== '' && $registryUser !== '' && $registryPass !== '') {
		return array(
			'db' => $registryDb,
			'user' => $registryUser,
			'pass' => $registryPass,
			'registry_db' => $registryDb,
			'source' => 'registry_dedicated',
		);
	}

	if ($host !== '' && function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname($host)) {
		$resolved = epc_portal_resolve_tenant_db_credentials();
		return array(
			'db' => (string) $resolved['db'],
			'user' => (string) $resolved['user'],
			'pass' => (string) $resolved['password'],
			'registry_db' => $registryDb,
			'source' => 'client_runtime',
		);
	}

	return array(
		'db' => $registryDb,
		'user' => $registryUser,
		'pass' => $registryPass,
		'registry_db' => $registryDb,
		'source' => 'registry',
	);
}

function epc_portal_tenant_row_to_profile(array $row): array
{
	$host = (string) $row['hostname'];
	$domainPath = 'https://' . $host . '/';
	return array(
		'tenant_id' => (int) $row['id'],
		'tenant_status' => (string) $row['status'],
		'site_key' => (string) $row['site_key'],
		'industry' => (string) $row['industry_code'],
		'domain_path' => $domainPath,
		'db' => (string) $row['db_name'],
		'user' => (string) $row['db_user'],
		'password' => (string) $row['db_password'],
		'dedicated_db' => (
			!empty($row['dedicated_db'])
			|| !empty($row['erp_only_shared'])
			|| (
				(string) ($row['db_name'] ?? '') !== ''
				&& (string) ($row['db_name'] ?? '') !== 'docpart'
			)
		) ? 1 : 0,
		'scale_policy' => (string) (
			($row['scale_policy'] ?? '') !== ''
				? $row['scale_policy']
				: (
					(
						!empty($row['dedicated_db'])
						|| !empty($row['erp_only_shared'])
						|| (
							(string) ($row['db_name'] ?? '') !== ''
							&& (string) ($row['db_name'] ?? '') !== 'docpart'
						)
					) ? 'dedicated_mysql' : 'shared_docpart'
				)
		),
		'trade_name' => (string) $row['trade_name'],
		'hub_name' => (string) $row['hub_name'],
		'from_email' => (string) $row['from_email'],
		'system_name' => 'e-world Commerce System',
		'tagline' => 'Designed by Electronic World Group',
	);
}

function epc_portal_load_tenant_by_host(string $host, ?PDO $pdo = null): ?array
{
	$host = strtolower(trim($host));
	if ($host === '' || epc_portal_is_platform_hostname($host)) {
		return null;
	}
	// eParts Cart uses shared docpart DB from config — skip ecomae registry PDO on every storefront hit.
	if (function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname($host)) {
		return null;
	}
	if ($pdo === null) {
		$pdo = epc_portal_platform_pdo();
	}
	if (!$pdo instanceof PDO) {
		return null;
	}
	require_once __DIR__ . '/epc_portal_db.php';
	epc_portal_db_ensure($pdo);
	$st = $pdo->prepare(
		'SELECT * FROM `epc_portal_tenants` WHERE `hostname` = ? AND `status` IN (\'dns_pending\', \'live\') LIMIT 1'
	);
	$st->execute(array($host));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	require_once __DIR__ . '/epc_portal_tenant_control.php';
	if (!epc_portal_tenant_control_row_is_active($row)) {
		return null;
	}
	return epc_portal_tenant_row_to_profile($row);
}

function epc_portal_tenant_registry_row(PDO $pdo, string $siteKey): ?array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($key === '') {
		return null;
	}
	require_once __DIR__ . '/epc_portal_db.php';
	epc_portal_db_ensure($pdo);
	$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
	$st->execute(array($key));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_portal_list_tenants(?PDO $pdo = null): array
{
	if ($pdo === null) {
		$pdo = epc_portal_platform_pdo();
	}
	if (!$pdo instanceof PDO) {
		return array();
	}
	require_once __DIR__ . '/epc_portal_db.php';
	epc_portal_db_ensure($pdo);
	return $pdo->query(
		'SELECT * FROM `epc_portal_tenants` ORDER BY `hostname` ASC'
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_portal_save_tenant(PDO $pdo, array $data): array
{
	require_once __DIR__ . '/epc_portal_db.php';
	epc_portal_db_ensure($pdo);

	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($data['site_key'] ?? '')));
	$hostname = strtolower(trim((string) ($data['hostname'] ?? '')));
	$erpOnlyShared = !empty($data['erp_only_shared']) || (string) ($data['hosted_on'] ?? '') === 'platform';
	if ($erpOnlyShared) {
		$hostname = 'www.ecomae.com';
		$data['hosted_on'] = 'platform';
		$data['erp_only_shared'] = 1;
	}

	$scalePolicy = strtolower(trim((string) ($data['scale_policy'] ?? '')));
	if ($scalePolicy === '') {
		$scalePolicy = !empty($data['dedicated_db']) || $erpOnlyShared ? 'dedicated_mysql' : 'shared_docpart';
	}
	if (!in_array($scalePolicy, array('dedicated_mysql', 'shared_docpart'), true)) {
		$scalePolicy = 'shared_docpart';
	}
	// ERP-only shared always gets a dedicated MySQL; commerce can opt into dedicated_mysql.
	$dedicatedDb = $erpOnlyShared
		|| $scalePolicy === 'dedicated_mysql'
		|| !empty($data['dedicated_db']);
	if ($dedicatedDb) {
		$scalePolicy = 'dedicated_mysql';
	}

	require_once __DIR__ . '/epc_blockchain_bos.php';
	$blockchainMode = epc_bc_bos_normalize_mode((string) ($data['blockchain_mode'] ?? 'anchor'));

	$industry = preg_replace('/[^a-z0-9_]/', '', (string) ($data['industry_code'] ?? 'auto_parts'));
	$status = (string) ($data['status'] ?? 'draft');
	$statuses = epc_portal_tenant_statuses();
	if (!isset($statuses[$status])) {
		$status = 'draft';
	}
	if ($key === '') {
		return array('ok' => false, 'message' => 'Site key is required');
	}
	if (!$erpOnlyShared && ($hostname === '' || strpos($hostname, '.') === false)) {
		return array('ok' => false, 'message' => 'Site key and full hostname are required');
	}
	if (!$erpOnlyShared && epc_portal_is_platform_hostname($hostname)) {
		return array('ok' => false, 'message' => 'Cannot register a platform hostname as tenant (use shared ERP flag for ecomae.com companies)');
	}

	$dbName = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($data['db_name'] ?? $key)));
	$dbUser = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($data['db_user'] ?? $dbName)));
	$dbPass = (string) ($data['db_password'] ?? '');

	if ($dedicatedDb && ($dbName === '' || $dbName === 'docpart')) {
		$dbName = $key;
		if ($dbUser === '' || $dbUser === 'docpart') {
			$dbUser = $key;
		}
	}

	if ($dedicatedDb && $dbName !== '' && $dbPass === '') {
		require_once __DIR__ . '/epc_portal_tenant_control.php';
		$dbPass = epc_portal_tenant_control_generate_password();
		if ($dbUser === '') {
			$dbUser = $dbName;
		}
		require_once __DIR__ . '/epc_portal_demo.php';
		$prov = epc_portal_demo_provision_database_raw($dbName, $dbUser, $dbPass);
		if (empty($prov['ok'])) {
			$hint = (string) ($prov['hint'] ?? '');
			if ($hint === '' && !empty($prov['log']) && is_array($prov['log'])) {
				$hint = implode('; ', array_slice($prov['log'], -3));
			}
			return array(
				'ok' => false,
				'message' => 'Dedicated MySQL provision failed for `' . $dbName . '`'
					. ($hint !== '' ? ': ' . $hint : ' — set db_password manually or run epc-erp-tenant-provision.php on server'),
			);
		}
		if (!empty($prov['db_name'])) {
			$dbName = (string) $prov['db_name'];
		}
		$data['db_password'] = $dbPass;
	}

	if ($dedicatedDb && $dbName !== '') {
		if (in_array($dbName, array('docpart', 'ecomae', 'epartscart'), true)) {
			return array('ok' => false, 'message' => 'Dedicated tenants must use their own database — not the shared commerce or platform registry database');
		}
		$dup = $pdo->prepare(
			'SELECT `site_key` FROM `epc_portal_tenants`
			 WHERE `db_name` = ? AND `site_key` != ? AND (`dedicated_db` = 1 OR `erp_only_shared` = 1) LIMIT 1'
		);
		$dup->execute(array($dbName, $key));
		$other = $dup->fetch(PDO::FETCH_ASSOC);
		if ($other) {
			return array('ok' => false, 'message' => 'Database ' . $dbName . ' already assigned to tenant ' . ($other['site_key'] ?? ''));
		}
	}

	$introJson = '';
	if (!empty($data['intro_json']) && is_array($data['intro_json'])) {
		$introJson = json_encode($data['intro_json'], JSON_UNESCAPED_UNICODE);
	} elseif (!empty($data['intro_json']) && is_string($data['intro_json'])) {
		$introJson = (string) $data['intro_json'];
	}

	$stmt = $pdo->prepare(
		'INSERT INTO `epc_portal_tenants`
		(`site_key`, `hostname`, `industry_code`, `status`, `trade_name`, `hub_name`, `from_email`,
		 `db_name`, `db_user`, `db_password`, `notes`, `intro_json`, `hosted_on`, `erp_only_shared`,
		 `dedicated_db`, `scale_policy`, `blockchain_mode`, `created_at`, `updated_at`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
		ON DUPLICATE KEY UPDATE
		`hostname` = VALUES(`hostname`), `industry_code` = VALUES(`industry_code`), `status` = VALUES(`status`),
		`trade_name` = VALUES(`trade_name`), `hub_name` = VALUES(`hub_name`), `from_email` = VALUES(`from_email`),
		`db_name` = IF(VALUES(`db_password`) != \'\', VALUES(`db_name`), `db_name`),
		`db_user` = IF(VALUES(`db_password`) != \'\', VALUES(`db_user`), `db_user`),
		`db_password` = IF(VALUES(`db_password`) != \'\', VALUES(`db_password`), `db_password`),
		`notes` = VALUES(`notes`),
		`intro_json` = IF(VALUES(`intro_json`) != \'\', VALUES(`intro_json`), `intro_json`),
		`hosted_on` = VALUES(`hosted_on`), `erp_only_shared` = VALUES(`erp_only_shared`),
		`dedicated_db` = VALUES(`dedicated_db`), `scale_policy` = VALUES(`scale_policy`),
		`blockchain_mode` = VALUES(`blockchain_mode`),
		`updated_at` = VALUES(`updated_at`)'
	);
	$now = time();
	$hostedOn = $erpOnlyShared ? 'platform' : (string) ($data['hosted_on'] ?? 'client');
	$sharedFlag = $erpOnlyShared ? 1 : (int) !empty($data['erp_only_shared']);
	$dedicatedFlag = $dedicatedDb ? 1 : 0;
	$stmt->execute(array(
		$key,
		$hostname,
		$industry,
		$status,
		substr((string) ($data['trade_name'] ?? $key), 0, 120),
		substr((string) ($data['hub_name'] ?? ''), 0, 120),
		substr((string) ($data['from_email'] ?? ''), 0, 120),
		$dbName,
		$dbUser,
		$dbPass,
		substr((string) ($data['notes'] ?? ''), 0, 500),
		$introJson,
		$hostedOn,
		$sharedFlag,
		$dedicatedFlag,
		$scalePolicy,
		$blockchainMode,
		$now,
		$now,
	));

	require_once __DIR__ . '/epc_portal_tenant_intro.php';
	$row = array(
		'site_key' => $key,
		'hostname' => $hostname,
		'industry_code' => $industry,
		'trade_name' => substr((string) ($data['trade_name'] ?? $key), 0, 120),
		'hub_name' => substr((string) ($data['hub_name'] ?? ''), 0, 120),
		'from_email' => substr((string) ($data['from_email'] ?? ''), 0, 120),
	);
	$intro = epc_portal_intro_decode($introJson);
	if (!empty($intro['submitted_at'])) {
		epc_portal_apply_intro_to_site_settings($pdo, $hostname, $row, $intro);
	} else {
		$settings = epc_portal_default_site_settings($hostname);
		$settings['host'] = $hostname;
		$settings['industry_code'] = $industry;
		if (!empty($data['trade_name'])) {
			$settings['hub_name'] = (string) $data['trade_name'];
		}
		$settings['domain_path'] = 'https://' . $hostname . '/';
		epc_portal_save_site_settings($pdo, $settings);
	}

	$sync = array('ok' => true, 'message' => 'skipped');
	if ($status === 'live' && trim($dbName) !== '' && function_exists('epc_portal_sync_tenant_packs_to_client_db')) {
		require_once __DIR__ . '/epc_portal_cp_menu.php';
		$sync = epc_portal_sync_tenant_packs_to_client_db($pdo, $hostname);
	}

	$msg = 'Tenant saved: ' . $hostname;
	if ($status === 'live' && !empty($sync['ok'])) {
		$msg .= ' — ' . ($sync['message'] ?? 'client packs synced');
	} elseif ($status === 'live' && empty($sync['ok'])) {
		$msg .= ' — client pack sync: ' . ($sync['message'] ?? 'failed');
	}

	return array('ok' => true, 'message' => $msg, 'site_key' => $key, 'client_sync' => $sync);
}

function epc_portal_tenant_dns_instructions(string $hostname): array
{
	$ip = epc_portal_platform_ip();
	return array(
		'ip' => $ip,
		'hostname' => $hostname,
		'steps' => array(
			'Log in to GoDaddy → DNS for ' . preg_replace('/^www\./', '', $hostname),
			'Add A record @ → ' . $ip,
			'Add A record www → ' . $ip,
			'Remove old A records pointing to other hosts',
			'Wait 5–60 minutes for DNS propagation',
			'In CloudPanel: add ' . $hostname . ' as domain alias on www.ecomae.com (same docroot, no extra disk)',
			'Issue Let\'s Encrypt SSL for the alias',
			'Set tenant status to Live in Super CP',
		),
	);
}
