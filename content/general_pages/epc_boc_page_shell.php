<?php
/**
 * BOC page shell — keep Super CP module pages inside the main BOS dashboard
 * + top menu (no nested CP "detail window" with legacy header/sidebar).
 *
 * Used by epc_cp_page_frame_* on Super CP, and callable from portal pages that
 * do not use the page frame helper.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_boc_kernel.php';
if (!function_exists('epc_boc_console_open') && is_file(__DIR__ . '/epc_boc_console.php')) {
	require_once __DIR__ . '/epc_boc_console.php';
}
// portal.php is optional at require-time (CLI tests); live CP always has it.
if (is_file(__DIR__ . '/epc_portal.php') && !function_exists('epc_portal_is_super_cp_host')) {
	require_once __DIR__ . '/epc_portal.php';
}

/**
 * Normalize a CP content URL to compare against epc_boc_areas() paths.
 */
function epc_boc_normalize_content_url(string $url): string
{
	$url = strtolower(trim($url, '/'));
	// Strip query string if present
	$q = strpos($url, '?');
	if ($q !== false) {
		$url = substr($url, 0, $q);
	}
	return $url;
}

/**
 * Resolve BOC area id from the current (or given) content URL.
 *
 * @return array{id:string,area:array<string,mixed>}|null
 */
function epc_boc_resolve_area(?string $contentUrl = null): ?array
{
	if ($contentUrl === null) {
		global $DP_Content;
		$contentUrl = (isset($DP_Content) && is_object($DP_Content))
			? (string) ($DP_Content->url ?? '')
			: '';
	}
	$url = epc_boc_normalize_content_url($contentUrl);
	if ($url === '') {
		return null;
	}
	$areas = epc_boc_areas();
	$bestId = '';
	$bestLen = -1;
	$bestArea = null;
	foreach ($areas as $id => $area) {
		$path = epc_boc_normalize_content_url((string) ($area['path'] ?? ''));
		if ($path === '') {
			continue;
		}
		// Exact or path-prefix match (e.g. erp shell query paths already stripped).
		$pathBase = $path;
		$pq = strpos($pathBase, '?');
		if ($pq !== false) {
			$pathBase = substr($pathBase, 0, $pq);
		}
		if ($url === $pathBase || strpos($url, $pathBase . '/') === 0) {
			$len = strlen($pathBase);
			if ($len > $bestLen) {
				$bestLen = $len;
				$bestId = (string) $id;
				$bestArea = $area;
			}
		}
	}
	if ($bestId === '' || !is_array($bestArea)) {
		return null;
	}
	return array('id' => $bestId, 'area' => $bestArea);
}

/**
 * Should this Super CP page render inside the BOS console shell?
 */
function epc_boc_should_use_page_shell(?string $contentUrl = null): bool
{
	if (!empty($GLOBALS['epc_cp_boc_page']) || !empty($GLOBALS['epc_boc_page_shell_open'])) {
		return false; // already inside BOC
	}
	if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
		return false;
	}
	if ($contentUrl === null) {
		global $DP_Content;
		$contentUrl = (isset($DP_Content) && is_object($DP_Content))
			? (string) ($DP_Content->url ?? '')
			: '';
	}
	$url = epc_boc_normalize_content_url((string) $contentUrl);
	if ($url === '' || $url === 'control') {
		// Home /cp/control already opens BOC itself via super dashboard.
		return false;
	}
	// Never wrap auth / bare login endpoints.
	$deny = array('login', 'logout', 'control/login', 'control/logout');
	if (in_array($url, $deny, true)) {
		return false;
	}
	// Super CP: every module detail stays in the same BOS topnav shell as the
	// Operations Command Center (no nested legacy "detail window").
	return true;
}

/**
 * Open the BOS topnav shell around module content (idempotent).
 *
 * @param array<string,mixed> $opts active|title|subtitle|base|operator|nav|scope
 */
function epc_boc_page_shell_open(array $opts = array()): void
{
	if (!empty($GLOBALS['epc_boc_page_shell_open']) || !empty($GLOBALS['epc_cp_boc_page'])) {
		return;
	}
	if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
		return;
	}

	global $DP_Config, $db_link, $DP_Content;
	$backend = isset($DP_Config) && is_object($DP_Config)
		? (string) ($DP_Config->backend_dir ?? 'cp')
		: 'cp';
	$base = '/' . trim($backend, '/');

	$resolved = epc_boc_resolve_area();
	$active = (string) ($opts['active'] ?? ($resolved['id'] ?? ''));
	$title = (string) ($opts['title'] ?? '');
	if ($title === '' && is_array($resolved) && isset($resolved['area']['label'])) {
		$title = (string) $resolved['area']['label'];
	}
	if ($title === '' && isset($DP_Content) && is_object($DP_Content)) {
		$title = (string) ($DP_Content->value ?? 'Operations');
	}
	if ($title === '') {
		$title = 'Operations';
	}

	$operator = (string) ($opts['operator'] ?? '');
	if ($operator === '' && class_exists('DP_User') && method_exists('DP_User', 'getUserId')) {
		// Prefer getName when available
		if (method_exists('DP_User', 'getUserName')) {
			$operator = (string) DP_User::getUserName();
		} elseif (method_exists('DP_User', 'getName')) {
			$operator = (string) DP_User::getName();
		}
	}
	if ($operator === '') {
		$operator = 'Operator';
	}

	$nav = null;
	if (isset($opts['nav']) && is_array($opts['nav'])) {
		$nav = $opts['nav'];
	} elseif (isset($db_link) && $db_link instanceof PDO && function_exists('epc_boc_nav_for_user') && class_exists('DP_User')) {
		$uid = method_exists('DP_User', 'getUserId') ? (int) DP_User::getUserId() : 0;
		$nav = epc_boc_nav_for_user($db_link, $uid);
	} else {
		$nav = epc_boc_nav();
	}

	$ctx = array(
		'active' => $active,
		'title' => $title,
		'subtitle' => (string) ($opts['subtitle'] ?? ''),
		'base' => $base,
		'operator' => $operator,
		'env' => (string) ($opts['env'] ?? 'Production'),
		'nav' => $nav,
		'scope' => (string) ($opts['scope'] ?? 'All units · Fleet'),
		'layout' => 'top',
	);
	epc_boc_console_open($ctx);
	$GLOBALS['epc_boc_page_shell_open'] = true;
	// Suppress the legacy CMS page header card (the nested "detail window" chrome).
	$GLOBALS['epc_cp_skip_page_header'] = true;
}

/**
 * Close the BOS shell if we opened it.
 */
function epc_boc_page_shell_close(): void
{
	if (empty($GLOBALS['epc_boc_page_shell_open'])) {
		return;
	}
	epc_boc_console_close();
	$GLOBALS['epc_boc_page_shell_open'] = false;
}
