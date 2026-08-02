#!/usr/bin/env php
<?php
/**
 * Issue/rotate final-gate smoke API keys and bind an active admin session into platform.env.
 * CLI only. Never prints plaintext keys/cookies. Requires ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES.
 *
 * Never runs CREATE TABLE — ecomae_aspnet often lacks DDL. Uses existing epc_api_clients.
 * Prefers PHP app PDO (ECOMAE_PHP_DOCROOT/config.php) or ECOMAE_SMOKE_DB_* when the
 * ASP.NET DB user cannot INSERT/UPDATE.
 *
 * Usage:
 *   ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES php scripts/php/issue_final_gate_smoke_credentials.php
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

$envFile = getenv('ECOMAE_ASPNET_ENV_FILE') ?: '/etc/ecomae-aspnet/platform.env';
if (!is_file($envFile) || !is_readable($envFile)) {
	fwrite(STDERR, "Missing env file: {$envFile}\n");
	exit(2);
}
if (!is_writable($envFile)) {
	fwrite(STDERR, "Env file not writable: {$envFile}\n");
	exit(2);
}

$env = parse_env_file($envFile);
[$pdo, $dbSource] = open_smoke_pdo($env);
fwrite(STDERR, "DB source: {$dbSource}\n");

ensure_api_clients_readable($pdo);

$catalogActions = json_encode(
	['manufacturers', 'models', 'modifications', 'categories', 'articles', 'vin', 'status', 'engines', 'analogs', 'brands', 'products', 'suppliers'],
	JSON_UNESCAPED_UNICODE
);

try {
	$priceKey = upsert_key($pdo, 'price_pro', 'Final-gate smoke Price PRO', 'smoke-final-gate@ecomae.local', 5000, '[]');
	$catalogKey = upsert_key($pdo, 'catalog', 'Final-gate smoke Catalog', 'smoke-final-gate@ecomae.local', 5000, $catalogActions);
} catch (Throwable $e) {
	fwrite(STDERR, "Failed to INSERT/UPDATE epc_api_clients: " . $e->getMessage() . "\n");
	fwrite(STDERR, "Issuer never runs CREATE. Fix privileges or use the PHP app DB user:\n");
	fwrite(STDERR, "  A) export ECOMAE_PHP_DOCROOT=/path/to/htdocs  (must contain config.php)\n");
	fwrite(STDERR, "  B) export ECOMAE_SMOKE_DB_HOST=127.0.0.1 ECOMAE_SMOKE_DB_NAME=... \\\n");
	fwrite(STDERR, "       ECOMAE_SMOKE_DB_USER=... ECOMAE_SMOKE_DB_PASSWORD=...\n");
	fwrite(STDERR, "  C) GRANT INSERT,UPDATE ON <db>.epc_api_clients TO 'ecomae_aspnet'@'localhost';\n");
	exit(1);
}

[$sessionToken, $userId] = find_admin_session($pdo);
if ($sessionToken === null) {
	fwrite(STDERR, "BLOCKED: no active admin session (sessions.type=1). Log into https://www.ecomae.com/CP/ once, then re-run.\n");
	// Still write API keys so price/catalog smoke can proceed.
	write_env_keys($envFile, $priceKey, $catalogKey, null);
	fwrite(STDOUT, "Wrote API keys into {$envFile}. Admin cookie still missing — login to Super CP, then re-run this script.\n");
	exit(3);
}

$cookie = 'admin_session=' . $sessionToken . '; admin_u_id=' . $userId;
write_env_keys($envFile, $priceKey, $catalogKey, $cookie);

fwrite(STDOUT, "OK wrote smoke credentials into {$envFile} (values not printed).\n");
fwrite(STDOUT, "price_key_prefix=" . substr($priceKey, 0, 24) . "…\n");
fwrite(STDOUT, "catalog_key_prefix=" . substr($catalogKey, 0, 24) . "…\n");
fwrite(STDOUT, "admin_u_id={$userId}\n");
fwrite(STDOUT, "Next: source {$envFile} && bash scripts/cloudpanel_validate_final_gate_env.sh\n");
exit(0);

/**
 * @param array<string,string> $env
 * @return array{0: PDO, 1: string}
 */
function open_smoke_pdo(array $env): array
{
	// 1) Explicit smoke DB overrides (operator-supplied PHP app user).
	$smokeHost = getenv('ECOMAE_SMOKE_DB_HOST') ?: '';
	$smokeDb = getenv('ECOMAE_SMOKE_DB_NAME') ?: '';
	$smokeUser = getenv('ECOMAE_SMOKE_DB_USER') ?: '';
	$smokePass = getenv('ECOMAE_SMOKE_DB_PASSWORD') ?: '';
	if ($smokeDb !== '' && $smokeUser !== '') {
		$host = $smokeHost !== '' ? $smokeHost : '127.0.0.1';
		$pdo = new PDO(
			"mysql:host={$host};dbname={$smokeDb};charset=utf8mb4",
			$smokeUser,
			$smokePass,
			[
				PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
				PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
			]
		);
		return [$pdo, "ECOMAE_SMOKE_DB_* user={$smokeUser} db={$smokeDb}"];
	}

	// 2) PHP site config.php (same grants the live PHP app uses).
	$docroot = getenv('ECOMAE_PHP_DOCROOT') ?: '';
	if ($docroot === '') {
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
				$docroot = $candidate;
				break;
			}
		}
	}
	if ($docroot !== '' && is_file($docroot . '/config.php')) {
		try {
			/** @var mixed $pdo */
			$pdo = null;
			require $docroot . '/config.php';
			if (isset($pdo) && $pdo instanceof PDO) {
				$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
				$pdo->setAttribute(PDO::ATTR_DEFAULT_FETCH_MODE, PDO::FETCH_ASSOC);
				return [$pdo, "php-config.php ({$docroot})"];
			}
		} catch (Throwable $e) {
			fwrite(STDERR, "WARN: PHP config.php PDO failed: " . $e->getMessage() . "\n");
		}
	}

	// 3) ASP.NET platform.env connection (may be read-mostly / no INSERT).
	$conn = $env['ConnectionStrings__TenantRegistry'] ?? '';
	if ($conn === '' || str_contains($conn, '<db_')) {
		fwrite(STDERR, "No usable DB credentials.\n");
		fwrite(STDERR, "Set ECOMAE_PHP_DOCROOT or ECOMAE_SMOKE_DB_*, or fill ConnectionStrings__TenantRegistry in {$GLOBALS['envFile']}.\n");
		exit(2);
	}

	$pdo = pdo_from_dotnet_conn($conn);
	return [$pdo, 'ConnectionStrings__TenantRegistry (platform.env)'];
}

function ensure_api_clients_readable(PDO $pdo): void
{
	try {
		$pdo->query('SELECT 1 FROM `epc_api_clients` LIMIT 1');
	} catch (Throwable $e) {
		fwrite(STDERR, "Table epc_api_clients missing or not readable.\n");
		fwrite(STDERR, "Create it once as MySQL root/admin (this issuer never runs CREATE):\n");
		fwrite(STDERR, <<<'SQL'
CREATE TABLE IF NOT EXISTS `epc_api_clients` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `client_key_hash` CHAR(64) NOT NULL,
  `client_key_prefix` VARCHAR(32) NOT NULL DEFAULT '',
  `product` VARCHAR(32) NOT NULL DEFAULT 'catalog',
  `label` VARCHAR(190) NOT NULL DEFAULT '',
  `contact_email` VARCHAR(190) NOT NULL DEFAULT '',
  `active` TINYINT(1) NOT NULL DEFAULT 1,
  `daily_limit` INT NOT NULL DEFAULT 1000,
  `calls_today` INT NOT NULL DEFAULT 0,
  `calls_reset_date` DATE NULL,
  `allowed_actions_json` TEXT NULL,
  `time_created` INT UNSIGNED NOT NULL DEFAULT 0,
  `time_updated` INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `client_key_hash` (`client_key_hash`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SQL
		);
		fwrite(STDERR, "Detail: " . $e->getMessage() . "\n");
		exit(1);
	}
}

/**
 * @return array<string,string>
 */
function parse_env_file(string $path): array
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

function pdo_from_dotnet_conn(string $conn): PDO
{
	$host = '127.0.0.1';
	$db = '';
	$user = '';
	$pass = '';
	foreach (explode(';', $conn) as $part) {
		$part = trim($part);
		if ($part === '' || !str_contains($part, '=')) {
			continue;
		}
		[$k, $v] = explode('=', $part, 2);
		$k = strtolower(trim($k));
		$v = trim($v);
		if (in_array($k, ['server', 'host', 'data source'], true)) {
			$host = $v;
		} elseif (in_array($k, ['database', 'initial catalog'], true)) {
			$db = $v;
		} elseif (in_array($k, ['user', 'uid', 'user id'], true)) {
			$user = $v;
		} elseif (in_array($k, ['password', 'pwd'], true)) {
			$pass = $v;
		}
	}
	if ($db === '' || $user === '') {
		throw new RuntimeException('Could not parse Server/Database/User from ConnectionStrings__TenantRegistry');
	}
	$dsn = "mysql:host={$host};dbname={$db};charset=utf8mb4";
	return new PDO($dsn, $user, $pass, [
		PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
		PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
	]);
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

/** @return array{0:?string,1:int} */
function find_admin_session(PDO $pdo): array
{
	$forced = trim((string) (getenv('ECOMAE_FORCE_ADMIN_COOKIE_HEADER') ?: ''));
	if ($forced !== '' && preg_match('/admin_session=([^;]+)/', $forced, $mSid) && preg_match('/admin_u_id=(\d+)/', $forced, $mUid)) {
		return [$mSid[1], (int) $mUid[1]];
	}

	// Prefer newest admin session rows. Column names vary slightly across installs.
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
	return [null, 0];
}

function write_env_keys(string $path, string $priceKey, string $catalogKey, ?string $cookie): void
{
	$lines = file($path, FILE_IGNORE_NEW_LINES);
	if ($lines === false) {
		throw new RuntimeException("Cannot read {$path}");
	}

	$set = [
		'ECOMAE_PRICE_LOOKUP_API_KEY' => $priceKey,
		'ECOMAE_CATALOG_API_KEY' => $catalogKey,
	];
	if ($cookie !== null && $cookie !== '') {
		$set['ECOMAE_ADMIN_COOKIE_HEADER'] = $cookie;
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
