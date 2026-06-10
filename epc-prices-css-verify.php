<?php
/**
 * Verify prices CP pages: CSS in head, no raw CSS in body.
 * GET: token=epartscart-deploy-2026, key=tech_key
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Invalid key')));
}

$db = new PDO(
	'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
	$DP_Config->user,
	$DP_Config->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$db->query('SET NAMES utf8');

$st = $db->query(
	'SELECT s.`session`, s.`user_id` FROM `sessions` s
	 WHERE s.`type` = 1 AND s.`user_id` = 5
	 ORDER BY s.`last_activiti_time` DESC LIMIT 1'
);
$row = $st->fetch(PDO::FETCH_ASSOC);
if (!$row) {
	exit(json_encode(array('status' => false, 'message' => 'No admin session')));
}

$cookie = 'admin_session=' . urlencode($row['session']) . '; admin_u_id=' . (int)$row['user_id'];
$paths = array(
	'/cp/shop/prices/prices_edit' => 'epc_prices_edit_css.php',
	'/cp/shop/prices' => 'epc_prices_cp_css.php',
);

$out = array('status' => true, 'pages' => array());
foreach ($paths as $path => $cssNeedle) {
	$ch = curl_init('https://www.epartscart.com' . $path);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 60,
		CURLOPT_HTTPHEADER => array('Cookie: ' . $cookie),
		CURLOPT_SSL_VERIFYPEER => false,
	));
	$html = (string)curl_exec($ch);
	$code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);

	$out['pages'][$path] = array(
		'http' => $code,
		'bytes' => strlen($html),
		'css_link_in_head' => (strpos($html, $cssNeedle) !== false),
		'raw_css_visible' => (bool)preg_match('/\.bgtd\{\s*background/', $html),
		'inline_style_bgtd' => (bool)preg_match('/<style[^>]*>\s*\.bgtd\{/', $html),
		'has_edit_content' => (strpos($html, 'Edit price list records') !== false),
		'has_prices_content' => (strpos($html, 'Price lists uploading') !== false || strpos($html, 'prices_manager') !== false),
		'php_fatal' => (bool)preg_match('/Fatal error|Parse error|Uncaught/i', $html),
	);
}

echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
