<?php
/**
 * Shared DB bootstrap for final-gate smoke issuer / ensure-table helpers.
 * Prefer PHP DP_Config credentials into ASP.NET TenantRegistry database.
 */
declare(strict_types=1);

/**
 * @return array<string,string>
 */
function smoke_parse_env_file(string $path): array
{
	$out = [];
	foreach (file($path, FILE_IGNORE_NEW_LINES) ?: [] as $line) {
		$line = trim($line);
		if ($line === '' || str_starts_with($line, '#') || !str_contains($line, '=')) {
			continue;
		}
		[$k, $v] = explode('=', $line, 2);
		$k = trim($k);
		$v = trim($v);
		if (
			(str_starts_with($v, '"') && str_ends_with($v, '"'))
			|| (str_starts_with($v, "'") && str_ends_with($v, "'"))
		) {
			$v = substr($v, 1, -1);
		}
		$out[$k] = $v;
	}
	return $out;
}

/**
 * @return array{host?:string,db?:string,user?:string,password?:string}
 */
function smoke_parse_dotnet_conn(string $conn): array
{
	$out = [];
	if ($conn === '' || str_contains($conn, '<db_')) {
		return $out;
	}
	foreach (explode(';', $conn) as $part) {
		$part = trim($part);
		if ($part === '' || !str_contains($part, '=')) {
			continue;
		}
		[$k, $v] = explode('=', $part, 2);
		$k = strtolower(trim($k));
		$v = trim($v);
		if (in_array($k, ['server', 'host', 'data source'], true)) {
			$out['host'] = $v;
		} elseif (in_array($k, ['database', 'initial catalog'], true)) {
			$out['db'] = $v;
		} elseif (in_array($k, ['user', 'uid', 'user id'], true)) {
			$out['user'] = $v;
		} elseif (in_array($k, ['password', 'pwd'], true)) {
			$out['password'] = $v;
		}
	}
	return $out;
}

function smoke_resolve_php_docroot(): string
{
	$docroot = getenv('ECOMAE_PHP_DOCROOT') ?: '';
	if ($docroot !== '' && is_file($docroot . '/config.php')) {
		return $docroot;
	}
	foreach ([
		'/home/ecomae/htdocs/www.ecomae.com',
		'/home/ecomae/htdocs',
		'/home/cloudpanel/htdocs/www.ecomae.com',
		'/home/www/htdocs',
		'/var/www/www.ecomae.com',
		'/var/www/ecomae',
		'/var/www/html',
	] as $candidate) {
		if (is_file($candidate . '/config.php')) {
			return $candidate;
		}
	}
	return '';
}

/**
 * @return array{host:string,db:string,user:string,password:string}|null
 */
function smoke_load_php_dp_config(string $docroot): ?array
{
	$configPath = $docroot . '/config.php';
	if (!is_file($configPath)) {
		return null;
	}

	$_SERVER['DOCUMENT_ROOT'] = $docroot;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}

	try {
		require_once $configPath;
		if (!class_exists('DP_Config', false)) {
			fwrite(STDERR, "WARN: DP_Config class not found after loading {$configPath}\n");
			return null;
		}
		$cfg = new DP_Config();
		if (is_file($docroot . '/config.local.php')) {
			$epc_config_local = null;
			require $docroot . '/config.local.php';
			if (isset($epc_config_local) && is_array($epc_config_local)) {
				foreach ($epc_config_local as $key => $value) {
					if (property_exists($cfg, $key)) {
						$cfg->$key = $value;
					}
				}
			}
		}
		$host = (string) ($cfg->host ?? '127.0.0.1');
		$db = (string) ($cfg->db ?? '');
		$user = (string) ($cfg->user ?? '');
		$pass = (string) ($cfg->password ?? '');
		if ($db === '' || $user === '') {
			fwrite(STDERR, "WARN: DP_Config missing db/user\n");
			return null;
		}
		return ['host' => $host, 'db' => $db, 'user' => $user, 'password' => $pass];
	} catch (Throwable $e) {
		fwrite(STDERR, "WARN: load DP_Config failed: " . $e->getMessage() . "\n");
		return null;
	}
}

function smoke_pdo_connect(string $host, string $db, string $user, string $pass): PDO
{
	$dsn = "mysql:host={$host};dbname={$db};charset=utf8mb4";
	return new PDO($dsn, $user, $pass, [
		PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
		PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
	]);
}

/**
 * @param array<string,string> $env
 * @return array{0: PDO, 1: string}
 */
function smoke_open_pdo(array $env): array
{
	$smokeHost = getenv('ECOMAE_SMOKE_DB_HOST') ?: '';
	$smokeDb = getenv('ECOMAE_SMOKE_DB_NAME') ?: '';
	$smokeUser = getenv('ECOMAE_SMOKE_DB_USER') ?: '';
	$smokePass = getenv('ECOMAE_SMOKE_DB_PASSWORD') ?: '';
	if ($smokeDb !== '' && $smokeUser !== '') {
		$host = $smokeHost !== '' ? $smokeHost : '127.0.0.1';
		$pdo = smoke_pdo_connect($host, $smokeDb, $smokeUser, $smokePass);
		return [$pdo, "ECOMAE_SMOKE_DB_* user={$smokeUser} db={$smokeDb}"];
	}

	$dotnet = smoke_parse_dotnet_conn($env['ConnectionStrings__TenantRegistry'] ?? '');
	$docroot = smoke_resolve_php_docroot();
	$phpCfg = $docroot !== '' ? smoke_load_php_dp_config($docroot) : null;

	if ($phpCfg !== null && ($dotnet['db'] ?? '') !== '') {
		try {
			$pdo = smoke_pdo_connect(
				(string) ($dotnet['host'] ?: $phpCfg['host']),
				(string) $dotnet['db'],
				(string) $phpCfg['user'],
				(string) $phpCfg['password']
			);
			return [$pdo, "php-DP_Config user → TenantRegistry db={$dotnet['db']} (docroot={$docroot})"];
		} catch (Throwable $e) {
			fwrite(STDERR, "WARN: PHP user → TenantRegistry db failed: " . $e->getMessage() . "\n");
		}
	}

	if ($phpCfg !== null) {
		try {
			$pdo = smoke_pdo_connect(
				(string) $phpCfg['host'],
				(string) $phpCfg['db'],
				(string) $phpCfg['user'],
				(string) $phpCfg['password']
			);
			$note = '';
			if (($dotnet['db'] ?? '') !== '' && $dotnet['db'] !== $phpCfg['db']) {
				$note = " WARN: PHP db={$phpCfg['db']} differs from TenantRegistry db={$dotnet['db']} — ASP.NET may not see keys";
			}
			return [$pdo, "php-DP_Config db={$phpCfg['db']} (docroot={$docroot}){$note}"];
		} catch (Throwable $e) {
			fwrite(STDERR, "WARN: PHP DP_Config PDO failed: " . $e->getMessage() . "\n");
		}
	} elseif ($docroot !== '') {
		fwrite(STDERR, "WARN: could not load DP_Config from {$docroot}/config.php\n");
	}

	if (($dotnet['db'] ?? '') === '' || ($dotnet['user'] ?? '') === '') {
		fwrite(STDERR, "No usable DB credentials.\n");
		exit(2);
	}

	$pdo = smoke_pdo_connect(
		(string) ($dotnet['host'] ?: '127.0.0.1'),
		(string) $dotnet['db'],
		(string) $dotnet['user'],
		(string) ($dotnet['password'] ?? '')
	);
	return [$pdo, "ConnectionStrings__TenantRegistry user={$dotnet['user']} db={$dotnet['db']}"];
}
