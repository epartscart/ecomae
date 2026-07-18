<?php
/**
 * Price-loader settings endpoint.
 * Never returns DB/IMAP passwords by default (set EPC_PRICES_TECH_ALLOW_SECRETS=1 only for trusted private workers).
 */
ob_start();
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');

if (empty($_GET['tech_key']) && empty($_POST['tech_key'])) {
	ob_end_clean();
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();

$techKey = (string) ($_POST['tech_key'] ?? $_GET['tech_key'] ?? '');
if (!is_string($DP_Config->tech_key ?? null) || $DP_Config->tech_key === '' || !hash_equals((string) $DP_Config->tech_key, $techKey)) {
	ob_end_clean();
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

// Optional IP allowlist for this sensitive endpoint
$allowRaw = getenv('EPC_PRICES_TECH_IPS');
if ($allowRaw !== false && trim($allowRaw) !== '') {
	$allowed = array_values(array_filter(array_map('trim', explode(',', $allowRaw))));
	$ip = (string) ($_SERVER['REMOTE_ADDR'] ?? '');
	if ($allowed !== array() && !in_array($ip, $allowed, true)) {
		ob_end_clean();
		exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
	}
}

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password
	);
} catch (PDOException $e) {
	ob_end_clean();
	exit(json_encode(array('status' => false, 'message' => 'Unavailable')));
}
$db_link->query('SET NAMES utf8;');

$allowSecrets = getenv('EPC_PRICES_TECH_ALLOW_SECRETS');
$allowSecrets = ($allowSecrets !== false && $allowSecrets !== '' && $allowSecrets !== '0');

$answer = array();
$answer['db'] = array(
	'host' => $DP_Config->host_external,
	'user' => $DP_Config->user,
	'db' => $DP_Config->db,
);
$answer['prices_email'] = array(
	'prices_email_server' => $DP_Config->prices_email_server,
	'prices_email_encryption' => $DP_Config->prices_email_encryption,
	'prices_email_port' => $DP_Config->prices_email_port,
	'prices_email_username' => $DP_Config->prices_email_username,
);

if ($allowSecrets) {
	$answer['db']['password'] = $DP_Config->password;
	$answer['prices_email']['prices_email_password'] = $DP_Config->prices_email_password;
} else {
	$answer['db']['password'] = null;
	$answer['prices_email']['prices_email_password'] = null;
	$answer['secrets'] = 'redacted';
}

$answer['status'] = true;
$answer['message'] = 'Ok';
ob_end_clean();
exit(json_encode($answer));
