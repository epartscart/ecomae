<?php
/**
 * Request-scoped cache — APCu when available, else JSON file cache (5–15 min TTL).
 */
defined('_ASTEXE_') or die('No access');

function epc_perf_cache_dir(): string
{
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_cache';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	return $dir;
}

function epc_perf_cache_key_safe(string $key): string
{
	return preg_replace('/[^a-zA-Z0-9_.\-]/', '_', $key);
}

/** @return mixed|null */
function epc_perf_cache_get(string $key)
{
	if (function_exists('apcu_fetch')) {
		$ok = false;
		$v = apcu_fetch($key, $ok);
		if ($ok) {
			return $v;
		}
	}
	$file = epc_perf_cache_dir() . '/' . epc_perf_cache_key_safe($key) . '.json';
	if (!is_file($file)) {
		return null;
	}
	$raw = @file_get_contents($file);
	if ($raw === false || $raw === '') {
		return null;
	}
	$data = json_decode($raw, true);
	if (!is_array($data) || !isset($data['exp'], $data['v'])) {
		return null;
	}
	if ((int) $data['exp'] < time()) {
		@unlink($file);
		return null;
	}
	return $data['v'];
}

/** @param mixed $value */
function epc_perf_cache_set(string $key, $value, int $ttl = 300): void
{
	if ($ttl < 1) {
		return;
	}
	if (function_exists('apcu_store')) {
		@apcu_store($key, $value, $ttl);
	}
	$file = epc_perf_cache_dir() . '/' . epc_perf_cache_key_safe($key) . '.json';
	@file_put_contents(
		$file,
		json_encode(array('exp' => time() + $ttl, 'v' => $value), JSON_UNESCAPED_UNICODE | JSON_INVALID_UTF8_SUBSTITUTE),
		LOCK_EX
	);
}

/**
 * @template T
 * @param callable():T $fn
 * @return T
 */
function epc_perf_cache_remember(string $key, int $ttl, callable $fn)
{
	$cached = epc_perf_cache_get($key);
	if ($cached !== null) {
		return $cached;
	}
	$value = $fn();
	epc_perf_cache_set($key, $value, $ttl);
	return $value;
}

/**
 * CP sidebar — cache control_groups + control_items (visibility still filtered per user).
 *
 * @return array{groups: array<int,array<string,mixed>>, items: array<int,array<string,mixed>>}
 */
function epc_cp_menu_cache(PDO $pdo): array
{
	$dbName = 'default';
	try {
		$dbName = (string) $pdo->query('SELECT DATABASE()')->fetchColumn();
	} catch (Throwable $e) {
		// keep default key
	}
	$key = 'epc_cp_menu_rows:v1:' . $dbName;
	return epc_perf_cache_remember($key, 300, static function () use ($pdo) {
		$groups = array();
		$st = $pdo->query('SELECT * FROM `control_groups` ORDER BY `order` ASC');
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$groups[] = $row;
		}
		$items = array();
		$st = $pdo->query('SELECT * FROM `control_items` ORDER BY `order` ASC');
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$items[] = $row;
		}
		return array('groups' => $groups, 'items' => $items);
	});
}

/** Delete file-cache entries whose key starts with $prefix (APCu keys are not cleared). */
function epc_perf_cache_bust_prefix(string $prefix): int
{
	$dir = epc_perf_cache_dir();
	if (!is_dir($dir)) {
		return 0;
	}
	$removed = 0;
	$safePrefix = epc_perf_cache_key_safe($prefix);
	foreach (glob($dir . '/*.json') ?: array() as $file) {
		$base = basename($file, '.json');
		if ($safePrefix === '' || strpos($base, $safePrefix) === 0) {
			if (@unlink($file)) {
				$removed++;
			}
		}
	}
	return $removed;
}
