<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/epc_demand_intelligence.php';
epc_demand_bootstrap_db_link();
epc_demand_require_customer_login(true);
$action = isset($_REQUEST['action']) ? strtolower(trim((string)$_REQUEST['action'])) : 'start';
$country = isset($_REQUEST['country']) ? trim((string)$_REQUEST['country']) : '';
$job_id = isset($_REQUEST['job_id']) ? trim((string)$_REQUEST['job_id']) : '';
$limit = isset($_REQUEST['limit']) ? (int)$_REQUEST['limit'] : 40;
$batch = isset($_REQUEST['batch']) ? (int)$_REQUEST['batch'] : 2;
$seed = isset($_REQUEST['seed']) && $_REQUEST['seed'] === '1';
$require_stock = !isset($_REQUEST['require_stock']) || $_REQUEST['require_stock'] !== '0';

try {
	$db = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db->query('SET NAMES utf8;');
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

if ($seed) {
	epc_demand_build_showcase($db, $DP_Config, 10, true);
}

if ($action === 'start') {
	$country_code = epc_demand_assert_country_allowed($db, $country, true);
	if ($country_code === '') {
		echo json_encode(array('status' => false, 'message' => 'Unknown country'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		exit;
	}
	$parts = epc_demand_country_price_list_parts($db, $DP_Config, $country_code, $limit, $require_stock);
	$job_id = 'di_' . $country_code . '_' . substr(sha1(uniqid((string)mt_rand(), true)), 0, 16);
	$job = array(
		'job_id' => $job_id,
		'country_code' => $country_code,
		'parts' => $parts,
		'parts_total' => count($parts),
		'cursor' => 0,
		'vehicles' => array(),
		'part_lines' => array(),
		'product_groups' => array(),
		'done' => false,
		'started_at' => time(),
		'updated_at' => time(),
		'current_part' => '',
	);
	if (!epc_demand_vehicle_job_write($job_id, $job)) {
		echo json_encode(array('status' => false, 'message' => 'Could not create job'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		exit;
	}
	echo json_encode(array(
		'status' => true,
		'job_id' => $job_id,
		'country_code' => $country_code,
		'parts_total' => count($parts),
		'parts_scanned' => 0,
		'progress' => count($parts) > 0 ? 0 : 100,
		'done' => count($parts) === 0,
		'vehicles' => array(),
		'part_lines' => array(),
		'products' => array(),
		'summary' => array(
			'country_code' => $country_code,
			'country_name' => epc_demand_country_registry()[$country_code]['name'] ?? $country_code,
			'parts_count' => count($parts),
			'vehicles_count' => 0,
			'product_groups_count' => 0,
			'makes_count' => 0,
			'total_stock_qty' => 0,
		),
		'message' => count($parts) > 0
			? 'Found ' . count($parts) . ' price-list parts for this country. Loading products & fitment…'
			: 'No in-stock price-list parts with demand tag for this country.',
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

if ($action === 'step') {
	$job = epc_demand_vehicle_job_read($job_id);
	if ($job === null) {
		echo json_encode(array('status' => false, 'message' => 'Job not found or expired'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		exit;
	}
	$result = epc_demand_vehicle_job_step($job, $DP_Config, $batch);
	epc_demand_vehicle_job_write($job_id, $result['job']);
	$parts_total = (int)($result['job']['parts_total'] ?? 0);
	$cursor = (int)($result['job']['cursor'] ?? 0);
	$progress = $parts_total > 0 ? (int)round(($cursor / $parts_total) * 100) : 100;
	echo json_encode(array(
		'status' => true,
		'job_id' => $job_id,
		'parts_total' => $parts_total,
		'parts_scanned' => $cursor,
		'progress' => $progress,
		'done' => $result['done'],
		'current_part' => (string)($result['job']['current_part'] ?? ''),
		'vehicles' => $result['vehicles'],
		'vehicle_count' => count($result['vehicles']),
		'part_lines' => $result['part_lines'],
		'products' => $result['products'],
		'summary' => $result['summary'],
		'message' => $result['done']
			? 'Scan complete — products and vehicles ready.'
			: ('Checking part ' . $cursor . ' / ' . $parts_total . '…'),
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

echo json_encode(array('status' => false, 'message' => 'Unknown action'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
