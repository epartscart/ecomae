<?php
/**
 * Verify epartscart prices page: tenant DB, counts, pyprices, CSRF bootstrap.
 * GET: token=epartscart-deploy-2026&host=www.epartscart.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');

$token = (string) ($_GET['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';

$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));
if ($host !== '') {
	$_SERVER['HTTP_HOST'] = $host;
}

$DP_Config = new DP_Config();
epc_portal_apply_config($DP_Config);

$result = array(
	'ok' => true,
	'host' => $host,
	'db' => $DP_Config->db,
	'domain_path' => $DP_Config->domain_path,
	'counts' => array(),
	'price_lists' => array(),
	'pyprices' => array(),
	'files' => array(
		'stop_csrf_has_epc_csrf' => is_file(__DIR__ . '/content/users/stop_csrf.php')
			&& str_contains((string) file_get_contents(__DIR__ . '/content/users/stop_csrf.php'), 'epc_csrf_should_use_admin_session'),
		'pyprices_api_php' => is_file(__DIR__ . '/pyprices/pyprices-api.php'),
		'pyprices_tables_cleaner' => is_file(__DIR__ . '/cp/content/shop/prices_upload/for_pyprices/pyprices_tables_cleaner.php'),
		'epc_prices_ajax_init' => is_file(__DIR__ . '/cp/content/shop/prices_upload/epc_prices_ajax_init.php'),
	),
);

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'DB: ' . $e->getMessage())));
}

foreach (array('shop_docpart_prices', 'shop_docpart_prices_data', 'epc_price_upload_history') as $tbl) {
	try {
		$result['counts'][$tbl] = (int) $pdo->query('SELECT COUNT(*) FROM `' . $tbl . '`')->fetchColumn();
	} catch (Throwable $e) {
		$result['counts'][$tbl] = null;
	}
}

$result['price_lists'] = $pdo->query(
	'SELECT p.`id`, p.`name`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `rows`
	 FROM `shop_docpart_prices` p ORDER BY p.`id`'
)->fetchAll(PDO::FETCH_ASSOC);

$postdata = http_build_query(array('key' => $DP_Config->tech_key, 'just_test_db' => 'yes'));
$ch = curl_init($DP_Config->domain_path . 'pyprices/pyprices-api.php');
curl_setopt_array($ch, array(
	CURLOPT_RETURNTRANSFER => true,
	CURLOPT_POST => true,
	CURLOPT_POSTFIELDS => $postdata,
	CURLOPT_SSL_VERIFYPEER => false,
	CURLOPT_SSL_VERIFYHOST => false,
	CURLOPT_TIMEOUT => 30,
));
$pyBody = curl_exec($ch);
$pyCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
curl_close($ch);
$pyJson = json_decode(trim((string) $pyBody), true);
$result['pyprices'] = array(
	'http_code' => $pyCode,
	'status' => is_array($pyJson) ? ($pyJson['status'] ?? null) : null,
	'message' => is_array($pyJson) ? ($pyJson['message'] ?? null) : substr((string) $pyBody, 0, 200),
);

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
