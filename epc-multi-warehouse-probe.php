<?php
header('Content-Type: application/json; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}
require_once __DIR__ . '/config.php';
$c = new DP_Config();
$db = new PDO('mysql:host=' . $c->host . ';dbname=' . $c->db . ';charset=utf8', $c->user, $c->password);
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$storages = $db->query(
	"SELECT s.id, s.name, s.connection_options
	 FROM shop_storages s
	 WHERE s.hidden = 0 AND s.interface_type = 2
	 ORDER BY s.name"
)->fetchAll(PDO::FETCH_ASSOC);

$priceMap = array();
foreach ($storages as $row) {
	$co = json_decode($row['connection_options'], true);
	$pid = isset($co['price_id']) ? (int)$co['price_id'] : 0;
	if ($pid > 0) {
		$priceMap[$pid] = array('storage_id' => (int)$row['id'], 'name' => $row['name']);
	}
}

$priceIds = array_keys($priceMap);
$in = implode(',', array_map('intval', $priceIds));
$brandFilter = trim((string)($_GET['brand'] ?? ''));
$articleFilter = trim((string)($_GET['article'] ?? ''));

if ($brandFilter !== '' && $articleFilter !== '') {
	$stmt = $db->prepare(
		"SELECT price_id, manufacturer, article, article_show, name, exist, price
		 FROM shop_docpart_prices_data
		 WHERE price_id IN ($in)
		   AND UPPER(TRIM(manufacturer)) = UPPER(?)
		   AND COALESCE(NULLIF(TRIM(article_show), ''), TRIM(article)) = ?
		   AND IFNULL(exist, 0) > 0 AND IFNULL(price, 0) > 0
		 ORDER BY price_id"
	);
	$stmt->execute(array($brandFilter, $articleFilter));
	$lines = array();
	while ($line = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$pid = (int)$line['price_id'];
		$lines[] = array(
			'warehouse' => isset($priceMap[$pid]) ? $priceMap[$pid]['name'] : ('price_id ' . $pid),
			'storage_id' => isset($priceMap[$pid]) ? $priceMap[$pid]['storage_id'] : null,
			'qty' => (float)$line['exist'],
			'price' => (float)$line['price'],
			'name' => $line['name'],
		);
	}
	echo json_encode(array(
		'ok' => true,
		'brand' => $brandFilter,
		'article' => $articleFilter,
		'warehouse_count' => count($lines),
		'lines' => $lines,
		'frontend_url' => rtrim($c->domain_path, '/') . '/en/parts/' . rawurlencode(str_replace('/', '%2F', $brandFilter)) . '/' . rawurlencode($articleFilter),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

$sql = "SELECT UPPER(TRIM(manufacturer)) AS brand,
               COALESCE(NULLIF(TRIM(article_show), ''), TRIM(article)) AS article,
               GROUP_CONCAT(DISTINCT price_id ORDER BY price_id) AS price_ids,
               COUNT(DISTINCT price_id) AS warehouse_count,
               SUM(IFNULL(exist, 0)) AS total_qty
        FROM shop_docpart_prices_data
        WHERE price_id IN ($in)
          AND IFNULL(exist, 0) > 0
          AND IFNULL(price, 0) > 0
          AND TRIM(IFNULL(manufacturer, '')) != ''
          AND TRIM(IFNULL(article, '')) != ''
        GROUP BY UPPER(TRIM(manufacturer)), COALESCE(NULLIF(TRIM(article_show), ''), TRIM(article))
        HAVING warehouse_count >= 2
        ORDER BY warehouse_count DESC, total_qty DESC
        LIMIT " . max(1, min(20, (int)($_GET['limit'] ?? 10)));

$rows = $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
$examples = array();
foreach ($rows as $r) {
	$pids = array_map('intval', explode(',', $r['price_ids']));
	$wh = array();
	$detail = array();
	foreach ($pids as $pid) {
		if (!isset($priceMap[$pid])) {
			continue;
		}
		$wh[] = $priceMap[$pid]['name'];
	}
	$stmt = $db->prepare(
		'SELECT price_id, exist, price, name FROM shop_docpart_prices_data
		 WHERE price_id IN (' . $in . ')
		   AND UPPER(TRIM(manufacturer)) = ?
		   AND COALESCE(NULLIF(TRIM(article_show), \'\'), TRIM(article)) = ?
		   AND IFNULL(exist, 0) > 0
		 ORDER BY price_id'
	);
	$stmt->execute(array($r['brand'], $r['article']));
	while ($line = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$pid = (int)$line['price_id'];
		$detail[] = array(
			'warehouse' => isset($priceMap[$pid]) ? $priceMap[$pid]['name'] : ('price_id ' . $pid),
			'storage_id' => isset($priceMap[$pid]) ? $priceMap[$pid]['storage_id'] : null,
			'qty' => (float)$line['exist'],
			'price' => (float)$line['price'],
		);
	}
	$examples[] = array(
		'brand' => $r['brand'],
		'article' => $r['article'],
		'warehouse_count' => (int)$r['warehouse_count'],
		'warehouses' => $wh,
		'lines' => $detail,
		'frontend_url' => rtrim($c->domain_path, '/') . '/en/parts/' . rawurlencode(str_replace('/', '%2F', $r['brand'])) . '/' . rawurlencode($r['article']),
	);
}

echo json_encode(array(
	'ok' => true,
	'price_warehouses' => array_values($priceMap),
	'multi_warehouse_examples' => $examples,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
