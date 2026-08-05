<?php
/**
 * Copy PHP $DP_Config->secret_succession into /etc/ecomae-aspnet/platform.env
 * as EcomAE__SecretSuccession so ASP.NET /auth/login/admin accepts the same
 * admin credentials as PHP CP/ERP/BOS.
 *
 * NEVER prints the secret value.
 *
 * Usage (from repo root, as root):
 *   ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES php scripts/php/sync_secret_succession_to_platform_env.php
 */
declare(strict_types=1);

if (getenv('ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION') !== 'YES') {
	fwrite(STDERR, "Refusing without ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES\n");
	exit(2);
}

$envFile = getenv('ECOMAE_PLATFORM_ENV') ?: '/etc/ecomae-aspnet/platform.env';
$docroot = getenv('ECOMAE_PHP_DOCROOT') ?: '';

require_once __DIR__ . '/_smoke_db_bootstrap.php';

if ($docroot === '' || !is_file(rtrim($docroot, '/') . '/config.php')) {
	$docroot = smoke_resolve_php_docroot();
}
if ($docroot === '' || !is_file(rtrim($docroot, '/') . '/config.php')) {
	fwrite(STDERR, "BLOCKED: could not find PHP config.php (set ECOMAE_PHP_DOCROOT)\n");
	exit(1);
}
$docroot = rtrim($docroot, '/');

$configPath = $docroot . '/config.php';
$_SERVER['DOCUMENT_ROOT'] = $docroot;
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

try {
	require_once $configPath;
	if (!class_exists('DP_Config', false)) {
		fwrite(STDERR, "BLOCKED: DP_Config class missing after loading config.php\n");
		exit(1);
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
} catch (Throwable $e) {
	fwrite(STDERR, "BLOCKED: load DP_Config failed: " . $e->getMessage() . "\n");
	exit(1);
}

$secret = trim((string) ($cfg->secret_succession ?? ''));
if ($secret === '') {
	fwrite(STDERR, "BLOCKED: PHP secret_succession is empty in {$docroot}\n");
	exit(1);
}

$dir = dirname($envFile);
if (!is_dir($dir)) {
	if (!mkdir($dir, 0750, true) && !is_dir($dir)) {
		fwrite(STDERR, "BLOCKED: cannot create {$dir}\n");
		exit(1);
	}
}

$existing = is_file($envFile) ? (string) file_get_contents($envFile) : '';
$line = 'EcomAE__SecretSuccession=' . $secret;
$replaced = false;
$outLines = [];
foreach (preg_split("/\r\n|\n|\r/", $existing) ?: [] as $raw) {
	if (preg_match('/^\s*(?:#\s*)?(?:EcomAE__SecretSuccession|ECOMAE_SECRET_SUCCESSION|EcomAE_SecretSuccession)\s*=/', $raw)) {
		if (!$replaced) {
			$outLines[] = $line;
			$replaced = true;
		}
		continue;
	}
	$outLines[] = $raw;
}
if (!$replaced) {
	if ($outLines !== [] && end($outLines) !== '') {
		$outLines[] = '';
	}
	$outLines[] = '# PHP-compatible login bridge (same CP/ERP/BOS credentials as PHP)';
	$outLines[] = $line;
}

$tmp = $envFile . '.tmp.' . getmypid();
$payload = implode("\n", $outLines);
if ($payload !== '' && !str_ends_with($payload, "\n")) {
	$payload .= "\n";
}
if (file_put_contents($tmp, $payload) === false) {
	fwrite(STDERR, "BLOCKED: cannot write temp env file\n");
	exit(1);
}
chmod($tmp, 0600);
if (!rename($tmp, $envFile)) {
	@unlink($tmp);
	fwrite(STDERR, "BLOCKED: cannot replace {$envFile}\n");
	exit(1);
}
chmod($envFile, 0600);

fwrite(STDOUT, "OK: wrote EcomAE__SecretSuccession to {$envFile} from PHP docroot={$docroot}\n");
fwrite(STDOUT, "OK: secret length=" . strlen($secret) . " (value not printed)\n");
fwrite(STDOUT, "NEXT: systemctl restart ecomae-platform.service\n");
fwrite(STDOUT, "THEN: bash scripts/cloudpanel_verify_secret_succession_configured.sh\n");
exit(0);
