#!/usr/bin/env php
<?php
/**
 * Rewrite ConnectionStrings__TenantRegistry Database= to PHP DP_Config db.
 * Only when PHP db has epc_api_clients and platform user can connect to it.
 * Never prints passwords.
 */
declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	fwrite(STDERR, "CLI only\n");
	exit(1);
}

if (getenv('ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB') !== 'YES') {
	fwrite(STDERR, "Refusing: set ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES\n");
	exit(2);
}

require_once __DIR__ . '/_smoke_db_bootstrap.php';

$envFile = getenv('ECOMAE_ASPNET_ENV_FILE') ?: '/etc/ecomae-aspnet/platform.env';
if (!is_file($envFile) || !is_writable($envFile)) {
	fwrite(STDERR, "Env file missing/unwritable: {$envFile}\n");
	exit(1);
}

$env = smoke_parse_env_file($envFile);
$conn = $env['ConnectionStrings__TenantRegistry'] ?? '';
$dotnet = smoke_parse_dotnet_conn($conn);
$tenantDb = (string) ($dotnet['db'] ?? '');
$tenantUser = (string) ($dotnet['user'] ?? '');
$tenantPass = (string) ($dotnet['password'] ?? '');
$tenantHost = (string) ($dotnet['host'] ?: '127.0.0.1');

$docroot = smoke_resolve_php_docroot();
$phpCfg = $docroot !== '' ? smoke_load_php_dp_config($docroot) : null;
if ($phpCfg === null) {
	fwrite(STDERR, "BLOCKED: could not load PHP DP_Config\n");
	exit(1);
}
$phpDb = (string) $phpCfg['db'];
if ($phpDb === '') {
	fwrite(STDERR, "BLOCKED: PHP DP_Config db empty\n");
	exit(1);
}

if ($tenantDb === $phpDb) {
	fwrite(STDOUT, "Already aligned: Database={$phpDb}\n");
	exit(0);
}

if ($tenantDb === '' || $tenantUser === '') {
	fwrite(STDERR, "BLOCKED: ConnectionStrings__TenantRegistry incomplete\n");
	exit(1);
}

// PHP db must already have the API clients table.
try {
	$phpPdo = smoke_pdo_connect((string) $phpCfg['host'], $phpDb, (string) $phpCfg['user'], (string) $phpCfg['password']);
	$phpPdo->query('SELECT 1 FROM `epc_api_clients` LIMIT 1');
} catch (Throwable $e) {
	fwrite(STDERR, "BLOCKED: PHP db={$phpDb} lacks readable epc_api_clients: " . $e->getMessage() . "\n");
	fwrite(STDERR, "Create it on PHP db first, or apply DDL on TenantRegistry instead.\n");
	exit(1);
}

// Platform user must be able to use the PHP db (ASP.NET will).
try {
	$platformPdo = smoke_pdo_connect($tenantHost, $phpDb, $tenantUser, $tenantPass);
	$platformPdo->query('SELECT 1 FROM `epc_api_clients` LIMIT 1');
} catch (Throwable $e) {
	fwrite(STDERR, "BLOCKED: platform user={$tenantUser} cannot use PHP db={$phpDb}: " . $e->getMessage() . "\n");
	fwrite(STDERR, "GRANT SELECT/INSERT/UPDATE on {$phpDb}.* to '{$tenantUser}'@'localhost' as MySQL admin, then retry.\n");
	exit(1);
}

$adminSessions = 0;
try {
	$adminSessions = (int) $platformPdo->query('SELECT COUNT(*) FROM `sessions` WHERE `type` = 1 AND `user_id` > 0')->fetchColumn();
} catch (Throwable) {
	// optional
}

$newConn = preg_replace('/(?i)\bDatabase\s*=\s*[^;]*/', 'Database=' . $phpDb, $conn, 1);
if (!is_string($newConn) || $newConn === $conn) {
	fwrite(STDERR, "BLOCKED: could not rewrite Database= in ConnectionStrings__TenantRegistry\n");
	exit(1);
}

$lines = file($envFile, FILE_IGNORE_NEW_LINES);
if ($lines === false) {
	fwrite(STDERR, "Cannot read {$envFile}\n");
	exit(1);
}

$out = [];
$seen = false;
foreach ($lines as $line) {
	$trimmed = ltrim($line);
	if ($trimmed === '' || str_starts_with($trimmed, '#') || !str_contains($line, '=')) {
		$out[] = $line;
		continue;
	}
	$key = trim(explode('=', $line, 2)[0]);
	if ($key === 'ConnectionStrings__TenantRegistry') {
		$out[] = 'ConnectionStrings__TenantRegistry=' . smoke_bash_quote($newConn);
		$seen = true;
	} else {
		$out[] = $line;
	}
}
if (!$seen) {
	$out[] = 'ConnectionStrings__TenantRegistry=' . smoke_bash_quote($newConn);
}

$backup = $envFile . '.bak-align-' . gmdate('YmdHis');
if (!copy($envFile, $backup)) {
	fwrite(STDERR, "BLOCKED: could not write backup {$backup}\n");
	exit(1);
}
chmod($backup, 0600);

$tmp = $envFile . '.tmp.' . getmypid();
file_put_contents($tmp, implode("\n", $out) . "\n");
chmod($tmp, 0600);
rename($tmp, $envFile);
chmod($envFile, 0600);

fwrite(STDOUT, "OK aligned TenantRegistry Database: {$tenantDb} → {$phpDb}\n");
fwrite(STDOUT, "Backup: {$backup}\n");
fwrite(STDOUT, "Platform user can read epc_api_clients on {$phpDb}; admin_sessions≈{$adminSessions}\n");
fwrite(STDOUT, "Restart ecomae-platform.service before issue/capture.\n");
exit(0);
