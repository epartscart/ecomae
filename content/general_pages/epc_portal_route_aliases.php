<?php
/**
 * Canonical CP / ERP / BOS doorway aliases.
 *
 * Operators and tenants commonly type uppercase surface names (e.g. /CP, /ERP,
 * /BOS). Keep one canonical lowercase URL internally so routing, cookies,
 * static assets, and cache keys remain stable across Apache/nginx setups.
 */
defined('_ASTEXE_') or die('No access');

function epc_portal_alias_request_path(?string $uri = null): string
{
	if ($uri === null) {
		$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '/';
	}
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return '/';
	}
	$path = '/' . trim(str_replace('\\', '/', $path), '/');
	return $path === '//' ? '/' : $path;
}

function epc_portal_alias_canonical_surface_path(string $path): ?string
{
	if (!preg_match('#^/(cp|erp|bos)(/.*)?$#i', $path, $m)) {
		return null;
	}
	$surface = strtolower($m[1]);
	$rest = isset($m[2]) ? (string) $m[2] : '';
	$canonical = '/' . $surface . $rest;
	return $canonical !== $path ? $canonical : null;
}

function epc_portal_alias_redirect_uppercase_surfaces(): bool
{
	if (PHP_SAPI === 'cli' || headers_sent()) {
		return false;
	}
	$path = epc_portal_alias_request_path();
	$canonical = epc_portal_alias_canonical_surface_path($path);
	if ($canonical === null) {
		return false;
	}
	$query = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== '' ? ('?' . $_SERVER['QUERY_STRING']) : '';
	header('Location: ' . $canonical . $query, true, 301);
	return true;
}

function epc_portal_alias_try_bos_entry(): bool
{
	$path = epc_portal_alias_request_path();
	if ($path !== '/bos' && strpos($path, '/bos/') !== 0) {
		return false;
	}
	$entry = $_SERVER['DOCUMENT_ROOT'] . '/bos/index.php';
	if (!is_file($entry)) {
		return false;
	}
	require $entry;
	return true;
}
