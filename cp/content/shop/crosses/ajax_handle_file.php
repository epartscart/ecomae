<?php
/**
 * Import crosses CSV into shop_docpart_articles_analogs_list.
 * Expected columns (semicolon or comma): manufacturer;article;manufacturer_cross;article_cross
 * Header row optional.
 */
header('Content-Type: application/json; charset=utf-8');
ini_set('display_errors', '0');
set_time_limit(300);
if (ob_get_level()) {
	@ob_end_clean();
}

function epc_crosses_csv_prepare_string($string)
{
	$sweep = array('#', '`', "\r\n", "\r", "\n", "\t", "'", '"');
	return trim(str_replace($sweep, '', (string) $string));
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
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_cross_interchange.php';

$raw = (string) ($_POST['import_options'] ?? '');
$import_options = json_decode($raw, true);
if (!is_array($import_options)) {
	$import_options = json_decode(urldecode($raw), true);
}
if (!is_array($import_options) || empty($import_options['file_full_path'])) {
	exit(json_encode(array('status' => false, 'message' => 'bad_request')));
}

$file = (string) $import_options['file_full_path'];
$tmpRoot = realpath($_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/tmp');
$fileReal = realpath($file);
if ($tmpRoot === false || $fileReal === false || strpos($fileReal, $tmpRoot) !== 0 || !is_file($fileReal)) {
	exit(json_encode(array('status' => false, 'message' => 'Invalid file path')));
}

$fh = fopen($fileReal, 'r');
if ($fh === false) {
	exit(json_encode(array('status' => false, 'message' => 'Cannot open file')));
}

$sweep = array(' ', '-', '_', '`', '/', "'", '"', '\\', '.', ',', '#', "\r\n", "\r", "\n", "\t");
$inserted = 0;
$skipped = 0;
$errors = 0;
$rowNum = 0;
$maxRows = 50000;

$first = fgets($fh);
if ($first === false) {
	fclose($fh);
	@unlink($fileReal);
	exit(json_encode(array('status' => false, 'message' => 'Empty CSV')));
}
$delimiter = (substr_count($first, ';') >= substr_count($first, ',')) ? ';' : ',';
rewind($fh);

while (($cols = fgetcsv($fh, 0, $delimiter)) !== false) {
	$rowNum++;
	if ($rowNum > $maxRows) {
		break;
	}
	if (!is_array($cols) || count($cols) < 4) {
		$skipped++;
		continue;
	}
	$c0 = trim((string) $cols[0]);
	$c1 = trim((string) $cols[1]);
	$c2 = trim((string) $cols[2]);
	$c3 = trim((string) $cols[3]);
	$headerProbe = strtolower($c0 . $c1 . $c2 . $c3);
	if ($rowNum === 1 && (strpos($headerProbe, 'manufacturer') !== false || strpos($headerProbe, 'article') !== false)) {
		continue;
	}

	$manufacturer_article = docpart_cross_prepare_brand_name(epc_crosses_csv_prepare_string($c0));
	$article = strip_tags(mb_strtoupper(str_replace($sweep, '', $c1), 'UTF-8'));
	$manufacturer_analog = docpart_cross_prepare_brand_name(epc_crosses_csv_prepare_string($c2));
	$analog = strip_tags(mb_strtoupper(str_replace($sweep, '', $c3), 'UTF-8'));

	if ($article === '' || $analog === '' || $manufacturer_article === '' || $manufacturer_analog === '') {
		$skipped++;
		continue;
	}
	try {
		$n = docpart_cross_persist_interchange_pair_bidirectional(
			$db_link,
			$article,
			$manufacturer_article,
			$analog,
			$manufacturer_analog
		);
		if ($n > 0) {
			$inserted += $n;
		} else {
			$skipped++;
		}
	} catch (Throwable $e) {
		$errors++;
	}
}
fclose($fh);
@unlink($fileReal);

exit(json_encode(array(
	'status' => true,
	'message' => 'Imported ' . $inserted . ' link(s); skipped ' . $skipped . '; errors ' . $errors,
	'inserted' => $inserted,
	'skipped' => $skipped,
	'errors' => $errors,
)));
