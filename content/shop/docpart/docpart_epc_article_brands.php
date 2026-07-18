<?php
/**
 * Collect distinct brands for an article from CP crosses, Crossbase, and UMAPI.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/docpart_article_match.php');
require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/docpart_manufacturer_synonyms.php');
require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/epc_crossbase_cache.php');

define('EPC_ARTICLE_BRANDS_CROSSBASE_MAX', 800);

function epc_article_brands_fetch_url($url)
{
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
		curl_setopt($ch, CURLOPT_FOLLOWLOCATION, true);
		curl_setopt($ch, CURLOPT_CONNECTTIMEOUT, 8);
		curl_setopt($ch, CURLOPT_TIMEOUT, 20);
		curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
		curl_setopt($ch, CURLOPT_USERAGENT, 'ePartsCart article brands');
		$html = curl_exec($ch);
		curl_close($ch);
		return is_string($html) ? $html : '';
	}
	$context = stream_context_create(array(
		'http' => array(
			'timeout' => 20,
			'header' => "User-Agent: ePartsCart article brands\r\n",
		),
	));
	$html = @file_get_contents($url, false, $context);
	return is_string($html) ? $html : '';
}

function epc_article_brands_umapi_key($DP_Config)
{
	$key = '';
	if (!empty($DP_Config->umapi_api_key)) {
		$key = (string)$DP_Config->umapi_api_key;
	} elseif (!empty($DP_Config->umapi_api_url)) {
		$key = (string)$DP_Config->umapi_api_url;
	}
	if (strpos($key, '/') !== false) {
		$parts = explode('/', rtrim($key, '/'));
		$key = end($parts);
	}
	return trim($key);
}

function epc_article_brands_crossbase_brand($number, $text)
{
	$text = trim(html_entity_decode(strip_tags((string)$text), ENT_QUOTES | ENT_HTML5, 'UTF-8'));
	if ($text === '') {
		return '';
	}
	$brand = trim(preg_replace('~\s+'.preg_quote($number, '~').'\s*$~iu', '', $text));
	if ($brand === '') {
		$brand = trim(preg_replace('~\s+'.preg_quote(docpart_normalize_article_for_price($number), '~').'\s*$~iu', '', $text));
	}
	return $brand;
}

function epc_article_brands_add(&$brands, &$seen, $brand, $name, $source)
{
	$brand = trim((string)$brand);
	if ($brand === '' || mb_strlen($brand, 'UTF-8') < 2) {
		return false;
	}
	$brand_show = mb_strtoupper($brand, 'UTF-8');
	$key = $brand_show;
	if (isset($seen[$key])) {
		if ($name !== '' && (empty($brands[$key]['name']) || $brands[$key]['name'] === 'Name not specified by the supplier')) {
			$brands[$key]['name'] = $name;
		}
		if (!in_array($source, $brands[$key]['sources'], true)) {
			$brands[$key]['sources'][] = $source;
		}
		return false;
	}
	$seen[$key] = true;
	$brands[$key] = array(
		'manufacturer' => $brand,
		'manufacturer_show' => $brand_show,
		'name' => ($name !== '') ? $name : 'Name not specified by the supplier',
		'sources' => array($source),
	);
	return true;
}

function epc_article_brands_from_local_crosses($db_link, $DP_Config, $article_norm, &$brands, &$seen)
{
	$count = 0;
	if (!isset($DP_Config->local_crosses) || empty($DP_Config->local_crosses)) {
		return 0;
	}
	try {
		if (function_exists('docpart_analogs_match_exprs')) {
			list($art_expr, $analog_expr) = docpart_analogs_match_exprs($db_link);
		} else {
			$art_expr = docpart_sql_article_normalized_expr('`article`');
			$analog_expr = docpart_sql_article_normalized_expr('`analog`');
		}
		$cross_query = $db_link->prepare(
			'SELECT `article`, `manufacturer_article`, `analog`, `manufacturer_analog` '
			.'FROM `shop_docpart_articles_analogs_list` '
			.'WHERE '.$art_expr.' = ? OR '.$analog_expr.' = ? '
			.'ORDER BY `manufacturer_article`, `article`, `manufacturer_analog`, `analog` '
			.'LIMIT 3000;'
		);
		$cross_query->execute(array($article_norm, $article_norm));
		while ($row = $cross_query->fetch(PDO::FETCH_ASSOC)) {
			$row_article_norm = docpart_normalize_article_for_price($row['article']);
			$row_analog_norm = docpart_normalize_article_for_price($row['analog']);
			if ($row_article_norm === $article_norm) {
				if (epc_article_brands_add($brands, $seen, $row['manufacturer_article'], '', 'cp_crosses')) {
					$count++;
				}
			}
			if ($row_analog_norm === $article_norm) {
				if (epc_article_brands_add($brands, $seen, $row['manufacturer_analog'], '', 'cp_crosses')) {
					$count++;
				}
			}
		}
	} catch (Exception $e) {
		return $count;
	}
	return $count;
}

function epc_article_brands_from_crossbase($article_input, $article_norm, &$brands, &$seen)
{
	$added = 0;
	$html = epc_crossbase_cache_read($article_input, 6 * 3600, false);
	if ($html === '') {
		$html = epc_article_brands_fetch_url('https://crossbase.ru/cross/?q='.rawurlencode($article_input));
		if ($html !== '') {
			epc_crossbase_cache_write($article_input, $html);
		} else {
			$html = epc_crossbase_cache_read($article_input, 0, true);
		}
	}
	if ($html === '') {
		return 0;
	}
	$patterns = array(
		'~<tr>\s*<td[^>]*>\s*[0-9]+\s*</td>\s*<td[^>]*>\s*<a[^>]*href=["\']/cross/\?q=([^"\']+)["\'][^>]*>(.*?)</a>~isu',
		'~<a\s+[^>]*href=["\']/cross/\?q=([^"\']+)["\'][^>]*>(.*?)</a>~isu',
		'~<a\s+[^>]*href=["\']/cross/\?q=([^"\']+)["\']~isu',
	);
	foreach ($patterns as $pattern) {
		if (!preg_match_all($pattern, $html, $matches, PREG_SET_ORDER)) {
			continue;
		}
		foreach ($matches as $match) {
			$number = trim(urldecode($match[1]));
			$number_norm = docpart_normalize_article_for_price($number);
			if ($number_norm === '' || $number_norm !== $article_norm) {
				continue;
			}
			$brand = '';
			if (isset($match[2])) {
				$brand = epc_article_brands_crossbase_brand($number, $match[2]);
			}
			if (epc_article_brands_add($brands, $seen, $brand, '', 'crossbase')) {
				$added++;
			}
			if ($added >= EPC_ARTICLE_BRANDS_CROSSBASE_MAX) {
				break 2;
			}
		}
		if ($added > 0) {
			break;
		}
	}
	return $added;
}

function epc_article_brands_from_umapi($DP_Config, $article_input, &$brands, &$seen)
{
	$key = epc_article_brands_umapi_key($DP_Config);
	if ($key === '') {
		return 0;
	}
	$url = 'https://api.umapi.ru/BrandRefinement/'.rawurlencode($article_input);
	$headers = array(
		'Accept: application/json',
		'X-App-Key: '.$key,
	);
	$body = '';
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_CONNECTTIMEOUT => 8,
			CURLOPT_TIMEOUT => 20,
			CURLOPT_HTTPHEADER => $headers,
			CURLOPT_SSL_VERIFYPEER => true,
			CURLOPT_SSL_VERIFYHOST => 2,
		));
		$body = curl_exec($ch);
		curl_close($ch);
	} else {
		$context = stream_context_create(array(
			'http' => array(
				'method' => 'GET',
				'header' => implode("\r\n", $headers),
				'timeout' => 20,
			),
		));
		$body = @file_get_contents($url, false, $context);
	}
	if (!is_string($body) || $body === '') {
		return 0;
	}
	$data = json_decode($body, true);
	if (!is_array($data)) {
		return 0;
	}
	$rows = isset($data['data']) && is_array($data['data']) ? $data['data'] : $data;
	if (!is_array($rows)) {
		return 0;
	}
	$added = 0;
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$brand = '';
		if (!empty($row['BRAND'])) {
			$brand = $row['BRAND'];
		} elseif (!empty($row['SUP_BRAND'])) {
			$brand = $row['SUP_BRAND'];
		} elseif (!empty($row['MANUFACTURER'])) {
			$brand = $row['MANUFACTURER'];
		}
		$name = '';
		if (!empty($row['TITLE'])) {
			$name = $row['TITLE'];
		} elseif (!empty($row['DES'])) {
			$name = $row['DES'];
		}
		if (epc_article_brands_add($brands, $seen, $brand, $name, 'umapi')) {
			$added++;
		}
	}
	return $added;
}

function epc_article_brands_apply_synonyms($db_link, &$brands)
{
	$canonical_map = docpart_load_manufacturer_canonical_map($db_link);
	foreach ($brands as &$brand_row) {
		$canonical = docpart_synonym_canonical_brand($brand_row['manufacturer'], $canonical_map);
		if ($canonical !== '') {
			$brand_row['manufacturer_show'] = $canonical;
		}
	}
	unset($brand_row);

	// Merge synonym rows (AISINC → AISIN) into one picker brand.
	$merged = array();
	$seen_show = array();
	foreach ($brands as $brand_row) {
		$show = mb_strtoupper(trim((string) ($brand_row['manufacturer_show'] ?? '')), 'UTF-8');
		if ($show === '') {
			$show = mb_strtoupper(trim((string) ($brand_row['manufacturer'] ?? '')), 'UTF-8');
		}
		if ($show === '') {
			continue;
		}
		if (isset($seen_show[$show])) {
			$idx = $seen_show[$show];
			if (!empty($brand_row['name'])
				&& ($merged[$idx]['name'] === '' || $merged[$idx]['name'] === 'Name not specified by the supplier')
			) {
				$merged[$idx]['name'] = $brand_row['name'];
			}
			foreach ((array) ($brand_row['sources'] ?? array()) as $source) {
				if (!in_array($source, $merged[$idx]['sources'], true)) {
					$merged[$idx]['sources'][] = $source;
				}
			}
			continue;
		}
		$seen_show[$show] = count($merged);
		$brand_row['manufacturer_show'] = $show;
		$merged[] = $brand_row;
	}
	$brands = $merged;
}

function epc_collect_article_catalog_brands($db_link, $DP_Config, $article_input)
{
	$article_norm = docpart_normalize_article_for_price($article_input);
	if ($article_norm === '') {
		return array(
			'status' => false,
			'message' => 'Empty article',
			'manufacturers' => array(),
		);
	}
	$brands = array();
	$seen = array();
	$warehouse_count = 0;
	// Local price warehouses first — these are the ePartsCart stock brands customers should pick.
	try {
		$warehouse_brands = epc_chpu_distinct_warehouse_brands_for_article($db_link, $DP_Config, $article_input);
		foreach ($warehouse_brands as $warehouse_brand) {
			if (epc_article_brands_add($brands, $seen, $warehouse_brand, '', 'warehouse')) {
				$warehouse_count++;
			}
		}
	} catch (Throwable $e) {
		$warehouse_count = 0;
	}
	$cp_count = epc_article_brands_from_local_crosses($db_link, $DP_Config, $article_norm, $brands, $seen);
	$crossbase_count = epc_article_brands_from_crossbase($article_input, $article_norm, $brands, $seen);
	$umapi_count = epc_article_brands_from_umapi($DP_Config, $article_input, $brands, $seen);
	epc_article_brands_apply_synonyms($db_link, $brands);
	$manufacturers = array();
	foreach ($brands as $brand_row) {
		$source_type = in_array('warehouse', $brand_row['sources'], true) ? 'prices' : 'catalog';
		$manufacturers[] = array(
			'manufacturer' => $brand_row['manufacturer'],
			'manufacturer_show' => $brand_row['manufacturer_show'],
			'manufacturer_id' => 0,
			'name' => $brand_row['name'],
			'storage_id' => 0,
			'office_id' => 0,
			'synonyms_single_query' => true,
			'params' => array(
				'type' => $source_type,
				'sources' => $brand_row['sources'],
			),
		);
	}
	usort($manufacturers, function ($a, $b) {
		return strcmp($a['manufacturer_show'], $b['manufacturer_show']);
	});
	return array(
		'status' => true,
		'article' => $article_norm,
		'warehouse_count' => $warehouse_count,
		'cp_crosses_count' => $cp_count,
		'crossbase_count' => $crossbase_count,
		'umapi_count' => $umapi_count,
		'manufacturers' => $manufacturers,
	);
}
