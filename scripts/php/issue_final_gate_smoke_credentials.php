#!/usr/bin/env php
<?php
/**
 * Issue/rotate final-gate smoke API keys and bind an active admin session into platform.env.
 * CLI only. Never prints plaintext keys/cookies. Requires ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES.
 *
 * Writes keys into the ASP.NET TenantRegistry database (ConnectionStrings__TenantRegistry)
 * so DbLegacyApiClientStore can authenticate. Cookie values are bash-quoted so
 * `source platform.env` does not truncate at ';' before admin_u_id=.
 * Creates epc_api_clients only when ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES.
 * When PHP sessions DB differs, set ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES to copy
 * the admin session row into TenantRegistry.
 *
 * Usage:
 *   ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES php scripts/php/issue_final_gate_smoke_credentials.php
 *   ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES \
 *     ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES php scripts/php/issue_final_gate_smoke_credentials.php
 */
declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	fwrite(STDERR, "CLI only\n");
	exit(1);
}

if (getenv('ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS') !== 'YES') {
	fwrite(STDERR, "Refusing: set ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES\n");
	exit(2);
}

require_once __DIR__ . '/_smoke_db_bootstrap.php';

$envFile = getenv('ECOMAE_ASPNET_ENV_FILE') ?: '/etc/ecomae-aspnet/platform.env';
if (!is_file($envFile) || !is_readable($envFile)) {
	fwrite(STDERR, "Missing env file: {$envFile}\n");
	exit(2);
}
if (!is_writable($envFile)) {
	fwrite(STDERR, "Env file not writable: {$envFile}\n");
	exit(2);
}

$env = smoke_parse_env_file($envFile);
[$pdo, $dbSource] = smoke_open_pdo($env);
fwrite(STDERR, "DB source: {$dbSource}\n");

$dotnet = smoke_parse_dotnet_conn($env['ConnectionStrings__TenantRegistry'] ?? '');
$primaryDb = (string) ($dotnet['db'] ?? '');
if (preg_match('/\bdb=([^\s)]+)/', $dbSource, $mDb)) {
	$primaryDb = $mDb[1];
}

ensure_api_clients_table($pdo, $env);

$catalogActions = json_encode(
	['manufacturers', 'models', 'modifications', 'categories', 'articles', 'vin', 'status', 'engines', 'analogs', 'brands', 'products', 'suppliers'],
	JSON_UNESCAPED_UNICODE
);

try {
	$priceKey = upsert_key($pdo, 'price_pro', 'Final-gate smoke Price PRO', 'smoke-final-gate@ecomae.local', 5000, '[]');
	$catalogKey = upsert_key($pdo, 'catalog', 'Final-gate smoke Catalog', 'smoke-final-gate@ecomae.local', 5000, $catalogActions);
} catch (Throwable $e) {
	fwrite(STDERR, "Failed to INSERT/UPDATE epc_api_clients: " . $e->getMessage() . "\n");
	fwrite(STDERR, "  bash scripts/cloudpanel_diagnose_smoke_db.sh\n");
	fwrite(STDERR, "  ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES bash scripts/cloudpanel_apply_epc_api_clients_ddl.sh\n");
	fwrite(STDERR, "  # or align TenantRegistry Database= to PHP db, restart platform, then re-issue\n");
	exit(1);
}

[$sessionToken, $userId, $sessionSource] = find_admin_session($pdo, $env, $primaryDb);
if ($sessionToken === null) {
	fwrite(STDERR, "BLOCKED: no active admin session (sessions.type=1). Log into https://www.ecomae.com/CP/ once, then re-run.\n");
	write_env_keys($envFile, $priceKey, $catalogKey, null, null);
	fwrite(STDOUT, "Wrote API keys into {$envFile}. Admin cookie still missing — login to Super CP, then re-run this script.\n");
	exit(3);
}

if ($sessionSource === 'php-app-db-mismatch') {
	fwrite(STDERR, "WARN: admin session found in PHP app DB, not TenantRegistry db={$primaryDb}.\n");
	$phpPdo = smoke_open_php_sessions_pdo($env, $primaryDb);
	if ($phpPdo !== null) {
		[$synced, $syncDetail] = smoke_sync_admin_session_to_tenant($pdo, $phpPdo, $sessionToken, $userId);
		fwrite(STDERR, ($synced ? 'OK' : 'WARN') . " session sync: {$syncDetail}\n");
		if ($synced) {
			$sessionSource = 'synced-from-php-app-db';
		} else {
			fwrite(STDERR, "ASP.NET /auth/session/probe reads TenantRegistry — re-run with:\n");
			fwrite(STDERR, "  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n");
			fwrite(STDERR, "    bash scripts/cloudpanel_issue_smoke_credentials.sh\n");
			fwrite(STDERR, "Or align ConnectionStrings__TenantRegistry Database= with the PHP sessions DB.\n");
		}
	}
	if (!smoke_tenant_admin_backend_ok($pdo, $userId)) {
		fwrite(STDERR, "WARN: TenantRegistry has no backend group bind for admin_u_id={$userId}.\n");
		fwrite(STDERR, "Probe stays anonymous until users/groups/users_groups_bind exist in db={$primaryDb},\n");
		fwrite(STDERR, "or Database= is pointed at the PHP app DB that holds Super CP identity.\n");
	}
}

$cookie = 'admin_session=' . $sessionToken . '; admin_u_id=' . $userId;
write_env_keys($envFile, $priceKey, $catalogKey, $cookie, $userId);

fwrite(STDOUT, "OK wrote smoke credentials into {$envFile} (values not printed).\n");
fwrite(STDOUT, "price_key_prefix=" . substr($priceKey, 0, 24) . "…\n");
fwrite(STDOUT, "catalog_key_prefix=" . substr($catalogKey, 0, 24) . "…\n");
fwrite(STDOUT, "admin_u_id={$userId}\n");
fwrite(STDOUT, "session_source={$sessionSource}\n");
fwrite(STDOUT, "Next: source {$envFile} && bash scripts/cloudpanel_validate_final_gate_env.sh\n");
exit(0);

/**
 * @param array<string,string> $env
 */
function ensure_api_clients_table(PDO $pdo, array $env): void
{
	try {
		$pdo->query('SELECT 1 FROM `epc_api_clients` LIMIT 1');
		return;
	} catch (Throwable $e) {
		fwrite(STDERR, "Table epc_api_clients missing or not readable: " . $e->getMessage() . "\n");
	}

	if (getenv('ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE') !== 'YES') {
		fwrite(STDERR, "BLOCKED: epc_api_clients missing on TenantRegistry — do not re-issue until table exists.\n");
		fwrite(STDERR, "  bash scripts/cloudpanel_diagnose_smoke_db.sh\n");
		fwrite(STDERR, "  A) ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES bash scripts/cloudpanel_apply_epc_api_clients_ddl.sh\n");
		fwrite(STDERR, "  B) ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES bash scripts/cloudpanel_align_tenant_registry_to_php_db.sh\n");
		fwrite(STDERR, "  C) ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY=YES \\\n");
		fwrite(STDERR, "       ECOMAE_CONFIRM_RESTART_PLATFORM=YES \\\n");
		fwrite(STDERR, "       bash scripts/cloudpanel_use_php_dp_config_as_tenant_registry.sh\n");
		fwrite(STDERR, "  Then re-issue with ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES\n");
		exit(1);
	}

	[$ok, $detail] = smoke_try_create_epc_api_clients($env, $pdo);
	if ($ok) {
		fwrite(STDERR, "Created epc_api_clients ({$detail})\n");
		return;
	}

	$dotnet = smoke_parse_dotnet_conn($env['ConnectionStrings__TenantRegistry'] ?? '');
	smoke_print_epc_api_clients_recovery($dotnet, $detail);
	exit(1);
}

function make_key(string $product): string
{
	$prefix = $product === 'price_pro' ? 'epc_pricepro_' : 'epc_catalog_';
	return $prefix . bin2hex(random_bytes(12));
}

function upsert_key(PDO $pdo, string $product, string $label, string $email, int $dailyLimit, string $actionsJson): string
{
	$plain = make_key($product);
	$hash = hash('sha256', $plain);
	$prefix = substr($plain, 0, 24);
	$now = time();

	$st = $pdo->prepare('SELECT `id` FROM `epc_api_clients` WHERE `label` = ? LIMIT 1');
	$st->execute([$label]);
	$id = (int) $st->fetchColumn();

	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `epc_api_clients` SET `client_key_hash`=?, `client_key_prefix`=?, `product`=?, `contact_email`=?,
			 `active`=1, `daily_limit`=?, `allowed_actions_json`=?, `time_updated`=? WHERE `id`=?'
		)->execute([$hash, $prefix, $product, $email, $dailyLimit, $actionsJson, $now, $id]);
	} else {
		$pdo->prepare(
			'INSERT INTO `epc_api_clients`
			 (`client_key_hash`,`client_key_prefix`,`product`,`label`,`contact_email`,`active`,`daily_limit`,`calls_today`,`calls_reset_date`,`allowed_actions_json`,`time_created`,`time_updated`)
			 VALUES (?,?,?,?,?,1,?,0,CURDATE(),?,?,?)'
		)->execute([$hash, $prefix, $product, $label, $email, $dailyLimit, $actionsJson, $now, $now]);
	}

	return $plain;
}

/**
 * @param array<string,string> $env
 * @return array{0:?string,1:int,2:string}
 */
function find_admin_session(PDO $pdo, array $env, string $primaryDb): array
{
	$forced = trim((string) (getenv('ECOMAE_FORCE_ADMIN_COOKIE_HEADER') ?: ''));
	if ($forced !== '' && preg_match('/admin_session=([^;]+)/', $forced, $mSid) && preg_match('/admin_u_id=(\d+)/', $forced, $mUid)) {
		return [$mSid[1], (int) $mUid[1], 'forced-env'];
	}

	$row = query_admin_session($pdo);
	if ($row !== null) {
		return [$row[0], $row[1], 'tenant-registry'];
	}

	$phpPdo = smoke_open_php_sessions_pdo($env, $primaryDb);
	if ($phpPdo !== null) {
		$row = query_admin_session($phpPdo);
		if ($row !== null) {
			return [$row[0], $row[1], 'php-app-db-mismatch'];
		}
	}

	return [null, 0, 'none'];
}

/** @return array{0:string,1:int}|null */
function query_admin_session(PDO $pdo): ?array
{
	$candidates = [
		'SELECT `session`, `user_id` FROM `sessions` WHERE `type` = 1 AND `user_id` > 0 ORDER BY `id` DESC LIMIT 1',
		'SELECT `session`, `user_id` FROM `sessions` WHERE `type` = 1 AND `user_id` > 0 ORDER BY `time` DESC LIMIT 1',
		'SELECT `session`, `user_id` FROM `sessions` WHERE `type` = 1 AND `user_id` > 0 LIMIT 1',
	];
	foreach ($candidates as $sql) {
		try {
			$row = $pdo->query($sql)->fetch();
			if ($row && !empty($row['session']) && (int) $row['user_id'] > 0) {
				return [(string) $row['session'], (int) $row['user_id']];
			}
		} catch (Throwable) {
			// try next shape
		}
	}
	return null;
}

function write_env_keys(string $path, string $priceKey, string $catalogKey, ?string $cookie, ?int $userId): void
{
	$lines = file($path, FILE_IGNORE_NEW_LINES);
	if ($lines === false) {
		throw new RuntimeException("Cannot read {$path}");
	}

	$set = [
		'ECOMAE_PRICE_LOOKUP_API_KEY' => smoke_bash_quote($priceKey),
		'ECOMAE_CATALOG_API_KEY' => smoke_bash_quote($catalogKey),
	];
	if ($cookie !== null && $cookie !== '') {
		$set['ECOMAE_ADMIN_COOKIE_HEADER'] = smoke_bash_quote($cookie);
	}
	if ($userId !== null && $userId > 0) {
		$set['ECOMAE_ADMIN_U_ID'] = smoke_bash_quote((string) $userId);
	}

	$seen = [];
	$out = [];
	foreach ($lines as $line) {
		$trimmed = ltrim($line);
		if ($trimmed === '' || str_starts_with($trimmed, '#') || !str_contains($line, '=')) {
			$out[] = $line;
			continue;
		}
		$key = trim(explode('=', $line, 2)[0]);
		if (isset($set[$key])) {
			$out[] = $key . '=' . $set[$key];
			$seen[$key] = true;
		} else {
			$out[] = $line;
		}
	}
	foreach ($set as $key => $value) {
		if (empty($seen[$key])) {
			$out[] = $key . '=' . $value;
		}
	}

	$tmp = $path . '.tmp.' . getmypid();
	file_put_contents($tmp, implode("\n", $out) . "\n");
	chmod($tmp, 0600);
	rename($tmp, $path);
	chmod($path, 0600);
}
