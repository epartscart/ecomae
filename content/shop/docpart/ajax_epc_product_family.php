<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store, no-cache, must-revalidate');

if (session_status() !== PHP_SESSION_ACTIVE) {
	session_start();
}

@set_time_limit(90);

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once __DIR__ . '/epc_product_family.php';

$DP_Config = new DP_Config();
$action = isset($_REQUEST['action']) ? strtolower(trim((string)$_REQUEST['action'])) : 'summary';
$label = isset($_REQUEST['label']) ? trim((string)$_REQUEST['label']) : '';
$brand = isset($_REQUEST['brand']) ? trim((string)$_REQUEST['brand']) : '';
$refresh = isset($_REQUEST['refresh']) && $_REQUEST['refresh'] === '1';
$limit = isset($_REQUEST['limit']) ? (int)$_REQUEST['limit'] : 2500;

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password
	);
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

function epc_pf_cache_file_path(): string
{
	$doc = isset($_SERVER['DOCUMENT_ROOT']) ? (string)$_SERVER['DOCUMENT_ROOT'] : 'epartscart';
	return rtrim(sys_get_temp_dir(), '/\\') . '/epc_pf_catalog_v3_' . md5($doc) . '.json';
}

function epc_pf_read_cache(bool $refresh): ?array
{
	if ($refresh) {
		return null;
	}
	$path = epc_pf_cache_file_path();
	if (!is_readable($path)) {
		return null;
	}
	$raw = @file_get_contents($path);
	if ($raw === false || $raw === '') {
		return null;
	}
	$data = json_decode($raw, true);
	if (!is_array($data) || empty($data['built_at']) || empty($data['products']) || empty($data['summary'])) {
		return null;
	}
	if (time() - (int)$data['built_at'] > 900) {
		return null;
	}
	return $data;
}

function epc_pf_write_cache(array $data): void
{
	$data['built_at'] = time();
	@file_put_contents(epc_pf_cache_file_path(), json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
}

function epc_pf_build_full_catalog(PDO $db, $DP_Config, int $limit): array
{
	$lines = epc_pf_fetch_catalog_lines($db, $limit);
	$catalog = epc_pf_build_catalog_from_lines($lines, $DP_Config, $db, 0);
	return array(
		'products' => epc_pf_products_for_cards($catalog['products']),
		'summary' => $catalog['summary'],
		'product_groups' => isset($catalog['product_groups']) && is_array($catalog['product_groups']) ? $catalog['product_groups'] : array(),
	);
}

function epc_pf_load_catalog(PDO $db, $DP_Config, int $limit, bool $refresh): array
{
	$cached = epc_pf_read_cache($refresh);
	if ($cached !== null) {
		return $cached;
	}

	$cache_key = 'epc_pf_catalog_v3';
	$now = time();
	if (!$refresh && !empty($_SESSION[$cache_key]) && is_array($_SESSION[$cache_key])) {
		$session_cached = $_SESSION[$cache_key];
		if (isset($session_cached['built_at']) && ($now - (int)$session_cached['built_at']) < 600
			&& !empty($session_cached['products']) && !empty($session_cached['summary'])) {
			return $session_cached;
		}
	}

	$built = epc_pf_build_full_catalog($db, $DP_Config, $limit);
	epc_pf_write_cache($built);
	try {
		$_SESSION[$cache_key] = $built;
	} catch (Throwable $e) {
	}
	return $built;
}

try {
	if ($action === 'summary') {
		$catalog = epc_pf_load_catalog($db, $DP_Config, $limit, $refresh);
		echo json_encode(array(
			'status' => true,
			'action' => 'summary',
			'products' => $catalog['products'],
			'summary' => $catalog['summary'],
			'cached' => !$refresh,
		), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		exit;
	}

	if ($action === 'group') {
		$catalog = epc_pf_load_catalog($db, $DP_Config, $limit, false);
		$groups = isset($catalog['product_groups']) && is_array($catalog['product_groups']) ? $catalog['product_groups'] : array();
		$group = epc_pf_find_group($groups, $label);
		if ($group === null) {
			echo json_encode(array('status' => false, 'message' => 'Product family not found'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
			exit;
		}
		$detail = epc_pf_group_detail($group, $brand);
		echo json_encode(array(
			'status' => true,
			'action' => 'group',
			'group' => $detail,
		), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		exit;
	}

	echo json_encode(array('status' => false, 'message' => 'Unknown action'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
} catch (Throwable $e) {
	error_log('[epc_product_family] ' . $e->getMessage() . ' in ' . $e->getFile() . ':' . $e->getLine());
	$payload = array('status' => false, 'message' => 'Catalog error');
	if (!empty($_GET['epc_debug']) && $_GET['epc_debug'] === '1') {
		$payload['debug'] = $e->getMessage();
	}
	echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
}
