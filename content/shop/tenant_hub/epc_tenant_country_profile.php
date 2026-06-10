<?php
/**
 * Tenant country profile — tax toolkit, ERP, Auto Price AI, portal registry.
 * Call on onboard, demo provision, and country change.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';

function epc_tenant_country_normalize(string $value): string
{
	$value = strtoupper(trim($value));
	if (preg_match('/^[A-Z]{2}$/', $value)) {
		return $value;
	}
	if (function_exists('epc_tax_toolkit_country_name_to_iso')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_tax_toolkit.php';
		$iso = epc_tax_toolkit_country_name_to_iso($value);
		if ($iso !== '') {
			return strtoupper($iso);
		}
	}
	$countries = epc_countries_iso3166_alpha2();
	foreach ($countries as $code => $name) {
		if (strcasecmp($name, $value) === 0) {
			return $code;
		}
	}
	return '';
}

/** @return array<string, string> ERP / locale defaults per ISO2 */
function epc_tenant_country_erp_defaults(string $countryCode): array
{
	$meta = function_exists('epc_apai_country_meta')
		? epc_apai_country_meta($countryCode)
		: array('currency' => 'AED');
	$currency = (string) ($meta['currency'] ?? 'AED');
	$dateFormat = 'd/m/Y';
	if (in_array($countryCode, array('US'), true)) {
		$dateFormat = 'm/d/Y';
	} elseif (in_array($countryCode, array('GB', 'PK', 'IN'), true)) {
		$dateFormat = 'd/m/Y';
	}
	return array(
		'company_country_code' => $countryCode,
		'company_currency' => $currency,
		'erp_date_format' => $dateFormat,
	);
}

function epc_tenant_country_pricing_set(PDO $pdo, string $key, string $value): void
{
	try {
		$pdo->prepare(
			'INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?)
			 ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)'
		)->execute(array($key, $value));
	} catch (Throwable $e) {
	}
}

function epc_tenant_country_tenant_pdo(array $tenantRow): ?PDO
{
	$dbName = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($tenantRow['db_name'] ?? '')));
	if ($dbName === '') {
		return null;
	}
	$user = (string) ($tenantRow['db_user'] ?? $dbName);
	$pass = (string) ($tenantRow['db_password'] ?? '');
	if ($pass === '' && function_exists('epc_portal_resolve_tenant_db_credentials')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		$cred = epc_portal_resolve_tenant_db_credentials();
		if (!empty($cred['db']) && $cred['db'] === $dbName) {
			$user = (string) ($cred['user'] ?? $user);
			$pass = (string) ($cred['pass'] ?? $pass);
		}
	}
	if ($pass === '') {
		return null;
	}
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$cfg = new DP_Config();
		return new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $dbName . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 5)
		);
	} catch (Throwable $e) {
		return null;
	}
}

/**
 * Apply country to platform registry, site settings, tenant DB (tax, ERP, APAI).
 *
 * @return array{ok:bool,country_code:string,country_name:string,steps:array<string,string>,errors:array<int,string>}
 */
function epc_tenant_apply_country_profile(string $siteKey, string $countryCode, ?PDO $platformPdo = null): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$countryCode = epc_tenant_country_normalize($countryCode);
	$countries = epc_countries_iso3166_alpha2();
	if ($siteKey === '' || $countryCode === '' || !isset($countries[$countryCode])) {
		return array(
			'ok' => false,
			'country_code' => $countryCode,
			'country_name' => '',
			'steps' => array(),
			'errors' => array('Invalid site key or country code'),
		);
	}
	$countryName = $countries[$countryCode];
	$steps = array();
	$errors = array();

	if ($platformPdo === null && function_exists('epc_portal_platform_pdo')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		$platformPdo = epc_portal_platform_pdo();
	}
	if (!$platformPdo instanceof PDO) {
		return array(
			'ok' => false,
			'country_code' => $countryCode,
			'country_name' => $countryName,
			'steps' => array(),
			'errors' => array('Platform database unavailable'),
		);
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	epc_portal_db_ensure($platformPdo);

	try {
		$platformPdo->prepare('UPDATE `epc_portal_tenants` SET `country_code` = ?, `updated_at` = ? WHERE `site_key` = ?')
			->execute(array($countryCode, time(), $siteKey));
		$steps['registry'] = $countryCode;
	} catch (Throwable $e) {
		$errors[] = 'registry: ' . $e->getMessage();
	}

	$tenantRow = null;
	if (function_exists('epc_portal_tenant_get')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';
		$tenantRow = epc_portal_tenant_get($platformPdo, $siteKey);
	}
	$hostname = is_array($tenantRow) ? (string) ($tenantRow['hostname'] ?? '') : '';

	if ($hostname !== '') {
		try {
			$settings = epc_portal_load_site_settings_for_host($platformPdo, $hostname);
			$settings['host'] = $hostname;
			$settings['country_code'] = $countryCode;
			$contact = is_array($settings['contact'] ?? null) ? $settings['contact'] : array();
			$contact['country'] = $countryName;
			$contact['country_code'] = $countryCode;
			$settings['contact'] = $contact;
			epc_portal_save_site_settings($platformPdo, $settings);
			$steps['platform_site_settings'] = $hostname;
		} catch (Throwable $e) {
			$errors[] = 'platform_site_settings: ' . $e->getMessage();
		}
	}

	$tenantPdo = is_array($tenantRow) ? epc_tenant_country_tenant_pdo($tenantRow) : null;
	if ($tenantPdo instanceof PDO && $hostname !== '') {
		try {
			$tSettings = epc_portal_load_site_settings_for_host($tenantPdo, $hostname);
			$tSettings['host'] = (string) ($tSettings['host'] ?? $hostname);
			$tSettings['country_code'] = $countryCode;
			$tContact = is_array($tSettings['contact'] ?? null) ? $tSettings['contact'] : array();
			$tContact['country'] = $countryName;
			$tContact['country_code'] = $countryCode;
			$tSettings['contact'] = $tContact;
			epc_portal_save_site_settings($tenantPdo, $tSettings);
			$steps['tenant_site_settings'] = (string) ($tenantRow['db_name'] ?? '');
		} catch (Throwable $e) {
			$errors[] = 'tenant_site_settings: ' . $e->getMessage();
		}
	}

	if ($tenantPdo instanceof PDO) {
		$taxFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_tax_toolkit.php';
		if (is_readable($taxFile)) {
			require_once $taxFile;
			try {
				epc_tax_toolkit_assign_tenant($tenantPdo, $countryCode, '', '', $siteKey, false);
				$steps['tax_toolkit'] = epc_tax_toolkit_country_to_kit_code($countryCode);
			} catch (Throwable $e) {
				$errors[] = 'tax_toolkit: ' . $e->getMessage();
			}
		}

		$erp = epc_tenant_country_erp_defaults($countryCode);
		foreach ($erp as $k => $v) {
			epc_tenant_country_pricing_set($tenantPdo, $k, $v);
		}
		$steps['erp'] = $erp['company_currency'];

		$apaiFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_country_sources.php';
		if (is_readable($apaiFile)) {
			require_once $apaiFile;
			try {
				$added = epc_apai_install_country_sources($tenantPdo, $siteKey);
				$steps['apai_sources'] = (string) $added;
			} catch (Throwable $e) {
				$errors[] = 'apai_sources: ' . $e->getMessage();
			}
		}
	} elseif (is_array($tenantRow) && trim((string) ($tenantRow['db_name'] ?? '')) !== '') {
		$errors[] = 'tenant_db: connect failed';
	}

	return array(
		'ok' => count($errors) === 0,
		'country_code' => $countryCode,
		'country_name' => $countryName,
		'steps' => $steps,
		'errors' => $errors,
	);
}

/** Resolve tenant market label for CP UI. */
function epc_tenant_country_market_label(?PDO $pdo = null, string $siteKey = ''): string
{
	if ($siteKey === '' && function_exists('epc_tax_toolkit_site_key_for_db') && $pdo instanceof PDO) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_tax_toolkit.php';
		$siteKey = epc_tax_toolkit_site_key_for_db($pdo);
	}
	if (function_exists('epc_apai_tenant_country') && function_exists('epc_apai_country_meta')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_country_sources.php';
		$cc = epc_apai_tenant_country($siteKey, $pdo);
		return (string) (epc_apai_country_meta($cc)['label'] ?? $cc);
	}
	return 'United Arab Emirates';
}
