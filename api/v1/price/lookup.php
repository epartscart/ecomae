<?php
/**
 * Price PRO API (beta) — article lookup by brand + number.
 * GET /api/v1/price/lookup.php?brand=BOSCH&article=0986424590
 * Header: X-API-Key: epc_pricepro_…
 */
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_api_clients.php';

$brand = isset($_GET['brand']) ? trim((string) $_GET['brand']) : '';
$article = isset($_GET['article']) ? trim((string) $_GET['article']) : '';

$client = epc_api_client_require_auth('price_pro', 'lookup');

if ($brand === '' || $article === '') {
	epc_api_clients_json_error(400, 'missing_params', 'Query params brand and article are required.');
}

$pdo = epc_api_clients_platform_pdo();
if ($pdo instanceof PDO) {
	epc_api_clients_log_usage($pdo, (int) $client['id'], array(
		'action' => 'price_lookup',
		'request_path' => '/api/v1/price/lookup',
		'http_status' => 200,
		'message' => $brand . '/' . $article,
	));
}

$offers = array();
$betaNote = 'Price PRO beta — supplier feeds are enabled per account. Contact sales for full enablement.';

$configPath = $_SERVER['DOCUMENT_ROOT'] . '/config.php';
if (is_file($configPath)) {
	require_once $configPath;
	if (class_exists('DP_Config')) {
		$cfg = new DP_Config();
		try {
			$db = new PDO(
				'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
				(string) $cfg->user,
				(string) $cfg->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
			);
			$brandUpper = mb_strtoupper($brand, 'UTF-8');
			$articleNorm = preg_replace('/\s+/', '', strtoupper($article));
			$stmt = $db->prepare(
				'SELECT `manufacturer`, COALESCE(NULLIF(`article_show`, \'\'), `article`) AS `article`,
				        `name`, `price`, `exist`, `storage`, `time_to_exe`
				 FROM `shop_docpart_prices_data`
				 WHERE UPPER(TRIM(`manufacturer`)) = ?
				   AND (UPPER(REPLACE(`article`, \' \', \'\')) = ? OR UPPER(REPLACE(COALESCE(`article_show`, `article`), \' \', \'\')) = ?)
				   AND IFNULL(`price`, 0) > 0
				 ORDER BY `price` ASC
				 LIMIT 25'
			);
			$stmt->execute(array($brandUpper, $articleNorm, $articleNorm));
			while ($row = $stmt->fetch()) {
				$offers[] = array(
					'supplier' => (string) ($row['storage'] ?? 'default'),
					'brand' => (string) ($row['manufacturer'] ?? $brand),
					'article' => (string) ($row['article'] ?? $article),
					'name' => (string) ($row['name'] ?? ''),
					'price' => (float) ($row['price'] ?? 0),
					'currency' => 'AED',
					'stock_hint' => (int) ($row['exist'] ?? 0),
					'lead_time' => (string) ($row['time_to_exe'] ?? ''),
				);
			}
			if ($offers) {
				$betaNote = '';
			}
		} catch (Exception $e) {
		}
	}
}

echo json_encode(array(
	'ok' => true,
	'beta' => true,
	'brand' => $brand,
	'article' => $article,
	'offers' => $offers,
	'message' => $betaNote,
	'client' => array(
		'label' => (string) ($client['label'] ?? ''),
		'key_prefix' => (string) ($client['client_key_prefix'] ?? ''),
	),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
