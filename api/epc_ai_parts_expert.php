<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once dirname(__DIR__) . '/config.php';
require_once dirname(__DIR__) . '/content/shop/docpart/epc_ai_parts_expert.php';

$DP_Config = new DP_Config();

if (!epc_ai_expert_enabled($DP_Config)) {
	echo json_encode(array('ok' => false, 'message' => 'AI Parts Expert search is disabled.'), JSON_UNESCAPED_UNICODE);
	exit;
}

$action = isset($_REQUEST['action']) ? trim((string)$_REQUEST['action']) : 'search';

if ($action === 'bootstrap') {
	echo json_encode(array(
		'ok' => true,
		'csrf' => epc_ai_expert_csrf_token($DP_Config),
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

if ($action !== 'search') {
	http_response_code(400);
	echo json_encode(array('ok' => false, 'message' => 'Unknown action.'), JSON_UNESCAPED_UNICODE);
	exit;
}

$csrf = isset($_REQUEST['csrf']) ? trim((string)$_REQUEST['csrf']) : '';
if (!epc_ai_expert_csrf_valid($DP_Config, $csrf)) {
	http_response_code(403);
	echo json_encode(array('ok' => false, 'message' => 'Invalid or expired security token. Refresh the page and try again.'), JSON_UNESCAPED_UNICODE);
	exit;
}

if (epc_ai_expert_rate_limited()) {
	http_response_code(429);
	echo json_encode(array('ok' => false, 'message' => 'Too many lookups. Please wait a minute and try again.'), JSON_UNESCAPED_UNICODE);
	exit;
}

$article = isset($_REQUEST['article']) ? trim((string)$_REQUEST['article']) : '';
$brand = isset($_REQUEST['brand']) ? trim((string)$_REQUEST['brand']) : '';
if ($brand === '' && isset($_REQUEST['manufacturer'])) {
	$brand = trim((string)$_REQUEST['manufacturer']);
}

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password
	);
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
} catch (Throwable $e) {
	http_response_code(503);
	echo json_encode(array('ok' => false, 'message' => 'Database unavailable.'), JSON_UNESCAPED_UNICODE);
	exit;
}

$result = epc_ai_expert_search($db, $DP_Config, $article, $brand);
echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
