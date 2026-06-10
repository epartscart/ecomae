<?php
/**
 * CP sidebar visibility — module packs + per-group / per-item blocks (Super CP → client sites).
 */
defined('_ASTEXE_') or die('No access');

function epc_portal_cp_menu_defaults()
{
	return array(
		'hidden_groups' => array(),
		'hidden_items' => array(),
		'modern_auth' => array(
			'password' => true,
			'email_otp' => true,
			'google_oauth' => true,
		),
	);
}

/** Map sidebar group caption keys to module packs (for hints in Industry settings). */
function epc_portal_cp_group_pack_map()
{
	return array(
		'744' => array('commerce', 'auto_parts', 'catalogue'),
		'epc_cp_group_channels' => array('commerce', 'marketing'),
		'epc_cp_group_logistics' => array('logistics', 'auto_parts'),
		'epc_cp_group_erp' => array('erp', 'professional', 'tax_advisory'),
		'epc_cp_group_ai' => array('auto_parts'),
		'epc_cp_group_marketing' => array('marketing'),
		'epc_cp_group_customers' => array('professional', 'commerce'),
		'epc_cp_group_documents' => array('professional', 'erp'),
		'epc_cp_group_portal' => array('core', 'super_platform'),
		'epc_cp_group_tenant_hub' => array('super_platform'),
		'epc_cp_group_operator' => array('super_platform'),
		'epc_cp_group_integrations' => array('core', 'super_platform'),
	);
}

/** Short sidebar subtitles for known CP groups (caption lang keys). */
function epc_portal_cp_group_subtitle_map()
{
	return array(
		'epc_cp_group_operator' => 'epc_cp_group_operator_desc',
		'epc_cp_group_integrations' => 'epc_cp_group_integrations_desc',
	);
}

function epc_portal_cp_group_subtitle($captionKey)
{
	$map = epc_portal_cp_group_subtitle_map();
	if (!isset($map[$captionKey])) {
		return '';
	}
	$langKey = (string) $map[$captionKey];
	if (function_exists('translate_str_by_key')) {
		$text = translate_str_by_key($langKey);
		if ($text !== '' && $text !== $langKey) {
			return $text;
		}
	}
	return '';
}

function epc_portal_cp_menu_policy(?array $settings = null)
{
	if ($settings === null) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
		$settings = epc_portal_load_site_settings();
	}
	$policy = epc_portal_cp_menu_defaults();
	if (!empty($settings['cp_menu']) && is_array($settings['cp_menu'])) {
		$policy = array_merge($policy, $settings['cp_menu']);
	}
	$policy['hidden_groups'] = array_map('intval', (array) ($policy['hidden_groups'] ?? array()));
	$policy['hidden_items'] = array_map('intval', (array) ($policy['hidden_items'] ?? array()));
	return $policy;
}

function epc_portal_cp_group_blocked($groupId, $groupCaption = '')
{
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}
	$policy = epc_portal_cp_menu_policy();
	if (in_array((int) $groupId, $policy['hidden_groups'], true)) {
		return true;
	}
	return false;
}

function epc_portal_cp_item_blocked($itemId, $groupId = 0)
{
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}
	$policy = epc_portal_cp_menu_policy();
	if (in_array((int) $itemId, $policy['hidden_items'], true)) {
		return true;
	}
	if ($groupId > 0 && in_array((int) $groupId, $policy['hidden_groups'], true)) {
		return true;
	}
	return false;
}

function epc_portal_cp_legacy_system_menu_hidden($url, $caption = '')
{
	$helpers = $_SERVER['DOCUMENT_ROOT'] . '/epc_cp_mainstream_menu.php';
	if (!is_file($helpers)) {
		$helpers = dirname(__DIR__, 2) . '/epc_cp_mainstream_menu.php';
	}
	if (is_file($helpers)) {
		require_once $helpers;
		if (function_exists('epc_cp_system_menu_item_hidden')) {
			return epc_cp_system_menu_item_hidden($url, $caption);
		}
	}
	$url = strtolower(preg_replace('#\?.*$#', '', (string) $url));
	foreach (array('/control/o-programme', '/control/obnovleniya', 'changes_fc') as $pat) {
		if (strpos($url, $pat) !== false) {
			return true;
		}
	}
	return false;
}

function epc_portal_cp_item_visible_enhanced($item)
{
	$url = is_array($item) ? (string) ($item['url'] ?? '') : (string) $item;
	$url = strtolower(str_replace(array('<backend>'), isset($GLOBALS['DP_Config']->backend_dir) ? $GLOBALS['DP_Config']->backend_dir : 'cp', $url));
	$caption = is_array($item) ? (string) ($item['caption'] ?? '') : '';
	if (epc_portal_cp_legacy_system_menu_hidden($url, $caption)) {
		return false;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (!epc_portal_cp_item_visible($url)) {
		return false;
	}
	if (is_array($item)) {
		$id = (int) ($item['id'] ?? 0);
		$gid = (int) ($item['items_group'] ?? 0);
		if (epc_portal_cp_item_blocked($id, $gid)) {
			return false;
		}
	}
	$helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_integrations_helpers.php';
	if (is_file($helpers)) {
		require_once $helpers;
		if (function_exists('epc_integrations_menu_blocked_by_feature') && epc_integrations_menu_blocked_by_feature($url)) {
			return false;
		}
	}
	$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
	if (!$isSuper && strpos($url, 'epc_tenant_features') !== false) {
		return false;
	}
	if ($isSuper && strpos($url, 'epc_tenant_email_settings') !== false) {
		return false;
	}
	if (!$isSuper) {
		$superOnly = array(
			'epc_super_cp_',
			'epc_pos_tenant_manage',
		);
		foreach ($superOnly as $needle) {
			if (strpos($url, strtolower($needle)) !== false) {
				return false;
			}
		}
		if (is_array($item)) {
			$operatorGroupId = 0;
			static $operatorGroupCache = null;
			if ($operatorGroupCache === null && isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
				$gst = $GLOBALS['db_link']->prepare('SELECT `id` FROM `control_groups` WHERE `caption` = ? LIMIT 1');
				$gst->execute(array('epc_cp_group_operator'));
				$operatorGroupCache = (int) $gst->fetchColumn();
			}
			$operatorGroupId = (int) $operatorGroupCache;
			if ($operatorGroupId > 0 && (int) ($item['items_group'] ?? 0) === $operatorGroupId) {
				return false;
			}
		}
	}
	return true;
}

function epc_portal_cp_menu_groups_for_settings(PDO $pdo)
{
	$groups = array();
	$st = $pdo->query('SELECT `id`, `caption`, `order` FROM `control_groups` ORDER BY `order` ASC, `id` ASC');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$captionKey = (string) $row['caption'];
		$label = $captionKey;
		if (function_exists('translate_str_by_key') && preg_match('/^[A-Za-z0-9_]+$/', $captionKey)) {
			$label = translate_str_by_key($captionKey);
		} elseif (is_numeric($captionKey) && function_exists('translate_str_by_id')) {
			$label = translate_str_by_id((int) $captionKey);
		}
		$cnt = $pdo->prepare('SELECT COUNT(*) FROM `control_items` WHERE `items_group` = ?');
		$cnt->execute(array((int) $row['id']));
		$groups[] = array(
			'id' => (int) $row['id'],
			'caption' => $captionKey,
			'label' => $label,
			'item_count' => (int) $cnt->fetchColumn(),
			'packs' => isset(epc_portal_cp_group_pack_map()[$captionKey]) ? epc_portal_cp_group_pack_map()[$captionKey] : array(),
		);
	}
	return $groups;
}

/**
 * Drop duplicate CP sidebar rows that share the same path (ignoring query string).
 */
function epc_portal_cp_menu_dedupe_items(array $items)
{
	$seen = array();
	$out = array();
	foreach ($items as $item) {
		$url = is_array($item) ? (string) ($item['url'] ?? '') : (string) $item;
		$key = strtolower(preg_replace('#\?.*$#', '', $url));
		if ($key === '' || isset($seen[$key])) {
			continue;
		}
		$seen[$key] = true;
		$out[] = $item;
	}
	return $out;
}

function epc_portal_cp_menu_items_for_settings(PDO $pdo, $groupId)
{
	$items = array();
	$st = $pdo->prepare('SELECT `id`, `caption`, `url`, `items_group`, `order` FROM `control_items` WHERE `items_group` = ? ORDER BY `order` ASC, `id` ASC');
	$st->execute(array((int) $groupId));
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$cap = (string) $row['caption'];
		$label = $cap;
		if (function_exists('translate_str_by_key') && preg_match('/^[A-Za-z0-9_]+$/', $cap)) {
			$label = translate_str_by_key($cap);
		} elseif (is_numeric($cap) && function_exists('translate_str_by_id')) {
			$label = translate_str_by_id((int) $cap);
		}
		$items[] = array(
			'id' => (int) $row['id'],
			'label' => $label,
			'url' => (string) $row['url'],
		);
	}
	return $items;
}

/**
 * Save client CP policy from Super CP (writes into tenant MySQL).
 */
function epc_portal_push_settings_to_tenant_host(PDO $platformPdo, $hostname, array $data)
{
	$hostname = strtolower(trim((string) $hostname));
	$hostname = preg_replace('/^www\./', '', $hostname);
	if ($hostname === '') {
		return array('ok' => false, 'message' => 'Invalid hostname');
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
	$st = $platformPdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `hostname` = ? OR `hostname` = ? LIMIT 1');
	$st->execute(array($hostname, 'www.' . $hostname));
	$tenant = $st->fetch(PDO::FETCH_ASSOC);
	if (!$tenant) {
		return array('ok' => false, 'message' => 'Tenant not registered for ' . $hostname);
	}
	$dbName = (string) $tenant['db_name'];
	$dbUser = (string) $tenant['db_user'];
	$dbPass = (string) $tenant['db_password'];
	if ($dbName === '') {
		return array('ok' => false, 'message' => 'Tenant database not configured');
	}
	$cfg = epc_portal_docpart_config();
	if ($dbUser === '') {
		$dbUser = $cfg->user;
	}
	if ($dbPass === '') {
		$dbPass = $cfg->password;
	}
	try {
		$clientPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $dbName . ';charset=utf8',
			$dbUser,
			$dbPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		return array('ok' => false, 'message' => 'Client DB connect failed: ' . $e->getMessage());
	}
	$saveHost = (string) $tenant['hostname'];
	if (strpos($saveHost, 'www.') !== 0) {
		$saveHost = 'www.' . $saveHost;
	}
	$data['host'] = $saveHost;
	epc_portal_save_site_settings($clientPdo, $data);
	return array('ok' => true, 'message' => 'Client CP settings saved for ' . $saveHost, 'db' => $dbName);
}

/**
 * When a tenant goes live on Super CP, copy platform registry packs + access_mode into the client docpart DB.
 */
function epc_portal_sync_tenant_packs_to_client_db(PDO $platformPdo, string $hostnameOrSiteKey): array
{
	require_once __DIR__ . '/epc_portal_tenant.php';
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($hostnameOrSiteKey));
	$hostname = strtolower(trim($hostnameOrSiteKey));
	if ($key !== '' && strpos($hostname, '.') === false) {
		$row = epc_portal_tenant_get($platformPdo, $key);
		if ($row === null) {
			return array('ok' => false, 'message' => 'Tenant not found: ' . $key);
		}
		$hostname = (string) $row['hostname'];
	} else {
		$bare = preg_replace('/^www\./', '', $hostname);
		$st = $platformPdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `hostname` = ? OR `hostname` = ? LIMIT 1');
		$st->execute(array($hostname, $bare));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			return array('ok' => false, 'message' => 'Tenant not registered for ' . $hostname);
		}
	}
	if (trim((string) ($row['db_name'] ?? '')) === '') {
		return array('ok' => false, 'message' => 'Tenant database not configured — cannot sync packs');
	}
	$saveHost = (string) $row['hostname'];
	if (strpos($saveHost, 'www.') !== 0) {
		$saveHost = 'www.' . $saveHost;
	}
	$settings = epc_portal_load_site_settings_for_host($platformPdo, $saveHost);
	$packs = isset($settings['enabled_packs']) && is_array($settings['enabled_packs'])
		? $settings['enabled_packs'] : array('core');
	$packs = array_values(array_unique(array_filter($packs, function ($p) {
		return $p !== 'super_platform';
	})));
	require_once __DIR__ . '/epc_portal_erp_modules.php';
	$erpModules = isset($settings['erp_modules']) && is_array($settings['erp_modules'])
		? epc_portal_erp_modules_normalize_list($settings['erp_modules'])
		: epc_portal_erp_modules_enabled($settings);
	$data = array(
		'host' => $saveHost,
		'industry_code' => $settings['industry_code'] ?? (string) ($row['industry_code'] ?? 'auto_parts'),
		'enabled_packs' => $packs,
		'access_mode' => $settings['access_mode'] ?? 'full',
		'erp_modules' => $erpModules,
		'system_name' => $settings['system_name'] ?? '',
		'hub_name' => $settings['hub_name'] ?? (string) ($row['trade_name'] ?? ''),
		'tagline' => $settings['tagline'] ?? '',
		'domain_path' => $settings['domain_path'] ?? ('https://' . $saveHost . '/'),
		'contact' => $settings['contact'] ?? array(),
		'theme_template' => $settings['theme_template'] ?? 'classic',
		'theme' => $settings['theme'] ?? array(),
		'cp_menu' => $settings['cp_menu'] ?? epc_portal_cp_menu_defaults(),
		'cp_default_lang' => $settings['cp_default_lang'] ?? 'en',
	);
	return epc_portal_push_settings_to_tenant_host($platformPdo, $saveHost, $data);
}
