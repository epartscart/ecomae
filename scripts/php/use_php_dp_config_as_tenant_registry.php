#!/usr/bin/env php
<?php
/**
 * Point ConnectionStrings__TenantRegistry at PHP DP_Config host/db/user/password.
 * Use when ecomae_aspnet cannot CREATE on asap and cannot CONNECT to ecomae,
 * but PHP DP_Config already has epc_api_clients + admin sessions.
 * Never prints passwords.
 */
declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	fwrite(STDERR, "CLI only\n");
	exit(1);
}

if (getenv('ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY') !== 'YES') {
	fwrite(STDERR, "Refusing: set ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY=YES\n");
	exit(2);
}

require_once __DIR__ . '/_smoke_db_bootstrap.php';

$envFile = getenv('ECOMAE_ASPNET_ENV_FILE') ?: '/etc/ecomae-aspnet/platform.env';
if (!is_file($envFile) || !is_writable($envFile)) {
	fwrite(STDERR, "Env file missing/unwritable: {$envFile}\n");
	exit(1);
}

$env = smoke_parse_env_file($envFile);
$oldConn = $env['ConnectionStrings__TenantRegistry'] ?? '';
$old = smoke_parse_dotnet_conn($oldConn);

$docroot = smoke_resolve_php_docroot();
$phpCfg = $docroot !== '' ? smoke_load_php_dp_config($docroot) : null;
if ($phpCfg === null) {
	fwrite(STDERR, "BLOCKED: could not load PHP DP_Config\n");
	exit(1);
}

$host = (string) $phpCfg['host'];
$db = (string) $phpCfg['db'];
$user = (string) $phpCfg['user'];
$pass = (string) $phpCfg['password'];
if ($db === '' || $user === '') {
	fwrite(STDERR, "BLOCKED: PHP DP_Config missing db/user\n");
	exit(1);
}

try {
	$pdo = smoke_pdo_connect($host, $db, $user, $pass);
	$pdo->query('SELECT 1 FROM `epc_api_clients` LIMIT 1');
} catch (Throwable $e) {
	fwrite(STDERR, "BLOCKED: PHP DP_Config cannot read epc_api_clients: " . $e->getMessage() . "\n");
	exit(1);
}

$adminSessions = 0;
$backend = false;
try {
	$adminSessions = (int) $pdo->query('SELECT COUNT(*) FROM `sessions` WHERE `type` = 1 AND `user_id` > 0')->fetchColumn();
} catch (Throwable) {
}
try {
	$sql = <<<'SQL'
SELECT COUNT(*) FROM `users_groups_bind` ugb
INNER JOIN `groups` g ON g.`id` = ugb.`group_id` AND g.`for_backend` = 1
SQL;
	$backend = ((int) $pdo->query($sql)->fetchColumn()) > 0;
} catch (Throwable) {
}

// Escape password for connection-string form (semicolon/equals uncommon but possible).
$passSafe = str_replace([';', '='], ['', ''], $pass);
if ($passSafe !== $pass) {
	fwrite(STDERR, "WARN: PHP DB password contained ; or = — stripped for connection string; prefer GRANT path if auth fails.\n");
}

$newConn = "Server={$host};Database={$db};User={$user};Password={$passSafe};TreatTinyAsBoolean=true;";

$sameDb = (($old['db'] ?? '') === $db);
$sameUser = (($old['user'] ?? '') === $user);
if ($sameDb && $sameUser) {
	fwrite(STDOUT, "Already using PHP DP_Config db={$db} user={$user}\n");
	exit(0);
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

$backup = $envFile . '.bak-php-tenant-' . gmdate('YmdHis');
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

$oldDb = (string) ($old['db'] ?? '?');
$oldUser = (string) ($old['user'] ?? '?');
fwrite(STDOUT, "OK TenantRegistry now uses PHP DP_Config credentials\n");
fwrite(STDOUT, "  was: Database={$oldDb} user={$oldUser}\n");
fwrite(STDOUT, "  now: Database={$db} user={$user} (password not printed)\n");
fwrite(STDOUT, "  epc_api_clients=present admin_sessions≈{$adminSessions} backend_binds=" . ($backend ? 'yes' : 'no') . "\n");
fwrite(STDOUT, "Backup: {$backup}\n");
fwrite(STDOUT, "Restart ecomae-platform.service before issue/capture.\n");
exit(0);
