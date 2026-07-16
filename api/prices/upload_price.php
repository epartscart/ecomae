<?php
/**
 * External price upload API.
 * POST: tech_key, id|price_id, document|file|price_file
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();

header('Content-Type: application/json; charset=utf-8');

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password
	);
} catch (PDOException $e) {
	exit(json_encode(['status' => false, 'data' => 'NO DB CONNECT']));
}
$db_link->query('SET NAMES utf8;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
multilang_init();

$techKey = (string) ($_POST['tech_key'] ?? $_POST['key'] ?? '');
if ($techKey === '' || !hash_equals((string) $DP_Config->tech_key, $techKey)) {
	exit(json_encode([
		'status' => false,
		'data' => translate_str_by_id(2056), // wrong key
	]));
}

function epc_api_prices_clear_dir(string $dir, bool $clear_only): void
{
	foreach (glob($dir . '/*') ?: [] as $file) {
		if (is_dir($file)) {
			epc_api_prices_clear_dir($file, false);
			continue;
		}
		$file_name = basename($file);
		if ($file_name !== 'index.html') {
			@unlink($file);
		}
	}
	if (!$clear_only) {
		@rmdir($dir);
	}
}

function epc_api_prices_import_csv_to_db(bool $clean_before, int $price_id): void
{
	global $DP_Config;
	$url = $DP_Config->domain_path . $DP_Config->backend_dir
		. '/content/shop/prices_upload/ajax_5_import_csv_to_db.php?price_id=' . $price_id
		. '&initiator=js&clean_before=' . ($clean_before ? '1' : '0')
		. '&key=' . rawurlencode((string) $DP_Config->tech_key);

	$curl = curl_init();
	if ($curl === false) {
		exit(json_encode([
			'status' => false,
			'data' => translate_str_by_id(2058),
		]));
	}
	curl_setopt_array($curl, [
		CURLOPT_URL => $url,
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_SSL_VERIFYHOST => 0,
		CURLOPT_SSL_VERIFYPEER => 0,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 180,
	]);
	$result = curl_exec($curl);
	curl_close($curl);

	$answer = json_decode((string) $result);
	if (!is_object($answer) || (int) ($answer->result ?? 0) !== 1) {
		exit(json_encode([
			'status' => false,
			'data' => translate_str_by_id(2058),
		]));
	}

	$url = $DP_Config->domain_path . $DP_Config->backend_dir
		. '/content/shop/prices_upload/ajax_6_complete_session.php?price_id=' . $price_id
		. '&key=' . rawurlencode((string) $DP_Config->tech_key);
	$curl = curl_init();
	if ($curl === false) {
		exit(json_encode([
			'status' => false,
			'data' => translate_str_by_id(2057),
		]));
	}
	curl_setopt_array($curl, [
		CURLOPT_URL => $url,
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_SSL_VERIFYHOST => 0,
		CURLOPT_SSL_VERIFYPEER => 0,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 60,
	]);
	$result = curl_exec($curl);
	curl_close($curl);

	$answer = json_decode((string) $result);
	if (!is_object($answer) || (int) ($answer->result ?? 0) !== 1) {
		exit(json_encode([
			'status' => false,
			'data' => translate_str_by_id(2057),
		]));
	}
}

$priceId = (int) ($_POST['id'] ?? $_POST['price_id'] ?? 0);
if ($priceId <= 0) {
	exit(json_encode([
		'status' => false,
		'data' => translate_str_by_id(2060),
	]));
}

$query = $db_link->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ?;');
$query->execute([$priceId]);
$result = $query->fetch(PDO::FETCH_ASSOC);
if (!$result) {
	exit(json_encode([
		'status' => false,
		'data' => translate_str_by_id(2060),
	]));
}

$fileField = null;
foreach (['document', 'file', 'price_file'] as $candidate) {
	if (!empty($_FILES[$candidate]['tmp_name']) && is_uploaded_file((string) $_FILES[$candidate]['tmp_name'])) {
		$fileField = $candidate;
		break;
	}
}
if ($fileField === null) {
	exit(json_encode([
		'status' => false,
		'data' => 'No uploaded file (use document, file, or price_file)',
	]));
}

$treelax_tmp_dir = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . $DP_Config->tmp_dir_prices_upload;
if (!is_dir($treelax_tmp_dir)) {
	if (!@mkdir($treelax_tmp_dir, 0755, true)) {
		exit(json_encode([
			'status' => false,
			'data' => translate_str_by_id(2059),
		]));
	}
} else {
	epc_api_prices_clear_dir($treelax_tmp_dir, true);
}

$origName = basename((string) ($_FILES[$fileField]['name'] ?? 'price.txt'));
if ($origName === '' || $origName === '.' || $origName === '..') {
	$origName = 'price.txt';
}
$dest = $treelax_tmp_dir . '/' . $origName;
if (!move_uploaded_file((string) $_FILES[$fileField]['tmp_name'], $dest)) {
	exit(json_encode([
		'status' => false,
		'data' => 'Could not store uploaded file',
	]));
}

epc_api_prices_import_csv_to_db(true, $priceId);

exit(json_encode([
	'status' => true,
	'data' => translate_str_by_id(2061),
	'price_id' => $priceId,
]));
