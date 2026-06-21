<?php
/**
 * Auto Price AI — tenant country resolver + per-country discovery source packs.
 */
defined('_ASTEXE_') or die('No access');

/**
 * ISO 3166-1 alpha-2 from tax toolkit profile, ERP company_country_code, portal/industry settings.
 * Fallback AE.
 */
function epc_apai_tenant_country(string $siteKey, ?PDO $pdo = null): string
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$cc = '';

	if ($pdo instanceof PDO && is_file(__DIR__ . '/../finance/epc_tax_toolkit.php')) {
		require_once __DIR__ . '/../finance/epc_tax_toolkit.php';
		if (function_exists('epc_tax_toolkit_detect_tenant_country')) {
			$cc = strtoupper(trim((string) epc_tax_toolkit_detect_tenant_country($pdo, $siteKey)));
		}
	}

	if ($cc === '' && $pdo instanceof PDO) {
		$cc = epc_apai_tenant_country_from_registry($pdo, $siteKey);
	}

	if ($cc === '' && $pdo instanceof PDO && is_file(__DIR__ . '/../pricing/epc_pricing.php')) {
		require_once __DIR__ . '/../pricing/epc_pricing.php';
		if (function_exists('epc_pricing_get_setting')) {
			$erp = strtoupper(trim((string) epc_pricing_get_setting($pdo, 'company_country_code', '')));
			if ($erp === 'UAE') {
				$erp = 'AE';
			}
			if (strlen($erp) >= 2) {
				$cc = $erp;
			}
		}
	}

	if ($cc === '' && $siteKey !== '' && function_exists('epc_tax_toolkit_known_tenant_countries')) {
		$known = epc_tax_toolkit_known_tenant_countries();
		if (isset($known[$siteKey])) {
			$cc = strtoupper((string) $known[$siteKey]);
		}
	}

	$cc = strtoupper(preg_replace('/[^A-Z]/', '', $cc));
	return strlen($cc) >= 2 ? substr($cc, 0, 2) : 'AE';
}

function epc_apai_tenant_country_from_registry(PDO $pdo, string $siteKey): string
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($siteKey === '') {
		return '';
	}
	try {
		if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		}
		if (function_exists('epc_portal_tenant_registry_row')) {
			$row = epc_portal_tenant_registry_row($pdo, $siteKey);
			if (is_array($row)) {
				foreach (array('country_code', 'country', 'market_country') as $k) {
					if (!empty($row[$k])) {
						$v = strtoupper(trim((string) $row[$k]));
						if (strlen($v) === 2) {
							return $v;
						}
					}
				}
			}
		}
	} catch (Throwable $e) {
	}
	try {
		$st = $pdo->prepare('SELECT `settings_json` FROM `epc_portal_industry_settings` WHERE `site_key` = ? LIMIT 1');
		$st->execute(array($siteKey));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row && !empty($row['settings_json'])) {
			$settings = json_decode((string) $row['settings_json'], true);
			if (is_array($settings)) {
				$contact = (array) ($settings['contact'] ?? array());
				if (!empty($contact['country_code'])) {
					return strtoupper(substr((string) $contact['country_code'], 0, 2));
				}
				if (!empty($contact['country']) && function_exists('epc_tax_toolkit_country_name_to_iso')) {
					require_once __DIR__ . '/../finance/epc_tax_toolkit.php';
					$iso = epc_tax_toolkit_country_name_to_iso((string) $contact['country']);
					if ($iso !== '') {
						return strtoupper($iso);
					}
				}
			}
		}
	} catch (Throwable $e) {
	}
	return '';
}

/**
 * Normalize a domain or URL to bare hostname (no scheme, path, or leading www.).
 */
function epc_apai_normalize_domain(string $domain): string
{
	$domain = strtolower(trim($domain));
	$domain = preg_replace('#^https?://#', '', $domain);
	$domain = preg_replace('#/.*$#', '', $domain);
	$domain = preg_replace('/^www\./', '', $domain);
	return preg_replace('/[^a-z0-9.\-]/', '', $domain);
}

/**
 * Hostnames belonging to this tenant's own storefront — never used as external discovery sources.
 *
 * @return array<int,string>
 */
function epc_apai_tenant_own_domains(string $siteKey, ?PDO $pdo = null): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($siteKey === '' || $siteKey === 'platform') {
		return array();
	}

	$hosts = array();
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		if (function_exists('epc_portal_tenant_templates')) {
			$templates = epc_portal_tenant_templates();
			if (!empty($templates[$siteKey]['hostname'])) {
				$hosts[] = (string) $templates[$siteKey]['hostname'];
			}
		}
	}

	$platformPdo = null;
	if ($pdo instanceof PDO) {
		$platformPdo = $pdo;
	}
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		if (function_exists('epc_portal_platform_pdo')) {
			$pp = epc_portal_platform_pdo();
			if ($pp instanceof PDO) {
				$platformPdo = $pp;
			}
		}
	}
	if ($platformPdo instanceof PDO && is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';
		if (function_exists('epc_portal_tenant_get')) {
			$row = epc_portal_tenant_get($platformPdo, $siteKey);
			if (is_array($row) && !empty($row['hostname'])) {
				$hosts[] = (string) $row['hostname'];
			}
		}
	}

	$domains = array();
	foreach ($hosts as $host) {
		$host = strtolower(trim($host));
		if ($host === '') {
			continue;
		}
		if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname($host)) {
			continue;
		}
		$bare = epc_apai_normalize_domain($host);
		if ($bare === '') {
			continue;
		}
		$domains[$bare] = true;
		$domains['www.' . $bare] = true;
	}
	return array_keys($domains);
}

function epc_apai_is_tenant_own_domain(string $siteKey, string $domain, ?PDO $pdo = null): bool
{
	$domain = epc_apai_normalize_domain($domain);
	if ($domain === '') {
		return false;
	}
	foreach (epc_apai_tenant_own_domains($siteKey, $pdo) as $own) {
		$ownBare = epc_apai_normalize_domain($own);
		if ($ownBare === '') {
			continue;
		}
		if ($domain === $ownBare || $domain === 'www.' . $ownBare) {
			return true;
		}
		if (strlen($domain) > strlen($ownBare) && substr($domain, -strlen($ownBare) - 1) === '.' . $ownBare) {
			return true;
		}
	}
	return false;
}

/**
 * Remove discovery sources that point at the tenant's own storefront domain.
 */
function epc_apai_purge_own_domain_sources(PDO $pdo, string $siteKey): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($siteKey === '') {
		return 0;
	}
	$stmt = $pdo->prepare('SELECT `id`, `domain` FROM `epc_discovery_sources` WHERE `site_key` = ?');
	$stmt->execute(array($siteKey));
	$purged = 0;
	foreach ($stmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $row) {
		if (!epc_apai_is_tenant_own_domain($siteKey, (string) ($row['domain'] ?? ''), $pdo)) {
			continue;
		}
		$pdo->prepare('DELETE FROM `epc_discovery_sources` WHERE `id` = ?')->execute(array((int) $row['id']));
		$purged++;
	}
	return $purged;
}

/**
 * @return array{code:string,label:string,tld:string,currency:string,search_gl:string}
 */
function epc_apai_country_meta(string $countryCode): array
{
	$code = strtoupper(substr(preg_replace('/[^A-Za-z]/', '', $countryCode), 0, 2));
	$map = array(
		'AE' => array('label' => 'United Arab Emirates', 'tld' => 'ae', 'currency' => 'AED', 'search_gl' => 'ae'),
		'PK' => array('label' => 'Pakistan', 'tld' => 'pk', 'currency' => 'PKR', 'search_gl' => 'pk'),
		'IN' => array('label' => 'India', 'tld' => 'in', 'currency' => 'INR', 'search_gl' => 'in'),
		'SA' => array('label' => 'Saudi Arabia', 'tld' => 'sa', 'currency' => 'SAR', 'search_gl' => 'sa'),
		'OM' => array('label' => 'Oman', 'tld' => 'om', 'currency' => 'OMR', 'search_gl' => 'om'),
		'GB' => array('label' => 'United Kingdom', 'tld' => 'co.uk', 'currency' => 'GBP', 'search_gl' => 'uk'),
		'US' => array('label' => 'United States', 'tld' => 'com', 'currency' => 'USD', 'search_gl' => 'us'),
	);
	$row = $map[$code] ?? array('label' => $code, 'tld' => strtolower($code), 'currency' => 'USD', 'search_gl' => strtolower($code));
	return array(
		'code' => $code !== '' ? $code : 'AE',
		'label' => (string) ($row['label'] ?? 'United Arab Emirates'),
		'tld' => (string) ($row['tld'] ?? 'ae'),
		'currency' => (string) ($row['currency'] ?? 'AED'),
		'search_gl' => (string) ($row['search_gl'] ?? 'ae'),
	);
}

/**
 * Base discovery domains per country (before industry overlay).
 *
 * @return array<int,array{domain:string,label:string,priority:int}>
 */
function epc_apai_country_source_pack(string $countryCode): array
{
	$meta = epc_apai_country_meta($countryCode);
	$code = $meta['code'];
	$packs = array(
		'AE' => array(
			array('domain' => 'sharafdg.com', 'label' => 'Sharaf DG UAE', 'priority' => 10),
			array('domain' => 'jumbo.ae', 'label' => 'Jumbo Electronics', 'priority' => 15),
			array('domain' => 'noon.com', 'label' => 'Noon UAE', 'priority' => 20),
			array('domain' => 'amazon.ae', 'label' => 'Amazon.ae', 'priority' => 25),
			array('domain' => 'carrefouruae.com', 'label' => 'Carrefour UAE', 'priority' => 30),
		),
		'PK' => array(
			array('domain' => 'daraz.pk', 'label' => 'Daraz Pakistan', 'priority' => 10),
			array('domain' => 'telemart.pk', 'label' => 'Telemart', 'priority' => 12),
			array('domain' => 'homeshopping.pk', 'label' => 'HomeShopping.pk', 'priority' => 14),
			array('domain' => 'shophive.com', 'label' => 'Shophive', 'priority' => 16),
			array('domain' => 'priceoye.pk', 'label' => 'PriceOye', 'priority' => 18),
			array('domain' => 'metro-online.pk', 'label' => 'Metro Online', 'priority' => 20),
			array('domain' => 'pakwheels.com', 'label' => 'PakWheels', 'priority' => 22),
			array('domain' => 'autostore.pk', 'label' => 'AutoStore Pakistan', 'priority' => 24),
			array('domain' => 'amazon.com', 'label' => 'Amazon (global)', 'priority' => 26),
			array('domain' => 'ebay.com', 'label' => 'eBay (global)', 'priority' => 28),
		),
		'IN' => array(
			array('domain' => 'flipkart.com', 'label' => 'Flipkart', 'priority' => 10),
			array('domain' => 'amazon.in', 'label' => 'Amazon India', 'priority' => 12),
			array('domain' => 'croma.com', 'label' => 'Croma', 'priority' => 14),
			array('domain' => 'reliancedigital.in', 'label' => 'Reliance Digital', 'priority' => 16),
			array('domain' => 'vijaysales.com', 'label' => 'Vijay Sales', 'priority' => 18),
			array('domain' => 'myntra.com', 'label' => 'Myntra', 'priority' => 20),
			array('domain' => 'ajio.com', 'label' => 'AJIO', 'priority' => 22),
			array('domain' => 'boodmo.com', 'label' => 'Boodmo India', 'priority' => 24),
			array('domain' => 'amazon.com', 'label' => 'Amazon (global)', 'priority' => 26),
			array('domain' => 'ebay.com', 'label' => 'eBay (global)', 'priority' => 28),
		),
		'SA' => array(
			array('domain' => 'extra.com', 'label' => 'Extra Stores KSA', 'priority' => 10),
			array('domain' => 'amazon.sa', 'label' => 'Amazon.sa', 'priority' => 15),
			array('domain' => 'noon.com', 'label' => 'Noon KSA', 'priority' => 20),
		),
		'OM' => array(
			array('domain' => 'luluhypermarket.com', 'label' => 'Lulu Hypermarket Oman', 'priority' => 10),
			array('domain' => 'carrefouroman.com', 'label' => 'Carrefour Oman', 'priority' => 12),
			array('domain' => 'extra.com', 'label' => 'Extra Stores Oman', 'priority' => 14),
			array('domain' => 'alizzislamic.om', 'label' => 'Alizz Islamic Bank marketplace', 'priority' => 18),
			array('domain' => 'omantel.om', 'label' => 'Omantel Shop', 'priority' => 20),
			array('domain' => 'autopartsoman.com', 'label' => 'Auto Parts Oman', 'priority' => 22),
			array('domain' => 'partsouq.com', 'label' => 'Partsouq (regional)', 'priority' => 24),
			array('domain' => 'amazon.ae', 'label' => 'Amazon (GCC)', 'priority' => 26),
			array('domain' => 'noon.com', 'label' => 'Noon (GCC)', 'priority' => 28),
			array('domain' => 'ebay.com', 'label' => 'eBay (global)', 'priority' => 30),
		),
		'GB' => array(
			array('domain' => 'amazon.co.uk', 'label' => 'Amazon UK', 'priority' => 10),
			array('domain' => 'currys.co.uk', 'label' => 'Currys', 'priority' => 15),
		),
		'US' => array(
			array('domain' => 'amazon.com', 'label' => 'Amazon US', 'priority' => 10),
			array('domain' => 'bestbuy.com', 'label' => 'Best Buy', 'priority' => 15),
		),
	);
	return $packs[$code] ?? $packs['AE'];
}

/**
 * Country pack merged with industry-specific domains (AE legacy packs preserved).
 *
 * @return array<int,array{domain:string,label:string,priority:int}>
 */
function epc_apai_country_sources_for_tenant(PDO $pdo, string $siteKey, string $industryKey = ''): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($industryKey === '' && function_exists('epc_apai_resolve_industry')) {
		require_once __DIR__ . '/epc_industry_taxonomy.php';
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	}
	$country = epc_apai_tenant_country($siteKey, $pdo);
	$base = epc_apai_country_source_pack($country);

	require_once __DIR__ . '/epc_industry_taxonomy.php';
	$industry = function_exists('epc_apai_country_industry_sources')
		? epc_apai_country_industry_sources($country, $industryKey)
		: (function_exists('epc_apai_ae_sources_for_industry') && $country === 'AE' ? epc_apai_ae_sources_for_industry($industryKey) : array());

	$merged = array();
	$seen = array();
	foreach (array_merge($industry, $base) as $src) {
		$d = strtolower((string) ($src['domain'] ?? ''));
		if ($d === '' || isset($seen[$d])) {
			continue;
		}
		if (epc_apai_is_tenant_own_domain($siteKey, $d, $pdo)) {
			continue;
		}
		$seen[$d] = true;
		$merged[] = $src;
	}
	usort($merged, function ($a, $b) {
		return ((int) ($a['priority'] ?? 100)) <=> ((int) ($b['priority'] ?? 100));
	});
	return $merged;
}

/**
 * Update pack metadata on an existing platform source (label, priority, auth hint).
 */
function epc_apai_country_source_sync_meta(PDO $pdo, string $siteKey, string $domain, array $src): void
{
	$domain = strtolower(trim($domain));
	if ($domain === '') {
		return;
	}
	$stmt = $pdo->prepare('SELECT `id`, `label`, `priority`, `auth_type` FROM `epc_discovery_sources` WHERE `site_key` = ? AND `domain` = ? LIMIT 1');
	$stmt->execute(array($siteKey, $domain));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return;
	}
	$label = (string) ($src['label'] ?? $row['label']);
	$priority = (int) ($src['priority'] ?? $row['priority']);
	$authType = strtolower(trim((string) ($src['auth_type'] ?? $row['auth_type'] ?? 'none')));
	if ($authType === '') {
		$authType = 'none';
	}
	if ($label === (string) $row['label'] && $priority === (int) $row['priority'] && $authType === (string) ($row['auth_type'] ?? 'none')) {
		return;
	}
	$pdo->prepare(
		'UPDATE `epc_discovery_sources` SET `label`=?, `priority`=?, `auth_type`=?, `updated_at`=? WHERE `id`=?'
	)->execute(array($label, $priority, $authType, time(), (int) $row['id']));
}

/**
 * Install country pack + search engine row into epc_discovery_sources (skips existing domains).
 */
function epc_apai_install_country_sources(PDO $pdo, string $siteKey): int
{
	require_once __DIR__ . '/epc_auto_price_engine.php';
	require_once __DIR__ . '/epc_industry_taxonomy.php';

	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	epc_disc_ensure_schema($pdo);
	epc_apai_purge_own_domain_sources($pdo, $siteKey);
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$country = epc_apai_tenant_country($siteKey, $pdo);
	$meta = epc_apai_country_meta($country);
	$sources = epc_apai_country_sources_for_tenant($pdo, $siteKey, $industryKey);
	$added = 0;

	foreach ($sources as $src) {
		$domain = strtolower((string) ($src['domain'] ?? ''));
		if ($domain === '' || epc_apai_is_tenant_own_domain($siteKey, $domain, $pdo)) {
			continue;
		}
		$chk = $pdo->prepare('SELECT `id` FROM `epc_discovery_sources` WHERE `site_key` = ? AND `domain` = ? LIMIT 1');
		$chk->execute(array($siteKey, $domain));
		if ((int) $chk->fetchColumn() > 0) {
			epc_apai_country_source_sync_meta($pdo, $siteKey, $domain, $src);
			continue;
		}
		$save = array(
			'source_type' => 'custom_website',
			'domain' => $domain,
			'label' => (string) ($src['label'] ?? $domain),
			'priority' => (int) ($src['priority'] ?? 100),
			'enabled' => 1,
		);
		if (!empty($src['auth_type'])) {
			$save['auth_type'] = (string) $src['auth_type'];
		}
		epc_disc_source_save($pdo, $siteKey, $save);
		$added++;
	}

	$profiles = epc_apai_industry_profiles();
	$searchLabel = (string) (($profiles[$industryKey]['label'] ?? ucfirst(str_replace('_', ' ', $industryKey))));
	$tld = $meta['tld'];
	$chk = $pdo->prepare('SELECT `id` FROM `epc_discovery_sources` WHERE `site_key` = ? AND `source_type` = ? LIMIT 1');
	$chk->execute(array($siteKey, 'search_engine'));
	if ((int) $chk->fetchColumn() === 0) {
		epc_disc_source_save($pdo, $siteKey, array(
			'source_type' => 'search_engine',
			'domain' => 'google.com',
			'label' => 'Google Search (.' . $tld . ' ' . $searchLabel . ')',
			'priority' => 5,
			'enabled' => 1,
		));
		$added++;
	}

	if (function_exists('epc_apai_install_sell_marketplaces')) {
		require_once __DIR__ . '/epc_apai_marketplace_channels.php';
		epc_apai_install_sell_marketplaces($pdo, $siteKey);
	}

	return $added;
}

/**
 * Scan industry taxonomy packs for new domains and upsert into tenant discovery sources (idempotent).
 */
function epc_apai_discover_platform_sources(PDO $pdo, string $siteKey = ''): array
{
	require_once __DIR__ . '/epc_industry_taxonomy.php';
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$tenants = array();
	if ($siteKey !== '') {
		$tenants[] = $siteKey;
	} elseif (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
		if (function_exists('epc_portal_platform_pdo')) {
			$pp = epc_portal_platform_pdo();
			if ($pp instanceof PDO && function_exists('epc_portal_list_tenants')) {
				foreach (epc_portal_list_tenants($pp) as $row) {
					$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($row['site_key'] ?? ''))));
					if ($sk !== '' && $sk !== 'platform' && (string) ($row['status'] ?? '') === 'live') {
						$tenants[] = $sk;
					}
				}
			}
		}
	}
	$tenants = array_values(array_unique($tenants));
	$results = array();
	foreach ($tenants as $sk) {
		$added = epc_apai_install_country_sources($pdo, $sk);
		$marketAdded = function_exists('epc_apai_install_sell_marketplaces')
			? epc_apai_install_sell_marketplaces($pdo, $sk)
			: 0;
		$results[] = array(
			'site_key' => $sk,
			'country' => epc_apai_tenant_country($sk, $pdo),
			'sources_added' => $added,
			'marketplaces_added' => $marketAdded,
		);
	}
	return array(
		'ok' => true,
		'tenants' => count($results),
		'results' => $results,
	);
}

function epc_apai_country_search_site_filter(string $countryCode): string
{
	$meta = epc_apai_country_meta($countryCode);
	$tld = $meta['tld'];
	if ($tld === 'com') {
		return 'site:amazon.com OR site:bestbuy.com';
	}
	if (strpos($tld, '.') !== false) {
		return 'site:.' . $tld;
	}
	return 'site:.' . $tld;
}
