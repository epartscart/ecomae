<?php
/**
 * Public warehouse offers for article (+ optional brand) — PHP CHPU parity helper.
 * Used by ASP.NET /storefront/search when tenant SQL is empty or mis-bound.
 * Mirrors epc_chpu / prices_enclosure article match: no price>0 / storefront_temp_disabled.
 */
header('Content-Type: application/json;charset=utf-8;');
header('Cache-Control: no-store');

require_once($_SERVER['DOCUMENT_ROOT'].'/config.php');
require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/docpart_article_match.php');

$DP_Config = new DP_Config;
$article_input = isset($_GET['article']) ? trim((string)$_GET['article']) : '';
if ($article_input === '' && isset($_POST['article'])) {
	$article_input = trim((string)$_POST['article']);
}
$brand_input = isset($_GET['brand']) ? trim((string)$_GET['brand']) : '';
if ($brand_input === '' && isset($_GET['brend'])) {
	$brand_input = trim((string)$_GET['brend']);
}
if ($brand_input === '' && isset($_POST['brand'])) {
	$brand_input = trim((string)$_POST['brand']);
}
if ($brand_input === '' && isset($_POST['brend'])) {
	$brand_input = trim((string)$_POST['brend']);
}
$limit = isset($_GET['limit']) ? (int)$_GET['limit'] : (isset($_POST['limit']) ? (int)$_POST['limit'] : 100);
if ($limit < 1) {
	$limit = 1;
}
if ($limit > 500) {
	$limit = 500;
}

$article_norm = docpart_normalize_article_for_price($article_input);
if ($article_norm === '') {
	echo json_encode(array(
		'status' => false,
		'article' => '',
		'brand' => $brand_input,
		'count' => 0,
		'rows' => array(),
		'message' => 'Empty article',
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

try {
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db_link->query('SET NAMES utf8;');
} catch (Exception $e) {
	echo json_encode(array(
		'status' => false,
		'article' => $article_norm,
		'brand' => $brand_input,
		'count' => 0,
		'rows' => array(),
		'message' => 'Database unavailable',
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

$price_ids = array();
if (is_file($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/epc_stock_brands_helpers.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/epc_stock_brands_helpers.php';
	if (function_exists('epc_stock_brand_price_ids_with_stock')) {
		$price_ids = epc_stock_brand_price_ids_with_stock($db_link);
	}
	if (function_exists('epc_stock_brand_price_ids')) {
		$office_price_ids = epc_stock_brand_price_ids($db_link);
		if (count($office_price_ids) > 0) {
			$price_ids = array_values(array_unique(array_merge($price_ids, $office_price_ids)));
		}
	}
}
if (count($price_ids) === 0) {
	try {
		$all_prices = $db_link->query('SELECT DISTINCT `id` FROM `shop_docpart_prices` ORDER BY `id` ASC');
		while ($price_row = $all_prices->fetch(PDO::FETCH_ASSOC)) {
			$price_ids[] = (int)$price_row['id'];
		}
	} catch (Exception $e) {
		$price_ids = array(1);
	}
}
$price_ids = array_values(array_unique(array_map('intval', $price_ids)));
if (count($price_ids) === 0) {
	echo json_encode(array(
		'status' => true,
		'article' => $article_norm,
		'brand' => $brand_input,
		'count' => 0,
		'rows' => array(),
		'source' => 'php-chpu',
		'message' => 'No price lists',
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

$article_values = docpart_resolve_article_search_values($db_link, $DP_Config, $article_input, $price_ids);
if (count($article_values) === 0) {
	$article_values = array($article_norm);
}
$art_expr = docpart_sql_article_match_expr($db_link, '`article`');
$price_ph = implode(',', array_fill(0, count($price_ids), '?'));
$art_ph = implode(',', array_fill(0, count($article_values), '?'));

$brand_upper = strtoupper(trim($brand_input));
$brand_compact = strtoupper(str_replace(array(' ', '-', '.'), '', $brand_upper));
$brand_sql = '';
$brand_params = array();
if ($brand_upper !== '') {
	$brand_sql = ' AND (UPPER(TRIM(`manufacturer`)) = ?'
		. ' OR REPLACE(REPLACE(REPLACE(UPPER(TRIM(`manufacturer`)), \' \', \'\'), \'-\', \'\'), \'.\', \'\') = ?)';
	$brand_params = array($brand_upper, $brand_compact);
}

$sql = 'SELECT d.`price_id`,'
	. ' IFNULL(p.`name`, \'\') AS price_list,'
	. ' IFNULL(d.`manufacturer`, \'\') AS manufacturer,'
	. ' IFNULL(d.`article`, \'\') AS article,'
	. ' IFNULL(d.`article_show`, \'\') AS article_show,'
	. ' IFNULL(d.`name`, \'\') AS name,'
	. ' IFNULL(d.`price`, 0) AS price,'
	. ' IFNULL(d.`exist`, 0) AS exist,'
	. ' IFNULL(d.`storage`, \'\') AS storage,'
	. ' IFNULL(d.`time_to_exe`, \'\') AS time_to_exe'
	. ' FROM `shop_docpart_prices_data` d'
	. ' LEFT JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`'
	. ' WHERE ' . $art_expr . ' IN (' . $art_ph . ')'
	. ' AND d.`price_id` IN (' . $price_ph . ')'
	. $brand_sql
	. ' ORDER BY d.`price` ASC'
	. ' LIMIT ' . (int)$limit;

$params = array_merge($article_values, $price_ids, $brand_params);
$rows = array();
try {
	$stmt = $db_link->prepare($sql);
	$stmt->execute($params);
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$rows[] = array(
			'price_id' => (int)($row['price_id'] ?? 0),
			'price_list' => (string)($row['price_list'] ?? ''),
			'manufacturer' => (string)($row['manufacturer'] ?? ''),
			'article' => (string)($row['article'] ?? ''),
			'article_show' => (string)($row['article_show'] ?? ''),
			'name' => (string)($row['name'] ?? ''),
			'price' => (float)($row['price'] ?? 0),
			'exist' => (int)($row['exist'] ?? 0),
			'storage' => (string)($row['storage'] ?? ''),
			'time_to_exe' => (string)($row['time_to_exe'] ?? ''),
		);
	}
} catch (Exception $e) {
	$rows = array();
}

// article_search miss → REPLACE normalize fallback (same as CHPU brands).
if (count($rows) === 0 && $art_expr === '`article_search`') {
	$norm_expr = docpart_sql_article_normalized_expr('`d`.`article`');
	$norm_show = docpart_sql_article_normalized_expr('IFNULL(d.`article_show`, \'\')');
	$sql_fb = 'SELECT d.`price_id`,'
		. ' IFNULL(p.`name`, \'\') AS price_list,'
		. ' IFNULL(d.`manufacturer`, \'\') AS manufacturer,'
		. ' IFNULL(d.`article`, \'\') AS article,'
		. ' IFNULL(d.`article_show`, \'\') AS article_show,'
		. ' IFNULL(d.`name`, \'\') AS name,'
		. ' IFNULL(d.`price`, 0) AS price,'
		. ' IFNULL(d.`exist`, 0) AS exist,'
		. ' IFNULL(d.`storage`, \'\') AS storage,'
		. ' IFNULL(d.`time_to_exe`, \'\') AS time_to_exe'
		. ' FROM `shop_docpart_prices_data` d'
		. ' LEFT JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`'
		. ' WHERE (' . $norm_expr . ' IN (' . $art_ph . ') OR ' . $norm_show . ' IN (' . $art_ph . '))'
		. ' AND d.`price_id` IN (' . $price_ph . ')'
		. $brand_sql
		. ' ORDER BY d.`price` ASC'
		. ' LIMIT ' . (int)$limit;
	try {
		$stmt_fb = $db_link->prepare($sql_fb);
		// article placeholders appear twice in OR clause
		$params_fb = array_merge($article_values, $article_values, $price_ids, $brand_params);
		$stmt_fb->execute($params_fb);
		while ($row = $stmt_fb->fetch(PDO::FETCH_ASSOC)) {
			$rows[] = array(
				'price_id' => (int)($row['price_id'] ?? 0),
				'price_list' => (string)($row['price_list'] ?? ''),
				'manufacturer' => (string)($row['manufacturer'] ?? ''),
				'article' => (string)($row['article'] ?? ''),
				'article_show' => (string)($row['article_show'] ?? ''),
				'name' => (string)($row['name'] ?? ''),
				'price' => (float)($row['price'] ?? 0),
				'exist' => (int)($row['exist'] ?? 0),
				'storage' => (string)($row['storage'] ?? ''),
				'time_to_exe' => (string)($row['time_to_exe'] ?? ''),
			);
		}
	} catch (Exception $e) {
		// keep empty
	}
}

echo json_encode(array(
	'status' => true,
	'article' => $article_norm,
	'brand' => $brand_input,
	'count' => count($rows),
	'rows' => $rows,
	'source' => 'php-chpu',
	'message' => '',
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
exit;
