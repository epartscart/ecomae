<?php
/**
 * Portal settings persistence — industry panel drives CP + storefront.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';

function epc_portal_db_ensure(PDO $pdo)
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_portal_industry` (
			`code` VARCHAR(32) NOT NULL PRIMARY KEY,
			`name` VARCHAR(120) NOT NULL,
			`theme_json` TEXT NULL,
			`default_packs_json` TEXT NULL,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`sort_order` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	try {
		$pdo->query('SELECT `default_packs_json` FROM `epc_portal_industry` LIMIT 1');
	} catch (Exception $e) {
		$pdo->exec('ALTER TABLE `epc_portal_industry` ADD COLUMN `default_packs_json` TEXT NULL AFTER `theme_json`');
	}
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_portal_site_settings` (
			`host` VARCHAR(120) NOT NULL PRIMARY KEY,
			`industry_code` VARCHAR(32) NOT NULL DEFAULT \'auto_parts\',
			`system_name` VARCHAR(120) NOT NULL DEFAULT \'\',
			`hub_name` VARCHAR(120) NOT NULL DEFAULT \'\',
			`tagline` VARCHAR(255) NOT NULL DEFAULT \'\',
			`domain_path` VARCHAR(255) NOT NULL DEFAULT \'\',
			`contact_json` TEXT NULL,
			`enabled_packs_json` TEXT NULL,
			`theme_json` TEXT NULL,
			`updated_at` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	foreach (array(
		'domain_path' => "VARCHAR(255) NOT NULL DEFAULT '' AFTER `tagline`",
		'contact_json' => 'TEXT NULL AFTER `domain_path`',
		'cp_menu_json' => 'TEXT NULL AFTER `theme_json`',
		'theme_template' => "VARCHAR(32) NOT NULL DEFAULT 'classic' AFTER `industry_code`",
		'access_mode' => "VARCHAR(16) NOT NULL DEFAULT 'full' AFTER `theme_template`",
		'erp_modules_json' => 'TEXT NULL AFTER `access_mode`',
		'cp_default_lang' => "VARCHAR(8) NOT NULL DEFAULT 'en' AFTER `erp_modules_json`",
		'country_code' => "CHAR(2) NOT NULL DEFAULT 'AE' AFTER `cp_default_lang`",
	) as $col => $def) {
		try {
			$pdo->query('SELECT `' . $col . '` FROM `epc_portal_site_settings` LIMIT 1');
		} catch (Exception $e) {
			$pdo->exec('ALTER TABLE `epc_portal_site_settings` ADD COLUMN `' . $col . '` ' . $def);
		}
	}
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_portal_deploy_targets` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL,
			`hostname` VARCHAR(120) NOT NULL,
			`industry_code` VARCHAR(32) NOT NULL DEFAULT \'auto_parts\',
			`chunk_url` VARCHAR(255) NOT NULL DEFAULT \'\',
			`extract_url` VARCHAR(255) NOT NULL DEFAULT \'\',
			`setup_url` VARCHAR(255) NOT NULL DEFAULT \'\',
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`last_deploy_at` INT NOT NULL DEFAULT 0,
			`last_deploy_status` VARCHAR(32) NOT NULL DEFAULT \'\',
			`last_deploy_message` TEXT NULL,
			UNIQUE KEY `site_key` (`site_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	$now = time();
	$sort = 0;
	foreach (epc_portal_industries() as $row) {
		$sort += 10;
		$stmt = $pdo->prepare(
			'INSERT INTO `epc_portal_industry` (`code`, `name`, `theme_json`, `default_packs_json`, `active`, `sort_order`, `updated_at`)
			VALUES (?, ?, ?, ?, 1, ?, ?)
			ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `theme_json` = VALUES(`theme_json`),
			`default_packs_json` = VALUES(`default_packs_json`), `updated_at` = VALUES(`updated_at`)'
		);
		$stmt->execute(array(
			$row['code'],
			$row['name'],
			json_encode(isset($row['theme']) ? $row['theme'] : array()),
			json_encode(isset($row['cp_packs']) ? $row['cp_packs'] : array('core')),
			$sort,
			$now,
		));
	}

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_portal_tenants` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL,
			`hostname` VARCHAR(120) NOT NULL,
			`industry_code` VARCHAR(32) NOT NULL DEFAULT \'auto_parts\',
			`status` VARCHAR(24) NOT NULL DEFAULT \'draft\',
			`trade_name` VARCHAR(120) NOT NULL DEFAULT \'\',
			`hub_name` VARCHAR(120) NOT NULL DEFAULT \'\',
			`from_email` VARCHAR(120) NOT NULL DEFAULT \'\',
			`db_name` VARCHAR(64) NOT NULL DEFAULT \'\',
			`db_user` VARCHAR(64) NOT NULL DEFAULT \'\',
			`db_password` VARCHAR(255) NOT NULL DEFAULT \'\',
			`notes` VARCHAR(500) NOT NULL DEFAULT \'\',
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `site_key` (`site_key`),
			UNIQUE KEY `hostname` (`hostname`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);

	foreach (array(
		'intro_json' => 'TEXT NULL AFTER `notes`',
		'hosted_on' => "VARCHAR(24) NOT NULL DEFAULT 'client' AFTER `intro_json`",
		'erp_only_shared' => 'TINYINT(1) NOT NULL DEFAULT 0 AFTER `hosted_on`',
		'is_active' => 'TINYINT(1) NOT NULL DEFAULT 1',
		'operator_temp_password' => "VARCHAR(120) NOT NULL DEFAULT ''",
		'country_code' => "CHAR(2) NOT NULL DEFAULT 'AE' AFTER `operator_temp_password`",
		// 1000+ tenant scale: dedicated MySQL per tenant (default for new onboardings).
		'dedicated_db' => 'TINYINT(1) NOT NULL DEFAULT 0 AFTER `erp_only_shared`',
		'scale_policy' => "VARCHAR(32) NOT NULL DEFAULT 'shared_docpart' AFTER `dedicated_db`",
	) as $col => $def) {
		$chk = $pdo->query("SHOW COLUMNS FROM `epc_portal_tenants` LIKE " . $pdo->quote($col))->fetch(PDO::FETCH_ASSOC);
		if (!$chk) {
			$pdo->exec('ALTER TABLE `epc_portal_tenants` ADD COLUMN `' . $col . '` ' . $def);
		}
	}
	// Backfill: ERP-only shared companies already have dedicated DBs.
	try {
		$pdo->exec(
			"UPDATE `epc_portal_tenants`
			 SET `dedicated_db` = 1, `scale_policy` = 'dedicated_mysql'
			 WHERE (`erp_only_shared` = 1 OR (`db_name` != '' AND `db_name` != 'docpart'))
			   AND (`dedicated_db` = 0 OR `scale_policy` = 'shared_docpart' OR `scale_policy` = '')"
		);
	} catch (Exception $e) {
	}
	// Shared ERP-only tenants may share www.ecomae.com — site_key stays unique, hostname is not.
	try {
		$idx = $pdo->query("SHOW INDEX FROM `epc_portal_tenants` WHERE Key_name = 'hostname' AND Non_unique = 0")->fetch(PDO::FETCH_ASSOC);
		if ($idx) {
			$pdo->exec('ALTER TABLE `epc_portal_tenants` DROP INDEX `hostname`');
		}
		$idxLoose = $pdo->query("SHOW INDEX FROM `epc_portal_tenants` WHERE Key_name = 'hostname_lookup'")->fetch(PDO::FETCH_ASSOC);
		if (!$idxLoose) {
			$pdo->exec('ALTER TABLE `epc_portal_tenants` ADD INDEX `hostname_lookup` (`hostname`)');
		}
	} catch (Exception $e) {
	}

	$targets = array();
	$token = 'epartscart-deploy-2026';
	$targets['ecomae'] = array(
		'ecomae',
		'www.ecomae.com',
		'platform_host',
		'https://www.ecomae.com/chunk-receiver.php',
		'https://www.ecomae.com/extract-zip-ecomae.php?token=' . $token,
		'https://www.ecomae.com/epc-ecomae-setup.php',
	);
	$ins = $pdo->prepare(
		'INSERT INTO `epc_portal_deploy_targets` (`site_key`, `hostname`, `industry_code`, `chunk_url`, `extract_url`, `setup_url`, `active`)
		VALUES (?, ?, ?, ?, ?, ?, 1)
		ON DUPLICATE KEY UPDATE `hostname` = VALUES(`hostname`), `industry_code` = VALUES(`industry_code`),
		`chunk_url` = VALUES(`chunk_url`), `extract_url` = VALUES(`extract_url`), `setup_url` = VALUES(`setup_url`)'
	);
	foreach ($targets as $t) {
		$ins->execute($t);
	}
}

function epc_portal_pack_definitions()
{
	return array(
		'core' => array('label' => 'Core CP', 'desc' => 'Content, users, templates, print docs', 'icon' => 'fa-cog'),
		'commerce' => array('label' => 'Commerce', 'desc' => 'Orders, cart, payments, channels', 'icon' => 'fa-shopping-cart'),
		'catalogue' => array('label' => 'Catalogue', 'desc' => 'Product catalogue and bulk upload', 'icon' => 'fa-th-large'),
		'auto_parts' => array('label' => 'Auto parts', 'desc' => 'Prices, crosses, parts search, procurement', 'icon' => 'fa-car'),
		'logistics' => array('label' => 'Logistics', 'desc' => 'Warehouses, storages, offices — shared module', 'icon' => 'fa-truck'),
		'erp' => array('label' => 'ERP & finance', 'desc' => 'GL, VAT, e-invoicing, payroll — shared module', 'icon' => 'fa-university'),
		'crm' => array('label' => 'CRM & pipeline', 'desc' => 'Native leads, opportunities, activities & kanban pipeline', 'icon' => 'fa-handshake-o'),
		'professional' => array('label' => 'Professional services', 'desc' => 'Customers, approvals, demand intelligence', 'icon' => 'fa-briefcase'),
		'marketing' => array('label' => 'Marketing', 'desc' => 'Campaigns and analytics', 'icon' => 'fa-bullhorn'),
		'super_platform' => array('label' => 'Super CP (ecomae)', 'desc' => 'Tenant hub — all client sites', 'icon' => 'fa-cloud'),
	);
}

function epc_portal_default_site_settings($host)
{
	$sites = epc_portal_sites();
	$profile = null;
	if (isset($sites[$host])) {
		$profile = $sites[$host];
	} else {
		foreach ($sites as $pattern => $site) {
			if ($host !== '' && substr($host, -strlen($pattern)) === $pattern) {
				$profile = $site;
				break;
			}
		}
	}
	if ($profile === null) {
		$profile = array(
			'industry' => 'auto_parts',
			'system_name' => 'e-world Commerce System',
			'hub_name' => 'Electronic World Group',
			'tagline' => 'Designed by Electronic World Group',
			'domain_path' => epc_portal_guess_domain_path($host),
		);
	}
	$industry_code = isset($profile['industry']) ? $profile['industry'] : 'auto_parts';
	require_once __DIR__ . '/epc_portal_theme_templates.php';
	$themeTemplate = epc_portal_default_theme_template($industry_code);
	$ind = epc_portal_industry($industry_code);
	$contact = epc_portal_default_contact($profile);
	$hostNorm = preg_replace('/^www\./', '', strtolower(trim((string) $host)));
	$isEpartscart = ($hostNorm === 'epartscart.com' || $hostNorm === 'epartscart');
	$contact['use_animated_hub_logo'] = !($industry_code === 'auto_parts' || $isEpartscart);
	if ($industry_code === 'auto_parts' || $isEpartscart) {
		$contact['use_tenant_brand'] = false;
	}
	$packs = isset($ind['cp_packs']) ? $ind['cp_packs'] : array('core');
	if (!empty($profile['super_cp'])) {
		if (!in_array('super_platform', $packs, true)) {
			$packs[] = 'super_platform';
		}
	} elseif ($industry_code === 'platform_host') {
		$packs = array_values(array_filter($packs, function ($p) {
			return $p !== 'super_platform';
		}));
	}
	return array(
		'host' => $host,
		'industry_code' => $industry_code,
		'system_name' => isset($profile['system_name']) ? $profile['system_name'] : 'Portal',
		'hub_name' => isset($profile['hub_name']) ? $profile['hub_name'] : 'Hub',
		'tagline' => isset($profile['tagline']) ? $profile['tagline'] : '',
		'domain_path' => isset($profile['domain_path']) ? $profile['domain_path'] : epc_portal_guess_domain_path($host),
		'contact' => $contact,
		'enabled_packs' => $packs,
		'theme_template' => $themeTemplate,
		'theme' => epc_portal_style_template_theme($industry_code, $themeTemplate),
		'cp_menu' => array('hidden_groups' => array(), 'hidden_items' => array()),
		'access_mode' => 'full',
		'erp_modules' => array(),
	);
}

function epc_portal_load_site_settings(?PDO $pdo = null)
{
	static $cachedByHost = array();
	$host = epc_portal_host();
	$cacheKey = $host;
	if (!empty($GLOBALS['epc_demo_storefront_context']) && !empty($GLOBALS['epc_demo_storefront_site_key'])) {
		$cacheKey = 'demo:' . (string) $GLOBALS['epc_demo_storefront_site_key'];
	}
	if (!empty($GLOBALS['epc_demo_cp_context']) && !empty($GLOBALS['epc_demo_cp_site_key'])) {
		$cacheKey = 'demo-cp:' . (string) $GLOBALS['epc_demo_cp_site_key'];
	}
	if ($pdo === null && function_exists('epc_portal_is_cp_request') && epc_portal_is_cp_request()) {
		$sharedFile = __DIR__ . '/epc_portal_shared_erp.php';
		if (is_file($sharedFile)) {
			require_once $sharedFile;
			$sharedRow = epc_portal_shared_erp_active_tenant();
			if ($sharedRow !== null) {
				$cacheKey = $host . ':erp:' . (string) $sharedRow['site_key'];
			}
		}
	}
	if ($pdo === null && $cacheKey !== '' && isset($cachedByHost[$cacheKey])) {
		return $cachedByHost[$cacheKey];
	}
	if ($pdo === null && $cacheKey !== '') {
		$perfFile = __DIR__ . '/epc_perf_cache.php';
		if (is_file($perfFile)) {
			require_once $perfFile;
			$perfKey = 'epc_site_settings:v1:' . $cacheKey;
			$perfCached = epc_perf_cache_get($perfKey);
			if (is_array($perfCached) && $perfCached !== array()) {
				$cachedByHost[$cacheKey] = $perfCached;
				return $perfCached;
			}
		}
	}
	$defaultsHost = $host;
	if (!empty($GLOBALS['epc_demo_storefront_context'])) {
		$demoIndustry = (string) ($GLOBALS['epc_demo_storefront_industry'] ?? 'auto_parts');
		$defaultsHost = ($demoIndustry === 'auto_parts') ? 'www.epartscart.com' : $host;
	}
	if (!empty($GLOBALS['epc_demo_cp_context'])) {
		$demoIndustry = (string) (($GLOBALS['epc_demo_cp_tenant_row']['industry_code'] ?? '') ?: 'auto_parts');
		$defaultsHost = ($demoIndustry === 'auto_parts') ? 'www.epartscart.com' : $host;
	}
	$defaults = epc_portal_default_site_settings($defaultsHost);
	$isClientStorefront = function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname($host)
		&& !(function_exists('epc_portal_is_cp_request') && epc_portal_is_cp_request())
		&& empty($GLOBALS['epc_demo_storefront_context'])
		&& empty($GLOBALS['epc_demo_cp_context']);
	// Avoid extra PDO before dp_core on tenant storefronts — prevents MySQL connection storms.
	if ($pdo === null && $isClientStorefront) {
		if ($cacheKey !== '') {
			$cachedByHost[$cacheKey] = $defaults;
		}
		return $defaults;
	}
	if ($pdo === null) {
		try {
			$cfg = null;
			if ((!empty($GLOBALS['epc_demo_storefront_context']) || !empty($GLOBALS['epc_demo_cp_context']))
				&& isset($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config'])) {
				$cfg = $GLOBALS['DP_Config'];
				$pdo = new PDO(
					'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8;connect_timeout=3',
					$cfg->user,
					$cfg->password,
					array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
				);
			} elseif (in_array($host, array('www.ecomae.com', 'ecomae.com', 'cp.ecomae.com'), true)) {
				$sharedFile = __DIR__ . '/epc_portal_shared_erp.php';
				if (is_file($sharedFile)) {
					require_once $sharedFile;
					$sharedRow = epc_portal_shared_erp_active_tenant();
					if ($sharedRow !== null) {
						$pdo = epc_portal_shared_erp_tenant_pdo($sharedRow);
					}
				}
				if (!$pdo instanceof PDO) {
					require_once __DIR__ . '/epc_portal_tenant.php';
					$pdo = epc_portal_platform_pdo();
				}
			} elseif (function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname($host)) {
				require_once __DIR__ . '/epc_portal_tenant.php';
				$pdo = epc_portal_tenant_storefront_pdo();
			} else {
				require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
				$cfg = new DP_Config();
			}
			if (!$pdo instanceof PDO) {
				if ($cfg === null) {
					require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
					$cfg = new DP_Config();
				}
				$pdo = new PDO(
					'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8;connect_timeout=2',
					$cfg->user,
					$cfg->password,
					array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 2)
				);
			}
		} catch (Exception $e) {
			if ($cacheKey !== '') {
				$cachedByHost[$cacheKey] = $defaults;
			}
			return $defaults;
		}
	}
	try {
		epc_portal_db_ensure($pdo);
		$st = $pdo->prepare('SELECT * FROM `epc_portal_site_settings` WHERE `host` = ? OR `host` = ? LIMIT 1');
		$bareHost = preg_replace('/^www\./', '', $host);
		$st->execute(array($host, $bareHost));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			if ($cacheKey !== '') {
				$cachedByHost[$cacheKey] = $defaults;
			}
			return $defaults;
		}
		$packs = json_decode((string) $row['enabled_packs_json'], true);
		$theme = json_decode((string) $row['theme_json'], true);
		$contact = json_decode((string) ($row['contact_json'] ?? ''), true);
		require_once __DIR__ . '/epc_portal_cp_menu.php';
		$cpMenu = json_decode((string) ($row['cp_menu_json'] ?? ''), true);
		if (!is_array($cpMenu)) {
			$cpMenu = $defaults['cp_menu'];
		} else {
			$cpMenu = array_merge(epc_portal_cp_menu_defaults(), $cpMenu);
		}
		require_once __DIR__ . '/epc_portal_theme_templates.php';
		$industryCode = (string) $row['industry_code'];
		$themeTemplate = epc_portal_normalize_theme_template(
			$industryCode,
			isset($row['theme_template']) ? (string) $row['theme_template'] : 'classic'
		);
		$accessMode = isset($row['access_mode']) ? (string) $row['access_mode'] : 'full';
		$styleTheme = epc_portal_style_template_theme($industryCode, $themeTemplate);
		if (is_array($theme) && $theme !== array()) {
			$styleTheme = array_merge($styleTheme, $theme);
		}
		require_once __DIR__ . '/epc_portal_erp_modules.php';
		$erpModules = epc_portal_erp_modules_normalize_list($row['erp_modules_json'] ?? '');
		$cpDefaultLang = preg_replace('/[^a-z\-]/', '', strtolower((string) ($row['cp_default_lang'] ?? 'en')));
		if ($cpDefaultLang === '') {
			$cpDefaultLang = 'en';
		}
		$integrations = json_decode((string) ($row['integrations_json'] ?? ''), true);
		if (!is_array($integrations)) {
			$integrations = array();
		}
		$cached = array(
			'host' => $host,
			'industry_code' => $industryCode,
			'theme_template' => $themeTemplate,
			'access_mode' => $accessMode,
			'erp_modules' => $erpModules,
			'cp_default_lang' => $cpDefaultLang,
			'system_name' => $row['system_name'],
			'hub_name' => $row['hub_name'],
			'tagline' => $row['tagline'],
			'domain_path' => isset($row['domain_path']) ? (string) $row['domain_path'] : $defaults['domain_path'],
			'contact' => is_array($contact) ? array_merge($defaults['contact'], $contact) : $defaults['contact'],
			'enabled_packs' => is_array($packs) ? $packs : $defaults['enabled_packs'],
			'theme' => $styleTheme,
			'cp_menu' => $cpMenu,
			'integrations' => $integrations,
		);
		$cached = epc_portal_normalize_site_settings($host, $cached, $defaults);
	} catch (Exception $e) {
		$cached = $defaults;
	}
	if ($cacheKey !== '') {
		$cachedByHost[$cacheKey] = $cached;
		if ($pdo === null) {
			$perfFile = __DIR__ . '/epc_perf_cache.php';
			if (is_file($perfFile)) {
				require_once $perfFile;
				epc_perf_cache_set('epc_site_settings:v1:' . $cacheKey, $cached, 300);
			}
		}
	}
	return $cached;
}

function epc_portal_normalize_site_settings(string $host, array $settings, array $defaults): array
{
	$profile = null;
	if (function_exists('epc_portal_sites')) {
		$sites = epc_portal_sites();
		if (isset($sites[$host])) {
			$profile = $sites[$host];
		}
	}
	$isSuperCp = !empty($profile['super_cp'])
		|| $host === 'cp.ecomae.com'
		|| (
			($settings['industry_code'] ?? '') === 'platform_host'
			&& function_exists('epc_portal_is_cp_request')
			&& epc_portal_is_cp_request()
		);
	if ($isSuperCp) {
		if (!in_array('super_platform', $settings['enabled_packs'], true)) {
			$settings['enabled_packs'][] = 'super_platform';
		}
		$settings['enabled_packs'] = array_values(array_unique(array_merge(
			$settings['enabled_packs'],
			array('core', 'commerce', 'professional', 'marketing', 'super_platform', 'erp', 'catalogue')
		)));
	} elseif (($settings['industry_code'] ?? '') === 'platform_host') {
		$settings['enabled_packs'] = array_values(array_filter($settings['enabled_packs'], function ($p) {
			return $p !== 'super_platform';
		}));
	}
	if (empty($settings['industry_code']) && !empty($defaults['industry_code'])) {
		$settings['industry_code'] = $defaults['industry_code'];
	}
	if (
		in_array($host, array('www.ecomae.com', 'ecomae.com', 'cp.ecomae.com'), true)
		&& empty($GLOBALS['epc_demo_storefront_context'])
	) {
		$settings['industry_code'] = 'platform_host';
		if (!empty($defaults['hub_name'])) {
			$settings['hub_name'] = $defaults['hub_name'];
		}
		if (!empty($defaults['tagline'])) {
			$settings['tagline'] = $defaults['tagline'];
		}
		if (!empty($defaults['domain_path'])) {
			$settings['domain_path'] = $defaults['domain_path'];
		}
	}
	if (!empty($GLOBALS['epc_demo_storefront_context']) && !empty($GLOBALS['epc_demo_storefront_site_key'])) {
		$demoIndustry = (string) ($GLOBALS['epc_demo_storefront_industry'] ?? '');
		if ($demoIndustry !== '') {
			$settings['industry_code'] = $demoIndustry;
		}
		$settings['domain_path'] = 'https://www.ecomae.com/demo/' . preg_replace('/[^a-z0-9_]/', '', (string) $GLOBALS['epc_demo_storefront_site_key']) . '/';
	}
	if (function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname($host)) {
		$settings['enabled_packs'] = array_values(array_filter($settings['enabled_packs'], function ($p) {
			return $p !== 'super_platform';
		}));
		if (($settings['industry_code'] ?? '') === 'platform_host') {
			$settings['industry_code'] = !empty($defaults['industry_code']) ? $defaults['industry_code'] : 'auto_parts';
		}
		if ($host !== '') {
			$settings['domain_path'] = 'https://' . $host . '/';
		}
	}
	require_once __DIR__ . '/epc_portal_erp_modules.php';
	if (empty($settings['erp_modules']) || !is_array($settings['erp_modules'])) {
		$settings['erp_modules'] = array();
	}
	$settings['erp_modules'] = epc_portal_erp_modules_normalize_list($settings['erp_modules']);
	if (count($settings['erp_modules']) === 0) {
		$mode = function_exists('epc_portal_resolve_access_mode')
			? epc_portal_resolve_access_mode($settings)
			: (string) ($settings['access_mode'] ?? 'full');
		$settings['erp_modules'] = epc_portal_erp_modules_default_ids($mode);
	}
	if (function_exists('epc_portal_resolve_access_mode')) {
		$settings['access_mode'] = epc_portal_resolve_access_mode($settings);
	}
	return $settings;
}

function epc_portal_save_site_settings(PDO $pdo, array $data)
{
	epc_portal_db_ensure($pdo);
	$host = !empty($data['host']) ? (string) $data['host'] : epc_portal_host();
	$packs = isset($data['enabled_packs']) && is_array($data['enabled_packs']) ? $data['enabled_packs'] : array('core');
	if (!in_array('core', $packs, true)) {
		array_unshift($packs, 'core');
	}
	if (function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname($host)) {
		$packs = array_values(array_filter($packs, function ($p) {
			return $p !== 'super_platform';
		}));
		if (preg_replace('/[^a-z0-9_]/', '', (string) ($data['industry_code'] ?? '')) === 'platform_host') {
			$data['industry_code'] = 'auto_parts';
		}
	}
	$theme = isset($data['theme']) && is_array($data['theme']) ? $data['theme'] : array();
	$contact = isset($data['contact']) && is_array($data['contact']) ? $data['contact'] : array();
	require_once __DIR__ . '/epc_portal_cp_menu.php';
	$cpMenu = isset($data['cp_menu']) && is_array($data['cp_menu']) ? $data['cp_menu'] : epc_portal_cp_menu_defaults();
	$cpMenu['hidden_groups'] = array_values(array_unique(array_map('intval', (array) ($cpMenu['hidden_groups'] ?? array()))));
	$cpMenu['hidden_items'] = array_values(array_unique(array_map('intval', (array) ($cpMenu['hidden_items'] ?? array()))));
	require_once __DIR__ . '/epc_portal_theme_templates.php';
	$industryForTheme = preg_replace('/[^a-z0-9_]/', '', (string) ($data['industry_code'] ?? 'auto_parts'));
	if ($industryForTheme === '') {
		$industryForTheme = 'auto_parts';
	}
	$themeTemplate = epc_portal_normalize_theme_template($industryForTheme, (string) ($data['theme_template'] ?? 'classic'));
	if ($theme === array()) {
		$theme = epc_portal_style_template_theme($industryForTheme, $themeTemplate);
	}
	$accessMode = isset($data['access_mode']) ? (string) $data['access_mode'] : 'full';
	if ($accessMode === 'full_commerce') {
		$accessMode = 'full';
	}
	if (!in_array($accessMode, array('full', 'erp_only', 'consultancy', 'mixed'), true)) {
		$accessMode = 'full';
	}
	require_once __DIR__ . '/epc_portal_erp_modules.php';
	$erpModules = isset($data['erp_modules']) ? epc_portal_erp_modules_normalize_list($data['erp_modules']) : array();
	if (count($erpModules) === 0 && !empty($data['erp_modules_json'])) {
		$erpModules = epc_portal_erp_modules_normalize_list($data['erp_modules_json']);
	}
	if (count($erpModules) === 0) {
		$erpModules = epc_portal_erp_modules_default_ids($accessMode);
	}
	$cpDefaultLang = 'en';
	if (isset($data['cp_default_lang'])) {
		$cpDefaultLang = preg_replace('/[^a-z\-]/', '', strtolower((string) $data['cp_default_lang']));
	}
	if ($cpDefaultLang === '') {
		$cpDefaultLang = 'en';
	}
	$integrations = isset($data['integrations']) && is_array($data['integrations']) ? $data['integrations'] : array();
	try {
		$pdo->query('SELECT `integrations_json` FROM `epc_portal_site_settings` LIMIT 1');
	} catch (Exception $e) {
		$pdo->exec("ALTER TABLE `epc_portal_site_settings` ADD COLUMN `integrations_json` TEXT NULL AFTER `cp_menu_json`");
	}
	$countryCode = 'AE';
	if (!empty($data['country_code'])) {
		$countryCode = strtoupper(substr(preg_replace('/[^A-Za-z]/', '', (string) $data['country_code']), 0, 2));
	} elseif (!empty($contact['country_code'])) {
		$countryCode = strtoupper(substr((string) $contact['country_code'], 0, 2));
	}
	if ($countryCode === '') {
		$countryCode = 'AE';
	}
	$contact['country_code'] = $countryCode;
	if (empty($contact['country']) && function_exists('epc_countries_iso3166_alpha2')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
		$countryNames = epc_countries_iso3166_alpha2();
		if (isset($countryNames[$countryCode])) {
			$contact['country'] = $countryNames[$countryCode];
		}
	}
	$stmt = $pdo->prepare(
		'INSERT INTO `epc_portal_site_settings`
		(`host`, `industry_code`, `theme_template`, `access_mode`, `erp_modules_json`, `cp_default_lang`, `country_code`, `system_name`, `hub_name`, `tagline`, `domain_path`, `contact_json`, `enabled_packs_json`, `theme_json`, `cp_menu_json`, `integrations_json`, `updated_at`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
		ON DUPLICATE KEY UPDATE
		`industry_code` = VALUES(`industry_code`), `theme_template` = VALUES(`theme_template`),
		`access_mode` = VALUES(`access_mode`), `erp_modules_json` = VALUES(`erp_modules_json`),
		`cp_default_lang` = VALUES(`cp_default_lang`), `country_code` = VALUES(`country_code`),
		`system_name` = VALUES(`system_name`),
		`hub_name` = VALUES(`hub_name`), `tagline` = VALUES(`tagline`),
		`domain_path` = VALUES(`domain_path`), `contact_json` = VALUES(`contact_json`),
		`enabled_packs_json` = VALUES(`enabled_packs_json`), `theme_json` = VALUES(`theme_json`),
		`cp_menu_json` = VALUES(`cp_menu_json`),
		`integrations_json` = VALUES(`integrations_json`),
		`updated_at` = VALUES(`updated_at`)'
	);
	$stmt->execute(array(
		$host,
		$industryForTheme,
		$themeTemplate,
		$accessMode,
		json_encode(array_values($erpModules)),
		substr($cpDefaultLang, 0, 8),
		$countryCode,
		substr((string) $data['system_name'], 0, 120),
		substr((string) $data['hub_name'], 0, 120),
		substr((string) $data['tagline'], 0, 255),
		substr((string) ($data['domain_path'] ?? ''), 0, 255),
		json_encode($contact),
		json_encode(array_values(array_unique($packs))),
		json_encode($theme),
		json_encode($cpMenu),
		json_encode($integrations),
		time(),
	));
	if (session_status() !== PHP_SESSION_ACTIVE) {
		@session_start();
	}
	$_SESSION[epc_portal_cp_industry_session_key()] = preg_replace('/[^a-z0-9_]/', '', (string) $data['industry_code']);
}

function epc_portal_enabled_packs()
{
	$settings = epc_portal_load_site_settings();
	return isset($settings['enabled_packs']) ? $settings['enabled_packs'] : array('core');
}

/** Load portal settings for a hostname from a specific database (Super CP tenant sync). */
function epc_portal_load_site_settings_for_host(PDO $pdo, string $host): array
{
	$host = strtolower(trim($host));
	if ($host !== '' && strpos($host, 'www.') !== 0) {
		$host = 'www.' . $host;
	}
	$defaults = epc_portal_default_site_settings($host);
	$st = $pdo->prepare('SELECT * FROM `epc_portal_site_settings` WHERE `host` = ? OR `host` = ? LIMIT 1');
	$bare = preg_replace('/^www\./', '', $host);
	$st->execute(array($host, $bare));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return $defaults;
	}
	$packs = json_decode((string) ($row['enabled_packs_json'] ?? ''), true);
	$contact = json_decode((string) ($row['contact_json'] ?? ''), true);
	$theme = json_decode((string) ($row['theme_json'] ?? ''), true);
	$cpMenu = json_decode((string) ($row['cp_menu_json'] ?? ''), true);
	require_once __DIR__ . '/epc_portal_cp_menu.php';
	require_once __DIR__ . '/epc_portal_erp_modules.php';
	$erpModules = epc_portal_erp_modules_normalize_list($row['erp_modules_json'] ?? '');
	$settings = array_merge($defaults, array(
		'host' => (string) $row['host'],
		'industry_code' => (string) ($row['industry_code'] ?? $defaults['industry_code']),
		'theme_template' => (string) ($row['theme_template'] ?? $defaults['theme_template']),
		'access_mode' => (string) ($row['access_mode'] ?? 'full'),
		'cp_default_lang' => preg_replace('/[^a-z\-]/', '', strtolower((string) ($row['cp_default_lang'] ?? 'en'))) ?: 'en',
		'country_code' => strtoupper(substr((string) ($row['country_code'] ?? ($contact['country_code'] ?? 'AE')), 0, 2)) ?: 'AE',
		'system_name' => (string) ($row['system_name'] ?? $defaults['system_name']),
		'hub_name' => (string) ($row['hub_name'] ?? $defaults['hub_name']),
		'tagline' => (string) ($row['tagline'] ?? $defaults['tagline']),
		'domain_path' => (string) ($row['domain_path'] ?? $defaults['domain_path']),
		'enabled_packs' => is_array($packs) ? $packs : $defaults['enabled_packs'],
		'contact' => is_array($contact) ? array_merge($defaults['contact'], $contact) : $defaults['contact'],
		'theme' => is_array($theme) ? $theme : $defaults['theme'],
		'cp_menu' => is_array($cpMenu) ? array_merge(epc_portal_cp_menu_defaults(), $cpMenu) : $defaults['cp_menu'],
		'erp_modules' => $erpModules,
	));
	$settings['access_mode'] = epc_portal_resolve_access_mode($settings);
	if (count($settings['erp_modules']) === 0) {
		$settings['erp_modules'] = epc_portal_erp_modules_default_ids($settings['access_mode']);
	}
	return $settings;
}

function epc_portal_deploy_targets(PDO $pdo)
{
	epc_portal_db_ensure($pdo);
	return $pdo->query('SELECT * FROM `epc_portal_deploy_targets` WHERE `active` = 1 ORDER BY `hostname` ASC')->fetchAll(PDO::FETCH_ASSOC);
}
