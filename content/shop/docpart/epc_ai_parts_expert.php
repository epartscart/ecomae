<?php
/**
 * AI Parts Expert — unified part-number lookup (UMAPI fitment, Crossbase, local warehouse).
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/docpart_article_match.php';
require_once __DIR__ . '/epc_crossbase_cache.php';
require_once __DIR__ . '/docpart_epc_article_brands.php';
require_once __DIR__ . '/epc_parts_agent.php';
require_once __DIR__ . '/epc_demand_intelligence.php';

define('EPC_AI_EXPERT_RATE_WINDOW', 600);
define('EPC_AI_EXPERT_RATE_MAX', 40);
define('EPC_AI_EXPERT_CROSS_MAX', 48);
define('EPC_AI_EXPERT_FITMENT_MAX', 80);

function epc_ai_expert_enabled($DP_Config): bool
{
	return epc_agent_enabled($DP_Config);
}

function epc_ai_expert_secret($DP_Config): string
{
	if (is_object($DP_Config) && !empty($DP_Config->secret_succession)) {
		return (string)$DP_Config->secret_succession;
	}
	return 'epc-ai-parts-expert';
}

function epc_ai_expert_csrf_token($DP_Config): string
{
	$day = gmdate('Y-m-d');
	return hash_hmac('sha256', 'epc-ai-expert|' . $day, epc_ai_expert_secret($DP_Config));
}

function epc_ai_expert_csrf_valid($DP_Config, string $token): bool
{
	$token = trim($token);
	if ($token === '') {
		return false;
	}
	$expected = epc_ai_expert_csrf_token($DP_Config);
	if (hash_equals($expected, $token)) {
		return true;
	}
	$yesterday = gmdate('Y-m-d', time() - 86400);
	$prev = hash_hmac('sha256', 'epc-ai-expert|' . $yesterday, epc_ai_expert_secret($DP_Config));
	return hash_equals($prev, $token);
}

function epc_ai_expert_client_ip(): string
{
	foreach (array('HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'REMOTE_ADDR') as $key) {
		if (empty($_SERVER[$key])) {
			continue;
		}
		$raw = (string)$_SERVER[$key];
		if ($key === 'HTTP_X_FORWARDED_FOR') {
			$parts = explode(',', $raw);
			$raw = trim($parts[0]);
		}
		if ($raw !== '') {
			return $raw;
		}
	}
	return '0.0.0.0';
}

function epc_ai_expert_rate_limited(): bool
{
	$dir = rtrim(sys_get_temp_dir(), DIRECTORY_SEPARATOR) . DIRECTORY_SEPARATOR . 'epc_ai_expert_rate';
	if (!is_dir($dir)) {
		@mkdir($dir, 0700, true);
	}
	$ip = epc_ai_expert_client_ip();
	$file = $dir . DIRECTORY_SEPARATOR . sha1($ip) . '.json';
	$now = time();
	$window = EPC_AI_EXPERT_RATE_WINDOW;
	$max = EPC_AI_EXPERT_RATE_MAX;
	$hits = array();
	if (is_file($file)) {
		$data = json_decode((string)@file_get_contents($file), true);
		if (is_array($data) && isset($data['hits']) && is_array($data['hits'])) {
			foreach ($data['hits'] as $t) {
				$t = (int)$t;
				if ($t > $now - $window) {
					$hits[] = $t;
				}
			}
		}
	}
	if (count($hits) >= $max) {
		return true;
	}
	$hits[] = $now;
	@file_put_contents($file, json_encode(array('hits' => $hits)), LOCK_EX);
	return false;
}

function epc_ai_expert_fetch_url(string $url, int $timeout = 20): string
{
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_CONNECTTIMEOUT => 8,
			CURLOPT_TIMEOUT => $timeout,
			CURLOPT_SSL_VERIFYPEER => true,
			CURLOPT_USERAGENT => 'ePartsCart AI Parts Expert',
		));
		$html = curl_exec($ch);
		curl_close($ch);
		return is_string($html) ? $html : '';
	}
	$ctx = stream_context_create(array(
		'http' => array(
			'timeout' => $timeout,
			'header' => "User-Agent: ePartsCart AI Parts Expert\r\n",
		),
	));
	$html = @file_get_contents($url, false, $ctx);
	return is_string($html) ? $html : '';
}

function epc_ai_expert_cross_refs_from_crossbase(string $article_input, int $max = EPC_AI_EXPERT_CROSS_MAX): array
{
	$refs = array();
	$seen = array();
	$html = epc_crossbase_cache_read($article_input, 6 * 3600, false);
	if ($html === '') {
		$html = epc_ai_expert_fetch_url('https://crossbase.ru/cross/?q=' . rawurlencode($article_input), 18);
		if ($html !== '') {
			epc_crossbase_cache_write($article_input, $html);
		} else {
			$html = epc_crossbase_cache_read($article_input, 0, true);
		}
	}
	if ($html === '') {
		return array('refs' => array(), 'available' => false, 'total_hint' => 0);
	}
	$total_hint = 0;
	if (preg_match('~существует.*?([0-9]+).*?замен~isu', $html, $m)) {
		$total_hint = (int)$m[1];
	}
	$patterns = array(
		'~<tr>\s*<td[^>]*>\s*[0-9]+\s*</td>\s*<td[^>]*>\s*<a[^>]*href=["\']/cross/\?q=([^"\']+)["\'][^>]*>(.*?)</a>~isu',
		'~<a\s+[^>]*href=["\']/cross/\?q=([^"\']+)["\'][^>]*>(.*?)</a>~isu',
	);
	foreach ($patterns as $pattern) {
		if (!preg_match_all($pattern, $html, $matches, PREG_SET_ORDER)) {
			continue;
		}
		foreach ($matches as $match) {
			$number = trim(urldecode($match[1]));
			$norm = docpart_normalize_article_for_price($number);
			if ($norm === '' || isset($seen[$norm])) {
				continue;
			}
			$brand = '';
			if (isset($match[2])) {
				$brand = epc_article_brands_crossbase_brand($number, $match[2]);
			}
			$seen[$norm] = true;
			$refs[] = array(
				'brand' => $brand,
				'article' => $number,
				'article_norm' => $norm,
				'source' => 'crossbase',
			);
			if (count($refs) >= $max) {
				break 2;
			}
		}
	}
	return array('refs' => $refs, 'available' => true, 'total_hint' => $total_hint);
}

function epc_ai_expert_umapi_brand_rows($DP_Config, string $article): array
{
	$base = epc_demand_site_base($DP_Config);
	if ($base === '') {
		return array();
	}
	$data = epc_demand_http_json(
		$base . '/api/umapi_proxy.php?' . http_build_query(array(
			'action' => 'brands',
			'article' => $article,
			'language' => 'en',
			'vehicle_type' => 'PC',
			'source' => 'ai_parts_expert',
		)),
		22
	);
	if (!is_array($data)) {
		return array();
	}
	if (isset($data['data']) && is_array($data['data'])) {
		return $data['data'];
	}
	if (is_array($data) && isset($data[0])) {
		return $data;
	}
	return array();
}

function epc_ai_expert_pick_brand(string $preferred, array $umapi_rows, array $local_stock): string
{
	$preferred = trim($preferred);
	if ($preferred !== '') {
		return $preferred;
	}
	if (!empty($local_stock[0]['brand'])) {
		return (string)$local_stock[0]['brand'];
	}
	foreach ($umapi_rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$b = isset($row['BRAND']) ? (string)$row['BRAND'] : (isset($row['SUP_BRAND']) ? (string)$row['SUP_BRAND'] : '');
		if ($b !== '') {
			return $b;
		}
	}
	return '';
}

function epc_ai_expert_fitment_rows($DP_Config, string $brand, string $article, int $max = EPC_AI_EXPERT_FITMENT_MAX): array
{
	$summary = epc_demand_fitment_summary($DP_Config, $brand, $article);
	$base = epc_demand_site_base($DP_Config);
	$rows = array();
	$available = false;
	if ($base === '' || trim($brand) === '') {
		return array(
			'available' => false,
			'part_name' => $summary['part_name'] ?? '',
			'product_group' => $summary['product_group'] ?? '',
			'vehicle_count' => 0,
			'rows' => array(),
			'sample' => $summary['vehicles_sample'] ?? array(),
		);
	}
	$art_id = epc_demand_resolve_umapi_art_id($DP_Config, $brand, $article);
	if ($art_id <= 0) {
		return array(
			'available' => false,
			'part_name' => $summary['part_name'] ?? '',
			'product_group' => $summary['product_group'] ?? '',
			'vehicle_count' => (int)($summary['vehicle_count'] ?? 0),
			'rows' => array(),
			'sample' => $summary['vehicles_sample'] ?? array(),
		);
	}
	$links = epc_demand_http_json(
		$base . '/api/umapi_proxy.php?' . http_build_query(array(
			'action' => 'article_links',
			'id' => $art_id,
			'language' => 'en',
			'source' => 'ai_parts_expert',
		)),
		28
	);
	if (!is_array($links)) {
		return array(
			'available' => false,
			'part_name' => $summary['part_name'] ?? '',
			'product_group' => $summary['product_group'] ?? '',
			'vehicle_count' => 0,
			'rows' => array(),
			'sample' => $summary['vehicles_sample'] ?? array(),
		);
	}
	$available = true;
	$all = array();
	foreach (array('PC', 'CV', 'Motorcycle') as $sec) {
		if (!empty($links[$sec]) && is_array($links[$sec])) {
			foreach ($links[$sec] as $row) {
				if (is_array($row)) {
					$all[] = $row;
				}
			}
		}
	}
	foreach ($all as $row) {
		$from = trim((string)($row['CI_FROM'] ?? ''));
		$to = trim((string)($row['CI_TO'] ?? ''));
		$years = ($from && $to) ? ($from . ' – ' . $to) : ($from ? ($from . ' – now') : $to);
		$kw = $row['POWER_KW'] ?? $row['POWER_KW_START'] ?? '';
		$ps = $row['POWER_PS'] ?? $row['POWER_PS_START'] ?? '';
		$power = ($kw && $ps) ? ($kw . ' kW / ' . $ps . ' PS') : ($kw ? ($kw . ' kW') : ($ps ? ($ps . ' PS') : ''));
		$rows[] = array(
			'make' => trim((string)($row['MANUFACTURER'] ?? '')),
			'model' => trim((string)($row['MODEL_SERIES'] ?? '')),
			'modification' => trim((string)($row['PASSENGER_CAR'] ?? $row['COMMERCIAL_VEHICLE'] ?? $row['MOTORBIKE'] ?? '')),
			'years' => $years,
			'power' => $power,
			'engine' => trim(implode(' / ', array_filter(array(
				$row['CAPACITY_TECH'] ?? $row['CAPACITY_LT'] ?? '',
				$row['FUEL_TYPE'] ?? '',
				$row['BODY_TYPE'] ?? $row['PLATFORM_TYPE'] ?? '',
			)))),
			'section' => isset($row['VEHICLE_TYPE']) ? (string)$row['VEHICLE_TYPE'] : '',
		);
		if (count($rows) >= $max) {
			break;
		}
	}
	return array(
		'available' => $available,
		'part_name' => $summary['part_name'] ?? '',
		'product_group' => $summary['product_group'] ?? '',
		'vehicle_count' => count($all),
		'rows' => $rows,
		'sample' => $summary['vehicles_sample'] ?? array(),
	);
}

function epc_ai_expert_local_stock(PDO $db, string $article_norm, string $brand = ''): array
{
	$lines = epc_demand_anchor_stock_from_db($db, $brand, $article_norm);
	if ($brand === '' && empty($lines)) {
		try {
			$art_expr = docpart_sql_article_normalized_expr('`article`');
			$stmt = $db->prepare(
				'SELECT * FROM `shop_docpart_prices_data`
				 WHERE ' . $art_expr . ' = ?
				 AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
				 AND ' . epc_ssf_price_data_active_sql() . '
				 ORDER BY IFNULL(`exist`, 0) DESC, `price` ASC
				 LIMIT 40'
			);
			$stmt->execute(array($article_norm));
			while ($product = $stmt->fetch(PDO::FETCH_ASSOC)) {
				$lines[] = array(
					'brand' => isset($product['manufacturer']) ? trim((string)$product['manufacturer']) : '',
					'article' => !empty($product['article_show']) ? $product['article_show'] : (isset($product['article']) ? $product['article'] : ''),
					'article_norm' => docpart_normalize_article_for_price(isset($product['article']) ? $product['article'] : ''),
					'name' => isset($product['name']) ? (string)$product['name'] : '',
					'price' => isset($product['price']) ? $product['price'] : '',
					'currency' => isset($product['currency']) ? (string)$product['currency'] : '',
					'qty' => isset($product['exist']) ? (float)$product['exist'] : 0,
					'warehouse' => isset($product['storage']) ? (string)$product['storage'] : '',
				);
			}
		} catch (Exception $e) {
		}
	}
	return $lines;
}

function epc_ai_expert_search(PDO $db, $DP_Config, string $article_input, string $brand_input = ''): array
{
	$article_input = trim($article_input);
	$brand_input = trim($brand_input);
	$article_norm = docpart_normalize_article_for_price($article_input);
	$messages = array();
	$sources = array('local' => true);

	if ($article_norm === '' || strlen($article_norm) < 3) {
		return array(
			'ok' => false,
			'message' => 'Enter a valid part number (at least 3 characters).',
		);
	}

	$local_stock = epc_ai_expert_local_stock($db, $article_norm, $brand_input);
	foreach ($local_stock as &$row) {
		$row['url'] = epc_demand_chpu_part_url($DP_Config, $row['brand'], $row['article']);
	}
	unset($row);

	$cross = epc_ai_expert_cross_refs_from_crossbase($article_input);
	if (!$cross['available']) {
		$messages[] = 'Cross-reference data is temporarily unavailable; showing warehouse and catalog fitment only.';
		$sources['crossbase'] = false;
	} else {
		$sources['crossbase'] = true;
	}
	foreach ($cross['refs'] as &$ref) {
		if ($ref['brand'] !== '') {
			$ref['url'] = epc_demand_chpu_part_url($DP_Config, $ref['brand'], $ref['article']);
		}
	}
	unset($ref);

	$umapi_brands = array();
	$umapi_rows = epc_ai_expert_umapi_brand_rows($DP_Config, $article_input);
	if (empty($umapi_rows)) {
		$messages[] = 'Epart catalog fitment lookup is temporarily unavailable.';
		$sources['umapi'] = false;
	} else {
		$sources['umapi'] = true;
		foreach ($umapi_rows as $row) {
			if (!is_array($row)) {
				continue;
			}
			$b = isset($row['BRAND']) ? (string)$row['BRAND'] : (isset($row['SUP_BRAND']) ? (string)$row['SUP_BRAND'] : '');
			if ($b === '') {
				continue;
			}
			$umapi_brands[] = array(
				'brand' => $b,
				'article' => isset($row['ARTICLE_NR']) ? (string)$row['ARTICLE_NR'] : (isset($row['ART_ARTICLE_NR']) ? (string)$row['ART_ARTICLE_NR'] : $article_input),
				'title' => isset($row['TITLE']) ? (string)$row['TITLE'] : (isset($row['DES']) ? (string)$row['DES'] : ''),
			);
		}
	}

	$fit_brand = epc_ai_expert_pick_brand($brand_input, $umapi_rows, $local_stock);
	$fitment = array('available' => false, 'rows' => array(), 'vehicle_count' => 0, 'part_name' => '', 'product_group' => '');
	if ($fit_brand !== '') {
		$fitment = epc_ai_expert_fitment_rows($DP_Config, $fit_brand, $article_input);
		if (!$fitment['available'] && empty($fitment['rows']) && !empty($fitment['sample'])) {
			$fitment['rows'] = $fitment['sample'];
		}
	}

	$part_url = '';
	if ($fit_brand !== '') {
		$part_url = epc_demand_chpu_part_url($DP_Config, $fit_brand, $article_input);
	} elseif (!empty($local_stock[0]['url'])) {
		$part_url = $local_stock[0]['url'];
	}

	return array(
		'ok' => true,
		'article' => $article_input,
		'article_norm' => $article_norm,
		'brand' => $fit_brand,
		'part_url' => $part_url,
		'local_stock' => $local_stock,
		'cross_refs' => $cross['refs'],
		'cross_refs_total_hint' => $cross['total_hint'],
		'umapi_brands' => $umapi_brands,
		'fitment' => $fitment,
		'sources' => $sources,
		'messages' => $messages,
	);
}
