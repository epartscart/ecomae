<?php
/**
 * Public (storefront) lookup for CP-managed SKU photos & specifications.
 * GET: action=lookup&brand=&article=&product_id=
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: public, max-age=120');

$root = isset($_SERVER['DOCUMENT_ROOT']) ? (string) $_SERVER['DOCUMENT_ROOT'] : dirname(__DIR__, 4);
if (!is_file($root . '/config.php') && is_file(dirname(__DIR__, 3) . '/config.php')) {
	$root = dirname(__DIR__, 3);
}
$_SERVER['DOCUMENT_ROOT'] = $root;

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once $root . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

$tenantFile = $root . '/config.tenant-host-db.php';
if (is_file($tenantFile)) {
	$epc_tenant_host_db = null;
	require $tenantFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password', 'host') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($DP_Config, $epcTk)) {
				$DP_Config->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}
if (is_file($root . '/content/general_pages/epc_portal.php')) {
	require_once $root . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		try {
			epc_portal_apply_config($DP_Config);
		} catch (Throwable $e) {
		}
		$GLOBALS['DP_Config'] = $DP_Config;
	}
}

try {
	$dbHost = trim((string) ($DP_Config->host ?? ''));
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		(string) $DP_Config->user,
		(string) $DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo json_encode(array('ok' => false, 'error' => 'No database', 'url' => '', 'photos' => array(), 'specs' => array()));
	exit;
}

require_once $root . '/content/shop/catalogue/epc_sku_media.php';

$action = (string) ($_GET['action'] ?? $_POST['action'] ?? 'lookup');
$brand = (string) ($_GET['brand'] ?? $_POST['brand'] ?? '');
$article = (string) ($_GET['article'] ?? $_POST['article'] ?? '');
$productId = (int) ($_GET['product_id'] ?? $_POST['product_id'] ?? 0);

if ($action !== 'lookup') {
	echo json_encode(array('ok' => false, 'error' => 'Unknown action'));
	exit;
}

try {
	epc_sku_media_ensure_schema($db_link);
	echo json_encode(epc_sku_media_public_lookup($db_link, $brand, $article, $productId));
} catch (Throwable $e) {
	echo json_encode(array('ok' => false, 'error' => 'Lookup failed', 'url' => '', 'photos' => array(), 'specs' => array()));
}
