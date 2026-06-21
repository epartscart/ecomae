<?php
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: private, max-age=120');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once __DIR__ . '/epc_demand_intelligence.php';
epc_demand_require_customer_login(true);
require_once __DIR__ . '/docpart_article_match.php';

$DP_Config = new DP_Config();
$country = isset($_REQUEST['country']) ? trim((string)$_REQUEST['country']) : '';

try {
	$db = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db->query('SET NAMES utf8;');
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'));
	exit;
}

epc_demand_ensure_schema($db);
$access = epc_demand_access_context($db);
if ($country !== '') {
	$country = epc_demand_assert_country_allowed($db, $country, true);
} elseif (!empty($access['country_locked'])) {
	$country = (string)$access['default_country'];
}
$index = array();
try {
	if ($country !== '') {
		$stmt = $db->prepare(
			'SELECT `manufacturer`, `article_norm`, `country_code` FROM `epc_article_demand` WHERE `country_code` = ?'
		);
		$stmt->execute(array($country));
	} elseif (!empty($access['is_admin'])) {
		$stmt = $db->query('SELECT `manufacturer`, `article_norm`, `country_code` FROM `epc_article_demand`');
	} elseif (!empty($access['allowed_codes'])) {
		$placeholders = implode(',', array_fill(0, count($access['allowed_codes']), '?'));
		$stmt = $db->prepare(
			'SELECT `manufacturer`, `article_norm`, `country_code` FROM `epc_article_demand` WHERE `country_code` IN (' . $placeholders . ')'
		);
		$stmt->execute($access['allowed_codes']);
	} else {
		$stmt = null;
	}
	if ($stmt) {
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$brand = mb_strtoupper(trim((string)$row['manufacturer']), 'UTF-8');
			$norm = trim((string)$row['article_norm']);
			if ($brand === '' || $norm === '') {
				continue;
			}
			$key = $brand . '|' . $norm;
			if (!isset($index[$key])) {
				$index[$key] = array();
			}
			$index[$key][strtoupper((string)$row['country_code'])] = true;
		}
	}
} catch (Exception $e) {
}

$flat = array();
foreach ($index as $key => $codes) {
	$flat[$key] = array_keys($codes);
}

echo json_encode(array(
	'status' => true,
	'country' => $country,
	'index' => $flat,
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
