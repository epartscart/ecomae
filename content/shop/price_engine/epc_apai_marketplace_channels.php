<?php
/**
 * Auto Price AI — marketplace arbitrage taxonomy + opportunity detection.
 *
 * Sell marketplaces = where tenant lists products (Noon, Amazon, eBay…).
 * Buy sources = discovery sources EXCEPT sell marketplaces (Sharaf DG, Jumbo, spare247…).
 */
defined('_ASTEXE_') or die('No access');

/**
 * Registry of sell-marketplace domains by country.
 *
 * @return array<string,array{domain:string,label:string,key:string,search_url:string}>
 */
function epc_apai_sell_marketplace_registry(string $countryCode = 'AE'): array
{
	$country = strtoupper(substr(preg_replace('/[^A-Za-z]/', '', $countryCode), 0, 2));
	$global = array(
		'amazon_com' => array(
			'domain' => 'amazon.com',
			'label' => 'Amazon US',
			'key' => 'amazon_com',
			'search_url' => 'https://www.amazon.com/s?k=%s',
		),
		'amazon_ae' => array(
			'domain' => 'amazon.ae',
			'label' => 'Amazon.ae',
			'key' => 'amazon_ae',
			'search_url' => 'https://www.amazon.ae/s?k=%s',
		),
		'ebay_com' => array(
			'domain' => 'ebay.com',
			'label' => 'eBay US',
			'key' => 'ebay_com',
			'search_url' => 'https://www.ebay.com/sch/i.html?_nkw=%s',
		),
		'ebay_ae' => array(
			'domain' => 'ebay.ae',
			'label' => 'eBay UAE',
			'key' => 'ebay_ae',
			'search_url' => 'https://www.ebay.ae/sch/i.html?_nkw=%s',
		),
	);
	$byCountry = array(
		'AE' => array(
			'noon' => array(
				'domain' => 'noon.com',
				'label' => 'Noon UAE',
				'key' => 'noon',
				'search_url' => 'https://www.noon.com/uae-en/search?q=%s',
			),
			'dubizzle' => array(
				'domain' => 'dubizzle.com',
				'label' => 'Dubizzle',
				'key' => 'dubizzle',
				'search_url' => 'https://uae.dubizzle.com/search/?keyword=%s',
			),
			'amazon_ae' => $global['amazon_ae'],
			'ebay_ae' => $global['ebay_ae'],
		),
		'GB' => array(
			'amazon_uk' => array(
				'domain' => 'amazon.co.uk',
				'label' => 'Amazon UK',
				'key' => 'amazon_uk',
				'search_url' => 'https://www.amazon.co.uk/s?k=%s',
			),
			'ebay_uk' => array(
				'domain' => 'ebay.co.uk',
				'label' => 'eBay UK',
				'key' => 'ebay_uk',
				'search_url' => 'https://www.ebay.co.uk/sch/i.html?_nkw=%s',
			),
		),
		'US' => array(
			'amazon_com' => $global['amazon_com'],
			'ebay_com' => $global['ebay_com'],
		),
		'SA' => array(
			'noon_sa' => array(
				'domain' => 'noon.com',
				'label' => 'Noon KSA',
				'key' => 'noon_sa',
				'search_url' => 'https://www.noon.com/saudi-en/search?q=%s',
			),
			'amazon_sa' => array(
				'domain' => 'amazon.sa',
				'label' => 'Amazon.sa',
				'key' => 'amazon_sa',
				'search_url' => 'https://www.amazon.sa/s?k=%s',
			),
		),
		'OM' => array(
			'noon_om' => array(
				'domain' => 'noon.com',
				'label' => 'Noon Oman',
				'key' => 'noon_om',
				'search_url' => 'https://www.noon.com/oman-en/search?q=%s',
			),
		),
		'PK' => array(
			'daraz_pk' => array(
				'domain' => 'daraz.pk',
				'label' => 'Daraz Pakistan',
				'key' => 'daraz_pk',
				'search_url' => 'https://www.daraz.pk/catalog/?q=%s',
			),
		),
		'IN' => array(
			'flipkart' => array(
				'domain' => 'flipkart.com',
				'label' => 'Flipkart',
				'key' => 'flipkart',
				'search_url' => 'https://www.flipkart.com/search?q=%s',
			),
			'amazon_in' => array(
				'domain' => 'amazon.in',
				'label' => 'Amazon India',
				'key' => 'amazon_in',
				'search_url' => 'https://www.amazon.in/s?k=%s',
			),
		),
	);
	$pack = $byCountry[$country] ?? $byCountry['AE'];
	return array_merge($global, $pack);
}

/**
 * Sell marketplaces for a country: global eBay + Amazon.com plus local channels.
 *
 * @return array<int,array{domain:string,label:string,key:string}>
 */
function epc_apai_sell_marketplaces_for_country(string $countryCode): array
{
	$registry = epc_apai_sell_marketplace_registry($countryCode);
	$globalKeys = array('ebay_com', 'amazon_com');
	$out = array();
	foreach ($globalKeys as $gk) {
		if (isset($registry[$gk])) {
			$out[] = $registry[$gk];
		}
	}
	foreach ($registry as $key => $entry) {
		if (in_array($key, $globalKeys, true)) {
			continue;
		}
		$dom = (string) ($entry['domain'] ?? '');
		if ($dom === 'ebay.com' || $dom === 'amazon.com') {
			continue;
		}
		$out[] = $entry;
	}
	return $out;
}

/**
 * Auto-enable sell marketplaces in tenant config_json.
 */
function epc_apai_install_sell_marketplaces(PDO $pdo, string $siteKey): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if (!function_exists('epc_ape_tenant_config_get')) {
		require_once __DIR__ . '/epc_auto_price_engine.php';
	}
	$country = function_exists('epc_apai_tenant_country') ? epc_apai_tenant_country($siteKey, $pdo) : 'AE';
	$sell = epc_apai_sell_marketplaces_for_country($country);
	$domains = array();
	foreach ($sell as $entry) {
		$d = function_exists('epc_apai_normalize_domain')
			? epc_apai_normalize_domain((string) ($entry['domain'] ?? ''))
			: strtolower(trim((string) ($entry['domain'] ?? '')));
		if ($d !== '') {
			$domains[] = $d;
		}
	}
	if (!$domains) {
		return 0;
	}
	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	$arb = (array) ($config['marketplace_arbitrage'] ?? array());
	$existing = (array) ($arb['sell_marketplaces'] ?? array());
	$merged = array_values(array_unique(array_merge($existing, $domains)));
	if ($merged === $existing) {
		return 0;
	}
	$arb['sell_marketplaces'] = $merged;
	if (empty($arb['primary_marketplace'])) {
		$arb['primary_marketplace'] = in_array('noon.com', $merged, true) ? 'noon' : 'amazon_com';
	}
	$arb['enabled'] = true;
	$config['marketplace_arbitrage'] = $arb;
	$now = time();
	$pdo->prepare(
		'INSERT INTO `epc_auto_price_tenant_config` (`site_key`, `profile`, `currency`, `active`, `config_json`, `updated_at`)
		 VALUES (?, ?, ?, 1, ?, ?)
		 ON DUPLICATE KEY UPDATE `config_json` = VALUES(`config_json`), `updated_at` = VALUES(`updated_at`)'
	)->execute(array(
		$siteKey,
		(string) ($cfg['profile'] ?? 'warehouse_supplier'),
		(string) ($cfg['currency'] ?? epc_apai_country_meta($country)['currency']),
		json_encode($config, JSON_UNESCAPED_UNICODE),
		$now,
	));
	return count($merged) - count($existing);
}

/**
 * Classify a source domain: sell_marketplace | buy_source | own_tenant.
 *
 * Non-marketplace sources are BUY-ONLY — never treated as sell targets.
 */
function epc_apai_source_role(string $domain, string $siteKey, ?PDO $pdo = null): string
{
	$domain = function_exists('epc_apai_normalize_domain')
		? epc_apai_normalize_domain($domain)
		: strtolower(preg_replace('/^www\./', '', trim($domain)));
	if ($domain === '') {
		return 'unknown';
	}
	if (function_exists('epc_apai_tenant_own_domains')) {
		foreach (epc_apai_tenant_own_domains($siteKey, $pdo) as $own) {
			if ($domain === $own || ($own !== '' && substr($domain, -strlen('.' . $own)) === '.' . $own)) {
				return 'own_tenant';
			}
		}
	}
	$sellSet = array();
	if ($pdo instanceof PDO) {
		$channels = epc_apai_marketplace_channels_for_tenant($pdo, $siteKey);
		foreach ((array) ($channels['sell_domains'] ?? array()) as $sd) {
			$sellSet[(string) $sd] = true;
		}
	} else {
		foreach (epc_apai_sell_marketplace_registry() as $entry) {
			$d = (string) ($entry['domain'] ?? '');
			if ($d !== '') {
				$sellSet[$d] = true;
			}
		}
	}
	if (isset($sellSet[$domain])) {
		return 'sell_marketplace';
	}
	return 'buy_source';
}

/**
 * Default sell/buy channel sets per tenant profile + site_key.
 *
 * @return array{sell:array<int,string>,buy:array<int,string>,primary:string,enabled:bool}
 */
function epc_apai_marketplace_profile_defaults(string $siteKey, string $profile, string $countryCode = 'AE'): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$registry = epc_apai_sell_marketplace_registry($countryCode);
	$allSellDomains = array();
	foreach ($registry as $entry) {
		$allSellDomains[] = (string) ($entry['domain'] ?? '');
	}

	$defaults = array(
		'electronicae' => array(
			'sell' => array('noon.com', 'amazon.ae', 'ebay.ae'),
			'buy' => array('sharafdg.com', 'jumbo.ae', 'virginmegastore.ae', 'microless.com'),
			'primary' => 'noon',
		),
		'epartscart' => array(
			'sell' => array('noon.com', 'amazon.ae', 'dubizzle.com'),
			'buy' => array('spare247.com', 'autoparts.ae', 'partsouq.com'),
			'primary' => 'noon',
		),
	);

	$profileDefaults = array(
		'marketplace_arbitrage' => array(
			'sell' => array('noon.com', 'amazon.ae', 'ebay.ae'),
			'buy' => array(),
			'primary' => 'noon',
		),
		'warehouse_supplier' => array(
			'sell' => array('noon.com', 'amazon.ae'),
			'buy' => array(),
			'primary' => 'noon',
		),
	);

	$base = $defaults[$siteKey] ?? ($profileDefaults[$profile] ?? $profileDefaults['marketplace_arbitrage']);
	if (empty($base['buy'])) {
		$base['buy'] = array();
	}

	return array(
		'sell' => (array) ($base['sell'] ?? array('noon.com', 'amazon.ae')),
		'buy' => (array) ($base['buy'] ?? array()),
		'primary' => (string) ($base['primary'] ?? 'noon'),
		'enabled' => in_array($profile, array('marketplace_arbitrage', 'warehouse_supplier'), true),
	);
}

/**
 * Resolved marketplace channels for tenant (config_json overrides profile defaults).
 *
 * @return array{sell:array<int,array{domain:string,label:string,key:string}>,buy:array<int,string>,primary:string,primary_label:string,min_margin_pct:float,enabled:bool,sell_domains:array<int,string>}
 */
function epc_apai_marketplace_channels_for_tenant(PDO $pdo, string $siteKey): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if (is_file(__DIR__ . '/epc_apai_country_sources.php')) {
		require_once __DIR__ . '/epc_apai_country_sources.php';
	}
	if (is_file(__DIR__ . '/epc_auto_price_engine.php') && !function_exists('epc_ape_tenant_config_get')) {
		require_once __DIR__ . '/epc_auto_price_engine.php';
	}

	$country = function_exists('epc_apai_tenant_country') ? epc_apai_tenant_country($siteKey, $pdo) : 'AE';
	$registry = epc_apai_sell_marketplace_registry($country);
	$cfg = function_exists('epc_ape_tenant_config_get') ? epc_ape_tenant_config_get($pdo, $siteKey) : array('profile' => 'marketplace_arbitrage', 'config' => array());
	$profile = (string) ($cfg['profile'] ?? 'marketplace_arbitrage');
	$config = (array) ($cfg['config'] ?? array());
	$arbCfg = (array) ($config['marketplace_arbitrage'] ?? array());
	$defaults = epc_apai_marketplace_profile_defaults($siteKey, $profile, $country);

	$sellDomains = (array) ($arbCfg['sell_marketplaces'] ?? $defaults['sell']);
	$buyDomains = (array) ($arbCfg['buy_sources'] ?? $defaults['buy']);
	$primary = (string) ($arbCfg['primary_marketplace'] ?? $defaults['primary']);
	$enabled = !array_key_exists('enabled', $arbCfg) ? (bool) $defaults['enabled'] : !empty($arbCfg['enabled']);

	$rules = function_exists('epc_ape_rules_get') ? epc_ape_rules_get($pdo, $siteKey) : array('min_margin_percent' => 15);
	$minMargin = (float) ($arbCfg['min_margin_pct'] ?? $rules['min_margin_percent'] ?? 15);

	$sell = array();
	$domainToKey = array();
	foreach ($registry as $key => $entry) {
		$domainToKey[(string) ($entry['domain'] ?? '')] = $key;
	}
	foreach ($sellDomains as $dom) {
		$dom = function_exists('epc_apai_normalize_domain') ? epc_apai_normalize_domain((string) $dom) : strtolower(trim((string) $dom));
		if ($dom === '') {
			continue;
		}
		$key = $domainToKey[$dom] ?? preg_replace('/[^a-z0-9_]/', '_', str_replace('.', '_', $dom));
		$label = $dom;
		foreach ($registry as $entry) {
			if ((string) ($entry['domain'] ?? '') === $dom) {
				$label = (string) ($entry['label'] ?? $dom);
				$key = (string) ($entry['key'] ?? $key);
				break;
			}
		}
		$sell[] = array('domain' => $dom, 'label' => $label, 'key' => $key);
	}

	if (empty($buyDomains) && function_exists('epc_apai_country_sources_for_tenant')) {
		require_once __DIR__ . '/epc_industry_taxonomy.php';
		$industry = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
		$allSources = epc_apai_country_sources_for_tenant($pdo, $siteKey, $industry);
		$sellSet = array();
		foreach ($sell as $s) {
			$sellSet[(string) $s['domain']] = true;
		}
		foreach ($allSources as $src) {
			$d = function_exists('epc_apai_normalize_domain')
				? epc_apai_normalize_domain((string) ($src['domain'] ?? ''))
				: strtolower((string) ($src['domain'] ?? ''));
			if ($d !== '' && !isset($sellSet[$d])) {
				$buyDomains[] = $d;
			}
		}
	}
	$buyDomains = array_values(array_unique(array_filter(array_map(function ($d) {
		return function_exists('epc_apai_normalize_domain') ? epc_apai_normalize_domain((string) $d) : strtolower(trim((string) $d));
	}, $buyDomains))));

	$primaryLabel = $primary;
	foreach ($sell as $s) {
		if ((string) ($s['key'] ?? '') === $primary || (string) ($s['domain'] ?? '') === $primary) {
			$primaryLabel = (string) ($s['label'] ?? $primary);
			$primary = (string) ($s['key'] ?? $primary);
			break;
		}
	}

	return array(
		'sell' => $sell,
		'buy' => $buyDomains,
		'primary' => $primary,
		'primary_label' => $primaryLabel,
		'min_margin_pct' => $minMargin,
		'enabled' => $enabled,
		'sell_domains' => array_column($sell, 'domain'),
		'buy_sources_only' => true,
		'sell_marketplaces' => array_column($sell, 'domain'),
	);
}

/**
 * Whether tenant should see marketplace arbitrage UI/features.
 */
function epc_apai_marketplace_arbitrage_enabled(PDO $pdo, string $siteKey): bool
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if (function_exists('epc_apai_resolve_industry')) {
		require_once __DIR__ . '/epc_industry_taxonomy.php';
		if (epc_apai_resolve_industry($pdo, $siteKey) === 'tax_advisory') {
			return false;
		}
	}
	$channels = epc_apai_marketplace_channels_for_tenant($pdo, $siteKey);
	return !empty($channels['enabled']) && !empty($channels['sell']) && !empty($channels['buy']);
}

/**
 * Save marketplace arbitrage settings into tenant config_json.
 *
 * @param array<string,mixed> $data sell_marketplaces[], buy_sources[], primary_marketplace, min_margin_pct
 */
function epc_apai_save_marketplace_arbitrage_config(PDO $pdo, string $siteKey, array $data): void
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if (!function_exists('epc_ape_tenant_config_get')) {
		require_once __DIR__ . '/epc_auto_price_engine.php';
	}
	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	$arb = (array) ($config['marketplace_arbitrage'] ?? array());

	if (isset($data['sell_marketplaces']) && is_array($data['sell_marketplaces'])) {
		$arb['sell_marketplaces'] = array_values(array_filter(array_map(function ($d) {
			return function_exists('epc_apai_normalize_domain') ? epc_apai_normalize_domain((string) $d) : strtolower(trim((string) $d));
		}, $data['sell_marketplaces'])));
	}
	if (isset($data['buy_sources']) && is_array($data['buy_sources'])) {
		$arb['buy_sources'] = array_values(array_filter(array_map(function ($d) {
			return function_exists('epc_apai_normalize_domain') ? epc_apai_normalize_domain((string) $d) : strtolower(trim((string) $d));
		}, $data['buy_sources'])));
	}
	if (!empty($data['primary_marketplace'])) {
		$arb['primary_marketplace'] = (string) $data['primary_marketplace'];
	}
	if (isset($data['min_margin_pct'])) {
		$arb['min_margin_pct'] = max(0, (float) $data['min_margin_pct']);
	}
	if (isset($data['enabled'])) {
		$arb['enabled'] = !empty($data['enabled']);
	}

	$config['marketplace_arbitrage'] = $arb;
	$now = time();
	$pdo->prepare(
		'INSERT INTO `epc_auto_price_tenant_config` (`site_key`, `profile`, `currency`, `active`, `config_json`, `updated_at`)
		 VALUES (?, ?, ?, 1, ?, ?)
		 ON DUPLICATE KEY UPDATE `config_json` = VALUES(`config_json`), `updated_at` = VALUES(`updated_at`)'
	)->execute(array(
		$siteKey,
		(string) ($cfg['profile'] ?? 'marketplace_arbitrage'),
		(string) ($cfg['currency'] ?? 'AED'),
		json_encode($config, JSON_UNESCAPED_UNICODE),
		$now,
	));
}

/**
 * Build search query from queue row (model, SKU, brand+article, or title).
 *
 * @param array<string,mixed> $row
 */
function epc_apai_marketplace_search_query(array $row): string
{
	$specs = is_array($row['specs'] ?? null) ? $row['specs'] : array();
	if (!$specs) {
		$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
	}
	foreach (array('Model', 'model', 'SKU', 'sku', 'MPN', 'Part Number', 'Article') as $k) {
		if (!empty($specs[$k])) {
			return trim((string) $specs[$k]);
		}
	}
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (is_array($meta) && !empty($meta['brand_article_key'])) {
		$parts = explode(':', (string) $meta['brand_article_key'], 2);
		if (count($parts) === 2) {
			return trim($parts[1]);
		}
	}
	$title = trim((string) ($row['title'] ?? ''));
	if (strlen($title) > 80) {
		$title = substr($title, 0, 80);
	}
	return $title;
}

/**
 * MVP: HTTP fetch marketplace search page, check if query token appears in results HTML.
 *
 * @return array{found:bool,price:float,checked_at:int,search_url:string}
 */
function epc_apai_marketplace_search_presence(string $marketplaceKey, string $query, string $countryCode = 'AE'): array
{
	$query = trim($query);
	if ($query === '') {
		return array('found' => false, 'price' => 0.0, 'checked_at' => time(), 'search_url' => '');
	}
	$registry = epc_apai_sell_marketplace_registry($countryCode);
	$entry = $registry[$marketplaceKey] ?? null;
	if (!$entry && strpos($marketplaceKey, '.') !== false) {
		foreach ($registry as $r) {
			if ((string) ($r['domain'] ?? '') === $marketplaceKey) {
				$entry = $r;
				break;
			}
		}
	}
	if (!$entry) {
		return array('found' => false, 'price' => 0.0, 'checked_at' => time(), 'search_url' => '');
	}

	$searchUrl = sprintf((string) ($entry['search_url'] ?? ''), rawurlencode($query));
	$html = '';
	if (function_exists('epc_disc_http_get')) {
		$html = epc_disc_http_get($searchUrl, array('timeout' => 12));
	} elseif (function_exists('epc_disc_http_fetch')) {
		$html = epc_disc_http_fetch($searchUrl, array('timeout' => 12));
	}
	if ($html === '') {
		return array('found' => false, 'price' => 0.0, 'checked_at' => time(), 'search_url' => $searchUrl);
	}

	$normQuery = strtolower(preg_replace('/\s+/', ' ', $query));
	$tokens = array_filter(explode(' ', $normQuery), function ($t) {
		return strlen($t) >= 3;
	});
	$found = false;
	if ($tokens) {
		$matchCount = 0;
		$htmlLower = strtolower($html);
		foreach ($tokens as $tok) {
			if (strpos($htmlLower, $tok) !== false) {
				$matchCount++;
			}
		}
		$found = $matchCount >= max(1, (int) ceil(count($tokens) * 0.6));
	} else {
		$found = stripos($html, $normQuery) !== false;
	}

	$noResults = preg_match('/(no results|0 results|did not match|لم يتم|لا توجد)/i', $html);
	if ($noResults) {
		$found = false;
	}

	$price = 0.0;
	if ($found && preg_match('/(?:AED|د\.إ)\s*([\d,]+(?:\.\d{2})?)/i', $html, $m)) {
		$price = (float) str_replace(',', '', $m[1]);
	} elseif ($found && preg_match('/"price"\s*:\s*"?([\d.]+)"?/i', $html, $m)) {
		$price = (float) $m[1];
	}

	return array(
		'found' => $found,
		'price' => $price,
		'checked_at' => time(),
		'search_url' => $searchUrl,
	);
}

/**
 * Check/cache marketplace presence for an identity across sell marketplaces.
 *
 * @param array<string,mixed> $row queue row
 * @param array<string,mixed> $cached existing marketplace_presence from meta
 * @return array<string,array{found:bool,price:float,checked_at:int}>
 */
function epc_apai_marketplace_presence_for_row(PDO $pdo, string $siteKey, array $row, array $cached = array(), bool $forceRefresh = false): array
{
	$channels = epc_apai_marketplace_channels_for_tenant($pdo, $siteKey);
	$query = epc_apai_marketplace_search_query($row);
	$country = function_exists('epc_apai_tenant_country') ? epc_apai_tenant_country($siteKey, $pdo) : 'AE';
	$presence = $cached;
	$cacheTtl = 6 * 3600;
	$now = time();

	foreach ((array) ($channels['sell'] ?? array()) as $sellEntry) {
		$key = (string) ($sellEntry['key'] ?? '');
		$domain = (string) ($sellEntry['domain'] ?? '');
		if ($key === '') {
			continue;
		}
		$prev = (array) ($presence[$key] ?? $presence[$domain] ?? array());
		if (!$forceRefresh && !empty($prev['checked_at']) && ($now - (int) $prev['checked_at']) < $cacheTtl) {
			$presence[$key] = $prev;
			continue;
		}
		$check = epc_apai_marketplace_search_presence($key, $query, $country);
		$presence[$key] = array(
			'found' => !empty($check['found']),
			'price' => (float) ($check['price'] ?? 0),
			'checked_at' => (int) ($check['checked_at'] ?? $now),
			'domain' => $domain,
			'search_url' => (string) ($check['search_url'] ?? ''),
		);
	}

	return $presence;
}

/**
 * Extract buy-source prices from row meta (matched_sources / alternate_sources / primary).
 *
 * @param array<string,mixed> $row
 * @param array<int,string> $buyDomains
 * @return array<int,array{source_domain:string,price:float,currency:string,source_url:string}>
 */
function epc_apai_buy_source_prices_from_row(array $row, array $buyDomains): array
{
	$buySet = array();
	foreach ($buyDomains as $d) {
		$buySet[strtolower($d)] = true;
	}
	$out = array();
	$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($meta)) {
		$meta = array();
	}

	$primaryDomain = strtolower(preg_replace('/^www\./', '', trim((string) ($row['source_domain'] ?? ''))));
	$primaryPrice = (float) ($row['suggested_price'] ?? 0);
	if ($primaryDomain !== '' && isset($buySet[$primaryDomain]) && $primaryPrice > 0) {
		$out[$primaryDomain] = array(
			'source_domain' => $primaryDomain,
			'price' => $primaryPrice,
			'currency' => (string) ($row['currency'] ?? 'AED'),
			'source_url' => (string) ($row['source_url'] ?? ''),
		);
	}

	foreach (array('matched_sources', 'alternate_sources', 'source_prices') as $k) {
		foreach ((array) ($meta[$k] ?? array()) as $src) {
			if (!is_array($src)) {
				continue;
			}
			$dom = strtolower(preg_replace('/^www\./', '', trim((string) ($src['source_domain'] ?? $src['source'] ?? ''))));
			$price = (float) ($src['price'] ?? 0);
			if ($dom === '' || $price <= 0 || !isset($buySet[$dom])) {
				continue;
			}
			if (!isset($out[$dom]) || $price < (float) $out[$dom]['price']) {
				$out[$dom] = array(
					'source_domain' => $dom,
					'price' => $price,
					'currency' => (string) ($src['currency'] ?? 'AED'),
					'source_url' => (string) ($src['source_url'] ?? ''),
				);
			}
		}
	}

	usort($out, function ($a, $b) {
		return ((float) ($a['price'] ?? 0)) <=> ((float) ($b['price'] ?? 0));
	});
	return array_values($out);
}

/**
 * Estimate marketplace sell price from presence checks, buy prices, or markup.
 *
 * @param array<string,array{found:bool,price:float}> $presence
 * @param array<int,array{price:float}> $buySources
 */
function epc_apai_estimate_marketplace_sell_price(array $presence, array $buySources, float $buyMin, float $minMarginPct): array
{
	$competitorPrices = array();
	foreach ($presence as $p) {
		if (!empty($p['found']) && (float) ($p['price'] ?? 0) > 0) {
			$competitorPrices[] = (float) $p['price'];
		}
	}
	if ($competitorPrices) {
		$avg = array_sum($competitorPrices) / count($competitorPrices);
		return array('price' => round($avg, 2), 'method' => 'competitor_avg', 'known' => true);
	}

	if ($buyMin > 0) {
		$markup = max($minMarginPct, 15) / 100;
		return array('price' => round($buyMin * (1 + $markup + 0.1), 2), 'method' => 'category_markup_estimate', 'known' => false);
	}
	return array('price' => 0.0, 'method' => 'research_needed', 'known' => false);
}

/**
 * Scan discovery queue for marketplace arbitrage opportunities.
 *
 * @param array<string,mixed> $options limit, check_presence (bool), top_lines_only (bool)
 * @return array{ok:bool,scanned:int,opportunities:int,updated:int,message:string}
 */
function epc_disc_marketplace_arbitrage_scan(PDO $pdo, string $siteKey, array $options = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if (!epc_apai_marketplace_arbitrage_enabled($pdo, $siteKey)) {
		return array('ok' => true, 'scanned' => 0, 'opportunities' => 0, 'updated' => 0, 'message' => 'Marketplace arbitrage not enabled for tenant');
	}

	if (!function_exists('epc_disc_queue_identity_key')) {
		require_once __DIR__ . '/epc_auto_price_engine.php';
	}
	if (!function_exists('epc_disc_http_get') && is_file(__DIR__ . '/epc_discovery_adapters.php')) {
		require_once __DIR__ . '/epc_discovery_adapters.php';
	}

	$channels = epc_apai_marketplace_channels_for_tenant($pdo, $siteKey);
	$buyDomains = (array) ($channels['buy'] ?? array());
	$sellDomains = (array) ($channels['sell_domains'] ?? array());
	$minMargin = (float) ($channels['min_margin_pct'] ?? 15);
	$checkPresence = !array_key_exists('check_presence', $options) || !empty($options['check_presence']);
	$limit = max(1, min(200, (int) ($options['limit'] ?? 80)));
	$now = time();

	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `status` = \'suggested\'
		 ORDER BY `updated_at` DESC LIMIT ' . (int) $limit
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$identityPresenceCache = array();
	$scanned = 0;
	$opportunities = 0;
	$updated = 0;
	$updStmt = $pdo->prepare('UPDATE `epc_product_discovery_queue` SET `meta_json` = ?, `cost_estimate` = ?, `sell_price` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?');

	foreach ($rows as $row) {
		$buySources = epc_apai_buy_source_prices_from_row($row, $buyDomains);
		if (!$buySources) {
			continue;
		}
		$scanned++;
		$buyMin = (float) ($buySources[0]['price'] ?? 0);
		if ($buyMin <= 0) {
			continue;
		}

		$identityKey = epc_disc_queue_identity_key($pdo, $siteKey, $row);
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}

		$cachedPresence = (array) ($identityPresenceCache[$identityKey] ?? $meta['marketplace_presence'] ?? array());
		if ($checkPresence) {
			$presence = epc_apai_marketplace_presence_for_row($pdo, $siteKey, $row, $cachedPresence, empty($cachedPresence));
			if ($identityKey !== '') {
				$identityPresenceCache[$identityKey] = $presence;
			}
		} else {
			$presence = $cachedPresence;
		}
		$meta['marketplace_presence'] = $presence;

		$missingMarketplaces = array();
		$onAnySell = false;
		foreach ((array) ($channels['sell'] ?? array()) as $sellEntry) {
			$key = (string) ($sellEntry['key'] ?? '');
			$domain = (string) ($sellEntry['domain'] ?? '');
			$p = (array) ($presence[$key] ?? $presence[$domain] ?? array());
			if (!empty($p['found'])) {
				$onAnySell = true;
			} else {
				$missingMarketplaces[] = $domain;
			}
		}

		$estSell = epc_apai_estimate_marketplace_sell_price($presence, $buySources, $buyMin, $minMargin);
		$estPrice = (float) ($estSell['price'] ?? 0);
		$marginAbs = $estPrice > 0 ? round($estPrice - $buyMin, 2) : 0.0;
		$marginPct = ($buyMin > 0 && $marginAbs > 0) ? round(($marginAbs / $buyMin) * 100, 1) : 0.0;

		$isOpportunity = !$onAnySell && $marginPct >= $minMargin;
		$meta['arbitrage_opportunity'] = $isOpportunity;
		$meta['buy_sources'] = $buySources;
		$meta['buy_price_min'] = $buyMin;
		$meta['missing_marketplaces'] = $missingMarketplaces;
		$meta['estimated_marketplace_price'] = $estPrice;
		$meta['estimated_marketplace_method'] = (string) ($estSell['method'] ?? '');
		$meta['estimated_marketplace_known'] = !empty($estSell['known']);
		$meta['arbitrage_margin_abs'] = $marginAbs;
		$meta['arbitrage_margin_pct'] = $marginPct;
		$meta['list_on_marketplaces'] = array_map(function ($s) {
			return (string) ($s['key'] ?? '');
		}, (array) ($channels['sell'] ?? array()));
		$meta['primary_marketplace'] = (string) ($channels['primary'] ?? 'noon');
		$meta['arbitrage_scan_at'] = $now;

		if ($isOpportunity) {
			$opportunities++;
		}

		$costEst = $buyMin;
		$sellEst = $estPrice > 0 ? $estPrice : (float) ($row['sell_price'] ?? 0);
		$updStmt->execute(array(
			json_encode($meta, JSON_UNESCAPED_UNICODE),
			$costEst,
			$sellEst,
			$now,
			(int) ($row['id'] ?? 0),
			$siteKey,
		));
		if ($updStmt->rowCount() > 0) {
			$updated++;
		}
	}

	return array(
		'ok' => true,
		'scanned' => $scanned,
		'opportunities' => $opportunities,
		'updated' => $updated,
		'message' => "Arbitrage scan: {$scanned} products, {$opportunities} opportunities, {$updated} updated",
	);
}

/**
 * Compare-tab matrix: marketplace gap opportunities.
 *
 * @return array<int,array<string,mixed>>
 */
function epc_disc_marketplace_gaps_matrix(PDO $pdo, string $siteKey, array $options = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if (!epc_apai_marketplace_arbitrage_enabled($pdo, $siteKey)) {
		return array();
	}
	if (!function_exists('epc_disc_pricing_advice') && is_file(__DIR__ . '/epc_auto_price_engine.php')) {
		require_once __DIR__ . '/epc_auto_price_engine.php';
	}
	$channels = epc_apai_marketplace_channels_for_tenant($pdo, $siteKey);
	$limit = max(1, min(100, (int) ($options['limit'] ?? 50)));
	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `status` = \'suggested\'
		 ORDER BY `updated_at` DESC LIMIT ' . (int) ($limit * 3)
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$out = array();

	foreach ($rows as $row) {
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (!is_array($meta) || empty($meta['arbitrage_opportunity'])) {
			continue;
		}
		$presence = (array) ($meta['marketplace_presence'] ?? array());
		$presenceCols = array();
		foreach ((array) ($channels['sell'] ?? array()) as $sellEntry) {
			$key = (string) ($sellEntry['key'] ?? '');
			$p = (array) ($presence[$key] ?? array());
			$presenceCols[$key] = !empty($p['found']);
		}
		$out[] = array(
			'queue_id' => (int) ($row['id'] ?? 0),
			'title' => (string) ($row['title'] ?? ''),
			'brand' => (string) ($meta['brand'] ?? ''),
			'article_number' => (string) ($meta['article_number'] ?? ''),
			'buy_price' => (float) ($meta['buy_price_min'] ?? 0),
			'buy_price_max' => (float) (($meta['buy_sources'] ?? array()) ? max(array_map(function ($b) {
				return (float) ($b['price'] ?? 0);
			}, (array) $meta['buy_sources'])) : 0),
			'buy_source' => (string) (($meta['buy_sources'][0]['source_domain'] ?? '') ?: ($row['source_domain'] ?? '')),
			'buy_source_labels' => array_map(function ($b) {
				return (string) ($b['source_domain'] ?? '');
			}, (array) ($meta['buy_sources'] ?? array())),
			'marketplace_price' => (float) ($meta['estimated_marketplace_price'] ?? 0),
			'marketplace_known' => !empty($meta['estimated_marketplace_known']),
			'estimated_sell' => (float) ($meta['estimated_marketplace_price'] ?? 0),
			'estimated_known' => !empty($meta['estimated_marketplace_known']),
			'your_price' => 0.0,
			'margin_abs' => (float) ($meta['arbitrage_margin_abs'] ?? 0),
			'margin_pct' => (float) ($meta['arbitrage_margin_pct'] ?? 0),
			'missing_marketplaces' => (array) ($meta['missing_marketplaces'] ?? array()),
			'presence' => $presenceCols,
			'currency' => (string) ($row['currency'] ?? 'AED'),
			'primary_marketplace' => (string) ($channels['primary_label'] ?? 'Noon'),
			'pricing_advice' => function_exists('epc_disc_pricing_advice') ? epc_disc_pricing_advice(array(
				'currency' => (string) ($row['currency'] ?? 'AED'),
				'arbitrage_opportunity' => true,
				'missing_marketplaces' => (array) ($meta['missing_marketplaces'] ?? array()),
				'primary_marketplace' => (string) ($channels['primary_label'] ?? 'Noon'),
				'source_price_range' => array(
					'buy_min' => (float) ($meta['buy_price_min'] ?? 0),
					'target_sell_price' => (float) ($meta['estimated_marketplace_price'] ?? 0),
				),
			), 0, (float) ($meta['buy_price_min'] ?? 0), (float) ($meta['estimated_marketplace_price'] ?? 0)) : array(),
		);
		if (count($out) >= $limit) {
			break;
		}
	}

	usort($out, function ($a, $b) {
		return ((float) ($b['margin_pct'] ?? 0)) <=> ((float) ($a['margin_pct'] ?? 0));
	});
	return $out;
}
