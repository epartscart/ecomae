<?php
/**
 * Shared CP control menu tree (groups + ACL-filtered items).
 * Used by left sidebar and the CP top mega-menu.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_cp_menu.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';

// control_helper needs backend_dir — resolve after portal bootstrap globals exist.
if (!function_exists('is_anable')) {
	global $DP_Config;
	$backendDir = (isset($DP_Config) && is_object($DP_Config) && !empty($DP_Config->backend_dir))
		? (string) $DP_Config->backend_dir
		: 'cp';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $backendDir . '/content/control/control_helper.php';
}

/**
 * Font Awesome icons for known CP group caption keys.
 *
 * @return array<string,string>
 */
function epc_cp_nav_group_icon_map(): array
{
	return array(
		'744' => 'fa-shopping-cart',
		'epc_cp_group_customers' => 'fa-users',
		'epc_cp_group_documents' => 'fa-file-text-o',
		'epc_cp_group_erp' => 'fa-university',
		'epc_cp_group_procurement' => 'fa-truck',
		'epc_cp_group_channels' => 'fa-share-alt',
		'epc_cp_group_logistics' => 'fa-cubes',
		'epc_cp_group_ai' => 'fa-magic',
		'epc_cp_group_marketing' => 'fa-bullhorn',
		'epc_cp_group_payments' => 'fa-credit-card',
		'epc_cp_group_integrations' => 'fa-plug',
		'epc_cp_group_portal' => 'fa-cog',
		'epc_cp_group_tenant_hub' => 'fa-sitemap',
		'epc_cp_group_operator' => 'fa-shield',
	);
}

/**
 * Short labels for the top bar (full caption still used in panels).
 *
 * @return array<string,string>
 */
function epc_cp_nav_group_short_map(): array
{
	return array(
		'744' => 'Commerce',
		'epc_cp_group_customers' => 'Customers',
		'epc_cp_group_documents' => 'Documents',
		'epc_cp_group_erp' => 'ERP',
		'epc_cp_group_procurement' => 'Purchase',
		'epc_cp_group_channels' => 'Channels',
		'epc_cp_group_logistics' => 'Logistics',
		'epc_cp_group_ai' => 'AI',
		'epc_cp_group_marketing' => 'Marketing',
		'epc_cp_group_payments' => 'Payments',
		'epc_cp_group_integrations' => 'Integrations',
		'epc_cp_group_portal' => 'Portal',
		'epc_cp_group_tenant_hub' => 'Platform',
		'epc_cp_group_operator' => 'Operator',
	);
}

function epc_cp_nav_group_icon(string $captionKey): string
{
	$map = epc_cp_nav_group_icon_map();
	return $map[$captionKey] ?? 'fa-folder-o';
}

function epc_cp_nav_group_short(string $captionKey, string $fallback = ''): string
{
	$map = epc_cp_nav_group_short_map();
	if (isset($map[$captionKey])) {
		return $map[$captionKey];
	}
	$fallback = trim($fallback);
	if ($fallback === '') {
		return 'Module';
	}
	// Keep top bar compact.
	if (function_exists('mb_strlen') && mb_strlen($fallback) > 14) {
		return rtrim(mb_substr($fallback, 0, 13)) . '…';
	}
	if (strlen($fallback) > 14) {
		return rtrim(substr($fallback, 0, 13)) . '…';
	}
	return $fallback;
}

/**
 * Build ACL-filtered, ordered CP nav groups.
 *
 * @return list<array{key:string,caption:string,caption_key:string,subtitle:string,icon:string,short:string,tier:string,items:list<array<string,mixed>>}>
 */
function epc_cp_build_nav_tabs(): array
{
	global $DP_Config, $db_link;

	static $cached = null;
	if (is_array($cached)) {
		return $cached;
	}

	$tabs = array();
	$epcCpMenuCache = ($db_link instanceof PDO) ? epc_cp_menu_cache($db_link) : array('groups' => array(), 'items' => array());

	foreach ((array) ($epcCpMenuCache['groups'] ?? array()) as $group) {
		$tabs[(string) $group['id']] = array(
			'caption' => translate_str_by_id($group['caption']),
			'caption_key' => (string) $group['caption'],
			'items' => array(),
		);
	}

	$epcCpNavBackend = function_exists('epc_cp_nav_url_prefix')
		? ltrim(epc_cp_nav_url_prefix(), '/')
		: (string) $DP_Config->backend_dir;
	$epcCpMenuItems = array();
	foreach ((array) ($epcCpMenuCache['items'] ?? array()) as $item) {
		$item['url'] = str_replace(array('<backend>'), $epcCpNavBackend, $item['url']);
		$epcCpMenuItems[] = $item;
	}
	if (function_exists('epc_cp_acl_preload')) {
		epc_cp_acl_preload($epcCpMenuItems);
	}

	$epcCpSuperHost = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
	$epcCpSuperAdmin = $epcCpSuperHost && DP_User::isAdmin();

	foreach ($epcCpMenuItems as $item) {
		$showAnyway = (int) (isset($item['show_anyway']) ? $item['show_anyway'] : 0) === 1;
		$aclOk = $epcCpSuperAdmin ? true : is_anable($item);
		$mayShow = $aclOk || ($showAnyway && !$epcCpSuperHost);
		if (!$mayShow) {
			continue;
		}
		if (!epc_portal_cp_item_visible_enhanced($item)) {
			continue;
		}
		$groupKey = (string) $item['items_group'];
		if (!isset($tabs[$groupKey]) || !is_array($tabs[$groupKey]['items'])) {
			continue;
		}
		$tabs[$groupKey]['items'][] = $item;
	}

	foreach ($tabs as $key => $tab) {
		$tab['items'] = epc_portal_cp_menu_dedupe_items($tab['items']);
		$tabs[$key] = $tab;
	}

	$epcPrimaryKeys = function_exists('epc_portal_cp_primary_group_keys')
		? epc_portal_cp_primary_group_keys()
		: array('744', 'epc_cp_group_customers', 'epc_cp_group_documents', 'epc_cp_group_erp');
	$epcAdvancedKeys = function_exists('epc_portal_cp_advanced_group_keys')
		? epc_portal_cp_advanced_group_keys()
		: array();

	$ordered = array();
	$rendered = array();

	$pushGroup = static function ($key, $tab, $tier) use (&$ordered, &$rendered) {
		if (!empty($rendered[$key]) || count($tab['items']) === 0) {
			return;
		}
		$ck = (string) ($tab['caption_key'] ?? '');
		$caption = (string) ($tab['caption'] ?? '');
		$ordered[] = array(
			'key' => (string) $key,
			'caption' => $caption,
			'caption_key' => $ck,
			'subtitle' => function_exists('epc_portal_cp_group_subtitle') ? epc_portal_cp_group_subtitle($ck) : '',
			'icon' => epc_cp_nav_group_icon($ck !== '' ? $ck : (string) $key),
			'short' => epc_cp_nav_group_short($ck !== '' ? $ck : (string) $key, $caption),
			'tier' => $tier,
			'items' => $tab['items'],
		);
		$rendered[$key] = true;
	};

	foreach ($epcPrimaryKeys as $pkey) {
		foreach ($tabs as $key => $tab) {
			$ck = (string) ($tab['caption_key'] ?? '');
			if ($ck === $pkey || (string) $key === $pkey) {
				$pushGroup($key, $tab, 'primary');
				break;
			}
		}
	}

	foreach ($tabs as $key => $tab) {
		if (!empty($rendered[$key])) {
			continue;
		}
		$ck = (string) ($tab['caption_key'] ?? '');
		if (in_array($ck, $epcAdvancedKeys, true)) {
			continue;
		}
		$pushGroup($key, $tab, 'primary');
	}

	$advancedToShow = array();
	foreach ($tabs as $key => $tab) {
		if (!empty($rendered[$key]) || count($tab['items']) === 0) {
			continue;
		}
		$advancedToShow[$key] = $tab;
	}
	foreach ($epcAdvancedKeys as $akey) {
		foreach ($advancedToShow as $key => $tab) {
			$ck = (string) ($tab['caption_key'] ?? '');
			if ($ck === $akey || (string) $key === $akey) {
				$pushGroup($key, $tab, 'advanced');
				unset($advancedToShow[$key]);
				break;
			}
		}
	}
	foreach ($advancedToShow as $key => $tab) {
		$pushGroup($key, $tab, 'advanced');
	}

	$cached = $ordered;
	return $cached;
}

/**
 * Whether the current request matches a nav item URL.
 */
function epc_cp_nav_url_is_active(string $itemUrl): bool
{
	$path = (string) (parse_url($itemUrl, PHP_URL_PATH) ?? '');
	if ($path === '') {
		return false;
	}
	$req = (string) ($_SERVER['REQUEST_URI'] ?? '');
	$reqPath = (string) (parse_url($req, PHP_URL_PATH) ?? '');
	if ($reqPath === '') {
		return false;
	}
	$path = rtrim($path, '/');
	$reqPath = rtrim($reqPath, '/');
	if ($path === '' || $reqPath === '') {
		return false;
	}
	return $reqPath === $path || strpos($reqPath . '/', $path . '/') === 0;
}
