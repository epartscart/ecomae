<?php
/**
 * Crossbase HTML disk cache (fresh TTL + stale fallback when provider is down).
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_crossbase_cache_dir()
{
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/cache/crossbase';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	return $dir;
}

function epc_crossbase_cache_key_for_article($article_input)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
	$key = docpart_normalize_article_for_price($article_input);
	if ($key === '') {
		$key = preg_replace('/[^A-Za-z0-9]/', '', (string)$article_input);
	}
	return $key;
}

function epc_crossbase_cache_path($article_input)
{
	$key = epc_crossbase_cache_key_for_article($article_input);
	if ($key === '') {
		return '';
	}
	return epc_crossbase_cache_dir() . '/' . $key . '.html';
}

function epc_crossbase_cache_read($article_input, $fresh_ttl_seconds = 21600, $allow_stale = true)
{
	$path = epc_crossbase_cache_path($article_input);
	if ($path === '' || !is_file($path)) {
		return '';
	}
	$age = time() - (int)filemtime($path);
	if ($fresh_ttl_seconds > 0 && $age <= $fresh_ttl_seconds) {
		$html = @file_get_contents($path);
		return (is_string($html) && $html !== '') ? $html : '';
	}
	if (!$allow_stale) {
		return '';
	}
	$html = @file_get_contents($path);
	return (is_string($html) && strlen($html) > 400) ? $html : '';
}

function epc_crossbase_cache_write($article_input, $html)
{
	$html = (string)$html;
	if ($html === '' || strlen($html) <= 400) {
		return false;
	}
	$path = epc_crossbase_cache_path($article_input);
	if ($path === '') {
		return false;
	}
	return @file_put_contents($path, $html, LOCK_EX) !== false;
}

function epc_crossbase_cache_stats()
{
	$dir = epc_crossbase_cache_dir();
	$files = glob($dir . '/*.html');
	if (!is_array($files)) {
		$files = array();
	}
	$fresh = 0;
	$stale = 0;
	$now = time();
	foreach ($files as $file) {
		if (!is_file($file)) {
			continue;
		}
		$age = $now - (int)filemtime($file);
		if ($age <= 21600) {
			$fresh++;
		} else {
			$stale++;
		}
	}
	return array(
		'files_total' => count($files),
		'files_fresh' => $fresh,
		'files_stale' => $stale,
	);
}
