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

/** @return array<string, array<string, mixed>> */
function epc_integrations_catalog(): array
{
	$be = epc_int_backend();
	return array(
		'email_smtp' => array(
			'label' => 'Email / SMTP',
			'icon' => 'fa-envelope',
			'color' => '#059669',
			'super_url' => '/' . $be . '/control/portal/epc_cp_auth_settings',
			'tenant_url' => '/' . $be . '/control/portal/epc_tenant_email_settings',
			'guide' => 'docs/guides/REGISTRATION_AND_AUTH.md#smtp',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array(),
		),
		'oauth' => array(
			'label' => 'OAuth (Google, Microsoft…)',
			'icon' => 'fa-sign-in',
			'color' => '#2563eb',
			'super_url' => '/' . $be . '/control/portal/epc_cp_auth_settings',
			'tenant_url' => '/' . $be . '/control/portal/epc_integrations_hub',
			'guide' => 'docs/guides/REGISTRATION_AND_AUTH.md#oauth',
			'super_only_config' => true,
			'default_enabled' => true,
			'menu_patterns' => array(),
		),
		'mobile_apps' => array(
			'label' => 'Mobile apps (Android / iOS)',
			'icon' => 'fa-mobile-alt',
			'color' => '#dc2626',
			'super_url' => '/' . $be . '/control/portal/epc_mobile_apps',
			'tenant_url' => '/' . $be . '/control/portal/epc_mobile_apps',
			'guide' => 'docs/guides/MOBILE_APPS.md',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array(),
		),
		'auto_price_ai' => array(
			'label' => 'Auto Price AI',
			'icon' => 'fa-magic',
			'color' => '#7c3aed',
			'super_url' => '/' . $be . '/control/portal/epc_auto_price_engine',
			'tenant_url' => '/' . $be . '/control/portal/epc_auto_price_engine',
			'guide' => 'https://www.ecomae.com/platform/auto-price-ai',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_auto_price', '/shop/parts_agent'),
		),
		'pos' => array(
			'label' => 'POS Terminal',
			'icon' => 'fa-cash-register',
			'color' => '#2563eb',
			'super_url' => '/' . $be . '/control/portal/epc_pos_tenant_manage',
			'tenant_url' => '/' . $be . '/shop/pos/terminal',
			'guide' => 'docs/guides/POS_TERMINAL.md',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('/shop/pos/'),
		),
		'tax_toolkit' => array(
			'label' => 'Tax Toolkit',
			'icon' => 'fa-globe',
			'color' => '#0f766e',
			'super_url' => '/' . $be . '/control/portal/epc_tax_toolkit_manage',
			'tenant_url' => '/' . $be . '/shop/finance/erp',
			'guide' => 'docs/guides/TAX_TOOLKIT.md',
			'super_only_config' => true,
			'default_enabled' => true,
			'menu_patterns' => array('epc_tax_toolkit', 'uae-tax-compliance'),
		),
		'visual_page_editor' => array(
			'label' => 'Visual page editor',
			'icon' => 'fa-paint-brush',
			'color' => '#db2777',
			'super_url' => '/' . $be . '/control/portal/epc_visual_page_editor',
			'tenant_url' => '/' . $be . '/control/portal/epc_visual_page_editor',
			'guide' => 'docs/guides/VISUAL_PAGE_EDITOR.md',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_visual_page_editor'),
		),
		'registration_enhanced' => array(
			'label' => 'Registration enhanced',
			'icon' => 'fa-user-plus',
			'color' => '#0891b2',
			'super_url' => '/' . $be . '/control/portal/epc_cp_auth_settings',
			'tenant_url' => '/' . $be . '/control/portal/epc_integrations_hub',
			'guide' => 'docs/guides/REGISTRATION_AND_AUTH.md',
			'super_only_config' => true,
			'default_enabled' => true,
			'menu_patterns' => array(),
		),
		'payment_gateways' => array(
			'label' => 'Payment gateways',
			'icon' => 'fa-credit-card',
			'color' => '#7c3aed',
			'super_url' => '/' . $be . '/shop/payments/payments',
			'tenant_url' => '/' . $be . '/shop/payments/payments',
			'guide' => 'docs/guides/INTEGRATIONS.md#payment',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('/shop/payments/'),
		),
		'api_integrations' => array(
			'label' => 'API clients & keys',
			'icon' => 'fa-code',
			'color' => '#475569',
			'super_url' => '/' . $be . '/control/portal/epc_api_clients_manage',
			'tenant_url' => '/' . $be . '/control/portal/epc_api_clients_manage',
			'guide' => 'docs/EPC-API-DOCUMENTATION.md',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_api_clients'),
		),
		'parts_agent' => array(
			'label' => 'AI parts agent',
			'icon' => 'fa-robot',
			'color' => '#8e44ad',
			'super_url' => '/' . $be . '/shop/parts_agent_chats',
			'tenant_url' => '/' . $be . '/shop/parts_agent_chats',
			'guide' => 'docs/EPC-AI-PARTS-EXPERT.md',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('parts_agent'),
		),
		'social_media_hub' => array(
			'label' => 'Social media hub',
			'icon' => 'fa-share-alt',
			'color' => '#e1306c',
			'super_url' => '/' . $be . '/control/portal/epc_social_media_hub',
			'tenant_url' => '/' . $be . '/control/portal/epc_social_media_hub',
			'guide' => '/' . $be . '/control/portal/epc_social_media_hub?tab=guide',
			'super_only_config' => false,
			'default_enabled' => true,
			'menu_patterns' => array('epc_social_media_hub'),
		),
		'tenant_registry' => array(
			'label' => 'Multi-tenant registry',
			'icon' => 'fa-sitemap',
			'color' => '#0369a1',
			'super_url' => '/' . $be . '/shop/tenant_hub/tenant_hub',
			'tenant_url' => '',
			'guide' => 'docs/EPC-SUPER-CP-TENANT-CONTROL.md',
			'super_only_config' => true,
			'default_enabled' => true,
			'menu_patterns' => array('tenant_hub'),
		),
	);
}

function epc_integrations_ensure_schema(PDO $pdo): void
{
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
	if (!$platformPdo instanceof PDO) {
		$platformPdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
	}
	if (!$platformPdo instanceof PDO) {
		return $default;
	}
	epc_integrations_ensure_schema($platformPdo);
	$st = $platformPdo->prepare('SELECT `enabled` FROM `epc_tenant_feature_flags` WHERE `site_key` = ? AND `feature_key` = ? LIMIT 1');
	$st->execute(array($siteKey, $featureKey));
	$row = $st->fetchColumn();
	if ($row === false) {
		return $default;
	}
	return (int) $row === 1;
}

/** @return array<string, bool> */
function epc_integrations_features_for_site(string $siteKey, ?PDO $platformPdo = null): array
{
	require_once __DIR__ . '/epc_perf_cache.php';
	$key = 'epc_int_features:v1:' . $siteKey;
	return epc_perf_cache_remember($key, 300, static function () use ($siteKey, $platformPdo) {
		$catalog = epc_integrations_catalog();
		$out = array();
		foreach ($catalog as $key => $meta) {
			$out[$key] = epc_integrations_feature_enabled($key, $siteKey, $platformPdo);
		}
		return $out;
	});
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
	foreach (epc_integrations_catalog() as $featureKey => $meta) {
		if (epc_integrations_feature_enabled($featureKey, $siteKey)) {
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
		$rows[] = array(
			'key' => $key,
			'label' => (string) $meta['label'],
			'icon' => (string) ($meta['icon'] ?? 'fa-plug'),
			'color' => (string) ($meta['color'] ?? '#64748b'),
			'enabled' => $enabled,
			'active' => $enabled,
			'configure_url' => $configUrl,
			'guide' => (string) ($meta['guide'] ?? ''),
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
