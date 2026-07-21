<?php
/**
 * Download CP crosses as CSV (manufacturer;article;manufacturer_cross;article_cross).
 */
ini_set('display_errors', '0');
set_time_limit(180);
if (ob_get_level()) {
	@ob_end_clean();
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

try {
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
	header('Content-Type: text/plain; charset=utf-8');
	exit('No DB connect');
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	header('Content-Type: text/plain; charset=utf-8');
	exit('forbidden');
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

$limit = 100000;
$filename = 'crosses_' . date('Ymd_His') . '.csv';
header('Content-Type: text/csv; charset=utf-8');
header('Content-Disposition: attachment; filename="' . $filename . '"');
header('Pragma: no-cache');
header('Expires: 0');

$out = fopen('php://output', 'w');
fputcsv($out, array('manufacturer', 'article', 'manufacturer_cross', 'article_cross'), ';');

$sql = 'SELECT `manufacturer_article`, `article`, `manufacturer_analog`, `analog`
	FROM `shop_docpart_articles_analogs_list`
	ORDER BY `id` DESC
	LIMIT ' . (int) $limit;
$stmt = $db_link->query($sql);
while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
	fputcsv($out, array(
		(string) $row['manufacturer_article'],
		(string) $row['article'],
		(string) $row['manufacturer_analog'],
		(string) $row['analog'],
	), ';');
}
fclose($out);
exit;
