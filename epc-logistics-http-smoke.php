<?php
/**
 * HTTP smoke for logistics CP pages using latest admin session.
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
$db = new PDO(
	'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
	$DP_Config->user,
	$DP_Config->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$db->query('SET NAMES utf8');

$st = $db->query(
	'SELECT s.`session`, s.`2fa_session`, s.`user_id`, u.`email`
	 FROM `sessions` s
	 INNER JOIN `users` u ON u.`user_id` = s.`user_id`
	 WHERE s.`type` = 1
	 ORDER BY s.`last_activiti_time` DESC LIMIT 1'
);
$row = $st->fetch(PDO::FETCH_ASSOC);
if (!$row) {
	exit(json_encode(array('status' => false, 'message' => 'No active CP admin session in DB')));
}

$host = 'www.epartscart.com';
$backend = $DP_Config->backend_dir;
$cookie = 'admin_session=' . urlencode($row['session']) . '; admin_u_id=' . (int)$row['user_id'];
if (!empty($row['2fa_session'])) {
	$cookie .= '; 2fa=' . urlencode($row['2fa_session']);
}

$paths = array(
	'carriers' => '/' . $backend . '/shop/logistics/carriers',
	'guide' => '/' . $backend . '/shop/logistics/guide',
	'hub' => '/' . $backend . '/shop/logistics',
);

$out = array(
	'status' => true,
	'session' => array('email' => $row['email'], 'user_id' => (int)$row['user_id'], 'has_2fa' => !empty($row['2fa_session'])),
	'pages' => array(),
);

foreach ($paths as $key => $path) {
	$url = 'https://' . $host . $path;
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 45,
		CURLOPT_HTTPHEADER => array('Cookie: ' . $cookie),
		CURLOPT_SSL_VERIFYPEER => false,
	));
	$html = curl_exec($ch);
	$code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
	$hasLogin = stripos($html, 'Log in form') !== false || stripos($html, 'id="login_form"') !== false;
	$hasGuide = stripos($html, 'step-by-step guide') !== false || stripos($html, 'Logistics — step-by-step') !== false;
	$hasCarriers = stripos($html, 'carriers &amp; shipments') !== false || stripos($html, 'Carrier accounts') !== false;
	$has403 = stripos($html, '403') !== false;
	$fatal = preg_match('/Fatal error|Parse error|Uncaught/i', $html) ? true : false;
	$out['pages'][$key] = array(
		'path' => $path,
		'http' => $code,
		'bytes' => strlen($html),
		'login_page' => $hasLogin,
		'has_guide_content' => $hasGuide,
		'has_carriers_content' => $hasCarriers,
		'has_403' => $has403,
		'php_fatal' => $fatal,
		'title_snippet' => preg_match('/<title>([^<]+)</i', $html, $m) ? $m[1] : '',
	);
}

echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
