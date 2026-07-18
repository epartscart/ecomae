<?php
/**
 * Google indexing rules for CHPU part URLs and canonical helpers.
 * Warehouse supplier tenants (epartscart): index parts even when guest prices are hidden.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_seo_lang_href()
{
	global $multilang_params;
	if (isset($multilang_params['lang_href']) && $multilang_params['lang_href'] !== '') {
		return rtrim((string) $multilang_params['lang_href'], '/');
	}
	return '/en';
}

/** @return string en|ar|ru */
function epc_seo_current_lang_code(): string
{
	$href = epc_seo_lang_href();
	if (preg_match('#^/(en|ar|ru)(/|$)#', $href, $m)) {
		return $m[1];
	}
	global $multilang_params;
	if (!empty($multilang_params['lang'])) {
		$lang = strtolower((string) $multilang_params['lang']);
		if (in_array($lang, array('en', 'ar', 'ru'), true)) {
			return $lang;
		}
	}
	return 'en';
}

function epc_seo_site_key(): string
{
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (strpos($host, 'epartscart') !== false) {
		return 'epartscart';
	}
	return 'epartscart';
}

/** GCC + Pakistan ISO2 codes served from UAE warehouse. */
function epc_seo_regional_country_codes(): array
{
	return array('AE', 'SA', 'OM', 'QA', 'BH', 'KW', 'PK');
}

function epc_seo_tenant_country_code($db_link): string
{
	$code = 'AE';
	if ($db_link instanceof PDO && is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_country_sources.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_country_sources.php';
		if (function_exists('epc_apai_tenant_country')) {
			$code = epc_apai_tenant_country(epc_seo_site_key(), $db_link);
		}
	}
	$code = strtoupper(preg_replace('/[^A-Z]/', '', (string) $code));
	return strlen($code) >= 2 ? substr($code, 0, 2) : 'AE';
}

function epc_seo_regional_shipping_phrase($lang = ''): string
{
	if ($lang === '') {
		$lang = epc_seo_current_lang_code();
	}
	if ($lang === 'ar') {
		return 'مستودع الإمارات · شحن إلى دول الخليج وباكستان · تصدير عالمي';
	}
	if ($lang === 'ru') {
		return 'Склад ОАЭ · доставка в GCC и Пакистан · экспорт по всему миру';
	}
	return 'UAE warehouse · ships GCC & Pakistan · worldwide export';
}

function epc_seo_regional_hub_description($hubKey, $lang = ''): string
{
	if ($lang === '') {
		$lang = epc_seo_current_lang_code();
	}
	$ship = epc_seo_regional_shipping_phrase($lang);
	$map = array(
		'available-brands' => array(
			'en' => 'Browse in-stock auto parts brands.',
			'ar' => 'تصفح ماركات قطع الغيار المتوفرة في المخزون.',
			'ru' => 'Каталог брендов автозапчастей в наличии.',
		),
		'spare-parts' => array(
			'en' => 'Search warehouse spare parts by brand and article.',
			'ar' => 'ابحث عن قطع الغيار حسب الماركة ورقم القطعة.',
			'ru' => 'Поиск запчастей на складе по бренду и артикулу.',
		),
		'parts' => array(
			'en' => 'Search in-stock auto parts by brand and article number.',
			'ar' => 'ابحث عن قطع الغيار المتوفرة حسب الماركة ورقم القطعة.',
			'ru' => 'Поиск автозапчастей в наличии по бренду и артикулу.',
		),
	);
	$intro = $map[$hubKey][$lang] ?? $map[$hubKey]['en'] ?? '';
	return trim($intro . ' ' . $ship);
}

function epc_seo_schema_country_entries(): array
{
	$names = array(
		'AE' => 'United Arab Emirates',
		'SA' => 'Saudi Arabia',
		'OM' => 'Oman',
		'QA' => 'Qatar',
		'BH' => 'Bahrain',
		'KW' => 'Kuwait',
		'PK' => 'Pakistan',
	);
	$out = array();
	foreach (epc_seo_regional_country_codes() as $code) {
		$out[] = array(
			'@type' => 'Country',
			'name' => $names[$code] ?? $code,
		);
	}
	$out[] = array('@type' => 'Place', 'name' => 'Worldwide');
	return $out;
}

function epc_seo_schema_shipping_destinations(): array
{
	$dest = array();
	foreach (epc_seo_regional_country_codes() as $code) {
		$dest[] = array(
			'@type' => 'DefinedRegion',
			'addressCountry' => $code,
		);
	}
	return $dest;
}

/**
 * Warehouse supplier storefront: index catalog/parts for Google regardless of guest price visibility.
 */
function epc_seo_warehouse_indexing_enabled($db_link): bool
{
	if (!($db_link instanceof PDO)) {
		return false;
	}
	$storefront = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_storefront.php';
	if (!is_file($storefront)) {
		return false;
	}
	require_once $storefront;
	return function_exists('epc_apai_is_warehouse_auto_parts_storefront')
		&& epc_apai_is_warehouse_auto_parts_storefront($db_link);
}

function epc_seo_stock_requires_price($db_link): bool
{
	return !epc_seo_warehouse_indexing_enabled($db_link);
}

function epc_seo_brand_article_has_stock($db_link, $manufacturer, $article)
{
	if ($manufacturer === '' || $article === '') {
		return false;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
	$mfr = html_entity_decode((string) $manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8');
	$norm = docpart_normalize_article_for_price(html_entity_decode((string) $article, ENT_QUOTES | ENT_XML1, 'UTF-8'));
	if ($norm === '') {
		return false;
	}
	$expr = docpart_sql_article_normalized_expr('`article`');
	$priceClause = epc_seo_stock_requires_price($db_link) ? ' AND IFNULL(`price`, 0) > 0' : '';
	try {
		$stmt = $db_link->prepare(
			'SELECT 1 FROM `shop_docpart_prices_data`
			WHERE ' . $expr . ' = ?
			AND UPPER(TRIM(`manufacturer`)) = UPPER(?)
			AND IFNULL(`exist`, 0) > 0' . $priceClause . '
			LIMIT 1'
		);
		$stmt->execute(array($norm, $mfr));
		return (bool) $stmt->fetch(PDO::FETCH_NUM);
	} catch (Exception $e) {
		return false;
	}
}

function epc_seo_brand_has_stock($db_link, $manufacturer)
{
	if ($manufacturer === '') {
		return false;
	}
	$mfr = html_entity_decode((string) $manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8');
	$priceClause = epc_seo_stock_requires_price($db_link) ? ' AND IFNULL(`price`, 0) > 0' : '';
	try {
		$stmt = $db_link->prepare(
			'SELECT 1 FROM `shop_docpart_prices_data`
			WHERE UPPER(TRIM(`manufacturer`)) = UPPER(?)
			AND IFNULL(`exist`, 0) > 0' . $priceClause . '
			AND TRIM(IFNULL(`article`, \'\')) != \'\'
			LIMIT 1'
		);
		$stmt->execute(array($mfr));
		return (bool) $stmt->fetch(PDO::FETCH_NUM);
	} catch (Exception $e) {
		return false;
	}
}

function epc_seo_build_parts_url($DP_Config, $lang_href, array $segments)
{
	$base = rtrim((string) $DP_Config->domain_path, '/');
	$lang = rtrim((string) $lang_href, '/');
	if ($lang === '') {
		$lang = '/en';
	}
	$slash = $DP_Config->chpu_search_config['slash_code'];
	$parts_root = $DP_Config->chpu_search_config['level_1']['url'];
	$encoded = array();
	foreach ($segments as $segment) {
		$segment = str_replace('/', $slash, (string) $segment);
		$encoded[] = rawurlencode($segment);
	}
	return $base . $lang . '/' . $parts_root . '/' . implode('/', $encoded);
}

function epc_seo_primary_brand_article_url($db_link, $DP_Config, $lang_href, $article)
{
	if ($article === '') {
		return '';
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
	$norm = docpart_normalize_article_for_price(html_entity_decode((string) $article, ENT_QUOTES | ENT_XML1, 'UTF-8'));
	if ($norm === '') {
		return '';
	}
	$expr = docpart_sql_article_normalized_expr('`article`');
	$priceClause = epc_seo_stock_requires_price($db_link) ? ' AND IFNULL(`price`, 0) > 0' : '';
	try {
		$stmt = $db_link->prepare(
			'SELECT TRIM(`manufacturer`) AS manufacturer,
				COALESCE(NULLIF(TRIM(`article_show`), \'\'), TRIM(`article`)) AS article_show
			FROM `shop_docpart_prices_data`
			WHERE ' . $expr . ' = ?
			AND IFNULL(`exist`, 0) > 0' . $priceClause . '
			AND TRIM(IFNULL(`manufacturer`, \'\')) != \'\'
			ORDER BY `exist` DESC' . (epc_seo_stock_requires_price($db_link) ? ', `price` ASC' : '') . '
			LIMIT 1'
		);
		$stmt->execute(array($norm));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			return '';
		}
		$artShow = trim((string) ($row['article_show'] ?? $row['article'] ?? ''));
		$artCanon = docpart_normalize_article_for_price($artShow !== '' ? $artShow : (string) $row['article']);
		if ($artCanon === '') {
			$artCanon = $artShow;
		}
		return epc_seo_build_parts_url($DP_Config, $lang_href, array($row['manufacturer'], $artCanon));
	} catch (Exception $e) {
		return '';
	}
}

/**
 * Explicit index,follow for warehouse catalog content pages (spare-parts, available-brands, parts hub).
 */
function epc_seo_apply_storefront_content_meta(&$DP_Content, $db_link)
{
	if (!epc_seo_warehouse_indexing_enabled($db_link)) {
		return;
	}
	$url = trim((string) ($DP_Content->url ?? ''), '/');
	$indexable = array(
		'spare-parts',
		'accessories',
		'accessories-spare-parts',
		'available-brands',
		'shipping-export',
		'umapi_catalog',
		'vehicle-catalog',
	);
	$partsRoot = '';
	global $DP_Config;
	if (is_object($DP_Config) && !empty($DP_Config->chpu_search_config['level_1']['url'])) {
		$partsRoot = trim((string) $DP_Config->chpu_search_config['level_1']['url'], '/');
		if ($partsRoot !== '' && $url === $partsRoot) {
			$indexable[] = $partsRoot;
		}
	}
	if (!in_array($url, $indexable, true)) {
		return;
	}
	if ($DP_Content->robots_tag === '' || stripos($DP_Content->robots_tag, 'noindex') !== false) {
		$DP_Content->robots_tag = 'index, follow';
	}
	global $DP_Config;
	if (!is_object($DP_Config)) {
		return;
	}
	$siteName = epc_seo_site_display_name($DP_Config);
	$lang = epc_seo_current_lang_code();
	$descMap = array(
		'available-brands' => epc_seo_regional_hub_description('available-brands', $lang),
		'spare-parts' => epc_seo_regional_hub_description('spare-parts', $lang),
		'accessories' => 'Browse UAE warehouse car accessories and spare parts by category, brand, price and region. ' . epc_seo_regional_shipping_phrase($lang),
		'accessories-spare-parts' => 'Browse UAE warehouse car accessories and spare parts by category, brand, price and region. ' . epc_seo_regional_shipping_phrase($lang),
		'umapi_catalog' => 'Vehicle parts catalog — find compatible spare parts by make and model. ' . epc_seo_regional_shipping_phrase($lang),
		'vehicle-catalog' => 'Vehicle catalog for OEM and aftermarket spare parts lookup. ' . epc_seo_regional_shipping_phrase($lang),
		'shipping-export' => epc_seo_regional_hub_description('parts', $lang),
	);
	if ($partsRoot !== '' && $url === $partsRoot) {
		$DP_Content->title_tag = ($lang === 'ar') ? 'بحث قطع الغيار' : (($lang === 'ru') ? 'Поиск запчастей' : 'Auto parts search');
		$DP_Content->description_tag = epc_seo_regional_hub_description('parts', $lang);
	} elseif (isset($descMap[$url])) {
		$labels = array(
			'available-brands' => ($lang === 'ar') ? 'الماركات المتوفرة' : (($lang === 'ru') ? 'Доступные бренды' : 'Available brands'),
			'spare-parts' => ($lang === 'ar') ? 'قطع الغيار' : (($lang === 'ru') ? 'Запчасти' : 'Spare parts'),
			'umapi_catalog' => ($lang === 'ar') ? 'كتالوج القطع' : (($lang === 'ru') ? 'Каталог запчастей' : 'Parts catalog'),
			'vehicle-catalog' => ($lang === 'ar') ? 'كتالوج المركبات' : (($lang === 'ru') ? 'Каталог авто' : 'Vehicle catalog'),
			'shipping-export' => ($lang === 'ar') ? 'الشحن والتصدير' : (($lang === 'ru') ? 'Доставка и экспорт' : 'Shipping & export'),
		);
		$DP_Content->title_tag = $labels[$url] ?? ucfirst(str_replace('-', ' ', $url));
		$DP_Content->description_tag = $descMap[$url];
	}
	if ($DP_Content->title_tag !== '') {
		$DP_Content->service_data['epc_seo_page_title'] = $DP_Content->title_tag . ' | ' . $siteName;
	}
}

function epc_seo_site_display_name($DP_Config): string
{
	if (function_exists('translate_str_by_id') && is_object($DP_Config)) {
		$name = trim((string) translate_str_by_id($DP_Config->site_name));
		if ($name !== '') {
			return $name;
		}
	}
	return 'eParts Cart';
}

function epc_seo_sitemap_price_clause($db_link): string
{
	return epc_seo_stock_requires_price($db_link) ? ' AND IFNULL(`price`, 0) > 0' : '';
}

function epc_seo_is_ecomae_marketing_host(): bool
{
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	return in_array($host, array('www.ecomae.com', 'ecomae.com'), true);
}

function epc_seo_fetch_part_row($db_link, $manufacturer, $article): ?array
{
	if (!($db_link instanceof PDO) || $manufacturer === '' || $article === '') {
		return null;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
	$mfr = html_entity_decode((string) $manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8');
	$norm = docpart_normalize_article_for_price(html_entity_decode((string) $article, ENT_QUOTES | ENT_XML1, 'UTF-8'));
	if ($norm === '') {
		return null;
	}
	$expr = docpart_sql_article_normalized_expr('`article`');
	$priceClause = epc_seo_stock_requires_price($db_link) ? ' AND IFNULL(`price`, 0) > 0' : '';
	try {
		$stmt = $db_link->prepare(
			'SELECT TRIM(`manufacturer`) AS manufacturer,
				TRIM(`article`) AS article,
				COALESCE(NULLIF(TRIM(`article_show`), \'\'), TRIM(`article`)) AS article_show,
				TRIM(`name`) AS name,
				IFNULL(`exist`, 0) AS exist,
				IFNULL(`price`, 0) AS price
			FROM `shop_docpart_prices_data`
			WHERE ' . $expr . ' = ?
			AND UPPER(TRIM(`manufacturer`)) = UPPER(?)
			AND IFNULL(`exist`, 0) > 0' . $priceClause . '
			ORDER BY `exist` DESC' . (epc_seo_stock_requires_price($db_link) ? ', `price` ASC' : '') . '
			LIMIT 1'
		);
		$stmt->execute(array($norm, $mfr));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		return $row ?: null;
	} catch (Exception $e) {
		return null;
	}
}

function epc_seo_format_part_title($manufacturer, $article, $siteName): string
{
	$mfr = strtoupper(trim(html_entity_decode((string) $manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8')));
	$art = trim(html_entity_decode((string) $article, ENT_QUOTES | ENT_XML1, 'UTF-8'));
	// Lead with brand + part/article number so Google matches OEM / aftermarket queries.
	return $mfr . ' ' . $art . ' — Part number ' . $art . ' | ' . $siteName;
}

function epc_seo_format_part_description(array $row): string
{
	$name = trim((string) ($row['name'] ?? ''));
	$mfr = trim((string) ($row['manufacturer'] ?? ''));
	$art = trim((string) ($row['article_show'] ?? $row['article'] ?? ''));
	$bits = array();
	$bits[] = 'Part number / article: ' . $art;
	$bits[] = 'Brand: ' . $mfr;
	if ($name !== '') {
		$bits[] = $name;
	}
	if ((int) ($row['exist'] ?? 0) > 0) {
		$lang = epc_seo_current_lang_code();
		$bits[] = ($lang === 'ar') ? 'متوفر في المستودع' : (($lang === 'ru') ? 'В наличии на складе' : 'In stock at UAE warehouse');
	}
	$bits[] = epc_seo_regional_shipping_phrase();
	return implode('. ', $bits) . '.';
}

function epc_seo_schema_include_price($db_link): bool
{
	$helper = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
	if (is_file($helper)) {
		require_once $helper;
		if (function_exists('epc_storefront_prices_visible_for_user')) {
			return epc_storefront_prices_visible_for_user();
		}
	}
	return true;
}

/**
 * JSON-LD Product for CHPU part pages (part number / article clearly exposed).
 *
 * @param list<array{brand?:string,article?:string}> $crossRefs Optional OE / cross numbers
 */
function epc_seo_build_product_schema_array($row, $DP_Config, $lang_href, $includePrice, $currencyCode = 'AED', array $crossRefs = array()): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
	$articleShow = trim((string) ($row['article_show'] ?? $row['article'] ?? ''));
	$artNorm = docpart_normalize_article_for_price($articleShow !== '' ? $articleShow : (string) ($row['article'] ?? ''));
	$mfr = trim((string) ($row['manufacturer'] ?? ''));
	$name = trim($mfr . ' ' . ($articleShow !== '' ? $articleShow : $artNorm) . ' ' . trim((string) ($row['name'] ?? '')));
	$slash = $DP_Config->chpu_search_config['slash_code'];
	$partsRoot = $DP_Config->chpu_search_config['level_1']['url'];
	$artUrlSeg = $artNorm !== '' ? $artNorm : $articleShow;
	$pageUrl = rtrim((string) $DP_Config->domain_path, '/')
		. rtrim((string) $lang_href, '/')
		. '/' . $partsRoot . '/'
		. rawurlencode(str_replace('/', $slash, $mfr)) . '/'
		. rawurlencode(str_replace('/', $slash, $artUrlSeg));
	$offer = array(
		'@type' => 'Offer',
		'url' => $pageUrl,
		'availability' => ((int) ($row['exist'] ?? 0) > 0)
			? 'https://schema.org/InStock'
			: 'https://schema.org/OutOfStock',
		'itemCondition' => 'https://schema.org/NewCondition',
		'seller' => array(
			'@type' => 'Organization',
			'name' => epc_seo_site_display_name($DP_Config),
		),
		'shippingDetails' => array(
			'@type' => 'OfferShippingDetails',
			'shippingDestination' => epc_seo_schema_shipping_destinations(),
		),
		'areaServed' => epc_seo_schema_country_entries(),
	);
	if ($includePrice && (float) ($row['price'] ?? 0) > 0) {
		$offer['priceCurrency'] = $currencyCode;
		$offer['price'] = number_format((float) $row['price'], 2, '.', '');
	}
	$props = array(
		array('@type' => 'PropertyValue', 'name' => 'Part number', 'value' => ($articleShow !== '' ? $articleShow : $artNorm)),
		array('@type' => 'PropertyValue', 'name' => 'Article number', 'value' => $artNorm),
		array('@type' => 'PropertyValue', 'name' => 'Brand', 'value' => $mfr),
	);
	$related = array();
	foreach (array_slice($crossRefs, 0, 25) as $cr) {
		if (!is_array($cr)) {
			continue;
		}
		$cb = trim((string) ($cr['brand'] ?? ''));
		$ca = trim((string) ($cr['article'] ?? ''));
		if ($ca === '') {
			continue;
		}
		$props[] = array(
			'@type' => 'PropertyValue',
			'name' => 'Cross reference / OE',
			'value' => trim($cb . ' ' . $ca),
		);
		$related[] = array(
			'@type' => 'Product',
			'name' => trim($cb . ' ' . $ca),
			'sku' => docpart_normalize_article_for_price($ca),
			'mpn' => docpart_normalize_article_for_price($ca),
			'brand' => array('@type' => 'Brand', 'name' => $cb !== '' ? $cb : 'OE'),
		);
	}
	$schema = array(
		'@context' => 'https://schema.org',
		'@type' => 'Product',
		'name' => $name,
		'sku' => $artNorm !== '' ? $artNorm : $articleShow,
		'mpn' => $artNorm !== '' ? $artNorm : $articleShow,
		'productID' => $artNorm !== '' ? $artNorm : $articleShow,
		'brand' => array(
			'@type' => 'Brand',
			'name' => $mfr,
		),
		'additionalProperty' => $props,
		'areaServed' => epc_seo_schema_country_entries(),
		'offers' => $offer,
		'url' => $pageUrl,
	);
	if ($related !== array()) {
		$schema['isRelatedTo'] = $related;
	}
	return $schema;
}

/**
 * Organization / LocalBusiness JSON-LD for warehouse storefront (Dubai HQ, GCC + PK service).
 */
function epc_seo_build_organization_schema_array($DP_Config, $db_link = null): array
{
	$base = rtrim((string) $DP_Config->domain_path, '/');
	$tenantCountry = ($db_link instanceof PDO) ? epc_seo_tenant_country_code($db_link) : 'AE';
	return array(
		'@context' => 'https://schema.org',
		'@type' => array('Organization', 'AutoPartsStore'),
		'name' => epc_seo_site_display_name($DP_Config),
		'url' => $base,
		'address' => array(
			'@type' => 'PostalAddress',
			'addressLocality' => 'Dubai',
			'addressRegion' => 'Dubai',
			'addressCountry' => $tenantCountry,
		),
		'areaServed' => epc_seo_schema_country_entries(),
	);
}

function epc_seo_apply_warehouse_page_enrichment(&$DP_Content, $db_link, $DP_Config, $search_type, $manufacturer = '', $article = '')
{
	if (!epc_seo_warehouse_indexing_enabled($db_link)) {
		return;
	}
	$siteName = epc_seo_site_display_name($DP_Config);
	$mfr = html_entity_decode((string) $manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8');
	$art = html_entity_decode((string) $article, ENT_QUOTES | ENT_XML1, 'UTF-8');

	if ($search_type === 'manufacturer_browse' && $mfr !== '') {
		$brand = strtoupper(trim($mfr));
		$lang = epc_seo_current_lang_code();
		if ($lang === 'ar') {
			$DP_Content->title_tag = 'قطع غيار ' . $brand;
			$DP_Content->description_tag = $brand . ' — قطع غيار متوفرة. ' . epc_seo_regional_shipping_phrase('ar');
			$DP_Content->service_data['epc_seo_page_title'] = 'قطع غيار ' . $brand . ' | ' . $siteName;
		} elseif ($lang === 'ru') {
			$DP_Content->title_tag = $brand . ' — запчасти';
			$DP_Content->description_tag = $brand . ' — автозапчасти в наличии. ' . epc_seo_regional_shipping_phrase('ru');
			$DP_Content->service_data['epc_seo_page_title'] = $brand . ' — запчасти | ' . $siteName;
		} else {
			$DP_Content->title_tag = $brand . ' spare parts';
			$DP_Content->description_tag = $brand . ' auto parts in stock. ' . epc_seo_regional_shipping_phrase('en');
			$DP_Content->service_data['epc_seo_page_title'] = $brand . ' spare parts | ' . $siteName;
		}
		return;
	}

	if ($search_type !== 'prices_by_article_and_manufacturer' || $mfr === '' || $art === '') {
		return;
	}
	if (stripos((string) $DP_Content->robots_tag, 'noindex') !== false) {
		return;
	}
	$row = epc_seo_fetch_part_row($db_link, $mfr, $art);
	if ($row === null) {
		return;
	}
	$articleShow = trim((string) ($row['article_show'] ?? $row['article'] ?? $art));
	$DP_Content->title_tag = strtoupper(trim((string) $row['manufacturer'])) . ' ' . $articleShow . ' — Part number ' . $articleShow;
	$DP_Content->description_tag = epc_seo_format_part_description($row);
	$DP_Content->keywords_tag = implode(', ', array_filter(array(
		$articleShow,
		strtoupper(trim((string) $row['manufacturer'])) . ' ' . $articleShow,
		'part number ' . $articleShow,
		'article ' . $articleShow,
		'spare parts',
		'auto parts UAE',
	)));
	$DP_Content->service_data['epc_seo_page_title'] = epc_seo_format_part_title($row['manufacturer'], $articleShow, $siteName);
	$DP_Content->service_data['epc_seo_og'] = array(
		'title' => $DP_Content->service_data['epc_seo_page_title'],
		'description' => $DP_Content->description_tag,
		'type' => 'product',
	);
}

function epc_seo_hreflang_loc($base, $lang, $path): string
{
	$prefix = '/' . $lang;
	return $base . $prefix . preg_replace('#^/(en|ru|ar)(/|$)#', '/', $path);
}

function epc_seo_hreflang_links($DP_Config, $pathWithoutQuery = ''): string
{
	global $multilang_params;
	if (empty($multilang_params['multilang'])) {
		return '';
	}
	$path = (string) $pathWithoutQuery;
	if ($path === '' && isset($_SERVER['REQUEST_URI'])) {
		$path = (string) strtok((string) $_SERVER['REQUEST_URI'], '?');
	}
	// CP language is lang_cp — never emit /{lang}/cp/ alternates (they 404 on tenant hosts).
	if (function_exists('epc_portal_is_cp_request') && epc_portal_is_cp_request()) {
		return '';
	}
	$base = rtrim((string) $DP_Config->domain_path, '/');
	$langs = array('en', 'ar', 'ru');
	$out = '';
	$default = '';
	foreach ($langs as $lang) {
		$loc = epc_seo_hreflang_loc($base, $lang, $path);
		if ($lang === 'en') {
			$default = $loc;
		}
		$out .= '<link rel="alternate" hreflang="' . htmlspecialchars($lang, ENT_QUOTES, 'UTF-8') . '" href="' . htmlspecialchars($loc, ENT_QUOTES, 'UTF-8') . '"/>' . "\n";
	}
	$regional = array(
		'en-AE' => 'en',
		'ar-AE' => 'ar',
		'en-SA' => 'en',
		'en-OM' => 'en',
		'en-PK' => 'en',
	);
	foreach ($regional as $hreflang => $langKey) {
		$loc = epc_seo_hreflang_loc($base, $langKey, $path);
		$out .= '<link rel="alternate" hreflang="' . htmlspecialchars($hreflang, ENT_QUOTES, 'UTF-8') . '" href="' . htmlspecialchars($loc, ENT_QUOTES, 'UTF-8') . '"/>' . "\n";
	}
	if ($default !== '') {
		$out .= '<link rel="alternate" hreflang="x-default" href="' . htmlspecialchars($default, ENT_QUOTES, 'UTF-8') . '"/>' . "\n";
	}
	return $out;
}

function epc_seo_geo_meta_html($DP_Config, $db_link = null): string
{
	if (!epc_seo_is_ecomae_marketing_host()) {
		$tenantCountry = ($db_link instanceof PDO) ? epc_seo_tenant_country_code($db_link) : 'AE';
		return '<meta name="geo.region" content="' . htmlspecialchars($tenantCountry, ENT_QUOTES, 'UTF-8') . '-DU">' . "\n"
			. '<meta name="geo.placename" content="Dubai, United Arab Emirates">' . "\n"
			. '<meta name="geo.position" content="25.2048;55.2708">' . "\n"
			. '<meta name="ICBM" content="25.2048, 55.2708">' . "\n";
	}
	return '';
}

function epc_seo_head_extras_html($DP_Content, $DP_Config): string
{
	if (!is_object($DP_Content) || !is_object($DP_Config)) {
		return '';
	}
	$out = '';
	if (!empty($DP_Content->service_data['epc_seo_page_title'])) {
		$out .= '<meta property="og:title" content="' . htmlspecialchars((string) $DP_Content->service_data['epc_seo_page_title'], ENT_QUOTES, 'UTF-8') . '">' . "\n";
	}
	if (!empty($DP_Content->description_tag)) {
		$out .= '<meta property="og:description" content="' . htmlspecialchars((string) $DP_Content->description_tag, ENT_QUOTES, 'UTF-8') . '">' . "\n";
	}
	$canonical = !empty($DP_Content->service_data['epc_canonical_url'])
		? (string) $DP_Content->service_data['epc_canonical_url']
		: rtrim((string) $DP_Config->domain_path, '/') . (isset($_SERVER['REQUEST_URI']) ? strtok((string) $_SERVER['REQUEST_URI'], '?') : '/');
	$out .= '<meta property="og:url" content="' . htmlspecialchars($canonical, ENT_QUOTES, 'UTF-8') . '">' . "\n";
	$ogType = !empty($DP_Content->service_data['epc_seo_og']['type'])
		? (string) $DP_Content->service_data['epc_seo_og']['type']
		: 'website';
	$out .= '<meta property="og:type" content="' . htmlspecialchars($ogType, ENT_QUOTES, 'UTF-8') . '">' . "\n";
	global $db_link;
	if ($db_link instanceof PDO && epc_seo_warehouse_indexing_enabled($db_link)) {
		$out .= epc_seo_geo_meta_html($DP_Config, $db_link);
		$out .= epc_seo_hreflang_links($DP_Config);
		$orgSchema = epc_seo_build_organization_schema_array($DP_Config, $db_link);
		$out .= '<script type="application/ld+json">' . json_encode($orgSchema, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE) . '</script>' . "\n";
	}
	return $out;
}

/**
 * Lightweight regional shipping footer for parts search pages.
 */
function epc_seo_regional_footer_html($DP_Config): string
{
	global $db_link;
	if (!($db_link instanceof PDO) || !epc_seo_warehouse_indexing_enabled($db_link)) {
		return '';
	}
	$lang = epc_seo_current_lang_code();
	$langHref = epc_seo_lang_href();
	$shipUrl = rtrim((string) $DP_Config->domain_path, '/') . $langHref . '/shipping-export';
	$phrase = epc_seo_regional_shipping_phrase($lang);
	$linkLabel = ($lang === 'ar') ? 'تفاصيل الشحن والتصدير' : (($lang === 'ru') ? 'Доставка и экспорт' : 'Shipping & export details');
	$heading = ($lang === 'ar') ? 'التوصيل الإقليمي' : (($lang === 'ru') ? 'Региональная доставка' : 'Regional delivery');
	return '<aside class="epc-seo-regional-footer" style="margin:24px 0 8px;padding:16px 18px;border:1px solid #e8e8e8;border-radius:8px;background:#fafafa;font-size:14px;line-height:1.5;">'
		. '<strong>' . htmlspecialchars($heading, ENT_QUOTES, 'UTF-8') . '</strong>'
		. '<p style="margin:8px 0 0;">' . htmlspecialchars($phrase, ENT_QUOTES, 'UTF-8') . '</p>'
		. '<p style="margin:8px 0 0;"><a href="' . htmlspecialchars($shipUrl, ENT_QUOTES, 'UTF-8') . '">' . htmlspecialchars($linkLabel, ENT_QUOTES, 'UTF-8') . '</a></p>'
		. '</aside>';
}

/**
 * Set robots meta (and optional canonical) for CHPU part routes before template head renders.
 *
 * @param DP_Content $DP_Content
 * @param PDO        $db_link
 * @param DP_Config  $DP_Config
 * @param string     $search_type parts_index|parts_brands_hub|parts_all_hub|manufacturer_browse|all_brands_by_article|prices_by_article_and_manufacturer
 * @param string     $manufacturer
 * @param string     $article
 */
function epc_seo_apply_chpu_meta(&$DP_Content, $db_link, $DP_Config, $search_type, $manufacturer = '', $article = '')
{
	$lang_href = epc_seo_lang_href();
	$mfr = ($manufacturer !== null && $manufacturer !== '') ? (string) $manufacturer : '';
	$art = ($article !== null && $article !== '') ? (string) $article : '';
	$warehouseIndex = epc_seo_warehouse_indexing_enabled($db_link);

	switch ($search_type) {
		case 'parts_index':
			if ($warehouseIndex) {
				$DP_Content->robots_tag = 'index, follow';
			}
			break;

		case 'parts_brands_hub':
		case 'parts_all_hub':
			// Duplicate picker hubs — keep out of index (also disallowed in robots.txt).
			$DP_Content->robots_tag = 'noindex, follow';
			break;

		case 'manufacturer_browse':
			if (epc_seo_brand_has_stock($db_link, $mfr)) {
				if ($warehouseIndex) {
					$DP_Content->robots_tag = 'index, follow';
				}
			} else {
				$DP_Content->robots_tag = 'noindex, follow';
			}
			break;

		case 'all_brands_by_article':
			$DP_Content->robots_tag = 'noindex, follow';
			$canonical = epc_seo_primary_brand_article_url($db_link, $DP_Config, $lang_href, $art);
			if ($canonical !== '') {
				$DP_Content->service_data['epc_canonical_url'] = $canonical;
			}
			break;

		case 'prices_by_article_and_manufacturer':
			if ($mfr === '') {
				$DP_Content->robots_tag = 'noindex, follow';
				$canonical = epc_seo_primary_brand_article_url($db_link, $DP_Config, $lang_href, $art);
				if ($canonical !== '') {
					$DP_Content->service_data['epc_canonical_url'] = $canonical;
				}
			} elseif (epc_seo_brand_article_has_stock($db_link, $mfr, $art)) {
				if ($warehouseIndex) {
					$DP_Content->robots_tag = 'index, follow';
				}
			} else {
				$DP_Content->robots_tag = 'noindex, follow';
			}
			break;

		default:
			break;
	}
	epc_seo_apply_warehouse_page_enrichment($DP_Content, $db_link, $DP_Config, $search_type, $mfr, $art);
}
