#!/usr/bin/env php
<?php
/**
 * Create epc_api_clients once in the ASP.NET TenantRegistry database.
 * Prefers ConnectionStrings__TenantRegistry, then PHP DP_Config user → that DB,
 * then optional ECOMAE_MYSQL_ADMIN_USER. On CREATE denial, prints paste-ready DDL+GRANT.
 * Requires ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES.
 * Never prints secrets.
 */
declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	fwrite(STDERR, "CLI only\n");
	exit(1);
}

if (getenv('ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE') !== 'YES') {
	fwrite(STDERR, "Refusing: set ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES\n");
	exit(2);
}

$envFile = getenv('ECOMAE_ASPNET_ENV_FILE') ?: '/etc/ecomae-aspnet/platform.env';
if (!is_file($envFile)) {
	fwrite(STDERR, "Missing env file: {$envFile}\n");
	exit(1);
}

require_once __DIR__ . '/_smoke_db_bootstrap.php';

$env = smoke_parse_env_file($envFile);
$dotnet = smoke_parse_dotnet_conn($env['ConnectionStrings__TenantRegistry'] ?? '');
[$pdo, $source] = smoke_open_pdo($env);
fwrite(STDERR, "DB source: {$source}\n");

try {
	$pdo->query('SELECT 1 FROM `epc_api_clients` LIMIT 1');
	fwrite(STDERR, "epc_api_clients already present\n");
	exit(0);
} catch (Throwable $e) {
	fwrite(STDERR, "Creating epc_api_clients (" . $e->getMessage() . ")\n");
}

[$ok, $detail] = smoke_try_create_epc_api_clients($env, $pdo);
if ($ok) {
	fwrite(STDERR, "OK {$detail}\n");
	// Verify primary platform user can read the table (ASP.NET path).
	try {
		$pdo->query('SELECT 1 FROM `epc_api_clients` LIMIT 1');
		fwrite(STDERR, "Verified readable via primary DB source\n");
	} catch (Throwable $e) {
		fwrite(STDERR, "WARN: created but primary user cannot SELECT yet: " . $e->getMessage() . "\n");
		smoke_print_epc_api_clients_recovery($dotnet, 'GRANT SELECT/INSERT/UPDATE to platform user');
		exit(1);
	}
	exit(0);
}

smoke_print_epc_api_clients_recovery($dotnet, $detail);
exit(1);
