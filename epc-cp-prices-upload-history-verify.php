<?php
/**
 * Verify CP price upload history: files, DB rows per list, AJAX smoke.
 * GET: token=epartscart-deploy-2026&host=www.epartscart.com&ajax=1
 */
declare(strict_types=1);

ob_start();
require_once __DIR__ . '/epc_deploy_auth.php';

$token = (string) ($_GET['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	ob_end_clean();
	http_response_code(403);
	header('Content-Type: application/json; charset=utf-8');
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/docpart/docpart_price_upload_history.php';

$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));
if ($host !== '') {
	$_SERVER['HTTP_HOST'] = $host;
}

$DP_Config = new DP_Config();
epc_portal_apply_config($DP_Config);
$backend = (string) $DP_Config->backend_dir;
$doAjax = !isset($_GET['ajax']) || (string) $_GET['ajax'] !== '0';

$result = array(
	'ok' => true,
	'host' => $host,
	'db' => $DP_Config->db,
	'files' => array(),
	'history_by_price_list' => array(),
	'ajax' => array('skipped' => !$doAjax),
);

$requiredFiles = array(
	'history_ajax' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/ajax_epc_price_upload_history.php',
	'history_lib' => __DIR__ . '/content/shop/docpart/docpart_price_upload_history.php',
	'history_config_js' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history_config.php',
	'history_js' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history.js',
	'prices_manager' => __DIR__ . '/' . $backend . '/content/shop/prices_upload/prices_manager.php',
);

foreach ($requiredFiles as $key => $path) {
	$result['files'][$key] = array(
		'exists' => is_file($path),
		'bytes' => is_file($path) ? (int) filesize($path) : 0,
	);
	if (!$result['files'][$key]['exists']) {
		$result['ok'] = false;
	}
}

$mgr = file_get_contents($requiredFiles['prices_manager']);
$result['checks'] = array(
	'has_modal' => stripos($mgr, 'id="epc_price_upload_history_modal"') !== false,
	'has_upload_history_button' => stripos($mgr, 'epcShowAllPriceUploadHistory') !== false,
	'inline_history_script_removed' => stripos($mgr, 'function epcLoadPriceUploadHistory') === false,
	'page_assets_has_history_js' => false,
);

$pageAssets = __DIR__ . '/content/general_pages/epc_cp_page_assets.php';
if (is_file($pageAssets)) {
	$pa = file_get_contents($pageAssets);
	$result['checks']['page_assets_has_history_js'] = stripos($pa, 'epc_prices_upload_history_config.php') !== false;
}
if (!$result['checks']['has_modal'] || !$result['checks']['has_upload_history_button'] || !$result['checks']['page_assets_has_history_js']) {
	$result['ok'] = false;
}

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	ob_end_clean();
	http_response_code(500);
	header('Content-Type: application/json; charset=utf-8');
	exit(json_encode(array('ok' => false, 'message' => 'DB: ' . $e->getMessage())));
}

epc_price_history_ensure_schema($pdo);

$priceLists = $pdo->query(
	'SELECT `id`, `name` FROM `shop_docpart_prices` ORDER BY `id`'
)->fetchAll(PDO::FETCH_ASSOC);

$histRoot = $_SERVER['DOCUMENT_ROOT'] . '/content/files/price_upload_history';
$result['storage'] = array(
	'root_exists' => is_dir($histRoot),
	'root_writable' => is_dir($histRoot) && is_writable($histRoot),
);

foreach ($priceLists as $pl) {
	$pid = (int) $pl['id'];
	$cnt = $pdo->prepare('SELECT COUNT(*) FROM `epc_price_upload_history` WHERE `price_id` = ?');
	$cnt->execute(array($pid));
	$total = (int) $cnt->fetchColumn();

	$latest = $pdo->prepare(
		'SELECT `id`, `created_at`, `original_filename`, `rows_imported`, `status`, `stored_relpath`
		 FROM `epc_price_upload_history` WHERE `price_id` = ? ORDER BY `id` DESC LIMIT 3'
	);
	$latest->execute(array($pid));
	$samples = array();
	while ($row = $latest->fetch(PDO::FETCH_ASSOC)) {
		$path = epc_price_history_file_absolute_path($row);
		$samples[] = array(
			'id' => (int) $row['id'],
			'date' => (string) $row['created_at'],
			'file' => (string) $row['original_filename'],
			'rows_imported' => (int) $row['rows_imported'],
			'status' => (string) $row['status'],
			'file_on_disk' => ($path !== '' && is_file($path)),
		);
	}

	$result['history_by_price_list'][] = array(
		'id' => $pid,
		'name' => (string) $pl['name'],
		'history_count' => $total,
		'samples' => $samples,
	);
}

$result['history_total'] = (int) $pdo->query('SELECT COUNT(*) FROM `epc_price_upload_history`')->fetchColumn();

if ($doAjax) {
	try {
		$rows = epc_price_history_list($pdo, 0, 10);
		$result['ajax'] = array(
			'ok' => true,
			'row_count' => count($rows),
			'sample_ids' => array_map(static function ($r) {
				return (int) ($r['id'] ?? 0);
			}, array_slice($rows, 0, 5)),
		);
	} catch (Throwable $e) {
		$result['ajax'] = array('ok' => false, 'error' => $e->getMessage());
		$result['ok'] = false;
	}
}

ob_end_clean();
header('Content-Type: application/json; charset=utf-8');
echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
