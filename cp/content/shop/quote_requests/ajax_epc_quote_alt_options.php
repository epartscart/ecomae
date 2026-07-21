<?php
/**
 * CP AJAX: alternative parts (cross / article / OEM) + supplier warehouses for quote amend modal.
 * GET/POST: quote_id, line_id, csrf_guard_key
 */
header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$dbHost = trim((string) $DP_Config->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (PDOException $e) {
	http_response_code(503);
	exit(json_encode(array('status' => false, 'message' => 'DB unavailable')));
}
$db_link->query('SET NAMES utf8mb4;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Access denied')));
}

$csrf_check_admin = 1;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/crosses/epc_cp_cross_helpers.php';

function epc_quote_alt_json($payload)
{
	echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

function epc_quote_alt_pair_key($brand, $article_norm)
{
	$brand = epc_cp_cross_prepare_brand($brand);
	$article_norm = epc_cp_cross_normalize_article($article_norm);
	if ($article_norm === '') {
		return '';
	}
	return $brand . '|' . $article_norm;
}

function epc_quote_alt_list_warehouses(PDO $db_link)
{
	$out = array();
	try {
		$q = $db_link->query(
			'SELECT `id`, `name`, `short_name`, `hidden`
			 FROM `shop_storages`
			 WHERE COALESCE(`hidden`, 0) = 0
			 ORDER BY `short_name` ASC, `name` ASC, `id` ASC'
		);
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$sid = (int) $row['id'];
			$short = trim((string) $row['short_name']);
			$name = trim((string) $row['name']);
			$label = $short !== '' ? $short : ($name !== '' ? $name : ('Warehouse #' . $sid));
			if ($short !== '' && $name !== '' && strcasecmp($short, $name) !== 0) {
				$label = $short . ' — ' . $name;
			}
			$out[] = array(
				'storage_id' => $sid,
				'warehouse' => $short !== '' ? $short : $label,
				'label' => $label,
				'name' => $name,
			);
		}
	} catch (Throwable $e) {
		return array();
	}
	return $out;
}

$quote_id = isset($_REQUEST['quote_id']) ? (int) $_REQUEST['quote_id'] : 0;
$line_id = isset($_REQUEST['line_id']) ? (int) $_REQUEST['line_id'] : 0;
if ($quote_id <= 0 || $line_id <= 0) {
	epc_quote_alt_json(array('status' => false, 'message' => 'quote_id and line_id required'));
}

$line_q = $db_link->prepare(
	'SELECT qi.*, qr.`user_id`
	 FROM `shop_quote_items` qi
	 INNER JOIN `shop_quote_requests` qr ON qr.`id` = qi.`quote_id`
	 WHERE qi.`id` = ? AND qi.`quote_id` = ? AND qr.`user_id` > 0
	 LIMIT 1'
);
$line_q->execute(array($line_id, $quote_id));
$line = $line_q->fetch(PDO::FETCH_ASSOC);
if (!$line) {
	epc_quote_alt_json(array('status' => false, 'message' => 'Quote line not found'));
}

$po = json_decode((string) $line['product_object_json'], true);
if (!is_array($po)) {
	$po = array();
}

$req_brand = isset($po['manufacturer']) ? trim((string) $po['manufacturer']) : '';
$req_article_show = '';
if (!empty($po['article_show'])) {
	$req_article_show = trim((string) $po['article_show']);
} elseif (!empty($po['article'])) {
	$req_article_show = trim((string) $po['article']);
}
$req_name = isset($po['name']) ? trim((string) $po['name']) : '';
$req_article_norm = epc_cp_cross_normalize_article($req_article_show);
$req_brand = epc_cp_cross_prepare_brand($req_brand);

// Extra OEM hints from product object when present.
$oem_candidates = array();
foreach (array('oem', 'oem_number', 'oem_article', 'article_oem', 'cross_oem') as $oem_key) {
	if (!empty($po[$oem_key])) {
		$oem_candidates[] = trim((string) $po[$oem_key]);
	}
}
if (!empty($po['json_params']) && is_string($po['json_params'])) {
	$jp = json_decode($po['json_params'], true);
	if (is_array($jp)) {
		foreach (array('oem', 'oem_number', 'oem_article') as $oem_key) {
			if (!empty($jp[$oem_key])) {
				$oem_candidates[] = trim((string) $jp[$oem_key]);
			}
		}
	}
}

if ($req_article_norm === '') {
	epc_quote_alt_json(array('status' => false, 'message' => 'Requested article missing on quote line'));
}

$search = epc_cp_cross_fetch_search($DP_Config, $req_article_show !== '' ? $req_article_show : $req_article_norm, $req_brand);
$references = ($search && !empty($search['references']) && is_array($search['references'])) ? $search['references'] : array();
$stock = ($search && !empty($search['stock']) && is_array($search['stock'])) ? $search['stock'] : array();

if (function_exists('epc_cp_cross_annotate_references')) {
	$references = epc_cp_cross_annotate_references($db_link, $req_article_norm, $req_brand, $references);
}

/** @var array<string,array<string,mixed>> $by_key */
$by_key = array();

$ensure_option = static function ($brand, $article_show, $name, $source) use (&$by_key) {
	$brand = epc_cp_cross_prepare_brand($brand);
	$article_show = trim((string) $article_show);
	$article_norm = epc_cp_cross_normalize_article($article_show);
	$key = epc_quote_alt_pair_key($brand, $article_norm);
	if ($key === '') {
		return null;
	}
	if (!isset($by_key[$key])) {
		$by_key[$key] = array(
			'key' => $key,
			'brand' => $brand,
			'article' => $article_norm,
			'article_show' => $article_show !== '' ? $article_show : $article_norm,
			'name' => trim((string) $name),
			'source' => trim((string) $source),
			'in_stock' => false,
			'warehouses' => array(),
		);
	} else {
		if ($by_key[$key]['name'] === '' && trim((string) $name) !== '') {
			$by_key[$key]['name'] = trim((string) $name);
		}
		if ($source !== '') {
			$existing = (string) $by_key[$key]['source'];
			if ($existing === '') {
				$by_key[$key]['source'] = $source;
			} elseif (stripos($existing, $source) === false) {
				$by_key[$key]['source'] = $existing . '+' . $source;
			}
		}
		// Prefer longer / more readable article_show
		if (strlen($article_show) > strlen((string) $by_key[$key]['article_show'])) {
			$by_key[$key]['article_show'] = $article_show;
		}
	}
	return $key;
};

// Always include the requested part first.
$ensure_option($req_brand, $req_article_show !== '' ? $req_article_show : $req_article_norm, $req_name, 'requested');

foreach ($references as $ref) {
	if (!is_array($ref)) {
		continue;
	}
	$brand = isset($ref['brand']) ? (string) $ref['brand'] : '';
	$article = isset($ref['article']) ? (string) $ref['article'] : '';
	$name = isset($ref['name']) ? (string) $ref['name'] : '';
	$source = isset($ref['source']) ? (string) $ref['source'] : 'cross';
	$ensure_option($brand, $article, $name, $source);
}

foreach ($oem_candidates as $oem_art) {
	if (trim((string) $oem_art) === '') {
		continue;
	}
	$ensure_option($req_brand, $oem_art, $req_name, 'oem');
}

// Attach stock / warehouse rows.
foreach ($stock as $row) {
	if (!is_array($row)) {
		continue;
	}
	$brand = isset($row['brand']) ? (string) $row['brand'] : '';
	$article_show = !empty($row['article']) ? (string) $row['article'] : (!empty($row['article_norm']) ? (string) $row['article_norm'] : '');
	$name = isset($row['name']) ? (string) $row['name'] : '';
	$key = $ensure_option($brand, $article_show, $name, isset($row['source']) ? (string) $row['source'] : 'stock');
	if ($key === null || !isset($by_key[$key])) {
		continue;
	}
	$storage_id = isset($row['storage_id']) ? (int) $row['storage_id'] : 0;
	if ($storage_id <= 0) {
		continue;
	}
	$wh_label = isset($row['warehouse']) ? trim((string) $row['warehouse']) : '';
	if ($wh_label === '') {
		$wh_label = 'Warehouse #' . $storage_id;
	}
	$price = isset($row['price']) && is_numeric($row['price']) ? (float) $row['price'] : 0.0;
	$qty = isset($row['qty']) && is_numeric($row['qty']) ? (float) $row['qty'] : 0.0;
	$delivery = isset($row['delivery']) && is_numeric($row['delivery']) ? (int) $row['delivery'] : null;
	$price_purchase = isset($row['price_purchase']) && is_numeric($row['price_purchase'])
		? (float) $row['price_purchase']
		: (isset($row['purchase']) && is_numeric($row['purchase']) ? (float) $row['purchase'] : null);

	$wh_key = (string) $storage_id;
	$existing_wh = null;
	foreach ($by_key[$key]['warehouses'] as $idx => $wh) {
		if ((int) $wh['storage_id'] === $storage_id) {
			$existing_wh = $idx;
			break;
		}
	}
	$wh_row = array(
		'storage_id' => $storage_id,
		'warehouse' => $wh_label,
		'label' => $wh_label,
		'price' => $price,
		'qty' => $qty,
		'delivery' => $delivery,
		'price_purchase' => $price_purchase,
		'in_stock' => $qty > 0,
	);
	if ($existing_wh === null) {
		$by_key[$key]['warehouses'][] = $wh_row;
	} else {
		// Keep cheaper positive price if both present.
		$prev = $by_key[$key]['warehouses'][$existing_wh];
		if ($price > 0 && (($prev['price'] ?? 0) <= 0 || $price < (float) $prev['price'])) {
			$by_key[$key]['warehouses'][$existing_wh] = $wh_row;
		} else {
			$by_key[$key]['warehouses'][$existing_wh]['qty'] = max((float) ($prev['qty'] ?? 0), $qty);
		}
	}
	$by_key[$key]['in_stock'] = true;
	if ($by_key[$key]['name'] === '' && $name !== '') {
		$by_key[$key]['name'] = $name;
	}
}

$alternatives = array_values($by_key);

// Sort: in-stock first, then brand/article.
usort($alternatives, static function ($a, $b) {
	$as = !empty($a['in_stock']) ? 0 : 1;
	$bs = !empty($b['in_stock']) ? 0 : 1;
	if ($as !== $bs) {
		return $as - $bs;
	}
	$cmp = strcasecmp((string) $a['brand'], (string) $b['brand']);
	if ($cmp !== 0) {
		return $cmp;
	}
	return strcasecmp((string) $a['article_show'], (string) $b['article_show']);
});

// Cap list for UI responsiveness; stocked rows already sorted to the top.
if (count($alternatives) > 250) {
	$alternatives = array_slice($alternatives, 0, 250);
}

$warehouses_all = epc_quote_alt_list_warehouses($db_link);

epc_quote_alt_json(array(
	'status' => true,
	'quote_id' => $quote_id,
	'line_id' => $line_id,
	'requested' => array(
		'brand' => $req_brand,
		'article' => $req_article_norm,
		'article_show' => $req_article_show !== '' ? $req_article_show : $req_article_norm,
		'name' => $req_name,
	),
	'alternatives' => $alternatives,
	'warehouses_all' => $warehouses_all,
	'source' => isset($search['source']) ? (string) $search['source'] : '',
	'reference_count' => count($references),
	'stock_count' => count($stock),
));
