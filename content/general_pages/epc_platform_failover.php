<?php
/**
 * EPC platform failover — mode file, status JSON, health probes.
 * Backup-server-first: static splash + JSON work when PHP/DB are partial.
 */
declare(strict_types=1);

defined('_ASTEXE_') or define('_ASTEXE_', 1);

function epc_failover_valid_modes(): array
{
	return array(
		'primary_ok',
		'primary_down',
		'backup_active',
		'failback_sync',
		'failback_redirect',
	);
}

function epc_failover_docroot(): string
{
	if (defined('EPC_FAILOVER_DOCROOT') && EPC_FAILOVER_DOCROOT !== '') {
		return rtrim((string) EPC_FAILOVER_DOCROOT, '/');
	}
	$root = $_SERVER['DOCUMENT_ROOT'] ?? '';
	if ($root !== '' && is_dir($root)) {
		return rtrim($root, '/');
	}
	return dirname(__DIR__, 2);
}

function epc_failover_mode_paths(): array
{
	$base = epc_failover_docroot();
	return array(
		$base . '/epc-platform-status.mode',
		$base . '/var/epc-platform-status.mode',
	);
}

function epc_failover_json_paths(): array
{
	$base = epc_failover_docroot();
	return array(
		$base . '/epc-platform-status.json',
	);
}

function epc_failover_config_paths(): array
{
	$base = epc_failover_docroot();
	return array(
		$base . '/epc-platform-failover.config.json',
		$base . '/var/epc-platform-failover.config.json',
	);
}

function epc_failover_default_config(): array
{
	return array(
		'backup_base_url' => '',
		'primary_url' => 'https://www.ecomae.com/',
		'poll_interval_sec' => 60,
		'show_cloud_primary_badge' => false,
	);
}

function epc_failover_read_config(): array
{
	$cfg = epc_failover_default_config();
	foreach (epc_failover_config_paths() as $path) {
		if (!is_readable($path)) {
			continue;
		}
		$raw = @file_get_contents($path);
		if ($raw === false || $raw === '') {
			continue;
		}
		$data = json_decode($raw, true);
		if (is_array($data)) {
			$cfg = array_merge($cfg, $data);
		}
	}
	$cfg['poll_interval_sec'] = max(30, min(300, (int) ($cfg['poll_interval_sec'] ?? 60)));
	$cfg['backup_base_url'] = rtrim((string) ($cfg['backup_base_url'] ?? ''), '/');
	$cfg['primary_url'] = rtrim((string) ($cfg['primary_url'] ?? 'https://www.ecomae.com/'), '/') . '/';
	$cfg['show_cloud_primary_badge'] = !empty($cfg['show_cloud_primary_badge']);
	return $cfg;
}

function epc_failover_write_config(array $patch): bool
{
	$cfg = array_merge(epc_failover_read_config(), $patch);
	if (isset($patch['backup_base_url'])) {
		$cfg['backup_base_url'] = rtrim((string) $patch['backup_base_url'], '/');
	}
	if (isset($patch['primary_url'])) {
		$cfg['primary_url'] = rtrim((string) $patch['primary_url'], '/') . '/';
	}
	$json = json_encode($cfg, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
	if ($json === false) {
		return false;
	}
	$ok = false;
	foreach (epc_failover_config_paths() as $path) {
		$dir = dirname($path);
		if (!is_dir($dir)) {
			@mkdir($dir, 0755, true);
		}
		if (@file_put_contents($path, $json . "\n", LOCK_EX) !== false) {
			$ok = true;
		}
	}
	if ($ok) {
		$mode = epc_failover_read_mode_file() ?? 'primary_ok';
		epc_failover_write_json_mirror(epc_failover_build_status($mode));
	}
	return $ok;
}

function epc_failover_primary_health_for_mode(string $mode): string
{
	if ($mode === 'primary_ok') {
		return 'ok';
	}
	if (in_array($mode, array('failback_sync', 'failback_redirect'), true)) {
		return 'recovering';
	}
	return 'down';
}

function epc_failover_env_label(string $mode): string
{
	if ($mode === 'backup_active' || $mode === 'primary_down') {
		return 'local_premises';
	}
	if (in_array($mode, array('failback_sync', 'failback_redirect'), true)) {
		return 'cloud_restoring';
	}
	return 'cloud_primary';
}

/** Seconds before status JSON mirror is refreshed (reduces disk + PHP on polls). */
function epc_failover_status_cache_ttl(): int
{
	$env = getenv('EPC_FAILOVER_STATUS_TTL');
	if ($env !== false && ctype_digit(trim($env))) {
		return max(15, min(300, (int) trim($env)));
	}
	return 60;
}

function epc_failover_json_mirror_age_sec(): ?int
{
	foreach (epc_failover_json_paths() as $path) {
		if (!is_readable($path)) {
			continue;
		}
		$mtime = @filemtime($path);
		if ($mtime === false) {
			return null;
		}
		return max(0, time() - (int) $mtime);
	}
	return null;
}

function epc_failover_primary_probe_url(): string
{
	$env = getenv('EPC_FAILOVER_PRIMARY_URL');
	if ($env !== false && trim($env) !== '') {
		return trim($env);
	}
	return 'https://www.ecomae.com/epc-platform-status.php?ping=1';
}

function epc_failover_read_mode_file(): ?string
{
	foreach (epc_failover_mode_paths() as $path) {
		if (!is_readable($path)) {
			continue;
		}
		$raw = trim((string) @file_get_contents($path));
		if ($raw !== '' && in_array($raw, epc_failover_valid_modes(), true)) {
			return $raw;
		}
	}
	return null;
}

function epc_failover_write_mode_file(string $mode, array $extra = array()): bool
{
	if (!in_array($mode, epc_failover_valid_modes(), true)) {
		return false;
	}
	$ok = false;
	foreach (epc_failover_mode_paths() as $path) {
		$dir = dirname($path);
		if (!is_dir($dir)) {
			@mkdir($dir, 0755, true);
		}
		if (@file_put_contents($path, $mode . "\n", LOCK_EX) !== false) {
			$ok = true;
		}
	}
	if ($ok) {
		epc_failover_write_json_mirror(epc_failover_build_status($mode, $extra));
	}
	return $ok;
}

function epc_failover_probe_primary(int $timeoutSec = 4): array
{
	$url = epc_failover_primary_probe_url();
	$probeHost = strtolower((string) parse_url($url, PHP_URL_HOST));
	$reqHost = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if ($reqHost !== '' && strpos($reqHost, ':') !== false) {
		$reqHost = explode(':', $reqHost, 2)[0];
	}
	if ($probeHost !== '' && $reqHost !== '' && ($probeHost === $reqHost || $probeHost === 'www.' . $reqHost || 'www.' . $probeHost === $reqHost)) {
		return array('ok' => true, 'http_code' => 200, 'error' => null, 'local' => true);
	}
	$ch = curl_init($url);
	if ($ch === false) {
		return array('ok' => false, 'http_code' => 0, 'error' => 'curl_init failed');
	}
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_CONNECTTIMEOUT => $timeoutSec,
		CURLOPT_TIMEOUT => $timeoutSec,
		CURLOPT_SSL_VERIFYPEER => true,
		CURLOPT_USERAGENT => 'EPC-Failover-Probe/1.0',
	));
	$body = curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$err = curl_error($ch);
	curl_close($ch);
	$ok = ($code >= 200 && $code < 400);
	if ($ok && is_string($body) && $body !== '') {
		$json = json_decode($body, true);
		if (is_array($json) && isset($json['mode']) && $json['mode'] === 'primary_ok') {
			$ok = true;
		} elseif (is_array($json) && isset($json['mode'])) {
			$ok = ($json['mode'] === 'primary_ok');
		}
	}
	return array('ok' => $ok, 'http_code' => $code, 'error' => $err !== '' ? $err : null);
}

function epc_failover_resolve_mode(bool $autoProbe = false): string
{
	$fileMode = epc_failover_read_mode_file();
	if ($fileMode !== null) {
		return $fileMode;
	}
	if ($autoProbe) {
		$probe = epc_failover_probe_primary(3);
		return $probe['ok'] ? 'primary_ok' : 'primary_down';
	}
	return 'primary_ok';
}

function epc_failover_build_status(string $mode, array $extra = array()): array
{
	$now = gmdate('c');
	$cfg = epc_failover_read_config();
	$labels = array(
		'primary_ok' => 'Cloud primary is healthy',
		'primary_down' => 'Cloud unreachable — switching to local premises backup',
		'backup_active' => 'Local premises backup active',
		'failback_sync' => 'Cloud primary restored — syncing',
		'failback_redirect' => 'Returning to cloud primary',
	);
	$payload = array_merge(array(
		'ok' => true,
		'mode' => $mode,
		'label' => $labels[$mode] ?? $mode,
		'updated_at' => $now,
		'primary_url' => $cfg['primary_url'],
		'backup_base_url' => $cfg['backup_base_url'],
		'primary_health' => epc_failover_primary_health_for_mode($mode),
		'env' => epc_failover_env_label($mode),
		'poll_interval_sec' => (int) $cfg['poll_interval_sec'],
		'show_cloud_primary_badge' => (bool) $cfg['show_cloud_primary_badge'],
		'backup_hint' => 'local premises / laptop standby',
		'splash_url' => '/epc-platform-splash.html',
		'status_php' => '/epc-platform-status.php',
		'status_json' => '/epc-platform-status.json',
		'snippet_js' => '/epc-failover-snippet.js',
	), $extra);
	if ($mode === 'failback_redirect') {
		$payload['redirect_seconds'] = isset($extra['redirect_seconds']) ? (int) $extra['redirect_seconds'] : 15;
	}
	return $payload;
}

/** Fast path for templates: mode file only (no probe, no JSON rewrite). */
function epc_failover_read_mode_fast(): string
{
	$m = epc_failover_read_mode_file();
	return $m !== null ? $m : 'primary_ok';
}

function epc_failover_read_json_mirror(): ?array
{
	foreach (epc_failover_json_paths() as $path) {
		if (!is_readable($path)) {
			continue;
		}
		$raw = @file_get_contents($path);
		if ($raw === false || $raw === '') {
			continue;
		}
		$data = json_decode($raw, true);
		if (is_array($data) && !empty($data['mode'])) {
			return $data;
		}
	}
	return null;
}

function epc_failover_write_json_mirror(array $status): bool
{
	$json = json_encode($status, JSON_UNESCAPED_SLASHES);
	if ($json === false) {
		return false;
	}
	$ok = false;
	foreach (epc_failover_json_paths() as $path) {
		$dir = dirname($path);
		if (!is_dir($dir)) {
			@mkdir($dir, 0755, true);
		}
		if (@file_put_contents($path, $json . "\n", LOCK_EX) !== false) {
			$ok = true;
		}
	}
	return $ok;
}

function epc_failover_current_status(bool $autoProbe = false): array
{
	$ttl = epc_failover_status_cache_ttl();
	$age = epc_failover_json_mirror_age_sec();
	$mirror = epc_failover_read_json_mirror();
	if (is_array($mirror) && !empty($mirror['mode'])) {
		$mode = (string) $mirror['mode'];
		if (in_array($mode, epc_failover_valid_modes(), true)) {
			if (!$autoProbe && ($age === null || $age < $ttl)) {
				return $mirror;
			}
			if (!$autoProbe) {
				$fileMode = epc_failover_read_mode_file();
				$mode = $fileMode !== null ? $fileMode : $mode;
				$status = epc_failover_build_status($mode);
				epc_failover_write_json_mirror($status);
				return $status;
			}
			return $mirror;
		}
	}
	$mode = epc_failover_resolve_mode($autoProbe);
	$status = epc_failover_build_status($mode);
	epc_failover_write_json_mirror($status);
	return $status;
}

/** Operator-only: deploy token or Super CP session (avoids public ?probe= curl loops). */
function epc_failover_probe_authorized(): bool
{
	if (!empty($_GET['token']) || !empty($_POST['token'])) {
		if (!function_exists('epc_deploy_token')) {
			$auth = epc_failover_docroot() . '/epc_deploy_auth.php';
			if (is_readable($auth)) {
				require_once $auth;
			}
		}
		if (function_exists('epc_deploy_token')) {
			$token = (string) ($_POST['token'] ?? $_GET['token'] ?? '');
			if ($token !== '' && hash_equals(epc_deploy_token(), $token)) {
				return true;
			}
		}
	}
	if (session_status() !== PHP_SESSION_ACTIVE) {
		@session_start();
	}
	if (!empty($_SESSION['user_id'])) {
		$root = epc_failover_docroot();
		if (is_readable($root . '/content/general_pages/epc_portal.php')) {
			require_once $root . '/content/general_pages/epc_portal.php';
			if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
				return true;
			}
		}
	}
	return false;
}

function epc_failover_should_show_splash(?string $mode = null): bool
{
	if ($mode === null) {
		$mode = epc_failover_resolve_mode(false);
	}
	return in_array($mode, array('primary_down', 'backup_active', 'failback_sync', 'failback_redirect'), true);
}

function epc_failover_splash_preview_requested(): bool
{
	return !empty($_GET['epc_splash_preview']) || !empty($_GET['preview']);
}

function epc_failover_host_label(): string
{
	$host = $_SERVER['HTTP_HOST'] ?? 'store';
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	return $host;
}
