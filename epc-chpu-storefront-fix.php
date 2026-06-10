<?php
/**
 * Probe/fix CHPU storefront warehouse display for demo SKUs (C110J, DT068).
 * GET: token=epartscart-deploy-2026&host=www.epartscart.com&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');

$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$host = trim((string) ($_GET['host'] ?? $_POST['host'] ?? 'www.epartscart.com'));

$DP_Config = new DP_Config();
if ($host !== '') {
	$_SERVER['HTTP_HOST'] = $host;
}
epc_portal_apply_config($DP_Config);

$result = array(
	'ok' => true,
	'apply' => $apply,
	'host' => $host,
	'db' => $DP_Config->db,
	'articles' => array('C110J', 'DT068'),
	'price_rows' => array(),
	'storages' => array(),
	'fixes' => array(),
	'verify_urls' => array(
		'C110J' => rtrim($DP_Config->domain_path, '/') . '/en/parts/' . rawurlencode('JS ASAKASHI') . '/C110J',
		'DT068' => rtrim($DP_Config->domain_path, '/') . '/en/parts/' . rawurlencode('JS ASAKASHI') . '/DT068',
	),
);

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$pdo->query('SET NAMES utf8');
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'DB connect failed: ' . $e->getMessage())));
}

$st = $pdo->query(
	"SELECT d.`id`, d.`price_id`, p.`name` AS `price_list`, d.`manufacturer`, d.`article`, d.`article_show`,
	        d.`exist`, d.`price`, d.`name` AS `part_name`
	 FROM `shop_docpart_prices_data` d
	 INNER JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
	 WHERE UPPER(TRIM(d.`article`)) IN ('C110J', 'DT068')
	 ORDER BY d.`article`, d.`price_id`"
);
$result['price_rows'] = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();

$st = $pdo->query(
	"SELECT s.`id`, s.`name`, s.`short_name`, s.`hidden`, s.`connection_options`
	 FROM `shop_storages` s
	 INNER JOIN `shop_storages_interfaces_types` t ON t.`id` = s.`interface_type`
	 WHERE t.`handler_folder` = 'prices'
	 ORDER BY s.`id`"
);
$result['storages'] = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();

// DT068 demo rows should match JS ASAKASHI on /parts/JS ASAKASHI/DT068 (price-list brand filter).
$dt068WrongBrand = array();
foreach ($result['price_rows'] as $row) {
	if (strtoupper(trim((string) $row['article'])) === 'DT068'
		&& strtoupper(trim((string) $row['manufacturer'])) !== 'JS ASAKASHI') {
		$dt068WrongBrand[] = (int) $row['id'];
	}
}
$result['dt068_wrong_brand_ids'] = $dt068WrongBrand;

if (!$apply) {
	$result['message'] = 'Dry run — pass apply=1 to align DT068 manufacturer to JS ASAKASHI';
	echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

if (count($dt068WrongBrand) > 0) {
	$idList = implode(',', array_map('intval', $dt068WrongBrand));
	$updated = $pdo->exec(
		"UPDATE `shop_docpart_prices_data`
		 SET `manufacturer` = 'JS ASAKASHI'
		 WHERE `id` IN ({$idList})"
	);
	$result['fixes']['dt068_manufacturer'] = (int) $updated;
}

$c110jCaseFix = $pdo->exec(
	"UPDATE `shop_docpart_prices_data`
	 SET `manufacturer` = 'JS ASAKASHI'
	 WHERE UPPER(TRIM(`article`)) = 'C110J'
	   AND UPPER(TRIM(`manufacturer`)) = 'JS ASAKASHI'
	   AND `manufacturer` <> 'JS ASAKASHI'"
);
if ($c110jCaseFix > 0) {
	$result['fixes']['c110j_manufacturer_case'] = (int) $c110jCaseFix;
}

$st = $pdo->query(
	"SELECT d.`price_id`, p.`name` AS `price_list`, d.`manufacturer`, d.`article`, d.`exist`, d.`price`
	 FROM `shop_docpart_prices_data` d
	 INNER JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
	 WHERE UPPER(TRIM(d.`article`)) IN ('C110J', 'DT068')
	 ORDER BY d.`article`, d.`price_id`"
);
$result['price_rows_after'] = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
$result['message'] = 'CHPU storefront DB alignment complete';

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
