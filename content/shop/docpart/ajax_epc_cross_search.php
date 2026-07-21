<?php

header('Content-Type: application/json;charset=utf-8;');
header('X-Robots-Tag: noindex, nofollow, noarchive');



require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");

require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_article_match.php");
$docpart_cross_interchange_path = $_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_cross_interchange.php";
if(is_file($docpart_cross_interchange_path))
{
	require_once($docpart_cross_interchange_path);
}
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_manufacturer_synonyms.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/epc_crossbase_cache.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/epc_storefront_anti_crawl.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/epc_storefront_prices_helpers.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");



$DP_Config = new DP_Config;
$GLOBALS['DP_Config'] = $DP_Config;

// Anti-crawl before any stock work (bots + rate limit). tech_key+cp_bulk still allowed.
$epc_cross_anti_crawl = epc_storefront_anti_crawl_enforce($DP_Config, array(
	'bucket' => 'cross_search',
	'guest_max' => 20,
	'user_max' => 80,
	'window' => 60,
));

$article_input = isset($_GET['article']) ? trim((string)$_GET['article']) : '';

$anchor_brand = isset($_GET['brand']) ? trim((string)$_GET['brand']) : '';
if ($anchor_brand === '' && isset($_GET['manufacturer'])) {
	$anchor_brand = trim((string)$_GET['manufacturer']);
}

$article_norm = docpart_normalize_article_for_price($article_input);



define('EPC_CROSS_LOCAL_MAX', 5000);

define('EPC_CROSS_CROSSBASE_MAX', 2500);

define('EPC_CROSS_STOCK_BATCH', 400);

define('EPC_CROSS_STOCK_MAX', 2000);



function epc_cross_json($payload)

{

	if(!empty($payload['status']))

	{

		header('Cache-Control: private, max-age=120');

	}

	echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);

	exit;

}



if($article_norm == '')

{

	epc_cross_json(array('status' => false, 'message' => 'Empty article', 'references' => array(), 'stock' => array()));

}



try

{

	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);

	$db_link->query("SET NAMES utf8;");

}

catch(Exception $e)

{

	epc_cross_json(array('status' => false, 'message' => 'Database unavailable', 'references' => array(), 'stock' => array()));

}



function epc_fetch_url($url, $timeout_seconds = 20)

{

	$timeout_seconds = max(3, min(30, (int)$timeout_seconds));

	if(function_exists('curl_init'))

	{

		$ch = curl_init($url);

		curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);

		curl_setopt($ch, CURLOPT_FOLLOWLOCATION, true);

		curl_setopt($ch, CURLOPT_CONNECTTIMEOUT, min(6, $timeout_seconds));

		curl_setopt($ch, CURLOPT_TIMEOUT, $timeout_seconds);

		curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);

		curl_setopt($ch, CURLOPT_USERAGENT, 'ePartsCart cross search');

		$html = curl_exec($ch);

		curl_close($ch);

		return is_string($html) ? $html : '';

	}

	$context = stream_context_create(array('http' => array('timeout' => $timeout_seconds, 'header' => "User-Agent: ePartsCart cross search\r\n")));

	$html = @file_get_contents($url, false, $context);

	return is_string($html) ? $html : '';

}



function epc_cross_fetch_crossbase_html($article_input, $timeout_seconds = 20)

{

	$article_input = trim((string)$article_input);

	$cached = epc_crossbase_cache_read($article_input, 6 * 3600, false);

	if($cached !== '')

	{

		return $cached;

	}

	$html = epc_fetch_url('https://crossbase.ru/cross/?q='.rawurlencode($article_input), $timeout_seconds);

	if($html !== '' && strlen($html) > 400)

	{

		epc_crossbase_cache_write($article_input, $html);

		return $html;

	}

	$stale = epc_crossbase_cache_read($article_input, 0, true);

	return $stale !== '' ? $stale : $html;

}



function epc_cross_count_refs_by_source($references, $source)

{

	$count = 0;

	foreach($references as $ref)

	{

		if(isset($ref['source']) && (string)$ref['source'] === (string)$source)

		{

			$count++;

		}

	}

	return $count;

}



function epc_cross_merge_source_into_ref(&$ref, $source)
{
	$source = trim((string)$source);
	if($source === '')
	{
		return;
	}
	$existing = isset($ref['source']) ? trim((string)$ref['source']) : '';
	if($existing === '')
	{
		$ref['source'] = $source;
		return;
	}
	$parts = preg_split('~\s*\+\s*~', $existing);
	if(!is_array($parts))
	{
		$parts = array($existing);
	}
	foreach($parts as $part)
	{
		if(strcasecmp(trim((string)$part), $source) === 0)
		{
			return;
		}
	}
	$ref['source'] = $existing.'+'.$source;
}

function epc_cross_merge_reference_rows($a, $b)
{
	if(!is_array($a))
	{
		return is_array($b) ? $b : array();
	}
	if(!is_array($b))
	{
		return $a;
	}
	$brand_a = isset($a['brand']) ? trim((string)$a['brand']) : '';
	$brand_b = isset($b['brand']) ? trim((string)$b['brand']) : '';
	if($brand_a === '' && $brand_b !== '')
	{
		$a['brand'] = $brand_b;
	}
	$art_a = isset($a['article']) ? trim((string)$a['article']) : '';
	$art_b = isset($b['article']) ? trim((string)$b['article']) : '';
	if($art_a === '' && $art_b !== '')
	{
		$a['article'] = $art_b;
	}
	elseif($art_b !== '' && strlen($art_b) > strlen($art_a))
	{
		$a['article'] = $art_b;
	}
	$norm_a = !empty($a['article_norm']) ? docpart_normalize_article_for_price($a['article_norm']) : docpart_normalize_article_for_price($art_a);
	$norm_b = !empty($b['article_norm']) ? docpart_normalize_article_for_price($b['article_norm']) : docpart_normalize_article_for_price($art_b);
	if($norm_a === '' && $norm_b !== '')
	{
		$a['article_norm'] = $norm_b;
	}
	elseif($norm_a !== '')
	{
		$a['article_norm'] = $norm_a;
	}
	$name_a = isset($a['name']) ? trim((string)$a['name']) : '';
	$name_b = isset($b['name']) ? trim((string)$b['name']) : '';
	if($name_a === '' && $name_b !== '')
	{
		$a['name'] = $name_b;
	}
	elseif($name_b !== '' && strlen($name_b) > strlen($name_a))
	{
		$a['name'] = $name_b;
	}
	if(!empty($b['source']))
	{
		epc_cross_merge_source_into_ref($a, $b['source']);
	}
	return $a;
}

function epc_cross_add_reference(&$references, &$seen, $brand, $article, $source = '')

{

	$article = trim((string)$article);

	$article_norm = docpart_normalize_article_for_price($article);

	$brand = trim((string)$brand);

	if($article_norm == '')

	{

		return false;

	}

	$key = mb_strtoupper($brand.'|'.$article_norm, 'UTF-8');

	if(isset($seen[$key]))

	{

		foreach($references as &$ref)

		{

			$ref_norm = !empty($ref['article_norm'])

				? docpart_normalize_article_for_price($ref['article_norm'])

				: docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');

			$ref_brand = isset($ref['brand']) ? trim((string)$ref['brand']) : '';

			if($ref_norm === $article_norm && mb_strtoupper($ref_brand.'|'.$ref_norm, 'UTF-8') === $key)

			{

				epc_cross_merge_source_into_ref($ref, $source);

				break;

			}

		}

		unset($ref);

		return false;

	}

	// Empty brand: attach to any existing row with the same article_norm.
	if($brand === '')

	{

		foreach($references as &$ref)

		{

			$ref_norm = !empty($ref['article_norm'])

				? docpart_normalize_article_for_price($ref['article_norm'])

				: docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');

			if($ref_norm !== $article_norm)

			{

				continue;

			}

			epc_cross_merge_source_into_ref($ref, $source);

			unset($ref);

			return false;

		}

		unset($ref);

	}

	else

	{

		// Promote a previously empty-brand row for the same article_norm.
		$empty_key = mb_strtoupper('|'.$article_norm, 'UTF-8');

		if(isset($seen[$empty_key]))

		{

			foreach($references as &$ref)

			{

				$ref_norm = !empty($ref['article_norm'])

					? docpart_normalize_article_for_price($ref['article_norm'])

					: docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');

				$ref_brand = isset($ref['brand']) ? trim((string)$ref['brand']) : '';

				if($ref_norm !== $article_norm || $ref_brand !== '')

				{

					continue;

				}

				$ref['brand'] = $brand;

				if($article !== '')

				{

					$ref['article'] = $article;

				}

				epc_cross_merge_source_into_ref($ref, $source);

				unset($seen[$empty_key]);

				$seen[$key] = true;

				unset($ref);

				return false;

			}

			unset($ref);

		}

	}

	$seen[$key] = true;

	$row = array(

		'brand' => $brand,

		'article' => $article,

		'article_norm' => $article_norm,

	);

	if($source !== '')

	{

		$row['source'] = $source;

	}

	$references[] = $row;

	return true;

}



function epc_cross_brand_from_crossbase_text($number, $text)

{

	$text = trim(html_entity_decode(strip_tags((string)$text), ENT_QUOTES | ENT_HTML5, 'UTF-8'));

	if($text === '')

	{

		return '';

	}

	$brand = trim(preg_replace('~\s+'.preg_quote($number, '~').'\s*$~iu', '', $text));

	if($brand === '')

	{

		$brand = trim(preg_replace('~\s+'.preg_quote(docpart_normalize_article_for_price($number), '~').'\s*$~iu', '', $text));

	}

	return $brand;

}

function epc_cross_infer_brand_from_article_norm($article_norm)
{
	$article_norm = docpart_normalize_article_for_price($article_norm);
	if($article_norm === '')
	{
		return '';
	}
	if(preg_match('/^90915/i', $article_norm))
	{
		return 'TOYOTA';
	}
	if(preg_match('/^15400/i', $article_norm))
	{
		return 'HONDA';
	}
	if(preg_match('/^15208|^22040/i', $article_norm))
	{
		return 'NISSAN';
	}
	if(preg_match('/^26300|^28113/i', $article_norm))
	{
		return 'HYUNDAI';
	}
	if(preg_match('/^06[A-Z0-9]|^1K0|^5W0|^8E0/i', $article_norm))
	{
		return 'VAG';
	}
	if(preg_match('/^A000|^A[0-9]{9,10}$/i', $article_norm))
	{
		return 'MERCEDES-BENZ';
	}
	if(preg_match('/^B6Y1|^PE01|^LF05/i', $article_norm))
	{
		return 'MAZDA';
	}
	return '';
}

function epc_cross_brand_from_crossbase_row($number, $row_html)
{
	$number_norm = docpart_normalize_article_for_price($number);
	if($number_norm === '')
	{
		return '';
	}
	if(preg_match_all('~href=["\']/cross/\?q=([^"\']+)["\'][^>]*>([^<]+)</a>~iu', $row_html, $link_matches, PREG_SET_ORDER))
	{
		foreach($link_matches as $link_match)
		{
			$link_number = trim(urldecode($link_match[1]));
			if(docpart_normalize_article_for_price($link_number) !== $number_norm)
			{
				continue;
			}
			$brand = epc_cross_brand_from_crossbase_text($number, $link_match[2]);
			if($brand !== '')
			{
				return $brand;
			}
		}
	}
	$plain = trim(preg_replace('~\s+~u', ' ', html_entity_decode(strip_tags($row_html), ENT_QUOTES | ENT_HTML5, 'UTF-8')));
	if($plain !== '')
	{
		if(preg_match('~(.+?)\s+'.preg_quote($number, '~').'\s*$~iu', $plain, $tail_match))
		{
			$brand = trim($tail_match[1]);
			if($brand !== '' && docpart_normalize_article_for_price($brand) !== $number_norm)
			{
				return $brand;
			}
		}
	}
	$inferred = epc_cross_infer_brand_from_article_norm($number_norm);
	if($inferred !== '')
	{
		return $inferred;
	}
	return '';
}

function epc_cross_enrich_reference_brands(&$references, $anchor_brand = '')
{
	$brands_by_article = array();
	foreach($references as $ref)
	{
		$norm = isset($ref['article_norm']) ? docpart_normalize_article_for_price($ref['article_norm']) : docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');
		$brand = isset($ref['brand']) ? trim((string)$ref['brand']) : '';
		if($norm === '' || $brand === '')
		{
			continue;
		}
		$brand_key = mb_strtoupper($brand, 'UTF-8');
		if(!isset($brands_by_article[$norm]))
		{
			$brands_by_article[$norm] = array();
		}
		$brands_by_article[$norm][$brand_key] = $brand;
	}
	foreach($references as &$ref)
	{
		$brand = isset($ref['brand']) ? trim((string)$ref['brand']) : '';
		if($brand !== '')
		{
			continue;
		}
		$norm = isset($ref['article_norm']) ? docpart_normalize_article_for_price($ref['article_norm']) : docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');
		if($norm === '')
		{
			continue;
		}
		$inferred = epc_cross_infer_brand_from_article_norm($norm);
		if($inferred !== '')
		{
			$source_name = isset($ref['source']) ? (string)$ref['source'] : '';
			$from_crossbase = ($source_name === 'crossbase' || $source_name === 'crossbase_oem');
			$oem_article = (bool)preg_match('/^90915/i', $norm);
			if($brand === '' || ($from_crossbase && $oem_article))
			{
				$ref['brand'] = $inferred;
				continue;
			}
		}
		if($brand === '' && isset($brands_by_article[$norm]) && count($brands_by_article[$norm]) === 1)
		{
			$ref['brand'] = reset($brands_by_article[$norm]);
		}
		if($brand === '' && function_exists('docpart_cross_resolve_brand_for_article'))
		{
			global $db_link;
			if(isset($db_link) && $db_link instanceof PDO)
			{
				$resolved = docpart_cross_resolve_brand_for_article($db_link, isset($ref['article']) ? $ref['article'] : '', array(
					'fallback_brand' => trim((string)$anchor_brand),
				));
				if($resolved !== '')
				{
					$ref['brand'] = $resolved;
				}
			}
		}
	}
	unset($ref);
}

function epc_cross_enrich_reference_names(&$references, $stock, $db_link, $synonym_map, $anchor_brand = '', $anchor_article = '')
{
	if(!is_array($references) || !count($references))
	{
		return;
	}

	$name_by_pair = array();
	foreach((array)$stock as $item)
	{
		$name = trim((string)(isset($item['name']) ? $item['name'] : ''));
		if($name === '')
		{
			continue;
		}
		$article_norm = docpart_normalize_article_for_price(isset($item['article_norm']) ? $item['article_norm'] : (isset($item['article']) ? $item['article'] : ''));
		if($article_norm === '')
		{
			continue;
		}
		$brand_names = docpart_synonym_names_for_brand(isset($item['brand']) ? $item['brand'] : '', $synonym_map);
		foreach($brand_names as $brand_name)
		{
			$key = epc_cross_reference_pair_key($brand_name, $article_norm);
			if($key !== '' && !isset($name_by_pair[$key]))
			{
				$name_by_pair[$key] = $name;
			}
		}
		if(!isset($name_by_pair['|'.$article_norm]))
		{
			$name_by_pair['|'.$article_norm] = $name;
		}
	}

	foreach($references as &$ref)
	{
		if(trim((string)(isset($ref['name']) ? $ref['name'] : '')) !== '')
		{
			continue;
		}
		$article_norm = !empty($ref['article_norm']) ? $ref['article_norm'] : docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');
		if($article_norm === '')
		{
			continue;
		}
		$brand_names = docpart_synonym_names_for_brand(isset($ref['brand']) ? $ref['brand'] : '', $synonym_map);
		foreach($brand_names as $brand_name)
		{
			$key = epc_cross_reference_pair_key($brand_name, $article_norm);
			if($key !== '' && isset($name_by_pair[$key]))
			{
				$ref['name'] = $name_by_pair[$key];
				continue 2;
			}
		}
		if(isset($name_by_pair['|'.$article_norm]))
		{
			$ref['name'] = $name_by_pair['|'.$article_norm];
		}
	}
	unset($ref);

	$missing_norms = array();
	foreach($references as $ref)
	{
		if(trim((string)(isset($ref['name']) ? $ref['name'] : '')) !== '')
		{
			continue;
		}
		$article_norm = !empty($ref['article_norm']) ? $ref['article_norm'] : docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');
		if($article_norm !== '')
		{
			$missing_norms[$article_norm] = true;
		}
	}
	if(!count($missing_norms))
	{
		return;
	}

	$art_expr = docpart_sql_article_normalized_expr('`article`');
	$norm_list = array_keys($missing_norms);
	if(count($norm_list) > EPC_CROSS_CROSSBASE_MAX)
	{
		$norm_list = array_slice($norm_list, 0, EPC_CROSS_CROSSBASE_MAX);
	}

	$db_name_by_pair = array();
	$db_name_by_norm = array();
	foreach(array_chunk($norm_list, 40) as $batch)
	{
		try
		{
			$article_placeholders = str_repeat('?,', count($batch) - 1) . '?';
			$name_query = $db_link->prepare(
				'SELECT `manufacturer`, `article`, TRIM(`name`) AS `name`, IFNULL(`exist`, 0) AS `exist` '
				.'FROM `shop_docpart_prices_data` '
				.'WHERE '.$art_expr.' IN ('.$article_placeholders.') '
				.'AND TRIM(IFNULL(`name`, \'\')) <> \'\' '
				.'ORDER BY IFNULL(`exist`, 0) DESC, LENGTH(`name`) DESC'
			);
			$name_query->execute($batch);
			while($row = $name_query->fetch(PDO::FETCH_ASSOC))
			{
				$name = trim((string)(isset($row['name']) ? $row['name'] : ''));
				if($name === '')
				{
					continue;
				}
				$product_norm = docpart_normalize_article_for_price(isset($row['article']) ? $row['article'] : '');
				if($product_norm === '')
				{
					continue;
				}
				$product_brand = trim((string)(isset($row['manufacturer']) ? $row['manufacturer'] : ''));
				$key = epc_cross_reference_pair_key($product_brand, $product_norm);
				if($key !== '' && !isset($db_name_by_pair[$key]))
				{
					$db_name_by_pair[$key] = $name;
				}
				if(!isset($db_name_by_norm[$product_norm]))
				{
					$db_name_by_norm[$product_norm] = $name;
				}
			}
		}
		catch(Exception $e)
		{
			continue;
		}
	}

	foreach($references as &$ref)
	{
		if(trim((string)(isset($ref['name']) ? $ref['name'] : '')) !== '')
		{
			continue;
		}
		$article_norm = !empty($ref['article_norm']) ? $ref['article_norm'] : docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');
		if($article_norm === '')
		{
			continue;
		}
		$brand_names = docpart_synonym_names_for_brand(isset($ref['brand']) ? $ref['brand'] : '', $synonym_map);
		foreach($brand_names as $brand_name)
		{
			$key = epc_cross_reference_pair_key($brand_name, $article_norm);
			if($key !== '' && isset($db_name_by_pair[$key]))
			{
				$ref['name'] = $db_name_by_pair[$key];
				continue 2;
			}
		}
		if(isset($db_name_by_norm[$article_norm]))
		{
			$ref['name'] = $db_name_by_norm[$article_norm];
		}
	}
	unset($ref);

	global $DP_Config;
	epc_cross_enrich_reference_names_umapi($references, $db_link, $DP_Config, $synonym_map, $anchor_brand, $anchor_article);

	foreach($references as &$ref)
	{
		$brand = isset($ref['brand']) ? trim((string)$ref['brand']) : '';
		$article = isset($ref['article']) ? trim((string)$ref['article']) : '';
		$description = trim((string)(isset($ref['name']) ? $ref['name'] : ''));
		$ref['name'] = epc_cross_format_reference_name($brand, $article, $description);
	}
	unset($ref);
}

function epc_cross_format_reference_name($brand, $article, $description = '')
{
	$brand = trim((string)$brand);
	$article = trim((string)$article);
	$description = trim((string)$description);
	$base = ($brand !== '' && $article !== '')
		? ($brand . ' ' . $article)
		: (($article !== '') ? $article : $brand);
	if($base === '')
	{
		return '';
	}
	if($description === '')
	{
		return $base;
	}
	$article_norm = docpart_normalize_article_for_price($article);
	$desc_norm = docpart_normalize_article_for_price($description);
	if($desc_norm !== '' && $desc_norm === $article_norm)
	{
		return $base;
	}
	$desc_upper = mb_strtoupper($description, 'UTF-8');
	$brand_upper = mb_strtoupper($brand, 'UTF-8');
	if($brand_upper !== '' && mb_strpos($desc_upper, $brand_upper) === 0)
	{
		return $description;
	}
	if($article_norm !== '' && mb_strpos($desc_upper, mb_strtoupper($article, 'UTF-8')) !== false && mb_strpos($desc_upper, $brand_upper) !== false)
	{
		return $description;
	}
	return $base . ' — ' . $description;
}

function epc_cross_umapi_key($DP_Config)
{
	$key = '';
	if(!empty($DP_Config->umapi_api_key))
	{
		$key = trim((string)$DP_Config->umapi_api_key);
	}
	elseif(!empty($DP_Config->umapi_api_url))
	{
		$key = trim((string)$DP_Config->umapi_api_url);
	}
	if(strpos($key, '/') !== false)
	{
		$parts = explode('/', rtrim($key, '/'));
		$key = end($parts);
	}
	return trim((string)$key);
}

function epc_cross_umapi_is_active($db_link, $DP_Config)
{
	if(epc_cross_umapi_key($DP_Config) === '')
	{
		return false;
	}
	try
	{
		$row = $db_link->query('SELECT `connected`, `last_success` FROM `epc_umapi_sync_status` WHERE `id` = 1 LIMIT 1;')->fetch(PDO::FETCH_ASSOC);
		if($row)
		{
			if((int)$row['connected'] === 1 || (int)$row['last_success'] > 0)
			{
				return true;
			}
		}
	}
	catch(Exception $e)
	{
		// Key exists — still try live lookup.
	}
	return true;
}

function epc_cross_umapi_fetch_json($url, $key, $timeout = 8)
{
	if($key === '' || !function_exists('curl_init'))
	{
		return null;
	}
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_CONNECTTIMEOUT => 4,
		CURLOPT_TIMEOUT => $timeout,
		CURLOPT_HTTPHEADER => array('Accept: application/json', 'X-App-Key: ' . $key),
		CURLOPT_FOLLOWLOCATION => false,
		CURLOPT_SSL_VERIFYPEER => true,
		CURLOPT_SSL_VERIFYHOST => 2,
	));
	$body = curl_exec($ch);
	curl_close($ch);
	if(!is_string($body) || trim($body) === '')
	{
		return null;
	}
	$decoded = json_decode($body, true);
	return is_array($decoded) ? $decoded : null;
}

function epc_cross_umapi_item_field(array $item, array $fields)
{
	foreach($fields as $field)
	{
		if(!empty($item[$field]) && trim((string)$item[$field]) !== '')
		{
			return trim((string)$item[$field]);
		}
	}
	return '';
}

function epc_cross_umapi_collect_name_map($payload, $synonym_map)
{
	$map = array();
	$items = array();
	if(isset($payload['data']) && is_array($payload['data']))
	{
		$items = $payload['data'];
	}
	elseif(is_array($payload))
	{
		$items = $payload;
	}
	foreach($items as $item)
	{
		if(!is_array($item))
		{
			continue;
		}
		$article = epc_cross_umapi_item_field($item, array('ART_ARTICLE_NR', 'ARTICLE_NR', 'ARTICLE', 'ART_NUMBER', 'OEN', 'OEM', 'NUMBER', 'DISPLAY_ARTICLE'));
		$brand = epc_cross_umapi_item_field($item, array('SUP_BRAND', 'BRAND', 'SUPPLIER', 'MANUFACTURER', 'BRAND_NAME'));
		$name = epc_cross_umapi_item_field($item, array('ART_PRODUCT_NAME', 'COMPLETE_DES', 'PRODUCT_NAME', 'DES', 'NAME', 'PRODUCT'));
		$article_norm = docpart_normalize_article_for_price($article);
		if($article_norm === '')
		{
			continue;
		}
		$brand_names = docpart_synonym_names_for_brand($brand, $synonym_map);
		if(empty($brand_names) && $brand !== '')
		{
			$brand_names = array($brand);
		}
		foreach($brand_names as $brand_name)
		{
			$key = epc_cross_reference_pair_key($brand_name, $article_norm);
			if($key === '')
			{
				continue;
			}
			if($name !== '' && !isset($map[$key]))
			{
				$map[$key] = $name;
			}
		}
	}
	return $map;
}

function epc_cross_umapi_cache_name_map($db_link, $anchor_brand, $anchor_article, $synonym_map)
{
	$map = array();
	$needles = array();
	if(trim((string)$anchor_article) !== '')
	{
		$needles[] = '%' . trim((string)$anchor_article) . '%';
	}
	if(trim((string)$anchor_brand) !== '')
	{
		$needles[] = '%' . trim((string)$anchor_brand) . '%';
	}
	if(empty($needles))
	{
		return $map;
	}
	try
	{
		$stmt = $db_link->prepare(
			'SELECT `response_json` FROM `epc_umapi_cache` '
			.'WHERE (`response_json` LIKE ? OR `response_json` LIKE ?) '
			.'ORDER BY `last_sync` DESC LIMIT 12'
		);
		$stmt->execute(array($needles[0], isset($needles[1]) ? $needles[1] : $needles[0]));
		while($row = $stmt->fetch(PDO::FETCH_ASSOC))
		{
			$payload = json_decode(isset($row['response_json']) ? $row['response_json'] : '', true);
			if(!is_array($payload))
			{
				continue;
			}
			$chunk = epc_cross_umapi_collect_name_map($payload, $synonym_map);
			foreach($chunk as $key => $name)
			{
				if(!isset($map[$key]))
				{
					$map[$key] = $name;
				}
			}
		}
	}
	catch(Exception $e)
	{
		return $map;
	}
	return $map;
}

function epc_cross_enrich_reference_names_umapi(&$references, $db_link, $DP_Config, $synonym_map, $anchor_brand, $anchor_article)
{
	if(!is_array($references) || !count($references) || !epc_cross_umapi_is_active($db_link, $DP_Config))
	{
		return;
	}
	$key = epc_cross_umapi_key($DP_Config);
	$name_map = epc_cross_umapi_cache_name_map($db_link, $anchor_brand, $anchor_article, $synonym_map);
	$anchor_brand = trim((string)$anchor_brand);
	$anchor_article = trim((string)$anchor_article);
	if($anchor_brand !== '' && $anchor_article !== '')
	{
		$url = 'https://api.umapi.ru/v2/autocatalog/en-WWW/Analogs/'
			. rawurlencode($anchor_article) . '/' . rawurlencode($anchor_brand) . '?limit=500';
		$payload = epc_cross_umapi_fetch_json($url, $key, 8);
		if(is_array($payload))
		{
			$live_map = epc_cross_umapi_collect_name_map($payload, $synonym_map);
			foreach($live_map as $pair_key => $name)
			{
				if(!isset($name_map[$pair_key]))
				{
					$name_map[$pair_key] = $name;
				}
			}
		}
	}
	if(empty($name_map))
	{
		return;
	}
	foreach($references as &$ref)
	{
		if(trim((string)(isset($ref['name']) ? $ref['name'] : '')) !== '')
		{
			continue;
		}
		$article_norm = !empty($ref['article_norm']) ? $ref['article_norm'] : docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');
		if($article_norm === '')
		{
			continue;
		}
		$brand_names = docpart_synonym_names_for_brand(isset($ref['brand']) ? $ref['brand'] : '', $synonym_map);
		foreach($brand_names as $brand_name)
		{
			$pair_key = epc_cross_reference_pair_key($brand_name, $article_norm);
			if($pair_key !== '' && isset($name_map[$pair_key]))
			{
				$ref['name'] = $name_map[$pair_key];
				$ref['name_source'] = 'umapi_catalog';
				continue 2;
			}
		}
	}
	unset($ref);
}

function epc_cross_is_aftermarket_article_norm($article_norm)

{

	$article_norm = docpart_normalize_article_for_price($article_norm);

	return ($article_norm !== '' && preg_match('/^[A-Z]{1,5}[0-9]{2,}[A-Z0-9]{0,4}$/i', $article_norm));

}



function epc_cross_load_local_aftermarket_for_oem($db_link, $article_norm, &$references, &$seen)

{

	$count = 0;

	if(strlen($article_norm) < 8)

	{

		return 0;

	}

	if (function_exists('docpart_analogs_host_load1')) {
		$load1 = docpart_analogs_host_load1();
		if ($load1 !== null && $load1 >= 5.0) {
			return 0;
		}
	}

	if (function_exists('docpart_analogs_match_exprs')) {
		list($art_expr, $analog_expr) = docpart_analogs_match_exprs($db_link);
	} else {
		$art_expr = docpart_sql_article_normalized_expr('`article`');
		$analog_expr = docpart_sql_article_normalized_expr('`analog`');
	}

	try

	{

		@$db_link->exec('SET SESSION max_statement_time = 2');
		@$db_link->exec('SET SESSION MAX_EXECUTION_TIME = 2000');
		$cross_query = $db_link->prepare(

			'SELECT `article`, `manufacturer_article`, `analog`, `manufacturer_analog` '

				.'FROM `shop_docpart_articles_analogs_list` '

				.'WHERE '.$analog_expr.' = ? OR '.$analog_expr.' LIKE ? '

				.'ORDER BY `id` DESC LIMIT 80'

		);

		$cross_query->execute(array($article_norm, $article_norm.'%'));

	}

	catch(Exception $e)

	{

		return 0;

	}

	while($row = $cross_query->fetch(PDO::FETCH_ASSOC))

	{

		$row_article_norm = docpart_normalize_article_for_price($row['article']);

		$row_analog_norm = docpart_normalize_article_for_price($row['analog']);

		$oem_on_article = ($row_article_norm === $article_norm || (strlen($article_norm) >= 8 && strpos($row_article_norm, $article_norm) === 0));

		$oem_on_analog = ($row_analog_norm === $article_norm || (strlen($article_norm) >= 8 && strpos($row_analog_norm, $article_norm) === 0));

		if(epc_cross_is_aftermarket_article_norm($row_article_norm) && $oem_on_analog)

		{

			if(epc_cross_add_reference($references, $seen, $row['manufacturer_article'], $row['article'], 'cp'))

			{

				$count++;

			}

		}

		elseif(epc_cross_is_aftermarket_article_norm($row_analog_norm) && $oem_on_article)

		{

			if(epc_cross_add_reference($references, $seen, $row['manufacturer_analog'], $row['analog'], 'cp'))

			{

				$count++;

			}

		}

		if($count >= 40)

		{

			break;

		}

	}

	return $count;

}



function epc_cross_load_local_references($db_link, $DP_Config, $article_norm, &$references, &$seen)

{

	$count = 0;

	$use_local = (isset($DP_Config->local_crosses) && !empty($DP_Config->local_crosses));

	if(!$use_local)

	{

		return 0;

	}

	// Protect mysqld CPU: local analogs REPLACE() walks are the main Hostinger CPU reset trigger.
	if (function_exists('docpart_analogs_host_load1')) {
		$load1 = docpart_analogs_host_load1();
		if ($load1 !== null && $load1 >= 6.0) {
			return 0;
		}
	}

	if (function_exists('docpart_analogs_match_exprs')) {
		list($art_expr, $analog_expr) = docpart_analogs_match_exprs($db_link);
	} else {
		$art_expr = docpart_sql_article_normalized_expr('`article`');
		$analog_expr = docpart_sql_article_normalized_expr('`analog`');
	}

	$count += epc_cross_load_local_aftermarket_for_oem($db_link, $article_norm, $references, $seen);

	$direct_queries = array(

		array(

			'SELECT `article`, `manufacturer_article`, `analog`, `manufacturer_analog` '

				.'FROM `shop_docpart_articles_analogs_list` WHERE '.$art_expr.' = ? ORDER BY `id` DESC LIMIT 120',

			$article_norm,

		),

		array(

			'SELECT `article`, `manufacturer_article`, `analog`, `manufacturer_analog` '

				.'FROM `shop_docpart_articles_analogs_list` WHERE '.$analog_expr.' = ? ORDER BY `id` DESC LIMIT 400',

			$article_norm,

		),

	);

	foreach($direct_queries as $query_spec)

	{

		$sql = $query_spec[0];

		$params = array_slice($query_spec, 1);

		try

		{

			$cross_query = $db_link->prepare($sql);

			$cross_query->execute($params);

		}

		catch(Exception $e)

		{

			continue;

		}

		while($row = $cross_query->fetch(PDO::FETCH_ASSOC))

		{

			$row_article_norm = docpart_normalize_article_for_price($row['article']);

			$row_analog_norm = docpart_normalize_article_for_price($row['analog']);

		$matches_anchor = ($row_article_norm === $article_norm || $row_analog_norm === $article_norm);

		if(!$matches_anchor && strlen($article_norm) >= 8)

		{

			$matches_anchor = (strpos($row_article_norm, $article_norm) === 0 || strpos($row_analog_norm, $article_norm) === 0);

		}

		if(!$matches_anchor)

		{

			continue;

		}

		if($row_article_norm === $article_norm || (strlen($article_norm) >= 8 && strpos($row_article_norm, $article_norm) === 0))

		{

			if(epc_cross_add_reference($references, $seen, $row['manufacturer_analog'], $row['analog'], 'cp'))

			{

				$count++;

			}

		}

		if($row_analog_norm === $article_norm || (strlen($article_norm) >= 8 && strpos($row_analog_norm, $article_norm) === 0))

		{

			if(epc_cross_add_reference($references, $seen, $row['manufacturer_article'], $row['article'], 'cp'))

			{

				$count++;

			}

		}

		}

	}

	if($count < 60 && function_exists('docpart_load_interchange_partners'))
	{
		$rounds = 2;
		$limit = 80;
		if (function_exists('docpart_analogs_host_load1')) {
			$load1 = docpart_analogs_host_load1();
			if ($load1 !== null && $load1 >= 5.0) {
				$rounds = 0;
			} elseif ($load1 !== null && $load1 >= 3.0) {
				$rounds = 1;
				$limit = 30;
			}
		}
		if ($rounds > 0) {
			$partners = docpart_load_interchange_partners($db_link, $article_norm, $rounds, $limit);
			foreach ($partners as $partner) {
				if (epc_cross_add_reference($references, $seen, $partner['brand'], $partner['article'], 'cp')) {
					$count++;
				}
			}
		}
	}

	return $count;

}



function epc_cross_load_crossbase_references($article_input, &$references, &$seen, &$crossbase_total, $fetch_timeout_seconds = 20, &$crossbase_html_out = null, $parse_max = 0)

{

	$parse_max = (int)$parse_max;

	if($parse_max < 1)

	{

		$parse_max = EPC_CROSS_CROSSBASE_MAX;

	}

	$added = 0;

	$crossbase_html = epc_cross_fetch_crossbase_html($article_input, $fetch_timeout_seconds);

	if($crossbase_html_out !== null)

	{

		$crossbase_html_out = $crossbase_html;

	}

	if($crossbase_html == '')

	{

		return 0;

	}

	if(preg_match('~существует.*?([0-9]+).*?замен~isu', $crossbase_html, $total_match))

	{

		$crossbase_total = (int)$total_match[1];

	}

	$patterns = array(

		'~<tr>\s*<td[^>]*>\s*[0-9]+\s*</td>\s*<td[^>]*>\s*<a[^>]*href=["\']/cross/\?q=([^"\']+)["\'][^>]*>(.*?)</a>~isu',

		'~<a\s+[^>]*href=["\']/cross/\?q=([^"\']+)["\'][^>]*>(.*?)</a>~isu',

		'~<a\s+[^>]*href=["\']/cross/\?q=([^"\']+)["\']~isu',

	);

	foreach($patterns as $pattern_index => $pattern)

	{

		if(!preg_match_all($pattern, $crossbase_html, $matches, PREG_SET_ORDER))

		{

			continue;

		}

		foreach($matches as $match)

		{

			$number = trim(urldecode($match[1]));

			$number_norm = docpart_normalize_article_for_price($number);

			if($number_norm == '')

			{

				continue;

			}

			$brand = '';

			if(isset($match[2]))

			{

				$brand = epc_cross_brand_from_crossbase_text($number, $match[2]);

			}

			if(epc_cross_add_reference($references, $seen, $brand, $number, 'crossbase'))

			{

				$added++;

			}

			if($added >= $parse_max)

			{

				break 2;

			}

		}

		if($added > 0)

		{

			break;

		}

	}

	return $added;

}



function epc_cross_aftermarket_oem_rank($article_norm)

{

	$article_norm = docpart_normalize_article_for_price($article_norm);

	$rank = epc_cross_interchange_sort_score(array('article_norm' => $article_norm));

	if(preg_match('/^C110/i', $article_norm))

	{

		return -2;

	}

	if(preg_match('/^C11[0-9]/i', $article_norm))

	{

		return -1;

	}

	return $rank;

}



function epc_cross_add_crossbase_aftermarket_for_oem_rows($crossbase_html, $article_norm, &$references, &$seen, $max_add = 50)

{

	$added = 0;

	$article_norm = docpart_normalize_article_for_price($article_norm);

	if($crossbase_html === '' || $article_norm === '' || strlen($article_norm) < 8)

	{

		return 0;

	}

	$needle = $article_norm;

	$candidates = array();

	$candidate_seen = array();

	if(!preg_match_all('~<tr\b.*?</tr>~isu', $crossbase_html, $row_matches))

	{

		return 0;

	}

	foreach($row_matches[0] as $row_html)

	{

		if(stripos($row_html, $needle) === false)

		{

			continue;

		}

		if(!preg_match_all('~href=["\']/cross/\?q=([^"\']+)["\']~iu', $row_html, $link_matches))

		{

			continue;

		}

		foreach($link_matches[1] as $raw_number)

		{

			$number = trim(urldecode($raw_number));

			$number_norm = docpart_normalize_article_for_price($number);

			if($number_norm === '' || $number_norm === $article_norm || !epc_cross_is_aftermarket_article_norm($number_norm))

			{

				continue;

			}

			$candidate_key = $number_norm;

			if(isset($candidate_seen[$candidate_key]))

			{

				continue;

			}

			$candidate_seen[$candidate_key] = true;

			$brand = epc_cross_brand_from_crossbase_row($number, $row_html);

			$candidates[] = array(

				'brand' => $brand,

				'article' => $number,

				'article_norm' => $number_norm,

				'rank' => epc_cross_aftermarket_oem_rank($number_norm),

			);

		}

	}

	usort($candidates, function($a, $b) {

		$rank_cmp = ($a['rank'] <=> $b['rank']);

		if($rank_cmp !== 0)

		{

			return $rank_cmp;

		}

		return strcmp($a['article_norm'], $b['article_norm']);

	});

	foreach(array_slice($candidates, 0, $max_add) as $candidate)

	{

		if(epc_cross_add_reference($references, $seen, $candidate['brand'], $candidate['article'], 'crossbase_oem'))

		{

			$added++;

		}

	}

	return $added;

}



function epc_cross_oem_from_crossbase_rank($article_norm)

{

	$article_norm = docpart_normalize_article_for_price($article_norm);

	$rank = epc_cross_interchange_sort_score(array('article_norm' => $article_norm));

	if(preg_match('/^90915/i', $article_norm))

	{

		return -3;

	}

	if(strlen($article_norm) >= 10 && preg_match('/^[0-9]{8,}/', $article_norm))

	{

		return -2;

	}

	if(strlen($article_norm) >= 8 && !epc_cross_is_aftermarket_article_norm($article_norm))

	{

		return -1;

	}

	return $rank;

}



function epc_cross_add_crossbase_oem_for_aftermarket_rows($crossbase_html, $article_norm, &$references, &$seen, $max_add = 40)

{

	$added = 0;

	$article_norm = docpart_normalize_article_for_price($article_norm);

	if($crossbase_html === '' || $article_norm === '' || !epc_cross_is_aftermarket_article_norm($article_norm))

	{

		return 0;

	}

	$needle = $article_norm;

	$candidates = array();

	$candidate_seen = array();

	if(!preg_match_all('~<tr\b.*?</tr>~isu', $crossbase_html, $row_matches))

	{

		return 0;

	}

	foreach($row_matches[0] as $row_html)

	{

		if(stripos($row_html, $needle) === false)

		{

			continue;

		}

		if(!preg_match_all('~href=["\']/cross/\?q=([^"\']+)["\']~iu', $row_html, $link_matches))

		{

			continue;

		}

		foreach($link_matches[1] as $raw_number)

		{

			$number = trim(urldecode($raw_number));

			$number_norm = docpart_normalize_article_for_price($number);

			if($number_norm === '' || $number_norm === $article_norm)

			{

				continue;

			}

			if(strlen($number_norm) < 8 || epc_cross_is_aftermarket_article_norm($number_norm))

			{

				continue;

			}

			$candidate_key = $number_norm;

			if(isset($candidate_seen[$candidate_key]))

			{

				continue;

			}

			$candidate_seen[$candidate_key] = true;

			$brand = epc_cross_brand_from_crossbase_row($number, $row_html);

			$candidates[] = array(

				'brand' => $brand,

				'article' => $number,

				'article_norm' => $number_norm,

				'rank' => epc_cross_oem_from_crossbase_rank($number_norm),

			);

		}

	}

	usort($candidates, function($a, $b) {

		$rank_cmp = ($a['rank'] <=> $b['rank']);

		if($rank_cmp !== 0)

		{

			return $rank_cmp;

		}

		return strcmp($a['article_norm'], $b['article_norm']);

	});

	foreach(array_slice($candidates, 0, $max_add) as $candidate)

	{

		if(epc_cross_add_reference($references, $seen, $candidate['brand'], $candidate['article'], 'crossbase_oem'))

		{

			$added++;

		}

	}

	return $added;

}



function epc_cross_build_price_storage_map($db_link)

{

	$map = array();

	try

	{

		$price_storages_query = $db_link->prepare(

			'SELECT `id`, `short_name`, `connection_options`

			FROM `shop_storages`

			WHERE `interface_type` IN (SELECT `id` FROM `shop_storages_interfaces_types` WHERE `handler_folder` = ?)

			AND `hidden` = 0;'

		);

		$price_storages_query->execute(array('prices'));

		while($price_storage = $price_storages_query->fetch(PDO::FETCH_ASSOC))

		{

			$connection_options = json_decode($price_storage['connection_options'], true);

			if(!empty($connection_options['price_id']))

			{

				$map[(int)$connection_options['price_id']] = array(

					'storage_id' => (int)$price_storage['id'],

					'warehouse' => $price_storage['short_name'],

				);

			}

		}

	}

	catch(Exception $e)

	{

		$map = array();

	}

	return $map;

}



function epc_cross_reference_pair_key($brand, $article_norm)

{

	$article_norm = docpart_normalize_article_for_price($article_norm);

	$brand = trim((string)$brand);

	if($article_norm === '')

	{

		return '';

	}

	return mb_strtoupper($brand.'|'.$article_norm, 'UTF-8');

}



function epc_cross_build_reference_pair_keys($references, $synonym_map)

{

	$keys = array();

	$norms = array();

	foreach($references as $reference)

	{

		$article_norm = !empty($reference['article_norm'])

			? $reference['article_norm']

			: docpart_normalize_article_for_price(isset($reference['article']) ? $reference['article'] : '');

		if($article_norm === '')

		{

			continue;

		}

		$norms[$article_norm] = true;

		$brand_names = docpart_synonym_names_for_brand(isset($reference['brand']) ? $reference['brand'] : '', $synonym_map);

		foreach($brand_names as $brand_name)

		{

			$key = epc_cross_reference_pair_key($brand_name, $article_norm);

			if($key !== '')

			{

				$keys[$key] = true;

			}

		}

	}

	return array($keys, array_keys($norms));

}



function epc_cross_load_stock_for_references($db_link, $references, $epc_price_storage_map, $synonym_map, $canonical_map)

{

	$stock = array();

	$stock_by_key = array();

	list($reference_keys, $reference_norms) = epc_cross_build_reference_pair_keys($references, $synonym_map);

	if(empty($reference_keys) || empty($reference_norms))

	{

		return $stock;

	}

	$art_expr = docpart_sql_article_normalized_expr('`article`');

	$batches = array_chunk($reference_norms, EPC_CROSS_STOCK_BATCH);

	foreach($batches as $batch)

	{

		if(count($stock) >= EPC_CROSS_STOCK_MAX)

		{

			break;

		}

		try

		{

			$article_placeholders = str_repeat('?,', count($batch) - 1) . '?';

			$stock_query = $db_link->prepare(

				'SELECT * FROM `shop_docpart_prices_data` '

				.'WHERE '.$art_expr.' IN ('.$article_placeholders.') '

				.'AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0 '

				.'ORDER BY `manufacturer`, `article`'

			);

			$stock_query->execute($batch);

			while($product = $stock_query->fetch(PDO::FETCH_ASSOC))

			{

				$product_norm = docpart_normalize_article_for_price(isset($product['article']) ? $product['article'] : '');

				$product_brand = isset($product['manufacturer']) ? trim((string)$product['manufacturer']) : '';

				$pair_key = epc_cross_reference_pair_key($product_brand, $product_norm);

				$norm_in_cluster = ($product_norm !== '' && in_array($product_norm, $reference_norms, true));

				if($product_norm === '' || (!$norm_in_cluster && ($pair_key === '' || !isset($reference_keys[$pair_key]))))

				{

					continue;

				}

				$price_id = isset($product['price_id']) ? (int)$product['price_id'] : 0;

				$warehouse = '';

				$storage_id = 0;

				if($price_id > 0 && isset($epc_price_storage_map[$price_id]))

				{

					$warehouse = $epc_price_storage_map[$price_id]['warehouse'];

					$storage_id = $epc_price_storage_map[$price_id]['storage_id'];

				}

				$canonical_brand = docpart_synonym_canonical_brand($product_brand, $canonical_map);

				$stock_key = mb_strtoupper($canonical_brand.'|'.$product_norm, 'UTF-8');

				$price_value = isset($product['price']) ? (float)$product['price'] : 0;

				if(isset($stock_by_key[$stock_key]))

				{

					$existing_price = isset($stock_by_key[$stock_key]['price']) ? (float)$stock_by_key[$stock_key]['price'] : 0;

					if($price_value >= $existing_price)

					{

						continue;

					}

				}

				$stock_by_key[$stock_key] = array(

					'brand' => $canonical_brand,

					'article' => !empty($product['article_show']) ? $product['article_show'] : (isset($product['article']) ? $product['article'] : ''),

					'article_norm' => $product_norm,

					'name' => isset($product['name']) ? $product['name'] : '',

					'price' => isset($product['price']) ? $product['price'] : '',

					'currency' => isset($product['currency']) ? $product['currency'] : '',

					'qty' => isset($product['exist']) ? $product['exist'] : '',

					'delivery' => isset($product['time_to_exe']) ? $product['time_to_exe'] : '',

					'warehouse' => $warehouse,

					'storage_id' => $storage_id,

					'price_id' => $price_id,

				);

				if(count($stock_by_key) >= EPC_CROSS_STOCK_MAX)

				{

					break;

				}

			}

		}

		catch(Exception $e)

		{

			continue;

		}

	}

	if(!empty($stock_by_key))

	{

		$stock = array_values($stock_by_key);

	}

	return $stock;

}



function epc_cross_supplement_stock_for_references($db_link, &$stock, $references, $epc_price_storage_map, $synonym_map, $canonical_map, $max_lookups = 120)

{

	$stock_by_key = array();

	foreach($stock as $row)

	{

		$row_norm = docpart_normalize_article_for_price(isset($row['article_norm']) ? $row['article_norm'] : (isset($row['article']) ? $row['article'] : ''));

		$row_brand = isset($row['brand']) ? trim((string)$row['brand']) : '';

		if($row_norm === '')

		{

			continue;

		}

		$stock_by_key[mb_strtoupper($row_brand.'|'.$row_norm, 'UTF-8')] = true;

	}

	$art_expr = docpart_sql_article_normalized_expr('`article`');

	$lookups = 0;

	foreach($references as $reference)

	{

		if($lookups >= $max_lookups)

		{

			break;

		}

		$article_norm = !empty($reference['article_norm'])

			? $reference['article_norm']

			: docpart_normalize_article_for_price(isset($reference['article']) ? $reference['article'] : '');

		if($article_norm === '')

		{

			continue;

		}

		$brand_names = docpart_synonym_names_for_brand(isset($reference['brand']) ? $reference['brand'] : '', $synonym_map);

		if(empty($brand_names))

		{

			continue;

		}

		$canonical_brand = docpart_synonym_canonical_brand($brand_names[0], $canonical_map);

		$stock_key = mb_strtoupper($canonical_brand.'|'.$article_norm, 'UTF-8');

		if(isset($stock_by_key[$stock_key]))

		{

			continue;

		}

		$mfr_placeholders = implode(',', array_fill(0, count($brand_names), '?'));

		$params = array($article_norm);

		foreach($brand_names as $brand_name)

		{

			$params[] = mb_strtoupper(trim($brand_name), 'UTF-8');

		}

		try

		{

			$row_query = $db_link->prepare(

				'SELECT * FROM `shop_docpart_prices_data` WHERE '.$art_expr.' = ? '

				.'AND UPPER(TRIM(`manufacturer`)) IN ('.$mfr_placeholders.') '

				.'AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0 '

				.'ORDER BY `exist` DESC, `price` ASC LIMIT 1'

			);

			$row_query->execute($params);

			$product = $row_query->fetch(PDO::FETCH_ASSOC);

			if(!$product)

			{

				continue;

			}

			$lookups++;

			$price_id = isset($product['price_id']) ? (int)$product['price_id'] : 0;

			$warehouse = '';

			$storage_id = 0;

			if($price_id > 0 && isset($epc_price_storage_map[$price_id]))

			{

				$warehouse = $epc_price_storage_map[$price_id]['warehouse'];

				$storage_id = $epc_price_storage_map[$price_id]['storage_id'];

			}

			$stock[] = array(

				'brand' => $canonical_brand,

				'article' => !empty($product['article_show']) ? $product['article_show'] : (isset($product['article']) ? $product['article'] : ''),

				'article_norm' => $article_norm,

				'name' => isset($product['name']) ? $product['name'] : '',

				'price' => isset($product['price']) ? $product['price'] : '',

				'currency' => isset($product['currency']) ? $product['currency'] : '',

				'qty' => isset($product['exist']) ? $product['exist'] : '',

				'delivery' => isset($product['time_to_exe']) ? $product['time_to_exe'] : '',

				'warehouse' => $warehouse,

				'storage_id' => $storage_id,

				'price_id' => $price_id,

				'source' => 'reference_lookup',

			);

			$stock_by_key[$stock_key] = true;

		}

		catch(Exception $e)

		{

			continue;

		}

	}

	return $stock;

}



function epc_cross_merge_anchor_stock(&$stock, $db_link, $article_norm, $anchor_brand, $epc_price_storage_map, $canonical_map)

{

	$article_norm = trim((string)$article_norm);

	$anchor_brand = trim((string)$anchor_brand);

	if($article_norm === '')

	{

		return;

	}

	$art_expr = docpart_sql_article_normalized_expr('`article`');

	$sql = 'SELECT * FROM `shop_docpart_prices_data` WHERE '.$art_expr.' = ? AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0';

	$params = array($article_norm);

	$mfr_names = array();

	if($anchor_brand !== '')

	{

		$mfr_names[] = mb_strtoupper($anchor_brand, 'UTF-8');

		try

		{

			$synonym_query = $db_link->prepare('SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = (SELECT `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? LIMIT 1);');

			$synonym_query->execute(array($mfr_names[0]));

			$synonym_record = $synonym_query->fetch(PDO::FETCH_ASSOC);

			if($synonym_record && !empty($synonym_record['name']))

			{

				$mfr_names[] = mb_strtoupper(trim($synonym_record['name']), 'UTF-8');

			}

		}

		catch(Exception $e) {}

		$mfr_names = array_values(array_unique($mfr_names));

		$mfr_placeholders = implode(',', array_fill(0, count($mfr_names), '?'));

		$sql .= ' AND UPPER(TRIM(`manufacturer`)) IN ('.$mfr_placeholders.')';

		$params = array_merge($params, $mfr_names);

	}

	$sql .= ' ORDER BY `exist` DESC, `price` ASC LIMIT 20';

	try

	{

		$anchor_query = $db_link->prepare($sql);

		$anchor_query->execute($params);

		while($product = $anchor_query->fetch(PDO::FETCH_ASSOC))

		{

			$product_norm = docpart_normalize_article_for_price(isset($product['article']) ? $product['article'] : '');

			$product_brand = isset($product['manufacturer']) ? trim((string)$product['manufacturer']) : '';

			$canonical_brand = docpart_synonym_canonical_brand($product_brand, $canonical_map);

			$stock_key = mb_strtoupper($canonical_brand.'|'.$product_norm, 'UTF-8');

			foreach($stock as $existing)

			{

				$existing_norm = docpart_normalize_article_for_price(isset($existing['article_norm']) ? $existing['article_norm'] : (isset($existing['article']) ? $existing['article'] : ''));

				$existing_brand = isset($existing['brand']) ? trim((string)$existing['brand']) : '';

				if($existing_norm === $product_norm && mb_strtoupper($existing_brand, 'UTF-8') === mb_strtoupper($canonical_brand, 'UTF-8'))

				{

					continue 2;

				}

			}

			$price_id = isset($product['price_id']) ? (int)$product['price_id'] : 0;

			$warehouse = '';

			$storage_id = 0;

			if($price_id > 0 && isset($epc_price_storage_map[$price_id]))

			{

				$warehouse = $epc_price_storage_map[$price_id]['warehouse'];

				$storage_id = $epc_price_storage_map[$price_id]['storage_id'];

			}

			array_unshift($stock, array(

				'brand' => $canonical_brand,

				'article' => !empty($product['article_show']) ? $product['article_show'] : (isset($product['article']) ? $product['article'] : ''),

				'article_norm' => $product_norm,

				'name' => isset($product['name']) ? $product['name'] : '',

				'price' => isset($product['price']) ? $product['price'] : '',

				'currency' => isset($product['currency']) ? $product['currency'] : '',

				'qty' => isset($product['exist']) ? $product['exist'] : '',

				'delivery' => isset($product['time_to_exe']) ? $product['time_to_exe'] : '',

				'warehouse' => $warehouse,

				'storage_id' => $storage_id,

				'price_id' => $price_id,

				'source' => 'anchor_price',

			));

		}

	}

	catch(Exception $e) {}

}



function epc_cross_apply_price_name_cluster($db_link, $article_norm, &$references, &$seen)

{

	$added = 0;

	if(!function_exists('docpart_cross_refs_from_price_name_cluster'))

	{

		return 0;

	}

	$cluster_refs = docpart_cross_refs_from_price_name_cluster($db_link, $article_norm, 40);

	foreach($cluster_refs as $ref)

	{

		if(epc_cross_add_reference($references, $seen, $ref['brand'], $ref['article'], 'price_name'))

		{

			$added++;

		}

	}

	if(strlen($article_norm) >= 8 && function_exists('docpart_cross_refs_from_stock_oem_mention'))

	{

		$oem_refs = docpart_cross_refs_from_stock_oem_mention($db_link, $article_norm, 20);

		foreach($oem_refs as $ref)

		{

			if(epc_cross_add_reference($references, $seen, $ref['brand'], $ref['article'], 'price_name'))

			{

				$added++;

			}

		}

	}

	return $added;

}



function epc_cross_persist_interchange_for_customer($db_link, $DP_Config, $article_input, $anchor_brand, &$references, $source_filter = '', $max_pairs = 120)

{

	$persisted = 0;

	$use_local = (isset($DP_Config->local_crosses) && !empty($DP_Config->local_crosses));

	if(!$use_local || !function_exists('docpart_cross_persist_interchange_pair_bidirectional'))

	{

		return 0;

	}

	// Persisting pairs runs expensive analogs_list SELECT id … REPLACE() checks.
	// Under load this floods MySQL and 524s CP (e.g. /cp/shop/prices/multivendor).
	if (function_exists('docpart_analogs_host_load1')) {
		$load1 = docpart_analogs_host_load1();
		if ($load1 !== null && $load1 >= 5.0) {
			return 0;
		}
		if ($load1 !== null && $load1 >= 3.0) {
			$max_pairs = min((int) $max_pairs, 10);
		}
	}

	$article_input = trim((string)$article_input);

	$anchor_brand = trim((string)$anchor_brand);

	$max_pairs = max(10, min(250, (int)$max_pairs));

	$pairs_done = 0;

	foreach($references as $ref)

	{

		if($pairs_done >= $max_pairs)

		{

			break;

		}

		if($source_filter !== '' && (!isset($ref['source']) || $ref['source'] !== $source_filter))

		{

			continue;

		}

		$partner_article = isset($ref['article']) ? trim((string)$ref['article']) : '';

		$partner_brand = isset($ref['brand']) ? trim((string)$ref['brand']) : '';

		if($partner_article === '')

		{

			continue;

		}

		$anchor_brand_resolved = docpart_cross_prepare_brand_name($anchor_brand);
		if($anchor_brand_resolved === '')
		{
			$anchor_brand_resolved = docpart_cross_resolve_brand_for_article($db_link, $article_input, array());
		}
		$partner_brand_resolved = docpart_cross_prepare_brand_name($partner_brand);
		if($partner_brand_resolved === '')
		{
			$partner_brand_resolved = docpart_cross_resolve_brand_for_article($db_link, $partner_article, array(
				'fallback_brand' => $anchor_brand_resolved,
				'partner_brand' => $anchor_brand_resolved,
			));
		}
		if($anchor_brand_resolved === '' || $partner_brand_resolved === '')
		{
			continue;
		}

		$persisted += docpart_cross_persist_interchange_pair_bidirectional(

			$db_link,

			$article_input,

			$anchor_brand_resolved,

			$partner_article,

			$partner_brand_resolved

		);

		$pairs_done++;

	}

	return $persisted;

}



function epc_cross_persist_crossbase_pairs($db_link, $DP_Config, $article_input, $anchor_brand, &$references)

{

	return epc_cross_persist_interchange_for_customer($db_link, $DP_Config, $article_input, $anchor_brand, $references, 'crossbase');

}



function epc_cross_interchange_sort_score($ref)

{

	$norm = isset($ref['article_norm']) ? (string)$ref['article_norm'] : '';

	if($norm === '')

	{

		return 2;

	}

	if(preg_match('/^[A-Z]{1,5}[0-9]{2,}[A-Z0-9]{0,4}$/i', $norm))

	{

		return 0;

	}

	if(strlen($norm) > 12)

	{

		return 2;

	}

	return 1;

}



function epc_cross_sort_interchange_bucket($refs)

{

	usort($refs, function($a, $b) {

		$score_cmp = epc_cross_interchange_sort_score($a) <=> epc_cross_interchange_sort_score($b);

		if($score_cmp !== 0)

		{

			return $score_cmp;

		}

		$brand_a = isset($a['brand']) ? (string)$a['brand'] : '';

		$brand_b = isset($b['brand']) ? (string)$b['brand'] : '';

		$brand_cmp = strcmp($brand_a, $brand_b);

		if($brand_cmp !== 0)

		{

			return $brand_cmp;

		}

		$art_a = isset($a['article_norm']) ? (string)$a['article_norm'] : '';

		$art_b = isset($b['article_norm']) ? (string)$b['article_norm'] : '';

		return strcmp($art_a, $art_b);

	});

	return $refs;

}



/**
 * Collapse references to unique brand+article_norm combinations.
 * Synonym brands (e.g. AISIN/AISINC) and empty-brand rows for the same article count as one.
 */
function epc_cross_dedupe_unique_references($references, $synonym_map = array(), $canonical_map = array())
{
	if(!is_array($references) || count($references) < 2)
	{
		return is_array($references) ? array_values($references) : array();
	}

	$merged = array();
	$order = array();

	foreach($references as $ref)
	{
		if(!is_array($ref))
		{
			continue;
		}
		$article = isset($ref['article']) ? trim((string)$ref['article']) : '';
		$article_norm = !empty($ref['article_norm'])
			? docpart_normalize_article_for_price($ref['article_norm'])
			: docpart_normalize_article_for_price($article);
		if($article_norm === '')
		{
			continue;
		}
		$brand = isset($ref['brand']) ? trim((string)$ref['brand']) : '';
		$canon = ($brand !== '' && is_array($canonical_map))
			? docpart_synonym_canonical_brand($brand, $canonical_map)
			: $brand;
		$key = mb_strtoupper(($canon !== '' ? $canon : '').'|'.$article_norm, 'UTF-8');

		if($canon === '')
		{
			foreach($merged as $existing_key => $existing_ref)
			{
				$existing_norm = !empty($existing_ref['article_norm'])
					? docpart_normalize_article_for_price($existing_ref['article_norm'])
					: docpart_normalize_article_for_price(isset($existing_ref['article']) ? $existing_ref['article'] : '');
				if($existing_norm === $article_norm)
				{
					$key = $existing_key;
					break;
				}
			}
		}
		else
		{
			$empty_key = mb_strtoupper('|'.$article_norm, 'UTF-8');
			if(isset($merged[$empty_key]))
			{
				$ref = epc_cross_merge_reference_rows($merged[$empty_key], $ref);
				unset($merged[$empty_key]);
				$order = array_values(array_filter($order, function($existing_order_key) use ($empty_key) {
					return $existing_order_key !== $empty_key;
				}));
			}
			foreach($merged as $existing_key => $existing_ref)
			{
				$existing_norm = !empty($existing_ref['article_norm'])
					? docpart_normalize_article_for_price($existing_ref['article_norm'])
					: docpart_normalize_article_for_price(isset($existing_ref['article']) ? $existing_ref['article'] : '');
				if($existing_norm !== $article_norm)
				{
					continue;
				}
				$existing_brand = isset($existing_ref['brand']) ? trim((string)$existing_ref['brand']) : '';
				if($existing_brand !== '' && docpart_synonym_brands_equivalent($existing_brand, $brand, $synonym_map))
				{
					$key = $existing_key;
					break;
				}
			}
		}

		$ref['article_norm'] = $article_norm;
		if($brand !== '' && (!isset($ref['brand']) || trim((string)$ref['brand']) === ''))
		{
			$ref['brand'] = $brand;
		}

		if(!isset($merged[$key]))
		{
			$merged[$key] = $ref;
			$order[] = $key;
		}
		else
		{
			$merged[$key] = epc_cross_merge_reference_rows($merged[$key], $ref);
		}
	}

	$result = array();
	foreach($order as $key)
	{
		if(isset($merged[$key]))
		{
			$result[] = $merged[$key];
		}
	}
	return $result;
}

function epc_cross_cap_references_for_api($references, $max_count = 120)

{

	$max_count = max(1, (int)$max_count);

	if(count($references) <= $max_count)

	{

		return $references;

	}

	$source_limits = array(

		'crossbase_oem' => 30,

		'cp' => 50,

		'price_name' => 30,

		'crossbase' => 25,

	);

	$buckets = array();

	$other = array();

	foreach($references as $ref)

	{

		$source = isset($ref['source']) ? (string)$ref['source'] : '';

		if(isset($source_limits[$source]))

		{

			if(!isset($buckets[$source]))

			{

				$buckets[$source] = array();

			}

			$buckets[$source][] = $ref;

		}

		else

		{

			$other[] = $ref;

		}

	}

	$ordered = array();

	foreach(array('crossbase_oem', 'cp', 'price_name', 'crossbase') as $source)

	{

		if(empty($buckets[$source]))

		{

			continue;

		}

		$bucket = ($source === 'cp') ? epc_cross_sort_interchange_bucket($buckets[$source]) : $buckets[$source];

		$limit = isset($source_limits[$source]) ? (int)$source_limits[$source] : 0;

		foreach(array_slice($bucket, 0, $limit) as $ref)

		{

			$ordered[] = $ref;

		}

	}

	$remaining = $max_count - count($ordered);

	if($remaining > 0)

	{

		foreach($other as $ref)

		{

			$ordered[] = $ref;

			$remaining--;

			if($remaining <= 0)

			{

				break;

			}

		}

	}

	if(count($ordered) < $max_count)

	{

		foreach(array('crossbase', 'price_name', 'cp') as $source)

		{

			if(empty($buckets[$source]))

			{

				continue;

			}

			$already = 0;

			foreach($ordered as $ref)

			{

				if(isset($ref['source']) && $ref['source'] === $source)

				{

					$already++;

				}

			}

			$bucket = ($source === 'cp') ? epc_cross_sort_interchange_bucket($buckets[$source]) : $buckets[$source];

			foreach(array_slice($bucket, $already) as $ref)

			{

				$ordered[] = $ref;

				if(count($ordered) >= $max_count)

				{

					break 2;

				}

			}

		}

	}

	return array_slice($ordered, 0, $max_count);

}



function epc_cross_prioritize_references($references, $max_count)

{

	$max_count = max(1, (int)$max_count);

	if(count($references) <= $max_count)

	{

		return $references;

	}

	return epc_cross_cap_references_for_api($references, $max_count);

}



function epc_cross_reciprocal_crossbase_expand($article_input, $article_norm, &$references, &$seen, $max_probes = 5)

{

	if(count($references) >= EPC_CROSS_LOCAL_MAX)

	{

		return 0;

	}

	$probes = array();

	foreach($references as $ref)

	{

		$source = isset($ref['source']) ? (string)$ref['source'] : '';

		if($source !== 'crossbase' && $source !== 'cp')

		{

			continue;

		}

		if($source === 'cp' && epc_cross_interchange_sort_score($ref) > 0)

		{

			continue;

		}

		$norm = !empty($ref['article_norm']) ? $ref['article_norm'] : docpart_normalize_article_for_price(isset($ref['article']) ? $ref['article'] : '');

		if($norm === '' || $norm === $article_norm || isset($probes[$norm]))

		{

			continue;

		}

		$probes[$norm] = isset($ref['article']) ? $ref['article'] : $norm;

		if(count($probes) >= $max_probes)

		{

			break;

		}

	}

	$added = 0;

	foreach($probes as $probe_article)

	{

		$crossbase_total_probe = null;

		$before = count($references);

		epc_cross_load_crossbase_references($probe_article, $references, $seen, $crossbase_total_probe, 6);

		$added += count($references) - $before;

	}

	return $added;

}



@set_time_limit(90);

@ini_set('memory_limit', '384M');

$epc_cross_cp_bulk = false;

if(!empty($_GET['cp_bulk']) && !empty($_GET['tech_key']) && hash_equals((string)$DP_Config->tech_key, (string)$_GET['tech_key']))

{

	$epc_cross_cp_bulk = true;

	@set_time_limit(300);

	@ini_set('memory_limit', '512M');

}

// Storefront must return every unique brand+article cross (not a 120-row sample).
$epc_cross_api_ref_max = $epc_cross_cp_bulk ? 5000 : EPC_CROSS_LOCAL_MAX;

$epc_cross_crossbase_parse_max = $epc_cross_cp_bulk ? 5000 : EPC_CROSS_CROSSBASE_MAX;



$references = array();

$seen = array();

$crossbase_total = null;

$crossbase_persisted = 0;

$price_name_count = 0;

$reciprocal_crossbase_count = 0;

$local_count = 0;

$crossbase_count = 0;



$local_count = epc_cross_load_local_references($db_link, $DP_Config, $article_norm, $references, $seen);

$price_name_count = epc_cross_apply_price_name_cluster($db_link, $article_norm, $references, $seen);

if(strlen($article_norm) >= 8 && function_exists('docpart_cross_refs_from_stock_oem_mention'))

{

	foreach(docpart_cross_refs_from_stock_oem_mention($db_link, $article_norm, 25) as $ref)

	{

		if(epc_cross_add_reference($references, $seen, $ref['brand'], $ref['article'], 'price_name'))

		{

			$price_name_count++;

		}

	}

}

$crossbase_html = '';

$crossbase_count = epc_cross_load_crossbase_references($article_input, $references, $seen, $crossbase_total, $epc_cross_cp_bulk ? 45 : 10, $crossbase_html, $epc_cross_crossbase_parse_max);

if(strlen($article_norm) >= 8 && $crossbase_html !== '')

{

	$crossbase_count += epc_cross_add_crossbase_aftermarket_for_oem_rows($crossbase_html, $article_norm, $references, $seen, 50);

}

if($crossbase_html !== '' && epc_cross_is_aftermarket_article_norm($article_norm))

{

	$crossbase_count += epc_cross_add_crossbase_oem_for_aftermarket_rows($crossbase_html, $article_norm, $references, $seen, 40);

}

// Probe crossbase for top aftermarket cp refs (e.g. C110J) when searching an OEM number.
$reciprocal_crossbase_count = 0;
if(
	strlen($article_norm) >= 8
	&& $local_count > 0
	&& count($references) < ($epc_cross_api_ref_max - 40)
	&& !epc_cross_is_aftermarket_article_norm($article_norm)
	&& epc_cross_count_refs_by_source($references, 'crossbase_oem') < 8
)
{
	$reciprocal_crossbase_count = epc_cross_reciprocal_crossbase_expand($article_input, $article_norm, $references, $seen, 3);
}

$epc_price_storage_map = epc_cross_build_price_storage_map($db_link);

$manufacturer_synonym_map = docpart_load_manufacturer_synonym_map($db_link);

$manufacturer_canonical_map = docpart_load_manufacturer_canonical_map($db_link);

// Fill empty brands first, then collapse to unique brand+article before stock/name work.
epc_cross_enrich_reference_brands($references, $anchor_brand);

$references = epc_cross_dedupe_unique_references($references, $manufacturer_synonym_map, $manufacturer_canonical_map);

if(count($references) > $epc_cross_api_ref_max)

{

	$references = epc_cross_prioritize_references($references, $epc_cross_api_ref_max);

}

$stock = array();

if(!$epc_cross_cp_bulk)

{

	$stock = epc_cross_load_stock_for_references($db_link, $references, $epc_price_storage_map, $manufacturer_synonym_map, $manufacturer_canonical_map);

}

epc_cross_merge_anchor_stock($stock, $db_link, $article_norm, $anchor_brand, $epc_price_storage_map, $manufacturer_canonical_map);

epc_cross_enrich_reference_names($references, $stock, $db_link, $manufacturer_synonym_map, $anchor_brand, $article_input);

// Names/stock enrichment can leave synonym duplicates; collapse once more for the API payload.
$references = epc_cross_dedupe_unique_references($references, $manufacturer_synonym_map, $manufacturer_canonical_map);

foreach($stock as &$stock_row)
{
	if(!empty($stock_row['brand']))
	{
		continue;
	}
	$stock_norm = docpart_normalize_article_for_price(isset($stock_row['article_norm']) ? $stock_row['article_norm'] : (isset($stock_row['article']) ? $stock_row['article'] : ''));
	$inferred_stock_brand = epc_cross_infer_brand_from_article_norm($stock_norm);
	if($inferred_stock_brand !== '')
	{
		$stock_row['brand'] = $inferred_stock_brand;
	}
}
unset($stock_row);



$crossbase_persisted = 0;

if(!$epc_cross_cp_bulk && ($crossbase_count > 0 || $price_name_count > 0))

{

	$persist_refs = $references;

	if(count($persist_refs) > 80)

	{

		$persist_refs = epc_cross_prioritize_references($persist_refs, 80);

	}

	$crossbase_persisted = epc_cross_persist_interchange_for_customer($db_link, $DP_Config, $article_input, $anchor_brand, $persist_refs, 'crossbase', 35);

	$crossbase_persisted += epc_cross_persist_interchange_for_customer($db_link, $DP_Config, $article_input, $anchor_brand, $persist_refs, 'price_name', 25);

}

if(($crossbase_persisted > 0 || $reciprocal_crossbase_count > 0) && count($references) < 100)

{

	$local_count += epc_cross_load_local_references($db_link, $DP_Config, $article_norm, $references, $seen);

	$references = epc_cross_dedupe_unique_references($references, $manufacturer_synonym_map, $manufacturer_canonical_map);

}



$unique_reference_count = count($references);

$source_parts = array();

if($local_count > 0)

{

	$source_parts[] = 'cp_crosses';

}

if($crossbase_count > 0)

{

	$source_parts[] = 'crossbase';

}

if($price_name_count > 0)

{

	$source_parts[] = 'price_name';

}

if($reciprocal_crossbase_count > 0)

{

	$source_parts[] = 'crossbase_reciprocal';

}

$source = count($source_parts) ? implode('_and_', $source_parts) : 'none';

// Guest / pending: never return raw price/qty/warehouse in stock[].
// CP bulk (tech_key) keeps full stock for admin tooling.
$epc_cross_prices_visible = true;
if (empty($epc_cross_cp_bulk)
	&& (empty($epc_cross_anti_crawl['tech_key']) && empty($epc_cross_anti_crawl['prices_visible']))
) {
	epc_storefront_anti_crawl_redact_cross_stock($stock);
	$epc_cross_prices_visible = false;
}

epc_cross_json(array(

	'status' => true,

	'source' => $source,

	// Unique brand+article count (not raw crossbase HTML row count).
	'total' => $unique_reference_count,

	'local_count' => $local_count,

	'crossbase_count' => $crossbase_count,

	'crossbase_total' => $crossbase_total,

	'price_name_count' => $price_name_count,

	'crossbase_persisted' => $crossbase_persisted,

	'interchange_bidirectional' => true,

	'reciprocal_crossbase_count' => $reciprocal_crossbase_count,

	'reference_count' => $unique_reference_count,

	'references_loaded' => $unique_reference_count,

	'unique_reference_count' => $unique_reference_count,

	'total_catalog' => $crossbase_total,

	'cp_bulk_mode' => $epc_cross_cp_bulk,

	'api_reference_cap' => $epc_cross_api_ref_max,

	'stock_count' => count($stock),

	'references' => $references,

	'stock' => $stock,

	'manufacturer_synonyms' => $manufacturer_synonym_map,

	'manufacturer_canonical' => $manufacturer_canonical_map,

	'prices_visible' => $epc_cross_prices_visible,

));

?>

