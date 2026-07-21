<?php
/**
 * Preview / import demand-countries CSV + overview stats for AI Parts module.
 */
header('Content-Type: application/json; charset=utf-8');
ini_set('display_errors', '0');
set_time_limit(180);
if (ob_get_level()) {
	@ob_end_clean();
}

try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;
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
	$GLOBALS['db_link'] = $db_link;
	$db_link->query('SET NAMES utf8mb4');
} catch (Throwable $e) {
	http_response_code(503);
	exit(json_encode(array('status' => false, 'message' => 'No DB connect')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'forbidden')));
}

$csrf_check_admin = true;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_demand_intelligence.php';

$action = isset($_POST['action']) ? (string) $_POST['action'] : (isset($_GET['action']) ? (string) $_GET['action'] : '');
$mode = isset($_POST['mode']) ? (string) $_POST['mode'] : 'merge';
$file_path = isset($_POST['file_full_path']) ? (string) $_POST['file_full_path'] : '';

try {
	epc_demand_ensure_schema($db_link);
} catch (Throwable $e) {
	exit(json_encode(array('status' => false, 'message' => 'Schema error')));
}

if ($action === 'stats') {
	$registry = epc_demand_country_registry();
	$byCountry = array();
	try {
		$st = $db_link->query(
			'SELECT `country_code`, COUNT(*) AS `cnt`, COUNT(DISTINCT CONCAT(UPPER(`manufacturer`), \'|\', `article_norm`)) AS `parts`
			 FROM `epc_article_demand` GROUP BY `country_code` ORDER BY `cnt` DESC'
		);
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$code = (string) $row['country_code'];
			$byCountry[] = array(
				'code' => $code,
				'name' => isset($registry[$code]['name']) ? (string) $registry[$code]['name'] : $code,
				'tags' => (int) $row['cnt'],
				'parts' => (int) $row['parts'],
			);
		}
	} catch (Throwable $e) {
	}
	$totalTags = 0;
	$totalParts = 0;
	try {
		$totalTags = (int) $db_link->query('SELECT COUNT(*) FROM `epc_article_demand`')->fetchColumn();
		$totalParts = (int) $db_link->query('SELECT COUNT(DISTINCT CONCAT(UPPER(`manufacturer`), \'|\', `article_norm`)) FROM `epc_article_demand`')->fetchColumn();
	} catch (Throwable $e) {
	}
	$demandMarkets = array();
	foreach ($registry as $code => $meta) {
		if (epc_demand_is_stock_pool_country_code($code)) {
			continue;
		}
		$demandMarkets[] = array('code' => $code, 'name' => (string) $meta['name']);
	}
	exit(json_encode(array(
		'status' => true,
		'stats' => array(
			'total_tags' => $totalTags,
			'total_parts' => $totalParts,
			'by_country' => $byCountry,
			'markets' => $demandMarkets,
		),
	), JSON_UNESCAPED_UNICODE));
}

if ($file_path === '') {
	exit(json_encode(array('status' => false, 'message' => 'Missing file path')));
}

$file_name = basename($file_path);
$tmpRoot = realpath($_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/tmp');
$fileReal = realpath($file_path);
if ($tmpRoot === false || $fileReal === false || strpos($fileReal, $tmpRoot) !== 0 || !is_file($fileReal)) {
	// Legacy equality check used by older uploads
	$expected = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/tmp/' . $file_name;
	if ($expected !== $file_path || strpos($file_path, '..') !== false || !is_readable($file_path)) {
		exit(json_encode(array('status' => false, 'message' => 'Invalid file path')));
	}
	$fileReal = $file_path;
}

if ($action === 'preview') {
	$result = epc_demand_csv_preview_file($fileReal, 40);
	// Flag UAE/ARE stock-pool codes in preview rows
	if (!empty($result['rows']) && is_array($result['rows'])) {
		foreach ($result['rows'] as &$row) {
			$codes = epc_demand_parse_country_codes_string((string) ($row['countries'] ?? ''));
			foreach ($codes as $code) {
				if (epc_demand_is_stock_pool_country_code($code)) {
					if (!isset($row['errors']) || !is_array($row['errors'])) {
						$row['errors'] = array();
					}
					$row['errors'][] = $code . ' is UAE stock pool — not a demand market';
				}
			}
		}
		unset($row);
	}
	exit(json_encode($result, JSON_UNESCAPED_UNICODE));
}

if ($action === 'import') {
	$result = epc_demand_csv_import_file($db_link, $fileReal, $mode === 'replace' ? 'replace' : 'merge');
	@unlink($fileReal);
	exit(json_encode($result, JSON_UNESCAPED_UNICODE));
}

exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
