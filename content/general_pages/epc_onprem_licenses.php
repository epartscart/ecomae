<?php
/**
 * On-Premises License Registry — issuance, activation, and core-engine bundle
 * signing for self-hosted ecomae ERP installs.
 *
 * A license row lives in `epc_onprem_licenses` on the platform DB. Activation
 * binds a license to one server fingerprint and returns a signed activation
 * certificate (RSA-SHA256) that the on-prem client can verify offline without
 * ever needing the private signing key.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_onprem_license_ensure_schema(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_onprem_licenses` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`license_key` VARCHAR(32) NOT NULL,
			`customer_name` VARCHAR(190) NOT NULL DEFAULT \'\',
			`tier` ENUM(\'standard\',\'professional\',\'enterprise\') NOT NULL DEFAULT \'standard\',
			`modules_json` TEXT NOT NULL,
			`users_max` INT UNSIGNED NOT NULL DEFAULT 25,
			`status` ENUM(\'issued\',\'active\',\'revoked\',\'expired\') NOT NULL DEFAULT \'issued\',
			`fingerprint` CHAR(64) NULL DEFAULT NULL,
			`hostname` VARCHAR(190) NOT NULL DEFAULT \'\',
			`ip` VARCHAR(45) NOT NULL DEFAULT \'\',
			`issued_at` INT NOT NULL DEFAULT 0,
			`activated_at` INT NULL DEFAULT NULL,
			`last_seen_at` INT NULL DEFAULT NULL,
			`expires_at` INT NULL DEFAULT NULL,
			`notes` VARCHAR(500) NOT NULL DEFAULT \'\',
			UNIQUE KEY `license_key` (`license_key`),
			KEY `status` (`status`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_onprem_health_log` (
			`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`license_key` VARCHAR(32) NOT NULL,
			`status` VARCHAR(40) NOT NULL DEFAULT \'\',
			`uptime` VARCHAR(40) NOT NULL DEFAULT \'\',
			`disk_free_gb` DECIMAL(10,1) NOT NULL DEFAULT 0,
			`memory_usage_mb` DECIMAL(10,1) NOT NULL DEFAULT 0,
			`php_version` VARCHAR(20) NOT NULL DEFAULT \'\',
			`db_size_mb` DECIMAL(10,1) NOT NULL DEFAULT 0,
			`last_backup` VARCHAR(40) NOT NULL DEFAULT \'\',
			`reported_at` INT NOT NULL DEFAULT 0,
			KEY `license_key_date` (`license_key`, `reported_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

/**
 * Issue a new on-premises license. Run from the CLI tool
 * (epc-onprem-license-generate.php) by a platform operator.
 */
function epc_onprem_license_generate(PDO $pdo, array $opts): array
{
	epc_onprem_license_ensure_schema($pdo);

	$year = date('Y');
	do {
		$part1 = strtoupper(substr(bin2hex(random_bytes(2)), 0, 4));
		$part2 = strtoupper(substr(bin2hex(random_bytes(2)), 0, 4));
		$key = "LIC-{$year}-{$part1}-{$part2}";
		$exists = $pdo->prepare('SELECT `id` FROM `epc_onprem_licenses` WHERE `license_key` = ?');
		$exists->execute(array($key));
	} while ($exists->fetch());

	$tier = in_array($opts['tier'] ?? '', array('standard', 'professional', 'enterprise'), true)
		? $opts['tier']
		: 'standard';
	$modules = isset($opts['modules']) && is_array($opts['modules']) ? $opts['modules'] : array('all');
	$usersMax = max(1, (int) ($opts['users_max'] ?? 25));
	$expiresDays = isset($opts['expires_days']) ? (int) $opts['expires_days'] : 365;
	$expiresAt = $expiresDays > 0 ? time() + ($expiresDays * 86400) : null;

	$pdo->prepare(
		'INSERT INTO `epc_onprem_licenses`
		(`license_key`, `customer_name`, `tier`, `modules_json`, `users_max`, `status`, `issued_at`, `expires_at`, `notes`)
		VALUES (?, ?, ?, ?, ?, \'issued\', ?, ?, ?)'
	)->execute(array(
		$key,
		(string) ($opts['customer_name'] ?? ''),
		$tier,
		json_encode($modules),
		$usersMax,
		time(),
		$expiresAt,
		(string) ($opts['notes'] ?? ''),
	));

	return array(
		'license_key' => $key,
		'tier' => $tier,
		'modules' => $modules,
		'users_max' => $usersMax,
		'expires_at' => $expiresAt,
	);
}

function epc_onprem_license_list(PDO $pdo, int $limit = 100): array
{
	epc_onprem_license_ensure_schema($pdo);
	$st = $pdo->prepare('SELECT * FROM `epc_onprem_licenses` ORDER BY `id` DESC LIMIT ?');
	$st->bindValue(1, $limit, PDO::PARAM_INT);
	$st->execute();
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_onprem_license_revoke(PDO $pdo, string $key): bool
{
	epc_onprem_license_ensure_schema($pdo);
	$st = $pdo->prepare('UPDATE `epc_onprem_licenses` SET `status` = \'revoked\' WHERE `license_key` = ?');
	$st->execute(array($key));
	return $st->rowCount() > 0;
}

function epc_onprem_license_fetch(PDO $pdo, string $key): ?array
{
	$st = $pdo->prepare('SELECT * FROM `epc_onprem_licenses` WHERE `license_key` = ? LIMIT 1');
	$st->execute(array($key));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

/**
 * Path to the RSA private key used to sign activation certificates.
 * Lives outside the web root on the license server (this platform), never
 * committed to git and never shipped to on-prem clients. Only the matching
 * public key (deploy/on-premises/license_public_key.pem) is distributed.
 */
function epc_onprem_license_signing_key_path(): string
{
	$path = getenv('EPC_LICENSE_SIGNING_KEY_PATH');
	return $path ?: '/etc/ecomae/license_signing_key.pem';
}

function epc_onprem_license_sign(array $certData): ?string
{
	$keyPath = epc_onprem_license_signing_key_path();
	if (!is_file($keyPath) || !is_readable($keyPath)) {
		return null;
	}
	$privateKey = openssl_pkey_get_private('file://' . $keyPath);
	if ($privateKey === false) {
		return null;
	}
	$payload = json_encode($certData, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	$signature = '';
	$ok = openssl_sign($payload, $signature, $privateKey, OPENSSL_ALGO_SHA256);
	return $ok ? base64_encode($signature) : null;
}

/**
 * Read the four core-engine files this platform depends on and package them
 * as a base64 tar.gz. These files are intentionally .gitignore'd (proprietary
 * runtime engine) and only ever leave this server through a validated,
 * per-license activation — never checked into any repository.
 */
function epc_onprem_core_bundle(): ?string
{
	$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/');
	if ($docRoot === '') {
		return null;
	}
	$files = array(
		'core/dp_core.php',
		'core/dp_content.php',
		'core/dp_module.php',
		'core/dp_template.php',
	);
	$existing = array();
	foreach ($files as $f) {
		if (is_file($docRoot . '/' . $f)) {
			$existing[] = $f;
		}
	}
	if (empty($existing)) {
		return null;
	}

	$tmpTar = tempnam(sys_get_temp_dir(), 'epc_core_bundle_') . '.tar.gz';
	$cmd = 'tar -czf ' . escapeshellarg($tmpTar) . ' -C ' . escapeshellarg($docRoot) . ' ' .
		implode(' ', array_map('escapeshellarg', $existing));
	exec($cmd, $out, $exitCode);
	if ($exitCode !== 0 || !is_file($tmpTar)) {
		@unlink($tmpTar);
		return null;
	}
	$bytes = file_get_contents($tmpTar);
	@unlink($tmpTar);
	return $bytes === false ? null : base64_encode($bytes);
}

function epc_onprem_license_activate(PDO $pdo, array $input): array
{
	epc_onprem_license_ensure_schema($pdo);

	$key = trim((string) ($input['license_key'] ?? ''));
	if (!preg_match('/^LIC-(\d{4})-([A-Z0-9]{4})-([A-Z0-9]{4})$/', $key)) {
		return array('success' => false, 'error' => 'invalid_key_format', 'message' => 'License key format is invalid.');
	}

	$row = epc_onprem_license_fetch($pdo, $key);
	if (!$row) {
		return array('success' => false, 'error' => 'not_found', 'message' => 'License key not recognized.');
	}
	if ($row['status'] === 'revoked') {
		return array('success' => false, 'error' => 'revoked', 'message' => 'This license has been revoked.');
	}
	if (!empty($row['expires_at']) && (int) $row['expires_at'] < time()) {
		return array('success' => false, 'error' => 'expired', 'message' => 'This license has expired.');
	}

	$fingerprint = (string) ($input['fingerprint'] ?? '');
	if ($fingerprint === '') {
		return array('success' => false, 'error' => 'missing_fingerprint', 'message' => 'Server fingerprint is required.');
	}

	// Allow re-activation on the same fingerprint (reinstall / re-run of
	// install.sh); reject a different fingerprint once already bound.
	if (!empty($row['fingerprint']) && $row['fingerprint'] !== $fingerprint) {
		return array(
			'success' => false,
			'error' => 'already_activated',
			'message' => 'This license is already activated on another server. Contact support to transfer it.',
		);
	}

	$pdo->prepare(
		'UPDATE `epc_onprem_licenses`
		 SET `status` = \'active\', `fingerprint` = ?, `hostname` = ?, `ip` = ?,
		     `activated_at` = COALESCE(`activated_at`, ?), `last_seen_at` = ?
		 WHERE `id` = ?'
	)->execute(array(
		$fingerprint,
		substr((string) ($input['hostname'] ?? ''), 0, 190),
		substr((string) ($input['ip'] ?? ''), 0, 45),
		time(),
		time(),
		(int) $row['id'],
	));

	$certData = array(
		'license_key' => $key,
		'fingerprint' => $fingerprint,
		'tier' => $row['tier'],
		'modules' => json_decode((string) $row['modules_json'], true) ?: array('all'),
		'users_max' => (int) $row['users_max'],
		'issued_at' => (int) $row['issued_at'],
		'expires_at' => $row['expires_at'] !== null ? date('c', (int) $row['expires_at']) : null,
	);

	$signature = epc_onprem_license_sign($certData);
	if ($signature === null) {
		return array(
			'success' => false,
			'error' => 'signing_unavailable',
			'message' => 'License server signing key is not configured. Contact ecomae support.',
		);
	}
	$certData['signature'] = $signature;

	return array(
		'success' => true,
		'activation_cert' => $certData,
		'core_bundle' => epc_onprem_core_bundle(),
	);
}

function epc_onprem_health_log(PDO $pdo, array $input): void
{
	epc_onprem_license_ensure_schema($pdo);
	$key = substr((string) ($input['license_key'] ?? ''), 0, 32);
	$pdo->prepare(
		'INSERT INTO `epc_onprem_health_log`
		(`license_key`, `status`, `uptime`, `disk_free_gb`, `memory_usage_mb`, `php_version`, `db_size_mb`, `last_backup`, `reported_at`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		$key,
		substr((string) ($input['status'] ?? ''), 0, 40),
		substr((string) ($input['uptime'] ?? ''), 0, 40),
		(float) ($input['disk_free_gb'] ?? 0),
		(float) ($input['memory_usage_mb'] ?? 0),
		substr((string) ($input['php_version'] ?? ''), 0, 20),
		(float) ($input['db_size_mb'] ?? 0),
		substr((string) ($input['last_backup'] ?? ''), 0, 40),
		time(),
	));
	$pdo->prepare('UPDATE `epc_onprem_licenses` SET `last_seen_at` = ? WHERE `license_key` = ?')
		->execute(array(time(), $key));
}
