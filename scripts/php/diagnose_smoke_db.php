#!/usr/bin/env php
<?php
/**
 * Redacted smoke DB diagnose: TenantRegistry vs PHP app DB.
 * Never prints passwords, keys, or session tokens.
 */
declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	fwrite(STDERR, "CLI only\n");
	exit(1);
}

require_once __DIR__ . '/_smoke_db_bootstrap.php';

$envFile = getenv('ECOMAE_ASPNET_ENV_FILE') ?: '/etc/ecomae-aspnet/platform.env';
if (!is_file($envFile)) {
	fwrite(STDERR, "Missing env file: {$envFile}\n");
	exit(2);
}

$env = smoke_parse_env_file($envFile);
$dotnet = smoke_parse_dotnet_conn($env['ConnectionStrings__TenantRegistry'] ?? '');
$tenantDb = (string) ($dotnet['db'] ?? '');
$tenantUser = (string) ($dotnet['user'] ?? '');
$docroot = smoke_resolve_php_docroot();
$phpCfg = $docroot !== '' ? smoke_load_php_dp_config($docroot) : null;
$phpDb = $phpCfg['db'] ?? '';

fwrite(STDOUT, "TenantRegistry Database={$tenantDb} user={$tenantUser}\n");
fwrite(STDOUT, "PHP DP_Config Database={$phpDb}\n");
if ($tenantDb !== '' && $phpDb !== '' && $tenantDb !== $phpDb) {
	fwrite(STDOUT, "MISMATCH: TenantRegistry db differs from PHP app db (keys/sessions must match ASP.NET ConnectionStrings).\n");
} elseif ($tenantDb !== '' && $phpDb !== '' && $tenantDb === $phpDb) {
	fwrite(STDOUT, "ALIGNED: TenantRegistry Database matches PHP app db.\n");
}

/**
 * @return array{ok:bool,table:?string,adminSessions:?int,backendAdmin:?bool,error:?string}
 */
function probe_db(?PDO $pdo): array
{
	if ($pdo === null) {
		return ['ok' => false, 'table' => null, 'adminSessions' => null, 'backendAdmin' => null, 'error' => 'no connection'];
	}
	$out = ['ok' => true, 'table' => null, 'adminSessions' => null, 'backendAdmin' => null, 'error' => null];
	try {
		$pdo->query('SELECT 1 FROM `epc_api_clients` LIMIT 1');
		$out['table'] = 'present';
	} catch (Throwable $e) {
		$out['table'] = 'missing';
		if (!str_contains($e->getMessage(), '1146') && !str_contains($e->getMessage(), "doesn't exist")) {
			$out['error'] = 'epc_api_clients: ' . $e->getMessage();
		}
	}
	try {
		$n = (int) $pdo->query('SELECT COUNT(*) FROM `sessions` WHERE `type` = 1 AND `user_id` > 0')->fetchColumn();
		$out['adminSessions'] = $n;
	} catch (Throwable $e) {
		$out['adminSessions'] = null;
		$out['error'] = trim(($out['error'] ?? '') . ' sessions: ' . $e->getMessage());
	}
	try {
		$sql = <<<'SQL'
SELECT COUNT(*) FROM `users_groups_bind` ugb
INNER JOIN `groups` g ON g.`id` = ugb.`group_id` AND g.`for_backend` = 1
SQL;
		$out['backendAdmin'] = ((int) $pdo->query($sql)->fetchColumn()) > 0;
	} catch (Throwable) {
		$out['backendAdmin'] = null;
	}
	return $out;
}

$tenantPdo = null;
$tenantErr = null;
if ($tenantDb !== '' && $tenantUser !== '') {
	try {
		$tenantPdo = smoke_pdo_connect(
			(string) ($dotnet['host'] ?: '127.0.0.1'),
			$tenantDb,
			$tenantUser,
			(string) ($dotnet['password'] ?? '')
		);
	} catch (Throwable $e) {
		$tenantErr = $e->getMessage();
	}
}

$phpPdo = null;
$phpErr = null;
if ($phpCfg !== null) {
	try {
		$phpPdo = smoke_pdo_connect(
			(string) $phpCfg['host'],
			(string) $phpCfg['db'],
			(string) $phpCfg['user'],
			(string) $phpCfg['password']
		);
	} catch (Throwable $e) {
		$phpErr = $e->getMessage();
	}
}

$tenantProbe = probe_db($tenantPdo);
$phpProbe = probe_db($phpPdo);

fwrite(STDOUT, "TenantRegistry probe: table={$tenantProbe['table']} admin_sessions=" . ($tenantProbe['adminSessions'] ?? 'n/a')
	. " backend_group_binds=" . ($tenantProbe['backendAdmin'] === null ? 'n/a' : ($tenantProbe['backendAdmin'] ? 'yes' : 'no')) . "\n");
if ($tenantErr) {
	fwrite(STDOUT, "  connect_error: {$tenantErr}\n");
} elseif ($tenantProbe['error']) {
	fwrite(STDOUT, "  note: {$tenantProbe['error']}\n");
}

fwrite(STDOUT, "PHP app DB probe: table={$phpProbe['table']} admin_sessions=" . ($phpProbe['adminSessions'] ?? 'n/a')
	. " backend_group_binds=" . ($phpProbe['backendAdmin'] === null ? 'n/a' : ($phpProbe['backendAdmin'] ? 'yes' : 'no')) . "\n");
if ($phpErr) {
	fwrite(STDOUT, "  connect_error: {$phpErr}\n");
} elseif ($phpProbe['error']) {
	fwrite(STDOUT, "  note: {$phpProbe['error']}\n");
}

// Can platform user reach PHP db?
$platformOnPhp = 'n/a';
if ($phpDb !== '' && $tenantUser !== '' && $phpCfg !== null) {
	try {
		smoke_pdo_connect(
			(string) ($dotnet['host'] ?: $phpCfg['host']),
			(string) $phpDb,
			$tenantUser,
			(string) ($dotnet['password'] ?? '')
		);
		$platformOnPhp = 'yes';
	} catch (Throwable $e) {
		$platformOnPhp = 'no (' . $e->getMessage() . ')';
	}
}
fwrite(STDOUT, "Platform user → PHP db connect: {$platformOnPhp}\n");

$exit = 0;
if (($tenantProbe['table'] ?? '') !== 'present') {
	$exit = 3;
	fwrite(STDOUT, "STATUS: TenantRegistry missing epc_api_clients — smoke price/catalog will HTTP 500 until CREATE/GRANT or Database= align.\n");
	if (($phpProbe['table'] ?? '') === 'present' && str_starts_with($platformOnPhp, 'yes')) {
		fwrite(STDOUT, "HINT: PHP db already has epc_api_clients and platform user can connect — prefer align path B.\n");
	} else {
		fwrite(STDOUT, "HINT: prefer apply DDL path A (debian.cnf / paste as MySQL admin).\n");
	}
} else {
	fwrite(STDOUT, "STATUS: TenantRegistry epc_api_clients present — proceed to issue smoke credentials.\n");
}

exit($exit);
