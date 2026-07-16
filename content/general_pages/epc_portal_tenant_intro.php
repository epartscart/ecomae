<?php
/**
 * Client onboarding — intro form fields, validation, launch checklist.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_portal_tenant.php';

function epc_portal_intro_field_defs(): array
{
	return array(
		'contact_person' => array('label' => 'Contact person', 'required' => true),
		'contact_email' => array('label' => 'Contact email', 'required' => true),
		'contact_phone' => array('label' => 'Phone / WhatsApp', 'required' => false),
		'legal_name' => array('label' => 'Legal company name', 'required' => false),
		'trn' => array('label' => 'TRN / VAT number', 'required' => false),
		'city' => array('label' => 'City', 'required' => false),
		'country' => array('label' => 'Country', 'required' => true),
		'country_code' => array('label' => 'Country code', 'required' => false),
		'head_office_address' => array('label' => 'Head office address', 'required' => false),
		'admin_email' => array('label' => 'Admin CP email', 'required' => true),
		'tagline' => array('label' => 'Tagline', 'required' => false),
		'domain_registrar' => array('label' => 'Domain registrar', 'required' => false),
		'launch_notes' => array('label' => 'Launch notes', 'required' => false),
	);
}

function epc_portal_intro_defaults(): array
{
	return array(
		'contact_person' => '',
		'contact_email' => '',
		'contact_phone' => '',
		'legal_name' => '',
		'trn' => '',
		'city' => '',
		'country' => 'United Arab Emirates',
		'country_code' => 'AE',
		'head_office_address' => '',
		'admin_email' => '',
		'tagline' => 'Designed by Electronic World Group',
		'domain_registrar' => 'GoDaddy',
		'launch_notes' => '',
		'submitted_at' => 0,
		'submitted_by' => '',
	);
}

function epc_portal_intro_from_post(array $post): array
{
	$intro = epc_portal_intro_defaults();
	foreach (epc_portal_intro_field_defs() as $key => $def) {
		if (isset($post[$key])) {
			$intro[$key] = trim((string) $post[$key]);
		}
	}
	$intro['erp_only'] = !empty($post['erp_only']) || !empty($post['tenant_mode']) && (string) $post['tenant_mode'] === 'erp_only';
	require_once __DIR__ . '/epc_portal_erp_modules.php';
	$intro['access_mode'] = isset($post['access_mode']) ? (string) $post['access_mode'] : '';
	if ($intro['access_mode'] === '' && $intro['erp_only']) {
		$intro['access_mode'] = 'erp_only';
	}
	if ($intro['access_mode'] === 'full_commerce') {
		$intro['access_mode'] = 'full';
	}
	$mods = epc_portal_erp_modules_from_post($post);
	$intro['erp_modules'] = count($mods) > 0 ? $mods : array();
	$intro['erp_modules_preset'] = isset($post['erp_modules_preset']) ? (string) $post['erp_modules_preset'] : '';
	$intro['erp_only_shared'] = !empty($post['erp_only_shared']) || !empty($post['hosted_on_platform']);
	if ($intro['erp_only_shared']) {
		$intro['hosted_on'] = 'platform';
	}
	// Scale policy: default dedicated MySQL for new onboardings (1000+ tenant readiness).
	// Explicit shared_docpart / dedicated_db=0 keeps legacy Model C shared commerce DB.
	if (isset($post['scale_policy'])) {
		$intro['scale_policy'] = strtolower(trim((string) $post['scale_policy']));
	}
	if (array_key_exists('dedicated_db', $post)) {
		$intro['dedicated_db'] = !empty($post['dedicated_db']) ? 1 : 0;
	} elseif (!empty($intro['erp_only_shared'])) {
		$intro['dedicated_db'] = 1;
		$intro['scale_policy'] = 'dedicated_mysql';
	} elseif (!isset($intro['dedicated_db'])) {
		$intro['dedicated_db'] = 1;
		$intro['scale_policy'] = 'dedicated_mysql';
	}
	if (!empty($post['country_code'])) {
		$intro['country_code'] = strtoupper(substr(preg_replace('/[^A-Za-z]/', '', (string) $post['country_code']), 0, 2));
	} elseif (!empty($intro['country']) && function_exists('epc_countries_normalize_code')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
		$cc = epc_countries_normalize_code((string) $intro['country']);
		if ($cc !== '') {
			$intro['country_code'] = $cc;
		}
	}
	// Optional theme overrides from onboard form (auto-derived from industry when blank).
	if (isset($post['theme_template'])) {
		$intro['theme_template'] = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $post['theme_template']));
	}
	if (isset($post['storefront_package'])) {
		$intro['storefront_package'] = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $post['storefront_package']));
	}
	return $intro;
}

function epc_portal_intro_merge(array $stored, array $incoming): array
{
	$base = epc_portal_intro_defaults();
	foreach ($base as $key => $default) {
		if (isset($incoming[$key]) && $incoming[$key] !== '') {
			$base[$key] = $incoming[$key];
		} elseif (isset($stored[$key]) && $stored[$key] !== '') {
			$base[$key] = $stored[$key];
		}
	}
	if (!empty($incoming['submitted_at'])) {
		$base['submitted_at'] = (int) $incoming['submitted_at'];
	} elseif (!empty($stored['submitted_at'])) {
		$base['submitted_at'] = (int) $stored['submitted_at'];
	}
	if (!empty($incoming['submitted_by'])) {
		$base['submitted_by'] = (string) $incoming['submitted_by'];
	} elseif (!empty($stored['submitted_by'])) {
		$base['submitted_by'] = (string) $stored['submitted_by'];
	}
	return $base;
}

function epc_portal_intro_decode(?string $json): array
{
	if ($json === null || $json === '') {
		return epc_portal_intro_defaults();
	}
	$data = json_decode($json, true);
	if (!is_array($data)) {
		return epc_portal_intro_defaults();
	}
	return epc_portal_intro_merge(array(), $data);
}

function epc_portal_intro_validate(array $intro, array $tenantData): array
{
	$errors = array();
	foreach (epc_portal_intro_field_defs() as $key => $def) {
		if (!empty($def['required']) && trim((string) ($intro[$key] ?? '')) === '') {
			$errors[] = $def['label'] . ' is required';
		}
	}
	$email = trim((string) ($intro['contact_email'] ?? ''));
	if ($email !== '' && !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		$errors[] = 'Contact email is invalid';
	}
	$admin = trim((string) ($intro['admin_email'] ?? ''));
	if ($admin !== '' && !filter_var($admin, FILTER_VALIDATE_EMAIL)) {
		$errors[] = 'Admin CP email is invalid';
	}
	$hostname = strtolower(trim((string) ($tenantData['hostname'] ?? '')));
	$shared = !empty($intro['erp_only_shared']) || !empty($tenantData['erp_only_shared'])
		|| (string) ($tenantData['hosted_on'] ?? '') === 'platform';
	if (!$shared && ($hostname === '' || strpos($hostname, '.') === false)) {
		$errors[] = 'Primary domain (www.client.com) is required — or enable shared ERP on ecomae.com';
	}
	$trade = trim((string) ($tenantData['trade_name'] ?? ''));
	if ($trade === '') {
		$errors[] = 'Trade / brand name is required';
	}
	$cc = strtoupper(substr((string) ($intro['country_code'] ?? ''), 0, 2));
	if ($cc === '' && !empty($intro['country'])) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
		$cc = epc_countries_normalize_code((string) $intro['country']);
	}
	if ($cc === '') {
		$errors[] = 'Country is required';
	}
	return $errors;
}

function epc_portal_site_key_from_hostname(string $hostname): string
{
	$host = strtolower(trim($hostname));
	$host = preg_replace('/^www\./', '', $host);
	$host = preg_replace('/\.[a-z0-9.-]+$/', '', $host);
	return preg_replace('/[^a-z0-9_]/', '', str_replace('-', '_', $host));
}

function epc_portal_tenant_get(PDO $pdo, string $siteKey): ?array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return null;
	}
	require_once __DIR__ . '/epc_portal_db.php';
	epc_portal_db_ensure($pdo);
	$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
	$st->execute(array($key));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_portal_apply_intro_to_site_settings(PDO $pdo, string $hostname, array $tenantRow, array $intro): void
{
	$settings = epc_portal_default_site_settings($hostname);
	$settings['host'] = $hostname;
	$settings['industry_code'] = (string) ($tenantRow['industry_code'] ?? 'auto_parts');
	$settings['hub_name'] = (string) ($tenantRow['trade_name'] ?? $settings['hub_name']);
	if (!empty($intro['tagline'])) {
		$settings['tagline'] = (string) $intro['tagline'];
	}
	$settings['domain_path'] = 'https://' . $hostname . '/';

	$countryCode = strtoupper(substr((string) ($intro['country_code'] ?? ''), 0, 2));
	if ($countryCode === '' && !empty($intro['country']) && function_exists('epc_countries_normalize_code')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
		$countryCode = epc_countries_normalize_code((string) $intro['country']);
	}
	if ($countryCode === '') {
		$countryCode = 'AE';
	}
	$contact = epc_portal_default_contact(array(
		'trade_name' => (string) ($tenantRow['trade_name'] ?? ''),
		'hub_name' => (string) ($tenantRow['hub_name'] ?? ''),
		'from_email' => (string) ($tenantRow['from_email'] ?? ''),
		'admin_email' => (string) ($intro['admin_email'] ?? ''),
		'contact_phone' => (string) ($intro['contact_phone'] ?? ''),
		'head_office_address' => (string) ($intro['head_office_address'] ?? ''),
		'head_office_email' => (string) ($intro['contact_email'] ?? ''),
		'city' => (string) ($intro['city'] ?? ''),
		'country' => (string) ($intro['country'] ?? ''),
		'country_code' => $countryCode,
	));
	$contact['trade_name'] = (string) ($tenantRow['trade_name'] ?? $contact['trade_name']);
	if (!empty($intro['contact_person'])) {
		$contact['from_name'] = (string) $intro['contact_person'];
	}
	if (!empty($tenantRow['from_email'])) {
		$contact['from_email'] = (string) $tenantRow['from_email'];
	} elseif (!empty($intro['contact_email'])) {
		$contact['from_email'] = (string) $intro['contact_email'];
	}
	$industryCode = preg_replace('/[^a-z0-9_]/', '', (string) ($tenantRow['industry_code'] ?? ''));
	$erpOnly = !empty($intro['erp_only'])
		|| $industryCode === 'erp_standalone';
	require_once __DIR__ . '/epc_portal_erp_modules.php';
	require_once __DIR__ . '/epc_portal_storefront_packages.php';
	require_once __DIR__ . '/epc_portal_theme_templates.php';
	$accessFromIntro = isset($intro['access_mode']) ? (string) $intro['access_mode'] : '';
	if ($erpOnly && $accessFromIntro === '') {
		$accessFromIntro = 'erp_only';
	}
	if ($erpOnly || $accessFromIntro === 'erp_only') {
		$settings['industry_code'] = 'erp_standalone';
		$settings['access_mode'] = 'erp_only';
		$settings['enabled_packs'] = epc_portal_erp_only_packs();
		$settings['system_name'] = (string) ($tenantRow['trade_name'] ?? 'ERP Suite') . ' ERP';
		$themeOpts = array(
			'erp_only' => true,
			'skip_package' => true,
			'theme_template' => !empty($intro['theme_template']) ? (string) $intro['theme_template'] : 'classic',
		);
		epc_portal_apply_industry_theme_profile($settings, $contact, 'erp_standalone', $themeOpts);
	} else {
		if ($accessFromIntro === 'mixed') {
			$settings['access_mode'] = 'mixed';
			if (!in_array('erp', $settings['enabled_packs'] ?? array(), true)) {
				$settings['enabled_packs'] = array_merge($settings['enabled_packs'] ?? array('core'), array('erp', 'professional'));
			}
		} elseif ($accessFromIntro !== '' && in_array($accessFromIntro, array('full', 'consultancy'), true)) {
			$settings['access_mode'] = $accessFromIntro;
		}
		// Every commerce industry: seed matching visual style + storefront package when registered.
		$themeOpts = array(
			'erp_only' => false,
			'keep_packs' => ($accessFromIntro === 'mixed'),
			'keep_access_mode' => ($accessFromIntro !== ''),
		);
		if (!empty($intro['theme_template'])) {
			$themeOpts['theme_template'] = (string) $intro['theme_template'];
		}
		if (isset($intro['storefront_package']) && (string) $intro['storefront_package'] !== '') {
			$themeOpts['storefront_package'] = (string) $intro['storefront_package'];
		}
		epc_portal_apply_industry_theme_profile($settings, $contact, $industryCode !== '' ? $industryCode : 'auto_parts', $themeOpts);
	}
	$mods = epc_portal_erp_modules_resolve_for_onboard(
		$intro,
		(string) ($settings['industry_code'] ?? $industryCode),
		(string) ($settings['access_mode'] ?? 'full')
	);
	$settings['erp_modules'] = $mods;
	$settings['contact'] = $contact;
	epc_portal_save_site_settings($pdo, $settings);
}

/**
 * Re-apply industry theme/package to an existing tenant (platform settings + optional client DB push).
 *
 * @param array $opts theme_template, storefront_package, push_client (bool, default true when live)
 * @return array{ok:bool,message:string,theme_template?:string,storefront_package?:string}
 */
function epc_portal_apply_industry_theme_to_tenant(PDO $pdo, string $siteKey, array $opts = array()): array
{
	require_once __DIR__ . '/epc_portal_db.php';
	require_once __DIR__ . '/epc_portal_storefront_packages.php';
	require_once __DIR__ . '/epc_portal_theme_templates.php';
	epc_portal_db_ensure($pdo);

	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return array('ok' => false, 'message' => 'Invalid tenant');
	}
	$row = epc_portal_tenant_get($pdo, $key);
	if ($row === null) {
		return array('ok' => false, 'message' => 'Tenant not found');
	}
	$hostname = (string) ($row['hostname'] ?? '');
	if ($hostname === '') {
		return array('ok' => false, 'message' => 'Tenant has no hostname');
	}
	$industryCode = preg_replace('/[^a-z0-9_]/', '', (string) ($opts['industry_code'] ?? ($row['industry_code'] ?? 'auto_parts')));
	if ($industryCode === '') {
		$industryCode = 'auto_parts';
	}
	// Persist industry on registry when operator overrides.
	if (!empty($opts['industry_code']) && $industryCode !== (string) ($row['industry_code'] ?? '')) {
		$st = $pdo->prepare('UPDATE `epc_portal_tenants` SET `industry_code` = ?, `updated_at` = ? WHERE `site_key` = ?');
		$st->execute(array($industryCode, time(), $key));
		$row['industry_code'] = $industryCode;
	}

	$saveHost = $hostname;
	if (strpos($saveHost, 'www.') !== 0 && strpos($saveHost, '.') !== false) {
		$saveHost = 'www.' . preg_replace('/^www\./', '', $saveHost);
	}
	$settings = epc_portal_load_site_settings_for_host($pdo, $saveHost);
	if (!is_array($settings) || $settings === array()) {
		$settings = epc_portal_default_site_settings($hostname);
	}
	$settings['host'] = $hostname;
	$contact = isset($settings['contact']) && is_array($settings['contact']) ? $settings['contact'] : array();
	$erpOnly = $industryCode === 'erp_standalone' || (($settings['access_mode'] ?? '') === 'erp_only');
	$themeOpts = array(
		'erp_only' => $erpOnly,
		'skip_package' => $erpOnly,
		'force_package_tagline' => !empty($opts['force_package_tagline']),
	);
	if (!empty($opts['theme_template'])) {
		$themeOpts['theme_template'] = (string) $opts['theme_template'];
	}
	if (isset($opts['storefront_package']) && (string) $opts['storefront_package'] !== '') {
		$themeOpts['storefront_package'] = (string) $opts['storefront_package'];
	}
	$applied = epc_portal_apply_industry_theme_profile($settings, $contact, $industryCode, $themeOpts);
	$settings['contact'] = $contact;
	epc_portal_save_site_settings($pdo, $settings);

	$push = array('ok' => true, 'message' => 'skipped');
	$shouldPush = array_key_exists('push_client', $opts) ? !empty($opts['push_client']) : (($row['status'] ?? '') === 'live');
	if ($shouldPush && trim((string) ($row['db_name'] ?? '')) !== '' && function_exists('epc_portal_sync_tenant_packs_to_client_db')) {
		require_once __DIR__ . '/epc_portal_cp_menu.php';
		$push = epc_portal_sync_tenant_packs_to_client_db($pdo, $key);
	}

	$msg = $applied['message'];
	if (($push['message'] ?? '') !== 'skipped') {
		$msg .= ' · client sync: ' . (string) ($push['message'] ?? '');
	}
	return array(
		'ok' => true,
		'message' => $msg,
		'theme_template' => (string) ($applied['theme_template'] ?? ''),
		'storefront_package' => (string) ($applied['storefront_package'] ?? ''),
		'client_sync' => $push,
		'site_key' => $key,
	);
}

function epc_portal_onboard_client(PDO $pdo, array $post, string $submittedBy = ''): array
{
	require_once __DIR__ . '/epc_portal_db.php';
	epc_portal_db_ensure($pdo);

	$hostname = strtolower(trim((string) ($post['hostname'] ?? '')));
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($post['site_key'] ?? '')));
	$intro = epc_portal_intro_from_post($post);
	$erpOnlyShared = !empty($post['erp_only_shared']) || !empty($post['hosted_on_platform'])
		|| !empty($intro['erp_only_shared']);
	if ($erpOnlyShared) {
		$hostname = 'www.ecomae.com';
	}
	if ($siteKey === '' && $hostname !== '' && !$erpOnlyShared) {
		$siteKey = epc_portal_site_key_from_hostname($hostname);
	}

	$existing = $siteKey !== '' ? epc_portal_tenant_get($pdo, $siteKey) : null;
	if ($existing !== null) {
		$storedIntro = epc_portal_intro_decode((string) ($existing['intro_json'] ?? ''));
		$intro = epc_portal_intro_merge($storedIntro, $intro);
	}

	// Default dedicated MySQL for 1000+ readiness. Explicit scale_policy wins over checkbox absence.
	if (isset($post['scale_policy'])) {
		$dedicatedDb = strtolower(trim((string) $post['scale_policy'])) === 'dedicated_mysql';
	} elseif (array_key_exists('dedicated_db', $post)) {
		$dedicatedDb = !empty($post['dedicated_db']);
	} else {
		$dedicatedDb = !isset($intro['dedicated_db']) || !empty($intro['dedicated_db']);
	}
	if ($erpOnlyShared) {
		$dedicatedDb = true;
	}
	$scalePolicy = $dedicatedDb ? 'dedicated_mysql' : 'shared_docpart';
	$dbNameDefault = $dedicatedDb ? $siteKey : 'docpart';
	$dbUserDefault = $dedicatedDb ? $siteKey : 'docpart';

	$tenantData = array(
		'site_key' => $siteKey,
		'hostname' => $hostname,
		'industry_code' => !empty($intro['erp_only']) ? 'erp_standalone' : (string) ($post['industry_code'] ?? 'auto_parts'),
		'status' => (string) ($post['status'] ?? 'dns_pending'),
		'trade_name' => (string) ($post['trade_name'] ?? ''),
		'hub_name' => (string) ($post['hub_name'] ?? 'Electronic World Group'),
		'from_email' => (string) ($post['from_email'] ?? ($intro['contact_email'] ?? '')),
		'db_name' => (string) ($post['db_name'] ?? $dbNameDefault),
		'db_user' => (string) ($post['db_user'] ?? $dbUserDefault),
		'db_password' => (string) ($post['db_password'] ?? ''),
		'notes' => (string) ($post['notes'] ?? ''),
		'hosted_on' => $erpOnlyShared ? 'platform' : 'client',
		'erp_only_shared' => $erpOnlyShared ? 1 : 0,
		'dedicated_db' => $dedicatedDb ? 1 : 0,
		'scale_policy' => $scalePolicy,
		'blockchain_mode' => isset($post['blockchain_mode'])
			? (string) $post['blockchain_mode']
			: (string) ($intro['blockchain_mode'] ?? 'anchor'),
	);

	$errors = epc_portal_intro_validate($intro, $tenantData);
	if (count($errors) > 0) {
		return array('ok' => false, 'message' => implode('. ', $errors), 'errors' => $errors);
	}

	$intro['submitted_at'] = time();
	if ($submittedBy !== '') {
		$intro['submitted_by'] = $submittedBy;
	}

	$result = epc_portal_save_tenant($pdo, $tenantData);
	if (empty($result['ok'])) {
		return $result;
	}

	$st = $pdo->prepare('UPDATE `epc_portal_tenants` SET `intro_json` = ? WHERE `site_key` = ?');
	$st->execute(array(json_encode($intro, JSON_UNESCAPED_UNICODE), $siteKey));

	$row = epc_portal_tenant_get($pdo, $siteKey);
	if ($row !== null) {
		epc_portal_apply_intro_to_site_settings($pdo, $hostname, $row, $intro);
	}

	$countryProfile = array('ok' => true, 'message' => 'skipped');
	$cc = strtoupper(substr((string) ($intro['country_code'] ?? ''), 0, 2));
	if ($cc === '' && !empty($intro['country'])) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
		$cc = epc_countries_normalize_code((string) $intro['country']);
	}
	if ($cc !== '' && $siteKey !== '') {
		$profileFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_country_profile.php';
		if (is_readable($profileFile)) {
			require_once $profileFile;
			$countryProfile = epc_tenant_apply_country_profile($siteKey, $cc, $pdo);
		}
	}

	$sync = array('ok' => true, 'message' => 'skipped');
	if ($row !== null && trim((string) ($row['db_name'] ?? '')) !== '' && ($row['status'] ?? '') === 'live'
		&& function_exists('epc_portal_sync_tenant_packs_to_client_db')) {
		require_once __DIR__ . '/epc_portal_cp_menu.php';
		$sync = epc_portal_sync_tenant_packs_to_client_db($pdo, $siteKey);
	}

	$checklist = epc_portal_tenant_launch_checklist($pdo, $siteKey);
	$actionUrls = array('storefront' => '', 'cp' => '', 'erp' => '');
	if ($row !== null) {
		$hubHelpers = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php';
		if (is_readable($hubHelpers)) {
			require_once $hubHelpers;
			if (function_exists('epc_th_tenant_action_urls')) {
				$actionUrls = epc_th_tenant_action_urls($row);
			}
		}
	}

	$msg = 'Client onboarded — tenant registered and portal settings seeded. Complete DNS + set Live when ready.';
	if ($erpOnlyShared) {
		$msg = 'Shared ERP company registered on www.ecomae.com — CP users and MySQL database provisioned when CloudPanel is configured.';
	} elseif ($dedicatedDb) {
		$msg = 'Client onboarded with dedicated MySQL (scale-ready). Complete DNS + set Live when ready.';
	}
	if (($row['status'] ?? '') === 'live' && !empty($sync['ok']) && ($sync['message'] ?? '') !== 'skipped') {
		$msg .= ' ' . $sync['message'];
	}

	// Queue async warmup so first login is not cold (best-effort; no-op if queue unavailable).
	$warmupJobId = 0;
	if ($dedicatedDb && $siteKey !== '') {
		try {
			require_once __DIR__ . '/epc_platform_jobs.php';
			$warmupJobId = epc_platform_jobs_enqueue(
				'tenant_warmup_pdo',
				$siteKey,
				array('reason' => 'onboard'),
				array('priority' => 50, 'dedupe' => true, 'delay_sec' => 5)
			);
		} catch (Throwable $e) {
			$warmupJobId = 0;
		}
	}

	return array(
		'ok' => true,
		'message' => $msg,
		'site_key' => $siteKey,
		'hostname' => $hostname,
		'country_code' => $cc !== '' ? $cc : 'AE',
		'country_profile' => $countryProfile,
		'dedicated_db' => $dedicatedDb ? 1 : 0,
		'scale_policy' => $scalePolicy,
		'warmup_job_id' => $warmupJobId,
		'cp_url' => $erpOnlyShared ? (string) ($actionUrls['erp_login'] ?? $actionUrls['erp'] ?? '') : (string) ($actionUrls['cp'] ?? ''),
		'erp_url' => (string) ($actionUrls['erp'] ?? ''),
		'checklist' => $checklist,
	);
}

function epc_portal_tenant_launch_checklist(PDO $pdo, string $siteKey): array
{
	require_once __DIR__ . '/epc_portal_db.php';
	$row = epc_portal_tenant_get($pdo, $siteKey);
	if ($row === null) {
		return array();
	}
	$intro = epc_portal_intro_decode((string) ($row['intro_json'] ?? ''));
	$hostname = (string) $row['hostname'];
	$status = (string) $row['status'];
	$sharedErp = function_exists('epc_portal_tenant_is_shared_erp_row') && epc_portal_tenant_is_shared_erp_row($row);
	$loginHost = $sharedErp ? 'www.ecomae.com' : $hostname;

	$items = array(
		array(
			'id' => 'intro',
			'label' => 'Client intro form submitted',
			'done' => !empty($intro['submitted_at']),
			'hint' => !empty($intro['submitted_at']) ? date('Y-m-d H:i', (int) $intro['submitted_at']) : 'Fill the onboard form',
		),
		array(
			'id' => 'tenant',
			'label' => 'Tenant registered in platform DB',
			'done' => true,
			'hint' => $hostname,
		),
		array(
			'id' => 'db',
			'label' => 'Tenant MySQL database configured',
			'done' => trim((string) ($row['db_name'] ?? '')) !== '',
			'hint' => trim((string) ($row['db_name'] ?? '')) !== '' ? (string) $row['db_name'] : 'Create DB + enter credentials',
		),
		array(
			'id' => 'settings',
			'label' => 'Site settings & contact seeded',
			'done' => !empty($intro['submitted_at']),
			'hint' => 'Branding, from-email, admin email',
		),
		array(
			'id' => 'dns',
			'label' => 'GoDaddy DNS → platform IP',
			'done' => in_array($status, array('dns_pending', 'live'), true),
			'hint' => 'A record @ and www → ' . epc_portal_platform_ip(),
		),
		array(
			'id' => 'alias',
			'label' => 'CloudPanel domain alias on www.ecomae.com',
			'done' => false,
			'hint' => 'Add ' . $hostname . ' as alias (manual)',
		),
		array(
			'id' => 'ssl',
			'label' => 'SSL certificate issued',
			'done' => false,
			'hint' => "Let's Encrypt for alias",
		),
		array(
			'id' => 'live',
			'label' => 'Tenant status = Live',
			'done' => $status === 'live',
			'hint' => $status === 'live' ? 'Live' : 'Set status to Live in Tenants tab',
		),
		array(
			'id' => 'cp',
			'label' => 'Client CP accessible',
			'done' => $status === 'live',
			'hint' => 'https://' . $hostname . '/cp/',
		),
	);

	if ($sharedErp) {
		foreach ($items as &$item) {
			if (in_array($item['id'], array('dns', 'alias', 'ssl'), true)) {
				$item['label'] = str_replace('GoDaddy DNS', 'Client DNS', $item['label']);
				$item['done'] = true;
				$item['hint'] = 'Not required — shared ERP on www.ecomae.com (no client domain)';
			}
			if ($item['id'] === 'cp') {
				$item['label'] = 'Client ERP login URL ready';
				$sk = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
				$item['hint'] = ($sk !== '' && function_exists('epc_client_erp_login_url'))
					? ('https://www.ecomae.com' . epc_client_erp_login_url($sk))
					: 'https://www.ecomae.com/cp/client-erp/{site_key}/';
			}
		}
		unset($item);
	}

	$settings = epc_portal_load_site_settings_for_host($pdo, $hostname);
	if (function_exists('epc_portal_resolve_access_mode') && epc_portal_resolve_access_mode($settings) === 'erp_only') {
		$mods = function_exists('epc_portal_erp_modules_enabled')
			? epc_portal_erp_modules_enabled($settings)
			: array();
		$items[] = array(
			'id' => 'erp_modules',
			'label' => 'ERP modules configured (' . count($mods) . ' areas)',
			'done' => count($mods) > 0,
			'hint' => implode(', ', $mods),
		);
		$sk = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
		$shellHint = ($sharedErp && $sk !== '' && function_exists('epc_client_erp_shell_url'))
			? ('https://www.ecomae.com' . epc_client_erp_shell_url($sk))
			: ('https://' . $loginHost . '/cp/shop/finance/erp?epc_erp_shell=1');
		$items[] = array(
			'id' => 'erp_shell',
			'label' => 'ERP shell login URL ready',
			'done' => $status === 'live',
			'hint' => $shellHint,
		);
		$items[] = array(
			'id' => 'erp_users',
			'label' => 'Staff users created (CP → Users)',
			'done' => false,
			'hint' => 'Create username/password accounts; assign ERP department in Staff tab',
		);
		$items[] = array(
			'id' => 'multi_entity',
			'label' => 'Multi-company enabled (optional)',
			'done' => false,
			'hint' => 'ERP → Multi-entity tab — one tenant, many legal entities',
		);
	}

	$done = 0;
	foreach ($items as $item) {
		if (!empty($item['done'])) {
			$done++;
		}
	}

	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
	$clientErpLogin = ($sharedErp && $siteKey !== '' && function_exists('epc_client_erp_login_url'))
		? ('https://www.ecomae.com' . epc_client_erp_login_url($siteKey))
		: '';
	$clientErpShell = ($sharedErp && $siteKey !== '' && function_exists('epc_client_erp_shell_url'))
		? ('https://www.ecomae.com' . epc_client_erp_shell_url($siteKey))
		: '';

	return array(
		'items' => $items,
		'done' => $done,
		'total' => count($items),
		'ready' => $status === 'live',
		'hostname' => $hostname,
		'cp_url' => $sharedErp ? $clientErpLogin : ('https://' . $loginHost . '/cp/'),
		'erp_url' => $sharedErp ? $clientErpShell : ('https://' . $loginHost . '/cp/shop/finance/erp?epc_erp_shell=1'),
		'erp_only_shared' => $sharedErp,
	);
}

function epc_portal_erp_only_onboard_steps(): array
{
	return array(
		array(
			'title' => 'Choose shared ERP on ecomae.com',
			'body' => 'On <strong>Onboard client</strong>, tick <strong>ERP only</strong> and <strong>Hosted on ecomae.com (shared)</strong>. Enter company display name and site key (e.g. <code>asap</code>) — <strong>no client domain</strong>. Hostname is always <code>www.ecomae.com</code>.',
		),
		array(
			'title' => 'Choose access mode & ERP modules',
			'body' => 'Set <code>access_mode=erp_only</code> and tick the ERP module grid — presets include Full ERP, Custom &amp; Shipping only, People only, Finance + e-invoice. Settings sync to the company MySQL DB on Live.',
		),
		array(
			'title' => 'Create tenant database & ERP users',
			'body' => 'Provision one MySQL database per company on the platform VPS (<code>asapc</code>, <code>company2</code>, …). Use Tenant hub onboard with <strong>Hosted on ecomae.com (shared)</strong> — the provision flow creates DB credentials automatically when CloudPanel is configured.',
		),
		array(
			'title' => 'Add company 2, 3 on same host',
			'body' => 'Repeat onboard with a new site key and separate MySQL DB. Each company gets its own login at <code>/cp/client-erp/{site_key}/</code>. If the same email exists in two companies, the login form shows a company picker.',
		),
		array(
			'title' => 'Hand off login URL',
			'body' => 'Share <code>https://www.ecomae.com/cp/client-erp/{site_key}/</code> (e.g. <code>…/asapcustom/</code>). After login, users land in the ERP shell for their company only — <strong>no Super CP</strong>, no storefront, no client DNS.',
		),
	);
}

function epc_portal_onboard_guide_steps(): array
{
	$ip = epc_portal_platform_ip();
	return array(
		array(
			'title' => 'Collect client intro',
			'body' => 'Use the <strong>Onboard client</strong> tab — one form captures brand, domain, contacts, admin email, and DB credentials. Submitting registers the tenant and seeds portal settings immediately, including <code>contact.use_animated_hub_logo</code> so the client storefront header shows the ECOM AE animated hub beside their trade name.',
		),
		array(
			'title' => 'Create tenant database',
			'body' => 'In CloudPanel / MySQL, create one database per client on the same VPS. Enter db name, user, and password in the intro form (or Tenants tab). Import industry seed if needed (e.g. docpart clone for auto parts).',
		),
		array(
			'title' => 'GoDaddy DNS',
			'body' => 'Client keeps the domain at GoDaddy. Add A records <code>@</code> and <code>www</code> → <code>' . $ip . '</code>. Remove old A records. Wait 5–60 minutes.',
		),
		array(
			'title' => 'CloudPanel alias (no extra site)',
			'body' => 'Add <code>www.client.com</code> as a <strong>domain alias</strong> on the existing <code>www.ecomae.com</code> site — same docroot, zero extra disk. Do not create a separate CloudPanel site per client.',
		),
		array(
			'title' => 'SSL + go Live',
			'body' => 'Issue Let\'s Encrypt for the alias. In Tenant hub → Tenants, set status to <strong>Live</strong>. The app routes by hostname to the tenant DB automatically.',
		),
		array(
			'title' => 'Hand off client CP',
			'body' => 'Client control panel: <code>https://www.client.com/cp/</code>. Platform operator hub stays at <code>https://cp.ecomae.com/cp/</code>. Share admin credentials separately.',
		),
	);
}
