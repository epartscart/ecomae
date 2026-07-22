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

/** Short sidebar subtitles for known CP groups (caption lang keys or plain EN). */
function epc_portal_cp_group_subtitle_map()
{
	return array(
		'744' => 'Orders, catalogue & prices',
		'epc_cp_group_customers' => 'Clients & CRM',
		'epc_cp_group_documents' => 'Invoices & PDFs',
		'epc_cp_group_erp' => 'Finance, VAT & reports',
		'epc_cp_group_procurement' => 'Purchasing & suppliers',
		'epc_cp_group_channels' => 'Marketplaces & feeds',
		'epc_cp_group_logistics' => 'Shipping & delivery',
		'epc_cp_group_payments' => 'Cards & online pay',
		'epc_cp_group_marketing' => 'Campaigns & social',
		'epc_cp_group_ai' => 'Pricing & assistants',
		'epc_cp_group_integrations' => 'epc_cp_group_integrations_desc',
		'epc_cp_group_portal' => 'Site & industry settings',
		'epc_cp_group_tenant_hub' => 'Platform tools',
		'epc_cp_group_operator' => 'epc_cp_group_operator_desc',
	);
}

/**
 * Primary (daily) sidebar groups for tenant CP — shown first.
 * @return list<string>
 */
function epc_portal_cp_primary_group_keys()
{
	return array(
		'744',
		'epc_cp_group_customers',
		'epc_cp_group_documents',
		'epc_cp_group_erp',
		'epc_cp_group_procurement',
		'epc_cp_group_channels',
		'epc_cp_group_logistics',
	);
}

/**
 * Advanced / less-used groups — rendered after a "More modules" divider.
 * @return list<string>
 */
function epc_portal_cp_advanced_group_keys()
{
	return array(
		'epc_cp_group_ai',
		'epc_cp_group_marketing',
		'epc_cp_group_payments',
		'epc_cp_group_integrations',
		'epc_cp_group_portal',
		'epc_cp_group_tenant_hub',
		'epc_cp_group_operator',
	);
}

function epc_portal_cp_group_subtitle($captionKey)
{
	$map = epc_portal_cp_group_subtitle_map();
	if (!isset($map[$captionKey])) {
		return '';
	}
	$value = (string) $map[$captionKey];
	// Lang-key style values still go through translation.
	if (strpos($value, 'epc_') === 0 && function_exists('translate_str_by_key')) {
		$text = translate_str_by_key($value);
		if ($text !== '' && $text !== $value) {
			return $text;
		}
		// Fallback EN when translation missing.
		if ($value === 'epc_cp_group_operator_desc') {
			return 'Cross-tenant platform tools';
		}
		if ($value === 'epc_cp_group_integrations_desc') {
			return 'Email, mobile, payments & more';
		}
		return '';
	}
	return $value;
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

/**
 * Resolve a control_groups / control_items caption to a human label.
 * Numeric captions are lang_text_strings.id (not str_key) — never show bare digits.
 */
function epc_portal_cp_menu_resolve_label(PDO $pdo, $caption, $url = '')
{
	$caption = trim((string) $caption);
	$url = trim((string) $url);
	$label = '';

	if ($caption !== '') {
		// Named lang keys (epc_*, EPC_*, etc.)
		if (preg_match('/^[A-Za-z][A-Za-z0-9_]*$/', $caption)) {
			try {
				$st = $pdo->prepare('SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ? LIMIT 1');
				$st->execute(array($caption, 'en'));
				$label = trim((string) $st->fetchColumn());
			} catch (Throwable $e) {
				$label = '';
			}
			if ($label === '' && function_exists('translate_str_by_key')) {
				$tr = trim((string) translate_str_by_key($caption, 'en'));
				if ($tr !== '' && $tr !== $caption) {
					$label = $tr;
				}
			}
		}
		// Legacy numeric captions = lang_text_strings.id
		if ($label === '' && ctype_digit($caption)) {
			try {
				$st = $pdo->prepare(
					'SELECT t.`value` FROM `lang_text_strings_translation` t
					 INNER JOIN `lang_text_strings` s ON s.`str_key` = t.`str_key`
					 WHERE s.`id` = ? AND t.`lang_code` = ? LIMIT 1'
				);
				$st->execute(array((int) $caption, 'en'));
				$label = trim((string) $st->fetchColumn());
			} catch (Throwable $e) {
				$label = '';
			}
		}
		// Already a readable plain-language caption
		if ($label === '' && preg_match('/[A-Za-z\x{0400}-\x{04FF}]/u', $caption) && !preg_match('/^\d+$/', $caption)
			&& !preg_match('/^[0-9]+_[0-9]+_/', $caption) && strpos($caption, 'epc_') !== 0) {
			$label = $caption;
		}
	}

	if ($label === '' || preg_match('/^\d+$/', $label) || preg_match('/^[0-9]+_[0-9]+_/', $label)
		|| (strpos($label, 'epc_') === 0 && strpos($label, ' ') === false)) {
		$fromUrl = epc_portal_cp_menu_humanize_url($url);
		if ($fromUrl !== '') {
			$label = $fromUrl;
		} elseif ($caption !== '' && !preg_match('/^\d+$/', $caption) && !preg_match('/^[0-9]+_[0-9]+_/', $caption)) {
			$label = epc_portal_cp_menu_humanize_key($caption);
		} else {
			$label = $fromUrl !== '' ? $fromUrl : 'Menu item';
		}
	}

	return $label;
}

/** Turn a CP URL into a short title (shop/orders/orders → Orders). */
function epc_portal_cp_menu_humanize_url($url)
{
	$url = str_replace('\\', '/', (string) $url);
	$url = preg_replace('#^/<backend>/#', '', $url);
	$url = preg_replace('#^/?(cp|backend)/#', '', $url);
	$url = preg_replace('#\?.*$#', '', $url);
	$url = trim($url, '/');
	if ($url === '') {
		return '';
	}
	$part = basename($url);
	$part = preg_replace('/[^a-z0-9_-]+/i', ' ', $part);
	$part = trim((string) preg_replace('/\s+/', ' ', $part));
	if ($part === '') {
		return '';
	}
	// Drop noisy prefixes
	$part = preg_replace('/^(epc|oms|crm)_?/i', '', $part);
	return ucwords(str_replace(array('-', '_'), ' ', strtolower($part)));
}

/** Humanize epc_foo_bar_cp style keys. */
function epc_portal_cp_menu_humanize_key($key)
{
	$key = trim((string) $key);
	$key = preg_replace('/^(epc_|EPC_)/', '', $key);
	$key = preg_replace('/_(cp|group)$/i', '', $key);
	$key = str_replace(array('-', '_'), ' ', $key);
	$key = trim((string) preg_replace('/\s+/', ' ', $key));
	return $key !== '' ? ucwords(strtolower($key)) : 'Menu item';
}

function epc_portal_cp_menu_groups_for_settings(PDO $pdo)
{
	$groups = array();
	$st = $pdo->query('SELECT `id`, `caption`, `order` FROM `control_groups` ORDER BY `order` ASC, `id` ASC');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$captionKey = (string) $row['caption'];
		$label = epc_portal_cp_menu_resolve_label($pdo, $captionKey, '');
		$subtitle = function_exists('epc_portal_cp_group_subtitle') ? epc_portal_cp_group_subtitle($captionKey) : '';
		$cnt = $pdo->prepare('SELECT COUNT(*) FROM `control_items` WHERE `items_group` = ?');
		$cnt->execute(array((int) $row['id']));
		$groups[] = array(
			'id' => (int) $row['id'],
			'caption' => $captionKey,
			'label' => $label,
			'subtitle' => $subtitle,
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
		$url = (string) $row['url'];
		$items[] = array(
			'id' => (int) $row['id'],
			'label' => epc_portal_cp_menu_resolve_label($pdo, $cap, $url),
			'url' => $url,
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
