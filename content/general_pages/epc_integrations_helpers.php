<?php
/**
 * Platform integrations catalog, tenant feature flags, and config helpers.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_portal.php';

function epc_int_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_int_backend(): string
{
	global $DP_Config;
	return trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
}

/** @return array<string, array{label:string,icon:string,blurb:string}> */
function epc_integrations_categories(): array
{
	return array(
		'identity' => array(
			'label' => 'Identity & messaging',
			'icon' => 'fa-id-badge',
			'blurb' => 'Login, email delivery, and customer messaging channels.',
		),
		'commerce' => array(
			'label' => 'Commerce & payments',
			'icon' => 'fa-shopping-bag',
			'blurb' => 'Checkout, POS, tax, and settlement rails.',
		),
		'growth' => array(
			'label' => 'Marketing & growth',
			'icon' => 'fa-bullhorn',
			'blurb' => 'Broadcast, social, tracking, and storefront content.',
		),
		'catalog' => array(
			'label' => 'Catalog & AI',
			'icon' => 'fa-cubes',
			'blurb' => 'Pricing intelligence and parts expert assistants.',
		),
		'data' => array(
			'label' => 'Data & APIs',
			'icon' => 'fa-database',
			'blurb' => 'REST keys, Power BI datasets, and analytics embeds.',
		),
		'platform' => array(
			'label' => 'Platform',
			'icon' => 'fa-server',
			'blurb' => 'Mobile shells and multi-tenant control.',
		),
	);
}

/**
 * Resolve a catalog guide field to a CP-clickable URL (never a bare docs path).
 */
function epc_integrations_resolve_guide(string $guide, string $key = ''): string
{
	$guide = trim($guide);
	$be = epc_int_backend();
	$master = '/' . $be . '/control/portal/epc_integrations_guide';
	if ($guide === '') {
		return $key !== '' ? $master . '#' . rawurlencode($key) : $master;
	}
	if (strpos($guide, 'http://') === 0 || strpos($guide, 'https://') === 0 || strpos($guide, '/') === 0) {
		return $guide;
	}
	if (strpos($guide, 'docs/') === 0 || strpos($guide, '#') === 0) {
		$anchor = $key !== '' ? $key : ltrim($guide, '#');
		return $master . '#' . rawurlencode($anchor);
	}
	return $master . ($key !== '' ? '#' . rawurlencode($key) : '');
}

/** @return array<string, array<string, mixed>> */
function epc_integrations_catalog(): array
{
	$be = epc_int_backend();
	$guide = '/' . $be . '/control/portal/epc_integrations_guide';
	return array(
		'email_smtp' => array(
			'label' => 'Email / SMTP',
			'icon' => 'fa-envelope',
			'color' => '#059669',
			'category' => 'identity',
			'blurb' => 'Transactional mail (orders, OTP, alerts) via tenant or platform SMTP.',
			'super_url' => '/' . $be . '/control/portal/epc_cp_auth_settings',
			'tenant_url' => '/' . $be . '/control/portal/epc_tenant_email_settings',
			'guide' => $guide . '#email_smtp',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array(),
		),
		'oauth' => array(
			'label' => 'OAuth (Google, Microsoft…)',
			'icon' => 'fa-sign-in',
			'color' => '#2563eb',
			'category' => 'identity',
			'blurb' => 'Social / Microsoft login for CP and storefront — configured on Super CP.',
			'super_url' => '/' . $be . '/control/portal/epc_cp_auth_settings',
			'tenant_url' => '/' . $be . '/control/portal/epc_integrations_hub',
			'guide' => $guide . '#oauth',
			'super_only_config' => true,
			'default_enabled' => true,
			'menu_patterns' => array(),
		),
		'registration_enhanced' => array(
			'label' => 'Registration enhanced',
			'icon' => 'fa-user-plus',
			'color' => '#0891b2',
			'category' => 'identity',
			'blurb' => 'Stronger signup flows, verification, and auth policies for tenants.',
			'super_url' => '/' . $be . '/control/portal/epc_cp_auth_settings',
			'tenant_url' => '/' . $be . '/control/portal/epc_integrations_hub',
			'guide' => $guide . '#registration_enhanced',
			'super_only_config' => true,
			'default_enabled' => true,
			'menu_patterns' => array(),
		),
		'whatsapp' => array(
			'label' => 'WhatsApp sharing',
			'icon' => 'fa-whatsapp',
			'color' => '#16a34a',
			'category' => 'identity',
			'blurb' => 'wa.me order sharing with bilingual EN/AR templates for sales desks.',
			'super_url' => '/' . $be . '/shop/orders/whatsapp-guide',
			'tenant_url' => '/' . $be . '/shop/orders/whatsapp-guide',
			'guide' => '/' . $be . '/shop/orders/whatsapp-guide',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('whatsapp'),
		),
		'payment_gateways' => array(
			'label' => 'Payment gateways',
			'icon' => 'fa-credit-card',
			'color' => '#0369a1',
			'category' => 'commerce',
			'blurb' => 'Telr, GCC BNPL, JazzCash/Easypaisa, crypto, and per-account settlements.',
			'super_url' => '/' . $be . '/shop/payments/payments',
			'tenant_url' => '/' . $be . '/shop/payments/payments',
			'guide' => $guide . '#payment_gateways',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('/shop/payments/'),
		),
		'pos' => array(
			'label' => 'POS Terminal',
			'icon' => 'fa-cash-register',
			'color' => '#1d4ed8',
			'category' => 'commerce',
			'blurb' => 'Counter sales, cash/card tender, and ERP-linked receipts.',
			'super_url' => '/' . $be . '/control/portal/epc_pos_tenant_manage',
			'tenant_url' => '/' . $be . '/shop/pos/terminal',
			'guide' => $guide . '#pos',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('/shop/pos/'),
		),
		'tax_toolkit' => array(
			'label' => 'Tax Toolkit',
			'icon' => 'fa-globe',
			'color' => '#0f766e',
			'category' => 'commerce',
			'blurb' => 'Market VAT / tax profiles that follow the tenant country registration.',
			'super_url' => '/' . $be . '/control/portal/epc_tax_toolkit_manage',
			'tenant_url' => '/' . $be . '/shop/finance/erp',
			'guide' => $guide . '#tax_toolkit',
			'super_only_config' => true,
			'default_enabled' => true,
			'menu_patterns' => array('epc_tax_toolkit', 'uae-tax-compliance'),
		),
		'custom_shipping' => array(
			'label' => 'Custom & shipping',
			'icon' => 'fa-ship',
			'color' => '#0e7490',
			'category' => 'commerce',
			'blurb' => 'Customs declarations, LGP intake, and shipping reports inside ERP.',
			'super_url' => '/' . $be . '/control/portal/epc_custom_shipping_guide',
			'tenant_url' => '/' . $be . '/shop/finance/erp?area=custom_shipping&tab=custom_shipping&epc_erp_shell=1',
			'guide' => '/' . $be . '/control/portal/epc_custom_shipping_guide',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('custom_shipping', 'custom-shipping'),
		),
		'social_media_hub' => array(
			'label' => 'Social media hub',
			'icon' => 'fa-share-alt',
			'color' => '#db2777',
			'category' => 'growth',
			'blurb' => 'Publish calendars, account links, and AI-assisted social posts.',
			'super_url' => '/' . $be . '/control/portal/epc_social_media_hub',
			'tenant_url' => '/' . $be . '/control/portal/epc_social_media_hub',
			'guide' => '/' . $be . '/control/portal/epc_social_media_hub?tab=guide',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_social_media_hub'),
		),
		'marketing_broadcast' => array(
			'label' => 'Marketing broadcast',
			'icon' => 'fa-paper-plane',
			'color' => '#ea580c',
			'category' => 'growth',
			'blurb' => 'Bulk email and WhatsApp campaigns with audience segments.',
			'super_url' => '/' . $be . '/control/portal/epc_marketing_broadcast',
			'tenant_url' => '/' . $be . '/control/portal/epc_marketing_broadcast',
			'guide' => '/' . $be . '/control/portal/epc_marketing_broadcast?tab=guide',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_marketing_broadcast', '/shop/marketing/'),
		),
		'web_tracker' => array(
			'label' => 'Web tracker',
			'icon' => 'fa-line-chart',
			'color' => '#0284c7',
			'category' => 'growth',
			'blurb' => 'GA4 / Meta / TikTok pixels and storefront event wiring.',
			'super_url' => '/' . $be . '/control/portal/epc_web_tracker',
			'tenant_url' => '/' . $be . '/control/portal/epc_web_tracker',
			'guide' => $guide . '#web_tracker',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_web_tracker'),
		),
		'visual_page_editor' => array(
			'label' => 'Visual page editor',
			'icon' => 'fa-paint-brush',
			'color' => '#be185d',
			'category' => 'growth',
			'blurb' => 'Drag-and-drop landing and content blocks for the storefront.',
			'super_url' => '/' . $be . '/control/portal/epc_visual_page_editor',
			'tenant_url' => '/' . $be . '/control/portal/epc_visual_page_editor',
			'guide' => $guide . '#visual_page_editor',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_visual_page_editor'),
		),
		'auto_price_ai' => array(
			'label' => 'Auto Price AI',
			'icon' => 'fa-magic',
			'color' => '#0f766e',
			'category' => 'catalog',
			'blurb' => 'Discover, compare, and import competitive parts pricing by market.',
			'super_url' => '/' . $be . '/control/portal/epc_auto_price_engine',
			'tenant_url' => '/' . $be . '/control/portal/epc_auto_price_engine',
			'guide' => '/' . $be . '/control/portal/epc_auto_price_guide',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_auto_price', '/shop/parts_agent'),
		),
		'parts_agent' => array(
			'label' => 'AI parts agent',
			'icon' => 'fa-robot',
			'color' => '#0e7490',
			'category' => 'catalog',
			'blurb' => 'Conversational parts expert for staff and storefront shoppers.',
			'super_url' => '/' . $be . '/shop/parts_agent_chats',
			'tenant_url' => '/' . $be . '/shop/parts_agent_chats',
			'guide' => $guide . '#parts_agent',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('parts_agent'),
		),
		'api_integrations' => array(
			'label' => 'API clients & keys',
			'icon' => 'fa-code',
			'color' => '#475569',
			'category' => 'data',
			'blurb' => 'Catalog & Price PRO clients plus tenant-scoped REST API keys.',
			'super_url' => '/' . $be . '/control/portal/epc_api_clients_manage',
			'tenant_url' => '/' . $be . '/control/portal/epc_api_clients_manage',
			'guide' => '/' . $be . '/control/portal/epc_api_documentation_guide',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_api_clients'),
		),
		'power_bi' => array(
			'label' => 'Power BI',
			'icon' => 'fa-bar-chart',
			'color' => '#ca8a04',
			'category' => 'data',
			'blurb' => 'JSON/CSV datasets for Desktop refresh and optional report embed.',
			'super_url' => '/' . $be . '/control/portal/epc_power_bi',
			'tenant_url' => '/' . $be . '/control/portal/epc_power_bi',
			'guide' => '/' . $be . '/control/portal/epc_power_bi_guide',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_power_bi', 'powerbi', 'epc_power_bi_guide'),
		),
		'mobile_apps' => array(
			'label' => 'Mobile apps (Android / iOS)',
			'icon' => 'fa-mobile-alt',
			'color' => '#dc2626',
			'category' => 'platform',
			'blurb' => 'PWA install plus Capacitor targets for CP, ERP, and storefront.',
			'super_url' => '/' . $be . '/control/portal/epc_mobile_apps',
			'tenant_url' => '/' . $be . '/control/portal/epc_mobile_apps',
			'guide' => $guide . '#mobile_apps',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array(),
		),
		'tenant_registry' => array(
			'label' => 'Multi-tenant registry',
			'icon' => 'fa-sitemap',
			'color' => '#0369a1',
			'category' => 'platform',
			'blurb' => 'Live tenant hosts, DB credentials, and Super CP feature toggles.',
			'super_url' => '/' . $be . '/shop/tenant_hub/tenant_hub',
			'tenant_url' => '',
			'guide' => $guide . '#tenant_registry',
			'super_only_config' => true,
			'default_enabled' => true,
			'menu_patterns' => array('tenant_hub'),
		),
	);
}

function epc_integrations_ensure_schema(PDO $pdo): void
{
	// Once per request/connection — feature_enabled used to call this for every
	// catalog feature × every CP sidebar item (~1400×), each re-running portal DB ensure.
	static $done = array();
	$oid = spl_object_id($pdo);
	if (isset($done[$oid])) {
		return;
	}
	$done[$oid] = true;

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_tenant_feature_flags` (
			`site_key` VARCHAR(64) NOT NULL,
			`feature_key` VARCHAR(64) NOT NULL,
			`enabled` TINYINT(1) NOT NULL DEFAULT 1,
			`config_json` TEXT NULL,
			`updated_at` INT NOT NULL DEFAULT 0,
			PRIMARY KEY (`site_key`, `feature_key`),
			KEY `feature_key` (`feature_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	require_once __DIR__ . '/epc_portal_db.php';
	epc_portal_db_ensure($pdo);
	try {
		$pdo->query('SELECT `integrations_json` FROM `epc_portal_site_settings` LIMIT 1');
	} catch (Exception $e) {
		$pdo->exec("ALTER TABLE `epc_portal_site_settings` ADD COLUMN `integrations_json` TEXT NULL AFTER `cp_menu_json`");
	}
}

function epc_integrations_site_key(?PDO $pdo = null): string
{
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return 'platform';
	}
	$host = function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? '');
	$host = preg_replace('/^www\./', '', strtolower(trim($host)));
	if ($pdo instanceof PDO && function_exists('epc_portal_list_tenants')) {
		foreach (epc_portal_list_tenants($pdo) as $row) {
			$h = preg_replace('/^www\./', '', strtolower((string) ($row['hostname'] ?? '')));
			if ($h === $host || $host === 'www.' . $h) {
				return (string) ($row['site_key'] ?? $h);
			}
		}
	}
	return preg_replace('/[^a-z0-9_-]/', '', str_replace('.', '-', $host));
}

/**
 * Load all feature flags for a site (one schema ensure + one SELECT), request-cached.
 *
 * @return array<string, bool>
 */
function epc_integrations_features_for_site(string $siteKey, ?PDO $platformPdo = null): array
{
	static $reqCache = array();
	$siteKey = preg_replace('/[^a-z0-9_\-]/', '', strtolower($siteKey));
	if ($siteKey !== '' && isset($reqCache[$siteKey])) {
		return $reqCache[$siteKey];
	}

	$catalog = epc_integrations_catalog();
	$defaults = array();
	foreach ($catalog as $key => $meta) {
		$defaults[$key] = !empty($meta['default_enabled']);
	}
	if ($siteKey === '' || $siteKey === 'platform') {
		$reqCache[$siteKey] = $defaults;
		return $defaults;
	}

	require_once __DIR__ . '/epc_perf_cache.php';
	$cacheKey = 'epc_int_features:v2:' . $siteKey;
	$loaded = epc_perf_cache_remember($cacheKey, 300, static function () use ($siteKey, $platformPdo, $defaults) {
		if (!$platformPdo instanceof PDO) {
			$platformPdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
		}
		if (!$platformPdo instanceof PDO) {
			return $defaults;
		}
		epc_integrations_ensure_schema($platformPdo);
		$out = $defaults;
		try {
			$st = $platformPdo->prepare('SELECT `feature_key`, `enabled` FROM `epc_tenant_feature_flags` WHERE `site_key` = ?');
			$st->execute(array($siteKey));
			while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
				$fk = (string) ($row['feature_key'] ?? '');
				if ($fk === '' || !array_key_exists($fk, $out)) {
					continue;
				}
				$out[$fk] = ((int) ($row['enabled'] ?? 0) === 1);
			}
		} catch (Throwable $e) {
			return $defaults;
		}
		return $out;
	});

	if (!is_array($loaded)) {
		$loaded = $defaults;
	}
	$reqCache[$siteKey] = $loaded;
	return $loaded;
}

function epc_integrations_feature_enabled(string $featureKey, ?string $siteKey = null, ?PDO $platformPdo = null): bool
{
	$catalog = epc_integrations_catalog();
	if (!isset($catalog[$featureKey])) {
		return true;
	}
	$default = !empty($catalog[$featureKey]['default_enabled']);
	if ($siteKey === null || $siteKey === '') {
		$siteKey = epc_integrations_site_key($platformPdo);
	}
	if ($siteKey === 'platform' || $siteKey === '') {
		return $default;
	}
	$flags = epc_integrations_features_for_site($siteKey, $platformPdo);
	return array_key_exists($featureKey, $flags) ? (bool) $flags[$featureKey] : $default;
}

function epc_integrations_save_feature_flags(PDO $platformPdo, string $siteKey, array $flags): array
{
	epc_integrations_ensure_schema($platformPdo);
	$now = time();
	$st = $platformPdo->prepare(
		'INSERT INTO `epc_tenant_feature_flags` (`site_key`, `feature_key`, `enabled`, `updated_at`)
		 VALUES (?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `enabled` = VALUES(`enabled`), `updated_at` = VALUES(`updated_at`)'
	);
	$saved = 0;
	foreach ($flags as $featureKey => $enabled) {
		$featureKey = preg_replace('/[^a-z0-9_]/', '', (string) $featureKey);
		if ($featureKey === '' || !isset(epc_integrations_catalog()[$featureKey])) {
			continue;
		}
		$st->execute(array($siteKey, $featureKey, !empty($enabled) ? 1 : 0, $now));
		$saved++;
	}
	return array('ok' => true, 'saved' => $saved);
}

function epc_integrations_load_tenant_config(?PDO $pdo = null): array
{
	require_once __DIR__ . '/epc_portal_db.php';
	if (!$pdo instanceof PDO) {
		$pdo = isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO ? $GLOBALS['db_link'] : null;
	}
	if (!$pdo instanceof PDO) {
		return array();
	}
	epc_integrations_ensure_schema($pdo);
	$settings = epc_portal_load_site_settings($pdo);
	$raw = $settings['integrations'] ?? null;
	if (is_string($raw)) {
		$decoded = json_decode($raw, true);
		return is_array($decoded) ? $decoded : array();
	}
	if (is_array($raw)) {
		return $raw;
	}
	return array();
}

function epc_integrations_save_tenant_config(PDO $pdo, array $integrations): array
{
	require_once __DIR__ . '/epc_portal_db.php';
	epc_integrations_ensure_schema($pdo);
	$settings = epc_portal_load_site_settings($pdo);
	$settings['integrations'] = $integrations;
	epc_portal_save_site_settings($pdo, $settings);
	return array('ok' => true);
}

function epc_integrations_default_mobile_config(): array
{
	return array(
		'enabled' => false,
		'app_name' => '',
		'bundle_id' => '',
		'deep_link_scheme' => '',
		'deep_link_domain' => '',
		'api_base_url' => '',
		'play_store_url' => '',
		'app_store_url' => '',
		'pwa_enabled' => true,
		'firebase_project_id' => '',
		'push_enabled' => false,
	);
}

function epc_integrations_mobile_config(?PDO $pdo = null): array
{
	$cfg = epc_integrations_load_tenant_config($pdo);
	$mobile = isset($cfg['mobile']) && is_array($cfg['mobile']) ? $cfg['mobile'] : array();
	return array_merge(epc_integrations_default_mobile_config(), $mobile);
}

function epc_integrations_platform_mobile_defaults(): array
{
	return array(
		'api_base_url' => 'https://www.ecomae.com',
		'firebase_template' => '',
		'capacitor_version' => '6',
		'allow_push' => true,
		'default_deep_link_scheme' => 'epartscart://',
	);
}

function epc_integrations_menu_blocked_by_feature(string $itemUrl): bool
{
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}
	$url = strtolower(preg_replace('#\?.*$#', '', $itemUrl));
	$siteKey = epc_integrations_site_key();
	if ($siteKey === 'platform' || $siteKey === '') {
		return false;
	}
	// One flag map for the whole sidebar pass (not N features × N items).
	$flags = epc_integrations_features_for_site($siteKey);
	foreach (epc_integrations_catalog() as $featureKey => $meta) {
		if (!empty($flags[$featureKey])) {
			continue;
		}
		foreach ((array) ($meta['menu_patterns'] ?? array()) as $pattern) {
			$pattern = strtolower((string) $pattern);
			if ($pattern !== '' && strpos($url, $pattern) !== false) {
				return true;
			}
		}
	}
	return false;
}

function epc_integrations_hub_rows(?PDO $pdo = null, bool $isSuper = false): array
{
	$siteKey = $isSuper ? 'platform' : epc_integrations_site_key($pdo);
	$features = epc_integrations_features_for_site($siteKey, $isSuper ? $pdo : null);
	$rows = array();
	foreach (epc_integrations_catalog() as $key => $meta) {
		if ($isSuper && empty($meta['super_url'])) {
			continue;
		}
		if (!$isSuper && empty($meta['tenant_url']) && !empty($meta['super_only_config'])) {
			continue;
		}
		$enabled = $isSuper ? true : !empty($features[$key]);
		$configUrl = $isSuper ? (string) ($meta['super_url'] ?? '') : (string) ($meta['tenant_url'] ?? $meta['super_url'] ?? '');
		if (!$isSuper && !empty($meta['super_only_config'])) {
			$configUrl = '/' . epc_int_backend() . '/control/portal/epc_integrations_hub';
		}
		$guideRaw = (string) ($meta['guide'] ?? '');
		// Tenant CP cannot open Super-only API docs guide — fall back to master guide.
		if (!$isSuper && strpos($guideRaw, 'epc_api_documentation_guide') !== false) {
			$guideRaw = '/' . epc_int_backend() . '/control/portal/epc_integrations_guide#api_integrations';
		}
		$rows[] = array(
			'key' => $key,
			'label' => (string) $meta['label'],
			'icon' => (string) ($meta['icon'] ?? 'fa-plug'),
			'color' => (string) ($meta['color'] ?? '#64748b'),
			'category' => (string) ($meta['category'] ?? 'platform'),
			'blurb' => (string) ($meta['blurb'] ?? ''),
			'enabled' => $enabled,
			'active' => $enabled,
			'configure_url' => $configUrl,
			'guide' => epc_integrations_resolve_guide($guideRaw, $key),
			'super_only' => !empty($meta['super_only_config']),
		);
	}
	return $rows;
}

function epc_integrations_register_cp_content(PDO $pdo, string $urlSlug, string $langKey, string $titleEn, string $titleRu, string $phpRel, int $menuOrder = 8): int
{
	require_once dirname(__DIR__, 2) . '/epc_cp_mainstream_menu.php';
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($langKey, $titleEn));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($langKey, 'en', $titleEn));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($langKey, 'ru', $titleRu));

	$contentUrl = 'control/portal/' . $urlSlug;
	$phpPath = '/<backend_dir>/content/control/portal/' . $urlSlug . '.php';
	$now = time();

	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('control/config'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		$parent->execute(array('control'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		return 0;
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($phpPath, $langKey, $langKey, $parentId, $level, $urlSlug, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$contentUrl, $level, $urlSlug, $langKey, $parentId,
			$titleEn,
			$phpPath, $langKey, $now, $now, $menuOrder,
		));
		$contentId = (int) $pdo->lastInsertId();
	}

	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('control/portal/industry_settings'));
	$refId = (int) $ref->fetchColumn();
	if ($refId > 0 && $contentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$groups->execute(array($refId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$ins->execute(array($contentId, (int) $g['group_id']));
			} catch (Exception $e) {
			}
		}
	}
	return $contentId;
}
