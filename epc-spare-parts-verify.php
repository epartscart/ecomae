<?php
/**
 * Verify spare parts page + warehouse search on live tenant.
 * GET ?token=…&brand=Toyota&article=1310154101
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/epc_spare_parts_warehouse.php';

$brand = trim((string) ($_GET['brand'] ?? 'Toyota'));
$article = trim((string) ($_GET['article'] ?? '1310154101'));
$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));

$cfg = new DP_Config();
$epcTenantHostDbFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	if (isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password') as $k) {
			if (!empty($epc_tenant_host_db[$host][$k])) {
				$cfg->$k = $epc_tenant_host_db[$host][$k];
			}
		}
	}
}

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

$contentStmt = $pdo->prepare('SELECT `id`, `url`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
$contentStmt->execute(array('spare-parts'));
$contentRow = $contentStmt->fetch(PDO::FETCH_ASSOC) ?: null;

$search = epc_spare_parts_warehouse_search($brand, $article, $pdo, $cfg);
$brands = epc_spare_parts_oem_brands($pdo);

$pageUrl = 'https://' . $host . '/en/spare-parts';
$pageFetch = array('http' => 0, 'bytes' => 0, 'has_form' => false, 'has_product_lines' => false);
$ch = curl_init($pageUrl);
if ($ch !== false) {
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 45,
		CURLOPT_HTTPHEADER => array('Host: ' . $host),
	));
	$html = (string) curl_exec($ch);
	$pageFetch['http'] = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$pageFetch['bytes'] = strlen($html);
	$pageFetch['has_form'] = stripos($html, 'epc-sp-form') !== false && stripos($html, 'Search warehouse') !== false;
	$pageFetch['has_product_lines'] = stripos($html, 'epc-ep-pl-grid') !== false || stripos($html, 'Shop by product line') !== false;
	curl_close($ch);
}

$homeFetch = array('http' => 0, 'has_spare_link' => false, 'has_product_lines' => false);
$ch2 = curl_init('https://' . $host . '/en/');
if ($ch2 !== false) {
	curl_setopt_array($ch2, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 45,
		CURLOPT_HTTPHEADER => array('Host: ' . $host),
	));
	$homeHtml = (string) curl_exec($ch2);
	$homeFetch['http'] = (int) curl_getinfo($ch2, CURLINFO_HTTP_CODE);
	$homeFetch['has_spare_link'] = stripos($homeHtml, '/spare-parts') !== false;
	$homeFetch['has_product_lines'] = stripos($homeHtml, 'epc-ep-pl-grid') !== false;
	curl_close($ch2);
}

echo json_encode(array(
	'ok' => $contentRow !== null
		&& !empty($search['ok'])
		&& $pageFetch['has_form']
		&& !$pageFetch['has_product_lines'],
	'content' => $contentRow,
	'warehouse_search' => $search,
	'brand_count' => count($brands),
	'page_url' => $pageUrl,
	'page_fetch' => $pageFetch,
	'home_fetch' => $homeFetch,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
